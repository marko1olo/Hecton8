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
The status/rationale and source code were not aligned. Direct reads of `ContextualPhysicalIkRuntime.cs` and `ContextualPhysicalIkRig.cs` showed old `sqrMagnitude` origin-shift checks, the old normal-only `HasHit` predicate, and fade paths that copied previous targets without finite validation. That made the report stronger than the executable code.

What was done:
Runtime and rig origin-shift handlers now convert shift vectors to `float3`, reject non-finite values, reject non-finite length-squared values, and reject shifts over 10km before rebasing. Runtime writes/dumps `TelemetryReasonInvalidOriginShift` on corrupt shift input. Structural forced-completion now swaps the completed target buffer, publishes it, logs the structural reason, and clears stale handles. `HasHit` requires finite point, normal, normal length, finite distance, and non-negative distance. `FadeOutTarget`, `FadeFootLane`, and `WriteFootSoa` now sanitize stale target data before lower-body SOA writes.

Cinematic cheats used:
No added simulation. The system remains the same lower-body visual fake: hip-origin batched rays, squared step triggers, triangle-wave lift, planar-velocity swim posture, pelvis yaw bias, and Burst two-bone solve.

Exact microseconds saved:
No new steady-state systems. Added work is a few finite checks around existing target writes and hit validation, estimated below 1 us on i3/MX350 for the active player rig. Prevented cost is NaN propagation through SOA lanes and animation jobs, plus wasted debugging caused by docs/source drift.

Verification:
Scoped forbidden-pattern scan over touched IK/KCC/signal files returned no matches for `sqrMagnitude`, `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, `OnAnimatorIK`, `SetIK`, `ikPass`, `StartCoroutine`, `GameObject.Find`, `FindObjectOfType`, or `Camera.main`. `git diff --check` is clean. `dotnet build Hecton8.Core.csproj --no-restore -v:quiet /m:1 /p:UseSharedCompilation=false /clp:ErrorsOnly` timed out again; orphaned `Hecton8.Core.csproj` build children were terminated. A separate `Assembly-CSharp.csproj` build process remains outside this agent's ownership.

Status:
PENDING VERIFICATION. Unity import, Burst compiler, Play Mode, GCMonitor, profiler proof, and full compile proof remain absent.

## 2026-05-15 Recursive QA Addendum 16

What was wrong:
Runtime jobs still had post-rig H-Phi leaks. Ground detection command origins could fall back to `entity.RootPosition` even if that root was corrupt. Predictive latch blend alone could suppress hand wall probes even when the latch position was invalid. Ground response smoothing wrote raw `SmoothScalar` output into tunnel blend, contact blends, packed foot blend state, and COM lean before final sanitizers.

What was done:
`ContextualPhysicalIkGroundDetectionJob` now derives a finite `safeRootPosition`, uses it for camera/tool/hand/foot ray fallback origins, validates predictive latch position before disabling wall probes, and sanitizes fallback values inside its local `SanitizeFloat3`. `ContextualPhysicalIkGroundResponseJob` now sanitizes fallback values in its local `SanitizeFloat3`, routes unit blend smoothing through `SmoothBlend`, and routes COM lean through `SmoothFiniteScalar` before target-frame/foot-state publication.

Cinematic cheats used:
No new sensing, ray lanes, gait physics, or solver authority. The runtime keeps the same deterministic visual fake: hip-origin batched rays, squared step triggers, triangle-wave foot lift, predictive hand latch, and small COM lean, but invalid state collapses to neutral data before publication.

Exact microseconds saved:
Added cost is finite checks, unit-blend clamps, and scalar smooth wrappers inside existing jobs, estimated below 0.4 us/frame on i3/MX350. Prevented cost is NaN ray commands, lost fallback wall probes, packed foot blend corruption, and COM lean spikes.

Verification:
No dotnet rebuild was run per user instruction. `git diff --check` and `git diff --cached --check` over `ContextualPhysicalIkRuntime.cs` passed. Scoped forbidden-pattern scans over lower-body/signal files returned no matches for `sqrMagnitude`, `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, interpolation strings, `.ToString(`, `OnAnimatorIK`, `SetIK`, `ikPass`, `StartCoroutine`, `GameObject.Find`, `FindObjectOfType`, or `Camera.main`. MCP resource listing returned no Unity resources.

