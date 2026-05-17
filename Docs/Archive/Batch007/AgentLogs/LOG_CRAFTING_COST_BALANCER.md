# LOG - CRAFTING_COST_BALANCER

## 2026-05-16 - Crafting Cost Balance Pass

What was wrong:
- No `Data/Economy/Crafting_Costs.json` table existed for the assigned 50 craftable items.
- Existing economy validation covered `Recipes.json` but did not prove the assigned 50-item crafting-cost matrix, Tier 2 tool gates, Standard O2 starter timing, or CSV parity.
- A chat-only status would violate the batch reporting protocol.

What was done:
- Added `Tools/CraftingCostsBaker.py` to deterministically bake the new economy table.
- Added `Data/Economy/Crafting_Costs.json` with 50 recipes, exact `PowerCost_kWh`, exact `FabricationTimeSeconds`, precomputed FNV-1a hashes, and exact input/output mass parity.
- Added `Data/Economy/Crafting_Costs.csv` as the readable designer review export.
- Added `Data/Economy/Crafting_Costs.h8bin` plus `Crafting_Costs.manifest.json` for 16-byte aligned little-endian SHINOBU/Data Monolith ingestion.
- Added `Data/Economy/Crafting_MonteCarlo_Audit.json`, `Crafting_Hash_Audit.json`, and `Crafting_DataSovereignty_Audit.json`.
- Extended `Tools/EconomyValidator.py` to validate the crafting table, CSV parity, hash fields, Tier 2 required Tier 1 tools, no-profit deconstruction, starter O2 timing, and four new crafting negative tests.
- Added `Tools/VerifyCraftingCosts.py` and `Tools/CraftingEconomyMonteCarlo.py`.
- Wrote `Docs/Tasks/Status_CRAFTING_COST_BALANCER.md` and `Docs/AgentLogs/Rationale_CRAFTING_COST_BALANCER.md`.

Cinematic Cheats used:
- Data-only bake instead of runtime economy derivation. The fabricator consumes precomputed scalar cost/time/mass/hash fields or aligned binary records.
- Non-consumed required tools for Tier 2 gating. This preserves progression without fake material sinks.

Exact Microseconds saved:
- Runtime measured savings: 0 us claimed. No Unity runtime/profiler artifact was produced.
- Static estimate only: runtime hash calculation, JSON text parsing, recipe closure summation, and starter-path simulation were avoided by offline data/binary; microsecond value remains PENDING VERIFICATION under `QA_Evidence_Text_Filter_Audit`.

Balance facts:
- Recipe count: 50.
- Tier cost ratios: 2.931 and 3.173.
- Tier power ratios: 3.002 and 2.959.
- Standard O2 Tank starter total: 4.446 minutes at 30 kW.
- Minified JSON: 126,840 bytes, one line.
- Binary: 7,424 bytes, 16-byte aligned, little-endian `<`, payload CRC32 `1295072744`.
- Monte Carlo: 1,000,000 deterministic steps, `profit_steps=0`.
- Hash audit: 341 CRAFTING_COST_BALANCER hash pairs, `collisions=0`; full economy validator reports 448 unique economy IDs.

Verification:
- `python -B Tools\CraftingCostsBaker.py` -> recipes=50, cost/power ratios reported, starter O2 = 4.446.
- `python -B Tools\VerifyCraftingCosts.py` -> binary/header/CRC/SHA/hash/PROJECT_ATLAS checks passed.
- `python -B Tools\CraftingEconomyMonteCarlo.py --steps 1000000` -> `profit_steps=0`.
- `python -B Tools\EconomyValidator.py --root . --negative-tests` -> `STATUS: ECONOMY BALANCED`, 10 negative cases failed as expected.
- `python -B -m py_compile Tools\EconomyValidator.py Tools\CraftingCostsBaker.py Tools\VerifyCraftingCosts.py Tools\CraftingEconomyMonteCarlo.py` -> passed.
- `git diff --check -- Tools\EconomyValidator.py Tools\CraftingCostsBaker.py Tools\VerifyCraftingCosts.py Tools\CraftingEconomyMonteCarlo.py` -> passed; Git emitted only an LF-to-CRLF warning.
- Trailing whitespace scan on touched new files -> empty list.

