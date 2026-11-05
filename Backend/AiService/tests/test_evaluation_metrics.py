"""Metriky na ručne spočítaných príkladoch – keď je metrika zle, sú zlé aj všetky výsledky."""

import pytest

from ai_service.evaluation.metrics import mean, recall_at_k, reciprocal_rank


def test_recall_counts_relevant_items_in_top_k() -> None:
    ranked = ["A", "X", "Y", "Z", "W", "B"]

    assert recall_at_k(ranked, {"A", "B"}, k=5) == 0.5  # B je až šiesty
    assert recall_at_k(ranked, {"A", "B"}, k=6) == 1.0
    assert recall_at_k([], {"A"}, k=5) == 0.0


def test_recall_needs_a_relevant_item() -> None:
    with pytest.raises(ValueError, match="Recall"):
        recall_at_k(["A"], set(), k=5)


@pytest.mark.parametrize(
    ("ranked", "expected"),
    [
        (["A", "X", "Y"], 1.0),
        (["X", "A", "Y"], 0.5),
        (["X", "Y", "A"], 1 / 3),
        (["X", "Y", "Z"], 0.0),
    ],
)
def test_reciprocal_rank_uses_first_relevant_position(ranked: list[str], expected: float) -> None:
    assert reciprocal_rank(ranked, {"A", "B"}) == pytest.approx(expected)


def test_reciprocal_rank_ignores_hits_beyond_k() -> None:
    assert reciprocal_rank(["X", "Y", "A"], {"A"}, k=2) == 0.0


def test_mean_of_empty_is_zero() -> None:
    assert mean([]) == 0.0
    assert mean([1.0, 0.5]) == 0.75
