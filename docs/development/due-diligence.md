# Development Due Diligence

Changes are built, tested, scanned, and reviewed before release.

## Guardrails

- Use Dalamud services first; use ClientStructs only where needed.
- No telemetry, backend sync, cloud service, or gameplay automation.
- No automatic achievement progress polling.
- Keep unsafe code isolated.
- Keep debug logging opt-in.
- Dispose hooks, windows, and event handlers cleanly.

## Verification

`./scripts/verify-local.sh HEAD` runs:

- unit tests,
- Debug build,
- Release build,
- CodeQL C# scan,
- Dalamud/AI policy tripwire,
- adversarial review tripwire,
- whitespace check.

## Manual testing

Covered during beta work:

- `/achtrack` open/close,
- configure/search/add/remove/reorder,
- opening native Achievement entries,
- progress updates from native Achievement UI,
- completion state,
- diagnostics toggle/logging.

## Known limits

- Numeric progress is not live at all times.
- Open an achievement in the game UI to refresh its value.
- Some achievements only show completion or target count until the game loads that entry.
