# METRIC_PHI_ANALYST Status

Prompt: `METRIC_PHI_ANALYST`
Domain: `META/ARCHITECTURE`
Task Count: 15
Status: `PHI CALCULATED / VERIFIED MASTER GRADE STATIC_SOURCE + CLI_PYTHON_DATA / RUNTIME PENDING`

## Mandates Selected

- `.agents-skills/README.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/ARCH_Pentarchy_Audit.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `Docs/ARCHITECTURE/HECTON_PHI_STATIC_METRIC.md`

## State Machine

- [x] Task 1 H_PHI_CALCULATION | Justification: implemented `Tools/CalculateHPhi.py` using the documented H-Phi static product; DOD practice: static evidence labeling and py_compile gate | Alternatives Rejected: shell-only one-off regex because it cannot be rerun or audited | Estimate: 0 runtime us; offline scan tooling only
- [x] Task 2 SOURCE_SCAN | Justification: immutable post-mutation snapshot scanned `5015` C# files and `1,723,719` lines; runtime subsection scanned `1279` files and `886,793` lines | Alternatives Rejected: first-party-only default because XML demanded the full C# corpus; monolithic text load because it risks memory spikes | Estimate: 0 runtime us; latest snapshot readback `121.88s` offline tool time
- [x] Task 3 DENSITY_METRIC | Justification: counted `SignalBus<T>.Push/Publish` plus `GlobalSignals.Publish` against direct method-call pressure; DOD practice: regex counters stored in JSON | Alternatives Rejected: claiming semantic coupling without Roslyn dataflow | Estimate: 0 runtime us; static counter only
- [x] Task 4 PURITY_METRIC | Justification: counted non-exempt Unity `Update/LateUpdate/FixedUpdate` methods against `IJob/ITickable/IFixedTickable/ISlowTickable`; latest runtime expanded purity is `1.0` | Alternatives Rejected: treating docs as proof of loop purity | Estimate: 0 runtime us; static counter only
- [x] Task 5 SOVEREIGNTY_METRIC | Justification: counted `NativeArray<T>` and local `new NativeArray<T>` against Vault/DataVault/GetBuffer surfaces; current runtime `DataSovereignty=0.019743027`, strict local-native-array sovereignty `0.089045936` | Alternatives Rejected: claiming ownership correctness from text hits | Estimate: 0 runtime us; static counter only
- [x] Task 6 JSON_REPORT | Justification: generated `Docs/Reports/HECTON_PHI_SCORE_FINAL.json`; status `PHI CALCULATED`, omega `VERIFIED MASTER GRADE STATIC_SOURCE ONLY` | Alternatives Rejected: chat-only result | Estimate: 0 runtime us; report write only
- [x] Task 7 DOMAIN_MAPPING | Justification: updated `Docs/PROJECT_ATLAS.md` with a generated 85-domain index parsed from `Docs/Actual Domains of Project.txt`; latest `domain_index_count=85` | Alternatives Rejected: manual table duplication | Estimate: 0 runtime us; doc write only
- [x] Task 8 NO_UNITY | Justification: no Unity command or project setting was invoked; DOD practice: Python-only artifact path in JSON | Alternatives Rejected: Unity batchmode because prompt explicitly forbids it | Estimate: 0 runtime us
- [x] Task 9 VISUAL_GRAPH | Justification: generated `Docs/Reports/HECTON_PHI_ARCHITECTURE_GRAPH.png` with networkx/matplotlib from dependency data; DOD practice: graph metadata stored in JSON | Alternatives Rejected: ASCII-only graph because prompt required a visual graph image | Estimate: 0 runtime us; offline graph render only
- [x] Task 10 EXECUTE | Justification: post-patch sweep reran `python -B Tools\CalculateHPhi.py --workers 4 --source-roots Assets Packages Tools`, exit `0`; output confirmed `DOMAIN_INDEX_COUNT=85`, `RUNTIME_H_PHI_STATIC=6.7481e-05`, and `STATUS: PHI CALCULATED` | Alternatives Rejected: dry-run reporting and stale under-scoped scan | Estimate: 0 runtime us; latest snapshot readback `121.88s`
- [x] Task 11 RATIONALE | Justification: expanded `Docs/AgentLogs/Rationale_METRIC_PHI_ANALYST.md` with H-Phi math state, binary/endian, math/economy/lore, stale-artifact repairs, and data-sovereignty boundary | Alternatives Rejected: chat-only proof and unverifiable broad claims | Estimate: 0 runtime us; offline docs only
- [x] Task 12 BOTTLENECK_HUNT | Justification: recorded top three static purity bottleneck rows from `Docs/Reports/HECTON_PHI_SCORE_FINAL.json` and preserved the real debt signal (`DataSovereignty=0.019743027`, `StrictLocalNativeArraySovereignty=0.089045936`) | Alternatives Rejected: pretending H-Phi is runtime/profiler evidence | Estimate: 0 runtime us; static tooling only
- [x] Task 13 EDGE_GUARD | Justification: latest post-patch sweep and wrappers validate 42 repo-owned binaries aligned16, 167 Python struct format sites with 0 H8 endian failures, 85-domain atlas fit, physics bases, 1,541,057 explicit economy Monte Carlo node-steps, 1,000,000 crafting Monte Carlo steps, zero FNV collisions, lore/noir metadata, toaster/RTX payloads, and 35 Python verifier commands | Alternatives Rejected: isolated one-off shell snippets that cannot be rerun by SHINOBU | Estimate: 0 runtime us; offline validators only
- [x] Task 14 PROGRESS_BAR | Justification: H-Phi CLI printed progress from `1/5015` through `5015/5015` and regenerated the shared H-Phi JSON/PNG/atlas artifacts after generated-source mutation | Alternatives Rejected: stale report reuse and first-party-only default scan | Estimate: 0 runtime us; latest snapshot readback `121.88s`
- [x] Task 15 STATUS | Justification: status, rationale, and CTO log are updated with current verification commands and residual runtime-proof boundary; `Docs/Reports/METRIC_PHI_DATA_TRUTH_AUDIT.json` reports `DATA_TRUTH_VERIFIED` | Alternatives Rejected: leaving process state tied to stale Babel/PDA/sweep artifacts | Estimate: 0 runtime us; documentation only

## Iteration Log

- Loop 0: Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`. State and rationale files created because they were absent.
- Loop 1: Mandates read and script written. `python -m py_compile Tools\CalculateHPhi.py` returned exit `0`.
- Loop 2: Full Python scan executed. Outputs: `Docs/Reports/HECTON_PHI_SCORE_FINAL.json`, `Docs/Reports/HECTON_PHI_ARCHITECTURE_GRAPH.png`, and updated `Docs/PROJECT_ATLAS.md`.
- Loop 3: Corrected stale Babel verifier defaults/API usage and verified `Babel_Dictionary.h8bin` directly against manifest/header/corpus-hash constraints.
- Loop 4: Ran data truth checks across hash, crafting, lore, Babel, economy, and physics-data verifiers.
- Loop 5: Added `Tools/VerifyDataInquisition.py`; report written to `Docs/Reports/Data_Inquisition_METRIC_PHI_ANALYST.json`; result `DATA_INQUISITION_VERIFIED_STATIC_ONLY`.
- Loop 6: Regenerated stale water-extinction data with `python Tools\OpticsBaker.py`; current primary matrix is `393216` bytes, aligned16, with toaster and RTX-overkill variants present.
- Loop 7: Added `Tools/VerifyMetricPhiDataTruth.py`; verifier scope was tightened to separate H8 little-endian blobs from external PNG/JPEG/PSD container big-endian fields.
- Loop 8: Restored `Tools/CalculateHPhi.py` default roots to `Assets`, `Packages`, and `Tools`, then reran H-Phi.
- Loop 9: Reran explicit economy Monte Carlo and data-truth wrappers; economy proof currently reports `1541057` node-steps, `0` failures, `p99=59.285135135135164`.
- Loop 10: Rebuilt Babel after hash-catalog drift and verified Babel binary/dictionary consistency.
- Loop 11: Hardened `Tools\VerifyDataInquisition.py` with AST-based struct format scanning, physics manifest contract checks, and stateless lookup checks.
- Loop 12: Added/ran `Tools\RunMetricPhiVerifySweep.py` with optional `xxhash` installed under `%TEMP%\metric_phi_xxhash_ref`.
- Loop 13: Fresh sweep exposed stale generated data: Babel constants changed and PDA H8PT extra-visual record layout was stale. Rebuilt `Babel_Dictionary.h8bin`, `H8LocHashes.cs`, `PdaTechnicalLogs.h8bin`, and `PdaTechnicalLogs.manifest.json`.
- Loop 14: Reran full H-Phi after regenerated artifacts. Current report: `generated_at=2026-05-16T10:22:02`, `domain_index_count=85`, `all_source.counters.files=5015`, `all_source.counters.lines=1723604`, `runtime_source.counters.files=1279`, `runtime_source.counters.lines=886678`, `runtime_source.scores.HPhiStatic=6.7481e-05`.
- Loop 15: Reran full verifier sweep after clearing stale bytecode caches and hardening missing replay-reference handling. Result: `VERIFY_SWEEP_PASS`, `totalCommands=28`, `requiredFailures=0`, replay reference `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=466 shuffle=256`.
- Loop 16: Reran standalone wrappers after the sweep. Results: `DATA_TRUTH_VERIFIED`, 36 checks, 0 failures; `VerifyDataInquisition` PASS; `DataTruthInquisition` PASS; explicit economy Monte Carlo PASS; crafting Monte Carlo PASS; py_compile clean.
- Loop 17: Tightened `Tools\VerifyMetricPhiDataTruth.py` binary scan roots from `Data` only to `Data`, `Assets/_Project/Data`, and `Docs/AgentLogs`; current audit checks `39` repo-owned `.bin/.h8bin` blobs with `0` unaligned files.
- Loop 18: Fresh OSHINO pass exposed a Babel dependency on `Docs/Reports/H8_Hash_Catalog_Audit.json`. Patched `Tools\RunMetricPhiVerifySweep.py` so the sweep now runs canonical FNV hash catalog generation before `BabelCompiler`, then `VerifyBabel`/`VerifyBabelDictionary`; removed recursive `VerifyMetricPhiDataTruth` from the sweep and kept it as the post-sweep binder. Final result: sweep `28` commands, `0` required failures; data truth `36` checks, `0` failures.
- Loop 19: Reran the full OSHINO inquisition from disk. H-Phi regenerated at `2026-05-16T11:16:06`; sweep regenerated at `2026-05-16T11:31:35`; Metric data truth regenerated at `2026-05-16T11:29:33`. Results stayed clean: 5015 C# files, 1,723,604 lines, 85 domains, 28 sweep commands, 0 required failures, 36 data-truth checks, 0 failures, 39 aligned binaries, 160 struct sites, 0 endian failures.
- Loop 20: Final machine readback after hygiene showed newer artifact timestamps on disk with the same pass values. Current disk truth was superseded by Loop 22 after a final explicit full-root H-Phi rerun.
- Loop 21: Found and fixed a silent H-Phi report-risk boundary. `rg` and `CalculateHPhi.discover_cs_files` both showed `5015` eligible C# files, while one shared artifact had been overwritten with `4058`; patched `Tools\CalculateHPhi.py` to abort on discovered/scanned count divergence and to atomically replace the JSON report. Reran guarded H-Phi; current report is back to `5015` files and `1,723,604` lines.
- Loop 22: Reran `python -B Tools\CalculateHPhi.py --workers 4 --source-roots Assets Packages Tools` after confirming no active H-Phi/sweep Python writer remained, then rebound `Tools\VerifyMetricPhiDataTruth.py` to that exact report. Current disk truth: H-Phi `generated_at=2026-05-16T11:54:51`, Metric data truth `generatedAt=2026-05-16T11:55:18`, sweep `generatedAt=2026-05-16T11:49:55`; all remain PASS.
- Loop 26: Fresh OSHINO rerun under concurrent-agent load. Reran `CalculateHPhi.py --workers 4 --source-roots Assets Packages Tools`, explicit `MonteCarloEconomySim.py --players 10000 --max-nodes 10000 --threshold-minutes 60`, and `CraftingEconomyMonteCarlo.py --steps 1000000`. Current H-Phi generated `2026-05-16T14:08:37`; current sweep artifact generated `2026-05-16T14:28:42`; current Metric data truth generated `2026-05-16T14:39:02`. Results stayed clean: 5015 C# files, 1,723,614 lines, 85 domains, 33 sweep commands, 0 required failures, 36 data-truth checks, 0 failures, 41 aligned binaries, 161 struct sites, 0 endian failures.
- Loop 27: Added explicit artifact inputs to `Tools\VerifyMetricPhiDataTruth.py` to survive parallel writers, snapshotted the recovered sweep, regenerated OSHINO-specific H-Phi/atlas/graph, and rebound the final data-truth audit to those immutable inputs. Current OSHINO final: H-Phi `generated_at=2026-05-16T15:25:46`, data truth `generatedAt=2026-05-16T15:26:45`, sweep snapshot `generatedAt=2026-05-16T15:09:32`; all remain PASS.
- Loop 28: Fresh shared sweep exposed a real ordering bug: `BabelCompiler` regenerated `Assets/_Project/Scripts/UI/Localization/H8LocHashes.cs` after the last H-Phi report, so `VerifyMetricPhiDataTruth` correctly failed `h_phi_report_fresh_for_eligible_sources`. Patched `Tools\RunMetricPhiVerifySweep.py` to run `CalculateHPhi` after mutating verifier commands and before the METRIC binder. Current immutable post-mutation snapshot: H-Phi `generated_at=2026-05-16T18:10:58`, sweep `generatedAt=2026-05-16T18:11:09`, data truth `generatedAt=2026-05-16T18:16:14`; all PASS.

