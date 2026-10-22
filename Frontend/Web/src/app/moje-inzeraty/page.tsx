import type { Metadata } from "next";
import { Suspense } from "react";
import Link from "next/link";
import { redirect } from "next/navigation";
import { ClipboardList } from "lucide-react";
import { CancelListingButton } from "@/components/cancel-listing-button";
import { Pagination } from "@/components/pagination";
import { getCategories, getMyListings, type ListingStatus, type ListingSummary } from "@/lib/api";
import { requireSession } from "@/lib/auth";
import {
  categoryName,
  formatBudget,
  formatDate,
  formatLocation,
  plural,
  statusLabels,
} from "@/lib/listings";
import { cn } from "@/lib/utils";

export const metadata: Metadata = { title: "Moje inzeráty" };

type SearchParams = PageProps<"/moje-inzeraty">["searchParams"];

export default function MyListingsPage(props: PageProps<"/moje-inzeraty">) {
  return (
    <div className="mx-auto max-w-5xl px-5 pt-10 sm:pt-16">
      <div className="flex items-end justify-between gap-4">
        <h1 className="text-4xl font-semibold tracking-tight sm:text-5xl">Moje inzeráty</h1>
        <Link
          href="/pridat-inzerat"
          className="hidden h-10 shrink-0 items-center rounded-full bg-primary px-5 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90 sm:inline-flex"
        >
          Pridať inzerát
        </Link>
      </div>
      {/* Iba pre prihlásených – číta cookie a volá API s tokenom, preto Suspense */}
      <Suspense fallback={<MyListingsSkeleton />}>
        <MyListings searchParams={props.searchParams} />
      </Suspense>
    </div>
  );
}

async function MyListings({ searchParams }: { searchParams: SearchParams }) {
  const { strana } = await searchParams;
  const page = Math.max(1, Math.floor(Number(strana)) || 1);
  const pageHref = (p: number) => (p > 1 ? `/moje-inzeraty?strana=${p}` : "/moje-inzeraty");

  const { token } = await requireSession(pageHref(page));
  const [result, categories] = await Promise.all([getMyListings(token, page), getCategories()]);
  const pageCount = Math.ceil(result.total / result.pageSize);

  if (result.total === 0) return <EmptyState />;
  // Strana za koncom (napr. starý odkaz) – radšej prvá strana ako prázdny zoznam
  if (result.items.length === 0) redirect(pageHref(1));

  return (
    <>
      <p className="mt-2 text-muted-foreground">
        {plural(result.total, ["inzerát", "inzeráty", "inzerátov"])} vrátane ukončených
      </p>
      <ul className="mt-8 divide-y divide-border/60 overflow-hidden rounded-3xl bg-surface">
        {result.items.map((listing) => (
          <MyListingRow
            key={listing.id}
            listing={listing}
            categoryName={categoryName(categories, listing.category)}
          />
        ))}
      </ul>
      <Pagination page={result.page} pageCount={pageCount} href={pageHref} />
    </>
  );
}

function MyListingRow({
  listing,
  categoryName,
}: {
  listing: ListingSummary;
  categoryName: string;
}) {
  return (
    <li className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center sm:gap-6 sm:p-6">
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground">
          <StatusBadge status={listing.status} />
          <span>
            {categoryName} · pridané {formatDate(listing.createdAt)}
          </span>
        </div>
        <Link
          href={`/inzeraty/${listing.id}`}
          className={cn(
            "mt-2 block text-lg leading-snug font-semibold tracking-tight hover:underline",
            listing.status === "cancelled" && "text-muted-foreground",
          )}
        >
          {listing.title}
        </Link>
        <p className="mt-1 text-sm text-muted-foreground">
          {formatLocation(listing)} · {formatBudget(listing.budget)}
        </p>
      </div>
      {/* Upraviť a stiahnuť sa dá iba otvorený inzerát – keď už prebieha, API vráti 409 */}
      {listing.status === "open" && (
        <div className="flex shrink-0 flex-wrap items-start gap-2">
          <Link
            href={`/inzeraty/${listing.id}/upravit`}
            aria-label={`Upraviť inzerát ${listing.title}`}
            className="inline-flex h-9 items-center rounded-full bg-background px-4 text-sm font-medium transition-colors hover:bg-secondary"
          >
            Upraviť
          </Link>
          <CancelListingButton listingId={listing.id} title={listing.title} />
        </div>
      )}
    </li>
  );
}

const statusStyles: Record<ListingStatus, string> = {
  open: "bg-brand/10 text-brand",
  in_progress: "bg-brand-orange/10 text-brand-orange",
  completed: "bg-secondary text-foreground",
  cancelled: "bg-secondary text-muted-foreground",
};

function StatusBadge({ status }: { status: ListingStatus }) {
  return (
    <span className={cn("rounded-full px-2.5 py-0.5 font-medium", statusStyles[status])}>
      {statusLabels[status]}
    </span>
  );
}

function EmptyState() {
  return (
    <div className="mt-10 flex flex-col items-center rounded-3xl bg-surface px-6 py-16 text-center">
      <ClipboardList className="size-10 text-muted-foreground" aria-hidden="true" />
      <h2 className="mt-4 text-xl font-semibold tracking-tight">Zatiaľ nemáš žiadny inzerát</h2>
      <p className="mt-2 max-w-sm text-muted-foreground">
        Opíš, čo potrebuješ vybaviť, a odborníci ti pošlú ponuky.
      </p>
      <Link
        href="/pridat-inzerat"
        className="mt-6 inline-flex h-11 items-center rounded-full bg-primary px-6 text-base font-medium text-primary-foreground transition-opacity hover:opacity-90"
      >
        Pridať inzerát
      </Link>
    </div>
  );
}

function MyListingsSkeleton() {
  return (
    <div className="mt-10 space-y-px overflow-hidden rounded-3xl">
      {[0, 1, 2].map((i) => (
        <div key={i} className="h-28 animate-pulse bg-surface" />
      ))}
    </div>
  );
}
