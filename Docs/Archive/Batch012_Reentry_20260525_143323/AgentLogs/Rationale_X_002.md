# X_002 Rationale - DATA_MONOLITH_ARCHITECT

Status: ACTIVE
Evidence class: CLI_BAKE_HEADER_PROOF / CLI_CORRUPTION_FUZZER / STATIC_SOURCE until Unity import/build/profiler artifacts exist.

## Decision 000 - Task Boundary

Problem: The project documentation states Data Monolith readiness requires an active `static_data.h8bin`, but assignment demands ten tasks spanning archaeology, schema, baker, loader, validators, and production fencing.
Solution: Treat `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` as the runtime artifact, keep CSV authoring editor/development-only, and route production reads through a binary schema with explicit little-endian fields.
Rejected Alternatives: Runtime text parsing is rejected because it violates zero-GC and designer bridge mandates. ScriptableObject runtime truth is rejected because it keeps mutable managed object graphs in gameplay dependency paths.
Scalability potential: Low uses compact binary tables and one validation pass. Middle keeps editor hot-reload. High keeps richer validation reports. Ultra may carry debug manifests without bloating gameplay DTOs.
Hardware Impact: i3/MX350 gain is cold-boot stability and removal of parser allocations; exact microseconds remain PENDING VERIFICATION until measured.

## Decision 001 - Mandate Selection

Problem: Data Monolith work crosses schema layout, designer authoring, vault ownership, bootstrap order, and corruption telemetry.
Solution: Load the eight mandates listed in `Status_X_002.md` before code generation.
Rejected Alternatives: Reading only generic AGENTS.md is insufficient because task-specific registry files contain ARM64 layout, CSV bridge, Vault, and blackbox constraints.
Scalability potential: Mandate-driven schema prevents low-tier stutters and keeps high-tier debug data out of gameplay truth.
Hardware Impact: Prevents misaligned ARM64 DTOs and parser heap churn on low-end silicon; exact gain PENDING VERIFICATION.

## Decision 002 - Header Contract Expansion

Problem: Existing Data Monolith header was 16 bytes and carried only magic, format, byte count, and checksum. Assignment requires a rigid 64-byte header with enough identity to reject schema drift before section access.
Solution: Expand `H8DataBlobHeader` to 64 bytes with blob size, directory range, section table range, section count, flags, world seed, app-version hash, and schema hash. Keep checksum coverage as bytes `[HeaderSizeBytes..blobLength)` and keep all fixed records 16-byte aligned.
Rejected Alternatives: Leaving identity only in the directory was rejected because header corruption could pass until later validation. Pack=1 headers were rejected because ARM64 layout must remain explicit and auditable.
Scalability potential: Low tier performs one compact header check before table reads. Middle/high/ultra can add richer diagnostics without changing record payload offsets.
Hardware Impact: i3/MX350 avoids late section validation work on corrupt blobs; expected save is tens of microseconds on bad data paths, zero measurable hot-frame cost.

## Decision 003 - Vault Ownership Route

Problem: `H8StaticDataArena` allocated Data Monolith buffers through local numeric casts and refreshed through `GlobalRegistry.DataVault`, which violated one-owner route clarity.
Solution: Move Data Monolith BufferIDs into `H8Memory.cs`, pass `_globalDataVault` explicitly from `GameBootstrapper.InitializeBootstrapDataMonolith`, and keep registry access only as a compatibility entry overload.
Rejected Alternatives: Continuing local `(BufferID)71103` casts was rejected as governance debt. Per-frame registry polling was rejected by Global Systems Doctrine.
Scalability potential: Low uses one cold Vault injection. Middle/high/ultra can add larger static payloads without changing dependency discovery.
Hardware Impact: Low-end gain is reduced cold-route ambiguity and no registry fallback in arena refresh; exact runtime microseconds are below measurement without profiler capture.

## Decision 004 - Corruption And Layout Proof

Problem: A happy-path bake does not prove the loader rejects corrupt magic, checksum, truncation, or section offset drift.
Solution: Add an editor corruption fuzzer and an InitializeOnLoad layout guard using explicit offsets and sizes for header, directory, section entries, major records, and telemetry.
Rejected Alternatives: Unit comments and manual inspector checks were rejected because they are not executable proof artifacts.
Scalability potential: Low devices benefit from fail-fast corrupt data rejection. High/ultra builds can retain richer editor diagnostics while runtime keeps fixed-size telemetry.
Hardware Impact: Editor-only validation has zero player-frame cost. Bad-data boot aborts before section scans in common header mismatch cases.

## Decision 005 - Static Parser Audit

Problem: Subagent and local scans found many runtime CSV/JSON/static profile readers outside the monolith route.
Solution: Add `OOP_StaticData_Scanner` using Roslyn AST with token fallback, reporting production parse/file/json calls to `Docs/Reports/DATA_PIPELINE_OPTIMIZATION_REPORT_X_002.json`.
Rejected Alternatives: Chat-only listing was rejected because the CTO protocol requires disk artifacts. Blanket deletion was rejected because many readers are outside the Data Monolith domain and need owner migration.
Scalability potential: Low devices need parser removal from boot/gameplay paths. Middle/high/ultra can keep editor authoring flexibility while the player consumes static binary sections.
Hardware Impact: Potential i3/MX350 gain is removal of managed file/text parse spikes; exact saving depends on owner migration of each flagged loader.

## Decision 006 - Compile Wall Handling

Problem: `dotnet build Hecton8.Core.csproj --no-restore` failed before Data Monolith code with 59 `AudioLogSystem.cs` missing symbol errors outside this domain.
Solution: Treat as dependency wall strike 1, do not edit AudioLog, and add a narrow DataMonolith bake CLI fallback that compiles only monolith compiler/types plus Unity editor stubs when dotnet is available.
Rejected Alternatives: Editing AudioLog was rejected as out-of-domain. Claiming compile success was rejected. Running another build while a foreign dotnet process is active is rejected by local CPU/compiler policy.
Scalability potential: CLI fallback can materialize static data for low-end test builds even when unrelated editor assemblies are broken; Unity editor path remains canonical when project compile is healthy.
Hardware Impact: No player cost. Tooling saves integration time but profiler microseconds remain PENDING VERIFICATION.

## Decision 007 - Isolated Bake CLI

Problem: The required `static_data.h8bin` could not wait on unrelated `AudioLogSystem.cs` compile failures, and the protocol rejects false readiness reports.
Solution: Add `Tools/DataMonolithBakeCli` to compile only Data Monolith compiler/types and minimal Unity stubs, then execute the same baker and validator against the project root.
Rejected Alternatives: Running runtime CSV conversion was rejected. Editing out-of-domain AudioLog compile errors was rejected. Declaring the Unity editor path verified was rejected because the full assembly still fails.
Scalability potential: Low devices receive the same compact binary artifact. Middle/high/ultra retain Unity editor tooling when the global compile wall clears. The CLI path is a narrow recovery lane, not a new gameplay authority.
Hardware Impact: Player-frame cost is 0 us. Tooling cost is editor/offline only. i3/MX350 benefit is removal of runtime text parse dependency for monolith-owned data.

## Decision 008 - Corruption Fuzzer Closure

Problem: A valid bake alone does not prove fail-closed behavior for corrupt payloads.
Solution: Wire `H8DataMonolithCorruptionFuzzer.Run()` into the CLI after a successful bake. The run mutates magic, checksum, truncation, and section table offset and requires each validator failure to name the expected error class.
Rejected Alternatives: Manual binary inspection was rejected because it is not repeatable. Loader fallback to generated defaults was rejected because no route card exists.
Scalability potential: Low devices reject bad data before payload traversal. Middle/high/ultra can expand the fuzzer matrix without changing runtime layout.
Hardware Impact: Editor/test only. Runtime bad-data exits earlier on header/range mismatch; exact runtime microseconds remain unprofiled.

## Decision 009 - Static Parser Ownership Boundary

Problem: Scans found production file/text/JSON readers outside Data Monolith ownership, but mass edits would cross domain boundaries and risk breaking other agents.
Solution: Produce `DATA_PIPELINE_OPTIMIZATION_REPORT_X_002.json` with concrete migration targets and keep X_002 code changes limited to Data Monolith, bootstrap injection, and central memory IDs.
Rejected Alternatives: Blanket parser deletion was rejected as architectural sabotage. Ignoring the readers was rejected because production parser debt must be visible.
Scalability potential: Low tier needs parser-free boot/gameplay routes. Middle/high/ultra can keep authoring CSV and diagnostics in editor-only lanes.
Hardware Impact: Potential i3/MX350 gain is owner-dependent; exact savings require profiling after each flagged route migrates to `static_data.h8bin`.

## Decision 010 - Re-Dispatch Inventory Correction

Problem: The current batch prompt returned and explicitly requires every CSV on disk to be analyzed. The existing core blob proves Data/Balance monolith readiness, but not full cross-domain CSV elimination.
Solution: Generate `Docs/Reports/DATA_MONOLITH_SOURCE_INVENTORY_X_002.json` with every discovered CSV classified by ownership and migration state. Downgrade status from full completion to core monolith baked plus cross-domain owner migration pending.
Rejected Alternatives: Claiming that 215 CSV files were baked was rejected as false. Absorbing cross-domain CSVs without route cards was rejected as architectural sabotage. Running another `dotnet` pass was rejected while external compiler processes exist.
Scalability potential: Low tier already receives compact binary Data/Balance truth. Middle/high/ultra can migrate remaining domain-owned CSVs into dedicated monolith sections or separate h8bin lanes without changing the current header contract.
Hardware Impact: Current baked lane has 0 us player-frame cost. Remaining root/StreamingAssets CSV risks can still produce boot I/O/parser cost until their owners migrate; exact savings require route-specific profiler proof.

## Decision 011 - Cache-Line Section Alignment And Expanded Fuzzer

Problem: The T.A.R.S. override demanded mathematical stress proof for corrupt XXHash3, bad offsets into void, and cache-line section alignment. The existing bake used 16-byte section starts and the fuzzer only proved 4 cases.
Solution: Change `H8DataLayoutConstants.SectionAlignmentBytes` to 64 and add `RecordAlignmentBytes=16` so sections begin on cache-line boundaries while DTO records remain ARM64-aligned. Expand the corruption fuzzer to 12 cases: bad magic, bad stored checksum, bad payload checksum, truncation, section-table offset drift, directory magic, directory identity, data-start offset drift, section record-size drift, unaligned section offset, out-of-bounds section range, and localization-directory mismatch. Rebuild and rerun the narrow CLI.
Rejected Alternatives: Padding every record to 64 bytes was rejected because it would bloat hot tables without improving pointer legality. Trusting the happy path was rejected because checksum/offset corruption must be executable proof, not comments.
Scalability potential: Low tier rejects damaged data before AI/physics reads any table. Middle/high/ultra can add more sections without changing the 64-byte start contract; saved runtime parser cost can buy richer static tables.
Hardware Impact: i3/MX350 avoids late undefined table traversal on corrupt files. Bad hash exits at checksum validation; bad offsets exit in section-table validation. Exact player boot microseconds remain pending Unity profiler, but the CLI fuzzer proves fail-closed status for 12/12 corruption paths.

## Decision 012 - Parser Isolation Boundary

Problem: The user demanded proof that release builds contain no CSV text parsing or `FileStream.ReadByte` config path. X_002 can only own Data Monolith code; the wider project still has non-Editor parsers in other domains.
Solution: Produce two reports. `DATA_MONOLITH_LOCAL_PARSER_ISOLATION_X_002.json` proves Data Monolith runtime has 0 CSV/text parser matches and 0 `FileStream.ReadByte` matches, with CSV compiler files fenced by `#if UNITY_EDITOR` and an Editor-only asmdef. `DATA_MONOLITH_RELEASE_PARSER_ISOLATION_X_002.json` remains a global fail report because other domains still contain production parser/file IO hits.
Rejected Alternatives: Blanket edits across Atmosphere, AI, Construction, Combat, Fauna, and other owners were rejected as domain sabotage. Claiming global release purity was rejected because static evidence says otherwise.
Scalability potential: Low tier benefits immediately for Data Monolith-owned static truth. Middle/high/ultra can migrate remaining owner routes into h8bin lanes or domain-specific binaries without weakening this header contract.
Hardware Impact: Data Monolith-owned CSV parser cost is removed from player runtime. Global i3/MX350 savings remain owner-dependent until the 1047 broad parser/file hits are classified and migrated.

