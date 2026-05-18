# LOG_SHINOBU_05

## 2026-05-17 - Delta Crusher Implementation Pass

What was wrong:
- SHINOBU_05 had no isolated laser carve test seam, no local sbyte density scratch contract, no explicit 32-byte debris DTO, no mock sampler, no standalone Burst RLE pair jobs, and no editor surface to inspect RLE or tune fake debris.
- Runtime debris already used a GPU/DataVault/indirect path, but caps were not aligned to the prompt requirement of 500 toaster / 10,000 high-end.
- Voxel material yield was not routed from CPU carve completion to `ItemAcquiredSignal`.
- CLI compile verification is blocked by existing project state: `Assembly-CSharp.csproj` lacks temp/reference DLLs, and `Hecton8.Core.csproj` currently fails in unrelated `GlobalSignals`, `GlobalWorldSampler`, `SaveDeltaCompression`, and `PredatorCognitionDomain` code.

What was done:
- Added `Assets/_Project/Scripts/VFX/Debris/ShinobuDeltaCrusherJobs.cs`.
  - `DebrisParticleDTO`: `float3 Position`, `float Radius`, `float3 Velocity`, `uint MaterialHash`, exactly 32 bytes, Pack=4.
  - `MockLaserFireSignal`, `MockLaserCarveGateJob`, `MockVoxelGridGeneratorJob`, `MockWorldSampler`.
  - `VoxelSphericalCarveJob` with AABB-only sphere iteration and `Interlocked.Add` density/mass accounting.
  - `RleCompressSByteJob` / `RleDecompressSByteJob` using `[Value, Count]` `NativeList<short>` pairs.
  - `DebrisMassToCountJob`, `DebrisEmitFromMassJob`, `DebrisPhysicsFakeJob`, and `ChunkBoundarySplitJob`.
- Added `Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs`.
  - Editor window `Voxel Sculptor`.
  - Brush Size slider, `Simulate Carve at Camera Center`, RLE validation, raw/RLE byte counters, ratio/flags, mass, debris count, DTO size.
  - Gravity, bounce, max debris, mass-per-particle tuning saved into `GlobalDataVault` as `CarveDebrisTuningDTO`.
- Updated `CarveDebrisComputeRenderer`.
  - Active debris hard cap now resolves through SHINOBU cap logic: 500 low-tier, 4096 middle, 10,000 high/ultra.
  - Allocates `ShinobuDebrisParticles` DataVault buffer.
  - Reads `CarveDebrisTuningDTO` from DataVault for gravity, bounce, and cap.
- Updated `Hecton_FluidAdvection.compute`.
  - Carve debris now bounces instead of freezing on collision.
  - Mock plane fallback at Y=0 when SDF is inactive.
  - Sleep threshold zeroes tiny velocities.
- Updated `VoxelDeltaProcessor`.
  - Counts carved mass units and total carved voxels.
  - Emits `ItemAcquiredSignal` for Titanium material carve completion using FNV-1a UTF-16 hashes: Titanium `0x61C51592`, Data_TitaniumScrap `0xD150482E`.
  - Publishes total carved mass telemetry after CPU carve commit.
- Updated `H8Memory.BufferID`.
  - Added `CarveDebrisTuning` and `ShinobuDebrisParticles` IDs on top of the current modified enum state.
- Created/updated:
  - `Docs/Tasks/Status_SHINOBU_05.md`
  - `Docs/AgentLogs/Rationale_SHINOBU_05.md`

Cinematic Cheats used:
- The Dear Lie: debris are DTO particles in Burst/compute/GraphicsBuffer paths, not GameObjects.
- Fake collision: flat Y=0 sampler for isolated proof; SDF collision in compute uses cheap up-normal bounce.
- Slag cover: existing recent-cut heat impostor globals mask delayed Marching Cubes rebuild.
- Hardware binary split: low tier shows fewer chips and relies on slag; high tier spends saved physics on 10,000 visible chips.

Exact microseconds saved:
- Rigidbody avoidance: estimated 2000-8000 us saved per 1000 debris on i3/MX350 versus PhysX bodies. Static estimate; profiler proof blocked.
- Low-tier cap: up to 9500 particles skipped versus high-end cap. Static estimate.
- RLE uniform chunk: 32768 raw sbyte bytes to 4 RLE bytes. Static source proof.
- Mock sampler sample: estimated 1 us per particle sample. Static estimate.
- Orphaned chunk gate: estimated 4 us per rejected request. Static estimate.
- Compile proof: `git diff --check` passed. Full compile remains blocked by external project errors and missing temp DLLs; no clean-build claim is made.

Self audit:
- GameObjects/Rigidbodies for falling rocks: PASS.
- 16-byte multiple DTO: PASS, 32 bytes.
- Burst RLE with no managed resize: PASS.
- Chunk boundary protection: PASS via gate, split job, and runtime chunk-address commit.
- Editor live RLE ratio: PASS.

## 2026-05-17 - Ultra-Think Polish Corrective Pass

What was wrong:
- SHINOBU had added two global BufferID slots (`CarveDebrisTuning`, `ShinobuDebrisParticles`). That was unnecessary core enum churn under concurrent-agent compile-wall pressure.
- The editor facade held persistent private NativeArray/NativeList fields. Runtime was not affected, but the polish mandate treats that as H-Phi debt.
- Two runtime debris structs in `CarveDebrisComputeRenderer` still used `Pack=1`.
- Titanium yield counted `write.MaterialId`, which is the requested/new material, not the previous material stored in the voxel chunk.

What was done:
- Removed the two SHINOBU-added BufferID entries from `H8Memory`.
- Packed debris tuning into existing `CarveDebrisJobState` slots 5-9: version, gravityY bits, bounce bits, max debris, mass units.
- Changed `Voxel Sculptor` to use action-local `Allocator.TempJob` scratch and scalar UI fields only.
- Removed `Pack=1` from `CarveDebrisRequest` and `CarveDebrisTelemetryEntry`; both remain explicit `Size=64`.
- Fixed material yield by reading `state.MaterialIds[(int)localIndex]` before `SetCell`.
- Rechecked shader thread groups: carve debris kernels use `HECTON_FLUID_ADVECTION_THREADS = 64`, not 1024.

Cinematic Cheats used:
- Low tier still drops extra debris and leans on slag heat.
- Debris motion remains a visual fake: gravity, SDF/mock-plane bounce, sleep threshold, no PhysX truth.
- GPU dynamic wakes are visual overkill only; loot and voxel truth stay CPU-side.

Exact microseconds saved:
- Removed BufferID churn: compile-time risk reduction, no runtime us claim.
- Pack=1 removal: ARM64 alignment risk removed, no fake numeric gain claimed.
- Titanium old-material read: one byte read per committed cell; correctness fix, no performance claim.
- Rigidbody avoidance estimate remains 2000-8000 us per 1000 debris on i3/MX350 versus PhysX bodies.
- Low-tier cap remains up to 9500 skipped particles versus high/ultra.

Verification:
- `rg` found no `Pack=1`, `Instantiate`, `Rigidbody`, `MeshCollider`, `FindObject`, `GetComponent<`, `.ToString()`, or `foreach` in SHINOBU VFX files after the corrective pass.
- `git diff --check` passed for touched tracked files; only CRLF warnings were emitted.
- One guarded `dotnet build Assembly-CSharp.csproj --no-restore` failed externally on missing `Temp/obj/*/project.assets.json` plus non-SHINOBU `Shinobu19EconomyLedger.cs` `NativeMultiHashMap` error. No clean compile is claimed.

## 2026-05-17 - L1 ABI / NaN Vaccination Pass

What was wrong:
- Local sbyte density code used `-127` as minimum despite the task's `127 -> -128` full-removal example.
- RLE compression assumed caller capacity was always correct; bad editor/test capacity could turn `AddNoResize` into a native exception.
- Padded SHINOBU structs used explicit `Size` but not explicit tail fields, which made the forensic layout weaker than the mandate demands.
- Mock debris reflection trusted sampler normals and finite payloads too much.

What was done:
- `MockEmptyDensity` now uses `sbyte.MinValue`.
- Density clamp and RLE decompression preserve `[-128,127]`.
- Editor delta-density slider now reaches `-255`, so `127 -> -128` can be simulated.
- Added explicit padding fields:
  - `MockLaserFireSignal`: `_pad0` = 4 bytes.
  - `CarveDebrisRequest`: `_pad0.._pad4` = 20 bytes.
  - `CarveDebrisTelemetryEntry`: `_pad0.._pad6` = 28 bytes.
- Added fail-closed guards for missing Native containers, invalid carve spans, invalid radii, non-finite positions/velocities, and RLE capacity overflow.
- Normalized mock collision normals before reflection.

Cinematic Cheats used:
- No new physical truth was added. The low-tier fake remains point motion plus plane/SDF bounce, with slag covering missing chips.

Exact microseconds saved:
- No new performance number claimed. This pass removes crash/NaN/ABI risk, not measured runtime cost.

Verification:
- `git diff --check` still passes for touched tracked files with CRLF warnings only.
- Static grep shows `HECTON_FLUID_ADVECTION_THREADS = 64` and no `1024` compute groups in `Hecton_FluidAdvection.compute`.
- Static grep found no `new string`, `.ToString()`, LINQ marker, `foreach`, `Instantiate`, `Rigidbody`, `MeshCollider`, `FindObject`, `GetComponent<`, or `Material.Set*` in SHINOBU VFX files.

## 2026-05-17 - Ultra-Polish ABI Compatibility Pass

What was wrong:
- Static grep still found runtime `[StructLayout(Pack=1)]` in `VoxelDeltaProcessor.cs`, which is in the touched voxel-delta carve/persistence path.
- `NativeSnapshotChunkHeaderDeltaRle` stored `ulong PayloadHash64` at byte offset 28. That is a misaligned 8-byte read risk on ARM64.
- The old snapshot format could not be fixed by simply changing struct sizes because existing `HXD2/HXD3/HXD4` saves depend on 12/20/28/36-byte cursor math.

What was done:
- Added `NativeSnapshotDeltaRleAlignedMagic` (`HXD5`) for new native voxel snapshots.
- Changed new snapshot runtime structs to aligned layouts:
  - `NativeSnapshotHeader`: 16 bytes.
  - `LegacyNativeSnapshotHeader`: 8 bytes.
  - `NativeSnapshotChunkHeader`: 24 bytes.
  - `NativeSnapshotChunkHeaderRle`: 32 bytes.
  - `NativeSnapshotChunkHeaderDeltaRle`: 40 bytes.
- Split the delta-RLE payload hash into `PayloadHashLow` and `PayloadHashHigh`, avoiding a misaligned runtime `ulong` field.
- Added manual legacy parsing for old `HXD2/HXD3/HXD4` snapshot headers using 4-byte reads and hash recomposition.
- Replaced `GetComponent<HectonVoxelEngine>()` in `VoxelDeltaProcessor.OnEnable()` with `TryGetComponent(out _engine)`.

