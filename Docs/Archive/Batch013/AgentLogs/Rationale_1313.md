# Rationale 1313 - DATA_MONOLITH_BAKER_AND_RELEASE_PURGER

Date: 2026-05-25
Evidence class: STATIC_SOURCE / STATIC_DOC until Unity import, bake, boot, profiler, and GC proof exist.

## Decision 01 - Phase 0 Boundary

Problem: User asked to start Phase 0, while the full prompt has 10 tasks and existing source already contains Data Monolith classes.
Solution: Execute Tasks 01-03 only: inventory static-data parser surfaces, audit DTO layout surface, and map boot dependency route. Do not mutate runtime code before the hit list is source-backed.
Rejected Alternatives: Implementing Tasks 04-10 immediately risks duplicating existing Data Monolith code and colliding with other agents. Declaring readiness from the existing payload violates `ARCH_Project_Bootstrap_Sequence_Init_Safety`.
Scalability potential: Low tier consumes compact binary tables; Middle tier can stage validated hot reload; High tier keeps richer editor manifests; Ultra tier can carry visual-overkill authoring data outside gameplay truth.
Hardware Impact: Static Phase 0 has no runtime gain. Expected later removal of runtime parsers targets cold boot spikes and managed allocations on i3/MX350.

## Decision 02 - Mandate Set

Problem: Data Monolith touches authoring CSV, binary storage, ARM64 DTO layout, boot order, DataVault, and parser purging.
Solution: Use 8 mandates: designer CSV bridge, ARM64 layout, zero-GC, native memory/jobs, bootstrap, global registry DI, crash telemetry, and binary/checksum persistence.
Rejected Alternatives: Reading physics/AI/render mandates would add noise outside the 1313 domain. Skipping mandates violates batch protocol.
Scalability potential: Mandate set covers low-to-ultra behavior without binary quality flags; GlobalQualityWeight affects optional debug/authoring richness, not static truth layout.
Hardware Impact: Correct mandate alignment prevents low-end cold boot file parsing and ARM64 unaligned DTO traps.

## Decision 03 - Evidence Class

Problem: Existing docs claim a current `static_data.h8bin` exists, but runtime readiness needs bake/import/boot proof.
Solution: Mark all Phase 0 findings `PENDING VERIFICATION`; cite static source and docs only.
Rejected Alternatives: Treating file existence as runtime readiness is false proof. Running a full build before CPU/compiler preflight is forbidden.
Scalability potential: Honest evidence boundaries avoid shipping a binary path that works only on developer machines.
Hardware Impact: No measured gain until bake and boot paths are executed under Unity/player proof.

## Decision 04 - Existing Monolith Route Is The Owner

Problem: The prompt asks for a binary monolith route, but source already contains `H8StaticDataArena`, `H8DataMonolithCompiler`, a layout guard, and a release build gate.
Solution: Treat the existing route as owner: `GameBootstrapper.InitializeMemoryPreWarmPhaseAsync` creates/registers `GlobalDataVault`, then calls `H8StaticDataArena.TryInitializeFromStreamingAssets` through `InitializeBootstrapDataMonolith`.
Rejected Alternatives: A second loader or direct runtime parser bridge would create two owners for static truth. Hot polling `GlobalRegistry` would violate cold DI policy.
Scalability potential: Low tier reads one resident binary payload; Middle tier keeps strict boot fail-fast; High tier can add editor hot reload; Ultra tier can carry richer authoring diagnostics without changing runtime DTO truth.
Hardware Impact: Preserving the existing payload/DataVault lane avoids extra startup allocations and duplicate memory on i3/MX350.

## Decision 05 - Text Ingest Evidence Split

Problem: A raw file-I/O scan finds hundreds of hits, but not every `Application.dataPath` or dump writer is a release static-data parser.
Solution: Split findings into strict runtime CSV/JSON/text ingest risks, CSV parser token review, broad file-I/O review, and monolith binary consumers in `DATA_MONOLITH_PHASE0_ARCHAEOLOGY_1313.json`.
Rejected Alternatives: Declaring all file I/O illegal would create false blockers. Ignoring non-editor CSV/parser tokens would hide real release purge work.
Scalability potential: Low tier release can purge strict text ingest first; Middle/High/Ultra can retain editor/dev diagnostics behind compile guards without changing the binary contract.
Hardware Impact: The first measurable low-end gain comes from removing strict runtime text ingest, not from deleting unrelated dump/file diagnostics.

## Decision 06 - No Compile In Phase 0

Problem: Phase 0 wrote report/status artifacts only, while project policy forbids casual rebuilds under shared-agent load.
Solution: Do not launch Unity/dotnet build. Verification remains static-source/static-binary only until a later source mutation or explicit build gate run.
Rejected Alternatives: Running a build without C# mutation wastes shared CPU and risks colliding with other agents. Claiming compile success without running it is false reporting.
Scalability potential: Honest verification staging keeps release gates meaningful across low-to-ultra hardware targets.
Hardware Impact: Zero runtime impact in Phase 0; no compiler load added to the shared machine.

## Decision 07 - 1313 Proof Artifacts Without Destroying X_002

Problem: Existing Roslyn scanner and release gate emit X_002 report paths, so 1313 cannot produce the mandated report names by running the existing Unity menu.
Solution: Add 1313 alias report writes while preserving X_002 outputs. `OOP_StaticData_Scanner` now writes `DATA_PIPELINE_OPTIMIZATION_REPORT_1313.json`; `H8DataMonolithReleaseBuildGate` now writes 1313 release/development gate reports.
Rejected Alternatives: Renaming X_002 constants would destroy another agent's proof route. Faking a Roslyn AST report from shell would be false evidence.
Scalability potential: Low/Middle/High/Ultra builds share the same gate logic and can emit separate proof artifacts without duplicating scanner ownership.
Hardware Impact: Editor-only write amplification only when scanner/gate runs. No player runtime cost.

