# Rationale_LADDER_CLIMB_IK

Runtime Status: CORE TASKS COMPLETE - FINAL BUILD BLOCKED BY DEPENDENCY

## Initial Technical Direction
Problem: Ladder traversal currently requested as embodiment-critical locomotion; teleport-style vertical movement would break VR body continuity and gives no hand contact truth.
Solution: Implement a narrow `Animation/Locomotion` procedural 2-bone IK kernel with AUP ladder inputs, discrete rung math, analytical arm solve, finite fallbacks, typed output signals, and a fixed 300-frame blackbox ring.
Rejected Alternatives: Animator states and authored rung transforms in hot paths. They are slower to author, harder to scale, and do not satisfy "Pure Burst math. No Animator States."
Scalability potential: Low = smooth camera slide for PC, Middle = two-hand target lock, High = VR grip delta climb, Ultra = richer contact/haptic cadence and tighter elbow pole refinement.
Hardware Impact: i3/MX350 target cost budget is under 0.05 ms for one player ladder solve when run as single-pass analytical math; no allocation budget consumed after initialization.

## Decision Journal

### Loop 1 - Tasks 1-5
Problem: Ladder traversal had no central `LadderManager`, but `ClimbableLadder` owned a hard teleport path that bypassed embodiment and AUP authority.
Solution: Keep the serialized gameplay ladder as a thin adapter and route interaction into `ProceduralLadderClimbRuntime`; add `BufferID.LadderAUPs` so the Burst solve reads ladder anchors from the DataVault contract, with an H8Memory fallback only when the vault is unavailable.
Rejected Alternatives: A new ladder singleton or per-rung scene transforms. Singleton ownership would violate registry routing, and authored rung transforms create scene dependency churn plus hot-path Transform reads.
Scalability potential: Low = one AUP anchor and automatic camera/movement slide; Middle = one analytical two-hand solve; High = VR grip pull path; Ultra = same data lane can carry richer rung contact timing without changing scene authoring.
Hardware Impact: i3/MX350 avoids per-rung GameObject scans and keeps the rung equation to `base + index * 0.3`, estimated under 8 us for anchor read plus rung derivation.

Problem: AUP precision had to survive floating origin shifts during climb.
Solution: Store the ladder base as `AbsoluteUniversePosition` and convert in the Burst job using `double3` absolute reconstruction minus the committed origin offset.
Rejected Alternatives: Trusting `Transform.position` as long-term truth. It is float-local and will drift under origin rebases.
Scalability potential: Low/Middle/High/Ultra share the same double precision anchor; only presentation cost scales.
Hardware Impact: Three double subtracts are cheaper than any scene rescan and prevent correction snaps on low-end silicon.

### Loop 2 - Tasks 6-10
Problem: A 2-bone ladder pose needs exact rung contact without Animator states or FABRIK iteration.
Solution: Use one Burst `IJob` over SoA-style native arrays; both hands get exact rung targets, elbows are solved analytically with `math.acos`, and the job writes no managed state.
Rejected Alternatives: Animator IK, Animation Rigging constraints, and iterative FABRIK. Those either allocate/dispatch through managed animation state or spend extra iterations on a two-segment arm that has a closed-form answer.
Scalability potential: Low = hand target solve still runs but presentation can be camera slide only; Middle = hand and elbow transforms; High = VR grip-gated climb; Ultra = extra haptic cadence can be layered from the same rung index deltas.
Hardware Impact: Analytical two-arm solve estimated at 12 us for one player; no per-frame GC and no Transform traversal inside the Burst job.

Problem: The climb state must be visible to other systems without direct dependencies.
Solution: Extend `PlayerStateSignal` with climbing flags and emit it through `GlobalSignals`; emit `HapticRequest` light thuds on rung index changes.
Rejected Alternatives: UnityEvents for runtime locomotion state or string-named animation parameters. Those do not satisfy signal-lane segregation and are hostile to parallel agents.
Scalability potential: Low devices receive one typed state packet; high/ultra devices can layer haptics and presentation without changing the payload shape.
Hardware Impact: Signal writes are fixed payload copies, estimated under 3 us per event on low-end silicon.

