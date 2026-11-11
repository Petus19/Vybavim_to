"""Nástroje agenta nad falošným API: môj inzerát podľa názvu, filtre hľadania, kompaktné výsledky."""

from typing import Any

import pytest

from ai_service.agent.llm import ToolCall
from ai_service.agent.tools import ToolRegistry, build_read_tools
from tests.agent_fakes import FakeApi, FakeSearch

LOGO_ID = "019a0b2c-4000-7000-8000-000000000001"
ADS_ID = "019a0b2c-4000-7000-8000-000000000002"

MINE = {
    "items": [
        {
            "id": LOGO_ID,
            "title": "Redizajn starého loga autoservisu",
            "budget": {"type": "fixed", "amount": 300},
        },
        {"id": ADS_ID, "title": "Google Ads pre autoservis", "budget": {"type": "fixed", "amount": 300}},
    ]
}
OFFER = {
    "id": "o1",
    "listingId": LOGO_ID,
    "listingTitle": "Redizajn starého loga autoservisu",
    "freelancerId": "f1",
    "freelancer": {"displayName": "Lucia Bartošová"},
    "price": 280,
    "deliveryDays": 7,
    "message": "Pripravím 3 návrhy.",
    "status": "pending",
}


def detail(listing_id: str, title: str, amount: float, status: str = "open") -> dict[str, Any]:
    return {
        "id": listing_id,
        "title": title,
        "description": "Popis.",
        "category": "grafika",
        "city": "Košice",
        "remote": True,
        "budget": {"type": "fixed", "amount": amount},
        "status": status,
    }


def run(api: FakeApi, name: str, search: FakeSearch | None = None, **arguments: Any) -> tuple[Any, bool]:
    registry = ToolRegistry(build_read_tools(api, search or FakeSearch([])))  # type: ignore[arg-type]
    outcome = registry.execute(ToolCall("c1", name, arguments))
    return outcome.data, outcome.is_error


@pytest.fixture
def api() -> FakeApi:
    return FakeApi(
        {
            "/api/v1/listings/mine": MINE,
            f"/api/v1/listings/{LOGO_ID}/offers": [OFFER],
            "/api/v1/profiles/f1": {"bio": "Grafička, 8 rokov praxe.", "skills": ["Logo dizajn"]},
        }
    )


@pytest.mark.parametrize("reference", ["logo", "redizajn loga", "LOGÁ", LOGO_ID])
def test_offers_found_by_title_word_or_id_in_one_step(api: FakeApi, reference: str) -> None:
    data, is_error = run(api, "get_offers_on_listing", listing=reference)

    assert not is_error
    assert data["listing_title"] == "Redizajn starého loga autoservisu"
    assert data["offers"][0]["specialist"] == "Lucia Bartošová"
    assert data["offers"][0]["specialist_skills"] == ["Logo dizajn"]  # profil na porovnanie


def test_ambiguous_title_asks_the_model_to_clarify(api: FakeApi) -> None:
    data, is_error = run(api, "get_offers_on_listing", listing="autoservis")  # sedí na oba inzeráty

    assert not is_error
    assert {c["id"] for c in data["ambiguous"]} == {LOGO_ID, ADS_ID}
    assert "Spýtaj sa" in data["note"]


def test_unknown_listing_returns_my_listings_as_a_hint(api: FakeApi) -> None:
    data, is_error = run(api, "get_offers_on_listing", listing="e-shop")

    assert is_error
    assert data["error"] == "listing_not_found"
    assert [item["title"] for item in data["my_listings"]] == [
        "Redizajn starého loga autoservisu",
        "Google Ads pre autoservis",
    ]


def test_search_applies_budget_and_status_filters() -> None:
    api = FakeApi(
        {
            "/api/v1/listings/a": detail("a", "Logo pre kaviareň", 300),
            "/api/v1/listings/b": detail("b", "Polep dodávky s logom", 400),
            "/api/v1/listings/c": detail("c", "Animované logo", 150, status="in_progress"),
        }
    )

    data, _ = run(api, "search_listings", FakeSearch(["a", "b", "c"]), query="logo", max_budget=300)

    assert [r["title"] for r in data["results"]] == ["Logo pre kaviareň"]
    assert data["results"][0]["budget"] == "300 € (pevná cena)"


def test_search_with_no_results_tells_the_model_not_to_invent(api: FakeApi) -> None:
    data, is_error = run(api, "search_listings", FakeSearch([]), query="účtovníctvo")

    assert not is_error
    assert data["results"] == []
    assert "Nevymýšľaj" in data["note"]


def test_offers_without_listing_returns_all_received_offers(api: FakeApi) -> None:
    api.routes[f"/api/v1/listings/{ADS_ID}/offers"] = []
    for item in MINE["items"]:
        item["status"] = "open"

    data, is_error = run(api, "get_offers_on_listing")

    assert not is_error
    assert [group["listing_title"] for group in data["listings"]] == ["Redizajn starého loga autoservisu"]
    assert data["listings"][0]["offers"][0]["specialist"] == "Lucia Bartošová"
