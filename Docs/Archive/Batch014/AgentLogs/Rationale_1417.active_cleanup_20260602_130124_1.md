# Rationale 1417 - Combat Damage / Armor Penetration Array Purge

Date: 2026-05-28
Status: LOOP 18 ARMOR LOCK OWNERSHIP FIXED - BUILD FAILED OPAQUE; STRESS/PREOWNED-DUMP-WORKER PENDING

## Decision 00 - Mandate Set

Problem: Combat native aliases violate Data Sovereignty and can become stale across DataVault relocation/defrag.
Solution: Use DataVault descriptor ownership, local method-scope view resolution, try/finally write-lock release, explicit ARM64 DTO layouts, and fixed 300-entry telemetry.
Rejected Alternatives: Keeping persistent NativeArray fields is banned. Converting to managed arrays would break Burst and introduce GC/heap pressure. Disabling combat visual reactions would avoid presentation cost but not fix memory ownership.
Scalability potential: Low tier keeps combat truth identical and sheds only presentation density. Middle tier keeps bounded VFX. High tier adds richer decals/sparks in VISUAL_SYNC. Ultra tier spends saved CPU on visual overkill only.
Hardware Impact: Expected low-end i3/MX350 gain comes from removing stale alias risk and bounding overflow paths; measured microseconds are pending static and runtime proof.

## Decision 01 - Armor Aliases Classified As Views

Problem: The batch count includes 19 NativeArray fields in ArmorPenetrationVaultViews. Treating them as persistent owners would delete the only legal Burst physical views resolved from DataVault descriptors.
Solution: Classify armor fields as transient ref struct views owned by existing VaultGenerationHandle descriptors. Keep job NativeArray fields because Burst requires physical views at schedule time.
Rejected Alternatives: Removing all NativeArray fields from armor jobs would force managed indirection or same-frame DataVault lookup inside Burst, both invalid. Keeping persistent damage aliases unchanged is also invalid.
Scalability potential: Low tier keeps armor truth as the same 8x6 LUT path; middle/high/ultra may spend saved CPU on richer debug hit/VFX presentation without changing armor authority.
Hardware Impact: Avoids extra lookup work in the penetration hot loop on i3/MX350; expected runtime delta is 0 us because this is a classification decision, not a new computation.

## Decision 02 - Damage Ingress Route

Problem: NativeQueue<CombatDamageRequest> is a persistent owner-side alias and cannot be placed in DataVault as-is without preserving bounded overload behavior.
Solution: Replace the owner queue with a DataVault NativeArray<CombatDamageRequest> ingress lane plus _queuedSignalCount. Peak overload fails closed when count reaches MaxQueuedSignals and records the existing queue-full anomaly.
Rejected Alternatives: Growing NativeQueue capacity on demand would allocate and break explosion overload determinism. Throwing on overflow would violate fail-closed combat routing. Dropping oldest damage changes combat truth under load.
Scalability potential: Low tier gets constant bounded ingress. Middle/high/ultra can increase visual response density elsewhere, but damage truth capacity remains fixed unless the owner explicitly changes balance data.
Hardware Impact: Removes NativeQueue structural overhead and eliminates overflow exception risk; i3/MX350 expected save is small per hit packet but critical under blast peaks.

## Decision 03 - Target Slot Lookup Route

Problem: NativeParallelHashMap<int,int> is a persistent alias and complicates DataVault ownership; linear scan would cost O(targets) on every hit.
Solution: Use two DataVault NativeArray<int> buffers as a fixed open-addressed lookup table: target id keys plus target slot values. Rebuild/removal stays owner-phase only.
Rejected Alternatives: Managed Dictionary introduces GC and is not Burst-safe. Linear scan is acceptable only for diagnostics, not damage hot paths. Keeping NativeParallelHashMap preserves the illegal owner alias.
Scalability potential: Low tier benefits from bounded probe count. High/ultra do not get different combat truth; visual overkill remains outside the lookup.
Hardware Impact: For i3/MX350, fixed cache-local probes are cheaper and more predictable than hash-map container state under impact storms.

## Decision 04 - Buffer ID Ownership

Problem: Adding 25 public BufferID enum values during a 20-agent batch risks merge churn and unrelated enum ownership conflict.
Solution: Use local cast BufferID constants 1417000-1417024 in CombatDamageRuntime and document them in COMBAT_NATIVE_ALIAS_LEDGER_1417.json.
Rejected Alternatives: Editing central H8Memory.cs enum is not needed for DataVault calls and increases cross-agent collision surface. Reusing armor IDs would corrupt owner facts.
Scalability potential: Same route across low, middle, high, and ultra devices; quality changes presentation only.
Hardware Impact: Runtime cost is unchanged; integration risk is lower because no central enum rebuild noise is introduced.

