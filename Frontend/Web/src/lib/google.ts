import { createHash, randomBytes } from "node:crypto";

/**
 * Prihlásenie cez Google (OAuth 2.0, „authorization code“ s PKCE).
 *
 * 1. /prihlasenie/google            → presmeruje na Google (výber účtu)
 * 2. /prihlasenie/google/callback   → Google vráti kód, server ho vymení za ID token,
 *                                     pošle ho do API (POST /api/v1/auth/google) a uloží naše tokeny
 *
 * Client secret je iba na serveri webu (.env.local), nikdy v prehliadači ani v gite.
 */

export const GOOGLE_STATE_COOKIE = "vybavim_google_oauth";

export function googleConfig() {
  const clientId = process.env.GOOGLE_CLIENT_ID;
  const clientSecret = process.env.GOOGLE_CLIENT_SECRET;
  return clientId && clientSecret ? { clientId, clientSecret } : null;
}

/** Tlačidlo „Pokračovať s Google“ sa zobrazí iba vtedy, keď sú údaje v .env.local vyplnené. */
export function isGoogleEnabled() {
  return googleConfig() !== null;
}

/** Adresa, kam Google vráti používateľa – musí presne sedieť s „Authorized redirect URI“ v Google Cloud. */
export function googleRedirectUri(origin: string) {
  return `${origin}/prihlasenie/google/callback`;
}

/**
 * Jednorazové náhodné hodnoty pre jedno prihlásenie:
 * - state – Google ho vráti späť; overíme, že odpoveď patrí k prihláseniu, ktoré začal tento prehliadač
 * - verifier/challenge (PKCE) – kód od Google sa dá vymeniť za token iba s pôvodným verifierom
 */
export function createOAuthSecrets() {
  const state = randomBytes(16).toString("base64url");
  const verifier = randomBytes(32).toString("base64url");
  const challenge = createHash("sha256").update(verifier).digest("base64url");
  return { state, verifier, challenge };
}
