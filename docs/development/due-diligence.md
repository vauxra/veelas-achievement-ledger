# Development due diligence

Achieve Ex+ is developed with human review, AI assistance, and repeatable local checks.

## Methodology

- Use official Dalamud docs before adding or changing APIs.
- Prefer Dalamud services and Lumina data before ClientStructs.
- Keep unsafe/native interaction isolated in small service classes.
- Avoid plugin-originated achievement progress requests, request queues, polling, telemetry, or backend sync.
- Keep the assisted progress flow user-guided: a click opens the native Achievement entry, then the plugin passively caches progress returned by the client.
- Keep Cosmic Class score support read-only and local-state based.
- Keep UI and docs short and user-facing.

## Manual checks

Before a beta build, verify in game:

- `/achex` opens the ledger.
- `/achex config`, `/achex configure`, and `/achex man` open configuration.
- `/achex help` and `/achex ?` open the Help tab.
- Configure can search, add, remove, and reorder tracked achievements.
- Top, Up, Down, and Bottom reorder controls behave correctly.
- Presets save, load/read, rename, and delete reusable tracked lists.
- Selecting a preset loads it immediately.
- Tracked achievements and presets persist after logout/login.
- The row reload icon opens the chosen native Achievement entry.
- **Update Next** opens the next unobserved or oldest-observed tracked entry.
- Progress updates when the native Achievement UI returns data.
- Cosmic Class score progress appears after WKS/Cosmic score data has been observed.
- No scheduled refresh UI, bulk request queue, game-event-driven refresh queue, or direct progress-request UI is present.
- No advanced diagnostics UI is present.

## Automated checks

Run:

```bash
./scripts/verify-local.sh HEAD
```

Coverage:

- unit tests,
- Debug build,
- Release build,
- CodeQL C# security/quality scan,
- AI/Dalamud policy tripwire,
- adversarial code-review tripwire,
- whitespace diff check.

## Review

For merge/submission, run a fresh-context review using:

- `docs/ai-policy-audits/adversarial-code-review-agent.md`

Provide the diff plus verification output.
