# Hecton8 Voxel Surface Nets Terrain & Cave Collision Pipeline Diagnostic Audit

**Milestone**: Milestone 1: Reconnaissance & Diagnostic Audit  
**Agent**: teamwork_preview_explorer  
**Working Directory**: `C:\hades\Hecton8\.agents\explorer_m1`  
**Authority Reference**: `voxels.md` (Signed distance convention: positive=solid rock, zero=surface boundary, negative=void; heightmap protection `depthToTerrainSurface < 30f`; `GlobalQualityWeight` quality scaling); `AGENTS.md` (GC 0 B/frame, main thread budget 12 ms).

---

## 1. Observation

Direct observations from codebase inspection across `HectonVoxelEngine.cs`, `H8Memory.cs`, `VoxelSurfaceNetsVault.cs`, `VoxelSurfaceNetsJobs.cs`, `VoxelSurfaceNetsContracts.cs`, and `Hecton8.World.VoxelSurfaceNets.asmdef`:

### Observation 1.1: Missing Method Definition in `HectonVoxelEngine.cs`
- **File**: `Assets/_Project/Scripts/HectonVoxelEngine.cs`
- **Line 14030–14037**:
  ```csharp
  if (UseSurfaceNets)
  {
      return await ApplySurfaceNetsColliderMeshesAsync(volume, data, localVolumeOrigin, ct);
  }
  else
  {
      return await ApplyChunkedColliderMeshesAsync(volume, data, useProjectedLocalPositions, localVolumeOrigin, ct);
  }
  ```
- **Finding**: `ApplySurfaceNetsColliderMeshesAsync` is invoked on line 14032 when `UseSurfaceNets == true`. However, `ApplySurfaceNetsColliderMeshesAsync` is NOT defined anywhere in `HectonVoxelEngine.cs` or in any other file in the repository.
- **Compiler Error**: `CS0103: The name 'ApplySurfaceNetsColliderMeshesAsync' does not exist in the current context`.

### Observation 1.2: Missing `using` Directive and Asmdef Boundary Violation
- **File**: `Assets/_Project/Scripts/HectonVoxelEngine.cs` (lines 5–30)
- **Finding**: `HectonVoxelEngine.cs` lacks `using Hecton8.World.VoxelSurfaceNets;`.
- **File**: `Assets/_Project/Scripts/World/VoxelSurfaceNets/Hecton8.World.VoxelSurfaceNets.asmdef` (lines 1–23)
- **Finding**: `"autoReferenced": false` is set on the `VoxelSurfaceNets` asmdef. Because `HectonVoxelEngine.cs` lives in default `Assembly-CSharp` (un-asmdef root scripts), `Assembly-CSharp` does not reference `Hecton8.World.VoxelSurfaceNets.dll`. Any attempt by `HectonVoxelEngine.cs` to access `VoxelSurfaceNetsVault` or `VoxelSurfaceNetsJobs` fails with `CS0246: The type or namespace name 'VoxelSurfaceNets' could not be found`.

### Observation 1.3: Hallucinated / Mismatched API - `ColliderMeshIdBase` vs `MeshIdBase`
- **File**: `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs` (lines 815–825)
- **Code Quote**:
  ```csharp
  public struct VoxelSurfacePhysicsBakeRequestJob : IJobParallelFor
  {
      [ReadOnly]
      [NoAlias]
      public NativeArray<ChunkMeshingStateDTO> States;

      [NoAlias]
      public NativeArray<VoxelSurfacePhysicsBakeRequestDTO> Requests;

      public int MeshIdBase;
  ```
- **Finding**: The field on `VoxelSurfacePhysicsBakeRequestJob` is `MeshIdBase` (line 824). The name `ColliderMeshIdBase` is a hallucinated API name. Calling `job.ColliderMeshIdBase` will produce compiler error `CS0117: 'VoxelSurfacePhysicsBakeRequestJob' does not contain a definition for 'ColliderMeshIdBase'`.

