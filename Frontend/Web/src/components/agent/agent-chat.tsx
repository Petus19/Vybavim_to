"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { ArrowUp, RotateCcw, Sparkles, Square, X } from "lucide-react";
import {
  agentRoleLabels,
  readAgentEvents,
  type AgentEvent,
  type AgentMessage,
  type AgentProblem,
  type AgentRole,
} from "@/lib/agent";
import { cn } from "@/lib/utils";
import { ConfirmationCard, MessageStats, RichText, StepRow } from "@/components/agent/agent-parts";

export type AgentStep = {
  kind: "step";
  key: number;
  tool: string;
  summary: string;
  status: "running" | "ok" | "failed";
  result?: string;
  url?: string;
};

export type AgentConfirmation = {
  kind: "confirmation";
  key: number;
  confirmationId: string;
  title: string;
  preview: string;
  expiresAt: number;
  status: "pending" | "sending" | "approved" | "declined" | "expired";
  error?: string;
};

type AgentTurn = {
  kind: "agent";
  items: (AgentStep | AgentConfirmation)[];
  thinking: boolean;
  done: boolean;
  message?: AgentMessage;
  error?: string;
};

type Turn = { kind: "user"; text: string } | AgentTurn;

const ROLE_STORAGE_KEY = "vybavim_agent_role";

const suggestions: Record<AgentRole, string[]> = {
  client: [
    "Potrebujem logo pre kaviareň v Košiciach, rozpočet do 200 €.",
    "Ukáž mi ponuky na môj inzerát s logom a odporuč najlepšiu.",
    "Aké ponuky som dostal?",
  ],
  specialist: [
    "Nájdi mi zákazky na logá do 300 €.",
    "Som grafik, nájdi mi zákazku na logo a pošli ponuku na najlepšiu.",
    "Aké ponuky som poslal?",
  ],
};

let nextKey = 0;

/** Pohodlie: keď sa úložisko nedá prečítať (server, blokované), ostane „Zadávam prácu“. */
function savedRole(): AgentRole {
  try {
    return localStorage.getItem(ROLE_STORAGE_KEY) === "specialist" ? "specialist" : "client";
  } catch {
    return "client";
  }
}

/**
 * AI asistent: plávajúce tlačidlo a panel s konverzáciou. Je v hlavnom layoute, takže konverzácia
 * prežije prechod na inú stránku (napr. klik na odkaz na vytvorený inzerát).
 *
 * Kroky agenta prichádzajú naživo (Server-Sent Events). Keď chce agent niečo zapísať, ukáže sa karta
 * s náhľadom a tlačidlami Potvrdiť / Zrušiť – agent na serveri dovtedy čaká.
 */
