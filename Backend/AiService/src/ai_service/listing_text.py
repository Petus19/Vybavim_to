"""Inzerát ako text pre embedding model.

Model nevidí databázu ani JSON – dostane iba text. Čo sem dáme, podľa toho sa bude hľadať.
"""

import hashlib
from dataclasses import dataclass
from uuid import UUID

# Zvýš, keď sa zmení, čo sa o inzeráte ukladá (napr. pribudol text pre BM25) – zmení sa tým odtlačok
# každého inzerátu a najbližšie /index všetko jednorazovo prepočíta.
INDEX_FORMAT_VERSION = 2


@dataclass(frozen=True)
class ListingDocument:
    """Údaje inzerátu z C# API, ktoré potrebujeme na embedding a na zobrazenie výsledku."""

    id: UUID
    title: str
    category: str
    description: str
    skills: tuple[str, ...]
    status: str

    def text(self) -> str:
        """Text, z ktorého model vyrobí vektor: názov, kategória, popis a zručnosti.

        Názov je na začiatku zámerne – je to najhutnejšie zhrnutie toho, o čo ide.
        """
        skills = ", ".join(self.skills)
        return f"{self.title}. Kategória: {self.category}. {self.description} Zručnosti: {skills}"

    def search_text(self) -> str:
        """Text pre hľadanie podľa slov (BM25): rovnaké polia, bez pomocných slov „Kategória:“."""
        return f"{self.title} {self.description} {' '.join(self.skills)} {self.category}"

    def content_hash(self, model_name: str) -> str:
        """Odtlačok textu, modelu a formátu indexu. Keď sa nič nezmení, vektor netreba počítať znova."""
        payload = f"v{INDEX_FORMAT_VERSION}\n{model_name}\n{self.text()}"
        return hashlib.sha256(payload.encode()).hexdigest()
