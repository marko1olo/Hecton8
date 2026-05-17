# METRIC_PHI_ANALYST Rationale

Status: `PHI CALCULATED / VERIFIED MASTER GRADE STATIC_SOURCE + CLI_PYTHON_DATA / RUNTIME PENDING`

## Initialization

Problem: Required state files were absent.
Solution: Create explicit status and rationale files before implementation.
Rejected Alternatives: Chat-only tracking; violates disk-backed anti-amnesia protocol.
Scalability potential: Low/Middle/High/Ultra unchanged; this is tooling infrastructure.
Hardware Impact: Runtime impact on i3/MX350 is zero because this task is Python-only offline analysis.

## Decision 1 - Static H-Phi Implementation

Problem: The prompt requested final H-Phi calculation, but H-Phi authority defines the metric as static evidence only.
Solution: Implemented `Tools/CalculateHPhi.py` as a repeatable Python scanner using regex counters and explicit `STATIC_SOURCE/STATIC_DOC/PY_TOOL` evidence labels.
Rejected Alternatives: Directly editing the JSON by hand; running Unity; claiming profiler/GC proof from text.
Scalability potential: Low uses static counters to expose shed/fake pressure; Middle keeps typed lanes measurable; High and Ultra can add visual consumers without hiding coupling debt.
Hardware Impact: 0 runtime us on i3/MX350. Offline run took `471.803s`; no player-frame cost exists.

## Decision 2 - File Scanning Strategy

Problem: The scan target is over 1.7M C# lines on current disk and can fail if loaded monolithically.
Solution: Discovered files under `Assets`, `Packages`, and `Tools`, then scanned one file per worker with multiprocessing and progress output.
Rejected Alternatives: Single full-repository text blob; PowerShell-only `Select-String` counters with no reusable JSON schema.
Scalability potential: Low hardware can run with `--workers 1`; stronger machines can raise workers for faster offline audits.
Hardware Impact: 0 runtime us on i3/MX350; offline CPU/disk only.

## Decision 3 - Atlas Marker Replacement

Problem: `Docs/PROJECT_ATLAS.md` is existing authority-adjacent documentation and blind overwrite would destroy unrelated atlas context.
Solution: Inserted/replaced only the marker-bounded H-Phi domain-index section.
Rejected Alternatives: Full atlas rewrite; appending duplicate sections on every run.
Scalability potential: Keeps docs stable under parallel agents and permits repeated recalculation without bloat.
Hardware Impact: 0 runtime us on i3/MX350.

## Decision 4 - Graph Scope

Problem: The prompt requested a visual graph of the entire project architecture, but full source-level call graph would be fake precision without Roslyn.
Solution: Generated a first-party asmdef dependency graph using networkx/matplotlib and stored node/edge counts in JSON.
Rejected Alternatives: Semantic call graph from regex; static image without source metadata.
Scalability potential: Low machines use the graph for dependency triage; High/Ultra decisions remain gated by real compile/runtime artifacts.
Hardware Impact: 0 runtime us on i3/MX350; offline PNG render only.

## Decision 5 - Babel Verifier API Repair

Problem: `Tools/VerifyBabel.py` referenced `BabelCompiler.OUTPUT_RELATIVE`, `BabelCompiler.MANIFEST_RELATIVE`, and `BabelCompiler.verify_against_manifest`, none of which exist in the current compiler. The verifier failed before reaching binary hygiene checks.
Solution: Replaced stale calls with direct verifier defaults and explicit header/manifest/blob checks: magic, version, required flags, `<` endianness, 16-byte table/payload alignment, payload bounds, FNV-1a 64 corpus hash, and duplicate language/key hash-pair rejection.
Rejected Alternatives: Running `BabelCompiler.py` and trusting compile success. That does not prove the shipped blob still matches its manifest after manual edits or stale merges.
Scalability potential: Low/Toaster uses core dictionary layers without runtime JSON; High/Ultra keeps all layers resident with font metadata and glitch styling, still via stateless hash lookup.
Hardware Impact: 0 runtime us on i3/MX350. Current offline verifier confirms `32604` records, `1525248` bytes, 16-byte alignment, little-endian manifest, and zero hash collisions.

## Decision 6 - OSHINO Data Inquisition Harness

Problem: The data truth checks were scattered across individual verifiers. That leaves room for false handoff claims: binary alignment can pass while atlas fit, Monte Carlo proof, lore tone, or scaler tiers drift.
Solution: Added `Tools/VerifyDataInquisition.py` as a repeatable OSHINO-level static verifier. It checks all `.bin/.h8bin` sizes under `Data` and `Assets/_Project/Data`, manifest endianness/alignment declarations, physics-source token presence, crafting Monte Carlo proof, H8 hash collision report, 85-domain atlas/H-Phi fit, lore-tone evidence, and toaster/RTX scalability payload presence.
Rejected Alternatives: One-off shell snippets. They are useful during investigation but not enough for SHINOBU ingestion or CI reuse.
Scalability potential: Low/Toaster data is explicitly checked through low/minimal/i3 tier payloads; RTX/God/Ultra payloads are also required so high-end visual consumers can overdeliver without mutating simulation truth.
Hardware Impact: 0 runtime us on i3/MX350. Current offline verifier output: `binaries=38 aligned16=true manifests=8 endian=< structFormats=146 monteCarloSteps=1000000 hashCollisions=0 atlasDomains=85`.

