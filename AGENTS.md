# Achieve Ex+ Agent Guide

This repo is a Final Fantasy XIV Dalamud plugin. Treat Dalamud's official docs at <https://dalamud.dev> as the source of truth.

## Architecture map

Before adding or moving code, read the agent-facing architecture docs:

- `docs/architecture/README.md` — current repo map and product goal.
- `docs/architecture/service-boundaries.md` — which layer/service owns each behavior.
- `docs/architecture/dalamud-conventions.md` — Dalamud, ClientStructs, hook, and native UI rules.
- `docs/architecture/feature-map.md` — feature-to-file/test ownership.
- `docs/architecture/domain-glossary.md` — stable terms for achievement/progress/update concepts.
- `docs/architecture/native-refresh-flow.md` — native refresh, inspection, activity, Cosmic, and cache-reset flows.
- `docs/architecture/agent-workflow.md` — workflow for future AI/code-review sessions.
- `docs/architecture/refactor-backlog.md` — conservative structure improvements to keep small and test-backed.

Do not duplicate existing services. Extend the documented owner for queueing,
native Achievement navigation, search/completion filtering, preset sanitation,
progress display, activity triggers, and Cosmic/WKS progress behavior.

## Before code changes

- Read the relevant Dalamud docs before changing APIs or ClientStructs code.
- Prefer documented Dalamud services first, then Lumina/local data, then ClientStructs only when needed.
- Keep `unsafe`/ClientStructs code isolated in small adapter classes.
- Do not store raw pointers across frames.
- Subscribe/unsubscribe events symmetrically and dispose hooks/resources.

## Product shape

The public/beta-safe shape for Achieve Ex+ is intentionally small:

- `/achex` opens the ledger.
- the row reload icon opens the native Achievement entry.
- Numeric progress is cached only when the native Achievement UI returns progress data.
- Tracked achievement IDs persist between logouts; observed ordinary achievement progress cache resets on login/logout.

For public/beta-safe branches, do not reintroduce:

- plugin-originated achievement progress requests,
- refresh queues/throttlers,
- automatic polling or background request loops,
- advanced diagnostics UI,
- packet capture/network experiments,
- backend sync, telemetry, analytics, or leaderboards.

## `achieve-ex-experimental` branch stance

This branch is explicitly **not meant for Dalamud publishing**. When the current
branch is `achieve-ex-experimental`, treat Dalamud publishing restrictions as risk
guidance and documentation requirements, not hard blockers. It is acceptable on
this branch to prototype direct `RequestAchievementProgress` calls, automatic
progress refresh queues, event-triggered refreshes, seconds-based auto timers,
local WKS/Cosmic ClientStructs reads, cached Cosmic score display, and debug
instrumentation if the implementation is clearly labeled experimental, keeps
unsafe/ClientStructs code isolated, remains easy to remove from public branches,
and avoids unrelated security/privacy risks.

Do still block on:

- secrets or credentials,
- command execution from plugin input/config,
- backend/network telemetry without explicit user direction,
- event/timer lifecycle leaks,
- raw pointer lifetime bugs,
- crashes from missing null checks,
- changes that accidentally position this branch as official-submission safe.

Local tripwires may downgrade Dalamud-publishing policy violations to warnings
on `achieve-ex-experimental`; security and lifecycle failures remain blockers.

## Security review requirements

Before commit, check:

- no secrets or credentials,
- no plugin input/config passed to shell/process execution,
- no path traversal risks,
- no dynamic assembly loading or dynamic code execution,
- no unsafe deserialization,
- no network calls without explicit design/privacy review,
- no background tasks/timers that survive disposal.

## Verification pipeline

Run before committing:

```bash
./scripts/verify-local.sh HEAD
```

This runs:

- unit tests,
- Debug build,
- Release build,
- CodeQL C# security/quality scan,
- AI/Dalamud policy tripwire,
- adversarial code-review tripwire,
- `git diff --check`.

For merge/submission, also run a fresh-context reviewer using:

- `docs/ai-policy-audits/adversarial-code-review-agent.md`

## Commit hygiene

- Keep feature/policy changes small.
- Do not commit `bin/`, `obj/`, `released/`, `.hermes/`, `local-src/`, or ad-hoc generated analysis output.
- Exception: `graphify-out/` is intentionally committed on analysis branches as an AI-orientation artifact.
- If a review finds issues, fix only those issues and re-run verification.

## Graphify orientation graph

This branch keeps a generated Graphify knowledge graph in `graphify-out/` for broad AI orientation. Use it to discover files, symbols, communities, and cross-file relationships before falling back to broad grep-style searches.

Graphify is **not** the C# source of truth. Keep using Roslyn-based tooling such as SharpToolsMCP for deep C# semantics, exact references, type resolution, and compiler-aware analysis.

Rules:
- For codebase-orientation questions, query the committed graph first when `graphify-out/graph.json` exists:
  - `uvx --from graphifyy graphify query "<question>" --graph graphify-out/graph.json`
  - `uvx --from graphifyy graphify path "<A>" "<B>" --graph graphify-out/graph.json`
  - `uvx --from graphifyy graphify explain "<concept>" --graph graphify-out/graph.json`
- Treat `docs/architecture/` and Dalamud docs as the authoritative design guidance; treat Graphify as a generated navigation aid.
- Do not install Graphify git hooks in this repo.
- When architecture, service boundaries, or major code topology changes, manually regenerate and commit `graphify-out/` with the same branch slice.
- Keep `.graphifyignore` conservative so headless regeneration stays local/API-key-free; Markdown architecture docs are read directly, not semantically extracted into the graph.

Regeneration command:

```bash
rm -rf graphify-out
uvx --from graphifyy graphify extract . --no-cluster --out .
uvx --from graphifyy graphify cluster-only . --graph graphify-out/graph.json --no-label
uvx --from graphifyy graphify export callflow-html
```
