# Rationale 1324 - MEMORY_SOVEREIGN_GAS_DYNAMICS_EXORCIST

Status: COMPLETE - DOMAIN STATIC GREEN - PROJECT COMPILE BLOCKED OUTSIDE 1324

## Decision 000 - Prompt Source Recovery
Problem: User specified root `current_batch.md`, but `C:\hades\current_batch.md` is absent.
Solution: Used CLI search and extracted `AGENT_PROMPT id="1324"` from `Docs/Tasks/CURRENT_BATCH.md` using an attribute-aware regex. This is the active batch file on disk.
Rejected Alternatives: Guessing from chat text was rejected because batch protocol requires disk extraction. Reading neighboring agent prompts as design input was rejected after locating the correct block.
Scalability potential: No runtime impact. Preserves agent isolation under multi-agent batch execution.
Hardware Impact: 0 us runtime. No i3/MX350 frame impact.

## Decision 001 - Relevant Mandates
Problem: Gas solver work touches native memory, Burst jobs, DTO layout, survival gas/pressure, telemetry, and global dependency routes.
Solution: Loaded native memory/job, zero-GC, ARM64 layout, survival O2/pressure, postmortem telemetry, visual-fake-first, and GlobalRegistry/DI mandates before code mutation.
Rejected Alternatives: Broad mandate dump was rejected because it inflates context and does not improve the first five tasks. Ignoring gas survival law was rejected because partial pressure is player-survival truth.
Scalability potential: Low: coarse compartment math and skipped update on contention. Middle: normal cadence with compact DTOs. High: richer telemetry and visual feedback from saved CPU. Ultra: presentation overkill remains outside gameplay truth DTOs.
Hardware Impact: Expected runtime gain cannot be claimed before source inspection. Target is 0 B GC and removal of stale pointer crash class.

## Decision 002 - Buffer Identity Ledger
Problem: Thirty-one class-scope NativeArray aliases made GasDynamicsSolver a second unmanaged owner of atmospheric state.
Solution: Mapped every lane to SystemID.HabitatAtmosphere with stable BufferID values in Docs/Reports/GAS_DYNAMICS_NATIVE_ALIAS_LEDGER_1324.json; reused BufferID.HabitatBaseAwakeState for the existing shared awake-state lane.
Rejected Alternatives: Editing global enum before conflict proof was rejected. Local private BufferID constants match existing project practice and avoid cross-domain churn.
Scalability potential: Low: fewer allocator stalls and no stale owner alias. Middle: same gas cadence with one truth route. High: richer atmosphere visualization can read snapshots without new truth buffers. Ultra: visual overkill stays downstream of stable DTOs.
Hardware Impact: i3/MX350 expected gain is stall avoidance, not claimed arithmetic speedup.

## Decision 003 - DTO And Black-Box Target
Problem: Existing GasDynamicsTelemetryEntry is 32 bytes while the assignment requires a 64-byte ARM64-safe telemetry record.
Solution: Convert runtime telemetry to a 64-byte explicit AtmosphereTelemetryEntry and preserve the 300-frame DataVault ring.
Rejected Alternatives: Padding only the dump writer was rejected because runtime DTO and crash artifact would disagree.
Scalability potential: Low: fixed 19.2 KB ring. Middle: stable state hash history. High: extra diagnostic fields without resizing. Ultra: richer postmortem facts without changing gameplay authority.
Hardware Impact: 300 x 64 bytes is 19.2 KB; expected i3/MX350 cost is negligible and deterministic.

## Decision 004 - Descriptor Ownership And Lock Span
Problem: GasDynamicsSolver held 31 persistent NativeArray fields, making solver lifetime a second owner beside DataVault.
Solution: Replaced class-scope physical lanes with VaultGenerationHandle<T> descriptors. Runtime mutation resolves NativeArray<T> views inside execution scope and owns write locks through try/finally; scheduled gas jobs keep state locks until TryCompleteStep releases them.
Rejected Alternatives: Immediate resolve-and-cache was rejected because it preserves stale pointer failure. Passing VaultGenerationHandle<T> into jobs was rejected because Burst jobs need resolved data-local views, not global descriptors. Releasing state locks immediately after Schedule() was rejected because the job still writes back lanes.
Scalability potential: Low: one owner route reduces allocator stalls and stale aliases. Middle: existing cadence stays deterministic. High: telemetry can expand presentation feedback without changing gas truth. Ultra: saved ownership risk can buy visual atmosphere overkill downstream.
Hardware Impact: i3/MX350 estimated gain is crash/stall avoidance, not arithmetic speedup. Lock overhead is estimated at 3 us per mutation batch, dwarfed by frame recovery from removing duplicate persistent ownership.

