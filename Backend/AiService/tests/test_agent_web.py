"""Agent vo webe: SSE udalosti v správnom poradí, potvrdenie / zamietnutie / vypršanie zápisu,
cudzí používateľ nič nepotvrdí a konverzácia patrí iba svojmu autorovi.

Model je scenár (ScriptedProvider) a C# API je v pamäti – testuje sa web vrstva, nie model.
TestClient vráti SSE odpoveď až po skončení behu, preto potvrdenie posiela druhé vlákno
(rovnako ako prehliadač: stream je otvorený a medzitým príde POST /agent/confirm).
"""

import asyncio
import json
import threading
import time
from collections.abc import Callable
from typing import Any

import httpx2 as httpx
import pytest
from fastapi import FastAPI
from fastapi.testclient import TestClient

from ai_service.agent.describe import describe_call
from ai_service.agent.llm import AssistantMessage, ToolCall, ToolSpec
from ai_service.agent.loop import Agent
from ai_service.agent.marketplace import ApiError
from ai_service.agent.prompts import Role
from ai_service.agent.tools import SearchListingsInput, SendOfferInput, Tool, ToolRegistry
from ai_service.agent.web import AgentHub, AgentSession, WebUser, agent_router
from ai_service.main import create_app
from tests.agent_fakes import FakeApi, ScriptedProvider, answer, call

CLIENT = WebUser("user-client", "Autoservis Kováč")
OTHER = WebUser("user-other", "Lucia Hrašková")
TOKENS = {"token-client": CLIENT, "token-other": OTHER}
OFFERS_PATH = "/api/v1/listings/L1/offers"

OFFER = {
    "listing": "Logo pre kaviareň",
    "price": 250,
    "delivery_days": 7,
    "message": "Navrhnem 3 varianty loga.",
}


def identify(token: str) -> WebUser:
    if token not in TOKENS:
        raise ApiError(401, None, "Unauthorized")
    return TOKENS[token]


class Stack:
    """Hub s falošným modelom a API + HTTP klient, ako by ho volal Next.js server."""

    def __init__(self, script: list[AssistantMessage | Exception], confirm_timeout: float = 5.0) -> None:
        self.api = FakeApi(routes={}, posts={OFFERS_PATH: {"id": "offer-1"}})
        self.provider = ScriptedProvider(script)
        self.tokens: list[str] = []  # tokeny, s ktorými agent volal API
        self.hub = AgentHub(identify, self._create_session, confirm_timeout=confirm_timeout)
        app = FastAPI()
        app.state.agent_hub = self.hub
        app.include_router(agent_router())
        self.client = TestClient(app)

    def _create_session(
        self, token: str, user: WebUser, role: Role, on_event: Any, confirm: Any
    ) -> AgentSession:
        self.tokens.append(token)
        agent = Agent(self.provider, _tools(self.api), f"system pre {role}", 8, on_event, confirm)
        return AgentSession(agent, self.tokens.append)

    def chat(self, message: str, user: str = "client", **body: Any) -> httpx.Response:
        return self.client.post("/agent/chat", json={"message": message, **body}, headers=_auth(user))

    def confirm(self, confirmation_id: str, approved: bool, user: str = "client") -> httpx.Response:
        return self.client.post(
            "/agent/confirm",
            json={"confirmation_id": confirmation_id, "approved": approved},
            headers=_auth(user),
        )

    def wait_for_confirmation(self) -> str:
        deadline = time.monotonic() + 5
        while not self.hub._pending:
            assert time.monotonic() < deadline, "agent sa nepýtal na potvrdenie"
            time.sleep(0.01)
        return next(iter(self.hub._pending))

    def in_background(self, action: Callable[[], Any]) -> tuple[threading.Thread, list[Any]]:
        results: list[Any] = []
        thread = threading.Thread(target=lambda: results.append(action()))
        thread.start()
        return thread, results


def _auth(user: str) -> dict[str, str]:
    """Token, ktorý by poslal Next.js server; „zly“ = neplatný (C# API ho odmietne)."""
    return {"Authorization": f"Bearer token-{user}"}


def _tools(api: FakeApi) -> ToolRegistry:
    def search(_: SearchListingsInput) -> dict[str, Any]:
        return {"results": [{"id": "L1", "title": "Logo pre kaviareň"}]}

    def send_offer(args: SendOfferInput) -> dict[str, Any]:
        api.post(OFFERS_PATH, {"price": args.price})
        return {"status": "sent", "offer_id": "offer-1", "url": "http://localhost:3000/inzeraty/L1"}

    return ToolRegistry(
        [
            Tool(ToolSpec("search_listings", "", {}), SearchListingsInput, search),
            Tool(
                ToolSpec("send_offer", "", {}),
                SendOfferInput,
                send_offer,
                preview=lambda a: f"Poslať ponuku na „{a.listing}“:\n  Cena: {a.price:g} €",
            ),
        ]
    )


