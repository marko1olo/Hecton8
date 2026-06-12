# SIGNAL_PARALLEL_WRITER_BUDGET_CLOSURE_X_001

Agent: X_001  
Domain: Echelon 1 Core Infrastructure / Typed SignalBus Corridor  
Date: 2026-05-24  
Status: CODE APPLIED / BUILD BLOCKED BY CPU GUARD

## Finding

The typed corridor no longer had external runtime `GlobalSignals.Publish`, `GlobalSignals.Push`, `GlobalSignals.TryDequeue`, or `GlobalSignals.*Writer` traffic, but a real storm weakness remained: job producers using `SignalBus<T>.ParallelWriter` could enqueue directly into `NativeQueue<T>` without a producer-side capacity claim. Main-thread `TryPush` was already bounded before enqueue. Job writers were only bounded at flush/backpressure, so a 5000-signal burst from parallel jobs could still pressure native queue blocks before the deterministic drop ledger saw it.

## Code Change

`Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs` now owns a per-closed-generic native writer budget:

- `NativeArray<int>[2]` per `SignalBus<T>` lane, allocated once during lane initialization.
- Slot 0: remaining job-writer enqueue budget for the current frame.
- Slot 1: job-writer drops accumulated before enqueue.
- Budget resolves as `max(1, min(expectedCapacity, LaneOverflowFaultThreshold))`.
- `TryEnqueueBounded(writer, budget, signal)` atomically decrements slot 0 before `NativeQueue<T>.ParallelWriter.Enqueue`.
- If the atomic claim underflows, the signal is dropped before native queue enqueue and slot 1 is incremented.
- Flush consumes slot 1 into the lane load-shed counter and resets the budget for the next frame.

This is native memory only and has no per-frame managed allocation. It is not a Unity profiler proof; it is a source-level memory route closure.

## Migrated Surface

First-party runtime job writers now pair every `SignalBus<T>.ParallelWriter` / `OpenParallelWriter()` acquisition with `SignalBus<T>.ParallelWriterBudget` and write through `SignalBus<T>.TryEnqueueBounded(...)`.

Static counts from the current scan:

- External first-party writer acquisition sites: 57.
- External first-party writer budget acquisition sites: 57.
- External first-party `TryEnqueueBounded` call sites: 60.
- Unique first-party job-writer lane types: 47.
- Selected signal-writer raw `.Enqueue(...)` hits after migration: 0.

## Writer Lane Types Covered

The migrated writer surface covers these first-party `SignalBus<T>` lane types:

`AuxiliaryFlareLightSignal`, `AuxiliarySonarRequestSignal`, `AuxiliaryTetherConnectionSignal`, `BaseIntegrityEventPayload`, `BaseModuleCompromisedSignal`, `BaseStructuralWarningSignal`, `BiomeChangedSignal`, `CardiacPulseSignal`, `CavitationAcousticSignal`, `CombatDamageSignal`, `DeconstructResultSignal`, `DeflectSignal`, `EquipmentOverheatSignal`, `FabricationCompletedSignal`, `FabricationTickSignal`, `FluidIncursionSignal`, `HUDNotificationSignal`, `ImpactSignal`, `InteractionUiSignal`, `InventoryCommandSignal`, `InventoryRespawnDeathAupSignal`, `ItemAcquiredSignal`, `LogisticsTransferSignal`, `MemoryDesyncSignal`, `MockPlayerPositionSignal`, `MockStoryEventSignal`, `MovementAcousticSignal`, `PhysicsEventPayload`, `PhysiologyStateSignal`, `PlayerFatalPressureSignal`, `PlayerRespawnSignal`, `ProgressionEventSignal`, `QuestDagMockItemAcquiredSignal`, `RadarJamSignal`, `RadiationSourceSignal`, `ResourceDepletionDeltaSignal`, `RollbackRequiredSignal`, `SeismicShockwaveSignal`, `SeismicSignal`, `StateChangedSignal`, `TerminalCommandSignal`, `TerminalUnlockedSignal`, `ThermalStateChangedSignal`, `ToolDepletedSignal`, `VehicleHazardSignal`, `VisualScavengeSignal`, `WakeGeneratedSignal`.

