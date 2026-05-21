# SHINOBU_278 Rationale - COOP_INPUT_PREDICTION_BUFFER

Status: POLISH STATIC VERIFICATION / COMPILE BLOCKED BY CPU GUARD

## Decision 000 - Scope Admission
Problem: Existing cooperative input prediction route is unknown; implementing a duplicate queue would create two authorities for rollback input truth.
Solution: Read domain, batch prompt, mandatory registry docs, and network/rollback source before code. New route must be Vault-owned, dispatcher-phased, and owner-local until real cross-domain exposure is proven.
Rejected Alternatives: A standalone MonoBehaviour queue was rejected because it would poll globals and bypass DataVault sovereignty. A managed `List<InputState>` replacement wrapper was rejected because it preserves managed history semantics.
Scalability potential: Low uses fixed small native capacity and cheap modulo seek; Middle/High/Ultra increase prediction window/redundancy continuously using latency and `GlobalQualityWeight` without changing DTO truth layout.
Hardware Impact: Expected low-end i3/MX350 gain is elimination of managed input-history allocation spikes; exact microseconds are pending static/compile/runtime proof.

## Decision 001 - Mandate Set
Problem: Task touches input, rollback, networking, native memory, AUP, dispatch phases, and crash telemetry.
Solution: Loaded 8 mandates: zero-GC, ARM64 layout, AUP determinism, native memory/jobs, execution phases, GlobalRegistry DI, network sync/reconciliation, and debug telemetry.
Rejected Alternatives: Reading only network mandate was rejected because DTO layout and DataVault ownership are primary failure modes.
Scalability potential: Mandates force continuous capacity/redundancy curves and 300-frame blackbox rather than binary quality modes.
Hardware Impact: Prevents ARM64 unaligned access traps and avoids hidden main-thread completions on low-end devices.

## Decision 002 - Existing Route Hijack
Problem: Requested folder `Assets/_Project/Scripts/Core/Network/` does not exist; duplicate netcode would violate one-owner routing.
Solution: Bound the new input ring to the existing `Assets/_Project/Scripts/Networking` rollback runtime and `Core/InputDispatcher` PRE_SIMULATION publisher. The route card is `Docs/ARCHITECTURE/SHINOBU_278_COOP_INPUT_PREDICTION_ROUTE_CARD.md`.
Rejected Alternatives: A parallel network queue was rejected because rollback already owns remote packet state and dispatcher already owns raw input phase timing.
Scalability potential: Low writes one 32-byte local input slot per tick; Middle/High/Ultra scale prediction window and redundancy without adding a second authority.
Hardware Impact: i3/MX350 impact is one modulo write plus one 32-byte store per input tick, replacing managed history semantics and avoiding GC spikes.

## Decision 003 - DTO/AUP Split
Problem: The prompt requires `PredictedInputDTO` to be exactly 32 bytes and also asks targeted input AUP `double3` storage; those two requirements cannot coexist in one struct because `double3` alone is 24 bytes.
Solution: Preserved the mandated 32-byte `PredictedInputDTO` ABI and stored targeted AUP as raw `double3` bits in a parallel `PredictedInputAupTargetDTO` ring keyed by the same tick modulo.
Rejected Alternatives: Expanding `PredictedInputDTO` to 56/64 bytes was rejected because it breaks the explicit ARM64 layout contract and remote frame ABI. Truncating target AUP to float3 was rejected because it creates rollback placement drift.
Scalability potential: Low-tier untargeted movement reads one 32-byte ring; Middle/High/Ultra targeted actions pay one extra 32-byte parallel read only when flags require AUP.
Hardware Impact: i3/MX350 avoids unaligned mixed payload reads; high-end devices keep exact AUP placement without widening every input packet.

## Decision 004 - Dear Lie Over Stall
Problem: Missing authoritative input packets previously force either a stall or a sudden correction when packet history catches up.
Solution: `ExtrapolateMissingInputsJob` and `EvaluateInputMismatchJob` synthesize missing remote inputs from the previous tick with exponential decay, then flag `DearLieExtrapolated`.
Rejected Alternatives: Holding the last remote input at full velocity was rejected because it overshoots during packet loss. Freezing remote movement was rejected because it exposes transport jitter.
Scalability potential: Low uses a single decay multiply; Middle/High/Ultra can carry more redundancy while using the same math path.
Hardware Impact: Estimated cost is sub-10us per rollback scan window on i3/MX350 because it is linear over bounded native ring slots and no allocation occurs.

