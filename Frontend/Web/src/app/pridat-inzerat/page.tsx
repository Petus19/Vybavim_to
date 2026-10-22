import type { Metadata } from "next";
import { Suspense } from "react";
import { ListingForm, ListingFormSkeleton } from "@/components/listing-form";
import { getCategories } from "@/lib/api";
import { requireUser } from "@/lib/auth";
import { emptyListingValues } from "@/lib/listing-form";

export const metadata: Metadata = { title: "Pridať inzerát" };

export default function NewListingPage() {
  return (
    <div className="mx-auto max-w-3xl px-5 pt-10 sm:pt-16">
      <h1 className="text-4xl font-semibold tracking-tight sm:text-5xl">Pridať inzerát</h1>
      <p className="mt-2 text-muted-foreground">
        Tri krátke kroky. Inzerát môžeš neskôr upraviť, kým ho nikto nerobí.
      </p>
      {/* Iba pre prihlásených – overenie číta cookie, preto Suspense */}
      <Suspense fallback={<ListingFormSkeleton />}>
        <NewListing />
      </Suspense>
    </div>
  );
}

async function NewListing() {
  await requireUser("/pridat-inzerat");
  const categories = await getCategories();
  return (
    <ListingForm
      categories={categories}
      listingId={null}
      initialValues={emptyListingValues}
      cancelHref="/moje-inzeraty"
    />
  );
}
