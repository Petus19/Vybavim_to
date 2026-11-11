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
    # vývojové heslo ako v appsettings.Development.json.
    database_url: str = "postgresql://vybavim:vybavim@localhost:5432/vybavim"

    # Viacjazyčný embedding model (vie aj po slovensky), 384 čísel na text, beží na CPU.
    # Vybraný porovnaním so multilingual-e5-small na slovenských dopytoch.
    embedding_model: str = "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2"

    # Minimálna kosínusová podobnosť pre výsledok hybridného hľadania (nastavené vyhodnotením pre MiniLM).
    # Iný model má inú škálu skóre – pri jeho zmene treba hodnotu znova odmerať.
    min_score: float = 0.45

    # Ako často (v sekundách) si služba sama stiahne zmeny inzerátov z C# API a obnoví index.
    # 0 = vypnuté (iba ručne cez POST /index). Indexovanie je inkrementálne, takže beh bez zmien je lacný.
    sync_interval: float = 30.0


@lru_cache
def get_settings() -> Settings:
    """Nastavenia sa načítajú raz a potom sa vracia stále tá istá inštancia."""
    return Settings()
