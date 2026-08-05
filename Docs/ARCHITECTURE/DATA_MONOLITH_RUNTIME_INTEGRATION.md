# Data Monolith Runtime Integration

Status: STATIC_ROUTE_DOC / RUNTIME_PROOF_PENDING

Owner domain: data monolith / runtime integration

| Field | Value |
| --- | --- |
| Date | 2026-06-02 |
| Status | CORE BLOB BAKED; CORRUPTION STRESS PASS; FAIL-CLOSED RUNTIME SIM PASS; NATIVE READ ZERO-GC TARGET PASS |
| Gates | RELEASE STATIC-CONFIG PASS; PLAYER CSV STAGING FENCE PASS; DIRECT FILESTREAM READBYTE PASS |
| Pending | UNITY PROFILER PROOF |
| Owner | X_002 DATA_MONOLITH_ARCHITECT |
| Evidence | STATIC_DOC / STATIC_SOURCE / CLI_BAKE_HEADER_PROOF / CLI_CORRUPTION_FUZZER / CLI_RESIDENT_LOAD_STRESS / CLI_FAIL_CLOSED_RUNTIME_SIM / SOURCE_INVENTORY / RELEASE_BUILD_GATE_SOURCE / FILESTREAM_READBYTE_SCAN / PLAYER_CSV_STAGING_SCAN |

## Current Short Read

- Runtime loader source exists: `H8StaticDataArena`.
- Editor compiler source exists: `H8DataMonolithCompiler`.
- Current payload exists at `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- Current static file identity: `7,457,664` bytes, mtime 2026-06-07, measured 2026-08-05; SHA-256 `77785d6c25602141c71033cf96a42787e1415f06abe9cde3831675edb0045c53` (supersedes 2026-06-02 check: `1,804,864` bytes, SHA-256 `4f40185a758263405bf6d4d95f04ea742fae501625db41eaaafd4d5b15f6c000`).
- Historical CLI stress/fail-closed reports are retained below as evidence snapshots.
- Unity import, player boot, profiler, GCMonitor, device, and save/load route proof remain pending unless a newer artifact is cited.

## Runtime Contract

The runtime loader consumes `Hecton8/DataMonolith/static_data.h8bin` from StreamingAssets through `H8StaticDataArena`.

Binary contract after X_002 pass:

- 64-byte explicit `H8DataBlobHeader`
- 64-byte explicit `H8DataBlobDirectory`
- 16-byte `H8DataSectionEntry` rows
- 64-byte cache-line section start alignment
- 16-byte fixed-record alignment, except `LocalizationUtf8` byte stream records
- XXHash3-64 checksum over bytes `[64..blobLength)`
- little-endian flag and schema hash in the header
- fixed 300-entry Data Monolith telemetry ring

Current artifact:

- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`
- bytes: `7457664`
- mtime: 2026-06-07
- SHA-256 on 2026-08-05 static file check: `77785d6c25602141c71033cf96a42787e1415f06abe9cde3831675edb0045c53`
- superseded 2026-06-02 static file check: bytes `1804864`, SHA-256 `4f40185a758263405bf6d4d95f04ea742fae501625db41eaaafd4d5b15f6c000`
- header/section fields below are from the 2026-06-02 parse and were not re-parsed on 2026-08-05
- magic: `0x4D443848`
- version: `2`
- header bytes: `64`
- checksum64: `0xA85210353432862A`
- data start offset: `576`
- section count: `28`
- schema hash: `0x33313332`

Data Monolith readiness:

- State: `CORE BLOB BAKED / CORRUPTION STRESS PASS / RELEASE CLI LOAD TARGET PASS / UNITY PROFILER PROOF PENDING`.
- CLI fallback materialized and validated the Data/Balance blob.
- `Docs/Reports/DATA_MONOLITH_CORRUPTION_FUZZER_X_002.json` passed 12/12 corruption cases.
- Unity player profiler proof is not claimed.

Runtime allocation note:

- Payload storage is native `GlobalDataVault` arena-backed.
- Windows Standalone/Editor attempts native `CreateFileW`/`ReadFile` into the Vault arena before MMF/FileStream fallback.
- Unity player-profiler proof is still required before final readiness language.

