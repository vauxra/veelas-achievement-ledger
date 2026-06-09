# Veela's Achievement Ledger Agent Guide

This repo is a Final Fantasy XIV Dalamud plugin. Treat Dalamud's official docs at <https://dalamud.dev> as the source of truth.

## Required pre-work before code changes

1. Read the relevant official Dalamud docs before touching code:
   - <https://dalamud.dev/plugin-development/interaction/> for safe API → ClientStructs → raw-memory escalation.
   - <https://dalamud.dev/plugin-publishing/restrictions> for automation, server interaction, combat/PvP, privacy, and backend restrictions.
   - <https://dalamud.dev/plugin-development/technical-considerations> for Windowing API, Lumina/local data, backend/privacy/performance guidance.
   - API pages for any service being added or changed, e.g. `IClientState`, `IUnlockState`, `IDataManager`.
2. For non-obvious API behavior, inspect local Dalamud XML/DLL metadata under `/home/developer/.xlcore/dalamud/Hooks/dev/` before guessing.
3. Record reusable findings in `docs/research/`.

## Dalamud coding rules

- Prefer documented Dalamud services and wrappers first.
- Use Lumina/local game data over external APIs for game data.
- Use ClientStructs only when a Dalamud API does not expose the needed behavior.
- Keep all `unsafe`/ClientStructs code inside small adapter classes behind interfaces.
- Do not store raw pointers across frames.
- Null-check `Instance()` pointers and validate request/response state before reading fields.
- Avoid raw memory, signatures, hooks, and unmanaged calls unless explicitly designed and policy-reviewed.
- Use Dalamud Windowing API for plugin windows and keep window objects referenced by the plugin.
- Subscribe/unsubscribe every Dalamud event symmetrically; dispose/cancel timers, tasks, and resources.
- Keep per-frame draw/update work cheap. Heavy work needs explicit event/time gating and must not hit game-server request paths automatically.

## Achievement progress policy

V1 is intentionally conservative:

- Passive local reads are okay.
- `IUnlockState.IsAchievementComplete(row)` is authoritative when the achievement list is loaded.
- Lumina target counts are local and okay.
- ClientStructs numeric progress requests must remain manual/user-triggered, queued, de-duplicated, and throttled.
- Clear progress cache, pending queue, and throttle state on login/logout or character/session changes.
- Do not add timers, framework-update loops, addon lifecycle handlers, login/zone/job/level events, or background tasks that call achievement progress request methods.
- Do not add backend sync, telemetry, analytics, leaderboards, WebSockets, or `HttpClient` in V1.

See `docs/research/auto-update-mechanisms.md` and `docs/research/numeric-achievement-progress.md`.

## Security review requirements

Apply C#/.NET secure-coding review before commit:

- No hardcoded secrets, tokens, passwords, API keys, or private keys.
- No command/process execution from plugin input/config.
- No path traversal risks from unvalidated file paths.
- No dynamic assembly loading or dynamic code execution.
- No unsafe deserialization.
- No network calls without explicit V1 design approval, timeout/error handling, and privacy review.
- No background tasks/timers that can continue after plugin disposal.
- Log meaningful failure context; do not swallow policy/security failures silently.

## Verification pipeline

Run before committing:

```bash
./scripts/verify-local.sh master
```

This runs:

- unit tests
- Debug build
- Release build
- CodeQL C# security/quality scan
- AI/Dalamud policy tripwire
- adversarial code-review tripwire
- `git diff --check`

For merge/submission, also run an independent fresh-context reviewer using:

- `docs/ai-policy-audits/adversarial-code-review-agent.md`

Provide that reviewer the diff plus outputs from both audit scripts.

## Commit hygiene

- Keep feature/policy changes in small commits.
- Do not commit `bin/`, `obj/`, `.hermes/`, or generated/transient analysis output.
- If a review finds issues, fix only the reported issues and re-run the full verification pipeline plus the independent reviewer.
