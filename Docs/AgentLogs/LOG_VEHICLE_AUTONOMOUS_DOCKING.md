# LOG_VEHICLE_AUTONOMOUS_DOCKING

## 2026-05-13 - HYDRO_MECHANIC - VEHICLE_AUTONOMOUS_DOCKING

Status: PENDING VERIFICATION
Domain: ECHELON 6 HABITAT & VEHICLES / Drone Fleet Commander

What was wrong:
- Docking was architecturally vulnerable to direct-manager ownership and simple return interpolation. That is wrong for the native headless drone fleet, where authority must remain in AUP/native state and cross-agent coupling must use `SignalBus<T>`.
- Docking approach had no dedicated unmanaged request/complete/fail contract, no obstacle abort telemetry, and no spline control-point migration during AUP shifts.
- Initial polish pass still contained honest scalar costs in visual/tempo math: three path-estimate square roots, one yaw-slip square root, and one main-thread `Vector3.magnitude` plus division for docking probe setup.

What was done:
- Added unmanaged docking signal contracts: `DockingRequestSignal`, `DockingCompleteSignal`, and `DockingFailedSignal` under `Hecton8.Vehicles.Automation`.
- Routed docking through `DroneFleetManager` and the existing native headless drone slots. No `DockingManager.Instance` dependency was introduced.
- Implemented cubic Bezier docking controls P0/P1/P2/P3 in `HeadlessDroneState`, evaluated in the existing `DroneCognitionJob` `IJobParallelFor` batch.
- Added kinematic spline override while `State == Docking`; normal AI steering, boids, and flow movement do not perturb final approach.
- Added visual-only cross-current yaw-slip through existing abyssal flow sampling. Position stays spline-authoritative.
- Added hatch-open service command at `t >= 0.8`, final clamp at `t >= 1.0`, exact `HomePosition` snap, velocity clear, and completion signal publish before native slot release.
- Added batched `RaycastCommand` obstacle probes along P0->P3, abort-to-loiter behavior, `DockingFailedSignal`, and `DockingAborts` blackbox telemetry.
- Added AUP shift migration for all Bezier control points and matching GPU `HeadlessDroneState` stride fields in `DroneCulling.compute`.
- Omega polish replaced `math.distance`/`math.sqrt`/`Vector3.magnitude`/division paths in the docking patch surface with constants, dominant-axis approximation, and `math.rsqrt`.

Cinematic cheats used:
- Deterministic Bezier path replaces physical force/drag/solver docking. Player sees smooth autonomy; simulation stays predictable.
- Cross-current is visual yaw-slip only. Current never pushes the drone off the docking spline.
- Path length is tempo approximation, not exact arc length. Fixed control legs use constants; the variable leg uses no-sqrt dominant-axis approximation.
- Low/MX350/Unknown tiers skip current tilt entirely. High/Ultra can spend saved cycles on docking VFX without touching authority.

Exact microseconds saved:
- Docking override skips normal AI steering while docking: estimated 2-5 us per active docking drone.
- `t*t*t` speed curve instead of `math.pow(t, 3)`: estimated 0.05 us per active docking drone.
- Omega no-sqrt path estimate: estimated 0.15-0.40 us per docking spline refresh.
- Omega no-sqrt yaw-slip magnitude: estimated 0.03-0.08 us per active docking drone on enabled tiers.
- Omega `rsqrt` probe normalization replacing `Vector3.magnitude` plus division: estimated 0.02-0.06 us per active docking probe.
- Batched `RaycastCommand` obstacle checks instead of synchronous per-drone physics calls: estimated 3-20 us saved when docking probes exist.
- Rigidbody solve and Transform parenting removed from live docking protocol: solver/transform cost eliminated for headless drones; Unity profiler proof is absent because no Unity session is connected.

Blocked items:
- True asmdef split `Hecton8.Vehicles.Automation -> Contracts` is blocked by `ISignal` living in `Hecton8.Core` and internal Core ownership of the drone fleet.
- Full compile is blocked by global dependency failures outside this domain. Latest `dotnet build Hecton8.Core.csproj --no-restore` reports 102 errors, with 0 matches for `DroneFleetManager.cs`, `DroneCognitionJob.cs`, `RepairDroneHub.cs`, `DroneDockingSignals.cs`, `Hecton8.Vehicles`, or `DroneCulling.compute`.

Verification:
- Prompt extracted by CLI from `Docs/Tasks/CURRENT_BATCH.md`; task count 19; re-extraction checkpoint hash `dd57b10e20bc7f32f9c7fe8014b9edfc2c9f6a3477157b5de7cd0d1e343ef02d`.
- Omega mandate read only after all tasks were done or dependency-blocked.
- `rg` scan found no `math.pow`, `math.sqrt`, `math.distance(`, `Vector3.Slerp`, `Vector3.MoveTowards`, managed `foreach`, `string.Format`, `.ToString()`, or interpolated strings in the docking patch surface.
- `git diff --check` on touched docking files and logs reports no whitespace errors; Git warns only about LF-to-CRLF normalization.
- Unity MCP `validate_script` failed with `no_unity_session`; runtime/editor verification is still pending.

