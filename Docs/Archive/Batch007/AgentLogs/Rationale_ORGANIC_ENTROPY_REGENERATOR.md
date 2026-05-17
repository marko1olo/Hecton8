# Rationale_ORGANIC_ENTROPY_REGENERATOR

Status: PENDING VERIFICATION

## Decision 0001 - Prompt Source

Problem: `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="ORGANIC_ENTROPY_REGENERATOR">`; `C:\hades\Hecton8\Docs\Tasks\CURRENT_BATCH.md` is absent. The user supplied a direct override with ID and task.
Solution: Treat the direct override as one task, record the mismatch, and avoid reading neighboring prompts as authority.
Rejected Alternatives: Applying an unrelated prompt from CURRENT_BATCH.md would cross-contaminate domain decisions. Stopping would leave the explicit user task unhandled despite a usable direct directive.
Scalability potential: Low/Middle/High/Ultra unaffected at this stage; this is workflow hygiene.
Hardware Impact: 0 us runtime impact on i3/MX350.

## Decision 0002 - Mandate Set

Problem: 1000-day regrowth and nutrient simulation touches organic entropy, deterministic authority, memory, phase scheduling, AUP locality, and telemetry.
Solution: Follow PHYS_Destructible_Organic_Entropy, MATH_Deterministic_RNG_SlotMachine, OPT_Zero_GC_Policy_AllocFree_Mandate, OPT_Native_Memory_Collections_JobSystem_Protocol, ARCH_Execution_Phases, MATH_AUP_Determinism_Sync, DBG_Telemetry_Crash_Reporting_PostMortem, and ARCH_Global_Registry_ServiceLocator_DI_Init.
Rejected Alternatives: Loading every `.agents-skills` file would waste context and increase odds of irrelevant cross-domain leakage.
Scalability potential: Low uses reduced cadence and coarse cells; Ultra can spend saved cycles on richer visible biomass variation while preserving the same deterministic authority.
Hardware Impact: Design target remains below 100 us per simulation slice on i3/MX350; measured proof absent until compile/runtime verification.

## Decision 0003 - Data-First Implementation

Problem: The existing organic regrowth harness was 365-day, JSON-only, and lacked binary alignment, FNV collision proof, and SHINOBU ingest metadata.
Solution: Extend `Tools/WorldEntropySim.py` into a 1000-day baker, add `Data/Ecosystem/Organic_Entropy_Regrowth.json`, `Organic_Entropy_Regrowth.h8bin`, `Organic_Entropy_Regrowth.manifest.json`, summary JSON, and `Tools/VerifyOrganicEntropy.py`.
Rejected Alternatives: Adding a runtime MonoBehaviour would increase private state and violate Data Sovereignty. Leaving data as JSON would force runtime parsing and cache misses.
Scalability potential: Low samples every tenth day and uses static final-cell weights; Middle samples every fifth day; High samples every second day; Ultra samples all 1000 days and uses per-record visual hashes for harmonic bloom/overgrowth scars.
Hardware Impact: SHINOBU reads 195344 bytes of aligned cold data; runtime can use readonly offset lookup with 0 B/frame GC. Estimated low-end gain versus JSON parsing is entire parse cost removed from startup/hot paths; measured Unity proof absent.

## Decision 0004 - Scientific Basis And Visual Fake Boundary

Problem: Organic regrowth cannot be sold as hard science if constants are unexplained magic numbers, but simulating chemical particles would violate frame budget.
Solution: Use macro Fickian eddy diffusion for nutrient debt, Q10 temperature scaling as biological rate metadata, and Redfield C:N:P 106:16:1 as nutrient basis metadata. Keep gameplay truth as byte-lane macro state and sell detail through visual payload IDs.
Rejected Alternatives: Per-particle nutrient chemistry, per-organism truth, or raw placeholder constants. Those are either too slow or indefensible.
Scalability potential: Toaster gets stable detritus/stain fakes; Ultra gets harmonic biolum scars and overgrowth residue without changing gameplay truth.
Hardware Impact: Low-tier avoids per-cell dynamic chemistry; estimated savings are above 0.1 ms versus any per-particle/organism simulation on MX350-class hardware. Runtime measurement absent.

## Decision 0005 - Binary Layout

Problem: Runtime consumers need zero-cost cache ingest and deterministic identity.
Solution: Pack `H8OR` v2 as `<4s19I` header, 32-byte biome records, 16-byte day records, 16-byte final-cell records, and a 65536-byte apex respawn LUT. Every section and total size are 16-byte aligned; endian probe is `0x01020304`.
Rejected Alternatives: Variable-length records, text keys, or platform-endian writes. They create byte-swap/cache ambiguity.
Scalability potential: Low can skip dense day records by stride; Ultra can read every day record and visual hash.
Hardware Impact: 195344 bytes fits cold data cache strategy; no runtime allocation required after DataVault/MMF ingest.

## Decision 0006 - External Habitat Failure Boundary