Mandates followed:
- DATA_Inventory_Resources_Items_SOA_Layout.txt
- LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- MATH_Deterministic_RNG_SlotMachine.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- QA_Evidence_Text_Filter_Audit.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt

Regression model:
- CPU: no runtime C# changed; offline Python validation cost only.
- GC: no gameplay path changed; runtime GC remains PENDING VERIFICATION.
- Memory: one 126,840-byte JSON, one 7,424-byte binary, manifest/audit JSONs, and one CSV added; runtime memory impact remains PENDING VERIFICATION until imported by the Data Monolith.
- Cadence: no Tick/Update/Unity cadence touched.
- Correctness: CLI validator proves table structure, hashes, mass conservation, tier gates, starter O2 timing, and no-profit deconstruction.

STATUS: VERIFIED MASTER GRADE for offline CLI artifacts. Unity import, Play Mode, profiler, player build, and GCMonitor remain PENDING VERIFICATION.

## 2026-05-16 - Inquisition Hardening Rerun

What was wrong:
- Source audit found valid but weakly documented literals in `Tools/CraftingCostsBaker.py` for packing scale factors, starter pacing, reclaim ratio, cost-class bands, and God-Mode visual budgets.
- A parallel Monte Carlo rerun timed out under process contention; an isolated rerun was required before any fresh claim.

What was done:
- Promoted the remaining baker literals into named constants.
- Added `binary_scale_model`, `starter_edge_model`, and `visual_overkill_model` metadata to `Data/Economy/Crafting_Costs.json`.
- Rebaked JSON/CSV/binary/manifest.
- Refreshed `Crafting_MonteCarlo_Audit.json` after the JSON SHA changed.

Cinematic Cheats used:
- Toaster path stays binary-record-only: hashes, grams, milli-units, mWh, deciseconds, and flat offsets.
- God-Mode path gets extra data only: fabricator FX hash, gradient LUT hash, gradient detail pack, harmonic noise seed hash, harmonic noise pack, spark particles, steam puffs, harmonic octaves, and hydraulic-jolt milliseconds.

Exact Microseconds saved:
- Runtime measured savings: 0 us claimed. No Unity runtime/profiler artifact exists.
- Static import-side effect: binary payload is 7,424 bytes and avoids text parsing for the Data Monolith path; microseconds remain PENDING VERIFICATION.

Verification:
- `python -B Tools\CraftingCostsBaker.py` -> v2 binary emitted payload CRC32 `1295072744`, binary SHA256 `632fdabb2a57dbe115f18fecc8a23a6ddda2bacb0cf53ea472f29b81a0377f69`.
- `python -B Tools\CraftingEconomyMonteCarlo.py --steps 1000000` -> `profit_steps=0`.
- `python -B Tools\VerifyCraftingCosts.py` -> binary/hash/PROJECT_ATLAS checks passed.
- `python -B Tools\EconomyValidator.py --root . --negative-tests` -> `STATUS: ECONOMY BALANCED`, 10 negative cases failed as expected.
- `python -B -m py_compile Tools\CraftingCostsBaker.py Tools\VerifyCraftingCosts.py Tools\CraftingEconomyMonteCarlo.py Tools\EconomyValidator.py` -> passed.
- `git diff --check -- ...` -> passed; Git emitted only the existing LF-to-CRLF warning for `Tools/EconomyValidator.py`.
- Trailing-whitespace scan -> `[]`.
- Corrected XML extractor -> `XML_FOUND task_count=15`.

STATUS: VERIFIED MASTER GRADE for offline CLI artifacts. Unity runtime remains PENDING VERIFICATION.

## 2026-05-17 - Source Contract Hardening Pass

What was wrong:
- The restored 10-case economy validator enforced core crafting invariants but was not included in `VerifyCraftingSourceContracts.py`.
- Raw threshold values remained in validator/verifier expressions after the negative-test restoration.

