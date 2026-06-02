# Rationale_1602

Status: VERIFIED STATIC / UNITY MCP VALIDATION BLOCKED / DOTNET BUILD THROTTLED

## Decisions

Problem: Drop pod transit had no scoped runtime domain under `Assets/_Project/Scripts/Vehicles/DropPod`, while interaction/IK services already existed elsewhere.
Solution: Add a new DropPod namespace using existing first-party interfaces: `IInteractable`, `IInteractableTextProvider`, `IPhysicalPanelButtonReceiver`, `IPhysicalHandIkTargetSink`, `SignalBus<T>`, `InputDispatcher`, and `GlobalRegistry` cold service slots.
Rejected Alternatives: Editing central Interaction core or inventing a new raycaster would create direct dependencies for sibling agents and duplicate established hot paths.
Scalability potential: Low uses primitive colliders and sparse text cadence; Middle raises dashboard refresh; High adds tighter needle jitter; Ultra increases dashboard cadence and lighting overkill without changing authority.
Hardware Impact: Avoids a new raycast/UI stack; estimated low-end i3/MX350 saving: 30-70 us per interaction sample compared with bespoke component scans.

Problem: Airlock needs physical hand believability without transform-parenting the player hand or scheduling tiny IK jobs.
Solution: `DropPodAirlockController` sends `PhysicalHandIkTarget` to the existing hand IK sink while the hatch rotates in the fixed dispatcher lane.
Rejected Alternatives: Parenting hand bones to the hatch handle risks animation ownership fights; a new FABRIK job for one handle is below the batch threshold.
Scalability potential: Low only updates handle target during motion; Middle/High/Ultra can drive more authored handle sparks/audio without changing IK route.
Hardware Impact: Single struct target write is under 5 us expected on i3/MX350; no MeshCollider narrow-phase added.

Problem: Seat entry must avoid teleport/fade while not handing gameplay truth to a visual camera script.
Solution: `DropPodSeatController` blocks movement/look/tools/discrete input through `InputDispatcher`, records start/end camera poses, moves along cubic Bezier + NLERP in `LateFrameTick`, and optionally publishes existing `DropPodLandedSignal`.
Rejected Alternatives: Direct scene teleport or timeline-only camera animation would bypass fail-closed state and input ownership.
Scalability potential: Low uses authored anchors only; Middle adds control-point polish; High/Ultra can add camera shake and extra cockpit VFX as visual-only signals.
Hardware Impact: Bezier/NLERP costs are scalar math only; estimated under 15 us per late-frame tick on i3/MX350.

Problem: Diegetic dashboard text can become a hidden GC source if it uses TMP `.text`, `ToString`, or `string.Format`.
Solution: `DropPodDashboardTextRenderer` owns four persistent char[40] buffers and writes through `TryFormat` + `TMP_Text.SetCharArray`.
Rejected Alternatives: RenderTexture Canvas and string composition add a camera pass and managed churn.
Scalability potential: Low refreshes every 0.24 s; Middle interpolates lower cadence; High refreshes near 0.05 s; Ultra increases needle jitter/fidelity but not string allocation.
Hardware Impact: Avoids one UI camera pass and per-refresh strings; estimated saving: 80-300 us CPU plus RT bandwidth on i3/MX350.

Problem: Drop pod command/status routes need to be hot-broadcast without managed delegates or string commands.
Solution: `DropPodCommandSignal` and `DropPodStatusSignal` are explicit 16-byte unmanaged DTOs; producers use `SignalBus<T>.TryPushTracked`.
Rejected Alternatives: `Action`, `event`, or `HectonEventBus` would be managed/cold isolation paths, not first-party hot broadcast.
Scalability potential: Low caps command/status low-tier frame signals at 4; Middle/High/Ultra can raise frame budgets through the same continuous quality byte.
Hardware Impact: 16-byte payloads stay cache-cheap; expected sub-10 us push cost and no heap pressure.

Problem: Cabin collision must be cheap and verifiable before artists iterate on cramped geometry.
Solution: Add `DropPodCabinColliderValidator` editor menu that rejects any `MeshCollider` under the selected cabin root.
Rejected Alternatives: Accepting concave MeshColliders for a capsule interior would push PhysX narrow-phase cost into every hand/body probe.
Scalability potential: Low/Middle use Box/SphereCollider primitives; High/Ultra can add more primitive detail without switching to MeshCollider.
Hardware Impact: Avoids unpredictable MeshCollider narrow-phase spikes; estimated saving varies by contact count, typically 50-500 us on weak CPUs.

Problem: Verification must be honest under active project compile contention.
Solution: Used `rg`, brace-balance checks, SHA-256, Unity console probe, and process scan; did not launch dotnet build because two dotnet processes were active and Unity reported a pre-existing PrologueOrbitSceneBootstrap URP compile error.
Rejected Alternatives: Running build anyway would violate the user ban and hide an external compile wall as my result.
Scalability potential: Static proofs are tier-independent; runtime performance still requires editor compile recovery by the owner of the external URP issue.
Hardware Impact: Saved host CPU by avoiding a redundant build under active compiler load; estimated avoided cost: seconds to minutes, not microseconds.

Problem: Airlock and dashboard toggles were phase-correct for motion but sensory feedback could fire in the command path before the visual switch/hatch reached its final pose.
Solution: Store feedback position, hand side, and target state as value fields during interaction, then emit haptic/audio from `LateFrameTick` after simulation clears `_moving`.
Rejected Alternatives: Keeping immediate feedback would feel responsive but violates visual-sync ordering; adding jobs/locks would be wasteful for two local value fields.
Scalability potential: Low/Middle get deterministic cheap feedback; High/Ultra can layer additional sparks/camera shake from the same late-frame completion edge.
Hardware Impact: Moves existing work to the correct phase with no heap allocation and no added jobs; expected CPU delta under 3 us on i3/MX350.

Problem: APEX verification required proof that the new domain did not add DataVault lock nesting or hot-path service lookups.
Solution: Added editor static assertions and repeated source scans for hot-path method bodies, DataVault/lock tokens, managed events, LINQ, direct Unity time reads, and direct registry polling.
Rejected Alternatives: Verbal proof or JSON reports provide no compile-time guard and were explicitly rejected by the user.
Scalability potential: The guard is device-independent and prevents future low-tier regressions from entering the cockpit domain.
Hardware Impact: Runtime impact is 0 us; tests are editor-only.

Problem: Seat transit sensory feedback still had a command-path edge: physical hand press could enqueue haptics before VISUAL_SYNC, and fail-closed dashboard wording was generic.
Solution: Converted seat feedback to value-field pending state and dispatch only from `LateFrameTick`; changed fail-closed status text to `HATCH OPEN` with explicit `AirlockOpen` and `SeatTransitActive` panel labels.
Rejected Alternatives: Keeping instant rumble was responsive but phase-impure; adding managed events or a new feedback dispatcher would expand global surface for one cockpit owner.
Scalability potential: Low uses one pending feedback slot and static text labels; Middle/High/Ultra can add richer audio event ids and stronger haptic/audio assets without changing phase ownership or DTO layout.
Hardware Impact: Runtime cost is local field copy plus one late-frame branch; expected under 3 us on i3/MX350 and zero heap allocation.

Problem: A sibling editor test blocked Unity compile hygiene by relying on an unqualified `CelestialRuntimeSnapshot` symbol after the type owner moved under `Hecton8.Core`.
Solution: Patch only the test reference to `Hecton8.Core.CelestialRuntimeSnapshot`, leaving runtime orbital ownership untouched.
Rejected Alternatives: Adding a global alias or moving the runtime type would widen cross-domain surface for a test-only blocker.
Scalability potential: No runtime behavior changes across Low/Middle/High/Ultra tiers.
Hardware Impact: Runtime impact is 0 us; the gain is removing a stale editor compile blocker without launching `dotnet build`.

Problem: `GlobalSignals` configured three legacy lanes with one low-tier budget and then reconfigured them later with a different category budget, producing `[SIGNAL CONTRACT] Rejected late reconfigure` at runtime.
Solution: Feed the intended category low-tier values into the first legacy-lane configure call for `PlayerStressSignal`, `HUDNotificationSignal`, and `DebrisSpawnSignal`.
Rejected Alternatives: Making `SignalBus.Configure` tolerant would hide real contract drift; removing category initialization would risk other agents' lane assumptions.
Scalability potential: Low/Middle/High/Ultra keep one route per lane with continuous quality budgets rather than conflicting binary setup.
Hardware Impact: Prevents repeated startup error logging and contract churn; expected startup saving is small per lane but avoids cascading diagnostics stalls.

Problem: DropPod haptics had fixed intensity/duration values, which violated the continuous `GlobalQualityWeight` rule.
Solution: Scale haptic low/high amplitudes and durations with `SignalBusRegistry.GlobalQualityWeight01` using bounded `math.lerp` in airlock, seat, and dashboard toggle controllers.
Rejected Alternatives: Binary weak/strong rumble tiers would create quality discontinuities and would not map to the project's continuous quality pillar.
Scalability potential: Low keeps short restrained haptics; Middle/High raise body without changing timing truth; Ultra can feel heavier while preserving the same command DTOs.
Hardware Impact: Three scalar lerps per feedback edge; expected under 1 us on i3/MX350 and zero heap allocation.

Problem: Dashboard toggle visual motion lived in the normal update lane even though it is presentation-only cockpit feedback.
Solution: Remove `IUpdatable` from `DropPodDashboardToggleSwitch` and advance visual motion inside `LateFrameTick` from `SystemDispatcher.CurrentFrameDeltaTime`, immediately before `FlushVisual()` and feedback dispatch.
Rejected Alternatives: Keeping a separate update registration created an unnecessary phase boundary and made proof harder; moving to physics would be wrong for visual switch travel.
Scalability potential: Low gets one late-frame presentation lane; Middle/High/Ultra can add richer switch materials or audio without adding another hot loop.
Hardware Impact: Removes one updater registration and one extra dispatch lane for each active switch; expected saving is small per switch but zero-risk on weak CPUs.

Problem: DropPod signal bootstrap used a static `s_configured` bool that could survive editor no-domain-reload after native SignalBus storage was disposed.
Solution: `EnsureConfigured()` now trusts `s_configured` only when both command and status lanes still expose native storage.
Rejected Alternatives: Forcing reconfigure every call would add unnecessary bootstrap churn; trusting the bool risks dead lanes after editor lifecycle reset.
Scalability potential: Low/Middle/High/Ultra keep the same bounded lane capacities; this only protects lifecycle correctness.
Hardware Impact: Two pure readiness probes during cold setup; runtime hot cost remains 0 us.

Problem: Physical receiver unregister used the current serialized `activationCollider` instead of the collider actually registered.
Solution: Store `_registeredCollider` on successful registration in airlock, seat, and toggle, and unregister that exact instance.
Rejected Alternatives: Assuming serialized references never change is weak in multi-agent scene/prefab work and can leave stale receiver slots.
Scalability potential: All tiers keep fixed receiver registry behavior; high-detail cockpit prefabs can swap authoring references without stale runtime identity.
Hardware Impact: One cached reference field per controller; prevents stale registry probes without hot-loop cost.

Problem: Airlock interaction still dispatched hand IK target from `QueueSealToggle()`, which is a command callback, not a named visual/simulation phase.
Solution: Remove command-path `DispatchHandTarget()` and keep IK target dispatch in `IFixedTickable.FixedTick` while the hatch is moving.
Rejected Alternatives: Immediate IK felt responsive but violated phase ownership and made APEX timing proof weaker.
Scalability potential: Low gets deterministic fixed-phase handle target; High/Ultra can layer richer hand/handle polish without adding callback side effects.
Hardware Impact: Removes one command-path presentation call; expected saving is single-digit us per interaction and cleaner phase behavior.

