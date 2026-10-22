/**
 * Popisky a formátovanie údajov o inzerátoch pre zobrazenie na webe.
 * Samotné dáta (inzeráty, kategórie) prichádzajú z API – pozri `src/lib/api.ts`.
 */

import type {
  Budget,
  Category,
  Engagement,
  ExperienceLevel,
  ListingStatus,
  ProjectSize,
} from "@/lib/api";

/** Mestá vo filtri. API zatiaľ nemá zoznam miest, preto sú tu napevno. */
export const cities = [
  "Bratislava",
  "Košice",
  "Žilina",
  "Banská Bystrica",
  "Nitra",
  "Prešov",
  "Trnava",
  "Trenčín",
];

export const levelLabels: Record<ExperienceLevel, string> = {
  beginner: "Začiatočník",
  intermediate: "Skúsený",
  expert: "Expert",
};

export const levelDescriptions: Record<ExperienceLevel, string> = {
  beginner: "Vhodné aj pre študentov a začínajúcich",
  intermediate: "Niekoľko podobných projektov za sebou",
  expert: "Dlhoročná prax a silné portfólio",
};

export const sizeLabels: Record<ProjectSize, string> = {
  small: "Malý",
  medium: "Stredný",
  large: "Veľký",
};

export const engagementLabels: Record<Engagement, string> = {
  one_time: "Jednorazovo",
  ongoing: "Dlhodobo",
};

export const statusLabels: Record<ListingStatus, string> = {
  open: "Otvorený",
  in_progress: "Prebieha",
  completed: "Dokončený",
  cancelled: "Stiahnutý",
};

/** Názov kategórie podľa slugu z inzerátu (napr. "grafika" → „Grafika a logo“). */
export function categoryName(categories: Category[], slug: string): string {
  return categories.find((c) => c.slug === slug)?.name ?? slug;
}

/** Kde sa pracuje: „Bratislava“, „Bratislava · aj na diaľku“ alebo „Na diaľku“. */
export function formatLocation({ city, remote }: { city: string | null; remote: boolean }) {
  if (!city) return "Na diaľku";
  return remote ? `${city} · aj na diaľku` : city;
}

const euro = new Intl.NumberFormat("sk-SK", {
  style: "currency",
  currency: "EUR",
  maximumFractionDigits: 0,
});

export function formatBudget(budget: Budget): string {
  if (budget.type === "negotiable" || budget.amount === null) return "Dohodou";
  const amount = euro.format(budget.amount);
  return budget.type === "hourly" ? `${amount} / hod.` : amount;
}

const dateFormat = new Intl.DateTimeFormat("sk-SK", {
  day: "numeric",
  month: "numeric",
  year: "numeric",
  timeZone: "Europe/Bratislava",
});

/** Dátum aj dátum s časom z API („2026-11-15“ alebo „2026-10-12T09:30:00+00:00“). */
export function formatDate(value: string): string {
  // Samotný dátum (bez času) by sa ako UTC polnoc mohol posunúť o deň – doplníme poludnie.
  const date = /^\d{4}-\d{2}-\d{2}$/.test(value) ? new Date(`${value}T12:00:00Z`) : new Date(value);
  return dateFormat.format(date);
}

const pluralRules = new Intl.PluralRules("sk-SK");

/** Slovenské tvary podľa počtu: plural(4, ["inzerát", "inzeráty", "inzerátov"]) → "4 inzeráty" */
export function plural(count: number, [one, few, many]: [string, string, string]): string {
  const rule = pluralRules.select(count);
  const word = rule === "one" ? one : rule === "few" ? few : many;
  return `${count} ${word}`;
}
