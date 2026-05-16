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

## Phase 6 Blackbox Binary Correctness
Problem: `ContentAuthorityTelemetryEntry` was declared as a 64-byte record, but the dump serialized only the explicit 48 bytes of fields and omitted a header. A crash dump reader would not know the record size or have the missing tail bytes.
Solution: Added explicit reserved fields to bring the serialized telemetry payload to 64 bytes, wrote a magic header, entry count, and struct-size header before the 300-entry ring, cached the dump path in `Awake`, and gated dump execution to once per session. The path still targets `Docs/AgentLogs/Dump_CONTENT_AUTHORITY_DICTATOR.bin` in-project and falls back to `Application.persistentDataPath` on platforms where that project directory is unavailable.
Rejected Alternatives: Keeping implicit `[StructLayout(Size=64)]` padding was rejected because managed field-by-field serialization does not write padding bytes. Rebuilding the file path on every non-finite frame was rejected because a persistent NaN would cause repeated fault-path work.
Scalability potential: Low/Quest/Steam Deck get a parseable, bounded 19.2 KB telemetry body plus 16-byte header without repeated dump storms. High/Ultra get the same deterministic forensic payload when visual overkill exposes data faults.
Hardware Impact: No normal-frame microsecond claim. Fault path now writes one bounded dump instead of repeatedly rewriting the file under a sustained non-finite pressure signal.

## Phase 6 Compile Wall
Problem: A green core build and green editor build were achieved, but a follow-up core build failed after concurrent external edits moved the compile wall.
Solution: Logged attempt 15 (`Hecton8.Core.csproj` exit 0), attempt 16 (`Hecton8.Editor.csproj /m:1` exit 0), and attempt 17 (core fail in `EcosystemRuntimeInstaller` and `BinaryLayoutManifest`). No failure referenced CORE/ASSETS.
Rejected Alternatives: Declaring PLATINUM_COMPILE complete from a transient green checkpoint was rejected because the latest build command failed. Editing Ecosystem/Core manifest files was rejected because those are outside the CONTENT authority boundary.
Scalability potential: Not applicable.
Hardware Impact: Not applicable until external compile wall is cleared again.

## Phase 7 Addressables Release Bridge and Tier Proof
Problem: The content VRAM intercept could remove the oldest unused biome from the content reference ledger and ask the lifecycle governor to evict low-priority assets, but it did not have an explicit `Addressables.Release` bridge for content-owned bundle handles. The tier validator also needed to prove actual Addressables group membership, not infer tier correctness from an address string.
Solution: Added a fixed 256-slot Addressables handle bridge in `ContentAuthorityRuntime`. Callers can bind an `AsyncOperationHandle` to `RegisterBundleAcquire(hash, handle)`. Duplicate handles for the same hash are released immediately, unused non-biome handles release when their ref count reaches zero, and the VRAM pressure path releases the tracked handle for the oldest unused biome cache before removing it from the ledger. Editor validation now resolves Addressables group membership by address and GUID and fails Core/High_Res/Overkill entries placed in the wrong group. `ObjectBatchBase.ReplacePayload` now refuses runtime mutation and remains an editor-only bake surface.
Rejected Alternatives: Keeping ledger-only eviction was rejected because it could report lower residency without releasing the actual Addressables handle. Storing `AsyncOperationHandle` in `GlobalDataVault` was rejected because it is not blittable vault data. Address substring validation was rejected because a wrong group can still have a correct-looking address.
Scalability potential: Low/Quest/MX350 get deterministic release of unused biome handles under the 1.8 GB ceiling and cannot download Overkill groups. Middle keeps High_Res assets only when grouped correctly. High/Ultra can retain Overkill handles only when the registry and Addressables group contract both prove ownership.
Hardware Impact: i3/MX350 benefit is VRAM correctness, not a claimed frame-time number. The handle bridge adds no per-frame scan; it uses bounded 256-slot scans only on acquire/release/pressure events. Editor tier validation is build-time, 0 runtime us.