## Decision 005 - Rollback Signal Route
Problem: Mismatch detection cannot mutate dispatcher state directly from a rollback job without breaking authority boundaries.
Solution: Added `RollbackRequiredSignal` and emits through `SignalBus<RollbackRequiredSignal>` while persisting first mismatch buffer id/byte offset in rollback runtime state.
Rejected Alternatives: Direct GlobalRegistry lookups from jobs and managed C# event callbacks were rejected because both are hot-path authority leaks.
Scalability potential: Low-tier lane capacity is 8 per frame; high-end cap is 64. Gameplay truth remains unchanged across quality.
Hardware Impact: NativeQueue bridge is a cold-compatible signal lane; expected low-end cost is lower than managed event dispatch and carries forensic offsets.

## Decision 006 - Verification Boundary
Problem: Runtime compile/build is mandatory, but HECTON-8 rules forbid starting dotnet when CPU load is over 50 percent or `csc.exe` is active.
Solution: Static source scans and JSON proof were completed; dotnet build is held while CPU guard reports 98.74 percent, 85.55 percent, 53.2 percent, 91.87 percent, 85.38 percent, 98.47 percent, 100/86.57/100 percent, and latest series 100/100/100/100/99.42 percent. No `csc.exe` or `dotnet.exe` was running during the latest guard samples.
Rejected Alternatives: Launching dotnet anyway was rejected because it violates the explicit batch protocol and can collide with other agents.
Scalability potential: Static proofs remain deterministic; runtime proof must be captured when the machine is below the build threshold.
Hardware Impact: Avoids starving parallel agents on shared CPU; compile proof remains pending, not fabricated.

## Decision 007 - BufferID Sovereignty Correction
Problem: Subagent audit found the first predicted-input BufferID proposal collided with existing logistics and caustics lanes.
Solution: Moved SHINOBU_278 ownership to central IDs 75000 predicted input, 75001 target AUP, and 75002 input prediction telemetry. Updated code, route card, binary payload ledger, and log proof.
Rejected Alternatives: Keeping the colliding IDs with documentation was rejected because BufferID collision is memory corruption, not a naming problem. Using local casts was rejected because this route is cross-domain rollback infrastructure and must be centrally visible.
Scalability potential: Low/Middle/High/Ultra all consume the same stable IDs; quality changes can tune capacity/cadence but never identity.
Hardware Impact: Prevents low-end devices from reading logistics/caustics memory as input prediction rows, eliminating undefined cache reads and false rollback triggers.

## Decision 008 - Borrowed Snapshot Descriptor Route
Problem: Rollback scheduling used a helper backed by `_vault.TryGetBuffer`, whose current implementation sanitizes payloads and marks external views. That violates read-accessor purity in the fixed schedule path.
Solution: Cache borrowed authoritative snapshot lanes as `VaultGenerationHandle<T>` descriptors during `TryEnsureBuffers`, then resolve schedule-local `NativeArray<T>` views through `TryResolveHandle`.
Rejected Alternatives: Keeping `TryGetBuffer` was rejected because it mutates Vault accounting from a read-looking helper. Reacquiring generation descriptors every schedule was rejected because handle binding belongs to owner/cold setup, not the hot rollback loop.
Scalability potential: Low quality can skip optional Merkle leaves when descriptors are missing; high/ultra can hash more leaves without changing the ownership route.
Hardware Impact: Expected i3/MX350 gain is removal of avoidable sanitize/external-view metadata work in the fixed schedule path, roughly 2-8 us when all borrowed lanes exist.

## Decision 009 - Signal Writer Guard and Layout Offset Guard
Problem: A default `NativeQueue<T>.ParallelWriter` would fail if the rollback signal lane was not initialized, and future private padding offset checks could false-fail because reflection was public-only.
Solution: Added `RollbackSignalsEnabled` after cold SignalBus initialization and guarded job enqueue. Layout guard now resolves public and private instance fields.
Rejected Alternatives: Editing the global `SignalBus<T>` API was rejected as unnecessary compile-wall expansion for this domain. Blind enqueue was rejected because a rare initialization fault would crash inside rollback.
Scalability potential: Signal lane capacity still scales through existing SignalBus configuration; the guard only disables emission if the lane is not ready.
Hardware Impact: One branch only on mismatch emission; no steady-state prediction cost.

