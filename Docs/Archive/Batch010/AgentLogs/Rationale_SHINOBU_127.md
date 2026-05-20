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

## 2026-05-19 Loop 9 / Lifecycle and CSV Authority Audit

Problem: Static runtime counters could outlive their Vault owner if tests or bootstrap replaced `GlobalRegistry.DataVault`, leaving stale primitive counts, active read counts, telemetry cursors, and debug state pointing conceptually at the old buffer set. VFX staging had no public bounded accessor, so a future presentation owner could scan stale entries past the current solve. The CSV parser computed FNV-1a hashes but did not use them to map weapon/material names, making row/column reordering unsafe. The editor layout verifier did not check the VFX staging DTO or tuning DTO.

Solution: `EnsureInitialized` now detects Vault instance changes, force-finalizes any old job, unlocks old solver buffers, and resets transient static counters before acquiring handles from the new Vault. `Shutdown` now uses the same transient reset. Added `TryGetImpactVfxStaging` to expose the staged impact buffer with a frame and count bounded by `BallisticsCountersDTO.TrajectoriesProcessed`. Reworked the CSV parser to map named weapon rows and material headers through FNV-1a hash constants, including CRLF skip handling. Expanded `BallisticsLayoutVerifier` to verify `BallisticImpactVfxDTO` and `BallisticsTuningDTO` sizes/offsets.

Rejected Alternatives: Clearing all 4096 VFX DTOs every frame was rejected because stale reads are better solved by a count/frame contract. Leaving CSV positional-only was rejected because designers can reorder spreadsheets without compiler feedback. Requiring a project-wide enum change for the staging route was rejected again to preserve the compile wall. Running build at 82.9% CPU was rejected under the hardware-protection rule; the build was delayed until CPU dropped to 30.9%.

Scalability potential: Low-tier presentation can read only the bounded staging count and skip stale slots. Middle/high/ultra can consume the same deterministic DTO stream for richer GPU impact presentation without changing combat truth. CSV tuning stays cold and human-readable without per-frame parsing.

Hardware Impact: Expected protection is fewer stale presentation scans and safer test/bootstrap resets. No Unity profiler microseconds are claimed. Static scans stayed clean; owner-local compile passed with 0 warnings and 0 errors in 24.16s after the CPU gate opened.

## 2026-05-19 Loop 10 / Transactional CSV Fail-Closed Audit

Problem: `TryLoadPenetrationCsv` read at most `CsvScratchBytes` bytes into the Vault scratch buffer and then parsed whatever was present. A designer CSV larger than the scratch buffer could be silently truncated and accepted as a partial LUT. `ApplyPenetrationCsvBytes` also wrote directly into the live Vault LUT while parsing, so a malformed file could leave the current penetration table partially mutated even when the parser returned failure.

Solution: The cold loader now rejects files that exceed the Vault scratch capacity before parsing. The span parser now copies the current 64 LUT values into a stack span, parses into that transaction buffer, validates 8 parsed data rows, validates all 8 header columns when a named header is present, rejects malformed header/data tokens, and commits the 64 floats to the Vault LUT only after the whole 8x8 matrix is valid. The stable binary/payload ledger now records SHINOBU_127 Vault buffer IDs `71270..71279`, primary DTO stride, CSV authority, and proof boundary.

Rejected Alternatives: Increasing `CsvScratchBytes` was rejected because it hides the failure mode and grows Vault payload without a concrete authoring need. Mutating the live LUT row by row was rejected because a bad CSV would create half-old, half-new penetration truth. Falling back to managed CSV parsing was rejected because cold authoring still does not need strings or heap collections here.

Scalability potential: Low/middle/high/ultra runtime behavior is unchanged because runtime penetration remains a flat O(1) LUT lookup. The polish protects the human-tuning path so all tiers receive the same validated matrix instead of tier-dependent accidental designer corruption.

