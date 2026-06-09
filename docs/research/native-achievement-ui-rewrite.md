# Native Achievement UI Notes

Date: 2026-06-09

## Current approach

1. User tracks achievements in `/val`.
2. User clicks **Open** or **Open next**.
3. The game Achievement window opens that entry.
4. The tracker records progress when the game returns it.

## Boundaries

- No polling.
- No backend or telemetry.
- No gameplay automation.
- No plugin-originated progress refresh loop.
- Extra logs stay behind **Advanced diagnostics**.

## Research notes

Local docs show `AgentAchievement.OpenById(...)`, completion state, and one current progress slot. They do not show a clean achievement proxy or category/subcategory API.

Use `/xldata network` for manual research only; do not automate it without maintainer approval.
