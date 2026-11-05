"""Testovacia sada a celé vyhodnotenie. Kontrola dát chráni pred preklepom v ID, ktorý by skreslil čísla."""

from collections import Counter

from ai_service.evaluation.dataset import QUERY_KINDS, load_dataset
from ai_service.evaluation.retrievers import KeywordRetriever, SemanticRetriever
from ai_service.evaluation.run import K, evaluate, results_table, threshold_analysis
from tests.fakes import FakeEmbedder

CATEGORIES = {"web", "grafika", "it-podpora", "marketing"}  # slugy z C# (CategoryConfiguration)


def test_dataset_is_consistent() -> None:
    dataset = load_dataset()
    keys = [listing.key for listing in dataset.listings]

    assert len(keys) == len(set(keys)), "duplicitné ID inzerátu"
    assert len({q.id for q in dataset.queries}) == len(dataset.queries), "duplicitné ID dopytu"
    assert {listing.document.category for listing in dataset.listings} == CATEGORIES
    assert len({listing.document.id for listing in dataset.listings}) == len(keys)
    for query in dataset.queries:
        assert query.kind in QUERY_KINDS, query.id
        assert query.relevant <= set(keys), f"{query.id} odkazuje na neexistujúci inzerát"
        assert (query.kind == "no-answer") == (not query.relevant), query.id
        assert len(query.relevant) <= K, f"{query.id}: viac správnych ako K – recall@K by nemohol byť 1"


def test_dataset_is_balanced_across_categories() -> None:
    counts = Counter(listing.document.category for listing in load_dataset().listings)

    assert len(set(counts.values())) == 1, counts  # rovnako inzerátov v každej kategórii


def test_evaluate_reports_metrics_per_kind_and_misses() -> None:
    dataset = load_dataset()

    result = evaluate(KeywordRetriever(dataset.listings), dataset.answerable)

    assert 0 <= result.recall_at_k <= 1
    assert 0 <= result.mrr <= 1
    assert result.recall_by_kind["synonym"] == 0  # kľúčové slovo nenájde synonymum – preto to celé robíme
    assert "q08" in result.misses
    assert "| keyword (current C# API) |" in results_table([result])


def test_threshold_analysis_covers_both_extremes() -> None:
    dataset = load_dataset()

    rows = threshold_analysis(SemanticRetriever(FakeEmbedder(), dataset.listings), dataset)

    assert rows[0].answered == 1.0  # najnižšia hranica prepustí všetko…
    assert rows[0].rejected == 0.0  # …a teda nič neodmietne
    assert [row.threshold for row in rows] == sorted(row.threshold for row in rows)
