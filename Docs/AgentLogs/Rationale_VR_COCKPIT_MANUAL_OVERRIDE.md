# Rationale - VR_COCKPIT_MANUAL_OVERRIDE

## Decision 1 - Isolated VR lever assembly must still receive physical hands

Problem: `PhysicalHandReceiverRegistry` was internal to the Core assembly, while the prompt requires a new `Hecton8.UI.VR` assembly. Without a bridge, the lever cannot receive `PhysicalInteractionHandler` hand callbacks.

Solution: expose the existing fixed-size registry as `public static` without changing storage, lookup, or semantics. This keeps the existing zero-GC collider lookup and avoids a new dispatcher.

Rejected Alternatives: standard Unity `GetComponentInParent<IPhysicalPanelButtonReceiver>()` from overlap results would add component traversal to the physical interaction hot path. A new singleton hand registry would violate the prompt and create integration conflict.

Scalability potential: Low uses the same O(1) receiver table. Middle/High/Ultra can add more cockpit controls without switching lookup model.

Hardware Impact: i3/MX350 avoids per-overlap component walks; estimated saved 3-12 us in dense cockpit overlap frames.

## Decision 2 - Kinematic lever, no physics joint

Problem: manual override must feel physical in OpenXR but remain deterministic and cheap.

Solution: solve hand projection in lever local space, then integrate a damped scalar spring over native angle/velocity arrays. The visual lever is just local rotation.

Rejected Alternatives: Unity `HingeJoint` would introduce solver-order variance, constraint jitter, and dependency on physics timestep; it is explicitly banned by prompt reread requirement.

Scalability potential: Low keeps reduced IK smoothing and same scalar solver. Middle/High keep smoother IK. Ultra can drive extra sparks/audio from the same angle signal without changing simulation.

Hardware Impact: i3/MX350 scalar solve is estimated under 2 us; joint solver replacement avoids unpredictable physics island cost.

## Decision 3 - Manual override as typed signal before prologue handoff

Problem: the lever must trigger prologue completion but consumers need an explicit cockpit-control event.

Solution: add `ManualOverridePulledSignal` to the existing typed `SignalBus<T>` lanes and publish `PrologueCompleteSignal` after latch.

Rejected Alternatives: direct scene transition or hard reference to prologue director would create an agent-order dependency and break the EventBus/GlobalRegistry decoupling rule.

Scalability potential: Low consumers can listen only for prologue completion. High/Ultra systems can consume angle, velocity, and hand side for overkill cockpit feedback.

Hardware Impact: fixed lane capacity 8; estimated publish cost under 1 us, no managed event fanout.

## Decision 4 - Dot/cross self-check instead of visual-only verification

Problem: a lever can look correct while the plane projection sign is inverted, causing the player to pull the handle and drive the angle negative or clamp at zero.

Solution: add an editor/development cold-path verifier: reference vector equals 0 degrees, perpendicular pull vector equals 90 degrees under `atan2(dot(axis, cross(reference, projected)), dot(reference, projected))`.

Rejected Alternatives: scene-only manual testing or relying on Quaternion visual rotation; neither proves the solver orientation.

Scalability potential: Low/Middle/High/Ultra share identical latch math, so the same verification protects all tiers.

Hardware Impact: zero player-frame impact in release; cold editor check only.

## Decision 5 - Core compile wall handling

Problem: `Hecton8.Core.csproj` fails before task code on missing references from unrelated domains (`Environment.Fluids`, `Audio.Virtualization`, `Physics.CCD`, `Core.Scheduling`, etc.). First attempt also exposed a real task-local placement error for `ManualOverridePulledSignal`.

Solution: fix the task-local signal placement by moving the payload into the compiled `GlobalSignals.cs` signal region. Re-run filtered Core build to confirm no remaining Core errors for manual override signal/registry symbols. The isolated `Hecton8.UI.VR` assembly remains Unity-compile pending because MCP lost its editor session before generating the new csproj.

Rejected Alternatives: editing unrelated asmdefs or dependency systems to make the Core project build; that is outside UX_ENGINEER domain and would risk sabotaging parallel agents.

