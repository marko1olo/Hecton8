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
