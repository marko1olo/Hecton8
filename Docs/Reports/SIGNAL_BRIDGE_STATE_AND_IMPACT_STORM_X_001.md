# X_001 Bridge State And Impact Storm Report

Date: 2026-05-24
Agent: X_001
Domain: Echelon 1 Core Infrastructure / Typed SignalBus Corridor

## Scope

This pass targeted the remaining central-state and storm-lane weaknesses after external runtime `GlobalSignals` calls were removed:

- pause/time/bullet-time/crafting/survival-death bridge state still owned by `GlobalSignals`
- `ImpactSignal` and `HighSpeedImpactSignal` lacking coalescing
- domain `SignalBus<T>.Configure` sites missing immediate prewarm
- storm producers still hiding bounded/drop semantics behind `SignalBus<T>.Push`

## Code Changes

- Added `Assets/_Project/Scripts/Core/Signals/SignalBridgeState.cs`.
- Updated `SignalBridgeRoutes.cs` to record/read bridge state through `SignalBridgeState` and initialize through `SignalCorridorRuntime`.
- Updated `GlobalSignals.State.cs`, `GlobalSignals.RuntimeLifecycle.cs`, and `GlobalSignals.LegacyFacade.cs` so `GlobalSignals` is a compatibility delegate, not the bridge-state owner.
- Marked 16 central latest/bridge read facades `[Obsolete(..., true)]`.
- Updated `SignalBusRuntime.cs` with allocation-free coalescing for `ImpactSignal` and `HighSpeedImpactSignal`.
- Patched all 13 direct domain `SignalBus<T>.Configure` sites that lacked immediate `EnsureInitialized`.
- Converted remaining impact/high-speed-impact/combat-damage/acoustic/deferred-submarine-impact storm producers from `SignalBus<T>.Push` wrappers to direct `TryPush`.
- Removed the string-taking first-party session lifecycle route. `ModLoader` now computes a FNV-1a slot hash before calling `SessionLifecycleSignalRoute.PublishGameLoadedHash(uint)`.

## Impact Coalescing Policy

`ImpactSignal` coalesces only when all identity keys match:

- AUP meter cell from `Point`
- `PrimaryBodyId` / `MaterialHash` alias value

Merged values:

- max `Force` / `Velocity` alias scalar
- max `Intensity` / `Mass` alias scalar
- max `WeightClass`
- OR `Flags`
- existing material/body identity retained

`HighSpeedImpactSignal` coalesces only when all identity keys match:

- AUP meter cell from `WorldPoint`
- `SourceHash`
- `TargetHash`
- `MaterialHash`

Merged values:

- max `KineticEnergy` / `LostKineticEnergy` alias scalar
- highest-energy sample keeps `WorldPoint`, `Normal`, `Speed`, `Frame`, and material ids
- max `EffectiveMass`
- OR `Flags`

Both paths mutate only existing `NativeArray<T>` frame snapshot entries. No managed collection, string, `GameObject`, `Transform`, or heap allocation is used.

## Capacity And Prewarm Snapshot

Latest static ledger:

- runtime configure/prewarm records: 329
- unique typed lanes: 277
- legacy typed prewarm registrations: 74
- cache-critical lanes: 4
- Core lifecycle registrations: 221
- domain-local registrations: 108
- direct `SignalBus<T>.Configure` sites missing immediate `EnsureInitialized`: 0

Storm route snapshot:

- storm-lane `Push` wrappers outside `GlobalSignals.LegacyFacade.cs`: 0
- typed `TryPush` call sites for impact/high-speed-impact/combat-damage/acoustic/deferred-submarine-impact lanes: 74

## Hidden Route Proof

Source scans after this pass:

- external runtime `GlobalSignals.` outside `Core/Signals`: 0
- runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer/latest bridge read` outside `Core/Signals`, Editor, and Tests: 0
- first-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests: 0
- `SignalBridgeRoutes.cs` direct `GlobalSignals.` references: 0
- DTO/route banned fields in Core payload/route/runtime-origin/bridge-state files: 0

Remaining `GlobalSignals.` references are compatibility delegates inside `Assets/_Project/Scripts/Core/Signals`.

## 5000-Signal Burst Model

A 5000 impact burst cannot grow managed memory through this route:

1. producers call `TryPush`
2. payload guards run on value structs
3. lane capacity and per-frame caps are fixed
4. same-cell impact facts are merged into existing native frame snapshot entries
5. non-mergeable overflow is handled by the existing deterministic `SignalBus<T>` drop/fault policy

Runtime profiler proof is not claimed. This report is static source proof plus deterministic policy proof.

## Build Status

Build was not launched after this pass. Guarded checks reported CPU 53 percent, then 90 percent after a 20-second wait; no active `dotnet/csc/VBCSCompiler` process was present in the last process check, but CPU alone violates the `AGENTS.md` build threshold.
