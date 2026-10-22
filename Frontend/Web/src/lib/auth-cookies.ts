/**
 * Názvy a nastavenia cookies s tokenmi. Samostatný súbor bez závislostí,
 * aby ho mohol použiť aj `src/proxy.ts` (beží pred vykreslením stránky).
 */

export const ACCESS_COOKIE = "vybavim_access";
export const REFRESH_COOKIE = "vybavim_refresh";

/** Refresh token platí v API 14 dní. */
export const REFRESH_MAX_AGE = 14 * 24 * 60 * 60;

/** Access cookie vyprší o minútu skôr ako token, aby sa včas obnovil. */
export function accessMaxAge(expiresIn: number) {
  return Math.max(60, expiresIn - 60);
}

/** httpOnly – JavaScript v prehliadači sa k tokenom nedostane; secure – v produkcii iba HTTPS. */
export function tokenCookieOptions(maxAge: number) {
  return {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax" as const,
    path: "/",
    maxAge,
  };
}
