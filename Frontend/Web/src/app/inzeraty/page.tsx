import type { Metadata } from "next";
import { Suspense } from "react";
import Link from "next/link";
import { ChevronDown, Search } from "lucide-react";
import { ListingCard, ListingCardSkeleton } from "@/components/listing-card";
import { ChevronLink } from "@/components/chevron-link";
import { FiltersDisclosure } from "@/components/filters-disclosure";
import { Pagination } from "@/components/pagination";
import { SearchForm } from "@/components/search-form";
import {
  getCategories,
  searchListings,
  type BudgetType,
  type Engagement,
  type ExperienceLevel,
  type ListingSearchQuery,
} from "@/lib/api";
import { categoryName, cities, engagementLabels, levelLabels, plural } from "@/lib/listings";
import { cn } from "@/lib/utils";

/** Počet inzerátov na jednu stránku výsledkov (API dovoľuje najviac 50). */
const PAGE_SIZE = 24;

type SortOrder = "newest" | "cheapest" | "priciest";

export const metadata: Metadata = {
  title: "Inzeráty",
};

type SearchParams = PageProps<"/inzeraty">["searchParams"];

/**
 * Filtre v URL (slovenské názvy parametrov, aby sa dal odkaz zdieľať):
 * ?q=logo&kategoria=grafika&mesto=Košice&typ=hodinova&sadzba_od=20&sadzba_do=40
 *  &cena_od=&cena_do=&na_dialku=1&otvorene=1&zoradit=najlacnejsie
 */
const filterKeys = [
  "q",
  "kategoria",
  "mesto",
  "typ",
  "cena_od",
  "cena_do",
  "sadzba_od",
  "sadzba_do",
  "uroven",
  "spolupraca",
  "na_dialku",
  "otvorene",
  "bez_faktury",
  "zoradit",
  "strana",
] as const;

type Filters = Record<(typeof filterKeys)[number], string>;

/** Filtre schované v paneli „Filtre“ – ich počet sa ukazuje na tlačidle. */
const panelKeys = [
  "typ",
  "cena_od",
  "cena_do",
  "sadzba_od",
  "sadzba_do",
  "mesto",
  "uroven",
  "spolupraca",
  "na_dialku",
  "otvorene",
  "bez_faktury",
] as const;

const budgetTypes: { value: string; label: string; type?: BudgetType }[] = [
  { value: "", label: "Všetko" },
  { value: "pevna", label: "Pevná cena", type: "fixed" },
  { value: "hodinova", label: "Na hodinu", type: "hourly" },
  { value: "dohodou", label: "Dohodou", type: "negotiable" },
];

const levelOptions: { value: string; label: string; level?: ExperienceLevel }[] = [
  { value: "", label: "Všetky" },
  { value: "zaciatocnik", label: levelLabels.beginner, level: "beginner" },
  { value: "skuseny", label: levelLabels.intermediate, level: "intermediate" },
  { value: "expert", label: levelLabels.expert, level: "expert" },
];

const engagementOptions: { value: string; label: string; engagement?: Engagement }[] = [
  { value: "", label: "Všetko" },
  { value: "jednorazovo", label: engagementLabels.one_time, engagement: "one_time" },
  { value: "dlhodobo", label: engagementLabels.ongoing, engagement: "ongoing" },
];

const sortOptions: { value: string; label: string; sort: SortOrder }[] = [
  { value: "", label: "Najnovšie", sort: "newest" },
  { value: "najlacnejsie", label: "Najlacnejšie", sort: "cheapest" },
  { value: "najdrahsie", label: "Najdrahšie", sort: "priciest" },
];

function first(value: string | string[] | undefined) {
  return (Array.isArray(value) ? value[0] : value) ?? "";
}

/** Kladné číslo z URL, inak undefined (prázdne alebo nezmyselné hodnoty sa ignorujú). */
function toAmount(value: string) {
  const amount = Number(value);
  return value !== "" && Number.isFinite(amount) && amount >= 0 ? amount : undefined;
}