## Phase 7 Compile Wall
Problem: Post-patch verification cannot declare PLATINUM_COMPILE because `Hecton8.Core.csproj` currently fails in external `Assets/_Project/Scripts/Core/BinaryLayoutManifest.cs`; the latest editor recheck now fails through that same core wall.
Solution: Logged attempt 19 with the exact external errors: missing `ResolveOptionalType`, missing `AssertResolved`, and incompatible `AssertSize`/`AssertOffset` overload calls. Attempt 20 against `Hecton8.Editor.csproj /m:1` exited 0 after the content patch. Attempt 21, run after the final handle-ownership comment, failed again through `BinaryLayoutManifest.cs`. No compile error references `Assets/_Project/Scripts/Core/Content`.
Rejected Alternatives: Editing `BinaryLayoutManifest.cs` was rejected because it is outside the CORE/ASSETS content authority surface and has active concurrent ownership.
Scalability potential: Not applicable.
Hardware Impact: Not applicable until the external binary-layout compile wall is cleared.

## Phase 7 First-Party Resources Gate and Compile Recovery
Problem: The one-time first-party `Resources.Load` purge could regress later unless content validation enforced it. The previous compile state was stale because the external binary-layout wall cleared after the phase 7 patch.
Solution: Added a first-party source scan to the content editor validator that fails any `Assets/_Project/**/*.cs` use of the banned Resources load APIs. Re-ran static scans: first-party Resources load scan returned no hits, and content-domain banned-pattern scan returned no hits. Re-ran compile: attempt 22 `Hecton8.Core.csproj` exited 0; attempt 23 `Hecton8.Editor.csproj /m:1` exited 0.
Rejected Alternatives: Relying on manual `rg` reports was rejected because a future first-party regression would bypass the build gate. Keeping task 20 blocked was rejected after the current build commands succeeded.
Scalability potential: Low/Quest/MX350 stay protected from accidental Resources-path loads and Overkill bundle pulls. High/Ultra keep explicit Addressables-tier ownership and can spend budget only through validated registries.
Hardware Impact: Build-time gate is 0 runtime us. Runtime benefit is avoiding accidental synchronous Resources path loads and their uncontrolled RAM residency.

## Phase 8 Refcount Integrity and Platform I/O
Problem: The content-owned Addressables handle bridge could remove a hash ledger entry after a failed handle-track attempt even when earlier references still existed. The lore provider assumed direct file access to StreamingAssets/dataPath, which is not portable for Android compressed package paths and is weak for Steam Deck microSD pressure. Build validation still sorted content hash maps while validating. Bundle refs lived in GlobalDataVault but teardown released handles without clearing the vault state.
Solution: Changed the failed handle-track rollback to release only the just-acquired ref and remove the hash only if that release made it unused. Changed lore path resolution to prefer `Application.persistentDataPath`, skip URI/jar package paths, and use a 64 KB fallback FileStream buffer aligned to `MaxSynchronousLoreReadBytes`. Added a build gate rejecting empty, absolute, URI, or jar lore dictionary paths. Removed `ForceSort()` from validator discovery. Added `ContentBundleReferenceCounter.Clear()` and teardown logic that releases content-owned handles, clears vault ref records, and unregisters the content VRAM owner.
Rejected Alternatives: Unconditional ledger removal was rejected because it can delete live residency state. Direct StreamingAssets File/MMF reads on Android were rejected because jar/compressed paths are not normal filesystem paths. Validator-side sorting was rejected because a build check must not repair authored data silently. Registering a zero-byte VRAM owner was rejected after confirming `VRAMBudgetTracker.Unregister()` exists.
Scalability potential: Low/Quest gets portable lore path behavior and no accidental deletion of live bundle refs under handle-table exhaustion. Steam Deck gets a larger bounded stream buffer for 64 KB lore blocks. High/Ultra keeps the same validated content hash route without changing overkill tier logic.
Hardware Impact: No new per-frame work. Refcount rollback runs only on a failed content-owned handle-track path. Lore fallback uses a 64 KB stream buffer to reduce repeated small reads; exact microseconds require platform I/O trace. Bundle vault clearing is teardown-only.

