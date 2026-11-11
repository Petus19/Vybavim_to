"""Jazyk, ktorým agent hovorí s modelom – nezávislý od poskytovateľa (Ollama, Claude, …).

Každý poskytovateľ má vlastný formát správ a volaní nástrojov. Agent pracuje iba s týmito typmi
a poskytovateľ ich prekladá tam a späť – preto sa model dá vymeniť jednou premennou prostredia.
"""

from dataclasses import dataclass, field
from typing import Any, Protocol


@dataclass(frozen=True)
class ToolSpec:
    """Popis nástroja pre model: názov, čo robí a JSON Schema argumentov."""

    name: str
    description: str
    parameters: dict[str, Any]


@dataclass(frozen=True)
class ToolCall:
    id: str
    name: str
    arguments: dict[str, Any]


@dataclass(frozen=True)
class UserMessage:
    text: str


@dataclass(frozen=True)
class AssistantMessage:
    text: str
    tool_calls: tuple[ToolCall, ...] = ()
    # Pôvodná odpoveď poskytovateľa (napr. bloky Claude), ak ju treba poslať späť bez zmeny
    raw: Any = None


@dataclass(frozen=True)
class ToolResultMessage:
    call_id: str
    name: str
    content: str  # JSON s výsledkom – model dostáva text
    is_error: bool = False


type Message = UserMessage | AssistantMessage | ToolResultMessage


@dataclass(frozen=True)
class Usage:
    input_tokens: int = 0  # nové vstupné tokeny (plná cena)
    output_tokens: int = 0
    cache_read_tokens: int = 0  # vstup prečítaný z cache poskytovateľa (zlomok ceny)
    cache_write_tokens: int = 0  # vstup zapísaný do cache

    @property
    def total_input_tokens(self) -> int:
        return self.input_tokens + self.cache_read_tokens + self.cache_write_tokens

    def __add__(self, other: "Usage") -> "Usage":
        return Usage(
            self.input_tokens + other.input_tokens,
            self.output_tokens + other.output_tokens,
            self.cache_read_tokens + other.cache_read_tokens,
            self.cache_write_tokens + other.cache_write_tokens,
        )


@dataclass(frozen=True)
class Completion:
    message: AssistantMessage
    usage: Usage = field(default_factory=Usage)


class LLMProvider(Protocol):
    """Jediné, čo agent od modelu potrebuje: na základe konverzácie a nástrojov vráť ďalší krok."""

    name: str  # napr. "ollama/qwen2.5:7b" – do výpisu a výsledkov vyhodnotenia

    def complete(self, system: str, messages: list[Message], tools: list[ToolSpec]) -> Completion: ...


class LLMError(Exception):
    """Model sa nepodarilo zavolať alebo vrátil nepoužiteľnú odpoveď."""
