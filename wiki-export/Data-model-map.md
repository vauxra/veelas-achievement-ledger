# Data model map

## `Configuration.cs`

Saved plugin settings.

```text
Configuration
├─ Version
├─ HideCompletedInSearch
├─ TrackedAchievementIds
├─ Presets
└─ CosmicClassScoreCache
```

Python analogy:

```python
@dataclass
class Configuration:
    version: int
    hide_completed_in_search: bool
    tracked_achievement_ids: list[int]
    presets: list[TrackedAchievementPreset]
    cosmic_class_score_cache: CosmicClassScoreCache
```

## `TrackedAchievementStore.cs`

In-memory ordered tracked list.

Important methods:

```text
LoadFrom(ids)
ToConfigList()
Add(id)
Remove(id)
MoveToTop(id)
MoveUp(id)
MoveDown(id)
MoveToBottom(id)
```

This is the thing the UI edits; then `Plugin.SaveTrackedAchievements()` copies it back into `Configuration` and saves.

## `TrackedAchievementPresetStore.cs`

Manages named saved lists.

Important methods:

```text
SavePreset(name, ids)
RenamePreset(oldName, newName)
DeletePreset(name)
TryGetPreset(name, out preset)
```

## `AchievementProgress.cs`

Represents displayable progress.

Kinds:

```text
Complete
Incomplete
Numeric(current, target)
TargetKnown(target)
Unavailable
DataNotAvailable
CompletionListNotLoaded
```

The UI mostly calls:

```text
AchievementProgress.ToDisplayText()
```

## `AchievementInfo.cs`

Display info from game data:

```text
AchievementInfo
├─ Id
├─ Name
├─ CategoryName
└─ maybe other display/category fields
```

## `CosmicClassScoreCache.cs`

Saved Cosmic class score cache:

```text
CosmicClassScoreCache
├─ Scores: List<int>
└─ UpdatedAtUtc: DateTimeOffset?
```

It stores 11 class scores in this order:

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