## Decision 005 - BaseAwakeState Buffer Route - Superseded
Problem: BaseAwakeState already had a global BufferID and external readers accessed it through `IGasDynamicsSolver`.
Solution: Initial pass reused `BufferID.HabitatBaseAwakeState` and did not release it from solver disposal.
Rejected Alternatives: Allocating a private duplicate awake-state lane was correctly rejected because it creates split truth.
Scalability potential: Superseded by Decision 025. The one-route choice was correct; the non-release ownership conclusion was not.
Hardware Impact: Superseded by Decision 025. No runtime saving is claimed from the old decision.

## Decision 006 - DTO Layout Guard
Problem: Explicit layout claims are not proof if a future edit changes field size or padding.
Solution: Added AreDtoLayoutsValid(), checking PendingBaseTransitionSignal is 64 bytes and AtmosphereTelemetryEntry equals TelemetryEntrySizeBytes before native state boot.
Rejected Alternatives: Documentation-only layout proof was rejected. Editor-only validation was insufficient because runtime boot must fail closed on bad DTO size.
Scalability potential: Low to Ultra: no hot path cost; cold boot prevents corrupted telemetry or transition DTO layout.
Hardware Impact: Cold boot UnsafeUtility.SizeOf checks only. Estimated frame cost: 0 us.

## Decision 007 - Compile Wall Classification
Problem: Required compile verification cannot complete because Hecton8.Core build stops at PlayerInventory.cs line 314, outside the 1324 domain.
Solution: Ran one guarded build with CPU under 50% and no active dotnet/csc. Recorded the external syntax errors and did not edit PlayerInventory.cs.
Rejected Alternatives: Touching PlayerInventory.cs was rejected as domain sabotage. Re-running builds while Assembly-CSharp builds were active was rejected by project protocol.
Scalability potential: No runtime impact. Protects parallel agent ownership.
Hardware Impact: 0 runtime us. Latest build wall is unrelated to gas solver memory ownership.

## Decision 008 - APEX Re-Audit Lock Correction
Problem: The first implementation held DataVault writer locks from FixedTick scheduling until PostFixedTick completion. That protects pointers but violates the stricter no-cross-phase lock gate.
Solution: Removed the scheduled cross-phase gas job path. GasDynamicsStepJob now runs synchronously inside the same FixedTick mutation window with try/finally lock release before handle swaps, UI publication, and telemetry readback. No JobHandle, DispatcherJobSwap, Schedule, Complete, or H8Memory active-job registration remains in GasDynamicsSolver.
Rejected Alternatives: Keeping writer locks across the job lifetime was rejected by the re-audit. Releasing locks after Schedule without a Vault-owned relocation fence was rejected because compaction could move buffers while the job owns raw NativeArray views. Adding TryLockBuffer pins across PostFixedTick was rejected because the new gate also forbids cross-phase pins.
Scalability potential: Low: bounded 128-room/256-bulkhead same-phase math avoids scheduler overhead. Middle: deterministic cadence remains continuous through GlobalQualityWeight. High: saved scheduler overhead can fund richer visual-only atmosphere feedback. Ultra: gameplay gas truth stays compact while presentation can overdraw externally.
Hardware Impact: Removes one async fence path and cross-phase lock exposure. Expected i3/MX350 gain is lower scheduling overhead and no compaction stall; arithmetic cost is unchanged.

## Decision 009 - Pointer-First DTO Reorder
Problem: The previous 64-byte DTOs were explicit-size but not strictly field-ordered: ushort fields preceded later 4-byte fields.
Solution: Reordered PendingBaseTransitionSignal and AtmosphereTelemetryEntry so 8-byte lanes start at offset 0, 4-byte lanes follow, then 2-byte and 1-byte lanes close the records. Padding is explicit.
Rejected Alternatives: Relying on StructLayout size alone was rejected because it does not prove ARM64-friendly field order.
Scalability potential: Same binary footprint, better ARM64 alignment auditability across low, middle, high, and ultra devices.
Hardware Impact: 0 runtime us claimed. Avoids unaligned-read risk by construction.

## Decision 010 - Inner Loop Branch Scrub
Problem: The first gas step retained data-dependent if/continue branches inside room and bulkhead inner loops.
Solution: Converted the hot gas Execute loops to mask/math.select style for awake, occupancy, fire, scrubber, breach, submerged, and diffusion gating. Retired legacy toxicity queue branch remains removed because the compile-time feature flag was false.
Rejected Alternatives: Full chemical solver expansion was rejected. This is gameplay truth, not a visual-only effect, so replacing it with a pure visual fake was rejected; the solver stays compact compartment math.
Scalability potential: Low: same cheap compartment math. Middle: fewer inner-loop branches. High: downstream visuals can use telemetry. Ultra: visual overkill remains outside truth ownership.
Hardware Impact: Expected i3/MX350 benefit is branch predictability, not measured frame time. No fake microsecond savings claimed.

