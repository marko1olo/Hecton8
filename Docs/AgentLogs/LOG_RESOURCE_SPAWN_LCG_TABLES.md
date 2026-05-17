# RESOURCE_SPAWN_LCG_TABLES Log

## 2026-05-16 - Ore LCG Matrix Bake And Inquisition Pass

What was wrong:
- Runtime-facing ore selection previously had no generated LCG probability matrix artifact for SHINOBU ingestion.
- First bake pass had a bad `table_version_hash32`: authored value `3854624960` did not match FNV-1a UTF-16 hash `3968294156` for `economy.ore_lcg_distribution.v1`.
- First bake pass had weak density/clump provenance. It was deterministic, but not hard enough for the data standard.
- JSON/CSV alone were not zero-cost ingestion data for SHINOBU.
- `VerifyLore.py --check` initially failed on `Docs/Lore/Archives/DeepReach_ColonyFailureArchive.md`.

What was done:
- Added `Tools/OreLcgBaker.py`.
- Added `Tools/test_ore_lcg_baker.py`.
- Generated `Data/Economy/Ore_Distribution.json`.
- Generated `Data/Economy/Ore_Distribution_Histogram.csv`.
- Generated `Data/Economy/Ore_Distribution.h8bin`.
- Generated `Docs/AgentLogs/OreLcgRuntimeStruct_RESOURCE_SPAWN_LCG_TABLES.md`.
- Created/updated `Docs/Tasks/Status_RESOURCE_SPAWN_LCG_TABLES.md`.
- Created/updated `Docs/AgentLogs/Rationale_RESOURCE_SPAWN_LCG_TABLES.md`.
- Re-baked lore through `VerifyLore.py --bake --check`; follow-up `VerifyLore.py --check` passed.

Cinematic Cheats used:
- No physical ore-near-ore simulation. Clumping is a deterministic byte threshold baked per biome.
- No runtime pressure/geology solve. Hydrostatic pressure is baked into integer clump metadata.
- Ultra visual detail is post-selection dressing only: deterministic gradients/harmonic noise never change resource authority.

Exact microseconds saved:
- Managed RNG replacement: estimated 120 us per 1000 rolls, STATIC_SOURCE only.
- Byte weight table over float cumulative weights: estimated 110 us per 1000 rolls, STATIC_SOURCE only.
- Prebaked density lookup: estimated 30 us per biome query, STATIC_SOURCE only.
- Prebaked clumping lookup: estimated 70 us per local spawn query, STATIC_SOURCE only.
- Binary cache over JSON parse: startup saving expected from 1632-byte memcpy instead of 30KB JSON parse; no profiler microsecond proof.

Verification:
- `python Tools\OreLcgBaker.py --root . --iterations 1000000`: exit 0, `STATUS: LCG BAKED`.
- `python -m unittest Tools.test_ore_lcg_baker`: exit 0, 2 tests OK.
- `python Tools\EconomyValidator.py --root . --negative-tests`: exit 0, `STATUS: ECONOMY BALANCED`, `monte_carlo_steps=1000000`, negative cases `10`.
- `python Tools\EconomyRecipeGraphAudit.py --root .`: exit 0, `cycle_count=0`, `is_dag=true`, `status=ECONOMY SECURED`.
- `python Tools\VerifyH8HashCollisions.py`: exit 0, `H8 hash records: 1018`, `HASH COLLISIONS: 0`.
- `python Tools\VerifyLore.py --check`: exit 0 after re-bake, `alignment=16`, `endian=<`.
- Binary audit: `Ore_Distribution.h8bin` size `1632`, offsets `64,224,1424,1472`, all 16-byte aligned, CRC match, JSON SHA match.
- H-Phi: `Docs/AgentLogs/HPhi_RESOURCE_SPAWN_LCG_TABLES.json` contains `status=PHI CALCULATED`; wrapper command timed out after 905s, so clean exit proof is absent.

