"""Celý reťazec naostro: agent → nástroje → bežiace C# API a AI služba → kontrola stavu cez API.

Model je nahradený scenárom (deterministické, zadarmo), všetko ostatné je skutočné. Vyžaduje bežiaci
lokálny systém s demo dátami, preto sa bežne preskakuje: `uv run pytest -m stack`.
"""

import pytest

from ai_service.agent.config import AgentSettings
from ai_service.agent.eval.runner import run_scenario
from ai_service.agent.eval.scenarios import load_scenarios
from ai_service.agent.eval.world import World
from tests.agent_fakes import ScriptedProvider, answer, call

pytestmark = pytest.mark.stack


def test_scripted_agent_reads_offers_and_declined_write_changes_nothing() -> None:
    settings = AgentSettings()
    world = World(settings)
    scenarios = {s.id: s for s in load_scenarios()}

    compare = ScriptedProvider(
        [call("get_offers_on_listing", listing="logom"), answer("Lucia Bartošová 280 €, Marek Hruška 190 €.")]
    )
    record = run_scenario(scenarios["client-compare-offers"], 1, compare, settings, world, True, [])
    assert record.success, record.failures

    # Model sa pokúsi zverejniť inzerát, „človek“ zo scenára nepotvrdí → v systéme sa nič nezmení
    draft = {
        "category": "it-podpora",
        "title": "Oprava pomalého notebooku",
        "description": "Notebook sa dlho zapína, treba ho vyčistiť a zrýchliť.",
        "budget_amount": 60,
        "skills": ["Windows"],
    }
    declined = ScriptedProvider([call("create_listing", **draft), answer("Inzerát som nezverejnil.")])
    record = run_scenario(scenarios["client-post-declined"], 1, declined, settings, world, True, [])
    assert record.success, record.failures
    assert record.confirmations == 1

    world.reset()
    world.close()