What was done:
- Added named constants to `Tools\EconomyValidator.py` for matrix size, primary recipe count, tolerances, rarity distance normalization, first-sub batch bounds, negative-test mass drift, and Monte Carlo minimum steps.
- Added named tolerance constants to `Tools\VerifyCraftingCosts.py`.
- Expanded `Tools\VerifyCraftingSourceContracts.py` to scan `Tools\EconomyValidator.py` in addition to `VerifyCraftingCosts.py` and `CraftingEconomyMonteCarlo.py`.

Cinematic Cheats used:
- No runtime derivation added. The game still reads precomputed scalar/binary records; all physical/economy derivation remains offline tooling.

Exact Microseconds saved:
- Runtime measured savings: 0 us claimed. Static source hygiene only.
- Import-side stability improved by keeping Toaster and full binary contracts guarded by the same validator/source-contract chain.

Verification:
- `python -B -m py_compile Tools\EconomyValidator.py Tools\VerifyCraftingCosts.py Tools\VerifyCraftingSourceContracts.py Tools\CraftingEconomyMonteCarlo.py Tools\CraftingCostsBaker.py Tools\RunFullVerifySweep.py` -> passed.
- `python -B Tools\VerifyCraftingSourceContracts.py` -> `literal_hit_count=0`, consumers: `VerifyCraftingCosts.py`, `CraftingEconomyMonteCarlo.py`, `EconomyValidator.py`.
- `python -B Tools\VerifyCraftingCosts.py` -> 50 recipes, 342 hash pairs, 0 collisions, aligned endian `<`.
- `python -B Tools\CraftingEconomyMonteCarlo.py --steps 1000000` -> `profit_steps=0`.
- `python -B Tools\EconomyValidator.py --root . --negative-tests` -> `STATUS: ECONOMY BALANCED`, `negative_cases=10`.
- `python -B Tools\VerifyDataInquisition.py --report Docs\AgentLogs\Data_Inquisition_CRAFTING_COST_BALANCER.json` -> 46 binaries, aligned16 true, endian `<`, atlas domains `85`, hash collisions `0`.
- `python -B Tools\VerifyH8HashCollisions.py --write-json Docs\AgentLogs\H8Hash_Collision_CRAFTING_COST_BALANCER.json` -> 1046 records, 0 collisions.
- `python -B Tools\VerifyBinaryHygiene.py --report Docs\AgentLogs\BinaryHygiene_CRAFTING_COST_BALANCER.json` -> 46 binaries, 0 misaligned.
- `python -B Tools\RunFullVerifySweep.py` -> 29 verifier scripts, `failed_count=0`.
- `git diff --check` and trailing-whitespace scan -> passed.

STATUS: VERIFIED MASTER GRADE for offline CLI artifacts. Unity runtime remains PENDING VERIFICATION.

## 2026-05-17 - OSHINO Static Evidence Revalidation

What was wrong:
- The live batch file rolled forward and no longer carried `CRAFTING_COST_BALANCER`; the original assignment had to be recovered from archive.
- The global Metric Phi evidence files contained stale Quest DAG failure state even though direct current quest and lore checks could pass.
- `EconomyValidator.py --negative-tests` had regressed to 6 malformed cases after tool restoration; the previous 10-case claim was not valid for current disk state.

What was done:
- Re-extracted the original XML from `Docs\Archive\Batch_GIT_SYNC_REBASE\CURRENT_BATCH_local_auxiliary_20260517.md`.
- Rebound Metric Phi / Quest DAG evidence by rerunning lore, quest, Metric Phi data-truth, and quest data-truth verifiers without weakening hash/lore validation.
- Added four crafting-specific negative cases to `Tools\EconomyValidator.py`: output mass drift, Tier 2 missing Tier 1 tool, zero fabrication time, and Monte Carlo profit-loop tamper.
- Reran the economy bake, million-step Monte Carlo, crafting binary readback, source contract verifier, economy validator, data inquisition, and full `Verify*.py` sweep.

Cinematic Cheats used:
- Physical kWh derivation stays offline; runtime consumers read precomputed scalar kWh/mWh.
- Toaster path uses `Crafting_Costs_Toaster.h8bin` fixed records; God-Mode consumes packed gradient/noise visual records from the full H8CR blob.

