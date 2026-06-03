# Rationale 1712

## Session Bootstrap

Problem: The assignment targets two runtime release blockers: RB-003 dynamic outpost shell mesh/material fallbacks and RB-011 mock foundation SDF truth.
Solution: Scope is limited to outpost generation, construction snapping, and offline structure generation. Runtime mesh/material creation must be removed. SDF absence must fail closed, not invent support.
Rejected Alternatives: Keeping `CreateCubeMesh()` as debug fallback is rejected because it can enter player runtime, allocates mesh arrays, and violates SRP/material batching. Keeping `MockSdfFallback` is rejected because it changes construction truth.
Scalability potential: Low uses serialized shared mesh/material and no runtime bake; middle/high/ultra spend saved runtime cost on authored/offline bevel density and richer static masks.
Hardware Impact: On i3/MX350, removing runtime mesh/material allocation prevents upload/GC spikes; exact microseconds remain PENDING PROFILER VERIFICATION.

Problem: The old standalone domain-map route is retired, so domain authority must come from active docs and prompt scope.
Solution: Treat `<AGENT_PROMPT id="1712">` domain directories, `Docs/PROJECT_ATLAS.md`, and the coverage matrix as the active boundary.
Rejected Alternatives: Editing outside the prompt domain based on guesswork is rejected.
Scalability potential: Domain-limited edits reduce integration conflicts with parallel agents.
Hardware Impact: No runtime hardware delta; prevents architectural conflict churn.

## Runtime Fallback Removal

Problem: `MarauderOutpostGenerationService` previously had a release path that fabricated shell geometry and material state when authored assets were missing.
Solution: Removed runtime shell mesh/material synthesis and made render initialization fail closed through `ValidateAuthoredRenderResources()`. The service now resolves only serialized `shellMesh` and `shellMaterial`, emits telemetry with `FaultFlag`, and sets `OutpostGenerationState.Faulted` on missing resources.
Rejected Alternatives: A hidden debug cube, `Shader.Find`, `RuntimeShaderReferenceCatalog` material construction, or lazy `new Material` fallback were rejected because they still create non-authored runtime render truth and fragment batching.
Scalability potential: Low uses one shared mesh/material and no cold upload. Middle/high/ultra can spend offline asset budget on bevels, trims, and static masks without changing runtime behavior.
Hardware Impact: Estimated gain on i3/MX350 is 35-120 us avoided on missing-asset cold path plus avoided managed allocation and GPU upload spike. Steady-state allocation impact is 0 B.

Problem: A wider assigned-domain sweep found additional construction preview/proxy `new Material()` calls outside the named outpost blocker.
Solution: Removed runtime material synthesis from `FoundationPylonGpuBatch`, `HectonBlueprintPreviewBatch`, `VRPipeBlueprintPreview`, and `ConstructionRuntimeProxyFactory`. Preview systems now require serialized/shared authored materials or fail visibly without synthesizing clones. The legacy station fabricator now uses existing authored material assets and does not mutate shared fallback material state.
Rejected Alternatives: Keeping editor/development `DontSave` clones was rejected because these paths still train the runtime to hide missing authoring and can enter development builds.
Scalability potential: Low avoids material clone churn and SetPass explosion. Middle/high/ultra retain visual fidelity through authored materials and shader data buffers.
Hardware Impact: Estimated gain on i3/MX350 is 10-80 us per avoided cold clone plus stable SRP batching; no managed material instance leak.

## Foundation SDF Truth

Problem: Foundation pylons could derive construction support from a mock SDF instead of authoritative terrain, allowing physically false placement.
Solution: Removed `MockSdfFallback`, mock SDF buffer reads, `CreateDefaultMockSdfConfig()`, and the mock SDF generator job. `FoundationPylonGpuBatch` now resolves `VoxelSdfPayloadDescriptorDTO` plus `VoxelSdfTexture3D` from `GlobalDataVault`, verifies generation, owner, byte count, finite origin/cell size/range, and schedules pylon jobs only with real encoded SDF data. Missing/stale data publishes `SnapFailed_NoSubstrate`, clears upload, and pushes a HUD warning.
Rejected Alternatives: Empty SDF, flat plane fallback, nearest proxy, or editor-only mock were rejected because any non-authoritative substrate changes gameplay truth.
Scalability potential: Low uses the same encoded SDF with lower ray budgets/interpolation weight. Middle uses more rays and bounded marching. High/ultra increase ray count, interpolation, shader flare, and draw fidelity without changing terrain authority.
Hardware Impact: Estimated gain on i3/MX350 is 250-900 us avoided when the old mock volume would be regenerated or sampled, plus no false support state. Steady-state job reads remain NativeArray-only.

Problem: DataVault SDF handles can become stale during compaction.
Solution: The pylon batcher checks `IsCompactionFenceActive`, includes descriptor and SDF buffers in the mutation guard, and validates descriptor generation before handing the NativeArray to Burst. If relocation is active or descriptor generation diverges, placement backs off and reports no substrate for the current frame.
Rejected Alternatives: Raw pointer lease, cached NativeArray across defrag, or same-frame handle reuse without descriptor generation check were rejected as stale-memory risks.
Scalability potential: Low backs off immediately under defrag pressure. Middle/high/ultra can retry next tick with the same route and no special-case gameplay truth.
Hardware Impact: Avoids undefined stale reads; expected low-end cost is one failed guard/read branch under defrag pressure, under 5 us.

## Offline Module Architect

Problem: Runtime primitive outpost visuals cannot deliver pressure-rated hard-surface detail without violating Zero-GC and batching rules.
Solution: Added editor-only `ModuleArchitect1712` as an `EditorWindow` and menu entry. It serializes hard-surface module meshes/prefabs, inserts beveled/chamfered topology, cuts socket faces into ring topology, bakes vertex color wear/rust masks in a Burst `IJobParallelFor`, saves meshes through `AssetDatabase`, and creates primitive `COL_` child `BoxCollider` proxies only.
Rejected Alternatives: Runtime CSG, runtime mesh mutation, `MeshCollider`, and material clone authoring were rejected because they move art construction into the player executable or force expensive physics/render paths.
Scalability potential: Low bakes a single flat bevel and lighter mask contrast. Middle increases bevel width and mask variation. High/ultra bake larger chamfers and denser visual masks while remaining static runtime assets.
Hardware Impact: On i3/MX350, runtime cost is transferred to editor bake. Expected runtime win is 0 B allocation and no visual mesh collision traversal; collider proxies stay PhysX primitive.

Problem: Bevels can self-intersect when bevel width exceeds local half-extents near sockets and corners.
Solution: Bevel width is clamped against the smallest module extent; socket cutouts clamp hole width and height to local face spans. Triangle winding is guarded by cross-normal dot correction, and bounds/normal validation rejects empty or non-finite meshes.
Rejected Alternatives: Fixed 0.34 m bevel on every shape and blind triangle append were rejected because smaller modules would invert or collapse.
Scalability potential: Low-middle-high-ultra all consume continuous `GlobalQualityWeight`; the result is different offline mesh geometry, not a runtime branch.
Hardware Impact: Prevents invalid culling bounds and driver-facing bad geometry. Estimated editor bake overhead remains under milliseconds for the generated module set.

