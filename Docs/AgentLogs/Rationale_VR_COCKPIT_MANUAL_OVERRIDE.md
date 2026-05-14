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

## Decision 12 - Source drift must be corrected by forward patch only

Problem: `OpenXRManualOverrideLever.cs` was observed back at older world-hand solver and tick-side native recovery code while the task evidence expected the hardened implementation. In a 20+ agent workspace, reset-style recovery can erase parallel work and hide drift.

Solution: reapply only task-owned safeguards by forward patch: lifecycle-only native recovery, local hand sample cache, projection singularity hold, XR/frame/basis caches, invalid hand-side fallback, public XML docs, dispatcher hotswap allocation recovery, and IK handle pose caching. Also changed `TryQueueHandPress()` to resolve the handle anchor only when pivot distance already fails.

Rejected Alternatives: `git reset`, checkout, or wholesale file replacement were rejected because they can erase parallel-agent changes. Leaving the regression was rejected because it reintroduced tick-side allocation reachability and false snap-to-min behavior.

Scalability potential: Low/Middle keep the cheap local-space solve and fewer transform/XR/frame property crossings. High/Ultra can scale cockpit controls without multiplying avoidable receiver/telemetry/solver work.

Hardware Impact: i3/MX350 restores the previous 0 B/frame proof, removes repeated transform conversions, and saves one handle transform conversion on pivot-close receiver callbacks. Direct UI response-file probe exits 0 after recovery.

## Decision 13 - Native telemetry must reject corrupted transforms before writes

Problem: world hand input was finite-checked, but transform matrices and inspector pivot values can still produce non-finite local coordinates. That violates the NaN/INF vaccination rule because local hand, pivot, target, and angle feed NativeArray state, visible transform rotation, and the blackbox ring.

Solution: add a shared `IsFiniteFloat3()` guard, reject non-finite transformed hand samples, sanitize non-finite `pivotLocalPosition` during cold configuration, fall back to pivot when handle-anchor world-to-local conversion is non-finite, and validate hand/pivot telemetry before writing `_blackBox`. On invalid telemetry state, dump `Docs/AgentLogs/Dump_VR_COCKPIT_MANUAL_OVERRIDE.bin` and skip the corrupt frame entry.

Rejected Alternatives: trusting Unity Transform output was rejected because corrupted parent transforms are exactly where VR rigs fail badly. Clamping NaN to zero inside the hot solver was rejected because it hides bad authoring and creates false lever motion. Recomputing handle/pivot every frame was rejected because the existing cold config and pivot-first receiver branch are cheaper and deterministic.

Scalability potential: Low keeps the same cheap scalar solve with explicit invalid-state rejection. Middle/High gain stable cockpit interaction under nested rigs. Ultra can spend saved solver simplicity on richer haptic/audio/visual response while the blackbox still records deterministic state.

Hardware Impact: i3/MX350 pays two extra `float3` finite checks in the 60Hz blackbox write and one receiver-side local-hand check only on hand candidate frames; estimated +0.04 us per tick and +0.03 us per receiver callback. This is accepted because it buys crash explainability without heap allocation or physics cost.

## Decision 14 - Presentation writes and input axes need their own guard boundary

Problem: the lever's simulation state was finite-guarded, but presentation and downstream input adaptation still trusted upstream values. A bad input provider, automation override, or corrupted handle transform could push non-finite axes into the local universal signal or write invalid pose data into IK/visual transforms.

Solution: clamp Move, Look, and Vertical to [-1,1] with zero fallback in `BuildUniversalInputSignal()`. Gate lever visual writes behind finite angle and quaternion checks. Gate IK writes behind finite handle pose checks, recover a corrupted IK target by snapping to the valid handle, and use one `SetPositionAndRotation()` call for the interpolated pose.

Rejected Alternatives: relying on upstream `InputDispatcher` sanitation was rejected because UI.VR must remain robust against hot-swap and automation injection. Leaving separate `position` and `rotation` writes was rejected because a single valid combined write is cleaner and avoids half-updated presentation state. Adding smoothing state arrays was rejected because current Math LOD smoothing already exists and extra state would not improve correctness.

Scalability potential: Low/Middle keep the cheap scalar lever while avoiding corrupted presentation output. High/Ultra can use richer cockpit art rigs and IK targets without increasing simulation truth. The same signal remains device-agnostic and bounded for all tiers.

