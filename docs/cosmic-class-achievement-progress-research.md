# Cosmic Class achievement progress research

Research date: 2026-06-10
Branch context: `achex-experimental`

## Goal

Some achievements, such as `Hammering with the Stars`, are not well-served by the normal binary achievement-complete check. They are tied to Cosmic Exploration class score state, and we want to display local progress like `455000 / 500000` when possible.

This should be an isolated, read-only, one-off integration. It must not change the core achievement request/update behavior.

## Example achievement

Lodestone URL:

<https://na.finalfantasyxiv.com/lodestone/playguide/db/achievement/f4a96cfdd53/?patch=latest>

Local Lumina row observed:

- Row ID: `3704`
- Name: `Hammering with the Stars`
- Description: `Earn a cosmic class score of 500,000 points as a carpenter.`
- Category: `Crafting & Gathering > Carpenter`
- Points: `10`
- Type: `1`
- AchievementTarget: row `9`, value `9`, type `2`

## Related achievement rows

Cosmic class score achievements found in local Lumina include rows `3702` through `3739`.

Per-class rows:

- Carpenter: `3702`, `3703`, `3704`
- Blacksmith: `3705`, `3706`, `3707`
- Armorer: `3708`, `3709`, `3710`
- Goldsmith: `3711`, `3712`, `3713`
- Leatherworker: `3714`, `3715`, `3716`
- Weaver: `3717`, `3718`, `3719`
- Alchemist: `3720`, `3721`, `3722`
- Culinarian: `3723`, `3724`, `3725`
- Miner: `3726`, `3727`, `3728`
- Botanist: `3729`, `3730`, `3731`
- Fisher: `3732`, `3733`, `3734`

Aggregate rows:

- Any Disciple of the Hand at 50,000: `3735`
- Any Disciple of the Land at 50,000: `3736`
- Every Disciple of the Hand at 500,000: `3737`
- Every Disciple of the Land at 500,000: `3738`
- Every Disciple of the Hand and Land at 500,000: `3739`

## Target values

The target score is not directly exposed as an ordinary numeric progress counter in the achievement row. It can be derived safely from the achievement description and/or known row mapping:

- Tier I rows: `50,000`
- Tier II rows: `150,000`
- Tier III / named capstone rows: `500,000`
- Aggregate capstone rows: `500,000` per relevant job

## Game system naming

Cosmic Exploration data appears under internal `WKS` naming in Lumina/ClientStructs, not under a direct `Cosmic` namespace.

Important ClientStructs type:

```csharp
FFXIVClientStructs.FFXIV.Client.Game.WKS.WKSManager
```

Relevant observed members:

```csharp
WKSManager.Instance()
WKSManager.IsLoaded
WKSManager.TerritoryId
WKSManager.DevGrade
WKSManager.CurrentScore
WKSManager.CurrentRank
WKSManager.Scores
WKSManager.State
WKSManager.State.Scores
WKSManager.IsFunctionUnlocked(byte functionRowId)
```

`Scores` is a span/fixed array of 11 `int` values. This matches the 11 Cosmic class jobs:

1. Carpenter
2. Blacksmith
3. Armorer
4. Goldsmith
5. Leatherworker
6. Weaver
7. Alchemist
8. Culinarian
9. Miner
10. Botanist
11. Fisher

This class-to-index mapping still needs in-game validation against the Cosmic Class Tracker UI.

## Quest/function gating clues

Lumina has a `WKSFunction` sheet with:

- `RequiredQuests0`
- `RequiredQuests1`
- `RequiredQuests2`
- `RequiredDevGrade`

Quest row names resolved locally include:

- `70789`: `A Cosmic Homecoming`
- `70790`: `Passion, Thy Name Is Ardorum`
- `70945`: `Go Forth, Brave Explorers`
- `70946`: `The Brightest Star`
- `70984`: `Mission of Gravity`

For display, the first implementation can avoid hard quest-gate logic and instead treat unreadable/unloaded WKS data as unavailable, using the persisted local cache if available.

## Proposed safe implementation

Add a small read-only adapter, isolated from the normal achievement-progress request pipeline:

```csharp
interface ILocalAchievementProgressProvider
{
    bool TryGetProgress(uint achievementId, out LocalAchievementProgress progress);
}
```

For Cosmic class achievements:

1. Map achievement ID to one or more WKS score indexes and a target score.
2. Read `WKSManager.Instance()`.
3. If WKS data is loaded and `Scores` has useful values, return current/target.
4. Persist the last observed full 11-score array to plugin config.
5. Refresh that cache passively while WKS data is loaded, even if the user is not currently viewing a specific Cosmic row.
6. If live WKS data is unavailable later, use the persisted cache.
7. If neither live data nor cache exists, return a graceful unavailable state such as `Cosmic score data not available`.

## Display behavior

Desired display for applicable Cosmic class achievements:

- Live or cached score available: `current / target`
- Score unavailable: `Data not available`
- Normal achievements: unchanged existing display logic

For aggregate achievements:

- `any DoH/DoL`: use the max score among relevant jobs against the target.
- `every DoH/DoL`: use the minimum score among relevant jobs against the target, because every class must reach the goal.
- Consider a tooltip/debug line showing the per-job values during test builds.

## Debugging to include

To reduce back-and-forth with someone who has the content unlocked, include a compact diagnostics section or debug text that can show:

- WKS manager pointer/state availability
- `IsLoaded`
- territory ID
- current score
- all 11 score values
- whether live data or persisted cache was used
- mapped achievement ID, target, and score indexes used

## Safety notes

This integration should be read-only:

- no server requests
- no packet capture
- no addon callback invocation
- no automatic game actions
- no writes to game memory

Persist only numeric score cache values in plugin config. Do not use player-entered strings, paths, or external files for this feature.
