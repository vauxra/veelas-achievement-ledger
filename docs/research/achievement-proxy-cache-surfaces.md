# Progress cache behavior

Current behavior:

- The native Achievement UI can return numeric progress for an opened entry.
- The plugin passively caches that `(current, max)` value.
- Cached progress is session-local and resets on login/logout.
- Completion state still comes from `IUnlockState` when available.

Avoid synthetic UI clicks, packet automation, and plugin-originated progress requests unless maintainers approve a separate design.
