# Rationale_DALTON_GAS_TOXICITY_TABLES

## Decision 001 - Missing Batch Prompt

Problem: The requested Prompt ID `DALTON_GAS_TOXICITY_TABLES` is not present in `Docs/Tasks/CURRENT_BATCH.md`, so the authoritative XML task list and task count cannot be extracted.

Solution: Preserve the missing-XML defect in status and proceed only because the user issued an explicit standalone continuation directive. The produced artifact is therefore not falsely tied to a batch XML block.

Rejected Alternatives: Using adjacent `O2_CONSUMPTION_STRESS_MODEL` or any other neighboring prompt was rejected because the batch protocol says to ignore neighboring prompts. Pretending the task count was known was rejected.

Scalability potential: The standalone artifact provides Low/Middle/High/Ultra lookup modes in its manifest. Low uses coarse nearest depth buckets; Middle uses linear lookup; High/Ultra spend saved simulation time on visual/audio warning fields while preserving identical gameplay truth.

Hardware Impact: 0 us measured runtime impact. No Unity hot-path code was added. Cold-load binary lookup replaces runtime recomputation potential, but profiler proof is absent.

## Decision 002 - Replace Raw 5-Float Dalton Table

Problem: The existing gas toxicity binary was a raw 40020-byte table, `mod16=4`, with only ambient/O2/N2/O2-toxicity/narcosis fields. It failed SHINOBU alignment and omitted CO2 and hypoxia curves.

Solution: Add `Tools/DaltonGasToxicityBaker.py` and rebuild `Data/Precomputed/dalton_gas_toxicity.bin` as a 128128-byte aligned binary: 64-byte `H8GT` header and 2001 rows of 16 little-endian float32 values. Each row is 64 bytes.

Rejected Alternatives: Padding the old file without schema upgrade was rejected because it would still hide missing CO2/hypoxia data. JSON-only data was rejected because runtime ingest would force parsing or private state. Runtime formula evaluation was rejected because cold authored LUTs are cheaper and deterministic.

Scalability potential: Low: read composite danger from stride-8 depth buckets. Middle: full rows with linear interpolation at control cadence. High: danger gradient and visor/audio drive fields. Ultra: complex presentation modulation from the same scalar truth without adding gameplay simulation.

Hardware Impact: 0 B/frame expected. Low-end i3/MX350 avoids per-frame exponentials and repeated pressure math. Top-tier machines receive extra presentation fields without changing simulation authority.

## Decision 003 - Physics Constants And Curves

Problem: Toxicity values must be derived from physics and physiology anchors, not placeholder scalars.

Solution: Dalton partial pressure uses `p_i = fraction_i * P_abs`, with `P_abs = 101325 + 1025 * 9.80665 * depthMeters`. Dry-air O2/N2/argon/CO2 fractions are normalized before use. Curves are derived from known anchors: hypoxia 16 to 10 kPa O2, O2 CNS 1.40 to 1.60 ATA, nitrogen narcosis 4 to 8 ATA nitrogen-equivalent, and CO2 0.506625 to 4.053 kPa.

Rejected Alternatives: The old `1 + depth / 10.1325` pressure shortcut was rejected because it hides rho*g*h. Linear hard cuts were rejected because they create unstable presentation. Per-molecule gas simulation was rejected by the cinematic-cheat and frame-time mandates.

Scalability potential: Low tiers consume final scalar flags. High tiers consume gradients/presentation data. Gameplay correctness remains Dalton scalar truth.

Hardware Impact: Offline bake cost only. Runtime lookup is stateless and DataVault-compatible.

## Decision 004 - Wider Binary Alignment Sweep

Problem: The user required every `.bin`/`.h8bin` binary blob to be 16-byte aligned. The active scan found unaligned Dalton, caustics, water fog density, and stale submarine runtime pack artifacts.

Solution: Align Dalton by schema rewrite, pad caustics to 1216 bytes, pad water fog density to 3008 bytes while preserving 1501 authored half-float samples, and regenerate the submarine runtime pack to 1152 bytes.