def events(response: httpx.Response) -> list[tuple[str, dict[str, Any]]]:
    parsed = []
    for block in response.text.split("\n\n"):
        lines = [line for line in block.splitlines() if line and not line.startswith(":")]
        if lines:
            kind = lines[0].removeprefix("event: ")
            parsed.append((kind, json.loads(lines[1].removeprefix("data: "))))
    return parsed


def kinds(response: httpx.Response) -> list[str]:
    return [kind for kind, _ in events(response)]


def first(response: httpx.Response, kind: str) -> dict[str, Any]:
    return next(data for k, data in events(response) if k == kind)


# --- Priebeh behu ---


def test_steps_are_streamed_in_order() -> None:
    stack = Stack([call("search_listings", query="logo", max_budget=300), answer("Našiel som 1 inzerát.")])

    response = stack.chat("Nájdi zákazky na logá do 300 €")

    assert response.status_code == 200
    assert response.headers["content-type"].startswith("text/event-stream")
    assert kinds(response) == ["start", "thinking", "tool_call", "tool_result", "thinking", "message"]
    assert first(response, "tool_call") == {
        "tool": "search_listings",
        "summary": "Hľadám inzeráty: logo, do 300 €",
    }
    assert first(response, "tool_result")["summary"] == "1 – Logo pre kaviareň"
    message = first(response, "message")
    assert message["text"] == "Našiel som 1 inzerát."
    assert message["stop_reason"] == "answered"
    assert message["stats"]["llm_calls"] == 2
    assert message["stats"]["provider"] == "scripted"
    assert stack.tokens == ["token-client"]  # agent volá API s tokenom používateľa


def test_approved_write_is_executed_and_links_to_result() -> None:
    stack = Stack([call("send_offer", **OFFER), answer("Ponuka je odoslaná.")])
    thread, results = stack.in_background(lambda: stack.chat("Pošli ponuku na logo za 250 €"))

    confirmation_id = stack.wait_for_confirmation()
    assert stack.api.posted == []  # kým človek nepotvrdí, nič sa nezapísalo
    assert stack.confirm(confirmation_id, approved=True).status_code == 204
    thread.join(5)

    response = results[0]
    assert kinds(response) == [
        "start",
        "thinking",
        "tool_call",
        "confirmation_required",
        "confirmation_result",
        "tool_result",
        "thinking",
        "message",
    ]
    required = first(response, "confirmation_required")
    assert required["confirmation_id"] == confirmation_id
    assert required["title"] == "Poslať ponuku"
    assert "Cena: 250 €" in required["preview"]
    assert first(response, "confirmation_result") == {"confirmation_id": confirmation_id, "approved": True}
    assert first(response, "tool_result")["url"] == "http://localhost:3000/inzeraty/L1"
    assert stack.api.posted == [(OFFERS_PATH, {"price": 250})]


def test_declined_write_does_nothing_and_model_is_told() -> None:
    stack = Stack([call("send_offer", **OFFER), answer("Dobre, ponuku neposielam.")])
    thread, results = stack.in_background(lambda: stack.chat("Pošli ponuku na logo"))

    stack.confirm(stack.wait_for_confirmation(), approved=False)
    thread.join(5)

    response = results[0]
    assert first(response, "confirmation_result")["approved"] is False
    result = first(response, "tool_result")
    assert result["ok"] is False
    assert result["summary"] == "nepotvrdené – nič sa nevykonalo"
    assert stack.api.posted == []
    last_seen = stack.provider.seen[-1][-1]  # výsledok nástroja, ktorý dostal model
    assert '"declined"' in last_seen.content


def test_confirmation_expires_and_cannot_be_used_later() -> None:
    stack = Stack([call("send_offer", **OFFER), answer("Ponuku som neposlal.")], confirm_timeout=0.2)

    response = stack.chat("Pošli ponuku na logo")

    assert "confirmation_expired" in kinds(response)
    assert "confirmation_result" not in kinds(response)
    assert stack.api.posted == []
    expired_id = first(response, "confirmation_required")["confirmation_id"]
    late = stack.confirm(expired_id, approved=True)
    assert late.status_code == 404
    assert late.json()["detail"]["code"] == "agent.confirmation_not_found"
    assert stack.api.posted == []


def test_another_user_cannot_confirm() -> None:
    stack = Stack([call("send_offer", **OFFER), answer("Ponuku som neposlal.")])
    thread, _ = stack.in_background(lambda: stack.chat("Pošli ponuku na logo"))
    confirmation_id = stack.wait_for_confirmation()

    foreign = stack.confirm(confirmation_id, approved=True, user="other")

    assert foreign.status_code == 404  # cudzie potvrdenie vyzerá ako neexistujúce
    assert stack.api.posted == []
    assert stack.confirm(confirmation_id, approved=False).status_code == 204  # autor ho stále môže vybaviť
    thread.join(5)
    assert stack.api.posted == []


