"use client";

import { Fragment, useEffect, useState, type ReactNode } from "react";
import Link from "next/link";
import { Check, CircleAlert, LoaderCircle, ShieldCheck } from "lucide-react";
import { modelName, type AgentMessage } from "@/lib/agent";
import { cn } from "@/lib/utils";
import type { AgentConfirmation, AgentStep } from "@/components/agent/agent-chat";

/** Jeden krok agenta: „Hľadám inzeráty: logo, do 300 €“ a pod ním, čo z toho vyšlo. */
export function StepRow({ step }: { step: AgentStep }) {
  return (
    <li className="flex gap-2 text-sm">
      <span className="mt-0.5 shrink-0" aria-hidden="true">
        {step.status === "running" ? (
          <LoaderCircle className="size-4 animate-spin text-muted-foreground" />
        ) : step.status === "ok" ? (
          <Check className="size-4 text-brand" />
        ) : (
          <CircleAlert className="size-4 text-muted-foreground" />
        )}
      </span>
      <span className="min-w-0">
        <span className={cn(step.status === "running" && "text-muted-foreground")}>
          {step.summary}
        </span>
        {step.result && !(step.url && step.status === "ok") && (
          <span className="block truncate text-xs text-muted-foreground" title={step.result}>
            {step.result}
          </span>
        )}
        {step.url && step.status === "ok" && (
          <InternalOrExternalLink
            href={step.url}
            className="mt-1 inline-flex text-sm font-medium text-primary hover:underline"
          >
            Otvoriť na webe →
          </InternalOrExternalLink>
        )}
      </span>
    </li>
  );
}

/**
 * Agent chce niečo zapísať a čaká. Náhľad je presne to, čo sa vykoná – rovnaký text,
 * aký v termináli ukazuje CLI. Bez kliknutia na „Potvrdiť“ sa nič nestane.
 */
export function ConfirmationCard({
  confirmation,
  onDecide,
}: {
  confirmation: AgentConfirmation;
  onDecide: (confirmationId: string, approved: boolean) => void;
}) {
  const { status } = confirmation;
  const waiting = status === "pending" || status === "sending";
  const [heading, ...lines] = confirmation.preview.split("\n");

  return (
    <div
      className={cn(
        "my-1 rounded-3xl border p-4 transition-colors",
        waiting ? "border-brand-orange/40 bg-brand-orange/5" : "border-border/60 bg-surface",
      )}
    >
      <p className="flex items-center gap-2 text-sm font-semibold">
        <ShieldCheck
          className={cn("size-4", waiting ? "text-brand-orange" : "text-muted-foreground")}
          aria-hidden="true"
        />
        {confirmation.title}
        {waiting && <Countdown expiresAt={confirmation.expiresAt} />}
      </p>
      <p className="mt-2 text-sm">{heading}</p>
      <PreviewLines lines={lines} />

      {waiting ? (
        <div className="mt-4 flex gap-2">
          <button
            type="button"
            disabled={status === "sending"}
            onClick={() => onDecide(confirmation.confirmationId, true)}
            className={cn(buttonClass, "bg-primary text-primary-foreground hover:opacity-90")}
          >
            {status === "sending" ? "Posielam…" : "Potvrdiť"}
          </button>
          <button
            type="button"
            disabled={status === "sending"}
            onClick={() => onDecide(confirmation.confirmationId, false)}
            className={cn(buttonClass, "bg-background hover:bg-secondary")}
          >
            Zrušiť
          </button>
        </div>
      ) : (
        <p
          className={cn(
            "mt-3 text-sm font-medium",
            status === "approved" ? "text-brand" : "text-muted-foreground",
          )}
        >
          {status === "approved"
            ? "✓ Potvrdené"
            : status === "declined"
              ? "Zrušené – nič sa nevykonalo"
              : "Potvrdenie vypršalo – nič sa nevykonalo"}
        </p>
      )}
      {confirmation.error && (
        <p role="alert" className="mt-2 text-sm text-destructive">
          {confirmation.error}
        </p>
      )}
    </div>
  );
}

/** Riadky náhľadu „  Cena:       250 €“ → dvojice názov / hodnota; ostatné ako text. */
function PreviewLines({ lines }: { lines: string[] }) {
  const rows = lines.filter((line) => line.trim());
  if (rows.length === 0) return null;
  return (
    <dl className="mt-2 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-sm">
      {rows.map((line, index) => {
        const match = /^\s*([^:]{1,20}):\s+(.+)$/.exec(line);
        return match ? (
          <Fragment key={index}>
            <dt className="text-muted-foreground">{match[1]}</dt>
            <dd className="min-w-0 break-words">{match[2]}</dd>
          </Fragment>
        ) : (
          <dd key={index} className="col-span-2 text-muted-foreground">
            {line.trim()}
          </dd>
        );
      })}
    </dl>
  );
}

