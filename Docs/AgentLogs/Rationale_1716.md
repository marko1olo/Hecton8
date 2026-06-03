# Rationale 1716 - Offline Compound Collider & Proxy Architect

Status: IMPLEMENTED / FULL PROJECT COMPILE BLOCKED BY CPU THROTTLE

## Session Initialization

Problem: LOD0 visual mesh colliders and runtime collider cooking create PhysX narrow-phase and streaming stalls.
Solution: Build an Editor-only optimizer that strips illegal visual MeshColliders, emits primitive compound proxies or convex proxy mesh assets, and validates topology before serialization.
Rejected Alternatives: Runtime baking, runtime `sharedMesh` reassignment, and direct LOD0 MeshCollider use remain rejected because they create CPU spikes and violate offline authoring doctrine.
Scalability potential: Low uses coarse primitives and broad hulls; Middle uses moderate primitive fit; High uses tighter compounds; Ultra permits tighter offline proxy density while preserving static runtime topology.
Hardware Impact: i3/MX350 gain comes from removing high-triangle narrow-phase and runtime mesh cooking; exact microseconds remain PENDING VERIFICATION until scans/build complete.

## Loop 1 - Audit Decisions

Problem: MeshCollider ownership is spread across serialized prefabs and runtime procedural scripts, so a scene-only pass misses real PhysX load.
Solution: Use direct serialized prefab scans for MeshCollider class IDs and C# scans for `Physics.BakeMesh`/`sharedMesh` runtime mutation. This is evidence-first and branch-stable.
Rejected Alternatives: Opening every prefab through Unity before text audit was rejected for first pass because it hides exact offender locations behind editor import state and costs minutes before we know the blast radius.
Scalability potential: Low/Middle/High/Ultra all benefit because removing LOD0 collision truth is tier-independent; higher tiers spend saved budget on visuals, not collider triangle count.
Hardware Impact: On i3/MX350, avoiding runtime mesh cooking removes unpredictable frame spikes. Static scan cost was 18,400 us command-side; runtime savings require Unity profiler proof after bake.

Problem: 1609 predecessor generated useful compound colliders but still carried runtime `Physics.BakeMesh` and runtime `sharedMesh` commit semantics.
Solution: Reuse only the offline editor-side fitting lessons; new 1716 route serializes proxy meshes and prefab collider topology before play.
Rejected Alternatives: Keeping a runtime baker as a fallback is rejected because fallback routes become production routes under streaming pressure.
Scalability potential: Low uses fewer support directions and primitives; Middle increases primitive coverage; High/Ultra increase offline support directions and tighter hulls while keeping runtime topology static.
Hardware Impact: i3/MX350 gain is avoiding PhysX cook bursts and broadphase churn from visual mesh colliders; exact per-prefab microseconds remain validator output.

Problem: Collision culling can drift into hot GlobalRegistry polling or DataVault misuse if implemented as a global query.
Solution: Keep culling local to serialized colliders and a cached camera transform; no DataVault ownership, no hot GlobalRegistry path.
Rejected Alternatives: A central runtime collider manager was rejected for this task because it invents a cross-domain dependency and violates the one-owner route.
Scalability potential: Low checks fewer frames or larger thresholds; Middle/High/Ultra may preserve farther colliders through continuous quality weight without changing truth ownership.
Hardware Impact: i3/MX350 avoids checking or enabling far LOD2 collision every frame; estimate pending implementation and profiler mock.

Problem: Editor optimizer failures must be diagnosable after context loss without adding disk I/O proof noise.
Solution: Keep a fixed 300-entry in-memory black-box ring in the optimizer and remove JSON/binary dump writer obligations from this source-only proof pass.
Rejected Alternatives: Console-only logs were rejected as volatile; JSON and binary dumps were rejected after the latest directive because they add stale I/O artifacts instead of source proof.
Scalability potential: Same telemetry footprint across all quality tiers; quality changes fidelity, not evidence capture.
Hardware Impact: Editor-only fixed ring has no runtime frame cost; low-end editor machines pay bounded write cost only on failure.

## Loop 2 - Implementation Decisions

