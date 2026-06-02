# Rationale_1600

Problem: Prompt path `Assets/_Project/Scripts/Player/Movement/` was absent while actual player KCC code is split across legacy movement, hydrodynamic KCC, and exosuit physics.
Solution: Create an isolated zero-G player movement branch using existing Exosuit DataVault/Burst pattern and add only BufferID expansion in core memory contracts.
Rejected Alternatives: Editing `HectonPlayerMovement.cs` would mix underwater, ground, camera, and tool concerns in a 200KB legacy file and raise cross-agent conflict risk.
Scalability potential: Low keeps single analytic orbit-wall probe and fixed telemetry; Middle/High/Ultra can consume the same DTOs for richer presentation without changing truth.
Hardware Impact: Low-end i3/MX350 avoids managed allocation and avoids Unity physics scene queries; expected hot-path win versus synchronous casts is roughly 20-80 us per player frame depending on collider density.

Problem: Task 09 asked for RaycastCommand/SpherecastCommand with TempJob-style batches, while project mandates reject synchronous/same-frame collision queries and persistent state outside DataVault.
Solution: Use analytic zero-G orbit-wall SDF detection inside the Burst solver and publish `ZeroGSurfaceHitDTO` through DataVault.
Rejected Alternatives: `SphereCast`, `Physics.Raycast`, or a TempJob command buffer create hidden dependency on Unity physics ownership and increase same-frame fence pressure.
Scalability potential: Low uses one AABB/SDF plane probe; Middle can add secondary probes; High/Ultra can blend richer visual contact sparks from the same surface DTO.
Hardware Impact: i3/MX350 path is constant-time scalar math; expected save is 30-120 us during dense orbit interior contact.

Problem: Zero-G movement truth must not change with quality switches.
Solution: `GlobalQualityWeight` is carried in DTOs and used for presentation telemetry/trauma weighting, not thrust, collision restitution, or authority layout.
Rejected Alternatives: Low/high binary movement branches would make replay/lockstep diverge.
Scalability potential: Low/Middle/High/Ultra scale sensory output and probe metadata only; gameplay truth stays invariant.
Hardware Impact: low-end devices keep deterministic motion while visual owners can reduce downstream effects.

Problem: Runtime needed scene movement without making GlobalRegistry a hot dependency source.
Solution: `ZeroGMovementRuntime` caches DataVault handles during cold setup, schedules `ZeroGPhysicsIntegrationJob` through the admission lane, and emits camera/haptic signals after readback.
Rejected Alternatives: Direct Rigidbody forces or KCC calls would couple zero-G to underwater/ground locomotion and break one-owner movement authority.
Scalability potential: Low uses one analytic bounds probe; Middle/High/Ultra can add visual-only contact debris, suit shake, and haptics from existing output DTOs.
Hardware Impact: i3/MX350 stays at O(1) Burst math with no managed heap; expected under 0.1 ms for the single player solver.

Problem: Fuzz proof needed without forcing a full project build or scene boot.
Solution: Add editor-only NUnit tests that run Burst-compatible jobs directly for layout, 10k inertial drift, quaternion fuzzing, and analytic wall reflection.
Rejected Alternatives: PlayMode scene tests would require scene loading and unrelated subsystem startup; dotnet build is forbidden unless critical.
Scalability potential: Low/Middle/High/Ultra all share the same deterministic solver tests, while presentation owners can scale separately.
Hardware Impact: tests allocate only editor `TempJob` buffers; runtime hot path remains DataVault-owned and allocation-free.

Problem: Verification pressure conflicted with host compile policy.
Solution: Ran `validate_script` on new runtime/job/contract/test files; skipped build and test execution when CPU was 68.8% and `dotnet` PID 25280 was already active.
Rejected Alternatives: Launching dotnet or Unity tests under load would violate the explicit coordinator rule and steal CPU from other agents.
Scalability potential: static and editor-script validation catches syntax/layout issues now; full test run can execute once host load drops.
Hardware Impact: preserved cluster CPU budget; zero microseconds of new runtime cost from verification tooling.

Problem: APEX verification required presentation side effects to be phase-safe after simulation settles; `CompletePendingJob` previously could apply transform and publish signals when called from `PostFixedTick`.
Solution: Split readback into value-type pending DTO fields and flush them only from `LateFrameTick` through `FlushVisualSyncReadback`.
Rejected Alternatives: Keeping transform/signals in the readback finalizer was cheaper to write but allowed POST_FIXED to touch presentation.
Scalability potential: Low/Middle/High/Ultra all get the same simulation DTO; visual owners can scale trauma, haptics, suit feedback, and camera effects after the solver without changing truth.
Hardware Impact: two struct copies replace direct side effects; no heap allocation, no scene search, expected cost below 1 us on i3/MX350.

Problem: Dependency and lock compliance needed executable proof without a full build.
Solution: Added editor static-contract tests that scan runtime method bodies for hot `GlobalRegistry.Get`, `GetComponent`, scene physics queries, non-LateFrame presentation flush, and multiple write-lock acquisition.
Rejected Alternatives: A prose-only claim would not survive later edits; a full Roslyn/dotnet pass is barred under current host load.
Scalability potential: The scanner protects low-end hot loops from accidental scene searches and keeps high-end presentation features decoupled from simulation ownership.
Hardware Impact: runtime cost remains 0 us; editor-only verification consumes CPU only when tests are deliberately run.

Problem: Zero-G runtime still depended on mock/external input and did not consume the existing project input sidecar.
Solution: Convert `CoreDeterminismSignals.TryGetLatestInput` into `ZeroGInputStateDTO` in `WriteFrameInput`: move+vertical become local thrust, look becomes local pitch/yaw, primary/secondary become roll, jump becomes push, sprint becomes brake, interact becomes horizon assist.
Rejected Alternatives: Pulling `InputAction` references or `IInputProvider` objects into zero-G movement would couple player physics to managed input assets and violate the existing InputDispatcher ownership route.
Scalability potential: Low through Ultra use the same input DTO and Burst solver; device-specific input richness stays upstream in InputDispatcher.
Hardware Impact: one static signal read and flat DTO pack per fixed frame; expected below 2 us on i3/MX350, with no heap allocation.

Problem: Fail-closed DataVault guard denial was silent in the zero-G telemetry path.
Solution: Add `VaultAccessDenied` state flag and fault code, increment a pending integer when frame input or job buffer guard acquisition is denied, and stamp the next telemetry ring entry after a successful solver frame.
Rejected Alternatives: Logging strings or forcing a telemetry write while compaction/guard contention is active would add managed I/O or deadlock risk.
Scalability potential: Low devices get cheap numeric diagnosis; high-tier debug UIs can render richer fault displays from the same ring without changing gameplay truth.
Hardware Impact: one saturated integer increment on denial and one branch during telemetry patch; normal-frame cost is negligible.

