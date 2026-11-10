"""Nástroje agenta: tenké, typované obaly nad verejným REST API.

Každý nástroj má Pydantic model vstupu (z neho vznikne JSON Schema pre model a zároveň sa ním
overia argumenty, ktoré model pošle) a vracia KOMPAKTNÝ výsledok – ID, názvy, ceny, krátke popisy.
Model tak nečíta celé odpovede API, čo šetrí tokeny a zmenšuje priestor na chyby.

V tomto kroku iba čítanie. Zapisovacie nástroje (inzerát, ponuka, prijatie) s potvrdením pribudnú ďalej.
"""

import json
from collections.abc import Callable
from dataclasses import dataclass
from typing import Any, Literal
from uuid import UUID

from pydantic import BaseModel, Field, ValidationError

from ai_service.agent.llm import ToolCall, ToolSpec
from ai_service.agent.marketplace import ApiError, MarketplaceClient, SearchClient
from ai_service.text_search import terms

CategorySlug = Literal["web", "grafika", "it-podpora", "marketing"]

DESCRIPTION_LIMIT = 500  # znakov popisu, ktoré model dostane – viac na rozhodnutie netreba
SEARCH_CANDIDATES = 15  # koľko výsledkov hybridného hľadania sa preverí filtrami


# --- Vstupy nástrojov (z nich vznikne JSON Schema pre model) ---


class NoInput(BaseModel):
    pass


class SearchListingsInput(BaseModel):
    query: str | None = Field(
        None,
        description=(
            "Čo sa hľadá, vlastnými slovami (napr. 'logo pre kaviareň'). "
            "Vynechaj pri hľadaní iba podľa filtrov."
        ),
    )
    category: CategorySlug | None = Field(None, description="Kategória inzerátu.")
    max_budget: float | None = Field(None, gt=0, description="Najvyššia pevná cena v € (napr. 300).")
    city: str | None = Field(None, description="Mesto (napr. 'Košice').")
    remote: bool | None = Field(None, description="True = iba práca na diaľku.")
    limit: int = Field(5, ge=1, le=10, description="Koľko výsledkov vrátiť.")


class ListingIdInput(BaseModel):
    listing_id: UUID = Field(description="ID inzerátu z výsledkov hľadania alebo z mojich inzerátov.")


class MyListingInput(BaseModel):
    listing: str = Field(
        min_length=1,
        description="Ktorý z MOJICH inzerátov: jeho ID alebo časť názvu (napr. 'logo', 'Google Ads').",
    )


# --- Kompaktné tvary výsledkov ---


def _budget(budget: dict[str, Any]) -> str:
    match budget.get("type"):
        case "fixed":
            return f"{budget['amount']:g} € (pevná cena)"
        case "hourly":
            return f"{budget['amount']:g} €/hod."
        case _:
            return "dohodou"


def _short(text: str | None, limit: int = DESCRIPTION_LIMIT) -> str:
    text = (text or "").strip()
    return text if len(text) <= limit else text[: limit - 1].rstrip() + "…"


def _listing_summary(detail: dict[str, Any]) -> dict[str, Any]:
    return {
        "id": detail["id"],
        "title": detail["title"],
        "category": detail["category"],
        "budget": _budget(detail["budget"]),
        "city": detail.get("city"),
        "remote": detail["remote"],
        "status": detail["status"],
    }


def _offer(offer: dict[str, Any]) -> dict[str, Any]:
    freelancer = offer.get("freelancer") or {}
    return {
        "offer_id": offer["id"],
        "listing_id": offer["listingId"],
        "listing_title": offer.get("listingTitle"),
        "specialist": freelancer.get("displayName"),
        "specialist_id": offer["freelancerId"],
        "price": f"{offer['price']:g} €",
        "delivery_days": offer["deliveryDays"],
        "message": _short(offer["message"], 400),
        "status": offer["status"],
    }


# --- Register nástrojov ---


@dataclass(frozen=True)
class Tool:
    spec: ToolSpec
    input_model: type[BaseModel]
    handler: Callable[[Any], Any]
    writes: bool = False  # zapisovacie nástroje budú vyžadovať potvrdenie človekom


@dataclass(frozen=True)
class ToolOutcome:
    content: str  # JSON pre model
    is_error: bool
    data: Any  # pôvodný výsledok – pre výpis v CLI a vyhodnotenie


def _schema(model: type[BaseModel]) -> dict[str, Any]:
    """JSON Schema bez nadbytočných „title“ – menej tokenov, model ich nepotrebuje."""
    schema = model.model_json_schema()
    schema.pop("title", None)
    for prop in schema.get("properties", {}).values():
        prop.pop("title", None)
    return schema


