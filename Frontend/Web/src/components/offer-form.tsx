"use client";

import { useRef, useState, useTransition } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { sendOffer } from "@/app/inzeraty/[id]/ponuka/actions";
import {
  emptyOfferValues,
  offerLimits,
  validateOffer,
  type OfferField,
  type OfferFieldErrors,
  type OfferFormValues,
} from "@/lib/offers";
import { cn } from "@/lib/utils";

const fieldId = (field: OfferField) => `ponuka-${field}`;

/**
 * Ponuka odborníka: cena, termín dodania a správa pre zadávateľa. Chyby sa ukážu po odoslaní
 * a potom sa priebežne aktualizujú pri písaní (rovnako ako formulár inzerátu).
 */
export function OfferForm({ listingId, cancelHref }: { listingId: string; cancelHref: string }) {
  const router = useRouter();
  const [values, setValues] = useState<OfferFormValues>(emptyOfferValues);
  const [submitted, setSubmitted] = useState(false);
  const [serverErrors, setServerErrors] = useState<OfferFieldErrors>({});
  const [error, setError] = useState<string>();
  const [pending, startTransition] = useTransition();
  const formRef = useRef<HTMLFormElement>(null);

  const errors: OfferFieldErrors = submitted ? { ...validateOffer(values), ...serverErrors } : {};

  function update(field: OfferField, value: string) {
    setValues((current) => ({ ...current, [field]: value }));
    setServerErrors((current) => ({ ...current, [field]: undefined }));
  }

  function focusFirstError(fieldErrors: OfferFieldErrors) {
    const first = (["price", "deliveryDays", "message"] as const).find((f) => fieldErrors[f]);
    if (first) formRef.current?.querySelector<HTMLElement>(`#${fieldId(first)}`)?.focus();
  }

  function submit(event: React.FormEvent) {
    event.preventDefault();
    setSubmitted(true);
    setError(undefined);
    const local = validateOffer(values);
    if (Object.keys(local).length > 0) {
      focusFirstError(local);
      return;
    }
    startTransition(async () => {
      const result = await sendOffer(listingId, values);
      if (result.sent) {
        router.push("/moje-ponuky");
        return;
      }
      setServerErrors(result.fieldErrors ?? {});
      setError(result.error);
      if (result.fieldErrors) focusFirstError(result.fieldErrors);
    });
  }

  return (
    <form ref={formRef} onSubmit={submit} noValidate className="space-y-6">
      <div className="grid gap-6 sm:grid-cols-2">
        <div>
          <label htmlFor={fieldId("price")} className={labelClass}>
            Cena
          </label>
          <div className="relative">
            <input
              id={fieldId("price")}
              inputMode="decimal"
              autoComplete="off"
              placeholder="napr. 250"
              value={values.price}
              onChange={(e) => update("price", e.target.value)}
              aria-invalid={!!errors.price}
              className={cn(inputClass, "pr-10")}
            />
            <span className="pointer-events-none absolute top-1/2 right-4 -translate-y-1/2 text-muted-foreground">
              €
            </span>
          </div>
          <FieldError message={errors.price} />
        </div>
        <div>
          <label htmlFor={fieldId("deliveryDays")} className={labelClass}>
            Dodám do
          </label>
          <div className="relative">
            <input
              id={fieldId("deliveryDays")}
              inputMode="numeric"
              autoComplete="off"
              placeholder="napr. 14"
              value={values.deliveryDays}
              onChange={(e) => update("deliveryDays", e.target.value)}
              aria-invalid={!!errors.deliveryDays}
              className={cn(inputClass, "pr-14")}
            />
            <span className="pointer-events-none absolute top-1/2 right-4 -translate-y-1/2 text-muted-foreground">
              dní
            </span>
          </div>
          <FieldError message={errors.deliveryDays} />
        </div>
      </div>

      <div>
        <label htmlFor={fieldId("message")} className={labelClass}>
          Správa pre zadávateľa
        </label>
        <textarea
          id={fieldId("message")}
          rows={7}
          placeholder="Prečo práve ty, čo dodáš a ako budeš postupovať."
          value={values.message}
          onChange={(e) => update("message", e.target.value)}
          aria-invalid={!!errors.message}
          aria-describedby="ponuka-message-hint"
          className={cn(inputClass, "h-auto resize-y py-3 leading-relaxed")}
        />
        <p id="ponuka-message-hint" className="mt-1.5 px-1 text-xs text-muted-foreground">
          {values.message.trim().length} / {offerLimits.maxMessage} znakov
        </p>
        <FieldError message={errors.message} />
      </div>

      {error && (
        <p
          role="alert"
          className="rounded-2xl bg-destructive/10 px-4 py-3 text-sm text-destructive"
        >
          {error}
        </p>
      )}

      <div className="flex flex-wrap items-center gap-3">
        <button
          type="submit"
          aria-disabled={pending}
          className={cn(
            buttonClass,
            "bg-primary text-primary-foreground hover:opacity-90",
            pending && "pointer-events-none opacity-60",
          )}
        >
          {pending ? "Posielam…" : "Poslať ponuku"}
        </button>
        <Link href={cancelHref} className={cn(buttonClass, "bg-surface hover:bg-secondary")}>
          Zrušiť
        </Link>
      </div>
    </form>
  );
}

function FieldError({ message }: { message?: string }) {
  if (!message) return null;
  return <p className="mt-1.5 px-1 text-sm text-destructive">{message}</p>;
}

const labelClass = "mb-2 block px-1 text-sm font-medium";
const inputClass =
  "h-12 w-full rounded-2xl bg-surface px-4 text-base outline-none placeholder:text-muted-foreground focus-visible:ring-3 focus-visible:ring-ring/40 aria-invalid:ring-2 aria-invalid:ring-destructive/60";
const buttonClass =
  "inline-flex h-11 items-center justify-center rounded-full px-6 text-base font-medium transition-[opacity,background-color]";
