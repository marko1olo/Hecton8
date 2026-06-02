# Rationale 1606

Status: CODE COMPLETE / ASSET BAKE DEFERRED BY CPU/UNITY COMPILER THROTTLE

## Decision 01: Prompt Parser

Problem: The active batch prompt tag for 1606 includes `role` and `chat_name` attributes, so a strict literal `<AGENT_PROMPT id="1606">` extraction fails.
Solution: Use an attribute-aware CLI regex over the full `CURRENT_BATCH.md` and count `Task NN:` markers inside the extracted block.
Rejected Alternatives: Reading neighboring prompt blocks; trusting chat text; using the strict literal parser.
Scalability potential: no runtime impact. Prevents wrong-domain work in a 20+ agent batch.
Hardware Impact: 0 us runtime; avoids wasted build/editor CPU on wrong task.

## Decision 02: Offline-Only Boundary

Problem: The assignment demands heavy voxel, erosion, AO, and collision generation, but runtime execution would violate frame-time and GC mandates.
Solution: Keep all new generator code under `Assets/_Project/Editor/Generators/Geology/`; runtime consumes only baked `.asset` meshes and `.prefab` files.
Rejected Alternatives: Runtime mesh generation; scene-time `MonoBehaviour` generators; hot-path physics mesh baking.
Scalability potential: low tier receives cheap baked silhouettes and COL proxies; middle/high/ultra can receive denser visual LODs and richer vertex masks without changing runtime truth.
Hardware Impact: i3/MX350 runtime cost target is 0 us/frame for generation. Editor generation cost is acceptable because it is offline.

## Decision 03: Collider First

Problem: Visual rocks can exceed thousands of triangles, but PhysX mesh contact against those assets would destroy CPU budget.
Solution: Every generated visual mesh must have a separate `COL_` mesh under 200 triangles and the prefab MeshCollider must bind only to that `COL_` mesh.
Rejected Alternatives: Assigning LOD0 to MeshCollider; relying on Unity auto-convex cleanup; leaving colliders to scatter-time tooling.
Scalability potential: low uses coarse collision; middle/high/ultra only increase visual detail and material masks, never physics truth complexity.
Hardware Impact: expected runtime collision savings versus LOD0 MeshCollider: tens to hundreds of microseconds per contact-heavy frame on i3/MX350, pending Unity profiler proof.

## Decision 04: Extend Existing Forge

Problem: The project already owns an offline geology forge with SDF extraction, Burst jobs, AO, LODs, manifest writing, and scanners. A second generator would create two geometry truths.
Solution: Patch `GeologyForge` in place and add only a thin `Assets/_Project/Editor/Generators/Geology` entrypoint for the requested studio path.
Rejected Alternatives: New standalone generator; direct runtime generator; copying Marching Cubes code into a second editor assembly.
Scalability potential: low, middle, high, and ultra tiers stay on one baked asset route; only quality weights and asset budgets scale.
Hardware Impact: no runtime frame cost. Editor work avoids duplicated import/mesh bake CPU and prevents repeated PhysX cooking against incompatible meshes.

## Decision 05: Hydraulic Erosion as SDF Fake

Problem: Full droplet hydraulic erosion would be slow, stateful, and unnecessary for offline abyssal prop silhouettes.
Solution: Add deterministic SDF displacement terms for runoff cuts, sediment fans, basalt facets, and thermal-vent throat carving inside the existing Burst density job.
Rejected Alternatives: Particle erosion; cellular water simulation; post-mesh vertex relaxation that would require extra adjacency allocations.
Scalability potential: low uses broad fake cuts; middle/high/ultra get richer AO rays, octaves, Voronoi/facet detail via continuous `GlobalQualityWeight`.
Hardware Impact: runtime 0 us/frame. Editor cost is arithmetic inside the existing density job, avoiding a separate erosion pass and native buffer.

## Decision 06: Vertex Color Contract

