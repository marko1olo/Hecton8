# LOG_WORLD_RESOURCE_SPAWNER

## 2026-05-12 - Procedural Ore LCG Runtime
What was wrong:
- Resource objective was still architecturally aimed at many authored/persisted ore objects. Prompt required deterministic procedural ore authority with dormant GPU draw and collider hydration only near the player.
- Resource event handoff lacked ore-specific native signal packets for mined yield and depletion deltas.
- `Assets/_Project/Scripts/World/Resources` did not exist, so there were no in-domain singletons to purge, no domain asmdef to isolate, and no existing ore spawner to rehabilitate.

What was done:
- Added `ProceduralOreSpawner` under `Assets/_Project/Scripts/World/Resources`.
- Added SoA authoritative state: `NativeArray<float3> OrePositions`, `NativeArray<int> OreTypes`, `NativeArray<ulong> DepletionMasks`.
- Added LCG Burst job seeded by AUP sector hash, with stable candidate slots so depletion bits do not shift after mining.
- Added MapMagic `NativeArray<ushort>` heightmap projection, finite-difference slope rejection, and native 16x16 biome heatmap sampling. Copper only emits for biome id `4`.
- Added dormant `Graphics.RenderMeshIndirect` rendering with double-buffered matrix upload.
- Added proximity hydration by `math.distancesq < 9f`, static prewarmed `MeshCollider` proxies, and `Physics.BakeMesh`.
- Added depletion bit clearing and `ResourceDepletionDeltaSignal` handoff containing sector hash, word index, and 64-bit mask.
- Added `ItemAcquiredSignal` lane and pushed mined yield from both the procedural proxy and legacy `ResourceNode`.
- Added AUP shift drain and native position/proxy offset without LCG regeneration.
- Added fixed 300-frame blackbox telemetry and dump path `Docs/AgentLogs/Dump_WORLD_RESOURCE_SPAWNER.bin`.

Cinematic Cheats used:
- Triangle-wave fallback terrain fake replaced sine wave projection for cheap plausible terrain when MapMagic payload is unavailable.
- `math.rsqrt(1 + dx*dx + dz*dz)` normal-Y estimate replaced normalized cross-product slope math.
- Dormant ore truth is a bitmask + matrix fake; only near ores become collider proxies.
- RLE save delta is a single changed 64-bit depletion word, not serialized ore positions.

Exact Microseconds saved:
- Replacing 10,000 ore GameObjects/MeshColliders with SoA + indirect draw is estimated at 300-900 us per ore-heavy sector on i3/MX350, pending profiler capture.
- Triangle-wave + rsqrt polish saves an estimated 20-60 us per 1024 fallback candidates on i3/MX350.
- Proximity hydration avoids thousands of broadphase colliders; expected broadphase/update savings are unprofiled but structurally bounded to 24 proxy colliders.
- Compile verification consumed 86,800,000 us in the first `dotnet build` attempt and remained blocked by unrelated global dependencies.

