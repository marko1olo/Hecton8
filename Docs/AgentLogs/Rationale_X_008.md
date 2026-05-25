# Rationale_X_008 - COMBAT_DAMAGE_AND_ARMOR_LUT_OPTIMIZER

Date: 2026-05-23
Evidence Class: STATIC_SOURCE until compile/runtime artifacts exist.

## Initial Boundary

Problem: X_008 must alter combat damage/armor routing without inventing cross-domain dependencies in a 20+ agent workspace.
Solution: Phase 0 is source archaeology first. New global/Vault/Signal surfaces are blocked until owner, phase, capacity, overflow, telemetry, and proof fields are known.
Rejected Alternatives: Direct implementation before call graph scan; direct dependencies on absent combat classes; `GlobalRegistry` hot polling.
Scalability potential: Low uses flat LUT and bounded signal queues; Middle keeps full gameplay truth at normal cadence; High adds richer telemetry; Ultra spends saved budget on VISUAL_SYNC-only impact presentation.
Hardware Impact: Expected low-end benefit is removal of trig and managed callbacks from pellet fanout path; numeric gain is PENDING VERIFICATION until profiler/static harness exists.

## Decision 001 - Mandate Set

Problem: Combat LUT work touches damage truth, DTO layout, jobs, signals, AUP hit positions, and crash telemetry.
Solution: Read 8 mandate files: damage/VFX, ARM64 DTO, zero-GC, native jobs, signal lanes, phases, AUP, telemetry.
Rejected Alternatives: Reading the entire `.agents-skills` registry; too much context noise and not task-specific.
Scalability potential: Mandate set covers Low/Middle/High/Ultra route design without binary quality switches.
Hardware Impact: Keeps design oriented around flat memory, bounded queues, and MX350/i3 hot-path budgets.

## Decision 002 - Verification Language

Problem: The prompt demands sub-10 microsecond claims, but no benchmark has run.
Solution: Treat every performance number as target until a script, compile, profiler, or runtime artifact exists.
Rejected Alternatives: Writing claimed microsecond savings into logs before measurement.
Scalability potential: Prevents false acceptance on weak hardware and leaves room for Ultra presentation upgrades after proof.
Hardware Impact: No direct gain yet; prevents regression masked by fake numbers.

## Decision 003 - Source Scanner Evidence

Problem: Task 01 requires a source-wide damage/armor/ballistics/fauna routing inventory, but the first Roslyn pass failed every file due host assembly binding, producing unusable parse-failure noise.
Solution: Replaced the failed artifact with `Docs/Reports/COMBAT_DAMAGE_PIPELINE_TARGET_LIST_X_008.json` schema v2: `rg` token/call-site scan, exact target file list, route findings, and explicit Roslyn status `BLOCKED_HOST_BINDING_System.Runtime.CompilerServices.Unsafe_6.0.0_and_4.0.4.1`.
Rejected Alternatives: Claiming AST success; using memory-only inspection; blocking on a dotnet build while CPU was above the project guard.
Scalability potential: Low/Middle devices benefit only after code changes; the scan prevents optimizing the wrong path. High/Ultra retain current Impact/Deflect presentation lanes for richer visuals after simulation truth is flat.
Hardware Impact: 0 us runtime gain. Evidence generated, not runtime code.

## Decision 004 - LUT Semantics Gap

Problem: Existing SHINOBU_318 `ArmorProfileDTO` is 64B and contains a 48-byte LUT, but current runtime interprets it as spatial `row * 8 + col` from local hit UV. X_008 requires projectile/material row times six angle steps.
Solution: Preserve the 64B DTO footprint and reinterpret the 48 bytes as `materialRow(0..7) * 6 + angleStep(0..5)` during the upcoming code pass. Surface normal and local point stay only as cheap inputs for dot quantization and feedback payloads.
Rejected Alternatives: Adding a managed material map; widening the DTO; doing real-time thickness/deformation; keeping spatial UV LUT and pretending it satisfies X_008.
Scalability potential: Low uses one dot, clamp, floor, and byte load. Middle keeps the same truth path with normal feedback. High can spend quality on richer ImpactSignal presentation. Ultra can add presentation-only decals/audio without altering combat truth.
Hardware Impact: Expected low-end gain comes from removing spatial hit-grid branch behavior and any future trig/thickness temptation from pellet fanout. Numeric gain pending profiler proof.

## Decision 005 - Feedback Route Boundary

Problem: Armor impacts need visual feedback without simulation jobs owning particles, audio, or managed fauna wound presentation.
Solution: Keep job outputs to `DeflectSignal`, `ImpactSignal`, `CombatDamageResultFlags`, and owner-phase `EntityDeathSignal`. `CreatureDamageManager.RegisterWoundWS` remains a late-frame presentation bridge, not a Burst dependency.
Rejected Alternatives: Calling particle/audio/renderer APIs from damage evaluation; publishing through `HectonEventBus`; hot polling `GlobalRegistry` from accessors.
Scalability potential: Low can shed ImpactSignal detail by quality weight. Middle/High/Ultra increase presentation fidelity through existing VISUAL_SYNC consumers while health truth remains CAS-owned.
Hardware Impact: 0 us current code gain. Prevents managed allocations on pellet storms once the LUT path is implemented.

## Decision 006 - Route Card

Problem: The existing SHINOBU_318 route card says 8x6 LUT but does not explicitly forbid the spatial row/column interpretation now proven in code.
Solution: Added `Docs/ARCHITECTURE/X_008_COMBAT_ARMOR_LUT_ROUTE_CARD.md` with exact route, DTO offsets, `materialRow * 6 + angleStep` contract, and forbidden paths.
Rejected Alternatives: Editing source before documenting the semantic correction; modifying unrelated SHINOBU_318 report history.
Scalability potential: Low/Middle/High/Ultra all share one truth route while presentation fidelity remains quality-weighted.
Hardware Impact: 0 us runtime gain. Prevents divergent agent implementations that would reintroduce expensive or managed damage paths.

## Decision 007 - LUT Materialization Edit

Problem: Task 04 requires an unmanaged 8x6 table without inventing a second authority or local singleton.
Solution: Added explicit 64B `ShinobuArmorPenetrationTable` and kept `ArmorProfileDTO` at 64B with its 48-byte table at offset 16. The existing `GlobalDataVault` `TargetArmorProfiles` buffer remains the storage owner, so there is no new hot dependency and no managed `byte[]`.
Rejected Alternatives: Adding a standalone persistent `NativeArray` singleton; widening target profiles; moving combat DTOs into a new assembly during a dirty multi-agent session.
Scalability potential: Low/Middle/High/Ultra share the same compact table. Device quality changes can alter table authoring or presentation detail, not DTO layout or authority.
Hardware Impact: Runtime footprint unchanged per target profile. Numeric frame gain pending compile/profiler.

## Decision 008 - Branchless Lookup Scope

Problem: X_008 demands `materialId * 6 + angleStep` lookup and a future `IJobParallelFor`, while current production drain is `ProcessDamageQueueJob : IJob` because it mutates health, shields, status masks, and minor-damage accumulators in one owner lane.
Solution: Corrected the production hot lookup semantics inside the current Burst job first: `materialRow = ReadDamageClass(packedMeta) & 7`, `angleStep = floor((1 - abs(dot(direction, normal))) * 6)`, `lutIndex = materialRow * 6 + angleStep`. This removes the spatial UV lookup and keeps CAS health ownership unchanged.
Rejected Alternatives: Pretending the current serial queue drain is the requested `EvaluateArmorPenetrationJob : IJobParallelFor`; splitting health/status writes without a verified transaction design.
Scalability potential: Low uses the same cheap dot/byte path. Middle keeps status and shields coherent. High/Ultra can increase presentation feedback without adding combat-truth branches.
Hardware Impact: Expected low-end benefit is reduced lookup math and no spatial grid branch path in armor evaluation; exact microseconds pending compile/profiler. Full parallel evaluation remains pending.

## Decision 009 - Trigonometric Debt Verification

Problem: User challenged whether hidden `math.acos` or `math.asin` survived under ricochet, penetration vector correction, or angle-of-attack code.
Solution: Ran scoped and project-wide static scans. `Gameplay/Combat` has zero `math/Mathf/System.Math` calls to `acos`, `asin`, `atan`, `atan2`, `sin`, `cos`, or `tan`. `Docs/Reports/COMBAT_OPTIMIZATION_REPORT_X_008.json` records `combatForbiddenTrigCount=0`. Project-wide `acos/asin` remains in IK, editor bake, celestial, and player movement code outside X_008 armor responsibility.
Rejected Alternatives: Hiding non-combat `acos/asin` findings; claiming the entire repository is trig-free; treating `quaternion.AxisAngle` in `BallisticsRuntime` mock primitive rotation as armor penetration.
Scalability potential: Low/Middle/High/Ultra combat truth uses one dot-product quantizer and flat byte lookup. Non-combat presentation or IK trig must be audited by their owners if it becomes hot.
Hardware Impact: Combat armor route has no inverse trig latency in the static source. Numeric gain remains pending Burst compile/profiler proof.

## Decision 010 - Source-Level Branch Cleanup

Problem: The previous LUT proof removed trig but left source-level branch syntax in material selection and AABB face normal selection before the LUT index.
Solution: Replaced material selection, finite sanitation, smooth weight sanitation, and AABB major-axis normal choice with `math.select` mask-style expressions and bitwise boolean composition. The LUT core is now `dot -> abs -> saturate -> floor/clamp -> materialRow * 6 + angleStep -> byte load`.
Rejected Alternatives: Claiming nested ternaries are branchless without Burst disassembly; replacing the AABB normal with an expensive geometric normal solver; reintroducing real angle math.
Scalability potential: Low devices get deterministic cheap hitbox normal quantization. Higher tiers can spend saved budget on VISUAL_SYNC sparks, blood, decals, and audio without changing combat truth.
Hardware Impact: Removes residual source-level data-dependent branch syntax from the lookup preparation. Exact branch lowering and microseconds require Burst disassembly/profiler.

## Decision 011 - CAS Stability Boundary

Problem: The user requested proof that atomic health subtraction survives a 100-pellet same-frame hit storm without lost HP.
Solution: Formalized the current CAS invariant: each successful `Interlocked.CompareExchange` is linearizable, no two writers can commit from the same observed float bits, non-finite health is rejected, damage is clamped non-negative, and health is monotonic non-increasing. Recorded the hard caveat from the old code: the helper retried 8 times, so it was not a mathematical guarantee for 100 concurrent writers to the same health slot in a future parallel apply phase. Current production drain is serial `IJob`, so same-slot CAS contention is absent there. Superseded by Decision 012.
Rejected Alternatives: Pretending bounded retry equals wait-free no-loss behavior under arbitrary contention; switching to managed locks or `TakeDamage`; publishing a ready claim without a transaction aggregation design.
Scalability potential: Low/Middle use serial owner apply or aggregate-before-apply to avoid CAS storms. High/Ultra may parallelize evaluation, but health mutation must remain owner-applied or per-target reduced before CAS.
Hardware Impact: Current serial path avoids cache-line CAS contention on weak CPUs. Future parallel path needs per-target aggregation to avoid burning cycles on failed CAS under pellet storms.

## Decision 012 - CAS Retry Closure Under Queue Cap

Problem: Decision 011 correctly rejected the old 8-retry CAS as proof for 100 simultaneous same-target pellet writers, but the user requested code closure rather than a caveat.
Solution: Added `AtomicHealthCasRetryLimit = MaxQueuedSignals` and changed `TryAtomicSubtractHealth` to retry to that ceiling. Proof: queue admission caps in-flight damage writers at `MaxQueuedSignals = 1024`; every failed `CompareExchange` means some other writer successfully changed the observed health bits; therefore a writer among `K` same-slot writers can lose at most `K-1` races before observing the latest value and committing. For 100 pellets, `100 <= 1024`, so bounded retry no longer drops HP due to retry exhaustion.
Rejected Alternatives: Leaving the 8-retry helper; switching to managed locks; pretending per-target aggregation exists; unbounded spin without a project-owned completion window.
Scalability potential: Low/Middle production serial apply remains cheapest. High/Ultra can still split parallel evaluation later, but apply should prefer per-target aggregation to reduce cache-line CAS storms even though the bounded retry proof now covers correctness under the queue cap.
Hardware Impact: Correctness improved for future parallel contention. Worst-case CAS traffic can still be high under same-slot pellet storms, so profiler proof is still required before claiming a microsecond win on weak CPUs.

## Decision 013 - Project-Wide Sweep Boundary

Problem: User escalated from the closed armor route to "check/improve the whole project", but X_008 cannot safely rewrite animation, world, audio, UI, or fauna-owner code without proof that the code is part of combat truth.
Solution: Expanded `Tools/OOP_Hitbox_Scanner.py` to emit `Docs/Reports/PROJECT_WIDE_HOTPATH_SWEEP_X_008.json`. The sweep inventories project-wide trig, runtime `acos/asin`, angle APIs, damage bypass candidates, managed event candidates, and direct Rigidbody tokens. Edits remain limited to Echelon 5 combat route unless a non-combat file directly bypasses `CombatDamageRuntime` and can be migrated without behavior loss.
Rejected Alternatives: Blindly removing every `sin/cos` in audio/VFX/world/editor code; editing Echelon 3 fauna immediate predation logic from X_008 without an owner route card; treating token matches as automatic bugs.
Scalability potential: Low devices benefit only after owner-specific migrations. The sweep gives each owner a bounded target list while combat truth remains the proven LUT/CAS path.
Hardware Impact: 0 us runtime gain from the sweep itself. Static findings: `298` project trig tokens, `7` runtime `acos/asin` hits, `72` damage-bypass candidates, `829` Rigidbody direct tokens. Combat armor remains `PASS`.

## Decision 014 - Parallel Evaluator And Torture Harness Source Closure

Problem: The armor route had a corrected serial production lookup, but the task explicitly required a named Burst `IJobParallelFor` evaluator and a 10,000-impact proof harness. The previous report was therefore source-incomplete even though the combat trig scan passed.
Solution: Added explicit 128B `ArmorPenetrationResolvedHitDTO`, `EvaluateArmorPenetrationJob : IJobParallelFor`, `CombatDamageTortureJob : IJobParallelFor`, and `RunArmorPenetrationTortureProof(10000, out telemetry)` for editor/development builds. The evaluator consumes pre-resolved target slots to keep its core path flat: request/detail/AUP/slot -> dot LUT sample -> resolved damage DTO. Health mutation remains outside the evaluator and stays in the CAS apply path.
Rejected Alternatives: Expanding production `MaxQueuedSignals` to 10,000; combining health CAS into the parallel evaluator; claiming `ProcessDamageQueueJob : IJob` was enough; blindly rewriting non-combat `acos/asin` in IK/celestial/player systems without owner proof.
Scalability potential: Low tier keeps serial owner apply and flat LUT truth. Middle can run the parallel evaluator as a batch pre-pass if profiler proves win. High/Ultra can spend saved combat truth budget on deferred VISUAL_SYNC sparks, blood, decals, and richer audio without changing DTO layout or authority route.
Hardware Impact: Source now supports a real 10k cold QA torture path and a parallel O(1) evaluator. Runtime microseconds remain PENDING until Unity import, Burst disassembly, and profiler execution are available; latest build guard blocked compile because CPU was 100% with active compiler processes.

## Decision 015 - Project Runtime Inverse-Trig Cleanup

Problem: Project-wide runtime inverse trig remained outside `Gameplay/Combat`: two IK jobs used `acos` only to feed `sin/cos`, while player/celestial runtime elevation code used `asin` for presentation angles. The armor route was already clean, but the user explicitly challenged hidden angle-of-attack debt beyond the closed route.
Solution: Removed the runtime inverse trig sites with algebraic or polynomial replacements. `ProceduralBiteIkJobs` now uses `sin(acos(x)) = sqrt(1 - x*x)` via rsqrt. `LadderClimbIkJobs` now uses `cos(acos(x)) = x` and `sin(acos(x)) = sqrt(1 - x*x)`. `HectonPlayerMovement` and `HectonCelestialEngine` now use a bounded `FastAsinDegrees` approximation for presentation pitch/elevation values. Scanner proof now reports `projectAcosAsinRuntimeCount = 0`; remaining `acos` sites are Editor/Baker-only.
Rejected Alternatives: Blindly rewriting editor bake math; removing legitimate `Quaternion.AngleAxis` rotations; pretending `math.asint` hash/bit-pack calls are trig; keeping exact `asin/acos` in runtime presentation code after a safe approximation path existed.
Scalability potential: Low tier avoids scalar inverse trig in IK/player/celestial runtime. Middle/High keep stable visual behavior with negligible approximation error for presentation values. Ultra can spend saved budget on richer VISUAL_SYNC and animation polish instead of exact inverse trig in every tick.
Hardware Impact: Runtime `acos/asin` static count changed from 7 to 0 in the project sweep. Exact microseconds are PENDING until Unity import/profiler; compile remains blocked by the build guard while CPU is above 50% or compiler processes are active.

## Decision 016 - Runtime Angle API Pruning

Problem: After exact runtime `acos/asin` was removed, two Unity angle helpers still computed angles where the code only needed a clamped pitch value or a same-pose threshold. `Vector3.SignedAngle` in hostile flora aiming and `Quaternion.Angle` in HUD pose validation are hidden inverse-trig style APIs.
Solution: Replaced hostile flora pitch extraction with rsqrt-normalized vertical projection plus `FastAsinDegrees`, preserving the old `SignedAngle` sign route through `dot(right, cross(flat, direction))`. Replaced HUD rotation threshold with quaternion dot and `sin^2(theta/2)` comparison against the authored `0.01` degree tolerance.
Rejected Alternatives: Rewriting legitimate `Quaternion.AngleAxis` calls that construct rotations; changing flora yaw/spread behavior; pretending all angle APIs are wrong; editing editor-only angle checks.
Scalability potential: Low tier avoids two more runtime angle helper calls. Middle/High/Ultra keep the same visual intent while saving exact angle extraction for places where rotation construction is genuinely required.
Hardware Impact: Project runtime angle API static count changed from `36` to `34`; runtime `Vector3.SignedAngle/Quaternion.Angle` exact scan now leaves only editor-only hits. Exact microseconds remain PENDING until profiler.

