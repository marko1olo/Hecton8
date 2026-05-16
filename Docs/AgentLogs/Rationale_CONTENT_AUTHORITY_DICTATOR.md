# CONTENT_AUTHORITY_DICTATOR Rationale

## Mandate Selection
Problem: Asset pipeline work crosses runtime streaming, build validation, content tiers, and Babel text lookup.
Solution: Use the eight mandates recorded in Status_CONTENT_AUTHORITY_DICTATOR.md as governing constraints before code.
Rejected Alternatives: Narrow reading of only Addressables docs was rejected because tasks include save topology, VRAM, visibility, and localization.
Scalability potential: Low uses tiny proxies, strict bundle denial, and early eviction. Middle keeps deterministic LOD. High and Ultra spend saved cycles on dense visible content and overkill groups.
Hardware Impact: Expected i3/MX350 gain is avoiding missing asset stalls, reducing redundant bundle loads, and cutting small-collider/LOD/shadow overhead before runtime.

## Initial Architecture Decision
Problem: The required domain folder does not exist, but optimization/runtime services already exist in Hecton8.Core.
Solution: Add a CORE/ASSETS content authority layer under Assets/_Project/Scripts/Core/Content and bind to existing GlobalRegistry, AssetLifecycleGovernor, AssetLoadDispatcher, VRAMMonitor, and typed SignalBus lanes.
Rejected Alternatives: Moving or rewriting Optimization services was rejected as cross-domain churn and collision risk with 20+ parallel agents.
Scalability potential: Low and Quest paths deny Overkill bundles before download; High and Ultra allow richer groups after validation.
Hardware Impact: Keeps main thread load dispatch under the existing 2 ms window and shifts content failures to editor/build gates.

## Runtime Content Spine
Problem: Binary hashes had no authored Unity asset bridge in CORE/ASSETS.
Solution: Added ContentAssetHashMap with FNV1a-32, sorted binary lookup, Addressables references, mesh/prefab bindings, estimated VRAM, tier, and dependency hashes.
Rejected Alternatives: Runtime string lookup and scene search were rejected because they add allocations and hide missing asset ownership.
Scalability potential: Low reads the same hash table but denies Overkill downloads; Ultra can bind higher-tier entries without changing save data.
Hardware Impact: Hash lookup is O(log n) and avoids string path resolution during gameplay; estimate under 10 us for typical registry sizes.

## Proxy and Batch Decisions
Problem: Heavy SDF and thousands of static wreck/debris objects can burn CPU before visibility or batching is proven.
Solution: Added VisibilityProxyBase AABB frustum gating and ObjectBatchBase chunk payloads for BRG-compatible renderers.
Rejected Alternatives: Per-object SDF calls and GameObject-driven debris were rejected as high-overhead and non-deterministic under streaming.
Scalability potential: Low exits before SDF; High/Ultra spend saved cycles on denser visible instances.
Hardware Impact: Expected i3/MX350 saving is 50-200 us per avoided SDF batch and large draw-submission reductions once BRG payloads are populated.

## Editor Build Gates
Problem: Missing Addressables groups, economy hashes without meshes, and cyclic bundle references would ship as runtime stalls or pink/missing assets.
Solution: Added Addressables group validation, economy JSON hash-to-3D-mesh build failure, tier group validation, and DFS cyclic dependency detection.
Rejected Alternatives: Runtime warnings were rejected because the player build must fail before content ships.
Scalability potential: Low/Quest deny Overkill before runtime; High/Ultra can include Overkill only when isolated in the correct group.
Hardware Impact: Moves asset failures to editor time; prevents runtime RAM spikes from accidental oversized/missing content.

## Streaming Runtime Guards
Problem: Async assets can be invisible during slow loads and duplicate bundle loads inflate VRAM.
Solution: Added ContentAuthorityRuntime with pooled hologram proxies after 100 ms, fixed-capacity bundle reference counting, VFX Addressables prewarm, VRAM ceiling intercept, and a 300-frame black-box ring.
Rejected Alternatives: Instantiating fallback meshes on timeout and duplicate Addressables calls were rejected.
Scalability potential: Low uses translucent proxy and forced eviction; Ultra allows richer bundles while telemetry preserves failure evidence.
Hardware Impact: Ref counting blocks duplicate texture residency; estimated savings are MB-scale VRAM avoidance and <100 us/frame service overhead.

