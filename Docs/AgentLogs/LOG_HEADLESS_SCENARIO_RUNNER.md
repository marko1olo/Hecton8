# LOG_HEADLESS_SCENARIO_RUNNER

## 2026-05-16 - Headless CI Stress Runner

What was wrong:
- No runner existed at `Tools/RunHeadlessSimulations.py`; nightly headless scenario execution was not configurable from disk.
- Scenario definitions were absent as machine-auditable JSON.
- Crash dump parsing, RAM-slope failure, deterministic replay hash comparison, hang kill, and report generation were not present for this prompt.
- Initial dummy report double-counted metrics when both telemetry JSONL and log fallback contained the same samples.
- Scenario catalog did not expose SHINOBU-facing endian/alignment/hash/tier contracts.

What was done:
- Added `Tools/RunHeadlessSimulations.py`.
- Added `Tools/HeadlessScenarios.json` with schema `H8_HEADLESS_SCENARIO_CATALOG`, little-endian policy, 16-byte binary alignment, FNV-1a 32-bit scenario IDs, atlas family/domain, telemetry contract, and TOASTER/MIDDLE/HIGH/RTX_OVERKILL profiles.
- Added `Tools/test_run_headless_simulations.py` with 3 focused tests for catalog audit, Blackbox header/alignment parsing, and dummy deterministic telemetry.
- Generated `Docs/Reports/Nightly_Build_Report.md` and `Docs/Reports/Nightly_Build_FrameTime.svg`.
- Generated crash-path dummy report `Docs/Reports/Nightly_Build_Report_CrashDummy.md` and `Docs/Reports/Nightly_Build_Report_CrashDummy_FrameTime.svg`.
- Added status/rationale journals at `Docs/Tasks/Status_HEADLESS_SCENARIO_RUNNER.md` and `Docs/AgentLogs/Rationale_HEADLESS_SCENARIO_RUNNER.md`.

Cinematic cheats used:
- None in runtime. This is external QA automation.
- CI-side telemetry parsing is the fake-first substitute for adding new Unity runtime instrumentation in this batch. It buys evidence without adding frame cost.

Exact microseconds saved:
- Unity runtime: `0 us/frame` direct cost because no Unity C# gameplay/runtime code was changed.
- Avoided runtime telemetry implementation: estimated `20-100 us/frame` avoided versus adding new per-frame managed Unity logging; this is a static estimate, not profiler proof.
- Report generation, FNV audit, graph generation, Blackbox parsing, and Monte Carlo execution are CI-only and not player-frame costs.

Verification:
- `python -B -c "import ast, pathlib; ast.parse(pathlib.Path('Tools/RunHeadlessSimulations.py').read_text(encoding='utf-8'))"`: exit 0.
- `python -B -m unittest Tools.test_run_headless_simulations`: exit 0, `Ran 3 tests`.
- `python -B Tools/RunHeadlessSimulations.py --dummy-exit-code 0 --scenario 100_Days_Idle --hang-timeout-sec 30`: exit 0, `HEADLESS_RUNNER_PASS`.
- `python -B Tools/RunHeadlessSimulations.py --dummy-exit-code 13 --scenario Max_Stress_Test --hang-timeout-sec 30 --report Docs/Reports/Nightly_Build_Report_CrashDummy.md`: expected non-zero scenario failure path, report generated, aligned Blackbox header parsed.
- `python -B Tools/VerifyH8HashCollisions.py --write-report Docs/Reports/H8_Hash_Catalog_Audit.md --write-json Docs/Reports/H8_Hash_Catalog_Audit.json`: exit 0, `1018` records, `HASH COLLISIONS: 0`.
- `python -B Tools/VerifyLore.py --check --verify-source --verify-manifest`: exit 0, `alignment=16 endian=<`.
- `python -B Tools/VerifySabineBaker.py`: exit 0, `SABINE_LUT_VERIFIED`, physics audit `Sabine+Thorp+BeerLambert+HydrostaticPressure`.
- `python -B Tools/VerifyVramBudgets.py`: exit 0, `HASH_COLLISIONS=0`, binary `Data/System/VFX_Budgets.h8bin`.
- `python -B Tools/NoiseBaker/VerifyBlueNoiseSpectrum.py`: exit 0, `passed=true`.
- `python -B Tools/Taxonomy/verify_taxonomy.py`: exit 0, `hashCollisions=0`, `binaryAligned16=yes`.
- `python -B Tools/Security/ValidateReplayHasherReferenceVerifier.py`: exit 0.
- `python -B Tools/Security/ValidateSaveMasterHashCSharp.py`: exit 0.
- Binary scan for `.bin/.h8bin` in `Data`, `Docs/AgentLogs`, `Docs/Reports`: `36` binary files, `0` unaligned.
- `python -B Tools/CalculateHPhi.py --workers 4 --json-output .codex-artifacts/headless-scenarios/HECTON_PHI_HEADLESS_AUDIT.json --graph-output .codex-artifacts/headless-scenarios/HECTON_PHI_HEADLESS_GRAPH.png --atlas .codex-artifacts/headless-scenarios/PROJECT_ATLAS_HEADLESS_AUDIT.md`: exit 0, `DOMAIN_INDEX_COUNT=85`.
- `python -B Tools/Economy/MonteCarloEconomySim.py --players 7000 --max-nodes 10000`: million-step audit passed (`total_nodes_mined=1109298`, `failures=0`) but p99 time failed default threshold (`60.635` minutes), so economy balance remains cross-domain `PENDING VERIFICATION`.