/** Slovenské parametre z URL webu → parametre API (`GET /api/v1/listings`). */
function toApiQuery(filters: Filters): ListingSearchQuery {
  const page = Math.max(1, Math.floor(toAmount(filters.strana) ?? 1));
  return {
    q: filters.q || undefined,
    category: filters.kategoria || undefined,
    city: filters.mesto || undefined,
    budgetType: budgetTypes.find((t) => t.value === filters.typ)?.type,
    priceMin: toAmount(filters.cena_od),
    priceMax: toAmount(filters.cena_do),
    rateMin: toAmount(filters.sadzba_od),
    rateMax: toAmount(filters.sadzba_do),
    level: levelOptions.find((o) => o.value === filters.uroven)?.level,
    engagement: engagementOptions.find((o) => o.value === filters.spolupraca)?.engagement,
    remote: filters.na_dialku === "1" || undefined,
    openOnly: filters.otvorene === "1" || undefined,
    noInvoice: filters.bez_faktury === "1" || undefined,
    sort: sortOptions.find((s) => s.value === filters.zoradit)?.sort,
    page,
    pageSize: PAGE_SIZE,
  };
}

/**
 * URL zoznamu so zachovanými filtrami, zmení sa iba to, čo je v `change`.
 * Pri zmene filtra sa vždy začína od prvej strany.
 */
function listingsHref(filters: Filters, change: Partial<Filters>) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries({ ...filters, strana: "", ...change })) {
    if (value) params.set(key, value);
  }
  const query = params.toString();
  return query ? `/inzeraty?${query}` : "/inzeraty";
}

export default function ListingsPage(props: PageProps<"/inzeraty">) {
  return (
    <div className="mx-auto max-w-5xl px-5 pt-14">
      <h1 className="text-4xl font-semibold tracking-tight sm:text-5xl">Inzeráty</h1>
      {/* Filtre čítajú searchParams (údaje z URL), preto musia byť v Suspense */}
      <Suspense fallback={<ListingsSkeleton />}>
        <ListingsView searchParams={props.searchParams} />
      </Suspense>
    </div>
  );
}

