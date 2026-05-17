# LOG_DALTON_GAS_TOXICITY_TABLES

## Entry - Missing Batch Prompt Block

What was wrong -> `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="DALTON_GAS_TOXICITY_TABLES">`. The user-requested task cannot be bound to an authoritative task count or output contract.

What was done -> Verified status/rationale absence, searched the batch file for the exact Prompt ID and related gas/toxicity terms, read relevant mandate files, then wrote blocked status and rationale artifacts.

Cinematic Cheats used -> None implemented. The intended data-path remains deterministic authored curves/LUTs rather than particle or molecule simulation, but no curve artifact was produced because the prompt block is missing.

Exact Microseconds saved -> 0 us measured. No runtime code changed; profiler evidence absent. Status remains PENDING VERIFICATION for any future implementation.

Regression model -> CPU no change, GC no change, memory no change, cadence no change, correctness blocked by missing task source.

Hot path impact -> None.

Failure modes -> If another agent proceeds by guessing this task from neighboring prompts, it may generate incompatible gas dynamics data or overwrite another domain's artifact. Required fix is to add the missing XML block or provide explicit standalone acceptance criteria.

## Entry - Standalone Override Gas Toxicity Bake

What was wrong -> `DALTON_GAS_TOXICITY_TABLES` still has no XML block in `Docs/Tasks/CURRENT_BATCH.md`. The existing gas binary was also technically defective: 40020 bytes, unaligned (`mod16=4`), raw headerless rows, and too few columns for CO2/hypoxia/gas-danger scalability.

What was done -> Added `Tools/DaltonGasToxicityBaker.py` and `Tools/test_dalton_gas_toxicity_baker.py`. Rebuilt `Data/Precomputed/dalton_gas_toxicity.bin` as `H8GT` v2: 64-byte header, 2001 rows, 16 float32 columns, 64-byte row stride, 128128 bytes total. Added `Data/Precomputed/dalton_gas_toxicity_manifest.json` with constants, formulas, quality tiers, FNV IDs, and runtime contract. Updated `Tools/MathLUTGenerator.py` and `Tools/test_math_lut_generator.py` so the legacy math pack validates the new contract.

Cinematic Cheats used -> No molecule simulation. Dalton/hydrostatic truth is precomputed offline, then runtime systems consume stateless lookup rows. High-tier overkill is limited to gradient/visor/audio presentation fields fed by the same gameplay scalar.

Exact Microseconds saved -> 0 us measured. No Unity profiler run. Expected hot-path gain is avoided per-frame pressure/toxicity recomputation, but this remains PENDING VERIFICATION until Unity profiler/GCMonitor evidence exists.

Physics basis -> `P_abs = 101325 + 1025 * 9.80665 * depthMeters`; `p_i = gasFraction * P_abs`. Dry air fractions are normalized. Curves: hypoxia 16 to 10 kPa O2; O2 CNS 1.40 to 1.60 ATA; nitrogen-equivalent narcosis 4 to 8 ATA; CO2 0.506625 to 4.053 kPa.

Binary/cache hygiene -> `Data/Precomputed/caustics_dispersion_offsets.bin` padded to 1216 bytes; `Data/Visuals/Water_Fog_Density_LUT.bin` padded to 3008 bytes; stale `Data/Physics/Submarine_RuntimePack.bin` regenerated to 1152 bytes. Active scan over `Data` and `Assets/_Project` reported `ALL_ACTIVE_DATA_BINARIES_16B_ALIGNED`.

Validation -> `Tools/DaltonGasToxicityBaker.py --verify` PASS. `Tools/MathLUTGenerator.py --verify` PASS. Unit tests passed: Dalton, math LUT, water color preview, submarine physics. `VerifyH8HashCollisions.py` synchronized generated C# and reported `HASH COLLISIONS: 0`. `CraftingEconomyMonteCarlo.py --steps 1000000` reported `profit_steps=0`. `EconomyValidator.py --negative-tests` reported `STATUS: ECONOMY BALANCED`. `VerifyDataInquisition.py` reported `binaries=38 aligned16=true manifests=8 endian=< structFormats=138 monteCarloSteps=1000000 hashCollisions=0 atlasDomains=85`. `VerifyLore.py --check`, `LoreTechValidator.py`, `VerifyVramBudgets.py`, and `VerifySabineBaker.py` passed.

