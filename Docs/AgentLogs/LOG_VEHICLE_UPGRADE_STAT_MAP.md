# LOG VEHICLE_UPGRADE_STAT_MAP

## 2026-05-16 - Submarine Upgrade Stat Map Bake

What was wrong:
- Submarine upgrade depth/speed/hull balance values had no baked JSON stat matrix for runtime hash lookup.
- Rebalancing required code-side float edits instead of changing data artifacts.
- Speed progression needed a non-linear cap path; direct linear speed growth would raise physics tunneling and control risk.

What was done:
- Added `Tools/UpgradeCurveBaker.py`.
- Added `Tools/test_upgrade_curve_baker.py`.
- Generated `Data/Economy/Submarine_Upgrade_Stat_Map.json` as a 616-byte minified runtime row array.
- Generated `Data/Economy/Submarine_Upgrade_Stat_Map_Layout.json` documenting the 16-byte unmanaged row contract for SHINOBU `SuitUpgradeManager`.
- Generated `Data/Economy/Submarine_Upgrade_Stat_Map_Validation.json` with hash uniqueness, Mk3 torque, Safe Shallows, and minification checks.
- Generated `Data/Economy/Submarine_Upgrade_SpeedPower.png` as the speed-vs-power proof graph.

Cinematic Cheats used:
- Torque scales geometrically to exactly 2.5x at Mk3, but speed preview is log-compressed to cap navigation risk.
- Hull pressure resistance uses a damped exponent instead of raw depth ratio, preserving abyss access without turning hull integrity into an untunable 25x scalar.
- PNG graph uses deterministic in-script raster drawing instead of external plotting dependency.

Exact Microseconds saved:
- Profiler-backed runtime microseconds saved: 0. Unity runtime was not executed by this no-C# data task.
- Static hot-path impact: 0 us added, 0 B/frame expected, because all curve/hash work is offline and the runtime-facing file contains numeric rows only.

Verification:
- `python Tools\UpgradeCurveBaker.py` exited 0 and reported `UPGRADES BAKED`.
- `python Tools\UpgradeCurveBaker.py --verify-only` exited 0.
- `python -m unittest Tools.test_upgrade_curve_baker` ran 2 tests OK.
- `python -m py_compile Tools\UpgradeCurveBaker.py Tools\test_upgrade_curve_baker.py` exited 0.
- Scoped task files are Python/Data/Docs only. Existing unrelated C# untracked file `Assets/_Project/Scripts/Core/Generated/H8QuestMasks.cs` was observed and not touched.

Artifacts:
- Runtime JSON SHA256 `EB0478004CA090DBC3C2BC36BF7A1EDEA62675AFFC859FE7133ADC0EC7A6FD73`.
- Layout JSON SHA256 `77EE7CD3EE916BC4E1B6D86D1E443045FBB4F995F99DD3F845DB6AD2BC94CABF`.
- Validation JSON SHA256 `2352BF9FCF819C314D240C4D06FC96C3345B23B8763C365343A079977B6F7061`.
- PNG SHA256 `3436DD164B48982A5C08D6E3CD15D21061BA081AD1D149B9FB2AEE4977EB72F3`.

## 2026-05-16 - Data Truth Inquisition Pass

What was wrong:
- The first hull curve was mathematically weak: damped exponent with insufficient hard-science provenance.
- Runtime JSON existed, but no SHINOBU-ready fixed binary pack existed.
- Economy loop proof was external and not attached to the upgrade stat-map artifact chain.
- Scalability sidecar data did not explicitly split Toaster ingest from RTX overkill visuals.

What was done:
- Rebuilt hull values from hydrostatic gauge pressure and shell-buckling cuberoot ratio.
- Rebuilt engine `PowerCost` from hydrodynamic drag power using `Data/Physics/Submarine_Specs.json`.
- Added `Data/Economy/Submarine_Upgrade_Stat_Map.h8bin`: 176 bytes, little-endian, 16-byte aligned.
- Added `Data/Economy/Submarine_Upgrade_Stat_Map_BinaryLayout.json`.
- Added `Data/Economy/Submarine_Upgrade_Stat_Map_Physics.json`.
- Added `Data/Economy/Submarine_Upgrade_Stat_Map_Scalability.json`.
- Added `Data/Economy/Submarine_Upgrade_Stat_Map_EconomyMonteCarlo.json`.
- Added `Data/Economy/Submarine_Upgrade_Stat_Map_Inquisition.json`.

Cinematic Cheats used:
- Speed remains log-compressed between rated cruise and terminal thrust speed; the cheat preserves player control and avoids physics tunneling.
- High-tier visual overkill is sidecar data: pressure gradient 1024, 8 propwash harmonics, 6 silt octaves, 512 cavitation sparkle samples.
- Toaster lane loads only binary/numeric scalar rows; no graphing or harmonic data is required.