export function AgentChat() {
  const router = useRouter();
  const [open, setOpen] = useState(false);
  // Naposledy zvolená rola. Ukazuje sa až v otvorenom paneli, takže server (kde je vždy „client“)
  // a prehliadač sa pri prvom vykreslení nelíšia.
  const [role, setRole] = useState<AgentRole>(savedRole);
  const [turns, setTurns] = useState<Turn[]>([]);
  const [conversationId, setConversationId] = useState<string | null>(null);
  const [input, setInput] = useState("");
  const [working, setWorking] = useState(false);
  const abortRef = useRef<AbortController | null>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);
  const scrollRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (open) inputRef.current?.focus();
  }, [open]);

  // Nové kroky a odpovede posunú konverzáciu dole
  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: "smooth" });
  }, [turns]);

  useEffect(() => {
    if (!open) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open]);

  /** Zmení poslednú odpoveď agenta (udalosti prichádzajú vždy k nej). */
  const patch = useCallback((change: (turn: AgentTurn) => AgentTurn) => {
    setTurns((current) => {
      const last = current.at(-1);
      if (!last || last.kind !== "agent") return current;
      return [...current.slice(0, -1), change(last)];
    });
  }, []);

  const handle = useCallback(
    (event: AgentEvent) => {
      switch (event.type) {
        case "start":
          setConversationId(event.data.conversation_id);
          break;
        case "thinking":
          patch((t) => ({ ...t, thinking: true }));
          break;
        case "tool_call":
          patch((t) => ({
            ...t,
            thinking: false,
            items: [
              ...t.items,
              {
                kind: "step",
                key: nextKey++,
                tool: event.data.tool,
                summary: event.data.summary,
                status: "running",
              },
            ],
          }));
          break;
        case "tool_result": {
          const { tool, ok, summary, url } = event.data;
          patch((t) => {
            const items = [...t.items];
            const index = items.findLastIndex(
              (i) => i.kind === "step" && i.tool === tool && i.status === "running",
            );
            if (index !== -1) {
              const step = items[index] as AgentStep;
              items[index] = { ...step, status: ok ? "ok" : "failed", result: summary, url };
            }
            return { ...t, items };
          });
          // Po zápise (nový inzerát, ponuka, prijatie) obnoví aktuálnu stránku – zmena je hneď vidno
          if (ok && url) router.refresh();
          break;
        }
        case "confirmation_required":
          patch((t) => ({
            ...t,
            items: [
              ...t.items,
              {
                kind: "confirmation",
                key: nextKey++,
                confirmationId: event.data.confirmation_id,
                title: event.data.title,
                preview: event.data.preview,
                expiresAt: Date.now() + event.data.expires_in * 1000,
                status: "pending",
              },
            ],
          }));
          break;
        case "confirmation_result":
        case "confirmation_expired": {
          const id = event.data.confirmation_id;
          const status =
            event.type === "confirmation_expired"
              ? "expired"
              : event.data.approved
                ? "approved"
                : "declined";
          patch((t) => ({
            ...t,
            items: t.items.map((i) =>
              i.kind === "confirmation" && i.confirmationId === id
                ? { ...i, status, error: undefined }
                : i,
            ),
          }));
          break;
        }
        case "message":
          patch((t) => ({ ...t, thinking: false, message: event.data }));
          break;
        case "error":
          patch((t) => ({ ...t, thinking: false, error: event.data.message }));
          break;
      }
    },
    [patch, router],
  );

  async function send(text: string) {
    const message = text.trim();
    if (!message || working) return;
    setInput("");
    setWorking(true);
    setTurns((current) => [
      ...current,
      { kind: "user", text: message },
      { kind: "agent", items: [], thinking: true, done: false },
    ]);

    const controller = new AbortController();
    abortRef.current = controller;
    try {
      const response = await fetch("/api/agent/chat", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ message, conversationId, role }),
        signal: controller.signal,
      });
      if (!response.ok || !response.body) {
        const problem = (await response.json().catch(() => null)) as AgentProblem | null;
        if (problem?.code === "agent.conversation_not_found") setConversationId(null);
        patch((t) => ({
          ...t,
          error:
            problem?.code === "agent.conversation_not_found"
              ? "Konverzácia vypršala. Pošli správu znova – začne sa nová."
              : (problem?.message ?? "Asistent teraz nie je dostupný. Skús to o chvíľu."),
        }));
        return;
      }
      for await (const event of readAgentEvents(response.body)) handle(event);
    } catch {
      patch((t) =>
        controller.signal.aborted
          ? { ...t, error: "Zastavené. Čo agent nestihol potvrdiť, sa nevykonalo." }
          : { ...t, error: "Spojenie s asistentom sa prerušilo." },
      );
    } finally {
      abortRef.current = null;
      setWorking(false);
      patch((t) => ({
        ...t,
        thinking: false,
        done: true,
        // Prúd skončil bez odpovede aj bez chyby – nemalo by sa stať, ale nech to používateľ vie
        error: t.error ?? (t.message ? undefined : "Spojenie s asistentom sa prerušilo."),
        items: t.items.map((i) =>
          // Nevybavené potvrdenie po zastavení či výpadku spojenia server zamietol – nič sa nevykonalo
          i.kind === "confirmation" && (i.status === "pending" || i.status === "sending")
            ? { ...i, status: controller.signal.aborted ? "declined" : "expired" }
            : i.kind === "step" && i.status === "running"
              ? { ...i, status: "failed" }
              : i,
        ),
      }));
      inputRef.current?.focus();
    }
  }

  async function decide(confirmationId: string, approved: boolean) {
    const update = (change: Partial<AgentConfirmation>) =>
      patch((t) => ({
        ...t,
        items: t.items.map((i) =>
          i.kind === "confirmation" && i.confirmationId === confirmationId
            ? { ...i, ...change }
            : i,
        ),
      }));

    update({ status: "sending", error: undefined });
    try {
      const response = await fetch("/api/agent/confirm", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ confirmationId, approved }),
      });
      if (response.ok) return; // výsledok príde ako udalosť z agenta (confirmation_result)
      const problem = (await response.json().catch(() => null)) as AgentProblem | null;
      update(
        response.status === 404
          ? { status: "expired", error: problem?.message }
          : { status: "pending", error: problem?.message ?? "Nepodarilo sa odoslať. Skús znova." },
      );
    } catch {
      update({ status: "pending", error: "Nepodarilo sa odoslať. Skús znova." });
    }
  }

  function reset(nextRole: AgentRole = role) {
    abortRef.current?.abort();
    setTurns([]);
    setConversationId(null);
    setRole(nextRole);
    try {
      localStorage.setItem(ROLE_STORAGE_KEY, nextRole);
    } catch {}
    inputRef.current?.focus();
  }

  if (!open) {
    return (
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="fixed right-5 bottom-5 z-50 inline-flex h-12 items-center gap-2 rounded-full bg-primary px-5 text-base font-medium text-primary-foreground shadow-lg shadow-black/15 transition-opacity hover:opacity-90"
      >
        <Sparkles className="size-5" aria-hidden="true" />
        Asistent
        {working && <span className="size-2 animate-pulse rounded-full bg-primary-foreground" />}
      </button>
    );
  }

  return (
    <section
      role="dialog"
      aria-label="AI asistent"
      className="fixed right-4 bottom-4 z-50 flex h-[min(44rem,calc(100dvh-5rem))] w-[min(28rem,calc(100vw-2rem))] flex-col overflow-hidden rounded-3xl border border-border/60 bg-background shadow-2xl shadow-black/20"
    >
      <header className="flex items-center gap-2 border-b border-border/60 px-4 py-3">
        <Sparkles className="size-5 text-primary" aria-hidden="true" />
        <h2 className="font-semibold tracking-tight">Asistent</h2>
        <div className="ml-auto flex items-center gap-1">
          <button
            type="button"
            onClick={() => reset()}
            disabled={turns.length === 0}
            aria-label="Nová konverzácia"
            title="Nová konverzácia"
            className={iconButtonClass}
          >
            <RotateCcw className="size-4" aria-hidden="true" />
          </button>
          <button
            type="button"
            onClick={() => setOpen(false)}
            aria-label="Zavrieť asistenta"
            className={iconButtonClass}
          >
            <X className="size-5" aria-hidden="true" />
          </button>
        </div>
      </header>

      <div
        role="radiogroup"
        aria-label="Čo robíš"
        className="mx-4 mt-3 grid grid-cols-2 rounded-full bg-surface p-1 text-sm"
      >
        {(["client", "specialist"] as const).map((value) => (
          <button
            key={value}
            type="button"
            role="radio"
            aria-checked={role === value}
            disabled={working}
            onClick={() => value !== role && reset(value)}
            className={cn(
              "h-8 rounded-full font-medium transition-colors disabled:opacity-60",
              role === value
                ? "bg-background shadow-sm"
                : "text-muted-foreground hover:text-foreground",
            )}
          >
            {agentRoleLabels[value]}
          </button>
        ))}
      </div>

      <div ref={scrollRef} className="flex-1 space-y-4 overflow-y-auto px-4 py-4">
        {turns.length === 0 ? (
          <EmptyState role={role} onPick={send} />
        ) : (
          turns.map((turn, index) =>
            turn.kind === "user" ? (
              <p
                key={index}
                className="ml-auto w-fit max-w-[85%] rounded-3xl rounded-br-lg bg-primary px-4 py-2.5 whitespace-pre-line text-primary-foreground"
              >
                {turn.text}
              </p>
            ) : (
              <AgentTurnView key={index} turn={turn} onDecide={decide} />
            ),
          )
        )}
      </div>

      <form
        onSubmit={(event) => {
          event.preventDefault();
          void send(input);
        }}
        className="flex items-end gap-2 border-t border-border/60 p-3"
      >
        <label htmlFor="agent-input" className="sr-only">
          Správa pre asistenta
        </label>
        <textarea
          id="agent-input"
          ref={inputRef}
          rows={1}
          maxLength={2000}
          value={input}
          onChange={(event) => setInput(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter" && !event.shiftKey) {
              event.preventDefault();
              void send(input);
            }
          }}
          placeholder={working ? "Asistent pracuje…" : "Napíš, čo potrebuješ…"}
          className="[field-sizing:content] max-h-32 min-h-11 flex-1 resize-none rounded-3xl bg-surface px-4 py-2.5 text-base outline-none placeholder:text-muted-foreground focus-visible:ring-3 focus-visible:ring-ring/40"
        />
        {working ? (
          <button
            type="button"
            onClick={() => abortRef.current?.abort()}
            aria-label="Zastaviť asistenta"
            title="Zastaviť"
            className={cn(sendButtonClass, "bg-secondary text-foreground")}
          >
            <Square className="size-4 fill-current" aria-hidden="true" />
          </button>
        ) : (
          <button
            type="submit"
            disabled={!input.trim()}
            aria-label="Poslať"
            className={cn(
              sendButtonClass,
              "bg-primary text-primary-foreground disabled:opacity-40",
            )}
          >
            <ArrowUp className="size-5" aria-hidden="true" />
          </button>
        )}
      </form>
    </section>
  );
}

