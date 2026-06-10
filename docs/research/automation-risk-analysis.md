# Automation risk analysis: GatherBuddyReborn and Artisan

Date: 2026-06-10
Branch: `research/automation-risk-analysis`

This document compares the automation approaches used by two established Dalamud plugins and frames what they imply for a possible automation branch of Veela's Achievement Ledger.

Sources reviewed:

- `FFXIV-CombatReborn/GatherBuddyReborn` at `ca27f0aec69f65c2c09f9365247d0b33b79ebca3`
- `PunishXIV/Artisan` at `d4428a0227a82f2555dbfff1ac7145904f5241cf`
- `goatcorp/dalamud-docs` at `9a29448fa4a742d7315972df903fdac5745c028d`
- `goatcorp/DalamudPluginsD17` at `0f26ad4c8c5966b90d440023b16bab2ef26a9115`

Local source checkouts used for this review:

- `/mnt/mintData/git/research-sources/GatherBuddyReborn`
- `/mnt/mintData/git/research-sources/Artisan`
- `/mnt/mintData/git/research-sources/dalamud-docs`
- `/mnt/mintData/git/research-sources/DalamudPluginsD17`

## Executive summary

Both reviewed plugins implement real automation, not just UI convenience:

- **GatherBuddyReborn** automates gathering, fishing, pathing, teleports, NPC/dialog interactions, vendor purchases, collectable turn-ins, and also includes crafting automation through its Vulcan components.
- **Artisan** is centered on automated crafting: solver-selected craft actions, endurance loops, quick synthesis, consumable use, recipe selection, gearset changes, repairs, materia extraction, retainer restocking, and IPC-triggered automation.

Against current Dalamud publishing guidance, both sit well beyond the low-risk pattern we have been using for Veela's Achievement Ledger. They use documented Dalamud APIs, but they also routinely use `FFXIVClientStructs`, addon callbacks, framework-update task queues, raw signatures/hooks, and direct `ActionManager.UseAction` or object-interaction calls.

For an Achievement Ledger automation branch, the most important takeaway is that there are at least three distinct risk bands:

1. **Low-risk assisted tracking**: documented Dalamud APIs, Lumina/local data, user-clicked native Achievement UI opening, passive observation/cache updates. This is the current safe direction.
2. **Medium-risk user-initiated helpers**: a direct user click causes one bounded native UI action, with no repeated queue, no automatic server request loop, and no synthetic confirmations. This may be defensible but should be reviewed before implementation.
3. **High-risk automation**: framework-loop queues, repeated server-affecting actions, addon callback firing for confirmations/submissions, direct action use, pathing, retainer/vendor/repair automation, raw hooks/signatures, IPC-triggerable automation. This is the pattern used by the reviewed automation plugins and should be treated as experimental/private-risk territory for our app.

Recommended stance for the achievement tracker: keep the public/beta line at **native Achievement UI open + passive observation**, with **no plugin-originated progress request queues, no automatic refresh loops, no packet/network capture, and no synthetic menu/callback submission**. If we intentionally build a research automation branch, gate it behind explicit naming and docs that it is not the official-submission-safe path.

## Dalamud policy and API baseline

### Interaction API priority

Official Dalamud documentation for interacting with the game gives this priority order:

1. Use **Dalamud-provided APIs** where possible. These are the safest, stable across API bumps, documented, and may include protections.
2. If Dalamud APIs do not expose the needed behavior, use **ClientStructs**. This lets plugins use the game as a library, but often requires pointers and unsafe code; plugin authors are responsible for safety.
3. If ClientStructs do not expose the behavior, use raw memory, raw functions, signatures, and hooks.

Source: `dalamud-docs/docs/plugin-development/interaction/index.md`.

### Plugin restrictions relevant to automation

The plugin restrictions say plugins should not interact with game servers in a way that is:

- automatic, such as polling data or making requests without direct user interaction, or
- outside specification, such as allowing the player to do or submit things to the server that would not be possible by normal means.

The restrictions also name common non-starters, including:

- automated crafting,
- skip dialog boxes,
- autoroll on loot,
- emote/expression looping,
- anything in PvP,
- and other features that create automation or unfair advantage concerns.