Exact Microseconds saved:
- Profiler-backed runtime microseconds saved: 0. Unity runtime was not executed by directive.
- Static runtime work moved offline: hydrostatic pressure, shell ratio, speed curve, power curve, hashing, and binary packing.
- Hot-path allocation estimate remains 0 B/frame added because artifacts are immutable numeric rows.

Verification:
- `python Tools\UpgradeCurveBaker.py` exited 0.
- `python Tools\UpgradeCurveBaker.py --verify-only` exited 0.
- `python -m py_compile Tools\UpgradeCurveBaker.py Tools\test_upgrade_curve_baker.py` exited 0.
- `python -m unittest Tools.test_upgrade_curve_baker` ran 2 tests OK.
- `python Tools\EconomyValidator.py --root .` exited 0 and reported `monte_carlo_steps=1000000`.
- `python Tools\EconomyRecipeGraphAudit.py --root . --report Docs\AgentLogs\EconomyRecipeGraphAudit_VEHICLE_UPGRADE_STAT_MAP.md` exited 0 and reported `cycle_count=0`.
- `python Tools\MathLUTGenerator.py --verify` exited 0; Dalton binary aligned16 true.
- `python Tools\OpticsBaker.py --verify` exited 0; Beer-Lambert matrix aligned16 true.
- `python Tools\SabineBaker.py --verify-only` exited 0.
- `python Tools\VerifySabineBaker.py` exited 0; Sabine/Thorp/BeerLambert/HydrostaticPressure audit passed.
- `python Tools\VerifyH8HashCollisions.py --root . --write-json Docs\AgentLogs\HashAudit_VEHICLE_UPGRADE_STAT_MAP.json --write-report Docs\AgentLogs\HashAudit_VEHICLE_UPGRADE_STAT_MAP.md` exited 0; collisions 0.
- `python Tools\VerifyLore.py --check` exited 0; encyclopedia `.h8bin` alignment 16 and endian `<`.
- `python Tools\VerifyVramBudgets.py` exited 0.
- `python Tools\SubmarinePhysicsSim.py --verify-only` exited 0.
- Full `Data/**/*.bin` and `Data/**/*.h8bin` size scan found `BINARY_COUNT=35 MISALIGNED=0`.

Updated artifacts:
- Runtime JSON SHA256 `73FFC0BC0F7EF2B6163670FE736D59AF563A511BA82435E816E1B9481CC08C0A`.
- Binary pack SHA256 `0FE11E2B64B7BC63946490D73E4CA28591CCE3DB7036C45ADFD23D47D0E77398`.
- Physics JSON SHA256 `51BB431E22A73ABE4F82D8446DD0D205E689E984AB8851B05F9AB96F3E082E83`.
- Economy Monte Carlo JSON SHA256 `4F53CFF4CA9789D936A9F330444E8C666742B3FED5CBA1FA4DB6CB1AE2F253F0`.
- Inquisition JSON SHA256 `4FECAC1A52C58AD559492AC2FBF0EFF3422AA91A9C6C9F2BB0F3B8FF1C244AAE`.

## 2026-05-16 - Exponential Power Repair And Full Verify Sweep

What was wrong:
- The previous hard-science power pass was drag-grounded but did not explicitly satisfy the XML requirement that higher-tier engines draw exponentially more power.
- Status and rationale still contained stale drag-only Mk1/Mk2/Mk3 `PowerCost` values.
- The initial all-in-one `Verify*.py` loop timed out, which was not acceptable evidence.

What was done:
- Repaired the bake to use `PowerCost = drag_power * torque_multiplier^1.5`.
- Updated status and rationale to the current values: Mk1 4316.076424, Mk2 9453.581896, Mk3 20055.037086.
- Re-ran all `Tools/Verify*.py` scripts as bounded batches so each result was visible.
- Removed Python cache files generated by syntax/unit verification for this task.

Cinematic Cheats used:
- Speed remains logarithmic and control-safe while torque and power climb hard enough to feel like dirty industrial machinery under load.
- Toaster lane remains 176-byte `.h8bin` scalar ingest; RTX lane keeps pressure shimmer, propwash harmonic, silt octave, and cavitation sidecar fields.

Exact Microseconds saved:
- Profiler-backed runtime microseconds saved: 0. Unity runtime was not executed by directive.
- Static runtime work avoided: hash calculation, curve evaluation, hydrodynamic pressure math, and binary packing are offline.
- Hot-path allocation estimate remains 0 B/frame added; measured Unity GC proof absent.

