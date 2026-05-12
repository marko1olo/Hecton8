# Rationale_ANIM_PROCEDURAL_BEHAVIOR

STATUS: PENDING VERIFICATION

## Session Bootstrap
Problem: Bottom-feeder locomotion uses sliding or heavy Animator IK; target is 100+ entities on i3/MX350 without hot-path GC.
Solution: Inspect existing code first, then implement a data-oriented procedural leg solver with NativeArray-backed S.O.A., Burst job kernels, async RaycastCommand path, and quality-tier raycast budgets.
Rejected Alternatives: Standard Unity Animator IK is too expensive and GameObject/Bone driven; FABRIK is rejected for crab legs because task requires analytical 2-bone solve; synchronous Physics.Raycast is rejected because it serializes terrain probing.
Scalability potential: Low = 2 legs raycasted per frame with frozen far poses; Middle = alternating side groups; High = full per-frame raycast targets; Ultra = denser visual matrices and body tilt fidelity while retaining the same data path.
Hardware Impact: Estimated low-end i3/MX350 gain is lower main-thread animation cost and 0 B/frame GC by moving work to Burst jobs and preallocated native buffers. Numeric proof is PENDING VERIFICATION.

## Loop 1 Decisions
Problem: Crabs need stable feet without Animator IK or per-crab GameObjects.
Solution: Created `ProceduralCrabLegIKRuntime` with entity S.O.A. and leg S.O.A. buffers: `NativeArray<ProceduralCrabLegEntityState>`, `NativeArray<float3> FootPositions`, `NativeArray<float3> TargetFootPositions`, and per-leg step state. DOD pattern: stable slot registry plus persistent native buffers registered with `NativeMemorySentinel`.
Rejected Alternatives: Standard Animator IK, `OnAnimatorIK`, and per-leg Transform hierarchies are too slow and not compatible with the no-GameObject crab mesh objective.
Scalability potential: Low = 4 legs with two ground probes per frame; Middle = 6 legs with alternating probe pairs; High = all legs probed every frame; Ultra = same S.O.A. path with later GPU joint upload overkill.
Hardware Impact: i3/MX350 avoids 4-6 synchronous raycasts and Animator callbacks per crab; expected savings are main-thread stalls rather than raw math cycles.

Problem: Gait instability if multiple legs on one side step simultaneously.
Solution: `ProceduralCrabStepSchedulerJob` first scans current stepping legs, locks side lanes, then triggers only one stride-over-threshold leg per side. DOD practice: deterministic local scheduler, no random gait state.
Rejected Alternatives: Randomized step offsets and authored animation events were rejected because they break determinism and require managed object state.
Scalability potential: Low = deterministic frozen targets between budgeted raycasts; Middle = same scheduler with more active entities; High/Ultra = per-frame target refresh while retaining side locks.
Hardware Impact: Branch-only side locks are cheaper than evaluating authored curves or animation state machines on MX350-class devices.

Problem: Foot lift needs visual readability without animation curves.
Solution: `AdvanceStep` uses `math.lerp` for horizontal position and a centered form of `1.0 - (t*t)` for Y lift, keeping Burst math scalar and allocation-free.
Rejected Alternatives: Unity `AnimationCurve`, sine waves, or spline arcs were rejected because they add managed sampling, trig, or unnecessary interpolation cost.
Scalability potential: Low = one multiply lift arc; Middle/High = same math; Ultra = saved cycles can be spent on GPU joint matrices and tilt polish.
Hardware Impact: Per-stepping-leg lift is multiply/add only; expected cost below 0.1us per active stepping leg on low silicon.

Problem: Terrain probing can hard-stall if performed synchronously.
Solution: Built `ProceduralCrabGroundRaycastBuildJob`, `RaycastCommand.ScheduleBatch`, and `ProceduralCrabGroundTargetResolveJob`, with a NativeArray mask so non-budgeted legs never consume stale hits.
Rejected Alternatives: `Physics.Raycast`, `Physics.RaycastNonAlloc`, and per-leg colliders were rejected because they push work back to the main thread or GameObject layer.
Scalability potential: Low = two commands per entity per frame; High = every active leg each frame; Ultra = same async path with denser visual output.
Hardware Impact: Low/MX350 converts 6 rays/entity/frame into 2 rays/entity/frame, roughly 66% fewer terrain probes for six-legged crabs.

