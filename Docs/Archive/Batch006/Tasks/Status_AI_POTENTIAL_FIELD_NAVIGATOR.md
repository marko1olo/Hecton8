# Status_AI_POTENTIAL_FIELD_NAVIGATOR

Prompt ID: AI_POTENTIAL_FIELD_NAVIGATOR
Role: AI_PROGRAMMER
Domain: ECHELON 3 - FLORA, FAUNA & BIOTA / flow-aware predator steering
Task count used: 8 numbered tasks. Prompt header says 15; XML contains 8 executable numbered tasks.
Status: NAVIGATION OPTIMIZED (PY_SIM) / PENDING UNITY VERIFICATION

## Mandates Loaded

- CORE_Weather_Abyssal_FlowField_Currents.txt
- AI_DYNAMIC_NAVGRID_SDF_INTEGRATION.txt
- AI_Navigation_AStar_Funnel_Smoothing_Pathfinding.txt
- AI_Creature_Cognition_States.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- MATH_Rsqrt_i3_SIMD.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Execution_Phases.txt

## Iterative Loop 0 - Intake

- [x] Extract own XML prompt from `Docs/Tasks/CURRENT_BATCH.md` | Justification: strict batch parsing used a PowerShell regex over raw file text to avoid truncated MCP-style reads. Alternative rejected: relying on neighboring prompt context. Estimate: 20,000,000 us.
- [x] Read domain authority and relevant mandates | Justification: DOD practice is mandate-first, not code-first. Alternative rejected: generic steering implementation without flow/SDF contracts. Estimate: 50,000,000 us.
- [x] Confirm status/rationale hygiene | Justification: missing status/rationale means no stale active-batch memory for this ID. Alternative rejected: reusing another agent's task file. Estimate: 31,000,000 us.

## Iterative Loop 1 - Tasks 1-5

- [x] Task 1 - Vector field integration | Justification: design consumes the source-confirmed analytical `AbyssalFlowField` contract and rejects GPU readback/direct force authority. Alternative rejected: per-entity water physics or direct dependency on `HectonFluidEngine` internals. Estimate: 85,000,000 us.
- [x] Task 2 - Potential field math | Justification: formula separates target pull, current boost/resistance, and immediate SDF repulsion as data-only math. Alternative rejected: full A* for distant flow steering and Bezier/relaxation smoothing. Estimate: 40,000,000 us.
- [x] Task 3 - Obstacle repulsion | Justification: implemented 1/d^2 SDF proxy repulsion with clamps and an explicit push-out fallback for simulator safety. Alternative rejected: raycast fan steering and delayed EWMA wall response. Estimate: 55,000,000 us.
- [x] Task 4 - Target attraction | Justification: normalized player/prey biomass pull is explicit and finite-guarded in the exported formula. Alternative rejected: target teleport/path-node chasing as a steering replacement. Estimate: 25,000,000 us.
- [x] Task 5 - Navigation simulator | Justification: `Tools/AiPathSim.py` simulates a predator crossing a hurricane current and writes deterministic metrics. Alternative rejected: hand-written unmeasured weights. Estimate: 36,000,000 us after final optimized run.
- [x] Compile/simulator verification after Tasks 1-5 | Justification: `python -m py_compile Tools/AiPathSim.py` passed and simulator reached target. Alternative rejected: claiming Unity verification from tooling. Estimate: 19,800,000 us.

## Iterative Loop 2 - Tasks 6-8

- [x] Task 6 - Self-audit jitter/EWMA smoothing | Justification: first successful run exposed jitter; EWMA now smooths target/current intent while SDF repulsion stays immediate. Alternative rejected: smoothing the wall term, which caused clipping. Estimate: 101,500,000 us.
- [x] Task 7 - Performance model 100 predators at 10Hz | Justification: JSON includes 100 predators at 10Hz, 1000 samples/sec, 16.67 samples/frame, and scalar-op estimates. Alternative rejected: fabricated Unity profiler numbers. Estimate: 20,000,000 us.
- [x] Task 8 - Export `Data/AI/Navigation_Tuning.json` | Justification: simulator writes selected weights, tier profiles, idle drift, failure modes, and metrics. Alternative rejected: Unity asset mutation or ScriptableObject runtime mutation. Estimate: 36,000,000 us.
- [x] Compile/simulator verification after Tasks 6-8 | Justification: final `python -m py_compile` passed; final simulator status printed `NAVIGATION OPTIMIZED`. Alternative rejected: calling this Unity-verified. Estimate: 19,800,000 us.

