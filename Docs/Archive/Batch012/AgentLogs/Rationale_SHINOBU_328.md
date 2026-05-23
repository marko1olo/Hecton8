# Rationale_SHINOBU_328

Status: STATIC_SOURCE_PENDING_COMPILE_GATE

## Decision 01 - Isolated SHINOBU_328 Route

Problem: Existing tether code already contains SHINOBU_132/143 AUP-Verlet and GPU-buffer work, but it does not provide the exact SHINOBU_328 `TetherStateDTO` ABI, named mock/integration/force jobs, CSV material route, or editor scanner proof. Mutating those large files risks merge contention with parallel agents.
Solution: Add an isolated SHINOBU_328 runtime file in the Physics domain that consumes Vault-owned buffers and existing first-party contracts (`TetherForcePacketDTO`, `TetherSplineVertexDTO`, `TetherTelemetryEntry`, `PhysicsEventPayload`, `TetherSnappedSignal`, `TetherTensionSignal`) without deleting SHINOBU_132/143 code.
Rejected Alternatives: Rewriting `TetherInstance`, `TetherManager`, or `CablePhysicsSolver132` directly would expand compile-wall and merge risk. Inventing a new Rigidbody-facing object graph would violate the prompt.
Scalability potential: Low = fewer nodes and 2 constraint iterations; Middle = moderate node/iteration budget; High = tighter constraints and fuller spline upload; Ultra = same truth route with richer GPU Catmull-Rom presentation.
Hardware Impact: i3/MX350 avoids PhysX joint islands and CPU LineRenderer uploads; exact measured gain pending profiler, static estimate is removal of per-joint solver/broadphase cost and per-frame managed line point copy where legacy code exists.

## Decision 02 - Signal Route

Problem: The prompt asks for ForcePacket signals, while the current central physics apply path already exposes a `SignalBus<PhysicsEventPayload>` bridge and `TetherForcePacketDTO` unmanaged packet mirror. There is no dedicated public `ForcePacketDTO` SignalBus contract in the scanned core surface, and later audit proved Burst queue writers can hijack/default shared lanes.
Solution: Emit two unmanaged `TetherForcePacketDTO` rows plus matching Vault `HarpoonTensionPhysicsEventMirrorDTO` rows in the Burst force job. After the returned JobHandle is complete, the owner calls `PublishCompletedSignals` to convert mirrors into `PhysicsEventPayload` and push `PhysicsEventPayload`, `TetherTensionSignal`, and snap side-band through `SignalBus<T>.TryPush`.
Rejected Alternatives: Calling `PhysicsForceRouter.QueueForceAtPosition` from the solver would reintroduce managed physics coupling. Adding a new core signal type would touch massive core contracts without direct authorization. Passing `NativeQueue<T>.ParallelWriter` into the Burst job or constructing UnityEngine.Vector3 payloads in Burst was rejected after audit because default writers/local lane reconfiguration are unsafe and managed payload ABI does not belong inside the kernel.
Scalability potential: Signal count remains two packets per active tether regardless of Low/Middle/High/Ultra; quality scales solver iterations and presentation density, not authority route.
Hardware Impact: Fixed two-packet route is cache-predictable and avoids PhysX joint solve amplification. Mobile avoids cross-domain object lookups.

## Decision 03 - AUP And Layout Proof

Problem: Harpoon anchors can be tens of kilometers from origin, so any absolute float conversion before subtracting AUPs corrupts distance and tension.
Solution: Store anchors as `double3` in `TetherStateDTO`, subtract `AnchorB_AUP - AnchorA_AUP` in double precision, clamp local deltas, then cast to `float3` only for Verlet nodes, shader buffers, and force direction normalization.
Rejected Alternatives: Using scene `Transform.position`, `Vector3.Distance`, or absolute float anchors would reproduce 100km jitter and snap faults.
Scalability potential: Same AUP route across all tiers; quality never changes DTO layout or authority.
Hardware Impact: Double subtraction is O(1) per tether and cheaper than debugging force explosions; mobile cost is dominated by node constraints, not anchor subtraction.

## Decision 04 - Vault Buffer IDs