## Loop 2 Decisions
Problem: AUP shifts can leave feet in stale local space for one frame and create infinite stretch artifacts.
Solution: `OnOriginShift` force-completes any pending crab job and runs Burst rebase jobs that subtract the shift from `FootPositions`, `TargetFootPositions`, step endpoints, entity roots, and body pose translations.
Rejected Alternatives: Waiting for the next pose update or rebasing only the body root was rejected because either path leaves feet visually detached during the shift frame.
Scalability potential: Low/Middle/High/Ultra use the same rare-path native rebase; no quality tier should tolerate origin-shift leg stretch.
Hardware Impact: Rare AUP event cost is linear native memory writes; no steady-frame cost on MX350.

Problem: Crab legs need predictable 2-bone solve without iterative FABRIK.
Solution: Implemented `ProceduralCrabAnalyticalTwoBoneIkJob` using Law of Cosines and direct direction reconstruction to produce upper, lower, and foot joint matrices.
Rejected Alternatives: FABRIK and Animator IK were rejected; both are iterative or managed-animation paths and exceed the prompt boundary.
Scalability potential: Low = fewer raycasts feeding the same IK; Middle/High = all active legs solved; Ultra = shader can consume the full joint matrix buffer for visual overkill.
Hardware Impact: No `acos`; leg solve stays vector/multiply/rsqrt heavy, appropriate for i3-class CPU.

Problem: Terrain conformity needs visual tilt without physics.
Solution: `ProceduralCrabBodyTiltJob` calculates a foot-plane normal using `math.cross(p1-p2, p3-p2)` and builds a body matrix aligned to it.
Rejected Alternatives: Rigidbody torque, ConfigurableJoint chains, or simulation-based body settling were rejected as too expensive and nondeterministic for 100+ bottom-feeders.
Scalability potential: Low = one plane per entity; High/Ultra = same normal can drive shader shell/antenna overkill later.
Hardware Impact: Plane normal costs one cross, normalization, and TRS per entity; expected below 0.3us/entity.

Problem: Solved joints must reach rendering without crab GameObjects.
Solution: Added body pose and joint matrix `GraphicsBuffer`s, material buffer binding, indirect args upload, and `Graphics.RenderMeshIndirect` submission.
Rejected Alternatives: SkinnedMeshRenderer, per-crab MeshRenderer, and transform hierarchies were rejected because they create GameObject overhead and make GPU-driven batching impossible.
Scalability potential: Low = same buffer path with cheap shaders; Middle = material reads body pose only; High = read leg joints; Ultra = shader consumes all matrices for exaggerated crab leg motion.
Hardware Impact: One indirect draw and two linear uploads replace hundreds of renderer/transform updates; exact GPU gain requires Unity play-mode capture.

## Loop 3 Decisions
Problem: Dead crabs must stop procedural stepping without falling into physics simulation.
Solution: Added `CorpseState` latch from health <= 0; raycasts stop, stepping clears, and foot targets collapse to root Y for static corpse presentation.
Rejected Alternatives: Ragdoll chains, ConfigurableJoints, and Animator death clips were rejected because they add physics or animation overhead for a state that can be visually faked.
Scalability potential: Low = static corpse matrices; Middle/High = corpses stay renderable through same indirect buffer; Ultra = shader can add visual shell collapse without CPU changes.
Hardware Impact: Corpse state removes raycasts and active stepping cost for dead entities.

Problem: Crab crowding needs separation but this runtime cannot depend on managed Eco-Director internals inside Burst jobs.
Solution: Added `SetSpatialHashAvoidance`, a native snapshot adapter that lets Eco-Director spatial hash write a separation offset/strength per slot; target-foot resolve applies the offset in the job.
Rejected Alternatives: Querying `WorldSpatialHashGrid` from the IK job was rejected because the current facade is managed and Transform/FaunaBrain oriented; direct dependency would violate multi-agent decoupling.
Scalability potential: Low = sparse separation offsets; Middle = per-neighbor Eco snapshots; High/Ultra = stronger visual stepping away without changing the IK pipeline.
Hardware Impact: Avoidance adds one vector multiply/add per raycasted leg, cheaper than collision avoidance solvers.

Problem: Critical IK system needs forensic state without managed allocations in the hot path.
Solution: Added fixed 300-entry `NativeArray<ProceduralCrabIkTelemetryEntry>` ring and binary dump to `Docs/AgentLogs/Dump_ANIM_PROCEDURAL_BEHAVIOR.bin` on NaN detection.
Rejected Alternatives: Per-frame file logs, strings, or Debug.Log telemetry were rejected because they allocate and are useless under crash pressure.
Scalability potential: Low/High/Ultra all get identical black-box coverage; telemetry is fixed capacity and does not scale with entity count.
Hardware Impact: One fixed entry write after job completion; no per-entity dump cost unless NaN is detected.