Hardware Impact: Hot frame impact is 0 by design; this path is cold/editor/manual load only. Static scans show the owned projectile path remains free of `Rigidbody`, `OnCollisionEnter`, `Physics.Raycast`, `.linearVelocity`, and `Instantiate(`. Owner-local compile is pending because the CPU gate read 65% and the build rule forbids launching `dotnet build` above 50% load.

## 2026-05-19 Loop 11 / Compile-Wall Using Audit

Problem: `BallisticsRuntime.cs` imported `Hecton8.World` even though its AUP calls resolve through `Hecton8.Core.HectonFloatingOrigin`. In a future asmdef split that unnecessary using would look like a sibling-domain dependency and weaken compile-wall evidence.

Solution: Removed the unused `using Hecton8.World;` from the owned ballistics runtime. Existing `CombatDamageRuntime` and `HostileFlora` world references were not touched because they are broader pre-existing gameplay/player-target integration points outside this narrow ballistics runtime cleanup.

Rejected Alternatives: Editing World contracts or rewriting existing target-resolution code was rejected as cross-domain churn. Replacing `HectonFloatingOrigin` itself was rejected because it is the current core AUP authority and already lives in the Core namespace.

Scalability potential: No runtime math changed. Low/middle/high/ultra behavior remains the same continuous quality curve; this pass only tightens dependency evidence for future assembly isolation.

Hardware Impact: No microseconds claimed. CPU gate recheck read 74% with no `dotnet`/`csc`, so owner-local compile remained pending under the project hardware-protection rule. A later gate opened at 25%, but the narrow build failed before SHINOBU-owned code on unrelated missing archive files still referenced by `Assembly-CSharp.csproj`: `Assets/_Project/_Archive/HectonWaterPhysics.cs` and `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs`.

## 2026-05-19 Loop 11 / Compile Proof Blocker

Problem: After CPU dropped below the hardware gate, the owner-local `Assembly-CSharp.csproj` compile did not reach the ballistics edits. `csc` failed with CS2001 because `Assembly-CSharp.csproj` still includes two deleted archive files: `Assets/_Project/_Archive/HectonWaterPhysics.cs` and `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs`.

Solution: Classify the compile as `[BLOCKED BY DEPENDENCY]`, not as a SHINOBU_127 code failure. Preserve the unrelated deletion state. Continue static owner-surface verification instead of restoring deleted archive files or editing project metadata outside the ballistics domain.

Rejected Alternatives: Restoring the deleted archive scripts was rejected because they are unrelated `_Archive` files and existing git deletions were not made by this agent. Editing `Assembly-CSharp.csproj` was rejected because it is generated/project-wide metadata and not a SHINOBU_127 domain file.

Scalability potential: Runtime scalability is unchanged. The failure is build-graph hygiene outside the math solver and does not change low/middle/high/ultra ballistic behavior.

Hardware Impact: Compile-wall impact only. No gameplay microseconds claimed. The failed command consumed 22.92s wall time and produced only the two unrelated CS2001 missing-file errors.

## 2026-05-19 Loop 12 / Static Solver Safety Audit

Problem: With owner-local compile blocked by unrelated deleted archive files, the remaining useful work is evidence-based static verification of the SHINOBU-owned solver surface. A prior broad `rg` command returned repository-wide package/doc noise, so its output could not be used as proof for the owned projectile path.

Solution: Re-ran targeted scans with explicit paths for `BallisticsRuntime.cs`, `BallisticsEditorFacade.cs`, `HostileFlora.cs`, and `FloraProjectile.cs`. The owned projectile path has no `Rigidbody`, `OnCollisionEnter`, `Physics.Raycast`, `.linearVelocity`, or `Instantiate(`. `BallisticsRuntime.cs` has no hot LINQ/foreach/UnityEngine.Random/Time.deltaTime/native container allocation markers. The editor facade has no `string.Format`, `ToString`, `foreach`, native allocation, PhysX, or projectile-instantiation markers in the checked set. The solver graph remains `BallisticIntersectionJob -> EmitBallisticDamageSignalsJob -> StageImpactVFXJob -> BallisticsTelemetryJob`; the signal writer is now used from a serial `IJob`, not from the parallel intersection kernel.

