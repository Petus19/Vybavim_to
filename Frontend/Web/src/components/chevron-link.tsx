import Link from "next/link";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { cn } from "@/lib/utils";

/** Textový odkaz so šípkou (štýl Apple: „Zobraziť všetky ›“). */
export function ChevronLink({
  href,
  back = false,
  className,
  children,
}: {
  href: string;
  back?: boolean;
  className?: string;
  children: React.ReactNode;
}) {
  return (
    <Link
      href={href}
      className={cn("inline-flex items-center text-brand hover:underline", className)}
    >
      {back && <ChevronLeft className="size-4" aria-hidden="true" />}
      {children}
      {!back && <ChevronRight className="size-4" aria-hidden="true" />}
    </Link>
  );
}
