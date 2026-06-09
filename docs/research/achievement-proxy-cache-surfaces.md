# Achievement Proxy/Cache Notes

Checked local Dalamud, ClientStructs, and Lumina docs for achievement cache/proxy data.

## Found

- `IUnlockState.IsAchievementListLoaded`
- `IUnlockState.IsAchievementComplete(...)`
- `Achievement.CompletedAchievements`
- `Achievement.CompletedAchievementsBitArray`
- `Achievement.History`
- one current progress slot:
  - `ProgressAchievementId`
  - `ProgressCurrent`
  - `ProgressMax`
- native Achievement UI methods:
  - `AgentAchievement.OpenById(...)`
  - generic event/callback methods

## Not found

- No public `InfoProxyAchievement` wrapper.
- No public achievement-progress observable.
- No clean category/subcategory selection API.

## Takeaway

Flow:

1. User clicks **↻** or **Update Next**.
2. The game opens the achievement entry.
3. The plugin records the progress value the game receives.

Avoid synthetic menu clicks or packet automation unless maintainers explicitly approve that direction.
