"use client";

import { useEffect, useId, useRef, useState, useTransition } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Check, X } from "lucide-react";
import { saveListing } from "@/app/pridat-inzerat/actions";
import { CategoryIcon } from "@/components/category-icon";
import type { Category } from "@/lib/api";
import {
  emptyListingValues,
  limits,
  stepOf,
  steps,
  today,
  validateListing,
  toListingInput,
  type FieldErrors,
  type ListingField,
  type ListingFormValues,
} from "@/lib/listing-form";
import {
  cities,
  engagementLabels,
  formatBudget,
  formatLocation,
  levelDescriptions,
  levelLabels,
  sizeLabels,
} from "@/lib/listings";
import { cn } from "@/lib/utils";

const budgetTypeLabels = {
  fixed: "Pevná cena",
  hourly: "Hodinová sadzba",
  negotiable: "Dohodou",
} as const;

/** Id poľa – na `<label htmlFor>` a na presun kurzora k prvej chybe. */
const fieldId = (field: ListingField) => `inzerat-${field}`;

/**
 * Formulár inzerátu po krokoch: čo → rozpočet a termín → kde. Ten istý pre pridanie
 * (`listingId` je `null`) aj úpravu. Chyby sa ukážu po kliknutí na „Pokračovať“
 * a potom sa priebežne aktualizujú pri písaní.
 */
