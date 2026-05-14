# ANIM_PROCEDURAL_LEGS_IK Rationale

Status: PENDING VERIFICATION

## Decision 1: Extend existing contextual IK owner

Problem: VR lower-body presence needs legs without creating a second animation authority.
Solution: Extend `ContextualPhysicalIkRuntime` and `ContextualPhysicalIkRig`, because the existing system already owns batched ground probes, PlayableGraph injection, two-bone Burst math, origin-shift rebasing, and black-box telemetry.
Rejected Alternatives: A new MonoBehaviour IK manager would duplicate scheduling and risk two owners writing the same bones. Unity Animator foot IK was rejected because the prompt requires Burst math and the mandates forbid Animator foot IK hot paths.
Scalability potential: Low disables non-XR foot IK. Middle keeps stepped 2-bone legs. High increases visual fidelity through existing muscle bulge/secondary chains. Ultra keeps all lower-body presentation active with smoother stepping.
Hardware Impact: i3/MX350 avoids an additional scheduler and keeps work in the existing batched raycast path; estimated gain versus separate manager is one fewer registry tick and zero extra command buffers.

## Decision 2: Use typed KCC velocity signal

Problem: Lower-body swim/step posture needs player velocity without a concrete `HectonPlayerMovement` dependency.
Solution: Add `KccVelocitySignal` to `PhysicsDeterminismSignals`, emitted from `PlayerKinematicsRuntime` and read as latest signal by contextual IK.
Rejected Alternatives: Polling Rigidbody from IK would couple animation to physics ownership. Reading `GlobalRegistry.Player` every tick was rejected because dependencies must be cached or signal-driven.
Scalability potential: Low consumes only the latest velocity. Middle/High/Ultra can use the same signal to add richer stride prediction without changing the producer.
Hardware Impact: A 32-entry NativeQueue lane plus latest snapshot is sub-kilobyte persistent memory and avoids per-frame scene/component lookup on i3/MX350.

## Decision 3: Visual fake over physical lower-body sim

Problem: Full lower-body physical simulation is not affordable for VR presence on MX350/i3 and would create collision authority ambiguity.
Solution: Use batched seabed rays, squared-distance step triggers, triangle-wave foot lift, and existing two-bone solver. This is a deterministic presentation fake.
Rejected Alternatives: VRIK/full-body IK, ragdoll legs, and per-joint physics were rejected as too expensive and too unstable for first-person VR.
Scalability potential: Low: disabled when non-XR. Middle: 2-bone stepped legs. High: velocity-aware swimming fallback. Ultra: existing muscle tension path can add visible overkill without changing solver authority.
Hardware Impact: Reuses existing raycast batch; added math is linear in two feet per player rig, estimated under 0.01 ms on i3/MX350 before Unity verification.

## Decision 4: Hip-origin seabed probes, foot-probe offsets preserved

Problem: The prompt requires seabed checks from the hips, but the existing rig authored left/right foot probe transforms already encode useful lateral stance.
Solution: Build two downward `RaycastCommand`s from pelvis-derived left/right hip offsets, using the authored foot probes only to estimate lateral separation and fallback on invalid transforms.
Rejected Alternatives: Direct foot-probe rays were rejected because they do not satisfy the hip-origin requirement. Voxel SDF sampling was rejected because the existing owner already batches `RaycastCommand`s and SDF access would add a new cross-domain dependency.
Scalability potential: Low non-XR does not issue leg rays. Middle/High/Ultra retain the same two command lanes and spend saved physics cost on smoother lower-body presentation.
Hardware Impact: No extra command buffers; ray count stays two foot probes per active rig. MX350 impact is bounded to existing batched physics scheduling.

## Decision 5: Runtime mirror for local compile, ASMDEF data retained

