# Rationale_CAMERA_JUICE_SYSTEM

Status: PENDING VERIFICATION

## Mandate Selection

Problem: Camera juice must be rebuilt as deterministic procedural math without singleton access, clip-driven shake, or GC in hot paths.
Solution: Apply GlobalRegistry for service discovery, EventBus-style signal consumption where existing contracts allow it, Zero-GC mandate for LateUpdate math, cinematic fake-first for screen trauma instead of physical camera simulation, performance budget for MX350/i3 constraints, crash telemetry/black-box rule for state recording, deterministic math for stable seeds, AUP mandate for local-space shake safety, and VR stencil/comfort constraints for XR paths.
Rejected Alternatives: Cinemachine impulse and AnimationClip-driven shake are too heavyweight for minor bumps and introduce hidden evaluation overhead. Direct CameraShake.Instance calls create tight dependencies and conflict with parallel agent work.
Scalability potential: Low uses 30Hz sampled noise with interpolation and no translation in VR. Middle evaluates full per-frame six-axis noise. High adds richer directional roll and FOV response. Ultra can spend saved CPU on stronger visual response and layered rotational detail without changing gameplay authority.
Hardware Impact: Target is 0 B/frame GC and less than 0.1 ms on i3/MX350. Estimated gain against clip/impulse path is 20-80 us CPU saved per impact burst, pending profiler proof.

## Initial Decisions

Problem: Batch identity ambiguity between role MOTION_ENGINEER and prompt id CAMERA_JUICE_SYSTEM.
Solution: Use CAMERA_JUICE_SYSTEM for status/log file names because prompt explicitly says Log to Status_CAMERA_JUICE_SYSTEM.md.
Rejected Alternatives: Status_MOTION_ENGINEER.md would violate the prompt-specific file name.
Scalability potential: Not runtime-affecting.
Hardware Impact: None.

Problem: Existing `GlobalSignals.ImpactSignal` is a single NativeQueue lane and `SoundscapeSystem` drains it.
Solution: Consume the multi-listener `PhysicsEvents` impact surface and `CombatDamageRuntime` resolved damage listener for camera trauma. Keep `GlobalSignals.ImpactSignal` untouched to avoid stealing audio impacts.
Rejected Alternatives: Draining `GlobalSignals.TryDequeueImpact` inside camera would create nondeterministic consumer races between camera and soundscape.
Scalability potential: Low/MX350 gets one callback and scalar accumulation only; High/Ultra can use the same signal data for stronger local roll and six-axis Perlin without extra allocations.
Hardware Impact: Avoids duplicate NativeQueue scans and prevents lost sound events. Estimated saved/avoided cost: 5-20 us/frame under impact traffic, pending profiler proof.

Problem: Registry currently exposes `CameraJuiceSystem` concrete type.
Solution: Add `ICameraJuiceSystem` contract and make the registry slot use the interface while keeping the concrete component as the runtime owner.
Rejected Alternatives: Keeping concrete access would preserve cross-domain dependencies and fail the singleton/interface purge objective.
Scalability potential: Interface slot lets future low-tier/null camera presentation service be swapped without touching producers.
Hardware Impact: Interface lookup is cold/reference-only; no frame allocation expected.

## Loop 1 Decisions

Problem: First-party systems called camera shake methods directly through the registry, but the registry now exposes only `ICameraJuiceSystem`.
Solution: Convert producers to `CameraJuiceSignals.PublishImpact(...)` and keep legacy `TriggerShake`/`TriggerSubmarineImpactShake` as compatibility bridges inside the camera runtime only.
Rejected Alternatives: Expanding `ICameraJuiceSystem` with shake methods would preserve direct producer-to-camera coupling and violate the signal migration objective. Publishing into `GlobalSignals.ImpactSignal` was rejected because `SoundscapeSystem` is already a single consumer.
Scalability potential: Low tier consumes one scalar trauma add and 30Hz noise interpolation. High and Ultra use the same packet direction for richer roll/FOV bias without producer changes.
Hardware Impact: Removes profile/list active-shake handling from hot ingress. Estimated 10-35 us saved during impact bursts on i3/MX350, pending profiler proof.

Problem: Procedural shake must run after player input/KCC without corrupting camera mouse-look rotation.
Solution: Apply local transform offsets in dispatcher `LateFrameTick`; store the last composite local rotation and remove the previous shake only when that exact composite is still present.
Rejected Alternatives: Blind inverse-removal of the last shake could inject an inverse roll when the mouse-look owner overwrote local rotation earlier in the frame. World-space shake was rejected because AUP/floating-origin shifts must not affect presentation noise.
Scalability potential: Low keeps translation cheap or disabled in VR. High/Ultra can increase rotational flavor from the same scalar without new dependencies.
Hardware Impact: Quaternion multiply/dot only, no managed allocation. Estimated sub-5 us/frame.

