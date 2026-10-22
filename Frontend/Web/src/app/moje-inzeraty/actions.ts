"use server";

import { refresh } from "next/cache";
import { authHeaders, client, problemTitle } from "@/lib/api";
import { getAccessToken } from "@/lib/auth";

/**
 * Stiahne inzerát (`POST /api/v1/listings/{id}/cancel`). Či ho smie stiahnuť, overí API –
 * iba autor alebo admin, a iba kým ešte neprebieha.
 */
export async function cancelListing(id: string): Promise<{ error?: string }> {
  const token = await getAccessToken();
  if (!token) return { error: "Prihlásenie vypršalo. Obnov stránku a prihlás sa znova." };

  try {
    const { data, error } = await client.POST("/api/v1/listings/{id}/cancel", {
      params: { path: { id } },
      headers: authHeaders(token),
    });
    if (!data) {
      // Stav sa medzitým zmenil (napr. stiahnutý v inej karte) – ukážeme aktuálny stav
      if ((error as { code?: unknown } | undefined)?.code === "listing.invalid_status_change") {
        refresh();
        return { error: "Inzerát sa medzitým zmenil, už sa nedá stiahnuť." };
      }
      return { error: problemTitle(error, "Inzerát sa nepodarilo stiahnuť.") };
    }
  } catch {
    return { error: "Teraz to nefunguje. Skús to o chvíľu znova." };
  }

  // Stránka sa načíta nanovo – inzerát bude mať stav „Stiahnutý“
  refresh();
  return {};
}