Problem: SHINOBU_328 needs persistent state, nodes, packets, telemetry, tuning, CSV profiles, and fault flags without private NativeArray ownership or collision with existing 132/143 tether lanes. Ledger re-check found the first draft range `71828..71840` collided with SHINOBU_264 lanes `71820..71831`.
Solution: Reserve local numeric `BufferID` range `72180..72193`, owned by `SystemID.Physics`, and keep every runtime persistent lane in `GlobalDataVault`; `72193` isolates cumulative snap stress outside the XML-mandated primary DTO padding.
Rejected Alternatives: Reusing `BufferID.Shinobu143TetherAup*` would mix ownership and corrupt existing telemetry. Keeping `71828..71840` after collision discovery would corrupt SHINOBU_264 async buoyancy payloads. Private manager fields or padding-overload state would violate H-PHI/Vault law.
Scalability potential: Low/Middle/High/Ultra use identical IDs and DTO layout; live owner calls may scale compact node stride, while the emergency mock route keeps its fixed seeded stride and scales iteration count plus visual richness continuously.
Hardware Impact: Fixed Vault lanes avoid runtime heap/native growth and make telemetry/dump ownership visible to diagnostics.

## Decision 05 - Deterministic Force Signal Bridge

Problem: The central apply system already owns Rigidbody mutation; the cable solver must not mutate bodies or search scene state.
Solution: `CalculateTetherForceJob` produces two `TetherForcePacketDTO` rows and two Vault `HarpoonTensionPhysicsEventMirrorDTO` rows. Snap/tension status and managed `PhysicsEventPayload` conversion happen only from the owner completion bridge after the solver handle is complete.
Rejected Alternatives: Direct `PhysicsForceRouter.QueueForceAtPosition` calls were rejected because they reintroduce managed Rigidbody coupling. A new core `ForcePacketDTO` signal type was rejected because it would require modifying global contracts during a parallel-agent batch. Burst-side SignalBus writers and Burst-side `PhysicsEventPayload` construction were rejected because they can open legacy MPSC writers/default writers outside owner phase and drag UnityEngine.Vector3 into deterministic solver output.
Scalability potential: Always two force packets per active tether; quality affects solver stiffness and GPU presentation only.
Hardware Impact: Mobile avoids PhysX joint solve amplification and hot scene/object lookups; force application remains one central route.

## Decision 06 - Cold Bootstrap Complete

Problem: Emergency mock buffers must exist deterministically for CI/editor stress, but gameplay solve phases must not block with hidden `.Complete()`.
Solution: `EnsureMockBuffers` performs one explicit `DispatcherJobFence.TryComplete` only during cold bootstrap/mock seeding after Vault allocation. Frame solve returns a `JobHandle` through `HarpoonTensionSchedule328` and does not complete.
Rejected Alternatives: Same-frame schedule/readback in gameplay would break phase discipline. Deferring mock seeding indefinitely would leave CI without a deterministic stress path.
Scalability potential: Cold only. Runtime scalability remains the quality-controlled iteration/node path.
Hardware Impact: No frame-loop stall added. Cold mock seed writes at most 5 tethers * 30 nodes.

## Decision 07 - Editor-Only Diagnostics

Problem: Designers need tuning, CSV reload, and x-ray inspection, but runtime cannot depend on editor UI, Roslyn, or scene drawing.
Solution: Put `OOP_Joint_Scanner`, `KinematicTetherTunerWindow328`, and `LiveVerletDebugGizmo328` under `Assets/_Project/Scripts/Editor` with `#if UNITY_EDITOR`. Editor tools may read `GlobalRegistry.DataVault` as diagnostics; runtime solver does not.
Rejected Alternatives: Runtime debug MonoBehaviours or gizmo GameObjects would add object authority and potential GC. Raw `grep` scanner was rejected because strings/comments create false positives.
Scalability potential: Tooling-only. On high-end devices visual overkill remains GPU Catmull-Rom/thickness shader work; editor gizmo is not shipped.
Hardware Impact: Runtime impact 0 us. Editor AST scan cost is offline/commanded only.

## Decision 08 - Compile Gate

Problem: Source is ready for compile, but user and project rules forbid `dotnet build` while compiler processes are active.
Solution: Sampled CPU and compiler processes. CPU average was 17%, but `VBCSCompiler.exe` PID 2036 was active. No build launched.
Rejected Alternatives: Launching `dotnet build` with an active compiler server would violate the explicit guard and risk shared-workstation contention.
Scalability potential: Not runtime-affecting.
Hardware Impact: Avoided a needless compile fight; static verification remains the current proof class.

## Decision 08B - Second Compile Gate

Problem: After polish patches, CPU/process guard had to be rechecked before any build. A foreign `dotnet.exe build Hecton8.Core.csproj --no-restore -v:minimal` plus `csc.exe` was active at CPU 100%. After waiting 45 seconds, CPU briefly read 46%, the WMI process query failed with Access denied, `Get-Process` showed no compiler rows, then CPU immediately read 100% again.
Solution: Do not launch another build. Record static proof and leave compile gate pending until CPU remains <=50% and no compiler process is visible.
Rejected Alternatives: Starting a build during CPU 100% violates the explicit project guard and would amplify the compile wall for other agents.
Scalability potential: Not runtime-affecting.
Hardware Impact: Prevents local IO/CPU contention on the shared workstation.

