# Rationale: PLAYER_IK_ENVIRONMENT_ADAPTER

## 2026-05-13 Session Start
Problem: Existing player IK stack contains a broad `ContextualPhysicalIkRuntime` and a smaller `PlayerKinematicsRuntime` hand probe path, but the current prompt requires explicit environment brace adaptation from player kinematics plus signal-driven squeeze/stress coupling.
Solution: Keep ownership in `PlayerKinematicsRuntime`, feed the existing `ContextualPhysicalIkRig.ApplyExternalWallHandTargets`, and add only typed signal-lane contract surface where the requested `PlayerStateSignal(Squeezing)` does not exist.
Rejected Alternatives: A new singleton IK manager, direct reference to `SDF_TRAVERSAL_KINEMATICS`, `Animator.SetIKPosition`, `OnAnimatorIK`, and controller YAML mutation were rejected as either absent, coupled, or non-deterministic.
Scalability potential: Low uses one central chest ray and generic brace/squeeze fake; Middle uses side probes; High uses side/head/knee probes plus smoother breathing; Ultra keeps richest brace jitter and overkill polish through saved cycles.
Hardware Impact: MX350/i3 keeps ray count to 1 on low-tier path and avoids heap allocation. Expected saving versus four sync raycasts is approximately 60-140us on weak CPU under wall-probe load; exact profiler proof pending.

## 2026-05-13 Loop 1: Ownership, Contracts, Probes
Problem: The prompt demands environment bracing without creating a new singleton or coupling to traversal code that may be owned by another concurrent agent.
Solution: Extended `PlayerKinematicsRuntime` as the owner. It consumes `HighSpeedImpactSignal` and `PlayerStateSignal` through typed SignalBus snapshots, stores the latest impact target in runtime coordinates, and schedules a preallocated `RaycastCommand` batch from `FastTick`.
Rejected Alternatives: A standalone `EnvironmentIkManager`, direct `SDF_TRAVERSAL_KINEMATICS` reference, and sync `Physics.Raycast` were rejected. They either create direct dependencies, allocate pressure, or move execution out of the mandated simulation lane.
Scalability potential: Low uses one central chest command and generic brace/squeeze target; Middle/High use shoulder/head/knee probes; Ultra spends saved CPU on stress jitter and richer breathing offsets.
Hardware Impact: Low-tier probe cadence is one command every four salted FastTicks. Estimated weak-CPU saving versus four synchronous raycasts is 60-140us under contact-heavy movement. Exact profiler capture blocked by unrelated compile wall.

Problem: The requested `Hecton8.Animation.IK -> Contracts` asmdef split cannot be completed safely while the active files are still compiled under the shared project and other agents are modifying contract lanes.
Solution: Kept the cross-domain surface as a typed `PlayerStateSignal` contract in `GlobalSignals`, then marked full asmdef migration blocked by dependency ownership.
Rejected Alternatives: Moving live gameplay and IK files into a new assembly during the batch was rejected because it would change reference topology for unrelated systems.
Scalability potential: Runtime cost is zero; the contract lane remains available for later asmdef extraction.
Hardware Impact: 0us runtime. Avoids a high-risk project-wide recompilation break.

## 2026-05-13 Loop 2: Brace, Squeeze, Breathing
Problem: A valid wall hit must not hard-snap the hands, and squeezing must visibly reduce arm profile without Animator states.
Solution: The Burst placement job builds brace targets from shoulder/head/knee hits, adds a normal offset, merges squeeze targets forward/inward, and writes flags. The main thread smooths raw targets through persistent `_smoothedHandTargets`; `ContextualPhysicalIkRig` uses the squeeze flag to bias arm pole offsets inward.
Rejected Alternatives: One-frame target assignment, `Animator.SetIKPosition`, and per-frame new target objects were rejected. Standard Animator IK hides execution order and does not satisfy zero-GC/braced collision needs.
Scalability potential: Low uses one chest target and generic inward hand pose. Middle uses side hits. High/Ultra preserve side/head/knee fallback and deterministic stress jitter for hands.
Hardware Impact: Two target smoothing passes and two haptic/audio edge checks are estimated at 4-12us. Burst hit resolution is estimated at 6-18us over 4 hits; low tier is lower by cadence and ray count.