Problem: `WriteFrameInput` previously acquired `FrameInputGuardMask` before resolving deterministic input, mock transform orientation, serialized tuning, and quality.
Solution: Build tuning/input/quality before acquiring the DataVault mutation guard, then hold the guard only while resolving the input/tuning buffers and writing DTOs.
Rejected Alternatives: Keeping external reads inside the guard is simpler but expands contention windows and makes future deadlock audits weaker.
Scalability potential: Low-end devices reduce lock hold time under input spikes; high-tier routes can add richer upstream input without increasing DataVault guard duration.
Hardware Impact: DataVault guard window loses signal read, transform rotation read, and tuning sanitize work; expected contention reduction is frame-dependent, normal path below 5 us saved on i3/MX350 under pressure.

Problem: The deterministic input route was text-scanned but not semantically tested.
Solution: Extract `TryPackDeterministicInputSignal` as a pure converter from `InputSignal` to `ZeroGInputStateDTO`, then add a focused NUnit test for thrust axes, look-to-angular axes, push/brake/horizon action mapping, and frame/tick stamping.
Rejected Alternatives: Testing only the private sidecar-consuming method would require global signal state and make the proof less deterministic.
Scalability potential: Low/Middle/High/Ultra can evolve InputDispatcher upstream without changing the zero-G converter contract.
Hardware Impact: no new runtime work; method extraction replaces inline code and keeps the same below-2 us fixed-frame path.

Problem: `ZeroGTuningDTO.MaxSubsteps` existed but the solver always used a single integration step, making the quality/collision precision control decorative.
Solution: Add a bounded 1..8 substep loop inside `ZeroGPhysicsIntegrationJob` covering thrust, angular momentum, orientation, brake assist, horizon lock, analytic wall resolution, and depenetration.
Rejected Alternatives: Running multiple jobs or scheduling per-substep collision commands would increase dispatcher/fence overhead and violate the zero-G single-player O(1) solver goal.
Scalability potential: Low can run one substep; mid/high/ultra can raise substeps for smoother high-speed contact without changing DTO layout or authority route.
Hardware Impact: low tier remains one pass; four substeps add deterministic scalar math only and avoid Unity physics queries, expected still below 0.1 ms for the single player on i3/MX350.

Problem: A dry suit propellant state had no focused semantic proof, and the drift assertion compared against the raw input velocity rather than the sanitized velocity actually used by the job.
Solution: Add `IntegrationJob_ZeroPropellantRejectsThrusterAcceleration`, keep propellant at zero, reject thruster velocity gain, assert `PropellantDry` without `ThrusterActive`, and compare drift proof against sanitized velocity.
Rejected Alternatives: Treating propellant UI as presentation-only would allow movement truth to diverge under dry fuel; comparing drift against raw non-finite input would make the test prove the wrong contract.
Scalability potential: Low/Middle/High/Ultra all share the same dry-fuel truth; high-tier presentation can add richer dry-rattle/haptic feedback from output DTOs without touching propulsion authority.
Hardware Impact: no new runtime branch beyond the existing `propellant01 > 0.0f` guard; editor proof only, expected runtime cost unchanged below 1 us.

Problem: APEX proof needed to cover the Burst job source directly, not only the MonoBehaviour runtime wrapper.
Solution: Add editor static contract coverage for `ZeroGPhysicsIntegrationJob.Execute` and `ResolveAnalyticOrbitSurface`, banning scene queries, `GetComponent`, `GlobalRegistry.Get<T>()`, managed collections, `foreach`, `Schedule(`, and `.Complete(`.
Rejected Alternatives: Relying on one repo-wide rg command is fragile because future edits could bypass the manual check; embedding the scanner in NUnit makes the contract repeatable.
Scalability potential: Low devices avoid accidental physics-scene and fence stalls; high/ultra tiers can spend cycles through explicit quality knobs instead of hidden job fences.
Hardware Impact: runtime cost is 0 us; editor-only scanner prevents future multi-millisecond stalls from synchronous Unity physics or job completion misuse.

Problem: A low propellant value smaller than the current substep drain could still produce a full substep of thrust before being clamped to zero.
Solution: Compute requested drain, derive `thrustScale = saturate(propellant01 / requestedDrain)`, scale velocity delta and fuel drain by that factor, and cover the half-budget case in `IntegrationJob_LastPropellantFractionScalesThrusterImpulse`.
Rejected Alternatives: Keeping full thrust until the gauge reaches zero creates free momentum and undermines survival-resource readability.
Scalability potential: Low through Ultra keep identical propulsion truth; presentation can independently exaggerate dry sputter, HUD warning, or haptics from output flags.
Hardware Impact: adds two scalar multiplies, one divide, and one saturate only while thrust is requested; expected below 0.5 us on i3/MX350 for the single-player solver.

Problem: `FixedTick` could schedule the solver even when `WriteFrameInput` failed to write a new frame, allowing stale input/tuning to advance simulation after a DataVault guard denial.
Solution: Capture `_scheduledFrame` before input write and return before scheduling when the frame remains unchanged.
Rejected Alternatives: Letting telemetry mark the denial while still running old input would be diagnosable but still mutates gameplay truth from stale data.
Scalability potential: Low through Ultra keep identical fail-closed simulation behavior; high-tier diagnostics can visualize the vault denial from telemetry without movement drift.
Hardware Impact: one uint compare per fixed tick; prevents wasted solver job when input ownership is unavailable.

Problem: DataVault hot-swap could call `ReleaseVaultBuffers` after `CompletePendingJob` even if the active job was not completed, risking release of buffers still referenced by a scheduled job.
Solution: Store pending replacement vault and apply it only in `ApplyPendingDataVaultReplacementWhenSafe` when `_jobScheduled` and `_jobBuffersLocked` are both false; call this after completion and in no-job paths.
Rejected Alternatives: Blocking on job completion during service replacement would violate dispatcher/fence policy and could stall the frame.
Scalability potential: Low devices avoid rare catastrophic release/use-after-free faults; high/ultra retain the same safe service replacement route under richer tooling.
Hardware Impact: no normal-frame cost except a cold branch after job completion; removes a potential crash vector during registry service replacement.