Problem: `Hecton8.Core.csproj` in this workspace does not include or generate a `Hecton8.Animation.IK.csproj`, so direct use of `Hecton8.Animation.IK.FootIKData` makes local dotnet compile fail before Unity asmdef import.
Solution: Keep the public packed `FootIKData` and constants in `Assets/_Project/Scripts/Animation/IK/LowerBodyPresenceIkJobs.cs` for the Unity asmdef boundary, and use an internal packed mirror in `ContextualPhysicalIkRuntime` to avoid a stale generated-csproj dependency.
Rejected Alternatives: Editing generated `.csproj` files was rejected as editor-churn and fragile under Unity regeneration. Moving the asmdef file into Gameplay was rejected because it violates the prompt's isolation target.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this is a compile-isolation decision only.
Hardware Impact: Mirror struct has the same packed scalar layout and no runtime allocation. Memory cost remains 256 persistent foot states for 128 IK slots.

## Decision 6: Compile wall classification

Problem: `dotnet build Hecton8.Core.csproj` fails before full verification due missing unrelated assemblies/types and duplicate `SaveManager` members.
Solution: Fix the direct new namespace failure by removing the gameplay dependency on the asmdef-only namespace, then run targeted build filtering for changed files.
Rejected Alternatives: Repairing global audio/fluid/save/ecology dependencies was rejected as outside the assigned animation domain and unsafe in a multi-agent batch.
Scalability potential: No runtime scalability effect.
Hardware Impact: No runtime effect. Verification remains PENDING because Unity MCP transport is down and full dotnet build is blocked by unrelated compile dependencies.

## Decision 7: Recursive lower-body stability upgrade

Problem: Re-verification found the hip-origin foot probes were losing authored fore/aft stance, swim fallback could treat vertical KCC velocity as forward swim direction, and stale persisted foot state could leave both feet marked as stepping.
Solution: Keep the same two `RaycastCommand` lanes, but offset them from pelvis with authored lateral plus fore/aft stance and a finite-clamped planar KCC velocity lead. Swim posture now uses planar velocity against root-up. Step triggering stores a finite-clamped velocity-scaled squared threshold and explicitly cancels one stale step if both foot lanes arrive in stepping state.
Rejected Alternatives: A gait planner, capsule foot sweeps, full-body IK, and extra ground probes were rejected because they add runtime ownership and physics cost for a first-person visual lie. Recomputing a new generated C# project reference for `Hecton8.Animation.IK` was rejected; the stale Gameplay using was removed instead.
Scalability potential: Low non-XR still disables foot IK. Middle gets stable two-foot presence. High gets velocity-predicted foot targets with reduced chatter. Ultra can spend saved cycles in existing secondary-chain/muscle presentation rather than adding solver authority.
Hardware Impact: i3/MX350 pays a few dot/lengthsq/rsqrt operations inside existing jobs and no new allocations or ray lanes. Estimated cost is <1 us per active rig for ray-origin lead and <0.5 us for step hysteresis, while avoiding expensive visual correction from foot chatter.

## Decision 8: AUP SOA and black-box hardening

Problem: The origin-shift path rebased SOA float3 lanes by skipping values close to zero. That protects inactive defaults, but it can leave a valid active target stale if the player happens to stand near world origin during a floating-origin shift. The telemetry dump also wrote ring memory in physical array order with no magic/header, making post-mortem chronology weaker.
Solution: Rebase hand SOA targets only when their IK weight is active, and rebase foot SOA targets/current positions only when their packed foot lane blend is active. This preserves inactive zero lanes and fixes active near-origin targets. The dump now writes a fixed magic, capacity, entry size, head index, reason flags, then ring entries oldest-to-newest.
Rejected Alternatives: Rebasing every SOA lane was rejected because inactive zero defaults would turn into false world positions. Leaving the dump as raw array order was rejected because a circular buffer must be readable after wrap.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged visually; this is correctness and post-mortem quality. It allows high-tier visual overkill to keep trustworthy crash data without adding hot-path cost.
Hardware Impact: Shift-only branch work. No per-frame CPU cost on i3/MX350. Dump format adds a small cold-path header and avoids wasted investigation time after a NaN fault.

## Decision 9: Distance throttle hysteresis

