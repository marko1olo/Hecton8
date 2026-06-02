# Rationale 1323 - Submarine Atmosphere Memory Sovereignty

Date: 2026-05-26
Status: VERIFIED_GREEN_STATIC / COMPILE_BLOCKED_BY_BUILD_HYGIENE

## Decision 001 - Domain Scope

Problem: Prompt names Echelon 5 survival physiology while target files are submarine atmosphere/gas dynamics and `Assets/_Project/Scripts/Atmosphere`.
Solution: Treat `SubmarineAtmosphereSystem.cs` as primary authority target and Atmosphere folder as secondary sweep. Gas partial pressure affects survival O2/CO2 truth, but file ownership is atmosphere-side.
Rejected Alternatives: Editing unrelated survival/player files would violate domain boundary and create cross-agent conflict.
Scalability potential: Low tier keeps coarse gas/visual fake cadence; middle keeps full compartment cadence; high/ultra spend saved CPU on richer VISUAL_SYNC steam, warning, and scrubber presentation without changing gameplay truth.
Hardware Impact: Avoiding cross-domain edits prevents compile churn; expected low-end gain is risk reduction, not measured frame time yet.

## Decision 002 - GlobalDataVault Route Stance

Problem: Prompt demands replacement of persistent native collection fields with generation handles, but a blind rewrite can break existing compile-visible APIs.
Solution: First build a field-level hit list and ownership map; then replace only after confirming available `GlobalDataVault`/handle APIs on disk.
Rejected Alternatives: Inventing `VaultGenerationHandle<T>` signatures before reading Core memory APIs would be fake architecture and likely non-compiling.
Scalability potential: Low/middle/high/ultra behavior must preserve `GlobalQualityWeight`; the memory route must not change solver truth identity.
Hardware Impact: Reduces stale-pointer/relocation risk on weak silicon; microsecond gain pending source and build audit.

## Decision 003 - Task 01 Scanner Result

Problem: The prompt says 37 forbidden fields, while the `SubmarineAtmosphereSystem` class itself contains 33 persistent `NativeArray` fields.
Solution: Count all persistent class/static-helper native aliases in `SubmarineAtmosphereSystem.cs`: 33 solver arrays plus 4 static pressure-event `NativeQueue` fields. This matches 37. Transient job fields remain outside the primary hit list.
Rejected Alternatives: Reporting only the 33 solver arrays would hide the two static event buses; counting `IJob` struct parameters would inflate the number with non-persistent phase-local views.
Scalability potential: Low tier keeps event capacity bounded; middle/high/ultra can raise presentation cadence through continuous budget scalars without changing gas truth arrays.
Hardware Impact: The ledger prevents stale pointer misses during DataVault relocation. Direct microsecond gain is not claimed; crash-risk surface reduction is the measurable artifact.

## Decision 004 - Buffer Range Selection

Problem: `SystemID.SubmarineAtmosphere` does not exist in `H8Memory.cs`, and `H8Memory.cs` is already dirty from another agent.
Solution: Use existing `SystemID.HabitatAtmosphere` and reserve local `BufferID` casts `72200..72238` for submarine atmosphere arrays, event payload lanes, telemetry ring, and cursor. `rg` found no collisions in project scripts or `H8Memory.cs`.
Rejected Alternatives: Editing the central `BufferID` enum during concurrent work risks a cross-agent merge conflict; reusing `AtmosphereLogistics*` IDs would violate one-owner-one-route.
Scalability potential: The range separates survival-submarine gas truth from base logistics atmosphere lanes, allowing each tier to scale cadence/capacity independently while preserving DTO identity.
Hardware Impact: Local route avoids central file churn now. Expected low-end gain comes from relocation safety; no frame-time claim before build/profiler proof.

## Decision 005 - DTO and Telemetry Scope

