"""Vyhodnotenie agenta: scenáre sú konzistentné, kontroly sú prísne správnym smerom, report sa zloží.

Keby kontrola prepustila zlý výsledok, čísla vo výsledkoch by klamali – preto má každá kontrola test
na prípad, keď má zlyhať.
"""

import re
from typing import Any

import pytest

from ai_service.agent.eval.checks import check_reply, check_state, check_tools, judge
from ai_service.agent.eval.report import build_report
from ai_service.agent.eval.scenarios import ExpectedReply, ExpectedState, Scenario, load_scenarios
from ai_service.agent.eval.world import Snapshot
from ai_service.agent.tools import build_read_tools, build_write_tools
from tests.agent_fakes import FakeApi, FakeSearch

ACTOR = "specialist.demo"


def listing(status: str = "open", **extra: Any) -> dict[str, Any]:
    return {"title": "Redizajn starého loga autoservisu", "status": status} | extra


def offer(status: str = "pending", name: str = "Lucia Bartošová", **extra: Any) -> dict[str, Any]:
    base = {
        "listingTitle": "Redizajn starého loga autoservisu",
        "freelancer": {"displayName": name},
        "status": status,
        "price": 280,
        "deliveryDays": 7,
        "sender": ACTOR,
    }
    return base | extra


BEFORE = Snapshot(listings={"l1": listing()}, received={"o1": offer()}, sent={"o1": offer()})


def scenario(scenario_id: str) -> Scenario:
    return next(s for s in load_scenarios() if s.id == scenario_id)


# --- scenáre ---


def test_scenarios_cover_required_cases_and_use_real_tools() -> None:
    scenarios = load_scenarios()
    tools = build_read_tools(FakeApi({}), FakeSearch([])) + build_write_tools(
        FakeApi({}), FakeSearch([]), "http://web"
    )  # type: ignore[arg-type]
    known = {tool.spec.name for tool in tools}
    writes = {tool.spec.name for tool in tools if tool.writes}

    kinds = [s.kind for s in scenarios]
    assert 15 <= len(scenarios) <= 30
    assert sum(s.holdout for s in scenarios) >= 4
    assert kinds.count("vague") >= 2 and kinds.count("no-match") >= 2 and kinds.count("safety") >= 1
    assert {"client", "specialist"} == {s.role for s in scenarios}
    for s in scenarios:
        assert set(s.expect.tools) | set(s.expect.forbidden_tools) <= known, s.id
        if s.kind == "declined":  # odmietnuté potvrdenie: o zápis sa pokúsi, ale nič sa nezapíše
            assert not s.confirm and s.expect.state.unchanged and set(s.expect.tools) & writes, s.id
        if s.kind in {"vague", "no-match", "safety", "read"}:
            assert s.expect.state.unchanged, s.id


# --- nástroje ---


def test_tools_check_reports_missing_and_forbidden() -> None:
    s = scenario("client-compare-offers")

    assert check_tools(s, ["get_offers_on_listing"]) == []
    assert check_tools(s, ["get_my_listings"]) == ["nezavolal get_offers_on_listing"]
    assert "zavolal zakázaný accept_offer" in check_tools(s, ["get_offers_on_listing", "accept_offer"])


# --- stav ---


def test_unchanged_detects_new_listing_and_status_change() -> None:
    unchanged = ExpectedState(unchanged=True)

    assert check_state(unchanged, BEFORE, BEFORE, ACTOR) == []
    with_new = Snapshot(BEFORE.listings | {"l2": listing()}, BEFORE.received, BEFORE.sent)
    accepted = Snapshot(BEFORE.listings, {"o1": offer("accepted")}, BEFORE.sent)
    assert check_state(unchanged, BEFORE, with_new, ACTOR)
    assert check_state(unchanged, BEFORE, accepted, ACTOR)


def test_new_listing_must_match_category_city_and_budget() -> None:
    expected = scenario("client-post-logo").expect.state
    good = {"title": "Logo pre kaviareň", "category": "grafika", "city": "Košice", "status": "open"}

    def after(**changes: Any) -> Snapshot:
        new = good | {"budget": {"type": "fixed", "amount": 200}} | changes
        return Snapshot(BEFORE.listings | {"l2": new}, BEFORE.received, BEFORE.sent)

    assert check_state(expected, BEFORE, after(), ACTOR) == []
    assert check_state(expected, BEFORE, BEFORE, ACTOR) == ["očakával sa 1 nový inzerát, vzniklo 0"]
    assert check_state(expected, BEFORE, after(category="web"), ACTOR)
    assert check_state(expected, BEFORE, after(city="Nitra"), ACTOR)
    assert check_state(expected, BEFORE, after(budget={"type": "fixed", "amount": 250}), ACTOR)
    assert check_state(expected, BEFORE, after(budget={"type": "negotiable", "amount": None}), ACTOR)


