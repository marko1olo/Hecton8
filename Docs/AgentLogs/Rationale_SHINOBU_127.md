# Rationale_SHINOBU_127

Problem: User assigned SHINOBU_127 as ARMOR_PENETRATION_BALLISTICS_EXPERT, but the active `Docs/Tasks/CURRENT_BATCH.md` contains no `<AGENT_PROMPT id="SHINOBU_127">` block. The strict batch protocol requires extracting the exact XML block and using its task count before code.

Solution: Halt implementation and mark the work `[BLOCKED BY DEPENDENCY]`. Status was recorded in `Docs/Tasks/Status_SHINOBU_127.md`; final report will be appended to `Docs/AgentLogs/LOG_SHINOBU_127.md`.

Rejected Alternatives: Inferring a 20-task ballistics scope from archived batch prompts or neighboring agents was rejected. Archived prompts are stale by the batch hygiene rule, and neighboring tasks would contaminate architecture decisions. Coding from the user's paraphrase alone was rejected because the protocol requires exact XML extraction and task-count verification.

Scalability potential: Not implemented. Expected future direction remains math-only Burst AABB trajectory tests, Vault-backed projectile/target DTOs, continuous `GlobalQualityWeight` for substep/ricochet fidelity, and low/middle/high/ultra behavior without binary quality switches.

Hardware Impact: No runtime code changed. Estimated gain remains unverified. Expected target after valid prompt: eliminate projectile `Rigidbody` and hot-path `Physics.Raycast`, replacing them with Burst AABB sweeps and LUT penetration to avoid main-thread PhysX stalls on i3/MX350.

Black Box: Not implemented because no authenticated active task block exists. Future implementation must include a 300-frame fixed-size telemetry ring and dump to `Docs/AgentLogs/Dump_SHINOBU_127.bin` on NaN or invalid ballistic state.

## 2026-05-19 Recheck After Ultra Mandate

Problem: The user supplied an additional `<ULTRA_THINK_POLISH_MANDATE agent_id="[YourID]">`, but it is not the required active `<AGENT_PROMPT id="SHINOBU_127">` block and does not contain the original 20-task matrix. The active `CURRENT_BATCH.md` still has no `SHINOBU_127` prompt after a fresh CLI extraction.

Solution: Keep the task blocked and update status/log evidence. Do not mutate runtime combat/physics/Vault code from inferred requirements. The architecture law says exact XML extraction and task-count verification are mandatory before coding.

Rejected Alternatives: Treating archived `COMBAT_ARMOR_PENETRATION` prompts as current was rejected because batch hygiene forbids old batch logs/prompts unless explicitly ordered. Treating the user's prose as the missing XML was rejected because the mandate itself orders re-reading the original XML assignment and its 20 tasks. Treating neighboring SHINOBU prompts as context was rejected because strict parsing requires deleting non-owned prompt text.

Scalability potential: Future authorized implementation must expose continuous quality weight for ballistic substep count, AABB candidate stride, ricochet resolution richness, telemetry sample cadence, and Dear Lie presentation richness. Low: one swept AABB against coarse local boxes, minimal ricochet. Middle: per-material LUT and limited ricochet. High: additional armor-normal refinement and hydrodynamic drag integration. Ultra: finer deterministic substeps and richer GPU impact/scar data, still no projectile Rigidbody or hot `Physics.Raycast`.

Hardware Impact: No new runtime code. No microsecond savings are claimed. Expected valid scope remains replacing main-thread PhysX projectile/Raycast queries with Burst math over Vault state to reduce i3/MX350 stalls, but proof is pending the real prompt and profiler/GCMonitor data.

## 2026-05-19 Active Prompt Reconciliation

Problem: `CURRENT_BATCH.md` now contains the authenticated `SHINOBU_127` XML block with 20 tasks. The previous blocked state is stale and would prevent authorized work.

Solution: Switch to active implementation. Keep edits inside Echelon 5 ballistics/combat integration: add a Vault-backed ballistics solver, replace the flora projectile physics path, wire combat target AABB registration through the existing Combat Damage Router, and expose editor-only tuning/debug surfaces.

Rejected Alternatives: Keeping the pooled `FloraProjectile` Rigidbody path was rejected because it is explicitly non-deterministic and can tunnel. Calling `target.TakeDamage()` directly was rejected because Agent 41 owns health mutation and existing `CombatDamageSignal` is the typed lane. Editing `SystemID` or global `BufferID` enums was rejected because it would touch core compile-wall surfaces; local numeric `BufferID` casts will be used instead.

Scalability potential: Low uses root AABBs and zero ricochet bounces. Middle admits a deterministic subset of limb AABBs and one bounce. High uses most primitives and two bounces. Ultra evaluates all registered primitives and up to three deterministic ricochets, then spends the saved CPU on richer deferred impact VFX staging rather than physical bullet GameObjects.

