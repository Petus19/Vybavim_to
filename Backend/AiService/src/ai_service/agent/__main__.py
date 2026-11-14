"""Agent v termináli: `uv run python -m ai_service.agent --as client` (alebo `--as specialist`).

Vypisuje každé volanie nástroja a jeho výsledok, aby bolo vidno, čo agent robí.
Prihlási sa demo účtom (iba vývoj, heslo z README) – rovnaké práva ako ten používateľ na webe.
"""

import argparse
import json
import os
import sys
from datetime import date
from typing import Any

from ai_service.agent.config import AgentSettings
from ai_service.agent.describe import summarize_result
from ai_service.agent.llm import LLMError, ToolCall
from ai_service.agent.loop import Agent, AgentReply, ToolStep
from ai_service.agent.marketplace import ApiError, MarketplaceClient, SearchClient
from ai_service.agent.pricing import estimate_cost
from ai_service.agent.prompts import Role, system_prompt
from ai_service.agent.providers import create_provider
from ai_service.agent.tools import ToolRegistry, build_read_tools, build_write_tools

DEMO_ACCOUNTS: dict[Role, str] = {"client": "client.demo", "specialist": "specialist.demo"}

# Farby v termináli (Windows Terminal aj PowerShell ich podporujú)
DIM, CYAN, GREEN, RED, YELLOW = "\033[2m", "\033[36m", "\033[32m", "\033[31m", "\033[33m"
BOLD, RESET = "\033[1m", "\033[0m"


def _arguments(call: ToolCall) -> str:
    return ", ".join(f"{k}={json.dumps(v, ensure_ascii=False)}" for k, v in call.arguments.items())


def on_event(kind: str, payload: Any) -> None:
    if kind == "tool_call":
        print(f"  {CYAN}→ {payload.name}({_arguments(payload)}){RESET}")
    elif kind == "tool_result":
        step: ToolStep = payload
        color = RED if step.outcome.is_error else GREEN
        mark = "✗" if step.outcome.is_error else "✓"
        print(f"    {color}{mark} {summarize_result(step.outcome.data)}{RESET}")


def confirm(tool_name: str, preview: str) -> bool:
    """Človek musí každý zápis potvrdiť – model sem nemá prístup, rozhoduje iba odpoveď v termináli."""
    print(f"\n  {YELLOW}{BOLD}⚠ Agent chce vykonať akciu ({tool_name}):{RESET}")
    for line in preview.splitlines():
        print(f"  {YELLOW}│{RESET} {line}")
    try:
        answer = input(f"  {YELLOW}Potvrdiť? [y/N]:{RESET} ").strip().lower()
    except (EOFError, KeyboardInterrupt):
        answer = ""
    approved = answer in {"y", "yes", "a", "ano", "áno"}
    print(f"    {GREEN + '✓ potvrdené' if approved else RED + '✗ zamietnuté'}{RESET}")
    return approved


def _stats(reply: AgentReply, provider_name: str) -> str:
    tokens = f"{reply.usage.total_input_tokens} + {reply.usage.output_tokens} tokenov"
    steps = f"{reply.llm_calls} volaní modelu · {len(reply.tool_steps)} nástrojov"
    cost = estimate_cost(provider_name, reply.usage)
    price = f" · ~${cost:.4f}" if cost else ""  # lokálny model je zadarmo, cenu neukazujeme
    return f"{steps} · {tokens}{price} · {reply.seconds:.1f} s"


def main() -> None:
    sys.stdout.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]  # slovenčina vo Windows konzole
    parser = argparse.ArgumentParser(description="Vybavím to – AI agent v termináli")
    parser.add_argument("--as", dest="role", choices=["client", "specialist"], default="client")
    parser.add_argument("--user", help="prihlasovacie meno (predvolene demo účet pre rolu)")
    parser.add_argument("--password", default=os.environ.get("AGENT_PASSWORD", "Demo1234"))
    args = parser.parse_args()

    settings = AgentSettings()
    role: Role = args.role
    login = args.user or DEMO_ACCOUNTS[role]

    api = MarketplaceClient(settings.api_base_url, settings.request_timeout)
    try:
        api.login(login, args.password)
        provider = create_provider(settings)
    except (ApiError, LLMError) as error:
        sys.exit(f"Nepodarilo sa spustiť agenta: {error}")

    try:
        user_name = api.get("/api/v1/profiles/me")["displayName"]
    except ApiError:
        user_name = login

    search = SearchClient(settings.ai_service_url, settings.request_timeout)
    tools = ToolRegistry(
        [*build_read_tools(api, search), *build_write_tools(api, search, settings.web_base_url)]
    )
    agent = Agent(
        provider,
        tools,
        system_prompt(role, user_name, date.today()),
        settings.max_steps,
        on_event,
        confirm,
    )

    print(f"{BOLD}Vybavím to – agent{RESET} · {user_name} ({login}) · model {provider.name}")
    print(f"{DIM}Napíš požiadavku po slovensky. Koniec: prázdny riadok alebo Ctrl+C.{RESET}\n")
    while True:
        try:
            text = input(f"{BOLD}Ty:{RESET} ").strip()
        except (EOFError, KeyboardInterrupt):
            break
        if not text:
            break
        reply = agent.ask(text)
        print(f"\n{BOLD}Agent:{RESET} {reply.text}")
        print(f"{DIM}({_stats(reply, provider.name)}){RESET}\n")


if __name__ == "__main__":
    main()
