"""Agent vo webe: POST /agent/chat (kroky naživo cez Server-Sent Events) a POST /agent/confirm.

Ako to funguje:
- Slučka agenta je TÁ ISTÁ ako v CLI a beží vo vlastnom vlákne. Každý krok (thinking, tool_call,
  tool_result, …) vloží do fronty a odtiaľ ide hneď do prehliadača ako jedna SSE udalosť.
- Keď chce agent zapisovať, vlákno sa zastaví na potvrdení – presne ako CLI na otázke y/N. Server pošle
  `confirmation_required` s náhľadom a ID a čaká, kým príde POST /agent/confirm s týmto ID.
- Potvrdiť môže iba ten istý používateľ (jeho token overí C# API) a iba do confirm_timeout; potom sa
  akcia berie ako nepotvrdená. Keď prehliadač zavrie spojenie, čakajúce potvrdenie sa zamietne.
- Token používateľa sa iba preposiela do C# API (rovnaké práva ako na webe), nikam sa neukladá.
  Konverzácie sú v pamäti procesu – na demo a jeden server stačí; viac serverov by potrebovalo Redis.
"""

import asyncio
import json
import logging
import threading
import time
import uuid
from collections.abc import AsyncIterator, Callable
from contextlib import suppress
from dataclasses import dataclass, field
from datetime import date
from typing import Annotated, Any

from fastapi import APIRouter, Depends, Header, HTTPException, Request, Response
from fastapi.responses import StreamingResponse
from pydantic import BaseModel, Field

from ai_service.agent.config import AgentSettings
from ai_service.agent.describe import ACTION_TITLES, describe_call, summarize_result
from ai_service.agent.llm import LLMProvider, ToolCall
from ai_service.agent.loop import Agent, AgentReply, EventHandler, ToolStep
from ai_service.agent.marketplace import ApiError, MarketplaceClient, SearchClient
from ai_service.agent.pricing import estimate_cost
from ai_service.agent.prompts import Role, system_prompt
from ai_service.agent.providers import create_provider
from ai_service.agent.tools import Confirm, ToolRegistry, build_read_tools, build_write_tools

logger = logging.getLogger("ai_service.agent")

KEEPALIVE_SECONDS = 15.0  # prázdna SSE správa počas čakania, aby proxy spojenie nezavrela


@dataclass(frozen=True)
class WebUser:
    id: str
    name: str


@dataclass
class AgentSession:
    """Agent jednej konverzácie. `use_token` mu pri ďalšej správe dá čerstvý token používateľa."""

    agent: Agent
    use_token: Callable[[str], None]
    close: Callable[[], None] = lambda: None


# token → používateľ (ApiError 401 pri neplatnom tokene)
type Identify = Callable[[str], WebUser]
# (token, používateľ, rola, udalosti slučky, potvrdenie) → agent; testy podstrčia falošný model a API
type SessionFactory = Callable[[str, WebUser, Role, EventHandler, Confirm], AgentSession]
type Sink = Callable[[tuple[str, dict[str, Any]] | None], None]  # None = koniec behu


class ConversationNotFoundError(Exception):
    pass


class ConversationBusyError(Exception):
    pass


class ConfirmationNotFoundError(Exception):
    pass


class Conversation:
    """Jedna konverzácia s agentom – patrí jednému používateľovi a pamätá si históriu ako CLI."""

    def __init__(self, user: WebUser, role: Role) -> None:
        self.id = str(uuid.uuid4())
        self.user = user
        self.role = role  # platí pre celú konverzáciu (je v systémovom prompte)
        self.session: AgentSession | None = None
        self.running = False
        self.last_used = time.monotonic()
        self.cancelled = threading.Event()  # prehliadač sa odpojil – nič ďalšie nezapisovať
        self._sink: Sink = lambda _item: None

    def attach(self, sink: Sink) -> None:
        self._sink = sink
        self.cancelled.clear()

    def emit(self, kind: str, data: dict[str, Any]) -> None:
        if not self.cancelled.is_set():
            self._sink((kind, data))

    def end(self) -> None:
        self._sink(None)

    def on_agent_event(self, kind: str, payload: Any) -> None:
        """Udalosti slučky agenta → krátke slovenské popisy pre prehliadač."""
        if kind == "thinking":
            self.emit("thinking", {"step": payload})
        elif kind == "tool_call":
            call: ToolCall = payload
            self.emit("tool_call", {"tool": call.name, "summary": describe_call(call)})
        elif kind == "tool_result":
            step: ToolStep = payload
            data = step.outcome.data
            event: dict[str, Any] = {
                "tool": step.call.name,
                "ok": not step.outcome.is_error and step.outcome.confirmed is not False,
                "summary": summarize_result(data),
            }
            if isinstance(data, dict) and isinstance(data.get("url"), str):
                event["url"] = data["url"]  # odkaz na vytvorený inzerát / ponuku v bežnom webe
            self.emit("tool_result", event)


