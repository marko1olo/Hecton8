# Technical Review & Adversarial Audit Report: Voxel Surface Nets Terrain & Cave Collision Pipeline (Milestone 4)

**Reviewer**: teamwork_preview_reviewer  
**Working Directory**: `C:\hades\Hecton8\.agents\reviewer_m4`  
**Date**: 2026-07-27  
**Authority Reference**: `AGENTS.md` (Spine authority & Zero Mocks / Integrity rule), `PROJECT_BIBLES.md`, `SYSTEMS_CONTRACTS.md`, `voxels.md`, `GEMINI.md`.

---

## Review Summary

**Verdict**: **REJECT**

**Authority Used**: `AGENTS.md`; `PROJECT_BIBLES.md`; `SYSTEMS_CONTRACTS.md`; `voxels.md`; `H8Memory.cs`; `VoxelSurfaceNetsContracts.cs`; `VoxelSurfaceNetsVault.cs`; `VoxelSurfaceNetsJobs.cs`; `HectonVoxelEngine.cs`.

The Voxel Surface Nets collision pipeline implementation fails Requirement R4 (dedicated non-aliased `BufferID` in `H8Memory.cs` for `PhysicsBakeRequests`) and contains a critical consumption defect in `ApplySurfaceNetsColliderMeshesAsync` that breaks dual-mesh LOD isolation during physics mesh baking.

---

## 1. Observation

### Observation 1: BufferID Aliasing Violation for `PhysicsBakeRequests` (Requirement R4 Failure)
- **File**: `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsContracts.cs`, Line 66:
  ```csharp
  public const BufferID PhysicsBakeRequests = BufferID.ShinobuFluidCsvScratch;
  ```
- **File**: `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`, Lines 141–143:
  ```csharp
  VoxelSurfaceNetsColliderVertices = 81000,
  VoxelSurfaceNetsColliderIndices = 81001,
  VoxelSurfaceNetsColliderCellVertexMap = 81002,
  ```
- **Fact**: `H8Memory.cs` lacks a dedicated `VoxelSurfaceNetsPhysicsBakeRequests = 81003` entry. `VoxelSurfaceNetsContracts.cs` aliases `PhysicsBakeRequests` to `BufferID.ShinobuFluidCsvScratch` (71521).
- **Requirement Violation**: R4 explicitly specifies: *"Confirm dedicated non-aliased BufferIDs in H8Memory.cs (VoxelSurfaceNetsColliderVertices, Indices, CellVertexMap, PhysicsBakeRequests)."*

### Observation 2: Dual-Mesh LOD Isolation Mismatch in `ApplySurfaceNetsColliderMeshesAsync` (Requirement R3 / R1 Defect)
- **File**: `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsJobs.cs`, Lines 264–284:
  ```csharp
  if (IsCanonicalCollider)
  {
      if (PhysicsBakeRequests.IsCreated)
      {
          VoxelSurfacePhysicsBakeRequestDTO req = PhysicsBakeRequests[stateIndex];
          req.ColliderVertexCount = vertexCount;
          req.ColliderIndexCount = indexCount;
          if (indexCapacityClamped)
              req.Flags = (byte)(req.Flags | VoxelMeshingFlags.CapacityClamped);
          PhysicsBakeRequests[stateIndex] = req;
      }
  }
  else
  {
      if (indexCapacityClamped)
          state.Flags = (byte)(state.Flags | VoxelMeshingFlags.CapacityClamped);
      state.VertexCount = vertexCount;
      state.IndexCount = indexCount;
      state.RawDebugVertexCount = rawDebugCount;
      state.Stage = (byte)VoxelMeshingStage.ReadyForUpload;
      States[stateIndex] = state;
  }
  ```
- **File**: `Assets/_Project/Scripts/HectonVoxelEngine.cs`, Lines 14067–14080:
  ```csharp
  NativeArray<VoxelVertexDTO> colliderVertices = buffers.ColliderVertices;
  NativeArray<uint> colliderIndices = buffers.ColliderIndices;
  NativeArray<ChunkMeshingStateDTO> states = buffers.States;
  ...
  int stateIndex = math.clamp(data.VolumeIndex, 0, states.Length - 1);
  ChunkMeshingStateDTO state = states[stateIndex];
  int vertCount = state.VertexCount;
  int indexCount = state.IndexCount;
  ```
- **Fact**: In `SurfaceNetExtractionJob`, canonical collider mesh counts are written to `PhysicsBakeRequests[stateIndex].ColliderVertexCount` and `ColliderIndexCount`. `state.VertexCount` and `state.IndexCount` store ONLY the visual pass mesh counts (which scale down with `GlobalQualityWeight` LOD/stride/decimation). `ApplySurfaceNetsColliderMeshesAsync` accesses `buffers.ColliderVertices` and `buffers.ColliderIndices` (canonical buffers), but reads mesh slice bounds `vertCount` and `indexCount` from `state.VertexCount` and `state.IndexCount` (visual pass counts).