Problem: Applying a pending DataVault replacement after job readback but before visual flush could initialize the replacement vault from the old transform, then apply the pending solver readback afterward.
Solution: Add `_hasPendingVisualSyncReadback` to the deferred replacement gate and invoke `ApplyPendingDataVaultReplacementWhenSafe` after `FlushVisualSyncReadback`.
Rejected Alternatives: Reinitializing immediately after job completion is simpler but can produce one-frame stale replacement state during service swaps.
Scalability potential: Low through Ultra preserve one phase route: simulation readback -> VISUAL_SYNC presentation -> cold vault replacement.
Hardware Impact: cold-path branch only; prevents a rare phase-order drift during service replacement.

Problem: While DataVault replacement was pending, external reads/writes and a new fixed tick could still target the old vault before replacement application.
Solution: Add `_hasPendingReplacementVault` gates to `FixedTick`, `TryWriteExternalInput`, and `TryGetCachedVault`.
Rejected Alternatives: Allowing old-vault access until release would be simpler but creates an authority split during service swaps.
Scalability potential: Low through Ultra use the same fail-closed replacement window; debug UI can observe absence of fresh data rather than reading stale data.
Hardware Impact: one cold/cheap branch on access paths; avoids stale-vault scheduling and writes.

Problem: Core input exposes `Dash`, but zero-G input conversion only mapped `Jump` to Push-and-Glide.
Solution: Map `Dash | Jump` to `ZeroGInputActions.PushAndGlide` and add a semantic test proving dash does not imply thruster.
Rejected Alternatives: Treating dash as ignored wastes an existing discrete movement action and weakens suit-control feel in 6DOF.
Scalability potential: Low through Ultra keep the same movement truth; higher tiers can layer stronger haptics/VFX for dash push without changing DTOs.
Hardware Impact: one bitwise OR in the pure converter; runtime cost effectively unchanged.

Problem: The 6DOF roll route from primary/secondary fire was implemented but not covered by an executable proof.
Solution: Add `DeterministicInputSignal_PrimaryAndSecondaryMapToOpposedRoll` to assert primary roll +1, secondary roll -1, both cancel, and none imply thruster.
Rejected Alternatives: Relying on visual playtesting would miss input-regression in a headless solver path.
Scalability potential: Low through Ultra keep the same roll truth; presentation tiers can scale suit roll feedback separately.
Hardware Impact: editor-only proof; runtime cost unchanged.

Problem: During pending disable teardown, `s_activeRuntime` can intentionally remain until LateFrame flush, but static external read/write APIs could still target that owner.
Solution: Add `_pendingDisableTeardown` gates to `FixedTick`, `TryWriteExternalInput`, and `TryGetCachedVault`.
Rejected Alternatives: Clearing `s_activeRuntime` before final visual sync would drop pending presentation readback; accepting access during teardown risks stale writes.
Scalability potential: Low through Ultra preserve the same shutdown order: finish simulation readback, flush visual sync, then release ownership.
Hardware Impact: one cheap branch on access paths; avoids teardown-window stale reads/writes.

Problem: `ZeroGMovementRuntime` uses fixed DataVault BufferIDs, so multiple enabled instances would become multiple writers to the same movement truth.
Solution: Reject second runtime activation in `OnEnable` before DataVault allocation and dispatcher registration if `s_activeRuntime` is already another instance.
Rejected Alternatives: Letting the last enabled instance win would hide duplicate scene setup errors and create non-deterministic writer order.
Scalability potential: Low through Ultra keep one player movement authority; future multi-actor zero-G must allocate per-entity buffers instead of reusing singleton BufferIDs.
Hardware Impact: one cold OnEnable branch; prevents duplicate fixed tick work and DataVault stomping.

Problem: After a mutation guard was acquired, failure to open required DataVault buffers returned fail-closed but did not increment the numeric vault-denial telemetry counter.
Solution: Add `RecordVaultAccessDenied()` before the old-frame return in `WriteFrameInput` and before `return false` in `TryAcquireJobBufferViews`.
Rejected Alternatives: Logging or JSON reports would violate hot-path/IO constraints; silent failure weakens postmortem evidence.
Scalability potential: Low through Ultra get the same cheap numeric fault path; higher-tier debug UI can interpret the ring later.
Hardware Impact: one saturated integer increment only on abnormal buffer-open failure; normal frame cost unchanged.

Problem: The Burst solver sanitized NaN/Inf inputs into safe values but could erase the evidence that the source state, input, tuning, camera origin, or delta time was non-finite.
Solution: Detect raw non-finite sources before sanitize, preserve `ZeroGMovementFaultCodes.NonFinite`, set `NaNDetected`, and carry the fault into state/output/telemetry even when sanitized math stays finite.
Rejected Alternatives: Waiting for final output non-finite detection is too late because sanitize can hide the original corruption; logging strings or dumps would violate hot-path and I/O policy.
Scalability potential: Low/Middle/High/Ultra all share the same cheap numeric forensic path; high-tier debug UI can render richer postmortem overlays from the ring without changing movement truth.
Hardware Impact: one bool expression over flat DTO fields per solver job; expected below 1 us on i3/MX350 for the single player, with no managed allocation.

Problem: DTO size-only verification did not prove fault-critical field offsets, leaving future edits able to move hash/fault/substep fields without failing the layout verifier.
Solution: Expand `ZeroGMovementLayoutVerifier` to assert critical offsets for state orientation/flags/fault, input view/flags, tuning substeps/hash, surface flags/hash, output fault, and test hash.
Rejected Alternatives: Relying on `[FieldOffset]` declarations alone gives no executable proof if a later edit changes declarations incorrectly.
Scalability potential: Low through Ultra preserve the same DataVault ABI and ARM64 alignment contract; richer presentation tiers can consume DTOs without layout ambiguity.
Hardware Impact: editor-only offset checks; runtime hot path cost is 0 us.

Problem: Push-and-Glide only applied when the player was already penetrating the analytic orbit wall, which made tactile wall push-off unreliable and encouraged clipping as an input condition.
Solution: In the analytic Burst collision solver, compute nearest non-penetrating wall clearance for the six interior AABB planes and apply one push impulse when `PushAndGlide` is requested inside `SurfaceProbeRadiusMeters`.
Rejected Alternatives: Adding Unity `SpherecastCommand` here would introduce a second physics query pipeline, extra native scratch ownership, and harder same-frame fence proof; keeping penetration-only behavior makes the first zero-G interaction feel broken.
Scalability potential: Low uses one nearest-plane probe; Middle/High/Ultra can drive richer wall-touch VFX/haptics from the same `ZeroGSurfaceHitDTO` without changing movement truth.
Hardware Impact: six scalar clearance comparisons and one branch per solver substep; expected below 1 us on i3/MX350, with no managed allocation or Unity Physics scene query.

