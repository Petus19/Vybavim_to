"""Nastavenia agenta z premenných prostredia s predponou AGENT_ (alebo zo súboru .env).

Poskytovateľa modelu prepína JEDNA premenná: AGENT_PROVIDER=local | api.
API kľúč (ANTHROPIC_API_KEY) číta priamo SDK z prostredia – v kóde ani v repozitári nie je.
"""

from typing import Literal

from pydantic import Field, SecretStr
from pydantic_settings import BaseSettings, SettingsConfigDict


class AgentSettings(BaseSettings):
    model_config = SettingsConfigDict(env_prefix="AGENT_", env_file=".env", extra="ignore")

    provider: Literal["local", "api"] = "local"

    # Lokálny model cez Ollama (beží na grafickej karte, bez internetu a bez poplatkov)
    ollama_url: str = "http://127.0.0.1:11434"
    ollama_model: str = "qwen2.5:7b"
    # Kontext v tokenoch (predvolené Ollamy je 4 096 a dlhší vstup potichu oreže zo začiatku)
    ollama_num_ctx: int = 8192

    # Hostovaný model cez oficiálny Anthropic SDK
    anthropic_model: str = "claude-opus-5-5"
    # Hĺbka premýšľania modelu: low | medium | high | xhigh | max (viac = pomalšie a drahšie)
    anthropic_effort: Literal["low", "medium", "high", "xhigh", "max"] = "medium"
    # Pri odmietnutí odpovede bezpečnostným klasifikátorom nech API skúsi záložný model
    anthropic_fallbacks: bool = True
    # Kľúč z .env alebo z prostredia (bez predpony AGENT_). SecretStr ho nevypíše ani v logu či chybe.
    # Keď chýba, SDK si prihlásenie nájde samo (napr. profil z `ant auth login`).
    anthropic_api_key: SecretStr | None = Field(default=None, validation_alias="ANTHROPIC_API_KEY")

    # Systémy, ktoré agent volá – iba cez HTTP
    api_base_url: str = "http://127.0.0.1:5080"
    ai_service_url: str = "http://127.0.0.1:8000"
    web_base_url: str = "http://localhost:3000"  # odkazy na vytvorený inzerát / ponuku v odpovedi agenta

    # Bezpečnostné limity
    max_steps: int = 8  # najviac toľko volaní modelu na jednu požiadavku používateľa
    request_timeout: float = 30.0  # sekundy na jedno HTTP volanie API
    llm_timeout: float = 180.0  # sekundy na jednu odpoveď modelu (lokálny model býva pomalší)