Problem: Contextual IK cadence switched tiers directly at 10m and 25m, so viewer movement around the threshold could flip job cadence between frames. That violates the state hysteresis rule and creates visible/CPU jitter risk.
Solution: Add a 4m hysteresis band to `ContextualPhysicalIkRig` throttle tier resolution. Tier 0 upgrades only past 14m and tier 1 returns only under 6m; tier 1 upgrades past 29m and tier 2 returns under 21m. The existing update bitmasks remain unchanged.
Rejected Alternatives: Time-accumulator hysteresis was rejected because the distance band is cheaper and satisfies the mandate without adding timers. Removing distance throttling was rejected because MX350 needs cadence control.
Scalability potential: Low holds reduced cadence without flicker. Middle keeps stable two-bone presence. High/Ultra avoid visible cadence popping while spending saved cycles on smoother secondary presentation.
Hardware Impact: One byte of persistent state and a few branch comparisons per capture. Estimated cost is <0.1 us/frame; it prevents cadence churn near thresholds.

## Decision 10: Slot lifecycle job-race guard

Problem: `ContextualPhysicalIkRuntime.RegisterRig` and `UnregisterRig` reset slot-owned NativeArrays. If a ground response job was pending, lifecycle mutation could touch the same frame/IK/foot lanes while Burst jobs were still writing them.
Solution: Add `CompletePendingGroundResponseForStructuralMutation()` and call it before slot allocation/free/reset. This is a cold lifecycle sync only; normal FastTick/LateFrame cadence still uses non-blocking completion.
Rejected Alternatives: Letting the next frame overwrite stale data was rejected because it does not protect NativeArray write ownership. Publishing a completed target swap during unregister was rejected because a disabling rig should not receive an extra buffer swap just to make structural mutation safe.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged visually. The fix keeps pooled rigs, scene transitions, and future multi-rig lower-body slots deterministic without adding hot-path cost.
Hardware Impact: No per-frame cost on i3/MX350. Enable/disable may pay one forced job completion only when it races an outstanding IK job; that is structural, not steady-state frame work.

## Decision 11: KCC velocity ownership binding

Problem: The runtime consumed the latest player `KccVelocitySignal` once and copied it into every active `ContextualPhysicalIkRig`. That contaminates future multi-rig scaling: NPCs or secondary bodies would inherit the player's foot-ray lead, step threshold allowance, and swimming posture.
Solution: Cache the KCC body AUP runtime position with the velocity signal and apply that velocity only to entity roots within 4m of the KCC body. The cached KCC body position rebases on origin shift with the IK target state. Non-matching rigs receive zero KCC velocity and keep their authored/grounded presentation stable.
Rejected Alternatives: Adding a direct `HectonPlayerMovement` or player-rig reference was rejected because the prompt requires signal migration and decoupling. A new public rig ownership API was rejected as interface churn during batch execution.
Scalability potential: Low keeps only the player rig velocity-driven. Middle/High/Ultra can register more contextual IK rigs without player-motion bleed. Future AI-specific velocity lanes can expand this without changing the lower-body solver.
Hardware Impact: One finite check and one squared-distance compare per active rig. Estimated <0.1 us/frame on i3/MX350 for the current single-rig case; avoids visible stride/swim corruption when multiple rigs enter the shared runtime.

## Decision 12: Source drift correction and fail-closed fade state

Problem: Direct source readback showed the actual runtime/rig files still contained old `sqrMagnitude` origin-shift guards and the weak normal-only `HasHit` predicate. The fade-out path also copied previous target frames wholesale, allowing a stale non-finite hand or foot target to persist while blend decayed.
Solution: Re-applied finite AUP shift rejection in runtime and rig code, including the 10km cap and runtime invalid-shift telemetry dump. Restored structural forced-completion swap/publish/log handling. `HasHit` now requires finite point, finite normal, finite distance, and non-negative distance. `FadeOutTarget`, `FadeFootLane`, and `WriteFootSoa` now fail closed to finite zero/up values before lower-body SOA lanes are written.
Rejected Alternatives: Trusting persisted status was rejected because disk source is the runtime authority. Scattered checks in each caller were rejected in favor of shared boundary predicates. Adding ray lanes, synchronous physics, or physical leg simulation was rejected as unrelated cost.
Scalability potential: Low/Middle/High/Ultra valid visuals are unchanged. Invalid physics, AUP, or stale target data now dies at the boundary instead of scaling into visible foot/hand spikes or richer high-tier secondary animation.
Hardware Impact: Hot-path cost is a few finite checks around existing hit and target writes, estimated below 1 us for the active player rig on i3/MX350. No allocations, jobs, new ray lanes, or managed references were added.