## Phase 8 Compile Wall
Problem: Current core compile is green after phase 8 patches, but current editor compile fails outside CORE/ASSETS.
Solution: Logged attempt 24 core green, attempt 25 interrupted tooling with empty log during concurrent dotnet build, attempt 26 editor fail in `ArchitectEyeVisualizer.cs`, attempt 27 core green, attempt 28 transient external `EcosystemDirector.cs` core fail, attempt 29 core green, and attempt 30 editor fail in external Diagnostics/Audio/World files. No error references `Assets/_Project/Scripts/Core/Content`.
Rejected Alternatives: Editing `Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs`, `Audio/PlayerCriticalProceduralAudioRenderer.cs`, or `World/AbyssalThermalManager.cs` was rejected because they are outside the content authority surface and have active external ownership.
Scalability potential: Not applicable.
Hardware Impact: Not applicable until external editor diagnostics wall is cleared.

## Phase 9 Object Batch and Resources Gate Hardening
Problem: `ObjectBatchBase` exposed BRG payload arrays but the build gate did not prove mesh/material tables, instance indices, chunk ranges, hashes, LOD levels, transforms, or bounds. The Resources purge validator matched only exact dotted calls and could miss spaced or fully qualified first-party API variants.
Solution: Added mesh/material count accessors to `ObjectBatchBase` and a build validator pass over first-party `ObjectBatchBase` assets. The validator fails malformed static debris payloads before runtime BRG binding. Replaced exact Resources substring matching with token scanning that catches `Resources`, fully qualified `UnityEngine.Resources`, whitespace around the dot, `Load`, `LoadAll`, and `LoadAsync` calls.
Rejected Alternatives: Reflection into serialized fields was rejected because public count accessors keep the validator simple and type-safe. Runtime null guards in BRG binding were rejected because malformed authored batches must fail before build. Exact substring matching was rejected because it is too easy to bypass with whitespace or a full namespace qualifier.
Scalability potential: Low/Quest/MX350 avoid malformed batch payloads that would waste draw setup or crash static debris rendering. Middle keeps validated chunked debris. High/Ultra can safely spend saved CPU/GPU submission budget on denser wreck dressing, salt crystals, volumetric silt, and procedural hull dents through the existing Overkill tier.
Hardware Impact: Build-time only, 0 runtime us added. Runtime benefit is avoided bad BRG payloads, avoided missing mesh/material null paths, and stronger prevention of synchronous Resources asset access.

## Phase 9 Compile Wall
Problem: Editor validator code now compiles, but current core compile is blocked by active external edits outside CORE/ASSETS.
Solution: Logged attempt 31 core failure in `Fauna/PredatorCognitionDomain.cs`, attempt 32 editor green after phase 9 validator changes, and attempt 33 core failure in `Bootstrap/GameBootstrapper.cs` due to `IDataVault` assembly identity mismatch. Static scans after the patch found no banned content-domain patterns and no first-party Resources load calls.
Rejected Alternatives: Editing Fauna, Bootstrap, or Core Memory assembly ownership from the content pass was rejected because those files are outside the assigned CORE/ASSETS domain and are not required cross-domain interfaces for the object-batch/resource validator change.
Scalability potential: Not applicable to compile wall.
Hardware Impact: Not applicable until the external core build wall is cleared.

## Phase 10 Lore Path Sovereignty and Short-Read Hardening
Problem: Lore dictionary validation rejected absolute/URI/jar paths, but it still allowed `..` traversal and runtime rooted paths. The fallback stream read used one `Read` call, which can return partial data under slow or contended storage even when more bytes remain.
Solution: Added `ContentLoreBinaryProvider.IsPortableDictionaryRelativePath()` and routed both runtime resolution and editor validation through it. Runtime resolution now accepts only portable relative dictionary paths, resolves full paths under known Unity roots, rejects candidates outside the chosen root, and loops fallback `FileStream.Read(Span<byte>)` until the requested block is complete or EOF is reached.
Rejected Alternatives: Leaving runtime absolute-path acceptance was rejected because build gates and runtime behavior must agree. Raw `Path.Combine` without root containment was rejected because traversal breaks data sovereignty. A managed byte-array staging buffer was rejected because the caller already supplies a `Span<byte>`.
Scalability potential: Low/Quest gets deterministic packaged/persistent lore behavior without bad jar/URI/rooted paths. Steam Deck gets safer partial-read handling for microSD or contended disk reads. High/Ultra uses the same hash-routed lore interface without divergent asset paths.
Hardware Impact: No new per-frame work. Path validation and full-path containment are cold open only. Read-loop overhead occurs only for fallback lore reads and prevents partial block failure; exact microseconds require platform I/O trace.

