"""Klient C# API: stránkovanie zoznamu a detail každého inzerátu – bez siete (MockTransport)."""

import httpx2

from ai_service.listings_client import PAGE_SIZE, ApiListingsSource


def detail(listing_id: str) -> dict[str, object]:
    return {
        "id": listing_id,
        "title": f"Inzerát {listing_id[-1]}",
        "category": "grafika",
        "description": "Popis",
        "skills": ["Logo"],
        "status": "open",
    }


def test_fetch_all_reads_every_page_and_detail() -> None:
    total = PAGE_SIZE + 1  # dve stránky
    ids = [f"00000000-0000-0000-0000-{i:012d}" for i in range(total)]
    requested: list[str] = []

    def handler(request: httpx2.Request) -> httpx2.Response:
        requested.append(request.url.path)
        if request.url.path == "/api/v1/listings":
            page = int(request.url.params["page"])
            items = ids[(page - 1) * PAGE_SIZE : page * PAGE_SIZE]
            return httpx2.Response(200, json={"items": [{"id": i} for i in items], "total": total})
        return httpx2.Response(200, json=detail(request.url.path.rsplit("/", 1)[1]))

    client = httpx2.Client(base_url="http://api", transport=httpx2.MockTransport(handler))

    documents = ApiListingsSource("http://api", client).fetch_all()

    assert len(documents) == total
    assert requested.count("/api/v1/listings") == 2
    assert documents[0].skills == ("Logo",)
