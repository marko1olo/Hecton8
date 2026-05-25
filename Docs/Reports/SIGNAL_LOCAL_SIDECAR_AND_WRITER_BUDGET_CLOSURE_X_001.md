# SIGNAL_LOCAL_SIDECAR_AND_WRITER_BUDGET_CLOSURE_X_001

Agent: X_001
Domain: Echelon 1 Core Infrastructure / Typed SignalBus Corridor
Date: 2026-05-24
Status: CODE APPLIED / BUILD BLOCKED BY CPU GUARD

## Finding

The main typed corridor was already free of external runtime `GlobalSignals.Publish`, `GlobalSignals.Push`, `GlobalSignals.TryDequeue`, `GlobalSignals.*Writer`, `SignalBus<T>.Push`, and first-party managed `HectonEventBus` hot traffic. The remaining defects were narrower:

- several domain-owned `SignalBus<T>.Configure(...)` sites needed direct local prewarm proof;
- `ShinobuApexBrainJob` had raw job-side `NativeQueue<T>.ParallelWriter.Enqueue(...)` calls outside `SignalBus<T>.TryEnqueueBounded(...)`;
- four first-party event facades kept unbounded `Dictionary<uint,string>` sidecars for hash-to-string resolution;
- narrative/order consumers could still grow managed string identity collections without hard caps;
- organic drop and KCC mock-input job queues could enqueue from jobs without a native producer-side budget;
- the retired gas toxicity writer still carried a dormant raw writer field and needed a hard no-enqueue path;
- pool diagnostics used `Dictionary<uint,string>` for event-payload name resolution.

## Code Change

Runtime files touched in this closure: 20.

- `ToxicOutgassingChemistryRuntime`, `HectonSeismicTideDirector`, `ModularEquipmentEngine`, `ExosuitKinematicsRuntime`, `SubmarineDynamicsRuntime`, `PlayerFlashlight`, and `TerrainChunkGeneratedEvents` now keep local configure/prewarm pairs with immediate `EnsureInitialized()`.
- `ShinobuApexBrainJobs` now carries `NativeArray<int>[2]` budgets for proximity, mock combat damage, and panic writers; raw enqueue calls are replaced with atomic bounded enqueue.
- `ShinobuApexBrainVault` now requires those writer budgets when attaching external writers.
- `NotificationEvents`, `AtlasSignalEvents`, `Atlas6DirectiveSystem`, and `NarrativeEvents` replaced `Dictionary<uint,string>` sidecars with fixed slot arrays.
- `HectonNarrativeDirector` clamps discovery hashes/string ids to the save capacity and reports overflow instead of growing silently.
- `CorporateOrderSystem` clamps active order/conflict identity state to `SaveData.MaxCorporateOrderIds`.
- `DropBuffer` owns a fixed native drop budget, resets it before scheduling, and passes it into `EntropyYieldJob`.
- `EntropyYieldJob` rejects organic drops before native enqueue when the budget is exhausted.
- `HydrodynamicKccRuntime` requires a native budget for mock input queue generation and rejects before enqueue.
- `GasDynamicsSolver` keeps the retired toxicity writer interface but routes it through a constant-false no-enqueue helper.
- `ObjectPoolDiagnostics` replaced the pool hash name `Dictionary<uint,string>` with a fixed 32-slot sidecar.

## Capacity And Overflow

The hot `SignalBus<T>` rule set after this closure:

- Main-thread producers use `TryPush`, which rejects before enqueue at `_expectedCapacity`.
- Job producers use `TryEnqueueBounded(writer, budget, signal)`, which consumes a lane-owned or owner-owned `NativeArray<int>[2]` budget before `NativeQueue<T>.ParallelWriter.Enqueue`.
- Budget slot 0 is remaining producer capacity; slot 1 is pre-enqueue drop count.
- `SignalBus<T>` writer budget resolves to `max(1, min(expectedCapacity, LaneOverflowFaultThreshold))`.
- Owner-local queues added in this pass use their configured queue capacity as the writer budget.
- `CombatDamageSignal`, `AcousticPingSignal`, `ImpactSignal`, and `HighSpeedImpactSignal` retain deterministic coalescing below overflow.
- Non-coalesced overflow is bounded drop/load-shed. No managed dictionary/list growth is used in the hot signal payload route.

For a 5000-signal burst, the path is bounded before native enqueue for job writers and before enqueue for main-thread producers. If a lane reaches its frame snapshot cap, it either coalesces into an existing native snapshot entry or records deterministic shed/drop counters. No new managed allocation is required by this logic.

## Verification

Commands run from `C:\hades\Hecton8`:

```powershell
rg -n "GlobalSignals\.(Publish|Push|TryDequeue|[A-Za-z0-9_]+Writer|TryGetLatest|Current|RuntimePosition|FoldEntity)" Assets/_Project/Scripts -g "*.cs" | rg -v "Core[\\/]Signals|Editor|Tests"
```

Result: no hits.

```powershell
rg -n "SignalBus<[^>]+>\.Push" Assets/_Project/Scripts -g "*.cs"
```

Result: no hits.

```powershell
rg -n "HectonEventBus\.(Publish|Subscribe|Unsubscribe)" Assets/_Project/Scripts -g "*.cs" | rg -v "ModdingAPI|Editor|Tests"
```

Result: no hits.

```powershell
Select-String -LiteralPath <Core signal payload and contract files> -Pattern "\b(GameObject|Transform|string|FixedString|NativeArray|NativeQueue|NativeList|NativeHashMap)\b\s+[_A-Za-z][A-Za-z0-9_]*\s*;"
```

Result: no hits.

```powershell
rg -n "\b\w+Writer\.Enqueue\(" Assets/_Project/Scripts -g "*.cs" | rg -v "ModdingAPI|SignalBusRuntime\.cs|LegacyFacade|Tests|Editor|TryEnqueueBounded"
```

Result: no first-party runtime hits.

```powershell
SignalBus<T>.Configure immediate prewarm scanner
```

Result: `ConfigureHits=243`, `MissingImmediateEnsure=0`.

```powershell
git diff --check -- <20 touched runtime files>
```

Result: no whitespace errors; LF-to-CRLF warnings only.

## Build Guard

Build was not launched. Latest guarded check reported CPU 100 percent. The latest `dotnet/csc/VBCSCompiler` process scan returned no rows, but CPU alone violates the project threshold of 50 percent.

## Limits

No Unity runtime profiler or GCMonitor capture was run. Runtime microsecond savings are therefore not claimed. This is source-level route, capacity, and allocation-shape proof.
