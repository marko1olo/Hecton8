# LOG_VFX_PARTICLE_BUDGET_MAP

What was wrong:
- No strict Data/System compute-buffer budget map existed for MarineSnow, Sparks, Bubbles, Silt, and Blood across TOASTER, DECK, PRO, and GOD_MODE.
- Existing visual scalability data is broad render policy, not exact per-system compute allocation authority.
- GOD_MODE MarineSnow requested 100000 live particles, which is not 64-thread dispatch aligned.

What was done:
- Created `Data/System/VFX_Budgets.json` as minified offline authority data.
- Created `Tools/VerifyVramBudgets.py`.
- Locked struct sizes to powers of two: MarineSnow/Sparks/Silt 32B, Bubbles/Blood 64B.
- Stored live count and dispatch-aligned buffer capacity separately.
- Documented SystemDispatcher VISUAL_SYNC cold allocation mapping. No per-frame JSON, GlobalRegistry polling, or buffer resize path is allowed.
- Defined SystemStress01 > 0.9 shedding zero order: Sparks, Blood, Bubbles, Silt. MarineSnow survives longest as baseline depth cue.

Cinematic Cheats used:
- MarineSnow: shader drift and depth fog cue before flow simulation.
- Sparks: additive screen-space glints before physical fragments.
- Bubbles: billboard wobble before buoyancy truth.
- Silt: scrolling flow masks before fluid simulation.
- Blood: decay tint plume before volumetric fluid truth.

Exact byte results:
- TOASTER: 474272 B, below 209715200 B cap.
- DECK: 2539680 B.
- PRO: 10010784 B.
- GOD_MODE: 16115360 B.
- GOD_MODE MarineSnow liveCount 100000, bufferCapacity 100032, deadPadding 32.

Microseconds saved:
- Hard caps versus runaway emitter allocation: estimated 120 us cold/setup avoidance on MX350-class hardware. PENDING GPU CAPTURE.
- Dispatch-aligned padding versus tail-branch handling: estimated 35 us setup/dispatch-path simplification. PENDING GPU CAPTURE.
- Cold immutable budget read versus per-frame JSON/config polling: estimated 40 us and 0 B/frame hot-path target. PENDING RUNTIME VERIFICATION.
- Stress shedding zero order: estimated 200-600 us pressure relief depending scene density and tier. PENDING GPU CAPTURE.

Verification:
- `python Tools/VerifyVramBudgets.py` passed: `VFX_VRAM_BUDGETS_OK TOASTER=474272B DECK=2539680B PRO=10010784B GOD_MODE=16115360B`.
- `python -m py_compile Tools/VerifyVramBudgets.py` passed.
- `python -m json.tool Data/System/VFX_Budgets.json` passed.
- `git diff --check` passed for touched files.
- `rg --files -g '*.csproj'` found no C# project file, so `dotnet build` was not runnable in this workspace.

Status:
- VERIFIED MASTER GRADE for offline data/verifier scope.
- Unity runtime, profiler, GCMonitor, RenderDoc, and player-build proof remain PENDING VERIFICATION.

## OSHINO Inquisition Pass

What was wrong:
- The first budget map proved arithmetic but did not prove stateless lookup identity, SHINOBU binary cache readiness, PROJECT_ATLAS fit, or high-tier extra-data accounting.
- GOD_MODE had particle counts but no explicit extra gradient/harmonic payload bytes.
- Hash identity was implicit.

What was done:
- Hardened `Data/System/VFX_Budgets.json` with `mathAudit`, `projectAtlasFit`, `hPhiAudit`, `hashing`, `binaryCache`, and `godModeExtraData`.
- Hardened `Tools/VerifyVramBudgets.py` to enforce FNV-1a hashes, 0 collisions, atlas domains 8/66, stateless lookup language, minified JSON, 16-byte binary cache alignment, little-endian `<` packing, exact binary rows, and GOD_MODE extra-data byte totals.
- Generated `Data/System/VFX_Budgets.h8bin`.