Resident load stress evidence:

- `Docs/Reports/DATA_MONOLITH_LOAD_STRESS_X_002.json`
- Release CLI status: `PASS_NATIVE_READ_ZERO_GC_TARGET_TIME`
- Scope: native file read plus resident pointer validation in CLI; not a real Unity player profiler trace.
- Historical report blob bytes: `1064384`; current 2026-08-05 blob is `7457664`, so load timing must be rerun before citing it as current.
- Native file read: `276.300 us`, heap `0 bytes`
- Full resident validation mean across `1024` iterations: `601.928 us`, heap `0 bytes`
- Native read+validate estimate: `878.228 us`, heap `0 bytes`
- Target threshold: `<1000 us`, `targetLoadMet=true`
- Managed file-read comparison is retained in the report as the heap-allocating baseline, not the runtime target.
- Forced corruption checks: `badChecksumRejected=true`, `badOffsetRejected=true`, failure codes `BadChecksum` and `SectionOutOfRange`

Fail-closed resident publish evidence:

- `Docs/Reports/DATA_MONOLITH_FAIL_CLOSED_RUNTIME_SIM_X_002.json`
- Release CLI status: `PASS_FAIL_CLOSED_NO_POISON_PUBLISH`
- Scope: resident-pointer simulation of the `H8StaticDataArena` publish gate; not a real Unity player profiler trace.
- Historical baseline checksum: `0x19D880780D6E1B46`; current 2026-06-01 blob checksum is `0xA85210353432862A`, so this simulation must be rerun before citing it as current.
- Baseline publish count: `1`
- Corrupt candidates rejected before publish: bad stored checksum, bad payload checksum, section range out of bounds, unaligned section offset, section table pointing into void, truncated blob.
- Final publish count after all corrupt candidates: `1`
- Historical final checksum after all corrupt candidates: `0x19D880780D6E1B46`
- Resident validation: `256/256`, mean `382.461 us`, heap `0 bytes`

Binary layout evidence:

- `Docs/Reports/DATA_MONOLITH_BINARY_LAYOUT_X_002.json`
- Header fields are little-endian explicit byte writes.
- Validation flags: `headerBlobBytesMatchesFile=true`, `directoryBlobBytesMatchesFile=true`, `dataStart64Aligned=true`, `allSectionOffsets64Aligned=true`, `allFixedRecords16Aligned=true`, `allSectionsInBlobRangeOrEmpty=true`, `littleEndianFlagSet=true`.
- Requested block groups are mapped in the report under `Ecology`, `Crafting`, `Audio`, and `Physiology`.

Parser isolation evidence:

- `Docs/Reports/DATA_MONOLITH_LOCAL_PARSER_ISOLATION_X_002.json`: Data Monolith runtime CSV/text parser matches `0`, runtime `FileStream.ReadByte` matches `0`; CSV parser code is in `Assets/_Project/Scripts/Editor/DataMonolith`, fenced by `#if UNITY_EDITOR` and an Editor-only asmdef.
- `Docs/Reports/DATA_MONOLITH_RELEASE_BUILD_GATE_X_002.json`: Editor-only `IPreprocessBuildWithReport` release gate is installed.
- Static parity report: `PASS_RELEASE_PARSER_GATE`, `0` blocking static-config parser findings.
- Remaining text/JSON/file reads in that report are classified user save/profile/mod persistence, outside Data Monolith static-config ownership.
- `DATA_MONOLITH_PLAYER_PARSER_ABSENCE_CLI_X_002.json`: player symbol scan `PASS_PLAYER_STATIC_CONFIG_PARSER_ABSENCE`.
- Release blocking findings: `0`.
- Development blocking findings: `0`.
- Direct `FileStream.ReadByte`: `0`.
- Remaining 9 text/JSON reads are documented save/profile/mod persistence.
- They are outside Data Monolith static-config ownership.
- `Docs/Reports/DATA_MONOLITH_PLAYER_PARSER_ABSENCE_CLOSURE_X_002.json`: closure proof for the last 9 static-config CSV helper leaks; 8 runtime files fenced, CLI pass, Core compile pass.
- `Docs/Reports/DATA_MONOLITH_PLAYER_CSV_STAGING_FENCE_SLICE_S_X_002.json`:
  - Scope: player CSV scratch/staging closure after parser absence.
  - Files touched: `11`.
  - Moved behind `UNITY_EDITOR`: at least `1122304` bytes of player CSV scratch, VocalBank metadata slots, BaseAtmosphere gas profile slots.
  - Touched-file player-active CSV/parser/text-read token scan: `0`.
  - Core compile proof: PASS with `0` errors.
