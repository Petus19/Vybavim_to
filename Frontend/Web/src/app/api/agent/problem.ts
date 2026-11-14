import type { AgentProblem } from "@/lib/agent";

/** Chyba pre prehliadač v jednom tvare: `{ code, message }` (správa po slovensky). */
export function problem(status: number, code: string, message: string) {
  return Response.json({ code, message } satisfies AgentProblem, { status });
}

/** Chyba z AI služby (`{ detail: { code, message } }`) → rovnaký tvar ako `problem`. */
export async function forwardProblem(upstream: Response) {
  const body = (await upstream.json().catch(() => null)) as { detail?: unknown } | null;
  const detail = body?.detail as Partial<AgentProblem> | undefined;
  if (detail && typeof detail.code === "string" && typeof detail.message === "string") {
    return problem(upstream.status, detail.code, detail.message);
  }
  return problem(
    upstream.status >= 500 ? 503 : upstream.status,
    "agent.unavailable",
    "Asistent teraz nie je dostupný. Skús to o chvíľu.",
  );
}