export function ListingForm({
  categories,
  listingId,
  initialValues,
  cancelHref,
}: {
  categories: Category[];
  listingId: string | null;
  initialValues: ListingFormValues;
  cancelHref: string;
}) {
  const router = useRouter();
  const [values, setValues] = useState(initialValues);
  const [step, setStep] = useState(0);
  // Kroky, v ktorých sa už používateľ pokúsil pokračovať – iba v nich ukazujeme chyby
  const [checkedSteps, setCheckedSteps] = useState<number[]>([]);
  const [serverErrors, setServerErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string>();
  const [pending, startTransition] = useTransition();
  const headingRef = useRef<HTMLHeadingElement>(null);
  const stepChanged = useRef(false);

  const isLastStep = step === steps.length - 1;
  const errors: FieldErrors = {
    ...Object.assign({}, ...checkedSteps.map((s) => validateListing(values, s))),
    ...serverErrors,
  };

  // Po zmene kroku presunieme kurzor na nadpis – čítačka obrazovky oznámi nový krok
  useEffect(() => {
    if (stepChanged.current) headingRef.current?.focus();
    stepChanged.current = true;
  }, [step]);

  function set<K extends ListingField>(field: K, value: ListingFormValues[K]) {
    setValues((current) => ({ ...current, [field]: value }));
    // Hláška zo servera platila pre starú hodnotu
    setServerErrors((current) => {
      const next = { ...current };
      delete next[field];
      return next;
    });
  }

  function focusField(field: ListingField) {
    document.getElementById(fieldId(field))?.focus();
  }

  function goTo(target: number) {
    setFormError(undefined);
    setStep(target);
  }

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (pending) return;

    const stepErrors = validateListing(values, step);
    if (!checkedSteps.includes(step)) setCheckedSteps([...checkedSteps, step]);
    const firstInvalid = steps[step].fields.find((field) => stepErrors[field]);
    if (firstInvalid) {
      focusField(firstInvalid);
      return;
    }
    if (!isLastStep) {
      goTo(step + 1);
      return;
    }

    setFormError(undefined);
    startTransition(async () => {
      const result = await saveListing(listingId, values);
      if (result.savedId) {
        const savedId = result.savedId;
        startTransition(() => {
          router.push(`/inzeraty/${savedId}`);
          // Next.js si pamätá stav navštívených stránok – nový inzerát má začať s prázdnym formulárom
          if (!listingId) {
            setValues(emptyListingValues);
            setStep(0);
            setCheckedSteps([]);
          }
        });
        return;
      }
      setServerErrors(result.fieldErrors ?? {});
      setFormError(result.error);
      const field = steps.flatMap((s) => s.fields).find((f) => result.fieldErrors?.[f]);
      if (field) {
        setStep(stepOf(field));
        setCheckedSteps(steps.map((_, i) => i));
      }
    });
  }

  return (
    <form onSubmit={handleSubmit} noValidate className="mt-8">
      <Stepper current={step} onSelect={goTo} />

      <div className="mt-8 rounded-3xl bg-surface p-5 sm:p-8">
        <h2
          ref={headingRef}
          tabIndex={-1}
          className="text-2xl font-semibold tracking-tight outline-none"
        >
          {steps[step].title}
        </h2>

        <div className="mt-6 space-y-7">
          {step === 0 && (
            <>
              <fieldset aria-invalid={!!errors.category}>
                <legend className={labelClass}>Kategória</legend>
                <div className="grid gap-2 sm:grid-cols-2">
                  {categories.map((category, index) => (
                    <label
                      key={category.slug}
                      className="flex cursor-pointer items-start gap-3 rounded-2xl bg-background p-4 ring-foreground transition-shadow has-checked:ring-2 has-focus-visible:ring-3 has-focus-visible:ring-ring/50"
                    >
                      <input
                        type="radio"
                        name="category"
                        id={index === 0 ? fieldId("category") : undefined}
                        value={category.slug}
                        checked={values.category === category.slug}
                        onChange={() => set("category", category.slug)}
                        className="sr-only"
                      />
                      <CategoryIcon
                        category={category.slug}
                        className="mt-0.5 size-5 shrink-0 text-brand"
                      />
                      <span>
                        <span className="block font-medium">{category.name}</span>
                        <span className="mt-0.5 block text-sm text-muted-foreground">
                          {category.description}
                        </span>
                      </span>
                    </label>
                  ))}
                </div>
                <FieldError message={errors.category} />
              </fieldset>

              <TextField
                field="title"
                label="Názov"
                value={values.title}
                onChange={(v) => set("title", v)}
                max={limits.title}
                placeholder="napr. Logo pre kaviareň"
                hint="Jedna veta, z ktorej je jasné, čo treba."
                error={errors.title}
              />

              <div>
                <label htmlFor={fieldId("description")} className={labelClass}>
                  Popis
                </label>
                <textarea
                  id={fieldId("description")}
                  value={values.description}
                  onChange={(e) => set("description", e.target.value)}
                  rows={7}
                  aria-invalid={!!errors.description}
                  aria-describedby={`${fieldId("description")}-hint`}
                  placeholder="Čo presne treba urobiť, čo už máš pripravené a čo očakávaš."
                  className={cn(inputClass, "h-auto resize-y py-3 leading-relaxed")}
                />
                <Hint
                  id={`${fieldId("description")}-hint`}
                  count={values.description.trim().length}
                  max={limits.description}
                >
                  Čím konkrétnejšie, tým presnejšie ponuky dostaneš.
                </Hint>
                <FieldError message={errors.description} />
              </div>

              <ItemsField
                field="skills"
                label="Zručnosti"
                items={values.skills}
                onChange={(v) => set("skills", v)}
                placeholder="napr. Figma"
                hint={`Čo by mal odborník ovládať. Aspoň 1, najviac ${limits.listItems}.`}
                error={errors.skills}
              />
              <ItemsField
                field="deliverables"
                label="Čo má byť výsledkom"
                optional
                items={values.deliverables}
                onChange={(v) => set("deliverables", v)}
                placeholder="napr. Logo vo vektoroch"
                error={errors.deliverables}
              />
              <ItemsField
                field="requirements"
                label="Požiadavky na odborníka"
                optional
                items={values.requirements}
                onChange={(v) => set("requirements", v)}
                placeholder="napr. Portfólio s podobnými projektmi"
                error={errors.requirements}
              />
            </>
          )}

          {step === 1 && (
            <>
              <div>
                <PillGroup
                  legend="Cena"
                  name="budgetType"
                  options={Object.entries(budgetTypeLabels).map(([value, label]) => ({
                    value,
                    label,
                  }))}
                  value={values.budgetType}
                  onChange={(v) => set("budgetType", v as ListingFormValues["budgetType"])}
                />
                {values.budgetType !== "negotiable" && (
                  <div className="mt-3">
                    <label htmlFor={fieldId("amount")} className="sr-only">
                      {values.budgetType === "hourly" ? "Sadzba v € za hodinu" : "Suma v €"}
                    </label>
                    <div className="relative max-w-56">
                      <input
                        id={fieldId("amount")}
                        inputMode="decimal"
                        value={values.amount}
                        onChange={(e) => set("amount", e.target.value)}
                        aria-invalid={!!errors.amount}
                        placeholder={values.budgetType === "hourly" ? "25" : "300"}
                        className={cn(inputClass, "pr-20")}
                      />
                      <span className="pointer-events-none absolute top-1/2 right-4 -translate-y-1/2 text-muted-foreground">
                        {values.budgetType === "hourly" ? "€ / hod." : "€"}
                      </span>
                    </div>
                    <FieldError message={errors.amount} />
                  </div>
                )}
              </div>

              <fieldset>
                <legend className={labelClass}>Akého odborníka hľadáš</legend>
                <div className="grid gap-2 sm:grid-cols-3">
                  {(Object.keys(levelLabels) as ListingFormValues["level"][]).map((level) => (
                    <label
                      key={level}
                      className="cursor-pointer rounded-2xl bg-background p-4 ring-foreground transition-shadow has-checked:ring-2 has-focus-visible:ring-3 has-focus-visible:ring-ring/50"
                    >
                      <input
                        type="radio"
                        name="level"
                        value={level}
                        checked={values.level === level}
                        onChange={() => set("level", level)}
                        className="sr-only"
                      />
                      <span className="block font-medium">{levelLabels[level]}</span>
                      <span className="mt-0.5 block text-sm text-muted-foreground">
                        {levelDescriptions[level]}
                      </span>
                    </label>
                  ))}
                </div>
              </fieldset>

              <div className="grid gap-7 sm:grid-cols-2">
                <PillGroup
                  legend="Rozsah projektu"
                  name="size"
                  options={Object.entries(sizeLabels).map(([value, label]) => ({ value, label }))}
                  value={values.size}
                  onChange={(v) => set("size", v as ListingFormValues["size"])}
                />
                <PillGroup
                  legend="Spolupráca"
                  name="engagement"
                  options={Object.entries(engagementLabels).map(([value, label]) => ({
                    value,
                    label,
                  }))}
                  value={values.engagement}
                  onChange={(v) => set("engagement", v as ListingFormValues["engagement"])}
                />
              </div>

              <div className="grid gap-7 sm:grid-cols-2">
                <TextField
                  field="duration"
                  label="Ako dlho to potrvá"
                  value={values.duration}
                  onChange={(v) => set("duration", v)}
                  max={limits.duration}
                  placeholder="napr. 2–4 týždne"
                  error={errors.duration}
                />
                <div>
                  <label htmlFor={fieldId("deadline")} className={labelClass}>
                    Termín <span className="font-normal text-muted-foreground">(nepovinné)</span>
                  </label>
                  <input
                    id={fieldId("deadline")}
                    type="date"
                    min={today()}
                    value={values.deadline}
                    onChange={(e) => set("deadline", e.target.value)}
                    aria-invalid={!!errors.deadline}
                    className={inputClass}
                  />
                  <FieldError message={errors.deadline} />
                </div>
              </div>
            </>
          )}

          {step === 2 && (
            <>
              <CheckboxField
                checked={values.remote}
                onChange={(v) => set("remote", v)}
                label="Dá sa robiť na diaľku"
                hint="Odborník nemusí prísť osobne."
              />
              <div>
                <label htmlFor={fieldId("city")} className={labelClass}>
                  Mesto{" "}
                  {values.remote && (
                    <span className="font-normal text-muted-foreground">(nepovinné)</span>
                  )}
                </label>
                <input
                  id={fieldId("city")}
                  list="inzerat-mesta"
                  value={values.city}
                  onChange={(e) => set("city", e.target.value)}
                  maxLength={limits.city}
                  aria-invalid={!!errors.city}
                  placeholder="napr. Bratislava"
                  autoComplete="address-level2"
                  className={cn(inputClass, "max-w-sm")}
                />
                <datalist id="inzerat-mesta">
                  {cities.map((city) => (
                    <option key={city} value={city} />
                  ))}
                </datalist>
                <FieldError message={errors.city} />
              </div>
              <CheckboxField
                checked={values.invoiceRequired}
                onChange={(v) => set("invoiceRequired", v)}
                label="Vyžadujem faktúru"
                hint="Odborník musí mať živnosť alebo firmu."
              />
              <Preview values={values} categories={categories} />
            </>
          )}
        </div>

        {formError && (
          <p role="alert" className="mt-6 text-sm text-destructive">
            {formError}
          </p>
        )}

        <div className="mt-8 flex items-center justify-between gap-3">
          {step === 0 ? (
            <Link href={cancelHref} className={cn(buttonClass, "bg-background hover:bg-secondary")}>
              Zrušiť
            </Link>
          ) : (
            <button
              type="button"
              onClick={() => goTo(step - 1)}
              className={cn(buttonClass, "bg-background hover:bg-secondary")}
            >
              Späť
            </button>
          )}
          <button
            type="submit"
            aria-disabled={pending}
            className={cn(
              buttonClass,
              "bg-primary text-primary-foreground hover:opacity-90",
              pending && "pointer-events-none opacity-60",
            )}
          >
            {pending
              ? "Ukladám…"
              : !isLastStep
                ? "Pokračovať"
                : listingId
                  ? "Uložiť zmeny"
                  : "Zverejniť inzerát"}
          </button>
        </div>
      </div>
    </form>
  );
}

