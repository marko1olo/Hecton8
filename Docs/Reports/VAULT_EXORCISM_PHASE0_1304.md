# VAULT EXORCISM PHASE 0 - 1304

Date: 2026-05-25
Agent: 1304
Role: MEMORY_SOVEREIGN_WORLD_VOXEL_EXORCIST
Status: PENDING VERIFICATION

## Scope

- Declared domain `Assets/Project/Scripts/World/Voxel` is absent on disk.
- Effective live first-party voxel scope used for Phase 0: `Assets/_Project/Scripts/HectonVoxelVolume.cs`, `Assets/_Project/Scripts/HectonVoxelEngine.cs`, `Assets/_Project/Scripts/VoxelDeltaProcessor.cs`, and `Assets/_Project/Scripts/World/VoxelSurfaceNets`.
- Third-party and non-voxel world systems were not treated as editable territory.

## Roslyn Proof Artifacts

- Full first-party scripts audit: `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1304_FULL.json`
  - Files scanned: 2413
  - Parse failures: 0
  - Native field declarations: 7516
  - Forbidden persistent candidates: 1947
  - Job transient fields: 5523
  - Core memory allowed fields: 46
  - Audit hash: `397573fbb9c1582d05266ae7642d80dc5befb5bd92c67455a0b76fdcd564a10f`
- VoxelSurfaceNets strict folder audit: `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1304_VOXEL_SURFACE_NETS.json`
  - Files scanned: 5
  - Parse failures: 0
  - Native field declarations: 50
  - Forbidden persistent candidates: 21
  - Job transient fields: 29
  - Audit hash: `58bc1354b9797cce60e6199a53fadaf7ccf0c7bef4110f6bcb8d6e6f2c4e26fa`

## Primary Hit List

| File | Owner | Count | Finding |
|---|---:|---:|---|
| `Assets/_Project/Scripts/HectonVoxelVolume.cs` | `HectonVoxelVolume` | 4 | Published sonar SDF/audio double-buffer fields at lines 396-399 are scene-persistent `NativeArray<byte>` aliases. |
| `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` | `VoxelDeltaProcessor` | 11 | Persistent carve queue, compaction scratch arrays, and native snapshot scratch at lines 203, 239-247, 250. |
| `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` | `ChunkDeltaState` | 4 | Struct stores direct native array views for dirty mask, SDF bits, material ids, and cell flags. |
| `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` | `CompactedChunkState` | 3 | Struct stores direct compacted SDF/material/cell flag views. |
| `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` | `ScheduledCompactionRequest` | 9 | Scheduled request stores direct compaction views across schedule windows. |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs` | `MCTables` | 2 | Static `NativeArray<int>` marching-cubes tables, allocated persistent outside vault. |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs` | `VoxelStreamingScratchSlot` | 49 | Persistent streaming scratch pool stores direct native arrays/lists/hash maps. |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs` | `VoxelStreamingScratchLease` | 49 | Lease mirrors scratch direct views; transient by intent, but still field-backed and cross-await visible. |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs` | `VoxelPipelineData` | 24 | Async generation payload stores direct native arrays/list/hash map until disposal. |
| `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsGpuUploadDispatcher.cs` | `VoxelSurfaceNetsGpuUploadDispatcher` | 3 | GraphicsBuffer lock views persist as fields while upload job is in flight. |
| `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` | `VoxelSurfaceNetsVaultBuffers` | 18 | View aggregate carries direct resolved vault arrays. This is acceptable only as method-local return data; persistent storage is forbidden. |

## Ownership And Lifecycle Map

- Sonar SDF ownership: `SystemID.WorldStreaming`; existing vault buffers: `BufferID.VoxelSdfTexture3D` and `BufferID.VoxelSdfPayloadDescriptor`. The local double-buffer remains the defect.
- Delta carve ownership: current code mixes `SystemID.TerrainSeams` vault-backed pools with direct persistent scratch. Existing vault buffers include `ShinobuDeltaCrusherVoxelBlackBox`, `ShinobuDeltaCrusherCarveWrites`, `ShinobuDeltaCrusherDirtyMaskPool`, `ShinobuDeltaCrusherSdfBitsPool`, `ShinobuDeltaCrusherMaterialPool`, and `ShinobuDeltaCrusherCellFlagsPool`.
- Meshing scratch ownership: `HectonVoxelEngine` owns cold persistent scratch slots outside vault. Migration requires a route card because it touches generation pipeline, collider bake, spawn extraction, and marching cubes.
- VoxelSurfaceNets ownership: vault handles already exist; unsafe part is storing GPU lock views in dispatcher fields and passing `VoxelSurfaceNetsVaultBuffers` beyond method scope.

## Dependency Graph

- `HectonVoxelVolume.TryGetPublishedSonarSdfPayload` exposes `NativeArray<byte>.ReadOnly` to sonar/GPR consumers through `IVoxelSonarSdfReadModel`.
- `HectonVoxelVolume.TrySampleDensity`, `TryRaymarchPublishedSdf`, and `TrySampleNearestPublishedGradient` read local SDF fields directly.
- `GroundPenetratingRadarRuntime` reads the sonar SDF through the registry-facing read model.
- `VoxelDeltaProcessor` reads sonar payload leases during compaction and writes carve results through vault-backed carve buffers.
- `VoxelSurfaceNetsVault.TryResolveViews` returns direct arrays; callers must keep this aggregate phase-local.

## DTO Layout

- `VoxelSdfPayloadDescriptorDTO`: explicit layout, 64 bytes, fields at 0/12/24/36/40/44/48/52/56/60. Size is 8-byte aligned.
- `VoxelModifiedCell`: explicit layout, 8 bytes, field offsets 0/2/3/4/6.
- `VoxelCarveTelemetryEntry`: explicit layout, 80 bytes, double3 first, 8-byte aligned.
- `VoxelBlackBoxDumpHeader`: explicit layout, 32 bytes, 8-byte aligned.
- `VoxelSurfaceNetsContracts` DTOs are explicit-layout by contract. `VoxelSurfaceNetsJobs` job carrier structs use `LayoutKind.Sequential`; this is acceptable for transient job parameters, not vault DTOs.
- `VoxelSurfaceVertex` and `VoxelColliderVertex` in `HectonVoxelEngine.cs` are `LayoutKind.Sequential`; they are mesh-upload structs, not vault DTOs, but should be layout-guarded before ARM64 shipping.

## Telemetry Ring Plan

- Use existing `BufferID.ShinobuDeltaCrusherVoxelBlackBox` for carve/deformation failures until a new route card is approved.
- New 1304-specific dump target must be `Docs/AgentLogs/Dump_1304_Voxel.bin`; current delta processor dump target is `Dump_SHINOBU_308_Voxel.bin` and must be corrected in Phase 1.
- Required 64-byte entry for new memory-sovereignty telemetry:
  - 0: `double3 LastAup` (24)
  - 24: `ulong OwnerId`
  - 32: `uint Frame`
  - 36: `uint BufferId`
  - 40: `uint Generation`
  - 44: `uint Flags`
  - 48: `ushort ExpectedCount`
  - 50: `ushort ActualCount`
  - 52: `ushort JobUsec`
  - 54: `ushort Cursor`
  - 56: `uint StateHash`
  - 60: `uint Pad0`

## Compile Status

- No C# production files were mutated in Phase 0.
- Roslyn scanner parse validation: passed with 0 parse failures.
- `dotnet build` was not launched because multiple `dotnet` processes are currently active. Project rule forbids build launches while another dotnet/csc process is running.