## Decision 7 - H-Phi Bottleneck Boundary

Problem: The latest H-Phi scan produces a valid static score, but `DataSovereignty=0.019743027` and `StrictLocalNativeArraySovereignty=0.089045936` remain low. Claiming architectural improvement from this pass would be false because no runtime owner was refactored into DataVault-backed stateless lookup.
Solution: Record the bottleneck as static architecture debt and keep runtime proof as pending. This pass improves data verification sovereignty, not the underlying C# ownership ratio.
Rejected Alternatives: Inflating status to "runtime solved" or editing unrelated runtime systems to chase a metric outside the prompt.
Scalability potential: Low/Middle/High/Ultra all benefit from repeatable data checks, but true H-Phi uplift requires future domain-owner work to replace private native state with GlobalDataVault-backed stateless lookups.
Hardware Impact: 0 runtime us on i3/MX350. No gameplay code changed.

## Decision 8 - Struct Format And Physics Manifest Hardening

Problem: The first OSHINO inquisition proved binary sizes and some manifest fields, but it did not inspect Python `struct` callsites or prove that generated manifests preserved hard-science labels after regeneration. `Tools/SabineBaker.py` emitted `P = P0 + rho*g*h` under a generic `pressure` key, which weakened hydrostatic evidence.
Solution: Extended `Tools/VerifyDataInquisition.py` with AST-based `struct` format auditing, external PNG/JPEG/PSD big-endian exemptions, physics-manifest checks for Sabine/Thorp/Beer-Lambert/Dalton/Snell, and stateless lookup contract checks. Updated `Tools/SabineBaker.py` to emit both `hydrostaticPressure` and the legacy `pressure` alias, then regenerated acoustic data.
Rejected Alternatives: Treating source-token hits as enough. A manifest can drift even when the source contains correct math, and a broad regex can falsely classify external image container big-endian fields as H8 blob byte-order failures.
Scalability potential: Low/Toaster tiers are explicitly checked; RTX/God/Ultra data remains required. The verifier now protects both stripped-down and overkill payloads from stale contract drift.
Hardware Impact: 0 runtime us on i3/MX350. Offline verification now reports `structFormats=146` in `VerifyDataInquisition.py`, `struct_format_sites=160` in `VerifyMetricPhiDataTruth.py`, and zero H8 endian failures.

## Decision 9 - Metric Data Truth Audit

Problem: The previous data-inquisition artifact was broad but did not specifically bind the current H-Phi report to binary/endian hygiene, the 85-domain atlas section, physics LUT basis, current Monte Carlo output, hash collisions, lore tone metadata, and scalability payloads in one METRIC-owned verifier.
Solution: Added `Tools/VerifyMetricPhiDataTruth.py`, a Python-only verifier that writes `Docs/Reports/METRIC_PHI_DATA_TRUTH_AUDIT.json` and `.md`. It consumes generated artifacts instead of trusting chat output.
Rejected Alternatives: Extending `CalculateHPhi.py` into a cross-domain verifier. H-Phi should stay an architecture scanner; data-truth validation is a separate gate.
Scalability potential: Low/Toaster gets a small audit path and explicit low-tier payload checks. High/Ultra gets graph/domain edge payloads plus visual-overkill data checks without increasing runtime simulation truth.
Hardware Impact: 0 runtime us on i3/MX350. Current run: `checks=36 failed=0`, `binary_files=39 unaligned=0`, `struct_format_sites=160 endian_failures=0`, `verify_sweep_pass=true`.

## Decision 10 - Binary And Endian Hygiene Scope

Problem: A naive struct-format scan flags legitimate PNG/JPEG/PSD big-endian container fields as H8 binary failures, while the actual H8 runtime blobs require little-endian and 16-byte alignment.
Solution: The verifier scans repo-owned H8 binary roots `Data`, `Assets/_Project/Data`, and `Docs/AgentLogs` for `.bin/.h8bin` 16-byte alignment, and scans Python `struct.*` literal formats while classifying external image-container big-endian fields as non-H8 exceptions.
Rejected Alternatives: Blanket "all > is fatal" scanning. That would block valid PNG/JPEG/PSD tooling and create noise instead of finding H8 binary debt.
Scalability potential: Low-tier ingestion can memcpy aligned H8 blobs safely; Ultra-tier tooling can still emit PNG previews and texture diagnostics without being mislabeled as byte-swap risk.
Hardware Impact: 0 runtime us. Current evidence: 39 repo-owned binary blobs aligned to 16 bytes; 160 struct format sites scanned; external-container big-endian sites allowed only for PNG/JPEG/PSD tooling; 0 H8 endian failures.

## Decision 11 - Math, Economy, Lore Evidence Boundary