Problem: Core deterministic input publishes held/latched action snapshots each frame, so mapping held Jump/Dash directly to `PushAndGlide` made wall push repeat every fixed frame while the button stayed down.
Solution: Reuse the reserved state tail as `LastActionMask` at byte offset 112, require a rising edge before applying push impulse, write the current mask back into the DTO, and include that mask in the state hash.
Rejected Alternatives: Consuming `PlayerInputSignal` discrete events in the solver would couple zero-G movement to a managed signal snapshot and add another hot dependency path; using frame numbers would still repeat while held.
Scalability potential: Low/Middle/High/Ultra keep identical movement truth; presentation tiers can exaggerate the single push with VFX/haptics without multiplying gameplay impulse.
Hardware Impact: one uint read, one bitwise latch test, one uint write, and one extra hash mix per solver frame; estimated below 0.2 us on i3/MX350, no managed allocation.

Problem: The telemetry ring was allocated with `NativeArrayOptions.UninitializedMemory`, while `TryReadLastTelemetry` could map cursor 0 to the final ring slot before any solver frame had written valid data.
Solution: Clear the entire 300-entry telemetry ring during cold bootstrap and make `TryReadLastTelemetry` reject entries whose `Frame` and `StateHash` are both zero.
Rejected Alternatives: Returning a default-looking last slot would leak undefined NativeArray contents into diagnostics; adding a managed count object would violate flat DataVault ownership.
Scalability potential: Low/Middle/High/Ultra get identical forensic semantics; debug overlays read either a real solver frame or no telemetry.
Hardware Impact: cold bootstrap writes 300 unmanaged structs once; hot read path adds one zero-frame check, estimated below 0.1 us.

Problem: Cold `GenerateEmergencyMockData` held the initialization mutation guard while reading Transform state, runtime origin, and serialized tuning.
Solution: Build all external scene/tuning state before acquiring `InitializationGuardMask`, then hold the guard only while resolving DataVault buffers, clearing telemetry, and writing DTOs.
Rejected Alternatives: Keeping cold external reads inside the guard is simple but weakens lock-flattening proof and extends guard hold time during scene boot.
Scalability potential: Low-end startup has a shorter DataVault guard window; high-tier startup can add richer bootstrap state without increasing lock scope.
Hardware Impact: no runtime-frame cost; cold boot saves guard duration by moving Transform/origin/tuning reads outside the critical section.

Problem: `01_ORBIT.unity` and the player prefab did not serialize `ZeroGMovementRuntime`, but wiring it directly through the prologue bootstrap would edit a neighboring scene/prologue domain without a route owner.
Solution: Add `ZeroGMovementRuntime.ConfigureCold(Transform authoritativeTransform, Transform orientationSource)` as a player-domain cold binding contract, return `false` if play-mode runtime is already active, and cover it with a static editor contract proving no hot-path call, no `GetComponent`, no `GlobalRegistry.Get<T>()`, no Unity physics query, and no legacy KCC type dependency.
Rejected Alternatives: Direct prefab YAML injection risks duplicate runtime ownership; editing `PrologueOrbitSceneBootstrap` would create cross-domain dependency before the scene owner declares the route; coupling to `HectonPlayerMovement` or `HydrodynamicKccRuntime` would violate zero-G/underwater solver decoupling.
Scalability potential: Low/Middle/High/Ultra keep identical simulation truth; scene owners can bind different camera/visual orientation sources cold without changing the Burst DTO path.
Hardware Impact: 0 us in fixed/simulation/visual-sync hot paths; cold setup adds only two Transform reference assignments and prevents later scene-search work on i3/MX350.

Problem: The analytic orbit-wall solver selected a single deepest penetration axis, so a corner or edge impact could resolve one wall while leaving the player still penetrating the adjacent wall until a later frame.
Solution: Replace deepest-axis penetration with clamp-to-inner-volume math: clamp the local sphere center into the allowed AABB, derive the full depenetration vector, normalize it into a diagonal contact normal, and reflect velocity against that combined normal.
Rejected Alternatives: Running iterative per-axis correction loops adds branch work and can create order-dependent corner responses; delegating corners to Unity physics queries violates the current zero-scene-query solver route.
Scalability potential: Low/Middle/High/Ultra share the same O(1) contact truth; higher tiers can spend saved stability on richer contact VFX from the same `ZeroGSurfaceHitDTO`.
Hardware Impact: replaces six penetration comparisons with one vector clamp, one length squared, one sqrt, and one rsqrt only on collision checks; expected below 1 us on i3/MX350 and removes repeated next-frame corner correction.

Problem: `BrakeAssist` reduced linear velocity and angular momentum for free, creating non-physical momentum removal even when suit propellant was empty.
Solution: Treat brake assist as counter-thrust: compute the requested damping deltas, convert them into a propellant drain request, scale the deltas by available propellant, and mark thruster activity only when actual damping work occurs.
Rejected Alternatives: Keeping free damping is comfortable but violates the cold-gas survival resource; adding a new energy meter is overengineering and creates a second truth route.
Scalability potential: Low/Middle/High/Ultra keep identical braking truth; higher tiers can sell braking with richer haptics/particles from existing output flags without changing movement math.
Hardware Impact: adds two vector lerps, two lengths, and scalar drain math only when brake is requested; expected below 1 us on i3/MX350 and prevents dry-suit stop exploits.

Problem: A serialized zero `PropellantDrainPerSecond` tuning value made `requestedDrain == 0`, leaving a dry-brake fallback path capable of damping momentum without fuel.
Solution: Sanitize propellant drain to a small positive floor and make the no-drain branch of brake scaling require nonzero propellant; extend the brake semantic test with a zero-drain dry case.
Rejected Alternatives: Treating zero drain as a harmless designer override creates an infinite free maneuvering resource and hides bad tuning until runtime; adding a separate debug bypass would add another authority route.
Scalability potential: Low/Middle/High/Ultra share the same cold-gas truth; visual tiers can scale dry-thruster sputter separately from the existing flags.
Hardware Impact: one scalar max in tuning sanitize and one select branch in brake path; expected below 0.1 us on i3/MX350, no managed allocation.

Problem: A non-finite `SuitPropellant01` source value was detected as a fault but sanitized to the fallback `1.0f`, which could turn corrupted fuel state into a full-thrust tank.
Solution: Change propellant sanitization fallback to `0.0f` and extend the non-finite source test so NaN fuel plus thrust request yields dry state, no velocity gain, `NonFinite` fault, and `NaNDetected` telemetry.
Rejected Alternatives: Keeping full-fuel fallback maximizes player comfort but violates fail-closed movement truth; clamping to previous-frame fuel would require another historical alias outside the flat state DTO.
Scalability potential: Low/Middle/High/Ultra share the same fault semantics; presentation tiers can use `NaNDetected` and `PropellantDry` flags for stronger dry-suit feedback.
Hardware Impact: no extra runtime operations; the existing sanitize call uses a different scalar fallback, and the proof is editor-only.