## Iterative Loop 3 - Source Self-Read

- [x] Read own simulator/design output for missed edge cases | Justification: self-read caught negative SDF clearance and delayed repulsion; both were corrected. Alternative rejected: accepting a path that reached target while clipping geometry. Estimate: 118,000,000 us.

## Iterative Loop 4 - Integration/Static Audit

- [x] Run static scans and available build/test command | Justification: Python compile passed, JSON parsed, per-file `git diff --check` passed, and no root `.sln`/`.csproj` exists for a relevant C# build. Alternative rejected: running unrelated Unity/runtime claims from tooling-only edits. Estimate: 74,900,000 us JSON parse, 33,000,000 us script diff check, 46,600,000 us doc diff checks.

## Iterative Loop 5 - Polish Mandate

- [x] Read `<POLISH_MANDATE>` after all core tasks are done/blocked | Justification: attempted exact tag extraction from `Docs/Tasks/CURRENT_BATCH.md`; tag is missing. Alternative rejected: inventing polish instructions. Estimate: 19,300,000 us. [BLOCKED BY DEPENDENCY: POLISH_MANDATE tag absent]
- [x] Execute final anti-bloat pass | Justification: scanned touched files for debt markers and stub text, removed unused imports, verified no prefab/YAML/project-setting edits. Alternative rejected: expanding runtime C# scope without prompt. Estimate: 43,400,000 us.

## Iterative Loop 6 - Continuation Hardening

- [x] Pin source flow parameters in tuning export | Justification: task explicitly required reading `AbyssalFlowField` noise/flow parameters; JSON now records flow texture resolution, 100m volume, 3.125m cell size, 32^3 vector noise, storm layer/turbulence, thermocline depth, and heat-source cap. Alternative rejected: undocumented constants hidden only inside simulator code. Estimate: 49,100,000 us.
- [x] Add regression tests for simulator and export | Justification: `Tools/AI_Sim/test_ai_path_sim.py` asserts NAVIGATION OPTIMIZED status, source constants, target reach, zero SDF pushouts, clearance >= 2m, jitter <= 1, and idle current drift. Alternative rejected: manual-only reruns. Estimate: 23,000,000 us.

## Iterative Loop 7 - Artifact Self-Check

- [x] Add simulator `--check` mode | Justification: generated tuning can now be replay-validated from disk for schema, source constants, selected weights, reach, clearance, jitter, idle drift, and 100-predator performance model. Alternative rejected: trusting a stale JSON artifact after future edits. Estimate: 31,000,000 us.
- [x] Re-run simulator, self-check, py_compile, unit tests, and diff whitespace guard | Justification: `python Tools/AiPathSim.py` regenerated the export, `python Tools/AiPathSim.py --check` passed, `python -m unittest Tools.AI_Sim.test_ai_path_sim` ran 5 tests OK, and `git diff --check` passed on touched files. Alternative rejected: chat-only completion evidence. Estimate: 89,000,000 us.
- [x] Final artifact invariant/debt scan | Justification: JSON invariant command confirmed optimized status, source resolution, zero SDF pushouts, and >=2m clearance; `rg` debt-marker scan returned no hits in touched files. Alternative rejected: leaving final acceptance to visual inspection. Estimate: 12,000,000 us.
- [x] Normalize exported float precision | Justification: tier tuning no longer leaks Python binary float tail values; regenerated JSON verified via `json-float-normalized`, self-check, py_compile, and unit tests. Alternative rejected: leaving machine-noisy values in a handoff data artifact. Estimate: 14,000,000 us.

## Iterative Loop 8 - Source Drift Guard

- [x] Validate export against live `HectonFluidEngine.cs` constants | Justification: `Tools/AiPathSim.py --check` now parses source constants and rejects drift between source, JSON snapshot, and expected flow values. Alternative rejected: accepting stale tuning if the fluid engine constants change later. Estimate: 42,000,000 us.
- [x] Extend regression suite to six tests | Justification: `test_source_constants_match_export` exercises the same source drift guard and the corrected standalone assertion printed `source-constants-ok`. Alternative rejected: hidden source coupling documented only in prose. Estimate: 18,000,000 us.
- [x] Validate duplicate source constant occurrences | Justification: parser now checks every matching occurrence of duplicated storm-layer constants; regression suite reached 7 tests and standalone assertion printed `duplicate-source-constants-ok`. Alternative rejected: accepting the first matching source constant while another class silently diverges. Estimate: 21,000,000 us.