Problem: Automatic AssetPostprocessor mutation can corrupt prefabs while the generator is still being authored by parallel agents.
Solution: Implement `ColliderOptimizerEngine1716` as explicit `MenuItem` tooling first, with reusable public APIs for later generator integration.
Rejected Alternatives: Immediate `OnPostprocessPrefab` mutation was rejected because 20+ agents are writing generated prefabs concurrently and uncontrolled import-time mutation creates non-deterministic blame.
Scalability potential: Low/Middle/High/Ultra are baked into static prefab variants through `GlobalQualityWeight`; runtime does not branch on hardware tier for collider truth.
Hardware Impact: i3/MX350 avoids LOD0 narrow phase entirely after the offline pass; expected saving is milliseconds on collision-heavy frames, pending Unity profiler.

Problem: Full QuickHull on high-poly rocks can explode on coplanar/near-coplanar triangles and create editor hangs.
Solution: Use a Burst support-point job to extract directional extremes, attempt a bounded convex support hull, and fall back to padded AABB if the hull exceeds 200 triangles or cannot prove bounds containment.
Rejected Alternatives: Unbounded face expansion and V-HACD integration were rejected for this pass because they introduce dependency and hang risk without a proof budget.
Scalability potential: Low uses fewer support directions and more padding; Middle increases support density; High and Ultra use more directions for tighter static proxies.
Hardware Impact: i3/MX350 runtime gets a <=200 triangle convex proxy or a 12-triangle box instead of decorative rock triangles.

Problem: Runtime distance culling can become a duplicate owner if implemented as a new physics-side component.
Solution: Fold generated `COL_` collider enable state into first-party `CullingManager`; `SlowTick` sets a dirty flag, `RunCullingEvaluationVisualSync` computes pending state, and `LateFrameTick` applies renderer/collider changes after simulation settles.
Rejected Alternatives: Standalone duplicate culler, `Update`, `FindObjects`, and central collider manager were rejected because they duplicate ownership or add hot lookup risk.
Scalability potential: Low disables generated scatter collision nearer; Middle/High/Ultra preserve farther collision through continuous distance interpolation.
Hardware Impact: i3/MX350 pays only a slow evaluation over cached arrays and a VISUAL_SYNC apply pass; no steady-state GC by code inspection.

Problem: Existing runtime systems explicitly called PhysX mesh cooking or committed generated meshes into MeshCollider.
Solution: Remove cook calls from old baker, terrain, voxel, and outpost paths. Generated MeshCollider commit lines in terrain/voxel upload paths now leave cheap proxy blockers active instead of swapping triangle meshes into PhysX.
Rejected Alternatives: Deleting whole terrain/voxel owners was rejected because it destroys world collision ownership and crosses into another domain without a replacement Addressables pipeline.
Scalability potential: Low keeps proxy blockers; Middle/High/Ultra need a future owner-authored Addressables COL chunk pipeline for tighter streamed collision.
Hardware Impact: i3/MX350 removes runtime cook spikes; collision detail for streamed procedural chunks is reduced to existing proxy blockers until a full offline chunk proxy pipeline exists.

Problem: Compiler verification is required but the host is already saturated.
Solution: Sampled CPU and compiler processes before build; CPU was 100% and `dotnet` PIDs 3100 and 18204 were active, so build was not launched.
Rejected Alternatives: Launching another `dotnet build` was rejected by explicit mandate and would contaminate compile timing.
Scalability potential: No runtime impact.
Hardware Impact: Avoided adding build load on an already saturated workstation.

## Loop 3 - Static Verification Decisions

Problem: Runtime cook eradication must be proven without a compiler pass.
Solution: Ran direct token scans for `Physics.BakeMesh` and generated `MeshCollider.sharedMesh` commit patterns across player scripts. Result: zero player-script bake calls; one remaining bake call is editor-only in `Assets/_Project/Scripts/Editor/GeologyForge/GeologyForgeGenerator.cs:1074`.
Rejected Alternatives: Treating editor-only baking as a violation was rejected because offline generators are the approved owner for PhysX mesh cooking.
Scalability potential: Low/Middle/High/Ultra all get the same runtime rule: no cook, no generated triangle mesh commit. Higher quality must be paid at authoring time only.
Hardware Impact: i3/MX350 avoids chunk-load cook spikes. Exact runtime microseconds are not claimed without Unity Profiler data.

Problem: The compound fallback path was reporting one generated primitive twice.
Solution: Removed the extra `PrimitiveCollidersGenerated` increment in the fallback BoxCollider path; the unified final increment now owns the count.
Rejected Alternatives: Leaving inflated metrics was rejected because the CTO report uses these counters as proof artifacts.
Scalability potential: No runtime impact; report accuracy is tier-independent.
Hardware Impact: No runtime gain. Prevents false capacity planning from an inflated primitive count.