## Phase 10 Compile Wall
Problem: Phase 10 code verification is blocked by external compile failures after the lore path patch.
Solution: Static scans after the patch returned clean for content banned patterns and first-party Resources load calls. Attempt 34 timed out under concurrent dotnet activity and was treated as interrupted tooling. Attempt 35 editor recheck failed in external `World/EcosystemDirector.cs` and third-party project restore assets. Attempt 36 core recheck failed in external `World/EcosystemDirector.cs`. No failure referenced `Assets/_Project/Scripts/Core/Content`.
Rejected Alternatives: Editing World, Tether, GPUInstancer, Den.Tools, Crest, or EasySave3 project state was rejected because those areas are outside the assigned CORE/ASSETS domain.
Scalability potential: Not applicable to compile wall.
Hardware Impact: Not applicable until external world/vendor build walls are cleared.

## Phase 11 Hologram Pool and Prefab Binding Gate
Problem: The 100 ms hologram fallback pool reused active proxies by round-robin index. That can alias two pending loads to one proxy; when the first load completes, it can hide the visible proxy for the second target. Prefab validation also did not fail missing `ContentAuthorityRuntime` hash-map or hologram proxy bindings.
Solution: Changed `ShowHologram()` to search the fixed pool for an inactive proxy and return `-1` when all proxies are active, avoiding alias corruption. Added public read-only binding/capacity accessors and a build validator pass that rejects content runtime prefabs missing the asset hash map, hologram mesh/material, or a pool capacity outside `1..MaxPendingLoadCount`.
Rejected Alternatives: Growing the pool at runtime was rejected because the pool must stay fixed and allocation-free. Reusing active proxies was rejected because it corrupts pending-load ownership. Relying on development logs was rejected because missing content authority bindings must fail before build.
Scalability potential: Low/Quest keeps a fixed proxy pool and avoids invisible blockers without runtime instantiation. High/Ultra keeps the same deterministic proxy ownership while richer assets stream in.
Hardware Impact: No allocation added. The only runtime cost is a bounded pool scan when a load has already exceeded the 100 ms proxy threshold; no normal-frame microseconds are claimed without profiler data.

## Phase 11 Compile Wall
Problem: Phase 11 core build cannot prove full compile because an external syntax wall stops compilation.
Solution: Static scans stayed clean after the hologram pool change. Attempt 37 failed in `SubmarineFluidDynamics.cs(4923,10)` with `CS1513 } expected`, outside CORE/ASSETS.
Rejected Alternatives: Editing SubmarineFluidDynamics from the content pass was rejected because it is outside the assigned content authority boundary.
Scalability potential: Not applicable to compile wall.
Hardware Impact: Not applicable until external syntax wall is cleared.

## Phase 12 Refcount Fail-Loud and Compile Recovery
Problem: `ContentBundleReferenceCounter.Release()` silently clamped negative ref counts to zero. That hides a double-release and can make Addressables/VRAM residency appear sane while ownership is already broken. The vault count guard also reset a local count to zero without clearing stale records, leaving later calls able to observe old bundle rows.
Solution: `Release()` now refuses zero hashes, unknown hashes, and zero or negative ref counts before returning failure; `Acquire()` refuses negative or `int.MaxValue` ref counts before increment; all public ledger access routes through normalized vault-count resolution. If the vault count exceeds the fixed capacity, the counter clears the fixed ledger in-place and emits a development-build diagnostic rather than walking invalid memory. Verification reached green core/editor checkpoints, then a follow-up core build failed in external `PhysicsApplySystem.cs` with no CORE/ASSETS errors.
Rejected Alternatives: Silent clamp was rejected because double-release is a crash vector under Addressables ownership. Throwing exceptions in gameplay was rejected by AGENTS. Keeping stale rows after count corruption was rejected because it preserves false VRAM residency state.
Scalability potential: Low/Quest/MX350 get deterministic residency failure behavior under handle misuse instead of hidden refcount drift. Middle/High/Ultra keep explicit Addressables ownership while Overkill content remains tied to valid ref counts and tier-gated handles.
Hardware Impact: No new per-frame work. Changes run only on acquire/release/ledger-read paths and add scalar guards over existing GlobalDataVault buffers. Exact microseconds are not claimed without profiler data.

