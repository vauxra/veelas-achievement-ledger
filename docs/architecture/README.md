# Achieve Ex+ Architecture

This directory is the starting point for future AI/code-review sessions. It maps the current Dalamud plugin shape to the services that own each feature so new work does not duplicate existing mechanics.

## Start here

1. Read `AGENTS.md` for branch, security, and verification rules.
2. Read this file for the repo map.
3. Read `service-boundaries.md` before adding or moving code.
4. Read `dalamud-conventions.md` before touching Dalamud services, ClientStructs, hooks, native UI, or framework/chat/login events.
5. Read `feature-map.md` to find the current owner files for a feature.
6. Read `domain-glossary.md` for stable product and code vocabulary.
7. Read `native-refresh-flow.md` before changing refresh, inspection, activity-trigger, Cosmic, or login/logout behavior.
8. Read `agent-workflow.md` before implementing a change.
9. Check `refactor-backlog.md` for known structure improvements that should remain small and test-backed.

## Product goal

Achieve Ex+ is a Final Fantasy XIV Dalamud plugin that provides a compact achievement ledger:

- `/achex` opens the ledger.
- Users can track selected achievement IDs across logouts.
- Ordinary achievement progress is observed and cached only after the native Achievement UI exposes progress data.
- Row reload/open actions use the native Achievement UI instead of direct server/progress requests.
- Experimental-branch features may add queueing, timed refreshes, activity-triggered refreshes, Cosmic/WKS reads, and debug instrumentation, but they must stay clearly labeled and easy to remove from public-safe branches.

## Top-level layout

| Path | Purpose |
|---|---|
| `AchievementTracker/Plugin.cs` | Dalamud entry point, service construction, command/event wiring, tick fan-out, config saves. |
| `AchievementTracker/Configuration.cs` | Persisted plugin configuration and config normalization. |
| `AchievementTracker/Models/` | Serializable/value types used by services and UI. |
| `AchievementTracker/Services/` | Reusable feature mechanics and policy decisions. Prefer adding pure/testable behavior here. |
| `AchievementTracker/Windows/` | ImGui windows and user-interaction wiring. Keep drawing/layout here. |
| `AchievementTracker.Tests/Program.cs` | Lightweight test harness for pure services and policies. |
| `docs/research/` | Historical/research notes; useful for context but not the primary architecture map. |
| `docs/ai-policy-audits/` | Reviewer/tripwire prompts and policy audit material. |
| `docs/architecture/` | Current agent-facing architecture and workflow docs. |
| `scripts/verify-local.sh` | Local verification pipeline used before handoff/commit. |

## Current structure verdict

The codebase is already mostly aligned with a service-boundary layout:

- `Plugin.cs` orchestrates Dalamud lifecycle and connects services.
- `Windows/*` draw UI and translate clicks/settings into service calls.
- `Services/*` own reusable mechanics: search/catalog, progress display, native queue scheduling, native window policy, activity classification, presets, and Cosmic progress.
- `Models/*` are simple value/config payloads.

The main risk is future duplication: without this architecture map, agents may reimplement queue spacing, preset sanitation, native window policy, progress formatting, or completion filtering in a window or in `Plugin.cs`. Prefer extending existing services and tests.

## Local analysis/tooling context

External reference repos and tool snapshots belong under ignored `local-src/`, not committed source. Local review artifacts belong under ignored `.hermes/reviews/`.

The coding-stack analysis found:

- SharpToolsMCP is viable as a future C#/Roslyn analysis aid after MCP configuration/restart.
- Magellan did not show useful C#/Roslyn support in the fetched source snapshot and should not be part of the default workflow.
- The fetched Dalamud source/log context is local-only and should not be committed.