Problem: Body posture needed a stress-visible life signal, but a simulated respiratory model would waste frame time.
Solution: `ContextualPhysicalIkRig` adds a fake breathing wave to spine targets. Low tier uses triangle wave; high tier uses sine and stress jitter. `PlayerStressSignal` changes the rate and jitter amplitude.
Rejected Alternatives: A physiology simulation and authored looping breathing clip were rejected. The clip would not couple cleanly to stress, and the simulation would spend CPU on non-gameplay physiology.
Scalability potential: Low = subtle triangle offset. Middle = full offset without extra jitter. High = sine plus jitter. Ultra = same math with richer visual output through existing IK stack.
Hardware Impact: Expected cost is <8us in rig capture on low-end silicon; no heap allocation and no extra transforms.

## 2026-05-13 Loop 3: AUP, LOD, Telemetry
Problem: Cached wall targets become catastrophic after origin shifts if not rebased immediately.
Solution: `OnOriginShift` now rebases raw hand targets, smoothed targets, cached probe source position, and the cached high-speed impact brace point. Telemetry entries were already rebased by the existing kinematics path.
Rejected Alternatives: Waiting for the next raycast or fading through the origin shift was rejected; it would visibly stretch arms across the rebase.
Scalability potential: Same behavior across Low/Middle/High/Ultra because rebase safety is non-negotiable.
Hardware Impact: 0us steady state; O(2 targets + telemetry ring) only on origin shift.

Problem: Blackbox evidence needed an agent-owned dump artifact, not just the existing physics determinism file.
Solution: Fault dumping now writes the shared physics dump and `Docs/AgentLogs/Dump_PLAYER_IK_ENVIRONMENT_ADAPTER.bin` with the same 300-frame telemetry ring and IK aux flags.
Rejected Alternatives: `Debug.Log` diagnosis and relying only on `Dump_PHYSICS_DETERMINISM_SYNC.bin` were rejected. The prompt requires agent-specific postmortem evidence.
Scalability potential: Low tier gets the same postmortem fidelity as High/Ultra. Runtime cost is paid only during active IK telemetry writes and fault dumps.
Hardware Impact: Active IK telemetry is one ring write; fault dump is cold-path disk IO only.

## 2026-05-13 Loop 4: Haptics, Audio, Compile Wall
Problem: Brace contact needs feedback without creating hardware/audio dependencies inside kinematics.
Solution: `PlayerKinematicsRuntime` emits typed `HapticRequest(ChannelLightThud)` only on the brace activation edge and typed `AcousticPingSignal(ChannelGloveScrape)` only while a recent wall-slide contact exists. Both are cooldown-gated.
Rejected Alternatives: Direct haptic device calls and `AudioSource.PlayOneShot` were rejected because gameplay should publish signals, not own device/audio emitters.
Scalability potential: Low tier still gets a subtle thud/scrape; High/Ultra can route the same signal to richer haptic/audio layers without changing kinematics.
Hardware Impact: Edge event cost is estimated below 3us for haptic and below 5us for scrape signal emission, excluding downstream consumers.

Problem: The prompt asks for Burst compile proof, but the project compile gate is currently broken outside this lane.
Solution: Validated all touched scripts with Unity MCP at 0 diagnostics and marked full Burst compile proof blocked by dependency rather than claiming success.
Rejected Alternatives: Faking a green compile report was rejected. Full `dotnet build Hecton8.Core.csproj` currently fails with unrelated missing namespaces/types and Unity console duplicate-member errors in `HectonFluidEngine.cs`.
Scalability potential: None runtime; this is verification integrity.
Hardware Impact: 0us runtime.