Status:
PENDING VERIFICATION. Unity import, Burst compiler, Play Mode, GCMonitor, profiler proof, and full compile proof remain absent.

## 2026-05-14 Recursive QA Addendum 6

What was wrong:
Telemetry could detect invalid target state, but some executable boundaries still trusted previous-frame values. Specifically, skipped-frame target reuse could publish stale data, hand SOA writes could preserve a finite weight for an invalid hand position, foot fade/update could smooth from a non-finite packed blend, and cold AUP rebases could subtract shift from corrupt target lanes.

What was done:
`ContextualPhysicalIkRuntime` now sanitizes complete target frames before SOA publication and before `ContextualPhysicalIkApplyJob` can read them. Hand SOA positions zero and weights drop to zero on invalid position/blend. Foot fade/update uses sanitized packed blend inputs. AUP rebase now clears invalid hand/foot SOA lanes and invalid packed foot state instead of rebasing corrupt values. `ContextualPhysicalIkRig` now rejects non-finite root transform capture and falls back corrupt pelvis/foot/hand probe transforms to the root position.

Cinematic cheats used:
No new physical simulation. The implementation remains hip-origin batched rays, squared step thresholds, triangle-wave lift, planar swim posture, pelvis yaw bias, and the existing Burst two-bone solver.

Exact microseconds saved:
No new systems or ray lanes. Added finite checks are estimated below 1 us per active rig on i3/MX350. Avoided cost is NaN target propagation into the animation job and the visual/diagnostic damage from invalid rebases.

Verification:
Scoped forbidden-pattern scan returned no matches for `sqrMagnitude`, `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, interpolation strings, `OnAnimatorIK`, `SetIK`, `ikPass`, `StartCoroutine`, `GameObject.Find`, `FindObjectOfType`, or `Camera.main`. `git diff --check` is clean except CRLF warnings. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false /clp:ErrorsOnly` timed out again before diagnostics; this pass's orphaned `Hecton8.Core.csproj` build children were terminated.

Status:
PENDING VERIFICATION. Unity import, Burst compiler, Play Mode, GCMonitor, profiler proof, and full compile proof remain absent.

## 2026-05-15 Recursive QA Addendum 7

What was wrong:
The target-frame quarantine did not fully close H-Phi at the animation execution boundary. `ContextualPhysicalIkApplyJob` still accepted invalid stream-handle reads, chain metadata, cached pose payloads, muscle output, and raw scalar saturation. The PlayerKinematics hand producer could also publish NaN/invalid wall or squeeze blends into the IK rig.

What was done:
`ContextualPhysicalIkRig` now finite-gates pelvis, two-bone, FABRIK appendage, spine, secondary, cached pose replay, quaternion approximations, muscle bulge accumulation, authoring blends, squeeze pole offsets, cold shiver, predictive latch, and external hand target blends. `ContextualPhysicalIkRuntime` now uses finite-safe `SanitizeBlend` at hand SOA, foot progress, foot fade, slope lean, predictive target, collision response, hand offsets, and target-frame sanitization. `PlayerKinematicsRuntime` now sanitizes brace/squeeze/stress/load/immersion/acoustic/haptic scalar paths and rejects invalid smoothed hand targets before calling IK.

Cinematic cheats used:
No new simulation, no new rays, no gait planner, no managed owner. The lower body remains hip-origin batched rays, squared-distance step logic, triangle-wave lift, planar swim posture, pelvis yaw bias, Burst two-bone solve, and optional secondary/muscle visual overkill fed only by finite values.

Exact microseconds saved:
Added cost is branch/finite scalar checks inside existing loops, estimated below 1 us for the standard player rig on i3/MX350. Avoided cost is catastrophic NaN propagation through animation jobs, repeated solver correction, invalid hand latching, and debugging without trustworthy H-Phi boundaries.