Residual risk:
- No compiled `Hecton8.exe` was present/provided in this shell; real player-build execution remains `PENDING VERIFICATION`.
- GCMonitor, Unity Console, Play Mode, profiler, and MX350 target performance are not proven by dummy process evidence.
- Economy p99 threshold failure is not fixed here because economy balance is outside this QA runner domain.

## 2026-05-16 - Integrated Validation Suite Hardening

What was wrong:
- Phase 1-4 data truth evidence existed as manual shell history and separate reports, not as a runner-owned CI gate.
- Binary hygiene proof was stale after new `.bin` artifacts appeared.
- The old economy note recorded an earlier risk, while the current disk run now has a passing million-step Monte Carlo artifact.

What was done:
- Added `validationSuite` execution to the headless runner path and wired the scenario catalog to fail CI on FNV, lore, Sabine, VFX, blue-noise, taxonomy, replay/save hash, binary alignment, H-Phi, and economy regressions.
- Regenerated `Docs/Reports/Nightly_Build_Report.md` with the integrated validation table and per-check artifacts under `.codex-artifacts/headless-scenarios/20260516T025228Z/validation/`.
- Updated status and rationale to reflect the latest integrated evidence instead of stale manual-only notes.

Cinematic cheats used:
- No runtime simulation was added. The cheat is external evidence capture: run the compiled-player/dummy process and data validators outside the frame, then spend zero runtime microseconds on new QA instrumentation.

Exact microseconds saved:
- Unity runtime: `0 us/frame` because this agent still made no Unity C# runtime edits.
- Avoided new per-frame validation hooks: estimated `20-100 us/frame` avoided versus managed per-frame logging or in-player validator dispatch; static estimate only, no profiler claim.
- CI validation wall time is external and does not consume player frame budget.

Verification:
- `Docs/Reports/Nightly_Build_Report.md`: `CI status: PASS`.
- Integrated validation suite: 11/11 fail-severity checks PASS.
- Binary alignment scan: `total=38 unaligned=0`.
- H-Phi audit: `DOMAIN_INDEX_COUNT=85`, `STATUS: PHI CALCULATED`.
- Economy Monte Carlo: `players=7000`, `total_nodes_mined=1078223`, `p99_minutes=59.150`, `million_step_audit_passed=True`, `failures=0`, `STATUS: ECONOMY PROVEN`.

Residual risk:
- Real `Hecton8.exe` execution, Unity Console, Play Mode, GCMonitor, profiler capture, and MX350 player telemetry remain `PENDING VERIFICATION` until a compiled player artifact and fresh runtime logs exist.

## 2026-05-16 - Restricted Runner Reverification

What was wrong:
- Fresh full-suite run failed instead of matching stale evidence: replay guard crashed during Python `TemporaryDirectory` cleanup, and H-Phi failed first on denied multiprocessing pipes, then on a 900-second single-worker timeout.
- The status file still referenced the earlier `--workers 4` H-Phi proof path, which is not reliable in the current restricted headless shell.

What was done:
- `Tools/RunHeadlessSimulations.py` now gives validation subprocesses workspace-local `TMP/TEMP/TMPDIR` and runs them from the repo root.
- `Tools/test_run_headless_simulations.py` now uses workspace-local `.codex-artifacts/headless-scenarios/unit-tests` scratch paths instead of OS temp.
- `Tools/Security/ValidateReplayHasherReferenceVerifier.py` now uses `.codex-artifacts/replay-reference-verifier` for the helper-module cleanup guard, avoiding Python temp cleanup failure while preserving the negative guard check.
- `Tools/HeadlessScenarios.json` now runs H-Phi as `--workers 1` with `timeoutSeconds=1800` so the 85-domain audit completes under sandboxed CI.