## Decision 017 - Combat Mock AxisAngle Removal

Problem: The combat scanner still reported one angle API in `Gameplay/Combat`: `quaternion.AxisAngle` inside `GenerateMockBallisticsJob`. It was not penetration math, but it kept the combat proof from being angle-API clean.
Solution: Replaced the mock AABB primitive rotation with `quaternion.identity`. The mock primitive grid still has deterministic positions, extents, material hashes, and primitive hashes; no gameplay trajectory, damage, or armor truth depends on yaw variety there.
Rejected Alternatives: Leaving the exception in the report; adding a sin/cos LUT for a mock-only rotation; touching production trajectory rotation logic where none was required.
Scalability potential: Low tier avoids useless mock rotation math during QA/dev stress generation. Middle/High/Ultra retain deterministic mock coverage; visual variety can be reintroduced presentation-side if profiling proves it matters.
Hardware Impact: Combat `AxisAngle/AngleAxis/acos/asin/sin/cos` static scan is now empty, `combatAngleApiCount` changed from `1` to `0`, and project `runtimeAngleApiCount` is now `33`. Measured microseconds remain PENDING.

## Decision 018 - Cursor-Ordered Combat Blackbox Dump

Problem: Task 09 requires a useful 300-frame forensic ring, but the combat and armor dump paths wrote the physical NativeArray order. After ring wrap, that order is not chronological and weakens postmortem analysis. `TryDumpCombatTelemetry` also set its dump latch before proving the file write succeeded, so a transient I/O failure could suppress later evidence.
Solution: Changed `TryDumpCombatTelemetry` to write `Docs/AgentLogs/Dump_SHINOBU_318_Combat.bin`, compute the oldest slot from the monotonic cursor, and serialize rows oldest-to-newest. Changed `DumpArmorTelemetryIfNeeded` to serialize `ArmorPenetrationTelemetryEntry[300]` oldest-to-newest from `_armorTelemetryCursor`. Moved both dump latches after successful writes in editor/development paths; release still records the dump request without managed disk I/O.
Rejected Alternatives: Leaving raw physical array order; allocating a managed sorted copy of the ring; marking the dump as completed before `FileStream`/`BinaryWriter` finishes; doing synchronous disk I/O in normal hot path.
Scalability potential: Low/Middle keep zero extra hot-path work because only diagnostic dump serialization changed. High/Ultra get better forensic quality without changing gameplay truth or DTO layout.
Hardware Impact: Runtime frame cost unchanged until a fault/NaN/over-budget diagnostic path fires. `OOP_Hitbox_Scanner.py` now proves `combatDumpCursorOrdered=true`, `armorDumpCursorOrdered=true`, and both dump latches occur after write evidence. Measured runtime proof remains PENDING.

## Decision 019 - Directional Deflect Impact Route Closure

Problem: The armor LUT deflect path published both `DeflectSignal` and `ImpactSignal`, but the directional front-deflection path in `ProcessDamageQueueJob` only published `DeflectSignal`. That left a presentation gap: some armor ricochets could reach the deflect lane without the generic late-frame impact consumers for camera juice, soundscape, and decals.
Solution: Added `EmitArmorImpactFeedback` as the shared native helper for armor impact presentation payloads. LUT deflection and directional front deflection now both enqueue `ImpactSignal` through the existing `SignalBus<ImpactSignal>.ParallelWriter` when `GlobalQualityWeight` permits and the AUP is finite. The simulation job still does not spawn particles, audio, renderer effects, or wound callbacks.
Rejected Alternatives: Directly invoking VFX/audio from `ProcessDamageQueueJob`; adding a new signal type during a dirty multi-agent session; leaving directional deflect as a deflect-only exception; forcing impact feedback when `GlobalQualityWeight` disables it.
Scalability potential: Low tier can suppress the optional `ImpactSignal` by continuous quality weight while retaining damage truth. Middle/High/Ultra get sparks/audio/decals from existing late-frame consumers without altering health, DTO layout, or combat authority.
Hardware Impact: One additional bounded signal enqueue only on directional front-deflect events and only after the existing deflect branch fires. No new managed allocation or hot polling. Measured frame cost remains PENDING until Unity compile/profiler; static scanner now proves `jobSideManagedPresentationTokenCount=0`.

## Decision 020 - Managed Combat Listener Route Removal

Problem: `CombatDamageRuntime` still exposed a managed listener route through `ICombatDamageEventListener` and an unused `ICombatDamageFeedbackReceiver`. This was outside the Burst job, but it weakened the "deferred signal only" presentation claim and the previous scanner did not detect it.
Solution: Removed the listener interface, listener array, register/unregister methods, and dispatch loop. Removed `CameraJuiceSystem` listener registration because the Burst camera-juice path already consumes `SignalBus<CombatDamageSignal>.GetFrameSnapshotArray()`. Removed `SubmarineAutoLevelBallastController` listener registration because its `OnCombatDamageResolved` logic duplicated `IDamageReceiver.ReceiveDamage`.
Rejected Alternatives: Leaving the managed listener route and hiding it in the scanner; deleting `IDamageReceiver.ReceiveDamage`, which is still the registered target owner handoff after job completion; introducing a new event bus route.
Scalability potential: Low/Middle avoid an extra managed listener loop and duplicate callback work. High/Ultra keep camera/impact presentation through existing SignalBus snapshots and owner receiver handoff without changing combat truth.
Hardware Impact: Removes one cold listener array allocation and one result-count-by-listener-count managed dispatch loop. Exact runtime gain is PENDING; compile/profiler remained blocked by CPU/compiler guard.

## Decision 021 - Visual Small-Angle Rotation Cheat

Problem: Project-wide runtime angle inventory still contained Burst animation `quaternion.AxisAngle` calls for small visual rotations: fauna trauma wobble, fauna jaw opening, and kinetic character damage flinch. These are not combat truth, but they are runtime visual jobs and safe to simplify.
Solution: Added normalized small-angle quaternion helpers in `ProceduralBoneMath` and `KineticCharacterMath`: vector part `axis * radians * 0.5`, scalar part `1 - half^2 * 0.5`, then normalize. Replaced the three `AxisAngle` calls. This avoids exact sin/cos inside those visual rotations while preserving deterministic, bounded visual motion.
Rejected Alternatives: Rewriting gameplay rotations or construction/orbit rotations outside X_008 evidence; removing animation flinch/jaw presentation; using exact `math.sin/math.cos` to rebuild the same axis-angle function by hand.
Scalability potential: Low tier gets cheaper procedural animation noise. Middle/High/Ultra keep the same authored motion envelope and can spend saved budget on richer presentation.
Hardware Impact: Static project runtime angle API count changed from `32` to `29`. Measured frame gain remains PENDING until Unity compile/profiler; combat truth unchanged.

## Decision 022 - Exact 180-Degree Quaternion Shortcut

Problem: `ShinobuSocketConstructionJobs.FromToRotation` used `quaternion.AxisAngle(axis, math.PI)` for the opposite-vector case. For 180 degrees, axis-angle does unnecessary sin/cos work because the quaternion is exactly `(axis, 0)` when the axis is already normalized.
Solution: Replaced the call with `new quaternion(axis.x, axis.y, axis.z, 0f)` after the existing `math.normalizesafe` axis calculation.
Rejected Alternatives: Leaving a trig-backed helper in a Burst construction job; using small-angle approximation for a non-small exact 180-degree case; changing the general from-to rotation path.
Scalability potential: Low/Middle avoid exact axis-angle construction on snap-opposite cases. High/Ultra preserve exact socket orientation.
Hardware Impact: Static runtime angle API count changed from `29` to `28`. Runtime gain is narrow and PENDING measurement; the replacement is exact for this branch.

## Decision 023 - Visual/Procedural Rotation Helper Sweep

Problem: After combat proof was clean, project runtime angle inventory still contained angle-construction helpers in visual/procedural routes: VR horizon correction, tool recoil pose, analog gauge, VR lever, compass dial, celestial presentation, procedural coral/flora/wreckage/scatter, biomimetic debris yaw, hostile flora aim/spread, and player camera rotation composition.
Solution: Replaced those helpers with bounded no-trig quaternion construction. Small bounded rotations use normalized small-angle quaternions. General visual/celestial/procedural rotations use a range-reduced polynomial sin/cos approximation and normalize the resulting quaternion. `HectonPlayerMovement` camera composition uses its existing 1024-entry degree sin/cos LUT to avoid large-yaw polynomial error. Combat LUT/CAS truth was not changed.
Rejected Alternatives: Replacing physics angular integration in `SubmarineDynamicsContracts.cs`; leaving easy visual helpers as exceptions; hiding runtime angle APIs in the scanner; using `math.sin/math.cos` by hand to recreate the removed helper.
Scalability potential: Low tier avoids exact helper overhead in presentation/procedural paths. Middle/High keep stable visual behavior. Ultra can spend saved CPU on richer VISUAL_SYNC effects, not more exact helper math.
Hardware Impact: Static project runtime angle API count changed from `28` to `2`; measured frame gain remains PENDING because Unity compile/profiler is still blocked by the build guard.

## Decision 024 - Physics Owner Blocker Boundary

Problem: Two runtime angle APIs remain in `Physics/Vehicles/SubmarineDynamicsContracts.cs`, both integrating angular velocity into a delta rotation. This is physics truth, not a presentation effect or armor penetration.
Solution: Left the two calls intact and updated `PROJECT_WIDE_HOTPATH_SWEEP_X_008.json` to classify them as `remainingRuntimeAngleApiOwnerBlockers` for Physics/Vehicles. X_008 must not replace exact vehicle dynamics with a visual cheat without that owner route.
Rejected Alternatives: Blindly approximating submarine angular integration; falsely reporting project-wide zero angle APIs; editing outside X_008 authority to satisfy a counter.
Scalability potential: Low/Middle/High/Ultra keep vehicle truth under the physics owner. X_008 only removed presentation/procedural angle helper debt and preserved combat LUT authority.
Hardware Impact: Static proof is honest: combat remains `0` forbidden trig and `0` combat angle APIs; project runtime angle API count is `2`, not zero. Compile/profiler proof is still PENDING.

## Decision 025 - Tool Damage Source And Status Metadata Route

Problem: `ToolHitUtility.ApplyDamage` already preferred `CombatDamageRuntime.TryQueueDamage` for registered targets, but it stamped all tool hits as `DamageSourceIds.EnvironmentHazard`. `StunPistolTool.ResolveStunDuration()` existed but was not passed into the native status route, so stun shots were reduced to generic impact damage metadata.
Solution: Added explicit tool source ids (`PlayerToolImpact`, `SurvivalBlade`, `Harpoon`, `StunPistol`, `SalvageSampler`) and a source-aware `ToolHitUtility.ApplyDamage` overload that carries `damageType`, `statusBits`, and `statusDurationSeconds` into `CombatDamageRuntime.PackSignalMeta` and `CombatDamageSignalDetail`. Knife and harpoon remain impact damage with tool-specific source ids. Salvage sampler uses `MicroFracture`. Stun pistol now queues `Emp + Stunned + ResolveStunDuration()` through the same registered-target LUT/CAS/status pipeline.
Rejected Alternatives: Leaving all tools as environment hazards; calling `StunTargetRuntime.TryApply` directly from the pistol; adding a new managed event; removing the unregistered `IDamageReceiver` fallback before all legacy targets are registered.
Scalability potential: Low tier gets one central registered-target route with no new allocations. Middle/High/Ultra can drive richer feedback and status presentation from source/type bits without changing armor DTO layout or damage truth.
Hardware Impact: Correctness and telemetry attribution improved. Hot path adds no new managed allocation; registered targets still queue one native combat request. Measured microseconds remain PENDING because build/profiler is blocked by active compiler processes.

## Decision 026 - Continuous Combat Quality Policy

Problem: `CombatDamageRuntime` still had active binary quality state (`_mathLod`, `_requestedMathLod`, `ResolveFeedbackMathLod`) even though project doctrine requires continuous `GlobalQualityWeight` consumption. The binary state did not own gameplay truth, but it made feedback policy a 0/1 switch.
Solution: Removed `_mathLod`, `_requestedMathLod`, and `ResolveFeedbackMathLod`. Added `SetCombatVisualQualityWeight(float)` and changed `RefreshRuntimePolicy` to compute `_visualQualityWeight01 = saturate(SignalBusRegistry.GlobalQualityWeight01) * saturate(_requestedVisualQualityWeight01)`. Kept `SetCombatMathLod(CombatMathLod)` only as a compatibility adapter that maps legacy Low/High callers to 0/1 requested weight.
Rejected Alternatives: Deleting the public enum and risking external compile breaks; leaving the binary state because most current callers use global quality; changing gameplay truth based on quality.
Scalability potential: Low can suppress optional impact/wound feedback continuously. Middle/High/Ultra can raise feedback density without changing LUT DTO layout, health authority, or save identity.
Hardware Impact: Removes one active binary branch from combat policy bookkeeping. Runtime gain is too small to claim without profiling; the important gain is doctrine compliance and smoother quality scaling.

## Decision 027 - Sanitized Scanner Evidence

Problem: `OOP_Hitbox_Scanner.py` used raw text regex over C# files, so comments and string literals were counted as live code. The first sanitizer implementation was correct enough semantically but too slow because it re-stripped every file for every regex pass and exceeded the command timeout under system load.
Solution: Added C# comment/string stripping before regex matching and cached sanitized `(line, raw, code)` tuples once per file. Evidence still reports the raw source line for review, but match decisions use the code-only line.
Rejected Alternatives: Leaving comment/string false positives; replacing the scanner with Roslyn despite the known Unity binding wall; accepting a scanner that times out under load.
Scalability potential: Low-end validation remains static and deterministic; higher-tier CI can run the same scanner without inflating false-positive triage.
Hardware Impact: Tooling-only. Runtime frame cost unchanged. Static false-positive counts dropped: direct mutation 835 -> 717, damage bypass 72 -> 63, managed event 274 -> 255.

## Decision 028 - Real Parallel Evaluator Proof

Problem: The scanner/report text claimed `EvaluateArmorPenetrationJob : IJobParallelFor`, but the source only had a combined `CombatDamageTortureJob` that generated synthetic requests and evaluated them in one job. That was a proof defect: the named evaluator route did not exist as a separable Burst job.
Solution: Added `EvaluateArmorPenetrationJob : IJobParallelFor` with read-only request/detail/AUP/target-slot inputs and write-only 128B `ArmorPenetrationResolvedHitDTO` output. `RunArmorPenetrationTortureProof` now fills synthetic requests via scheduled `CombatDamageTortureJob`, then schedules and times the separated LUT evaluator. Scanner now records `parallelEvaluatorProof.evaluateJobActuallyScheduled=true` and `tortureJobActuallyScheduled=true`.
Rejected Alternatives: Keeping the hardcoded report claim; renaming `CombatDamageTortureJob` and losing the explicit torture route; mixing CAS health mutation into the evaluator; measuring mock request generation as if it were LUT solve time.
Scalability potential: Low tier keeps the production owner apply path and uses the flat evaluator only when batching is worthwhile. Middle/High can profile the separated evaluator without changing health authority. Ultra can spend saved combat truth time on deferred VISUAL_SYNC effects while the evaluator output contract stays stable.
Hardware Impact: Source proof is corrected; runtime gain remains PENDING until Unity import, Burst disassembly, and profiler execution. The change reduces evidence risk, not measured frame time yet.

## Decision 029 - Full Offset Verifier

Problem: The JSON report listed the full byte layout, but runtime/editor validators only checked size plus selected offsets. That left a proof gap for the user's explicit 64B ARM64 layout challenge.
Solution: Extended `ValidateArmorLayout` and `ArmorPenetrationLayoutVerifier` to check every `ArmorProfileDTO` field offset and the complete `ShinobuArmorPenetrationTable` offset set: `Cells=0`, `Revision=48`, `AuthoringHash=52`, `_pad0=56`; `SpeciesHashID=0`, `BaseHealth=4`, `BaseArmor=8`, `_pad0=12`, `ArmorGridLUT=16`.
Rejected Alternatives: Leaving the proof only in markdown/JSON; using `Pack=1`; adding managed reflection checks to hot paths. These checks remain validation/editor/cold code, not pellet fanout work.
Scalability potential: Low/Middle/High/Ultra share the same DTO layout. Quality weight cannot change offsets, save identity, or health authority.
Hardware Impact: Runtime hot path cost unchanged. The value is rejection of silent layout drift before it reaches ARM64 devices.

## Decision 030 - Development QA Harness Availability

Problem: The status called the 10k torture proof editor/development capable, but source gates were editor-only. That made the proof route unavailable in development players where profiler/Burst checks are often collected.
Solution: Changed `GenerateMockArmorImpacts` and `RunArmorPenetrationTortureProof` guards to `UNITY_EDITOR || DEVELOPMENT_BUILD`. The path still does not run in production builds and remains explicit QA/API invocation only.
Rejected Alternatives: Leaving the status inaccurate; enabling torture calls in release; moving CSV authoring or managed dump I/O into runtime players.
Scalability potential: Low hardware can run the same dev harness with production-like Burst/player constraints; High/Ultra can use it for stress captures without changing gameplay truth.
Hardware Impact: Production hot path unchanged. Development build proof becomes possible once Unity import/build is available.

