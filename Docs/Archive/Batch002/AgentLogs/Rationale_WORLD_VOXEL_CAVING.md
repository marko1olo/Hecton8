# Rationale_WORLD_VOXEL_CAVING

Status: PENDING VERIFICATION

## Initial Scope

Problem: Static SDF caves need asynchronous carving, modified chunk meshing, RLE persistence, burn coloring, nav/VFX/debris signals, collider bake gating, and hard MapMagic dependency reconnaissance.
Solution: Build around existing world/voxel code only after repository inspection. Use NativeQueue/NativeArray/Burst jobs where matching project contracts exist. Keep systems decoupled through GlobalRegistry or EventBus-style signals instead of direct cross-agent dependencies.
Rejected Alternatives: Direct Unity mesh/collider edits from the laser cutter hot path are rejected because they stall the main thread and break zero-GC and bake-state rules. Decal GameObjects for burns are rejected by assignment and by the visual-fake mandate; vertex color is the cheaper rendering channel.
Scalability potential: Low processes 1 carve per frame and masks latency with dust/vertex color. Middle can batch 2. High can batch 4. Ultra can spend saved CPU on denser burn data, more analytical normals, and richer slag shading.
Hardware Impact: Expected low-end i3/MX350 gain depends on current code shape. Initial target is to keep carve dispatch below 0.1 ms main thread and move SDF edits, mesh rebuild prep, and bake work out of synchronous gameplay paths. Numeric proof remains PENDING VERIFICATION.

## Mandate Selection

- VOX_Voxel_World_Logic_Carving_Persistence: chunk addressing, carve queue, dirty propagation, RLE/save lifecycle.
- VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline: SDF topology and meshing integration constraints.
- OPT_Native_Memory_Collections_JobSystem_Protocol: NativeContainer ownership, job scheduling, disposal, Burst safety.
- OPT_Zero_GC_Policy_AllocFree_Mandate: hot-path allocation bans.
- DBG_Telemetry_Crash_Reporting_PostMortem: 300-frame black box and crash dump expectations.
- DATA_Save_Persistence_Binary_Delta_Checksum: delta persistence and binary save boundaries.
- MATH_Coordinate_Precision_AUP_FloatingOrigin: authoritative carve coordinates.
- ARCH_Global_Registry_ServiceLocator_DI_Init: decoupling and init/service access rules.

## Decision 1 - Async Carve Ingress

Problem: Public carve calls wrote straight into the managed pending carve ring, so the laser cutter had no explicit NativeContainer ingress lane and burstable callers had no fixed event contract.
Solution: Added `VoxelCarveEvent` plus a bounded `NativeQueue<VoxelCarveEvent>` owned by `VoxelDeltaProcessor`. Existing public absolute carve APIs now push AUP hit point, radius, operation, shape, material, and impulse into this queue. `Tick()` drains by Math LOD into the existing scheduled carve ring.
Rejected Alternatives: Replacing the entire pending carve and commit pipeline was rejected because the existing code already owns RLE dirty chunks, rebuild enqueue, nav patching, debris, and bake state. Direct synchronous mesh mutation from the laser path remains rejected.
Scalability potential: Low/MX350 drains 1 carve per frame. Mid drains 2. High/Ultra drain 4 and spend saved latency budget on richer burn and debris signals.
Hardware Impact: Low-end i3/MX350 avoids a same-frame carve burst when multiple hits arrive. Estimate: 25-70 us main-thread spike avoided on queue bursts, PENDING VERIFICATION.

## Decision 2 - Preserve Existing Half-Bit SDF Pipeline

Problem: The prompt names `NativeArray<sbyte>` SDF data, but this project stores persistent voxel deltas as half-bit `ushort` density values and compacts them into sparse/uniform RLE snapshots.
Solution: Kept the existing `CarveSdfJob : IJobParallelFor` and its axis-weighted sphere/box/capsule approximation. The new `NativeQueue` feeds that job through the existing request scheduler so RLE, seam bounds, nav patches, and rebuild queues stay intact.
Rejected Alternatives: Introducing a parallel `sbyte` SDF store would fork persistence and break current `ChunkDeltaState.SdfValueBits`, `TryBuildDeltaMapForVolume`, and compaction code.
Scalability potential: Low keeps coarse half-density deltas and one active carve. High/Ultra can batch queue ingress while the job covers larger candidate bounds without a new storage format.
Hardware Impact: Avoids a second SDF representation and conversion pass. Estimate: 40-110 us per modified chunk avoided, PENDING VERIFICATION.