## Decision 08 - Runtime Zero-GC Verdict Is FAIL

Problem: User demanded proof of no runtime `new`, boxing, string concatenation, `ToString`, LINQ, `FileStream`, or parser residue.
Solution: Run a paranoid static scan and mark `H8StaticDataArena` as failing Zero-GC proof: 33 managed-token hits remain after adding the 1313 dump route. Exact lines are in `DATA_MONOLITH_PARANOID_REVIEW_1313.md`.
Rejected Alternatives: Calling startup managed I/O acceptable would violate the prompt. Rewriting the entire file path and platform I/O layer in one blind pass would risk breaking boot without profiler or player proof.
Scalability potential: Later fix should split pure native player file ingestion from editor/mobile staging paths so low-tier avoids managed cold boot spikes while high/ultra keep editor diagnostics.
Hardware Impact: Current state still risks cold-load managed allocation overhead on i3/MX350. No measured savings claimed.

## Decision 09 - Black Box Ownership Patch

Problem: Data Monolith telemetry dumps existed for SHINOBU_103, DATA_MONOLITH, and X_002, but not 1313.
Solution: Add `Dump_1313.bin` to `H8StaticDataArena.DumpTelemetry` so crash/corruption evidence has a 1313-owned binary artifact.
Rejected Alternatives: Treating `Dump_X_002.bin` as sufficient violates one owner, one proof artifact.
Scalability potential: Same 300-frame ring supports all hardware tiers without changing gameplay truth.
Hardware Impact: Only executes on corruption/threshold dump path. No steady-state frame cost.

## Decision 10 - Fail-Closed Without Data Monolith Managed Throw

Problem: `H8StaticDataArena.TryInitializeFromStreamingAssets` and `GameBootstrapper.InitializeBootstrapDataMonolith` both had managed fatal-exception routes for the same Data Monolith boot failure.
Solution: Remove the arena throw and make the non-editor bootstrap route return `loaded`; `InitializeMemoryPreWarmPhaseAsync` already logs a fixed literal and returns `false`, stopping boot without creating the Data Monolith exception string.
Rejected Alternatives: Keeping duplicate throws violates fail-closed and creates managed allocation on corruption. Swallowing the failure and continuing would poison `GlobalDataVault`.
Scalability potential: Low tier gets deterministic boot abort without exception churn; Middle/High/Ultra keep the same binary truth route and can add richer out-of-band diagnostics later.
Hardware Impact: Saves one managed exception path and one enum-to-string concatenation on failure. No steady-state frame gain claimed.

## Decision 11 - No Blind ABI Field Reorder

Problem: Strict field-order policy flags `H8DataBlobHeader` and `H8ItemRecord`, but both are serialized ABI. Moving fields would invalidate the existing `static_data.h8bin` without a coordinated schema migration and rebake.
Solution: Keep offsets stable and document the exact byte map failure. Harden header/directory validation instead: exact section table offset, exact little-endian flag, and zeroed reserved fields are now required before Ready.
Rejected Alternatives: Reordering fields for a cleaner report would create binary incompatibility. Ignoring the strict-order failure would be false reporting.
Scalability potential: Low/Middle/High/Ultra all keep one compatible payload. A future schema version can reorder item payloads only with a baker migration.
Hardware Impact: Validation rejects corrupt/ambiguous payloads earlier. No microsecond saving claimed until boot measurements exist.

## Decision 12 - Runtime Residue Remains A Real Blocker

Problem: After cleanup, `H8StaticDataArena` still has 6 actual managed heap/I/O `new` hits: `UnityWebRequest`, `DownloadHandlerFile`, `FileInfo`, two `FileStream`s, and `BinaryWriter`.
Solution: Mark Runtime Zero-GC ideal as incomplete and regenerate `DATA_MONOLITH_PARANOID_REVIEW_1313` plus `DATA_PIPELINE_OPTIMIZATION_REPORT_1313` with current line numbers.
Rejected Alternatives: Calling `ReadOnlySpan` constructors heap allocations is inaccurate; declaring the remaining file/dump path acceptable would violate the task.
Scalability potential: Low tier still needs a native player file/dump lane; Middle/High/Ultra can retain editor/mobile staging behind compile fences or explicit platform adapters.
Hardware Impact: Current pass removed three false-positive struct initializer tokens and one exception failure route. The remaining real gain requires replacing managed file and dump I/O.

## Decision 13 - Windows Player Native File Lane

Problem: Source-wide managed residue in `H8StaticDataArena` was real for editor/development, and the non-development Windows player had no active proof that `static_data.h8bin` could be read without managed file or URI staging.
Solution: Split the loader by preprocessor state. Non-development Windows player builds the player StreamingAssets monolith path with stackalloc `char*`, `GetModuleFileNameW`, and fixed literals, then reads the binary via `CreateFileW`, `GetFileSizeEx`, and `ReadFile` directly into the `GlobalDataVault` arena.
Rejected Alternatives: Keeping `UnityWebRequest`, `FileInfo`, or `FileStream` active in player release violates the Zero-GC purge target. Deleting editor/development staging would break WebGL/mobile/editor URI workflows without a replacement adapter.
Scalability potential: Low tier gets one binary read into resident native storage. Middle tier keeps the same deterministic payload. High and Ultra can add richer editor diagnostics without changing gameplay truth or DTO layout.
Hardware Impact: Expected low-end gain is removal of managed file object allocation and URI staging from Windows release cold boot. Exact microseconds are unmeasured because Unity/player boot was not run.

## Decision 14 - Active Token Model Over Source-Wide Panic