Problem: Previous packing stored AO brightness in red, zero in green, and LOD mask in blue, which did not support sediment/AO material masking.
Solution: Red = LOD mask, Green = upward sediment/silt mask, Blue = AO darkness.
Rejected Alternatives: Extra UV channel; material property blocks per prefab; texture bake per mesh.
Scalability potential: weak devices get one vertex-color material path; high/ultra shaders can spend the same baked masks on extra wetness, silt, and shadow response.
Hardware Impact: expected i3/MX350 gain versus texture-mask sampling is one less texture lookup per pixel on the rock material path, exact shader cost pending material profiling.

## Decision 07: Static Audits Before Heavy Build

Problem: The user explicitly forbids `dotnet build` after small edits, and multiple agents may already be compiling.
Solution: Add source-level EditMode tests and run static checks first; Unity compilation/generation is allowed only if editor state is available and no compiler wall is detected.
Rejected Alternatives: launching project-wide build; claiming success without source and asset checks.
Scalability potential: no runtime impact; reduces shared-cluster CPU contention.
Hardware Impact: avoids multi-second CPU saturation from an unnecessary build on host hardware.

## Decision 08: Burst Import Recovery

Problem: Unity rejected `CompileSynchronously = true` Burst job compilation during script/import state and later exposed a pending writer disposal fault on normal bucket storage.
Solution: Switch GeologyForge editor jobs to asynchronous Burst compilation and add an explicit editor-phase fence after `BuildNormalBucketJob` before smooth-normal reads.
Rejected Alternatives: Repeated menu execution; project-wide dotnet build; disabling Burst; ignoring the NativeParallelMultiHashMap disposal error.
Scalability potential: all tiers keep the same baked geometry route. The extra editor fence is not runtime cost and buys deterministic asset generation under Unity import pressure.
Hardware Impact: runtime 0 us/frame. Editor bake may spend a small extra sync point, cheaper than failed imports and repeated Burst compile attempts on i3/MX350-class hosts.

## Decision 09: Combined LOD Collision Bounds

Problem: The `COL_` proxy used LOD0 bounds only. LOD1/LOD2 decimation snap can create slightly different extents, leaving a visual child outside the convex proxy.
Solution: Add `CalculateCombinedVisualBounds(lods)` and use it for manifest bounds and collision proxy generation.
Rejected Alternatives: PhysX cooking on LOD0; one proxy per LOD; trusting decimation to preserve exact LOD0 bounds.
Scalability potential: low tier keeps the same 12-triangle collider; middle, high, and ultra can increase visual LOD budgets without changing physics truth.
Hardware Impact: runtime physics remains 12 triangles. Extra editor bounds encapsulation is three mesh bounds reads, below measurable frame cost.

## Decision 10: APEX Static Verification

Problem: The user required dependency, phase, lock, and compile-throttle proof without spamming a project build.
Solution: Run focused static scans over 1606 Forge and adjacent geology/voxel code for hot lookups, phase hooks, DataVault locks, and managed allocation markers; confirm braces and collision markers after patch.
Rejected Alternatives: project-wide `dotnet build`; broad unrelated refactors in other agents' compile walls; chat-only assertions.
Scalability potential: preserves cold dependency routing and keeps runtime geology consumption as baked data across weak, middle, high, and ultra devices.
Hardware Impact: saved a full build while CPU reported 100% and Unity VBCSCompiler was active. Runtime impact of verification code is 0 us/frame.

## Decision 11: COL-Aware Self Audit

Problem: After adding `COL_` meshes, the old layout audit would count physics proxy meshes as unmanifested visual meshes and run the 32-byte visual vertex validator against simple collider geometry.
Solution: Split audit paths: visual LOD meshes stay manifest/vertex-layout checked; `COL_` meshes get physics proxy checks; prefabs get MeshCollider/LODGroup/separate-visual checks.
Rejected Alternatives: adding `COL_` meshes to the visual manifest; ignoring false audit failures; validating collider meshes as visual GPU streams.
Scalability potential: low tier gets strict cheap collider guarantees; middle/high/ultra can increase visual LODs without changing physics proxy validation.
Hardware Impact: runtime 0 us/frame. Editor audit avoids wasting time chasing false failures after every bake.

