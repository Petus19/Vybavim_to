import { Code, Laptop, Megaphone, Palette, type LucideProps } from "lucide-react";
import type { CategoryId } from "@/lib/listings";

const icons = {
  web: Code,
  grafika: Palette,
  "it-podpora": Laptop,
  marketing: Megaphone,
} satisfies Record<CategoryId, unknown>;

export function CategoryIcon({ category, ...props }: { category: CategoryId } & LucideProps) {
  const Icon = icons[category];
  return <Icon aria-hidden="true" {...props} />;
}