Problem: A source-wide grep still reports managed tokens because editor/development code is intentionally retained behind guards, while the user asked for release runtime proof.
Solution: Use a fixed preprocessor-aware token scan for two release models: Windows non-development and non-Windows non-development. Current result: `ACTIVE_HITS_WIN_RELEASE=0`, `ACTIVE_HITS_NONWIN_RELEASE=0` for `new`, managed file APIs, exception catches, string formatting/conversion, LINQ, and literal concat patterns.
Rejected Alternatives: Reporting source-wide residue as active release failure is imprecise. Reporting the old broken PowerShell scan would be false evidence; it was rerun after fixing bool literal substitution.
Scalability potential: Low/Middle/High/Ultra all keep the same player release branch purity on Windows. Editor/development diagnostics remain available only outside release.
Hardware Impact: Static proof only. No frame or boot measurement claimed.

## Decision 15 - Non-Windows Release Is Fail-Closed, Not Ready

Problem: Quest/Android/non-Windows release cannot use the Win32 native path, and using `UnityWebRequest` or managed URI staging would violate the strict runtime purge.
Solution: Non-development non-Windows release now returns `ReadFailed`/fail-closed unless an already loaded arena exists and `failIfMissing` is false. This prevents silent managed fallback but does not load the monolith on those platforms.
Rejected Alternatives: Pretending Windows P/Invoke solves Quest is false. Re-enabling managed URI staging in release contradicts the prompt.
Scalability potential: The next correct step is a platform PAL: Android asset manager or Unity-approved native plugin path for APK/StreamingAssets, then the same arena validation. Quality tiers must not affect DTO truth.
Hardware Impact: Current non-Windows path saves no gameplay time because it aborts. It prevents a bad release path from allocating or reading text/URI data.

## Decision 16 - Build Suppression After Large Source Change

Problem: A normal response after 542-line `H8StaticDataArena` diff would be to compile, but the user repeatedly ordered rare dotnet/build usage and shared-agent CPU contention is active policy.
Solution: Do not run dotnet or Unity build. Use static guards only: JSON parse, `git diff --check`, brace balance, `#if/#endif` balance, prompt recount, boot throw token scan, and active-preprocessor managed-token scan.
Rejected Alternatives: Running a build to make the report look stronger violates the explicit instruction. Claiming compile success without running it is false reporting.
Scalability potential: Static checks reduce obvious breakage risk without consuming shared compiler capacity. Actual readiness still requires Unity import, player boot, profiler, and GCMonitor proof later.
Hardware Impact: No compiler CPU load added. Runtime impact remains unmeasured.

## Decision 17 - Batch Audit Must Fail On Parser Findings

Problem: `H8DataMonolithBatchAudit.RunFromCommandLine` executed `OOP_StaticData_Scanner.Run()` but ignored the returned production finding count when deciding the batch-mode exit code.
Solution: Add `parserClean = parserFindings == 0`, emit an error when false, and exit success only when bake, validation, fuzzer, and parser scanner all pass.
Rejected Alternatives: Logging parser findings while returning exit code 0 would let CI accept a release with static-data parser residue.
Scalability potential: All hardware tiers benefit from the same release gate; no gameplay truth or quality tier is affected.
Hardware Impact: Editor/CI-only. No player frame cost. Prevents a bad binary/text-parser release from reaching low-end hardware.

## Decision 18 - 1313 Proof Alias For Fuzzer And Vault Stress

Problem: Existing corruption fuzzer and GlobalDataVault stress probe wrote X_002 reports only, leaving 1313 without direct proof artifact names for Phase 2 evidence.
Solution: Preserve X_002 reports and write 1313 aliases from the same run output: `DATA_MONOLITH_CORRUPTION_FUZZER_1313.json` and `DATA_MONOLITH_UNITY_GLOBAL_DATA_VAULT_STRESS_1313.json`.
Rejected Alternatives: Renaming X_002 files would break another agent's evidence. Copying stale old report files manually would be false proof.
Scalability potential: Evidence naming only; same bake/fuzz/stress logic verifies the payload used by all quality tiers.
Hardware Impact: Editor-only report write. No runtime cost.

## Decision 19 - Global Parser Purge Is Still Incomplete

Problem: `H8StaticDataArena` release branch is statically clean, but the master prompt requires hunting all runtime CSV/JSON/text parser routes. A Data Monolith-only scan is insufficient.
Solution: Run an `rg` candidate scan across 1731 non-editor C# files and save `Docs/Reports/DATA_MONOLITH_RELEASE_ROUTE_SCAN_1313.json/.md`. It reports 281 production candidates. Some are false positives by token model, but enough are real cold CSV/JSON loaders to reject any "production parser purge complete" claim.
Rejected Alternatives: Claiming release purity from the loader branch alone would be false. Editing 281 cross-domain candidates blind would violate domain boundary and likely break other agents' systems.
Scalability potential: This candidate list is the migration queue for future monolith sections. Low tier benefits only after those domains read binary DTOs instead of cold CSV/JSON.
Hardware Impact: No measured runtime gain from the scan. It prevents fake release-readiness reporting.

## Decision 20 - Native Dump Must Not Burn Stack

Problem: The Windows native dump route avoided managed allocation, but its first implementation serialized the whole 300-entry telemetry payload into one large stack buffer before writing.
Solution: Keep the native `WriteFile` route, but serialize a 20-byte header and then each 64-byte `H8DataMonolithTelemetryEntry` through a reusable stack buffer.
Rejected Alternatives: Reverting to `FileStream/BinaryWriter` would reintroduce managed release residue. Keeping the large stack buffer was unnecessary pressure in a fatal path.
Scalability potential: Low/Middle/High/Ultra all get the same bounded fatal dump path; quality weight does not affect crash evidence layout.
Hardware Impact: Expected gain is reduced fatal-path stack pressure, not steady-frame speed. No microsecond runtime saving is claimed.

## Decision 21 - Boot Marker NativeArray Was Real Residue

