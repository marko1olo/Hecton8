# Status_VEHICLE_AUTONOMOUS_DOCKING

Status: PENDING VERIFICATION
Agent: HYDRO_MECHANIC
Prompt ID: VEHICLE_AUTONOMOUS_DOCKING
Domain: ECHELON 6 HABITAT & VEHICLES / Drone Fleet Commander
Task count: 19
Started: 2026-05-13

## Mandates Loaded

- CORE_Submarine_Vehicles_Kinematics_AUP
- CORE_Weather_Abyssal_FlowField_Currents
- MATH_Coordinate_Precision_AUP_FloatingOrigin
- OPT_Zero_GC_Policy_AllocFree_Mandate
- OPT_Native_Memory_Collections_JobSystem_Protocol
- PHYS_Physics_Integrity_Determinism_ForceMode
- ARCH_Global_Registry_ServiceLocator_DI_Init
- DBG_Telemetry_Crash_Reporting_PostMortem
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First

## Setup Evidence

- [x] Prompt extracted cover-to-cover from `Docs/Tasks/CURRENT_BATCH.md`. DOD: CLI extraction by XML id, counted 19 task lines. Rejected: MCP/basic read because batch prompts can truncate or leak neighbor prompts. Estimate: 140 us.
- [x] Domain verified against `Docs/Actual Domains of Project.txt`. DOD: matched Drone Fleet Commander under ECHELON 6. Rejected: broad vehicle ownership because docking edits must stay in drone fleet/autonomous docking surface. Estimate: 90 us.
- [x] Architecture docs checked. DOD: read drone protocol, signal corridor, AUP integration, flow-field math, and doc audit. Rejected: scene-object docking implementation because live drones are native headless slots. Estimate: 420 us.

## Core Tasks