## Verification Limits

Problem: The requested `dotnet build` cannot be launched under the local guard.
Solution: Checked build gate three times. First gate: CPU 100% with active `dotnet` processes. Second gate: CPU 83% with active `dotnet` PID 3100. Third gate: CPU 100% with multiple active `dotnet` processes. Build was not launched.
Rejected Alternatives: Starting another build anyway was rejected by the explicit CPU/compiler rule.
Scalability potential: No runtime impact; protects shared workstation throughput for the 20+ agent environment.
Hardware Impact: No compile result. Static sweeps and hash proof were completed, but syntax remains unverified by build.

## Self-Refinement Continuation

Problem: After removing `new Material()`, the development construction proxy could still build an invisible root with a null `sharedMaterial`.
Solution: `ConstructionRuntimeProxyFactory` now resolves the authored editor material `Assets/_Project/Art/Materials/Construction/Mat_Module_Foundation.mat` and fails before object creation when no shared material exists.
Rejected Alternatives: Hidden `Shader.Find`, `RuntimeShaderReferenceCatalog` fallback, or assigning null material were rejected because they either recreate the clone problem or create false visual proof.
Scalability potential: Low devices avoid material instantiation and missing-material draw churn; middle/high/ultra keep authored proxy visuals in editor without changing runtime authoring rules.
Hardware Impact: Estimated i3/MX350 gain remains 10-80 us per avoided clone path; the main win is preventing an invisible debug object from masking missing prefab authoring.

Problem: Pylon scheduling used several manual early-return release calls after acquiring profile/socket fences and the DataVault mutation guard.
Solution: Failure paths now rely on the schedule `finally`; upload finalization wraps every post-completion branch in `try/finally` and releases the guard exactly after job-owned NativeArray views are no longer needed.
Rejected Alternatives: Releasing the mutation guard before scheduled jobs finish was rejected because it can permit compaction while Burst jobs still reference vault arrays.
Scalability potential: Low devices back off cheaply under compaction; high-tier devices can run longer pylon jobs without stale-buffer risk.
Hardware Impact: No measurable steady-state cost; removes deadlock/leak vector on exception or early return.

Problem: The pylon job still carried dead approximate-SDF support code after the mock route was removed.
Solution: Removed `ApproximateSdf` and the first-distance proxy blend branch. Hit truth now comes only from the real encoded voxel SDF sample/march path; invalid payload writes `SnapFailed_NoSubstrate`.
Rejected Alternatives: Keeping proxy vertical length as a cheap support estimate was rejected because it can look like terrain truth even when not a real surface hit.
Scalability potential: Low uses fewer rays and nearest real-SDF sampling; middle/high/ultra increase rays, march steps, and interpolation weight against the same authoritative buffer.
Hardware Impact: Saves a few scalar branches per ray and removes false support state. Exact gain is below profiler resolution until runtime test.

Problem: `ResolveRayBudget`, `ResolveMarchSteps`, and `ResolveSdfInterpolationWeight` returned ultra values regardless of `GlobalQualityWeight`, contradicting the project scalability rule and existing test contract.
Solution: Restored continuous interpolation from low to ultra values. This scales fidelity/cadence only; terrain authority and DTO layout remain unchanged.
Rejected Alternatives: Binary low/ultra switches and fixed ultra-only settings were rejected as hostile to MX350-class devices.
Scalability potential: Low = one ray/low march/nearest SDF. Middle = interpolated ray and step count. High/ultra = full ray count, trilinear SDF sampling, and higher march budget.
Hardware Impact: On i3/MX350, low quality avoids unnecessary rays and trilinear samples; estimated pylon job reduction is proportional to ray count and march-step reduction.

Problem: The previous job test only proved the valid encoded-SDF path, not the absent-SDF fail-closed path.
Solution: Added `MissingVoxelSdf_FailsClosedWithNoSubstrateFlag`, asserting no active pylon, no hits, and `SnapFailed_NoSubstrate` when `EncodedVoxelSdfTexture3D` is absent.
Rejected Alternatives: HUD-only verification was rejected because presentation notification does not prove job truth.
Scalability potential: No runtime impact; protects all tiers from false support.
Hardware Impact: Editor test only.

Problem: Global orphan `.meta` scan reports unrelated orphan files under `.codexbuild` and `Assets/Shapes`.
Solution: Assigned-domain scan reports zero orphan metas for outpost/construction/structure-generator/test paths touched by this task. Non-domain orphan cleanup is left untouched to avoid cross-agent asset deletion.
Rejected Alternatives: Deleting unrelated generated-package metas was rejected under domain-boundary and parallel-agent safety rules.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact.

Problem: The offline module generator still represented socket truth as generated child GameObjects, while the active construction catalog route consumes `BaseModuleTemplate.SocketDefinition` into unmanaged DTOs.
Solution: `ModuleArchitect1712` now creates or updates one `BaseModuleTemplate` asset per generated module, writes `socketDefinitions`, `snapPoints`, proxy bounds, and binds that template to a root `BaseModule` before prefab serialization. Generated prefabs no longer create `Socket_` child objects as their source of snapping truth.
Rejected Alternatives: Adding a parallel custom metadata component was rejected because `BaseModuleTemplate -> BaseModuleCatalogRuntime -> SocketDefinitionDTO` is the existing first-party O(1) route. Keeping empty socket children was rejected because it preserves a scene-hierarchy dependency in generated content.
Scalability potential: Low devices consume pre-baked socket DTOs with no hierarchy traversal. Middle/high/ultra can add richer visual geometry without changing the socket authority path.
Hardware Impact: Avoids cold prefab child/socket scan pressure for generated modules and keeps runtime snapping on the existing catalog DTO lane; exact gain requires Unity profiling.

Problem: After `new Material()` removal, pylon and blueprint preview components still carried serialized shader fields and editor shader-path lookups that implied an inactive fallback route.
Solution: Removed `pylonShader`, `previewShader`, shader fallback path constants, and editor shader loads from the pylon and preview target files. Missing authored materials now log a single fail-closed error and do not retain a shader-only recovery path.
Rejected Alternatives: Keeping shader fields for diagnostics was rejected because the runtime dependency should be the authored material asset, not a recoverable shader reference.
Scalability potential: Low devices avoid hidden cold authoring recovery work; higher tiers still use the same shared authored material lane.
Hardware Impact: No steady-state frame gain expected; removes cold missing-authoring ambiguity and serialized dependency churn.

