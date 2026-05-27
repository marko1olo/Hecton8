# Unity Voxel Request Allocation And Queue Traps - UNKNOWN - 2026-05-26

Status: source fixed, compile reclosed.

## Verdict

This report now covers three narrow first-party cave/voxel request
traps.

First, three first-party cave/voxel request owners used per-request
`CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token)` even though
each owner already cancels pending requests explicitly on stale request,
disable, and destroy paths.

This was a real but narrow allocation/lifetime trap:

- early cancellation of heavy voxel generation is still needed;
- a per-request `CancellationTokenSource` is still kept;
- the linked-token layer was redundant in these owners;
- gameplay truth, AUP, DTO layout, DataVault ownership, save identity, and
  authority routes were not changed.

Second, `WorldGenerativeGeologyVoxelBridgeDirector.BuildVoxelRequestData`
created a new `CavePresetLibrary.Create(CavePresetType.Grotto)` fallback per
request when `voxelEngine.defaultPreset` was missing. `CavePreset` is a class
and initializes managed fields including `allowedStructureTypes`, so this was
a real fallback allocation route. The fallback is now owner-cached once.

Third, `WorldGenerativeGeologyVoxelBridgeDirector.FlushQueuedLaunches` used
`_queuedLaunchOrder.RemoveAt(0)` while draining launches from a Tick-driven
queue. This was not a GC allocation claim, but it was a real O(n) hot-path
copy/shift trap. The dequeue route now advances a head index, skips cancelled
stale keys through `_queuedLaunchKeys`, and compacts the physical list only on
enqueue/slow cleanup thresholds.

## Sources Checked

- Unity `Awaitable`: https://docs.unity.cn/6000.0/Documentation/ScriptReference/Awaitable.html
- Unity Awaitable manual: https://docs.unity.cn/6000.0/Documentation/Manual/AwaitSupport.html
- Unity Awaitable continuations: https://docs.unity.cn/6000.0/Documentation/Manual/async-awaitable-continuations.html
- Unity `MonoBehaviour.destroyCancellationToken`: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour-destroyCancellationToken.html
- Microsoft `CancellationTokenSource.CreateLinkedTokenSource`: https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtokensource.createlinkedtokensource
- Microsoft `List<T>.RemoveAt`: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1.removeat

Relevant boundary:

- Unity `Awaitable` is the correct Unity-facing async return route.
- Unity APIs remain main-thread constrained unless explicitly on safe background
  work.
- `destroyCancellationToken` cancels on MonoBehaviour destruction, but Unity
  documents that it must be cached before destruction.
- `CreateLinkedTokenSource` creates another CTS linked to source tokens. It is
  useful when a request must listen to multiple independent cancellation owners.
  These three owners already have explicit pending-request cancellation paths,
  so the extra link was not carrying unique authority.
- `List<T>.RemoveAt(index)` moves following elements up to close the gap. At
  index `0`, that means the whole remaining launch order is shifted during the
  dequeue loop.

## Local Static Scan

Targeted scan:

```powershell
rg -n "CreateLinkedTokenSource\(lifetime\.Token\)|CreatePendingRequestState|CreatePendingSpawnState|EnsureLifetimeCancellation" Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs Assets/_Project/Scripts/WorldCaveDirector.cs
```

Current result:

- `CreateLinkedTokenSource(lifetime.Token)` no longer appears in the three
  patched owner files.
- `CreatePendingRequestState` / `CreatePendingSpawnState` now create a direct
  per-request `CancellationTokenSource`.
- Lifetime CTS remains owner-local and manually cancelled/disposed.

Global first-party residual scan:

```powershell
rg -n "CreateLinkedTokenSource\(" Assets/_Project/Scripts -g '*.cs'
```

Residual hits remain in dirty files not touched in this pass:

- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
- `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs`

Those files were already modified by other agents, so this pass did not edit
them.

Queue dequeue scan:

```powershell
rg -n "RemoveAt\(0\)|_queuedLaunchOrder\.Remove|_queuedLaunchOrder\.Count|_debugQueuedLaunches = _queuedLaunchOrder" Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs
```

Current result:

- no `RemoveAt(0)` remains in `WorldGenerativeGeologyVoxelBridgeDirector`;
- `_debugQueuedLaunches` and reconcile telemetry now use
  `_queuedLaunchKeys.Count`, not the physical backing list count;
- cancelled queued launches leave stale physical keys that are skipped by
  `TryDequeueQueuedLaunchKey`, avoiding `List.Remove(runtimeKey)` in the
  cancellation route;
- one `RemoveRange(0, _queuedLaunchHeadIndex)` remains in
  `CompactQueuedLaunchOrderIfNeeded`; it is a bounded compaction path called
  during queue admission/cleanup, not every dequeue iteration.

