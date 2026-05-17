# Rationale_VISUAL_LOD_GRADE_ARCHITECT

Status: PENDING VERIFICATION
Domain: PRESENTATION/RENDERING

## Decision 1 - Missing XML Prompt

Problem: `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="VISUAL_LOD_GRADE_ARCHITECT">`, so the mandated XML task count cannot be extracted.

Solution: Record the mismatch and use the explicit one-line assignment found in the separate Docs/Tasks instruction list as the only fallback directive. The stable doc update stays limited to rendering scalability policy.

Rejected Alternatives: Editing `CURRENT_BATCH.md` would fabricate batch authority. Borrowing a neighboring prompt would violate strict parsing. Stopping without recording status would leave no audit trail.

Scalability potential: Low/Toaster remains MX350-safe; Middle keeps current doctrine; High/Ultra spend saved budgets on visible detail, longer LOD residency, and richer post/lighting only after budget proof.

Hardware Impact: Estimated runtime gain is 0 us because this is documentation. Expected downstream benefit on i3/MX350 is fewer accidental high-end feature leaks into the low tier; measured proof absent.

## Decision 2 - Stable Doc Location

Problem: The project already has a stable scalability contract. Adding a separate dated matrix would split authority and cause agents to drift.

Solution: Extend `Docs/ARCHITECTURE/SCALABILITY_MATRIX.md` with a visual-tier contract that maps Toaster, Low, Med, RTX, and God Mode behavior.

Rejected Alternatives: A report under `Docs/Reports` would be evidence-only, not policy. A chat-only matrix would be ignored by future agents. A new orphan doc would require index maintenance and increase search debt.

Scalability potential: Toaster uses baked AO, impostors, depth fog, shader LOD 100/0, and aggressive LOD bias. RTX uses half-res/quality-gated volumetrics, longer HLOD residency, higher texture residency, richer surface detail, and optional VRS only with capability proof.

Hardware Impact: i3/MX350 avoids Bloom, full volumetrics, VRS assumptions, heavy temporal upscalers, and high upload buffers. RTX-class hardware buys visual density without changing deterministic gameplay truth.

## Decision 3 - Matrix Boundaries

Problem: "RTX vs Toaster" can become a vague marketing phrase unless every row has budgets, load-shed, and fallback behavior.

Solution: Define concrete rows for render scale, LOD bias, HLOD distance, shader LOD, fog, lighting, shadows, materials, flora, VFX, post, occlusion, VRS, async upload, and demotion gates.

Rejected Alternatives: A single "Ultra = everything on" column was rejected because it ignores VRAM pressure, runtime spikes, and project rules against unbounded simulation.

Scalability potential: Low - stable presentation through cheap fakes. Middle - selective richer features. High - expanded draw distance and lighting. Ultra - visual overkill through density and surface response, not gameplay-state divergence.

Hardware Impact: Expected low-end benefit is avoidance of 0.1ms+ suspicious passes without proof. High-end benefit is visible upgrade budget spent on authored density and quality-gated rendering. Exact microseconds saved: PENDING PROFILER.

## Decision 4 - Physics-Derived Visual Data

Problem: The visual matrix carried constants that looked like authored values without a derivation trail. Under the data-truth audit, that is indistinguishable from placeholder data.

Solution: Add `physicsDerivation` to `Data/System/Visual_Scalability_Matrix.json` and enforce it in `Tools/VisualLodMatrixBaker.py`. Beer-Lambert values now derive from `T(d)=exp(-sigma*d)` and `d=-ln(T)/sigma` with sigma `[0.45, 0.12, 0.03]`. Volumetric density scales from turbidity NTU through an explicit Mie coefficient and Rayleigh ratio.

Rejected Alternatives: Leaving constants as comments was rejected because comments do not fail a build. Recomputing at runtime was rejected because this is presentation data and belongs in a boot-time cache. Full spectral simulation was rejected because HECTON-8 needs predictable cinematic fakes, not per-photon truth.

Scalability potential: Toaster receives cheap depth fog and baked AO driven by the same physical anchors. RTX/God Mode spends extra data on higher gradient resolution, harmonic noise, raymarch steps, and longer residency without changing gameplay truth.