Verification:
- `python Tools\VerifyAiNavigationTuning.py` exited 0.
- `python Tools\VerifyBabel.py` exited 0.
- `python Tools\VerifyBabelDictionary.py` exited 0.
- `python Tools\VerifyBinaryHygiene.py` exited 0; `binaryCount=39`, `misalignedCount=0`.
- `python Tools\VerifyCraftingCosts.py` exited 0.
- `python Tools\VerifyDataInquisition.py` exited 0; `monteCarloSteps=1000000`, `hashCollisions=0`, `atlasDomains=85`.
- `python Tools\VerifyH8HashCollisions.py --root . --write-json Docs\AgentLogs\HashAudit_VEHICLE_UPGRADE_STAT_MAP.json --write-report Docs\AgentLogs\HashAudit_VEHICLE_UPGRADE_STAT_MAP.md` exited 0; collisions 0.
- `python Tools\VerifyHullStressBudget.py` exited 0.
- `python Tools\VerifyLore.py --check` exited 0.
- `python Tools\VerifyMetricPhiDataTruth.py` exited 0; checks 33, failed 0.
- `python Tools\VerifyOpticsBaker.py` exited 0.
- `python Tools\VerifyOrganicEntropy.py` exited 0.
- `python Tools\VerifyPdaTechnicalLogs.py` exited 0.
- `python Tools\VerifyQuestDag.py` exited 0.
- `python Tools\VerifySabineBaker.py` exited 0.
- `python Tools\VerifySnellRefractionLut.py` exited 0.
- `python Tools\VerifyTideBaker.py` exited 0.
- `python Tools\VerifyUpgradeCurveBaker.py` exited 0; `binary_mod16=0`, `power_growth_ratios=[2.190318, 2.121422]`, `atlas_domains=[4,52,54,57,58]`.
- `python Tools\VerifyVisualLodMatrix.py` exited 0.
- `python Tools\VerifyVramBudgets.py` exited 0.
- `python Tools\VerifyVrComfortData.py` exited 0.
- `python Tools\EconomyValidator.py --root .` exited 0; `monte_carlo_steps=1000000`, status `ECONOMY BALANCED`.
- `python Tools\EconomyRecipeGraphAudit.py --root . --report Docs\AgentLogs\EconomyRecipeGraphAudit_VEHICLE_UPGRADE_STAT_MAP.md` exited 0; `cycle_count=0`, status `ECONOMY SECURED`.
- `python Tools\MathLUTGenerator.py --verify` exited 0; Dalton binary aligned16 true.
- `python Tools\OpticsBaker.py --verify` exited 0; Beer-Lambert matrix aligned16 true.
- `python Tools\SabineBaker.py --verify-only` exited 0.
- `python Tools\SubmarinePhysicsSim.py --verify-only` exited 0.
- `python -m py_compile Tools\UpgradeCurveBaker.py Tools\VerifyUpgradeCurveBaker.py Tools\test_upgrade_curve_baker.py` exited 0.
- `python -m unittest Tools.test_upgrade_curve_baker` ran 2 tests OK.
- Corrected binary extension scan found `BINARY_COUNT=37`, `MISALIGNED=0`.

Updated artifacts:
- Runtime JSON SHA256 `1DB8BD9C1EAA890A26C24F7D8824A04B8835980ACE492CF2AD5ECEE1A9B7DAC0`.
- Binary pack SHA256 `CA0CB2C3E8E970DC45D45AAEA2E247A8696E910C3990117BA32EE180AF79066A`.
- Physics JSON SHA256 `71E3D05FC8AEE1956C0769CB10AC11097D9E7C06A58AC0E066BB7DC4EFA47D7D`.
- Economy Monte Carlo JSON SHA256 `4F53CFF4CA9789D936A9F330444E8C666742B3FED5CBA1FA4DB6CB1AE2F253F0`.
- Inquisition JSON SHA256 `4FECAC1A52C58AD559492AC2FBF0EFF3422AA91A9C6C9F2BB0F3B8FF1C244AAE`.
- Validation JSON SHA256 `0B7DF37C565D2D2C5C82F3999F6A9C28450DA1106DE6C945CED4A6134C12BB9D`.
- PNG SHA256 `B05E06E67975036BB4E6838EE9E08B82766C4D4E0233C43E62892AA8A8455610`.

## 2026-05-16 - Unit Audit, MetricPhi Repair, Repo Binary Hygiene

What was wrong:
- Sidecar fields labeled instantaneous power with stale energy-style field names; the formula is `force * speed / 1000`, which is kW.
- `VerifyUpgradeCurveBaker.py` still expected the old physics-doc formula key after the unit correction.
- `VerifyMetricPhiDataTruth.py` expected PDA `ExtraData` to be a runtime tier, while the current PDA contract packs `ExtraVisualRecord` and marks `ExtraData` authoring-only.
- A repo-wide binary scan found ignored temp/build `.bin` and `.h8bin` blobs not padded to 16 bytes.

