# Development due diligence

Achieve Ex+ is developed with human review, AI assistance, and repeatable local checks.

## Branch stance

`achieve-ex-experimental` is a private/testing branch, not a normal Dalamud submission candidate. It intentionally explores achievement-progress behavior that the safer public/beta design avoids.

The branch must stay clear about its risks:

- direct plugin-originated achievement progress requests are experimental,
- queued **Update All**, timed auto update, and event-triggered refreshes are experimental,
- framework-update timers may schedule request queues or passive local-state cache refreshes,
- WKS/Cosmic ClientStructs reads are local/read-only and isolated,
- diagnostics are for tester feedback, not polished public UX.

The branch must still avoid unrelated high-risk behavior:

- no movement/pathing automation,
- no crafting/gathering action execution,
- no synthetic addon submissions or confirmation callbacks,
- no packet capture,
- no backend telemetry/cloud sync,
- no command execution from plugin input/config.

## Methodology

- Use official Dalamud docs before adding or changing APIs.
- Prefer Dalamud services and Lumina data before ClientStructs.
- Keep unsafe/native interaction isolated in small service classes.
- Treat direct server-affecting progress requests as experimental/private-branch behavior only.
- Keep UI and docs short, clear, and explicit about risk.

## Manual checks

Before an experimental build, verify in game:

- `/achex` opens the ledger.
- `/achex config`, `/achex configure`, and `/achex man` open config.
- `/achex help` and `/achex ?` open the Help tab.
- Configure can search, add, remove, and reorder up to 20 tracked achievements.
- Presets can save, read, rename, delete, and auto-load on dropdown selection.
- Tracked achievements and presets persist after logout/login.
- Row reload queues a direct progress request for the selected row.
- **Update All** queues tracked updates and skips very recently updated rows.
- Timed auto update waits for its first countdown and resets when auto-timer settings change.
- `Stop Update Tasks` disables auto update and clears queued update tasks.
- Enabled gathering/fishing/crafting/completion event triggers queue only matching scoped updates.
- Cosmic Class achievements show score progress after WKS/Cosmic state has been observed.
- Cached Cosmic scores remain visible outside Cosmic content.
- Help shows the experimental warning and Cosmic diagnostics.

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

For quick iteration, at minimum run:

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet run --project AchievementTracker.Tests/AchievementTracker.Tests.csproj
dotnet build AchievementTracker/AchievementTracker.csproj -c Debug
dotnet build AchievementTracker/AchievementTracker.csproj -c Release
git diff --check
```

## Review

For merge/submission, run a fresh-context review using:

- `docs/ai-policy-audits/adversarial-code-review-agent.md`

For `achieve-ex-experimental`, policy findings about direct progress requests or timers may be warnings if the README/Help clearly label the branch as experimental. Security, lifecycle, crash, privacy, and secret-handling findings remain blockers.

Provide the diff plus verification output.