Verification:
No dotnet rebuild was run per user instruction. `git diff --check -- Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs` passed with CRLF warnings only. Scoped forbidden-pattern scan over the touched lower-body/signal files returned no matches for `sqrMagnitude`, `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, interpolation strings, `OnAnimatorIK`, `SetIK`, `ikPass`, `StartCoroutine`, `GameObject.Find`, `FindObjectOfType`, or `Camera.main`.

Status:
PENDING VERIFICATION. Unity import, Burst compiler, Play Mode, GCMonitor, profiler proof, and full compile proof remain absent.

## 2026-05-15 Recursive QA Addendum 8

What was wrong:
The lower-body IK path was finite-gated downstream, but its KCC velocity producer still had a small upstream H-Phi leak. Bad planar/vertical input, bad SDF sample step, or non-finite roll spring state could contaminate intended movement or roll state before velocity was published for stride/swim prediction.

What was done:
`PlayerKinematicsRuntime` now zeroes non-finite planar input, clamps vertical input through a signed-unit sanitizer, sanitizes intended movement before storing it, sanitizes SDF gradient sample step, sanitizes roll side-dot/target/position/velocity, uses non-negative roll amplitude, and zeroes non-finite triangle-wave phase.

Cinematic cheats used:
No new physical model. The fix preserves the existing visual lie: KCC-derived velocity lead, triangle-wave roll/step response, and lower-body IK prediction. Invalid input now collapses to neutral movement instead of trying to simulate through corrupt data.

Exact microseconds saved:
Added cost is a few scalar/vector finite checks in existing player kinematic paths, estimated below 1 us/frame on i3/MX350. Avoided cost is NaN propagation into KCC velocity, foot-ray lead, swim posture, and IK smoothing.

Verification:
No dotnet rebuild was run per user instruction. `git diff --check -- Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs` passed with CRLF warnings only. Scoped forbidden-pattern scan over touched lower-body/signal files returned no matches for `sqrMagnitude`, `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, interpolation strings, `OnAnimatorIK`, `SetIK`, `ikPass`, `StartCoroutine`, `GameObject.Find`, `FindObjectOfType`, or `Camera.main`.

Status:
PENDING VERIFICATION. Unity import, Burst compiler, Play Mode, GCMonitor, profiler proof, and full compile proof remain absent.

## 2026-05-15 Recursive QA Addendum 9

What was wrong:
The downstream target and KCC paths were finite-gated, but two H-Phi edge cases remained in the animation/raycast boundary. A finite zero-length stream quaternion could pass a finite-only check before inverse math, and corrupt camera/probe/origin scalar data could still degrade IK ray commands or proxy blends into unsafe fallbacks.

What was done:
`ContextualPhysicalIkMath` now normalizes finite Unity quaternions through the no-sqrt `rsqrt` path while preserving zero/invalid quaternions for rejection. `ContextualPhysicalIkRig` now requires quaternions to be finite and non-zero length before treating them as valid, and the apply job owns its spine target count for range validation. `ContextualPhysicalIkRuntime` now sanitizes brace directions, camera/tool/hand/foot ray origins, foot step cache state, tool retraction/recoil origins, contact offsets, max-delta heights, collision distances, smoothing fallbacks, and brace proxy distances before writing commands or targets.

Cinematic cheats used:
No physical gait, no extra rays, no new solver. The system remains a visual fake: batched hip-origin rays, squared thresholds, triangle-wave foot lift, planar swim posture, small pelvis yaw, and Burst two-bone solve.

Exact microseconds saved:
Added cost is finite checks, length-squared checks, and existing `rsqrt` quaternion normalization inside existing paths, estimated below 1 us/frame on i3/MX350. Prevented cost is NaN/zero-quaternion inverse fallout, zero-origin ray pollution, bad step cache replay, and expensive visual correction/debugging.