Problem: The user requested hard-science proof, 1,000,000-step economy proof, and NASA-Punk Noir tone proof. These are cross-domain data claims; claiming them from H-Phi counters alone would be false.
Solution: Reran the owner Python verifiers and consumed their artifacts in `VerifyMetricPhiDataTruth.py`: Beer-Lambert optics via `OpticsBaker`, Dalton gas via `DaltonGasToxicityBaker`/`MathLUTGenerator`, Sabine acoustics via `VerifySabineBaker`, economy via `MonteCarloEconomySim.py`, lore via `VerifyLore.py --check` and `LoreTechValidator.py`.
Rejected Alternatives: Manual inspection of JSON values only; that misses stale binary payloads and hash drift.
Scalability potential: Data manifests include toaster/i3 and RTX/God-mode payloads: optical toaster/overkill matrices, acoustic quality tiers, Dalton quality tiers, and lore extra-data fields.
Hardware Impact: 0 runtime us for this pass. Current economy artifact: `1541057` Monte Carlo node steps, `0` failures, `p99=59.285135135135164` minutes. Runtime Unity proof remains pending.

## Decision 12 - Stale Water Optics Payload Repair

Problem: `python Tools\OpticsBaker.py --verify` found `Water_Extinction_Matrix.bin` at the old 33,554,432-byte spectral layout while the current batch-owned OpticsBaker contract requires 393,216 bytes.
Solution: Regenerated with `python Tools\OpticsBaker.py`, restoring the current Beer-Lambert RGB payload plus toaster and RTX-overkill variants. The follow-up verify passed.
Rejected Alternatives: Padding or accepting the stale spectral payload. That would violate the current batch XML byte contract and waste raw data budget.
Scalability potential: Low/MX350 uses the compact main/toaster lookup. High/Ultra keeps richer overkill matrix and harmonic-noise metadata for visual presentation.
Hardware Impact: Runtime code unchanged. Raw primary LUT payload is back to 393,216 bytes; static estimate from the optics owner remains 5-30 us saved per underwater full-screen pass by replacing shader exponentials with lookup sampling, pending Unity profiler proof.

## Decision 13 - Full-Source H-Phi Finalization

Problem: The scanner default targeted first-party `Assets/_Project` plus `Tools`, but the XML directive requires the full `.cs` corpus. A default run was under-scoped.
Solution: Restored `Tools/CalculateHPhi.py` default roots to `Assets`, `Packages`, and `Tools`. Current readback report generated at `2026-05-16T10:22:02`, scanned `5015` C# files and `1,723,604` lines, kept runtime metrics isolated to `Assets/_Project/Scripts`, and preserved `DOMAIN_INDEX_COUNT=85`.
Rejected Alternatives: Accepting the first-party-only default-root run; it scanned only `1534` files for all-source and did not satisfy the XML wording. Keeping the full-source pass as a one-off CLI override was also rejected because future default runs would silently regress.
Scalability potential: Low/Toaster dashboards can still consume the runtime subsection. High/Ultra architecture dashboards get full-source pressure and graph data without changing runtime code.
Hardware Impact: 0 runtime us on i3/MX350. Current readback offline scan cost was `523.172s`; no player-frame path changed.

## Decision 14 - Babel Drift Correction After Hash Catalog Regeneration

Problem: `VerifyBabelDictionary.py` failed after the FNV hash catalog was regenerated. The Babel source manifest includes `Docs/Reports/H8_Hash_Catalog_Audit.json`, so the shipped dictionary was stale against current source material.
Solution: Rebuilt with `python Tools\BabelCompiler.py`, then reran `VerifyBabel.py --hash-audit` and `VerifyBabelDictionary.py`. Current Babel evidence: `32604` entries, `45` sources, `1525248` bytes, 16-byte aligned, little-endian, `0` collision resolutions.
Rejected Alternatives: Weakening `VerifyBabelDictionary.py` to stop doing deterministic rebuild checks. That would hide stale generated data instead of fixing it.
Scalability potential: Low tier keeps core dictionary layers in a prepacked stateless blob; High/Ultra can keep all language/font metadata resident without runtime JSON parsing.
Hardware Impact: Runtime code changed: none. Generated data changed only; runtime lookup remains hash/offset based and allocation-free by design, pending Unity-side proof.

## Decision 15 - Data Truth Boundary Pass

Problem: The user requested hard proof for economy loops, FNV collisions, binary alignment, endianness, atlas fit, and H-Phi data sovereignty. H-Phi alone cannot prove those.
Solution: Cross-checks ran `Tools\Economy\DataTruthInquisition.py`, `Tools\VerifyMetricPhiDataTruth.py`, `Tools\VerifyDataInquisition.py`, `Tools\RunMetricPhiVerifySweep.py`, and `py_compile` for the H-Phi/data-truth scripts. Current Metric data-truth results are `DATA_TRUTH_VERIFIED`, `monte_carlo_steps=1541057`, `fnv_collisions=0`, `checks=36`, `failed=0`, `struct_format_sites=160`, `endian_failures=0`, `verify_sweep_required_failures=0`.
Rejected Alternatives: Treating the prior report as still current after regenerating Babel and H-Phi artifacts.
Scalability potential: Toaster data remains covered through stripped binary tiers; RTX/God-mode data remains covered through extra visual/audio payload fields. Runtime Data Sovereignty did not increase in C# ownership terms; observability increased.
Hardware Impact: 0 runtime us. The pass is offline CLI/static evidence only; Unity import, Play Mode, profiler, GCMonitor, player build, and frame-time remain pending verification.

## Decision 16 - External Replay Hash Reference Proof

