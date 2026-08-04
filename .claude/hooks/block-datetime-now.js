// PreToolUse hook (Edit|Write): blocks introducing DateTime.Now in API-server/**/*.cs.
// Postgres columns are timestamp with time zone; Npgsql throws on insert for
// DateTime.Kind = Local. Must use DateTime.UtcNow instead. See CLAUDE.md.
let raw = "";
process.stdin.on("data", (chunk) => (raw += chunk));
process.stdin.on("end", () => {
  let input;
  try {
    input = JSON.parse(raw);
  } catch {
    process.exit(0);
  }
  if (!input || typeof input !== "object") process.exit(0);

  const toolName = input.tool_name;
  if (toolName !== "Edit" && toolName !== "Write") process.exit(0);

  // Lowercased: Windows filesystems are case-insensitive, so API-server/api-server
  // must be treated as the same path.
  const filePath = (input.tool_input && input.tool_input.file_path) || "";
  const normalized = filePath.replace(/\\/g, "/").toLowerCase();
  const isCsInSource = normalized.includes("api-server/") && normalized.endsWith(".cs");
  if (!isCsInSource) process.exit(0);

  const content =
    (input.tool_input && (input.tool_input.content ?? input.tool_input.new_string)) || "";

  if (/\bDateTime\.Now\b/.test(content)) {
    console.log(
      JSON.stringify({
        hookSpecificOutput: {
          hookEventName: "PreToolUse",
          permissionDecision: "deny",
          permissionDecisionReason:
            "DateTime.Now is not allowed in API-server source — use DateTime.UtcNow. " +
            "Postgres columns are timestamptz and Npgsql throws on insert for DateTime.Kind=Local " +
            "(this has already caused real bugs here: AuthService token expiry, CreatedDate defaults). " +
            "See CLAUDE.md.",
        },
      })
    );
  }
  process.exit(0);
});
