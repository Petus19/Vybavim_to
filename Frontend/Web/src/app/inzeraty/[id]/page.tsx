import type { Metadata } from "next";
import { Suspense } from "react";
import { notFound } from "next/navigation";
import {
  BadgeCheck,
  Building2,
  CalendarDays,
  CircleCheck,
  Gauge,
  Layers,
  MapPin,
  Receipt,
  Repeat,
  Star,
  User,
  type LucideIcon,
} from "lucide-react";
import { ChevronLink } from "@/components/chevron-link";
import { ListingCard } from "@/components/listing-card";
import {
  engagementLabels,
  formatBudget,
  formatDate,
  getCategory,
  getListing,
  getSimilarListings,
  levelDescriptions,
  levelLabels,
  listings,
  plural,
  sizeLabels,
  statusLabels,
  type Listing,
} from "@/lib/listings";

// Mock inzeráty poznáme vopred, takže sa detaily vygenerujú pri builde
export function generateStaticParams() {
  return listings.map((l) => ({ id: l.id }));
}

export async function generateMetadata(props: PageProps<"/inzeraty/[id]">): Promise<Metadata> {
  const { id } = await props.params;
  const listing = getListing(id);
  return { title: listing?.title ?? "Inzerát nenájdený" };
}

type Params = PageProps<"/inzeraty/[id]">["params"];

export default function ListingDetailPage(props: PageProps<"/inzeraty/[id]">) {
  return (
    <div className="mx-auto max-w-5xl px-5 pt-10">
      <ChevronLink href="/inzeraty" back className="text-sm">
        Inzeráty
      </ChevronLink>
      {/* params (id z URL) čítame v Suspense, aby navigácia zobrazila stránku okamžite */}
      <Suspense fallback={<DetailSkeleton />}>
        <ListingDetail params={props.params} />
      </Suspense>
    </div>
  );
}