## Decision 13: Target-frame quarantine before AnimationJob exposure

Problem: The telemetry path could detect invalid target values, but hand SOA writes, skipped-frame target reuse, and cold AUP rebase paths still trusted previous-frame floats. A NaN hand target or corrupt foot blend could therefore reach `ContextualPhysicalIkApplyJob` before the black-box dump became useful.
Solution: Added a frame-level sanitizer inside `ContextualPhysicalIkGroundResponseJob` before SOA writes and target-frame publication. Hand SOA weights now drop to zero when position/blend is invalid. Foot fade/update paths sanitize previous blend before smoothing. AUP rebase clears invalid hand/foot lanes instead of subtracting shift from corrupt data. Rig capture now rejects non-finite root transforms and falls back probe positions to root when authored probe transforms are corrupt.
Rejected Alternatives: Relying on telemetry-only detection was rejected because the animation job must not consume invalid state. Adding exception/log spam was rejected as GC and runtime noise. Extra raycasts or physical validation were rejected because this is a data-boundary defect, not a simulation problem.
Scalability potential: Low/Middle/High/Ultra valid visuals are unchanged. Low-tier devices avoid catastrophic IK spikes; high-tier secondary-chain and muscle presentation receive only finite targets and can spend visual budget without amplifying invalid data.
Hardware Impact: Added branch/finite checks are linear in two hands/two feet per active rig, estimated below 1 us on i3/MX350. No allocations, no extra jobs, no extra rays, no new managed owners.

## Decision 14: AnimationJob finite H-Phi guard

Problem: Target-frame quarantine protected runtime SOA lanes, but `ContextualPhysicalIkApplyJob` still trusted stream handle reads, chain metadata, cached pose state, muscle-bulge state, and `math.saturate` on external floats. A non-finite bone pose or NaN blend could still reach two-bone, FABRIK, spine, secondary, and PlayerKinematics wall/squeeze hand production.
Solution: Added finite and handle-range validation inside the Burst animation job before pelvis, two-bone, appendage, spine, and secondary writes. Sanitized blend inputs, stiffness/damping, cached pose playback, muscle-bulge output, quaternion nlerp/axis/euler approximations, chain lengths, runtime foot/hand publication, and the PlayerKinematics hand target producer. Invalid states now skip the specific solve/write path or reset secondary state to finite pose/zero velocity.
Rejected Alternatives: Dotnet rebuild was explicitly rejected by user instruction. Logs/exceptions inside animation jobs were rejected as GC/noise. Additional rays or physical validation were rejected because this is an H-Phi data-integrity defect, not a simulation defect.
Scalability potential: Low/Middle/High/Ultra valid visuals are unchanged. Low-tier avoids animation spikes; high-tier muscle/secondary overkill no longer amplifies invalid scalar or stream data. Low keeps the cheap finite gates; Middle keeps two-bone presence; High keeps velocity/predictive polish; Ultra keeps secondary/muscle visuals without trusting corrupt inputs.
Hardware Impact: Branch/finite checks are per active IK chain and mostly inside existing AnimationJob/producer loops; estimated below 1 us for the standard player rig and no allocations/jobs/rays.

## Decision 15: KCC input-to-IK finite velocity boundary

