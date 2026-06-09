# Dalamud Research Notes — Achievement Tracker Mod

Date: 2026-06-08
Project path: `/mnt/mintData/git/achievement-tracker-mod`

## Goal

Build a Final Fantasy XIV plugin that helps the local player track achievements using the Dalamud plugin framework used by XIVLauncher.

## Primary sources read

- Dalamud docs: <https://dalamud.dev>
- Plugin development start: <https://dalamud.dev/plugin-development/getting-started>
- Project layout: <https://dalamud.dev/plugin-development/project-layout>
- Plugin metadata: <https://dalamud.dev/plugin-development/plugin-metadata>
- Technical considerations: <https://dalamud.dev/plugin-development/technical-considerations>
- Interacting with the game: <https://dalamud.dev/plugin-development/interaction/>
- Plugin restrictions: <https://dalamud.dev/plugin-publishing/restrictions>
- Submission process: <https://dalamud.dev/plugin-publishing/submission>
- Approval process: <https://dalamud.dev/plugin-publishing/approval-process>
- AI usage policy: <https://dalamud.dev/plugin-publishing/ai-policy>
- API index: <https://dalamud.dev/api/>
- Useful API docs:
  - `IDataManager`: <https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IDataManager>
  - `IUnlockState`: <https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IUnlockState>
  - `IPlayerState`: <https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IPlayerState>
  - `IClientState`: <https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IClientState>
  - `ICommandManager`: <https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/ICommandManager>
  - `WindowSystem`: <https://dalamud.dev/api/Dalamud.Interface.Windowing/Classes/WindowSystem>
  - `Window`: <https://dalamud.dev/api/Dalamud.Interface.Windowing/Classes/Window>

## Where to start

1. Use the official SamplePlugin template, not a blank project:
   - <https://github.com/goatcorp/SamplePlugin>
2. Customize it into `AchievementTracker` (or another final internal name) before publishing. The docs warn that the plugin `AssemblyName` becomes the immutable `InternalName`.
3. Use `Dalamud.NET.Sdk` for packaging:
   - <https://github.com/goatcorp/Dalamud.NET.Sdk>
4. Keep the project structure close to the docs:

```text
AchievementTracker.sln
AchievementTracker/
  AchievementTracker.csproj
  AchievementTracker.json or AchievementTracker.yaml
  packages.lock.json
  Plugin.cs
```

5. Start as a local/dev plugin first; only think about official repository submission after the design is safely inside the restrictions.

## Likely plugin shape

A safe first version should be an in-game UI helper:

- slash command: `/achtrack` opens the tracker window
- window: Dalamud `WindowSystem` + ImGui table/list
- data source: Lumina achievement sheets through `IDataManager.GetExcelSheet<Achievement>()`
- completion state: `IUnlockState.IsAchievementComplete(Achievement row)` when `IUnlockState.IsAchievementListLoaded` is true
- local character context: `IPlayerState` if needed, but avoid storing `ContentId` unless absolutely necessary
- config: saved user preferences only, via normal Dalamud plugin config / `IPluginConfiguration`

## APIs that look relevant

### `IDataManager`

`IDataManager` provides game data/Lumina access. It exposes:

- `GameData`
- `Excel`
- `GetExcelSheet<T>(ClientLanguage? language = null, string? name = null)`
- `GetSubrowExcelSheet<T>(...)`
- `GetFile(...)`

For this project, use it to load Lumina achievement rows, probably `Lumina.Excel.Sheets.Achievement`.

### `IUnlockState`

This is the big one for achievement completion. Docs say:

- `IsAchievementListLoaded`: whether the full achievement list was received
- `IsAchievementComplete(Achievement row)`: returns whether a given achievement is completed
- Important caveat: `IsAchievementComplete` requires that the player requested/loaded the achievements list; check `IsAchievementListLoaded` first.

Research implication: the UI should clearly show “achievement list not loaded yet” instead of polling or forcing sketchy behavior. A safe UX may ask the user to open their Achievements window in-game first if the list is not loaded.