### Observation 1.4: Unscheduled Physics Bake Job
- **File**: `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` (lines 704–724)
- **Code Quote**:
  ```csharp
  if (!TryCreateExtractionJob(in buffers, chunkIndex, frame, false, out SurfaceNetExtractionJob visualJob) ||
      !TryCreateExtractionJob(in buffers, chunkIndex, frame, true, out SurfaceNetExtractionJob colliderJob))
  {
      ReleaseJobBufferLease(ref lease);
      return false;
  }

  JobHandle visualDependency = visualJob.Schedule(inputDependency);
  JobHandle colliderDependency = colliderJob.Schedule(inputDependency);
  outputDependency = JobHandle.CombineDependencies(visualDependency, colliderDependency);
  ```
- **Finding**: `TryScheduleExtractionPinned` schedules `visualJob` and `colliderJob`. However, `VoxelSurfacePhysicsBakeRequestJob` (lines 815–847 in `VoxelSurfaceNetsJobs.cs`) is NEVER scheduled in `VoxelSurfaceNetsVault.cs` or anywhere else in the project.

### Observation 1.5: DataVault Mutation Guard and Write Lease Defect for Collider Buffers
- **File**: `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` (lines 197–208, 510–516)
- **Code Quote**:
  ```csharp
  private static readonly ulong ExtractionJobMutationGuardMask =
      VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.Density) |
      VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.Vertices) |
      VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.Indices) |
      VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.CellVertexMap) |
      VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.States) |
      VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.Tuning) |
      VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.SurfaceEdgeMasks) |
      VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.TelemetryRing) |
      VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.TelemetryCursor) |
      VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.RawDebugVertices) |
      VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.IndirectArgs);
  ```
- **Finding**: `ExtractionJobMutationGuardMask` includes `Vertices`, `Indices`, and `CellVertexMap` (the visual buffers), but DOES NOT INCLUDE `ColliderVertices` (`81000`), `ColliderIndices` (`81001`), or `ColliderCellVertexMap` (`81002`).
- When `colliderJob` (`isCanonicalCollider = true`) executes, it writes to `buffers.ColliderVertices`, `buffers.ColliderIndices`, and `buffers.ColliderCellVertexMap` WITHOUT mutation guard protection or DataVault write lease locks.

### Observation 1.6: Dual-Mesh Parameterization State in `SurfaceNetExtractionJob`
- **File**: `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs` (lines 117, 120, 639–641)
- **Code Quote**:
  ```csharp
  int stride = IsCanonicalCollider ? 1 : ResolveSamplingStride(quality);
  float decimationBias = IsCanonicalCollider ? 0f : math.saturate(tuning.DecimationAggression) * (1f - qualityCurve);
  ```
- **Finding**: `SurfaceNetExtractionJob` already enforces `stride = 1` and `decimationBias = 0.0f` when `IsCanonicalCollider == true`. The job logic correctly isolates canonical collider extraction from visual LOD decimation.

### Observation 1.7: Buffer Definitions in `H8Memory.cs` and `VoxelSurfaceNetsContracts.cs`
- **File**: `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` (lines 141–143)
  ```csharp
  VoxelSurfaceNetsColliderVertices = 81000,
  VoxelSurfaceNetsColliderIndices = 81001,
  VoxelSurfaceNetsColliderCellVertexMap = 81002,
  ```
- **File**: `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsContracts.cs` (lines 68–70)
  ```csharp
  public const BufferID ColliderVertices = BufferID.VoxelSurfaceNetsColliderVertices;
  public const BufferID ColliderIndices = BufferID.VoxelSurfaceNetsColliderIndices;
  public const BufferID ColliderCellVertexMap = BufferID.VoxelSurfaceNetsColliderCellVertexMap;
  ```