## Decision 013 - Resident Load Stress Boundary

Problem: The user demanded proof that corrupt XXHash3 and section offsets fail closed, and also demanded fractions-of-millisecond zero-allocation loading into GlobalDataVault. A single happy-path bake cannot prove either claim, and managed file IO cannot be called zero-GC.
Solution: Add `Tools/DataMonolithBakeCli/DataMonolithLoadStressProbe.cs`. The Release CLI stress path reads the blob once, copies it into a 64-byte aligned unmanaged resident pointer, validates header/directory/section ranges and XXHash3 for 1024 iterations, then mutates checksum and first-section offset to prove failure codes `BadChecksum` and `SectionOutOfRange`. The report separates managed file-read allocation from resident copy+validation.
Rejected Alternatives: Claiming the existing `FileStream`/MMF path was zero-GC was rejected because the measured file staging path allocated `1064664` managed bytes. Faking a GlobalDataVault profiler claim from a CLI pointer test was rejected because the real Unity player/Vault instance was not profiled. Removing checksum validation for speed was rejected because corrupt data must fail before AI/physics table consumers run.
Scalability potential: Low tier can boot from a compact validated blob and abort corrupt data before simulation systems read undefined memory. Middle/high/ultra can add sections while keeping the 64-byte section alignment and the same fail-fast checksum contract.
Hardware Impact: Release CLI measured resident native copy `211.200 us`, full resident validation mean `385.379 us`, copy+validate estimate `596.579 us`, and resident heap `0 bytes` for the current `1064384` byte blob. Managed file read measured `169.400 us` and `1064664` allocated bytes; real Unity player/GlobalDataVault zero-heap proof remains pending.

## Decision 014 - Native File Read Before Managed Fallback

Problem: The resident pointer stress pass still depended on a managed `File.ReadAllBytes` staging lane for the real file-read comparison, which left a measured `1064664` byte heap allocation and did not satisfy the zero-heap boot demand.
Solution: Add a Windows native read path to `H8StaticDataArena.TryReadWholeFileIntoArena`: `CreateFileW`/`ReadFile` copies directly into the existing Vault-backed arena before MMF/FileStream fallback. Extend the CLI stress probe with the same native read path and target-time field.
Rejected Alternatives: Removing MMF/FileStream fallback was rejected because non-Windows/player platform behavior still needs a safe fallback until platform-specific native readers exist. Claiming managed staging was zero-GC was rejected by measurement. Shrinking `BiomeHeatmap` to fake faster hashes was rejected because 256x256 consumers exist in voxel, encounter, boundary, and GPU scatter paths.
Scalability potential: Low tier gets the native zero-heap load path for the current 1 MB blob; middle/high/ultra retain the same binary contract and can add platform native readers without changing section layout.
Hardware Impact: Release CLI measured native read `194.900 us`, full validation mean `523.373 us`, native read+validate estimate `718.273 us`, and native resident allocated bytes `0`. Managed comparison path remains `1064664` allocated bytes and is now a fallback/comparison, not the first Windows route.

## Decision 015 - Release Parser Build Gate

Problem: Static reports proved Data Monolith-owned parser isolation, but they did not physically prevent a non-development player build from shipping while other domains still compile CSV/text parser routes and `ReadByte` file paths.
Solution: Add `H8DataMonolithReleaseBuildGate` as an Editor-only `IPreprocessBuildWithReport` gate after the Data Monolith bake preprocessor. The gate models release preprocessor symbols, skips Editor/Test files, writes `Docs/Reports/DATA_MONOLITH_RELEASE_BUILD_GATE_X_002.json`, and throws `BuildFailedException` for non-development builds when release-active parser routes remain.
Rejected Alternatives: Editing 722 findings across Atmosphere, AI, Core, Save, UI, VFX, and other domains was rejected as cross-domain sabotage without owner route cards. Keeping scanner-only evidence was rejected because it permits accidental release packaging. Blocking development builds was rejected because the assignment permits editor/development CSV bridges for iteration.
Scalability potential: Low devices get a hard release guard against parser heap spikes and byte-at-a-time file IO. Middle/high/ultra retain authoring flexibility in Editor/Development lanes while production consumes binary h8bin routes.
Hardware Impact: Player-frame cost is `0 us`; the gate is editor/prebuild only. Initial parity report found `722` blocking release findings; after the fix slices the current parity recount finds `521` and `0` release-active FileStream ReadByte findings. The immediate hardware gain is prevention of an invalid build, not a measured runtime speedup. Full compile was not launched because compiler/CPU gates were active during the relevant verification windows.

## Decision 016 - Release Parser Fix Slice

Problem: The build gate was correctly blocking release, but it still reported X_002-owned or adjacent CSV override routes as release-active: fabrication timing CSV import, Apex brain CSV overrides, toxic outgassing chemistry overrides, cartography scanner CSV import, memory_overrides.csv boot/polling, and SystemDispatcher CSV path literals. The user explicitly ordered fixes, not another passive report.
Solution: Fence those routes under `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, including public bridge methods and private parser helpers. Replace the byte-at-a-time file reads in Apex brain and toxic outgassing with bulk `Span<byte>` reads. Keep the gate installed and refresh the parity recount. Add `DATA_MONOLITH_RELEASE_PARSER_FIX_SLICE_X_002.json` proving the targeted routes have `0` release-active hits.
Rejected Alternatives: Blanket editing all remaining global findings was rejected because many are owned by Survival, UI, Audio, AI, Animation, World, Physics, and VFX domains and need owner route cards or binary sections. Removing development/editor CSV authoring was rejected because designer bridge tooling is valid outside release. Claiming release readiness was rejected because the global gate still fails.
Scalability potential: Low devices now avoid these specific debug CSV paths in release and keep boot on binary/static data. Middle keeps development hot reload for iteration. High and Ultra can retain richer editor diagnostics and larger authored tables without changing release truth ownership.
Hardware Impact: Player-frame cost added is `0 us`. The fixed slice removes byte-at-a-time file IO and debug CSV parser IL from release for the touched routes. Exact i3/MX350 boot savings are not claimed without Unity profiler capture; the measured Data Monolith native read+validate proof remains `718.273 us`, heap `0 bytes`.

## Decision 017 - Release-Active FileStream ReadByte Purge

Problem: The user specifically demanded no release build reference to `FileStream.ReadByte`. After the editor/dev fences, a release-aware scanner still found FileStream-style byte-at-a-time readers in input, equipment, lighting, buoyancy, cavitation, seaglide, submarine, UI glitch, camera juice, visor, and volcanic profile/config loaders.
Solution: Replace those loops with bulk `FileStream.Read(Span<byte>)` into existing stack buffers or existing native scratch buffers. Preserve parser behavior and bounds checks. For submarine binary float probes, read four bytes into a stack span and decode little-endian manually. For cavitation, reject files larger than the native scratch before reading.
Rejected Alternatives: Leaving byte-at-a-time reads because the global parser gate still fails was rejected; the explicit `ReadByte` demand is independently fixable. Allocating temporary managed byte arrays was rejected because it would trade one release violation for heap churn. Guarding all cross-domain profile loaders without owner review was rejected because several systems may still depend on those profiles until migrated to h8bin sections.
Scalability potential: Low devices avoid kernel call amplification and parser byte loops in the touched cold paths. Middle/high/ultra keep the same authored data behavior until owners migrate to binary sections.
Hardware Impact: Player-frame cost added is `0 us`. Release-active FileStream ReadByte findings are now `0`. Exact boot microseconds are not claimed because no Unity Player profiler capture was run; CPU samples included `97.5%`, so compile/profiler launch was forbidden.

## Decision 018 - Touched-Scope Parser Closure

Problem: After the ReadByte purge, the touched slice still compiled CSV parser surfaces in release, and one `DiegeticGlitchSurgeonRuntime` guard was too wide, hiding normal runtime methods from release along with the CSV parser.
Solution: Fix the `DiegeticGlitchSurgeonRuntime` preprocessor boundary so runtime tuning, pointer resolution, locks, shader push, and black-box dump stay release-active. Fence remaining touched-scope parser bridges under `#if UNITY_EDITOR || DEVELOPMENT_BUILD`: input profile watcher/parser, auxiliary equipment CSV parser/result route, cavitation ordnance import, seaglide vehicle profile import, submarine CSV/hull/gyro calls and local parser helpers, camera juice trauma profile import, and visor CSV overrides.
Rejected Alternatives: Leaving parser surfaces because `ReadByte` was already zero was rejected; the release build gate still sees them. Blanket owner-domain migration of the remaining 521 findings was rejected without route cards because it would cross Survival, AI, Audio, Save, UI, QA, Physics, World, and VFX ownership. Deleting development CSV authoring was rejected because designer bridge tooling remains valid outside release.
Scalability potential: Low tier no longer pays touched-scope parser/file IO boot risk in release. Middle keeps development hot reload for iteration. High and Ultra retain authored-table iteration in editor/development while production truth stays binary/static.
Hardware Impact: Player-frame cost added is `0 us`. Touched-scope release-aware parser findings are `0`; global gate remains `FAIL_RELEASE_PARSER_GATE_BLOCKED` with `521` findings. Compile/profiler proof was not run because CPU sample was `62%`, above the local build gate.

## Decision 019 - Extended Release Parser Closure

Problem: The user demanded fixing release parser contamination, not another passive report. After the previous touched-scope pass, the global release gate still reported `521` findings, including safe-to-fence cold CSV authoring lanes and standalone parser utilities.
Solution: Fence additional cold CSV bridges under `#if UNITY_EDITOR`: sump pump pipe profiles, haptic profile import, airlock profiles, modular equipment tool specs, fauna steering profiles, ecology symbiosis overrides, chemical emitter profiles, radiation profiles, standalone contract parser utilities, future seam CSV reservations, kinetic rig CSV, exosuit tuning CSV, Trade Marauder economy CSV, and survival database text injection. Keep runtime defaults, binary/vault paths, and non-parser math/hash utilities compiled for release. Re-run the release-aware parity scanner and the Core csproj build.
Rejected Alternatives: Disabling the build gate was rejected because it would fake compliance. Moving all remaining 383 owner-domain routes in one blind sweep was rejected because it risks breaking active systems without owner-specific route cards. Keeping survival/economy text injection in release was rejected because it compiles managed parser APIs into player assemblies.
Scalability potential: Low tier removes more cold parser IL and text-import boot hazards from release. Middle retains editor authoring. High and Ultra keep richer authored data while production truth migrates to h8bin or owner binary lanes.
Hardware Impact: Player-frame cost added is `0 us`. Changed-file release-aware parser findings are `0`; global gate now reports `383` findings, down from `521` at the start of this pass. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` passed with `0` warnings and `0` errors. No Unity Player profiler capture was run, so the Data Monolith runtime load claim remains the existing CLI proof: native read+validate `718.273 us`, heap `0 bytes`.

## Decision 020 - T.A.R.S. Parser Closure Pass A

Problem: The T.A.R.S. override demanded fixing remaining release parser contamination. The gate still found `383` findings after Loop 14, including safe-to-fence cold authoring imports in Fauna, Physiology, Power, Modding, Construction, Scavenging, UI, QA, Audio, Loc, Bulkhead hatch locks, Habitat fluid, Flora, and Topographical Sonar.
Solution: Fence public CSV bridge methods, default CSV auto-load calls, and private parser helpers under `#if UNITY_EDITOR || DEVELOPMENT_BUILD` while leaving release defaults, Vault buffers, runtime math, black-box dumps, and binary/static routes compiled. Replace Wrist HUD binary palette/font reads with bounded bulk reads instead of `ReadAllBytes`. Keep the release gate installed and refresh machine-readable reports.
Rejected Alternatives: Deleting authoring workflows was rejected because designer CSV hot reload is valid in editor/development. Disabling the build gate was rejected because it would allow an invalid player build. Rewriting runtime truth ownership for every foreign domain in one blind sweep was rejected because route cards are absent.
Scalability potential: Low tier avoids more cold parser IL and text-file boot hazards in release. Middle keeps development hot reload. High and Ultra retain richer authoring diagnostics without changing release truth routing.
Hardware Impact: Player-frame cost added is `0 us`. Changed-scope release-active parser findings are `0`; release-active `FileStream.ReadByte` findings remain `0`; global gate reduced from `383` to `278`. The Data Monolith loader proof remains native read+validate `718.273 us`, heap `0 bytes`; Unity Player profiler proof remains pending.

