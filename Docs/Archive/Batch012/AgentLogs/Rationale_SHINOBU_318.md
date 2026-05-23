PROMPT IDENTIFIED: SHINOBU_318
DOMAIN: Echelon 5 Combat & Survival Physiology / Armor Penetration LUT
TASK COUNT: 20
STATUS: IMPLEMENTED / GUARDED COMPILE HIT EXTERNAL WALL / POST-COMPILE SOURCE PATCH GUARDED BY CPU

## Decision 001: Existing Owner
Problem: Existing project already has `CombatDamageRuntime` managing native health, status, results, `CombatDamageSignal` ingestion, and `DeflectSignal` output.
Solution: Convert `CombatDamageRuntime` to partial and add `HectonCombatRuntime_ArmorPenetration.cs` as an isolated partial file.
Rejected Alternatives: A new `HectonArmorManager` would create a second owner for health truth and violate one fact -> one owner -> one route.
Scalability potential: Low/Middle/High/Ultra all use the same owner. Quality only changes math precision and feedback density, not health authority.
Hardware Impact: Avoids another registry/signal drain and duplicate native maps; estimated 8us saved on i3/MX350 during combat frames.

## Decision 002: Feedback Lane
Problem: Task asks for deflect feedback with AUP/material payload, but existing `VisualFlareSignal` is a 32B screen-space flare and cannot carry AUP/material.
Solution: Use existing `DeflectSignal` for armor deflection and `ImpactSignal` where AUP/material feedback is required. Do not invent `ArmorBouncedSignal`.
Rejected Alternatives: Mutating `VisualFlareSignal` would break existing consumers and binary layout. Adding a new lane without route card/review is forbidden.
Scalability potential: Low can drop impact feedback by existing lane budgets; Ultra can consume existing `ImpactSignal` for richer sparks/gore without adding gameplay truth cost.
Hardware Impact: Reuses configured flush lanes; estimated 3us saved versus new lane validation/flush overhead.

## Decision 003: AUP Source
Problem: Current `TryBuildCombatSignal` turns `ImpactAup` into runtime point, which is not target-local and loses the exact impact relation at world edges.
Solution: Store the original `double3 ImpactAup` in a native side array and subtract target root `double3` inside the Burst solver before float downcast.
Rejected Alternatives: Casting absolute AUP to `float3` first was rejected due precision loss near the 100km boundary. Transform-local Unity calls in the hot loop are rejected.
Scalability potential: Low uses same AUP truth with cheaper armor blend. Ultra can add presentation detail without changing the authoritative coordinate path.
Hardware Impact: AUP subtraction is a few scalar ops; it buys deterministic hits and removes PhysX ray broadphase cost. Net low-end gain depends on hit volume; profiler proof pending.

## Decision 004: Collider Sanitation
Problem: Runtime prefab mutation via YAML or blind deletion is unsafe, and current repo already has `FaunaColliderValidator`.
Solution: Extend editor validation/scanning only. No raw prefab YAML edits in this pass.
Rejected Alternatives: Text deleting collider YAML was rejected because FileID/GUID alignment risk is higher than the performance benefit without a concrete prefab target.
Scalability potential: Low devices benefit from fewer active broadphase shapes; Ultra keeps visual meshes while combat vulnerability remains LUT-only.
Hardware Impact: Broadphase savings are scene-dependent and PENDING VERIFICATION.

## Decision 005: Armor Side Data
Problem: CombatDamageRuntime already owns flat native health/status buffers, but armor needs target-local AUP roots, rotations, half extents, and LUT profiles without creating a second health owner or private persistent native arrays.
Solution: Request armor side data from `GlobalDataVault` as pointer-free `VaultGenerationHandle<T>` descriptors and resolve transient `NativeArray<T>` views only at boot, scheduling, editor, and parser boundaries. Refresh target snapshots from cached receiver transforms before the damage job.
Rejected Alternatives: Private persistent `NativeArray<T>` fields were rejected after Vault archaeology because they create a second memory owner and complicate compaction/rollback fences. Per-hit `Transform.InverseTransformPoint` was rejected because it is managed scene access in the hot loop. Per-body collider lookup was rejected because it resurrects PhysX broadphase dependency.
Scalability potential: Low uses base armor blended toward LUT; middle/high/ultra consume the same 8x6 LUT with stronger material/VFX fidelity. No binary quality switch.
Hardware Impact: Removes per-hit Transform/Collider path from armor; estimated 30us saved for a 50-target pellet burst on i3/MX350, pending profiler.

