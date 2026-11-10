"""Slučka agenta so scenárom modelu: poradie krokov, chyby nástrojov, limit krokov, výpadok modelu."""

import json

from pydantic import BaseModel

from ai_service.agent.llm import ToolResultMessage, ToolSpec
from ai_service.agent.loop import STEP_LIMIT_MESSAGE, Agent
from ai_service.agent.marketplace import ApiError
from ai_service.agent.tools import Tool, ToolRegistry
from tests.agent_fakes import ScriptedProvider, answer, call, llm_down


class EchoInput(BaseModel):
    text: str


def echo(args: EchoInput) -> dict[str, str]:
    return {"echo": args.text}


def failing(_: EchoInput) -> dict[str, str]:
    raise ApiError(403, "listing.forbidden", "Tento inzerát nie je váš.")


TOOLS = ToolRegistry(
    [
        Tool(ToolSpec("echo", "Vráti text.", EchoInput.model_json_schema()), EchoInput, echo),
        Tool(ToolSpec("failing", "Vždy zlyhá.", EchoInput.model_json_schema()), EchoInput, failing),
    ]
)


def agent(provider: ScriptedProvider, max_steps: int = 8) -> Agent:
    return Agent(provider, TOOLS, "system", max_steps)


def tool_results(provider: ScriptedProvider) -> list[ToolResultMessage]:
    return [m for m in provider.seen[-1] if isinstance(m, ToolResultMessage)]


def test_calls_tool_and_returns_final_answer() -> None:
    provider = ScriptedProvider([call("echo", text="ahoj"), answer("Hotovo.")])

    reply = agent(provider).ask("povedz ahoj")

    assert reply.text == "Hotovo."
    assert reply.stop_reason == "answered"
    assert [step.call.name for step in reply.tool_steps] == ["echo"]
    assert json.loads(tool_results(provider)[0].content) == {"echo": "ahoj"}
    assert (reply.llm_calls, reply.usage.input_tokens) == (2, 200)


def test_api_error_is_returned_to_model_not_raised() -> None:
    provider = ScriptedProvider([call("failing", text="x"), answer("Nemáte oprávnenie.")])

    reply = agent(provider).ask("urob to")

    result = tool_results(provider)[0]
    assert result.is_error
    assert json.loads(result.content)["error"] == "listing.forbidden"
    assert reply.text == "Nemáte oprávnenie."


def test_invalid_arguments_and_unknown_tool_are_reported_to_model() -> None:
    provider = ScriptedProvider([call("echo", wrong=1), call("delete_everything"), answer("Ok.")])

    agent(provider).ask("niečo")

    errors = [json.loads(m.content)["error"] for m in provider.seen[-1] if isinstance(m, ToolResultMessage)]
    assert errors == ["invalid_arguments", "unknown_tool"]


def test_stops_after_step_limit() -> None:
    provider = ScriptedProvider([call("echo", text=str(i)) for i in range(10)])

    reply = agent(provider, max_steps=3).ask("cyklus")

    assert reply.stop_reason == "step_limit"
    assert reply.text == STEP_LIMIT_MESSAGE
    assert reply.llm_calls == 3


def test_model_outage_ends_with_message() -> None:
    reply = agent(ScriptedProvider([llm_down()])).ask("ahoj")

    assert reply.stop_reason == "llm_error"
    assert "neodpovedá" in reply.text


def test_remembers_conversation_between_requests() -> None:
    provider = ScriptedProvider([answer("Ktorý inzerát?"), answer("Rozumiem.")])
    bot = agent(provider)

    bot.ask("ukáž ponuky")
    bot.ask("ten s logom")

    assert len(provider.seen[-1]) == 3  # otázka, odpoveď, nová otázka
