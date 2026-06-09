# Achievement proxy/cache surface scan

Date: 2026-06-09
Branch: `rewrite-guided-achievement-ui`

## Question

After add-on developer guidance mentioned network/proxy/cache views, check what achievement-related information is exposed locally through Dalamud docs, ClientStructs XML, and Lumina metadata.

## Local docs scanned

- `/home/micheal/.xlcore/dalamud/Hooks/dev/FFXIVClientStructs.xml`
- `/home/micheal/.xlcore/dalamud/Hooks/dev/Dalamud.xml`
- `/home/micheal/.xlcore/dalamud/Hooks/dev/Lumina.Excel.xml`
- Project source and research notes

Search themes:

- `InfoProxy`, `InfoModule`, `NetworkHandlers`
- `Achievement`, `AgentAchievement`, `AchievementCategory`
- `Cache`, `CompletedAchievements`, `History`
- `ReceiveAchievementProgress`, `RequestAchievementProgress`

## Findings

### No obvious achievement info proxy

The local metadata does **not** expose an obvious `InfoProxyAchievement` or public Dalamud achievement proxy/cache wrapper.

`Dalamud.Game.Network.Structures.InfoProxy.*` wrappers found locally are character/social-list oriented, for example `InfoProxy.CharacterData`, with fields such as name, worlds, language, class/job, and statuses. That is not an achievement progress cache.

`Dalamud.Game.Network.Internal.NetworkHandlers` exposes market-board observables and content-finder pop handling, not achievement progress observables.

### Public Dalamud achievement surface remains completion-only

Dalamud exposes:

- `IUnlockState.IsAchievementListLoaded`
- `IUnlockState.IsAchievementComplete(Lumina.Excel.Sheets.Achievement)`

This is useful for complete/incomplete state after achievement data has loaded, but it does not expose numeric current/max progress.

### ClientStructs achievement singleton is the local cache/state surface

`FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement` exposes:

- `State`
- `IsLoaded()`
- `CompletedAchievements`
- `CompletedAchievementsBitArray`
- `History` — last five achievement IDs
- `ProgressRequestState`
- `ProgressAchievementId`
- `ProgressCurrent`
- `ProgressMax`
- `RequestAchievementProgress(uint id)`
- `ReceiveAchievementProgress(uint id, uint current, uint max)`
- `SetAchievementCompleted(uint achievementId)`

This confirms the numeric progress cache is a single active/last progress slot, not per-achievement local state for every tracked row.

### AgentAchievement surface is UI/agent oriented

The local metadata shows `AgentAchievement` inherits generic agent/addon methods and exposes `OpenById`, `ReceiveEvent`, `OnGameEvent`, addon status helpers, and config/job/level callbacks. I did not find clean category/subcategory selection APIs.

Generic `AtkValue`/callback/event routes exist, but using them to synthesize category/subcategory/achievement clicks would require reverse-engineering native callback values. That is less moderator-safe than a user-guided checklist and could look like fake user input.

## Design implication

The safest next UI shape is:

- keep direct `OpenById` buttons only for A/B testing,
- add a guided manual state machine for the moderator-safe path,
- do not synthesize category/subcategory clicks,
- passively observe `ReceiveAchievementProgress` and completion events,
- show `IUnlockState` completion and local Lumina category/name/target hints,
- continue `/xldata network` observation manually as external evidence only.

## Open questions for manual testing

- Does `/xldata network` show any info-proxy/cache entry when the native Achievement UI is open besides opcode `800 / 0x320`?
- Does selecting category/subcategory without selecting an achievement cause any useful local state changes in `AgentAchievement.ReceiveEvent` logs?
- Does `Achievement.History` update on direct/native UI selection consistently enough to help guide or confirm selected entries?

## Boundary

Do not convert proxy/network observations into packet automation, synthetic requests, or automated UI traversal without explicit maintainer approval.
