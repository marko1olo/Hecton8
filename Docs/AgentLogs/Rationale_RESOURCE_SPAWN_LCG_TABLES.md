# RESOURCE_SPAWN_LCG_TABLES Rationale

Status: `VERIFIED MASTER GRADE - STATIC_SOURCE ONLY`
Evidence boundary: `STATIC_SOURCE` / `CLI_COMPILE` / Python unit tests. Unity runtime, profiler, GCMonitor, player build, and scene wiring remain `PENDING VERIFICATION`.

## Mandates Followed

- Data-oriented resource SOA: offline tables must produce contiguous, numeric resource rows and avoid runtime ScriptableObject dependence.
- Deterministic RNG slot-machine law: rolls derive from explicit seed inputs and use integer weights plus integer thresholds.
- Zero-GC policy: runtime-facing contract must avoid strings, floats for authority, and managed random state.
- Performance budget: runtime evaluation must be cold table lookup plus integer arithmetic, not per-node probability math.
- AUP determinism: spawn authority is keyed by stable IDs/chunk/AUP-like IDs, never presentation `Transform.position`.
- Evidence filter: generated data and CLI output are not Unity runtime proof.

## Decisions

### Decision 1: Reuse Existing LCG Constants

Problem: Ore spawning needs deterministic probability matrices without introducing a second RNG contract.
Solution: Use the existing Numerical Recipes LCG constants already mirrored in `Tools/Economy/MonteCarloEconomySim.py`: `a=1664525`, `c=1013904223`, `m=2^32`. This supports uint overflow and power-of-two masking.
Rejected Alternatives: `System.Random` / Python `random` are stateful and banned for authority. PCG/xoroshiro would require new runtime implementation and cross-agent API drift.
Scalability potential: Low uses one LCG step and 8-bit packed thresholds; Middle uses cumulative u16; High/Ultra can spend saved CPU on visual cluster dressing after the gameplay result is chosen.
Hardware Impact: Estimated gain for i3/MX350 is avoiding managed RNG and float distribution scans in runtime hot paths; static estimate only, no profiler proof.

### Decision 2: Keep Bake Output in `Data/Economy`

Problem: Prompt domain is `Data/Economy/`, while a Python tool must live in `Tools`.
Solution: Place the executable baker at `Tools/OreLcgBaker.py` and generated JSON/CSV artifacts under `Data/Economy/`, with the C# template documented under `Docs/AgentLogs/`.
Rejected Alternatives: Writing runtime C# structs directly would cross into integration ownership and risk compile churn under concurrent agents.
Scalability potential: JSON/CSV stays source-controlled and can be consumed by later binary/DataVault bakes without Unity asset mutation.
Hardware Impact: No runtime cost from this tool; runtime consumers get integer tables ready for Burst-friendly packing.

### Decision 3: Validate Exact 50% at Matrix Level

Problem: A finite 100,000-roll LCG sample can drift by normal sampling noise, so exact sample count is not a reliable authored data guarantee.
Solution: Encode Safe Shallows as `Titanium=255`, `Total=510`, producing exactly `5000` basis points in the authoritative matrix. The histogram remains finite-sample evidence and records its delta separately.
Rejected Alternatives: Searching seeds for an exact 50,000-count window was expensive and brittle. Antithetic/complement sampling would prove the sampler instead of the actual LCG stream.
Scalability potential: Low/Middle tiers use the exact byte matrix. High/Ultra tiers can add richer visual cluster dressing after the selected resource hash is deterministic.
Hardware Impact: Exact integer matrix removes runtime percentage math; estimated gain remains STATIC_SOURCE until profiler proof exists.

### Decision 4: Biome-Level Clumping Factor

