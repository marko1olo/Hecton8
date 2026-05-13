# Rationale_PROCEDURAL_GEOMETRY_ARCHITECT

Status: PENDING VERIFICATION

## Decision 0 - Tooling Boundary

Problem: Procedural kelp/coral/rock generation must not become runtime mesh generation on the i3/MX350 target.
Solution: Keep Bio-Forge entirely under an Editor assembly and write production `.asset` meshes and prefabs for runtime consumption.
Rejected Alternatives: Runtime MonoBehaviour generator, scene-time generator, or bootstrap-registered generator. All would violate the prompt and risk CPU stalls.
Scalability potential: Low uses coarse baked meshes and aggressive LOD2; Middle uses denser LOD0; High uses richer silhouettes; Ultra spends saved runtime cycles on shader detail, not CPU generation.
Hardware Impact: Estimated runtime cost stays 0 us on i3/MX350 because generation is offline.

## Decision 1 - SDF Blend Math

Problem: Branching organic forms need smooth joins without per-branch mesh intersections.
Solution: Use exponential smooth-min: `-log(exp(-k*a) + exp(-k*b)) / k`, with clamped exponent inputs and `k` authored per BioRuleData.
Rejected Alternatives: Boolean mesh union is slower and fragile in editor batches; naive `min(a,b)` causes visible hard seams; runtime deformation is banned.
Scalability potential: Low uses smaller volume resolution and lower branch counts; Middle/High raise resolution; Ultra can increase smoothness/detail because runtime is still baked.
Hardware Impact: Runtime 0 us. Editor generation cost is bounded by volume resolution and uses Native containers.

## Decision 2 - Deterministic Variation

Problem: Batch output must be reproducible and not depend on UnityEngine.Random or wall-clock state.
Solution: Use explicit integer seed hashing and deterministic xorshift-style generation for rule expansion, rock noise offsets, and variation naming.
Rejected Alternatives: `UnityEngine.Random.Range`, `System.Random`, object instance IDs, and clock-based naming. These break replayability and auditability.
Scalability potential: Same seed can output Low/Middle/High/Ultra mesh budgets deterministically.
Hardware Impact: No runtime cost; editor batches are reproducible for asset diffs.

## Decision 3 - Marching Cubes Table Dependency Cut

Problem: Reusing the existing runtime `MCTables` and voxel jobs tied the editor tool to the currently broken Core compile surface.
Solution: Replace that dependency with a self-contained Burst editor marching-cubes pass using cube traversal and tetra decomposition inside the isolated `Hecton8.Editor.ProceduralGen` assembly.
Rejected Alternatives: Referencing `Hecton8.Core` MC tables was rejected because Unity reported unrelated Core/UI compile failures and Burst resolver instability; duplicating the full 4096-entry tri table was rejected as bloat for this editor-only generator.
Scalability potential: Low uses lower SDF resolution and the same offline bake path; Middle/High raise resolution; Ultra can bake denser silhouettes while runtime remains a static LOD prefab.
Hardware Impact: Runtime remains 0 us on i3/MX350. Editor memory is bounded by a capped NativeList raw vertex buffer; overflow logs instead of unbounded allocation.

## Decision 4 - Compile Wall Classification

Problem: Full Unity script compile is currently blocked by unrelated files outside this agent domain.
Solution: Validate all new scripts individually through Unity MCP and continue task execution while recording the compile blockers for the integrator.
Rejected Alternatives: Editing `GlobalSignals.cs`, `HectonFluidEngine.cs`, or UI tool assemblies was rejected as cross-domain sabotage; reverting this editor tool was rejected because new scripts have 0 diagnostics and no longer appear in compiler errors.
Scalability potential: No runtime feature depends on this compile wall; once global compile owners clear their errors, Bio-Forge can import as an isolated editor assembly.
Hardware Impact: No runtime impact. Verification remains PENDING until global compile is green.

## Decision 5 - LOD and Vertex Metadata