## Decision 09 - Ledger Collision Repair

Problem: Static ledger proof exposed an ABI collision: SHINOBU_328 originally documented `71828..71840`, while SHINOBU_264 already owns `71820..71831` for async buoyancy readback.
Solution: Move SHINOBU_328 runtime constants, self-audit text, ledger route, and logs to `72180..72193`. Focused search over `H8Memory.cs`, architecture ledger, and C# sources found no exact owner lanes before SHINOBU_328 reservation.
Rejected Alternatives: Adding enum entries to `H8Memory.cs` would touch a massive core header during a parallel batch. Leaving numeric IDs near `718xx` would create hidden cross-domain Vault aliasing.
Scalability potential: No gameplay or quality curve change; the route remains one owner, one lane family, one telemetry proof.
Hardware Impact: Prevents nondeterministic Vault alias writes that would be worse than any microsecond cost. Runtime cost remains 0 us; this is ABI hygiene.

## Decision 10 - Same-Domain Layout Proof Hardening

Problem: Focused post-polish scan found `VerletCableDTOs.cs` still used `Marshal.OffsetOf` in the existing tether/cable layout validator. That file is same-domain ABI surface and can contradict the SHINOBU_328 unsafe-layout mandate.
Solution: Replace the legacy helper with `UnsafeUtility.GetFieldOffset(FieldInfo)` while preserving the existing DTO layout checks and avoiding semantic changes to cable math.
Rejected Alternatives: Ignoring the hit because it predates SHINOBU_328 would leave a known weak proof in the exact cable/tether domain. Rewriting the whole cable DTO file would be unnecessary blast radius.
Scalability potential: No runtime quality effect; it strengthens cold validation before Low/Middle/High/Ultra routes consume the same DTOs.
Hardware Impact: Cold validation only. Runtime cost 0 us; ARM64 layout proof now uses the Unity unsafe metadata route consistently.

## Decision 11 - Compile Wall Containment

Problem: A guarded Core build was eventually allowed and failed before SHINOBU_328 could receive compile proof. Reported errors were in unrelated Gameplay files: missing `VRSomaticKinematicStateMirrorDTO`, missing `VRSomaticComfortDTO`, and missing `PlayerHandIkConfigFlags`.
Solution: Do not edit out-of-domain Gameplay kinematics/somatic files from this Physics/Tether mandate. Record SHINOBU_328 as `STATIC_SOURCE_PENDING_COMPILE_GATE` and keep verification factual until the external compile wall is cleared.
Rejected Alternatives: Patching unrelated Gameplay DTOs/flags would violate domain boundary and create ownership debt. Re-running build while CPU samples above 50% violates the local no-build guard.
Scalability potential: Not runtime-affecting; it preserves compile-wall discipline for parallel agents.
Hardware Impact: Avoids wasting shared CPU/IO on repeated builds that cannot pass the unrelated wall. Runtime impact 0 us.

## Decision 12 - Unity Import And Meta Hygiene

Problem: Focused `rg` over `*.csproj` and `Directory.Build.targets` found no `HarpoonTensionSolver328` or `OOP_Joint_Scanner`; the current generated project is stale and did not compile the new files. The new scripts also lacked `.meta` files, which would let Unity generate unstable GUIDs at import.
Solution: Add minimal `.meta` files beside both new scripts and document the generated-project gap. Do not mutate `Directory.Build.targets` or the generated `Hecton8.Core.csproj` from this domain pass.
Rejected Alternatives: Editing global build targets would expand blast radius and risk merge conflicts with other agents. Claiming compile proof from a stale generated project would be false.
Scalability potential: Not runtime-affecting; it improves Unity import determinism before Low/Middle/High/Ultra runtime routes are exercised.
Hardware Impact: Runtime impact 0 us. Stable GUIDs avoid import churn; avoiding global build-target edits protects iteration time.

## Decision 13 - Overload Gate Discipline

Problem: After report/meta restoration, even cheap static checks started timing out because the workstation was saturated. A 30-second CPU/process probe returned CPU 100% and active `csc.exe` PID 11916.
Solution: Stop compile attempts and avoid expensive repository-wide scans until the compiler process clears and CPU remains under the 50% project guard. Keep the latest cheap successful proofs: JSON parse, diff whitespace check, `.meta` existence, and generated-project stale scan.
Rejected Alternatives: Running `dotnet build` or broad `rg` under CPU 100% would violate the explicit build guard and interfere with other agents.
Scalability potential: Not runtime-affecting.
Hardware Impact: Prevents shared CPU/IO contention. Runtime impact 0 us.

