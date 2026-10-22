"use client";

import { useTransition } from "react";
import Link from "next/link";
import { usePathname, useSearchParams } from "next/navigation";
import { LogOut } from "lucide-react";
import { logout } from "@/app/prihlasenie/actions";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

/** Aktuálna stránka vrátane filtrov – na návrat po prihlásení alebo odhlásení. */
function useCurrentPath() {
  const pathname = usePathname();
  const query = useSearchParams().toString();
  return query ? `${pathname}?${query}` : pathname;
}

/** „Prihlásiť“ v hlavičke – po prihlásení sa človek vráti na stránku, kde práve je. */
export function LoginLink() {
  const current = useCurrentPath();
  const href =
    current === "/" || current.startsWith("/prihlasenie")
      ? "/prihlasenie"
      : `/prihlasenie?navrat=${encodeURIComponent(current)}`;

  return (
    <Link href={href} className="transition-colors hover:text-foreground">
      Prihlásiť
    </Link>
  );
}

/** Avatar prihláseného používateľa s menu (e-mail, odhlásenie). */
export function AccountMenu({ email }: { email: string }) {
  const current = useCurrentPath();
  const [pending, startTransition] = useTransition();
  const initial = email.charAt(0).toUpperCase();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        aria-label={`Účet ${email}`}
        className="inline-flex size-8 items-center justify-center rounded-full bg-primary text-sm font-medium text-primary-foreground transition-opacity outline-none hover:opacity-90 focus-visible:ring-3 focus-visible:ring-ring/50"
      >
        {initial}
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-60 rounded-2xl p-1.5">
        <DropdownMenuGroup>
          <DropdownMenuLabel className="px-2 py-1.5">
            <span className="block text-xs font-normal text-muted-foreground">Prihlásený ako</span>
            <span className="block truncate text-sm font-medium text-foreground">{email}</span>
          </DropdownMenuLabel>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          disabled={pending}
          onClick={() => startTransition(() => logout(current))}
          className="rounded-xl px-2 py-2"
        >
          <LogOut aria-hidden="true" />
          {pending ? "Odhlasujem…" : "Odhlásiť sa"}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
