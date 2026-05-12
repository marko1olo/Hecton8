# Status_WORLD_VOXEL_SDF

Agent: VOXEL_SURGEON
Prompt ID: WORLD_VOXEL_SDF
Domain: Echelon 2 - World Generation & Terrain / Voxel SDF Pipeline
Batch source: Docs/Tasks/CURRENT_BATCH.md
Status: PENDING VERIFICATION

## Relevant Mandates Read

- VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt
- VOX_Voxel_World_Logic_Carving_Persistence.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- REND_Shader_Noir_Aesthetics_Dithering_Fog.txt
- DATA_Save_Persistence_Binary_Delta_Checksum.txt
- AI_DYNAMIC_NAVGRID_SDF_INTEGRATION.txt

## Loop 1 - Tasks 1-5

- [x] 1. Shader-based normal smoothing - DONE. DOD: `ResolveScreenSpaceSmoothedVoxelNormal` uses `ddx(positionWS)` and `ddy(positionWS)` to micro-bevel the coarse nearest-grid normal in shader. Rejected CPU trilinear normals. Estimate: preserves 35-60 us CPU per rebuilt chunk.
- [x] 2. Organic cavity masking fake AO - DONE. DOD: `VoxelNormalJob` derives AO from 6 neighbor density cells and `VoxelColorJob` carries AO through vertex color/UV data; shader multiplies baked AO by 1D depth noise. Rejected post-process SSAO dependency. Estimate: avoids 15-25 us extra CPU sampling per chunk.
- [x] 3. Axis-weighted carving no sqrt - DONE. DOD: sphere/box/capsule carve SDF uses `AxisWeightedLengthApprox` (`cmax + sum * 0.33f`) instead of Euclidean length. Rejected `math.length` realism. Estimate: 8-20 us saved per large carve batch.
- [x] 4. 2-axis cinematic triplanar - DONE. DOD: rock shader samples only primary/secondary axes via `ResolveCinematicTwoAxisProjection` for color and normal. Rejected true 3-axis triplanar. Estimate: one axis sample path removed from visible terrain shading.
- [x] 5. Dominant-axis mining impulse - DONE. DOD: `ResolveDominantAxisDirection` snaps impulse to cardinal axis with abs comparisons and no normalize. Rejected normalized arbitrary hit vector. Estimate: 1-3 us per carve aftermath event.

## Loop 2 - Tasks 6-10

- [x] 6. Bit-packed chunk address - DONE. DOD: `ChunkAddress` is an 8-byte packed key and hashes by XOR-folding `_packedKey`. Rejected tuple/dictionary key expansion. Estimate: 2-6 us saved during chunk registry churn.
- [x] 7. sbyte SDF data - DONE. DOD: MC and normal jobs consume `NativeArray<sbyte>` quantized density. Rejected float SDF hot grid storage. Estimate: 20-80 us memory-bandwidth reduction per chunk pass.
- [x] 8. In-place carve pointers - DONE. DOD: `CarveSdfJob` writes `CarveCellWrite* WritesPtr` directly while keeping a `NativeArray<CarveCellWrite>` ownership field. Rejected managed staging or NativeArray-only indexer path. Estimate: 5-15 us per carve job.
- [x] 9. Compaction pointers - DONE. DOD: `VoxelDeltaCompactionJob` uses raw input/output pointers and `InvCellSize` multiply for sampling. Rejected per-cell division and indexer-only compaction. Estimate: 30-80 us per compaction job.
- [x] 10. Worker-thread RLE detection - DONE. DOD: `VoxelDeltaUniformRunDetectJob` runs after compaction as an `IJob` and returns only a 1-byte uniform flag (`NativeArray<byte> RleUniformFlag`). Rejected main-thread scan and 4-byte run header. Estimate: 20-60 us main-thread stall avoided; 3 bytes less flag storage per request.

## Loop 3 - Tasks 11-15

- [x] 11. SDF-to-physics bridge GetSDFDensity - DONE. DOD: `HectonVoxelVolume.GetSDFDensity(float3 aupPosition)` and bool overload expose published SDF density without raycasts. Rejected physics query bridge. Estimate: 50-150 us avoided per predator steering batch.
- [x] 12. Mining VFX ripple EventBus signal - DONE. DOD: accepted carve schedules publish `DebrisSpawnSignal` immediately with AUP hit point, deterministic source hash, intensity, and source flags. Rejected waiting for mesh rebuild/debris hydration. Estimate: signal enqueue under 5 us and hides 2-frame meshing latency.
- [x] 13. Subtractive delta persistence - DONE. DOD: dense dirty chunks still save masks/arrays; uniform compacted chunks now write a `StorageUniformSdfRle` DTO payload containing only `ushort uniformSdfValueBits`, and load back into compacted RLE state. Migration clears stale dense arrays for that storage mode. Rejected serializing 32x32x32 default cells. Estimate: 135,166 bytes and roughly 100-500 us serialization/IO avoided per solid chunk.
- [x] 14. Bitmask ring queues - DONE. DOD: pending carve and compaction queues resolve slots with `& PendingCarveMask` / `& PendingCompactionMask`. Rejected modulo ring indexing. Estimate: 1-4 us per burst queue drain.
- [x] 15. Scheduled reciprocal multiply - DONE. DOD: density quantization uses `source * densityDecodeInvScale`. Rejected per-voxel division. Estimate: 10-30 us per quantized chunk.