Problem: The task asks for clumping probability, but simulating ore-near-ore truth in the runtime spawn path would invite neighbor scans.
Solution: Bake one `clumping_factor_u8` per biome. Runtime can use it as a cheap deterministic second roll after the first resource selection.
Rejected Alternatives: Physics overlap checks, per-node local density fields, and neighbor list generation were rejected as unnecessary for an authored probability table.
Scalability potential: Low uses a single byte threshold. Middle/High can increase cluster visual dressing density. Ultra can add visual-only vein decals without changing resource authority.
Hardware Impact: Expected i3/MX350 impact is avoiding local spatial searches during spawn placement; estimate is STATIC_SOURCE, not profiler proof.

### Decision 5: Markdown Struct Handoff, No Runtime Mutation

Problem: The prompt asks for a C# unmanaged struct template, but implementing runtime consumers would change another agent's integration surface.
Solution: Generate a markdown handoff with `[StructLayout(LayoutKind.Sequential, Pack = 4)]` structs and a multiply-high selection snippet.
Rejected Alternatives: Creating new C# files in `Assets/_Project/Scripts` was rejected because the task is data/economy bake ownership, not runtime SHINOBU ownership.
Scalability potential: Low uses `OreLcgResourceRecord` byte/ushort reads. Ultra can add post-selection visual-only variation without changing these records.
Hardware Impact: The template prevents managed references in the intended runtime data path; hardware gain remains a static design estimate until integrated and profiled.

### Decision 6: SHINOBU Binary Cache Added After User Escalation

Problem: JSON/CSV are correct source artifacts but not zero-cost for SHINOBU ingestion.
Solution: Add `Data/Economy/Ore_Distribution.h8bin` with explicit little-endian `struct.pack("<...")`, 64-byte header, 16-byte aligned sections, CRC32, and SHA-256 cross-reference in JSON.
Rejected Alternatives: Leaving SHINOBU to parse JSON at runtime was rejected after the binary/cache hygiene escalation. Raw ad hoc binary writing without a header was rejected because Steam Deck/Quest byte order and offset proof would be hearsay.
Scalability potential: Low reads the minimal section: density bytes, clump bytes, total weights, and the flat U8 weight matrix. Ultra reads the visual section for deterministic vein gradients and harmonic noise after gameplay selection.
Hardware Impact: On i3/MX350, runtime startup can memcpy 1776 bytes into an unmanaged table instead of parsing JSON. Microsecond gain remains static until startup profiling.

### Decision 7: Replace Weak Density/Clump Formulas

Problem: The first pass used authored scaling constants without enough physical provenance.
Solution: `base_density_u8` is now source-matrix total weight scaled across the 10 authored biomes; `clumping_factor_u8` is hydrostatic mid-depth pressure scaled across the same biomes using `rho=1025 kg/m^3`, `g=9.807 m/s^2`, and `surface=101325 Pa`. The byte floors/ceilings are derived from Q8 headroom, not loose literals: density reserves 1/8 of the 256-step byte domain (`[32,224]`), while clump reserves 1/16 (`[16,240]`) so Ultra visual dressing can use higher-frequency variation without saturating gameplay authority.
Rejected Alternatives: Beer-Lambert, Dalton, and Sabine were rejected for ore spawn authority because those govern optics, gas physiology, and acoustics, not geology/economy resource distribution. Pretending otherwise would be false physics.
Scalability potential: Low uses the derived byte values. Ultra uses deterministic visual gradients/harmonics for richer ore-vein dressing without changing resource authority.
Hardware Impact: Runtime stays integer-only. The physics context is baked offline; zero per-spawn pressure math is required.

### Decision 8: Lore Blob Drift Repaired By Project Tool

Problem: `VerifyLore.py --check` failed on `Docs/Lore/Archives/DeepReach_ColonyFailureArchive.md`.
Solution: Re-baked lore with `VerifyLore.py --bake --check`, then re-ran `VerifyLore.py --check` successfully. This used `LorePacker`'s 16-byte aligned little-endian blob writer.
Rejected Alternatives: Manual binary patching was rejected. Hand-editing the lore Markdown was outside this data bake unless terminology defects were proven.
Scalability potential: Lore blob remains single-hash lookup plus raw byte slice.
Hardware Impact: Existing lore blob now verifies at 16-byte alignment; no new gameplay runtime cost.

