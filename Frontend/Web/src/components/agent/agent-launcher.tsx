import { AgentChat } from "@/components/agent/agent-chat";
import { getCurrentUser } from "@/lib/auth";

/**
 * AI asistent iba pre prihlásených – koná s ich oprávneniami (token z cookie).
 * Keď API nebeží, asistent sa nezobrazí, zvyšok stránky funguje ďalej.
 */
export async function AgentLauncher() {
  const user = await getCurrentUser().catch(() => null);
  return user ? <AgentChat /> : null;
}
