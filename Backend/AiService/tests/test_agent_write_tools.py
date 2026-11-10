"""Zapisovacie nástroje: bez potvrdenia človekom sa NIČ nezapíše; náhľad ukazuje, čo sa stane."""

from typing import Any

import pytest

from ai_service.agent.llm import ToolCall
from ai_service.agent.tools import ToolOutcome, ToolRegistry, build_write_tools
from tests.agent_fakes import FakeApi

LOGO_ID = "019a0b2c-4000-7000-8000-000000000001"
LUCIA_OFFER = "00000000-0000-0000-0000-0000000000a1"
MAREK_OFFER = "00000000-0000-0000-0000-0000000000a2"
NEW_LISTING = "00000000-0000-0000-0000-0000000000b1"


def offer(offer_id: str, name: str, price: float, status: str = "pending") -> dict[str, Any]:
    return {
        "id": offer_id,
        "listingId": LOGO_ID,
        "listingTitle": "Redizajn starého loga autoservisu",
        "freelancerId": "f",
        "freelancer": {"displayName": name},
        "price": price,
        "deliveryDays": 7,
        "message": "Správa.",
        "status": status,
    }


DRAFT = {
    "category": "grafika",
    "title": "Logo pre kaviareň v Košiciach",
    "description": "Otvárame kaviareň v Košiciach a potrebujeme logo a farby značky.",
    "budget_type": "fixed",
    "budget_amount": 200,
    "city": "Košice",
    "skills": ["Logo dizajn", "Branding"],
}


@pytest.fixture
def api() -> FakeApi:
    return FakeApi(
        routes={
            "/api/v1/listings/mine": {
                "items": [{"id": LOGO_ID, "title": "Redizajn starého loga autoservisu"}]
            },
            f"/api/v1/listings/{LOGO_ID}/offers": [
                offer(LUCIA_OFFER, "Lucia Bartošová", 280),
                offer(MAREK_OFFER, "Marek Hruška", 190),
            ],
            f"/api/v1/listings/{LOGO_ID}": {
                "id": LOGO_ID,
                "title": "Redizajn starého loga autoservisu",
                "owner": {"displayName": "Autoservis Novák"},
                "budget": {"type": "fixed", "amount": 300},
            },
        },
        posts={
            "/api/v1/listings": {"id": NEW_LISTING, "title": DRAFT["title"]},
            f"/api/v1/offers/{LUCIA_OFFER}/accept": offer(LUCIA_OFFER, "Lucia Bartošová", 280, "accepted"),
            f"/api/v1/listings/{LOGO_ID}/offers": {"id": "new-offer"},
        },
    )


class Human:
    """Odpovedá na potvrdenie podľa scenára a zapamätá si, čo videl."""

    def __init__(self, answer: bool) -> None:
        self.answer = answer
        self.previews: list[str] = []

    def __call__(self, tool_name: str, preview: str) -> bool:
        self.previews.append(preview)
        return self.answer


def run(api: FakeApi, name: str, confirm: Human | None, **arguments: Any) -> ToolOutcome:
    registry = ToolRegistry(build_write_tools(api, "http://web"))  # type: ignore[arg-type]
    return registry.execute(ToolCall("c1", name, arguments), confirm)


def test_declined_confirmation_writes_nothing(api: FakeApi) -> None:
    human = Human(answer=False)

    outcome = run(api, "create_listing", human, **DRAFT)

    assert api.posted == []  # žiadne HTTP volanie
    assert outcome.confirmed is False
    assert outcome.data["status"] == "declined"
    assert "Logo pre kaviareň v Košiciach" in human.previews[0]
    assert "200 € (pevná cena)" in human.previews[0]


def test_without_confirm_function_nothing_is_ever_written(api: FakeApi) -> None:
    outcome = run(api, "create_listing", None, **DRAFT)

    assert api.posted == []
    assert outcome.confirmed is False


def test_confirmed_listing_is_created_with_full_api_shape(api: FakeApi) -> None:
    outcome = run(api, "create_listing", Human(answer=True), **DRAFT)

    path, body = api.posted[0]
    assert path == "/api/v1/listings"
    assert body is not None
    assert body["budget"] == {"type": "fixed", "amount": 200}
    assert body["scope"] == {"size": "small", "duration": "do 2 týždňov"}
    assert outcome.confirmed is True
    assert outcome.data["url"] == f"http://web/inzeraty/{NEW_LISTING}"


def test_accept_preview_warns_that_other_offers_get_rejected(api: FakeApi) -> None:
    human = Human(answer=True)

    outcome = run(api, "accept_offer", human, offer_id=LUCIA_OFFER)

    assert "Lucia Bartošová" in human.previews[0]
    assert "Ostatné čakajúce ponuky (1) budú automaticky odmietnuté" in human.previews[0]
    assert "nedá vrátiť" in human.previews[0]
    assert api.posted == [(f"/api/v1/offers/{LUCIA_OFFER}/accept", None)]
    assert outcome.data["status"] == "accepted"


def test_offer_on_someone_elses_listing_fails_before_asking(api: FakeApi) -> None:
    human = Human(answer=True)

    outcome = run(api, "accept_offer", human, offer_id="00000000-0000-0000-0000-000000000999")

    assert outcome.is_error
    assert outcome.data["error"] == "offer_not_found"
    assert human.previews == []  # človeka sa nepýta na niečo, čo aj tak nejde
    assert api.posted == []


def test_send_offer_preview_names_the_listing(api: FakeApi) -> None:
    human = Human(answer=True)

    run(
        api,
        "send_offer",
        human,
        listing_id=LOGO_ID,
        price=250,
        delivery_days=10,
        message="Logá pre autoservisy robím 5 rokov, pripravím 3 návrhy.",
    )

    assert (
        "„Redizajn starého loga autoservisu“ (Autoservis Novák, rozpočet 300 € (pevná cena))"
        in human.previews[0]
    )
    assert api.posted[0][1] == {
        "price": 250,
        "deliveryDays": 10,
        "message": "Logá pre autoservisy robím 5 rokov, pripravím 3 návrhy.",
    }
