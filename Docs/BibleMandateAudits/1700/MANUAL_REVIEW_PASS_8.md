# Manual Review Pass 8 - Vegetation, HUD, Physics Culling, VFX Runtime Asset Boundaries

Status: STATIC METHOD REVIEW - NO UNITY/PROFILER PROOF
Date: 2026-06-02

## Mandates Applied

- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`: runtime hot paths must not allocate, grow buffers, build strings, or mutate mesh/material resources without a proven cold owner window.
- `.agents-skills/REND_Instanced_Flora_Physics.txt`: flora instance data must be preallocated per chunk, GPU/BRG owned, and must not generate or upload unchanged payloads per frame.
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`: RenderGraph paths, SRP batching, no material clones, no blocking readback, and compact GPU proof are required before render-system acceptance.
- `.agents-skills/AI_DYNAMIC_NAVGRID_SDF_INTEGRATION.txt`: dynamic navgrid work must be worker-owned, double buffered, and preallocated; main-thread blocking and per-frame allocation are forbidden.
- `.agents-skills/PHYS_Physics_Integrity_Determinism_ForceMode.txt`: physics state changes, collider state, and job completion windows need deterministic owner phases and profiler proof.
- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`: UI hierarchy/material/text updates must prove no post-bootstrap growth, no hot text allocation, and no repeated material construction.

## Reviewed Files

- `Assets/_Project/Scripts/World/HectonIndirectVegetationRenderer.cs`
- `Assets/_Project/Scripts/World/HectonProceduralVegetationStripBuilder.cs`
- `Assets/_Project/Scripts/World/VegetationChunkResidencyDirector.cs`
- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs`
- `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs`
- `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs`
- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`
- `Assets/_Project/Scripts/Visor/HectonVolumetricParticulateFogFeature.cs`
- `Assets/_Project/Scripts/Visor/HectonVisorARStencilRendererFeature.cs`
- `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs`
- `Assets/_Project/Scripts/VFX/NativeTrailRenderer.cs`
- `Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs`
- `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs`

## Findings

### 1. Indirect Vegetation Runtime Mesh Fallback Is Stronger Than Previously Recorded

`HectonIndirectVegetationRenderer.cs:189-190` documents an optional authored near mesh, but `:230-233` sets `_generateMeshAtRuntime = true` by default. The far impostor route mirrors this: `:253-258` documents an optional authored impostor and defaults `_generateImpostorMeshAtRuntime = true`. Setup then calls `HectonProceduralVegetationStripBuilder.Build(...)` when `_generateMeshAtRuntime || _mesh == null` at `:1691-1698`, and calls `BuildImpostorCardMesh()` when `_generateImpostorMeshAtRuntime || _impostorMesh == null` at `:1701-1702`.

`HectonProceduralVegetationStripBuilder.cs:20-31` allocates managed vertex, normal, tangent, UV, color, and index arrays, then creates a `new Mesh` at `:79-83`. It correctly encodes a tip mask into vertex color R at `:46` and `:60-61`, but G/B/A are all `255`, so it does not satisfy the complete organic vertex channel bible by itself: green bioluminescence phase and blue AO are not authored values. `BuildImpostorCardMesh()` in `HectonIndirectVegetationRenderer.cs:6453-6501` also creates a runtime `new Mesh` plus managed arrays.

Classification: `P0_RUNTIME_FLORA_MESH_FALLBACK_DEFAULT_ENABLED`. This does not prove every prefab uses the fallback, but it proves the fallback is not an edge-only null path. Release closure needs authored near/impostor mesh assignment, flags disabled, and prefab/build proof; otherwise the project can ship runtime-generated strip/card vegetation.

### 2. Vegetation Chunk And MapMagic Bridge Allocation Windows Need Pool Proof

`VegetationChunkResidencyDirector.cs:465-467` allocates three job record arrays during chunk build. `:480-484` allocates an H8Memory threat echo copy. `:786-800` allocates sand, rock, and height payloads for chunk jobs. Finalization at `:862-866` uses non-forced `DispatcherJobSwap.TryComplete(... forceComplete: false)`, which is good owner-shape but not proof that allocation windows are bounded.

`HectonMapMagicVegetationBridge.cs:5559-5568` allocates persistent job record arrays. `:5576-5591` creates managed matrix/data/int arrays for finalized chunk payloads. Teardown routes use forced completion: `CompleteAndReleaseChunkBuildJob()` calls `DispatcherJobSwap.TryComplete(... forceComplete: true)` at `:5662-5666`, while dispose helpers force-complete dependency disposal at `:5702-5712`, `:5726-5735`, and `:5766-5775`.

Classification: `YELLOW_CHUNK_STREAMING_ALLOCATION_AND_TEARDOWN_FENCE_PROOF_REQUIRED`. Static context looks like chunk build / tile teardown rather than per-frame hot work, but mandates require evidence: pool/preallocation or a chunk stream stress capture proving no uncontrolled post-bootstrap allocation/growth and no healthy-gameplay forced fence stalls.

### 3. Voxel Dynamic NavGrid Uses Registered Colliders, But Still Needs Obstacle Churn Proof

`VoxelDynamicNavGridRuntime.cs:1229-1248` registers module obstacles as arrays of `BoxCollider` and `CapsuleCollider`, stores them in fixed registration slots, and marks all volumes dirty. `:1259-1296` counts and writes registered collider bounds plus macro flora and persistent dynamic obstacles into a snapshot. `:1299-1324` initializes a persistent snapshot pool. `:1452-1464` completes pending dynamic obstacle updates with non-forced `DispatcherJobSwap.TryComplete(... forceComplete: false)`. Bounds are read from cached collider arrays at `:3160-3220`, not through scene-wide search.

Classification: `YELLOW_REGISTERED_COLLIDER_NAVGRID_PROOF_REQUIRED`. This is structurally better than scene scanning, but release acceptance still needs primitive-count caps, dirty-cadence proof, no repeated pool growth after bootstrap, and path/nav stress showing no hidden main-thread stalls.

### 4. Physics Culling Caches And Toggles Colliders At Runtime

`GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs:1389-1406` caches sleep colliders through `body.GetComponentsInChildren(false, _sleepColliderScratch)` into fixed arrays. `:1409-1449` disables and restores collider `enabled` flags by distance sleep state. The job writes command bits for sleep, kinematic, and mesh-collider strip at `:2688-2723`. Black-box dump payload allocation at `:2193-2227` uses `Allocator.Temp`; the surrounding method reads as a dump/fault path, not normal physics cadence.

Classification: `YELLOW_PHYSICS_CULLING_COLLIDER_TRANSITION_PROOF_REQUIRED`. The fixed scratch list is better than alloc-array component retrieval, and collider toggles may be a valid cinematic cheat for far physics load. It still needs transition-count telemetry, PhysX stability proof, no per-frame cache rebuilding, and confirmation that mesh-collider strip/restore commands do not hide LOD0 MeshCollider usage.

### 5. Suit HUD Runtime Materials And Hierarchy Assembly Remain Proof Gates

`SuitHUDV4CanvasOverlay.cs:2653-2675` builds threat chevron runtime resources and creates `new Material(threatChevronShader)`. `:2684-2704`, `:2728-2748`, and `:2788-2804` create dithered background, saving pulse, and acoustic radar runtime materials. `:3934-4028` adds `CanvasGroup`, `RectMask2D`, `CanvasRenderer`, and isolated `Canvas` components if missing. `:5390-5500` constructs gauge and quickbar hierarchy through runtime `AddComponent` calls, and `:7904-7934` creates a content root GameObject if absent.

Classification: `YELLOW_UI_BOOTSTRAP_ASSEMBLY_AND_MATERIAL_PROOF_REQUIRED`. The code is written as cold assembly and has several retained buffers, but the UI/menu bible cannot accept it without prefab assignment proof or 300-frame interaction captures showing no post-bootstrap hierarchy/material growth, no Canvas rebuild spikes, and 0 B/frame text updates.

### 6. RenderGraph Feature Shape Exists, But VFX Runtime Asset Fallbacks Are Not Closed

`HectonVolumetricParticulateFogFeature.cs:746-755` uses `RecordRenderGraph`, which is structurally aligned with the Unity 6000 RenderGraph mandate. However, `:1720-1728` executes `BuildMockVolumetricLightsJob` synchronously, and `:2448-2458` creates fallback 3D textures for empty density/flow. This can be acceptable if it is a cold bridge/fallback, but it still needs feature lifecycle and GPU proof.

Runtime VFX fallback assets are present elsewhere: `HectonVisorARStencilRendererFeature.cs:1677-1688` creates a fallback stencil mesh; `HectonMarineSnowRenderer.cs:4300-4312` creates a legacy indirect quad mesh and `:4481` / `:4535` creates render textures; `NativeTrailRenderer.cs:260-274` creates trail arrays and a dynamic mesh; `ShinobuPlasmaBeamRuntime.cs:1179-1182` creates a runtime material; `CarveDebrisComputeRenderer.cs:1842-1881` creates private/fallback debris materials and `:2365-2381` builds a fallback octahedron mesh.

Classification: `YELLOW_VFX_RUNTIME_ASSET_AND_GPU_LIFECYCLE_PROOF_REQUIRED`. Dynamic trails, render textures, and GPU effects can be legitimate runtime presentation systems, but the release gate is strict: authored/default assets where possible, fixed material/mesh counts, no repeated creation under enable/disable/reload, compact/high GPU captures, and no fallback low-poly debris/visor geometry as normal production visuals.

## Blocker Changes From Pass 8

- Strengthen `RB-002`: indirect vegetation runtime mesh fallback is default-enabled and creates managed arrays plus `new Mesh` for near strip and far impostor unless production prefabs override it.
- Strengthen `RB-006` / `RB-116`: vegetation chunk and MapMagic bridge allocations include chunk payload arrays, H8Memory copies, managed finalized arrays, and forced completion in teardown/disposal routes.
- Strengthen `RB-102`: voxel navgrid looks structurally registered rather than scene-searched, but obstacle dirty cadence and snapshot pool growth still require stress proof.
- Add `RB-122`: physics culling collider cache/toggle proof.
- Add `RB-123`: VFX/runtime presentation mesh/material/RT fallback proof.
- Add `RB-124`: volumetric fog mock/fallback resource lifecycle proof.

## Current Honest Verdict

The bibles are directionally correct: they reject runtime generated art, placeholder materials, unbounded UI, and unproven GPU effects. The codebase is not release-green against those bibles. The strongest pass-8 mismatch is indirect vegetation because the runtime generation booleans are default true and the builder produces simple strip/card meshes in player code. The other findings are yellow proof gates rather than confirmed hot-path defects until profiler/build/prefab evidence exists.