## Phase 12 Compile Wall
Problem: Latest post-patch core recheck cannot hold PLATINUM_COMPILE because `PhysicsApplySystem.cs` now fails outside CORE/ASSETS.
Solution: Logged attempt 41. Errors are missing physics vault helpers, missing force packet queues/validation buffers, and missing `BufferID.Physics*` members. No errors reference `Assets/_Project/Scripts/Core/Content`.
Rejected Alternatives: Editing `PhysicsApplySystem.cs` or core physics buffer IDs was rejected because those are outside the content authority surface and not required cross-domain interfaces for the refcount patch.
Scalability potential: Not applicable to compile wall.
Hardware Impact: Not applicable until the external physics compile wall is cleared.

## Phase 13 Lore Block Metadata Gate
Problem: Lore providers could pass build validation with zero block indices, zero hashes, duplicate hashes, negative offsets, zero-length blocks, overflowed ranges, or overlapping byte ranges. Runtime `TryReadBlock()` would reject some of these, but that is too late for a content authority gate.
Solution: `ValidateLoreBlockIoBudgets()` now rejects malformed lore block metadata before build and sorts a local editor-only copy by byte offset to detect overlaps. `ContentLoreBinaryProvider.Open()` now catches IO/MMF path failures, disposes partial state, and returns false with a development-build diagnostic instead of letting a cold-open exception escape.
Rejected Alternatives: Relying on runtime false returns was rejected because UI text assets use the same hash route as textures and must fail before player builds. Letting MMF/FileStream exceptions propagate was rejected because content open failure needs controlled diagnostics, not an unknown crash.
Scalability potential: Low/Quest and Steam Deck get deterministic 64 KB bounded lore reads with valid non-overlapping ranges. High/Ultra keep the same hash-routed lore interface without divergent text asset plumbing.
Hardware Impact: Validator checks are build-time only, 0 runtime us. Runtime change affects cold dictionary open only; no profiler-backed microseconds are claimed.

## Phase 13 Compile Wall
Problem: Phase 13 verification cannot hold PLATINUM_COMPILE because current builds fail outside CORE/ASSETS.
Solution: Logged attempt 42 and attempt 43. Core fails in external `Core/Memory/H8Memory.cs` duplicate `BufferID.Physics*` entries and `World/SargassumMicroFaunaBoids.cs` duplicate `SaturateFinite01`. Editor fails through external `Core/Determinism/LockstepStateValidator.cs` missing lockstep/system-glitch lane constants. No failure references content runtime or content editor validators.
Rejected Alternatives: Editing Core Memory, World, or Determinism files was rejected because those are outside the content authority surface and not required cross-domain interfaces for the lore metadata gate.
Scalability potential: Not applicable to compile wall.
Hardware Impact: Not applicable until the external compile walls are cleared.

## Phase 14 Registry Shape Gate
Problem: `ContentAssetEntry.ToBinaryRecord()` can only represent dependency counts as `ushort` and records a compact enum/tier/LOD shape, but validation did not fail malformed enum values, negative VRAM estimates, unsupported LOD values, zero dependency hashes, self-dependencies, duplicate dependencies, or dependency lists above the binary count capacity.
Solution: Added `ValidateEntryShape()` inside hash-map validation. It rejects invalid asset kinds, invalid tiers, negative VRAM estimates, LOD levels above 2, dependency lists above `ushort.MaxValue`, zero dependencies, self-dependencies, and duplicate dependency hashes before the binary bridge can export a bad record.
Rejected Alternatives: Letting `ToBinaryRecord()` clamp dependency count was rejected because it silently drops graph edges. Treating `Unknown` kind or invalid tier as fallback was rejected because the registry is the authority bridge from binary hashes to Unity assets.
Scalability potential: Low/Quest/MX350 avoid loading malformed or oversized registry entries. High/Ultra keep Overkill asset residency behind valid tier and dependency metadata, not accidental enum drift.
Hardware Impact: Build-time only, 0 runtime us. No runtime hash lookup path changed.

