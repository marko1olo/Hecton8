# Rationale_HEADLESS_SCENARIO_RUNNER

Mandates followed:
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/CI_MATH_VIOLATIONS_Gate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

Problem: Headless stress coverage is absent; scenario claims cannot be verified from static docs.
Solution: Add a Python CI runner outside Unity runtime that launches the compiled player with explicit headless flags, reads telemetry from files, parses Blackbox dumps after crashes, and writes a nightly evidence report.
Rejected Alternatives: Unity Editor test runner was rejected because the task targets the compiled `Hecton8.exe` player and CI stress must match player-build behavior. New Unity C# telemetry code was rejected by `[NO_UNITY_CODE]`.
Scalability potential: Low uses log-file telemetry and strict hang kill. Middle adds deterministic scenario hashes. High/Ultra can feed denser telemetry and longer scenario matrices without changing Unity gameplay code.
Hardware Impact: Estimated gain for low-end silicon as i3/MX350 is detection, not runtime frame gain. CI catches RAM slope and frame spikes before they ship to MX350. Runtime overhead in game is 0 us because this is external automation.

Problem: Crash visibility needs post-mortem evidence without touching the runtime Black Box implementation.
Solution: Parse `Docs/AgentLogs/Dump_*.bin` metadata when player exit code is non-zero. Record size, SHA256, and HECTON8 header fields when available.
Rejected Alternatives: Creating new runtime crash dump code was rejected because ownership belongs to Unity telemetry/Blackbox systems, not QA runner.
Scalability potential: Low only records dump metadata. Middle and High can archive dump manifests. Ultra can attach dumps to CI artifacts.
Hardware Impact: 0 us runtime impact; crash processing happens after process exit.

Problem: The first dummy report exposed duplicate telemetry samples because both telemetry JSONL and log fallback were parsed as authoritative.
Solution: Added priority parsing: telemetry JSONL wins; Unity log/stdout are fallback only for missing fields. This keeps replay evidence deterministic and prevents false frame sample inflation.
Rejected Alternatives: Leaving duplicated samples would make p50/p95 graphs dishonest. Removing log parsing was rejected because compiled player builds may only emit `-logFile` text until internal telemetry is wired.
Scalability potential: Low parses one JSONL. Middle/High can add optional log fallback fields without changing report schema. Ultra can add denser channels while still preserving single-source metric ownership.
Hardware Impact: 0 us runtime impact. CI-side parsing avoids gameplay overhead.

Problem: SHINOBU ingestion requirements need explicit binary and hash contracts instead of implicit scenario names.
Solution: Hardened `Tools/HeadlessScenarios.json` with schema, version, little-endian policy, 16-byte alignment, FNV-1a ASCII-lower scenario IDs, atlas domain binding, telemetry contract, TOASTER/MIDDLE/HIGH/RTX_OVERKILL profiles, and stateless data-sovereignty text.
Rejected Alternatives: Magic scenario names and undocumented profile assumptions were rejected because they are not machine-auditable.
Scalability potential: TOASTER uses sparse telemetry and one process. MIDDLE/HIGH add selected channels. RTX_OVERKILL records dense telemetry and Blackbox write-index data without changing Unity runtime code.
Hardware Impact: 0 us runtime impact. On i3/MX350 this prevents CI from overdriving concurrent headless players; on high-end machines it permits richer evidence capture.

Problem: A crash dump can exist but still be unusable if size alignment or header endian contract drifts.
Solution: Added `aligned16` reporting for parsed Blackbox dumps and a failure reason for unaligned or bad-header dumps. Dummy crash now emits exactly 16 bytes using `<QII`.
Rejected Alternatives: Header-only validation without alignment was rejected because SIMD/native ingest can still break on a valid magic with bad byte count.
Scalability potential: Low records metadata only. Ultra can archive binary dumps and manifests as CI artifacts.
Hardware Impact: 0 us runtime impact; parsing happens after process exit.

Problem: The user escalated data truth audits that are outside the original QA runner domain but relevant to proving the runner is not hiding data debt.
Solution: Ran existing project verification scripts without editing Unity C# or stealing other agents' data ownership. Results: hash collisions 0 across 1018 records, lore blob check passed, Sabine LUT physics/endian/alignment passed, VFX budget binary passed, blue-noise/flow verifier passed, taxonomy binary aligned and hash collisions 0, replay verifier guard passed, save hash guard passed, Data/Docs binary `.bin`/`.h8bin` scan found 38/38 aligned, H-Phi Python audit produced domain count 85, and the economy million-step Monte Carlo returned `STATUS: ECONOMY PROVEN`.
Rejected Alternatives: Mutating economy/lore/math data from QA ownership was rejected. Broad edits would violate domain boundaries and batch parallelism.
Scalability potential: Verification hooks remain external and stateless. Additional CI stages can consume the generated reports without new runtime state.
Hardware Impact: 0 us runtime impact. Tooling CPU cost is CI-only.