async function ListingsView({ searchParams }: { searchParams: SearchParams }) {
  const params = await searchParams;
  const filters = Object.fromEntries(filterKeys.map((key) => [key, first(params[key])])) as Filters;

  const [categories, result] = await Promise.all([
    getCategories(),
    searchListings(toApiQuery(filters)),
  ]);
  const hasFilters = filterKeys.some(
    (key) => key !== "zoradit" && key !== "strana" && filters[key],
  );
  // od/do jedného rozsahu sa počíta ako jeden filter
  const activeCount = new Set(
    panelKeys.filter((key) => filters[key]).map((key) => key.replace(/_(od|do)$/, "")),
  ).size;

  return (
    <>
      {/* key zabezpečí, že sa formulár po zmene URL prekreslí s novými hodnotami */}
      <SearchForm
        key={Object.values(filters).join("|")}
        action="/inzeraty"
        className="mt-8 flex flex-col gap-3"
      >
        {filters.kategoria && <input type="hidden" name="kategoria" value={filters.kategoria} />}
        {filters.zoradit && <input type="hidden" name="zoradit" value={filters.zoradit} />}

        <FiltersDisclosure
          activeCount={activeCount}
          bar={
            <div className="relative">
              <label>
                <span className="sr-only">Hľadať</span>
                <Search
                  className="pointer-events-none absolute top-1/2 left-4 size-4 -translate-y-1/2 text-muted-foreground"
                  aria-hidden="true"
                />
                <input
                  name="q"
                  type="search"
                  defaultValue={filters.q}
                  placeholder="Hľadať inzeráty"
                  className={cn(fieldClass, "w-full bg-surface pr-24 pl-11")}
                />
              </label>
              <button
                type="submit"
                className={cn(
                  primaryButtonClass,
                  "absolute top-1/2 right-1.5 h-8 -translate-y-1/2 px-4",
                )}
              >
                Hľadať
              </button>
            </div>
          }
        >
          <div className="mt-1 grid gap-6 rounded-3xl bg-surface p-6 sm:grid-cols-2">
            <PillRadioGroup
              legend="Typ ceny"
              name="typ"
              options={budgetTypes}
              value={filters.typ}
              className="sm:col-span-2"
            />

            <RangeField
              legend="Pevná cena"
              unit="€"
              minName="cena_od"
              maxName="cena_do"
              minValue={filters.cena_od}
              maxValue={filters.cena_do}
              placeholders={["0", "5 000"]}
            />
            <RangeField
              legend="Hodinová sadzba"
              unit="€/hod."
              minName="sadzba_od"
              maxName="sadzba_do"
              minValue={filters.sadzba_od}
              maxValue={filters.sadzba_do}
              placeholders={["0", "100"]}
            />

            <PillRadioGroup
              legend="Úroveň odborníka"
              name="uroven"
              options={levelOptions}
              value={filters.uroven}
            />
            <PillRadioGroup
              legend="Spolupráca"
              name="spolupraca"
              options={engagementOptions}
              value={filters.spolupraca}
            />

            <div>
              <p className={legendClass}>Mesto</p>
              <SelectField label="Mesto" name="mesto" defaultValue={filters.mesto}>
                <option value="">Celé Slovensko</option>
                {cities.map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
              </SelectField>
            </div>

            <fieldset>
              <legend className={legendClass}>Ďalšie</legend>
              <div className="space-y-2.5">
                <Checkbox name="na_dialku" checked={filters.na_dialku === "1"}>
                  Dá sa robiť aj na diaľku
                </Checkbox>
                <Checkbox name="otvorene" checked={filters.otvorene === "1"}>
                  Iba otvorené inzeráty
                </Checkbox>
                <Checkbox name="bez_faktury" checked={filters.bez_faktury === "1"}>
                  Bez faktúry (nepotrebujem živnosť)
                </Checkbox>
              </div>
            </fieldset>

            <div className="flex items-center justify-end gap-5 border-t border-border/60 pt-5 sm:col-span-2">
              {hasFilters && (
                <Link href="/inzeraty" className="text-sm text-brand hover:underline">
                  Zrušiť všetko
                </Link>
              )}
              <button type="submit" className={cn(primaryButtonClass, "h-10 px-6")}>
                Zobraziť výsledky
              </button>
            </div>
          </div>
        </FiltersDisclosure>
      </SearchForm>

      <nav
        aria-label="Kategórie"
        className="-mx-5 mt-5 [scrollbar-width:none] overflow-x-auto px-5 [&::-webkit-scrollbar]:hidden"
      >
        <ul className="flex gap-2">
          <li>
            <Chip href={listingsHref(filters, { kategoria: "" })} active={!filters.kategoria}>
              Všetko
            </Chip>
          </li>
          {categories.map((c) => (
            <li key={c.slug}>
              <Chip
                href={listingsHref(filters, { kategoria: c.slug })}
                active={filters.kategoria === c.slug}
              >
                {c.name}
              </Chip>
            </li>
          ))}
        </ul>
      </nav>

      <section aria-live="polite" className="mt-10">
        <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-2">
          <p className="text-sm text-muted-foreground">
            {result.ok
              ? plural(result.page.total, ["inzerát", "inzeráty", "inzerátov"])
              : "Neplatný filter"}
            {hasFilters && (
              <>
                {" · "}
                <Link href="/inzeraty" className="text-brand hover:underline">
                  Zrušiť filtre
                </Link>
              </>
            )}
          </p>
          <nav aria-label="Zoradenie" className="flex items-center gap-4 text-sm">
            {sortOptions.map((s) => {
              const active = filters.zoradit === s.value;
              return (
                <Link
                  key={s.value}
                  href={listingsHref(filters, { zoradit: s.value })}
                  aria-current={active ? "true" : undefined}
                  className={cn(
                    "transition-colors",
                    active
                      ? "font-medium text-foreground"
                      : "text-muted-foreground hover:text-foreground",
                  )}
                >
                  {s.label}
                </Link>
              );
            })}
          </nav>
        </div>
        {!result.ok ? (
          <EmptyState title="Tento filter nevieme použiť" text={result.message} />
        ) : result.page.items.length > 0 ? (
          <>
            <div className="mt-4 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {result.page.items.map((l) => (
                <ListingCard
                  key={l.id}
                  listing={l}
                  categoryName={categoryName(categories, l.category)}
                />
              ))}
            </div>
            <Pagination
              page={result.page.page}
              pageCount={Math.ceil(result.page.total / result.page.pageSize)}
              href={(strana) => listingsHref(filters, { strana: strana > 1 ? String(strana) : "" })}
            />
          </>
        ) : (
          <EmptyState title="Nič sme nenašli" text="Skús iné slovo alebo menej filtrov." />
        )}
      </section>
    </>
  );
}

function EmptyState({ title, text }: { title: string; text: string }) {
  return (
    <div className="mt-4 rounded-3xl bg-surface px-6 py-16 text-center">
      <p className="text-xl font-semibold tracking-tight">{title}</p>
      <p className="mt-2 text-muted-foreground">{text}</p>
      <ChevronLink href="/inzeraty" className="mt-6">
        Zobraziť všetky inzeráty
      </ChevronLink>
    </div>
  );
}

const fieldClass =
  "h-11 rounded-full bg-background px-4 text-sm outline-none placeholder:text-muted-foreground focus-visible:ring-3 focus-visible:ring-ring/40";

