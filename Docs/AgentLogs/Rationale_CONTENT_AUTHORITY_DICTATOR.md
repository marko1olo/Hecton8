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

## Final Self-Audit and Omega Polish
Problem: Content authority code must not hide runtime debt behind managed enumeration, silent missing assets, or runtime null fallback.
Solution: Ran a final `rg` audit on `Assets/_Project/Scripts/Core/Content` for `foreach`, `Resources.Load`, LINQ list chains, scene search APIs, coroutine hooks, `Camera.main`, and renderer material allocation paths; scan returned no hits. Editor validators fail loud through build/editor exceptions for missing Addressables groups, missing mesh/prefab hash bindings, and cyclic dependencies.
Rejected Alternatives: Broad runtime tolerance and post-load null guards were rejected because missing content must stop the build before player execution.
Scalability potential: Low keeps only Core/High_Res content and cheap proxies. Middle keeps validated LOD and batch payloads. High allows richer resident content after reference counting. Ultra admits Overkill only on devices that can spend the VRAM.
Hardware Impact: Expected i3/MX350 gain is avoided duplicate bundle residency, denied Overkill downloads, stripped flora colliders, automated LOD, small-shadow purge, and proxy rendering under slow async loads.