## Decision 006: AUP LUT Projection
Problem: Existing CombatDamageSignalCodec converts ImpactAup into a runtime point that is not target-local and loses precision under floating-origin shifts.
Solution: Preserve the original ImpactAup in a side array, subtract the target root AUP in double precision inside the Burst solver, then inverse-rotate the local float3 into an 8x6 LUT address.
Rejected Alternatives: Absolute float conversion and Physics.Raycast body-part intersection were rejected as nondeterministic near sector boundaries.
Scalability potential: Low, middle, high, and ultra share exact AUP truth. Quality only changes the armor blend weight and VFX intensity.
Hardware Impact: Adds constant scalar math per hit and removes broadphase sync; expected net win under swarm impact loads.

## Decision 007: Material Byte Encoding
Problem: Armor material and thickness must fit a compact LUT while still differentiating chitin and steel.
Solution: Use two high bits for material class and six low bits for continuous strength. Mitigation uses the strength as a normalized scalar; material bits route ImpactSignal material IDs.
Rejected Alternatives: A managed material table or string material names in hot path were rejected for cache and GC reasons.
Scalability potential: Low can ignore material-rich VFX; ultra can consume material hash and strength byte for sparks, gore, or decal variation.
Hardware Impact: One byte lookup plus masks; estimated sub-microsecond per 64 hits on low-end silicon.

## Decision 008: Health Mutation
Problem: Pellet fanout can eventually become parallel; non-atomic float writes would lose damage under concurrent writers.
Solution: Use float-bit CAS through NativeArrayUnsafeUtility and Interlocked.CompareExchange, while keeping current job single-owner deterministic.
Rejected Alternatives: Main-thread health mutation callbacks and lock objects were rejected. NativeReference aggregation was rejected because it adds a second merge phase.
Scalability potential: The same health route survives low single-job scheduling and future high/ultra parallel impact shards.
Hardware Impact: CAS is more expensive than a raw store but avoids future race bugs. Cost is acceptable because PhysX armor raycasts are removed.

## Decision 009: Editor Enforcement
Problem: A runtime LUT route is meaningless if prefabs keep body-part colliders and developers can reintroduce Physics.Raycast damage probes.
Solution: Extend FaunaColliderValidator for redundant primitive hitbox stripping and add OOP_Hitbox_Scanner plus Ballistic Armor X-Ray editor tooling.
Rejected Alternatives: Raw prefab YAML deletion was rejected due FileID/GUID corruption risk. Runtime collider deletion was rejected because hot path hygiene must be authored before play.
Scalability potential: Low devices lose broadphase clutter; ultra keeps visual meshes and richer feedback without physical hitbox cost.
Hardware Impact: Scene-dependent broadphase savings; editor-only scanner/tuner cost does not touch runtime.

## Decision 010: Mock Burst Completion
Problem: The task requires an emergency mock combat generator, but scheduling a QA-only generator without completion would leave managed queue counts stale.
Solution: Generate mock request/detail/AUP arrays with a deterministic Burst `IJobParallelFor`, complete immediately inside the explicit QA/editor method, then enqueue through the normal public route.
Rejected Alternatives: Running the generator in `FrameTick` was rejected because it would hide a same-frame `.Complete()` in runtime. Hand-building mock signals on the main thread was rejected because Task 06 explicitly asked for Burst proof.
Scalability potential: Low/middle/high/ultra runtime paths are unaffected; the mock generator is only a test harness.
Hardware Impact: No gameplay cost. Editor-only completion cost is bounded by requested mock signal count.

## Decision 011: Vault Buffer Sovereignty
Problem: The first local numeric Vault candidate `71620..71630` collided with existing SHINOBU_158 buoyancy/SIMD lanes in `H8Memory`.
Solution: Move SHINOBU_318 armor lanes to owner-local numeric `73580..73590` and record the collision repair in the binary payload ledger and route card.
Rejected Alternatives: Sharing `71620..71630` was rejected because it would corrupt unrelated buoyancy memory. Editing the central `BufferID` enum was rejected because local numeric casts are sufficient and avoid a core compile-wall edit.
Scalability potential: Low/Middle/High/Ultra all keep stable DTO identity. Quality changes math/presentation only, not buffer IDs or save/rollback ownership.
Hardware Impact: Runtime cost is 0us; prevents cross-domain Vault corruption.

