# Status_OPTICAL_EXTINCTION_LUT_BAKER

Prompt: `OPTICAL_EXTINCTION_LUT_BAKER`
Role: `DATA_SCIENTIST`
Domain: `DATA/MATH`
Scoped output domain: `Data/Visuals/`
Status: `VERIFIED MASTER GRADE`
Batch hygiene: status file was missing at session start; no stale per-agent status data detected.

## Mandates Loaded

- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `CI_MATH_VIOLATIONS_Gate.txt`

## Iteration 0 - Intake

- [x] Extract prompt from `Docs/Tasks/CURRENT_BATCH.md` | DOD: strict XML block extraction by ID using PowerShell raw read and regex. | Rejected: manual reading adjacent agent prompts. | Estimate: 500 us.
- [x] Read domain map | DOD: CLI read of `Docs/Actual Domains of Project.txt`; Data/Visuals belongs to data/math artifact ownership for this task. | Rejected: editing Unity scene/assets outside domain. | Estimate: 300 us.
- [x] Load relevant mandates | DOD: six task-relevant `.agents-skills` files read before code edits. | Rejected: broad mandate bulk-load and mandate guessing. | Estimate: 900 us.

## Iteration 1 - Tasks 1-3

- [x] Task 1 PYTHON_NUMPY: write `Tools/OpticsBaker.py` using numpy. | DOD: NumPy vectorized builder with deterministic constants and CLI verification path. | Rejected: hand-written CSV/table and runtime Unity baker. | Estimate: 8000 us.
- [x] Task 2 BEER_LAMBERT: implement `I = I0 * exp(-mu * d)`. | DOD: matrix builder computes `np.exp(-mu * depth)` offline. | Rejected: shader-side `exp()` and fake linear ramp. | Estimate: 1500 us.
- [x] Task 3 COEFFICIENTS: define RGB seawater absorption; red dies near 10m; blue persists to 500m. | DOD: source-named pure-water anchors red 700nm `0.6240`, green 530nm `0.0434`, blue 470nm `0.0106`; red at 10m quantizes to `0.0019498552718089743`, blue at 500m remains `0.004993438720703125`. | Rejected: threshold-derived constants and stale 256-wavelength payload. | Estimate: 900 us.
- [x] Iteration 1 verification | DOD: `python -m py_compile Tools\OpticsBaker.py` passed; prompt re-extracted after three tasks. | Rejected: running stale oversized binary as acceptance. | Estimate: 1200 us.

## Iteration 2 - Tasks 4-6

- [x] Task 4 TURBIDITY_AXIS: second LUT axis is silt/turbidity global absorption. | DOD: `mu = muRgb * (1 + turbidity)` with 256 linear turbidity samples from 0.0 to 2.5. | Rejected: per-channel silt particle simulation and extra spectral axis. | Estimate: 700 us.
- [x] Task 5 MATRIX_SHAPE: create `256x256x3` matrix. | DOD: in-memory validation returned shape `(256, 256, 3)`. | Rejected: stale `(256, 256, 256)` wavelength matrix. | Estimate: 500 us.
- [x] Task 6 FP16_CAST: cast matrix to little-endian `float16`. | DOD: matrix is contiguous half-float payload, 393216 bytes, packed through NumPy for raw write. | Rejected: float32 payload and JSON transport. | Estimate: 600 us.
- [x] Iteration 2 verification | DOD: in-memory Python validation passed for shape, byte count, red 500m zero, blue 500m survival; prompt re-extracted after six tasks. | Rejected: accepting compile-only proof. | Estimate: 1400 us.

## Iteration 3 - Tasks 7-9

- [x] Task 7 BINARY_WRITE: write raw bytes to `Data/Visuals/Water_Extinction_Matrix.bin`. | DOD: baker wrote raw headerless half-float bytes; file length is 393216. | Rejected: old 33554432-byte spectral binary. | Estimate: 2500 us.
- [x] Task 8 VALIDATION_IMAGE: write `Data/Visuals/Water_Extinction_GradientPreview.png`. | DOD: matplotlib generated PNG preview; file has valid PNG signature through baker verification. | Rejected: non-matplotlib fallback after dependency was installed. | Estimate: 9000 us cold, 2500 us warm.
- [x] Task 9 EDGE_CASE_TEST: red at 500m is exactly `0.0`. | DOD: `python Tools\OpticsBaker.py --verify` reported `redAt500mClear=0.0`. | Rejected: visual-only preview acceptance. | Estimate: 400 us.
- [x] Iteration 3 verification | DOD: bake and verify passed; prompt re-extracted after nine tasks; output sizes checked. | Rejected: accepting a partially failed first bake after missing matplotlib and return-shape bug. | Estimate: 1800 us.