Hardware Impact: i3/MX350 avoids runtime formula evaluation and JSON parsing in hot paths. High-end GPUs get richer visual response from the same stateless lookup keys. Exact measured microseconds: PENDING PROFILER.

## Decision 5 - Binary Cache Contract

Problem: SHINOBU ingestion cannot depend on free-form JSON, unknown endian, or unaligned records.

Solution: Add `Tools/VisualLodMatrixBaker.py` and `Tools/VerifyVisualLodMatrix.py`. The cache uses magic `H8VG`, little-endian `<` struct formats, a 64-byte header, 128-byte tier records, 64-byte extra-data records, 16-byte hash records, and total binary size 2016 bytes. Every section is 16-byte aligned.

Rejected Alternatives: CSV and ad hoc JSON were rejected because they require parsing and validation at the wrong layer. A packed variable-length blob was rejected because it complicates zero-copy reads and collision audits.

Scalability potential: Low/Middle/High/Ultra all become fixed records with deterministic offsets. God Mode can add overkill fields in the extra-data table while Toaster reads only the stripped tier record.

Hardware Impact: Boot-time loader can mmap or block-read stateless records. i3/MX350 avoids string lookup churn; RTX hardware gets additional visual fields without branching into private runtime state. Exact measured microseconds: PENDING PROFILER.

## Decision 6 - Data Truth Verification Sweep

Problem: The user explicitly required proof for math, economy loops, binary alignment, endian, hash collisions, lore consistency, atlas fit, and H-Phi. Partial visual-only verification would leave cross-domain data debt.

Solution: Run the root verifier sweep and fix one real artifact drift: `Tools/VerifyQuestDag.py` failed because the compiled quest DAG binary did not match graph-derived output, so `Tools/QuestCompiler.py` regenerated it before re-verification. Economy proof was advanced to 1,001,972 mined nodes with zero failures and no recipe cycles. H-Phi recalculation confirmed the 85-domain map and wrote the static score artifact.

Rejected Alternatives: Ignoring non-render verifier failures was rejected because data ingestion is shared infrastructure. Manually editing generated quest binary was rejected; the compiler is the owner.

Scalability potential: Stateless data artifacts improve Data Sovereignty because render tiers, VFX budgets, lore, quests, and economy tables can be verified independently before runtime wiring.

Hardware Impact: Low-end silicon benefits from prevalidated static tables and stripped tier records. High-end paths can consume extra visual density fields without forcing private state or per-frame discovery. Exact measured microseconds: PENDING PROFILER.

## Decision 7 - Remaining Verification Boundary

Problem: Static tools passed, but Unity runtime evidence is still absent.

Solution: Keep the status as PENDING VERIFICATION and separate code-review/data-proof from runtime proof. The missing evidence is Unity import, Frame Debugger, RenderDoc, Profiler, Memory Profiler, GCMonitor, and player-build validation.

Rejected Alternatives: Claiming completion from Python and static scans was rejected because AGENTS.md forbids runtime readiness claims from docs/static scans.

Scalability potential: The data contract is ready for ingestion tests, but visual quality and frame-time acceptance still require device-tier captures.

Hardware Impact: Estimated low-end gain remains architectural until measured on MX350-class hardware. Exact measured microseconds: 0 us measured; PENDING PROFILER.

## Decision 8 - Polish Mandate Absence

Problem: The status checklist reached 100 percent checked, which normally requires reading `<POLISH_MANDATE>` from the batch file.

Solution: Scan `Docs/Tasks/CURRENT_BATCH.md` for `<POLISH_MANDATE>` and `</POLISH_MANDATE>`. No tag was present, so no additional polish directive can be executed without fabrication.

Rejected Alternatives: Inventing a generic polish phase was rejected because the batch protocol requires tag-based authority.

Scalability potential: No effect on runtime data. The completed self-audit already covered binary hygiene, physics derivation, cross-tier fallbacks, hash collisions, and atlas fit.

Hardware Impact: 0 us runtime. No code path changed.

## Decision 9 - Babel Dictionary Drift Fix

Problem: The full `Verify*.py` sweep found a deterministic byte mismatch in `Tools/VerifyBabelDictionary.py`. The Babel dictionary blob did not match the current manifest-driven rebuild.