Problem: Economy Monte Carlo proof cannot remain a manual side note; the runner must fail CI if recipes or resource loops drift.
Solution: Added the economy Monte Carlo to the runner-owned `validationSuite` as a fail-severity command check. Latest integrated run: `players=7000`, `total_nodes_mined=1078223`, `average_minutes=41.315`, `p99_minutes=59.150`, `million_step_audit_passed=True`, `failures=0`, `STATUS: ECONOMY PROVEN`.
Rejected Alternatives: Accepting a separate manual economy log was rejected because it lets the nightly runner pass while cross-domain economy debt regresses. Editing economy data was rejected because this agent owns QA automation, not economy balance.
Scalability potential: QA runner gates nightly builds on deterministic external evidence. Low tier pays no runtime cost; high-end CI can extend the same suite with larger player counts or longer soak windows.
Hardware Impact: 0 us runtime impact. CI-only CPU time prevents a resource-loop regression from reaching i3/MX350 player builds.

Problem: Runner behavior needed pinned tests, not only manual CLI invocations.
Solution: Added `Tools/test_run_headless_simulations.py` covering catalog contract, Blackbox `<QII` little-endian alignment parsing, and dummy telemetry determinism without Unity.
Rejected Alternatives: Manual report inspection alone was rejected because it does not prevent regression of subprocess/log parsing behavior.
Scalability potential: Unit tests are cold CI checks. They add no runtime private state and no Unity C# dependency.
Hardware Impact: 0 us runtime impact. Test cost is CI-only.

Problem: Phase 1-4 audits were initially scattered across manual commands, which made the nightly runner weaker than the user escalation required.
Solution: Moved the hard gates into `Tools/HeadlessScenarios.json` under `validationSuite` and taught `Tools/RunHeadlessSimulations.py` to execute command checks, scan `.bin/.h8bin` alignment, capture per-check logs, and fail the Markdown report if any fail-severity gate fails. Latest integrated report shows FNV, lore, Sabine, VFX, blue-noise, taxonomy, replay/save hash guards, binary alignment, H-Phi, and economy all PASS.
Rejected Alternatives: Chat-only proof and one-off shell evidence were rejected because they decay after context compression and cannot be consumed by SHINOBU CI.
Scalability potential: TOASTER profile still runs one dummy/player process with sparse telemetry. RTX_OVERKILL keeps dense telemetry channels and validation artifacts without adding Unity runtime state.
Hardware Impact: 0 us runtime impact. On low-end silicon the benefit is pre-ship rejection of bad data; on high-end CI hardware the suite spends wall time to buy stronger nightly evidence.

Problem: Current restricted headless shell denied Python temp cleanup and multiprocessing pipe creation, causing false CI failures in replay guard and H-Phi validation.
Solution: Hardened cold-path tooling only. Validation subprocesses now receive workspace-local `TMP/TEMP/TMPDIR`; runner unit tests use `.codex-artifacts/headless-scenarios/unit-tests`; replay guard uses `.codex-artifacts/replay-reference-verifier` instead of `TemporaryDirectory`; H-Phi gate uses `--workers 1` with `timeoutSeconds=1800`.
Rejected Alternatives: Escalating the process or marking the failed checks as warnings was rejected because the task requires the validation suite to be executable from current disk without lying. Modifying Unity runtime code was rejected by the original XML `[NO_UNITY_CODE]`.
Scalability potential: TOASTER/locked-down CI uses single-worker H-Phi and workspace artifacts. High-end CI can restore parallel H-Phi in a separate profile once process-pool permissions are proven, without changing gameplay state.
Hardware Impact: 0 us runtime impact. CI wall time increased for H-Phi (`1370.16` seconds in the latest pass) to preserve deterministic evidence under restricted infrastructure.

Problem: Markdown reports are not a zero-cost SHINOBU ingestion surface, and endian/subprocess hygiene was still partly dependent on manual grep.
Solution: Added `Docs/Reports/Nightly_Build_Report.json` output from the runner and a fail-severity `QA_Source_Contract_Scan` validation gate. The scan uses AST for `shell=True` and `TemporaryDirectory`, and source text for `struct.pack/unpack/calcsize` endian prefixes. Latest result: `files=3`, `structEndianViolations=0`, `shellTrue=0`, `tempDirectoryUses=0`.
Rejected Alternatives: Parsing Markdown tables in CI was rejected because it is brittle. Manual source scans were rejected because they are not replayable after context compression.
Scalability potential: Low-end CI can consume compact JSON without regex scraping. RTX/overkill CI can ingest the same schema and attach richer telemetry without changing Unity runtime code.
Hardware Impact: 0 us runtime impact. The additional scan took `0.52` CI seconds in the latest full pass.