Problem: AUP and camera AUP can both be finite in double precision while their local offset overflows during `double3` to `float3` conversion, silently zeroing local simulation without preserving a fault.
Solution: Detect non-finite raw local offset or converted local position before simulation, mark `NonFinite`/`NaNDetected`, suppress all movement actions for that frame, and preserve the original double AUP instead of committing camera-origin fallback.
Rejected Alternatives: Always rebasing inside the player solver would violate AUP ownership and create a cross-domain route; silently clamping local position hides coordinate corruption from telemetry.
Scalability potential: Low/Middle/High/Ultra keep the same AUP truth; high-tier presentation can visualize the fault, but simulation does not invent movement from an invalid local coordinate.
Hardware Impact: one double3 subtraction and two finite checks per solver job; expected below 0.2 us on i3/MX350, no managed allocation.

Problem: Raw non-finite source detection marked a fault but sanitized values could still allow same-frame thrust, roll, horizon lock, brake, or push if the corrupted source did not itself dry the suit or overflow local AUP; the same risk existed for corrupted input axes sanitized to zero while action bits stayed active.
Solution: Add a single `frameInputAllowed = !sourceNonFinite && !localOffsetFault` gate, use it to zero local thrust/angular input, disable action booleans, and force `subDt = 0` for the corrupted frame while still writing numeric fault telemetry.
Rejected Alternatives: Sanitizing then continuing simulation is comfortable but can convert corrupted source state into real motion; reverting to previous-frame state would require another hidden history route outside the flat DTO.
Scalability potential: Low/Middle/High/Ultra share identical fail-closed gameplay truth; high-tier presentation can amplify the existing fault flags without changing movement authority.
Hardware Impact: one bool and several scalar selects in the single-player Burst job; expected below 0.2 us on i3/MX350, with no managed allocation and less corrupted-frame work because the substep delta becomes zero.

Problem: A faulted frame that blocked Push-and-Glide still wrote raw `input.ActionMask` into `LastActionMask`, causing the next valid held push to be treated as already consumed.
Solution: Introduce `acceptedActionMask`, set it to raw input only when `frameInputAllowed` is true, preserve `previousActionMask` during fault blackout, and hash/write the accepted mask instead of the raw mask.
Rejected Alternatives: Forcing the player to release and press again after a transient solver fault is an input loss disguised as determinism; clearing the latch on every fault would duplicate held pushes after faults that happen after a legitimate push.
Scalability potential: Low/Middle/High/Ultra keep identical input truth; higher visual tiers can display fault feedback without changing accepted gameplay input.
Hardware Impact: one uint select per solver frame; expected below 0.05 us on i3/MX350, no allocation.

Problem: The fault blackout gate zeroed `horizonWeight` and wrote that zero back to state, so one non-finite frame could silently disable Horizon Lock assist for later valid frames.
Solution: Always sanitize and preserve `HorizonLockWeight`; rely on `frameInputAllowed` only to block the Horizon Lock action for the corrupted frame.
Rejected Alternatives: Clearing assist state during a fault is not fail-closed; it is persistent player-control degradation unrelated to the corrupted input.
Scalability potential: Low/Middle/High/Ultra keep identical assist truth; quality tiers may vary presentation feedback, not control state.
Hardware Impact: removes a conditional select and keeps one scalar sanitize; no additional hot cost.

Problem: Deterministic input packing sanitized NaN/Inf axes before the solver could see source corruption, allowing partial movement from a damaged `InputSignal` or fallback to mock movement.
Solution: Reject non-finite move/look/vertical/view/scale values in `TryPackDeterministicInputSignal`; when a fresh signal exists but fails packing, `TryBuildDeterministicSignalInput` emits a finite no-op DTO with `SignalDrop` and external-input flags.
Rejected Alternatives: Silent sanitization hides input corruption; fallback to mock input can move the player from unrelated serialized debug values.
Scalability potential: Low/Middle/High/Ultra share the same input truth; high-tier UI can show signal-loss feedback from the flag without changing locomotion.
Hardware Impact: five finite checks in PRE_SIMULATION input packing only; expected below 0.1 us on i3/MX350, no managed allocation.

Problem: External-authority zero-G input stored in DataVault could contain raw NaN/Inf axes or view quaternion values; the old sanitizer clamped those values but preserved action bits, allowing a corrupt DTO to become partial thrust, brake, or push intent.
Solution: Replace the private managed-input sanitizer with pure `SanitizeExternalAuthorityInput`, detect raw non-finite DTO fields first, zero thrust/angular axes, clear `ActionMask`, and set `SignalDrop` before the frame input bridge removes `ExternalAuthority`.
Rejected Alternatives: Passing the corrupt DTO into the Burst solver would preserve `NonFinite` fault evidence but still lets an external writer bypass the deterministic input fail-closed contract; silently clamping only the axes leaves discrete actions live.
Scalability potential: Low/Middle/High/Ultra use one input truth route; weak devices avoid corruption-induced movement, while high/ultra presentation can render signal-loss feedback from the existing flag without changing gameplay authority.
Hardware Impact: four finite checks and one branch only when external-authority DTOs are sanitized; expected below 0.1 us on i3/MX350, no heap allocation and no DataVault layout change.

Problem: `OnEnable` rejected duplicate active zero-G runtimes, but deferred DataVault replacement could later call `EnsureBuffers(true)` and assign `s_activeRuntime = this` from another registered runtime after the original owner was already active.
Solution: Add an ownership guard inside `ApplyPendingDataVaultReplacementWhenSafe` before `ReleaseVaultBuffers`, `_dataVault` swap, and `EnsureBuffers(true)`; a non-owner drops its pending replacement and becomes inactive.
Rejected Alternatives: Relying only on the `OnEnable` guard leaves a cold bootstrap race during service replacement; releasing buffers from a non-owner risks deleting shared fixed BufferIDs owned by the real runtime.
Scalability potential: Low/Middle/High/Ultra keep exactly one movement authority for the singleton player path; future multi-actor zero-G must allocate per-entity BufferIDs instead of bypassing this guard.
Hardware Impact: one cold-path branch during DataVault replacement only; 0 us in fixed solver and visual sync hot paths.

