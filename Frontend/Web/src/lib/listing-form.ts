/**
 * Formulár inzerátu (pridanie aj úprava): hodnoty, pravidlá a prevod na tvar API.
 *
 * Pravidlá sú rovnaké ako v backende (`Listing.Normalize` v module Listings). Web ich overuje
 * priebežne v prehliadači a ešte raz v Server Action – API ich aj tak overí ako posledné.
 */

import type { BudgetType, Engagement, ExperienceLevel, Listing, ProjectSize } from "@/lib/api";
import type { components } from "@shared/api/schema";

export type ListingInput = components["schemas"]["ListingInput"];

export const limits = {
  title: 120,
  description: 5000,
  duration: 60,
  city: 100,
  listItems: 10,
  listItem: 100,
} as const;

/** Hodnoty formulára – čísla a dátum sú text, presne ako ich používateľ napísal. */
export type ListingFormValues = {
  category: string;
  title: string;
  description: string;
  skills: string[];
  deliverables: string[];
  requirements: string[];
  budgetType: BudgetType;
  amount: string;
  deadline: string;
  level: ExperienceLevel;
  size: ProjectSize;
  duration: string;
  engagement: Engagement;
  invoiceRequired: boolean;
  remote: boolean;
  city: string;
};

export type ListingField = keyof ListingFormValues;
export type FieldErrors = Partial<Record<ListingField, string>>;

export const emptyListingValues: ListingFormValues = {
  category: "",
  title: "",
  description: "",
  skills: [],
  deliverables: [],
  requirements: [],
  budgetType: "fixed",
  amount: "",
  deadline: "",
  level: "intermediate",
  size: "medium",
  duration: "",
  engagement: "one_time",
  invoiceRequired: false,
  remote: false,
  city: "",
};

/** Predvyplnenie pri úprave existujúceho inzerátu. */
export function valuesFromListing(listing: Listing): ListingFormValues {
  return {
    category: listing.category,
    title: listing.title,
    description: listing.description,
    skills: listing.skills,
    deliverables: listing.deliverables,
    requirements: listing.requirements,
    budgetType: listing.budget.type,
    amount: listing.budget.amount === null ? "" : String(listing.budget.amount),
    deadline: listing.deadline ?? "",
    level: listing.level,
    size: listing.scope.size,
    duration: listing.scope.duration,
    engagement: listing.engagement,
    invoiceRequired: listing.invoiceRequired,
    remote: listing.remote,
    city: listing.city ?? "",
  };
}

/** Kroky formulára a polia, ktoré sa v nich vypĺňajú. */
export const steps: { title: string; fields: ListingField[] }[] = [
  {
    title: "Čo potrebuješ",
    fields: ["category", "title", "description", "skills", "deliverables", "requirements"],
  },
  {
    title: "Rozpočet a termín",
    fields: ["budgetType", "amount", "deadline", "level", "size", "duration", "engagement"],
  },
  { title: "Kde", fields: ["remote", "city", "invoiceRequired"] },
];

/** Krok, v ktorom je pole – aby sa pri chybe zo servera formulár vrátil na správne miesto. */
export function stepOf(field: ListingField): number {
  return Math.max(
    0,
    steps.findIndex((step) => step.fields.includes(field)),
  );
}

/** Suma z textu („300“, „12,50“) alebo `null`, ak to nie je kladné číslo. */
export function parseAmount(value: string): number | null {
  const amount = Number(value.trim().replace(",", "."));
  return value.trim() && Number.isFinite(amount) && amount > 0 ? amount : null;
}

/** Dnešný dátum v tvare „2026-10-22“ (pre `min` pri výbere termínu). */
export function today(): string {
  const now = new Date();
  const month = String(now.getMonth() + 1).padStart(2, "0");
  const day = String(now.getDate()).padStart(2, "0");
  return `${now.getFullYear()}-${month}-${day}`;
}

function listError(items: string[]): string | undefined {
  if (items.length > limits.listItems) return `Najviac ${limits.listItems} položiek.`;
  if (items.some((item) => item.length > limits.listItem))
    return `Položka môže mať najviac ${limits.listItem} znakov.`;
  return undefined;
}

/** Overí hodnoty. S `step` iba polia daného kroku (pred „Pokračovať“), inak všetko. */
export function validateListing(values: ListingFormValues, step?: number): FieldErrors {
  const errors: FieldErrors = {};
  const title = values.title.trim();
  const description = values.description.trim();
  const duration = values.duration.trim();
  const city = values.city.trim();

  if (!values.category) errors.category = "Vyber kategóriu.";
  if (!title) errors.title = "Napíš krátky názov.";
  else if (title.length > limits.title) errors.title = `Najviac ${limits.title} znakov.`;
  if (!description) errors.description = "Opíš, čo treba urobiť.";
  else if (description.length > limits.description)
    errors.description = `Najviac ${limits.description} znakov.`;
  if (values.skills.length === 0) errors.skills = "Pridaj aspoň jednu zručnosť.";
  else errors.skills = listError(values.skills);
  errors.deliverables = listError(values.deliverables);
  errors.requirements = listError(values.requirements);

  if (values.budgetType !== "negotiable" && parseAmount(values.amount) === null)
    errors.amount = "Zadaj sumu väčšiu ako 0.";
  if (values.deadline && values.deadline < today())
    errors.deadline = "Termín nesmie byť v minulosti.";
  if (!duration) errors.duration = "Odhadni, ako dlho to potrvá.";
  else if (duration.length > limits.duration)
    errors.duration = `Najviac ${limits.duration} znakov.`;

  if (!values.remote && !city) errors.city = "Zadaj mesto, alebo označ prácu na diaľku.";
  else if (city.length > limits.city) errors.city = `Najviac ${limits.city} znakov.`;

  const fields = step === undefined ? null : steps[step].fields;
  return Object.fromEntries(
    Object.entries(errors).filter(
      ([field, message]) => message && (!fields || fields.includes(field as ListingField)),
    ),
  );
}

/** Prevod na telo požiadavky `POST/PUT /api/v1/listings` (hodnoty musia prejsť `validateListing`). */
export function toListingInput(values: ListingFormValues): ListingInput {
  const city = values.city.trim();
  return {
    category: values.category,
    title: values.title.trim(),
    description: values.description.trim(),
    budget: {
      type: values.budgetType,
      amount: values.budgetType === "negotiable" ? null : parseAmount(values.amount),
    },
    city: city || null,
    remote: values.remote,
    deadline: values.deadline || null,
    level: values.level,
    scope: { size: values.size, duration: values.duration.trim() },
    engagement: values.engagement,
    invoiceRequired: values.invoiceRequired,
    skills: values.skills,
    deliverables: values.deliverables,
    requirements: values.requirements,
  };
}

/** Kód chyby z API → pole formulára, pri ktorom sa hláška zobrazí. */
export const fieldByErrorCode: Record<string, ListingField> = {
  "listing.unknown_category": "category",
  "listing.title_required": "title",
  "listing.title_too_long": "title",
  "listing.description_required": "description",
  "listing.description_too_long": "description",
  "listing.skills_required": "skills",
  "listing.budget_amount_required": "amount",
  "listing.budget_amount_not_allowed": "amount",
  "listing.deadline_in_past": "deadline",
  "listing.duration_required": "duration",
  "listing.duration_too_long": "duration",
  "listing.city_required": "city",
  "listing.city_too_long": "city",
};