## Loop 4 - Race And Allocation Decisions

Problem: Distance-based collider culling could accidentally become a DataVault/AUP consumer and inherit compaction-fence hazards.
Solution: `CullingManager` reads only a cached managed camera, renderer arrays, generated `Collider[]`, and local scalar settings. It has no `GlobalDataVault`, no native pointer, no job handle, and no hot `GlobalRegistry.Get<` route. If the camera transform is unavailable, VISUAL_SYNC evaluation returns and retries on the next request.
Rejected Alternatives: AUP position reads, standalone physics culler, and centralized collider-culling service were rejected because they add compaction ownership risk or invent a cross-domain hot dependency.
Scalability potential: Low uses longer cadence and shorter cull distance; Middle/High/Ultra interpolate distance and cadence through `GlobalQualityWeight` without changing collision truth ownership.
Hardware Impact: i3/MX350 pays zero managed allocation in steady-state slow ticks by code inspection; profiler proof remains pending.

Problem: Compound collider seams can cause snagging if child transforms are offset or zero-thickness.
Solution: Generated roots are parented at local zero; primitive centers are calculated in root-local coordinates; collider axes are clamped to 0.025 m; capsules are used for long cylindrical/tube-like shapes.
Rejected Alternatives: Visual triangle colliders and zero-thickness box planes were rejected because they create narrow-phase cost or contact instability.
Scalability potential: Low may use fewer larger primitives; Middle/High/Ultra may use more tightly fitted primitives up to the hard cap of 10.
Hardware Impact: i3/MX350 broadphase/narrow-phase contacts are bounded by <=10 primitive colliders per generated proxy rather than decorative triangle soup.

## Loop 5 - Report And Build Gate Decisions

Problem: The latest directive rejects JSON reports and binary telemetry dumps; earlier disk proof artifacts became stale and misleading.
Solution: Removed the stale JSON report and kept verification in source/tests/status only. Runtime profiler microsecond fields remain unclaimed because no profiler run occurred.
Rejected Alternatives: Keeping JSON hashes or claiming stripped prefab counts without running the optimizer/profiler was rejected as fake proof.
Scalability potential: Source gates distinguish implemented authoring capability from unexecuted asset mutation so Low/Middle/High/Ultra bake variants can be generated after compile clearance.
Hardware Impact: No runtime impact until the editor pass is executed. The player runtime cook path removal is active in source.

Problem: A second build eligibility sample still showed overload.
Solution: CPU sampled at 90% with active `dotnet` PIDs 3100 and 12768; build remained blocked. Static `git diff --check` passed with line-ending warnings only.
Rejected Alternatives: Running build despite active compilers was rejected by explicit task mandate.
Scalability potential: No runtime impact.
Hardware Impact: Avoided compounding workstation load and generating contaminated compiler timing.

## Loop 6 - Source Polish And Integration Corrections

Problem: Adding sphere primitive support made `ColliderPrimitiveFit1716` layout dependent on compiler tail padding.
Solution: Added explicit `Pad3` so the sequential DTO reaches an intentional 64-byte size; static `UnsafeUtility.SizeOf<T>()` alignment validation remains active in `ValidateEditorStructLayouts`.
Rejected Alternatives: Relying on implicit tail padding was rejected because ARM64 cache-line correctness must be explicit for unmanaged DTOs.
Scalability potential: No runtime gameplay impact; editor bake DTOs stay deterministic across Low/Middle/High/Ultra authoring presets.
Hardware Impact: Prevents accidental misaligned native copy/read in editor jobs; no frame cost.

Problem: Full compile proof cannot be isolated while another compiler is active and Unity has unrelated red console entries.
Solution: Re-ran Unity MCP syntax validation on patched optimizer, culling manager, runtime baker, voxel engine/volume, voxel smoke tester, and collider tests. Re-ran static scans for runtime BakeMesh, runtime MeshCollider commits, DataVault locks, orphan `.meta`, and diff whitespace.
Rejected Alternatives: Launching `dotnet build` while `dotnet` processes are active was rejected by throttle mandate; patching unrelated UI, shader, or vegetation errors was rejected as cross-domain work.
Scalability potential: No runtime behavior change.
Hardware Impact: Avoided contaminating CPU with duplicate build load; source validation cost remained bounded.

## Loop 7 - Classifier And Runtime Apply Polish