Problem: Horizon Lock used `cross(currentUp, horizonUp)` as its correction axis and returned unchanged when the cross product was zero, so exact upside-down 180-degree orientation could never recover.
Solution: Compute the dot first; if vectors are already aligned, return; if they are anti-parallel, use the current local-right axis as a deterministic quaternion correction axis and continue with `AxisAngle` multiplication.
Rejected Alternatives: Euler roll/pitch correction would reintroduce gimbal risk; random fallback axes would make replay orientation nondeterministic.
Scalability potential: Low/Middle/High/Ultra share identical horizon-assist truth; visual tiers can scale disorientation effects separately while the quaternion solver remains deterministic.
Hardware Impact: one extra dot and a cold anti-parallel branch inside Horizon Lock only; expected below 0.1 us on i3/MX350 when assist is active, no allocation.

Problem: The non-owner branch of deferred DataVault replacement correctly refused to steal `s_activeRuntime`, but its teardown path called `ReleaseVaultBuffers`, which can release fixed BufferID storage owned by the real active runtime.
Solution: Split teardown into owner and non-owner paths. Owners still use `ReleaseVaultBuffers` before rebinding vaults. Non-owners now unregister dispatcher/hotswap callbacks, call `ClearVaultHandlesWithoutRelease`, null the cached vault, and mark themselves inactive.
Rejected Alternatives: Leaving a dormant non-owner registered costs repeated callbacks; calling `ReleaseVaultBuffers` from a non-owner risks deleting shared singleton movement buffers; allocating per-runtime replacement buffers would violate the current one-player BufferID route.
Scalability potential: Low/Middle/High/Ultra keep exactly one movement authority and one fixed zero-G DataVault route; future multi-actor support must allocate per-entity BufferIDs rather than weakening this singleton guard.
Hardware Impact: cold replacement path only; removes dormant callback overhead and avoids catastrophic shared-buffer deletion. Expected hot-frame cost: 0 us on i3/MX350.

Problem: `CompletePendingJob` patched the last telemetry slot and copied solver readback without proving that the DTOs still belonged to the exact completed frame; a ring cursor mismatch or stale DataVault view could make forensic timing or VISUAL_SYNC state lie.
Solution: Capture `_scheduledFrame` as `completedFrame`, pass it into telemetry patch and readback, reject state/output pairs whose frames differ, reject mismatched state/output hashes, and refuse to patch telemetry elapsed time unless the entry frame equals the completed frame.
Rejected Alternatives: Scanning the full 300-entry telemetry ring each frame is unnecessary CPU spend; trusting the cursor without a frame check leaves black-box data vulnerable to stale-slot corruption.
Scalability potential: Low/Middle/High/Ultra keep the same movement truth and telemetry route; high/ultra can add richer presentation from a readback that is now explicitly frame-bound.
Hardware Impact: three integer compares and one hash compare per completed solver job; expected below 0.05 us on i3/MX350, no heap allocation.

Problem: The surface solver wrote `LowTierProbe` when `quality < 0.35f`, creating a binary quality switch inside a physics DTO instead of exposing continuous `GlobalQualityWeight`.
Solution: Remove low-tier flag emission from the Burst solver and preserve `QualityProbeWeight` as the single continuous scalar for presentation/VFX scaling.
Rejected Alternatives: Keeping a boolean tier flag is convenient for presentation, but it violates the project rule that quality must be continuous and must not create discrete gameplay/DTO authority branches.
Scalability potential: Low/Middle/High/Ultra now consume the same surface contact truth with a continuous quality scalar; weak devices can reduce VFX intensity and ultra devices can overdrive VFX without changing solver decisions.
Hardware Impact: removes two branches from the surface contact path; tiny but real branch reduction, expected below 0.02 us on i3/MX350.

Problem: `OnDisable` could complete a pending solver job, create `_hasPendingVisualSyncReadback`, then immediately call `FinishDisableTeardown`, clearing the readback and releasing buffers before `LateFrameTick` owned presentation sync.
Solution: Gate disable teardown on both `_jobScheduled` and `_hasPendingVisualSyncReadback`; keep LateFrame registered until the pending readback is flushed, matching the existing PostFixed/LateFrame teardown conditions.
Rejected Alternatives: Dropping visual sync on disable is simpler but violates the phase contract; forcing transform writes in `OnDisable` would move presentation outside VISUAL_SYNC.
Scalability potential: Low/Middle/High/Ultra keep identical simulation truth; all tiers get deterministic final pose/signal transfer before teardown.
Hardware Impact: one cold lifecycle boolean check only; hot-frame cost 0 us.

Problem: `TryReadState` exposed `ZeroGMovementStateDTO`, `ZeroGSolverOutputDTO`, and tuning without proving that the state and solver output came from the same completed frame.
Solution: Add a pure read-side frame/hash coherence gate: if `state.Frame != output.Frame` or `output.StateHash != state.StateHash`, clear all returned DTOs and return `false`.
Rejected Alternatives: Trusting DataVault cursor order leaves external readers vulnerable to stale or torn snapshots; adding a new lock would increase contention and violate read accessor purity.
Scalability potential: Low/Middle/High/Ultra keep identical movement truth; high-tier diagnostic/presentation consumers can rely on coherent zero-G snapshots without changing the solver.
Hardware Impact: two integer/hash compares in a public read accessor; expected below 0.02 us on i3/MX350, no managed allocation.

Problem: Public zero-G DataVault access did not explicitly fail closed during vault allocation lock or compaction fence, even though the batch prompt requires fence checks before DTO access.
Solution: Capture one local `IDataVault` in each accessor, then add `IsAllocationLocked` and `IsCompactionFenceActive` gates to `TryWriteExternalInput` before write-lock acquisition and to `TryGetCachedVault` before cached read handle resolution.
Rejected Alternatives: Relying on downstream handle resolution is too implicit for a movement authority route; reading `_dataVault` repeatedly across a hotswap window weakens the proof; acquiring an extra read lock would add contention without improving snapshot purity.
Scalability potential: Low/Middle/High/Ultra preserve the same fail-closed movement route during memory maintenance; diagnostics can observe absent fresh data instead of torn vault aliases.
Hardware Impact: two property reads on public access paths; expected below 0.02 us on i3/MX350, no managed allocation.

Problem: Internal fixed-frame work still reached input packing, emergency bootstrap scene reads, or job-buffer mutation guard acquisition before an explicit allocation/compaction fence preflight.
Solution: Add early `IsAllocationLocked` and `IsCompactionFenceActive` gates to `WriteFrameInput`, `GenerateEmergencyMockData`, and `TryAcquireJobBufferViews`; record a numeric vault-denial fault on fixed-frame input/job paths.
Rejected Alternatives: Letting `TryAcquireMutationGuard` fail later is too implicit and still spends work under a memory-maintenance fence; adding a second DataVault lock would increase contention without improving safety.
Scalability potential: Low/Middle/High/Ultra all skip the same fixed-frame simulation work during DataVault maintenance; high-tier diagnostics can render the existing numeric denial fault without changing movement truth.
Hardware Impact: two vault property reads before input packing and two before job buffer acquisition; expected below 0.03 us on i3/MX350, with larger savings during compaction because deterministic input packing and job guard attempts are avoided.