function AgentTurnView({
  turn,
  onDecide,
}: {
  turn: AgentTurn;
  onDecide: (confirmationId: string, approved: boolean) => void;
}) {
  const { message } = turn;
  return (
    <div className="space-y-2">
      {turn.items.length > 0 && (
        <ul aria-label="Kroky asistenta" aria-live="polite" className="space-y-1.5">
          {turn.items.map((item) =>
            item.kind === "step" ? (
              <StepRow key={item.key} step={item} />
            ) : (
              <li key={item.key}>
                <ConfirmationCard confirmation={item} onDecide={onDecide} />
              </li>
            ),
          )}
        </ul>
      )}
      {turn.thinking && !turn.done && (
        <p className="flex items-center gap-2 text-sm text-muted-foreground" aria-live="polite">
          <span className="flex gap-1" aria-hidden="true">
            <span className="size-1.5 animate-bounce rounded-full bg-current [animation-delay:-0.3s]" />
            <span className="size-1.5 animate-bounce rounded-full bg-current [animation-delay:-0.15s]" />
            <span className="size-1.5 animate-bounce rounded-full bg-current" />
          </span>
          Premýšľam…
        </p>
      )}
      {message && (
        <div
          className={cn(
            "max-w-[95%] rounded-3xl rounded-bl-lg px-4 py-3",
            message.stop_reason === "answered"
              ? "bg-surface"
              : "bg-brand-orange/10 text-foreground",
          )}
        >
          <RichText text={message.text} />
          <MessageStats message={message} />
        </div>
      )}
      {turn.error && (
        <p
          role="alert"
          className="rounded-2xl bg-destructive/10 px-4 py-3 text-sm text-destructive"
        >
          {turn.error}
        </p>
      )}
    </div>
  );
}

function EmptyState({ role, onPick }: { role: AgentRole; onPick: (text: string) => void }) {
  return (
    <div className="space-y-4 pt-2">
      <div>
        <p className="font-medium">
          {role === "client" ? "Čo potrebuješ vybaviť?" : "Akú prácu hľadáš?"}
        </p>
        <p className="mt-1 text-sm text-muted-foreground">
          Asistent hľadá v inzerátoch, porovná ponuky a pripraví inzerát či ponuku. Kým nič
          nepotvrdíš, nič sa neodošle.
        </p>
      </div>
      <ul className="space-y-2">
        {suggestions[role].map((text) => (
          <li key={text}>
            <button
              type="button"
              onClick={() => onPick(text)}
              className="w-full rounded-2xl bg-surface px-4 py-3 text-left text-sm transition-colors hover:bg-secondary"
            >
              {text}
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}

const iconButtonClass =
  "inline-flex size-8 items-center justify-center rounded-full text-muted-foreground transition-colors hover:bg-secondary hover:text-foreground disabled:pointer-events-none disabled:opacity-40";
const sendButtonClass =
  "inline-flex size-11 shrink-0 items-center justify-center rounded-full transition-opacity hover:opacity-90";
