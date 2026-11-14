"""Slučka agenta: model → volania nástrojov → výsledky späť modelu → … → odpoveď používateľovi.

Je to celé „agentovanie“: model sám rozhoduje, ktorý nástroj zavolá a kedy má dosť informácií.
Kód drží limity, ktoré model obísť nesmie (najviac max_steps volaní modelu na jednu požiadavku).
"""

import time
from collections.abc import Callable
from dataclasses import dataclass, field
from typing import Any, Literal

from ai_service.agent.llm import (
    AssistantMessage,
    LLMError,
    LLMProvider,
    Message,
    ToolCall,
    ToolResultMessage,
    Usage,
    UserMessage,
)
from ai_service.agent.tools import Confirm, ToolOutcome, ToolRegistry

STEP_LIMIT_MESSAGE = (
    "Dosiahol som limit krokov pre jednu požiadavku a zastavil som sa, aby som nerobil nič neočakávané. "
    "Skúste požiadavku rozdeliť na menšie časti."
)


@dataclass(frozen=True)
class ToolStep:
    call: ToolCall
    outcome: ToolOutcome
    seconds: float


@dataclass
class AgentReply:
    text: str
    stop_reason: Literal["answered", "step_limit", "llm_error"]
    tool_steps: list[ToolStep] = field(default_factory=list)
    llm_calls: int = 0
    usage: Usage = field(default_factory=Usage)
    seconds: float = 0.0


# Udalosti pre výpis v termináli a webový chat:
# ("thinking", číslo kroku) / ("tool_call", ToolCall) / ("tool_result", ToolStep)
type EventHandler = Callable[[str, Any], None]


class Agent:
    def __init__(
        self,
        provider: LLMProvider,
        tools: ToolRegistry,
        system: str,
        max_steps: int = 8,
        on_event: EventHandler | None = None,
        confirm: Confirm | None = None,
    ) -> None:
        self.provider = provider
        self.tools = tools
        self.system = system
        self.max_steps = max_steps
        self.history: list[Message] = []  # celá konverzácia – agent si pamätá predchádzajúce otázky
        self._on_event = on_event or (lambda _kind, _payload: None)
        # Bez potvrdzovacej funkcie sa žiadny zápis nevykoná – bezpečné predvolené správanie
        self._confirm = confirm

    def ask(self, text: str) -> AgentReply:
        started = time.perf_counter()
        reply = AgentReply(text="", stop_reason="answered")
        self.history.append(UserMessage(text))

        for step in range(1, self.max_steps + 1):
            self._on_event("thinking", step)  # web ukáže „Premýšľam…“, kým model odpovedá
            try:
                completion = self.provider.complete(self.system, self.history, self.tools.specs)
            except LLMError as error:
                reply.text, reply.stop_reason = f"Model teraz neodpovedá ({error}).", "llm_error"
                break
            reply.llm_calls += 1
            reply.usage = reply.usage + completion.usage
            message = completion.message
            self.history.append(message)

            if not message.tool_calls:  # model už nič nevolá → toto je odpoveď používateľovi
                reply.text = message.text.strip()
                break

            for call in message.tool_calls:
                self._on_event("tool_call", call)
                step_started = time.perf_counter()
                outcome = self.tools.execute(call, self._confirm)
                step = ToolStep(call, outcome, time.perf_counter() - step_started)
                reply.tool_steps.append(step)
                self._on_event("tool_result", step)
                self.history.append(ToolResultMessage(call.id, call.name, outcome.content, outcome.is_error))
        else:
            # Limit vypršal počas volania nástrojov – ukončíme konverzáciu jasnou správou
            reply.text, reply.stop_reason = STEP_LIMIT_MESSAGE, "step_limit"
            self.history.append(AssistantMessage(STEP_LIMIT_MESSAGE))

        reply.seconds = time.perf_counter() - started
        return reply
