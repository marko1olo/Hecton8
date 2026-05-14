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

Regression model:
- CPU: no Unity runtime code changed; offline Python run elapsed 226.3 s.
- GC: no Unity runtime code changed; runtime GC proof absent.
- Memory: JSON artifacts are small data files; Unity memory impact unmeasured until integration.
- Cadence: no tick/update loop changed.
- Correctness: constants are simulation-balanced only; scene wiring and runtime consumption remain `PENDING VERIFICATION`.