## Decision 021 - T.A.R.S. Parser Closure Pass B And Build Verification

Problem: After Pass A, the gate still reported `278` global findings. The highest-count offenders included release-active CSV/profile parsers and managed scalar parse calls in SpatialAudio, WorldGenerativeGeology, Construction validation, MemorySentinel, Somatic kinematics, Respawn reconciliation, World streaming, KCC, Abyssal shadow culling, AUP origin, Headless QA, AdaptiveStem, VocalBank, and ProceduralBone systems.
Solution: Fence the cold CSV/profile bridges under `#if UNITY_EDITOR || DEVELOPMENT_BUILD`; move SpatialAudio emergency acoustic defaults to a non-CSV helper so release no longer references the editor-only CSV parser; replace WorldGenerativeGeology `Enum.TryParse`/`int.TryParse` with manual deterministic resolvers; fence headless static source scanning outside release; then run the release-aware parity scan and Core build.
Rejected Alternatives: Converting remaining domains to Data Monolith sections in the same pass was rejected because schema ownership and route cards are missing. Leaving scalar `TryParse` calls because they are cold was rejected because the installed release gate blocks them. Claiming full release purity was rejected because the gate still reports `216` findings.
Scalability potential: Low tier release now excludes another batch of text authoring code and managed parser APIs. Middle keeps authoring lanes for iteration. High and Ultra can still use richer authored tables after migration to h8bin or owner binary lanes.
Hardware Impact: Player-frame cost added is `0 us`. Changed-scope release-active parser findings are `0`; release-active `FileStream.ReadByte` findings remain `0`; global gate reduced from `278` to `216`. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` passed with `0` warnings and `0` errors in `3.77s`. No Unity Player profiler capture was run; Data Monolith runtime load claim remains CLI-only.

## Decision 022 - Direct FileStream ReadByte Closure And Gate Reclassification

Problem: The user demanded hard proof that release builds do not retain `FileStream.ReadByte` or text/static-config parser routes. After broader parser closure, direct `stream.ReadByte()` still existed in several editor/development or smoke paths and stale docs still claimed the release gate was red.
Solution: Replace the remaining direct `stream.ReadByte()`/`fileStream.ReadByte()` calls in the checked production tree slice with `Span<byte>` reads: bulk stack-buffer reads for CSV/editor hash lanes and one-byte stack spans for corruption smoke mutators. Refresh proof artifacts: `DATA_MONOLITH_RELEASE_BUILD_GATE_X_002.json` reports `PASS_RELEASE_PARSER_GATE` with `0` blocking static-config parser findings and `12` explicitly allowed user save/profile/mod persistence findings; `DATA_MONOLITH_FILESTREAM_READBYTE_SCAN_X_002.json` reports `0` direct production-path FileStream/stream `ReadByte` matches.
Rejected Alternatives: Treating all methods named `ReadByte` as illegal was rejected because unmanaged pointer accessors, memory-mapped accessors, and custom save binary readers are not `FileStream.ReadByte` nor static text config parsing. Deleting editor tools was rejected because editor/development authoring lanes are valid. Claiming a fresh compile was rejected because the CPU/compiler gate was active.
Scalability potential: Low tier release avoids byte-at-a-time file IO and static-config parser IL. Middle keeps editor/development authoring bridges. High and Ultra retain richer authored static data after migration to h8bin or owner binary sections without changing gameplay truth ownership.
Hardware Impact: Player-frame cost added is `0 us`. Direct production-path FileStream/stream `ReadByte` findings are `0`; preprocessor balance issues are `0`; `git diff --check` is clean except LF/CRLF warnings. Fresh `dotnet build` was not launched because the latest gate sample was CPU `100%` with `9` active `dotnet/csc` processes; last completed Core build remains PASS from Loop 17.

## Decision 023 - Fresh Compile After ReadByte Closure

Problem: The direct `ReadByte` closure modified compile-active files after the last green build, so the previous build proof was stale. Running `dotnet` while CPU/compiler gates were active would violate project rules.
Solution: Wait until CPU dropped below `50%` and no `dotnet/csc` processes were active, then run `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`. Record the exact warnings instead of collapsing them into a false clean build.
Rejected Alternatives: Launching build at CPU `69-71%` was rejected by local policy. Calling the build warning-free was rejected because `CombatDamageRuntime.EvaluateArmorPenetrationJob` emits four CS0649 warnings outside X_002 ownership. Editing combat fields was rejected as out-of-domain and unrelated to Data Monolith release fencing.
Scalability potential: Low tier release still benefits from parser-free static-config and zero-GC monolith load path. Middle/high/ultra retain editor/development authoring lanes. Compile hygiene now has current post-fix evidence instead of stale proof.
Hardware Impact: Player-frame cost added is `0 us`. Latest Core build result: PASS, `0` errors, `4` non-X002 warnings, elapsed `79.71s`. Narrow CLI fuzzer/load-stress rerun remained blocked after build because CPU returned to `100%` then `65.7%`; existing stress proof remains native read+validate `718.273 us`, heap `0 bytes`.

## Decision 024 - Release CLI Stress Revalidation

Problem: The post-build status still treated the narrow CLI fuzzer/load-stress rerun as CPU-gated, and a stale/no-build stress report cannot be used as proof for the current blob and current code.
Solution: Rebuild `Tools/DataMonolithBakeCli/DataMonolithBakeCli.csproj` in Release once CPU/compiler gates allowed it, then run the CLI bake/fuzzer/load-stress path against the current `static_data.h8bin`. Use the fresh JSON reports as the only current timing/failure-code source.
Rejected Alternatives: Reusing stale Debug or no-build timings was rejected because they can measure old code. Claiming Unity Player/GlobalDataVault profiler proof from a CLI resident-pointer probe was rejected because no Unity Player profiler capture was run. Removing checksum or range validation to chase lower timings was rejected because corrupt data must fail before simulation consumers can read undefined sections.
Scalability potential: Low tier receives a single compact, validated blob path and aborts corrupt data before AI/physics table access. Middle keeps development/editor authoring lanes. High and Ultra can add sections while preserving the 64-byte section alignment, little-endian contract, and fail-fast header/section checks.
Hardware Impact: Player-frame cost added is `0 us`. Fresh Release CLI proof: native read `233.900 us`, resident validation mean `461.574 us`, native read+validate estimate `695.474 us`, native resident allocated bytes `0`, bad checksum rejected with failure code `3` (`BadChecksum`), bad offset rejected with failure code `8` (`SectionOutOfRange`). Narrow Release CLI build passed with `0` errors and `38` editor DTO/stub warnings; real Unity player/GlobalDataVault profiler proof remains pending.

## Decision 025 - Strict Editor-Only CSV Fence Slice

Problem: The non-development release gate was clean, but a strict reading of the user requirement disallows static-config CSV/text parsers from compiling into Development Build via `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. That is a narrower and harsher policy than the previous release-only proof.
Solution: Patch a high-risk static-config slice by narrowing parser-heavy development guards from `#if UNITY_EDITOR || DEVELOPMENT_BUILD` to `#if UNITY_EDITOR` across 41 target files, producing 37 current code diffs, including dispatcher/config, ecology, physiology, audio, UI, VFX, physics, localization, QA, and Data Monolith contract surfaces. Record the mechanical slice in `Docs/Reports/DATA_MONOLITH_EDITOR_ONLY_CSV_FENCE_SLICE_X_002.json`.
Rejected Alternatives: Leaving Development Build parser bridges was rejected because the latest override demands literal editor-only isolation. Blindly rewriting all 159 remaining broad matches was rejected because the broad grep includes diagnostics and owner-domain systems that need route-card review. Deleting authoring paths was rejected because designers still need editor CSV facades.
Scalability potential: Low tier and development-player test builds now carry less static-config parser IL in the touched slice. Middle keeps editor authoring only. High and Ultra keep richer authoring and diagnostics in Editor, not in player truth routes.
Hardware Impact: Player-frame cost added is `0 us`. Touched-slice direct `#if UNITY_EDITOR || DEVELOPMENT_BUILD` guards are `0`, guard replacements are `195`, current code diff files are `37`, preprocessor balance is PASS, release gate remains `PASS_RELEASE_PARSER_GATE` with `0` blocking findings. Compile remains pending because CPU gate sampled `79.92%`, then `100%` with active `csc/dotnet`; no Unity Player profiler proof is claimed.

## Decision 026 - Strict Editor-Only CSV Fence Slice B

Problem: After slice A, high-signal parser-heavy `UNITY_EDITOR || DEVELOPMENT_BUILD` guards still existed in profile/config bridge files. The remaining list also included broad bootstrap/audio diagnostics; blindly rewriting those would risk deleting legitimate development instrumentation.
Solution: Patch a second bounded slice of static-config/profile bridges by narrowing `#if UNITY_EDITOR || DEVELOPMENT_BUILD` to `#if UNITY_EDITOR` across 32 target files, producing 25 current code diffs and 82 guard replacements. The slice covers visor, world resources, scheduling profiles, buoyancy, scavenging, AUP origin, lighting, save fuzzer, thermodynamics, combat tuning, vehicle damage profiles, habitat locks, auxiliary equipment, stress/somatic/visual pressure, utility AI, ecosystem, audio stem/vocal bank, telemetry, memory sentinel, terminal UI, bioluminescence, and ecosystem director surfaces.
Rejected Alternatives: Broadly rewriting GameBootstrapper and SpatialAudioManager was rejected for this pass because their guard count is high and includes diagnostics that need owner review. Leaving the second high-signal slice untouched was rejected because it still compiles static-config parser bridges into development player code. Converting these systems to new h8bin sections in this pass was rejected because route cards and schema ownership are absent.
Scalability potential: Low and development-player builds carry less static-config parser IL in the touched slice. Middle retains editor-only tuning. High and Ultra keep authoring diagnostics inside the editor while production truth remains binary/static.
Hardware Impact: Player-frame cost added is `0 us`. Touched-slice direct `#if UNITY_EDITOR || DEVELOPMENT_BUILD` guards are `0`, guard replacements are `82`, current code diff files are `25`, preprocessor balance is PASS, release gate remains `PASS_RELEASE_PARSER_GATE` with `0` blocking findings. Compile remains pending because CPU gate sampled `100%` with 9 active `csc/dotnet` processes; no Unity Player profiler proof is claimed.

## Decision 027 - Strict Editor-Only CSV Fence Slice C

Problem: After slices A and B, parser/profile bridge guards still compiled CSV/text configuration code into Development Build across atmosphere, construction, equipment, rendering, physics, power, and world systems.
Solution: Patch a third bounded slice by narrowing `#if UNITY_EDITOR || DEVELOPMENT_BUILD` to `#if UNITY_EDITOR` across 28 target files, producing 23 current code diffs and 55 guard replacements. The slice covers world streaming profiles, auxiliary equipment CSV contracts/parsers, ocean fluid profiles, storm/ocean atmosphere bridges, construction catalog/pipe validators, voxel/path ecosystem bridges, material/culling profile imports, buoyancy/vehicle physics profile lanes, power logistics profiles, water optics, structural warning data, ballast auto-level profiles, voxel surface nets, and procedural coral vault data.
Rejected Alternatives: A global rewrite of every remaining broad guard was rejected because some remaining blocks are broad diagnostics or development instrumentation rather than parser-only static-config code. Leaving these slice-C bridges in Development Build was rejected because they are direct parser/profile bridge surfaces. Converting them to new h8bin sections in this pass was rejected because section ownership and route cards are not established.
Scalability potential: Low and development-player builds carry less parser IL and fewer cold text import routes. Middle retains editor-only tuning. High and Ultra keep authored table richness through the editor while player truth remains binary/static.
Hardware Impact: Player-frame cost added is `0 us`. Touched-slice direct `#if UNITY_EDITOR || DEVELOPMENT_BUILD` guards are `0`, guard replacements are `55`, current code diff files are `23`, preprocessor balance is PASS, release gate remains `PASS_RELEASE_PARSER_GATE` with `0` blocking findings and `12` allowed persistence findings. Compile remains pending CPU/compiler gate; no Unity Player profiler proof is claimed.