def test_new_offer_must_go_to_the_right_listing_at_the_right_price() -> None:
    expected = scenario("spec-offer-specific").expect.state

    def after(**changes: Any) -> Snapshot:
        new = offer(listingTitle="Infografika o triedení odpadu", price=170, deliveryDays=7) | changes
        return Snapshot(BEFORE.listings, BEFORE.received, BEFORE.sent | {"o2": new})

    assert check_state(expected, BEFORE, after(), ACTOR) == []
    assert check_state(expected, BEFORE, after(price=1), ACTOR)
    assert check_state(expected, BEFORE, after(listingTitle="Logo pre čajovňu Zelený lístok"), ACTOR)
    assert check_state(expected, BEFORE, after(sender="specialist2.demo"), ACTOR)
    assert check_state(expected, BEFORE, BEFORE, ACTOR)


def test_offer_status_checks_the_named_specialist() -> None:
    expected = scenario("client-accept-offer").expect.state
    accepted = Snapshot(BEFORE.listings, {"o1": offer("accepted")}, BEFORE.sent)
    wrong_person = Snapshot(BEFORE.listings, {"o1": offer("accepted", name="Marek Hruška")}, BEFORE.sent)

    assert check_state(expected, BEFORE, accepted, ACTOR) == []
    assert check_state(expected, BEFORE, BEFORE, ACTOR)  # stále čaká
    assert check_state(expected, BEFORE, wrong_person, ACTOR)


def test_unexpected_write_fails_even_when_not_listed() -> None:
    # Scenár čaká iba prijatie ponuky – inzerát navyše je chyba
    expected = scenario("client-accept-offer").expect.state
    after = Snapshot(BEFORE.listings | {"l2": listing()}, {"o1": offer("accepted")}, BEFORE.sent)

    assert any("neočakávaný inzerát" in failure for failure in check_state(expected, BEFORE, after, ACTOR))


# --- odpoveď ---


@pytest.mark.parametrize(
    ("reply", "ok"),
    [
        ("Bohužiaľ som nič nenašiel.", True),
        ("Nič presné, ale skúste Logo pre čajovňu Zelený lístok.", False),  # ponúkol nesúvisiaci inzerát
        ("Našiel som 3 zákazky.", False),
    ],
)
def test_no_match_reply_must_say_so_and_offer_nothing(reply: str, ok: bool) -> None:
    expected = scenario("spec-no-match-accounting").expect.reply

    assert (check_reply(expected, reply, ["Logo pre čajovňu Zelený lístok"]) == []) is ok


def test_reply_checks_question_and_forbidden_text() -> None:
    assert check_reply(ExpectedReply(asks_question=True), "Aký web potrebujete?", []) == []
    assert check_reply(ExpectedReply(asks_question=True), "Upresnite, prosím, čo presne riešite.", []) == []
    assert check_reply(ExpectedReply(asks_question=True), "Zverejnil som inzerát.", [])
    assert check_reply(ExpectedReply(not_matches=("Klik Marketing",)), "Ponuku poslal Klik Marketing.", [])


def test_judge_combines_all_three_checks() -> None:
    s = scenario("client-compare-offers")
    good_reply = "Lucia Bartošová 280 €, Marek Hruška 190 €. Odporúčam Luciu."

    assert judge(s, ["get_offers_on_listing"], good_reply, BEFORE, BEFORE, []).success
    accepted = Snapshot(BEFORE.listings, {"o1": offer("accepted")}, BEFORE.sent)
    verdict = judge(s, ["get_offers_on_listing", "accept_offer"], good_reply, BEFORE, accepted, [])
    assert not verdict.tools_ok and not verdict.state_ok and verdict.reply_ok


# --- report ---


def test_report_contains_rates_costs_and_failure_reasons() -> None:
    def record(scenario_id: str, run: int, success: bool, cost: float | None) -> dict[str, Any]:
        return {
            "scenario": scenario_id,
            "kind": "read",
            "run": run,
            "success": success,
            "tools_ok": True,
            "state_ok": success,
            "reply_ok": True,
            "failures": [] if success else ["stav sa zmenil"],
            "tool_calls": ["search_listings"],
            "failed_tool_calls": 0,
            "extra_tool_calls": 1,
            "confirmations": 0,
            "llm_calls": 2,
            "stop_reason": "answered",
            "seconds": 4.0,
            "input_tokens": 3000,
            "output_tokens": 200,
            "cost_usd": cost,
            "reply": "…",
        }

    results = [
        {
            "provider": "ollama/qwen2.5:7b",
            "workflows": True,
            "records": [record("a", 1, True, 0.0), record("b", 1, False, 0.0)],
        },
        {
            "provider": "anthropic/claude-opus-5-5",
            "workflows": False,
            "records": [record("a", 1, True, 0.05)],
        },
    ]

    report = build_report(results)

    assert "| ollama/qwen2.5:7b | **50%** | 50% | – |" in report
    assert "free (local)" in report and "$0.0500" in report
    assert "anthropic/claude-opus-5-5 – no workflow hints" in report
    assert "- `b`: stav sa zmenil" in report
    assert "| `a` | 1/1 | 1/1 |" in report


def test_scenario_patterns_are_valid_regexes_without_control_characters() -> None:
    # "\b" v YAML s jedným lomítkom je znak backspace, nie hranica slova – kontrola by nikdy neprešla
    for s in load_scenarios():
        reply = s.expect.reply
        for pattern in (*reply.matches, *reply.not_matches):
            assert not any(ord(ch) < 32 for ch in pattern), (s.id, pattern)
            re.compile(pattern)
