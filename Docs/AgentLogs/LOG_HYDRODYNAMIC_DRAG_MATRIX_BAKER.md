# LOG - HYDRODYNAMIC_DRAG_MATRIX_BAKER

## Entry

What was wrong:
Submarine hydrodynamic feel had no machine-readable Cd/Cl, added-mass, cavitation, torque, stop-distance, or power-curve definition. Without this, later Unity tuning would collapse into inspector guessing and spacecraft-like acceleration.

What was done:
Created `Tools/SubmarinePhysicsSim.py`.
Generated `Data/Physics/Submarine_Specs.json`.
Generated `Data/Physics/Submarine_SpeedPower.csv`.
Generated `Data/Physics/Submarine_SpeedPower.svg`.
Generated `Data/Physics/Submarine_SpeedPower.png`.
Generated `Data/Physics/Submarine_RuntimePack.bin`.
Generated `Data/Physics/Submarine_RuntimePackLayout.json`.
Generated `Data/Physics/Submarine_Verification.json`.
Created `Docs/Tasks/Status_HYDRODYNAMIC_DRAG_MATRIX_BAKER.md`.
Created `Docs/AgentLogs/Rationale_HYDRODYNAMIC_DRAG_MATRIX_BAKER.md`.
Added `Tools/test_submarine_physics_sim.py`.

Cinematic Cheats used:
Cavitation uses sigma-threshold tables to trigger acoustic/VFX states. No per-bubble vapor simulation.
Wake/noise/pressure feel is represented as exported thresholds and constants; runtime visual overkill is reserved for quality tiers.
No CFD, no mesh sampling, no bubble cloud physics, no Unity inspector tuning loop.

Hydrodynamic data:
Five hulls exported: Sleek Scout, Industrial Hauler, Boxy Salvage Tug, Alien Manta, Armored Crawler.
Each hull has Cd/Cl values, pitch/yaw lift curve samples, local XYZ CdA drag tensor, reverse axial CdA, square-drag acceleration tensor, added-mass tensor, effective-mass tensor, rigid/added angular inertia tensors, angular quadratic damping torque tensor, cavitation thresholds by depth, acceleration gate, and stop-distance check.
The export includes Low/Middle/High/Ultra runtime usage guidance.
Runtime binary pack: little-endian, magic `H8HYDRO\0`, version 1, hull count 5, 53 floats per record, 220-byte record stride, 1124 total bytes.
Runtime layout JSON: schema `hecton8.submarine_runtime_pack_layout.v1`, 24-byte header, 220-byte record, 55 record fields including shape hash and shape index.

Verification:
`python Tools/SubmarinePhysicsSim.py --out-dir Data/Physics` returned `HYDRODYNAMICS DEFINED`.
`python -m py_compile Tools/SubmarinePhysicsSim.py` passed.
Custom JSON/CSV/PNG validator passed: 5 hulls, monotonic power curves, valid PNG header, no 50 m/s reach within 90 seconds, stop distance >= 3 hull lengths.
Deterministic rerun hashes were stable for JSON, CSV, SVG, PNG, and verification JSON.
PNG header verified: magic `89504E470D0A1A0A`, IHDR, 1200x720, 8-bit RGB.
`Data/Physics/Submarine_Verification.json` includes bytes and SHA256 for specs, CSV, SVG, and PNG artifacts.
`Data/Physics/Submarine_Verification.json` includes bytes and SHA256 for the runtime binary pack.
`Data/Physics/Submarine_Verification.json` includes bytes and SHA256 for the runtime layout JSON.
`python -W error::ResourceWarning -m unittest Tools/test_submarine_physics_sim.py` passed 23 tests.
`python Tools/SubmarinePhysicsSim.py --out-dir Data/Physics --verify-only` returned `HYDRODYNAMICS DEFINED`.
No root `.sln` or `.csproj` found; C# compilation was not applicable because no C# files changed.
Unity Editor / Play Mode / GCMonitor verification not run.

