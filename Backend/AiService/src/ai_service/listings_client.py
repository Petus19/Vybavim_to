"""Čítanie inzerátov z hlavného C# API – ako hocijaký iný klient, bez prístupu k jeho tabuľkám."""

from typing import Any, Protocol
from uuid import UUID

import httpx2

from ai_service.listing_text import ListingDocument

PAGE_SIZE = 50  # maximum, ktoré C# API dovolí


class ListingsSource(Protocol):
    """Zdroj inzerátov. V službe C# API, v testoch zoznam v pamäti."""

    def fetch_all(self) -> list[ListingDocument]: ...


class ApiListingsSource:
    """Stiahne všetky verejné inzeráty: zoznam po stránkach, potom detail každého (popis, zručnosti).

    Pre stovky inzerátov to stačí. Pri raste ich bude služba dostávať cez udalosti (outbox)
    namiesto opakovaného sťahovania všetkého.
    """

    def __init__(self, base_url: str, client: httpx2.Client | None = None) -> None:
        self._client = client or httpx2.Client(base_url=base_url, timeout=10.0)

    def fetch_all(self) -> list[ListingDocument]:
        ids: list[str] = []
        page = 1
        while True:
            response = self._client.get("/api/v1/listings", params={"page": page, "pageSize": PAGE_SIZE})
            response.raise_for_status()
            body = response.json()
            ids.extend(item["id"] for item in body["items"])
            if page * PAGE_SIZE >= body["total"]:
                break
            page += 1

        return [self._fetch_detail(listing_id) for listing_id in ids]

    def _fetch_detail(self, listing_id: str) -> ListingDocument:
        response = self._client.get(f"/api/v1/listings/{listing_id}")
        response.raise_for_status()
        return to_document(response.json())


def to_document(detail: dict[str, Any]) -> ListingDocument:
    """JSON detailu inzerátu z C# API → ListingDocument."""
    return ListingDocument(
        id=UUID(detail["id"]),
        title=detail["title"],
        category=detail["category"],
        description=detail["description"],
        skills=tuple(detail["skills"]),
        status=detail["status"],
    )