- `Docs/Reports/DATA_MONOLITH_FILESTREAM_READBYTE_SCAN_X_002.json`: direct production-path `FileStream.ReadByte` / `stream.ReadByte` matches are `0`; remaining `ReadByte` symbols are editor-only tools, memory accessors, or custom binary readers.
- `Docs/Reports/DATA_MONOLITH_EDITOR_ONLY_CSV_FENCE_SLICE_X_002.json`: 41 parser-heavy static-config bridge target files, producing 37 current code diffs, were narrowed from `#if UNITY_EDITOR || DEVELOPMENT_BUILD` to `#if UNITY_EDITOR` in the touched slice.
- `Docs/Reports/DATA_MONOLITH_EDITOR_ONLY_CSV_FENCE_SLICE_B_X_002.json`: second 32-file target slice; 25 current code diffs.
- Slice B result: stricter than the non-development release gate; removes those CSV/text parser bridges from Development Build player code.
- Slice B compile proof remains pending the CPU/compiler gate.
- `Docs/Reports/DATA_MONOLITH_EDITOR_ONLY_CSV_FENCE_SLICE_C_X_002.json`:
  - Slice: 28 target files.
  - Output: 23 current code diffs, 55 guard replacements.
  - Domains: atmosphere, construction, equipment, rendering, physics, power, world profile bridges.
  - Compile proof: pending CPU/compiler gate.
- `Docs/Reports/DATA_MONOLITH_EDITOR_ONLY_CSV_FENCE_SLICE_D_X_002.json`:
  - Slice: fourth target slice, 40 files.
  - Output: 30 current code diffs, 71 guard replacements.
  - Domains: animation, audio DSP, construction, crafting, ecosystem, gameplay, graphics, voxel, interaction, inventory, physics, physiology, power, ocean/rendering profiles.
  - Rebuild after slice D exposed and fixed unrelated `HectonOSBootManager` missing `System.Text`.
  - Final rebuild: pending CPU/compiler gate.
- `Docs/Reports/DATA_MONOLITH_EDITOR_ONLY_CSV_FENCE_SLICE_E_X_002.json`:
  - Slice: fifth target slice, `22` files.
  - Output: `18` current code diffs, `31` guard replacements.
  - Domains: procedural IK, cartography, vault archaeology, scanner data mining, physics, suit integrity, QA, quest, DRS, thermodynamics, tools, sonar, VFX, decal vault.
  - Core rebuild after slices D/E and compile-wall fixes: pending CPU/compiler gate.
- `Docs/Reports/DATA_MONOLITH_EDITOR_ONLY_CSV_FENCE_SLICE_F_X_002.json`:
  - Slice: sixth target slice, `5` files.
  - Output: `5` current code diffs, `8` guard replacements.
  - Fixed: Terminal OS development-player CSV monitor mismatch.
  - Narrowed to editor-only: Flora genome, procedural geology, terrain streaming profile, SpatialAudio acoustic CSV/LUT fallback bridges.
  - Core rebuild after slices D/E/F and compile-wall fixes: pending CPU/compiler gate.
- `Docs/Reports/DATA_MONOLITH_EDITOR_ONLY_CSV_FENCE_SLICE_G_X_002.json`:
  - Slice: seventh target slice, `4` files.
  - Output: `4` current code diffs, `5` guard replacements, one helper-function fence.
  - Narrowed to editor-only: biome atmosphere CSV ingest, flora stiffness CSV reload, world streaming profile parsing, flora ambient sway biome parser helpers.
  - Core rebuild after slices D/E/F/G and compile-wall fixes: pending CPU/compiler gate.