Source: `dalamud-docs/docs/plugin-publishing/restrictions.md`.

### Backend/network policy

Dalamud docs permit maintainer-run backend services, but require care around data minimization, privacy, opt-in telemetry, encrypted communication, and DNS hostnames. They recommend Lumina/local game files over XIVAPI for static game data.

Source: `dalamud-docs/docs/plugin-development/technical-considerations.md`.

### Approval criteria

D17 approval checks include whether a plugin meets the published guidelines, passes informal code review, installs cleanly, behaves correctly, has no obvious technical issues, and meets technical criteria. New plugins are expected to start in testing.

Source: `DalamudPluginsD17/README.md`.

## Risk scale used in this report

- **1: Safe/read-only**: Dalamud services, Lumina, local state, no server-affecting actions.
- **2: Assisted/user-triggered**: one explicit user action causes a bounded native UI open or equivalent; no queue/polling/repeat loop.
- **3: ClientStructs/action-adjacent**: direct use of ClientStructs or game functions, but still bounded and user-triggered.
- **4: Synthetic interface automation**: addon callbacks, `ReceiveEvent`, object interaction, confirmations, purchases, turn-ins, or action execution from a task queue.
- **5: Full automation / official-guideline conflict**: repeated unattended automation, crafting/gathering/combat-like action loops, pathing, server-affecting queues, raw hooks/signatures, or IPC-triggerable automation.

## GatherBuddyReborn analysis

### Overall ranking

- **Automation risk**: 5 / 5
- **API-layer risk**: 5 / 5
- **Official-guideline fit**: poor for official-repo-safe automation patterns; the plugin intentionally implements unattended gathering/crafting-style automation.
- **Server interaction model**: repeated framework-update-driven task queues call game actions, interact with objects, fire addon callbacks, teleport, buy, gather, craft, and turn in collectables.
- **Backend/network model**: uses public/remote HTTP APIs for market/fish-record features in addition to in-game automation.

### Public description and intent

The repository README and manifest explicitly describe automation:

- `README.md:10-18` markets automated routes via `vnavmesh`, `AutoGather`, automated pathing, and full BTN/MIN automation.
- `manifest.json:6` describes automatic gathering from user-specified lists and says automated gathering requires `vnavmesh`.
- `manifest.json:17` includes an `Automatic` tag/category.

This is not a hidden implementation detail. The plugin is openly an automation plugin.

### Dalamud service usage

GatherBuddyReborn uses many documented Dalamud services through `GatherBuddy/Dalamud.cs:16-39`, including:

- `IDalamudPluginInterface`
- `ICommandManager`
- `IDataManager`
- `IClientState`
- `IObjectTable`
- `IPlayerState`
- `IChatGui`
- `IFramework`
- `ICondition`
- `IGameGui`
- `ITargetManager`
- `IAddonLifecycle`
- `IGameInventory`
- `IToastGui`

It also injects interop-capable services:

- `IGameInteropProvider` at `GatherBuddy/Dalamud.cs:28` and `GatherBuddy/Dalamud.cs:36`
- `ISigScanner` at `GatherBuddy/Dalamud.cs:38`

The main plugin update path subscribes to `Dalamud.Framework.Update` and calls automation components every tick:

- `GatherBuddy/GatherBuddy.cs:211` registers the update handler.
- `GatherBuddy/GatherBuddy.cs:317-323` updates crafting/vendor bridge components.
- `GatherBuddy/GatherBuddy.cs:330-333` calls `AutoGather.DoAutoGather()` every tick.

### Task queues and automation loop

GatherBuddyReborn has a custom task manager in `GatherBuddy/AutoGather/TaskManager.cs`:

- `TaskManager.cs:29-33` subscribes to framework updates.
- `TaskManager.cs:63-109` enqueues normal tasks.
- `TaskManager.cs:115-161` enqueues immediate tasks.
- `TaskManager.cs:217-264` provides delay helpers.
- `TaskManager.cs:268-330` starts and runs tasks until completion or timeout.

`AutoGather` constructs this task manager from `Dalamud.Framework` at `GatherBuddy/AutoGather/AutoGather.cs:48-52`.

Policy implication: this is an unattended framework-loop automation architecture. It does not require a fresh user action for every server-affecting operation.

