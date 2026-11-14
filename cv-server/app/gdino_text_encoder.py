"""GroundingDINO's BERT text encoder on transformers 5.

GroundingDINO (pinned in requirements.txt) wraps transformers' BertModel in its
BertModelWarper so that it can pass its own text attention mask, in which each phrase
of the prompt ("chair . table .") attends only to its own tokens. BertModelWarper
copies BertModel.get_extended_attention_mask, invert_attention_mask and get_head_mask,
which transformers 5 removed, so GroundingDINO's model fails to build.

BertTextEncoder takes its place. Its forward does what BertModelWarper.forward does on
GroundingDINO's path (an encoder: no cache, no cross-attention, no head mask), with
only BertModel's embeddings, encoder and pooler. It keeps those attribute names, so
the checkpoint's ``bert.*`` weights load into it unchanged.
"""

from __future__ import annotations

import contextlib
from typing import Iterator

import torch
from torch import nn
from transformers.modeling_outputs import BaseModelOutputWithPoolingAndCrossAttentions


class BertTextEncoder(nn.Module):
    def __init__(self, bert_model: nn.Module) -> None:
        super().__init__()
        self.config = bert_model.config
        self.embeddings = bert_model.embeddings
        self.encoder = bert_model.encoder
        self.pooler = bert_model.pooler

    def forward(
        self,
        input_ids: torch.Tensor,
        attention_mask: torch.Tensor | None = None,
        token_type_ids: torch.Tensor | None = None,
        position_ids: torch.Tensor | None = None,
        **_unused,
    ) -> BaseModelOutputWithPoolingAndCrossAttentions:
        if attention_mask is None:
            attention_mask = torch.ones_like(input_ids)
        if token_type_ids is None:
            token_type_ids = torch.zeros_like(input_ids)
        embedding_output = self.embeddings(
            input_ids=input_ids, position_ids=position_ids, token_type_ids=token_type_ids
        )
        mask = additive_attention_mask(attention_mask, self.embeddings.word_embeddings.weight.dtype)
        sequence_output = self.encoder(embedding_output, attention_mask=mask)[0]
        pooled_output = self.pooler(sequence_output) if self.pooler is not None else None
        return BaseModelOutputWithPoolingAndCrossAttentions(
            last_hidden_state=sequence_output, pooler_output=pooled_output
        )


def additive_attention_mask(attention_mask: torch.Tensor, dtype: torch.dtype) -> torch.Tensor:
    """Turn a keep-mask (1 = attend) into the additive mask BERT's attention adds to its scores.

    [batch, from, to] (GroundingDINO's phrase mask) or [batch, to] becomes
    [batch, 1, from or 1, to]: 0 where attending, the dtype's minimum elsewhere. This is
    what transformers 4's get_extended_attention_mask returned for an encoder.
    """
    if attention_mask.dim() == 3:
        mask = attention_mask[:, None, :, :]
    elif attention_mask.dim() == 2:
        mask = attention_mask[:, None, None, :]
    else:
        raise ValueError(f"attention_mask must be 2-D or 3-D, got shape {tuple(attention_mask.shape)}")
    mask = mask.to(dtype=dtype)
    return (1.0 - mask) * torch.finfo(dtype).min


@contextlib.contextmanager
def bert_text_encoder_for_groundingdino() -> Iterator[None]:
    """While GroundingDINO builds its model, give it BertTextEncoder for BertModelWarper."""
    from groundingdino.models.GroundingDINO import groundingdino as gdino_module

    original = gdino_module.BertModelWarper
    gdino_module.BertModelWarper = BertTextEncoder
    try:
        yield
    finally:
        gdino_module.BertModelWarper = original
