# SIGNAL_MAIN_THREAD_TRYPUSH_REFUSAL_TRACKING_X_001

Agent: X_001  
Date: 2026-05-25  
Domain: Echelon 1 Core Infrastructure / Typed SignalBus Corridor  
Evidence class: SOURCE_ONLY, pending Unity import/profiler/GCMonitor.

## Problem

`SignalBus<T>.TryPush(...)` was already bounded and lane-telemetered, but many main-thread producers still discarded the returned bool. Under a 5000-event burst the lane would shed deterministically, but several owner systems would lose local proof that their own presentation/gameplay-adjacent signal was refused.

The obsolete `GlobalSignals.Publish/Push` facade was compile-time banned for new code, but its internal compatibility delegates still ignored the typed lane refusal result.

## Changes

- Added `SignalBus<T>.TryPushTracked(in T signal, ref int ownerDroppedSignalCount)`.
- Patched 22 owner files to store a caller-owned drop counter and use `TryPushTracked` for main-thread signal pushes.
- Patched `DroneFleetManager` owner-local heap insertion fallout so native heap refusal increments the same local counter without pretending the heap is a SignalBus lane.
- Added `SignalBridgeState.LegacyPublishDropCount` and `RecordLegacyPublishDrop()`.
- Routed obsolete `GlobalSignals.LegacyFacade` publishes through `TryPushLegacy`, so compatibility facade drops are counted by bridge state instead of silently disappearing.

## Touched Code Files

- `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs`
- `Assets/_Project/Scripts/Core/Signals/SignalBridgeState.cs`
- `Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs`
- `Assets/_Project/Scripts/HectonPlayerMovement.cs`
- `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs`
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`
- `Assets/_Project/Scripts/Construction/DroneFleetManager.cs`
- `Assets/_Project/Scripts/PlayerInventory.cs`
- `Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs`
- `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs`
- `Assets/_Project/Scripts/ConstructionManager.cs`
- `Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs`
- `Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs`
- `Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs`
- `Assets/_Project/Scripts/Tools/ToolKinematics/ToolKinematicsRuntime.cs`
- `Assets/_Project/Scripts/SaveManager.cs`
- `Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs`
- `Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs`
- `Assets/_Project/Scripts/World/EcosystemDirector.cs`
- `Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs`
- `Assets/_Project/Scripts/World/WorldChunkResidencyManager.cs`
- `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs`
- `Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs`
- `Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs`
- `Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs`

## Static Proof

- Changed code files: 25.
- `SignalBus<T>.TryPushTracked(...)` call sites: 177.
- `GlobalSignals.LegacyFacade` `TryPushLegacy(...)` refs: 116 total, including the helper declaration.
- Changed-file direct `.TryPush(...)` refs: 2, limited to the `SignalBus<T>.Push` compatibility wrapper and `TryPushLegacy` helper body.
- Changed-file brace delta: 0.
- Bad `TryPushTracked` receiver scan: 0. Every tracked call targets `SignalBus<T>`.
- Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer`, `SignalBus<T>.Push`, first-party `HectonEventBus.Publish/Subscribe/Unsubscribe`, and `ThreadSafeCommandQueue.Enqueue` scans outside allowed zones: 0.
- Scoped core signal payload/contract banned-field scan: 0.
- `git diff --check`: no whitespace errors; LF-to-CRLF warnings only.

## Overflow Behavior

This pass does not raise capacities and does not add managed fallback state.

`TryPushTracked` delegates to the existing `SignalBus<T>.TryPush` policy:

- finite guard failure: reject and increment lane corruption telemetry;
- non-critical VFX under high system stress: reject and increment lane shed telemetry;
- queue count at or above expected capacity: reject and increment lane shed/drop counters;
- accepted payload: enqueue into existing native lane, update latest payload, increment accepted counter.

The new owner counter is an `int` field in the owner class or static owner. It is incremented only on rejected push and uses no heap allocation, no delegate, no string, and no sidecar container.

## Build Status

Build not launched. Guard reported `CPU=99 compiler_count=0`; AGENTS blocks `dotnet build` above 50 percent CPU.

Runtime microseconds saved: 0us verified. No Unity profiler, GCMonitor, Play Mode, or player build artifact was produced.