Problem: `Tools\Security\VerifyReplayHasherReference.py` requires the third-party `xxhash` package via `--xxhash-path`; skipping it would leave the replay hash reference proof as hearsay.
Solution: Installed `xxhash==3.7.0` into `%TEMP%\metric_phi_xxhash_ref` only, then ran `python Tools\Security\VerifyReplayHasherReference.py --xxhash-path %TEMP%\metric_phi_xxhash_ref --fuzz-count 256` through the sweep runner.
Rejected Alternatives: Adding `xxhash` to the project environment or treating the internal guard verifier as equivalent to an external reference check.
Scalability potential: Low/Middle/High/Ultra replay validation can trust the same deterministic little-endian XXH3 reference without adding runtime dependency weight.
Hardware Impact: 0 runtime us on i3/MX350. Latest offline proof passed `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=466 shuffle=256`.

## Decision 17 - Verify Sweep Evidence

Problem: The escalation explicitly required every Python `Verify*.py` script to run again. The evidence needed to be one artifact, not a scattered terminal transcript.
Solution: Added `Tools/RunMetricPhiVerifySweep.py`, installed optional `xxhash` into `%TEMP%\metric_phi_xxhash_ref`, and initially ran 26 verifier commands into `Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json` and `.md`. The current sweep surface is 28 commands as recorded in Decision 20.
Rejected Alternatives: Marking `VerifyReplayHasherReference.py` skipped because `xxhash` was not installed. The verifier explicitly supports a temporary reference path, so a real reference comparison was possible without adding a repo package.
Scalability potential: Low-tier ingestion gets one repeatable binary/data gate. High/Ultra payloads stay covered because optics, Sabine, Dalton, VFX, VR comfort, tide, visual LOD, taxonomy, and network verifiers all run in the same sweep.
Hardware Impact: 0 runtime us on i3/MX350. Current sweep result: `VERIFY_SWEEP_PASS`, `totalCommands=28`, `requiredFailures=0`.

## Decision 18 - Final Sweep Binding

Problem: The status and CTO handoff needed to reflect the current `VerifyMetricPhiDataTruth.py` contract after it began consuming `Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json`.
Solution: Reran `Tools\RunMetricPhiVerifySweep.py`, reran `Tools\VerifyMetricPhiDataTruth.py`, and appended a final CTO log entry with the fresh 36-check result.
Rejected Alternatives: Leaving the older `checks=33` transcript as the bottom-of-file evidence. That would make the latest handoff weaker than the current artifacts on disk.
Scalability potential: Low/Toaster and High/Ultra payload proofs stay bound to one data-truth gate; no runtime system has to hold private audit state.
Hardware Impact: 0 runtime us on i3/MX350. Evidence at that pass: `VERIFY_SWEEP_PASS`, 26 commands, 0 required failures; `DATA_TRUTH_VERIFIED`, 36 checks, 0 failures; py_compile clean for METRIC Python entrypoints. Current evidence is superseded by Decision 20.

## Decision 19 - Fresh Escalation Rerun And Stale Generated Data Repair

Problem: The fresh escalation rerun exposed real generated-data drift. `VerifyBabelDictionary` detected a constants mismatch after source inputs changed, and `VerifyPdaTechnicalLogs` detected a stale H8PT extra-visual record layout. Leaving either failure in place would make the SHINOBU ingest claim false.
Solution: Rebuilt the generated artifacts from their current source authorities. `python -B Tools\BabelCompiler.py` currently verifies `32604` records, `1525248` bytes, `12700` localization constants, and zero collision resolutions. `python -B Tools\PackPdaTechnicalLogs.py` rebuilt the PDA H8PT blob to `58880` bytes with 16-byte alignment and little-endian records. Then H-Phi was rerun from the full default roots, the verifier sweep passed, standalone Metric data truth passed, Data Inquisition passed, explicit economy Monte Carlo passed, crafting Monte Carlo passed, and py_compile passed for the METRIC-owned tooling.
Rejected Alternatives: Ignoring the sweep failure as "just generated data"; weakening the Babel or PDA verifiers; accepting stale sweep artifacts after the runner expanded its command surface.
Scalability potential: Low/Toaster keeps stripped aligned binary payloads for Babel/PDA/data lookup. High/Ultra keeps extra text/visual metadata in stateless payloads without adding simulation authority or per-system private state. The H-Phi runtime sovereignty ratio still did not improve; this pass improved data correctness and observability only.
Hardware Impact: 0 runtime us on i3/MX350 for this pass. Generated data changed, but no Unity runtime system was edited. Latest CLI evidence: H-Phi runtime static `6.7481e-05`, runtime `DataSovereignty=0.019743027`, strict local-native-array sovereignty `0.089045936`, 39 Metric data binaries aligned, 160 Python struct format sites scanned, 0 endian failures, 28 sweep commands, 0 required failures.

## Decision 20 - Bytecode-Safe Sweep And Full Binary Root Coverage

