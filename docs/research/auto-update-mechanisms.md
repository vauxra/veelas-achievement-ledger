# In-game achievement progress cache auto-update research

Date: 2026-06-08  
Branch: `auto-update`

## Corrected scope

This note is about **runtime cache update criteria inside the plugin**, not release packaging. The question is how our tracker should decide when cached tracked-achievement progress is fresh, stale, or safe to refresh while the player naturally progresses achievements.

## Dalamud guidance that constrains the design

Sources checked from the local official docs:

- `https://dalamud.dev/plugin-development/interaction/`
- `https://dalamud.dev/plugin-publishing/restrictions`
- `https://dalamud.dev/plugin-development/technical-considerations`
- `https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IClientState`
- `https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IUnlockState`
- `https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IDataManager`

Relevant guidance:

1. Prefer Dalamud APIs first, then ClientStructs, then raw memory/signatures only if needed.
2. Prefer Lumina/local game data over external APIs for game data.
3. Do not interact with game servers automatically, including polling data or making requests without direct user interaction.
4. Be mindful of performance; avoid work every frame unless it is cheap.
5. `IClientState` exposes lifecycle events we can use to scope caches: `Login`, `Logout`, `TerritoryChanged`, `ClassJobChanged`, `LevelChanged`, etc.
6. `IUnlockState` safely exposes loaded/completed status, but not numeric current counters.

Implication for Veela's Achievement Ledger:

- Passive local reads and local cache invalidation are fine.
- User-clicked progress refresh is fine.
- Background server-request polling for numeric achievement progress is risky and should be avoided unless reviewed/explicitly accepted.

## What the compared plugins do

### Price Insight

Purpose: show market-board prices on item hover, with Universalis data.

Source: <https://github.com/kouzukii/ffxiv-priceinsight>

Patterns found:

- Uses event-driven updates from UI/game context, not blind per-frame fetching.
- Hooks item tooltip lifecycle with `AddonLifecycle` `PostRequestedUpdate` and only fetches when an item tooltip exists / item is hovered.
- Has an explicit refresh gesture: `RefreshWithAlt`; tapping Alt while hovering removes the cache entry and cancels active task.
- Uses `EasyCaching.InMemory` with a 90-minute TTL for price data.
- Tracks in-flight work in `activeTasks` so repeated lookups do not spawn duplicate requests.
- Uses a `ConcurrentQueue` and `PeriodicTimer` to batch queued item requests every 200ms, up to 50 at a time.
- Prefetches inventory prices only on meaningful inventory events and with throttles:
  - inventory update: at most once/minute
  - saddlebag open: at most once/30 seconds
  - retainer open: at most once/5 seconds
- Clears cache on logout.

Useful lesson for us:

- Maintain per-id cache entries.
- De-duplicate queued refreshes.
- Track in-flight requests.
- Use user-visible/game-context triggers and throttles.
- Clear character-scoped cache on login/logout.

Caution:

- Price Insight talks to Universalis, an external web API, not the game server. Our `RequestAchievementProgress` uses the game/client achievement request path, so our automatic refresh bar should be stricter.

### PaissaHouse

Purpose: crowdsourced housing availability and lottery tracking.

Source: <https://github.com/zhudotexe/FFXIV_PaissaHouse>

Patterns found:

- Uses a `SweepState` for knowledge artifacts gathered from game state.
- A sweep has clear identity and freshness criteria: world, district, seen ward numbers, and a 10-minute time window.
- Starts a new sweep when world/district changes or sweep age exceeds 10 minutes.
- Ignores duplicate ward info already seen in the current sweep.
- Debounces/batches outgoing ingest with a 1200ms debounce queue.
- HTTP retries use delay+jitter and log failures.
- WebSocket reconnect uses bounded attempts and randomized backoff.

Useful lesson for us:

- Cache entries should have a scope and freshness criteria.
- Progress cache scope should be at least current character/session.
- Refresh queues should de-duplicate and should not re-request data if a recent answer exists.
- If we ever add backend/network behavior, use debounce/backoff/jitter rather than loops.

Caution:

- PaissaHouse is designed around a backend and explicit crowdsourcing. Our V1 should avoid external services.

### Glamaholic

Purpose: save/share/swap glamour plates.

Source: <https://github.com/caitlyn-gg/Glamaholic>

Patterns found:

- Uses `IFramework.Update`, but gates periodic work with a 5-second timestamp check.
- The periodic update is for inter-plugin status (`Glamourer.RefreshStatus`), not high-frequency data fetching.
- Most state is user-managed saved config/artifacts, with explicit saves.

Useful lesson for us:

- Framework update handlers are acceptable for cheap checks, but expensive or side-effecting work must be time-gated.
- Status polling should be about cheap/local/plugin state, not server requests.

### Simple Tweaks

Purpose: framework for many opt-in quality-of-life tweaks.