Problem: Binding generated module prefabs through root `BaseModule` without an authored `interiorTrigger` lets `BaseModule.CacheReferences()` resolve an arbitrary owned `BoxCollider`, including `COL_` physics proxies, and causes dry-zone warnings.
Solution: `ModuleArchitect1712` now emits an `InteriorTrigger` child with `BoxCollider.isTrigger = true` and serializes that exact collider into `BaseModule.interiorTrigger`.
Rejected Alternatives: Relying on `ComponentReferenceUtility.ResolveOwnedComponent<BoxCollider>()` was rejected because it can select structural proxy colliders. Removing `BaseModule` was rejected because final construction prefabs already use it as the first-party module runtime contract.
Scalability potential: Low devices avoid wrong interior volume classification and warning churn. Middle/high/ultra keep richer generated visuals without changing the construction/runtime component contract.
Hardware Impact: Prevents cold component-resolution ambiguity; no expected steady-state cost increase.

Problem: Foundation pylon no-substrate and structural warning presentation could run while the pylon DataVault mutation guard was still held by the scheduling/finalization path.
Solution: No-substrate failure now releases profile/socket fences and the DataVault guard before clearing/publishing the HUD warning. Upload finalization now releases the DataVault guard after NativeArray upload and telemetry write, before telemetry dump and structural SignalBus publishing.
Rejected Alternatives: Keeping signal/HUD work under the guard was rejected because presentation and I/O are not lightweight vault writes. Releasing before GPU upload was rejected because the upload still reads job-owned NativeArray views.
Scalability potential: Low devices avoid guard-held presentation stalls. Middle/high/ultra keep the same visual output with a shorter guarded section.
Hardware Impact: Removes a potential main-thread stall vector around compaction fences; exact microseconds require profiler capture.

Problem: Generated hard-surface prefabs had a root `BaseModule` and template metadata, but no `ModuleMarker` backed by `BuildableData`. `ConstructionManager` save/load and catalog restore paths resolve stable identity through `ModuleMarker.PrefabId`, so generated modules could be skipped or forced into development proxy fallback.
Solution: `ModuleArchitect1712` now creates or updates `*_Buildable.asset`, writes stable id, module family, template reference, power defaults, and final prefab reference, then attaches a root `ModuleMarker` initialized with that asset before saving the prefab. After `SaveAsPrefabAsset`, the generator back-writes `BuildableData.finalPrefab` to the saved prefab asset. The paired `BaseModuleTemplate` now gets an explicit template hash and kW draw metadata aligned with the generated `BuildableData.powerRating`.
Rejected Alternatives: A new custom metadata MonoBehaviour was rejected because `BuildableData -> ModuleMarker -> ConstructionManager` is the existing persistence owner route. Leaving `ModuleMarker` to be added by development builds was rejected because release builds retire unmarked modules.
Scalability potential: Low devices restore authored prefabs directly from catalog without runtime proxy fabrication. Middle/high/ultra get richer generated static meshes through the same catalog identity path.
Hardware Impact: Removes a save/load skip vector and development proxy fallback path; no steady-state runtime cost added beyond one existing `ModuleMarker` component on authored prefabs.

Problem: The development construction proxy still emitted `Socket_` child GameObjects, and the station module library parsed child `ModuleSocket` components before checking the existing template/catalog metadata route.
Solution: Removed proxy socket GameObject emission. `DeepReachStationModuleLibrary` now resolves `BaseModuleTemplate` through `ModuleMarker.Data` or root `BaseModule`, builds station socket DTOs from `BaseModuleTemplate.SocketDefinition`, and falls back to legacy child `ModuleSocket` only for old prefabs without templates.
Rejected Alternatives: Keeping proxy socket children was rejected because generated modules already own socket truth in `BaseModuleTemplate`. Removing the legacy fallback was rejected because older construction prefabs still contain `ModuleSocket` children and must remain analyzable by editor tools.
Scalability potential: Low devices avoid extra development proxy hierarchy churn. Middle/high/ultra authoring uses richer generated meshes with the same template socket DTO route.
Hardware Impact: Removes one child GameObject allocation per proxy socket in development fallback; no player release cost because this path is editor/development gated.

Problem: The development construction proxy factory still retained a full fallback fabrication lane in the tracked diff: `Shader.Find`, `RuntimeShaderReferenceCatalog`, `new Material(shader)`, generated wire-box `Mesh`, `ProxyVisual`, and `Socket_` child hierarchy. Its class was also hidden behind `UNITY_EDITOR || DEVELOPMENT_BUILD` while runtime callers reference it outside matching guards, creating a player-build compile risk.
Solution: Reduced `ConstructionRuntimeProxyFactory` to an always-compiled compatibility wrapper. `TryCreatePlacedProxy` now returns false for missing `finalPrefab`, logs one constant controlled error, and creates no GameObject, Mesh, Material, collider, PowerNode, BaseModule, managed string, or socket hierarchy.
Rejected Alternatives: Keeping an authored-material proxy was rejected because it still allows missing prefab authoring to enter placement/load flows as a synthetic module. Deleting the class was rejected because existing callers outside the direct 1712 domain need a stable symbol until their owner removes the fallback branch.
Scalability potential: Low devices avoid hidden dev-proxy object and mesh allocation. Middle/high/ultra must use the offline-baked `ModuleArchitect1712` prefab route, so visual overkill stays in serialized assets instead of runtime fabrication.
Hardware Impact: Removes the remaining proxy-fallback cold allocation route and eliminates a release compile visibility hazard. Exact runtime microseconds are not claimed without Unity profiler capture.

Problem: The offline module baker still mapped `GlobalQualityWeight` mostly to bevel width and vertex mask contrast; the actual chamfer topology remained one flat segment even at high quality, leaving the high-tier specular response below the hard-surface mandate.
Solution: `ModuleArchitect1712` now resolves quality to 1..3 bevel segments, tessellates edge bands and rounded corner patches, writes smooth split normals per bevel vertex, clamps bevel width against 40% of the smallest half extent, and prewarms editor mesh lists to the maximum generated topology capacity. The default set can also be baked directly from a MenuItem without first opening the window.
Rejected Alternatives: Adding runtime smoothing, material tricks, or shader-only bevel illusion was rejected because hard-surface geometry is an offline asset-authoring responsibility. Leaving the single chamfer was rejected because it wastes high-tier visual budget and produces flat 90-degree-adjacent highlight breaks.
Scalability potential: Low bakes one cheap chamfer segment and remains readable on compact hardware. Middle bakes two segments. High/ultra bake three segment edge/corner patches for stronger grazing highlights while runtime still consumes a static mesh and shared material.
Hardware Impact: Player runtime cost remains 0 B allocation and no runtime mesh mutation. Editor bake vertex/index count rises inside prewarmed buffers; no MX350 gameplay frame cost is introduced.