Problem: A long sweep run exposed two weaknesses: stale Python bytecode could make the sweep execute outdated verifier code, and `VerifyMetricPhiDataTruth.py` only counted `Data` binaries while Babel and crash-dump blobs live under other project-owned roots.
Solution: Updated `Tools\RunMetricPhiVerifySweep.py` so subprocesses run with `PYTHONDONTWRITEBYTECODE=1` and a missing replay `--xxhash-path` is a required failure. Updated `Tools\VerifyMetricPhiDataTruth.py` to scan `Data`, `Assets/_Project/Data`, and `Docs/AgentLogs` for `.bin/.h8bin` alignment.
Rejected Alternatives: Treating stale `.pyc` behavior as harmless; leaving Babel and dump blobs to the broader inquisition only; allowing the replay hash reference to pass as optional.
Scalability potential: SHINOBU ingest now sees one stricter METRIC gate for all current repo-owned binary blobs, while toaster and RTX-overkill payloads remain stateless data lookups.
Hardware Impact: 0 runtime us on i3/MX350. Latest evidence: H-Phi generated `2026-05-16T10:22:02`; `VERIFY_SWEEP_PASS`, `totalCommands=28`, `requiredFailures=0`; `DATA_TRUTH_VERIFIED`, `checks=36`, `binary_files=39`, `unaligned=0`, `struct_format_sites=160`, `endian_failures=0`.

## Decision 21 - Canonical Hash To Babel Ordering

Problem: The fresh OSHINO sweep proved that Babel verification is order-sensitive because `Docs/Reports/H8_Hash_Catalog_Audit.json` is one of Babel's source files. If the canonical FNV catalog changes after Babel verification, the dictionary becomes stale even when the binary itself is aligned and little-endian.
Solution: Patched `Tools\RunMetricPhiVerifySweep.py` to run canonical `VerifyH8HashCollisions.py` first, then `BabelCompiler.py`, then `VerifyBabel.py --hash-audit` and `VerifyBabelDictionary.py`. `VerifyMetricPhiDataTruth.py` was removed from the sweep body and kept as the post-sweep binder because it validates the completed sweep artifact.
Rejected Alternatives: Keeping manual Babel rebuild as a tribal step; weakening deterministic Babel verification; embedding the data-truth binder recursively inside the sweep.
Scalability potential: Low tier gets deterministic stateless localization/hash payloads without runtime JSON. High/Ultra keeps richer text metadata and source-hash visual payloads without making systems hold private mutable lookup state.
Hardware Impact: 0 runtime us on i3/MX350. Latest evidence: canonical FNV records `1018`, collisions `0`; Babel records `32604`, bytes `1525248`, constants `12700`; no-recursion sweep `28` commands, `0` required failures; post-sweep data truth `36` checks, `0` failures.

## Decision 22 - Fresh No-Drift OSHINO Rerun

Problem: The latest escalation required the current disk state to be revalidated, not trusted from the previous pass.
Solution: Reran full-source H-Phi, the 28-command sweep, the post-sweep Metric data-truth binder, explicit crafting Monte Carlo, explicit economy Monte Carlo, Sabine, Math LUT, and lore validators.
Rejected Alternatives: Treating the previous `VERIFY_SWEEP_PASS` artifact as current enough. Generated reports are mutable, so the audit must consume fresh timestamps and current source hashes.
Scalability potential: Toaster paths remain covered by stripped aligned binaries and low-tier payload metadata; RTX-overkill paths remain covered by harmonic noise, high-resolution LUT, and extra visual/audio payload checks. No runtime private state was introduced.
Hardware Impact: 0 runtime us on i3/MX350. Latest evidence: H-Phi generated `2026-05-16T11:16:06`, 5015 C# files, 1,723,604 lines, 85 domains; sweep generated `2026-05-16T11:31:35`, 28 commands, 0 required failures; data truth generated `2026-05-16T11:29:33`, 36 checks, 0 failures, 39 aligned binaries, 160 struct sites, 0 endian failures.

## Decision 23 - Final Artifact Timestamp Readback

Problem: Final machine readback showed the report artifacts on disk had newer timestamps than the just-written log entry, while the pass/fail values were unchanged.
Solution: Updated status and CTO log to bind to the actual current artifact timestamps instead of stale terminal-memory values.
Rejected Alternatives: Leaving the older generatedAt values because the numeric checks were unchanged. That would violate disk-as-source-of-truth.
Scalability potential: None changed; this is evidence hygiene. Toaster and RTX-overkill payload checks remain covered by the same verified data-truth and sweep artifacts.
Hardware Impact: 0 runtime us on i3/MX350. Current disk evidence after the final guarded rerun: H-Phi `2026-05-16T11:54:51`, data truth `2026-05-16T11:55:18`, sweep `2026-05-16T11:49:55`; all pass with 85 domains, 28 sweep commands, 36 data-truth checks, 39 aligned binaries, 0 endian failures.

## Decision 24 - H-Phi Report Count Guard And Atomic Writes

Problem: A shared report artifact showed `4058` scanned files while `rg` and `CalculateHPhi.discover_cs_files` both showed `5015` eligible C# files. The CLI progress also showed `5015`, so the risk was not source discovery; it was a silent incomplete/clobbered report boundary under parallel-agent execution.
Solution: Patched `Tools\CalculateHPhi.py` to fail with `HPHI_SCAN_INCOMPLETE` when discovered and scanned file counts diverge, and to atomically replace `Docs\Reports\HECTON_PHI_SCORE_FINAL.json` via a process-specific temp file. Patched `Tools\RunMetricPhiVerifySweep.py` earlier in the same OSHINO pass to atomically replace sweep reports and record one required-failure retry without hiding the initial failure.
Rejected Alternatives: Trusting the lower `4058` artifact because runtime-source counters were unchanged; manually editing the JSON; ignoring shared artifact races as another agent's problem.
Scalability potential: Low/Toaster dashboards no longer ingest a quietly under-scoped H-Phi JSON. High/Ultra architecture dashboards still get the full dependency graph and source counters, but with a hard stop if scan coverage falls apart.
Hardware Impact: 0 runtime us on i3/MX350. Current guarded H-Phi rerun passed: `generated_at=2026-05-16T11:54:51`, `all_source.counters.files=5015`, `all_source.counters.lines=1723604`, `domain_index_count=85`, `runtime_source.scores.HPhiStatic=6.7481e-05`.

