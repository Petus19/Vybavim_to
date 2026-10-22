import { NextResponse, type NextRequest } from "next/server";
import {
  ACCESS_COOKIE,
  REFRESH_COOKIE,
  REFRESH_MAX_AGE,
  accessMaxAge,
  tokenCookieOptions,
} from "@/lib/auth-cookies";

/**
 * Obnovenie prihlásenia. Access token platí hodinu – keď jeho cookie vyprší, ale refresh
 * token ešte platí, vypýtame si od API nové tokeny skôr, než sa stránka začne vykresľovať.
 * Používateľ tak zostane prihlásený až 14 dní bez opätovného zadávania hesla.
 *
 * (Cookies sa počas vykresľovania stránky meniť nedajú, preto to robí proxy.)
 */
export async function proxy(request: NextRequest) {
  const refreshToken = request.cookies.get(REFRESH_COOKIE)?.value;
  if (request.cookies.has(ACCESS_COOKIE) || !refreshToken) {
    return NextResponse.next();
  }

  const apiUrl = process.env.API_URL ?? "http://localhost:5080";
  let tokens: { accessToken: string; refreshToken: string; expiresIn: number } | null = null;
  try {
    const response = await fetch(`${apiUrl}/api/v1/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken }),
    });
    if (response.ok) tokens = await response.json();
  } catch {
    // API nebeží – stránka sa vykreslí ako pre neprihláseného, refresh token zostane na neskôr
    return NextResponse.next();
  }

  if (!tokens) {
    // Refresh token je neplatný alebo vypršal – zahodíme ho, treba sa prihlásiť znova
    const response = NextResponse.next();
    response.cookies.delete(REFRESH_COOKIE);
    return response;
  }

  // Nový token dostane aj práve vykresľovaná stránka (cookie v požiadavke) …
  request.cookies.set(ACCESS_COOKIE, tokens.accessToken);
  const response = NextResponse.next({ request: { headers: request.headers } });
  // … aj prehliadač pre ďalšie požiadavky (cookie v odpovedi)
  response.cookies.set(
    ACCESS_COOKIE,
    tokens.accessToken,
    tokenCookieOptions(accessMaxAge(tokens.expiresIn)),
  );
  response.cookies.set(REFRESH_COOKIE, tokens.refreshToken, tokenCookieOptions(REFRESH_MAX_AGE));
  return response;
}

export const config = {
  // Iba stránky – nie statické súbory, obrázky a favicon
  matcher: ["/((?!_next/static|_next/image|icon.svg|favicon.ico).*)"],
};