Solution: Run the owner tool `Tools/BabelCompiler.py`, which regenerated `Assets/_Project/Data/Localization/Babel_Dictionary.h8bin`, `Babel_Dictionary.manifest.json`, and `H8LocHashes.cs`. Post-fix verification reports 45 sources, 32,579 entries, 17 languages, 1,523,792 bytes, endian `<`, 16-byte alignment, and zero collision resolutions.

Rejected Alternatives: Manually editing the `.h8bin` was rejected because generated binary artifacts must be compiler-owned. Editing the verifier to accept stale bytes was rejected because it would destroy the deterministic rebuild guarantee. Ignoring localization as out-of-domain was rejected because the user requested project-wide data truth.

Scalability potential: Localization remains a single stateless binary lookup blob. Toaster-class hardware avoids runtime localization JSON parsing; high-end hardware receives the same deterministic keyspace without private state.

Hardware Impact: Runtime measurement absent. Expected benefit is preserving zero-GC localization lookup shape; exact measured microseconds remain PENDING PROFILER.

## Decision 10 - Full Verifier Rerun After Fix

Problem: A verifier pass with one failure is not evidence. The fix had to be revalidated against every discovered `Verify*.py` script, not just Babel.

Solution: Rerun 25 verifier commands after the Babel rebuild. Result: 25 passed, 0 failed. Then rerun `Tools/Economy/MonteCarloEconomySim.py` directly, producing 1,541,057 mined-node steps, p99 59.285 minutes, and zero failures. DataTruth and MetricPhi verifiers remained green, and `Tools/CalculateHPhi.py` rebuilt `Docs/PROJECT_ATLAS.md` with 85 domains and static H-Phi `6.7481e-05`.

Rejected Alternatives: Relying on stale reports was rejected. Running only visual-domain verifiers was rejected because binary/cache hygiene is shared data infrastructure.

Scalability potential: The matrix, Babel, optics, Sabine, VRAM, quest, lore, crafting, and economy data are independently validated as stateless or fixed-record artifacts. This increases Data Sovereignty because consumers do not need private mutable state to interpret tier records.

Hardware Impact: Low-end benefit is reduced boot/runtime parsing risk from fixed binaries and precomputed tables. High-end benefit is extra visual data fields without divergent gameplay state. Exact measured microseconds: 0 us measured; PENDING PROFILER.

## Decision 11 - Generated Unity Meta Hygiene

Problem: `Tools/BabelCompiler.py` produced Unity-facing generated files under `Assets/_Project/Data/Localization` and `Assets/_Project/Scripts/UI/Localization/H8LocHashes.cs`, but the generated file set lacked `.meta` companions. That would let Unity generate nondeterministic GUIDs on import.

Solution: Add deterministic `.meta` files for the localization folder, BabelMocks folder, `.json` sources, `.h8bin` blob, manifest, and generated C# hash file. Importer patterns match existing project conventions: folders and `.h8bin` use `DefaultImporter`, JSON uses `TextScriptImporter`, and the generated C# hash file follows the existing minimal `.cs.meta` style in the localization folder.

Rejected Alternatives: Waiting for Unity to generate GUIDs was rejected because this is shared data infrastructure. Hand-editing binary content was rejected; only metadata companions were added. Changing the Babel compiler to own meta generation was rejected in this pass because it is outside the visual matrix scope and could alter another agent's tool contract.

Scalability potential: Stable GUIDs reduce import churn for localization data consumers. Data remains stateless and fixed-binary.

Hardware Impact: 0 us runtime. This affects Unity import determinism only.

## Decision 12 - Current-State Final Verifier Sweep

Problem: Adding metas and rerunning Babel changed the current static tree. Verifier evidence taken before that point was stale.

Solution: Rerun `Tools/BabelCompiler.py`, `Tools/VerifyBabelDictionary.py`, `Tools/VerifyBabel.py --hash-audit`, `Tools/CalculateHPhi.py`, and then the full 25-command `Verify*.py` sweep. Final result: 25 passed, 0 failed; H-Phi still reports 85 domains and `6.7481e-05`.

Rejected Alternatives: Keeping the previous all-green sweep was rejected because the file graph changed after it. Running only the visual verifier was rejected because the data-truth request was project-wide.

Scalability potential: Current disk state now has fixed-record visual, localization, economy, audio, optics, VR, net-sync, and radio data passing their own verifiers.

