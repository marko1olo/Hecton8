# Rationale 1609 - COMPOUND_COLLIDER_AND_PHYSICS_OPTIMIZER

Date: 2026-06-01
Status: INTEGRATOR_STATIC_VERIFIED_FAIL_CLOSED_BATCH_ROUTE

## Decision 001 - Editor-Only Pipeline Boundary

Problem: High-poly MeshColliders destroy PhysX narrow phase, but runtime decimation would violate frame budget and zero-GC rules.
Solution: Keep generation/inspection under `Assets/_Project/Editor/Physics/`; runtime consumes static primitive colliders or pre-authored COL_ proxy assets through a data-only baker handoff.
Rejected Alternatives: Runtime mesh simplification and scene-load collider baking; both risk main-thread stalls and hidden allocations.
Scalability potential: Low uses primitives/no colliders; Middle uses limited compound primitives; High uses padded COL_ proxy meshes; Ultra spends saved CPU on visual dressing, not physics truth.
Hardware Impact: i3/MX350 gain is from replacing triangle BVH checks with primitive or 12-triangle proxy collision; exact project-wide savings pending Unity prefab pass.

## Decision 002 - JSON Reports Are Secondary

Problem: Batch prompt requests JSON ledgers, while coordinator instruction rejects unread report dumps as primary proof.
Solution: Main proof remains optimized prefab mutation when Unity is available plus concise status/log records; machine-readable counters are generated only by the tool.
Rejected Alternatives: Generating bulky JSON as primary success artifact.
Scalability potential: Proof focuses on collider count and triangle removal, not document volume.
Hardware Impact: No runtime impact; reduces agent/document noise.

## Decision 003 - No Raw Prefab YAML Mutation

Problem: Prefab collider edits can corrupt YAML if done by string replacement.
Solution: Use Unity Editor APIs from Editor scripts for prefab loading/saving; shell scans remain read-only.
Rejected Alternatives: Direct `.prefab` text patching.
Scalability potential: Safe for low-volume manual use and high-volume batch pass.
Hardware Impact: Editor-only cost; prevents broken prefabs from causing runtime physics failures.

## Decision 004 - Technie Fallback

Problem: User requested Technie, but `Packages/manifest.json` contains no Technie package.
Solution: Implement deterministic in-house compound primitive fitter using submesh covariance axes, sphere/capsule classification, and BoxCollider fallback.
Rejected Alternatives: Adding an undeclared package dependency or pretending Technie exists.
Scalability potential: Low/Middle get Box/Sphere/Capsule primitives; High/Ultra can later swap the fitting backend behind the same engine if Technie is installed.
Hardware Impact: i3/MX350 avoids convex mesh solving for man-made structures; primitive pair checks are the cheapest PhysX path.

## Decision 005 - Root-Space Proxy Bounds

Problem: A proxy mesh built in a child mesh's local space can miss offset or scaled child geometry when attached to the prefab root.
Solution: Build COL_ proxy mesh from every visual MeshFilter transformed through `root.worldToLocalMatrix * filter.localToWorldMatrix`, then add 0.04 m padding.
Rejected Alternatives: Largest-mesh local-space AABB and unpadded visual bounds.
Scalability potential: Low uses a safe coarse 12-triangle blocker; Middle/High/Ultra can spend more on tighter generated proxies later without changing the authoring route.
Hardware Impact: Prevents player fall-through failures while keeping collision at 12 triangles for weak CPUs.

## Decision 006 - Async Bake Handoff Without Private Scheduler

Problem: `Physics.BakeMesh` must not run from a private MonoBehaviour update loop or hidden same-frame `.Complete()`.
Solution: `RuntimePhysicsBaker1609` exposes pure read accessors and an `IJob` bake request; a dispatcher owner must schedule/complete and call `CommitBakedCollider()` in POST_SIMULATION. Burst annotation was rejected because existing project precedent (`VoxelMeshBakeJob`) calls `Physics.BakeMesh` from an unannotated `IJob`, and UnityEngine API calls are not safe to assume Burst-compatible.
Rejected Alternatives: `Update()`, coroutine bake polling, synchronous bake during scene load, or fake Burst decoration on an engine API call.
Scalability potential: Low devices can delay commit and use bootstrap BoxCollider longer; High/Ultra can dispatch more COL_ bakes per frame under a quality/capacity budget.
Hardware Impact: Avoids load spikes on i3/MX350; background bake work becomes controllable by the physics dispatcher.

## Decision 007 - Build Blocked By Contention

