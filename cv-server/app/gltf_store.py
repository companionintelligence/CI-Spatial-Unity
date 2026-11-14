"""Where glTF uploads are kept, and for how long.

``POST /v1/gltf`` stores each upload in ``GLTF_UPLOAD_DIR`` under a new upload ID:
32 lowercase hexadecimal characters, 128 random bits from the ``secrets`` module.
The ID holds no file name, counter or timestamp, so it reveals nothing about the
upload, the client or the server. An upload's files sit side by side:

- ``<id>.gltf``: the glTF JSON, as sent
- ``<id>.bin``: the binary buffer, when one was sent
- ``<id>.extras.json``: the ``extras`` field, when one was sent

Uploads stored before these IDs keep their files, named
``spatial-map-<YYYYmmdd>-<HHMMSS>-<8 hex>`` plus the same suffixes. That base name
is their ID, so ``DELETE /v1/gltf/{id}`` and retention cover them too.

Retention is ``GLTF_UPLOAD_RETENTION_DAYS`` (``settings.gltf_upload_retention_days``).
0, the default, keeps every upload until it is deleted. A positive value deletes each
upload whose newest file is older than that many days, at startup and then every
hour. Only files named like an upload are ever deleted.
"""

from __future__ import annotations

import asyncio
import contextlib
import logging
import os
import re
import secrets
import time
from dataclasses import dataclass
from typing import Dict, List, Optional

from app.config import settings

logger = logging.getLogger(__name__)

#: Random bytes behind an upload ID; the ID is their lowercase hex, 32 characters.
UPLOAD_ID_BYTES = 16

GLTF_SUFFIX = ".gltf"
BIN_SUFFIX = ".bin"
EXTRAS_SUFFIX = ".extras.json"
#: Every file of an upload is named ``<id><suffix>``.
UPLOAD_SUFFIXES = (GLTF_SUFFIX, BIN_SUFFIX, EXTRAS_SUFFIX)

#: Seconds between retention sweeps while the server runs.
RETENTION_SWEEP_INTERVAL_SECONDS = 60 * 60

_SECONDS_PER_DAY = 24 * 60 * 60
_UPLOAD_ID = re.compile("[0-9a-f]{%d}" % (2 * UPLOAD_ID_BYTES))
# How uploads were named before opaque IDs: spatial-map-<YYYYmmdd>-<HHMMSS>-<8 hex>.
_LEGACY_UPLOAD_ID = re.compile("spatial-map-[0-9]{8}-[0-9]{6}-[0-9a-f]{8}")
# Two IDs of 128 random bits do not collide in practice; this only bounds the loop.
_NEW_ID_ATTEMPTS = 4

_retention_task: Optional[asyncio.Task] = None


@dataclass(frozen=True)
class StoredUpload:
    upload_id: str
    gltf_path: str
    bin_path: Optional[str]
    extras_path: Optional[str]


def new_upload_id() -> str:
    """Return a new upload ID: 32 random lowercase hexadecimal characters."""
    return secrets.token_hex(UPLOAD_ID_BYTES)


def is_upload_id(value: str) -> bool:
    """True for an upload ID, new or from before opaque IDs, and for nothing else."""
    return bool(_UPLOAD_ID.fullmatch(value) or _LEGACY_UPLOAD_ID.fullmatch(value))


def _path(upload_dir: str, upload_id: str, suffix: str) -> str:
    return os.path.join(upload_dir, upload_id + suffix)


def save_upload(
    upload_dir: str,
    gltf: bytes,
    bin_data: Optional[bytes] = None,
    extras: Optional[str] = None,
) -> StoredUpload:
    """Store an upload under a new ID. ``bin_data`` and ``extras`` are optional."""
    os.makedirs(upload_dir, exist_ok=True)
    for _ in range(_NEW_ID_ATTEMPTS):
        upload_id = new_upload_id()
        try:
            # "x" claims the ID: it fails rather than overwrite an existing file.
            gltf_file = open(_path(upload_dir, upload_id, GLTF_SUFFIX), "xb")
        except FileExistsError:
            continue
        break
    else:
        raise RuntimeError("Could not find an unused glTF upload ID.")

    bin_path = None
    extras_path = None
    try:
        with gltf_file:
            gltf_file.write(gltf)
        if bin_data is not None:
            bin_path = _path(upload_dir, upload_id, BIN_SUFFIX)
            with open(bin_path, "xb") as handle:
                handle.write(bin_data)
        if extras:
            extras_path = _path(upload_dir, upload_id, EXTRAS_SUFFIX)
            with open(extras_path, "xb") as handle:
                handle.write(extras.encode("utf-8"))
    except BaseException:
        # Leave no half-written upload behind.
        delete_upload(upload_dir, upload_id)
        raise
    return StoredUpload(upload_id, _path(upload_dir, upload_id, GLTF_SUFFIX), bin_path, extras_path)