## Decision 05 - Damage Black Box Route

Problem: Current damage telemetry dump name is SHINOBU_318-branded and does not satisfy agent 1417 crash forensic ownership.
Solution: Keep the fixed 300-frame native telemetry ring and route damage anomaly dump to Docs/AgentLogs/Dump_1417_CombatDamage.bin.
Rejected Alternatives: Per-crash managed JSON allocation is banned. Expanding telemetry capacity by quality would alter memory shape and violate fixed black-box policy.
Scalability potential: Low tier pays the same fixed ring cost. Middle/high/ultra can add presentation diagnostics outside the ring without changing combat state capture.
Hardware Impact: Fixed 300x64B ring remains about 19.2 KiB; no heap allocation, bounded disk dump only on anomaly.

## Decision 06 - Damage DataVault Descriptor Surface

Problem: CombatDamageRuntime.cs carried 24 persistent NativeCollection fields, including queue, hashmap, health, status, counters, LUT, and black-box telemetry arrays.
Solution: Move physical storage behind VaultGenerationHandle descriptors in CombatDamageRuntime_VaultViews.cs. Resolve CombatDamageVaultViews inside owner methods and pass only physical NativeArray views into Burst jobs.
Rejected Alternatives: Keeping manager NativeArray fields violates Data Sovereignty. Passing VaultGenerationHandle into Burst Execute is invalid because jobs need physical NativeArray views at schedule time.
Scalability potential: Low tier gets bounded deterministic combat truth. Middle, high, and ultra tiers use the same truth route and spend quality scalar only on visual signal density and surface-normal fidelity.
Hardware Impact: Removes stale-pointer risk during vault relocation; i3/MX350 benefit is stability and predictable cache-local array access, not a claimed profiler-measured speedup.

## Decision 07 - Damage Ingress Writer Fences

Problem: Damage packet ingress writes request, detail, and impact-AUP buffers. A plain mutable DataVault view would be vulnerable during a compaction fence.
Solution: TryQueueDamage now acquires write locks for CombatDamageSignalsBufferId, CombatDamageSignalDetailsBufferId, and ArmorPenetrationVaultBufferIds.SignalImpactAups, writes the bounded packet, and releases every acquired lock through ReleaseDamageIngressWriteLocks from a finally block.
Rejected Alternatives: Per-frame NativeQueue.Enqueue was the original illegal owner alias. Growing a queue during explosions would allocate and break deterministic overload behavior.
Scalability potential: Low tier drops excess packets without exceptions. Middle/high/ultra keep identical combat truth; saved frame budget can increase decals, sparks, and hit hologram presentation outside the damage authority path.
Hardware Impact: Three writer-fence calls per accepted packet cost more than an unprotected array store, but prevent relocation corruption. Overflow path avoids IndexOutOfRangeException under blast peaks on i3/MX350.

## Decision 08 - Flat Target Lookup

Problem: NativeParallelHashMap<int,int> was a persistent alias and could not be owned directly by the combat manager.
Solution: Replace it with two DataVault NativeArray<int> buffers, keys and slots, using fixed open addressing with CombatTargetLookupCapacity 4096.
Rejected Alternatives: Managed Dictionary creates GC and is not Burst compatible. Linear scans of MaxTargets on every hit are predictable but too expensive under multi-drone firefights.
Scalability potential: Low/middle/high/ultra all use the same lookup truth; no quality switch changes target identity.
Hardware Impact: Fixed probe loop is branch-predictable and cache-local. Worst-case is bounded by 4096 probes, normal case is expected near O(1).

## Decision 09 - Read-Only Presentation Access

Problem: Public read accessors must not mutate, publish, allocate, complete jobs, or pin writer state.
Solution: Added CombatDamageReadOnlyVaultViews using IDataVault.TryReadOnlyHandle for target lookup, health, inverse max health, and instance ids. IsTargetRegistered, TryGetTargetHealthFraction, and registered-target resolution now consume read-only views.
Rejected Alternatives: Reusing mutable TryReadHandle in public readbacks works mechanically but violates the global read accessor doctrine.
Scalability potential: Read-only route is identical across weak, middle, high, and ultra hardware; quality scalar cannot change health truth.
Hardware Impact: Prevents presentation/UI queries from becoming writer participants. Runtime cost is descriptor validation only; no heap allocation.

## Decision 10 - Residual Combat Domain Debt

