# Native Achievement UI Rewrite Notes

Date: 2026-06-09
Branch: `rewrite/native-achievement-ui-guided-v1`

## Add-on dev guidance captured from discussion

- Do not try to imitate the game's timing or automatically walk achievement tabs/entries.
- Automatic updates are difficult to do correctly because many criteria are server-side or content-specific.
- A safer baseline is a UI that tells the player when data is stale and lets the player open the native Achievement window/entry themselves.
- Replacing or augmenting the Achievement window with a custom ImGui/KTK-like window can be technically possible through agent callbacks, but the value is better presentation/search, not bypassing tracking limitations.
- `/xldata network` may help inspect zone packet / info-proxy exposure during manual testing, but that should remain research/diagnostics. It should not become packet automation or synthetic requests.

## Rewrite direction

The experimental rewrite removes the tracker-originated progress refresh queue and uses this flow instead:

1. User tracks achievements in `/achtrack`.
2. User clicks **Open in Achievements** or **Open next in Achievements**.
3. The plugin opens the native game Achievement UI entry via `AgentAchievement.OpenById` from that direct user click.
4. If the native client receives achievement progress data, passive observation records the current/max values in the local cache.
5. The tracker displays when each value was observed and from which passive source.

## Boundaries

- No polling.
- No backend or telemetry.
- No plugin-originated `Achievement.RequestAchievementProgress` calls.
- No synthetic packets or automatic `/xldata network` driven behavior.
- Verbose logs remain opt-in under **Advanced diagnostics**.

## Follow-up research

Use `/xldata network` manually in a dev environment while opening native Achievement entries to see whether zone packet or info-proxy views expose useful achievement-adjacent state. Treat findings as evidence for display/search only unless maintainers confirm a specific surface is acceptable for plugin use.
