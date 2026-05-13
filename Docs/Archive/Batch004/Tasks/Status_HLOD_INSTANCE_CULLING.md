# Status_HLOD_INSTANCE_CULLING

Batch prompt: `HLOD_INSTANCE_CULLING`
Agent role: `GPU_INSTANCER_ARCHITECT`
Domain: ECHELON 2 / BRG Scatter Director GPU instancing and compute culling
Task count: 19
Status: PENDING UNITY IMPORT / CORE IMPLEMENTED

## Loop 0 - Initialization

- [x] Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md` using CLI regex from cover to cover. DOD: strict prompt isolation. Alternative rejected: relying on IDE tab or truncated MCP read. Estimate: 25 us.
- [x] Relevant mandates read: `REND_GPU_Occlusion_Culling_6000`, `REND_GPU_Sovereignty`, `REND_URP_Graphics_HotPath_Optimization_HLOD`, `GPU_Compute_Kernels_Kernels_Optimization_MX350`, `GPU_Compute_Warp_Sizing_Mobile`, `REND_Instanced_Flora_Physics`, `MATH_Coordinate_Precision_AUP_FloatingOrigin`, `ARCH_Global_Registry_ServiceLocator_DI_Init`, `OPT_Zero_GC_Policy_AllocFree_Mandate`. DOD: mandate-gated before code. Alternative rejected: generic Unity instancing from memory. Estimate: 40 us.

## Tasks

- [x] 1. SINGLETON ERADICATION: `FloraCullingManager.Instance` scan returned no infection; `IInstanceCullingService` registered through `GlobalRegistry` slot `InstanceCullingRuntime`. DOD: registry contract, hot-swap safe. Alternative rejected: direct singleton/component lookup. Estimate: 12 us saved per lookup path.
- [x] 2. SIGNAL MIGRATION: `InstanceCullingService` consumes `CameraPositionSignal` and `CameraFrustumSignal` payloads with no `Camera.main` polling. DOD: signal payload ownership. Alternative rejected: runtime camera dependency. Estimate: 8 us saved per dispatch setup.
- [x] 3. ASMDEF ISOLATION: `Hecton8.Graphics.Culling.asmdef` references `Hecton8.World.Contracts` plus Unity package assemblies only. DOD: project dependency isolation. Alternative rejected: Core/World renderer coupling. Estimate: 0 us runtime, lower compile blast radius.
- [x] 4. DEAD CODE HUNT: `rg` found no `FloraCullingManager.Instance`; `math.distancesq` hits in flora files are interaction/regrowth, not static flora render cull. Existing BRG CPU fallback was not widened. DOD: evidence scan. Alternative rejected: risky renderer rewrite outside prompt. Estimate: avoided unbounded integration cost.
- [x] 5. COMPUTE KERNEL: Added `InstanceCulling.compute` using `StructuredBuffer<float4x4>` input and `AppendStructuredBuffer<float4x4>` visible output. DOD: GPU append path. Alternative rejected: CPU matrix compaction. Estimate: 180-450 us CPU/PCIe saved at 100k instances, pending profiler capture.
- [x] 6. FRUSTUM PLANES: Six planes passed as shader constants; plane checks use dot-product sphere masks. DOD: branch-reduced frustum mask. Alternative rejected: CPU frustum loop. Estimate: 90-220 us CPU saved at 100k instances.
- [x] 7. DISTANCE FADE: Default/max cull distance clamps to 200m. DOD: hard distance gate. Alternative rejected: unlimited kelp submission. Estimate: scene-dependent PCIe reduction.
- [x] 8. INDIRECT ARGS GENERATION: `GraphicsBuffer.CopyCount` writes append count into indirect args offset 4. DOD: zero same-frame CPU readback. Alternative rejected: `GetData`/readback-driven rendering. Estimate: avoids 0.2-2.0 ms stall risk.
- [x] 9. AUP SHIFT SAFETY: `ApplyAupShiftJob` offsets matrix translations with Burst after rare rebase signal. DOD: structural job, not render hot path. Alternative rejected: stale AUP coordinates or per-frame CPU rebuild. Estimate: avoids full buffer regeneration on shift.
- [x] 10. HI-Z PREPARATION: Optional `VoxelSdfTexture3D` culls instances inside solid rock as MX350 Hi-Z substitute. DOD: cinematic cheat. Alternative rejected: full Hi-Z depth pyramid ownership. Estimate: saves GPU overdraw where cave rock occludes dense flora.
- [x] 11. DYNAMIC BATCHING OVERRIDE: Service owns append visible buffer and indirect args for procedural/manual-BRG props only. DOD: compute pipeline authority boundary. Alternative rejected: competing Unity dynamic/static batching. Estimate: prevents duplicate submission.
- [x] 12. WIND SWAY DATA: Shader preserves full matrix payload; radius/sway-compatible spare component `m31` is reserved as packed per-instance scalar. DOD: no extra buffer bind. Alternative rejected: extra structured seed buffer. Estimate: one bind and memory stream avoided.
- [x] 13. ZERO-GC: Dispatch path uses persistent buffers, cached readback callback, and no hot-path managed allocations. DOD: allocation scan. Alternative rejected: per-frame arrays/lists. Estimate: prevents GC spikes; exact us non-applicable.
- [x] 14. MATH LOD: Low tier forces 100m cull distance. DOD: toaster path. Alternative rejected: one-size 200m range. Estimate: up to 50-75% visible instance reduction in dense exterior scenes.
- [x] 15. VRAM BUDGET ABORT: `VramUsedMb > 1600` sets downsample flag; shader rejects odd instance IDs. DOD: deterministic half-rate visual survival. Alternative rejected: OOM risk or random thinning. Estimate: halves procedural instance load under pressure.
- [x] 16. BLACKBOX DUMP: 300-frame native telemetry ring records source/visible/culled counts and dumps to `Docs/AgentLogs/Dump_HLOD_INSTANCE_CULLING.bin` on invalid state. DOD: fixed-size black box. Alternative rejected: console-only postmortem. Estimate: zero hot allocation.
- [x] 17. EVENT BUS: `InstanceCullingServiceRegistryBridge` drains telemetry and emits `CullingOverloadSignal` above 50,000 visible instances. DOD: NativeQueue signal lane. Alternative rejected: direct cross-system calls. Estimate: 0 us hot coupling, prevents dependency lock.
- [x] 18. CROSS-DOMAIN AUDIT: `FloraInteractionManager` caches `IInstanceCullingService`, hot-swaps on registry replacement, and publishes culled visible buffer globals for vertex sway. DOD: registry interface only. Alternative rejected: Graphics assembly reference from World interaction code. Estimate: one buffer stream reused.
- [x] 19. [BLOCKED BY UNITY SESSION] OMEGA COMPILE CHECK: `[numthreads(64,1,1)]` and C# `GetKernelThreadGroupSizes` are implemented; `dotnet build Hecton8.World.Contracts` passes; Unity shader import/compile cannot be verified because MCP reports `no_unity_session` and refresh waits timed out. DOD: source-level verification plus blocked import note. Alternative rejected: fake green compile report. Estimate: pending Unity import.

## Verification Log

- PASS: Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md` using CLI regex after implementation.
- PASS: `dotnet build Hecton8.World.Contracts.csproj --no-restore -m:2 /nr:false` completed with 0 warnings and 0 errors.
- PASS: `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:BuildProjectReferences=false` filtered for this task returned no `InstanceCulling`/`CullingOverloadSignal`/culled-flora errors. Full Core build remains broken by unrelated missing interfaces from concurrent agents.
- PASS: `git diff --check` on touched files reports line-ending warnings only, no whitespace errors.
- PASS: `rg` confirms no `FloraCullingManager.Instance` references.
- PASS: OMEGA polish audit found no managed `foreach`, `string.Format`, interpolation, `.ToString()`, `math.sqrt`, or `math.normalize` in the new culling service/bridge/contracts/shader set.
- RED GLOBAL: Full `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:BuildProjectReferences=false` fails with 109 unrelated errors from other domains (`BinaryBlittableSafe`, `SoundEmissionSignal`, `AcousticAup`, `AcousticPathResult`, etc.).
- BLOCKED: Unity MCP `refresh_unity`, `validate_script`, `read_console`, and `unity_reflect` attempts returned timeout or `no_unity_session`; compute shader import cannot be claimed.

## Iterative Loops

- Loop 1: Closed tasks 1-5. Re-read contracts/registry/kernel files. Miss caught: actual prompt id was `HLOD_INSTANCE_CULLING`, not agent name. Correction: strict XML extraction by prompt id.
- Loop 2: Closed tasks 6-10. Re-read compute shader and service. Miss caught: voxel indexing mixed uint/int constructor. Correction: explicit uint clamp then int cast.
- Loop 3: Closed tasks 11-15. Re-read dispatch hot path. Miss caught: implicit `Vector3` to `float3` validation casts. Correction: explicit float3 construction.
- Loop 4: Closed tasks 16-18. Re-read bridge and flora manager. Miss caught: Core must not depend on Graphics implementation. Correction: bridge uses serialized `MonoBehaviour` cast to contract.
- Loop 5: Closed task 19 as blocked. Re-read original prompt and compute shader. Miss caught: visibility bool chain was branch-heavy. Correction: plane/distance/SDF decisions flattened into float masks where practical.