In-game result:
- PENDING VERIFICATION. No Unity import, scene wiring, Play Mode, profiler, GCMonitor, or player build evidence was produced.

## 2026-05-16 - Second Data Truth Inquisition Re-run

What was wrong:
- The prior report still left one avoidable weak point: density/clump byte scale bounds were named constants, but the Q8 derivation was not embedded in the source data.
- H-Phi had produced an artifact previously, but the command wrapper timed out, so clean exit proof was absent.
- The broad `Verify*.py` surface had not been rerun from a single current-file sweep.

What was done:
- Patched `Tools/OreLcgBaker.py` so density byte range derives from 1/8 Q8 headroom (`[32,224]`) and clump byte range derives from 1/16 Q8 headroom (`[16,240]`).
- Re-baked `Data/Economy/Ore_Distribution.json`, `Data/Economy/Ore_Distribution_Histogram.csv`, and `Data/Economy/Ore_Distribution.h8bin` with `1,000,000` LCG iterations per biome.
- Added `byte_scale_derivation`, `density_u8_range`, `clump_u8_range`, and the source rarity formula to the JSON `science_basis`.
- Re-ran the broad `Tools\Verify*.py` sweep. Usage-only failures were rerun with correct declared arguments.
- Re-ran H-Phi to clean exit and updated `Docs/PROJECT_ATLAS.md`.

Cinematic Cheats used:
- Still no ore-neighbor physics. Clumping remains a deterministic hydrostatic-pressure-derived byte threshold.
- Ultra "God-Mode" fields remain visual-only: `ultra_visual_seed_u32`, three pressure-gradient channels, and harmonic noise. They cannot alter resource authority.
- Low-tier/toaster path remains the minimal integer table: biome hash, density byte, clump byte, total weight, and flat U8 weights.

Exact microseconds saved:
- No new runtime code was integrated; all microsecond gains remain STATIC_SOURCE estimates.
- Runtime parse avoidance remains a 1632-byte `.h8bin` memcpy path versus JSON parse, pending Data Monolith integration and startup profiling.

Verification:
- `python -m py_compile Tools\OreLcgBaker.py Tools\test_ore_lcg_baker.py; python Tools\OreLcgBaker.py --root . --iterations 1000000`: exit 0, `STATUS: LCG BAKED`.
- `python -m unittest Tools.test_ore_lcg_baker`: exit 0, 2 tests OK.
- `python Tools\EconomyValidator.py --root . --negative-tests`: exit 0, `STATUS: ECONOMY BALANCED`, `monte_carlo_steps=1000000`, negative cases `10`.
- `python Tools\EconomyRecipeGraphAudit.py --root .`: exit 0, `cycle_count=0`, `is_dag=true`, `status=ECONOMY SECURED`.
- `python Tools\VerifyH8HashCollisions.py`: exit 0, `H8 hash records: 1018`, `HASH COLLISIONS: 0`.
- `python Tools\VerifyLore.py --check`: exit 0, `alignment=16`, `endian=<`.
- `python Tools\Security\VerifyReplayHasherReference.py --xxhash-path %TEMP%\h8_xxhash_ref --fuzz-count 128`: exit 0, `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=338 shuffle=128`.
- Broad verify sweep: 27 `Tools\Verify*.py` scripts enumerated; runnable checks passed after correct-argument reruns.
- Ore byte audit: `ORE_AUDIT_ISSUES 0`; binary header `H8OL`, endian marker `0x01020304`, size `1632`, payload CRC `1433586587`, SHA-256 `5f2b5cd2a1d79d61a363c81a9c8cb1a430cf1d1c752a144315709635f8da93f0`.
- `python Tools\CalculateHPhi.py --root . --workers 2 --json-output Docs\AgentLogs\HPhi_RESOURCE_SPAWN_LCG_TABLES.json`: exit 0, elapsed `906.857` seconds, `DOMAIN_INDEX_COUNT=85`, `STATUS: PHI CALCULATED`.
- H-Phi values after rerun: `DataSovereignty=0.019743027`, `MemoryAlignment=0.516657853`, `BinarySafeRatio=0.018508726`, `HPhiStatic=6.7481e-05`.

