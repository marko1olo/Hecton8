# Status - HYDRODYNAMIC_DRAG_MATRIX_BAKER

Prompt ID: HYDRODYNAMIC_DRAG_MATRIX_BAKER
Role: AEROSPACE_ENGINEER
Domain: ECHELON 4 / Hydrodynamic Drag & Buoyancy
Status: HYDRODYNAMICS DEFINED

## Mandates Loaded

- CORE_Submarine_Vehicles_Kinematics_AUP.txt - vehicle mass/inertia and platform kinematics boundary.
- PHYS_Physics_Integrity_Determinism_ForceMode.txt - analytical drag, force packet doctrine, finite-safe math.
- CORE_Weather_Abyssal_FlowField_Currents.txt - currents affect only gameplay-critical vehicles and VFX cues.
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt - cavitation/noise as deterministic presentation fake unless gameplay-critical.
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt - exported data must be suitable for hot-path zero-GC consumers.
- MATH_Rsqrt_i3_SIMD.txt - runtime consumers should prefer squared magnitude, rsqrt, rcp forms.
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt - MX350 frame budget and load-shed constraints.
- DBG_Telemetry_Crash_Reporting_PostMortem.txt - future runtime hydro system needs 300-frame black box.

## Checklist

- [x] Task 1 - Drag/Lift coefficient calculator | DOD: 5 hull profiles expose Cd/Cl and CdA diagonal tensors in `Data/Physics/Submarine_Specs.json` | Rejected: runtime hull-mesh sampling and inspector-only drag tuning | Estimate: constant lookup instead of runtime coefficient fit, model estimate 8-20 us saved per vehicle fixed-step, measured proof absent
- [x] Task 2 - Added mass terms | DOD: local XYZ added-mass and effective-mass tensors exported per hull using displaced seawater coupling | Rejected: full 6x6 coupled hydrodynamic solve because gameplay only needs stable surge/sway/heave response now | Estimate: diagonal tensor multiply instead of matrix solve, model estimate 3-8 us saved per vehicle fixed-step, measured proof absent
- [x] Task 3 - Cavitation thresholds | DOD: sigma-based onset table exported by depth for each prop profile | Rejected: bubble particle physics and per-bubble acoustic simulation; use threshold-driven VFX/audio state | Estimate: event threshold vs bubble sim, model estimate >100 us saved during cavitation events, measured proof absent
- [x] Task 4 - Torque tensors | DOD: roll/pitch/yaw angular quadratic damping and added angular inertia tensors exported | Rejected: Unity single `angularDrag` scalar because it cannot differentiate roll, pitch, and yaw water resistance | Estimate: 3-axis tensor constants, model estimate 2-5 us saved per vehicle fixed-step vs dynamic shape solve, measured proof absent
- [x] Task 5 - Hydro simulator and plotter | DOD: `Tools/SubmarinePhysicsSim.py` writes JSON, CSV, SVG plot, and verification summary | Rejected: manual spreadsheet and Unity Play Mode tuning loop | Estimate: 0 runtime cost; offline bake avoids hot-path allocations entirely
- [x] Task 6 - Self-audit 50 m/s acceleration gate | DOD: simulator records terminal speed, 90 s acceleration, and `time_to_50_mps_seconds` per hull | Rejected: subjective "feels slow" claim without numeric gate | Estimate: no runtime cost; prevents coefficient regression before Unity integration
- [x] Task 7 - Data export JSON | DOD: `Data/Physics/Submarine_Specs.json` generated with status `HYDRODYNAMICS DEFINED`, 5 hulls, tensors, thresholds, runtime contract, and verification data | Rejected: chat-only table and spreadsheet-only bake | Estimate: loader can bulk import constants once; hot path JSON parsing forbidden
- [x] Task 8 - Expensive Weight rationale | DOD: rationale recorded in `Rationale_HYDRODYNAMIC_DRAG_MATRIX_BAKER.md` and exported `expensive_weight_feel` section | Rejected: pure top-speed tuning because it ignores acceleration, stop distance, added mass, and cavitation | Estimate: design/physics constraint avoids refactor churn; measured runtime savings not applicable
- [x] Polish mandate - `<POLISH_MANDATE>` tag absent from `Docs/Tasks/CURRENT_BATCH.md`; local anti-bloat inquisition executed after core tasks.

## Verification Log