Rejected Alternatives: Treating global third-party/package hits as SHINOBU-owned violations was rejected because the domain boundary is the ballistics/combat projectile path. Running another `dotnet build` was rejected because `Assembly-CSharp.csproj` still references `Assets/_Project/_Archive/HectonWaterPhysics.cs` and `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs`, and both files are deleted by unrelated work. Restoring or editing those files/project entries was rejected as cross-domain state mutation.

Scalability potential: Low quality still collapses work through root-biased AABB admission, zero or minimal ricochet budget, and a 16-signal deterministic emission cap. Middle/high tiers admit more primitives and signals continuously. Ultra keeps full primitive admission and up to three ricochets, while impact presentation remains deferred unmanaged DTO staging.

Hardware Impact: No new microseconds claimed. The static audit protects the i3/MX350 path from accidental reintroduction of PhysX bullet bodies, main-thread raycasts, hot managed iteration, or unbounded signal ordering. Compile proof remains blocked by unrelated missing archive files, not by SHINOBU code.

## 2026-05-19 Loop 13 / Cold Vault Mutation Hardening

Problem: Several cold/editor mutation routes resolved Vault buffers without the same explicit lifetime discipline used by the gameplay solver. `WriteTuning` wrote the tuning DTO without a mutation guard. `GenerateMockBallistics` wrote trajectory and AABB buffers for manual profiling without locking those buffers. `TryLoadPenetrationCsv` locked neither CSV scratch nor LUT, and read byte-by-byte before applying the transactional parser.

Solution: `WriteTuning` now wraps the tuning DTO write with `TryAcquireMutationGuard`/`ReleaseMutationGuard`. `GenerateMockBallistics` now locks the current write trajectory buffer and `AabbPrimitives`, runs the Burst mock job as a cold/manual sync, unlocks both buffers in `finally`, and clears `_activeReadCount` so debug gizmos do not pair stale solved hits with new unsolved mock input. `TryLoadPenetrationCsv` now locks `CsvScratch` and `PenetrationLut`, rejects absent/empty/oversized files before parse, reads through a `Span<byte>` over the Vault scratch buffer, and commits the validated LUT under the mutation guard.

Rejected Alternatives: Leaving cold paths unguarded was rejected because editor/manual mutation can still race with Vault compaction or debug reads. Completing or blocking normal gameplay jobs for every tuning or CSV path was rejected; those routes fail closed while `_jobScheduled` is true. Restoring unrelated `_Archive` source files or editing `Assembly-CSharp.csproj` to obtain a new compile proof was rejected as cross-domain state mutation.

Scalability potential: Runtime low/middle/high/ultra ballistic math is unchanged. This pass protects the human tuning and mock profiling surface so all quality tiers consume validated Vault state rather than partial LUT/tuning/mock writes. Low-tier still uses root-biased admission and low signal budget; high/ultra still spend saved CPU on richer VFX staging.

Hardware Impact: Hot-frame impact is 0 by design because the changes are cold/editor/manual routes. The useful low-end protection is avoiding undefined Vault aliasing or stale debug state during profiling/tuning. Static scans remained clean for projectile PhysX/raycast authority. Compile proof remains blocked by unrelated missing archive files referenced by `Assembly-CSharp.csproj`.

## 2026-05-19 Loop 14 / Sqrt-Free Broadphase and Vault Lock Rollback Audit

Problem: The conservative AABB reach broadphase still paid `math.sqrt(radiusSq)` before exact rotated slab intersection. That is a hot primitive-candidate cost with no need for an actual radius value. Separately, the solver buffer lock chain previously had a blanket unlock fallback risk: if a later Vault lock failed, unlocking every expected buffer would be unsafe in a count-based lock table when this call did not acquire every lock.

Solution: The broadphase now stays in squared space. It rejects primitives behind the segment or past the segment by comparing squared projection/excess values against `radiusSq`, then uses squared perpendicular distance against `radiusSq` before the exact rotated slab test. `TryLockSolverBuffers` now stores per-buffer acquisition booleans and only calls `TryUnlockBuffer` for buffers actually locked in that attempt.