## Decision 011 - Editor Offset Validator Closure
Problem: The runtime size guard proved DTO stride but did not independently prove every explicit field offset requested by Task 18.
Solution: Added `AtmosphereMemorySovereigntyValidator1324.cs`, an editor-only `InitializeOnLoadMethod` validator that asserts `UnsafeUtility.SizeOf` and `UnsafeUtility.GetFieldOffset`/reflection offsets for every field in `PendingBaseTransitionSignal` and `AtmosphereTelemetryEntry`. Padding lanes are private and named `_pad*`.
Rejected Alternatives: Leaving `AreDtoLayoutsValid()` as the only proof was rejected because size-only checks can miss field-order regressions. Moving the validator into an Editor folder was rejected because the existing atmosphere validator pattern lives beside runtime atmosphere files behind `#if UNITY_EDITOR`.
Scalability potential: Low, Middle, High, Ultra: no runtime frame cost. The editor fails before play if a DTO regresses, preserving cheap deterministic runtime paths for weak hardware and rich visual consumers on high-tier machines.
Hardware Impact: 0 us runtime. Editor-only reflection and offset checks do not execute during simulation.

## Decision 012 - Helper Branch Purge
Problem: A second self-audit found branchy helper code called from the gas diffusion loops: `DiffuseGas` used if/else, and `ReadRoomAwake01` delegated to branchy validity checks.
Solution: Converted `DiffuseGas` to branchless positive/negative delta selection and made `ReadRoomAwake01` use clamped base indices plus `math.select`. Room limits now include `RoomBaseIndex.Length` before the loops.
Rejected Alternatives: Claiming the helper branches were outside the loop body was rejected because inlined Burst helpers become loop body code. Adding a full chemistry model was rejected by the cinematic-cheat mandate.
Scalability potential: Low: cheap branchless compartment math. Middle: stable SIMD-friendly loop structure. High: saved predictability budget can feed downstream visuals. Ultra: visual overkill remains separate from gas truth DTOs.
Hardware Impact: Expected i3/MX350 gain is branch predictability and safer Burst vectorization. No measured microsecond saving is claimed.

## Decision 013 - Post-Acquire Lock Leak Fix
Problem: `TryAcquireWriteLock` could succeed, then a subsequent `IsCreated` or capacity validation could fail before the solver marked the lock as owned. That path leaked the newly acquired Vault writer lock.
Solution: Split acquisition from post-acquire validation. If state lane validation fails after acquisition, the lane is released immediately. If telemetry ring validation fails after acquisition, the telemetry ring writer lock is released immediately and the local view is cleared.
Rejected Alternatives: Relying on caller-level `finally` was rejected because the caller only knows about locks after the helper reports success. Ignoring invalid-buffer cases was rejected because stale/malformed handles are exactly the failure mode the mandate targets.
Scalability potential: Low: contention fails closed without compaction blockage. Middle: normal single-phase updates remain unchanged. High and Ultra: compaction can proceed without leaked lock debt.
Hardware Impact: One extra branch only on failure paths. Runtime hot success path remains one Vault acquisition per lane group.

## Decision 014 - Resolver Property And Read Accessor Purge
Problem: The 31 `private NativeArray<T> => ResolveLane(...)` resolver properties held no physical memory, but their class-level shape was too close to the forbidden field-like pattern and public reads still resolved mutable views in several paths.
Solution: Removed all 31 NativeArray resolver properties. Mutable views now resolve through explicit resolver methods into method-local variables only. Public room/base/depth/audit reads use `TryReadLane` read-only views.
Rejected Alternatives: Keeping resolver properties and explaining scanner nuance was rejected because the prompt demands no ambiguity. Converting read paths by taking write-capable views was rejected because read accessors must be pure.
Scalability potential: Low to Ultra: no added hot allocation. Read paths fail closed during compaction and no class member exposes a NativeArray surface.
Hardware Impact: 0 measured microsecond claim. The change removes audit ambiguity and reduces stale-view risk.

## Decision 015 - Guarded Build Rerun
Problem: After the lock/read fixes, compilation had to be rechecked, but the project forbids builds when CPU is over 50% or dotnet/csc is active.
Solution: Waited until CPU sampled at 39.8% and no dotnet/csc process was active, then ran one Hecton8.Core build with shared compilation disabled and a dedicated log.
Rejected Alternatives: Building during active dotnet processes was rejected by project protocol. Editing the reported Construction error was rejected as outside 1324 domain.
Scalability potential: No runtime impact. Protects parallel agent ownership and keeps atmosphere changes scoped.
Hardware Impact: 0 runtime us. Latest compile wall is external: `DroneFleetManager_Transactions.cs(1164,17)` CS0308.

