# VAULT EXORCISM APEX REVIEW 1304

Date: 2026-05-25
Agent: 1304
Domain declared: `Assets/Project/Scripts/World/Voxel`
Effective first-party scope: `Assets/_Project/Scripts` voxel/SDF files
Prompt extraction: `Docs/Tasks/CURRENT_BATCH.md`, bytes=21853, tasks=20, sha256=`e93d536283b5c370d3ce6f26b4728ff8adff4fc45bc007f1072020bd36f728df`

## Static Scan Result

- Full first-party Roslyn AST: `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1304_FULL_APEX_RECHECK.json`
- Full scan: files=2418, parseFailures=0, totalNativeFields=7462, forbiddenPersistentCandidates=1866, forbiddenMonoBehaviourCandidates=417, jobTransientFields=5532, coreMemoryAllowedFields=46, hash=`3e5f22573f34c97959fb1b089f0ec6c5db9573169bdfae4bf19563a8c76935fe`
- SurfaceNets Roslyn AST: `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1304_VOXEL_SURFACE_NETS_APEX_RECHECK.json`
- SurfaceNets scan: files=6, parseFailures=0, totalNativeFields=29, forbiddenPersistentCandidates=0, forbiddenMonoBehaviourCandidates=0, jobTransientFields=29, hash=`8ff30cf64e12439d0c052e090b83b045812f7aa2c1ecec0ae135314c31344baa`
- Filtered target files: `TARGET_FORBIDDEN_COUNT=0`

## Text Scan Result

- Runtime hotpath AST: `Docs/Reports/VOXEL_RUNTIME_HOTPATH_AUDIT_1304.json`
- Hotpath scan: files=8, parseFailures=0, objectCreations=289, managedRiskCreations=102, nativeTempJobAllocations=12, nativePersistentAllocations=15, `string.Format=0`, `.ToString()=0`, LINQ=0, `foreach=0`, interpolation=0, concat=0, hash=`1be6a51baa78e95279ecf78868d210469b0e41b6c8969a5329467007909b8ad4`
- Target route grep: 0 hits for `Agent1312`, `Dump_1312`, or `Dump_SHINOBU`.
- Editor-only managed diagnostics remain editor-only; they are not part of the production hotpath AST.
- Native allocation tokens are classified, not hidden:
  - `HectonVoxelVolume.cs:2056` and `2065`: two cold `Allocator.TempJob` publish scratch arrays, disposed in `finally`.
  - `VoxelDeformationSmokeTester.cs:223-325`: smoke-test `Allocator.TempJob` arrays.
  - `H8Memory.cs:2354-2374`, `3387`, `3826-3830`: core memory authority persistent allocator state.

## Fixed Defects Found By APEX Recheck

- `VoxelDeltaProcessor.cs:140`: dump route corrected from `Dump_1312_VoxelPaging.bin` to `Docs/AgentLogs/Dump_1304_Voxel.bin`.
- `VoxelDeltaProcessor.cs:5809`: private layout validator is now `ValidateAgent1304PrivateLayouts`, no 1312 proof alias, and private pad offsets are validated without illegal `nameof(privateField)` references.
- `VoxelSurfaceNetsContracts.cs:134`: `VoxelMeshingTuningDTO.LastCsvWriteTicks` moved to offset 0 before 4-byte fields.
- `VoxelDeformationSmokeTester.cs:65-92`: string status builders are editor-only.
- `VoxelDeformationSmokeTester.cs:675-677`: failure log no longer concatenates strings in development/runtime builds.
- `VoxelDeformationSmokeTester.cs:390-451` and `VoxelMemorySovereigntyValidator1304.cs:186-196`: dev/editor carve DTO float bridge fields are assigned only after double local-delta subtraction.
- `VoxelDeltaProcessor.cs:1048-1085` and `5617-5679`: blackbox telemetry writes now acquire `TryAcquireWriteLock` on `BufferID.ShinobuDeltaCrusherVoxelBlackBox`, re-ensure only after stale/invalid handle proof, and release in `finally`.
- `VoxelDeltaProcessor.cs:5762-5823`: dump export now holds the same vault write lock while copying the ring to disk.

