# HECTON-8 Documentation Actuality Ledger

Date: 2026-05-24
Status: PENDING VERIFICATION
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Evidence class: STATIC_DOC / STATIC_SOURCE / STATIC_FILESYSTEM

This ledger is the active documentation-change register. Full historical text is archived at `../_Archive/Architecture_X_012_APEX_2026-05-23/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.full.md`.

## Source Constants

| Contract | Current value | Source / proof |
|---|---:|---|
| Save writer version | `0x000B` | `Assets/_Project/Scripts/SaveBinaryStorage.cs` |
| Save header size | `56` bytes | `SaveBinaryStorage.CurrentHeaderSize` |
| Legacy save header size | `44` bytes | `SaveBinaryStorage.LegacyHeaderSize` |
| H8DM header size | `64` bytes | `Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs` |
| H8DM directory record size | `64` bytes | `H8DataLayoutConstants.DirectoryRecordSizeBytes` |
| Data Monolith payload | `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` | present; `1,064,384` bytes in X_012 scan |
| Signal lane capacity | `512` | `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs` |
| Scalability DTO | `16` bytes | `ScalabilityStateDTO` static source |
| AUP/blit struct | `48` bytes | AUP static source |

Prompt/report values that disagree with source are stale. Current source wins.

## X_012 2026-05-23 Pass