## Decision 031 - Machine-Checked Branchless Source Surface

Problem: The branchless proof still depended on prose plus line evidence. That is insufficient for the user's challenge because a future edit could reintroduce `if`, `switch`, ternary, loop, trig, or angle helper tokens inside the evaluator while leaving the high-level formula unchanged.
Solution: Extended `Tools/OOP_Hitbox_Scanner.py` to extract sanitized C# bodies for `EvaluateArmorPenetrationJob.Execute`, `EvaluateArmorPenetrationCore`, `ResolveArmorAngleStep`, and `BuildArmorPenetrationResolvedHit`. The report now records explicit control-token count, loop-token count, `math.select` count, forbidden trig count, angle API count, and source byte count for each block. Current proof is zero explicit control tokens, zero loop tokens, zero forbidden trig, and zero angle APIs across all four checked bodies.
Rejected Alternatives: Keeping the branchless claim as markdown; treating whole `ProcessDamageQueueJob` as branchless despite queue drains, status/death handling, feedback gates, and CAS branches; claiming CPU instruction-cache or branch-predictor behavior without Burst disassembly.
Scalability potential: Low tier gets a flat evaluator whose source can be regression-checked by tooling. Middle/High/Ultra can add richer presentation outside the evaluator without weakening the LUT truth surface.
Hardware Impact: Tooling-only immediate cost. It prevents hidden source-level branch/trig regressions before ARM64/Burst profiling; measured frame savings remain pending compile/profiler.

## Decision 032 - Same-Slot CAS Torture Harness

Problem: The CAS proof was mathematical and source-backed, but there was no executable same-slot contention harness to validate the exact helper under a parallel pellet storm once Unity can import and run the code.
Solution: Added `RunAtomicHealthCasTortureProof` for editor/development builds. It initializes a single native health slot to `pelletCount`, schedules `AtomicHealthCasTortureJob : IJobParallelFor`, and has every worker call `TryAtomicSubtractHealth(Health, 0, 1f, ...)` against that same slot. The harness validates `successCount == pelletCount` and `finalHealth <= 0.0001f`. The editor X-Ray window now exposes `Run 100 CAS Torture`; scanner reports all `casTortureHarness` booleans true.
Rejected Alternatives: Claiming the CAS helper is runtime-proven without executing a contention harness; moving CAS mutation into the LUT evaluator; using managed locks; disabling safety globally instead of narrowly marking the one QA health array with `NativeDisableParallelForRestriction`.
Scalability potential: Low/Middle production remains serial owner apply. High/Ultra can use the harness to justify or reject future parallel apply phases; per-target aggregation remains the preferred performance design under dense same-target pellet storms.
Hardware Impact: Production hot path unchanged. Development harness gives a direct correctness test for the 100-pellet case; measured microseconds and Burst backend proof remain pending compile/runtime execution.

## Decision 033 - Domain Document LUT Size Correction

Problem: `Docs/Actual Domains of Project.txt` still described Armor Penetration LUT as `8x8`, conflicting with the extracted X_008 prompt, source DTO layout, route card, and scanner proof that define the table as `8x6`.
Solution: Changed the Echelon 5 domain line to `8x6 material-row x angle-step penetration tables`.
Rejected Alternatives: Leaving the root domain document stale; widening DTOs to satisfy a stale doc; editing unrelated domain descriptions.
Scalability potential: Low/Middle/High/Ultra share the same compact 48-cell truth table; documentation now matches the code contract.
Hardware Impact: 0 us runtime. Prevents future agents from reintroducing an 8x8/64-cell layout that would break the 64B DTO contract.

## Decision 034 - Shader Inverse-Trig Presentation Cheat

Problem: After the C# runtime inverse-trig route was clean, `Hecton_AlienSky_Master.shader` and `HectonFirmamentBake.compute` still used `asin` for latitude lookup and density bands. These are not armor truth, but they are GPU presentation/bake paths and kept project-wide inverse-trig evidence incomplete.
Solution: Replaced the shader `asin` calls with `HectonFastAsinUnit`, a bounded polynomial inverse-sine approximation over `[-1, 1]`, then reused it for latitude normalization and firmament density bands. Extended `Tools/OOP_Hitbox_Scanner.py` to scan `.shader`, `.compute`, and `.hlsl` sources separately and report `shaderAcosAsinCount`.
Rejected Alternatives: Claiming shader inverse trig is irrelevant after the user asked for whole-project continuation; deleting all shader `sin/cos/atan` tokens blindly; replacing combat/physics truth with approximations. Remaining shader trig is presentation/bake inventory and must be handled by visual owners only when profiler evidence justifies it.
Scalability potential: Low tier avoids expensive inverse-trig in sky/firmament presentation. Middle/High keep stable visual mapping. Ultra can spend saved GPU/CPU budget on richer sky and impact presentation without changing combat LUT, DTO layout, or health authority.
Hardware Impact: Static inverse-trig count is now `0` for C# scanner inventory and `0` for shader/compute/hlsl inventory. Runtime microseconds remain PENDING until Unity shader import/GPU capture is available.

## Decision 035 - Shader Atan2 Longitude Cheat

Problem: After shader `asin/acos` was removed, project shader sources still had `atan2` in sky longitude mapping, visor edge serration, phantom drone tangent orientation, and gas-giant celestial occlusion UV. These are presentation routes, but they are still inverse-angle instructions and weakened the whole-project cleanup evidence.
Solution: Added local `HectonFastAtan2` polynomial helpers to the affected shader/compute files and replaced all scanned shader `atan2` calls. Extended `Tools/OOP_Hitbox_Scanner.py` with `shaderInverseAngleCount` for `asin/acos/atan/atan2`.
Rejected Alternatives: Touching the two physics-owner `quaternion.AxisAngle` angular integration calls; rewriting every remaining shader `sin/cos` token without visual/profiler evidence; leaving `atan2` as an undocumented exception.
Scalability potential: Low tier avoids inverse-angle cost in sky/visor/drone/gas-giant presentation. Middle/High keep deterministic visual mapping with bounded approximation error. Ultra can spend saved budget on richer sky/visor presentation, not exact inverse-angle instructions.
Hardware Impact: Static shader inverse-angle count is now `0`; shader trig inventory dropped to `54` remaining `sin/cos` presentation/bake tokens. Runtime microseconds remain PENDING until Unity shader import/GPU capture is available.

## Decision 036 - Dead Damage UnityEvent Route Removal

Problem: Project-wide managed-event inventory still contained damage-oriented UnityEvent fields that were not bound in serialized assets: `EnvironmentalHazard.OnDamageDealt`, `FloraProjectile.OnHitPlayer`, and `FloraProjectile.OnHitEnvironment`. These were outside Burst jobs, but they were stale managed damage routes.
Solution: Removed the unused damage UnityEvent fields and the `OnDamageDealt` invoke. `EnvironmentalHazard` keeps enter/exit/intensity events because they are presentation hooks; actual injury remains status/signal based. `FloraProjectile` keeps its mathematical damage path through `BallisticsRuntime`.
Rejected Alternatives: Removing all `EnvironmentalHazard` UnityEvents blindly; deleting serialized particle/audio compatibility fields on `FloraProjectile`; converting hazard presentation hooks without a consumer route card.
Scalability potential: Low/Middle avoid dead managed damage hook dispatch and stale inspector surface. High/Ultra keep presentation flexibility through non-damage hazard events while damage truth stays on signal/status routes.
Hardware Impact: Static `damageManagedEventCandidateCount` dropped from `11` to `8`; `projectManagedEventTokenCount` dropped from `255` to `252`. Runtime microseconds are PENDING; removed route only dispatched when a non-radiation hazard applied damage.

## Decision 037 - Vault-Owned Torture Proof Buffers

Problem: The 10k armor evaluator proof and same-slot CAS proof were editor/development routes, but they still allocated `NativeArray` scratch memory with `Allocator.TempJob` per run. That weakened the Zero-GC proof surface and could hide QA-only allocation churn behind the O(1) LUT claim.
Solution: Added DataVault-owned scratch buffers for evaluator torture (`TortureRequests`, `TortureDetails`, `TortureAups`, `TortureTargetSlots`, `TortureResolvedHits`) sized to `ArmorTortureMaxImpacts=10000`, plus CAS torture buffers (`CasTortureHealth`, `CasTortureSuccesses`) sized to the bounded CAS proof. `RunArmorPenetrationTortureProof` and `RunAtomicHealthCasTortureProof` now resolve and lock those buffers before scheduling jobs; scanner reports zero `Allocator.TempJob` tokens inside both proof methods.
Rejected Alternatives: Leaving per-run proof allocations because they are development-only; expanding production queues to 10k; using managed arrays for test data; removing the proof harness instead of making it allocation-stable.
Scalability potential: Low hardware can run the torture proofs without allocation noise when Unity import is available. Middle/High/Ultra can use the same fixed buffers for profiler captures while gameplay truth, DTO layout, and queue authority remain unchanged.
Hardware Impact: Production hot path unchanged. QA/proof path removes per-run native allocation churn; measured microseconds remain PENDING until Unity executes the harness.

## Decision 038 - Environmental Hazard Damage Route Restoration

Problem: After the stale `OnDamageDealt` UnityEvent route was removed, `EnvironmentalHazard.ApplyDamage()` still calculated damage and interrupted player actions but did not publish injury through the central combat route or owner packet route. That meant non-radiation hazard exposure could become presentation-only.
Solution: Route normal hazard injury through `CombatDamageRuntime.TryQueueDamage` when the player is registered as a combat target. The signal carries `DamageSourceIds.EnvironmentHazard`, toxic damage metadata, poison status metadata, and an AUP impact position. If the player target is not registered yet, fallback uses `HectonPlayerHealth.ReceiveDamage(in DamagePacket)` so the owner still handles survival grace, invulnerability, sync, and trauma presentation.
Rejected Alternatives: Restoring the removed UnityEvent; calling `TakeDamage` directly; dropping damage when the combat target is temporarily unregistered; adding a new managed event route.
Scalability potential: Low/Middle keep one central hazard damage path with no per-tick allocations. High/Ultra can drive richer feedback from existing damage/status metadata without changing player health authority.
Hardware Impact: Correctness fix. Runtime cost is one queued combat signal at the existing hazard interval, or one owner packet fallback during registration gaps; measured microseconds remain PENDING.

## Decision 039 - Tool LocalPoint Preservation

Problem: `ToolHitUtility.TryQueueCentralDamage()` resolved a valid AUP hit position for registered targets but wrote `CombatDamageSignalDetail.LocalPoint = float3.zero`. That erased receiver-local impact data for weakspot, height, wound, and armor profile logic while the unregistered fallback path already computed the correct local point.
Solution: Compute `receiverComponent.transform.InverseTransformPoint(hitPoint)`, sanitize it to finite `float3`, and pass it into `CombatDamageSignalDetail.LocalPoint` before `CombatDamageRuntime.TryQueueDamage`.
Rejected Alternatives: Leaving registered targets with weaker data than fallback targets; recomputing local point inside the Burst job from managed transforms; adding a managed hit callback for tools.
Scalability potential: Low/Middle keep one cheap local transform conversion at tool hit ingress and a flat native payload afterward. High/Ultra can spend accurate local impact data on richer presentation without changing health authority.
Hardware Impact: Correctness fix. Runtime overhead is one cold/main-thread transform inverse per tool hit, replacing loss of data. Pellet fanout LUT path remains unchanged and measured microseconds remain PENDING.

## Decision 040 - Fauna Registered Combat Damage Route

Problem: Fauna could bite registered player targets through `CombatDamageRuntime`, but incoming tool/wreck damage to fauna still fell back to direct `FaunaBrain.TakeDamage`/`ICuttable.ApplyCutDamage` because `FaunaBrain` was not an `IDamageReceiver` combat target. That kept armored-fauna gameplay outside the 8x6 LUT route.
Solution: Added `FaunaBrain.CombatDamageReceiver.cs` partial implementing `IDamageReceiver`, `ICombatHitProfileSource`, and `ICombatPushbackBodySource`. Active fauna register with `CombatDamageRuntime` as `CombatEntityKind.Fauna`; apex predators use `OrganicHeavy`, aggressive or high-health fauna use `Shell`, and passive small fauna use `None`. Direct legacy health changes call `CombatDamageRuntime.SyncTargetHealth` so the native mirror does not drift. `ReceiveDamage` applies the owner state change and preserves survival-blade cut interaction plus wound presentation.
Rejected Alternatives: Blindly deleting direct fauna damage; making tools call fauna-specific managed methods first; registering fauna without syncing legacy direct damage; routing wound presentation from the Burst job.
Scalability potential: Low devices get one central native damage route for registered fauna. Middle/High keep owner-side behavior and wound visuals. Ultra can add richer impact presentation from the same local point/AUP payload without changing DTO layout or save identity.
Hardware Impact: Correctness and route unification. Registration is lifecycle/cold; hot damage path becomes one queued native request for registered fauna. Exact frame cost and balance impact require Unity runtime/profiler.

## Decision 041 - Manta Wreck Fauna Collision Bridge

Problem: `MantaEmergencyWreck` collision damage called `faunaBrain.TakeDamage(damage)` directly. After fauna registration, that direct call would bypass armor LUT, source id, AUP impact data, and native health CAS for registered fauna.
Solution: Added `DamageSourceIds.MantaEmergencyWreck = 15` and `TryQueueFaunaCollisionDamage`. Registered fauna collision damage now queues `CombatDamageRuntime.TryQueueDamage` with impact damage metadata, finite local point, finite AUP impact position, and normalized collision direction. The old direct `TakeDamage` call remains only as fallback when the target is not registered or AUP resolution fails.
Rejected Alternatives: Misusing `DamageSourceIds.MantaScooter`; dropping damage on registration gaps; adding a new managed event; editing vehicle physics angular integration outside X_008 authority.
Scalability potential: Low/Middle get predictable central damage accounting for wreck impacts. High/Ultra can drive stronger impact feedback from the stable source id and AUP payload.
Hardware Impact: Correctness fix. Hot path adds one bounded central queue attempt on wreck-fauna collision; fallback preserves legacy behavior. Measured microseconds remain PENDING.

## Decision 042 - Hazard Heat LocalPoint And Fallback Closure

Problem: `EnvironmentalHazard` heat damage queued central combat damage with `LocalPoint = float3.zero`, and the previous central-route restoration did not actually call the owner `DamagePacket` fallback when the player target was not registered.
Solution: Registered heat damage now computes target-local point from `playerTransform.InverseTransformPoint(impactPoint)`, keeps AUP impact position, and calls `ApplyOwnerHazardDamageFallback` only if `TryQueueCentralHazardDamage` fails. The fallback uses the existing `HectonPlayerHealth.ReceiveDamage(in DamagePacket)` owner contract.
Rejected Alternatives: Leaving area heat with no local impact data; dropping damage during registration gaps; restoring the old UnityEvent damage hook; adding a new managed event route.
Scalability potential: Low/Middle preserve one central heat injury route with fixed DTO payloads. High/Ultra can use the same local point/AUP metadata for richer VISUAL_SYNC feedback without changing health authority.
Hardware Impact: Correctness fix. Added cost is one target-local transform conversion per hazard damage interval and only an owner packet fallback on route failure. Measured microseconds remain PENDING.

## Decision 043 - Thermal Boiling Damage Central Route

Problem: `AbyssalThermalManager` wrote world coordinates into `CombatDamageSignalDetail.LocalPoint`, and `SubmarineAtmosphereSystem` boiling fauna spillover called `faunaBrain.TakeDamage(damageAmount)` directly. Registered fauna could therefore bypass the 8x6 LUT/native health route for room boiling damage, and thermal impact metadata was spatially wrong.
Solution: Abyssal boiling and thermal-shock damage now resolve target-local point and queue AUP impact data. Submarine room boiling now attempts `CombatDamageRuntime.TryQueueDamage` for registered fauna with `DamageSourceIds.SubmarineAtmosphereBoiling`, thermal/burning metadata, finite local point, AUP impact position, and normalized hazard-to-target direction before direct fallback.
Rejected Alternatives: Treating thermal damage as presentation-only; using world position as local point; dropping fauna damage when AUP resolution fails; routing boiling damage through a new event or unmanaged source without a stable source id.
Scalability potential: Low devices get one native registered-fauna thermal damage route and fallback only on registration/AUP gaps. Middle/High/Ultra can drive stronger burn/impact feedback from stable source/AUP/local data without touching DTO layout or combat truth.
Hardware Impact: Correctness and route-unification fix. Hot path adds scalar finite checks and one local transform conversion per affected target; direct managed damage is no longer first route for registered fauna. Measured microseconds remain PENDING.

## Decision 044 - Abyssal Thermal Registered-Target Gate

Problem: `AbyssalThermalManager` could enqueue thermal damage by raw `EntityId` without proving that the id was registered in `CombatDamageRuntime`. That wastes queue capacity and can silently drop damage in the native drain.
Solution: Added a bounded ancestor walk that resolves only registered combat targets before queuing boiling or thermal-shock damage. The resolved registered transform is also the source for target-local point conversion.
Rejected Alternatives: Queueing raw ids and hoping the drain finds them; adding scene searches; adding a direct `TakeDamage` fallback without an owner contract; changing public combat APIs during a multi-agent batch.
Scalability potential: Low/Middle avoid useless queue traffic and preserve one route for thermal damage. High/Ultra keep richer thermal presentation driven by AUP/local metadata, not by additional gameplay branches.
Hardware Impact: Correctness and capacity hygiene. Worst-case added work is up to six parent checks per thermal damage attempt; it prevents wasted queued requests against unregistered ids. Measured microseconds remain PENDING.