Exact Microseconds saved:
- Runtime measured savings: 0 us claimed. No Unity profiler, GCMonitor, Play Mode, or player-build artifact exists.
- Static ingestion guard: full crafting binary is 7,424 bytes, Toaster binary is 2,464 bytes, both 16-byte aligned.

Verification:
- `python -B Tools\CraftingCostsBaker.py` -> 50 recipes, cost ratios `2.931/3.173`, power ratios `2.916/2.966`, starter O2 `4.446`, full SHA256 `632fdabb2a57dbe115f18fecc8a23a6ddda2bacb0cf53ea472f29b81a0377f69`, Toaster SHA256 `67d24c52a6a14dcdd7d0488a315067a7eacefc78d444769c7ce9ea959dacc752`.
- `python -B Tools\CraftingEconomyMonteCarlo.py --steps 1000000` -> `profit_steps=0`, max value delta `-1000`, max mass delta `-400000 mg`, max energy delta `-133000 mWh`.
- `python -B Tools\EconomyValidator.py --root . --negative-tests` -> `STATUS: ECONOMY BALANCED`, `negative_cases=10`.
- `python -B Tools\VerifyCraftingCosts.py` -> 50 recipes, 171 ingredients, 38 tool records, 50 God-Mode visual records, 342 hash pairs, 0 collisions.
- `python -B Tools\VerifyDataInquisition.py --report Docs\AgentLogs\Data_Inquisition_CRAFTING_COST_BALANCER.json` -> 46 binaries, aligned16 true, 11 manifests, endian `<`, 273 struct formats, 1,000,000 Monte Carlo steps, 0 hash collisions, 85 atlas domains.
- `python -B Tools\RunFullVerifySweep.py` -> 29 top-level verifier scripts, `failed_count=0`, report `Docs\AgentLogs\VerifySweep_CRAFTING_COST_BALANCER.json`.
- `python -B -m py_compile Tools\EconomyValidator.py Tools\VerifyCraftingCosts.py Tools\CraftingCostsBaker.py Tools\CraftingEconomyMonteCarlo.py Tools\RunFullVerifySweep.py` -> passed.

STATUS: VERIFIED MASTER GRADE for offline CLI artifacts. Unity runtime remains PENDING VERIFICATION.

## 2026-05-16 - Source Contract Verifier Gate

What was wrong:
- The literal hardening proof depended on transient shell scans.
- That is not enough for AAA data hygiene; the next edit could reintroduce raw masks/scales and still pass the older sweep.

What was done:
- Added `Tools\VerifyCraftingSourceContracts.py`.
- The verifier scans owned consumer files for raw binary masks, scale multipliers, 16-byte divisors, CRC masks, and implicit surface exponent literals.
- It validates JSON/binary contract fields against named constants exported by `Tools\CraftingCostsBaker.py`.
- It writes `Docs\AgentLogs\Crafting_SourceContract_Audit.json`.
- Reran `Tools\RunFullVerifySweep.py`; the sweep now covers 26 verifier scripts.

Cinematic Cheats used:
- No runtime logic was added. The change is an offline source contract tripwire.

Exact Microseconds saved:
- Runtime measured savings: 0 us claimed.
- Offline QA wall time: latest full verifier sweep took approximately 882.4 seconds in this shell.

Verification:
- `python -B Tools\VerifyCraftingSourceContracts.py` -> `literal_hit_count=0`.
- `python -B Tools\RunFullVerifySweep.py` -> 26 scripts, `failed_count=0`.

STATUS: VERIFIED MASTER GRADE for offline CLI artifacts. Unity runtime remains PENDING VERIFICATION.

## 2026-05-16 - Full Verify Sweep Pass

What was wrong:
- The previous OSHINO pass verified owned economy scripts and selected hard-science domains, but the escalation explicitly demanded `Verify*.py` scripts.
- A bare `VerifyLore.py` run is not a verification mode; it prints usage and exits `2`, so a naive sweep creates a false failure.

What was done:
- Added `Tools\RunFullVerifySweep.py`.
- The runner enumerates all `Tools\Verify*.py` scripts, runs them with `python -B`, and writes `Docs\AgentLogs\VerifySweep_CRAFTING_COST_BALANCER.json`.
- Added required script-specific args for `VerifyLore.py --check`.
- Reran the sweep after fixing the runner.

