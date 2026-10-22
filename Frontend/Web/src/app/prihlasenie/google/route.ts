import { NextResponse, type NextRequest } from "next/server";
import { safeReturnPath } from "@/lib/auth";
import { tokenCookieOptions } from "@/lib/auth-cookies";
import {
  GOOGLE_STATE_COOKIE,
  createOAuthSecrets,
  googleConfig,
  googleRedirectUri,
} from "@/lib/google";

/** Krok 1: presmerovanie na Google. `?navrat=` sa zapamätá a použije po prihlásení. */
export async function GET(request: NextRequest) {
  const navrat = safeReturnPath(request.nextUrl.searchParams.get("navrat"));
  const config = googleConfig();
  if (!config) {
    return NextResponse.redirect(new URL("/prihlasenie?chyba=google", request.url));
  }

  const { state, verifier, challenge } = createOAuthSecrets();

  const google = new URL("https://accounts.google.com/o/oauth2/v2/auth");
  google.search = new URLSearchParams({
    client_id: config.clientId,
    redirect_uri: googleRedirectUri(request.nextUrl.origin),
    response_type: "code",
    scope: "openid email profile",
    state,
    code_challenge: challenge,
    code_challenge_method: "S256",
    prompt: "select_account", // vždy ukázať výber účtu (dôležité pri viacerých Google účtoch)
  }).toString();

  const response = NextResponse.redirect(google);
  // Hodnoty na overenie odpovede od Google – platia 10 minút a iba pre tento prehliadač
  response.cookies.set(
    GOOGLE_STATE_COOKIE,
    JSON.stringify({ state, verifier, navrat }),
    tokenCookieOptions(10 * 60),
  );
  return response;
}