## Decision 012: Pure Read Accessors
Problem: `TryGetArmorTuning` and `TryGetArmorDebugBuffers` initially could allocate/grow or cache generation handles through the shared resolver.
Solution: Split resolver behavior by `ensure`. `ensure=false` uses local generation descriptors and `TryReadHandle` only; it does not allocate, grow, cache, lock, publish, complete jobs, or mutate global state. `ensure=true` is restricted to boot/schedule/editor/parser mutation paths.
Rejected Alternatives: Calling `EnsureArmorPenetrationNativeState()` from read accessors was rejected as a Global Systems Doctrine violation.
Scalability potential: Read cost stays flat across all quality weights; visual/debug richness can scale without changing truth ownership.
Hardware Impact: Avoids hidden main-thread memory work in UI/debug reads; gain is path-dependent, estimated 1-5us during editor/player diagnostics.

## Decision 013: Cold QA Mock Locking
Problem: The mock Burst generator writes Vault-backed request/detail/AUP buffers and reads target AUP/extents, but it intentionally completes inside a QA/editor method.
Solution: Keep the immediate `.Complete()` fenced behind `UNITY_EDITOR || DEVELOPMENT_BUILD`, lock the relevant Vault buffers during the scheduled job, then unlock before returning.
Rejected Alternatives: Running mock generation during `FrameTick` was rejected because it would create a hidden runtime same-frame schedule/readback loop. Hand-authored managed mock packets were rejected because the task required Burst proof.
Scalability potential: Runtime Low/Middle/High/Ultra cost remains 0. QA can stress any quality weight through the same authoritative route.
Hardware Impact: No gameplay cost; editor-only locking protects DataVault compaction hygiene.

## Decision 014: Mock Lane Isolation and Locked-Vault Recovery
Problem: The shared `ensure:true` resolver could allocate QA mock buffers when runtime only needed hot armor lanes. A blunt `IsAllocationLocked` guard would also block legitimate recovery of already-created generation descriptors during AUP allocation locks.
Solution: Add `includeMock` to `TryResolveArmorPenetrationVaultViews`. Runtime/boot/parser paths resolve core armor lanes only; `GenerateMockArmorImpacts` is the only caller that requests mock request/detail/AUP buffers. Under `IDataVault.IsAllocationLocked`, the buffer resolver first tries the cached handle or `TryGetGenerationHandle` and `TryResolveHandle`; allocation-capable `EnsureGenerationHandle` is unreachable until the lock clears. Default tuning initialization now acquires a DataVault writer lock.
Rejected Alternatives: Keeping mock lanes in the core resolver was rejected because cold QA capacity would become part of the hot combat memory contract. Returning false for all allocation-locked resolves was rejected because it prevents descriptor recovery after service replacement even when no allocation is needed. Writing default tuning directly through a resolved `NativeArray` was rejected because it bypasses Vault writer ownership.
Scalability potential: Low/Middle/High/Ultra runtime costs stay identical because mock lanes are absent from normal scheduling. QA can still stress every quality weight through `includeMock:true` without mutating the hot route contract.
Hardware Impact: Saves 0us in the common already-booted frame, but removes three unnecessary cold-buffer descriptor/growth checks from runtime ensure paths and avoids allocation-lock stalls during AUP shift windows.

## Decision 015: Subagent Static Audit Integration
Problem: Static review found four remaining production risks: hot AUP enqueue could call allocation-capable Vault resolve, job/Vault locks could leak if `Schedule()` or parser safety checks throw, release fault handling could run synchronous managed file I/O, and collider stripping could delete legitimate non-root primitive colliders.
Solution: `WriteSignalImpactAup` and frame scheduling now use `ensure:false`; core armor lanes must already exist from cold init/register/editor paths. Job scheduling, mock generation, default tuning, runtime tuning, and CSV profile import now release Vault locks in `finally`. Fault completion records `_armorTelemetryDumpRequested`; only editor/development builds perform the synchronous `Dump_SHINOBU_318.bin` write. Primitive collider deletion now requires explicit damage naming, damage layer, or damage component markers.
Rejected Alternatives: Allocating missing Vault buffers from the damage enqueue path was rejected as hot-path mutation. Trusting no-exception scheduling/parser paths was rejected because editor safety checks can throw and leave Vault lanes locked. Deleting every non-root non-trigger primitive collider was rejected because interaction/proximity/movement colliders are not necessarily damage hitboxes.
Scalability potential: Low/Middle/High/Ultra all keep the same truth route. The changes remove cold/QA/debug side effects from the hot path without changing quality curves or authoritative damage math.
Hardware Impact: Hot-path allocation risk removed; direct microsecond savings are path-dependent, but the critical gain is eliminating frame-stall and stuck-lock failure modes under editor safety checks or AUP allocation locks.