Stop-distance results:
Sleek Scout: 24.088482 hull lengths.
Industrial Hauler: 14.062744 hull lengths.
Boxy Salvage Tug: 8.30792 hull lengths.
Alien Manta: 23.520304 hull lengths.
Armored Crawler: 11.029128 hull lengths.

50 m/s self-audit:
Sleek Scout terminal speed: 19.23782 m/s, time to 50 m/s: never within 90 seconds.
Industrial Hauler terminal speed: 12.068155 m/s, time to 50 m/s: never within 90 seconds.
Boxy Salvage Tug terminal speed: 8.929206 m/s, time to 50 m/s: never within 90 seconds.
Alien Manta terminal speed: 18.465898 m/s, time to 50 m/s: never within 90 seconds.
Armored Crawler terminal speed: 9.907589 m/s, time to 50 m/s: never within 90 seconds.

Exact Microseconds saved:
Measured Unity profiler proof absent.
Model estimate for runtime coefficient lookup instead of runtime hull coefficient fitting: 8-20 us saved per vehicle fixed-step.
Model estimate for diagonal added-mass tensor instead of coupled matrix solve: 3-8 us saved per vehicle fixed-step.
Model estimate for tensor angular constants instead of runtime angular shape solve: 2-5 us saved per vehicle fixed-step.
Model estimate for cavitation threshold event vs bubble simulation: >100 us saved during cavitation events.
Runtime cost of the Python baker: 0 us; it is offline.
Runtime cost of regression tests: 0 us; they are offline.
Runtime cost of the PNG plotter: 0 us; it is offline.
Runtime cost of artifact manifest hashing: 0 us; it is offline.
Runtime cost of verify-only validation: 0 us; it is offline.
Runtime cost of lift-curve baking: 0 us; it is offline.
Runtime cost of binary pack baking: 0 us; it is offline. Runtime import cost is load-time only, not hot path.
Runtime cost of runtime layout generation: 0 us; it is offline.
Runtime cost of binary pack round-trip validation: 0 us; it is offline. It prevents shifted-field imports before Unity integration.
Runtime cost of runtime layout drift validation: 0 us; it is offline. It prevents renamed/shifted layout fields before importer work.
Runtime cost of power CSV semantic validation: 0 us; it is offline. It prevents graph-source data drifting from JSON hydrodynamic specs.
Runtime cost of non-finite numeric validation: 0 us; it is offline. It prevents NaN/Inf coefficients from entering future runtime import paths.
Runtime cost of strict JSON output and manifest-updated binary/layout corruption gates: 0 us; they are offline.
Runtime cost of non-finite CSV speed/power validation: 0 us; it is offline.
Runtime cost of canonical specs document validation: 0 us; it is offline.
Runtime cost of verification-summary validation: 0 us; it is offline.
Runtime cost of canonical SVG/PNG plot validation: 0 us; it is offline.

## Entry - Runtime Pack Round-Trip Tightening

What was wrong:
The runtime binary pack had header, stride, hash, index, and layout checks, but field-level payload parity against `Submarine_Specs.json` was not enforced.

What was done:
Added `read_runtime_pack()` to decode `Data/Physics/Submarine_RuntimePack.bin`.
Added verify-only cross-checking for all 5 hulls and all 53 runtime float fields per record.
Added same-size runtime pack payload corruption coverage in `Tools/test_submarine_physics_sim.py`.
Regenerated `Data/Physics/*` and verified the current artifacts.

Cinematic Cheats used:
No new runtime simulation. The binary pack keeps the cavitation and wake truth as threshold data, leaving bubble visuals/audio as tier-gated presentation.

Exact Microseconds saved:
Measured Unity profiler proof absent.
Runtime hot-path cost remains 0 us for this batch because validation is offline.
The practical saving is integration risk: shifted binary fields now fail before a runtime importer can feed wrong drag or added-mass constants to FixedTick.