## Decision 12: Collision/Prefab Rollback

Problem: Visual LOD asset save had rollback, but collision mesh and prefab save could leave partial artifacts if prefab serialization failed after `COL_` creation.
Solution: Back up existing `COL_` and prefab assets, then restore or delete them in `TryCleanupFailedCollisionAndPrefabSave` on failure.
Rejected Alternatives: relying on Unity AssetDatabase overwrite success; cleaning only visual LODs; making prefab save non-transactional.
Scalability potential: all quality tiers keep one coherent visual/physics asset set. Failed high/ultra bakes cannot poison low-tier proxy assets.
Hardware Impact: runtime 0 us/frame. Editor rollback cost is paid only on asset save failure.

## Decision 13: Stop At Static Proof Under Active Compiler

Problem: Final Unity validation retry disconnected while CPU was 60% and Unity `dotnet.exe` was already active, which is the exact environment where project-wide compile, Test Runner, or bake attempts would waste cluster CPU and risk orphaned editor work.
Solution: Freeze verification at source-level proof: scoped diff-check, brace balance, hot lookup scan, DataVault lock scan, and prior successful Unity MCP `validate_script` results.
Rejected Alternatives: forcing `dotnet build`; repeatedly retrying Unity MCP through a busy compiler; editing unrelated 1601/Structures compile walls outside domain.
Scalability potential: no runtime impact. Keeps the geology pipeline ready for weak, middle, high, and ultra asset tiers without corrupting shared editor state.
Hardware Impact: avoided a full project build and repeated Unity compiler retries on a host already above the allowed CPU threshold.

## Decision 14: Collider Bounds Proof Inside Self Audit

Problem: Prefab self-audit proved `COL_` naming and triangle budget but did not prove that the collider was convex or that its bounds enclosed the visual LOD hierarchy.
Solution: Add prefab audit checks for `collider.convex` and `BoundsContains(collider.sharedMesh.bounds, combinedVisualBounds)` using root-local transformed mesh bounds.
Rejected Alternatives: assuming the generator's 12-triangle AABB is always wired correctly; waiting for physical playtest collision to find undercoverage.
Scalability potential: low tier gets the same cheap 12-triangle proxy, and middle/high/ultra can raise visual budgets without changing or weakening physics truth.
Hardware Impact: runtime 0 us/frame. Audit-time bounds math is editor-only and prevents high-poly or under-covering collider regressions from reaching runtime.

## Decision 15: Editor Tool Fail-Closed Profile Fallback

Problem: `GeologyForgeWindow.SelectProfile` dereferenced profile index 0 even when CSV loading produced zero profiles.
Solution: Seed the window with 1606 validation profiles when CSV is empty and keep a defensive empty-list branch in `SelectProfile`.
Rejected Alternatives: letting the editor window throw; requiring a designer to manually fix CSV before the tool opens.
Scalability potential: no runtime impact. Keeps the offline asset path available across weak, middle, high, and ultra target bakes.
Hardware Impact: runtime 0 us/frame. Prevents wasted editor restart/domain reload time after an empty or broken CSV.

## Decision 16: Scheduled Preview Job

Problem: The SceneView preview used direct `.Run(count)` for the density job, which is acceptable only because the preview is small but still weakens the architectural rule that density evaluation belongs to scheduled jobs.
Solution: Switch preview density evaluation to `Schedule(count, 64)` with an explicit editor preview completion fence.
Rejected Alternatives: keeping direct `Run`; adding a separate preview-only density path.
Scalability potential: preview and bake share the same job route, while quality still scales continuously through `GlobalQualityWeight`.
Hardware Impact: runtime 0 us/frame. Editor preview can use worker scheduling instead of forcing all density work through a synchronous direct run.

## Decision 17: Explicit PhysX Collision Cook