## Decision 016: DataVault Hot-Swap Recovery Without Frame Polling
Problem: After moving damage enqueue and frame scheduling to `ensure:false`, a DataVault that appears or is replaced after initial static reset could leave armor lanes unavailable until another cold mutation path runs.
Solution: Register a cold `IGlobalRegistryHotSwapListener` from `EnsureArmorPenetrationNativeState`. DataVault replacement releases previous SHINOBU-owned generation handles, caches the current Vault, and reopens core armor lanes from the registry event. If a damage job owns the lanes, the rebind is deferred and applied from the owner completion window after `FinishArmorPenetrationScheduledCompletion`.
Rejected Alternatives: Polling `GlobalRegistry.DataVault` from `FrameTick` was rejected as hot identity lookup. Re-enabling `ensure:true` in the damage path was rejected because it can allocate/grow Vault rows while processing combat. Releasing handles during an in-flight job was rejected because it can invalidate native views still owned by Burst.
Scalability potential: Recovery behavior does not depend on quality weight and does not alter gameplay truth. Low/Middle/High/Ultra all regain the same Vault lanes after service replacement.
Hardware Impact: Normal-frame cost is 0us because registry calls the bridge only on service replacement. Cold recovery prevents a stuck no-armor route without reintroducing hot allocation.

## Decision 017: Packed Metadata Bounds Vaccination
Problem: `ProcessDamageQueueJob` trusted packed detail, damage-class, and armor-class metadata before indexing `SignalDetails` and the 8x8 damage armor LUT. Internal ingress writes valid values, but defensive Burst code must survive corrupted signal payloads and stale queue rows.
Solution: Add an unsigned `detailIndex` bounds check before reading `SignalDetails[detailIndex]`. Clamp packed damage class and armor class to `[0, 7]` before indexing `DamageArmorLut`.
Rejected Alternatives: Relying on managed ingress correctness was rejected because hot jobs must fail closed under corrupted signals. Expanding the counter layout was rejected because it changes existing combat telemetry ABI; malformed detail rows currently increment `CounterDroppedResults`.
Scalability potential: All quality weights use the same metadata guard; visual/ALU scaling remains unchanged.
Hardware Impact: Adds two clamps and one rare branch per signal. Cost is below the removed PhysX armor route and prevents undefined native array access.

## Decision 018: Registered Tool Hits Must Enter Armor By AUP, Not Child Collider Metadata
Problem: `ToolHitUtility.TryQueueCentralDamage` still called `ResolveLocalizedHit(hitCollider, ...)` and `receiverComponent.transform.InverseTransformPoint(hitPoint)` before queuing registered combat targets. That allowed child-collider weakspot/limb metadata to bypass the AUP/LUT armor route.
Solution: Expose `CombatDamageRuntime.TryQueueDamage(in CombatDamageRequest, in CombatDamageSignalDetail, double3 impactAup)` as the public AUP-bearing ingress. Registered tool damage now resolves finite hit-point AUP, sends that AUP to the Vault-backed armor lane, sets weakspot metadata to `None`, and leaves `LocalPoint` zero as non-authoritative fallback detail. Removed `ResolveLocalizedHit`, `ICombatWeakspot`, and `ICombatLimbHealthSource`; the remaining weakspot enum is packed metadata ABI, not collider ownership.
Rejected Alternatives: Keeping collider-derived weakspot metadata was rejected because it preserves physical hitbox authority. Using `Transform.InverseTransformPoint` as the armor coordinate path was rejected because it is managed scene math and fails the AUP subtraction mandate. Replacing all tool physics contact acquisition was rejected as Echelon 4 ownership; this pass changes only registered combat damage ingress.
Scalability potential: Low/Middle/High/Ultra all use the same AUP truth. Quality continues to blend base armor toward LUT detail and VFX richness without changing hit authority.
Hardware Impact: Removes two interface/component probes and one transform inverse from registered tool damage ingress. Estimated 2-8us saved during dense cutter/melee hits on i3/MX350, plus broad architectural gain by closing the child-collider weakspot bypass.