Scalability potential: Manual override stays on a fixed typed lane; unrelated compile debt is isolated for integrator follow-up.

Hardware Impact: no runtime impact. Build verification narrowed from 134 broad errors to no task-local Core errors in the filtered pass.

## OMEGA POLISH CHANGES

Problem: final audit found two honest math paths in the lever hot path: division in non-VR fallback/normalization and unconditional `math.normalize` after projection.

Solution: replaced division with `math.rcp` multiplication and replaced `math.normalize(projected)` with `projected *= math.rsqrt(projectedLengthSq)` after the existing guard. Static scan found no `HingeJoint`, no managed `foreach`, no `.ToArray()`, no `FindObject`, no `GetComponentInParent`, and no remaining `math.normalize` in task files.

Rejected Alternatives: a LUT for the angular solver was rejected because the input vector is continuous and latch correctness matters; `atan2` remains the correct scalar solve. A physical joint remained rejected as non-deterministic bloat.

Scalability potential: Low uses reduced IK smoothing with identical latch math. Middle/High use smoother hand target interpolation. Ultra can layer extra visual/audio feedback from the same angle and ratchet signals without extra solver work.

Hardware Impact: i3/MX350 projection path avoids sqrt/divide; estimated 0.15 us saved on fallback/projection frames and no managed allocation added.

Cinematic Cheats Used: kinematic visual lever instead of physics joint; scalar damped spring instead of force solver; 10-degree haptic ratchet instead of continuous mechanical simulation; local-space blackbox telemetry instead of verbose managed logs.

Final Git Diff: task-local code paths are `Assets/_Project/Scripts/UI/VR/OpenXRManualOverrideLever.cs`, `Assets/_Project/Scripts/UI/VR/Contracts/ManualOverrideLeverContracts.cs`, `Assets/_Project/Scripts/UI/VR/Hecton8.UI.VR.asmdef`, `Assets/_Project/Scripts/Core/GlobalSignals.cs`, `Assets/_Project/Scripts/Core/Signals/PrologueReentrySignals.cs`, and `Assets/_Project/Scripts/Interaction/PhysicalHandReceiverRegistry.cs`. Current visible diff after polish is the reciprocal/rsqrt replacement in `OpenXRManualOverrideLever.cs` plus status/rationale/log updates; broader core/registry additions are present in the working tree snapshot.

## Decision 6 - Handle-first grab without breaking pivot solver

Problem: pivot-only grab detection obeyed the literal task text but creates bad VR ergonomics when the physical handle is offset from the hinge. A player reaches for the handle, not the axle.

Solution: accept a grab when the hand is within 0.15m of either the pivot or the handle position transformed into lever local space. The angular solver still projects the hand around the pivot, so mechanical behavior remains deterministic.

Rejected Alternatives: handle-only grab would drop the prompt's pivot check; pivot-only grab is correct on paper and wrong in a real cockpit.

Scalability potential: Low/Middle/High/Ultra all use the same local-space scalar check. High/Ultra can add handle glow or decals without changing simulation.

Hardware Impact: one extra local-space distance check only during physical receiver callbacks; estimated +0.2 us in candidate frames, no steady-frame cost.

## Decision 7 - Compile probe honesty

Problem: Unity generated `Hecton8.UI.VR.rsp` but not `Hecton8.UI.VR.dll`, so the runtime assembly needed a direct probe.

Solution: invoke the generated response file with Unity's Roslyn compiler. The reported errors are stale Core reference symptoms: `ManualOverridePulledSignal` is absent from `Hecton8.Core.ref.dll` and `PhysicalHandReceiverRegistry` is still internal in that stale ref. No new lever-local syntax errors surfaced before those dependency errors.

Rejected Alternatives: claiming compile success from file inspection; killing active quiet `dotnet build Hecton8.Core.csproj` processes that may belong to other agents.

Scalability potential: once Core compiles, UI.VR should bind to the public registry and signal lane through normal asmdef references.

Hardware Impact: no runtime impact; verification-only action.

## Decision 8 - Reject stale hand lock and untracked dispose jobs