## Decision 045 - Fauna Attack AUP Payload Closure

Problem: The route sweep still found two registered central damage calls using `CombatDamageRuntime.TryQueueDamage(in signal, in detail)` without an AUP payload: predator bite in `FaunaBrain` and leviathan tentacle grab in `LeviathanTentacleVerletSolver`. That left impact feedback and AUP forensic data weaker than tool, wreck, hazard, and thermal routes.
Solution: Predator bite now sanitizes contact point/direction/local point and queues with `impactAup` resolved from `TryResolveAupFromRuntimeOrigin(safeImpactPoint)`. Leviathan grab now queues with `impactAup` resolved from `ToAbsoluteUniversePosition(tipRuntimePosition)`. Scanner proof now records `predatorBiteCarriesAup=true`, `leviathanGrabCarriesAup=true`, and `directTwoArgQueueCallCount=0` for the two fauna attack files.
Rejected Alternatives: Leaving the two-argument overload as an implicit zero-AUP route; dropping bite damage when AUP origin is temporarily unavailable; adding managed presentation callbacks; changing the public `TryQueueDamage` API in a dirty multi-agent batch.
Scalability potential: Low/Middle keep one bounded native damage route with enough spatial data for the LUT/impact pipeline. High/Ultra can spend the correct AUP/local payload on richer late-frame impact presentation without changing combat truth ownership, DTO layout, or save identity.
Hardware Impact: Correctness/data-quality fix. Runtime added work is scalar finite sanitation plus one existing AUP conversion per registered bite/grab damage event; no managed allocations or hot polling added. Measured microseconds remain PENDING.

## Decision 046 - Project-Wide AUP Queue Completeness Gate

Problem: After the two known fauna attack gaps were fixed, the proof was still scoped to those files. A future or hidden central damage ingress could still call the two-argument `CombatDamageRuntime.TryQueueDamage(in request, in detail)` overload and silently lose AUP hit payloads.
Solution: Generalized the scanner regex to find external `CombatDamageRuntime.TryQueueDamage(in *, in *)` calls across all `Assets/_Project/Scripts/**/*.cs`, excluding the overload declaration wrapper in `CombatDamageRuntime` itself. The combat report now records `toolDamageRouteProof.projectDirectTwoArgQueueCallCount` and hit details.
Rejected Alternatives: Trusting the current manual `rg`; removing the public overload during a dirty multi-agent batch; using a runtime guard that would add hot-path work instead of a static proof gate.
Scalability potential: Low/Middle keep stable native AUP payloads for all registered damage ingress. High/Ultra can rely on complete AUP metadata for richer deferred impact presentation without adding gameplay branches.
Hardware Impact: Runtime unchanged. Static proof now reports `projectDirectTwoArgQueueCallCount=0`; measured microseconds remain PENDING for runtime paths.

## Decision 047 - Leviathan Grab Finite AUP Payload

Problem: `LeviathanTentacleVerletSolver.TryQueueGrabDamage()` carried an AUP payload after Loop 25, but the route converted the resolved `AbsoluteUniversePosition` directly to `double3`. The call was not a two-argument zero-AUP route anymore, but the source proof did not force an explicit finite gate before the AUP was published.
Solution: Store the resolved value as `AbsoluteUniversePosition impactAupValue`, check `impactAupValue.IsFinite()`, and convert to `double3` only for finite payloads. Invalid AUP becomes `double3.zero`, matching the existing deferred impact convention without adding a managed fallback or route mutation. The scanner now requires that exact finite-check pattern before reporting `leviathanGrabCarriesAup=true`.
Rejected Alternatives: Keeping the chained `ToAbsoluteUniversePosition(...).ToAbsoluteDouble3()` conversion; dropping grab damage when AUP is invalid; adding a managed presentation callback; removing the public two-argument overload during a dirty multi-agent batch.
Scalability potential: Low/Middle keep one bounded native grab damage route with deterministic AUP sanitation. High/Ultra can use the same finite AUP/local payload for richer delayed impact presentation without changing combat truth ownership or DTO layout.
Hardware Impact: Runtime adds one scalar finite check per registered leviathan grab damage tick. Correctness/data quality improves; measured microseconds remain PENDING until Unity compile/profiler.

## Decision 048 - Registered Tool AUP Failure Does Not Bypass Central Damage

Problem: `ToolHitUtility.TryQueueCentralDamage()` returned `false` for a registered combat target if impact AUP resolution failed. The caller then attempted `ICuttable.ApplyCutDamage` or owner `ReceiveDamage` fallback, so a registered target could bypass the central LUT/CAS route because spatial metadata failed, not because the target was unregistered.
Solution: Sanitize `hitPoint` to `safeHitPoint`, use it for local-point conversion, initialize `impactAup` to `double3.zero`, and only overwrite it when `TryResolveImpactPointAup(safeHitPoint, out pointAup)`, `pointAup.IsFinite()`, and the converted `double3` are all finite. Registered targets now stay on `CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup)` even with degraded AUP metadata. The direct fallback remains for truly unregistered targets.
Rejected Alternatives: Keeping direct fallback on AUP failure; dropping registered tool damage when AUP fails; moving transform/AUP resolution into the Burst job; removing owner fallback for unregistered gameplay objects.
Scalability potential: Low/Middle keep one central native damage truth route for registered tool hits and avoid managed bypass under floating-origin edge cases. High/Ultra retain enough local point metadata for richer impact presentation when AUP is available, while degraded metadata remains deterministic.
Hardware Impact: Runtime adds finite checks already needed for safe payload sanitation. It prevents hidden managed fallback work for registered targets on AUP failure; measured microseconds remain PENDING until Unity profiler.

## Decision 049 - One-Argument Central Queue Proof Gate

Problem: The scanner already blocked external two-argument `CombatDamageRuntime.TryQueueDamage(in request, in detail)` calls, but the public one-argument overload `CombatDamageRuntime.TryQueueDamage(in request)` also drops detail and AUP payloads by design. A future ingress could therefore bypass local point/AUP proof without tripping the two-argument gate.
Solution: Added `DIRECT_ONE_ARG_DAMAGE_QUEUE_RE` and report fields `projectDirectOneArgQueueCallCount` / `projectDirectOneArgQueueHits`, excluding only the overload declaration wrapper in `CombatDamageRuntime.cs`. Current project count is `0`.
Rejected Alternatives: Trusting manual `rg`; removing the overload during a dirty multi-agent session; relying only on runtime null/default payload behavior.
Scalability potential: Low/Middle keep all registered ingress on full detail/AUP payloads. High/Ultra can depend on complete spatial metadata for deferred presentation without adding hot runtime guards.
Hardware Impact: Runtime unchanged. Static proof now blocks zero-detail external ingress; measured microseconds remain PENDING for runtime paths.

## Decision 050 - Queue Admission Reject Telemetry

Problem: `CombatDamageRuntime.TryQueueDamage()` returned `false` silently when a damage job was already scheduled or when `_queuedSignalCount >= MaxQueuedSignals`. Callers can choose not to fallback for registered targets, but the system had no proof artifact for dropped admission under overload or frame-phase contention.
Solution: Added queue-reject anomaly hashes, `TelemetryFlagQueueRejected`, and `PublishQueueRejectAnomaly()`. The helper rate-limits by frame and anomaly hash, then publishes a bounded `TelemetryAnomalySignal` through the existing signal lane. Scanner proof now records `blackBoxTelemetryProof.queueRejectTelemetryRateLimited=true`.
Rejected Alternatives: Direct fallback damage on queue full, which would bypass LUT/CAS; unbounded logging per rejected pellet; managed `Debug.Log` spam; increasing `MaxQueuedSignals` without profiler proof.
Scalability potential: Low/Middle keep predictable queue caps and get one diagnostic signal per frame/hash instead of silent loss. High/Ultra can raise presentation fidelity independently, while queue capacity remains an explicit profiled decision.
Hardware Impact: Hot path unchanged until admission rejection. Rejection path adds a few scalar comparisons and one bounded signal push per frame/hash; measured microseconds remain PENDING until profiler.

## Decision 051 - Mutator Guard Does Not Complete Jobs

Problem: `CanMutateTargets()` was a target register/sync/unregister guard, but it could finalize a completed damage job, clear `_damageJobScheduled`, and call `FinishArmorPenetrationScheduledCompletion()` outside `LateFrameTick()`. That is hidden phase work from a guard path and risks losing native result dispatch because `DispatchResults()` is owned elsewhere.
Solution: Reduced `CanMutateTargets()` to a pure scheduled-state gate: `return !_damageJobScheduled && !_statusJobScheduled;`. `LateFrameTick()` remains the non-forced owner for `DispatcherJobSwap.TryComplete(ref _damageJobHandle, forceComplete: false)`, `FinishArmorPenetrationScheduledCompletion()`, and `DispatchResults()`. `Shutdown()` remains the only forced completion path.
Rejected Alternatives: Completing jobs from register/sync/unregister guards; dispatching results from mutator guards; keeping the hidden finalize path because it was rare; adding a managed fallback for missed packets.
Scalability potential: Low/Middle avoid unpredictable same-frame completion stalls in hot owner registration paths. High/Ultra retain deterministic phase ownership and can profile completion windows cleanly.
Hardware Impact: Prevents hidden job completion and potential result loss. Mutations may defer while a job is scheduled instead of forcing phase work; measured microseconds remain PENDING until Unity profiler.

## Decision 052 - Leviathan Physical Strike Central Damage Route

Problem: `SargassumMicroFaunaBoids.ApplyLeviathanPhysicalStrike()` applied player health damage through `_playerHealth.TakeLeviathanDamage(leviathanStrikeDamage)` directly. For registered player targets this bypassed the central LUT/CAS route, stable source id, local impact payload, AUP forensic payload, and queue rejection telemetry.
Solution: Added `TryQueueLeviathanStrikeDamage`. Registered player targets now queue `CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup)` with `DamageSourceIds.FaunaLeviathanBite`, `CombatDamageTypes.Impact`, target-local point, finite-gated AUP, finite strike direction, and finite non-negative impulse magnitude. Direct `TakeLeviathanDamage` remains only when the player target is not registered. For registered targets, queue rejection does not fall through to direct damage; Loop 30 queue-reject telemetry is the diagnostic proof route.
Rejected Alternatives: Leaving direct player-health damage as the primary path; direct fallback on queue rejection, which would bypass armor/CAS under overload; adding a managed event hook; adding stun/status side effects that were not present in the original physical strike behavior.
Scalability potential: Low/Middle keep one native registered player damage route with bounded queue admission and deterministic degraded `double3.zero` AUP metadata when absolute position cannot be proven finite. High/Ultra can spend the same AUP/local payload on richer deferred impact presentation without changing combat truth, DTO layout, or save identity.
Hardware Impact: Correctness and route-unification fix. Runtime adds scalar finite checks, one local transform conversion, and one AUP conversion per registered leviathan strike event; it removes a hidden managed direct-damage route for registered targets. Scoped `Assembly-CSharp` build passed; measured frame microseconds remain PENDING until Unity runtime/profiler.

## Decision 053 - Registered Damage Routes Do Not Fallback On Metadata Or Queue Failure

Problem: Three registered-target helpers still returned the result of `CombatDamageRuntime.TryQueueDamage` or failed the route when AUP metadata could not be resolved: `MantaEmergencyWreck.TryQueueFaunaCollisionDamage`, `SubmarineAtmosphereSystem.TryQueueBoilingFaunaDamage`, and `EnvironmentalHazard.TryQueueCentralHazardDamage`. That allowed registered targets to fall through into direct `TakeDamage` or owner `ReceiveDamage` when the queue was busy/full or AUP metadata degraded, bypassing LUT/CAS exactly under stress conditions.
Solution: After target registration is proven, each helper now builds the full detail payload, resolves AUP opportunistically, degrades invalid AUP to `double3.zero`, calls `CombatDamageRuntime.TryQueueDamage`, and returns `true`. Direct fallback remains only for invalid input or unregistered targets. Queue rejection is surfaced by Loop 30 telemetry instead of silently switching routes.
Rejected Alternatives: Keeping direct fallback on queue rejection; dropping damage when AUP cannot be resolved; increasing queue capacity without profiler proof; adding managed event retries; editing construction/resource direct damage routes outside X_008 authority.
Scalability potential: Low/Middle preserve one central registered combat truth path even when floating-origin metadata is unavailable or the queue rejects admission. High/Ultra get stable local/AUP payloads for richer late-frame presentation when available, while degraded metadata remains deterministic and allocation-free.
Hardware Impact: Correctness under overload improved. Runtime cost is unchanged for valid AUP except final return semantics; degraded AUP paths avoid managed fallback work and publish a bounded queue-reject anomaly. Scoped `Assembly-CSharp` build passed; measured frame microseconds remain PENDING until Unity runtime/profiler.

## Decision 054 - Direct Return Queue Gates Removed Project-Wide

Problem: Exact scan still found `return CombatDamageRuntime.TryQueueDamage(...)` in tool hits, predator bite, and leviathan grab. These helpers were registered-target routes, but returning the queue admission bool allowed callers to fall back into managed direct damage on queue busy/full. Predator bite and grab also needed explicit finite `double3` validation after AUP conversion.
Solution: `ToolHitUtility.TryQueueCentralDamage`, `FaunaBrain.TryQueuePredatorBiteDamage`, and `LeviathanTentacleVerletSolver.TryQueueGrabDamage` now call `CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup)` and return `true` after registration. Predator bite and grab AUP payloads are finite-gated with `IsFinite()` plus `math.all(math.isfinite(...))`; invalid payloads publish `double3.zero`. Scanner now records project-wide direct-return central queue count `0`.
Rejected Alternatives: Trusting caller fallback to be harmless; allowing direct damage fallback on queue rejection; removing queue caps without profiler evidence; editing non-combat construction/resource damage routes to inflate file count.
Scalability potential: Low/Middle keep predictable queue ownership under burst damage and avoid hidden managed fallback when admission is rejected. High/Ultra can use real AUP payloads for presentation when present; degraded metadata remains deterministic and allocation-free.
Hardware Impact: Valid-path runtime is unchanged except final return semantics. Queue rejection now stays on the central telemetry route instead of invoking managed fallback damage. Compile for this loop is pending because active compiler processes block build by project rule.

## Decision 055 - Branchless Helper Surface Included In Proof

Problem: The previous branchless source proof covered the evaluator body but did not fully cover helper normalization. `ResolveArmorSurfaceNormal` and deflect `FrontDot` in the armor partial still called shared `ResolveExactDirection`, whose old ternary fallback could be used as a hidden branch-style route outside the reported LUT surface.
Solution: Armor LUT surface now uses `NormalizeArmorLookup` for surface-normal fallback and deflect `FrontDot`. The scanner explicitly analyzes `NormalizeArmorLookup`, `ResolveArmorSurfaceNormal`, and `CombatDamageRuntime.ResolveExactDirection`, and records `hiddenHelperGate.armorRuntimeResolveExactDirectionCallCount=0`. Shared `ResolveExactDirection` and `ResolveApproximateDirection` were also converted to `math.select` fallback for remaining production callers.
Rejected Alternatives: Claiming branchlessness from the top-level evaluator only; leaving helper ternaries as "probably optimized"; rewriting the entire production damage job for source-level branchlessness without profiler proof; editing Physics/Vehicles angular integration outside X_008 authority.
Scalability potential: Low/Middle keep the LUT truth path to dot/abs/saturate/floor/clamp/byte-load plus mask-style sanitation. High/Ultra can spend budget on deferred impact visuals; helper sanitation no longer weakens the proof surface.
Hardware Impact: Correctness/proof closure, not a measured frame-time win. It removes one hidden source-level branch proof gap around armor normal fallback; microseconds remain PENDING until Burst disassembly and profiler execution.

## Decision 056 - Fauna Hibernation Health Is Not Combat Damage

Problem: `FaunaDirector.HydrateResidentCreatures()` restored saved fauna health by spawning the creature at max health and calling `ai.TakeDamage(restoreDamage)`. That is not combat; it triggered hit flash, immediate hit reaction, parental defense/fear signals, and damage-style side effects during hibernation hydration.
Solution: Added `FaunaBrain.ApplyHibernationHealthSnapshot(float savedHealth)`. The method finite-gates the saved value, clamps it to current max health, writes `_currentHealth`, marks the combat mirror dirty, and calls `Die()` only if the saved snapshot is dead. `FaunaDirector` now calls this snapshot route directly.
Rejected Alternatives: Keeping artificial damage because it was simple; routing hydration through `CombatDamageRuntime` and fabricating a combat source; silently setting health without syncing the native combat mirror; editing unrelated resource/building direct damage routes to reduce scanner counts.
Scalability potential: Low/Middle avoid false AI/presentation side effects during resident hydration. High/Ultra keep combat feedback budget for actual impacts instead of spawn-state reconciliation noise.
Hardware Impact: Correctness and side-effect removal. Runtime removes one possible managed damage cascade during hydration spawn; measured microseconds remain PENDING because build/profiler are still blocked by active compiler processes.

## Decision 057 - Fauna Interaction Bonus Keeps Damage Source

