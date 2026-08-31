import { createParser, type EventSourceMessage } from "eventsource-parser";
import type { Socket } from "socket.io";
import { config } from "./config.js";

const KNOWN_EVENTS = new Set(["meta", "token", "done", "error"]);

// Thin 1:1 passthrough: fetches the .NET SSE stream and re-emits each frame as a
// Socket.IO event of the same name (`chat:${event.event}`), with the parsed JSON
// payload. No business logic lives here — .NET owns the RAG + generation pipeline.
export async function relayChatStream(
  socket: Socket,
  token: string,
  question: string,
  signal: AbortSignal,
): Promise<void> {
  const res = await fetch(`${config.dotnetApiBaseUrl}/api/chat/stream`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify({ question }),
    signal,
  });

  if (!res.ok || !res.body) {
    // Release the connection back to the pool instead of leaving it unconsumed.
    await res.body?.cancel();
    socket.emit("chat:error", { message: `Upstream API returned ${res.status}` });
    return;
  }

  // Set once a terminal SSE frame (done/error) has been parsed and emitted, so the read
  // loop below can stop as soon as the client has everything it needs — rather than
  // waiting for this fetch's body to hit true EOF, which leaves a race window where the
  // client sees chat:done and sends the next chat:ask before this function (and the
  // inFlight guard in index.ts that depends on it) has actually resolved.
  let terminal = false;

  const parser = createParser({
    onEvent(event: EventSourceMessage) {
      if (!event.event || !KNOWN_EVENTS.has(event.event)) {
        return;
      }
      let payload: unknown = {};
      try {
        payload = event.data ? JSON.parse(event.data) : {};
      } catch {
        socket.emit("chat:error", { message: "Received a malformed event from the chat service." });
        terminal = true;
        return;
      }
      socket.emit(`chat:${event.event}`, payload);
      if (event.event === "done" || event.event === "error") {
        terminal = true;
      }
    },
  });

  const reader = res.body.getReader();
  const decoder = new TextDecoder();

  while (!terminal) {
    const { done, value } = await reader.read();
    if (done) {
      break;
    }
    parser.feed(decoder.decode(value, { stream: true }));
  }

  // Release the connection back to the pool instead of leaving it unconsumed —
  // relevant when we broke out early on `terminal`, since the body may not be at EOF yet.
  await reader.cancel().catch(() => {});
}