## Iteration 4 - Tasks 10-12

- [x] Task 10 SHADER_CONTRACT: write `Docs/Design/LUT_Shader_Mapping.md`. | DOD: mapping doc defines byte contract, 2D texture import, fallback raw R16F layout, sampling rules, and verification commands. | Rejected: leaving SHINOBU agents with stale 4096x4096 spectral mapping. | Estimate: 5000 us.
- [x] Task 11 NO_UNITY: do not touch `.cs` files. | DOD: `git diff --name-only -- '*.cs'` returned empty. | Rejected: Unity loader implementation in this no-Unity task. | Estimate: 200 us.
- [x] Task 12 EXECUTE: run bake. | DOD: `python Tools\OpticsBaker.py` returned `OPTICS_BAKER_STATUS: PASS`. | Rejected: relying on in-memory builder only. | Estimate: 36000 us warm after matplotlib cache.
- [x] Iteration 4 verification | DOD: prompt re-extracted after twelve tasks; no C# diff; bake passed. | Rejected: stale partial prompt snippets from timed-out parallel read. | Estimate: 1800 us.

## Iteration 5 - Tasks 13-15

- [x] Task 13 VERIFY: binary size is `393216` bytes. | DOD: `Get-Item` and baker verification both reported `393216`. | Rejected: manifest-only proof. | Estimate: 300 us.
- [x] Task 14 RATIONALE: document formula. | DOD: formula, coefficients, rejected alternatives, scalability, and hardware impact recorded in rationale and mapping docs. | Rejected: chat-only explanation. | Estimate: 1800 us.
- [x] Task 15 STATUS: `LUT BAKED`. | DOD: generated manifest status is `LUT BAKED`, baker verify returns PASS. | Rejected: final status before byte/endian checks. | Estimate: 500 us.
- [x] Iteration 5 self-review | DOD: read `Tools/OpticsBaker.py` after final patch; fixed partial-bake risk by loading matplotlib before overwriting matrix bytes. | Rejected: accepting the first successful run without code reread. | Estimate: 2200 us.

## Omega Polish

- [x] Read `<POLISH_MANDATE>` only after all 15 tasks are complete or blocked. | DOD: no global `<POLISH_MANDATE>` tag found; agent-local Omega section read after tasks 1-15 were checked. | Rejected: reading polish before core completion. | Estimate: 1200 us.
- [x] Final status: `VERIFIED MASTER GRADE`. | DOD: offline artifact verification passed; Unity runtime import remains explicitly pending. | Rejected: claiming Unity profiler/GC proof without running Unity. | Estimate: 400 us.

## Final Verification

- [x] `python -B -c compile(...)` in-memory syntax gate passed for `Tools\OpticsBaker.py`, `Tools\VerifyOpticsBaker.py`, and `Tools\VerifyBinaryHygiene.py`. Latest `py_compile` disk-bytecode attempt hit WinError 5 on `.pyc` rename; treated as tooling/cache ACL noise, not syntax proof.
- [x] `python Tools\OpticsBaker.py --verify` passed.
- [x] `Data\Visuals\Water_Extinction_Matrix.bin` size is `393216` bytes.
- [x] Manifest status is `LUT BAKED`.
- [x] Red at 500m clear water is `0.0`.
- [x] Red at 10m clear water is `0.0019498552718089743`.
- [x] Blue at 500m clear water is `0.004993438720703125`.
- [x] Half-float contract is `<e`.
- [x] `git diff --name-only -- '*.cs'` returned empty during original optical pass. Current workspace later shows dirty `Assets/_Project/Scripts/Core/Generated/H8Hashes.cs`; this file is outside the optics edit set and was not authored by `Tools/OpticsBaker.py`.
- [x] `rg --files -g '*.sln' -g '*.csproj'` returned no project files, so `dotnet build` is not applicable in this workspace snapshot.

## Phase 1-4 Inquisition Repass