Problem: `family_coral` paths were caught by broad `/Flora/` logic and could be stripped instead of receiving massive-coral convex collision.
Solution: Exclude coral tokens from flora classification and route coral/family_coral through organic convex proxy mode.
Rejected Alternatives: Keeping coral as non-colliding flora was rejected because massive coral clusters are navigation blockers in the prompt.
Scalability potential: Low gets coarse AABB fallback when support hull cannot contain all vertices; Middle/High/Ultra get tighter offline hulls under the same 200-triangle cap.
Hardware Impact: i3/MX350 avoids visual triangle collision while preserving coral blocking truth.

Problem: Kelp/flora strip-only removed trigger contact needed for audio/shader contact response.
Solution: Route flora to compound primitive generation, set flora generated colliders as triggers, and bias kelp/vine/frond/stem/tendril names toward capsule fitting.
Rejected Alternatives: Strip-only flora was rejected because it removes contact events; solid flora collision was rejected because kelp should not block movement.
Scalability potential: Low uses coarse trigger capsules; Middle/High/Ultra can use more fitted trigger primitives offline.
Hardware Impact: i3/MX350 pays cheap trigger primitive overlap instead of MeshCollider or no contact feedback.

Problem: Support-hull acceptance used bounds proof, which can under-cover source vertices even when bounds match.
Solution: Add `HullContainsSourceVertices` plane validation over every source vertex before accepting a generated hull; fallback to padded AABB if any vertex lies outside.
Rejected Alternatives: Bounds-only acceptance was rejected as mathematically weak; full V-HACD remains rejected as dependency/hang risk.
Scalability potential: All quality levels keep correctness; higher quality only increases support direction density.
Hardware Impact: Editor-only validation cost, no runtime frame cost; prevents collision holes on cheap devices.

Problem: Primitive fitting assumed every submesh is triangle topology.
Solution: Gate `TryFitPrimitive` with `mesh.GetTopology(subMeshIndex) == MeshTopology.Triangles` before `GetTriangles`.
Rejected Alternatives: Letting helper line/point submeshes flow into collision fitting was rejected as invalid topology.
Scalability potential: No tier impact; prevents authoring-time bad proxy generation.
Hardware Impact: Editor-only guard, no runtime cost.

Problem: `LateFrameTick` recomputed bounds for every cullable object every frame after the integration pass.
Solution: Move bounds refresh into the requested VISUAL_SYNC culling evaluation and add `_cullStateApplyRequested` so LateFrame returns without object iteration when no cull state is dirty.
Rejected Alternatives: Per-frame bounds recompute was rejected because the culling cadence is slow-tick driven and per-frame apply should only handle dirty presentation state.
Scalability potential: Low/Middle/High/Ultra all keep stable culling behavior; saved CPU can buy more visual density at higher quality.
Hardware Impact: i3/MX350 removes an O(N registered cullables) LateFrame cost from steady frames.

## Loop 8 - Replacement Gate And Material Determinism

Problem: Compound and convex generation stripped old MeshColliders before proving that a replacement proxy existed. A no-vertex or rejected-proxy path could save a prefab with collision removed instead of optimized.
Solution: Generate the compound fallback or convex proxy first, then strip old MeshColliders only after replacement exists. Added generated-root presence validation and convex-root validation requiring exactly one convex MeshCollider with a <=200 triangle proxy mesh.
Rejected Alternatives: Treating "no collider" as valid strip-only output in compound/convex modes was rejected because it destroys gameplay collision truth.
Scalability potential: Low/Middle/High/Ultra all keep static replacement guarantees; only primitive count, support directions, and padding scale.
Hardware Impact: i3/MX350 avoids both LOD0 triangle collision and accidental missing blockers.

Problem: Flora routing depended mostly on prefab path, so a kelp prefab saved outside `/Flora/` could become a solid world collider instead of a trigger contact proxy.
Solution: Added `IsFloraAsset(path, name)` with coral/rock exclusions and token matching for kelp, grass, seaweed, sargassum, and algae. Layer, trigger, material, and auto-mode decisions use this route.
Rejected Alternatives: Path-only flora classification was rejected because generator output folders vary across agents.
Scalability potential: Low gets coarse trigger capsules; Middle/High/Ultra can author tighter trigger compounds without changing runtime truth.
Hardware Impact: i3/MX350 pays cheap trigger overlap for flora contact instead of solid blocking or visual MeshCollider cost.