Hardware Impact: Expected low-end gain is removal of per-shot PhysX Rigidbody integration and collision callbacks from hostile flora shots. No measured profiler value exists yet. The target is microsecond-scale Burst slab tests over flat DTOs; final numbers remain PENDING VERIFICATION until Unity/Profiler evidence exists.

Black Box: Implementation will allocate a 300-entry `BallisticsTelemetryEntry` ring in the DataVault and dump `Docs/AgentLogs/Dump_BALLISTICS_SURGEON.bin` on NaN or >0.5 ms solver wall-time.

## 2026-05-19 Loop 1 / Tasks 01-05

Problem: Hostile flora was still a physical bullet system: pooled projectile GameObject, fallback `Rigidbody.linearVelocity`, and `FloraProjectile.OnCollisionEnter` as the damage authority.

Solution: `HostileFlora.Shoot()` now queues a mathematical trajectory through `BallisticsRuntime.QueueTrajectoryFromVelocity`. `FloraProjectile` was reduced to a legacy prefab facade that queues a trajectory and despawns immediately. Added explicit 64B `BallisticTrajectoryDTO`, 96B `AABBPrimitiveDTO`, Vault buffer IDs, editor-time layout verification, and `GenerateMockBallisticsJob`.

Rejected Alternatives: Keeping projectile visuals alive and waiting for collision was rejected because it preserves nondeterministic PhysX authority. Deleting `FloraProjectile` entirely was rejected because existing prefabs may still reference it; a compatibility facade avoids broken serialized components while removing runtime collision authority. Adding global `BufferID` enum values was rejected to avoid core compile-wall churn.

Scalability potential: Low-tier shots are just one trajectory DTO queued into a double-buffered Vault array. Middle/high/ultra can add richer deferred VFX and more registered hit primitives without reintroducing GameObject bullets.

Hardware Impact: Expected i3/MX350 gain is removal of per-shot physics body integration and collision callback traffic from flora fire. Exact microseconds remain PENDING VERIFICATION until Unity profiler/GCMonitor.

## 2026-05-19 Loop 2 / Tasks 06-19

Problem: The codebase lacked an owned armor-penetration ballistic kernel. A flora shot could only become damage through a spawned component and collision callback, and designers had no isolated mock/tuning/debug surface for hit math.

Solution: Added `BallisticIntersectionJob` with target-relative AUP subtraction, local-space slab AABB intersection, analytical drag, 8x8 LUT penetration, deterministic ricochet reflection, and typed `CombatDamageSignal` emission. Added double-buffered trajectory Vault handles so new shots can queue while a previous solve is fenced. Added `StageImpactVFXJob`, a 300-frame telemetry ring, cold CSV LUT ingestion, UI Toolkit tuner, and live gizmo facade.

Rejected Alternatives: Completing jobs inside `FrameTick` was rejected because it would stall the main thread. Finalization now uses `DispatcherJobSwap.TryFinalizeCompleted` unless teardown owns the forced barrier. Direct GraphicsBuffer writes from the solver were rejected because this domain should stage unmanaged DTOs and leave upload/draw ownership to the presentation renderer. Writing to core `SystemID`/`BufferID` enums was rejected to preserve compile-wall boundaries.

Scalability potential: Low evaluates root AABBs and zero ricochets. Middle admits a deterministic subset of non-root primitives and one bounce. High admits most primitives and two bounces. Ultra evaluates all registered primitives and three ricochets, then feeds richer impact staging for indirect VFX.

Hardware Impact: Expected low-end gain is replacing PhysX projectile/collision work with flat Burst math and reducing primitive evaluation under low `GlobalQualityWeight`. Expected high-tier value is richer hit/VFX staging while retaining deterministic hit truth. Measurements remain PENDING VERIFICATION because the CPU gate blocked `dotnet build` at 100% processor load.

## 2026-05-19 Loop 3 / Vault Fence Self-Review

Problem: Initial solver locking covered trajectory, primitive, hit, and VFX staging buffers but omitted the LUT, telemetry, and counter buffers read/written by the same job chain.

Solution: Expanded the solver buffer fence to lock/unlock `PenetrationLut`, `TelemetryRing`, and `Counters` alongside the active trajectory buffer, AABB primitives, hit results, and impact VFX staging. This preserves Vault compaction safety for every NativeArray passed into the scheduled job chain.

Rejected Alternatives: Leaving LUT/telemetry unlocked was rejected because a Vault relocation during an active Burst read/write would invalidate raw pointers. Completing jobs immediately to avoid locks was rejected because it violates the non-blocking combat path.

Scalability potential: The wider lock does not add per-trajectory math; it only protects buffer lifetime. Low-to-ultra scalability remains driven by primitive admission and ricochet budget.

