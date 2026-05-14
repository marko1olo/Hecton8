# LOG_ANIM_PROCEDURAL_LEGS_IK

## 2026-05-14 VR Lower Body Presence

What was wrong:
VR lower body presence depended on authored animation only. Looking down could expose a floating torso. Full-body physical IK was rejected by the prompt and by MX350/i3 frame budget.

What was done:
Extended the existing `ContextualPhysicalIkRuntime`/`ContextualPhysicalIkRig` path rather than creating a second IK owner. Added packed lower-body foot data under `Assets/_Project/Scripts/Animation/IK/LowerBodyPresenceIkJobs.cs`. Added `KccVelocitySignal` to `PhysicsDeterminismSignals` and published it from `PlayerKinematicsRuntime`. Added persistent SOA foot target/current lanes, hip-origin batched foot ray commands, per-foot step triggering, alternating step phase lock, swim fallback from KCC velocity, pelvis yaw bias from camera look, and origin-shift rebasing for new foot state.

Cinematic cheats used:
Two downward batched rays instead of body simulation. Squared-distance thresholds instead of gait analysis. Triangle-wave Y lift instead of foot dynamics. Velocity-backed swim pose instead of full swim-body IK. Small pelvis yaw dot product instead of full spine twist.

Exact microseconds saved:
New singleton owner rejected: 0.0 us/frame ownership overhead added. Unity Animator IK rejected: avoids Animator IK pass. Non-XR low tier disables leg IK: saves two foot ray lanes and lower-body job math on MX350 outside VR. Added work remains estimated under 10 us/frame for one active rig pending profiler proof.

Verification:
`dotnet build Hecton8.Core.csproj` attempted. Full build is blocked by unrelated missing assemblies/types and duplicate `SaveManager` members. Targeted build filtering after the fix returned no errors from `ContextualPhysicalIkRuntime`, `ContextualPhysicalIkRig`, `PlayerKinematicsRuntime`, `PhysicsDeterminismSignals`, `KccVelocitySignal`, or `ContextualPhysicalIkFootData`. Unity MCP validation failed due HTTP transport error at `127.0.0.1:8088/mcp`.

Status:
PENDING VERIFICATION. Global compile dependency wall remains outside this domain.

## 2026-05-14 Recursive Lower-Body Polish Addendum

What was wrong:
Re-verification found three quality risks in the lower-body presentation path: hip-origin foot rays preserved lateral stance but not authored fore/aft stance, swim fallback used full KCC velocity including vertical drift, and stale packed foot state could theoretically keep both feet stepping if both lanes arrived with the stepping flag already set.

What was done:
Updated `ContextualPhysicalIkRuntime` only. Removed the stale Gameplay `using Hecton8.Animation.IK` so local generated Core compile no longer depends on the asmdef-only namespace. Hip foot rays now include authored fore/aft probe projection plus a finite-clamped planar KCC velocity lead. Swim fallback now derives direction from planar KCC velocity against root-up. Step triggering stores a finite-clamped velocity-scaled squared threshold and cancels one stale step if both left/right lanes are marked stepping.

Cinematic cheats used:
Kept the same two downward batched rays. Used dot products, length-squared, rsqrt-backed safe normalization, and triangle-wave stepping. Rejected extra capsule sweeps, gait planning, and full-body IK.

Exact microseconds saved:
No new ray lanes and no managed allocations. Estimated added math is <1.5 us/frame for one active rig. Rejecting capsule sweeps/full gait planning avoids a likely 40-150 us/frame VR presentation cost on i3/MX350.

Verification:
Prompt re-extracted from `CURRENT_BATCH.md`. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` still exits red from unrelated global dependency walls, but filtering for `ContextualPhysicalIkRuntime`, `ContextualPhysicalIkRig`, `PlayerKinematicsRuntime`, `PhysicsDeterminismSignals`, `KccVelocitySignal`, `ContextualPhysicalIkFootData`, and `LowerBodyPresenceIkJobs` returned no matching errors. Scoped anti-bloat scan over touched lower-body/signal files returned no matches for `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, interpolation, `OnAnimatorIK`, `SetIK`, or `ikPass`. Unity MCP `validate_script` remains blocked by HTTP transport failure at `127.0.0.1:8088/mcp`.

Status:
PENDING VERIFICATION. Full Unity/Burst verification remains blocked by the current editor/MCP and global compile state.

## 2026-05-14 Recursive QA Addendum 2

What was wrong:
The AUP rebase pass skipped SOA float3 lanes near zero to protect inactive defaults. That can leave a valid active IK target stale if a floating-origin shift happens while the player is near world origin. The IK black-box dump wrote circular-buffer memory in physical order without a header. The distance-based IK cadence also switched immediately at 10m and 25m.