Problem: Existing project may still contain Animator IK callsites.
Solution: Ran the required ripgrep recon and logged no matches to `RECON_ANIM_PROCEDURAL_BEHAVIOR.md`.
Rejected Alternatives: Manual IDE search was rejected because the batch protocol requires objective CLI evidence.
Scalability potential: Not tiered; this is integration hygiene.
Hardware Impact: Confirms this implementation does not coexist with hidden Animator IK hot-path users in project scripts.

## Loop 4 Self-Review
Problem: Serialized `_maxEntities` could change after NativeArrays are allocated, causing job schedules to use a different capacity than buffer length.
Solution: Capacity properties now return allocated NativeArray lengths when created; serialized capacity is cold-start only.
Rejected Alternatives: Runtime resize was rejected because it would allocate and invalidate active slots in a multi-agent integration context.
Scalability potential: Low/High/Ultra keep stable native buffer lengths until destruction; avoids accidental editor-side capacity drift.
Hardware Impact: No frame cost; removes an out-of-bounds risk.

Problem: Analytical IK could receive coincident hip/foot positions and produce a zero target direction.
Solution: Target direction now uses `ContextualPhysicalIkMath.SafeNormalize` with a deterministic downward fallback before Law-of-Cosines reconstruction.
Rejected Alternatives: Early-out identity rotation was rejected because it leaves stale matrices and weak telemetry.
Scalability potential: Same at every tier; bad spawn poses do not corrupt the joint matrix buffer.
Hardware Impact: One safe-normalize path; still no `acos` or `sqrt`.

## OMEGA POLISH CHANGES
Problem: Polish mandate required bitmask flags in Burst jobs instead of scattered boolean branches.
Solution: Added `StateFlags`, `EntityFlagActive`, and `EntityFlagCorpse`; raycast build, target resolve, step scheduler, AUP rebase, body tilt, and IK jobs now test entity state with `(flags & MASK)`.
Rejected Alternatives: Keeping `IsActive`/`CorpseState` as independent branch sources was rejected because the polish mandate explicitly demands bitmask tests in Burst jobs.
Scalability potential: Low/Middle/High/Ultra use the same compact flag path; future states can be appended without new bool lanes.
Hardware Impact: Small branch-read cleanup; expected sub-0.01us/entity, but reduces state divergence risk.