## Decision 25 - Final Full-Root Rebind After Shared Artifact Drift

Problem: A prior under-scoped H-Phi artifact had overwritten the report with `4058` files despite the eligible full-source corpus being `5015`. The only acceptable end state is the explicit full-root report bound to the current data-truth audit.
Solution: Reran `python -B Tools\CalculateHPhi.py --workers 4 --source-roots Assets Packages Tools`, confirmed the report contains `5015` files and `1,723,604` lines, checked no active H-Phi or sweep Python writer remained, and reran `Tools\VerifyMetricPhiDataTruth.py` against the fresh H-Phi report.
Rejected Alternatives: Accepting the stale full-status text; trusting runtime-source counters while all-source coverage was wrong; finalizing while another Python sweep could overwrite artifacts.
Scalability potential: Low/Toaster and High/Ultra dashboards now consume the same full-source H-Phi report, while data payload checks keep stripped and God-Mode extra fields covered by stateless lookups.
Hardware Impact: 0 runtime us on i3/MX350. Current disk evidence: H-Phi `2026-05-16T11:54:51`; data truth `2026-05-16T11:55:18`; sweep `2026-05-16T11:49:55`; `checks=36`, `failed=0`, `binary_files=39`, `struct_format_sites=160`, `endian_failures=0`.

## Decision 26 - Fresh OSHINO Rerun Under Concurrent Writers

Problem: The latest escalation required another full disk-backed proof pass while other agents were actively writing shared verifier reports. Running blind parallel official sweeps would corrupt the evidence boundary and create false failures.
Solution: Waited for active sweep artifacts to settle, then consumed the current green sweep artifact and reran the non-recursive binders: full-root H-Phi, explicit 10,000-player economy Monte Carlo, 1,000,000-step crafting Monte Carlo, post-sweep Metric data truth, Data Inquisition, and py_compile. The current disk state is H-Phi `2026-05-16T14:08:37`, sweep `2026-05-16T14:28:42`, and Metric data truth `2026-05-16T14:39:02`.
Rejected Alternatives: Starting another official sweep while `RunMetricPhiVerifySweep.py` was already active; claiming the previous 11:55 artifacts as current; ignoring that other agents increased sweep coverage and binary coverage.
Scalability potential: Low/Toaster coverage expanded with the current binary inventory (`41` repo-owned blobs aligned16). High/Ultra remains covered through extra-data and overkill payload checks in the 33-command sweep.
Hardware Impact: 0 runtime us on i3/MX350. Current CLI evidence: H-Phi `5015` files, `1,723,614` lines, `85` domains, runtime H-Phi `6.7481e-05`; sweep `33` commands, `0` required failures; Metric data truth `36` checks, `0` failures, `41` binaries, `0` unaligned, `161` struct format sites, `0` endian failures; economy Monte Carlo `1541057` node steps, `0` failures.

## Decision 27 - Immutable OSHINO Final Evidence Inputs

Problem: A binder run caught a transient sweep state with `requiredFailures=2` while a parallel writer was still retrying. The shared report path recovered to PASS later, but binding to mutable filenames remains unsafe under 20+ agents.
Solution: Added explicit input path arguments to `Tools\VerifyMetricPhiDataTruth.py`, snapshotted the recovered sweep, regenerated OSHINO-specific H-Phi/atlas/graph artifacts, and ran the final binder against those immutable paths.
Rejected Alternatives: Waiting indefinitely for every other agent; weakening `verify_sweep_pass`; claiming the current shared `METRIC_PHI_VERIFY_SWEEP.json` was stable while sweep parents were still active.
Scalability potential: Low/Toaster and High/Ultra evidence consumers can now bind a fixed artifact set without hidden private state or last-writer-wins report ambiguity.
Hardware Impact: 0 runtime us on i3/MX350. Current OSHINO evidence: H-Phi `2026-05-16T15:25:46`, `5015` files, `1,723,661` lines, `85` domains; sweep snapshot `2026-05-16T15:09:32`, `34` commands, `0` required failures; data truth `2026-05-16T15:26:45`, `36` checks, `0` failures, `42` aligned binaries, `162` struct sites, `0` endian failures.

## Decision 28 - Post-Mutation H-Phi In Sweep

