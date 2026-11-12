import { getAccessToken } from "@/lib/auth";
import { aiServiceUrl, type AgentRole } from "@/lib/agent";
import { forwardProblem, problem } from "@/app/api/agent/problem";

/**
 * Správa pre AI asistenta. Pridá token z httpOnly cookie a pošle ju AI službe;
 * jej prúd udalostí (Server-Sent Events) vráti prehliadaču bez zmeny – kroky agenta tak
 * vidno hneď, ako sa stanú. Keď prehliadač spojenie zavrie, zruší sa aj požiadavka na AI službu
 * (`signal`) a tá čakajúce potvrdenie zamietne – bez kliknutia sa nič nezapíše.
 */
export async function POST(request: Request) {
  const token = await getAccessToken();
  if (!token) return problem(401, "agent.unauthorized", "Pre asistenta sa najprv prihlás.");

  const body = (await request.json().catch(() => null)) as {
    message?: unknown;
    conversationId?: unknown;
    role?: unknown;
  } | null;
  const message = typeof body?.message === "string" ? body.message.trim() : "";
  if (!message || message.length > 2000) {
    return problem(400, "agent.invalid_message", "Napíš správu (najviac 2 000 znakov).");
  }
  const role: AgentRole = body?.role === "specialist" ? "specialist" : "client";
  const conversationId = typeof body?.conversationId === "string" ? body.conversationId : null;

  let upstream: Response;
  try {
    upstream = await fetch(`${aiServiceUrl}/agent/chat`, {
      method: "POST",
      headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" },
      body: JSON.stringify({ message, role, conversation_id: conversationId }),
      signal: request.signal,
    });
  } catch {
    return problem(503, "agent.unavailable", "Asistent teraz nie je dostupný. Skús to o chvíľu.");
  }
  if (!upstream.ok || !upstream.body) return forwardProblem(upstream);

  return new Response(upstream.body, {
    headers: {
      "Content-Type": "text/event-stream; charset=utf-8",
      "Cache-Control": "no-cache, no-transform",
      "X-Accel-Buffering": "no",
    },
  });
}