Problem: Emergency cabin lights were phase-correct but still wrote `Light.intensity` and shader globals every `LateFrameTick` after the status had settled.
Solution: Add `_lightingDirty`, `_lastAppliedEmergency01`, and `LightingApplyEpsilon` so status snapshots are still drained every late frame, while GPU/global writes are skipped when the visual weight is unchanged.
Rejected Alternatives: Moving status consumption to a slower lane would risk missing same-frame cockpit state; keeping unconditional writes burns driver-facing calls for no visual gain.
Scalability potential: Low/Middle skip stable writes aggressively; High/Ultra can add richer emergency color curves later without changing signal DTOs or state ownership.
Hardware Impact: Stable emergency/cabin state now costs one late-frame signal snapshot drain and branch only; estimated low-end i3/MX350 saving is 5-40 us per frame depending on light count and driver overhead.

Problem: A pure epsilon gate can leave final lighting at 0.999x after an emergency transition, which is visually harmless but contractually imprecise for cockpit state.
Solution: When the transition enters epsilon, mark lighting dirty once, snap `_emergency01` to `_targetEmergency01`, and apply the exact final light/shader state.
Rejected Alternatives: Accepting an approximate terminal value makes future tests and authored material thresholds less deterministic.
Scalability potential: All tiers get exact terminal states; richer tiers can layer flicker after this stable base without hidden residual weights.
Hardware Impact: Adds one final settled write per transition; removes all following redundant writes until the next status edge.

Problem: Dashboard text rendering was zero-GC but still rewrote identical TMP character arrays every refresh interval, forcing avoidable text mesh work while the numeric values stayed unchanged.
Solution: Track `_textDirty`, `_lastRenderedStatusId`, and last rounded O2/velocity/hull metric values; skip `SetCharArray` for unchanged text while keeping `ApplyNeedles()` active for analog jitter.
Rejected Alternatives: Stopping the whole dashboard refresh would freeze needle motion; using string diffing would violate the zero-GC text path.
Scalability potential: Low/Middle avoid redundant mesh updates; High/Ultra can keep higher needle cadence and richer jitter without paying text rewrite cost when values are stable.
Hardware Impact: Stable panel text now avoids TMP mesh updates; estimated low-end i3/MX350 saving is 10-80 us per dashboard refresh depending on TMP object count and material state.

Problem: Disabling cockpit components mid-motion could leave the hatch, switch, or camera in a partial visual state while the authoritative local bools had already stopped advancing.
Solution: Airlock disable snaps to committed `_sealed`; toggle disable snaps to committed `_isOn`; seat disable performs a local abort that restores the captured start camera pose and clears transit without publishing new status or feedback.
Rejected Alternatives: Continuing motion while disabled is impossible; publishing abort/status from `OnDisable` would create noisy lifecycle side effects and possible duplicate downstream signals.
Scalability potential: All device tiers get deterministic re-enable visuals; high-detail prefabs can be toggled during editor/runtime iteration without half-state cockpit artifacts.
Hardware Impact: One local snap only on disable; steady-frame cost is 0 us and it prevents player/camera recovery work after lifecycle churn.

Problem: Seat controller dispatcher hot-swap re-registration ignored `_feedbackPending`, so fail-closed or completion feedback queued for `LateFrameTick` could be stranded if the dispatcher service was replaced outside active transit/hover state.
Solution: Include `_feedbackPending` in the dispatcher replacement registration condition and add a static audit that verifies the condition.
Rejected Alternatives: Emitting feedback immediately from the command path would violate VISUAL_SYNC ordering; dropping feedback would make fail-closed interaction feel dead.
Scalability potential: All tiers keep one late-frame sensory route; high-tier richer feedback remains safe because the registration condition is still local value state.
Hardware Impact: One extra boolean check only during service replacement; steady-frame cost is 0 us.

Problem: Seat transit restored the entire `InputDispatcher` block mask from a cached value, which can erase unrelated input blocks set by another system while the player is sliding into the seat.
Solution: Define `TransitInputBlockMask`, set only those bits on block, and restore only those bits while preserving foreign bits already present in the current dispatcher mask.
Rejected Alternatives: A full owner-token stack would require changing `InputDispatcher` outside this domain; whole-mask restore is simpler but unsafe in a multi-agent/global-input architecture.
Scalability potential: All tiers preserve deterministic control lock behavior; richer cockpit transitions no longer risk unblocking unrelated UI/tool locks.
Hardware Impact: Two extra bitwise operations on transit end only; steady-frame cost is 0 us.

Problem: The first masked restore still treated every transit bit as owned by the seat, which can clear movement/look/tool/discrete blocks that were already set before the player touched the cockpit seat.
Solution: Track `_ownedInputBlockBits = TransitInputBlockMask & ~currentMask` at block time, then restore only those owned bits while leaving pre-existing and newly added foreign bits intact.
Rejected Alternatives: Snapshot restore and blanket transit-bit clear both violate multi-owner input discipline; changing `InputDispatcher` to a full token stack is outside this DropPod domain.
Scalability potential: Low/Middle/High/Ultra keep identical deterministic control truth; richer cockpit presentation cannot accidentally unlock another system's modal/tool lock.
Hardware Impact: Adds two bitwise operations on transit start/end only; steady-frame cost is 0 us.

Problem: Seat transit could proceed if `InputDispatcher` was unavailable, causing a diegetic camera slide while player controls remained live.
Solution: Convert `BlockInput()` into `TryBlockInput()` and fail closed before `_transiting = true` when input cannot be blocked; feedback remains queued for `LateFrameTick`.
Rejected Alternatives: Allowing transit without input isolation risks movement/camera conflict; introducing a new global input token API would be cross-domain scope creep.
Scalability potential: Low devices get predictable fail-closed safety; Middle/High/Ultra can add richer denied feedback without changing phase ownership.
Hardware Impact: One cold dispatcher check on transit start; prevents control contention and has 0 us steady-frame cost.

Problem: Airlock `QueueSealToggle()` could publish `AirlockMoving` even when the hatch target was already reached.
Solution: If `_moving` is false after target resolution, publish committed `AirlockSealed`/`AirlockOpen` status and skip moving feedback registration.
Rejected Alternatives: Letting dashboard/lighting see a false moving edge creates presentation drift; forcing a tiny fake motion would be dishonest state.
Scalability potential: All tiers get exact cockpit state; high-tier polish can still add authored click feedback on real transitions only.
Hardware Impact: Removes unnecessary tick registration and status churn for no-op presses; expected saving is low single-digit us per no-op interaction.

Problem: Interactive cockpit motion could be accepted before the required dispatcher tick registration was proven, leaving airlock, toggle, or camera transit state unable to advance.
Solution: Make `TryRegisterTicks()`/`TryRegisterLate()` return success and require those results before publishing moving state or mutating committed toggle/transit state.
Rejected Alternatives: Letting motion start and hoping dispatcher recovers creates half-state cockpit faults; adding a new scheduler is unnecessary when the first-party dispatcher already owns phases.
Scalability potential: Low devices get strict fail-closed behavior; Middle/High/Ultra can layer richer visual/audio denial without changing command DTOs or phase routes.
Hardware Impact: One dispatcher readiness check per interaction edge; steady-frame cost remains 0 us and prevents recovery work from stuck cockpit motion.

Problem: Seat motor service can appear during an active camera transit; without re-registering the fixed phase, the camera could keep sliding while the physical seat-lock body remains unsynchronized.
Solution: On `GlobalRegistryServiceSlot.PlayerMotor` replacement, re-register ticks when `_transiting`; `TryRegisterTicks()` requires fixed registration when `_seatLockMotor.HasControllableBody`.
Rejected Alternatives: Ignoring motor hot-swap is cheaper but creates a split between visual transit and physical body lock; polling the registry from `LateFrameTick` would violate hot-path DI rules.
Scalability potential: All tiers keep one authoritative seat-lock route; high-tier cockpit polish remains decoupled from physical body ownership.
Hardware Impact: One service-replacement branch only; no steady-frame cost and no registry hot polling.

Problem: Dispatcher-phase compliance was implemented in runtime code but not guarded against future raw Unity scheduler or LINQ regressions in the DropPod domain.
Solution: Add a static audit that rejects raw `Update`, `FixedUpdate`, `LateUpdate`, coroutines, `System.Linq`, and common LINQ terminal/filter operators in DropPod runtime.
Rejected Alternatives: Human review only is weak under 20+ parallel agents; runtime wrappers would add surface without preventing source drift.
Scalability potential: All tiers keep the same phase budget route; future high-tier visual overkill must still enter through dispatcher phases.
Hardware Impact: Runtime impact is 0 us; editor-only audit prevents accidental hot-path allocations and hidden scheduling.

Problem: Dashboard and emergency lighting consumers used frame-only status cursors while `DropPodStatusSignal` already carried `Sequence`, so same-frame multi-producer status snapshots could let an older status overwrite a newer cockpit state if the lane order changed.
Solution: Move sequence ownership into `DropPodSignalLaneBootstrap.NextSequence()` with an interlocked global counter, remove per-controller `_sequence` fields, and make status consumers accept only newer `(Frame, Sequence)` pairs.
Rejected Alternatives: Sorting the SignalBus snapshot would mutate a shared core lane and add cross-domain risk; status-priority heuristics would hide true publish order and break future cockpit commands.
Scalability potential: Low/Middle/High/Ultra all keep the same 16-byte DTO ABI; richer cockpit status spam remains bounded by the lane and ordered without extra allocations.
Hardware Impact: One `Interlocked.Increment` per interaction/status publish edge only; steady-frame dashboard and lighting cost remains branch-only, with no heap allocation and no DataVault write lock in DropPod code.

Problem: `SignalBusRegistry.DisposeAll()` resets typed lane configuration, but active DropPod components can survive no-domain-reload/editor lifecycle edges and publish later.
Solution: Make `DropPodSignalLaneBootstrap.NextSequence()` repair the DropPod lane contract through `EnsureConfigured()` before any command/status publish receives a sequence number.
Rejected Alternatives: Trusting `SignalBus<T>.TryPush` would initialize the lane with generic defaults; adding a global registry callback would overreach outside DropPod ownership.
Scalability potential: All tiers preserve the same command/status capacities and 16-byte ABI after editor lifecycle churn; no gameplay truth or DTO layout changes.
Hardware Impact: Normal publish edge pays two pure storage checks behind the configured fast path plus one interlocked sequence increment; steady-frame cost is 0 us.

Problem: The global `ushort` sequence cursor could wrap inside a frame and make a newer same-frame DropPod status compare lower than an older one; the reset path also had a compare-exchange race that could clamp contested publishes to `65535`.
Solution: Replace the global increment/reset path with `NextSequence(uint frame)`, storing `(frame, sequence)` in one interlocked `long` state. Sequence resets to 1 only when the frame changes and saturates on impossible same-frame overflow instead of wrapping.
Rejected Alternatives: Widening `DropPodStatusSignal.Sequence` to `uint` would break the existing 16-byte DTO ABI; sorting SignalBus snapshots would mutate shared signal-core behavior outside this domain.
Scalability potential: Low/Middle/High/Ultra keep the same DTO size and lane budgets. High/Ultra cockpit status bursts remain ordered without per-frame allocations or a shared lock.
Hardware Impact: One `Interlocked.Read` plus CAS loop per publish edge only; no steady-frame cost. It removes a rare ordering fault without adding DataVault locks or heap state.