Problem: Compilation was requested by protocol but the host was under load.
Solution: CPU/compiler guard was sampled again: CPU 100%, active `dotnet` PIDs 3100, 20868, 25520, 25676, 26688, 27664, and 29636. Build/test launch was withheld.
Rejected Alternatives: Launching `dotnet build` during 100% CPU and active compiler/runtime processes was rejected by the strict throttle rule.
Scalability potential: No runtime effect.
Hardware Impact: No compile artifact. Current proof remains static source scan and bracket balance until host load permits a single build.

Problem: After runtime material synthesis was removed, pylon and blueprint preview components still destroyed `HideFlags.DontSave` materials in `OnDestroy`, preserving an obsolete ownership assumption from the clone era.
Solution: Removed the `Destroy(pylonMaterial)` and `Destroy(previewMaterial)` teardown branches. These components now consume serialized/shared authored materials and do not own or destroy material lifetime.
Rejected Alternatives: Keeping the cleanup was rejected because a designer-assigned/shared material with `DontSave` flags could be destroyed by a component that did not allocate it. Reintroducing runtime-owned material tracking was rejected because runtime material ownership itself is the removed P0 path.
Scalability potential: Low/middle/high/ultra all stay on shared material assets; quality differences must come from authored materials, buffers, and offline geometry, not per-instance clones.
Hardware Impact: No steady-state frame delta. Removes a teardown-time asset lifetime hazard and closes the last material-clone ownership residue in the 1712 target components.

Problem: Final compile verification was requested but the host still had active compiler/runtime pressure.
Solution: Re-ran the throttle guard and withheld build/test launch because CPU was 68% and active `dotnet` PIDs 3100 and 24796 were present.
Rejected Alternatives: Starting a build under active dotnet processes was rejected by the explicit CPU/compiler throttle.
Scalability potential: No runtime effect.
Hardware Impact: No compile artifact. Current proof remains static forbidden-symbol scan, diff-check, and bracket balance until the host is clear.

Problem: `ModuleArchitect1712` collider proxies were authored as solid hull/keel/crown boxes. That is cheap, but it can physically fill the playable interior of generated modules and contradicts the pressure-hull shell model.
Solution: Replaced full-volume proxies with primitive shell colliders: floor, ceiling, side walls, and socket-aware doorway frame colliders. The root and proxies are assigned to `World_Static` when the layer exists.
Rejected Alternatives: Keeping a single solid hull proxy was rejected because it blocks interior traversal. MeshCollider was rejected by mandate and PhysX cost. Runtime trigger repair was rejected because collider truth must be baked offline.
Scalability potential: Low devices get primitive collider surfaces only. Middle/high/ultra can spend visual budget on denser static mesh bevels while physics remains primitive and predictable.
Hardware Impact: No managed runtime allocation. Expected gameplay benefit is collision correctness; CPU cost remains primitive BoxCollider broadphase/narrowphase rather than visual mesh traversal.

Problem: The generator wrote serialized fields through raw `FindProperty` calls and accepted any output/material path, producing late null failures if contracts drifted or the path was outside the AssetDatabase.
Solution: Added `NormalizeAssetFolder`, `NormalizeAssetPath`, `RequireProperty`, and `RequireRelativeProperty`. The generator now fails early with the missing field/path name before saving bad assets.
Rejected Alternatives: Direct public field access was rejected because several fields are private serialized contract fields. Leaving raw `FindProperty` was rejected because it converts schema drift into unclear `NullReferenceException`.
Scalability potential: Editor-only; all device tiers benefit indirectly from cleaner baked assets and fewer bad prefab variants.
Hardware Impact: No player-runtime cost.

Problem: Compile/test execution is still requested by protocol, but the host remains above the allowed threshold.
Solution: Final guard sampled CPU 62% with active `dotnet` PID 3100. Build/test launch was withheld.
Rejected Alternatives: Launching another build was rejected by the explicit throttle.
Scalability potential: No runtime effect.
Hardware Impact: No compile artifact. Static proof after this pass: forbidden scan clean, brace/parens/brackets balanced, orphan meta count 0 in assigned paths.

Problem: Real voxel SDF metadata with zero range or non-finite origin could still be converted into sanitized defaults in non-job utility paths, hiding corrupt terrain authority as a tiny valid substrate.
Solution: `FoundationPylonGpuBatch` rejects descriptor `SdfRangeMeters <= 0.0001f`; `CalculateFoundationPylonsJob` requires finite positive origin/cell/range and exact voxel count; `SanitizeSdfConfig` clears `RealVoxelSdf`, sets `SnapFailed_NoSubstrate`, zeroes dimensions/range/origin, and never promotes corrupt real payloads to positive defaults.
Rejected Alternatives: Clamping corrupt real SDF range to epsilon or defaulting dimensions to 2x2x2 was rejected because it fabricates construction truth.
Scalability potential: Low through ultra all consume the same fail-closed truth; quality only changes ray budget/interpolation after a valid SDF is present.
Hardware Impact: No steady-state frame cost. Prevents false support and NaN propagation into no-substrate diagnostics.

Problem: Generated hard-surface modules could be fully baked to disk but remain absent from the first-party construction catalog, leaving the builder/save route unable to discover them by stable `BuildableData`.
Solution: `ModuleArchitect1712` now appends generated `*_Buildable.asset` references to `Assets/_Project/Data/Construction/ModuleCatalog_Starter.asset` while preserving the existing authored list. The catalog asset is created only if the project copy is missing.
Rejected Alternatives: Manual inspector drag-in was rejected because the offline generator must produce integrated construction assets. Replacing the whole catalog list was rejected because other agents/authored modules own existing entries.
Scalability potential: Low through ultra all consume the same catalog identity route; high-tier visual complexity stays in the generated prefab/mesh, not in catalog lookup logic.
Hardware Impact: Editor-only mutation. Runtime benefit is avoiding catalog misses that can route placement/load into development fallback handling.

Problem: Adding `ModuleMarker` to temporary editor bake roots allowed `OnEnable` to register those transient objects into `WorldSpatialHashGrid` outside play mode.
Solution: `ModuleMarker.OnEnable` now returns when `Application.isPlaying` is false, so editor-time prefab generation serializes the marker without mutating runtime spatial state.
Rejected Alternatives: Removing `ModuleMarker` from generated prefabs was rejected because save/load identity depends on `BuildableData -> ModuleMarker`. Adding an editor-only fake marker was rejected as a parallel metadata route.
Scalability potential: All tiers keep one persistence identity path; editor generation no longer leaks into spatial runtime state.
Hardware Impact: No frame-time claim. Prevents editor bake side effects and avoids stale spatial handles on generated prefab roots.

Problem: Compile/test execution remains required, but the host is still outside the allowed throttle envelope.
Solution: Latest guard reported CPU 100% and active `dotnet` PID 3100. Build/test launch was withheld.
Rejected Alternatives: Running `dotnet build` under 100% CPU and an active dotnet process was rejected by the explicit compile-throttle rule.
Scalability potential: No runtime effect.
Hardware Impact: No compile artifact. Current proof is static scan, bracket balance, diff-check, and orphan-meta scan only.