## Decision 028 - Strict Editor-Only CSV Fence Slice D And Compile Fix

Problem: Remaining parser/profile bridge guards still compiled static-config CSV/text import code into Development Build across animation, audio DSP, construction, crafting, ecosystem, gameplay, rendering, physics, physiology, power, and ocean systems. A post-slice Core build also exposed an unrelated compile error in `HectonOSBootManager`: `StringBuilder` was used without importing `System.Text`.
Solution: Patch a fourth bounded slice by narrowing `#if UNITY_EDITOR || DEVELOPMENT_BUILD` to `#if UNITY_EDITOR` across 40 target files, producing 30 current code diffs and 71 guard replacements. Fix the compile error by adding `using System.Text;` to `Assets/_Project/Scripts/UI/HectonOSBootManager.cs`.
Rejected Alternatives: A blind global rewrite was rejected because remaining broad guards include diagnostics and non-parser development instrumentation. Leaving the `StringBuilder` compile error was rejected because the build failure is deterministic and the fix is isolated. Replacing `StringBuilder` with string concatenation was rejected because it would allocate more and touch more code than needed.
Scalability potential: Low and development-player builds carry less parser IL. Middle keeps editor-only data tuning. High and Ultra keep authored richness through editor tools while player truth remains binary/static.
Hardware Impact: Player-frame cost added is `0 us`. Touched-slice direct `#if UNITY_EDITOR || DEVELOPMENT_BUILD` guards are `0`, guard replacements are `71`, current code diff files are `30`, preprocessor balance is PASS, release gate remains `PASS_RELEASE_PARSER_GATE` with `0` blocking findings. First rebuild attempt failed only on missing `System.Text`; rebuild after the fix is pending CPU/compiler gate.

## Decision 029 - Compile Wall Partial Closure After Slice D

Problem: Post-slice Core compile exposed cascading unrelated walls: stale bootstrap/input contract output, missing math namespace in brine movement, ambiguous `Object` in death memory UI, unsafe `int` to `ushort` SourceId assignment, and missing toxic exposure signal type in Core compile.
Solution: Rebuild `Hecton8.Bootstrap.Contracts.csproj` clean, add deterministic local fixes (`Unity.Mathematics`, `UnityEngine.Object`, bounded `ushort` SourceId conversion), include `ToxicOutgassingChemistryTypes.cs` in `Hecton8.Core.csproj`, and rebuild `Hecton8.Input.csproj` clean so `InputManager` implements the refreshed `INativeInputManagerRuntime` contract.
Rejected Alternatives: Ignoring compile walls was rejected because user explicitly ordered fixing problems. Replacing `StringBuilder` or input contracts with local duplicate interfaces was rejected because it would split ownership and break registry identity. Blindly editing gameplay toxic signal call sites was rejected because the signal type already has one owner in Atmosphere contracts.
Scalability potential: Low tier gets compile-valid static signal contracts and no parser bridge regression. Middle/high/ultra retain the same binary/static truth route and refreshed input contract path.
Hardware Impact: Player-frame cost added is `0 us`. `Hecton8.Bootstrap.Contracts` build passed with `0` errors. `Hecton8.Input` build passed with `0` errors and 2 existing CS0252 warnings. Core rebuild after local fixes is pending CPU gate; no Unity Player profiler proof is claimed.

## Decision 030 - Strict Editor-Only CSV Fence Slice E

Problem: After slices A-D, remaining high-signal parser/static-tuning bridges still compiled through `UNITY_EDITOR || DEVELOPMENT_BUILD`; broad save/profiler/bootstrap/dev instrumentation remained too risky for blind conversion.
Solution: Patch a fifth bounded slice by narrowing `#if UNITY_EDITOR || DEVELOPMENT_BUILD` to `#if UNITY_EDITOR` across 22 target files, producing 18 current code diffs and 31 guard replacements. The slice covers procedural IK stage profiles, cartography grid import, vault binary archaeology, scanner data mining, wave/brine/harpoon physics tuning, suit integrity overlays, headless fracture bot data, quest DAG resolver, bilateral DRS profiles, thermodynamics hazard/reactor contracts, laser cutter upgrade data, topographical sonar synthesis, VFX foam/propwash/fog/silt contracts, and dynamic decal vault tuning.
Rejected Alternatives: Rewriting save recovery/fuzzer and runtime profiler blocks was rejected because they are persistence/diagnostic instrumentation, not necessarily static-config ownership. Rewriting GameBootstrapper/SpatialAudioManager broad blocks was again rejected without route-card review. Waiting idle for CPU was rejected because another safe parser slice was available.
Scalability potential: Low and development-player builds carry less parser/static-tuning IL. Middle retains editor-only table authoring. High and Ultra keep richer authored visual/physics tuning through editor tools while player truth remains static/binary.
Hardware Impact: Player-frame cost added is `0 us`. Touched-slice direct `#if UNITY_EDITOR || DEVELOPMENT_BUILD` guards are `0`, guard replacements are `31`, current code diff files are `18`, preprocessor balance is PASS, release gate remains `PASS_RELEASE_PARSER_GATE` with `0` blocking findings. Core rebuild remains CPU-gated.

## Decision 031 - Signal/Core Compile Fixes

Problem: Core rebuild after slice E and namespace fixes exposed missing `GlobalSignals.AdvanceSignalSequence`, stale `ToolHitUtility` `SourceId` conversion, and ambiguous `math.max(byte, byte)` in signal coalescing.
Solution: Add a private `GlobalSignals.AdvanceSignalSequence(ref int)` helper in the partial state file, precompute a bounded `ushort packetSourceId` before constructing `DamagePacket`, and replace the byte `math.max` call with an explicit byte comparison.
Rejected Alternatives: Replacing signal sequence increments with direct duplicated arithmetic at every call site was rejected because it repeats wrap handling. Using `math.max((int)...)` for byte merge was acceptable but noisier than direct byte comparison. Leaving `SourceId` implicit was rejected because `DamagePacket.SourceId` is explicitly `ushort`.
Scalability potential: Low/middle/high/ultra all keep deterministic signal sequence behavior and avoid runtime parser regressions.
Hardware Impact: Player-frame cost added is `0 us`; all fixes are compile correctness with equivalent or cheaper generated code. Core rebuild after these fixes is pending CPU gate.

## Decision 032 - Strict Editor-Only CSV Fence Slice F

Problem: A static follow-up found one concrete development-player hazard: `TerminalOsRuntime` had CSV monitor methods returning only under `!UNITY_EDITOR && !DEVELOPMENT_BUILD`, while the parser methods they called were already `UNITY_EDITOR` only. Development player builds would still compile the FileStream CSV monitor branch without compiling the parser methods. Additional CSV/static-config bridges in Flora genome hotloading, procedural geology, terrain streaming profiles, and SpatialAudio acoustic CSV/LUT fallback also remained under `UNITY_EDITOR || DEVELOPMENT_BUILD`.
Solution: Make the Terminal OS CSV monitor methods return outside `UNITY_EDITOR`, and narrow the safe parser/static-config bridge guards in `FloraGenomeCsvHotloader`, `ProceduralGeologyContracts`, `TerrainChunkPagerTypes`, and `SpatialAudioManager` to `UNITY_EDITOR`. Leave broad SpatialAudio debug logging/throttling guards intact because they are not CSV/static-config parser ownership.
Rejected Alternatives: Leaving Terminal OS as-is was rejected because it creates a development-player compile/runtime boundary mismatch. Blindly narrowing every remaining `UNITY_EDITOR || DEVELOPMENT_BUILD` guard in SpatialAudio was rejected because most are diagnostics, not parser bridges. Moving these systems into new h8bin sections in this pass was rejected because owner route cards and binary schemas are not established.
Scalability potential: Low and development-player builds carry less parser/file-IO code. Middle keeps editor-only table authoring. High and Ultra keep authored acoustic/botany/world profile richness through editor tools while player truth remains binary/static.
Hardware Impact: Player-frame cost added is `0 us`. Slice F narrowed 5 files and 8 guards. Touched parser-bridge broad development guards are `0`; remaining project-wide `#if !UNITY_EDITOR && !DEVELOPMENT_BUILD` is a MemorySentinel cheat-engine simulation gate, not static-config parsing. Core rebuild remains pending CPU gate.

## Decision 033 - Strict Editor-Only CSV Fence Slice G

Problem: A structural scan of remaining broad development guards found actual world/profile parser blocks still compiling into Development Build: biome atmosphere CSV ingest, flora stiffness CSV reload, world streaming profile CSV parsing, and flora ambient sway biome profile parsing. In FloraAmbientSway the public parser was guarded, but helper functions still compiled into player assemblies.
Solution: Narrow those parser bridges to `UNITY_EDITOR`, and add an explicit editor-only fence around FloraAmbientSway parser helpers. Leave non-parser runtime constants and Vault buffer IDs intact because they are part of runtime DTO/state layout, not authoring import.
Rejected Alternatives: Moving all world profile routes into Data Monolith sections immediately was rejected because section schema ownership is missing. Removing the runtime Vault scratch handles was rejected because they are part of existing buffer layout and may be used by editor import. Narrowing debug-only FloraInteraction compute-shader log guard was rejected because it is diagnostic, not CSV/static-config parsing.
Scalability potential: Low and development-player builds compile less parser code. Middle retains editor-only tuning. High and Ultra retain richer authored world/vegetation atmosphere profiles through editor import until owner route cards move them to binary sections.
Hardware Impact: Player-frame cost added is `0 us`. Slice G narrowed 4 files, 5 broad development guards, and one helper function cluster. Touched parser-bridge broad development guards are `0`; Core rebuild remains pending CPU/compiler gate.

## Decision 034 - Strict Editor-Only CSV Fence Slice H

Problem: After slice G, a structural scan still found safe-to-fence CSV facades compiling into Development Build: world chunk streaming profile text import, flora genome CSV overrides, PDA scanner profile import, bootstrap memory layout CSV overrides, and AUP floating-origin tuner reload.
Solution: Narrow those five route facades to `UNITY_EDITOR`. Keep remaining development guards in those touched files because they are watchdog, logging, or diagnostic instrumentation and not static-config parser bridges.
Rejected Alternatives: Fencing all GameBootstrapper development diagnostics was rejected because that would remove development health telemetry unrelated to CSV parsing. Editing SaveBinaryStorage/WAL fuzzer hits was rejected because those are persistence/fuzzer ownership, not Data Monolith static config. Deleting the authoring facades was rejected because designers still need editor import.
Scalability potential: Low and development-player builds compile less config parser surface. Middle keeps editor-only table authoring. High and Ultra keep richer authored route data through editor import while runtime truth remains binary/static.
Hardware Impact: Player-frame cost added is `0 us`. Slice H narrowed 5 files and 5 broad development guards. Touched broad development parser hits are `0`; Core rebuild remains pending CPU gate.

## Decision 035 - Post-Slice Core Compile Closure

Problem: Slices D-H and compile-wall fixes touched Core-compiled code after the last green build. Reports still said pending compile, which is stale evidence.
Solution: Wait for the local build gate to open at CPU `47.49%` with `0` active `dotnet/csc`, then run `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`. Update slice reports A-H from pending compile to Core compile pass.
Rejected Alternatives: Launching while external compilers were active or CPU was above 50% was rejected by project policy. Claiming Unity player proof from a Core csproj build was rejected because it does not measure player boot or profiler GC. Re-running unrelated full-solution builds was rejected because the required proof was the touched Core assembly.
Scalability potential: Low/middle/high/ultra now share the same compile-clean static-config fencing state; no gameplay truth route changed.
Hardware Impact: Player-frame cost added is `0 us`. Core compile proof: PASS, `0` warnings, `0` errors, elapsed `108.34s`. Direct production-path FileStream/stream `ReadByte` scan remains `0`.