- [x] 1. Purge `DockingManager.Instance`. DOD: `rg` scan found no first-party `DockingManager.Instance`, so no singleton code remained to purge. Rejected: adding a replacement manager, because signal/native fleet ownership already exists. Estimate: 20 us.
- [x] 2. Drones consume `DockingRequestSignal` and emit `DockingCompleteSignal`. DOD: added unmanaged `DockingRequestSignal`, `DockingCompleteSignal`, and `DockingFailedSignal`; `DroneFleetManager` consumes request snapshots and publishes complete/fail through `SignalBus<T>`. Rejected: managed events and direct singleton callbacks. Estimate: 55 us per signal.
- [BLOCKED BY DEPENDENCY] 3. ASMDEF isolation: `Hecton8.Vehicles.Automation` -> Contracts. DOD: signal namespace and compile include were added, but proper asmdef isolation is blocked because `ISignal` lives in `Hecton8.Core` and `DroneFleetManager` is an internal monolithic Core owner. Rejected: circular Contracts->Core->Contracts assembly reference and broad internal/public API churn. Estimate: N/A dependency wall.
- [x] 4. Eradicate `Vector3.Slerp` or `MoveTowards` from drone movement scripts. DOD: `rg` against `Assets/_Project/Scripts/Construction` and `Assets/_Project/Scripts/Vehicles` found no remaining movement hits. Rejected: touching unrelated UI/third-party files. Estimate: 35 us.
- [x] 5. Define `BaseAirlock` entry points as AUP plus Forward vector. DOD: `RepairDroneHub` now exposes `DockAup`, `DockForward`, and optional `DockingAirlock` event target. Rejected: runtime transform parenting as the authoritative docking definition. Estimate: 45 us.
- [x] 6. Burst cubic Bezier control points P0/P1/P2/P3. DOD: `DroneCognitionJob.PrepareDockingSpline` writes P0, P1, P2, P3 into native `HeadlessDroneState`. Rejected: allocating a managed spline object per drone. Estimate: 0.35 us per docking drone.
- [x] 7. Burst Bernstein spline target and tangent evaluation. DOD: `EvaluateDockingBezier` uses Bernstein polynomials and derivative tangent with `float3` only. Rejected: Unity `Vector3`, animation curves, or `math.pow`. Estimate: 0.45 us per docking drone.
- [x] 8. Kinematic override while docking. DOD: `State == Docking` returns before task selection, boids, steering, and flow velocity movement; spline position is authoritative. Rejected: mixing steering with docking. Estimate: saved 2-5 us per docking drone.
- [x] 9. Cross-current visual yaw-slip only; trajectory remains spline-authoritative. DOD: `ResolveDockingVisualRotation` samples existing flow and biases only rotation into projected cross-current. Rejected: physical force/current advection during final approach. Estimate: 0.25 us when enabled.
- [x] 10. Cubic speed deceleration without managed math/pow overhead. DOD: speed uses `progress * progress * progress` and `math.lerp(MaxSpeed, 0.5f, t3)`. Rejected: `math.pow(t, 3)` per recursive prompt. Estimate: 0.05 us saved per docking drone.
- [x] 11. Hatch animation sync after `t > 0.8` via event command. DOD: Burst queues `DockingHatchOpen`; main thread maps it to the existing `BaseAirlockEvents.RaiseCycleStarted` lane for authored airlocks. Rejected: adding a second airlock event bus or calling Unity events from Burst. Estimate: 8 us main-thread rare event.
- [x] 12. Clamp at `t >= 1`, exact AUP snap, rigidbody kinematic policy, matrix-only visual attachment. DOD: native drone snaps exactly to `HomePosition`, clears velocity, enters Completed, and render matrix is removed without transform parenting. Rejected: Rigidbody solve; live drones have no Rigidbody bodies in the headless protocol. Estimate: removes solver cost entirely.
- [x] 13. Obstacle abort via raycast corridor; fail signal; AI loiter fallback. DOD: batched `RaycastCommand` probes segmented Bezier corridor points, ignores owning hub, aborts to `Wander`, increments abort telemetry, and emits `DockingFailedSignal`. Rejected: per-drone synchronous physics raycasts in the Burst job and a single P0->P3 chord that can miss curve-belly blockers. Estimate: amortized 3-20 us when docking rays exist.
- [x] 14. AUP shift safety for all spline control points. DOD: `DroneFleetOriginShiftJob` shifts P0-P3 with other native runtime positions. Rejected: recalculating curves from stale transforms. Estimate: 0.08 us per active drone during origin shift.
- [x] 15. Math LOD: Low tier ignores cross-current visual tilt. DOD: `CrossCurrentVisualSlipEnabled` is disabled for `Low`, `Mx350`, and `Unknown` tiers. Rejected: middle-ground always-on cosmetic math. Estimate: 0.25 us saved per docking drone on low silicon.
- [x] 16. Zero-GC spline math using native state only. DOD: spline fields live in `NativeArray<HeadlessDroneState>` and all spline evaluation is Burst `float3` math. Rejected: `List`, `AnimationCurve`, managed delegates, and transform hierarchy state. Estimate: 0 bytes/frame.
- [x] 17. Multi-drone batch evaluation in one `IJobParallelFor`. DOD: docking evaluation remains inside the existing `DroneCognitionJob.Schedule(HeadlessDroneCapacity, HeadlessDroneCapacity)` batch. Rejected: one job or update call per drone. Estimate: avoids managed fanout.
- [x] 18. Telemetry: write `DockingAborts` to blackbox. DOD: `DroneFleetBlackBoxEntry` now records `DockingAborts`; dump path is `Docs/AgentLogs/Dump_VEHICLE_AUTONOMOUS_DOCKING.bin`. Rejected: log-only counters outside the fixed 300-frame ring. Estimate: +4 bytes/frame.
- [BLOCKED BY DEPENDENCY] 19. Compile check: Burst Bezier math has no Unity `Vector3`. DOD: code scan confirms Bezier math uses `float3`; repeated `dotnet build Hecton8.Core.csproj` reports no errors in edited docking files after adding the new compile include. Latest loop-8 build remains blocked by unrelated missing project dependencies (`Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Core.Memory.Layout`, audio propagation, CCD, radar/resource contracts) with 0 docking-file matches. Unity validation also returned `no_unity_session`. Rejected: hiding dependency failures or modifying unrelated systems. Estimate: N/A dependency wall.

## Loop Log

### Loop 0 - Discovery

