# Rationale - LOOT_TABLE_ENTROPY_AUDIT

## Decision 1 - Distribution Source

Problem: The prompt requires `Ore_Distribution.json`; it was absent at initial ingest, then appeared during the batch as `Data/Economy/Ore_Distribution.json` with `weight_u8` entries.
Solution: Keep CSV fallback, but prefer the current authored JSON when present. The simulator accepts `lcg_spawn_weight_u16`, `weight_u16`, `weight_u8`, or `weight` so source-format drift does not invalidate the audit.
Rejected Alternatives: Creating a fake source `Ore_Distribution.json` before analysis would hide upstream handoff drift. Mutating C# spawners is prohibited by the prompt. Mutating the source CSV/JSON is too broad for an audit task.
Scalability potential: Low uses integer weights only. Middle adds display metadata. High/Ultra consume tuned JSON extra fields for deterministic visual overkill without runtime table mutation.
Hardware Impact: 0 us/frame on i3/MX350 because all work is offline Python; no runtime memory or VRAM impact.

## Decision 2 - First Base Definition

Problem: The prompt says First Base but does not name the exact target recipe set.
Solution: Define First Base as the closed loop of `Module_FoundationKit`, `Module_AirlockFrame`, `Module_FabricatorBench`, `Module_PowerRelay`, and `Module_O2Recycler`. That set covers structure, access, fabrication, power, and oxygen. The simulator resolves raw resource demand recursively from `Recipes.json`.
Rejected Alternatives: Using the full `Time_To_First_Submarine.json` path adds submarine upgrades and measures a later milestone. Hardcoding only X Titanium/Y Copper ignores dependency costs and violates task 14.
Scalability potential: Low/Middle read the core target list. High/Ultra can add optional storage/comfort modules as separate scenarios without contaminating the base gate.
Hardware Impact: 0 us/frame; offline dependency graph only.

## Decision 3 - RNG Contract

Problem: Monte Carlo is worthless if it uses Python `random` while runtime uses deterministic LCG/Burst math.
Solution: Mirror C# constants `1664525u` and `1013904223u`, plus the `DeployableSdfDrillMath.Mix` bit pattern, and use multiply-high range mapping for weighted rolls.
Rejected Alternatives: `random.Random`, modulo weighted selection, and floating cumulative weights. They do not match project slot-machine law and can hide bias.
Scalability potential: Low uses 16-bit table weights. Middle/High/Ultra can raise table complexity while preserving integer cumulative selection.
Hardware Impact: 0 us/frame; offline CPU cost only. Runtime implication is favorable because the same integer path is Burst-friendly.

## Decision 4 - Tuning Boundary

Problem: Current authored JSON baseline 10,000-player p99 time to First Base was 79.555 minutes, above the 60 minute soft-lock threshold. Copper p99 was 59.284 minutes, but Titanium and secondary dependencies pushed total p99 over budget.
Solution: Generate `Tools/Economy/Ore_Distribution_Tuned.json` as a non-destructive tuned recommendation. Pass 1 reduced p99 to 59.285 minutes with 0 failures and 1,541,057 mined-node steps. Source JSON/CSV were not changed.
Rejected Alternatives: Mutating `Ore_Distribution.json` or `Resource_Distribution_Matrix.csv` inside an audit, changing C# spawners, or only inflating Copper while Titanium/secondary resources still gate completion.
Scalability potential: Low keeps current source data and can import tuned weights later. Middle/High/Ultra can use tuned JSON as a richer authored resource profile, with visual overkill bought through saved runtime churn rather than runtime randomness.
Hardware Impact: 0 us/frame on i3/MX350; no runtime code path changed. Estimated runtime gain is process-side: avoids a PlayMode/runtime balancing loop for this audit slice.

## Decision 5 - Evidence Class

Problem: The prompt demands `ECONOMY PROVEN` and `VERIFIED MASTER GRADE`, but project evidence law forbids runtime claims from static/CLI artifacts.
Solution: Mark the economy audit as `ECONOMY PROVEN` for CLI/static Monte Carlo data and `VERIFIED MASTER GRADE` for the offline batch artifact set, while explicitly retaining Unity runtime proof as PENDING VERIFICATION.
Rejected Alternatives: Claiming Unity route safety, GCMonitor cleanliness, or PlayMode resource acquisition without those artifacts.
Scalability potential: Low/Middle/High/Ultra status can all consume the same audited table once runtime import ownership exists.
Hardware Impact: 0 us/frame; evidence labeling prevents false runtime assumptions on low-end hardware.