Problem: a held grip could keep the lever grabbed while no fresh physical hand sample arrived, causing the solver to reuse stale pose data. Cleanup also scheduled NativeArray disposal without keeping or completing the returned handle.

Solution: release grab after more than 3 stale frames; during short 2-3 frame gaps, hold the current target angle instead of advancing against stale data. Dispose the five persistent native arrays through tracked deferred `Dispose(JobHandle)` after sentinel unregister, store `_disposeHandle`, null the arrays, and schedule batched jobs.

Rejected Alternatives: polling XR hands directly would bypass the physical hand receiver contract and create a new dependency. Keeping asynchronous disposal was unnecessary for arrays of size 1/300 and made ownership harder to prove.

Scalability potential: Low/Middle get stable release behavior when tracking drops. High/Ultra can add visual IK reacquire effects later without changing the deterministic lever solver.

Hardware Impact: i3/MX350 pays one branch per tick, estimated 0.05 us. Deferred disposal is cold path only and avoids blocking teardown while preserving owner-visible disposal state.

Correction Note: The previous pass briefly used synchronous `NativeArray.Dispose()`. That violates the active `AGENTS.md` native-memory rule. It was corrected to the project pattern before closeout.

## Decision 9 - Registry rebound without tick polling

Problem: the lever cached `IInputService` at `OnEnable`. Bootstrap or service hot-swap could replace Input or Dispatcher after the lever enabled, leaving the lever bound to a no-op/stale service or registered against an obsolete dispatcher.

Solution: implement `IGlobalRegistryHotSwapListener`. On Input rebound, replace the cached `IInputService`. On Dispatcher rebound, clear the local registration flag and try to register against the new dispatcher. Registration/unregistration remains cold lifecycle work.

Rejected Alternatives: polling `GlobalRegistry.Input` every tick wastes a hot-path branch and can trigger fallback-warning behavior. Direct dependency on bootstrap ordering would make scene placement brittle.

Scalability potential: Low/Middle/High/Ultra all keep zero steady-frame cost. Hot-swap resilience matters most in VR because input backends and OpenXR runtime availability can change during startup.

Hardware Impact: 0 us steady-state. Cold rebound path only; avoids a dead lever caused by stale no-op input cache.

## Decision 10 - Localized haptics without per-frame hand polling

Problem: ratchet and latch haptic tool commands were broadcast to both XR motors. That is acceptable for non-VR fallback, but wrong for a grabbed cockpit lever because a left-hand pull should not feel centered or right-biased.

Solution: add explicit left/right motor masks and resolve `ToolHapticsRuntime` motor routing from the owning `PhysicalHandSide` or latched signal hand side. Unknown/non-VR keeps the existing both-hands mask. Also removed the last `.normalized` fallback and made `OnDestroy` unregister the hot-swap listener idempotently.

Rejected Alternatives: adding per-frame XR controller polling was rejected because the physical hand receiver already owns hand identity. Broadcasting both controllers was rejected because it spends haptic bandwidth and lowers tactile clarity. Adding a new haptic service dependency was rejected because `ToolHapticsRuntime` already supplies fixed queue semantics.

Scalability potential: Low keeps the same event frequency and cheaper IK smoothing. Middle/High get correctly localized gear clicks. Ultra can layer secondary cockpit shake/audio from the same ratchet steps without changing the solver.

Hardware Impact: i3/MX350 pays one branch only on ratchet/latch dispatch frames, estimated 0.02 us. No added steady-frame polling or allocation.

## Decision 11 - Solver basis must survive nested cockpit art rigs

Problem: `ResolveReferenceVector()` used `handleAnchor.localPosition`, which is only correct when the handle anchor is parented directly under the lever root. Real cockpit art rigs often nest the visible handle under a rotating visual child, so the angular solver could be initialized with a reference vector in the wrong local space.

Solution: derive the reference vector from `handleAnchor.position` transformed through the lever root with `InverseTransformPoint`, matching the existing handle-proximity path. Also reset ratchet step state when idle, track hot-swap listener registration locally, keep receiver registration play-mode-only, use increment/compare for blackbox ring wrap, and keep deferred native recovery in lifecycle/hotswap paths only.