Problem: A wider combat-domain scan after the 43-entry purge found persistent NativeQueue/NativeArray fields in CombatDamageRuntime_StatusEffects.cs. These are outside the two-file master ledger target, but they are still the same Data Sovereignty class of problem.
Solution: Do not hide the violation in final proof. Keep the current 43-entry purge scoped and record the residual status-effect debt as a separate combat-domain blocker unless there is enough compile budget to migrate it safely.
Rejected Alternatives: Claiming full combat-domain purge would be false. Rushing status-effect migration without build capacity risks breaking combat scheduling while CPU is already saturated by another dotnet process.
Scalability potential: Low tier is most exposed because status queues can spike during hazards; high/ultra visual quality must not affect status truth ownership.
Hardware Impact: Risk is stability rather than measured speed: stale status aliases can corrupt on relocation. Current mitigation is documentation and no final complete claim until migrated or formally excluded.

## Decision 11 - DTO Layout Proof

Problem: The damage DTOs already used explicit layout, but there was no direct damage-side validator equivalent to the armor layout guard.
Solution: Add ValidateCombatDamageLayout with UnsafeUtility.SizeOf checks for CombatDamageRequest, CombatDamageSignalDetail, CombatDamageResult, and CombatTelemetryEntry plus Marshal.OffsetOf checks including private padding fields by literal name.
Rejected Alternatives: Documentation-only layout proof is not enforceable. Reordering fields now would create unnecessary behavioral risk because the explicit offsets already meet 8-byte total-size alignment.
Scalability potential: Layout truth is hardware-invariant across weak, middle, high, and ultra devices; quality scalar does not alter DTO ABI.
Hardware Impact: 0 us runtime unless the validator is invoked; prevents ARM64 padding regressions from silently entering Burst jobs.

## Decision 12 - Build Gate Honesty

Problem: Final compile is required for proof, but the host was already saturated.
Solution: Sample CPU and active compiler/dotnet process first. Result: CPU 100 with dotnet PID 16780 active, then after 30 seconds CPU 100 with dotnet PID 55080 active. No dotnet build was launched by agent 1417.
Rejected Alternatives: Spamming dotnet build would violate the explicit resource-throttling order and contaminate proof.
Scalability potential: No gameplay scalability change; this is host stability discipline.
Hardware Impact: Avoided adding compile load to an already saturated machine.

## Decision 13 - Status Effect Residual Native Alias Migration

Problem: Wider combat scan found eight persistent native owner fields in `CombatDamageRuntime_StatusEffects.cs`: request queue, state rows, tuning, telemetry ring/cursor, counters, VFX requests, and staged damage signals. They were outside the original 43-entry prompt ledger but inside the same combat authority class.
Solution: Move the request lane to `BufferID.Shinobu319StatusEffectRequests = 71269` and resolve all status buffers through method-local `CombatStatusEffectVaultViews` / `CombatStatusEffectReadOnlyVaultViews`. `ApplyStatusEffectRequestsJob` now scans a bounded `NativeArray<CombatStatusEffectRequest>` with `RequestCount` instead of draining a persistent `NativeQueue`.
Rejected Alternatives: Keeping the status queue as "separate domain debt" was no longer defensible after the primary purge. Reusing reserved scanner/shared-report IDs would corrupt status ownership. Growing a queue during hazard spikes would allocate and break deterministic overload behavior.
Scalability potential: Low tier keeps fixed bounded status request ingress and cadence scaling. Middle tier increases cadence and batch size smoothly. High tier emits richer staged VFX. Ultra tier can spend saved CPU on visual overkill, but status truth, DTO layout, and damage authority do not branch.
Hardware Impact: On i3/MX350 this removes stale persistent aliases and replaces queue structural state with linear fixed rows. Measured microseconds are absent; static estimate is stability gain plus lower branch/container overhead under hazard bursts.

## Decision 14 - Status Effect Writer Lock Hardening

Problem: After moving status buffers into the Vault, several owner writes still used mutable resolved views without an explicit writer/pin window: tuning seed/update, mock status generation, target slot reset/copy, and counter writes.
Solution: External request/tuning/state/counter writes now use `TryAcquireWriteLock` with `finally` release. Mock generation and cold clear/seed use existing `TryLockStatusEffectVaultBuffersForJobs` / `TryLockCombatDamageVaultBuffersForJobs` pin windows with `finally` unlock. Scheduled status tuning is written only after status buffers are pinned for the job window.
Rejected Alternatives: Treating cold/mock paths as safe because they are not frame-hot leaves relocation windows unproven. Adding a new managed lock is invalid because DataVault already owns relocation semantics.
Scalability potential: No binary low/high switch. The lock discipline is identical across low, middle, high, and ultra hardware; only status cadence, batch size, toxic bubble cadence, and VFX density consume `GlobalQualityWeight`.
Hardware Impact: Adds small cold/mock fence overhead and prevents relocation corruption. No runtime profiler measurement exists; build and stress proof remain pending because CPU gate blocked compilation.