## Current Verification

- `Docs\Reports\HECTON_PHI_SCORE_POST_MUTATION_FINAL.json`: PASS snapshot; guarded discovery/scanned count matched `5015`; all_source files=5015; all_source lines=1723719; runtime files=1279; runtime lines=886793; domain_index_count=85; runtime H-Phi static=6.7481e-05; status=PHI CALCULATED; elapsed_seconds=121.88; generated_at=2026-05-16T18:10:58.
- `python -B Tools\BabelCompiler.py`: PASS; records=32604; sources=45; bytes=1525248; constants=12700; word_count=170572; alignment=16; endian=<; collisions_resolved=0.
- `python -B Tools\VerifyBabel.py --hash-audit`: PASS; records=32604; sources=45; bytes=1525248; alignment=16; endian=little; hashCollisions=0.
- `python -B Tools\VerifyBabelDictionary.py`: PASS; deterministic rebuild matches current Babel binary; entries=32604; word_count=170572; constants=12700.
- `python -B Tools\PackPdaTechnicalLogs.py`: PASS; entries=100; bytes=58880; alignment=16; endian=<.
- `python -B Tools\VerifyPdaTechnicalLogs.py`: PASS; entries=100; binaryBytes=58880; alignment=16; endian=<; hashCollisions=0; hPhiDataSovereignty=1.0.
- `python -B Tools\VerifyBinaryHygiene.py --report Docs\Reports\METRIC_PHI_BINARY_HYGIENE_OSHINO_FINAL.json`: PASS; binaryCount=41; misalignedCount=0.
- `Docs\Reports\METRIC_PHI_VERIFY_SWEEP_POST_MUTATION_FINAL.json`: PASS snapshot; totalCommands=35; requiredFailures=0; includes required `CalculateHPhi`, `VerifyMetricPhiDataTruth`, and `VerifyReplayHasherReference`; generatedAt=2026-05-16T18:11:09.
- `Docs\Reports\METRIC_PHI_DATA_TRUTH_AUDIT_POST_MUTATION_FINAL.json`: PASS snapshot; checks=37; failed=0; data binaries=42; unaligned=0; struct format sites=167; endian_failures=0; sweepCommands=35; sweepFailures=0; generatedAt=2026-05-16T18:16:14.
- `python -B Tools\VerifyDataInquisition.py`: PASS; binaries=40 aligned16=true; manifests=8 endian=<; structFormats=151; monteCarloSteps=1000000; hashCollisions=0; atlasDomains=85; status=DATA_INQUISITION_VERIFIED_STATIC_ONLY.
- `python -B Tools\DataTruthInquisition.py`: PASS; economy Monte Carlo node-steps=1541057; FNV collisions=0; domain index count=85; binary alignment failures=0; struct format review items=0.
- `python -B Tools\Economy\MonteCarloEconomySim.py --root . --players 10000 --max-nodes 10000 --threshold-minutes 60`: PASS; total_nodes_mined=1541057; million_step_audit_passed=True; players=10000; failures=0; p99_minutes=59.285135135135164; status=ECONOMY PROVEN.
- `python -B Tools\CraftingEconomyMonteCarlo.py --steps 1000000`: PASS; profit_steps=0; max value/mass/energy deltas all negative.
- `python -B -m py_compile Tools\CalculateHPhi.py Tools\VerifyBabel.py Tools\VerifyBabelDictionary.py Tools\VerifyDataInquisition.py Tools\VerifyMetricPhiDataTruth.py Tools\RunMetricPhiVerifySweep.py Tools\Economy\DataTruthInquisition.py Tools\Economy\MonteCarloEconomySim.py Tools\PackPdaTechnicalLogs.py Tools\VerifyPdaTechnicalLogs.py Tools\CraftingEconomyMonteCarlo.py Tools\VerifySabineBaker.py Tools\MathLUTGenerator.py Tools\VerifyLore.py Tools\LoreTechValidator.py`: PASS.
- Python cache cleanup: PASS; removed verifier-created `Tools/**/__pycache__` directories in the final pass; removed=1; remaining count=0.
- `git diff --check` on METRIC-owned touched files: PASS.
- Active writer scan: remaining Python writer uses SNELL-specific output paths; final METRIC evidence is bound to immutable `*_POST_MUTATION_FINAL` snapshots.
- Final shared rebind after pre-patch SNELL binder exit: PASS; shared H-Phi `generatedAt=2026-05-16T18:10:58`, shared sweep `generatedAt=2026-05-16T18:11:09`, shared data truth `generatedAt=2026-05-16T18:27:20`, `checks=37`, `failed=0`, `binary_files=42`, `struct_format_sites=167`, `hPhiFresh=true`, active Python process count `0`, cache cleanup `remaining=0`.
- Fresh live OSHINO rerun after user escalation: PASS; `python -B Tools\RunMetricPhiVerifySweep.py --xxhash-path C:\Users\User\AppData\Local\Temp\metric_phi_xxhash_ref` returned `VERIFY_SWEEP_PASS`, `35` commands, `0` required failures; shared H-Phi `generatedAt=2026-05-16T18:36:56`, `5015` files, `1,723,719` lines; shared data truth `generatedAt=2026-05-16T18:38:04`, `37` checks, `0` failures, `42` aligned binaries, `167` struct sites, `hPhiFresh=true`; explicit economy MC `1541057` node steps, crafting MC `1000000` steps, `0` profit steps; py_compile PASS; cache cleanup `removed=2 remaining=0`; active Python process count `0`.
- `Docs/AgentLogs/LOG_METRIC_PHI_ANALYST.md`: PASS; latest bottom-of-file entry appended at `2026-05-16T18:38:57+03:00`.