H-Phi correction -> `Tools/CalculateHPhi.py` was narrowed to explicit first-party source roots (`Assets/_Project`, `Tools`) and now writes actual scan roots into the report. The DALTON run wrote `Docs/AgentLogs/HPhi_DALTON_GAS_TOXICITY_TABLES.json` and `.png`; `DOMAIN_INDEX_COUNT=85`, `RUNTIME_H_PHI_STATIC=6.7481e-05`, `runtime_data_sovereignty=0.019743027`, `runtime_files=1279`. A `--workers 4` run also completed; temporary workercheck artifacts were removed.

Residual risk -> Unity import, Play Mode, GCMonitor, profiler, frame-time, and player build proof were not run.

## 2026-05-16 - Dedicated Dalton Verify Gate And Final Audit Refresh

What was wrong -> Dalton validation existed in the baker, but there was no standalone `Verify*.py` entry point for the gas toxicity data. That weakened the self-validation loop and made the endianness/source-tier checks less visible. The manifest source notes were human-readable but not machine-checkable source references. A combined unit-test command timed out at 307s, so treating that command as a pass would have been false reporting.

What was done -> Added `Tools/VerifyDaltonGasToxicity.py` and `Tools/test_verify_dalton_gas_toxicity.py`. Extended `Data/Precomputed/dalton_gas_toxicity_manifest.json` generation with NOAA NDSSM 2025, US Navy Diving Manual Rev 7, and CDC/NIOSH CO2 IDLH source references. Regenerated the Dalton manifest and reran the dedicated verifier. Split the timed-out unit command and reran individual suites to completion.

Cinematic Cheats used -> Dalton truth remains a cold, stateless lookup table. No per-molecule or per-breath runtime physics was added. Toaster path uses nearest stride-8 danger scalars. High/RTX paths spend the same scalar truth on visor gradient, breath audio filter, dirty regulator modulation, and perceptual color-LUT drive.

Exact Microseconds saved -> Measured Unity runtime savings absent. Expected runtime hot-path allocation remains 0 B/frame. Expected per-query runtime compute saved: pressure and toxicity curve recomputation replaced by aligned binary lookup. Exact microseconds remain PENDING UNITY PROFILER.

Verification evidence -> `python -B Tools\DaltonGasToxicityBaker.py` passed and regenerated the manifest. `python -B Tools\VerifyDaltonGasToxicity.py` passed: 128128 bytes, 2001 rows, 16 columns, `<` endian, 0 FNV collisions, NOAA/US_NAVY/NIOSH references present. `python -B -m unittest Tools.test_dalton_gas_toxicity_baker Tools.test_verify_dalton_gas_toxicity` passed: 6 tests OK. Split suites passed after timeout split: math LUT 7 tests OK, water fog 1 test OK, submarine physics 24 tests OK. Relevant verify gate set passed: BinaryHygiene, DaltonGasToxicity, H8HashCollisions, Lore, PdaTechnicalLogs, CraftingCosts, Optics, SnellRefraction, Sabine, VramBudgets, DataInquisition. Latest DataInquisition: `binaries=38 aligned16=true manifests=8 endian=< structFormats=148 monteCarloSteps=1000000 hashCollisions=0 atlasDomains=85`.

Residual risk -> Batch XML still lacks `<AGENT_PROMPT id="DALTON_GAS_TOXICITY_TABLES">`. Unity import, Play Mode, GCMonitor, profiler, and player build proof remain absent.

## 2026-05-16 - CO2 Derivation Hardening

What was wrong -> CO2 toxicity kPa thresholds were numerically correct but encoded as naked derived literals. That left room for later agents to mistake them for magic numbers or silently drift the ppm/kPa relationship.