## Decision 010 - Rollback Descriptor-Only Vault Route
Problem: The rollback owner still persisted obsolete pointer-bearing `VaultBufferHandle<T>` records and resolved them with `.Resolve(_vault)`. Even though `IDataVault.TryResolveHandle(in VaultBufferHandle<T>)` ignores the cached pointer, storing stale pointers in rollback infrastructure violates the current descriptor doctrine and weakens static proof.
Solution: Migrated every `HectonRollbackNetcodeRuntime` owner lane to `VaultGenerationHandle<T>`. Owner/mutating phases resolve local `NativeArray<T>` views through `TryResolveOwned`; public read accessors use `TryReadOwned` and `IDataVault.TryReadHandle` so read routes stay pure and do not publish fault telemetry.
Rejected Alternatives: Keeping `VaultBufferHandle<T>` as a migration bridge was rejected because SHINOBU_278 owns a new rollback-critical route and should not carry pointer-era state. Editing `GlobalDataVault` was rejected because the required API already exists and broader core changes would widen the compile wall.
Scalability potential: Low/Middle/High/Ultra all use the same 16-byte descriptor ABI; quality changes adjust prediction window, redundancy, and optional Merkle leaf budget without changing ownership or pointer lifetime.
Hardware Impact: Each persisted rollback lane shrinks from a 24-byte pointer-bearing handle to a 16-byte descriptor. The bigger gain is correctness: stale raw pointers cannot survive a Vault generation change, and editor/runtime read probes avoid avoidable fault-accounting side effects.

## Decision 011 - Rollback Truth Is Not Quality-Gated
Problem: Subagent audit found look mismatch rollback truth was gated by `math.step(minQuality, GlobalQualityWeight)`, making authoritative rollback behavior depend on quality. That violates the doctrine that `GlobalQualityWeight` may scale cadence, capacity, and telemetry, but not gameplay truth.
Solution: `RollbackNetcodeMath.ShouldRollback` now treats any mismatch bit as rollback-relevant. Quality still influences severity/cost curves, prediction window, resend redundancy, and optional Merkle leaf budget, but it cannot suppress a detected authoritative mismatch.
Rejected Alternatives: A smoother threshold was rejected because any threshold still changes truth by quality. Ignoring look mismatches entirely was rejected because it creates silent divergence for view-dependent tools and targeted interactions.
Scalability potential: Low-tier devices reduce look rollback cost through continuous severity and smaller windows; High/Ultra retain wider lookback and richer telemetry. The rollback truth route stays invariant.
Hardware Impact: One branch chain is simplified; estimated cost improvement is negligible but correctness removes a cross-device divergence class.

## Decision 012 - Cached Rollback Signal Writer
Problem: Schedule-time `SignalBus<RollbackRequiredSignal>.ParallelWriter` property access re-entered the legacy writer-open facade every fixed schedule, including `EnsureInitialized` and open accounting. The enqueue guard did not prevent writer acquisition overhead.
Solution: Cache the `NativeQueue<RollbackRequiredSignal>.ParallelWriter` once during cold `TryEnsureBuffers` after `SignalBus.Configure/EnsureInitialized`. The runtime now opens the native queue through `OpenQueueForLegacyGlobalSignals()`, verifies `IsCreated`, then calls `AsParallelWriter()` and passes `_rollbackSignalWriter` into the Burst pipeline. Both safety suppressions now carry `SAFETY_JUSTIFICATION_SHINOBU_278`.
Rejected Alternatives: Editing `SignalBus<T>` was rejected as global compile-wall expansion. Keeping the hot property access was rejected because a native writer can be cached after cold lane initialization without changing the signal route.
Scalability potential: Signal lane capacity still scales through the existing `SignalBus.Configure(32, maxFrameSignals:64, lowTierFrameSignals:8)` route; writer caching does not change queue ownership or quality behavior.
Hardware Impact: Removes fixed-schedule legacy-open metadata work, estimated at 0.2-1.0 us on low-end CPUs depending on signal bus checks.

## Decision 013 - Cached Writer Lifecycle Fence
Problem: `SignalBus<T>.Dispose()` resets the underlying queue and `_initialized=false`; a disabled rollback runtime must not retain a stale native writer if the dispatcher later re-enables the instance.
Solution: `OnDisable` now clears `_rollbackSignalWriter` and `_rollbackSignalsReady`. `TryEnsureBuffers` returns through `TryCacheRollbackSignalWriterCold()` when buffers are already present, and that cold helper re-runs the rollback layout guard before opening the queue. `_rollbackSignalsReady` is set only after the returned native queue reports `IsCreated`.
Rejected Alternatives: Adding a public initialized probe to shared `SignalBus<T>` was rejected as a global API compile-wall change. Reopening `SignalBus<RollbackRequiredSignal>.ParallelWriter` every schedule was rejected because it recreates the original hot metadata churn.
Scalability potential: Low/Middle/High/Ultra keep the same signal lane identity and capacity curve. This only protects lifecycle transitions; it does not change authority, DTO layout, or quality behavior.
Hardware Impact: Prevents stale native writer usage after disable/enable while preserving the 0.2-1.0 us schedule-side saving from writer caching.

