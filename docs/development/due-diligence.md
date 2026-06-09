# Development due diligence

Veela's Achievement Ledger is developed with human review, AI assistance, and repeatable local checks.

## Methodology

- Use official Dalamud docs before adding or changing APIs.
- Prefer Dalamud services and Lumina data before ClientStructs.
- Keep unsafe/native interaction isolated.
- Avoid plugin-originated achievement progress requests, polling, telemetry, or backend sync.
- Keep UI and docs short and user-facing.

## Manual checks

Before a beta build, verify in game:

- `/val` opens the ledger.
- Configure can search, add, remove, and reorder tracked achievements.
- Tracked achievements persist after logout/login.
- `↻` opens the chosen native Achievement entry.
- **Update Next** opens the next unobserved or oldest-observed tracked entry.
- Progress updates when the native Achievement UI returns data.
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
