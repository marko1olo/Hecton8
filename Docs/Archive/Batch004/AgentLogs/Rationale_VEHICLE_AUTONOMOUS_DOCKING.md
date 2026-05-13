# Rationale_VEHICLE_AUTONOMOUS_DOCKING

Status: PENDING VERIFICATION
Agent: HYDRO_MECHANIC
Prompt ID: VEHICLE_AUTONOMOUS_DOCKING

## Decision 001 - Patch Native Headless Docking Path

Problem: Existing drone docking is a linear native-slot interpolation inside `DroneCognitionJob`, while the assignment requires autonomous Bezier spline docking without `DockingManager.Instance` or per-drone GameObject control.
Solution: Keep the docking implementation inside the existing headless fleet `IJobParallelFor`, add native Bezier control state to each drone slot, and route external requests/completion/failure through unmanaged signal contracts.
Rejected Alternatives: A MonoBehaviour docking manager, Unity transform parenting, or Rigidbody-driven docking would violate the existing drone fleet protocol, add direct dependencies, and create unpredictable current/physics coupling.
Scalability potential: Low disables cross-current yaw slip and uses pure spline orientation; Middle adds cheap visual slip; High/Ultra can increase visual slip intensity or hatch timing polish while the trajectory remains deterministic.
Hardware Impact: On i3/MX350-class hardware, one batched Burst job and native slot fields avoid managed update fanout and keep docking below the suspicious 0.1 ms budget target for the current 64-slot fleet.

## Decision 002 - Visual Current Compensation Instead Of Physical Force

Problem: Abyssal currents can make docking look disconnected, but applying physical current forces during a precision dock would move drones off the spline and destabilize arrival.
Solution: Sample the existing abyssal flow snapshot inside the Burst job, project cross-current against the spline tangent, and bias only visual rotation/yaw slip into that current while the position remains on the Bezier curve.
Rejected Alternatives: ForceMode-based current advection, Rigidbody drag, or resampling a GPU flow texture on CPU were rejected because docking needs predictability over realism and the project already exposes a CPU analytical current snapshot.
Scalability potential: Low uses no slip; Middle uses capped yaw slip; High/Ultra can raise visual bias without changing the deterministic spline.
Hardware Impact: The chosen path is a few dot products and normalizations per active docking drone, not physics solver work; expected low-end gain is avoiding broadphase/solver churn and avoiding transform hierarchy changes.

## Decision 003 - Signal Contracts Without Singleton Ownership

Problem: The prompt requires request/complete/fail docking signals, but the existing signal corridor requires unmanaged `ISignal` payloads and the drone fleet owner lives in `Hecton8.Core`.
Solution: Added `DockingRequestSignal`, `DockingCompleteSignal`, and `DockingFailedSignal` as blittable contracts in `Hecton8.Vehicles.Automation`, then consumed/published them through `SignalBus<T>` from `DroneFleetManager`.
Rejected Alternatives: `DockingManager.Instance`, C# managed events, or direct hub references from producers were rejected because they couple agents and break parallel ownership.
Scalability potential: Low/Middle/High/Ultra use the same small payloads; high-end devices can add more docking cosmetics without changing the contract.
Hardware Impact: NativeQueue-backed signals avoid per-frame allocations and keep request/complete routing off managed event multicast paths.

## Decision 004 - Do Not Force ASMDEF Split Through Core Internals

Problem: Full `Hecton8.Vehicles.Automation -> Contracts` asmdef isolation is not directly possible because `ISignal` is defined in `Hecton8.Core` and drone fleet types are internal to the Core assembly.
Solution: Keep the runtime patch in Core, add the contract namespace and project compile include, and mark true asmdef extraction blocked until `ISignal` or signal contracts are moved into a lower-level contracts assembly.
Rejected Alternatives: Creating a circular `Contracts -> Core -> Contracts` reference, or making the drone fleet public across assemblies, was rejected as build-risky architecture churn outside this task.
Scalability potential: Once signal base types are in a lower assembly, this code can be moved with no runtime behavior change.
Hardware Impact: No runtime cost; the decision avoids compile churn and protects frame-time work from assembly surgery.

## Decision 005 - RaycastCommand Corridor Abort On Main Owner Thread