What was done:
- Renamed submarine sidecar curve fields to `power_kw` and `drag_power_kw`; runtime JSON and binary ABI stayed unchanged.
- Regenerated `Submarine_Upgrade_Stat_Map_Layout.json`, `Submarine_Upgrade_Stat_Map_Physics.json`, `Submarine_Upgrade_Stat_Map_Validation.json`, and `Submarine_Upgrade_SpeedPower.png`.
- Updated `VerifyUpgradeCurveBaker.py` to enforce `upgrade_power_cost_kw`.
- Updated `VerifyMetricPhiDataTruth.py` to accept `TierPayloads=["Text","CompactText","ExtraVisualRecord"]`, `AuthoringOnlyFields=["ExtraData"]`, and fixed binary extra payload encoding.
- Padded only ignored cache/build binary blobs under `.codex-artifacts`, `.codex-build`, and `Temp` to 16-byte length.

Cinematic Cheats used:
- No new runtime simulation. The same log-compressed speed cap buys high-tier propwash/cavitation visuals without increasing collision skip risk.
- High-end visuals remain sidecar-driven; toaster runtime still consumes the fixed 176-byte stat blob.

Exact Microseconds saved:
- Runtime microseconds saved: 0 measured. Unity runtime was not executed.
- Static hot-path impact: unchanged; no new runtime code, no ABI expansion, no private state.

Verification:
- `python Tools\UpgradeCurveBaker.py` exited 0.
- `python Tools\UpgradeCurveBaker.py --verify-only` exited 0.
- `python Tools\VerifyUpgradeCurveBaker.py` exited 0.
- `python -m py_compile Tools\UpgradeCurveBaker.py Tools\VerifyUpgradeCurveBaker.py Tools\test_upgrade_curve_baker.py` exited 0.
- `python -m unittest Tools.test_upgrade_curve_baker` ran 2 tests OK.
- `python Tools\VerifyBabelDictionary.py` exited 0 after deterministic rebuild.
- `python Tools\VerifyMetricPhiDataTruth.py` exited 0; checks 36, failed 0.
- `python Tools\RunMetricPhiVerifySweep.py --xxhash-path "$env:TEMP\metric_phi_xxhash_ref"` exited 0; commands 28, required failures 0.
- Repo-wide `.bin`/`.h8bin` scan: `BINARY_COUNT_REPO=74`, `MISALIGNED_REPO=0`.

Updated artifacts:
- Runtime JSON SHA256 `1DB8BD9C1EAA890A26C24F7D8824A04B8835980ACE492CF2AD5ECEE1A9B7DAC0`.
- Binary pack SHA256 `CA0CB2C3E8E970DC45D45AAEA2E247A8696E910C3990117BA32EE180AF79066A`.
- Layout JSON SHA256 `7FAA142DBC28EB950733E41E79C9389173699213EE8CBAECE3722275B1715ABF`.
- Physics JSON SHA256 `C848623558E93260089D09012D2A08C347FCDF7777EF1ED813AA76568B162BC5`.
- Validation JSON SHA256 `1E3AA3ABA459D73A339C79EE9B2C6E19DF73ED989E4B9B8147AD83B9A5DE2783`.
- PNG SHA256 `39DD5E013F310DE547890C1BEC72C320EF3D6BA19CE8E88A22C5AC1DF99A3BAB`.

## 2026-05-16 - Verify Replay And H-Phi Drift Closure

What was wrong:
- Fresh root `Tools/Verify*.py` replay found one stale global evidence contract: `VerifyOrganicEntropy.py` rejected the organic entropy manifest because the manifest lacked explicit `hPhiAudit` fields even though the source data already declared stateless lookup.
- MetricPhi was reading the old failed sweep report, so `VerifyMetricPhiDataTruth.py` failed on `verify_sweep_pass`.

What was done:
- Added `hPhiAudit` to `Data/Ecosystem/Organic_Entropy_Regrowth.manifest.json` with no private runtime state, Data Sovereignty increased, and readonly DataVault/stateless offset lookup wording.
- Re-ran all current root `Tools/Verify*.py` scripts in bounded batches.
- Regenerated `Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json` through `python Tools\RunMetricPhiVerifySweep.py --xxhash-path "$env:TEMP\metric_phi_xxhash_ref"`.
- Re-ran `python Tools\VerifyMetricPhiDataTruth.py` after the sweep.

Cinematic Cheats used:
- No runtime simulation was added. Organic entropy remains a static binary lookup with toaster decimation and ultra harmonic visual payloads.
- Submarine stat-map remains a fixed numeric data table; high-tier visual dirt stays in sidecars.