### Observation 3: Unscheduled Bake Job helper `TrySchedulePhysicsBakeRequestsPinned` (Requirement R2 Defect)
- **File**: `Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsVault.cs`, Lines 754–784.
- **Search Command**: `grep_search` across `Assets/_Project/Scripts` for `TrySchedulePhysicsBakeRequestsPinned`.
- **Result**: `TrySchedulePhysicsBakeRequestsPinned` is defined in `VoxelSurfaceNetsVault.cs`, but is never invoked anywhere in `HectonVoxelEngine.cs` or runtime meshing dispatchers.

---

## 2. Logic Chain

1. **Step 1 (Requirement R4)**: R4 mandates dedicated non-aliased `BufferID`s in `H8Memory.cs` for `VoxelSurfaceNetsColliderVertices`, `ColliderIndices`, `ColliderCellVertexMap`, and `PhysicsBakeRequests`. Inspection of `H8Memory.cs` shows only 81000, 81001, and 81002 are defined. `VoxelSurfaceNetsContracts.cs` line 66 points `PhysicsBakeRequests` to `BufferID.ShinobuFluidCsvScratch`. This is an memory aliasing violation between Shinobu fluid simulation memory and terrain physics bake memory.
2. **Step 2 (Requirement R3 & R1)**: `SurfaceNetExtractionJob` guarantees dual-mesh isolation by running `IsCanonicalCollider = false` (visual pass with dynamic LOD stride/decimation) and `IsCanonicalCollider = true` (canonical pass with invariant stride=1, decimation=0). The visual pass writes output counts to `States[stateIndex].VertexCount` / `IndexCount`, while the canonical pass writes output counts to `PhysicsBakeRequests[stateIndex].ColliderVertexCount` / `ColliderIndexCount`.
3. **Step 3 (Requirement R3 & R1)**: When `ApplySurfaceNetsColliderMeshesAsync` executes in `HectonVoxelEngine.cs`, it extracts canonical collider vertices from `buffers.ColliderVertices` and `buffers.ColliderIndices`, but slices them using `vertCount = state.VertexCount` and `indexCount = state.IndexCount`. When graphics quality decreases (LOD stride = 2 or 4), visual vertex count becomes 1/4 or 1/16 of canonical vertex count. `ApplySurfaceNetsColliderMeshesAsync` copies truncated vertex arrays while copying indices that reference higher vertex indices in canonical memory, leading to corrupted PhysX bakes or out-of-bounds index errors.
4. **Step 4 (Requirement R2)**: `TrySchedulePhysicsBakeRequestsPinned` was added to `VoxelSurfaceNetsVault.cs`, but `HectonVoxelEngine.cs` does not call `TrySchedulePhysicsBakeRequestsPinned` before calling `ApplySurfaceNetsColliderMeshesAsync`, leaving `PhysicsBakeRequests` unpopulated if `ApplySurfaceNetsColliderMeshesAsync` were to rely on `PhysicsBakeRequests`.
5. **Conclusion**: The implementation fails Requirements R4 and R3. Verdict must be **REJECT**.

---

## 3. Findings

### [Critical] Finding 1: Memory Aliasing Violation for `PhysicsBakeRequests` (Requirement R4)
- **What**: `PhysicsBakeRequests` is aliased to `BufferID.ShinobuFluidCsvScratch` instead of having a dedicated `BufferID` in `H8Memory.cs`.
- **Where**: `H8Memory.cs` (lines 141-143) & `VoxelSurfaceNetsContracts.cs` (line 66).
- **Why**: Violates R4 and creates cross-system buffer overwrite risk with Shinobu fluid simulation.
- **Suggestion**:
  1. Add `VoxelSurfaceNetsPhysicsBakeRequests = 81003` to `H8Memory.cs`.
  2. Update `VoxelSurfaceNetsContracts.cs` line 66:
     ```csharp
     public const BufferID PhysicsBakeRequests = BufferID.VoxelSurfaceNetsPhysicsBakeRequests;
     ```

### [Critical] Finding 2: Visual LOD Mesh Count Used for Canonical Physics Baking (Requirement R3 / R1)
- **What**: `ApplySurfaceNetsColliderMeshesAsync` reads `state.VertexCount` and `state.IndexCount` from `buffers.States` (visual LOD counts) to slice `buffers.ColliderVertices` and `buffers.ColliderIndices` (canonical max-quality buffers).
- **Where**: `HectonVoxelEngine.cs`, lines 14077–14080.
- **Why**: Breaks dual-mesh LOD isolation during physics baking when graphics quality changes (`GlobalQualityWeight` scaling).
- **Suggestion**:
  Update `ApplySurfaceNetsColliderMeshesAsync` to read collider mesh counts from `buffers.PhysicsBakeRequests`:
  ```csharp
  NativeArray<VoxelSurfacePhysicsBakeRequestDTO> bakeRequests = buffers.PhysicsBakeRequests;
  if (!bakeRequests.IsCreated || stateIndex >= bakeRequests.Length)
  {
      volume.DisableColliderChunksForCinematicFake();
      return false;
  }
  VoxelSurfacePhysicsBakeRequestDTO bakeReq = bakeRequests[stateIndex];
  int vertCount = bakeReq.ColliderVertexCount;
  int indexCount = bakeReq.ColliderIndexCount;
  ```

