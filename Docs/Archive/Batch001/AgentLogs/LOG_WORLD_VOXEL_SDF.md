# WORLD_VOXEL_SDF Report - 2026-05-11

Agent: VOXEL_SURGEON
Status: PENDING VERIFICATION

## What Was Wrong

- Nearest-grid voxel normals were CPU-cheap but visually faceted.
- Uniform RLE compaction still returned an int-sized header shape instead of a flag.
- Save DTO projection could serialize compacted uniform chunks as dense 32x32x32 payloads.
- Laser/mining visual feedback waited on downstream mesh/debris work instead of using the existing EventBus lane.

## What Was Done

- Preserved shader-side `ddx/ddy` micro-bevel smoothing and verified fake AO/triplanar paths.
- Converted scheduled RLE detection state to `NativeArray<byte> RleUniformFlag`.
- Added `VoxelDeltaChunkDTO.StorageUniformSdfRle` plus `ushort uniformSdfValueBits`; uniform compacted chunks now save/load as scalar RLE state instead of dense arrays.
- Updated voxel save migration to preserve uniform RLE chunks and clear stale dense arrays for that storage mode.
- Loaded native uniform RLE snapshots directly into compacted state instead of hydrating a dense dirty chunk.
- Published `DebrisSpawnSignal` immediately when a subtractive non-box carve is accepted for scheduling.

## ddx/ddy Normal Smoothing Evidence

```hlsl
float3 dpdx = ddx(positionWS);
float3 dpdy = ddy(positionWS);
half3 faceNormalWS = SafeNormalize3((half3)cross(dpdy, dpdx));
faceNormalWS = dot(faceNormalWS, coarseNormalWS) < 0.0h ? -faceNormalWS : faceNormalWS;

float3 pixelSpan = abs(dpdx) + abs(dpdy);
half bevelMask = saturate((half)((pixelSpan.x + pixelSpan.y + pixelSpan.z) * max(_ScreenSpaceNormalBevelStrength, 0.0)));
half3 smoothedNormalWS = SafeNormalize3(lerp(coarseNormalWS, faceNormalWS, bevelMask * organicWeight));
```

## Cinematic Cheats Used

- Shader `ddx/ddy` micro-bevel replaces CPU trilinear normal reconstruction.
- 2-axis dominant projection replaces full 3-axis triplanar.
- Axis-weighted SDF approximation replaces Euclidean carve length.
- Dominant-axis impulse replaces normalized debris vector.
- 2-byte scalar RLE payload replaces dense uniform chunk serialization.

## Microseconds Saved

- CPU normal recovery avoided: 35-60 us per rebuilt chunk.
- Fake AO reuse of neighbor density reads: 15-25 us per chunk avoided.
- Axis-weighted carve length: 8-20 us per large carve batch avoided.
- Pointer-backed carve writes: 5-15 us per carve job avoided.
- Pointer-backed compaction with reciprocal sampling: 30-80 us per compaction job avoided.
- Worker RLE flag avoids main-thread uniform scan: 20-60 us stall avoided.
- Reciprocal density quantization: 10-30 us per quantized chunk avoided.
- Cast-bias rounding: 8-20 us per quantized chunk avoided.
- Uniform chunk save payload: 135,166 bytes avoided per solid chunk; 100-500 us serialization/IO avoided depending on serializer and storage.
- Mining VFX signal: under 5 us enqueue cost; hides 2-frame mesh latency instead of reducing CPU time.

These are engineering estimates from code-path cost, not profiler captures. No profiler trace was available in this session.

## Verification

- `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:minimal`: PASS, 0 warnings, 0 errors.
- `dotnet build Assembly-CSharp.csproj -v:minimal /m:1`: BLOCKED by unrelated active-agent errors in `GlobalSignals`, `FaunaBrain`, and `ConstructionManager`.
- `dotnet build Hecton8.Core.csproj -v:minimal /m:1`: BLOCKED by unrelated active-agent errors in `HabitatGraphManager` and `ConstructionManager`.
- `git diff --check` on touched code files: PASS, only CRLF conversion warnings.

## Final Diff

- `Assets/_Project/Scripts/VoxelDeltaProcessor.cs`
- `Assets/_Project/Scripts/VoxelDeltaPersistenceDTO.cs`
- `Assets/_Project/Scripts/SaveDataMigration.cs`
- `Docs/Tasks/Status_WORLD_VOXEL_SDF.md`
- `Docs/AgentLogs/Rationale_WORLD_VOXEL_SDF.md`

Tracked code diff stat: 3 files changed, 208 insertions, 57 deletions. `Status_WORLD_VOXEL_SDF.md`, `Rationale_WORLD_VOXEL_SDF.md`, and this log are new untracked agent evidence files.
