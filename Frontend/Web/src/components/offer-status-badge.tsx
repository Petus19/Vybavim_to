import type { OfferStatus } from "@/lib/api";
import { offerStatusLabels } from "@/lib/offers";
import { cn } from "@/lib/utils";

const styles: Record<OfferStatus, string> = {
  pending: "bg-brand-orange/10 text-brand-orange",
  accepted: "bg-brand/10 text-brand",
  rejected: "bg-secondary text-muted-foreground",
  withdrawn: "bg-secondary text-muted-foreground",
};

/** Stav ponuky ako farebný odznak (rovnaký štýl ako stav inzerátu v „Moje inzeráty“). */
export function OfferStatusBadge({ status }: { status: OfferStatus }) {
  return (
    <span className={cn("rounded-full px-2.5 py-0.5 text-xs font-medium", styles[status])}>
      {offerStatusLabels[status]}
    </span>
  );
}
