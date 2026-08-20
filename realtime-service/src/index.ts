import { createServer } from "node:http";
import { Server } from "socket.io";
import { config } from "./config.js";
import { relayChatStream } from "./chatRelay.js";

const httpServer = createServer();
const io = new Server(httpServer, {
  cors: { origin: config.corsOrigin, methods: ["GET", "POST"] },
});

io.use((socket, next) => {
  const token = socket.handshake.auth?.token;
  if (typeof token !== "string" || token.length === 0) {
    next(new Error("unauthorized"));
    return;
  }
  next();
});

io.on("connection", (socket) => {
  let inFlight = false;

  socket.on("chat:ask", async ({ question }: { question: string }) => {
    if (inFlight) {
      socket.emit("chat:error", { message: "A question is already in progress." });
      return;
    }

    inFlight = true;
    const controller = new AbortController();
    const onDisconnect = () => controller.abort();
    socket.once("disconnect", onDisconnect);

    try {
      await relayChatStream(socket, socket.handshake.auth.token as string, question, controller.signal);
    } catch (err) {
      if (!controller.signal.aborted) {
        socket.emit("chat:error", { message: "Failed to reach the chat service." });
      }
    } finally {
      socket.off("disconnect", onDisconnect);
      inFlight = false;
    }
  });
});

httpServer.listen(config.port, () => {
  console.log(`realtime-service listening on :${config.port}`);
});