Problem: Project law and user instruction prohibit build attempts while compiler processes are active or when static verification is sufficient.
Solution: Performed static syntax/structure scans only. `Get-Process` found active `dotnet` processes during the turn; Unity MCP also returned `no_unity_session`.
Rejected Alternatives: Forcing `dotnet build` to satisfy ritual verification.
Scalability potential: Protects the 20+ agent cluster from compile storms.
Hardware Impact: Prevents avoidable CPU contention on host; no runtime impact.

## Decision 008 - Phase-Gated Bake Commit

Problem: A data-only bake handoff can still be misused if any caller commits a baked MeshCollider during simulation.
Solution: `CommitBakedCollider()` now requires `RuntimePhysicsBakeCommitPhase1609.PostSimulation` or `RuntimePhysicsBakeCommitPhase1609.VisualSync`, records the accepted byte phase, and rejects invalid calls without mutation.
Rejected Alternatives: Trusting comments, `Update()` polling, or synchronous scene-load bake.
Scalability potential: Low devices can hold the bootstrap BoxCollider longer; Middle/High/Ultra can commit more completed bakes per owner phase without changing physics authority.
Hardware Impact: Prevents simulation-phase collider swaps that can cause stalls or transient fall-through; no hot-path allocation added.

## Decision 009 - LOD0 Collision Source Filter

Problem: Compound generation could accidentally consume generated collision meshes, impostors, or lower LODs and create duplicate/off-target colliders.
Solution: `IsPrimaryCollisionVisual()` accepts non-LOD meshes or LOD0 renderers only, and rejects generated names (`COL_`, `CollisionProxy`, `PhysicsProxy`, `Impostor`).
Rejected Alternatives: Fitting every MeshFilter in the prefab hierarchy.
Scalability potential: Low/Middle avoid redundant primitives; High/Ultra keep the same physics truth while visual LODs remain presentation-only.
Hardware Impact: Reduces prefab optimizer work and prevents collider bloat on i3/MX350.

## Decision 010 - Editor Scratch Indices

Problem: `mesh.triangles` and `mesh.GetIndices()` allocate managed arrays during mass prefab analysis.
Solution: Triangle counts use `mesh.GetIndexCount()`; submesh fitting uses `mesh.GetTriangles(s_IndexScratch, subMeshIndex, true)` with preallocated scratch storage.
Rejected Alternatives: Accepting cold Editor GC as harmless during 10k-prefab passes.
Scalability potential: Low-end authoring machines survive large batches; higher machines spend saved editor time on tighter proxy validation.
Hardware Impact: Removes large per-mesh managed copies during optimizer runs; runtime unchanged.

## Decision 011 - No Disk JSON Proof Route

Problem: The current coordinator protocol rejects JSON report dumps as proof, but the 1609 tool still had `WriteReport()` and a `Docs/Reports/*.json` path.
Solution: Removed disk report generation from the Editor engine/window. `ColliderOptimizationReport1609` remains an in-memory counter DTO for UI and tests only.
Rejected Alternatives: Keeping dead report I/O because the original batch text mentioned it.
Scalability potential: Low-end editor nodes avoid pointless disk writes during collider batches; high-end nodes keep the same mutation pipeline and live counters.
Hardware Impact: Removes report-file I/O and string-format work; runtime impact is zero.

## Decision 012 - Proxy Validation Source Discipline

Problem: Encapsulation validation could include non-primary visual sources and produce noise against LOD/impostor/proxy meshes.
Solution: `ValidateProxyEncapsulation()` now uses the same `IsPrimaryCollisionVisual()` gate as generation, so proof checks LOD0 physical silhouette only.
Rejected Alternatives: Validating every MeshFilter in the hierarchy regardless of role.
Scalability potential: Low/Middle avoid false positives from cheap impostors; High/Ultra keep consistent physics truth while presentation LODs scale independently.
Hardware Impact: Reduces editor validation work and prevents unnecessary collider expansion driven by presentation-only meshes.

## Decision 013 - Fallback Proxy Mesh Disposal

Problem: `PrepareProxyBake()` created an unsaved proxy mesh before checking its triangle budget. If the budget failed, fallback to primitives could leave an Editor-only transient `Mesh` alive.
Solution: Proxy budget uses `CountMeshTrianglesNoAlloc(proxy)` and destroys the unsaved proxy with `Object.DestroyImmediate(proxy)` before primitive fallback.
Rejected Alternatives: Relying on Editor GC/native cleanup after batch processing.
Scalability potential: Low-end editor nodes can process large organic batches without accumulating rejected native mesh objects; high-end nodes keep deterministic fallback behavior.
Hardware Impact: Removes avoidable Editor native memory retention; runtime impact is zero.

