"use client";

import { useSyncExternalStore } from "react";
import { Moon, Sun } from "lucide-react";
import { THEME_STORAGE_KEY, type ThemePreference } from "@/lib/theme";
import { cn } from "@/lib/utils";

const darkQuery = "(prefers-color-scheme: dark)";
const listeners = new Set<() => void>();

function readPreference(): ThemePreference {
  try {
    const value = localStorage.getItem(THEME_STORAGE_KEY);
    return value === "light" || value === "dark" ? value : "system";
  } catch {
    return "system";
  }
}

function applyPreference(preference: ThemePreference) {
  const dark = preference === "dark" || (preference === "system" && matchMedia(darkQuery).matches);
  const theme = dark ? "dark" : "light";
  document.documentElement.dataset.theme = theme;
  document.documentElement.style.colorScheme = theme;
}

function setPreference(preference: ThemePreference) {
  try {
    if (preference === "system") localStorage.removeItem(THEME_STORAGE_KEY);
    else localStorage.setItem(THEME_STORAGE_KEY, preference);
  } catch {
    // súkromný režim prehliadača – voľba platí aspoň do obnovenia stránky
  }
  applyPreference(preference);
  listeners.forEach((notify) => notify());
}

function subscribe(notify: () => void) {
  listeners.add(notify);
  // pri „Automaticky“ sledujeme zmenu systému, pri zmene v inej karte localStorage
  const media = matchMedia(darkQuery);
  const onSystemChange = () => {
    if (readPreference() === "system") applyPreference("system");
    notify();
  };
  const onStorage = (event: StorageEvent) => {
    if (event.key !== THEME_STORAGE_KEY) return;
    applyPreference(readPreference());
    notify();
  };
  media.addEventListener("change", onSystemChange);
  window.addEventListener("storage", onStorage);
  return () => {
    listeners.delete(notify);
    media.removeEventListener("change", onSystemChange);
    window.removeEventListener("storage", onStorage);
  };
}

/** Na serveri voľbu nepoznáme (je v prehliadači), preto `null`. */
function usePreference() {
  return useSyncExternalStore(subscribe, readPreference, () => null);
}

/** Ikona v hlavičke: jedným klikom prepne medzi svetlým a tmavým režimom. */
export function ThemeToggle({ className }: { className?: string }) {
  return (
    <button
      type="button"
      aria-label="Prepnúť svetlý alebo tmavý režim"
      title="Svetlý / tmavý režim"
      onClick={() =>
        setPreference(document.documentElement.dataset.theme === "dark" ? "light" : "dark")
      }
      className={cn(
        "inline-flex size-8 items-center justify-center rounded-full text-foreground/80 transition-colors hover:bg-surface hover:text-foreground",
        className,
      )}
    >
      {/* Ikonu volí CSS podľa data-theme, takže sa server a prehliadač nerozchádzajú */}
      <Moon className="size-4 dark:hidden" aria-hidden="true" />
      <Sun className="hidden size-4 dark:block" aria-hidden="true" />
    </button>
  );
}

const options: { value: ThemePreference; label: string }[] = [
  { value: "system", label: "Automaticky" },
  { value: "light", label: "Svetlý" },
  { value: "dark", label: "Tmavý" },
];

/** Prepínač v päte: Automaticky / Svetlý / Tmavý. */
export function ThemeSwitcher() {
  const current = usePreference();

  return (
    <div
      role="radiogroup"
      aria-label="Vzhľad"
      className="inline-flex rounded-full bg-secondary p-0.5"
    >
      {options.map((option) => {
        const active = current === option.value;
        return (
          <button
            key={option.value}
            type="button"
            role="radio"
            aria-checked={active}
            onClick={() => setPreference(option.value)}
            className={cn(
              "rounded-full px-3 py-1 text-xs transition-colors",
              active
                ? "bg-background text-foreground shadow-sm"
                : "text-muted-foreground hover:text-foreground",
            )}
          >
            {option.label}
          </button>
        );
      })}
    </div>
  );
}