Rejected Alternatives: constraining scene hierarchy was rejected because it makes authoring brittle. Recomputing the reference vector every Tick was rejected because the closed handle basis is static config, not frame state. Leaving modulo in telemetry was rejected because the branch wrap is simpler and cheaper.

Scalability potential: Low/Middle get the same deterministic scalar solve under richer art hierarchy. High/Ultra can use nested mechanical linkages and animated handle meshes without changing the solver contract.

Hardware Impact: i3/MX350 removes one integer modulo from the 60Hz telemetry path, estimated 0.01 us saved per tick. Other changes are cold lifecycle/configuration, edit-mode hygiene, or idle-only branches.

## Decision 12 - Degenerate hand projection must hold, not snap

Problem: the angular solver returned `minAngleDegrees` when the hand vector projected onto the lever rotation plane collapsed below epsilon. That can happen when the player controller crosses the pivot/axis line. The old behavior converted a mathematically invalid sample into a real lever target, causing an artificial snap toward closed.

Solution: add a zero-GC `TrySolveAngleFromHand` helper. Valid pulls still use the same `atan2(dot(axis, cross(reference, projected)), dot(reference, projected))` solver. Degenerate projection returns `false`; VR grab holds the current angle and sets a blackbox telemetry bit so the last 300 frames show the singularity instead of hiding it.

Rejected Alternatives: keeping the previous `minimum` fallback was rejected because it creates false physical motion. Smoothing over the snap with a spring-only delay was rejected because it preserves the wrong target. Polling OpenXR hands directly for a secondary vector was rejected because the physical hand receiver already owns hand identity and pose delivery.

Scalability potential: Low/Middle/High/Ultra all retain identical latch math on valid samples. Low avoids visible lever flicker during imperfect tracking. High/Ultra can layer reacquire animation from the telemetry bit later without changing the deterministic solver.

Hardware Impact: i3/MX350 pays one boolean branch on valid VR solve frames, estimated +0.03 us. The trade removes false spring work and visual correction on singular frames. No managed allocation, no Unity physics, no haptic spam.

Batch Drift Note: `Docs/Tasks/CURRENT_BATCH.md` no longer contains `VR_COCKPIT_MANUAL_OVERRIDE`; it currently contains unrelated prompt IDs. Existing `Status_` and `Rationale_` files remain the local assignment memory for this continuation, and status remains `PENDING VERIFICATION`.

## Decision 13 - Invalid hand-side bytes must not become right-hand events

Problem: `PhysicalHandSide` currently defines only `Left` and `Right`, but the lever receives that value across a collider interaction boundary. The previous helper treated every non-left value as right, which is acceptable for the current enum but brittle under corrupted data, future enum expansion, or bad fallback wiring.

Solution: make `ResolveSignalHandSide` and `ResolveHapticMotorMask` explicit. `Left` maps left, `Right` maps right, and any invalid value degrades to `HandUnknown` plus both motors. Valid runtime behavior is unchanged; bad data no longer produces misleading right-hand telemetry.

Rejected Alternatives: leaving the ternary was rejected because it hides invalid hand identity. Throwing or logging was rejected because this path can run on interaction frames and must remain fail-soft and zero-GC.

Scalability potential: Low/Middle keep the same haptic cost. High/Ultra retain clean hand-channel telemetry for later cockpit feedback layers without assuming enum closure forever.

Hardware Impact: i3/MX350 pays at most two integer comparisons only on haptic/latch dispatch frames, estimated +0.01 us. No steady polling, no allocation, no string logging.

## Decision 14 - Native allocation recovery belongs to lifecycle, not Tick

Problem: the defensive native-state recovery helper could call `AllocateNativeState()` from `Tick` if `_nativeAllocated` was false. The path is rare, but it still leaves `new NativeArray` reachable from a hot-path call stack, which violates the Zero-GC policy and makes static review weaker.

Solution: make `Tick` a pure `_nativeAllocated` gate. Lifecycle now calls `EnsureNativeStateForLifecycle()` before dispatcher registration, and `TryRegisterTick()` refuses registration unless native state exists. Allocation and reinitialization stay cold; the per-frame path never creates native containers.

