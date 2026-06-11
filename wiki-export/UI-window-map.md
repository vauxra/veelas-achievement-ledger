# UI/window map

Version: `v0.2.0.33`

The UI code is in `AchievementTracker/Windows/`. It uses ImGui, which is immediate-mode UI: every `Draw...` method is called repeatedly, but button bodies only run when clicked.

## Main window: `TrackerWindow.cs`

Purpose: the normal `/val` window where you view tracked achievements.

```text
TrackerWindow.Draw()
├─ AchievementProgressSource.UpdateCache()
├─ DrawTopButtons()
│  ├─ DrawConfigureButton()
│  ├─ DrawUpdateNextButton()
│  ├─ DrawCloseAchievementsButton()
│  └─ DrawUpdateOpenLockoutStatus()
└─ DrawTrackedAchievementList()
   └─ DrawAchievement(id)
      ├─ DrawRowUpdateButton(id)
      ├─ DrawRowInspectButton(id)
      └─ draw name/progress/last-observed text
```

### Top buttons

- **Configure**
  - calls `Plugin.ToggleConfigUi()`
  - opens/closes the config window
  - risk: low

- **Update Next**
  - calls `OpenNextTrackedAchievementForUpdate()`
  - chooses the first unobserved achievement, otherwise the oldest observed achievement
  - calls `Plugin.OpenAchievementForUpdate(id)`
  - uses adaptive update-open pacing: closed native window waits for data up to 15s with a 6s minimum; already-open native window uses only a 1s request cooldown
  - risk: low-to-medium because it opens native Achievement UI, but only on click

- **Close Achievements**
  - calls `NativeAchievementNavigator.CloseAchievements()`
  - internally calls `AgentAchievement.Instance()->Hide()`
  - risk: low-to-medium native UI action, user-click only

### Row buttons

- Reload icon
  - calls `Plugin.OpenAchievementForUpdate(id)`
  - shared adaptive pacing applies
  - intended as update-action open

- Magnifying glass
  - calls `NativeAchievementNavigator.OpenAchievement(id)`
  - lockout does not apply
  - intended as inspect/open action

## Config window: `ConfigWindow.cs`

Purpose: manage tracked achievements, presets, search, and help.

```text
ConfigWindow.Draw()
├─ DrawHeader()
├─ DrawLeftNavigation()
└─ DrawSelectedPage()
   ├─ DrawTrackedAchievementsPage()
   └─ DrawHelp()
```

### Header

- **Open VAL**
  - calls `Plugin.OpenMainUi()`
  - risk: low

### Navigation

- **Tracked Achievements**
  - presets, tracked list, search/add/remove
- **Help**
  - player-facing explanations

### Presets

```text
DrawPresetControls()
├─ DrawPresetNameInput()
├─ DrawPresetSaveButton()
├─ DrawPresetPicker()
├─ DrawPresetReadButton()
├─ DrawPresetRenameButton()
└─ DrawPresetDeleteButton()
```

Preset actions change only plugin config:

- save current tracked list
- select/load preset immediately, filtering preset IDs through `AchievementCatalog.IsManuallyViewable(id)` before they enter the tracked list
- read selected preset again
- rename selected preset
- delete selected preset

Risk: low.

### Tracked management column

```text
DrawTrackedManagement()
└─ DrawTrackedAchievementRow(id)
   ├─ Top / Up / Down / Bottom
   ├─ remove
   ├─ reload/update-open
   ├─ magnifying glass inspect-open
   └─ achievement name/category/Cosmic progress
```

Risk notes:

- Top/Up/Down/Bottom/remove only change local plugin config.
- Reload/update-open calls the shared adaptive pacing path and opens native Achievement UI.
- Magnifying glass opens native Achievement UI as inspect action.

### Search column

```text
DrawSearchAndAdd()
├─ DrawHideCompletedCheckbox()
├─ DrawSearchInput()
├─ GetVisibleSearchResults()
│  └─ AchievementCatalog.Search(query) filters hidden/non-manually-viewable rows
└─ DrawSearchResultRow(result)
   ├─ DrawSearchResultAction(...)
   │  ├─ Add, guarded by AchievementCatalog.IsManuallyViewable(id)
   │  ├─ Remove
   │  └─ Full label
   └─ DrawSearchResultDetails(result)
```

Risk: low. Search reads Lumina data and plugin config. It excludes hidden categories and achievement/name hide conditions so users can only add achievements that should be manually viewable in the native Achievement menu. The magnifying-glass button opens native Achievement UI by direct user click.

## Why UI nesting still exists

ImGui naturally creates nested UI blocks:

- windows contain children
- rows contain groups
- buttons contain click handlers
- combo boxes contain selectable rows

The refactor moved most row/action details into helpers so the top-level UI methods stay readable even when ImGui requires braces.