Problem: Dashboard and emergency-light status consumers retained `_lastStatusFrame` and `_lastStatusSequence` across `OnDisable`/`OnEnable`, so a component surviving editor no-domain-reload or runtime lifecycle churn could ignore fresh lower-frame status snapshots after reactivation.
Solution: Add `ResetStatusCursor()` to both consumers and call it from `OnEnable()` before any status snapshot drain or first render/apply pass.
Rejected Alternatives: Reading latest status from a global heap/DataVault would add an unnecessary ownership route; periodically forcing status heartbeats would create extra signal traffic without fixing the stale local cursor.
Scalability potential: Low/Middle/High/Ultra all keep the same 16-byte status DTO and snapshot route. Low devices get no extra frame work; high-tier cockpit polish can re-enable richer panels/lights without stale state suppression.
Hardware Impact: Two field writes on enable only; steady-frame cost is 0 us. It prevents recovery churn from a dead dashboard/light state on i3/MX350 class CPUs.

Problem: Dashboard serialized/runtime metrics and emergency lighting tunables could accept non-finite floats, allowing NaN values to reach TMP numeric output, needle rotations, `Light.intensity`, or shader globals.
Solution: Add local sanitizers for percent, velocity, transition sharpness, intensity, and LED color. Runtime values are clamped to finite ranges before presentation writes while preserving zero-GC char-buffer and visual-fake routes.
Rejected Alternatives: Relying on `[Range]` is editor-only and does not protect runtime tuning, corrupted prefab state, or service-driven values. Adding a global validator would overreach outside the DropPod presentation domain.
Scalability potential: Low/Middle/High/Ultra all get deterministic finite presentation writes. Low devices avoid recovery from poisoned transforms/lights; high-tier visual overkill can safely increase jitter/lighting intensity without propagating NaN.
Hardware Impact: A few scalar finite checks per dashboard refresh and lighting frame; expected under 2 us on i3/MX350. The avoided cost is catastrophic NaN propagation, not a hot-loop optimization target.

Problem: Seat transit validated only start/end positions, so a poisoned Bezier control anchor or invalid seat rotation could make the camera path produce non-finite transforms while still publishing a completed cockpit state.
Solution: Fail closed in `TryBeginTransit()` unless start/control/end positions and start/end rotations are finite; sanitize `ResolveSeatRotation()` by falling back to identity-safe rotation and rejecting degenerate/non-finite forward vectors before NLERP.
Rejected Alternatives: Clamping only the final sampled spline would hide broken cockpit authoring and still let bad rotations leak into camera orientation. Adding physics/joint correction would be unnecessary for a presentation spline.
Scalability potential: Low/Middle/High/Ultra all preserve the same deterministic seat route. Low devices avoid NaN recovery stalls; high-tier camera shake/polish can stack on top because the base transit pose is finite before visual overkill begins.
Hardware Impact: Six finite checks on transit start plus one rotation/forward guard when resolving the seat anchor; steady-frame cost is 0 us. Expected edge-time cost under 2 us on i3/MX350, with catastrophic transform poisoning eliminated.

Problem: Serialized Euler values for hatch/switch authoring could contain NaN/Inf and poison `Quaternion.Euler` before any runtime finite guards saw the resulting local rotation.
Solution: Centralize the guard in `DropPodSplineMath.ResolveLocalEulerNoAlloc()`, returning `Quaternion.identity` for non-finite authoring vectors before creating the quaternion.
Rejected Alternatives: Trusting inspector constraints or per-controller duplicate clamps would miss corrupted prefab/runtime-tuned values and increase maintenance surface.
Scalability potential: Low/Middle/High/Ultra all get deterministic finite cockpit presentation; richer tiers can add more authored switch/hatch poses without expanding failure modes.
Hardware Impact: Cold authoring-rotation guard only during cache/setup paths; steady-frame cost is 0 us. Expected edge-time cost under 1 us on i3/MX350.

Problem: `DropPodDashboardToggleSwitch.AdvanceVisualMotion()` sanitized `deltaTime` but not `motionSeconds` or the computed step, so NaN/Inf duration could keep `_moving` true and the switch registered in `LateFrameTick` forever.
Solution: Add `ResolveMotionDuration()` and a non-finite `next` snap that commits visual position to `_target01` and stops motion in the same visual-sync lane.
Rejected Alternatives: Relying on `[Range]` is editor-only; forcing disable cleanup would leave a live stuck visual lane until lifecycle churn; adding physics correction is wrong for a switch visual fake.
Scalability potential: Low devices avoid a stuck late-frame branch; Middle/High/Ultra can add richer switch geometry/material polish while preserving the same zero-GC scalar motion kernel.
Hardware Impact: One finite check and helper call only while the switch is actively moving; estimated under 1 us on i3/MX350, with 0 us steady-state after completion.

Problem: Airlock hatch motion and IK target dispatch trusted serialized/runtime `sealSeconds`, `handTargetHoldSeconds`, and `handTargetBlend`; NaN, Inf, or absurd finite values could stall fixed-phase hatch motion or send invalid hand-target timing to the physical interaction layer.
Solution: Clamp `sealSeconds` through `ResolveSealDuration()`, snap non-finite fixed-step output to `_targetSeal01`, and sanitize IK hold/blend values before constructing `PhysicalHandIkTarget`.
Rejected Alternatives: Inspector `[Range]` does not protect corrupted prefab data or runtime tuning; moving IK dispatch to presentation phase would break the physical hand-off contract; adding hinge physics would be overkill for an authored hatch fake.
Scalability potential: Low devices get fail-closed hatch completion without extra physics; Middle/High/Ultra can add richer hatch material/light/audio polish on top of the same finite scalar seal state.
Hardware Impact: Two scalar finite checks on hatch motion only while moving and two clamps on interaction edge; estimated under 2 us on i3/MX350, with 0 us steady-frame cost after settle.

Problem: Dashboard text cadence and needle authoring values trusted `lowTierRefreshSeconds`, `highTierRefreshSeconds`, `needleSweepDegrees`, and jitter inputs; non-finite values could force per-frame text work or poison cockpit needle rotations.
Solution: Resolve refresh cadence through finite-clamped low/high values, sanitize quality before interpolation, clamp needle sweep to finite non-negative degrees, and zero non-finite jitter before `Quaternion.Euler`.
Rejected Alternatives: Turning the panel off would hide state instead of fixing it; clamping after transform writes would still leak poisoned rotations; using strings or allocations for diagnostics violates the existing zero-GC diegetic text path.
Scalability potential: Low devices stay on bounded refresh cadence; Middle/High/Ultra can increase analog jitter and richer panel visuals without risking non-finite transform state or text churn.
Hardware Impact: A few scalar checks per dashboard refresh and per needle update; estimated under 2 us on i3/MX350, with avoided cost from per-frame churn and NaN transform recovery.

Problem: `DropPodSeatController.FixedTick()` returned silently when the seat-lock motor disappeared or lost controllable-body support during transit, leaving a stale fixed-phase registration until some later lifecycle edge cleaned it up.
Solution: Unregister fixed phase directly from `FixedTick()` when the motor route is unavailable, and route `PlayerMotor` hot-swap through `RefreshSeatLockMotorRegistration()` so valid motors re-register and invalid/null motors drop the fixed lane.
Rejected Alternatives: Keeping the no-op fixed tick is cheap but violates scheduler hygiene; polling `GlobalRegistry.PlayerMotor` from fixed phase would violate cold dependency rules; adding a new motor heartbeat signal is unnecessary for this local owner edge.
Scalability potential: Low devices avoid permanent no-op fixed scheduler work; Middle/High/Ultra retain the same physical seat-lock synchronization when the motor is present and can add richer camera polish without hidden fixed-phase drift.
Hardware Impact: Saves a no-op fixed callback after motor loss; estimated low-end i3/MX350 saving is 1-4 us per fixed frame in the fault state, with zero steady-frame cost when the motor route is healthy.

Problem: `DropPodSeatController.LateFrameTick()` fed serialized `transitSeconds` directly into spline time resolution. Non-finite values were already handled by shared math, but a huge finite runtime value could keep the camera transit and late-frame registration alive for an unreasonable duration.
Solution: Add `ResolveTransitDuration()` with explicit min/max/fallback constants matching the authored range and call it before `ResolveTransitT()`.
Rejected Alternatives: Trusting `[Range]` is editor-only; adding a timeout in `CompleteTransit()` would hide the poisoned input later; changing shared `SanitizeDuration()` globally risks altering unrelated math semantics.
Scalability potential: Low devices avoid long-lived camera-lane work from corrupted prefabs; Middle/High/Ultra retain the same cinematic seat slide and can layer richer shake/lighting without unbounded timing.
Hardware Impact: One scalar range sanitize per active `LateFrameTick` during seat transit; estimated under 1 us on i3/MX350, with avoided minutes-long scheduler drift under bad authoring.

Problem: Airlock opening published `AirlockOpen` as soon as `_seal01` dropped below the sealed threshold, while the hatch was still visibly moving. Dashboard text and emergency lighting could therefore present settled-open state during active travel.
Solution: Defer `AirlockSealed`/`AirlockOpen` publication until `_moving` becomes false in the fixed phase, but mark `_sealed = false` immediately after a registered opening request so seat entry fails closed during the motion.
Rejected Alternatives: Keeping early `OPEN` made presentation drift; delaying `_sealed` until final open kept a short unsafe window where seat entry could still look available.
Scalability potential: Low keeps one cheap scalar hatch fake; Middle/High/Ultra can add richer hatch/audio polish without changing the authoritative safety bit or status DTO.
Hardware Impact: No steady-frame cost. One branch on interaction edge and one final status publish; expected under 1 us on i3/MX350 while removing dashboard/light state churn during hatch travel.

Problem: Haptic quality, audio volume/pitch, normalized visual values, and late-frame time deltas still trusted finite inputs. NaN quality or authoring audio fields could poison haptics/audio, and non-finite dispatcher time could stall seat/dashboard/lighting presentation.
Solution: Add shared `DropPodSplineMath.SanitizeUnit01` and `SanitizeRange`, route feedback/audio/quality through them, and clamp raw late-frame deltas through explicit `math.isfinite(rawDeltaSeconds)` guards.
Rejected Alternatives: Unity `[Range]` protects only normal inspector edits; `math.saturate`/`math.clamp` alone do not make a clear non-finite contract for runtime-tuned values.
Scalability potential: Low devices avoid non-finite recovery and stuck scheduler lanes; Middle/High/Ultra can push stronger haptic/audio/needle/light polish through the same continuous quality path.
Hardware Impact: Scalar checks only on interaction/feedback edges and existing late-frame presentation ticks; expected under 2 us on i3/MX350, with catastrophic NaN propagation removed.

Problem: Disabling `DropPodEmergencyLightingController` while emergency lighting was active left external `Light` intensities and shader globals at the last applied emergency value even though the owner no longer ticked.
Solution: `OnDisable()` now calls `ClearPresentationLighting()`, applying neutral cabin lighting and marking the write gate dirty so re-enable reapplies the current internal weight deterministically.
Rejected Alternatives: Leaving globals untouched creates stale scene-wide presentation state; zeroing all lights would make a disabled/re-enabled cockpit flash black instead of returning to the neutral baseline.
Scalability potential: All tiers clear emergency presentation ownership deterministically; high-tier richer LED/global material effects can be added behind the same owner cleanup.
Hardware Impact: One disable-edge light/shader write pass only. Steady-frame cost is 0 us; prevents stale global material work and red-light drift after lifecycle churn.