Hardware Impact: 0 us measured. Runtime import/profiler evidence remains PENDING VERIFICATION.

## Decision 13 - MetricPhi Sweep Stale Evidence Repair

Problem: A fresh verifier pass exposed `Tools/VerifyMetricPhiDataTruth.py` failing. The failed check was not a binary alignment or endian issue; `Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json` had stale replay-hasher evidence recorded without `--xxhash-path`, so `VerifyReplayHasherReference` returned code 2 inside the sweep artifact.

Solution: Rerun `Tools/RunMetricPhiVerifySweep.py` with the explicit reference package path `C:\Users\User\AppData\Local\Temp\metric_phi_xxhash_ref`, then rerun `Tools/VerifyMetricPhiDataTruth.py`. The final sweep JSON reports `VERIFY_SWEEP_PASS`, `requiredFailures=0`, and replay hasher return code 0. The data-truth verifier then reports `DATA_TRUTH_VERIFIED`, 36 checks, 0 failures, 37 binaries aligned, 158 struct sites, and 0 endian failures.

Rejected Alternatives: Editing `METRIC_PHI_VERIFY_SWEEP.json` by hand was rejected because verifier evidence must be produced by the owner tool. Ignoring the failure as non-visual was rejected because the user requested project-wide data truth and H-Phi audit closure.

Scalability potential: The repair keeps the validation chain stateless and reproducible; MetricPhi now consumes a fresh sweep artifact instead of stale private report state.

Hardware Impact: 0 us runtime. Offline verifier evidence only; Unity runtime proof remains absent.

## Decision 14 - Current Economy Step Boundary

Problem: Direct Monte Carlo and DataTruth inquisition can rewrite the same economy report with different deterministic sampling windows while still exceeding the 1,000,000-step requirement.

Solution: Record both fresh observations honestly. Direct `Tools/Economy/MonteCarloEconomySim.py` produced 1,541,057 mined-node steps with zero failures. A later inquisition pass temporarily rewrote the current report to 1,078,223 mined-node steps, p99 59.150 minutes, and zero failures. The newest Loop 9 pass restored the current disk economy report to 1,541,057 mined-node steps, p99 59.285 minutes, and zero failures.

Rejected Alternatives: Keeping only the larger number was rejected because the current file on disk is the source of truth. Rerunning until the larger count returns was rejected because both values pass the million-step audit.

Scalability potential: Economy proof remains external to visual runtime and does not force private state into the visual matrix.

Hardware Impact: 0 us runtime. Offline simulation only.

## Decision 15 - Loop 9 Current-State Audit

Problem: Concurrent agents changed data artifacts after the prior all-green pass. Static counts drifted again: Babel now reports 32,604 records and PDA technical logs now report 58,880 bytes. Prior pass numbers could no longer be treated as current truth.

Solution: Rerun the complete offline validation chain from disk: visual bake, visual verify, focused tests, economy Monte Carlo, economy DataTruth, H-Phi, MetricPhi sweep, and the current verifier suite. Result: 32 audit commands, 0 accepted failures. MetricPhi sweep wrapper returned `4294967295`, but the final sweep JSON reports `VERIFY_SWEEP_PASS`, required failures 0, total commands 29, and replay hasher return code 0; `Tools/VerifyMetricPhiDataTruth.py` then passed, so the bound acceptance gate is green.

Rejected Alternatives: Reusing Loop 8 evidence was rejected because file counts changed. Treating the MetricPhi wrapper exit code alone as authoritative was rejected because the owner verifier consumes the generated JSON and passes; the contradiction is recorded as a tool-output risk.

Scalability potential: Visual matrix, VFX, VR, localization, audio, optics, crafting, quest, tide, upgrade, net-sync, noise, radio, and economy data remain stateless or fixed-record artifacts with Toaster and overkill paths validated by their owners.

Hardware Impact: 0 us runtime. Offline data verification only; Unity import and profiler proof remain absent.

## Decision 16 - MetricPhi Process Exit Hardening

Problem: Loop 9 recorded a contradiction: `Tools/RunMetricPhiVerifySweep.py` produced a passing `METRIC_PHI_VERIFY_SWEEP.json` and `VerifyMetricPhiDataTruth.py` accepted it, but the wrapper process surfaced `4294967295` to the caller. That leaves automation unable to distinguish a real failed verifier from a late Python process teardown fault.