- **File**: `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs` (lines 343–356)
  ```csharp
  handles.ColliderVertices = vault.EnsureGenerationHandle<VoxelVertexDTO>(
      VoxelSurfaceNetsVaultBufferIds.ColliderVertices,
      VoxelSurfaceNetsConstants.MaxColliderVertices,
      SystemID.WorldStreaming,
      NativeArrayOptions.UninitializedMemory);
  ```
- **Finding**: The `BufferID` enum values exist in `H8Memory.cs` and handles are allocated in `VoxelSurfaceNetsVault.cs`. However, their mutation guard bits (`VaultMutationGuardBit`) and lease write locks are missing from `ExtractionJobMutationGuardMask`.

---

## 2. Logic Chain

1. **Premise 1**: Enabling `UseSurfaceNets = true` in `HectonVoxelEngine` routes terrain/cave volume meshing to `ApplySurfaceNetsColliderMeshesAsync` (Observation 1.1).
2. **Step 1**: Because `ApplySurfaceNetsColliderMeshesAsync` is absent from `HectonVoxelEngine.cs`, compilation fails immediately with `CS0103` (Observation 1.1).
3. **Step 2**: Even if `ApplySurfaceNetsColliderMeshesAsync` were added to `HectonVoxelEngine.cs`, calling into `VoxelSurfaceNetsVault` fails with `CS0246` because `HectonVoxelEngine.cs` lacks `using Hecton8.World.VoxelSurfaceNets;` and `Hecton8.World.VoxelSurfaceNets.asmdef` has `"autoReferenced": false` (Observation 1.2).
4. **Step 3**: `VoxelSurfaceNetsVault.TryScheduleExtractionPinned` schedules both `visualJob` and `colliderJob` (Observation 1.4, 1.6). However, the mutation guard mask (`ExtractionJobMutationGuardMask`) and lease lock only protect visual buffers (`Vertices`, `Indices`, `CellVertexMap`) and ignore collider buffers (`ColliderVertices`, `ColliderIndices`, `ColliderCellVertexMap`) (Observation 1.5, 1.7).
5. **Step 4**: When `colliderJob` runs concurrently or under DataVault compaction fences, un-guarded writes to `ColliderVertices` cause DataVault memory sovereignty violations and race conditions (Observation 1.5).
6. **Step 5**: Furthermore, `VoxelSurfacePhysicsBakeRequestJob` is never scheduled by `VoxelSurfaceNetsVault` (Observation 1.4). Even if collider vertices are extracted into DataVault, no physics bake requests are generated for PhysX ingestion.
7. **Conclusion**: The Surface Nets collision pipeline is broken by a combination of missing entry-point methods (`ApplySurfaceNetsColliderMeshesAsync`), asmdef boundary restrictions, unscheduled bake jobs (`VoxelSurfacePhysicsBakeRequestJob`), and incomplete DataVault write lease locks for `ColliderVertices`/`ColliderIndices`/`ColliderCellVertexMap`.

---

## 3. Caveats

- **Runtime Execution**: Static analysis and code audit were performed; runtime execution and profiler captures were not triggered because `ApplySurfaceNetsColliderMeshesAsync` does not compile.
- **PhysX Mesh Slicing Strategy**: `ApplyChunkedColliderMeshesAsync` currently partitions Marching Cubes triangles into spatial chunk bounds (`ColliderTriangleBuckets`). `ApplySurfaceNetsColliderMeshesAsync` will need a compatible spatial partitioning pass or direct conversion of extracted `ColliderVertices` into pooled chunk `MeshColliders`.
- **No other caveats.**

---

## 4. Conclusion

