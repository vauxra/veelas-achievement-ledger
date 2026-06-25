# Agent Workflow

Follow this workflow when an AI agent or reviewer works in this repository.

## 1. Gather context before editing

- Read `AGENTS.md`.
- Read `docs/architecture/README.md` and the architecture doc relevant to the feature.
- For broad orientation, query the committed Graphify graph before broad source searches:
  - `uvx --from graphifyy graphify query "<question>" --graph graphify-out/graph.json`
  - `uvx --from graphifyy graphify path "<A>" "<B>" --graph graphify-out/graph.json`
- Search for the existing owner before creating a new service or helper.
- For Dalamud APIs, native UI, ClientStructs, or hooks, read the relevant Dalamud docs first.
- Inspect neighboring files and tests before changing code.

Useful local commands:

```bash
git status --short --branch
git branch --show-current
dotnet test AchievementTracker.sln
./scripts/verify-local.sh HEAD
```

## 2. Choose the owner deliberately

Use `service-boundaries.md`:

- UI-only layout/change: `AchievementTracker/Windows/*`.
- Dalamud lifecycle/event subscription/config save routing: `Plugin.cs`.
- Reusable or duplicate-prone mechanics: `AchievementTracker/Services/*`.
- Persisted value/config state: `Configuration.cs` or `Models/*`.
- Architecture/process guidance: `docs/architecture/*` and `AGENTS.md`.

If the change would duplicate queueing, native window policy, search filtering, preset sanitation, progress display, or activity classification, extend the existing service instead.

## 3. Prefer testable service changes

Before changing a complex UI/native path, look for a pure service/policy test to add or extend in `AchievementTracker.Tests/Program.cs`.

Good extraction candidates:

- Pure decisions around eligibility, search filters, trigger classification, row status, or display formatting.
- Config normalization or migration rules.
- Scheduler ordering/dedupe/backoff policies.

Risky extraction candidates:

- Native Achievement open/park/restore/close ordering.
- Hook detours.
- Framework update sequencing.
- Anything requiring in-game addon pointer state.

## 4. Dalamud/native safety checklist

Before finalizing changes involving Dalamud/native code:

- No raw pointer stored across frames.
- Null/readiness/address checks before dereference.
- Events and hooks unsubscribe/dispose symmetrically.
- No background task/timer survives disposal.
- No plugin input/config reaches shell/process execution.
- No network/backend telemetry without explicit design/privacy review.
- Public-safe branch behavior remains small unless explicitly working on `achieve-ex-experimental`.

## 5. External reference/tool workflow

Approved external references may be fetched under ignored `local-src/` for local analysis. Do not commit those sources.

Current guidance:

- Use committed `graphify-out/` as an AI-orientation/navigation artifact for broad topology questions. It is generated from code/project manifests only so regeneration stays local and API-key-free.
- Regenerate Graphify manually after architecture, service-boundary, or major code-topology changes; do not install Graphify git hooks in this repo.
- Use SharpToolsMCP as the optional C#/Roslyn MCP analysis aid after configuring Hermes MCP and restarting the session; see `roslyn-analysis.md` for the local build/server shape.
- Prefer SharpToolsMCP/Roslyn over Graphify for exact C# references, type resolution, compiler-aware navigation, or semantic correctness questions.
- Do not rely on Magellan by default for Achieve Ex+ C# analysis; the fetched snapshot did not show useful C#/Roslyn support.
- Keep raw logs and tool output ignored. Promote durable conclusions into committed docs.

Graphify regeneration:

```bash
bash scripts/regenerate-graphify.sh
```

## 6. Verification

For small pure service/doc changes:

```bash
dotnet test AchievementTracker.sln
git diff --check
```

Before commit or handoff of code/policy changes:

```bash
./scripts/verify-local.sh HEAD
```

This script runs unit tests, Debug/Release builds, CodeQL, AI/Dalamud policy tripwires, adversarial review tripwires, and whitespace checks.

If verification finds issues, fix only the relevant issue and rerun the narrow test first, then rerun the broader pipeline.

## 7. Handoff format

When handing off a local build or branch, keep it terse:

- Branch and commit/HEAD.
- Changed files.
- Verification commands and results.
- Any blocked follow-up, especially if Greptile/Greploop requires a pushed branch/PR.
