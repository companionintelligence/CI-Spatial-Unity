from typing import List

import numpy as np

from app.schemas.segment import RLEMask


def rle_encode(mask: np.ndarray) -> RLEMask:
    if mask.dtype != np.uint8:
        mask = mask.astype(np.uint8)
    flat = mask.flatten(order="F")
    counts: List[int] = []
    last = 0
    run_length = 0
    for value in flat:
        if value == last:
            run_length += 1
        else:
            counts.append(run_length)
            run_length = 1
            last = value
    counts.append(run_length)
    counts_str = " ".join(str(c) for c in counts)
    return RLEMask(size=[mask.shape[0], mask.shape[1]], counts=counts_str)