Problem: The forbidden solver arrays store primitives (`float`, `int`, `uint`, `byte`, `int2`), not custom gas DTOs.
Solution: Leave primitive ABI unchanged and add a planned 64-byte explicit `AtmosphereTelemetryEntry1323` for blackbox state. Existing `HighPressureEventPayload` and `FatalPressureImplosionEventPayload` are already explicit 32-byte payloads.
Rejected Alternatives: Packing 33 scalar lanes into a new monolithic compartment DTO would rewrite solver truth layout, change cache behavior, and exceed the task’s memory-ownership blast radius.
Scalability potential: Low/middle/high/ultra can scale solver cadence and fake presentation; DTO layout remains fixed so saves, telemetry, and authority routes do not bifurcate.
Hardware Impact: 64-byte telemetry stride aligns ring entries to cache lines; low-end benefit is deterministic post-mortem visibility without managed allocation.

## Decision 006 - Primary Vault Descriptor Substitution

Problem: `SubmarineAtmosphereSystem.cs` owned 33 solver `NativeArray<T>` fields and two static event buses owned 4 `NativeQueue<T>` fields, blocking DataVault relocation.
Solution: Replace persistent native aliases with `VaultGenerationHandle<T>` descriptors and local `BufferID 72200..72238` lanes under `SystemID.HabitatAtmosphere`; keep Burst job `NativeArray<T>` fields transient because they are phase-local scheduled views.
Rejected Alternatives: Managed arrays would violate zero-GC/cache rules; editing central `H8Memory.cs` during another agent's dirty change would create conflict; retaining DataVault-exempt queues would fail Task 06.
Scalability potential: Low tier can skip a contended atmosphere step without corrupting state; middle/high/ultra keep the same truth route and spend quality budget on presentation fakes, not alternate gameplay state.
Hardware Impact: Removes stale pointer owners from the MonoBehaviour and static buses. Expected low-end gain is crash-risk reduction during compaction; no frame-time claim before profiler proof.

## Decision 007 - Job Lock Window

Problem: Burst `AtmosphereStepJob` still requires raw `NativeArray<T>` views, but DataVault compaction must not relocate buffers while a job is running.
Solution: Acquire DataVault buffer locks for every job input/output lane immediately before constructing the job, release locks immediately after `DispatcherJobSwap.TryComplete`, then swap generation handles. DataVault replacement force-completes and releases through the previous vault.
Rejected Alternatives: Passing handles into Burst jobs is useless to Burst and violates the prompt; resolving views without a lock would leave a relocation window; same-frame `.Complete()` loops were not introduced.
Scalability potential: Weak devices can miss a solver step on lock contention; middle/high/ultra retain the same cadence when locks are available and continue to scale visual overkill through presentation systems.
Hardware Impact: Adds 26 lock/unlock calls around the scheduled job. Cost must be profiled, but it buys relocation correctness and avoids hard crashes on i3/MX350-class devices.

## Decision 008 - Blackbox Route

Problem: The prompt requires a 300-frame post-mortem buffer for atmosphere state and NaN faults without managed hot-path logging.
Solution: Add `SubmarineAtmosphereTelemetryEntry` as `LayoutKind.Explicit, Size=64`, stored in DataVault lane `72237` with cursor lane `72238`; write one fixed row after completed solver steps and dump to `Docs/AgentLogs/Dump_1323_SubmarineAtmosphere.bin` on NaN.
Rejected Alternatives: Managed `Queue<T>`, `List<T>`, string logs, or chat reports would allocate and fail the Black Box rule; background thread dumping was avoided because Unity object paths and DataVault read views are safer on the main fault path.
Scalability potential: Low tier receives identical crash proof with one 64-byte row per completed step; high/ultra can extend visual telemetry without changing the 64-byte core schema.
Hardware Impact: Telemetry write is bounded at one row per completed atmosphere step. Fault-only binary serialization is outside steady frame cost; ring footprint is 19,200 bytes plus 4-byte cursor.

## Decision 009 - Public Read Accessors