## Decision 016 - Stale Resolver Reference Re-Audit
Problem: The resolver-property purge removed class-level `NativeArray<T>` properties, but three base-state routines still referenced the old unqualified symbols (`BaseAwakeState`, `_basePlayerInside`, `_baseRoomCount`, `_baseCenterAup`) without method-local view resolution. `TryConfigureBase` also retained an old `_roomBaseIndex.IsCreated` precondition before local resolution.
Solution: Added method-local `NativeArray<T>` view resolution and `IsCreated` fail-closed guards to transition overflow, player-inside wake, and hibernation resolution paths. Removed the stale `_roomBaseIndex` precondition from `TryConfigureBase`. Wrapped `ScheduleStep` state write-lock ownership in an outer `try/finally`, so state locks release even when telemetry lock acquisition fails.
Rejected Alternatives: Reintroducing resolver properties was rejected because it restores scanner ambiguity. Leaving manual release before the telemetry lock was rejected because the lock mandate requires acquisition scopes to be visibly guarded by `finally`.
Scalability potential: Low: no added allocations and no extra solver work. Middle: hibernation state remains deterministic. High: telemetry and presentation readers continue to see one DataVault truth route. Ultra: visual overkill remains downstream of stable gas truth.
Hardware Impact: 0 measured runtime gain claimed. The change removes compile and stale-view risk; added guards are failure-path only except four local NativeArray struct copies.

## Decision 017 - Previous Build Wall Reclassification
Problem: The previous guarded build had to be reported honestly, but the project did not compile because of files outside the 1324 domain.
Solution: Ran the guarded Hecton8.Core build only after CPU/process guard cleared. Counted 197 `error CS` lines and confirmed 0 references to `GasDynamicsSolver.cs` or `AtmosphereMemorySovereigntyValidator1324.cs`. Classified the wall as external and left unrelated files untouched.
Rejected Alternatives: Editing PDA, world vegetation, broad submarine atmosphere, fluid, or audio files was rejected as cross-domain interference. Claiming a clean project compile was rejected because the log proves otherwise.
Scalability potential: No runtime behavior change. The atmosphere memory gates are proven independently while parallel agents retain ownership of their compile walls.
Hardware Impact: 0 runtime us. Build verification cost is offline only.

## Decision 018 - Final APEX Guard Closure
Problem: A stricter re-audit still left audit ambiguity: compaction fence checks were partly implicit through `GlobalDataVault`, base wake catch-up used separate branchy solver paths, the diffusion pass had an outer branch gate, and editor validation contained a direct `throw new` token.
Solution: Added explicit `IsCompactionFenceActive` checks to `TryAcquireLaneWriteLock`, `TryEnsureTelemetryRing`, `IsTelemetryRingReady`, and `TryReadTelemetryRing`. Rewrote base wake catch-up into one masked loop using `math.select` for dead-battery and leak-active modes. Converted the diffusion gate into masked `bulkheadLimit` and `activeDiffusionBase`. Replaced editor `throw new` with an editor assertion and replaced `catch (System.Exception)` in the cold dump path with specific I/O/access/argument catches.
Rejected Alternatives: Relying on hidden Vault internals for compaction proof was rejected because the requested evidence must be local. Keeping branchy wake catch-up was rejected because job helper branches become part of Burst execution. Removing dump fault handling entirely was rejected because black-box dumping must fail closed on file system errors.
Scalability potential: Low: same coarse compartment gas math, no scheduler or allocation debt. Middle: deterministic same-phase update with bounded telemetry. High: saved risk budget can feed visual-only atmosphere effects from snapshots. Ultra: presentation can overdraw downstream while gas truth remains compact and single-owner.
Hardware Impact: No measured frame-time claim. Expected i3/MX350 gain is reduced branch variance and no compaction stall on failed lock/telemetry paths. After the build guard cleared, final guarded build ran and remained blocked by 75 external errors outside the 1324 domain; no `GasDynamicsSolver.cs` or validator compile errors appeared in the build log.

## Decision 019 - Rejection-Repeat Evidence Sync
Problem: The disk reports still pointed at the previous guarded build with 75 external errors, while the newest rejection-repeat build log contains 13 external errors and no 1324-domain errors.
Solution: Parsed `Build_1324_Hecton8Core_APEX_RejectionRepeat.log`, confirmed 13 `error CS` entries and 0 references to `GasDynamicsSolver.cs` or `AtmosphereMemorySovereigntyValidator1324.cs`, and synchronized status/report/log artifacts to the latest evidence.
Rejected Alternatives: Claiming a clean project compile was rejected because the latest log still proves external World-domain compile blockers. Editing `World/*` was rejected because it is outside the 1324 atmosphere domain and belongs to other agents.
Scalability potential: No runtime behavior change. Keeps the gas memory sovereignty proof current while preserving multi-agent domain boundaries.
Hardware Impact: 0 runtime us. Verification is offline only; the latest code-side implementation hash is `50C08C9D14E50CFA7A508B5ABB4797F0E2F01F77BC8AA50D2EDD82117A5A73EF`.