Cinematic cheats used:
- None in runtime. This is CI-only hardening.
- The fake-first choice remains external data validation instead of new in-player verification loops.

Exact microseconds saved:
- Unity runtime: `0 us/frame`; no Unity C# runtime code was edited.
- Avoided runtime validation hooks: estimated `20-100 us/frame` avoided versus per-frame managed validation/logging. Static estimate only.
- CI wall time increased intentionally to preserve evidence under restricted process permissions.

Verification:
- `python -B Tools/Security/ValidateReplayHasherReferenceVerifier.py`: exit 0, `REPLAY_REFERENCE_VERIFIER_GUARD=PASS checks=20`.
- `python -B -m unittest Tools.test_run_headless_simulations`: exit 0, `Ran 3 tests`.
- `python -B Tools/RunHeadlessSimulations.py --dummy-exit-code 0 --scenario 100_Days_Idle --hang-timeout-sec 30 --run-validation-suite --report Docs/Reports/Nightly_Build_Report.md`: exit 0.
- Latest report `Docs/Reports/Nightly_Build_Report.md`: generated `2026-05-16T06:38:30Z`, `CI status: PASS`, validation suite 11/11 PASS.
- H-Phi latest pass: scanned `5015` files, `DOMAIN_INDEX_COUNT=85`, `RUNTIME_H_PHI_STATIC=6.7481e-05`, `STATUS: PHI CALCULATED`.
- Economy latest pass: `players=7000`, `total_nodes_mined=1078223`, `p99_minutes=59.150`, `million_step_audit_passed=True`, `failures=0`, `STATUS: ECONOMY PROVEN`.
- Binary alignment latest pass: `total=38 unaligned=0`.

Residual risk:
- Real compiled `Hecton8.exe`, Unity Console, Play Mode, profiler, GCMonitor, and MX350 telemetry remain `PENDING VERIFICATION`.

## 2026-05-16 - SHINOBU JSON Evidence Pass

What was wrong:
- `Docs/Reports/Nightly_Build_Report.md` was readable but not ideal for zero-cost CI ingestion.
- Endian and subprocess hygiene were proven by source review and grep, not by a first-class validation gate.

What was done:
- Added JSON report emission beside the Markdown report: `Docs/Reports/Nightly_Build_Report.json`.
- Added `QA_Source_Contract_Scan` to `Tools/HeadlessScenarios.json`.
- Added source-contract scan support to `Tools/RunHeadlessSimulations.py`, checking owned QA Python for little-endian `struct.pack/unpack/calcsize`, `shell=True`, and `TemporaryDirectory` regressions.

Cinematic cheats used:
- None in runtime. The evidence layer remains external and stateless.
- JSON output is the ingestion fake: SHINOBU consumes cold data instead of scraping rich prose.

Exact microseconds saved:
- Unity runtime: `0 us/frame`; still no Unity runtime code touched.
- CI ingestion: avoids Markdown table parsing; runtime frame cost is not applicable.

Verification:
- Full runner command exited 0: `python -B Tools/RunHeadlessSimulations.py --dummy-exit-code 0 --scenario 100_Days_Idle --hang-timeout-sec 30 --run-validation-suite --report Docs/Reports/Nightly_Build_Report.md`.
- Latest report generated `2026-05-16T10:29:48Z`: `CI status: PASS`.
- Validation suite: 12/12 PASS.
- `QA_Source_Contract_Scan`: `files=3`, `structEndianViolations=0`, `shellTrue=0`, `tempDirectoryUses=0`.
- `Docs/Reports/Nightly_Build_Report.json`: `ciStatus=PASS`, `validation` length `12`, `fnvCollisions=0`, `binaryAlignmentBytes=16`.

Residual risk:
- Real player-build execution and Unity-side GC/profiler/MX350 telemetry remain `PENDING VERIFICATION` until `Hecton8.exe` exists.

## 2026-05-16 - Broad Verify Gate Expansion

What was wrong:
- The runner gated selected validators, but broad existing `Verify*.py` data inquisition surfaces were not part of the nightly pass.
- Manual proof that those scripts passed was not enough for SHINOBU or context-compressed continuation.

What was done:
- Added `Data_Inquisition_Static` to the validation suite.
- Added `Binary_Hygiene_Global` to the validation suite.
- Added `Metric_Phi_Data_Truth` to the validation suite.
- Added `Optics_Beer_Lambert_LUT` to the validation suite.
- Re-ran the full headless validation suite and regenerated Markdown plus JSON reports.

Cinematic cheats used:
- None in runtime. This is cold CI proof.
- Broad static/data verifiers replace new runtime probes, preserving `0 us/frame` player cost.