## 2026-05-13 Loop 5: Smoothness Re-Verification
Problem: The prompt explicitly forbids one-frame hand pops after Tasks 1-18.
Solution: Re-read the prompt and verified the active wall/squeeze path: raw hit targets go into `_handTargets`, then `SmoothHandTarget` filters position, normal, elbow pole direction, elbow cosine, and blend into `_smoothedHandTargets`; the rig then applies its existing predictive repair blend smoothing.
Rejected Alternatives: Direct assignment to rig targets without an intermediate smooth buffer was rejected because the first ray hit after a wall appears can jump several centimeters.
Scalability potential: Low tier gets the same no-snap blend using one generic chest target. High/Ultra preserve richer target sources while sharing the same smoothing law.
Hardware Impact: Two target smooth passes per hand placement completion, estimated 4-12us; no hot-path heap allocation.

## OMEGA POLISH CHANGES
Problem: Final anti-bloat pass required proof that the implementation did not keep honest/expensive math, managed hot-path churn, or fake verification.
Solution: Re-read `<POLISH_MANDATE id="OMEGA_POLISH">`, converted the Burst hand-placement job from separate byte booleans to `RuntimeFlags` bitmasks, confirmed hit-distance divisions use `math.rcp`, confirmed hand target length uses `rsqrt`, and preserved low-tier triangle-wave breathing while keeping `math.sin` only on non-low tiers.
Rejected Alternatives: Leaving separate job booleans and claiming full Burst compile despite the global assembly wall were rejected. A LUT for breathing was rejected because low tier already uses triangle wave and high-tier sine is visually intentional overkill.
Scalability potential: Low = one chest ray, triangle breathing, generic brace. Middle = side probes and smooth targets. High = four probes with sine breathing. Ultra = stress jitter and downstream signal consumers can over-deliver visuals/audio/haptics using the same cheap contract signals.
Hardware Impact: Polish change is instruction-level: one job byte bitmask instead of two independent state fields. No new allocations. Estimated direct gain is sub-1us but reduces branch/state surface in Burst.

Cinematic Cheats Used:
- Triangle wave breathing on low tier instead of sine.
- Generic central chest ray brace on low tier instead of four physical hand contacts.
- Stress jitter is deterministic triangle-wave visual noise, not physiology simulation.
- Squeeze profile is a hand/pole target lie, not arm/body collision solving.
- Impact fallback reuses the CCD contact point instead of resimulating impact response.

Final Git Diff Evidence:
- `git diff --stat -- Assets/_Project/Scripts/Core/GlobalSignals.cs Assets/_Project/Scripts/Gameplay/HectonPlayerState.cs Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRuntime.cs`
- Stat: 5 files changed, 1479 insertions(+), 82 deletions(-).
- Numstat: `GlobalSignals.cs 363/1`, `ContextualPhysicalIkRig.cs 141/5`, `ContextualPhysicalIkRuntime.cs 6/6`, `HectonPlayerState.cs 5/0`, `PlayerKinematicsRuntime.cs 964/70`.
- Note: this workspace is shared with other concurrent agents; `GlobalSignals.cs` and `PlayerKinematicsRuntime.cs` already contained adjacent in-flight edits. I integrated with them and did not revert them.

