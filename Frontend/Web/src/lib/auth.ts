import { cache } from "react";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { ApiUnavailableError, client, type AccessTokens, type User } from "@/lib/api";
import {
  ACCESS_COOKIE,
  REFRESH_COOKIE,
  REFRESH_MAX_AGE,
  accessMaxAge,
  tokenCookieOptions,
} from "@/lib/auth-cookies";

/**
 * Prihlásenie na webe (Data Access Layer – všetko o prihlásení je na jednom mieste).
 *
 * Tokeny z API (`POST /api/v1/auth/login`) ukladáme do httpOnly cookies webu:
 * JavaScript v prehliadači sa k nim nedostane a API volá iba server webu.
 * - access token  – platí 1 hodinu, posiela sa ako `Authorization: Bearer …`
 * - refresh token – platí 14 dní, keď access token vyprší, `src/proxy.ts` si vypýta nový
 */

/** Uloží tokeny do cookies (iba v Server Action – počas vykresľovania sa cookies meniť nedajú). */
export async function saveTokens(tokens: AccessTokens) {
  const store = await cookies();
  store.set(ACCESS_COOKIE, tokens.accessToken, tokenCookieOptions(accessMaxAge(tokens.expiresIn)));
  store.set(REFRESH_COOKIE, tokens.refreshToken, tokenCookieOptions(REFRESH_MAX_AGE));
}

export async function clearTokens() {
  const store = await cookies();
  store.delete(ACCESS_COOKIE);
  store.delete(REFRESH_COOKIE);
}

/**
 * Prihlásený používateľ alebo `null`. Číta cookie, preto beží iba pri požiadavke
 * a komponent, ktorý ho volá, musí byť v `<Suspense>`.
 */
export const getCurrentUser = cache(async (): Promise<User | null> => {
  const token = (await cookies()).get(ACCESS_COOKIE)?.value;
  if (!token) return null;

  try {
    const { data, response } = await client.GET("/api/v1/auth/me", {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (data) return data;
    if (response.status === 401) return null; // token neplatný alebo vypršal
    throw new ApiUnavailableError(`HTTP ${response.status}`);
  } catch (error) {
    if (error instanceof ApiUnavailableError) throw error;
    throw new ApiUnavailableError(error);
  }
});

/**
 * Stránky, kam sa dá ísť iba prihlásený (poslať ponuku, pridať inzerát…).
 * Neprihláseného presmeruje na prihlásenie a po ňom ho vráti späť na `returnTo`.
 */
export async function requireUser(returnTo: string): Promise<User> {
  const user = await getCurrentUser();
  if (!user) redirect(loginHref(returnTo));
  return user;
}

/** Odkaz na prihlásenie s návratom na danú stránku. */
export function loginHref(returnTo?: string) {
  const path = safeReturnPath(returnTo);
  return path === "/" ? "/prihlasenie" : `/prihlasenie?navrat=${encodeURIComponent(path)}`;
}

/**
 * Návrat po prihlásení smie byť iba stránka tohto webu („/inzeraty/…“),
 * nie cudzia adresa – inak by sa dal odkaz na prihlásenie zneužiť na presmerovanie na podvodný web.
 */
export function safeReturnPath(value: unknown): string {
  if (typeof value !== "string") return "/";
  if (!value.startsWith("/") || value.startsWith("//") || value.startsWith("/\\")) return "/";
  return value;
}