Problem: `FoundationStructuralWarningSignal` is an unmanaged SignalBus payload but was not included in the foundation layout gate.
Solution: Added `FoundationStructuralWarningSignalSizeBytes`, `UnsafeUtility.SizeOf<FoundationStructuralWarningSignal>()` validation, and editor offset assertions for `WarningFlags` and `ResultHash`.
Rejected Alternatives: Trusting `[StructLayout(Size=64)]` without runtime validation was rejected because signal DTO drift can break Burst/ARM64 cache-line assumptions.
Scalability potential: All tiers use the same 64-byte signal payload; quality changes do not alter signal layout.
Hardware Impact: No frame-time cost. Validation is cold/editor proof; it prevents silent unmanaged payload drift.

Problem: Generated hard-surface modules had a single visual mesh, so weak devices would keep evaluating the same bevel-heavy topology at distance.
Solution: `ModuleArchitect1712` now bakes LOD0/LOD1/LOD2 mesh assets and serializes a standard `LODGroup`; root LOD0 `MeshFilter` is preserved for legacy root-mesh consumers.
Rejected Alternatives: Runtime mesh decimation was rejected because geometry generation belongs to editor bake. Removing root renderer was rejected because existing consumers inspect root `MeshFilter`.
Scalability potential: Low/compact devices shed to one-segment low-detail mesh earlier. Middle/high/ultra keep LOD0 longer through standard LODGroup residency without changing gameplay truth.
Hardware Impact: Runtime gets authored LOD switching, not managed generation. Exact frame gain requires Unity profiler capture; expected benefit is reduced far-distance vertex cost.

Problem: Compile/test execution remains requested after code changes.
Solution: Latest guard reported CPU 71% and no active compiler process listing. Build/test launch was withheld because CPU alone exceeds the 50% threshold.
Rejected Alternatives: Running `dotnet build` at 71% CPU was rejected by the explicit compile-throttle rule.
Scalability potential: No runtime effect.
Hardware Impact: No compile artifact.

Problem: `CreateEmptySdfConfig()` correctly marked missing substrate, but `SanitizeSdfConfig()` could still promote that explicit no-substrate payload into positive voxel dimensions and a positive range.
Solution: `SanitizeSdfConfig()` now detects non-real `SnapFailed_NoSubstrate` configs first, preserves finite origin, zeroes size/range/reserved fields, and returns without applying positive defaults. Added editor coverage for `SanitizeSdfConfig(CreateEmptySdfConfig(origin))`.
Rejected Alternatives: Keeping default 2x2x2 dimensions was rejected because diagnostics and downstream consumers could misread a missing SDF as a tiny valid SDF volume.
Scalability potential: Low through ultra all share the same fail-closed substrate truth. Quality only changes ray/march/interpolation after a valid real SDF exists.
Hardware Impact: No steady-state frame cost. Prevents false support diagnostics and avoids any future fallback work keyed off non-zero SDF dimensions.

Problem: Compile/test execution remains requested after the no-substrate DTO patch.
Solution: Latest throttle guard reported CPU 99.4% and no active compiler processes. Build/test launch was withheld because CPU exceeds the 50% threshold.
Rejected Alternatives: Running `dotnet build` at 99.4% CPU was rejected by the explicit compile-throttle rule.
Scalability potential: No runtime effect.
Hardware Impact: No compile artifact. Current proof is static scan and `git diff --check` only.

Problem: `BaseModuleCatalogRuntime` still contained an authored-data bypass: `ScheduleMockCatalog`, hardcoded module hashes, and `GenerateMockModuleCatalogJob` could publish fake construction modules.
Solution: Removed the generated mock catalog route entirely and left only real binary/template DTO paths. Added editor coverage that scans the runtime source for the retired route and verifies real template-to-DTO conversion.
Rejected Alternatives: Keeping the mock route behind development usage was rejected because public static APIs drift into runtime callers under parallel agent work. Replacing it with another synthetic default catalog was rejected because construction identity must come from authored `BaseModuleTemplate`/binary catalog data.
Scalability potential: Low through ultra all use the same authoritative catalog truth. Device quality can affect visuals and LODs, not module identity or socket topology.
Hardware Impact: Removes a cold fake-catalog job and associated hash writes. No steady-state frame-time gain is claimed; the value is eliminating non-authoritative module truth.

Problem: `TryEnsureVaultBuffers` was a public cold bootstrap API that always returned `false`, so real catalog lane setup could silently fail even after the mock route was retired.
Solution: Routed it through existing `TryAcquireCatalogWriteViews`, validated the real state/module/socket/cost/hash/telemetry lanes, stamped only the empty-state endian flag, and released the mutation guard in a strict `finally`.
Rejected Alternatives: Allocating a separate catalog bootstrapper was rejected because the existing runtime already owns the DataVault lane IDs and mutation guard mask. Returning false as a fail-closed behavior was rejected because callers need a real cold bootstrap path, not a permanent failure.
Scalability potential: Low through ultra share the same native lane setup; larger catalogs should arrive through binary hydration, not per-device synthetic defaults.
Hardware Impact: Cold-path only. It restores real DataVault readiness without adding hot-loop polling, scene lookup, or managed allocation in simulation phases.

Problem: Compile/test execution remains requested after catalog runtime changes.
Solution: Latest throttle guard reported CPU 88.4% and no active compiler processes. Build/test launch was withheld because CPU exceeds the 50% threshold.
Rejected Alternatives: Running `dotnet build` at 88.4% CPU was rejected by the explicit compile-throttle rule.
Scalability potential: No runtime effect.
Hardware Impact: No compile artifact. Current proof is forbidden-token scan, bracket balance, and `git diff --check`.

Problem: `BaseModuleCatalogRuntime` still exposed a no-lease `ScheduleHydrateCatalog` overload that returned the incoming dependency and default views. A real hydrate write job cannot be protected without returning the write lease to the caller.
Solution: Removed the unsafe/no-op overload and kept only the overload that returns `ModuleCatalogWriteLease` for explicit caller release after job completion.
Rejected Alternatives: Completing the hydration job inside the overload was rejected because synchronous completion is banned without profiler proof. Releasing the guard before the job completes was rejected because compaction could move catalog lanes while the job writes.
Scalability potential: All tiers keep the same catalog hydration route; capacity and visual fidelity are orthogonal to hydration authority.
Hardware Impact: No steady-state cost. Removes a cold API trap that could hide a failed catalog hydrate.