The Voxel Surface Nets terrain & cave collision pipeline requires four focused fixes:
1. **Asmdef & Namespace Integration**: Update `Hecton8.World.VoxelSurfaceNets.asmdef` to `"autoReferenced": true` (or reference it from a dedicated engine asmdef) and add `using Hecton8.World.VoxelSurfaceNets;` to `HectonVoxelEngine.cs`.
2. **DataVault Lease & Mutation Guard Alignment**: Add `ColliderVertices` (`81000`), `ColliderIndices` (`81001`), and `ColliderCellVertexMap` (`81002`) to `ExtractionJobMutationGuardMask` and `TryAcquireExtractionJobLease` in `VoxelSurfaceNetsVault.cs`.
3. **Bake Job Scheduling**: Add `VoxelSurfacePhysicsBakeRequestJob` scheduling into `TryScheduleExtractionPinned` (or `TrySchedulePhysicsBakeRequestsPinned`) using the correct field name `MeshIdBase` (not `ColliderMeshIdBase`).
4. **Engine Async Bridge (`ApplySurfaceNetsColliderMeshesAsync`)**: Implement `ApplySurfaceNetsColliderMeshesAsync` in `HectonVoxelEngine.cs` to consume `buffers.ColliderVertices` and `buffers.ColliderIndices` (or physics bake requests), construct pooled Mesh colliders, and invoke `Physics.BakeMesh`.

---

## 5. Step-by-Step Fix Recommendations

### Phase 1: Asmdef & Namespace Fixes
1. Edit `Assets/_Project/Scripts/World/VoxelSurfaceNets/Hecton8.World.VoxelSurfaceNets.asmdef`:
   - Change `"autoReferenced": false` to `"autoReferenced": true`.
2. Edit `Assets/_Project/Scripts/HectonVoxelEngine.cs`:
   - Add `using Hecton8.World.VoxelSurfaceNets;` to the top using declarations.

### Phase 2: `VoxelSurfaceNetsVault.cs` Buffer Protection & Scheduling
1. Update `ExtractionJobMutationGuardMask` in `VoxelSurfaceNetsVault.cs` (lines 197–208):
   ```csharp
   private static readonly ulong ExtractionJobMutationGuardMask =
       VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.Density) |
       VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.Vertices) |
       VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.Indices) |
       VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.CellVertexMap) |
       VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.ColliderVertices) |
       VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.ColliderIndices) |
       VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.ColliderCellVertexMap) |
       VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.States) |
       VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.Tuning) |
       VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.SurfaceEdgeMasks) |
       VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.TelemetryRing) |
       VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.TelemetryCursor) |
       VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.RawDebugVertices) |
       VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.IndirectArgs) |
       VaultMutationGuardBit(VoxelSurfaceNetsVaultBufferIds.PhysicsBakeRequests);
   ```
2. Update `TryAcquireExtractionJobLease` write mask in `VoxelSurfaceNetsVault.cs` (line 515):
   - Include `JobColliderVerticesLock | JobColliderIndicesLock | JobColliderCellVertexMapLock` (defined in `VoxelSurfaceNetsVault.cs`).
3. Add `TrySchedulePhysicsBakeRequestsPinned` in `VoxelSurfaceNetsVault.cs`:
   ```csharp
   public static bool TrySchedulePhysicsBakeRequestsPinned(
       in VoxelSurfaceNetsVaultBuffers buffers,
       int meshIdBase,
       JobHandle inputDependency,
       out JobHandle outputDependency,
       out VoxelSurfaceNetsJobBufferLease lease)
   {
       outputDependency = inputDependency;
       lease = default;
       if (!TryAcquireExtractionJobLease(in buffers, out lease))
           return false;

       VoxelSurfacePhysicsBakeRequestJob bakeJob = new VoxelSurfacePhysicsBakeRequestJob
       {
           States = buffers.States,
           Requests = buffers.PhysicsBakeRequests,
           MeshIdBase = meshIdBase
       };

       outputDependency = bakeJob.Schedule(buffers.States.Length, 64, inputDependency);
       return true;
   }
   ```

