"""Nástroje agenta: tenké, typované obaly nad verejným REST API.

Každý nástroj má Pydantic model vstupu (z neho vznikne JSON Schema pre model a zároveň sa ním
overia argumenty, ktoré model pošle) a vracia KOMPAKTNÝ výsledok – ID, názvy, ceny, krátke popisy.
Model tak nečíta celé odpovede API, čo šetrí tokeny a zmenšuje priestor na chyby.

Zapisovacie nástroje (writes=True) sa NIKDY nespustia bez potvrdenia človekom: register najprv ukáže
náhľad (čo sa vytvorí / pošle / prijme) a HTTP volanie urobí iba po súhlase. Vynucuje to kód, nie prompt –
model to neobíde, ani keby ho k tomu nahovoril text v inzeráte.
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


class CreateListingInput(BaseModel):
    category: CategorySlug = Field(description="Kategória – vyber podľa obsahu požiadavky.")
    title: str = Field(min_length=5, max_length=120, description="Krátky výstižný názov po slovensky.")
    description: str = Field(
        min_length=20, description="Popis po slovensky: čo treba urobiť, pre koho, čo má byť výsledkom."
    )
    budget_type: Literal["fixed", "hourly", "negotiable"] = Field("fixed", description="Typ rozpočtu.")
    budget_amount: float | None = Field(None, gt=0, description="Suma v € (pri 'negotiable' vynechaj).")
    city: str | None = Field(None, description="Mesto, ak ho používateľ uviedol.")
    remote: bool = Field(True, description="Dá sa robiť na diaľku.")
    skills: list[str] = Field(min_length=1, max_length=8, description="Potrebné zručnosti (1–8).")
    level: Literal["beginner", "intermediate", "expert"] = Field("intermediate", description="Úroveň.")
    size: Literal["small", "medium", "large"] = Field("small", description="Veľkosť práce.")
    duration: str = Field("do 2 týždňov", description="Odhad trvania vlastnými slovami.")


class SendOfferInput(BaseModel):
    listing: str = Field(
        min_length=1,
        description="Inzerát: ID z výsledkov hľadania ALEBO jeho názov či časť názvu (napr. 'Etikety').",
    )
    price: float = Field(gt=0, description="Cena ponuky v €.")
    delivery_days: int = Field(ge=1, le=365, description="Za koľko dní prácu dodáš.")
    message: str = Field(
        min_length=20,
        description="Správa zadávateľovi po slovensky: prečo práve ja, čo dodám, ako postupujem.",
    )


class OfferRefInput(BaseModel):
    offer: str = Field(
        min_length=1,
        description="Ponuka na môj inzerát: jej ID ALEBO meno špecialistu, ktorý ju poslal (napr. 'Lucia').",
    )
    listing: str | None = Field(
        None,
        description="Časť názvu môjho inzerátu – iba ak ten istý špecialista ponúkol na viac inzerátov.",
    )


class MyListingInput(BaseModel):
    listing: str | None = Field(
        None,
        description=(
            "Ktorý z MOJICH inzerátov: jeho ID alebo časť názvu (napr. 'logo', 'Google Ads'). "
            "Vynechaj, ak chce používateľ ponuky na všetky svoje inzeráty."
        ),
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
    # Zapisovacie nástroje: preview vyrobí text pre človeka, handler sa spustí až po potvrdení
    preview: Callable[[Any], str] | None = None

    @property
    def writes(self) -> bool:
        return self.preview is not None


@dataclass(frozen=True)
class ToolOutcome:
    content: str  # JSON pre model
    is_error: bool
    data: Any  # pôvodný výsledok – pre výpis v CLI a vyhodnotenie
    confirmed: bool | None = None  # pri zápise: súhlasil človek? (None = nástroj nič nezapisuje)


# Otázka človeku: (názov nástroja, náhľad) → súhlas. CLI sa pýta y/n, vyhodnotenie odpovedá podľa scenára.
type Confirm = Callable[[str, str], bool]

DECLINED = {
    "status": "declined",
    "message": "Používateľ akciu nepotvrdil, nič sa nevykonalo. Neopakuj ju, kým o ňu znova nepožiada.",
}


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

    def execute(self, call: ToolCall, confirm: Confirm | None = None) -> ToolOutcome:
        """Overí argumenty a spustí nástroj. Chyby nezhodia agenta – vrátia sa modelu ako výsledok.

        Zápis sa vykoná iba vtedy, keď `confirm` vráti True. Bez `confirm` sa zápis nevykoná nikdy.
        """
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
            if tool.preview is not None:
                preview = tool.preview(arguments)  # môže čítať API (napr. názov inzerátu)
                if confirm is None or not confirm(call.name, preview):
                    return ToolOutcome(
                        json.dumps(DECLINED, ensure_ascii=False), False, DECLINED, confirmed=False
                    )
            data = tool.handler(arguments)
        except ApiError as error:
            return _error(error.to_result())
        except ToolError as error:
            return _error(error.data)
        confirmed = True if tool.preview is not None else None
        return ToolOutcome(json.dumps(data, ensure_ascii=False, default=str), False, data, confirmed)


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
        # Stiahnuté inzeráty sa podľa názvu nehľadajú – ponuky na ne už nikto neposiela a iba by mýlili
        wanted = set(terms(reference))
        active = [item for item in mine if item.get("status") != "cancelled"]
        scored = [(len(wanted & set(terms(item["title"]))), item) for item in active]
        best = max((score for score, _ in scored), default=0)
        matches = [item for score, item in scored if score == best and score > 0]

    if len(matches) == 1:
        return matches[0]
    titles = [
        {"id": item["id"], "title": item["title"]} for item in mine if item.get("status") != "cancelled"
    ]
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
        # Kde už mám ponuku, druhú poslať nejde (API vráti 409) – model to musí vedieť vopred
        try:
            mine = api.get("/api/v1/offers/mine")
        except ApiError:
            mine = []  # označenie je pomôcka – keď sa nepodarí, výsledky hľadania aj tak platia
        offered = {o["listingId"] for o in mine if o["status"] in {"pending", "accepted"}}
        for result in results:
            if result["id"] in offered:
                result["already_offered"] = True
        if any(r.get("already_offered") for r in results):
            return {"results": results, "note": "Na inzeráty s already_offered už ponuku máš – vyber iný."}
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

    def offers_with_profiles(listing_id: str) -> list[dict[str, Any]]:
        results = []
        for offer in api.get(f"/api/v1/listings/{listing_id}/offers"):
            summary = _offer(offer)
            # Na porovnanie ponúk treba aj to, kto ich posiela – verejný profil špecialistu
            try:
                profile = api.get(f"/api/v1/profiles/{offer['freelancerId']}")
                summary["specialist_bio"] = _short(profile.get("bio"), 250)
                summary["specialist_skills"] = profile.get("skills", [])
            except ApiError:
                summary["specialist_bio"] = None
            results.append(summary)
        return results

    def get_offers_on_listing(args: MyListingInput) -> dict[str, Any]:
        if not (args.listing or "").strip():
            # „Aké ponuky som dostal?“ – všetky moje inzeráty naraz, jeden krok pre model
            mine = api.get("/api/v1/listings/mine", pageSize=50)["items"]
            groups = [
                {"listing_id": item["id"], "listing_title": item["title"], "offers": offers}
                for item in mine
                if item["status"] != "cancelled" and (offers := offers_with_profiles(item["id"]))
            ]
            if not groups:
                return {"listings": [], "note": "Na tvoje inzeráty zatiaľ neprišli žiadne ponuky."}
            return {"total_offers": sum(len(g["offers"]) for g in groups), "listings": groups}

        listing = resolve_my_listing(api, args.listing or "")
        if isinstance(listing, Ambiguous):
            return {
                "ambiguous": listing.candidates,
                "note": "Názvu zodpovedá viac inzerátov. Spýtaj sa používateľa, ktorý myslí.",
            }
        results = offers_with_profiles(listing["id"])
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
            "Ponuky, ktoré som DOSTAL na svoje inzeráty, vrátane profilov špecialistov. Inzerát zadaj ID "
            "alebo časťou názvu (napr. 'logo') – netreba ho najprv hľadať; bez inzerátu vráti ponuky "
            "na všetky moje inzeráty.",
            MyListingInput,
            get_offers_on_listing,
        ),
        tool(
            "get_my_offers",
            "Ponuky, ktoré som POSLAL na cudzie inzeráty (ako špecialista). Nie ponuky, ktoré som dostal.",
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


# --- Zapisovacie nástroje (každý s náhľadom pre potvrdenie človekom) ---


def _as_uuid(value: str) -> str | None:
    try:
        return str(UUID(value.strip()))
    except ValueError:
        return None


def _best_by_words(
    items: list[dict[str, Any]], reference: str, text: Callable[[dict[str, Any]], str]
) -> list[dict[str, Any]]:
    """Položky, ktorých text zdieľa najviac slov s odkazom (rovnaké delenie na slová ako BM25)."""
    wanted = set(terms(reference))
    scored = [(len(wanted & set(terms(text(item)))), item) for item in items]
    best = max((score for score, _ in scored), default=0)
    return [item for score, item in scored if score == best and score > 0]


def _offer_label(offer: dict[str, Any]) -> dict[str, Any]:
    return {
        "offer_id": offer["id"],
        "specialist": (offer.get("freelancer") or {}).get("displayName"),
        "listing_title": offer.get("listingTitle"),
        "status": offer["status"],
    }


def _find_my_offer(
    api: MarketplaceClient, reference: str, listing: str | None = None
) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    """Ponuka na MÔJ inzerát podľa ID alebo mena špecialistu + ostatné ponuky na ten istý inzerát.

    Hľadá iba v ponukách na moje inzeráty – cudzie nevidí. Model nemusí najprv zisťovať ID ponuky:
    „prijmi ponuku od Lucie“ = jeden krok.
    """
    by_listing: dict[str, list[dict[str, Any]]] = {}
    for item in api.get("/api/v1/listings/mine", pageSize=50)["items"]:
        if item.get("status") != "cancelled":
            by_listing[item["id"]] = api.get(f"/api/v1/listings/{item['id']}/offers")
    offers = [offer for group in by_listing.values() for offer in group]

    wanted_id = _as_uuid(reference)
    if wanted_id is not None:
        matches = [offer for offer in offers if offer["id"] == wanted_id]
    else:
        candidates = offers
        if listing:
            candidates = _best_by_words(offers, listing, lambda o: o.get("listingTitle") or "") or offers
        matches = _best_by_words(
            candidates, reference, lambda o: (o.get("freelancer") or {}).get("displayName") or ""
        )
        pending = [offer for offer in matches if offer["status"] == "pending"]
        matches = pending or matches  # rozhodovať sa dá iba o čakajúcej ponuke

    if len(matches) == 1:
        offer = matches[0]
        return offer, [o for o in by_listing[offer["listingId"]] if o["id"] != offer["id"]]
    if not matches:
        raise ToolError(
            {
                "error": "offer_not_found",
                "message": f"Ponuku '{reference}' na tvoje inzeráty nevidím.",
                "my_offers": [_offer_label(o) for o in offers],
            }
        )
    raise ToolError(
        {
            "error": "ambiguous_offer",
            "message": "Zodpovedá tomu viac ponúk – zopakuj volanie s parametrom listing alebo sa spýtaj.",
            "candidates": [_offer_label(o) for o in matches],
        }
    )


def _find_listing_for_offer(api: MarketplaceClient, search: SearchClient, reference: str) -> dict[str, Any]:
    """Inzerát pre ponuku podľa ID alebo názvu (hybridné hľadanie + zhoda slov v názve)."""
    wanted_id = _as_uuid(reference)
    if wanted_id is not None:
        return api.get(f"/api/v1/listings/{wanted_id}")
    details = [api.get(f"/api/v1/listings/{hit['listing_id']}") for hit in search.search(reference, 10)]
    open_listings = [d for d in details if d["status"] == "open"]
    matches = _best_by_words(open_listings, reference, lambda d: d["title"])
    if len(matches) == 1:
        return matches[0]
    if not matches:
        raise ToolError(
            {
                "error": "listing_not_found",
                "message": f"Otvorený inzerát '{reference}' som nenašiel. Použi search_listings.",
            }
        )
    raise ToolError(
        {
            "error": "ambiguous_listing",
            "message": "Názvu zodpovedá viac inzerátov – spýtaj sa používateľa alebo použi ID.",
            "candidates": [{"id": d["id"], "title": d["title"]} for d in matches],
        }
    )


def build_write_tools(api: MarketplaceClient, search: SearchClient, web_base_url: str) -> list[Tool]:
    def listing_url(listing_id: str) -> str:
        return f"{web_base_url}/inzeraty/{listing_id}"

    def _amount(args: CreateListingInput) -> str:
        return _budget({"type": args.budget_type, "amount": args.budget_amount})

    def preview_create_listing(args: CreateListingInput) -> str:
        place = ", ".join(part for part in (args.city, "aj na diaľku" if args.remote else None) if part)
        return "\n".join(
            [
                "Zverejniť nový inzerát:",
                f"  Názov:      {args.title}",
                f"  Kategória:  {args.category}",
                f"  Rozpočet:   {_amount(args)}",
                f"  Miesto:     {place or 'neuvedené'}",
                f"  Zručnosti:  {', '.join(args.skills)}",
                f"  Popis:      {_short(args.description, 300)}",
            ]
        )

    def create_listing(args: CreateListingInput) -> dict[str, Any]:
        created = api.post(
            "/api/v1/listings",
            {
                "category": args.category,
                "title": args.title,
                "description": args.description,
                "budget": {"type": args.budget_type, "amount": args.budget_amount},
                "city": args.city,
                "remote": args.remote,
                "deadline": None,
                "level": args.level,
                "scope": {"size": args.size, "duration": args.duration},
                "engagement": "one_time",
                "invoiceRequired": False,
                "skills": args.skills,
                "deliverables": None,
                "requirements": None,
            },
        )
        return {
            "status": "created",
            "listing_id": created["id"],
            "title": created["title"],
            "url": listing_url(created["id"]),
        }

    def preview_send_offer(args: SendOfferInput) -> str:
        listing = _find_listing_for_offer(api, search, args.listing)
        owner = (listing.get("owner") or {}).get("displayName") or "neznámy zadávateľ"
        budget = _budget(listing["budget"])
        return "\n".join(
            [
                f"Poslať ponuku na inzerát „{listing['title']}“ ({owner}, rozpočet {budget}):",
                f"  Cena:       {args.price:g} €",
                f"  Dodanie:    {args.delivery_days} dní",
                f"  Správa:     {_short(args.message, 400)}",
            ]
        )

    def send_offer(args: SendOfferInput) -> dict[str, Any]:
        listing = _find_listing_for_offer(api, search, args.listing)
        offer = api.post(
            f"/api/v1/listings/{listing['id']}/offers",
            {"price": args.price, "deliveryDays": args.delivery_days, "message": args.message},
        )
        return {
            "status": "sent",
            "offer_id": offer["id"],
            "listing_title": listing["title"],
            "url": listing_url(listing["id"]),
        }

    def preview_accept_offer(args: OfferRefInput) -> str:
        offer, others = _find_my_offer(api, args.offer, args.listing)
        pending = [o for o in others if o["status"] == "pending"]
        name = (offer.get("freelancer") or {}).get("displayName") or "špecialista"
        lines = [
            f"Prijať ponuku na „{offer.get('listingTitle')}“:",
            f"  Od:         {name}",
            f"  Cena:       {offer['price']:g} €, dodanie {offer['deliveryDays']} dní",
            "  Inzerát prejde do stavu „v realizácii“.",
        ]
        if pending:
            lines.append(f"  Ostatné čakajúce ponuky ({len(pending)}) budú automaticky odmietnuté.")
        lines.append("  Prijatie sa nedá vrátiť späť.")
        return "\n".join(lines)

    def accept_offer(args: OfferRefInput) -> dict[str, Any]:
        offer_id = _find_my_offer(api, args.offer, args.listing)[0]["id"]
        offer = api.post(f"/api/v1/offers/{offer_id}/accept")
        return {"status": "accepted", "offer": _offer(offer), "url": listing_url(offer["listingId"])}

    def preview_reject_offer(args: OfferRefInput) -> str:
        offer, _ = _find_my_offer(api, args.offer, args.listing)
        name = (offer.get("freelancer") or {}).get("displayName") or "špecialista"
        return f"Odmietnuť ponuku od {name} ({offer['price']:g} €) na „{offer.get('listingTitle')}“."

    def reject_offer(args: OfferRefInput) -> dict[str, Any]:
        offer_id = _find_my_offer(api, args.offer, args.listing)[0]["id"]
        offer = api.post(f"/api/v1/offers/{offer_id}/reject")
        return {"status": "rejected", "offer": _offer(offer)}

    def tool(
        name: str,
        description: str,
        model: type[BaseModel],
        handler: Callable[[Any], Any],
        preview: Callable[[Any], str],
    ) -> Tool:
        return Tool(ToolSpec(name, description, _schema(model)), model, handler, preview)

    confirm_note = " Používateľ akciu pred vykonaním potvrdí – zavolaj nástroj rovno, nepýtaj sa vopred."
    return [
        tool(
            "create_listing",
            "Zverejní nový inzerát prihláseného používateľa (zadávateľa)." + confirm_note,
            CreateListingInput,
            create_listing,
            preview_create_listing,
        ),
        tool(
            "send_offer",
            "Pošle ponuku špecialistu na otvorený inzerát." + confirm_note,
            SendOfferInput,
            send_offer,
            preview_send_offer,
        ),
        tool(
            "accept_offer",
            "Prijme ponuku na môj inzerát; ostatné čakajúce ponuky sa odmietnu." + confirm_note,
            OfferRefInput,
            accept_offer,
            preview_accept_offer,
        ),
        tool(
            "reject_offer",
            "Odmietne ponuku na môj inzerát." + confirm_note,
            OfferRefInput,
            reject_offer,
            preview_reject_offer,
        ),
    ]