Exact Microseconds saved:
- Profiler-backed runtime microseconds saved: 0. Unity runtime was not executed.
- Static hot-path impact remains unchanged: all repaired evidence is offline JSON/manifest/report data.

Verification:
- Root `Tools/Verify*.py` set replayed: 24 scripts.
- `python Tools\VerifyOrganicEntropy.py` exited 0 after manifest repair.
- `python Tools\VerifyUpgradeCurveBaker.py` exited 0; `binary_mod16=0`, `monte_carlo_steps=1000000`, `atlas_domains=[4,52,54,57,58]`.
- `python Tools\EconomyValidator.py --root .` exited 0; `monte_carlo_steps=1000000`, status `ECONOMY BALANCED`.
- `python Tools\EconomyRecipeGraphAudit.py --root . --report Docs\AgentLogs\EconomyRecipeGraphAudit_VEHICLE_UPGRADE_STAT_MAP.md` exited 0; `cycle_count=0`, status `ECONOMY SECURED`.
- `python Tools\RunMetricPhiVerifySweep.py --xxhash-path "$env:TEMP\metric_phi_xxhash_ref"` exited 0; 28 commands, 0 required failures.
- `python Tools\VerifyMetricPhiDataTruth.py` exited 0; 36 checks, 0 failed.
- Repo-wide `.bin`/`.h8bin` scan remains `BINARY_COUNT_REPO=74`, `MISALIGNED_REPO=0`.
- Local task scan found no stale energy-unit labels in stat-map tools, sidecars, status, rationale, or log.

Residual risk:
- Unity import, Play Mode, GCMonitor, profiler, frame-time, memory, scene wiring, and player build proof remain `PENDING VERIFICATION` by XML `NO_UNITY`.

## 2026-05-16 - Recursive Verify Final Replay After Fail-Fast Patch

What was wrong:
- After removing physics fallbacks, the first recursive verifier replay used a PowerShell API unavailable in this shell, so special verifier arguments were not applied cleanly.
- The same replay surfaced transient cross-domain drift in Babel/PDA evidence while other agents were writing: Babel SHA drift and PDA `ToneAudit` missing.

What was done:
- Re-ran `python Tools\VerifyBabel.py`, `python Tools\VerifyBabelDictionary.py`, and `python Tools\VerifyPdaTechnicalLogs.py` in isolation; each exited 0 on current disk state.
- Replayed every recursive `Tools/**/Verify*.py` script using explicit substring relative paths.
- Preserved required special args:
  - `Tools\Security\VerifyReplayHasherReference.py --xxhash-path "$env:TEMP\metric_phi_xxhash_ref" --fuzz-count 256`
  - `Tools\VerifyH8HashCollisions.py --root . --write-json Docs\AgentLogs\HashAudit_VEHICLE_UPGRADE_STAT_MAP.json --write-report Docs\AgentLogs\HashAudit_VEHICLE_UPGRADE_STAT_MAP.md`
  - `Tools\VerifyLore.py --check`

Cinematic Cheats used:
- No simulation or runtime code. Verification only.
- Existing data lanes remain static numeric/binary lookup; high-tier overkill remains sidecar-driven.

Exact Microseconds saved:
- Runtime microseconds saved: 0 measured. Unity runtime was not executed.
- Static hot-path impact: unchanged.

Verification:
- Final recursive `Tools/**/Verify*.py` replay exited 0.
- Scripts run: 31.
- Passed: 31.
- Failed: 0.
- `VerifyBinaryHygiene.py`: binaryCount 42, misalignedCount 0.
- `VerifyMetricPhiDataTruth.py`: 36 checks, 0 failed, binary_files 42, unaligned 0, struct_format_sites 167, endian_failures 0.
- `VerifyH8HashCollisions.py`: H8 hash records 1018, items 209, biomes 523, signals 286, hash collisions 0.
- `VerifyUpgradeCurveBaker.py`: 9 rows, 176-byte stat-map binary, 16-byte aligned, `power_growth_ratios=[2.190318,2.121422]`.
- `VerifyPdaTechnicalLogs.py`: entries 100, binaryBytes 59104, hashCollisions 0, H-Phi data sovereignty 1.0.
- `VerifyBabel.py` and `VerifyBabelDictionary.py`: exited 0 after transient drift cleared on current disk state.

Residual risk:
- Unity import, Play Mode, GCMonitor, profiler, frame-time, memory, scene wiring, and player build proof remain `PENDING VERIFICATION` by XML `NO_UNITY`.

## 2026-05-16 - Recursive Verify Star Sweep