Problem: Broad verifier pass found `VerifyHullStressBudget.py` failing on Habitat god-mode decal seed mismatches and economy-report proof-floor checks.
Solution: Record the boundary, then recheck through the Metric Phi sweep after other data changed. Current `Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json` records `VerifyHullStressBudget` PASS. No Habitat binary was mutated by this agent.
Rejected Alternatives: Fixing Habitat binary from an Ecology/Data agent would violate domain boundary and risk cross-agent conflict.
Scalability potential: None for ecology; this remained a verification-boundary issue.
Hardware Impact: 0 us ecology runtime impact.

## Decision 0007 - Replay Hasher Reference Debt

Problem: `VerifyMetricPhiDataTruth.py` required `VerifyReplayHasherReference` return code 0, but the replay verifier only accepted a temporary third-party `xxhash` package path. PyPI was unreachable from the shell, so the broad sweep could pass optional status while data-truth still failed.
Solution: Add embedded official XXH3-64 seeded sanity vectors from Cyan4973/xxHash `cli/xsum_sanity_check.c` as the default verifier path, keep `--xxhash-path` as the stronger external-package comparison mode, and mark the sweep row as required official-vector evidence.
Rejected Alternatives: Weakening `VerifyMetricPhiDataTruth` to accept missing optional evidence would turn a proof gap into a fake pass. Vendoring `xxhash` into the project would add third-party package contamination for a cold verifier.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; this is cold verification hygiene for deterministic save/replay hashes.
Hardware Impact: 0 us runtime impact on i3/MX350. Verification now avoids a network dependency and completes the replay reference row in about 12,769,000 us in the latest sweep artifact.

## Decision 0008 - Source Metadata Hardening

Problem: The generated organic entropy manifest contained SHINOBU binary layout and H-Phi stateless lookup proof, but the source constants JSON did not expose `binaryContract`, `hPhiAudit`, or explicit toaster/ultra extra-data payloads. That made the source file weaker than the generated artifact.
Solution: Add those fields to `Data/Ecosystem/Organic_Entropy_Regrowth.json`, propagate them through `Tools/WorldEntropySim.py`, and make `Tools/VerifyOrganicEntropy.py` fail if they are missing or drift from the actual binary struct formats.
Rejected Alternatives: Trusting the generated manifest alone would leave source-of-truth data incomplete. Adding runtime managers or private state to carry this metadata would violate Data Sovereignty.
Scalability potential: Toaster now has explicit stride/payload contract; Ultra now has explicit full-curve/per-cell extra-data contract for state hash, overkill noise, harmonic biolum scar, and overgrowth residue phase.
Hardware Impact: Runtime 0 us; metadata-only cold data. Low-end still consumes decimated offsets. High-end can consume dense records without changing gameplay truth.

## Decision 0009 - Full Verifier Coverage

Problem: The aggregate Metric Phi sweep was selective and missed verifier files on disk, including Dalton, Ore LCG, crafting source contracts, Tide inquisition, and Metric Phi data truth itself. Shared sweep artifacts were also being written by other agents, creating race-contaminated evidence.
Solution: Add all missing verifier scripts to `Tools/RunMetricPhiVerifySweep.py`, give `VerifyTideInquisition.py` a long timeout, and pass `--sweep-input` into `VerifyMetricPhiDataTruth.py` so custom sweep reports validate themselves. Run an agent-owned sweep at `Docs/Reports/ORGANIC_ENTROPY_VERIFY_SWEEP.json`.
Rejected Alternatives: Accepting direct one-off passes without adding sweep coverage would let the next audit regress. Trusting `Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json` would mix this agent's proof with other active writers.
Scalability potential: Runtime unchanged. Verification now covers toaster/rtx data gates across Dalton, tide, ore, crafting, optics, ecology, and data-inquisition reports.
Hardware Impact: Runtime 0 us. Offline verification wall time increased by design; the proof surface is broader and explicit.

## Decision 0010 - Macro Calibration Basis

Problem: The organic entropy constants had a physical basis for diffusion/Q10/Redfield, but gameplay-scale rates for growth, nutrient depletion, food web pressure, tombstone decay, and apex respawn could still be read as naked authored constants.
Solution: Add `macroCalibrationBasis` to `Data/Ecosystem/Organic_Entropy_Regrowth.json`, validate its formulas in `Tools/WorldEntropySim.py`, enforce the field in `Tools/VerifyOrganicEntropy.py`, and add a unit test that rejects drift between constants and declared macro-ecology calibration.
Rejected Alternatives: Pretending every macro gameplay rate is raw physics would be false. Leaving the constants unexplained would fail the data-truth audit. Moving the rates into a runtime manager would violate stateless lookup and add private state.
Scalability potential: Low still reads decimated final-cell/curve data; Middle/High increase curve density; Ultra consumes full 1000-day records plus harmonic visual hashes. Gameplay truth stays identical across tiers, only presentation payload density changes.
Hardware Impact: Runtime 0 us. This is cold-source validation and generated-manifest metadata. Low-end i3/MX350 avoids runtime calibration math; high-end machines can spend the saved budget on richer biolum residue, overgrowth stains, and harmonic noise.
