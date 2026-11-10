"""Poskytovatelia modelov. Výber podľa AGENT_PROVIDER – kód agenta sa pri zmene nemení."""

from ai_service.agent.config import AgentSettings
from ai_service.agent.llm import LLMError, LLMProvider


def create_provider(settings: AgentSettings) -> LLMProvider:
    if settings.provider == "local":
        from ai_service.agent.providers.ollama import OllamaProvider

        return OllamaProvider(settings.ollama_url, settings.ollama_model, settings.llm_timeout)

    # Hostovaný model (Claude cez oficiálny SDK) pribudne v ďalšom kroku
    raise LLMError("Poskytovateľ 'api' ešte nie je hotový – zatiaľ použi AGENT_PROVIDER=local.")
