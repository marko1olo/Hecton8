# LOG_FAUNA_BEHAVIOR_SIMULATOR

## 2026-05-14 - Utility AI Weight Tuner

What was wrong:
- No current `CURRENT_BATCH_OSHINO.md` existed in the workspace. The active assignment existed in `Docs/Tasks/CURRENT_BATCH.md`.
- The task needed a Python-only data simulation. C# or Dotnet integration would violate the prompt.
- A first smoke implementation path was too slow for practical sweeps and then a concurrent workspace change removed the FAUNA files after the first full run.

What was done:
- Extracted the `FAUNA_BEHAVIOR_SIMULATOR` XML prompt from `Docs/Tasks/CURRENT_BATCH.md` by CLI regex.
- Created `Tools/AI_Sim/FaunaBalanceSim.py`.
- Modeled `Alpha Leviathan`, `Stalker`, and `Prey` as Python species objects with aggression, hunger, fear, acoustic/retinal tracking, sensory noise, and polynomial fear scoring.
- Ran the full command: `python Tools\AI_Sim\FaunaBalanceSim.py --frames 1000000 --discovery-frames 12000`.
- Exported `Data/AI/Fauna_Global_Weights.json`.
- Exported matching detailed report `Tools/AI_Sim/FaunaBalanceSim_Report.json`.
- Polish pass compacted `Data/AI/Fauna_Global_Weights.json` to a runtime handoff and left detailed telemetry in `Tools/AI_Sim/FaunaBalanceSim_Report.json`.
- Added repeat-seed validation mode and wrote `Tools/AI_Sim/FaunaBalanceSim_ReplicateValidation.json`.
- Added `Tools/AI_Sim/test_fauna_balance_sim.py` regression harness.
- Added `--check-artifacts` CLI mode to `Tools/AI_Sim/FaunaBalanceSim.py`.
- Added strict non-finite JSON guard: `allow_nan=False` writer plus recursive artifact scan.
- Added selected-constant range/type guard for compact constants, detailed report, and `load_selected_weights()`.

Cinematic Cheats used:
- Aggregate prey/predator biomass simulation instead of per-creature truth.
- Acoustic tracking scalar for retinal-blindness compensation instead of physical perception simulation.
- Quadratic fear buildup as a controllable utility curve instead of emergent panic physics.
- Sensory noise modeled as deterministic 1-bit radar error injection instead of ray/perception simulation.

Selected constants:
- `AggressionScalar`: `1.38`
- `FearScalar`: `0.76`
- `HungerWeight`: `0.92`
- `FearWeight`: `1.16`
- `AcousticTrackingWeight`: `0.68`
- `RetinalTrackingWeight`: `0.32`
- `FearCurvePower`: `2.0`
- `SensoryNoiseTolerance`: `0.12`

Simulation result:
- Status: `AI BALANCED`
- Evidence class: `CLI_PYTHON_SIMULATION`
- Unity runtime proof: `PENDING VERIFICATION`
- Frames: `1,000,000`
- Final prey: `9436.618`
- Final stalker: `38.109`
- Final alpha leviathan: `1.664`
- Score: `0.4471`
- Retinal blindness with acoustic kill throughput ratio: `0.56321`
- Retinal blindness without acoustic kill throughput ratio: `0.08101`
- Linear-vs-quadratic score delta: `0.058491` in favor of quadratic fear.

Exact microseconds saved:
- Runtime measured savings: `0 us` claimed. This task did not modify Unity runtime code and no profiler sample exists.
- Runtime cost introduced by this task: `0 us` until a runtime owner consumes the JSON.
- Static estimate versus per-creature runtime ecology truth: savings are material but unmeasured; profiler proof remains `PENDING VERIFICATION`.
- File bloat removed: constants handoff reduced from full-report duplicate to `1812` bytes; detailed report remains `40188` bytes.
- After adding replicate summary, constants handoff is `2250` bytes and replicate validation report is `5683` bytes.
- Repeat-seed validation: `5` replicates x `200000` frames, `0` failures, status `REPLICATE_STABLE`.
- Repeat-seed population range: prey `9434.857..9439.544`, stalker `38.061..38.093`, alpha `1.814..1.815`.