Problem: The validation suite still did not gate the broad `Verify*.py` data inquisition scripts that already exist on disk.
Solution: Added four fail-severity command checks to `Tools/HeadlessScenarios.json`: `Data_Inquisition_Static`, `Binary_Hygiene_Global`, `Metric_Phi_Data_Truth`, and `Optics_Beer_Lambert_LUT`. Latest full run is 16/16 PASS. Evidence includes `monteCarloSteps=1000000`, `hashCollisions=0`, `atlasDomains=85`, global `misalignedCount=0`, metric-phi `failed=0`, `endian_failures=0`, and optics `pack=<e`.
Rejected Alternatives: Running those scripts manually and leaving the runner green without them was rejected because the next batch would lose the proof. Adding every `Verify*.py` blindly was rejected because some scripts are owned by unrelated active domains and can require domain-specific arguments or artifacts; the added checks cover the user-requested math, economy, lore, binary, hash, and H-Phi surfaces with stable CLI contracts.
Scalability potential: Low-end CI still runs deterministic cold scripts and writes JSON artifacts. High-end CI can extend the validation catalog with more domain-owned verifiers without changing runner code.
Hardware Impact: 0 us runtime impact. Latest additional CI costs: data inquisition `39.22s`, binary hygiene `539.06s`, metric-phi data truth `10.66s`, optics LUT `5.31s`.

Problem: New verification scripts can appear on disk without being gated, which would silently rot the QA runner's evidence boundary.
Solution: Added `Verification_Tool_Inventory`, a fail-severity inventory scan over `Tools/**/(Verify|Validate)*.py`. Every discovered verifier must be classified as direct, covered by a broad gate, or deferred with a cross-domain reason. Latest integrated result: `discovered=43`, `classified=43`, `directCommandScripts=14`, `requiredDirect=12`, `unclassified=0`, `stale=0`, `missingDirect=0`.
Rejected Alternatives: Blindly executing every verifier was rejected because several are cross-domain Unity, hardware, UX, or network validators with separate ownership. Leaving them invisible was rejected because it hides debt. The inventory gate makes deferred scope explicit and fails if new unclassified verification debt appears.
Scalability potential: Low-end CI gets a cheap classification check. High-end CI can promote any deferred verifier into `requiredDirect` without changing runner code.
Hardware Impact: 0 us runtime impact. Latest inventory scan duration rounds to `0.00s` in the report.

Problem: SHINOBU can ingest JSON, but a report bundle without per-artifact hashes still forces consumers to trust filenames and timestamps.
Solution: Added `Docs/Reports/Nightly_Build_ArtifactManifest.json` generation. The manifest records schema `H8_HEADLESS_ARTIFACT_MANIFEST`, SHA-256, byte count, existence, and binary alignment for the scenario catalog, Markdown report, JSON report, FrameTime SVG, per-run log/stdout/telemetry files, validation logs, and Blackbox dumps. The JSON and Markdown reports now expose the manifest path.
Rejected Alternatives: Embedding hashes only inside Markdown was rejected because Markdown is a human surface. Hashing only the JSON report was rejected because validation logs and telemetry are the actual evidence body.
Scalability potential: Low/TOASTER CI can consume a compact hash manifest without replaying logs. Middle/High/RTX_OVERKILL can attach denser telemetry and dumps while keeping the same stateless manifest schema.
Hardware Impact: 0 us runtime impact. CI-only hashing of the latest 27 artifacts makes ingestion deterministic without adding Unity memory or frame cost.

Problem: The first full post-manifest suite failed because `Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json` was stale and referenced an old missing self-check sidecar.
Solution: Regenerated the canonical Metric Phi sweep using `Tools/RunMetricPhiVerifySweep.py`; the new report is `VERIFY_SWEEP_PASS`, 35 commands, 0 required failures. Then reran `VerifyMetricPhiDataTruth.py` and the full headless runner validation suite. Latest results: `DATA_TRUTH_VERIFIED`, `checks=37`, `failed=0`, integrated runner `CI_STATUS=PASS`, validation `17/17`, manifest `PASS`, artifacts `27`, missing `0`.
Rejected Alternatives: Weakening the `Metric_Phi_Data_Truth` required output check was rejected because that would hide stale cross-domain evidence. Editing Metric Phi internals was rejected because regeneration fixed the canonical report and this agent owns QA automation, not the Metric Phi tool.
Scalability potential: Runner remains stateless and fails on stale upstream evidence instead of caching private state. High-end CI can regenerate the Metric Phi sweep before headless validation when wall time is acceptable.
Hardware Impact: 0 us runtime impact. CI wall time increased, but stale data-truth evidence is now rejected before player builds reach low-end hardware.

Problem: The artifact manifest was generated correctly, but the runner did not yet fail the process if the freshly generated manifest was later discovered to be false.
Solution: Added `validate_artifact_manifest()` to re-read the manifest and verify schema, status, missing list, artifact existence, byte count, SHA-256, and `.bin/.h8bin` 16-byte alignment. The runner exit path now treats manifest validation errors as `HEADLESS_RUNNER_FAIL`. Unit coverage now tampers with a validation log after manifest generation and requires `SHA256_MISMATCH`.
Rejected Alternatives: Warning-only validation was rejected because SHINOBU ingest must be hard-gated. Putting the verifier only in a separate script was rejected because the runner itself must reject a corrupt report bundle.
Scalability potential: Low/TOASTER CI fails fast on bad report bundles without replaying expensive validations. High/RTX CI can trust the same stateless manifest before uploading denser telemetry and dumps.
Hardware Impact: 0 us runtime impact. Latest full runner pass after this change: `CI_STATUS=PASS`, validation `17/17`, manifest `PASS`, artifacts `27`, missing `0`, manifest validation errors `0`.
