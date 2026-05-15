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
Each hull has Cd/Cl values, local XYZ CdA drag tensor, reverse axial CdA, square-drag acceleration tensor, added-mass tensor, effective-mass tensor, rigid/added angular inertia tensors, angular quadratic damping torque tensor, cavitation thresholds by depth, acceleration gate, and stop-distance check.

Verification:
`python Tools/SubmarinePhysicsSim.py --out-dir Data/Physics` returned `HYDRODYNAMICS DEFINED`.
`python -m py_compile Tools/SubmarinePhysicsSim.py` passed.
Custom JSON/CSV/PNG validator passed: 5 hulls, monotonic power curves, valid PNG header, no 50 m/s reach within 90 seconds, stop distance >= 3 hull lengths.
Deterministic rerun hashes were stable for JSON, CSV, SVG, PNG, and verification JSON.
PNG header verified: magic `89504E470D0A1A0A`, IHDR, 1200x720, 8-bit RGB.
`Data/Physics/Submarine_Verification.json` includes bytes and SHA256 for specs, CSV, SVG, and PNG artifacts.
`python -W error::ResourceWarning -m unittest Tools/test_submarine_physics_sim.py` passed 9 tests.
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

Regression model:
CPU: no runtime code changed; future consumers must load constants once and use analytical drag.
GC: no Unity hot-path allocation introduced; JSON parsing in Tick/FixedTick is explicitly forbidden in export contract.
Memory: new data files are disk artifacts only; runtime NativeArray/struct import remains future work.
Cadence: future vehicle physics should run in fixed-step gather and route ForcePackets through PhysicsApplySystem.
Correctness: status remains code-review/offline verified only until Unity Play Mode and profiler captures are produced.

Status:
HYDRODYNAMICS DEFINED.