Verification:
- `python -m py_compile Tools\AI_Sim\FaunaBalanceSim.py` -> pass.
- `python -m json.tool Data\AI\Fauna_Global_Weights.json` -> pass.
- `python -m json.tool Tools\AI_Sim\FaunaBalanceSim_Report.json` -> pass.
- Compact constants JSON size -> `2250` bytes after replicate summary.
- Detailed report JSON size -> `40188` bytes with `101` samples.
- Repeat validation JSON size -> `5683` bytes.
- Required compact JSON keys present.
- Source self-review for `TODO`, Dotnet, subprocess, `os.system`, `eval`, `exec`, `random.` -> no matches in `Tools/AI_Sim/FaunaBalanceSim.py`.
- `<POLISH_MANDATE>` lookup -> `POLISH_MANDATE_NOT_FOUND`.
- Hard self-audit invariant check -> constants/report match, replicate summary/report match, `failureCount=0`.
- Removed generated `Tools/AI_Sim/__pycache__`.
- Corrected stale evidence wording in rationale and changed overstated CLI completion wording to `FINISHED`.
- Regression harness: `python -m unittest Tools.AI_Sim.test_fauna_balance_sim -v` -> `4` tests, `OK`, final validation elapsed `1.470 s`.
- Regression coverage: constants/report alignment, replicate summary/report alignment, bounded selected-weight short run, short replicate stability.
- Removed generated `Tools/AI_Sim/__pycache__` and `Tools/__pycache__` after test execution.
- Artifact checker: `python Tools\AI_Sim\FaunaBalanceSim.py --check-artifacts` -> `ARTIFACT_CHECK_PASSED`, constants `2250` bytes, report `40188` bytes, replicate `5683` bytes, frames `1000000`, replicates `5`.
- Expanded regression harness: `python -m unittest Tools.AI_Sim.test_fauna_balance_sim -v` -> `5` tests, `OK`, final validation elapsed `1.672 s`.
- Non-finite guard validation: `python -m unittest Tools.AI_Sim.test_fauna_balance_sim -v` -> `6` tests, `OK`, final validation elapsed `0.446 s`; negative test injects `NaN` and expects checker failure.
- Post-guard validation: `python Tools\AI_Sim\FaunaBalanceSim.py --check-artifacts` -> `ARTIFACT_CHECK_PASSED`; `py_compile` and JSON parse validation passed.
- Selected-constant range validation: `python -m unittest Tools.AI_Sim.test_fauna_balance_sim -v` -> `8` tests, `OK`, final validation elapsed `0.647 s`; negative tests corrupt `AggressionScalar` and `FearCurvePower`.
- Post-range validation: `python Tools\AI_Sim\FaunaBalanceSim.py --check-artifacts` -> `ARTIFACT_CHECK_PASSED`; `py_compile` and JSON parse validation passed.

Regression model:
- CPU: no Unity runtime code changed; offline Python run elapsed 226.3 s.
- GC: no Unity runtime code changed; runtime GC proof absent.
- Memory: JSON artifacts are small data files; Unity memory impact unmeasured until integration.
- Cadence: no tick/update loop changed.
- Correctness: constants are simulation-balanced only; scene wiring and runtime consumption remain `PENDING VERIFICATION`.

## 2026-05-14 - Artifact Header Contract Guard

What was wrong:
- `--check-artifacts` guarded selected constants and summaries, but it could still accept artifacts with drifted schema/provenance/runtime-proof/report-path fields.
- Current `Docs/Tasks/CURRENT_BATCH.md` no longer contains the `FAUNA_BEHAVIOR_SIMULATOR` XML prompt. Existing status/rationale files remain the local source of truth for this agent.
- Python `tempfile.TemporaryDirectory()` and `py_compile` both hit `WinError 5` under the current sandbox when writing/renaming generated files.