## Decision 14 - Signal Writer Opt-In And State Race Closure

Problem: The public schedule path accepted default `NativeQueue<T>.ParallelWriter` values and the node integration job could mark shared tether state from parallel node lanes during non-finite recovery.
Solution: Remove queue publication from the Burst job completely; it now writes Vault mirrors only, and owner completion uses managed `SignalBus<T>.TryPush`. Remove parallel `States[tetherIndex]` mutation from `SimulateTetherNodesJob`; serialized constraint/force jobs own state flag writes.
Rejected Alternatives: Unconditional `Enqueue` risks invalid writer use by callers that only want packet mirrors. Per-node state flag writes create a same-cache-line race and nondeterministic fault attribution. Keeping opt-in job writers was rejected after runtime audit because default writer safety cannot be proven.
Scalability potential: Low/Middle/High/Ultra use the same truth route. Quality changes iteration count and presentation density only, not signal writer authority.
Hardware Impact: Removes false-sharing and invalid writer risk. Runtime ALU cost is unchanged except one byte branch already present in signal emission.

## Decision 15 - Cumulative Snap Stress ABI

Problem: One-frame tension spikes can snap a cable instantly, while the mandate asks for mathematically controlled tension failure rather than transient PhysX-style impulse behavior.
Solution: Keep the final four bytes of the 64-byte `TetherStateDTO` as `_pad0@60` and add a separate `TetherStressStateDTO[64B]` lane at BufferID `72193`; `SnapStressSeconds@56` stays in `HarpoonTensionTuningDTO`. `CalculateTetherForceJob` accumulates over-threshold stress in `TetherStressStateDTO.StressSeconds@0` using fixed `SimulationTickDelta`, decays it under threshold, guards NaN, and snaps only after the configured stress window.
Rejected Alternatives: Reusing primary DTO padding would silently break the XML ABI. Immediate snap was cheaper but less controllable and less designer-tunable. A managed damage/snap component would violate the zero-GC hot path.
Scalability potential: Low devices still run one scalar accumulator per tether; high/ultra can lower the designer-tuned stress window or raise visual response without changing authority route or DTO layout.
Hardware Impact: One add/subtract and finite guard per active tether. Cost is below measurement noise compared to constraint relaxation; benefit is deterministic snap behavior and fewer transient false failures.

## Decision 16 - Local Burst AUP Builder And Report Evidence Normalization

Problem: The Burst force job called `AbsoluteUniversePosition.FromAbsolutePosition`, a cross-type helper that is mathematically simple but widens job compile coupling. The editor scanner also wrote a nonstandard `ROSLYN_AST_TARGETED` evidence class into forensic output.
Solution: Add `BuildAbsoluteUniversePosition(double3)` locally with the same cell math and finite guards. Change scanner report output to `evidenceClass: STATIC_SOURCE` plus `scannerMode: ROSLYN_AST_TARGETED`, and write shared reports through a temp-file atomic replacement.
Rejected Alternatives: Keeping the external helper leaves unnecessary compile/link ambiguity in a hot job. Adding a full JSON DOM dependency for one report merge was rejected until Unity assembly refs are proven; temp-file replacement hardens current write behavior without package churn.
Scalability potential: Not gameplay quality affecting. The GPU Dear Lie and continuous solver quality path are unchanged.
Hardware Impact: Same O(1) AUP math in hot path; less cross-assembly friction. Editor report write cost is offline only.

## Decision 17 - No Rebuild Despite Temporary CPU Opening

Problem: A later cheap gate sampled CPU at 47% with no visible compiler process, but the generated `Hecton8.Core.csproj` still does not include `HarpoonTensionSolver328.cs` or `OOP_Joint_Scanner.cs`. The last guarded Core build failed on unrelated Gameplay symbols before any SHINOBU_328 compile proof.
Solution: Do not run another `dotnet build` from this domain pass. Keep proof factual: static source checks, JSON parse, diff whitespace check, generated-project stale scan, and documented external compile blockers.
Rejected Alternatives: Re-running Core build would consume CPU and reproduce known unrelated errors while still omitting the new files. Hand-editing generated project files would violate compile-wall discipline and Unity project generation ownership.
Scalability potential: Not runtime-affecting.
Hardware Impact: Avoids a useless compile pass. Runtime impact 0 us.

## Decision 18 - Bootstrap Sentinel Without ClearMemory

