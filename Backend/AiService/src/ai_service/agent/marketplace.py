"""HTTP klienti systémov, na ktorých agent koná: C# API trhoviska a hybridné hľadanie AI služby.

Agent má presne tie práva ako prihlásený používateľ – volá verejné API s jeho tokenom,
takže kontroly oprávnení robí C# API (napr. ponuky na cudzí inzerát nevidí). Databázu nikdy nečíta.
"""

from typing import Any

import httpx2 as httpx


class ApiError(Exception):
    """Očakávaná chyba API (RFC 9457 ProblemDetails s kódom) – agent ju vráti modelu ako výsledok nástroja."""

    def __init__(self, status: int, code: str | None, message: str) -> None:
        super().__init__(f"{status} {code}: {message}")
        self.status = status
        self.code = code
        self.message = message

    def to_result(self) -> dict[str, Any]:
        return {"error": self.code or f"http_{self.status}", "status": self.status, "message": self.message}


def _raise_for_problem(response: httpx.Response) -> None:
    if response.is_success:
        return
    try:
        problem = response.json()
    except ValueError:
        problem = {}
    message = problem.get("detail") or problem.get("title") or response.reason_phrase
    errors = problem.get("errors")
    if isinstance(errors, dict):  # validačné chyby jednotlivých polí
        details = "; ".join(f"{field}: {' '.join(msgs)}" for field, msgs in errors.items())
        message = f"{message} ({details})"
    raise ApiError(response.status_code, problem.get("code"), str(message))


class MarketplaceClient:
    """C# API s tokenom prihláseného používateľa. Pri 401 raz obnoví token, potom to vzdá."""

    def __init__(self, base_url: str, timeout: float = 30.0, client: httpx.Client | None = None) -> None:
        self._client = client or httpx.Client(base_url=base_url, timeout=timeout)
        self._access_token: str | None = None
        self._refresh_token: str | None = None

    def login(self, login: str, password: str) -> None:
        response = self._client.post("/api/v1/auth/login", json={"login": login, "password": password})
        _raise_for_problem(response)
        self._store_tokens(response.json())

    def use_token(self, access_token: str) -> None:
        """Webový chat (neskôr): token používateľa sa iba preposiela, nikde sa neukladá."""
        self._access_token, self._refresh_token = access_token, None

    def _store_tokens(self, body: dict[str, Any]) -> None:
        self._access_token = body["accessToken"]
        self._refresh_token = body.get("refreshToken")

    def _refresh(self) -> bool:
        if not self._refresh_token:
            return False
        response = self._client.post("/api/v1/auth/refresh", json={"refreshToken": self._refresh_token})
        if not response.is_success:
            return False
        self._store_tokens(response.json())
        return True

    def request(self, method: str, path: str, **kwargs: Any) -> Any:
        response = self._send(method, path, **kwargs)
        if response.status_code == 401 and self._refresh():
            response = self._send(method, path, **kwargs)  # jeden pokus po obnovení tokenu, nie viac
        _raise_for_problem(response)
        return response.json() if response.content else None

    def _send(self, method: str, path: str, **kwargs: Any) -> httpx.Response:
        headers = {"Authorization": f"Bearer {self._access_token}"} if self._access_token else {}
        try:
            return self._client.request(method, path, headers=headers, **kwargs)
        except httpx.TimeoutException as error:
            raise ApiError(504, "agent.timeout", "API neodpovedalo včas.") from error
        except httpx.HTTPError as error:
            raise ApiError(503, "agent.api_unavailable", f"API nie je dostupné: {error}") from error

    def get(self, path: str, **params: Any) -> Any:
        clean = {k: v for k, v in params.items() if v is not None}
        return self.request("GET", path, params=clean)

    def post(self, path: str, body: dict[str, Any] | None = None) -> Any:
        return self.request("POST", path, json=body)


class SearchClient:
    """Hybridné hľadanie AI služby (GET /search) – prázdny zoznam znamená „nič relevantné“."""

    def __init__(self, base_url: str, timeout: float = 30.0, client: httpx.Client | None = None) -> None:
        self._client = client or httpx.Client(base_url=base_url, timeout=timeout)

    def search(self, query: str, limit: int) -> list[dict[str, Any]]:
        try:
            response = self._client.get("/search", params={"q": query, "limit": limit})
        except httpx.HTTPError as error:
            raise ApiError(503, "agent.search_unavailable", f"Hľadanie nie je dostupné: {error}") from error
        _raise_for_problem(response)
        return response.json()