Problem: Lower-body stride lead and swim posture depend on `KccVelocitySignal`, but `PlayerKinematicsRuntime` still allowed non-finite input move axes, vertical axis, SDF sample step, roll side-dot, roll spring state, and triangle-wave phase to survive upstream of the KCC velocity path.
Solution: Sanitized planar input to zero on non-finite values, clamped vertical input through a signed-unit helper, sanitized intended movement before storing it, clamped SDF sample step through the non-negative helper, sanitized roll side-dot/target/position/velocity, and zeroed non-finite triangle-wave phase before cheap roll wave evaluation.
Rejected Alternatives: Dotnet rebuild was explicitly rejected by user instruction. Adding input-event logging was rejected as noise and potential allocation. Replacing roll/stride prediction with a physical body solver was rejected because the lower-body system is a visual fake consuming finite KCC data.
Scalability potential: Low keeps stable cheap KCC output for disabled/non-XR lower-body IK. Middle keeps two-bone stride prediction finite. High/Ultra can use velocity lead, swim posture, haptics, and secondary IK polish without amplifying corrupt input state.
Hardware Impact: Added scalar finite checks are in existing player kinematic paths and estimated well below 1 us/frame on i3/MX350; no allocations, no new jobs, no new rays.

## Decision 16: Quaternion/raycast command finite boundary

Problem: `ContextualPhysicalIkApplyJob` rejected non-finite quaternions, but a finite zero-length stream rotation could still pass the check and reach `math.inverse`. The ground detection job also had defensive gaps where corrupt camera/probe/origin values could collapse command origins to zero or let NaN scalar inputs affect hand/foot proxy blends.
Solution: Normalized finite Unity quaternions through the shared no-sqrt path, preserved zero/invalid quaternions for explicit rejection, upgraded quaternion validators to require finite non-zero length, sanitized brace directions and camera/tool/hand/foot ray origins, sanitized foot step cache state, clamped contact offsets, max-delta heights, collision distances, and brace proxy distances before ray-response math, and kept spine target range validation self-contained inside the Burst apply job.
Rejected Alternatives: Dotnet rebuild was explicitly rejected by user instruction. Logging bad quaternions or ray inputs was rejected as GC/noise. Adding extra ray probes, a gait planner, or physical leg authority was rejected because this is an H-Phi boundary defect in the existing visual fake.
Scalability potential: Low/MX350 keeps the same two foot rays and cheap hand rays without zero-origin command pollution. Middle keeps stable two-bone lower-body presence. High gets cleaner velocity-led foot placement and hand retraction. Ultra can spend saved trust on secondary/muscle visual overkill without amplifying corrupt stream rotations.
Hardware Impact: Added work is branch/finite/length-squared checks and existing `rsqrt` quaternion normalization, estimated below 1 us/frame on i3/MX350 for the standard player rig. No allocations, no public API changes, no new jobs, no new ray lanes.

## Decision 17: Black-box telemetry cursor hardening

Problem: `PlayerKinematicsRuntime` black-box telemetry is the post-mortem source for KCC-to-IK faults, but its write cursor was read directly from a native int and modulo-indexed. If that cursor became negative or stale, the telemetry write itself could fault or skip chronology before the dump captured the bad lower-body/KCC state.
Solution: Added bounded telemetry slot reservation in the Burst body job and shared main-thread telemetry writer, clamping negative cursors to zero, rejecting missing/zero-length buffers, advancing from the wrapped index, finite-sanitizing telemetry position/velocity/intended movement payloads before black-box writes, and dumping telemetry oldest-to-newest from a sanitized wrapped head.
Rejected Alternatives: Dotnet rebuild was explicitly rejected by user instruction. Exceptions/logging on cursor corruption were rejected because telemetry is the fault path and must not allocate or cascade. A larger telemetry buffer was rejected because the existing 300-frame black box satisfies the mandate.
Scalability potential: Low/MX350 keeps the same 300-entry telemetry footprint with safer writes. Middle/High/Ultra preserve chronological KCC/IK evidence under richer hand/leg presentation without increasing hot-path memory or event lanes.
Hardware Impact: Added cost is integer bounds checks and finite vector selects only when telemetry is written, estimated below 0.5 us per telemetry event on i3/MX350. No allocations, no new native containers, no new public API.

## Decision 18: Environment IK telemetry scalar clamp