## AUP Shift Memory Window
Problem: The prompt requires orphan memory cleanup during spatial jumps under high stress.
Solution: Added a gated AupShiftSignal hook that triggers the Unity unused-asset async cleanup only when SystemStress01 > 0.8 and no cleanup is already running.
Rejected Alternatives: Calling cleanup every pressure tick was rejected because it would stall unpredictably; broad GC was rejected.
Scalability potential: Low benefits from jump-window cleanup; High/Ultra should rarely cross the stress threshold.
Hardware Impact: Expected gain is reclaimed orphaned memory during an already-disruptive transition, not a normal-frame optimization.

## Asset Postprocessing
Problem: Imported flora mesh colliders, unmanaged LODs, tiny shadow casters, and dense box collider fields leak runtime cost into content.
Solution: Added postprocessor rules for flora MeshCollider stripping, environment LODGroup automation, small-shadow purge, and a convex proxy baker.
Rejected Alternatives: Runtime stripping was rejected because prefab import time is the correct failure boundary.
Scalability potential: Low gets collider/shadow cost removed; High/Ultra keeps visual richness while physics stays simplified.
Hardware Impact: Expected savings vary by asset density; 50 box colliders collapsed to one convex proxy can cut PhysX broadphase work by hundreds of us in dense bases.

## Save and Lore Routing
Problem: Save slots, MacroDB payloads, seed-derived data, and Babel binary text had no single content topology declaration.
Solution: Added ContentSaveSlotTopology, CONTENT_SAVE_SLOT_TOPOLOGY.md, and ContentLoreBinaryProvider for hash-based memory-mapped Babel_Dictionary.h8bin reads.
Rejected Alternatives: Saving prefab paths or loading lore as TextAssets was rejected as non-deterministic and allocation-prone.
Scalability potential: Low and Ultra share the same stable hashes; only resolved asset tier differs.
Hardware Impact: Avoids duplicating derived data in .sav and lets UI request lore blocks without string-key lookups.

## Compile Wall
Problem: dotnet build Hecton8.Core.csproj fails in dirty files outside CORE/ASSETS.
Solution: Logged three compile attempts and isolated that no errors reference the new Content files. Strike 1 failed in UI/Core/VFX/World/Physics dirty files. Strike 2 failed on missing `Assets/_Project/Scripts/Physics/Tethers/Contracts/TetherSignalContracts.cs` referenced by `Directory.Build.targets`. Strike 3 failed in `GameBootstrapper.cs` because `Hecton8.Core.Bucketing` and `ModuloSimulationBucketer` are unavailable.
Rejected Alternatives: Reverting or rewriting other agents' dirty files was rejected under parallel-agent ownership rules.
Scalability potential: Not applicable.
Hardware Impact: Not applicable until compile wall is cleared.

## Phase 2 Compile Wall
Problem: Phase-2 `dotnet build Hecton8.Core.csproj` attempts still fail outside CORE/ASSETS after content hardening.
Solution: Logged attempts 4-7. Latest blocker is duplicate field definitions in `SubmarineFluidDynamics`; earlier phase-2 blockers included ProceduralLadderClimbRuntime, LockstepStateValidator, EcosystemDirector, and SpatialAudioManager. No errors referenced `Assets/_Project/Scripts/Core/Content`.
Rejected Alternatives: Editing Submarine/Animation/Determinism/Audio files was rejected because the prompt domain remains CORE/ASSETS and those files are active parallel-agent ownership zones.
Scalability potential: Not applicable.
Hardware Impact: Not applicable until external compile wall is cleared.

## Final Self-Audit and Omega Polish
Problem: Content authority code must not hide runtime debt behind managed enumeration, silent missing assets, or runtime null fallback.
Solution: Ran a final `rg` audit on `Assets/_Project/Scripts/Core/Content` for `foreach`, `Resources.Load`, LINQ list chains, scene search APIs, coroutine hooks, `Camera.main`, and renderer material allocation paths; scan returned no hits. Editor validators fail loud through build/editor exceptions for missing Addressables groups, missing mesh/prefab hash bindings, and cyclic dependencies.
Rejected Alternatives: Broad runtime tolerance and post-load null guards were rejected because missing content must stop the build before player execution.
Scalability potential: Low keeps only Core/High_Res content and cheap proxies. Middle keeps validated LOD and batch payloads. High allows richer resident content after reference counting. Ultra admits Overkill only on devices that can spend the VRAM.
Hardware Impact: Expected i3/MX350 gain is avoided duplicate bundle residency, denied Overkill downloads, stripped flora colliders, automated LOD, small-shadow purge, and proxy rendering under slow async loads.

