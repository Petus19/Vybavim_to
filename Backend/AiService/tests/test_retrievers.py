"""Spôsoby hľadania na malej sade – bez modelu (sémantika beží s falošným modelom z fakes.py)."""

from uuid import UUID

import pytest

from ai_service.evaluation.dataset import EvalListing
from ai_service.evaluation.retrievers import (
    Bm25Retriever,
    HybridRetriever,
    KeywordRetriever,
    SemanticRetriever,
    normalize,
    reciprocal_rank_fusion,
    tokenize,
)
from ai_service.listing_text import ListingDocument
from tests.fakes import FakeEmbedder


def listing(key: str, title: str, description: str = "", skills: tuple[str, ...] = ()) -> EvalListing:
    return EvalListing(key, ListingDocument(UUID(int=len(key)), title, "web", description, skills, "open"))


LISTINGS = [
    listing("cafe", "Logo pre kaviareň", "Hľadáme grafika na logo kaviarne.", ("Illustrator",)),
    listing("wifi", "Nastavenie Wi-Fi v predajni", "Vypadáva nám sieť."),
    listing("shop", "E-shop so sviečkami", "Chcem internetový obchod.", ("WooCommerce",)),
]


def keys(ranking: list[tuple[str, float]]) -> list[str]:
    return [key for key, _ in ranking]


def test_normalize_removes_diacritics_and_case() -> None:
    assert normalize("Kaviareň Ťahanovce ÄÔ") == "kaviaren tahanovce ao"


def test_tokenize_shortens_words_to_prefix() -> None:
    assert tokenize("kaviareň, kaviarne!", prefix_length=5) == ["kavia", "kavia"]


def test_keyword_matches_like_the_csharp_api() -> None:
    retriever = KeywordRetriever(LISTINGS)

    assert keys(retriever.rank("KAVIAREN")) == ["cafe"]  # bez diakritiky a veľkosti písmen
    assert keys(retriever.rank("woocommerce")) == ["shop"]  # hľadá aj v zručnostiach
    # Celý dopyt je JEDEN podreťazec – slová, ktoré nie sú v texte presne za sebou, nenájde
    assert retriever.rank("logo kaviaren") == []
    assert retriever.rank("wifi") == []  # v texte je „Wi-Fi“


def test_bm25_finds_words_in_any_order_and_form() -> None:
    retriever = Bm25Retriever(LISTINGS)

    assert keys(retriever.rank("kaviarne logo"))[0] == "cafe"
    assert retriever.rank("upratovanie") == []  # žiadne spoločné slovo → žiadny výsledok


def test_bm25_ranks_more_matching_words_higher() -> None:
    ranking = Bm25Retriever(LISTINGS).rank("internetový obchod sviečky")

    assert keys(ranking)[0] == "shop"
    assert ranking[0][1] > 0


def test_semantic_ranks_all_listings_by_similarity() -> None:
    retriever = SemanticRetriever(FakeEmbedder(), LISTINGS)

    ranking = retriever.rank("nefunguje wifi")

    assert keys(ranking)[0] == "wifi"
    assert len(ranking) == len(LISTINGS)  # sémantika vždy vráti všetko – aj nezmysly
    assert ranking[0][1] >= ranking[-1][1]


def test_semantic_adds_prefixes() -> None:
    embedder = FakeEmbedder()
    SemanticRetriever(embedder, LISTINGS[:1], query_prefix="query: ", passage_prefix="passage: ").rank("x")

    assert embedder.embedded_texts[0].startswith("passage: Logo pre kaviareň")
    assert embedder.embedded_texts[-1] == "query: x"


def test_reciprocal_rank_fusion_rewards_agreement() -> None:
    first = [("a", 9.0), ("b", 5.0), ("c", 1.0)]
    second = [("b", 0.9), ("c", 0.8), ("a", 0.1)]

    fused = reciprocal_rank_fusion([first, second])

    # b je 2. a 1. → najlepší súčet; a je 1. a 3.; c je 3. a 2.
    assert keys(fused) == ["b", "a", "c"]
    assert fused[0][1] == pytest.approx(1 / 62 + 1 / 61)


def test_hybrid_keeps_results_found_by_only_one_method() -> None:
    hybrid = HybridRetriever(KeywordRetriever(LISTINGS), SemanticRetriever(FakeEmbedder(), LISTINGS))

    assert set(keys(hybrid.rank("eshop"))) == {"cafe", "wifi", "shop"}
    assert hybrid.name.startswith("hybrid: keyword")