## Decision 6 - Simulator Throughput And Artifact Races

Problem: The first full rerun exposed two tooling defects: timed-out Python processes kept running and overwrote reports out of order, and the audit loop used per-node Python `Counter` and string demand checks.
Solution: Terminated stale Python workers, then converted the simulator hot audit loop to compact integer resource indices, fixed-size inventory lists, and a remaining-demand counter. Also removed duplicate baseline simulation inside `run_with_tuning`.
Rejected Alternatives: Accepting the transient 100-player smoke report, accepting a stale 7,000-player report, or raising timeouts without removing the duplicate baseline pass.
Scalability potential: Low/Middle/High/Ultra all get the same deterministic LCG proof faster. This is offline tooling speed, not a runtime gameplay claim.
Hardware Impact: 0 us/frame runtime. Offline 100-player smoke dropped from roughly 57 seconds to 8.4 seconds in the observed shell; exact wall time is host-dependent.

## Decision 7 - Million-Step And Infinite-Loop Proof

Problem: The user demanded proof of 1,000,000 Monte Carlo steps and no infinite resource loop, not just p99 balance.
Solution: Persist `total_nodes_mined`, `monte_carlo_steps`, and `million_step_audit_passed` into the JSON/report. Final stable readback is 10,000 players, 1,541,057 mined-node steps, 0 failures, p99 59.285135 minutes. Recipe graph audit reports DAG true, cycle_count 0, no zero-ingredient recipes, no nonpositive quantities, and no unknown ingredients.
Rejected Alternatives: Counting theoretical budget as proof, using average time as proof, or relying on recipe file shape without graph traversal.
Scalability potential: Low gets fail-fast table proof. High/Ultra get extra deterministic data without allowing recipes to become a private-state loop.
Hardware Impact: 0 us/frame runtime. Offline audit cost is bounded and outside Unity hot paths.

## Decision 8 - Binary, Endian, Hash, And Physics-LUT Evidence

Problem: Binary/cache hygiene and physical LUT truth cannot be asserted from names or comments.
Solution: Ran `DataTruthInquisition`, `VerifyBinaryHygiene`, `VerifyH8HashCollisions`, `VerifyOpticsBaker`, `VerifySabineBaker`, `VerifyLore`, `test_dalton_gas_toxicity_baker`, and `test_math_lut_generator`. Evidence: 0 FNV collisions in the H8 catalog, 0 unaligned binary blobs, 0 unknown-endian binary manifests, optics `<e`, Sabine `<ff`/`<ffff`, lore endian `<`, and math audit tags for Beer-Lambert, Dalton, Sabine/Thorp/hydrostatic pressure.
Rejected Alternatives: Manual grep-only binary audit, unexplained-constant acceptance without verifier scripts, or running all verifiers in parallel after PowerShell/.NET pagefile failure.
Scalability potential: Toaster data uses stripped integer/binary lookups. High/Ultra consume verified extra fields and LUTs for visual overkill without changing simulation truth.
Hardware Impact: 0 us/frame runtime from this audit. Binary alignment and little-endian contracts reduce ingest copy/swap risk on low-end silicon.

## Decision 9 - H-Phi / Data Sovereignty

Problem: Economy proof must still fit the PROJECT_ATLAS 85-domain map and cannot add runtime private state.
Solution: Kept changes in offline `Tools/Economy` scripts and generated reports/tuned JSON. `VerifyDataInquisition.py` reports atlasDomains=85 and `DATA_INQUISITION_VERIFIED_STATIC_ONLY`. Data is consumed as stateless CSV/JSON/binary lookup evidence; no asmdef, C# API, GlobalRegistry dependency, or Unity object truth store was added.
Rejected Alternatives: Adding runtime loaders, changing C# contracts, or creating private MonoBehaviour state to prove a data problem.
Scalability potential: Low = uint16 cumulative weights only; Middle = display metadata; High = cluster/gradient/mica-glint/wet-soot extra data; Ultra = harmonic detail bands and resource scarcity highlight LUT samples.
Hardware Impact: 0 us/frame runtime. On i3/MX350, stripped data avoids extra string/visual metadata consumption; on high-end, extra fields buy richer resource inspection visuals.

## Decision 10 - Full Verify Sweep And Binary Scope Correction