Cinematic Cheats used:
- TOASTER keeps stripped fake-first data: no extra payloads, no hidden physical truth.
- GOD_MODE buys visible overkill with exact extra payloads: MarineSnow gradients/harmonics, Sparks thermal ramp, Bubbles pressure wobble/rim spectrum, Silt dirty-flow harmonics, Blood wound tint/clot coefficients.

Exact byte results:
- TOASTER: 474272 B.
- DECK: 2539680 B.
- PRO: 10010784 B.
- GOD_MODE: 16128160 B.
- GOD_MODE extra-data delta: 12800 B.
- Binary cache: 1344 B, 16-byte aligned, little-endian, 20 rows.

Microseconds saved:
- Runtime JSON/string lookup rejection: estimated 40 us cold-path avoidance, 0 B/frame target.
- Fixed binary row lookup: estimated 10-25 us cold-load improvement versus parsing minified JSON on weak CPUs. PENDING DEVICE CAPTURE.
- Tail-branch avoidance remains 35 us setup/dispatch-path simplification estimate. PENDING GPU CAPTURE.

Additional validation:
- `python Tools/VerifyVramBudgets.py` passed with `HASH_COLLISIONS=0`.
- `python -m py_compile Tools/VerifyVramBudgets.py` passed.
- `python -m json.tool Data/System/VFX_Budgets.json` passed.
- `python Tools/ValidateVfxParticleBudgetCatalog.py` passed.
- Binary hygiene scan: all discovered `Data/**/*.bin` and `Data/**/*.h8bin` reported `mod16=0`.
- `python Tools/VerifyH8HashCollisions.py --root .` passed with 1018 records and 0 collisions.
- `python Tools/CraftingEconomyMonteCarlo.py --steps 1000000 --seed 12648430` passed with `profit_steps=0`.
- `python Tools/EconomyValidator.py --root .` passed with `STATUS: ECONOMY BALANCED`.
- `python Tools/EconomyRecipeGraphAudit.py --root .` passed with `ECONOMY SECURED`.
- `python Tools/VisualStressSim.py --write-report --report Docs/AgentLogs/VisualStressSim_VFX_PARTICLE_BUDGET_MAP.json` passed.
- `Data/System/VFX_Budgets.manifest.json` added and verifier-enforced against `Data/System/VFX_Budgets.h8bin` SHA-256/CRC32.
- VFX lore aliases now verifier-enforce dirty NASA-punk terms and reject sterile labels in the VFX system table.
- Count derivation strings are verifier-enforced for each tier/system row so non-mandated caps are not anonymous magic numbers.

External verifier debt found:
- `VerifyHullStressBudget.py` fails Habitat god-mode decal seed parity and its own economy proof status.
- `VerifyLore.py --check` fails blob payload mismatch for `Docs/Lore/Archives/DeepReach_ColonyFailureArchive.md`.
- These files are outside `VFX_PARTICLE_BUDGET_MAP` Data/System ownership and were not patched.

## 2026-05-16 SHINOBU Layout Hardening Pass

What was wrong:
- The binary cache was hash-verified, endian-verified, and 16-byte aligned, but the manifest did not expose exact header/row field offsets or row byte offsets as machine-readable data.

What was done:
- Updated `Tools/VerifyVramBudgets.py` to generate and validate `H8VB.VFXBudget.FixedRows.v1`.
- Added explicit 16-field header layout, 16-field row layout, `rowIndex=tierIndex*5+systemIndex`, and `byteOffset=64+rowIndex*64`.
- Added 20 exact row offsets to `Data/System/VFX_Budgets.manifest.json`.
- Regenerated `Data/System/VFX_Budgets.json`, `Data/System/VFX_Budgets.h8bin`, and `Data/System/VFX_Budgets.manifest.json`.