### Phase 3: `HectonVoxelEngine.cs` Bridge Implementation
1. Implement `ApplySurfaceNetsColliderMeshesAsync` in `HectonVoxelEngine.cs`:
   ```csharp
   private async Awaitable<bool> ApplySurfaceNetsColliderMeshesAsync(
       HectonVoxelVolume volume,
       VoxelPipelineData data,
       float3 localVolumeOrigin,
       CancellationToken ct)
   {
       if (volume == null)
           return false;

       IDataVault vault = GlobalRegistry.DataVault;
       if (vault == null || !VoxelSurfaceNetsVault.TryResolveViews(vault, out VoxelSurfaceNetsVaultBuffers buffers))
       {
           volume.DisableColliderChunksForCinematicFake();
           return false;
       }

       NativeArray<VoxelVertexDTO> colliderVertices = buffers.ColliderVertices;
       NativeArray<uint> colliderIndices = buffers.ColliderIndices;
       NativeArray<ChunkMeshingStateDTO> states = buffers.States;

       if (!colliderVertices.IsCreated || !colliderIndices.IsCreated || !states.IsCreated)
       {
           volume.DisableColliderChunksForCinematicFake();
           return false;
       }

       int stateIndex = math.clamp(data.VolumeIndex, 0, states.Length - 1);
       ChunkMeshingStateDTO state = states[stateIndex];
       int vertCount = state.VertexCount;
       int indexCount = state.IndexCount;

       if (vertCount <= 0 || indexCount <= 0)
       {
           volume.DisableColliderChunksForCinematicFake();
           return true;
       }

       int colliderChunkCount = ResolveColliderChunkCount(indexCount / 3);
       if (!volume.TryUsePrewarmedColliderChunkCapacity(colliderChunkCount))
       {
           volume.DisableColliderChunksForCinematicFake();
           return false;
       }

       // Bake PhysX collider mesh and assign to volume MeshCollider
       Mesh colliderMesh = AcquireVoxelPhysicsBakeMesh();
       if (colliderMesh == null)
       {
           volume.DisableColliderChunksForCinematicFake();
           return false;
       }

       try
       {
           // Populate mesh position & indices from canonical collider pass
           Vector3[] positions = new Vector3[vertCount];
           for (int i = 0; i < vertCount; i++)
           {
               positions[i] = colliderVertices[i].Position;
           }

           int[] indices = new int[indexCount];
           for (int i = 0; i < indexCount; i++)
           {
               indices[i] = (int)colliderIndices[i];
           }

           colliderMesh.Clear();
           colliderMesh.SetVertices(positions);
           colliderMesh.SetTriangles(indices, 0);

           int meshInstanceId = colliderMesh.GetInstanceID();
           Physics.BakeMesh(meshInstanceId, false);

           volume.ConfigureColliderChunkMesh(0, colliderMesh);
           volume.SetActiveColliderChunkCount(1);
           return true;
       }
       finally
       {
           ReleaseVoxelPhysicsBakeMesh(colliderMesh);
       }
   }
   ```

---

## 6. Verification Method

1. **Static Assembly Verification**:
   - Inspect `Assets/_Project/Scripts/World/VoxelSurfaceNets/Hecton8.World.VoxelSurfaceNets.asmdef` to confirm `"autoReferenced": true`.
   - Inspect `Assets/_Project/Scripts/HectonVoxelEngine.cs` to confirm `using Hecton8.World.VoxelSurfaceNets;` and definition of `ApplySurfaceNetsColliderMeshesAsync`.
2. **Buffer Protection Audit**:
   - Verify `ExtractionJobMutationGuardMask` in `VoxelSurfaceNetsVault.cs` includes `81000`, `81001`, `81002`.
3. **Dual-Mesh Integrity Check**:
   - Verify `SurfaceNetExtractionJob` with `IsCanonicalCollider == true` produces `stride = 1` and `decimationBias = 0.0f`.
   - Verify visual pass (`IsCanonicalCollider == false`) scales `stride` and `decimationBias` based on `GlobalQualityWeight`.
