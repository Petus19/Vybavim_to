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

const baseUrl = process.env.API_URL ?? "http://localhost:5080";
const client = createClient<paths>({ baseUrl });

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
    const title = (error as { title?: string } | undefined)?.title;
    return { ok: false, message: title ?? "Neplatný filter." };
  }
  throw new ApiUnavailableError(`HTTP ${response.status}`);
}

const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** Detail inzerátu alebo `null`, ak neexistuje. `cache` – stránka aj metadáta volajú API raz. */
export const getListing = cache(async (id: string): Promise<Listing | null> => {
  if (!uuidPattern.test(id)) return null;
  const { data, response } = await call(() =>
    client.GET("/api/v1/listings/{id}", { params: { path: { id } } }),
  );
  if (data) return data;
  if (response.status === 404) return null;
  throw new ApiUnavailableError(`HTTP ${response.status}`);
});