Problem: Generated meshes must be cheap on MX350 while preserving enough authored data for underwater vegetation motion.
Solution: Bake three deterministic LOD meshes from the same SDF output, then store wind stiffness as normalized Y-height in vertex color R and cylindrical coordinates in UV0.
Rejected Alternatives: Runtime mesh simplification, CPU bend metadata, and unique unwrap generation. All add runtime or pipeline cost without improving the in-game silhouette enough to justify it.
Scalability potential: Low uses LOD2 at 200 triangles; Middle uses LOD1 at 1000 triangles; High uses LOD0 at 5000 triangles; Ultra can bake higher source resolution while keeping the same runtime prefab contract.
Hardware Impact: Runtime CPU remains 0 us; GPU vertex cost is bounded by LOD selection. On i3/MX350 the expected saving versus single 100k raw mesh is multiple milliseconds of avoided draw/vertex cost.

## Decision 6 - Rock SDF Noise

Problem: Rock generation must be procedural and deterministic without importing placeholder meshes.
Solution: Intersect sphere SDF with Unity.Mathematics 3D simplex noise offset by explicit seed hashing inside the Burst SDF job.
Rejected Alternatives: Value-noise placeholder was rejected after self-review because the prompt explicitly requires 3D Simplex Noise; imported rock meshes were rejected as violating the zero-dollar art pipeline.
Scalability potential: Low lowers SDF resolution and amplitude; Middle keeps default settings; High raises frequency/detail; Ultra can bake dense silhouette variants offline.
Hardware Impact: Runtime remains 0 us. Editor generation pays the noise cost once per baked asset.

## Decision 7 - Prefab and HLOD Contract

Problem: Generated art must enter the existing nature/instance pipeline as static prefab assets, not as bespoke runtime generators.
Solution: Save LOD mesh assets, create a prefab with one `LODGroup`, three child renderers, crossfade enabled, shadow casting disabled, and one shared material assignment.
Rejected Alternatives: Multi-material procedural output, runtime mesh filters, and loose mesh-only assets. Multi-material variants break HLOD/instance-culling batching; runtime mesh filters violate editor-only generation.
Scalability potential: Low consumes LOD2 and a single material; Middle consumes LOD1; High consumes LOD0; Ultra can pair the same mesh contract with richer shader/detail settings.
Hardware Impact: Runtime CPU remains 0 us. Single-material output preserves batching and avoids additional renderer state changes on i3/MX350.

## Decision 8 - Batch Count Enforcement

Problem: The prompt demands "Generate 100 Variations" output, while authorable counts can drift into inconsistent tool behavior.
Solution: The visible 100-variation buttons now use a fixed `MandatedBatchCount = 100`, with deterministic seed offsets for flora and rock batches.
Rejected Alternatives: Leaving `BioRuleData.BatchVariationCount` in control was rejected because a lower value would make the prompt-visible button lie; wall-clock randomness was rejected for nondeterministic asset diffs.
Scalability potential: Low can still reduce SDF/LOD settings in the rule while preserving 100 outputs; Ultra can increase per-asset detail with the same deterministic batch loop.
Hardware Impact: Runtime 0 us. Editor cost is predictable: exactly 100 cold bakes per batch command.

## OMEGA POLISH CHANGES

Problem: The first completed pass still carried exact math forms that are acceptable for an editor bake but weaker than the Omega mandate requires.
Solution: Replaced Burst SDF/interpolation divisions with `math.rcp` multiplications, replaced unconditional `math.length` / `math.normalize` usage with `math.lengthsq` + `math.rsqrt`, and kept mesh generation strictly editor-only.
Rejected Alternatives: Leaving exact `math.length` calls because the tool is editor-only was rejected; moving generation runtime was rejected again as direct i3/MX350 sabotage.
Scalability potential: Low keeps cheap baked LODs and single-material prefab output; Middle/High increase authored SDF resolution; Ultra spends offline time on richer silhouette bakes while runtime remains static mesh rendering.
Hardware Impact: Runtime remains 0 us. Editor bake math is cheaper and less division-heavy; expected runtime microseconds saved remains the full avoided runtime generation cost, conservatively >100 us per generated asset frame and practically multiple milliseconds avoided during batch-heavy scenes.

