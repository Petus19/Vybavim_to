import Link from "next/link";
import { Logo } from "@/components/logo";
import { ThemeToggle } from "@/components/theme-toggle";

export function SiteHeader() {
  return (
    <header className="sticky top-0 z-40 border-b border-border/60 bg-background/80 backdrop-blur-xl backdrop-saturate-150">
      <div className="mx-auto flex h-12 max-w-5xl items-center gap-4 px-5">
        <Link href="/" aria-label="Vybavím to – domov" className="shrink-0">
          <Logo />
        </Link>

        <nav className="ml-auto flex items-center gap-3 text-sm text-foreground/80 sm:gap-5">
          <Link
            href="/inzeraty"
            className="hidden transition-colors hover:text-foreground sm:block"
          >
            Inzeráty
          </Link>
          <Link href="/prihlasenie" className="transition-colors hover:text-foreground">
            Prihlásiť
          </Link>
          <ThemeToggle />
          <Link
            href="/pridat-inzerat"
            className="rounded-full bg-primary px-3.5 py-1.5 text-sm font-medium whitespace-nowrap text-primary-foreground transition-opacity hover:opacity-90"
          >
            Pridať<span className="hidden sm:inline"> inzerát</span>
          </Link>
        </nav>
      </div>
    </header>
  );
}
