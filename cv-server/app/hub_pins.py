"""Hugging Face downloads the server makes at run time, pinned to repository commits.

scripts/fetch_weights.py fetches the GroundingDINO and SAM 2 files from pinned URLs
with pinned SHA-256 checksums. Two models still come from the Hugging Face hub when
they are first loaded:

* BERT base uncased, GroundingDINO's text encoder (its tokenizer and initial weights);
* Florence-2, in the format of transformers' own Florence-2 implementation, so the
  repository supplies weights, configs and tokenizer files but no Python code.

A branch such as ``main`` can move to different weights at any time, so both are
downloaded at a fixed commit. A commit id names one immutable snapshot of a
repository: what was checked is what every later start downloads.
"""

from __future__ import annotations

import os
import re
from dataclasses import dataclass
from typing import Dict, Optional, Tuple

_COMMIT_ID = re.compile(r"[0-9a-f]{40}")


@dataclass(frozen=True)
class HubPin:
    repo_id: str
    revision: str  # full 40-character commit id


#: GroundingDINO's text encoder. The Swin-T OGC config names it ``bert-base-uncased``,
#: the old alias of this repository, which GroundingDINO would load from ``main``.
BERT_BASE_UNCASED = HubPin(
    "google-bert/bert-base-uncased", "86b5e0934494bd15c9632b12f734a8a67f723594"
)

#: The BERT files that BertModel and its tokenizer read. The repository also holds
#: TensorFlow, Flax, ONNX, Core ML and pickle copies of the weights, which are not
#: downloaded.
BERT_FILES = (
    "config.json",
    "model.safetensors",
    "tokenizer.json",
    "tokenizer_config.json",
    "vocab.txt",
)

#: Florence-2 repositories and the commit each is loaded at when FLORENCE_MODEL_REVISION
#: is not set. Any other model id needs FLORENCE_MODEL_REVISION. These hold Microsoft's
#: Florence-2 weights converted to the format of transformers' own implementation
#: (MIT, like the originals).
FLORENCE2_REVISIONS: Dict[str, str] = {
    "florence-community/Florence-2-large": "4271c66b88cdbc05735372ec13b2360108de5317",
    "florence-community/Florence-2-base": "00921df66db728a9ceb750f5eca43e5c203a2051",
}

#: Microsoft's repositories hold the same weights for their own repository code, which
#: needs trust_remote_code and does not run on transformers 5. These ids, the server's
#: earlier default, load the converted repository instead.
FLORENCE2_CONVERTED: Dict[str, str] = {
    "microsoft/Florence-2-large": "florence-community/Florence-2-large",
    "microsoft/Florence-2-base": "florence-community/Florence-2-base",
}


def is_commit_id(revision: str) -> bool:
    return bool(_COMMIT_ID.fullmatch(revision))


def florence_model(model_id: str, override: str = "") -> Tuple[str, Optional[str]]:
    """Return the repository (or local directory) and commit to load Florence-2 from.

    ``model_id`` is FLORENCE_MODEL_ID and ``override`` FLORENCE_MODEL_REVISION. A Microsoft
    id is replaced by its converted repository (FLORENCE2_CONVERTED); a commit given for a
    Microsoft id is refused, because it names a snapshot of the other repository.
    """
    model_id = model_id.strip()
    override = (override or "").strip()
    converted = {repo.lower(): conv for repo, conv in FLORENCE2_CONVERTED.items()}
    replacement = converted.get(model_id.lower())
    if replacement:
        if override:
            raise RuntimeError(
                f"{model_id} holds Florence-2 for its own repository code, which this server "
                f"does not run. Set FLORENCE_MODEL_ID to {replacement} and FLORENCE_MODEL_REVISION "
                "to a commit of that repository."
            )
        model_id = replacement
    return model_id, florence_revision(model_id, override)


def florence_revision(model_id: str, override: str = "") -> Optional[str]:
    """Return the commit to load Florence-2 ``model_id`` at.

    ``override`` is FLORENCE_MODEL_REVISION. Without one, the commit pinned above for
    ``model_id`` applies. A model id that is a local directory needs no revision: nothing
    is downloaded. Anything else without a full commit id is refused, because the server
    would otherwise load whatever the repository's branch holds on the day it starts.
    """
    override = (override or "").strip()
    if not override and os.path.isdir(model_id):
        return None
    pinned = {repo.lower(): rev for repo, rev in FLORENCE2_REVISIONS.items()}
    revision = override or pinned.get(model_id.strip().lower(), "")
    if not revision:
        raise RuntimeError(
            f"No pinned revision for Florence-2 model {model_id!r}. Set FLORENCE_MODEL_REVISION "
            "to the full commit id to load it at (listed under 'Files and versions' > "
            "'History' on the model's Hugging Face page)."
        )
    if not is_commit_id(revision):
        raise RuntimeError(
            f"FLORENCE_MODEL_REVISION must be a full 40-character commit id, got {revision!r}. "
            "Branch and tag names can move to different weights."
        )
    return revision
