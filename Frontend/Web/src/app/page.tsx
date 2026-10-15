import Link from "next/link";
import Form from "next/form";
import { Search } from "lucide-react";
import { CategoryIcon } from "@/components/category-icon";
import { ChevronLink } from "@/components/chevron-link";
import { ListingCard } from "@/components/listing-card";
import { categories, getListings } from "@/lib/listings";

const steps = [
  { title: "Zadaj inzerát", text: "Popíš, čo potrebuješ, rozpočet a termín." },
  { title: "Vyber z ponúk", text: "Odborníci sa ozvú sami. Porovnaj hodnotenia." },
  { title: "Vybavené", text: "Dohodnite sa cez chat a ohodnoťte spoluprácu." },
];

export default function HomePage() {
  const latest = getListings()
    .filter((l) => l.status === "open")
    .slice(0, 3);

  return (
    <>
      <section className="mx-auto max-w-3xl px-5 pt-20 pb-16 text-center sm:pt-28">
        <p className="text-sm font-medium text-brand">IT a grafika na Slovensku</p>
        <h1 className="mt-3 text-5xl leading-[1.05] font-semibold tracking-tight sm:text-7xl">
          Nájdi odborníka.
          <br />
          <span className="text-muted-foreground">Vybavené.</span>
        </h1>
        <p className="mx-auto mt-6 max-w-xl text-lg text-muted-foreground sm:text-xl">
          Zadaj, čo potrebuješ, a overení odborníci ti pošlú ponuky.
        </p>

        <Form action="/inzeraty" className="relative mx-auto mt-10 max-w-xl">
          <label>
            <span className="sr-only">Čo potrebuješ vybaviť?</span>
            <Search
              className="pointer-events-none absolute top-1/2 left-5 size-5 -translate-y-1/2 text-muted-foreground"
              aria-hidden="true"
            />
            <input
              name="q"
              type="search"
              placeholder="Logo, e-shop, správa siete…"
              className="h-14 w-full rounded-full bg-surface pr-32 pl-13 text-base outline-none placeholder:text-muted-foreground focus-visible:ring-3 focus-visible:ring-ring/40"
            />
          </label>
          <button
            type="submit"
            className="absolute top-1/2 right-2 h-10 -translate-y-1/2 rounded-full bg-primary px-5 text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90"
          >
            Hľadať
          </button>
        </Form>

        <div className="mt-8 flex justify-center gap-8 text-base">
          <ChevronLink href="/inzeraty">Prezrieť inzeráty</ChevronLink>
          <ChevronLink href="/prihlasenie">Som odborník</ChevronLink>
        </div>
      </section>

      <section className="bg-surface py-20">
        <div className="mx-auto max-w-5xl px-5">
          <h2 className="text-center text-3xl font-semibold tracking-tight sm:text-4xl">
            Čo potrebuješ vybaviť?
          </h2>
          <div className="mt-10 grid grid-cols-2 gap-4 lg:grid-cols-4">
            {categories.map((c) => (
              <Link
                key={c.id}
                href={`/inzeraty?kategoria=${c.id}`}
                className="group rounded-3xl bg-background p-6 transition-transform duration-300 hover:scale-[1.02]"
              >
                <CategoryIcon category={c.id} className="size-7 text-brand" strokeWidth={1.5} />
                <p className="mt-6 font-semibold tracking-tight">{c.name}</p>
                <p className="mt-1 text-sm text-muted-foreground">{c.description}</p>
              </Link>
            ))}
          </div>
        </div>
      </section>

      <section className="mx-auto max-w-5xl px-5 pt-20">
        <div className="flex items-baseline justify-between gap-4">
          <h2 className="text-3xl font-semibold tracking-tight sm:text-4xl">Najnovšie</h2>
          <ChevronLink href="/inzeraty">Všetky inzeráty</ChevronLink>
        </div>
        <div className="mt-8 grid gap-4 md:grid-cols-3">
          {latest.map((l) => (
            <ListingCard key={l.id} listing={l} />
          ))}
        </div>
      </section>

      <section className="mx-auto max-w-5xl px-5 pt-24">
        <h2 className="text-center text-3xl font-semibold tracking-tight sm:text-4xl">
          Ako to funguje
        </h2>
        <ol className="mt-12 grid gap-10 text-center sm:grid-cols-3">
          {steps.map((s, i) => (
            <li key={s.title}>
              <span className="text-4xl font-semibold text-brand">{i + 1}</span>
              <p className="mt-3 text-lg font-semibold tracking-tight">{s.title}</p>
              <p className="mt-1 text-muted-foreground">{s.text}</p>
            </li>
          ))}
        </ol>
      </section>
    </>
  );
}