Verification:
- Unity MCP validate: `PlayerKinematicsRuntime.cs` 0 errors after polish; `ContextualPhysicalIkRig.cs` 0 errors after retry; `ContextualPhysicalIkRuntime.cs` 0 errors. `GlobalSignals.cs` validated earlier with 0 errors, then later MCP regex validation timed out without a source diagnostic.
- `git diff --check` on touched script/docs scope returned exit 0 with only line-ending warnings.
- Diff-only anti-bloat scan found no added `math.sqrt`, unconditional `math.normalize`, managed `foreach`, `string.Format`, `.ToString()`, or interpolated strings in touched files.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` remains blocked by unrelated project-wide missing namespaces/types. Unity console currently reports external `HectonFluidEngine.cs` / `SystemID.Fluid` errors plus an MCP regex timeout log.
- Status remains `PENDING VERIFICATION`, not green.

## 2026-05-13 Loop 6: Professional Recheck / Phase Correction
Problem: `ContextualPhysicalIkRuntime` was still capturing IK entity state through the normal update lane while `PlayerKinematicsRuntime` produces environment brace state in `FastTick`. That leaves the contextual IK capture one dispatcher phase behind the player kinematics path.
Solution: Moved `ContextualPhysicalIkRuntime` from `IUpdatable` registration to `IFastTickable` registration in the Player lane. The runtime now captures/schedules after all normal update kinematics, and its job completion still happens inside the dispatcher LateFrame swap window.
Rejected Alternatives: Completing the player hand probe/placement jobs from `FastTick` was rejected because `DispatcherJobSwap` marks non-forced completion outside swap windows as illegal. Moving contextual IK back to the UI lane was rejected because the prompt requires simulation-phase IK, not UI-lane presentation.
Scalability potential: Low/Middle/High/Ultra all keep the same ray count and math LODs; only the phase changes. High-end machines still spend saved time on richer IK response, while weak devices avoid extra work.
Hardware Impact: 0us additional work. The change removes a phase of visible latency without adding jobs, raycasts, or allocations.

Problem: The first squeeze-pole implementation used fixed `+X/-X` offsets for left/right arms. That is too brittle for rigs with mirrored or nonstandard arm parent axes and can push elbows outward on some skeletons.
Solution: Resolve the inward pole shift from each arm's cached base lateral pole offset, pull toward local X centerline, and clamp the shift to 75% of the base lateral distance. Fallback side sign only applies when the authored lateral offset is effectively zero.
Rejected Alternatives: A full arm collision solver, direct use of SDF traversal internals, and unbounded fixed side signs were rejected. The visual lie is cheaper and more robust.
Scalability potential: Low tier gets the same inward silhouette correction with two scalar clamps. High/Ultra get the cleaner pose without any extra raycasts.
Hardware Impact: Two `abs/sign/min` scalar operations per capture when squeeze blend is active; estimated <1us on i3/MX350 and no heap allocation.

Verification:
- Unity MCP `validate_script` passed 0 errors for `ContextualPhysicalIkRuntime.cs`, `ContextualPhysicalIkRig.cs`, and `PlayerKinematicsRuntime.cs` after the recheck edits.
- `git diff --check` passed on the two newly touched scripts with only line-ending warnings.
- Diff-only anti-bloat scan found no added `math.sqrt`, unconditional `math.normalize`, managed `foreach`, `string.Format`, `.ToString()`, or interpolated strings in the new diff.
- A script refresh/compile request was triggered through Unity MCP, but Unity timed out after 60s waiting for readiness. Unity console read is still unavailable because the MCP session stopped answering `read_console`; global compile remains dependency-blocked, not green.

## 2026-05-13 Loop 7: Latency And Payload Cut
Problem: Brace probe and placement jobs were only drained from `LateFrameTick`. That means completed ray/placement work could sit until LateFrame before being passed into the rig, increasing visible response latency.
Solution: Added `PumpHandEnvironmentJobs(forceComplete, allowFinalizeOutsideSwap)`. `FastTick` now uses `DispatcherJobSwap.TryFinalizeCompleted` to consume only already-finished jobs without blocking or entering an illegal swap window. LateFrame and teardown keep the existing `TryComplete` path.
Rejected Alternatives: Blocking `FastTick` on raycasts, forcing job completion outside swap windows, or moving the raycast system to synchronous physics were rejected. They would trade responsiveness for frame spikes or violate dispatcher ownership rules.
Scalability potential: Low tier benefits most because its single-ray cadence can now hand off finished results earlier. High/Ultra keep four-probe richness but avoid one extra idle phase when jobs finish early.
Hardware Impact: Non-blocking `IsCompleted` checks add sub-1us overhead. When jobs are already done, target application happens earlier with no additional raycasts or allocations.

Problem: `PlayerKinematicsHandTarget` still carried `ElbowPoleDirection` and `ElbowCosine`, but the rig never consumed either field. `ElbowCosine` also paid per-target reach math that did not affect the animation job.
Solution: Removed both fields, deleted `ResolveElbowCosine`, and removed smoothing for dead elbow data. Squeeze pole control remains in `ContextualPhysicalIkRig` where `ContextualPhysicalIkApplyJob` actually reads `TwoBoneSetups`.
Rejected Alternatives: Keeping the fields for speculative future use was rejected as dead payload. Wiring the per-target pole direction into the animation job was rejected because the rig already owns stable pole offsets and the prompt needs a cheap silhouette lie, not a new per-frame pole channel.
Scalability potential: Low/Middle/High/Ultra all reduce target payload size and Burst work. High-end visual output is unchanged because the consumed rig pole bias remains.
Hardware Impact: Saves one cosine-law helper per generated target plus two smoothing lanes per hand. Estimated weak-CPU saving is 1-4us during active brace/squeeze frames; native target payload shrinks by two fields.

Verification:
- `rg` found no remaining `ElbowPoleDirection`, `ElbowCosine`, or `ResolveElbowCosine` references under `Assets/_Project/Scripts`.
- Unity MCP validation is unavailable because there is no active Unity session.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` still fails on existing global missing namespaces/types outside this lane.
- `git diff --check` passed with line-ending warnings only.
- Diff-only anti-bloat scan found no added `math.sqrt`, unconditional `math.normalize`, managed `foreach`, `string.Format`, `.ToString()`, or interpolated strings in the touched script diff.