Hardware Impact: No measured runtime change. This is correctness hardening against rare compaction/relocation faults.

## 2026-05-19 Loop 4 / Moving Target AABB Refresh

Problem: AABB primitives registered only at target registration would become stale for moving players/fauna/submarines. That would make the mathematical solver deterministic but wrong.

Solution: `CombatDamageRuntime.FrameTick` now refreshes registered target root AABBs only when `BallisticsRuntime.HasPendingTrajectories` is true, immediately before scheduling the solve. This uses existing cached combat target transforms and heights and avoids per-frame target scans when no shots are queued.

Rejected Alternatives: Refreshing all target AABBs every frame was rejected as unnecessary main-thread work. Letting the solver use stale registration-time AABBs was rejected because moving targets would be missed or hit at obsolete positions.

Scalability potential: Low still evaluates root primitives only. Higher tiers can register additional primitives later through the same API without changing the refresh route.

Hardware Impact: Adds an O(active combat targets) main-thread refresh only on frames containing queued shots. This trades a bounded transform read pass for removal of projectile physics authority.

## 2026-05-19 Loop 5 / Verification Fence

Problem: The final proof needed to separate owned ballistics compile health from unrelated repo-wide compile failures. A full build currently fails before SHINOBU_127 code is the deciding factor because Visor and Somatic editor/runtime types are missing in `Hecton8.Core.csproj`.

Solution: Ran static forbidden-pattern scans over the owned ballistic/flora fire path, ran `git diff --check` on owned files, and ran a narrow `Assembly-CSharp.csproj` compile with `BuildProjectReferences=false` after the CPU/dotnet gate was clear. This confirms the modified Assembly-CSharp surface compiles against available referenced outputs while preserving the report that full project verification is blocked by unrelated dependencies.

Rejected Alternatives: Fixing Visor/Somatic missing DTOs was rejected as cross-domain sabotage. Claiming full build health from the narrow compile was rejected; the status records the exact boundary. Running Unity Editor profiling was rejected because no Editor MCP session or playmode profiler evidence was available in this turn.

Scalability potential: The implemented quality path is continuous: low-tier collapses to root AABB with no ricochet budget, middle-tier admits a deterministic subset and one bounce, high-tier admits most primitives and two bounces, ultra admits all registered primitives and three bounces while feeding richer deferred impact VFX data.

Hardware Impact: Expected low-end gain remains removal of projectile Rigidbody integration, collision callbacks, and engine ray queries from the owned flora/ballistics path. Exact microseconds are not claimed as measured; profiler proof remains pending Unity runtime instrumentation.

## 2026-05-19 Loop 6 / Ultra Polish Audit

Problem: The first green owner-local compile still had several architectural risks: editor gizmo reads could touch job-owned Vault buffers, same-frame target AABB refresh could happen before a completed solver was finalized, impact VFX matrices used target-local hit coordinates as runtime translation, direct SignalBus emission had no per-solve pressure budget, primitive tombstoning lacked an explicit mutation guard, active-buffer telemetry pointed at the post-swap write buffer, and `DamageWriter` carried an unnecessary safety-disabling attribute.

Solution: Added `BallisticsRuntime.PrepareFrameForTargetRefresh()` so `CombatDamageRuntime` non-blockingly finalizes completed solver work before mutating target AABBs. `TryGetDebugBuffers` now fails closed if a job still owns the buffers. `StageImpactVFXJob` now receives `PresentationOriginAUP` and converts `HitAUP` into runtime-space for impact matrices. `BallisticIntersectionJob` now consumes a continuous quality-derived signal budget from 16 to 128, sets `BallisticHitFlags.SignalDropped` on overflow, and telemetry accounts for dropped signals. `TombstonePrimitivesForTarget` now uses the Vault mutation guard. Active-read buffer telemetry is resolved before the write buffer advances. The unsafe container safety suppression on `DamageWriter` was removed.

Rejected Alternatives: Forcing `JobHandle.Complete()` from the gizmo or every AABB refresh was rejected because it would make editor/debug or target movement a hidden gameplay stall. Leaving damage emission unbounded was rejected because a mock firefight could inflate the NativeQueue and shift pressure into the Combat Damage Router. Direct `GraphicsBuffer` upload from ballistics was rejected again; the combat domain owns hit truth and unmanaged staging, not presentation upload. Fixing the unrelated Visor/Somatic missing DTOs was rejected as cross-domain sabotage.

Scalability potential: Low quality now budgets only 16 damage signals per solve, evaluates root-biased primitives, and suppresses ricochet work. Middle increases admitted primitives and signal throughput gradually. High and ultra move toward 128 emitted signals, full primitive admission, and richer impact staging, still without Rigidbody bullets or CPU decal instantiation.

