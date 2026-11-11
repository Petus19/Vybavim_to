"""Claude poskytovateľ s falošným SDK klientom: preklad správ tam a späť, tokeny, odmietnutie, chyby."""

from types import SimpleNamespace
from typing import Any

import anthropic
import httpx2 as httpx
import pytest

from ai_service.agent.llm import (
    AssistantMessage,
    LLMError,
    ToolCall,
    ToolResultMessage,
    ToolSpec,
    Usage,
    UserMessage,
)
from ai_service.agent.pricing import estimate_cost
from ai_service.agent.providers.anthropic import AnthropicProvider

TOOLS = [ToolSpec("search_listings", "Hľadá.", {"type": "object", "properties": {}})]


def response(content: list[Any], stop_reason: str = "tool_use") -> SimpleNamespace:
    usage = SimpleNamespace(
        input_tokens=50, output_tokens=20, cache_read_input_tokens=900, cache_creation_input_tokens=100
    )
    return SimpleNamespace(content=content, stop_reason=stop_reason, usage=usage)


class FakeMessages:
    def __init__(self, result: Any) -> None:
        self.result = result
        self.requests: list[dict[str, Any]] = []

    def create(self, **request: Any) -> Any:
        self.requests.append(request)
        if isinstance(self.result, Exception):
            raise self.result
        return self.result


def provider(
    result: Any, model: str = "claude-haiku-5-5"
) -> tuple[AnthropicProvider, FakeMessages, FakeMessages]:
    plain, beta = FakeMessages(result), FakeMessages(result)
    client = SimpleNamespace(messages=plain, beta=SimpleNamespace(messages=beta))
    return AnthropicProvider(model, client=client), plain, beta


def test_parses_text_tool_calls_and_usage() -> None:
    blocks = [
        SimpleNamespace(type="thinking", thinking=""),
        SimpleNamespace(type="text", text="Pozriem sa."),
        SimpleNamespace(type="tool_use", id="toolu_1", name="search_listings", input={"query": "logo"}),
    ]
    claude, plain, _ = provider(response(blocks))

    completion = claude.complete("system", [UserMessage("nájdi logo")], TOOLS)

    assert completion.message.text == "Pozriem sa."
    assert completion.message.tool_calls == (ToolCall("toolu_1", "search_listings", {"query": "logo"}),)
    assert completion.message.raw is blocks  # pôvodné bloky (aj premýšľanie) sa vrátia API nezmenené
    assert completion.usage == Usage(50, 20, cache_read_tokens=900, cache_write_tokens=100)
    request = plain.requests[0]
    assert request["tools"][0]["input_schema"] == {"type": "object", "properties": {}}
    assert request["output_config"] == {"effort": "medium"}
    assert "temperature" not in request and "thinking" not in request


def test_history_replays_raw_blocks_and_groups_tool_results_in_one_user_message() -> None:
    raw = [SimpleNamespace(type="tool_use", id="a", name="x", input={})]
    history = [
        UserMessage("ahoj"),
        AssistantMessage("", (ToolCall("a", "x", {}), ToolCall("b", "y", {})), raw=raw),
        ToolResultMessage("a", "x", '{"ok": 1}'),
        ToolResultMessage("b", "y", '{"error": "e"}', is_error=True),
        AssistantMessage("Limit krokov."),  # správa, ktorú nevyrobil Claude
    ]
    claude, plain, _ = provider(response([SimpleNamespace(type="text", text="ok")], "end_turn"))

    claude.complete("system", history, TOOLS)

    messages = plain.requests[0]["messages"]
    assert [m["role"] for m in messages] == ["user", "assistant", "user", "assistant"]
    assert messages[1]["content"] is raw
    assert [b["tool_use_id"] for b in messages[2]["content"]] == ["a", "b"]
    assert messages[2]["content"][1]["is_error"] is True
    assert messages[3]["content"] == [{"type": "text", "text": "Limit krokov."}]


def test_fallbacks_use_beta_endpoint_only_for_supported_models() -> None:
    done = response([SimpleNamespace(type="text", text="ok")], "end_turn")
    opus, opus_plain, opus_beta = provider(done, "claude-opus-5-5")
    haiku, haiku_plain, haiku_beta = provider(done, "claude-haiku-5-5")

    opus.complete("system", [UserMessage("x")], TOOLS)
    haiku.complete("system", [UserMessage("x")], TOOLS)

    assert opus_beta.requests[0]["fallbacks"] == "default" and not opus_plain.requests
    assert haiku_plain.requests and not haiku_beta.requests


def test_refusal_becomes_plain_message_without_tool_calls() -> None:
    claude, _, _ = provider(response([], "refusal"))

    completion = claude.complete("system", [UserMessage("x")], TOOLS)

    assert "odmietol" in completion.message.text
    assert completion.message.tool_calls == ()


def test_connection_error_becomes_llm_error() -> None:
    error = anthropic.APIConnectionError(request=httpx.Request("POST", "https://api.anthropic.com"))
    claude, _, _ = provider(error)

    with pytest.raises(LLMError, match="nie je dostupné"):
        claude.complete("system", [UserMessage("x")], TOOLS)


def test_cost_estimate_uses_cache_prices() -> None:
    usage = Usage(input_tokens=1_000_000, output_tokens=100_000, cache_read_tokens=2_000_000)

    assert estimate_cost("anthropic/claude-opus-5-5", usage) == pytest.approx(4.0 + 2.0 + 0.4)
    assert estimate_cost("ollama/qwen2.5:7b", usage) == 0.0
    assert estimate_cost("anthropic/unknown-model", usage) is None