## Decision 036 - Development-Build CSV Gate Symbol Model And Slice I

Problem: The release build gate had a real blind spot: it accepted a `developmentBuild` parameter but still evaluated `DEVELOPMENT_BUILD` and `DEBUG` as false in preprocessor expressions. That meant development-player CSV residues could be under-counted. A follow-up scan also found static-config CSV parser routes still compiling in development players: GlobalTelemetryBus flag CSV, GlobalDataVault memory-budget CSV, rollback netcode CSV tuning, save compression CSV jobs, Merkle CSV overrides, and WAL fuzzer CSV profile shell.
Solution: Make the scanner evaluate `UNITY_EDITOR=false`, `DEVELOPMENT_BUILD=<actual build option>`, `DEBUG=<actual build option>`, and split release/development reports so a development warning does not overwrite release PASS evidence. Narrow the found static-config CSV/parser routes to `UNITY_EDITOR`; in rollback netcode, also fence the CSV path, hash constants, scratch handle, poll, and parser so development players no longer allocate the CSV scratch buffer or build the CSV path.
Rejected Alternatives: Treating development builds as release in the scanner was rejected because it hides the exact policy the user asked to test. Blindly converting every remaining `UNITY_EDITOR || DEVELOPMENT_BUILD` block was rejected because the residual set includes replay streams, dev smoke telemetry, and save persistence/failure-recovery paths that are not Data Monolith static-config. Claiming compile proof from the previous A-H build was rejected because slice I is post-build.
Scalability potential: Low and development-player builds carry less parser/file-IO IL and one less rollback CSV scratch allocation. Middle retains editor-only authoring and fuzzer workflows. High and Ultra keep richer tuning through editor bakes instead of player-side text parsing.
Hardware Impact: Player-frame cost added is `0 us`. Removed development-player rollback CSV poll cadence and 4096-byte CSV scratch handle allocation. Static verification: touched-file broad development CSV guards `0`, preprocessor balance `0`, focused diff-check PASS with LF/CRLF warnings only, direct production-path FileStream/stream `ReadByte` scan `0`. Compile remains CPU-gated: samples `100%`, `52%`, `80.53%`, no compiler processes.

## Decision 037 - Development-Build Static Config Slice J

Problem: Slice I fixed the scanner model and one residue set, but a stricter structural search still found concrete development-player static-config hazards: `#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)` around CSV/file-ingest methods and unguarded authoring CSV loaders in Ballistics/Haptic surfaces. Some methods called parser helpers already guarded by `UNITY_EDITOR`, which can create development-player compile mismatches.
Solution: Narrow the remaining static-config CSV cold-ingest methods to `UNITY_EDITOR`: Ballistics armor penetration CSV loader/parser, Metabolism biological/suit CSV loaders and parser helpers, telemetry endpoint CSV loader/helpers, terrain streaming profile CSV loader, nutrient drift/carrion/radiation/sensory/ocean/shoreline/predator acoustic CSV cold-ingest lanes, and Haptic profile CSV scratch lane. Where safe, also stop reserving player-side CSV scratch Vault buffers for Ballistics and Haptic.
Rejected Alternatives: Leaving development builds with parser/file IO was rejected because the user explicitly asked for physical absence of configuration text parsing outside the editor. Converting those tables into new Data Monolith sections in this slice was rejected because section ownership and route cards are not established for combat, physiology, rendering, fauna, and input domains. Removing diagnostic `UNITY_EDITOR || DEVELOPMENT_BUILD` blocks was rejected when the block only logs or validates layout and does not ingest static config.
Scalability potential: Low and development-player builds carry less file IO/parser IL and reserve less native scratch. Middle keeps editor-only authoring. High and Ultra can later move these authored datasets into h8bin sections without changing gameplay truth ownership.
Hardware Impact: Player-frame cost added is `0 us`. Removed Ballistics 16 KB CSV scratch reservation plus Haptic/Ocean/Shoreline CSV scratch/file-read helper surfaces from non-editor players. Static proof: `Docs/Reports/DATA_MONOLITH_DEV_BUILD_CSV_FENCE_SLICE_J_X_002.json`, remaining negated broad-development static-config candidates `0`, 12 touched files preprocessor balance `0`, production-path direct FileStream/stream `ReadByte` matches `0`. Compile remains CPU-gated: initial CPU `100%` with `9` active `dotnet/csc`; later 5-minute build-gate wait stayed at CPU `100%` with `0` compiler processes, so no build was launched.

## Decision 038 - Development-Build Static Config Slice K

Problem: After slice J, static review found four remaining source shapes where player assemblies could still physically carry static-config CSV/file parser helpers even when their call sites were intended as editor authoring only: `StressDrivenSpawnDirector` rule CSV reload helpers, `VocalWarningSystem.ParseWarningProfiles`, `FoundationSnappingCalculatorData` profile CSV loaders/token parsers, and `BaseAtmosphereLogisticsRuntime.ReadCsvFileNoStringAlloc`.
Solution: Fence those helper bodies under `UNITY_EDITOR` while leaving runtime simulation buffers, read fences, blackbox dumps, and baked/default DTO paths intact. `StressDrivenSpawnDirector` no longer compiles the rule CSV scratch handle, reload entry point, path resolver, or FileStream reader into non-editor players.
Rejected Alternatives: Removing the authoring helpers was rejected because editor tuning windows still need them. Gating runtime read fences in Foundation snapping was rejected because they protect live native profile reads. Editing legacy binary ecosystem loaders in the same slice was rejected because those are not CSV/text parser helpers and need owner route-card review before removal.
Scalability potential: Low and development-player builds carry less parser/file-IO IL and fewer cold scratch lanes. Middle keeps editor-only authored tuning. High and Ultra can later migrate these domain tables into h8bin lanes without changing gameplay truth ownership.
Hardware Impact: Player-frame cost added is `0 us`. Static proof: `Docs/Reports/DATA_MONOLITH_DEV_BUILD_CSV_FENCE_SLICE_K_X_002.json`, 4 touched files preprocessor balance `0`, development-player touched static-config/file/parser hit scan `0`, focused `git diff --check` PASS with LF/CRLF warnings only. Compile remains CPU-gated at CPU `100%` with `9` active `dotnet/VBCSCompiler` processes.

## Decision 039 - Cold Binary Bridge Heap Removal Slice L

Problem: `FaunaKinematicsRuntime` hydrated `leviathan_rig_definitions.h8bin` through a managed `byte[]` before copying bytes into native Vault scratch. Even though this is a cold binary route rather than CSV text parsing, it violates the zero-GC boot direction.
Solution: Replace the managed 4096-byte array with `stackalloc Span<byte>` and keep the existing FileStream read, Vault scratch copy, binary parser, and emergency mock fallback behavior unchanged.
Rejected Alternatives: Gating the binary rig loader to editor-only was rejected because it is a runtime binary asset route, not a CSV authoring facade. Rewriting it into a Data Monolith section in this slice was rejected because fauna rig schema ownership and route cards are outside X_002's monolith-owned balance tables. Leaving the heap allocation was rejected because the fix is local and behavior-preserving.
Scalability potential: Low removes a cold boot heap allocation on fauna rigs. Middle keeps the same binary delivery route. High and Ultra can later move richer rig data into owner-approved binary sections without changing this no-heap read pattern.
Hardware Impact: Player-frame cost added is `0 us`. Removed one 4096-byte managed cold-read allocation from the rig hydration path. Static proof: `Docs/Reports/DATA_MONOLITH_ZERO_GC_COLD_BINARY_FIX_SLICE_L_X_002.json`; compile remains gated by active `dotnet/VBCSCompiler` processes.

## Decision 040 - Cold Binary Record Heap Removal Slice M

Problem: `VolcanicUpdraftDirector` read each legacy vent binary record through a managed 64-byte `byte[]`. It is a cold runtime binary bridge rather than CSV parsing, but it still adds avoidable managed heap staging to a boot/runtime data hydration route.
Solution: Replace the record array with `stackalloc Span<byte>`, convert the exact-read helper to `Span<byte>`, and convert little-endian float/double readers to `ReadOnlySpan<byte>`. Existing record layout, fallback behavior, and runtime DTO ownership remain unchanged.
Rejected Alternatives: Gating the binary loader to editor-only was rejected because vents are runtime binary content. Migrating vents into the main Data Monolith section in this slice was rejected because the world/updraft owner route and schema card are not established. Leaving the array was rejected because the no-heap fix is local and does not change behavior.
Scalability potential: Low removes a cold managed allocation from vent binary hydration. Middle keeps the current binary delivery route. High and Ultra can later move richer vent fields into an owner-approved binary section without reintroducing managed staging.
Hardware Impact: Player-frame cost added is `0 us`. Removed one 64-byte managed cold-record buffer per loader invocation. Static proof: `Docs/Reports/DATA_MONOLITH_ZERO_GC_COLD_BINARY_FIX_SLICE_M_X_002.json`; compile remains gated by active `dotnet/VBCSCompiler` processes.

## Decision 041 - Post-Slice Core Compile Closure I/J/K/L/M