Hardware Impact: i3/MX350 pays roughly +0.03 us per tick for axis sanitation, +0.02 us per visual write guard, and +0.05 us while grabbed for IK pose guards. One combined IK transform write replaces separate position/rotation writes during interpolation.

## Decision 15 - Blackbox dumps must be fault evidence, not repeated frame work

Problem: the blackbox guard could call `DumpBlackBox()` every Tick while native state stayed corrupt. The dump path is intentionally allowed to allocate and touch disk as a crash artifact, but repeating it every frame would damage the same frame pacing the zero-GC mandate is trying to protect.

Solution: add `_blackBoxDumped` as a lifecycle-reset latch. `DumpBlackBox()` exits after the first attempted dump until `OnEnable()` or `InitializeLeverStateAfterAllocation()` resets the latch. Telemetry flag bit 5 records that a dump has already been attempted on later valid frames.

Rejected Alternatives: deleting the dump was rejected because the blackbox rule requires evidence on NaN/crash. Logging every corrupt frame was rejected because managed logs allocate and are noisy. Keeping repeated binary rewrites was rejected because one corrupted rig could create disk I/O every frame.

Scalability potential: Low/toaster path gets one bounded dump and no repeated disk churn. Middle/High stay deterministic. Ultra can spend saved failure-mode headroom on richer cockpit aftermath presentation while the same fault bit tells downstream diagnostics that the blackbox entered dump mode.

Hardware Impact: normal frames pay no extra IO and only a telemetry flag branch. Persistent corrupt state avoids repeated 300-entry binary rewrites; on i3/MX350 this prevents multi-millisecond disk spikes during a fault loop. No numeric H-Phi score is claimed for this local robustness pass because the static formula does not measure fault-dump rate limiting directly.

## Decision 16 - Cold component fallback still counts as H-Phi lookup debt

Problem: scoped H-Phi hygiene found one `GetComponent<BoxCollider>()` fallback in `EnsureReferences()`. It is cold lifecycle code, not a Tick allocation, but the audit pattern treats `GetComponent<...>` as component lookup debt and the fallback is inside the owned VR lever file.

Solution: replace the fallback with `TryGetComponent(out activationVolume)`. The serialized collider reference remains the preferred path, and `[RequireComponent(typeof(BoxCollider))]` still guarantees normal prefab safety.

Rejected Alternatives: deleting the fallback was rejected because a scene instance with an unwired serialized field should still recover. Keeping `GetComponent<...>` was rejected because the H-Phi audit already has a safer lookup pattern available. Runtime scene search or `FindObject*` was rejected as architecture debt.

Scalability potential: Low/Middle/High/Ultra all keep the same cold lifecycle behavior. The local H-Phi hygiene counter over the task scope now reports `GetComponentCalls=0`, `FindObjectCalls=0`, `UnityUpdateMethods=0`, `PublicEvents=0`, `HingeJoint=0`, and `DirectInput=0`.

Hardware Impact: no steady-frame impact. Cold lifecycle cost is equivalent for the expected `RequireComponent` path, and the change removes one audit-counted lookup debt site from the VR lever domain.

## Decision 17 - Do not keep fake Burst surface for a one-lever scalar solve

Problem: the source still contained an unused `LeverAngularSolveJob` with a `Unity.Burst` dependency, while the real Tick path intentionally solves one scalar lever on the main thread. The IK smoothing step also used `blend * max(1, dt * 90)`, which makes the default high-tier blend snap at normal 60 Hz and contradicts the smoother high-tier presentation claim.

Solution: remove the unused Burst job, remove `using Unity.Burst`, and remove `Unity.Burst` from `Hecton8.UI.VR.asmdef`. Keep `Unity.Jobs` because deferred native disposal still owns a `JobHandle`. Change IK step to `saturate(blend * saturate(dt * 60f))` and return early on zero step before transform reads.

Rejected Alternatives: scheduling the job and completing it in Tick was rejected because `Complete()` in frame code violates the native job mandate and would cost more than the scalar solve. Leaving the dead job for static H-Phi optics was rejected as evidence fraud. Keeping the snap-prone IK formula was rejected because richer rigs need visible interpolation, not instant pose jumps.