Cinematic Cheats used:
- No runtime simulation was added. The map remains a fixed visual budget contract: shader drift, pooled visual cues, and GPU buffer caps over physical fluid truth.

Exact Microseconds saved:
- Hot path: 0 us claimed; runtime proof absent.
- Cold path: fixed-row seek replaces string/table inference. Measured ingest microseconds remain PENDING RUNTIME CAPTURE.

Verification:
- `python Tools/VerifyVramBudgets.py --rewrite-json --write-binary-cache`: passed.
- `python Tools/VerifyVramBudgets.py`: passed with TOASTER=474272B, DECK=2539680B, PRO=10010784B, GOD_MODE=16128160B, HASH_COLLISIONS=0.
- `python -B -c compile(...)`: passed. `python -m py_compile` could not be used because Windows denied rename access inside `Tools/__pycache__`.
- JSON parse for budget and manifest: passed.
- Data binary alignment scan: all discovered `Data/**/*.bin` and `Data/**/*.h8bin` reported mod16=0.
- `python Tools/VerifyH8HashCollisions.py --root .` printed 1018 records and HASH COLLISIONS: 0; wrapper timed out after output, so process exit status is PENDING VERIFICATION.

## 2026-05-16 Row CRC + Full Verify Sweep

What was wrong:
- The SHINOBU manifest exposed row offsets but did not provide row-local integrity. Whole-file SHA/CRC could prove corruption but not isolate the broken tier/system row.
- Previous status still contained stale external verifier failure memory.

What was done:
- Added `rowIntegrity` metadata to `Data/System/VFX_Budgets.json` and `Data/System/VFX_Budgets.manifest.json`.
- Added `rowCrc32` to each of the 20 manifest row offsets.
- Updated `Tools/VerifyVramBudgets.py` so manifest row CRCs are recomputed from the `.h8bin` payload and enforced.
- Re-ran every `Tools/Verify*.py` script from disk. All 24 exited 0.

Cinematic Cheats used:
- No physical fluid truth added. The VFX table remains fake-first: fixed caps, dead padding, shader drift, billboard wobble, flow-mask haze, and deterministic shedding.

Exact Microseconds saved:
- Hot path: 0 us claimed; no runtime code changed.
- Cold path: row-local CRC avoids whole-file binary triage when one row is corrupt. Measured ingest microseconds remain PENDING RUNTIME CAPTURE.

Verification:
- `python Tools/VerifyVramBudgets.py --rewrite-json --write-binary-cache`: passed.
- `python Tools/VerifyVramBudgets.py`: passed.
- `python -B -c compile(...)`: passed.
- `python Tools/CraftingEconomyMonteCarlo.py --steps 1000000 --seed 12648430`: passed with `profit_steps=0`.
- `python Tools/EconomyValidator.py --root .`: passed with `STATUS: ECONOMY BALANCED`.
- `python Tools/VerifyH8HashCollisions.py --root .`: exited 0 with 1018 records and 0 collisions.
- Full `Tools/Verify*.py` sweep: 24 scripts exited 0, including BinaryHygiene, DataInquisition, MetricPhi, HullStress, Lore, Babel, QuestDag, Sabine, Dalton, Snell, Tide, VisualLOD, and VR comfort.
- Post-row-CRC affected re-check: VFX verifier, BinaryHygiene, DataInquisition, MetricPhi, VFX catalog validator, JSON parse, row CRC table check, and git diff whitespace check exited 0 after the final file edits.

## 2026-05-16 Header CRC + Struct Format Proof

What was wrong:
- Row integrity was local, but the 64-byte header still had no local CRC in the manifest.
- The little-endian struct formats existed in Python, but SHINOBU did not have explicit machine-readable format strings for the header and row payloads.