## Decision 014 - Look Rollback Slider Is Severity, Not Truth
Problem: Removing the quality gate made the existing `MinQualityForLookRollback` tuning field semantically dangerous: leaving it as a no-op would mislead designers, while using it as a truth threshold would reintroduce quality-dependent rollback.
Solution: The field is kept for ABI/CSV compatibility but is now consumed as `LookMismatchSeverityWeight` in `ResolveMismatchSeverity`. Editor UI labels it `Look severity`; detected look mismatch still always enters rollback truth through `ShouldRollback`.
Rejected Alternatives: Renaming the DTO field was rejected because it changes binary/CSV contract surface for no runtime gain. Keeping the old label was rejected because it suggests quality can disable authoritative look rollback.
Scalability potential: Low quality lowers non-critical severity/cost from a 0.05 base through the continuous quality curve; middle/high/ultra can raise proof richness without changing mismatch ownership or rollback identity.
Hardware Impact: Adds only a few scalar ops on the already-rare mismatch path; estimated cost is 0.02 us while removing a cross-device divergence trap.

## Decision 015 - Inquisition Report Schema Preservation
Problem: The editor scanner could overwrite the richer SHINOBU_278 report section with a reduced schema that omitted explicit BufferID proof, weakening future static evidence after a menu run.
Solution: `Input_Queue_Inquisition` now emits `scannedFiles`, `vaultBuffers`, `bufferIds`, and PASS/FAIL fields matching the current report automation. The scanner still only runs editor-side and does not enter gameplay paths.
Rejected Alternatives: Leaving the current JSON manually richer was rejected because tooling must preserve proof on regeneration. Moving scanner logic into runtime was rejected because file IO/string work belongs in editor diagnostics only.
Scalability potential: No runtime scalability impact. The report preserves low/middle/high/ultra buffer identity proof while quality curves remain in runtime contracts.
Hardware Impact: 0 runtime us; editor-only scan cost is unchanged aside from a few string fragments in generated JSON.

## Decision 016 - Deterministic Idle Ring Initialization
Problem: `NativeArrayOptions.UninitializedMemory` saves cold allocation cost, but a rollback-critical input ring must not expose arbitrary slack rows before the first real input write.
Solution: Added `InitializePredictedInputRingJob`, a deterministic Burst cold-init pass that writes every predicted input slot with its tick, zero movement/look/buttons, and valid predicted flags. InputDispatcher uses it after acquiring deterministic input Vault lanes; rollback fallback uses it only when it created the predicted ring itself.
Rejected Alternatives: Blind `MemClear` was rejected because it leaves every row as tick 0 with no validity flags. Relying on producer writes alone was rejected because rollback can read bounded historical windows before all ring slots have been touched by gameplay input.
Scalability potential: Runtime quality behavior is unchanged. Low/Middle/High/Ultra all start from the same deterministic idle state; quality only changes window/redundancy/severity after boot.
Hardware Impact: Costs one cold 512-row pass when the lane is acquired. Runtime hot path remains one modulo store per local tick and zero managed allocation.

## Decision 017 - Post-Compaction Objective Replay
Problem: Chat compaction can stale or distort task count, API assumptions, and static proof details; finalizing from memory would be a false report.
Solution: Re-extracted the SHINOBU_278 XML block from `Docs/Tasks/CURRENT_BATCH.md` and confirmed 20 tasks. Re-checked `SignalBus<T>` and `IDataVault` definitions against the current source: cached rollback signal writer opens from `OpenQueueForLegacyGlobalSignals()` once the queue is created, and Vault access is descriptor-only through `VaultGenerationHandle<T>`, `TryResolveHandle`, and pure `TryReadHandle`.
Rejected Alternatives: Trusting the summarized context was rejected. Naive brace counting was rejected because editor JSON strings contain literal braces; a code-aware string/comment-skipping scan was used instead.
Scalability potential: No gameplay behavior change. The proof preserves the continuous quality route: quality can tune window, redundancy, severity, and optional telemetry, but not DTO layout or authoritative mismatch truth.
Hardware Impact: Static proof only. Compile remains blocked because the CPU guard crossed 50 percent (`62.53/34.9/66.67`, then `100`); latest process scan reports `dotnet=0` and `csc=0`.

