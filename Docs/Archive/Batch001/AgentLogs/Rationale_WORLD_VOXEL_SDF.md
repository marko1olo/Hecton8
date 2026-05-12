# Rationale_WORLD_VOXEL_SDF

Status: PENDING VERIFICATION

## Decision 0 - Mandate Selection

Problem: Optimize voxel normals and RLE without returning to CPU-heavy 6-tap trilinear smoothing or violating zero-GC native job constraints.
Solution: Use shader-side ddx/ddy normal smoothing, vertex-color AO from existing neighbor density reads, raw pointer writes only inside Burst jobs with NativeArray ownership fields, and byte/sbyte packed data where existing code permits.
Rejected Alternatives: Rejected CPU trilinear normal resampling because the batch explicitly forbids it and it increases voxel-job ALU. Rejected broad architecture rewrites because 20+ agents are working concurrently and public interface mutation is forbidden.
Scalability potential: Low uses coarse nearest-grid normals plus shader fake; Middle uses vertex AO and 2-axis triplanar; High adds stronger shader smoothing/noise; Ultra can spend saved CPU on richer material response while keeping same data layout.
Hardware Impact: Expected low-end i3/MX350 gain is preserved CPU budget from avoiding trilinear normals and sqrt-heavy carving; exact microseconds remain PENDING VERIFICATION until compile/profiler data exists.

## Decision 1 - Shader Fake Over CPU Normal Recovery

Problem: Nearest-grid normals saved CPU but produced faceted cave lighting.
Solution: Keep the coarse normal and let `Hecton_AbyssalVoxelRock.shader` perform screen-space micro-bevel smoothing with `ddx/ddy`, then layer fake cavity AO from neighbor density and 1D depth noise.
Rejected Alternatives: Rejected CPU trilinear normal reconstruction and extra SDF samples because they move cost back into the chunk rebuild path. Rejected full 3-axis triplanar because the prompt explicitly demanded deterministic 2-axis projection.
Scalability potential: Low disables high-cost math through `_HectonMathLodMode`; Middle uses baked vertex AO; High uses stronger shader smoothing/noise; Ultra can spend saved CPU on richer material response without touching mesh jobs.
Hardware Impact: Expected low-end i3/MX350 gain is 35-60 us CPU preserved per rebuilt chunk; GPU cost is bounded to visible pixels and replaces heavier mesh-side work.

## Decision 2 - RLE Uniform Flag Shrink

Problem: Uniform RLE detection was worker-threaded but still returned an int-sized run header shape.
Solution: Replace the request field with `NativeArray<byte> RleUniformFlag`, keep the detection in `VoxelDeltaUniformRunDetectJob`, and promote compacted chunks to scalar RLE state when the flag is 1.
Rejected Alternatives: Rejected main-thread `for` scan because it stalls carve completion. Rejected `NativeArray<int>` because the batch asked for a flag, not a run header.
Scalability potential: Low keeps one byte of RLE metadata; Middle/High/Ultra can still hydrate dense arrays only for non-uniform modified chunks.
Hardware Impact: Saves 3 bytes per scheduled compaction request and avoids 20-60 us of potential main-thread scan/stall on low-end silicon.

## Decision 3 - Two-Byte Uniform Save Payload

Problem: Compacted solid chunks could still be projected into dense DTO arrays, destroying the intended RLE save win.
Solution: Add `VoxelDeltaChunkDTO.StorageUniformSdfRle` and `uniformSdfValueBits`; uniform default-material compacted chunks now save only the 2-byte SDF payload and load back into `CompactedChunkState` without allocating dense cell arrays. Migration clears stale dense arrays when this storage flag is present.
Rejected Alternatives: Rejected saving a full dirty mask plus 32x32x32 SDF/material/flag arrays for uniform chunks. Rejected changing dirty overlay semantics because active overlays still need dense masks.
Scalability potential: Low devices save/load less data; Middle keeps dense deltas only where modified; High/Ultra can retain large cave volumes without save spikes.
Hardware Impact: Dense payload avoided per solid chunk is 135,166 bytes; estimated serialization/IO gain is 100-500 us per chunk depending on disk and serializer path.

## Decision 4 - Immediate Mining VFX Signal

Problem: Mesh rebuild latency can leave laser cuts visually late even when the carve job is accepted.
Solution: Publish `DebrisSpawnSignal` as soon as a subtractive non-box carve is scheduled, using AUP hit point, deterministic source hash, intensity from radius, and source flags. This decouples VFX from mesh rebuild and debris service hydration.
Rejected Alternatives: Rejected waiting until `TryCommitScheduledCarve` finishes all staged writes because that preserves the visual latency. Rejected direct VFX service coupling because the project requires signal corridors for parallel agents.
Scalability potential: Low uses a tiny queue packet and cheap glowing particle response; Middle/High can add richer consumers; Ultra can overdraw more particles without touching voxel jobs.
Hardware Impact: Event enqueue is expected under 5 us and buys back perceived responsiveness across the 2-frame meshing delay.

## Decision 5 - Compile Boundary