## Decision 15 - Explicit Combat Stress Harness Source

Problem: The batch requires a 100000 packet stress harness. Before this loop, there was no source artifact for the test, so Task 16 was pure pending debt.
Solution: Add `Assets/_Project/Tests/Editor/CombatDamageRuntime1417StressHarnessEditTests.cs` with an explicit heavy ingress test that creates a temporary DataVault, registers one damage receiver, spams 100000 `TryQueueDamage` calls, and asserts fail-closed acceptance/rejection instead of overflow exceptions. The same file includes a non-heavy static source audit for persistent native fields and forbidden hot tokens.
Rejected Alternatives: Running the stress test without Unity Test Runner/profiler is impossible from this shell. Claiming 0 B GC from source inspection is false. Making the heavy test automatic would punish every editor test pass.
Scalability potential: Low tier proof focuses on bounded rejection under overload. Middle/high/ultra truth remains identical; higher devices can spend visual budget on hit/VFX response after the bounded authority lane rejects excess packets.
Hardware Impact: Harness is editor-only and explicit. Runtime impact is 0 us until manually executed. Verification remains pending: no Unity execution, no GCMonitor, no profiler sample.

## Decision 16 - Target And Armor Writer Lock Hardening

Problem: Loop 5 audit found real remaining relocation hazards after the first migration. Target register/unregister/sync and helper refresh paths wrote DataVault-backed target arrays without a dedicated writer window. `TryQueueDamage` refreshed hit profile from managed receiver/Transform state before ingress locks. Armor target snapshots could be refreshed before armor buffers were pinned. Late dispatch wrote counters, status-active flags, and telemetry after job locks were released. Queue-reject anomalies published a signal but did not write the fixed black-box ring.
Solution: Add `TryAcquireCombatTargetWriteLocks`/`ReleaseCombatTargetWriteLocks`, `TryAcquireCombatTelemetryWriteLocks`/`ReleaseCombatTelemetryWriteLocks`, and `TryAcquireArmorTargetWriteLocks`/`ReleaseArmorTargetWriteLocks`. Wrap every owner-phase target and armor mutation in `try/finally`. Move armor snapshot refresh inside locked/pinned windows. Remove hot ingress hit-profile refresh from `TryQueueDamage`; owner phase must publish `SyncTargetHitProfile`. Add late dispatch pending flags so result/status dispatch retries under `TryLockCombatDamageVaultBuffersForJobs` instead of writing after unlock. Record queue/storage rejects into the 300-entry telemetry ring under telemetry write locks.
Rejected Alternatives: Keeping hot `Transform`/receiver reads in damage ingress violates the route doctrine and makes hit packets mutate target state from the wrong phase. Treating registration/sync as "cold enough" leaves compaction relocation windows unproven. Adding managed locks would duplicate DataVault ownership semantics. Using a full physical penetration simulation was rejected; the armor LUT is the deterministic truth route and quality is spent on presentation/debug density.
Scalability potential: Low tier gets bounded hit ingestion, fail-closed overflow, deterministic LUT armor truth, and reduced mutation cadence. Middle tier keeps the same truth route with higher status cadence/batch. High tier spends saved budget on richer hit normals, decals, and VFX. Ultra tier can push visual overkill through SignalBus presentation, but damage truth, DTO layout, buffer IDs, and save identity do not branch.
Hardware Impact: No profiler microseconds are claimed. Static impact is stability: writer fences add small owner-phase overhead and prevent DataVault relocation corruption. On i3/MX350, the practical gain is avoiding exception/corruption during explosion spikes; runtime proof remains pending because CPU/build gate was blocked.

## Decision 17 - Atomic Target Side-State Fail-Closed Contract

Problem: Loop 6 audit found a real partial-publication path. Armor/status helper methods could fail silently when a DataVault write lock was unavailable. `UnregisterTarget` also tombstoned BallisticsRuntime primitives before all side-state locks succeeded, so lock contention could leave a still-registered target without a ballistic primitive.
Solution: Convert target armor/status helper routes to bool-return fail-closed contracts and consume those returns at register, unregister, protection sync, and clear call sites. Add `MoveTargetSideState` and `ClearTargetSideState` so status and armor side-state locks are acquired together, all buffers are validated before mutation, and both domains are released in `finally`. Move ballistic tombstone after successful `ClearSlot` and `RebuildTargetLookup`.
Rejected Alternatives: Keeping best-effort void helpers would hide lock contention and corrupt cross-domain target facts. Copying status and armor through independent helpers was rejected because one domain could mutate while the other failed. Tombstoning first was rejected because it publishes an irreversible side-effect before the owning combat slot mutation has actually succeeded.
Scalability potential: Low tier gets fail-closed target mutation under compaction or explosion pressure. Middle tier keeps the same deterministic target truth with more status cadence. High tier can spend saved stability margin on richer hit/debris presentation. Ultra tier can push visual overkill through SignalBus without changing target, armor, status, DTO, or save identity ownership.
Hardware Impact: No profiler microseconds are claimed. On i3/MX350 the impact is stability: failed write locks now return false before combat arrays and ballistic primitive state diverge. Added owner-phase lock checks are not in per-hit Burst loops.