class ToolRegistry:
    def __init__(self, tools: list[Tool]) -> None:
        self._tools = {tool.spec.name: tool for tool in tools}

    @property
    def specs(self) -> list[ToolSpec]:
        return [tool.spec for tool in self._tools.values()]

    def get(self, name: str) -> Tool | None:
        return self._tools.get(name)

    def execute(self, call: ToolCall) -> ToolOutcome:
        """Overí argumenty a spustí nástroj. Chyby nezhodia agenta – vrátia sa modelu ako výsledok."""
        tool = self._tools.get(call.name)
        if tool is None:
            return _error({"error": "unknown_tool", "message": f"Nástroj '{call.name}' neexistuje."})
        try:
            arguments = tool.input_model.model_validate(call.arguments)
        except ValidationError as error:
            problems = "; ".join(
                f"{'.'.join(map(str, e['loc'])) or 'vstup'}: {e['msg']}" for e in error.errors()
            )
            return _error({"error": "invalid_arguments", "message": problems})
        try:
            data = tool.handler(arguments)
        except ApiError as error:
            return _error(error.to_result())
        except ToolError as error:
            return _error(error.data)
        return ToolOutcome(json.dumps(data, ensure_ascii=False, default=str), False, data)


def _error(data: dict[str, Any]) -> ToolOutcome:
    return ToolOutcome(json.dumps(data, ensure_ascii=False), True, data)


class ToolError(Exception):
    """Chyba, ktorú nástroj vráti modelu ako výsledok – s radou, čo urobiť ďalej."""

    def __init__(self, data: dict[str, Any]) -> None:
        super().__init__(data.get("message", ""))
        self.data = data


@dataclass(frozen=True)
class Ambiguous:
    """Názvu zodpovedá viac inzerátov – model sa musí spýtať používateľa, ktorý myslí."""

    candidates: list[dict[str, Any]]


def resolve_my_listing(api: MarketplaceClient, reference: str) -> dict[str, Any] | Ambiguous:
    """Nájde MÔJ inzerát podľa ID alebo časti názvu. Hľadá iba v /listings/mine – cudzie nevidí.

    Model tak nemusí najprv zisťovať ID ďalším nástrojom: „ponuky na logo“ = jeden krok namiesto dvoch.
    """
    mine = api.get("/api/v1/listings/mine", pageSize=50)["items"]
    reference = reference.strip()
    try:
        wanted_id = str(UUID(reference))
    except ValueError:
        wanted_id = None

    if wanted_id is not None:
        matches = [item for item in mine if item["id"] == wanted_id]
    else:
        # Rovnaké delenie na slová ako BM25: bez diakritiky, „loga“ = „logo“
        wanted = set(terms(reference))
        scored = [(len(wanted & set(terms(item["title"]))), item) for item in mine]
        best = max((score for score, _ in scored), default=0)
        matches = [item for score, item in scored if score == best and score > 0]

    if len(matches) == 1:
        return matches[0]
    titles = [{"id": item["id"], "title": item["title"]} for item in mine]
    if not matches:
        raise ToolError(
            {
                "error": "listing_not_found",
                "message": f"Žiadny z inzerátov používateľa nezodpovedá '{reference}'.",
                "my_listings": titles,
            }
        )
    return Ambiguous([{"id": item["id"], "title": item["title"]} for item in matches])