## 2026-05-13 Loop 8: Payload Tail Cut
Problem: After removing per-target elbow cosine calculation, `PlayerKinematicsHandPlacementJob` still carried `UpperArmLength`, `LowerArmLength`, and the owner still assigned `ArmSegmentLength`. Those scalars no longer had a consumer.
Solution: Removed the dead job fields and the dead owner constant/assignments. The hand placement job now carries only data used by wall target resolution, squeeze, stress jitter, and feedback.
Rejected Alternatives: Keeping the scalar tail for future elbow math was rejected because the current rig reads pole control through `ContextualPhysicalIkRig` setup data. Dead payload is not acceptable in the hot path.
Scalability potential: Low/Middle/High/Ultra all get a smaller job payload. Visual output is unchanged.
Hardware Impact: Estimated gain is sub-1us per hand placement job; the larger value is reduced cache/register pressure on weak CPUs.

Verification:
- `rg` found no remaining `UpperArmLength`, `LowerArmLength`, or `ArmSegmentLength` references in `PlayerKinematicsRuntime.cs`.
- Unity MCP `validate_script` passed 0 errors for `PlayerKinematicsRuntime.cs`, `HectonPlayerState.cs`, `ContextualPhysicalIkRuntime.cs`, and `ContextualPhysicalIkRig.cs`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` still exits 1 on unrelated global missing namespaces/types, but reports no errors in the touched files.
- Unity console currently reports an external duplicate `OnGlobalRegistryServiceReplaced` in `SuitHUDV4CanvasOverlay.cs`, not this lane.

## 2026-05-13 Loop 9: Domain Boundary And Phase Audit
Problem: `PlayerKinematicsRuntime.DumpFaultTelemetryIfNeeded` wrote `Dump_SDF_TRAVERSAL_KINEMATICS.bin` in addition to the shared physics dump and this lane's player IK dump. That crosses the assigned domain boundary because the SDF traversal agent already owns and documents that artifact.
Solution: Removed only the SDF traversal dump write from this lane. The fault path still writes `Dump_PHYSICS_DETERMINISM_SYNC.bin` for shared determinism evidence and `Dump_PLAYER_IK_ENVIRONMENT_ADAPTER.bin` for brace/squeeze telemetry.
Rejected Alternatives: Leaving the redundant write was rejected because it creates two owners for one postmortem file. Deleting the shared physics dump was rejected because existing determinism telemetry uses that artifact as cross-system crash evidence.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; this is cold fault-path ownership cleanup. Weak hardware keeps the same hot-path cost and high-tier visual output is unchanged.
Hardware Impact: 0us steady-state. Fault-path disk IO is reduced by one binary file write when `_faultFlags[0] != 0`.

Problem: Moving contextual IK capture into the Player fast lane raised an ordering question because `SystemDispatcher` iterates each lane from tail to head.
Solution: Verified the normal bootstrap path creates `ContextualPhysicalIkRuntime` before player layer nodes; later `PlayerKinematicsRuntime` registration therefore runs first in the reverse-order Player fast lane. The alternate rig-created path may be one FastTick late, but does not block the frame and keeps smoothed target continuity.
Rejected Alternatives: Forcing same-frame completion in `FastTick` was rejected by the job-swap mandate. Moving IK capture to UI or LateFrame presentation was rejected because the prompt requires simulation-phase IK before rendering.
Scalability potential: Low keeps one central ray on cadence. Middle/High/Ultra keep four probes and richer breathing without an extra synchronization point.
Hardware Impact: 0us additional work. Avoids a forced job wait that could cost hundreds of microseconds on i3/MX350 under PhysX load.

Verification:
- Unity MCP `validate_script` passed 0 errors for `PlayerKinematicsRuntime.cs`, `ContextualPhysicalIkRig.cs`, `ContextualPhysicalIkRuntime.cs`, and `HectonPlayerState.cs`.
- `GlobalSignals.cs` validation still times out inside the MCP regex validator; no C# diagnostic was returned.
- Static scan found no owned `Physics.Raycast`, `RaycastAll`, `OverlapSphere`, Animator IK calls, coroutines, hot string formatting, scene Find calls, or message sends in the touched files.
- `git diff --check` passed with only line-ending warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` still exits 1 on unrelated global missing namespaces/types and reports `NO_TOUCHED_FILE_ERRORS_REPORTED`.
- Unity console read is unavailable because the MCP session is not answering pings.

