"use client";

import { useRouter } from "next/navigation";

/**
 * GET formulár pre vyhľadávanie a filtre, ktorý z URL vynechá prázdne polia.
 * Namiesto `/inzeraty?q=&typ=hodinova&cena_od=&…` vznikne `/inzeraty?typ=hodinova`,
 * takže sa odkaz dá pekne zdieľať. Bez JavaScriptu funguje ako obyčajný formulár.
 */
export function SearchForm({
  action,
  className,
  children,
}: {
  action: string;
  className?: string;
  children: React.ReactNode;
}) {
  const router = useRouter();

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const params = new URLSearchParams();
    for (const [key, value] of new FormData(event.currentTarget)) {
      if (typeof value === "string" && value.trim() !== "") params.set(key, value.trim());
    }
    const query = params.toString();
    router.push(query ? `${action}?${query}` : action);
  }

  return (
    <form action={action} onSubmit={handleSubmit} className={className}>
      {children}
    </form>
  );
}
