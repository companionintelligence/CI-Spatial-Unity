"""Bounding-box conversions, and the box format the API returns.

Every box the API returns (``bboxes`` from ``/v1/detect``, ``/v1/detect/batch`` and
``/v1/florence``, ``bbox`` in each ``/v1/segment`` annotation, and the same results on
``WS /v1/stream``) is ``[x1, y1, x2, y2]``:

- ``(x1, y1)`` is the top-left corner and ``(x2, y2)`` the bottom-right one, so
  ``x1 <= x2`` and ``y1 <= y2``;
- the origin is the top-left corner of the image, x points right and y points down;
- x is a fraction of the image width and y a fraction of the image height, in [0, 1].

Fractions do not depend on the image size, so a box is the same whether or not
``max_side`` made the server shrink the image first. Multiply x by the width and y by
the height of the image you sent to get pixels. This is what the Quest client reads:
``BoundingBoxUtils.TryNormalize`` takes ``[x1, y1, x2, y2]`` from the top-left and uses
values that are all at most 1 as they are.

GroundingDINO's ``predict`` returns ``(cx, cy, w, h)`` instead: the box centre and size,
also as fractions of the image. :func:`cxcywh_to_xyxy` converts them.
"""

from typing import Any, List

import numpy as np


def _as_box_array(boxes: Any) -> np.ndarray:
    """Return ``boxes`` (a tensor, an array or nested lists) as an ``(n, 4)`` float array."""
    if boxes is None:
        return np.zeros((0, 4), dtype=np.float64)
    if hasattr(boxes, "detach"):  # torch.Tensor; avoids importing torch here
        boxes = boxes.detach().cpu().numpy()
    array = np.asarray(boxes, dtype=np.float64)
    if array.size == 0:
        return np.zeros((0, 4), dtype=np.float64)
    if array.ndim != 2 or array.shape[1] != 4:
        raise ValueError(f"Expected boxes of shape (n, 4), got {array.shape}.")
    return array


def cxcywh_to_xyxy(boxes: Any) -> List[List[float]]:
    """Convert GroundingDINO's ``(cx, cy, w, h)`` boxes to the API's ``[x1, y1, x2, y2]``.

    Both are fractions of the image size. Corners are clipped to the image, and the
    boxes keep their order, so they still line up with the labels and scores.
    """
    array = _as_box_array(boxes)
    cx, cy, w, h = array.T
    xyxy = np.stack([cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2], axis=1)
    xyxy = np.clip(xyxy, 0.0, 1.0)
    return [[float(value) for value in box] for box in xyxy]


def xyxy_to_pixels(boxes: Any, width: int, height: int) -> np.ndarray:
    """Scale API boxes (``[x1, y1, x2, y2]`` as fractions) to pixels of a ``width`` x ``height`` image.

    SAM 2 takes box prompts this way: corners in pixels of the image given to ``set_image``.
    """
    array = _as_box_array(boxes)
    return array * np.array([width, height, width, height], dtype=np.float64)
