"""BertTextEncoder stands in for GroundingDINO's BERT wrapper on transformers 5.

See app/gdino_text_encoder.py. A small random BERT is enough: no weights or network.
"""

import pytest
import torch
from transformers import BertConfig, BertModel

from app.gdino_text_encoder import (
    BertTextEncoder,
    additive_attention_mask,
    bert_text_encoder_for_groundingdino,
)


def _tiny_bert() -> BertModel:
    torch.manual_seed(0)
    config = BertConfig(
        vocab_size=64,
        hidden_size=32,
        num_hidden_layers=2,
        num_attention_heads=4,
        intermediate_size=64,
        max_position_embeddings=32,
    )
    return BertModel(config).eval()


def test_additive_mask_is_zero_where_attending() -> None:
    keep = torch.tensor([[[1, 1, 0], [1, 1, 0], [0, 0, 1]]], dtype=torch.bool)
    mask = additive_attention_mask(keep, torch.float32)
    assert mask.shape == (1, 1, 3, 3)
    assert torch.equal(mask[0, 0] == 0, keep[0])
    assert mask.min() == torch.finfo(torch.float32).min
    assert additive_attention_mask(torch.tensor([[1, 0]]), torch.float32).shape == (1, 1, 1, 2)
    with pytest.raises(ValueError):
        additive_attention_mask(torch.ones(1, 1, 1, 2), torch.float32)


def test_matches_bert_model_with_a_padding_mask() -> None:
    bert = _tiny_bert()
    input_ids = torch.tensor([[2, 5, 9, 3, 0, 0], [2, 7, 7, 8, 11, 3]])
    attention_mask = torch.tensor([[1, 1, 1, 1, 0, 0], [1, 1, 1, 1, 1, 1]])
    with torch.no_grad():
        ours = BertTextEncoder(bert)(input_ids=input_ids, attention_mask=attention_mask)
        reference = bert(input_ids=input_ids, attention_mask=attention_mask)
    assert torch.allclose(ours.last_hidden_state, reference.last_hidden_state, atol=1e-5)
    assert torch.allclose(ours.pooler_output, reference.pooler_output, atol=1e-5)


def test_phrase_mask_keeps_phrases_apart() -> None:
    # GroundingDINO's 3-D mask: tokens 0-2 and 3-5 are two phrases that see only themselves.
    encoder = BertTextEncoder(_tiny_bert())
    phrases = torch.zeros(1, 6, 6, dtype=torch.bool)
    phrases[0, :3, :3] = True
    phrases[0, 3:, 3:] = True
    position_ids = torch.tensor([[0, 1, 2, 0, 1, 2]])
    first = torch.tensor([[2, 5, 9, 4, 6, 3]])
    second = torch.tensor([[2, 5, 9, 12, 13, 14]])  # only the second phrase differs
    with torch.no_grad():
        a = encoder(input_ids=first, attention_mask=phrases, position_ids=position_ids).last_hidden_state
        b = encoder(input_ids=second, attention_mask=phrases, position_ids=position_ids).last_hidden_state
    assert torch.allclose(a[0, :3], b[0, :3], atol=1e-6)
    assert not torch.allclose(a[0, 3:], b[0, 3:])


def test_weight_names_match_bert_model() -> None:
    # The GroundingDINO checkpoint stores the text encoder under BertModel's names.
    bert = _tiny_bert()
    assert set(BertTextEncoder(bert).state_dict()) == set(bert.state_dict())


def test_groundingdino_gets_the_text_encoder_while_building() -> None:
    gdino = pytest.importorskip("groundingdino.models.GroundingDINO.groundingdino")
    original = gdino.BertModelWarper
    with bert_text_encoder_for_groundingdino():
        assert gdino.BertModelWarper is BertTextEncoder
    assert gdino.BertModelWarper is original