Problem: `FaunaBrain.ApplyFaunaInteraction()` applied `DamageMultiplier` bonus damage through blind `TakeDamage(bonusDamage)`. The base interaction damage was source-aware, but the bonus lost `sourcePosition`, weakening hit reaction, parental defense/fear stimulus, source-aware combat mirror sync, and impact presentation.
Solution: Route the bonus through `TakeDamageFromSource(bonusDamage, sourcePosition)`. The scanner now proves `faunaRegisteredTargetRoute.interactionBonusUsesSourceAwareDamage=true` and exact search finds no remaining `TakeDamage(bonusDamage)` call.
Rejected Alternatives: Keeping blind bonus damage; fabricating a new central combat source for an already owner-local fauna interaction; editing unrelated construction/resource direct-damage routes to lower scanner counts.
Scalability potential: Low/Middle keep source-correct reaction without extra systems or allocations. High/Ultra can spend the preserved source vector on richer response/presentation while combat truth ownership remains unchanged.
Hardware Impact: Correctness fix. Runtime cost is unchanged materially: one existing owner-side damage helper is called with a source position instead of the blind wrapper. Measured microseconds remain PENDING because build/profiler are blocked by active compiler processes.

## Decision 058 - Predator Bite Registration-Gap Player Fallback

Problem: `FaunaBrain.HandleAttackPerform()` queued player bite damage through `TryQueuePredatorBiteDamage`, but if the player transform was not registered in `CombatDamageRuntime` yet, the method returned false and the hit had no owner fallback. Registered queue rejection was intentionally closed earlier, but registration gaps should still preserve gameplay damage through the owner packet contract.
Solution: Added `ApplyPredatorBiteOwnerFallbackDamage`. The player bite branch now calls it only when `TryQueuePredatorBiteDamage` returns false before registration. The fallback sends a `DamagePacket` to `HectonPlayerHealth.ReceiveDamage` with local point, `CombatDamageTypes.Impact`, and source id `FaunaBite` or `FaunaLeviathanBite`. Scanner proof records `predatorBiteUnregisteredOwnerFallback=true` while `predatorBiteDoesNotDirectFallbackOnQueueReject=true` remains true.
Rejected Alternatives: Allowing registered queue rejection to direct-fallback; dropping player bite damage during registration gaps; fabricating a managed event route; making the fallback run for every player bite before central registration proof.
Scalability potential: Low/Middle preserve bite damage determinism during bootstrap or registration churn without extra allocations. High/Ultra keep the central LUT/CAS route for registered targets and can still use source-tagged fallback presentation when the owner route is the only valid route.
Hardware Impact: Correctness fix. Registered hot path remains central queue plus return true. Fallback cost is one owner packet construction only when the player target is unregistered; measured microseconds remain PENDING because compiler processes block build/profiler.

## Decision 059 - Fauna Registration-Gap Fallbacks Use Owner Packets

Problem: Manta wreck collision and submarine boiling spillover had central registered-target routes, but their registration-gap fallback still called `faunaBrain.TakeDamage(...)`. That preserved damage amount but discarded source id, damage type, and local impact point before `FaunaBrain.ReceiveDamage` could resolve source-aware reactions.
Solution: Replaced both blind fallbacks with owner `DamagePacket` routes. `MantaEmergencyWreck.ApplyFaunaCollisionOwnerFallbackDamage` sends impact damage with `DamageSourceIds.MantaEmergencyWreck`; `SubmarineAtmosphereSystem.ApplyBoilingFaunaOwnerFallbackDamage` sends thermal damage with `DamageSourceIds.SubmarineAtmosphereBoiling`. Registered targets still queue centrally and return true. Scanner proof records both owner-packet fallback booleans true and damage-bypass candidates dropped to `60`.
Rejected Alternatives: Keeping blind `TakeDamage` because it was fallback-only; letting queue rejection fall through to owner packets; adding a new event route; editing construction/resource integrity owners outside X_008 authority.
Scalability potential: Low/Middle keep deterministic fallback damage with source/local metadata during registration gaps. High/Ultra can use the same packet metadata for richer owner-side impact presentation while registered combat truth remains LUT/CAS.
Hardware Impact: Correctness/data-quality fix. Registered hot path unchanged. Fallback constructs one 48B unmanaged packet instead of calling the blind wrapper; measured microseconds remain PENDING because compiler processes block build/profiler.

## Decision 060 - Leviathan Strike Registration-Gap Fallback Uses Owner Packet

Problem: `SargassumMicroFaunaBoids.ApplyLeviathanPhysicalStrike()` used the central registered player route, but the registration-gap fallback still called `_playerHealth.TakeLeviathanDamage(leviathanStrikeDamage)`. That kept the special trauma advisory but skipped the packet route and discarded local impact point metadata.
Solution: Added `ApplyLeviathanStrikeOwnerFallbackDamage`. The fallback now sends a `DamagePacket` to `HectonPlayerHealth.ReceiveDamage` with `DamageSourceIds.FaunaLeviathanBite`, `CombatDamageTypes.Impact`, and `ResolveLeviathanStrikeLocalPoint`. Registered targets still queue centrally and return true. Scanner proof records `leviathanStrikeDamageRouteProof.unregisteredFallbackUsesOwnerPacket=true`.
Rejected Alternatives: Keeping direct `TakeLeviathanDamage`; allowing queue rejection to owner-fallback; adding a second trauma event outside the player owner; editing Physics/Vehicles angular integration outside X_008 authority.
Scalability potential: Low/Middle preserve deterministic leviathan strike damage during registration gaps with source/local metadata. High/Ultra can drive stronger HUD/trauma presentation from the same owner packet while registered combat truth remains LUT/CAS.
Hardware Impact: Correctness/data-quality fix. Registered hot path unchanged. Fallback constructs one 48B unmanaged packet only when player combat registration is missing; measured microseconds remain PENDING because CPU/compiler guard blocks build/profiler.

## Decision 061 - External Direct TakeDamage Gate

Problem: After removing several direct fallback routes, the proof still relied on broad `damageBypassCandidateCount`, which includes method declarations, save DTO fields, construction integrity, and resource health. It did not separately prove that runtime external `.TakeDamage(...)` or `.TakeLeviathanDamage(...)` calls were gone.
Solution: Added `EXTERNAL_DIRECT_TAKE_DAMAGE_RE` to `Tools/OOP_Hitbox_Scanner.py`, scanning code with comments/strings stripped and excluding editor scanner text. The combat report now records `toolDamageRouteProof.projectExternalDirectTakeDamageCallCount=0` and an empty hit list.
Rejected Alternatives: Treating the broad bypass count as enough; hiding remaining owner declarations by suppressing them; removing public owner methods without call-site proof; relying on manual `rg` only.
Scalability potential: Low/Middle/High/Ultra share the same proof gate. Future direct health-wrapper reintroduction fails the static report before runtime profiling.
Hardware Impact: Runtime unchanged. This is proof infrastructure only; measured microseconds remain PENDING.

## Decision 062 - Branchless Layout CAS Proof Fields

Problem: The user demanded mathematical proof, not prose, for three specific debt points: no hidden branchy/trig angle path, exact 64-byte `ArmorProfileDTO` byte layout with no holes, and bounded CAS correctness for 100 same-target pellets.
Solution: Extended `Tools/OOP_Hitbox_Scanner.py` so the generated combat report now carries machine-readable proof fields: `sourceBranchlessnessVerdict=PASS`, `hundredPelletOperationModel` with zero checked branch/loop tokens and 100 flat LUT loads, `ArmorProfileDTO.lutCellMap` with all 48 `materialRow*6+angleStep` cells at offsets `16..63`, explicit `4+4+4+4+48=64` size math, and `casStabilityProof.hundredPelletBound` proving 99 maximum failed CAS races at K=100 under a 1024 retry ceiling.
Rejected Alternatives: Writing another chat-only proof; claiming CPU branch flush absence without Burst disassembly; using `Pack=1` or changing DTO layout to make the proof easier; marking Task 05/06 done without Unity/Burst/runtime execution.
Scalability potential: Low/Middle devices keep the flat 64-byte table and deterministic source-level branchless lookup. High/Ultra use the same combat truth and spend saved budget on deferred impact presentation, not heavier damage physics.
Hardware Impact: Runtime unchanged. Proof quality improved; measured microseconds, Burst branch lowering, and GC remain PENDING VERIFICATION until Unity/Burst/profiler artifacts exist.

## Decision 063 - Full Build Proof Wall Closure

Problem: X_008 proof had a scoped runtime compile pass, but full `Assembly-CSharp.csproj` was blocked by interface migration fallout in adjacent cold/read-model routes. A static LUT/CAS proof without full C# compile was not enough evidence for the current workspace.
Solution: Closed only the exact compile gaps surfaced by the compiler. `AtlasSignalSystem` now satisfies the already-declared `IAtlasSignalReadModel` proof properties. `QuestManager.TryCopyQuestPresentation(...)` is public for `IQuestSystem`. `IQuestSystem` declares the uint overloads already implemented by `QuestManager`. `GlobalRegistry` exposes existing AudioLog, FirstHour, and Localization read-model services through narrow properties consumed by existing code. Full `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal` now passes with `0` errors.
Rejected Alternatives: Reverting other agents' interface migrations; changing consumers back to concrete owners; editing `AudioLogSystem` when current source already implemented the required interface members; adding string quest lookups to hide missing uint overloads; claiming the old scoped build was sufficient.
Scalability potential: Low/Middle keep cold read-model dependencies out of hot combat truth. High/Ultra can use the same compiled read-model surfaces for richer presentation without changing combat authority, DTO layout, or GlobalQualityWeight semantics.
Hardware Impact: No hot combat runtime gain. This is a proof/build unblocker. Existing warnings remain non-X_008 debt: four `CS2002` duplicate-source warnings in `Hecton8.Core.csproj` and two `MSB9008` missing `Hecton8.Input.csproj` references.

## Decision 064 - Headless Runtime Proof Is Blocked By Unity License, Not Claimed

Problem: Static proof and full C# compile were not enough for Tasks 05-09. The project needed an executable route for 10k LUT torture and 100-pellet CAS storm, but Unity batch mode did not reach `Hecton8.Gameplay.ArmorPenetrationBatchProofRunner.Run`.
Solution: Added an editor-only batch proof runner that registers a temporary combat target, executes `RunArmorPenetrationTortureProof(10000, out telemetry)` and `RunAtomicHealthCasTortureProof(100, out successes, out finalHealth)`, and writes `Docs/Reports/COMBAT_RUNTIME_PROOF_X_008.json` only after actual execution. Closed the current compile wall by expanding `IObjectPoolService` to the existing `ObjectPoolManager` surface and generalizing module content ejection helpers to consume the interface. Full `Assembly-CSharp.csproj` build now passes. Runtime proof is explicitly recorded as blocked in `Docs/Reports/COMBAT_RUNTIME_PROOF_X_008_BLOCKED.json`: sandbox Unity fails Package Manager IPC; escalated Unity fails licensing with missing `com.unity.editor.headless` entitlement and return code `198`.
Rejected Alternatives: Faking `COMBAT_RUNTIME_PROOF_X_008.json`; treating static proof as runtime proof; converting combat hot code during a proof-harness pass; reverting other agents' object-pool/interface migrations; falling back to concrete `ObjectPoolManager` in module code; claiming Unity profiler or Burst disassembly evidence without a licensed Editor run.
Scalability potential: Low/Middle/High/Ultra all keep the same LUT/CAS combat truth. The new runner is cold editor proof infrastructure only; when Unity licensing is available it can produce measured data without touching runtime gameplay authority, DTO layout, or GlobalQualityWeight semantics.
Hardware Impact: Hot runtime gain is 0 us in this loop. Build proof improved from blocked to pass. Runtime microseconds, Burst branch lowering, GC, and blackbox dump execution remain PENDING VERIFICATION because Unity exits before the method runs.

## Decision 065 - Project Runtime Angle API Blocker Removed With No-Trig Angular Integration

Problem: After armor/combat had zero inverse-trig and zero angle-helper calls, the project-wide scanner still reported two runtime `quaternion.AxisAngle` calls in `Physics/Vehicles/SubmarineDynamicsContracts.cs`. They were not armor penetration code, but they remained the only runtime angle API owner-blockers in the proof artifact the user challenged.
Solution: Added `SubmarineDynamicsSimdMath.IntegrateAngularVelocityNoTrig(float3 angularVelocity, float dt)`. It computes a delta quaternion directly from angular velocity using `h = |w|*dt/2`, `q.xyz = w * dt * 0.5 * (1 - h^2/6 + h^4/120)`, `q.w = 1 - h^2/2 + h^4/24`, then normalizes with finite guards and identity fallback. Both submarine angular integration sites now call this helper instead of `quaternion.AxisAngle(axis, angle)`. Scanner now reports `runtimeAngleApiCount=0` and empty `remainingRuntimeAngleApiOwnerBlockers`.
Rejected Alternatives: Leaving the owner-blocker because it was outside X_008; replacing physics truth with a purely visual cheat; keeping `AxisAngle` and suppressing the scanner; using `math.sin/cos` in a wrapper; editing unrelated editor-only angle helpers to inflate scope.
Scalability potential: Low/Middle avoid runtime trig in submarine angular integration while keeping bounded stable rotation updates. High/Ultra use the same deterministic physics truth and spend saved budget on presentation, not heavier angular math. Editor-only authoring angle helpers remain outside runtime proof.
Hardware Impact: Expected small CPU reduction and no managed allocation change in the submarine physics job. Exact microseconds are PENDING. Post-edit compile is also PENDING: no-restore build failed before C# with `NETSDK1004` because `Temp/obj/Assembly-CSharp/project.assets.json` is missing, and restore-build was blocked by CPU/active-dotnet guard.

## Decision 066 - Static Proof Refreshed While Build Guard Is Closed

Problem: Loop 45 changed a runtime physics file after the last full build pass, but the workspace cannot legally launch restore-build while CPU is above 50% and another `dotnet/csc` compile is active. Claiming readiness from the earlier build would be false.
Solution: Re-extracted the X_008 prompt with `Select-String`, reran the scanner AST check, reran `Tools/OOP_Hitbox_Scanner.py`, and queried the generated JSON proof fields. The reports still prove zero combat trig, zero combat angle APIs, zero project inverse-trig inventory, zero runtime angle API owner blockers, branchless LUT source verdict `PASS`, exact 64B `ArmorProfileDTO` map, and CAS 100-pellet bound `PASS_STATIC_SOURCE`.
Rejected Alternatives: Launching `dotnet build` over active compiler processes; killing another agent's compiler; treating the previous full build as proof for a newer physics edit; fabricating runtime/Burst branch-lowering proof.
Scalability potential: Low/Middle keep verified flat source-level combat truth and no runtime angle API blockers. High/Ultra keep the same truth path and can spend presentation budget after a licensed Unity runtime proof exists.
Hardware Impact: 0 us measured in this loop. Static proof quality is refreshed; post-edit compile, Burst disassembly, profiler microseconds, and Unity runtime GC remain PENDING until the build guard and Unity license wall clear. Latest CPU samples after scanner were still `79%` and `100%`, so build launch remains illegal even with no active compiler processes in the final sample.

## Decision 067 - Branchless Claim Boundary Rechecked

Problem: The user challenged hidden inverse trig, branchlessness under 100 shotgun pellets, exact `ArmorProfileDTO` packing, and CAS stability. The risk is overstating source proof as CPU branch-flush proof or pretending the whole combat transaction is branchless.
Solution: Re-extracted the X_008 prompt with a bounded CLI range, reran exact source scans, inspected the remaining `Vector3.SignedAngle` hit, and queried the machine-readable proof report. `acos/asin` call syntax is absent under `Assets/_Project/Scripts`. The remaining non-editor-directory angle helper is inside `#if UNITY_EDITOR` in `HectonCelestialEngine.SyncEditorOrbitFromSunTransform`; combat and submarine runtime files have no angle API hits. The branchless claim remains scoped to the LUT index surface: normalize by `rsqrt`, `abs(dot)`, `saturate`, `floor/clamp`, `materialRow * 6 + angleStep`, one byte load. The full `ProcessDamageQueueJob` remains conditional by design because queue drain, target lookup, shield/status/death handling, feedback gates, and CAS success paths are gameplay transaction logic.
Rejected Alternatives: Claiming hardware branch-flush proof without Burst disassembly; deleting editor-only authoring angle math to inflate counts; rewriting conditional transaction ownership as mask math without profiler proof; running `dotnet build` while CPU is 100% and external compiler processes are active.
Scalability potential: Low/Middle devices keep the flat LUT truth path and avoid runtime inverse trig. High/Ultra use the same truth route and can spend saved budget on deferred impact presentation. Editor-only authoring math does not affect runtime device tiers.
Hardware Impact: 0 us measured in this loop. Static evidence is stronger, but compile, Burst disassembly, profiler microseconds, GC, and Unity runtime proof remain PENDING because the build guard and Unity license wall are still closed.

## Decision 068 - Production Damage Amount Branches Removed

Problem: The armor LUT index surface was branchless, but production `ProcessDamageQueueJob` still selected amount-driven vs kinetic damage through source-level ternaries. CAS sanitation also used a ternary for finite damage. This did not reintroduce trig, but it weakened the source-level branchlessness story around the damage amount feeding the LUT/CAS route.
Solution: Replaced production `baseAmount` and `momentumMultiplier` ternaries with `ResolveBranchlessBaseDamage(signal.Amount, signal.Direction, signal.ImpulseMagnitude, kind)` and `ResolveBranchlessMomentumMultiplier(signal.Amount, signal.Direction)`. Extended `ResolveBranchlessBaseDamage` to preserve the original priority order: finite positive explicit amount, finite positive impulse magnitude, then vector kinetic fallback. Removed the now-dead nested `ResolveKineticDamage` and `ResolveMomentumMultiplier` helpers instead of preserving unreachable legacy code. Converted CAS damage sanitation to `math.select`. Updated `Tools/OOP_Hitbox_Scanner.py` so the live branchless helpers are part of the recorded control surface.
Rejected Alternatives: Dropping `ImpulseMagnitude` to simplify the math; claiming the full damage transaction is branchless; rewriting queue/target/status/death logic as mask math without profiler proof; running a build while seven external `dotnet` processes are active.
Scalability potential: Low/Middle get fewer source-level conditional branches in the combat amount path while keeping deterministic damage priority. High/Ultra keep the same truth route and can spend presentation budget through deferred Impact/Deflect lanes.
Hardware Impact: Expected CPU gain is small and unmeasured; this is branch-proof cleanup, not a verified frame-time win. Scanner now proves zero explicit control tokens in the added helper surfaces. Compile and profiler remain PENDING because build guard is closed.

