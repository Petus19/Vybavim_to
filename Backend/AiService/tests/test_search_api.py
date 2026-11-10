"""Indexovanie a hľadanie cez HTTP – s falošným modelom, úložiskom v pamäti a inzerátmi v pamäti."""

from collections.abc import Iterator
from dataclasses import dataclass
from uuid import UUID

import pytest
from fastapi.testclient import TestClient

from ai_service.main import Services, create_app
from tests.fakes import FakeEmbedder, InMemoryListings, InMemoryRepository, document

LOGO = document(1, "Logo pre kaviareň", "Hľadáme grafika na logo a vizitky.")
WIFI = document(2, "Wifi v kancelárii", "Nefunguje sieť a tlačiareň.")
ESHOP = document(3, "E-shop s kozmetikou", "Chceme internetový obchod.")


@dataclass
class Fixture:
    client: TestClient
    embedder: FakeEmbedder
    listings: InMemoryListings


@pytest.fixture
def api() -> Iterator[Fixture]:
    embedder = FakeEmbedder()
    listings = InMemoryListings([LOGO, WIFI, ESHOP])
    services = Services(embedder=embedder, repository=InMemoryRepository(), listings=listings)

    # "with" spustí lifespan aplikácie (štart a koniec), ako pri skutočnom serveri
    with TestClient(create_app(lambda _settings: services)) as client:
        yield Fixture(client, embedder, listings)


def test_index_embeds_all_listings_first_time(api: Fixture) -> None:
    response = api.client.post("/index")

    assert response.status_code == 200
    assert response.json() == {"indexed": 3, "unchanged": 0, "removed": 0}


def test_index_again_skips_unchanged_and_removes_withdrawn(api: Fixture) -> None:
    api.client.post("/index")
    api.embedder.embedded_texts.clear()

    # Inzerát WIFI zmizol z verejného API (napr. ho stiahli), ostatné sa nezmenili
    api.listings.documents = [LOGO, ESHOP]
    response = api.client.post("/index")

    assert response.json() == {"indexed": 0, "unchanged": 2, "removed": 1}
    assert api.embedder.embedded_texts == []  # model sa vôbec nevolal


def test_index_reembeds_changed_listing(api: Fixture) -> None:
    api.client.post("/index")

    api.listings.documents = [document(1, "Logo pre kaviareň", "Nový popis: aj etikety."), WIFI, ESHOP]
    response = api.client.post("/index")

    assert response.json() == {"indexed": 1, "unchanged": 2, "removed": 0}


def test_search_returns_most_similar_first(api: Fixture) -> None:
    api.client.post("/index")

    response = api.client.get("/search", params={"q": "potrebujem znak pre firmu od grafika", "min_score": 0})
    hits = response.json()

    assert response.status_code == 200
    assert hits[0]["listing_id"] == str(LOGO.id)
    assert hits[0]["score"] > hits[1]["score"]
    assert set(hits[0]) == {"listing_id", "title", "status", "score", "keyword_match"}


def test_search_hides_results_below_min_score(api: Fixture) -> None:
    api.client.post("/index")

    response = api.client.get("/search", params={"q": "potrebujem znak pre firmu od grafika"})

    assert [hit["listing_id"] for hit in response.json()] == [str(LOGO.id)]


def test_search_returns_empty_list_when_nothing_is_relevant(api: Fixture) -> None:
    api.client.post("/index")

    response = api.client.get("/search", params={"q": "upratovanie kancelárie"})

    assert response.status_code == 200
    assert response.json() == []


def test_search_finds_exact_words_even_with_low_similarity(api: Fixture) -> None:
    api.client.post("/index")

    # Falošný model nepozná slovo „kozmetika“ (nízke skóre), ale BM25 nájde všetky slová dopytu
    hits = api.client.get("/search", params={"q": "kozmetikou"}).json()

    assert [hit["listing_id"] for hit in hits] == [str(ESHOP.id)]
    assert hits[0]["keyword_match"] is True
    assert hits[0]["score"] < 0.41


def test_search_before_indexing_returns_nothing(api: Fixture) -> None:
    assert api.client.get("/search", params={"q": "logo"}).json() == []


def test_search_respects_limit(api: Fixture) -> None:
    api.client.post("/index")

    response = api.client.get("/search", params={"q": "wifi", "limit": 1, "min_score": 0})

    assert len(response.json()) == 1


@pytest.mark.parametrize(
    "params",
    [{"q": "a"}, {"q": "logo", "limit": 0}, {"q": "logo", "limit": 51}, {"q": "logo", "min_score": 2}, {}],
)
def test_search_invalid_query_returns_422(api: Fixture, params: dict[str, object]) -> None:
    response = api.client.get("/search", params=params)

    assert response.status_code == 422


def test_similar_excludes_the_listing_itself(api: Fixture) -> None:
    api.client.post("/index")

    response = api.client.get(f"/listings/{LOGO.id}/similar")
    ids = [hit["listing_id"] for hit in response.json()]

    assert response.status_code == 200
    assert str(LOGO.id) not in ids
    assert len(ids) == 2


def test_similar_unknown_listing_returns_404(api: Fixture) -> None:
    response = api.client.get(f"/listings/{UUID(int=999)}/similar")

    assert response.status_code == 404
