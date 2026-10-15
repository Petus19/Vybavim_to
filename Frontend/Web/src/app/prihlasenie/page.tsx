import type { Metadata } from "next";
import { ComingSoon } from "@/components/coming-soon";

export const metadata: Metadata = { title: "Prihlásenie" };

export default function LoginPage() {
  return (
    <ComingSoon
      title="Prihlásenie pripravujeme"
      text="Registrácia a prihlásenie pribudnú spolu s modulom účtov na backende."
    />
  );
}
