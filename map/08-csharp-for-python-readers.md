# C# primer for Python readers

Version: `v0.2.0.32`

This document explains the C# conventions used in Veela's Achievement Ledger by comparing them to Python ideas.

## Files, namespaces, and classes

C# code is usually organized around classes. In this project, most files contain one main class.

```csharp
namespace AchievementTracker.Services;

public sealed class NativeAchievementNavigator
{
}
```

Python-ish mental model:

```python
# module: AchievementTracker/Services/NativeAchievementNavigator.py
class NativeAchievementNavigator:
    pass
```

- `namespace` is like the package/module path.
- `class` is a class.
- `sealed` means other classes cannot inherit from it.
- `public` means other code can call it.
- `private` means only this class can call it.

## `this.` means `self.`

C#:

```csharp
this.plugin.SaveConfiguration();
```

Python equivalent:

```python
self.plugin.save_configuration()
```

In this codebase, `this.` is used consistently so it is obvious when a field or method belongs to the current object.

## Constructors are named after the class

C#:

```csharp
public TrackerWindow(Plugin plugin)
{
    this.plugin = plugin;
}
```

Python equivalent:

```python
class TrackerWindow:
    def __init__(self, plugin):
        self.plugin = plugin
```

## Return types come before method names

C#:

```csharp
public bool OpenAchievement(uint achievementId)
```

Python-ish equivalent:

```python
def open_achievement(achievement_id: int) -> bool:
```

Common return types in this project:

- `void` = returns nothing / `None`
- `bool` = `True` or `False`
- `string` = Python `str`
- `int` = normal signed integer
- `uint` = non-negative integer
- `TimeSpan` = duration
- `DateTimeOffset` = timestamp with offset

## Nullable values: `?`

C#:

```csharp
private ClientAchievementProgressSource? passiveAchievementProgressObserver;
```

Python-ish:

```python
passive_achievement_progress_observer: ClientAchievementProgressSource | None
```

A `?` after a type means the value may be `null`.

## Guard clauses keep code flat

This project tries to prefer early returns instead of deeply nested `if` blocks.

C#:

```csharp
if (agent == null)
{
    return false;
}

agent->OpenById(achievementId);
return true;
```

Python:

```python
if agent is None:
    return False

agent.open_by_id(achievement_id)
return True
```

That style is used to keep most methods under about three nested brace levels.

## Properties vs fields

C# property:

```csharp
public Configuration Configuration { get; }
```

Python-ish mental model:

```python
@property
def configuration(self):
    return self._configuration
```

A property with only `get;` is read-only from outside the class after construction.

## Static members

C#:

```csharp
private static readonly TimeSpan AchievementUpdateMinimumLockout = TimeSpan.FromSeconds(6);
private static readonly TimeSpan AchievementUpdateMinimumJitter = TimeSpan.Zero;
private static readonly TimeSpan AchievementUpdateMaximumLockout = TimeSpan.FromSeconds(15);
```

Python-ish:

```python
ACHIEVEMENT_UPDATE_OPEN_LOCKOUT = timedelta(seconds=5)
```

`static` means the value belongs to the class/type, not one object instance.

## `readonly`

C#:

```csharp
private readonly Plugin plugin;
```

Means the field is assigned in the constructor and not reassigned afterward.

Python has no exact equivalent, but you can read it as “do not mutate this reference after init.”

## `using` imports

C#:

```csharp
using Dalamud.Bindings.ImGui;
using System;
```

Python:

```python
import system
from dalamud.bindings import imgui
```

## Lists and LINQ

C#:

```csharp
var trackedIds = this.plugin.TrackedAchievements.AchievementIds.ToList();
```

Python-ish:

```python
tracked_ids = list(self.plugin.tracked_achievements.achievement_ids)
```

C# LINQ chains are like Python comprehensions / sorting pipelines.

C#:

```csharp
return trackedIds
    .Select(id => new { Id = id, ObservedAt = ... })
    .OrderBy(item => item.ObservedAt)
    .First().Id;
```

Python-ish:

```python
return sorted(items, key=lambda item: item.observed_at)[0].id
```

## Lambdas

C#:

```csharp
() => true
id => id.RowId
```

Python:

```python
lambda: True
lambda id: id.row_id
```

## Switch expressions / switch statements

C# switch statement:

```csharp
switch (normalized)
{
    case "help":
        this.OpenConfigUi(help: true);
        break;
    default:
        this.ToggleMainUi();
        break;
}
```

Python equivalent:

```python
if normalized == "help":
    self.open_config_ui(help=True)
else:
    self.toggle_main_ui()
```

C# switch expression used for Cosmic achievement rules:

```csharp
return achievementId switch
{
    3702 => Single(Carpenter, 50_000),
    _ => null,
};
```

Python-ish:

```python
return {
    3702: single(CARPENTER, 50_000),
}.get(achievement_id)
```

## `out` parameters

C# often returns extra values through `out` parameters.

```csharp
if (this.plugin.AchievementCatalog.TryGetRow(achievementId, out var row))
{
    // use row
}
```

Python-ish:

```python
ok, row = achievement_catalog.try_get_row(achievement_id)
if ok:
    # use row
```

Project convention: methods named `Try...` usually return `bool` and give you the result through `out var`.

## `unsafe` and pointers

C#:

```csharp
public unsafe sealed class NativeAchievementNavigator
...
agent->OpenById(achievementId);
```

Python has no normal equivalent. Think of this as “direct native/client memory or function access.”

In this project, `unsafe` is intentionally isolated in service classes:

- `NativeAchievementNavigator`
- `ClientAchievementProgressSource`
- `ClientAchievementProgressSource`
- parts of `CosmicClassProgressProvider`

Risk rule:

- `unsafe` is not automatically bad.
- It is higher-risk and should stay small, commented, and easy to review.
- It must not hide direct server requests, synthetic callbacks, packet work, or automation.

## Hooks

`ClientAchievementProgressSource` uses hooks.

Python-ish mental model:

```python
def wrapped_callback(*args):
    original_callback(*args)       # keep game behavior first
    cache_observed_values(*args)   # then store what happened
```

Important project convention:

- Hook methods call the original function first.
- Then they cache observed data.
- They do not request new data.

## ImGui UI pattern

UI files are immediate-mode. That means the UI is redrawn every frame.

C#:

```csharp
if (ImGui.Button("Configure"))
{
    this.plugin.ToggleConfigUi();
}
```

Python-ish:

```python
if imgui.button("Configure"):
    self.plugin.toggle_config_ui()
```

The `if` block only runs on the frame where the player clicks the button.

## Common project method prefixes

- `Draw...` = render UI and handle button clicks for that UI block.
- `Open...` = open a window or native UI.
- `Try...` = return true/false and output a value if successful.
- `Save...` = write plugin config.
- `Refresh...` = update local cached state.
- `Record...` = store observed progress in memory/cache.

## Risk comment convention added in the refactor

Look for comments like:

```csharp
// Component: native Achievement window navigation.
// Risk level: medium.
// Why: uses ClientStructs AgentAchievement to open/close the game UI.
// Safety boundary: methods are called from user button clicks only.
```

These comments tell you:

1. What part of the plugin you are reading.
2. What external component it interacts with.
3. Why it is risky or not risky.
4. What safety promise the method/class is supposed to keep.
