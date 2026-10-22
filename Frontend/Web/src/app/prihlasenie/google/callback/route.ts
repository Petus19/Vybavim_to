import { NextResponse, type NextRequest } from "next/server";
import { client } from "@/lib/api";
import { safeReturnPath } from "@/lib/auth";
import {
  ACCESS_COOKIE,
  REFRESH_COOKIE,
  REFRESH_MAX_AGE,
  accessMaxAge,
  tokenCookieOptions,
} from "@/lib/auth-cookies";
import { GOOGLE_STATE_COOKIE, googleConfig, googleRedirectUri } from "@/lib/google";

type SavedState = { state: string; verifier: string; navrat: string };

/** Krok 2: Google vrátil používateľa s kódom → vymeníme ho za ID token → API nám dá naše tokeny. */
export async function GET(request: NextRequest) {
  const params = request.nextUrl.searchParams;
  const saved = readSavedState(request);
  const navrat = safeReturnPath(saved?.navrat);

  const fail = (reason: string) => {
    console.warn(`Prihlásenie cez Google zlyhalo: ${reason}`);
    const url = new URL("/prihlasenie", request.url);
    url.searchParams.set("chyba", "google");
    if (navrat !== "/") url.searchParams.set("navrat", navrat);
    const response = NextResponse.redirect(url);
    response.cookies.delete(GOOGLE_STATE_COOKIE);
    return response;
  };

  // Používateľ na Google klikol „Zrušiť“ – vrátime ho na prihlásenie bez chybovej hlášky
  if (params.get("error") === "access_denied") {
    const url = new URL("/prihlasenie", request.url);
    if (navrat !== "/") url.searchParams.set("navrat", navrat);
    return NextResponse.redirect(url);
  }

  const config = googleConfig();
  const code = params.get("code");
  if (!config) return fail("chýba GOOGLE_CLIENT_ID alebo GOOGLE_CLIENT_SECRET v .env.local");
  if (!saved || !code) return fail("chýba kód alebo uložený stav (vypršal po 10 minútach?)");
  // Odpoveď musí patriť k prihláseniu, ktoré začal tento prehliadač (ochrana proti podvrhnutiu)
  if (params.get("state") !== saved.state) return fail("nesedí state");

  // Výmena kódu za tokeny od Google – iba so serverovým Client secret a pôvodným PKCE verifierom
  let idToken: string | undefined;
  try {
    const tokenResponse = await fetch("https://oauth2.googleapis.com/token", {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: new URLSearchParams({
        code,
        client_id: config.clientId,
        client_secret: config.clientSecret,
        redirect_uri: googleRedirectUri(request.nextUrl.origin),
        grant_type: "authorization_code",
        code_verifier: saved.verifier,
      }),
    });
    const body = (await tokenResponse.json()) as { id_token?: string; error?: string };
    if (!tokenResponse.ok)
      return fail(`Google token endpoint: ${body.error ?? tokenResponse.status}`);
    idToken = body.id_token;
  } catch (error) {
    return fail(`Google nedostupný: ${String(error)}`);
  }
  if (!idToken) return fail("Google nevrátil id_token");

  // API overí ID token a vráti rovnaké tokeny ako pri prihlásení heslom
  let tokens;
  try {
    const { data, error } = await client.POST("/api/v1/auth/google", { body: { idToken } });
    if (!data) return fail(`API: ${JSON.stringify(error)}`);
    tokens = data;
  } catch (error) {
    return fail(`API nedostupné: ${String(error)}`);
  }

  const response = NextResponse.redirect(new URL(navrat, request.url));
  response.cookies.delete(GOOGLE_STATE_COOKIE);
  response.cookies.set(
    ACCESS_COOKIE,
    tokens.accessToken,
    tokenCookieOptions(accessMaxAge(tokens.expiresIn)),
  );
  response.cookies.set(REFRESH_COOKIE, tokens.refreshToken, tokenCookieOptions(REFRESH_MAX_AGE));
  return response;
}

function readSavedState(request: NextRequest): SavedState | null {
  const raw = request.cookies.get(GOOGLE_STATE_COOKIE)?.value;
  if (!raw) return null;
  try {
    const value = JSON.parse(raw) as Partial<SavedState>;
    return value.state && value.verifier ? (value as SavedState) : null;
  } catch {
    return null;
  }
}