Problem: Slice reports I/J/K/L/M still carried compile-pending status after static verification. A legal build window finally opened, but the first build attempt raced concurrent edits in `LaserCutter` and `PDADataLogTab`, producing stale missing-symbol errors that were not present in the current file snapshot.
Solution: Wait for active compiler processes to clear, re-sample CPU under the local threshold, and rebuild the current Core project snapshot. The second build passed with `0` warnings and `0` errors; slice reports were updated from pending to pass.
Rejected Alternatives: Treating the first failed build as a Data Monolith failure was rejected because the current sources no longer contained the reported missing symbols. Editing `LaserCutter`/`PDADataLogTab` blindly was rejected because those files were actively changed by another agent and the current snapshot already resolved the symbols. Claiming success without a rebuild was rejected.
Scalability potential: Low/middle/high/ultra now share a compile-verified state for development-player CSV fencing and cold binary no-heap fixes; gameplay truth ownership remains unchanged.
Hardware Impact: Player-frame cost added is `0 us`. Build proof: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` PASS, `0` warnings, `0` errors, elapsed `58.77s`. Unity player/profiler proof remains pending.

## Decision 042 - Development-Player Text Read Fence Slice N

Problem: A development-aware parser/file-IO scan over non-editor runtime scripts found one remaining active managed text read in a player-capable assembly: `VisualOmegaSmokeTester.ReadProjectFile` used `File.ReadAllText` under `UNITY_EDITOR || DEVELOPMENT_BUILD`. The route is a source/shader audit, not Data Monolith static config, but it still physically compiles text file IO into development players.
Solution: Narrow the smoke-test source audit to `UNITY_EDITOR` only. The editor source audit remains available; development players no longer compile the `File.ReadAllText` source reader.
Rejected Alternatives: Leaving it because it is only a smoke tester was rejected because the user demanded physical parser/text-read absence where practical. Rewriting the source audit to binary monolith data was rejected because it inspects source and shader files, not gameplay static truth. Deleting the tester was rejected because editor QA still uses it.
Scalability potential: Low/development-player builds carry less managed text IO. Middle/high/ultra retain editor QA coverage without shipping source-audit readers.
Hardware Impact: Player-frame cost added is `0 us`. Static proof: `Docs/Reports/DATA_MONOLITH_DEV_BUILD_TEXT_READ_FENCE_SLICE_N_X_002.json`; focused development-aware scanner candidate files `302`, finding count `0`; extended release/development scanner candidate files `1332`, production candidates `862`, blocking findings `0` in both player symbol models, documented save/profile/mod persistence findings `12` in both modes. Core compile is pending CPU/compiler gate for this one-file post-build change.

## Decision 043 - Memory-Ingest Fail Telemetry Parity Slice O

Problem: `H8StaticDataArena.TryInitializeFromMemory` failed closed on early corrupt input classes (`source == null`, too small, too large, arena allocation/view failure), but some of those returns did not record Data Monolith failure telemetry or request the dump path. The file-path boot loader already recorded telemetry for the same failure class.
Solution: Add `RecordFailureTelemetry(status, 0u)` before the early memory-ingest `false` returns for `FileTooSmall`, `FileTooLarge`, and arena allocation/view failure. Leave the ready-locked branch unchanged because it is a write-lock policy rejection, not corrupt input.
Rejected Alternatives: Throwing exceptions from memory ingest was rejected because the loader must fail closed without cascading managed faults. Clearing an existing loaded arena on early invalid memory input was rejected because preserving the last valid resident blob is safer than replacing it with poisoned or empty state. Adding a new telemetry schema was rejected because the existing Data Monolith ring and dump path already encode load status.
Scalability potential: Low/middle/high/ultra all get the same deterministic failure visibility. No gameplay truth route, DTO layout, section offset, or binary schema changes.
Hardware Impact: Player-frame cost added is `0 us`. Added four source-level telemetry calls on cold failure branches; no runtime parser, string parse, or managed allocation was introduced. Static proof: `Docs/Reports/DATA_MONOLITH_MEMORY_INGEST_FAIL_TELEMETRY_SLICE_O_X_002.json`; Core compile remains pending CPU/compiler gate.

## Decision 044 - Span Compile-Wall Closure Slice P

Problem: The post-N/O Core build attempt exposed two deterministic span errors that block any further Data Monolith compile proof: `SubtitleManager.HandleNotificationPushed` passed a `ReadOnlySpan<char>` to a string overload, and `LocalizationManager.TryResolvePluralRawSpanFromSuffix` tried to expose a span derived from a stackalloc key buffer across helper boundaries.
Solution: Keep both paths span/native-buffer based. `SubtitleManager` now calls `EnqueueBuffered(ReadOnlySpan<char>, ...)`. `LocalizationManager` now uses the stack buffer only to compute a `LocHash` key and resolves the final text from the registry-owned raw buffer in the caller.
Rejected Alternatives: Allocating a string for notification subtitles was rejected because it would fix compile by adding heap churn. Returning the stack-backed plural span was illegal and unsafe. Deleting plural fallback was rejected because it changes localization behavior.
Scalability potential: Low/development-player builds keep zero-heap subtitle and localization paths. Middle/high/ultra retain the same localized content richness without changing DTO layout or static data authority.
Hardware Impact: Player-frame cost added is `0 us`. The fix removes compile failure without introducing managed allocation, parser code, binary schema changes, or Data Monolith layout changes. Core compile proof is still pending because a 10-minute build-gate wait timed out under CPU `60-100%` and intermittent compiler processes.

## Decision 045 - Player Heap Staging Fix Slice Q

Problem: `WristHologramHudRuntime` had already fenced its font-metrics CSV and legacy binary import helpers under `UNITY_EDITOR`, but the reusable `byte[8192]` scratch field remained outside the editor fence. That means player component construction still paid an unnecessary managed heap allocation for an editor-only authoring path.
Solution: Move `_csvReadBuffer` behind `UNITY_EDITOR` while leaving the existing editor-only import functions unchanged. Player builds no longer instantiate the CSV/binary staging array.
Rejected Alternatives: Leaving the array because the parser methods are editor-only was rejected because the allocation still physically exists in player instances. Moving wrist HUD font metrics into the Data Monolith in this pass was rejected because UI font atlas ownership and binary schema are not part of X_002 static balance data. Deleting editor import was rejected because editor QA still uses it.
Scalability potential: Low removes one player-side managed staging allocation per wrist HUD component. Middle keeps editor authoring. High and Ultra can keep richer UI font tuning through editor import without carrying the scratch array in player runtime.
Hardware Impact: Player-frame cost added is `0 us`. Removed `8192` managed bytes per `WristHologramHudRuntime` instance from non-editor compilation. No parser, schema, DTO layout, or gameplay truth route changed. Core compile proof remains pending CPU/compiler gate.

## Decision 046 - Player Heap Staging Fix Slice R

Problem: `ThermodynamicsHazardGridRuntime.FileWorker` allocated both runtime binary constants staging bytes and editor CSV override staging bytes when starting the config worker. The CSV request and apply path is editor-only, but `_csvWorkerBytes ??= new byte[CsvBufferBytes]` still allocated 4096 managed bytes in non-editor players.
Solution: Keep the 16-byte runtime binary constants worker buffer, and move only the CSV worker allocation behind `UNITY_EDITOR`. The player binary constants h8bin lane remains operational while the editor-only CSV staging buffer disappears from player construction.
Rejected Alternatives: Removing the whole file worker was rejected because `thermodynamic_constants.h8bin` is a runtime binary config route. Allocating the CSV buffer lazily without an editor fence was rejected because a player could still allocate it if a stale request state is set. Moving thermodynamics profiles into the main Data Monolith in this pass was rejected because thermodynamics has a separate owner and binary route.
Scalability potential: Low removes another cold managed staging allocation while preserving binary constants load. Middle keeps editor CSV tuning. High and Ultra can keep richer thermodynamic authoring through editor bake/import without carrying CSV scratch in players.
Hardware Impact: Player-frame cost added is `0 us`. Removed `4096` managed bytes per thermodynamics runtime worker from non-editor compilation; preserved the required 16-byte runtime binary staging path. Core compile proof remains pending CPU/compiler gate.

## Decision 047 - Core Build Attempt S Stale Import Classification

Problem: After the CPU/compiler gate finally opened, `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` failed on `ConstructionRuntimeProxyFactory.cs(3,15)` claiming `Hecton8.Graphics` was missing. Current source inspection immediately after the failure shows line 3 is `using Hecton8.Power;` and there is no `using Hecton8.Graphics;` in that file.
Solution: Classify this as a stale/raced source build attempt and require a retry before claiming compile proof. Do not apply a fake fix to `ConstructionRuntimeProxyFactory.cs` because the reported import is absent in the current snapshot.
Rejected Alternatives: Blindly editing construction code was rejected because it would touch a non-existent current error. Claiming a pass was rejected because the build exited `1`. Removing duplicate Input compile warnings from generated Unity csproj was rejected for now because the project file is generated and the warnings are not the current blocking error.
Scalability potential: Low/middle/high/ultra are unaffected until a clean compile retry proves current source. The build wall has no gameplay runtime change.
Hardware Impact: Player-frame cost added is `0 us`. Build attempt S failed in `19.70s` with `1` error and `2` warnings; no Data Monolith binary, schema, or parser route changed.

## Decision 048 - Core Build Attempt T Stale Current-Source Mismatch

Problem: A second legal Core build attempt failed on six errors in `FaunaBrain`, `HectonPlayerMotor`, and `SubmarineAtmosphereSystem`, but immediate source inspection shows those exact issues are already absent in the current workspace snapshot.
Solution: Record the failed attempt and require another retry. Do not duplicate fixes already present in current source: `PhysicsDeterminismSignals.cs` is compiled by Core, `FaunaBrain` imports `Hecton8.Physics` and defines `SanitizeFiniteInputFloat3`, `HectonPlayerMotor` has renamed KCC velocity locals, and `SubmarineAtmosphereSystem` copies `hit.AbsolutePosition` before by-ref validation.
Rejected Alternatives: Editing already-fixed current lines was rejected because it risks breaking other agents' concurrent changes. Claiming compile success was rejected because the build exited `1`. Treating duplicate generated csproj warnings as blocker fixes was rejected because the current hard failure is stale diagnostics, not duplicate items.
Scalability potential: No runtime behavior changes until a clean compile retry proves current source. Low/middle/high/ultra remain on the last green Core compile plus pending post-build slices.
Hardware Impact: Player-frame cost added is `0 us`. Build attempt T failed in `86.17s` with `6` errors and `4` warnings; no Data Monolith binary, schema, parser, or heap route changed.

## Decision 049 - Core Build Attempt U Compiler Server Isolation

Problem: Attempts S/T looked stale, but a stale compiler server could not be ruled out.
Solution: Wait for a legal gate, run `dotnet build-server shutdown`, then run Core build with `/p:UseSharedCompilation=false`. The build still failed on current-source mismatches: `HasRequiredResources` reported missing while the current class defines it, and `PhysicsDeterminismSignals` reported missing while the source is included and imported.
Rejected Alternatives: Patching existing definitions again was rejected because it would be noise and risk conflicting with active agents. Deleting generated csproj duplicate warnings was rejected because the generated project warns but the current hard failure is still stale source mismatch. Claiming build proof was rejected because exit code is `1`.
Scalability potential: No gameplay route changed. Low/middle/high/ultra remain blocked only on compile evidence for post-build slices, not on Data Monolith binary/fuzzer/load proof.
Hardware Impact: Player-frame cost added is `0 us`. Build attempt U failed in `52.77s` with `4` errors and `5` warnings after compiler-server shutdown; no schema/parser/heap route changed.

## Decision 050 - Core Build Closure N/O/P/Q/R

Problem: Slices N/O/P/Q/R had source/static proof but remained compile-pending after stale/raced build attempts S/T/U. Leaving them pending would be honest but incomplete now that a legal build window opened.
Solution: Recheck the local build gate at CPU `38%` with `0` active `dotnet/csc/VBCSCompiler`, shut down build servers, and rebuild `Hecton8.Core.csproj` with shared compilation disabled. The current snapshot compiled with `0` errors. Reports N/O/P/Q/R were upgraded to Core compile pass and a closure report was written.
Rejected Alternatives: Claiming the earlier stale attempts as success was rejected. Editing generated `Hecton8.Core.csproj` to remove duplicate-source warnings was rejected because the project file is generated and the warnings are pre-existing project hygiene, not Data Monolith parser/layout/runtime defects. Running Unity player or full solution proof was rejected here because the available gate only authorized Core compile; Unity profiler proof still requires the Editor/player pipeline.
Scalability potential: Low/development-player builds no longer carry the Visual Omega source text reader, Wrist HUD editor CSV scratch, or thermodynamics editor CSV worker allocation. Middle keeps editor authoring. High and Ultra keep the same binary/static-data route without changing DTO layout, save identity, or gameplay authority.
Hardware Impact: Player-frame cost added is `0 us`. Core build proof: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false` PASS, `0` errors, `5` CS2002 duplicate-source warnings, elapsed `00:01:24.55`. Removed player heap staging remains `8192` bytes per Wrist HUD instance plus `4096` bytes per thermodynamics runtime worker. Unity player/profiler proof remains pending.

## Decision 051 - Fail-Closed Runtime Simulation Probe

Problem: Existing evidence proved compiler validation and two corrupt resident checks, but did not directly prove that a corrupt candidate blob cannot overwrite the last valid resident publish state. The missing proof was explicit poison-publish prevention for bad XXHash3, table/range corruption, unaligned offsets, and truncation.
Solution: Add `DataMonolithFailClosedProbe` to the DataMonolith CLI. It uses two 64-byte aligned native resident buffers: one published baseline and one corrupt candidate. Each candidate is validated before publish; publish count and checksum are only advanced on a valid candidate. The probe runs six corrupt cases and 256 baseline resident validations, then writes `Docs/Reports/DATA_MONOLITH_FAIL_CLOSED_RUNTIME_SIM_X_002.json`.
Rejected Alternatives: Extending only the editor fuzzer was rejected because it proves file validation, not publish gating. Throwing exceptions on corrupt blobs was rejected because loader failure must be deterministic and telemetry-friendly. Clearing the active resident state on corrupt candidate failure was rejected because preserving the last known-good blob is safer than publishing poison or empty data. Keeping the probe's first validator version was rejected after inspection because it required a fixed section-table offset earlier than runtime does; it was corrected to validate header/directory consistency and table range like the runtime path.
Scalability potential: Low/middle/high/ultra share one deterministic static-data truth route. Weak devices avoid undefined AI/physics reads because corrupt candidates never publish. High and Ultra can load richer static sections later without changing the fail-closed gate.
Hardware Impact: Player-frame cost added is `0 us`. CLI proof: `DataMonolithBakeCli.exe C:\hades\Hecton8` PASS, exit `0`; fail-closed report status `PASS_FAIL_CLOSED_NO_POISON_PUBLISH`; six corrupt candidates rejected; final publish count stayed `1`; checksum stayed `0x0D49885F30E5DF35`; 256 resident validations allocated `0` bytes with mean `220.780 us` after the parser-absence rerun. Refreshed native read stress: `240.500 us` read, `590.054 us` read+validate estimate, heap `0`. Unity player/profiler proof remains pending.