## Residual Risk

- Unity import, Unity Console, Play Mode, Memory Profiler, player build, frame-time, scene wiring, visual review, and GCMonitor evidence remain PENDING VERIFICATION.
- H-Phi remains low: `DataSovereignty=0.019743027`, `StrictLocalNativeArraySovereignty=0.089045936`. This is an architecture debt signal, not a runtime failure proof.
- Data observability increased through repeatable validators. Runtime C# data sovereignty did not increase this pass; systems still need future DataVault/stateless-lookup refactors to move the H-Phi sovereignty ratios.

## 2026-05-16T22:53:00+03:00 Struct-Scope Repair

- [x] Data Python struct surface widened | Justification: `Tools\VerifyMetricPhiDataTruth.py` and `Tools\VerifyDataInquisition.py` now scan `Tools/**/*.py` and `Data/**/*.py`; DOD practice: direct machine audit of every discovered literal `struct` callsite in both roots | Alternatives Rejected: manual whitelist of `Data/Localization/Radio/*.py` or trusting the previous `167`-site count | Estimate: 0 runtime us
- [x] Shared xxhash default hardened | Justification: `Tools\RunMetricPhiVerifySweep.py` now resolves `METRIC_PHI_XXHASH_PATH`, `%TEMP%\metric_phi_xxhash_ref`, or `.codex_tmp\metric_phi_xxhash_ref` when callers omit `--xxhash-path`; DOD practice: deterministic local reference discovery | Alternatives Rejected: requiring tribal CLI flags and allowing no-path sweeps to poison the shared report | Estimate: 0 runtime us
- [x] Sidecar binder written | Justification: `Docs\Reports\METRIC_PHI_DATA_TRUTH_AUDIT_STRUCT_SCOPE_FINAL.json` is bound to explicit H-Phi, atlas, graph, and green sweep inputs while pre-patch shared sweeps are still live | Alternatives Rejected: killing external sweep processes or copying a snapshot over the mutable shared report | Estimate: 0 runtime us