Problem: Generated physics materials set friction and bounce values but left combine policy implicit.
Solution: `EnsurePhysicsMaterial` now sets `PhysicsMaterialCombine` explicitly. Kelp/flora use minimum friction, steel uses maximum friction, all generated materials use minimum bounce for dense-water damping.
Rejected Alternatives: Leaving Unity defaults was rejected because contact pair behavior should not drift by asset import defaults.
Scalability potential: Material truth is static across tiers; high tiers spend saved CPU on visuals, not different physics semantics.
Hardware Impact: Deterministic contact response with no runtime cost.

Problem: The runtime sharedMesh audit parser treated `==` and `!=` comparisons as assignments.
Solution: Added single-assignment operator parsing and comment stripping before `IsRuntimeMeshColliderCommitLine` evaluates a line.
Rejected Alternatives: Regex-only scan was rejected because false positives pollute the compile gate and hide real runtime collider commits.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact; improves source gate precision.

## Loop 9 - Atomic Proxy Serialization And Voxel Runtime Cook Closure

Problem: Convex proxy meshes were created as `.asset` files before the prefab passed the full post-generation validator. A later validator failure could leave orphaned proxy assets on disk.
Solution: Defer `SerializeGeneratedProxyMeshes` until after the first `ValidatePrefabColliderBudget` pass and immediately before `PrefabUtility.SaveAsPrefabAsset`. `GenerateConvexProxy` now builds an in-memory mesh only; serialization validates vertex count, triangle count, volumetric bounds, and proxy folder ownership before creating the asset.
Rejected Alternatives: Creating the mesh asset inside `GenerateConvexProxy` was rejected because generation and persistence have different failure domains. Deleting leftovers after failure was rejected because it creates a cleanup race under parallel agents.
Scalability potential: Low/Middle/High/Ultra all get the same atomic authoring rule; quality only changes support directions, primitive budget, and padding.
Hardware Impact: No runtime frame cost. Editor failure cleanup cost drops to 0 asset deletes on rejected prefabs.

Problem: `HectonVoxelEngine` still had an active runtime PhysX bake route: `VoxelMeshBakeJob.Execute` called `UnityEngine.Physics.BakeMesh`, call-sites scheduled it, waited for completion, then tried to publish MeshCollider meshes through deferred upload.
Solution: Remove the PhysX call from `VoxelMeshBakeJob.Execute`, make `TryScheduleVoxelPhysicsBake` fail closed, remove fallback/chthonic/chunked schedule/readback/publish call-sites, and keep collision on existing `BoxCollider` proxy paths. `HectonVoxelVolume.PublishColliderChunkMesh` now keeps the proxy and does not enqueue mesh upload.
Rejected Alternatives: Keeping runtime mesh collision for high quality was rejected because `GlobalQualityWeight` may not change physics truth ownership or reintroduce frame spikes. Deleting the whole voxel collision owner was rejected because it would remove spatial blockers before an offline voxel COL stream exists.
Scalability potential: Low uses coarse proxy chunks; Middle/High/Ultra can later consume offline serialized voxel COL assets, but runtime remains cook-free across all tiers.
Hardware Impact: i3/MX350 avoids runtime PhysX bake and MeshCollider publication stalls. Expected saved time is the eliminated bake/upload stall per voxel collision chunk; exact microseconds remain pending profiler after unrelated compile blockers clear.

## Loop 10 - Voxel Collider Staging API Closure

Problem: The old voxel volume API still exposed misleading "publish" naming and mesh staging accessors even after direct runtime bake call-sites were removed.
Solution: Rename `PublishColliderChunkMesh` to `EnableColliderChunkProxy`, make `IsDeferredColliderChunkUploadReady`, `GetColliderChunkBakeMesh`, `AssignColliderChunkBakeMesh`, `GetOrCreateColliderChunkMesh`, and `GetOrCreateColliderChunkBakeMesh` fail closed, and keep cleanup methods only for stale pooled mesh release.
Rejected Alternatives: Leaving unused mesh staging accessors alive was rejected because future runtime code could reacquire pooled meshes and recreate the PhysX publication path. Deleting the entire deferred upload subsystem was rejected in this pass because old teardown/drain state is shared with shutdown cleanup and broader removal risks cross-domain regressions.
Scalability potential: Low/Middle/High/Ultra all use primitive voxel collision proxies at runtime. Higher quality must arrive through offline serialized COL assets, not by reopening MeshCollider staging under `GlobalQualityWeight`.
Hardware Impact: i3/MX350 avoids staged mesh allocation/acquisition for voxel collider chunks and prevents reintroduction of runtime narrow-phase mesh publication. Exact runtime microseconds remain pending profiler.