## Decision 052 - Player Parser-Absence Closure

Problem: The fail-closed proof closed corrupt-blob publish safety, but the stricter player parser-absence CLI still found 9 active player-compiled static-config CSV helper signatures. The release gate was already green, but helper methods such as `NextCsvToken`, `TrimCsvField`, `SplitCsvLine`, and `TryReadCsvBytesForLoad` still physically compiled into player assemblies in several owner systems.

Solution: Fence the remaining static-config CSV helper bodies and byte-reader facades under `UNITY_EDITOR` in Fauna steering profiles, FutureCommand sandbox tuning, AssetLifecycle cache rules, ChemicalInfluence emitter profiles, UtilityAI anxiety profiles, SignalWarden signal hot-swap tables, Terminal OS layout/decryption CSV import, and Async Buoyancy vehicle sampling profiles. Leave runtime math, telemetry dumps, binary constants, and simulation DTO paths active.

Rejected Alternatives: Weakening the scanner was rejected because the user asked for physical absence, not a softer audit. Deleting authoring import paths was rejected because designers still need editor CSV workflows. Fencing unrelated save/profile/mod persistence was rejected because those are user data ownership routes, not Data Monolith static-config truth.

Scalability potential: Low and development-player builds carry less parser/file-IO IL. Middle keeps editor authoring workflows. High and Ultra retain richer authored data through offline bake and binary/Vault routes without changing gameplay authority or DTO layout.

Hardware Impact: Player-frame cost added is `0 us`. Removed active player static-config parser surfaces in 8 runtime files. CLI proof: `Docs/Reports/DATA_MONOLITH_PLAYER_PARSER_ABSENCE_CLI_X_002.json` status `PASS_PLAYER_STATIC_CONFIG_PARSER_ABSENCE`, release/development blocking findings `0`, direct `FileStream.ReadByte` `0`. Core compile proof: PASS, `0` errors, `4` generated duplicate-source warnings, elapsed `00:00:30.23`. Latest load stress after rerun: native read `240.500 us`, native read+validate estimate `590.054 us`, heap `0`; fail-closed validation mean `220.780 us`, heap `0`. Unity player/profiler proof remains pending.

## Decision 053 - Player CSV Scratch/Staging Fence Slice S

Problem: After parser absence was green, a stricter player-memory scan found systems that no longer parsed static-config CSV in player but still compiled CSV scratch/staging into player memory: managed byte arrays in ToolKinematics and GlobalShader CSV override state, native CsvScratch lanes in UtilityAI cognition, Apex brain, Voxel A*, BaseAtmosphere, ToxicOutgassing, AdaptiveStem, DynamicMusic, VocalWarning, and a 1 MB VocalBank dialogue CSV scratch lane.

Solution: Fence editor-only CSV scratch handles, managed byte buffers, CSV metadata staging, path state, and parser helper constants behind `UNITY_EDITOR` while leaving runtime DTO buffers, binary banks, black-box dump writes, signal lanes, and simulation jobs active. For methods that must still exist in player, return deterministic false/default rather than allocating hidden text staging.

Rejected Alternatives: Weakening the parser scanner was rejected because the issue was not scanner noise; the memory lanes physically existed. Migrating every owner table into `static_data.h8bin` in this slice was rejected because audio, atmosphere, pathfinding, and AI owner schemas need route cards before becoming Data Monolith truth. Deleting editor import paths was rejected because designers still need authoring workflows.

Scalability potential: Low removes cold player heap/native scratch pressure and reduces boot/Vault reservation size. Middle keeps editor authoring. High and Ultra can still carry richer authored datasets through offline binary sections later without changing gameplay truth ownership or DTO layout.

Hardware Impact: Player-frame cost added is `0 us`. Minimum byte scratch removed from player compilation is `1122304` bytes, plus VocalBank metadata slots and BaseAtmosphere gas profile slots. Static proof: `Docs/Reports/DATA_MONOLITH_PLAYER_CSV_STAGING_FENCE_SLICE_S_X_002.json`; touched-file player-active token scan PASS `0`; preprocessor balance PASS; Core compile PASS `0` errors, `4` generated duplicate-source warnings, elapsed `00:00:59.78`. CLI proof: parser absence PASS, fail-closed publish simulation PASS, native resident load estimate `878.228 us`, heap `0`. Unity player/profiler proof remains pending.

## Decision 054 - Player CSV Scratch/Staging Fence Slice T

Problem: After slice S, a stricter follow-up found more player-compiled CSV scratch/state lanes that survived as passive memory or cold file fallback even when parser absence was green. The concrete residues were Kinetic Character rig CSV scratch, Fabrication timing CSV scratch, Construction deconstruction CSV scratch, UtilityAI Anxiety psychology CSV scratch, Symbiosis CSV override scratch/timestamps, Storm Propagation impact-profile CSV scratch, and Ocean Surface legacy weather fallback scratch.

Solution: Move those constants, handles, path strings, timestamp state, Vault allocations, release/default paths, and CSV scratch resolvers behind `UNITY_EDITOR`. Keep runtime DTO buffers, telemetry rings, binary legacy symbiosis link scratch, storm profile DTOs, ocean baked/default weather rows, and gameplay authority routes active. Ocean player startup now skips project-file legacy weather fallback and generates/uses runtime defaults instead of reading project data files.

Rejected Alternatives: Claiming parser absence was enough was rejected because passive scratch still consumes player memory/Vault IDs. Removing editor import paths was rejected because designers still need authoring workflows. Moving every owner table into `static_data.h8bin` in this slice was rejected because several touched systems need owner route cards before becoming monolith truth. Launching Core compile despite active compiler processes was rejected by the project build gate.

Scalability potential: Low removes more cold player scratch/Vault pressure and one project-file weather fallback. Middle keeps editor authoring. High and Ultra can later move these owner datasets into binary monolith sections without changing DTO layout or gameplay authority.

Hardware Impact: Player-frame cost added is `0 us`. Static proof: touched-file player-active CSV/parser/text-read token scan `0`, preprocessor balance PASS, focused diff-check PASS with LF/CRLF warnings only. CLI proof: parser absence PASS with release/development blocking `0`, fail-closed publish simulation PASS, native resident load estimate `635.606 us`, heap `0`. Core compile remains pending because a 450-second legal build wait stayed blocked by `7-10` active `dotnet/csc/VBCSCompiler` processes.

## Decision 055 - Core Namespace Wall Closure After Slice T

Problem: The post-slice-T Core build finally ran and exposed real current-source namespace gaps, not Data Monolith schema failures. `SubmarineAutoLevelBallastController`, `PlayerKinematicsRuntime`, and `HectonUnderwaterVisuals` referenced already-existing physics types without `Hecton8.Physics`; `SpatialAudioManager` referenced already-existing fatal pressure implosion types without `Hecton8.Atmosphere`.

Solution: Add only the missing namespace imports. No DTO layout, Data Monolith binary payload, parser fence, heap staging, runtime authority, or event route was changed. Re-run Core compile and the full DataMonolith CLI path after the import fixes.

Rejected Alternatives: Recreating `PhysicsDeterminismSignals`, `SubmarineFluidDynamics`, `PhysicsForceRouter`, or `FatalPressureImplosionEvent` was rejected because the types already existed and duplicate definitions would corrupt ownership. Hiding the references behind preprocessor guards was rejected because it would silently remove runtime behavior. Claiming the earlier compile-pending slice as green was rejected until the current build passed.

Scalability potential: Low/middle/high/ultra behavior is unchanged; this is compile integrity. The actual Data Monolith scalability remains binary-first static truth on low devices and richer editor-authored data for higher tiers.