Rejected Alternatives: Reporting only the Dalton file was rejected because the active data scan found broader binary hygiene debt. Changing water fog axis count was rejected because runtime data semantics must stay stable; tail padding is cheaper and safer.

Scalability potential: Alignment makes SHINOBU-style bulk ingest predictable on low-end and high-end hardware. Padding is a cold data cost only.

Hardware Impact: Dalton increased by 88108 bytes over the old file; caustics +4 bytes; water fog +6 bytes; submarine runtime pack +28 bytes. Frame-time impact: 0 us measured.

## Decision 005 - Hash And Economy Proof

Problem: Hash IDs and recipe economy claims are hearsay without deterministic tools.

Solution: Regenerated and checked `H8Hashes.cs` with `VerifyH8HashCollisions.py`; result: 1018 records, 0 collisions, generated header up to date. Ran `CraftingEconomyMonteCarlo.py --steps 1000000`; result: 1,000,000 steps, 0 profit steps, negative value/mass/energy deltas. Reran `EconomyValidator.py --negative-tests`.

Rejected Alternatives: Relying on recipe DAG alone was rejected because graph acyclicity does not prove deconstruct/reclaim economics. Ignoring stale `H8Hashes.cs` was rejected because generated hashes were out of sync.

Scalability potential: Hash catalog gives stateless lookup IDs. Economy proof keeps recipes data-driven and prevents private runtime correction logic.

Hardware Impact: 0 us runtime impact. Tooling-only proof.

## Decision 006 - H-Phi Tool Scope Fix

Problem: `Tools/CalculateHPhi.py` did not produce an artifact when scanning all `Assets`, `Packages`, and `Tools`. That scope dragged package/third-party C# into a first-party architecture metric and stalled the audit.

Solution: Change the tool default to first-party `Assets/_Project` plus `Tools`, and add explicit `--source-roots` support. The report now records actual scan roots instead of the constant default. The tool wrote `Docs/AgentLogs/HPhi_DALTON_GAS_TOXICITY_TABLES.json` and `.png`, with `DOMAIN_INDEX_COUNT=85` and `RUNTIME_H_PHI_STATIC=6.7481e-05`.

Rejected Alternatives: Claiming H-Phi success without an artifact was rejected. Scanning package sources was rejected because `PROJECT_ATLAS.md` states first-party `Assets/_Project/**/*.asmdef` is the scope and third-party/package asmdefs are dependency evidence, not project-domain ownership.

Scalability potential: Data sovereignty improved for this task because gas toxicity is a stateless aligned binary lookup with manifest-defined quality tiers.

Hardware Impact: 0 us runtime impact. H-Phi is static source/doc evidence only; no Unity runtime proof.

## Decision 007 - Dedicated Dalton Verify Gate

Problem: The Dalton artifact was validated by `Tools/DaltonGasToxicityBaker.py --verify`, but the project convention and user order explicitly demanded `Verify*.py` scripts. Leaving the audit inside the baker made it too easy for later agents to skip binary/header/source-tier checks.

Solution: Added `Tools/VerifyDaltonGasToxicity.py` and `Tools/test_verify_dalton_gas_toxicity.py`. The verifier reloads the binary and manifest, rechecks `<` Little Endian formats, exact 128128-byte/64-byte-row contract, row formulas, 0 FNV collisions, source references, Low/Middle/High/Ultra tier policy, and stateless runtime contract. The manifest now embeds NOAA, US Navy, and CDC/NIOSH source references for the toxicity anchors.

Rejected Alternatives: Renaming the baker was rejected because it would blur authoring and verification ownership. A shell-only checksum check was rejected because it would not catch row-format endianness drift or source-tier omissions. Running every unrelated `Verify*.py` in the repository was rejected because AI/quest/other domains are not part of the Dalton gas data boundary.

Scalability potential: Low/toaster reads nearest stride-8 danger scalars; Middle reads stride-2 full rows; High and RTX tiers consume extra presentation fields from the same truth table. No gameplay-private gas solver state is required.

Hardware Impact: 0 us runtime impact. Authoring verification only. i3/MX350 consumes an aligned binary lookup; high-end devices spend saved cycles on visor/audio/gradient overkill rather than physics recomputation.

