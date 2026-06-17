# Code map for Achieve Ex+

This folder is a human-friendly map of the C# codebase. It is written for someone who is more comfortable with Python than C#.

Start here:

1. [Whole plugin hierarchy](./00-whole-plugin-hierarchy.md) — the full top-down map of the plugin.
2. [Big picture](./01-big-picture.md) — what the plugin does and how data moves.
3. [Function call map](./02-function-call-map.md) — important functions and what they call.
4. [Cosmic Class cache flow](./03-cosmic-cache-flow.md) — exactly where Cosmic scores are read and saved.
5. [UI/window map](./04-ui-window-map.md) — buttons and what code they trigger.
6. [Data model map](./05-data-model-map.md) — config, presets, progress values.
7. [Safety map](./06-safety-map.md) — direct-request/automation boundaries.
8. [File index](./07-file-index.md) — every C# file and its main members.
9. [C# primer for Python readers](./08-csharp-for-python-readers.md) — C# concepts used in this plugin, translated into Python mental models.
10. [Dalamud layer model](./09-dalamud-layer-model.md) — OSI-style hierarchy of Dalamud, ClientStructs, plugin code, native game surfaces, and version groupings.

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