Source: <https://github.com/Caraxi/SimpleTweaksPlugin>

Patterns found:

- Has an event-controller abstraction with `FrameworkUpdateAttribute { NthTick }` so tweaks can run only every N ticks.
- Uses `AddonLifecycle` pre/post setup/update/refresh events extensively to react only when relevant UI/game add-ons change.
- Caches ClientStructs signature resolution to a plugin config file (`csSigCache.json`).
- Clears localization cache on reload.
- For online localization, uses timestamp freshness windows (language list/update roughly hourly) and user-facing notifications.

Useful lesson for us:

- Prefer add-on/client events and Nth-tick/time gates over raw per-frame logic.
- Keep cached knowledge artifacts clearable/reloadable.
- If using framework update at all, run only cheap checks per frame and gate any heavier work.

### FC Name Color

Purpose: color FC members' nameplates using local nameplate events plus Lodestone data.

Source: <https://github.com/WesselKuipers/FCNameColor>

Patterns found:

- Hooks `INamePlateGui.OnNamePlateUpdate` and `OnDataUpdate`; update work happens when nameplate data changes.
- Uses `IClientState.Login` + `IFramework.Update` once after login because `LocalPlayer` may not be ready immediately.
- Caches fetched character/FC data in config by player/FC id.
- Uses `LastUpdated` timestamps; skips additional FC updates if data is under ~11/12 hours old.
- Schedules additional FC updates with a 30-second delay between FCs.
- Has a `skipCache` of entity IDs that are known not to need recoloring; clears it when FC data changes.
- Clears/loading/error/cooldown state on failures and retries later rather than hammering.

Useful lesson for us:

- Login is not always ready immediately; use a flag + framework update if local player context is required.
- Character-scoped cache should be reset or keyed by character.
- Negative/skip caches are useful but must be invalidated when source data changes.
- Stale windows should be measured in terms appropriate to the source. For achievement progress, cache can become stale immediately after a relevant player action, so timestamps alone are not enough.

### Universalis

Purpose: service/API and client libraries for market data; not a Dalamud plugin in the official PluginMaster snapshot checked.

Patterns found:

- Release workflows and client package publishing are useful for build/release automation, but not directly relevant to in-game cache refresh.

## Criteria for our progress cache

### Cache scope

Progress cache is character/session-scoped. Do not carry cached numeric progress across logout/login or character swaps.

Implemented on this branch:

- `IAchievementProgressSource.ClearCache()`
- `ProgressRefreshQueue.Clear()`
- `ProgressRequestThrottler.Clear()`
- `Plugin` subscribes to `IClientState.Login` and `Logout` and resets progress state.

### Source priority

When displaying a tracked achievement:

1. If `IUnlockState.IsAchievementListLoaded` and `IsAchievementComplete(row)` is true, display complete/max immediately. This local completion signal is authoritative and should override stale numeric cache.
2. Else, if a numeric ClientStructs result exists in the per-achievement cache, display it.
3. Else, if Lumina target count is known, display `Current unavailable / target`.
4. Else, display incomplete/unavailable/load-status text.

Implemented on this branch:

- `AchievementProgressService` now checks loaded+complete before cached numeric progress, so stale `current/max` cannot hide a newly completed achievement.

### Passive cache updates

Safe to do automatically:

- Call `ClientAchievementProgressSource.UpdateCache()` while drawing or processing the queue. It only reads the client’s current shared achievement progress response slot and stores it by achievement id when already loaded.
- Invalidate/reset cache on login/logout.
- Override stale numeric cache with local completion state.

This is passive/local and does not request data.

### User-triggered requests

Safe current behavior:

- The visible `Refresh Progress` button queues currently tracked achievements.
- Queue de-duplicates ids.
- Requests process one at a time and are throttled per achievement.
- Unsafe/ClientStructs request code is isolated in `ClientAchievementProgressSource`.

### What not to do yet

Do **not** add a timer that calls `RequestAchievementProgress` every N seconds/minutes for tracked achievements. That looks like automatic polling/making requests without direct interaction, which the Dalamud restriction docs explicitly warn against.

Do **not** treat every framework tick, territory change, class/job change, or level change as permission to request achievement progress. Those are good invalidation hints, but not direct user intent to make a server-path request.

## Candidate future refinement

If manual refresh is too clunky, the least-risky improvement would be an explicit opt-in setting such as:

- `Refresh tracked progress after opening the tracker`
- `Refresh at most once per tracked achievement per 5 minutes`
- `Only while not in combat/PvP and while the tracker window is visible`
- `Stop after one pass; no repeating timer`

Even then, mark it for policy review because it still causes automatic game/client requests.

## Current recommended V1 behavior

Ship with:

- Passive cache capture.
- Character/session cache reset.
- Completion-state override.
- Manual refresh button.
- Clear UI language that progress is refreshed on demand.

That gives us correct cache hygiene without risking an automatic request loop.