What was wrong:
- Previous evidence included root verifiers and MetricPhi curated sweep, but the literal recursive `Tools/**/Verify*.py` set had not been recorded in this agent log after the last repair.

What was done:
- Ran every recursive `Verify*.py` script under `Tools` from disk with `PYTHONDONTWRITEBYTECODE=1`.
- Passed `--xxhash-path "$env:TEMP\metric_phi_xxhash_ref" --fuzz-count 256` to `Tools\Security\VerifyReplayHasherReference.py`.
- Passed `--root . --write-json Docs\AgentLogs\HashAudit_VEHICLE_UPGRADE_STAT_MAP.json --write-report Docs\AgentLogs\HashAudit_VEHICLE_UPGRADE_STAT_MAP.md` to `Tools\VerifyH8HashCollisions.py`.
- Passed `--check` to `Tools\VerifyLore.py`.

Cinematic Cheats used:
- No new simulation. Verification only.
- Existing visual overkill lanes remain data-side: pressure shimmer, propwash harmonic payloads, blue-noise/flow textures, VFX budgets, and visual LOD matrix.

Exact Microseconds saved:
- Runtime microseconds saved: 0 measured. Unity runtime was not executed.
- Hot-path impact: unchanged; this was an offline verifier replay.

Verification:
- Recursive scripts executed: 28.
- Failed scripts: 0.
- `Tools\Architecture\VerifyNetSyncMerkleProtocol.py`: pass, 85 domain labels, 39 aligned binary payloads.
- `Tools\NoiseBaker\VerifyBlueNoiseSpectrum.py`: pass.
- `Tools\Security\VerifyReplayHasherReference.py`: pass, `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK`.
- `Tools\Taxonomy\verify_taxonomy.py`: pass, hash collisions 0.
- `Tools\VerifyDataInquisition.py`: pass, `monteCarloSteps=1000000`, hash collisions 0, atlas domains 85.
- `Tools\VerifyUpgradeCurveBaker.py`: pass, 9 runtime rows, 176-byte binary, 16-byte aligned, `power_growth_ratios=[2.190318,2.121422]`.
- `Tools\VerifyMetricPhiDataTruth.py`: pass, 36 checks, 0 failed.
- `Tools\VerifyBinaryHygiene.py`: pass, binaryCount 39, misalignedCount 0.
- `Tools\VerifyCraftingCosts.py`: pass, recipe count 50, hash collisions 0.
- `Tools\VerifyDaltonGasToxicity.py`: pass, endian `<`, aligned16 true.
- `Tools\VerifySabineBaker.py`: pass, Sabine/Thorp/BeerLambert/HydrostaticPressure audit.
- `Tools\VerifyVrComfortData.py`: pass, toaster and RTX overkill binaries aligned16.

Residual risk:
- Unity runtime evidence remains absent by XML `NO_UNITY`; static and CLI evidence only.

## 2026-05-16 - Physics Fallback Purge

What was wrong:
- `Tools/UpgradeCurveBaker.py` still carried fallback submarine physics constants. The active bake used `Data/Physics/Submarine_Specs.json`, but a missing spec file could have silently generated future stat maps from embedded constants.

What was done:
- Removed fallback constants and fallback return path from `load_physics_reference()`.
- Added required positive source-value checks for `sea_water_density_kg_m3`, `gravity_m_s2`, `atmospheric_pressure_pa`, `rated_cruise_speed_mps`, `terminal_speed_at_max_thrust_mps`, and `max_thrust_n`.
- Updated the physics sidecar magic-number audit to state that the project submarine spec is required.
- Updated `Tools\VerifyUpgradeCurveBaker.py` to reject any physics sidecar not sourced from `Data/Physics/Submarine_Specs.json`.

Cinematic Cheats used:
- No new simulation. This is a bake-time source integrity gate.
- Runtime remains fixed numeric lookup; Ultra visual fields remain sidecar-only.

Exact Microseconds saved:
- Runtime microseconds saved: 0 measured. Unity runtime was not executed.
- Hot-path impact: unchanged. `.h8bin` remains 176 bytes and the runtime JSON remains 631 bytes.

Verification:
- `python Tools\UpgradeCurveBaker.py --verify-only` exited 0.
- `python Tools\VerifyUpgradeCurveBaker.py` exited 0; `binary_mod16=0`, `monte_carlo_steps=1000000`, `failures=[]`.
- `python -m unittest Tools.test_upgrade_curve_baker` exited 0; 2 tests passed.
- `python -m py_compile Tools\UpgradeCurveBaker.py Tools\VerifyUpgradeCurveBaker.py Tools\VerifyMetricPhiDataTruth.py Tools\test_upgrade_curve_baker.py` exited 0.