Problem: `EnsureMockBuffers` still used `NativeArrayOptions.ClearMemory` for the one-int bootstrap lane, contradicting the zero-init overhead promise even though it was a cold path.
Solution: Resolve an existing bootstrap lane first. If absent and allocation is allowed, allocate it with `UninitializedMemory` and immediately write `0` to the new sentinel before checking for the magic value. The mock initialization job remains the deterministic owner that writes the final magic.
Rejected Alternatives: Keeping `ClearMemory` would be easy but weakens the static proof. Reading an uninitialized bootstrap value as authoritative could randomly skip mock seeding if it matched the magic value.
Scalability potential: Not quality-affecting; all tiers share the same cold bootstrap semantics.
Hardware Impact: Removes even the tiny cold zero-fill while avoiding uninitialized authority. Runtime hot path remains 0 us.

## Decision 19 - Bootstrap Invariant And Snap Tuning Surface

Problem: A freshly allocated one-int bootstrap lane is safe after explicit zero write, but a stale or corrupted Vault lane could still contain the magic value while required payload lanes are missing or uninitialized. The editor tuner also exposed force constants but not the new cumulative snap window, leaving designers with a hidden C# constant.
Solution: `IsMockBootstrapValid` now requires every SHINOBU_328 Vault lane to resolve at required capacities and verifies the first state/stress/tuning/material rows for active flags, finite AUPs, finite stress scalars, positive rest length, positive tension constants, and nonzero tuning/profile flags before trusting `BootstrapMagic`. `KinematicTetherTunerWindow328` exposes `Snap Stress Seconds` beside tension, strength, gravity, quality, node, and iteration controls.
Rejected Alternatives: Trusting `bootstrap[0]` alone would make an uninitialized or stale scalar a truth owner. Adding a new checksum lane would widen the Vault ABI for a cold mock path. Leaving snap timing hidden in code would force C# recompiles for tuning.
Scalability potential: Low devices can increase snap window and reduce iterations without route changes; middle/high/ultra can tune tighter snap response while GPU presentation absorbs visual fidelity. Quality still does not change BufferIDs, DTO layout, or force authority.
Hardware Impact: Bootstrap invariant checks are cold only. The editor slider is editor-only. Hot runtime impact remains the existing one scalar stress accumulator per active tether.

## Decision 20 - Loop 9 Build Gate

Problem: After report synchronization, cheap CPU/compiler probes sampled CPU at 100 percent and then 97 percent. No `dotnet`, `csc`, or `VBCSCompiler` process was visible, but the project rule forbids build launch above 50 percent CPU. The generated `Hecton8.Core.csproj` still omits the new SHINOBU_328 files until Unity import/project regeneration.
Solution: Do not launch `dotnet build`. Keep the proof class at `STATIC_SOURCE_PENDING_COMPILE_GATE` and record the exact cheap checks: prompt extraction `22,873 chars / 20 tasks`, JSON/XML parse OK, focused forbidden scan OK, runtime braces `143/143`, editor lexical depth `0`, and diff whitespace check OK with LF/CRLF warnings only.
Rejected Alternatives: Running a build at CPU 100 percent violates the explicit workstation guard. Hand-editing generated project files to include the new scripts would mutate Unity-owned build artifacts and expand compile-wall risk.
Scalability potential: Not runtime-affecting; it preserves the same Low/Middle/High/Ultra solver route until a valid import/compile gate is available.
Hardware Impact: Avoids saturating a shared workstation and avoids a known stale-project compile pass. Runtime impact 0 us.

## Decision 21 - Emergency Mock Fixed Stride

Problem: `GenerateMockHarpoonTensionJob` seeds emergency mock buffers with fixed `MockNodesPerTether` stride, but `TryScheduleMockFromVault` previously resolved a quality-scaled nodes-per-tether value before scheduling. At low `GlobalQualityWeight`, that would reinterpret the same flat node/constraint buffers with a smaller stride and make tether ranges overlap.
Solution: Preserve fixed `MockNodesPerTether` in the emergency mock schedule route. Keep `ResolveNodesPerTether` available for live owners that provide compact buffers matching their chosen stride. Quality in the mock route still scales solver iterations and visual scalars, not memory layout.
Rejected Alternatives: Re-seeding mock buffers every frame with dynamic stride would erase cumulative snap stress and waste cold setup work. Adding active-node stride fields to every job is valid later but larger than the immediate alias fix. Leaving the old path would corrupt deterministic CI/editor mock proof.
Scalability potential: Low/Middle/High/Ultra retain continuous iteration and presentation pressure. Live owner data can still choose sparse or dense compact node layouts before calling `Schedule`; mock remains a stable fixed-layout test harness.
Hardware Impact: No added hot ALU or memory. It removes an aliasing fault that could poison force packets and telemetry under low quality.