Problem: Public room getters exposed presentation/survival data and previously resolved class properties that returned writable `NativeArray<T>` views.
Solution: Route public `GetRoom*` methods through `TryReadOnlyHandle` via `TryReadVaultValue`, fail closed to deterministic fallback constants, and avoid publishes, allocation, job completion, or registry lookup.
Rejected Alternatives: Keeping property-backed direct reads would violate the Global Systems Doctrine for pure read accessors; adding synchronous job completion would hide scheduler bugs and stall frames.
Scalability potential: UI and survival readers now get stable fallbacks during relocation contention on low tier; high/ultra preserve exact values when DataVault read views are valid.
Hardware Impact: Read-only handle resolution adds validation cost but avoids unsafe cached pointer reads; expected low-end benefit is correctness under compaction, not a claimed microsecond win.

## Decision 010 - Scalability Preservation

Problem: Memory ownership migration must not create binary quality routes or alter gameplay gas truth based on hardware tier.
Solution: Keep the submarine atmosphere memory route independent of quality switches; no `GlobalQualityWeight`, low-end/high-end, or ultra branches were added to the primary diff. Existing broader atmosphere systems continue to own continuous quality math.
Rejected Alternatives: Adding separate low-tier and ultra-tier memory lanes would bifurcate authority and violate one fact/one owner/one route.
Scalability potential: Weak, middle, high, and ultra devices use identical BufferID/DTO/save identity; only cadence/contention outcomes and presentation systems may scale continuously.
Hardware Impact: No extra branch ladder in hot gas truth. Low-end impact is avoided complexity; high-end remains free to spend visual budget outside this memory route.

## Decision 011 - Domain Sweep Boundary

Problem: The prompt requires a broader Atmosphere sweep, but `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` is already dirty and likely owned by another agent.
Solution: Treat `GasDynamicsSolver.cs` as a collision and skip edits; scan every uncontested Atmosphere C# file with the same class-field scanner. Result: zero persistent native collection fields in uncontested scope.
Rejected Alternatives: Editing the dirty sibling file would create cross-agent conflict; adding churn to clean files with zero violations would inflate risk without value.
Scalability potential: Domain-level memory proof stays stable across weak/middle/high/ultra devices because no new alternate ownership route is introduced.
Hardware Impact: No runtime cost. The gain is integration stability and proof that no extra class-level stale pointers were found outside the primary target.

## Decision 012 - Layout Guard Placement

Problem: DTO layout proof must survive future edits, not only this chat session.
Solution: Add `AtmosphereMemorySovereigntyValidator1323.cs` as an editor-only validator using `UnsafeUtility.SizeOf` and `Marshal.OffsetOf` for `HighPressureEventPayload`, `FatalPressureImplosionEventPayload`, and `SubmarineAtmosphereTelemetryEntry`.
Rejected Alternatives: Runtime assertions would add player cost; documentation-only offset tables would not stop a future compile/import violation.
Scalability potential: All hardware tiers consume the same DTO stride. Low tier avoids unaligned traps; high/ultra do not get a divergent schema.
Hardware Impact: Editor/import-only cost. Runtime impact is zero; low-end runtime receives fixed 32-byte event payloads and a fixed 64-byte telemetry row.

## Decision 013 - Final Proof Artifact

Problem: The coordinator requires machine-readable proof, not chat claims.
Solution: Generate `Docs/Reports/VAULT_EXORCISM_REPORT_1323.json` with before=37 from Task 01 ledger, after=0 from scanner, skipped dirty files, audited file count, audited SHA-256 hashes, and remaining violation list.
Rejected Alternatives: Manual Markdown-only proof would be non-reproducible; compiling during CPU >50% would violate build hygiene.
Scalability potential: The report is independent of quality tier and preserves one route for all hardware classes.
Hardware Impact: No runtime cost. It prevents later regressions by making the audited hash set explicit.

## Decision 014 - Rejection Audit Parser Fix

Problem: The strict `<AGENT_PROMPT id="1323">` extractor failed because the active tag has `role` and `chat_name` attributes across a wrapped line.
Solution: Re-extract with `<AGENT_PROMPT\b(?=[^>]*\bid="1323")[^>]*>...`, verify 20 `Task NN` entries, and treat only that block as the live assignment.
Rejected Alternatives: Falling back to status-memory alone would violate batch isolation; scanning neighboring prompts would contaminate the domain.
Scalability potential: Prompt isolation has no runtime tier effect; it prevents cross-agent edits that would fracture the atmosphere authority route.
Hardware Impact: No runtime cost. It removes process risk, not frame time.