Verification:
- `python -B -m py_compile Tools\RunMetricPhiVerifySweep.py Tools\VerifyMetricPhiDataTruth.py Tools\VerifyDataInquisition.py`: PASS.
- `python -B Tools\Security\VerifyReplayHasherReference.py --xxhash-path C:\Users\User\AppData\Local\Temp\metric_phi_xxhash_ref --fuzz-count 256`: PASS; `xxh3=466`, `shuffle=256`.
- `python -B Tools\VerifyDataInquisition.py`: PASS; report readback `42` binaries, `11` manifest/layout contracts, `161` struct formats, `0` failures.
- `python -B Tools\VerifyBinaryHygiene.py --report Docs\Reports\METRIC_PHI_BINARY_HYGIENE_LIVE.json`: PASS; `42` binaries, `0` misaligned.
- `Docs\Reports\METRIC_PHI_DATA_TRUTH_AUDIT_STRUCT_SCOPE_FINAL.json`: PASS; `37` checks, `0` failures, `42` aligned binaries, `171` struct sites, `0` endian failures. Newly covered `Data/Localization/Radio` struct sites: `4`, all explicit `<` little-endian.

## 2026-05-17T01:29:46+03:00 Full Sidecar Sweep And Ore LCG Repair

- [x] Struct resolver promoted from literal scan to constant/import scan | Justification: `Tools\VerifyMetricPhiDataTruth.py` and `Tools\VerifyDataInquisition.py` now resolve module constants, imported tool constants, string multiplication/addition, numeric arithmetic, `len(...)`, `struct.calcsize(...)`, format-table subscripts, guarded dynamic `<` formats, external container byte-order, and negative big-endian sentinel tests | Alternatives Rejected: leaving `struct.pack(FORMAT_CONST, ...)` as unverified or weakening unresolved formats to warnings | Estimate: 0 runtime us
- [x] Ore LCG stale binary cache repaired | Justification: full sidecar sweep failed `VerifyOreLcgBaker` and `VerifyOreLcgBinaryIndependent` on minimal/toaster LOD byte counts; reran `python -B Tools\OreLcgBaker.py --root . --iterations 1000000`, then both verifiers passed | Alternatives Rejected: editing JSON/binary manually or suppressing the independent verifier | Estimate: 0 runtime us; generated data only
- [x] Full sidecar sweep rerun | Justification: `python -B Tools\RunMetricPhiVerifySweep.py --xxhash-path C:\Users\User\AppData\Local\Temp\metric_phi_xxhash_ref --json-output Docs\Reports\METRIC_PHI_VERIFY_SWEEP_STRUCT_SCOPE_FINAL.json --markdown-output Docs\Reports\METRIC_PHI_VERIFY_SWEEP_STRUCT_SCOPE_FINAL.md` returned `VERIFY_SWEEP_PASS`, `35` commands, `0` required failures | Alternatives Rejected: relying on mutable shared sweep paths while external agents are active | Estimate: 0 runtime us; offline tooling only