## Source Change

Changed files:

- `Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs`
- `Assets/_Project/Scripts/WorldCaveDirector.cs`

Removed:

- per-request `CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token)`;
- one redundant lifetime-to-destroy linked CTS in `HectonVoxelStreamingBridge`.
- per-request fallback `CavePresetLibrary.Create(CavePresetType.Grotto)` in
  `WorldGenerativeGeologyVoxelBridgeDirector`.
- `List.RemoveAt(0)` dequeue from
  `WorldGenerativeGeologyVoxelBridgeDirector.FlushQueuedLaunches`.

Kept:

- one CTS per heavy pending voxel/cave request;
- explicit stale/disable/destroy cancellation;
- async generation cancellation token handoff to `GenerateVolumeAsync`;
- owner dictionary identity checks before pending removal.
- authored `voxelEngine.defaultPreset` as the primary route;
- one owner-cached fallback grotto preset for missing-defaultPreset cases.
- FIFO launch order semantics through `_queuedLaunchHeadIndex`;
- queued active count through `_queuedLaunchKeys.Count`.

Rejected:

- deleting request cancellation entirely, because stale heavy voxel generation
  must still be interruptible;
- editing dirty `GameBootstrapper.cs` / `PrologueSequenceRegistryBridge.cs`;
- rewriting the request state classes into structs in this pass, because CTS
  lifetime/disposal race risk needs a separate focused proof.
- editing dirty `HectonVoxelEngine.cs`, where a separate fallback preset factory
  call remains under active cross-agent edits.
- using `NativeQueue<T>` for this managed owner queue in this pass, because the
  payload remains managed dictionaries/request states and the minimal risk fix
  was removing the O(n) front-removal trap without changing ownership.

## Validation

Static:

- scoped scan shows no `CreateLinkedTokenSource(lifetime.Token)` in the three
  patched cave/voxel owner files;
- scoped scan shows no `RemoveAt(0)` in
  `WorldGenerativeGeologyVoxelBridgeDirector`;
- global residual linked-CTS scan is documented above;
- scoped `git diff --check` passed with line-ending warnings only.

Build:

- guarded full-solution CLI build:
  `Docs/Reports/BUILD_UNKNOWN_VOXEL_QUEUE_DEQUEUE_RECHECK_20260526.log`;
- guard samples blocked earlier launches while CPU/compiler state was illegal;
- final guard sample before launch: CPU `45.5%`, compiler processes `0`;
- command:
  `dotnet build .\Hecton8.slnx -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false`;
- exit `0`;
- `Build succeeded.`;
- `0 Warning(s)`;
- `0 Error(s)`.

Runtime proof:

- Not claimed.
- No Unity Editor import, Console, PlayMode, player build, profiler,
  GCMonitor, shader-variant, scene wiring, visual, or platform gate was run.

Documentation gates:

- `Tools/VerifyDocStructure.py`: `pass=true`, `activeDocCount=699`,
  `encodingWithoutUtf8Sig=0`;
- `Tools/OOP_Doc_Scanner.py`: `finalPass=true`, `activeFileCount=699`,
  `sourceSyncPass=true`.

## Residual

- The three request paths still allocate one CTS per launched heavy request.
  That is intentional until profiler/runtime proof says stale request
  cancellation can be coalesced or pooled safely.
- `HectonVoxelEngine.cs` still contains a separate
  `CavePresetLibrary.Create(CavePresetType.Grotto)` fallback, but that file was
  dirty before this pass and was not touched.
- `GameBootstrapper.cs` and `PrologueSequenceRegistryBridge.cs` still contain
  linked CTS routes, but they were dirty cross-agent surfaces during this pass.
- The queue still uses managed `List`/`Dictionary` containers because this
  director owns managed async request state. This pass removed the pathological
  front-removal copy path; it did not convert the whole owner to native storage.
- No measured frame-time or GC allocation delta is claimed.

## Hardware Impact

Measured microseconds saved: `0`.

Expected static benefit:

- removes one linked CTS layer/registration from each launched voxel/cave
  request in the patched owner paths;
- removes one fallback `CavePreset` class allocation from each geology voxel
  request when the voxel engine default preset is missing;
- removes O(n) `List.RemoveAt(0)` element shifting from each queued geology
  launch dequeue;
- preserves early cancellation for stale heavy generation;
- avoids touching shared bootstrap/prologue files with active unrelated edits.

Low tier: less request-side managed cancellation overhead during cave/voxel
streaming churn.
Middle tier: same generation cadence and fidelity with simpler cancellation
ownership.
High tier: same visual overkill budget; less request bookkeeping overhead.
Ultra tier: no visual cap removed; this is lifetime/allocation hygiene.