Cinematic Cheats used:
- No runtime simulation was added. This is an offline verification harness.
- Data remains stateless and binary-backed; the runner only proves the current verifier surface is executable.

Exact Microseconds saved:
- Runtime measured savings: 0 us claimed.
- Verification wall time: approximately 460.1 seconds in the latest shell run; this is offline QA cost, not frame cost.

Verification:
- `python -B Tools\RunFullVerifySweep.py` -> 24 verifier scripts, `failed_count=0`.
- Report written to `Docs\AgentLogs\VerifySweep_CRAFTING_COST_BALANCER.json`.

STATUS: VERIFIED MASTER GRADE for offline CLI artifacts. Unity runtime remains PENDING VERIFICATION.

## 2026-05-16 - Source Literal Hardening Pass

What was wrong:
- `Tools\EconomyValidator.py` and `Tools\VerifyCraftingCosts.py` still duplicated binary-pack masks, alignment divisors, scale factors, and CRC masks as raw literals.
- The surface-area exponent was implicit verifier code instead of explicit JSON model data.

What was done:
- Imported binary contract constants from `Tools\CraftingCostsBaker.py` into the validator/verifier.
- Added `KG_TO_MILLIGRAMS`, `UINT32_MASK`, `UINT32_BITS`, and full-precision `surface_area_volume_exponent` authority to the baker/model surface.
- Updated Monte Carlo scale math to use named constants.
- Rejected a rounded `0.666667` exponent after it caused a real `surface_joining_kwh` drift in `Recipe_Tool_Repair`.
- Rebaked all crafting data and reran core economy, binary, hash, data-inquisition, and full verifier sweep gates.

Cinematic Cheats used:
- No runtime computation was added. The hardening makes the offline contract clearer and keeps runtime consumers on scalar/binary lookup.

Exact Microseconds saved:
- Runtime measured savings: 0 us claimed.
- Static artifact effect: JSON is now 126,840 bytes; binary remains 7,424 bytes.

Verification:
- `python -B Tools\CraftingCostsBaker.py` -> binary CRC32 `1295072744`, binary SHA256 `632fdabb2a57dbe115f18fecc8a23a6ddda2bacb0cf53ea472f29b81a0377f69`.
- `python -B Tools\CraftingEconomyMonteCarlo.py --steps 1000000` -> `profit_steps=0`.
- `python -B Tools\VerifyCraftingCosts.py` -> `CRAFTING COST VERIFY OK`.
- `python -B Tools\EconomyValidator.py --root . --negative-tests` -> `STATUS: ECONOMY BALANCED`.
- `python -B Tools\RunFullVerifySweep.py` -> 24 scripts, `failed_count=0`.

STATUS: VERIFIED MASTER GRADE for offline CLI artifacts. Unity runtime remains PENDING VERIFICATION.

## 2026-05-16 - OSHINO Revalidation Pass

What was wrong:
- The escalation required fresh disk-backed proof after context compression.
- Prior outputs were not enough unless the current artifacts could be rebaked and reverified from files on disk.

What was done:
- Reread `Docs\Tasks\Status_CRAFTING_COST_BALANCER.md`, `Docs\AgentLogs\Rationale_CRAFTING_COST_BALANCER.md`, and the full XML block in `Docs\Tasks\CURRENT_BATCH.md`.
- Rebaked `Data\Economy\Crafting_Costs.json`, CSV, manifest, and `.h8bin`.
- Reran the 1,000,000-step Monte Carlo exploit audit.
- Reran crafting verifier, full economy validator with negative tests, binary hygiene, global H8 hash collision audit, and data inquisition.
- Reran cross-domain physical-data gates for Beer-Lambert optics, Dalton gas toxicity, Sabine/Thorp acoustics, Snell refraction, and Metric Phi data truth without modifying those non-owned domains.
- Audited owned crafting artifacts for sterile placeholder terms and stale numbers.

Cinematic Cheats used:
- Crafting runtime remains scalar and stateless: precomputed kWh, mass, time, hashes, and flat binary records.
- Toaster path reads compact aligned records and skips per-recipe FX.
- Ultra path reads packed God-Mode visual records for slag heat gradients, harmonic ridge noise, resin smoke, and hydraulic jolt presentation.

