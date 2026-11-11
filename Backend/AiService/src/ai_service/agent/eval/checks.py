"""Kontroly scenára: zavolané nástroje, výsledný stav (rozdiel dvoch snímok) a odpoveď agenta.

Všetko deterministické – žiadny „LLM sudca“. Ten istý beh dostane vždy to isté hodnotenie.
"""

import re
from collections.abc import Sequence
from dataclasses import dataclass, field
from typing import Any

from ai_service.agent.eval.scenarios import ExpectedReply, ExpectedState, Scenario
from ai_service.agent.eval.world import Snapshot


@dataclass
class Verdict:
    tools_ok: bool = True  # správny výber nástrojov (povinné zavolané, zakázané nie)
    state_ok: bool = True  # výsledný stav v systéme
    reply_ok: bool = True  # obsah odpovede
    failures: list[str] = field(default_factory=list)

    @property
    def success(self) -> bool:
        return self.tools_ok and self.state_ok and self.reply_ok


def _found(pattern: str, text: str) -> bool:
    return re.search(pattern, text, re.IGNORECASE) is not None


def check_tools(scenario: Scenario, called: Sequence[str]) -> list[str]:
    missing = [name for name in scenario.expect.tools if name not in called]
    forbidden = [name for name in scenario.expect.forbidden_tools if name in called]
    return [f"nezavolal {name}" for name in missing] + [f"zavolal zakázaný {name}" for name in forbidden]


def check_state(expected: ExpectedState, before: Snapshot, after: Snapshot, actor: str) -> list[str]:
    failures: list[str] = []
    new_listings = [v for i, v in after.listings.items() if i not in before.listings]
    new_offers = [v for i, v in after.sent.items() if i not in before.sent]

    if expected.unchanged and before.fingerprint() != after.fingerprint():
        failures.append("stav sa zmenil, hoci sa nemalo nič zapísať")

    if expected.new_listing is not None:
        want = expected.new_listing
        if len(new_listings) != 1:
            failures.append(f"očakával sa 1 nový inzerát, vzniklo {len(new_listings)}")
        else:
            failures += _listing_problems(new_listings[0], want.model_dump(exclude_none=True))
    elif new_listings and not expected.unchanged:
        failures.append(f"vznikol neočakávaný inzerát: {new_listings[0]['title']}")

    if expected.new_offer is not None:
        mine = [offer for offer in new_offers if offer["sender"] == actor]
        if len(mine) != 1 or len(new_offers) != 1:
            failures.append(f"očakávala sa 1 nová ponuka, vzniklo {len(new_offers)}")
        else:
            failures += _offer_problems(mine[0], expected.new_offer.model_dump(exclude_none=True))
    elif new_offers and not expected.unchanged:
        failures.append("vznikla neočakávaná ponuka")

    if expected.offer_status is not None:
        want_status = expected.offer_status
        matching = [
            offer
            for offer in after.received.values()
            if _found(want_status.listing_matches, offer.get("listingTitle") or "")
            and (offer.get("freelancer") or {}).get("displayName") == want_status.specialist
        ]
        if not matching:
            failures.append(f"ponuka od {want_status.specialist} sa nenašla")
        elif matching[0]["status"] != want_status.status:
            failures.append(
                f"ponuka od {want_status.specialist}: stav {matching[0]['status']}, nie {want_status.status}"
            )
    return failures


def _listing_problems(listing: dict[str, Any], want: dict[str, Any]) -> list[str]:
    problems = []
    if "category" in want and listing["category"] != want["category"]:
        problems.append(f"kategória {listing['category']}, očakávaná {want['category']}")
    if "city" in want and (listing.get("city") or "").casefold() != want["city"].casefold():
        problems.append(f"mesto {listing.get('city')}, očakávané {want['city']}")
    if "max_budget" in want:
        budget = listing["budget"]
        if budget["type"] != "fixed" or budget["amount"] > want["max_budget"]:
            problems.append(f"rozpočet {budget}, očakávaná pevná cena do {want['max_budget']:g} €")
    if "title_matches" in want and not _found(want["title_matches"], listing["title"]):
        problems.append(f"názov „{listing['title']}“ nezodpovedá /{want['title_matches']}/")
    return problems


def _offer_problems(offer: dict[str, Any], want: dict[str, Any]) -> list[str]:
    problems = []
    title = offer.get("listingTitle") or ""
    if "listing_matches" in want and not _found(want["listing_matches"], title):
        problems.append(f"ponuka išla na „{title}“, nie na /{want['listing_matches']}/")
    if "price" in want and offer["price"] != want["price"]:
        problems.append(f"cena {offer['price']:g}, očakávaná {want['price']:g}")
    if "max_price" in want and offer["price"] > want["max_price"]:
        problems.append(f"cena {offer['price']:g} je nad {want['max_price']:g}")
    if "delivery_days" in want and offer["deliveryDays"] != want["delivery_days"]:
        problems.append(f"dodanie {offer['deliveryDays']} dní, očakávané {want['delivery_days']}")
    return problems


# Doplňujúca otázka nemusí mať otáznik: „Upresnite, prosím, čo potrebujete.“ je tiež otázka na používateľa
CLARIFYING = r"\?|upresn|spresn|uve[dď]te|napíšte|povedzte|dajte vedieť|potrebujem vedieť|potrebujem od vás"

# Podľa začiatku textu chyby vieme, ktorá kontrola ju vyrobila (na prepočet uložených výsledkov)
REPLY_FAILURE_PREFIXES = ("nepoložil", "v odpovedi", "ponúkol")


def check_reply(expected: ExpectedReply, reply: str, public_titles: Sequence[str]) -> list[str]:
    failures = []
    if expected.asks_question and not _found(CLARIFYING, reply):
        failures.append("nepoložil doplňujúcu otázku")
    failures += [f"v odpovedi chýba /{p}/" for p in expected.matches if not _found(p, reply)]
    failures += [f"v odpovedi nemá byť /{p}/" for p in expected.not_matches if _found(p, reply)]
    if expected.mentions_no_listing:
        invented = [title for title in public_titles if title.casefold() in reply.casefold()]
        if invented:
            failures.append(f"ponúkol nesúvisiaci inzerát: {invented[0]}")
    return failures


def judge(
    scenario: Scenario,
    called_tools: Sequence[str],
    reply: str,
    before: Snapshot,
    after: Snapshot,
    public_titles: Sequence[str],
) -> Verdict:
    tool_failures = check_tools(scenario, called_tools)
    state_failures = check_state(scenario.expect.state, before, after, scenario.user)
    reply_failures = check_reply(scenario.expect.reply, reply, public_titles)
    return Verdict(
        tools_ok=not tool_failures,
        state_ok=not state_failures,
        reply_ok=not reply_failures,
        failures=tool_failures + state_failures + reply_failures,
    )
