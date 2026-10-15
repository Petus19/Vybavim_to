import { cn } from "@/lib/utils";

/**
 * Značka „Vybavím to“: oranžová bodka = zadávateľ, zelená bodka = odborník,
 * čiara medzi nimi = fajka „vybavené“. Farby idú z design tokenov, takže
 * fungujú v svetlom aj tmavom režime.
 */
export function LogoMark({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 44 40" className={cn("h-8 w-auto", className)} aria-hidden="true">
      <path
        d="M8 20 L18 30 L36 10"
        fill="none"
        stroke="var(--brand-check)"
        strokeWidth="4.5"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <circle cx="8" cy="20" r="7" fill="var(--brand-orange)" />
      <circle cx="36" cy="10" r="7" fill="var(--brand)" />
    </svg>
  );
}

export function Logo({ className }: { className?: string }) {
  return (
    <span className={cn("inline-flex items-center gap-2", className)}>
      <LogoMark className="h-5" />
      <span className="text-[15px] font-semibold tracking-tight text-foreground">vybavím to</span>
    </span>
  );
}