In-game result:
- PENDING VERIFICATION. This pass hardened offline data and static architecture evidence. Unity import, scene wiring, Play Mode, profiler, GCMonitor, target hardware, and player build proof remain absent.

## 2026-05-16 - Ore Verifier Gate Added

What was wrong:
- Ore-specific binary/hash/math audit evidence existed only as inline shell output. That is repeatable, but it is not a stable project gate.
- The first run of the new verifier caught a real provenance wording defect: the JSON formula was correct, but the value did not explicitly label itself as hydrostatic.

What was done:
- Added `Tools/VerifyOreLcgBaker.py`.
- Added `Docs/AgentLogs/VerifyOreLcg_RESOURCE_SPAWN_LCG_TABLES.json`.
- Patched `Tools/OreLcgBaker.py` to emit `hydrostatic_pressure_pa = ...` in `science_basis`.
- Re-baked ore JSON/CSV/binary at `1,000,000` iterations.
- Re-ran H-Phi after adding the verifier source file.

Cinematic Cheats used:
- The verifier enforces that clumping is a pressure-derived byte proxy, not a runtime neighbor or physics solve.
- The verifier enforces that Ultra fields are visual-only and cannot affect deterministic resource authority.

Exact microseconds saved:
- No new runtime path was integrated. Savings remain static design estimates only.
- New value: bad data now fails before runtime ingestion; no frame-time claim is made.

Verification:
- First verifier run failed: `FAIL: hydrostatic pressure derivation missing`.
- After patch/re-bake: `python -m py_compile Tools\OreLcgBaker.py Tools\VerifyOreLcgBaker.py Tools\test_ore_lcg_baker.py; python Tools\OreLcgBaker.py --root . --iterations 1000000; python Tools\VerifyOreLcgBaker.py --root .`: exit 0, `VERIFY_ORE_LCG_STATUS: ORE_LCG_VERIFIED_STATIC_ONLY`.
- `Docs/AgentLogs/VerifyOreLcg_RESOURCE_SPAWN_LCG_TABLES.json`: binary bytes `1632`, endian `<`, offsets `64/224/1424/1472`, payload CRC `1433586587`, SHA-256 `5f2b5cd2a1d79d61a363c81a9c8cb1a430cf1d1c752a144315709635f8da93f0`, hash collisions `0`, sterile lore term hits `0`, industrial alias hits `130`.
- `python -m unittest Tools.test_ore_lcg_baker`: exit 0, 2 tests OK.
- `python Tools\EconomyValidator.py --root . --negative-tests`: exit 0, `STATUS: ECONOMY BALANCED`, `monte_carlo_steps=1000000`.
- `python Tools\EconomyRecipeGraphAudit.py --root .`: exit 0, `cycle_count=0`, `is_dag=true`, `status=ECONOMY SECURED`.
- `python Tools\VerifyH8HashCollisions.py`: exit 0, `HASH COLLISIONS: 0`.
- `python Tools\VerifyLore.py --check`: exit 0, `alignment=16`, `endian=<`.
- `python Tools\VerifyDataInquisition.py`: exit 0, `DATA_INQUISITION_VERIFIED_STATIC_ONLY`.
- `python Tools\CalculateHPhi.py --root . --workers 2 --json-output Docs\AgentLogs\HPhi_RESOURCE_SPAWN_LCG_TABLES.json`: exit 0, elapsed `526.638` seconds, `DOMAIN_INDEX_COUNT=85`, `HPhiStatic=6.7481e-05`.

In-game result:
- PENDING VERIFICATION. Static data gate is stronger; Unity runtime proof is still absent.

