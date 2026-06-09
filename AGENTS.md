# Veela's Achievement Ledger Agent Guide

This repo is a Final Fantasy XIV Dalamud plugin. Treat Dalamud's official docs at <https://dalamud.dev> as the source of truth.

## Before code changes

- Read the relevant Dalamud docs before changing APIs or ClientStructs code.
- Prefer documented Dalamud services first, then Lumina/local data, then ClientStructs only when needed.
- Keep `unsafe`/ClientStructs code isolated in small adapter classes.
- Do not store raw pointers across frames.
- Subscribe/unsubscribe events symmetrically and dispose hooks/resources.

## Current product shape

Veela's Achievement Ledger is intentionally small:

- `/val` opens the ledger.
- Users track up to five achievements.
- the row reload icon and **Update Next** open the native Achievement entry.
- Numeric progress is cached only when the native Achievement UI returns progress data.
- Tracked achievement IDs persist between logouts; observed progress cache resets on login/logout.

Do not reintroduce:

- plugin-originated achievement progress requests,
- refresh queues/throttlers,
- automatic polling or background request loops,
- advanced diagnostics UI,
- packet capture/network experiments,
- backend sync, telemetry, analytics, or leaderboards.

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
- Do not commit `bin/`, `obj/`, `released/`, `.hermes/`, or generated analysis output.
- If a review finds issues, fix only those issues and re-run verification.