## Decision 015 - Phase Lock Release Hardening

Problem: The first implementation released job pins on DataVault replacement/dispose, but phase write locks could remain live if a service swap hit during an owned phase.
Solution: Release `_atmospherePhaseWriteLockMask` through the previous vault before handle release and call `ReleaseAtmospherePhaseWriteLocks()` during native-state disposal. Wrap phase/job multi-lock acquisition in `try/finally` with partial-mask release.
Rejected Alternatives: Assuming service replacement never occurs during a phase is not valid in a concurrent agent runtime. Holding locks through handle release would block compaction.
Scalability potential: Weak devices may fail closed on contention; middle/high/ultra preserve the same truth route and spend recovered stability on presentation, not alternate gas state.
Hardware Impact: Adds bounded release checks; prevents dangling lock/pin stalls on low-end silicon during relocation.

## Decision 016 - Branchless Atmosphere Kernel

Problem: `AtmosphereStepJob.Execute` still had pre-existing `if`/`continue` guards inside room and door loops, violating the rejection gate for SIMD-friendly hot kernels.
Solution: Replace active-room, valid-door, and status decisions with boolean masks, clamped safe indices, `math.select`, and `math.lerp`. Invalid lanes write deterministic fallbacks or self-preserving values without escaping array bounds.
Rejected Alternatives: Leaving the branchy solver and calling it "pre-existing" would fail the gate; simulating richer chemistry would waste CPU without player-facing value.
Scalability potential: Low tier gets predictable fixed-loop cost; middle/high/ultra can spend saved branch-prediction budget on fake steam/pressure presentation while gas truth stays identical.
Hardware Impact: Removes explicit branch tokens from the Burst kernel inner loops; expected i3/MX350 gain is lower misprediction risk, not claimed as measured until profiler.

## Decision 017 - DTO Padding Literalization

Problem: `FatalPressureImplosionEventPayload` used one private `ulong` pad at offset 24, which is aligned but not the byte-explicit padding shape demanded by the rejection gate.
Solution: Replace it with `_pad0.._pad7` byte fields at offsets 24..31. Event payload size remains 32B and ARM64 multiple-of-8.
Rejected Alternatives: Keeping the `ulong` pad would be technically aligned but weaker as a proof artifact.
Scalability potential: All hardware tiers consume the same payload ABI; no quality split is introduced.
Hardware Impact: Runtime layout unchanged at 32B; proof quality improved, no frame cost.

## Decision 018 - Compile Hygiene Boundary

Problem: Final compile verification is still desired, but build policy forbids launching dotnet while CPU is under load or another dotnet/csc process exists.
Solution: Run build preflight only. Current preflight reports 25% CPU with seven active `dotnet` processes, so no compile was launched. Static gates and `git diff --check` are the completed verification artifacts.
Rejected Alternatives: Starting another build would violate explicit project policy and could interfere with other agents.
Scalability potential: No runtime effect.
Hardware Impact: Prevents workstation contention; no frame-time claim.

## Decision 019 - Pre-Lock View Probe Purge

Problem: The rejection audit found that several public mutation routes checked compatibility `NativeArray` properties with `.IsCreated` before acquiring the atmosphere phase write lock.
Solution: Move those checks inside `TryEnterAtmosphereWritePhase`/`finally ExitAtmosphereWritePhase` and make top-level readiness checks descriptor-only through `VaultGenerationHandle<T>`.
Rejected Alternatives: Treating `.IsCreated` as harmless would still resolve a writable view before the compaction-safe lock window.
Scalability potential: Weak devices can fail closed on lock/contention without exposing stale writable views; middle/high/ultra keep the same gas truth path and can spend recovered stability on presentation fakes.
Hardware Impact: Removes pre-lock pointer exposure. Runtime cost is a few handle-field comparisons before phase entry; no measured frame-time claim.

