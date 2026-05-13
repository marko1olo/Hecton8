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
