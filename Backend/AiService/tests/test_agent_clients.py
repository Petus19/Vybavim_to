"""HTTP vrstva agenta s falošným serverom (MockTransport): Ollama, C# API a jeho chyby, obnovenie tokenu."""

import json

import httpx2 as httpx
import pytest

from ai_service.agent.llm import AssistantMessage, ToolCall, ToolResultMessage, ToolSpec, UserMessage
from ai_service.agent.marketplace import ApiError, MarketplaceClient
from ai_service.agent.providers.ollama import OllamaProvider


def test_ollama_maps_messages_and_tool_calls() -> None:
    sent: dict[str, object] = {}

    def handler(request: httpx.Request) -> httpx.Response:
        sent.update(json.loads(request.content))
        return httpx.Response(
            200,
            json={
                "message": {
                    "content": "",
                    "tool_calls": [
                        {"function": {"name": "search_listings", "arguments": {"query": "logo"}}},
                        {"function": {"name": "get_listing", "arguments": '{"listing_id": "x"}'}},
                        {"function": {"name": "get_listing", "arguments": "{nie je json"}},
                    ],
                },
                "prompt_eval_count": 120,
                "eval_count": 15,
            },
        )

    client = httpx.Client(base_url="http://ollama", transport=httpx.MockTransport(handler))
    provider = OllamaProvider("http://ollama", "qwen2.5:7b", client=client)
    history = [
        UserMessage("nájdi logo"),
        AssistantMessage("", (ToolCall("c0", "list_categories", {}),)),
        ToolResultMessage("c0", "list_categories", "[]"),
    ]

    completion = provider.complete("system", history, [ToolSpec("search_listings", "Hľadá.", {})])

    roles = [m["role"] for m in sent["messages"]]  # type: ignore[index]
    assert roles == ["system", "user", "assistant", "tool"]
    assert sent["tools"][0]["function"]["name"] == "search_listings"  # type: ignore[index]
    calls = completion.message.tool_calls
    assert calls[0].arguments == {"query": "logo"}
    assert calls[1].arguments == {"listing_id": "x"}  # JSON ako text sa prečíta
    assert calls[2].arguments == {"_invalid_json": "{nie je json"}  # neplatný JSON nezhodí agenta
    assert (completion.usage.input_tokens, completion.usage.output_tokens) == (120, 15)


def test_api_problem_details_become_api_error() -> None:
    def handler(request: httpx.Request) -> httpx.Response:
        return httpx.Response(
            404, json={"status": 404, "title": "Inzerát neexistuje.", "code": "listing.not_found"}
        )

    api = MarketplaceClient(
        "http://api", client=httpx.Client(base_url="http://api", transport=httpx.MockTransport(handler))
    )

    with pytest.raises(ApiError) as error:
        api.get("/api/v1/listings/x")

    assert error.value.to_result() == {
        "error": "listing.not_found",
        "status": 404,
        "message": "Inzerát neexistuje.",
    }


def test_expired_token_is_refreshed_once_then_request_retried() -> None:
    requests: list[str] = []

    def handler(request: httpx.Request) -> httpx.Response:
        requests.append(f"{request.url.path} {request.headers.get('authorization')}")
        if request.url.path == "/api/v1/auth/login":
            return httpx.Response(200, json={"accessToken": "old", "refreshToken": "r1"})
        if request.url.path == "/api/v1/auth/refresh":
            return httpx.Response(200, json={"accessToken": "new", "refreshToken": "r2"})
        if request.headers.get("authorization") == "Bearer old":
            return httpx.Response(401)
        return httpx.Response(200, json={"ok": True})

    api = MarketplaceClient(
        "http://api", client=httpx.Client(base_url="http://api", transport=httpx.MockTransport(handler))
    )
    api.login("client.demo", "heslo")

    assert api.get("/api/v1/listings/mine") == {"ok": True}
    assert requests[1:] == [
        "/api/v1/listings/mine Bearer old",
        "/api/v1/auth/refresh None",
        "/api/v1/listings/mine Bearer new",
    ]


def test_second_401_after_refresh_stops() -> None:
    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/api/v1/auth/login":
            return httpx.Response(200, json={"accessToken": "a", "refreshToken": "r"})
        if request.url.path == "/api/v1/auth/refresh":
            return httpx.Response(200, json={"accessToken": "b", "refreshToken": "r"})
        return httpx.Response(401, json={"title": "Neprihlásený."})

    api = MarketplaceClient(
        "http://api", client=httpx.Client(base_url="http://api", transport=httpx.MockTransport(handler))
    )
    api.login("client.demo", "heslo")

    with pytest.raises(ApiError) as error:
        api.get("/api/v1/listings/mine")

    assert error.value.status == 401
