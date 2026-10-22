"use client"; // Chybové stránky musia byť klientske komponenty

import { useEffect } from "react";
import { LogoMark } from "@/components/logo";

/**
 * Zobrazí sa, keď sa stránka nedá vykresliť – najčastejšie preto, že nebeží API.
 * Hlavička a päta zostanú, nahradí sa iba obsah stránky.
 */
export default function Error({
  error,
  retry,
}: {
  error: Error & { digest?: string };
  retry: () => void;
}) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <div className="mx-auto flex max-w-lg flex-col items-center px-5 pt-28 text-center">
      <LogoMark className="h-10" />
      <h1 className="mt-8 text-4xl font-semibold tracking-tight">Niečo sa nepodarilo</h1>
      <p className="mt-3 text-lg text-muted-foreground">
        Údaje sa teraz nedajú načítať. Skús to o chvíľu znova.
      </p>
      {process.env.NODE_ENV === "development" && (
        <p className="mt-6 rounded-2xl bg-surface px-5 py-4 text-left text-sm text-muted-foreground">
          Vývoj: beží API? Spusti <code className="text-foreground">docker compose up -d</code> v
          koreni repozitára a <code className="text-foreground">dotnet run --project Api</code> v
          priečinku <code className="text-foreground">Backend</code>.
        </p>
      )}
      <button
        type="button"
        onClick={() => retry()}
        className="mt-8 h-11 rounded-full bg-primary px-6 text-base font-medium text-primary-foreground transition-opacity hover:opacity-90"
      >
        Skúsiť znova
      </button>
    </div>
  );
}
