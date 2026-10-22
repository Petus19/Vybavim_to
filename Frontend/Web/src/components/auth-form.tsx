"use client";

import { useActionState, useId, useState } from "react";
import Link from "next/link";
import { Check, Eye, EyeOff } from "lucide-react";
import { login, register, type AuthFormState } from "@/app/prihlasenie/actions";
import { cn } from "@/lib/utils";

type Mode = "login" | "register";

/** Rovnaké pravidlá ako v API (ASP.NET Identity) – zobrazujú sa priebežne pri písaní. */
const passwordRules = [
  { label: "aspoň 8 znakov", test: (p: string) => p.length >= 8 },
  { label: "veľké písmeno", test: (p: string) => /[A-ZÁ-Ž]/.test(p) },
  { label: "malé písmeno", test: (p: string) => /[a-zá-ž]/.test(p) },
  { label: "číslica", test: (p: string) => /\d/.test(p) },
];

/** Adresa prihlásenia so zvolenou záložkou a návratom (`?rezim=registracia&navrat=…`). */
function modeHref(mode: Mode, navrat: string) {
  const params = new URLSearchParams();
  if (mode === "register") params.set("rezim", "registracia");
  if (navrat !== "/") params.set("navrat", navrat);
  const query = params.toString();
  return query ? `/prihlasenie?${query}` : "/prihlasenie";
}

/**
 * Prihlásenie a registrácia na jednej obrazovke. Po úspechu Server Action uloží tokeny
 * do httpOnly cookies a presmeruje na `navrat` – tam, kde používateľ chcel pokračovať.
 *
 * Záložka je v URL (nie v stave komponentu): Next.js si pamätá stav navštívených stránok,
 * takže by sa napr. po odhlásení zobrazila záložka z minulej návštevy.
 */
export function AuthForm({
  navrat,
  mode,
  googleEnabled,
}: {
  navrat: string;
  mode: Mode;
  googleEnabled: boolean;
}) {
  return (
    <div className="w-full">
      {googleEnabled && (
        <>
          {/* Obyčajný odkaz, nie <Link>: vedie na Route Handler, ktorý presmeruje na Google */}
          <a
            href={
              navrat === "/"
                ? "/prihlasenie/google"
                : `/prihlasenie/google?navrat=${encodeURIComponent(navrat)}`
            }
            className="flex h-12 w-full items-center justify-center gap-3 rounded-full border border-border bg-background text-base font-medium transition-colors hover:bg-surface"
          >
            <GoogleLogo />
            Pokračovať s Google
          </a>
          <div className="my-6 flex items-center gap-3 text-xs text-muted-foreground">
            <span className="h-px flex-1 bg-border" />
            alebo e-mailom
            <span className="h-px flex-1 bg-border" />
          </div>
        </>
      )}

      <div
        role="tablist"
        aria-label="Prihlásenie alebo registrácia"
        className="grid grid-cols-2 rounded-full bg-secondary p-1"
      >
        <Tab active={mode === "login"} href={modeHref("login", navrat)}>
          Prihlásiť sa
        </Tab>
        <Tab active={mode === "register"} href={modeHref("register", navrat)}>
          Vytvoriť účet
        </Tab>
      </div>

      {/* key: pri prepnutí sa formulár vytvorí nanovo, takže sa nemiešajú chyby z druhej záložky */}
      {mode === "login" ? (
        <LoginForm key="login" navrat={navrat} />
      ) : (
        <RegisterForm key="register" navrat={navrat} />
      )}
    </div>
  );
}

/** Farebné logo „G“ podľa pravidiel Google pre tlačidlá prihlásenia. */
function GoogleLogo() {
  return (
    <svg viewBox="0 0 48 48" className="size-5" aria-hidden="true">
      <path
        fill="#EA4335"
        d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"
      />
      <path
        fill="#4285F4"
        d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"
      />
      <path
        fill="#FBBC05"
        d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z"
      />
      <path
        fill="#34A853"
        d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"
      />
    </svg>
  );
}

function LoginForm({ navrat }: { navrat: string }) {
  const [state, action, pending] = useActionState<AuthFormState, FormData>(login, {});

  return (
    <form action={action} className="mt-6 space-y-3">
      <input type="hidden" name="navrat" value={navrat} />
      <Field label="E-mail">
        <input
          name="email"
          type="email"
          autoComplete="email"
          required
          defaultValue={state.email}
          placeholder="meno@example.sk"
          className={inputClass}
        />
      </Field>
      <PasswordField autoComplete="current-password" />
      <FormError message={state.error} />
      <SubmitButton pending={pending}>Prihlásiť sa</SubmitButton>
    </form>
  );
}