Hardware Impact: Player-frame cost added is `0 us`; import-only source fix. Core proof: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false` PASS, `0` warnings, `0` errors, elapsed `00:02:05.11`. Refreshed CLI proof: fuzzer PASS `12/12`, parser release/development blocking `0`, direct `FileStream.ReadByte` `0`, fail-closed corrupt candidate rejection PASS, native resident load estimate `917.350 us`, heap `0`. Unity player profiler proof remains pending.

## Decision 056 - Unity GlobalDataVault Fail-Closed Closure

Problem: The Unity batch proof initially failed before it could prove runtime safety. The blockers were real: generic `VaultGenerationHandle<T>`/`VaultBufferHandle<T>`/`VaultSliceHandle<T>` used explicit layout that Unity's Mono loader rejects for generic structs; `SignalPayloadLayoutValidator` contradicted runtime SignalBus stride policy and rejected a valid 96-byte `SeismicSignal`; `H8StaticDataArena` saved `_vault`, then `ShutdownArenaOnly()` nulled it before the current load could allocate the Data Monolith payload buffer.

Solution: Change the pointer-free generic Vault handles to sequential fixed-size structs, align the editor signal validator with the runtime rule of positive 8-byte-aligned payloads up to 192 bytes, and restore the active Vault after internal arena cleanup before file/memory allocation. Add `H8DataMonolithGlobalDataVaultStressProbe` to execute the real Unity GlobalDataVault route, corrupt hot reload, corrupt cold boot, requested section resolution, and zero-managed-allocation resident reload checks.

Rejected Alternatives: Keeping explicit generic layout was rejected because Unity proved it cannot load the type. Weakening `SeismicSignal` was rejected because runtime already accepts the layout and the validator was stale. Allocating a separate fallback arena outside GlobalDataVault was rejected because it would bypass the required ownership route. Claiming Unity timing as player timing was rejected: Editor repeated reload is `3624.749 us`, while Release CLI native resident load is the target-speed proof at `563.294 us`, heap `0`.

Scalability potential: Low uses one binary payload in GlobalDataVault and fails before AI/physics section access on corrupt data. Middle keeps editor batch verification and authoring. High and Ultra can add larger static sections without changing the fail-closed gate, header contract, or section alignment.

Hardware Impact: Player-frame cost added is `0 us`. Core compile proof after fixes: PASS, `0` warnings, `0` errors, `00:00:30.19`. Unity batch proof: PASS, exit `0`, file load allocated `0` managed bytes, locked corrupt reload returned `ReadyLocked`, cold corrupt cases `6/6`, all requested block sections resolved/cache-line aligned. Release CLI target timing: native read `195.400 us`, validation mean `367.894 us`, native read+validate `563.294 us`, heap `0`.

## Decision 057 - Canonical Section Cursor Overlap Rejection

Problem: Range-only section validation still left one valid-hash corruption lane: a malicious or damaged section table could point a later section to an earlier 64-byte-aligned, in-range payload offset. XXHash3 would pass if recomputed over the altered payload, and AI/physics consumers could read the wrong static table without a crash at the loader boundary.
Solution: Add canonical cursor validation everywhere the monolith is accepted. For each non-empty section, `offset` must equal the expected cursor, and the cursor advances by `AlignUp(offset + sectionBytes, 64)`. The check now exists in the editor compiler, runtime `H8StaticDataArena`, CLI load/fail-closed probes, corruption fuzzer, and Unity GlobalDataVault stress probe. Added explicit `bad_section_overlap` cases in CLI and Unity.
Rejected Alternatives: Keeping range-only validation was rejected because it allowed table aliasing. Sorting the section table at load time was rejected because it would mutate the baked contract and hide compiler defects. Accepting arbitrary holes between sections was rejected because it would weaken deterministic offsets and make future binary diff/telemetry proof harder.
Scalability potential: Low rejects damaged static data before any gameplay table access. Middle keeps identical editor authoring flow. High and Ultra can add richer sections without changing DTO layout, checksum coverage, or the 64-byte cache-line section start contract.
Hardware Impact: Player-frame cost added is `0 us`; this is cold-load validation only. Latest Release CLI native resident load estimate is `516.306 us`, heap `0`. Fail-closed validation mean is `217.661 us`, heap `0`. Unity batch file load allocated `0` managed bytes and cold corrupt boot rejected `7/7`, including checksum-valid section overlap as `InvalidSectionTable`.

## Decision 058 - Localization Directory Corruption Closure

Problem: The Unity GlobalDataVault proof still lacked the checksum-valid localization-directory mismatch case. A corrupt directory could point localization metadata away from the canonical localization section while the payload checksum was recomputed, leaving a report gap even though normal section range checks were already hard.
Solution: Add `bad_localization_directory` to the CLI fuzzer, CLI fail-closed publish simulation, and Unity GlobalDataVault cold boot probe. The runtime acceptance path treats directory localization range mismatch as an invalid section table and blocks section access before publish. Also fixed the CLI fail-closed `failureCodeLegend` so code `9` is explicitly documented as `LocalizationDirectory`.
Rejected Alternatives: Treating localization as optional metadata was rejected because localization offsets are part of the header/directory contract and can poison UI/audio/static references. Repairing the directory at load time was rejected because that would hide a bad bake or corrupted binary. Claiming the previous `7/7` Unity proof was complete was rejected because the user explicitly requested offsets leading into void and complete header structure proof.
Scalability potential: Low devices reject corrupt localization metadata before any managed fallback or section read. Middle keeps editor localization authoring. High and Ultra can add larger localized pools without changing the binary acceptance contract.
Hardware Impact: Player-frame cost added is `0 us`; cold-load validation only. Latest CLI fail-closed validation mean is `225.195 us`, heap `0`. Final publish count remains `1` and checksum remains `0x0D49885F30E5DF35` after the corrupt localization candidate. Final Unity cold corrupt boot rejects `8/8` cases with `0` managed allocation per corrupt case.

## Decision 059 - Generated Project Compile Wall Closure

Problem: After the Unity proof, the generated `Assembly-CSharp.csproj` path still needed a clean compile proof. The first pass exposed real namespace/import gaps in `HectonSurfaceWeatherDirector`, `TraumaDispatcher`, and `PlayerExplorationTracker`, plus stale incremental diagnostics until MSBuild servers were shut down and Core was rebuilt.
Solution: Add only existing-owner aliases/imports: physics ocean/weather/buoyancy aliases in `HectonSurfaceWeatherDirector`, acoustic and EMP aliases in event consumers, and `System` for `ReadOnlySpan<T>` in `TraumaDispatcher`. Expose only cold read accessors required by already-existing consumers: dispatcher frame delta accessors and nearest active voxel volume query. Add the missing Outposts reference to `Hecton8.Core.Memory` instead of duplicating `IDataVault` contracts.
Rejected Alternatives: Duplicating physics contracts in local namespaces was rejected because one fact must have one owner. Hiding broken references behind preprocessor guards was rejected because it would silently remove runtime behavior. Treating Unity's successful batch as sufficient was rejected after `Assembly-CSharp` proved the generated dotnet graph still had source-visible gaps.
Scalability potential: Low/middle/high/ultra behavior is unchanged; this is compile integrity and route ownership. The Data Monolith remains a binary/Vault route on low-end hardware and a richer static section substrate for higher tiers.
Hardware Impact: Player-frame cost added is `0 us`; import/visibility/asmdef fixes only. Core rebuild proof: `0` warnings, `0` errors, `00:01:04.85`. `Assembly-CSharp.csproj` proof: `0` errors, `2` generated `MSB9008` warnings for absent `Hecton8.Input.csproj`, `00:00:20.77`. Final Release CLI target proof: native read `166.900 us`, validation mean `363.841 us`, native read+validate `530.741 us`, heap `0`.

## Decision 060 - Strict Header And Directory Contract Hardening

Problem: The prior loader rejected corrupt checksums, void offsets, overlap, and localization drift, but a checksum-valid header could still carry unknown flags, non-zero reserved fields, or a shifted section-table offset and fail later than the binary contract should allow. That is a data-sovereignty gap: a future flag bit or padding bit could become accidental runtime authority before the schema owner explicitly supports it.
Solution: Make the acceptance contract exact in all acceptance paths. Runtime `H8StaticDataArena`, editor compiler validation, CLI load stress, CLI fail-closed simulation, editor fuzzer, and Unity GlobalDataVault stress now require `flags == BlobFlagLittleEndian`, all header/directory reserved fields equal zero, and the header section table begins at the canonical fixed offset `HeaderSizeBytes + DirectorySizeBytes` (`128`). Added checksum-valid fuzzer and cold boot cases for `bad_header_unknown_flags`, `bad_header_reserved`, `bad_directory_reserved`, `bad_header_section_count`, and `bad_header_section_table_offset`.
Rejected Alternatives: Allowing unknown header flags was rejected because there is no schema negotiation route. Ignoring reserved fields was rejected because padding corruption must not become hidden control data. Allowing a variable section-table offset was rejected because this monolith format is fixed and canonical; future formats must increment schema/version, not stretch V1 silently.
Scalability potential: Low rejects malformed data at the first header/directory boundary before any AI, physics, audio, or crafting table read. Middle keeps editor authoring and richer reports. High and Ultra can add sections or a V2 schema later without weakening V1 deterministic offsets, DTO layout, save identity, or GlobalDataVault authority route.
Hardware Impact: Player-frame cost added is `0 us`; this is cold-load validation only. Current Release CLI proof: native read `145.900 us`, validation mean `344.380 us`, native read+validate `490.280 us`, heap `0`. Fail-closed publish simulation rejects `13/13` corrupt candidates with final publish count pinned at `1` and checksum `0x0D49885F30E5DF35`. Unity GlobalDataVault cold corrupt boot rejects `13/13` with `0` managed allocation per case and blocks section access after failure.

## Decision 061 - Player CSV Staging Fence Slice U

Problem: Parser absence was green, but a stricter touched-file scan still found player-active CSV ownership residue in physiology gas override state, plus more editor CSV scratch/path/parser state in physiology, camera juice, PDA projector, and drone fleet code. A separate current-source Core build also exposed owner import gaps for a physics impact contract and the submarine hull breach read model.
Solution: Fence the remaining authoring scratch, timestamps, legacy file fallback, parser helpers, and CSV path state behind `UNITY_EDITOR`; rename the runtime breathing-gas override state away from CSV ownership while keeping deterministic DTO semantics; add a source inventory probe to the DataMonolith CLI; stabilize the load-stress timing by using best-of-five native reads and validation batches. Close the audio compile wall with an existing owner alias for the submarine hull breach read model; verify the current PhysicsImpact contract uses existing owner imports rather than duplicating contracts.
Rejected Alternatives: Weakening the scanner was rejected because physical player absence was the requirement. Moving black-box dump FileStream writes behind editor was rejected because crash evidence is a runtime requirement and these are not `ReadByte`/config reads. Duplicating `ISubmarineHullBreachReadModel`, `AbsoluteUniversePosition`, or `BinaryBlittableSafeAttribute` was rejected because one fact must keep one owner.
Scalability potential: Low removes more passive player CSV scratch and keeps corrupt static data fail-closed before section access. Middle keeps editor authoring. High and Ultra can add richer binary sections later without changing the current V1 header, DTO layout, or player authority route.
Hardware Impact: Player-frame cost added is `0 us`. Current proof: touched-slice player-active static-config token scan `0`; Core build PASS `0` warnings/errors in `00:01:07.91`; DataMonolith CLI PASS; fuzzer PASS `18/18`; fail-closed corrupt cases `13`, publish count pinned at `1`; player parser release/development blocking `0`; direct `FileStream.ReadByte` `0`; native resident load estimate `404.596 us`, heap `0`. Assembly-CSharp was not rerun after this slice because external compiler processes stayed active through the legal build window.

## Decision 062 - Source Inventory V4 And Current Build Closure

Problem: Source inventory V3 still treated editor-fenced root and StreamingAssets CSV files as runtime risks, which polluted the proof boundary. A stricter player compile scan also showed `InputDispatcher` still compiled CSV scratch/staging fields into player even after parser absence was green. Current generated-project builds then depended on existing-owner Core Contracts / `IPhysicsService` route fixes in `BaseModule` and `HectonFloatingOrigin`.

Solution: Add V4 source inventory classification that evaluates release and development player preprocessor state per line, separates editor-only authoring CSV references from player-active static-config references, and records player-active code reference counts. Fence `InputDispatcher` CSV scratch handles, watcher state, staged profile state, path strings, and scratch Vault acquisition behind `UNITY_EDITOR`. Keep the generated-project closure on existing `Hecton8.Core.Contracts.Physics` / `IPhysicsService` routes; no physics contract duplication or runtime behavior deletion.

Rejected Alternatives: Weakening the scanner was rejected because the user asked for proof, not silence. Classifying every unreferenced root CSV as baked was rejected because `buhlmann_zh16_profiles.csv` has no active code refs but is still a root static-data file requiring owner disposition. Duplicating physics services or hiding consumers behind preprocessor guards was rejected because one fact must keep one owner and runtime behavior must stay visible. Claiming a fresh Unity batch was rejected because Loop 59 only reran CLI/Core/Assembly, not Unity Editor batch.

Scalability potential: Low removes more passive player CSV scratch and keeps production on binary/default DTO state. Middle keeps editor CSV authoring isolated. High and Ultra can add richer monolith sections later without changing V1 header identity, DTO layout, or GlobalDataVault authority.

Hardware Impact: Player-frame cost added is `0 us`. Current proof: source inventory V4 reports `218` CSV files, `0` StreamingAssets static-config risks, `1` repo-root static-config risk with `0` code refs, `42` monolith source CSVs, and `26/26` cache-line aligned sections. Parser absence release/development blocking findings `0`; direct `FileStream.ReadByte` `0`; fuzzer PASS `18/18`; fail-closed simulation PASS `13/13`; Release CLI native read `88.600 us`, validation mean `317.812 us`, native resident estimate `406.412 us`, heap `0`; Core build PASS `0` warnings/errors in `00:01:37.83`; `Assembly-CSharp.csproj` PASS `0` errors with `2` generated `MSB9008` warnings in `00:03:17.57`.

## Decision 063 - Source Inventory V5 Legacy Root Separation

Problem: V4 still reported one repo-root static-config risk even though the file was `buhlmann_zh16_profiles.csv` with no active code references. X_009 status/log evidence and `ShinobuPhysiologyRuntime.cs` show the active physiology route is `buhlmann_3tissue_profiles.csv`, and the active CSV parser/load helpers are under `UNITY_EDITOR`. Keeping the 16-row legacy comparison file in the player-risk bucket was false risk accounting.

Solution: Change `DataMonolithSourceInventoryProbe` schema to V5 and split root CSV classification: no player-active references plus any code references remains `editor_fenced_repo_root_authoring_csv`; no player-active references and no code references becomes `repo_root_unref_legacy_csv`; only player-active root CSV references stay `repo_root_csv_static_config_risk`. Add a dedicated total and authority label for owner disposition.

Rejected Alternatives: Deleting or moving `buhlmann_zh16_profiles.csv` was rejected because physiology ownership belongs to X_009 and the worktree is multi-agent dirty. Marking the file as baked was rejected because it is not a Data Monolith source table. Leaving it as runtime risk was rejected because parser-absence and reference scans prove no player route. Weakening the scanner globally was rejected.

Scalability potential: Low keeps static-config boot risk count honest and zero for player-active root CSVs. Middle keeps editor/reference data visible for owner cleanup. High and Ultra can later migrate physiology reference curves into a dedicated binary owner route without changing Data Monolith V1 header, section layout, or GlobalDataVault authority.

Hardware Impact: Player-frame cost added is `0 us`; runtime code unchanged. Current proof: source inventory V5 reports `218` CSV files, repo-root static-config risks `0`, repo-root unreferenced legacy CSVs `1`, StreamingAssets risks `0`, editor-fenced root authoring CSVs `8`, monolith source CSVs `42`, and `static_data.h8bin` bytes `1064384`. Parser absence release/development blocking findings `0`; direct `FileStream.ReadByte` `0`; fuzzer PASS `18/18`; fail-closed simulation PASS `13/13`; Release CLI native read `65.700 us`, validation mean `318.877 us`, native resident estimate `384.577 us`, native resident allocated bytes `0`. CLI build PASS with `0` errors and `38` known CLI stub/editor DTO warnings.