## Storm Behavior

For a 5000-signal burst:

- Main-thread producers use `TryPush`, which rejects before enqueue once `_queue.Count >= _expectedCapacity`.
- Parallel job producers use `TryEnqueueBounded`, which rejects before enqueue once the per-lane native writer budget is exhausted.
- `CombatDamageSignal` retains the existing deterministic coalescing path by target/damage/channel identity.
- `AcousticPingSignal` retains deterministic coalescing by channel and AUP meter cell.
- `ImpactSignal` coalesces by AUP meter cell plus primary body/material alias identity and preserves max force/intensity/weight plus OR flags.
- `HighSpeedImpactSignal` coalesces by AUP meter cell plus source/target/material identity and preserves the highest-energy sample.
- Non-coalesced overflow is deterministic load shedding recorded on lane counters.

No string, `GameObject`, `Transform`, `NativeArray`, `NativeQueue`, `NativeList`, `NativeHashMap`, or `NativeParallelHashMap` fields are present in the Core signal DTO scan used for this closure.

## Scans

Commands run from `C:\hades\Hecton8`:

```powershell
rg -n "SignalBus<[^>]+>\.Push" Assets/_Project/Scripts -g "*.cs"
```

Result: no hits.

```powershell
rg -n "GlobalSignals\.(Publish|Push|TryDequeue|[A-Za-z0-9_]+Writer|TryGetLatest|Current|RuntimePosition|FoldEntity)" Assets/_Project/Scripts -g "*.cs" | rg -v "Core[\\/]Signals|Editor|Tests"
```

Result: no hits.

```powershell
rg -n "HectonEventBus\.(Publish|Subscribe|Unsubscribe)" Assets/_Project/Scripts -g "*.cs" | rg -v "ModdingAPI|Editor|Tests"
```

Result: no hits.

```powershell
rg -n "(?:public|private|internal)\s+(?:readonly\s+)?(?:GameObject|Transform|string|FixedString\d+Bytes|NativeArray<|NativeQueue<|NativeList<|NativeHashMap<|NativeParallelHashMap<)\s+[A-Za-z_]" Assets/_Project/Scripts/Core/Signals Assets/_Project/Scripts/Core/Contracts/Signals -g "*.cs"
```

Result: no hits.

```powershell
rg -n "\b(DeflectSignalWriter|ImpactSignalWriter|DamageWriter|AcousticWriter|SeismicWriter|ShockwaveWriter|TransferSignalWriter|RollbackSignals|PhysicsEventWriter|BiomeChangedWriter|BaseModuleWriter|RadiationWriter|ThermalWriter|Commands|UiSignals|UnlockedSignals|ItemWriter|VisualWriter|DepletionWriter|HudWriter|ProgressionWriter|WarningSignals|IntegrityEvents|FluidEvents|CompromisedEvents|IncursionWriter|WakeWriter|HazardWriter|CavitationWriter|PhysiologyWriter|CardiacPulseWriter|InventoryCommands|InventoryDeathAupSignals|FatalPressureSignals|RespawnSignals|PlayerPositionWriter|ItemAcquiredWriter|StoryEventWriter|StateChangedWriter|RadarJamWriter|OverheatWriter|DepletedWriter|FlareWriter|SonarWriter|TetherWriter|DesyncWriter)\.Enqueue\(" Assets/_Project/Scripts -g "*.cs" | rg -v "ModdingAPI|Editor|Tests"
```

Result: no hits.

## Compile Guard

Build was not launched for this closure. Latest guard check:

- CPU: 100 percent.
- Compiler processes: 0.

CPU alone violates the `AGENTS.md` build threshold of 50 percent.