## Decision 22 - Signal Publication Completion Bridge

Problem: Read-only runtime audit proved `EnsureSignalLanes()` was reconfiguring a shared `PhysicsEventPayload` lane already owned by Core, and `NativeQueue<T>.ParallelWriter` fields inside `CalculateTetherForceJob` could enqueue through invalid/default writers. That is a global lane corruption risk, not just style debt.
Solution: Remove all `NativeQueue<T>.ParallelWriter` fields and all SignalBus enqueues from `CalculateTetherForceJob`. The Burst job writes only Vault mirrors (`TetherForcePacketDTO` and `HarpoonTensionPhysicsEventMirrorDTO`). Add `PublishCompletedSignals`/`TryPublishCompletedSignalsFromVault`, which requires explicit owner completion proof and uses `SignalBus<T>.TryPush` after the solver handle has completed.
Rejected Alternatives: Keeping local `ConfigureCacheLineCritical` on `SignalBus<PhysicsEventPayload>` would mutate a core-owned lane. Using the legacy MPSC `ParallelWriter` remains possible elsewhere but is not acceptable for this solver. Direct force application is still rejected.
Scalability potential: Signal count remains continuous-capacity managed by Core SignalBus profiles; solver quality still scales iterations and GPU presentation, not authority route or lane ownership.
Hardware Impact: Removes queue CAS writer fields from Burst job and prevents shared snapshot capacity corruption. Managed completion bridge cost is two force events plus one tether signal per active tether after dispatcher completion.

## Decision 23 - Fault Dump Fence And Layout Debt

Problem: Audit found normal cable snaps were included in fault flags, `TryDumpTelemetryIfFault` could read while the telemetry job was still running, legacy cable iteration budget used a tier switch, and same-domain layout proof did not check offsets for force/spline/telemetry DTOs.
Solution: `TryDumpTelemetryIfFault` now requires explicit completion proof and masks to `HarpoonTensionFaultFlags328.DumpTriggerMask`. `RecordTetherTelemetryJob` keeps `Snapped` in telemetry rows but writes dump flags only for non-finite, constraint, layout, signal, or budget faults. `VerletCableDTOs` now validates force/spline/telemetry field offsets and resolves iteration budget from continuous `GlobalQualityWeight` with a byte compatibility adapter.
Rejected Alternatives: Hidden `.Complete()` inside dump was rejected. Treating normal snap as crash forensic trigger was rejected because snap is gameplay state, not an engine fault. Leaving the tier switch in same-domain cable helpers would contradict the quality continuum.
Scalability potential: Low/Middle/High/Ultra now share the same continuous iteration curve in the legacy helper surface. Dump and layout proof do not change gameplay quality.
Hardware Impact: No hot allocation or job blocking. Fault dumps become owner-phase only, preventing torn telemetry reads.

## Decision 24 - Completion Bridge Active Window

Problem: `CalculateTetherForceJob` only schedules active tether rows, but the managed completion bridge previously scanned the full `PhysicsEventPayload` Vault capacity. If a later frame had fewer active tethers, stale event rows beyond `activeTetherCount * 2` could be republished.
Solution: Bound `PublishCompletedSignals` to `eventLimit = min(physicsEvents.Length, activeTetherCount * 2)` and keep tether-status signal publication bounded by `activeTetherCount`. In the same pass, make `VerletCableLayout.ResolveIterationBudget(float, requested)` treat `requested` as a quality-scaled ceiling, not an override that bypasses `GlobalQualityWeight`.
Rejected Alternatives: Clearing the entire event Vault tail every frame would add memory bandwidth work. Publishing the full capacity was unsafe. Leaving requested iterations fixed would reintroduce a non-continuous helper path.
Scalability potential: Low/Middle/High/Ultra keep the same force truth route; signal work now scales with active tethers, and legacy iteration work scales smoothly inside the requested ceiling.
Hardware Impact: Adds one integer min on the completion bridge and avoids stale signal pushes. On low-end hardware it prevents wasted managed SignalBus traffic when tether counts drop.

## Decision 25 - Editor Unsafe Context