Problem: Obstacle aborts require physics raycasts, but `RaycastCommand` results cannot be synchronously queried from inside the Burst drone movement job without a separate scheduling barrier.
Solution: The fleet manager builds a fixed native batch of P0->P3 docking corridor `RaycastCommand`s before the cognition job, completes the batch, aborts blocked drones to `Wander`, and emits `DockingFailedSignal`.
Rejected Alternatives: Calling `Physics.Raycast` per drone, running physics from inside the movement job, or ignoring obstacles were rejected for determinism and cost reasons.
Scalability potential: Low has only active docking probes; High/Ultra can increase probe density later if visual overkill needs it.
Hardware Impact: On i3/MX350, command batching avoids per-drone managed raycast calls and only runs while drones are in docking state.

## Decision 006 - Native State Extension Requires GPU Struct Extension

Problem: `HeadlessDroneState` is uploaded to `DroneCulling.compute`; adding C# fields without matching HLSL layout would corrupt structured buffer indexing.
Solution: Added matching Bezier/docking fields to the compute shader state struct so the GPU stride remains aligned with the C# native state.
Rejected Alternatives: Separate native arrays for control points were rejected because they add allocation/registration surfaces and scatter state ownership for no current runtime gain.
Scalability potential: Low ignores fields except render culling stride; High/Ultra can consume docking state in future GPU VFX.
Hardware Impact: The state stride grows by 60 bytes per drone; at 64 real drones this is 3.75 KB, acceptable for the saved CPU simplicity and deterministic native ownership.

## Decision 007 - Compile Wall Classification

Problem: Full project compile cannot complete because the current repo has missing unrelated dependencies and contracts outside the docking domain.
Solution: Fixed the local generated-project include, reran build, and verified the second build reports no errors in `DroneFleetManager.cs`, `DroneCognitionJob.cs`, `RepairDroneHub.cs`, or `DroneDockingSignals.cs`.
Rejected Alternatives: Editing scheduling, audio propagation, radar, inertial, or memory-layout systems to force a green build was rejected as cross-domain sabotage.
Scalability potential: Compile wall has no runtime scalability impact.
Hardware Impact: No runtime cost; risk is integration-only.

## OMEGA POLISH CHANGES

Problem: The first pass still paid honest scalar costs in visual/tempo math: three `math.distance` calls for docking path estimate, one `math.sqrt` for visual yaw-slip strength, and one `Vector3.magnitude` plus division in the docking raycast probe builder.
Solution: Replaced the path estimate with fixed Bezier control-leg constants plus a dominant-axis `ApproximateDistanceNoSqrt` for the variable middle leg; reused that approximation for yaw-slip magnitude; replaced `Vector3.magnitude` and `1f / length` with `math.lengthsq`, `math.rsqrt`, and multiplication in the obstacle probe.
Rejected Alternatives: Exact cubic arc length, Newton integration, `math.sqrt`, Unity `Vector3.magnitude`, and scalar division were rejected because docking tempo and yaw-slip are cinematic controls; only obstacle query direction needs normalized truth, and `rsqrt` is sufficient.
Scalability potential: Low/MX350 keeps cross-current visual slip disabled. Middle uses approximate slip. High/Ultra can spend saved cycles on stronger docking VFX, hatch vapor, or GPU-side spline glow without changing the deterministic trajectory.
Hardware Impact: On i3/MX350, this removes three square roots per spline refresh, one square root per cross-current visual slip sample, and one scalar division per active docking raycast probe. Estimated gain: 0.20-0.54 us on a 64-slot docking burst with several active dockers; zero GC change.
Cinematic Cheats Used: visual yaw-slip fakes current compensation while position remains spline-authoritative; path length is approximate tempo control, not physical distance; low tier drops the current tilt entirely.
Final Git Diff:
`Assets/_Project/Scripts/Construction/DroneCognitionJob.cs` replaces `math.distance`/`math.sqrt` with `ApproximateDistanceNoSqrt`.
`Assets/_Project/Scripts/Construction/DroneFleetManager.cs` replaces `Vector3.magnitude` and division with `math.rsqrt` probe normalization.
`Docs/Tasks/Status_VEHICLE_AUTONOMOUS_DOCKING.md` records Omega loop evidence and blocked compile status.
`Docs/AgentLogs/Rationale_VEHICLE_AUTONOMOUS_DOCKING.md` records this Omega polish decision.

