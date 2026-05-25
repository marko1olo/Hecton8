# SHINOBU_356 Core Memory Dispatcher Fence Blocker Route Card

Date: 2026-05-23
Status: BLOCKED_BY_CORE_MEMORY_OWNER
Evidence class: STATIC_SOURCE / UNITY_IMPORT_BLOCKER

## Authority Boundary

SHINOBU_356 owns offline Echelon 9 QA for the Power Grid Jacobi stress fuzzer.
The blocking file is Echelon 1 Core/Memory:

- `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`
- `Assets/_Project/Scripts/Core/Memory/Hecton8.Core.Memory.asmdef`
- `Assets/_Project/Scripts/Hecton8.Core.asmdef`
- `Assets/_Project/Scripts/Core/DispatcherJobFence.cs`

SHINOBU_356 does not edit the Core/Memory source in this route card. This card
records the smallest legal fix for the Integrator or Core/Memory owner.

## Problem

Unity import for `PowerGridJacobiStressFuzzerEditTests` stops before the
SHINOBU_356 assemblies compile because `H8Memory.cs` calls:

- `Hecton8.Core.DispatcherJobFence.TryComplete(ref ownerHandle, forceComplete: true)` at line 2986
- `Hecton8.Core.DispatcherJobFence.TryComplete(ref ownerHandle, forceComplete: true)` at line 3003

`H8Memory.cs` compiles inside `Hecton8.Core.Memory`. That asmdef references
only `Hecton8.Core.Contracts` plus Unity Burst/Collections/Jobs/Mathematics.
`Hecton8.Core.asmdef` already references `Hecton8.Core.Memory`, so adding
`Hecton8.Core` back into Core.Memory would create a dependency cycle.

## Route Disposition

Route ID: `CORE_MEMORY_FORCED_OWNER_JOB_TEARDOWN`
Owner: Core/Memory owner or Integrator
Owner domain: Echelon 1 Core and Memory Infrastructure
Owning file/system: `H8Memory.cs` owner job teardown
Status: PROPOSED_FOR_CORE_OWNER / BLOCKING_SHINOBU_356_UNITY_IMPORT

Instrument:

- No new `GlobalRegistry` route.
- No new `SignalBus<T>` route.
- No new `GlobalDataVault` route.
- No new managed event route.

Producer/consumer phase:

- Cold scene transition, owner teardown, and shutdown only.
- Existing call sites are already annotated `[BLOCKING_SYNC_POINT]`.
- This route must not be used in gameplay Tick, FixedTick, LateUpdate, or render/audio jobs.

Accessor purity:

- No read accessor should publish signals.
- No read accessor should allocate or grow buffers.
- No read accessor should complete jobs.
- No scene search is involved.

## Smallest Legal Fix

Replace the two upward `Hecton8.Core.DispatcherJobFence` calls with a
Core.Memory-local forced-teardown helper or direct forced teardown:

```csharp
ownerHandle.Complete();
ownerHandle = default;
```

Reason:

- both current calls pass `forceComplete: true`;
- return value is not consumed;
- `DispatcherJobFence.TryComplete` forced path completes and clears handle;
- swap-window warning applies only to non-forced editor/development calls.

## Rejected Alternatives

- Add `Hecton8.Core` to `Hecton8.Core.Memory.asmdef`: rejected because
  `Hecton8.Core` already references `Hecton8.Core.Memory`.
- Move `DispatcherJobFence` into Contracts: rejected for this blocker because
  it is mutable runtime state, not a pure DTO or interface contract.
- Route through `Hecton8.Core.Scheduling`: rejected because Scheduling already
  depends on Core.Memory in the current graph.
- Route through `World.DispatcherJobSwap`: rejected because Core/Memory cannot
  depend upward into World.
- Patch Core/Memory under SHINOBU_356: rejected because SHINOBU_356 is an
  Echelon 9 QA fuzzer agent and the shared Core/Memory file has separate
  ownership and unrelated working-tree churn.

## Proof Required Before GREEN

- Unity import reaches SHINOBU_356 editor/test assemblies after the Core/Memory
  fix.
- `PowerGridJacobiStressFuzzerEditTests` runs through Unity EditMode without
  this dependency error.
- No new asmdef cycle is introduced.
- No new `JobHandle.Complete()` appears in gameplay Tick/FixedTick/LateUpdate
  paths; the forced completion remains cold teardown only.
