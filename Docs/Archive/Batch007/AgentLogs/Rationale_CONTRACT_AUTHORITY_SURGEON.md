# CONTRACT_AUTHORITY_SURGEON Rationale

## Initial State
Problem: Mandatory state files were absent; initial required read returned missing-file errors.
Solution: Create persistent status and rationale files before C# edits so context compression cannot erase work state.
Rejected Alternatives: Chat-only memory; violates anti-amnesia protocol.
Scalability potential: Audit files do not touch runtime. Cheap devices and top-tier devices unaffected.
Hardware Impact: 0 us/frame. Disk write occurs outside runtime.

## Mandate Selection
Problem: Contract centralization touches Burst constants, AUP cell sizes, save hashes, telemetry identifiers, and frame-budget thresholds.
Solution: Bind work to PROJECT_LTS_Compatibility_Layer, OPT_Zero_GC_Policy_AllocFree_Mandate, MATH_Coordinate_Precision_AUP_FloatingOrigin, DATA_Save_Persistence_Binary_Delta_Checksum, DBG_Telemetry_Crash_Reporting_PostMortem, ARCH_Signal_Lane_Segregation, OPT_Performance_Budgets_FrameTime_VRAM_Limits, and OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.
Rejected Alternatives: Reading only the two required registry files; insufficient because the task explicitly owns AUP, save versioning, telemetry lanes, and LOD budgets.
Scalability potential: Low tier gets central caps and cheap approximations; Ultra tier gets explicit overkill caps without searching system code.
Hardware Impact: 0 us/frame for mandate selection. Runtime impact is constrained to compile-time constants or static readonly data.

## Loop 1 - Contract Authority Spine
Problem: Critical constants were split across physics math, gas dynamics, ecology balancing, AUP conversion, Homeostasis, MMF defaults, and save hashing.
Solution: Added contract classes under Core/Contracts using const aliases and private static readonly ref-readonly wrappers. Bound high-risk callers to the contracts and kept cross-domain edits to ABI-facing constants only.
Rejected Alternatives: Global search-and-replace of every number; too risky in a 20-agent dirty worktree. Inspector defaults as authority; too slow to audit and not Burst-inlineable.
Scalability potential: Low tier gets explicit hard caps and early hibernation thresholds; Ultra tier gets larger boid ceilings, wider spawn reactivation distances, and visual-overkill headroom without changing systems.
Hardware Impact: 0 us/frame for const aliases. Static validation is cold path only. Save payload grows by 16 bytes.

## Loop 1 - Compile Repair
Problem: Generated project state saw contract files through mixed Core and Core.Contracts assembly contexts; the save path could not resolve HectonContractVersion, then validator access was too narrow.
Solution: Kept a stable HectonContractVersion.cs anchor and moved the public version type into an included contract file; made HectonContractValidator public because other contract files can compile in a separate assembly.
Rejected Alternatives: Editing generated csproj; it is explicitly marked generated and would not survive Unity regeneration. Reverting save hash; violates BLACKBOX_VERSIONING.
Scalability potential: Compile repair only. No runtime scalability change.
Hardware Impact: 0 us/frame.

## Loop 2 - Human Sync And Sanity Gates
Problem: The contract Bible needs a human-readable sync path and automated impossible-value checks, or magic numbers will re-enter through markdown drift and inspector defaults.
Solution: Added a PowerShell handbook generator that parses public const declarations from Core/Contracts and regenerates Docs/ARCHITECT_HANDBOOK.md. Added NUnit edit-mode tests for finiteness, survival range bounds, ecology/scalability ratios, signal lane uniqueness, and contract version hash presence.
Rejected Alternatives: Manual documentation as source of truth; too easy to desynchronize. Runtime assertions in hot loops; violates zero-GC/frame-budget policy.
Scalability potential: Low tier keeps cheap caps testable; Middle/High/Ultra contract ratios remain visible for visual-overkill tuning without editing gameplay systems.
Hardware Impact: 0 us/frame. Generator and tests are editor/build-time only.

## Loop 3 - Omega Polish And Semantic Sweep
Problem: Post-polish scans still found semantic physical constants outside the first pass: water density, gravity, surface pressure, hydrostatic pressure, sound speed, survival gas defaults, and world Lotka defaults.
Solution: Added hydrostatic and world-ecology constants to the contracts, regenerated the handbook, mixed the new values into HectonContractVersion, and rebound targeted runtime/editor call sites. Replaced one consumer-side inverse with the math.rcp-backed ref-readonly AUP inverse.
Rejected Alternatives: Treat every 0.012f visual offset as O2 drain; false positive and cross-domain churn. Rebuilding all editor dependencies as proof; blocked by missing RealtimeCSG source files unrelated to this domain.
Scalability potential: Low tier gets identical laws with cheaper caps; Ultra tier can raise visual/entity ceilings from ScalabilityContract without changing gameplay systems.
Hardware Impact: 0 us/frame. Constants inline or static-ref copy; save payload remains +16 bytes; editor handbook/test work is offline.