## Decision 018 - CSV Profile Rows Tune Logical Window, Not Physical Ring
Problem: The task requires `netcode_input_profiles.csv` to map connection types to buffer sizes and redundancy multipliers, but physical Vault ring capacity is rollback identity. Reallocating `PredictedInputDTO[512]` from an editor/CSV facade would break snapshot stride and cross-frame handle stability.
Solution: The cold parser now supports `active_profile,<name>`, `key,value`, default/global/generic profile rows, and scoped `connection_profile,key,value` rows. `buffer_capacity`, `buffer_size`, `prediction_window`, and `prediction_window_ticks` all tune `PredictionWindowTicks`, the logical active prediction/search window. Physical ring length remains fixed and visible as a read-only physical capacity label.
Rejected Alternatives: `string.Split`, managed profile objects, and per-profile dictionaries were rejected because they allocate and do not belong in a runtime parser. Hot physical ring reallocation was rejected because it would mutate authority identity and invalidate blind memcpy rollback snapshots.
Scalability potential: Low devices can shrink the logical active window to 5 ticks; middle/high/ultra can expand toward 30 ticks and higher redundancy. The 512-slot ring preserves identical DTO layout and save/network identity across all quality levels.
Hardware Impact: No hot-frame cost. Cold parser remains a single pass over a Vault byte scratch buffer, with scalar FNV-1a hashing and no managed row allocations.

## Decision 019 - Compile Guard Overrides Compile Desire
Problem: The code needs compile proof, but the local machine is still above the explicit build threshold. Starting `dotnet build` while CPU is saturated would violate the batch protocol and interfere with other agents.
Solution: Re-ran non-build verification only: exact managed input queue scan, DTO auto-property/Pack hazard scan, SHINOBU report JSON parse, and `git diff --check` on the touched surface. Build guard samples remained above threshold, with latest post-Dewey sample CPU `100`, `csc.exe=0`, `dotnet.exe=0`; compile remains intentionally unlaunched.
Rejected Alternatives: Running a partial `dotnet build Hecton8.Core.csproj` was rejected because the CPU guard is absolute. Running Unity import/PlayMode checks was rejected for the same reason and because it is heavier than a project compile.
Scalability potential: No runtime behavior change. The static proof preserves the low/middle/high/ultra route: fixed DTO identity, logical quality-scaled window, and no quality-gated rollback truth.
Hardware Impact: Avoids starving low-end/shared CPUs and preserves IO bandwidth for parallel agents. Verification remains static until CPU drops below 50 percent.

## Decision 020 - Safety Suppression Proof Is Reviewer-Readable
Problem: The rollback signal writer requires `NativeDisableContainerSafetyRestriction` because Unity cannot prove the lifetime of a cached `NativeQueue<T>.ParallelWriter` acquired from SignalBus during cold setup. A one-line comment identified the suppression but did not fully prove ownership, guard behavior, and non-aliasing.
Solution: Expanded both SHINOBU_278 safety justifications into three paragraphs: the writer is SignalBus-owned and not a Vault array view; `RollbackSignalsEnabled` prevents default-writer enqueue; the suppression is scoped only to the writer descriptor while Vault arrays retain safety metadata and `[NoAlias]`.
Rejected Alternatives: Removing the cached writer was rejected because it would restore schedule-time SignalBus facade work. Editing global SignalBus safety/lifetime APIs was rejected because SHINOBU_278 can document the narrow suppression locally without widening the compile wall.
Scalability potential: No gameplay behavior change. Low/Middle/High/Ultra still use the same signal route and quality-scaled capacity; this only strengthens static proof for the native queue bridge.
Hardware Impact: 0 runtime us. Keeps the 0.2-1.0 us fixed-schedule metadata saving from cached writer acquisition while making the safety exception auditable.

## Decision 021 - Rollback Binds Input Truth, It Does Not Own It
Problem: Dewey's audit found rollback could create dispatcher input truth buffers if it booted before `InputDispatcher`. That made owner attribution init-order dependent: the same `PredictedInputDTO` ring could be owned by rollback or input depending on scene timing.
Solution: Removed rollback fallback creation for `ShinobuInputJournalRing`, `ShinobuPredictedInputRing`, and `ShinobuPredictedInputAupTargets`. Rollback now only binds existing handles through `TryGetGenerationHandle`; when absent, mismatch jobs receive default arrays and set missing-input-journal flags until the input owner creates the lanes.
Rejected Alternatives: Calling into `InputDispatcher` from rollback to force creation was rejected because it creates a concrete cross-domain dependency and boot-order coupling. Keeping rollback fallback creation was rejected because one fact cannot have two possible owners.
Scalability potential: Low/Middle/High/Ultra use the same route identity. Quality can shrink the active prediction window, but it cannot change owner or creation phase.
Hardware Impact: 0 runtime allocation. The hot path keeps the same modulo ring reads; diagnostics no longer misattribute input truth ownership after unusual boot order.