Problem: `GameBootstrapper.WriteBootStateRecord` used `new NativeArray<byte>(BootStateRecordBytes, Allocator.Temp)` for a fixed 32-byte boot marker. That was small, but it violated the user's NativeArray residue audit.
Solution: Replace the temporary `NativeArray` with `stackalloc byte[BootStateRecordBytes]`, zero it with `UnsafeUtility.MemClear`, and pass the pointer directly into the existing `AsyncWriteManager.WriteAll` request.
Rejected Alternatives: Leaving it because it is only 32 bytes would be a false "small allocation is acceptable" exception. Reworking all bootstrap logging in this pass would exceed 1313 ownership.
Scalability potential: All tiers get the same deterministic fixed-size boot marker; no binary quality switch or gameplay truth change.
Hardware Impact: Removes one fixed Temp NativeArray allocation per boot marker write. Exact microseconds are unmeasured because build/player boot was not run.

## Decision 22 - Fatal Boot NativeArray Residual Identified

Problem: Static scan still finds `GameBootstrapper.cs:5387` allocating `NativeArray<byte>` for fatal boot crash payload staging.
Solution: Record it as a real residual blocker instead of silently ignoring it. Superseded by Decision 24, which removed the allocation through stack staging.
Rejected Alternatives: Claiming zero NativeArray residue would be false. Blindly replacing the crash writer risks corrupting a cross-domain black-box route while other agents are editing bootstrap.
Scalability potential: Low tier needs minimal fatal dump overhead; Middle/High/Ultra can emit richer diagnostics through the same native writer without changing hot read accessors.
Hardware Impact: This was a fatal-path allocation finding. After Decision 24, the specific Temp NativeArray residue is cleared.

## Decision 23 - AUP Token Finding Belongs To Voxel/Bootstrap

Problem: The AUP scan found `new float3(rayOrigin.x, rayOrigin.y, rayOrigin.z)` in `GameBootstrapper.cs:4566`, inside a VoxelSonar ground-ready helper. Data Monolith itself does not compute distances, forces, collisions, or cast static absolute coordinates to float.
Solution: Report the line as a cross-domain AUP token finding while keeping the 1313 Data Monolith claim narrow: static payload storage has double/long AUP fields aligned; hydration performs no spatial math. Superseded by Decision 25, which removed the `new float3` token.
Rejected Alternatives: Editing the VoxelSonar helper without owning its coordinate contract could break another agent's domain. Ignoring the line would hide a possible Quest precision defect.
Scalability potential: Correct AUP handling must scale from toaster hardware to ultra without changing truth ownership; local float vectors should be derived after double-origin subtraction.
Hardware Impact: Data Monolith has no measured impact here. The token is now cleared; semantic AUP ownership remains with Voxel/Bootstrap if absolute coordinates are ever introduced.

## Decision 24 - Fatal Boot NativeArray Cleared

Problem: `GameBootstrapper.WriteFatalBootstrapLog` still allocated `NativeArray<byte>` for fatal crash text staging and also depended on a static `new UTF8Encoding(false)` plus `Substring` truncation.
Solution: Replace the Temp `NativeArray<byte>`, `UTF8Encoding`, and `Substring` truncation with a direct fixed ASCII copy into `stackalloc byte[byteCount]`, then pass that pointer to `AsyncWriteManager.WriteAll`.
Rejected Alternatives: Leaving it because the fatal path is cold would be a policy exception. Replacing the whole bootstrap persistent path policy would exceed 1313's domain and risk unrelated systems.
Scalability potential: Low/Middle/High/Ultra now share a fixed stack-staged fatal text write path. Quality weight does not affect crash evidence.
Hardware Impact: Removes one Temp NativeArray allocation, one static encoding object dependency, and crash-log substring allocation from fatal logging. Exact microseconds are unmeasured because player boot/build was not run.

## Decision 25 - AUP Token Cleared Without Claiming Ownership

Problem: `GameBootstrapper.cs:4566` used `new float3(...)`, which polluted the runtime text scan and looked like direct AUP-to-float risk.
Solution: Replace it with `math.float3(...)` and record the actual contract: `IVoxelSonarSdfReadModel` expects `runtimeOrigin float3`; Data Monolith itself performs no spatial math and stores AUP fields as double/long DTO data.
Rejected Alternatives: Rewriting the VoxelSonar read model from Data Monolith scope would violate domain ownership. Leaving the `new float3` token would keep a false-positive in the audit.
Scalability potential: Correct owners still must apply `localDouble = objectAupDouble3 - originAupDouble3; localFloat = (float3)localDouble` before calling runtime float SDF APIs.
Hardware Impact: Token cleanup only. No runtime math change and no measured performance gain.

## Decision 26 - Global Route Triage Instead Of Blind Cross-Domain Edits

Problem: The global scan reported 281 production candidates, but the raw number mixed real parser routes with CSV state counters and UI labels.
Solution: Generate `DATA_MONOLITH_RELEASE_ROUTE_TRIAGE_1313.md/.json`: 262 strict blockers remain, split into 121 CSV method declarations, 100 CSV invocations, and 41 managed file/json/split operations. The remaining 19 entries are CSV-state/UI/noise and still need cleanup but are not direct parser execution proof.
Rejected Alternatives: Editing 262 cross-domain routes from 1313 would violate domain ownership and collide with active agents. Treating all 281 as identical would blur the actual release blockers.
Scalability potential: The triage is the migration queue for moving each domain's static truth into monolith sections without changing quality-tier authority.
Hardware Impact: No runtime gain measured. It identifies the cold-boot/text-parser work still blocking low-end hardware.

## Decision 27 - Scanner Verb Coverage Must Be Generic

Problem: Scanner rules recognized narrow names such as `LoadProfilesCsv`, `LoadFaultProfilesCsv`, and `LoadAestheticProfilesCsv`, but could miss routes named `LoadQualityProfilesCsv`, `LoadLightingProfilesCsv`, or other `Load*Csv` variants.
Solution: Patch both `OOP_StaticData_Scanner` and `H8DataMonolithReleaseBuildGate` to detect callable routes through generic `Csv + parser verb` matching: `Parse`, `TryApply`, `TryIngest`, `TryLoad`, `TryReload`, `Reload`, or `Load`.
Rejected Alternatives: Adding more one-off names would keep creating gaps. Matching non-callable CSV state fields as hard parser routes would inflate false positives.
Scalability potential: One scanner rule now covers future domains and table names without new hard-coded route strings.
Hardware Impact: Editor/build-gate only. No player cost. It prevents release builds from slipping through because a route name used a new CSV suffix.

