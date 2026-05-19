# Rationale_SHINOBU_126

Agent: SHINOBU_126
Role: VR_SOMATIC_COMFORT_ENGINEER
Status: PENDING VERIFICATION / COMPILE BLOCKED BY CPU GUARD

## Decision 01 - Missing Live XML Prompt

Problem: `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="SHINOBU_126">`; CLI extraction and `rg` search found no active block.
Solution: Treat the user-provided assignment as a single explicit runtime task and log XML absence as batch drift. Domain is bounded by Echelon 4 item 39: VR Somatic Comfort.
Rejected Alternatives: Reading archived SOMATIC prompts would violate batch hygiene; inventing a 20-task XML count would be false reporting.
Scalability potential: Low/Middle/High/Ultra comfort logic must consume continuous `GlobalQualityWeight`, not tier booleans.
Hardware Impact: Avoiding archive-driven scope creep protects MX350/i3 frame budget; target runtime hot-path cost remains below 0.1 ms and 0 B GC.

## Decision 02 - Mandate Set

Problem: VR comfort touches player kinematics, presentation, XR masking/foveation, telemetry, and global execution phase rules.
Solution: Use zero-GC, physics integrity, foveated simulation, VR stencil masking, global registry DI, execution phases, crash telemetry, and AUP determinism mandates as active constraints.
Rejected Alternatives: Treating FOV narrowing and horizon lock as camera MonoBehaviour polish would couple to render camera properties and violate the user's camera-independent math requirement.
Scalability potential: Low uses cheap scalar gating; Middle increases smoothing precision; High/Ultra add denser telemetry and smoother visual output while preserving the same deterministic math.
Hardware Impact: Continuous scalar comfort output lets low-end silicon buy vestibular stability with minimal ALU; high-end hardware spends saved cycles on smoother presentation rather than more simulation truth.

## Decision 03 - KCC Angular Acceleration Source

Problem: Existing VR comfort computed head angular acceleration from HMD rotation; the assignment requires KCC angular acceleration and camera-independent math.
Solution: Consume the non-destructive `SignalBus<KccVelocitySignal>.GetFrameSnapshot()` view, derive signed planar yaw delta with `atan2(cross, dot)`, divide by KCC signal frame delta, clamp angular velocity to 16 rad/s and acceleration to 240 rad/s^2, then drive comfort scalars from acceleration magnitude.
Rejected Alternatives: Reading `Camera.main`, HMD transform yaw, or KCC GameObject rotation would couple math to camera/body presentation and add fragile cross-domain dependencies.
Scalability potential: Low lowers acceleration thresholds and raises assist through continuous `GlobalQualityWeight`; Middle keeps stock smoothing; High/Ultra tolerate sharper motion before visual clamp and spend budget on smoother presentation.
Hardware Impact: Runtime math is two float2 operations, one atan2, clamps, and lerps per new KCC signal. Estimated hot-path cost: 3-8 us on i3/MX350 class CPU, 0 B GC. Avoided camera lookup/property path estimate: 10-40 us per frame and unknown XR camera side effects.

## Decision 04 - FOV And Horizon Output

Problem: FOV narrowing and horizon stabilization must react to body-turn acceleration without mutating camera FOV or depending on camera component state.
Solution: Feed KCC vignette into existing `_VRComfortVignette`, publish `_HectonVRComfortKccState`, and pass `KccHorizonLock01` into `VRSomaticRootSyncJob` to lower the horizon correction threshold during sharp KCC turns.
Rejected Alternatives: Setting `Camera.fieldOfView`, adding a per-camera post effect component, or binary low/ultra quality branches. Those violate camera independence and scalability rules.
Scalability potential: Low: early tunnel, stronger horizon lock. Middle: default continuous smoothing. High: delayed tunnel, lower opacity. Ultra: same deterministic input, more visual headroom for downstream shaders.
Hardware Impact: Added root job work is scalar math only. Estimated added job cost: 1-3 us on i3/MX350. Saved cycles versus physical vestibular/camera inertia simulation: estimated 35-80 us and zero transform hierarchy churn.

## Decision 05 - Black Box ABI