## Decision 18 - Ingress Reject Telemetry After Lock Release

Problem: `TryQueueDamage` had one post-lock storage reject branch that called queue-reject telemetry while damage ingress write locks were still held. This was not needed and nested telemetry lock acquisition inside ingress locks.
Solution: Record `postLockStorageReject` while holding ingress locks, release ingress locks in `finally`, and publish queue reject telemetry only after `ReleaseDamageIngressWriteLocks` has executed.
Rejected Alternatives: Keeping nested telemetry publication was mechanically functional but unnecessary. Removing the telemetry would hide overflow/storage faults. Throwing would violate fail-closed overload behavior.
Scalability potential: Low tier avoids extra lock nesting during overload spikes. Middle, high, and ultra retain identical damage truth; quality only affects visual/reporting fidelity outside authority.
Hardware Impact: No measured microseconds. Static impact is cleaner lock ordering in the hit ingress path, with the same bounded packet capacity and no heap allocation.

## Decision 19 - Continuation Build Gate Recheck

Problem: The final build/stress proof remains required, but launching another compiler while the host is saturated would violate the explicit Compilation Resource Throttling rule.
Solution: Re-sampled host load before any build attempt. Result: CPU 99 percent, active dotnet PID 38260 and VBCSCompiler PID 18948. No dotnet build was launched by agent 1417.
Rejected Alternatives: Running build because code edits are already staged would contaminate proof and compete with an active compiler. Claiming final compile proof without an executed build is false.
Scalability potential: No gameplay change. This preserves machine stability so low-tier device work can still be validated later under controlled CPU conditions.
Hardware Impact: 0 us runtime change, 0 build CPU consumed by this continuation. Loop 8 post-patch gate also blocked: CPU 87 percent, no compiler process printed.

## Decision 20 - Cross-Domain Target Lock Order Normalization

Problem: Subagent audit found inconsistent nested writer-lock order. `RegisterTarget` and `UnregisterTarget` acquired combat target locks first, then called helpers that acquired armor/status locks. Status scheduling already uses armor -> status -> combat, so the owner-phase target mutation route could fail closed unnecessarily under contention and created an avoidable ordering hazard.
Solution: Rework owner target mutation to acquire locks in one order: armor first, status second, combat third. Add locked armor/status side-state variants so `RegisterTarget`, `UnregisterTarget`, `SyncTargetProtection`, and standalone `ClearSlot` do not call helper methods that acquire new locks while combat locks are held. Release order is combat -> status -> armor in `finally`.
Rejected Alternatives: Keeping combat -> armor/status was rejected because it conflicts with scheduler order. Adding a managed global mutex was rejected because DataVault lock ownership already defines the concurrency contract. Removing side-state updates was rejected because armor/status target facts would diverge.
Scalability potential: Low tier gets fewer avoidable fail-closed registration/unregistration failures during compaction. Middle/high/ultra keep identical combat truth and can spend quality budget only on visual fidelity.
Hardware Impact: No measured microseconds. Owner-phase target mutation now holds combat locks for less time and performs side-state validation before combat mutation.

## Decision 21 - Armor Quality Must Not Change Gameplay Truth

Problem: A subagent/static audit found `HomeostasisBrain.GlobalQualityWeight` feeding armor damage authority: effective armor blended from `baseArmor * 0.65f` to LUT armor, and weak-point damage scalar was multiplied by quality. That violates the global rule that quality cannot change gameplay truth.
Solution: Make `EvaluateArmorPenetrationCore` use LUT armor directly (`effectiveArmor = lutArmor`) and apply weak-point scalar from `weakWeight` only. Keep `GlobalQualityWeight` available for telemetry/presentation, not damage authority.
Rejected Alternatives: Keeping the blend as a "low-end approximation" was rejected because weak devices would take different damage from high-end devices. A higher-fidelity physical solver was rejected because the LUT is cheaper, deterministic, and sufficient for cinematic impact.
Scalability potential: Low tier uses the same armor truth as ultra. Middle/high/ultra spend quality budget on diagnostic hit density, impact VFX, decals, and status presentation rather than changing damage math.
Hardware Impact: No measured microseconds. Static impact is authority correctness: one quality-dependent lerp and one quality multiplier were removed from the damage calculation.

## Decision 22 - Debug Hit Density Is Presentation, Not Truth

