import type { Metadata } from "next";
import { Suspense } from "react";
import Link from "next/link";
import { Send } from "lucide-react";
import { WithdrawOfferButton } from "@/components/offer-buttons";
import { OfferStatusBadge } from "@/components/offer-status-badge";
import { getMyOffers, type Offer } from "@/lib/api";
import { requireSession } from "@/lib/auth";
import { formatDate, plural } from "@/lib/listings";
import { formatPrice } from "@/lib/offers";

export const metadata: Metadata = { title: "Moje ponuky" };

export default function MyOffersPage() {
  return (
    <div className="mx-auto max-w-5xl px-5 pt-10 sm:pt-16">
      <div className="flex items-end justify-between gap-4">
        <h1 className="text-4xl font-semibold tracking-tight sm:text-5xl">Moje ponuky</h1>
        <Link
          href="/inzeraty?otvorene=1"
          className="hidden h-10 shrink-0 items-center rounded-full bg-primary px-5 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90 sm:inline-flex"
        >
          Hľadať zákazky
        </Link>
      </div>
      {/* Iba pre prihlásených – číta cookie a volá API s tokenom, preto Suspense */}
      <Suspense fallback={<MyOffersSkeleton />}>
        <MyOffers />
      </Suspense>
    </div>
  );
}

async function MyOffers() {
  const { token } = await requireSession("/moje-ponuky");
  const offers = await getMyOffers(token);
  if (offers.length === 0) return <EmptyState />;

  const pending = offers.filter((o) => o.status === "pending").length;
  return (
    <>
      <p className="mt-2 text-muted-foreground">
        {plural(offers.length, ["ponuka", "ponuky", "ponúk"])}, z toho{" "}
        {plural(pending, ["čaká", "čakajú", "čaká"])} na rozhodnutie
      </p>
      <ul className="mt-8 divide-y divide-border/60 overflow-hidden rounded-3xl bg-surface">
        {offers.map((offer) => (
          <MyOfferRow key={offer.id} offer={offer} />
        ))}
      </ul>
    </>
  );
}

function MyOfferRow({ offer }: { offer: Offer }) {
  const title = offer.listingTitle ?? "Inzerát";
  return (
    <li className="flex flex-col gap-4 p-5 sm:flex-row sm:items-start sm:gap-6 sm:p-6">
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground">
          <OfferStatusBadge status={offer.status} />
          <span>poslaná {formatDate(offer.createdAt)}</span>
        </div>
        <Link
          href={`/inzeraty/${offer.listingId}`}
          className="mt-2 block text-lg leading-snug font-semibold tracking-tight hover:underline"
        >
          {title}
        </Link>
        <p className="mt-1 text-sm text-muted-foreground">
          {formatPrice(offer.price)} · dodanie do{" "}
          {plural(offer.deliveryDays, ["dňa", "dní", "dní"])}
        </p>
        <p className="mt-3 line-clamp-2 text-sm whitespace-pre-line">{offer.message}</p>
      </div>
      {offer.status === "pending" && (
        <div className="shrink-0">
          <WithdrawOfferButton offerId={offer.id} listingTitle={title} />
        </div>
      )}
    </li>
  );
}

function EmptyState() {
  return (
    <div className="mt-10 flex flex-col items-center rounded-3xl bg-surface px-6 py-16 text-center">
      <Send className="size-10 text-muted-foreground" aria-hidden="true" />
      <h2 className="mt-4 text-xl font-semibold tracking-tight">
        Zatiaľ si neposlal žiadnu ponuku
      </h2>
      <p className="mt-2 max-w-sm text-muted-foreground">
        Nájdi zákazku, ktorá ti sedí, a pošli zadávateľovi ponuku s cenou a termínom.
      </p>
      <Link
        href="/inzeraty?otvorene=1"
        className="mt-6 inline-flex h-11 items-center rounded-full bg-primary px-6 text-base font-medium text-primary-foreground transition-opacity hover:opacity-90"
      >
        Hľadať zákazky
      </Link>
    </div>
  );
}

function MyOffersSkeleton() {
  return (
    <div className="mt-10 space-y-px overflow-hidden rounded-3xl">
      {[0, 1, 2].map((i) => (
        <div key={i} className="h-32 animate-pulse bg-surface" />
      ))}
    </div>
  );
}