Verification:
No dotnet rebuild was run per user instruction. `git diff --check -- Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkMath.cs Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs` passed with CRLF warnings only. Scoped forbidden-pattern scan over lower-body/signal files returned no matches for `sqrMagnitude`, `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, interpolation strings, `OnAnimatorIK`, `SetIK`, `ikPass`, `StartCoroutine`, `GameObject.Find`, `FindObjectOfType`, or `Camera.main`. MCP resource listing returned no Unity resources.

Status:
PENDING VERIFICATION. Unity import, Burst compiler, Play Mode, GCMonitor, profiler proof, and full compile proof remain absent.

## 2026-05-15 Recursive QA Addendum 10

What was wrong:
`PlayerKinematicsRuntime` black-box telemetry trusted `_telemetryWriteIndex` directly. A negative or stale native cursor could become an invalid modulo index, and some main-thread telemetry payloads could copy non-finite position/velocity/intended movement into the dump path.

What was done:
Added bounded telemetry slot reservation for the Burst body job and the main-thread squeeze, environment IK, and sync-fence telemetry writers. The cursor now clamps negative values to zero, rejects missing or zero-length buffers, advances from the wrapped slot, telemetry payloads finite-sanitize position, velocity, and intended movement before writing, and the binary dump emits oldest-to-newest entries from a sanitized wrapped head.

Cinematic cheats used:
No simulation change. This protects the 300-frame black box behind the existing KCC-driven lower-body visual fake so post-mortem evidence stays usable when stride/swim/hand IK data faults.

Exact microseconds saved:
Added cost is integer bounds checks and vector finite selects only on telemetry writes, estimated below 0.5 us/event on i3/MX350. Prevented cost is fault-path collapse from invalid telemetry indexing and unusable dump evidence.

Verification:
No dotnet rebuild was run per user instruction. `git diff --check -- Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs` passed with CRLF warnings only. Scoped forbidden-pattern scan over lower-body/signal files returned no matches for `sqrMagnitude`, `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, interpolation strings, `OnAnimatorIK`, `SetIK`, `ikPass`, `StartCoroutine`, `GameObject.Find`, `FindObjectOfType`, or `Camera.main`. MCP resource listing returned no Unity resources.

Status:
PENDING VERIFICATION. Unity import, Burst compiler, Play Mode, GCMonitor, profiler proof, and full compile proof remain absent.

## 2026-05-15 Recursive QA Addendum 11

What was wrong:
Environment IK telemetry sanitized its cursor, position, velocity, and intended movement payloads, but could still write a non-finite `activeBlend` into `SolidDensity` when squeeze, impact, low-tier, or scrape flags triggered the event independently of brace blend.

What was done:
`WriteEnvironmentIkTelemetry` now clamps `activeBlend` through `SanitizeUnit` before aux flag selection and writes the same finite scalar into `SolidDensity`.

Cinematic cheats used:
No simulation change. This is fault-path hygiene for the existing KCC-to-IK visual fake and its 300-frame black box.

Exact microseconds saved:
Added cost is one scalar finite/clamp operation per environment IK telemetry event, estimated below 0.1 us/event on i3/MX350. Prevented cost is corrupted dump evidence from a NaN scalar in an otherwise valid squeeze/impact/scrape event.