### `IClientState`

Useful for login/zone state and safety gating:

- `IsLoggedIn`
- `IsPvP`
- `IsClientIdle()`
- events like `Login`, `Logout`, `TerritoryChanged`, `LevelChanged`

For this project, avoid doing heavy scans during combat/PvP or while the game is busy.

### `ICommandManager`

Register chat command handlers:

- `AddHandler(string command, CommandInfo info)`
- `RemoveHandler(string command)`

Use for `/achtrack` or similar.

### `WindowSystem` / `Window`

The docs explicitly recommend Dalamud Windowing API for normal settings/utility windows. Use it for the tracker UI.

### `IPlayerState`

Provides local character state such as name, world, job, level, and `ContentId`. Avoid collecting or transmitting personal identifiers. If per-character local config is needed, prefer a local, resettable, non-exported mapping.

## Documentation refresh — 2026-06-08

Re-read the official Dalamud development, publishing, restrictions, approval, AI policy, and relevant API docs.

Key refreshed docs takeaways:

- Start from the official `goatcorp/SamplePlugin` template when we are ready to scaffold, but do **not** scaffold until the design is settled.
- The plugin `AssemblyName` becomes the immutable `InternalName`; choose it carefully before publishing because it affects the config directory, log entries, DLL name, and D17 submissions.
- Use `Dalamud.NET.Sdk` / `DalamudPackager` for packaging and metadata generation.
- A plugin DLL must ship with a manifest named after the internal name; minimum metadata includes `Name`, `Author`, `Punchline`, `Description`, and usually `RepoUrl`.
- For regular settings/utility UI, use the Dalamud Windowing API.
- For game data, prefer Lumina over XIVAPI because Lumina uses local game files, stays current with the local install, and avoids external API requests. This matches the maintainer's local game path: `/mintData/games/.xlcore/ffxiv`.
- Prefer Dalamud-provided APIs first, Client Structs only if a needed behavior is not exposed, and raw memory/hooks only as a last resort.
- `IDataManager.GetExcelSheet<T>()` is the appropriate entry point for Lumina game sheets such as `Achievement`.
- `IUnlockState.IsAchievementComplete(Achievement row)` requires `IUnlockState.IsAchievementListLoaded`; the UI should handle the unloaded state instead of forcing or automating anything.
- Official repository submissions are open source, built from a public commit hash, reviewed as diffs, and new plugins go through the D17 `testing/live/` track first.
- The official build system has no direct internet access; do not rely on build-time downloads.

## Rules checklist for planning

Use this checklist when we manually plan v1:

### Green / preferred

- Achievement tracker helper.
- Read achievement data from local game files through Lumina / `IDataManager`.
- Read local player achievement completion through `IUnlockState` only when `IsAchievementListLoaded` is true.
- Use a normal Dalamud window for display/settings.
- Save only local preferences such as filters, sort order, collapsed categories, and display toggles.
- Provide clear user instructions when the achievement list is not loaded, e.g. ask the user to open the in-game Achievements window.
- Keep the plugin explainable, personally tested, and reviewable by the maintainer.

### Red / avoid

- No automation of gameplay, dialog, crafting, loot rolls, cutscenes, emotes, server requests, or achievement-related actions.
- No polling or automatic game-server interaction without direct user action.
- No out-of-spec server requests or behavior impossible through normal gameplay.
- No combat augmentation, PvP advantage, DPS parsing, raid logging, FFLogs integration, or ACT-as-plugin direction.
- No collecting account IDs of other player characters.
- No exposing whether specific users use the plugin.
- No backend, leaderboard, public directory, telemetry, or cloud sync in v1.
- No bypassing Square Enix monetization or supporting out-of-spec gameplay scenarios.
- No hard dependency on plugins that violate Dalamud guidelines.

### Yellow / needs explicit design review later

- Any backend or sync feature.
- Any telemetry or analytics.
- Any storage or transmission of local player identifiers such as name or Content ID.
- Any feature that touches combat/PvP context, even display-only.
- Any AI-generated icon, image, audio, or user-facing asset.

