// PreToolUse hook (Edit|Write): blocks writing a non-empty Anthropic:ApiKey value
// into any appsettings*.json file. Unlike JWT:Key/Postgres password (deliberately
// plaintext in this local-only project), the Anthropic key carries real cost-abuse
// risk if leaked and must stay in `dotnet user-secrets`. See CLAUDE.md.
let raw = "";
process.stdin.on("data", (chunk) => (raw += chunk));
process.stdin.on("end", () => {
  let input;
  try {
    input = JSON.parse(raw);
  } catch {
    process.exit(0);
  }

  const toolName = input.tool_name;
  if (toolName !== "Edit" && toolName !== "Write") process.exit(0);

  const filePath = (input.tool_input && input.tool_input.file_path) || "";
  const basename = filePath.replace(/\\/g, "/").split("/").pop() || "";
  const isAppSettings = /^appsettings.*\.json$/i.test(basename);
  if (!isAppSettings) process.exit(0);

  const content =
    (input.tool_input && (input.tool_input.content ?? input.tool_input.new_string)) || "";

  const match = content.match(/"ApiKey"\s*:\s*"([^"]*)"/);
  if (match && match[1].trim().length > 0) {
    console.log(
      JSON.stringify({
        hookSpecificOutput: {
          hookEventName: "PreToolUse",
          permissionDecision: "deny",
          permissionDecisionReason:
            "Anthropic:ApiKey must not be written into appsettings*.json with a real value — " +
            'set it via `dotnet user-secrets set "Anthropic:ApiKey" "<key>"` from API-server/ instead. ' +
            "See CLAUDE.md (Local dev gotchas).",
        },
      })
    );
  }
  process.exit(0);
});