def test_browser_disconnect_declines_pending_confirmation() -> None:
    stack = Stack([call("send_offer", **OFFER), answer("Ponuku som neposlal.")])
    loop = asyncio.new_event_loop()
    try:
        run = stack.hub.start(CLIENT, "token-client", "Pošli ponuku", None, "client", loop)
        stack.wait_for_confirmation()

        stack.hub.cancel(run.conversation)  # to isté urobí SSE odpoveď, keď sa prehliadač odpojí
        run.worker.join(5)
    finally:
        loop.close()

    assert not run.worker.is_alive()
    assert stack.api.posted == []
    assert stack.hub._pending == {}


def test_unexpected_failure_ends_with_error_event() -> None:
    stack = Stack([RuntimeError("niečo sa pokazilo"), answer("Už to ide.")])

    response = stack.chat("Ahoj")
    again = stack.chat("Skúsme znova", conversation_id=first(response, "start")["conversation_id"])

    assert kinds(response)[-1] == "error"
    assert "niečo sa pokazilo" not in response.text  # detaily chyby ostávajú v logu servera
    assert kinds(again)[-1] == "message"  # konverzácia nezostala zaseknutá


# --- Konverzácia a prihlásenie ---


def test_conversation_continues_with_history() -> None:
    stack = Stack([answer("Na aký rozpočet?"), answer("Rozumiem, do 200 €.")])

    first_response = stack.chat("Potrebujem logo")
    conversation_id = first(first_response, "start")["conversation_id"]
    second = stack.chat("Do 200 €", conversation_id=conversation_id)

    assert first(second, "start")["conversation_id"] == conversation_id
    assert [m.text for m in stack.provider.seen[-1] if hasattr(m, "text")][:2] == [
        "Potrebujem logo",
        "Na aký rozpočet?",
    ]
    assert stack.tokens == ["token-client", "token-client"]  # druhá správa dala agentovi čerstvý token


def test_conversation_belongs_to_its_user() -> None:
    stack = Stack([answer("Ahoj")])
    conversation_id = first(stack.chat("Ahoj"), "start")["conversation_id"]

    assert stack.chat("Čo ste písali?", user="other", conversation_id=conversation_id).status_code == 404
    assert stack.chat("Ahoj", conversation_id="neexistuje").status_code == 404


def test_second_message_while_agent_works_is_refused() -> None:
    stack = Stack([call("send_offer", **OFFER), answer("Ponuku som neposlal.")])
    thread, _ = stack.in_background(lambda: stack.chat("Pošli ponuku na logo"))
    confirmation_id = stack.wait_for_confirmation()
    conversation_id = next(iter(stack.hub._conversations))

    busy = stack.chat("A ešte niečo", conversation_id=conversation_id)

    assert busy.status_code == 409
    assert busy.json()["detail"]["code"] == "agent.busy"
    stack.confirm(confirmation_id, approved=False)
    thread.join(5)


@pytest.mark.parametrize("header", [None, "token-client", "Bearer ", "Bearer zly-token"])
def test_chat_requires_valid_token(header: str | None) -> None:
    stack = Stack([answer("Ahoj")])
    headers = {"Authorization": header} if header else {}

    response = stack.client.post("/agent/chat", json={"message": "Ahoj"}, headers=headers)

    assert response.status_code == 401
    assert response.json()["detail"]["code"] == "agent.unauthorized"
    assert stack.provider.seen == []  # model sa ani nezavolal


def test_confirm_requires_valid_token() -> None:
    stack = Stack([answer("Ahoj")])

    response = stack.confirm("hocico", approved=True, user="zly")

    assert response.status_code == 401


def test_app_exposes_agent_endpoints() -> None:
    paths = create_app().openapi()["paths"]

    assert "/agent/chat" in paths
    assert "/agent/confirm" in paths


# --- Popisy krokov pre človeka ---


@pytest.mark.parametrize(
    ("name", "arguments", "expected"),
    [
        (
            "search_listings",
            {"query": "logo", "max_budget": 300, "city": "Košice"},
            "Hľadám inzeráty: logo, do 300 €, Košice",
        ),
        ("search_listings", {"category": "grafika"}, "Hľadám inzeráty: grafika"),
        ("search_listings", {}, "Hľadám otvorené inzeráty"),
        ("get_offers_on_listing", {"listing": "logo"}, "Pozerám ponuky na inzerát „logo“"),
        ("get_offers_on_listing", {}, "Pozerám ponuky na tvoje inzeráty"),
        ("send_offer", {"listing": "Etikety", "price": 180}, "Pripravujem ponuku na „Etikety“ za 180 €"),
        ("send_offer", {"listing": "Etikety", "price": "veľa"}, "Pripravujem ponuku na „Etikety“"),
        ("accept_offer", {"offer": "Lucia"}, "Pripravujem prijatie ponuky (Lucia)"),
        ("neznamy_nastroj", {}, "neznamy_nastroj"),
    ],
)
def test_describe_call(name: str, arguments: dict[str, Any], expected: str) -> None:
    assert describe_call(ToolCall("c1", name, arguments)) == expected