- [x] Cognitive reset rerun | DOD: re-read status, rationale, and original XML directive from disk before additional work. | Rejected: relying on chat memory. | Estimate: 900 us.
- [x] Math audit hardened | DOD: optics coefficients now use source-named pure-water absorption anchors: red 700nm `0.6240`, green 530nm `0.0434`, blue 470nm `0.0106`; Beer-Lambert formula unchanged. | Rejected: threshold-derived art constants as primary physics. | Estimate: 1800 us.
- [x] Binary alignment audit | DOD: `Water_Extinction_Matrix_Toaster.bin`, `Water_Extinction_Matrix.bin`, and `Water_Extinction_Matrix_Overkill.bin` all verify 16-byte aligned; restored `Tools\VerifyBinaryHygiene.py` scanned production `.bin/.h8bin` payloads and reported `binaryCount=44`, `misalignedCount=0`. | Rejected: manifest-only alignment claims. | Estimate: 700 us.
- [x] Endianness audit | DOD: `Tools\OpticsBaker.py --verify` checks raw bytes against Python `struct.pack("<e", value)`. | Rejected: native-endian assumption. | Estimate: 600 us.
- [x] FNV collision audit | DOD: local optics artifact hashes are 6 IDs / 0 collisions; project-wide `VerifyH8HashCollisions.py` produced 1018 records / 0 collisions. | Rejected: hearsay hash IDs. | Estimate: 266000000 us for project-wide gate.
- [x] Toaster and RTX data | DOD: emitted `64x64x3` toaster binary and `512x512x3` overkill binary plus high-res overkill preview and harmonic presentation fields. | Rejected: one-size-only 2010-era LUT. | Estimate: 120000 us.
- [x] Optics verify script | DOD: added and ran `Tools\VerifyOpticsBaker.py`, producing `Docs/AgentLogs/OpticsVerification_OPTICAL_EXTINCTION_LUT_BAKER.json`. | Rejected: relying only on baker self-report. | Estimate: 49000000 us.
- [x] Dalton/Sabine verify scripts | DOD: `Tools\MathLUTGenerator.py --verify` returned `PASS`; `Tools\VerifySabineBaker.py` returned `STATUS: SABINE_LUT_VERIFIED`. | Rejected: claiming hard-science coverage from optical data alone. | Estimate: 113000000 us.
- [x] Economy Monte Carlo | DOD: `python Tools\Economy\MonteCarloEconomySim.py --players 6500 --max-nodes 10000` mined `1001972` nodes, `million_step_audit_passed=True`, `failures=0`, `p99_minutes=59.284`, `STATUS: ECONOMY PROVEN`; `python -B Tools\EconomyValidator.py --root . --negative-tests` returned `STATUS: ECONOMY BALANCED`, `negative_cases=10`, `toaster_binary_bytes=2464`. | Rejected: accepting the earlier timed-out 10000-player run or temp-permission failure as data proof. | Estimate: 188900000 us.
- [x] Recipe infinite-loop audit | DOD: `Tools\EconomyRecipeGraphAudit.py` output graph `is_dag=True`, `cycle_count=0`, no zero-ingredient or value exploit lists. | Rejected: Monte Carlo alone as cycle proof. | Estimate: 102000000 us.
- [x] Lore audit | DOD: stale failure was rechecked; `python Tools\VerifyLore.py --check` now reports `CHECK OK: entries=2`, blob `Data/Lore/Encyclopedia.h8bin`, compression `none/raw-utf8`, alignment `16`, endian `<`. | Rejected: carrying an obsolete failure line after clean verification. | Estimate: 48000000 us.
- [x] Static data inquisition | DOD: `python Tools\VerifyDataInquisition.py --report Docs\AgentLogs\DataInquisition_OPTICAL_EXTINCTION_LUT_BAKER.json` reported `DATA_INQUISITION_VERIFIED_STATIC_ONLY`, `binaries=43`, `aligned16=true`, `manifests=11`, `endian=<`, `structFormats=162`, `monteCarloSteps=1000000`, `hashCollisions=0`, `atlasDomains=85`. | Rejected: chat-only phase compliance. | Estimate: 65000000 us.
- [x] Scalability fallback audit | DOD: optics emits toaster/main/overkill variants; `python Tools\VerifyVramBudgets.py` reported `VFX_VRAM_BUDGETS_OK` with TOASTER/DECK/PRO/GOD_MODE data; `python Tools\VerifySnellRefractionLut.py` reported PASS for refraction LUT; `python Tools\VerifyCraftingCosts.py` reported `CRAFTING COST VERIFY OK`. | Rejected: assuming cross-data scalability from optics manifest alone. | Estimate: 123000000 us.
- [x] Source provenance hardening | DOD: `Water_Extinction_Matrix.json` now carries `sourceReferences` with Pope/Fry Applied Optics DOI `10.1364/AO.36.008710`, per-channel `coefficientProvenance`, `mathDerivation`, and lore lexicon constraints. | Rejected: source-named prose without machine-readable provenance. | Estimate: 41000000 us.
- [x] H-Phi data sovereignty audit | DOD: regenerated `HECTON_PHI_SCORE_FINAL.json` with `python -B Tools\CalculateHPhi.py --root . --workers 4`; `python -B Tools\RunMetricPhiVerifySweep.py --xxhash-path %TEMP%\metric_phi_xxhash_ref` regenerated `METRIC_PHI_VERIFY_SWEEP.json` with `VERIFY_SWEEP_PASS`, `35` commands, `0` required failures, replay hasher reference `returnCode=0`; `python Tools\VerifyMetricPhiDataTruth.py` then reported `DATA_TRUTH_VERIFIED`, `checks=37`, `failed=0`, `binary_files=43`, `unaligned=0`, `struct_format_sites=174`, `endian_failures=0`. | Rejected: asserting H-Phi improvement while replay-hasher proof or H-Phi freshness was stale/missing. | Estimate: 1805000000 us.
- [x] PROJECT_ATLAS fit | DOD: atlas has Graphics and lighting family plus H-Phi/Data Sovereignty model; optics report declares stateless binary lookup, no runtime assembly dependency, no GlobalRegistry, no private Native state. | Rejected: adding runtime owner/private state. | Estimate: 37000 us.

