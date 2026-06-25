# Dalamud Conventions

Dalamud docs at <https://dalamud.dev> are the source of truth. Read the relevant docs before changing APIs, hooks, services, ClientStructs, or native UI interaction.

## API preference order

Prefer the safest source that can satisfy the feature:

1. Documented Dalamud services.
2. Lumina/local sheet data through `IDataManager`.
3. Existing local plugin state/config/cache.
4. ClientStructs/native UI interaction only when needed and isolated behind a small adapter.
5. Hooks only when documented/understood, disposed symmetrically, and branch-appropriate.

## Branch stance

Public/beta-safe branches should keep the plugin shape small:

- `/achex` opens the ledger.
- Row reload/open actions use the native Achievement UI.
- Numeric progress is cached only when the native Achievement UI returns progress data.
- Tracked achievement IDs persist between logouts.
- Observed ordinary achievement progress cache resets on login/logout.

Do not reintroduce plugin-originated progress requests, polling loops, background refresh queues, diagnostics UI, packet/network experiments, backend sync, telemetry, or leaderboards on public-safe branches.

The `achex-experimental` branch is explicitly not intended for Dalamud publishing. On that branch, experimental auto refresh queues, activity-triggered refreshes, WKS/Cosmic reads, and debug instrumentation may exist if clearly labeled, isolated, easy to remove, and covered by lifecycle/security review.

## Native/ClientStructs boundaries

Keep native and unsafe code in small adapters:

- `NativeAchievementNavigator` owns `AgentAchievement`, `IGameGui`, and Achievement addon show/hide/park/restore behavior.
- `ClientAchievementProgressSource` owns passive reads from the native Achievement progress slot.
- `PassiveAchievementProgressObserver` owns experimental progress hooks.
- `CosmicClassProgressProvider` owns experimental WKS score reads.

Rules:

- Never store raw pointers across frames.
- Null-check native singleton/addon pointers every access.
- Treat addon readiness/visibility/address checks as mandatory before dereferencing.
- Do not broaden unsafe code into windows or general services.
- Prefer pure policy helpers for decisions around native behavior, then call the unsafe adapter narrowly.

## Event and lifecycle rules

Subscribe and unsubscribe symmetrically:

- `PluginInterface.UiBuilder.Draw` ↔ unsubscribe in `Dispose`.
- `OpenMainUi` / `OpenConfigUi` ↔ unsubscribe in `Dispose`.
- `Framework.Update` ↔ unsubscribe in `Dispose`.
- `ClientState.Login` / `Logout` ↔ unsubscribe in `Dispose`.
- `IChatGui.LogMessage` in `AchievementActivityUpdateObserver` ↔ unsubscribe in its `Dispose`.
- Dalamud hooks in `PassiveAchievementProgressObserver` ↔ dispose hooks in its `Dispose`.

Background timers/tasks must not survive disposal. Prefer driving periodic behavior from `IFramework.Update` with explicit state and clear/circuit-break logic.

## Native Achievement refresh policy

The current experimental refresh path intentionally serializes native Achievement UI actions:

- The UI requests an update/open through `Plugin.Enqueue*` or `OpenNativeAchievementForInspection`.
- `Plugin` filters IDs for completion/native-open eligibility.
- `AchievementProgressUpdater` schedules and serializes native actions.
- `NativeAchievementNavigator.OpenAchievement` uses the native Achievement agent.
- `ClientAchievementProgressSource` records observed progress when the native slot/hook reports data.

Do not bypass this with direct achievement progress server calls unless the branch and user explicitly approve it. Do not add a second queue/throttler.

## Config and persistence

- Persist plugin config through `Configuration.Save()` / `Plugin.SaveConfiguration()` / `Plugin.SaveTrackedAchievements()`.
- Normalize new config values in `Configuration.NormalizeAutoUpdateSettings()` or a clearly named normalization helper.
- Tracked achievement IDs should remain saved across logouts.
- Ordinary observed progress cache should reset on login/logout.
- Cosmic score cache is persisted separately as experimental cached WKS-derived data.

## Local analysis artifacts

Local source/tool snapshots and logs are useful for development but must not be committed:

- `local-src/`
- `.magellan/`
- `*.magellan.db`
- `sharptools-logs/`
- `.hermes/`

If a future analysis produces durable findings, promote the findings into `docs/architecture/`, not the raw tool output.