Rejected Alternatives: Keeping `math.sqrt` was rejected because the exact slab test does not need a radius magnitude, only conservative rejection. Using blanket unlock on failure was rejected because lock counters are not proof of ownership for buffers never acquired by this call. Launching another `dotnet build` was rejected because the user explicitly said not to launch build until needed and the project file still references deleted unrelated `_Archive` source files.

Scalability potential: Low-tier frames benefit most from cheaper broadphase misses because primitive admission is already root-biased and signal budget is low. Middle/high/ultra keep exact rotated AABB truth after admission and spend saved CPU on higher primitive admission, ricochet budget, and deferred VFX staging through continuous `GlobalQualityWeight`.

Hardware Impact: Expected gain is one avoided square root on admitted primitive miss candidates; no Unity profiler microseconds are claimed. Static proof found no `math.sqrt` in owned ballistics/flora files, only guarded `math.rsqrt` normalization helpers. The Vault lock change is correctness hardening, not a frame-time claim. Compile proof remains blocked by unrelated deleted archive files referenced by `Assembly-CSharp.csproj`.

## 2026-05-19 Loop 15 / Solver Buffer Naming and Division Hygiene

Problem: The double-buffer phase naming still contained a trap: `ResolveReadBufferId()` returned the current write buffer ID because that buffer becomes the solver read buffer only after scheduling state flips. That name was technically survivable but hostile to future Vault lock maintenance. The queue and solver math also still contained avoidable raw divisions or implicit square-root paths: `Vector3.magnitude`, `velocity / speed`, float division in default LUT seeding, power-of-two integer divides in mock generation, and a reciprocal written as `1f / safeDirection`.

Solution: Renamed the private helper to `ResolveWriteBufferId()`, renamed the frame-local trajectory array to `solverTrajectories`, and renamed lock rollback state from `readLocked` to `trajectoryLocked`. Replaced velocity magnitude/division with `math.lengthsq` plus guarded `math.rsqrt`; replaced slab inverse with `math.rcp` after signed epsilon selection; changed default LUT seeding to reciprocal multiplication; changed mock grid coordinate extraction to bit masks/shifts; changed limb admission hash scaling to a reciprocal constant.

Rejected Alternatives: Leaving phase-ambiguous naming was rejected because the next lock edit could accidentally fence the wrong trajectory buffer. Keeping `Vector3.magnitude` and raw division was rejected because this route is the compatibility entry for legacy projectile prefabs and still runs on gameplay shots. Launching a compile was rejected because the user explicitly constrained build use and the known unrelated `_Archive` source references still prevent owner-local compile proof.

Scalability potential: Low-tier frames benefit from cheaper shot queue normalization and cheaper primitive-admission math. Middle/high/ultra behavior is unchanged in outcome; saved ALU remains available for richer primitive admission, ricochet budget, and deferred VFX staging through `GlobalQualityWeight`.

Hardware Impact: No measured microseconds are claimed. Static arithmetic proof improved: owned runtime no longer has `Vector3.magnitude`, `math.sqrt`, or hot raw vector division in the ballistic queue/solver path. The remaining slash scan hit is the cold CSV parser division guarded by `math.max(divisor, 1f)` plus an editor header string.

## 2026-05-19 Loop 16 / Queue Roundtrip and Static-Proof Noise Audit

Problem: Loop 15 still left two proof irritants and one small hot API inefficiency. `QueueTrajectoryFromVelocity` normalized to a `float3`, reconstructed a Unity `Vector3`, then `QueueTrajectoryFromRuntime` converted that value back to `float3`. The remaining arithmetic scan hits were a cold CSV parser division and a legacy inspector string `VFX / Audio`, which made the grep output less useful as a future regression tripwire.