## Phase 1-4 Inquisition Repass 2

- [x] Broad `Verify*.py` sweep | DOD: `python -B Tools\RunFullVerifySweep.py` ran 28 root `Tools\Verify*.py` scripts with `rc=0`; initial failed H-Phi/quest gates were rerun after H-Phi regeneration and now pass. | Rejected: leaving stale `VerifySweep_CRAFTING_COST_BALANCER.json` failures on disk. | Estimate: 184000000 us.
- [x] Metric-Phi sweep | DOD: elevated `python -B Tools\RunMetricPhiVerifySweep.py --xxhash-path %TEMP%\metric_phi_xxhash_ref` produced `VERIFY_SWEEP_PASS`, `35` commands, `0` required failures. | Rejected: sandboxed replay-hasher failure with `xxhash module has no __file__`. | Estimate: 1805000000 us.
- [x] Economy proof rerun | DOD: `python -B Tools\Economy\MonteCarloEconomySim.py --players 6500 --max-nodes 10000` produced `total_nodes_mined=1001972`, `million_step_audit_passed=True`, `failures=0`, `p99_minutes=59.284`, `STATUS: ECONOMY PROVEN`; `EconomyValidator.py --negative-tests` returned `negative_cases=10`, `STATUS: ECONOMY BALANCED`; recipe graph remained DAG with `cycle_count=0`. | Rejected: treating older Monte Carlo output as enough after user demanded another pass. | Estimate: 123000000 us.
- [x] Hard-science sibling LUT rerun | DOD: `MathLUTGenerator.py --verify` returned `PASS`, `VerifyDaltonGasToxicity.py` returned `VERIFY_DALTON_GAS_TOXICITY_PASS`, `VerifySabineBaker.py` returned `SABINE_LUT_VERIFIED`, and Snell refraction returned `PASS`. | Rejected: claiming Beer-Lambert optics alone covers Dalton/Sabine/Snell. | Estimate: 285000000 us.
- [x] Lore/noir and scalability rerun | DOD: `VerifyLore.py --check` returned `CHECK OK`; `VerifyVramBudgets.py` returned `VFX_VRAM_BUDGETS_OK` with TOASTER/DECK/PRO/GOD_MODE tiers and hash collisions `0`. | Rejected: showroom sci-fi text or single-tier LUT proof. | Estimate: 74000000 us.
- [x] Optics-owned tool restoration | DOD: concurrent workspace mutation removed root `Tools/*.py` files after broad sweep; restored only optics-owned `Tools\OpticsBaker.py`, `Tools\VerifyOpticsBaker.py`, and `Tools\VerifyBinaryHygiene.py`; in-memory compile, `OpticsBaker.py --verify`, `VerifyOpticsBaker.py`, and `VerifyBinaryHygiene.py` all pass. | Rejected: reverting unrelated tracked tool deletions from other agents. | Estimate: 18000000 us.
- [x] External verifier deletion recorded | DOD: root broad-verifier tools such as `VerifyMetricPhiDataTruth.py`, `VerifyDataInquisition.py`, and `RunFullVerifySweep.py` are currently absent after concurrent mutation; broad H-Phi/data-inquisition rerun is therefore blocked by dependency, while last completed Metric-Phi sweep before deletion was recorded as `VERIFY_SWEEP_PASS`, 35 commands, 0 required failures. | Rejected: fabricating a fresh broad sweep after the scripts disappeared. | Estimate: 2000 us.
- [x] Lore surface purge | DOD: removed showroom wording literals from `Tools\OpticsBaker.py`, `Data\Visuals\Water_Extinction_README.md`, `Docs\Design\LUT_Shader_Mapping.md`, regenerated `Water_Extinction_Matrix.json`, and `rg` found no draft-marker/showroom-fog tokens in the optics artifact surface. | Rejected: keeping banned terms inside a negative-tone list in shipped JSON. | Estimate: 30000000 us.