Problem: The shared sweep correctly failed when `BabelCompiler` regenerated `Assets/_Project/Scripts/UI/Localization/H8LocHashes.cs` after the latest H-Phi scan. `VerifyMetricPhiDataTruth` flagged `h_phi_report_fresh_for_eligible_sources` because the report timestamp was older than an eligible `.cs` input.
Solution: Patched `Tools\RunMetricPhiVerifySweep.py` so `CalculateHPhi` runs after mutating verifier commands and before `VerifyMetricPhiDataTruth`. The binder now validates an H-Phi report generated after Babel/hash source mutation, not before it.
Rejected Alternatives: Weakening the freshness check; treating generated C# as outside the source corpus; relying only on the immutable OSHINO snapshot while the shared sweep remained able to fail.
Scalability potential: Low/Toaster and High/Ultra dashboards now consume a fresh full-source H-Phi report from the same sweep that proves binary/data hygiene. No runtime system holds private audit state.
Hardware Impact: 0 runtime us on i3/MX350. Current immutable post-mutation evidence: H-Phi `2026-05-16T18:10:58`, `5015` files, `1,723,719` lines, `85` domains; sweep `2026-05-16T18:11:09`, `35` commands, `0` required failures; data truth `2026-05-16T18:16:14`, `37` checks, `0` failures, `42` aligned binaries, `167` struct sites, `0` endian failures; economy Monte Carlo `1541057` node steps.

## Decision 29 - Immutable Post-Mutation Snapshot Under Live Writers

Problem: External agents continued launching `RunMetricPhiVerifySweep.py`, `CalculateHPhi.py`, and hash/economy verifiers after the shared report paths had returned to green. A first `POST_MUTATION_FINAL` copy captured an in-progress `selfCheckPending=True` sweep and was invalid.
Solution: Rejected that failed snapshot, waited until the shared artifacts reached a completed PASS state, then overwrote the `POST_MUTATION_FINAL` artifacts from that green set: H-Phi, architecture graph, atlas, sweep, and data-truth reports.
Rejected Alternatives: Leaving failed `*_POST_MUTATION_FINAL` artifacts on disk; claiming shared filenames are final while another sweep process remains active; killing other agents' Python processes.
Scalability potential: Low/Toaster and High/Ultra consumers now have a stable pass snapshot independent of last-writer-wins shared reports.
Hardware Impact: 0 runtime us on i3/MX350. Snapshot evidence: H-Phi `2026-05-16T18:10:58`, `5015` files, `1,723,719` lines; sweep `2026-05-16T18:11:09`, `35` commands, `0` required failures; data truth `2026-05-16T18:16:14`, `37` checks, `0` failures.

## Decision 30 - H-Phi Freshness Guard In Data Truth

Problem: The METRIC binder could previously pass while `HECTON_PHI_SCORE_FINAL.json` was older than generated eligible C# source. Under parallel sweeps, `BabelCompiler` regenerated `Assets/_Project/Scripts/UI/Localization/H8LocHashes.cs` after H-Phi, leaving the architecture metric stale while binary/endian checks stayed green.
Solution: Patched `Tools\VerifyMetricPhiDataTruth.py` to import `CalculateHPhi.discover_cs_files`, compare eligible file count against the H-Phi report count, and fail `h_phi_report_fresh_for_eligible_sources` when the newest eligible `.cs` timestamp is newer than `generated_at`. The H-Phi payload now records generated time, counters, and freshness detail.
Rejected Alternatives: Treating generated C# as outside the source corpus; weakening the freshness failure to a warning; relying on chat/status timestamps instead of a machine gate.
Scalability potential: Low/Toaster and High/Ultra dashboards now reject stale architecture metrics before SHINOBU ingestion. The check is offline and stateless; runtime systems still read data by hash/offset rather than holding private audit state.
Hardware Impact: 0 runtime us on i3/MX350. Current patched evidence: `VerifyMetricPhiDataTruth` PASS at `2026-05-16T18:16:14`, `37` checks, `0` failures, H-Phi freshness `true`, newest eligible file `Assets/_Project/Scripts/UI/Localization/H8LocHashes.cs` at `2026-05-16T17:57:16.540789`, H-Phi report `2026-05-16T18:10:58`.

## Decision 31 - Custom Sweep Sidecar Isolation

Problem: Domain-owned custom sweeps such as `SNELL_METRIC_PHI_VERIFY_SWEEP.json` still wrote default METRIC H-Phi and data-truth outputs. One pre-patch SNELL binder overwrote `METRIC_PHI_DATA_TRUTH_AUDIT.json` with a non-METRIC sweep input.
Solution: Patched `Tools\RunMetricPhiVerifySweep.py` so custom `--json-output` paths derive sidecar H-Phi score, graph, atlas, and data-truth paths. Rebound the shared METRIC data-truth audit to the shared METRIC sweep after the last pre-patch binder exited.
Rejected Alternatives: Killing other agents; waiting indefinitely; accepting shared last-writer-wins reports. None of those create a durable ingestion boundary.
Scalability potential: Low/Toaster and High/Ultra domain sweeps can now run without corrupting METRIC canonical evidence. The sidecars remain stateless file artifacts.
Hardware Impact: 0 runtime us on i3/MX350. Final readback: shared H-Phi `2026-05-16T18:10:58`, `5015` files, `1,723,719` lines; shared sweep `2026-05-16T18:11:09`, `35` commands, `0` failures; shared data truth `2026-05-16T18:27:20`, `37` checks, `0` failures, `42` aligned binaries, `167` struct sites, H-Phi freshness `true`; cache cleanup `remaining=0`; active Python process count `0`.

## Decision 32 - Fresh Live OSHINO Revalidation

