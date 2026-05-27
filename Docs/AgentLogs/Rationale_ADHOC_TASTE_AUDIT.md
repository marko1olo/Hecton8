# ADHOC_TASTE_AUDIT Rationale

Date: 2026-05-26
Evidence class: STATIC_DOC / STATIC_SOURCE unless upgraded.

## Decision 1: Use Ad Hoc ID

Problem: User gave no `<AGENT_PROMPT id="...">` and no batch path, but asked for a project taste audit.
Solution: Use `ADHOC_TASTE_AUDIT` and keep all state in matching Status/Rationale/LOG files.
Rejected Alternatives: Claiming an existing batch ID would pollute another agent domain. Reading all current batch prompts cover-to-cover would violate strict parsing and create cross-task contamination.
Scalability potential: no runtime code impact.
Hardware Impact: 0us runtime. Static-only process.

## Decision 2: Static Audit First

Problem: `TASTE.md` defines aesthetic/product identity, but the workspace is heavily dirty and active agents modified many files.
Solution: Build a static mismatch map first, then patch only objective, isolated violations.
Rejected Alternatives: Broad code/style rewrite would trample concurrent agent changes and may break compile.
Scalability potential: preserves low-tier and ultra-tier requirements by checking `GlobalQualityWeight` and visual-fake doctrine before any runtime change.
Hardware Impact: 0us runtime until code changes exist.

## Decision 3: Relevant Mandates

Problem: Taste audit crosses docs, UI, rendering, sound, and possible runtime presentation.
Solution: Load `TASTE.md`, Zero-GC, performance budget, cinematic fake, noir rendering, global registry, evidence reporting, and zero-GC UI mandates.
Rejected Alternatives: Reading all `.agents-skills` files would waste context and dilute the task.
Scalability potential: low/middle/high/ultra taste review must preserve readability at weak settings and add sensory overload only at high settings.
Hardware Impact: 0us runtime. Prevents accidental simulation-heavy fixes.

## Decision 4: Dirty Worktree Guard

Problem: `git status` shows many modified/deleted/untracked files from other agents.
Solution: Do not revert, reformat, or normalize unrelated files. Before touching any dirty file, inspect local diff and require an objective TASTE violation.
Rejected Alternatives: Full cleanup or broad formatting would destroy parallel work.
Scalability potential: keeps integration risk bounded.
Hardware Impact: 0us runtime.

## Decision 5: Remove Derivative Public/Debug Text, Keep Serialized Type Names

Problem: First-party UI/gameplay source contained direct Subnautica comparison text and a visible debug title `SUBNAUTICA SYSTEMS DEBUG`.
Solution: Replace comments, runtime-created object names, debug log tags, and visible overlay title with HECTON pressure/machinery/visibility language.
Rejected Alternatives: Renaming `SubnauticaSystemsDebugUI` class/file or raw-editing scene YAML would risk serialized MonoBehaviour breakage. Keeping visible competitor text violates `TASTE.md` rejection list.
Scalability potential: Low/middle/high/ultra unchanged; this is identity cleanup only.
Hardware Impact: 0us runtime expected. String literal lengths changed only in cold/debug UI paths.

## Decision 6: Promote TASTE.md Into Active Root Policy