Problem: The generated `COL_` mesh was assigned to a prefab `MeshCollider`, but the pipeline did not explicitly pre-cook it through `Physics.BakeMesh` before runtime use.
Solution: Bake the saved `COL_` mesh with `Physics.BakeMesh(collisionMesh.GetEntityId(), true, CollisionCookingOptions)` and stamp the same cooking flags onto the prefab collider before assigning `sharedMesh`.
Rejected Alternatives: relying on first runtime collider assignment to cook; baking the visual LOD mesh; using non-convex cooking for a 12-triangle proxy.
Scalability potential: low tier gets pre-cooked 12-triangle collision; middle/high/ultra can raise visual richness without increasing PhysX cooking pressure.
Hardware Impact: shifts collider cooking cost to editor bake. Runtime target remains 0 us/frame for generation and no first-touch MeshCollider cook stall.

## Decision 18: Static Occlusion Gate

Problem: Generated geology prefabs marked every visual LOD child as `OccluderStatic`, even when the rock could be a small scatter prop that should not enter occlusion-bake blocker sets.
Solution: Add a 2 m3 combined-visual-bounds volume gate. All renderers stay `BatchingStatic|OccludeeStatic`; only sufficiently large rocks receive `OccluderStatic`. Self-audit rejects tiny occluders and missing renderer static flags.
Rejected Alternatives: leaving every renderer as an occluder; disabling all static flags; adding per-profile binary quality switches.
Scalability potential: low tier avoids wasted occlusion bake data from pebbles; middle/high/ultra keep large basalt pillars and vent spires as occluders without changing physics or mesh identity.
Hardware Impact: runtime mesh path remains 0 us/frame. Expected low-end gain is reduced baked occlusion set size and fewer bad occlusion cells from tiny scatter props; exact bake/runtime visibility impact requires Unity occlusion profiler pass after compile wall clears.

## Decision 19: Static Renderer Contract

Problem: Generated LOD renderers relied on Unity defaults for shadow casting, shadow receiving, probe usage, and motion vectors. Static geology should not emit object motion vectors or drift per prefab.
Solution: Configure every generated visual renderer with explicit shadow/probe policy and `MotionVectorGenerationMode.ForceNoMotion`, then make self-audit reject any generated prefab that drifts from that contract.
Rejected Alternatives: manual prefab cleanup; renderer defaults; disabling all shadows/probes and flattening abyssal rocks visually.
Scalability potential: low tier avoids unnecessary static-object motion-vector work while keeping predictable lighting. Middle/high/ultra keep richer probe/shadow response without changing mesh or physics identity.
Hardware Impact: runtime generation remains 0 us/frame. ForceNoMotion removes static-rock object-motion-vector participation; exact GPU gain requires RenderDoc/URP profiler after Unity session returns.

## Decision 20: Test Bounds In Prefab Root Space

Problem: The generated-prefab EditMode test compared each visual mesh's local `mesh.bounds` directly against the root collider mesh bounds. Self-audit already did the correct root-local transform, but the test could miss undercoverage if a future LOD child gets a local offset, rotation, or scale.
Solution: Mirror self-audit math in the test: transform all eight corners through `CalculateLocalToRootMatrix`, encapsulate in root-local space, and compare that combined visual bounds against the `COL_` mesh bounds. Skip collider mesh filters during renderer/static assertions.
Rejected Alternatives: keeping the weaker child-local comparison; instantiating prefabs into a scene just to use world bounds; broad prefab pipeline refactor.
Scalability potential: low, middle, high, and ultra bakes can move or scale visual LOD children without invalidating the physical proof route. Physics remains one cheap `COL_` proxy.
Hardware Impact: runtime remains 0 us/frame. Test/audit math is editor-only; expected low-end gain is avoided collision-regression debugging and no accidental high-poly collider fallback.

## Decision 21: Pin Continuous Quality In Seed DTO Tests

Problem: `GeologySeedDTO` layout and continuous `GlobalQualityWeight` behavior were enforced in source, but the 1606 static test suite did not pin those contracts directly. A future edit could shift offsets or turn quality into a binary gate while still leaving broad generator tests green.
Solution: Add `SeedDtoPinsQualityAsContinuousArm64Layout` to assert the 64-byte explicit DTO, `GlobalQualityWeight` offset 56, `ProfileHash` offset 60, validator offset checks, and continuous saturate/smoothstep quality consumption.
Rejected Alternatives: running a full project build for layout confidence; adding runtime reflection in hot code; accepting source-only validator proof without test markers.
Scalability potential: weak, middle, high, and ultra tiers keep one deterministic seed DTO while quality scales cadence/fidelity continuously instead of splitting data identity.
Hardware Impact: runtime 0 us/frame. Editor/source test cost is static file reads only; avoids ARM64 layout regressions that would cost far more in bake/debug time.