## Loop 4 - Prompt Replay Re-Verification
Problem: The same assignment was replayed after the contract pass had already reached checked and polished status.
Solution: Re-extracted the XML block from CURRENT_BATCH.md, re-read AGENTS.md and the domain map, ran targeted contract scans, and rebuilt Hecton8.Core without code churn.
Rejected Alternatives: Rewriting completed contract files to create artificial progress; violates anti-refactor-loop discipline. Moving quaternion coefficients or visual offsets into physics contracts; those are authored presentation values, not engine laws.
Scalability potential: Re-verification preserves the current Low/Middle/High/Ultra contract surface and avoids bloating the authority layer with non-scaling art constants.
Hardware Impact: 0 us/frame. No runtime code changed during replay verification.

## Loop 5 - Multiplatform / H-Phi Inquisition
Problem: The replayed polish order expanded the contract authority surface: platform ABI ceilings, Steam Deck IO pressure, typed-lane ownership, fixed 300-frame blackbox sizing, and Dear Lie versus Ultra visual-overkill budgets were not represented as named laws.
Solution: Added HectonPlatformContract, HectonDataSovereigntyContract, and HectonVisualOverkillContract to the compiled contract authority unit; mapped their override offsets; mixed them into HectonContractVersion; regenerated Docs/ARCHITECT_HANDBOOK.md; added edit-mode sanity tests; and purged the final exact `5000.0` AUP sector literal outside HectonPhysicsContract from a world smoke-test probe.
Rejected Alternatives: Editing generated csproj files directly; they are disposable Unity output. Moving shader implementation, physics buffers, or renderer effects into contract authority; contracts must remain pure data. Repairing SubmarineFluidDynamics from this domain; that is an active physics-domain VaultNativeBuffer migration and not a contract failure.
Scalability potential: Low mode is explicit Dear Lie math: LUTs, one-octave triangle noise, dot-product samples, no raymarch/POM/SSS. Middle and High raise budgets progressively. Ultra mode reserves 64 raymarch steps, 16 POM taps, 8 SSS samples, 8192 wake silt particles, 2048 salt crystals, and 512 hull dent decals for RTX-class visual overkill.
Hardware Impact: 0 us/frame for the contract layer. Runtime systems still pay only when they consume these constants. The smoke-test literal replacement is compile-time/local variable reuse. Current project compile is blocked by unrelated `SubmarineFluidDynamics.cs` CS0103 errors; `Hecton8.World.Contracts.csproj` builds clean.

## Loop 6 - Contract File Authority Repair
Problem: The last pass left three contract surfaces as comment-only anchor files while the actual types lived in HectonContractValidator.cs. That made the "single law file per topic" model harder to audit and made the handbook/generator dependent on nested class parsing.
Solution: Moved HectonPlatformContract, HectonDataSovereigntyContract, and HectonVisualOverkillContract back into their named files. Directory.Build.targets now removes generated duplicate entries and explicitly includes those files for Hecton8.Core. The same build target removes the stale Hecton8.Core.Memory.Defrag DLL reference and includes MemoryDefragContracts.cs source so GlobalDataVault/SystemDispatcher compile against the actual contract type.
Rejected Alternatives: Editing Hecton8.Core.csproj directly; it is generated and disposable. Leaving anchor files; that violates the contract authority map. Moving defrag contracts into Core/Contracts; that would steal a memory-domain contract instead of wiring its existing file into the local build.
Scalability potential: No runtime behavior change. Low/Middle/High/Ultra visual and IO laws are now easier to find and change in their named files, which preserves toaster-mode and 4090-mode tuning without searching validator logic.
Hardware Impact: 0 us/frame. The repair is compile/build graph only; no hot-path allocations, no new jobs, no new runtime branches.