Solution: Added `QueueResolvedTrajectoryNoMarker` as the shared writer for already-resolved `float3` directions. `QueueTrajectoryFromRuntime` still accepts the existing `Vector3` API, but `QueueTrajectoryFromVelocity` now passes `velocity3 * invSpeed` directly to the writer. Replaced cold CSV fractional division with `fraction * math.rcp(math.max(divisor, 1f))`. Renamed the legacy inspector header to `VFX and Audio` so owned arithmetic grep output is zero-noise.

Rejected Alternatives: Adding a new public overload and forcing call sites to migrate was rejected because `HostileFlora` and prefab compatibility already enter through the velocity facade. Keeping the CSV slash as "cold enough" was rejected because the project uses static scans as proof gates, and a noisy clean-up target can hide future hot divisions. Reverting legacy prefab fields was rejected because serialized compatibility matters; only the header text changed.

Scalability potential: Runtime behavior is unchanged. Low quality still benefits most from cheap queueing, root-biased primitive admission, low signal budget, and low ricochet budget. Middle/high/ultra still scale through `GlobalQualityWeight` toward richer primitive admission, ricochets, and deferred impact VFX staging.

Hardware Impact: No measured profiler value is claimed. Expected impact is tiny: one Unity `Vector3` direction reconstruction and one float3-to-Vector3-to-float3 route are removed from the legacy velocity queue path. Static arithmetic proof now returns no owned slash/sqrt/magnitude matches in the checked runtime/projectile files.

## 2026-05-19 Loop 17 / Burst Job Raw-Pointer Iteration Audit

Problem: The hottest intersection job already used unsafe refs for trajectory, hit result, and primitive scan data, but several scheduled jobs still used `NativeArray[index]` in their bodies. The authenticated XML says the solver must iterate raw pointers and mutate data directly, so leaving mixed indexer access in scheduled jobs weakened Task 03 and Task 20 evidence.

Solution: Converted the remaining scheduled job writes/reads to `NativeArrayUnsafeUtility` pointer access plus `UnsafeUtility.AsRef<T>`. `GenerateMockBallisticsJob` writes mock trajectories/primitives through refs. `BallisticIntersectionJob` now reuses the primitive pointer across bounce loops, copies the hit primitive through a readonly ref, and reads the penetration LUT through a `float*`. `EmitBallisticDamageSignalsJob`, `StageImpactVFXJob`, and `BallisticsTelemetryJob` now use refs for hit result mutation, VFX staging, telemetry ring write, and counter write.

Rejected Alternatives: Keeping `NativeArray[index]` in scheduled jobs was rejected because the XML specifically asked for raw pointer iteration and because indexer access can hide extra bounds/safety machinery in the proof story. Passing raw pointers as job fields with `NativeDisableUnsafePtrRestriction` was rejected because it would weaken container lifetime safety; retaining `NativeArray` fields with `[NoAlias]` and resolving unsafe refs inside `Execute` preserves the scheduled job dependency model.

Scalability potential: Runtime quality behavior is unchanged. Low/middle/high/ultra still scale via primitive admission, ricochet budget, signal budget, and deferred VFX richness. The pointer pass makes that same math path more explicit for Burst and ARM64 cache reasoning.

Hardware Impact: No profiler microseconds are claimed. Expected benefit is reduced abstraction overhead in scheduled job bodies and stronger proof that hot mutation is ref-based over Vault-backed unmanaged arrays. Static indexer scan now reports only the main-thread queue write, not the Burst job loops.

## 2026-05-19 Loop 18 / NaN Normalization Fatalism Audit

Problem: The mock firefight job still used `math.normalize(...)`, which hides an unguarded rsqrt even though the authored vector has a z component of 1. The legacy flora velocity clamp also used `math.rsqrt(speedSq)` after branch validation but without the literal denominator guard demanded by the NaN fatalism rule.

Solution: Replaced mock trajectory normalization with `BallisticsRuntime.NormalizeOrDefault(...)`, which checks finite input and guards `math.rsqrt` with `math.max(lengthSq, Epsilon)`. Changed the legacy velocity clamp to `math.rsqrt(math.max(speedSq, 0.000001f))`.

