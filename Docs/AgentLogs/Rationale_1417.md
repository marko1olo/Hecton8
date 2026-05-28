# Rationale 1417 - Combat Damage / Armor Penetration Array Purge

Date: 2026-05-28
Status: LOOP 4 STATUS-EFFECT RESIDUAL PURGED STATICALLY - BUILD/STRESS PENDING

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