Problem: Omega status demanded "VERIFIED MASTER GRADE", but the project compile still fails in a foreign survival file.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false`; final blocker is `Assets/_Project/Scripts/HectonSurvivalSystem.cs(298,29): CS0246 SurvivalPhysiologyScalarResult`.
Rejected Alternatives: Editing survival domain was rejected because it is outside the assigned fauna/procedural IK boundary and would violate the domain file without critical justification.
Scalability potential: Crab runtime remains PENDING VERIFICATION until upstream compile blocker is cleared.
Hardware Impact: No runtime impact; verification gate remains blocked.

Cinematic Cheats used:
- Foot movement is a parabolic visual arc, not animated skeletal playback.
- Body tilt is a foot-plane normal fake, not Rigidbody torque.
- Corpse state is a static Y-collapse, not ragdoll simulation.
- Spatial avoidance is a precomputed separation offset from Eco-Director, not physics collision solving.
- Analytical IK reconstructs directions directly from Law of Cosines, no angle animation, no `acos`.

## Honest R&D AAA Upgrade - 2026-05-12
Problem: Ground probes used previous `TargetFootPositions` XZ as their ray origins. That is stable for idle crabs but dishonest under root motion: a moving body can leave probes behind the intended leg home, causing foot drag, late steps, or frozen-looking rear contacts.
Solution: `ProceduralCrabGroundRaycastBuildJob` now computes each ray origin from `RootPosition + rotate(RootRotation, ResolveLegHomeLocal()) + Velocity * VelocityLeadSeconds`. This keeps foot acquisition tied to body-relative home sockets while adding a cheap anticipation bias for acceleration.
Rejected Alternatives: Full predictive gait planning, per-leg velocity filters, and terrain sweep volumes were rejected as overbuilt for the current no-GameObject crab IK scope. Reusing stale target XZ was rejected because it produces visible slide instead of AAA contact intent.
Scalability potential: Low = tiny velocity lead and two-leg raycast budget; Middle = same home probes with normal avoidance; High = all legs get current home probes every frame; Ultra = future tier can increase lead/pose polish in shader without changing the CPU data path.
Hardware Impact: i3/MX350 pays one `math.rotate` and one vector add per budgeted command, estimated ~0.04us/leg; visible gain is higher than cost because contacts stop chasing old foot positions.

Problem: The step scheduler scanned legs from index 0 every frame. With constant movement, front legs could repeatedly satisfy stride first and starve rear legs, especially on six-legged crabs.
Solution: Added `LeftStepCursor` and `RightStepCursor` to `ProceduralCrabLegEntityState`. `ProceduralCrabStepSchedulerJob` now advances active steps first, then triggers at most one candidate per side using round-robin local-leg cursors.
Rejected Alternatives: Randomizing leg order was rejected because deterministic replay and visual debugging matter. Authored gait phase tables were rejected because the system must remain procedural and data-only.
Scalability potential: Low = fair leg coverage with the existing two-ray budget; Middle = deterministic tripod-like turnover; High/Ultra = consistent all-leg visual rhythm with no extra managed state.
Hardware Impact: Cursor math is integer modulo and two attempts per side on four-leg, three per side on six-leg entities; estimated ~0.03us/entity, while avoiding visible rear-leg neglect.

Problem: Full project verification now fails before fauna IK can be proven by `dotnet build`, and the blocker set moved from the earlier survival error to wider core/platform missing symbols.
Solution: Re-ran Unity MCP `validate_script` on `Assets/_Project/Scripts/Fauna/ProceduralCrabLegIKRuntime.cs`; result was 0 warnings/0 errors. Then ran the full build and recorded the actual blocker names: `HectonPersistentPathPolicy`, `HardwareTierDetector`, `PlatformPrecisionClock`, `SteamDeckInputPal`, `HectonThreadPriorityPolicy`, `HectonThreadRole`, `HectonNativeBridge`, `HectonNativeLibrary`, and `HapticWaveformLibrary`.
Rejected Alternatives: Patching core, platform, audio bridge, haptics, or save systems was rejected because those are outside the assigned fauna procedural IK domain and would be architectural trespass without an explicit integration directive.
Scalability potential: Crab runtime stays PENDING VERIFICATION until the global compile wall is cleared, but local script validation and static zero-GC audit are clean.
Hardware Impact: No runtime impact from the blocker; build gate remains blocked upstream.

## Honest R&D Contact Safety - 2026-05-12
Problem: `IsGrounded` could stay latched after a budgeted raycast missed. That turns a previous valid contact into a lie; feet may keep stepping toward a target that no longer has terrain under the current body-relative home.
Solution: `ProceduralCrabGroundTargetResolveJob` now clears `IsGrounded` on missed budgeted probes. `ProceduralCrabStepSchedulerJob` refuses to start new steps when the leg state is ungrounded, while existing steps finish deterministically.
Rejected Alternatives: Clearing targets to zero or snapping feet to root height on every miss was rejected because that would create worse visual popping on temporary SDF holes. Keeping stale grounded state was rejected because it hides contact failure from the scheduler.
Scalability potential: Low = stale targets are frozen until the next valid two-leg probe; Middle/High = invalid contacts recover faster with more frequent probes; Ultra = future shader can visually mark slipping/unsettled feet from the grounded bit.
Hardware Impact: One byte write on miss and one byte branch per candidate leg; estimated 0.01us/budgeted leg.

Problem: Spatial-hash avoidance is an external input. If Eco-Director produces a large separation vector, the foot target could jump sideways far beyond a crab's plausible gait.
Solution: Added `_maxAvoidanceFootOffset` and `SpatialHashAvoidanceMaxOffset`. Ground target resolve clamps avoidance by length using `math.rsqrt`, preserving direction while limiting visual displacement.
Rejected Alternatives: Trusting upstream separation was rejected because multi-agent systems must be fault-contained. Normalizing every offset unconditionally was rejected because small valid offsets should remain exact and cheaper.
Scalability potential: Low = clamp keeps crowding cheap and stable; Middle = stronger crowd separation without target explosions; High/Ultra = bigger visual overkill can raise the serialized cap while preserving the same guardrail.
Hardware Impact: Fast path returns after `lengthsq` compare; active clamp path is one `rsqrt`, estimated 0.03us/raycasted leg only when avoidance exceeds cap.

Problem: Unity console verification is red for unrelated domains, so a clean global Editor state cannot be claimed.
Solution: Recorded the current blockers: `NativeArenaArrayEditTests` missing Burst symbols, `SaveBinaryStorage` Burst unsupported catch-filter, and MCP regex timeout. Crab runtime still passes `validate_script` with 0 warnings/0 errors.
Rejected Alternatives: Editing memory arena tests or save compression was rejected because those are not fauna procedural IK and there is no explicit cross-domain integration order.
Scalability potential: Crab contact safety is locally validated, but project status remains PENDING VERIFICATION until global console/build blockers are cleared.
Hardware Impact: No crab runtime impact; verification remains upstream-blocked.
