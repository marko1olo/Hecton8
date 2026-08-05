# HECTON-8 Documentation Actuality Ledger

Date: 2026-06-09
Status: PENDING VERIFICATION
Owner: DOC_ROOT_ARCH_AUDIT
Evidence class: STATIC_DOC / STATIC_SOURCE / STATIC_FILESYSTEM / CLI_COMPILE where artifact cited

This ledger is the concise documentation-change register and proof-snapshot holder. Full historical text is archived at `../_Archive/Architecture_X_012_APEX_2026-05-23/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.full.md`.

## Source Constants

| Contract | Current value | Source / proof |
|---|---:|---|
| Save writer version | `0x000B` | `Assets/_Project/Scripts/SaveBinaryStorage.cs` |
| Save header size | `56` bytes | `SaveBinaryStorage.CurrentHeaderSize` |
| Legacy save header size | `44` bytes | `SaveBinaryStorage.LegacyHeaderSize` |
| H8DM header size | `64` bytes | `Assets/_Project/Scripts/Data/Monolith/H8DataMonolithTypes.cs` |
| H8DM directory size | `64` bytes | `H8DataLayoutConstants.DirectorySizeBytes` |
| H8DM schema hash | `0x33313332` | `H8DataLayoutConstants.SchemaHash` |
| Data Monolith payload | `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` | present; `7,457,664` bytes, mtime 2026-06-07, measured 2026-08-05 (supersedes 2026-06-01 check: `1,804,864` bytes) |
| Signal lane capacity | `512` | `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs` |
| Scalability DTO | `16` bytes | `ScalabilityStateDTO` static source |
| AUP/blit struct | `48` bytes | AUP static source |

Prompt/report values that disagree with source are stale. Current source wins.

## 2026-06-09 Documentation Validator Scope Refresh

No runtime code, Unity assets, imports, builds, or Play Mode proof were touched by this refresh. This section records documentation tooling reality only.

| Area | Current static fact | Source / proof |
|---|---|---|
| `taskslocal` boundary | `taskslocal/` is explicit standalone dispatch provenance, not authority or bulk context; batch archival requires exact path/name/batch reference proof | `../../taskslocal/README.md`; `../DOC_GOVERNANCE.md`; `../ROOT_DOCS_REFERENCE.md` |
| Structure validator scope | `Tools/VerifyDocStructure.py` now validates the authority spine by default: root anchors and route bibles from `PROJECT_BIBLES.md`, root `Docs/*.md` control files, `Docs/ARCHITECTURE/**`, and explicit README boundary files | `../../Tools/VerifyDocStructure.py` |
| Content corpus boundary | Lore, GeneratedAssets, Marketing, Modding, Reports, Tasks, AgentLogs, and other content/support corpora are not default authority-spine doc-structure failures; use `--include-corpora` only for explicit corpus sweeps | `../../Tools/VerifyDocStructure.py`; `../README.md` |
| Encoding policy | UTF-8 and UTF-8 with BOM are accepted; missing BOM is informational, not an acceptance failure | `../../Tools/VerifyDocStructure.py` |
| Structure validation | `pass=true`; `docScope=authority_spine`; activeDocCount `282`; rootTextDocCount `76`; missing root docs `0`; unexpected root docs `0`; broken links `0`; duplicate headers `0`; fence issues `0`; stale parameter files `0`; encodingWithoutUtf8Sig `81` informational | `../Reports/DOC_STRUCTURE_VALIDATION_X_012.json` |

## 2026-06-05 Documentation Completeness Static Refresh

No runtime code or Unity assets were edited by this documentation-completeness refresh. This section records static documentation/source facts only.

| Area | Current static fact | Source / proof |
|---|---|---|
| Documentation-completeness report front | Active dated evidence front exists and is indexed as an evidence snapshot, not stable authority | `Docs/Reports/DocumentationCompleteness_20260605/README.md`; `Docs/Reports/README.md` |
| Source script surface | `Assets/_Project/Scripts/**/*.cs` static count is `2545` | `Docs/Reports/DocumentationCompleteness_20260605/SOURCE_COVERAGE_REALITY_AUDIT_3223_20260605.md` |
| Exact source-routing baseline | `SOURCE_SYSTEMS_REALITY_MAP.md` plus `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` exact relative-path anchors cover `288` scripts; `2257` scripts lack exact relative-path anchors in those two docs | `Docs/Reports/DocumentationCompleteness_20260605/SOURCE_COVERAGE_REALITY_AUDIT_3223_20260605.md` |
| Source-routing family audits | Presentation, authoring/data/tools, world/gameplay, and core/systems family audits confirm sparse exact-owner routing; counts overlap and must not be summed globally | `Docs/Reports/DocumentationCompleteness_20260605/SOURCE_ROUTING_FAMILY_AUDITS_SYNTHESIS_20260605.md` |
| Root route bibles | `PROJECT_BIBLES.md` route scan found `63` route refs, all existing, with missing first-20/evidence/status/GQW/proof/rejection terms at `0` after static patch waves | `Docs/Reports/DocumentationCompleteness_20260605/ROOT_BIBLE_FIRST20_POSTPATCH_AUDIT_20260605.md` |
| Architecture route metadata | Filename route-card set `108` files and broader route/authority set `186` files have `Status`, evidence-class, owner-domain, and review/disposition rows present after static patch waves | `Docs/Reports/DocumentationCompleteness_20260605/ARCHITECTURE_ROUTE_CARD_METADATA_AUDIT_20260605.md` |
| Control-doc metadata | Control markdown scan found `75` scoped control docs; postpatch missing `Evidence class:` is `0`, missing static/pending boundary marker is `0`, and the only missing `Status:` hit is read-only `AGENTS.md` | `Docs/Reports/DocumentationCompleteness_20260605/CONTROL_DOC_METADATA_BOUNDARY_AUDIT_20260605.md` |
| Combined root-bible snapshot | `Docs/PROJECT_ROOT_BIBLES_COMBINED.md` was regenerated by deterministic static generator; generator check passed; live source hook count and combined hook count are both `57` | `Tools/Docs/BuildProjectRootBiblesCombined.py`; `Docs/Reports/DocumentationCompleteness_20260605/COMBINED_ROOT_BIBLES_STALE_SNAPSHOT_AUDIT_20260605.md` |
| Proof-language scan | Focused active-doc scan found no positive runtime/platform/release readiness claim requiring immediate patch; remaining hits are negative prohibitions, proof labels, stale mirror text, orchestration memory, or archive debt | `Docs/Reports/DocumentationCompleteness_20260605/STABLE_DOC_PROOF_LANGUAGE_AUDIT_20260605.md` |

Interpretation: documentation routing and index quality improved by static evidence only. Runtime, Unity import, Play Mode, profiler, GCMonitor, player build, save/load, shader, platform, visual, Data Monolith boot, and first-20 player-route proof remain absent unless a row cites a current artifact of that class.

## 2026-06-01 Documentation Source Reality Refresh

No runtime code was edited by `1619`. This section records current static facts from filesystem/source scans.

| Area | Current static fact | Source / proof |
|---|---|---|
| Root text anchors | `AGENTS.md`, `TASTE.md`, `textes.md`, `MASTER_RELEASE_WORK_PLAN.md`, `BUILD_PLAYTEST_ISSUES.md` | `../DOC_GOVERNANCE.md`, `../ROOT_DOCS_REFERENCE.md`; `AGENTS.md` public-copy route requires root `textes.md` |
| First-party asmdefs | `171` under `Assets/_Project` | `rg --files Assets/_Project -g '*.asmdef'` |
| Data Monolith payload | present, `1,804,864` bytes in this 2026-06-01 check; superseded by 2026-08-05 measurement `7,457,664` bytes, mtime 2026-06-07 | `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` |
| H8DM header parse | magic `0x4D443848`; format `2`; header `64`; section count `28`; checksum64 `0xA85210353432862A`; schema hash `0x33313332` | Python static byte parse; `H8DataMonolithTypes.cs` |
| Save constants | writer `0x000B`; current header `56`; legacy header `44`; aligned section header `0x000B` | `Assets/_Project/Scripts/SaveBinaryStorage.cs` |
| SignalBus constants | lane capacity `512`; default expected capacity `64`; default max frame signals `64` | `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs` |
| Link scan | active corpus broken relative markdown links `0`; duplicate header files `0` | 1619 Python markdown scan |
| Encoding debt | active corpus files without UTF-8 BOM: `1786`; edited files are normalized separately | 1619 Python markdown scan |
| Build boundary | `dotnet build`, `msbuild`, and Unity batch mode launched `0` times | shell command review; user directive |

Runtime, Unity import, Play Mode, profiler, GCMonitor, player build, save/load, shader, platform, and visual proof remain absent.

## 2026-06-01 DOCS_AUDIT Content Corpus Classification

No runtime code was edited by `DOCS_AUDIT`. This section records documentation and source-surface classification only.

| Area | Current static fact | Source / proof |
|---|---|---|
| Active non-archive Docs folders | `ARCHITECTURE`, `Lore`, `Marketing`, `Modding`, `Design`, `Data`, `Audio`, `Atmosphere`, `AI_Texturing_Templates`, `Generated` | `Get-ChildItem Docs -Directory`, excluding `Archive`, `_Archive`, `DEPRECATED`, `Reports`, `AgentLogs`, `Tasks` |
| Lore corpus | root lore files plus `AppliedContent`, `ContentPacks`, `Encyclopedia`, and local lore archive folders | `Docs/Lore` static filesystem scan |
| Marketing corpus | controlled public/commercial planning corpus with README, Steam, creator, press, community, launch, data, and operations subfolders | `Docs/Marketing/README.md`; static filesystem scan |
| Data/profile corpora | `Docs/Data`, `Docs/Audio`, and `Docs/Atmosphere` contain CSV authoring/profile data | static filesystem scan |
| Design support corpus | binary specs, LUT/shader mapping, UI scaler, mission failure modes, VR comfort/haptic support docs | `Docs/Design` static filesystem scan |
| Source script surface | `56` direct directories under `Assets/_Project/Scripts` | static filesystem scan |

Interpretation: active docs are split between architecture contracts, content corpora, public/commercial planning, mod/API planning, generated artifacts, and authoring/profile data. Source and fresh proof artifacts still decide runtime reality.

## 2026-06-02 DOCS_AUDIT Code Reality Correction

No runtime code was edited by `DOCS_AUDIT`. User rejected folder/glossary-style documentation. This correction promotes source-backed implementation facts into `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md` and marks proof gaps explicitly.