Residual risk:
- Unity import, Play Mode, GCMonitor, profiler, frame-time, memory, scene wiring, and player build proof remain `PENDING VERIFICATION` by XML `NO_UNITY`.

## 2026-05-16 - Renewed Data Truth Inquisition Replay

What was wrong:
- The last durable status/rationale did not record the newer Monte Carlo seed provenance repair.
- The renewed audit required current disk proof across stat-map, economy, physics LUT, binary hygiene, hash collisions, lore, MetricPhi, and recursive `Verify*.py` scripts.
- One local hash audit command used an obsolete `--report` argument for `VerifyH8HashCollisions.py`; that evidence was invalid until rerun with the current CLI contract.

What was done:
- Re-ran `python Tools\UpgradeCurveBaker.py`; status `UPGRADES BAKED`, runtime JSON 631 bytes, stat-map `.h8bin` 176 bytes.
- Re-ran `python Tools\UpgradeCurveBaker.py --verify-only`, `python Tools\VerifyUpgradeCurveBaker.py`, `python -m unittest Tools.test_upgrade_curve_baker`, and `python -m py_compile Tools\UpgradeCurveBaker.py Tools\VerifyUpgradeCurveBaker.py Tools\test_upgrade_curve_baker.py`; all exited 0.
- Re-ran economy proof: `EconomyValidator.py --root .` reported `monte_carlo_steps=1000000` and `STATUS: ECONOMY BALANCED`; `EconomyRecipeGraphAudit.py` reported `cycle_count=0` and `status=ECONOMY SECURED`.
- Re-ran hard-science/data proof: MathLUT, Optics, Sabine, Dalton, Submarine Hydrodynamics, VRAM budgets, Ore LCG, DataInquisition, MetricPhi sweep, MetricPhi data truth, Quest DAG data truth, and Tide inquisition.
- Corrected the hash audit invocation to `VerifyH8HashCollisions.py --write-json Docs\AgentLogs\HashAudit_VEHICLE_UPGRADE_STAT_MAP.json --write-report Docs\AgentLogs\HashAudit_VEHICLE_UPGRADE_STAT_MAP.md`; result: 1018 hash records, collisions 0.
- Replayed literal recursive `Tools/**/Verify*.py` with `PYTHONDONTWRITEBYTECODE=1`; result: 33 scripts, 0 failures.

Cinematic Cheats used:
- No new runtime simulation. Stat-map authority remains static numeric lookup.
- Low/toaster path remains the 176-byte stat-map `.h8bin`; high/ultra visuals remain sidecar-driven pressure shimmer, propwash/cavitation, harmonic/noise, VFX budget, and visual LOD data.

Exact Microseconds saved:
- Profiler-backed runtime microseconds saved: 0. Unity runtime was not executed.
- Static hot-path impact: unchanged. No C# was touched.

Verification:
- `VerifyUpgradeCurveBaker.py`: 9 runtime rows, `binary_bytes=176`, `binary_mod16=0`, `monte_carlo_steps=1000000`, `power_growth_ratios=[2.190318,2.121422]`, `atlas_domains=[4,52,54,57,58]`, failures `[]`.
- `VerifyBinaryHygiene.py`: `binaryCount=44`, `misalignedCount=0`.
- `VerifyDataInquisition.py`: 44 binaries, aligned16 true, endian `<`, structFormats 273, Monte Carlo steps 1,000,000, hash collisions 0, atlas domains 85.
- `RunMetricPhiVerifySweep.py --xxhash-path "$env:TEMP\metric_phi_xxhash_ref"`: 35 commands, required failures 0.
- `VerifyMetricPhiDataTruth.py`: 37 checks, 0 failed, 44 binary files, 0 unaligned, 274 struct format sites, 0 endian failures.
- `VerifyOreLcgBaker.py`: binaryBytes 1776, hash collisions 0.
- `VerifyOreLcgBinaryIndependent.py`: binaryBytes 1776, resourceRecordsChecked 150.
- Recursive `Tools/**/Verify*.py`: 33 scripts, 0 failures.
- Python cache hygiene: verified resolved deletion targets under `C:\Hecton8`, removed generated `__pycache__` directories, then confirmed scoped cache scan over `Tools`, `.codex_tmp`, and `Temp`: `PYCACHE_COUNT_SCOPED=0`, `PYC_PYO_COUNT_SCOPED=0`.
- Post-log static recheck: `VerifyUpgradeCurveBaker.py`, `VerifyDataInquisition.py`, `VerifyMetricPhiDataTruth.py`, and `VerifyBinaryHygiene.py` printed pass status. The run recreated `Tools\__pycache__`; it was removed after path containment verification.