## Decision 022 - Late Handle Binding Without Hot Creation
Problem: Borrowed snapshot handles and input truth handles were bound once during cold setup. If their owning systems created Vault lanes after rollback reached `_buffersReady`, the rollback pipeline could permanently see default arrays.
Solution: `TryEnsureBuffers()` now retries input-truth and borrowed snapshot handle binding after `_buffersReady` is set. The helper only calls `TryGetGenerationHandle` while the local descriptor is missing, so already-bound lanes do not run the full bind path every fixed tick.
Rejected Alternatives: Reacquiring every handle every schedule was rejected as metadata churn. Creating missing borrowed lanes from rollback was rejected because those buffers have separate owners.
Scalability potential: The retry path only repairs startup ordering. It does not alter quality curves, DTO layout, or mismatch truth.
Hardware Impact: Missing-handle metadata probes are cold/transient; once handles bind, the branch exits on `BufferID != 0`.

## Decision 023 - Central BufferID and Pure Input Reads
Problem: `InputPredictionTelemetry` used a local `(BufferID)75002` cast, and `InputDispatcher` read-named accessors used `TryResolveHandle`. Both weaken static proof: collision scans miss local casts, and read probes can mutate Vault resolution telemetry.
Solution: Added `BufferID.ShinobuInputPredictionTelemetry = 75002` to `H8Memory` and changed rollback telemetry to reference it. Added `TryReadInputBuffer()` in `InputDispatcher` and routed current input DTO, historical input read, button-window check, profile read, and block-mask read through `IDataVault.TryReadHandle`.
Rejected Alternatives: Leaving the cast was rejected because BufferID ownership belongs in the central enum. Renaming all existing write helpers was rejected because mutating producer paths still need `TryResolveHandle`.
Scalability potential: No quality behavior change. This preserves stable identity across low/middle/high/ultra and keeps read facades side-effect-free.
Hardware Impact: Expected 1-4 us avoided in editor/runtime read probes that previously touched resolving metadata; gameplay write cost unchanged.

## Decision 024 - Dear Lie Frame-Zero Underflow Guard
Problem: Missing remote input for frame 0 underflowed `frame - 1u` to `uint.MaxValue`, causing the Dear Lie extrapolator to seed from the last ring slot on the first packet-loss frame.
Solution: Both missing-input extrapolation paths now branch frame 0 to use the current predicted frame as seed with a single-tick decay, avoiding unsigned wraparound.
Rejected Alternatives: Special-casing by clearing the whole remote ring was rejected as unnecessary work. Stalling frame 0 was rejected because the Dear Lie should still hide loss without changing truth.
Scalability potential: The branch only affects missing-packet extrapolation; quality curves still tune window/redundancy and cannot change authoritative mismatch truth.
Hardware Impact: One scalar branch on rare missing-frame path; prevents bogus cold-start remote motion.

## Decision 025 - Bound Descriptor Refresh Is Failure-Path Only
Problem: Late owner allocation was fixed for missing descriptors, but a later owner-side release/reacquire could leave rollback with a nonzero stale `VaultGenerationHandle<T>`. The previous helper exited on `BufferID != 0`, so the schedule could see default arrays forever after a generation change.
Solution: Added `ResolveBoundBuffer()` for schedule-time predicted input and borrowed snapshot lanes. It accepts the exact `BufferID`, rejects mismatched cached descriptors, resolves the current handle once in steady state, and refreshes through `TryGetGenerationHandle<T>` only after a missing descriptor or failed resolve. `Input_Queue_Inquisition` was also widened to detect whitespace-separated generic declarations so the managed queue proof is not limited to exact source tokens.
Rejected Alternatives: Rebinding every borrowed handle inside `TryEnsureBuffers()` was rejected because it adds metadata churn to the normal fixed schedule path. Reintroducing rollback fallback creation for input truth was rejected because it violates one-owner routing. Regex-based scanner logic was rejected because a simple token scanner is easier to audit and remains editor-only.
Scalability potential: Low/Middle/High/Ultra keep the same DTO layout and owner route. Weak devices pay no extra steady-state work beyond the existing resolve; high-tier sessions recover cleanly after owner-side lane migration without widening rollback truth or save identity.
Hardware Impact: Steady-state cost is unchanged at one generation-checked resolve per schedule-bound buffer. A stale generation recovery costs one failed resolve plus one descriptor lookup, bounded to owner reallocation events rather than every frame.

