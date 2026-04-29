# System Interconnect Matrix

Date: `2026-04-29`
Status: `PENDING VERIFICATION`

Mandates followed:

- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `STRM_Persistent_Object_Registry.txt`

## Scope Correction

The request wording was inaccurate.

- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs` is a managed generic event bus for mod-facing traffic.
- `HectonEventBus` is not `NativeQueue`-backed.
- The first-party queue-backed nervous system is the set of static event lanes flushed by `SystemDispatcher.LateUpdate()`.

## Verified LateUpdate Flush Order

Source: `Assets/_Project/Scripts/Core/SystemDispatcher.cs`

1. `NarrativeEvents.FlushPending()`
2. `ScanEvents.FlushPending()`
3. `SaveEvents.FlushPending()`
4. `QuestEvents.FlushPending()`
5. `AudioLogEvents.FlushPending()`
6. `Hecton8.AtlasSignal.AtlasSignalEvents.FlushPending()`
7. `Hecton8.UI.NotificationEvents.FlushPending()`
8. `Hecton8.Bootstrap.SceneBootstrap.FlushPendingEvents()`
9. `ObjectPoolDiagnostics.FlushPending()`
10. `Hecton8.AtlasSignal.Atlas6Events.FlushPending()`

## Queue-Backed Lanes

| Lane Owner | Backing Queue | Listener Contract | Raise Surface | Flush Owner | Notes |
| --- | --- | --- | --- | --- | --- |
| `NarrativeEvents` | `NativeQueue<NarrativeEventPayload>` | `INarrativeEventListener` | `RaiseNarrativePOIRegistered`, `RaiseNarrativePOIDisposed`, `RaiseDiscoveryMade`, `RaiseDepthTierReached` | `SystemDispatcher.LateUpdate()` | Also has immediate `INarrativePointOfInterestListener` buckets outside the queue lane. |
| `ScanEvents` | `NativeQueue<ScanEventPayload>` | `IScanEventListener` | `RaiseScanTriggered`, `RaiseNodeFound`, `RaiseEntryDiscovered` | `SystemDispatcher.LateUpdate()` | Scanner lane for node discovery and radius scan traffic. |
| `SaveEvents` | `NativeQueue<SaveEventPayload>` | `ISaveEventListener` | `RaiseSaveStarted`, `RaiseSaveCompleted`, `RaiseSaveFailed`, `RaiseLoadStarted`, `RaiseLoadCompleted`, `RaiseLoadFailed`, `RaiseEmergencyBackupRestoreRequested` | `SystemDispatcher.LateUpdate()` | Uses `FixedString` payload fields for slot and failure text. |
| `QuestEvents` | `NativeQueue<QuestEventPayload>` | `IQuestEventListener` | `RaiseActivated`, `RaiseCompleted`, `RaiseFailed`, `RaiseRevertRequested` | `SystemDispatcher.LateUpdate()` | `FlushPending()` also forces `QuestGraphEvaluator.FlushPendingSignals()`. |
| `AudioLogEvents` | `NativeQueue<AudioLogEventPayload>` | `IAudioLogEventListener` | `RaiseLogDiscovered`, `RaisePlaybackStarted`, `RaisePlaybackStopped`, `RaisePlaybackCompleted` | `SystemDispatcher.LateUpdate()` | Audio-log lane only. |
| `AtlasSignalEvents` | `NativeQueue<AtlasSignalEventPayload>` | `IAtlasSignalEventListener` | `RaisePulse`, `RaiseDetected`, `RaiseStrengthChanged`, `RaiseDecoded` | `SystemDispatcher.LateUpdate()` | Holds decoded Atlas signal message IDs through a local lookup table. |
| `NotificationEvents` | `NativeQueue<NotificationEventPayload>` | `INotificationEventListener` | `PushInfo`, `PushWarning`, `PushCritical` | `SystemDispatcher.LateUpdate()` | UI notification lane. |
| `SceneBootstrap` | `NativeQueue<SceneBootstrapEventPayload>` | `ISceneBootstrapEventListener` | private `RaiseGameReadyEvent`, private `RaiseBootstrapFailedEvent` | `SystemDispatcher.LateUpdate()` via `FlushPendingEvents()` | Queue producer is internal to bootstrap orchestration. |
| `Atlas6Events` | `NativeQueue<Atlas6EventPayload>` | `IAtlas6EventListener` | `RaisePlayerStatusChanged`, `RaiseDirectiveConflict`, `RaiseBarterAccepted`, `RaiseScarcityDirective` | `SystemDispatcher.LateUpdate()` | Declared inside `Atlas6DirectiveSystem.cs`; separate lane from `AtlasSignalEvents`. |

## Managed Bus Kept Separate

`Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs`

- publish surface: `Publish<TEvent>(TEvent evt)`
- delivery model: immediate managed dispatch
- internal shape: managed subscription dictionaries/lists
- queue lane: none
- ownership role: mod/API event surface, not first-party `NativeQueue` infrastructure

## Dependency Graph

`runtime producers`
-> `lane-specific Raise* / Push* methods`
-> `static NativeQueue payload buffer`
-> `SystemDispatcher.LateUpdate()`
-> `lane FlushPending()`
-> `listener bucket dispatch`

Side branch:

`mod producers`
-> `HectonEventBus.Publish<TEvent>()`
-> `managed subscription list walk`

## Architectural Findings

- There is no single queue-backed monolithic bus.
- The project uses multiple lane-specific buses with explicit listener contracts.
- `HectonEventBus` and the first-party static queue buses should not be merged conceptually in docs.
- `Atlas6Events` being nested inside `Atlas6DirectiveSystem.cs` increases discoverability risk; the lane exists, but the ownership surface is hidden in a gameplay file rather than a dedicated event file.

## Regression Model

- CPU: none added by this document.
- GC: none added by this document.
- Memory: no runtime change.
- Cadence: documentation drift risk is high if new queue lanes are added without updating this matrix and `PROJECT_ATLAS.md`.
- Correctness: low risk on queue ownership claims because every lane listed here was verified from source files and dispatcher flush order.

## Hot Path Impact

- none from the documentation change itself
- the actual runtime hot-path sensitivity remains in `SystemDispatcher.LateUpdate()` because every queue flush is serialized there

## Failure Modes

- new event lanes can be added outside this document and make it stale
- listener implementations were not exhaustively mapped here; this matrix covers queue ownership and dispatch lanes, not every subscriber class
- `HectonEventBus` may be incorrectly treated as queue-backed by future agents unless this scope note is preserved

## Why Kept

- the file separates verified queue lanes from managed mod bus traffic
- the dispatch order is anchored to `SystemDispatcher.LateUpdate()`
- no inferred listener graph was invented beyond what source ownership supports
