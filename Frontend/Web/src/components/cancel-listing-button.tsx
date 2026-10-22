"use client";

import { useState, useTransition } from "react";
import { cancelListing } from "@/app/moje-inzeraty/actions";
import { cn } from "@/lib/utils";

/**
 * „Stiahnuť“ s potvrdením priamo na mieste (bez dialógu) – stiahnutie sa nedá vrátiť,
 * preto sa najprv opýta a predvolene je zamerané „Nie“.
 */
export function CancelListingButton({ listingId, title }: { listingId: string; title: string }) {
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState<string>();
  const [pending, startTransition] = useTransition();

  if (!confirming) {
    return (
      <button
        type="button"
        onClick={() => setConfirming(true)}
        aria-label={`Stiahnuť inzerát ${title}`}
        className={cn(buttonClass, "bg-background hover:bg-secondary")}
      >
        Stiahnuť
      </button>
    );
  }

  return (
    <div role="group" aria-label={`Stiahnuť inzerát ${title}?`} className="space-y-2">
      <p className="text-sm">
        <span className="font-medium">Naozaj stiahnuť?</span>{" "}
        <span className="text-muted-foreground">Zmizne z vyhľadávania, vrátiť sa to nedá.</span>
      </p>
      <div className="flex gap-2">
        <button
          type="button"
          aria-disabled={pending}
          onClick={() =>
            startTransition(async () => {
              const result = await cancelListing(listingId);
              setError(result.error);
            })
          }
          className={cn(
            buttonClass,
            "bg-destructive text-white hover:opacity-90",
            pending && "pointer-events-none opacity-60",
          )}
        >
          {pending ? "Sťahujem…" : "Áno, stiahnuť"}
        </button>
        <button
          type="button"
          autoFocus
          onClick={() => {
            setConfirming(false);
            setError(undefined);
          }}
          className={cn(buttonClass, "bg-background hover:bg-secondary")}
        >
          Nie
        </button>
      </div>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}

const buttonClass =
  "inline-flex h-9 items-center rounded-full px-4 text-sm font-medium transition-colors focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none";