Problem: Environment IK black-box telemetry sanitized vector payloads and cursor writes, but `activeBlend` could still be non-finite when squeeze, impact, low-tier, or scrape aux flags triggered a telemetry write.
Solution: Clamp `activeBlend` through `SanitizeUnit` before aux flag selection and reuse the same safe scalar for `SolidDensity`.
Rejected Alternatives: Dotnet rebuild was explicitly rejected by user instruction. Dropping all aux-flag telemetry on invalid blend was rejected because squeeze/impact/scrape evidence can still be useful with a neutral scalar. Logging was rejected as fault-path noise.
Scalability potential: Low/MX350 keeps telemetry deterministic and cheap. Middle/High/Ultra preserve black-box quality while richer IK/haptic events are enabled.
Hardware Impact: One scalar finite/clamp operation per environment IK telemetry event, estimated below 0.1 us/event on i3/MX350. No allocations, no new containers, no new event lanes.

## OMEGA POLISH CHANGES

Problem: Final anti-bloat pass required checking the lower-body implementation for honest simulation, unbounded math, GC leaks, and out-of-domain edits.
Solution: Kept the system as a cinematic lie: hip-origin batched rays, squared-distance trigger, triangle-wave foot lift, velocity-backed swim pose, and small pelvis yaw dot product. Verified no `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, interpolation strings, `OnAnimatorIK`, or `SetIK*` in the touched lower-body/runtime signal files.
Rejected Alternatives: True gait simulation, per-joint physics, full-body spine twist, and synchronous physics queries were rejected as frame-time waste for first-person VR presence.
Scalability potential: Low: foot IK disabled outside XR. Middle: two-bone stepped feet. High: swim posture and pelvis yaw stay active. Ultra: existing muscle-bulge and secondary-chain presentation can spend saved cycles on visual overkill without new simulation authority.
Hardware Impact: No new managed hot-path allocations. Runtime leg work is two foot lanes per entity plus reused `RaycastCommand` batch. `dotnet build Hecton8.Core.csproj` remains blocked by unrelated global dependency errors; targeted changed-file filter returned no errors for `ContextualPhysicalIkRuntime`, `ContextualPhysicalIkRig`, `PlayerKinematicsRuntime`, `PhysicsDeterminismSignals`, `KccVelocitySignal`, or `ContextualPhysicalIkFootData`.

Recursive Verification Addendum: after the user requested additional patience/professional polish, `CURRENT_BATCH.md` was re-extracted. `ContextualPhysicalIkRuntime` now removes the stale Gameplay dependency on `Hecton8.Animation.IK`, preserves foot-probe fore/aft stance in hip rays, adds finite-clamped planar velocity lead, uses planar velocity for swimming posture, stores finite-clamped velocity-scaled squared step thresholds, cancels stale dual-foot step state, rebases active SOA lanes safely under AUP shift, writes ordered black-box dumps, forces a cold pending-job completion before lifecycle slot reset/allocation, binds player KCC velocity only to rigs near the KCC body AUP, and rebases the cached KCC body position on origin shift. `ContextualPhysicalIkRig` now applies a 4m distance hysteresis band to IK cadence tiers. `dotnet build Hecton8.Core.csproj --no-restore -v:quiet /m:1 /p:UseSharedCompilation=false /clp:ErrorsOnly` remains a global dependency-wall check, but the changed-file filter completed with no matching errors. Unity MCP resources are unavailable in this session. Scoped `Select-String` anti-bloat scan over touched lower-body/signal files returned no matches for `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, interpolation, `OnAnimatorIK`, `SetIK`, or `ikPass`.

Final Evidence Scope: recursive code polish and required evidence files audited in this pass:
`Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkMath.cs`
`Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs`
`Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs`
`Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs`
`Assets/_Project/Scripts/Physics/PhysicsDeterminismSignals.cs`
`Assets/_Project/Scripts/Animation/IK/LowerBodyPresenceIkJobs.cs`
`Docs/AgentLogs/Rationale_ANIM_PROCEDURAL_LEGS_IK.md`
`Docs/Tasks/Status_ANIM_PROCEDURAL_LEGS_IK.md`
`Docs/AgentLogs/LOG_ANIM_PROCEDURAL_LEGS_IK.md`