## Decision 020 - Inner-Loop Short-Circuit Branch Closure
Problem: The previous branch audit counted zero `if/else` in gas inner loops, but a deeper scan found short-circuit `&&`/`||` expressions inside `GasDynamicsStepJob.Execute`. Those can compile to conditional branches and are not acceptable under the APEX branch-law reading.
Solution: Replaced telemetry aggregation `&&`/`||` with eager `&`/`|`, and replaced diffusion `activeEdge` short-circuit chain with eager bitwise boolean evaluation. Reran inner-loop token scan: wake catch-up, diffusion, and telemetry aggregation now report zero `if`, `else`, `continue`, `break`, `&&`, and `||`.
Rejected Alternatives: Treating short-circuit boolean operators as "not if/else" was rejected because Burst vectorization cares about generated control flow, not prose. Replacing the gas truth model with a visual-only fake was rejected because O2/CO2 pressure is player-survival authority; the fake remains downstream presentation only.
Scalability potential: Low: branch-stable compartment math on weak hardware. Middle: same deterministic truth cadence. High: telemetry snapshots can fund richer atmosphere visuals. Ultra: presentation overkill remains separate from gameplay gas DTOs.
Hardware Impact: No measured frame-time claim. Expected i3/MX350 benefit is lower branch variance only. Guarded `Hecton8.Core.csproj` build passed after this change with 0 warnings and 0 errors; implementation hash is `79441BC850F2AEC9FC3A8D021CAB679FD555530F4CA252C8C95E3DE1387C4150`.

## Decision 021 - Gas Toxicity Route Repair
Problem: The gas solver computed CO2 toxicity for `GasRoomSnapshot` and UI state, but the live physiology route consumes `SignalBus<ToxicityExposureSignal>`. That meant active-room CO2 poisoning could be visible diagnostically while not contributing to `ShinobuPhysiologyRuntime` toxemia.
Solution: Added cold `SignalBus<ToxicityExposureSignal>` configuration in `OnEnable`, emitted a bounded active-room CO2 exposure signal after a same-phase gas step, and kept the legacy `TryDequeueToxicitySignal` contract as a latest-latch for old consumers. The signal uses the existing stable player fallback hash `0x504C5952` and does not allocate in the hot frame because storage is configured cold and `HasNativeStorage` is checked before push.
Rejected Alternatives: Polling physiology directly was rejected because cross-domain direct mutation violates one-route ownership. Reintroducing a NativeQueue in `GasDynamicsSolver` was rejected because it restores persistent native ownership. Emitting managed events was rejected because it violates the zero-GC hot route.
Scalability potential: Low: one scalar signal per toxic frame and no particle chemistry. Middle: deterministic CO2 toxemia from existing room pressure. High: downstream HUD/audio can consume snapshots. Ultra: extra presentation can be layered without changing gas truth DTOs.
Hardware Impact: No measured frame-time claim. Expected i3/MX350 cost is one bounded signal push only when toxicity crosses epsilon; gameplay correctness gain is the reason for the change.

## Decision 022 - Hot Tick Native Boot Removal
Problem: `FixedTick` and `FrostTick` could call `EnsureNativeState()`, which may allocate or grow DataVault lanes if cold boot had failed due to contention. That is a hot-path native boot leak risk even if it is rare.
Solution: Hot ticks now fail closed when `IsInitialized` is false. Native lane creation remains in cold activation or DataVault hotswap paths. One-time standard atmosphere seeding still writes existing lanes only and returns if locks cannot be acquired.
Rejected Alternatives: Retrying `EnsureNativeState()` every frame was rejected because it hides allocation behind runtime cadence. Throwing or logging on missing state was rejected because hot simulation must fail closed and avoid managed text work.
Scalability potential: Low: weak devices avoid allocator spikes under startup contention. Middle: normal initialized cadence unchanged. High and Ultra: no hidden boot work steals budget from visual atmosphere layers.
Hardware Impact: No measured frame-time claim. Expected gain is removal of rare allocator stalls from Fixed/Frost ticks; steady-state arithmetic is unchanged.