function Countdown({ expiresAt }: { expiresAt: number }) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, []);
  const seconds = Math.max(0, Math.round((expiresAt - now) / 1000));
  const label = `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}`;
  return (
    <span
      className="ml-auto text-xs font-normal text-muted-foreground tabular-nums"
      title="Potom sa akcia nevykoná"
    >
      platí {label}
    </span>
  );
}

/** Model, čas a odhad ceny – vo videu vidno, že model sa dá prepnúť a čo stojí. */
export function MessageStats({ message }: { message: AgentMessage }) {
  const { stats } = message;
  const parts = [
    modelName(stats.provider),
    `${stats.seconds.toLocaleString("sk-SK")} s`,
    stats.cost_usd != null
      ? `~$${stats.cost_usd.toLocaleString("sk-SK", { maximumFractionDigits: 4 })}`
      : null,
  ].filter(Boolean);
  return <p className="mt-2 text-xs text-muted-foreground">{parts.join(" · ")}</p>;
}

/**
 * Odpoveď modelu: odseky, odrážky, **tučné** a odkazy. Bez knižnice na Markdown –
 * text vykresľuje React ako text, takže HTML z odpovede modelu sa nikdy nevykoná.
 */
export function RichText({ text }: { text: string }) {
  const blocks: ReactNode[] = [];
  let list: string[] = [];
  const flushList = () => {
    if (list.length > 0) {
      blocks.push(
        <ul key={blocks.length} className="list-disc space-y-1 pl-5">
          {list.map((item, i) => (
            <li key={i}>{inline(item)}</li>
          ))}
        </ul>,
      );
      list = [];
    }
  };

  for (const raw of text.split("\n")) {
    const line = raw.trim();
    const bullet = /^(?:[-*•]|\d+[.)])\s+(.*)$/.exec(line);
    if (bullet) {
      list.push(bullet[1]);
      continue;
    }
    flushList();
    if (line) {
      const heading = /^#{1,4}\s+(.*)$/.exec(line);
      blocks.push(
        <p key={blocks.length} className={cn(heading && "font-semibold")}>
          {inline(heading ? heading[1] : line)}
        </p>,
      );
    }
  }
  flushList();
  return <div className="space-y-2 leading-relaxed break-words">{blocks}</div>;
}

const INLINE =
  /\*\*(.+?)\*\*|\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)|(https?:\/\/[^\s)]+[^\s).,;:!?])/g;

function inline(text: string): ReactNode[] {
  const parts: ReactNode[] = [];
  let last = 0;
  for (const match of text.matchAll(INLINE)) {
    if (match.index > last) parts.push(text.slice(last, match.index));
    const [, bold, label, labelUrl, bareUrl] = match;
    if (bold) {
      parts.push(<strong key={match.index}>{bold}</strong>);
    } else {
      parts.push(
        <InternalOrExternalLink
          key={match.index}
          href={(labelUrl ?? bareUrl)!}
          className="font-medium text-primary underline-offset-2 hover:underline"
        >
          {label ?? "Otvoriť na webe →"}
        </InternalOrExternalLink>,
      );
    }
    last = match.index + match[0].length;
  }
  if (last < text.length) parts.push(text.slice(last));
  return parts;
}

/** Odkaz na tento web (inzerát, ponuka) ostane v aplikácii – asistent sa nezavrie. */
function InternalOrExternalLink({
  href,
  className,
  children,
}: {
  href: string;
  className?: string;
  children: ReactNode;
}) {
  let internal: string | null = null;
  try {
    const origin = typeof window === "undefined" ? "http://localhost" : window.location.origin;
    const url = new URL(href, origin);
    // Agent dáva odkazy s adresou webu (AGENT_WEB_BASE_URL) – v aplikácii z nich ostane iba cesta
    if (url.origin === origin || url.hostname === "localhost") {
      internal = url.pathname + url.search;
    }
  } catch {}
  if (internal) {
    return (
      <Link href={internal} className={className}>
        {children}
      </Link>
    );
  }
  return (
    <a href={href} target="_blank" rel="noopener noreferrer" className={className}>
      {children}
    </a>
  );
}

const buttonClass =
  "inline-flex h-9 items-center rounded-full px-4 text-sm font-medium transition-colors focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none disabled:pointer-events-none disabled:opacity-60";