## Decision 019: Tool-Hit AUP Fallback Must Fail Closed, Not Center The LUT
Problem: After the registered tool route stopped using collider-local weakspot metadata, `TryQueueCentralDamage` could still queue a registered target with `impactAup = double3.zero` if the player pose bridge was unavailable. The Burst solver would then fall back to `LocalPoint=float3.zero`, effectively converting a missing AUP into a center-cell LUT hit.
Solution: `TryQueueCentralDamage` now requires a finite hit-point AUP before queueing registered armor damage. `TryResolveImpactPointAup` resolves by player pose first, then falls back to `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(hitPoint)` for finite runtime-space hits. `OOP_Hitbox_Scanner` now reports method-scoped registered armor counts separately from the legacy unregistered `IDamageReceiver` fallback so proof artifacts cannot claim global removal of that fallback transform path.
Rejected Alternatives: Keeping `double3.zero` was rejected because zero is a plausible world position and violates fail-closed AUP truth. Removing the unregistered `IDamageReceiver` fallback was rejected as out-of-domain legacy gameplay ownership; the armor route is registered `CombatDamageRuntime` only. Using child-collider `InverseTransformPoint` for registered hits remains rejected because it reintroduces physical hitbox authority.
Scalability potential: Low/Middle/High/Ultra all preserve the same finite AUP truth. Quality still controls only LUT blend and feedback richness, never hit authority or fallback identity.
Hardware Impact: Adds one finite AUP guard and a cold fallback conversion on registered tool hits. It prevents misprojected center-cell armor solves and keeps the removed transform inverse on the registered route at 0 hits; estimated correctness win outweighs sub-microsecond branch cost.

## Decision 020: Mock Burst Job Must Prove Non-Aliasing Too
Problem: The production `ProcessDamageQueueJob` had explicit `[NoAlias]` fields, but the cold editor/development `GenerateMockArmorImpactSignalsJob` only used `[ReadOnly]` and `[WriteOnly]`. Even though the mock generator is not a gameplay frame path, it is still a Burst mathematical job and must not leave aliasing ambiguity in the proof surface.
Solution: Add `[NoAlias]` to all six NativeArray fields in `GenerateMockArmorImpactSignalsJob`: `InstanceIds`, `TargetRootAups`, `TargetHalfExtents`, `Requests`, `Details`, and `ImpactAups`.
Rejected Alternatives: Treating QA/editor-only jobs as exempt was rejected because the mandate requires every mathematical job to expose aliasing facts. Splitting mock generation into managed setup was rejected because Task 06 explicitly requires a Burst stress harness.
Scalability potential: Runtime Low/Middle/High/Ultra cost remains 0 because the mock path is gated to editor/development. QA can still stress all quality weights without weakening production aliasing proof.
Hardware Impact: No gameplay microsecond claim. In editor/development stress runs, the compiler can assume disjoint arrays and emit cleaner vectorized stores.

## Decision 021: Proof Artifacts Must Track Source-Level Aliasing Corrections
Problem: After Decision 020, source had the correct mock job `[NoAlias]` contract, but route-card/XML/JSON/ledger proof still described aliasing at the production-job level only. That mismatch is not a runtime bug, but it is an audit bug: future reviewers would not see that Task 06's Burst job also obeys pointer aliasing discipline.
Solution: Update `LOG_SHINOBU_318.md`, `SHINOBU_318_ARMOR_PENETRATION_LUT_ROUTE_CARD.md`, `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, `PHYSICS_OPTIMIZATION_REPORT_SHINOBU_318.json`, and `SHINOBU_318_SELF_AUDIT.xml` with the exact six mock NoAlias fields.
Rejected Alternatives: Leaving the proof stale was rejected because HECTON-8 treats docs/logs as long-term memory after context compression. Re-running a build just to validate a documentation-only sync was rejected under the CPU/compiler guard and because the source patch already passed focused static checks.
Scalability potential: Runtime Low/Middle/High/Ultra route remains unchanged. The synchronized proof prevents a future agent from weakening the mock lane or adding collider/raycast test harnesses.
Hardware Impact: 0us gameplay delta. Documentation correctness preserves maintainability and keeps Burst aliasing evidence visible for editor/development stress runs.