## Loop 7 - Automated Inquisition Gate
Problem: The contract authority checks were executable only as repeated ad hoc shell scans. That is not durable under context compression or a 20-agent dirty worktree.
Solution: Added Tools/ContractAuthority/Test-ContractAuthority.ps1. It enforces no public static float fields, no non-readonly public static fields, no Update/string.Format/delegates/EventBus, no local native allocation, StructLayout Pack=1, exact 5000.0 ownership, named contract-file authority, handbook sync, no DirectX-only shader pragmas, and shader thread-group ceilings. The gate passes and reports shader max product 512.
Rejected Alternatives: Claiming manual scans are enough; they are not reproducible. Folding the audit into Unity editor tests only; local shell audit is faster and catches shader/project text drift before editor import. Fixing current Core compile errors in Audio/GamePlay/Tether ownership domains; the failures changed across three probes and are active parallel-agent churn, not contract authority failures.
Scalability potential: Low tier remains protected by Dear Lie constants and shader ceilings; Ultra tier remains explicitly permitted through named visual-overkill budgets while avoiding hidden DirectX-only paths.
Hardware Impact: 0 us/frame. The audit is build/developer tooling only. Current Hecton8.World.Contracts build is clean; current Hecton8.Core build is blocked by non-contract interface implementation errors in external owner files.

## Loop 8 - Compile Stabilization Re-Probe
Problem: The prior Core compile wall was caused by active non-contract churn, then the disk state changed again while the session continued. The contract authority needed a fresh proof pass instead of preserving stale blocked status.
Solution: Re-ran the automated contract audit, rebuilt Hecton8.World.Contracts, rebuilt Hecton8.Core, probed the root `dotnet build` command, and ran diff whitespace hygiene against the touched contract/docs/tool files. The selected contract targets now compile cleanly; root `dotnet build` remains invalid without a selected project or solution because the Unity root contains many generated `.csproj` files.
Rejected Alternatives: Editing audio/gameplay/tether logic from the contract domain; not required after the owning churn settled. Claiming plain root `dotnet build` is green; it returns MSB1011 by project selection, not by C# compile failure. Creating a solution file as a workaround; that is build-system ownership, not contract authority.
Scalability potential: No runtime behavior change. Low/Middle/High/Ultra contract laws remain centralized, and the audit still blocks hidden DirectX-only shader paths and oversized mobile thread groups.
Hardware Impact: 0 us/frame. All work is static audit/build verification. Measured Unity runtime/GC proof remains absent in this CLI-only session.

## Loop 9 - Adjacent Survival Compile Repair
Problem: A later Core probe regressed in `HectonSurvivalSystem.cs`: `Awake()` still called the removed `EnsurePhysiologyScalarBuffer()` after the file had already been migrated to a `VaultBufferHandle<SurvivalPhysiologyScalarResult>` resolver.
Solution: Replaced the stale cold-path call with `_ = TryResolvePhysiologyScalarBuffer(out _)`, preserving the existing GlobalDataVault-backed allocation path and avoiding a return to local persistent NativeArray ownership.
Rejected Alternatives: Recreating `EnsurePhysiologyScalarBuffer()` with a local `NativeArray`; that violates the DataVault sovereignty migration. Removing the cold bootstrap entirely; that would delay diagnostics and change initialization behavior. Editing broader survival logic; not needed for this compile repair.
Scalability potential: Low tier keeps a single scalar result buffer requested from the vault; Ultra tier can consume the same scalar lane without duplicating persistent native storage.
Hardware Impact: 0 us/frame. The changed call is in `Awake()` only; hot-path `UpdatePhysiologyScalars` already resolves the existing vault view and returns if unavailable.

## Loop 10 - Version Authority File Repair
Problem: `HectonContractVersion.cs` was a comment-only anchor while the actual `HectonContractVersion` type lived inside `HectonContractValidator.cs`. That hides the save-law hash in the validator file and repeats the named-file authority failure already fixed for platform/data/visual contracts.
Solution: Moved `HectonContractVersion` back into `HectonContractVersion.cs`, kept `HectonContractValidator.cs` focused on cold validation helpers, explicitly wired the version file in `Directory.Build.targets`, and hardened the audit gate against generic comment-only anchors and off-file version definitions.
Rejected Alternatives: Leaving the anchor because builds previously passed; that is not an authority spine. Running build after every edit; user explicitly rejected rebuild spam, so the pass used static gates first and one final selected Core build only. Moving hash mixing out of contracts entirely; BLACKBOX_VERSIONING requires a stable public contract hash surface.
Scalability potential: Low/Middle/High/Ultra laws are unchanged. Save files still see the same 128-bit contract hash, now discoverable in the named version file.
Hardware Impact: 0 us/frame. The hash is static cold-path metadata; runtime gameplay systems do not tick it.