## Decision 28 - Fatal Log Must Be Fixed Marker Only

Problem: After removing `NativeArray` and `UTF8Encoding`, `WriteFatalBootstrapLog` still accepted an arbitrary `string` and kept generic truncation bounds through `FatalBootCrashLogBufferBytes`; a later scan also flagged `FatalBootCrashMessage.Length`.
Solution: Remove the parameter, the 24KB bound, and runtime string length usage. The method now writes only fixed 66-byte `FatalBootCrashMessage`, with a stack buffer sized by `FatalBootCrashMessageByteCount`.
Rejected Alternatives: Keeping a generic fatal text writer inside the audited path would invite future managed string formatting and large stack buffers. Depending on `.Length` was correct but left scan ambiguity. Replacing the wider persistent path policy is outside 1313 scope.
Scalability potential: All tiers receive identical minimal boot-fatal evidence. Rich diagnostics must go through a separate owner-approved black-box path, not this marker.
Hardware Impact: Removes generic fatal-message branching, the oversized bound, and runtime string length lookup from the marker path. Exact microseconds are unmeasured because player boot/build was not run.

## Decision 29 - Fatal Marker Must Not Pin Managed Text

Problem: The fixed marker still pinned `FatalBootCrashMessage` through `fixed (char*)`. It was not a heap allocation, but it kept a managed string surface in the audited crash writer and depended on a manually synchronized byte count.
Solution: Remove the log-message string from the writer and emit the 66-byte ASCII marker through direct byte assignments into the existing stack buffer.
Rejected Alternatives: Keeping the pinned string because the path is cold would preserve a managed text dependency in a file changed for zero-GC audit. Replacing `HectonPersistentPathPolicy` and `AsyncWriteManager.WriteAll(string, ...)` is a broader bootstrap persistence PAL migration and outside the 1313 hydrator route.
Scalability potential: Low/Middle/High/Ultra all get the same fixed fatal marker bytes. Rich diagnostics stay outside this marker and must use a separate black-box owner.
Hardware Impact: Removes the managed string pin/copy loop from the fatal marker path. Exact microseconds are unmeasured because player boot/build was not run.

## Decision 30 - Strict Recheck Report Is Rejection Evidence

Problem: The user demanded byte-level proof, while the current state still has real blockers. A chat-only statement would be unauditable.
Solution: Write `DATA_MONOLITH_APEX_STRICT_RECHECK_1313.md/.json` with active release scan results, DTO layout counts, parser blocker counts, bootstrap residual managed path policy lines, and explicit no-build evidence.
Rejected Alternatives: Inflating the report into a fake pass would hide the Quest/non-Windows fail-closed route and 262 cross-domain parser blockers. Running dotnet/Unity build would violate the repeated user restriction.
Scalability potential: The report keeps the migration queue exact across weak, middle, high, and ultra tiers; quality weight does not change static truth layout.
Hardware Impact: No runtime gain. The report prevents false release acceptance and identifies the remaining work that blocks low-end cold-boot savings.

## Decision 31 - Quest Loader Must Stay Rejected Without Native PAL

Problem: Android/Quest `StreamingAssets` inside the APK cannot be read through the existing Windows `CreateFileW` path, and the repository has no Data Monolith native asset-manager bridge.
Solution: Keep Android/non-Windows release fail-closed and write `DATA_MONOLITH_ANDROID_PAL_REJECTION_1313.md/.json` with source proof: no `AAssetManager` hits, Android plugin folder has manifest/gradle only, native plugin matrix requires Android LZ4 only, and the only current custom native bridge is audio-owned/standalone-guarded.
Rejected Alternatives: `UnityWebRequest` reintroduces managed URI staging; `AndroidJavaObject` creates managed JNI wrapper objects; reusing `HectonSensoryKernel` would be a horizontal audio-domain dependency and has no `static_data.h8bin` read export.
Scalability potential: Low/Middle/High/Ultra must share one binary static-truth route. Platform PAL differences may change file access only; they must not change DTO layout, BufferID authority, or GlobalQualityWeight behavior.
Hardware Impact: No runtime saving from this pass. The value is preventing a false Quest-ready claim; the measurable low-end gain requires a real Android arm64 native asset read bridge that writes directly into the `GlobalDataVault` arena.

## Decision 32 - Evidence Scanner Must Not Have Narrow Fallback Gaps

Problem: The Roslyn scanner already caught generic callable `Csv + parser verb` routes, but its text fallback for syntax-broken files only detected `TryLoad + Csv`. The stress proof also wrote `directory.SectionTableBytes` through a misleading field named `DirectoryBytesValue`.
Solution: Reuse `IsCsvRouteName(source)` in `OOP_StaticData_Scanner.ScanTextFallback` and rename the stress snapshot field to `SectionTableBytes`.
Rejected Alternatives: Keeping a weaker fallback would let syntax-broken files with `LoadQualityProfilesCsv` or `ParseLightingCsv` escape the 1313 scanner. Renaming the JSON key would churn report consumers; only the internal field name was wrong.
Scalability potential: Low/Middle/High/Ultra all depend on the same release gate. Stronger scanner evidence blocks text-parser regressions without changing runtime DTO truth or quality scaling.
Hardware Impact: Editor/report-only. No player frame gain. It prevents false release acceptance for broken-source fallback paths.

## Decision 33 - Release Gate Must Block Unsupported Monolith Platforms

