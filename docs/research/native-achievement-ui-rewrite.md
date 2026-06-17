# Native Achievement UI flow

Current beta flow:

1. User tracks achievements in `/achex`.
2. User clicks the row reload icon or **Update Next**.
3. The native Achievement window opens that entry.
4. The plugin passively records progress returned to the native UI.

Boundaries:

- no plugin-originated progress requests,
- no automatic refresh loop,
- no advanced diagnostics UI,
- no synthetic menu clicks or packet/network automation.