Rejected Alternatives: Keeping `math.normalize` was rejected because future edits can accidentally remove the non-zero z assumption. Treating the velocity branch as enough was rejected because static proof should show denominator protection at the call site. Removing the legacy facade entirely was rejected because prefab compatibility remains useful while damage authority is already mathematical.

Scalability potential: Runtime behavior is unchanged for valid data. Low/middle/high/ultra tiers still scale through `GlobalQualityWeight`; this pass prevents bad mock or legacy inputs from becoming a NaN source across all tiers.

Hardware Impact: No measured microseconds are claimed. This is stability hardening. Static scan now has no owned `math.normalize` and every owned `math.rsqrt` match is visibly guarded by `math.max`.

## 2026-05-19 Loop 19 / Trajectory Queue Raw Write Audit

Problem: After Loop 17, scheduled jobs were pointer/ref-based, but the main-thread queue writer still assigned `writeTrajectories[index] = trajectory`. That is not a Burst loop, but it is still the hot ingress point for every ballistic shot and the last SHINOBU-owned trajectory buffer indexer.

Solution: Replaced the queue assignment with `NativeArrayUnsafeUtility.GetUnsafePtr(writeTrajectories)` plus `UnsafeUtility.AsRef<BallisticTrajectoryDTO>` at the exact 64-byte trajectory stride. The queue writer now follows the same Vault-backed raw DTO mutation pattern as the scheduled jobs.

Rejected Alternatives: Leaving the indexer because it is main-thread code was rejected; the XML asks for raw pointer mutation over the trajectory DTO array and the queue path is part of that data route. Adding persistent raw pointer fields was rejected because the Vault can relocate buffers; pointers are resolved only at the mutation point under the current buffer handle.

Scalability potential: Runtime quality behavior is unchanged. Low/middle/high/ultra still scale through `GlobalQualityWeight`; this pass only makes trajectory ingress structurally consistent across tiers.

Hardware Impact: No measured profiler value is claimed. Static proof improves: owned trajectory/primitive/hit/VFX/telemetry/counter/LUT indexer scan now returns no matches in `BallisticsRuntime.cs`.

## 2026-05-19 Loop 20 / Compile-Wall Fire-Path Purge

Problem: The SHINOBU-touched fire path still had a legacy direct import/reference surface in `HostileFlora`: an unused `using Hecton8.Audio` and a direct `Hecton8.World.WorldRuntimeReferenceUtility.TryResolvePlayerTransform(...)` call for target acquisition. The ballistic runtime itself was clean, but the touched firing component still weakened compile-wall evidence.

Solution: Removed the dead audio namespace import. Replaced the World utility call with the existing Core registry route: `GlobalRegistry.Player` and `IPlayerRuntimeContext.PlayerTransform`. This uses an existing Core contract instead of inventing a new sibling dependency.

Rejected Alternatives: Adding a new player-target contract was rejected because Core already exposes the player runtime context. Keeping the World utility fallback was rejected because this component is now part of the mathematical firing path and should not carry a sibling World reference. Editing World runtime utility or global registry headers was rejected as unnecessary cross-domain churn.

Scalability potential: Runtime quality behavior is unchanged. Target acquisition remains a cold/slow-tick component concern; ballistic hit truth still scales through `GlobalQualityWeight` inside the Vault-backed solver.

Hardware Impact: No measured microseconds are claimed. This is compile-wall hygiene and dependency surface reduction. Static scan over the touched fire path now shows only Core/Core.Contracts/Core.Memory Hecton8 dependencies.

## 2026-05-19 Loop 21 / Flora Fire-Source Determinism Audit

Problem: `HostileFlora` still used `_shotSeed` for two separate facts: spread RNG and ballistic source identity. The spread scalar came from a custom hash-to-float helper, not `Unity.Mathematics.Random`, and the slow-tick fire cadence subtracted a stale `0.5f` even though the dispatcher contract documents a configured 10 Hz slow cadence. Aim interpolation used the same stale scalar and a mixed `Mathf.Clamp` call.