Problem: The first DataTruth pass proved the economy slice but did not exactly match the production binary scan scope, and the user escalation required a broader proof sweep across math LUTs, binary hygiene, lore, hash catalogs, scalability payloads, and the 85-domain map.
Solution: Tightened `Tools/Economy/DataTruthInquisition.py` to use the same production binary inclusion/exclusion scope as `Tools/VerifyBinaryHygiene.py`, then re-ran the relevant root and nested `Verify*.py` scripts. That checkpoint reported status PASS, 39 binary blobs, 0 unaligned blobs, 0 unknown-endian blobs, and 0 big/mixed-endian blobs; the current Loop 15 readback supersedes the count with 43 production blobs and the same zero-failure hygiene result. `VerifyDataInquisition.py` and `VerifyNetSyncMerkleProtocol.py` both preserve the 85-domain atlas evidence.
Rejected Alternatives: Treating the earlier `Data/`-only binary scan as sufficient, claiming `VerifyReplayHasherReference.py` passed before the required `--xxhash-path` was supplied, or installing a new hash package outside the task boundary.
Scalability potential: Low/Toaster consumes stripped little-endian aligned binary and uint16 lookup payloads. Middle consumes metadata tables. High/Ultra consume verified extra visual fields, gradients, harmonic bands, and richer LUTs without adding runtime private state.
Hardware Impact: 0 us/frame runtime. Binary alignment and endianness evidence reduce SHINOBU ingest copy/byte-swap risk; on i3/MX350 this protects load cost, while high-end profiles keep optional visual-overkill data separated from baseline lookup truth.

## Decision 11 - Visual Constants And Sweep Freshness

Problem: The tuned economy JSON carried high-tier visual fields as naked Q16 values with retired names. The values were intended as visual-only payload, but without derivation they read like unexplained constants. The Metric Phi verifier also consumed a stale sweep report and failed on old cross-agent verifier state until the owner sweep was refreshed.
Solution: Changed the generator to emit `mica_glint_probability_q16 = round(0.015 * 65535)` and `wet_soot_overlay_weight_q16 = round(0.050 * 65535)`, added derivation strings for High and Ultra visual fields, and made the DataTruth lore audit fail if this agent's generated scalability payload contains retired visual terminology. Reran `Tools\RunMetricPhiVerifySweep.py` with the existing `%TEMP%\metric_phi_xxhash_ref` reference path, then reran `VerifyMetricPhiDataTruth.py`.
Rejected Alternatives: Leaving visual constants unexplained because they are not economy authority, editing the Metric Phi sweep report by hand, or accepting a nonzero optional replay-hasher row after the local xxhash reference was found.
Scalability potential: Toaster stays hash + uint16 weight only. Middle keeps display fields. High uses derived Q16 mica-glint and soot-overlay hints. Ultra uses derived power-of-two LUT lanes and harmonic visual bands. Authority remains deterministic weighted resource selection; High/Ultra only buy inspection visuals.
Hardware Impact: 0 us/frame runtime. The offline artifact now separates stripped low-end ingest from optional high-end visual data without forcing low-end clients to parse or store visual payload fields.

## Decision 12 - Direct Struct-Pack Audit And Wording Hygiene

Problem: Metric Phi already checked Python struct endianness, but the LOOT DataTruth report itself did not expose direct struct-pack evidence. The rationale/status/log trail also still contained stale style wording from the earlier debt description.
Solution: Added `audit_struct_pack_formats()` to `Tools/Economy/DataTruthInquisition.py`. It scans `Tools/**/*.py` for `struct.pack`, `struct.unpack`, `pack_into`, `unpack_from`, `calcsize`, and `Struct` format strings; it fails multi-byte formats without explicit endian prefixes and fails `>` / `!` outside approved external container contexts. Removed stale sterile wording from LOOT-owned status, rationale, log, and tool/test literals while keeping the audit term active through split-string construction.
Rejected Alternatives: Relying on Metric Phi as the only struct-pack evidence, or keeping historical sterile wording in audit files and arguing that it was only descriptive.
Scalability potential: Toaster ingest gets stronger proof: aligned blobs, manifest endian evidence, and Python format-site proof. High/Ultra keep optional visual payloads with derived Q16/power-of-two data and no private runtime state.
Hardware Impact: 0 us/frame runtime. This reduces Steam Deck/Quest byte-swap risk and SHINOBU ingest ambiguity without adding runtime parsing or allocation.

## Decision 13 - Crafting Monte Carlo Report Freshness