Problem: Hit stop needed an exact three-frame command path, but camera must not mutate Unity `Time.timeScale` directly.
Solution: Add `SystemDispatcher.RequestCoreTickDilation(float scalar, int frameCount, uint reasonHash)` and call it for severity > 0.8.
Rejected Alternatives: Calling `GlobalPhysicsStateManager.RequestKinematicHitStop` uses 0.1 seconds, not exactly 3 frames, and is tied to kinematic impact speed rather than camera trauma severity.
Scalability potential: Same frame-count scalar for all tiers; low-end sees cheap cinematic impact without extra simulation.
Hardware Impact: One scalar multiply per dispatcher frame while active. Estimated cost under 1 us/frame; visual impact bought from time dilation rather than extra simulation.

Problem: Compile verification failed before camera-specific errors could be proven.
Solution: Record dependency wall and keep changes scoped. Current blockers are missing `Hecton8.Cartography`, warning-signal structs, ballast controller type, and unrelated interface implementation drift in world/biolum systems.
Rejected Alternatives: Fixing unrelated domains would violate camera domain ownership and risk parallel-agent conflicts.
Scalability potential: Not runtime-affecting.
Hardware Impact: None.

## Loop 2 Decisions

Problem: The old active-shake List/Burst job path remained as unreachable code after the trauma rewrite.
Solution: Remove the dead job/list pipeline and keep the legacy public methods only as thin trauma bridges.
Rejected Alternatives: Leaving dormant Burst/List code would preserve maintenance debt and make future agents believe two shake authorities still exist.
Scalability potential: Low has one scalar state path. High/Ultra spend budget on richer rotational/FOV response from the same scalar rather than parallel systems.
Hardware Impact: Removes cold NativeArray shake-job buffers and list mutation risk. Estimated 8-25 us saved during shake bursts, plus lower scene native memory.

Problem: Black Box mandate requires last-frame state for critical presentation failures and NaN detection.
Solution: Add `NativeArray<CameraJuiceTelemetryEntry>[300]` circular telemetry and dump `Dump_CAMERA_JUICE_SYSTEM.bin` on invalid procedural math.
Rejected Alternatives: Debug logs are not deterministic, allocate strings, and do not preserve the last 300 frames.
Scalability potential: Low/High/Ultra use the same 64-byte frame record; cheap devices get postmortem proof without runtime string allocation.
Hardware Impact: 19.2 KB persistent native memory, one struct write per LateFrame. Estimated sub-2 us/frame.

Problem: Compile pass 2 exposed a camera-owned namespace error after dead-code removal.
Solution: Restore the `Hecton8.Optimization` import because `VRAMMonitor` still belongs to that namespace.
Rejected Alternatives: Removing adaptive VRAM pressure response would be unrelated behavior loss.
Scalability potential: Keeps VRAM pressure scaling active for MX350.
Hardware Impact: No hot-path cost change.

Problem: Final compile still fails outside the camera domain.
Solution: Mark compile as `[BLOCKED BY DEPENDENCY]` for missing cartography and player-kinematics dependency types after confirming no remaining camera-juice compiler errors are emitted.
Rejected Alternatives: Creating placeholder cartography, physics-determinism, or input types would be architectural sabotage outside the camera domain.
Scalability potential: Not runtime-affecting.
Hardware Impact: None.

## OMEGA POLISH CHANGES