Exact Microseconds saved:
- Runtime measured savings: 0 us claimed. No Unity profiler/GCMonitor evidence was produced.
- Static ingest proof: `Crafting_Costs.h8bin` remains 7,424 bytes, 16-byte aligned, little-endian, CRC32 `1295072744`.

Verification:
- `python -B Tools\CraftingCostsBaker.py` -> 50 recipes, cost ratios `2.931/3.173`, power ratios `2.916/2.966`, starter O2 `4.446`, SHA256 `632fdabb2a57dbe115f18fecc8a23a6ddda2bacb0cf53ea472f29b81a0377f69`.
- `python -B Tools\CraftingEconomyMonteCarlo.py --steps 1000000` -> `profit_steps=0`, value delta `-1000`, mass delta `-400000 mg`, energy delta `-133000 mWh`.
- `python -B Tools\VerifyCraftingCosts.py` -> 50 recipes, 171 ingredients, 38 tool records, 50 God-Mode visual records, 341 hash pairs, 0 collisions.
- `python -B Tools\EconomyValidator.py --root . --negative-tests` -> `STATUS: ECONOMY BALANCED`, 10 negative cases failed as expected, 1736 hash pairs checked, 448 unique hashes.
- `python -B Tools\VerifyBinaryHygiene.py --report Docs\AgentLogs\BinaryHygiene_CRAFTING_COST_BALANCER.json` -> 39 binaries, 0 misaligned.
- `python -B Tools\VerifyH8HashCollisions.py --write-json Docs\AgentLogs\H8Hash_Collision_CRAFTING_COST_BALANCER.json` -> 1018 records, 0 collisions.
- `python -B Tools\VerifyDataInquisition.py --report Docs\AgentLogs\Data_Inquisition_CRAFTING_COST_BALANCER.json` -> 38 aligned binaries, 8 manifests, 148 struct formats, 85 atlas domains.
- `python -B Tools\VerifyOpticsBaker.py` -> Beer-Lambert optics matrix 393,216 bytes, aligned16, little-endian, 0 FNV collisions.
- `python -B Tools\VerifyDaltonGasToxicity.py` -> Dalton binary 128,128 bytes, aligned16, 0 FNV collisions.
- `python -B Tools\VerifySabineBaker.py` -> Sabine binary 524,288 bytes, `<ff` records, 0 FNV collisions, stateless binary lookup.
- `python -B Tools\VerifySnellRefractionLut.py` -> Snell LUT PASS, 524,288 bytes, 0 FNV collisions.
- `python -B Tools\VerifyMetricPhiDataTruth.py` -> `DATA_TRUTH_VERIFIED`, 36 checks, 0 failures, 0 endian failures.
- `python -B -m py_compile ...` -> passed for owned economy and verifier scripts.

STATUS: VERIFIED MASTER GRADE for offline CLI artifacts. Unity runtime remains PENDING VERIFICATION.

## 2026-05-16 - God-Mode Visual Binary Table Pass

What was wrong:
- God-Mode visual overkill existed in JSON but did not have a dedicated binary extra-data table for high-resolution gradients and complex harmonic noise.

What was done:
- Bumped `Crafting_Costs.h8bin` to version 2.
- Expanded the header to 80 bytes.
- Added 50 aligned 16-byte `GODMODE_VISUAL_STRUCT <IIII>` records.
- Packed `arc_gradient_lut_hash32`, `harmonic_noise_seed_hash32`, gradient samples/heat bands/RGBA channels, and harmonic frequency/amplitude/ridge mix.
- Updated `Tools\VerifyCraftingCosts.py` and `Tools\EconomyValidator.py` to read the table back.

Cinematic Cheats used:
- Ultra visuals consume deterministic hashes and packed scalar controls; no runtime procedural authoring is required.
- Toaster ignores the God-Mode table and reads the lean recipe record only.

Exact Microseconds saved:
- Runtime measured savings: 0 us claimed. No Unity profiler or GCMonitor artifact exists.
- Static binary ingest remains aligned and branchable by tier.

