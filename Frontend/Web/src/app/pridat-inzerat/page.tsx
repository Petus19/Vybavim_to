import type { Metadata } from "next";
import { Suspense } from "react";
import { ComingSoon } from "@/components/coming-soon";
import { requireUser } from "@/lib/auth";

export const metadata: Metadata = { title: "Pridať inzerát" };

export default function NewListingPage() {
  return (
    // Iba pre prihlásených – overenie číta cookie, preto Suspense
    <Suspense fallback={null}>
      <NewListing />
    </Suspense>
  );
}

async function NewListing() {
  const user = await requireUser("/pridat-inzerat");
  return (
    <ComingSoon
      title="Pridanie inzerátu pripravujeme"
      text={`Si prihlásený ako ${user.email}. Formulár po krokoch (čo, rozpočet a termín, lokalita) bude ďalšia stránka, na ktorej budeme pracovať.`}
    />
  );
}