Problem: `KinematicTetherTunerWindow328` uses `NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks` and `UnsafeUtility.AsRef` to mutate Vault tuning rows, but the class itself was a normal `EditorWindow`. That can fail C# compilation because `void*` usage requires an unsafe context even in editor-only code.
Solution: Mark `KinematicTetherTunerWindow328` as `unsafe sealed`. The unsafe surface stays inside `#if UNITY_EDITOR`; runtime solver code and shipped jobs are unchanged.
Rejected Alternatives: Replacing `UnsafeUtility.AsRef` with `tuning[0]` copy/write would weaken the stated editor facade proof. Wrapping only individual lines in unsafe blocks would work but adds noise with no ownership benefit.
Scalability potential: Editor-only. Low/Middle/High/Ultra runtime paths are unaffected.
Hardware Impact: 0 us runtime. It removes a compile-shape fault without adding gameplay cost.

## Decision 26 - Public Schedule Count Fence

Problem: The public `Schedule(...)` entry point accepted owner-provided active tether, node, and constraint counts and passed them toward `IJobParallelFor.Schedule` after only upper-bound `math.min` checks. A negative count from an external owner could become a negative job length before Burst math ever runs.
Solution: Clamp `activeTetherCount`, `activeNodeCount`, and `activeConstraintCount` to at least zero before applying buffer-length ceilings. Remove the stale `PhysicsEventLaneHash` constant because the solver no longer configures or owns the `PhysicsEventPayload` signal lane.
Rejected Alternatives: Trusting all callers would make the solver fragile at the cross-domain boundary. Clearing output buffers for invalid negative counts was rejected because the owner did not grant a valid active range. Keeping the stale lane hash would imply local lane authority that was deliberately removed.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; invalid input collapses to a zero-work deterministic schedule, while valid input still scales iterations and presentation through `GlobalQualityWeight`.
Hardware Impact: Three integer clamps in the cold schedule path; prevents a scheduler exception and invalid pointer window. No extra per-node or per-constraint ALU.

## Decision 27 - TetherManager SHINOBU_328 Live Hook

Problem: The SHINOBU_328 solver existed as a Burst/Vault lane, but `TetherManager` still only bootstrapped older SHINOBU_132/143 mock schedulers. That left the new tension solver as static proof rather than a live owner-phase route.
Solution: Add `TetherManager` bootstrap/schedule/finalize methods for SHINOBU_328. The manager calls `EnsureMockBuffers`, schedules `TryScheduleMockFromVault`, registers the returned handle with `H8Memory`, finalizes only with `DispatcherJobFence.TryFinalizeCompleted`, then publishes `PhysicsEventPayload`, `TetherTensionSignal`, and snap signals through the managed owner completion bridge. `TryHasMockBuffers` now verifies every required Vault lane and capacity so an allocation-lock bootstrap miss is retried.
Rejected Alternatives: Leaving the solver unreferenced would be dead architecture. Publishing directly from Burst or resurrecting `NativeQueue<T>.ParallelWriter` was rejected because it mutates shared signal lanes outside owner phase. Rewriting the full legacy `TetherInstance` object graph in one pass was rejected as a domain-wide blast radius during a 40-agent batch.
Scalability potential: Low keeps 2-iteration truth and fixed mock stride; middle/high/ultra increase relaxation and GPU spline richness through the existing continuous `GlobalQualityWeight` route. The manager hook does not change DTO layout, BufferIDs, or authority route.
Hardware Impact: Adds one batched job chain and one completion bridge on the manager path. It avoids Unity Joint/LineRenderer authority and gives MX350 a GPU-owned cable visual route without per-frame CPU rope mesh expansion. Measured proof remains pending Unity import/console/profiler.

## Decision 28 - Proof Artifact Synchronization After Live Hook

Problem: Disk state showed the checklist and rationale carried the live `TetherManager` bridge, but `BuildSelfAuditXml`, the XML snapshot, the shared physics JSON report, and the binary ledger still described a generic static owner-completion dependency graph.
Solution: Patch all proof artifacts to name `TetherManager.ScheduleShinobu328TensionMock`, `H8Memory.RegisterActiveJob`, `DispatcherJobFence.TryFinalizeCompleted`, teardown-only forced completion, and the legacy `TetherInstance` debt fence. Append the missing Loop 15 report to `Docs/AgentLogs/LOG_SHINOBU_328.md`.
Rejected Alternatives: Leaving stale reports would make the CTO-visible artifact disagree with the code. Regenerating reports from Unity editor tooling is blocked until import/compile gate clears. Rewriting `TetherInstance` debt in this pass remains rejected because it is a broad object-graph migration, not a literal Joint/LineRenderer eradication fix.
Scalability potential: No route or layout change. The proof now correctly states that quality changes iteration/presentation only while the manager keeps one authority route across cheap, middle, high, and ultra devices.
Hardware Impact: 0 us runtime. Prevents false operational decisions from stale forensic evidence.

## Decision 29 - TetherManager Compile-Shape Hygiene

