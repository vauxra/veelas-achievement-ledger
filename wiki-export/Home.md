# Code map for Veela's Achievement Ledger

This folder is a human-friendly map of the C# codebase. It is written for someone who is more comfortable with Python than C#.

Start here:

1. [Whole plugin hierarchy](Whole-plugin-hierarchy) — the full top-down map of the plugin.
2. [Big picture](Big-picture) — what the plugin does and how data moves.
3. [Function call map](Function-call-map) — important functions and what they call.
4. [Cosmic Class cache flow](Cosmic-Class-cache-flow) — exactly where Cosmic scores are read and saved.
5. [UI/window map](UI-window-map) — buttons and what code they trigger.
6. [Data model map](Data-model-map) — config, presets, progress values.
7. [Safety map](Safety-map) — direct-request/automation boundaries.
8. [File index](File-index) — every C# file and its main members.
9. [C# primer for Python readers](CSharp-for-Python-readers) — C# concepts used in this plugin, translated into Python mental models.
10. [Dalamud layer model](Dalamud-layer-model) — practical hierarchy of VAL plugin code, Dalamud services, native adapters, game surfaces, guardrails, and version groupings.

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