## Decision 3 - Vertex Color R Burn Channel

Problem: Fresh cuts had dirty UV blend and global heat, but the mesh did not mark directly modified SDF cells in vertex color R as requested.
Solution: `VoxelColorJob` now reads `NativeParallelHashMap<int3, VoxelModifiedCell>` and sets vertex color R to `1.0` when the welded vertex maps to a modified SDF cell. `Hecton_AbyssalVoxelRock.shader` treats `R >= 0.999` plus dirty blend as burn/slag input.
Rejected Alternatives: Decal GameObjects were rejected by assignment and by the visual-fake mandate. A separate burn mesh stream was rejected because it duplicates vertex data and adds upload cost.
Scalability potential: Low gets a zero-object burn mask embedded in existing mesh data. Mid/High keep the same mesh channel and can enhance shader response. Ultra can increase material contrast and heat response without CPU work.
Hardware Impact: Eliminates per-hit decal allocation/transform/culling overhead. Estimate: 60-180 us saved on laser-hit bursts versus GameObject decals, PENDING VERIFICATION.

## Decision 4 - Shader Gate For Burn Safety

Problem: Vertex color RGB already encodes cave AO and cave-mouth splat shading. Using red directly as burn would falsely char bright AO vertices.
Solution: The shader gates burn on `terrainSplatColor.r >= 0.999` and `freshCutBlend`, then uses green/blue AO for burn-safe cave AO only on burned vertices.
Rejected Alternatives: Repacking all color channels was rejected because it would break cave-mouth tinting and require broader art-side retuning.
Scalability potential: All tiers pay two half operations and one step; High/Ultra can increase slag contrast through material parameters.
Hardware Impact: Negligible shader ALU cost; avoids CPU-side decal fallback entirely. Estimate: <3 us GPU impact per visible chunk, PENDING VERIFICATION.

## Decision 5 - Compile Wall Ownership

Problem: Unity compile is blocked by `Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs(144,42)` due ambiguous `DamageSignal` between `Hecton8.Gameplay` and `Hecton8.Core.Signals`.
Solution: Do not edit UI code from voxel domain. Record as external dependency while validating touched voxel scripts independently.
Rejected Alternatives: Patching the UI namespace collision from the voxel task was rejected as cross-domain churn and unsafe with 20+ agents working concurrently.
Scalability potential: Not runtime-relevant; compile blocker only.
Hardware Impact: None. Build verification remains PENDING VERIFICATION until the UI dependency is fixed.

## OMEGA POLISH CHANGES

Problem: The polish mandate required an anti-bloat audit after core closure. New burn-cell code used a direct `position / voxelStep` division and the touched color job still had hot-path divisions in terrain/mouth blending.
Solution: Replaced those divisions with `math.rcp(...)` plus multiplication in `VoxelColorJob.IsModifiedSdfCell`, `VoxelDirtyBlendJob.Execute`, `VoxelColorJob.TryResolveCaveMouthTerrainColor`, and `VoxelColorJob.SampleTerrainHeight`.
Rejected Alternatives: Leaving exact divisions in a Burst mesh-color path was rejected because reciprocal multiply is cheaper and preserves the same visual result.
Scalability potential: Low/MX350 gets lower color/dirty-blend ALU cost. Mid/High/Ultra preserve the same burn fidelity and can spend budget on shader slag intensity.
Hardware Impact: Estimate: 2-8 us per rebuilt chunk on i3/MX350 depending on welded vertex count, PENDING VERIFICATION.

Cinematic cheats used:
- Axis-weighted sphere/capsule/box SDF approximation instead of exact Euclidean length.
- NativeQueue drain budget to spread carve bursts over frames instead of proving instantaneous destruction.
- Vertex color R-channel burn mask plus shader slag/soot blend instead of Decal GameObjects.
- Immediate `DebrisSpawnSignal` dust to hide mesh rebuild latency.
- Deferred collider bake proxy so collision presentation waits for worker bake completion.

