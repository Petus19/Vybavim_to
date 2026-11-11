"use client";

import { useState, useTransition } from "react";
import { cn } from "@/lib/utils";

/**
 * Tlačidlo, ktoré sa najprv opýta priamo na mieste (bez dialógu) – pre kroky, ktoré sa nedajú
 * vrátiť (prijať či odmietnuť ponuku, stiahnuť ju). Predvolene je zamerané „Nie“.
 */
export function ConfirmActionButton({
  label,
  question,
  consequence,
  confirmLabel,
  pendingLabel,
  ariaLabel,
  tone = "neutral",
  action,
}: {
  label: string;
  question: string;
  consequence: string;
  confirmLabel: string;
  pendingLabel: string;
  ariaLabel: string;
  tone?: "primary" | "neutral" | "destructive";
  action: () => Promise<{ error?: string }>;
}) {
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState<string>();
  const [pending, startTransition] = useTransition();

  if (!confirming) {
    return (
      <button
        type="button"
        onClick={() => setConfirming(true)}
        aria-label={ariaLabel}
        className={cn(
          buttonClass,
          tone === "primary"
            ? "bg-primary text-primary-foreground hover:opacity-90"
            : "bg-background hover:bg-secondary",
        )}
      >
        {label}
      </button>
    );
  }

  return (
    <div role="group" aria-label={`${ariaLabel}?`} className="w-full space-y-2 sm:w-auto">
      <p className="text-sm">
        <span className="font-medium">{question}</span>{" "}
        <span className="text-muted-foreground">{consequence}</span>
      </p>
      <div className="flex gap-2">
        <button
          type="button"
          aria-disabled={pending}
          onClick={() =>
            startTransition(async () => {
              const result = await action();
              setError(result.error);
            })
          }
          className={cn(
            buttonClass,
            tone === "destructive"
              ? "bg-destructive text-white hover:opacity-90"
              : "bg-primary text-primary-foreground hover:opacity-90",
            pending && "pointer-events-none opacity-60",
          )}
        >
          {pending ? pendingLabel : confirmLabel}
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
