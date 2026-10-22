import Link from "next/link";
import type { ListingSummary } from "@/lib/api";
import { formatBudget, levelLabels, statusLabels } from "@/lib/listings";

/** Karta inzerátu: iba to podstatné – kategória, úroveň, názov, mesto a rozpočet. */
export function ListingCard({
  listing,
  categoryName,
}: {
  listing: ListingSummary;
  categoryName: string;
}) {
  return (
    <Link
      href={`/inzeraty/${listing.id}`}
      className="group flex h-full flex-col rounded-3xl bg-surface p-6 transition-transform duration-300 hover:scale-[1.015] focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
    >
      <p className="text-xs font-medium text-muted-foreground">
        {categoryName} · {levelLabels[listing.level]}
        {listing.status !== "open" && <> · {statusLabels[listing.status]}</>}
      </p>
      <h3 className="mt-2 text-lg leading-snug font-semibold tracking-tight">{listing.title}</h3>
      <div className="mt-auto flex items-end justify-between gap-3 pt-6">
        <span className="text-sm text-muted-foreground">{listing.city ?? "Na diaľku"}</span>
        <span className="text-base font-semibold">{formatBudget(listing.budget)}</span>
      </div>
    </Link>
  );
}

/** Zástupná karta počas načítania. */
export function ListingCardSkeleton() {
  return <div className="h-44 animate-pulse rounded-3xl bg-surface" />;
}
