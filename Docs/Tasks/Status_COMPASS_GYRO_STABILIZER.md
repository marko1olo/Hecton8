# Status_COMPASS_GYRO_STABILIZER

Prompt: `COMPASS_GYRO_STABILIZER`
Domain: `UX/NAVIGATION`
Code domain: `Assets/_Project/Scripts/UI/Navigation/`
Status: IMPLEMENTED CODE; PREFAB/SCENE BINDING NOT SERIALIZED; FINAL BUILD BLOCKED BY EXTERNAL DEPENDENCIES

Latest inquisition pass: 2026-05-16, loop 8

Relevant mandates read before coding:
- `UI_Diegetic_Physical_Interfaces.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `MATH_AUP_Determinism_Sync.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Signal_Lane_Segregation.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Checklist
- [x] 1. PURGE_SINGLETONS - Removed runtime installer path for the old screen ribbon and confirmed no `CompassUI.Instance` in touched compass path. DOD: source scan plus no singleton in new runtime. Rejected: static owner singleton. Estimate: 4 us.
- [x] 2. DEBT_CLEANUP - Removed camera-euler heading from `ShaderCompassRibbon`; fallback now consumes `IInertialNavigationService` and refuses non-world-space Canvas. DOD: runtime uses player AUP pose/service heading, not `Camera.main`/camera eulers. Rejected: camera yaw ribbon. Estimate: 7 us.
- [x] 3. DATA_EVICTION - Added vault-owned `CompassStateDTO`, `CompassHeadingOutput`, and `CompassBlackBox` buffers. DOD: `GlobalDataVault` buffers own state/output/blackbox. Rejected: MonoBehaviour field authority. Estimate: 12 us.
- [x] 4. BURST_ALGORITHM - Added `GyroDriftJob` integrating catch-up, noise, anomaly spin, finite fallback, and output slots. DOD: Burst job integrates heading drift with noise and anomaly scalar. Rejected: managed per-frame compass math. Estimate: 22 us.
- [x] 5. AUP_INTEGRITY - Heading resolves from `IPlayerRuntimeContext.TryGetPlayerPoseSnapshot()` forward vector against global +Z and stores AUP as `double3`. DOD: AUP-safe player pose, not transform euler authority. Rejected: transform euler authority. Estimate: 5 us.
- [x] 6. DOD_SOA_LAYOUT - Output writes to vault `NativeArray<float>` slots via `CompassOutputSlot`. DOD: current/actual/drift/anomaly/power/glitch/cardinal/max-drift are SOA floats. Rejected: managed DTO polling only. Estimate: 3 us.
- [x] 7. SIGNAL_FLOW - `AnomalyProximitySignal` is consumed through typed `SignalBus<T>.GetFrameSnapshot()`. DOD: no delegates, no strings, no direct dependency on anomaly producers. Rejected: monolithic string event. Estimate: 6 us.
- [x] 8. LOW_TIER_FAKE - Diegetic TMP cardinal label uses fixed `char[2]` plus `SetCharArray`. DOD: no TMP `.text` or `SetText` in compass path. Rejected: screen Canvas text rebuild. Estimate: 3 us.
- [x] 9. HIGH_END_OVERKILL - High tier can draw a physical dial mesh through preallocated `ComputeBuffer` args and `Graphics.DrawMeshInstancedIndirect`. DOD: no runtime dial clones. Rejected: spawning/cloning dial objects. Estimate: 18 us.
- [x] 10. REACTIVE_VFX - Anomaly over 0.8 adds wild-spin heading offset, `_CompassGlassChromatic` shader scalar, `_CompassOverkill01`, and optional High/Ultra-only local failure particles. DOD: tier-gated cinematic fake, not magnetometer simulation. Rejected: mobile particle spam and physical gyro simulation. Estimate: 6 us low, up to 20 us spent on High/Ultra visuals when bound.
- [x] 11. STP_STABILIZATION - N/A. DOD: documented no STP authority path and no motion-vector edits. Rejected: unrelated upscaler mutation. Estimate: 0 us.
- [x] 12. NAN_VACCINATION - Headings normalize through `math.fmod(Heading, 360f)` with finite fallback. DOD: job and presentation guard non-finite inputs. Rejected: blind angle accumulation. Estimate: 4 us.
- [x] 13. BLACKBOX_LOGGING - Fixed 300-entry vault ring logs `MaxGyroDriftDegrees`; NaN path dumps to `Docs/AgentLogs/Dump_COMPASS_GYRO_STABILIZER.bin`. DOD: no `Debug.Log` telemetry dependency. Rejected: ephemeral console-only evidence. Estimate: 8 us.
- [x] 14. TRIPLE_STRIKE_REPAIR - [BLOCKED BY DEPENDENCY] Build strikes completed; compass-side signal placement and layout defects were fixed, then build remained blocked by non-compass compile walls. DOD: errors read and repaired where owned. Rejected: editing input/core bootstrap/progression walls outside compass authority. Estimate: external.
- [x] 15. HOMEOSTASIS_ADAPTATION - Drift runs on SlowTick when low tier, CPU stress exceeds 0.8, or power is dead. DOD: `SystemHealthSignal` gates fast cadence. Rejected: fixed 60Hz on stressed hardware. Estimate: 5 us.
- [x] 16. CALIBRATION - `CompassCalibratedSignal` and `RequestRecalibration()` reset current heading to actual during the next job. DOD: typed lane plus service entry point. Rejected: direct base beacon lookup. Estimate: 5 us.
- [x] 17. POWER_TIE_IN - `SurvivalVitalsChangedSignal.Energy01` below 1% powers down the compass and displays `--`. DOD: no per-frame registry power polling. Rejected: independent compass battery model. Estimate: 4 us.
- [x] 18. FINAL_VALIDATION - [BLOCKED BY DEPENDENCY] `dotnet build` remains red in unrelated domains after compass-owned repair. DOD: latest build failures recorded below. Rejected: false green report. Estimate: external.