Solution: Added a distinct `_sourceEntityId` seeded through Core `GlobalSignals.FoldEntityIdToSourceId(EntityId.ToULong(...))`; `FloraProjectile` now uses the same fold for legacy facade shots. Added `BallisticsRuntime.ResolveNextSimulationFrameCounter()` so the fire path can seed spread against the next deterministic ballistic solve frame without referencing dispatcher internals. `ResolveShotSpreadAngle` now derives a 1 km sector hash from AUP, combines sector hash + next ballistic frame + source salt, and uses `Unity.Mathematics.Random.NextFloat(-spread,+spread)`. Cooldown and aim interpolation now consume `NominalSlowTickSeconds = 0.1f`; pitch clamp uses `math.clamp`.

Rejected Alternatives: Keeping `_shotSeed` as `SourceEntityID` was rejected because damage provenance and random salt are different owners. Keeping custom hash-to-float was rejected because the active mandate requires Unity.Mathematics RNG for deterministic random state. Reading `Time.frameCount` or `Time.deltaTime` was rejected; the route uses the ballistic domain's own next simulation frame. Editing `SystemDispatcher` or `ISlowTickable` to pass deltas was rejected as cross-domain compile-wall churn. Keeping the stale `0.5f` slow-tick scalar was rejected because it makes a 2 second cooldown fire in roughly 0.4 seconds under the current 10 Hz dispatcher lane.

Scalability potential: Runtime solver quality is unchanged. Low/middle/high/ultra ballistic cost remains controlled by `GlobalQualityWeight` after the trajectory enters the Vault. This pass makes the shot ingress deterministic and source-clean before the quality-scaled solver consumes it.

Hardware Impact: No profiler microseconds are claimed. Expected impact is correctness: stable damage source provenance, deterministic spread seed surface, and corrected slow-tick authored fire rate. Static verification found no `UnityEngine.Random`, `Random.`, `Mathf.`, `Time.deltaTime`, `Time.time`, or `Time.frameCount` in the owned fire path.

## 2026-05-19 Loop 22 / Rollback-Local RNG State Purge

Problem: Loop 21 still left `_shotOrdinal++` in `HostileFlora`. It was deterministic in forward play, but it is mutable MonoBehaviour state and there was no proof that rollback rewinds it with the ballistic DTO ring. That made the spread seed partially outside the owner-local simulation proof.

Solution: Removed `_shotOrdinal`. Flora spread is now a pure function of folded source ID salt, AUP-derived sector hash, and `BallisticsRuntime.ResolveNextSimulationFrameCounter()`. Same flora, sector, and simulation frame produce the same angle after rollback without requiring a local counter rewind.

Rejected Alternatives: Serializing `_shotOrdinal` into a new combat DTO was rejected as unnecessary state expansion. Keeping the counter because slow tick normally fires once per cooldown was rejected because `ForceShoot` and rollback recovery still need proof rather than convention. Using Unity `Time.frameCount` was rejected again; the ballistic domain frame is the local authority for this path.

Scalability potential: Runtime quality behavior is unchanged. Low/middle/high/ultra still scale after queue ingress via `GlobalQualityWeight`; this pass only removes a non-rewindable seed component.

Hardware Impact: No measured microseconds are claimed. Static verification found no `_shotOrdinal` and no Unity random/time/math regressions in the owned fire path.

## 2026-05-19 Loop 23 / Fire-Path Stale Comment Purge

Problem: `HostileFlora` source comments still described GameTickManager, projectile spawning, prefab projectile assignment, and player tag caching. That was now false and would mislead the next agent toward restoring prefab projectile authority.

Solution: Updated the file header and `SlowTick` summary to describe Core registry dispatcher ownership and mathematical trajectory queueing. Removed unused `PlayerTag`.

Rejected Alternatives: Leaving comments stale was rejected because this project uses source comments as architectural guardrails under context compaction. Removing the legacy `projectilePrefab` serialized field was rejected because prefab compatibility is intentional; only false instructions were removed.

