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

## OMEGA POLISH CHANGES

Problem: Final anti-bloat pass required checking the lower-body implementation for honest simulation, unbounded math, GC leaks, and out-of-domain edits.
Solution: Kept the system as a cinematic lie: hip-origin batched rays, squared-distance trigger, triangle-wave foot lift, velocity-backed swim pose, and small pelvis yaw dot product. Verified no `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, interpolation strings, `OnAnimatorIK`, or `SetIK*` in the touched lower-body/runtime signal files.
Rejected Alternatives: True gait simulation, per-joint physics, full-body spine twist, and synchronous physics queries were rejected as frame-time waste for first-person VR presence.
Scalability potential: Low: foot IK disabled outside XR. Middle: two-bone stepped feet. High: swim posture and pelvis yaw stay active. Ultra: existing muscle-bulge and secondary-chain presentation can spend saved cycles on visual overkill without new simulation authority.
Hardware Impact: No new managed hot-path allocations. Runtime leg work is two foot lanes per entity plus reused `RaycastCommand` batch. `dotnet build Hecton8.Core.csproj` remains blocked by unrelated global dependency errors; targeted changed-file filter returned no errors for `ContextualPhysicalIkRuntime`, `ContextualPhysicalIkRig`, `PlayerKinematicsRuntime`, `PhysicsDeterminismSignals`, `KccVelocitySignal`, or `ContextualPhysicalIkFootData`.

Recursive Verification Addendum: after the user requested additional patience/professional polish, `CURRENT_BATCH.md` was re-extracted. `ContextualPhysicalIkRuntime` now removes the stale Gameplay dependency on `Hecton8.Animation.IK`, preserves foot-probe fore/aft stance in hip rays, adds finite-clamped planar velocity lead, uses planar velocity for swimming posture, stores finite-clamped velocity-scaled squared step thresholds, cancels stale dual-foot step state, rebases active SOA lanes safely under AUP shift, writes ordered black-box dumps, and forces a cold pending-job completion before lifecycle slot reset/allocation. `ContextualPhysicalIkRig` now applies a 4m distance hysteresis band to IK cadence tiers. `dotnet build Hecton8.Core.csproj --no-restore -v:quiet /m:1 /p:UseSharedCompilation=false /clp:ErrorsOnly` remains a global dependency-wall check, but the changed-file filter completed with no matching errors. Unity MCP resources are unavailable in this session. Scoped `Select-String` anti-bloat scan over touched lower-body/signal files returned no matches for `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, interpolation, `OnAnimatorIK`, `SetIK`, or `ikPass`.

Final Git Diff: active diff now includes the recursive code polish and required evidence files:
`Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs`
`Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs`
`Docs/AgentLogs/Rationale_ANIM_PROCEDURAL_LEGS_IK.md`
`Docs/Tasks/Status_ANIM_PROCEDURAL_LEGS_IK.md`
`Docs/AgentLogs/LOG_ANIM_PROCEDURAL_LEGS_IK.md` (new)