## Decision 22: Renderer Audit Applies Only To Visual Mesh Filters

Problem: The generated-prefab test skipped filters whose mesh is the collider mesh, but self-audit still applied static renderer/shadow/probe checks to every `MeshFilter`. A future hidden `COL_` mesh filter would be treated as a presentation renderer failure.
Solution: Pass `collider.sharedMesh` into `ValidateOccluderStaticGate` and skip filters that reference that mesh before visual renderer validation.
Rejected Alternatives: requiring a MeshRenderer on collision-only helpers; deleting the test skip; merging physics and presentation contracts.
Scalability potential: all tiers can keep or add collision-only helpers without changing visual LOD proof. Visual filters still carry the full abyssal lighting/static contract.
Hardware Impact: runtime 0 us/frame. Editor audit avoids false failures and prevents pressure to put renderers on collision-only objects.

## Decision 23: Make APEX Hot-Path Proof Executable

Problem: Hot lookup and DataVault lock proof was manual `rg` output. That is not enough for future patches because the next edit could add a cold lookup inside an `Execute` job and still leave the static marker tests green.
Solution: Add an EditMode source scanner that extracts hot method bodies by name and rejects `GlobalRegistry.Get`, component lookups, scene/resource searches, and DataVault write-lock tokens inside those bodies across the canonical forge folder and the 1606 menu-entry assembly folder.
Rejected Alternatives: project-wide compiler/test run under active Unity `dotnet.exe`; broad Roslyn dependency; chat-only proof.
Scalability potential: protects the offline geology/topography job layer across weak, middle, high, and ultra bake paths. Hot jobs stay pure data transforms.
Hardware Impact: runtime 0 us/frame. Editor test cost is bounded source-file reads and string scans; prevents hidden per-voxel/component lookup regressions that would be catastrophic on i3/MX350-class hosts.

## Decision 24: Prove 1606 Has No DataVault Write Locks At All

Problem: Hot-method scanning prevents DataVault lock use in hot methods, but lock flattening for this domain is stronger: 1606 geology generation should not acquire DataVault write locks anywhere because it is offline AssetDatabase generation.
Solution: Add `GeologyForgeDoesNotAcquireDataVaultWriteLocks`, scanning forge and 1606 entry assembly sources for `GlobalDataVault`, `DataVault`, `AcquireWrite`, `WriteLock`, and `EnterWrite`.
Rejected Alternatives: only documenting absence; allowing cold DataVault writes in editor generation; adding try/finally lock wrappers around a lock route the domain does not need.
Scalability potential: keeps baked geology generation independent from cross-domain native ownership. Weak through ultra tiers consume assets, not live global locks.
Hardware Impact: runtime 0 us/frame. Removes any risk of editor bake code introducing lock contention or deadlock vectors into runtime/global systems.

## Decision 25: Enforce Zero-GC Hot Method Tokens

Problem: The APEX scanner rejected cold lookup and lock tokens, but zero-GC hot-body proof still relied on a manual managed-allocation scan. Future `Execute` bodies could introduce `List`, LINQ, `ToArray`, or string formatting and bypass the test.
Solution: Extend the hot-method banned token set with managed allocation/LINQ/reflection/marshal markers and reject `.Execute(...)` method calls as declarations so body extraction does not capture unrelated blocks.
Rejected Alternatives: one-time manual `rg`; broad Roslyn parser dependency; banning all `new` tokens, which would falsely reject Burst value-type construction.
Scalability potential: keeps voxel/topography jobs allocation-free across weak, middle, high, and ultra bake paths while still allowing value-type math construction.
Hardware Impact: runtime 0 us/frame for the test. Prevents hidden GC allocations in hot loops that would cause stalls on low-end CPUs.