Problem: Crash analysis needed KCC comfort state, but the old 64-byte blackbox entry only stored head speed and generic vignette.
Solution: Bump blackbox version to 3, keep 300-frame circular `NativeArray`, expand entry to explicit 128 bytes, and dump KCC angular velocity, angular acceleration, KCC vignette, horizon lock, signal sequence, signal frame, and signal source id to `Docs/AgentLogs/Dump_SHINOBU_126.bin`.
Rejected Alternatives: Text logs in hot path, managed lists, or changing public `VRSomaticSnapshot`; all create allocation risk or downstream ABI churn.
Scalability potential: Low/Middle/High/Ultra share the same telemetry schema; higher devices can interpret denser visual state without changing runtime contracts.
Hardware Impact: Memory grows from 19.2 KB to 38.4 KB for 300 frames, a fixed +19.2 KB. Runtime write remains one struct assignment; estimated added cost below 2 us, 0 B GC.

## Decision 06 - Verification Guard

Problem: Compile verification is required, but current guard measured `_Total Processor Time` at 100% five times. User rule forbids `dotnet build` when CPU is above 50% or `dotnet/csc` is running.
Solution: Run `git diff --check`, static hot-path allocation scans, process guard, and CPU guard. Mark compile as blocked by CPU guard instead of violating the build policy.
Rejected Alternatives: Launching `dotnet build Assembly-CSharp.csproj` under 100% CPU, or claiming compile passed without objective output.
Scalability potential: Build deferral does not affect runtime tiers; it prevents adding integration load while another workload owns the machine.
Hardware Impact: Avoided build contention on CPU-saturated host. Runtime verification remains pending; static audit found no new hot-path managed containers, LINQ, `Camera.main`, scene search, coroutine, or `ToString` use.

## Decision 07 - Signal Route Repair

Problem: The first implementation read KCC through concrete `PhysicsDeterminismSignals`, but source archaeology found a second KCC publisher pushing `KccVelocitySignal` directly into the typed SignalBus. That could miss valid body-turn data and violated one-route signal discipline.
Solution: Remove the `Hecton8.Physics` dependency and consume the non-destructive `SignalBus<KccVelocitySignal>.GetFrameSnapshot()` view. Select the newest signal by frame, sequence, then source id, and track frame/sequence/source in blackbox state.
Rejected Alternatives: Keeping the concrete physics helper, destructively calling `TryReadFrame`, or adding a new global latest accessor in Core. The first misses a route, the second steals signals from other readers, the third expands global API surface.
Scalability potential: Low/Middle/High/Ultra share one signal route; quality only changes comfort thresholds and smoothing, not ownership.
Hardware Impact: Snapshot scan is bounded by the lane frame count; expected KCC count is 1-2. Estimated cost: 1-4 us on i3/MX350, 0 B GC. Avoided duplicate-route bug cost is correctness, not measurable CPU.

## Decision 08 - DTO Layout And Burst Hardening

Problem: The first KCC patch left touched DTOs sequential/pack-4 and root input size was 76 bytes, not a clean ARM64-friendly multiple. Burst jobs also lacked explicit `CompileSynchronously=true` and `NoAlias` on arrays.
Solution: Convert `VRSomaticBlackBoxEntry` to explicit 128 bytes, `VRSomaticRootSyncInput` to explicit 80 bytes, `VRSomaticRootSyncOutput` to explicit 32 bytes, and `HeadCastSample` to explicit 48 bytes. Add `ValidateNativeLayouts()` using `UnsafeUtility.SizeOf<T>()` in editor/development builds. Add `CompileSynchronously=true` and `[NoAlias]` to touched job NativeArrays.
Rejected Alternatives: Leaving sequential layout to compiler packing or padding with comments only. ARM64 alignment must be machine-verifiable, not narrative.
Scalability potential: Fixed ABI across all quality weights; high-end shader/readback tooling can consume the richer blackbox without runtime contract drift.
Hardware Impact: Explicit layout removes unplanned 76-byte root records and 36-byte head samples. Blackbox memory increases to 38.4 KB total for 300 frames, still below one 64 KB page class and 0 B GC.