Problem: `TryReadLastTelemetry` only rejected a completely default ring slot, so a zero hash or non-finite corrupted entry with a nonzero frame could be exposed to diagnostics.
Solution: Reject `Frame == 0`, `StateHash == 0`, and non-finite local position, velocity, angular momentum, collision impulse, propellant, or solver time before returning the telemetry entry; prove the Burst state hash writer clamps zero to one.
Rejected Alternatives: Trusting the cursor is cheap but leaves forensic readers vulnerable to half-written or corrupted ring state; adding a DataVault read lock would violate read-accessor purity and increase contention.
Scalability potential: Low/Middle/High/Ultra use the same black-box telemetry ring; richer diagnostic overlays consume only coherent numeric entries.
Hardware Impact: one public-read finite scan over three float3s and three floats; expected below 0.05 us on i3/MX350 and no managed allocation.

Problem: VISUAL_SYNC accepted `double3` AUP-local offsets after only finite checks, so a finite orbital-scale value beyond `float.MaxValue` could cast to an invalid Unity `Vector3`; camera/haptic publication also trusted solver output and serialized haptic scale without its own presentation finite gate.
Solution: Add `LocalDoubleFitsFloat3` before the Transform cast, add `OutputSignalPayloadIsFinite` before camera/haptic publication, and sanitize `_hapticScale` before deriving haptic intensity.
Rejected Alternatives: Clamping presentation position to `float.MaxValue` would display a false location and hide an AUP route fault; relying solely on Burst solver sanitation leaves VISUAL_SYNC vulnerable to stale/corrupted readback or serialized presentation-scale corruption.
Scalability potential: Low/Middle/High/Ultra keep identical movement truth; weak devices fail closed without transform corruption, while high/ultra presentation can still consume richer impact/haptic signals when the payload is coherent.
Hardware Impact: one `double3` abs/range check on transform readback and four scalar/vector finite checks before signal publication; expected below 0.05 us on i3/MX350, no heap allocation.

Problem: A finite AUP-local offset can fit in `float3` while its squared length overflows to infinity; the analytic orbit-wall solver then risks NaN depenetration and can collapse the preserved double AUP to camera origin during final sanitation.
Solution: Add `math.lengthsq(rawLocalPosition)` to the local-offset fault gate before substeps and surface solving; if it is not finite, block all frame input, preserve the original double AUP, and write the existing numeric non-finite fault.
Rejected Alternatives: Clamping the local position to the orbit bounds would hide a broken AUP/camera-origin route and create a false in-bounds player location; widening collision math to double would spend CPU in the single-player hot solver while still owning the wrong coordinate problem.
Scalability potential: Low/Middle/High/Ultra share the same fail-closed AUP truth; weak devices avoid NaN propagation, while higher tiers can visualize the fault from existing telemetry without changing movement authority.
Hardware Impact: one `float3` dot/lengthsq and one finite check per solver frame; expected below 0.03 us on i3/MX350, no heap allocation.

Problem: `ActionMask` accepted reserved bits from external authority DTOs and preserved unknown bits from `LastActionMask`, letting non-zero garbage affect latch state and state hash even when those bits did not drive movement.
Solution: Define `ZeroGInputActions.SimulationMask` and `ValidMask`, mask external-authority inputs at the cold DTO boundary, and mask previous/current solver action masks before edge detection, action booleans, accepted latch storage, and hashing.
Rejected Alternatives: Trusting upstream input writers or ignoring the unknown bits only in action checks leaves false state in the latch/hash path; throwing exceptions or logging would add managed hot-path behavior instead of fail-closed numeric sanitation.
Scalability potential: Low/Middle/High/Ultra keep identical input truth and replay hashes; higher-tier presentation can add new actions only after explicitly extending the mask contract.
Hardware Impact: two bitwise AND operations in the input/solver path; expected below 0.01 us on i3/MX350, no managed allocation.

Problem: `IsFreshInputSignal` treated `Frame == 0`, `currentFrame == 0`, and future-frame input as fresh, so a bootstrap or phase-ahead signal could become real zero-G movement before the dispatcher frame contract had settled.
Solution: Route freshness through `IsFreshInputSignalForFrame`, reject zero sequence, zero signal frame, zero current frame, future frames, and signals older than the two-frame tolerance; add a semantic editor proof for current, max-age, stale, zero, and future cases.
Rejected Alternatives: Accepting future frames to tolerate dispatcher wrap is unsafe because `uint` frame wrap is not the normal-frame path and would let phase-ahead input mutate movement truth; converting future frames to stale fallback would still hide an upstream ordering bug.
Scalability potential: Low/Middle/High/Ultra keep the same deterministic input gate; weak devices avoid invalid movement from delayed or unstamped input, while high/ultra presentation can show signal loss using existing flags without changing locomotion authority.
Hardware Impact: two additional uint comparisons in the deterministic input sidecar gate; expected below 0.01 us on i3/MX350, no managed allocation and no DataVault layout change.

Problem: If cold `OnEnable` reached a DataVault allocation or compaction fence, `EnsureBuffers(true)` could fail while the runtime still registered dispatcher callbacks; later `FixedTick` used `EnsureBuffers(false)`, so the runtime had no owner and no recovery path.
Solution: Add `TryEnsureRuntimeOwnership` before any fixed-frame input write or solver schedule. It accepts the existing owner, tears down non-owner duplicates, waits while DataVault is absent, retries cold buffer initialization only before steady-state simulation begins, and assigns `s_activeRuntime` only after buffers are initialized.
Rejected Alternatives: Forcing `OnEnable` to disable the component on a transient fence would require scene-side reactivation and drop the orbital prologue movement owner; letting registered callbacks spin forever preserves CPU waste and leaves static APIs ownerless.
Scalability potential: Low/Middle/High/Ultra recover through the same owner route after memory maintenance; weak devices avoid permanent no-movement bootstrap failures during DataVault fences, while higher tiers can still build richer VISUAL_SYNC output from the same buffers after ownership is claimed.
Hardware Impact: one cold-branch ownership check per fixed tick, expected below 0.02 us steady-state because the first `ReferenceEquals` returns true; bootstrap retry work only runs before the solver has become active.

