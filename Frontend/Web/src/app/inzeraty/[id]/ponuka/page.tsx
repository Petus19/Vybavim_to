import type { Metadata } from "next";
import { Suspense } from "react";
import { notFound } from "next/navigation";
import { ChevronLink } from "@/components/chevron-link";
import { OfferForm } from "@/components/offer-form";
import { getListing, getMyOffers } from "@/lib/api";
import { requireSession } from "@/lib/auth";
import { formatBudget, formatLocation } from "@/lib/listings";
import { formatPrice, offerStatusLabels } from "@/lib/offers";

export const metadata: Metadata = { title: "Poslať ponuku" };

type Params = PageProps<"/inzeraty/[id]/ponuka">["params"];

export default function OfferPage(props: PageProps<"/inzeraty/[id]/ponuka">) {
  return (
    <div className="mx-auto max-w-2xl px-5 pt-10 sm:pt-16">
      {/* Iba pre prihlásených – overenie číta cookie a id z URL, preto Suspense */}
      <Suspense fallback={<div className="h-96 animate-pulse rounded-3xl bg-surface" />}>
        <Offer params={props.params} />
      </Suspense>
    </div>
  );
}

async function Offer({ params }: { params: Params }) {
  const { id } = await params;
  const { user, token } = await requireSession(`/inzeraty/${id}/ponuka`);
  const [listing, myOffers] = await Promise.all([getListing(id, null), getMyOffers(token)]);
  if (!listing) notFound();

  const back = (
    <ChevronLink href={`/inzeraty/${listing.id}`} back className="text-sm">
      Späť na inzerát
    </ChevronLink>
  );
  // Rovnaké pravidlá kontroluje aj API – tu ich ukážeme vopred namiesto chyby po odoslaní
  const existing = myOffers.find(
    (o) => o.listingId === listing.id && (o.status === "pending" || o.status === "accepted"),
  );
  const blocked =
    user.id === listing.ownerId
      ? "Na vlastný inzerát sa ponuka posiela nedá."
      : listing.status !== "open"
        ? "Inzerát už neprijíma nové ponuky."
        : existing
          ? `Na tento inzerát už máš ponuku za ${formatPrice(existing.price)} (${offerStatusLabels[existing.status].toLowerCase()}).`
          : null;

  return (
    <>
      {back}
      <p className="mt-8 text-sm font-medium text-brand">Ponuka na inzerát</p>
      <h1 className="mt-2 text-3xl leading-tight font-semibold tracking-tight sm:text-4xl">
        {listing.title}
      </h1>
      <p className="mt-3 text-muted-foreground">
        Rozpočet zadávateľa {formatBudget(listing.budget)} · {formatLocation(listing)}
      </p>

      {blocked ? (
        <div className="mt-10 rounded-3xl bg-surface p-6">
          <p className="font-medium">{blocked}</p>
          <ChevronLink href="/moje-ponuky" className="mt-3 text-sm">
            Moje ponuky
          </ChevronLink>
        </div>
      ) : (
        <div className="mt-10">
          <OfferForm listingId={listing.id} cancelHref={`/inzeraty/${listing.id}`} />
        </div>
      )}
    </>
  );
}
