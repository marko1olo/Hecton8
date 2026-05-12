# LOG_WORLD_VOXEL_CAVING

## 2026-05-11 - VOXEL_DESTRUCTOR Pass

Status: PENDING VERIFICATION

What was wrong:
- Runtime voxel caves had strong existing async infrastructure, but public carve ingress still lacked an explicit `NativeQueue<VoxelCarveEvent>` contract.
- Fresh laser cuts had dirty blend/global heat but did not encode modified SDF cells into vertex color R for shader slag/soot.
- MapMagic terrain consumers were undocumented relative to laser deformation, so terrain/scatter/resource systems could stay stale after voxel cuts.

What was done:
- Added `VoxelCarveEvent`, `VoxelCarveOperationType`, and `VoxelCarveShapeType`.
- Added a bounded persistent `NativeQueue<VoxelCarveEvent>` to `VoxelDeltaProcessor`, registered through `NativeMemorySentinel`, prewarmed cold, disposed on disable, and drained from `Tick()`.
- Routed public absolute sphere/box/weld/capsule carve APIs into the queue instead of writing straight into the pending carve ring.
- Added Math LOD drain budget: Low/MX350/Unknown=1, Mid=2, High/Ultra=4 queued carves per frame.
- Kept existing `CarveSdfJob : IJobParallelFor` and axis-weighted SDF approximation, feeding it through the existing scheduler to preserve RLE, seam bounds, rebuilds, nav patches, VFX, debris, and collider bake lifecycle.
- Added `VoxelColorJob` modified-cell lookup and set vertex color R to `1.0` when the welded vertex maps to a modified SDF cell.
- Updated `Hecton_AbyssalVoxelRock.shader` so vertex color R plus dirty blend contributes to fresh-cut slag/soot without treating normal AO red as burn.
- Replaced new/touched voxel-step and mouth-darkening divisions with reciprocal multiply during Omega polish.
- Scanned `Assets/_Project/Scripts/World/` for hard MapMagic terrain dependencies and wrote `Docs/AgentLogs/RECON_WORLD_VOXEL_CAVING.md`.
- Wrote `Docs/Tasks/Status_WORLD_VOXEL_CAVING.md` and `Docs/AgentLogs/Rationale_WORLD_VOXEL_CAVING.md`.

Cinematic cheats used:
- Axis-weighted sphere/capsule/box SDF distance instead of exact Euclidean distance.
- Dust `DebrisSpawnSignal` masks the 1-2 frame rebuild latency.
- Vertex color R-channel burn mask plus shader blend replaces Decal GameObjects.
- Queue drain budget spreads destruction over frames based on hardware tier.
- Deferred collider bake proxy avoids presenting a MeshCollider until worker bake completes.

Exact microseconds saved, estimates pending profiler verification:
- NativeQueue burst smoothing: 25-70 us main-thread spike avoided on i3/MX350 when multiple laser hits arrive.
- Preserving half-bit SDF delta storage instead of adding `sbyte` conversion: 40-110 us per modified chunk avoided.
- Modified-chunk rebuild only: 120-450 us avoided per unaffected chunk.
- RLE/dense snapshot avoidance: 50-300 us serialization/IO pressure avoided depending on dirty density.
- Vertex-color burn instead of Decal GameObjects: 60-180 us avoided per laser-hit burst.
- Localized nav patch instead of synchronous predator nav rebuild: 200+ us hitch avoided.
- Deferred collider bake: 300-900 us hitch avoided on collider chunk rebuild.
- Omega reciprocal multiply polish: 2-8 us per rebuilt chunk on i3/MX350 depending on welded vertex count.

