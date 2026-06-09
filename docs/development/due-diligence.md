# Development Due Diligence

Achievement Tracker was developed with conservative Dalamud-plugin guardrails and verification before release cleanup.

## Guardrails

- Prefer documented Dalamud services and local Lumina data before ClientStructs.
- Avoid plugin-originated achievement progress requests. Prefer user-driven native Achievement UI interactions and passive observation of client state/responses already caused by that UI.
- Do not add automatic polling, gameplay automation, telemetry, backend sync, or cloud services.
- Keep ClientStructs and unmanaged-access code isolated in small services; see `https://dalamud.dev/plugin-development/interaction/` for the documented safe API → ClientStructs → raw-memory escalation model.
- Keep passive observation hooks small, local, and non-requesting; gate verbose diagnostic surfaces behind an opt-in setting.
- Dispose event handlers, hooks, windows, and diagnostic surfaces on shutdown/toggle-off.

## Testing performed

Local verification is run with:

```bash
./scripts/verify-local.sh HEAD
```

The verification script covers:

- unit tests,
- Debug build,
- Release build,
- CodeQL C# security/quality scan,
- Dalamud policy / AI tripwire,
- adversarial code-review tripwire,
- whitespace diff check.

Manual in-game testing covered:

- `/achtrack` command and UI toggles,
- achievement search/add/remove/reorder,
- guided native Achievement window opening,
- passive capture of native Achievement window progress responses,
- completion events,
- gathering/mining chat/log/condition surfaces.

## Review process

Policy-sensitive changes were reviewed with deterministic scripts and independent fresh-context adversarial review focused on:

- plugin-originated game-server requests,
- hook lifecycle and disposal safety,
- stale cache correctness,
- chat/log privacy risk,
- backend/network additions,
- C#/.NET security hazards.

## Research records

Relevant findings are kept under `docs/research/`, including:

- numeric achievement progress sources,
- achievement progress debug hooks,
- native Achievement UI/agent exploration,
- gameplay activity surfaces,
- beta cleanup checklist,
- packet-capture and `/xldata network` experiment notes on experimental branches.

## Known limitations

- Numeric current progress is not continuously live; users open tracked entries in the native Achievement window when they want current values.
- Some achievements expose only target counts or completion state until the native UI has loaded that entry.
- Advanced diagnostics are for troubleshooting and can produce verbose Dalamud logs.