- Result: Existing live drone docking is in `Assets/_Project/Scripts/Construction/DroneCognitionJob.cs` and currently uses linear `math.lerp` to `HomePosition`.
- Result: No first-party `DockingManager` class or instance was found during scan.
- Result: No drone movement script currently uses `Vector3.Slerp` or `Vector3.MoveTowards`; verification will be repeated after edits.
- Result: Prompt re-extracted from `CURRENT_BATCH.md`, task count 19.

### Loop 1 - Tasks 1-5

- Implemented signal contracts, request consumption, complete/fail publish, and AUP/forward dock entry points.
- Blocked full asmdef extraction due Core-owned `ISignal` and internal drone manager ownership.

### Loop 2 - Tasks 6-10

- Replaced linear docking with native Bezier controls and Bernstein evaluation.
- Re-read `DroneCognitionJob` after patch; confirmed docking returns before normal AI steering.
- Rejected `math.pow`; retained explicit `t*t*t`.

### Loop 3 - Tasks 11-14

- Added hatch-open service command, docking clamp, RaycastCommand obstacle abort, and origin-shift control-point migration.
- Re-read `DroneFleetManager` service drain and completion order; complete signal is published before slot clearing.

### Loop 4 - Tasks 15-18

- Added low-tier visual-slip bypass, native zero-GC spline storage, existing single batch job integration, and `DockingAborts` blackbox field.
- Updated GPU `HeadlessDroneState` shader stride to match the C# native state extension.

### Loop 5 - Verification

- Re-extracted prompt checkpoints 3/6/9/12/15/18 via CLI. SHA256: `dd57b10e20bc7f32f9c7fe8014b9edfc2c9f6a3477157b5de7cd0d1e343ef02d`.
- `dotnet build Hecton8.Core.csproj` attempted twice. Attempt 1 found the new signal file missing from the generated project; fixed via `Hecton8.Core.csproj` compile include. Attempt 2 found no edited-file errors, but repo-wide dependency failures remain.
- Unity MCP `validate_script` attempted for `DroneCognitionJob.cs`; result: `no_unity_session`.
- `git diff --check` found no whitespace errors; Git warned only about LF->CRLF normalization on touched files.

### Loop 6 - Omega Polish

- Read `<POLISH_MANDATE id="OMEGA_POLISH">` only after all 19 tasks were done or dependency-blocked.
- Replaced docking path length `math.distance` trio with constants for fixed control legs plus `ApproximateDistanceNoSqrt` for the middle control gap. DOD: visual tempo approximation, no honest spline arc-length tax. Rejected: three square roots per docking spline refresh. Estimate: 0.15-0.40 us saved per docking spline refresh.
- Replaced cross-current yaw-slip `math.sqrt` with `ApproximateDistanceNoSqrt`. DOD: visual-only fake; spline trajectory remains exact. Rejected: physically honest current magnitude because slip is cosmetic. Estimate: 0.03-0.08 us saved per docking drone on enabled tiers.
- Replaced docking raycast `Vector3.magnitude` and `1f / length` with `lengthSq`, `math.rsqrt`, and multiplication. DOD: exact enough normalized ray direction without scalar division. Rejected: Unity vector magnitude property in the probe loop. Estimate: 0.02-0.06 us saved per active docking probe.
- Omega audit scan: no `math.pow`, `math.sqrt`, `math.distance`, `Vector3.Slerp`, `Vector3.MoveTowards`, `foreach`, `string.Format`, `.ToString()`, or `$"..."` in the docking patch surface. The only scanner hit was pre-existing `math.distancesq`, not a sqrt path.
- Omega `dotnet build Hecton8.Core.csproj --no-restore` log: `Temp/VEHICLE_AUTONOMOUS_DOCKING_omega_build.log`; result remains red from global dependency walls, 0 matches for docking files. Status stays PENDING VERIFICATION.

### Loop 7 - Patient Recheck / Upgrade

