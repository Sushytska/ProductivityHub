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
    socket.emit("chat:error", { message: `Upstream API returned ${res.status}` });
    return;
  }

  const parser = createParser({
    onEvent(event: EventSourceMessage) {
      if (!event.event || !KNOWN_EVENTS.has(event.event)) {
        return;
      }
      const payload = event.data ? JSON.parse(event.data) : {};
      socket.emit(`chat:${event.event}`, payload);
    },
  });

  const reader = res.body.getReader();
  const decoder = new TextDecoder();

  while (true) {
    const { done, value } = await reader.read();
    if (done) {
      break;
    }
    parser.feed(decoder.decode(value, { stream: true }));
  }
}