@dataclass
class PendingConfirmation:
    id: str
    conversation: Conversation
    approved: bool = False
    decided: threading.Event = field(default_factory=threading.Event)


@dataclass
class Run:
    """Jeden beh agenta (jedna správa používateľa) – fronta udalostí, ktorú číta SSE odpoveď."""

    hub: "AgentHub"
    conversation: Conversation
    queue: asyncio.Queue[tuple[str, dict[str, Any]] | None]
    worker: threading.Thread

    async def events(self) -> AsyncIterator[str]:
        try:
            while True:
                try:
                    item = await asyncio.wait_for(self.queue.get(), KEEPALIVE_SECONDS)
                except TimeoutError:
                    yield ": ping\n\n"  # komentár – prehliadač ho ignoruje
                    continue
                if item is None:
                    break
                yield _sse(*item)
        finally:
            if self.worker.is_alive():  # prehliadač odišiel uprostred behu
                self.hub.cancel(self.conversation)


def _sse(kind: str, data: dict[str, Any]) -> str:
    return f"event: {kind}\ndata: {json.dumps(data, ensure_ascii=False)}\n\n"


class AgentHub:
    """Konverzácie a čakajúce potvrdenia. Zámok chráni slovníky – pristupujú k nim vlákna agentov
    aj požiadavky /agent/confirm naraz."""

    def __init__(
        self,
        identify: Identify,
        create_session: SessionFactory,
        confirm_timeout: float = 300.0,
        conversation_ttl: float = 1800.0,
    ) -> None:
        self.identify = identify
        self._create_session = create_session
        self.confirm_timeout = confirm_timeout
        self.conversation_ttl = conversation_ttl
        self._lock = threading.Lock()
        self._conversations: dict[str, Conversation] = {}
        self._pending: dict[str, PendingConfirmation] = {}

    # --- Beh agenta ---

    def start(
        self,
        user: WebUser,
        token: str,
        text: str,
        conversation_id: str | None,
        role: Role,
        loop: asyncio.AbstractEventLoop,
    ) -> Run:
        """Nájde (alebo založí) konverzáciu a spustí agenta vo vlákne. Udalosti idú do fronty `Run`."""
        with self._lock:
            self._forget_idle()
            if conversation_id is not None:
                conversation = self._conversations.get(conversation_id)
                # Cudzia konverzácia vyzerá rovnako ako neexistujúca – neprezradí sa, že existuje
                if conversation is None or conversation.user.id != user.id:
                    raise ConversationNotFoundError
                if conversation.running:
                    raise ConversationBusyError
            else:
                conversation = Conversation(user, role)
                self._conversations[conversation.id] = conversation
            conversation.running = True
            conversation.last_used = time.monotonic()

        try:
            if conversation.session is None:
                conversation.session = self._create_session(
                    token,
                    user,
                    conversation.role,
                    conversation.on_agent_event,
                    lambda tool, preview: self._wait_for_decision(conversation, tool, preview),
                )
            else:
                conversation.session.use_token(token)
        except Exception:
            with self._lock:
                conversation.running = False
            raise

        queue: asyncio.Queue[tuple[str, dict[str, Any]] | None] = asyncio.Queue()

        def sink(item: tuple[str, dict[str, Any]] | None) -> None:
            with suppress(RuntimeError):  # slučka servera už skončila – udalosť nemá kto čítať
                loop.call_soon_threadsafe(queue.put_nowait, item)

        conversation.attach(sink)
        session = conversation.session
        conversation.emit(
            "start", {"conversation_id": conversation.id, "provider": session.agent.provider.name}
        )
        worker = threading.Thread(
            target=self._work, args=(conversation, session.agent, text), daemon=True, name="agent-run"
        )
        worker.start()
        return Run(self, conversation, queue, worker)

    def _work(self, conversation: Conversation, agent: Agent, text: str) -> None:
        try:
            reply = agent.ask(text)
            conversation.emit("message", _message(reply, agent.provider))
        except Exception:  # vlákno nesmie spadnúť potichu – prehliadač musí dostať koniec behu
            logger.exception("Beh agenta zlyhal")
            conversation.emit(
                "error", {"message": "Agent narazil na neočakávanú chybu. Skús to, prosím, znova."}
            )
        finally:
            with self._lock:
                conversation.running = False
                conversation.last_used = time.monotonic()
            conversation.end()

    # --- Potvrdenie zápisu ---

    def _wait_for_decision(self, conversation: Conversation, tool: str, preview: str) -> bool:
        """Volá sa vo vlákne agenta namiesto CLI otázky y/N: pošle náhľad a čaká na rozhodnutie."""
        if conversation.cancelled.is_set():
            return False
        pending = PendingConfirmation(str(uuid.uuid4()), conversation)
        with self._lock:
            self._pending[pending.id] = pending
        conversation.emit(
            "confirmation_required",
            {
                "confirmation_id": pending.id,
                "tool": tool,
                "title": ACTION_TITLES.get(tool, tool),
                "preview": preview,
                "expires_in": round(self.confirm_timeout),
            },
        )
        pending.decided.wait(self.confirm_timeout)
        with self._lock:
            # Ak je potvrdenie ešte v slovníku, nikto nerozhodol včas. Rozhodnutie ho odtiaľ vyberie
            # pod tým istým zámkom, takže súbeh „klik presne pri vypršaní“ dopadne vždy jednoznačne.
            expired = self._pending.pop(pending.id, None) is not None
        if expired:
            conversation.emit("confirmation_expired", {"confirmation_id": pending.id})
            return False
        result = {"confirmation_id": pending.id, "approved": pending.approved}
        conversation.emit("confirmation_result", result)
        return pending.approved

    def decide(self, user: WebUser, confirmation_id: str, approved: bool) -> None:
        """POST /agent/confirm. Neznáme, vypršané, už rozhodnuté aj cudzie potvrdenie = NotFound."""
        with self._lock:
            pending = self._pending.get(confirmation_id)
            if pending is None or pending.conversation.user.id != user.id:
                raise ConfirmationNotFoundError
            del self._pending[confirmation_id]
            pending.approved = approved
            pending.decided.set()

    def cancel(self, conversation: Conversation) -> None:
        """Prehliadač sa odpojil: čakajúce potvrdenia tejto konverzácie sa zamietnu."""
        conversation.cancelled.set()
        with self._lock:
            mine = [p for p in self._pending.values() if p.conversation is conversation]
            for pending in mine:
                del self._pending[pending.id]
                pending.approved = False
                pending.decided.set()

    # --- Upratovanie ---

    def _forget_idle(self) -> None:
        now = time.monotonic()
        idle = [
            c
            for c in self._conversations.values()
            if not c.running and now - c.last_used > self.conversation_ttl
        ]
        for conversation in idle:
            del self._conversations[conversation.id]
            if conversation.session is not None:
                conversation.session.close()

    def close(self) -> None:
        with self._lock:
            conversations = list(self._conversations.values())
            self._conversations.clear()
        for conversation in conversations:
            self.cancel(conversation)
            if conversation.session is not None:
                conversation.session.close()


