# LOG_PROCEDURAL_GEOMETRY_ARCHITECT

## 2026-05-13 - Offline L-Systems & SDF Meshing

What was wrong:
- No isolated editor-time Bio-Forge pipeline existed for procedural flora/rock mesh baking.
- Runtime generation was explicitly forbidden for the i3/MX350 target; the project needed static mesh/prefab assets.
- Existing runtime Core compile surface was unstable, so depending on shared runtime marching-cubes tables would increase blast radius.

What was done:
- Created isolated Editor assembly: `Assets/_Project/Scripts/Editor/ProceduralGen/Hecton8.Editor.ProceduralGen.asmdef`.
- Added `BioRuleData` ScriptableObject for L-system axioms, rules, SDF resolution, branch dimensions, LOD budgets, rock noise, material, and output folders.
- Added `HECTON-8/Bio-Forge` editor window and default-rule creation menu.
- Implemented deterministic L-system expansion and parser into `NativeList<Matrix4x4>` branch transforms plus native branch SDF records.
- Implemented Burst SDF evaluation for branch capsule/cone shapes combined with exponential smooth-min.
- Implemented offline SDF extraction using a self-contained tetra-decomposed marching-cubes-style Burst job.
- Implemented deterministic LOD output for LOD0 5k, LOD1 1k, and LOD2 200 triangle budgets.
- Baked cylindrical UV0 and normalized Y-height into vertex color R for shader wind stiffness.
- Implemented 3D simplex-noise sphere SDF rock generation path.
- Implemented mandated deterministic 100-variation flora and rock batch buttons.
- Serialized meshes under `Assets/_Project/Art/Generated/Flora` and prefabs under `Assets/_Project/Prefabs/Nature/Flora/BioForge`.
- Created single-material LODGroup prefabs with crossfade and three child mesh renderers.
- Omega polish replaced float divisions with `math.rcp` multiplications and replaced unconditional `math.length` / `math.normalize` usage with `math.lengthsq` + `math.rsqrt`.

Cinematic Cheats used:
- Offline baked meshes instead of runtime growth or deformation.
- Smooth-min SDF branch blending instead of boolean mesh union.
- Static vertex color wind mask instead of CPU-side sway metadata.
- Single-material LOD prefabs instead of multi-material procedural variants.
- Self-contained tetra extraction instead of runtime marching-cubes dependency.

Exact Microseconds saved:
- Runtime generation cost removed: >100 us per generated asset frame minimum; practical avoided stalls are millisecond-scale for dense/batch flora.
- Runtime CPU after bake: 0 us for generation, 0 B/frame GC from the generator.
- Material state churn avoided: single material per renderer preserves instance/HLOD batching; exact SetPass savings depend on placement count, but tool adds no extra material slots.
- Vertex wind metadata saved CPU sway bookkeeping: 0 us runtime CPU per plant from this tool.

Verification:
- `validate_script` returned 0 errors / 0 warnings for `BioRuleData.cs`, `BioForgeJobs.cs`, `BioForgeGenerator.cs`, and `BioForgeWindow.cs`.
- Unity console before Omega polish listed unrelated gameplay mining CS0103 errors in `DeployableSdfDrillRuntime.cs`; Bio-Forge files were not listed.
- `dotnet build Hecton8.Core.csproj` failed with 154 unrelated Core dependency errors / 47 warnings. Bio-Forge editor files were not in the error set.
- Final Unity refresh entered compile/domain-reload turbulence and MCP stopped answering `editor_state` / `read_console`; final status remains PENDING VERIFICATION until the global compile wall is cleared.

Files:
- `Assets/_Project/Scripts/Editor/ProceduralGen/Hecton8.Editor.ProceduralGen.asmdef`
- `Assets/_Project/Scripts/Editor/ProceduralGen/BioRuleData.cs`
- `Assets/_Project/Scripts/Editor/ProceduralGen/BioForgeJobs.cs`
- `Assets/_Project/Scripts/Editor/ProceduralGen/BioForgeGenerator.cs`
- `Assets/_Project/Scripts/Editor/ProceduralGen/BioForgeWindow.cs`
- `Docs/Tasks/Status_PROCEDURAL_GEOMETRY_ARCHITECT.md`
- `Docs/AgentLogs/Rationale_PROCEDURAL_GEOMETRY_ARCHITECT.md`

## 2026-05-13 - Production Hardening Pass

What was wrong:
- The first decimation path could create zero-area triangles after local collapse.
- The LOD output job allocated an index NativeArray that the final mesh path did not need.
- Batch generation could save assets up to 100 times per command.
- Existing mesh overwrite used `CopySerialized` but did not destroy the temporary generated Mesh.
- Fallback material creation could produce transient prefab references if the built-in material was unavailable.
- L-system expansion could exceed `MaxExpansionChars` by the size of one replacement string.