Problem: Armor debug hit recording wrote one row for every processed hit. That does not alter damage, but it consumes diagnostic buffer bandwidth during blast spikes and was not scaled by continuous quality.
Solution: Add `ShouldCaptureArmorDebugHit(sequence, sourceId, VisualQualityWeight01)` using `SmoothStep01` and a stable hash threshold. Gate only `WriteArmorDebugHit`; damage amount, armor mitigation, health, status, and result DTOs remain unchanged.
Rejected Alternatives: Binary `if (isLowEnd)` gating was rejected. Removing debug hits entirely was rejected because high-tier diagnostics and cinematic tooling need the data. Changing damage cadence was rejected because gameplay truth must stay fixed.
Scalability potential: Low tier keeps sparse diagnostics under overload. Middle tier captures more samples. High tier captures dense debug traces. Ultra can approach full diagnostic density while the same combat truth remains authoritative.
Hardware Impact: No measured microseconds. Expected i3/MX350 benefit is lower diagnostic write pressure in explosion spikes; gameplay cost is unchanged.

## Decision 23 - Job Pin Partial Failure Finally Contract

Problem: Several `TryLockBuffer` helper methods and multi-vault scheduling paths released partial locks through branch-local manual unlocks before returning false. The releases were present, but the proof surface did not satisfy the required single `finally` cleanup contract.
Solution: Convert combat, status, armor, mock, evaluator torture, and CAS torture pin helpers to `success` flag plus `finally` cleanup. Wrap damage/status multi-vault scheduling acquisition so partial locks are released in `finally` unless ownership transfers to the scheduled job.
Rejected Alternatives: Leaving manual branch unlocks was rejected because future edits can add a return between lock and unlock. A managed global mutex was rejected because DataVault lock/pin APIs are the ownership contract.
Scalability potential: Low tier gets fail-closed cleanup under contention and compaction. Middle/high/ultra get identical authority behavior; quality scaling stays in presentation/cadence only.
Hardware Impact: No measured microseconds. Static impact is lower relocation/deadlock risk; the added bool checks are outside the Burst per-hit loop.

## Decision 24 - Build Gate Blocked By Active External Dotnet

Problem: Final compilation is still required, but the host had CPU 87 percent and active dotnet PID 51336 already running `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`. After 30 seconds, CPU was still 74 percent and PID 51336 was still active.
Solution: Do not launch a second build. Record the gate failure and leave Task 15 pending.
Rejected Alternatives: Launching another build would violate the explicit CPU >50 / active compiler ban and contaminate evidence. Claiming the external process as agent 1417 verification would be false.
Scalability potential: No gameplay change. This preserves host stability for later controlled verification.
Hardware Impact: 0 us runtime change and 0 build CPU consumed by agent 1417 in this continuation.

## Decision 25 - Fault Dump Writer Allocation Surface Reduction

Problem: Loop 10 audit found `new FileStream` and `BinaryWriter` in the three 1417 owner-specific black-box dump writers. These paths are fault-only, but they still expanded managed allocation surface exactly where forensic dumping is supposed to be predictable.
Solution: Replace `BinaryWriter` entry serialization with stackalloc `Span<byte>` buffers and `BinaryPrimitives` little-endian writes in combat damage, status effects, and armor penetration dump writers. Replace textual `new FileStream` with `File.Open` while preserving the existing binary header and oldest-to-newest ring order.
Rejected Alternatives: Claiming the dump path was hot-path clean was false. Switching to JSON was rejected because binary telemetry must stay fixed-size. Implementing a rushed background worker was rejected because a correct worker needs a pre-owned export snapshot or MMF route; passing live DataVault NativeArrays to an ad-hoc thread would create a worse lifetime hazard.
Scalability potential: Low tier gets smaller managed fault-path pressure and the same fixed 300-frame forensic data. Middle/high/ultra retain the same dump truth; richer diagnostics must be added through presentation/debug lanes, not by changing combat authority.
Hardware Impact: No measured microseconds. Static scan after the patch: `BinaryWriter = 0`, `new FileStream = 0`, fault dump writer reference-type `new = 0`. Residual remains: synchronous `File.Open` is still managed IO, not MMF/pre-owned worker export.

## Decision 26 - Burst NativeArray Budget Lane NoAlias Repair

