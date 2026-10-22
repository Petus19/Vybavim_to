import type { Metadata } from "next";
import { Suspense } from "react";
import { notFound } from "next/navigation";
import { ChevronLink } from "@/components/chevron-link";
import { ListingForm, ListingFormSkeleton } from "@/components/listing-form";
import { getCategories, getListing } from "@/lib/api";
import { requireSession } from "@/lib/auth";
import { valuesFromListing } from "@/lib/listing-form";
import { statusLabels } from "@/lib/listings";

export const metadata: Metadata = { title: "Upraviť inzerát" };

type Params = PageProps<"/inzeraty/[id]/upravit">["params"];

export default function EditListingPage(props: PageProps<"/inzeraty/[id]/upravit">) {
  return (
    <div className="mx-auto max-w-3xl px-5 pt-10 sm:pt-16">
      <h1 className="text-4xl font-semibold tracking-tight sm:text-5xl">Upraviť inzerát</h1>
      {/* Iba pre autora – číta cookie a id z URL, preto Suspense */}
      <Suspense fallback={<ListingFormSkeleton />}>
        <EditListing params={props.params} />
      </Suspense>
    </div>
  );
}

async function EditListing({ params }: { params: Params }) {
  const { id } = await params;
  const { user, token } = await requireSession(`/inzeraty/${id}/upravit`);
  const [listing, categories] = await Promise.all([getListing(id, token), getCategories()]);
  // Cudzí inzerát upravovať nejde – tvárime sa, že stránka neexistuje (API by vrátilo 403)
  if (!listing || listing.ownerId !== user.id) notFound();

  if (listing.status !== "open") {
    return (
      <div className="mt-8 rounded-3xl bg-surface p-8">
        <p className="text-lg">
          Inzerát je v stave <strong>{statusLabels[listing.status]}</strong> – upraviť sa dá iba
          otvorený inzerát.
        </p>
        <ChevronLink href="/moje-inzeraty" back className="mt-4">
          Moje inzeráty
        </ChevronLink>
      </div>
    );
  }

  return (
    <ListingForm
      // Iný inzerát = nový formulár (Next.js inak drží stav navštívenej stránky)
      key={listing.id}
      categories={categories}
      listingId={listing.id}
      initialValues={valuesFromListing(listing)}
      cancelHref={`/inzeraty/${listing.id}`}
    />
  );
}