## Decision 069 - Sanitizer Branch Surface Reduced

Problem: After the LUT and damage amount helpers were clean, finite/fallback sanitizer code still used source-level ternaries in adjacent combat, armor, and ballistics math. These sites were not gameplay transaction branches, but they weakened the evidence chain around zero-trig, low-branch hot math feeding pellet and ricochet routes.
Solution: Replaced finite fallback ternaries with `math.select` in `CombatDamageRuntime.TryBuildCombatSignal`, telemetry scalar/local-point sanitation, `NormalizeOrDefault`, and `ResolveDirectionOctant`. Replaced armor tuning/quality/rotation/telemetry finite gates with `math.select` equivalents. Added `BallisticsRuntime.SelectFinite`, converted ballistics vector/quaternion normalizers to mask/rsqrt form, and used the helper in trajectory, primitive, tuning, hit penetration, and VFX placement sanitation. Extended `Tools/OOP_Hitbox_Scanner.py` with `branchlessSanitizerProof` for six helper surfaces: combat normalize, combat octant, armor quality, ballistics normalize, ballistics quaternion normalize, and ballistics tuning sanitize.
Rejected Alternatives: Rewriting collision/intersection, queue, status, shield, death, or target lookup branches as mask math without profiler proof; treating cold editor CSV parsing as a hot runtime issue; claiming target CPU branch-flush proof without Burst disassembly; launching `dotnet build` while CPU stayed at 99-100%.
Scalability potential: Low/Middle devices get cheaper finite/fallback helper code in damage and ballistics routes while preserving deterministic gameplay ownership. High/Ultra keep the same truth route and can spend saved budget on deferred VFX/audio/decal presentation, not heavier damage physics.
Hardware Impact: Expected gain is small and unmeasured; this is source-branch reduction and proof hardening. Scanner now reports `branchlessSanitizerProof.sourceBranchlessnessVerdict=PASS`, with zero explicit control tokens, zero forbidden trig, and zero angle APIs in the checked sanitizer surfaces. Compile/profiler remain PENDING because the build guard is closed.

## Decision 070 - Complete Calls Annotated As Cold Proof Only

Problem: Static search showed four `.Complete()` calls in `HectonCombatRuntime_ArmorPenetration.cs`. Three were already marked cold editor/QA proof paths, but the CAS storm proof completion had no annotation. That created ambiguity: it looked like a possible hidden same-frame readback in the armor route even though the method is the cold proof harness.
Solution: Added the missing `COLD EDITOR/QA ONLY` annotation to `RunAtomicHealthCasTortureProof` and extended `Tools/OOP_Hitbox_Scanner.py` to record `armorRuntimeCompleteCallCount`, `unannotatedArmorRuntimeCompleteCallCount`, and the unannotated hit list. The report now proves four complete calls and zero unannotated complete calls in the armor runtime file.
Rejected Alternatives: Removing the cold proof completions and losing deterministic QA evidence; pretending `.Complete()` is absent; suppressing the search result without an artifact; launching a build while CPU was 82% with an active `dotnet` process.
Scalability potential: Low/Middle/High/Ultra runtime FrameTick remains free of these proof completions. The cold proof harness can still run deterministically when Unity licensing and build guard allow it.
Hardware Impact: 0 us hot runtime change. This is proof-route clarity only. Compile/profiler remain PENDING because the build guard is closed.

## Decision 071 - Ballistics Read Accessors Stop Finalizing Jobs

Problem: `BallisticsRuntime.TryGetDebugBuffers` and `TryGetImpactVfxStaging` were named read accessors but called `TryFinalizeScheduledNoWait()`. That violates the doctrine that `Get/TryGet/Read/Resolve` routes must not mutate global state, complete/finalize jobs, or publish side effects.
Solution: Removed the finalization calls from both accessors. They now return `false` while `_jobScheduled` is true. Job completion remains owned by `FrameTick`, `LateFrameTick`, and teardown. `Tools/OOP_Hitbox_Scanner.py` now emits `ballisticsReadAccessorPurityProof` to prove the two read accessors do not call the finalizer.
Rejected Alternatives: Leaving the mutation because it was non-blocking; renaming the accessors while preserving hidden mutation; completing jobs from presentation/VFX reads; running build while CPU was still above the 50% guard.
Scalability potential: Low/Middle avoid unexpected read-side work spikes and hidden owner-state mutation. High/Ultra keep predictable phase ownership while VFX consumers read only completed snapshots.
Hardware Impact: Runtime hot cost can only improve or stay equal: presentation/debug reads no longer pay finalization checks. Exact microseconds remain PENDING; compile/profiler are blocked by the build guard.

## Decision 072 - Ballistics Read Accessors Stop Cold Allocation

Problem: Loop 51 removed read-side finalization, but `BallisticsRuntime.TryGetTuning`, `TryGetDebugBuffers`, and `TryGetImpactVfxStaging` still called `EnsureInitialized()`. That cold path can acquire Vault lanes and seed defaults, so a read accessor could allocate/grow buffers or mutate owner state.
Solution: Replaced those calls with `CanReadVaultSnapshots()`, a pure bound-state gate that only checks `_initialized`, `_vault != null`, and existing lane bindings. The three accessors now return `false` unless Ballistics was already initialized by an owner/mutator path, and return `false` while `_jobScheduled` is true. The scanner now proves both no finalization and no `EnsureInitialized()` in all three read blocks.
Rejected Alternatives: Keeping `TryGetTuning` as an implicit bootstrap because it is editor-facing; moving allocation into a renamed read helper; letting VFX/debug reads lazily create buffers; running build while CPU was 96% and many `dotnet` processes were active.
Scalability potential: Low/Middle avoid one-frame read-triggered spikes and hidden Vault allocation when debug or VFX presentation probes a cold runtime. High/Ultra keep the same completed-snapshot read model while owner phases decide when to pay setup cost.
Hardware Impact: Expected hot-path gain is small but directionally positive: read accessors no longer pay cold initialization checks or allocate lanes. Measured microseconds and GC remain PENDING because build/profiler are blocked by CPU/compiler guard.

## Decision 073 - Combat Read Accessors Fail Closed On Stale Slots

Problem: `TryGetTargetHealthFraction`, `TryGetStatusEffectMask`, and `TryGetStatusMobilityScale` trusted a slot returned by `_slotByTargetId` and immediately indexed NativeArrays. Normal owner flow keeps the map coherent, but teardown/rebind or stale map corruption should fail closed instead of trusting the slot.
Solution: Added unsigned bounds checks before every NativeArray dereference in those accessors. `TryGetTargetHealthFraction` validates `_health` and `_invMaxHealth`; both status accessors validate `_statusEffectStates`. The scanner now records `combatReadAccessorBoundsProof` with a `PASS` verdict.
Rejected Alternatives: Assuming `_slotByTargetId` can never be stale; adding exceptions/asserts in gameplay read paths; completing jobs or reinitializing storage from the read accessor; running build while CPU was 65% with active `dotnet`.
Scalability potential: Low/Middle avoid rare crash spikes from invalid read probes during target churn. High/Ultra keep the same snapshot access pattern without adding managed validation or owner-phase work.
Hardware Impact: Hot read cost adds one or two unsigned comparisons only on external probes. It is cheaper than a failed safety check/crash path. Measured microseconds remain PENDING because compile/profiler are blocked.

## Decision 074 - Armor Debug Read Accessor Clamps Target Buffer Bounds

Problem: `HectonCombatRuntime_ArmorPenetration.TryGetArmorDebugBuffers` checked only `TargetArmorProfiles.IsCreated` before returning four read-only Vault views and a raw `_targetCount`. A partial Vault rebind or stale target count could let debug/editor consumers index `TargetRootAups` or `TargetHalfExtents` past their actual lengths.
Solution: Require `TargetArmorProfiles`, `TargetRootAups`, `TargetHalfExtents`, and `DebugHits` to be created before exposing snapshots. Clamp `targetCount` to the shortest target buffer and clamp negative `_targetCount` to zero. Added `armorDebugReadAccessorBoundsProof` to the scanner so the contract is machine-checked.
Rejected Alternatives: Assuming debug-only consumers can tolerate invalid NativeArray views; allocating a defensive managed copy; completing or reinitializing armor jobs from the read accessor; returning raw `_targetCount`; running build while CPU was 71%.
Scalability potential: Low/Middle avoid rare editor/debug crashes and read-side phase surprises during target churn. High/Ultra keep the same snapshot route for richer armor visualization without changing combat truth, DTO layout, or hot LUT evaluation.
Hardware Impact: 0 us measured. This is a cold/debug read safety fix; hot combat evaluation remains unchanged. Compile/profiler proof remains PENDING because the build guard is closed.

## Decision 075 - Status Debug Snapshot Guards Receiver Array Length

Problem: `TryGetStatusEffectDebugSnapshot` checked `_receiverTransforms != null` and slot against `_targetCount`, but indexed `_receiverTransforms[slot]` without checking the managed array length. `TryGetTargetHealthFraction` also used NativeArray lengths as an implicit created check.
Solution: Added explicit `_health.IsCreated` and `_invMaxHealth.IsCreated` checks before health fraction reads. Added `(uint)slot >= (uint)_receiverTransforms.Length` guard before the status debug transform array read. Expanded `combatReadAccessorBoundsProof` to cover created checks, NativeArray bounds, target count, receiver array length, and status-state length.
Rejected Alternatives: Trusting `_targetCount` as a proxy for managed array length; relying on NativeArray length access when the array may be uncreated; adding exceptions/asserts to read paths; running build while CPU was 76% with active `dotnet`.
Scalability potential: Low/Middle avoid rare debug/presentation crashes during registration churn and teardown. High/Ultra keep the same status visualization route without changing status gameplay truth or adding managed copies.
Hardware Impact: 0 us measured. Adds cheap read-side guards only; hot status application and armor LUT evaluation are unchanged. Compile/profiler proof remains PENDING because the build guard is closed.

## Decision 076 - Ballistics Debug Counts Clamp To Returned Buffers

Problem: `BallisticsRuntime.TryGetDebugBuffers` stopped hidden read-side initialization/finalization, but still returned `trajectoryCount` and `primitiveCount` from raw internal counters. After a buffer swap, target churn, or partial Vault rebind, a debug consumer could iterate past the returned read-only buffers.
Solution: Clamp `trajectoryCount` to the smaller of returned trajectory and hit buffer lengths, clamp `primitiveCount` to returned primitive buffer length, and clamp negative raw counters to zero. Expanded `ballisticsReadAccessorPurityProof` so it checks both purity and count bounds.
Rejected Alternatives: Trusting `_activeReadCount`, `_pendingTrajectoryCount`, and `_primitiveCount` as always coherent with returned buffers; allocating defensive copies; finalizing jobs in the read accessor to repair counters; running build while CPU was 99%.
Scalability potential: Low/Middle avoid debug/VFX inspection crashes under solver swap or teardown. High/Ultra keep the same snapshot inspection route for richer ballistics visualization without touching solver truth or adding managed allocations.
Hardware Impact: 0 us measured. Adds cold/read-side clamps only; solver jobs and armor LUT evaluation are unchanged. Compile/profiler proof remains PENDING because the build guard is closed.

## Decision 077 - Status Debug Target Count Clamps To Snapshot Buffers

Problem: `ReadStatusEffectDebugTargetCount` returned raw `_targetCount`. The snapshot accessor itself now fails closed, but the editor gizmo could still iterate stale slots and repeatedly probe invalid buffer entries after target churn or partial teardown.
Solution: Return zero while a status job is scheduled, while status states are missing, or while receiver transforms are unavailable. Otherwise clamp the debug target count to `min(_targetCount, _statusEffectStates.Length, _receiverTransforms.Length)` with non-negative `_targetCount`. Expanded `combatReadAccessorBoundsProof` to cover this count route.
Rejected Alternatives: Leaving fail-closed snapshot as the only protection; trusting `_targetCount` as globally authoritative for debug array length; allocating a filtered managed target list; running build while CPU was 99%.
Scalability potential: Low/Middle avoid editor/debug iteration spikes and stale-slot noise during churn. High/Ultra keep the same debug visualization route and can display richer status overlays without changing status truth storage.
Hardware Impact: 0 us measured. This affects editor/debug read iteration only; hot status jobs and armor LUT evaluation are unchanged. Compile/profiler proof remains PENDING because the build guard is closed.

## Decision 078 - Status Telemetry Read Uses Actual Ring Length

Problem: `TryGetLastStatusEffectTelemetry` checked that the telemetry ring and cursor NativeArrays existed, but it did not check actual lengths and used `StatusEffectTelemetryCapacity` for modulo indexing. A partial Vault rebind or malformed lane could expose a short ring/cursor and make a read accessor index out of range.
Solution: Added ring length and cursor lane length guards. The latest telemetry index now uses modulo over `min(StatusEffectTelemetryCapacity, _statusEffectTelemetryRing.Length)`. Added `statusTelemetryReadAccessorBoundsProof` to the scanner.
Rejected Alternatives: Trusting declared capacity as always equal to actual Vault lane length; relying on Vault creation invariants inside a public read accessor; allocating a defensive telemetry copy; running build while CPU was 99%.
Scalability potential: Low/Middle avoid debug/status telemetry crashes during rebind or teardown. High/Ultra keep the same blackbox/status telemetry route without changing hot status evaluation.
Hardware Impact: 0 us measured. This is read-side telemetry safety only; status jobs and armor LUT evaluation are unchanged. Compile/profiler proof remains PENDING because the build guard is closed.

## Decision 079 - Status Telemetry Writers And Dump Use Actual Ring Length

Problem: Loop 58 fixed status telemetry reads, but `WriteStatusCompletionTelemetry`, `AppendStatusTelemetryEntry`, and `TryDumpStatusEffectTelemetry` still trusted `StatusEffectTelemetryCapacity` for indexing and dump iteration. A malformed or partially rebound Vault lane could make the blackbox writer or dump path index beyond the actual NativeArray length, and the dump latch was set before successful row writes.

Solution: Added created, ring length, and cursor length guards to both writer paths. Both writers now compute `ringLength = min(StatusEffectTelemetryCapacity, _statusEffectTelemetryRing.Length)` and modulo by that value. Status dump now emits actual ring length, computes ordered start/index by actual ring length, tolerates missing cursor storage by using cursor `0`, and sets `_statusEffectTelemetryDumpedThisSession` only after telemetry rows are written. The scanner now records `statusTelemetryWriteBoundsProof` and `statusTelemetryDumpBoundsProof`.

Rejected Alternatives: Trusting declared capacity as a Vault invariant; allocating a managed copy for dumping; keeping the latch before IO; silently skipping the write proof because the path is diagnostic; running `dotnet build` while CPU was 56%.

Scalability potential: Low/Middle avoid rare diagnostic crash or corrupt dump during churn/rebind. High/Ultra keep the same 300-frame blackbox route and can add richer status overlays without changing gameplay truth or DTO layout.

Hardware Impact: 0 us measured. This is owner/diagnostic path correctness; hot status jobs and armor LUT evaluation are unchanged. Compile/profiler proof remains PENDING because the build guard is closed.

## Decision 080 - Status Jobs Require Actual Counter/Cursor Lane Lengths Before Scheduling

Problem: Status jobs mutate `CombatStatusEffectCounterLane` via unsafe pointer arithmetic at `index * 64`, and anomaly reporting can touch telemetry cursor lane `StatusEffectTelemetryLastAnomaly`. Locking a Vault buffer proves ownership, not that the actual NativeArray length still satisfies the lane indices after a malformed rebind. `ClearStatusEffectTelemetryImmediate` also still iterated the declared ring capacity.

Solution: Added `CanUseStatusEffectJobBuffers()` after Vault locks and before scheduling `ApplyStatusEffectRequestsJob` / `EvaluateStatusEffectsJob`. It fails closed unless counter lanes are at least `StatusEffectCounterLength`, telemetry cursor lanes are at least `StatusEffectTelemetryCursorLength`, the ring has positive actual length, and all job input/output buffers are created. Preflight failure unlocks both status Vault buffers and borrowed armor buffers. Telemetry clear now iterates only `min(StatusEffectTelemetryCapacity, _statusEffectTelemetryRing.Length)`.

Rejected Alternatives: Trusting successful Vault lock as a length invariant; adding per-counter length branches inside Burst unsafe helpers; allowing diagnostic clear to trust declared capacity; running `dotnet build` while CPU was 100%.

Scalability potential: Low/Middle avoid rare unsafe-lane OOB crashes during rebind/teardown. High/Ultra keep the same status effect job route and blackbox telemetry without adding managed copies or changing gameplay truth.

Hardware Impact: 0 us measured. Adds owner-phase preflight comparisons before scheduling; hot per-entity status evaluation and armor LUT math are unchanged. Compile/profiler proof remains PENDING because the build guard is closed.

## Decision 081 - Combat Damage Job Rejects Stale Target Slots Before Native Reads

Problem: `ProcessDamageQueueJob` trusted `_slotByTargetId` and read many NativeArrays at `slot` before any slot-length proof. A stale map entry or partial buffer rebind could make the hot damage job index past health, armor, status, or armor-LUT target buffers. Dispatch paths also trusted receiver/result buffer lengths after job completion.