Solution: Keep the owner verifier logic intact and harden only the process-exit handoff. After `main()` completes, the wrapper now flushes stdout/stderr and calls `os._exit(exit_code)`, so the OS process status matches the already-atomically-written verifier result. Re-ran py_compile, the full MetricPhi sweep, standalone MetricPhi data-truth, visual bake/verify, binary hygiene, focused tests, direct economy Monte Carlo, economy DataTruth, and H-Phi.

Rejected Alternatives: Hand-editing the sweep JSON was rejected because evidence must be produced by owner tools. Ignoring the bad process status was rejected because SHINOBU-style ingestion and CI need reliable exit codes. Rewriting the sweep architecture was rejected because the report schema and verifier command set were already passing.

Scalability potential: The change increases Data Sovereignty by making the offline validation command stateless and machine-readable again. Toaster and RTX paths consume the same fixed-record data; the fix affects only audit automation, not runtime visual state.

Hardware Impact: 0 us runtime. Offline verifier wrapper only. Current rebind evidence: MetricPhi sweep exit code 0, commands 34, required failures 0; MetricPhi data truth 36 checks, 0 failed; binary hygiene 41 binaries, 0 misaligned; economy DataTruth 1,541,057 Monte Carlo steps, 0 cycles, 0 FNV collisions; H-Phi 85 domains, static score `6.7481e-05`. Unity import/profiler proof remains absent.

## Decision 17 - PDA Tone Debt Repack

Problem: The lore/tone audit found one content phrase, `shiny tank label`, in `TECH_05`. The formal validator did not fail it, but under the user's NASA-Punk Noir constraint it reads too clean for a field technical log.

Solution: Edit the authoritative generator `Tools/BuildPdaTechnicalLogs.py`, changing the phrase to `unscarred tank label`. Regenerate `Data/Lore/PdaTechnicalLogs.h8jsonl` and `Data/Localization/en_US.json`, repack `Data/Lore/PdaTechnicalLogs.h8bin` and manifest through `Tools/PackPdaTechnicalLogs.py`, rebuild Babel through `Tools/BabelCompiler.py`, then rerun PDA, Babel, lore, binary, economy, H-Phi, MetricPhi sweep, and MetricPhi data-truth verifiers.

Rejected Alternatives: Editing generated JSONL only was rejected because the next generator run would revert it. Ignoring the phrase was rejected because the user explicitly called out sterile tone debt. Editing binary artifacts by hand was rejected because PDA/Babel data must remain owner-tool generated.

Scalability potential: The text remains a stateless hash/offset lookup. Low tier still reads compact PDA records and fixed binary rows; high/Ultra retain the same extra visual records without private runtime state.

Hardware Impact: 0 us runtime. The PDA binary is now 59,120 bytes, little-endian, 16-byte aligned, and collision-free. Babel is 1,529,088 bytes, little-endian, 16-byte aligned, and collision-free. Full MetricPhi sweep now reports 35 commands, required failures 0, and `selfCheckPending=False`; MetricPhi data-truth reports 37 checks, 0 failures, 42 aligned binaries, 167 struct sites, and 0 endian failures.

## Decision 18 - MetricPhi Canonical Report Hygiene

Problem: `RunMetricPhiVerifySweep.py` still wrote the provisional `selfCheckPending=True` payload to the canonical `METRIC_PHI_VERIFY_SWEEP.json` before running `VerifyMetricPhiDataTruth`. If the process was interrupted or the sidecar cleanup failed, downstream consumers could ingest incomplete evidence. A stale `METRIC_PHI_VERIFY_SWEEP.selfcheck.*.json` file was observed under `Docs/Reports`.

Solution: Write the provisional payload only to the self-check sidecar, never to the canonical report. Add startup/final cleanup of `METRIC_PHI_VERIFY_SWEEP.selfcheck.*.json` sidecars and return code 3 if cleanup fails. Add `Tools/test_metric_phi_verify_sweep.py` to enforce no canonical provisional write, final `selfCheckPending=False`, and stale sidecar removal.