## 2026-05-16 - Independent Binary Verification And Clean Verify Sweep

What was wrong:
- The ore-specific verifier imported the baker, so binary proof still shared constants and struct formats with the writer.
- A broad `Verify*.py` sweep produced sandbox/ACL false negatives before the final clean run.

What was done:
- Added `Tools/VerifyOreLcgBinaryIndependent.py`.
- Added `Docs/AgentLogs/VerifyOreLcgBinaryIndependent_RESOURCE_SPAWN_LCG_TABLES.json`.
- Verified the ore binary without importing `OreLcgBaker.py`.
- Repaired workspace-local `Temp\h8_xxhash_ref` ACL so the replay hasher reference verifier could import `xxhash`.
- Reran the full `Verify*.py` sweep under escalation to avoid locked-binary sandbox false negatives.
- Reran H-Phi under escalation after the non-escalated process-pool scan hit a sandbox `PermissionError`.

Cinematic Cheats used:
- The independent verifier enforces that clump is a byte proxy and Ultra data is visual-only.
- No runtime physics/geology/neighbor simulation was added.

Exact microseconds saved:
- No runtime code was integrated. Runtime savings remain STATIC_SOURCE estimates only.
- Added value is data-gate failure before ingestion, not frame-time proof.

Verification:
- `python Tools\VerifyOreLcgBinaryIndependent.py --root .`: exit 0, `ORE_LCG_BINARY_INDEPENDENT_VERIFIED_STATIC_ONLY`.
- Independent binary report: bytes `1632`, endian `<`, header `H8OL`, offsets `64/224/1424/1472`, CRC `1433586587`, SHA-256 `5f2b5cd2a1d79d61a363c81a9c8cb1a430cf1d1c752a144315709635f8da93f0`, resource records checked `150`, failures `[]`.
- `python Tools\VerifyOreLcgBaker.py --root .`: exit 0, `ORE_LCG_VERIFIED_STATIC_ONLY`.
- `python Tools\EconomyValidator.py --root . --negative-tests`: escalated rerun exit 0, `STATUS: ECONOMY BALANCED`, `monte_carlo_steps=1000000`.
- `python Tools\Security\VerifyReplayHasherReference.py --xxhash-path Temp\h8_xxhash_ref --fuzz-count 128`: exit 0, `XXH3_REFERENCE_AND_SHUFFLE_FUZZ_OK xxh3=338 shuffle=128`.
- Full escalated `Verify*.py` sweep: `33` scripts, `VERIFY_FAILURE_COUNT 0`.
- `python Tools\CalculateHPhi.py --root . --workers 2 --json-output Docs\AgentLogs\HPhi_RESOURCE_SPAWN_LCG_TABLES.json`: escalated rerun exit 0, `DOMAIN_INDEX_COUNT=85`, `HPhiStatic=6.7481e-05`.

In-game result:
- PENDING VERIFICATION. Static verification is stronger; Unity import, Play Mode, profiler, GCMonitor, target hardware, and player build evidence remain absent.

## 2026-05-17 - Toaster Binary Payload And Global Hygiene Re-audit

What was wrong:
- Active `Docs/Tasks/CURRENT_BATCH.md` no longer contains `RESOURCE_SPAWN_LCG_TABLES`; the original XML had moved to `Docs/Archive/Batch_GIT_SYNC_REBASE/CURRENT_BATCH_local_auxiliary_20260517.md`.
- The ore JSON toaster contract listed `weight_matrix_u8_flat`, but the minimal binary section needed explicit proof that it also carried the 150-byte flat weight table.
- Global binary hygiene had real adjacent debt: lore blob trailing bytes, `Data/Balance/Baked/Babel_Dictionary.h8bin` at `1284` bytes, and `.binlog` diagnostic files being counted as game binaries.

