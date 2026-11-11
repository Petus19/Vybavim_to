import type { OfferInput, OfferStatus } from "@/lib/api";

/**
 * Ponuka odborníka na inzerát – pravidlá formulára a texty.
 * Limity zodpovedajú backendu (`Offer.Create` v module Projects): API ich overí znova.
 */

export const offerLimits = {
  maxPrice: 1_000_000,
  maxDeliveryDays: 365,
  maxMessage: 2000,
} as const;

export const offerStatusLabels: Record<OfferStatus, string> = {
  pending: "Čaká na rozhodnutie",
  accepted: "Prijatá",
  rejected: "Odmietnutá",
  withdrawn: "Stiahnutá",
};

/** Hodnoty z formulára – ako text, presne ako ich používateľ napísal. */
export type OfferFormValues = { price: string; deliveryDays: string; message: string };
export type OfferField = keyof OfferFormValues;
export type OfferFieldErrors = Partial<Record<OfferField, string>>;

export const emptyOfferValues: OfferFormValues = { price: "", deliveryDays: "", message: "" };

/** „1 250,50“ alebo „1250.5“ → 1250.5 (slovenská desatinná čiarka aj medzery v tisícoch). */
export function parseAmount(value: string): number {
  const normalized = value.replace(/\s/g, "").replace(",", ".");
  return normalized === "" ? Number.NaN : Number(normalized);
}

export function validateOffer(values: OfferFormValues): OfferFieldErrors {
  const errors: OfferFieldErrors = {};
  const price = parseAmount(values.price);
  if (!Number.isFinite(price) || price <= 0) {
    errors.price = "Zadaj cenu väčšiu ako 0 €.";
  } else if (price > offerLimits.maxPrice) {
    errors.price = "Cena môže byť najviac 1 000 000 €.";
  } else if (Math.round(price * 100) !== price * 100) {
    errors.price = "Cena môže mať najviac dve desatinné miesta.";
  }

  const days = Number(values.deliveryDays);
  if (!Number.isInteger(days) || days < 1 || days > offerLimits.maxDeliveryDays) {
    errors.deliveryDays = `Zadaj počet dní od 1 do ${offerLimits.maxDeliveryDays}.`;
  }

  const message = values.message.trim();
  if (message.length === 0) {
    errors.message = "Napíš zadávateľovi pár viet – prečo práve ty a ako to urobíš.";
  } else if (message.length > offerLimits.maxMessage) {
    errors.message = `Správa môže mať najviac ${offerLimits.maxMessage} znakov.`;
  }
  return errors;
}

export function toOfferInput(values: OfferFormValues): OfferInput {
  return {
    price: parseAmount(values.price),
    deliveryDays: Number(values.deliveryDays),
    message: values.message.trim(),
  };
}

/** Chyby API, ktoré patria ku konkrétnemu poľu – zobrazia sa priamo pri ňom. */
export const offerFieldByErrorCode: Record<string, OfferField> = {
  "offer.invalid_price": "price",
  "offer.invalid_delivery_days": "deliveryDays",
  "offer.message_required": "message",
  "offer.message_too_long": "message",
};

const euroWithCents = new Intl.NumberFormat("sk-SK", {
  style: "currency",
  currency: "EUR",
  minimumFractionDigits: 0, // „280 €“, ale „280,50 €“
  maximumFractionDigits: 2,
});

export function formatPrice(price: number): string {
  return euroWithCents.format(price);
}