Problem: The static job-field scan found two NativeArray<int> fields in `ProcessDamageQueueJob` without NoAlias: `DeflectSignalWriterBudget` and `ImpactSignalWriterBudget`. These arrays are SignalBus budget lanes passed into a Burst job and must not be left ambiguous for alias analysis.
Solution: Add `NoAlias` beside the existing `NativeDisableParallelForRestriction` attributes on both budget fields.
Rejected Alternatives: Ignoring the result because the fields are small was rejected; the contract is about aliasing, not size. Passing managed writer state into the job was already avoided and remains rejected.
Scalability potential: Low tier keeps predictable Burst codegen and bounded signal writer budget checks. Middle/high/ultra get identical combat truth; visual overkill can consume SignalBus presentation lanes without weakening job alias contracts.
Hardware Impact: No measured microseconds. Post-fix scan: 8 combat/armor/status job structs, 90 NativeArray fields, missing NoAlias = 0.

## Decision 27 - Final Build Gate Honesty

Problem: A final compilation proof is still required, but the resource-throttling rule forbids `dotnet build` when CPU load is above 50 percent, even if no compiler process is currently visible.
Solution: Re-sample immediately before final build decision. Result: CPU average 74, compiler_processes=none, build_invoked=false. Keep Task 15 pending instead of turning a blocked gate into a fake pass.
Rejected Alternatives: Running `dotnet build` at CPU 74 would violate the explicit rule and contaminate the verification artifact. Claiming an older external build as agent 1417 proof would be false.
Scalability potential: No gameplay change. This is host stability discipline; the combat code remains quality-scaled through continuous presentation/cadence scalars only.
Hardware Impact: 0 us runtime change and 0 build CPU consumed by agent 1417 at this final gate.

## Decision 28 - Timed-Out Build Process Is Not Compile Proof

Problem: A later conditional gate command started `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` as PID 6088, but the tool-call timed out after 124 seconds. The dotnet process later exited, yet stdout and exit code were not captured.
Solution: Mark the build result as unproven. Do not launch a second build merely to recover lost output. Update status/report/log to distinguish "process executed" from "compilation verified".
Rejected Alternatives: Claiming success from an exited process would be hallucination. Running another build immediately would be build spam and could hide the lost evidence problem.
Scalability potential: No gameplay change. Verification discipline remains separate from combat quality scaling.
Hardware Impact: Compile CPU was consumed by PID 6088, but no reliable build result artifact exists. Post-exit CPU sample was 85 with no compiler processes.

## Decision 29 - Final External Compiler Contention

Problem: A final process sample after the unproven PID 6088 build showed the machine under new external compiler load: CPU 93, dotnet PID 67008 running `dotnet build Hecton8.Core.csproj --nologo -clp:ErrorsOnly -maxcpucount:1`, and child csc PID 19420.
Solution: Do not launch any additional build. Record this as the latest contention state and keep compile proof pending.
Rejected Alternatives: Retrying the solution build while another build and csc are active would violate both the CPU > 50 rule and the active compiler rule.
Scalability potential: No gameplay change. Verification remains blocked by host contention, not by a quality-scaling design choice.
Hardware Impact: 0 additional compile CPU consumed by agent 1417 after PID 6088.

## Decision 30 - Adjacent Ballistics Dump Allocation Surface

Problem: Wider `Assets/_Project/Scripts/Gameplay/Combat` scan found the same forensic allocation smell in adjacent `BallisticsRuntime.cs`: `BinaryWriter` plus `new FileStream` in `Dump_BALLISTICS_SURGEON.bin`, and `new FileStream` in the cold CSV LUT import path.
Solution: Replace those writers/readers with `File.Open`; serialize the ballistics telemetry dump through stackalloc `Span<byte>` and `BinaryPrimitives`, preserving the existing 8-byte header and 48-byte row payload. This is adjacent-domain hygiene, not a change to combat damage truth.
Rejected Alternatives: Leaving the token hit would keep final combat-folder scans noisy. Expanding this into a full ballistics rewrite was rejected because agent 1417 owns damage router / armor LUT, not projectile architecture.
Scalability potential: No gameplay truth change. High-tier visual overkill remains driven by presentation systems; ballistics dump serialization is fault/cold path only.
Hardware Impact: No measured microseconds. Static proof: `rg (BinaryWriter|new FileStream) Assets/_Project/Scripts/Gameplay/Combat -g *.cs` now returns empty.

## Decision 31 - Post-Ballistics Build Gate Blocked

Problem: After the adjacent `BallisticsRuntime.cs` hygiene patch, compile proof was still needed, but the host gate failed.
Solution: Sample CPU and compiler processes before any build. Result: CPU 53 and active external dotnet PID 35384 running `dotnet build Hecton8.Core.csproj --no-restore -nologo -v:minimal /m:1 /p:UseSharedCompilation=false /nr:false`; build_invoked=false.
Rejected Alternatives: Launching a second build while CPU > 50 and another dotnet build is active would violate the explicit resource-throttling rule.
Scalability potential: No gameplay change.
Hardware Impact: 0 additional compile CPU consumed by agent 1417 after Loop 14.

