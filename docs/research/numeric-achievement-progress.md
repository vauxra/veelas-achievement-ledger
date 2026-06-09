# Numeric achievement progress

Current beta behavior:

- The plugin does not send achievement progress requests.
- `↻` and **Update Next** open the native Achievement entry.
- When the game receives progress for that entry, the plugin passively caches `(current, max)`.
- `IUnlockState` remains the source for completion state when the achievement list is loaded.
- Lumina data is used only for local names, categories, points, and target hints.

Do not reintroduce queued refresh requests, throttlers, timers, or automatic polling without a separate design and policy review.
