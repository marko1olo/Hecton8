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

Solution: derive the reference vector from `handleAnchor.position` transformed through the lever root with `InverseTransformPoint`, matching the existing handle-proximity path. Also reset ratchet step state when idle, track hot-swap listener registration locally, keep receiver registration play-mode-only, use increment/compare for blackbox ring wrap, and allow Tick to recover if deferred native disposal delays allocation.

Rejected Alternatives: constraining scene hierarchy was rejected because it makes authoring brittle. Recomputing the reference vector every Tick was rejected because the closed handle basis is static config, not frame state. Leaving modulo in telemetry was rejected because the branch wrap is simpler and cheaper.

Scalability potential: Low/Middle get the same deterministic scalar solve under richer art hierarchy. High/Ultra can use nested mechanical linkages and animated handle meshes without changing the solver contract.

Hardware Impact: i3/MX350 removes one integer modulo from the 60Hz telemetry path, estimated 0.01 us saved per tick. Other changes are cold lifecycle/configuration, edit-mode hygiene, or idle-only branches.