What was done:
- Added canonical schema/provenance/evidence/runtime-proof/report-path/species-target constants to `Tools/AI_Sim/FaunaBalanceSim.py`.
- Hardened `--check-artifacts` to reject root-type drift, header drift, species-target drift, report-path drift, non-finite numbers, selected-constant range drift, summary mismatch, replicate failure, and detailed-payload leakage into the compact constants file.
- Added regression tests for schema drift, fake Unity verification, report-path drift, non-finite numbers, out-of-range constants, bad selected-weight loads, and happy-path artifact checks.
- Replaced OS temp usage with workspace-local deterministic test artifact folders and set `sys.dont_write_bytecode = True`; final validation uses `python -B`.

Cinematic Cheats used:
- Kept all balancing and validation offline. No runtime fauna simulation, no Unity C# integration, no Dotnet.
- Preserved the compact `Data/AI/Fauna_Global_Weights.json` handoff and kept detailed telemetry in tool-side reports.

Verification:
- `python -B -m unittest Tools.AI_Sim.test_fauna_balance_sim -v` -> `11` tests, `OK`, elapsed `2.812 s`.
- Direct source compile check via Python `compile()` -> `SOURCE_COMPILE_PASS`.
- `python -B Tools\AI_Sim\FaunaBalanceSim.py --check-artifacts` -> `ARTIFACT_CHECK_PASSED`, constants `2250` bytes, report `40188` bytes, replicate `5683` bytes, frames `1000000`, replicates `5`.
- `python -m json.tool` parsed constants, detailed report, and replicate report.
- `py_compile` final gate is not used because `.pyc` atomic replacement fails with `WinError 5`; this is recorded as filesystem evidence, not a source compile error.
- Safe cleanup was attempted after workspace path resolution. `Tools\AI_Sim\__pycache__` and `Tools\__pycache__` deletion remains blocked by `WinError 5`.

Exact Microseconds saved:
- 0 measured runtime microseconds. Offline guard only. Unity runtime/profiler proof remains absent.

## 2026-05-14 - Million-Frame Timeline Guard

What was wrong:
- The detailed million-frame report could lose or corrupt timeline samples while endpoint score/population checks still passed.

What was done:
- Added `validate_million_frame_samples()` to `Tools/AI_Sim/FaunaBalanceSim.py`.
- `--check-artifacts` now validates sample count `101`, first frame `0`, last frame `1000000`, integer frame fields, and strict frame ordering.
- Added negative tests for sample truncation and frame-order drift.

Cinematic Cheats used:
- Timeline validation remains offline and data-only. No runtime AI or Unity integration.

Verification:
- `python -B -m unittest Tools.AI_Sim.test_fauna_balance_sim -v` -> `24` tests, `OK`, elapsed `3.253 s`.
- Direct source compile check via Python `compile()` -> `SOURCE_COMPILE_PASS`.
- `python -B Tools\AI_Sim\FaunaBalanceSim.py --check-artifacts` -> `ARTIFACT_CHECK_PASSED`.

Exact Microseconds saved:
- 0 measured runtime microseconds. Offline guard only. Unity runtime/profiler proof remains absent.

## 2026-05-14 - Final Cleanup and Evidence Rerun

What was wrong:
- Generated Python cache/test directories were previously left as a permission-blocked cleanup item.

What was done:
- Removed `Tools\AI_Sim\__pycache__`, `Tools\__pycache__`, and `Temp\FaunaBalanceSimTests` after verifying resolved paths stayed under `C:\Hecton8`.

Verification:
- `python -B -m unittest Tools.AI_Sim.test_fauna_balance_sim -v` -> `24` tests, `OK`, elapsed `8.199 s`.
- Direct source compile check via Python `compile()` -> `SOURCE_COMPILE_PASS`.
- `python -B Tools\AI_Sim\FaunaBalanceSim.py --check-artifacts` -> `ARTIFACT_CHECK_PASSED`.
- `python -m json.tool` parsed compact constants, detailed report, and replicate report.

Exact Microseconds saved:
- 0 measured runtime microseconds. Offline guard only. Unity runtime/profiler proof remains absent.

## 2026-05-14 - Sweet Spot and Run-Weight Contract

What was wrong:
- Heatmap evidence could exist without proving the exported aggression/fear sweet spot.
- Million-frame evidence could theoretically drift to weights different from the selected constants.