## Decision 014 - Cold Bake Mesh Identity Cache

Problem: `TryResolveBakeRequest()` is a dispatcher read accessor. Calling `collisionProxyMesh.GetEntityId()` there would make Unity object identity resolution part of the scheduling read path.
Solution: `RuntimePhysicsBaker1609` now resolves the proxy mesh `EntityId` only during cold setup (`OnEnable`, `ConfigureAuthoring`, or explicit `RefreshBakeIdentityCold`) and stores both `EntityId` and a serialized ulong key. `TryResolveBakeRequest()` returns the cached identity and fails closed when the key is zero.
Rejected Alternatives: Resolving mesh identity in every bake request, polling a registry, or adding a private scheduler to hide the lookup.
Scalability potential: Low devices can defer bakes and keep the bootstrap box without hot identity churn; Middle/High/Ultra can scale dispatcher bake capacity from cached immutable requests.
Hardware Impact: Removes Unity object identity calls from the dispatcher read path; exact microsecond gain is small per request but prevents repeated managed/native bridge work under large rock batches.

## Decision 015 - Idempotent Proxy Bake Replacement

Problem: Re-running proxy bake on the same prefab could create a fresh `COL_*.asset` and new bootstrap `BoxCollider` while stale generated artifacts remained referenced by the previous baker.
Solution: `PrepareProxyBake()` now removes previous generated target MeshCollider, bootstrap BoxCollider, and generated proxy mesh asset before creating the replacement. It deletes only assets under `GeneratedAssetRoot/COL_*.asset`, guards component deletion to the loaded prefab root, and removes stale `RuntimePhysicsBaker1609` when falling back to primitives. The async bake request now carries `MeshColliderCookingOptions`, so `Physics.BakeMesh` and the target MeshCollider use the same cooking contract.
Rejected Alternatives: Trusting repeated `GenerateUniqueAssetPath()` runs, leaving stale bootstrap blockers, or baking with default options while the collider uses explicit options.
Scalability potential: Low editor machines can rerun batches without accumulating native mesh assets and extra colliders; Middle/High/Ultra get deterministic replacement passes and stable bake cooking across generated rock proxies.
Hardware Impact: Prevents prefab collider bloat across repeated optimization passes; saved runtime cost is proportional to avoided duplicate bootstrap colliders and stale mesh assets.

## Decision 016 - Continuous GlobalQualityWeight Collider Authoring

Problem: The optimizer still relied primarily on discrete strategies, while HECTON-8 requires continuous quality scaling for weak through ultra hardware.
Solution: Added `ColliderOptimizationSettings1609` with continuous `GlobalQualityWeight`. The scalar controls maximum generated primitive colliders per prefab and proxy padding: low quality emits fewer primitives and larger safety padding; high quality allows tighter compound fitting and smaller padding. The runtime authority route and DTO layout remain unchanged.
Rejected Alternatives: Adding low/high binary modes, changing gameplay collision ownership per tier, or introducing a runtime quality branch inside the bake component.
Scalability potential: Weak devices get coarse, safe blockers with fewer PhysX shapes; middle devices get moderate compounds; high/ultra authoring can spend more collider budget on closer silhouettes while still avoiding heavy MeshColliders.
Hardware Impact: Low-end i3/MX350 avoids shape-count bloat; high-end machines use saved mesh-collider cost for better visual systems without changing gameplay collision truth.

## Decision 017 - Static Guard Tests Without Validator Noise

Problem: The static scheduler guard test contained literal banned tokens, causing Unity's script validator to report irrelevant warnings while still testing the right contract.
Solution: Split the banned tokens through a small `Token()` helper and added `QualitySettingsScaleContinuouslyAndStayInBounds()` with numeric assertions for low/mid/high quality settings.
Rejected Alternatives: Removing the static guard or accepting warning noise as harmless.
Scalability potential: The quality contract is now enforced by executable assertions, so future edits cannot silently collapse the scalar back to binary quality modes.
Hardware Impact: Test-only change; prevents future regressions that would increase PhysX shape count on low-end hardware.

## Decision 018 - Bake Key Fail-Closed Commit Guard