## 2026-05-13 Loop 10: Squeeze Flag Fade Hygiene
Problem: `SmoothHandTarget` preserved the previous smoothed target flags when the raw target had no hit. During fade-out, stale `FlagSqueeze` could keep refreshing `ContextualPhysicalIkRig` squeeze-pole hold after the raw squeeze target stopped.
Solution: Copy `rawTarget.Flags` in the no-hit branch before writing the smoothed target. This keeps positional/blend fade-out smooth while ending squeeze-pole intent as soon as the source target clears.
Rejected Alternatives: Clearing the entire smoothed target immediately was rejected because that reintroduces hand pops. Leaving stale flags was rejected because it extends squeeze posture beyond the signal's lifetime.
Scalability potential: Low/Middle/High/Ultra visual smoothness is unchanged. The pose state now decays according to the existing hold timer rather than an accidental stale flag.
Hardware Impact: One byte assignment during target smoothing, estimated below measurement noise. No allocation, no ray count change, no added job.

Verification:
- Unity MCP `validate_script` passed 0 errors for `PlayerKinematicsRuntime.cs`.
- Static scan found no owned sync raycasts, Animator IK calls, coroutines, hot string formatting, scene Find calls, or message sends in touched files.
- `git diff --check` passed for `PlayerKinematicsRuntime.cs` with only line-ending warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` still exits 1 on unrelated global dependency errors and reports `NO_TOUCHED_FILE_ERRORS_REPORTED`.

## 2026-05-13 Loop 11: Low-Tier External Brace Gate
Problem: `ContextualPhysicalIkRig` defaults `disableWallTouchOnLowTier` to true. That correctly suppresses the rig's own expensive wall-touch raycasts on weak hardware, but it also cleared external hand brace targets from `PlayerKinematicsRuntime`, breaking the prompt's required low-tier generic brace.
Solution: Let `ApplyExternalWallHandTargetsToPredictiveLatch` continue when external wall-hand hold timers are active, even if `wallTouchEnabled` is false. The entity state still reports `EnableWallTouch = 0` on low tier, so the contextual rig does not schedule its own additional hand rays; only the cheap external player kinematics target is applied through the predictive latch.
Rejected Alternatives: Turning `disableWallTouchOnLowTier` off by default was rejected because it would re-enable extra rig raycasts on MX350. Duplicating a separate low-tier hand pose path was rejected because the existing predictive latch already blends world targets smoothly.
Scalability potential: Low = one player kinematics chest ray and external predictive latch only. Middle/High/Ultra = full contextual wall-touch probes plus richer player kinematics bracing. This preserves the prompt's hard split between toaster approximation and high-tier visual overkill.
Hardware Impact: 0 new raycasts on low tier. Added two timer comparisons in rig capture, estimated below measurement noise. Avoids the previous failure mode where low-tier saved ray cost but lost visible brace immersion.

Verification:
- Unity MCP `validate_script` passed 0 errors for `ContextualPhysicalIkRig.cs`.
- Static scan found no owned sync raycasts, Animator IK calls, coroutines, hot string formatting, scene Find calls, or message sends in touched files.
- `git diff --check` passed for `ContextualPhysicalIkRig.cs` and `PlayerKinematicsRuntime.cs` with only line-ending warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` still exits 1 on unrelated global dependency errors and reports `NO_TOUCHED_FILE_ERRORS_REPORTED`.

