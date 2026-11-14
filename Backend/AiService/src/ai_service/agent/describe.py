"""Kroky agenta po slovensky pre človeka: čo agent práve robí a čo z toho vyšlo.

Model dostáva celé dáta (JSON), človek vidí jeden riadok – v termináli aj vo webovom chate.
Argumenty píše model, preto sa s nimi zaobchádza opatrne (chýbajúce pole nesmie nič zhodiť).
"""

import json
from typing import Any

from ai_service.agent.llm import ToolCall

# Nadpis karty s potvrdením – čo sa stane, keď človek klikne „Potvrdiť“
ACTION_TITLES = {
    "create_listing": "Zverejniť inzerát",
    "send_offer": "Poslať ponuku",
    "accept_offer": "Prijať ponuku",
    "reject_offer": "Odmietnuť ponuku",
}


def _text(value: Any, limit: int = 60) -> str:
    text = str(value or "").strip()
    return text if len(text) <= limit else text[: limit - 1].rstrip() + "…"


def _amount(value: Any) -> str:
    try:
        return f"{float(value):g} €"
    except (TypeError, ValueError):
        return ""


def _search(args: dict[str, Any]) -> str:
    filters = [
        _text(args.get("query")),
        f"do {_amount(args['max_budget'])}" if args.get("max_budget") else "",
        _text(args.get("city")),
        "na diaľku" if args.get("remote") else "",
        _text(args.get("category")) if not args.get("query") else "",
    ]
    described = ", ".join(part for part in filters if part)
    return f"Hľadám inzeráty: {described}" if described else "Hľadám otvorené inzeráty"


def describe_call(call: ToolCall) -> str:
    """„Hľadám inzeráty: logo, do 300 €“ – čo agent robí, bez ID a technických názvov."""
    args = call.arguments if isinstance(call.arguments, dict) else {}
    listing = _text(args.get("listing"))
    match call.name:
        case "list_categories":
            return "Pozerám kategórie"
        case "search_listings":
            return _search(args)
        case "get_listing":
            return "Čítam detail inzerátu"
        case "get_my_profile":
            return "Pozerám tvoj profil"
        case "get_my_listings":
            return "Pozerám tvoje inzeráty"
        case "get_offers_on_listing":
            return f"Pozerám ponuky na inzerát „{listing}“" if listing else "Pozerám ponuky na tvoje inzeráty"
        case "get_my_offers":
            return "Pozerám ponuky, ktoré si poslal"
        case "create_listing":
            return f"Pripravujem inzerát „{_text(args.get('title'))}“"
        case "send_offer":
            price = _amount(args.get("price"))
            return f"Pripravujem ponuku na „{listing}“" + (f" za {price}" if price else "")
        case "accept_offer":
            return f"Pripravujem prijatie ponuky ({_text(args.get('offer'))})"
        case "reject_offer":
            return f"Pripravujem odmietnutie ponuky ({_text(args.get('offer'))})"
        case _:
            return call.name


def summarize_result(data: Any) -> str:
    """Jeden riadok o výsledku nástroja – celé dáta dostáva model, človek vidí súhrn."""
    if isinstance(data, dict):
        if "error" in data:
            return f"chyba {data['error']}: {data.get('message', '')}"
        status = data.get("status")
        if status == "declined":
            return "nepotvrdené – nič sa nevykonalo"
        if status in {"created", "sent", "accepted", "rejected"}:
            done = {
                "created": "vytvorené",
                "sent": "odoslané",
                "accepted": "prijaté",
                "rejected": "odmietnuté",
            }
            return f"{done[status]} {data.get('url', '')}".rstrip()
        if "ambiguous" in data:
            return f"nejednoznačné – {len(data['ambiguous'])} inzeráty"
        if "listings" in data:
            count = sum(len(group["offers"]) for group in data["listings"])
            return f"{count} ponúk na {len(data['listings'])} inzerátoch"
        for key in ("results", "offers"):
            if key in data:
                items = data[key]
                titles = ", ".join(str(i.get("title") or i.get("specialist")) for i in items[:3])
                return f"{len(items)} – {titles}" if items else "nič (0)"
        if "title" in data:
            return f"{data['title']} · {data.get('budget', '')}"
        if "name" in data:
            return str(data["name"])
    if isinstance(data, list):
        return f"{len(data)} položiek"
    return json.dumps(data, ensure_ascii=False)[:120]
