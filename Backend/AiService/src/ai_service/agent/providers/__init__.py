"""Poskytovatelia modelov. Výber podľa AGENT_PROVIDER – kód agenta sa pri zmene nemení."""

from ai_service.agent.config import AgentSettings
from ai_service.agent.llm import LLMProvider


def create_provider(settings: AgentSettings) -> LLMProvider:
    if settings.provider == "local":
        from ai_service.agent.providers.ollama import OllamaProvider

        return OllamaProvider(
            settings.ollama_url, settings.ollama_model, settings.llm_timeout, num_ctx=settings.ollama_num_ctx
        )

    from ai_service.agent.providers.anthropic import AnthropicProvider  # SDK sa načíta, iba keď treba

    key = settings.anthropic_api_key
    return AnthropicProvider(
        settings.anthropic_model,
        settings.llm_timeout,
        api_key=key.get_secret_value() if key and key.get_secret_value() else None,
        effort=settings.anthropic_effort,
        fallbacks=settings.anthropic_fallbacks,
    )