## Phase 2 Multiplatform Hardening
Problem: The first pass left binary layout, vault ownership, and platform I/O policy too soft for Quest/Android, Metal, Steam Deck, and high-end PC.
Solution: Added packed binary layouts and build-time size assertions; moved content black-box telemetry to `GlobalDataVault` via `BufferID.ContentAuthorityBlackBox` and `SystemID.ContentAuthority`; cached vault/VRAM/lifecycle dependencies before tick registration; added compute thread-group validation; capped synchronous lore reads to 64 KB; added visual feature masks for Dear Lie low/XR and Overkill PC content.
Rejected Alternatives: Private telemetry arrays, runtime `Resources.UnloadUnusedAssets()`, per-frame GlobalRegistry resolution, unbounded lore reads, and balanced middle-tier visual policy were rejected.
Scalability potential: Low/XR uses 1D LUT, triangle noise, and dot-product visibility lies. Middle keeps limited silt/salt features. High/Ultra unlock salt crystals, volumetric wake silt, procedural hull dents, raymarch detail, and 16-tap POM feature flags without allowing Quest/MX350 to download Overkill bundles.
Hardware Impact: i3/MX350 avoids Overkill residency and unbounded lore reads; Quest gets packed binary audit and no >1024 compute groups; Steam Deck avoids microSD-scale sync lore stalls; RTX-class hardware gets explicit feature flags to spend saved cycles on visuals.

## Phase 3 H-Phi Data Sovereignty
Problem: The runtime still owned binary residency and pending-load metadata in managed local state after the Phase 2 pass.
Solution: Moved bundle reference records/count to `BufferID.ContentAuthorityBundleRefs` and `BufferID.ContentAuthorityBundleRefCount`; moved pending load hash/start/hologram metadata/count to `BufferID.ContentAuthorityPendingLoads` and `BufferID.ContentAuthorityPendingLoadCount`. The only remaining managed arrays in the runtime are Unity object-reference bridges: renderer targets and hologram GameObjects/Renderers, which cannot be stored in native vault buffers.
Rejected Alternatives: Keeping private managed metadata tables was rejected. Storing `UnityEngine.Renderer`, `GameObject`, or `AsyncOperationHandle` payloads in `GlobalDataVault` was rejected because those are managed/engine object handles, not blittable cross-platform data.
Scalability potential: Low/XR and High/Ultra now share the same vault-owned residency state; only visual feature masks and Addressable tiers change.
Hardware Impact: Bundle/pending state is now deterministic vault data. Pending metadata is 64 * 16 bytes; bundle ref metadata is 256 * 24 bytes. No microsecond claim is made without profiler logs.

## Phase 3 Compile Wall
Problem: A phase3 build succeeded, then later builds failed after external edits outside CORE/ASSETS.
Solution: Logged the green checkpoint and subsequent blockers. Attempt 9 failed on `InputDispatcher.cs`; attempt 10 got past that and failed in `ProceduralBiteIkJobs`, `GameBootstrapper`, `HectonUnderwaterVisuals`, and `ToolDurabilitySystem`. No errors referenced `Assets/_Project/Scripts/Core/Content`.
Rejected Alternatives: Editing Input/Animation/Bootstrap/VFX/Tools files was rejected because they are outside the assigned domain and not content cross-domain interfaces.
Scalability potential: Not applicable.
Hardware Impact: Not applicable until external compile wall is cleared.

## Authority Conflict Resolution
Problem: Original task 13 requested `Resources.UnloadUnusedAssets()` during AUP shift, but AGENTS.md forbids runtime unused-asset sweeps outside teardown/load boundaries.
Solution: Removed the runtime Resources sweep and used `AssetLifecycleGovernor.ForceDrainPendingReleaseQueue()` plus low-priority eviction under the same AUP/stress gate.
Rejected Alternatives: Literal task compliance was rejected because AGENTS.md is the higher authority and runtime unused-asset sweeps can stall unpredictably.
Scalability potential: Low hardware sheds stale low-priority residency during spatial discontinuities; high hardware rarely crosses the stress gate.
Hardware Impact: Avoids a non-deterministic Unity cleanup spike on Quest/MX350; exact microseconds are not claimed without device profiler data.