## Decision 26: Sanitize Source Before APEX Body Extraction

Problem: The executable hot-method scanner counted braces and banned tokens inside comments, regular strings, verbatim strings, raw strings, and char literals. That could either false-fail harmless proof text or false-capture a wrong method body when a comment/string contained `{` or `}`.
Solution: Add `RemoveCommentsAndStringLiterals` to blank non-code text while preserving length and line breaks, then run hot-method extraction and DataVault token scans on the sanitized source. Add a regression test containing banned tokens and braces inside non-code text.
Rejected Alternatives: broad Roslyn dependency; `dotnet build`; accepting manual scans; deleting proof text from future comments to appease a brittle scanner.
Scalability potential: weak through ultra tiers keep the same zero-hot-lookup, zero-lock generator proof without forcing project builds while other agents compile.
Hardware Impact: runtime 0 us/frame. Editor test cost is bounded linear source scanning. It avoided a build while CPU was low but Unity `dotnet.exe` was already active.

## Decision 27: Make Editor-Only Boundary Executable

Problem: The assignment's strongest safety rule is that geology generation must be physically incapable of running in Play Mode or a player build. Folder placement was correct, but the proof was only architectural convention.
Solution: Add `GeologyGenerationSourcesRemainEditorOnly` to assert the source folders and every nested C# file remain under `/Editor/`, and reject runtime entry tokens in Forge/1606 sources.
Rejected Alternatives: trusting namespace/folder memory; adding runtime guards around code that should never compile into runtime assemblies; broad project scan outside 1606 ownership.
Scalability potential: weak, middle, high, and ultra targets all consume baked meshes and prefabs only. Generation cost stays editor-only and never enters frame budget.
Hardware Impact: runtime 0 us/frame. Prevents accidental Play Mode/bootstrap generator entry points that could stall low-end i3/MX350 hardware.

## Decision 28: Reject String Interpolation In Hot Bodies

Problem: Sanitizing whole string literals fixed false positives, but it could also hide `$"{...}"` interpolation expressions. Interpolation allocates managed strings and can contain real code inside braces, so hiding it weakens zero-GC and cold-lookup proof.
Solution: Recognize interpolated regular, verbatim, and raw strings before normal string stripping, blank the literal text, stamp a `$I` allocation marker, and make hot-method scanning fail on that marker.
Rejected Alternatives: leaving interpolation hidden; scanning raw text and accepting false positives in normal strings/comments; adding Roslyn/build dependency.
Scalability potential: weak, middle, high, and ultra bake paths keep hot jobs/data transforms free of managed string allocation.
Hardware Impact: runtime 0 us/frame for the test. Prevents future hot-loop string allocations that would create GC stalls on low-end CPU targets.

## Decision 29: Clamp Absurd Geology Profile Ranges

Problem: CSV validation required positive finite values, but a positive finite radius, height scale, frequency, or amplitude can still be absurdly large and produce useless bounds, unstable preview scale, or overflow pressure before the main generator sanitizer matters.
Solution: Add explicit maximum constants and clamp the four bounding/noise lanes in `SanitizeProfile`. Route editor window field output through `SanitizeForEditor` before preview or selected bake.
Rejected Alternatives: trusting CSV authors; adding runtime guards; clamping only in Burst jobs after editor UI already consumed bad values.
Scalability potential: weak devices avoid accidental giant collision/preview artifacts; middle/high/ultra can still use richer quality inside bounded prop dimensions.
Hardware Impact: runtime 0 us/frame. Prevents editor-side giant SDF bounds from wasting CPU and producing unusable PhysX proxies.

## Decision 30: Contain Broken CSV Before Bake State Starts

