# Status 1746 - Camera Shake And Impact Kinetics Coordinator

Status: IMPLEMENTED_STATIC_VERIFIED_RUNTIME_PENDING

Scope:
- Updated first-party `CameraJuiceSystem` / `CameraJuiceSignals` route.
- Did not add Cinemachine; first-party Cinemachine impulse route is absent in this project state.

Completed:
- Added ABI-stable profile, amplitude, priority, radius, translation, and rotation fields to `CameraJuiceImpactSignal` reserved bytes.
- Added profile/priority publish overloads in `CameraJuiceSignals`.
- Added priority-band camera impact admission in `EvaluateCameraTraumaJob` under the existing 32-record cap.
- Wired camera impact profile lookup through DataVault `CameraTraumaProfileDTO` rows with named fallback profiles.
- Updated `camera_trauma_profiles.csv` to the four named profile rows consumed by signal profile hashes.
- Added impact-driven FOV punch from accepted native impulse results.
- Scaled shake and FOV presentation by cached `SettingsManager.UiMotionScale`; no new settings contract was invented.

Verification:
- `validate_script` passed: `Assets/_Project/Scripts/Core/CameraJuiceSignals.cs`.
- `validate_script` passed: `Assets/_Project/Scripts/VFX/CameraJuiceSystem_CameraJuiceBurst.cs`.
- Unity console read: 0 current error entries.
- `git diff --check` passed for touched files except line-ending warnings.
- `dotnet build` not run: CPU was 100% and a `dotnet` process was already active, which violates project build rules.

Pending:
- Runtime play-mode validation of impulse feel, FOV punch comfort, and 0 B/frame GC.
- Profiler proof for the priority pass under dense impact snapshots.
- Full compile when CPU/build guard permits.