Scalability potential: Low keeps cheaper low-tier IK smoothing. Middle/High get actual smooth handle following at 60 Hz. Ultra can add denser hand/lever presentation because the solver remains scalar and the assembly no longer depends on Burst for unused code.

Hardware Impact: i3/MX350 avoids an unnecessary Burst package dependency in the VR UI assembly and keeps the hot solve on simple scalar math. Zero-dt pause frames now skip handle/IK transform reads and writes. No project-wide numeric H-Phi gain is claimed because the global audit timed out; scoped hygiene improves by making `BurstRefs=0` and `IJobRefs=0` in the VR lever slice.

## Decision 18 - Receiver unregister must use the collider that was actually registered

Problem: `TryUnregisterReceiver()` used the current `activationVolume` field. If editor tooling, prefab repair, or runtime initialization swaps that field after registration, the old collider can remain in `PhysicalHandReceiverRegistry` and keep routing hand presses to a disabled lever.

Solution: cache the exact collider in `_registeredActivationVolume` immediately after registration, then unregister that cached collider and clear the cache during teardown.

Rejected Alternatives: assuming serialized fields are immutable during play was rejected because this workspace has multiple agents and runtime repair paths. Searching the registry for this receiver was rejected because the registry is intentionally fixed-key and lookup-only for the interaction hot path.

Scalability potential: Low/Middle/High/Ultra all keep the same fixed receiver table. Dense cockpit panels avoid stale receiver entries when authored controls are reconfigured or disabled.

Hardware Impact: no steady-frame cost. Cold lifecycle adds one managed reference field assignment and prevents stale registry probes from surviving after a control is disabled.

## Decision 19 - Registry saturation must not look like successful registration

Problem: `PhysicalHandReceiverRegistry.Register()` logged once when the fixed table saturated, but it returned `void`. The VR lever therefore marked itself registered even when no receiver slot was written. In a dense cockpit, that creates a silent dead lever instead of a truthful lifecycle state.

Solution: add `TryRegister()` to return the actual slot-write result and keep `Register()` as a compatibility wrapper. `OpenXRManualOverrideLever.TryRegisterReceiver()` now sets `_receiverRegistered` and `_registeredActivationVolume` only after `TryRegister()` succeeds.

Rejected Alternatives: raising `MaxReceivers` was rejected because it hides authoring pressure and increases fixed memory without proving the intended cockpit budget. Changing every existing caller was rejected because the compatibility wrapper keeps the old API stable while the VR lever uses the stricter path.

Scalability potential: Low/Middle/High/Ultra all preserve the same fixed-size receiver table and O(1) lookup. Dense cockpit authoring gets truthful failure state instead of false registration.

Hardware Impact: no steady-frame cost and no hot lookup change. Cold registration pays one boolean return. On saturated tables, the lever avoids stale local state and keeps later retry paths possible.

## Decision 20 - Receiver truth must cover all physical-control consumers

Problem: after `TryRegister()` existed, adjacent physical controls still called the compatibility `Register()` wrapper and marked themselves registered even if the fixed table saturated. Two controls also kept `MinimumDeltaTime` fake progress, so a dispatcher `dt=0` frame could still move visuals or repeat a panel Hold event. Their haptic fallback also treated every non-left hand value as right-hand feedback.

Solution: move `PhysicalPanelButton`, `PhysicalSnapSwitch`, and `LifePodSeatStrapLatch` to `TryRegister()` for lifecycle truth. Panel buttons and snap switches now cache the exact registered collider before unregister. `FastDecayBlend()` in both controls now returns zero on sanitized zero-dt; panel buttons skip hold-repeat dispatch and unchanged mesh writes when time is frozen, and snap switches return before visual solve on zero-dt frames. Panel/switch haptics now fall back to both-hand motor masks for invalid/future hand-side values after authored layer masks fail. Public registry methods now document saturation and collider identity semantics.

Rejected Alternatives: leaving old callers on `Register()` was rejected because it creates a split-brain registry contract. Raising `MaxReceivers` was rejected because it hides cockpit density pressure and increases fixed memory without proving budget. Keeping `MinimumDeltaTime` was rejected because deterministic time control must obey dispatcher `dt`, not force visible progress through pause/time-dilation frames. Defaulting unknown hand side to right was rejected because bad hand identity should not create confident wrong tactile localization.