## Iterative Loops
- [x] Loop 1: tasks 1-5 + static verification. `dotnet build Hecton8.Core.csproj --no-restore` failed on pre-existing dependency wall: missing `ProceduralLadderClimbRuntime`, `ItemData`, `OrganicDebrisProfile`; no compass compile error was reached.
- [x] Loop 2: tasks 6-10 + static verification. Hazard scan found no `CompassUI.Instance`, `Camera.main`, camera eulers, TMP `.text`, `SetText`, `Time.frameCount`, `StartCoroutine`, managed formatting, or screen-space Canvas creation in touched compass files. Build strike 2 failed in `FaunaKinematicsRuntime`; `Assembly-CSharp.csproj` also failed because `Temp/obj/Assembly-CSharp/project.assets.json` is missing.
- [x] Loop 3: tasks 11-14 + compile repair. Build strike 3 exposed compass signal structs missing from core signal lane ownership; fixed by moving `AnomalyProximitySignal` and `CompassCalibratedSignal` into `GlobalSignals.cs`. Rebuild then failed only on external walls: `Hecton8.VFX.Wakes`, `IDockingAutopilotService`, `ActiveSplineData`, and ecosystem service method mismatches.
- [x] Loop 4: tasks 15-18 + dependency-wall classification. Verified homeostasis/power/calibration paths and marked final build blocked instead of editing docking, flora, fauna, or ecosystem ownership.
- [x] Loop 5: self-inquisition / omega polish. Re-read runtime, legacy ribbon, signal placement, and scans. Result: compass implementation is internally consistent, but status cannot be upgraded to `VERIFIED MASTER GRADE` until the external compile wall is cleared.
- [x] Loop 6: multiplatform/data sovereignty pass. Removed persistent `NativeArray` fields from `DiegeticGyroCompassRuntime`; all compass buffers are now transient views fetched from `GlobalDataVault`. Changed compass DTO/snapshot/blackbox structs to `Pack = 1`. Low tier now uses triangle noise instead of coherent noise; high tier uses richer two-octave drift plus `_CompassOverkill01`. Indirect dial draw is guarded against GLES/no-compute/no-instancing targets.
- [x] Loop 7: post-inquisition verification. Domain hazard scan found no private `NativeArray`, standard `Update`, `LateUpdate`, `FixedUpdate`, managed formatting, TMP `.text`, `SetText`, camera polling, EventBus, managed delegates, `GameObject.Find`, coroutine, or direct `H8Memory.Allocate` in navigation domain. `dotnet build Hecton8.Core.csproj --no-restore -m:1 /v:minimal` remains blocked by external `Hecton8.Core.Bucketing` / `ModuloSimulationBucketer` errors in `GameBootstrapper`. `dotnet build Assembly-CSharp.csproj -m:1 /v:minimal` timed out after 124 seconds.
- [x] Loop 8: titanium inquisition pass. Re-read XML and mandates from disk. Made compass layout sizes explicit: blackbox 40 bytes, `CompassStateDTO` 136 bytes, `InertialNavigationSnapshot` 120 bytes; core compass signals remain 80/32 bytes. Removed the SlowTick dependency-poll fallback; tick paths now use cached dependencies or return. Added cold `InjectDependencies(...)` and `ConfigurePhysicalBinding(...)` APIs so the runtime can be bootstrapped and mapped to a physical tool without screen-space UI. Added optional High/Ultra-only anomaly particle emission for compass glass failure, gated by quality tier and system stress with a 128-particle hard cap. GUID scan found no serialized prefab/scene reference to `DiegeticGyroCompassRuntime`, so Unity scene wiring remains unverified and is not claimed. Latest `dotnet build Hecton8.Core.csproj --no-restore -m:1 /v:minimal` stops on external `EcosystemRuntimeInstaller.cs` / `SubmarineFluidDynamics.cs` errors for missing `Hecton8.AI.Ecosystem` and `VaultNativeBuffer<>`. `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /v:minimal` stops on missing `Temp/obj/Assembly-CSharp/project.assets.json`.