Problem: The runtime loader is static-token clean on Windows release but non-Windows production branches fail closed. Before this pass, the release gate only blocked parser/file-I/O residue and could still report a clean parser gate for Android/Quest even though no zero-GC native/PAL monolith loader exists.
Solution: Pass `BuildTarget` into `H8DataMonolithReleaseParserScanner`, record target PAL status in the JSON report, inject `unsupportedStaticDataMonolithPlatformPal` as a blocking finding for non-development targets outside `StandaloneWindows` and `StandaloneWindows64`, and evaluate Unity platform preprocessor symbols from the same target.
Rejected Alternatives: Adding a fake Android `DllImport` without a compiled `.so` would create a runtime entrypoint failure. Re-enabling `UnityWebRequest` or `AndroidJavaObject` would violate Zero-GC runtime ingestion. Leaving the gate unchanged would allow false release readiness.
Scalability potential: Low/Middle/High/Ultra keep one static truth contract. Platform PAL differences may only affect file access; they must not change DTO layout, BufferID ownership, or GlobalQualityWeight behavior.
Hardware Impact: Editor/build-gate only. No player runtime gain. It prevents Quest/Android release artifacts from shipping without a real native asset-loader bridge.

## Decision 34 - Batch Audit Must Execute The Release Gate

Problem: `H8DataMonolithBatchAudit.RunFromCommandLine` baked, validated, fuzzed, and ran the OOP scanner, but it did not run `H8DataMonolithReleaseParserScanner`. A CLI audit could therefore return exit code 0 while the real release build gate would reject the same target for parser/file/PAL blockers.
Solution: Call `H8DataMonolithReleaseParserScanner.Scan(writeReport: true, blockOnFindings: false, developmentBuild: false, target: EditorUserBuildSettings.activeBuildTarget)` inside batch audit, log `releaseGate.BlockingFindingCount`, and require `releaseGateClean` for batch-mode exit 0.
Rejected Alternatives: Duplicating release-gate rules in batch audit would create two owners for the same policy. Leaving the CLI audit weaker than build preprocessing would allow false CI evidence.
Scalability potential: Low/Middle/High/Ultra keep one release acceptance policy. Platform-specific PAL support can only change the file-access adapter, not static truth layout or quality ownership.
Hardware Impact: Editor/CI-only. No player runtime gain. It prevents unsupported Android/Quest or parser-residue release artifacts from being accepted by the batch audit.

## Decision 35 - H8ItemRecord Can Be Reordered, Header Cannot

Problem: The strict byte-order audit still flagged `H8ItemRecord` because two `ulong` recipe masks started at offsets 16/24 after 4-byte fields. Unlike `H8DataBlobHeader`, this record is consumed through named fields and a fixed 80-byte stride, so the field offsets can be changed if the binary schema version changes.
Solution: Move `RecipeMask0/RecipeMask1` to offsets 0/8, move 4-byte fields to offsets 16-72, keep `MaxStack/RecipeIngredientCount` at offsets 76/78, update `H8DataMonolithLayoutGuard`, bump `FormatVersion` to 2, and bump `SchemaHash` to `0x33313331` so stale blobs fail closed.
Rejected Alternatives: Reordering `H8DataBlobHeader` would violate the file contract that starts with `Magic`, version, and header bytes. Reordering `H8ItemRecord` without version/schema change would allow old blobs to be misread.
Scalability potential: All tiers keep the same 80-byte item stride and named-field API. The schema change only affects offline bake/runtime validation, not GlobalQualityWeight or gameplay truth.
Hardware Impact: Runtime steady-state gain is not measured. The practical gain is ARM64-friendly item record ordering and earlier rejection of stale binary payloads.

## Decision 36 - Layout Guard Must Validate Every DTO Field

Problem: `H8DataMonolithLayoutGuard` still used spot checks plus `ValidateBlittableSizes`, so a future DTO could add a `bool`, omit `FieldOffset`, overlap bytes, or rely on hidden padding while still escaping the exact checks.
Solution: Add `ExpectAllDeclaredLayouts()` and `ExpectDeclaredLayout<T>()` for all 32 Data Monolith DTO structs. The guard now requires explicit layout, exact `UnsafeUtility.SizeOf<T>()`, 8-byte size multiple, declared `FieldOffsetAttribute`, actual `UnsafeUtility.GetFieldOffset` equality, natural field alignment, no overlaps, no undeclared holes, no `bool`, and no managed refs/string.
Rejected Alternatives: Keeping the old spot-check guard was too weak for Task 09. Using `Marshal.SizeOf` as primary proof was rejected because the mandate names `UnsafeUtility.SizeOf` and `UnsafeUtility.GetFieldOffset`.
Scalability potential: Low tier avoids silent ARM64 layout drift and stale binary poison; Middle/High/Ultra can add more static data sections without weakening DTO invariants. Quality weight cannot affect DTO layout or authority route.
Hardware Impact: Editor-only validation cost. Runtime frame cost is 0 us. It prevents low-end ARM64 misalignment and hidden padding defects before a bad blob reaches Quest-class hardware.

## Decision 37 - Checked-In Blob Must Match Schema V2

Problem: `H8ItemRecord` was reordered to ARM64-friendly v2 offsets, but the checked-in `static_data.h8bin` still contained format `1`, schema `0x58303032`, and v1 item record byte order. Source-only schema migration would make runtime validation correctly fail closed.
Solution: Migrate the checked-in binary in place: update header and directory format to `2`, update schema hash to `0x33313331`, remap every item record from v1 field offsets to v2 offsets, and recompute XXH3 checksum over `bytes[64..end)` to `0x19D880780D6E1B46`.
Rejected Alternatives: Leaving the blob stale would keep the task objectively incomplete. Running Unity bake/build for this narrow ABI migration violates the user's repeated build restriction and is unnecessary when the record transform is byte-exact and revalidated.
Scalability potential: Low/Middle/High/Ultra now consume the same static v2 payload contract. GlobalQualityWeight still does not affect DTO layout, save identity, or authority route.
Hardware Impact: No frame-time gain claimed. It removes a cold-boot fail-closed blocker caused by stale binary layout, enabling the v2 item record path to be tested without forcing an editor rebake.