### Server-affecting actions

Representative examples:

- Fishing and gathering actions use `ActionManager.Instance()->UseAction(...)`:
  - `GatherBuddy/AutoGather/AutoGather.Actions.cs:474-491`
  - `GatherBuddy/AutoGather/AutoGather.Actions.cs:650-667`
- Gathering item selection fires UI callbacks:
  - `GatherBuddy/AutoGather/AtkReaders/ItemSlot.cs:10-15`
  - `GatherBuddy/AutoGather/AutoGather.Gather.cs:137-147`
- Node interaction uses `TargetSystem.Instance()->OpenObjectInteraction(...)`:
  - `GatherBuddy/AutoGather/AutoGather.Gather.cs:22-35`
  - `GatherBuddy/AutoGather/AutoGather.Gather.cs:125-134`
- Teleporting uses ClientStructs `Telepo.Instance()->Teleport(...)`:
  - `GatherBuddy/SeFunctions/Teleporter.cs:31-36`
  - queued from `GatherBuddy/AutoGather/AutoGather.Movement.cs:532-545`
- Mounting/dismounting uses `ActionManager.UseAction(...)`:
  - `GatherBuddy/AutoGather/AutoGather.Movement.cs:27-61`

These are game/server-affecting interactions executed from automation logic.

### Movement and pathing automation

GatherBuddyReborn relies heavily on `vnavmesh` IPC:

- `GatherBuddy/Plugin/IpcSubscribers.cs:18-21` checks `vnavmesh` availability.
- `IpcSubscribers.cs:42-75` defines `vnavmesh.Nav.*` calls such as pathfinding.
- `IpcSubscribers.cs:124-154` defines `vnavmesh.Path.MoveTo`, `Stop`, `IsRunning`, and waypoint APIs.
- `IpcSubscribers.cs:168-175` defines `vnavmesh.SimpleMove.PathfindAndMoveTo/CloseTo`.

Movement implementation:

- `GatherBuddy/AutoGather/AutoGather.Movement.cs:226-280` calculates navigation targets and calls `PathfindCancelable`.
- `AutoGather.Movement.cs:282-381` drives path movement and fallback behavior.
- `AutoGather.Movement.cs:408-431` generates fly/ground paths.
- `AutoGather.Movement.cs:473-505` handles node landing offsets and mesh correction.

It also has raw movement override hooks:

- `GatherBuddy/AutoGather/Helpers/OverrideMovement.cs:40-72` defines signature-based walk/fly input hooks.
- `OverrideMovement.cs:74-80` initializes hooks from attributes.
- `OverrideMovement.cs:90-113` detours movement input toward a desired position.

Policy implication: autonomous movement/pathing is a high-risk pattern for a public achievement tracker and should not be copied.

### Synthetic UI/addon interaction

GatherBuddyReborn wraps addon callbacks in helpers:

- `GatherBuddy/Automation/AddonMaster.cs:35` calls `Base->ReceiveEvent(...)`.
- `AddonMaster.cs:67-72` force-enables `SelectYesno` and fires a yes callback.
- `AddonMaster.cs:84-117` selects entries from `SelectString`.
- `AddonMaster.cs:122-140` simulates Talk addon mouse events.
- `AddonMaster.cs:161-169` fires Contents Finder commence callbacks.

Diadem and NPC automation uses those helpers:

- `GatherBuddy/AutoGather/AutoGather.cs:1065-1127` navigates to NPCs, opens object interaction, selects dialogue, confirms yes/no, clicks talk, and commences duty.
- `AutoGather.cs:1991-2015` automates leaving Diadem.
- `AutoGather.cs:2092-2129` closes gathering/masterpiece addons and confirmations.

Policy implication: this is synthetic interface interaction for risky/server-affecting actions. For our tracker, synthetic callbacks should be treated as beyond the public-safe branch unless a maintainer explicitly approves the exact flow.

### Crafting/Vulcan automation

GatherBuddyReborn includes crafting queue and Vulcan components. This is especially policy-relevant because official restrictions name automated crafting as a non-starter.