## Iterative Loop 9 - Black Box Handoff

- [x] Export AI black-box telemetry contract | Justification: `Data/AI/Navigation_Tuning.json` now includes a 300-frame circular `NativeArray<AiPotentialFieldTelemetryEntry>` contract, dump path, triggers, finite guards, and required telemetry fields. Alternative rejected: leaving Black Box compliance as prose only. Estimate: 28,000,000 us.
- [x] Validate telemetry contract in simulator checks | Justification: `python Tools/AiPathSim.py --check` and the regression suite reject missing black-box capacity/path/fields; tests reached 8 and `blackbox-contract-ok` passed. Alternative rejected: relying on future runtime agents to infer telemetry shape. Estimate: 16,000,000 us.

## Iterative Loop 10 - Metric Replay Guard

- [x] Validate stored metrics against deterministic replay | Justification: `Tools/AiPathSim.py --check` now recomputes the full candidate search and rejects stale raw/smoothed/idle/search metrics that do not match the exported JSON. Alternative rejected: accepting plausible selected weights with hand-edited or stale metrics. Estimate: 57,000,000 us.
- [x] Extend regression suite to nine tests | Justification: `test_exported_metrics_match_replay` asserts selected weights, raw metrics, smoothed metrics, idle drift, and search counts match a fresh replay; standalone JSON invariant printed `metric-replay-json-ok`. Alternative rejected: metric validation only in prose. Estimate: 47,000,000 us.

## Iterative Loop 11 - State Hysteresis Guard

- [x] Add tier-switch hysteresis to Low/Middle/High/Ultra profiles | Justification: AI scalability switches now include a 5m distance band and 3s dwell time so runtime steering tier changes cannot flip-flop immediately. Alternative rejected: cadence-only tiers with no state stability band. Estimate: 22,000,000 us.
- [x] Validate hysteresis in check/test suite | Justification: `Tools/AiPathSim.py --check` rejects missing/out-of-range hysteresis, regression suite reached 10 tests, and standalone invariant printed `hysteresis-json-ok`. Alternative rejected: relying on implementer memory of the hysteresis mandate. Estimate: 31,000,000 us.

## Iterative Loop 12 - Path Trace Evidence

- [x] Export compact selected-path trace | Justification: `Data/AI/Navigation_Tuning.json` now records deterministic path samples with step, time, position, target distance, SDF clearance, and flow alignment. Alternative rejected: aggregate metrics without route evidence. Estimate: 38,000,000 us.
- [x] Validate trace against replay | Justification: `Tools/AiPathSim.py --check` and `test_path_trace_matches_replay` reject stale path samples; regression suite reached 11 tests and standalone invariant printed `path-trace-json-ok`. Alternative rejected: visual inspection of JSON samples. Estimate: 46,000,000 us.

## Iterative Loop 13 - Deterministic Export Guard

- [x] Remove volatile Python wall-clock timing from JSON | Justification: `pythonMicroBenchmark` made identical simulator runs produce different `Navigation_Tuning.json` hashes; export now uses deterministic `sampleCostModel`. Alternative rejected: keeping workstation-noisy timing as handoff evidence. Estimate: 25,000,000 us.
- [x] Add byte-stable regeneration test | Justification: `test_export_regeneration_is_deterministic` reruns the simulator and byte-compares `Data/AI/Navigation_Tuning.json`; regression suite reached 13 tests and manual hash check stayed `BE874CA325A9B3DE0BAABB6784837C4DBA7F5BA66B93EFAD63F3D750D7FFA693` across repeated runs. Alternative rejected: assuming determinism from stable aggregate metrics. Estimate: 50,000,000 us.

## Iterative Loop 14 - Validator Fail-Closed Guard

- [x] Harden numeric validation against malformed JSON | Justification: `finite_float` now prevents malformed source/hysteresis/path numeric fields from crashing `--check`; invalid data returns structured errors. Alternative rejected: letting Python `float()` exceptions terminate artifact validation. Estimate: 24,000,000 us.
- [x] Add corrupted-field regression coverage | Justification: `test_malformed_numeric_fields_fail_closed` corrupts hysteresis and source snapshot values in memory; regression suite reached 14 tests and standalone invariant printed `fail-closed-json-ok`. Alternative rejected: testing only valid exports. Estimate: 32,000,000 us.