- `Docs/Reports/DATA_MONOLITH_EDITOR_ONLY_CSV_FENCE_SLICE_H_X_002.json`: eighth 5-file target slice.
- Output: 5 current code diffs and 5 guard replacements.
- Scope narrowed to editor-only: world chunk streaming profile text import, flora genome CSV overrides, PDA scanner profile import, bootstrap memory overrides, AUP floating-origin tuner reload.
- Post-slice Core compile proof: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` passed after slices A-H and compile-wall fixes with `0` warnings, `0` errors, elapsed `108.34s`.
- `Docs/Reports/DATA_MONOLITH_DEV_BUILD_CSV_FENCE_SLICE_I_X_002.json`:
  - Slice: ninth static-config/development-build slice.
  - Fixed symbol model: `DEVELOPMENT_BUILD` and `DEBUG` now match scan mode.
  - Split release/development report paths.
  - Narrowed telemetry/DataVault/rollback netcode/save-compression/Merkle/WAL CSV parser routes to `UNITY_EDITOR`.
  - Touched-file broad development CSV guards: `0`.
  - Core build proof: PASS, `0` warnings, `0` errors.
- `Docs/Reports/DATA_MONOLITH_DEV_BUILD_CSV_FENCE_SLICE_J_X_002.json`: tenth slice; fenced Ballistics, Metabolism, telemetry endpoint, terrain streaming, nutrient/radiation/sensory/ocean/shoreline/predator acoustic, and Haptic CSV ingest to `UNITY_EDITOR`.
- Slice J proof: remaining negated broad-development static-config candidates `0`; production-path direct FileStream/stream `ReadByte` matches `0`; Core build PASS, `0` warnings, `0` errors.
- `Docs/Reports/DATA_MONOLITH_DEV_BUILD_CSV_FENCE_SLICE_K_X_002.json`: eleventh slice; fenced player-compiled StressDirector, VocalWarning, FoundationSnapping, and BaseAtmosphere CSV helper bodies.
- Slice K proof: development-player touched static-config/file/parser hits `0`; Core build PASS, `0` warnings, `0` errors.
- `Docs/Reports/DATA_MONOLITH_ZERO_GC_COLD_BINARY_FIX_SLICE_L_X_002.json`:
  - Change: `FaunaKinematicsRuntime` rig-definition read buffer moved from managed `4096` bytes to `stackalloc Span<byte>`.
  - Scope: outside CSV parser isolation.
  - Proof: Core build PASS, `0` warnings, `0` errors.
- `Docs/Reports/DATA_MONOLITH_ZERO_GC_COLD_BINARY_FIX_SLICE_M_X_002.json`:
  - Change: `VolcanicUpdraftDirector` legacy vent record buffer moved from managed `64` bytes to `stackalloc Span<byte>`.
  - Readers: span-based little-endian.
  - Scope: outside CSV parser isolation.
  - Proof: Core build PASS, `0` warnings, `0` errors.
- `Docs/Reports/DATA_MONOLITH_CORE_BUILD_CLOSURE_I_TO_M_X_002.json`: Core project compile closure after slices I/J/K/L/M, `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` PASS, `0` warnings, `0` errors, `58.77s`.
- `Docs/Reports/DATA_MONOLITH_DEV_BUILD_TEXT_READ_FENCE_SLICE_N_X_002.json`:
  - Change: narrowed `VisualOmegaSmokeTester` source/shader audit from `UNITY_EDITOR || DEVELOPMENT_BUILD` to `UNITY_EDITOR`.
  - Focused scanner: 302 candidate non-editor files, `findingCount=0`.
  - Extended scanner: 1332 parser/file-IO/static-config token files.
  - Blocking findings: `0` in release and development player symbol models.
  - Documented persistence findings: 12 save/profile/mod cases per mode.
  - Core compile proof: PASS with `0` errors and 5 generated duplicate-source warnings.
- `Docs/Reports/DATA_MONOLITH_MEMORY_INGEST_FAIL_TELEMETRY_SLICE_O_X_002.json`: memory-ingest parity fix.
- Added failure telemetry/dump requests to early `H8StaticDataArena.TryInitializeFromMemory` corrupt-input and arena-failure returns.
- Memory-ingest now matches file-path boot validation observability.
- Core compile proof: PASS, `0` errors, 5 generated duplicate-source warnings.
- `Docs/Reports/DATA_MONOLITH_CORE_COMPILE_WALL_CLOSURE_P_X_002.json`: span compile-wall closure.
- Fixed `SubtitleManager` notification enqueue and `LocalizationManager` plural fallback span lifetime.
- No managed allocation, parser code, schema change, or Data Monolith layout change.
- Core compile proof: PASS, `0` errors, 5 generated duplicate-source warnings.
- `Docs/Reports/DATA_MONOLITH_PLAYER_HEAP_STAGING_FIX_SLICE_Q_X_002.json`: player heap staging fix.
- Moved `WristHologramHudRuntime` editor/manual font-metrics CSV scratch `byte[8192]` behind `UNITY_EDITOR`.
- Import helpers were already editor-only; this removes unused player allocation.
- Core compile proof: PASS, `0` errors, 5 generated duplicate-source warnings.
- `Docs/Reports/DATA_MONOLITH_PLAYER_HEAP_STAGING_FIX_SLICE_R_X_002.json`:
  - moved `ThermodynamicsHazardGridRuntime.FileWorker` editor CSV worker `byte[4096]` allocation behind `UNITY_EDITOR`;
  - preserved runtime `thermodynamic_constants.h8bin` binary lane;
  - Core compile proof PASS with `0` errors and 5 generated duplicate-source warnings.
- `Docs/Reports/DATA_MONOLITH_CORE_BUILD_ATTEMPT_S_X_002.json`: legal Core build attempt failed on a stale `ConstructionRuntimeProxyFactory.cs` `Hecton8.Graphics` import that is absent from the current source snapshot. No compile pass claimed; retry required.
- `Docs/Reports/DATA_MONOLITH_CORE_BUILD_ATTEMPT_T_X_002.json`: second legal Core build failed on stale mismatches in `FaunaBrain`, `HectonPlayerMotor`, and `SubmarineAtmosphereSystem`. Retry required.
- `DATA_MONOLITH_CORE_BUILD_ATTEMPT_U_X_002.json`: third legal Core build attempt ran after `dotnet build-server shutdown`.
- It used `/p:UseSharedCompilation=false`.
- Result: failed on current-source mismatches.
- No compile pass claimed.
- Retry required after concurrent churn settles.
- `Docs/Reports/DATA_MONOLITH_CORE_BUILD_CLOSURE_N_TO_R_X_002.json`: current Core project compile closure after slices N/O/P/Q/R. Command `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false` PASS, `0` errors, `5` generated duplicate-source warnings, elapsed `00:01:24.55`.

Cross-domain CSV migration is not complete. `Docs/Reports/DATA_MONOLITH_SOURCE_INVENTORY_X_002.json` records:

- `215` CSV files on disk
- `125` data/asset/root CSVs
- `18` active Data/Balance tables in the current monolith bake lane
- `22` Data/Balance schema templates
- `2` allowed external Data/Balance CSVs not sectioned into the blob
- `3` StreamingAssets CSV runtime risks
- `8` repo-root CSV runtime risks
- `70` cross-domain authoring CSVs requiring owner route cards before migration
- `68` docs/archive/report CSVs outside runtime truth

## Ownership

- immutable static data belongs to Data Monolith
- mutable save data belongs to the save container / paging protocols
- cross-domain native runtime buffers belong to `GlobalDataVault`
- Addressables or visual asset groups are delivery mechanisms, not gameplay truth stores

Bootstrap route:

- `GameBootstrapper.InitializeBootstrapDataMonolith` runs during MemoryPreWarm after `_globalDataVault` creation.
- `H8StaticDataArena.TryInitializeFromStreamingAssets(IDataVault, ...)` receives the bootstrap-owned Vault explicitly.
- 1330 RERUN4 hardening: bootstrap now calls `H8StaticDataArena.TryInitializeFromStreamingAssetsAsync(...)` from the existing MemoryPreWarm `Awaitable` phase.
  URL-backed StreamingAssets paths are staged through `UnityWebRequest` with frame-yielding `AwaitableDebtMonitor.NextFrameAsync`, not a blocking `Thread.Sleep` spin.
  The async route enters `Awaitable.MainThreadAsync` before platform-specific path handling so Unity API access and player-symbol branches keep an explicit Awaitable continuation.
  The legacy synchronous `TryInitializeFromStreamingAssets(...)` route now accepts direct filesystem paths only and fails closed for URL-backed StreamingAssets paths.
- Data Monolith buffers are named in the central `BufferID` ledger: `DataMonolithPayload`, `DataMonolithTelemetryRing`, `DataMonolithTelemetryCursor`.
- 1330 hardening: release player loading is not Windows-only.
  Windows keeps the native `CreateFileW` path.
  Non-WebGL players use the same binary validation path through `Application.streamingAssetsPath`.
  Android player builds bypass generic JAR URL staging for the Data Monolith and use the 1504 NDK `AAssetManager` source-plugin route below.
  WebGL remains fail-closed until a zero-copy browser staging route exists; managed `DownloadHandlerBuffer.data` hydration is rejected for the runtime static-data contract.
- 1330 RERUN8 hardening: the release parser gate now distinguishes native PAL support from production H8BIN loader support.
  `StandaloneWindows`, `StandaloneWindows64`, `StandaloneLinux64`, `StandaloneOSX`, `Android`, and `iOS` are allowed production loader targets; WebGL and unlisted targets remain blocked.
  StreamingAssets cancellation in the async loader returns `ReadFailed` and records `PathFlagStreamingUriStagingCancelled`; it no longer throws a managed cancellation exception from the Data Monolith boot path.
- 1330 hardening: Data Monolith DTOs require `StructLayout(LayoutKind.Explicit, Pack = 1, Size = ...)`; `H8DataMonolithLayoutGuard` rejects missing Pack=1 before bake/import proof.
- 1504 Android PAL hardening: Android player builds no longer route the Data Monolith through URL staging. `H8StaticDataArena` obtains Unity's Java `AssetManager`, passes it to the NDK bridge, and reads `static_data.h8bin` with `AAsset_read` directly into the `GlobalDataVault` payload pointer. The bridge probes `AAsset_openFileDescriptor64` first; compressed or non-FD-backed `h8bin` entries fail closed before hydration. Gradle must keep `h8bin` in `noCompress`; ProjectSettings must keep `AndroidTargetArchitectures: 2` and `androidSplitApplicationBinary: 0` for the source-plugin APK route.
- Unity GameActivity remains allowed for this route because the Android loader uses bounded raw JNI calls for `currentActivity.getAssets()` and does not depend on Java `Looper`, `myLooper`, or `Handler` APIs from the native player-loop thread.

## Failure Rule

Runtime boot must fail closed or enter a documented fallback if the H8DM payload is required and missing.

Silent fallback to generated defaults is rejected unless a route card names owner, version, checksum, and diagnostic artifact.

## Verification Wall

Latest completed project compile pass:

- Command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false`.
- Result: `0` errors, `4` CS2002 duplicate-source warnings, `00:00:59.78`.
- Covered scope:
  - Loop 50 player CSV staging fence.
  - Previous Loop 49: green, `0` errors, `4` duplicate-source warnings, `00:00:30.23`.
  - Previous N/O/P/Q/R: green, `0` errors, `5` duplicate-source warnings, `00:01:24.55`.
  - Previous I-M: green, `0` warnings, `0` errors, `58.77s`.
  - Previous A-H: green, `0` warnings, `0` errors, `108.34s`.
- Warning status: generated `Hecton8.Core.csproj` duplicate-source warnings for Input, UniversalInputStateSignal, and Audio remain project hygiene debt, not Data Monolith defects.
- Narrow DataMonolith Release CLI build: green with `0` errors and `38` warnings in editor JSON DTO/stub fields.
- DataMonolith CLI pipeline execution: `DataMonolithBakeCli.exe C:\hades\Hecton8` PASS, exit `0`, after bake, corruption fuzzer, load stress, fail-closed runtime simulation, and player parser-absence scan.
- Missing proof: Unity import, player build, profiler.