Verification:
No dotnet rebuild was run per user instruction. `git diff --check` over the touched IK/KCC/docs files passed with CRLF warnings only. Scoped forbidden-pattern scan over the lower-body/signal files returned no matches for `sqrMagnitude`, `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, interpolation strings, `OnAnimatorIK`, `SetIK`, `ikPass`, `StartCoroutine`, `GameObject.Find`, `FindObjectOfType`, or `Camera.main`.

Status:
PENDING VERIFICATION. Unity import, Burst compiler, Play Mode, GCMonitor, profiler proof, and full compile proof remain absent.

## 2026-05-15 Recursive QA Addendum 12

What was wrong:
The KCC-to-IK pipeline still had producer-side H-Phi holes. Raw Rigidbody position/velocity could enter NativeArrays before the Burst body job sanitizer ran. Origin-shift offsets could rebase KCC arrays with invalid data. Sync-fence hashes had raw fallbacks. The contextual IK black-box ring also trusted its private cursor/head and could dump non-finite first-sample vectors.

What was done:
`PlayerKinematicsRuntime` now sanitizes Rigidbody position/velocity before NativeArray writes, SDF sampling, advection, KCC signal publishing, reset/start state, staged sync-fence writes, and sync-fence hashing. Invalid raw body state flags `FaultNaN`; invalid or >10km origin-shift input flags `FaultInvalidOriginShift` and dumps without rebasing. `ContextualPhysicalIkRuntime` now bounds telemetry cursor/head against the actual ring length and sanitizes telemetry vectors/weights before hashing, ring writes, and binary dump serialization.

Cinematic cheats used:
No physical gait, no additional probes, no new solver. The lower-body remains the same visual fake: batched hip-origin rays, squared thresholds, triangle-wave lift, planar swim posture, small pelvis yaw, and Burst two-bone solve.

Exact microseconds saved:
Added cost is branch/finite checks at existing producer/sync-fence boundaries, estimated below 0.5 us/frame on i3/MX350, plus below 0.1 us/contextual telemetry sample. Prevented cost is NaN propagation into NativeArray-backed KCC/IK state and unusable black-box dumps.

Verification:
No dotnet rebuild was run per user instruction. `git diff --check` over touched IK/KCC files passed with CRLF warnings only. Scoped forbidden-pattern scan over lower-body/signal files returned no matches for `sqrMagnitude`, `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, interpolation strings, `OnAnimatorIK`, `SetIK`, `ikPass`, `StartCoroutine`, `GameObject.Find`, `FindObjectOfType`, or `Camera.main`. Additional source scans found no remaining raw `SnapMillimeter(ToFloat3(_body.*))`, `_body.position` AUP hashing, direct position subtraction rebases, or direct `_telemetryRing[_telemetryCursor]` writes in the audited files.

Status:
PENDING VERIFICATION. Unity import, Burst compiler, Play Mode, GCMonitor, profiler proof, and full compile proof remain absent.

## 2026-05-15 Recursive QA Addendum 13

What was wrong:
The KCC-to-IK path still had small output-side H-Phi leaks after producer sanitization. GPU-flow metadata could enable full advection boost even if the field metadata was corrupt or degenerate. SDF payload origin/cell/range metadata could reach the body job. Ladder hit points, acoustic AUP output, shader VAT scalar, and movement roll publication trusted cached or caller data at the final boundary. Contextual IK scheduling/reset/rebase/telemetry paths also assumed native storage lengths matched the fixed lane counts.

What was done:
`PlayerKinematicsRuntime` now rejects non-finite or degenerate GPU-flow metadata, rejects invalid SDF payload metadata, ignores non-finite ladder hit points, clamps scaled advection after multiplication, snaps/sanitizes acoustic AUP output, sanitizes cached VAT scalar comparisons, and publishes roll only through a finite neutral fallback. `ContextualPhysicalIkRuntime` now validates native storage before scheduling the ground pipeline, adds a black-box reason flag for invalid storage, length-guards scheduled state and target-frame rebases, length-guards telemetry sampling, and bounds reset writes for hand/foot SOA lanes.

Cinematic cheats used:
No new gait physics, no additional rays, no solver change. Invalid metadata falls back to cheaper visual approximations: CPU-scaled advection, neutral roll/VAT, and no contextual IK schedule when native storage is invalid.

Exact microseconds saved:
Added cost is finite checks, integer length comparisons, and scalar sanitization at existing boundaries, estimated below 0.3 us/frame on i3/MX350 plus cold reset-only guards. Prevented cost is corrupt metadata amplifying stride/swim prediction or faulting the native ground pipeline.