What was done:
- Added `headerStructFormat=<4s15I` and `rowStructFormat=<16I` to `Data/System/VFX_Budgets.json` and `Data/System/VFX_Budgets.manifest.json`.
- Added `headerCrc32` over the first 64 bytes of `Data/System/VFX_Budgets.h8bin`.
- Updated `Tools/VerifyVramBudgets.py` to assert both packed structs are 64 bytes and to recompute the header CRC from the binary payload.

Cinematic Cheats used:
- None added. This is binary hygiene only; the VFX path remains visual-fake-first and fixed-budget.

Exact Microseconds saved:
- Hot path: 0 us claimed; no runtime code changed.
- Cold path: header corruption can be isolated without scanning row payloads. Measured ingest microseconds remain PENDING RUNTIME CAPTURE.

Verification:
- `python Tools/VerifyVramBudgets.py --rewrite-json --write-binary-cache`: passed.
- `python Tools/VerifyVramBudgets.py`: passed.
- In-memory Python compile: passed.
- Struct size proof: `<4s15I` = 64 bytes, `<16I` = 64 bytes.
- Manifest proof: `headerIntegrity=crc32_first_64_bytes`, `headerCrc32=258637209`, `rowIntegrity=crc32_each_64_byte_row`, row count 20.
- `python Tools/VerifyDataInquisition.py`: passed with `aligned16=true`, `endian=<`, `atlasDomains=85`.
- `python Tools/VerifyMetricPhiDataTruth.py`: passed with `failed=0`, `unaligned=0`, `endian_failures=0`.
- `python Tools/VerifyBinaryHygiene.py`: passed with `misalignedCount=0`.
- `python Tools/ValidateVfxParticleBudgetCatalog.py`: passed.

## 2026-05-16 Tier Slice + Final Sweep Pass

What was wrong:
- SHINOBU could validate rows, but a low-tier ingest path still had to infer the contiguous TOASTER tier range.
- A fresh full verifier sweep exposed external Babel source-hash drift after lore metadata changed.

What was done:
- Added `tierSliceFormula=tierByteOffset=64+tierIndex*5*64; tierByteCount=5*64` to `Data/System/VFX_Budgets.json` and manifest output.
- Added four `tierSlices` to `Data/System/VFX_Budgets.manifest.json`, each 320 bytes with `tierCrc32`, declared tier bytes, live particle totals, and capacity totals.
- Updated `Tools/VerifyVramBudgets.py` to recompute tier-slice CRCs from `Data/System/VFX_Budgets.h8bin`.
- Repaired external Babel drift through `python Tools/BabelCompiler.py`, not by hand-editing generated files.

Cinematic Cheats used:
- VFX still uses fixed buffer caps and fake-first visual cues. No particle physics truth was added.

Exact Microseconds saved:
- Hot path: 0 us claimed; no runtime code changed.
- Cold TOASTER ingest: can validate/read one 320-byte tier slice instead of scanning all 20 rows. Measured ingest microseconds remain PENDING RUNTIME CAPTURE.

Verification:
- `python Tools/VerifyVramBudgets.py --rewrite-json --write-binary-cache`: passed.
- `python Tools/VerifyVramBudgets.py`: passed.
- Tier-slice CRC proof: 4 slices, 320 bytes each.
- `python Tools/VerifyBinaryHygiene.py`: passed with `binaryCount=41/42` across reruns and `misalignedCount=0`.
- `python Tools/VerifyDataInquisition.py`: passed with `atlasDomains=85`, `endian=<`, `hashCollisions=0`.
- `python Tools/VerifyMetricPhiDataTruth.py`: passed with `failed=0`, `endian_failures=0`.
- `python Tools/BabelCompiler.py`: rebuilt Babel dictionary, manifest, and hash constants: 45 sources, 32672 entries, 17 languages, 1529088 bytes.
- `python Tools/VerifyBabel.py`: passed.
- `python Tools/VerifyBabelDictionary.py`: passed.
- Full `Tools/Verify*.py` replay after repairs: 28 scripts exited 0.
- `python Tools/CraftingEconomyMonteCarlo.py --steps 1000000 --seed 12648430`: passed with `profit_steps=0`.
- `python Tools/EconomyValidator.py --root .`: passed with `STATUS: ECONOMY BALANCED`, `toaster_binary_bytes=2464`, `unique_id_hashes=449`.

