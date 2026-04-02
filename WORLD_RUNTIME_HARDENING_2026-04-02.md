# World Runtime Hardening - 2026-04-02

## What Was Fixed

### 1. Safe reinitialization of `ProximityColliderSystem`

File:
- `Assets/_Project/Scripts/ProximityColliderSystem.cs`

Problem:
- Repeated `Initialize(...)` previously dropped straight into cleanup.
- If an active job was still alive, internal native buffers could be released too early.
- Active collider proxies also were not guaranteed to return to the pool first.
- An empty point set was treated like an error instead of a valid runtime-clear scenario.

Resolution:
- Added the explicit public contract `ClearRuntimeData()`.
- Reinitialization now follows the safe order:
  - `CompleteCurrentJob()`
  - `DespawnAllColliders()`
  - `Cleanup()`
- Both `Initialize(Vector3[], int)` and `Initialize(NativeArray<float3>)` now treat an empty point set as a valid runtime clear.

What this gives:
- no job-buffer release while a job is still active
- no ghost collider proxies after runtime world changes
- a proper production runtime clear/rebuild path

### 2. Correct rock-runtime clearing when no rock points remain

File:
- `Assets/_Project/Scripts/HectonRockManager.cs`

Problem:
- When no live rock points were found, `HectonRockManager` called:
  - `proximityColliderSystem.Initialize(Array.Empty<Vector3>())`
- That created noisy behavior and depended on an error-like path instead of a real runtime clear.

Resolution:
- Empty rock passes now call:
  - `proximityColliderSystem.ClearRuntimeData()`
- The aggregated-point diagnostic state is reset together with runtime physics cleanup.

What this gives:
- clean rock-physics shutdown when the rock layer is temporarily or fully unloaded
- deterministic world-streaming behavior without stale colliders

## Verification

- Unity rebuilt after the fix.
- These two files introduced no new compile errors.
- The runtime contract between `HectonRockManager` and `ProximityColliderSystem` is now aligned with real streaming behavior.

## What Is No Longer Relevant

- The old note about `FlashlightTool` compile blockers is obsolete.
- That blocker was closed later in a separate compile/runtime pass and is no longer part of the active risk for this world-runtime fix.

## What Still Remains

- The remaining follow-up for this area is no longer compile repair.
- The real next step is broader world/runtime smoke verification on a stable play-mode session.