Solution: Added `CanUseDamageJobBuffers(in armorViews)` before damage job scheduling to verify all owner and armor Vault views needed by the job. Added `IsValidDamageSlot(slot)` inside `ProcessDamageQueueJob` before direct slot reads. Guarded `_receivers[slot]` during managed mirror dispatch, clamped status result dispatch to actual result buffers, and made `ClearCounters()` clamp to actual counter length. The scanner now records `damageJobBufferAndSlotBoundsProof`.

Rejected Alternatives: Trusting `_slotByTargetId` as globally infallible; adding exceptions/asserts in Burst jobs; relying only on public read-accessor guards; running `dotnet build` while CPU was 77%.

Scalability potential: Low/Middle avoid rare crash or undefined NativeArray reads during target churn. High/Ultra keep the same damage truth route, with richer presentation still deferred through signal lanes.

Hardware Impact: 0 us measured. Adds owner preflight and one slot-validity branch before direct target reads; prevents catastrophic stale-slot work. Compile/profiler proof remains PENDING because the build guard is closed.

## Decision 082 - Combat Telemetry Uses Actual Ring And State Lengths

Problem: Base combat telemetry still used `TelemetryFrameCapacity` for ring modulo and dump metadata, and read `_telemetryState[TelemetryWriteCursorIndex]` without checking actual state length. This could make the crash blackbox fail exactly when telemetry storage was partially rebound or malformed.

Solution: `RecordTelemetry` now requires created ring/state buffers, positive actual ring length, and `TelemetryStateLength` state capacity. It writes by modulo over `min(TelemetryFrameCapacity, _telemetryRing.Length)`. `TryDumpCombatTelemetry` writes the actual dump count, reads cursor only when state storage is long enough, and preserves latch-after-write behavior. `DispatchResults` now clamps result count to actual `_results.Length`.

Rejected Alternatives: Trusting declared 300-frame capacity as a runtime invariant; allocating a managed telemetry copy for dump ordering; leaving crash dump metadata as declared capacity; running `dotnet build` while CPU was 76% with active `dotnet`.

Scalability potential: Low/Middle avoid blackbox failure during churn and crash capture. High/Ultra keep the same combat telemetry route and can add richer diagnostics without changing damage truth or layout.

Hardware Impact: 0 us measured. Adds only owner/dispatch telemetry bounds checks; combat LUT/CAS math is unchanged. Compile/profiler proof remains PENDING because the build guard is closed.

## Decision 083 - Managed Mirror Slots Are Checked Before Side Effects

Problem: Loop 61 guarded `_receivers[slot]` in `DispatchResults`, but other owner-side managed mirrors still trusted the same slot when applying pushback, resolving impact world points, refreshing ballistic target AABBs, or syncing hit profile data. `_targetCount` is not a proof that `_receivers`, `_receiverTransforms`, `_targetBodies`, or native ballistic/profile buffers share the same actual length after target churn or partial rebind.

Solution: Added `IsManagedMirrorSlotReadable(slot)` and routed `DispatchResults` through it. Pushback, registered-transform resolution, and world-point resolution now fail closed on stale managed mirror slots. `RefreshBallisticTargetAabbs` clamps iteration to the shortest managed/native buffer set, and `RefreshTargetHitProfile` checks managed receiver plus native profile buffer lengths before writes. `Tools/OOP_Hitbox_Scanner.py` now emits `managedMirrorBoundsProof` and the damage dispatch proof accepts the helper only when it checks all managed mirror arrays.

Rejected Alternatives: Trusting `_targetCount` as a proxy for every managed mirror array; duplicating partial receiver-only checks in each call site; allocating defensive managed copies; running `dotnet build` before checking the CPU/compiler guard.

Scalability potential: Low/Middle avoid rare side-effect crashes and stale ballistic registration under target churn. High/Ultra keep the same combat truth route and can use richer pushback/VFX metadata without changing LUT/CAS authority.

Hardware Impact: 0 us measured. This adds owner-side bounds checks only; armor LUT evaluation and CAS health subtraction are unchanged. Compile/profiler proof remains PENDING until the build guard and Unity license wall clear.

## Decision 084 - Target Mutators Prove Slot Storage Before Writes

Problem: Target registration, sync, and unregister paths used slots from `_slotByTargetId` or `_targetCount` and wrote many native/managed mirrors without one shared proof that every affected lane had that slot. `UnregisterTarget` also swap-moved core target lanes but did not move or clear transient status result lanes, which could leave stale status result residue for a reused slot.

Solution: Added `CanUseExistingTargetSlot`, `CanUseRegistrationTargetSlot`, and `CanUseTargetStorageSlot`. Register/sync/unregister routes now fail closed before writing if health, armor, target flags, status masks/durations, status result lanes, status-effect state, or managed mirrors are not actually long enough. Swap-remove now validates the last slot before map mutation, moves transient status result state with the moved target, and clears result residue in `ClearSlot`. Armor profile seed/move/clear now use `CanUseArmorTargetSlot`, covering profile, AUP, rotation, and half-extent lanes.

Rejected Alternatives: Trusting `_targetCount` and the hash map as infallible; leaving status result residue because it is transient; checking only `_receivers.Length`; allocating defensive copies; widening the hot Burst job instead of hardening owner-phase mutators.

Scalability potential: Low/Middle avoid target churn crashes and ghost status result dispatch. High/Ultra keep deterministic target ownership while spending saved runtime budget on deferred presentation, not repair code after corrupt mirrors.

Hardware Impact: 0 us measured. Adds owner-phase comparisons during registration/sync/unregister only; LUT evaluation and CAS subtraction are unchanged. Compile/profiler proof remains PENDING because build guard and Unity license constraints still apply.

## Decision 085 - Damage Admission Proves Detail And AUP Storage

Problem: `TryQueueDamage` checked `_queuedSignalCount < MaxQueuedSignals`, but then wrote `_signalDetails[detailIndex]` and armor impact AUP storage without proving those actual lanes still existed and were long enough. If storage was malformed or partially rebound, admission could corrupt or throw before the damage job preflight rejected scheduling.

Solution: Added `TelemetryAnomalyQueueStorage` and `CanUseDamageIngressSlot(detailIndex)`. Admission now fails closed before any ingress writes unless `_damageSignals`, `_signalDetails`, and armor `SignalImpactAups` are created and the detail index is within both declared queue budget and actual lane lengths. Failure uses the existing rate-limited queue anomaly route.

Rejected Alternatives: Relying on later `CanUseDamageJobBuffers`; silently dropping malformed storage without telemetry; forcing armor storage creation from the read/admission path; expanding the queue budget to mask bad storage.

Scalability potential: Low/Middle avoid rare admission crashes and stuck queues under storage churn. High/Ultra keep the same central queue truth and get cleaner telemetry for storage faults without changing damage authority.

Hardware Impact: 0 us measured. Adds admission-time bounds checks only; per-hit LUT/CAS job math is unchanged. Compile/profiler proof remains PENDING until the build guard and Unity license wall clear.

## Decision 086 - Status Simulation Preflight Proves Target Count Lengths

Problem: `EvaluateStatusEffectsJob` is scheduled over `_targetCount`. The job has per-index validity guards, but the owner preflight only proved created buffers, not that each lane was at least `_targetCount` long. That leaves a hidden dependency on early-out checks inside the Burst loop and makes scheduling accept malformed storage it should reject.

Solution: `CanUseStatusEffectJobBuffers` now computes `targetCount = max(0, _targetCount)`. It fails closed unless status state/mask/duration/fracture lanes are at least targetCount long, and when simulation work runs it also checks target root AUPs, ids, health lanes, result lanes, VFX request lane, and status damage-signal lane against targetCount before scheduling.

Rejected Alternatives: Relying solely on `EvaluateStatusEffectsJob.IsValidEvaluationIndex`; clamping the scheduled count silently; allocating compact status target lists; changing status gameplay cadence or math.

Scalability potential: Low/Middle avoid malformed storage scheduling and wasted Burst iterations. High/Ultra keep the same status truth route and can spend stable frames on richer status VFX rather than recovery from bad storage.

Hardware Impact: 0 us measured. Adds owner-phase preflight comparisons before scheduling; per-target status math and armor LUT/CAS paths are unchanged. Compile/profiler proof remains PENDING because the build guard and Unity license wall still apply.

## Decision 087 - Armor Snapshot Refresh Clamps Its Own Target Lanes

Problem: `RefreshArmorTargetSnapshots(ref views)` is called by both damage scheduling and status scheduling. The status path called it before the strengthened status preflight, while the refresh loop still indexed armor AUP/rotation/extent lanes, `_receiverTransforms`, and `_targetHeights` by raw `_targetCount`.

Solution: The refresh now checks all required snapshot lanes and clamps its loop count to the shortest actual managed/native length. It no longer depends on a later damage/status job preflight to make its own writes safe.

Rejected Alternatives: Relying on damage preflight even when status calls the refresh first; moving the refresh after locks without proving ownership side effects; silently skipping status armor snapshot updates; allocating a compact transform list.

Scalability potential: Low/Middle avoid rare target snapshot crashes during status ticks. High/Ultra keep accurate AUP/rotation/extents for richer armor debug and presentation without changing LUT truth.

Hardware Impact: 0 us measured. Adds owner-phase min-length clamps before snapshot writes; per-hit armor LUT math is unchanged. Compile/profiler proof remains PENDING until guard and Unity license constraints clear.

## Decision 088 - Armor Torture Jobs Use Proven Target Count

Problem: The cold 10k armor torture proof already checked scratch request/detail/AUP/result buffers, but the mock fill and evaluator jobs still depended on raw `_targetCount` for target-side arrays. If target lanes were malformed, the proof harness could fail by reading shortened target storage instead of proving the LUT evaluator.

Solution: Added `CanUseArmorEvaluatorTargetBuffers(in views, targetCount)`. Mock and torture proof paths now compute `targetCount = max(0, _targetCount)`, validate target ids, flags, heights, armor LUT, AUPs, rotations, half extents, and armor profiles, and pass the proven count into the scheduled jobs.

Rejected Alternatives: Treating cold QA as exempt from bounds proof; relying on target snapshot refresh to imply evaluator safety; clamping only scratch count while leaving target arrays raw; disabling the torture path.

Scalability potential: Low/Middle get a proof harness that fails closed instead of crashing under target churn. High/Ultra retain the same harness for measured overkill validation once Unity licensing permits runtime execution.

Hardware Impact: 0 us hot runtime change. This is cold QA/proof hardening; measured runtime microseconds remain PENDING because Unity batch execution is still blocked.

## Decision 089 - Armor CSV Profile Apply Clamps Runtime Profile Storage

Problem: Editor CSV import writes the same 64B armor profiles used by runtime LUT evaluation. `ApplyCsvProfileToTargets` iterated raw `_targetCount` and used fallback `_maxHealth[i]` / `_armorValues[i]` without proving those actual lanes were long enough.

Solution: The CSV apply path now fails closed when profile storage is missing, clamps iteration to actual `TargetArmorProfiles.Length`, and guards fallback health/armor lane reads by actual lengths before merging the profile.

Rejected Alternatives: Treating editor import as harmless; relying on current MaxTargets allocation; copying profiles into a managed list; allowing partial profile writes without lane proof.

Scalability potential: Low/Middle avoid corrupting runtime armor LUT profiles from editor tooling. High/Ultra keep the same profile import route for richer authored armor without changing DTO layout.

Hardware Impact: 0 us hot runtime change. Editor/cold authoring path only; runtime LUT/CAS unchanged. Compile/profiler proof remains PENDING.

## Decision 090 - Mock Armor Impact Jobs Prove Scratch Lanes

Problem: `GenerateMockArmorImpacts` computed a bounded `count` and proved target-side evaluator buffers, but it scheduled `GenerateMockArmorImpactSignalsJob` without proving `MockDetails`, `MockAups`, and `MockTargetSlots` were created and at least `count` long. A partial Vault rebind could make the cold QA job write past a shortened scratch lane before production `TryQueueDamage` admission had a chance to reject anything.

Solution: Added `CanUseArmorMockSignalBuffers(in views, count)` and call it after `count` is computed and before mock buffer locks and job scheduling. The helper proves `MockRequests`, `MockDetails`, `MockAups`, and `MockTargetSlots` are created and at least `count` long. `Tools/OOP_Hitbox_Scanner.py` now emits `armorMockImpactBufferBoundsProof` and requires the preflight to appear before `GenerateMockArmorImpactSignalsJob` construction.

Rejected Alternatives: Trusting `TryResolveArmorPenetrationVaultViews(... includeMock: true)` to imply every lane length forever; checking only `MockRequests`; relying on job-side index guards while still scheduling malformed scratch storage; allocating managed mock arrays for QA convenience.

Scalability potential: Low/Middle avoid editor/development stress harness crashes during target/vault churn. High/Ultra keep the same cold proof route for large armor validation without changing production LUT truth or DTO layout.

Hardware Impact: 0 us hot runtime change. This is editor/development QA preflight only; production armor LUT evaluation and CAS health subtraction are unchanged. Compile/profiler proof remains PENDING until CPU/compiler guard and Unity license constraints clear.

## Decision 091 - Ballistics Mock Proof Rejects Empty Scratch Lanes

Problem: `GenerateMockBallistics` clamped requested trajectory and primitive counts to buffer lengths, but only checked `IsCreated`. If a scratch lane existed with length `0`, the method could schedule zero work or return success with no generated proof data. That makes the cold ballistics proof harness dishonest under malformed Vault storage.

Solution: The method now fails closed unless both trajectory and primitive lanes are created and non-empty, then rejects zero `safeTrajectoryCount` or `safePrimitiveCount` before constructing `GenerateMockBallisticsJob`. `Tools/OOP_Hitbox_Scanner.py` now emits `ballisticsMockGenerationBoundsProof` and requires the zero-count rejection to happen before job construction.

Rejected Alternatives: Treating zero scheduled iterations as harmless; relying on `math.clamp` when min/max can collapse to zero; reporting success and letting later debug reads find no data; allocating temporary fallback lanes.

Scalability potential: Low/Middle avoid false QA success and editor churn on weak machines. High/Ultra keep the same cold proof path for dense ballistic mock batches without changing production damage authority.

Hardware Impact: 0 us hot runtime change. This is cold editor/manual mock generation only; production solver, armor LUT, and CAS paths are unchanged. Compile/profiler proof remains PENDING until the build guard clears.

## Decision 092 - Ballistics Primitive Registration Rejects Negative Counter Slots

Problem: `RegisterAabbPrimitiveFromRuntime` clamped its search loop to `min(_primitiveCount, primitives.Length)`, but new-slot selection still used `slot = _primitiveCount++`. If `_primitiveCount` was corrupted negative, the route could write `primitives[-1]`. `TombstonePrimitivesForTarget` also trusted the raw count for iteration.

Solution: Registration now computes `capacity = min(primitives.Length, MaxAabbPrimitives)` and `count = min(max(0, _primitiveCount), capacity)`. New-slot allocation uses `nextSlot = max(0, _primitiveCount)`, rejects `nextSlot >= capacity`, then writes `_primitiveCount = nextSlot + 1`. Tombstone iteration clamps negative count to zero. `Tools/OOP_Hitbox_Scanner.py` now emits `ballisticsPrimitiveRegistrationBoundsProof`.

Rejected Alternatives: Assuming `_primitiveCount` can never corrupt; relying on loop skip to protect the subsequent slot write; clamping only the search loop; clearing the entire primitive table on bad count.

Scalability potential: Low/Middle avoid hard crashes in combat AABB registration under corrupted state or partial restore. High/Ultra keep deterministic primitive ownership for dense ballistic solver batches without extra allocation or scene search.

Hardware Impact: 0 us measured. Adds owner-path integer clamps only during primitive registration/tombstone; ballistic intersection jobs, armor LUT evaluation, and CAS health subtraction are unchanged. Compile/profiler proof remains PENDING until the build guard clears.

## Decision 093 - Ballistics Frame Solve Requires Real Buffer Lengths

Problem: `BallisticsRuntime.FrameTick` checked solver lanes for `IsCreated`, then scheduled intersection/VFX/telemetry jobs even if critical lanes had zero length or if the penetration LUT was shorter than the 8x8 table. It also computed telemetry cursor modulo `TelemetryRingLength` instead of actual ring length, and passed raw `_primitiveCount` variants into counter/job payloads.

Solution: Frame scheduling now rejects zero-length trajectory, primitive, hit-result, telemetry, counter, and impact-VFX lanes, requires `penetrationLut.Length >= PenetrationLutLength`, clamps `primitiveCount` once to actual primitive storage, uses actual telemetry ring length for `_activeTelemetryIndex`, and passes the proven primitive count to `ClearCounter`, `BallisticIntersectionJob`, and `BallisticsTelemetryJob`. `Tools/OOP_Hitbox_Scanner.py` now emits `ballisticsFrameSolveBufferPreflightProof`.

Rejected Alternatives: Letting jobs self-skip zero storage; relying on `ResolvePenetrationScalar` fallback for a missing LUT; allowing telemetry to silently skip writes because the computed ring index is outside actual length; using raw `_primitiveCount` in telemetry for convenience.

Scalability potential: Low/Middle avoid scheduling empty or malformed solver jobs and avoid silent missing telemetry. High/Ultra keep deterministic solver batches and accurate proof counters under denser ballistic testing.

Hardware Impact: 0 us measured. Adds owner-phase preflight checks before scheduling; actual solver, LUT sampling, and CAS damage paths are unchanged. Compile/profiler proof remains PENDING until the build guard clears.

## Decision 094 - Damage Ingress AUP Writer Guards Its Own Lane