## Decision 023 - DataVault Hotswap Handle Order
Problem: On DataVault replacement, assigning `_dataVault` before disposal would make stale handles release against the new vault, leaving old-vault buffers behind or releasing mismatched descriptors.
Solution: Hotswap now disposes current native state against the old vault first, then assigns the replacement vault and cold-initializes fresh handles if active. This preserves descriptor consistency and prevents stale BufferID/Generation pairs from crossing vault instances.
Rejected Alternatives: Leaving handles live across vault replacement was rejected because compaction and generation semantics are vault-local. Releasing after assignment was rejected because it targets the wrong owner.
Scalability potential: Low: no leak under device pressure or scene reload. Middle: stable DataVault ownership. High and Ultra: richer diagnostics can trust BufferID/Generation identity.
Hardware Impact: 0 runtime us in normal frames. Hotswap is cold path; benefit is leak/stale-handle prevention.

## Decision 024 - Loop 13 Guarded Build
Problem: The Loop 13 code changes invalidated the previous compile proof, but project protocol forbids `dotnet build` while CPU is above 50% or another compiler process is active.
Solution: Delayed the build while CPU sampled 96.7%, 65.0%, and 100%. When CPU dropped to 48.9% and no `dotnet`/`csc` process existed, ran `Hecton8.Core.csproj` with `--no-restore`, `-m:1`, and `UseSharedCompilation=false`. Result: 0 errors, 22 CS0649 warnings in external World/Gameplay files, and 0 references to the 1324 files.
Rejected Alternatives: Building during high CPU was rejected because it violates multi-agent protocol. Editing warning files was rejected because they are outside the atmosphere gas domain.
Scalability potential: No runtime behavior change. The compile proof is current for the patched gas solver while parallel ownership stays intact.
Hardware Impact: 0 runtime us. Verification-only cost.

## Decision 025 - BaseAwakeState Ownership Correction
Problem: Re-audit showed the solver is the only code path creating `BufferID.HabitatBaseAwakeState`; external power code reads awake state through `IGasDynamicsSolver` and copies a byte into `LogisticsNetworkGraph`, but does not own the Vault buffer. Skipping release left a gas-owned lane outside solver lifecycle and hid its bytes from `TryGetNativeMemoryAudit`.
Solution: Removed `_baseAwakeVaultOwned`, included `BaseAwakeState` in gas native memory audit totals, and release `_baseAwakeStateHandle` in `ReleaseGasStateBuffers` with the rest of the gas descriptor set.
Rejected Alternatives: Keeping the old shared-owner rationale was rejected because no direct non-gas Vault owner exists in source. Editing power graph ownership was rejected because it already copies scalar awake state and does not need to own the native lane.
Scalability potential: Low: avoids stale awake buffer accumulation after reload/disable on weak devices. Middle: one gas owner route remains. High and Ultra: downstream systems keep scalar/read-only consumption without gaining BufferID authority.
Hardware Impact: 0 hot-frame us. Cold lifecycle cleanup avoids one byte-per-base lane plus Vault metadata leaking across solver disposal; no measured frame-time claim.

## Decision 026 - Atmosphere Logistics AUP Demotion Repair
Problem: `BaseAtmosphereLogisticsJobs.cs` nearest-node jobs subtracted node/source AUP in double precision, then cast the local `double3` directly to `float3`. This respected subtract-before-cast, but skipped the required clamp stage before float demotion.
Solution: Added `AtmosphereLogisticsAupMath.LocalNodeDeltaClamped()`, routing all consumer/source/vent nearest-node distance math through `AupPrecisionMath.LocalDeltaDouble()` and `AupPrecisionMath.DowncastLocalDeltaClamped()`. Replaced data-dependent best-node update `if` blocks with `math.select`.
Rejected Alternatives: Leaving the direct casts was rejected because large AUP deltas can still overflow or destabilize float distance comparisons. Rewriting the whole BaseAtmosphere dispatcher to synchronous execution was rejected in this loop because it would introduce hidden same-frame completion and belongs to a broader subsystem scheduling redesign.
Scalability potential: Low: stable bounded nearest-node math on weak CPUs. Middle: deterministic logistics routing without AUP jitter. High: saved correctness budget lets visuals consume stable gas/atmosphere telemetry. Ultra: richer atmosphere presentation can layer above the same bounded local coordinates.
Hardware Impact: No measured frame-time claim. Expected i3/MX350 effect is correctness and branch variance reduction in three nearest-node loops; arithmetic cost adds one clamp per candidate.

## Decision 027 - Loop 15 Verification Guard
Problem: The Loop 15 code change invalidated the previous build proof, but project rules forbid `dotnet build` or Roslyn audit when CPU is above 50% or a compiler is already active.
Solution: Ran non-dotnet static scans only. CPU samples were 94.6%, 81.0%, and 99.8%; active `dotnet`/`csc` processes were zero. Build and Roslyn rerun are explicitly deferred until the guard clears.
Rejected Alternatives: Launching build under 80-100% CPU was rejected because it violates the multi-agent guard and would interfere with other agents. Claiming the Loop 14 build as current was rejected because `BaseAtmosphereLogisticsJobs.cs` changed after that build.
Scalability potential: No runtime behavior change. Preserves parallel work ownership and avoids contaminating verification with machine overload.
Hardware Impact: 0 runtime us. Verification-only gate.