Problem: Phase gating alone does not prove the pending COL_ mesh identity is still the cold-cached mesh identity when a dispatcher asks for a bake or commits the baked collider.
Solution: `TryResolveBakeRequest()` now returns true only when `EntityId.ToULong(meshEntityId)` matches `cachedCollisionProxyMeshKey`; `CommitBakedCollider()` also rejects null target/proxy, zero cached key, and key mismatch before mutating the MeshCollider. Static edit tests assert the guard appears before `targetCollider.sharedMesh = collisionProxyMesh` and that only `PostSimulation`/`VisualSync` are valid commit phases.
Rejected Alternatives: Resolving `GetEntityId()` inside the read accessor, polling `GlobalRegistry`, or trusting serialized object references without a cold identity guard.
Scalability potential: Weak devices can keep the bootstrap primitive blocker if proxy identity is invalid; middle/high/ultra devices can batch more proxy bake requests without introducing hot object lookup or unsafe phase commits.
Hardware Impact: Prevents invalid proxy commit stalls/fall-through defects; per-request cost is one cached ulong comparison, no scene search and no allocation.

## Decision 019 - Hot Method Body Static Guard

Problem: Whole-file string scans prove broad absence, but they do not prove the actual hot/read bodies remain clean if future cold authoring code adds legal Unity lookup calls elsewhere.
Solution: Added edit-test extraction of method bodies for `RuntimePhysicsBakeJob1609.Execute`, `RuntimePhysicsBaker1609.TryResolveBakeRequest`, and `RuntimePhysicsBaker1609.CommitBakedCollider`. Each extracted body is asserted free of `GlobalRegistry`, component lookup, DataVault write-lock tokens, `Monitor`, `lock`, hidden `.Complete()`, `new`, `List<`, and `.ToString(`.
Rejected Alternatives: Trusting only whole-file grep, or moving legal cold setup calls out of the component just to satisfy text scans.
Scalability potential: Future dispatcher integration can be validated at method-body granularity, preserving cold authoring flexibility while keeping runtime scheduling bodies clean across weak/middle/high/ultra tiers.
Hardware Impact: Test-only guard; prevents future edits from adding hot scene search, managed allocation, or lock surface to the bake request/commit path.

## Decision 020 - Flora Purge Root Expansion

Problem: `PurgeFloraColliders()` only targeted `Assets/_Project/Prefabs/Nature/Flora`, while the actual project also stores flora-like prefabs under `WorldRuntime/ProceduralPlaceholders/Flora` and `WorldProceduralProxy` kelp/coral family paths.
Solution: `PurgeFloraColliders()` now scans every prefab under `PrefabRoot` and invokes the destructive `PurgeAll` path only when `IsFloraPath()` matches. The flora classifier now includes kelp, grass, `_Flora_`, `/Flora/`, and small decorative coral families (`family_coral_low`, `family_coral_brittle`, `family_coral_branching`). Read-only shell classification found 317 flora-like prefabs out of 602.
Rejected Alternatives: Purging the entire `WorldProceduralProxy` folder, which would strip rocks/debris/ruins, or keeping the old single-folder target and leaving generated flora with colliders.
Scalability potential: Weak devices avoid thousands of broadphase pairs from generated flora; middle/high/ultra tiers keep visual richness through GPU/renderer systems instead of PhysX shapes.
Hardware Impact: Removes collider work from up to 317 flora-like prefab families when the tool is executed; exact runtime microseconds depend on scene density, but broadphase pair count drops at source.

## Decision 021 - EditorWindow Purge Scope Honesty

Problem: After broadening flora purge to the full prefab tree with a flora-path filter, the EditorWindow still displayed the old `Nature/Flora` folder as the last purge target.
Solution: Added `FloraPurgeScopeLabel` and used it for the purge button's last-folder display. Static tests assert the label exists in engine/window code.
Rejected Alternatives: Leaving stale UI text that understates the destructive scope, or creating a second hardcoded UI-only string.
Scalability potential: Authoring remains safe for large batches because operators see the true filtered scope before/after use.
Hardware Impact: Editor-only clarity change; prevents accidental re-runs caused by misleading scope reporting.

## Decision 022 - Interactable Pickup Exclusion From Flora Purge

Problem: The widened flora classifier caught `Resources/Pickups/PFB_Resource_FiberKelp.prefab` through the `kelp` token, which would delete colliders from an interactable resource pickup rather than scene flora.
Solution: Added `IsNonFloraInteractablePath()` and made `IsFloraPath()` fail closed for `/Resources/Pickups/`, `/Items/`, `/Tools/`, and `Pickup` paths before matching flora tokens. Read-only shell classification dropped from 317 to 316 flora-like prefabs.
Rejected Alternatives: Removing the `kelp` token entirely, which would miss generated kelp scenery, or accepting pickup collider deletion as collateral damage.
Scalability potential: Weak devices still get collider-free scenery flora; interactable pickups retain explicit collision/interaction affordances across all hardware tiers.
Hardware Impact: Preserves gameplay pickup collisions while still eliminating PhysX cost for 316 scenery flora-like prefabs.