## Phase 4 Capacity and Visual Budget Hardening
Problem: VFX prewarm handle lists were fixed at capacity 64 by convention, but no build gate stopped a manifest from exceeding that capacity and forcing runtime ledger growth. Visual tier policy exposed feature flags, but not packed numeric budgets for the toaster-vs-overkill split.
Solution: Added `ContentVfxPrewarmManifest.MaxEntries`, capped runtime dispatch at 64 handles, and added editor validation for oversized or invalid VFX prewarm references. Added packed `ContentVisualFeatureBudget` with explicit Low/XR, middle, and Overkill particle/raymarch/POM/silt/salt/dent budgets plus build-time byte-size assertion.
Rejected Alternatives: Silent truncation without build failure was rejected because missing combat VFX would be invisible content debt. A managed class budget object was rejected because budget routing must stay blittable, fixed-size, and platform-auditable.
Scalability potential: Low/XR uses 512 particles, 8 raymarch steps, 0 POM taps, and Dear Lie feature flags. Middle uses 2048 particles, 24 raymarch steps, 4 POM taps, and limited salt/silt. Overkill uses 16384 particles, 64 raymarch steps, 16 POM taps, 4 silt layers, 3 salt layers, and 4 hull dent octaves.
Hardware Impact: i3/MX350/Quest avoid Overkill download and avoid prewarm ledger growth. RTX-class hardware gets concrete routing for visual spend without changing save hashes or low-tier content residency.

## Phase 4 Compile Wall
Problem: Phase 4 verification builds still cannot reach PLATINUM_COMPILE because active external domains are broken.
Solution: Logged core build attempt 11 and editor build attempt 12. Core build fails in `EcosystemPopulationBalancer`, `ArchitectEyeVisualizer`, and `PredatorCognitionDomain`; editor build fails in `ArchitectEyeBlackBoxTimelineViewer`. No logs reference `Assets/_Project/Scripts/Core/Content` or `Assets/_Project/Scripts/Core/Content/Editor`.
Rejected Alternatives: Editing AI/Fauna/Core Diagnostics/Editor Diagnostics files was rejected because the assigned domain remains CORE/ASSETS and those are not required cross-domain interfaces for this pass.
Scalability potential: Not applicable.
Hardware Impact: Not applicable until external compile wall is cleared.

## Phase 5 Runtime Mutation and Registry Integrity
Problem: `ContentAssetHashMap` and `ContentLoreBinaryProvider` could sort serialized arrays during runtime lookup/open. That mutates ScriptableObject/component authored state in play and hides bad authoring order. The hash-map validator also skipped duplicate hashes and missing dependency hashes too softly.
Solution: Replaced runtime sorting with non-mutating sort-state scans and linear fallback for unsorted authored data. Kept sorting only in editor authoring paths. Added build validation for zero hashes, duplicate hashes, missing Addressables bindings, missing dependency hashes, and >256 MB single content entries.
Rejected Alternatives: Keeping runtime sorting was rejected under the ScriptableObject runtime mutation rule. Silently accepting duplicate hashes was rejected because FNV collisions or bad authoring would route one binary hash to the wrong Unity asset.
Scalability potential: Low and Overkill tiers now share stricter registry truth: no tier can ship with missing dependency hashes or silent duplicate bindings. Low hardware avoids accidental oversized assets; high hardware must opt into Overkill through valid Addressables/tier metadata.
Hardware Impact: MX350/Quest get a build-time 256 MB single-entry ceiling and no runtime SO dirty-state drift. High-end hardware still gets Overkill budgets only when registry entries are unique and explicitly bound.

## Phase 5 Compile Wall
Problem: Phase 5 verification still cannot reach PLATINUM_COMPILE due to moving external compile failures.
Solution: Logged attempt 13 and attempt 14. Core build now fails in `SpatialAudioManager`; editor build fails through `Hecton8.Core.csproj` in `LaserCutter`. No logs reference `Assets/_Project/Scripts/Core/Content`.
Rejected Alternatives: Editing SpatialAudio/Tool files was rejected because they are outside CORE/ASSETS and not necessary cross-domain interfaces for the content authority pass.
Scalability potential: Not applicable.
Hardware Impact: Not applicable until external compile wall is cleared.
