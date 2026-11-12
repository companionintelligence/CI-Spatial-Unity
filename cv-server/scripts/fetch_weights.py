#!/usr/bin/env python3
"""Fetch the GroundingDINO and SAM 2 files the CV server loads, and verify them.

Every file has pinned URLs (Hugging Face URLs pinned to a repo commit, GitHub URLs
to a commit or release tag), tried in order, and a pinned SHA-256 and size. Files
land where the server reads them: the GDINO_CONFIG_PATH, GDINO_WEIGHTS_PATH and
SAM2_WEIGHTS_PATH settings, resolved against --root when relative (by default
configs/ and checkpoints/ under cv-server/, or under /app in the image).

* A file already in place with the pinned checksum is kept; nothing is downloaded.
* A file in place with a different checksum is an error unless --force is given,
  because this script did not put it there.
* Downloads stream into a temporary file next to the target and are moved into
  place only after size and checksum match, so an interrupted or tampered
  download never leaves a usable-looking file behind.

The container entrypoint runs this on start; the image build can run it too
(--build-arg FETCH_WEIGHTS_AT_BUILD=1). Florence-2 is downloaded by transformers
from the Hugging Face hub at startup and is not handled here.

Standard library only, so it runs before (or without) the server's dependencies.

Usage:
    python scripts/fetch_weights.py [--root DIR] [--check] [--force]
"""

from __future__ import annotations

import argparse
import hashlib
import http.client
import os
import sys
import tempfile
import time
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Mapping, Optional, Sequence, Tuple

CHUNK_SIZE = 1 << 20
USER_AGENT = "cv-server-fetch-weights/1.0"

Log = Callable[[str], None]


@dataclass(frozen=True)
class Artifact:
    name: str
    env_var: str  # server setting that points at the file
    default_path: str  # used when env_var is unset; relative to --root
    sha256: str
    size: int
    urls: Tuple[str, ...]  # tried in order; all serve the same bytes


# The files the server's default configuration loads. This pins where they come
# from; it does not choose models.
MANIFEST: Tuple[Artifact, ...] = (
    Artifact(
        name="groundingdino-swint-ogc-config",
        env_var="GDINO_CONFIG_PATH",
        default_path="configs/GroundingDINO_SwinT_OGC.py",
        sha256="172e80017f9395668a9cb5d1b8bd9d061c0e360471c6ed673c83b69bb14399f1",
        size=1006,
        urls=(
            "https://raw.githubusercontent.com/IDEA-Research/GroundingDINO/"
            "856dde20aee659246248e20734ef9ba5214f5e44/groundingdino/config/GroundingDINO_SwinT_OGC.py",
            "https://huggingface.co/ShilongLiu/GroundingDINO/resolve/"
            "a94c9b567a2a374598f05c584e96798a170c56fb/GroundingDINO_SwinT_OGC.cfg.py",
        ),
    ),
    Artifact(
        name="groundingdino-swint-ogc-weights",
        env_var="GDINO_WEIGHTS_PATH",
        default_path="checkpoints/groundingdino_swint_ogc.pth",
        sha256="3b3ca2563c77c69f651d7bd133e97139c186df06231157a64c507099c52bc799",
        size=693997677,
        urls=(
            "https://huggingface.co/ShilongLiu/GroundingDINO/resolve/"
            "a94c9b567a2a374598f05c584e96798a170c56fb/groundingdino_swint_ogc.pth",
            "https://github.com/IDEA-Research/GroundingDINO/releases/download/"
            "v0.1.0-alpha/groundingdino_swint_ogc.pth",
        ),
    ),
    Artifact(
        name="sam2.1-hiera-tiny-weights",
        env_var="SAM2_WEIGHTS_PATH",
        default_path="checkpoints/sam2.1_hiera_tiny.pt",
        sha256="7402e0d864fa82708a20fbd15bc84245c2f26dff0eb43a4b5b93452deb34be69",
        size=156008466,
        urls=(
            "https://huggingface.co/facebook/sam2.1-hiera-tiny/resolve/"
            "de431c4043854a71d8101e17995dfe596bf101a5/sam2.1_hiera_tiny.pt",
            "https://dl.fbaipublicfiles.com/segment_anything_2/092824/sam2.1_hiera_tiny.pt",
        ),
    ),
)


class FetchError(Exception):
    """A file could not be put in place with the pinned checksum."""


class ChecksumMismatch(FetchError):
    """A source served different bytes; retrying the same URL will not help."""


def _print(message: str) -> None:
    print(message, flush=True)


def sha256_of(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(CHUNK_SIZE), b""):
            digest.update(chunk)
    return digest.hexdigest()


def destination(artifact: Artifact, root: Path, environ: Mapping[str, str] = os.environ) -> Path:
    path = Path(environ.get(artifact.env_var) or artifact.default_path)
    return path if path.is_absolute() else root / path