def build_read_tools(api: MarketplaceClient, search: SearchClient) -> list[Tool]:
    def list_categories(_: NoInput) -> list[dict[str, Any]]:
        return [
            {"slug": c["slug"], "name": c["name"], "description": c["description"]}
            for c in api.get("/api/v1/categories")
        ]

    def search_listings(args: SearchListingsInput) -> dict[str, Any]:
        if args.query:
            # Hybridné hľadanie AI služby; prázdny zoznam = nič relevantné (nevymýšľať!)
            hits = search.search(args.query, SEARCH_CANDIDATES)
            details = [api.get(f"/api/v1/listings/{hit['listing_id']}") for hit in hits]
            matches = [d for d in details if _passes_filters(d, args)][: args.limit]
        else:
            page = api.get(
                "/api/v1/listings",
                category=args.category,
                priceMax=args.max_budget,
                city=args.city,
                remote=args.remote,
                openOnly=True,
                pageSize=args.limit,
            )
            matches = [api.get(f"/api/v1/listings/{item['id']}") for item in page["items"]]

        results = [_listing_summary(d) | {"description": _short(d["description"], 200)} for d in matches]
        if not results:
            return {
                "results": [],
                "note": "Nič relevantné sa nenašlo. Nevymýšľaj inzeráty, povedz to používateľovi.",
            }
        return {"results": results}

    def get_listing(args: ListingIdInput) -> dict[str, Any]:
        detail = api.get(f"/api/v1/listings/{args.listing_id}")
        owner = detail.get("owner") or {}
        return _listing_summary(detail) | {
            "description": _short(detail["description"]),
            "skills": detail["skills"],
            "level": detail["level"],
            "deadline": detail.get("deadline"),
            "owner": owner.get("displayName"),
        }

    def get_my_profile(_: NoInput) -> dict[str, Any]:
        try:
            profile = api.get("/api/v1/profiles/me")
        except ApiError as error:
            if error.code == "profile.not_found":
                return {"profile": None, "note": "Používateľ ešte nemá vyplnený profil."}
            raise
        return {
            "name": profile["displayName"],
            "type": profile["type"],
            "city": profile.get("city"),
            "bio": _short(profile.get("bio"), 300),
            "skills": profile.get("skills", []),
        }

    def get_my_listings(_: NoInput) -> dict[str, Any]:
        page = api.get("/api/v1/listings/mine", pageSize=20)
        return {
            "results": [
                {"id": i["id"], "title": i["title"], "budget": _budget(i["budget"]), "status": i["status"]}
                for i in page["items"]
            ]
        }

    def get_offers_on_listing(args: MyListingInput) -> dict[str, Any]:
        listing = resolve_my_listing(api, args.listing)
        if isinstance(listing, Ambiguous):
            return {
                "ambiguous": listing.candidates,
                "note": "Názvu zodpovedá viac inzerátov. Spýtaj sa používateľa, ktorý myslí.",
            }
        offers = api.get(f"/api/v1/listings/{listing['id']}/offers")
        results = []
        for offer in offers:
            summary = _offer(offer)
            # Na porovnanie ponúk treba aj to, kto ich posiela – verejný profil špecialistu
            try:
                profile = api.get(f"/api/v1/profiles/{offer['freelancerId']}")
                summary["specialist_bio"] = _short(profile.get("bio"), 250)
                summary["specialist_skills"] = profile.get("skills", [])
            except ApiError:
                summary["specialist_bio"] = None
            results.append(summary)
        header = {"listing_id": listing["id"], "listing_title": listing["title"]}
        if not results:
            return header | {"offers": [], "note": "Na tento inzerát zatiaľ nie sú ponuky."}
        return header | {"offers": results}

    def get_my_offers(_: NoInput) -> dict[str, Any]:
        return {"offers": [_offer(o) for o in api.get("/api/v1/offers/mine")]}

    def tool(name: str, description: str, model: type[BaseModel], handler: Callable[[Any], Any]) -> Tool:
        return Tool(ToolSpec(name, description, _schema(model)), model, handler)

    return [
        tool("list_categories", "Zoznam kategórií inzerátov (slug, názov, popis).", NoInput, list_categories),
        tool(
            "search_listings",
            "Nájde OTVORENÉ inzeráty podľa významu textu (hybridné hľadanie) a filtrov. "
            "Prázdny zoznam znamená, že nič relevantné neexistuje.",
            SearchListingsInput,
            search_listings,
        ),
        tool(
            "get_listing",
            "Detail jedného inzerátu: popis, zručnosti, rozpočet, zadávateľ.",
            ListingIdInput,
            get_listing,
        ),
        tool(
            "get_my_profile",
            "Profil prihláseného používateľa (meno, mesto, zručnosti).",
            NoInput,
            get_my_profile,
        ),
        tool(
            "get_my_listings",
            "Inzeráty, ktoré prihlásený používateľ zverejnil, vo všetkých stavoch.",
            NoInput,
            get_my_listings,
        ),
        tool(
            "get_offers_on_listing",
            "Ponuky špecialistov na MÔJ inzerát vrátane ich profilu. Inzerát zadaj ID alebo časťou názvu "
            "(napr. 'logo') – netreba ho najprv hľadať.",
            MyListingInput,
            get_offers_on_listing,
        ),
        tool(
            "get_my_offers",
            "Ponuky, ktoré prihlásený používateľ poslal (ako špecialista).",
            NoInput,
            get_my_offers,
        ),
    ]


def _passes_filters(detail: dict[str, Any], args: SearchListingsInput) -> bool:
    if detail["status"] != "open":
        return False
    if args.category and detail["category"] != args.category:
        return False
    if args.city and (detail.get("city") or "").casefold() != args.city.casefold():
        return False
    if args.remote is not None and detail["remote"] != args.remote:
        return False
    if args.max_budget is not None:
        budget = detail["budget"]
        if budget["type"] == "hourly":
            return False  # hodinovú sadzbu nevieme porovnať s celkovým rozpočtom
        if budget["type"] == "fixed" and budget["amount"] > args.max_budget:
            return False
    return True
