"""Stav trhoviska pred a po scenári – čítaný cez verejné API ako demo používatelia (nie z databázy).

Vyhodnotenie tak overuje to isté, čo by videl človek na webe: pribudol inzerát? má ponuka stav „prijatá“?
"""

from dataclasses import dataclass, field
from typing import Any

import httpx2 as httpx

from ai_service.agent.config import AgentSettings
from ai_service.agent.marketplace import MarketplaceClient

DEMO_PASSWORD = "Demo1234"  # noqa: S105 – verejné heslo demo účtov z README, existujú iba vo vývoji
CLIENT = "client.demo"
SPECIALISTS = ("specialist.demo", "specialist2.demo", "specialist3.demo")


@dataclass(frozen=True)
class Snapshot:
    listings: dict[str, dict[str, Any]]  # inzeráty zadávateľa client.demo (id → karta)
    received: dict[str, dict[str, Any]]  # ponuky na jeho inzeráty (id → ponuka)
    sent: dict[str, dict[str, Any]] = field(default_factory=dict)  # ponuky demo špecialistov (id → ponuka)

    def fingerprint(self) -> tuple[Any, ...]:
        """Všetko, čo sa zápisom môže zmeniť – dva rovnaké odtlačky = nič sa nezapísalo."""
        return (
            sorted((i, v["status"]) for i, v in self.listings.items()),
            sorted((i, v["status"]) for i, v in self.received.items()),
            sorted((i, v["status"]) for i, v in self.sent.items()),
        )


class World:
    def __init__(self, settings: AgentSettings) -> None:
        self._settings = settings
        self._clients: dict[str, MarketplaceClient] = {}
        self._http = httpx.Client(timeout=60)

    def close(self) -> None:
        self._http.close()
        for client in self._clients.values():
            client.close()

    def client(self, user: str) -> MarketplaceClient:
        if user not in self._clients:
            api = MarketplaceClient(self._settings.api_base_url, self._settings.request_timeout)
            api.login(user, DEMO_PASSWORD)
            self._clients[user] = api
        return self._clients[user]

    def reset(self) -> None:
        """Čerstvé demo dáta (vývojový endpoint C# API) a zosynchronizovaný index hľadania."""
        self._http.post(f"{self._settings.api_base_url}/api/v1/dev/reset-demo-data").raise_for_status()
        self._http.post(f"{self._settings.ai_service_url}/index").raise_for_status()

    def snapshot(self) -> Snapshot:
        owner = self.client(CLIENT)
        listings = {item["id"]: item for item in owner.get("/api/v1/listings/mine", pageSize=50)["items"]}
        received = {
            offer["id"]: offer
            for listing in listings.values()
            if listing["status"] != "cancelled"
            for offer in owner.get(f"/api/v1/listings/{listing['id']}/offers")
        }
        sent = {
            offer["id"]: offer | {"sender": user}
            for user in SPECIALISTS
            for offer in self.client(user).get("/api/v1/offers/mine")
        }
        return Snapshot(listings, received, sent)

    def public_titles(self) -> list[str]:
        """Názvy všetkých verejných inzerátov – na kontrolu, že agent pri „nič sa nenašlo“ nič neponúkol."""
        titles: list[str] = []
        page = 1
        while True:
            body = self.client(CLIENT).get("/api/v1/listings", page=page, pageSize=50)
            titles += [item["title"] for item in body["items"]]
            if page * 50 >= body["total"]:
                return titles
            page += 1