def matches(path: Path, artifact: Artifact) -> bool:
    return (
        path.is_file()
        and path.stat().st_size == artifact.size
        and sha256_of(path) == artifact.sha256
    )


def download(url: str, artifact: Artifact, directory: Path, timeout: float, log: Log) -> Path:
    """Stream ``url`` into a temporary file in ``directory`` and return it once verified."""
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    fd, name = tempfile.mkstemp(dir=directory, prefix=f".{artifact.name}-", suffix=".part")
    tmp_path = Path(name)
    try:
        digest = hashlib.sha256()
        received = 0
        report_every = max(artifact.size // 10, 32 * CHUNK_SIZE)
        next_report = report_every
        with os.fdopen(fd, "wb") as out, urllib.request.urlopen(request, timeout=timeout) as response:
            while True:
                chunk = response.read(CHUNK_SIZE)
                if not chunk:
                    break
                received += len(chunk)
                if received > artifact.size:
                    raise ChecksumMismatch(f"{url} sent more than the pinned {artifact.size} bytes")
                digest.update(chunk)
                out.write(chunk)
                if received >= next_report:
                    log(f"         {artifact.name}: {received * 100 // artifact.size}%")
                    next_report += report_every
        if received != artifact.size:
            # A short body is usually a dropped connection, so it is worth a retry.
            raise FetchError(f"{url} sent {received} bytes, expected {artifact.size}")
        actual = digest.hexdigest()
        if actual != artifact.sha256:
            raise ChecksumMismatch(f"{url} has SHA-256 {actual}, expected {artifact.sha256}")
        os.chmod(tmp_path, 0o644)
        return tmp_path
    except BaseException:
        tmp_path.unlink(missing_ok=True)
        raise


def ensure(
    artifact: Artifact,
    root: Path,
    *,
    force: bool = False,
    check_only: bool = False,
    attempts: int = 3,
    timeout: float = 60.0,
    retry_delay: float = 2.0,
    environ: Mapping[str, str] = os.environ,
    log: Log = _print,
) -> bool:
    """Make sure ``artifact`` is in place and verified.

    Returns False only in ``check_only`` mode when the file is missing or differs;
    otherwise raises FetchError when the file cannot be put in place.
    """
    target = destination(artifact, root, environ)
    if target.exists():
        if matches(target, artifact):
            log(f"ok       {artifact.name}: {target}")
            return True
        if check_only:
            log(f"MISMATCH {artifact.name}: {target} does not match the pinned SHA-256")
            return False
        if not force:
            raise FetchError(
                f"{target} exists but does not match the pinned SHA-256 for {artifact.name}. "
                "Move it aside, or rerun with --force to replace it."
            )
        log(f"replace  {artifact.name}: {target} does not match the pinned SHA-256 (--force)")
    elif check_only:
        log(f"MISSING  {artifact.name}: {target}")
        return False

    target.parent.mkdir(parents=True, exist_ok=True)
    failures = []
    for url in artifact.urls:
        for attempt in range(1, attempts + 1):
            log(f"fetch    {artifact.name}: {url} (attempt {attempt}/{attempts})")
            try:
                tmp_path = download(url, artifact, target.parent, timeout, log)
            except ChecksumMismatch as exc:
                failures.append(str(exc))
                break
            except (OSError, http.client.HTTPException, FetchError) as exc:
                failures.append(f"{url}: {exc}")
                if attempt < attempts:
                    time.sleep(retry_delay * attempt)
                continue
            os.replace(tmp_path, target)
            log(f"ok       {artifact.name}: {target}")
            return True
    raise FetchError(f"could not fetch {artifact.name}:\n  " + "\n  ".join(failures))


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        description="Fetch and verify the GroundingDINO and SAM 2 files the CV server loads."
    )
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(__file__).resolve().parent.parent,
        help="directory that relative paths resolve against (default: cv-server/; /app in the image)",
    )
    parser.add_argument(
        "--check", action="store_true", help="only verify; exit 1 if a file is missing or differs"
    )
    parser.add_argument(
        "--force", action="store_true", help="replace files whose checksum does not match"
    )
    parser.add_argument("--attempts", type=int, default=3, help="attempts per URL (default: 3)")
    parser.add_argument(
        "--timeout", type=float, default=60.0, help="network timeout in seconds (default: 60)"
    )
    args = parser.parse_args(argv)

    ok = True
    for artifact in MANIFEST:
        try:
            ok &= ensure(
                artifact,
                args.root,
                force=args.force,
                check_only=args.check,
                attempts=max(1, args.attempts),
                timeout=args.timeout,
            )
        except FetchError as exc:
            print(f"error: {exc}", file=sys.stderr, flush=True)
            ok = False
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