What was done:
- `--check-artifacts` now validates that `heatmapTop10[0]` carries the selected `aggressionScalar` and `fearScalar`.
- `--check-artifacts` now validates `millionFrameRun.weights` against the exported selected constants.
- Added negative tests for heatmap sweet-spot drift and million-frame weight drift.

Cinematic Cheats used:
- Validation remains offline and data-only. No runtime AI or Unity integration.

Verification:
- `python -B -m unittest Tools.AI_Sim.test_fauna_balance_sim -v` -> `22` tests, `OK`, elapsed `2.822 s`.
- Direct source compile check via Python `compile()` -> `SOURCE_COMPILE_PASS`.
- `python -B Tools\AI_Sim\FaunaBalanceSim.py --check-artifacts` -> `ARTIFACT_CHECK_PASSED`.

Exact Microseconds saved:
- 0 measured runtime microseconds. Offline guard only. Unity runtime/profiler proof remains absent.

## 2026-05-14 - Original Task Evidence Guard

What was wrong:
- Artifact validation did not yet prove that the original task evidence remained present: heatmap rows, expected 1-bit radar noise cases, retinal-blind acoustic compensation, and quadratic-vs-linear fear proof.
- Compact million-frame summary validation omitted score and stability.
- `load_selected_weights()` validated selected constants but not the artifact header.
- Generated Python cache folders could not be deleted under the current sandbox and polluted status.

What was done:
- Added `validate_task_evidence()` to `Tools/AI_Sim/FaunaBalanceSim.py`.
- `--check-artifacts` now validates heatmap length, exact noise case list, `sensoryNoiseTolerance=0.12`, retinal acoustic compensation superiority, acoustic/no-acoustic ratios, required fear-curve rows, quadratic fear improvement, and linear-vs-quadratic delta.
- Added million-frame `score` and `stability` comparisons between compact constants and detailed report.
- `load_selected_weights()` now validates artifact header/provenance/status before constructing `UtilityWeights`.
- Added `.gitignore` patterns for Python `__pycache__` and `.pyc` artifacts because cleanup is blocked by `WinError 5`.

Cinematic Cheats used:
- Kept all evidence validation offline. No runtime fauna simulation, no C# integration, no Dotnet.

Verification:
- Final rerun `python -B -m unittest Tools.AI_Sim.test_fauna_balance_sim -v` -> `20` tests, `OK`, elapsed `5.311 s`.
- Direct source compile check via Python `compile()` -> `SOURCE_COMPILE_PASS`.
- `python -B Tools\AI_Sim\FaunaBalanceSim.py --check-artifacts` -> `ARTIFACT_CHECK_PASSED`.
- `python -m json.tool` parsed compact constants, detailed report, and replicate report.
- `git diff --check` -> no whitespace errors; only LF-to-CRLF warnings.

Exact Microseconds saved:
- 0 measured runtime microseconds. Offline guard only. Unity runtime/profiler proof remains absent.

## 2026-05-14 - Replicate Contract Guard

What was wrong:
- Replicate validation could drift on `framesPerReplicate`, `replicates`, or detailed replicate weights while still preserving `status`, `failureCount`, and population summary.

What was done:
- Added `REPLICATE_WEIGHT_KEYS` and compare the detailed replicate report's validated weights against compact selected constants.
- Added compact-summary comparisons for `framesPerReplicate` and `replicates`.
- Added negative tests for replicate summary drift and replicate weight drift.

Cinematic Cheats used:
- Kept repeat-seed evidence offline and scalar-based. No runtime creature simulation was added.

Verification:
- `python -B -m unittest Tools.AI_Sim.test_fauna_balance_sim -v` -> `13` tests, `OK`, elapsed `2.587 s`.
- Direct source compile check via Python `compile()` -> `SOURCE_COMPILE_PASS`.
- `python -B Tools\AI_Sim\FaunaBalanceSim.py --check-artifacts` -> `ARTIFACT_CHECK_PASSED`.

Exact Microseconds saved:
- 0 measured runtime microseconds. Offline guard only. Unity runtime/profiler proof remains absent.
