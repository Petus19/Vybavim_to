"use client";

import { useId, useState } from "react";
import { SlidersHorizontal } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Riadok s vyhľadávaním + tlačidlo „Filtre“, ktoré rozbalí panel s ďalšími filtrami.
 *
 * Panel sa pri zatvorení iba skryje (atribút hidden), nie odstráni – jeho polia
 * sú stále súčasťou formulára, takže sa filtre nestratia, keď niekto iba hľadá text.
 */
export function FiltersDisclosure({
  bar,
  activeCount,
  children,
}: {
  bar: React.ReactNode;
  activeCount: number;
  children: React.ReactNode;
}) {
  const [open, setOpen] = useState(false);
  const panelId = useId();

  return (
    <>
      <div className="flex gap-3">
        <div className="min-w-0 flex-1">{bar}</div>
        <button
          type="button"
          aria-label={activeCount > 0 ? `Filtre (zapnuté: ${activeCount})` : "Filtre"}
          aria-expanded={open}
          aria-controls={panelId}
          onClick={() => setOpen((value) => !value)}
          className={cn(
            "inline-flex h-11 shrink-0 items-center gap-2 rounded-full px-4 text-sm transition-colors",
            open ? "bg-foreground text-background" : "bg-surface hover:bg-secondary",
          )}
        >
          <SlidersHorizontal className="size-4" aria-hidden="true" />
          <span className="hidden sm:inline">Filtre</span>
          {activeCount > 0 && (
            <span
              className={cn(
                "inline-flex size-5 items-center justify-center rounded-full text-xs font-medium",
                open ? "bg-background text-foreground" : "bg-primary text-primary-foreground",
              )}
            >
              <span className="sr-only">Zapnuté filtre: </span>
              {activeCount}
            </span>
          )}
        </button>
      </div>
      <div id={panelId} hidden={!open}>
        {children}
      </div>
    </>
  );
}