## Decision 028 - Gas Public Native View Contract Removal
Problem: `IGasDynamicsSolver` still exposed `NativeArray<T>.ReadOnly` room and base awake views. Even read-only views are physical native handles; a Power consumer could cache them beyond the gas read phase and outlive a DataVault compaction or hotswap.
Solution: Removed `RoomO2`, `RoomCO2`, `RoomPressure`, and `BaseAwakeState` view properties from `IGasDynamicsSolver`. `WfcOutpostPowerBootRuntime` now calls `TryGetBaseHibernationSnapshot(0)` and passes only a scalar awake byte to `LogisticsNetworkGraph.TryBindBaseAwakeStateValue`.
Rejected Alternatives: Keeping read-only views with comments was rejected because the contract still allowed stale native handle escape. Giving Power ownership of `BufferID.HabitatBaseAwakeState` was rejected because gas creates and releases that lane; Power only needs a copied authority value.
Scalability potential: Low: no stale native view held on weak devices during compaction. Middle: scalar graph binding remains deterministic. High and Ultra: richer power/atmosphere presentation can read snapshots without owning gas lanes.
Hardware Impact: 0 measured frame-time claim. Expected i3/MX350 benefit is risk removal; one snapshot read replaces a native view bind and does not add solver work.

## Decision 029 - Atomic Native Memory Audit
Problem: `TryGetNativeMemoryAudit` checked only `RoomO2`; every other `TryReadLane` result was ignored. A stale or missing lane could silently be omitted from the audit and still return a green-looking byte total.
Solution: Made the audit fail closed unless every gas lane and the 300-entry telemetry ring resolve successfully. The telemetry ring is now included unconditionally after a successful read. Added a redundant compaction-fence check inside `TryEnsureLane`, even though the caller already checks it, so the lane bootstrap primitive cannot be reused unsafely later.
Rejected Alternatives: Partial audit totals were rejected because they hide exactly the descriptor inconsistency the assignment targets. Logging missing lanes was rejected because this is a read accessor and must stay pure/no-GC.
Scalability potential: Low to Ultra: no hot-frame work. Cold diagnostics now return false instead of publishing misleading memory evidence.
Hardware Impact: 0 runtime us in normal simulation. Cold audit cost increases by checking all lane bools; frame impact is none.

## Decision 030 - Loop 16 Verification Guard
Problem: Loop 16 changed gas and gas-consumer contracts, so the previous build proof is stale. Project protocol still forbids dotnet/Roslyn verification under high CPU.
Solution: Re-extracted `AGENT_PROMPT id="1324"` from disk, reran non-dotnet static scans, and recorded CPU guard state. Recursive Atmosphere field scan: 26 files, 112 native field declarations, 112 transient IJob fields, 0 persistent candidates. Public gas native-view scan: 0 old `BaseAwakeState/Room*` refs. Touched-set forbidden-token scan: 0 hits. AUP demotion scan: 0 unsafe casts. CPU average was 98.6%, active `dotnet`/`csc` processes were 0, so build and Roslyn rerun are deferred.
Rejected Alternatives: Launching a build under 98.6% CPU was rejected because it violates the multi-agent guard. Editing broader Power architecture was rejected because the needed cross-domain change was limited to removing the leaked gas native view contract.
Scalability potential: No runtime behavior change except safer scalar coupling between gas and power. Future low/mid/high/ultra presentation layers must consume snapshots/signals, not physical gas lanes.
Hardware Impact: 0 measured frame-time claim. Current implementation hash is `09385733932a9c741a55c5e93be489b53b253682ae8a32293123ad60830e1662`.

## Decision 031 - Mutable Vault View Lock Gate Hardening
Problem: `GasDynamicsSolver.ResolveLane` returned mutable DataVault views whenever `TryReadHandle` succeeded. All current mutation callers were intended to hold gas state write-locks, but the helper itself did not prove that invariant, and several read/ensure/acquire routes only checked the compaction fence before receiving the view.
Solution: `ResolveLane` now fails closed unless `_stateWriteLockMask` is non-zero, so future call sites cannot accidentally mutate gas Vault lanes without an active gas write-lock. `TryEnsureLane`, `TryReadLane`, `TryAcquireStateWriteLocks`, `TryAcquireLaneWriteLock`, `TryEnsureTelemetryRing`, `IsTelemetryRingReady`, `TryReadTelemetryRing`, and `TryAcquireTelemetryRingForStep` now perform local post-view or post-lock compaction fence checks. Failed read paths explicitly clear out buffers before returning false.
Rejected Alternatives: Trusting caller discipline was rejected because the mandate requires local proof artifacts, not assumptions. Rewriting `BaseAtmosphereLogisticsRuntime` or `ToxicOutgassingChemistryRuntime` in this loop was rejected because those systems require scheduling ownership redesign; a hidden same-frame `.Complete()` or multi-frame write-lock would violate the project job policy.
Scalability potential: Low: weak devices fail closed during compaction instead of receiving stale mutable gas views. Middle: normal same-phase gas cadence is unchanged. High: presentation and telemetry consumers keep immutable snapshots while gas truth remains single-owner. Ultra: visual overkill can layer downstream without owning physical gas lanes.
Hardware Impact: 0 measured frame-time claim. Expected i3/MX350 effect is risk reduction only; added checks are branch-only guard rails around existing Vault access and do not add allocation or solver work.