Problem: `ShinobuSocketConstructionRuntime` still had a public editor-triggered synthetic grid publisher, managed mock scratch arrays, a mock builder ghost validation job, and mock-labeled runtime capacity constants. That path could publish fake socket topology into the construction vault.
Solution: Removed `GenerateMockBaseConstructionGrid`, the editor mock button, `GenerateMockBuilderGhostValidationJob`, the mock builder state buffer handle, and all managed `s_Mock*` arrays. Renamed capacity constants to `ModuleCapacity`, `SocketsPerModuleCapacity`, and `SocketCapacity`, and added editor regression coverage for no synthetic grid route plus DTO layout checks.
Rejected Alternatives: Keeping the grid as an editor diagnostic was rejected because it writes into the same authoritative construction vault lanes. Moving it behind another flag was rejected because the problem is not access level; it is fake topology publication.
Scalability potential: Low through ultra all consume real authored/socket-vault topology. Device quality can alter ghost presentation cadence and visual fidelity, not module/socket truth.
Hardware Impact: Removes cold managed arrays sized for 500 modules and 3000 sockets from the mock grid route. No steady-state frame-time claim; runtime truth is cleaner and less failure-prone.

Problem: Compile/test execution remains requested after socket construction changes.
Solution: Latest throttle guard reported CPU 98.7% and active `dotnet` PID 2588. Build/test launch was withheld.
Rejected Alternatives: Running `dotnet build` at 98.7% CPU with active dotnet was rejected by the explicit compile-throttle rule.
Scalability potential: No runtime effect.
Hardware Impact: No compile artifact. Current proof is forbidden-token scan, bracket balance, orphan-meta scan, and `git diff --check`.

Problem: `BulkheadContainmentRuntime` still owned two synthetic authority routes: generated bulkhead rows and generated hatch fluid compartments used for pressure locking.
Solution: Removed the bulkhead mock job/scheduler and the hatch mock pressure job/buffer handle. Hatch pressure now reads only `ShinobuFluidCompartmentFront`; missing real fluid data schedules the existing `MarkHatchFluidUnavailableJob` and records missing-compartment telemetry.
Rejected Alternatives: Keeping mock pressure behind serialized toggles was rejected because it writes pressure truth into the same hatch FSM lane as real fluid. Creating a new fluid adapter was rejected because `ShinobuFluidCompartmentFront` is already the first-party front-buffer route.
Scalability potential: Low through ultra keep one pressure authority route. Quality can alter hatch cadence and presentation, not whether fake fluid may unlock/lock a hatch.
Hardware Impact: Removes cold synthetic row writes and one mock buffer pin from the schedule path. No steady-state microsecond claim without Unity profiler capture.

Problem: Compile/test execution remains requested after bulkhead/hatch route changes.
Solution: Latest throttle guard reported CPU 71%, active `dotnet` PID 2588, and active `VBCSCompiler` PID 23852. Build/test launch was withheld.
Rejected Alternatives: Running `dotnet build` under CPU >50% and active compiler/runtime processes was rejected by the explicit throttle rule.
Scalability potential: No runtime effect.
Hardware Impact: No compile artifact. Current proof is precise forbidden-token scan, hot-loop token scan, delimiter balance, and `git diff --check`.

Problem: `SumpPumpPipeGridRuntime` still owned a deterministic mock drainage authority route. It could write a 2000-node/6000-edge fake pipe graph into the same Vault lanes used by real drainage topology, and editor UI exposed the trigger.
Solution: Removed `GenerateMockPipeNetworkJob`, all mock seed fields/finalizers, public mock facade, auto-generate toggle, editor mock button, and mock node/edge flags. Empty counters now resolve to zero, not capacity defaults. Missing topology records `TopologyInvalid|HeartbeatFrame` telemetry and does not schedule the drainage solve.
Rejected Alternatives: Keeping the route for diagnostics was rejected because it writes into authoritative construction lanes. Keeping count fallback to capacity was rejected because it silently converts absent topology into fake work.
Scalability potential: Low through ultra consume the same authored drainage topology route. Quality can change cadence and delta passes only after real topology exists.
Hardware Impact: Removes a cold fake graph job and prevents solver work when no authored topology exists. No profiler-backed steady-state microsecond claim.

Problem: `FoundationPylonGpuBatch` still converted `ConstructionPreviewSignal` into a `FoundationModuleAupDTO` when no real socket module input existed. That made support positioning render from a presentation-only module source.
Solution: Removed `TryPopulatePreviewFallback`. Pylon scheduling now clears/does nothing when socket module vault input is absent, and still requires a valid encoded voxel SDF before any pylon solve.
Rejected Alternatives: Retaining presentation-only pylon supports was rejected because the pylon system is structural feedback; preview signals are not the ownership route for module placement truth.
Scalability potential: Low through ultra keep one structural support truth path: socket module vault data plus encoded voxel SDF. Device quality may alter ray budget/march fidelity, not input authority.
Hardware Impact: Removes one signal snapshot scan and prevents stale/fake support visualization. Exact frame gain requires Unity profiler capture.

Problem: Compile/test execution remains requested after sump/foundation fallback removal.
Solution: Latest throttle guard reported CPU 91.9%, active `dotnet` PID 30272, and active `VBCSCompiler` PID 19648. Build/test launch was withheld.
Rejected Alternatives: Running `dotnet build` under CPU >50% with active compiler/runtime processes was rejected by the explicit throttle rule.
Scalability potential: No runtime effect.
Hardware Impact: No compile artifact. Current proof is forbidden-token scan, hot-loop token scan, delimiter balance, and `git diff --check`.

Problem: `ModularBaseConstructionValidator` still carried mock sampler naming and an emergency synthetic bounds seed that could write fallback `ConstructionBuilderBounds` into the same vault route used by authored construction bounds.
Solution: Renamed the sampler route to `ConstructionTerrainSampler`/`CreateTerrainSampler`, removed `GenerateEmergencyMockBounds` and `EmergencyMockBoundsCount`, and changed `InitializeVault` to only ensure the bounds override buffer instead of publishing fake rows. Updated `PlayerBuilder` and `WfcBuilderTunerWindow` to the real sampler contract and added regression coverage in the existing cross-domain edit tests.
Rejected Alternatives: Keeping the synthetic bounds as a diagnostic seed was rejected because it mutates authoritative construction input. Creating a parallel adapter was rejected because the existing validator owns this placement-clearance contract.
Scalability potential: Low, middle, high, and ultra tiers use the same authored/override bounds truth. Quality can change placement visualization and feedback cadence, not whether fake module bounds are injected.
Hardware Impact: Removes cold synthetic bounds writes and avoids false placement clearance. No profiler-backed microsecond claim; proof is source scan, delimiter check, and `git diff --check`.

Problem: Compile/test execution remains requested after validator cleanup.
Solution: One allowed `dotnet build .\Assembly-CSharp-Editor.csproj --no-restore` attempt was launched while CPU was 32% and no compiler process was active. It failed before edited construction files on `Assets/Editor/HectonMcpBridgeAutoConnect1428.cs` missing `MCPForUnity.Editor.*` namespaces. After `dotnet build-server shutdown`, one allowed `dotnet build .\Assembly-CSharp.csproj --no-restore` runtime build ran at CPU 23% and succeeded with 0 errors and existing reference warnings.
Rejected Alternatives: Editing `HectonMcpBridgeAutoConnect1428.cs` was rejected because it is outside the assigned construction domain and already has unrelated local changes. Additional repeated builds were rejected; runtime was verified once after the editor dependency failure.
Scalability potential: No runtime effect.
Hardware Impact: Runtime compile artifact produced at `Temp\CodexBuild\Assembly-CSharp\Assembly-CSharp.dll`. Current proof is runtime build success, forbidden-token scan, hot-loop token scan on edited construction runtime files, delimiter checks, `git diff --check`, and the captured unrelated editor dependency failure.