## 2026-05-16 Reset Audit: MetricPhi Evidence Repair

What was wrong:
- Current-disk `VerifyMetricPhiDataTruth.py` failed with 37 checks, 2 failures.
- The failures were stale evidence artifacts: `HECTON_PHI_SCORE_FINAL.json` was older than `Assets/_Project/Scripts/UI/Localization/H8LocHashes.cs`, and `METRIC_PHI_VERIFY_SWEEP.json` still recorded `VERIFY_SWEEP_FAIL`.
- VFX rows, binary cache, row CRCs, header CRC, tier slices, endian contract, and FNV collision proof remained valid.

What was done:
- Re-read `Docs/Tasks/Status_VFX_PARTICLE_BUDGET_MAP.md`, `Docs/AgentLogs/Rationale_VFX_PARTICLE_BUDGET_MAP.md`, and the original XML prompt from `Docs/Tasks/CURRENT_BATCH.md`.
- Regenerated H-Phi through `python -B Tools/CalculateHPhi.py --workers 4 --source-roots Assets Packages Tools --json-output Docs/Reports/HECTON_PHI_SCORE_FINAL.json --graph-output Docs/Reports/HECTON_PHI_ARCHITECTURE_GRAPH.png --atlas Docs/PROJECT_ATLAS.md`.
- Regenerated the MetricPhi sweep through `python -B Tools/RunMetricPhiVerifySweep.py --xxhash-path %TEMP%\metric_phi_xxhash_ref --json-output Docs/Reports/METRIC_PHI_VERIFY_SWEEP.json --markdown-output Docs/Reports/METRIC_PHI_VERIFY_SWEEP.md`.
- Confirmed the sweep report now reads `VERIFY_SWEEP_PASS`, 35 commands, 0 required failures, final `VerifyMetricPhiDataTruth=True`.

Cinematic Cheats used:
- None added in this pass. The VFX matrix remains visual-fake-first: capped compute buffers, fixed row lookup, deterministic stress shedding, and GOD_MODE extra payloads with explicit bytes.

Exact Microseconds saved:
- Runtime: 0 us claimed; no runtime code changed.
- Offline evidence: stale self-check debt removed. `RunMetricPhiVerifySweep.py` wall time was 1617.9 s; measured runtime ingest savings remain PENDING RUNTIME CAPTURE.

Verification:
- `python Tools/VerifyVramBudgets.py`: passed with TOASTER 474272 B, DECK 2539680 B, PRO 10010784 B, GOD_MODE 16128160 B, hash collisions 0.
- `python Tools/VerifyBinaryHygiene.py`: passed with binaryCount 42 and misalignedCount 0.
- `python Tools/VerifyDataInquisition.py`: passed with binaries 41, aligned16 true, manifests 9, endian `<`, structFormats 156, Monte Carlo steps 1000000, hashCollisions 0, atlasDomains 85.
- `python Tools/VerifyH8HashCollisions.py --root .`: passed with 1018 records and 0 collisions.
- `python Tools/CraftingEconomyMonteCarlo.py --steps 1000000 --seed 12648430`: passed with `profit_steps=0`.
- `python -B Tools/VerifyMetricPhiDataTruth.py`: passed with `DATA_TRUTH_VERIFIED`, checks 37, failed 0, binary_files 42, unaligned 0, struct_format_sites 167, endian_failures 0.
- `python -B Tools/VerifyQuestDagDataTruth.py`: passed with `QUEST_DAG_DATA_TRUTH_VERIFIED`, checks 10, failed 0.
- Unity runtime, GCMonitor, RenderDoc, frame-time, memory, scene wiring, and player build proof remain PENDING VERIFICATION.
