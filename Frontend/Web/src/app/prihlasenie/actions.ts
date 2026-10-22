"use server";

import { redirect } from "next/navigation";
import { client } from "@/lib/api";
import { clearTokens, safeReturnPath, saveTokens } from "@/lib/auth";

/** Stav formulára pre `useActionState`: chyba na zobrazenie a vyplnený e-mail (aby sa nestratil). */
export type AuthFormState = { error?: string; email?: string };

const UNAVAILABLE = "Prihlásenie teraz nefunguje. Skús to o chvíľu znova.";

function text(formData: FormData, name: string) {
  const value = formData.get(name);
  return typeof value === "string" ? value.trim() : "";
}

/** Chybová hláška z API (ProblemDetails `title`) – API ich vracia po slovensky. */
function problemTitle(error: unknown, fallback: string) {
  const title = (error as { title?: unknown } | undefined)?.title;
  return typeof title === "string" && title ? title : fallback;
}

/** Prihlási cez API a uloží tokeny. Vráti chybu na zobrazenie alebo `null` pri úspechu. */
async function signIn(login: string, password: string): Promise<string | null> {
  try {
    const { data, error } = await client.POST("/api/v1/auth/login", {
      body: { login, password },
    });
    if (!data) return problemTitle(error, "Prihlásenie sa nepodarilo.");
    await saveTokens(data);
    return null;
  } catch {
    return UNAVAILABLE;
  }
}

export async function login(_: AuthFormState, formData: FormData): Promise<AuthFormState> {
  const email = text(formData, "email");
  const password = formData.get("password");
  if (!email || typeof password !== "string" || !password) {
    return { error: "Vyplň e-mail aj heslo.", email };
  }

  const error = await signIn(email, password);
  if (error) return { error, email };

  // redirect musí byť mimo try/catch – funguje tak, že vyhodí špeciálnu výnimku
  redirect(safeReturnPath(formData.get("navrat")));
}

export async function register(_: AuthFormState, formData: FormData): Promise<AuthFormState> {
  const email = text(formData, "email");
  const password = formData.get("password");
  if (!email || typeof password !== "string" || !password) {
    return { error: "Vyplň e-mail aj heslo.", email };
  }
  if (formData.get("suhlas") !== "on") {
    return { error: "Na vytvorenie účtu treba súhlasiť s podmienkami.", email };
  }

  try {
    const { data, error } = await client.POST("/api/v1/auth/register", {
      body: { email, password },
    });
    if (!data) return { error: problemTitle(error, "Účet sa nepodarilo vytvoriť."), email };
  } catch {
    return { error: UNAVAILABLE, email };
  }

  // Po registrácii rovno prihlásiť, nech človek nemusí zadávať údaje znova
  const error = await signIn(email, password);
  if (error) return { error, email };

  redirect(safeReturnPath(formData.get("navrat")));
}

/** Odhlásenie: zmaže tokeny a vráti na danú stránku (ak vyžaduje prihlásenie, ukáže sa prihlásenie). */
export async function logout(returnTo: string) {
  await clearTokens();
  redirect(safeReturnPath(returnTo));
}