## Decision 32 - Armor Fault Dump Player Route Narrowing

Problem: Subagent audit found the armor black-box dump writer was editor-only and used a broad `catch (Exception)`, so player/dev builds marked `_armorTelemetryDumped` without writing `Dump_1417_ArmorPenetration.bin`.
Solution: Remove the `UNITY_EDITOR` gate from `DumpArmorTelemetryIfNeeded` and replace the broad catch with explicit I/O/path exception catches. The writer still uses the fixed 20-byte header, 64-byte entries, `stackalloc Span<byte>`, and `BinaryPrimitives`.
Rejected Alternatives: Claiming Task 17 complete from an editor-only writer was false. Adding a rushed worker without a pre-owned snapshot was rejected because passing live Vault `NativeArray` views to a thread would create a worse lifetime violation.
Scalability potential: No gameplay or visual quality change. Low/mid/high/ultra all receive the same forensic route; visual overkill remains outside combat authority.
Hardware Impact: No measured microseconds. Residual remains: the writer still uses synchronous `File.Open`; full MMF/pre-owned worker is pending.

## Decision 33 - Armor Lock Borrowing Rejected

Problem: `TryLockArmorVaultBuffersForJobs` returned true when `_armorVaultBuffersLocked` was already true. Editor/dev mock and torture proof paths checked `_damageJobScheduled` but not `_statusJobScheduled`, so they could borrow armor locks owned by a status job and later unlock buffers they did not acquire.
Solution: Make `TryLockArmorVaultBuffersForJobs` fail closed when armor buffers are already locked, block mock/torture proof while `_statusJobScheduled`, defer armor vault rebind while status jobs are scheduled, and apply pending armor rebind after status unlock in both normal and early completion paths.
Rejected Alternatives: Reference-counting the lock in this patch was rejected because DataVault exposes buffer locks, not nested ownership tokens; a nested borrow would hide who owns the release. Waiting for Unity runtime proof before patching was rejected because the static race was concrete.
Scalability potential: Low tier avoids rare relocation/unlock corruption under status-combat overlap. Middle/high/ultra keep identical combat truth; quality scaling remains presentation/debug density only.
Hardware Impact: No profiler microseconds. Static impact is fail-closed editor/dev proof paths and safer hot-swap deferral. Build proof failed opaquely after this fix.

## Decision 34 - Opaque Build Failure

Problem: After Loop 18 patches, the build gate finally opened with CPU 33 and no active compiler processes. The single permitted `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` returned exit code 1 but captured no stdout/stderr.
Solution: Mark compile proof as failing/opaque. Do not claim green. A diagnostic rerun was blocked by CPU 57, then CPU 100 with active dotnet PID 66612 running `dotnet build Assembly-CSharp.csproj --no-restore -nologo -v:minimal /m:1 /p:UseSharedCompilation=false /nr:false`.
Rejected Alternatives: Declaring success from empty output is hallucination. Launching another build during CPU > 50 and active dotnet violates the resource-throttling rule.
Scalability potential: No gameplay change.
Hardware Impact: Compile CPU was consumed, but no admissible error list exists yet.

## Decision 35 - Diagnostic Build Still Has No Error Line

Problem: The Loop 18 build failure was opaque, so a diagnostic pass was needed, but only when the resource gate allowed it. A later gate opened with CPU 47 and no dotnet/csc/VBCSCompiler processes.
Solution: Launch exactly one diagnostic solution build: `dotnet build Hecton8.slnx -nologo -maxcpucount:1 -v:minimal`. It ran 501.8 seconds and returned exit code 1. Captured output lists successful DLLs through `Hecton8.World.Contracts` and contains no `CS` compiler error or `MSB` build error line. Save the evidence to `Docs/AgentLogs/Dump_1417_BuildFailure_20260528T1919.txt` and keep Task 15 pending. Separate compile-medic logs after the latest 1417 source timestamps show `Assembly-CSharp`, `Assembly-CSharp-Editor`, `Hecton8.Core`, `MapMagic`, `MapMagic.Editor`, `MapMagic.MicroSplat`, and `MapMagic.MicroSplat.Editor` succeeded, but they are not a full solution pass.
Rejected Alternatives: Claiming full build success from partial assembly logs was rejected. Launching a third solution build after post-build CPU sampled at 100 was rejected by the compilation throttling rule. Patching span-based `FileStream.Write(Span<byte>)` was rejected because the post-timestamp `Assembly-CSharp`/editor compile logs already show the Unity C# profile accepted those overloads.
Scalability potential: No gameplay change. Verification remains separate from quality scaling; low/middle/high/ultra combat authority remains identical.
Hardware Impact: One diagnostic build consumed 501.8 seconds wall time. No runtime microsecond change.