Problem: `TASTE.md` exists in the repository root and AGENTS requires it for gameplay/design decisions, but active docs still claimed only three root md files and did not list TASTE in authority order.
Solution: Update `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, and `Docs/ROOT_DOCS_REFERENCE.md` to recognize `TASTE.md` as root taste authority with evidence boundary.
Rejected Alternatives: Moving `TASTE.md` into Docs would violate the user's stated location and AGENTS read requirement. Leaving the contradiction causes future agents to treat the file as root bloat.
Scalability potential: no runtime effect. It protects weak-tier readability and high-tier sensory-overkill rules by making taste authority discoverable.
Hardware Impact: 0us runtime. Documentation-only correction.

## Decision 7: Clean Remaining Comment-Only Competitor Equivalents

Problem: Second source pass found `Subnautica-style`, `EXCEEDS SUBNAUTICA`, `Tiger Plant`, and `Brain Coral` residue in comments, UI progress notes, and debug-facing text.
Solution: Replace those with HECTON-8 pressure, route, instrument, and evidence language. Keep behavior and public API stable.
Rejected Alternatives: Treating competitor comparison as harmless internal shorthand violates `TASTE.md` and leaks design identity into future work. Renaming serialized classes/files in this pass risks broken Unity references.
Scalability potential: Low/middle/high/ultra behavior unchanged. The wording now points future work toward route evidence and pressure feedback instead of competitor mimicry.
Hardware Impact: 0us runtime expected. Comment/docs/string cleanup only.

## Decision 8: Defang Neon Aquarium Deep Cave Defaults With Palette Constants

Problem: `CaveDressingConfig.CreateDeepConfig()` explicitly described deep caves as alien/exotic and used blue-purple and bright cyan values that push toward aquarium spectacle.
Solution: Replace the deep preset wording and palette constants with black-water, oxidized mineral, muted cyan-green, and amber service-remnant accents. Preserve counts, cadence, and cheap shader/billboard approach.
Rejected Alternatives: Full art-system rewrite or particle simulation would violate cinematic-cheat and frame-time doctrine. Leaving neon values would contradict `TASTE.md`.
Scalability potential: Weak devices keep readable silhouette/fog/mineral cues; middle/high/ultra retain density and overkill layers without changing gameplay truth or becoming bright alien reef.
Hardware Impact: 0us CPU change. GPU cost unchanged by count; color constants only. Visual gain buys identity, not benchmark speed.

## Decision 9: Stop At Migration Boundary

Problem: `SubnauticaSystemsDebugUI` remains in class/file/scene serialized identity, and `floodedReef*` remains in serialized/save-adjacent vocabulary.
Solution: Record them as residual migration candidates instead of raw-renaming under a dirty, parallel workspace.
Rejected Alternatives: Raw YAML mutation, class rename without Unity validation, or save-field rename without migration would be high-risk integration damage.
Scalability potential: no runtime change. Future controlled migration can clean identity without breaking low-tier or high-tier behavior.
Hardware Impact: 0us runtime. Static risk containment only.

## Decision 10: Correct Player-Facing Lore And Localization

Problem: Active localization and lore registry content described wonder, beautiful screenshot fauna, coral/glowing algae, a rideable passive ray, safe shallow starts, screamers, plasma, and final boss framing.
Solution: Replace visible text with pressure-route, instrument-debris, acoustic warning, route-risk, fossil shelf, and cutting-arc language. Keep localization keys, table keys, stable creature IDs, and asset IDs unchanged.
Rejected Alternatives: Renaming keys like `REEF_GLIDER` or item asset paths would break generated hashes, localization lookup, or serialized references without a migration owner.
Scalability potential: Weak devices still get route silhouette and instrument evidence; high/ultra can add sensory overload without changing gameplay truth.
Hardware Impact: 0us runtime. Text/fallback-only changes.

## Decision 11: Preserve Serialization While Rewording Procedural World Data

Problem: World procedural families, rules, pattern profiles, and editor generators were pushing reef/coral/colorful/garden/alien identity into generated content.
Solution: Change labels, summaries, gameplay roles/intents, and proxy colors toward fossil shelf, carbonate, muted mineral, pressure habitat, and route readability. Preserve `familyId`, `ruleId`, `variantId`, `heatmapChannel`, GUIDs, and prefab root fields.
Rejected Alternatives: Raw-renaming `family.coral.*`, `coral_density`, prefab roots, or `biome.family.fossil_reef` would desynchronize scenes, authoring generators, saved data, and hash contracts.
Scalability potential: Low tier keeps cheap proxy silhouettes and readable muted color; middle/high/ultra can spend saved clarity budget on denser sensory layers without aquarium brightness.
Hardware Impact: 0us CPU. Proxy count and scatter budgets unchanged; only labels and color constants changed.

## Decision 12: Replace Comfort-Safe Copy With Reorientation Semantics

Problem: Biome matrix and world plans repeatedly framed first-hour/world spaces as safe, calm, comfortable, bright, and trusted, which conflicts with `TASTE.md` pressure-before-spectacle rules.
Solution: Mechanically replace active data and generators with reorientation, relief, route control, stable landmark, readable shelf, and pressure language while leaving `safePocket*` field names as schema.
Rejected Alternatives: Renaming `safePocket*` fields or enum/domain names would be a schema migration, not a safe taste fix. Leaving the wording would keep generating cozy content.
Scalability potential: Low tier still has readable survival pauses; high/ultra can make those pauses visually richer but not safer in gameplay truth.
Hardware Impact: 0us runtime. Static strings only.

## Decision 13: Encoding And Hash Discipline

Problem: A first JSON rewrite path used PowerShell text output and produced encoding risk; item fallback rename also changed a generated item display hash.
Solution: Restore only the JSON files touched by that faulty rewrite, redo localization edits with .NET strict UTF-8 no-BOM read/write, validate JSON, compute the new FNV UTF-16 hash for `Enzyme Carbonate`, and run `VerifyH8HashCollisions.py`.
Rejected Alternatives: Leaving mojibake or mismatched generated hashes would create content corruption. Running full hash generator under a dirty parallel workspace could rewrite unrelated generated sections.
Scalability potential: No visual/runtime change; protects deterministic data lookup on all tiers.
Hardware Impact: 0us runtime. Hash check returned 1218 records and 0 collisions.

## Decision 14: Regenerate Hash Catalog Instead Of Hand Editing

Problem: `H8Hashes.cs` is marked auto-generated and `--check-csharp` reported stale: current data/source scan contains 1218 records while the checked-in generated file had `TotalCount = 1018` and only 286 signal records.
Solution: Run `python Tools/VerifyH8HashCollisions.py --write-csharp Assets/_Project/Scripts/Core/Generated/H8Hashes.cs`, then verify with `--check-csharp`.
Rejected Alternatives: Keeping the manual `EnzymeCoralHash` edit would leave 200 signal constants missing and violate generated-file ownership.
Scalability potential: Low/Middle/High/Ultra all use the same deterministic IDs; no quality-tier truth changes.
Hardware Impact: 0us/frame. Compile-time constants only; hash verifier reports 1218 records and 0 collisions.

## Decision 15: Acoustic Echo DataVault Late Binding

Problem: `AcousticEchoLocationRuntime.EnsureInitialized()` could run before `GlobalRegistry.DataVault` existed, set `_initialized = 1`, and then `EnsureBootstrapVault()` refused every later retry because `_initialized != 0`.
Solution: Gate `EnsureBootstrapVault()` only on `_dataVault != null`, allowing a later registry publication to bind the vault while keeping steady-state cached access.
Rejected Alternatives: New service owner, scene search, or direct `TryGetLatestCreated()` fallback would break route doctrine or create a hotter dependency path.
Scalability potential: Low-tier devices avoid silent AI sensory failure after bootstrap ordering drift; high/ultra keep richer acoustic pursuit without changing authority.
Hardware Impact: 0us/frame after vault is bound. Bootstrap-only retry while unbound.

## Decision 16: WorldSliceAnchor Fidelity Cache

Problem: `WorldSliceAnchor.Awake()` always called `RefreshFidelityRoots()`, causing a child component scan and array replacement per anchor even when editor serialization already held a valid `fidelityRoots` cache.
Solution: Add `HasUsableFidelityRootCache()` and call refresh only when the cache is missing or contains nulls.
Rejected Alternatives: Removing refresh entirely could break dynamically damaged/old scenes; scanning on every state transition would be worse.
Scalability potential: Low/toaster scene loads avoid avoidable object graph scans; high/ultra can keep more authored fidelity roots without multiplying cold-start allocation.
Hardware Impact: Saves one `GetComponentsInChildren` plus one array replacement per correctly authored anchor at load; 0us/frame.

## Decision 17: Debug UI Identity Migration

Problem: Active source, scene identity, and csproj include still used `SubnauticaSystemsDebugUI`, an objective `TASTE.md` identity violation.
Solution: Rename class/file to `HectonSystemsDebugUI`, move `.meta` with the script to preserve GUID `46be80d17c774224b9ae34d72bccf74b`, update bootstrap scene object/class identifier, and update `Hecton8.Core.csproj`.
Rejected Alternatives: Leaving the residue trains future agents toward competitor framing. Raw class rename without `.meta` and csproj validation risks missing-script or compile-wall damage.
Scalability potential: No tier behavior change; debug identity now points at HECTON systems instead of derivative shorthand.
Hardware Impact: 0us/frame. Debug/cold identity cleanup.

## Decision 18: Atlas Checker BOM Handling

Problem: `Tools/AtlasCheck.py` failed before real validation because `Docs/DEPENDENCY_GRAPH.json` contains a UTF-8 BOM but the checker read JSON with plain `utf-8`.
Solution: Read atlas markdown/json/cache with `utf-8-sig` so the checker reaches actual reference validation.
Rejected Alternatives: Rewriting atlas outputs just to remove BOM would mask a checker robustness bug and churn generated docs under a dirty workspace.
Scalability potential: Offline documentation gate only. It protects integration reliability across machines with different text encodings.
Hardware Impact: 0us runtime. Tool-only fix.

## Decision 19: Build Gate Blocked By CPU Rule

Problem: C# files and csproj changed, but project rules forbid dotnet build while CPU is under load over 50%.
Solution: Run process/CPU preflight, hash gate, scans, and `git diff --check`; do not launch build while `Get-Counter` reports 97-100% and then 100% CPU.
Rejected Alternatives: Starting a build under explicit CPU prohibition would violate the repo's own integration guard and risk colliding with other agents.
Scalability potential: No runtime impact; protects parallel integration throughput.
Hardware Impact: 0us runtime. Verification debt: Unity/dotnet compile still required once CPU drops.

## Decision 20: WFC Grid Lease Lifetime

Problem: `WfcOutpostPowerBootRuntime.TryScheduleTranslation()` scheduled a job reading a leased WFC grid and writing DataVault-backed graph buffers, then released the grid lease and translation buffer locks before the job completed.
Solution: Keep the grid lease and buffer lock mask on the runtime while translation is pending, release them only after `DispatcherJobSwap.TryFinalizeCompleted()` reaches `CommitTranslation()`, and also release on dispose after completing the dependency.
Rejected Alternatives: Copying the whole grid into a fresh native buffer per translation would add memory churn. Leaving the old lease pattern risks compaction/reuse races and invalid reads under load.
Scalability potential: Low tier gets deterministic outpost power graph translation without hidden memory hazards; high/ultra can run denser WFC outposts without changing ownership routes.
Hardware Impact: 0us/frame steady state. Saves no microseconds directly; it removes a correctness hazard. Avoided extra 500-byte native copy per translation.

## Decision 21: Lease Release API

Problem: Existing `TryGetGrid()` had no explicit public release route, so callers could accidentally lock grid buffers permanently or release too early by convention.
Solution: Add `WfcOutpostGridRegistry.ReleaseGridLease(in lease)` and `ReleaseGridLease(BufferID, SystemID)`, then wrap generation service lease reads in `try/finally`.
Rejected Alternatives: Returning raw `NativeArray<byte>` without a release contract violates DataVault ownership discipline. Scene/global polling fallback would hide the lifetime bug.
Scalability potential: Weak devices avoid compaction stalls from leaked locks; high/ultra can keep larger runtime grids without lock leaks becoming frame spikes.
Hardware Impact: 0us/frame. Lock/unlock cost is cold/replay/translation-path only.

## Decision 22: Quest Android Quality Route

Problem: Platform audit proved `URP_Quest_VR.asset` existed but Android default quality still pointed at a non-Quest quality tier.
Solution: Add a dedicated `Quest (VR)` quality row using Quest configurator values and set `m_PerPlatformDefaultQuality.Android` to that row.
Rejected Alternatives: Relying on editor scripts to repair quality at build time leaves current project settings objectively wrong. Making a binary low/high switch would violate continuous quality doctrine.
Scalability potential: Quest gets a low-cost survival route; higher tiers remain excluded from Android and can keep visual overkill on their own platforms.
Hardware Impact: Quest avoids PC Low URP mismatch. Runtime CPU estimate depends on device, but settings reduce shadows, texture pressure, vegetation, and async upload budget for mobile VR.

## Decision 23: Shader Warmup Audit Modernization

Problem: `PlatformPortabilityProofAudit.py` treated only legacy `ShaderVariantCollection.WarmUp()` as explicit warmup, but the bootstrap uses Unity 6 `ShaderWarmup.WarmupShaderFromCollection()` and `GraphicsStateCollection.WarmUpProgressively()`.
Solution: Count both modern calls, keep legacy count visible, and update tests to assert the Unity 6 route.
Rejected Alternatives: Adding obsolete legacy calls to satisfy the audit would be fake compliance. Ignoring the warning would hide real missing-warmup cases in future changes.
Scalability potential: Low-tier hardware keeps predictable shader/PSO prewarm validation; high/ultra can add more collections without breaking audit semantics.
Hardware Impact: Audit-only 0us runtime. Prevents false platform failures and preserves progressive PSO warmup.

## Decision 24: Generator/Data Taste Residue Boundary

Problem: Active biome data and `BiomeMatrixBootstrapAuthoring` still contained `Fossil Reef`, `Coral-Porous`, `reef-node`, `beautiful`, `inviting`, `jagged neon`, and corrupted Russian mojibake in generated defaults.
Solution: Reword visible labels/descriptions/defaults to carbonate, oxidized, route-risk, exposure, and reorientation language. Preserve asset names, IDs, GUIDs, enums, and schema names as migration boundaries.
Rejected Alternatives: Renaming asset files, `*_CORAL` keys, `MaterialClass.Coral`, or `safe_shallows` in this pass would desynchronize generated hashes, serialized assets, and code contracts.
Scalability potential: Low tier keeps readable landmarks without aquarium/neon semantics; middle/high/ultra can add denser sensory layers while preserving pressure identity.
Hardware Impact: 0us runtime. Text/data/display-only changes.

## Decision 25: Verification Boundary

Problem: C# logic changed in power/grid systems, but project policy forbids launching dotnet/Unity build while CPU exceeds 50%.
Solution: Run all non-build gates available: strict residue scans, hash catalog check, Python unit tests, py_compile, platform audit, atlas gate, diff whitespace check, and CPU/process preflight.
Rejected Alternatives: Starting a build at 100% CPU would violate explicit repo rules. Claiming compile success without running it would be false reporting.
Scalability potential: Keeps parallel agents from fighting over build resources while preserving proof artifacts.
Hardware Impact: 0us runtime. Verification debt remains: Unity/dotnet compile when CPU load drops.

## Decision 26: Runtime Raw Job Completion Cleanup

Problem: `JobCompletionAudit` reported raw runtime blockers in abyssal path smoothing, sargassum density teardown/hot-swap, WFC dispose, logistics dispose, and the H8Memory owner-handle shutdown route.
Solution: Route those waits through `DispatcherJobSwap.TryComplete(..., forceComplete: true)` or `DispatcherJobFence.TryComplete(..., forceComplete: true)`. Keep the existing synchronous abyssal path semantics because converting it to true async requires owned persistent `NativeList`/snapshot lifetime state and would be a larger cross-domain rewrite.
Rejected Alternatives: Leaving raw `.Complete()` hides blocking boundaries. Rewriting abyssal path solving into a new async state machine inside a dirty multi-agent workspace risks disposing TempJob buffers before dependent jobs finish.
Scalability potential: Low-tier devices get explicit dispatcher-owned blocking points instead of accidental waits; middle/high/ultra keep the same path quality and can later move path smoothing to a real deferred owner without changing public contracts.
Hardware Impact: Steady-state work is unchanged; expected 0us/frame delta. Contract gain: `rawRuntimeBlockers` dropped from 7 to 0.

## Decision 27: RenderGraph Static Render Function

Problem: `HectonFluidAdvectionRenderFeature` used a non-static RenderGraph render lambda even though it only accessed `PassData` and `ComputeGraphContext`.
Solution: Mark the render function lambda `static` to prevent accidental closure capture and make the no-allocation contract explicit.
Rejected Alternatives: Leaving it non-static relies on compiler behavior and makes future captured state harder to spot.
Scalability potential: All tiers keep the same compute payload route; low-tier avoids avoidable render-path allocation risk, high/ultra keep richer fluid advection without closure churn.
Hardware Impact: Expected 0 allocations/frame for this render function; no dispatch count, group count, or GPU workload change.

## Decision 28: Build Gate Still Blocked

Problem: C# code changed, but CPU preflight after verification reported 99.8078583013261% processor time.
Solution: Do not start `dotnet build` or Unity compile. Use static and script gates instead: JobCompletion, OOP compute scanner, hash catalog check, platform audit, and `git diff --check`.
Rejected Alternatives: Launching build under the explicit >50% CPU prohibition would collide with parallel agents and invalidate the report.
Scalability potential: Protects shared integration throughput while preserving proof artifacts.
Hardware Impact: 0us runtime. Verification debt remains compile/import once CPU load drops below the project threshold.

## Decision 29: XR Provider Proof Must Be Loader-Route Proof

Problem: OpenXR package settings and `OpenXRLoader.asset` exist, but no serialized reference to the OpenXRLoader GUID was found in ProjectSettings or `Assets/XR`, and Quest feature blocks are present with `m_enabled: 0`.
Solution: Update `PlatformPortabilityProofAudit.py` schema v12 to report legacy provider proof, XR Management settings registration, OpenXRLoader asset presence, serialized loader GUID reference count, Single Pass Instanced, and Quest feature enabled state separately. Keep `xrProviderSerializedProof=false` until an actual loader route is serialized.
Rejected Alternatives: Counting OpenXR settings asset presence as provider proof would create a false platform readiness claim. Raw-writing XR Management YAML by hand would be unsafe without Unity import/API ownership.
Scalability potential: Low tier avoids shipping a dead headset route; middle/high/ultra can scale XR visuals only after the provider route exists.
Hardware Impact: 0us runtime. Static audit accuracy only.

## Decision 30: Android Quality Route Check Targets The Default Row

Problem: `XrPlatformReadinessValidator` failed Android quality readiness whenever any quality row excluded Android, even though the configured Quest row is Android default and included while PC rows should exclude Android.
Solution: Parse `m_PerPlatformDefaultQuality.Android`, resolve that quality row, and fail only if the Android default row is missing or itself excludes Android.
Rejected Alternatives: Removing the quality gate would hide a real Quest render-route risk. Keeping the broad `- Android` check makes correct multi-platform isolation look broken.
Scalability potential: Weak Quest hardware gets its tuned Quest URP row; desktop/high/ultra rows remain isolated instead of being forced into Android.
Hardware Impact: 0us player runtime. Prevents false build-preprocessor failure.

## Decision 31: Editor Platform Audit Must Not Emit False WARN/PASS

Problem: `PlatformCompatibilityAudit` classified Quest readiness without serialized XR provider proof and treated an empty `Assets/AddressableAssetsData` folder as Addressables project data.
Solution: Gate Quest status on serialized XR provider proof, reuse Android default quality-row validation, and require at least one non-meta Addressables data file before reporting Addressables data as PASS.
Rejected Alternatives: Trusting package install plus empty folders inflates static scaffolding into support claims. Creating placeholder Addressables settings would bypass the real content-authority gate.
Scalability potential: Platform readiness remains evidence-based on weak devices and high-end devices; content streaming and XR visuals are not claimed before routes exist.
Hardware Impact: 0us player runtime. Editor audit only.

## Decision 32: Addressables Gap Is Real, Not A Safe Local Fabrication

Problem: Addressables package and route validators exist, but `Assets/AddressableAssetsData` contains zero non-meta files. Content validators correctly require real settings/groups (`Core`, `High_Res`, `Overkill`).
Solution: Record the gap and improve audits; do not create a synthetic settings asset outside Unity Addressables APIs.
Rejected Alternatives: Hand-authored Addressables YAML would risk GUID/schema corruption and false content readiness. Ignoring the empty folder would keep a known build blocker hidden.
Scalability potential: Low/middle/high/ultra content tiers can only scale correctly after real Addressables groups exist.
Hardware Impact: 0us runtime. Verification debt: create/import Addressables settings through Unity and rerun content validators.

## Decision 33: Compute Audit Must Separate Owner, Reachability, And Payload Bridges

Problem: `PlatformPortabilityProofAudit.py` reported runtime compute dispatch and risky compute thread-group warnings as one coarse bucket. That mixed first-party runtime defects, vendor package code, RenderGraph payload-sized dispatch bridges, and runtime-folder compute assets referenced only by editor tests.
Solution: Upgrade the audit to schema v14. Add owner buckets (`FirstParty`, `Vendor`, `ExternalAsset`), payload-sized dispatch bridge detection across multiline `DispatchCompute` calls, first-party runtime dispatch gates, and a separate editor/test-only runtime asset risky compute bucket.
Rejected Alternatives: Whitelisting the two current RenderGraph files would be brittle. Treating every vendor package dispatch as a first-party blocker would make the gate noisy. Hiding vendor samples would lose useful platform debt evidence.
Scalability potential: Low-tier hardware gets hard gates only for owned runtime defects; middle/high/ultra work can still track vendor compute debt and payload bridge volume without blocking unrelated platform proof.
Hardware Impact: 0us runtime. Tool-only change. First-party runtime dispatch-without-proof count is now 0; vendor runtime dispatch samples remain visible.

## Decision 34: Shader Warmup Readiness Is Bootstrap-Owned

Problem: The audit exposed `shaderWarmupPreloaded=false` as a readiness failure even though project tests explicitly require `ProjectSettings/GraphicsSettings.asset:m_PreloadedShaders` to be empty. Global preloaded shaders bypass `GameBootstrapper` telemetry, timeout, fail-closed behavior, and continuous quality cadence.
Solution: Replace the readiness meaning with `graphicsSettingsShaderPreloadBypassDisabled=true` and `shaderWarmupRoutePresent=true` when ShaderVariantCollections plus bootstrap-owned Unity 6 warmup route exist. Keep raw preloaded entry count in the shader surface.
Rejected Alternatives: Reintroducing global preloads to satisfy a static flag would regress bootstrap authority. Treating an empty preloaded list as a warning would keep a known false positive alive.
Scalability potential: Weak devices keep frame-sliced bootstrap warmup; high/ultra can add more SVC/PSO data under the same authority instead of a binary global preload path.
Hardware Impact: 0us runtime. Prevents uncontrolled boot spike route from being normalized by the audit.

## Decision 35: PICO SDK Absence Is Not A Readiness Failure Without A Target Card

Problem: `picoPackagePresent=false` appeared as a readiness failure even though active evidence in this pass only proves a Quest/OpenXR scaffold target and no explicit PICO target requirement was found.
Solution: Keep PICO package candidates in the package/XR surface, but remove `picoPackagePresent` from readiness flags.
Rejected Alternatives: Installing or requiring an optional SDK without a platform target card would add dependency weight and false obligations. Hiding PICO package candidates entirely would reduce future platform visibility.
Scalability potential: Platform scope remains evidence-based. If PICO becomes an explicit target, a separate target card can promote it from package evidence to a hard gate.
Hardware Impact: 0us runtime. Tool-only classification correction.

## Decision 36: Build Gate Still Blocked At Phase 7

Problem: Python audit tooling changed and C# project state still needs Unity/dotnet verification eventually, but CPU preflight reported 100% and no `dotnet`/`csc` process. Project rules forbid starting builds above 50% CPU.
Solution: Run Python compile/tests, platform audit, OOP compute scanner, diff whitespace check, and CPU/process preflight. Do not launch dotnet/Unity build.
Rejected Alternatives: Starting a build under 100% CPU would violate the explicit parallel-agent rule. Claiming compile/import success without execution would be false reporting.
Scalability potential: Keeps shared build resources from thrashing while preserving deterministic proof artifacts for the next low-CPU window.
Hardware Impact: 0us runtime. Verification debt remains Unity/dotnet compile/import once CPU drops below threshold.

## Decision 37: Addressables Load Mode Must Be A Build Gate

Problem: Addressables 2.7 exposes `AssetLoadMode.AllPackedAssetsAndDependencies`, which can pull an entire packed bundle on first asset touch. The content validator required `Core`, `High_Res`, and `Overkill` groups but did not reject this mode.
Solution: Validate every bundled Addressables group against `AssetLoadMode.RequestedAssetAndDependencies`; required tier groups must also have a `BundledAssetGroupSchema`. Texture and item authoring helpers set this mode explicitly through Unity Addressables APIs.
Rejected Alternatives: Hand-writing Addressables YAML would corrupt GUID/schema ownership. Trusting package defaults leaves a future inspector change able to silently create whole-bundle memory spikes.
Scalability potential: Low devices load only requested content and dependencies; middle/high/ultra can still spend memory on richer assets through explicit tier groups instead of accidental bundle-wide pulls.
Hardware Impact: 0us player runtime for the code change. Prevents cold load/VRAM spikes that would be worst on MX350-class hardware.

## Decision 38: Texture Tier Group Must Not Collapse Into DefaultGroup

Problem: `HectonTextureImportDictator.ResolveTieredTextureGroup()` returned `settings.DefaultGroup` whenever it existed, so the named `Hecton_TextureStreaming_Auto` group was effectively bypassed in normal Addressables settings.
Solution: Resolve or create the named texture streaming group directly and configure its bundled schema to requested-asset mode.
Rejected Alternatives: Keeping DefaultGroup reuse hides texture-tier ownership inside whichever group happens to be default. Renaming serialized labels was unnecessary and would not fix group ownership.
Scalability potential: Low tier can prewarm `Tier_Low` without dragging unrelated default content; high/ultra can prewarm `Tier_High` under the same label route.
Hardware Impact: 0us frame cost. Editor/import route only; prevents future bundle topology from bloating cold texture prewarm.

## Decision 39: Bootstrap Dependency Handle Fault Must Release

Problem: The new bootstrap Addressables dependency prewarm released dependency handles only through `GlobalRegistry.AssetLifecycle`. If the governor was missing, a successful handle caused a false failure and remained unreleased.
Solution: Keep lifecycle governor as the normal release route. If it is absent, directly `Addressables.Release(handle)` and return `false` so bootstrap remains fail-closed while avoiding a leaked handle.
Rejected Alternatives: Returning success without the lifecycle owner would hide a broken bootstrap service route. Returning failure without release leaks memory.
Scalability potential: Low devices avoid leaked dependency handles during failed boot; high/ultra retain the same owner-routed release path when bootstrap services are healthy.
Hardware Impact: 0us steady state. Missing-governor fault path releases one handle instead of leaking it.

## Decision 40: Android OpenXR Repair Must Enable Features

Problem: The XR repair route assigned the OpenXR loader and Single Pass Instanced render mode, but current Android OpenXR feature assets had `m_enabled: 0`, including `MetaQuestFeature` and all Quest controller profiles.
Solution: Extend the Unity API repair route to enable `MetaQuestFeature`, `OculusTouchControllerProfile`, `MetaQuestTouchPlusControllerProfile`, and `MetaQuestTouchProControllerProfile`. Add validation failure when Meta Quest support or all Quest controller profiles are disabled.
Rejected Alternatives: Counting loader assignment alone as platform proof would still ship a Quest route without the required OpenXR feature extension or controller bindings. Raw-editing `Assets/XR/Settings` YAML remains unsafe.
Scalability potential: Quest/mobile gets explicit provider/input readiness; high-end desktop XR remains isolated by build target group instead of inheriting Android feature assumptions.
Hardware Impact: 0us player frame delta. Editor/build-preprocessor only; device validation still required after Unity import.

## Decision 41: Compile Verification Boundary At Phase 8

Problem: C# editor/runtime code changed and should be compiled, but the workspace has no root `Hecton8.Core.csproj`. CPU briefly dropped to 44%, then returned to 100% after the missing-project build attempt.
Solution: Attempt `dotnet build Hecton8.Core.csproj --no-restore` only during the low-CPU window; record the concrete `MSB1009 Project file does not exist` failure. Do not start additional build attempts after CPU returns to 100%.
Rejected Alternatives: Claiming compile success would be false. Running broad Unity/dotnet work under 100% CPU violates the parallel-agent rule.
Scalability potential: Protects shared machine throughput and keeps verification debt explicit.
Hardware Impact: 0us runtime. Verification debt remains Unity import/compile in an environment with generated csproj or through Unity batchmode.

## Decision 42: Shader Catalog Must Be Bootstrap-Owned, Not Resources-Owned

Problem: `RuntimeShaderReferenceCatalog` was a first-party runtime asset loaded through `Resources.Load` during `BeforeSceneLoad`, while local project rules ban first-party runtime Resources routes and require one owner/one route proof.
Solution: Move the catalog asset to `Assets/_Project/Data`, preserve GUID `66443d0a1f184aef87c6fd729fd8f401`, serialize it on `GameBootstrapper`, register it in `Awake()`, and unregister it in `OnDestroy()`. Keep all `TryGet*` accessors pure reads of the cached reference.
Rejected Alternatives: `Shader.Find` is release-fragile and string-owned. Always Included Shaders creates a global bucket outside bootstrap telemetry. Raw Addressables YAML would fabricate content settings in a project whose Addressables data is still intentionally blocked. A hot registry lookup in every accessor would violate read purity.
Scalability potential: Low tier keeps the cheapest direct shader reference route without pulling a Resources bucket; middle/high/ultra can expand the catalog under the same bootstrap owner without changing gameplay truth or DTO layout.
Hardware Impact: 0us/frame. Cold boot replaces a hidden Resources lookup with one serialized reference registration; first-party Resources packing pressure is reduced.

## Decision 43: BuildInfo And Diagnostic Materials Cannot Stay In First-Party Resources

Problem: `BuildInfo.asset` and diegetic UI diagnostic materials were still under `Assets/_Project/Resources`, so a future player build could pack first-party assets through the legacy Resources route even after runtime code stopped calling it.
Solution: Move `BuildInfo.asset` to `Assets/_Project/Data`, update `BuildInfoPreprocess.AssetPath`, move the three diagnostic materials to `Assets/_Project/Art/Materials/Diagnostics`, and keep their original `.meta` GUIDs.
Rejected Alternatives: Deleting the assets would break any GUID consumers. Changing GUIDs would create avoidable Unity reference churn. Leaving the assets in Resources because no active code path was found would keep the banned packaging surface alive.
Scalability potential: Weak devices avoid accidental startup/memory tax from a legacy all-in-folder asset route; higher tiers can still reference materials explicitly through normal art/diagnostic ownership.
Hardware Impact: 0us/frame. Expected gain is packaging/startup hygiene, not hot-path speed.

## Decision 44: Add A Future-Proof Resources Asset Gate

Problem: The existing content authority validator rejected first-party `Resources.Load` calls but did not reject non-doc assets placed under `Assets/_Project/Resources`.
Solution: Add `ValidateNoFirstPartyResourcesAssets()` to the build validators. It permits only `.meta` files and the folder README, and fails build preprocessing for any other file under the first-party Resources root.
Rejected Alternatives: Relying on convention or one-time cleanup allows the same banned route to reappear. Deleting the empty folder/meta in this dirty workspace would add hygiene churn without improving the enforcement.
Scalability potential: All quality tiers keep asset ownership explicit. Future low/middle/high/ultra content cannot silently bypass Addressables/serialized-owner routes through a hidden Resources drop.
Hardware Impact: 0us player runtime. Editor/build-preprocessor scan only; current folder scan found no non-doc files.

## Decision 45: Phase 9 Verification Boundary

Problem: C# and Unity scene serialization changed; early CPU preflight reported 96%, and final low-CPU preflight still showed no root Unity-generated `.csproj` or `.sln`.
Solution: Run static proof gates only: strict runtime Resources scans, first-party Resources folder scan, moved GUID scan, focused `git diff --check`, and process/CPU preflight. Do not claim Unity/dotnet compile.
Rejected Alternatives: Running a build above the explicit 50% CPU ceiling would violate the parallel-agent rule. Running `dotnet build` without a project would only repeat the existing `MSB1009` failure. Claiming compile/import success without Unity is false. Hand-editing foreign generated report artifacts would create noisy ownership churn.
Scalability potential: Keeps integration pressure low while preserving precise proof artifacts and residuals for the next import/build window.
Hardware Impact: 0us runtime. Verification debt remains Unity import/compile and generated report refresh.

## Decision 46: Hot Path Validator Must Cover Dispatcher Lanes

Problem: `PerformanceHotPathValidator` claimed to scan hot-path methods, but its signature regex only covered `Update`, `LateUpdate`, `FixedUpdate`, `Tick`, `FixedTick`, and `SlowTick`. Current dispatcher contracts and mandates also use `FastTick`, `UnscaledFastTick`, `ColdTick`, `FrostTick`, and `LateFrameTick`, so a violating method in those lanes could pass the proof gate.
Solution: Expand the editor validator regex to include the missing dispatcher lane method names. Keep the change limited to the editor scanner; do not alter runtime lanes, registries, jobs, or gameplay code.
Rejected Alternatives: Changing runtime dispatcher interfaces would be architecture churn and could conflict with other agents. Raising the validator from warning/reporting to build failure without a false-positive study would be a separate policy change. Leaving the gap would keep a false-negative proof artifact.
Scalability potential: Low/middle/high/ultra all depend on the same hot-path discipline; better scanner coverage protects cheap devices from hidden allocation/scene-search regressions and lets high/ultra spend performance on visuals intentionally.
Hardware Impact: 0us player runtime. Editor-only regex coverage change; verification parser reported 0 expanded-hot-method issues in the sampled runtime domains.

## Decision 47: Phase 10 Verification Boundary

Problem: The validator changed in C#, so compile/import proof is desirable, but CPU preflight reported 91% and the project root still contains no `.csproj` or `.sln`.
Solution: Run focused `git diff --check`, confirm the expanded regex by `rg`, and run a targeted static parser over primary runtime script roots with the expanded method set. Do not start dotnet/Unity compile under the explicit CPU rule or without project files.
Rejected Alternatives: Launching a build at 91% CPU violates the parallel-agent rule. Claiming compile success without Unity/project files would be false. Broad generated-report rewrites would trample foreign ownership and do not prove this editor scanner change.
Scalability potential: Verification stays cheap and repeatable on overloaded shared hardware; the next low-CPU Unity import/build pass can validate syntax.
Hardware Impact: 0us runtime. Verification debt remains Unity compile/import once CPU and generated project files allow it.