## Entry - Runtime Layout Drift Gate

What was wrong:
`Submarine_RuntimePackLayout.json` had a layout schema, but verify-only did not compare every header and field definition against the canonical generator output.

What was done:
Tightened `validate_existing_output()` to compare runtime layout header metadata, hull order, and all record field definitions against `runtime_pack_layout_document()`.
Added a regression test that mutates the `length_m` field name and expects verify-only to fail with `runtime layout field definition mismatch`.
Regenerated `Data/Physics/*` and verified current artifacts.

Cinematic Cheats used:
No new physical simulation. This is a data-contract guard for the existing threshold/tensor fake-first hydro model.

Exact Microseconds saved:
Measured Unity profiler proof absent.
Runtime hot-path cost remains 0 us because this is an offline validation gate.
Integration risk reduction: importers now fail before consuming a renamed or shifted field offset.

## Entry - Power CSV Semantic Gate

What was wrong:
`Submarine_SpeedPower.csv` was checked for columns, row count, monotonicity, and manifest hash, but not recalculated against the JSON hull specs.

What was done:
Added `power_csv_spec_failures()` in `Tools/SubmarinePhysicsSim.py`.
Verify-only now recomputes the full speed/power table from `Submarine_Specs.json` and compares every speed row and hull power cell.
Added a regression test that corrupts the CSV, updates its manifest to match the corrupted bytes, and still expects verify-only to fail.

Cinematic Cheats used:
No new physical simulation. This preserves the existing offline graph as evidence for the same deterministic drag model.

Exact Microseconds saved:
Measured Unity profiler proof absent.
Runtime hot-path cost remains 0 us because this is an offline validation gate.
Validation risk reduction: graph data can no longer drift silently from the hydrodynamic JSON authority.

Verification note:
An invalid parallel run of generation and verify-only against the same files produced a transient CSV manifest mismatch. Serial verify-only after generation passed and is the valid gate.

## Entry - Non-Finite Numeric Gate

What was wrong:
`Submarine_Specs.json` could contain `NaN`/`Inf` values because Python accepts non-finite JSON constants by default, and regular comparison gates can miss `NaN`.

What was done:
Added recursive non-finite numeric validation in `Tools/SubmarinePhysicsSim.py`.
Added a regression test that injects `NaN` into `terminal_speed_at_max_thrust_mps`, updates the manifest to match the corrupted spec file, and verifies the failure still occurs.
Regenerated `Data/Physics/*` and verified current artifacts serially.

Cinematic Cheats used:
No new simulation. This is a fail-fast data-health gate for the existing baked hydrodynamic constants.

Exact Microseconds saved:
Measured Unity profiler proof absent.
Runtime hot-path cost remains 0 us because this validation is offline.
Risk reduction: future runtime loaders should never receive NaN/Inf drag, mass, cavitation, or torque constants from this export.

## Entry - Strict JSON and Manifest-Updated Runtime Corruption Gates

What was wrong:
`write_json()` used Python's default permissive encoder, and two runtime corruption tests could still rely on artifact hash mismatch instead of proving semantic validation caught the corruption.

What was done:
Changed `write_json()` to use `allow_nan=False`.
Updated runtime pack corruption testing to refresh the manifest after corrupting the binary, forcing JSON-to-binary field validation to catch the drift.
Updated runtime layout drift testing to refresh the manifest after corrupting the layout, forcing full layout document validation to catch the drift.
Regenerated `Data/Physics/*` and verified current artifacts serially.

Cinematic Cheats used:
No new simulation. This is a data-authority hardening pass for the existing threshold/tensor hydrodynamic model.

Exact Microseconds saved:
Measured Unity profiler proof absent.
Runtime hot-path cost remains 0 us because this validation is offline.
Risk reduction: future importers cannot hide bad binary/layout data by updating hashes alone.

## Entry - Non-Finite CSV Gate