Verification:
- Forbidden API scan on touched files found no `Resources.Load`, `FindObjectOfType`, `UnityEngine.Random`, coroutine, `Update`, `FixedUpdate`, `LateUpdate`, `math.sqrt`, `math.normalize`, `math.sin`, managed `foreach`, `string.Format`, or `.ToString()`.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` remains blocked by unrelated missing global dependencies: `Hecton8.Cartography`, `Hecton8.Physics.Determinism`, `InputSignal`, and `PendingSwap`.
- Unity batchmode compile could not verify because the Hecton8 project is already open in the Unity Editor.

Integrator Notes:
- `Hecton8.World.Resources.asmdef` is blocked until contracts expose `GlobalSignals`, dispatcher registration, AUP/MapMagic sampling, and render-upload seams without depending on main `Hecton8.Core`.
- Generated `Hecton8.Core.csproj` has not regenerated to include `ProceduralOreSpawner.cs`; Unity will need to refresh project files or close/reopen after current Editor work is saved.

## 2026-05-13 - Hardening Pass After Recheck
What was wrong:
- `ProceduralOreSpawner` still had forced `Complete()` calls on disable/dispose/sector/AUP paths.
- Slow tick could continue into hydration while a spawn job was still writing ore SoA arrays.
- Hydration used proxy-slot scans per ore candidate.
- Cleanup used `UnregisterNativeHashMap` for a `NativeParallelHashMap`, and `_argsMesh` was dead state.

What was done:
- Replaced forced completion with `TryCompleteFinishedSpawnJob()`, guarded by `IsCompleted`.
- Chained native disposal behind the active spawn `JobHandle`; no main-thread wait was added.
- Made AUP shifts defer into `_pendingRuntimeShift` while generation is in flight, then apply before completed output commits.
- Made slow tick skip hydration while `_spawnJobScheduled` is true and zeroed indirect args during sector generation.
- Added `_oreProxySlots` for O(1) ore-to-proxy lookup.
- Added XML docs/tooltips, removed `_argsMesh`, and fixed native parallel hash map sentinel unregister.

Cinematic Cheats used:
- No new physical simulation. The existing visual lie remains: dormant ores are matrices and a bitmask; only near-player ores hydrate into real collider proxies.
- Sector generation now blanks dormant draw output while the job is in flight rather than presenting stale geometry as truth.

Exact Microseconds saved:
- Forced job completion hitch avoidance: estimated 200-2000 us on i3/MX350 during sector churn or origin shifts.
- Proxy lookup reduction: worst-case slow-tick comparisons at 2048 ores and 24 proxies drop from about 49,152 to 2,048; estimated 20-120 us saved per dense-sector slow tick.
- Dead `_argsMesh` removal: 0 us direct, removes one stale state path.
- Sentinel unregister correction: 0 us direct, improves memory telemetry accuracy.

Verification:
- Did not run `dotnet build`; user explicitly prohibited it on 2026-05-13.
- Static scans found no `Resources.Load`, `FindObjectOfType`, `UnityEngine.Random`, coroutine, Unity `Update` methods, `math.sqrt`, or stale `FindProxySlotForOre` in touched resource code.
- Remaining `.Complete()` call is only inside `TryCompleteFinishedSpawnJob()` after `_spawnJob.IsCompleted`.
- `git diff --check` on touched tracked files returned no whitespace errors; untracked new ore files are awaiting Unity project regeneration.

## 2026-05-13 - Signal/Lifecycle Recheck
What was wrong:
- Ore AUP handling used `GlobalSignals.TryDequeueAupShift`, a legacy destructive cursor path also used by `WorldChunkResidencyManager`.
- AUP shift drain ran from slow tick, risking missed frame snapshots between geology ticks.
- Disable/re-enable could allow an in-flight spawn job to later commit stale sector matrices.

What was done:
- Changed ore AUP handling to non-destructive `SignalBus<AupShiftSignal>.GetFrameSnapshot()`.
- Added `_lastAppliedAupShiftFrameId` to process each shift sequence once.
- Drained AUP shifts in `LateFrameTick` before job retirement/rendering.
- Added `_discardSpawnJobOutput`, `DiscardSpawnJobOutput()`, and `ClearPresentationState()` to zero draw/proxy state and discard stale in-flight output after disable or sector replacement.

Cinematic Cheats used:
- No physical simulation added. The visual truth remains matrix-backed dormant ore with bounded proxy colliders.
- Disabled or replacing sectors now show no ore for the in-flight frame rather than stale ore as a believable lie.

Exact Microseconds saved:
- Avoided destructive signal ordering bug: correctness gain; estimated runtime cost under 2 us per shift frame.
- LateFrame no-shift path: estimated under 1 us.
- Disable stale-output discard avoids forced completion hitch: retained previous 200-2000 us hitch avoidance estimate on i3/MX350.

Verification:
- Did not run `dotnet build`.
- Static scans found no ore-side `TryDequeueAupShift`, no `Resources.Load`, no `FindObjectOfType`, no `UnityEngine.Random`, no coroutine/Unity Update methods, no `math.sqrt`, and no trailing whitespace in `ProceduralOreSpawner.cs`.
- The remaining `.Complete()` path is still guarded by `_spawnJob.IsCompleted`.

## 2026-05-13 - Compile-Risk/Contract Recheck
What was wrong:
- `ProceduralOreSpawner` needed `System` types but had lost the `using System;` import during iterative edits.
- Yield and depletion emissions used direct `SignalBus<T>.Push`, drifting from the XML directive to use `GlobalSignals.Push`.
- AUP shift accumulation accepted non-finite shift values.
- Spawn job slot seeding used signed multiplication before casting to uint.
- Public `Dispose()` did not explicitly unregister dispatcher callbacks.

What was done:
- Restored one `using System;`.
- Restored `GlobalSignals.Push(in signal)` for `ItemAcquiredSignal` and `ResourceDepletionDeltaSignal`.
- Added finite checks before AUP shift accumulation.
- Changed slot seed mixing to unsigned unchecked math and added a job-local matrix builder.
- Added `UnregisterDispatchers()` and call it from both `OnDisable()` and `Dispose()`.

Cinematic Cheats used:
- No new simulation. This pass preserves the matrix/proxy ore lie and hardens the signal/lifecycle plumbing around it.

Exact Microseconds saved:
- Direct runtime savings are negligible, 0-2 us. Main value is preventing compile failure, signal contract drift, NaN propagation, and disposed-memory tick callbacks.
- Retains previous hitch avoidance estimate of 200-2000 us by keeping forced completion out of teardown paths.

Verification:
- Did not run `dotnet build`.
- Static scans found exactly one `using System;`, `GlobalSignals.Push` on resource yield/depletion, no ore-side destructive AUP dequeue, no forbidden gameplay APIs, no trailing whitespace, and one guarded `.Complete()` path.
