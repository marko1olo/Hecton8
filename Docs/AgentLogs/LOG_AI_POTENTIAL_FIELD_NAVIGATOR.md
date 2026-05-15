# LOG_AI_POTENTIAL_FIELD_NAVIGATOR

## 2026-05-15 - Potential Field Navigation

What was wrong -> Flow-aware predator steering had no bounded tuning artifact for treating `AbyssalFlowField` currents as boost/resistance signals. A naive EWMA on the entire steering vector delayed wall repulsion and allowed SDF penetration in the simulator.

What was done -> Added `Tools/AiPathSim.py`, `Docs/ARCHITECTURE/AI_POTENTIAL_FIELD_NAVIGATION.md`, and `Data/AI/Navigation_Tuning.json`. The simulator searches deterministic weights for a predator crossing a hurricane current, exports tier profiles, records idle current drift, and models 100 predators at 10Hz.

Cinematic Cheats used -> Current is an analytical hint vector, not a global force simulation. Idle fauna drift visually with the current. Obstacle truth remains an SDF clearance proxy. GPU flow texture readback, static NavMesh, raycast fan steering, and per-entity water physics were rejected.

Verification -> `python Tools/AiPathSim.py` printed `NAVIGATION OPTIMIZED`. Final selected run: 48 candidates evaluated, 30 reached, smoothed path reached in 34.6s, final distance 2.8104m, jitter events 1, SDF pushout events 0, minimum clearance 2.2249m. `python -m py_compile Tools/AiPathSim.py` passed. `Data/AI/Navigation_Tuning.json` parsed via Python. Per-file `git diff --check` passed for touched files. No root `.sln` or `.csproj` exists; no C# runtime file was edited.

Exact Microseconds saved -> 0 Unity runtime microseconds claimed. Static model only: 100 predators at 10Hz = 1000 samples/sec = 16.67 samples/frame at 60Hz; low-tier estimate 3,500 scalar ops/frame, high-tier estimate 7,166.67 scalar ops/frame. Runtime frame-time and GC remain PENDING UNITY VERIFICATION.

Regression model -> CPU: O(1) steering samples staggered at 10Hz base. GC: runtime design is struct/native-data only; Python tooling allocations are irrelevant to player runtime. Memory: JSON/data docs only, no persistent Unity allocation. Cadence: Low/Middle 10Hz, High 15Hz, Ultra 20Hz. Correctness: non-finite flow falls back to target/SDF steering; SDF repulsion is immediate and not EWMA-smoothed.

Polish -> `<POLISH_MANDATE>` tag was absent from `Docs/Tasks/CURRENT_BATCH.md`; recorded as dependency-blocked. Local anti-bloat pass removed unused Python imports and kept scope out of prefabs/scenes/project settings.

## 2026-05-15 - Continuation Hardening

What was wrong -> The simulator/export could still be weakened later without a local regression test, and the flow constants read from source were not pinned in the exported tuning data.

What was done -> Added `Tools/AI_Sim/test_ai_path_sim.py`, added `sourceParameterSnapshot` to `Data/AI/Navigation_Tuning.json`, and documented the pinned constants in `Docs/ARCHITECTURE/AI_POTENTIAL_FIELD_NAVIGATION.md`.

Cinematic Cheats used -> Same as core pass: analytical flow hints and SDF proxy steering; no GPU flow readback and no per-entity physical water simulation.

Verification -> `python Tools/AiPathSim.py` printed `NAVIGATION OPTIMIZED`. `python -m py_compile Tools/AiPathSim.py Tools/AI_Sim/test_ai_path_sim.py` passed. `python -m unittest Tools.AI_Sim.test_ai_path_sim` ran 4 tests in 0.487s and passed. Exported source constants: 32 flow texture resolution, 100m volume, 3.125m cell, 32 vector noise resolution, 50m storm layer, 0.4 storm turbulence, 120m thermocline, 8 heat sources.

Exact Microseconds saved -> 0 Unity runtime microseconds claimed. Regression test runtime was approximately 487,000 us and is tooling-only.

## 2026-05-15 - Artifact Self-Check

What was wrong -> A stale `Data/AI/Navigation_Tuning.json` could survive after simulator edits and still look complete from file presence alone.

What was done -> Added `python Tools/AiPathSim.py --check`. The check loads the exported JSON, validates schema/status/evidence class/source constants, reconstructs selected weights, replays the smoothed and raw simulations, validates target reach, zero SDF pushouts, >=2m clearance, <=1 jitter event, EWMA jitter non-regression, idle current drift, and the 100-predator 10Hz performance constants.

Cinematic Cheats used -> The check preserves the same cheat boundary: analytical current hint, SDF proxy repulsion, no static NavMesh, no GPU readback, no per-predator water physics.

Verification -> `python -m py_compile Tools/AiPathSim.py Tools/AI_Sim/test_ai_path_sim.py` passed. `python Tools/AiPathSim.py` regenerated `Data/AI/Navigation_Tuning.json` and printed `NAVIGATION OPTIMIZED`. `python Tools/AiPathSim.py --check` printed `CHECK PASSED`. `python -m unittest Tools.AI_Sim.test_ai_path_sim` ran 5 tests in 0.471s and passed. JSON invariant command printed `json-assert-ok`. `git diff --check` passed on touched files. Debt-marker scan returned no hits.

Exact Microseconds saved -> 0 Unity runtime microseconds claimed. New artifact check cost is tooling-only. Runtime frame-time and GC remain PENDING UNITY VERIFICATION.

Regression model -> CPU: unchanged runtime design; only offline verification added. GC: no Unity hot-path code touched. Memory: no Unity allocation added. Cadence: unchanged Low/Middle 10Hz, High 15Hz, Ultra 20Hz. Correctness: stale or weakened tuning now fails fast before handoff.

Artifact polish -> Exported float weights are rounded to six decimals before JSON write. Verified `Low.sdfObstacleRepulsionWeight == 122.4` and `Ultra.flowAlignmentBoostWeight == 0.225`; no binary float noise remains in tier handoff values.