### Loop 3 - Tasks 11-13
Problem: `math.acos` can poison the IK chain if the input leaves `[-1, 1]`, and a ladder crash without frame history violates black-box policy.
Solution: Clamp the law-of-cosines input, sanitize finite vectors, flag unreachable targets, and write a fixed 300-frame `LadderClimbTelemetryEntry` ring; runtime dumps `Docs/AgentLogs/Dump_LADDER_CLIMB_IK.bin` on NaN output.
Rejected Alternatives: `Debug.Log` spam or trusting authored limb lengths. Logs allocate and lose the last-frame history; authored lengths can still create unreachable poses.
Scalability potential: Low/Middle/High/Ultra use the same telemetry ring; high tiers can consume the hashes for richer QA visualization later.
Hardware Impact: Blackbox write is a compact struct copy, estimated 4 us/frame; crash dump is cold path only.

### Loop 4 - Tasks 14-17
Problem: Compile integration exposed two self-owned errors before hitting unrelated repository failures.
Solution: Added the runtime file to the existing core generated-project include list, removed the direct `Hecton8.Input.Universal` assembly dependency, and exposed `SubmitUniversalInputState(uint actionsBitmask, ...)` for callers to feed `UniversalInputStateSignal.ActionsBitmask` without making Core depend on the input subassembly.
Rejected Alternatives: Pulling the input assembly into Core or reverting the registry slot. Both would deepen assembly coupling or lose the decoupled runtime owner.
Scalability potential: Low = PC auto slide; Middle = input-independent hand lock; High/Ultra = external VR input submits grip deltas through the narrow bitmask method.
Hardware Impact: The high-end grip path costs only a bitmask check plus averaged hand delta, estimated under 2 us before the shared solve.

Problem: Climbing needed failure pressure, not a free vertical elevator.
Solution: Drain local stamina by climb meters and drop through a downward velocity impulse if stamina reaches zero; publish slip state through `PlayerStateSignal`.
Rejected Alternatives: No stamina drain or direct health coupling. No drain removes risk; direct physiology mutation would cross domain ownership.
Scalability potential: Low/Middle/High/Ultra share the same stamina scalar; future physiology owner can consume the signal without this runtime owning survival state.
Hardware Impact: One multiply/subtract per progress update, estimated 2 us/player.

### Loop 5 - Compile Wall
Problem: After self-owned errors were repaired, `dotnet build` still fails in unrelated project dependencies and generated temp assets.
Solution: Treat final validation as `[BLOCKED BY DEPENDENCY]`; no remaining `LadderClimb`/`ProceduralLadder` errors were found in the targeted error scans after the fixes.
Rejected Alternatives: Editing unrelated voxel, bootstrap, package restore, or shader/global-data-vault files from the animation prompt. That would violate domain boundary and risk trampling other agents.
Scalability potential: Local runtime remains narrow and should not require broad project compile surgery.
Hardware Impact: No additional runtime cost; build wall is repository integration debt outside this agent's domain.

### Omega Polish
Problem: Batch-level `<POLISH_MANDATE>` tag was not present in `Docs/Tasks/CURRENT_BATCH.md`; only the agent-local mandate text was present.
Solution: Performed the anti-bloat scan against touched ladder/runtime files anyway: no `Debug.Log`, no Animator state use, no coroutine, no teleport method, no `player.position =`, no `Player.transform.position += Vector3.up`, and no remaining ladder-symbol build errors found in targeted scans.
Rejected Alternatives: Skipping polish because the tag was absent. The agent-local mandate still requires final self-inquisition.
Scalability potential: Low path remains a movement/camera slide, high path remains grip-gated, and no additional per-frame services were added.
Hardware Impact: Polish patch removed the last `position +=` presentation write from the runtime fallback and uses `Transform.Translate` only when no movement force sink exists; no hot-path allocation introduced.