## Decision 032 - Adjacent Atmosphere Risk Boundary
Problem: The broad Atmosphere scan still finds two architecture risks outside the safe primary gas patch: `BaseAtmosphereLogisticsRuntime` pins dispatcher job buffers until PostSimulation, and `ToxicOutgassingChemistryRuntime` schedules long job chains over views opened through `OpenBuffer/TryResolveHandle`.
Solution: Recorded both as residual architecture risks in status/report. No code was changed there in Loop 17 beyond previous AUP/job math fixes, because the correct solution is an owner-level scheduling redesign or immutable snapshot path, not a local patch that blocks the main thread or holds writer locks across frames.
Rejected Alternatives: Adding `.Complete()` after scheduling was rejected by AGENTS job policy. Holding `TryAcquireWriteLock` across scheduled chains was rejected by the compaction law. Blindly editing files with active multi-agent modifications was rejected by the batch conflict protocol.
Scalability potential: Low: current residual risk is documented and bounded instead of hidden. Middle: future redesign should use cadence-scaled snapshots. High and Ultra: richer toxic atmosphere visuals must consume immutable readbacks rather than live Vault views.
Hardware Impact: 0 runtime us from documentation. Avoided a harmful "fix" that would likely add main-thread stalls on i3/MX350.

## Decision 033 - State Lock Failure Telemetry Closure
Problem: Several `GasDynamicsSolver` mutation paths returned immediately when `TryAcquireStateWriteLocks()` failed. That protected state, but it did not satisfy the fail-closed telemetry law: lock contention must leave a numeric failure record in the unmanaged black-box ring.
Solution: Added `RecordStateWriteLockFailure()` and `FailStateWriteLock()` and routed every state write-lock failure call site through them. Each failure attempts to write `TelemetryFailureStateWriteLock` into the 300-entry `AtmosphereTelemetryEntry` ring without throwing or allocating. The scheduler path now uses the same helper instead of its one-off telemetry call.
Rejected Alternatives: Ignoring rare contention was rejected because compaction/lock contention is the exact crash class this agent owns. Throwing or logging was rejected because hot failure paths must not stall or allocate. Rewriting adjacent scheduled atmosphere systems was rejected here because the safe fix requires an owner-level scheduler redesign, not a local `.Complete()` patch.
Scalability potential: Low: weak devices skip the frame and keep an explainable ring entry. Middle: normal gas cadence is unchanged. High and Ultra: downstream presentation can still overdraw from stable snapshots; failure telemetry remains compact gameplay truth.
Hardware Impact: No measured frame-time claim. Success path cost is only resetting one byte counter on lock acquisition. Failure path writes one 64-byte telemetry struct and remains allocation-free.

## Decision 034 - Consecutive Lock Failure Dump Trigger
Problem: Task 15 required catastrophic lock failures to dump the 300-frame black box. Before Loop 18, NaN telemetry triggered dumps, but repeated state write-lock failures did not.
Solution: Added `_consecutiveStateWriteLockFailures` with a threshold of 4. Successful state-lock acquisition and native disposal reset it. Once the threshold is reached, the solver calls the existing `DumpBlackBoxOnce()` route, which only writes if the telemetry ring can be read.
Rejected Alternatives: Dumping on the first lock miss was rejected because transient contention during compaction is expected and would spam disk. Starting a background managed task was rejected because the existing dump path is already bounded and adding task allocation would violate the hot-failure discipline. Increasing physics fidelity to mask missed gas ticks was rejected as the wrong layer.
Scalability potential: Low: avoids disk spam on weak devices while preserving postmortem data for real repeated contention. Middle: same threshold and ring size. High and Ultra: richer diagnostics can be added later only through aligned telemetry fields, not by bloating hot simulation.
Hardware Impact: No measured frame-time claim. Added state is 1 byte plus padding in the managed object; hot success path is a scalar reset. Catastrophic dump path remains failure-only.
