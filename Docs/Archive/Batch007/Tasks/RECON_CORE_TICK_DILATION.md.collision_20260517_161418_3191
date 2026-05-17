# CORE_TICK_DILATION Recon

Command:
`rg -n "Time\.timeScale|Time\.deltaTime|Time\.fixedDeltaTime|Task\.Delay|WaitForSeconds" Assets/_Project/Scripts -g "*.cs"`

Findings:
- `Assets/_Project/Scripts/Core/BootstrapContracts/BootstrapStatus.cs:248` was a real offender: `Time.timeScale = 0f` during safe halt. Fixed to `1f`; scripted physics halt remains.
- `Assets/_Project/Scripts/Core/BootstrapContracts/BootstrapStatus.cs:114,154,211` set `Time.timeScale = 1f`. Kept. These lines enforce neutral Unity global time.
- `Assets/_Project/Scripts/SubmarineFluidDynamics.cs:4281` reads `Time.fixedDeltaTime`. Out of CORE/SCHEDULING domain; flag for physics owner to route through dispatcher/fixed accumulator.
- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs:3230` reads `Time.fixedDeltaTime`. Out of CORE/SCHEDULING domain; flag for fauna owner to route through dispatcher dt.
- `Assets/_Project/Scripts/Dev/CelestialTimeLapseDebugger.cs:30` exposes `Time.fixedDeltaTime` for debug readback. Non-runtime critical.
- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs:3935,4202,4233`, `BaseStressRuntimeSmokeTester.cs:83`, `Atmosphere/BaseAtmosphereEngine.cs:243`, and `HectonPlayerSpawner.cs:381,479` use `Awaitable.WaitForSecondsAsync`. These are bootstrap/test/polling waits, not custom dilated gameplay waits; no CORE patch made.
- `Assets/_Project/Scripts/GameTickManager.cs` contains cached `WaitForSeconds` comments. Legacy comments only in this scan.
- Gameplay `Time.deltaTime` hits are XML/comment documentation naming the deltaTime parameter, not executable reads.

Task 14 conclusion:
Custom gameplay delay path is `AwaitableExtension.DelayDilated`, and no `Task.Delay` hits were found under `Assets/_Project/Scripts`.