Verification:
No dotnet rebuild was run per user instruction. `git diff --check` over touched IK/KCC files passed with CRLF warnings only. Scoped forbidden-pattern scan over lower-body/signal files returned no matches for `sqrMagnitude`, `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, interpolation strings, `OnAnimatorIK`, `SetIK`, `ikPass`, `StartCoroutine`, `GameObject.Find`, `FindObjectOfType`, or `Camera.main`.

Status:
PENDING VERIFICATION. Unity import, Burst compiler, Play Mode, GCMonitor, profiler proof, and full compile proof remain absent.

## 2026-05-15 Recursive QA Addendum 14

What was wrong:
Rig-side capture still had source-boundary H-Phi leaks before the runtime/job sanitizer. Predictive controller AUPs could be built before finite controller-position validation. Spine targets could write bad HMD yaw/breath offsets into NativeArray lanes. Appendage targets could publish non-finite transform or snapped-corner positions with nonzero weight. Upper-arm FOV culling could hide arms when camera or renderer bounds data became corrupt.

What was done:
`ContextualPhysicalIkRig` now gates predictive controller AUP creation on finite source positions, keeps previous controller AUPs only when the source pose is valid, sanitizes spine target writes, sanitizes appendage surface normals, ignores non-finite voxel snapped corners, zeroes appendage weight when the target position is invalid, sanitizes upper-arm culling hysteresis, and fails open to visible arms on invalid camera/bounds culling data.

Cinematic cheats used:
No extra raycasts, no physical arm model, no new solver. Invalid capture data collapses to neutral targets or visible arms so the existing visual-fake IK path stays believable instead of trying to simulate through corrupt transforms.

Exact microseconds saved:
Added cost is branch/finite checks plus one no-sqrt normal fallback inside existing rig capture/culling paths, estimated below 0.3 us/frame on i3/MX350. Prevented cost is IK target spikes, corrupt appendage weights, and disappearing arms caused by invalid source transforms or renderer bounds.

Verification:
No dotnet rebuild was run per user instruction. `git diff --check` over touched IK/KCC files passed with CRLF warnings only. Scoped forbidden-pattern scan over lower-body/signal files returned no matches for `sqrMagnitude`, `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, interpolation strings, `OnAnimatorIK`, `SetIK`, `ikPass`, `StartCoroutine`, `GameObject.Find`, `FindObjectOfType`, or `Camera.main`.

Status:
PENDING VERIFICATION. Unity import, Burst compiler, Play Mode, GCMonitor, profiler proof, and full compile proof remain absent.

## 2026-05-15 Recursive QA Addendum 15

What was wrong:
Rig-local transient state still had H-Phi trust windows after the previous capture pass. Recoil offsets decayed raw stored vectors. Terminal/external/predictive blend latches could preserve non-finite smooth output. Breathing and cold-shiver phases only subtracted one wrap. Predictive repair trusted AUP distance/runtime target output after snap resolution. Spine, appendage, and muscle output writes assumed NativeArray and managed companion lengths were always aligned.

What was done:
`ContextualPhysicalIkRig` now clamps decayed recoil offsets with the existing no-sqrt vector clamp, sanitizes terminal/external/predictive/breathing/shiver/muscle smooth outputs, wraps breathing and shiver phases through a finite positive phase helper, sanitizes shiver offsets, rejects non-finite predictive AUP distance/runtime targets before latch publication, length-guards spine and muscle target output, bounds appendage capture to the shorter native target/runtime length, and null/length guards appendage target/fallback companion arrays.

Cinematic cheats used:
No new physical leg, arm, or torso simulation. The patch preserves the visual-fake model: finite scalar latches, triangle-wave breathing/shiver, no-sqrt recoil decay, and deterministic target rejection before the Burst IK job consumes data.

Exact microseconds saved:
Added cost is scalar finite/clamp work, one `math.floor` phase wrap per active breathing/shiver tick, and integer length guards, estimated below 0.4 us/frame on i3/MX350. Prevented cost is native target faults, hand-latch spikes, corrupt muscle shader output, and NaN propagation into lower-body/appendage presentation.

Verification:
No dotnet rebuild was run per user instruction. `git diff --check` and `git diff --cached --check` over `ContextualPhysicalIkRig.cs` passed with CRLF warnings only. Scoped forbidden-pattern scans over lower-body/signal files returned no matches for `sqrMagnitude`, `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, interpolation strings, `.ToString(`, `OnAnimatorIK`, `SetIK`, `ikPass`, `StartCoroutine`, `GameObject.Find`, `FindObjectOfType`, or `Camera.main`. Rig-only scan found no remaining direct `= ContextualPhysicalIkMath.SmoothScalar` assignments.

Status:
PENDING VERIFICATION. Unity import, Burst compiler, Play Mode, GCMonitor, profiler proof, and full compile proof remain absent.
