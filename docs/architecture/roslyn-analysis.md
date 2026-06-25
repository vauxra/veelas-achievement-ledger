# Roslyn Analysis Workflow

Use this page when Graphify's broad code map is not precise enough and the question needs compiler-aware C# semantics.

## Tool roles

| Tool | Role | Good for | Not good for |
|---|---|---|---|
| Graphify | Broad generated orientation graph | Finding hubs, likely owners, cross-file topology, call-flow HTML, quick navigation questions. | Exact C# overload/type/reference correctness. |
| SharpToolsMCP / Roslyn | Compiler-aware C# semantic analysis | Exact references, symbol definitions, interface implementations, inheritance, complexity, type resolution, solution/project maps. | Broad product/design authority. |
| `docs/architecture/` + Dalamud docs | Human-authored source of truth | Ownership, safety boundaries, Dalamud policy/API conventions, public-vs-experimental scope. | Mechanical symbol lookup. |

Default order:

1. Read `AGENTS.md` and relevant `docs/architecture/*`.
2. Query `graphify-out/graph.json` for broad orientation.
3. Use SharpToolsMCP/Roslyn when the next question requires exact C# semantics.
4. Read source/tests directly before editing.

## Local SharpToolsMCP snapshot

Fetched local source path:

```text
local-src/opensrc/repos/github.com/kooshi/SharpToolsMCP/main
```

This path is ignored and must not be committed. Re-fetch or rebuild it locally if missing.

Build command:

```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
dotnet build local-src/opensrc/repos/github.com/kooshi/SharpToolsMCP/main/SharpTools.sln --nologo
```

Current local build result during this analysis branch:

- build succeeded,
- 7 warnings from the SharpToolsMCP snapshot,
- 0 errors.

The warnings were in external ignored source, mostly one `NuGet.Protocol` low-severity advisory and nullable/obsolete warnings in SharpToolsMCP itself. They are not Achieve Ex+ warnings.

## Stdio server path

After building, the stdio server binary is expected at:

```text
local-src/opensrc/repos/github.com/kooshi/SharpToolsMCP/main/SharpTools.StdioServer/bin/Debug/net8.0/SharpTools.StdioServer
```

Recommended arguments for local Achieve Ex+ analysis:

```bash
--log-directory local-src/sharptools-logs
--log-level Information
--disable-git
```

`--disable-git` is intentional. Hermes already manages repo edits/commits; SharpTools should be used here as an analysis aid, not as an autonomous git branch/commit actor.

## Hermes MCP configuration shape

Do not edit global Hermes MCP config from a repo task unless the user explicitly asks to wire the current Hermes profile. If wiring is requested, use Hermes's native MCP client and restart Hermes afterward.

Project-specific config shape to add under `mcp_servers` in `~/.hermes/config.yaml`:

```yaml
mcp_servers:
  sharptools_achex:
    command: "/mnt/mintData/git/achieve-ex/local-src/opensrc/repos/github.com/kooshi/SharpToolsMCP/main/SharpTools.StdioServer/bin/Debug/net8.0/SharpTools.StdioServer"
    args:
      - "--log-directory"
      - "/mnt/mintData/git/achieve-ex/local-src/sharptools-logs"
      - "--log-level"
      - "Information"
      - "--disable-git"
    timeout: 180
    connect_timeout: 60
```

After restart, MCP tools should be registered with a prefix similar to:

```text
mcp_sharptools_achex_SharpTool_LoadSolution
mcp_sharptools_achex_SharpTool_FindReferences
mcp_sharptools_achex_SharpTool_ViewDefinition
mcp_sharptools_achex_SharpTool_AnalyzeComplexity
```

Exact names depend on Hermes's MCP tool-name normalization and the server's advertised tool names.

## First calls after MCP restart

Load the solution before asking symbol questions:

```text
SharpTool_LoadSolution(solutionPath: "/mnt/mintData/git/achieve-ex/AchievementTracker.sln")
```

Then use Roslyn-backed tools for questions like:

- find all references to `AchievementProgressUpdater.EnqueueUpdateAll`,
- inspect the exact type hierarchy around `IAchievementProgressSource`,
- analyze complexity of `TrackerWindow` or `AchievementProgressUpdater`,
- list implementations of an interface or abstract member,
- verify a refactor's reference updates across the solution.

## Guardrails

- Use SharpToolsMCP for analysis unless the user explicitly asks for Roslyn-backed edits.
- If allowing edits, keep `--disable-git` and let Hermes own commits.
- Never let SharpTools replace the normal verification pipeline.
- Continue to run:

```bash
./scripts/verify-local.sh HEAD
```

before handoff or commits that affect code/policy.