const primaryButtonClass =
  "rounded-full bg-primary text-sm font-medium text-primary-foreground transition-opacity hover:opacity-90";

const legendClass = "mb-3 text-sm font-medium";

/** Skupina prepínačov v tvare piluliek (natívne radio, takže funguje aj bez JavaScriptu). */
function PillRadioGroup({
  legend,
  name,
  options,
  value,
  className,
}: {
  legend: string;
  name: string;
  options: { value: string; label: string }[];
  value: string;
  className?: string;
}) {
  return (
    <fieldset className={className}>
      <legend className={legendClass}>{legend}</legend>
      <div className="flex flex-wrap gap-2">
        {options.map((option) => (
          <label
            key={option.value}
            className="cursor-pointer rounded-full bg-background px-4 py-2 text-sm transition-colors has-checked:bg-foreground has-checked:text-background has-focus-visible:ring-3 has-focus-visible:ring-ring/40"
          >
            <input
              type="radio"
              name={name}
              value={option.value}
              defaultChecked={value === option.value}
              className="sr-only"
            />
            {option.label}
          </label>
        ))}
      </div>
    </fieldset>
  );
}

function RangeField({
  legend,
  unit,
  minName,
  maxName,
  minValue,
  maxValue,
  placeholders,
}: {
  legend: string;
  unit: string;
  minName: string;
  maxName: string;
  minValue: string;
  maxValue: string;
  placeholders: [string, string];
}) {
  return (
    <fieldset>
      <legend className={legendClass}>
        {legend} <span className="font-normal text-muted-foreground">({unit})</span>
      </legend>
      <div className="flex items-center gap-2">
        <label className="flex-1">
          <span className="sr-only">
            {legend} od ({unit})
          </span>
          <input
            type="number"
            inputMode="numeric"
            min={0}
            step={1}
            name={minName}
            defaultValue={minValue}
            placeholder={`od ${placeholders[0]}`}
            className={cn(fieldClass, "w-full")}
          />
        </label>
        <span className="text-muted-foreground" aria-hidden="true">
          –
        </span>
        <label className="flex-1">
          <span className="sr-only">
            {legend} do ({unit})
          </span>
          <input
            type="number"
            inputMode="numeric"
            min={0}
            step={1}
            name={maxName}
            defaultValue={maxValue}
            placeholder={`do ${placeholders[1]}`}
            className={cn(fieldClass, "w-full")}
          />
        </label>
      </div>
    </fieldset>
  );
}

function Checkbox({
  name,
  checked,
  children,
}: {
  name: string;
  checked: boolean;
  children: React.ReactNode;
}) {
  return (
    <label className="flex cursor-pointer items-center gap-3 text-sm">
      <input
        type="checkbox"
        name={name}
        value="1"
        defaultChecked={checked}
        className="size-5 cursor-pointer rounded-md accent-[var(--primary)]"
      />
      {children}
    </label>
  );
}

/**
 * Výber v tvare pilulky. Natívnu šípku prehliadača skrývame (appearance-none),
 * lebo sa nedá odsadiť od okraja – namiesto nej je vlastná s rovnakým odsadením ako lupa.
 */
function SelectField({
  label,
  name,
  defaultValue,
  children,
}: {
  label: string;
  name: string;
  defaultValue: string;
  children: React.ReactNode;
}) {
  return (
    <label className="relative block">
      <span className="sr-only">{label}</span>
      <select
        name={name}
        defaultValue={defaultValue}
        className={cn(fieldClass, "w-full cursor-pointer appearance-none pr-11")}
      >
        {children}
      </select>
      <ChevronDown
        className="pointer-events-none absolute top-1/2 right-4 size-4 -translate-y-1/2 text-muted-foreground"
        aria-hidden="true"
      />
    </label>
  );
}

function Chip({
  href,
  active,
  children,
}: {
  href: string;
  active: boolean;
  children: React.ReactNode;
}) {
  return (
    <Link
      href={href}
      aria-current={active ? "page" : undefined}
      className={cn(
        "block rounded-full px-4 py-2 text-sm whitespace-nowrap transition-colors",
        active ? "bg-foreground text-background" : "bg-surface text-foreground hover:bg-secondary",
      )}
    >
      {children}
    </Link>
  );
}

function ListingsSkeleton() {
  return (
    <div className="mt-8 space-y-10">
      <div className="h-11 animate-pulse rounded-full bg-surface" />
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {Array.from({ length: 6 }, (_, i) => (
          <ListingCardSkeleton key={i} />
        ))}
      </div>
    </div>
  );
}