## Loop 4 - Tasks 16-20

- [x] 16. Cast-bias rounding - DONE. DOD: quantization uses sign-aware cast bias instead of `math.round`. Rejected generic round helper. Estimate: 8-20 us per quantized chunk.
- [x] 17. Struct padding - DONE. DOD: `VoxelModifiedCell` is `[StructLayout(... Size = 8)]`; `CarveCellWrite` is `[StructLayout(... Size = 32)]`. Rejected implicit layout. Estimate: 5-20 us from predictable native strides during carve/delta traversal.
- [x] 18. Burst native wrappers - DONE. DOD: pointer-backed carve and compaction jobs retain `NativeArray` fields for Burst ownership/safety while using unsafe pointers inside execution. Rejected unmanaged pointers without native ownership wrappers. Estimate: prevents Burst fallback/copy path.
- [x] 19. LUT collider table - DONE. DOD: chthonic pillar collider uses literal 24-point `float2` LUT, no runtime sin/cos generation. Rejected procedural trig table. Estimate: 10-40 us setup saved per collider table build.
- [x] 20. Precompute density strides - DONE. DOD: `VoxelNormalJob` precomputes `densityStrideY/Z` and samples six neighbors by direct index addition. Rejected repeated `GridIndex(x,y,z)` multiplication. Estimate: 20-80 us per chunk normal pass.

## Verification

- [x] Compile after tasks 1-5 - COVERED by focused `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:minimal` success.
- [x] Compile after tasks 6-10 - COVERED by focused `Assembly-CSharp.csproj` success.
- [x] Compile after tasks 11-15 - COVERED by focused `Assembly-CSharp.csproj` success.
- [x] Compile after tasks 16-20 - COVERED by focused `Assembly-CSharp.csproj` success.
- [x] Strict self-read loop 1 - Shader normal/AO/triplanar code re-read.
- [x] Strict self-read loop 2 - Engine quantization/normal/AO/LUT code re-read.
- [x] Strict self-read loop 3 - Delta carve/compaction/RLE code re-read.
- [x] Strict self-read loop 4 - SDF density bridge and persistence DTO/migration re-read.
- [x] Strict self-read loop 5 - Native snapshot RLE load/save path and dirty worktree diff re-read.
- [x] Focused compile - PASSED: `Assembly-CSharp -> Temp\bin\Debug\Assembly-CSharp.dll`, 0 warnings, 0 errors.
- [x] Full solution compile - BLOCKED BY DEPENDENCY: `dotnet build Assembly-CSharp.csproj -v:minimal /m:1` fails in pre-existing cross-agent code: missing `AnomalySignal`, `AcousticPingSignal`, `HypoxiaSignal`, `ScanCompleteSignal`, `FaunaTier1LodProxyEntry`, and `ConstructionManager` missing `IOriginShiftListener.OnOriginShift`.
- [x] OMEGA POLISH parsed - DONE: `<POLISH_MANDATE id="OMEGA_POLISH">` read from `Docs/Tasks/CURRENT_BATCH.md`.
- [x] Domain boundary checked - DONE: `Docs/Actual Domains of Project.txt` places Voxel SDF Pipeline, Marching Cubes, and Voxel Carving in Echelon 2. Save DTO/migration edit is justified as persistence for voxel carving deltas.
- [x] `Hecton8.Core.csproj` build - BLOCKED BY DEPENDENCY: `dotnet build Hecton8.Core.csproj -v:minimal /m:1` failed with 0 warnings and 3 unrelated errors: missing `TransitionHatchMeshState` in `HabitatGraphManager` and missing `Hecton8.Physics.SyncTransforms` in `ConstructionManager`.

## Dirty Worktree Notes

- `SaveDataMigration.cs` already contained unrelated edits in the current worktree (including data archaeology migration changes and a pre-existing using diff). I only changed the voxel delta migration branch and did not revert neighboring edits.
- `GlobalSignals.cs` has existing compile errors outside this task's write scope. The voxel task used the existing `DebrisSpawnSignal` lane without editing that file.