Hardware Impact: The polish changes are primarily correctness and pressure control. Expected low-end protection is avoiding unbounded signal queue growth and avoiding forced main-thread barriers from debug/AABB refresh paths. Exact microseconds remain unmeasured; latest owner-local compile proof after this pass was 0 warnings and 0 errors in 37.22s with `BuildProjectReferences=false`.

## 2026-05-19 Loop 7 / Determinism and Facade Audit

Problem: Damage signals were emitted directly from `BallisticIntersectionJob`, an `IJobParallelFor`. `NativeQueue.ParallelWriter` is safe for concurrent writes, but enqueue order is scheduler-dependent and therefore weak for rollback truth. The previous quality budget also used trajectory index, not actual hit emission count, so sparse high-index hits could be dropped even when the signal budget was not exhausted. The editor tuner telemetry readout used string concatenation and `ToString`, violating the stated Task 17 zero-GC facade intent. The debug gizmo returned the write buffer count after a solve, so it could display unsolved pending trajectories against solved hit results.

Solution: Split hit truth from signal emission. `BallisticIntersectionJob` now writes only deterministic hit results, including a new `ImpactDirection` field for ricochet-correct impulse direction. A new deterministic `EmitBallisticDamageSignalsJob` scans hit results in stable trajectory index order after intersection, enqueues up to the continuous quality-derived budget, and tags overflow with `SignalDropped` for telemetry. `BallisticHitResultDTO` was expanded to an explicit 112-byte layout and the editor verifier now checks its size and `ImpactDirection` offset. The editor tuner telemetry was converted to numeric UI Toolkit fields updated with `SetValueWithoutNotify` instead of per-refresh string formatting. The debug gizmo now prefers the last solved active trajectory buffer/count so trajectories and hits describe the same frame.

Rejected Alternatives: Keeping parallel signal enqueue was rejected because combat damage ordering could drift between hosts. Adding atomics to count emitted hits inside the parallel job was rejected because it adds contention and still risks nondeterministic ordering. Reconstructing ricochet impact direction from the original trajectory was rejected because it loses post-ricochet reflection truth. Leaving the editor readout as managed strings was rejected because Task 17 explicitly asks for a zero-GC readout surface, even though it is editor-only.

Scalability potential: Low quality still caps signal emission near 16, but now those 16 are the first deterministic valid hits, not the first 16 trajectory indices. Middle/high/ultra scale continuously toward 128 signals and richer VFX staging while retaining deterministic order.

Hardware Impact: The serial emission job adds an O(trajectory count) scan after the parallel solve. At the current 4096 trajectory cap this is bounded and cache-linear; it buys deterministic signal order and removes parallel queue storm behavior from the intersection kernel. Latest owner-local compile proof after this pass was 0 warnings and 0 errors in 27.83s with `BuildProjectReferences=false`.

## 2026-05-19 Loop 8 / Primitive Churn and AUP Mock Audit

Problem: Primitive tombstoning only cleared flags; later registrations appended until `_primitiveCount` reached the Vault cap, so long target churn could exhaust `AabbPrimitives` even with inactive slots available. Mock firefight data was authored around AUP zero, which weakens the 100km floating-origin regression surface. The exact rotated slab path paid inverse-rotation and slab math for primitives that a conservative sphere check could reject before the expensive part. Runtime AABB half extents trusted caller sign and could collapse negative magnitudes to the minimum hull.

Solution: `RegisterAabbPrimitiveFromRuntime` now records the first inactive slot and reuses it before appending. Runtime half extents are sanitized with `math.abs` then min-clamped. `GenerateMockBallisticsJob` receives `MockOriginAUP` from `HectonFloatingOrigin.CurrentTotalOffsetDouble` and builds mock target/trajectory AUPs around the current floating origin. `TryIntersectPrimitive` now performs a conservative bounding-sphere range/perpendicular rejection in target-relative float space before inverse rotation and exact slab intersection.

Rejected Alternatives: Compacting the primitive array on every tombstone was rejected because it would mutate broad sections of Vault state and complicate registered primitive identity. Leaving mock data near world zero was rejected because it does not test the AUP law at map edges. Replacing the exact slab with sphere collision was rejected because it would turn armor hitboxes into inflated approximations; the sphere is only an early-out, never the final truth.

Scalability potential: Low/middle quality benefits most because many limb or distant primitives are rejected before quaternion math. High/ultra still reaches exact rotated AABB truth for admitted primitives and can spend saved ALU on richer staged impact presentation.

Hardware Impact: Expected low-end gain is reduced per-primitive math on misses and prevention of long-session primitive-cap exhaustion. No Unity profiler microseconds are claimed. Static scans stayed clean, and owner-local compile passed with 0 warnings and 0 errors in 31.64s after a 26.5% CPU/no-dotnet gate.