What was done:
- Re-extracted the original XML from archive and kept the task identity as `RESOURCE_SPAWN_LCG_TABLES | DATA/ECONOMY | 15`.
- Regenerated/verified `Ore_Distribution.h8bin` as `1776` bytes with minimal LOD payload `190` bytes, aligned section `192` bytes, `minimal_weight_matrix_bytes=150`, ultra offset `1616`, CRC `2957493204`, SHA-256 `60f9a95ec619b4c9b7c168a01ac308415190df44b545fb1f722a11e983709c06`.
- Re-baked lore with `VerifyLore.py --bake`, then `VerifyLore.py --check` passed.
- Padded `Data/Balance/Baked/Babel_Dictionary.h8bin` to `1296` bytes and updated its header file length and payload CRC.
- Patched `Tools/VerifyBinaryHygiene.py` to scan only `.bin` and `.h8bin` suffixes, not arbitrary `.binlog` evidence.
- Removed temporary ore scratch paths under `.codex_tmp` and `Data/Economy`.

Cinematic Cheats used:
- Ore clumping remains a hydrostatic-pressure-derived byte proxy. No neighbor physics, per-node geology simulation, or runtime ore clustering truth was added.
- Ultra fields remain visual-only: deterministic seed, high-res gradient bands, and harmonic noise. They do not alter resource authority.

Exact microseconds saved:
- Runtime path is still not integrated. Static estimate only: low tier can read one compact minimal LOD block instead of scanning resource records or parsing JSON.
- New binary ingest payload is `1776` bytes total; startup/frame savings remain pending Data Monolith integration and profiler proof.

Verification:
- Original XML: found in `Docs/Archive/Batch_GIT_SYNC_REBASE/CURRENT_BATCH_local_auxiliary_20260517.md`.
- `python -B Tools\VerifyOreLcgBaker.py --root .`: exit 0, binary bytes `1776`, hash collisions `0`, runtime proof `PENDING_VERIFICATION`.
- `python -B Tools\VerifyOreLcgBinaryIndependent.py --root .`: exit 0, resource records checked `150`, binary bytes `1776`.
- `python -B -m unittest Tools.test_ore_lcg_baker`: exit 0, 2 tests OK.
- `python -B Tools\EconomyValidator.py --root . --negative-tests`: exit 0, `STATUS: ECONOMY BALANCED`, `monte_carlo_steps=1000000`, negative cases `6`.
- `python -B Tools\EconomyRecipeGraphAudit.py --root .`: exit 0, `cycle_count=0`, `is_dag=true`, `status=ECONOMY SECURED`.
- Full `Verify*.py` sweep: `33` scripts, `VERIFY_FAILURE_COUNT 0`.
- `python Tools\CalculateHPhi.py --workers 2 --json-output Docs\AgentLogs\HPhi_RESOURCE_SPAWN_LCG_TABLES.json`: exit 0, `domain_index_count=85`, `DataSovereignty=0.019743027`, `MemoryAlignment=0.516657853`, `BinarySafeRatio=0.018508726`, `HPhiStatic=6.7481e-05`.

In-game result:
- PENDING VERIFICATION. Static data is colder and stricter; Unity import, scene wiring, Play Mode, profiler, GCMonitor, target hardware, and player build evidence remain absent.

## 2026-05-17 - Final Verify Sweep Contract Repair

What was wrong:
- The archived XML extraction regex used during the reset was too narrow for `<AGENT_PROMPT id="RESOURCE_SPAWN_LCG_TABLES" role="..." chat_name="...">`. Attribute-tolerant extraction confirmed `TASK_COUNT=15`.
- A broad manual verifier sweep found current evidence debt outside the ore table: `VerifyMetricPhiDataTruth.py` and `VerifyQuestDagDataTruth.py` failed after the Metric Phi sweep report was stale/failed.
- Root cause: `VerifyMetricPhiDataTruth.py` required exactly `35` commands even for `RunMetricPhiVerifySweep.py`'s intentional pre-final self-check payload, where `selfCheckPending=true` and only `34` non-self commands have completed.