### Decision 9: Evidence Boundary Held

Problem: The polish mandate says `VERIFIED MASTER GRADE`, but project evidence rules forbid runtime claims without Unity/profiler artifacts.
Solution: Mark status `VERIFIED MASTER GRADE - STATIC_SOURCE ONLY` and record exact command artifacts. Unity import, Play Mode, profiler, GCMonitor, player build, and scene wiring remain `PENDING VERIFICATION`.
Rejected Alternatives: Claiming runtime `0 GC` or frame-time gains from Python/static output was rejected.
Scalability potential: Current data supports Low/Middle/High/Ultra fields; runtime owners must still wire DataVault ingestion.
Hardware Impact: Static architecture improved for SHINOBU ingestion, but runtime hardware proof is still absent.

### Decision 10: Re-run Data Truth Inquisition Instead Of Trusting Prior Logs

Problem: The second escalation required proof from current files, including all `Verify*.py` scripts and a clean H-Phi audit.
Solution: Re-ran the ore bake at `1,000,000` iterations, unit tests, economy validator, recipe graph audit, hash collision verification, lore verification, binary hygiene, data inquisition, metric-phi data truth, and all runnable `Tools\Verify*.py` checks. Invocation-only failures were corrected with required arguments: `VerifyLore.py --check` and `VerifyReplayHasherReference.py --xxhash-path %TEMP%\h8_xxhash_ref --fuzz-count 128`.
Rejected Alternatives: Reporting previous command output as current proof was rejected. Treating usage errors as failed data was also rejected; the correct response was rerun with the verifier's declared CLI contract.
Scalability potential: The ore table remains a stateless binary lookup for Low/Middle, with Ultra visual-only gradients and harmonic noise after resource authority is chosen.
Hardware Impact: No runtime code changed. Startup/runtime gains remain static estimates until the Data Monolith owner wires the `.h8bin` into DataVault and profiles on target hardware.

### Decision 11: H-Phi Did Not Improve From This Offline Bake

Problem: User asked whether Data Sovereignty increased.
Solution: Ran `CalculateHPhi.py` to clean exit. The atlas still reports `DataSovereignty=0.019743027`, `MemoryAlignment=0.516657853`, `BinarySafeRatio=0.018508726`, and `HPhiStatic=6.7481e-05`. This data bake supports stateless lookup, but it did not change runtime source ownership counts because no DataVault integration code was added.
Rejected Alternatives: Claiming increased H-Phi from JSON/binary existence alone was rejected by the evidence mandate.
Scalability potential: Data-side Low/Ultra paths are present. Global architecture score requires runtime owners to replace local native allocations and concrete references with DataVault handles and typed lanes.
Hardware Impact: Static-only. No Unity import, Play Mode, profiler, GCMonitor, or player build evidence exists for a hardware delta.

### Decision 12: Add Ore-Specific Verifier Gate

Problem: The ore byte audit existed as inline command evidence, which is repeatable only by copying shell snippets.
Solution: Added `Tools\VerifyOreLcgBaker.py`. It verifies the JSON schema, source matrix hash parity, exact Safe Shallows Titanium basis points, 1,000,000 simulation metadata, LCG constants, Q8 byte range derivation, binary header/endian/alignment/CRC/SHA, JSON-to-binary record mirroring, histogram shape, industrial alias tone, atlas domain fit, and H-Phi artifact. It writes `Docs\AgentLogs\VerifyOreLcg_RESOURCE_SPAWN_LCG_TABLES.json`.
Rejected Alternatives: Leaving verification as an ad hoc inline script was rejected because future agents would have no stable gate. Weakening the verifier after it caught a provenance wording defect was rejected; the baker was fixed instead.
Scalability potential: The verifier locks the low-tier stateless lookup and Ultra visual-only fields to the same binary artifact, so future tuning drift cannot silently remove toaster or overkill paths.
Hardware Impact: No runtime cost. The benefit is earlier static failure before a bad table reaches Data Monolith ingestion.