- Initial prompt extracted from Docs/Tasks/CURRENT_BATCH.md with CLI.
- No previous status/rationale file existed for this ID at session start.
- Python 3.14.0 executed `Tools/SubmarinePhysicsSim.py --out-dir Data/Physics`.
- Generated `Data/Physics/Submarine_Specs.json`, `Submarine_SpeedPower.csv`, `Submarine_SpeedPower.svg`, and `Submarine_Verification.json`.
- Verification summary returned `HYDRODYNAMICS DEFINED` with zero failures for Tasks 1-5.
- Self-audit values: Sleek 19.23782 m/s, Industrial 12.068155 m/s, Boxy 8.929206 m/s, Alien 18.465898 m/s, Armored Crawler 9.907589 m/s terminal speed; no hull reaches 50 m/s in 90 s.
- Stop-distance ratios: Sleek 24.088482x, Industrial 14.062744x, Boxy 8.30792x, Alien 23.520304x, Armored Crawler 11.029128x; all exceed 3x hull length.
- Python compile check passed: `python -m py_compile Tools/SubmarinePhysicsSim.py`.
- Custom JSON/CSV validator passed: 5 hulls, monotonic power curves, `HYDRODYNAMICS DEFINED`.
- Polish lookup: `CURRENT_BATCH.md` has no `<POLISH_MANDATE>` tag. Searched `POLISH|Polish|polish|MANDATE`.
- Anti-bloat loop removed unused `Iterable` import from `Tools/SubmarinePhysicsSim.py`.
- Determinism check passed: repeated baker run produced stable SHA256 hashes for JSON, CSV, SVG, and verification JSON.
- Random/time/network/process scan passed: no `datetime`, `random`, `time.`, `uuid`, `requests`, `urllib`, `subprocess`, or `os.system` usage in the baker.
- Build-scope note: no root `.sln` or `.csproj` found by `rg --files -g '*.sln' -g '*.csproj'`; C# compilation not applicable because no C# files changed.
- Added persistent regression tests in `Tools/test_submarine_physics_sim.py`.
- Test run passed clean with ResourceWarnings elevated to errors: `python -W error::ResourceWarning -m unittest Tools/test_submarine_physics_sim.py`.
- Python compile check passed for both baker and test: `python -m py_compile Tools/SubmarinePhysicsSim.py Tools/test_submarine_physics_sim.py`.
- Improved verification JSON determinism by writing stable artifact file names instead of output-directory-dependent paths.
- Replaced optional matplotlib PNG generation with deterministic built-in PNG writer.
- Generated `Data/Physics/Submarine_SpeedPower.png` as a self-contained 1200x720 RGB plot; PNG header verified: magic `89504E470D0A1A0A`, IHDR, width 1200, height 720, bit depth 8, color type 2.
- Regression tests now pass 6 tests: `python -W error::ResourceWarning -m unittest Tools/test_submarine_physics_sim.py`.
- Determinism check now includes PNG: stable SHA256 `BC7A8882B8A3F861648E883677BCF30FFA516CC384413E2B822A315BE6D886D7`.
- `Data/Physics/Submarine_Verification.json` now contains bytes and SHA256 for specs, CSV, SVG, and PNG artifacts.
- Regression tests now pass 7 tests after artifact-manifest coverage: `python -W error::ResourceWarning -m unittest Tools/test_submarine_physics_sim.py`.
- Current verification artifact SHA256: `D3C5B39E991BD64E0BB4113B11BC39A43F1875F63AF363854B9EFF2DEE255A67`.
- Added `--verify-only` CLI mode to validate existing hydro artifacts without rewriting files.
- Regression tests now pass 9 tests after verify-only and same-size corruption coverage: `python -W error::ResourceWarning -m unittest Tools/test_submarine_physics_sim.py`.
- Verify-only command passed on actual artifacts: `python Tools/SubmarinePhysicsSim.py --out-dir Data/Physics --verify-only`.

## Iteration Loops

1. Loop 1 - Implemented initial baker and ran `python Tools/SubmarinePhysicsSim.py --out-dir Data/Physics`; output status passed.
2. Loop 2 - Parsed generated JSON and checked terminal speeds, 50 m/s gate, and stop-distance ratios; output passed.
3. Loop 3 - Ran `python -m py_compile` and custom JSON/CSV monotonic power validator; output passed.
4. Loop 4 - Re-read script sections, found missing explicit exported weight rationale, added `expensive_weight_feel`, reran baker; output passed.
5. Loop 5 - Re-read imports/tail/export code, removed unused import, ran deterministic hash/no-random scan and validator; output passed.
6. Loop 6 - Added permanent unittest coverage for output contract, hydro gates, tensor diagonality, cavitation monotonicity, power monotonicity, and byte determinism; output passed.
7. Loop 7 - Removed optional plotting dependency risk by adding deterministic pure-Python PNG output and PNG header tests; output passed.
8. Loop 8 - Added artifact byte/SHA256 manifest to verification JSON and tests that manifest hashes match payloads; output passed.
9. Loop 9 - Added verify-only CLI validation and same-size corruption detection; output passed.
