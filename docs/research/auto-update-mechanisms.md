# Runtime update approach

Current decision: no automatic achievement progress refresh.

The beta uses a user-guided flow:

1. User clicks `↻` or **Update Next**.
2. The native Achievement window opens the selected entry.
3. The plugin passively caches progress returned to the native UI.

Rejected for beta:

- plugin-originated progress requests,
- queued/throttled refresh loops,
- timers or framework-update polling,
- gameplay-event-driven refreshes,
- packet capture or network experiments.

This keeps behavior small, reviewable, and aligned with Dalamud policy expectations.