- `GatherBuddy/Crafting/CraftingQueueProcessor.cs:88-128` starts a crafting queue.
- `CraftingQueueProcessor.cs:159-194` processes crafting state.
- `CraftingQueueProcessor.cs:488-625` selects recipes, applies consumables, decides quick synthesis, sets solver/macro, and calls `CraftingGameInterop.StartCraft(...)`.
- `GatherBuddy/Crafting/CraftingGameInterop.cs:710-724` fires `RecipeNote` callback to craft.
- `CraftingGameInterop.cs:733-760` opens quick synthesis by callback.
- `CraftingGameInterop.cs:895-931` confirms quick synthesis quantity/HQ/NQ by callback.
- `GatherBuddy/Crafting/CraftingActionExecutor.cs:26-64` maps Vulcan skills to action IDs and calls `ActionManager.Instance()->UseAction(...)`.

Policy implication: do not use this as a model for official-compatible Achievement Ledger behavior.

### Vendor purchases and collectable turn-ins

- `GatherBuddy/Vulcan/Vendors/VendorPurchaseManager.cs:399-499` opens shops through dialogue/menu interaction.
- `VendorPurchaseManager.cs:502-550` selects and purchases items by callback.
- `VendorPurchaseManager.cs:922-980` confirms purchases and retries confirmations.
- `GatherBuddy/AutoGather/Collectables/CollectableManager.cs:87-153` starts collectable turn-in queues.
- `CollectableManager.cs:173-224` navigates, opens turn-ins, selects job/item, submits, checks cap dialogs, and runs purchases.
- `GatherBuddy/AutoGather/Collectables/CollectableWindowHandler.cs:14-77` directly fires addon callbacks to select job, select item, submit item, and close the window.

Policy implication: purchases and turn-ins are server-affecting actions and should be considered high-risk if automated.

### Raw memory, signatures, and hooks

Representative examples:

- `GatherBuddy/FishTimer/Parser/FishingParser.cs:13-30` defines hooks and hooks `ActionManager.MemberFunctionPointers.UseAction`.
- `FishingParser.cs:53-85` detours fish-catch update and `UseAction`.
- `GatherBuddy/SeFunctions/UpdateFishCatch.cs:9-13` contains a hardcoded signature.
- `GatherBuddy/SeFunctions/SeFunctionBase.cs:19-43` scans signatures and creates delegates.
- `GatherBuddy/AutoGather/Helpers/OverrideMovement.cs:66-113` hooks movement input.
- `GatherBuddy/Gui/NativeItemTooltipBridge.cs:19-24` and `NativeItemTooltipBridge.cs:137-143` use a hardcoded signature/native function pointer for tooltips.

Policy implication: raw hooks/signatures are the third and riskiest Dalamud interaction tier.

### Backend/network calls

GatherBuddyReborn has non-game-server HTTP use:

- Universalis API:
  - `GatherBuddy/Marketboard/UniversalisService.cs:12-18` defines `https://universalis.app/api/v2`, request limits, and retries.
  - `UniversalisService.cs:20-27` creates `HttpClient` with a plugin user agent.
  - `UniversalisService.cs:98-112` performs throttled `GetAsync` requests.
- Fish record upload:
  - `GatherBuddy/FishTimer/FishRecorder.Remote.cs:21` defines an AWS endpoint.
  - `FishRecorder.Remote.cs:23-31` batches records.
  - `FishRecorder.Remote.cs:87-125` serializes local fish records and posts JSON.

Policy implication: external HTTP is not inherently banned, but it must meet the backend guidance. For Achievement Ledger, external backend calls should be avoided unless the feature absolutely needs them; static game data should come from Lumina/local sheets.

## Artisan analysis

### Overall ranking

- **Automation risk**: 5 / 5
- **API-layer risk**: 5 / 5
- **Official-guideline fit**: poor for official-repo-safe automation patterns because automated crafting is directly named as a non-starter in current restrictions.
- **Server interaction model**: framework-update loops, task managers, solver-selected action execution, craft starts, quick synthesis, repair, materia extraction, retainer interaction, and addon callbacks.
- **Backend/network model**: mostly informational HTTP calls to Universalis/GitHub/Teamcraft-style resources; the core risk is in-game automation, not backend communication.

### Main update loop

Artisan runs repeated automation from its framework update handler:

- `Artisan/Artisan.cs:41-42` creates task managers.
- `Artisan/Artisan.cs:98-106` initializes crafting, consumables, endurance, IPC, retainer info, context menu, and watcher components.
- `Artisan/Artisan.cs:115` subscribes `Svc.Framework.Update += OnFrameworkUpdate`.
- `Artisan/Artisan.cs:197-210` runs `Crafting.Update()`, `PreCrafting.Update()`, and `Endurance.Update()` on framework updates.
- `Artisan/Artisan.cs:212-214` repeats trial craft when configured.

Policy implication: repeated framework-driven automation is high risk when it triggers server-affecting actions.

### Crafting solver and action execution

Artisan's core automation is craft-solver-driven action use:

- `Artisan/CraftingLogic/CraftingProcessor.cs:39-50` registers solvers.
- `CraftingProcessor.cs:178-185` computes a first solver recommendation.
- `CraftingProcessor.cs:200-205` computes the next recommendation on craft advance.
- `Artisan/UI/CraftingWindow.cs:225-240` queues `ActionManagerEx.UseSkill(...)` when auto mode or IPC override is active.
- `Artisan/GameInterop/ActionManagerEx.cs:21-30` maps a skill to an action ID and calls `ActionManager.Instance()->UseAction(...)`.
- `ActionManagerEx.cs:31-35` resets AFK/input timers after skill use.

Policy implication: direct automated crafting action execution lands in the highest-risk category and directly overlaps the official non-starter example.

### ClientStructs action and item use

- `Artisan/GameInterop/ActionManagerEx.cs:1-4` imports FFXIVClientStructs game/UI namespaces.
- `ActionManagerEx.cs:13` calls `ActionManager.Instance()->GetActionStatus`.
- `ActionManagerEx.cs:30` calls `ActionManager.Instance()->UseAction`.
- `ActionManagerEx.cs:40-42` exposes `UseItem`, `UseRepair`, and `UseMateriaExtraction` through `UseAction`.

Policy implication: this is not just reading state through documented Dalamud wrappers; it directly executes game actions.

### Raw hooks and signatures

- `Artisan/GameInterop/Crafting.cs:75-81` hooks a crafting event handler with `Svc.Hook.HookFromSignature(...)`.
- `Artisan/GameInterop/PreCrafting.cs:52-58` hooks synthesis button, gearset callback, and cosmic recipe callback signatures.
- `PreCrafting.cs:61-75` detours cosmic button handling.
- `PreCrafting.cs:78-98` detours `SelectYesno` callback behavior.
- `PreCrafting.cs:599-617` intercepts synthesis button clicks and starts automation.

Policy implication: raw hooks/signatures are the least stable and highest-review tier in the Dalamud interaction guidance.

### Pre-crafting task automation

`Artisan/GameInterop/PreCrafting.cs` queues the setup work before crafting:

- `PreCrafting.cs:108-128` processes a task list.
- `PreCrafting.cs:169-210` queues exit craft stance, class change, required-item equip, consumable use, recipe selection, and craft start.
- `PreCrafting.cs:327-349` exits craft stance with addon callbacks.
- `PreCrafting.cs:357-397` changes gearsets with `RaptureGearsetModule.Instance()->EquipGearset`.
- `PreCrafting.cs:410-423` equips required items by moving inventory slots.
- `PreCrafting.cs:428-491` uses manuals, food, and potions with `ActionManagerEx.UseItem`.
- `PreCrafting.cs:503-540` opens/selects recipes with `AgentRecipeNote.Instance()->OpenRecipeByRecipeId` and callbacks.
- `PreCrafting.cs:545-564` starts craft by firing addon callbacks.

Policy implication: this is a full task-runner for server-affecting setup and craft start.

### Endurance mode and crafting lists

Endurance mode repeats crafting automatically:

- `Artisan/Autocraft/Endurance.cs:77-89` toggles endurance.
- `Endurance.cs:288-407` runs repair, materia extraction, class change, required item equip, consumable use, recipe selection, quick synth, or normal craft.
- `Endurance.cs:399-402` queues quick synthesis.
- `Endurance.cs:451-485` has error-throttle logic and aborts tasks after repeated errors.

Crafting lists batch-process recipes:

- `Artisan/CraftingList/CraftingListUI.cs:102-104` exposes a Start Crafting List action.
- `CraftingListUI.cs:178-201` expands recipe quantities and sets processing state.
- `Artisan/CraftingList/CraftingList.cs:214-265` processes current items and closes/exits at the end.
- `CraftingList.cs:333-347` automates class change and required item equip.
- `CraftingList.cs:366-374` automates materia extraction and repair.
- `CraftingList.cs:401-408` queues consumables.
- `CraftingList.cs:414-435` queues recipe selection and quick synthesis.
- `CraftingList.cs:507-508` clicks cosmic HQ/NQ buttons.
- `CraftingList.cs:540-547` clicks recipe material/context-menu callbacks.

Policy implication: this is unattended batch crafting automation and should not inform a public Achievement Ledger branch except as a boundary example.

### Synthetic addon/interface interaction

Representative examples:

- `Artisan/GameInterop/Operations.cs:36-81` fires `RecipeNote` and `SynthesisSimpleDialog` callbacks for quick synthesis.
- `Operations.cs:136-155` starts craft through cosmic or `RecipeNote` callbacks.
- `Artisan/GameInterop/PreCrafting.cs:527` fires a callback to select a cosmic recipe entry.
- `PreCrafting.cs:553-564` fires callbacks to start crafts.
- `Artisan/Tasks/TaskSelectRetainer.cs:185-190` fires context-menu callback to retrieve retainer items.
- `TaskSelectRetainer.cs:206` fires numeric input callback.
- `TaskSelectRetainer.cs:221` clicks a close button.
- `TaskSelectRetainer.cs:258` selects a `SelectString` entry.
- `Artisan/Autocraft/RepairManager.cs:32-34` calls `AddonMaster.Repair(...).RepairAll()`.
- `RepairManager.cs:47` confirms `SelectYesno`.

Policy implication: synthetic UI callbacks are a major risk amplifier because they can submit/confirm actions without direct user input at the moment of submission.

### Retainer, repair, and materia automation

Retainer restocking:

- `Artisan/IPC/RetainerInfo.cs:84-105` subscribes to AllaganTools IPC.
- `RetainerInfo.cs:337-383` hooks framework tick, suppresses AutoRetainer, interacts with bells, selects retainers, selects entrust, extracts items, and closes windows.
- `RetainerInfo.cs:452-497` implements a similar restock flow for crafting-list materials.
- `Artisan/Tasks/TaskInteractWithNearestBell.cs:146-157` calls `TargetSystem.Instance()->InteractWithObject` on nearby bells.
- `Artisan/Tasks/TaskSelectRetainer.cs:30-44` selects retainers and reads retainer manager state.

Repair and materia:

- `Artisan/Autocraft/Endurance.cs:143-169` exposes automatic repair and materia extraction toggles.
- `Endurance.cs:345-353` runs materia extraction and repair during endurance.
- `Artisan/CraftingList/CraftingList.cs:366-374` runs extraction and repair for lists.
- `Artisan/Autocraft/RepairManager.cs:171-179` opens repair NPC interactions and fires select-icon callback.
- `RepairManager.cs:224-228` confirms yes/no and repair all.
- `RepairManager.cs:236-248` interacts with repair NPCs and uses the repair general action.

Policy implication: these systems combine object interaction, UI callbacks, direct action use, and automation loops.

### IPC-triggered automation

Artisan exposes automation to other plugins:

- `Artisan/IPC/IPC.cs:41-70` registers IPC providers including endurance status, `CraftItem`, `StartListById`, and config mutation endpoints.
- `IPC.cs:187-201` implements `CraftX(recipeId, amount)`, sets `Endurance.IPCOverride`, and enables endurance automation.
- `IPC.cs:209-211` reports busy state.
- `IPC.cs:550-563` starts crafting lists by IPC.

Policy implication: IPC automation can remove direct human interaction even further. We should not expose achievement automation through IPC unless it is strictly read-only or user-confirmed.

### Movement and pathing

Artisan does not appear to have production pathfinding like GatherBuddyReborn:

- No production `vnavmesh` pathing/movement automation was found in targeted search.
- `Artisan/UI/DebugTab.cs:298-300` has a debug-only `vnavmesh.Stop` call.
- `DebugTab.cs:735-757` has a debug helper for teleporting to a grand company town.