What was done:
`ContextualPhysicalIkRuntime` now rebases hand SOA lanes by active IK weight and foot SOA lanes by active packed foot blend, so inactive zero lanes stay clean while active near-origin targets shift correctly. The telemetry dump writes a magic/header/head/reason and then oldest-to-newest entries. `ContextualPhysicalIkRig` now keeps a one-byte stable throttle tier with a 4m distance hysteresis band.

Cinematic cheats used:
Kept the same batched ray count and cadence bitmasks. Used hysteresis and weighted data ownership instead of adding probes, timers, or physical gait state.

Exact microseconds saved:
No hot-path allocations. AUP rebase and dump changes are cold-path only. Throttle hysteresis adds branch math estimated below 0.1 us/frame and prevents cadence churn at distance thresholds.

Verification:
`dotnet build Hecton8.Core.csproj --no-restore -v:quiet /m:1 /p:UseSharedCompilation=false /clp:ErrorsOnly` still exits red from unrelated global dependency walls, but filtering for the changed IK/KCC/signal files returned no matching errors. Scoped anti-bloat scan over touched lower-body/signal files returned no matches for `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, interpolation, `OnAnimatorIK`, `SetIK`, or `ikPass`. `git diff --check` passed with line-ending warnings only. Unity MCP `validate_script` remains blocked by HTTP transport failure at `127.0.0.1:8088/mcp`.

Status:
PENDING VERIFICATION. Unity import/Burst validation and profiler numbers are still absent.

## 2026-05-14 Recursive QA Addendum 3

What was wrong:
The runtime slot lifecycle had a concrete race risk. `RegisterRig` and `UnregisterRig` can reset target, hand, and lower-body foot NativeArray lanes. If an IK ground response job was still pending, those lifecycle paths could mutate the same slot-owned buffers while jobs were writing them.

What was done:
Added `CompletePendingGroundResponseForStructuralMutation()` in `ContextualPhysicalIkRuntime` and call it before slot allocation/free/reset. The method force-completes the pending IK response handle only on structural lifecycle mutation and then clears the scheduled state. The steady FastTick/LateFrame pipeline remains non-blocking.

Cinematic cheats used:
No new simulation. No new ray lanes. No gait planner. This is write-ownership hygiene around the existing two-ray, triangle-step lower-body fake.

Exact microseconds saved:
Hot path: 0 us added. Lifecycle path: one forced completion only if a rig registers/unregisters while a response job is pending. Avoided failure mode is a NativeArray write race that would cost debugging time or crash stability, not steady-state CPU.

Verification:
Prompt re-extracted from `CURRENT_BATCH.md`. Static anti-bloat scan over touched IK/KCC/signal files returned no matches for forbidden hot-path patterns. `git diff --check` passed with CRLF warnings only. First targeted `dotnet build` filter timed out; second pass completed with no matching errors for `ContextualPhysicalIkRuntime`, `ContextualPhysicalIkRig`, `PlayerKinematicsRuntime`, `PhysicsDeterminismSignals`, `KccVelocitySignal`, or `LowerBodyPresenceIkJobs`. MCP resource listing returned no Unity resources.

Status:
PENDING VERIFICATION. Unity import, Burst compiler, Play Mode, GCMonitor, and profiler proof are still absent.

## 2026-05-14 Recursive QA Addendum 4

What was wrong:
The shared IK runtime consumed the latest player `KccVelocitySignal` and assigned that velocity to every registered rig. That works only while the player rig is the sole user. It is wrong for scalable shared IK because a future NPC/secondary rig would inherit player step lead, velocity-scaled step threshold, and swimming posture.

What was done:
`ContextualPhysicalIkRuntime` now caches the KCC body AUP runtime position when consuming `KccVelocitySignal`. Per entity, it applies the KCC velocity only when the rig root is finite and within 4m of the KCC body position. Non-matching rigs receive zero KCC velocity.

Cinematic cheats used:
Kept the same presentation fake: two batched foot rays, planar velocity lead, squared thresholds, and triangle-wave stepping. Rejected direct player movement coupling and new rig ownership APIs.

Exact microseconds saved:
No managed allocation. Added cost is one finite gate and one squared-distance compare per active rig, estimated <0.1 us/frame for the single-player rig case. Prevented future visual corruption rather than adding simulation.

Verification:
Prompt re-extracted from `CURRENT_BATCH.md`. Targeted `dotnet build` filter completed with no matching errors for touched IK/KCC/signal files. Static anti-bloat scan over touched lower-body/signal files returned no forbidden hot-path patterns. `git diff --check` passed with CRLF warnings only.

Status:
PENDING VERIFICATION. Unity import, Burst compiler, Play Mode, GCMonitor, and profiler proof remain absent.
