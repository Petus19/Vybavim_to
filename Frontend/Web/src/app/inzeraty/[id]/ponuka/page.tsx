import type { Metadata } from "next";
import { Suspense } from "react";
import { notFound } from "next/navigation";
import { ChevronLink } from "@/components/chevron-link";
import { LogoMark } from "@/components/logo";
import { getListing } from "@/lib/api";
import { requireUser } from "@/lib/auth";

export const metadata: Metadata = { title: "Poslať ponuku" };

export default function OfferPage(props: PageProps<"/inzeraty/[id]/ponuka">) {
  return (
    // Iba pre prihlásených – overenie číta cookie a id z URL, preto Suspense
    <Suspense fallback={null}>
      <Offer params={props.params} />
    </Suspense>
  );
}

async function Offer({ params }: { params: PageProps<"/inzeraty/[id]/ponuka">["params"] }) {
  const { id } = await params;
  const user = await requireUser(`/inzeraty/${id}/ponuka`);
  // Bez tokenu, ako verejnosť – na stiahnutý inzerát sa ponuka poslať nedá (404)
  const listing = await getListing(id, null);
  if (!listing) notFound();

  return (
    <div className="mx-auto flex max-w-lg flex-col items-center px-5 pt-28 text-center">
      <LogoMark className="h-10" />
      <p className="mt-8 text-sm font-medium text-brand">Ponuka na inzerát</p>
      <h1 className="mt-2 text-3xl font-semibold tracking-tight">{listing.title}</h1>
      <p className="mt-4 text-lg text-muted-foreground">
        Si prihlásený ako {user.email}. Posielanie ponúk pripravujeme – pribudne s modulom Projects
        na backende.
      </p>
      <ChevronLink href={`/inzeraty/${listing.id}`} back className="mt-8">
        Späť na inzerát
      </ChevronLink>
    </div>
  );
}