## Decision 008 - Post-Omega Recheck Upgrades

Problem: Second-pass code review found three correctness gaps: same-frame docking completion could skip hatch-open publication, invalid request failures could carry stale request IDs, and a single P0->P3 obstacle chord could miss blockers on the curved Bezier belly. The yaw-slip path also allowed vertical current to influence visual pitch.
Solution: Added an idempotent `DockingFlagHatchOpenPublished` path so pending hatch events publish before completion clears a slot; added missing-drone and incoming-request-id failure publication; projected cross-current to yaw-plane only; upgraded obstacle probing to segmented Bezier corridor raycasts with 2 segments on Low/Mid and 3 on High/Ultra using the same persistent native arrays.
Rejected Alternatives: Reordering the full service drain was rejected because repair/attack service timing is unrelated. Exact spline swept volumes and managed physics casts were rejected because docking safety needs bounded, batched cost. Full 3D current visual drift was rejected because it violates yaw-slip and makes final approach look less controlled.
Scalability potential: Low/MX350 pays two batched ray segments and no visual current tilt. Mid pays two ray segments and yaw-only cosmetic slip. High/Ultra pay three ray segments and can spend saved physical-solver cost on hatch vapor, guide lights, or GPU spline glow.
Hardware Impact: Low-end cost increases only while drones are actively docking: +1 ray command per active docking drone versus the old chord, still zero GC and batched. The correction removes animation desync risk and improves obstacle reliability without adding per-drone GameObject logic.
Cinematic Cheats Used: segmented ray chords approximate the Bezier tube instead of a true swept volume; yaw-only slip preserves the illusion of current compensation while spline position stays deterministic.
Verification: `dotnet build Hecton8.Core.csproj --no-restore -verbosity:minimal -maxcpucount:1 -nodeReuse:false` still fails from 114 unrelated global dependency errors and reports 0 docking-file matches. Unity MCP validation for both touched scripts still returns `no_unity_session`.

## Decision 009 - Loop-8 Result Dedupe And Exact Completion Target

Problem: Segmented Bezier obstacle probes can generate multiple hit results for the same drone in one raycast batch. The first hit aborts the drone, but later same-slot hits could still be processed, duplicating `DockingFailedSignal` and overcounting `DockingAborts`. Completion also re-read the hub socket when available, which could report a mutable authored transform rather than the exact native endpoint the spline snapped to.
Solution: Re-check `drone.State == Docking` immediately before each abort result is applied, so the first hit owns the state transition and later same-slot hits are ignored. Publish complete using finite `drone.HomePosition` fallbacking to `drone.Position`, and derive `DockForward` from `drone.HomeRotation`.
Rejected Alternatives: A managed `HashSet<int>` or per-frame dedupe list was rejected because it would add allocation or extra persistent state for a result loop that already has authoritative native state. Hub transform readback on completion was rejected because the drone's native state is the completed docking truth after AUP shifts.
Scalability potential: Low/MX350 keeps two corridor segments and now pays only one state byte check per actual hit result. Mid uses the same dedupe. High/Ultra can keep three segments for curve-belly coverage without multiplying failure events or telemetry counts.
Hardware Impact: On i3/MX350, the duplicate-abort guard is a rare-path byte compare. It prevents downstream signal spam and telemetry inflation without adding a hot-path container. Completion publication removes hub lookup branches on the rare complete path.
Cinematic Cheats Used: Segmented line chords still approximate the Bezier corridor instead of a true swept volume; dedupe preserves deterministic gameplay consequences from the first obstruction only.
Verification: `rg` forbidden scan remains clean. `dotnet build Hecton8.Core.csproj --no-restore -verbosity:minimal -maxcpucount:1 -nodeReuse:false` wrote `Temp/VEHICLE_AUTONOMOUS_DOCKING_loop8_build.log`, failed only on unrelated global dependency errors, and had 0 docking-file matches. Unity MCP validation and console read still returned `no_unity_session`.
