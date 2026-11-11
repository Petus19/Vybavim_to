"""Lokálny model cez Ollama (/api/chat s natívnym volaním nástrojov).

Beží na vlastnom počítači: žiadne poplatky, dáta neopúšťajú stroj. Daň je nižšia presnosť
menších modelov – koľko presne, ukazuje vyhodnotenie agenta.
"""

import json
from typing import Any

import httpx2 as httpx

from ai_service.agent.llm import (
    AssistantMessage,
    Completion,
    LLMError,
    Message,
    ToolCall,
    ToolResultMessage,
    ToolSpec,
    Usage,
    UserMessage,
)


class OllamaProvider:
    def __init__(
        self,
        base_url: str,
        model: str,
        timeout: float = 180.0,
        client: httpx.Client | None = None,
        num_ctx: int = 8192,
    ) -> None:
        self.name = f"ollama/{model}"
        self._model = model
        # Veľkosť kontextu v tokenoch. Ollama má predvolene 4 096 a čo sa nezmestí, POTICHU odreže zo
        # začiatku – teda práve systémový prompt. Prompt s nástrojmi má ~2 500 tokenov, s výsledkami
        # nástrojov sa k limitu ľahko dostane.
        self._num_ctx = num_ctx
        self._client = client or httpx.Client(base_url=base_url, timeout=timeout)

    def complete(self, system: str, messages: list[Message], tools: list[ToolSpec]) -> Completion:
        payload = {
            "model": self._model,
            "messages": [{"role": "system", "content": system}, *(_to_ollama(m) for m in messages)],
            "tools": [
                {
                    "type": "function",
                    "function": {"name": t.name, "description": t.description, "parameters": t.parameters},
                }
                for t in tools
            ],
            "stream": False,
            # Teplota 0 = vždy najpravdepodobnejšia odpoveď → opakovateľnejšie vyhodnotenie
            "options": {"temperature": 0, "num_ctx": self._num_ctx},
        }
        try:
            response = self._client.post("/api/chat", json=payload)
            response.raise_for_status()
        except httpx.HTTPError as error:
            raise LLMError(f"Ollama nie je dostupná alebo zlyhala: {error}") from error

        body = response.json()
        message = body.get("message", {})
        calls = tuple(
            ToolCall(id=f"call_{i}", name=call["function"]["name"], arguments=_arguments(call["function"]))
            for i, call in enumerate(message.get("tool_calls") or [])
        )
        return Completion(
            AssistantMessage(text=message.get("content") or "", tool_calls=calls),
            Usage(body.get("prompt_eval_count", 0), body.get("eval_count", 0)),
        )


def _arguments(function: dict[str, Any]) -> dict[str, Any]:
    """Ollama posiela argumenty ako objekt; staršie verzie a niektoré modely ako text s JSON."""
    arguments = function.get("arguments") or {}
    if isinstance(arguments, str):
        try:
            arguments = json.loads(arguments)
        except json.JSONDecodeError:
            # Neplatný JSON nezhodí agenta: nástroj ho odmietne a model dostane chybu, aby to opravil
            return {"_invalid_json": arguments}
    return arguments if isinstance(arguments, dict) else {"_invalid_json": str(arguments)}


def _to_ollama(message: Message) -> dict[str, Any]:
    match message:
        case UserMessage(text=text):
            return {"role": "user", "content": text}
        case AssistantMessage(text=text, tool_calls=calls):
            converted: dict[str, Any] = {"role": "assistant", "content": text}
            if calls:
                converted["tool_calls"] = [
                    {"function": {"name": c.name, "arguments": c.arguments}} for c in calls
                ]
            return converted
        case ToolResultMessage(name=name, content=content):
            return {"role": "tool", "tool_name": name, "content": content}