Rejected Alternatives: Leaving provisional canonical writes was rejected because it already produced a false failed state. Silently ignoring leftover sidecars was rejected because cache hygiene requires clean ingestion boundaries. Relying only on the long sweep was rejected because handoff behavior needs a fast regression test.

Scalability potential: This improves Data Sovereignty at the evidence layer: consumers see either the last complete canonical sweep or a new complete canonical sweep, never an incomplete private intermediate state.

Hardware Impact: 0 us runtime. Offline Python tool only. Verification after the fix: focused sweep handoff test passed, full MetricPhi sweep passed with 35 commands and 0 required failures, no `Docs/Reports/*selfcheck*.json` files remain, MetricPhi data-truth reports 37 checks/0 failures/43 aligned binaries/175 struct sites/0 endian failures, BinaryHygiene reports 43 binaries/0 misaligned.

## Decision 19 - VISUAL-Owned MetricPhi Evidence Rebind

Problem: The shared canonical `Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json` was overwritten during concurrent agent work and captured stale failures: `VerifyOreLcgBaker`, `VerifyOreLcgBinaryIndependent`, and `VerifyMetricPhiDataTruth`. The Ore binary on disk had already been rebaked from the failed 1,632-byte artifact to a current 1,776-byte artifact, so the canonical failure no longer represented current data.

Solution: Stop only the stale verifier process tree started by this agent, verify current Ore artifacts directly, then run `Tools/RunMetricPhiVerifySweep.py` into VISUAL-owned AgentLogs paths with explicit `--xxhash-path`. Bind the result with standalone `Tools/VerifyMetricPhiDataTruth.py` using the matching VISUAL-owned sweep, H-Phi score, atlas, and graph artifacts.

Rejected Alternatives: Hand-editing canonical MetricPhi JSON was rejected because verifier evidence must be owner-tool generated. Killing other agents' MetricPhi processes was rejected because the batch is parallel. Trusting the stale canonical failure was rejected because current disk Ore artifacts now pass both owner and independent verifiers.

Scalability potential: The evidence layer is stateless again for this agent: consumers can read VISUAL-owned sweep/data-truth artifacts without depending on private process state or shared report timing. Toaster and RTX records remain fixed binary/data lookups.

Hardware Impact: 0 us runtime. Offline verifier/reporting only. Current evidence: Ore owner verifier PASS at 1,776 bytes; independent Ore binary verifier PASS at 1,776 bytes; BinaryHygiene PASS with 43 binaries/0 misaligned; VISUAL-owned MetricPhi sweep PASS with 35 commands/0 required failures/no transient retries; VISUAL-owned MetricPhi data-truth PASS with 37 checks/0 failures/43 aligned binaries/274 struct format sites/0 endian failures.

## Decision 20 - Loop 14 Rebound Under Renewed Inquisition

Problem: The user demanded another proof pass after prior evidence had already been produced. In a parallel batch, older proof can be stale even when the visual matrix itself has not changed.

Solution: Rerun the full VISUAL-owned MetricPhi sweep into Loop 14 AgentLogs paths, then bind the exact Loop 14 sweep with standalone `VerifyMetricPhiDataTruth.py`. Rebound the visual matrix baker/verifier, binary hygiene, focused regression tests, and economy DataTruth from current disk.

Rejected Alternatives: Trusting the previous VISUAL-owned CURRENT sweep was rejected because the user explicitly requested a fresh inquisition. Reading the shared canonical MetricPhi report was rejected because concurrent agents can overwrite it. Hand-editing proof JSON was rejected because evidence must be owner-tool generated.

Scalability potential: The visual tier contract remains stateless: Toaster reads stripped fixed records; God Mode reads extra records for high-res gradients, harmonic noise, and longer visual residency without private runtime state.

Hardware Impact: 0 us runtime. Offline verification/reporting only. Current Loop 14 evidence: MetricPhi sweep PASS with 35 commands/0 required failures/`selfCheckPending=False`; MetricPhi data-truth PASS with 37 checks/0 failures/44 aligned binaries/274 struct format sites/0 endian failures; visual matrix PASS at 2016 bytes/aligned16/little-endian/0 hash collisions/God Mode ratio 9.097; binary hygiene PASS with 44 binaries/0 misaligned; economy DataTruth PASS with 1,078,223 Monte Carlo steps/0 FNV collisions/0 recipe cycles/0 endian or alignment failures.