Problem: Airlock interaction could reverse an opening hatch but could not reverse a closing hatch because `_sealed=false` dominated the next-target decision while the hatch was moving toward sealed.
Solution: Route the next target through `ResolveNextSealTarget()`: active motion flips against `_targetSeal01`, settled motion uses committed `_sealed`.
Rejected Alternatives: Keeping one-way closing motion makes physical/dashboard input feel ignored; using a new hatch state enum would be unnecessary surface for a two-state scalar visual fake.
Scalability potential: Low devices keep the same cheap scalar hatch fake; Middle/High/Ultra can add richer latch, light, and audio polish while the target resolver remains deterministic.
Hardware Impact: One branch on interaction edge only, 0 us steady-frame cost. Prevents user-input retry churn and false stuck-closing perception without adding scheduler work.

Problem: Dashboard text and emergency lighting reset their status cursors on enable, then performed the first visible text/light write before draining the current status snapshot.
Solution: Drain `SignalBus<DropPodStatusSignal>` immediately after `ResetStatusCursor()` and before `RenderNow()` or forced lighting apply.
Rejected Alternatives: Waiting for the next `LateFrameTick` leaves a visible one-frame stale cockpit state; pulling state from a global heap/DataVault would add a second ownership route.
Scalability potential: Low/Middle/High/Ultra all keep the same 16-byte signal route and zero-GC value transfer; richer panel/light presentation no longer starts from stale local state after re-enable.
Hardware Impact: One bounded SignalBus snapshot scan on enable only; steady-frame cost is 0 us. Expected edge cost under 5 us on i3/MX350 with stale first-frame presentation removed.

Problem: Destroy paths did not mirror disable cleanup for active cockpit presentation motion. Removing a component mid-motion could leave hatch, switch, or camera transforms in a partial pose after the owner disappeared.
Solution: Airlock `OnDestroy()` now clears hand IK target, pending feedback, hover/motion flags, and snaps to committed seal state. Toggle `OnDestroy()` clears motion/feedback and snaps to committed switch state. Seat `OnDestroy()` aborts local transit, restores input, and clears pending feedback fields.
Rejected Alternatives: Assuming `OnDisable()` always runs first is weak under Unity lifecycle/editor component removal; publishing abort/status from destroy would create noisy lifecycle side effects.
Scalability potential: All tiers get deterministic cleanup under prefab iteration and runtime component teardown; high-tier cockpit polish can add more visuals without creating orphaned transform states.
Hardware Impact: Destroy-edge cleanup only; steady-frame cost is 0 us. Avoids camera/input recovery work and half-state cockpit artifacts after lifecycle churn.

Problem: Emergency lighting destroy still differed from disable cleanup. A destroyed owner could leave scene lights and shader globals in the last emergency state if disable cleanup was bypassed by lifecycle order.
Solution: Call `ClearPresentationLighting()` from `DropPodEmergencyLightingController.OnDestroy()` before unregistering. The owner now neutralizes external presentation writes on both disable and destroy.
Rejected Alternatives: Trusting Unity to always invoke `OnDisable()` first is not strong enough for editor component removal and prefab iteration under parallel agents.
Scalability potential: Low/Middle/High/Ultra all get deterministic external-light ownership cleanup. Higher tiers can add stronger emergency LED visuals without increasing stale-global failure modes.
Hardware Impact: Destroy-edge write pass only; steady-frame cost is 0 us. Prevents red-light/global shader drift after teardown.

Problem: `DropPodSplineMath.ResolveNlerp()` treated finite zero quaternions as valid inputs. Blending two degenerate quaternions returned a zero quaternion instead of a valid rotation, and `ApplyNeedle()` still used raw `math.saturate(value01)`.
Solution: `ResolveNlerp()` now returns `Quaternion.identity` when the blended quaternion length is degenerate or non-finite. Dashboard needle values now pass through `DropPodSplineMath.SanitizeUnit01(value01)` before rotation math.
Rejected Alternatives: Relying on every caller to provide normalized quaternions and pre-sanitized needle values leaves the shared math kernel fragile. Adding a new math abstraction would be unnecessary.
Scalability potential: Low devices avoid poisoned transforms and recovery stalls; Middle/High/Ultra can layer richer camera/needle jitter because the shared rotation kernel fails to a finite identity.
Hardware Impact: One scalar length check inside existing NLERP calls and one value sanitize per needle write; estimated under 1 us on i3/MX350 while eliminating catastrophic zero-rotation propagation.

Problem: `DropPodAirlockController.QueueSealToggle()` could leave a fixed or late dispatcher registration alive until the next callback if fixed registration succeeded and late registration failed, or vice versa.
Solution: Call `UnregisterTicks()` immediately on the `TryRegisterTicks()` failure branch before publishing fail-closed status.
Rejected Alternatives: Waiting for the next fixed/late callback to self-unregister is cheaper in code but leaves a stale scheduler slot after a failed interaction edge.
Scalability potential: Low devices avoid dead no-op callbacks; Middle/High/Ultra keep the same hatch scalar fake and can add richer latch/audio polish without scheduler residue.
Hardware Impact: Failure-edge cleanup only; steady-frame cost is 0 us. Fault-state saving is one avoided no-op dispatcher callback, roughly 1-4 us on i3/MX350.

Problem: Emergency lighting re-enable applied the old internal `_emergency01` after draining status, so the first visible frame after component reactivation could show stale red/cabin lighting before the late-frame transition corrected it.
Solution: After `DrainStatusSignals()` in `OnEnable()`, snap `_emergency01` to `_targetEmergency01` before the forced apply; `ClearPresentationLighting()` also updates `_lastAppliedEmergency01 = 0f` to keep the write gate aligned with the external light/shader state it just wrote.
Rejected Alternatives: Preserving a hidden disabled-state transition is not valuable; visible re-enable correctness is stronger than animating from stale internal state.
Scalability potential: Low/Middle avoid false emergency light flicker; High/Ultra can run stronger LED/shader response with the same owner cleanup and no stale-global drift.
Hardware Impact: One field assignment on enable/clear only; steady-frame cost is 0 us. Prevents one-frame stale presentation and redundant recovery writes.

Problem: Airlock IK blend sanitization duplicated finite/saturate logic while shared normalized sanitization already existed for DropPod presentation inputs.
Solution: Route `ResolveHandTargetBlend()` through `DropPodSplineMath.SanitizeUnit01(blend)`.
Rejected Alternatives: Keeping duplicate guards is safe today but increases drift risk as the shared finite contract evolves.
Scalability potential: All tiers share one normalized input guard for IK and dashboard values; richer hand polish remains finite by default.
Hardware Impact: Same scalar cost as the old guard, under 1 us on i3/MX350, with lower maintenance drift.

Problem: Dashboard toggle haptics had a narrower fallback than airlock/seat. If a future, corrupted, or cast-invalid `PhysicalHandSide` reached the switch feedback path, the old inline ternary routed the pulse to the right motor only.
Solution: Add `BothMotorMask` and `ResolveMotorMask()` to `DropPodDashboardToggleSwitch`, matching the existing airlock/seat pattern: left -> left motor, right -> right motor, unknown -> both motors.
Rejected Alternatives: Keeping the inline ternary is smaller but hides invalid/future hand-side input as a right-hand-only feedback artifact. Expanding `PhysicalHandSide` is cross-domain and unnecessary for this local defensive route.
Scalability potential: Low devices get deterministic feedback without extra systems; Middle/High/Ultra can strengthen haptic amplitude through existing continuous quality scaling without changing command/status ownership.
Hardware Impact: One branch chain on feedback completion only, 0 us steady-frame cost. Estimated edge cost under 1 us on i3/MX350 while preventing misleading one-sided cockpit feedback.

Problem: `DropPodCabinColliderValidator` enforced the no-MeshCollider cabin rule through an array-returning `GetComponentsInChildren<MeshCollider>(true)` call. It was editor-only, but still a sloppy validator path for a domain whose contract is "no heavy MeshCollider inside the cabin."
Solution: Replace the array-returning scan with a static scratch `List<MeshCollider>` and clear it through `finally` after the count. Add a static audit that rejects the old array route.
Rejected Alternatives: Keeping the allocation is acceptable for rare menu execution but contradicts the zero-waste tooling style in this domain. Moving the rule into runtime would be worse because cabin collider hygiene is an authoring/editor validation concern.
Scalability potential: Low/Middle builds keep primitive/compound collider discipline. High/Ultra can add more primitive collider detail without letting artists regress to concave cabin MeshColliders.
Hardware Impact: Runtime impact is 0 us. Editor validation avoids one transient MeshCollider array allocation per scan and keeps the proof tool cheap on i3/MX350-class authoring machines.

Problem: A player/camera service hot-swap or teardown during active seat transit could leave `_cameraTransform` null while the late-frame spline timer continued. That path could eventually publish `Seated` even though no camera pose had been applied.
Solution: Add `AbortTransitForLostCameraRoute()` and call it from `LateFrameTick()` before spline time advances. The abort restores input, publishes `AbortTransit` plus fail-closed status, queues denied feedback, and unregisters the fixed motor lane.
Rejected Alternatives: Completing the transit with a missing camera route is false state. Aborting directly in the registry callback would move presentation failure handling out of the visual-sync phase. Polling `GlobalRegistry.Player` from `LateFrameTick` would violate cold dependency rules.
Scalability potential: Low devices avoid stuck or false-complete camera lane work. Middle/High/Ultra keep the cinematic seat slide when the camera route is valid and can layer stronger cockpit shake later because the base route now fails closed.
Hardware Impact: One null branch only during active transit; estimated under 1 us on i3/MX350. Fault path saves the remaining spline/camera work and avoids player input recovery churn after a broken transit.

Problem: The editor scratch `List<MeshCollider>` was intentionally cold but lacked the mandated allocation owner comment.
Solution: Add the canonical `COLD ALLOC: List<MeshCollider>[16]` comment and static audit assertion.
Rejected Alternatives: Leaving it undocumented makes future allocation scans noisy. Moving the list to a per-call local would reintroduce managed allocation.
Scalability potential: Tooling remains cheap across Low/Middle/High/Ultra authoring workstations; richer collider primitive detail can be validated without regressing to array allocations.
Hardware Impact: Runtime impact is 0 us. Editor proof remains one reused list and one clear/finally path.

Problem: Dashboard needle authoring accepted every finite value. A corrupted prefab/runtime tune could rotate cockpit needles through massive Euler angles even though the serialized UI contract is 1-220 degrees sweep and 0-12 degrees jitter.
Solution: Add `MaxNeedleSweepDegrees` and `MaxNeedleJitterDegrees`, route sweep/jitter through `DropPodSplineMath.SanitizeRange`, and clamp final jitter symmetrically before the local rotation write.
Rejected Alternatives: Trusting `[Range]` attributes is editor-only hygiene, not runtime safety. Adding a new dashboard state object would be unnecessary for two scalar guards.
Scalability potential: Low devices keep cheap bounded needle motion; Middle/High/Ultra can increase dashboard visual polish without risking corrupted transform rotations.
Hardware Impact: Two scalar range clamps per dashboard render cadence, not every frame when the panel is stable. Expected under 1 us on i3/MX350.