What was done -> Replaced direct CO2 kPa constants with ppm anchors: 5000 ppm TWA, 30000 ppm STEL, 40000 ppm IDLH, plus `PPM_TO_FRACTION = 1 / 1000000`. kPa thresholds are now derived from `co2Ppm * PPM_TO_FRACTION * 101.325`. The manifest records `co2KPaDerivation`, ppm anchors, and NIOSH `usedFor` bindings. The Dalton verifier now rejects CO2 kPa drift from ppm anchors and rejects source rows without authority, HTTPS URL, and usedFor list.

Cinematic Cheats used -> No new runtime simulation. This remains cold authored Dalton scalar truth with quality-tier presentation channels.

Exact Microseconds saved -> 0 us measured. Runtime binary unchanged; binary SHA remained `A0D6AAB39497BA771FCA7D4C5754C31C0F0CDBEAFF0F9860B44B01BC7240C9DD`.

Verification evidence -> `python -B Tools\DaltonGasToxicityBaker.py` passed. `python -B Tools\VerifyDaltonGasToxicity.py` passed. `python -B -m unittest Tools.test_dalton_gas_toxicity_baker Tools.test_verify_dalton_gas_toxicity` passed: 7 tests OK. Relevant gates passed: BinaryHygiene, DaltonGasToxicity, H8HashCollisions, CraftingCosts, Optics, Sabine, VramBudgets, DataInquisition. Latest DataInquisition: `binaries=38 aligned16=true manifests=8 endian=< structFormats=149 monteCarloSteps=1000000 hashCollisions=0 atlasDomains=85`.

Residual risk -> Batch XML still missing. Unity runtime/profiler proof still absent.

## 2026-05-16 - Actual Tier Binaries And Gate Repair

What was wrong -> Dalton tier policy existed in manifest text, but no stripped toaster binary or RTX overkill binary existed. That was not enough for zero-cost SHINOBU-style ingest. During the broad gate rerun, `VerifyVramBudgets.py` also failed on missing `binaryCache.headerStructFormat` in VFX data.

What was done -> Added `Data/Precomputed/dalton_gas_toxicity_toaster.bin`: `H8GL`, 4080 bytes, 251 rows, 4 float32 danger columns, 8m depth stride. Added `Data/Precomputed/dalton_gas_toxicity_overkill.bin`: `H8GX`, 96112 bytes, 2001 rows, 12 float32 presentation columns. Updated the Dalton manifest and verifier to validate both tier binaries byte-for-byte against derived tables. Repaired VFX metadata only via `VerifyVramBudgets.py --rewrite-json --write-binary-cache`, then reran its clean verifier.

Cinematic Cheats used -> No gas particle solver. Toaster uses coarse scalar danger rows. RTX uses FNV-derived harmonic seed/phase and central gradients from the same Dalton truth table for visor/audio/color overload.

Exact Microseconds saved -> Measured Unity runtime savings absent. Expected low-end cold data read shrinks from 128128-byte full table to 4080-byte toaster tier for danger-only lookup. Runtime hot-path allocation remains 0 B/frame by contract.

Verification evidence -> `python -B Tools\DaltonGasToxicityBaker.py` passed. `python -B Tools\VerifyDaltonGasToxicity.py` passed with full=128128 bytes, toaster=4080 bytes, overkill=96112 bytes. `python -B -m unittest Tools.test_dalton_gas_toxicity_baker Tools.test_verify_dalton_gas_toxicity` passed: 8 tests OK. Full relevant gates passed after VFX repair: MathLUTGenerator, BinaryHygiene, DaltonGasToxicity, H8HashCollisions, CraftingCosts, Optics, Sabine, VramBudgets, DataInquisition. Latest DataInquisition: `binaries=40 aligned16=true manifests=8 endian=< structFormats=151 monteCarloSteps=1000000 hashCollisions=0 atlasDomains=85`.

Residual risk -> Batch XML still lacks `<AGENT_PROMPT id="DALTON_GAS_TOXICITY_TABLES">`. Unity import, Play Mode, GCMonitor, profiler, and player build proof remain absent.