/** Zástupný tvar formulára počas načítania. */
export function ListingFormSkeleton() {
  return <div className="mt-8 h-[36rem] animate-pulse rounded-3xl bg-surface" />;
}

/** Číslované kroky hore. Na hotový krok sa dá vrátiť kliknutím, dopredu iba cez „Pokračovať“. */
function Stepper({ current, onSelect }: { current: number; onSelect: (step: number) => void }) {
  return (
    <ol className="flex items-center gap-2 text-sm">
      {steps.map((step, index) => {
        const done = index < current;
        const content = (
          <>
            <span
              className={cn(
                "inline-flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-medium",
                index === current
                  ? "bg-primary text-primary-foreground"
                  : done
                    ? "bg-brand text-brand-foreground"
                    : "bg-secondary text-muted-foreground",
              )}
            >
              {done ? <Check className="size-3.5" aria-hidden="true" /> : index + 1}
            </span>
            <span
              className={cn(
                index === current ? "font-medium" : "text-muted-foreground",
                index !== current && "hidden sm:inline",
              )}
            >
              {step.title}
            </span>
          </>
        );
        return (
          <li key={step.title} className="flex items-center gap-2">
            {index > 0 && <span className="h-px w-4 bg-border sm:w-8" aria-hidden="true" />}
            {done ? (
              <button
                type="button"
                onClick={() => onSelect(index)}
                aria-label={`Krok ${index + 1}: ${step.title} (hotový, späť na úpravu)`}
                className="flex items-center gap-2 rounded-full hover:underline"
              >
                {content}
              </button>
            ) : (
              <span
                className="flex items-center gap-2"
                aria-current={index === current ? "step" : undefined}
              >
                <span className="sr-only">Krok {index + 1}: </span>
                {content}
              </span>
            )}
          </li>
        );
      })}
    </ol>
  );
}