Exact cinematic cheats used:
- Visual fake first: L-system/SDF bakes static geometry offline instead of simulating living growth or deformation.
- SDF capsule/cone branches: organic joins from smooth-min, not boolean mesh surgery.
- Tetra-decomposed offline extraction: self-contained table-light mesh extraction instead of runtime marching cubes.
- Vertex color wind metadata: shader can bend tips without CPU sway state.
- Single-material LOD prefab: HLOD/instance-culling compatible static output.

Verification evidence:
- `validate_script` on `BioForgeJobs.cs`: 0 errors, 0 warnings after Omega math patch.
- `validate_script` on `BioForgeGenerator.cs`: 0 errors, 0 warnings after Omega math patch.
- Prior `validate_script` on `BioForgeWindow.cs` and `BioRuleData.cs`: 0 errors, 0 warnings.
- Unity console before final polish listed only unrelated `DeployableSdfDrillRuntime.cs` CS0103 errors.
- Final Unity refresh entered compile/domain-reload turbulence and MCP stopped answering `editor_state` / `read_console`; status remains PENDING VERIFICATION.
- `dotnet build Hecton8.Core.csproj`: failed with 154 errors / 47 warnings from existing Core dependency breakage (`Hecton8.Environment.Fluids`, `Hecton8.Core.Memory.Layout`, `Hecton8.Audio.Propagation`, `MacroSwarm`, `AcousticAup`, etc.). Bio-Forge editor files were not in the error set.

Final Git Diff:
- `?? Assets/_Project/Scripts/Editor/ProceduralGen/Hecton8.Editor.ProceduralGen.asmdef` - isolated Editor assembly.
- `?? Assets/_Project/Scripts/Editor/ProceduralGen/BioRuleData.cs` - ScriptableObject L-system/SDF/LOD rule asset.
- `?? Assets/_Project/Scripts/Editor/ProceduralGen/BioForgeJobs.cs` - Burst SDF, extraction, vertex bake, decimation jobs.
- `?? Assets/_Project/Scripts/Editor/ProceduralGen/BioForgeGenerator.cs` - offline generator, asset serialization, prefab/LOD binding, batch generation.
- `?? Assets/_Project/Scripts/Editor/ProceduralGen/BioForgeWindow.cs` - `HECTON-8/Bio-Forge` editor UI and default-rule menu.
- `M Docs/Tasks/Status_PROCEDURAL_GEOMETRY_ARCHITECT.md` - task state, compile blockers, loop log.
- `M Docs/AgentLogs/Rationale_PROCEDURAL_GEOMETRY_ARCHITECT.md` - decisions, Omega polish, verification evidence.

## Decision 9 - Production Hardening Pass

Problem: The first LOD path could emit degenerate triangles after local edge collapse, repeated 100-variant batches forced unnecessary asset saves, and existing mesh overwrite paths leaked temporary Unity Mesh objects.
Solution: Replace destructive per-triangle collapse with deterministic grid-snap simplification plus explicit non-finite/zero-area rejection before mesh serialization; remove unused output index NativeArray allocation; defer `AssetDatabase.SaveAssets()` until batch completion; destroy generated temp meshes after `CopySerialized`; wrap prefab save root cleanup in `finally`; cap L-system appends exactly at `MaxExpansionChars`; persist fallback material assets instead of leaving transient materials.
Rejected Alternatives: Keeping the literal edge-collapse placeholder was rejected because it produced zero-area faces. Saving assets after every variant was rejected because it scales poorly across 100 outputs. Allowing transient fallback materials was rejected because generated prefabs need stable asset references.
Scalability potential: Low gets cleaner LOD2 meshes without degenerate faces; Middle/High keep denser LODs with safer serialization; Ultra can raise SDF settings without multiplying AssetDatabase save/import stalls per generated variant.
Hardware Impact: Runtime remains 0 us. Editor memory drops by one `NativeArray<int>` per LOD bake, batch save/import overhead is reduced from up to 100 save calls to 1, and generated LODs avoid wasting GPU vertex work on degenerate triangles.