What was done:
- Patched `Tools/VerifyMetricPhiDataTruth.py` so the command count check accepts final `35`, or pre-final `34` only when `selfCheckPending=true`; `requiredFailures` still must be `0`.
- Reran `python -B Tools\RunMetricPhiVerifySweep.py --xxhash-path Temp\h8_xxhash_ref`; it exited 0 with `commands=35`, `required_failures=0`, `VERIFY_SWEEP_PASS`.
- Reran standalone `python -B Tools\VerifyMetricPhiDataTruth.py`; it exited 0 with `checks=37 failed=0`, `binary_files=46`, `struct_format_sites=133`, `endian_failures=0`.
- Reran standalone `python -B Tools\VerifyQuestDagDataTruth.py`; it exited 0 with `checks=10 failed=0`.
- Reran the enumerated `Verify*.py` sweep over `33` scripts, including `Tools\Taxonomy\verify_taxonomy.py`; final result `VERIFY_FAILURE_COUNT 0`.
- Removed `.codex_tmp\OreLcgBakerTests` after unit tests recreated it. Other `.codex_tmp` directories owned by concurrent agents were not touched.

Cinematic Cheats used:
- None added. This was verification contract repair. Ore clump remains a hydrostatic-pressure-derived byte proxy, and Ultra ore fields remain visual-only.

Exact microseconds saved:
- Runtime: `0` proven. This pass repaired static evidence flow only.
- Static review cost saved: future sweep runs no longer poison `METRIC_PHI_VERIFY_SWEEP.json` with a false self-check failure.

Verification:
- `python -B -m py_compile Tools\VerifyMetricPhiDataTruth.py Tools\RunMetricPhiVerifySweep.py Tools\VerifyQuestDagDataTruth.py`: exit 0.
- `python -B Tools\RunMetricPhiVerifySweep.py --xxhash-path Temp\h8_xxhash_ref`: exit 0, `commands=35 required_failures=0`.
- `python -B Tools\VerifyMetricPhiDataTruth.py`: exit 0, `checks=37 failed=0`.
- `python -B Tools\VerifyQuestDagDataTruth.py`: exit 0, `checks=10 failed=0`.
- Final enumerated `Verify*.py` sweep: `VERIFY_TOTAL 33`, `VERIFY_FAILURE_COUNT 0`.

In-game result:
- PENDING VERIFICATION. CLI/static data gates are clean; Unity import, Play Mode, profiler, GCMonitor, target hardware, and player build evidence remain absent.

## 2026-05-17 - Loop 11 Renewed Data Truth Inquisition

What was wrong:
- The user demanded another reset and current-disk proof. Previous verifier output was not treated as current proof.
- A manual binary-header read made during this pass initially used the wrong field mapping; the verifier-declared labels were re-read and the header was parsed again correctly.
- H-Phi JSON schema shifted from the older `runtime_scores` shape to `h_phi_audit`; reports had to read the current schema instead of assuming stale keys.

What was done:
- Re-read `Docs/Tasks/Status_RESOURCE_SPAWN_LCG_TABLES.md`, `Docs/AgentLogs/Rationale_RESOURCE_SPAWN_LCG_TABLES.md`, and the archived XML directive. Extracted `TASK_COUNT=15`.
- Re-read relevant mandates and the domain map: data-oriented resources, deterministic RNG slot-machine law, zero-GC evidence boundary, anti-lie evidence filter, `Actual Domains of Project`, and PROJECT_ATLAS rows for domains `4`, `17`, and `38`.
- Re-baked ore data with `python -B Tools\OreLcgBaker.py --root . --iterations 1000000`.
- Reran economy, recipe graph, ore direct verifier, independent ore binary verifier, binary hygiene, data inquisition, FNV collision, lore, optics, Dalton, Sabine, Snell, H-Phi, Metric Phi data truth, and the full `Verify*.py` enumeration.
- Used RESOURCE-scoped Metric Phi output `Docs\AgentLogs\MetricPhiDataTruth_RESOURCE_SPAWN_LCG_TABLES_LOOP11_FULL.json` to avoid unnecessary global report churn where the tool allowed it.
- Removed `.codex_tmp\OreLcgBakerTests` after the full verifier run recreated it. Concurrent-agent scratch under `.codex_tmp\metric_phi_selfcheck` and `.codex_tmp\net_float_crime` was not touched.