## Loop 11 - Root-Wide LOD0 Mesh Reference Gate And Proxy Cleanup Fail-Closed

Problem: `ValidatePrefabColliderBudget`, audit, dry-run, and strip paths only detected a visual LOD0 MeshCollider when the `MeshCollider` lived on the same GameObject as the `MeshFilter`. A sibling or nested child could still reference the exact same LOD0 `sharedMesh` and pass the validator if the mesh had <=200 triangles.
Solution: Add `IsPrimaryVisualMeshReference(root, collider)` and route validator/audit/dry-run/strip through it. The helper checks all primary visual `MeshFilter` components under the prefab root and rejects any MeshCollider that references the same mesh. Added a behavioral editor test that uses a separate collider child referencing a LOD0 visual mesh.
Rejected Alternatives: Raising only the triangle threshold guard was rejected because the rule is identity-based: visual collision truth is illegal even when the test mesh is small. Adding a runtime detector was rejected because this is an offline serialization gate.
Scalability potential: Low/Middle/High/Ultra all receive the same topology law; quality may change offline proxy density, not allow visual mesh collision.
Hardware Impact: i3/MX350 avoids a class of prefab authoring leaks where LOD0 mesh identity is hidden behind a child collider object. Exact runtime microseconds remain pending profiler.

Problem: `DetachColliderChunkBakeMesh` and `ReleaseColliderChunkBakeMesh` were legacy mesh cleanup APIs that disabled the primitive chunk proxy while removing staged mesh state.
Solution: Keep MeshCollider disabled, but call `EnableColliderChunkProxy(index)` before clearing or releasing staged mesh data. Tests now assert those cleanup methods do not call `DisableColliderChunkBakeProxy` and do re-enable the primitive proxy.
Rejected Alternatives: Disabling all proxies in cleanup was rejected because stale mesh cleanup must fail closed to coarse collision, not fail open to no collision. Removing the methods entirely was rejected because other branches may still call legacy cleanup during shutdown.
Scalability potential: Low/Middle/High/Ultra keep primitive voxel collision as the runtime truth. Higher fidelity must come from offline serialized COL chunks, not runtime staged MeshCollider cleanup.
Hardware Impact: i3/MX350 avoids both PhysX mesh publication and accidental collision holes after deferred cleanup. No measured profiler number claimed.

## Loop 12 - Generator Serialization Gates

Problem: The 1716 optimizer could validate/optimize by menu or folder pass, but first-party generators could still save prefabs without invoking the 1716 topology gate. A post-save-only gate would detect illegal collision after the prefab had already been written.
Solution: Add `ValidatePrefabAssetTopology(prefabPath)` for cold single-prefab validation and wire RockSculptor1713, ModuleArchitect1712, and EquipmentPropBaker1715 to run `ValidatePrefabColliderBudget(root)` before save and `ValidatePrefabAssetTopology(prefabPath)` after save. The pre-save gate aborts before illegal serialization; the post-save gate verifies the AssetDatabase asset that downstream tooling will load.
Rejected Alternatives: Running the full optimizer from every generator was rejected because several generators already own valid `COL_` primitive proxies and full regeneration would duplicate colliders. Reflection dispatch was rejected for RockSculptor because an explicit asmdef reference is cleaner and compile-visible. Copying validator logic into generators was rejected as duplicate ownership.
Scalability potential: Low/Middle/High/Ultra all share the same illegal-topology stop gate. Quality still only changes offline proxy fidelity, not the rule that visual LOD meshes are render-only.
Hardware Impact: i3/MX350 avoids generator-authored LOD0 MeshCollider leaks entering content. No runtime frame cost; this is editor serialization hygiene.

## Loop 13 - Cross-Editor Generator Gate Closure