## AUP Determinism Proof

- Formula: `localDeltaDouble = absoluteObjectAup - originOrCameraAup`; only `localDeltaDouble` is downcast to `float3`.
- `HectonFloatingOrigin.cs:394-397`: `ToRuntimePosition(double3,double3)` returns `ToVector3(absoluteUniversePosition - committedTotalOffset)`.
- `VoxelSurfaceNetsJobs.cs:637-644`: chunk priority uses `AupPrecisionMath.LocalDeltaDouble(state.ChunkOriginAup, CameraAup)` then downcasts local delta.
- `VoxelDeltaProcessor.cs:6325-6332`: SDF sample computes `(absolutePosition - VolumeOrigin) * InvCellSize` in double, then clamps/casts local sample coordinates.
- `HectonVoxelVolume.cs:2465-2472`: generation origin rebasing subtracts current origin in double before `Vector3` conversion.
- `VoxelDeformationSmokeTester.cs:390-451`: smoke carve DTO setup computes local delta from double AUP before legacy float bridge assignment.
- `VoxelMemorySovereigntyValidator1304.cs:186-196`: editor race fuzzer computes local delta from double AUP before legacy float bridge assignment.
- Residual legacy bridge: `HectonVoxelVolume.cs:3904-3921` converts absolute double to `Vector3` after bounds checking. It is not a new 1304 route; treat as unsafe for long-lived absolute storage.

## Assembly Isolation