Problem: `TryReadState` proved state/output frame and hash coherence, but it did not reject zero-frame, zero-hash, or non-finite state/output/tuning payloads before returning a public snapshot to external consumers.
Solution: Add read-side fail-closed predicates for `ZeroGMovementStateDTO`, `ZeroGSolverOutputDTO`, and `ZeroGTuningDTO`; reject uninitialized hashes/frames and corrupted numeric fields before exposing the snapshot.
Rejected Alternatives: Trusting solver sanitation is insufficient for public read access because DataVault buffers can be stale, half-initialized, or externally corrupted; adding locks would violate read-accessor purity and increase contention.
Scalability potential: Low/Middle/High/Ultra consume the same coherent zero-G snapshot route; richer high-tier diagnostics can depend on finite state without changing simulation truth or DTO layout.
Hardware Impact: public read accessor gains flat finite scans only when called, expected below 0.08 us on i3/MX350; fixed solver and VISUAL_SYNC hot paths receive no new allocation or lock work.

Problem: Completed solver readback was frame/hash checked, but a zero-hash or non-finite state/output payload could still enter the deferred VISUAL_SYNC lane and reach transform, camera, haptic, or fault signal publication.
Solution: Add the same fail-closed state/output finite and zero-hash guards to `TryReadHeldReadback` before `_pendingVisualSyncReadback` is set; extend the static editor contract around completed-frame readback.
Rejected Alternatives: Relying on `ApplyReadbackToTransform` would still allow camera/haptic signal publication from partially corrupt output; duplicating presentation-side clamps would hide the corruption and split the proof across multiple consumers.
Scalability potential: Low/Middle/High/Ultra now share one coherent readback admission gate; weak devices skip corrupted presentation work immediately, while high/ultra tiers can visualize the existing fault path without consuming invalid DTOs.
Hardware Impact: one flat finite scan on the completed job readback path only, expected below 0.06 us on i3/MX350; no managed allocation, no DataVault write lock, no job completion change.

Problem: Solver completion elapsed time was written directly to telemetry and reported to job admission after a raw Stopwatch conversion; a non-finite, negative, or absurd elapsed value could poison budget flags and blackbox timing.
Solution: Add `JobBudgetExceededMs` and `MaxRecordedSolverElapsedMs`; sanitize `ResolveElapsedJobMs` for invalid frequency, non-positive delta, NaN, infinity, and excessive elapsed values; re-sanitize inside `PatchLastTelemetryElapsed` before writing the telemetry ring.
Rejected Alternatives: Clamping only in `TryReadLastTelemetry` would hide corrupt stored forensic data; clamping only at telemetry write would still let job admission receive a bad elapsed value.
Scalability potential: Low/Middle/High/Ultra keep the same timing truth and budget flag semantics; weak devices avoid false budget spikes from invalid clocks, while high/ultra diagnostic overlays can trust bounded solver timing.
Hardware Impact: completion-only scalar checks and one double conversion guard, expected below 0.02 us per completed solver frame on i3/MX350; no hot solver allocation and no DataVault lock expansion.

Problem: Telemetry readers derived the last ring index with `cursor - 1`; corrupt negative cursors were treated as wraparound and could read or patch the final ring slot.
Solution: Add `TryResolveTelemetryLastIndex` and route both `TryReadLastTelemetry` and `PatchLastTelemetryElapsed` through it; reject empty rings, negative cursors, and cursors outside the live ring length before touching telemetry entries.
Rejected Alternatives: Letting the job writer normalize cursor on the next frame leaves public reads and completion patching exposed between frames; clamping negative cursors to zero would silently rewrite corruption into a valid index.
Scalability potential: Low/Middle/High/Ultra now share one deterministic blackbox cursor contract; weak devices avoid misleading crash telemetry from stale final-slot reads, while high/ultra diagnostics can trust the last-entry selector.
Hardware Impact: two integer comparisons and one branch on public telemetry read or completion telemetry patch only; expected below 0.01 us on i3/MX350, no heap allocation and no DataVault write-lock expansion.

Problem: `TryResolveTelemetryLastIndex` became part of public telemetry read and completion patching, but the static hot-path forbidden-token scanner did not yet include that helper.
Solution: Add `TryResolveTelemetryLastIndex` to `RuntimeHotPaths_DoNotUseColdLookupOrSceneQueries`, so future edits cannot add managed allocations, scene queries, registry polling, or debug logging inside the cursor admission path.
Rejected Alternatives: Relying only on whole-file `rg` scans is weaker because the semantic editor test is the durable in-repo proof artifact; duplicating the helper body in tests would create stale parallel logic.
Scalability potential: Low/Middle/High/Ultra keep the same blackbox helper contract; weak devices are protected from accidental GC in telemetry reads, while high/ultra diagnostics can safely expand consumers around a proven zero-GC helper.
Hardware Impact: no runtime cost; one additional editor-only static assertion method extraction.

Problem: The blackbox contract is a fixed 300-frame ring, but `TryResolveTelemetryLastIndex` accepted any positive telemetry length; a truncated or wrong-sized DataVault buffer could therefore expose stale or partial forensic data.
Solution: Require `telemetryLength == TelemetryCapacity` inside `TryResolveTelemetryLastIndex` before resolving the previous cursor entry; update the editor contract to assert the fixed-capacity gate.
Rejected Alternatives: Accepting smaller buffers for resilience would violate the crash-ring proof artifact and make cursor wrap semantics data-dependent; clamping to the available length hides DataVault layout corruption.
Scalability potential: Low/Middle/High/Ultra now share one fixed blackbox capacity contract; weak devices avoid misleading under-sized telemetry, while high/ultra diagnostics can trust the 300-frame history shape.
Hardware Impact: one integer equality check on telemetry read/patch only, expected below 0.01 us on i3/MX350; no managed allocation and no additional DataVault locks.

Problem: Public telemetry reads now require the fixed 300-frame ring, but `TryAcquireJobBufferViews` still accepted oversized telemetry rings and any positive cursor buffer, allowing Burst write admission to a shape the public blackbox contract would reject.
Solution: Require `telemetry.Length == TelemetryCapacity` and `telemetryCursor.Length == 1` before `_jobBuffersLocked` is set and before the job receives NativeArray views; extend the fail-closed editor proof.
Rejected Alternatives: Letting the writer run and rejecting only public reads wastes simulation/postmortem work and hides DataVault layout corruption until consumers ask for telemetry.
Scalability potential: Low/Middle/High/Ultra share the same telemetry shape at writer and reader boundaries; weak devices avoid wasted job completion patching on invalid buffers, while high/ultra diagnostics can rely on exact ring geometry.
Hardware Impact: two integer equality checks during job buffer admission only, expected below 0.01 us on i3/MX350; no managed allocation and no additional write locks.