Problem: `HabitatDeconstructionTransactionKernel` and `DroneFleetTransactionKernel` still declared unused synthetic seed jobs that could fabricate teardown cost rows and drone transaction rows if a future caller wired them back in.
Solution: Removed `GenerateMockDeconstructionDataJob` and `GenerateMockDroneTransactionsJob` definitions. Added one existing-suite regression that asserts the synthetic seed jobs stay absent while the real execution jobs remain present.
Rejected Alternatives: Keeping the structs as dormant test helpers was rejected because no call site exists and dormant public/internal Burst jobs become attractive fake-data entry points under parallel-agent work.
Scalability potential: Low through ultra tiers now share the same authored/live transaction input route. Quality may change execution budget, not whether transaction truth can be seeded synthetically.
Hardware Impact: Removes two unused Burst job definitions and their fake hash/default writes. No steady-state frame-time claim because they had no active call site.

Problem: Compile/test execution remains requested after transaction kernel cleanup.
Solution: Runtime build was withheld because throttle guard reported active `dotnet` PID 48016 running Unity Roslyn `VBCSCompiler.dll`. Static proof was kept to source scans, delimiter balance, and `git diff --check`.
Rejected Alternatives: Launching a new `dotnet build` while Unity Roslyn compiler server is active was rejected by the compile-throttle rule. Killing PID 48016 was rejected because it is not proven to be owned by this agent.
Scalability potential: No runtime effect.
Hardware Impact: No new compile artifact for this incremental change. Previous runtime build was clean before this change; this change is structurally verified but not compiled.

Problem: `MarauderOutpostGenerationService.ScheduleMatrixExtraction()` still invented a flat terrain volume when the real heightmap payload was absent, then allowed extraction to continue from synthetic ground truth.
Solution: Moved real heightmap validation before extraction scratch preparation. Missing payload now sets `_missingHeightmap`, writes `FaultFlag|MissingHeightmapFlag`, dumps the black-box ring, faults the generation state, and schedules no extraction job.
Rejected Alternatives: A flat terrain fallback based on `OriginMeters.y - StiltClearanceMeters` was rejected because it fabricates support placement. Preparing scratch then failing was rejected because missing data should not warm extraction buffers.
Scalability potential: Low through ultra all use the same heightmap authority. Quality may change outpost visual density and decay response, not terrain truth.
Hardware Impact: Avoids extraction scratch prep and matrix/support generation on missing heightmap. No profiler-backed microsecond claim; static proof is clean retired-token scan and source-order regression.

Problem: `MarauderOutpostMatrixExtractionJob.SampleHeight()` still encoded fallback-height behavior, and descriptor naming still called the state a fallback instead of a missing-authority fault.
Solution: `SampleHeight()` now samples only valid heightmap data. Invalid heightmap metadata exits the job immediately with `Counters[4] = 1`. The shared WFC descriptor flag was renamed from `DescriptorFlagHeightmapFallback` to `DescriptorFlagMissingHeightmap` without changing bit `1 << 1`.
Rejected Alternatives: Keeping the old flag name as compatibility alias was rejected because it preserves fallback semantics in the owner contract. Using a local `1u << 1` literal was rejected because it creates two owners for one bit.
Scalability potential: Low, middle, high, and ultra keep one descriptor bit route; device quality does not alter DTO layout or authority state.
Hardware Impact: No steady-state frame cost. Prevents false support and stale fallback telemetry wording.

Problem: Compile/test execution remains requested after the Marauder heightmap changes.
Solution: Build was withheld. First guard: CPU 100% with active `dotnet` PIDs 48016 and 54108. Second guard after 30 seconds: CPU 32%, but active `dotnet` PID 48016 is Unity Roslyn `VBCSCompiler.dll`.
Rejected Alternatives: Running `dotnet build` while any compiler process is active was rejected by the explicit throttle. Killing Unity Roslyn was rejected because it is outside this agent's ownership.
Scalability potential: No runtime effect.
Hardware Impact: No new compile artifact. Current proof is forbidden-token scan, hot-loop token scan, orphan-meta scan, `git diff --check`, and regression test source changes.

Problem: `CalculateFoundationPylonsJob.ResolveSdfNormal()` ignored failed neighbor `TrySampleSdf()` calls. Near a real voxel SDF boundary, an out-of-bounds neighbor could contribute the sentinel max pylon length as gradient data and skew a valid pylon normal.
Solution: Captured all sample validity bits. The normal now uses central differences when both sides are valid, one-sided differences when only one neighbor plus center is valid, and zero axis contribution when the axis has no real sample. Added a boundary encoded-SDF regression that keeps a plane normal upright near the x edge.
Rejected Alternatives: Keeping sentinel fallback distances was rejected because it fabricates slope at the SDF volume edge. Failing the whole pylon hit on one gradient-neighbor miss was rejected because the center hit is still real substrate data.
Scalability potential: Low through ultra use the same real SDF gradient contract. Quality still controls ray budget and interpolation weight, not whether invalid samples become surface truth.
Hardware Impact: No allocation change. Hit-only normal resolution performs one additional center sample and replaces invalid-neighbor sentinel math with branch-local real-sample gradients; no profiler-backed microsecond claim.

Problem: Compile/test execution remains requested after foundation boundary normal hardening.
Solution: Build was withheld because throttle guard reported CPU 91% and active `dotnet` PID 48016. Static checks were used: retired-token scan, hot-loop lookup/allocation scan, `.Complete()` source scan, brace balance, orphan-meta scan, and `git diff --check`.
Rejected Alternatives: Launching `dotnet build` under CPU >50% with an active compiler/runtime process was rejected by the explicit throttle. Killing the process was rejected because it is not owned by this agent.
Scalability potential: No runtime effect.
Hardware Impact: No new compile artifact for this incremental patch.

Problem: Three-segment bevel/corner tessellation outgrew the old fixed generated mesh list capacity, so editor bake could silently reallocate while building high-quality static modules.
Solution: Split the capacity contract into socket-face, edge-bevel, and corner-bevel maxima and derive `GeneratedVertexCapacity`/`GeneratedIndexCapacity` from those exact bounds. Added editor-source regression coverage in the existing 1716 suite.
Rejected Alternatives: Letting `List<T>` grow dynamically was rejected because the generator is supposed to prove its offline memory envelope. Over-allocating an arbitrary large number was rejected because the bound is cheaply derivable from topology terms.
Scalability potential: Low still bakes single-segment chamfers; middle/high/ultra can bake up to three bevel segments without editor allocation drift. Runtime consumes serialized meshes only.
Hardware Impact: Editor-only allocation avoidance. Player runtime frame cost remains unchanged.

