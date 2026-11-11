"use server";

import { authHeaders, client, problemTitle } from "@/lib/api";
import { getAccessToken } from "@/lib/auth";
import {
  offerFieldByErrorCode,
  toOfferInput,
  validateOffer,
  type OfferFieldErrors,
  type OfferFormValues,
} from "@/lib/offers";

export type SendOfferResult = { sent?: boolean; error?: string; fieldErrors?: OfferFieldErrors };

/**
 * Pošle ponuku na inzerát (`POST /api/v1/listings/{id}/offers`). Pravidlá overí ešte raz tu
 * a nakoniec API – napr. že inzerát je otvorený, nie je môj a ponuku naň ešte nemám.
 */
export async function sendOffer(
  listingId: string,
  values: OfferFormValues,
): Promise<SendOfferResult> {
  const fieldErrors = validateOffer(values);
  if (Object.keys(fieldErrors).length > 0) return { fieldErrors };

  const token = await getAccessToken();
  if (!token) return { error: "Prihlásenie vypršalo. Obnov stránku a prihlás sa znova." };

  try {
    const { data, error } = await client.POST("/api/v1/listings/{listingId}/offers", {
      params: { path: { listingId } },
      body: toOfferInput(values),
      headers: authHeaders(token),
    });
    if (data) return { sent: true };

    const code = (error as { code?: unknown } | undefined)?.code;
    const field = typeof code === "string" ? offerFieldByErrorCode[code] : undefined;
    const message = problemTitle(error, "Ponuku sa nepodarilo poslať.");
    return field ? { fieldErrors: { [field]: message } } : { error: message };
  } catch {
    return { error: "Teraz to nefunguje. Skús to o chvíľu znova." };
  }
}