What was wrong:
`Submarine_SpeedPower.csv` semantic validation compared floats, but `nan` parses as a float and can evade ordinary comparison logic.

What was done:
Added finite-value checks for CSV speed rows and hull power cells in `power_csv_spec_failures()`.
Added a regression test that writes `nan` into a CSV power cell, refreshes the manifest, and verifies `--verify-only` still fails.
Regenerated `Data/Physics/*` and verified current artifacts serially.

Cinematic Cheats used:
No new simulation. This hardens the evidence plot data for the existing deterministic drag/power model.

Exact Microseconds saved:
Measured Unity profiler proof absent.
Runtime hot-path cost remains 0 us because this validation is offline.
Risk reduction: plot/source data cannot silently carry non-finite values into tuning reviews or downstream import tooling.

## Entry - Canonical Specs Document Gate

What was wrong:
`Submarine_Specs.json` metadata could drift without affecting CSV or binary-pack semantic checks.

What was done:
Added `canonical_export_document()` in `Tools/SubmarinePhysicsSim.py`.
Verify-only now compares `Submarine_Specs.json` to the canonical document rebuilt from the baked hull definitions.
Added a regression test that changes a hull display name, refreshes the specs manifest, and verifies `--verify-only` still fails.
Regenerated `Data/Physics/*` and verified current artifacts serially.

Cinematic Cheats used:
No new simulation. This protects the exported authority text and data contract for the existing fake-first hydrodynamic model.

Exact Microseconds saved:
Measured Unity profiler proof absent.
Runtime hot-path cost remains 0 us because this validation is offline.
Risk reduction: metadata, runtime contract text, and physical constants cannot drift independently from the baker.

## Entry - Verification Summary Gate

What was wrong:
`Submarine_Verification.json` could carry stale top-level artifact names or stale failure text while the artifact manifest itself remained correct.

What was done:
Verify-only now validates the top-level generated file-name fields and requires the generated `failures` list to be empty.
Added a regression test that corrupts `power_png` and inserts a stale failure entry, then verifies `--verify-only` catches both.
Regenerated `Data/Physics/*` and verified current artifacts serially.

Cinematic Cheats used:
No new simulation. This hardens the integrator-facing evidence file for the existing baked hydrodynamic data.

Exact Microseconds saved:
Measured Unity profiler proof absent.
Runtime hot-path cost remains 0 us because this validation is offline.
Risk reduction: the CTO/integrator status object cannot silently contradict the generated artifact set.

## Entry - Canonical Plot Payload Gate

What was wrong:
The SVG and PNG plot artifacts could be corrupted and rehashed while still satisfying header/dimension checks.

What was done:
Added canonical SVG and PNG byte builders in `Tools/SubmarinePhysicsSim.py`.
Changed plot writers to emit those canonical byte payloads.
Verify-only now compares existing SVG/PNG bytes against canonical payloads.
Added regression tests that corrupt SVG and PNG plot files, refresh their manifests, and require verify-only to fail.
Regenerated `Data/Physics/*` and verified current artifacts serially.

Cinematic Cheats used:
No new physical simulation. This protects the speed-vs-power evidence plot for the existing quadratic-drag/cubic-power model.

Exact Microseconds saved:
Measured Unity profiler proof absent.
Runtime hot-path cost remains 0 us because this validation is offline.
Risk reduction: graph artifacts cannot silently drift from the baked hydrodynamic evidence even if their hashes are refreshed.

Regression model:
CPU: no runtime code changed; future consumers must load constants once and use analytical drag.
GC: no Unity hot-path allocation introduced; JSON parsing in Tick/FixedTick is explicitly forbidden in export contract.
Memory: new data files are disk artifacts only; runtime NativeArray/struct import remains future work.
Cadence: future vehicle physics should run in fixed-step gather and route ForcePackets through PhysicsApplySystem.
Correctness: status remains code-review/offline verified only until Unity Play Mode and profiler captures are produced.

Status:
HYDRODYNAMICS DEFINED.