## Loop 11 - Handbook Static-Readonly Sync Gate
Problem: The handbook generator only documented `public const` declarations. That left `public static readonly` contract authority, specifically `HectonContractVersion.HashLo/HashHi`, out of the human-readable law map even though save files depend on it.
Solution: Updated the generator to parse public static readonly declarations and to accumulate multi-line declarations until the terminating semicolon, then regenerated `Docs/ARCHITECT_HANDBOOK.md`. Expanded the audit gate so every primary authority class must live in its named file, must not be duplicated in another contract file, and must appear in the handbook.
Rejected Alternatives: Treating the generated handbook as "close enough"; it would keep hiding computed version authority. Running another selected Core build for docs/tool-only edits; the user explicitly rejected rebuild spam, and the static audit covers this change surface. Moving computed hash values into consts; the contract hash is intentionally mixed in the static constructor from the current law set.
Scalability potential: Low tier still reads the same cheap caps and Dear Lie constants. Ultra tier still has explicit raymarch/POM/SSS/wake/salt/hull-dent budgets. The improvement is findability and CI-style drift prevention, not runtime behavior.
Hardware Impact: 0 us/frame. Generator and audit are developer tooling only; no hot-path allocations, no new native buffers, no extra Unity ticks.

## Loop 12 - Signal ABI Registry Gate
Problem: The signal-lane authority was still vulnerable to partial coverage. The edit-mode test sampled a handful of lanes, and `HectonContractVersion` mixed only `WfcOutpostStateChangedSignal`, so future lane ABI drift could avoid save/hash detection. The selected Core compile also exposed a generated-project shim gap: player movement presentation signal payload structs existed with explicit `Pack = 1`, but the Core build did not include their source file.
Solution: Added `HectonSignalLaneContract.SignalLaneRegistryHash` as a const FNV-1a digest over every public byte lane name/value pair, mixed that hash into `HectonContractVersion`, replaced the sampled NUnit lane test with reflection over all public lane IDs, and expanded `Test-ContractAuthority.ps1` to recompute the registry hash, fail duplicate/out-of-range lanes, verify handbook sync for every lane, and require the Core shim to include `PlayerMovementPresentationSignals.cs`.
Rejected Alternatives: Duplicating the missing player signal structs in `GlobalSignals` or Core/Contracts; that would create a second ABI source. Leaving versioning on one representative lane; that misses telemetry sort drift. Running repeated Core rebuilds after each small edit; user explicitly prohibited rebuild spam, so static gates ran first and one selected Core build ran after the build-graph repair.
Scalability potential: Low tier keeps the same byte-lane telemetry sort path with no managed dispatch. Middle/High/Ultra can add more typed lanes later, but the registry hash and audit now force explicit ABI ownership before those lanes reach saves or telemetry.
Hardware Impact: 0 us/frame. The hash is const metadata and the tests/audit are editor/tooling only. The build shim change only includes existing signal structs; it adds no runtime allocation, no Unity tick, and no native buffer.

## Loop 13 - SignalBus Coverage Closure
Problem: The lane contract still did not cover every concrete configured `SignalBus<T>`. A static comparison found 15 configured typed lanes without byte IDs, and the supposedly named `PlayerMovementPresentationSignals.cs` file had become an empty namespace while its six payload structs were embedded in the GlobalSignals monolith. A selected Core compile also showed adjacent scalability-event churn using a local `0x53434C54u` lane hash and an unqualified typed bus reference.
Solution: Added byte IDs 111-125 for the missing configured lanes, moved the six player presentation payload structs into `Core/Signals/PlayerMovementPresentationSignals.cs` with explicit Pack=1 layouts, removed those payload definitions from `GlobalSignals`, moved the scalability lane hash into `HectonSignalLaneContract.ScalabilityChangedEventStableHash`, and qualified `IPlatformIntegration` typed bus calls against `global::Hecton8.Core.Contracts.Signals.SignalBus<T>`.
Rejected Alternatives: Leaving configured lanes unregistered because the stable lane hash exists; telemetry sorting requires byte IDs. Duplicating player payload structs in both files; that creates ABI ambiguity. Restoring `GlobalSignals.InitializeAllQueues()` in scalability events; that hides the typed-lane dependency and does extra cold work. Repairing Sargassum/MarineSnow compile failures from this contract pass; those are World/VFX owner files and outside the assigned Core/Contracts boundary.
Scalability potential: Low tier keeps typed signal dispatch and byte-lane telemetry with no managed delegates. Middle/High/Ultra can add future signal lanes, but the audit now blocks any configured lane until it is registered and documented.
Hardware Impact: 0 us/frame. Constants and payload type movement are compile-time/ABI hygiene. The only compile wall remaining is external World/VFX churn in the selected Core project graph.