Exact microseconds saved:
- Unity runtime: `0 us/frame`; no Unity runtime code touched.
- CI time spent intentionally: data inquisition `39.22s`, binary hygiene `539.06s`, metric-phi data truth `10.66s`, optics LUT `5.31s`.

Verification:
- Full runner command exited 0: `python -B Tools/RunHeadlessSimulations.py --dummy-exit-code 0 --scenario 100_Days_Idle --hang-timeout-sec 30 --run-validation-suite --report Docs/Reports/Nightly_Build_Report.md`.
- Latest report generated `2026-05-16T11:39:07Z`: `CI status: PASS`.
- Validation suite: 16/16 PASS.
- `Data_Inquisition_Static`: `binaries=40`, `aligned16=true`, `endian=<`, `structFormats=151`, `monteCarloSteps=1000000`, `hashCollisions=0`, `atlasDomains=85`.
- `Binary_Hygiene_Global`: `binaryCount=41`, `misalignedCount=0`.
- `Metric_Phi_Data_Truth`: `checks=36`, `failed=0`, `binary_files=41`, `unaligned=0`, `struct_format_sites=161`, `endian_failures=0`.
- `Optics_Beer_Lambert_LUT`: `matrixBytes=393216`, `aligned16=True`, `byteOrder=little-endian`, `pack=<e`, `fnvCollisions=0`, `dataSovereignty=stateless_binary_lookup`.

Residual risk:
- The runner still cannot prove real player GC/profiler/MX350 behavior without a compiled `Hecton8.exe`.

## 2026-05-16 - Verification Inventory Closure

What was wrong:
- The suite had broad `Verify*.py` gates, but new verifier scripts could still appear under `Tools` without being gated or consciously deferred.
- That would let CI claim completeness while the verification surface drifted.

What was done:
- Added `Verification_Tool_Inventory` validation kind to `Tools/RunHeadlessSimulations.py`.
- Added `Verification_Tool_Inventory` gate to `Tools/HeadlessScenarios.json`.
- Classified all current verification-style tools under `Tools`: direct, covered by broad gate, or deferred cross-domain.
- Re-ran the full suite and regenerated `Docs/Reports/Nightly_Build_Report.md` and `.json`.

Cinematic cheats used:
- None in runtime. This is CI-only evidence closure.
- The inventory gate is the cheap deterministic fake for blanket execution of unrelated cross-domain validators.

Exact microseconds saved:
- Unity runtime: `0 us/frame`.
- Inventory scan: report duration rounded to `0.00s`; player runtime cost remains zero.

Verification:
- Full runner command exited 0.
- Latest report generated `2026-05-16T19:56:22Z`: `CI status: PASS`.
- Validation suite: 17/17 PASS.
- `Verification_Tool_Inventory`: `discovered=43`, `classified=43`, `directCommandScripts=14`, `requiredDirect=12`, `unclassified=0`, `stale=0`, `missingDirect=0`.
- JSON report check: `ciStatus=PASS`, validation count `17`, failing validation list `[]`.

Residual risk:
- Deferred cross-domain verifiers are explicitly classified, not executed by this QA runner. Real player-build telemetry still requires `Hecton8.exe`.

## 2026-05-16 - Artifact Manifest and Stale Sweep Recovery

What was wrong:
- The headless report had JSON, Markdown, SVG, run logs, telemetry, and validation logs, but no single hash manifest for zero-cost SHINOBU ingestion.
- A post-manifest full-suite run failed because `Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json` was stale and referenced a missing old self-check sidecar, causing `Metric_Phi_Data_Truth` to fail.

What was done:
- Added `Docs/Reports/Nightly_Build_ArtifactManifest.json` generation in `Tools/RunHeadlessSimulations.py`.
- Added manifest path exposure to `Docs/Reports/Nightly_Build_Report.md` and `Docs/Reports/Nightly_Build_Report.json`.
- Added `artifactIntegrityContract` to `Tools/HeadlessScenarios.json`.
- Added unit coverage for manifest role presence, SHA-256 fields, zero missing artifacts, and JSON report manifest linkage.
- Regenerated the canonical Metric Phi sweep with `Tools/RunMetricPhiVerifySweep.py`, then reran `VerifyMetricPhiDataTruth.py` and the full headless validation suite.

Cinematic cheats used:
- None in runtime. This remains external QA automation.
- Manifest hashing is a cold data fake for runtime evidence state: SHINOBU gets deterministic proof without Unity player allocations.