- `Assets/_Project/Scripts/World/VoxelSurfaceNets/Hecton8.World.VoxelSurfaceNets.asmdef`: references only `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, Burst/Collections/Jobs/Mathematics.
- `Assets/_Project/Scripts/World/VoxelSurfaceNets/Editor/Hecton8.World.VoxelSurfaceNets.Editor.asmdef`: `includePlatforms=["Editor"]`; references SurfaceNets/Core/Contracts/Memory and Unity.Collections/Mathematics.
- No new runtime asmdef upward/horizontal domain dependency was introduced.
- Editor validator references `Hecton8.Caves` only in editor scope to call the private voxel layout guard.

## Fail-Closed Behavior

- Invalid carve, queue overflow, corrupt pending carve, and commit budget faults write `VoxelCarveTelemetryEntry` into the fixed 300-frame ring.
- Dump path writes `VoxelBlackBoxDumpHeader` plus raw ring bytes to `Docs/AgentLogs/Dump_1304_Voxel.bin`.
- Blackbox ring write/dump access is guarded by `TryAcquireWriteLock` under `SystemID.TerrainSeams`; lock release is in `finally`.
- Dump catches only cold I/O/object-state exceptions to prevent a second failure during fault export.
- NaN/local coordinate sampling clamps non-finite SDF sample coordinates to safe defaults before array access.
- Defrag/read lease route locks `BufferID.VoxelSdfTexture3D` under `SystemID.TerrainSeams` and releases through explicit branches/finally.

## Byte Offset Map

All listed sizes are explicit and multiples of 8.

| DTO | File line | Size | Offsets |
| --- | ---: | ---: | --- |
| `VoxelModifiedCell` | `VoxelDeltaProcessor.cs:21` | 8 | 0 `half Density`, 2 `byte MaterialId`, 3 `byte Flags`, 4 `ushort Reserved`, 6 `ushort Reserved1` |
| `VoxelSonarSdfRaycastHit` | `GroundRadarContracts.cs:16` | 64 | 0 `float3 Point`, 12 `float3 Normal`, 24 `float Distance`, 28 `float Density`, 32 `float Density01`, 36 `float Range`, 40 `uint Version`, 44 `uint Flags`, 48/56 pads |
| `VoxelSdfPayloadDescriptorDTO` | `GroundRadarContracts.cs:33` | 80 | 0 `float3 VolumeOrigin`, 12 `int3 GridDimensions`, 24 `float3 VoxelCellSize`, 36 `float SdfRange`, 40 `int ByteCount`, 44 `BufferID`, 48 `uint Generation`, 52 `uint SdfVersion`, 56 `SystemID`, 60 `uint Flags`, 64 `int AudioMaterialByteCount`, 68 `BufferID AudioMaterialBufferId`, 72 `uint AudioMaterialBufferGeneration`, 76 `_pad0` |
| `VoxelBlackBoxDumpHeader` | `VoxelDeltaProcessor.cs:5718` | 32 | 0 `Magic`, 4 `Capacity`, 8 `Stride`, 12 `Cursor`, 16 `ReasonFlags`, 20/24/28 pads |
| `VoxelCarveTelemetryEntry` | `VoxelDeltaProcessor.cs:5905` | 80 | 0 `double3 LastHitAup`, 24 `ulong FocusVolumeId`, 32 `Frame`, 36 `Flags`, 40-60 touched AABB ints, 64 `_pad0`, 68/70/72/74/76 ushort counters/hash, 78 `ScheduledState`, 79 `DrainBudget` |
| `CarveCellWrite` | `VoxelDeltaProcessor.cs:6152` | 32 | 0/4/8 absolute cell ints, 12 `BlendStrength`, 16 `SdfValueBits`, 18 `MaterialId`, 19 `DeltaFlags`, 20 `IsActive`, 21/22/24/28 pads |
| `NativeSnapshotWriteStats` | `VoxelDeltaProcessor.cs:6581` | 16 | 0 `TotalBytes`, 4 `ChunkCount`, 8 `TotalDirtyCellCount`, 12 `Reserved0` |
| `NativeSnapshotHeader` | `VoxelDeltaProcessor.cs:6590` | 16 | 0 `Version`, 4 `ChunkCount`, 8 `TotalDirtyCellCount`, 12 `Reserved0` |
| `LegacyNativeSnapshotHeader` | `VoxelDeltaProcessor.cs:6599` | 8 | 0 `ChunkCount`, 4 `TotalDirtyCellCount` |
| `NativeSnapshotChunkHeader` | `VoxelDeltaProcessor.cs:6606` | 24 | 0/4/8 chunk ints, 12 `VoxelSize`, 16 `DirtyCellCount`, 20 `Reserved0` |
| `NativeSnapshotChunkHeaderRle` | `VoxelDeltaProcessor.cs:6617` | 32 | 0/4/8 chunk ints, 12 `VoxelSize`, 16 `DirtyCellCount`, 20 `StorageFlags`, 21 `Reserved0`, 22 `Reserved1`, 24 `PayloadByteLength`, 28 `Reserved2` |
| `NativeSnapshotChunkHeaderDeltaRle` | `VoxelDeltaProcessor.cs:6632` | 40 | previous 0-24 fields, 28 `PayloadHashLow`, 32 `PayloadHashHigh`, 36 `Reserved2` |
| `ChunkAddress` | `VoxelDeltaProcessor.cs:6649` | 8 | 0 `ulong _packedKey` |
| `VoxelVertexDTO` | `VoxelSurfaceNetsContracts.cs:92` | 32 | 0 `Position`, 12 `NormalPacked`, 16 `TangentPacked`, 20 `ColorPacked`, 24 `UV` |
| `ChunkMeshingStateDTO` | `VoxelSurfaceNetsContracts.cs:107` | 64 | 0 `double3 ChunkOriginAup`, 24 `BoundsCenterLocal`, 36 `VoxelSize`, 40 `VertexCount`, 44 `IndexCount`, 48 `RawDebugVertexCount`, 52 `ChunkHash`, 56 `Version`, 60 `Stage`, 61 `Flags`, 62 `Priority` |
| `VoxelMeshingTuningDTO` | `VoxelSurfaceNetsContracts.cs:134` | 64 | 0 `LastCsvWriteTicks`, 8 `GlobalQualityWeight`, 12 `IsoSurface`, 16 `DecimationAggression`, 20 `NormalSmoothingAngleDegrees`, 24 `VoxelSize`, 28 `BiomeBlendScale`, 32 `MaxExtractionMs`, 36 `DebugRawCapture01`, 40 `MaxChunksPerFrame`, 44 `ChunkResolution`, 48 `Version`, 52 `Flags`, 56 `ForceRemeshVersion`, 60 `LastCsvHash` |
| `VoxelMeshingTelemetryEntry` | `VoxelSurfaceNetsContracts.cs:169` | 64 | 0 `Frame`, 4 `ChunkHash`, 8 `VertexCount`, 12 `IndexCount`, 16 `ChunksMeshedThisFrame`, 20 `ExtractionComputeTimeMs`, 24 `GlobalQualityWeight`, 28 `DecimationRatio`, 32 `SamplingRatio`, 36 `Flags`, 40 `RawDebugVertexCount`, 44 `StateHash`, 48 `DumpReason`, 52 `_pad1`, 56 `_pad0` |
| `VoxelSurfaceAabbDTO` | `VoxelSurfaceNetsContracts.cs:204` | 64 | 0 `double3 CenterAup`, 24 `ExtentsLocal`, 36 `ChunkHash`, 40 `Version`, 44 `VisibleFlags`, 45 `Priority`, 46/48/56 pads |
| `VoxelSurfaceModifiedSignal` | `VoxelSurfaceNetsContracts.cs:227` | 64 | 0 `double3 ChunkOriginAup`, 24 `ChunkCoord`, 36 `ChunkHash`, 40 `Version`, 44 `Dirty`, 45 `ForceHighPriority`, 46/48/56 pads |
| `VoxelSurfacePriorityDTO` | `VoxelSurfaceNetsContracts.cs:250` | 16 | 0 `Score`, 4 `ChunkIndex`, 8 `ChunkHash`, 12 `Flags` |
| `VoxelSurfaceIndirectArgsDTO` | `VoxelSurfaceNetsContracts.cs:263` | 32 | 0 `IndexCountPerInstance`, 4 `InstanceCount`, 8 `StartIndex`, 12 `BaseVertex`, 16 `StartInstance`, 20/24/28 pads |
| `MockVoxelDensityArray` | `VoxelSurfaceNetsContracts.cs:284` | 48 | 0 `Dimensions`, 12 `VoxelSize`, 16 `CenterLocal`, 28 `Radius`, 32 `ShellThickness`, 36 `Seed`, 40 `Flags`, 44 `_pad0` |
| `VoxelSurfacePhysicsBakeRequestDTO` | `VoxelSurfaceNetsContracts.cs:305` | 32 | 0 `MeshId`, 4 `ChunkIndex`, 8 `ChunkHash`, 12 `Version`, 16 `Pending`, 17 `Completed`, 18/20/24 pads |
| `VoxelSurfaceHzbTileDTO` | `VoxelSurfaceNetsContracts.cs:328` | 16 | 0 `Depth01`, 4 `TileX`, 8 `TileY`, 12 `Flags` |

## Overengineering Review

- No new physical/mathematical solver was added.
- SurfaceNets remains a bounded extraction path with quality-weight stride/decimation.
- Compacted chunk persistence keeps uniform/sparse RLE routes; non-uniform live native-view storage remains rejected until it has a vault DTO route.
- Sonar material publication remains byte payload plus encoded SDF; no scientific simulation was introduced.

## Verification Blockers

- `dotnet build Hecton8.Core.csproj --no-restore --nologo` was not rerun in the final no-build pass per user instruction.
- Last guarded build remains externally blocked.
- Remaining errors: 2 `CS0122` in `Assets/_Project/Scripts/Audio/AcousticPortalPropagation.cs`, 16 `CS0177` in `Assets/_Project/Scripts/TetherInstance.cs`, 3 `CS0246` in `Assets/_Project/Scripts/TetherInstance.cs`.
- Current 1304 target files produce 0 build errors in the latest compiler output.
