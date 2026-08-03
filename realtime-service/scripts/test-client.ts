// Throwaway manual verification script — not part of the running relay service.
// Usage: TEST_JWT=<jwt> npm run test:client
import { io } from "socket.io-client";

const token = process.env.TEST_JWT;
if (!token) {
  console.error("Set TEST_JWT to a valid .NET-issued JWT before running this script.");
  process.exit(1);
}

const port = process.env.PORT ?? "4000";
const socket = io(`http://localhost:${port}`, { auth: { token } });

socket.on("connect", () => {
  console.log("connected, asking...");
  socket.emit("chat:ask", { question: process.argv[2] ?? "What did I write about X?" });
});

socket.on("chat:meta", (payload) => console.log("[meta]", payload));
socket.on("chat:token", (payload) => process.stdout.write(payload.text));
socket.on("chat:done", () => {
  console.log("\n[done]");
  socket.disconnect();
  process.exit(0);
});
socket.on("chat:error", (payload) => {
  console.error("\n[error]", payload);
  socket.disconnect();
  process.exit(1);
});

socket.on("connect_error", (err) => {
  console.error("connect_error:", err.message);
  process.exit(1);
});