Problem: Older editor pipelines outside `Assets/_Project/Editor/Generators` still saved prefabs with MeshColliders without passing through the 1716 gate. `HadalArchBakePipeline` had the worst route: `lod2 ?? lod1 ?? lod0` with `convex=false`, so a missing low LOD could serialize LOD0 as collision truth.
Solution: Wire GeologyForge, BioForge, WorldProceduralGeologyFinalAuthoring, and OfflineHadalArchBaker to `ValidatePrefabColliderBudget(root)` before save and `ValidatePrefabAssetTopology(prefabPath)` after save. Move generated geology/rock MeshColliders under `COL_ConvexProxy_1716`. Replace Hadal MeshCollider topology with a three-BoxCollider `COL_CompoundProxy_1716` that approximates two arch pillars plus the upper lintel. Align GeologyForgeSelfAudit and ShallowsBioForgeBatchBaker with the generated root topology.
Rejected Alternatives: Reflection bridge was rejected because explicit editor asmdef references expose compile-time dependency problems. Leaving Hadal's LOD fallback was rejected because it can silently reintroduce LOD0 MeshCollider topology. A convex Hadal hull was rejected because it can overblock the arch opening; primitive pillars preserve the gameplay passage with O(1) collider cost. Copying validator logic into each generator was rejected as duplicate collision policy ownership.
Scalability potential: Low gets the same serialized proxy law with coarse meshes; Middle/High/Ultra can use tighter offline proxy meshes while staying under the 200-triangle MeshCollider cap. Runtime physics truth does not branch on hardware tier.
Hardware Impact: i3/MX350 avoids generator-authored LOD0/concave MeshCollider leaks and Hadal arch triangle collision entirely. No profiler microseconds claimed; this is authoring prevention with zero steady-state runtime allocation.

## Loop 14 - BioForge Primitive Proxy Closure

Problem: BioForge rock generation still used the visual LOD2 mesh as a convex MeshCollider payload. It was lower than LOD0 but still a generated visual mesh route, and ShallowsBioForge batch validation accepted the old topology.
Solution: Replace BioForge rock collision output with `COL_CompoundProxy_1716` and one finite, minimum-size `BoxCollider` derived from LOD0 bounds/fallback SDF bounds. Update ShallowsBioForge batch validation to reject MeshColliders and require primitive proxy topology.
Rejected Alternatives: Reusing LOD2 MeshCollider was rejected because it preserves triangle collision dependency in runtime prefabs. Generating a separate BioForge convex hull was rejected for this pass because the main 1716 optimizer already owns hull generation and BioForge rock fallback only needs a coarse blocker.
Scalability potential: Low uses the same cheap primitive blocker; Middle/High/Ultra can later run the central 1716 optimizer for tighter offline hulls without allowing BioForge to own MeshCollider policy.
Hardware Impact: i3/MX350 avoids BioForge-authored triangle narrow phase for generated rock batches. Static source gate only; profiler microseconds not claimed.

## Loop 15 - Runtime MeshCollider Contract Removal

Problem: Runtime compatibility shells and chunk systems no longer called `Physics.BakeMesh`, but they still exposed cooking options, created disabled MeshColliders, or kept an empty fake bake job scheduler. That leaves an attractive regression path for future runtime mesh publication.
Solution: Remove `MeshColliderCookingOptions` from `RuntimePhysicsBaker1609` API, make 1609 editor optimizer prebind the proxy mesh offline, remove terrain chunk MeshCollider creation and fake terrain bake scheduling, and route voxel fallback/chunk collision through `BoxCollider` proxies without runtime MeshCollider creation.
Rejected Alternatives: Keeping disabled MeshCollider placeholders was rejected because source gates cannot distinguish "disabled now" from future reassignment. Removing all legacy cleanup fields in one pass was rejected where broader voxel state still owns stale mesh cleanup; those paths now fail closed and source gates reject active creation/assignment.
Scalability potential: Low/Middle/High/Ultra all keep static primitive runtime collision truth. Higher quality must be purchased by offline `COL_` assets, not by runtime PhysX cooking.
Hardware Impact: i3/MX350 avoids empty same-frame job schedule/finalize overhead and prevents runtime triangle-collider component creation in terrain/voxel streaming paths. Exact PhysX frame savings remain pending profiler.

## Loop 16 - Physics Skin Generator Contract Closure

