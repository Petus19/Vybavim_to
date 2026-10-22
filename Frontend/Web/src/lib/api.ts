import { cache } from "react";
import { connection } from "next/server";
import createClient from "openapi-fetch";
import type { components, paths } from "@shared/api/schema";

/**
 * Klient pre backendové API (Backend/Api, predvolene http://localhost:5080).
 *
 * Typy sú vygenerované z OpenAPI do Frontend/Shared/api/schema.d.ts
 * (`npm run api:types`), takže TypeScript skontroluje adresy, parametre aj tvar odpovedí.
 *
 * Volá sa iba zo servera (Server Components), nie z prehliadača – adresa API
 * sa preto nastavuje cez API_URL bez prefixu NEXT_PUBLIC_.
 */

type Schemas = components["schemas"];

export type Category = Schemas["CategoryResponse"];
export type ListingSummary = Schemas["ListingSummaryResponse"];
export type Listing = Schemas["ListingDetailResponse"];
export type Budget = Schemas["BudgetResponse"];
export type BudgetType = Schemas["BudgetType"];
export type ExperienceLevel = Schemas["ExperienceLevel"];
export type Engagement = Schemas["Engagement"];
export type ProjectSize = Schemas["ProjectSize"];
export type ListingStatus = Schemas["ListingStatus"];
export type ListingPage = Schemas["PagedResponseOfListingSummaryResponse"];

/** Parametre vyhľadávania presne podľa API (`GET /api/v1/listings`). */
export type ListingSearchQuery = NonNullable<
  paths["/api/v1/listings"]["get"]["parameters"]["query"]
>;

export type User = Schemas["UserResponse"];
export type AccessTokens = Schemas["AccessTokenResponse"];

export const baseUrl = process.env.API_URL ?? "http://localhost:5080";

/** Typovaný klient – používa ho aj `src/lib/auth.ts` pre prihlásenie. */
export const client = createClient<paths>({ baseUrl });

/** Hlavička pre chránené endpointy. Token je v httpOnly cookie – pozri `getAccessToken` v `src/lib/auth.ts`. */
export function authHeaders(token: string) {
  return { Authorization: `Bearer ${token}` };
}

/** Chybová hláška z API (ProblemDetails `title`) – API ich vracia po slovensky. */
export function problemTitle(error: unknown, fallback: string): string {
  const title = (error as { title?: unknown } | undefined)?.title;
  return typeof title === "string" && title ? title : fallback;
}

/** API nebeží alebo vrátilo neočakávanú chybu – zobrazí sa chybová stránka (app/error.tsx). */
export class ApiUnavailableError extends Error {
  constructor(cause?: unknown) {
    super(`API na adrese ${baseUrl} neodpovedá alebo vrátilo chybu.`, { cause });
    this.name = "ApiUnavailableError";
  }
}

/**
 * Pred každým volaním `connection()`: dáta sa načítajú až pri požiadavke používateľa,
 * nie pri `next build`. Build (aj v CI) tak nepotrebuje bežiace API.
 */
async function call<T>(request: () => Promise<T>): Promise<T> {
  await connection();
  try {
    return await request();
  } catch (error) {
    throw new ApiUnavailableError(error);
  }
}

/** Kategórie. `cache` zabezpečí, že sa počas jednej požiadavky načítajú iba raz. */
export const getCategories = cache(async (): Promise<Category[]> => {
  const { data, response } = await call(() => client.GET("/api/v1/categories"));
  if (!data) throw new ApiUnavailableError(`HTTP ${response.status}`);
  return data;
});

export type SearchResult = { ok: true; page: ListingPage } | { ok: false; message: string };

/**
 * Vyhľadávanie inzerátov. Neplatný filter (400, napr. cena „od“ väčšia ako „do“)
 * vráti správu pre používateľa namiesto chyby celej stránky.
 */
export async function searchListings(query: ListingSearchQuery): Promise<SearchResult> {
  const { data, error, response } = await call(() =>
    client.GET("/api/v1/listings", { params: { query } }),
  );
  if (data) return { ok: true, page: data };
  if (response.status === 400) {
    return { ok: false, message: problemTitle(error, "Neplatný filter.") };
  }
  throw new ApiUnavailableError(`HTTP ${response.status}`);
}

const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Detail inzerátu alebo `null`, ak neexistuje. `cache` – stránka aj metadáta volajú API raz.
 * S tokenom autora vráti aj jeho stiahnutý inzerát (ostatní dostanú 404).
 */
export const getListing = cache(
  async (id: string, token: string | null): Promise<Listing | null> => {
    if (!uuidPattern.test(id)) return null;
    const { data, response } = await call(() =>
      client.GET("/api/v1/listings/{id}", {
        params: { path: { id } },
        headers: token ? authHeaders(token) : undefined,
      }),
    );
    if (data) return data;
    if (response.status === 404) return null;
    throw new ApiUnavailableError(`HTTP ${response.status}`);
  },
);

export const MY_LISTINGS_PAGE_SIZE = 20;

/** Inzeráty prihláseného používateľa vo všetkých stavoch, najnovšie prvé. */
export async function getMyListings(token: string, page: number): Promise<ListingPage> {
  const { data, response } = await call(() =>
    client.GET("/api/v1/listings/mine", {
      params: { query: { page, pageSize: MY_LISTINGS_PAGE_SIZE } },
      headers: authHeaders(token),
    }),
  );
  if (data) return data;
  throw new ApiUnavailableError(`HTTP ${response.status}`);
}