Cinematic Cheats used:
- Ore clumping remains a hydrostatic-pressure-derived byte proxy, not per-node neighbor physics.
- Beer-Lambert, Dalton, and Sabine were not falsely applied to ore probability. Their owner LUTs were verified separately.
- Ultra ore fields remain visual-only: seed, gradient bands, and harmonic noise after deterministic resource selection.

Exact microseconds saved:
- Runtime: `0` measured. Unity/Profiler/GCMonitor proof is absent.
- Static ingest shape remains cheaper: `Ore_Distribution.h8bin` is `1776` bytes, little-endian, 16-byte aligned, with a compact toaster section containing density, clump, totals, and `150` flat weights.

Verification:
- `python -B Tools\OreLcgBaker.py --root . --iterations 1000000`: exit 0, `STATUS: LCG BAKED`, `binaryBytes=1776`, `safeShallowsTitaniumBp=5000`.
- `python -B Tools\EconomyValidator.py --root . --negative-tests`: exit 0, `STATUS: ECONOMY BALANCED`, `monte_carlo_steps=1000000`, `negative_cases=10`.
- `python -B Tools\EconomyRecipeGraphAudit.py --root .`: exit 0, `cycle_count=0`, `is_dag=true`, `status=ECONOMY SECURED`.
- `python -B Tools\VerifyOreLcgBaker.py --root .`: exit 0, `binaryBytes=1776`, `hashCollisions=0`.
- `python -B Tools\VerifyOreLcgBinaryIndependent.py --root .`: exit 0, `resourceRecordsChecked=150`.
- Correct binary header parse: `magic=H8OL`, `headerBytes=64`, `endianMarker=0x01020304`, offsets `64/224/1424/1616`, `payloadCrc32=2957493204`, `fnvCollisionCount=0`, SHA-256 `60f9a95ec619b4c9b7c168a01ac308415190df44b545fb1f722a11e983709c06`.
- `python -B Tools\VerifyBinaryHygiene.py`: exit 0, `binaryCount=46`, `misalignedCount=0`.
- `python -B Tools\VerifyDataInquisition.py`: exit 0, `atlasDomains=85`, `structFormats=273`, `monteCarloSteps=1000000`, `hashCollisions=0`.
- `python -B Tools\VerifyH8HashCollisions.py`: exit 0, `H8 hash records=1046`, `HASH COLLISIONS=0`.
- `python -B Tools\VerifyLore.py --check`: exit 0, `CHECK OK`.
- Hard-science owner gates: optics, Dalton, Sabine, and Snell verifiers all exited 0.
- `python Tools\CalculateHPhi.py --workers 2 --json-output Docs\AgentLogs\HPhi_RESOURCE_SPAWN_LCG_TABLES.json`: exit 0, `DOMAIN_INDEX_COUNT=85`, `RUNTIME_H_PHI_STATIC=6.7481e-05`; current JSON reports `runtime_data_sovereignty_increased_by_this_pass=false`, `runtime_data_sovereignty_score=0.019743027`.
- Full `Verify*.py` enumeration: `VERIFY_TOTAL 33`, `VERIFY_FAILURE_COUNT 0`.

In-game result:
- PENDING VERIFICATION. Static data is verified; Unity import, scene wiring, Play Mode, profiler, GCMonitor, target hardware, and player build evidence remain absent.
