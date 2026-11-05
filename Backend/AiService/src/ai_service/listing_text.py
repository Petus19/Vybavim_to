"""Inzerát ako text pre embedding model.

Model nevidí databázu ani JSON – dostane iba text. Čo sem dáme, podľa toho sa bude hľadať.
"""

import hashlib
from dataclasses import dataclass
from uuid import UUID


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

    def content_hash(self, model_name: str) -> str:
        """Odtlačok textu a modelu. Keď sa nezmení ani jedno, vektor netreba počítať znova."""
        return hashlib.sha256(f"{model_name}\n{self.text()}".encode()).hexdigest()
