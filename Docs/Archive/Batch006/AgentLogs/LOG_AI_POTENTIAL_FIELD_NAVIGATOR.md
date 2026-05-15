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

## 2026-05-15 - Source Drift Guard

What was wrong -> The tuning JSON could validate internally while drifting from live `HectonFluidEngine.cs` constants after a future fluid-system edit.

What was done -> Added live source-constant validation to `Tools/AiPathSim.py --check`. The guard parses `AbyssalFlowTextureResolution`, `AbyssalFlowTextureWorldSizeMeters`, `VectorNoiseResolution`, `SurfaceStormLayerDepthMeters`, `StormSurfaceTurbulenceStrength`, `AbyssalFlowThermoclineDepthMeters`, and `MaxAbyssalHeatSourceCount`, then compares source values against `Data/AI/Navigation_Tuning.json`.

Cinematic Cheats used -> Same boundary remains: flow is a hint vector and SDF is a proxy. The new work only prevents stale data; it does not introduce physical water authority, GPU readback, static NavMesh, or runtime class coupling.

Verification -> `python Tools/AiPathSim.py` printed `NAVIGATION OPTIMIZED`. `python Tools/AiPathSim.py --check` printed `CHECK PASSED`. `python -m unittest Tools.AI_Sim.test_ai_path_sim` ran 6 tests and passed. Corrected standalone source assertion printed `source-constants-ok`. `python -m py_compile Tools/AiPathSim.py Tools/AI_Sim/test_ai_path_sim.py` passed.

Exact Microseconds saved -> 0 Unity runtime microseconds claimed. Source drift validation is offline tooling only.

Regression model -> CPU: unchanged runtime design. GC: no Unity hot-path code touched. Memory: no Unity allocation. Cadence: unchanged. Correctness: source/export mismatch now fails before runtime handoff.

## 2026-05-15 - Duplicate Source Constant Guard

What was wrong -> `HectonFluidEngine.cs` defines storm-layer constants in more than one source context. A first-match parser could accept one value while another context diverged.

What was done -> Changed the simulator source parser to collect every matching constant occurrence and validate each value against the JSON snapshot and expected source contract. Added a regression test for duplicate `SurfaceStormLayerDepthMeters` and `StormSurfaceTurbulenceStrength` occurrences.

Cinematic Cheats used -> No runtime behavior changed. This protects the analytical-flow hint contract used by the steering fake.

Verification -> `python Tools/AiPathSim.py` printed `NAVIGATION OPTIMIZED`. `python Tools/AiPathSim.py --check` printed `CHECK PASSED`. `python -m unittest Tools.AI_Sim.test_ai_path_sim` ran 7 tests and passed. `python -m py_compile Tools/AiPathSim.py Tools/AI_Sim/test_ai_path_sim.py` passed. Standalone source assertion printed `duplicate-source-constants-ok`.

Exact Microseconds saved -> 0 Unity runtime microseconds claimed. Duplicate constant validation is offline tooling only.

Regression model -> CPU/GC/memory/cadence unchanged for runtime. Correctness: source split-brain drift now fails before handoff.

## 2026-05-15 - Black Box Handoff

What was wrong -> The navigation handoff required future AI runtime black-box telemetry, but the export did not define a concrete schema.

What was done -> Added `blackBoxTelemetry` to `Data/AI/Navigation_Tuning.json` through `Tools/AiPathSim.py`. It specifies 300 frames, circular `NativeArray<AiPotentialFieldTelemetryEntry>` storage, dump path `Docs/AgentLogs/Dump_AI_POTENTIAL_FIELD_NAVIGATOR.bin`, dump triggers, finite guards, and required fields: frame/entity ids, AUP cell, local position, velocity, target distance, flow alignment, SDF clearance, flags, and state hash.

Cinematic Cheats used -> No physical simulation added. The telemetry contract protects the existing analytical-flow/SDF-proxy steering fake.

Verification -> `python Tools/AiPathSim.py` printed `NAVIGATION OPTIMIZED`. `python Tools/AiPathSim.py --check` printed `CHECK PASSED`. `python -m unittest Tools.AI_Sim.test_ai_path_sim` ran 8 tests and passed. `python -m py_compile Tools/AiPathSim.py Tools/AI_Sim/test_ai_path_sim.py` passed. Standalone assertion printed `blackbox-contract-ok`.

Exact Microseconds saved -> 0 Unity runtime microseconds claimed. The black-box schema is exported metadata only; runtime cost remains PENDING UNITY VERIFICATION.

Regression model -> CPU/GC/memory/cadence unchanged in this patch. Correctness: future runtime handoff now has a fixed crash/NaN evidence schema instead of an inferred one.

## 2026-05-15 - Metric Replay Guard

What was wrong -> The JSON artifact could retain valid selected weights while raw/smoothed/idle/search metrics were stale or manually edited.

