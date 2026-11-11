"use server";

import { refresh } from "next/cache";
import { authHeaders, client, problemTitle } from "@/lib/api";
import { getAccessToken } from "@/lib/auth";

type Decision = "accept" | "reject" | "withdraw";

const failures: Record<Decision, string> = {
  accept: "Ponuku sa nepodarilo prijať.",
  reject: "Ponuku sa nepodarilo odmietnuť.",
  withdraw: "Ponuku sa nepodarilo stiahnuť.",
};

/**
 * Rozhodnutie o ponuke: autor inzerátu ju prijme alebo odmietne, odborník ju stiahne.
 * Kto čo smie, overí API (`POST /api/v1/offers/{id}/accept|reject|withdraw`).
 * Prijatie zároveň odmietne ostatné čakajúce ponuky a inzerát prejde do stavu „Prebieha“.
 */
export async function decideOffer(
  offerId: string,
  decision: Decision,
): Promise<{ error?: string }> {
  const token = await getAccessToken();
  if (!token) return { error: "Prihlásenie vypršalo. Obnov stránku a prihlás sa znova." };

  const options = { params: { path: { id: offerId } }, headers: authHeaders(token) };
  try {
    const { data, error } =
      decision === "accept"
        ? await client.POST("/api/v1/offers/{id}/accept", options)
        : decision === "reject"
          ? await client.POST("/api/v1/offers/{id}/reject", options)
          : await client.POST("/api/v1/offers/{id}/withdraw", options);
    if (!data) {
      // Ponuka sa medzitým zmenila (napr. iná bola prijatá) – ukážeme aktuálny stav
      if ((error as { code?: unknown } | undefined)?.code === "offer.invalid_status_change") {
        refresh();
        return { error: "Ponuka sa medzitým zmenila." };
      }
      return { error: problemTitle(error, failures[decision]) };
    }
  } catch {
    return { error: "Teraz to nefunguje. Skús to o chvíľu znova." };
  }

  refresh();
  return {};
}