def _message(reply: AgentReply, provider: LLMProvider) -> dict[str, Any]:
    cost = estimate_cost(provider.name, reply.usage)
    return {
        "text": reply.text,
        "stop_reason": reply.stop_reason,  # answered | step_limit | llm_error
        "stats": {
            "provider": provider.name,
            "llm_calls": reply.llm_calls,
            "tool_calls": len(reply.tool_steps),
            "input_tokens": reply.usage.total_input_tokens,
            "output_tokens": reply.usage.output_tokens,
            "cost_usd": round(cost, 5) if cost else None,
            "seconds": round(reply.seconds, 1),
        },
    }


# --- Skutočné služby: C# API, hybridné hľadanie a model podľa AGENT_PROVIDER ---


def build_agent_hub(settings: AgentSettings | None = None) -> AgentHub:
    settings = settings or AgentSettings()
    provider_lock = threading.Lock()
    providers: list[LLMProvider] = []

    def provider() -> LLMProvider:
        # Model sa vytvorí až pri prvej správe – služba štartuje aj bez Ollamy či API kľúča
        with provider_lock:
            if not providers:
                providers.append(create_provider(settings))
            return providers[0]

    def identify(token: str) -> WebUser:
        api = MarketplaceClient(settings.api_base_url, settings.request_timeout)
        api.use_token(token)
        try:
            me = api.get("/api/v1/auth/me")
            try:
                name = api.get("/api/v1/profiles/me")["displayName"]
            except ApiError:
                name = me.get("userName") or "používateľ"
            return WebUser(str(me["id"]), name)
        finally:
            api.close()

    def create_session(
        token: str, user: WebUser, role: Role, on_event: EventHandler, confirm: Confirm
    ) -> AgentSession:
        api = MarketplaceClient(settings.api_base_url, settings.request_timeout)
        api.use_token(token)
        search = SearchClient(settings.ai_service_url, settings.request_timeout)
        tools = ToolRegistry(
            [*build_read_tools(api, search), *build_write_tools(api, search, settings.web_base_url)]
        )
        agent = Agent(
            provider(),
            tools,
            system_prompt(role, user.name, date.today()),
            settings.max_steps,
            on_event,
            confirm,
        )

        def close() -> None:
            api.close()
            search.close()

        return AgentSession(agent, api.use_token, close)

    return AgentHub(identify, create_session, settings.confirm_timeout, settings.conversation_ttl)