Omega audit evidence:
- `validate_script` zero diagnostics: `Assets/_Project/Scripts/VoxelDeltaProcessor.cs`.
- `validate_script` zero diagnostics: `Assets/_Project/Scripts/HectonVoxelEngine.cs` after reciprocal patch.
- Static scan of touched runtime files found no `foreach`, `string.Format`, `.ToString(`, `math.sqrt`, or `math.normalize` in the authored runtime changes. Existing string interpolation hits are in the editor inspector block of `HectonVoxelEngine`.
- `git diff --check` on touched runtime/shader files reports only CRLF normalization warnings.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` timed out after 124 seconds; direct build process `50416` was stopped. This did not produce a clean build result.
- Unity console compile remains blocked by external `Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs(144,42)` ambiguous `DamageSignal`.

Final Git diff summary vs current HEAD for touched runtime/shader files:
- `Assets/_Project/Art/Shaders/Hecton_AbyssalVoxelRock.shader`: 18 insertions, 13 deletions.
- `Assets/_Project/Scripts/HectonVoxelEngine.cs`: 44 insertions, 21 deletions.
- `Assets/_Project/Scripts/VoxelDeltaProcessor.cs`: 781 insertions, 138 deletions.
- Note: this worktree had pre-existing same-file changes before this agent pass; the summary is repository diff, not exclusive authorship.

## Honest R&D Addendum - Smoke Contract Hardening

Problem: Source edits were validated, but the smoke harness did not lock the actual async carving contracts. A future agent could remove the queue, vertex burn, RLE writer, nav patch, or async bake path while the older smoke still passed.
Solution: Extended `VoxelDeformationSmokeTester` with an `AsyncCarveContracts` phase. In Editor it reads `VoxelDeltaProcessor.cs`, `HectonVoxelEngine.cs`, and `Hecton_AbyssalVoxelRock.shader` and asserts the exact contracts for `NativeQueue<VoxelCarveEvent>`, public queue ingress, Math LOD drain, `CarveSdfJob : IJobParallelFor`, `AxisWeightedLengthApprox`, sparse RLE writers, localized nav-grid patch, immediate debris/dust signal, absence of `DecalProjector`, worker `Physics.BakeMesh`, `ModifiedCells` into `VoxelColorJob`, `colorPayload.x = 1f`, and shader `vertexBurnMask`.
Rejected Alternatives: Adding another standalone smoke file was rejected because this project already has a voxel deformation smoke tester. Runtime-only scene tests were rejected for this pass because Unity readiness is unstable and the immediate need is preventing regression of the authored contracts.
Scalability potential: Low tier benefits from tests preserving 1-per-frame queueing and no decal burn objects. High/Ultra keep the 4-per-frame drain and richer shader path.
Hardware Impact: Editor-only source read has no runtime frame cost. Runtime smoke still uses tiny 1-8 element NativeArray fixtures. Estimated gameplay impact: 0 us.

Evidence:
- `validate_script Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs`: 0 errors / 0 warnings.
- MCP `execute_code` smoke run returned `{"tester":"VoxelDeformationSmokeTester","run":1,"pass":true,"phase":"Passed","issue":""}`.
- `validate_script Assets/_Project/Scripts/UI/DiegeticVisorHudMesh.cs`: 0 errors / 0 warnings, so the previous `DamageSignal` console error is stale or was fixed by another agent before this pass.
- `refresh_unity` compile wait timed out after 60 seconds; subsequent `read_console` calls returned Unity session not ready. Do not record full compile success.
- Editor.log tail read after MCP timeout shows the current global compile wall has moved to non-voxel files: `SpectrumSystem`, `HectonVisorUberPostFeature`, `SaveBinaryStorage`, `DroneFleetManager`, `AbyssalThermalManager`, `SargassumMicroFaunaBoids`, and `CombatDamageRuntime`. Full list: `Docs/AgentLogs/COMPILE_BLOCKERS_WORLD_VOXEL_CAVING.md`.

## Honest R&D Addendum - Native Queue Invariant

Problem: The `AsyncCarveContracts` smoke prevented deleted symbols, but a source-string check still cannot prove that the carve packet is a valid NativeQueue payload or that the Math LOD budget is callable without touching `GlobalRegistry`.
Solution: Added `VoxelDeltaProcessor.DebugResolveQueuedCarveDrainBudget(HectonQualityTier tier)` and changed the runtime resolver to delegate to it. Extended `VoxelDeformationSmokeTester` with `NativeCarveQueue`: it allocates a temp `NativeQueue<VoxelCarveEvent>`, enqueues subtract/add payloads, verifies FIFO dequeue and payload fields, asserts packet size stays <=80 bytes, and checks Unknown/Low/MX350=1, Mid=2, High/Ultra=4.
Rejected Alternatives: Reflection over private resolver state was rejected because it is brittle and allocation-heavy. A scene-based laser carve test was deferred because the global project compile remains blocked outside voxel domain.
Scalability potential: Low/MX350 queue throttling now has a direct invariant. High/Ultra 4-per-frame budget is locked without depending on console-only source inspection.
Hardware Impact: Editor/dev smoke only. Runtime impact is 0 us; the production resolver still performs one switch through the same helper.

Evidence:
- `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs`: 0 errors / 0 warnings after helper extraction.
- `validate_script Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs`: 0 errors / 0 warnings after NativeQueue smoke.
- MCP smoke run returned `{"tester":"VoxelDeformationSmokeTester","run":1,"pass":true,"phase":"Passed","issue":""}`.

## Honest R&D Addendum - Voxel Black Box

Problem: The voxel carving path was a critical system with queueing, async Burst writes, RLE persistence, nav patching, and visual burn state, but it did not keep a local 300-frame postmortem ring. A bad AUP hit, NaN radius, or invalid pending carve could be rejected by incidental guards without leaving enough state for the integrator to reconstruct what happened.
Solution: Added a fixed `NativeArray<VoxelCarveTelemetryEntry>[300]` to `VoxelDeltaProcessor`, registered through the existing NativeMemorySentinel array path and disposed with the component. `Tick()` writes one high-level sample containing queue depth, pending carve depth, scheduled write count, dirty chunk count, touched-cell bounds, active hit AUP, state flags, drain budget, and a compact state hash. `TryQueueCarveEvent` and pending-carve admission now reject non-finite payloads and trigger a dev/editor binary dump at `Docs/AgentLogs/Dump_WORLD_VOXEL_CAVING.bin`.
Rejected Alternatives: Using the global `CrashTelemetryBuffer` alone was rejected because it lacks carve-specific queue depth, touched cell bounds, and AUP hit data. Per-event `Debug.Log` was rejected because it allocates/noises the hot path and loses the last-300-frame sequence. A managed `Queue<T>` was rejected because the black-box mandate requires fixed native storage.
Scalability potential: Low/MX350 pays one 64-byte NativeArray write per voxel tick and gets deterministic failure evidence without expanding carve work. Middle/High/Ultra keep the same telemetry cost while using the saved cycles for richer shader burn, more debris, or denser rebuilds. Ultra can later widen the dump reader/visualizer without changing the runtime ring.
Hardware Impact: Estimated hot-path cost is below 1 us per voxel `Tick` on i3/MX350: one bounded NativeArray assignment, a small FNV-style hash over counters, and no managed allocation. Fault-path dump is editor/dev only and intentionally cold.

Evidence:
- `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs` basic: 0 errors / 0 warnings after black-box addition.
- `validate_script Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs` basic: 0 errors / 0 warnings after `VoxelBlackBox` phase.
- `VoxelDeformationSmokeTester` now validates black-box capacity=300, entry size=64, finite-event acceptance, NaN-event rejection, and dump-path/source contract.
- `git diff` scan of added lines in touched voxel files found no new hot-path `foreach`, `string.Format`, `.ToString(`, `new List`, `GameObject.Find`, `FindObjectOfType`, `RaycastAll`, or `OverlapSphere(`.
- MCP `execute_code` smoke run could not execute because Unity currently reports non-voxel compile blockers: `SargassumMicroFaunaBoids.cs(2286,17)` missing `PrewarmQueue`, and `SaveBinaryStorage.cs(7667,41)` Burst `catch` + filter unsupported. Do not record full compile or runtime smoke success for this addendum.

## Honest R&D Addendum - Voxel Chunk Modified Event

Problem: Task 7 had localized nav patching, but the carve commit path still forced downstream systems to infer voxel changes from the processor or nav runtime. That violates simultaneous-agent decoupling: ecosystem, AI, audio, or streaming consumers need a bounded voxel-domain event lane without depending on direct processor internals.
Solution: Added `VoxelChunkModifiedEvents` with a fixed `NativeQueue<VoxelChunkModifiedEvent>` capacity of 64 and a 64-byte unmanaged payload. `VoxelDeltaProcessor.TryCommitScheduledCarve()` now publishes one event after a successful touched-cell commit, including volume id, min/max absolute cells, voxel size, operation, shape, source flags, frame, and a compact state hash. `VoxelDeformationSmokeTester` now has a `VoxelChunkModifiedEvent` phase that publishes/dequeues a packet and validates payload preservation plus source contracts.
Rejected Alternatives: Editing `GlobalSignals` was rejected because it is a shared core file and already dirty in the worktree. Polling dirty chunks from ecosystem systems was rejected because it adds frame cost and cross-domain knowledge. A managed C# event was rejected because it is not Burst/job-friendly and does not satisfy the zero-GC event-corridor mandate.
Scalability potential: Low/MX350 uses one bounded packet per committed carve, letting consumers drain slowly or drop oldest without scanning chunk maps. Middle keeps the same packet with stable bounds for nav/audio. High can attach richer downstream response to the event. Ultra can spend saved polling cost on heavier visual overkill around the touched cell range without changing the producer.
Hardware Impact: Estimated hot-path cost is 1-3 us per committed carve on i3/MX350: one NativeQueue enqueue and a small integer hash. Estimated avoided cost is 20-60 us for downstream consumers that no longer need to poll dirty chunk dictionaries or inspect mesh rebuild queues.

Evidence:
- `validate_script Assets/_Project/Scripts/VoxelChunkModifiedEvents.cs` basic: 0 errors / 0 warnings.
- `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs` basic: 0 errors / 0 warnings after event publish integration.
- `validate_script Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs` basic: 0 errors / 0 warnings after smoke phase addition.
- MCP `execute_code` smoke returned `{"tester":"VoxelDeformationSmokeTester","run":1,"pass":true,"phase":"Passed","issue":""}`.
- `git diff --check` on touched voxel files reported only CRLF normalization warnings.
- Added-line scan found no new hot-path `foreach`, `string.Format`, `.ToString(`, `new List`, `GameObject.Find`, `FindObjectOfType`, `RaycastAll`, or `OverlapSphere(`.
- Latest `read_console` still contains external non-voxel errors in `Assets/_Project/Scripts/Input/UserOptionsPersistence.cs(597,33)` and `(598,28)` for missing `HectonPersistentPathPolicy`. Do not record full project compile success.

## Honest R&D Addendum - Voxel Event Corridor Hardening

Problem: `VoxelChunkModifiedEvents` had bounded native storage, but malformed packets could still enter the queue and overflow was silent. A bad dirty-range packet with NaN voxel size or inverted bounds would poison downstream nav/ecosystem consumers; a burst overflow would erase history with no counter for postmortem analysis.
Solution: Added `TryPublish(in VoxelChunkModifiedEvent)` with zero-GC packet validation, rejection telemetry, oldest-drop overflow telemetry, and last rejected/dropped state hashes. Kept `Publish` as a compatibility wrapper. Added event pending/drop/reject counters into the voxel black-box state hash so dumps reflect corridor pressure. Extended `VoxelDeformationSmokeTester` to assert valid publish/dequeue, NaN rejection, oldest-drop overflow behavior, and the hardened source contracts.
Rejected Alternatives: `Debug.Log` on rejected packets was rejected because the corridor can be hit from hot carve paths and logs allocate/noise the frame. Throwing exceptions was rejected because malformed packets are data faults, not a reason to crash the terrain lane. Expanding the queue was rejected because capacity inflation hides backpressure instead of exposing it.
Scalability potential: Low/MX350 keeps a 64-packet cap and drops stale dirty ranges deterministically. Middle/High can drain more consumers without any producer change. Ultra can attach richer ecosystem/audio reactions while using the same lossy event contract, not a larger poller.
Hardware Impact: Estimated hot-path overhead is below 1 us per published packet on i3/MX350: finite float check, integer bounds comparisons, enum range checks, and one enqueue. Overflow/rejection counters are scalar writes. Avoided cost remains 20-60 us versus downstream polling and removes invalid-packet propagation risk.

Evidence:
- Attribute-aware CLI extraction captured only `<AGENT_PROMPT id="WORLD_VOXEL_CAVING" role="VOXEL_DESTRUCTOR" ...>` after the strict id-only regex failed.
- `validate_script Assets/_Project/Scripts/VoxelChunkModifiedEvents.cs` basic: 0 errors / 0 warnings.
- `validate_script Assets/_Project/Scripts/VoxelDeltaProcessor.cs` basic: 0 errors / 0 warnings after black-box hash inclusion.
- `validate_script Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs` basic: 0 errors / 0 warnings after invalid/overflow smoke assertions.
- Unity `refresh_unity` first timed out after 60s, then recovered and reported editor ready.
- MCP `execute_code` smoke returned `{"tester":"VoxelDeformationSmokeTester","run":1,"pass":true,"phase":"Passed","issue":""}`.
- `git diff --check` on touched voxel/doc files reported only CRLF normalization warnings in touched C# files.
- Added-line scan found no new hot-path `foreach`, `string.Format`, `.ToString(`, `new List`, `GameObject.Find`, `FindObjectOfType`, `RaycastAll`, or `OverlapSphere(`.
- Latest Unity console has no C# compiler errors in the last 20 entries, but contains MCP websocket warnings and one blank exception entry. Do not record full project verification.