Problem: Full project compile currently fails before validating this task because other active agents have broken shared contracts.
Solution: Run the full compile once to capture the dependency wall, then run a focused `Assembly-CSharp.csproj` compile with project references disabled to validate this task's edited C# against current generated references.
Rejected Alternatives: Rejected fixing `GlobalSignals`, `FaunaBrain`, or `ConstructionManager` because those errors are outside WORLD_VOXEL_SDF scope and belong to other agents. Rejected reverting dirty worktree state.
Scalability potential: Low/Middle/High/Ultra unaffected; this is a coordination boundary, not runtime behavior.
Hardware Impact: Runtime impact is none. Verification impact: focused compile passed in 10.11 s with 0 warnings/errors; full compile remains blocked by dependency errors.

## OMEGA POLISH CHANGES

Problem: Final anti-bloat audit required proof that the voxel implementation did not add honest physics/math, hidden GC, or cross-domain leakage after task completion.
Solution: Re-read the OMEGA mandate, checked `Docs/Actual Domains of Project.txt`, scanned touched voxel/save files for `foreach`, string formatting, `.ToString()`, `math.sqrt`, `math.normalize`, and `new`, then ran `dotnet build Hecton8.Core.csproj -v:minimal /m:1`.
Rejected Alternatives: Rejected adding quality-tier branches around the new `DebrisSpawnSignal` path because the signal is already a tiny fixed-size struct packet and the expensive visual response belongs to downstream VFX consumers. Rejected replacing struct constructors (`new int4`, `new double3`, `new DebrisSpawnSignal`) because they are value types and do not allocate managed heap memory.
Scalability potential: Low emits only the signal and uses existing shader Math LOD gates; Middle uses vertex AO and 2-axis projection; High/Ultra can spend the saved CPU on richer VFX consumers without changing voxel jobs or save layout.
Hardware Impact: No new per-frame managed allocation found in the touched hot paths. The only new scheduled hot-path work is one `DebrisSpawnSignal` enqueue, expected under 5 us. The DTO/migration work is cold save/load path only.

Honest calculations replaced with cinematic cheats:
- CPU normal recovery remains replaced by shader `ddx/ddy` micro-bevel.
- True 3-axis triplanar remains replaced by 2-axis dominant projection.
- Euclidean carve length remains replaced by axis-weighted SDF approximation.
- Normalized debris impulse remains replaced by dominant-axis snap.
- Dense uniform chunk serialization is replaced by a 2-byte scalar RLE payload.

Math LOD audit:
- No new `math.sqrt()` or `math.normalize()` was added in the touched code.
- Existing shader smoothing is gated by `_HectonMathLodMode`.
- New VFX dispatch does not own particle count or shader cost; downstream consumers can scale by hardware tier.

Zero-GC audit:
- New hot-path signal packet uses struct/value constructors only.
- New RLE flag uses `NativeArray<byte>` persistent allocation in the existing scheduled compaction allocation path, not per frame.
- `Array.Empty<T>()` changes are DTO save/load hygiene and do not allocate new arrays.
- No new string formatting, `.ToString()`, or managed `foreach` was added by this task.

Cache/locality audit:
- RLE uniform load now avoids dense 32x32x32 native array hydration for scalar chunks.
- Compaction still scans SDF/material/flag arrays linearly.
- Existing 8-byte `VoxelModifiedCell`, 32-byte `CarveCellWrite`, and 8-byte `ChunkAddress` layouts remain intact.

Silo audit:
- Echelon 2 explicitly owns Voxel SDF Pipeline, Marching Cubes, and Voxel Carving.
- `VoxelDeltaPersistenceDTO.cs` and the voxel branch of `SaveDataMigration.cs` are justified cross-domain persistence edits because task 13 required save format changes for voxel delta RLE.
- `GlobalSignals.cs` was not edited; the voxel system uses the existing `DebrisSpawnSignal` EventBus lane.

Build health:
- Focused task validation: `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:minimal` passed with 0 warnings and 0 errors.
- Required OMEGA command: `dotnet build Hecton8.Core.csproj -v:minimal /m:1` failed with 0 warnings and 3 unrelated errors: missing `TransitionHatchMeshState` in `HabitatGraphManager` and missing `Hecton8.Physics.SyncTransforms` in `ConstructionManager`.
- Full referenced compile remains blocked by other active agents; no compiler errors were reported in `VoxelDeltaProcessor.cs`, `VoxelDeltaPersistenceDTO.cs`, or the voxel branch of `SaveDataMigration.cs`.

Final Git Diff:
- `Assets/_Project/Scripts/VoxelDeltaProcessor.cs`: RLE flag shrunk to byte, uniform native snapshot load now hydrates compacted RLE state, immediate `DebrisSpawnSignal` dispatch added, DTO uniform save path added.
- `Assets/_Project/Scripts/VoxelDeltaPersistenceDTO.cs`: storage flag and 2-byte uniform SDF payload fields added; zero-capacity state now clears stale dense arrays.
- `Assets/_Project/Scripts/SaveDataMigration.cs`: voxel delta migration preserves uniform RLE chunks and clears stale dense arrays for that storage mode.
- Diff stat for tracked code files: 3 files changed, 208 insertions, 57 deletions. Existing unrelated dirty edits in `SaveDataMigration.cs` are still present and were not reverted.