async function ListingDetail({ params }: { params: Params }) {
  const { id } = await params;
  const listing = getListing(id);
  if (!listing) notFound();

  const category = getCategory(listing.category);
  const isOpen = listing.status === "open";
  const similar = getSimilarListings(listing);

  return (
    <>
      <header className="mt-8 max-w-3xl">
        <p className="text-sm font-medium text-brand">
          {category.name}
          {!isOpen && (
            <span className="text-muted-foreground"> · {statusLabels[listing.status]}</span>
          )}
        </p>
        <h1 className="mt-2 text-4xl leading-tight font-semibold tracking-tight sm:text-5xl">
          {listing.title}
        </h1>
        <p className="mt-4 text-muted-foreground">
          {listing.city} · pridané {formatDate(listing.createdAt)}
        </p>
      </header>

      <div className="mt-10 grid gap-10 lg:grid-cols-[minmax(0,1fr)_300px]">
        {/* Na mobile ide rozpočet s tlačidlom hneď pod nadpis a zadávateľ až na koniec,
            na počítači sú obe karty v pravom stĺpci */}
        <aside className="space-y-4 lg:sticky lg:top-20 lg:order-last lg:self-start">
          <BudgetCard listing={listing} />
          <div className="hidden lg:block">
            <OwnerCard owner={listing.owner} />
          </div>
        </aside>

        <article className="min-w-0">
          <Facts listing={listing} />

          <Section title="Popis">
            <p className="text-lg leading-relaxed whitespace-pre-line">{listing.description}</p>
          </Section>

          {listing.deliverables && listing.deliverables.length > 0 && (
            <Section title="Čo má byť výsledkom">
              <ul className="space-y-3">
                {listing.deliverables.map((item) => (
                  <li key={item} className="flex items-start gap-3">
                    <CircleCheck
                      className="mt-0.5 size-5 shrink-0 text-brand-check"
                      aria-hidden="true"
                    />
                    {item}
                  </li>
                ))}
              </ul>
            </Section>
          )}

          <Section title="Zručnosti">
            <ul className="flex flex-wrap gap-2">
              {listing.skills.map((skill) => (
                <li key={skill} className="rounded-full bg-surface px-3.5 py-1.5 text-sm">
                  {skill}
                </li>
              ))}
            </ul>
          </Section>

          {listing.requirements && listing.requirements.length > 0 && (
            <Section title="Požiadavky na odborníka">
              <ul className="list-disc space-y-2 pl-5 marker:text-muted-foreground">
                {listing.requirements.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </Section>
          )}

          <div className="mt-12 lg:hidden">
            <OwnerCard owner={listing.owner} />
          </div>
        </article>
      </div>

      {similar.length > 0 && (
        <section className="mt-20">
          <div className="flex items-baseline justify-between gap-4">
            <h2 className="text-2xl font-semibold tracking-tight">Podobné inzeráty</h2>
            <ChevronLink href={`/inzeraty?kategoria=${listing.category}`}>Všetky</ChevronLink>
          </div>
          <div className="mt-6 grid gap-4 md:grid-cols-3">
            {similar.map((l) => (
              <ListingCard key={l.id} listing={l} />
            ))}
          </div>
        </section>
      )}
    </>
  );
}

/** Hlavné fakty v dlaždiciach – na prvý pohľad je jasné, či sa oplatí ozvať. */
function Facts({ listing }: { listing: Listing }) {
  const facts: { icon: LucideIcon; label: string; value: string; hint?: string }[] = [
    {
      icon: Gauge,
      label: "Úroveň",
      value: levelLabels[listing.level],
      hint: levelDescriptions[listing.level],
    },
    {
      icon: Layers,
      label: "Rozsah",
      value: sizeLabels[listing.scope.size],
      hint: listing.scope.duration,
    },
    { icon: Repeat, label: "Spolupráca", value: engagementLabels[listing.engagement] },
    {
      icon: CalendarDays,
      label: "Termín",
      value: listing.deadline ? `do ${formatDate(listing.deadline)}` : "Bez termínu",
    },
    {
      icon: MapPin,
      label: "Spôsob práce",
      value: listing.remote ? "Aj na diaľku" : "Osobne",
      hint: listing.remote ? undefined : listing.city,
    },
    {
      icon: Receipt,
      label: "Faktúra",
      value: listing.invoiceRequired ? "Vyžaduje sa" : "Nevyžaduje sa",
      hint: listing.invoiceRequired ? "Treba živnosť alebo firmu" : undefined,
    },
  ];

  return (
    <dl className="grid grid-cols-2 gap-3 sm:grid-cols-3">
      {facts.map((fact) => (
        <div key={fact.label} className="rounded-2xl bg-surface p-4">
          <dt className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <fact.icon className="size-3.5" aria-hidden="true" />
            {fact.label}
          </dt>
          <dd className="mt-1.5 font-semibold tracking-tight">{fact.value}</dd>
          {fact.hint && <dd className="mt-0.5 text-xs text-muted-foreground">{fact.hint}</dd>}
        </div>
      ))}
    </dl>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="mt-12">
      <h2 className="mb-4 text-xl font-semibold tracking-tight">{title}</h2>
      {children}
    </section>
  );
}

function BudgetCard({ listing }: { listing: Listing }) {
  const isOpen = listing.status === "open";
  return (
    <div className="rounded-3xl bg-surface p-6">
      <p className="text-sm text-muted-foreground">Rozpočet</p>
      <p className="mt-1 text-3xl font-semibold tracking-tight">{formatBudget(listing.budget)}</p>
      <p className="mt-2 text-sm text-muted-foreground">
        {plural(listing.responses, ["ponuka", "ponuky", "ponúk"])} · odpovedá{" "}
        {listing.owner.responseTime}
      </p>
      {isOpen ? (
        <button
          type="button"
          className="mt-5 h-11 w-full rounded-full bg-primary text-base font-medium text-primary-foreground transition-opacity hover:opacity-90"
        >
          Poslať ponuku
        </button>
      ) : (
        <p className="mt-5 text-sm text-muted-foreground">Inzerát už neprijíma nové ponuky.</p>
      )}
    </div>
  );
}

function OwnerCard({ owner }: { owner: Listing["owner"] }) {
  const Icon = owner.type === "company" ? Building2 : User;
  return (
    <div className="rounded-3xl bg-surface p-6">
      <p className="text-sm text-muted-foreground">Zadávateľ</p>
      <div className="mt-3 flex items-center gap-3">
        <span className="inline-flex size-11 shrink-0 items-center justify-center rounded-full bg-background">
          <Icon className="size-5 text-muted-foreground" aria-hidden="true" />
        </span>
        <div className="min-w-0">
          <p className="flex items-center gap-1 font-semibold">
            <span className="truncate">{owner.name}</span>
            {owner.verified && (
              <BadgeCheck className="size-4 shrink-0 text-brand-check" aria-label="Overený" />
            )}
          </p>
          <p className="inline-flex items-center gap-1 text-sm text-muted-foreground">
            <Star className="size-3.5 fill-current" aria-hidden="true" />
            {owner.rating.toLocaleString("sk-SK")} ·{" "}
            {plural(owner.reviewCount, ["hodnotenie", "hodnotenia", "hodnotení"])}
          </p>
        </div>
      </div>
      <dl className="mt-4 space-y-1.5 border-t border-border/60 pt-4 text-sm">
        <OwnerStat label="Zadané projekty" value={String(owner.projectCount)} />
        <OwnerStat label="Na platforme od" value={owner.memberSince} />
        <OwnerStat label="Overený účet" value={owner.verified ? "Áno" : "Zatiaľ nie"} />
      </dl>
    </div>
  );
}

function OwnerStat({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between gap-3">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="font-medium">{value}</dd>
    </div>
  );
}

function DetailSkeleton() {
  return (
    <div className="mt-8 space-y-6">
      <div className="h-14 w-3/4 animate-pulse rounded-2xl bg-surface" />
      <div className="grid gap-10 lg:grid-cols-[minmax(0,1fr)_300px]">
        <div className="h-64 animate-pulse rounded-3xl bg-surface" />
        <div className="h-48 animate-pulse rounded-3xl bg-surface" />
      </div>
    </div>
  );
}