Problem: The dashboard text `Append()` helper assumed callers always pass a valid cursor. A future metric extension could pass a negative or over-capacity cursor and return a noncanonical length.
Solution: Saturate the cursor in `Append()` before copying: null buffer returns 0, over-capacity returns buffer length, negative cursor becomes 0.
Rejected Alternatives: Leaving the helper fragile keeps every future call site responsible for the same bounds proof. Allocating a larger fallback buffer violates the fixed char-buffer contract.
Scalability potential: All tiers keep the same fixed `char[40]` path and avoid emergency string/overflow fallbacks.
Hardware Impact: Three branch guards inside existing text formatting cadence. Runtime cost is below measurement noise and prevents invalid `SetCharArray` lengths.

Problem: Emergency lighting accepted finite but unbounded intensity/color channels. A corrupted inspector value could overdrive Unity `Light.intensity` or shader global LED color far beyond the authored drop-pod range.
Solution: Cap light intensity with `DropPodSplineMath.SanitizeRange(value, 0f, MaxLightIntensity, 0f)` and clamp finite LED RGBA channels through `SanitizeUnit01`.
Rejected Alternatives: Allowing HDR emergency color here is not required by the current material contract and weakens low-end stability. Moving the clamp to shader only would still overdrive `Light` components.
Scalability potential: Low/Middle keep stable cabin lighting; High/Ultra can later add explicit HDR controls behind a named material contract instead of accidental inspector corruption.
Hardware Impact: Two scalar light clamps plus four color clamps only when lighting writes are dirty or forced. Stable-frame cost remains 0 us because `ApplyLightingIfNeeded` still gates writes.

Problem: `DropPodSplineMath.ResolveLocalEulerNoAlloc()` rejected NaN/Inf authoring rotations but still accepted any huge finite Euler value. That left hatch and dashboard switch rotations vulnerable to corrupted prefab/runtime tune values.
Solution: Add `MaxAuthoringEulerDegrees = 360f`, sanitize each Euler component through `SanitizeAuthoringEulerDegrees()`, and pass the bounded vector to `Quaternion.Euler`.
Rejected Alternatives: Clamping separately in airlock and toggle would duplicate policy. Trusting Unity's quaternion conversion for huge finite values keeps the shared math kernel permissive.
Scalability potential: Low devices avoid expensive-looking cockpit rotation corruption and recovery; Middle/High/Ultra keep normal authored rotations while future richer cockpit motion inherits the same bounded math route.
Hardware Impact: Three scalar clamps during cold rotation cache only. Runtime steady-frame impact is 0 us.

Problem: `DropPodDashboardToggleSwitch.Toggle()` always set `_moving = true`. A rapid reverse press could target the current switch position, causing a phantom late-frame motion and delaying click feedback by one frame.
Solution: Reject disabled invocations and compute `_moving = math.abs(_target01 - _position01) > 0.001f;` after target selection.
Rejected Alternatives: Leaving `AdvanceVisualMotion()` to settle on the next frame costs a pointless scheduler callback and makes interaction feedback less immediate. Adding a switch state enum would be surface area without value.
Scalability potential: Low devices avoid dead presentation ticks; Middle/High/Ultra keep responsive physical switch feedback while richer haptics/audio stay on the same zero-GC edge route.
Hardware Impact: One scalar absolute comparison on interaction edge. It saves one `LateFrameTick` callback in the no-motion reversal edge, roughly 1-3 us on i3/MX350.

Problem: Active airlock, dashboard toggle, or seat transit motion could lose its dispatcher route during a `GlobalRegistryServiceSlot.Dispatcher` hot-swap. Existing code cleared registration flags and waited for a future route, which could strand `_moving` or `_transiting` state, leave seat input blocked, or leave a partially moved physical cockpit control when the dispatcher was null or refused registration.
Solution: On dispatcher replacement, active motion now takes a hard local route: if re-registration succeeds, it continues; if the dispatcher is null or `TryRegisterTicks()` fails, airlock snaps back to committed seal state and clears hand IK, dashboard toggle snaps to committed switch state, and seat aborts local transit while restoring only its owned input block bits. Pending feedback is cleared and no callback-phase audio/haptic/status emission is performed.
Rejected Alternatives: Waiting for `LateFrameTick` after dispatcher loss may never execute. Publishing fail-closed feedback from the hot-swap callback would violate VISUAL_SYNC sensory timing. Polling `GlobalRegistry.Dispatcher` from active hot paths would violate cold dependency rules.
Scalability potential: Low devices avoid stuck scheduler lanes and input lock recovery cost. Middle/High/Ultra keep the cinematic motion when the route is healthy and can add richer visual/audio polish because the failure branch stays local and deterministic.
Hardware Impact: Steady-frame cost is 0 us. Fault-state saving on i3/MX350 is one avoided dead fixed/late callback per affected controller, roughly 1-4 us per frame until recovery, plus immediate input-unblock on seat abort.

Problem: The airlock hand-side sanitizer briefly normalized both IK and haptic feedback hand-side values. That made IK safe, but it would collapse corrupted/future hand-side haptics to right motor only and erase the existing both-motor fallback.
Solution: Split the routes. `ResolveIkHandSide()` normalizes only `_activeHandSide` for procedural IK target ownership. `_pendingFeedbackHandSide` keeps the original hand-side payload so `ResolveMotorMask()` can still route unknown values to `BothMotorMask`.
Rejected Alternatives: Normalizing all downstream routes is simpler but degrades invalid/future hand-side haptics. Adding another signal field would be cross-domain surface area for a local defensive route.
Scalability potential: Low devices keep deterministic cockpit haptics without new systems. Middle/High/Ultra can scale pulse strength through the existing quality-weighted feedback path while preserving correct fallback semantics.
Hardware Impact: One branch chain on interaction edge only. Steady-frame cost is 0 us. It prevents misleading one-sided feedback without adding allocations, registry polling, or callback work.

Problem: `DropPodSeatController.Interact()` used a zero motor mask for fallback seat entry. On controller/gamepad fallback interaction this made the beginning of the pilot-seat lock tactile path silent even though the physical-hand path already routed haptics.
Solution: Route fallback seat entry through `BothMotorMask`, preserving the same late-frame `QueueFeedback`/`DispatchPendingFeedback` path and avoiding immediate haptic/audio emission from the interaction callback.
Rejected Alternatives: Leaving fallback silent weakens the drop-pod boarding climax. Guessing left or right hand would be less honest than both-motor fallback when the interaction route has no hand-side authority.
Scalability potential: Low devices get the same deterministic single haptic edge without extra systems. Middle/High/Ultra scale intensity through the existing continuous `GlobalQualityWeight` haptic math.
Hardware Impact: Steady-frame cost is 0 us. Edge cost is unchanged; only the byte mask changes from 0 to 3 before an existing late-frame dispatch.

Problem: `ResolveSeatRotation()` passed `cameraRollBlend` directly into the seat rotation NLERP. Shared NLERP sanitized the value, but the seat controller did not document or own the finite-range decision for this authored camera comfort parameter.
Solution: Sanitize `cameraRollBlend` locally with `DropPodSplineMath.SanitizeUnit01(cameraRollBlend)`, branch on the sanitized `rollBlend`, and pass `rollBlend` into `ResolveNlerp`.
Rejected Alternatives: Relying on `ResolveNlerp` to hide bad roll values keeps the seat controller's camera-comfort contract implicit. Adding a new camera settings object is unnecessary.
Scalability potential: Low/Middle avoid corrupted roll tuning and camera discomfort. High/Ultra can increase cockpit motion polish later while inheriting the same bounded blend contract.
Hardware Impact: One scalar clamp only during transit setup, not per-frame. Expected under 1 us on i3/MX350.

Problem: `DropPodStatusId.EngineIgnitionArmed` already drives emergency lighting, but the dashboard status label fell through to `IDLE`. During ignition the cockpit could visually say idle while lighting said launch-ready.
Solution: Add a fixed `IgnitionLabel` constant and map `EngineIgnitionArmed` to `IgnitionLabel.AsSpan()` in `ResolveStatusLabel()`.
Rejected Alternatives: Reusing `TRANSIT` or `SEALED` hides a distinct sequence state. Building dynamic localized text here would add surface area and is not needed for this fixed diegetic status panel.
Scalability potential: Low devices keep the same zero-GC char-buffer route. Middle/High/Ultra can layer stronger ignition lighting/audio while the dashboard state stays coherent.
Hardware Impact: Steady-frame cost is 0 us. The resolver adds one switch case and writes through the existing dirty text path only.

Problem: `DropPodSeatController` held a concrete `InputDispatcher` reference and did not handle `GlobalRegistryServiceSlot.Input` hot-swap while the seat transit owned movement/look/tool input block bits. A service replacement during active spline transit could either leave the old dispatcher blocked or later clear equivalent bits on the wrong dispatcher.
Solution: Store input block ownership through `IInputDeterminismService`, handle the Input hot-swap slot, restore the old owned bits before rebinding, re-apply the transit block to the new route when transit remains active, and fail-close through a pending `LateFrameTick` command/status if the input route cannot be recovered.
Rejected Alternatives: Polling `GlobalRegistry.Input` from `LateFrameTick` would violate cold dependency routing. Publishing fail-closed status directly inside the hot-swap callback would violate phase ownership. Keeping a concrete `InputDispatcher` reference makes fallback brittle under service replacement.
Scalability potential: Low devices avoid permanent input lock and recovery churn. Middle/High/Ultra preserve the cinematic seat spline while keeping input ownership route-correct for richer cockpit sequencing.
Hardware Impact: Steady-frame cost is 0 us. Hot-swap fault path adds a few scalar mask operations only on service replacement; it prevents indefinite blocked input, which is more expensive than the branch cost on i3/MX350.

Problem: The final proof scanner initially matched `lock(` inside the method name `RestoreInputBlock()` and would have produced a false lock violation.
Solution: Tighten the lock scan to syntax-level `\block\s*\(`, then rerun runtime lock/DataVault/Complete/LINQ/event/legacy scans, signal DTO scans, hot-path scans, whitespace scans, and scoped diff checks on current disk state.
Rejected Alternatives: Ignoring the scan failure would make the proof dishonest. Renaming `RestoreInputBlock()` would create churn in a stable input-ownership routine.
Scalability potential: Low/Middle/High/Ultra builds keep the same source; only the proof route was corrected so future audits do not push unnecessary code churn.
Hardware Impact: Runtime impact is 0 us. Tooling impact is one cheap static scan and no compiler process.

Problem: `AbortTransitForLostInputRoute()` assumed a LateFrame tick could always be registered after input-service loss. If dispatcher was also unavailable, the method left `_transiting` true with no guaranteed VISUAL_SYNC callback to publish the fail-closed abort.
Solution: Keep the preferred pending LateFrame fail-closed route when `TryRegisterLate()` succeeds. If it fails, clear the pending flag, locally abort transit without restoring a stale camera pose, restore only owned input block bits, and unregister ticks without emitting audio, haptics, command, or status from the callback.
Rejected Alternatives: Publishing fail-closed status directly from the hot-swap callback violates phase ownership. Leaving the transit pending waits on a dispatcher callback that may never arrive. Forcing a global registry poll from `LateFrameTick` would reintroduce hot dependency lookup.
Scalability potential: Low devices avoid permanent control lock and stranded scheduler state under service churn. Middle/High/Ultra keep the same cinematic spline when routes are healthy and gain a deterministic fault path for richer cockpit sequencing.
Hardware Impact: Steady-frame cost is 0 us. Fault-path cost is a few branch/mask operations on service replacement; it prevents indefinite input-block and camera-route churn on i3/MX350.