Verification:
- `python -B Tools\CraftingCostsBaker.py` -> binary bytes `7424`, CRC32 `1295072744`, SHA256 `632fdabb2a57dbe115f18fecc8a23a6ddda2bacb0cf53ea472f29b81a0377f69`.
- `python -B Tools\CraftingEconomyMonteCarlo.py --steps 1000000` -> `profit_steps=0`.
- `python -B Tools\VerifyCraftingCosts.py` -> `godmode_visual_count=50`, hash pairs `341`, collisions `0`.
- `python -B Tools\EconomyValidator.py --root . --negative-tests` -> `STATUS: ECONOMY BALANCED`, binary bytes `7424`, unique ID hashes `448`.
- `python -B Tools\VerifyDataInquisition.py --report Docs\AgentLogs\Data_Inquisition_CRAFTING_COST_BALANCER.json` -> atlas domains `85`, endian `<`, aligned16 true.
- `python -B Tools\VerifyBinaryHygiene.py --report Docs\AgentLogs\BinaryHygiene_CRAFTING_COST_BALANCER.json` -> 39 binaries, 0 misaligned.
- `python -B Tools\VerifyH8HashCollisions.py --write-json Docs\AgentLogs\H8Hash_Collision_CRAFTING_COST_BALANCER.json` -> 1018 records, 0 collisions.

STATUS: VERIFIED MASTER GRADE for offline CLI artifacts. Unity runtime remains PENDING VERIFICATION.

## 2026-05-16 - Physical kWh Derivation Pass

What was wrong:
- The previous kWh model was executable but still depended on tuned electromechanical/value constants.
- That was insufficient evidence for the current hard-science audit.

What was done:
- Replaced the kWh model with `craft.power.material_process_energy.v2`.
- Added physical material profiles to raw resources and composite material profiles to crafted outputs.
- Added per-recipe `physical_energy_terms_kwh` fields: thermal conditioning, surface joining, feed motion, chamber vacuum, and total.
- Updated `Tools\EconomyValidator.py` to recompute every physical energy term from JSON.
- Rebuilt binary/manifest/hash/sovereignty/Monte Carlo artifacts.
- Ran global binary hygiene, global H8 hash collision audit, and data inquisition checks into `Docs/AgentLogs`.

Cinematic Cheats used:
- Runtime still reads a scalar kWh/mWh field. The expensive derivation is offline-only.
- God-Mode gets material provenance plus packed high-res gradient and harmonic-noise data; Toaster gets the same compact aligned recipe record.

Exact Microseconds saved:
- Runtime measured savings: 0 us claimed. Static data only.
- Import-side proof: binary is 7,424 bytes; no Unity profiler or GCMonitor evidence exists.

Verification:
- `python -B Tools\CraftingCostsBaker.py` -> cost ratios `2.931/3.173`, power ratios `2.916/2.966`, starter O2 `4.446`.
- `python -B Tools\CraftingEconomyMonteCarlo.py --steps 1000000` -> `profit_steps=0`, max energy delta `-133000 mWh`.
- `python -B Tools\VerifyCraftingCosts.py` -> 341 hash pairs, 0 collisions, material-process kWh terms matched, 50 God-Mode visual records matched, binary CRC32 `1295072744`.
- `python -B Tools\EconomyValidator.py --root . --negative-tests` -> `STATUS: ECONOMY BALANCED`.
- `python -B Tools\VerifyBinaryHygiene.py --report Docs\AgentLogs\BinaryHygiene_CRAFTING_COST_BALANCER.json` -> 39 binaries, 0 misaligned.
- `python -B Tools\VerifyH8HashCollisions.py --write-json Docs\AgentLogs\H8Hash_Collision_CRAFTING_COST_BALANCER.json` -> 1018 records, 0 collisions.
- `python -B Tools\VerifyDataInquisition.py --report Docs\AgentLogs\Data_Inquisition_CRAFTING_COST_BALANCER.json` -> 85 atlas domains, aligned16 true, endian `<`, Monte Carlo 1,000,000, hash collisions 0.

STATUS: VERIFIED MASTER GRADE for offline CLI artifacts. Unity runtime remains PENDING VERIFICATION.
