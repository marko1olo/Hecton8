## 2026-05-25 AUTOFIX2

What was wrong:
- 28 runtime/dev smoke, UI fallback, visual validation, and scatter diagnostic files still called `UnityEngine.Debug.Log*` directly.
- These were diagnostic surfaces, not gameplay truth routes, and should use the project-owned compile-stripped facade.

What was done:
- Converted direct `Debug.LogWarning`, `Debug.LogError`, and `Debug.LogException` to `Hecton8.Core.H8Debug` in:
  `AudioLog/AudioLogPickup.cs`, `BarterRuntimeSmokeTester.cs`, `BaseStressRuntimeSmokeTester.cs`,
  `Dev/CelestialCataclysmSmokeTester.cs`, `Dev/ShellVerificationRuntimeSmokeTester.cs`,
  `FabricationRuntimeSmokeTester.cs`, `FaunaRuntimeSmokeTester.cs`, `FluidIncursionSmokeTester.cs`,
  `OmegaSurvivalKinematicsSmokeTester.cs`, `SaveSystemRuntimeSmokeTester.cs`, `ScanRuntimeSmokeTester.cs`,
  `SurvivalKinematicsSmokeTester.cs`, `ThermalMeltSmokeTester.cs`, `ThermalSurvivalSmokeTester.cs`,
  `ToolTrialRangeRuntimeSmokeTester.cs`, `UI/PauseControlsPanel.cs`, `UI/RelayHUDRuntimeBootstrap.cs`,
  `UI/SettingsComparisonView.cs`, `UI/VR/OpenXRManualOverrideLever.cs`,
  `UI/WristHologramHudRuntime_PdaScreenProjector.cs`, `UIRuntimeSmokeTester.cs`,
  `VFX/CameraJuiceSystem_CameraJuiceBurst.cs`, `VisualBudgetSmokeTester.cs`, `VisualCascadeSmokeTester.cs`,
  `VoxelDeformationSmokeTester.cs`, `World/ScatterEvaluator.cs`, `World/ScatterRuntimeBackendFacade.cs`,
  `WorldGenerativeGeologyRuntimeSmokeTester.cs`.

Cinematic cheats used:
- No simulation added. Diagnostic visibility stays in editor/development builds through `H8Debug`; release-player log noise is stripped instead of simulated/observed at runtime.

Proof artifacts:
- Identifier-bound scoped `rg` for direct `Debug.Log*` in touched files returned no matches.
- `git diff --check` exit 0; only LF/CRLF normalization warnings reported.
- Build gate: no dotnet/csc process listed; CPU average was 52.1%, so AGENTS.md forbids launching build.

Exact microseconds saved:
- Measured: PENDING VERIFICATION because build/profiler run was blocked by CPU gate.
- Static estimate: 0 us steady-frame in normal gameplay; savings are avoided release-player diagnostic call/string surface on the converted cold/dev/fallback paths.
