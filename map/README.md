# Code map for Veela's Achievement Ledger

Version: `v0.2.0.33`

This folder is a human-friendly map of the C# codebase. It is written for someone who is more comfortable with Python than C# and wants to know which plugin, Dalamud, and game-client components are touched.

## Ordered reading list

1. [Home / code map](./README.md) — this page, reading order, color legend, and mental model.
2. [Whole plugin hierarchy](./00-whole-plugin-hierarchy.md) — the full top-down map of the plugin.
3. [Big picture](./01-big-picture.md) — user actions, component level, and important call chains.
4. [Function call map](./02-function-call-map.md) — important functions, what they call, and component safety labels.
5. [Cosmic Class cache flow](./03-cosmic-cache-flow.md) — exactly where Cosmic scores are read and saved.
6. [UI/window map](./04-ui-window-map.md) — buttons and what code they trigger.
7. [Data model map](./05-data-model-map.md) — saved config, in-memory state, load/save timing, and Dalamud best-practice notes.
8. [Safety map](./06-safety-map.md) — direct-request/automation boundaries.
9. [File index](./07-file-index.md) — source-file locator for jumping from wiki concepts to concrete classes/methods.
10. [C# primer for Python readers](./08-csharp-for-python-readers.md) — C# concepts used in this plugin, translated into Python mental models.
11. [Dalamud layer model](./09-dalamud-layer-model.md) — simplified hierarchy diagrams for VAL, Dalamud services, ClientStructs/native surfaces, and guardrails.


## TLP / layer color legend

This wiki uses traffic-light color labels to show what kind of component the plugin is touching and how safe/expected that surface is:

- 🟢 **TLP-GREEN — safe/supported component layer:** ordinary plugin code plus supported Dalamud services/libraries such as `WindowSystem`, ImGui helpers, `IDataManager`, `IUnlockState`, `IClientState`, `IFramework`, and plugin config APIs.
- 🟡 **TLP-YELLOW — native/ClientStructs read or UI adapter:** isolated adapters touching game-client surfaces such as `AgentAchievement`, `Achievement.Instance()`, or `WKSManager`. Acceptable when small, read-only/user-guided, and documented.
- 🔴 **TLP-RED — blocked/deprecated path:** raw memory scans, signatures, low-level hooks, `Dalamud.Hooking`, direct achievement-progress request queues. These should not be present in current mainline.

## Mental model

Think of the plugin like a small Python app with:

- `Plugin.cs` as the main application object / dependency container.
- `Windows/*.cs` as UI views.
- `Services/*.cs` as helper modules that do the actual work.
- `Models/*.cs` as dataclasses / value objects.
- `Configuration.cs` as the saved settings object.

C# names to translate mentally:

- `public sealed class Foo` ≈ `class Foo:` where other files can use it and it is not meant to be subclassed.
- `private void Method()` ≈ internal helper method, returns nothing.
- `public bool Method()` ≈ returns `True`/`False`.
- `uint` ≈ non-negative integer.
- `DateTimeOffset.UtcNow` ≈ `datetime.now(timezone.utc)`.
- `=>` ≈ short one-line return, like `lambda` or a one-line function.