## Phase 14 Compile Wall
Problem: Phase 14 verification cannot hold PLATINUM_COMPILE because current builds fail outside CORE/ASSETS after the registry shape gate.
Solution: Logged attempt 44 and attempt 45. Core now fails in external `UI/Navigation/DiegeticGyroCompassRuntime.cs`, `Core/SystemDispatcher.cs`, and `Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs`. Editor fails through external `DiegeticGyroCompassRuntime.cs`, `ArchitectEyeVisualizer.cs`, and `GlobalSignals.cs`. No failure references content validators or content runtime.
Rejected Alternatives: Editing UI Navigation, SystemDispatcher, Diagnostics, or GlobalSignals was rejected because those are outside the content authority surface and not required cross-domain interfaces for the registry validator change.
Scalability potential: Not applicable to compile wall.
Hardware Impact: Not applicable until external UI/dispatcher/diagnostics walls are cleared.

## Phase 15 Pending Vault and Blackbox Fault Containment
Problem: Pending-load metadata lives in `GlobalDataVault`, but the runtime read the shared count directly in several paths. If that count is corrupted above the 64-slot ceiling, the system could stop processing without clearing stale pending records, target bridges, or active hologram proxies. The NaN-triggered blackbox dump also used raw file/path calls and marked the dump complete before proving the write succeeded.
Solution: Added `TryResolvePendingLoadsNormalized()` so Track/Complete/Tick/Clear/Telemetry all share the same capacity guard. A corrupted pending count now clears the fixed vault records, clears the managed renderer bridge, hides all holograms, and emits a development-build diagnostic. Reworked blackbox writing into a contained `TryWriteBlackBox()` path that catches recoverable IO/path failures, creates the directory when allowed, retries the persistent-data fallback, and sets `_blackBoxDumpedThisSession` only after a successful write. Dump path resolution now catches recoverable failures and falls back to `Dump_CONTENT_AUTHORITY_DICTATOR.bin`.
Rejected Alternatives: Returning false on a corrupted pending count was rejected because it leaves visible proxy state disconnected from the authority ledger. Letting `File.Open`, `Directory.GetCurrentDirectory`, `Path.GetDirectoryName`, or `persistentDataPath` exceptions escape was rejected because the dump path executes during a fault report. Marking the dump one-shot before write success was rejected because it can lose the last 300-frame crash record.
Scalability potential: Low/Quest/MX350 keep a fixed 64-entry pending-load/hologram ceiling and recover from corrupted shared vault counts without dynamic allocation or invisible blockers. Middle/High/Ultra keep deterministic crash telemetry for Overkill asset failures and the same tiered visual budget routing.
Hardware Impact: No new normal-frame allocation path. Pending normalization is scalar work on existing Track/Complete/Tick/Telemetry calls. Blackbox IO changes are fault-path only after non-finite telemetry detection; no profiler-backed microseconds are claimed.

## Phase 15 Compile Wall
Problem: Phase 15 reached fresh green core/editor checkpoints after the content patch, but latest rechecks cannot hold PLATINUM_COMPILE because external code drifted again.
Solution: Logged attempts 46-51. Attempt 48 editor minimal build exited 0 with 0 warnings and 0 errors. Attempt 49 core recheck exited 0 with 0 warnings and 0 errors. Latest core/editor rechecks fail outside CORE/ASSETS in `VFX/Bioluminescence/BiolumPulseSyncRuntime.cs` missing `ResolveDataVault` and vendor `Temp/obj` restore assets. No failure references content runtime or content editor validators.
Rejected Alternatives: Editing VFX or vendor project restore state was rejected because those files are outside the content authority surface and not required cross-domain interfaces for pending-load vault normalization or blackbox fault containment.
Scalability potential: Not applicable to compile wall.
Hardware Impact: Not applicable until external VFX/vendor restore walls are cleared.