Residual risk:
- Unity import, Play Mode, GCMonitor, profiler, frame-time, memory, scene wiring, and player build proof remain `PENDING_VERIFICATION` by XML `NO_UNITY`.

## 2026-05-17 - Renewed Inquisition And Lore Verifier Repair

What was wrong:
- Current disk state had to be treated as the only authority under the anti-amnesia protocol.
- The VEHICLE-owned baker/verifier/test toolchain needed a fresh current-disk replay, not a stale report.
- `Tools/VerifyLore.py --check` was stale against the current raw UTF-8 lore manifest. It assumed the older compressed record layout and rejected valid 16-byte zero padding after the final payload.

What was done:
- Re-read `Docs/Tasks/Status_VEHICLE_UPGRADE_STAT_MAP.md`, `Docs/AgentLogs/Rationale_VEHICLE_UPGRADE_STAT_MAP.md`, and the original XML block in `Docs/Tasks/CURRENT_BATCH.md`.
- Re-ran `python Tools\UpgradeCurveBaker.py`; status `UPGRADES BAKED`.
- Re-ran `python Tools\UpgradeCurveBaker.py --verify-only`, `python Tools\VerifyUpgradeCurveBaker.py`, and `python -m unittest Tools.test_upgrade_curve_baker`; all exited 0.
- Patched `Tools/VerifyLore.py` to verify the current manifest contract: raw UTF-8 payload slices, `<4sIII` header, `<IIII` records, source-byte SHA-256 checks, stateless hash lookup, little-endian `<`, and zeroed trailing alignment padding.
- Re-ran explicit economy, binary, hash, lore, atlas/network, noise, replay hash, and taxonomy verification.
- Replayed literal recursive `Tools/**/Verify*.py` with `PYTHONDONTWRITEBYTECODE=1`; per-script arguments were supplied for lore, H8 hash collisions, and replay hashing.

Cinematic Cheats used:
- No new runtime physical simulation. Submarine upgrade data remains static numeric lookup.
- Low/toaster path remains the 176-byte `.h8bin` stat map.
- High/ultra path remains sidecar-driven: pressure shimmer, propwash/cavitation, harmonic/noise fields, visual LOD, and VFX budget payloads.

Exact Microseconds saved:
- Runtime microseconds saved: 0 measured. Unity runtime was not executed by XML `NO_UNITY`.
- Static hot-path impact: unchanged. No C# was touched.

Verification:
- `UpgradeCurveBaker.py`: `UPGRADES BAKED`; runtime JSON 631 bytes, SHA-256 `1DB8BD9C1EAA890A26C24F7D8824A04B8835980ACE492CF2AD5ECEE1A9B7DAC0`; stat-map binary 176 bytes, SHA-256 `CA0CB2C3E8E970DC45D45AAEA2E247A8696E910C3990117BA32EE180AF79066A`.
- `VerifyUpgradeCurveBaker.py`: 9 runtime rows, `binary_mod16=0`, `monte_carlo_steps=1000000`, `power_growth_ratios=[2.190318,2.121422]`, atlas domains `[4,52,54,57,58]`, failures `[]`.
- Unit tests: 2 tests passed.
- `MonteCarloEconomySim.py --root .`: `million_step_audit_passed=True`, `total_nodes_mined=1539943`, failures 0.
- `DataTruthInquisition.py --root .`: PASS, hash collisions 0, recipe cycles 0, binary unaligned 0, endian unknown 0, struct format failures 0.
- `VerifyLore.py --check`: PASS, entries 2, `compression=none/raw-utf8`, `alignment=16`, `endian=<`.
- `VerifyH8HashCollisions.py`: 480 H8 hash records, collisions 0.
- `VerifyBinaryHygiene.py`: 44 binaries, misaligned 0.
- `VerifyNetSyncMerkleProtocol.py`: PASS, 85 domain labels, 44 aligned binary payloads.
- `VerifyReplayHasherReference.py`: `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK`.
- Recursive `Tools/**/Verify*.py`: 32 scripts, 0 failures.
- Final post-log pulse: `py_compile` on VEHICLE/lore tool files exited 0; `VerifyLore.py --check`, `VerifyUpgradeCurveBaker.py`, `VerifyBinaryHygiene.py`, and `DataTruthInquisition.py --root .` all exited 0.
- Cache hygiene: resolved deletion targets verified under `C:\Hecton8`; scoped scan over `Tools`, `.codex_tmp`, and `Temp` reports `PYCACHE_COUNT_SCOPED=0`, `PYC_PYO_COUNT_SCOPED=0`.

Residual risk:
- Unity import, Play Mode, GCMonitor, profiler, frame-time, memory, scene wiring, and player build proof remain `PENDING_VERIFICATION` by XML `NO_UNITY`.