Verification:
- `Docs\Reports\METRIC_PHI_VERIFY_SWEEP_STRUCT_SCOPE_FINAL.json`: PASS; generatedAt=`2026-05-17T01:24:25`; commands=`35`; requiredFailures=`0`; selfCheckPending=`False`.
- `Docs\Reports\METRIC_PHI_VERIFY_SWEEP_STRUCT_SCOPE_FINAL_H_PHI_SCORE.json`: PASS; generatedAt=`2026-05-17T01:23:22`; files=`5015`; lines=`1,723,788`; runtime H-Phi=`6.7481e-05`; DataSovereignty=`0.019743027`.
- `Docs\Reports\METRIC_PHI_DATA_TRUTH_AUDIT_STRUCT_SCOPE_FINAL.json`: PASS; generatedAt=`2026-05-17T01:29:46`; checks=`37`; failures=`0`; binary_files=`43`; unaligned=`0`; struct_format_sites=`274`; endian_failures=`0`; sweep selfCheckPending=`False`.
- `python -B Tools\VerifyOreLcgBaker.py`: PASS; binaryBytes=`1776`; hashCollisions=`0`.
- `python -B Tools\VerifyOreLcgBinaryIndependent.py`: PASS; binaryBytes=`1776`; resourceRecordsChecked=`150`.
- `python -B Tools\VerifyBinaryHygiene.py --report Docs\Reports\METRIC_PHI_BINARY_HYGIENE_LIVE.json`: PASS; binaryCount=`43`; misalignedCount=`0`.
- `python -B -m py_compile Tools\RunMetricPhiVerifySweep.py Tools\VerifyMetricPhiDataTruth.py Tools\VerifyDataInquisition.py Tools\OreLcgBaker.py Tools\VerifyOreLcgBaker.py Tools\VerifyOreLcgBinaryIndependent.py`: PASS.
- `git diff --check` on METRIC/Ore evidence files: PASS.