- Re-read docking code and found same-frame hatch loss: a drone could jump from `t < 0.8` to completed and clear its slot before the service queue published hatch open. Fixed with `DockingFlagHatchOpenPublished` and idempotent main-thread `PublishPendingDockingHatchOpen`. DOD: completion path publishes pending hatch before complete signal; service queue path marks published to prevent duplicates. Rejected: reordering all service drains, because repair/attack service timing was unrelated. Estimate: 0.01 us flag check per completed/drained docking command.
- Re-read docking request consumer and found request correlation loss on invalid requests. Fixed missing-drone, invalid-state, wrong-hub, and invalid-AUP failures to publish the incoming `RequestId`. DOD: producers can correlate failed requests without reading stale drone state. Rejected: mutating drone state with bad request IDs. Estimate: no hot-path cost beyond invalid request handling.
- Re-read yaw-slip and restricted current compensation to horizontal yaw-plane contribution. DOD: current slip no longer introduces pitch drift; spline tangent remains authoritative. Rejected: full 3D visual drift because prompt specified yaw-slip. Estimate: one dot and subtract only on enabled tiers.
- Replaced single P0->P3 obstacle chord with segmented Bezier corridor probes. DOD: Low/Mid use 2 segments, High/Ultra use 3; capacity raised to 192 persistent ray commands. Rejected: exact continuous sweep or per-drone managed physics casts. Estimate: +1 ray per active docking drone on Low/Mid, +2 on High/Ultra, still zero GC and batched.
- Verification: `rg` scan found no forbidden docking patch surface hits for `math.pow`, `math.sqrt`, `math.distance(`, `Vector3.Slerp`, `Vector3.MoveTowards`, managed `foreach`, `string.Format`, `.ToString()`, or interpolated strings. Broader division scan only hit unrelated/pre-existing code lines.
- Build: `dotnet build Hecton8.Core.csproj --no-restore -verbosity:minimal -maxcpucount:1 -nodeReuse:false` wrote `Temp/VEHICLE_AUTONOMOUS_DOCKING_segmented_build.log`; still red with 114 unrelated errors and 0 matches for docking files. Unity MCP `validate_script` for `DroneCognitionJob.cs` and `DroneFleetManager.cs` returned `no_unity_session`.

### Loop 8 - Patient Recheck / Edge-Case Upgrade

- Re-extracted `<AGENT_PROMPT id="VEHICLE_AUTONOMOUS_DOCKING">` by CLI lines 312-354. DOD: task count remains 19 and status must remain `PENDING VERIFICATION`. Rejected: relying on memory after repeated edits. Estimate: 80 us.
- Re-read segmented obstacle result handling and found duplicate failure risk: multiple ray segments for one drone could all hit in the same batch, incrementing abort telemetry and emitting multiple `DockingFailedSignal`s. Fixed by re-checking `State == Docking` before aborting a hit result. DOD: first hit mutates the state to `Wander`; later same-slot hits are ignored. Rejected: allocating a per-frame dedupe set. Estimate: 0.02 us per hit result.
- Re-read completion signal publication and found it could report the current hub socket instead of the exact native snap target. Fixed `DockingCompleteSignal.DockAup` to use finite `drone.HomePosition` fallbacking to `drone.Position`, and `DockForward` to use `drone.HomeRotation`. DOD: complete signal now reflects the completed native spline endpoint after AUP shifts. Rejected: mutable hub transform read on completion. Estimate: removes one hub lookup/branch path on rare completion.
- Re-ran forbidden API scan. DOD: no hits for `math.pow`, `math.sqrt`, `math.distance(`, `Vector3.Slerp`, `Vector3.MoveTowards`, managed `foreach`, `string.Format`, `.ToString()`, or interpolated strings in the docking patch surface. Rejected: broad repo scan as primary evidence because unrelated systems are dirty. Estimate: 35 us.
- Re-ran `git diff --check`; result: no whitespace errors, only LF-to-CRLF warnings on touched files. Estimate: N/A.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore -verbosity:minimal -maxcpucount:1 -nodeReuse:false`; log: `Temp/VEHICLE_AUTONOMOUS_DOCKING_loop8_build.log`. Result: `EXIT=1`, raw `: error` line count 228 from unrelated global dependency wall, 0 matches for docking files. Unity MCP `validate_script` and `read_console` still returned `no_unity_session`.