## Decision 023 - Physical Rock Exclusion From Flora Purge

Problem: The broad `/Flora/` path token also matched `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows/PorousRock/*_Rock_*.prefab`. Those prefabs are physical environment rocks with MeshCollider surfaces, not decorative kelp/grass/coral scenery. Purging them would create real player fall-through and wall-penetration defects.
Solution: Added `IsNonFloraPhysicalEnvironmentPath()` and made `IsFloraPath()` fail closed for `/PorousRock/`, `_Rock_`, `/Rocks/`, `/Geology/`, `GOTOVYE_PREFABY_KAMNEY`, and `PFB_Geo` before applying flora tokens. Static read-only classification now reports 266 flora-like prefabs out of 602, with 0 of 50 PorousRock prefabs classified as flora.
Rejected Alternatives: Keeping `/Flora/` as an absolute purge rule, or removing the `/Flora/` token entirely and missing generated scenery flora. The first breaks rock collision; the second leaves future decorative flora colliders in PhysX.
Scalability potential: Weak devices still get collider-free scenery flora; middle/high/ultra tiers keep visual richness while rock geometry stays under the proxy/primitive optimization route instead of the no-collider route.
Hardware Impact: Prevents deletion of 50 rock collider candidates under a misleading Flora folder. Runtime gain is correctness, not raw speed: rocks remain collision-safe, while actual scenery flora still contributes 0 PhysX shapes after purge.

## Decision 024 - Public PurgeAll Route Guard

Problem: The EditorWindow exposed `ColliderOptimizationStrategy1609.PurgeAll` through the generic Optimize button, and public `OptimizeFolder(..., PurgeAll, ...)` could be called on `PrefabRoot` or any arbitrary folder. That made a single wrong UI/API call capable of deleting all colliders from world, rock, item, and player-adjacent prefabs.
Solution: `OptimizeFolder` now skips every non-flora path when strategy is `PurgeAll`. The EditorWindow shows a warning for `PurgeAll` and routes generic Optimize clicks to `PurgeFloraColliders()` instead of arbitrary-folder purge.
Rejected Alternatives: Trusting operators to use only the separate Purge Flora button, or removing the enum value and breaking existing static references. The correct fix is a fail-closed public API guard.
Scalability potential: Weak/middle/high/ultra all preserve the same gameplay collision truth; only scenery flora can take the no-collider route.
Hardware Impact: Prevents catastrophic collider deletion. Runtime speed is not the gain; stable collision ownership is.

## Decision 025 - Zero-Fit Compound Fallback Collider

Problem: `GenerateCompoundColliders()` deleted MeshColliders before primitive fitting completed. If the source prefab had no accepted primary visual mesh or all submesh fits failed, the method could save a modified prefab with no replacement collider.
Solution: Before deleting each MeshCollider, the tool expands a root-space AABB from the collider mesh bounds. If generated primitive count is zero, it destroys the temporary generated root and emits a root `BoxCollider` from those cached bounds. This preserves collision safety even when detailed fitting fails.
Rejected Alternatives: Leaving the old MeshCollider in place after partial mutation, or saving a no-collider prefab and relying on QA to find fall-through. The fallback BoxCollider is crude but deterministic and cheap.
Scalability potential: Low devices get one safe primitive; middle/high/ultra can still generate richer compounds when valid visual sources exist. The fallback is a correctness floor, not a quality ceiling.
Hardware Impact: In zero-fit cases, one BoxCollider replaces a MeshCollider BVH while preventing collision holes. Expected low-end gain is the same class as other primitive replacements, with higher safety than deleting collision outright.

## Decision 026 - Non-Finite GlobalQualityWeight Guard

Problem: `ColliderOptimizationSettings1609.FromGlobalQualityWeight()` passed input through `Mathf.Clamp01` without an explicit NaN/Infinity contract. A non-finite quality scalar could poison primitive budget and proxy padding calculations.
Solution: The factory now routes NaN and Infinity to `DefaultGlobalQualityWeight` before clamping. Edit tests assert NaN and PositiveInfinity both resolve to the default quality.
Rejected Alternatives: Trusting callers, or relying on downstream `NormalizeSettings()` only. The public factory is itself a contract boundary and must be finite.
Scalability potential: Weak/middle/high/ultra scaling remains continuous for valid values and stable for invalid inputs; no binary hardware tier branch was introduced.
Hardware Impact: Prevents invalid authoring settings from generating zero/extreme collider budgets or non-finite proxy padding. Runtime impact is zero; prefab safety impact is direct.