Problem: `ReloadProfiles` only fell back when profile count was zero after successful load. If an existing CSV was malformed or empty, `GeologyProfileCsv.LoadProfiles` threw before fallback, breaking the editor window. The CSV bake menu also threw instead of rejecting the request.
Solution: Add `TryLoadCsvProfiles`: clear the list, log a concise warning, return false on load exceptions. Menu bake rejects immediately; editor window uses 1606 validation profiles as fallback.
Rejected Alternatives: letting exceptions propagate through UI; starting async bake with no profiles; silently using stale profile data.
Scalability potential: stable artist tooling across weak and strong workstations. Bad data cannot enter bake state or poison deterministic outputs.
Hardware Impact: runtime 0 us/frame. Saves editor restart/domain reload time after malformed CSV edits.

## Decision 31: Sanitize Direct Preview Calls

Problem: The editor window routed field output through `SanitizeForEditor`, but `GeologyForgePreview.Build` is a public static editor method and could be called directly with an unsafe profile, bypassing the new bounds clamps before preview extent/job setup.
Solution: Sanitize the profile at the first line of `GeologyForgePreview.Build` and add a static audit that checks the sanitizer marker appears inside the preview method body.
Rejected Alternatives: trusting all future callers to use `ResolveProfileFromFields`; making preview private and risking editor integration churn; duplicating clamp math inside the preview code.
Scalability potential: weak devices avoid accidental giant preview SDF bounds; middle/high/ultra still use bounded `GlobalQualityWeight` and profile richness without changing prop identity.
Hardware Impact: runtime 0 us/frame. Editor preview avoids unbounded extent and density setup from direct caller mistakes; avoided cost depends on bad caller input.

## Decision 32: Store Only Sanitized Async Bake Profiles

Problem: `BakeProfilesAsync` copied caller profiles into `_asyncProfiles` before sanitation. Later bake ticks sanitized again, but unsafe radius/frequency/variation values still lived in async state and could drift from progress/counting assumptions if sanitation rules change.
Solution: Sanitize each profile before adding it to the copied async list. CountTotalBakes, result capacity, progress, and tick execution now share the same sanitized DTO sequence.
Rejected Alternatives: relying on tick-time sanitation only; sanitizing only UI inputs; adding a second async-state validation pass.
Scalability potential: weak through ultra bake requests use the same bounded queue state, so invalid artist/API caller input cannot expand editor work beyond designed limits.
Hardware Impact: runtime 0 us/frame. Editor queue allocation and progress accounting no longer see unbounded caller profile data.

## Decision 33: Sanitize Selected Profiles Before UI Projection

Problem: `SelectProfile` projected the raw selected DTO from CSV/fallback storage into UI controls. Later preview/bake routes sanitized again, but the editor state could display unsafe values and leave `_profiles` holding unbounded data after selection.
Solution: Sanitize the selected DTO immediately, write it back into `_profiles[_selectedProfileIndex]`, then project only bounded values into the UI controls.
Rejected Alternatives: trusting later preview/bake sanitation; clamping each slider separately; mutating CSV load output without a single generator-owned sanitizer route.
Scalability potential: weak workstations avoid giant preview/control states from bad CSV data; middle, high, and ultra authoring still use the same bounded profile identity and continuous quality scaling.
Hardware Impact: runtime 0 us/frame. Editor impact is a small struct sanitation pass per selection, preventing larger bad-input preview/bake work.

## Decision 34: Sanitize Full Profile Storage At Load Time

Problem: After CSV/fallback load, `_profiles` could still contain raw unbounded rows until each row was selected or sent through async bake sanitation. `BakeAll` copied that storage directly, leaving two defensive gates downstream but a dirty editor source of truth.
Solution: Add `SanitizeProfilesInPlace` and call it in `ReloadProfiles` before dropdown name projection, selection, preview, or BakeAll can consume `_profiles`.
Rejected Alternatives: relying on `SelectProfile`; relying on `BakeProfilesAsync`; sanitizing only request lists while keeping dirty editor state alive.
Scalability potential: weak machines avoid bad CSV rows inflating preview/bake state; middle, high, and ultra authoring keep one bounded profile storage route with continuous quality scaling.
Hardware Impact: runtime 0 us/frame. Editor cost is one struct sanitation loop per profile reload, bounded by profile count and cheaper than one bad preview/bake setup.