Verification evidence:
- `validate_script` after hardening: 0 errors / 0 warnings for `BioForgeJobs.cs`, `BioForgeGenerator.cs`, `BioRuleData.cs`, and `BioForgeWindow.cs`.
- Static forbidden scan after hardening: no matches for runtime randomness, scene searches, coroutine use, `.materials`, `renderer.material`, `math.length(`, `math.normalize(`, or `mesh.triangles`.
- Unity console after hardening: unrelated errors only, `SuitHUDV4CanvasOverlay.cs` duplicate `OnGlobalRegistryServiceReplaced` and Burst missing `Hecton8.Vehicles.VFX`.

## Decision 10 - Organic Normal Quality and Batch Import Scaling

Problem: The generated meshes had correct topology but LOD simplification could flatten normals and the prefab root pivot was encoded by moving the root transform, which is a bad asset contract for placed prefabs.
Solution: Bake smooth normals from central differences over the SDF density field, preserve those normals through LOD grid simplification, recalculate tangents for normal-mapped materials, keep the prefab root transform at zero, and move visual child renderers horizontally to center the geometry. Wrap 100-output sweeps in balanced `AssetDatabase.StartAssetEditing` / `StopAssetEditing`.
Rejected Alternatives: Face-normal LODs were rejected because organic kelp/coral/rock assets need smooth lighting. Root-position pivoting was rejected because it creates bad prefab placement semantics. Repeated per-asset imports were rejected because production batches must scale to hundreds of generated assets.
Scalability potential: Low uses smooth lighting even on LOD2; Middle/High keep better normals under higher budgets; Ultra can push higher SDF resolution and normal-map materials without changing runtime code.
Hardware Impact: Runtime CPU remains 0 us. GPU visual quality improves without extra runtime systems. Editor batch import overhead drops because imports are deferred across the 100-asset sweep.

Verification evidence:
- `validate_script` after this pass: 0 errors / 0 warnings for `BioForgeJobs.cs`, `BioForgeGenerator.cs`, `BioRuleData.cs`, and `BioForgeWindow.cs`.
- Static forbidden scan after this pass: no matches for runtime randomness, scene searches, coroutine use, `.materials`, `renderer.material`, `math.length(`, `math.normalize(`, `mesh.triangles`, or stale `BatchVariationCount`.
- Unity console after this pass: unrelated errors only, currently `GlobalDataVault.cs` missing `Hecton8.Core.Signals` / Burst symbols and a Burst entry-point exception.

## Decision 11 - Branch Broadphase for SDF Bakes

Problem: Flora SDF evaluation scales as voxel points multiplied by emitted L-system branches, so high-end source resolutions can waste editor time evaluating distant branches that cannot change the current smooth-min result.
Solution: Store per-branch expanded bounds at parse time, then use a Burst-side smooth-min cull margin before capsule/cone SDF and exponential `smin`. A branch is skipped when its expanded AABB is farther than the current best SDF plus the negligible exponential blend margin.
Rejected Alternatives: Runtime generation was rejected again as forbidden. A full BVH/octree was rejected for this pass because it adds authoring complexity and extra Native containers before the simpler per-branch broadphase has been measured. Pure brute force was rejected because it scales poorly for dense kelp/coral batches.
Scalability potential: Low keeps cheap SDF settings and skips most irrelevant branches in wide bounds; Middle/High can raise branch count or resolution with less editor stall; Ultra can spend saved editor time on denser silhouettes while preserving the same static LOD prefab runtime contract.
Hardware Impact: Runtime remains 0 us. Editor bake cost can drop by thousands to millions of capsule/smin evaluations per 100-asset flora batch depending on branch spread; i3/MX350 runtime receives only baked meshes.

Verification evidence:
- `validate_script` after this pass: 0 errors / 0 warnings for `BioForgeJobs.cs`, `BioForgeGenerator.cs`, `BioRuleData.cs`, and `BioForgeWindow.cs`.
- Static forbidden scan after this pass: no matches for runtime randomness, scene searches, coroutine use, `.materials`, `renderer.material`, `math.length(`, `math.normalize(`, `mesh.triangles`, or stale `BatchVariationCount`.
- `refresh_unity` requested script compile and timed out after 60 seconds waiting for editor readiness.
- Unity console after the refresh: unrelated errors only, currently `HectonUnderwaterVisuals.cs` missing `IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(...)` and a Burst entry-point exception.
