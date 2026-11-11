"use client";

import { decideOffer } from "@/app/moje-ponuky/actions";
import { ConfirmActionButton } from "@/components/confirm-action-button";

/** Autor inzerátu: prijať alebo odmietnuť čakajúcu ponuku. */
export function OfferDecisionButtons({
  offerId,
  specialist,
  otherPending,
}: {
  offerId: string;
  specialist: string;
  otherPending: number;
}) {
  const others =
    otherPending === 0
      ? ""
      : otherPending === 1
        ? " Druhá čakajúca ponuka sa odmietne."
        : ` Ostatné čakajúce ponuky (${otherPending}) sa odmietnu.`;
  return (
    <div className="flex flex-wrap items-start gap-2">
      <ConfirmActionButton
        label="Prijať"
        ariaLabel={`Prijať ponuku od ${specialist}`}
        question="Prijať túto ponuku?"
        consequence={`Inzerát prejde do stavu „Prebieha“.${others} Vrátiť sa to nedá.`}
        confirmLabel="Áno, prijať"
        pendingLabel="Prijímam…"
        tone="primary"
        action={() => decideOffer(offerId, "accept")}
      />
      <ConfirmActionButton
        label="Odmietnuť"
        ariaLabel={`Odmietnuť ponuku od ${specialist}`}
        question="Odmietnuť túto ponuku?"
        consequence="Odborník uvidí, že ste ju odmietli."
        confirmLabel="Áno, odmietnuť"
        pendingLabel="Odmietam…"
        tone="destructive"
        action={() => decideOffer(offerId, "reject")}
      />
    </div>
  );
}

/** Odborník: stiahnuť vlastnú ponuku, kým o nej zadávateľ nerozhodol. */
export function WithdrawOfferButton({
  offerId,
  listingTitle,
}: {
  offerId: string;
  listingTitle: string;
}) {
  return (
    <ConfirmActionButton
      label="Stiahnuť"
      ariaLabel={`Stiahnuť ponuku na ${listingTitle}`}
      question="Stiahnuť ponuku?"
      consequence="Zadávateľ ju už nebude môcť prijať."
      confirmLabel="Áno, stiahnuť"
      pendingLabel="Sťahujem…"
      tone="destructive"
      action={() => decideOffer(offerId, "withdraw")}
    />
  );
}
