// PreToolUse hook (Edit|Write): blocks writing a non-empty Anthropic:ApiKey value
// into any appsettings*.json file. Unlike JWT:Key/Postgres password (deliberately
// plaintext in this local-only project), the Anthropic key carries real cost-abuse
// risk if leaked and must stay in `dotnet user-secrets`. See CLAUDE.md.
const fs = require("fs");

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

  const toolInput = input.tool_input || {};
  const filePath = toolInput.file_path || "";
  const basename = filePath.replace(/\\/g, "/").split("/").pop() || "";
  const isAppSettings = /^appsettings.*\.json$/i.test(basename);
  if (!isAppSettings) process.exit(0);

  let content;
  if (toolName === "Write") {
    content = toolInput.content || "";
  } else {
    // Edit's new_string alone can omit the "ApiKey" label (e.g. an edit that only
    // replaces the value of an existing "ApiKey": "" entry), so reconstruct the
    // resulting file content instead of scanning new_string in isolation.
    let existing = "";
    try {
      existing = fs.readFileSync(filePath, "utf8");
    } catch {
      existing = "";
    }
    const oldString = toolInput.old_string ?? "";
    const newString = toolInput.new_string ?? "";
    content =
      existing && existing.includes(oldString)
        ? existing.replace(oldString, newString)
        : `${existing}\n${newString}`;
  }

  // Case-insensitive: ASP.NET Core config binding matches property names
  // case-insensitively (apiKey / APIKEY / ApiKey all bind to the same key).
  const match = content.match(/"ApiKey"\s*:\s*"([^"]*)"/i);
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