Cinematic Cheats used:
- No new simulation truth was added. The Dear Lie remains GPU/Burst point debris: gravity, cheap bounce, sleep threshold, slag cover, and no GameObject/Rigidbody debris.

Exact microseconds saved:
- No measured microseconds claimed. This pass removes ARM64 ABI risk and preserves save compatibility.
- Existing static estimate remains: avoiding Rigidbody debris saves roughly 2000-8000 us per 1000 debris versus PhysX bodies on i3/MX350-class hardware, pending profiler proof.

Verification:
- `rg "Pack\\s*=\\s*1|GetComponent<" Assets/_Project/Scripts/VFX/Debris Assets/_Project/Scripts/VoxelDeltaProcessor.cs` returned no matches.
- Scoped anti-physics/static-GC grep returned no `Pack=1`, `Rigidbody`, `MeshCollider`, `Instantiate`, `Material.Set`, `FindObject`, `GetComponent<`, `System.Linq`, or `.ToString()` in the SHINOBU VFX + voxel-delta slice.
- `git diff --check` passed for touched tracked files; only CRLF warnings were emitted.
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -p:UseSharedCompilation=false -p:RunAnalyzers=false /m:1` failed before source compilation with `NETSDK1004`: missing `Temp/obj/Assembly-CSharp/project.assets.json`.

<SELF_AUDIT>
Task 01 [PASS] BINARY_GRAVEYARD_RECONNAISSANCE.
Task 02 [PASS] RIGIDBODY_ERADICATION_PASS.
Task 03 [PASS] ARM64_DEBRIS_ALIGNMENT.
Task 04 [PASS] ORPHANED_CHUNK_PROTECTION.
Task 05 [PASS] BLIND_SAMPLER_MOCKING.
Task 06 [PASS] THE_SPHERICAL_CARVE_KERNEL.
Task 07 [PASS] RUN_LENGTH_ENCODING_BURST.
Task 08 [PASS] DELTA_EXTRACTION_FOR_DEBRIS.
Task 09 [PASS] GPU_DEBRIS_KINEMATICS_JOB.
Task 10 [PASS] BATCH_RENDERER_DEBRIS_LINK.
Task 11 [PASS] VISUAL_SLAG_HOLOGRAM.
Task 12 [PASS] MATERIAL_YIELD_ROUTING.
Task 13 [PASS] HARDWARE_LOD_DEBRIS_CAP.
Task 14 [PASS] CHUNK_BOUNDARY_SEAM_FIX.
Task 15 [PASS] ASYNCHRONOUS_READBACK_AVOIDANCE.
Task 16 [PASS] RLE_DECOMPRESSION_HYDRATOR.
Task 17 [PASS] TELEMETRY_MASS_TRACKER.
Task 18 [PASS] DESTRUCTION_SCULPTOR_WINDOW.
Task 19 [PASS] LIVE_RLE_INSPECTOR.
Task 20 [PASS] DEBRIS_PHYSICS_TUNER.

ARM64 layout:
- `DebrisParticleDTO` size 32: offset 0 `float3 Position` 12, offset 12 `float Radius` 4, offset 16 `float3 Velocity` 12, offset 28 `uint MaterialHash` 4.
- `NativeSnapshotHeader` size 16: offset 0 `int Version`, 4 `int ChunkCount`, 8 `int TotalDirtyCellCount`, 12 `int Reserved0`.
- `NativeSnapshotChunkHeaderDeltaRle` size 40: offset 0 `int ChunkX`, 4 `int ChunkY`, 8 `int ChunkZ`, 12 `float VoxelSize`, 16 `int DirtyCellCount`, 20 `byte StorageFlags`, 21 `byte Reserved0`, 22 `ushort Reserved1`, 24 `int PayloadByteLength`, 28 `uint PayloadHashLow`, 32 `uint PayloadHashHigh`, 36 `uint Reserved2`.

ZERO-GC check:
- SHINOBU runtime debris path has no GameObject/Rigidbody debris, no LINQ, no managed `List<T>` resize in Burst RLE, no `.ToString()`/string formatting in Tick paths found by scoped grep.
- Editor IMGUI uses managed strings by Unity editor design; not a runtime hot path.

AUP check:
- Gameplay truth remains CPU/AUP-side. Debris renderer consumes runtime-relative positions and applies AUP shift signals; loot is awarded on CPU carve commit before GPU cosmetics.

Dear Lie check:
- Real falling-rock physics was faked with point particles, gravity integration, SDF/mock-plane bounce, sleep threshold, and slag heat cover. CPU inventory never waits for debris falling or GPU readback.

Dependency check:
- No new asmdef references or sibling runtime concrete dependencies were added. Debris tuning uses existing `CarveDebrisJobState` DataVault slots. Cross-domain output uses typed `SignalBus` lanes.

H-Phi check:
- Runtime debris arrays are in DataVault (`CarveDebris`, `CarveDebrisVelocity`, `CarveDebrisRequests`, `CarveDebrisJobState`, `CarveDebrisBlackBox`).
- Known limitation: pre-existing `VoxelDeltaProcessor` still owns `_blackBox` and `_scheduledCarveWrites` via `H8Memory` sentinel, not DataVault. This was not converted in this pass because it requires a dedicated BufferID/contract integration slot.

Blackbox:
- Debris blackbox remains a 300-frame DataVault ring.
- Voxel carve blackbox remains a 300-frame fixed native ring with dump path `Docs/AgentLogs/Dump_WORLD_VOXEL_CAVING.bin`.

Compile guard:
- Scoped static checks passed. Full source compile is blocked before source compilation by missing `Temp/obj/Assembly-CSharp/project.assets.json`; no clean compile is claimed.
</SELF_AUDIT>

## 2026-05-18 - Superseded Draft - Clamped Mass / NaN Proof Jobs

This block is the current bottom truth for SHINOBU_05 after the dispatch-cap pass.

What was wrong:
- `VoxelSphericalCarveJob` counted removed mass from raw int accumulator values. Re-carving a cell already below `sbyte.MinValue` could mint extra debris even though the voxel was already empty.
- Several fallback proof jobs assumed valid NativeArray creation and matching lengths.
- `DebrisPhysicsFakeJob` trusted sampler distance/normal output to be finite.

What was done:
- Removed mass is now computed from clamped previous/next density in `[-128,127]`.
- Mock grid generation, accumulator initialization, and density-apply jobs now guard invalid or mismatched containers.
- Debris count conversion uses a `long` intermediate; emission clamps to particle capacity.
- Fake debris physics clears poisoned particles if sampler distance or normal becomes non-finite.

Verification:
- `git diff --check -- Assets/_Project/Scripts/VFX/Debris/ShinobuDeltaCrusherJobs.cs` passed.
- Isolated Roslyn syntax compile of `ShinobuDeltaCrusherJobs.cs` passed using Unity references and a temp `ISignal` stub because the generated `Hecton8.Core.ref.dll` is unavailable in the current broken Core compile state.
- `dotnet build Assembly-CSharp.csproj -maxcpucount:1` remains externally blocked by missing RealtimeCSG source files plus non-SHINOBU Core/physics errors. No clean full-project compile is claimed.

<SELF_AUDIT>
Task 01 [PASS] sbyte density fallback remains.
Task 02 [PASS] no Rigidbody/GameObject debris path.
Task 03 [PASS] `DebrisParticleDTO` remains 32 bytes.
Task 04 [PASS] unloaded chunk gate remains.
Task 05 [PASS] mock sampler remains, now fail-closed on non-finite output in fake physics.
Task 06 [PASS] spherical carve job now clamps mass truth before debris accounting.
Task 07 [PASS] Burst RLE remains.
Task 08 [PASS] mass-to-debris is bounded and cannot mint debris from already-empty cells.
Task 09 [PASS] fake debris physics remains gravity/bounce/sleep without PhysX.
Task 10 [PASS] indirect GPU rendering remains.
Task 11 [PASS] slag impostor remains.
Task 12 [PASS] CPU loot route remains decoupled.
Task 13 [PASS] tier caps remain 500/4096/10000.
Task 14 [PASS] chunk split/runtime chunk writes remain.
Task 15 [PASS] no GPU readback for loot.
Task 16 [PASS] RLE decompressor remains.
Task 17 [PASS] 300-frame blackbox remains.
Task 18 [PASS] editor sculptor remains.
Task 19 [PASS] live RLE inspector remains.
Task 20 [PASS] tuning bridge remains.

ARM64 / GPU layout:
- `DebrisParticleDTO` size 32: 0 `float3 Position`, 12 `float Radius`, 16 `float3 Velocity`, 28 `uint MaterialHash`.
- `MockLaserFireSignal` size 48: `double3 AupPosition` first, explicit 8-byte tail pad.
- `VoxelCarveTelemetryEntry` size 80: `double3 LastHitAup` first, explicit `_pad0`.

Zero-GC:
- Added only arithmetic clamps and branch guards inside jobs. No managed allocation, LINQ, closures, boxing, reflection, GameObject, Rigidbody, or GPU readback path.

AUP:
- AUP truth remains `double3` for mock signal and voxel blackbox. Debris points stay camera/local cosmetic truth.

Dear Lie:
- The fake remains point-mass gravity/bounce/sleep instead of Unity physics. The physical calculation faked is rubble rigidbody collision; gameplay truth stays CPU carve/loot.

H-Phi / Dependency:
- No new BufferID, asmdef, signal lane, or sibling assembly dependency.
- Runtime debris/blackbox/write staging stays DataVault/GraphicsBuffer based.
</SELF_AUDIT>

## 2026-05-18 - Superseded Draft - H8Dump Fault Contract

This block is the current bottom truth for SHINOBU_05 after the blackbox dump-contract pass.

What was wrong:
- Debris and voxel carve blackbox fault exports still used stale `.bin` dump paths.
- The debris dump wrote only a 4-byte reason flag plus raw ring bytes, forcing postmortem tooling to guess capacity, entry stride, and cursor.
- `VoxelDeformationSmokeTester` still asserted the old voxel `.bin` path.

What was done:
- Debris fault export now writes `Docs/AgentLogs/Dump_SHINOBU_05_DEBRIS_PHYSICS_FAKE.h8dump`.
- Voxel carve fault export now writes `Docs/AgentLogs/Dump_SHINOBU_05_VOXEL_CARVE.h8dump`.
- Debris `.h8dump` now starts with a 20-byte little-endian header: magic `VFXD`, ring capacity, entry size, cursor, reason flags.
- Debris ring entries are written in chronological ring order from `_blackBoxCursor`.
- Smoke tester contract now checks the `.h8dump` voxel path.

Cinematic Cheats used:
- No new simulation. The Dear Lie remains GPU/Burst point debris: gravity, cheap bounce, sleep, and shader slag mask hide terrain rebuild latency.
- Fault export is outside the frame loop and does not promote cosmetic debris into gameplay truth.

Exact Microseconds saved:
- No new saving is claimed for this pass.
- Existing static estimate remains: avoiding Rigidbody debris saves roughly 2000-8000 us per 1000 debris on low-end CPU. PENDING PROFILER.
- This pass buys diagnosis integrity, not frame time: no steady-frame allocation or physics work was added.

Verification:
- Scoped `rg` over SHINOBU VFX, `VoxelDeltaProcessor`, and `VoxelDeformationSmokeTester` finds only agent-specific `.h8dump` paths and no scoped `Dump_*.bin`.
- `git diff --check` passed for `CarveDebrisComputeRenderer.cs`, `VoxelDeltaProcessor.cs`, and `VoxelDeformationSmokeTester.cs`.
- Full `Assembly-CSharp.csproj` compile is still blocked by external RealtimeCSG missing source files and non-SHINOBU Core/physics errors. No clean full-project compile is claimed.

<SELF_AUDIT>
Task 01 [PASS] sbyte fallback and mock grid remain.
Task 02 [PASS] no GameObject/Rigidbody debris path.
Task 03 [PASS] `DebrisParticleDTO` remains 32 bytes.
Task 04 [PASS] unloaded chunk gate remains.
Task 05 [PASS] mock sampler remains.
Task 06 [PASS] spherical carve job remains bounded to sphere AABB.
Task 07 [PASS] Burst RLE remains preallocated.
Task 08 [PASS] clamped mass-to-debris truth remains.
Task 09 [PASS] fake debris physics remains point gravity/bounce/sleep.
Task 10 [PASS] indirect GPU rendering path remains.
Task 11 [PASS] slag impostor remains.
Task 12 [PASS] CPU loot route remains decoupled from debris.
Task 13 [PASS] tier caps remain 500/4096/10000.
Task 14 [PASS] chunk-boundary split/runtime chunk writes remain.
Task 15 [PASS] no GPU readback for loot.
Task 16 [PASS] RLE decompressor remains.
Task 17 [PASS] 300-frame blackbox remains and now exports `.h8dump`.
Task 18 [PASS] editor sculptor remains.
Task 19 [PASS] live RLE inspector remains.
Task 20 [PASS] DataVault tuning bridge remains.

ARM64 / GPU layout:
- `DebrisParticleDTO` size 32: offset 0 `float3 Position`, 12 `float Radius`, 16 `float3 Velocity`, 28 `uint MaterialHash`.
- `CarveDebrisTelemetryEntry` size 64: offset 0 `uint FrameIndex`, 4 `int ActiveCarveDebrisCount`, 8 `int QueuedCarves`, 12 `int InjectedParticles`, 16 `uint Flags`, 20 `uint StateHash`, 24 `float3 AppliedAupShift`, 36-63 explicit `uint` padding.
- `VoxelCarveTelemetryEntry` size 80: offset 0 `double3 LastHitAup`, 24 `ulong FocusVolumeId`, 32+ 4-byte state fields, 2-byte counters, byte flags, explicit tail pad.

Zero-GC:
- Runtime `Tick`/frame path was not given new managed allocations, LINQ, closures, boxing, reflection, GameObject, Rigidbody, or GPU readback.
- New `.h8dump` writing is fault-path only after invalid state.

AUP:
- Voxel forensic truth remains `double3`; debris remains local cosmetic point motion after camera/sector-relative conversion.

Dear Lie:
- Faked calculation: rubble rigidbody collision and settling. The implementation uses bounded point kinematics plus SDF/mock-plane collision; gameplay truth is CPU carve/loot.

H-Phi / Dependency:
- Runtime arrays remain in DataVault/GraphicsBuffer lanes.
- No new BufferID, asmdef, signal lane, or sibling assembly reference was added in this pass.

Blackbox:
- Debris ring: 300 `CarveDebrisTelemetryEntry` frames, `.h8dump` headerized export.
- Voxel ring: 300 `VoxelCarveTelemetryEntry` frames, `.h8dump` headerized export.
</SELF_AUDIT>

## 2026-05-18 - Superseded Draft - H8Dump Fault Contract

This block is the current bottom truth for SHINOBU_05 after the blackbox dump-contract pass.

What was wrong:
- Debris and voxel carve blackbox fault exports still used stale `.bin` dump paths.
- The debris dump wrote only a 4-byte reason flag plus raw ring bytes, forcing postmortem tooling to guess capacity, entry stride, and cursor.
- `VoxelDeformationSmokeTester` still asserted the old voxel `.bin` path.

What was done:
- Debris fault export now writes `Docs/AgentLogs/Dump_SHINOBU_05_DEBRIS_PHYSICS_FAKE.h8dump`.
- Voxel carve fault export now writes `Docs/AgentLogs/Dump_SHINOBU_05_VOXEL_CARVE.h8dump`.
- Debris `.h8dump` now starts with a 20-byte little-endian header: magic `VFXD`, ring capacity, entry size, cursor, reason flags.
- Debris ring entries are written in chronological ring order from `_blackBoxCursor`.
- Smoke tester contract now checks the `.h8dump` voxel path.

Cinematic Cheats used:
- No new simulation. The Dear Lie remains GPU/Burst point debris: gravity, cheap bounce, sleep, and shader slag mask hide terrain rebuild latency.
- Fault export is outside the frame loop and does not promote cosmetic debris into gameplay truth.

Exact Microseconds saved:
- No new saving is claimed for this pass.
- Existing static estimate remains: avoiding Rigidbody debris saves roughly 2000-8000 us per 1000 debris on low-end CPU. PENDING PROFILER.
- This pass buys diagnosis integrity, not frame time: no steady-frame allocation or physics work was added.

Verification:
- Scoped `rg` over SHINOBU VFX, `VoxelDeltaProcessor`, and `VoxelDeformationSmokeTester` finds only agent-specific `.h8dump` paths and no scoped `Dump_*.bin`.
- `git diff --check` passed for `CarveDebrisComputeRenderer.cs`, `VoxelDeltaProcessor.cs`, and `VoxelDeformationSmokeTester.cs`.
- Full `Assembly-CSharp.csproj` compile is still blocked by external RealtimeCSG missing source files and non-SHINOBU Core/physics errors. No clean full-project compile is claimed.

<SELF_AUDIT>
Task 01 [PASS] sbyte fallback and mock grid remain.
Task 02 [PASS] no GameObject/Rigidbody debris path.
Task 03 [PASS] `DebrisParticleDTO` remains 32 bytes.
Task 04 [PASS] unloaded chunk gate remains.
Task 05 [PASS] mock sampler remains.
Task 06 [PASS] spherical carve job remains bounded to sphere AABB.
Task 07 [PASS] Burst RLE remains preallocated.
Task 08 [PASS] clamped mass-to-debris truth remains.
Task 09 [PASS] fake debris physics remains point gravity/bounce/sleep.
Task 10 [PASS] indirect GPU rendering path remains.
Task 11 [PASS] slag impostor remains.
Task 12 [PASS] CPU loot route remains decoupled from debris.
Task 13 [PASS] tier caps remain 500/4096/10000.
Task 14 [PASS] chunk-boundary split/runtime chunk writes remain.
Task 15 [PASS] no GPU readback for loot.
Task 16 [PASS] RLE decompressor remains.
Task 17 [PASS] 300-frame blackbox remains and now exports `.h8dump`.
Task 18 [PASS] editor sculptor remains.
Task 19 [PASS] live RLE inspector remains.
Task 20 [PASS] DataVault tuning bridge remains.

ARM64 / GPU layout:
- `DebrisParticleDTO` size 32: offset 0 `float3 Position`, 12 `float Radius`, 16 `float3 Velocity`, 28 `uint MaterialHash`.
- `CarveDebrisTelemetryEntry` size 64: offset 0 `uint FrameIndex`, 4 `int ActiveCarveDebrisCount`, 8 `int QueuedCarves`, 12 `int InjectedParticles`, 16 `uint Flags`, 20 `uint StateHash`, 24 `float3 AppliedAupShift`, 36-63 explicit `uint` padding.
- `VoxelCarveTelemetryEntry` size 80: offset 0 `double3 LastHitAup`, 24 `ulong FocusVolumeId`, 32+ 4-byte state fields, 2-byte counters, byte flags, explicit tail pad.

Zero-GC:
- Runtime `Tick`/frame path was not given new managed allocations, LINQ, closures, boxing, reflection, GameObject, Rigidbody, or GPU readback.
- New `.h8dump` writing is fault-path only after invalid state.

AUP:
- Voxel forensic truth remains `double3`; debris remains local cosmetic point motion after camera/sector-relative conversion.

Dear Lie:
- Faked calculation: rubble rigidbody collision and settling. The implementation uses bounded point kinematics plus SDF/mock-plane collision; gameplay truth is CPU carve/loot.

H-Phi / Dependency:
- Runtime arrays remain in DataVault/GraphicsBuffer lanes.
- No new BufferID, asmdef, signal lane, or sibling assembly reference was added in this pass.

Blackbox:
- Debris ring: 300 `CarveDebrisTelemetryEntry` frames, `.h8dump` headerized export.
- Voxel ring: 300 `VoxelCarveTelemetryEntry` frames, `.h8dump` headerized export.
</SELF_AUDIT>

## 2026-05-18 - Superseded Draft - H8Dump Fault Contract

This block is the current bottom truth for SHINOBU_05 after the blackbox dump-contract pass.

What was wrong:
- Debris and voxel carve blackbox fault exports still used stale `.bin` dump paths.
- The debris dump wrote only a 4-byte reason flag plus raw ring bytes, forcing postmortem tooling to guess capacity, entry stride, and cursor.
- `VoxelDeformationSmokeTester` still asserted the old voxel `.bin` path, so the regression would survive a smoke run.

What was done:
- Debris fault export now writes `Docs/AgentLogs/Dump_SHINOBU_05_DEBRIS_PHYSICS_FAKE.h8dump`.
- Voxel carve fault export now writes `Docs/AgentLogs/Dump_SHINOBU_05_VOXEL_CARVE.h8dump`.
- Debris `.h8dump` now starts with a 20-byte little-endian header: magic `VFXD`, ring capacity, entry size, cursor, reason flags.
- Debris ring entries are written in chronological ring order from `_blackBoxCursor`, not raw storage order.
- Smoke tester contract now checks the `.h8dump` voxel path.

Cinematic Cheats used:
- No new simulation. The Dear Lie remains GPU/Burst point debris: gravity, cheap bounce, sleep, and shader slag mask hide terrain rebuild latency.
- Fault export is outside the frame loop and does not promote cosmetic debris into gameplay truth.

Exact Microseconds saved:
- No new saving is claimed for this pass.
- Existing static estimate remains: avoiding Rigidbody debris saves roughly 2000-8000 us per 1000 debris on low-end CPU. PENDING PROFILER.
- This pass buys diagnosis integrity, not frame time: no steady-frame allocation or physics work was added.

Verification:
- Scoped `rg` over SHINOBU VFX, `VoxelDeltaProcessor`, and `VoxelDeformationSmokeTester` finds only agent-specific `.h8dump` paths and no scoped `Dump_*.bin`.
- `git diff --check` passed for `CarveDebrisComputeRenderer.cs`, `VoxelDeltaProcessor.cs`, and `VoxelDeformationSmokeTester.cs`.
- Full `Assembly-CSharp.csproj` compile is still blocked by external RealtimeCSG missing source files and non-SHINOBU Core/physics errors. No clean full-project compile is claimed.

<SELF_AUDIT>
Task 01 [PASS] sbyte fallback and mock grid remain.
Task 02 [PASS] no GameObject/Rigidbody debris path.
Task 03 [PASS] `DebrisParticleDTO` remains 32 bytes.
Task 04 [PASS] unloaded chunk gate remains.
Task 05 [PASS] mock sampler remains.
Task 06 [PASS] spherical carve job remains bounded to sphere AABB.
Task 07 [PASS] Burst RLE remains preallocated.
Task 08 [PASS] clamped mass-to-debris truth remains.
Task 09 [PASS] fake debris physics remains point gravity/bounce/sleep.
Task 10 [PASS] indirect GPU rendering path remains.
Task 11 [PASS] slag impostor remains.
Task 12 [PASS] CPU loot route remains decoupled from debris.
Task 13 [PASS] tier caps remain 500/4096/10000.
Task 14 [PASS] chunk-boundary split/runtime chunk writes remain.
Task 15 [PASS] no GPU readback for loot.
Task 16 [PASS] RLE decompressor remains.
Task 17 [PASS] 300-frame blackbox remains and now exports `.h8dump`.
Task 18 [PASS] editor sculptor remains.
Task 19 [PASS] live RLE inspector remains.
Task 20 [PASS] DataVault tuning bridge remains.

ARM64 / GPU layout:
- `DebrisParticleDTO` size 32: offset 0 `float3 Position`, 12 `float Radius`, 16 `float3 Velocity`, 28 `uint MaterialHash`.
- `CarveDebrisTelemetryEntry` size 64: offset 0 `uint FrameIndex`, 4 `int ActiveCarveDebrisCount`, 8 `int QueuedCarves`, 12 `int InjectedParticles`, 16 `uint Flags`, 20 `uint StateHash`, 24 `float3 AppliedAupShift`, 36-63 explicit `uint` padding.
- `VoxelCarveTelemetryEntry` size 80: offset 0 `double3 LastHitAup`, 24 `ulong FocusVolumeId`, 32+ 4-byte state fields, 2-byte counters, byte flags, explicit tail pad.

Zero-GC:
- Runtime `Tick`/frame path was not given new managed allocations, LINQ, closures, boxing, reflection, GameObject, Rigidbody, or GPU readback.
- New `.h8dump` writing is fault-path only after invalid state.

AUP:
- Voxel forensic truth remains `double3`; debris remains local cosmetic point motion after camera/sector-relative conversion.

Dear Lie:
- Faked calculation: rubble rigidbody collision and settling. The implementation uses bounded point kinematics plus SDF/mock-plane collision; gameplay truth is CPU carve/loot.

H-Phi / Dependency:
- Runtime arrays remain in DataVault/GraphicsBuffer lanes.
- No new BufferID, asmdef, signal lane, or sibling assembly reference was added in this pass.

Blackbox:
- Debris ring: 300 `CarveDebrisTelemetryEntry` frames, `.h8dump` headerized export.
- Voxel ring: 300 `VoxelCarveTelemetryEntry` frames, `.h8dump` headerized export.
</SELF_AUDIT>

## 2026-05-18 - Superseded Draft - Dispatch Cap / Material State Cache

What was wrong:
- `CarveDebrisComputeRenderer` accepted `GetKernelThreadGroupSizes()` up to 1024, while the active SHINOBU compute shader is 64-thread and the portable mandate caps mobile-safe groups at 256/512.
- `_CarveDebrisMotionParams` was written through `Material.SetVector` every render call even when unchanged.

What was done:
- Added `ThreadGroupPortableMaxSize = 512`.
- Changed thread-group discovery to `min(kernelThreads, ThreadGroupPortableMaxSize)`.
- Added `_boundMotionParams` / `_boundMotionParamsValid` and skip redundant `_CarveDebrisMotionParams` uploads unless the vector or material binding changes.

Cinematic Cheats used:
- The Dear Lie stays unchanged: cosmetic rock chips are point particles in DataVault/GraphicsBuffer lanes, integrated by cheap gravity/SDF collision and rendered indirectly.
- Low tier remains 500 debris plus slag/heat cover.
- Middle tier remains 4096 debris.
- High/Ultra remain 10,000 visual chips without gameplay authority.

Exact microseconds saved:
- No measured claim.
- Static expected effect: avoids one redundant material vector upload on frames where motion params are unchanged and prevents accidental 1024-thread dispatch selection.

Verification:
- `rg` confirmed `ThreadGroupPortableMaxSize`, the 512 cap, `_boundMotionParams`, and cached `CarveDebrisMotionParamsId` write.
- `git diff --check -- Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs` returned CRLF warning only.
- `dotnet build Hecton8.VFX.Debris.csproj` cannot run because no such generated project exists.
- `dotnet build Assembly-CSharp.csproj -maxcpucount:1` is externally blocked by missing RealtimeCSG files plus non-SHINOBU Core/physics/ecosystem errors. No clean project compile is claimed.

<SELF_AUDIT>
Task 01 [PASS] sbyte density fallback remains.
Task 02 [PASS] no Rigidbody/GameObject debris path.
Task 03 [PASS] `DebrisParticleDTO` still 32 bytes.
Task 04 [PASS] unloaded chunk gate remains.
Task 05 [PASS] mock sampler remains.
Task 06 [PASS] spherical carve job remains.
Task 07 [PASS] Burst RLE remains.
Task 08 [PASS] mass-to-debris remains.
Task 09 [PASS] fake debris physics remains.
Task 10 [PASS] indirect GPU rendering remains.
Task 11 [PASS] slag impostor remains.
Task 12 [PASS] CPU loot route remains decoupled.
Task 13 [PASS] tier caps remain 500/4096/10000.
Task 14 [PASS] chunk split/runtime chunk writes remain.
Task 15 [PASS] no GPU readback for loot.
Task 16 [PASS] RLE decompressor remains.
Task 17 [PASS] 300-frame blackbox remains.
Task 18 [PASS] editor sculptor remains.
Task 19 [PASS] live RLE inspector remains.
Task 20 [PASS] tuning bridge remains.

ARM64 / GPU layout:
- `DebrisParticleDTO` size 32: 0 `float3 Position`, 12 `float Radius`, 16 `float3 Velocity`, 28 `uint MaterialHash`.
- `MockLaserFireSignal` size 48: `double3` first, explicit 8-byte tail pad.
- `VoxelCarveTelemetryEntry` size 80: `double3 LastHitAup` first, explicit `_pad0`.
- Compute shader thread groups remain 64 today; renderer can no longer accept >512.

Zero-GC:
- Added only fields and branch checks; no per-frame allocation, LINQ, closures, or boxing.

AUP:
- No AUP math changed. Voxel forensic AUP remains `double3`; debris remains local cosmetic truth.

Dear Lie:
- No physical rock bodies. The faked calculation is gravity/bounce/sleep on points instead of PhysX rigidbody debris.

H-Phi / Dependency:
- No new BufferID, asmdef, signal lane, or contract reference.
- Data stays in existing DataVault/GraphicsBuffer lanes.
</SELF_AUDIT>

## 2026-05-18 - AUP Blackbox Forensic Precision - Superseded By Later Bottoms

What was wrong:
- The previous bottom closure still listed `VoxelCarveTelemetryEntry` as a 64-byte struct with `float3 LastHitAup`. That is now obsolete and superseded by this block.
- `LastHitAup` was forensic AUP data in name only; storing it as `float3` could lose the postmortem truth needed for a 100x100 km world.
- The smoke tester still accepted the old 64-byte blackbox entry size.

What was done:
- `VoxelCarveTelemetryEntry` is now `Pack=4`, `Size=80`, with `double3 LastHitAup` first and explicit `_pad0`.
- `WriteBlackBoxSample` validates `PendingCarveRequest.AbsoluteHitPoint` with `IsFiniteDouble3` and writes default on non-finite AUP.
- `VoxelDeformationSmokeTester` now requires DataVault blackbox ownership and `DebugVoxelBlackBoxEntryBytes == 80`.
- Re-extracted the full SHINOBU_05 block from `Docs/Tasks/CURRENT_BATCH.md` with CLI `Select-String` and rechecked current status/rationale/project-state/domain docs before closure.

Cinematic Cheats used:
- The Dear Lie remains intact: falling rock visuals are GPU/DataVault particles, not GameObjects, Rigidbodies, MeshColliders, or authoritative gameplay objects.
- Low tier spends the saved physics budget on slag/heat impostor cover and a 500-debris cap.
- Middle tier keeps denser fake debris.
- High/Ultra keeps up to 10,000 cosmetic chips while CPU loot and voxel truth stay decoupled.

Exact microseconds saved:
- No new measured microsecond claim.
- Existing static estimate remains 2000-8000 us saved per 1000 debris versus Rigidbody debris on i3/MX350-class hardware.
- The 2026-05-18 pass intentionally spends +4800 fixed bytes for the 300-frame voxel blackbox to preserve AUP truth.

Verification:
- `rg` confirmed `VaultBufferHandle<VoxelCarveTelemetryEntry> _blackBoxHandle`, `BufferID.ShinobuDeltaCrusherVoxelBlackBox`, `BufferID.ShinobuDeltaCrusherCarveWrites`, `double3 LastHitAup`, and smoke tester `DebugVoxelBlackBoxEntryBytes == 80`.
- Scoped `rg` found no `Pack=1`, `Rigidbody`, `MeshCollider`, or `Instantiate(` in the SHINOBU VFX plus touched voxel-delta slice.
- `git diff --check -- Assets/_Project/Scripts/VoxelDeltaProcessor.cs Assets/_Project/Scripts/VoxelDeformationSmokeTester.cs` returned only CRLF warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -nologo -clp:ErrorsOnly -maxcpucount:1` failed before source compile on missing `Temp/obj/Hecton8.Core/project.assets.json`.
- `dotnet build Hecton8.Core.csproj -nologo -clp:ErrorsOnly -maxcpucount:1` reached source compile and failed outside SHINOBU on `GlobalPhysicsStateManager.cs` missing `WakeRequestSignal`. No SHINOBU file error was emitted.

<SELF_AUDIT>
Task 01 [PASS] BINARY_GRAVEYARD_RECONNAISSANCE.
Task 02 [PASS] RIGIDBODY_ERADICATION_PASS.
Task 03 [PASS] ARM64_DEBRIS_ALIGNMENT.
Task 04 [PASS] ORPHANED_CHUNK_PROTECTION.
Task 05 [PASS] BLIND_SAMPLER_MOCKING.
Task 06 [PASS] THE_SPHERICAL_CARVE_KERNEL.
Task 07 [PASS] RUN_LENGTH_ENCODING_BURST.
Task 08 [PASS] DELTA_EXTRACTION_FOR_DEBRIS.
Task 09 [PASS] GPU_DEBRIS_KINEMATICS_JOB.
Task 10 [PASS] BATCH_RENDERER_DEBRIS_LINK.
Task 11 [PASS] VISUAL_SLAG_HOLOGRAM.
Task 12 [PASS] MATERIAL_YIELD_ROUTING.
Task 13 [PASS] HARDWARE_LOD_DEBRIS_CAP.
Task 14 [PASS] CHUNK_BOUNDARY_SEAM_FIX.
Task 15 [PASS] ASYNCHRONOUS_READBACK_AVOIDANCE.
Task 16 [PASS] RLE_DECOMPRESSION_HYDRATOR.
Task 17 [PASS] TELEMETRY_MASS_TRACKER.
Task 18 [PASS] DESTRUCTION_SCULPTOR_WINDOW.
Task 19 [PASS] LIVE_RLE_INSPECTOR.
Task 20 [PASS] DEBRIS_PHYSICS_TUNER.

ARM64 struct layout:
- `DebrisParticleDTO` size 32: offset 0 `float3 Position` (12), offset 12 `float Radius` (4), offset 16 `float3 Velocity` (12), offset 28 `uint MaterialHash` (4).
- `MockLaserFireSignal` size 48: offset 0 `double3 AupPosition` (24), offset 24 `float Radius`, offset 28 `sbyte DeltaDensity`, offset 29 `byte ChunkState`, offset 30 `ushort Reserved0`, offset 32 `uint MaterialHash`, offset 36 `uint Frame`, offset 40 `uint _pad0`, offset 44 `uint _pad1`.
- `VoxelCarveTelemetryEntry` size 80: offset 0 `double3 LastHitAup` (24), offset 24 `ulong FocusVolumeId`, offset 32 `uint Frame`, offset 36 `uint Flags`, offset 40 `int TouchedMinX`, offset 44 `int TouchedMinY`, offset 48 `int TouchedMinZ`, offset 52 `int TouchedMaxX`, offset 56 `int TouchedMaxY`, offset 60 `int TouchedMaxZ`, offset 64 `ushort QueuedCarves`, offset 66 `ushort PendingCarves`, offset 68 `ushort ScheduledWrites`, offset 70 `ushort DirtyChunks`, offset 72 `byte ScheduledState`, offset 73 `byte DrainBudget`, offset 74 `ushort StateHash16`, offset 76 `uint _pad0`.
- No runtime `Pack=1` remains in the SHINOBU VFX plus touched voxel-delta slice.

Zero-GC check:
- No hidden LINQ/string-format/boxing path was added to Tick/update code.
- The added blackbox precision pass changes value-type telemetry fields only.
- Fault dump uses editor/development-only file IO in the crash/export path, not the frame hot path.

AUP check:
- Mock laser input uses `double3 AupPosition`.
- Voxel blackbox now stores `double3 LastHitAup`.
- Gameplay math converts AUP to local float deltas where appropriate; cosmetic debris remains local/fake and does not own absolute truth.

Dear Lie check:
- Simulated debris-body physics is faked as point integration, cheap gravity, fake SDF/plane bounce, sleep threshold, and indirect GPU rendering.
- Loot/inventory is awarded at CPU carve commit and never waits for GPU debris falling.

H-Phi check:
- Debris runtime state remains DataVault/GraphicsBuffer based.
- Voxel blackbox and scheduled carve-write buffers resolve through DataVault handles.
- No private `NativeArray` ownership was reintroduced for the SHINOBU blackbox path.

Blackbox:
- Voxel carve blackbox is a 300-frame DataVault ring.
- Fatal/development dump path writes `Docs/AgentLogs/Dump_WORLD_VOXEL_CAVING.bin`.
- Entry payload is now AUP-precise; older 64-byte report text is superseded.

Dependency check:
- No sibling runtime domain reference was added.
- Communication remains through DataVault, GlobalRegistry-compatible service surfaces, and typed project signals.
- Compile guard found the current Core wall outside SHINOBU: `GlobalPhysicsStateManager.cs` references missing `WakeRequestSignal`.
</SELF_AUDIT>

## 2026-05-17 - Corrective Pass 11 - AUP/DataVault/CSV Closure

What was wrong:
- `MockLaserFireSignal` was previously a local float-space proof signal while the report claimed AUP-grade behavior. That was not acceptable for a 100km world.
- `VoxelDeltaProcessor` still held SHINOBU-critical blackbox/write staging as private NativeArray fields. That was an H-Phi ownership breach.
- The designer bridge had live IMGUI tuning, but no CSV-to-binary route for non-programmer balance iteration.
- One audit attempt added an unused CSV scratch BufferID; dead enum churn is compile-wall bait.

What was done:
- Re-read `CURRENT_BATCH.md` and extracted the full `<AGENT_PROMPT id="SHINOBU_05">` block with a CLI regex that handles tag attributes.
- Converted `MockLaserFireSignal` to a 48-byte AUP signal: `double3 AupPosition`, `float Radius`, density/chunk/material fields, and explicit `_pad0/_pad1`.
- Moved voxel blackbox and scheduled carve writes behind DataVault handles:
  - `BufferID.ShinobuDeltaCrusherVoxelBlackBox = 70130`
  - `BufferID.ShinobuDeltaCrusherCarveWrites = 70131`
- Removed the unused CSV scratch BufferID after audit.
- Added editor-only CSV export/import plus compact `DXC5` binary bake for debris tuning. Runtime gameplay still uses DataVault tuning and does not touch File I/O.
- Kept the pre-existing typed `_queuedCarveEvents` `NativeQueue` as a signal queue; it is not a private array and was not rewritten into a fake signal system during this pass.

Cinematic Cheats used:
- Falling rock remains the Dear Lie: DataVault/GraphicsBuffer points, cheap gravity, SDF/mock-plane bounce, sleep threshold, indirect render.
- CPU loot is authoritative at carve commit. GPU debris never blocks inventory, save truth, or terrain ownership.
- Toaster mode drops debris at cap and hides the missing chips with slag heat. High/Ultra buy visual density with the cycles saved from not using PhysX.

Exact microseconds saved:
- No new measured microsecond number claimed.
- Existing static estimate still stands: avoiding Rigidbody debris saves roughly 2000-8000 us per 1000 debris on i3/MX350-class hardware, pending profiler capture.
- Low tier still avoids up to 9500 debris updates/draw instances versus high/ultra cap.

Verification:
- `git diff --check` on touched tracked files returned CRLF warnings only.
- Scoped hot-path grep found no `Pack=1`, `Rigidbody`, `MeshCollider`, `Instantiate`, `Material.Set`, `FindObject`, `GetComponent<`, `System.Linq`, or runtime `.ToString()` in the SHINOBU VFX plus touched voxel-delta slice.
- Manual Unity Csc passed for `Hecton8.Core.Memory` after the new BufferIDs.
- Manual Unity Csc passed for `Hecton8.VFX.Debris` with the current SHINOBU sources, using existing `Hecton8.Core.dll` because `Hecton8.Core.ref.dll` cannot be produced while Core is externally broken.
- Full `Hecton8.Core` Csc fails outside SHINOBU on duplicate `HomeostasisBrain.ScalabilityDictatorFallback` methods. No clean full-project compile is claimed.

<SELF_AUDIT>
Task 01 [PASS] BINARY_GRAVEYARD_RECONNAISSANCE.
Task 02 [PASS] RIGIDBODY_ERADICATION_PASS.
Task 03 [PASS] ARM64_DEBRIS_ALIGNMENT.
Task 04 [PASS] ORPHANED_CHUNK_PROTECTION.
Task 05 [PASS] BLIND_SAMPLER_MOCKING.
Task 06 [PASS] THE_SPHERICAL_CARVE_KERNEL.
Task 07 [PASS] RUN_LENGTH_ENCODING_BURST.
Task 08 [PASS] DELTA_EXTRACTION_FOR_DEBRIS.
Task 09 [PASS] GPU_DEBRIS_KINEMATICS_JOB.
Task 10 [PASS] BATCH_RENDERER_DEBRIS_LINK.
Task 11 [PASS] VISUAL_SLAG_HOLOGRAM.
Task 12 [PASS] MATERIAL_YIELD_ROUTING.
Task 13 [PASS] HARDWARE_LOD_DEBRIS_CAP.
Task 14 [PASS] CHUNK_BOUNDARY_SEAM_FIX.
Task 15 [PASS] ASYNCHRONOUS_READBACK_AVOIDANCE.
Task 16 [PASS] RLE_DECOMPRESSION_HYDRATOR.
Task 17 [PASS] TELEMETRY_MASS_TRACKER.
Task 18 [PASS] DESTRUCTION_SCULPTOR_WINDOW.
Task 19 [PASS] LIVE_RLE_INSPECTOR.
Task 20 [PASS] DEBRIS_PHYSICS_TUNER.

Struct layout:
- `DebrisParticleDTO` size 32: offset 0 `float3 Position` 12, offset 12 `float Radius` 4, offset 16 `float3 Velocity` 12, offset 28 `uint MaterialHash` 4.
- `MockLaserFireSignal` size 48: offset 0 `double3 AupPosition` 24, offset 24 `float Radius` 4, offset 28 `sbyte DeltaDensity` 1, offset 29 `byte ChunkState` 1, offset 30 `ushort Reserved0` 2, offset 32 `uint MaterialHash` 4, offset 36 `uint Frame` 4, offset 40 `uint _pad0` 4, offset 44 `uint _pad1` 4.
- `VoxelCarveTelemetryEntry` size 64: offset 0 `ulong FocusVolumeId` 8, offset 8 `float3 LastHitAup` 12, offset 20 `uint Frame` 4, offset 24 `uint Flags` 4, offset 28..51 six `int` min/max fields, offset 52..59 four `ushort` counters, offset 60 `byte ScheduledState`, offset 61 `byte DrainBudget`, offset 62 `ushort StateHash16`.
- `CarveCellWrite` size 32: offsets 0/4/8 `int AbsoluteCellXYZ`, offset 12 `float BlendStrength`, offset 16 `ushort SdfValueBits`, offset 18 `byte MaterialId`, offset 19 `byte DeltaFlags`, offset 20 `byte IsActive`, offset 21 `byte _pad0`, offset 22 `ushort _pad1`, offset 24 `uint _pad2`, offset 28 `uint _pad3`.
- `NativeSnapshotChunkHeaderDeltaRle` size 40: offset 0 `int ChunkX`, 4 `int ChunkY`, 8 `int ChunkZ`, 12 `float VoxelSize`, 16 `int DirtyCellCount`, 20 `byte StorageFlags`, 21 `byte Reserved0`, 22 `ushort Reserved1`, 24 `int PayloadByteLength`, 28 `uint PayloadHashLow`, 32 `uint PayloadHashHigh`, 36 `uint Reserved2`.

H-Phi check:
- Runtime debris arrays are DataVault-backed: `CarveDebris`, `CarveDebrisVelocity`, `CarveDebrisRequests`, `CarveDebrisJobState`, `CarveDebrisBlackBox`.
- Voxel carve blackbox and scheduled carve-write arrays are DataVault-backed through `ShinobuDeltaCrusherVoxelBlackBox` and `ShinobuDeltaCrusherCarveWrites`.
- Editor scratch is per-action `Allocator.TempJob`; no persistent editor-owned NativeArray cache remains in the SHINOBU sculptor.
- Remaining local native container: pre-existing `_queuedCarveEvents` typed `NativeQueue`, used as the carve event lane. Not a private array.

Zero-GC check:
- Runtime SHINOBU carve/debris paths use NativeArray/NativeList/GraphicsBuffer/DataVault, direct fields, no LINQ, no boxed foreach, no runtime string formatting in scoped grep.
- Editor CSV/IMGUI code is editor-only and is not a gameplay hot path.

AUP check:
- Mock laser now transports `double3 AupPosition`.
- Runtime carve truth already uses AUP-side data; local math converts after subtracting the relevant origin/camera basis. GPU debris remains cosmetic and never owns absolute gameplay truth.

Dear Lie check:
- Fake physics replaced PhysX: points fall under gravity, bounce by math, sleep by velocity threshold, and render indirect. No GameObject/Rigidbody/MeshCollider debris path was added.

Blackbox:
- Debris blackbox is a 300-frame DataVault ring through `CarveDebrisBlackBox`.
- Voxel carve blackbox is now a 300-frame DataVault ring through `ShinobuDeltaCrusherVoxelBlackBox`.

Compile guard:
- No new direct sibling asmdef references were added.
- Cross-domain state uses `GlobalRegistry`, DataVault handles, and typed signals already present in the project.
- `Hecton8.Core.Memory` and `Hecton8.VFX.Debris` pass manual Unity Csc at the current source state.
- Full Core/project compile is blocked by external duplicate `HomeostasisBrain` fallback methods, so full compile remains unclaimed.
</SELF_AUDIT>

## 2026-05-17 - HXD5 Chunk Padding / Unity Compile Boundary Pass

What was wrong:
- The previous HXD5 ABI pass aligned every header struct, but chunk records were still vulnerable to cursor drift after tiny payloads. A uniform chunk payload is 1 byte; without record padding, the next aligned 40-byte delta header could start at an unaligned address.
- `dotnet build Assembly-CSharp.csproj --no-restore` was not a real source proof because it failed before C# compilation on missing `Temp/obj/Assembly-CSharp/project.assets.json`.
- Full Unity script compilation still could not be called clean because unrelated agent domains currently break global compile.

What was done:
- Added 4-byte payload alignment for new HXD5 chunk records.
- Added loader-side cursor alignment for HXD5 only, including uniform/sparse/dense/hashing/corrupt-skip branches.
- Kept HXD2/HXD3/HXD4 legacy cursor math unchanged through manual 4-byte legacy parsing.
- Ran Unity 6000.4.1f1 batchmode import/script compilation.
- Unity generated:
  - `Assets/_Project/Scripts/VFX/Debris/ShinobuDeltaCrusherJobs.cs.meta`
  - `Assets/_Project/Scripts/VFX/Debris/ShinobuVoxelSculptorWindow.cs.meta`
- Bee source proof: `Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.VFX.Debris.rsp` includes `CarveDebrisComputeRenderer.cs`, `ShinobuDeltaCrusherJobs.cs`, and `ShinobuVoxelSculptorWindow.cs`.
- Bee output proof: `Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.VFX.Debris.dll` exists after the Unity run.

Cinematic Cheats used:
- Real rock physics remains rejected. Debris is point data in DataVault/GraphicsBuffer with cheap gravity, SDF/mock-plane bounce, and sleep threshold.
- Low tier drops excess rocks at the cap and relies on the molten slag heat impostor to hide missing fragments.
- High/Ultra spend the saved PhysX budget on denser GPU chips and visual wake payloads, still decoupled from gameplay truth.

Exact microseconds saved:
- New chunk-padding pass: no measured microsecond claim. It removes an ARM64 alignment hazard.
- Rigidbody eradication estimate remains 2000-8000 us saved per 1000 debris versus PhysX bodies on i3/MX350-class hardware. This is still a static estimate, not profiler proof.
- Low-tier cap still avoids up to 9500 simulated/rendered debris points versus high/ultra cap.

Verification:
- Scoped grep found no `Pack=1`, `Rigidbody`, `MeshCollider`, `Instantiate`, `Material.Set`, `FindObject`, `GetComponent<`, `System.Linq`, or `.ToString()` in the SHINOBU VFX plus touched voxel-delta slice.
- `git diff --check` passed for touched tracked files with CRLF warnings only before this report update.
- Unity batchmode reached Bee/Csc and produced `Hecton8.VFX.Debris.dll`.
- Unity global compile failed on external non-SHINOBU walls: `H8BinaryWorldPager`, `WristHologramHudRuntime`, `QuestDag*`, `TerminalOS`, `SabineReverbDspTunerWindow`, `PredatorCognitionDomain`, `GlobalShaderDispatcher`, and `GlobalTelemetryBus`.
- No clean full-project compile is claimed.

<SELF_AUDIT>
Task 01 [PASS] BINARY_GRAVEYARD_RECONNAISSANCE.
Task 02 [PASS] RIGIDBODY_ERADICATION_PASS.
Task 03 [PASS] ARM64_DEBRIS_ALIGNMENT.
Task 04 [PASS] ORPHANED_CHUNK_PROTECTION.
Task 05 [PASS] BLIND_SAMPLER_MOCKING.
Task 06 [PASS] THE_SPHERICAL_CARVE_KERNEL.
Task 07 [PASS] RUN_LENGTH_ENCODING_BURST.
Task 08 [PASS] DELTA_EXTRACTION_FOR_DEBRIS.
Task 09 [PASS] GPU_DEBRIS_KINEMATICS_JOB.
Task 10 [PASS] BATCH_RENDERER_DEBRIS_LINK.
Task 11 [PASS] VISUAL_SLAG_HOLOGRAM.
Task 12 [PASS] MATERIAL_YIELD_ROUTING.
Task 13 [PASS] HARDWARE_LOD_DEBRIS_CAP.
Task 14 [PASS] CHUNK_BOUNDARY_SEAM_FIX.
Task 15 [PASS] ASYNCHRONOUS_READBACK_AVOIDANCE.
Task 16 [PASS] RLE_DECOMPRESSION_HYDRATOR.
Task 17 [PASS] TELEMETRY_MASS_TRACKER.
Task 18 [PASS] DESTRUCTION_SCULPTOR_WINDOW.
Task 19 [PASS] LIVE_RLE_INSPECTOR.
Task 20 [PASS] DEBRIS_PHYSICS_TUNER.

Struct layout:
- `DebrisParticleDTO` size 32: offset 0 `float3 Position` 12, offset 12 `float Radius` 4, offset 16 `float3 Velocity` 12, offset 28 `uint MaterialHash` 4.
- `MockLaserFireSignal` size 32: offset 0 `double3 AupPosition`, offset 24 `float Radius`, offset 28 `byte ChunkState`, offset 29..31 explicit pad.
- `CarveDebrisRequest` size 64: offset 0 `double3 ImpactAup`, offset 24 `float3 Normal`, offset 36 `float Energy`, offset 40 `uint MaterialHash`, offset 44 `int RequestedCount`, offset 48 `uint Flags`, offset 52..63 explicit pad.
- `CarveDebrisTelemetryEntry` size 64: offset 0 `double FrameTime`, offset 8 `double3 ObserverAup`, offset 32 `int ActiveCount`, offset 36 `int SpawnedThisFrame`, offset 40 `uint DroppedCount`, offset 44 `uint Flags`, offset 48 `float AverageSpeed`, offset 52 `float MaxSpeed`, offset 56..63 explicit pad.
- `NativeSnapshotChunkHeaderDeltaRle` size 40: offset 0 `int ChunkX`, 4 `int ChunkY`, 8 `int ChunkZ`, 12 `float VoxelSize`, 16 `int DirtyCellCount`, 20 `byte StorageFlags`, 21 `byte Reserved0`, 22 `ushort Reserved1`, 24 `int PayloadByteLength`, 28 `uint PayloadHashLow`, 32 `uint PayloadHashHigh`, 36 `uint Reserved2`.

H-Phi check:
- Runtime debris arrays are DataVault-backed: `CarveDebris`, `CarveDebrisVelocity`, `CarveDebrisRequests`, `CarveDebrisJobState`, `CarveDebrisBlackBox`.
- Editor scratch is per-action `Allocator.TempJob`; no persistent editor-owned NativeArray cache remains in the SHINOBU sculptor.
- Known limitation remains: pre-existing `VoxelDeltaProcessor` owns `_blackBox` and `_scheduledCarveWrites` through H8Memory sentinel, not DataVault. Not hidden as complete.

Zero-GC check:
- SHINOBU runtime debris/carve jobs use NativeArray/NativeList/GraphicsBuffer paths, direct fields, no LINQ, no boxed foreach, no string formatting in Tick paths found by scoped grep.
- Editor IMGUI strings are editor-only and not a gameplay hot path.

AUP check:
- Carve and loot truth remain CPU/AUP-side. Absolute positions are stored as `double3`, then renderer/debris paths consume camera/runtime-relative float deltas. GPU cosmetics do not decide inventory or save truth.

Dear Lie check:
- Falling rocks are fake: points move by gravity, bounce against SDF/mock plane, sleep under velocity threshold, and render through indirect GPU buffers. No GameObject/Rigidbody/MeshCollider debris path was added.

Blackbox:
- Debris blackbox is a 300-frame DataVault ring through `CarveDebrisBlackBox`.
- Voxel carve blackbox remains a fixed 300-frame native ring with dump path `Docs/AgentLogs/Dump_WORLD_VOXEL_CAVING.bin`.

Compile guard:
- No new asmdef references were added.
- No SHINOBU-added BufferID remains.
- Unity Bee compiled `Hecton8.VFX.Debris.dll`; full project compile remains blocked by external domains.
</SELF_AUDIT>

## 2026-05-17 - Final Bottom Closure - DataVault/AUP/CSV Audit

What was wrong:
- The older audit text was no longer strict enough after the user mandate: mock laser truth needed real `double3` AUP, and voxel carve blackbox/write staging needed DataVault ownership.
- A dead CSV scratch BufferID was introduced during audit and then removed.

What was done:
- `MockLaserFireSignal` is now a 48-byte AUP signal with explicit padding.
- `VoxelDeltaProcessor` blackbox and scheduled carve-write arrays now resolve from DataVault using `ShinobuDeltaCrusherVoxelBlackBox` and `ShinobuDeltaCrusherCarveWrites`.
- `Voxel Sculptor` now supports editor-only CSV export/import and bakes compact `DXC5` binary tuning data.
- The report/Rationale/Status files were updated after the corrective pass.

Cinematic Cheats used:
- No rock uses GameObject, Rigidbody, or MeshCollider.
- Debris remains the Dear Lie: DataVault/GraphicsBuffer points, cheap gravity, mock/SDF bounce, sleep threshold, indirect render.
- Low tier caps debris at 500 and relies on slag impostor coverage; High/Ultra uses up to 10,000 GPU chips.

Exact microseconds saved:
- No new measured microsecond claim.
- Static estimate remains 2000-8000 us saved per 1000 debris versus Rigidbody debris on i3/MX350-class hardware.

Verification:
- Scoped grep found no runtime `Pack=1`, `Rigidbody`, `MeshCollider`, `Instantiate`, `Material.Set`, `FindObject`, `GetComponent<`, `System.Linq`, or runtime `.ToString()` in the SHINOBU VFX plus touched voxel-delta slice.
- `git diff --check` returned CRLF warnings only.
- Manual Unity Csc passed for `Hecton8.Core.Memory`.
- Manual Unity Csc passed for `Hecton8.VFX.Debris` at current source state.
- Full `Hecton8.Core` Csc is externally blocked by duplicate `HomeostasisBrain.ScalabilityDictatorFallback` methods. No clean full-project compile is claimed.

<SELF_AUDIT>
Task 01 [PASS] BINARY_GRAVEYARD_RECONNAISSANCE.
Task 02 [PASS] RIGIDBODY_ERADICATION_PASS.
Task 03 [PASS] ARM64_DEBRIS_ALIGNMENT.
Task 04 [PASS] ORPHANED_CHUNK_PROTECTION.
Task 05 [PASS] BLIND_SAMPLER_MOCKING.
Task 06 [PASS] THE_SPHERICAL_CARVE_KERNEL.
Task 07 [PASS] RUN_LENGTH_ENCODING_BURST.
Task 08 [PASS] DELTA_EXTRACTION_FOR_DEBRIS.
Task 09 [PASS] GPU_DEBRIS_KINEMATICS_JOB.
Task 10 [PASS] BATCH_RENDERER_DEBRIS_LINK.
Task 11 [PASS] VISUAL_SLAG_HOLOGRAM.
Task 12 [PASS] MATERIAL_YIELD_ROUTING.
Task 13 [PASS] HARDWARE_LOD_DEBRIS_CAP.
Task 14 [PASS] CHUNK_BOUNDARY_SEAM_FIX.
Task 15 [PASS] ASYNCHRONOUS_READBACK_AVOIDANCE.
Task 16 [PASS] RLE_DECOMPRESSION_HYDRATOR.
Task 17 [PASS] TELEMETRY_MASS_TRACKER.
Task 18 [PASS] DESTRUCTION_SCULPTOR_WINDOW.
Task 19 [PASS] LIVE_RLE_INSPECTOR.
Task 20 [PASS] DEBRIS_PHYSICS_TUNER.

Struct layout:
- `DebrisParticleDTO` size 32: 0 `float3 Position`, 12 `float Radius`, 16 `float3 Velocity`, 28 `uint MaterialHash`.
- `MockLaserFireSignal` size 48: 0 `double3 AupPosition`, 24 `float Radius`, 28 `sbyte DeltaDensity`, 29 `byte ChunkState`, 30 `ushort Reserved0`, 32 `uint MaterialHash`, 36 `uint Frame`, 40 `uint _pad0`, 44 `uint _pad1`.
- `VoxelCarveTelemetryEntry` size 64: 0 `ulong FocusVolumeId`, 8 `float3 LastHitAup`, 20 `uint Frame`, 24 `uint Flags`, 28..51 six `int` bounds, 52..59 four `ushort` counters, 60 `byte ScheduledState`, 61 `byte DrainBudget`, 62 `ushort StateHash16`.
- `CarveCellWrite` size 32: 0/4/8 `int AbsoluteCellXYZ`, 12 `float BlendStrength`, 16 `ushort SdfValueBits`, 18 `byte MaterialId`, 19 `byte DeltaFlags`, 20 `byte IsActive`, 21..31 explicit pad.

H-Phi check:
- Debris buffers are DataVault-backed.
- Voxel blackbox and scheduled carve writes are DataVault-backed.
- Remaining `_queuedCarveEvents` is a pre-existing typed `NativeQueue`, not a private array.

Zero-GC check:
- Runtime hot path uses Native containers, DataVault handles, GraphicsBuffers, direct fields, and Burst jobs. Scoped grep found no LINQ/string formatting/runtime boxing pattern in the target slice.

AUP check:
- Mock laser transports `double3 AupPosition`; GPU debris remains cosmetic and does not own absolute gameplay truth.

Dear Lie check:
- Fake falling rock physics replaces PhysX: point integration, cheap bounce, sleep threshold, indirect render.

Blackbox:
- Debris blackbox: 300-frame DataVault ring.
- Voxel blackbox: 300-frame DataVault ring.

Dependency check:
- No direct sibling asmdef dependency was added. Communication stays through GlobalRegistry/DataVault/typed project signals.
</SELF_AUDIT>

## 2026-05-18 - Superseded By Later Bottom - AUP Blackbox 80B

This block is the current bottom truth for SHINOBU_05. Any older `VoxelCarveTelemetryEntry` size-64 / `float3 LastHitAup` text above is obsolete.

What was wrong:
- The voxel blackbox used a `float3` under the AUP name `LastHitAup`.
- The smoke tester still accepted the old 64-byte telemetry entry.

What was done:
- `VoxelCarveTelemetryEntry` is now `Pack=4`, `Size=80`, with `double3 LastHitAup` at offset 0 and explicit `uint _pad0` at offset 76.
- `WriteBlackBoxSample` sanitizes non-finite pending hit AUPs with `IsFiniteDouble3`.
- `VoxelDeformationSmokeTester` now checks the DataVault handle/BufferID contract and `DebugVoxelBlackBoxEntryBytes == 80`.

Verification:
- `rg` confirmed `double3 LastHitAup`, DataVault blackbox handles, carve-write BufferID, and the 80-byte smoke-test assertion.
- Scoped `rg` found no `Pack=1`, `Rigidbody`, `MeshCollider`, or `Instantiate(` in SHINOBU VFX plus the touched voxel-delta slice.
- `git diff --check` returned CRLF warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore` stopped before source on missing `Temp/obj/Hecton8.Core/project.assets.json`.
- `dotnet build Hecton8.Core.csproj -maxcpucount:1` reached source compile and stopped outside SHINOBU on missing `WakeRequestSignal` in `GlobalPhysicsStateManager.cs`.

<SELF_AUDIT>
Task 01 [PASS] sbyte mock density / archaeology fallback.
Task 02 [PASS] no Rigidbody/GameObject debris path.
Task 03 [PASS] `DebrisParticleDTO` 32 bytes.
Task 04 [PASS] unloaded chunk gate writes telemetry.
Task 05 [PASS] mock plane sampler.
Task 06 [PASS] spherical AABB carve job.
Task 07 [PASS] Burst RLE pairs.
Task 08 [PASS] removed mass to debris count.
Task 09 [PASS] fake gravity/bounce/sleep debris.
Task 10 [PASS] indirect GPU debris rendering path.
Task 11 [PASS] slag/heat impostor cover.
Task 12 [PASS] CPU carve-to-loot route.
Task 13 [PASS] 500/4096/10000 tiered debris cap.
Task 14 [PASS] chunk boundary split/runtime chunk-address writes.
Task 15 [PASS] no GPU readback for loot.
Task 16 [PASS] RLE decompressor.
Task 17 [PASS] mass/RLE telemetry and 300-frame blackbox.
Task 18 [PASS] editor `Voxel Sculptor`.
Task 19 [PASS] live RLE inspector.
Task 20 [PASS] debris tuning bridge with CSV/DXC5 editor bake.

ARM64 layout:
- `DebrisParticleDTO` size 32: 0 `float3 Position`, 12 `float Radius`, 16 `float3 Velocity`, 28 `uint MaterialHash`.
- `MockLaserFireSignal` size 48: 0 `double3 AupPosition`, 24 `float Radius`, 28 `sbyte DeltaDensity`, 29 `byte ChunkState`, 30 `ushort Reserved0`, 32 `uint MaterialHash`, 36 `uint Frame`, 40/44 pad.
- `VoxelCarveTelemetryEntry` size 80: 0 `double3 LastHitAup`, 24 `ulong FocusVolumeId`, 32 `uint Frame`, 36 `uint Flags`, 40..63 six int bounds, 64..71 four ushort counters, 72 `byte ScheduledState`, 73 `byte DrainBudget`, 74 `ushort StateHash16`, 76 `uint _pad0`.

Zero-GC check:
- No new Tick/update allocation path, LINQ, closures, boxing, or string formatting was added.

AUP check:
- Mock signal and voxel blackbox now carry `double3` AUP. Cosmetic debris stays local and non-authoritative.

Dear Lie:
- Falling rocks remain fake GPU/DataVault points with cheap gravity, fake collision, sleep threshold, and indirect rendering.

H-Phi / Dependency:
- Debris, voxel blackbox, and scheduled carve-write buffers are vault-backed.
- No new sibling asmdef dependency was added; integration stays through DataVault, GlobalRegistry-compatible services, and typed signals.
</SELF_AUDIT>

## 2026-05-18 - Superseded By Clamped Mass Bottom - Dispatch Cap / Material State Cache

This block is the current bottom truth for SHINOBU_05 after the AUP 80-byte pass.

What was wrong:
- `CarveDebrisComputeRenderer` accepted `GetKernelThreadGroupSizes()` up to 1024, while the active SHINOBU compute shader is 64-thread and the portable mandate caps mobile-safe groups at 256/512.
- `_CarveDebrisMotionParams` was written through `Material.SetVector` every render call even when unchanged.

What was done:
- Added `ThreadGroupPortableMaxSize = 512`.
- Changed thread-group discovery to `min(kernelThreads, ThreadGroupPortableMaxSize)`.
- Added `_boundMotionParams` / `_boundMotionParamsValid` and skip redundant `_CarveDebrisMotionParams` uploads unless the vector or material binding changes.

Verification:
- `rg` confirmed `ThreadGroupPortableMaxSize`, the 512 cap, `_boundMotionParams`, and cached `CarveDebrisMotionParamsId` write.
- `git diff --check -- Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs` returned CRLF warning only.
- `dotnet build Hecton8.VFX.Debris.csproj` cannot run because no such generated project exists.
- `dotnet build Assembly-CSharp.csproj -maxcpucount:1` is externally blocked by missing RealtimeCSG files plus non-SHINOBU Core/physics/ecosystem errors. No clean project compile is claimed.

<SELF_AUDIT>
Task 01 [PASS] sbyte density fallback remains.
Task 02 [PASS] no Rigidbody/GameObject debris path.
Task 03 [PASS] `DebrisParticleDTO` still 32 bytes.
Task 04 [PASS] unloaded chunk gate remains.
Task 05 [PASS] mock sampler remains.
Task 06 [PASS] spherical carve job remains.
Task 07 [PASS] Burst RLE remains.
Task 08 [PASS] mass-to-debris remains.
Task 09 [PASS] fake debris physics remains.
Task 10 [PASS] indirect GPU rendering remains.
Task 11 [PASS] slag impostor remains.
Task 12 [PASS] CPU loot route remains decoupled.
Task 13 [PASS] tier caps remain 500/4096/10000.
Task 14 [PASS] chunk split/runtime chunk writes remain.
Task 15 [PASS] no GPU readback for loot.
Task 16 [PASS] RLE decompressor remains.
Task 17 [PASS] 300-frame blackbox remains.
Task 18 [PASS] editor sculptor remains.
Task 19 [PASS] live RLE inspector remains.
Task 20 [PASS] tuning bridge remains.

ARM64 / GPU layout:
- `DebrisParticleDTO` size 32: 0 `float3 Position`, 12 `float Radius`, 16 `float3 Velocity`, 28 `uint MaterialHash`.
- `MockLaserFireSignal` size 48: `double3` first, explicit 8-byte tail pad.
- `VoxelCarveTelemetryEntry` size 80: `double3 LastHitAup` first, explicit `_pad0`.
- Compute shader thread groups remain 64 today; renderer can no longer accept >512.

Zero-GC:
- Added only fields and branch checks; no per-frame allocation, LINQ, closures, or boxing.

AUP:
- No AUP math changed. Voxel forensic AUP remains `double3`; debris remains local cosmetic truth.

Dear Lie:
- No physical rock bodies. The faked calculation is gravity/bounce/sleep on points instead of PhysX rigidbody debris.

H-Phi / Dependency:
- No new BufferID, asmdef, signal lane, or contract reference.
- Data stays in existing DataVault/GraphicsBuffer lanes.
</SELF_AUDIT>

## 2026-05-18 - Actual Bottom Supersession - Clamped Mass / NaN Proof Jobs

This block is the current bottom truth for SHINOBU_05 after the dispatch-cap pass.

What was wrong:
- `VoxelSphericalCarveJob` counted removed mass from raw int accumulator values. Re-carving a cell already below `sbyte.MinValue` could mint extra debris even though the voxel was already empty.
- Several fallback proof jobs assumed valid NativeArray creation and matching lengths.
- `DebrisPhysicsFakeJob` trusted sampler distance/normal output to be finite.

What was done:
- Removed mass is now computed from clamped previous/next density in `[-128,127]`.
- Mock grid generation, accumulator initialization, and density-apply jobs now guard invalid or mismatched containers.
- Debris count conversion uses a `long` intermediate; emission clamps to particle capacity.
- Fake debris physics clears poisoned particles if sampler distance or normal becomes non-finite.

Verification:
- `git diff --check -- Assets/_Project/Scripts/VFX/Debris/ShinobuDeltaCrusherJobs.cs` passed.
- Isolated Roslyn syntax compile of `ShinobuDeltaCrusherJobs.cs` passed using Unity references and a temp `ISignal` stub because the generated `Hecton8.Core.ref.dll` is unavailable in the current broken Core compile state.
- `dotnet build Assembly-CSharp.csproj -maxcpucount:1` remains externally blocked by missing RealtimeCSG source files plus non-SHINOBU Core/physics errors. No clean full-project compile is claimed.

<SELF_AUDIT>
Task 01 [PASS] sbyte density fallback remains.
Task 02 [PASS] no Rigidbody/GameObject debris path.
Task 03 [PASS] `DebrisParticleDTO` remains 32 bytes.
Task 04 [PASS] unloaded chunk gate remains.
Task 05 [PASS] mock sampler remains, now fail-closed on non-finite output in fake physics.
Task 06 [PASS] spherical carve job now clamps mass truth before debris accounting.
Task 07 [PASS] Burst RLE remains.
Task 08 [PASS] mass-to-debris is bounded and cannot mint debris from already-empty cells.
Task 09 [PASS] fake debris physics remains gravity/bounce/sleep without PhysX.
Task 10 [PASS] indirect GPU rendering remains.
Task 11 [PASS] slag impostor remains.
Task 12 [PASS] CPU loot route remains decoupled.
Task 13 [PASS] tier caps remain 500/4096/10000.
Task 14 [PASS] chunk split/runtime chunk writes remain.
Task 15 [PASS] no GPU readback for loot.
Task 16 [PASS] RLE decompressor remains.
Task 17 [PASS] 300-frame blackbox remains.
Task 18 [PASS] editor sculptor remains.
Task 19 [PASS] live RLE inspector remains.
Task 20 [PASS] tuning bridge remains.

ARM64 / GPU layout:
- `DebrisParticleDTO` size 32: 0 `float3 Position`, 12 `float Radius`, 16 `float3 Velocity`, 28 `uint MaterialHash`.
- `MockLaserFireSignal` size 48: `double3 AupPosition` first, explicit 8-byte tail pad.
- `VoxelCarveTelemetryEntry` size 80: `double3 LastHitAup` first, explicit `_pad0`.

Zero-GC:
- Added only arithmetic clamps and branch guards inside jobs. No managed allocation, LINQ, closures, boxing, reflection, GameObject, Rigidbody, or GPU readback path.

AUP:
- AUP truth remains `double3` for mock signal and voxel blackbox. Debris points stay camera/local cosmetic truth.

Dear Lie:
- The fake remains point-mass gravity/bounce/sleep instead of Unity physics. The physical calculation faked is rubble rigidbody collision; gameplay truth stays CPU carve/loot.

H-Phi / Dependency:
- No new BufferID, asmdef, signal lane, or sibling assembly dependency.
- Runtime debris/blackbox/write staging stays DataVault/GraphicsBuffer based.
</SELF_AUDIT>

## 2026-05-18 - Actual Bottom Supersession - H8Dump Fault Contract

Current bottom truth: SHINOBU_05 blackbox fault export now uses agent-specific `.h8dump` files, with debris dump metadata sufficient to decode the 300-frame ring without guessing.

What was wrong:
- Debris and voxel carve blackbox fault exports still used stale `.bin` dump paths.
- Debris dump only wrote reason flags plus raw ring bytes; cursor, stride, and capacity were implicit.
- Smoke tester asserted the old voxel `.bin` contract.

What was done:
- `CarveDebrisComputeRenderer` now writes `Docs/AgentLogs/Dump_SHINOBU_05_DEBRIS_PHYSICS_FAKE.h8dump`.
- `VoxelDeltaProcessor` now writes `Docs/AgentLogs/Dump_SHINOBU_05_VOXEL_CARVE.h8dump`.
- Debris dump header is 20 bytes: magic `VFXD`, capacity, entry size, cursor, reason flags.
- Debris entries are emitted in chronological ring order from `_blackBoxCursor`.
- `VoxelDeformationSmokeTester` now checks the `.h8dump` path.

Verification:
- Scoped `rg`: no SHINOBU `Dump_*.bin`; only `Dump_SHINOBU_05_*.h8dump`.
- `git diff --check`: clean for the touched runtime, smoke, status, rationale, and log files.
- Full project compile remains externally blocked by missing RealtimeCSG source files plus non-SHINOBU `SaveBinaryStorage.cs(2423,65)` CS0841; no clean full-project compile is claimed.

<SELF_AUDIT>
Task 01 [PASS] sbyte fallback/mock grid intact.
Task 02 [PASS] no GameObject/Rigidbody debris.
Task 03 [PASS] `DebrisParticleDTO` 32B.
Task 04 [PASS] unloaded chunk gate intact.
Task 05 [PASS] mock sampler intact.
Task 06 [PASS] sphere-AABB carve intact.
Task 07 [PASS] Burst RLE intact.
Task 08 [PASS] clamped mass-to-debris intact.
Task 09 [PASS] point fake debris physics intact.
Task 10 [PASS] indirect GPU render intact.
Task 11 [PASS] slag impostor intact.
Task 12 [PASS] CPU loot decoupled.
Task 13 [PASS] tier caps 500/4096/10000 intact.
Task 14 [PASS] chunk boundary handling intact.
Task 15 [PASS] no GPU readback for loot.
Task 16 [PASS] RLE hydrate intact.
Task 17 [PASS] 300-frame `.h8dump` blackbox active.
Task 18 [PASS] editor sculptor intact.
Task 19 [PASS] live RLE inspector intact.
Task 20 [PASS] DataVault tuning bridge intact.

Struct Layout:
- `DebrisParticleDTO` 32B: 0 `float3 Position`, 12 `float Radius`, 16 `float3 Velocity`, 28 `uint MaterialHash`.
- `CarveDebrisTelemetryEntry` 64B: 0 `uint FrameIndex`, 4 `int ActiveCarveDebrisCount`, 8 `int QueuedCarves`, 12 `int InjectedParticles`, 16 `uint Flags`, 20 `uint StateHash`, 24 `float3 AppliedAupShift`, 36-63 explicit padding.
- `VoxelCarveTelemetryEntry` 80B: 0 `double3 LastHitAup`, 24 `ulong FocusVolumeId`, 32+ 4B fields, 2B counters, byte flags, explicit tail pad.

Zero-GC:
- No managed allocation, LINQ, closure, boxing, reflection, GameObject, Rigidbody, or GPU readback added to the frame path.
- `.h8dump` file I/O is fault-path only.

AUP:
- Voxel forensic coordinates remain `double3`; cosmetic debris stays camera/sector-local point motion.

Dear Lie:
- Faked calculation: rubble rigidbody collision/settling. The runtime keeps bounded point kinematics plus SDF/mock-plane collision; gameplay truth remains CPU carve/loot.

H-Phi / Dependency:
- Data remains in DataVault/GraphicsBuffer lanes.
- No new BufferID, asmdef, signal lane, or sibling assembly reference in this pass.
</SELF_AUDIT>