Problem: Airlock and dashboard generic fallback `Interact(Transform)` paths passed `PhysicalHandSide.Right`, even though controller/gamepad fallback has no physical hand authority. That made fallback cockpit haptics right-motor-biased while seat fallback had already been corrected to both motors.
Solution: Store pending feedback as a byte motor mask. Generic fallback paths pass `BothMotorMask`; physical-hand paths pass `ResolveMotorMask(fallbackHandSide)`. Airlock still keeps a separate sanitized IK hand-side route, so unknown controller fallback can use right-hand IK anchoring without lying to haptics.
Rejected Alternatives: Extending `PhysicalHandSide` with a new enum value would be a cross-domain interaction contract change. Relying on the unreachable invalid-enum fallback in `ResolveMotorMask` does not fix normal fallback calls.
Scalability potential: Low devices get deterministic tactile feedback with no new systems. Middle/High/Ultra can scale intensity through existing `GlobalQualityWeight` while preserving correct motor ownership.
Hardware Impact: Steady-frame cost is 0 us. The change replaces one enum field with one byte mask and removes a branch chain from haptic dispatch.

Problem: Input service hot-swap still executed `AbortTransitLocal()` inside the GlobalRegistry callback after failing to re-block the new input route. That wrote camera presentation outside `LateFrameTick` while the code claimed fail-closed state transfer was deferred. Player camera hot-swap also allowed an active spline captured from one camera route to continue onto a different valid camera transform.
Solution: `AbortTransitForLostInputRoute()` now only clears pending sensory state, marks `_inputRouteFailClosedPending`, unregisters fixed motor work, and requests late registration. `DispatchPendingInputRouteFailure()` is the only route that restores camera pose, restores input, queues denied feedback, and publishes abort/fail-closed status. Player route changes during active transit now set `_cameraRouteFailClosedPending`; `DispatchPendingCameraRouteFailure()` fail-closes from `LateFrameTick`, and lost camera routes abort without writing the old start pose into a new camera transform.
Rejected Alternatives: Keeping callback abort was simpler but broke VISUAL_SYNC ownership. Polling `GlobalRegistry.Player` or `GlobalRegistry.Input` in `LateFrameTick` would violate cold dependency rules. Completing the spline on a new camera route would be false cockpit state.
Scalability potential: Low devices avoid permanent input-block drift and camera snaps during service replacement. Middle devices retain the spline when routes stay valid. High/Ultra can layer stronger cockpit shake and haptics because route loss is now a deterministic late-phase fail-closed lane.
Hardware Impact: Steady-frame cost is one additional pending-failure branch in `LateFrameTick`, under 1 us on i3/MX350. Fault path prevents indefinite fixed motor lane work and avoids applying stale camera spline data to a replaced camera route.

Problem: `KinematicTerminalInteractionBridge` accepted non-finite tick delta into `_tickAccumulator`, treated non-finite global quality as 1f high-cadence operation, and kept pending haptic mask state across disable/destroy edges.
Solution: Clamp non-finite delta to 0f before accumulation, sanitize non-finite quality to 0f survival cadence, capture the pending motor mask before clearing it in `LateFrameTick`, clear pending haptics on disable/destroy, and route unknown hand-side haptics to `BothMotorMask`.
Rejected Alternatives: Trusting dispatcher delta and quality globals would let NaN or corrupted config stall/overdrive a shared interaction bridge. Clearing the mask before dispatch would silently zero the haptic command. Extending `PhysicalHandSide` is cross-domain contract churn.
Scalability potential: Low uses slower survival terminal cadence and no stale haptic pulses after lifecycle churn. Middle/High/Ultra can raise terminal scan cadence through the same continuous quality curve while keeping finite state transfer and tactile fallback.
Hardware Impact: Steady-frame cost is one finite check and branch in an existing tick path, under 1 us on i3/MX350. Fault prevention avoids a NaN accumulator stall and stale feedback edge after component re-enable.

Problem: Final APEX proof had to be reconciled after concurrent source churn and two false-positive scanner failures: case-insensitive PowerShell treated `math.select` as LINQ `Select`, and the lightweight brace scanner is not reliable on the editor test file's string-heavy assertions.
Solution: Rerun token proof on exact source snippets, rerun broad runtime hot-path and legacy scans across all DropPod C# plus `KinematicTerminalInteractionBridge.cs`, keep brace balance proof to runtime files, keep whitespace proof on runtime plus editor audit, and record the current broad 9-file source hash.
Rejected Alternatives: Running `dotnet build` would violate active compiler contention. Treating the false positives as source defects would create churn. Reporting the stale terminal-only hash would not describe current disk state.
Scalability potential: Low/Middle/High/Ultra all keep the same runtime source. The proof keeps hot lookup, managed event, DataVault, and lock risks out of the DropPod presentation loop without adding runtime machinery.
Hardware Impact: Runtime impact is 0 us. Final proof used static scans only; active `dotnet` PID `31232` blocked build/test execution under the throttling rule.

Problem: `KinematicTerminalInteractionBridge` sanitized cadence and quality, but still trusted serialized terminal reach, hand surface offset, snap hold duration, scroll analog delta, canvas projection coordinates, and IK hand side. A NaN or invalid enum from corrupted serialized/input/panel data could leak into panel projection or `PhysicalHandIkTarget`.
Solution: Add finite scalar/vector guards: `ResolveReachMeters`, `ResolveSurfaceOffsetMeters`, `ResolveSnapHoldSeconds`, `ResolveFiniteAnalogDelta`, `IsFinite(float2)`, and `ResolveIkHandSide`. Use them before panel projection, button snap projection, input event payload construction, and IK target construction.
Rejected Alternatives: Clamping only after the target is built would still leak non-finite values to the sink. Extending the hand-side enum would alter a cross-domain contract. Adding exception handling would violate fail-closed hot-path policy and risk managed overhead.
Scalability potential: Low devices avoid NaN stalls and panel/IK churn. Middle/High/Ultra can increase terminal cadence through `GlobalQualityWeight` while the same finite DTO contract protects richer panel behavior.
Hardware Impact: Steady-frame cost is a small set of scalar/vector finite checks in the existing terminal tick, under 1 us on i3/MX350. No allocation, no new dispatcher route, no compiler process launched.

Problem: `KinematicTerminalInteractionBridge` could retain pending haptic state when the dispatcher route was replaced and late-frame registration was unavailable. `OnDestroy()` also only cleared haptic flags, while `OnDisable()` cleared hand target, dispatcher registration, hot-swap listener, pressed state, and accumulator.
Solution: Mirror disable cleanup in `OnDestroy()` using the same guarded local calls, and clear `_pendingPressHaptic` plus `_pendingPressHapticMotorMask` in dispatcher hot-swap if `_registeredLateFrame` remains false after the cold re-register attempt.
Rejected Alternatives: Waiting for a future `LateFrameTick` is unsafe when the dispatcher route is absent. Emitting haptics from the hot-swap callback would violate VISUAL_SYNC. Adding a global feedback queue would widen cross-domain surface for one local stale-state edge.
Scalability potential: Low devices avoid stale rumble after terminal unload or dispatcher churn. Middle/High/Ultra keep richer terminal haptics safe because feedback still only dispatches from late-frame when the route exists.
Hardware Impact: Steady-frame cost is 0 us. Fault-path cost is two flag clears and guarded unregister calls only during destroy or dispatcher replacement; expected under 3 us on i3/MX350 and no allocation.

Problem: The first dispatcher teardown fix only reasoned about `_registeredLateFrame`, but terminal correctness requires both update and late-frame routes. Updatable without late-frame can keep ray/panel state moving while feedback has no VISUAL_SYNC dispatch lane.
Solution: Make `TryRegister()` return `_registered && _registeredLateFrame`, and have dispatcher hot-swap clear route-local state through `ClearRuntimeStateForLostDispatcherRoute()` when the dispatcher is null or either registration lane fails.
Rejected Alternatives: Treating updatable-only registration as safe would keep a half-alive terminal. Clearing only haptics would leave IK and pressed state stale. Publishing from the callback would still violate phase ownership.
Scalability potential: Low devices avoid stuck terminal IK and stale button press state after dispatcher churn. Middle/High/Ultra can raise terminal cadence and haptic strength while the same route-loss gate keeps the bridge deterministic.
Hardware Impact: Steady-frame cost is 0 us. Fault-path cost is one boolean route result and local field clears only during service replacement; under 3 us on i3/MX350.

Problem: Dispatcher replacement could still leave the terminal bridge half-registered: updatable registration could succeed while late-frame registration failed. In that state terminal sampling might continue without a valid `VISUAL_SYNC` haptic lane, or stale IK/haptic state could survive a null dispatcher.
Solution: Make `TryRegister()` return full route readiness and clear all runtime terminal state when dispatcher is null or either updatable/late-frame registration fails. The clear path removes IK target, pressed state, pending haptic mask, and cadence accumulator without emitting feedback from the callback.
Rejected Alternatives: Checking only `_registeredLateFrame` missed updatable-only partial registration. Dispatching denied feedback from hot-swap would break phase ownership. Polling the registry from Tick would violate cold dependency rules.
Scalability potential: Low devices avoid stale terminal IK and rumble after dispatcher churn. Middle/High/Ultra can keep high-cadence terminal input because route loss now has a deterministic local teardown.
Hardware Impact: Steady-frame cost is 0 us. Fault-path cost is a few field clears plus existing unregister guards only on dispatcher replacement; no allocation and no build process.

Problem: `TryRegister()` returned false when one terminal dispatcher lane failed, but it did not unregister the lane that had already succeeded. That created an updatable-only or late-frame-only partial owner after the method had declared route failure.
Solution: After registration attempts, return true only when both lanes are registered; otherwise call `TryUnregister()` before returning false. The caller still clears route-local terminal state, but the scheduler no longer retains a partial callback.
Rejected Alternatives: Clearing only terminal state is insufficient because the dispatcher can continue invoking the surviving lane. Duplicating unregister code in the hot-swap callback would miss `OnEnable()` partial-registration failure and make the contract less local.
Scalability potential: Low devices avoid dead terminal sampling after scheduler pressure. Middle/High/Ultra keep higher terminal cadence only when both simulation and VISUAL_SYNC lanes are present.
Hardware Impact: Steady-frame cost is 0 us. Fault-path cost is one existing unregister routine only during registration failure, under 3 us on i3/MX350.

Problem: Airlock and seat motion used the same partial registration pattern as the terminal. A successful fixed registration followed by failed late-frame registration could keep physical motion callbacks alive without a VISUAL_SYNC lane for feedback, cleanup, and presentation completion.
Solution: Make `DropPodAirlockController.TryRegisterTicks()` and `DropPodSeatController.TryRegisterTicks()` return true only after the complete route is ready. On partial failure they call `UnregisterTicks()` before returning false.
Rejected Alternatives: Cleaning up only at the call site misses dispatcher hot-swap and future callers. Registering fixed without late-frame would make motion truth outlive its presentation/feedback owner.
Scalability potential: Low devices avoid dangling fixed callbacks under dispatcher pressure. Middle/High/Ultra keep hatch and seat cinematic motion only when the whole scheduler route is alive.
Hardware Impact: Steady-frame cost is 0 us. Fault path reuses existing unregister routines during rare scheduler registration failure, under 4 us on i3/MX350.

