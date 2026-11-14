import { getAccessToken } from "@/lib/auth";
import { aiServiceUrl } from "@/lib/agent";
import { forwardProblem, problem } from "@/app/api/agent/problem";

/**
 * Potvrdiť / Zrušiť akciu, na ktorú agent čaká. Či potvrdzuje ten istý používateľ
 * a či potvrdenie ešte platí (5 minút), overí AI služba podľa tokenu.
 */
export async function POST(request: Request) {
  const token = await getAccessToken();
  if (!token) return problem(401, "agent.unauthorized", "Prihlásenie vypršalo. Prihlás sa znova.");

  const body = (await request.json().catch(() => null)) as {
    confirmationId?: unknown;
    approved?: unknown;
  } | null;
  if (typeof body?.confirmationId !== "string" || typeof body.approved !== "boolean") {
    return problem(400, "agent.invalid_confirmation", "Neplatné potvrdenie.");
  }

  let upstream: Response;
  try {
    upstream = await fetch(`${aiServiceUrl}/agent/confirm`, {
      method: "POST",
      headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" },
      body: JSON.stringify({ confirmation_id: body.confirmationId, approved: body.approved }),
    });
  } catch {
    return problem(503, "agent.unavailable", "Asistent teraz nie je dostupný. Skús to o chvíľu.");
  }
  if (!upstream.ok) return forwardProblem(upstream);
  return new Response(null, { status: 204 });
}
