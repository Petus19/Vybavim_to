import {
  Briefcase,
  Code,
  Laptop,
  Megaphone,
  Palette,
  type LucideIcon,
  type LucideProps,
} from "lucide-react";

/** Ikony podľa slugu kategórie z API. Nová kategória bez ikony dostane všeobecnú ikonu. */
const icons: Record<string, LucideIcon> = {
  web: Code,
  grafika: Palette,
  "it-podpora": Laptop,
  marketing: Megaphone,
};

export function CategoryIcon({ category, ...props }: { category: string } & LucideProps) {
  const Icon = icons[category] ?? Briefcase;
  return <Icon aria-hidden="true" {...props} />;
}