### [Major] Finding 3: `TrySchedulePhysicsBakeRequestsPinned` Disconnected from Engine Pipeline (Requirement R2)
- **What**: `TrySchedulePhysicsBakeRequestsPinned` is defined in `VoxelSurfaceNetsVault.cs` but never called in `HectonVoxelEngine.cs`.
- **Where**: `VoxelSurfaceNetsVault.cs` (line 754) vs `HectonVoxelEngine.cs`.
- **Why**: `PhysicsBakeRequests` array fields (`ColliderVertexCount`, `MeshId`, etc.) are not initialized by physics bake jobs prior to engine collider mesh application.
- **Suggestion**: Ensure `TrySchedulePhysicsBakeRequestsPinned` is properly integrated into the job scheduling flow in `HectonVoxelEngine.cs` prior to `ApplySurfaceNetsColliderMeshesAsync`.

---

## 4. Verified Claims Matrix

| Claim / Requirement | Status | Verification Method |
| :--- | :--- | :--- |
| **R1**: `autoReferenced: true` in asmdef | **PASS** | Inspected `Hecton8.World.VoxelSurfaceNets.asmdef` line 18. |
| **R1**: `using Hecton8.World.VoxelSurfaceNets;` present | **PASS** | Inspected `HectonVoxelEngine.cs` line 28. |
| **R1**: `ApplySurfaceNetsColliderMeshesAsync` signature | **PASS** | Inspected `HectonVoxelEngine.cs` line 14048. |
| **R2**: Volume-to-Chunk mapping bridge | **PASS** | Inspected `TryScheduleExtractionPinned` & `data.VolumeIndex` clamping. |
| **R2**: `SurfaceNetExtractionJob` visual + collider scheduled | **PASS** | Inspected `VoxelSurfaceNetsVault.cs` lines 730–740. |
| **R2**: `VoxelSurfacePhysicsBakeRequestJob` scheduled | **PARTIAL** | Defined in `VoxelSurfaceNetsVault.cs` line 754, but unscheduled in engine flow. |
| **R3**: Extraction LOD scaling vs canonical invariant max-quality | **PASS (Job)** | Inspected `VoxelSurfaceNetsJobs.cs` lines 117-120 (`IsCanonicalCollider ? 1 : stride`). |
| **R3**: Dual-Mesh consumption in `ApplySurfaceNetsColliderMeshesAsync` | **FAIL** | Inspected `HectonVoxelEngine.cs` lines 14078-14080 (uses visual `state.VertexCount` for canonical buffers). |
| **R4**: Dedicated non-aliased BufferIDs in `H8Memory.cs` | **FAIL** | `PhysicsBakeRequests` missing in `H8Memory.cs`, aliased to `ShinobuFluidCsvScratch` in `VoxelSurfaceNetsContracts.cs`. |
| **R4**: Write leases & mutation guard mask for collider buffers | **PASS** | Inspected `VoxelSurfaceNetsVault.cs` lines 198–208 & 508–517. |

---

## 5. Coverage Gaps

- **Play Mode Runtime Bake Performance**: Verified via static analysis and AST checks; live frame time profiling during PhysX mesh baking requires Unity Editor Play Mode.

---

## 6. Unverified Items

- None. All requested source files and requirements were fully inspected and audited.

---

## 7. Conclusion

**Verdict**: **REJECT**

The implementation cannot be approved in its current state due to:
1. Integrity violation of Requirement R4 (aliased `BufferID.ShinobuFluidCsvScratch` used for `PhysicsBakeRequests` instead of a dedicated non-aliased `BufferID` in `H8Memory.cs`).
2. Defect in Requirement R3 dual-mesh isolation consumption (`ApplySurfaceNetsColliderMeshesAsync` slicing canonical collider buffers with visual LOD `state.VertexCount`).

To pass review, worker must address Finding 1, Finding 2, and Finding 3.

---

## 8. Verification Method for Re-Review

1. Inspect `H8Memory.cs` to confirm `VoxelSurfaceNetsPhysicsBakeRequests = 81003` exists.
2. Inspect `VoxelSurfaceNetsContracts.cs` line 66 to confirm `public const BufferID PhysicsBakeRequests = BufferID.VoxelSurfaceNetsPhysicsBakeRequests;`.
3. Inspect `HectonVoxelEngine.cs` line 14077–14085 to confirm `vertCount` and `indexCount` are read from `buffers.PhysicsBakeRequests[stateIndex]`.
4. Run `python -B Tools/AssemblyDependencyAudit.py` to confirm zero dependency cycles.
