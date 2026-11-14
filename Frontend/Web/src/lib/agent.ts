/**
 * AI asistent (agent z Backend/AiService): typy udalostí a čítanie ich prúdu.
 *
 * Prehliadač volá iba tento web – `/api/agent/chat` a `/api/agent/confirm` (Route Handlery).
 * Tie pridajú token z httpOnly cookie a požiadavku pošlú AI službe, takže token sa do JavaScriptu
 * v prehliadači nikdy nedostane. AI služba posiela kroky agenta ako Server-Sent Events.
 */

export type AgentRole = "client" | "specialist";

export const agentRoleLabels: Record<AgentRole, string> = {
  client: "Zadávam prácu",
  specialist: "Hľadám prácu",
};

/** Udalosti z `POST /agent/chat` – presne podľa `Backend/AiService/src/ai_service/agent/web.py`. */
export type AgentEvent =
  | { type: "start"; data: { conversation_id: string; provider: string } }
  | { type: "thinking"; data: { step: number } }
  | { type: "tool_call"; data: { tool: string; summary: string } }
  | {
      type: "tool_result";
      data: { tool: string; ok: boolean; summary: string; url?: string };
    }
  | {
      type: "confirmation_required";
      data: {
        confirmation_id: string;
        tool: string;
        title: string;
        preview: string;
        expires_in: number;
      };
    }
  | { type: "confirmation_result"; data: { confirmation_id: string; approved: boolean } }
  | { type: "confirmation_expired"; data: { confirmation_id: string } }
  | { type: "message"; data: AgentMessage }
  | { type: "error"; data: { message: string } };

export type AgentStopReason = "answered" | "step_limit" | "llm_error";

export type AgentMessage = {
  text: string;
  stop_reason: AgentStopReason;
  stats: {
    provider: string;
    llm_calls: number;
    tool_calls: number;
    input_tokens: number;
    output_tokens: number;
    cost_usd: number | null;
    seconds: number;
  };
};

/** Chyba pred začatím prúdu (neprihlásený, agent pracuje, služba nebeží) – `{ code, message }`. */
export type AgentProblem = { code: string; message: string };

/**
 * Prečíta SSE prúd po udalostiach. Udalosť končí prázdnym riadkom; riadky „: ping“
 * sú len udržiavanie spojenia a preskočia sa.
 */
export async function* readAgentEvents(
  body: ReadableStream<Uint8Array>,
): AsyncGenerator<AgentEvent> {
  const reader = body.getReader();
  const decoder = new TextDecoder(); // UTF-8; znak rozdelený medzi dva kúsky poskladá správne
  let buffer = "";
  try {
    while (true) {
      const { value, done } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true }).replaceAll("\r\n", "\n");
      let end: number;
      while ((end = buffer.indexOf("\n\n")) !== -1) {
        const block = buffer.slice(0, end);
        buffer = buffer.slice(end + 2);
        const event = parseBlock(block);
        if (event) yield event;
      }
    }
  } finally {
    reader.releaseLock();
  }
}

function parseBlock(block: string): AgentEvent | null {
  let type = "";
  let data = "";
  for (const line of block.split("\n")) {
    if (line.startsWith("event: ")) type = line.slice(7);
    else if (line.startsWith("data: ")) data += line.slice(6);
  }
  if (!type || !data) return null;
  try {
    return { type, data: JSON.parse(data) } as AgentEvent;
  } catch {
    return null;
  }
}

/** „ollama/qwen2.5:7b“ → „qwen2.5:7b“, „anthropic/claude-opus-5-5“ → „claude-opus-5-5“. */
export function modelName(provider: string): string {
  return provider.includes("/") ? provider.slice(provider.indexOf("/") + 1) : provider;
}

/** Adresa AI služby – volá ju iba server webu (Route Handlery), nie prehliadač. */
export const aiServiceUrl = process.env.AI_SERVICE_URL ?? "http://127.0.0.1:8000";