| Area | Source-backed reality recorded | Source / proof |
|---|---|---|
| Bootstrap | `GameBootstrapper` owns real phased boot: hardware, memory prewarm, core services, environment, player, UI, scene activation; scene constants cover bootstrap, main menu, orbit, world; Data Monolith bootstrap retries and Addressables prewarm routes exist. | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` static source scan |
| Registry | `GlobalRegistry` has a wide concrete service-registration surface across core, player, environment, ocean, power, interaction, ecosystem/fauna, thermodynamics/fluid, logistics, worldgen, quest, PDA, localization, telemetry, asset lifecycle, VRAM, and floating origin. | `Assets/_Project/Scripts/Core/GlobalRegistry.cs` static source scan |
| Dispatcher | `SystemDispatcher` implements master phases plus legacy/priority tick lanes; fast/slow/cold/frost cadences exist; 300-frame DataVault-backed black box exists. | `Assets/_Project/Scripts/Core/SystemDispatcher.cs`, `SystemDispatcherContracts.cs` static source scan |
| SignalBus | `SignalBus<T>` is bounded frame-snapshot transport with capacity policy, finite guards, deterministic ordering policy, overflow faulting, and payload coalescing/load-shed routes. | `Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs` static source scan |
| Native memory | `IDataVault` / `GlobalDataVault` expose generation handles, read/write handles, writer locks, buffer locks, release routes, frost defrag, and a bootstrap/diagnostic `TryGetLatestCreated()` escape. | `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs`, `H8Memory.cs` static source scan |
| Save/static data | Save writer `0x000B`, header `56`, legacy header `44`, LZ4, XXH3, tmp/bak, migration, and indexed storage paths exist. Data Monolith arena/layout code exists with schema `0x33313332`, header `64`, directory `64`. | `SaveManager.cs`, `SaveBinaryStorage.cs`, `Data/Monolith/*.cs` static source scan |
| Domain breadth | Core, world, gameplay, physics, construction, AI/fauna/ecosystem, atmosphere, UI/PDA/visor/audio, optimization, modding, networking, and QA all have active source surfaces with tick/service participants in several domains. | static scan of `Assets/_Project/Scripts` direct domains |
| Loose root scripts | Root-level `Assets/_Project/Scripts/*.cs` carries real mixed-domain systems: save, crafting, fabricator, survival, tether, voxel/fluid/world, localization, tools, HUD/PDA bridges, smoke/profiler helpers. Folder anchors are routing hints, not full ownership proof. | `cmd.exe /d /c for %f in (Assets/_Project/Scripts/*.cs) do @echo %~nxf` static listing |
| Boot phases | Current `GameBootstrapper.BootstrapPhase` is seven source phases: `HardwareCheck`, `MemoryPreWarm`, `CoreServices`, `Environment`, `Player`, `UI`, `SceneActivate`. Older six-stage boot language is a contract grouping, not the current enum. | `GameBootstrapper.cs` static source scan |
| Dispatch hybrid | `SystemDispatcher` has master phases and legacy/priority tick lanes at the same time. Fast/slow/cold/frost cadence constants and 300-frame blackbox rings exist. | `SystemDispatcher.cs`, `GlobalRegistry.cs` static source scan |
| Data Monolith file identity | Current `static_data.h8bin` static file size `7,457,664` bytes, mtime 2026-06-07, measured 2026-08-05; SHA-256 `77785d6c25602141c71033cf96a42787e1415f06abe9cde3831675edb0045c53` (supersedes 2026-06-02 check: `1,804,864` bytes, SHA `4f40185a...`). | `sha256sum`, file size/mtime check |
| Real script systems map | `SOURCE_SYSTEMS_REALITY_MAP.md` now records concrete source owners across core, save/data, world/voxel/scatter, player/interaction/VR, inventory/crafting/survival, habitat/flooding/power, AI/fauna/ecosystem, atmosphere/thermal, UI/visor/audio/VFX, networking/modding/tooling. | static `rg`/filesystem scan of `Assets/_Project/Scripts` |

Correction boundary: this is still `STATIC_SOURCE`. It does not prove compile, Unity import, Play Mode, route completion, profiler/GC, shader correctness, Data Monolith boot, save/load roundtrip, networking, or device/platform readiness.

## 2026-05-28 Root And Architecture Source Reality Audit

No runtime code was edited by `DOC_ROOT_ARCH_AUDIT`. This section records static source/filesystem facts used to update root and architecture onboarding docs.

| Area | Current static fact | Source / proof |
|---|---|---|
| Unity version | `6000.4.1f1` | `ProjectSettings/ProjectVersion.txt` |
| Render pipeline | URP package `17.4.0` | `Packages/manifest.json` |
| Core packages | Addressables `2.7.6`, Input System `1.19.0`, Memory Profiler `1.1.12` | `Packages/manifest.json` |
| XR package presence | OpenXR `1.17.0`, Meta OpenXR `2.5.0`, XR Management `4.6.0` | `Packages/manifest.json`; package presence is not platform readiness |
| Enabled scene list | `00_BOOTSTRAP`, `01_MAIN_MENU`, `01_ORBIT`, and `02_HECTON_WORLD` are enabled in BuildSettings; enabled list is not production handoff proof | `ProjectSettings/EditorBuildSettings.asset` |
| First 20 route docs | Current first-20 proof uses `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`; `01_ORBIT` remains standalone/YELLOW prologue route, not mandatory first-20 acceptance | `FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`, `FIRST_20_MINUTES_ROUTE_BRIEF.md`, `PROLOGUE_ORBIT_HANDOFF_ROUTE_CARD_13PRO.md` |
| Scene authority status | Root `AGENTS.md` states normative production handoff is `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`; `01_ORBIT` can only become main handoff after explicit route-bible/GREEN proof and root authority update | `AGENTS.md`, `ProjectSettings/EditorBuildSettings.asset`, `PROLOGUE_ORBIT_HANDOFF_ROUTE_CARD_13PRO.md` |
| First-party asmdefs | superseded by 2026-06-01 count: `171` under `Assets/_Project` | 2026-06-01 static filesystem count |
| Data Monolith payload | superseded by 2026-08-05 measurement: `7,457,664` bytes, mtime 2026-06-07 (historical 2026-06-01 check: `1,804,864` bytes) | `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` |
| Data Monolith scoped validator | `PASS`; `files=2`; `structs=32`; `mb=1.0495`; `seconds=0.491846`; Python schema/payload proof only | `../Reports/DOC_ROOT_ARCH_AUDIT_h8bin_validator_narrow_20260528.json` |
| Source topology doc | Added current project topology, source owner spine, route map, and verification gaps | `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md` |
| Source topology source-spine recheck | Corrected player/environment runtime-context source anchors to `Assets/_Project/Scripts/Core/...` | PowerShell path scan |
| Domain coverage doc | Added echelon map to active docs, source anchors, and proof gaps | `DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md` |
| Domain coverage reference check | Every listed architecture doc and `Assets/_Project/Scripts` anchor exists | PowerShell path scan |
| Black-box doctrine sync | 300-frame rings; `Dump_*.bin` primary; `.h8dump` legacy | `../PROJECT_ATLAS.md` |
| Index updates | Root and architecture read orders now surface topology, boot, dispatch, first route, and platform proof ladder | `../README.md`, `README.md`, `../PROJECT_BASELINE.md`, `../HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `../HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md` |
| Atlas stub cleanup | `observedAssemblyCount = 83` is compatibility-only; current count lives in topology/generated graph | `../PROJECT_ATLAS.md` |
| Dependency graph regeneration | `BuildArchitectureAtlas.py` regenerated graph markdown/json/cache on 2026-05-28; graph reports `220` asmdefs scanned, `167` first-party asmdefs under `Assets/_Project`; graph count is stale against the 2026-06-01 filesystem count of `171` until regenerated | `../Generated/DEPENDENCY_GRAPH.md`, `../Generated/DEPENDENCY_GRAPH.json`, `../Generated/DEPENDENCY_GRAPH.cache.json` |
| AtlasCheck | `ATLAS_CHECK_PASS references=5807` | `python Tools/AtlasCheck.py` |
| H-Phi atlas | `Docs/Generated/PROJECT_ATLAS_HPHI.md` is absent; `HectonPhiStaticAudit.py --no-fail` timed out after 300 seconds before producing it | no proof artifact; see `Docs/Generated/README.md` |
| Structure validation | Superseded by 2026-06-09 authority-spine validator scope; old `activeDocCount=704` was a pre-corpus snapshot | `../Reports/DOC_STRUCTURE_VALIDATION_X_012.json` |
| OOP doc scanner | `finalPass=true`; activeFileCount `704`; sourceSyncPass `true`; active stale parameter files `0`; wordReductionPercent above `31%` | `../Reports/DOCUMENTATION_OPTIMIZATION_REPORT_X_012.json` |

Interpretation: project file topology is now easier to read.

Compile, import, Play Mode, profiler, GC, save/load, player-build, shader, platform, and visual readiness remain unproven until fresh artifacts exist.

## Current Proof Snapshots

### Native Memory Ownership

Latest reviewed native alias evidence: `../Reports/VAULT_NATIVE_ALIAS_LEDGER_1315_PASS23.json`.

| Metric | Value |
|---|---:|
| Scanned files | `2439` |
| Parse failures | `0` |
| Native field declarations | `6648` |
| Forbidden persistent candidates | `834` |
| Forbidden MonoBehaviour candidates | `72` |
| Job-transient fields | `5481` |
| Stack-only/ref-struct view fields | `288` |
| Core-memory-allowed fields | `45` |
| Raw pointer fields | `861` |

Earlier cleanup evidence recorded `1770` forbidden persistent candidates and `358` MonoBehaviour candidates. The reduction is real. The project is still not clean.

Largest residual native groups are recorded in `../Reports/VAULT_NATIVE_ALIAS_LEDGER_1315_PASS23.json`; the project is still not clean.

### Compile Slice

Latest reviewed green full-solution CLI pass is stale after later source edits.

Current guarded builds hit generated-project or CPU-guard boundaries.

Runtime proof remains absent.

Required proof: Unity import, Console, Play Mode, profiler, GC, player build, shader import, save/load, platform, visual checks.

### Static Audit Wording Boundary

Rows labeled `Latest verified audit` below refer to the cited static/CLI report artifact only. They are not current Unity import, Play Mode, profiler, GC, player build, route, platform, or visual proof unless the row explicitly cites that artifact class.

## 2026-05-26 Documentation Distillation

No C# source was edited by `DOCS_ACTUALIZATION`.

| Area | Active action | Proof |
|---|---|---|
| Project baseline | Rebuilt root baseline as stable authority/doctrine, not a current status page | `../PROJECT_BASELINE.md` |
| Root index | Removed current-build and scanner-counter prose from root read map | `../README.md` |
| Root policy | Removed current compile/report boundary from root reference | `../ROOT_DOCS_REFERENCE.md` |
| Architecture index | Kept only contract read order and routed current facts here | `README.md` |
| Reports index | Reframed reports as evidence storage, not knowledge base | `../Reports/README.md` |
| Deprecated transient indexes | Removed folder-index approach from active docs and recorded rejection | `../DEPRECATED/DOCS_ACTUALIZATION_TRANSIENT_INDEXES_2026-05-26.md` |
| Deprecated root docs noise | Moved token telemetry, FAQ, glossary, and marketing binary archive out of active `Docs/` root | `../DEPRECATED/Root_Docs_Noise_2026-05-26/MANIFEST.md` |
| Deprecated active-doc cleanup | Legacy bundles, superseded route card, and raw marketing sheets left active docs | `../DEPRECATED/Active_Doc_Deprecation_2026-05-26/MANIFEST.md` |

## Historical Distillate

| Area | Distilled fact | Proof |
|---|---|---|
| 1303 report bloat | `47` superseded SignalBus/tether report revisions were purged on 2026-06-06 after V16 became the only current evidence snapshot | `../Reports/SIGNALBUS_HOTPATH_AUDIT_1303_APEX_V16.md` |
| SignalBus evidence | V16 SignalBus hot-path audit remains the current evidence snapshot | `../Reports/SIGNALBUS_HOTPATH_AUDIT_1303_APEX_V16.md` |
| X_012 root docs | Root text anchors were reduced to stable authority files | `../Reports/DOCUMENTATION_OPTIMIZATION_REPORT_X_012.json` |
| X_012 historical reports | obsolete bulk report payload was purged on 2026-06-06; only explicitly referenced historical schemas remain | `../_Archive/Reports_X_012_2026-05-23/` |
| APEX architecture pass | Verbose architecture ledgers were compressed and full snapshots archived | `../_Archive/Architecture_X_012_APEX_2026-05-23/` |
| Concision chain | Historical concision scans remain report evidence only | `../Reports/README.md` |

## Active Verification Gaps

| Area | Required proof artifact |
|---|---|
| Current full-solution compile | fresh guarded build log matching current source |
| Data Monolith runtime readiness | bake/import/boot/checksum/player-build proof for `static_data.h8bin` |
| Save readiness | current write/read/migration/checksum-failure artifact |
| Global authority runtime behavior | lane overflow, route-card, and profiler proof |
| Native memory ownership | fresh alias ledger plus owner-route fixes for remaining persistent and MonoBehaviour hotspots |
| Continuous scalability | frame-time, shader, and dynamic-resolution capture across quality weight range |
| AUP compliance | static scan plus rebase replay |
| Netcode | transport loopback, fuzz, jitter, hash replay, profiler, GC proof |
| UI zero-GC | GCMonitor or Memory Profiler capture |
| Terrain geography | generator/streaming proof against flooded terrestrial template |

## 2026-05-27 Runtime Allocation Route Pass

| Area | Fact | Proof |
|---|---|---|
| Clean runtime files patched | Eight allocation-route call sites were removed; `WorldSliceAnchor` and `H8DataBaker` remain dirty-file residuals | `../Reports/UNITY_RUNTIME_ALLOC_ROUTE_PASS_UNKNOWN_20260527.md` |
| Release shader lookup | First-party `Shader.Find(...)` sites remain editor/development guarded in the local static scan | `../Reports/UNITY_RUNTIME_ALLOC_ROUTE_PASS_UNKNOWN_20260527.md` |
| Remaining residuals | `2` first-party non-Editor-folder `.ToArray()` text hits remain in dirty files | `../Reports/UNITY_RUNTIME_ALLOC_ROUTE_PASS_UNKNOWN_20260527.md` |
| Build boundary | Second guarded build launched legally; current failure is missing generated `.csproj`, not C# | `../Reports/BUILD_UNKNOWN_RUNTIME_ALLOC_ROUTE_PASS2_20260527.log` |

## 2026-05-27 Read Accessor Purity Pass

| Area | Fact | Proof |
|---|---|---|
| Read-shaped allocation route | `PerformanceBudgetController.GetBudgetStatus()` now reuses an owner snapshot; `RTLProcessor` lazy buffer route is named `EnsureBuffer()` | `../Reports/UNITY_READ_ACCESSOR_PURITY_PASS_UNKNOWN_20260527.md` |
| Read/create split | `WorldShippingContentFilter` now uses pure `TryGetSuppressedHierarchyIds()` and explicit `EnsureSuppressedHierarchyIds()` | `../Reports/UNITY_READ_ACCESSOR_PURITY_PASS_UNKNOWN_20260527.md` |
| Build boundary | Guarded build launched legally after final source edit; current failure is missing generated `.csproj`, not C# | `../Reports/BUILD_UNKNOWN_READ_ACCESSOR_PURITY_RECHECK2_20260527.log` |

## 2026-05-27 Signal Contract Pass

| Area | Fact | Proof |
|---|---|---|
| Runtime signal names | Confirmed duplicate `ToolDepletedSignal` contract errors are fixed; gameplay local event is now `PlayerToolDepletedSignal` | `../Reports/UNITY_SIGNAL_CONTRACT_PASS_UNKNOWN_20260527.md` |
| Plugin asmdef route | `Hecton8.Plugins` now directly references `Hecton8.Core.Contracts` for signal-contract usage | `../Reports/UNITY_SIGNAL_CONTRACT_PASS_UNKNOWN_20260527.md` |
| Contract scan | Final SignalBus contract audit reports `errors=0`, `confirmedErrors=0`, `asmdefContractBoundaryHits=0` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_FINALSCAN.json` |
| Build boundary | Guarded build launched legally; current failure remains missing generated `.csproj`, not C# | `../Reports/BUILD_UNKNOWN_SIGNAL_CONTRACT_PASS_RECHECK_20260527.log` |

## 2026-05-27 Signal Queue Diagnostics Pass

| Area | Fact | Proof |
|---|---|---|
| Queue ownership scanner | NativeQueue helper ownership is now detected; `POSSIBLE_ORPHANED_SIGNAL_QUEUE=0` in the recheck | `../Reports/UNITY_SIGNAL_QUEUE_DIAGNOSTICS_PASS_UNKNOWN_20260527.md` |
| Toolchain target | `SignalBusContractAuditCli` now targets `net10.0`; tool restore/build succeeded with `0` warnings and `0` errors | `../Reports/UNITY_SIGNAL_QUEUE_DIAGNOSTICS_PASS_UNKNOWN_20260527.md` |
| Diagnostics flush allocation | `RuntimeDiagnosticsTrace.FlushSuppressedDuplicates()` no longer allocates a list on flush | `../Reports/UNITY_SIGNAL_QUEUE_DIAGNOSTICS_PASS_UNKNOWN_20260527.md` |
| Remaining native rings | `8` registered non-Vault telemetry rings remain as owner-by-owner architecture decisions | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_QUEUE_RECHECK.json` |
| Build boundary | Full solution build was guard-blocked by CPU above `50%` for `30` attempts | `../Reports/BUILD_UNKNOWN_SIGNAL_QUEUE_DIAGNOSTICS_RECHECK_20260527.log` |

## 2026-05-27 Signal Audit Classifier And Seam MPB Pass

| Area | Fact | Proof |
|---|---|---|
| Signal audit classifier | Constructor/editor-only/MPB/ComputeShader/owner-local telemetry/cold allocation/multiline method/layout cases are separated from hard warnings in the CLI source | `../Reports/UNITY_SIGNAL_AUDIT_CLASSIFIER_AND_SEAM_MPB_PASS_UNKNOWN_20260527.md` |
| Seam dither draw parameters | `SeamGapDitherRenderer` now sends per-draw buffers/camera/distance through a cached `MaterialPropertyBlock` instead of mutating the material | `../Reports/UNITY_SIGNAL_AUDIT_CLASSIFIER_AND_SEAM_MPB_PASS_UNKNOWN_20260527.md` |
| Contract-name cleanup | Clean local payload shadows were renamed; duplicate-name review warnings dropped from `48` to `14` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_EXEC_CARRIER_RECHECK.json` |
| Latest verified audit | Final full scan reports `errors=0`, `confirmedErrors=0`, `warnings=171`, `infos=826`, `SIGNAL_LAYOUT_REVIEW=2`, `JOB_STRUCT_LAYOUT_REVIEW=69 info`, `EXECUTABLE_STRUCT_LAYOUT_REVIEW=2 info` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_EXEC_CARRIER_RECHECK.json` |
| Build boundary | CLI build exits `0`; guarded full solution build exits `1` on Unity-generated editor project circular dependency and missing `Temp/CodexBuild/Unity.ShaderGraph.Editor.dll` | `../Reports/BUILD_UNKNOWN_EXEC_CARRIER_RECHECK_20260527.log` |

## 2026-05-27 Signal Telemetry Ownership Recheck

| Area | Fact | Proof |
|---|---|---|
| TMP font swap route | `FontStreamingManager.ProcessSwapBatch()` no longer resolves `_targetFont.material`; the material is cached at queue start | `../Reports/UNITY_SIGNAL_TELEMETRY_OWNERSHIP_RECHECK_UNKNOWN_20260527.md` |
| NativeArray ownership audit | `SignalBusContractAuditCli` recognizes `VaultGenerationHandle`, `ref _fieldHandle`, and helper-owned `AllocateArray<T>` telemetry routes | `../Reports/UNITY_SIGNAL_TELEMETRY_OWNERSHIP_RECHECK_UNKNOWN_20260527.md` |
| Latest verified audit | Final recheck reports `errors=0`, `confirmedErrors=0`, `warnings=166`, `infos=832`; declared-only telemetry rings dropped to `1` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_TELEMETRY_OWNERSHIP_RECHECK2.json` |
| Build boundary | Full solution build is not green: `350` error lines, `0` warning lines, no touched-source hits; failure buckets are generated/plugin project graph refs | `../Reports/BUILD_UNKNOWN_TELEMETRY_OWNERSHIP_FULL_SOLUTION_RECHECK_20260527.log` |
| Documentation gates | `VerifyDocStructure pass=true activeDocCount=688`; `OOP_Doc_Scanner finalPass=true activeFileCount=688 sourceSyncPass=true` | `../Reports/UNITY_SIGNAL_TELEMETRY_OWNERSHIP_RECHECK_UNKNOWN_20260527.md` |

## 2026-05-27 Signal Layout/Alias Classifier Pass

| Area | Fact | Proof |
|---|---|---|
| Cache-line classifier | `ProgressionEventSignal` and `VocalCueSignal` false stride debt is removed by global struct-layout indexing | `../Reports/UNITY_SIGNAL_LAYOUT_ALIAS_CLASSIFIER_UNKNOWN_20260527.md` |
| Native telemetry aliases | Expression-bodied Vault accessors, private-ref nested buffers, H8Memory release helpers, and DataVault allocator aliases are classified separately | `../Reports/UNITY_SIGNAL_LAYOUT_ALIAS_CLASSIFIER_UNKNOWN_20260527.md` |
| Signal-like DTO layouts | `FaunaDirector.AcousticPanicCommand` and `VocalWarningSystem.VocalWarningTelemetrySnapshot` now have explicit layout proof | `../Reports/UNITY_SIGNAL_LAYOUT_ALIAS_CLASSIFIER_UNKNOWN_20260527.md` |
| Latest verified audit | Final recheck reports `errors=0`, `confirmedErrors=0`, `warnings=155`, `infos=1171`, `SIGNAL_LAYOUT_REVIEW=0`, `CACHELINE_CRITICAL_SIGNAL_STRIDE_DEBT=1` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_LAYOUT_ALIAS_FINAL.json` |
| Closed gap | Final borrowed-view classifier build passed in `UnknownFinal` with `0` warnings and `0` errors | `../Reports/BUILD_UNKNOWN_SIGNAL_CLI_LAYOUT_ALIAS_FINAL2_20260527.log` |
| Full solution boundary | Escalated `Hecton8.slnx` build is not green: `3141` warnings, `365` errors, `0` touched-file hits | `../Reports/BUILD_UNKNOWN_SIGNAL_LAYOUT_ALIAS_FULL_SOLUTION_ESCALATED_20260527.log` |
| Documentation gates | `VerifyDocStructure pass=true activeDocCount=692`; `OOP_Doc_Scanner finalPass=true activeFileCount=692 sourceSyncPass=true` | `../Reports/UNITY_SIGNAL_LAYOUT_ALIAS_CLASSIFIER_UNKNOWN_20260527.md` |

## 2026-05-27 Signal Residual Contract Cleanup

| Area | Fact | Proof |
|---|---|---|
| Sector override commit route | `PersistentWorldRegistry` no longer snapshots due commit work with `List<T>.ToArray()`; the route uses a cold-owned bounded `SectorOverrideCommitWork[16]` buffer | `../Reports/UNITY_SIGNAL_RESIDUAL_CONTRACT_CLEANUP_UNKNOWN_20260527.md` |
| Tether tension lane | `TetherTensionSignal` is no longer declared cache-line-critical while carrying a 192-byte endpoint telemetry payload | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_TBDR_UI_SANDBOX_TETHER_SECTOR_RECHECK.json` |
| Duplicate signal-like names | Clean sandbox/UI/TBDR local carriers were renamed; duplicate-name review warnings are now `8` instead of `14` | `../Reports/UNITY_SIGNAL_RESIDUAL_CONTRACT_CLEANUP_UNKNOWN_20260527.md` |
| Build boundary | Full project compile errors were intentionally not fixed in this pass by user instruction | `../Reports/UNITY_SIGNAL_RESIDUAL_CONTRACT_CLEANUP_UNKNOWN_20260527.md` |
| Documentation gates | `VerifyDocStructure pass=true activeDocCount=693`; `OOP_Doc_Scanner finalPass=true activeFileCount=693 sourceSyncPass=true` | `../Reports/UNITY_SIGNAL_RESIDUAL_CONTRACT_CLEANUP_UNKNOWN_20260527.md` |

## 2026-05-27 Core Memory Signal Domain Deep Pass

| Area | Fact | Proof |
|---|---|---|
| Mod projection cull telemetry | Production ownership moved from local persistent ring to `GlobalDataVault` generation handle, with local fallback only when the Vault is unavailable | `../Reports/UNITY_CORE_MEMORY_SIGNAL_DOMAIN_DEEP_PASS_UNKNOWN_20260527.md` |
| TBDR Vault lifetime | Runtime and vertex-budget Vault buffers now release their `VaultGenerationHandle` routes on dispose | `../Reports/UNITY_CORE_MEMORY_SIGNAL_DOMAIN_DEEP_PASS_UNKNOWN_20260527.md` |
| SignalBus audit movement | Rebuilt-CLI recheck reports warnings `148 -> 145`, registered non-Vault telemetry rings `3 -> 0`, owner-local rings `7 -> 8`, and Vault aliases `3 -> 5` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_DEEP_DOMAIN_RECHECK_FINAL.json` |
| Tool build boundary | `SignalBusContractAuditCli` build succeeds with `0` warnings and `0` errors; full project compile errors are intentionally untouched | `../Reports/BUILD_UNKNOWN_SIGNAL_CLI_DEEP_DOMAIN_RECHECK_20260527.log` |

## 2026-05-27 Global Route Cache Pass

| Area | Fact | Proof |
|---|---|---|
| Dispatcher lane registration | `ConnectionSplineBatchRenderer` and `SceneRuntimeService` use cached dispatcher availability plus direct `SystemDispatcher` lanes instead of registry wrappers in live helpers | `../Reports/UNITY_GLOBAL_ROUTE_CACHE_PASS_UNKNOWN_20260527.md` |
| Object-pool command route | `ThreadSafeCommandQueue` and `ModWorldPersistenceManager` use cached object-pool dependencies for command drain, mod spawn, despawn, and restore routes | `../Reports/UNITY_GLOBAL_ROUTE_CACHE_PASS_UNKNOWN_20260527.md` |
| Physics late-frame route | `SystemDispatcher` late-frame physics pending-count and flush now read cached `IPhysicsService`, not `GlobalRegistry.Physics` | `../Reports/UNITY_GLOBAL_ROUTE_CACHE_PASS_UNKNOWN_20260527.md` |
| Transition presentation route | `SceneRuntimeService` terminal boot handles, world-drone audio bridge, tick dispatcher, and camera-juice handles are cached and hot-swap refreshed | `../Reports/UNITY_GLOBAL_ROUTE_CACHE_PASS_UNKNOWN_20260527.md` |
| Static audit | SignalBus recheck reports `errors=0`, `confirmedErrors=0`, `warnings=145`, `infos=1172` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_GLOBAL_ROUTE_CACHE_RECHECK.json` |

## 2026-05-27 Mod Registry Cache Pass

| Area | Fact | Proof |
|---|---|---|
| Mod settings route | `ModSettingsRegistry` caches `UserOptionsPersistence` and storage keys; apply routes no longer read `GlobalRegistry.UserOptions` or rebuild the key string | `../Reports/UNITY_MOD_REGISTRY_CACHE_PASS_UNKNOWN_20260527.md` |
| Mod slider persistence | Mod slider rows apply live callbacks in memory and persist once on commit/disable instead of saving options on every value event | `../Reports/UNITY_MOD_REGISTRY_CACHE_PASS_UNKNOWN_20260527.md` |
| Mod catalog route | `ModItemRegistry` and `ModBuildableRegistry` cache inventory/logistics services; active catalog resolution reads cached owner interfaces | `../Reports/UNITY_MOD_REGISTRY_CACHE_PASS_UNKNOWN_20260527.md` |
| Mod sandbox Vault route | `FutureCommandSandboxValidator.OpenVaultLane()` and rollback checks read cached `IDataVault`; the fallback registry read was removed | `../Reports/UNITY_MOD_REGISTRY_CACHE_PASS_UNKNOWN_20260527.md` |
| Static audit | SignalBus recheck reports `errors=0`, `confirmedErrors=0`, `warnings=145`, `infos=1172`; touched-file findings are info-only | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_MOD_REGISTRY_CACHE_RECHECK.json` |

## 2026-05-27 Vault Rebind Release Pass

| Area | Fact | Proof |
|---|---|---|
| Future command Vault lifetime | `FutureCommandSandboxValidator` releases all `20/20` sandbox `VaultLane<T>` handles through cached `IDataVault` on shutdown and DataVault rebind | `../Reports/UNITY_VAULT_REBIND_RELEASE_PASS_UNKNOWN_20260527.md` |
| Projected mod cull telemetry | `ModEventProjectionBridge` releases and reopens cull telemetry storage on DataVault hot-swap, with Vault-backed storage preferred and fallback only when Vault is unavailable | `../Reports/UNITY_VAULT_REBIND_RELEASE_PASS_UNKNOWN_20260527.md` |
| Static audit | SignalBus recheck reports `errors=0`, `confirmedErrors=0`, `warnings=145`, `infos=1172`; touched-file findings are info-only | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_VAULT_REBIND_RELEASE_RECHECK.json` |

## 2026-05-28 Core Vault Release Pass

| Area | Fact | Proof |
|---|---|---|
| StaticData/Babel telemetry ownership | Babel telemetry and BTree telemetry now use Babel-specific DataVault buffer IDs instead of sharing StaticData/BTree telemetry IDs | `../Reports/UNITY_CORE_VAULT_RELEASE_PASS_UNKNOWN_20260528.md` |
| Core Vault release routes | StaticData, Babel, SignalWarden tuning/telemetry/scratchpad, and MacroDatabase shutdown/rebind paths release Vault handles before clearing descriptors | `../Reports/UNITY_CORE_VAULT_RELEASE_PASS_UNKNOWN_20260528.md` |
| Static audit | SignalBus recheck reports `errors=0`, `confirmedErrors=0`, `warnings=73`, `infos=1020`; touched-file non-info findings are `0` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260528_CORE_VAULT_RELEASE_COLD_RECHECK.json` |

## 2026-05-28 Core Sync IO And Accessor Pass

| Area | Fact | Proof |
|---|---|---|
| Lockstep replay writer route | Replay writer setup moved to `OnEnable()` cold lifecycle; post-fixed replay write no longer opens/creates file state | `../Reports/UNITY_CORE_SYNC_IO_ACCESSOR_PASS_UNKNOWN_20260528.md` |
| Cold Core IO contracts | Input replay setup, binding override deletion, and parent-directory creation now expose cold/persistence names | `../Reports/UNITY_CORE_SYNC_IO_ACCESSOR_PASS_UNKNOWN_20260528.md` |
| Lockstep DataVault helper contract | Mutating `GetVaultBuffer<T>()` was renamed to `OpenOrAcquireVaultBufferView<T>()`; `TryGetVaultBuffer<T>()` remains existing-handle-only | `../Reports/UNITY_CORE_SYNC_IO_ACCESSOR_PASS_UNKNOWN_20260528.md` |
| Static audit | SignalBus recheck reports `errors=0`, `confirmedErrors=0`, `warnings=68`, `infos=1025`; Core subtree non-info findings are `0` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260528_CORE_SYNC_IO_ACCESSOR_RECHECK.json` |

## 2026-05-28 Global Signal Name Pass

| Area | Fact | Proof |
|---|---|---|
| Global telemetry DTO names | Duplicate signal-like DTO names were removed by renaming local/narrow structs; runtime layout and BufferID/SystemID ownership were not changed | `../Reports/UNITY_GLOBAL_SIGNAL_NAME_PASS_UNKNOWN_20260528.md` |
| Editor diagnostics rows | `SystemDiagnosticsBoard` crash table row no longer uses a runtime signal-like DTO name | `../Reports/UNITY_GLOBAL_SIGNAL_NAME_PASS_UNKNOWN_20260528.md` |
| Static audit | SignalBus recheck reports `errors=0`, `confirmedErrors=0`, `warnings=57`, `infos=1024`; duplicate/editor signal-like warning categories are `0` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260528_GLOBAL_SIGNAL_NAME_RECHECK.json` |

## 2026-05-28 Global Stability Sync IO Pass

| Area | Fact | Proof |
|---|---|---|
| Meta profile persistence | `GlobalProfileManager.SlowTick()` no longer writes `profile.json`; profile file IO is lifecycle-cold only | `../Reports/UNITY_GLOBAL_STABILITY_SYNC_IO_PASS_UNKNOWN_20260528.md` |
| Cold/background IO contracts | Babel background readers, input override persistence, QA endurance artifacts, and LUT boot streaming expose `BackgroundCold` or `Cold` helper names | `../Reports/UNITY_GLOBAL_STABILITY_SYNC_IO_PASS_UNKNOWN_20260528.md` |
| Static audit | SignalBus recheck reports `errors=0`, `confirmedErrors=0`, `warnings=40`, `infos=1041`; sync-IO warnings reduced by `17` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260528_GLOBAL_STABILITY_IO_FINAL.json` |

## 2026-05-28 Global Route Signal/Babel Pass

| Area | Fact | Proof |
|---|---|---|
| Babel Vault route | `LocRegistry.TryResolveBabelVault()` no longer polls `GlobalRegistry`; `LocalizationManager` binds Babel Vault cold and on hot-swap | `../Reports/UNITY_GLOBAL_ROUTE_SIGNAL_BABEL_PASS_UNKNOWN_20260528.md` |
| SignalBus lane snapshot route | `SignalBus<T>` frame snapshots use cached `SignalBusRegistry` Vault, not lazy first-publish registry reads | `../Reports/UNITY_GLOBAL_ROUTE_SIGNAL_BABEL_PASS_UNKNOWN_20260528.md` |
| Static audit | SignalBus recheck reports `errors=0`, `confirmedErrors=0`, `warnings=110`, `infos=1195`; touched-file warnings are `0` | `../Reports/SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260528_GLOBAL_ROUTE_SIGNAL_BABEL_RECHECK.json` |

## 2026-05-28 Hardware Thermal Blackbox ABI Pass

| Area | Fact | Proof |
|---|---|---|
| Thermal blackbox ABI | `HardwareThermalTelemetryEntry` dump stride now follows the 64-byte native record width instead of writing 24-byte entries | `../Reports/UNITY_HARDWARE_THERMAL_BLACKBOX_ABI_PASS_UNKNOWN_20260528.md` |
| Thermal write route | Severity and blackbox writes use DataVault write locks with `finally` release instead of mutable `TryResolve*` helpers | `../Reports/UNITY_HARDWARE_THERMAL_BLACKBOX_ABI_PASS_UNKNOWN_20260528.md` |
| Build boundary | Full build was not launched because a foreign `dotnet.exe` build was active and CPU was `100%`; current compile wall remains outside this pass | `../Reports/UNITY_HARDWARE_THERMAL_BLACKBOX_ABI_PASS_UNKNOWN_20260528.json` |

## 2026-05-28 Global Telemetry Blackbox Accessor Pass

| Area | Fact | Proof |
|---|---|---|
| Blackbox read route | `TryGetBlackboxRingBuffer()` was removed; the pure route is `TryResolveBlackboxRingBufferView()` and it does not initialize DataVault storage | `../Reports/UNITY_GLOBAL_TELEMETRY_BLACKBOX_ACCESSOR_PASS_UNKNOWN_20260528.md` |
| Blackbox open route | The only ring-view API that may initialize is now named `OpenOrInitializeBlackboxRingBufferView()` and is owner-thread gated | `../Reports/UNITY_GLOBAL_TELEMETRY_BLACKBOX_ACCESSOR_PASS_UNKNOWN_20260528.md` |
| Static proof | Scoped source check reports old active source call sites `0`, brace delta `0`, and `git diff --check` exit `0` with line-ending warning only | `../Reports/UNITY_GLOBAL_TELEMETRY_BLACKBOX_ACCESSOR_PASS_UNKNOWN_20260528.json` |

## 2026-05-28 Hardware Tier Read Accessor Pass

| Area | Fact | Proof |
|---|---|---|
| Hardware tier getters | `HardwareTierDetector` read properties no longer call `EnsureInitialized()`; boot/explicit owners initialize the snapshot | `../Reports/UNITY_HARDWARE_TIER_READ_ACCESSOR_PASS_UNKNOWN_20260528.md` |
| Quest policy getters | Quest Vulkan policy read properties no longer initialize from getters; Quest candidate fails closed until initialized | `../Reports/UNITY_HARDWARE_TIER_READ_ACCESSOR_PASS_UNKNOWN_20260528.md` |
| Static proof | `EnsureInitialized()` remains only in BeforeSceneLoad calls and method declarations in touched files; brace delta `0`; scoped diff check exit `0` | `../Reports/UNITY_HARDWARE_TIER_READ_ACCESSOR_PASS_UNKNOWN_20260528.json` |

## 2026-05-28 Global Telemetry DataVault Binding Pass

| Area | Fact | Proof |
|---|---|---|
| Blackbox Vault route | `GlobalTelemetryBus` blackbox storage now consumes a cold-bound `_blackboxBoundVault` instead of reading `GlobalRegistry.DataVault` from its bind helper | `../Reports/UNITY_GLOBAL_TELEMETRY_DATAVAULT_BINDING_PASS_UNKNOWN_20260528.md` |
| Registry bind/unbind | `GlobalRegistry.RegisterDataVault()` binds the blackbox route and `UnregisterDataVault()` clears it with the authoritative Vault | `../Reports/UNITY_GLOBAL_TELEMETRY_DATAVAULT_BINDING_PASS_UNKNOWN_20260528.md` |
| Static proof | Direct `GlobalRegistry.DataVault` reads in `GlobalTelemetryBus.cs` and `GlobalTelemetryBus.Blackbox.cs` are `0`; brace delta `0`; scoped diff check exit `0` | `../Reports/UNITY_GLOBAL_TELEMETRY_DATAVAULT_BINDING_PASS_UNKNOWN_20260528.json` |

## 2026-05-28 Memory Sentinel Vault Lifecycle Pass

| Area | Fact | Proof |
|---|---|---|
| MemorySentinel Vault route | `MemorySentinelRuntime` now subscribes to `GlobalRegistry` DataVault hot-swap and rebinds from a cold owner route | `../Reports/UNITY_MEMORY_SENTINEL_VAULT_LIFECYCLE_PASS_UNKNOWN_20260528.md` |
| Frame-phase buffer access | `VisualSyncTick()` and `PublishHashDelta()` now resolve existing Vault buffers through `TryResolveVaultBuffers()` instead of opening them | `../Reports/UNITY_MEMORY_SENTINEL_VAULT_LIFECYCLE_PASS_UNKNOWN_20260528.md` |
| Static proof | Touched source brace delta is `0`; scoped diff check exit `0`; `TryResolveVaultBuffers` active references are `4` | `../Reports/UNITY_MEMORY_SENTINEL_VAULT_LIFECYCLE_PASS_UNKNOWN_20260528.json` |

## 2026-05-28 MathGuard DataVault Binding Pass

| Area | Fact | Proof |
|---|---|---|
| MathGuard Vault route | `MathGuard` no longer reads `GlobalRegistry.DataVault`; it consumes a Vault bound by `GlobalRegistry.RegisterDataVault()` | `../Reports/UNITY_MATHGUARD_DATAVAULT_BINDING_PASS_UNKNOWN_20260528.md` |
| Owner unbind route | `GlobalRegistry.UnregisterDataVault()` clears MathGuard invalid-number handles when the authoritative Vault is removed | `../Reports/UNITY_MATHGUARD_DATAVAULT_BINDING_PASS_UNKNOWN_20260528.md` |
| Static proof | `MathGuard.cs` direct `GlobalRegistry.DataVault` reads are `0`; `CacheDataVaultCold` references are `0`; touched source brace delta is `0` | `../Reports/UNITY_MATHGUARD_DATAVAULT_BINDING_PASS_UNKNOWN_20260528.json` |

## 2026-05-28 Homeostasis DataVault Rebind Pass

| Area | Fact | Proof |
|---|---|---|
| Homeostasis Vault rebind | `HomeostasisBrain` reopens runtime buffers from the DataVault hot-swap route before pre-simulation frame code resumes | `../Reports/UNITY_HOMEOSTASIS_DATAVAULT_REBIND_PASS_UNKNOWN_20260528.md` |
| Frame-phase resolver | `PreSimulationTick()` uses a pure `TryResolveRuntimeBuffers()` route; buffer open/acquire is isolated to `OpenOrAcquireRuntimeBuffers()` | `../Reports/UNITY_HOMEOSTASIS_DATAVAULT_REBIND_PASS_UNKNOWN_20260528.md` |
| Static proof | `OpenOrAcquireRuntimeBuffers` refs `3`; `TryResolveRuntimeBuffers` refs `4`; `TryResolveHardwareMetrics` refs `0`; touched source brace delta `0` | `../Reports/UNITY_HOMEOSTASIS_DATAVAULT_REBIND_PASS_UNKNOWN_20260528.json` |

## 2026-05-28 APEX Homeostasis Verification

| Area | Fact | Proof |
|---|---|---|
| Zero-GC self-audit | Changed ranges `334-345`, `992-1044`, `1047-1080`, and `1187-1207` have `0` hits for reference `new`, `string.Format`, `.ToString()`, LINQ query calls, and `foreach` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_HOMEOSTASIS_20260528.json` |
| Hash proof | APEX JSON SHA-256 is `A6484321E054D644F5EF220EC97F9ABD6679BF89524C5C6B93EEE1886768ADDD`; source report JSON SHA-256 is `C28063C63403175C4679E073561C728A754E0F1E9CE5952F8DF86B5A626EA038` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_HOMEOSTASIS_20260528.json.sha256` |
| Build throttle | CPU sample was `100%`, active `dotnet.exe` PID `62124`, and this verification launched `0` builds | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_HOMEOSTASIS_20260528.json` |

## 2026-05-28 Foveated Native Ownership Pass

| Area | Fact | Proof |
|---|---|---|
| Persistent native aliases | `FoveatedSimulationManager` private `NativeArray<T>` fields are `0` | `../Reports/UNITY_FOVEATED_NATIVE_OWNERSHIP_PASS_UNKNOWN_20260528.md` |
| DataVault writer locks | Score-position and telemetry writes now use `TryAcquireWriteLock` with `finally` release; current file count is `2` acquires and `2` releases | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_FOVEATED_20260528.md` |
| Job pointer pins | Importance-job BufferIDs `73220..73226` are pinned with `TryLockBuffer` before scheduling and unlocked after completion/failure/dispose | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_FOVEATED_20260528.json` |
| Route contract | `IFoveatedDispatcher.TryResolveTick` active references are `0`; mutating tick path is now `TryAdvanceTick` with `3` active references | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_FOVEATED_20260528.json` |
| Build throttle | Initial CPU sample was `100%` with active `csc.exe` PID `16180` and `dotnet.exe` PID `31636`; final guard was CPU `61.5%` with active `dotnet.exe` PID `6088`; this pass launched `0` builds | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_FOVEATED_20260528.json` |
| Hash proof | APEX JSON SHA-256 is `999B48C6A45FF134C2AF5F1ABA9EBE7D9FD164F8E9D9931F09DAB67AA30F5838`; updated source report JSON SHA-256 is `0CE6153E7862186ABF48F2D1C610690E6E01863B46D37E3B91E24C83DC1F578F` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_FOVEATED_20260528.json.sha256` |

## 2026-05-28 Homeostasis Scalability Read Accessor / Writer Lock Pass

| Area | Fact | Proof |
|---|---|---|
| Read route purity | `TryReadMockHeavyLoad`, `TryResolveMockTerrainSamplerStatus`, `TryResolveCsvScratch`, `TryResolveScalabilityTelemetry`, and public `TryGet*` facades have `Ensure=0`, `TryResolveOrAcquire=0`, and native writes `0` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_HOMEOSTASIS_SCALABILITY_20260528.json` |
| DataVault write discipline | State, telemetry, mock-heavy-load, and editor terrain status writes use writer locks; player terrain sampler job pins `ShinobuScalabilityMockScatterDensity` with `TryLockBuffer` and releases with `TryUnlockBuffer` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_HOMEOSTASIS_SCALABILITY_20260528.md` |
| Continuous scalability | The source still has binary low-end token count `0`; `GlobalQualityWeight` remains the continuous scalar for stochastic decimation, render scale, and MathLOD | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_HOMEOSTASIS_SCALABILITY_20260528.json` |
| Build throttle | CPU sample was `100%` with active `dotnet.exe` PID `59388` and `VBCSCompiler.exe` PID `14544`; this pass launched `0` builds | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_HOMEOSTASIS_SCALABILITY_20260528.json` |
| Hash proof | APEX JSON SHA-256 is `45202940359096680F0D9D82DB57DFB80FC4D0B8A34DE47FB28B5F53E6F90472` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_HOMEOSTASIS_SCALABILITY_20260528.json.sha256` |

## 2026-05-28 Content Authority DataVault Lock Pass

| Area | Fact | Proof |
|---|---|---|
| Mutating route names | `ContentRuntimeServices.cs` active `TryResolveTelemetryPointer`, `TryResolveTelemetryBuffers`, `TryResolvePendingLoads`, `TryResolveOrAcquire`, and `TryResolveNormalized` references are `0`; mutating routes use `OpenOrAcquire*Write*` | `../Reports/UNITY_CONTENT_AUTHORITY_DATAVAULT_LOCK_PASS_UNKNOWN_20260528.md` |
| DataVault write discipline | Bundle refs, pending loads, and telemetry writes use writer locks and `finally` release paths | `../Reports/UNITY_CONTENT_AUTHORITY_DATAVAULT_LOCK_PASS_UNKNOWN_20260528.json` |
| Static proof | Source SHA-256 is `E4A64A1BF9DC433AE2A5990231BB25D4834555BE82086126A5E7AD8C3B60A24D`; brace counts `268/268`; scoped diff check exit `0`; added-line forbidden scan is clean | `../Reports/UNITY_CONTENT_AUTHORITY_DATAVAULT_LOCK_PASS_UNKNOWN_20260528.json` |
| Build throttle | Intended build check skipped at CPU `100%` with one active compiler/dotnet process; follow-up guard CPU `57%`, active `dotnet.exe` PID `48280`; this pass launched `0` builds | `../Reports/UNITY_CONTENT_AUTHORITY_DATAVAULT_LOCK_PASS_UNKNOWN_20260528.json` |
| Hash proof | Report JSON SHA-256 is `02E88835B73E03525C8174EBC9180C6E9AF866AD66BBB5201B1F63371C224D50` | `../Reports/UNITY_CONTENT_AUTHORITY_DATAVAULT_LOCK_PASS_UNKNOWN_20260528.json.sha256` |

## 2026-05-28 AUP Origin Route Resolve/Open Split

| Area | Fact | Proof |
|---|---|---|
| Mutating route names | `AupOriginShiftCoordinator.cs` active `TryResolveOrAcquire` and `EnsureRuntimeState` references are `0`; owner mutation is `OpenOrAcquireRuntimeStateForOwnerRoute` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_AUP_ORIGIN_ROUTE_20260528.md` |
| Frame/shift route | `TickPreSimulation`, `ScheduleVaultOriginRebase`, `RecordRebaseCompletion`, and `TryGetEditorSnapshot` use resolve-only `TryResolveRuntimeState` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_AUP_ORIGIN_ROUTE_20260528.json` |
| Static proof | AUP source SHA-256 is `4FE8D7649224590D900F97E2C79B53E24FEC7831C30E750920C3B39316FAF04D`; FloatingOrigin source SHA-256 is `86C8B2CC51620AED9DFF22C522A168BD4A643F42053ABAD00D649F198A2E105F`; added-line forbidden scan is clean | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_AUP_ORIGIN_ROUTE_20260528.json` |
| Build throttle | CPU sample was `87.5%` with active `dotnet.exe` PIDs `37700` and `67192`; this pass launched `0` builds | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_AUP_ORIGIN_ROUTE_20260528.json` |
| Hash proof | Report JSON SHA-256 is `2619A7A03D51E6A7C3D9BA1A2F122B73C63032C10E43478358929F2E1142267F` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_AUP_ORIGIN_ROUTE_20260528.json.sha256` |
| Residual closed statically | AUP direct NativeArray writer-lock/job-pin residual was superseded by the AUP lock/pin pass below | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_AUP_LOCK_PIN_PASS_20260528.json` |

## 2026-05-28 AUP Origin Writer-Lock / Job-Pin Pass

| Area | Fact | Proof |
|---|---|---|
| Owner writer locks | AUP owner buffers `73030..73037` now use writer-lock paths for active writes; release paths are guarded with `finally` in the write bodies | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_AUP_LOCK_PIN_PASS_20260528.md` |
| Scheduled job pins | Scheduled AUP rebase pins `MockStatesBuffer`, `CounterBuffer`, `MockHistoricalPointsBuffer`, `VaultHotEntityData`, and tether historical buffers with `TryLockBuffer`; `HectonFloatingOrigin` releases pins after job completion and again in `finally` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_AUP_LOCK_PIN_PASS_20260528.json` |
| Static proof | Source SHA-256 values are `5F11D259FF25C1469463034608EEFD2CC783A9B3F9D32A0E5C17FA3ED52EBF03` and `061B6D3820FF35AA0CC4F51068F97ACA7EAE555309D217354787D57E0AC67EC6`; added-line forbidden scan is clean | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_AUP_LOCK_PIN_PASS_20260528.json` |
| Build boundary | One throttled build was attempted after CPU `31.2%` and no active compiler; it timed out at `904s` and captured `60` unrelated compile errors in `ModularEquipmentEngine.cs` and `SargassumMicroFaunaBoids.cs` | `../Reports/BUILD_UNKNOWN_AUP_LOCK_PIN_PASS_20260528.log` |
| Hash proof | Report JSON SHA-256 is `E10FCFC8F1A3824A92380982B7C182854DD1FF203D7D94314127D7EC1C7BDD4A` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_AUP_LOCK_PIN_PASS_20260528.json.sha256` |
| Residual | Runtime proof is still absent: no Unity import, Console, Play Mode, profiler/GCMonitor, player build, device run, or crash dump | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_AUP_LOCK_PIN_PASS_20260528.md` |

## 2026-05-28 Input Route Naming Pass

| Area | Fact | Proof |
|---|---|---|
| Mutating route name | `TryResolveOrAcquireInputBuffer<T>` active references are `0`; the mutating helper is now `OpenOrAcquireInputBufferForOwnerRoute<T>` with `21` active references | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_INPUT_ROUTE_NAMING_PASS_20260528.md` |
| Owner route evidence | The helper declaration is `InputDispatcher.cs:992`; it calls `vault.EnsureGenerationHandle<T>` at `InputDispatcher.cs:1006`, so it is explicitly an owner/open route | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_INPUT_ROUTE_NAMING_PASS_20260528.json` |
| Zero-GC static scan | Added-line scan has reference `new=0`, `string.Format=0`, `.ToString()=0`, LINQ `0`, `foreach=0`, `.Complete()=0`, added `GlobalRegistry=0` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_INPUT_ROUTE_NAMING_PASS_20260528.json` |
| Build throttle | CPU sample was `51.7%`; no active dotnet/csc/VBCSCompiler was listed; this pass launched `0` builds because CPU exceeded the 50% guard | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_INPUT_ROUTE_NAMING_PASS_20260528.json` |
| Hash proof | Report JSON SHA-256 is `A38DE05D5D31067D593E305C8CA081AC44B843EAFDF1DD2DD9667E93BC062528` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_INPUT_ROUTE_NAMING_PASS_20260528.json.sha256` |
| Residual | Runtime proof is still absent: no Unity import, Console, Play Mode, profiler/GCMonitor, player build, device run, or crash dump | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_INPUT_ROUTE_NAMING_PASS_20260528.md` |

## 2026-05-28 Simulation Bucketer Writer-Lock / Job-Pin Pass

| Area | Fact | Proof |
|---|---|---|
| Writer locks | Synchronous `ModuloSimulationBucketer` writes now route through `TryAcquireWriteView` / `TryAcquireWriteLock` and release in `finally` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_SIMULATION_BUCKETER_LOCK_PIN_PASS_20260528.md` |
| Scheduled job pins | Rebalance job buffers `SimulationBucketEntityCostEwma`, `SimulationBucketEntityWork`, `SimulationBucketRebalanceLoads`, and `SimulationBucketRebalanceResult` are pinned with `TryLockBuffer` and released through `ReleaseRebalanceBufferPins` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_SIMULATION_BUCKETER_LOCK_PIN_PASS_20260528.json` |
| BufferIDs | Secured BufferIDs are `98`, `99`, `100`, `101`, `102`, `103`, `104`, and `194` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_SIMULATION_BUCKETER_LOCK_PIN_PASS_20260528.json` |
| Continuous scalability | Existing `_qualityWeight01`, `SmoothStep01`, `ResolveActiveSlowBucketCount`, and `ResolveRebalanceCadenceFrames` remain continuous; no binary `isLowEnd` route was added | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_SIMULATION_BUCKETER_LOCK_PIN_PASS_20260528.json` |
| Static proof | Source SHA-256 is `51F68EBFFA50165B4153E5C3DCC8E3151D418B494F4F9256CDDD6B1DE24AA1BD`; added-line forbidden scan is clean; brace count is `150/150` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_SIMULATION_BUCKETER_LOCK_PIN_PASS_20260528.json` |
| Build throttle | CPU sample was `99.8%` with active `dotnet.exe` PIDs `10736` and `42644`; this pass launched `0` builds | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_SIMULATION_BUCKETER_LOCK_PIN_PASS_20260528.json` |
| Hash proof | Report JSON SHA-256 is `CD394CF9AFBBA0E9A82E3CF6C8FCE17D5F32BC461F5C6E50DCCF3E3CC3CF555F` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_SIMULATION_BUCKETER_LOCK_PIN_PASS_20260528.json.sha256` |
| Residual | Runtime proof is still absent: no Unity import, Console, Play Mode, profiler/GCMonitor, player build, device run, or crash dump | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_SIMULATION_BUCKETER_LOCK_PIN_PASS_20260528.md` |

## 2026-05-28 APEX Current Recheck

| Area | Fact | Proof |
|---|---|---|
| Current source hashes | InputDispatcher, HapticSynth, and ModuloSimulationBucketer hashes were recomputed after docs/source edits | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_CURRENT_RECHECK_20260528.json` |
| Combined Zero-GC scan | Current touched-source added lines have reference `new=0`, `string.Format=0`, `.ToString()=0`, LINQ `0`, `foreach=0`, `.Complete()=0`, `GlobalRegistry=0`, binary low-end tokens `0` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_CURRENT_RECHECK_20260528.json` |
| Build throttle | CPU sample was `98.7%` with active `dotnet.exe` PID `68208`; this recheck launched `0` builds | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_CURRENT_RECHECK_20260528.json` |
| Hash proof | Report JSON SHA-256 is `562AE537F4D33103F2D07D0EAC8AC88CC73B8210DC6A1F42D2F4B3473753DAB7` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_CURRENT_RECHECK_20260528.json.sha256` |
| Residual | Runtime proof is still absent: no Unity import, Console, Play Mode, profiler/GCMonitor, player build, device run, or crash dump | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_CURRENT_RECHECK_20260528.md` |

## 2026-05-28 UNKNOWN Job Admission Writer-Lock Pass

| Area | Fact | Proof |
|---|---|---|
| Source | `BurstTokenBucketJobAdmissionService.cs` was updated to lock `SystemID.JobAdmission` DataVault write paths and remove mutable `Resolve*` helpers | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_JOB_ADMISSION_LOCK_PASS_20260528.json` |
| BufferIDs | `JobAdmissionLaneBudgets=283`, `JobAdmissionBaseRefill=284`, `JobAdmissionJobHashes=285`, `JobAdmissionEwmaCosts=286`, `JobAdmissionBlackBox=287` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_JOB_ADMISSION_LOCK_PASS_20260528.md` |
| Zero-GC static scan | Added lines have reference `new=0`, `string.Format=0`, `.ToString()=0`, LINQ `0`, `foreach=0`, `.Complete()=0`, `GlobalRegistry=0`, binary low-end tokens `0` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_JOB_ADMISSION_LOCK_PASS_20260528.json` |
| Build throttle | CPU sample was `90.0%`, active `dotnet.exe` PID `28668`, active `csc.exe` PID `21340`; this pass launched `0` builds | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_JOB_ADMISSION_LOCK_PASS_20260528.json` |
| Hash proof | Report JSON SHA-256 is `ECBE6B5EA34B997ACC118B836B3BC162164C1E7BB8CCB48D0F468F5BA91A7686` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_JOB_ADMISSION_LOCK_PASS_20260528.json.sha256` |
| Residual | Runtime proof is still absent: no Unity import, Console, Play Mode, profiler/GCMonitor, player build, device run, or crash dump | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_JOB_ADMISSION_LOCK_PASS_20260528.md` |

## 2026-05-28 UNKNOWN Hardware Thermal Owner-Route Pass

| Area | Fact | Proof |
|---|---|---|
| Source | `HardwareThermalService.cs` hot thermal severity and blackbox writes now use resolve-existing `TryAcquireThermal*WriteView` helpers; cold prewarm uses explicit `OpenOrAcquireThermal*ForOwnerRoute` helpers | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_HARDWARE_THERMAL_OWNER_ROUTE_PASS_20260528.json` |
| BufferIDs | `HardwareThermalSeverity=166`, `HardwareThermalBlackBox=167` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_HARDWARE_THERMAL_OWNER_ROUTE_PASS_20260528.md` |
| Zero-GC static scan | Added lines have reference `new=0`, `string.Format=0`, `.ToString()=0`, LINQ `0`, `foreach=0`, `.Complete()=0`, `EnsureGenerationHandle=0`, `GlobalRegistry=0`, binary low-end tokens `0` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_HARDWARE_THERMAL_OWNER_ROUTE_PASS_20260528.json` |
| Build throttle | CPU sample was `100%`, active `dotnet.exe` PID `65020`; this pass launched `0` builds | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_HARDWARE_THERMAL_OWNER_ROUTE_PASS_20260528.json` |
| Hash proof | Report JSON SHA-256 is `3CF3074A50A62DB74DB866C832FF78E0D0ACB44C3704E78AF9E391FA962CC918` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_HARDWARE_THERMAL_OWNER_ROUTE_PASS_20260528.json.sha256` |
| Residual | Runtime proof is still absent. Existing binary thermal-pressure registry override remains documented residual pending coordinated GlobalRegistry/Homeostasis API work | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_HARDWARE_THERMAL_OWNER_ROUTE_PASS_20260528.md` |

## 2026-05-28 UNKNOWN Platform Pressure Continuous Scaling Pass

| Area | Fact | Proof |
|---|---|---|
| Source | `PlatformBatteryWatchdog.cs` and `PlatformAdaptiveBudgetGovernor.cs` no longer call the binary platform/battery transient-low override route from the clean platform pressure files | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_PLATFORM_PRESSURE_CONTINUOUS_PASS_20260528.json` |
| Continuous scaling | Platform pressure now emits continuous pressure, quality, render-scale, cadence, and optional-HUD weights and composes with `HomeostasisBrain.GlobalQualityWeight` / `HomeostasisBrain.TargetRenderScale01` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_PLATFORM_PRESSURE_CONTINUOUS_PASS_20260528.md` |
| Zero-GC static scan | Added lines have reference `new=0`, `string.Format=0`, `.ToString()=0`, LINQ `0`, `foreach=0`, `.Complete()=0`, `GlobalRegistry=0`, binary low-end tokens `0` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_PLATFORM_PRESSURE_CONTINUOUS_PASS_20260528.json` |
| Build throttle | Final CPU sample was `100%`, active `dotnet` PID `57828`; this pass launched `0` builds | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_PLATFORM_PRESSURE_CONTINUOUS_PASS_20260528.json` |
| Hash proof | Report JSON SHA-256 is `08E9154E9A309D2F29B8523D16363D103C1F40C9ED9326B25179209B61AB348A` | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_PLATFORM_PRESSURE_CONTINUOUS_PASS_20260528.json.sha256` |
| Residual | Runtime proof is still absent. Central binary transient override API/callers remain in dirty `GlobalRegistry`, `HomeostasisBrain`, and `HardwareThermalService` files pending coordinated central work | `../Reports/APEX_FINAL_VERIFICATION_UNKNOWN_PLATFORM_PRESSURE_CONTINUOUS_PASS_20260528.md` |

## Validation

| Validator | Required state |
|---|---|
| Current `Tools/OOP_Doc_Scanner.py` | 2026-05-28 `finalPass=true`; activeFileCount `708`; sourceSyncPass `true`; active stale parameter files `0`; wordReductionPercent `30.358189496725423` |
| Current `Tools/VerifyDocStructure.py` | 2026-06-09 `pass=true`; `docScope=authority_spine`; activeDocCount `282`; rootTextDocCount `76`; broken links `0`; duplicate headers `0`; fence issues `0`; stale parameter files `0`; missing-BOM count `81` informational |
| `Tools/OOP_Doc_Scanner.py` | `finalPass=true`; source sync; reduction `>=30%`; markers `0`; stale parameter files `0` |
| `Tools/VerifyDocStructure.py` | `pass=true`; broken links `0`; duplicate headers `0`; fence issues `0`; stale parameter files `0` |
| 1334 final scan | `DOCUMENTATION_OPTIMIZATION_REPORT_1334_FINAL_SCAN.json`: `finalPass=true`; active docs `693`; stale parameter files `0`; reduction `48.0975%` |
| 1334 final structure | `DOC_STRUCTURE_VALIDATION_1334_FINAL.json`: `pass=true`; broken links `0`; duplicate headers `0`; fence issues `0`; stale parameter files `0` |

This ledger may cite CLI compile artifacts. It is not Unity import, Play Mode, profiler, GC, player-build, or visual proof.
## 2026-06-02 Narrative Runtime Source Sweep

| Area | Correction | Evidence boundary |
|---|---|---|
| Narrative / AppliedLore route | `Docs/Lore/Implementation_Notes.md` now separates source-present AppliedLore runtime route from missing scene bindings. Source map, topology, and coverage matrix list `H8AppliedLoreRuntime`, generated hashes, PDA streamer, scanner/terminal owners, QuestDAG, progression bridge, and AudioLog owners. | Static source scan plus existing AppliedLore audit text. Unity import, Play Mode, PDA/terminal visual proof, save/load continuity, and scene/prefab non-zero lore hash assignments remain pending. |
| Lore encyclopedia index | `Docs/Lore/Encyclopedia/Article_Index.md` no longer marks AppliedContent packets as ready in runtime; packet entries are `source-authored` with explicit route-binding proof pending. | Static doc correction only. Scene binding, Unity route, PDA/scanner/terminal UI proof, and save/load continuity remain pending. |

## 2026-06-02 Meta Runtime Source Sweep

| Area | Correction | Evidence boundary |
|---|---|---|
| Optimization / Networking / Modding / QA / Build tooling | Source map, topology, and coverage matrix now name concrete owners for asset lifecycle/load dispatch, VRAM pressure/telemetry, RT lifecycle/pool services, rollback/Merkle/prediction, envelope-only mod API/sandbox/loader, mod-world persistence, QA/headless blackbox runners, and editor build/preflight validators. `Docs/Modding` signal split corrected to `175 / 2 / 173` with `rg --pcre2`. | Static source scan and docs/schema reconciliation only. Network loopback, mod envelope runtime playbook, QA/headless run artifacts, player build, platform/device, profiler, GC, and VRAM proof remain pending. |

## 2026-06-02 Presentation Runtime Source Sweep

| Area | Correction | Evidence boundary |
|---|---|---|
| UI / PDA / Visor / Audio / VFX / Graphics | Source map, topology, and coverage matrix now name concrete presentation owners: PDA streamer, subtitle/font/loading managers, visor HUD/URP features, foveated render commander, thermal DRS adapter, culling/TBDR services, player-critical procedural audio, vocal warning/music/adaptive stem systems, marine snow/propwash/plasma/fog VFX buffers. | Static source scan only. UI GC, audio-thread/device, RenderGraph/Frame Debugger, shader import, visual capture, VRAM/DRS, Play Mode, and player-build proof remain pending. |

## 2026-06-02 Gameplay Economy Source Sweep

| Area | Correction | Evidence boundary |
|---|---|---|
| Inventory / crafting / tools / survival / economy | Source map, topology, and coverage matrix now name concrete owners for native SOA inventory, inventory service manager, powered fabricator, recipe jobs, physical fabrication output, survival saveable slow/late tick, auxiliary equipment router, AUP/DataVault loot magnet, powered recycler, first-hour director, data archaeology, endings, airlocks, and tools/scavenging. | Static source scan only. Copper acquisition, boot-to-craft-to-save/load, inventory/fabricator UI feedback, loot pickup route, profiler, GC, Play Mode, and player-build proof remain pending. |

## 2026-06-02 World Streaming Voxel Scatter Source Sweep

| Area | Correction | Evidence boundary |
|---|---|---|
| World / terrain / voxel / scatter | Source map, topology, and coverage matrix now split the old broad world row into streaming/residency/HLOD, voxel/geology/terrain seams, and procedural scatter/vegetation/persistent content. Concrete owners include `WorldChunkResidencyManager`, `TerrainChunkPagerRuntime`, `HectonVoxelEngine`, `VoxelDeltaProcessor`, geology seam/voxel bridge, scatter backends, vegetation DataVault/culling routes, persistent world registry, procedural wreckage, ore/resource distribution, regrowth, biome SDF/transition managers, and HLOD/impostor/culling services. | Static source scan only. Scene wiring, Addressables/hydration, voxel carve save/load, terrain seam visual proof, scatter backend parity, vegetation/wreck/resource visual proof, HLOD capture, VRAM/frame hitch, profiler, GC, Play Mode, and player-build proof remain pending. |

## 2026-06-02 Construction Habitat Power Source Sweep

| Area | Correction | Evidence boundary |
|---|---|---|
| Construction / habitat / flooding / power / logistics | Source map, topology, and coverage matrix now split the old broad construction row into construction/deconstruction, flooding/fluid pipes/bulkheads, and power/logistics/base modules/drones. Concrete owners include `ConstructionManager`, `BaseModule`, habitat construction/graph managers, deconstruction kernel, module catalog, fluid incursion director/jobs, fluid pipe graph, sump pump grid, bulkhead/hatch containment, power grid service, logistics graph, WFC power boot, RTG, battery charger logistics, Shinobu logistics router, and drone/repair routes. | Static source scan only. Base-building Play Mode proof, module save/load, deconstruction refund/loot, flood authority, pipe rupture/drainage, bulkhead/hatch interaction, power distribution, battery charge inventory, drone/repair gameplay, UI/VR feedback, profiler, GC, visual proof, and player-build proof remain pending. |

## 2026-06-02 AI Ecosystem Atmosphere Thermodynamics Source Sweep

| Area | Correction | Evidence boundary |
|---|---|---|
| Fauna / cognition / pathfinding / ecosystem | Source map, topology, and coverage matrix now split the old broad AI row into fauna simulation/cognition/damage, AI cognition/voxel pathfinding, and ecosystem/boids/macro migration. Concrete owners include `FaunaSimulationEngine`, `FaunaBrain`, `PredatorCognitionDomain`, acoustic SDF, stress spawn director, fauna kinematics/IK/tentacles, utility cognition vault/jobs, apex and alpha cognition vaults, path funnel, voxel A*, acoustic echo, `EcosystemDirector`, ecosystem balancer, flora-fauna symbiosis, macro migration, nutrient drift, fauna spatial hash, and vegetation fear field. | Static source scan only. Scene spawn integration, combat damage route, gameplay path requests, SDF/navgrid wiring, deterministic ordering, swarm visual proof, fault-dump runtime proof, profiler, GC, and First 20 Minutes impact remain pending. |
| Atmosphere / weather / ocean / thermodynamics | Source map, topology, and coverage matrix now split the old broad atmosphere row into base atmosphere/gas/weather/ocean surface and thermodynamics/hazards/reactor thermal grid. Concrete owners include `BaseAtmosphereEngine`, `BaseAtmosphereLogisticsRuntime`, gas dynamics, toxic outgassing, ocean surface atmosphere runtime, global weather director, storm propagation, abyssal thermodynamics solver, thermodynamics hazard grid, and reactor thermal jobs. | Static source scan only. Base room wiring, gas/player survival route, weather/ocean visual proof, reactor/power/atmosphere coupling, deterministic cheat boundaries, continuous quality load-shed, profiler, GC, device, and player-build proof remain pending. |

## 2026-06-02 Combat Vehicle Physics Source Sweep

| Area | Correction | Evidence boundary |
|---|---|---|
| Combat / armor / trauma / hazards | Source map, topology, and coverage matrix now name concrete combat owners: `CombatDamageRuntime`, armor penetration, status effects, ballistics, trauma dispatcher, radiation hazard grid, toxin hazard, vehicle component damage, and hull dent shader bridge. | Static source scan only. Weapon/hazard scene route, target registration, vehicle damage-to-hull visual route, HUD/VFX/audio feedback, profiler, GC, and player-build proof remain pending. |
| Vehicle physics / force application / buoyancy / tethers | Source map, topology, and Echelon 6 now split the old vehicle/equipment physics row into submarine/seaglide/exosuit/docking and physics force/buoyancy/cavitation/tether/cable routes. Concrete owners include submarine dynamics/gyro/ballast, docking autopilot, seaglide hydrodynamics, exosuit kinematics, vehicle motor, buoyancy displacement, analytical waves, async buoyancy readback, cavitation, cable/tether solvers, and harpoon tension. | Static source scan only. PhysicsApplySystem ownership proof, collision correctness, same-frame job/readback audit, controller scene proof, ballast/docking gameplay, tether/cable gameplay, visual/audio feedback, profiler, GC, device, and player-build proof remain pending. |

## 2026-06-02 Player Interaction KCC Source Sweep

| Area | Correction | Evidence boundary |
|---|---|---|
| Player input / KCC / movement | Source map, topology, and Echelon 4 now split the old player row into deterministic input/XR buffers, hydrodynamic KCC, and player kinematics. Concrete owners include `InputDispatcher`, haptic synth, `PlayerInputState`, `PlayerRuntimeContext`, `PlayerKinematicsRuntime`, `HydrodynamicKccRuntime`, and `CoreDeterminismSignals`. | Static source scan only. Controller/device route, KCC collision correctness, environment provider wiring, same-frame job/readback audit, First 20 Minutes movement proof, profiler, GC, Play Mode, and player-build proof remain pending. |
| Physical interaction / VR hands / tool signal queue | Source map, topology, and Echelon 4 now name concrete owners for look target, physical grab/snap, VR hand bridge, somatic comfort, and tool/equipment interaction queues. Concrete owners include `PlayerInteraction`, `PhysicalInteractionHandler`, `PhysicalHandController`, `VRInteractionKinematicBridge`, `VRSomaticProvider`, `EquipmentInteractionContracts`, `EquipmentInteractionHandler`, `InteractableRegistry`, `VRLeakPatchWeldTarget`, tool haptics, tool kinematics, and auxiliary equipment router. | Static source scan only. Physical controller scene proof, grab/force ownership, XR device route, hand bridge runtime proof, queued surface query, tool-to-damage/voxel route, UI/haptic feedback, profiler, GC, Play Mode, and player-build proof remain pending. |

## 2026-06-02 Meta Runtime Support Split Source Sweep

| Area | Correction | Evidence boundary |
|---|---|---|
| Optimization / asset lifecycle / VRAM / RT | Source map, topology, and Echelon 9 now split optimization from generic meta tooling. Concrete owners include `AssetLifecycleGovernor`, `AssetLoadDispatcher`, `AssetRecord`, `VRAMMonitor`, `VRAMPressureMonitor`, `VRAMEnforcer`, `RenderTextureLifecycleTracker`, `RenderTexturePool`, RT managers, `ThermalDynamicResolutionAdapter`, `TBDRPipelineSurgeonRuntime`, and `InstanceCullingService`. | Static source scan only. Addressables release, RT leak, Memory Profiler/VRAM capture, DRS visual proof, profiler, GC, and frame proof remain pending. |
| Rollback networking / prediction | Source map, topology, and Echelon 9 now split rollback networking into its own route. Concrete owners include `HectonNetworkManager`, `HectonRollbackNetcodeRuntime`, and `RollbackNetcodeContracts`: snapshots, Merkle nodes, prediction journals, mock jitter packets, rollback signals, snapshot/restore/hash/mismatch jobs, and quality-scaled rollback math. | Static source scan only. Loopback/device, authoritative transport, packet serialization, desync recovery, rollback correctness, profiler, and GC proof remain pending. |
| Modding API / sandbox / persistence | Source map, topology, and Echelon 9 now split modding from QA/build. Concrete owners include `HectonAPI`, `HectonEventBus`, game/mod event contracts, event projection, `ModCommandDispatcher`, `FutureCommandSandboxValidator`, `ModLoader`, resource proxy, registry/settings/runtime state, and `ModWorldPersistenceManager`. | Static source scan only. Mod envelope runtime playbook, external starter kit validator proof, mod load/play proof, command budget/security proof, and save roundtrip proof remain pending. |
| QA watchdog / endurance / headless | Source map, topology, and Echelon 9 now split QA harnesses. Concrete owners include `QA_WatchdogBot`, `QAEnduranceWatchdogBot`, `QAWatchdogGcAllocationFuzzer1524`, headless simulation/stress runners, Shinobu38 watchdog runtime, and Jacobi stress fuzzer. | Static source scan only. Fresh QA/headless run artifacts, current CSV/blackbox reports, profiler/GC execution proof, and CI wiring proof remain pending. |
| Editor build / platform / SDK tooling | Source map, topology, and Echelon 9 now split editor tooling. Concrete owners include XR/OpenXR readiness, Quest Vulkan/URP configurator, graphics API/native plugin/shader/thread/machine-code validators, debug metadata stripping, platform compatibility audit, build playtest log, and mod sandbox/kernel editor tooling. | Static source scan only. Player build, platform/device, current CI validator output, Quest/PCVR build, and SDK tool run proof remain pending. |