## Decision 026 - Compile-Wall Guard And Active Network Folder Reconciliation
Problem: The assignment names `Assets/_Project/Scripts/Core/Network/`, but this branch does not contain that folder. The active rollback/network implementation is under `Assets/_Project/Scripts/Networking/`. A false folder assumption would create an empty audit, while moving files or splitting asmdefs during a live 20-agent batch would widen the compile wall.
Solution: Scanned the missing path explicitly, then scanned the active `Networking` folder and SHINOBU touched Core/Editor files. Verified the touched runtime files live in the existing `Hecton8.Core.asmdef` and editor diagnostics in `Hecton8.Editor.asmdef`. No asmdef file was edited; no new sibling-domain reference was introduced by SHINOBU_278.
Rejected Alternatives: Creating `Core/Network/` was rejected because it would hide the real rollback scripts and add churn. Adding a new `Hecton8.Networking.Prediction` asmdef was rejected because this batch already has rollback code inside `Hecton8.Core`, and splitting assemblies without an integration pass risks compile-order damage.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The compile-wall guard preserves iteration velocity while the runtime scalability remains in logical prediction window, redundancy, and mismatch severity curves.
Hardware Impact: 0 runtime us. Avoids unnecessary script reload/assembly graph churn; compile remains intentionally unlaunched because CPU samples are `100,99.42,100` with the explicit guard at `>50%`.

## Decision 027 - Mock Input RNG Uses Unity.Mathematics.Random
Problem: `GenerateMockInputHistoryJob` used a hand-rolled LCG for synthetic input jitter. It was deterministic, but the project rule is stricter: state-affecting RNG must use `Unity.Mathematics.Random` with deterministic seeding so cross-platform replay proof is standardized.
Solution: Replaced the LCG with `Unity.Mathematics.Random`. The seed is `math.hash(new uint3(Seed, StartTick, count))` with a nonzero fallback, so the same stable seed/window emits identical mock input rows while different windows do not reuse the same random sequence.
Rejected Alternatives: Keeping the LCG was rejected because it creates a local RNG dialect outside the deterministic RNG doctrine. Calling into networking `RollbackNetcodeMath.CreateDeterministicRandom` from Core was rejected because it would add a Core -> Networking dependency and violate compile-wall routing.
Scalability potential: Low/Middle/High/Ultra are unchanged; mock history remains a cold CI/editor stress route. High-tier validation can still generate erratic movement/button masks without changing runtime truth ownership or DTO layout.
Hardware Impact: Cold mock fill remains O(n) over the bounded ring. Any cost delta versus the LCG is irrelevant to gameplay frames; the gain is deterministic replay compliance and simpler auditability.

## Decision 028 - Mock History Seeding Belongs To InputDispatcher
Problem: The rollback emergency mock path still ran `GenerateMockInputHistoryJob` against `BufferID.ShinobuPredictedInputRing` and `BufferID.ShinobuPredictedInputAupTargets`. Those lanes are owned by `InputDispatcher`; a rollback-side cold write could overwrite real local input or make owner attribution init-order dependent.
Solution: Moved the callable mock history facade to `InputDispatcher.GenerateMockInputHistory(startTick,count,seed)`, which resolves and writes the predicted input/AUP rings only through the input owner. Removed the rollback emergency predicted-ring write; rollback emergency mock now seeds only rollback-owned runtime/tuning/jitter/remote buffers.
Rejected Alternatives: Keeping the rollback write because it was cold was rejected; one-owner routing is structural and cannot depend on frequency. Calling the input facade from rollback was rejected because rollback emergency setup should not command the input domain to mutate local truth.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. CI/editor mock generation remains available through the owner, while rollback runtime quality curves continue to scale prediction window, redundancy, and severity without changing truth ownership.
Hardware Impact: 0 hot-frame us. Cold rollback emergency setup performs less work by skipping predicted-ring mock fill; input-owned mock fill remains bounded O(n) only when explicitly requested.

## Decision 029 - Replay Evidence After Ownership Repair
Problem: The latest ownership and RNG repairs touched both input-owner and rollback runtime surfaces. Without a fresh static replay, the route could still hide an old rollback-side predicted-ring write, a local RNG dialect, or a managed input queue variant with whitespace.
Solution: Re-read the SHINOBU XML prompt, relevant mandates, domain boundary, and binary ledger, then reran focused scans: whitespace-aware managed input queue search, RNG ban search, DTO `Pack=`/property hazard search, stale Vault handle/route search, report JSON parse, code-aware brace/preprocessor scan, and `git diff --check`.
Rejected Alternatives: Trusting the previous loop was rejected because the route changed after the subagent finding. Running dotnet anyway was rejected because CPU samples were `100,100,100` with the explicit guard at `>50%`.
Scalability potential: No runtime behavior change. The replay preserves the same continuous low/middle/high/ultra scaling route: logical prediction window, packet redundancy, mismatch severity, and optional proof richness can scale; DTO identity and rollback truth cannot.
Hardware Impact: 0 hot-frame us. The static pass prevents owner-route regression without adding runtime work; compile/import/profiler proof remains pending until CPU load permits.