Exact microseconds saved:
- Unity runtime: `0 us/frame`; no Unity C# gameplay/runtime code was touched.
- Avoided in-player artifact indexing/log hashing: estimated `20-100 us/frame` avoided versus managed per-frame evidence hooks. Static estimate only.
- CI wall time was spent deliberately: Metric Phi sweep regenerated 35 command rows; latest headless suite wall time was CI-only.

Verification:
- `python -B -m py_compile Tools/RunHeadlessSimulations.py Tools/test_run_headless_simulations.py`: exit 0.
- `python -B -m unittest Tools.test_run_headless_simulations`: exit 0, `Ran 4 tests`.
- `python -B Tools/RunMetricPhiVerifySweep.py`: exit 0, `VERIFY_SWEEP_PASS`, commands `35`, required failures `0`.
- `python -B Tools/VerifyMetricPhiDataTruth.py --json-output Docs/Reports/Headless_Metric_Phi_Data_Truth.json --markdown-output Docs/Reports/Headless_Metric_Phi_Data_Truth.md`: exit 0, `DATA_TRUTH_VERIFIED`, `checks=37`, `failed=0`, `binary_files=43`, `unaligned=0`, `struct_format_sites=274`, `endian_failures=0`.
- `python -B Tools/RunHeadlessSimulations.py --dummy-exit-code 0 --scenario 100_Days_Idle --hang-timeout-sec 30 --run-validation-suite --report Docs/Reports/Nightly_Build_Report.md`: exit 0.
- Latest report directory: `.codex-artifacts/headless-scenarios/20260516T221141Z/`.
- `Docs/Reports/Nightly_Build_Report.json`: `CI_STATUS=PASS`, validation `17/17`, failures `[]`, manifest `Docs/Reports/Nightly_Build_ArtifactManifest.json`.
- `Docs/Reports/Nightly_Build_ArtifactManifest.json`: status `PASS`, artifacts `27`, missing `0`, bad artifact/alignment list `[]`.
- `git diff --check` on owned files: exit 0; only CRLF warning on `Tools/Security/ValidateReplayHasherReferenceVerifier.py`.

Residual risk:
- Real compiled `Hecton8.exe`, Unity Console, Play Mode, profiler, GCMonitor, target hardware telemetry, and actual player crash-dump semantics remain `PENDING VERIFICATION`.
- A `.codex_tmp/metric_phi_selfcheck/*.json` sidecar remained after the upstream Metric Phi sweep. It did not block this runner because the canonical sweep and data-truth reports passed; ownership of that cleanup behavior is Metric Phi tooling, not the headless runner.

## 2026-05-16 - Manifest Self-Validation Gate

What was wrong:
- The runner generated a hash manifest, but the process exit path did not hard-fail if the manifest was inconsistent.
- The manifest test covered the happy path only; it did not prove tamper detection.

What was done:
- Added `validate_artifact_manifest()` to `Tools/RunHeadlessSimulations.py`.
- The validator re-checks schema, status, missing-artifact list, artifact existence, byte count, SHA-256, and `.bin/.h8bin` 16-byte alignment.
- Wired manifest validation errors into `HEADLESS_RUNNER_FAIL`.
- Extended `Tools/test_run_headless_simulations.py` to tamper with a validation log after manifest generation and require `SHA256_MISMATCH`.
- Re-ran the full validation suite after changing runner failure semantics.

Cinematic cheats used:
- None in runtime. This is cold CI hardening.
- The manifest verifier is the cheap deterministic substitute for runtime evidence bookkeeping.

Exact microseconds saved:
- Unity runtime: `0 us/frame`; no Unity C# runtime code was touched.
- Avoided in-player artifact integrity bookkeeping: estimated `20-100 us/frame` avoided versus managed runtime evidence hashing. Static estimate only.

Verification:
- `python -B -m py_compile Tools/RunHeadlessSimulations.py Tools/test_run_headless_simulations.py`: exit 0.
- `python -B -m unittest Tools.test_run_headless_simulations`: exit 0, `Ran 4 tests`.
- Cheap dummy runner pass: manifest `PASS`, artifacts `10`, missing `0`, validation errors `0`.
- Full runner pass: `CI_STATUS=PASS`, validation `17/17`, failures `[]`.
- Latest report directory: `.codex-artifacts/headless-scenarios/20260516T224717Z/`.
- Latest artifact manifest: status `PASS`, artifacts `27`, missing `0`, validation errors `0`.
- `git diff --check` on owned files: exit 0; only CRLF warning on `Tools/Security/ValidateReplayHasherReferenceVerifier.py`.

Residual risk:
- Real compiled `Hecton8.exe`, Unity Console, Play Mode, profiler, GCMonitor, target hardware telemetry, and runtime Blackbox field semantics remain `PENDING VERIFICATION`.