Problem: The user required another from-disk proof pass after the sidecar isolation and freshness fixes. Prior green artifacts were valid, but not fresh to the new escalation.
Solution: Reran the canonical Metric sweep, explicit economy Monte Carlo, crafting Monte Carlo, DataTruthInquisition, VerifyMetricPhiDataTruth, VerifyDataInquisition, Sabine, Math LUT, lore validators, py_compile, diff-check, cache cleanup, and active Python readback.
Rejected Alternatives: Reusing the previous `18:27:20` data-truth artifact; treating the immutable snapshot as enough after a new escalation; claiming runtime DataVault sovereignty increased.
Scalability potential: Low/Toaster data paths remain covered by stripped aligned binaries and low-tier LUTs; High/Ultra remains covered by overkill LUTs, harmonic/noise payloads, and sidecar-safe sweep outputs.
Hardware Impact: 0 runtime us on i3/MX350. Fresh evidence: H-Phi `2026-05-16T18:36:56`, `5015` files, `1,723,719` lines; sweep `2026-05-16T18:37:07`, `35` commands, `0` required failures; data truth `2026-05-16T18:38:04`, `37` checks, `0` failures, `42` aligned binaries, `167` struct sites, `0` endian failures; economy MC `1541057` node steps; crafting MC `1000000` steps, `0` profit steps; cache cleanup remaining `0`; active Python process count `0`.

## Decision 33 - Data Python Struct Surface And xxhash Default Discovery

Problem: A later disk audit showed that `VerifyMetricPhiDataTruth.py` and `VerifyDataInquisition.py` scanned Python `struct` callsites under `Tools` while `Data/Localization/Radio/*.py` also contains binary packer/verifier structs. The shared sweep path was also vulnerable to callers that omitted `--xxhash-path`, allowing a known replay-reference false failure to overwrite the canonical report under concurrent agents.
Solution: Added `STRUCT_SCAN_ROOTS=(Tools, Data)` to both verifiers and added `Docs/AgentLogs` to the Data Inquisition binary roots. Added deterministic `xxhash` path discovery in `RunMetricPhiVerifySweep.py` from `METRIC_PHI_XXHASH_PATH`, `%TEMP%\metric_phi_xxhash_ref`, or `.codex_tmp\metric_phi_xxhash_ref`. Bound final proof to `Docs/Reports/METRIC_PHI_DATA_TRUTH_AUDIT_STRUCT_SCOPE_FINAL.json` instead of the currently mutable shared sweep path.
Rejected Alternatives: Trusting the prior `167` struct-site count; manually whitelisting the radio files outside the scanner; killing active external sweeps; copying a green snapshot over the shared report while live writers still exist.
Scalability potential: Low/Toaster ingest now has wider stateless binary hygiene coverage without runtime private state. High/Ultra keeps the same overkill payload coverage while the sweep runner stops depending on tribal CLI flags for hash-reference proof.
Hardware Impact: 0 runtime us on i3/MX350. Evidence: py_compile PASS; `VerifyReplayHasherReference` PASS with pinned `xxhash` (`xxh3=466 shuffle=256`); Data Inquisition PASS with `42` binaries, `11` manifest/layout contracts, `161` struct formats, `0` failures; binary hygiene PASS with `42` binaries and `0` misaligned; sidecar Metric data truth PASS at `2026-05-16T22:25:24` with `37` checks, `0` failures, `42` aligned binaries, `171` struct sites, and `0` endian failures.

## Decision 34 - Strict Struct Resolver And Ore LCG Minimal LOD Repair

Problem: The literal-only struct scan still missed `struct.pack(FORMAT_CONST, ...)` and imported format constants. A full sidecar sweep then exposed real generated Ore LCG drift: `Ore_Distribution.h8bin` and `Ore_Distribution.json` disagreed on the minimal/toaster LOD section.
Solution: Upgraded the METRIC struct scanners to resolve module constants, imported tool constants, string/numeric expressions, `struct.calcsize`, format-table subscripts, guarded dynamic `<` formats, external container byte order, and negative big-endian sentinel tests. Rebuilt Ore LCG data from `Data/Economy/Resource_Distribution_Matrix.csv` using `Tools\OreLcgBaker.py --iterations 1000000`. Reran the full sidecar sweep and final binder against completed sidecar artifacts.
Rejected Alternatives: Marking unresolved struct formats as warnings; trusting the old Ore LCG binary because it was aligned; manually patching binary/JSON byte counts; relying on shared sweep reports while external agents were writing them.
Scalability potential: Low/Toaster now has verified minimal Ore LCG payload bytes plus full stripped binary coverage. High/Ultra keeps ultra visual records and overkill LUT payloads while all proof remains stateless file lookup, not private runtime state.
Hardware Impact: 0 runtime us on i3/MX350. Evidence: Ore LCG binary now `1776` bytes and aligned16; `VerifyOreLcgBaker` PASS; `VerifyOreLcgBinaryIndependent` PASS; full sidecar sweep PASS with `35` commands and `0` required failures; final Metric data truth PASS at `2026-05-17T01:29:46` with `43` binaries, `0` unaligned, `274` struct sites, `0` endian failures; H-Phi sidecar files=`5015`, lines=`1,723,788`, runtime H-Phi=`6.7481e-05`, DataSovereignty=`0.019743027`.