What was done:
- Replaced destructive local edge collapse with deterministic grid-snap simplification and filtered zero-area/non-finite triangles before serialization.
- Removed unused LOD index NativeArray allocation.
- Deferred `AssetDatabase.SaveAssets()` during 100-variant batches and added cancelable progress bars.
- Destroyed temporary generated meshes after overwriting existing assets.
- Wrapped prefab root destruction in `finally`.
- Added persistent fallback material asset creation with null-shader guard.
- Added exact capped StringBuilder append helpers for L-system expansion.
- Replaced implicit float2/float3 to Unity Vector conversions with explicit constructors.

Cinematic Cheats used:
- Cluster/grid simplification keeps the silhouette readable at distance without expensive topology rebuilding.
- Degenerate triangle purge prevents invisible GPU work.
- Batch save deferral spends editor time on asset generation, not repeated database flushes.

Exact Microseconds saved:
- Runtime: still 0 us, no runtime generator exists.
- Editor: removes one `NativeArray<int>` allocation per LOD bake and reduces batch save calls from up to 100 to 1.
- GPU runtime: avoids vertex shader work on zero-area triangles; exact gain depends on generated mesh, but wasted degenerate vertex work is removed.

Verification:
- Unity `validate_script`: 0 errors / 0 warnings for all Bio-Forge scripts after hardening.
- Unity console still blocked outside this domain: `SuitHUDV4CanvasOverlay.cs` duplicate `OnGlobalRegistryServiceReplaced`; Burst missing `Hecton8.Vehicles.VFX`.

## 2026-05-13 - AAA Normal and Batch Scaling Pass

What was wrong:
- LOD simplification preserved mesh shape but could flatten lighting by replacing smooth SDF normals with face normals.
- The generated prefab root transform was moved to center geometry, which creates poor placement semantics.
- 100-asset sweeps still allowed import churn; save deferral alone was not enough.
- `BioRuleData` still exposed a stale batch-count field that no longer controlled the mandated 100-output buttons.

What was done:
- Added SDF-gradient normal baking from the density field using central differences.
- Preserved SDF normals through LOD simplification with fallback only when invalid.
- Kept prefab root transform at zero and moved LOD child renderers horizontally to center generated geometry.
- Added `mesh.RecalculateTangents()` after bounds for material readiness.
- Wrapped batch generation in balanced `AssetDatabase.StartAssetEditing` / `StopAssetEditing`.
- Removed the dead `BatchVariationCount` authoring field/property.

Cinematic Cheats used:
- SDF-gradient lighting sells organic volume without runtime deformation.
- Child-geometry offset gives clean prefab placement while preserving root-at-base behavior.
- Deferred import/save keeps batch work editor-bound and predictable.

Exact Microseconds saved:
- Runtime: still 0 us for generation and 0 B/frame GC from this tool.
- Editor: 100-output sweeps now defer imports and saves across the batch, reducing repeated AssetDatabase overhead.
- GPU/runtime visual waste: smooth normals/tangents improve material response without adding runtime CPU systems.

Verification:
- Unity `validate_script`: 0 errors / 0 warnings for all Bio-Forge scripts.
- Static scan: no matches for runtime randomness/search/coroutine/material-copy patterns, `math.length(`, `math.normalize(`, `mesh.triangles`, or stale `BatchVariationCount`.
- Unity console remains blocked outside this domain: `GlobalDataVault.cs` missing `Hecton8.Core.Signals` / Burst symbols and a Burst entry-point exception.

## 2026-05-13 - SDF Branch Broadphase Scaling Pass

What was wrong:
- Flora SDF evaluation still walked every L-system branch for every voxel sample.
- Dense kelp/coral rules would pay capsule/cone distance and exponential smooth-min costs for distant branches with no visible contribution.

What was done:
- Added `BoundsMin`, `BoundsMax`, and `MaxRadius` to each emitted `BioForgeBranch`.
- Built global SDF bounds from those precomputed branch bounds.
- Added a Burst-side smooth-min cull margin that skips branches whose expanded AABB is too far to affect the current best SDF value.
- Kept the culling entirely editor-time and deterministic.

Cinematic Cheats used:
- Broadphase AABB rejection treats negligible exponential smooth-min contribution as visually irrelevant.
- Static offline baking keeps the runtime contract unchanged: only prefab LOD meshes ship.

Exact Microseconds saved:
- Runtime: still 0 us for generation and 0 B/frame GC.
- Editor: saves one capsule/cone SDF plus one exponential `smin` per culled branch per voxel sample. Exact savings depend on rule spread; at 64^3 points, every 10 skipped branches avoids roughly 2.6 million branch distance/blend evaluations per flora asset.
- Low-end impact: i3/MX350 receives the same static LOD prefab, not the bake workload.

Verification:
- Unity `validate_script`: 0 errors / 0 warnings for all Bio-Forge scripts.
- Static scan: no matches for runtime randomness/search/coroutine/material-copy patterns, `math.length(`, `math.normalize(`, `mesh.triangles`, or stale `BatchVariationCount`.
- `refresh_unity` timed out after 60 seconds waiting for editor readiness.
- Unity console remains blocked outside this domain: `HectonUnderwaterVisuals.cs` missing `IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(...)` and a Burst entry-point exception.