Scalability potential: Runtime quality behavior is unchanged. This prevents future maintenance from bypassing the `GlobalQualityWeight` ballistic solver path.

Hardware Impact: No runtime microseconds claimed. The impact is maintenance safety: fewer textual cues pointing back to GameObject projectile damage authority.

## 2026-05-19 Loop 24 / Quality NaN Fail-Closed and Barrier Audit

Problem: `ResolveGlobalQualityWeight()` and the old intersection-job smoothing helper treated non-finite quality input as `1.0f`. That is backwards for a thermal/scalability control surface: a NaN quality source would silently promote ricochet budget, limb admission, signal budget, and VFX scale toward ultra workload. `LimbAdmissionFloor` also accepted `1.0f`, which can collapse or invert the `math.smoothstep(floor, 0.95f, quality)` edge interval. The main runtime damage-signal budget also referenced that smoothing helper from outside its owning type, weakening compile proof.

Solution: Added `BallisticsRuntime.SanitizeQualityWeight(float)` and `BallisticsRuntime.SmoothQualityWeight(float)`, then routed global quality, damage-signal budgeting, intersection quality smoothing, impact VFX scale, and telemetry counters through those runtime helpers. Non-finite quality now fails closed to `0.0f`. Clamped `LimbAdmissionFloor` to `0.0f..0.9f` in tuning sanitation and again inside the job before `smoothstep`, preserving a positive denominator interval below the fixed `0.95f` upper edge.

Rejected Alternatives: Leaving NaN quality as ultra was rejected because fault handling must shed work, not amplify it. Relying only on editor tuning validation was rejected because Burst jobs consume DTOs and must defend against corrupted or rollback-restored values. Replacing smooth admission with a binary limb switch was rejected because the batch explicitly forbids binary quality gates.

Scalability potential: Low or faulted quality now deterministically collapses to root-biased primitive admission, zero ricochet budget, minimum signal budget, and smaller VFX scale. Middle/high/ultra still use the same polynomial quality curve and continuous admission, preserving the visual-overkill path when quality is valid.

Hardware Impact: No measured microseconds are claimed. Expected low-end protection is avoiding accidental ultra workload after a non-finite quality read and preventing `smoothstep` denominator instability from becoming a NaN source in the hot intersection job.

Barrier Audit: Focused scan found no arbitrary hot-path `Complete()`. The graph remains `BallisticIntersectionJob.Schedule -> EmitBallisticDamageSignalsJob.Schedule -> StageImpactVFXJob.Schedule -> BallisticsTelemetryJob.Schedule -> JobHandle.ScheduleBatchedJobs`. The only direct `handle.Complete()` is the documented cold/manual mock injection path; force completion is contained in teardown/reset through `DispatcherJobSwap.TryComplete`.

## 2026-05-19 Loop 25 / HostileFlora Inspector Authority Hygiene

Problem: `HostileFlora` still exposed an unused `playerLayerMask` field and inspector/source text about projectile spawning. Runtime had already moved to Core registry player lookup and mathematical trajectory queueing, so the inspector surface was lying to designers and future agents.

Solution: Removed the unused `playerLayerMask` serialized field and its defaulting block. Updated state comments, class summary, muzzle tooltip, speed tooltip, and legacy visual shell tooltip to state the actual route: Core registry target acquisition and BallisticsRuntime combat authority.

Rejected Alternatives: Keeping the layer mask as harmless compatibility was rejected because it had no read site and implied a stale detection mechanism. Removing `projectilePrefab` outright was rejected because it may still be serialized on existing prefabs; the field remains explicitly marked as ignored by combat authority.

Scalability potential: Runtime quality behavior is unchanged. The change protects the continuous `GlobalQualityWeight` ballistic solver route by removing inspector instructions that could route future work back to spawned projectiles.

Hardware Impact: No measured microseconds are claimed. This removes no hot-path math; it removes misleading serialized surface area and one cold validation branch.