Problem: Emergency lighting had no explicit response for `FailClosed`, `SeatTransitArmed`, or `SeatTransitActive`. A failed seat entry or active transit could leave the cabin visually calm, depending on the previous status target.
Solution: Add named continuous alert weights: idle 0.0, transit 0.45, armed 0.7, full 1.0. `DrainStatusSignals()` maps moving/transit/armed/fail-closed states through those weights and keeps lighting interpolation in `LateFrameTick`.
Rejected Alternatives: Binary on/off warning light loses physical nuance. Publishing extra lighting commands from airlock/seat would duplicate state ownership instead of consuming the status bus.
Scalability potential: Low devices still update one scalar target and existing lights. Middle/High/Ultra can make the same weights drive stronger materials, volumetrics, or cockpit warning overlays without changing status ownership.
Hardware Impact: Steady-frame cost remains 0 us when stable because `ApplyLightingIfNeeded` gates writes. Status-drain edge adds one switch and scalar assignment, under 1 us on i3/MX350.

Problem: Dispatcher hot-swap handlers reset `_registeredFixed` or `_registeredLate` before unregistering scheduler lanes. Because `GlobalRegistry.Unregister*` routes into static `SystemDispatcher` lanes, this can leave the previous callback alive while the controller believes it is unregistered.
Solution: In airlock, seat, dashboard toggle, dashboard text, and emergency lighting, call `UnregisterTicks()` or `UnregisterLate()` before any rebind decision. Static audit checks the order and rejects direct `_registered* = false` inside hot-swap bodies.
Rejected Alternatives: Keeping flag reset is not cleanup. Adding a new dispatcher token API is cross-domain scope creep. Polling registry services in hot loops violates cold DI rules.
Scalability potential: Low devices avoid duplicate callbacks under dispatcher churn. Middle/High/Ultra can keep richer cockpit presentation only while the full fixed/late or VISUAL_SYNC route is actually alive.
Hardware Impact: Steady-frame cost is 0 us. Fault-path cleanup reuses existing unregister calls during rare service replacement, estimated under 5 us on i3/MX350 and prevents indefinite duplicate callback cost.

Problem: `SeatTransitArmed` drove emergency lighting to a 0.7 warning weight, but the dashboard text defaulted to `IDLE`. The cockpit could show an idle state while the physical seat route was armed and visual warning state was active.
Solution: Add a static `ARMED` dashboard label and map `DropPodStatusId.SeatTransitArmed` to `ArmedLabel.AsSpan()` inside `ResolveStatusLabel()`. The existing char buffer and TMP `SetCharArray` path remains unchanged.
Rejected Alternatives: Reusing `TRANSIT` hides the distinction between armed and moving. Publishing a separate dashboard command would duplicate status ownership and add a second route for the same fact.
Scalability potential: Low devices pay one extra switch case only when rendering status text. Middle/High/Ultra can drive stronger cockpit material or haptic cues from the same status without changing the data contract.
Hardware Impact: Steady hot-path cost is 0 us after the label is rendered because status text is dirty-gated. Edge render cost is one switch case and one `ReadOnlySpan<char>` over a static string, under 1 us on i3/MX350 and 0 B GC.

Problem: Airlock and seat dispatcher-route loss cancelled local motion but did not publish a terminal fail-closed state. Dashboard and lighting consumers could keep rendering the previous `AirlockMoving` or `SeatTransitActive` snapshot until another signal arrived.
Solution: Airlock route loss now publishes `DropPodStatusId.FailClosed` after snapping the hatch and clearing IK target. Seat route loss now publishes `DropPodCommandId.AbortTransit` and `DropPodStatusId.FailClosed` after local abort/input restore, while still clearing pending feedback and unregistering scheduler lanes.
Rejected Alternatives: Silent local cancel hides the fault from diegetic presentation. Queueing audio/haptic in the hot-swap callback would violate VISUAL_SYNC. Waiting for `LateFrameTick` is unsafe when dispatcher route recovery has already failed.
Scalability potential: Low devices get deterministic fail-closed cockpit state without extra systems. Middle/High/Ultra can make dashboard/lighting/audio richer from the same status bus while preserving one fact owner and one route.
Hardware Impact: Steady-frame cost is 0 us. Fault path adds two unmanaged SignalBus pushes only on dispatcher route loss; expected under 5 us on i3/MX350 and no managed allocation.

Problem: Seat camera and input route failures relied on `LateFrameTick` deferral for presentation-safe feedback. If the late-frame lane could not be registered, camera route failure could leave `_cameraRouteFailClosedPending` stuck forever, and input route failure could silently abort without publishing fail-closed state.
Solution: Camera route loss now calls `AbortTransitForLostCameraRoute(false)` if `TryRegisterLate()` fails, publishing abort/fail-closed without feedback. Input route loss now publishes the same abort/fail-closed state on its no-late fallback path. `QueueFeedback` remains behind a `queueFeedback` guard and is only used by LateFrame-capable paths.
Rejected Alternatives: Leaving pending flags alive without a late-frame lane is a dead state. Queueing feedback from the hot-swap callback violates the VISUAL_SYNC presentation rule. Duplicating a separate emergency route would split one fact across two owners.
Scalability potential: Low devices get deterministic fail-closed recovery under scheduler pressure. Middle/High/Ultra keep richer feedback when `LateFrameTick` exists, using the same state contract and no new allocations.
Hardware Impact: Steady-frame cost is 0 us. Failure fallback adds two unmanaged SignalBus pushes and no haptic/audio work, estimated under 5 us on i3/MX350 with 0 B GC.

Problem: Dashboard status text was dirty-gated but still waited behind the metric refresh timer. A fail-closed or armed cockpit state could be delayed by the low-tier text cadence, and `AirlockMoving` displayed `LOCK` even during hatch opening. Seat-complete feedback also pulsed only the right motor despite being a whole-seat event.
Solution: Let `_textDirty` bypass `_refreshTimer` in `LateFrameTick()` while preserving stable metric write skipping, rename the moving status label to `MOVING`, and queue complete transit haptics with `BothMotorMask`.
Rejected Alternatives: Forcing all dashboard metrics to refresh every frame would waste TMP mesh work on weak hardware. Keeping `LOCK` would encode the wrong direction for an opening hatch. Treating completion as right-hand feedback would confuse physical interaction ownership.
Scalability potential: Low devices get immediate critical cockpit text without per-frame metric churn. Middle devices keep analog needle cadence. High/Ultra can add stronger seat clamp audio/haptic assets on the same both-motor route without changing the DTO or phase contract.
Hardware Impact: The status dirty branch is one boolean check in `LateFrameTick`, under 1 us on i3/MX350. It avoids delayed fail-closed cognition while preserving the existing text mesh savings; haptic mask change has 0 steady-frame cost.

Problem: Pre-transit seat rejection paths queued blocked feedback and then called `TryRegisterLate()` without checking the result. If VISUAL_SYNC registration failed, `_feedbackPending` could remain armed with no scheduler lane and fire later after unrelated dispatcher recovery.
Solution: Add `QueueFeedbackIfLateRouteAvailable()` for rejection feedback. It queues feedback, attempts late registration, and calls `ClearPendingFeedback()` if no late route exists. Success paths still use the existing feedback queue because their scheduler route is already alive.
Rejected Alternatives: Silently keeping pending feedback is a temporal leak. Emitting feedback immediately from the rejection callback violates the phase contract. Removing feedback entirely would make legitimate blocked-seat UX worse on working schedulers.
Scalability potential: Low devices under scheduler pressure fail closed with no stale tactile event. Middle/High/Ultra keep richer blocked-seat feedback when LateFrame is actually available.
Hardware Impact: Steady-frame cost is 0 us. Rejection edge adds one helper call and, only on failed late registration, three scalar clears; under 2 us on i3/MX350 and 0 B GC.

Problem: `KinematicTerminalInteractionBridge` still cleared `_registered` and `_registeredLateFrame` directly during dispatcher hot-swap before attempting rebind. The object could lose knowledge of lanes already registered in the static dispatcher, leaving terminal sampling or VISUAL_SYNC callbacks stranded after service replacement.
Solution: Replace direct flag reset with `TryUnregister()` in the dispatcher hot-swap branch, then attempt `TryRegister()` against the current dispatcher route. Extend the static audit to require unregister-before-register order and reject direct flag reset inside the hot-swap method.
Rejected Alternatives: Keeping the direct flag reset is not cleanup. Clearing only haptic/IK runtime state would leave scheduler callbacks alive. Adding a new dispatcher token API is cross-domain scope creep for a local lifecycle defect.
Scalability potential: Low devices avoid duplicate or stranded terminal callbacks under dispatcher churn. Middle/High/Ultra can raise terminal cadence and richer haptic feedback only while both update and VISUAL_SYNC lanes are actually registered.
Hardware Impact: Steady-frame cost is 0 us. Fault-path cost is the existing unregister routine during rare dispatcher replacement, estimated under 4 us on i3/MX350 and 0 B GC.

Problem: Airlock, seat, and dashboard toggle cleared pending feedback only when a non-null dispatcher route existed but re-registration failed. If dispatcher hot-swap reported `currentService == null` between simulation settle and `LateFrameTick`, pending haptic/audio/visual state could survive and fire after a later dispatcher recovery.
Solution: Treat null dispatcher as a late-only route loss. Airlock now calls `ClearLateOnlyStateForLostDispatcherRoute()` for null or failed late registration, dashboard toggle clears/snap-restores on null or failed tick registration, and seat clears pending feedback on null or failed late registration.
Rejected Alternatives: Waiting for dispatcher recovery would preserve stale tactile/audio facts across phases. Emitting feedback from the hot-swap callback violates VISUAL_SYNC. Adding a global dead-letter queue is unnecessary for local value-field cleanup.
Scalability potential: Low devices under scheduler pressure drop stale feedback instead of replaying it later. Middle/High/Ultra keep rich completion feedback only when VISUAL_SYNC exists in the same phase window.
Hardware Impact: Steady-frame cost is 0 us. Fault path adds one null branch and local field clears during dispatcher replacement, under 3 us on i3/MX350 and 0 B GC.

Problem: Airlock, seat, and dashboard toggle handled active motion dispatcher loss, but late-only pending presentation state could survive a dispatcher hot-swap when the new VISUAL_SYNC route failed to register. Pending completion feedback or final toggle visual flush could execute later after unrelated route recovery.
Solution: Clear pending airlock/seat feedback when failed late registration is detected in the hot-swap branch. For the dashboard toggle, add `ClearLateOnlyStateForLostDispatcherRoute()` to clear feedback, snap to the committed switch state, and unregister the late lane without audio or haptic dispatch.
Rejected Alternatives: Emitting completion feedback from the callback violates VISUAL_SYNC. Keeping pending state for a future dispatcher recovery leaks temporal ownership. Cancelling full committed switch state would incorrectly undo a command already published.
Scalability potential: Low devices under dispatcher pressure fail quiet without stale tactile/audio artifacts. Middle/High/Ultra keep richer haptic/audio presentation only when VISUAL_SYNC is actually registered.
Hardware Impact: Steady-frame cost is 0 us. Fault-path cost is scalar clears and one committed transform snap for the toggle, estimated under 4 us on i3/MX350 and 0 B GC.

Problem: Dashboard text and emergency lighting consume `DropPodStatusSignal` through frame snapshots. If dispatcher hot-swap removes VISUAL_SYNC before they drain the fail-closed frame, they can miss the status signal and recover later with stale cockpit state.
Solution: On dispatcher null or failed late-frame registration, mark only local fail-closed presentation state: dashboard stores `DropPodStatusId.FailClosed` and sets `_textDirty`; emergency lighting targets `FullAlertLightWeight`. Actual TMP and Light writes remain in `LateFrameTick`.
Rejected Alternatives: Publishing another status signal from consumers would split authority. Writing TMP or Light output from a hot-swap callback violates VISUAL_SYNC. Retaining stale state relies on transient frame snapshots during a scheduler fault.
Scalability potential: Low devices under scheduler pressure recover into fail-closed cockpit state without extra polling. Middle/High/Ultra keep stronger cockpit warning presentation from the same local dirty target once VISUAL_SYNC exists.
Hardware Impact: Steady-frame cost is 0 us. Fault-path cost is one branch plus scalar/enum assignment during dispatcher replacement, under 2 us on i3/MX350 and 0 B GC.