Problem: The full Metric Phi sweep later failed because `Data/Economy/Crafting_MonteCarlo_Audit.json` was stale against the active `Crafting_Costs.json`; this broke `EconomyValidator.py` even though `VerifyCraftingCosts.py` could validate the current binary/data shape. The stale report also undermined the recipe-loop proof demanded by the LOOT directive.
Solution: Reran `Tools\CraftingEconomyMonteCarlo.py --steps 1000000`, which regenerated the crafting exploit Monte Carlo report with the current `Crafting_Costs.json` SHA. Then reran `EconomyValidator.py`, `VerifyCraftingCosts.py`, `VerifyDataInquisition.py`, the full Metric Phi sweep, `VerifyMetricPhiDataTruth.py`, and LOOT DataTruth.
Rejected Alternatives: Editing the stale report hash by hand, limiting proof to the LOOT time-to-base simulation, or marking the global sweep failure as unrelated without repairing the data evidence it consumed.
Scalability potential: Toaster remains binary-minimal for crafting records; God-mode crafting payloads retain hydraulic arc / harmonic motor visual data while the resource-loop proof remains negative value/mass/energy deltas.
Hardware Impact: 0 us/frame runtime. This preserves stateless data lookup and prevents a stale economic exploit report from entering SHINOBU ingest as authoritative proof.

## Decision 14 - Bytecode-Free Compile And Audit Text Debt

Problem: A verification pass using plain `py_compile` failed with a Windows access-denied rename into `Tools\Economy\__pycache__`, and the LOOT-owned audit trail still exposed direct stale style-debt phrases in status/rationale/log text.
Solution: Re-ran compile with `python -B -m py_compile` to avoid bytecode writes, patched LOOT-owned artifacts and the DataTruth style-contract text, reran the LOOT unit tests, reran DataTruth, and verified the targeted stale-style literal scan returned no matches.
Rejected Alternatives: Deleting cache files in a shared multi-agent workspace, accepting a failed compile as a sandbox quirk, or leaving stale style terms in evidence logs while claiming the lore audit was clean.
Scalability potential: Low/Toaster remains stripped to deterministic lookup data. High/Ultra keep derived Q16/harmonic visual payloads without forcing low-end devices to parse presentation fields.
Hardware Impact: 0 us/frame runtime. The change is verification/tooling hygiene only; it reduces false-negative compile noise and keeps SHINOBU-facing audit text clean.

## Decision 15 - Verifier Throughput And Scope Reconciliation

Problem: The full self-validation loop exposed root verifier debt: DataTruth spent most of its time in full-repo binary traversal, `VerifyBinaryHygiene.py` and `VerifyH8HashCollisions.py` exceeded normal automation timeouts, and live binary count drifted while other agents were writing artifacts.
Solution: Replaced Path-based full-repo binary walks with pruned `os.scandir` traversal, cached hash-verifier file reads, bounded authored signal data scanning to the current Lore signal-authoring root confirmed by a direct field search, and added cheap substring gates before expensive C# regex passes. Hash math, FNV constants, LCG math, and runtime code were not changed.
Rejected Alternatives: Raising timeouts, deleting cache files, narrowing binary proof to LOOT-only data, or changing public/runtime code to hide verifier cost.
Scalability potential: Toaster data remains stripped, aligned, little-endian binary lookup. High/Ultra keep optional derived visual payloads. The verifier path now scales with production data roots instead of stat storms across excluded cache/scratch trees.
Hardware Impact: 0 us/frame runtime. Offline verifier wall time dropped enough for the key gates to return inside the normal automation window; SHINOBU ingest evidence now reports 43 production blobs, 0 unaligned, 0 endian failures, and 0 FNV collisions.

## Decision 16 - Stale Evidence Wording Repair

Problem: Status and CTO log entries from earlier loops still used current/latest wording around 39-binary and 28-command checkpoints after the workspace advanced to 43 production blobs and a 35-command sweep report. That made old evidence look like live evidence.
Solution: Reworded LOOT-owned status/rationale/log text so historical command outputs stay labeled as checkpoints and live claims point to Loop 15 authority: 43 production blobs, 0 misaligned, 0 unknown endian, 0 struct format failures, 1,018 hash records, 0 collisions, 35-command Metric Phi sweep PASS.
Rejected Alternatives: Rewriting old command outputs as if they had always seen 42 blobs, or leaving contradictory proof text because the latest JSON artifacts were already correct.
Scalability potential: Toaster and Ultra payload claims now read from one current evidence chain instead of mixed stale checkpoints, preserving stateless data sovereignty for SHINOBU ingest.
Hardware Impact: 0 us/frame runtime. The repair is audit hygiene only; it prevents stale binary-count claims from polluting low-end ingest decisions.

