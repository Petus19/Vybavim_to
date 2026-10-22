"use server";

import { authHeaders, client, problemTitle } from "@/lib/api";
import { getAccessToken } from "@/lib/auth";
import {
  fieldByErrorCode,
  toListingInput,
  validateListing,
  type FieldErrors,
  type ListingFormValues,
} from "@/lib/listing-form";

export type SaveListingResult = { savedId?: string; error?: string; fieldErrors?: FieldErrors };

/**
 * Pridá nový inzerát (`listingId` je `null`) alebo upraví existujúci.
 * Pravidlá overí ešte raz tu a nakoniec API – hodnoty z prehliadača sa nedajú brať ako overené.
 */
export async function saveListing(
  listingId: string | null,
  values: ListingFormValues,
): Promise<SaveListingResult> {
  const fieldErrors = validateListing(values);
  if (Object.keys(fieldErrors).length > 0) return { fieldErrors };

  const token = await getAccessToken();
  if (!token) return { error: "Prihlásenie vypršalo. Obnov stránku a prihlás sa znova." };

  const body = toListingInput(values);
  const headers = authHeaders(token);
  try {
    const { data, error } = listingId
      ? await client.PUT("/api/v1/listings/{id}", {
          params: { path: { id: listingId } },
          body,
          headers,
        })
      : await client.POST("/api/v1/listings", { body, headers });
    if (data) return { savedId: data.id };

    // Chyba pravidla (napr. listing.title_too_long) sa zobrazí priamo pri poli
    const code = (error as { code?: unknown } | undefined)?.code;
    const field = typeof code === "string" ? fieldByErrorCode[code] : undefined;
    const message = problemTitle(error, "Inzerát sa nepodarilo uložiť.");
    return field ? { fieldErrors: { [field]: message } } : { error: message };
  } catch {
    return { error: "Teraz to nefunguje. Skús to o chvíľu znova." };
  }
}