Verification:
- `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs`: 0 errors, 0 warnings.
- `validate_script Assets/_Project/Scripts/HectonVoxelEngine.cs`: 0 errors, 0 warnings after Omega reciprocal patch.
- `git diff --check` on touched runtime/shader files: CRLF normalization warnings only.
- Unity compile blocked by external dependency: `Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs(144,42)` ambiguous `DamageSignal` between `Hecton8.Gameplay.DamageSignal` and `Hecton8.Core.Signals.DamageSignal`.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`: timed out after 124 seconds; direct build process was stopped. No clean dotnet build claim.

Integrator note:
- Do not mark this VERIFIED. Status remains PENDING VERIFICATION because global Unity compile is blocked outside voxel domain.
- Fix the UI `DamageSignal` ambiguity first, then rerun Unity compile and a runtime laser carve smoke pass.

## 2026-05-12 - Honest R&D Addendum

Status: PENDING VERIFICATION

What was wrong:
- The first voxel pass had local script validation and source audit, but the existing `VoxelDeformationSmokeTester` did not prove the new async carving contracts would remain present after later edits.
- The old Unity console error for `DiegeticVisorHudMesh.cs` might be stale; current file-level validation needed objective confirmation.

What was done:
- Added `AsyncCarveContracts` phase to `VoxelDeformationSmokeTester`.
- The smoke phase now asserts the concrete contracts: bounded `NativeQueue<VoxelCarveEvent>`, public `TryQueueCarveEvent`, Math LOD drain resolver, High/Ultra drain budget, `CarveSdfJob : IJobParallelFor`, axis-weighted approximation, dirty/compacted sparse RLE writers, localized nav patch, immediate dust/debris signal, no `DecalProjector` dependency, worker `Physics.BakeMesh`, modified-cell map fed into vertex color job, vertex color R burn write, and shader `vertexBurnMask`.

Cinematic cheats used:
- The smoke preserves the axis-weighted carve approximation rather than exact distance math.
- It preserves shader vertex burn masking rather than burn decals.
- It preserves queue-based latency hiding and dust signal masking instead of synchronous carving presentation.

Exact microseconds saved, estimates:
- Runtime cost of the added source-contract smoke: 0 us in gameplay.
- Prevented regression budget: keeps prior estimated savings intact: 25-70 us queue smoothing, 60-180 us no-decal burst saving, 300-900 us async bake hitch avoidance.

Verification:
- `validate_script Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs`: 0 errors / 0 warnings.
- MCP `execute_code` smoke run: `{"tester":"VoxelDeformationSmokeTester","run":1,"pass":true,"phase":"Passed","issue":""}`.
- `validate_script Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs`: 0 errors / 0 warnings.
- Full Unity refresh/compile wait timed out after 60 seconds. `read_console` then reported Unity session not ready. No full build success claimed.
- Editor.log tail lists current global compile blockers outside voxel domain in `SpectrumSystem`, `HectonVisorUberPostFeature`, `SaveBinaryStorage`, `DroneFleetManager`, `AbyssalThermalManager`, `SargassumMicroFaunaBoids`, and `CombatDamageRuntime`; details written to `Docs/AgentLogs/COMPILE_BLOCKERS_WORLD_VOXEL_CAVING.md`.

## 2026-05-12 - Native Queue Invariant R&D

Status: PENDING VERIFICATION

What was wrong:
- `AsyncCarveContracts` caught deleted symbols but did not execute a real `NativeQueue<VoxelCarveEvent>` path.
- The drain-budget switch was private and tied to `GlobalRegistry.ScalabilityTier`, making direct invariant testing awkward.

What was done:
- Added `VoxelDeltaProcessor.DebugResolveQueuedCarveDrainBudget(HectonQualityTier tier)`.
- Routed production `ResolveQueuedCarveDrainBudget()` through the debug-tested helper.
- Added `NativeCarveQueue` phase to `VoxelDeformationSmokeTester`.
- The new smoke phase allocates a temp `NativeQueue<VoxelCarveEvent>`, enqueues subtract/add events, verifies FIFO payload preservation, asserts packet size <=80 bytes, and verifies Unknown/Low/MX350=1, Mid=2, High/Ultra=4.

Cinematic cheats used:
- Math LOD carve throttling remains explicit: toaster-class hardware delays carves instead of simulating impossible instantaneous terrain destruction.
- Axis-weighted carve approximation is still protected by the follow-up source-contract phase.

Exact microseconds saved, estimates:
- Runtime cost of the new invariant: 0 us outside dev smoke.
- Protected savings remain: 25-70 us from low-tier queue smoothing and 60-180 us from vertex burn instead of decals.

Verification:
- `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs`: 0 errors / 0 warnings.
- `validate_script Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs`: 0 errors / 0 warnings.
- MCP `execute_code` smoke run: `{"tester":"VoxelDeformationSmokeTester","run":1,"pass":true,"phase":"Passed","issue":""}`.
- Full project compile remains blocked by external non-voxel files listed in `COMPILE_BLOCKERS_WORLD_VOXEL_CAVING.md`.

## 2026-05-12 - Voxel Black Box R&D

Status: PENDING VERIFICATION

What was wrong:
- The async carve path had source and queue invariants, but no local last-300-frame black box for carve queue depth, scheduled write state, touched bounds, or invalid AUP payloads.
- A NaN carve event could be rejected without writing a voxel-specific binary dump for postmortem reconstruction.

What was done:
- Added `NativeArray<VoxelCarveTelemetryEntry>[300]` to `VoxelDeltaProcessor`, registered/disposed with existing native memory tracking.
- Wrote one fixed-size telemetry sample per `Tick`: queued events, pending carves, scheduled writes, dirty chunks, touched min/max cells, last hit AUP, scheduler flags, drain budget, and compact hash.
- Added finite guards for `VoxelCarveEvent` and `PendingCarveRequest`; invalid payloads are rejected before queue/scheduler admission.
- Added dev/editor dump path: `Docs/AgentLogs/Dump_WORLD_VOXEL_CAVING.bin`, with a `"VOXD"` magic, capacity, stride, cursor, reason flags, and all 300 entries.
- Added `VoxelBlackBox` phase to `VoxelDeformationSmokeTester` for capacity, 64-byte entry size, finite/NaN gate, and dump contract.

Cinematic cheats used:
- The black box records high-level carve state instead of per-voxel traces; enough for failure reconstruction, not a proton simulator.
- The cost remains one compact NativeArray write per voxel tick while visual immersion still comes from vertex burn, dust, and deferred mesh/bake presentation.

Exact microseconds saved, estimates:
- Direct saved time: 0 us; this is failure forensics, not an optimization.
- Avoided debugging waste: invalid carve payloads now stop before job/RLE/nav propagation and emit deterministic evidence.
- Runtime cost estimate: <1 us per voxel `Tick` on i3/MX350, no managed allocation; fault dump is editor/dev cold path only.

Verification:
- `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs` basic: 0 errors / 0 warnings.
- `validate_script Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs` basic: 0 errors / 0 warnings.
- Added-line scan found no new hot-path `foreach`, `string.Format`, `.ToString(`, `new List`, `GameObject.Find`, `FindObjectOfType`, `RaycastAll`, or `OverlapSphere(` in touched voxel diffs.
- MCP `execute_code` smoke could not run because current global compile is blocked outside voxel domain by `SargassumMicroFaunaBoids.cs(2286,17)` missing `PrewarmQueue`; console also reports `SaveBinaryStorage.cs(7667,41)` Burst unsupported `catch` + filter construction.

## 2026-05-12 - Voxel Chunk Event Corridor R&D

Status: PENDING VERIFICATION

What was wrong:
- The carve commit path patched nav directly but did not publish an explicit voxel-modified packet for other systems.
- Downstream systems would have to poll dirty chunks, reach into `VoxelDeltaProcessor`, or infer work from nav-grid side effects. That is cross-domain coupling and expensive under 20+ agents.

What was done:
- Added `Assets/_Project/Scripts/VoxelChunkModifiedEvents.cs`.
- Added `VoxelChunkModifiedEvent`, a 64-byte unmanaged payload carrying volume id, min/max absolute cells, voxel size, frame, operation, shape, source flags, and compact state hash.
- Added bounded `NativeQueue<VoxelChunkModifiedEvent>` capacity 64, registered through `NativeMemorySentinel`, with prewarm and oldest-drop backpressure.
- Updated `VoxelDeltaProcessor` to publish `VoxelChunkModifiedEvents.Publish(in modifiedEvent)` after successful scheduled carve commit and localized nav-grid patch.
- Extended `VoxelDeformationSmokeTester` with `VoxelChunkModifiedEvent` phase and source-contract checks in `AsyncCarveContracts`.

Cinematic cheats used:
- Event payload publishes touched AUP cell bounds instead of per-voxel change lists.
- Consumers get one compact dirty range and can fake dust/audio/fauna reaction around that box instead of simulating exact fragment physics.
- Oldest-drop queue behavior preserves frame time when carve spam exceeds consumer drain.

Exact microseconds saved, estimates:
- Added producer cost: 1-3 us per committed carve on i3/MX350.
- Avoided downstream polling: 20-60 us per frame for systems that would otherwise scan dirty chunk maps or rebuild queues.
- Preserved nav patch saving: 200+ us main-thread spike avoided versus synchronous predator nav rebuild.

Verification:
- `validate_script Assets/_Project/Scripts/VoxelChunkModifiedEvents.cs` basic: 0 errors / 0 warnings.
- `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs` basic: 0 errors / 0 warnings.
- `validate_script Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs` basic: 0 errors / 0 warnings.
- `refresh_unity` completed to idle after script compile request.
- MCP `execute_code` smoke returned `{"tester":"VoxelDeformationSmokeTester","run":1,"pass":true,"phase":"Passed","issue":""}`.
- `git diff --check` on touched voxel files reported only CRLF normalization warnings.
- Added-line scan found no new hot-path `foreach`, `string.Format`, `.ToString(`, `new List`, `GameObject.Find`, `FindObjectOfType`, `RaycastAll`, or `OverlapSphere(`.
- Full project verification is not claimed. Current `read_console` still contains external non-voxel errors in `Assets/_Project/Scripts/Input/UserOptionsPersistence.cs(597,33)` and `(598,28)` for missing `HectonPersistentPathPolicy`; an MCP scene unload error from `ExecuteCode.cs` is also present.

Integrator note:
- New generated asset metadata exists at `Assets/_Project/Scripts/VoxelChunkModifiedEvents.cs.meta`.
- Downstream consumers should drain `VoxelChunkModifiedEvents.TryDequeue(out VoxelChunkModifiedEvent evt)` and treat events as lossy dirty-range notifications, not authoritative save data.

## 2026-05-12 - Voxel Event Corridor Hardening R&D

Status: PENDING VERIFICATION

What was wrong:
- `VoxelChunkModifiedEvents` was bounded but accepted malformed payloads.
- Queue overflow dropped old data without a telemetry counter.
- The voxel black box did not include event-corridor pressure in its compact state hash.

What was done:
- Added `VoxelChunkModifiedEvents.TryPublish(in VoxelChunkModifiedEvent)` with validation.
- Rejected packets now update `DebugRejectedCount` and `DebugLastRejectedStateHash`.
- Overflow drops the oldest event, updates `DebugDroppedCount`, and records `DebugLastDroppedStateHash`.
- `Publish` remains as a compatibility wrapper for the existing carve commit call.
- `VoxelDeltaProcessor` now hashes `VoxelChunkModifiedEvents.PendingCount`, dropped count, and rejected count into the 300-frame black-box sample.
- `VoxelDeformationSmokeTester` now validates valid event payload preservation, NaN voxel-size rejection, oldest-drop overflow behavior, and source contracts for the hardened path.
- Re-extracted the batch prompt with an attribute-aware XML regex because the strict id-only regex failed on the current `role` and `chat_name` attributes.

Cinematic cheats used:
- Dirty-range event stays as one compact AUP cell box, not per-voxel truth.
- Overflow is latest-state biased: stale ranges drop first instead of expanding the queue or stalling the frame.
- Black-box records event pressure as compact counters in the hash, not a full event replay.

Exact microseconds saved, estimates:
- Added producer validation cost: <1 us per packet on i3/MX350.
- Avoided malformed downstream work: 20-60 us whenever consumers would otherwise poll or recover from invalid dirty ranges.
- Queue cap preserves the prior no-stall behavior under carve spam.

Verification:
- `validate_script Assets/_Project/Scripts/VoxelChunkModifiedEvents.cs` basic: 0 errors / 0 warnings.
- `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs` basic: 0 errors / 0 warnings.
- `validate_script Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs` basic: 0 errors / 0 warnings.
- Unity `refresh_unity` initially timed out after 60s, then recovered and reported editor ready.
- MCP `execute_code` smoke returned `{"tester":"VoxelDeformationSmokeTester","run":1,"pass":true,"phase":"Passed","issue":""}`.
- `git diff --check` on touched voxel/doc files reported only CRLF normalization warnings in touched C# files.
- Added-line scan found no new hot-path `foreach`, `string.Format`, `.ToString(`, `new List`, `GameObject.Find`, `FindObjectOfType`, `RaycastAll`, or `OverlapSphere(`.
- Full project verification is not claimed. Latest Unity console has no C# compiler errors in the last 20 entries, but it contains MCP websocket warnings and one blank exception entry.
