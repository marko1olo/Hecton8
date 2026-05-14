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
`ContextualPhysicalIkRuntime` now caches the KCC body AUP runtime position when consuming `KccVelocitySignal`. Per entity, it applies the KCC velocity only when the rig root is finite and within 4m of the KCC body position. The cached KCC body position rebases on origin shift. Non-matching rigs receive zero KCC velocity.

Cinematic cheats used:
Kept the same presentation fake: two batched foot rays, planar velocity lead, squared thresholds, and triangle-wave stepping. Rejected direct player movement coupling and new rig ownership APIs.

Exact microseconds saved:
No managed allocation. Added cost is one finite gate and one squared-distance compare per active rig, estimated <0.1 us/frame for the single-player rig case. Prevented future visual corruption rather than adding simulation.

Verification:
Prompt re-extracted from `CURRENT_BATCH.md`. Targeted `dotnet build` filter completed with no matching errors for touched IK/KCC/signal files. Static anti-bloat scan over touched lower-body/signal files returned no forbidden hot-path patterns. `git diff --check` passed with CRLF warnings only.

Status:
PENDING VERIFICATION. Unity import, Burst compiler, Play Mode, GCMonitor, and profiler proof remain absent.

## 2026-05-14 Recursive QA Addendum 5

What was wrong:
The lifecycle race guard was memory-safe but incomplete. It force-completed a pending IK ground response before slot mutation, then discarded the completed back-buffer frame by not swapping/publishing it. During register/unregister churn, other active rigs could miss one finished lower-body target update. Forced origin-shift completion also left the old completed `JobHandle` value in the field and did not mark telemetry with a reason flag.

What was done:
`ContextualPhysicalIkRuntime` now swaps the completed target buffer inside `CompletePendingGroundResponseForStructuralMutation()` and returns whether a frame was produced. `RegisterRig` publishes that frame before adding the new slot. `UnregisterRig` removes/resets the leaving slot first, then publishes to the remaining active rigs so a disabling rig does not receive an unnecessary buffer swap. Normal LateFrame, structural completion, and origin-shift completion all clear `_pendingGroundResponseHandle`. Structural and origin-shift forced completions now write distinct black-box telemetry reason flags.

Cinematic cheats used:
No added simulation. The fix preserves the existing two batched foot rays, squared step trigger, triangle-wave lift, and Burst two-bone presentation lie. It only fixes target-buffer ownership and black-box chronology.

Exact microseconds saved:
Hot path: 0 us added. Cold lifecycle/origin-shift path: one required forced completion when a job is already pending, one buffer swap, one active-rig publish pass, and one fixed telemetry sample over 128 slots. Avoided cost is a dropped target frame and post-mortem ambiguity; steady-state i3/MX350 frame time is unchanged.