## Decision 030 - Owner Pointer Is A Field, Not A Property
Problem: `InputDispatcher.ActiveRuntimeInstance` was an internal auto-property. It is not an unmanaged DTO and not a Burst hot array, but it is SHINOBU-owned owner identity used by the cold mock facade. Leaving it as a hidden accessor weakens the property-creep audit even if runtime cost is negligible.
Solution: Converted `ActiveRuntimeInstance` to a raw internal static field. Existing lifecycle assignments and mock facade reads remain identical, and no public API contract was changed.
Rejected Alternatives: Purging every public property on `InputDispatcher` and `HectonRollbackNetcodeRuntime` was rejected because those are established service/editor API surfaces, not unmanaged hot-path DTO fields, and broad removal would widen the compile wall into unrelated consumers.
Scalability potential: No runtime scalability change. Low/Middle/High/Ultra paths keep the same Vault route, prediction window scaling, and rollback truth behavior.
Hardware Impact: Removes one trivial accessor call on cold/editor/mock owner lookup. Expected hot-frame gain is 0 us because the field is not read inside Burst jobs or the fixed rollback loop.

## Decision 031 - Editor Readout Uses Visual Scalars, Labels Are Annotations
Problem: Task 16 asks for a real-time zero-GC readout, but `RollbackNetcodeTunerWindow.EditorTick()` was formatting several `Label.text` strings every editor update. That is editor-only, but it pollutes profiling windows and makes the readout path look like the runtime HUD anti-pattern.
Solution: Added `RollbackTelemetryStripElement`, a UI Toolkit visual element that stores raw scalar telemetry and draws bars through `generateVisualContent`/`Painter2D`. The strip is the live readout for quality, mismatch severity, resim pressure, packet loss, redundancy, and Dear Lie extrapolation. Numeric labels were moved behind a 0.25s dirty gate in `RefreshTextReadout()`, and the previous `_packetLabel.text = _packetLabel.text + ...` concatenation was removed.
Rejected Alternatives: A full custom glyph/text renderer was rejected for this batch because Unity UI Toolkit text ultimately still wants managed string state and would broaden editor risk. Leaving per-update label formatting was rejected because it contradicts the Task 16 readout intent even though it does not ship in player hot paths.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. The editor strip visualizes the same continuous quality/redundancy/severity scalars without changing DTO layout, BufferID ownership, or rollback truth.
Hardware Impact: 0 player-runtime us. Editor profiling sessions avoid repeated per-update string assembly for the primary readout; remaining string allocations are changed-only annotations at most 4 Hz.

## Decision 032 - Editor Scalar Path Is NaN-Safe
Problem: The new editor telemetry strip accepted raw floats from rollback telemetry. If a non-finite value reached severity, quality, or resim pressure, dirty comparisons could fail indefinitely and the visual bar could carry NaN geometry. The packet-loss bar also summed uint counters before converting to float, which can wrap during long sessions.
Solution: Added `Sanitize01()` and `SanitizePositive()` before comparison/drawing, and changed the loss-bar sum to cast each counter to float before addition and saturation.
Rejected Alternatives: Ignoring it because the strip is editor-only was rejected; the mandate's math hygiene is cheap here and prevents false visual diagnostics during failure analysis.
Scalability potential: No runtime scalability change. The strip still visualizes continuous low/middle/high/ultra quality and redundancy behavior; bad telemetry collapses to zero visual pressure instead of poisoning the editor draw path.
Hardware Impact: 0 player-runtime us. Editor-only scalar guards avoid repeated dirty repaints and prevent overflowed long-session counters from showing false low loss pressure.

## Decision 033 - Editor Capacity Read Is Scalar-Only
Problem: `RollbackNetcodeTunerWindow` requested `NativeArray<PredictedInputDTO>` from rollback just to display physical ring capacity. The route used a pure `TryReadHandle`, but the editor still received a mutable native view of dispatcher-owned prediction truth for a scalar label.
Solution: Added `HectonRollbackNetcodeRuntime.TryGetPredictedInputCapacity(out int)`, which reads the bound predicted-input descriptor through `TryReadOwned()` and returns only `Length`. The tuner now uses this scalar facade. Source inventory found no callers for the old `TryGetPredictedInputs(...)` facade, so that mutable-array read surface was removed.
Rejected Alternatives: Keeping `TryGetPredictedInputs` as a public debug escape hatch was rejected after source inventory showed no caller. Keeping the tuner on the array-returning route was rejected because a scalar read contract is sufficient and easier to audit.
Scalability potential: No runtime quality behavior changes. Low/Middle/High/Ultra keep fixed ring identity and continuous logical window/redundancy curves; the editor reads only a capacity scalar.
Hardware Impact: 0 player-runtime us. Editor read surface is narrower; descriptor read cost is unchanged and no additional allocation is introduced.
