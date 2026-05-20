# SHINOBU_228 Status - BUILDER_TOOL_HOLOGRAPHY_SYNC

Date: 2026-05-20
Domain: Habitat Builder Tool visual representation and placement validation
Assignment Source: `Docs/Tasks/CURRENT_BATCH.md` / `<AGENT_PROMPT id="SHINOBU_228">`
Task Count: 20
Status: PENDING VERIFICATION

## Mandates Read

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `MATH_AUP_Determinism_Sync.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `DATA_Runtime_Struct_Layout_ARM64.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `REND_GPU_Sovereignty.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt`
- `ARCH_Execution_Phases.txt`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`

## Pre-Code Analysis

[ANALYSIS]
Target: Replace habitat builder ghost-prefab preview authority with data DTO, Burst math validation, and GPU buffer backed hologram rendering.
Affected systems: `PlayerBuilder`, `PlacementGhost`, `Construction/ShinobuSocketConstructionData.cs`, `Construction/ShinobuSocketConstructionJobs.cs`, `Construction/HectonBlueprintPreviewBatch.cs`, `Hecton_ConstructionDearLieHologram.shader`, editor/static scanners.
Zero GC proof: hot path must not allocate managed arrays, strings, materials, GameObjects, MaterialPropertyBlock, or physics result arrays; job DTOs are unmanaged and buffers are Vault/GraphicsBuffer backed.
State check: existing builder directory from prompt is absent; actual owner files are root `BuilderTool.cs`, `PlayerBuilder.cs`, root `PlacementGhost.cs`, and `Construction/` systems. Existing ghost object path still spawns/despawns pooled prefabs/runtime proxies and runs a PhysX overlap check. Existing socket math already uses Burst and AUP but still drives a GameObject ghost transform.
Rule quote: "VISUAL_SYNC consumes stable simulation snapshots and updates presentation"; "AUP is the only simulation-scale spatial authority"; "MaterialPropertyBlock on standard geometry forbidden; use GraphicsBuffer for GPU Instanced/BRG geometry"; "NativeArray initializations use UninitializedMemory when fully written before read."
[/ANALYSIS]

## Loop 1 - Tasks 01-05

- [ ] Task 01 PREFAB_INSTANTIATION_INQUISITION | Pending. DOD: remove preview ghost spawn/despawn as placement authority; reject prefab lifecycle as hot preview surface. Estimate: 120-180 us saved per equip spike plus GC risk removal.
- [ ] Task 02 PHYSICS_OVERLAP_ERADICATION | Pending. DOD: replace `PlacementGhost.FixedTick` overlap validity with Burst SDF/AABB flags; reject moving collider broadphase. Estimate: 60-180 us/frame saved during preview.
- [ ] Task 03 CS1612_HOT_PATH_PROPERTY_ANNIHILATION | Pending. DOD: DTOs expose raw unmanaged fields; ref/pointer helpers for large DTOs; reject C# property DTO reads in jobs. Estimate: 5-20 us/job batch saved via SIMD-friendly access.
- [ ] Task 04 ARM64_GHOST_ALIGNMENT_ASSERTION | Pending. DOD: explicit 128-byte `BuilderGhostStateDTO`, field offsets audited with editor static scanner; reject sequential layout ambiguity. Estimate: correctness gate, prevents unaligned double3 trap.
- [ ] Task 05 EMERGENCY_MOCK_VALIDATION_GENERATOR | Pending. DOD: Burst `IJobParallelFor` mock 10,000 matrices/SDF checks; reject manual scene-only repro. Estimate: profiling isolation, no runtime claim without Unity artifact.

## Loop 2 - Tasks 06-10

- [ ] Task 06 BURST_AUP_SNAPPING_KERNEL | Pending. DOD: double3 grid snap before float cast, 90-degree rotation snap, camera AUP local matrix. Estimate: 20-45 us/frame target.
- [ ] Task 07 SDF_BASED_VALIDATION_MATH | Pending. DOD: 8 OBB corners against SDF and module AABBs in Burst; reject PhysX/trigger validation. Estimate: 40-120 us/frame target depending candidate count.
- [ ] Task 08 THE_DEAR_LIE_HOLOGRAPHIC_PROJECTION | Pending. DOD: StructuredBuffer DTO, indirect draw path; reject GameObject preview rendering. Estimate: CPU draw prep under 0.1 ms target.
- [ ] Task 09 ASYNCHRONOUS_GPU_UPLOAD_DISPATCHER | Pending. DOD: double-buffered LockBufferForWrite upload; reject `GraphicsBuffer.SetData`. Estimate: avoids sync stall, 10-60 us saved on MX350.
- [ ] Task 10 CONTINUOUS_SCALABILITY_HOLO_MATH | Pending. DOD: `GlobalQualityWeight` scales shader ALU continuously; reject binary low/high switch. Estimate: saves fragment ALU under pressure while preserving valid math.

## Loop 3 - Tasks 11-15

- [ ] Task 11 SOCKET_MAGNETISM_OVERRIDE | Pending. DOD: Burst socket distance/alignment override using existing socket catalog buffers; reject transform/socket collider searches. Estimate: 20-80 us/frame vs scene scan.
- [ ] Task 12 AUP_PRECISION_DELTA_MATH | Pending. DOD: double3 delta first, then local float3; reject absolute world float distances. Estimate: correctness gate at 50km+.
- [ ] Task 13 ROLLBACK_NETCODE_EXCLUSION_FENCE | Pending. DOD: preview DTO marked presentation-only, excluded from replay/Merkle hashing docs/scanner. Estimate: prevents false desync.
- [ ] Task 14 ZERO_INIT_OVERHEAD_BYPASS | Pending. DOD: Vault buffers use `NativeArrayOptions.UninitializedMemory` and full writes; reject MemClear. Estimate: 1-10 us/init/update saved per small buffer.
- [ ] Task 15 TELEMETRY_HOLOGRAPHY_RECORDER | Pending. DOD: 300-entry ring and dump path `Dump_SHINOBU_228.bin`; reject "unknown crash" path. Estimate: forensic coverage, threshold >0.5 ms.

## Loop 4 - Tasks 16-20

- [ ] Task 16 HOLOGRAPHY_TUNER_WINDOW | Pending. DOD: UI Toolkit editor window reads telemetry/tuning DTO; reject runtime string/alloc hot path. Estimate: editor-only.
- [ ] Task 17 CSV_BUILDER_PROFILES_INGESTOR | Pending. DOD: cold `ReadOnlySpan<byte>` parser with FNV-1a and manual float parse; reject `string.Split` and `float.Parse`. Estimate: cold boot only, avoids managed parse churn.
- [ ] Task 18 LIVE_BOUNDS_DEBUG_GIZMO | Pending. DOD: editor gizmo draws OBB/corner validity from DTO; reject physics collider proof. Estimate: editor-only.
- [ ] Task 19 ARCHITECTURAL_METRIC_VALIDATOR | Pending. DOD: static scanner emits `Docs/Reports/MEMORY_OPTIMIZATION_REPORT.json`; reject chat-only evidence. Estimate: static proof.
- [ ] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | Pending. DOD: layout, GC-static, GPU fence, and exclusion audit. Estimate: acceptance gate.

## Iteration Log

- [ ] Iteration 1 | Pending. Archaeology and local sanitation.
- [ ] Iteration 2 | Pending. Core DTO/jobs/GPU upload.
- [ ] Iteration 3 | Pending. Socket/SDF/AUP precision and telemetry.
- [ ] Iteration 4 | Pending. Editor facade/scanner/docs.
- [ ] Iteration 5 | Pending. Compile/static audit/self-review.