Problem: Generated module catalog registration was object-reference-only. Changing output folders or regenerating assets with the same stable id could append duplicate `BuildableData` entries and create ambiguous construction catalog truth.
Solution: `RegisterGeneratedBuildablesInCatalog()` now searches existing catalog entries by `BuildableData.PersistentId` and replaces the reference in place. Added regression coverage asserting the stable-id route and absence of the old object-reference-only append path.
Rejected Alternatives: Clearing the whole starter catalog was rejected because it would delete designer-owned entries. Keeping duplicates was rejected because `PersistentId` is the save/catalog identity owner.
Scalability potential: Low through ultra all load one stable module entry per generated id. Visual fidelity and LOD complexity remain separate from catalog identity.
Hardware Impact: Editor-only catalog cleanup. Runtime avoids duplicate catalog ambiguity without adding hot-path work.

Problem: Compile/test execution remains requested after catalog dedupe and capacity correction.
Solution: Build was withheld because throttle guard reported CPU 100% and active `dotnet` PIDs 24940 and 30272. Static checks completed: target forbidden-token scan empty, hot-loop lookup/allocation scan empty, braces balanced, target orphan-meta scan empty, and `git diff --check` clean except existing CRLF warnings.
Rejected Alternatives: Starting another `dotnet build` under CPU 100% with active compiler/runtime processes was rejected by the explicit throttle rule.
Scalability potential: No runtime effect.
Hardware Impact: No new compile artifact for this incremental patch.

Problem: `ModuleArchitect1712` already emitted top/bottom socket metadata, but the generated hard-surface mesh and primitive collider shell still sealed floor and ceiling apertures.
Solution: Added Y-axis visual cutouts through `AddYFaceWithOptionalCutout` and socket-aware `AddYSlabProxy` collider frames for `COL_FloorProxy`/`COL_CeilingProxy`. Existing side-wall proxy logic stays untouched.
Rejected Alternatives: Leaving vertical sockets as metadata-only was rejected because it makes generated prefabs lie to snapping/catalog consumers. A `MeshCollider` hatch proxy was rejected because primitive proxies are the established 1716 collider route.
Scalability potential: Low devices receive simple serialized primitive frames. Middle/high/ultra can bake richer bevel/LOD visuals around the same authored hatch aperture without runtime mesh mutation.
Hardware Impact: Editor-only generation. Player runtime keeps static meshes and primitive colliders; no managed allocation or runtime CSG added.

Problem: The vertical socket route existed only as code after the aperture fix; the default generated module set still had no top/bottom socket module to exercise it.
Solution: Added `H8_A1712_VerticalShaft_01` to the existing `ModuleSpec[]` and introduced `SocketMask.Vertical = Top | Bottom`. The shaft keeps north/south side access plus vertical stack access and flows through the existing template/buildable/catalog route.
Rejected Alternatives: Adding a separate generator or prefab family was rejected because the current architect already owns generated hard-surface construction modules. Leaving the route unused was rejected because it produces no actual gameplay content.
Scalability potential: Low devices consume one extra authored static prefab with primitive colliders. Middle/high/ultra can spend offline bevel/LOD detail on the same vertical shaft without changing runtime placement truth.
Hardware Impact: Editor-only content expansion. Player runtime sees serialized `BuildableData`, `BaseModuleTemplate`, static meshes, and primitive colliders; no hot-loop allocation added.

Problem: Generated module metadata was still flat: every bake output became `BuildableFamily.Habitat`, `-10W`, priority 50, and no template-level structural/emergency flags. That makes reactor/airlock content impossible to represent honestly through the existing generated route.
Solution: Extended the existing `ModuleSpec` to carry family, power rating, power priority, structural-anchor, and emergency-airlock flags. `CreateOrUpdateTemplate`, `CreateOrUpdateBuildableData`, and `AttachRuntimeContracts` now write those spec values. Added an airlock and reactor-room spec to the default bake set.
Rejected Alternatives: Creating separate reactor/airlock generators was rejected because it duplicates the architect route. Keeping one global power/family default was rejected because construction UI, persistence, and power systems already read `BuildableData`.
Scalability potential: Low devices still load static serialized modules. Middle/high/ultra get richer authored module roles and can spend offline geometry budget without altering runtime truth.
Hardware Impact: Editor-only metadata/content expansion. Player runtime reads existing serialized fields; no hot-loop lookups or allocations added.

Problem: Compile/test execution remains requested after vertical socket patch.
Solution: Build was withheld because throttle guards reported CPU 93% with active `VBCSCompiler` PID 55708, CPU 57%, then CPU 100% with active `dotnet` PIDs 44152 and 57456. Static checks were used: route scan, old-solid-slab absence scan, spec power/family route scan, forbidden-token sweep, brace balance, and `git diff --check`.
Rejected Alternatives: Launching another `dotnet build` under CPU >50% or with active compiler/runtime processes was rejected by the explicit throttle rule.
Scalability potential: No runtime effect.
Hardware Impact: No new compile artifact for this incremental patch.

Problem: `ModuleArchitect1712` mirrored `GeneratedIndexCapacity` from `GeneratedVertexCapacity`, which undercounts quad-heavy topology because each quad contributes four vertices and six indices.
Solution: Split offline mesh bounds into `MaxSocketFaceQuadCount`, `MaxEdgeBevelQuadCount`, and `MaxCornerBevelTriangleCount`. `GeneratedVertexCapacity` now derives from 4/4/3 vertex terms and `GeneratedIndexCapacity` from 6/6/3 index terms. Existing editor regression coverage now asserts the separated route.
Rejected Alternatives: Letting `List<int>` grow dynamically was rejected because the offline baker must prove its memory envelope. Arbitrary oversized constants were rejected because the topology maxima are exact.
Scalability potential: Low through ultra generated modules can bake different bevel segment counts without runtime geometry mutation. The corrected index bound keeps high-quality three-segment output inside the prewarmed editor envelope.
Hardware Impact: Editor-only allocation-risk reduction. Player runtime consumes serialized meshes; no frame-time claim.

Problem: Compile/test execution remains requested after the index-capacity correction.
Solution: Build was withheld because throttle guard reported CPU 100% and active `dotnet` PIDs 24940 and 41832. Static checks completed: retired-token scan empty, capacity-retired-token scan empty, target orphan-meta scan empty, `TryGetComponent` occurrences are cold prefab/proxy resolution helpers, and `git diff --check` is clean except existing CRLF warnings.
Rejected Alternatives: Starting another `dotnet build` under CPU 100% with active `dotnet` processes was rejected by the explicit throttle rule.
Scalability potential: No runtime effect.
Hardware Impact: No new compile artifact for this incremental patch.
