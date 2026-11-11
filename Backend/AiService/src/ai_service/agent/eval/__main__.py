"""Vyhodnotenie agenta: `uv run python -m ai_service.agent.eval --provider local|api [možnosti]`.

Potrebuje bežiace C# API (s demo dátami), AI službu a pri `local` Ollamu. Demo dáta sa pred každým
scenárom obnovia – NEPÚŠŤAJ proti databáze, v ktorej máš na demo účtoch niečo, čo chceš zachovať.
"""

import argparse
import sys

from ai_service.agent.config import AgentSettings
from ai_service.agent.eval.report import write_report
from ai_service.agent.eval.runner import RESULTS_DIR, RunRecord, rescore, run_evaluation
from ai_service.agent.eval.scenarios import load_scenarios
from ai_service.agent.eval.world import World
from ai_service.agent.llm import LLMError
from ai_service.agent.providers import create_provider


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]
    parser = argparse.ArgumentParser(description="Evaluate the agent on scripted scenarios.")
    parser.add_argument(
        "--provider", choices=["local", "api"], help="model provider (default: AGENT_PROVIDER)"
    )
    parser.add_argument("--model", help="model name for the chosen provider")
    parser.add_argument("--runs", type=int, default=1, help="how many times to run each scenario")
    parser.add_argument("--scenario", action="append", help="run only this scenario id (repeatable)")
    parser.add_argument(
        "--no-workflows", action="store_true", help="prompt without workflow hints (ablation)"
    )
    parser.add_argument("--report-only", action="store_true", help="only rebuild results.md from saved runs")
    parser.add_argument(
        "--rescore", action="store_true", help="re-apply reply checks to saved runs (after fixing a check)"
    )
    args = parser.parse_args()

    if args.report_only:
        print(f"Report: {write_report(RESULTS_DIR)}")
        return
    if args.rescore:
        world = World(AgentSettings())
        changed = rescore(RESULTS_DIR, load_scenarios(), world.public_titles())
        world.close()
        print(
            f"Prepočítané odpovede, zmenený výsledok: {changed} záznamov. Report: {write_report(RESULTS_DIR)}"
        )
        return

    overrides: dict[str, object] = {"anthropic_fallbacks": False}  # výsledok má patriť meranému modelu
    if args.provider:
        overrides["provider"] = args.provider
    settings = AgentSettings(**overrides)  # type: ignore[arg-type]
    if args.model:
        field = "ollama_model" if settings.provider == "local" else "anthropic_model"
        settings = settings.model_copy(update={field: args.model})

    scenarios = load_scenarios()
    if args.scenario:
        scenarios = [s for s in scenarios if s.id in args.scenario]
    try:
        provider = create_provider(settings)
    except LLMError as error:
        sys.exit(f"Model sa nepodarilo pripraviť: {error}")

    def show(record: RunRecord) -> None:
        mark = "✓" if record.success else "✗"
        cost = f" ${record.cost_usd:.4f}" if record.cost_usd else ""
        detail = "" if record.success else "  ← " + "; ".join(record.failures)
        tools = ",".join(record.tool_calls) or "-"
        print(
            f"  {mark} run {record.run} {record.scenario:28} {record.seconds:5.1f} s{cost}  [{tools}]{detail}"
        )

    variant = "bez postupov v prompte" if args.no_workflows else "s postupmi v prompte"
    print(f"Model {provider.name}, {len(scenarios)} scenárov, {args.runs} behov každý, {variant}")
    result = run_evaluation(scenarios, provider, settings, args.runs, not args.no_workflows, show)

    passed = sum(1 for r in result.records if r.success)
    total_cost = sum(r.cost_usd or 0 for r in result.records)
    print(f"\nÚspešnosť: {passed}/{len(result.records)} = {passed / len(result.records):.0%}", end="")
    print(f" · odhad ceny ${total_cost:.2f}" if total_cost else "")
    if not args.scenario:  # čiastočný beh neprepisuje uložené výsledky
        print(f"Výsledky: {result.save()}")
        print(f"Report:   {write_report(RESULTS_DIR)}")


if __name__ == "__main__":
    main()
