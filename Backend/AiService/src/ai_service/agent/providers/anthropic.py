"""Hostovaný model: Claude cez oficiálny Anthropic SDK (Messages API s volaním nástrojov).

Slučku agenta (loop.py) nepoužívame z SDK, ale vlastnú – je spoločná pre všetkých poskytovateľov,
vďaka čomu sa model prepína jednou premennou. Tento súbor iba prekladá naše správy do formátu
Claude API a späť.
"""

from typing import Any

import anthropic

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

MAX_TOKENS = 16000  # strop jednej odpovede; nestreamujeme, takže držíme rozumnú hodnotu

# Modely, pri ktorých API vie pri odmietnutí odpovede (stop_reason "refusal") samo prejsť na záložný model
FALLBACK_BETA = "server-side-fallback-2026-07-01"
FALLBACK_MODELS = ("claude-opus-5", "claude-sonnet-5-5", "claude-fable-5")


class AnthropicProvider:
    def __init__(
        self,
        model: str,
        timeout: float = 180.0,
        api_key: str | None = None,
        effort: str = "medium",
        fallbacks: bool = True,
        client: Any = None,
    ) -> None:
        self.name = f"anthropic/{model}"
        self._model = model
        self._effort = effort
        # Záložný model pri odmietnutí: dobré pre používateľa, ale vo vyhodnotení ho vypíname,
        # aby výsledok patril naozaj tomu modelu, ktorý meriame.
        self._fallbacks = fallbacks and model.startswith(FALLBACK_MODELS)
        # Bez api_key si SDK nájde prihlásenie samo (premenná ANTHROPIC_API_KEY alebo profil `ant auth login`)
        self._client = client or anthropic.Anthropic(api_key=api_key, timeout=timeout)

    def complete(self, system: str, messages: list[Message], tools: list[ToolSpec]) -> Completion:
        request: dict[str, Any] = {
            "model": self._model,
            "max_tokens": MAX_TOKENS,
            "system": system,
            "messages": _to_claude(messages),
            "tools": [
                {"name": t.name, "description": t.description, "input_schema": t.parameters} for t in tools
            ],
            # Premýšľanie je na týchto modeloch zapnuté vždy (adaptívne); hĺbku riadi effort
            "output_config": {"effort": self._effort},
            # Prompt, nástroje a doterajšia konverzácia sa pri ďalšom kroku čítajú z cache (lacnejšie)
            "cache_control": {"type": "ephemeral"},
        }
        try:
            if self._fallbacks:
                response = self._client.beta.messages.create(
                    **request, betas=[FALLBACK_BETA], fallbacks="default"
                )
            else:
                response = self._client.messages.create(**request)
        except anthropic.AuthenticationError as error:
            raise LLMError("Neplatný alebo expirovaný API kľúč (ANTHROPIC_API_KEY).") from error
        except anthropic.PermissionDeniedError as error:
            raise LLMError("API kľúč nemá oprávnenie na tento model.") from error
        except anthropic.NotFoundError as error:
            raise LLMError(f"Model '{self._model}' neexistuje alebo nie je dostupný.") from error
        except anthropic.RateLimitError as error:
            raise LLMError("Prekročený limit požiadaviek na Claude API, skúste o chvíľu.") from error
        except anthropic.APIStatusError as error:  # napr. 400 – vyčerpaný kredit, 5xx – chyba servera
            raise LLMError(f"Claude API vrátilo chybu {error.status_code}: {error.message}") from error
        except anthropic.APIConnectionError as error:
            raise LLMError("Claude API nie je dostupné (sieť alebo časový limit).") from error

        usage = Usage(
            input_tokens=response.usage.input_tokens,
            output_tokens=response.usage.output_tokens,
            cache_read_tokens=getattr(response.usage, "cache_read_input_tokens", 0) or 0,
            cache_write_tokens=getattr(response.usage, "cache_creation_input_tokens", 0) or 0,
        )

        if response.stop_reason == "refusal":
            # Bezpečnostný klasifikátor odpoveď odmietol – nie je čo vykonať ani poslať späť
            return Completion(AssistantMessage("Model túto požiadavku odmietol spracovať."), usage)
        if response.stop_reason == "max_tokens":
            raise LLMError("Odpoveď modelu bola orezaná (max_tokens).")

        text = "".join(block.text for block in response.content if block.type == "text")
        calls = tuple(
            ToolCall(id=block.id, name=block.name, arguments=dict(block.input))
            for block in response.content
            if block.type == "tool_use"
        )
        # raw = pôvodné bloky odpovede (vrátane premýšľania) – API ich chce v ďalšom kroku dostať nezmenené
        return Completion(AssistantMessage(text, calls, raw=response.content), usage)


def _to_claude(messages: list[Message]) -> list[dict[str, Any]]:
    converted: list[dict[str, Any]] = []
    for message in messages:
        match message:
            case UserMessage(text=text):
                converted.append({"role": "user", "content": text})
            case AssistantMessage(raw=raw) if raw is not None:
                converted.append({"role": "assistant", "content": raw})
            case AssistantMessage(text=text, tool_calls=calls):
                # Správa, ktorú nevyrobil Claude (napr. hlásenie o limite krokov) – poskladáme ju ručne
                blocks: list[dict[str, Any]] = [{"type": "text", "text": text}] if text else []
                blocks += [
                    {"type": "tool_use", "id": c.id, "name": c.name, "input": c.arguments} for c in calls
                ]
                converted.append({"role": "assistant", "content": blocks or [{"type": "text", "text": "…"}]})
            case ToolResultMessage(call_id=call_id, content=content, is_error=is_error):
                block = {
                    "type": "tool_result",
                    "tool_use_id": call_id,
                    "content": content,
                    "is_error": is_error,
                }
                # Výsledky všetkých nástrojov z jedného kroku musia ísť v JEDNEJ správe používateľa
                previous = converted[-1] if converted else None
                if previous and previous["role"] == "user" and isinstance(previous["content"], list):
                    previous["content"].append(block)
                else:
                    converted.append({"role": "user", "content": [block]})
    return converted
