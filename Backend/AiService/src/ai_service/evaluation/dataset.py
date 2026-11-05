"""Testovacia sada: inzeráty a dopyty s ručne určenými správnymi odpoveďami (data/*.json).

Dáta sú syntetické (napísané pre tento projekt), nie od skutočných používateľov – a tak to aj uvádzame.
"""

import json
from dataclasses import dataclass
from pathlib import Path
from uuid import NAMESPACE_URL, uuid5

from ai_service.listing_text import ListingDocument

DATA_DIR = Path(__file__).parent / "data"

# Druhy dopytov – výsledky sa vypisujú aj po druhoch, aby bolo vidno, KDE ktorá metóda vyhráva
QUERY_KINDS = ("exact", "synonym", "no-diacritics", "typo", "natural", "no-answer")


@dataclass(frozen=True)
class EvalListing:
    key: str  # čitateľné ID, napr. "graf-01" – dopyty sa naň odkazujú
    document: ListingDocument  # ten istý typ ako v službe → rovnaký text pre model ako v produkcii


@dataclass(frozen=True)
class EvalQuery:
    id: str
    kind: str
    text: str
    relevant: frozenset[str]  # kľúče inzerátov, ktoré sú správnou odpoveďou; prázdne = nič vhodné neexistuje


@dataclass(frozen=True)
class Dataset:
    listings: tuple[EvalListing, ...]
    queries: tuple[EvalQuery, ...]

    @property
    def answerable(self) -> tuple[EvalQuery, ...]:
        """Dopyty, ku ktorým existuje aspoň jeden správny inzerát – len z nich sa počíta recall a MRR."""
        return tuple(q for q in self.queries if q.relevant)


def load_dataset(directory: Path = DATA_DIR) -> Dataset:
    raw_listings = json.loads((directory / "listings.json").read_text(encoding="utf-8"))
    raw_queries = json.loads((directory / "queries.json").read_text(encoding="utf-8"))

    listings = tuple(
        EvalListing(
            key=item["id"],
            document=ListingDocument(
                id=uuid5(NAMESPACE_URL, f"vybavim-to/eval/{item['id']}"),  # stabilné UUID z kľúča
                title=item["title"],
                category=item["category"],
                description=item["description"],
                skills=tuple(item["skills"]),
                status="open",
            ),
        )
        for item in raw_listings
    )
    queries = tuple(
        EvalQuery(item["id"], item["kind"], item["text"], frozenset(item["relevant"])) for item in raw_queries
    )
    return Dataset(listings, queries)