## Decision 38 - External Validator Contract Was Stale

Problem: `Tools/h8bin_validator.py` failed the migrated blob because it expected `DataStartOffset == SectionTableOffset + SectionTableBytes` and treated `BlobFlagLittleEndian` as an unsupported directory flag. The compiler contract already writes `DataStartOffset = AlignUp(tableEnd, SectionAlignmentBytes)` and sets `BlobFlagLittleEndian`.
Solution: Patch the validator to use `align_up(sectionTableOffset + sectionTableBytes, sectionAlignment)` and to allow `RleDirectoryFlag | BlobFlagLittleEndian`. Rerun the validator against the active blob; result: `PASS`, 1 file, 32 parsed structs, 26 sections, 3339 sampled records.
Rejected Alternatives: Documenting the validator failure as a blob defect would be false evidence. Removing the directory flag would weaken endian proof and contradict the runtime/compiler schema.
Scalability potential: The CI validator now matches the compiler's 64-byte alignment rule and endian flag across weak, middle, high, and ultra targets without changing runtime truth.
Hardware Impact: Tooling-only. No player runtime cost. It prevents a valid aligned payload from being rejected before reaching hardware tests.

## Decision 39 - Validator Report Identity Must Be Explicit

Problem: `DATA_MONOLITH_H8BIN_VALIDATOR_1313.json` was generated for 1313 but the Python validator hard-coded `agent_id = SHINOBU_358`, making the proof artifact ambiguous.
Solution: Add `--agent-id` to `Tools/h8bin_validator.py`, stamp the top-level report, self-audit, metrics log, and Metric Phi row with the requested report agent, while preserving `tool_origin_agent_id = SHINOBU_358` and `legacy_tool_owner = SHINOBU_258`.
Rejected Alternatives: Manually editing the JSON after every run would be fake tooling. Replacing the historical tool-owner fields would destroy provenance.
Scalability potential: Any future agent can reuse the same validator without fabricating ownership or losing tool lineage; runtime DTO truth remains unchanged.
Hardware Impact: Tooling-only. No player runtime cost. It removes a proof-chain ambiguity that could hide stale or misattributed validation.

## Decision 40 - Active Release Proof Must Not Hide Editor Residue

Problem: Source-wide grep still reports managed APIs in `H8StaticDataArena`, while the active release branches show zero forbidden tokens. Reporting only one side either hides editor/development residue or falsely marks the release loader dirty.
Solution: Write `DATA_MONOLITH_APEX_PARANOID_PASS2_1313.md/.json` with both facts: active Windows/non-Windows release token hits are 0 in the owned loader slices, and all-branch managed residue remains at exact lines `173/175/199/201/202/205/206/217/231/249/345/1481/1482/1500/1595/2202/2203/2204/2205/2207/2220/2221`.
Rejected Alternatives: Source-wide panic would mislabel guarded editor/dev code as release-active. Active-only reporting would hide remaining managed surfaces that must stay fenced and watched.
Scalability potential: Low tier gets a clean Windows release branch now; Middle/High/Ultra keep editor diagnostics and richer bake tooling outside production truth. Non-Windows tiers remain blocked until a native/PAL loader exists.
Hardware Impact: Report-only. No runtime gain. The practical effect is preventing a false Quest/Android release claim while preserving exact Windows static-token evidence.

## Decision 41 - Read Accessors Must Use Read-Only Vault Handles

Problem: Public Data Monolith read accessors resolved the resident blob through `IDataVault.TryResolveHandle`, which is a mutable current-phase view. The project doctrine requires read accessors to be pure and consumers to read immutable snapshots or cached interfaces.
Solution: Add `TryRefreshArenaReadOnly(out NativeArray<byte>.ReadOnly)` using `IDataVault.TryReadOnlyHandle`, and route section lookup, span creation, localization reads, resident hash, validation, and arena export through that helper. Keep mutable `TryRefreshArenaView` only for cold/hydration writes into the arena.
Rejected Alternatives: Leaving readers on mutable handles would preserve a hidden authority leak. Changing `GetSectionDataPointer` to a different return type would break existing consumers and C# cannot encode const unmanaged pointers; the current fix removes mutable vault resolution from the accessor route while preserving ABI.
Scalability potential: Low/Middle/High/Ultra tiers now consume static truth through the same read-only DataVault route. Quality weight still cannot alter DTO layout, identity, or authority.
Hardware Impact: No measured frame gain. It removes mutable-handle exposure from hot static-data reads and reduces accidental write risk on low-end ARM64 devices.

## Decision 42 - Public Pointer Surface Must Be Quarantined

Problem: `GetSectionDataPointer` no longer used a mutable vault handle, but its public `void*` signature still allowed external domains to treat Data Monolith memory as writable. It also forced double arena resolution in span/pointer lookups.
Solution: Migrate all external biome consumers to `GetSectionSpan<H8BiomeRecord>`, make `GetSectionDataPointer` private, and add `TryGetSectionFromArena` so `TryGetSectionSpan` and the private pointer path resolve the read-only arena once per call.
Rejected Alternatives: Keeping a public `void*` as "read-only by convention" is not an unmanaged contract. Replacing it with a new public pointer API still cannot express constness in C# and would not remove the authority leak.
Scalability potential: Weak devices avoid repeated vault handle resolution on hot static-data reads; higher tiers keep the same immutable static truth route without extra authority surfaces.
Hardware Impact: No measured microsecond gain because Unity/player profiling was not run. Static effect: one DataVault read-only handle resolution instead of two for `TryGetSectionSpan` and no external writable pointer surface.

## Decision 43 - Normal Section Reads Must Use One Span Contract