## Decision 17 - Recursive Sweep Self-Check Reconciliation

Problem: A direct readback briefly observed `METRIC_PHI_VERIFY_SWEEP.json` red while the sweep/data-truth self-check chain was being refreshed. The sweep includes `VerifyMetricPhiDataTruth.py`, which validates a self-check sidecar during the run and then writes the final report.
Solution: Reran `RunMetricPhiVerifySweep.py` with the xxhash reference path to completion and then read the final JSON artifacts after the process settled. Final authority is `VERIFY_SWEEP_PASS`, 35 commands, 0 required failures, `VerifyMetricPhiDataTruth` return code 0, and `DATA_TRUTH_VERIFIED` with 37 checks, 43 binaries, and 174 struct format sites.
Rejected Alternatives: Claiming the transient failed readback was a pass, removing the self-check from the sweep, or treating the replay hasher reference as optional after the user requested hard verification.
Scalability potential: Toaster/Ultra ingest proof now references the completed sweep artifact rather than a mid-run self-check state.
Hardware Impact: 0 us/frame runtime. This is verification ordering hygiene only; it reduces false red reports in automation without changing runtime data.

## Decision 18 - Live Binary Scope Drift

Problem: During verification, `Docs/AgentLogs/Dump_HEADLESS_SCENARIO_RUNNER.bin` entered the production binary scan. That moved current binary authority from 42 to 43 blobs and changed Metric Phi struct-format site count from 173 to 174.
Solution: Reran `VerifyBinaryHygiene.py`, `DataTruthInquisition.py`, and `VerifyMetricPhiDataTruth.py`. Current evidence reports 43 production blobs, 0 misaligned, 0 unknown endian, 0 struct endian failures, 37 Metric Phi checks, 0 failures, and 174 struct format sites.
Rejected Alternatives: Treating the new dump as out of scope without verifier evidence, or leaving current proof text at 42/173 after the filesystem changed.
Scalability potential: Toaster/Ultra data remains stateless; binary ingest authority now includes the crash-dump proof blob without weakening alignment/endian gates.
Hardware Impact: 0 us/frame runtime. This is live evidence reconciliation only; SHINOBU ingest proof now matches the actual filesystem.

## Decision 19 - Ore LCG Binary Re-Bake

Problem: The isolated Metric Phi sweep exposed real economy data debt: `VerifyOreLcgBaker.py` and `VerifyOreLcgBinaryIndependent.py` both rejected the minimal/toaster section of `Data/Economy/Ore_Distribution.h8bin` against the current JSON/source matrix.
Solution: Reran `Tools\OreLcgBaker.py` from `Data/Economy/Resource_Distribution_Matrix.csv`. The regenerated `Ore_Distribution.h8bin` is 1776 bytes, little-endian, 16-byte aligned, and both the baker-coupled and binary-independent verifiers pass.
Rejected Alternatives: Editing verifier thresholds, changing runtime C# code, or accepting a mismatched toaster payload because the Monte Carlo JSON still passed.
Scalability potential: Toaster data now has a verified minimal section; Ultra data still keeps visual-only records separate from spawn authority.
Hardware Impact: 0 us/frame runtime. This fixes binary ingest data only and removes a low-end cache mismatch.

## Decision 20 - Isolated Sweep Artifact

Problem: Global Metric Phi reports were being overwritten by other active Python sweep processes, including runs without the xxhash reference path. That made global JSON unstable even when LOOT-owned gates were green.
Solution: Patched `RunMetricPhiVerifySweep.py` to stabilize transient non-self failures before writing the self-check sidecar, then ran an isolated LOOT sweep to `Docs/Reports/LOOT_METRIC_PHI_VERIFY_SWEEP.json`. Final isolated sweep evidence: `VERIFY_SWEEP_PASS`, 35 commands, 0 required failures; LOOT data-truth sidecar: `DATA_TRUTH_VERIFIED`, 37 checks, 0 failures, 43 binaries, 274 struct format sites, 0 endian failures.
Rejected Alternatives: Killing other agents' Python jobs, using the global `METRIC_PHI_VERIFY_SWEEP.json` while it was actively contested, or reporting a pass from a stale green artifact.
Scalability potential: LOOT audit proof is now stateless and namespaced; SHINOBU can ingest the LOOT evidence without depending on shared global report timing.
Hardware Impact: 0 us/frame runtime. This is verification isolation only; it reduces evidence races in a multi-agent batch.