Rejected Alternatives: keeping the recovery path was rejected because "rare" does not satisfy hot-path allocation law. Retrying allocation through a per-frame fallback or SlowTick was rejected because this component should allocate in Awake/OnEnable or fail inert until lifecycle rebind.

Scalability potential: Low/Middle/High/Ultra all get a stronger static 0 B/frame proof. Top-tier visual/haptic layers can consume the read model without inheriting hidden allocation risk.

Hardware Impact: i3/MX350 removes a rare but severe allocation/reinitialization spike from the Tick call graph. Steady cost is unchanged: one `_nativeAllocated` branch remains.

## Decision 15 - Dispatcher hotswap must recover native state before registration

Problem: after the hot-path allocation closure, `TryRegisterTick()` correctly refuses to register unless `_nativeAllocated` is true. A dispatcher service replacement could therefore leave an active lever inert if native allocation had previously been delayed by a deferred dispose handle.

Solution: on `GlobalRegistryServiceSlot.Dispatcher` replacement, clear the local registration flag, require a non-null dispatcher, then call `EnsureNativeStateForLifecycle()` before `TryRegisterTick()`. Allocation remains lifecycle/cold-path only, and `Tick` remains a pure `_nativeAllocated` gate.

Rejected Alternatives: reintroducing allocation retry inside `Tick` was rejected because it weakens the 0 B/frame proof. Per-frame dispatcher polling was rejected because service rebinding is a cold event. Ignoring the edge case was rejected because a dead manual override is worse than an inert cold-path recovery attempt.

Scalability potential: Low/Middle keep the same scalar lever and no extra steady work. High/Ultra can survive service rebinding during richer startup stacks without adding per-frame registry checks or new dependencies.

Hardware Impact: i3/MX350 pays 0 us steady-state. Cold hotswap may allocate the same five persistent native buffers that `Awake`/`OnEnable` already own; no allocation is reachable from `Tick`.

## Decision 16 - Public lever contract needs explicit XML documentation

Problem: the lever's external contract is used by dispatcher, physical hand receiver, registry hot-swap, haptics, UI, and cinematic consumers. Without public XML documentation, future consumers must infer read timing and ownership rules from implementation details.

Solution: add concise XML docs to the public lever class, read-model properties, `Tick`, `TryQueueHandPress`, `OnGlobalRegistryServiceReplaced`, `IManualOverrideLeverReadModel`, and execution-phase constant. Documentation stays on the public boundary; scalar math details remain in code/rationale to avoid comment bloat.

Rejected Alternatives: documenting private implementation fields was rejected because it adds noise and maintenance drag. Leaving the API undocumented was rejected because this component is an integration boundary across multiple agents.

Scalability potential: Low/Middle/High/Ultra consumers now have the same stable read-model contract without depending on private fields. Extra cockpit feedback layers can bind to documented read timing instead of reflection or scene probing.

Hardware Impact: 0 us runtime and 0 B/frame. XML comments are compile-time documentation only; direct UI response-file probe still exits 0.

## Decision 17 - Hand samples should cross Unity Transform once

Problem: the receiver already converted accepted world-space hand samples into lever-local space for distance checks, but the tick path converted the stored world hand position again for the angular solver and blackbox telemetry. That repeated Unity `Transform.InverseTransformPoint` native work in the frame path.

Solution: store `_lastHandLocalPosition` as the accepted sample. The VR solver and blackbox write consume that blittable local value directly. World-to-local conversion now happens only at physical hand sample acceptance, where the system is already validating distance against the pivot and handle.

Rejected Alternatives: keeping both world and local hand positions was rejected because only local space is needed for this lever's deterministic solver and AUP-safe telemetry. Recomputing local hand position in `Tick` was rejected because it spends native transform crossings after the receiver has already done the conversion.

Scalability potential: Low/Middle reduce frame-path transform work. High/Ultra can add more cockpit levers without each one repeating native coordinate conversion in telemetry and solve paths.

Hardware Impact: i3/MX350 saves up to two `InverseTransformPoint` crossings on fresh VR grab frames and one crossing on blackbox telemetry frames. 0 B/frame and no behavior change to latch math.