Problem: `HectonPhysicsSkinGenerator` was an editor-only tool, but its saved output used a legacy `PHYSICS_SKIN` root with `convex=false` MeshCollider payloads. That created a second collider policy outside the 1716 `COL_` route, and the chunked branch still contained `AddComponent<MeshCollider>` plus `sharedMesh = chunkMesh`.
Solution: Route the tool through the 1716 contract: generated meshes are named `COL_Skin_*`, target triangles are capped by `ColliderOptimizerEngine1716.ProxyMeshTriangleLimit`, normal output uses `COL_ConvexProxy_1716`, MeshCollider is `convex=true`, same-object visual LOD0 MeshCollider is stripped, and `ValidateProxyMesh` plus `ValidatePrefabColliderBudget` must pass before success is reported. The chunked non-convex path now fails closed and its body uses BoxCollider chunks instead of MeshCollider chunks.
Rejected Alternatives: Keeping non-convex low-poly skins was rejected because it preserves a parallel topology standard and can drift into runtime prefabs without central validation. Running the full optimizer from this window was rejected because the window already owns a specific mesh-skin bake path; it only needed to obey the central 1716 validation contract.
Scalability potential: Low uses coarse <=200 triangle convex proxy or BoxCollider fallback through the central optimizer; Middle/High/Ultra may increase offline proxy tightness through the central 1716 settings, never by reopening chunked runtime MeshCollider policy.
Hardware Impact: i3/MX350 avoids legacy non-convex shell collision and accidental LOD0 MeshCollider survival from this editor tool. No runtime profiler number claimed; source gates prove the authoring route no longer emits the old PhysX-heavy topology by default.

## Loop 17 - Legacy Root Migration And Root Assembly Save Gates

Problem: `ColliderOptimizationEngine1609` still used legacy generated root naming and root-level proxy placement patterns, while Flora1604/1711, DeepReachStationFabricator, and InteriorFinisher1608 could save prefabs without the shared 1716 topology gate.
Solution: Migrate 1609 generated roots toward `COL_CompoundProxy_1716` and `COL_ConvexProxy_1716`, place its proxy MeshCollider under the convex child root, convert Flora1604 collision to child `COL_CompoundProxy_1716` sphere objects, and add pre-save/post-save `ColliderOptimizerEngine1716` validation to the root editor assembly generators that serialize prefabs.
Rejected Alternatives: Pulling Fauna1610 or AbyssalScatter1614 into `Hecton8.Project.Editor` was rejected because those separate asmdefs do not create colliders and the dependency would be artificial. Gating generic missing-script or LOD repair tools was rejected because they repair existing prefabs and do not own physics topology.
Scalability potential: Low/Middle/High/Ultra all keep the same serialized topology law. Quality can increase offline proxy tightness, not reopen visual LOD mesh collision.
Hardware Impact: i3/MX350 avoids additional generator-authored LOD0/MeshCollider leaks and legacy root drift. No runtime profiler number claimed; this is editor serialization prevention.

## Loop 18 - DataVault LUT Lock Flattening

Problem: `HectonWorldGenerator.EnsureLutBuffer` evaluated an `AnimationCurve` 1024 times while holding a `GlobalDataVault` write lock.
Solution: Bake west/east/biome LUTs into temporary `NativeArray<float>` buffers before acquiring the vault write locks, then copy the precomputed floats inside strict `try/finally` lock scopes.
Rejected Alternatives: Leaving curve evaluation inside the lock was rejected because designer curves can do managed/editor-side work and should not block the compaction fence. Replacing the LUTs with managed arrays was rejected because it would add GC pressure and break the existing NativeArray route.
Scalability potential: Low/Middle/High/Ultra keep identical world truth; the fix only shortens lock duration.
Hardware Impact: i3/MX350 avoids holding the vault write lock across curve evaluation. Runtime microseconds are not claimed without profiler data.

## Loop 19 - Content Proxy Baker Contract Closure

Problem: `ContentPhysicsProxyBaker` still emitted a manual `GEN_PhysicsProxyHull` MeshCollider outside the 1716 generated-root contract, used world-space bounds as local mesh input, and saved proxy meshes without the central proxy/topology validators.
Solution: Route the baker through `COL_ConvexProxy_1716` with a child `COL_ContentProxyHull_1716`, compute an AABB from every BoxCollider corner in the selected root's local space, validate the 12-triangle box hull through `ColliderOptimizerEngine1716.ValidateProxyMesh`, and validate the generated hierarchy through `ValidatePrefabColliderBudget(root)` before removing source BoxColliders or creating the mesh asset.
Rejected Alternatives: Keeping the old `GEN_` route was rejected because it bypasses the single collider policy. Copying validator logic into ContentAuthority was rejected because collider topology has one owner. Running the full optimizer from the menu was rejected because this tool intentionally merges hand-authored BoxColliders into one content proxy.
Scalability potential: Low uses the same 12-triangle content hull with no runtime cook; Middle/High/Ultra can later run the central optimizer for tighter offline proxies while keeping the same serialized `COL_` contract.
Hardware Impact: i3/MX350 avoids manual content assets drifting back to unmanaged visual/legacy MeshCollider topology. Runtime profiler microseconds are not claimed; source gates prove this route now serializes a bounded convex proxy only.
