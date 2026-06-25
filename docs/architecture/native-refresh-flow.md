# Native Refresh Flow

This page documents the fragile native Achievement refresh/inspection flows. Keep it current whenever queueing, eligibility, native window parking, or progress observation changes.

## Manual row inspection

Purpose: user asks to open a specific native Achievement entry.

```mermaid
sequenceDiagram
    participant UI as Tracker/Config Window
    participant Plugin
    participant Catalog as AchievementCatalog
    participant Updater as AchievementProgressUpdater
    participant Navigator as NativeAchievementNavigator
    participant Native as AgentAchievement / Addon

    UI->>Plugin: OpenNativeAchievementForInspection(achievementId)
    Plugin->>Catalog: CanOpenInNativeAchievementUi(id)
    alt native unsafe
        Plugin-->>UI: false
        Plugin->>Plugin: DebugLog skipped reason
    else native safe
        Plugin->>Updater: QueueInspection(id, "manual-inspect")
        Updater->>Updater: enqueue inspection in unified native queue
        Plugin-->>UI: true
        Updater->>Navigator: OpenAchievement(id) on Framework.Update
        Navigator->>Native: AgentAchievement.OpenById(id)
        Updater->>Navigator: request restore/reset if needed
    end
```

Notes:

- Inspection goes through the same serialized native queue as refreshes.
- `NativeAchievementNavigator` owns all direct native agent/addon calls.
- Inspection is user-visible and should restore parked native window state.

## Manual / timed / activity refresh enqueue

Purpose: choose eligible IDs and queue native Achievement refresh actions.

```mermaid
flowchart TD
    A[UI or trigger asks to update IDs] --> B[Plugin.EnqueueUpdate*]
    B --> C[UpdateEligibilityPolicy.Evaluate]
    C --> D{Each normalized ID}
    D -->|zero/duplicate| E[ignore]
    D -->|native unsafe| F[report native unsafe skip]
    D -->|complete| G[report completed skip]
    D -->|eligible| H[eligible IDs in original distinct order]
    F --> I[report auto-update removal if configured]
    G --> I
    H --> J[AchievementProgressUpdater.EnqueueUpdateAll]
    I --> K[Plugin applies config save/reset/log side effects]
    J --> L[AchievementProgressRequestScheduler schedules due times]
```

Semantic owners:

- `UpdateEligibilityPolicy` owns pure filtering and returned intent.
- `Plugin` owns config mutation, debug logging, and completion-observation side effects.
- `AchievementProgressUpdater` owns queue run state and native action lifecycle.
- `AchievementProgressRequestScheduler` owns spacing, dedupe, backoff, and dirty activity-key behavior.

## Refresh execution lifecycle

Purpose: execute one scheduled native refresh safely.

```mermaid
sequenceDiagram
    participant Updater as AchievementProgressUpdater
    participant Scheduler as AchievementProgressRequestScheduler
    participant Navigator as NativeAchievementNavigator
    participant Native as Native Achievement UI
    participant Source as ClientAchievementProgressSource

    Updater->>Scheduler: TryTakeDueRequest(now)
    Scheduler-->>Updater: ScheduledAchievementProgressRequest
    Updater->>Updater: enforce native cooldown / same-ID backoff
    Updater->>Navigator: OpenAchievement(id)
    Navigator->>Native: AgentAchievement.OpenById(id)
    alt open failed
        Updater->>Updater: RegisterNativeFailure
        Updater->>Scheduler: MarkActivityJobSettled
    else open sent
        Updater->>Navigator: park window if refresh opened it from closed state
        Updater->>Updater: set ActiveNativeAchievementRequest
        loop Framework.Update until min wait and max timeout
            Updater->>Source: TryGetFreshObservation(id, startedAt)
            Source->>Native: read progress slot if due
            alt matching fresh observation
                Source-->>Updater: current/max
                Updater->>Updater: RegisterNativeSuccess
                Updater->>Updater: CompleteRefreshWindowLifecycle
                Updater->>Scheduler: MarkActivityJobSettled
            else timeout
                Updater->>Updater: RegisterNativeFailure
                Updater->>Updater: CompleteRefreshWindowLifecycle
                Updater->>Scheduler: MarkActivityJobSettled
            end
        end
    end
```

Key invariants:

- Native opens happen from `Framework.Update`, not directly from arbitrary UI draw logic.
- A refresh waits at least `RefreshMinimumWait` before accepting an observation.
- A refresh times out after `RefreshMaximumWait`.
- Repeated failures trip the native circuit breaker and clear pending native actions.
- Same-achievement backoff prevents rapid repeated opens for one ID.

## Activity-triggered refresh

Purpose: convert local craft/gather log events into scoped refreshes without broad text heuristics.

```mermaid
flowchart TD
    A[IChatGui.LogMessage] --> B[AchievementActivityUpdateObserver]
    B --> C[AchievementActivityUpdateClassifier.TryClassify]
    C -->|unknown LogMessageId| D[ignore]
    C -->|known trigger/category| E{trigger enabled?}
    E -->|no| F[debug disabled]
    E -->|yes| G[SelectTrackedIdsForCategory]
    G --> H[ActivityTriggerCandidateSelection excludes Cosmic/WKS]
    H --> I[ActivityTriggerDelayPolicy initial delay]
    I --> J[AchievementProgressUpdater.EnqueueActivityUpdateAll]
    J --> K[AchievementProgressRequestScheduler coalesces same activity key]
```

Key invariants:

- Use known `LogMessageId` classification, not broad text fallbacks.
- Same `(trigger, category)` bursts coalesce; different keys can still queue normally.
- Cosmic/WKS achievements are excluded because `CosmicClassProgressProvider` owns those progress values.

## Cosmic/WKS progress display

Purpose: show Cosmic class score progress without ordinary refresh requests.

```mermaid
flowchart TD
    A[Framework.Update every ~5s] --> B[Plugin.RefreshCosmicCacheFromLiveState]
    B --> C[CosmicClassProgressProvider.RefreshCacheFromLiveScores]
    C --> D{WKSManager loaded?}
    D -->|yes| E[read 11 class scores, normalize, save cache if changed]
    D -->|no| F[keep existing cache]
    G[Window draws row] --> H[AchievementProgressService.GetProgress]
    H --> I{CosmicClassProgressProvider handles row?}
    I -->|yes| J[use live scores or cached scores]
    I -->|no| K[ordinary IUnlockState/observed progress path]
```

Key invariants:

- WKS reads stay inside `CosmicClassProgressProvider`.
- Cosmic cache is persisted in plugin config.
- Cosmic achievements should not be routed through ordinary activity-trigger refresh candidates.

## Login/logout reset

Purpose: keep persisted tracking but clear session-only observed ordinary progress.

```mermaid
flowchart TD
    A[IClientState.Login or Logout] --> B[Plugin.ResetProgressState]
    B --> C[AchievementProgressSource.ClearCache]
    B --> D[AchievementProgressUpdater.Clear]
    B --> E[clear pending native window scale reset]
    F[TrackedAchievementStore + config] --> G[persist unchanged]
```

Key invariant: tracked IDs persist; observed ordinary progress does not.

## What not to add

- Do not add a second refresh queue/throttler.
- Do not bypass `UpdateEligibilityPolicy` for refresh candidates.
- Do not move native agent/addon calls out of `NativeAchievementNavigator`.
- Do not store native pointers across frames.
- Do not treat Cosmic/WKS progress as ordinary queue-refresh progress.
- Do not add direct progress/server requests unless the branch/user explicitly approves that experimental surface.