## Decision 18 - XR runtime state should be sampled once per lever frame

Problem: `Tick` already needed XR active state to choose VR versus fallback control, but telemetry and latch signal publication queried `XRSettings` again. This duplicated native/runtime property reads and could make the same frame report inconsistent state if the XR backend changed between reads.

Solution: cache `XRSettings.enabled && XRSettings.isDeviceActive` into `_xrActiveThisFrame` once at the start of `Tick`. The branch decision, manual override flags, and blackbox flags all use that cached value.

Rejected Alternatives: keeping repeated property reads was rejected because telemetry should describe the same sampled frame state as the simulation branch. Polling XR state through a separate service was rejected because the existing task scope already isolates input through `IInputService`, and this change only caches the current engine state.

Scalability potential: Low/Middle reduce engine property traffic. High/Ultra can layer more telemetry/feedback consumers without each consumer querying XR runtime state directly.

Hardware Impact: i3/MX350 saves one XR active-state read per lever frame and one additional read on latch frames. 0 B/frame and no behavior change.

## Decision 19 - Solver basis should already be in Burst-friendly float form

Problem: the valid VR solve path converted `_resolvedLocalAxis` and `_referenceLocalVector` from Unity `Vector3` into `float3` on every fresh hand sample. The values change only when configuration is cached, not during the solve.

Solution: cache `_axisLocalFloat` and `_referenceLocalFloat` in `CacheConfiguration()` and pass those fields directly into `TrySolveAngleFromHand`.

Rejected Alternatives: leaving conversion in the solver path was rejected because the data is static configuration. Storing only `float3` and removing `Vector3` was rejected because Unity visual rotation and configuration helpers still consume `Vector3`.

Scalability potential: Low/Middle save small but repeated scalar conversion work. High/Ultra keep the same solver while allowing more cockpit controls to share the same config-hotpath split.

Hardware Impact: i3/MX350 saves two `Vector3` to `float3` conversions on fresh VR solve frames. 0 B/frame and no behavior change.

## Decision 20 - Frame stamps must be consistent inside one tick

Problem: the lever asked `Time.frameCount` multiple times while building input, haptic, signal, and telemetry payloads during one dispatcher tick. That wastes repeated engine/static property reads and could stamp same-tick payloads inconsistently if called across a frame boundary.

Solution: sample `Time.frameCount` once into `_frameThisTick` at the start of `Tick`. All tick-owned payloads use that cached frame. `TryQueueHandPress` still samples `Time.frameCount` at receiver callback time because hand acceptance can occur outside the lever tick.

Rejected Alternatives: replacing the receiver callback timestamp with `_frameThisTick` was rejected because it could use a stale tick frame when physical hand callbacks run before the lever tick. Leaving repeated tick-side frame reads was rejected because all downstream payloads should share the same sampled frame state.

Scalability potential: Low/Middle reduce engine property reads per cockpit control. High/Ultra keep coherent payload frames as more haptic/visual consumers bind to manual override events.

Hardware Impact: i3/MX350 saves five frame-count reads on ordinary frames and more on latch/ratchet frames. 0 B/frame and no behavior change.

## Decision 21 - IK handle pose should be read once per follow step

Problem: `UpdateIkTarget()` read `handleAnchor.position` and `handleAnchor.rotation` separately in the snap branch and again in the smoothing branch. During grabbed VR interaction this is avoidable transform property traffic.

Solution: read handle position and rotation once into local value types after the early-out and before branch selection, then use those values for `SetPositionAndRotation`, `Vector3.Lerp`, and `Quaternion.Slerp`.

Rejected Alternatives: caching handle pose across frames was rejected because handle art can animate. Removing IK smoothing was rejected because low/high tier presentation tuning is part of the lever feel.

Scalability potential: Low/Middle reduce per-frame transform property traffic during grabs. High/Ultra keep the same visual smoothing while leaving budget for richer cockpit feedback.

Hardware Impact: i3/MX350 saves two transform property reads on smoothed IK frames. 0 B/frame and no behavior change.