## 2026-05-13 Loop 12: True One-Ray Low-Tier Scheduling
Problem: The low-tier player brace path visually used only the central chest probe, but `RaycastCommand.ScheduleBatch` still received the full four-command native arrays with three disabled commands. That preserved correctness, but it did not strictly satisfy the prompt's "reduce the 4 rays to 1 central chest ray" budget on MX350.
Solution: Schedule subarrays with `GetSubArray(0, scheduledProbeCount)`. Low tier submits one command and one hit slot; Middle/High/Ultra submit all four. The spare native storage remains preallocated for high-tier reuse, but it is no longer part of the low-tier PhysX batch.
Rejected Alternatives: Keeping disabled spare commands was rejected because the scheduler still sees four entries. Allocating separate low-tier native arrays was rejected because it adds persistent memory and ownership surface for a slice that `NativeArray.GetSubArray` already handles as a zero-GC alias. Switching to sync `Physics.Raycast` was rejected by the physics and IK mandates.
Scalability potential: Low = one central chest ray on salted cadence plus generic brace. Middle = side/head/knee probes. High/Ultra = four-probe brace with stress jitter and richer breathing; saved weak-device cycles remain available for the visible brace fake instead of dead command overhead.
Hardware Impact: Low-tier PhysX command count drops from four scheduled entries to one scheduled entry on active cadence frames. Static estimate: saves low single-digit microseconds per probe frame on i3/MX350, with larger value under PhysX worker contention. No managed allocation, no API coupling, no visual regression.

Verification:
- Unity MCP `validate_script` passed 0 errors for `PlayerKinematicsRuntime.cs`.
- Static scan found no owned sync raycasts, Animator IK calls, coroutines, hot string formatting, scene Find calls, or message sends in touched files.
- Diff-only anti-bloat scan found no added `foreach`, `math.sqrt`, `math.normalize`, hot string formatting, or forbidden physics/API patterns.
- `git diff --check` passed with line-ending warnings only.
- Unity console currently reports external `GlobalDataVault.cs` / missing `MemoryAddressShiftSignal` and `Hecton8.Core.GlobalRegistry/GlobalSignals` errors, not this lane.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` still exits 1 on unrelated global dependency errors and reports `NO_TOUCHED_FILE_ERRORS_REPORTED`.
