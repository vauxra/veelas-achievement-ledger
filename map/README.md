# Code map for Veela's Achievement Ledger

> **Documentation release:** `v0.2.0.20` / testing prerelease architecture refresh.
> **TLP legend:** 🟢 plugin/domain code, 🟡 Dalamud managed services or UI/data libraries, 🟠 isolated ClientStructs/native adapters, 🔴 blocked/deprecated policy paths.

This folder is a human-friendly map of the C# codebase. It is written for someone who is more comfortable with Python than C# and wants to know which plugin, Dalamud, and game-client components are touched.

## Ordered reading list

1. [Home / code map](./README.md) — this page, reading order, color legend, and mental model.
2. [Whole plugin hierarchy](./00-whole-plugin-hierarchy.md) — the full top-down map of the plugin.
3. [Big picture](./01-big-picture.md) — user actions, component level, and important call chains.
4. [Function call map](./02-function-call-map.md) — important functions, what they call, and TLP layer labels.
5. [Cosmic Class cache flow](./03-cosmic-cache-flow.md) — exactly where Cosmic scores are read and saved.
6. [UI/window map](./04-ui-window-map.md) — buttons and what code they trigger.
7. [Data model map](./05-data-model-map.md) — saved config, in-memory state, load/save timing, and Dalamud best-practice notes.
8. [Safety map](./06-safety-map.md) — direct-request/automation boundaries.
9. [File index](./07-file-index.md) — source-file locator for jumping from wiki concepts to concrete classes/methods.
10. [C# primer for Python readers](./08-csharp-for-python-readers.md) — C# concepts used in this plugin, translated into Python mental models.
11. [Dalamud layer model](./09-dalamud-layer-model.md) — simplified hierarchy diagrams for VAL, Dalamud services, ClientStructs/native surfaces, and guardrails.

## TLP / layer color legend

This wiki uses traffic-light color labels to show what layer a method or component touches:

- 🟢 **TLP-GREEN — plugin-owned safe layer:** VAL windows, stores, models, pure formatting, config models.
- 🟡 **TLP-YELLOW — Dalamud managed layer:** injected Dalamud services, WindowSystem, ImGui helpers, Lumina data reads, plugin config save/load.
- 🟠 **TLP-AMBER — native/ClientStructs read or UI adapter:** small isolated adapters touching `AgentAchievement`, `Achievement.Instance()`, or `WKSManager`.
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
