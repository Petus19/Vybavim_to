import { ChevronLink } from "@/components/chevron-link";

/** Stránkovanie: Predchádzajúca · Strana 2 z 5 · Ďalšia (zobrazí sa, iba ak je viac strán). */
export function Pagination({
  page,
  pageCount,
  href,
}: {
  page: number;
  pageCount: number;
  href: (page: number) => string;
}) {
  if (pageCount <= 1) return null;
  return (
    <nav aria-label="Stránkovanie" className="mt-10 flex items-center justify-center gap-6 text-sm">
      {page > 1 ? (
        <ChevronLink href={href(page - 1)} back>
          Predchádzajúca
        </ChevronLink>
      ) : (
        <span className="w-28" />
      )}
      <span className="text-muted-foreground">
        Strana {page} z {pageCount}
      </span>
      {page < pageCount ? (
        <ChevronLink href={href(page + 1)}>Ďalšia</ChevronLink>
      ) : (
        <span className="w-28" />
      )}
    </nav>
  );
}