## Decision 020 - Read-Only Debug Snapshot Route

Problem: `RefreshDebugState` can run from cold lifecycle and skipped-tick paths, so writable compatibility properties there were not protected by a phase write lock.
Solution: Resolve method-local read-only vault views with `TryReadVaultArray`, bounds-check every row, and fall back to deterministic zeros/reference temperature if a view is unavailable.
Rejected Alternatives: Forcing a write phase just to render debug numbers would be wrong ownership and could block compaction for non-authoritative telemetry.
Scalability potential: Low tier can skip/zero debug stats during relocation; high/ultra retain exact debug stats without changing authoritative atmosphere state.
Hardware Impact: Read-only checks are bounded by room count and already debug-state work. No extra managed allocation is introduced.

## Decision 021 - DTO Runtime Position Lane Split

Problem: The event payloads were explicit and 32-byte aligned, but storing `RuntimePosition` as a `Vector3` field hid three 4-byte lanes behind an aggregate type in the offset proof.
Solution: Split event payload runtime position into `RuntimePositionX`, `RuntimePositionY`, and `RuntimePositionZ` at offsets 0, 4, and 8. Keep a non-storage `RuntimePosition` property for call-site compatibility.
Rejected Alternatives: Leaving `Vector3` as a single field would satisfy size but weaken the byte-by-byte ARM64 proof and pointer-first audit.
Scalability potential: Weak, middle, high, and ultra tiers consume the same 32-byte payload ABI; presentation can scale independently from event truth.
Hardware Impact: No size increase. ARM64 offset proof is now literal 4-byte lane order.

## Decision 022 - Event Buffer Readiness Purity

Problem: Deferred event flush readiness used `TryResolveHandle`, returning a writable `NativeArray<T>` view before the actual dequeue write lock.
Solution: Change readiness to `TryReadOnlyHandle`; dequeuing and enqueueing remain under `TryAcquireWriteLock` with `finally ReleaseWriteLock`.
Rejected Alternatives: Treating readiness as harmless would leave an unnecessary writable view outside the mutation lock window.
Scalability potential: Low tier can skip dispatch on stale/contended handles; higher tiers retain exact event delivery without extra gameplay authority routes.
Hardware Impact: Read-only readiness is bounded and avoids write-lock pressure until mutation is required.

## Decision 023 - Native Field Scanner Classification

Problem: The fourth rejection pass used an intentionally strict regex and surfaced the descriptor-backed `NativeArray<T>` expression-bodied accessors as candidates even though they are not C# fields and store no native pointer.
Solution: Keep the code unchanged and classify those 33 members separately as `vaultDescriptorPropertyAccessors`; the actual C# native field declarations remain only the 26 `AtmosphereStepJob` fields, all transient job views.
Rejected Alternatives: Renaming all accessors to methods would create high-risk mechanical churn without removing stored native state; treating accessors as persistent fields would falsify the C# field count.
Scalability potential: Weak, middle, high, and ultra tiers keep the same DataVault handle authority route. The proof artifact now separates stored fields from non-storing accessors.
Hardware Impact: No runtime change. Audit precision improves; no frame-time claim.

## Decision 024 - Writable View Local Resolution

Problem: `dotnet build` proved that writing through descriptor-backed `NativeArray<T>` properties fails with CS1612 because the property return is not a variable.
Solution: Keep descriptors as the only persistent state and resolve writable `NativeArray<T>` views into method-local variables inside the already-owned write phase, job scheduling window, or cold clear/seed route before index assignment.
Rejected Alternatives: Restoring persistent `NativeArray<T>` fields would compile but violate the memory sovereignty mandate; rewriting unrelated world/vegetation compile failures is outside agent 1323 domain.
Scalability potential: Weak, middle, high, and ultra tiers keep the same DataVault route. The local-view fix changes compile semantics only; it does not add a quality fork.
Hardware Impact: No additional managed allocation. Local `NativeArray<T>` structs are stack values; repeated property resolutions are reduced in patched write methods.