## Decision 027 - ASCII-Only Generated Collider Asset Stems

Problem: `SanitizeAssetStem()` used `char.IsLetterOrDigit`, which accepts Unicode letters and can preserve non-ASCII prefab names inside generated `COL_*.asset` paths and child collider GameObject names.
Solution: The sanitizer now accepts only ASCII `A-Z`, `a-z`, `0-9`, `_`, and `-`; every other character becomes `_`, with empty stems falling back to `ColliderProxy`.
Rejected Alternatives: Keeping Unicode asset stems because Unity can often serialize them, or adding a culture-aware transliterator. The first is unstable for automation/tooling paths; the second adds unnecessary authoring complexity.
Scalability potential: Weak/middle/high/ultra build lanes receive deterministic generated asset names regardless of machine locale or prefab source language.
Hardware Impact: Runtime 0 us. Authoring impact is fewer path/asset database edge cases during mass collider optimization on low-end editor nodes.

## Decision 028 - Bounded Editor Scratch Capacity

Problem: The optimizer uses static scratch lists for mesh filters, vertices, and triangle indices. A single huge mesh could force those lists to grow and then hold inflated backing arrays for the rest of a 10k-prefab Editor batch.
Solution: Added explicit scratch capacity constants and `ClearScratch<T>()` helpers that clear each buffer and shrink it back to its intended ceiling after use. The active mesh read still reuses the same buffers; only post-use retention is capped.
Rejected Alternatives: Allocating fresh local lists per prefab, or leaving inflated static buffers alive until domain reload. Fresh lists increase GC churn; retained oversized buffers punish low-end editor nodes.
Scalability potential: Weak editor machines avoid long-lived memory spikes during giant prefab scans; middle/high/ultra machines keep the same deterministic output and can spend memory on useful authoring work instead of stale buffers.
Hardware Impact: Runtime 0 us. Editor memory retention is capped to 256 MeshColliders, 512 Colliders, 512 MeshFilters, 256 Rigidbodies, 65,536 vertices, and 131,072 indices per scratch buffer after each use.

## Decision 029 - Flora Layer Fail-Safe

Problem: The layer matrix audit validates `Flora` self-collision sparsity, but `ResolvePhysicsLayer()` mapped flora-classified paths to `Default`. If a flora collider survived before purge or in a placeholder prefab, it would not benefit from the high-density flora layer route.
Solution: Flora-classified paths now resolve to `Flora` with normal fallback behavior if the layer is absent. Read-only prefab/YAML scan found 8 flora-classified placeholder prefabs with collider tokens, so this is a real fail-safe rather than theoretical cleanup.
Rejected Alternatives: Relying exclusively on destructive purge, or keeping flora on `Default` because most flora should have no colliders. The purge is the target state; the layer route is the safety net.
Scalability potential: Weak/middle/high/ultra all keep the same gameplay truth while accidental flora colliders remain isolated from dense default-layer broadphase behavior.
Hardware Impact: Runtime savings apply only to surviving flora colliders: self-pair broadphase candidates can be rejected by the sparse `Flora` layer matrix instead of entering default collision lanes.

## Decision 030 - Per-Prefab Fail-Closed Batch Continuation

Problem: `OptimizeFolder()` and `PurgeFloraColliders()` called `OptimizePrefabAsset()` directly. A single broken prefab load, asset database exception, or unexpected Unity API failure could abort the whole collider optimization pass.
Solution: Added `TryOptimizePrefabAsset()` around each prefab mutation, `PrefabsFailed` telemetry, null-root handling, and guarded `PrefabUtility.UnloadPrefabContents(root)` so loaded roots are still released while the batch continues to the next prefab.
Rejected Alternatives: Letting one broken prefab terminate the batch, or swallowing failures silently. Termination wastes long Editor passes; silence hides unsafe assets from the integrator.
Scalability potential: Weak editor machines can process large folders without restarting from prefab zero after one asset failure; high-end batch nodes keep throughput while surfacing exact failed assets.
Hardware Impact: Runtime 0 us. Editor throughput gain is avoiding total batch restart after a single prefab failure.