function RegisterForm({ navrat }: { navrat: string }) {
  const [state, action, pending] = useActionState<AuthFormState, FormData>(register, {});
  // Heslo a súhlas držíme v stave – React by ich inak po neúspešnom odoslaní vymazal
  const [password, setPassword] = useState("");
  const [consent, setConsent] = useState(false);

  return (
    <form action={action} className="mt-6 space-y-3">
      <input type="hidden" name="navrat" value={navrat} />
      <Field label="E-mail">
        <input
          name="email"
          type="email"
          autoComplete="email"
          required
          defaultValue={state.email}
          placeholder="meno@example.sk"
          className={inputClass}
        />
      </Field>
      <PasswordField
        autoComplete="new-password"
        value={password}
        onChange={setPassword}
        describedBy="pravidla-hesla"
      />
      <ul id="pravidla-hesla" className="flex flex-wrap gap-x-4 gap-y-1 px-1 text-xs">
        {passwordRules.map((rule) => {
          const ok = rule.test(password);
          return (
            <li
              key={rule.label}
              className={cn(
                "inline-flex items-center gap-1 transition-colors",
                ok ? "text-brand" : "text-muted-foreground",
              )}
            >
              <Check className={cn("size-3", !ok && "opacity-30")} aria-hidden="true" />
              {rule.label}
              <span className="sr-only">{ok ? " – splnené" : " – nesplnené"}</span>
            </li>
          );
        })}
      </ul>
      <label className="flex cursor-pointer items-start gap-3 px-1 pt-2 text-sm">
        <input
          type="checkbox"
          name="suhlas"
          required
          checked={consent}
          onChange={(e) => setConsent(e.target.checked)}
          className="mt-0.5 size-4 shrink-0 cursor-pointer accent-[var(--primary)]"
        />
        <span className="text-muted-foreground">
          Súhlasím s podmienkami používania a so spracovaním osobných údajov (GDPR).
        </span>
      </label>
      <FormError message={state.error} />
      <SubmitButton pending={pending}>Vytvoriť účet</SubmitButton>
    </form>
  );
}

const inputClass =
  "h-12 w-full rounded-2xl bg-surface px-4 text-base outline-none placeholder:text-muted-foreground focus-visible:ring-3 focus-visible:ring-ring/40";

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1.5 block px-1 text-sm font-medium">{label}</span>
      {children}
    </label>
  );
}

function PasswordField({
  autoComplete,
  value,
  onChange,
  describedBy,
}: {
  autoComplete: "current-password" | "new-password";
  value?: string;
  onChange?: (value: string) => void;
  describedBy?: string;
}) {
  const [visible, setVisible] = useState(false);
  const id = useId();

  return (
    <div>
      <label htmlFor={id} className="mb-1.5 block px-1 text-sm font-medium">
        Heslo
      </label>
      <div className="relative">
        <input
          id={id}
          name="password"
          type={visible ? "text" : "password"}
          autoComplete={autoComplete}
          required
          value={value}
          onChange={onChange ? (e) => onChange(e.target.value) : undefined}
          aria-describedby={describedBy}
          className={cn(inputClass, "pr-12")}
        />
        <button
          type="button"
          onClick={() => setVisible((v) => !v)}
          aria-label={visible ? "Skryť heslo" : "Zobraziť heslo"}
          aria-pressed={visible}
          className="absolute top-1/2 right-2 inline-flex size-9 -translate-y-1/2 items-center justify-center rounded-full text-muted-foreground transition-colors hover:text-foreground"
        >
          {visible ? (
            <EyeOff className="size-4" aria-hidden="true" />
          ) : (
            <Eye className="size-4" aria-hidden="true" />
          )}
        </button>
      </div>
    </div>
  );
}

function FormError({ message }: { message?: string }) {
  return (
    <p aria-live="polite" className={cn("px-1 text-sm text-destructive", !message && "sr-only")}>
      {message}
    </p>
  );
}

function SubmitButton({ pending, children }: { pending: boolean; children: React.ReactNode }) {
  return (
    <button
      type="submit"
      aria-disabled={pending}
      className={cn(
        "mt-2 h-12 w-full rounded-full bg-primary text-base font-medium text-primary-foreground transition-opacity hover:opacity-90",
        pending && "pointer-events-none opacity-60",
      )}
    >
      {pending ? "Chvíľu…" : children}
    </button>
  );
}

function Tab({
  active,
  href,
  children,
}: {
  active: boolean;
  href: string;
  children: React.ReactNode;
}) {
  return (
    <Link
      href={href}
      replace
      scroll={false}
      role="tab"
      aria-selected={active}
      className={cn(
        "flex h-9 items-center justify-center rounded-full text-sm transition-colors",
        active
          ? "bg-background font-medium text-foreground shadow-sm"
          : "text-muted-foreground hover:text-foreground",
      )}
    >
      {children}
    </Link>
  );
}
