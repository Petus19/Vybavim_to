"""Načítanie a overenie scenárov zo scenarios.yaml. Neznáme pole = chyba (preklep by inak ticho
vypol kontrolu a scenár by „prešiel“)."""

from pathlib import Path
from typing import Literal

import yaml
from pydantic import BaseModel, ConfigDict, Field

from ai_service.agent.prompts import Role

SCENARIOS_FILE = Path(__file__).parent / "scenarios.yaml"

Kind = Literal["read", "act", "declined", "vague", "no-match", "safety"]


class Strict(BaseModel):
    model_config = ConfigDict(extra="forbid", frozen=True)


class NewListing(Strict):
    category: str | None = None
    city: str | None = None
    max_budget: float | None = None
    title_matches: str | None = None


class NewOffer(Strict):
    listing_matches: str | None = None
    price: float | None = None
    max_price: float | None = None
    delivery_days: int | None = None


class OfferStatus(Strict):
    listing_matches: str
    specialist: str
    status: Literal["pending", "accepted", "rejected", "withdrawn"]


class ExpectedState(Strict):
    unchanged: bool = False  # nič sa nesmie zapísať
    new_listing: NewListing | None = None  # presne jeden nový inzerát zadávateľa
    new_offer: NewOffer | None = None  # presne jedna nová ponuka špecialistu
    offer_status: OfferStatus | None = None  # ponuka na inzerát zadávateľa má daný stav


class ExpectedReply(Strict):
    asks_question: bool = False
    matches: tuple[str, ...] = ()
    not_matches: tuple[str, ...] = ()
    mentions_no_listing: bool = False  # v odpovedi nesmie byť názov žiadneho existujúceho inzerátu


class Expectation(Strict):
    tools: tuple[str, ...] = ()
    forbidden_tools: tuple[str, ...] = ()
    state: ExpectedState = Field(default_factory=ExpectedState)
    reply: ExpectedReply = Field(default_factory=ExpectedReply)


class Scenario(Strict):
    id: str
    kind: Kind
    role: Role
    turns: tuple[str, ...] = Field(min_length=1)
    confirm: bool = True
    ideal_tool_calls: int = Field(ge=0)
    expect: Expectation

    @property
    def user(self) -> str:
        return "client.demo" if self.role == "client" else "specialist.demo"


def load_scenarios(path: Path = SCENARIOS_FILE) -> list[Scenario]:
    raw = yaml.safe_load(path.read_text(encoding="utf-8"))
    scenarios = [Scenario.model_validate(item) for item in raw]
    ids = [s.id for s in scenarios]
    if len(ids) != len(set(ids)):
        raise ValueError("Scenáre majú duplicitné ID.")
    return scenarios
