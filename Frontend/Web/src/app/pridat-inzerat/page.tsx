import type { Metadata } from "next";
import { ComingSoon } from "@/components/coming-soon";

export const metadata: Metadata = { title: "Pridať inzerát" };

export default function NewListingPage() {
  return (
    <ComingSoon
      title="Pridanie inzerátu pripravujeme"
      text="Formulár po krokoch (čo, rozpočet a termín, lokalita) bude ďalšia stránka, na ktorej budeme pracovať."
    />
  );
}