Scalability potential: Low keeps fixed O(1) receiver lookup with truthful saturation failure and less pause-frame work. Middle/High can run denser cockpit panels without stale receiver state. Ultra can spend the saved control stability budget on richer tactile/audio response because the physical controls remain scalar, event-driven, and allocation-free.

Hardware Impact: i3/MX350 pays no steady-frame registration cost. Cold registration adds one boolean result check and one collider-reference cache. Zero-dt frames avoid snap-switch visual solve/write and prevent panel Hold spam risk; stable panel frames skip unchanged mesh writes. Haptic fallback adds one branch only on haptic dispatch frames. No project-wide numeric H-Phi gain is claimed because no full audit completed in this pass.

## Decision 21 - Empty receiver tables must not still pay physics probe cost

Problem: `PhysicalInteractionHandler.TickPhysicalPanelButtons()` ran the physical panel probe path whenever XR panel buttons were enabled and XR was active, even if no collider-backed receiver was registered. In empty or transition cockpit states, that still paid for hand pose reads, signal-service reads, `OverlapSphereNonAlloc`, bounds extraction, and registry lookups that could not produce a press.

Solution: maintain a scalar receiver count inside `PhysicalHandReceiverRegistry` and expose `HasReceivers`. The count increments only when a new open-addressed slot is written and decrements only when an exact collider/receiver pair is removed. `TickPhysicalPanelButtons()` now checks `HasReceivers` before reading probe pose or issuing the physics overlap.

Rejected Alternatives: deregistering the interaction handler when the table is empty was rejected because receiver registration has no event callback and late-spawned cockpit controls must become live on the next dispatcher tick. Scanning the 128-slot table every frame was rejected because it replaces one cheap branch with O(n) registry work. Raising/lowering receiver capacity was rejected because this was not a saturation problem.

Scalability potential: Low/toaster states skip panel physics probes when no usable receiver exists. Middle keeps the same fixed table and next-tick activation for late-spawned controls. High/Ultra can run denser cockpits because active receivers still use the same O(1) hash lookup while empty transitional periods avoid wasted physics queries.

Hardware Impact: i3/MX350 pays one static integer comparison per active XR tick and one int increment/decrement on receiver lifecycle. Empty receiver states save one hand pose read, one interaction-signal service read, one `OverlapSphereNonAlloc`, and up to eight candidate bounds/registry checks per XR frame. Normal registered-control frames keep the existing NonAlloc path and 0 B/frame behavior.

## Decision 22 - Physical hand fixed solve must obey zero dispatcher time

Problem: `PhysicalHandController.SanitizeFixedDeltaSeconds()` converted non-finite or zero fixed-step deltas into `MinimumDeltaTime`. A dispatcher `dt=0` frame could therefore advance haptic cooldowns, harvest snap timers, recoil decay, open/finger pose blending, suit shell movement, virtual hand lag, articulation targets, and grabbed-body force solve despite no simulation time passing.

Solution: sanitize non-finite or non-positive fixed deltas to zero, return early from `StepFixed()` on zero, and keep the positive-step minimum clamp for tiny positive deltas that feed velocity divisions. Preserve `_lastFingerPoseDeltaTime` when a finger pose job is already scheduled so the late-frame completion for a previous valid fixed step still uses its original blend delta.

Rejected Alternatives: leaving `MinimumDeltaTime` as a universal fallback was rejected because it forces fake progress through pause/time-dilation. Allowing the rest of the fixed solve to run with `dt=0` was rejected because the hand solver divides by dt in virtual hand and angular velocity paths. Clearing scheduled finger jobs was rejected because job completion belongs to `LateFrameTick()`.

Scalability potential: Low/toaster pause frames skip the full physical hand fixed solve. Middle keeps deterministic authored hand state during time dilation. High/Ultra can spend the saved zero-time frames on presentation without desynchronizing the physical hand proxy.

Hardware Impact: i3/MX350 pays one zero-dt branch in `StepFixed()`. Zero-time frames avoid haptic cooldown work, harvest snap advance, recoil/open-pose solve, suit shell updates, virtual hand lag, articulation drive writes, grabbed-body force solve, and finger job scheduling. Normal positive fixed steps keep the existing minimum clamp and division safety. 0 B/frame.

## Decision 23 - Late-frame hand work must be job-pending, not grab-lifetime