/** Takto bude inzerát vyzerať v zozname – posledná kontrola pred zverejnením. */
function Preview({ values, categories }: { values: ListingFormValues; categories: Category[] }) {
  const input = toListingInput(values);
  const category = categories.find((c) => c.slug === values.category);
  return (
    <section aria-label="Náhľad inzerátu">
      <p className={labelClass}>Náhľad v zozname</p>
      <div className="rounded-3xl bg-background p-6">
        <p className="text-xs font-medium text-muted-foreground">
          {category?.name ?? "Bez kategórie"} · {levelLabels[values.level]}
        </p>
        <p className="mt-2 text-lg leading-snug font-semibold tracking-tight">
          {input.title || "Bez názvu"}
        </p>
        <div className="mt-6 flex items-end justify-between gap-3">
          <span className="text-sm text-muted-foreground">
            {formatLocation({ city: input.city, remote: input.remote })}
          </span>
          <span className="text-base font-semibold">{formatBudget(input.budget)}</span>
        </div>
      </div>
    </section>
  );
}

const labelClass = "mb-2 block px-1 text-sm font-medium";
const inputClass =
  "h-12 w-full rounded-2xl bg-background px-4 text-base outline-none placeholder:text-muted-foreground focus-visible:ring-3 focus-visible:ring-ring/40 aria-invalid:ring-2 aria-invalid:ring-destructive/60";
const buttonClass =
  "inline-flex h-11 items-center justify-center rounded-full px-6 text-base font-medium transition-[opacity,background-color]";

function FieldError({ message }: { message?: string }) {
  if (!message) return null;
  return <p className="mt-1.5 px-1 text-sm text-destructive">{message}</p>;
}

function Hint({
  id,
  count,
  max,
  children,
}: {
  id?: string;
  count?: number;
  max?: number;
  children?: React.ReactNode;
}) {
  return (
    <p id={id} className="mt-1.5 flex justify-between gap-4 px-1 text-xs text-muted-foreground">
      <span>{children}</span>
      {max !== undefined && (
        <span className={cn("tabular-nums", count! > max && "text-destructive")}>
          {count}/{max}
        </span>
      )}
    </p>
  );
}