## Decision 008 - CO2 PPM Derivation Hardening

Problem: The CO2 thresholds were correct values but appeared in code as precomputed kPa literals. That is weak provenance for a hard-science data table because later agents cannot distinguish a derived limit from a placeholder scalar.

Solution: Introduced `CO2_REL_TWA_PPM`, `CO2_STEL_PPM`, `CO2_IDLH_PPM`, and `PPM_TO_FRACTION`, then derive kPa thresholds from `co2Ppm * (1 / 1000000) * 101.325`. Added `co2KPaDerivation` to the manifest. Hardened `VerifyDaltonGasToxicity.py` to reject ppm/kPa drift and reject source rows without authority, HTTPS URL, or usedFor binding. Added a negative unit test that mutates `co2IdlhKPa` and requires verifier failure.

Rejected Alternatives: Keeping the kPa literals with comments was rejected because comments do not enforce derivation. Fetching remote source PDFs during every verifier run was rejected because the build gate must be deterministic and offline; source URLs are recorded, but local numeric invariants are enforced.

Scalability potential: No runtime change. Low/Middle/High/Ultra consumers still read the same aligned binary rows; stronger provenance improves stateless data trust without adding solver state.

Hardware Impact: 0 us runtime impact. The binary SHA stayed unchanged because numerical table values did not move; only metadata/provenance and verification strictness changed.

## Decision 009 - Actual Toaster And RTX Tier Binaries

Problem: The manifest described toaster and RTX/God-mode policies, but the only generated Dalton binary was the full table. That was weaker than the SHINOBU ingest requirement because tier selection would still require interpreting the full table or trusting JSON-only policy.

Solution: Added two real tier binaries generated from the full Dalton truth table. `dalton_gas_toxicity_toaster.bin` uses `H8GL`, 251 rows at 8m stride, and four float32 danger columns. `dalton_gas_toxicity_overkill.bin` uses `H8GX`, 2001 rows, and twelve float32 presentation columns: depth, composite danger, central gradients, visor pulse, breath filter, deterministic FNV harmonic seed/phase, perceptual color drive, and regulator distortion. Both use the same 64-byte little-endian header pattern and 16-byte-aligned rows.

Rejected Alternatives: Manifest-only tier declarations were rejected because they are not zero-cost ingest. Separate runtime tier solvers were rejected because they create private state and duplicate Dalton logic. Arbitrary visual noise constants were rejected; harmonic noise seed/phase are derived from FNV of schema/depth.

Scalability potential: Low/toaster can load 4080 bytes and skip full-table parsing. Middle/high can still consume the full 128128-byte truth table. RTX/God-mode can load the 96112-byte presentation table and spend saved runtime cycles on visor/audio/color overload without changing gameplay truth.

Hardware Impact: 0 us measured runtime impact. Expected low-end memory/read reduction for gas danger lookup: 128128 bytes full table versus 4080 bytes toaster tier. High-end presentation data remains stateless and cold-loaded.

## Decision 010 - Cross-Domain VFX Metadata Repair Via Owner Tool

Problem: The relevant verification gate set failed at `VerifyVramBudgets.py` because `Data/System/VFX_Budgets.json` was missing `binaryCache.headerStructFormat`. This was outside the Dalton domain but blocked the user-requested data inquisition pass.

Solution: Used the VFX owner tool only: `python -B Tools\VerifyVramBudgets.py --rewrite-json --write-binary-cache`, then reran `python -B Tools\VerifyVramBudgets.py`. No manual VFX JSON editing was performed.

Rejected Alternatives: Ignoring the failed gate was rejected because the user demanded `Verify*.py` reruns. Hand-editing VFX files was rejected because it is cross-domain and more error-prone than the domain verifier's deterministic repair path.

Scalability potential: VFX budget data remains stateless fixed-row hash lookup with TOASTER/DECK/PRO/GOD_MODE tiers.

Hardware Impact: 0 us runtime impact from this agent. Static data/manifest repair only; VFX runtime proof remains pending.