### Decision 13: Fix Hydrostatic Provenance Wording

Problem: `VerifyOreLcgBaker.py` failed because `science_basis.hydrostatic_pressure` carried the formula but not the explicit hydrostatic label.
Solution: Changed the generated string to `hydrostatic_pressure_pa = 101325 + depth_m * ((1025 kg/m^3 * 9.807 m/s^2) rounded to integer Pa/m)` and re-baked. The verifier now passes.
Rejected Alternatives: Teaching the verifier to accept an unlabeled pressure formula was rejected; data provenance must be self-explanatory in the artifact.
Scalability potential: Low-tier and Ultra consumers can inspect one JSON source artifact and understand that pressure-derived clump is a baked proxy, not runtime physics.
Hardware Impact: No runtime cost. It prevents misusing the table as a live pressure solver later.

### Decision 14: Add Independent Binary Verifier

Problem: `VerifyOreLcgBaker.py` is a useful gate, but it imports `OreLcgBaker.py`, so part of the verification shares constants and struct formats with the writer.
Solution: Added `Tools\VerifyOreLcgBinaryIndependent.py`. It does not import the baker. It independently declares `H8OL`, `<4sHH14I`, `<IHHBBBBI`, `<IHBB`, `<IHHHHI`, FNV-1a UTF-16LE, LCG constants, offsets, and policy checks, then compares binary records against JSON/CSV.
Rejected Alternatives: Treating writer-backed verification as enough was rejected. Rewriting runtime C# integration was rejected because this agent owns data bake, not Data Monolith ingestion.
Scalability potential: Low-tier minimal section and Ultra visual section are independently checked in one pass, preventing future tuning drift from breaking toaster or God-Mode consumers.
Hardware Impact: No runtime cost. The static gate reduces risk that SHINOBU maps the wrong struct layout on Steam Deck/Quest.

### Decision 15: Resolve Verify Sweep False Negatives Before Reporting

Problem: The broad `Verify*.py` sweep initially produced false negatives from sandbox/ACL/tooling, plus one transient external data failure that passed on direct rerun.
Solution: EconomyValidator was rerun with approved escalation for temp-directory access. AI navigation was rerun with approved escalation for locked binary read access. The workspace-local `xxhash` reference package ACL was repaired and replay verification passed. A final escalated 33-script `Verify*.py` sweep completed with `VERIFY_FAILURE_COUNT 0`.
Rejected Alternatives: Ignoring failures because they were outside `Data/Economy` was rejected for the global inquisition. Editing other domains was also rejected because direct reruns proved no current data defect in AI/Habitat, and domain boundaries forbid unnecessary cross-domain mutation.
Scalability potential: Global data gate evidence now includes the ore verifiers alongside other binary/LUT systems, so the 85-domain map remains coherent.
Hardware Impact: Static-only. No Unity runtime or target hardware proof was produced.

### Decision 16: Strengthen Toaster Binary And Repair Global Binary Hygiene

Problem: The JSON toaster contract named `weight_matrix_u8_flat`, but the fastest binary minimal section initially carried only density, clump, and total weights. A later full sweep also exposed non-ore binary hygiene debt: the lore blob had trailing bytes, `Data/Balance/Baked/Babel_Dictionary.h8bin` was 1284 bytes, and the hygiene verifier incorrectly counted `.binlog` diagnostic evidence as game binary.
Solution: Expanded the ore minimal LOD payload to `density_u8[10]`, `clump_u8[10]`, `total_weight_u16[10]`, `weight_u8[150]`, then 16-byte padding. Re-baked `Ore_Distribution.h8bin` to 1776 bytes with minimal LOD bytes `192`, minimal payload bytes `190`, ultra offset `1616`, CRC `2957493204`, and SHA-256 `60f9a95ec619b4c9b7c168a01ac308415190df44b545fb1f722a11e983709c06`. Re-baked lore through `VerifyLore.py`, padded the balance Babel dictionary to `1296` bytes with header length/CRC updated, and narrowed binary hygiene scanning to actual `.bin/.h8bin` suffixes.
Rejected Alternatives: Leaving toaster consumers to scan full resource records was rejected because the binary minimal section should be a stripped ingest lane. Ignoring non-ore binary failures was rejected because the user's binary hygiene requirement was global. Deleting or reverting unrelated concurrent-agent work was rejected.
Scalability potential: Low/Celeron/i3 can read the minimal LOD section as a compact stateless table. High/Ultra still consume deterministic visual-only gradient and harmonic fields after resource authority is chosen.
Hardware Impact: Static-only. Binary ingest is now more direct for low hardware, but Unity startup/profiler/GCMonitor proof remains absent. H-Phi runtime Data Sovereignty remains `0.019743027`; this pass did not add DataVault runtime ownership.