Problem: The batch demanded anti-bloat proof after all 19 objectives were checked or blocked.
Solution: Re-extracted the `CAMERA_JUICE_SYSTEM` XML prompt with CLI regex, scanned the camera-owned files for banned magnitude/normalize/string-format patterns, scanned first-party scripts for direct `GlobalRegistry.CameraJuice.Trigger*`, `CameraManager.Instance`, `CameraShake.Instance`, and `CinemachineImpulseListener`, and re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`.
Rejected Alternatives: Trusting earlier scans or the IDE state was rejected because the prompt requires evidence on disk.
Scalability potential: The final path remains one scalar trauma authority with Low/MX350 30 Hz sampled noise, Middle full six-axis `noise.cnoise`, High directional roll/FOV kick, and Ultra allowed to spend saved CPU on stronger presentation without new gameplay simulation.
Hardware Impact: Hot-path audit found no `math.sqrt`, `.magnitude`, `math.normalize`, `Vector3.Normalize`, `foreach`, `string.Format`, interpolation strings, or `.ToString()` in `CameraJuiceSystem`, `CameraJuiceSignals`, or `SystemDispatcher` target scan. Estimated 0 B/frame allocation remains unmeasured by profiler.

Problem: Cinematic impact had to be visible without honest physical simulation.
Solution: Use `_trauma` as a visual fake, square it for intensity, sample six deterministic `noise.cnoise` lanes, apply local-space translation/rotation after input, inject short directional bias, and recover roll through a damped scalar spring.
Rejected Alternatives: Cinemachine impulses, AnimationClips, real camera body physics, world-space shake, and clip queues were rejected for GC risk, hidden evaluation overhead, and AUP contamination.
Scalability potential: Low disables expensive perceptual detail through 30 Hz interpolation and VR disables translation/FOV. Middle/High/Ultra scale the same scalar math into stronger rotational/FOV response.
Hardware Impact: Estimated 20-80 us saved per impact burst versus clip/impulse handling, plus 10-35 us saved by removing direct active-shake list/profile ingress. Pending profiler confirmation.

Problem: Heavy impacts required exactly three frames of freeze without global time-scale mutation.
Solution: Route severity greater than 0.8 through `SystemDispatcher.RequestCoreTickDilation(0.05f, 3, reasonHash)`.
Rejected Alternatives: `Time.timeScale` mutation and the existing 0.1 second kinematic hit-stop path were not exact-frame camera juice commands.
Scalability potential: Low-end machines get cinematic brutality from time dilation rather than added simulation; high-end machines can combine it with richer camera response.
Hardware Impact: One scalar multiply while active. Estimated under 1 us/frame.

Problem: Critical camera math must be debuggable after invalid state.
Solution: Added a fixed 300-frame `NativeArray<CameraJuiceTelemetryEntry>` circular buffer and dump path `Docs/AgentLogs/Dump_CAMERA_JUICE_SYSTEM.bin` on non-finite procedural math.
Rejected Alternatives: Runtime debug logs allocate strings and do not preserve deterministic last-frame state.
Scalability potential: Same 19.2 KB native buffer on all tiers.
Hardware Impact: One struct write per LateFrame, estimated sub-2 us/frame.

## Loop 3 Follow-up Audit - 2026-05-13

Problem: The previous camera pass left task 12 effectively soft-failed because impact trauma did not drive projection FOV kick.
Solution: Add `_impactFovKickOffset`, feed it from `AddProceduralTrauma`, apply it after locomotion/input-reclaim FOV composition, decay it with the existing Pade approach, record it in camera telemetry, and zero it in XR.
Rejected Alternatives: Leaving impact feedback as shake-only was rejected because the prompt explicitly requires a temporary FOV bump on rapid trauma. `AnimationCurve`/`CinematicMath` dependency was rejected because local Pade decay already fits the zero-GC hot path.
Scalability potential: Low/MX350 gets a cheap scalar FOV bump. Middle/High/Ultra can scale perceptual violence through adaptive FOV scale without adding simulation.
Hardware Impact: One scalar branch and one Pade decay while active. Estimated 4 us/frame worst active window, no managed allocation expected.

Problem: `CameraJuiceSignals` prewarmed its NativeQueue but did not stop a burst from exceeding that budget.
Solution: Add `EnsurePrewarmed()` for play-enable allocation and gate enqueue at `ImpactSignalCapacity` by dropping the oldest queued camera impact before accepting the newest packet.
Rejected Alternatives: Letting `NativeQueue` grow was rejected because it can allocate beyond the prewarm target during collision bursts. Dropping the newest event was rejected because camera presentation should prefer the latest impact direction.
Scalability potential: Low tier cannot be memory-spiked by collision storms. High/Ultra keep the same packet cap and can spend visual budget in the camera response, not the queue.
Hardware Impact: Prevents unbounded native queue growth. Saturation path costs one dequeue and one enqueue; normal path remains one enqueue.

Problem: Several camera hot paths were polling `GlobalRegistry` services every frame.
Solution: Cache player/submarine rigidbodies, structural grid, dynamic-resolution scaler, VRAM monitor, scalability tier, and tick dispatcher through `TryResolveGameplayDependencies()`; refresh on the existing SlowTick cadence.
Rejected Alternatives: Per-frame registry reads in speed/FOV/adaptive-budget math were rejected by the GlobalRegistry mandate. Caching only at Awake was rejected because submarine and scaler services can register later.
Scalability potential: Low tier removes small but repeated lookup overhead. High/Ultra keep adaptive render/FOV response through cached services.
Hardware Impact: Estimated 1-6 us/frame avoided on MX350 in active camera update, pending profiler proof.

Problem: User explicitly prohibited `dotnet build` during this follow-up pass.
Solution: Verification used static scans and `git diff --check` only. No compile/build process was launched.
Rejected Alternatives: Running the prior compile command would violate the latest user instruction.
Scalability potential: Not runtime-affecting.
Hardware Impact: None.