What was done -> `Tools/AiPathSim.py --check` now reruns the deterministic candidate search and requires selected weights, raw metrics, smoothed metrics, idle drift metrics, candidate count, and reached count to match the exported JSON exactly at export rounding.

Cinematic Cheats used -> No new simulation truth added. This only protects evidence for the existing analytical-flow/SDF-proxy steering fake.

Verification -> `python Tools/AiPathSim.py` printed `NAVIGATION OPTIMIZED`. `python Tools/AiPathSim.py --check` printed `CHECK PASSED`. `python -m unittest Tools.AI_Sim.test_ai_path_sim` ran 9 tests and passed. `python -m py_compile Tools/AiPathSim.py Tools/AI_Sim/test_ai_path_sim.py` passed. Standalone assertion printed `metric-replay-json-ok`.

Exact Microseconds saved -> 0 Unity runtime microseconds claimed. Replay validation is offline tooling only.

Regression model -> CPU/GC/memory/cadence unchanged for runtime. Correctness: metric evidence now fails fast if it diverges from deterministic replay.

## 2026-05-15 - State Hysteresis Guard

What was wrong -> The Low/Middle/High/Ultra steering tier profiles were AI scalability switches without exported hysteresis bands.

What was done -> Added 5m distance and 3s dwell-time hysteresis to every tier profile in `Data/AI/Navigation_Tuning.json`. `Tools/AiPathSim.py --check` now rejects missing or out-of-range hysteresis. Added a regression test for all four tiers.

Cinematic Cheats used -> No new simulation truth added. Hysteresis stabilizes the existing math-LOD steering fake so visual/path behavior does not flicker at tier thresholds.

Verification -> `python Tools/AiPathSim.py` printed `NAVIGATION OPTIMIZED`. `python Tools/AiPathSim.py --check` printed `CHECK PASSED`. `python -m unittest Tools.AI_Sim.test_ai_path_sim` ran 10 tests and passed. `python -m py_compile Tools/AiPathSim.py Tools/AI_Sim/test_ai_path_sim.py` passed. Standalone assertion printed `hysteresis-json-ok`.

Exact Microseconds saved -> 0 Unity runtime microseconds claimed. Hysteresis is exported metadata; runtime cost remains PENDING UNITY VERIFICATION.

Regression model -> CPU: future runtime adds bounded scalar state checks only. GC/memory: no runtime allocation introduced here. Cadence: unchanged. Correctness: steering tiers now have state stability instead of immediate flipping.

## 2026-05-15 - Path Trace Evidence

What was wrong -> The simulator exported aggregate metrics but not the selected route through the hurricane current.

What was done -> Added compact `pathTrace` samples to `Data/AI/Navigation_Tuning.json`. Each sample records step, time, position, target distance, SDF clearance, and signed flow alignment. `Tools/AiPathSim.py --check` now rejects stale path samples by comparing them against deterministic replay.

Cinematic Cheats used -> The trace documents the analytical-flow/SDF-proxy path. It does not add per-entity water physics or GPU readback.

Verification -> `python Tools/AiPathSim.py` printed `NAVIGATION OPTIMIZED`. `python Tools/AiPathSim.py --check` printed `CHECK PASSED`. `python -m unittest Tools.AI_Sim.test_ai_path_sim` ran 11 tests and passed. `python -m py_compile Tools/AiPathSim.py Tools/AI_Sim/test_ai_path_sim.py` passed. Standalone assertion printed `path-trace-json-ok`.

Exact Microseconds saved -> 0 Unity runtime microseconds claimed. Path trace generation is offline evidence only.

Regression model -> CPU/GC/memory/cadence unchanged for runtime. Correctness: route evidence now fails fast if it diverges from deterministic replay.

## 2026-05-15 - Deterministic Export Guard

What was wrong -> `Data/AI/Navigation_Tuning.json` included Python wall-clock timing, so identical simulator runs produced different hashes.

What was done -> Removed volatile `pythonMicroBenchmark` export data and replaced it with deterministic `sampleCostModel` metadata. Added `test_export_regeneration_is_deterministic`, which reruns `Tools/AiPathSim.py` and byte-compares the JSON.

Cinematic Cheats used -> No runtime behavior changed. This keeps the analytical-flow/SDF-proxy tuning artifact stable and diffable.

Verification -> Repeated `python Tools/AiPathSim.py` runs produced identical SHA256 `BE874CA325A9B3DE0BAABB6784837C4DBA7F5BA66B93EFAD63F3D750D7FFA693`. `python Tools/AiPathSim.py --check` printed `CHECK PASSED`. `python -m unittest Tools.AI_Sim.test_ai_path_sim` ran 13 tests and passed. Regression suite now includes deterministic regeneration coverage.

Exact Microseconds saved -> 0 Unity runtime microseconds claimed. Removed Python timing from handoff data because it was not Unity evidence.

Regression model -> CPU/GC/memory/cadence unchanged for runtime. Correctness: export churn now indicates real data change, not workstation load noise.