### Decision 17: Fix Metric Phi Sweep Self-Check Contract

Problem: A final manual `Verify*.py` sweep exposed current-disk verifier debt outside the ore table. `VerifyMetricPhiDataTruth.py` had been tightened to require exactly `35` sweep commands even when `RunMetricPhiVerifySweep.py` intentionally invokes it against a pre-final self-check report with `selfCheckPending=true` and `34` completed commands. That false failure poisoned `Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json`, which then made `VerifyQuestDagDataTruth.py` fail its H-Phi report check.
Solution: Patched `Tools\VerifyMetricPhiDataTruth.py` so the command-count check accepts `35` commands for final reports, or `34` commands only when the sweep payload explicitly declares `selfCheckPending=true`. Required failures still must be zero. Reran the canonical sweep, standalone Metric Phi data truth, standalone Quest DAG data truth, and the full 33-script `Verify*.py` enumeration.
Rejected Alternatives: Suppressing `VerifyMetricPhiDataTruth.py`, editing reports by hand, or lowering `requiredFailures` tolerance was rejected. The defect was in the verifier state model, not in ore data or Quest DAG binary data.
Scalability potential: The offline verification lane now supports both the runner's self-check and final report state without weakening the final 35-command evidence gate. Low/Ultra data paths remain stateless binary/JSON lookup artifacts.
Hardware Impact: Static-only. No Unity runtime code changed; the gain is preventing false-failed evidence from blocking SHINOBU data ingestion review. Runtime proof remains absent.

### Decision 18: Re-run Loop 11 Data Truth Without Reclassifying Ore Physics

Problem: The renewed escalation demanded another full data-truth pass and specifically named Beer-Lambert, Dalton, and Sabine. Those laws are hard-science authorities for optics, gas physiology, and acoustics; applying them to ore spawn probability would be fake physics.
Solution: Re-baked the ore LCG data at `1,000,000` iterations and retained the ore science boundary: resource probability comes from the authored economy matrix; ore clumping is a hydrostatic-pressure-derived byte proxy using `rho=1025 kg/m^3`, `g=9.807 m/s^2`, and surface pressure `101325 Pa`; Beer-Lambert/Dalton/Sabine are explicitly not used for ore authority. Then reran the owner verifiers for optics, Dalton gas toxicity, Sabine acoustics, and Snell refraction to prove those physics LUTs are intact in their own domains.
Rejected Alternatives: Inventing Beer-Lambert/Dalton/Sabine terms in ore generation was rejected as false science. Editing runtime DataVault ingestion was rejected because this agent owns the offline `Data/Economy` bake, and runtime sovereignty needs a separate integration owner.
Scalability potential: Low/Celeron/i3 still uses the minimal LOD binary section with density, clump, totals, and 150 flat weights. Ultra still uses deterministic visual-only seed, gradient, and harmonic noise fields after resource authority is chosen.
Hardware Impact: Static-only. `Ore_Distribution.h8bin` remains 1776 bytes, 16-byte aligned, little-endian, and stateless lookup-ready. H-Phi did not increase runtime Data Sovereignty; latest score remains `0.019743027`, because no runtime DataVault code changed.