Problem: `WriteSignalImpactAup` relied on `CanUseDamageIngressSlot` to have proven `SignalImpactAups.IsCreated` before the helper wrote the impact AUP lane. That hidden dependency made the helper unsafe if reused or called after a partial armor Vault rebind.

Solution: `WriteSignalImpactAup` now checks `views.SignalImpactAups.IsCreated` before indexing and sanitizes finite AUP data with `math.select(double3.zero, impactAup, new bool3(IsFinite(impactAup)))`. `Tools/OOP_Hitbox_Scanner.py` now folds the helper's local lane proof and branchless AUP sanitize into `damageIngressBufferBoundsProof`.

Rejected Alternatives: Relying on the caller preflight forever; duplicating comments instead of code; leaving the ternary sanitizer in a route that is already being audited for branchless cheap ingress math.

Scalability potential: Low/Middle avoid rare malformed ingress writes after partial Vault rebinding. High/Ultra keep richer impact AUP metadata for VFX/debug without changing damage truth or DTO layout.

Hardware Impact: 0 us measured. Adds one local helper guard and a branchless select on admission metadata only; armor LUT evaluation and CAS health subtraction are unchanged. Compile/profiler proof remains PENDING until the build guard clears.

## Decision 095 - Damage Queue Proof Uses Balanced Call Parsing

Problem: The project-wide queue route proof used simple regexes for `CombatDamageRuntime.TryQueueDamage(in x)` and `CombatDamageRuntime.TryQueueDamage(in x, in y)`. That was too weak for a large Unity codebase because a multiline call, qualified argument, nested expression, or source formatting change could bypass the proof. The report also had an honesty defect: `ballisticsFrameSolveBufferPreflightProof` reported `PASS` while `clearCounterReceivesProvenPrimitiveCount` was false because the block extractor excluded the method signature.

Solution: Added a balanced-parentheses call parser in `Tools/OOP_Hitbox_Scanner.py` after comment/string stripping. The report now records every external `CombatDamageRuntime.TryQueueDamage(...)` call with argument count, direct-return flag, and negated-admission-gate flag. The ballistics proof now validates the actual `ClearCounter(..., int primitiveCount)` signature from full source. Added top-level `armorProfileLayoutProof` and `shinobuArmorPenetrationTableLayoutProof` aliases so byte-layout proof is directly addressable.

Rejected Alternatives: Keeping the one-line regex and trusting current formatting; treating the `clearCounter` false field as cosmetic because the verdict still passed; moving to Roslyn in this pass despite earlier Unity binding failures; broad edits outside Echelon 5 combat routes.

Scalability potential: Low/Middle avoid false-positive route proof and direct damage bypass regressions. High/Ultra keep the same proof surface while allowing richer combat call sites, as long as they carry explicit detail and AUP metadata.

Hardware Impact: 0 us runtime. Static scanner cost increased from roughly 19-96s to ~125s on this workstation, but it is offline evidence tooling and does not touch the combat hot path. Build proof is blocked by CPU/compiler guard.

## Decision 096 - Legacy Damage Queue Overloads Fail Compile Without AUP

Problem: Current external call sites all used `TryQueueDamage(in signal, in detail, impactAup)`, but `CombatDamageRuntime` still exposed public one-argument and two-argument overloads that silently filled `impactAup` with `double3.zero`. That left a future API route for damage without explicit AUP metadata, contradicting the AUP and damage-ingress proof.

Solution: Marked the one-argument and two-argument overloads with `[Obsolete(..., true)]` and changed the one-argument overload to forward directly to the AUP overload. The scanner now reports `legacyQueueOverloadsCompileFailWithoutAup=true` and the balanced parser proves all 9 external call sites use three arguments.

Rejected Alternatives: Deleting the overloads outright before a full build; leaving runtime scanner detection as the only guard; keeping `[Obsolete(..., false)]` as a warning that could be ignored; forcing all registration-gap fallbacks to synthesize fake AUP instead of requiring explicit metadata at central ingress.

Scalability potential: Low/Middle avoid missing impact metadata in cheap VFX/debug routes. High/Ultra can consume richer impact AUP for overkill feedback without weakening damage truth ownership.

Hardware Impact: 0 us runtime. This is compile-time API hardening; hot LUT/CAS math is unchanged. Full `Assembly-CSharp.csproj` build passed after the guard cleared; only existing `MSB9008` warnings for missing `Hecton8.Input.csproj` remain.

## Decision 097 - Combat Runtime Prewarms In Dispatcher Cold Init

Problem: `TryQueueDamage` still had a practical lazy-runtime path: if no receiver had registered before the first damage signal, `TryQueueDamage` would call `EnsureInitialized()` and allocate native queues/arrays during damage ingress. That violates the intent of zero-GC/cold allocation even though current registered targets usually initialize the route earlier.

Solution: Added `CombatDamageRuntime.Prewarm()` and called it from `SystemDispatcher.InitializeService()` after the dispatcher is registered. `SystemDispatcher` already owns the per-frame `CombatDamageRuntime.FrameTick` call, so this is a cold owner-phase prewarm, not a new hot dependency. `TryQueueDamage` now fails closed without calling `EnsureInitialized()` when the runtime was not prewarmed, so damage ingress cannot allocate native route storage on first hit. Scanner now emits `combatPrewarmProof` and proves `damageIngressRejectsUninitializedWithoutAlloc=true`.

Rejected Alternatives: Keeping the ingress fallback and accepting a lazy first-hit allocation route; relying on first target registration as implicit initialization; adding a new scene object solely to call prewarm; moving allocation into a read accessor.

Scalability potential: Low/Middle avoid first-hit allocation spikes on weak CPUs. High/Ultra keep deterministic combat ingress timing and can spend recovered budget on richer late-frame impact presentation.

Hardware Impact: 0 us steady-state measured. Expected gain is removal of a possible first-hit allocation/stall on weak CPUs. Full `Assembly-CSharp.csproj` build passed after the guard cleared in `00:00:27.54` with `0` errors and `2` existing `MSB9008` warnings for missing `Hecton8.Input.csproj` references.

## Decision 098 - Registration Gaps Use Owner Damage Packets

Problem: Three first-party damage emitters attempted the central LUT/CAS route but ignored `false` when the target was not registered: heat hazard, predator bite, and leviathan strike. Registered targets were protected from queue-rejection fallback, but registration gaps became silent no-damage paths.

Solution: Added owner `DamagePacket` fallbacks for those registration/setup gaps only. `EnvironmentalHazard`, `FaunaBrain`, and `SargassumMicroFaunaBoids` now attempt `CombatDamageRuntime.TryQueueDamage(..., impactAup)` first, return true for registered targets after the queue attempt, and use `ReceiveDamage(in packet)` only when the central route is unavailable. Updated the deferred feedback proof scanner to match the current bounded SignalBus API instead of stale `.Enqueue(...)` text.

Rejected Alternatives: Reintroducing direct `TakeDamage`; falling back on queue rejection for registered targets; dropping damage when registration is absent; widening `CombatDamageRuntime` to own every unregistered legacy object; treating stale proof booleans as cosmetic.

Scalability potential: Low/Middle avoid combat damage no-ops during bootstrap or registration churn without adding hot managed fallback to registered targets. High/Ultra retain central LUT/CAS metadata whenever registration exists, with owner packets only as compatibility fallback.

Hardware Impact: 0 us measured steady-state. Registered hot route is unchanged; fallback work only runs when registration is absent. Build proof is pending because an active `dotnet` process blocked the project build guard.

## Decision 099 - Predator Bite Resolves Registered Player Owner

Problem: `TryQueuePredatorBiteDamage` resolved the combat target id from the attacked `Transform` directly. If the attack target was a child collider under the player, a registered player health owner could be misread as unregistered, pushing valid central damage into owner fallback.

Solution: Resolve `HectonPlayerHealth` from the target hierarchy first, use the health owner `GameObject` for `CombatDamageRuntime.ResolveTargetId`, and use the health owner transform for central and fallback local point calculation when available. Scanner now proves `predatorBiteResolvesPlayerHealthOwner=true`.

Rejected Alternatives: Trusting attack target to always be the player root; registering every child collider as a combat target; accepting owner fallback for registered child-collider hits; scene-wide player lookup in the hot route.

Scalability potential: Low/Middle keep player bite damage on the native LUT/CAS route during collider hierarchy churn. High/Ultra retain richer AUP/local point metadata for impact presentation because central routing is not lost on child targets.

Hardware Impact: 0 us measured. Adds one owner component resolve only in predator attack routing, not pellet/armor hot loops. Build proof is pending because CPU/compiler guard is blocked.

## Decision 100 - Environmental Hazard Resolves Player Health From Parent

Problem: `EnvironmentalHazard.ResolvePlayerHealth()` used runtime context first, but its transform fallback only checked `_playerTransform.TryGetComponent`. If the tracked player transform was a child trigger/collider without `HectonPlayerHealth`, heat damage and toxicity ownership could fail even though the player owner existed in the parent hierarchy.

Solution: Keep the direct component check first, then use `_playerTransform.GetComponentInParent<HectonPlayerHealth>()` as a bounded owner fallback. Scanner now proves `resolvePlayerHealthUsesParentFallback=true`.

Rejected Alternatives: Scene-wide `FindObjectOfType`; requiring every player child collider to carry `HectonPlayerHealth`; routing child-trigger heat directly to owner fallback without preserving central registration; ignoring the gap because runtime context usually exists.

Scalability potential: Low/Middle avoid silent hazard damage loss during bootstrap or prefab hierarchy changes. High/Ultra keep central heat damage/status/AUP route when registration exists.

Hardware Impact: 0 us measured steady-state. The parent lookup only runs when cached/runtime player health is absent. Build proof is pending because active `dotnet` blocks the guard.

## Decision 101 - Player Receiver Reconciles Central CAS Packets

Problem: `CombatDamageRuntime.DispatchResults()` sends player packets after the native CAS path has already resolved `PreviousHealth -> NextHealth`. `HectonPlayerHealth.ReceiveDamage()` re-entered legacy `TakeDamage(packet.Magnitude)`, which let invulnerability frames reject later packets in the same pellet storm and then sync a partial owner value back into the native combat mirror. That is a real correctness gap for 100-pellet shotgun fanout.

Solution: Added `TryApplyAuthoritativeCombatDamagePacket(in packet, out appliedDamage)`. Finite central packets with `PreviousValue > NextValue` now clamp and apply `packet.NextValue` directly to `currentHealth`, bypassing legacy `TakeDamage`, `IsInvulnerable`, and `ExtendInvulnerability`. The central packet path does not call `MarkCombatDamageSyncDirty()` for normal non-death packets, because native CAS storage already owns the final result for the completed batch. Registration-gap packets with zero previous/next still fall through to legacy `TakeDamage(packet.Magnitude)` so bootstrap/fallback hazards preserve owner invulnerability and survival-grace behavior.

Rejected Alternatives: Leaving central packets to legacy `TakeDamage`; blindly ignoring invulnerability inside `TakeDamage` for every caller; syncing every central packet back into native storage during result dispatch; treating the scanner CAS proof as sufficient while the owner receiver could still undo batch results.

Scalability potential: Low/Middle keep shotgun and fragment storms deterministic without managed per-pellet invulnerability side effects. High/Ultra retain the same CAS truth while feedback remains owner-phase and can scale visually through existing signal consumers.

Hardware Impact: 0 us measured. Expected impact is correctness and reduced owner-phase churn during registered player pellet storms: no per-packet invulnerability extension and no intermediate native mirror resync. Runtime/profiler proof remains PENDING because the build guard is blocked.

## Decision 102 - Fauna Receiver Reconciles Central CAS Packets

Problem: `FaunaBrain.ReceiveDamage()` had the same central packet mirror pattern as the player route: the native CAS job already computed `PreviousValue -> NextValue`, then the owner receiver called `TakeDamageFromSource(packet.Magnitude)`. Fauna has no invulnerability gate, so it usually converged to the same final health, but it still performed redundant owner subtraction and called `MarkCombatDamageSyncDirty()` during result dispatch, creating intermediate native mirror writes after the completed CAS batch.

Solution: Added a fauna-local `TryApplyAuthoritativeCombatDamagePacket(in packet, hitPoint, out appliedDamage)`. Finite central packets now clamp and apply `packet.NextValue` to `_currentHealth` directly, bypassing `TakeDamageFromSource` and intermediate native sync. The helper preserves source-position presentation: foveated damage lock, hit flash, immediate hit reaction, parental defense, predator fear, death, and survival-blade wound feedback. Packets without a finite decreasing previous/next snapshot remain legacy fallback and still call `TakeDamageFromSource(packet.Magnitude, hitPoint)`.

Rejected Alternatives: Leaving redundant owner subtraction because fauna usually has no invulnerability; stripping presentation side effects from central packets; syncing every result packet back into native health; changing production CAS result format.

Scalability potential: Low/Middle reduce owner-phase churn during dense fauna pellet/fragment hits while preserving visual combat feedback. High/Ultra keep the same damage truth and can scale richer wound presentation without changing the CAS/LUT route.

Hardware Impact: 0 us measured. Expected benefit is correctness margin and less redundant mirror traffic during registered fauna hit storms. Runtime/profiler proof remains PENDING because the build guard is blocked.

## Decision 103 - Habitat Receiver Removes Duplicate Mirror Sync

Problem: `HabitatIntegrityManager.ReceiveDamage()` called `_baseModule.ApplyDamage(packet.Magnitude)` for central integrity packets and then immediately called `MarkCombatDamageSyncDirty()`. `BaseModule.ApplyDamage` already dispatches the integrity delta back through `HabitatIntegrityManager.DispatchIntegrityChanged`, and that method owns the dirty-sync call. The extra call was redundant mirror traffic during result dispatch.

Solution: Removed the immediate `MarkCombatDamageSyncDirty()` after `_baseModule.ApplyDamage(packet.Magnitude)`. The BaseModule fanout path remains intact: damage still updates module integrity, dispatches normalized integrity changes, handles breach/cascade presentation, and the existing `DispatchIntegrityChanged` method marks the combat mirror once.

Rejected Alternatives: Rewriting BaseModule damage application from X_008; setting BaseModule integrity directly from `packet.NextValue` without reproducing breach/cascade side effects; keeping duplicate sync because habitat hit storms are less common than fauna/player pellet storms.

Scalability potential: Low/Middle avoid avoidable owner-phase mirror writes in structural combat. High/Ultra keep the same habitat breach presentation while damage truth stays on the central packet route.

Hardware Impact: 0 us measured. Expected benefit is small but real in structural hit batches: one fewer native mirror sync per BaseModule central integrity packet. Runtime/profiler proof remains PENDING because the build guard is blocked.

## Decision 104 - Abyssal Thermal Registration Gaps Use Owner Packets

Problem: `AbyssalThermalManager.QueueBoilingDamage()` and `EmitThermalShock()` only attempted registered central combat targets. If a boiling/shock target implemented `IDamageReceiver` but was not registered yet, the damage was silently lost. The route also used `CombatDamageSignalCodec.FromRuntimePoint(positionWS)`, which hides failed AUP resolution as `double3.zero` without local route evidence.

Solution: Registered targets still use `CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup)` and never direct-fallback on queue rejection. If registration is absent, a bounded parent walk resolves `IDamageReceiver` and sends a source-tagged `DamagePacket` with target-local point, thermal damage type, and depth metadata. Central AUP now uses `ResolveCombatImpactAup()`: `TryResolveAupFromRuntimeOrigin`, `AbsoluteUniversePosition.IsFinite()`, and final `double3` finite checks before writing the ingress lane, else `double3.zero` metadata while staying on the central route.

Rejected Alternatives: Dropping unregistered abyssal thermal damage; direct `TakeDamage`; direct fallback after registered queue rejection; keeping weak codec fallback without route-specific proof; widening `CombatDamageRuntime` to know world thermal owner objects.

Scalability potential: Low/Middle avoid missing heat/shock damage during bootstrap or registration churn while registered hot paths remain on the native LUT/CAS route. High/Ultra keep richer AUP/local point metadata for deferred thermal impact presentation.

Hardware Impact: 0 us measured. Registered hot route adds only finite-gated AUP resolution already used by adjacent routes; fallback work runs only when registration is absent. Build proof remains PENDING because CPU guard is blocked.

## Decision 105 - Direct Damage Ingress Uses Signal AUP Bounds

Problem: `SignalBus<CombatDamageSignal>` sanitizes impact AUP with `CombatDamageSignalCodec.IsFiniteAup`, which checks finite values and the project AUP extent. Direct `CombatDamageRuntime.TryQueueDamage(..., impactAup)` routes bypass that sanitizer and the armor ingress helper only checked finite `double3`, so huge-but-finite coordinates could enter the impact AUP lane.

Solution: `HectonCombatRuntime_ArmorPenetration.IsFinite(double3)` now delegates to `CombatDamageSignalCodec.IsFiniteAup(value)`. The existing branchless `math.select(double3.zero, impactAup, new bool3(IsFinite(impactAup)))` write remains intact, but the predicate now matches the first-party signal sanitizer bounds.

Rejected Alternatives: Duplicating the max-extent constant in armor runtime; trusting every direct caller to sanitize correctly; routing all direct callers through SignalBus just to reuse its sanitizer; leaving inconsistent AUP validity between central and global ingress.

Scalability potential: Low/Middle avoid invalid far-coordinate impact metadata corrupting deferred presentation or telemetry. High/Ultra keep richer AUP impact presentation while sharing the same validity contract across ingress routes.

Hardware Impact: 0 us measured. Predicate adds the same simple bound checks already used in SignalBus sanitization; combat LUT and CAS truth math is unchanged. Build proof remains PENDING because CPU/compiler guard is blocked.