Problem: The new `ScheduleShinobu328TensionMock` call used an `out _` discard for `Vector3` and passed `null` as the dump reason. Both are legal on modern C# surfaces, but Unity language settings and nullable strictness are not proven until import/regeneration.
Solution: Replace the discard with an explicit local `Vector3 cameraPosition` and pass `string.Empty` to `TryDumpTelemetryIfFault`.
Rejected Alternatives: Waiting for the compiler to reject the shape would waste a gated build attempt. Adding nullable annotations or broader language-version changes would touch project-wide configuration outside this domain.
Scalability potential: No runtime quality effect. The fixed bridge remains continuous-quality and route-stable.
Hardware Impact: 0 us runtime; this is compile-risk removal only.

## Decision 30 - Blittable Physics Event Mirror

Problem: Runtime audit found `CalculateTetherForceJob` still wrote `PhysicsEventPayload`, which contains UnityEngine `Vector3` fields. Even if the current project accepts that struct elsewhere, the deterministic Burst force kernel should not construct managed engine-facing payloads while solving cable tension.
Solution: Add and validate `HarpoonTensionPhysicsEventMirrorDTO=80` with explicit offsets (`float3` runtime position, direction, force, scalar payload, ids, ushort event/body slots). Change all Vault `72185` views and force-job fields to `NativeArray<HarpoonTensionPhysicsEventMirrorDTO>`. Convert mirrors into `PhysicsEventPayload` only in `BuildPhysicsEventPayload`, called by owner-phase `PublishCompletedSignals` after completion proof.
Rejected Alternatives: Keeping `PhysicsEventPayload` in Burst would leave UnityEngine payload shape inside the solver. Adding a new global ForcePacket SignalBus contract would touch core contracts during a parallel-agent batch. Clearing/publishing full event capacity was already rejected because it can republish stale tail rows.
Scalability potential: Low/Middle/High/Ultra signal count and authority route remain unchanged: two mirror rows per active tether plus continuous iteration/GPU presentation scaling. Quality still does not change DTO layout, BufferIDs, save identity, or force ownership.
Hardware Impact: Removes engine-facing payload construction from the force kernel and keeps output contiguous/blittable. Owner-phase conversion cost is two `Vector3` payloads per active tether after the dispatcher fence, not inside Burst solver ALU.

## Decision 31 - TetherManager Black-Box Cold Reset

Problem: Focused post-patch scan found existing `TetherManager` telemetry acquisition still requested `NativeArrayOptions.ClearMemory`. The SHINOBU_328 manager bridge now depends on the same owner black-box path, so leaving broad zero-fill in that touched surface weakens the zero-init proof.
Solution: Open the telemetry ring and head with `NativeArrayOptions.UninitializedMemory`, then explicitly reset the ring and head only when the Vault handle is new or its generation changes. Normal frame writes keep ring mutation bounded to the current row.
Rejected Alternatives: Keeping `ClearMemory` is simple but contradicts the local zero-init mandate. Removing the reset entirely would make first dump rows read undefined telemetry before 300 samples are written.
Scalability potential: Low/Middle/High/Ultra runtime paths are unchanged; telemetry allocation/generation changes are cold events and do not alter cable truth or presentation quality.
Hardware Impact: Removes allocation-time bulk clear from the normal open/acquire call. The explicit reset loop runs only on new/generation-changed telemetry buffers, not per frame.

## Decision 32 - Primary DTO ABI Fence

Problem: The original XML assignment explicitly defines `TetherStateDTO[60..63]` as padding. The cumulative snap patch had used those four bytes for `StressSeconds`, which preserved size but violated the literal ABI and could desync blind rollback snapshots or external payload readers that expect padding there.
Solution: Restore `TetherStateDTO._pad0@60` and move cumulative snap state to `TetherStressStateDTO=64` on Vault lane `72193`. Runtime acquisition, mock bootstrap validation, public schedule, `CalculateTetherForceJob`, and owner-phase signal publication now pass/require `StressStates` with `[NoAlias]` and capacity checks. The force kernel writes one stress row per tether and `PublishCompletedSignals` reads that row for snap side-band emission.
Rejected Alternatives: Keeping stress in primary padding was too brittle. Recomputing stress from telemetry loses frame-exact snap history. A managed dictionary or component would violate zero-GC and one-route authority.
Scalability potential: Low devices still pay one scalar update per active tether; high/ultra can increase visual reaction from the same stress scalar without changing DTO layout, BufferIDs, or authority route.
Hardware Impact: Adds one 64-byte Vault row per tether and avoids primary DTO ABI drift. Hot ALU is unchanged except the same scalar stress update now targets a separate cache-line row.