Problem: The fail-closed presentation fallback covered dispatcher hot-swap but not initial enable into an already-missing VISUAL_SYNC route. Dashboard and lighting could enable, drain no new signal, perform their first local presentation write, and remain non-fail-closed until another route event. The terminal bridge also ignored failed initial update/late registration on `OnEnable()`.
Solution: Capture `lateRouteReady` in dashboard and emergency lighting `OnEnable()`, then mark the same local fail-closed presentation state before the first existing render/apply when playing and no late route exists. Terminal `OnEnable()` now calls `ClearRuntimeStateForLostDispatcherRoute()` if `TryRegister()` fails.
Rejected Alternatives: Duplicating a status signal from consumers would split authority. Polling the dispatcher from hot paths would violate cold DI. Leaving enable-time state to hot-swap callbacks misses the case where the component enables after dispatcher loss.
Scalability potential: Low devices under scheduler pressure recover into warning cockpit state without extra ticks or allocations. Middle/High/Ultra keep the same route-local cleanup and can layer richer warning visuals once VISUAL_SYNC returns.
Hardware Impact: Steady-frame cost is 0 us. Enable fault path adds one bool and local enum/scalar/flag clears, under 3 us on i3/MX350 and 0 B GC.

Problem: Seat `OnEnable()` always published `SeatTransitArmed`, even when the linked airlock was not sealed. That lets dashboard text and emergency lighting claim an armed pod while the same controller would reject strap-in with `Seal Hatch First`.
Solution: Add `PublishSeatAvailabilityStatus(byte flags)`. The seat publishes `SeatTransitArmed` only when `IsSeatAvailable()` is true; otherwise it publishes `FailClosed` with the fail-closed flag, using the existing SignalBus status route.
Rejected Alternatives: Adding a new status ID would widen the cross-domain DTO contract. Leaving only prompt text to explain the block makes the dashboard and warning lights lie about physical readiness.
Scalability potential: Low devices get correct cockpit state from one existing status signal. Middle/High/Ultra can drive stronger warning lighting/audio from the same fail-closed status without changing status layout.
Hardware Impact: Steady-frame cost is 0 us. Enable edge cost is one branch and one existing unmanaged SignalBus push, under 3 us on i3/MX350 and 0 B GC.

Problem: Airlock dispatcher-route loss during active hatch motion snapped to committed state. During unlock, `_sealed` is cleared before motion completes, so a route fault could visually teleport the hatch to fully open instead of preserving the last physically sampled pose.
Solution: Add `FreezeMotionAtCurrentSealPose()`. The route-loss path sanitizes `_seal01`, freezes `_targetSeal01`, derives `_sealed` from `_seal01 >= 0.995f`, reapplies smooth-step hatch rotation, clears hand/feedback state, publishes fail-closed, and unregisters dispatcher lanes.
Rejected Alternatives: `SnapToCommittedSealState()` is correct for disable/destroy cleanup but wrong for dispatcher fault during motion. Keeping motion alive without a scheduler route would hide a dead callback path. Publishing audio/haptics from the hot-swap callback would violate VISUAL_SYNC.
Scalability potential: Low devices avoid a large visual pop under scheduler pressure. Middle/High/Ultra retain the current cinematic pose and can layer warning lighting/audio from the same fail-closed status once VISUAL_SYNC exists.
Hardware Impact: Steady-frame cost is 0 us. Fault path adds one sanitize, one threshold, and one existing rotation application, estimated under 3 us on i3/MX350 and 0 B GC.

Problem: The sealed airlock interaction prompt said `Hatch Sealed` while pressing it sends the unlock/unseal route. A diegetic physical panel should name the next action, not restate current state.
Solution: Change the default sealed prompt to `Unseal Hatch` and add a static audit guard that rejects the old prompt while preserving the zero-allocation `TryCopyInteractText` path.
Rejected Alternatives: Showing state-only text makes the interaction ambiguous. Adding a second status widget is unnecessary because the physical action prompt already owns this local affordance.
Scalability potential: Low devices get clearer UX with only a serialized string default. Middle/High/Ultra can add richer cockpit labels later without changing interaction routing.
Hardware Impact: Runtime cost is 0 us; serialized default string only.

Problem: `DropPodStatusId.FailClosed` carried two different facts: a physical boarding block from an open hatch and generic system/route failures. Dashboard rendered all of them as `HATCH OPEN`, which lies during camera, input, dispatcher, or spline failure.
Solution: Add `DropPodStatusId.SeatBlockedAirlockOpen = 10u` for the specific physical block. Seat availability and the first blocked strap-in branch publish that status with the existing fail-closed flag. Dashboard maps it to `HATCH OPEN`, while generic `FailClosed` maps to `FAULT`; emergency lighting treats both as full alert. Signal DTO size stays 16 bytes.
Rejected Alternatives: Adding a reason field would widen the hot status DTO. Keeping `FailClosed` overloaded corrupts player feedback. Publishing separate dashboard-only commands would split one fact across two routes.
Scalability potential: Low devices get truthful cockpit text from the same status lane. Middle/High/Ultra can layer stronger audio/material warning variants based on the enum without touching simulation authority or payload layout.
Hardware Impact: Steady-frame cost is 0 us after label render. Edge cost is one extra enum switch case in existing VISUAL_SYNC drain, under 1 us on i3/MX350 and 0 B GC.

Problem: LifePod strap hot-swap handlers directly cleared `_registeredFixedTick` / `_registeredTick` during dispatcher replacement. That discards local knowledge before unregistering the old static scheduler lane and can leave old callbacks alive after a dispatcher swap.
Solution: Replace direct flag resets with `TryUnregisterFixedTick()` / `TryUnregisterTick()` before rebind. Mirror latch destroy cleanup with tick unregister, hot-swap unregister, highlighter clear, and local hold-state reset. Add a source audit covering both hot-swap methods and destroy cleanup.
Rejected Alternatives: Direct flag reset is not cleanup. Adding a new dispatcher token API would be broader than the defect. Waiting for disable to run before destroy is lifecycle optimism.
Scalability potential: Low devices avoid duplicate strap callbacks after service churn. Middle/High/Ultra can keep richer physical strap feedback without accumulating stale scheduler work.
Hardware Impact: Steady-frame cost is 0 us. Fault path adds existing unregister calls during rare dispatcher replacement/destroy; estimated under 4 us on i3/MX350 and prevents indefinite duplicate callback cost.

Problem: `LifePodSeatStrapCoordinator.TryLatch()` can be called from the latch tick chain, and `EngageSeatLock()` can execute in the same simulation path. Both were sending haptic commands immediately, before the frame settled and outside VISUAL_SYNC.
Solution: Make the coordinator an `ILateFrameTickable`. Latch and lock paths now copy sanitized haptic values into fixed scalar pending fields; `LateFrameTick()` drains them through `DispatchPendingHaptics()`. Dispatcher hot-swap unregisters stale late lanes and clears pending fields if the route cannot be restored.
Rejected Alternatives: Direct `ToolHapticsRuntime` calls from `TryLatch()` or `EngageSeatLock()` violate phase separation. A managed queue or list is unnecessary and would add allocation pressure. Dropping latch feedback entirely would weaken the physical strap affordance.
Scalability potential: Low devices keep zero-GC scalar transfer and avoid stale haptic replay after scheduler churn. Middle devices keep one latch pulse plus one full-lock pulse. High/Ultra can raise authored haptic frequencies/durations within the same bounded late-frame route without changing simulation truth.
Hardware Impact: Steady-frame cost is 0 us when no pending haptics. Latch edge adds scalar copies and one late-frame registration; estimated under 5 us on i3/MX350 and 0 B GC. Full-lock edge may dispatch two bounded haptic commands from VISUAL_SYNC, not from the tick path.

Problem: LifePod straps could still claim a seat lock and queue full-lock feedback without proving that the cached player motor and fixed dispatcher route were alive. The latch also kept `_contactThisTick` and hold progress after a dispatcher/tick route loss.
Solution: Gate `EngageSeatLock()` on `TryEnsurePlayerMotor()` and successful `TryRegisterFixedTick()`. Release active lock runtime state on player/motor/dispatcher route loss. Convert latch `TryRegisterTick()` to a bool and clear transient hold contact/progress if tick registration or dispatcher recovery fails.
Rejected Alternatives: Keeping `_seatLockActive` as an optimistic flag would create a physical lie. Replaying stale latch contact after dispatcher recovery violates phase ownership. Adding a new global strap signal or managed queue would widen authority for a local lifecycle defect.
Scalability potential: Low devices fail quiet under scheduler pressure with no stale tactile or lock state. Middle keeps deterministic strap progress only while the tick route exists. High/Ultra can add richer strap clamp presentation only after the same motor + dispatcher proof succeeds.
Hardware Impact: Steady-frame cost remains 0 us. Lock edge adds two cached-interface checks and one bool return from registration, under 2 us on i3/MX350. Fault path clears scalar fields and unregisters the fixed lane, under 4 us and 0 B GC.

Problem: `LifePodSeatStrapCoordinator.OnEnable()` still restored an inherited `_seatLockActive` state by calling `TryRegisterFixedTick()` without proving the cached player motor/runtime context or handling registration failure. A disabled/re-enabled strap could claim a physical seat lock while no route could apply it.
Solution: On enable, active lock state now survives only if `TryCacheSeatLockPose()`, `TryEnsurePlayerMotor()`, and `TryRegisterFixedTick()` all pass. Any failure calls `ReleaseSeatLockForLostRuntimeRoute()`, clearing lock runtime and pending lock haptic state.
Rejected Alternatives: Leaving the guard only in `EngageSeatLock()` misses reload/reenable paths. Clearing only the fixed lane keeps a false `_seatLockActive` fact. Polling registry from `FixedTick()` would violate cold DI.
Scalability potential: Low devices fail quiet after scene churn or dispatcher pressure. Middle keeps deterministic seat lock only when the motor and scheduler lane are present. High/Ultra can add stronger lock presentation later without changing route ownership.
Hardware Impact: Steady-frame cost remains 0 us. Enable edge adds three existing boolean checks and one scalar cleanup path, under 2 us on i3/MX350 with 0 B GC.

Problem: Strap fixed/tick lanes could start even if the `GlobalRegistry` hot-swap listener route failed. That makes the object blind to later dispatcher/player service replacement while still applying strap or seat-lock runtime state.
Solution: Convert strap hot-swap registration helpers to `bool`. Coordinator active lock restore and `EngageSeatLock()` require the listener route before preserving active lock state. Latch `TryRegisterTick()` refuses to start unless the hot-swap listener route is registered.
Rejected Alternatives: Treating hot-swap registration as optional leaves a lifecycle blind spot. Polling registry services from tick/fixed paths violates cold DI. Adding a new dispatcher-token API is wider than the local route-precondition defect.
Scalability potential: Low devices fail quiet if scheduler/listener capacity is exhausted. Middle keeps strap simulation only while service replacement can be observed. High/Ultra can increase strap presentation only after the same route proof succeeds.
Hardware Impact: Steady-frame cost remains 0 us. First tick/lock activation adds one cached bool/helper check, under 1 us on i3/MX350; failure cleanup stays scalar and 0 B GC.