Problem: `PhysicalHandController.RequiresLateFrameTick` stayed true for the whole grab lifetime even though `LateFrameTick()` only calls `CompleteScheduledFingerPose()`. The XR idle-bypass function also read `InputDispatcher` state before returning false for grabs, harvest snaps, suit shell, or active suit contact. Suit-contact haptics treated every non-left hand side as right-hand output.

Solution: narrow `RequiresLateFrameTick` to `_fingerPoseScheduled`. Move active/contact solve gates before dispatcher access in `ShouldBypassXRHandKinematicUpdate()`. Add explicit XR controller-index resolution that rejects invalid hand side, and add `BothMotorMask`/`ResolveHandMotorMask()` so invalid/future hand sides generate non-localized both-hand contact feedback instead of right-biased feedback.

Rejected Alternatives: leaving late-frame registered for all grabs was rejected because no late-frame work exists without a pending finger job. Keeping dispatcher reads before active solve gates was rejected because the solve state already proves the idle-bypass path cannot be used. Defaulting invalid side to right was rejected for the same reason as panel/switch haptics: bad identity should not create precise wrong tactile routing.

Scalability potential: Low/toaster avoids empty late-frame calls and unnecessary XR input-state reads during active physical interactions. Middle keeps deterministic finger job completion because fixed tick still registers late-frame immediately after scheduling. High/Ultra can run richer hand/contact presentation without paying idle-probe overhead in every grabbed/contact frame.

Hardware Impact: i3/MX350 saves one empty dispatcher late-frame callback per rendered frame during grabs with no pending finger job, and one dispatcher/input-state lookup per fixed step while grabbing, harvest-snapping, suit-shell-enabled, or in suit contact. Haptic fallback adds only branch work on contact haptic dispatch. 0 B/frame.

## Decision 24 - Lever IK smoothing should use presentation-grade math, not spherical interpolation

Problem: `OpenXRManualOverrideLever.UpdateIkTarget()` used `Vector3.Lerp` and `Quaternion.Slerp` every grabbed presentation tick. The target is an IK helper transform, not gameplay truth; the lever angle/latched state remains scalar and authoritative. Slerp buys precision that is not visible here while costing more than a normalized lerp.

Solution: replace position interpolation with `math.lerp(float3)` and rotation interpolation with `ApproximateNlerp()`. The helper performs shortest-arc sign correction, blends quaternion components, validates length squared, and normalizes with `math.rsqrt`.

Rejected Alternatives: keeping Slerp was rejected because this is presentation smoothing, not simulation authority. Snapping directly to the handle was rejected because prior work deliberately restored visible IK smoothing. Adding a Burst job was rejected because one IK transform blend is cheaper on the main thread.

Scalability potential: Low/toaster saves grabbed-frame presentation math. Middle keeps smooth IK output. High/Ultra can spend the saved cycles on denser cockpit tactile/audio/visual feedback while the manual override lever remains a scalar kinematic control.

Hardware Impact: i3/MX350 removes one `Quaternion.Slerp` and one `Vector3.Lerp` from grabbed lever presentation frames, replacing them with struct math and one `rsqrt`. 0 B/frame. Gameplay angle solve and blackbox telemetry are unchanged.

## Decision 25 - Panel hand pose should not be sampled without a signal sink

Problem: `TickPhysicalPanelButtons()` read hand pose and validated it before checking whether `GlobalRegistry.InteractionSignals` existed and was initialized. If the interaction signal service is unavailable during boot or service reload, no receiver can queue a valid press, so the pose read and all later probe work are dead.

Solution: move the existing `InteractionSignals` readiness check before `TryGetInteractionProbePose()`, hand collider fetch, probe radius resolve, and `OverlapSphereNonAlloc`.

Rejected Alternatives: leaving the order unchanged was rejected because service outage is a clear fast-fail condition. Caching the signal service as a long-lived field was rejected because GlobalRegistry can hot-swap services and this path already has a cheap property read.

Scalability potential: Low/toaster boot and service-transition frames avoid wasted physical hand sampling. Middle/High keep identical behavior once the signal service is initialized. Ultra can keep dense cockpit controls without extra bridge work during service reloads.

Hardware Impact: i3/MX350 saves one physical hand pose read and all downstream panel-probe work per XR frame when the signal service is absent/uninitialized. Normal initialized frames pay the same work as before, just in a safer order. 0 B/frame.
