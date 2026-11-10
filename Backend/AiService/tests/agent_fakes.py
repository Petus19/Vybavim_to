"""Falošné služby pre testy agenta: model so scenárom a API v pamäti – bez siete, Ollamy a C# API."""

from collections.abc import Iterator
from typing import Any

from ai_service.agent.llm import AssistantMessage, Completion, LLMError, Message, ToolCall, ToolSpec, Usage
from ai_service.agent.marketplace import ApiError


class ScriptedProvider:
    """Model, ktorý vracia vopred napísané kroky – testujeme slučku agenta, nie inteligenciu modelu."""

    name = "scripted"

    def __init__(self, steps: list[AssistantMessage | Exception]) -> None:
        self._steps: Iterator[AssistantMessage | Exception] = iter(steps)
        self.seen: list[list[Message]] = []  # čo model dostal pri každom volaní

    def complete(self, system: str, messages: list[Message], tools: list[ToolSpec]) -> Completion:
        self.seen.append(list(messages))
        step = next(self._steps, AssistantMessage("koniec scenára"))
        if isinstance(step, Exception):
            raise step
        return Completion(step, Usage(100, 10))


def call(name: str, **arguments: Any) -> AssistantMessage:
    return AssistantMessage("", (ToolCall(f"call_{name}", name, arguments),))


def answer(text: str) -> AssistantMessage:
    return AssistantMessage(text)


def llm_down() -> LLMError:
    return LLMError("spojenie odmietnuté")


class FakeApi:
    """C# API v pamäti: cesta → odpoveď (alebo ApiError). Zaznamenáva volania."""

    def __init__(self, routes: dict[str, Any]) -> None:
        self.routes = routes
        self.calls: list[tuple[str, dict[str, Any]]] = []

    def get(self, path: str, **params: Any) -> Any:
        self.calls.append((path, params))
        if path not in self.routes:
            raise ApiError(404, "listing.not_found", "Inzerát neexistuje.")
        value = self.routes[path]
        if isinstance(value, ApiError):
            raise value
        return value


class FakeSearch:
    def __init__(self, hits: list[str]) -> None:
        self.hits = hits

    def search(self, query: str, limit: int) -> list[dict[str, Any]]:
        return [{"listing_id": listing_id} for listing_id in self.hits[:limit]]