Problem: After quarantining the public pointer API, `H8StaticDataArena` still had a private `GetSectionDataPointer` helper used by normal query methods. That preserved two section-read contracts: pointer and span.
Solution: Remove `GetSectionDataPointer` completely and convert normal record queries, localization reference scanning, and loot range lookup to `ReadOnlySpan<T>` from `GetSectionSpan<T>`. `TryResolveLootItem` now resolves the loot span once and passes it into the range helper. Keep only the private Burst pointer helper for item hash binary search, with the public entrypoint remaining the span overload.
Rejected Alternatives: Leaving a private pointer helper was smaller but weaker; the point of the pass is to collapse static-data reads onto a single immutable span contract. Removing the Burst helper would be unnecessary churn because it does not resolve DataVault memory and is private.
Scalability potential: Weak devices get one read abstraction and fewer pointer aliases; higher tiers can add more Data Monolith sections without reintroducing public or private section pointer helpers.
Hardware Impact: No measured player gain. Static cost reduction is less aliasing and direct span indexing for normal read paths; profiling still required before claiming microseconds.

## Decision 44 - Runtime Localization Decode Must Not Use Managed Encoding

Problem: `H8StaticDataArena.TryReadLocalizedText` still used `Encoding.UTF8.GetCharCount` and `Encoding.UTF8.GetChars` in active runtime read accessors. These calls use caller-owned spans and do not instantiate strings, but they preserve a managed codec surface in the release static-data accessor path.
Solution: Replace both runtime calls with `TryDecodeUtf8(ReadOnlySpan<byte>, Span<char>, out int)`, a manual UTF-8 decoder that writes directly to caller-owned char storage and rejects truncated sequences, invalid continuations, overlong encodings, surrogate scalar input, and values above `U+10FFFF`. Guard `using System.Text` behind editor/development because the remaining `Encoding.UTF8` use is editor dump output only.
Rejected Alternatives: Keeping `Encoding.UTF8` because it is not a string allocation would weaken the Zero-GC audit. Using `Encoding.GetString` would allocate a managed string and is categorically invalid. Replacing the entire localization storage format in this pass would churn the ABI without measured need.
Scalability potential: Low/Middle/High/Ultra all read the same localization byte block and decode only into caller-owned buffers. Quality weight does not alter localization offsets, DTO layout, or static truth ownership.
Hardware Impact: No player profiler run, so no microsecond claim. Static effect: active release token scan now has 0 hits for `Encoding.UTF8`, `GetCharCount`, and `GetChars`; the runtime accessor no longer depends on the managed UTF-8 codec path.

## Decision 45 - Validator Must Respect Release Preprocessor Fences

Problem: `h8bin_validator.py` flagged editor-only CSV loaders as release blockers because it scanned raw text without preprocessor activity. That created false evidence and hid the real remaining artifact problem.
Solution: Add a release preprocessor mask for `#if/#elif/#else/#endif` that treats `UNITY_EDITOR` and `DEVELOPMENT_BUILD` as false and checks whether other platform symbols can be active in any player release before scanning StreamingAssets text-loader lines.
Rejected Alternatives: Whitelisting the four file paths would rot immediately and would not prove future editor-only facades. Leaving the false positives would keep the release gate noisy and untrustworthy.
Scalability potential: Low/Middle/High/Ultra use one validator rule. Editor authoring bridges remain allowed; player-release text loaders stay blocked.
Hardware Impact: Tooling-only. No runtime microseconds claimed. It removes false blocker noise and makes the real release payload gate deterministic.

## Decision 46 - Human CSV Must Leave StreamingAssets, Not Be Deleted

Problem: Three designer CSV files lived under `Assets/StreamingAssets`, so the strict release artifact gate correctly failed even after active loader false positives were removed.
Solution: Move camera, haptic, and PDA CSV authoring sources with `.meta` files into `Assets/_Project/Data/{VFX,Haptics,UI}` and update editor-only facades to read those paths. Fence two ecosystem helper methods so release-active source cannot expose StreamingAssets CSV paths.
Rejected Alternatives: Deleting the CSVs would destroy designer source data. Keeping them in StreamingAssets would ship text payloads in release. Baking them into Data Monolith in this pass would cross several domain DTO contracts without owner review.
Scalability potential: Low tier boots from binary payloads only; Middle/High/Ultra retain editable authoring CSVs outside runtime payload ownership. GlobalQualityWeight cannot alter static data authority or file layout.
Hardware Impact: No player profiler run. Static release payload reduction is 736 bytes of CSV plus three `.meta` files removed from StreamingAssets; cold parser risk from these artifacts is eliminated.

## Decision 47 - Pass 8 Must Separate Clean Loader Proof From Release Readiness

Problem: The user demanded another paranoid audit after repeated prompt rereads, but a clean Windows source scan can be falsely inflated into a release-ready claim while Android/Quest still lacks a native/PAL monolith loader and no Unity player profiler proof exists.
Solution: Write `DATA_MONOLITH_APEX_PARANOID_PASS8_1313.md/.json`, rerun the strict `h8bin_validator` as `DATA_MONOLITH_H8BIN_VALIDATOR_RELEASE_BLOCKERS_PASS8_1313.json`, and record the exact split: `H8StaticDataArena` active release forbidden-token hits are 0 for Windows and Android scan models, StreamingAssets text artifacts are 0, `static_data.h8bin` is v2 and validator-clean, but Android/Quest release remains fail-closed because there is no `AAssetManager`/native plugin bridge.
Rejected Alternatives: Implementing a fake Android `DllImport` or managed `UnityWebRequest` fallback would create a false loader. Running dotnet/Unity build for another static-report pass violates the user's explicit build restriction. Hiding cross-domain parser blockers behind the clean Data Monolith loader would be false evidence.
Scalability potential: Low/Middle/High/Ultra keep one static truth payload and one release gate; platform PAL can only change file access mechanics, not DTO layout, BufferID authority, or GlobalQualityWeight behavior.
Hardware Impact: No new runtime microseconds claimed. Static proof prevents text artifacts from shipping in `StreamingAssets`; measurable low-end savings still require Android PAL plus player boot/profiler GC evidence.