Verification:
Current `CURRENT_BATCH.md` has rotated away from this agent prompt, so persisted status/rationale were used and neighboring prompts were ignored. Scoped `dotnet build Hecton8.Core.csproj --no-restore -v:quiet /m:1 /p:UseSharedCompilation=false /clp:ErrorsOnly` filter completed with no matching errors for `ContextualPhysicalIkRuntime`, `ContextualPhysicalIkRig`, `PlayerKinematicsRuntime`, `PhysicsDeterminismSignals`, `KccVelocitySignal`, or `LowerBodyPresenceIkJobs`. Scoped anti-bloat scan returned no matches for `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, interpolation, `OnAnimatorIK`, `SetIK`, `ikPass`, `StartCoroutine`, `GameObject.Find`, `FindObjectOfType`, or `Camera.main`. `git diff --check` reports CRLF warnings only.

Status:
PENDING VERIFICATION. Unity import, Burst compiler, Play Mode, GCMonitor, and profiler proof remain absent because Unity MCP resources are unavailable and the global build remains outside-domain red.

## 2026-05-14 Recursive QA Addendum 6

What was wrong:
The runtime and rig origin-shift listeners trusted `Vector3.sqrMagnitude` before checking finite values. NaN compares through that guard and would rebase lower-body target frames, foot SOA lanes, hand targets, spine targets, and cached KCC body position into corrupt world-space data. The rig pole-offset builder also had a cold authoring path where corrupt transform positions or rotation could generate a non-finite pole vector for the animation job.

What was done:
`ContextualPhysicalIkRuntime.OnOriginShift` and `ContextualPhysicalIkRig.OnOriginShift` now convert the shift to `float3`, reject non-finite vectors, reject non-finite length-squared values, and reject shifts over the 10km AUP mandate cap before any rebase. Runtime writes a telemetry sample and dumps with `TelemetryReasonInvalidOriginShift` on corrupt shift input. Rig terminal-hand normal and local pole-offset setup now use finite `math.lengthsq` guards and explicit fallback values.

Cinematic cheats used:
No physical simulation added. This is AUP hygiene around the existing visual IK fake: two batched foot rays, squared step trigger, triangle-wave step lift, and two-bone Burst pose solve.

Exact microseconds saved:
Hot path: 0 us added. Origin shift and authoring setup are cold paths; added cost is finite checks and `math.lengthsq`. Prevented cost is catastrophic NaN propagation through 256 lower-body SOA lanes and animation job data.

Verification:
Current `CURRENT_BATCH.md` still does not contain this agent prompt, so persisted assignment/status/rationale were used and neighboring prompts were ignored. Scoped `dotnet build Hecton8.Core.csproj --no-restore -v:quiet /m:1 /p:UseSharedCompilation=false /clp:ErrorsOnly` filter completed with `NO_MATCHING_ERRORS_IN_TOUCHED_IK_FILES`. Forbidden-pattern scan over touched lower-body/signal files returned no matches for `sqrMagnitude`, `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, interpolation, `OnAnimatorIK`, `SetIK`, `ikPass`, `StartCoroutine`, `GameObject.Find`, `FindObjectOfType`, or `Camera.main`. `git diff --check` reports CRLF warnings only.

Status:
PENDING VERIFICATION. Unity import, Burst compiler, Play Mode, GCMonitor, and profiler proof remain absent because Unity MCP resources are unavailable and the global build remains outside-domain red.

## 2026-05-14 Recursive QA Addendum 7

What was wrong:
The shared `HasHit` predicate could accept a raycast result because the normal looked meaningful while the hit point or distance was not proven finite. That creates a narrow but serious path for corrupt physics data to enter foot targets and contextual hand targets before downstream finite guards see a derived value instead of the original bad hit.

What was done:
`ContextualPhysicalIkRuntime.HasHit` now converts the hit point and normal to `float3`, computes normal length-squared once, and accepts the hit only when distance, point, normal, and normal length-squared are all finite. The existing positive-distance or meaningful-normal condition remains as the final acceptance gate. This fixes the shared source instead of duplicating checks in every target builder.

Cinematic cheats used:
No added simulation. The lower-body presence remains the same visual fake: two hip-origin batched foot rays, squared step trigger, triangle-wave lift, swim posture from planar KCC velocity, and Burst two-bone presentation. The change only rejects corrupt raycast data before it reaches the fake.

Exact microseconds saved:
Hot path added cost is a few finite checks and one `math.lengthsq` per validated hit, estimated below 1 us across the two-foot plus contextual hand-probe lanes on i3/MX350. Avoided cost is NaN propagation through lower-body SOA state and animation job inputs, plus post-mortem time from ambiguous crash data.

Verification:
Scoped `dotnet build Hecton8.Core.csproj --no-restore -v:quiet /m:1 /p:UseSharedCompilation=false /clp:ErrorsOnly` filter completed with `NO_MATCHING_ERRORS_IN_TOUCHED_IK_FILES`. Forbidden-pattern scan over touched lower-body/signal files returned no matches for `sqrMagnitude`, `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, `OnAnimatorIK`, `SetIK`, `ikPass`, `StartCoroutine`, `GameObject.Find`, `FindObjectOfType`, or `Camera.main`. `git diff --check` reports CRLF warnings only.

Status:
PENDING VERIFICATION. Unity import, Burst compiler, Play Mode, GCMonitor, and profiler proof remain absent because Unity MCP resources are unavailable and the global build remains outside-domain red.
