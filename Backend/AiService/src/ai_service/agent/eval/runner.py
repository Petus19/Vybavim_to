"""Spustí scenáre s daným modelom proti bežiacemu systému a zapíše surové výsledky (JSON)."""

import json
import re
from collections.abc import Callable, Sequence
from dataclasses import asdict, dataclass, field
from datetime import UTC, date, datetime
from pathlib import Path

from ai_service.agent.config import AgentSettings
from ai_service.agent.eval.checks import REPLY_FAILURE_PREFIXES, check_reply, judge
from ai_service.agent.eval.scenarios import Scenario
from ai_service.agent.eval.world import World
from ai_service.agent.llm import LLMProvider, Usage
from ai_service.agent.loop import Agent
from ai_service.agent.marketplace import ApiError, SearchClient
from ai_service.agent.pricing import estimate_cost
from ai_service.agent.prompts import system_prompt
from ai_service.agent.tools import ToolRegistry, build_read_tools, build_write_tools

RESULTS_DIR = Path(__file__).parent / "results"


@dataclass
class RunRecord:
    """Jeden beh jedného scenára – všetko, čo treba na tabuľku výsledkov aj na hľadanie príčiny zlyhania."""

    scenario: str
    kind: str
    holdout: bool
    run: int
    success: bool
    tools_ok: bool
    state_ok: bool
    reply_ok: bool
    failures: list[str]
    tool_calls: list[str]
    tool_trace: list[dict[str, object]]  # argumenty a (skrátený) výsledok každého volania – na rozbor zlyhaní
    failed_tool_calls: int
    extra_tool_calls: int  # o koľko viac volaní, než je najkratšia možná cesta
    confirmations: int
    llm_calls: int
    stop_reason: str
    seconds: float
    input_tokens: int
    output_tokens: int
    cost_usd: float | None
    reply: str


@dataclass
class EvalRun:
    provider: str
    workflows: bool  # boli v prompte typické postupy?
    started: str
    records: list[RunRecord] = field(default_factory=list)

    @property
    def label(self) -> str:
        return self.provider + (" (with workflow hints)" if self.workflows else "")

    def save(self, directory: Path = RESULTS_DIR) -> Path:
        directory.mkdir(parents=True, exist_ok=True)
        name = re.sub(r"[^a-z0-9.]+", "-", self.label.lower()).strip("-")
        path = directory / f"{name}.json"
        path.write_text(json.dumps(asdict(self), ensure_ascii=False, indent=1), encoding="utf-8")
        return path


def run_scenario(
    scenario: Scenario,
    run: int,
    provider: LLMProvider,
    settings: AgentSettings,
    world: World,
    workflows: bool,
    public_titles: Sequence[str],
) -> RunRecord:
    world.reset()
    before = world.snapshot()

    api = world.client(scenario.user)
    try:
        user_name = api.get("/api/v1/profiles/me")["displayName"]
    except ApiError:
        user_name = scenario.user
    search = SearchClient(settings.ai_service_url, settings.request_timeout)
    tools = ToolRegistry(
        [*build_read_tools(api, search), *build_write_tools(api, search, settings.web_base_url)]
    )

    confirmations: list[str] = []

    def confirm(tool_name: str, _preview: str) -> bool:
        """„Človek“ zo scenára: na každé potvrdenie odpovie rovnako (confirm: true/false)."""
        confirmations.append(tool_name)
        return scenario.confirm

    prompt = system_prompt(scenario.role, user_name, date.today(), workflows)
    agent = Agent(provider, tools, prompt, settings.max_steps, confirm=confirm)
    try:
        replies = [agent.ask(turn) for turn in scenario.turns]
    finally:
        search.close()

    after = world.snapshot()
    steps = [step for reply in replies for step in reply.tool_steps]
    called = [step.call.name for step in steps]
    verdict = judge(scenario, called, replies[-1].text, before, after, public_titles)
    if any(reply.stop_reason != "answered" for reply in replies):
        verdict.reply_ok = False
        verdict.failures.append(f"agent neskončil odpoveďou ({replies[-1].stop_reason})")

    usage = sum((reply.usage for reply in replies), Usage())
    return RunRecord(
        scenario=scenario.id,
        kind=scenario.kind,
        holdout=scenario.holdout,
        run=run,
        success=verdict.success,
        tools_ok=verdict.tools_ok,
        state_ok=verdict.state_ok,
        reply_ok=verdict.reply_ok,
        failures=verdict.failures,
        tool_calls=called,
        tool_trace=[
            {
                "tool": step.call.name,
                "arguments": step.call.arguments,
                "error": step.outcome.is_error,
                "confirmed": step.outcome.confirmed,
                "result": step.outcome.content[:600],
            }
            for step in steps
        ],
        failed_tool_calls=sum(1 for step in steps if step.outcome.is_error),
        extra_tool_calls=max(0, len(called) - scenario.ideal_tool_calls),
        confirmations=len(confirmations),
        llm_calls=sum(reply.llm_calls for reply in replies),
        stop_reason=replies[-1].stop_reason,
        seconds=round(sum(reply.seconds for reply in replies), 2),
        input_tokens=usage.total_input_tokens,
        output_tokens=usage.output_tokens,
        cost_usd=estimate_cost(provider.name, usage),
        reply=replies[-1].text,
    )


def run_evaluation(
    scenarios: Sequence[Scenario],
    provider: LLMProvider,
    settings: AgentSettings,
    runs: int,
    workflows: bool,
    on_record: Callable[[RunRecord], None] = lambda _record: None,
) -> EvalRun:
    world = World(settings)
    world.reset()
    public_titles = world.public_titles()
    result = EvalRun(provider.name, workflows, datetime.now(UTC).isoformat(timespec="seconds"))
    for run in range(1, runs + 1):
        for scenario in scenarios:
            record = run_scenario(scenario, run, provider, settings, world, workflows, public_titles)
            result.records.append(record)
            on_record(record)
    world.reset()  # po vyhodnotení nechaj demo dáta čisté
    world.close()
    return result


def rescore(directory: Path, scenarios: Sequence[Scenario], public_titles: Sequence[str]) -> int:
    """Znova vyhodnotí ODPOVEDE v uložených výsledkoch (po oprave kontroly odpovede) bez nového behu modelu.

    Kontroly nástrojov a stavu sa nemenia – stav systému po behu už nie je k dispozícii, ich výsledok
    sa preberá z uloženého záznamu. Vráti počet záznamov, ktorým sa zmenil výsledok.
    """
    by_id = {scenario.id: scenario for scenario in scenarios}
    changed = 0
    for path in sorted(directory.glob("*.json")):
        data = json.loads(path.read_text(encoding="utf-8"))
        for record in data["records"]:
            scenario = by_id.get(record["scenario"])
            if scenario is None:
                continue
            kept = [f for f in record["failures"] if not f.startswith(REPLY_FAILURE_PREFIXES)]
            reply_failures = check_reply(scenario.expect.reply, record["reply"], public_titles)
            if record["stop_reason"] != "answered":
                reply_failures.append(f"agent neskončil odpoveďou ({record['stop_reason']})")
            success = record["tools_ok"] and record["state_ok"] and not reply_failures
            if success != record["success"]:
                changed += 1
            record.update(reply_ok=not reply_failures, success=success, failures=kept + reply_failures)
        path.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")
    return changed