def delete_upload(upload_dir: str, upload_id: str) -> bool:
    """Delete every file of an upload. False when ``upload_id`` names no upload."""
    if not is_upload_id(upload_id):
        return False
    deleted = False
    for suffix in UPLOAD_SUFFIXES:
        try:
            os.remove(_path(upload_dir, upload_id, suffix))
        except FileNotFoundError:
            continue
        deleted = True
    return deleted


def _upload_id_of(file_name: str) -> Optional[str]:
    for suffix in UPLOAD_SUFFIXES:
        if file_name.endswith(suffix):
            upload_id = file_name[: -len(suffix)]
            return upload_id if is_upload_id(upload_id) else None
    return None


def delete_expired_uploads(
    upload_dir: str, retention_days: int, now: Optional[float] = None
) -> List[str]:
    """Delete the uploads whose newest file is older than ``retention_days``.

    Returns the deleted IDs. A ``retention_days`` of 0 keeps everything. Files that
    are not named like an upload, and directories, are left alone.
    """
    if retention_days <= 0:
        return []
    cutoff = (time.time() if now is None else now) - retention_days * _SECONDS_PER_DAY
    newest: Dict[str, float] = {}
    try:
        with os.scandir(upload_dir) as entries:
            for entry in entries:
                upload_id = _upload_id_of(entry.name)
                if upload_id is None:
                    continue
                try:
                    if entry.is_dir(follow_symlinks=False):
                        continue
                    modified = entry.stat(follow_symlinks=False).st_mtime
                except FileNotFoundError:  # deleted meanwhile
                    continue
                newest[upload_id] = max(modified, newest.get(upload_id, modified))
    except FileNotFoundError:  # nothing uploaded yet
        return []

    deleted = []
    for upload_id in sorted(newest):
        if newest[upload_id] >= cutoff:
            continue
        try:
            delete_upload(upload_dir, upload_id)
        except OSError as exc:
            logger.warning("Could not delete expired glTF upload %s: %s", upload_id, exc)
            continue
        deleted.append(upload_id)
    return deleted


def delete_expired_uploads_now() -> List[str]:
    """Apply GLTF_UPLOAD_RETENTION_DAYS to GLTF_UPLOAD_DIR once."""
    days = settings.gltf_upload_retention_days
    deleted = delete_expired_uploads(settings.gltf_upload_dir, days)
    if deleted:
        logger.info("Deleted %d glTF upload(s) older than %d day(s).", len(deleted), days)
    return deleted


async def retention_loop(interval_seconds: float = RETENTION_SWEEP_INTERVAL_SECONDS) -> None:
    """Apply the retention period every ``interval_seconds`` until cancelled."""
    while True:
        await asyncio.sleep(interval_seconds)
        try:
            await asyncio.to_thread(delete_expired_uploads_now)
        except Exception:  # try again next time rather than stop expiring uploads
            logger.exception("glTF upload retention sweep failed.")


async def start_retention() -> None:
    """With a retention period set, delete expired uploads now and then every hour."""
    global _retention_task
    if settings.gltf_upload_retention_days <= 0:
        return
    if _retention_task is not None and not _retention_task.done():
        return
    await asyncio.to_thread(delete_expired_uploads_now)
    _retention_task = asyncio.create_task(retention_loop())


async def stop_retention() -> None:
    """Stop the hourly retention sweep, if it runs."""
    global _retention_task
    task, _retention_task = _retention_task, None
    if task is None:
        return
    task.cancel()
    with contextlib.suppress(asyncio.CancelledError):
        await task
