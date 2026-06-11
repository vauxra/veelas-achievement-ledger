# Safety map

Version: `v0.2.0.20`

## Related function map entries

- [`Plugin.OpenAchievementForUpdate(...)`](./02-function-call-map.md#plugin-openachievementforupdate)
- [`NativeAchievementNavigator` native UI adapter](./02-function-call-map.md#nativeachievementnavigator)
- [`ClientAchievementProgressSource` bounded cache](./02-function-call-map.md#clientachievementprogresssource)
- [`CosmicClassProgressProvider` WKS score adapter](./02-function-call-map.md#cosmicclassprogressprovider)


This file maps the potentially sensitive areas and what they do.

## No direct direct progress requests

The safe mainline design avoids plugin-originated calls like:

```text
direct achievement-progress request API
RequestProgress
```

The player clicks a button, the plugin opens the native Achievement UI, and then the plugin briefly watches the matching local progress slot during a bounded observation window.

## Native Achievement UI actions

File:

```text
AchievementTracker/Services/NativeAchievementNavigator.cs
```

Calls:

```text
AgentAchievement.Instance()->OpenById(achievementId)
AgentAchievement.Instance()->Hide()
```

Purpose:

- `OpenById` opens the native Achievement entry from a user click.
- `Hide` closes the native Achievement window from a user click.

These are not achievement progress request calls.

## Bounded observed-progress cache

File:

```text
AchievementTracker/Services/ClientAchievementProgressSource.cs
```

Reads:

```text
Achievement.Instance() local progress slot
```

Boundary:

- starts only after [`Plugin.OpenAchievementForUpdate(achievementId)`](./02-function-call-map.md#plugin-openachievementforupdate) opens the native Achievement entry from a user click
- records only if the loaded slot matches the active achievement ID
- expires the observation window
- uses no hooks, signatures, or direct progress requests

## Cosmic Class score reads

File:

```text
AchievementTracker/Services/CosmicClassProgressProvider.cs
```

Reads:

```text
WKSManager.Instance()->State.Scores
```

Gate:

```text
Plugin.RefreshCosmicCacheFromLiveState()
├─ only territory 1237 / Sinus Ardorum
└─ throttled by CosmicCacheRefreshInterval
```

Purpose:

- local WKS/Cosmic score display
- no server call
- no achievement progress request

## Backend/network/privacy checks

The safe mainline should not contain:

```text
HttpClient
WebSocket
Socket
ContentId
telemetry
analytics
leaderboard
packet capture
FireCallback / synthetic addon callbacks
```

Use this quick check from repo root:

```bash
git grep -n -E 'direct achievement-progress request API|RequestProgress\(|HttpClient|WebSocket|ContentId|telemetry|analytics|leaderboard|packet capture|FireCallback' -- AchievementTracker AchievementTracker.Tests
```

Expected result: no matches.
