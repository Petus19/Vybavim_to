"""Spôsoby hľadania vo vyhodnotení – bez modelu (sémantika beží s falošným modelom z fakes.py)."""

from uuid import UUID

from ai_service.evaluation.dataset import EvalListing
from ai_service.evaluation.retrievers import (
    Bm25Retriever,
    HybridRetriever,
    KeywordRetriever,
    SemanticRetriever,
    ServedHybridRetriever,
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


def test_keyword_matches_like_the_csharp_api() -> None:
    retriever = KeywordRetriever(LISTINGS)

    assert keys(retriever.rank("KAVIAREN")) == ["cafe"]  # bez diakritiky a veľkosti písmen
    assert keys(retriever.rank("woocommerce")) == ["shop"]  # hľadá aj v zručnostiach
    # Celý dopyt je JEDEN podreťazec – slová, ktoré nie sú v texte presne za sebou, nenájde
    assert retriever.rank("logo kaviaren") == []
    assert retriever.rank("wifi") == []  # v texte je „Wi-Fi“


def test_bm25_ranks_more_matching_words_higher() -> None:
    ranking = Bm25Retriever(LISTINGS).rank("internetový obchod sviečky")

    assert keys(ranking)[0] == "shop"


def test_semantic_ranks_all_listings_by_similarity() -> None:
    ranking = SemanticRetriever(FakeEmbedder(), LISTINGS).rank("nefunguje wifi")

    assert keys(ranking)[0] == "wifi"
    assert len(ranking) == len(LISTINGS)  # sémantika vždy vráti všetko – aj nezmysly


def test_semantic_adds_prefixes() -> None:
    embedder = FakeEmbedder()
    SemanticRetriever(embedder, LISTINGS[:1], query_prefix="query: ", passage_prefix="passage: ").rank("x")

    assert embedder.embedded_texts[0].startswith("passage: Logo pre kaviareň")
    assert embedder.embedded_texts[-1] == "query: x"


def test_hybrid_keeps_results_found_by_only_one_method() -> None:
    hybrid = HybridRetriever(KeywordRetriever(LISTINGS), SemanticRetriever(FakeEmbedder(), LISTINGS))

    assert set(keys(hybrid.rank("eshop"))) == {"cafe", "wifi", "shop"}
    assert hybrid.name.startswith("hybrid: keyword")


def test_served_hybrid_returns_nothing_for_irrelevant_query() -> None:
    served = ServedHybridRetriever(Bm25Retriever(LISTINGS), SemanticRetriever(FakeEmbedder(), LISTINGS), 0.41)

    assert served.rank("upratovanie kancelárie") == []
    assert keys(served.rank("logo kaviareň"))[0] == "cafe"
