"""Nastavenia služby z premenných prostredia (obdoba appsettings.json v C#)."""

from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Každé pole sa dá prepísať premennou prostredia s predponou AI_, napr. AI_API_BASE_URL.

    Pydantic hodnoty pri načítaní overí a prevedie na správny typ – zlá hodnota
    (napr. text namiesto čísla) zastaví štart služby s jasnou chybou.
    """

    model_config = SettingsConfigDict(env_prefix="AI_", env_file=".env", extra="ignore")

    # Hlavné C# API – odtiaľ služba číta inzeráty (ako hocijaký iný klient, nie priamo z tabuliek)
    api_base_url: str = "http://localhost:5080"

    # Databáza pre vlastnú schému "ai" (embeddingy). Rovnaký PostgreSQL ako docker-compose.yml,
    # vývojové heslo ako v appsettings.Development.json. Použijeme ju v ďalšom kroku.
    database_url: str = "postgresql://vybavim:vybavim@localhost:5432/vybavim"


@lru_cache
def get_settings() -> Settings:
    """Nastavenia sa načítajú raz a potom sa vracia stále tá istá inštancia."""
    return Settings()