# --- HTTP endpointy ---


class ChatRequest(BaseModel):
    message: str = Field(min_length=1, max_length=2000, description="Požiadavka používateľa po slovensky.")
    conversation_id: str | None = Field(
        None, description="Pokračovanie konverzácie (z udalosti `start`); vynechaj pre novú."
    )
    role: Role = Field(
        "client", description="Pri novej konverzácii: zadávateľ (client) alebo špecialista (specialist)."
    )


class ConfirmRequest(BaseModel):
    confirmation_id: str
    approved: bool


def _problem(status: int, code: str, message: str) -> HTTPException:
    return HTTPException(status_code=status, detail={"code": code, "message": message})


def get_hub(request: Request) -> AgentHub:
    return request.app.state.agent_hub


HubDep = Annotated[AgentHub, Depends(get_hub)]
AuthorizationHeader = Annotated[str | None, Header(description="Bearer token prihláseného používateľa")]


def _bearer(authorization: str | None) -> str:
    scheme, _, token = (authorization or "").partition(" ")
    if scheme.lower() != "bearer" or not token.strip():
        raise _problem(401, "agent.unauthorized", "Chýba prihlásenie (Authorization: Bearer …).")
    return token.strip()


def _identify(hub: AgentHub, token: str) -> WebUser:
    try:
        return hub.identify(token)
    except ApiError as error:
        if error.status == 401:
            raise _problem(401, "agent.unauthorized", "Prihlásenie vypršalo alebo je neplatné.") from error
        raise _problem(503, "agent.api_unavailable", "Hlavné API teraz neodpovedá.") from error


def agent_router() -> APIRouter:
    router = APIRouter(prefix="/agent", tags=["Agent"])

    @router.post(
        "/chat",
        summary="Send a message to the agent; its steps are streamed as Server-Sent Events",
        response_class=StreamingResponse,
        responses={
            200: {
                "content": {"text/event-stream": {}},
                "description": "Events: start, thinking, tool_call, tool_result, confirmation_required, "
                "confirmation_result, confirmation_expired, message, error",
            },
            401: {"description": "Missing or invalid token"},
            404: {"description": "Unknown conversation (or another user's)"},
            409: {"description": "The agent is still working on the previous message"},
        },
    )
    async def chat(body: ChatRequest, hub: HubDep, authorization: AuthorizationHeader = None) -> Response:
        token = _bearer(authorization)
        user = await asyncio.to_thread(_identify, hub, token)
        loop = asyncio.get_running_loop()
        try:
            run = await asyncio.to_thread(
                hub.start, user, token, body.message.strip(), body.conversation_id, body.role, loop
            )
        except ConversationNotFoundError as error:
            raise _problem(404, "agent.conversation_not_found", "Konverzácia neexistuje.") from error
        except ConversationBusyError as error:
            raise _problem(409, "agent.busy", "Agent ešte pracuje na predchádzajúcej správe.") from error
        return StreamingResponse(
            run.events(),
            media_type="text/event-stream",
            headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
        )

    @router.post(
        "/confirm",
        status_code=204,
        summary="Approve or decline the write action the agent is waiting for",
        responses={
            401: {"description": "Missing or invalid token"},
            404: {"description": "Unknown, expired, already decided, or another user's confirmation"},
        },
    )
    def confirm(body: ConfirmRequest, hub: HubDep, authorization: AuthorizationHeader = None) -> Response:
        user = _identify(hub, _bearer(authorization))
        try:
            hub.decide(user, body.confirmation_id, body.approved)
        except ConfirmationNotFoundError as error:
            raise _problem(
                404, "agent.confirmation_not_found", "Potvrdenie už neplatí (vypršalo alebo je vybavené)."
            ) from error
        return Response(status_code=204)

    return router