## Iterative Loop 15 - JSON Root/Parse Fail-Closed Guard

- [x] Harden invalid JSON handling | Justification: `check_export` now catches `JSONDecodeError`/IO errors and reports `CHECK FAILED` instead of traceback; `validate_export` rejects non-object roots. Alternative rejected: letting malformed files crash the checker. Estimate: 18,000,000 us.
- [x] Add invalid JSON regression coverage | Justification: `test_invalid_json_and_non_object_roots_fail_closed` validates JSON array roots and temporary malformed JSON files fail cleanly; regression suite reached 15 tests and standalone invariant printed `invalid-json-fail-closed-ok`. Alternative rejected: only testing syntactically valid JSON. Estimate: 27,000,000 us.

## Iterative Loop 16 - Test Output Hygiene

- [x] Capture expected invalid-JSON checker output inside regression test | Justification: fail-closed behavior remains asserted while the unit suite no longer prints an expected `CHECK FAILED` line as loose output. Alternative rejected: suppressing checker output globally or deleting the invalid JSON regression. Estimate: 9,000,000 us.
- [x] Re-run full verification chain after output cleanup | Justification: simulator, self-check, unit suite, py_compile, diff whitespace guard, debt scan, and deterministic export hash all remained clean. Alternative rejected: treating a test-only harness edit as too small to reverify. Estimate: 74,000,000 us.

## Iterative Loop 17 - CLI and Source Reference Fail-Closed Guard

- [x] Validate exported source contract paths on disk | Justification: `sourceFiles` are now checked as relative existing project paths, so stale evidence references fail the artifact check. Alternative rejected: trusting a copied source file list without disk validation. Estimate: 18,000,000 us.
- [x] Add explicit-path `--check` CLI validation | Justification: `Tools/AiPathSim.py --check <path>` now rejects corrupt external artifacts without traceback, which protects handoff automation. Alternative rejected: testing only the default generated JSON path. Estimate: 20,000,000 us.
- [x] Re-run full verification chain after CLI/source-reference hardening | Justification: simulator stayed `NAVIGATION OPTIMIZED`, default and explicit-path `--check` passed, regression suite reached 17 tests OK, py_compile passed, diff whitespace guard passed, debt scan had no hits, and JSON hash stayed stable. Alternative rejected: treating CLI/test hardening as documentation-only. Estimate: 92,000,000 us.

## Iterative Loop 18 - Architecture Handoff Sync

- [x] Update architecture handoff with explicit-path checker and source-file existence guard | Justification: documentation now matches the actual CLI and validation surface. Alternative rejected: leaving runtime integrators with stale default-only checker instructions. Estimate: 8,000,000 us.
- [x] Re-run post-doc verification guard | Justification: default and explicit-path `--check` passed, regression suite stayed 17 tests OK, py_compile passed, diff whitespace guard passed, debt scan had no hits, and JSON hash stayed stable. Alternative rejected: trusting documentation-only edits without verification. Estimate: 62,000,000 us.

## Iterative Loop 19 - Export Authority Field Guard

- [x] Validate `promptId` and `sourceContracts` in the checker | Justification: ownership and source-boundary fields are now hard contract data, so a hand-edited artifact cannot pass with the wrong agent id or a forbidden flow boundary. Alternative rejected: treating these fields as decorative JSON metadata. Estimate: 16,000,000 us.
- [x] Re-run authority-field verification chain | Justification: simulator stayed `NAVIGATION OPTIMIZED`, both checker paths passed, regression suite stayed 17 tests OK with new authority-field assertions, py_compile passed, diff guard passed, debt scan had no hits, and JSON hash stayed stable. Alternative rejected: accepting validator changes without proving deterministic export. Estimate: 176,000,000 us.

## Iterative Loop 20 - Formula Contract Guard

- [x] Validate exported steering formula text | Justification: `formula` is now a shared contract constant and `validate_export` rejects corrupted EWMA/SDF/flow formula text. Alternative rejected: treating formula strings as comments while metrics remain valid. Estimate: 14,000,000 us.
- [x] Re-run formula-contract verification chain | Justification: simulator stayed `NAVIGATION OPTIMIZED`, both checker paths passed, regression suite reached 18 tests OK, py_compile passed, diff guard passed, debt scan had no hits, and JSON hash stayed stable. Alternative rejected: accepting formula validation changes without replay and deterministic export proof. Estimate: 177,000,000 us.