If a backend is ever proposed, the docs require minimum necessary data, explicit opt-in for non-essential telemetry, resettable pseudo-random analytics IDs, no personal-info-derived identifiers, no user-enumeration risk, HTTPS/TLS with trusted certificates, and DNS hostnames rather than raw IPs.

## AI policy checklist

For official Dalamud plugin repository submissions:

- AI-assisted code is held to the same standard as hand-written code.
- the maintainer must understand, test, and be able to explain the plugin.
- If AI was used beyond basic autocomplete/inline suggestions, disclose the level in the PR description.
- Disclosure levels are `None`, `Hint`, `Assist`, `Pair`, `Copilot`, and `Auto`.
- With Hermes involved in design/planning and any future code, likely disclosure will be at least `Assist`; if AI writes significant code from the maintainer's plan, it may be `Copilot`.
- Avoid `Auto`; entirely AI-generated submissions with no meaningful human involvement are auto-rejected, and repeated attempts can result in a ban.
- Undisclosed AI use in a demonstrably AI-written submission can result in a ban.
- AI output must be verified because it often gets Dalamud and adjacent APIs wrong.
- AI-generated user-facing assets must be disclosed in the plugin description; handmade icons are preferred, even crude ones.
- AI-assisted translations are acceptable as placeholders but should get native-speaker/community review before final localization.

## What we can do

Based on refreshed docs/restrictions/policy, acceptable directions appear to include:

- Build an open-source Dalamud plugin for the local player.
- Use AI as a development assistant if the maintainer understands, tests, reviews, and can explain the code.
- Disclose AI use in the official repository PR if AI was used beyond basic autocomplete.
- Read local game data through Dalamud/Lumina.
- Use Dalamud APIs first; use Client Structs only if needed; raw memory/hooks only as a last resort.
- Display achievement information differently for the player, as long as it does not automate gameplay or provide competitive advantage.
- Maintain local plugin configuration/preferences.
- Use maintainer-run backend services only if there is a real need and if the plugin follows strict privacy/telemetry requirements.

## What we cannot / should not do

### AI policy constraints

For official plugin repository submissions:

- Entirely AI-generated submissions are auto-rejected.
- Undisclosed AI use in a demonstrably AI-written submission can result in a ban.
- AI use beyond basic autocomplete must be disclosed in the PR description.
- The developer must personally test the plugin.
- The developer must understand and be able to explain implementation choices.
- AI output must be verified because it often gets Dalamud and adjacent APIs wrong.
- AI-generated assets must be disclosed in the plugin description; the community prefers handmade icons, even crude ones.
- AI-assisted translations are acceptable as placeholders, but should get native-speaker/community review for final localization.

Recommended disclosure level for this project if Hermes keeps helping with design/code: likely **Assist**, **Pair**, or **Copilot** depending how much code AI writes. Avoid **Auto**.

### Dalamud/plugin restrictions

Avoid anything that:

- automatically interacts with game servers without direct user interaction
- sends out-of-spec requests or allows server actions impossible through normal gameplay
- augments/alters/interferes with combat beyond allowed display-only party/alliance info
- provides parsing, raid logging, DPS meters, FFLogs integration, or similar
- collects account IDs of other player characters in any form
- gives an advantage in PvP or competitive environments
- automates crafting, loot rolls, cutscenes, dialog, emotes, or other player actions
- bypasses Square Enix monetization or Mog Station purchases
- is only useful for out-of-spec scenarios
- hard-depends on plugins that violate the guidelines

For an achievement tracker, the main danger zones are:

- Do **not** automate achievement-related gameplay.
- Do **not** poll game/server state aggressively.
- Do **not** collect/send personal character identifiers unless absolutely necessary.
- Do **not** expose whether specific other players use the plugin.
- Do **not** build a backend leaderboard or public directory without careful opt-in/privacy review.

## Backend / telemetry rules if we ever add cloud sync

Technical considerations allow plugins to talk to maintainer-run servers, but requirements are strict:

- send the minimum necessary data
- hash local-player info in the plugin when feasible
- non-essential telemetry requires explicit opt-in
- telemetry must serve public interest/plugin improvement
- analytics identifiers must be pseudo-random, not derived from personal info, and resettable by the user
- collected data must be topical to the plugin
- do not expose a list of plugin users or make it easy to test whether a specific user uses the plugin
- server communication must be encrypted via HTTPS/TLS with trusted CA certs
- connect via DNS hostname, not raw IP

Recommendation: **v1 should avoid external services**. Add cloud sync only after a separate design/privacy review.

## Official repository submission notes

- Official plugins must be open source.
- Submission goes through `goatcorp/DalamudPluginsD17`.
- New plugins go to `testing/live/` first.
- D17 submission uses a `manifest.toml` with repository URL, commit hash, owners, project path, and changelog.
- Approval reviews source code and resulting diff; new submissions need multiple yes votes and testing track first.
- Build system has no direct internet access; do not depend on downloading code at build time.

## Environment notes from this machine

- Project directory created: `/mnt/mintData/git/achievement-tracker-mod`
- Git repo initialized.
- Local FFXIV/XIVLauncher game files are at `/mintData/games/.xlcore/ffxiv` (also visible via `/mnt/mintData/games/.xlcore/ffxiv`).
- Local Dalamud-related directories found under `/mintData/games/.xlcore/`, including `dalamud`, `dalamudAssets`, and `installedPlugins`.
- the maintainer reported the intended config path as `.xlcore/ffxiv/config`; on this machine, `/mintData/games/.xlcore/ffxiv/config` was not present during the initial check, so verify the exact config/dev-plugin path before wiring build scripts.
- `dotnet --info` showed .NET runtime 10.0.8 installed, but **no .NET SDK installed**. Current SamplePlugin docs mention .NET SDK 8 as a prerequisite, while the checked SamplePlugin uses `Dalamud.NET.Sdk/15.0.0` and a `net10.0-windows7.0` lockfile. Re-check the official docs/template before installing or targeting an SDK.

## Reset / current working stance

Do not scaffold plugin code yet. Keep the project in research/design mode until we have:

1. re-read the official docs and AI policy,
2. confirmed the correct local Dalamud dev-plugin/build paths,
3. chosen a minimal v1 feature shape,
4. written a small implementation plan that the maintainer approves.

## First implementation plan draft

1. Install/verify the .NET SDK version expected by the current SamplePlugin/Dalamud API.
2. Clone or template from `goatcorp/SamplePlugin` into this project.
3. Rename project/assembly/manifest to `AchievementTracker` before doing any real work so `InternalName` is stable.
4. Add basic plugin shell:
   - `Plugin.cs`
   - `/achtrack` command
   - `WindowSystem`
   - main tracker window
5. Add data layer:
   - load `Achievement` sheet through `IDataManager`
   - group/filter achievements by category if the Lumina rows expose category fields
6. Add completion layer:
   - if `IUnlockState.IsAchievementListLoaded` false, show a friendly instruction to load achievements in-game
   - if true, call `IsAchievementComplete(row)` for display
7. Add user settings:
   - filters/sort/search
   - hidden/completed toggles
   - local config
8. Add safety gates:
   - no work in PvP
   - no server calls
   - no automation
9. Add tests or at minimum isolated pure-C# tests for filtering/grouping logic outside Dalamud runtime.
10. Before any official submission:
   - review restrictions again
   - prepare AI disclosure
   - personally test in-game
   - submit to D17 testing/live if still desired

## Open questions

- What exact feature should v1 have: simple checklist, category progress, recommended next achievements, daily/weekly reminders, or search/filter only?
- Does `IUnlockState.IsAchievementListLoaded` become true only after opening the achievement UI, and can we guide the user without triggering automation?
- Which Lumina fields on `Achievement`/related sheets best identify category, rewards, points, title rewards, and criteria?
- What .NET SDK target does current SamplePlugin require?
