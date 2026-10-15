import Link from "next/link";
import { ThemeSwitcher } from "@/components/theme-toggle";

const links = [
  { href: "/inzeraty", label: "Inzeráty" },
  { href: "/pridat-inzerat", label: "Pridať inzerát" },
  { href: "/prihlasenie", label: "Prihlásenie" },
];

export function SiteFooter() {
  return (
    <footer className="mt-24 bg-surface">
      <div className="mx-auto flex max-w-5xl flex-col gap-4 px-5 py-8 text-xs text-muted-foreground sm:flex-row sm:items-center sm:justify-between">
        <div className="space-y-3">
          <p>Vybavím to · ukážková verzia s testovacími dátami</p>
          <nav className="flex gap-5">
            {links.map((l) => (
              <Link key={l.href} href={l.href} className="hover:text-foreground">
                {l.label}
              </Link>
            ))}
          </nav>
        </div>
        <div className="flex items-center gap-3">
          <span>Vzhľad</span>
          <ThemeSwitcher />
        </div>
      </div>
    </footer>
  );
}