It still performs object/NPC interaction for server-affecting flows:

- retainer bell interaction in `TaskInteractWithNearestBell.cs:146-157`
- repair NPC interaction in `RepairManager.cs:171-179`

Policy implication: Artisan has less autonomous movement risk than GatherBuddyReborn, but very high crafting/action/callback risk.

### Backend/network calls

Artisan network use appears mostly informational:

- `Artisan/Universalis/UniversalisClient.cs:15-22` creates an HTTP client.
- `UniversalisClient.cs:149-152` performs HTTP GETs to Universalis.
- `Artisan/RawInformation/ExtendedIngredient.cs:101-106` starts marketboard fetches when enabled and not on-demand.
- `Artisan/UI/Tables/IngredientTable.cs:421-425` starts DC/region fetches from the UI.
- `Artisan/RawInformation/DropSources.cs:35-37` fetches Teamcraft drop-source JSON from GitHub.

Policy implication: this is not the core risk compared to automated crafting. If our tracker needs external data, it still needs explicit opt-in/privacy review, but the safer approach is to avoid external servers and use Lumina/local data.

## Comparison table

| Category | GatherBuddyReborn | Artisan | Achievement Ledger implication |
| --- | --- | --- | --- |
| Primary automation | Gathering, fishing, pathing, turn-ins, vendors, crafting | Crafting, quick synth, lists, repair, materia, retainer restock | Do not copy unattended loops into public/beta tracker |
| Dalamud APIs | Heavy use | Heavy use | Good baseline, but not sufficient by itself |
| ClientStructs | Heavy use for action, target, teleport, UI | Heavy use for action, gearset, target, UI | Use only if documented services cannot work, and keep bounded |
| Raw hooks/signatures | Fishing parser, movement override, tooltip/native functions | Crafting events/buttons/yes-no hooks | Avoid for public tracker unless passive and review-gated |
| Addon callbacks | Frequent: selects, confirms, gathers, buys, turn-ins, duty commence | Frequent: craft start, quick synth, retainer, repair, confirmations | Avoid synthetic callbacks for risky actions |
| Movement/pathing | Extensive `vnavmesh` + movement override hooks | No clear production pathing | Avoid movement/pathing entirely |
| Repeated task queues | Yes | Yes | Avoid automatic repeated server-affecting queues |
| Backend calls | Universalis + fish upload endpoint | Universalis/GitHub/Teamcraft-style info fetches | Prefer no backend; Lumina/local data where possible |
| Official guideline fit | High risk; automation-forward | High risk; automated crafting non-starter | Current passive UI-assisted model is safer |

## Server calls vs interface interaction

Neither plugin primarily constructs raw FFXIV network packets in the reviewed code. Instead, both rely on game-client interfaces that cause the normal client to submit actions to the server:

- `ActionManager.UseAction(...)`
- `TargetSystem.OpenObjectInteraction(...)` / `InteractWithObject(...)`
- `Telepo.Instance()->Teleport(...)`
- addon callback firing (`Callback.Fire`, `addon->FireCallback`, `ReceiveEvent`)
- task queues that invoke the above repeatedly

This matters because avoiding raw packet code does **not** make the automation low risk. Dalamud restrictions focus on automatic interaction with game servers and outside-spec behavior, not only on packet injection. Synthetic UI callbacks and ClientStructs action calls can still cause automated server-affecting behavior.

For our achievement tracker, a safer distinction is:

- **Safer**: open the native Achievement UI because the user clicked `Update Next`; passively record what the native UI/player action causes to load.
- **Riskier**: plugin calls an achievement progress request on a timer or queue.
- **Riskier still**: plugin fires native addon callbacks to navigate categories/entries or submit/confirm actions repeatedly.
- **Out of scope for public branch**: packet/network capture or packet-originated action automation.

## Recommended automation branch options for Veela's Achievement Ledger

### Option A: Public-safe assisted branch

Risk: 1-2 / 5

Features:

- Keep `/val` tracker UI.
- Keep `Update Next` and row reload icon.
- User click opens the native Achievement entry.
- Plugin passively records progress already returned to the native Achievement UI.
- No plugin-originated progress request queue.
- No background refresh loop.
- No synthetic category/subcategory/entry callback navigation unless explicitly reviewed.
- No external backend.
- No IPC automation endpoints except read-only status.

This remains closest to current Dalamud guidance and the current beta due-diligence story.

### Option B: Research-only user-triggered refresh branch

Risk: 3 / 5

Features:

- A user click may trigger one bounded progress-related ClientStructs call or native UI action.
- The UI clearly shows that the update is user-triggered.
- No automatic polling.
- No queue that chains multiple server-affecting calls without a fresh user action.
- Add throttling only as a safety guard, not as a way to disguise automation.
- Add code comments citing the Dalamud restrictions.
- Keep all risky code isolated behind a tiny adapter with tests around the queue/throttle logic.

This is where we would experiment if we decide to accept moderate risk. It should not be the public/beta default without maintainer feedback.

### Option C: Private/high-risk automation experiment

Risk: 4-5 / 5

Features that would put us here:

- automatic refresh queues for many achievements,
- framework-update loops that request progress,
- synthetic addon callbacks to navigate the Achievement UI,
- packet/network observation,
- IPC endpoints that trigger refreshes,
- any action that repeatedly interacts with game servers without a direct user click.

This is comparable in architecture to the automation parts of GatherBuddyReborn/Artisan. If pursued, it should be explicitly separated from the public branch, documented as research/private-risk, disabled by default, and excluded from official-submission positioning.

## Concrete guardrails for implementation planning

1. **Preserve the safe mainline**
   - Keep `main` and release/beta branches aligned with native UI assisted update and passive observation.
   - Do not merge research automation into public release paths until reviewed.

2. **Separate risky adapters**
   - Put any ClientStructs/action/addon interaction in a small adapter namespace.
   - Keep unsafe pointers and hooks out of UI classes.
   - Add comments that cite `plugin-publishing/restrictions.md` and `plugin-development/interaction/index.md`.

3. **No automatic server-affecting loops**
   - No `Framework.Update` queue that triggers achievement server requests.
   - No timer-based achievement refresh.
   - No refresh-on-zone-change/job-change/chat-message heuristics.

4. **No synthetic confirmation/submission callbacks**
   - Do not follow GatherBuddyReborn/Artisan's pattern of firing addon callbacks for confirmations, purchases, turn-ins, craft starts, etc.
   - For the Achievement UI, prefer opening a native entry over manually driving category/subcategory callbacks.

5. **No movement/pathing**
   - Achievement tracking does not need `vnavmesh`, movement overrides, object interaction, or teleport automation.

6. **No backend by default**
   - Use Lumina/local sheets for achievement metadata and targets.
   - Avoid telemetry or shared backend services unless a feature absolutely requires it and the data/opt-in model is reviewed.

7. **No IPC-triggered automation**
   - IPC may expose read-only state if useful.
   - Do not expose `RefreshAchievement`, `RefreshAll`, or similar automation IPC unless it is explicitly user-mediated and reviewed.

8. **Review before implementation**
   - Run deterministic searches for `UseAction`, `Callback.Fire`, `FireCallback`, `ReceiveEvent`, `HookFromSignature`, `Signature`, `Framework.Update`, `HttpClient`, and `Task.Run` before merging automation experiments.
   - Run an adversarial policy/security review for any branch that uses ClientStructs, hooks, networking, background tasks, or addon callbacks.

## Bottom line

GatherBuddyReborn and Artisan show how far Dalamud plugins can technically go: framework-loop task managers, direct action calls, addon callback firing, object interaction, pathing, crafting/gathering automation, and raw hooks/signatures. They are useful references for implementation mechanics, but they are not good templates for an official-submission-safe achievement tracker.

For Veela's Achievement Ledger, the safest and most defensible plan remains:

- user chooses what to track,
- user clicks `Update Next` or the row reload icon,
- plugin opens the native Achievement UI entry,
- plugin passively observes/cache updates,
- no automatic refresh queues,
- no server request polling,
- no synthetic UI callback chains,
- no packet/network automation.

If we want an automation branch, create it as a clearly marked research path and decide up front which risk band we are willing to accept before implementing anything server-affecting.