function TextField({
  field,
  label,
  value,
  onChange,
  max,
  placeholder,
  hint,
  error,
}: {
  field: ListingField;
  label: string;
  value: string;
  onChange: (value: string) => void;
  max: number;
  placeholder?: string;
  hint?: string;
  error?: string;
}) {
  const id = fieldId(field);
  return (
    <div>
      <label htmlFor={id} className={labelClass}>
        {label}
      </label>
      <input
        id={id}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        aria-invalid={!!error}
        aria-describedby={`${id}-hint`}
        placeholder={placeholder}
        className={inputClass}
      />
      <Hint id={`${id}-hint`} count={value.trim().length} max={max}>
        {hint}
      </Hint>
      <FieldError message={error} />
    </div>
  );
}

/**
 * Zoznam krátkych položiek (zručnosti, výsledky, požiadavky). Enter alebo „Pridať“ pridá,
 * Backspace v prázdnom poli zmaže poslednú. Rozpísaný text sa pridá aj pri opustení poľa.
 */
function ItemsField({
  field,
  label,
  optional = false,
  items,
  onChange,
  placeholder,
  hint,
  error,
}: {
  field: ListingField;
  label: string;
  optional?: boolean;
  items: string[];
  onChange: (items: string[]) => void;
  placeholder: string;
  hint?: string;
  error?: string;
}) {
  const [draft, setDraft] = useState("");
  const id = fieldId(field);
  const listId = useId();
  const full = items.length >= limits.listItems;

  function add() {
    const item = draft.trim().slice(0, limits.listItem);
    setDraft("");
    if (!item || full || items.some((i) => i.toLowerCase() === item.toLowerCase())) return;
    onChange([...items, item]);
  }

  return (
    <div>
      <label htmlFor={id} className={labelClass}>
        {label} {optional && <span className="font-normal text-muted-foreground">(nepovinné)</span>}
      </label>
      {items.length > 0 && (
        <ul id={listId} aria-label={label} className="mb-2 flex flex-wrap gap-2">
          {items.map((item) => (
            <li
              key={item}
              className="inline-flex items-center gap-1 rounded-full bg-background py-1 pr-1 pl-3.5 text-sm"
            >
              {item}
              <button
                type="button"
                onClick={() => onChange(items.filter((i) => i !== item))}
                aria-label={`Odstrániť ${item}`}
                className="inline-flex size-6 items-center justify-center rounded-full text-muted-foreground hover:bg-secondary hover:text-foreground"
              >
                <X className="size-3.5" aria-hidden="true" />
              </button>
            </li>
          ))}
        </ul>
      )}
      <div className="flex gap-2">
        <input
          id={id}
          value={draft}
          disabled={full}
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              e.preventDefault(); // Enter tu pridáva položku, neodosiela formulár
              add();
            } else if (e.key === "Backspace" && !draft && items.length > 0) {
              onChange(items.slice(0, -1));
            }
          }}
          onBlur={add}
          maxLength={limits.listItem}
          aria-invalid={!!error}
          aria-describedby={hint ? `${id}-hint` : undefined}
          placeholder={full ? `Najviac ${limits.listItems} položiek` : placeholder}
          className={inputClass}
        />
        <button
          type="button"
          onClick={add}
          disabled={full}
          className="h-12 shrink-0 rounded-2xl bg-background px-4 text-sm font-medium transition-colors hover:bg-secondary disabled:opacity-50"
        >
          Pridať
        </button>
      </div>
      {hint && <Hint id={`${id}-hint`}>{hint}</Hint>}
      <FieldError message={error} />
    </div>
  );
}

function PillGroup({
  legend,
  name,
  options,
  value,
  onChange,
}: {
  legend: string;
  name: string;
  options: { value: string; label: string }[];
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <fieldset>
      <legend className={labelClass}>{legend}</legend>
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
              checked={value === option.value}
              onChange={() => onChange(option.value)}
              className="sr-only"
            />
            {option.label}
          </label>
        ))}
      </div>
    </fieldset>
  );
}

function CheckboxField({
  checked,
  onChange,
  label,
  hint,
}: {
  checked: boolean;
  onChange: (checked: boolean) => void;
  label: string;
  hint: string;
}) {
  return (
    <label className="flex cursor-pointer items-start gap-3 px-1">
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        className="mt-0.5 size-5 shrink-0 cursor-pointer accent-[var(--primary)]"
      />
      <span>
        <span className="block font-medium">{label}</span>
        <span className="block text-sm text-muted-foreground">{hint}</span>
      </span>
    </label>
  );
}