| Area | Active action | Proof |
|---|---|---|
| Root docs | Kept only `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, `BUILD_PLAYTEST_ISSUES.md` as root text anchors | `Docs/Reports/DOCUMENTATION_OPTIMIZATION_REPORT_X_012.json` |
| Root bloat | Archived full old root anchors; rewrote active anchors as concise status files | `Docs/DEPRECATED/Root_Bloat_X_012_2026-05-23/` |
| Historical reports | Moved `160` top-level report text files out of active corpus | `Docs/_Archive/Reports_X_012_2026-05-23/MANIFEST.md` |
| Stale Data Monolith reports | Moved stale absence-era reports to deprecated archive | `Docs/DEPRECATED/X_012_Stale_DataMonolith_Reports_2026-05-23/` |
| Active indexes | Updated root, reports, architecture, deprecated, and archive indexes | `Docs/README.md`, `Docs/Reports/README.md`, `Docs/_Archive/README.md` |
| Structure gate | Added root policy, links, duplicate headers, fences, stale constants, and UTF-8-SIG checks | `Tools/VerifyDocStructure.py` |
| Reduction gate | Added source-sync and word-reduction proof JSON | `Tools/OOP_Doc_Scanner.py` |

## APEX Architecture Pass

| Area | Active action | Proof |
|---|---|---|
| Binary payload ledger | Replaced verbose run log with active source-constant index | `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` |
| Binary payload archive | Preserved full pre-compression ledger | `../_Archive/Architecture_X_012_APEX_2026-05-23/BINARY_PAYLOAD_INTEGRATION_LEDGER.full.md` |
| Payload records | Extracted `288` boundary records to machine-readable JSON | `../Reports/BINARY_PAYLOAD_LEDGER_CONCISION_X_012.json` |
| Architecture boilerplate | Removed repeated global-boundary boilerplate from active architecture specs | `Docs/Reports/DOCUMENTATION_OPTIMIZATION_REPORT_X_012.json` |
| Long prose | Converted overlong narrative paragraphs to tables/lists | `architecture.longNarrativeParagraphCount = 0` in the optimization report |
| This ledger | Archived full historical ledger and kept this active register concise | `../_Archive/Architecture_X_012_APEX_2026-05-23/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.full.md` |
| Strict paragraph pass | Rewrote unstructured architecture paragraphs over `90` words into bullet/list structure | `../Reports/ARCHITECTURE_CONCISION_AUDIT_X_012.json` |
| Strict line pass | Split architecture list/table lines over `70` words | `../Reports/ARCHITECTURE_LINE_CONCISION_AUDIT_X_012.json` |
| File-cap pass | Compressed active architecture files over `2500` words and archived full snapshots | `../Reports/ARCHITECTURE_FILE_CAP_AUDIT_X_012.json` |
| Residual prose pass | Archived active `.diff` provenance, converted residual prose to lists, and tightened paragraph/sentence gates to `55`/`35` words | `../Reports/ARCHITECTURE_RESIDUAL_PROSE_AUDIT_X_012.json` |
| Manual prose pass | Manually removed residual document-voice markers and compressed README compile-history row | `../Reports/ARCHITECTURE_MANUAL_PROSE_AUDIT_X_012.json` |
| Manual density pass | Split remaining near-threshold list/table lines and removed residual marker text by hand | `../Reports/ARCHITECTURE_MANUAL_DENSITY_AUDIT_X_012.json` |
| Micro-density pass | Manually split residual architecture paragraphs `>=55` words and structured lines `>=60` words | `../Reports/ARCHITECTURE_MICRO_DENSITY_AUDIT_X_012.json` |
| Ultra-density pass | Manually split residual architecture paragraphs and structured lines `>=50` words; removed final marker hit | `../Reports/ARCHITECTURE_ULTRA_DENSITY_AUDIT_X_012.json` |
| 45-word density pass | Manually split residual architecture paragraphs and structured lines `>=45` words | `../Reports/ARCHITECTURE_45WORD_DENSITY_AUDIT_X_012.json` |
| 40-word density pass | Manually split residual architecture paragraphs and structured lines `>=40` words | `../Reports/ARCHITECTURE_40WORD_DENSITY_AUDIT_X_012.json` |
| 35-word density pass | Manually split residual architecture blocks and made scanner fail at `>34` words | `../Reports/ARCHITECTURE_35WORD_DENSITY_AUDIT_X_012.json` |
| 34-word density pass | Manually split residual architecture blocks and made scanner fail at `>33` words | `../Reports/ARCHITECTURE_34WORD_DENSITY_AUDIT_X_012.json` |
| 33-word density pass | Manually split residual architecture blocks and made scanner fail at `>32` words | `../Reports/ARCHITECTURE_33WORD_DENSITY_AUDIT_X_012.json` |
| 32-word density pass | Manually split residual architecture blocks and made scanner fail at `>31` words | `../Reports/ARCHITECTURE_32WORD_DENSITY_AUDIT_X_012.json` |
| 31-word density pass | Manually split residual architecture blocks and made scanner fail at `>30` words | `../Reports/ARCHITECTURE_31WORD_DENSITY_AUDIT_X_012.json` |
| 30-word density pass | Manually split residual architecture blocks and made scanner fail at `>29` words | `../Reports/ARCHITECTURE_30WORD_DENSITY_AUDIT_X_012.json` |
| 29-word density pass | Manually split residual architecture blocks and made scanner fail at `>28` words | `../Reports/ARCHITECTURE_29WORD_DENSITY_AUDIT_X_012.json` |

## Active Gaps

| Gap | Required proof artifact |
|---|---|
| EXTERNAL_CODEX loop165 source gate | Remaining 34 runtime files moved unsigned `Time.frameCount` casts to `SystemDispatcher.CurrentFrameId`; touched grep 0; build skipped by `BUILD_GUARD cpu=100 compiler_count=2` |
| EXTERNAL_CODEX loop164 source gate | 34 runtime files moved unsigned `Time.frameCount` casts to `SystemDispatcher.CurrentFrameId`; touched cast grep 0; build skipped by `BUILD_GUARD cpu=100 compiler_count=0` |
| EXTERNAL_CODEX loop163 source gate | 38 files moved frame-id payload casts to `SystemDispatcher.CurrentFrameId`; targeted cast grep 0; scoped `diff --check` passed; build skipped by guard |
| EXTERNAL_CODEX loop162 source gate | 29 files moved selected frame stamps to `SystemDispatcher`; `HectonBiolumZone` reads cached/hot-swapped; selected frame grep 0; build skipped by `BUILD_GUARD cpu=100 compiler_count=0` |
| EXTERNAL_CODEX loop161 source gate | 19 Dispatcher/TickManager stale-registration tails fixed; touched-file `diff --check` passed; broad stale scans 0; build skipped by `BUILD_GUARD cpu=73 compiler_count=9` |
| EXTERNAL_CODEX loop160 source gate | 50 additional Dispatcher stale-registration tails fixed; targeted touched-file `diff --check` passed with LF warnings only; build skipped by `BUILD_GUARD cpu=63 compiler_count=1` |
| EXTERNAL_CODEX loop159 source gate | Singleton owner-route grep returned 0; `?? GlobalRegistry|GlobalRegistry.TryGet` grep returned 0; scoped `diff --check` passed; build skipped by `BUILD_GUARD cpu=100 compiler_count=2` |
| EXTERNAL_CODEX loop158 source gate | Latest wall: `Build_EXTERNAL_CODEX_hotpath_cleanup158_world_dispatcher_rebind.log`; `NETSDK1004` before C#; no warnings/`CS*`; retry blocked by `BUILD_GUARD cpu=79 compiler_count=2`; targeted greps pass |
| EXTERNAL_CODEX loop157 source gate | UI/Construction singleton-tail greps pass; scoped `diff --check` passed; build skipped by `BUILD_GUARD cpu=100 compiler_count=2` |
| EXTERNAL_CODEX loop143 compile verification | Source-only; guarded build skipped by `BUILD_GUARD cpu=100 compiler_count=0` after targeted hot-swap/getter greps |
| EXTERNAL_CODEX loop142 compile verification | Source-only after pre-build `BUILD_GUARD cpu=78.3 compiler_count=2` |
| EXTERNAL_CODEX loop141 compile verification | Fresh guarded build after CPU <= 50% and no active compiler; current loop141 is source-only after latest `BUILD_GUARD cpu=93.2 compiler_count=1` |
| EXTERNAL_CODEX loop140 build wall | `Build_EXTERNAL_CODEX_hotpath_cleanup139_context_purity.log`: `NETSDK1004` project.assets missing and `MSB3491` Temp/obj denied before C# diagnostics |
| EXTERNAL_CODEX loop139 compile verification | Fresh guarded build after CPU <= 50% and no active compiler; current loop139 is source-only |
| EXTERNAL_CODEX loop138 compile verification | Fresh guarded build after CPU <= 50% and no active compiler; current loop138 is source-only |
| EXTERNAL_CODEX loop137 compile verification | Fresh guarded build after CPU <= 50% and no active compiler; current loop137 is source-only |
| EXTERNAL_CODEX loop136 compile verification | Fresh guarded build after CPU <= 50% and no active compiler; current loop136 is source-only |
| EXTERNAL_CODEX loop135 compile verification | Fresh guarded build after CPU <= 50% and no active `dotnet`/`csc`/`VBCSCompiler`/`MSBuild`; current loop135 is source-only |
| EXTERNAL_CODEX loop132 compile verification | `Build_EXTERNAL_CODEX_hotpath_cleanup131_target_dedupe.log`: reaches editor DLL output with 1 `MSB3101` Temp/obj cache warning, 0 errors, no `CS*` diagnostics, no final summary/exit line |
| Data Monolith runtime readiness | Bake/import/boot/checksum/player-build proof for `static_data.h8bin` |
| Save readiness | Current write/read/migration/checksum-failure artifact |
| Global authority runtime behavior | Lane overflow, route-card, and profiler proof |
| Continuous scalability | Frame-time, shader, and dynamic-resolution capture across quality weight range |
| AUP compliance | Static scan plus rebase replay |
| Netcode | Transport loopback, fuzz, jitter, hash replay, profiler, GC proof |
| UI zero-GC | GCMonitor or Memory Profiler capture |
| Terrain geography | Generator/streaming proof against flooded terrestrial template |

## Validation

| Validator | Required state |
|---|---|
| `Tools/OOP_Doc_Scanner.py` | `finalPass=true`; source sync; reduction `>=30%`; markers `0`; narrative paragraphs `0`; strict paragraph/sentence/line/file/non-contract offenders `0`; instructional markers `0` |
| `Tools/VerifyDocStructure.py` | `pass=true`; root text docs `3`; broken links `0`; duplicate headers `0`; fence issues `0`; stale parameter files `0`; active non-BOM files `0` |

This file is not compile, Unity import, Play Mode, profiler, GC, player-build, or visual proof.
