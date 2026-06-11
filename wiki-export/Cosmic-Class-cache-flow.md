# Cosmic Class cache flow

Version: `v0.2.0.22`

## Short answer to your question

This function is **where the plugin decides whether to update the Cosmic cache**:

```csharp
private void RefreshCosmicCacheFromLiveState()
{
    if (ClientState.TerritoryType != SinusArdorumTerritoryTypeId)
    {
        this.nextCosmicCacheRefreshAt = DateTimeOffset.MinValue;
        return;
    }

    var now = DateTimeOffset.UtcNow;
    if (now < this.nextCosmicCacheRefreshAt)
    {
        return;
    }

    this.nextCosmicCacheRefreshAt = now + CosmicCacheRefreshInterval;
    this.CosmicClassProgressProvider.RefreshCacheFromLiveScores();
}
```

But it is **not where the score is actually read**.

The actual score read happens here:

```csharp
private unsafe int[]? TryReadLiveScores(bool saveWhenAvailable = true)
{
    var manager = WKSManager.Instance();
    if (manager is null || !manager->IsLoaded)
    {
        return null;
    }

    var liveScores = manager->State.Scores.ToArray();
    ...
}
```

## Full call chain

```text
Dalamud framework tick
└─ Plugin.OnFrameworkUpdate(IFramework framework)
   └─ Plugin.RefreshCosmicCacheFromLiveState()
      ├─ checks territory is Sinus Ardorum / 1237
      ├─ checks enough time passed
      └─ CosmicClassProgressProvider.RefreshCacheFromLiveScores()
         └─ CosmicClassProgressProvider.TryReadLiveScores()
            ├─ WKSManager.Instance()
            ├─ manager->IsLoaded
            └─ manager->State.Scores.ToArray()
```

## Python-style pseudocode

```python
SINUS_ARDORUM = 1237
COSMIC_CACHE_REFRESH_INTERVAL = timedelta(seconds=30)
next_cosmic_cache_refresh_at = datetime.min


def on_framework_update():
    refresh_cosmic_cache_from_live_state()


def refresh_cosmic_cache_from_live_state():
    global next_cosmic_cache_refresh_at

    if client_state.territory_type != SINUS_ARDORUM:
        next_cosmic_cache_refresh_at = datetime.min
        return

    now = utc_now()
    if now < next_cosmic_cache_refresh_at:
        return

    next_cosmic_cache_refresh_at = now + COSMIC_CACHE_REFRESH_INTERVAL
    cosmic_class_progress_provider.refresh_cache_from_live_scores()


class CosmicClassProgressProvider:
    def refresh_cache_from_live_scores(self):
        self.try_read_live_scores()

    def try_read_live_scores(self, save_when_available=True):
        manager = WKSManager.instance()
        if manager is None or not manager.is_loaded:
            return None

        live_scores = list(manager.state.scores)
        if len(live_scores) < 11:
            return None

        live_scores = [max(0, score) for score in live_scores[:11]]

        if save_when_available and live_scores != self.cache.scores:
            self.cache.scores = live_scores
            self.cache.updated_at_utc = utc_now()
            self.save_cache()

        return live_scores
```

## What it reads

It reads local game/client state through ClientStructs:

```text
WKSManager.Instance()
└─ State.Scores
```

Those scores are interpreted as 11 class scores:

```text
0  Carpenter
1  Blacksmith
2  Armorer
3  Goldsmith
4  Leatherworker
5  Weaver
6  Alchemist
7  Culinarian
8  Miner
9  Botanist
10 Fisher
```

## What it saves

It saves this in plugin config:

```text
Configuration
└─ CosmicClassScoreCache
   ├─ Scores: List<int>  // 11 score values
   └─ UpdatedAtUtc: DateTimeOffset?
```

## What it does not do

It does not call:

```text
direct achievement-progress request API
RequestProgress
HttpClient
WebSocket
packet capture
addon callbacks
```

It is a local read of already-loaded WKS/Cosmic state.

## How Cosmic achievement progress is computed

When the UI needs progress for a Cosmic Class achievement:

```text
AchievementProgressService.GetProgress(row)
└─ if CosmicClassProgressProvider.Handles(row.RowId)
   └─ CosmicClassProgressProvider.GetProgress(row.RowId)
      ├─ find rule for achievement ID
      ├─ read live scores, or fall back to cached scores
      ├─ choose max score for "any class" rules
      ├─ choose min score for "all/every class" rules
      └─ return "current / target"
```

Examples:

```text
3702 → Carpenter 50,000
3703 → Carpenter 150,000
3704 → Carpenter 500,000
3726 → Miner 50,000
3735 → any Disciple of Hand 50,000
3739 → every class 500,000
```
