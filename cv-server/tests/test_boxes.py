"""Unit tests for app.utils.boxes, the conversions behind the API's box format."""

import numpy as np
import pytest
import torch

from app.utils.boxes import cxcywh_to_xyxy, xyxy_to_pixels


def _assert_close(actual, expected) -> None:
    np.testing.assert_allclose(np.asarray(actual, dtype=np.float64), expected, rtol=0, atol=1e-6)


def test_cxcywh_to_xyxy_returns_corners() -> None:
    # Centre (0.25, 0.5), 0.2 wide and 0.4 high.
    _assert_close(cxcywh_to_xyxy([[0.25, 0.5, 0.2, 0.4]]), [[0.15, 0.3, 0.35, 0.7]])


def test_cxcywh_to_xyxy_clips_to_the_image() -> None:
    boxes = [[0.9, 0.9, 0.4, 0.4], [0.05, 0.1, 0.3, 0.4]]
    _assert_close(cxcywh_to_xyxy(boxes), [[0.7, 0.7, 1.0, 1.0], [0.0, 0.0, 0.2, 0.3]])


def test_cxcywh_to_xyxy_keeps_order_and_returns_python_floats() -> None:
    boxes = np.array([[0.5, 0.5, 0.2, 0.2], [0.2, 0.8, 0.1, 0.2]], dtype=np.float32)
    result = cxcywh_to_xyxy(boxes)
    _assert_close(result, [[0.4, 0.4, 0.6, 0.6], [0.15, 0.7, 0.25, 0.9]])
    assert all(type(value) is float for box in result for value in box)


def test_cxcywh_to_xyxy_accepts_the_tensor_groundingdino_returns() -> None:
    boxes = torch.tensor([[0.5, 0.25, 0.5, 0.5]])
    _assert_close(cxcywh_to_xyxy(boxes), [[0.25, 0.0, 0.75, 0.5]])


@pytest.mark.parametrize("empty", [None, [], np.zeros((0, 4)), torch.zeros((0, 4))])
def test_cxcywh_to_xyxy_without_detections(empty) -> None:
    assert cxcywh_to_xyxy(empty) == []


@pytest.mark.parametrize("bad", [[0.5, 0.5, 0.2, 0.2], [[0.5, 0.5, 0.2]]])
def test_cxcywh_to_xyxy_rejects_other_shapes(bad) -> None:
    with pytest.raises(ValueError):
        cxcywh_to_xyxy(bad)


def test_xyxy_to_pixels_scales_x_by_width_and_y_by_height() -> None:
    pixels = xyxy_to_pixels([[0.15, 0.3, 0.35, 0.7], [0.0, 0.0, 1.0, 1.0]], width=200, height=100)
    _assert_close(pixels, [[30.0, 30.0, 70.0, 70.0], [0.0, 0.0, 200.0, 100.0]])
