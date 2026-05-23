# SHINOBU_319 Rationale - STATUS_EFFECTS_FSM_ENGINE

Status: PENDING VERIFICATION
Evidence class: STATIC_SOURCE until Unity/Burst/profiler proof exists.

## Decision 000 - Preflight Mandate Set
Problem: Status effects touch combat truth, entity DTO layout, Burst jobs, signal routing, AUP VFX coordinates, and crash telemetry. Missing one law creates either compile-wall coupling or hot-path GC.
Solution: Use the eight selected mandates as hard constraints before code archaeology: Zero-GC, ARM64 layout, Native jobs, Signal lanes, Execution phases, AUP determinism, Crash telemetry, Damage routing.
Rejected Alternatives: Reading only the batch XML was rejected because it omits current global authority and source-route drift from R51 docs.
Scalability potential: Low uses sparse slow-tick math and bounded queues; Middle runs full status truth at normal cadence; High/Ultra can add richer VFX/debug telemetry without changing gameplay truth.
Hardware Impact: Static planning only. Expected low-end gain comes from replacing coroutine/object timers with flat bit masks and SoA timers; measured microseconds absent.

## Phase Ownership Draft
Problem: Status requests, DoT evaluation, damage routing, VFX lie, and blackbox writes belong to different phase responsibilities.
Solution: PRE_SIMULATION drains status effect requests; SIMULATION evaluates status masks/timers; POST_SIMULATION publishes damage/VFX signals and writes telemetry; VISUAL_SYNC consumes VFX/debug only.
Rejected Alternatives: A standalone MonoBehaviour manager ticking in Update was rejected because it violates dispatcher phase ownership and creates registry hot-poll temptation.
Scalability potential: Cadence scales by continuous GlobalQualityWeight from 0.1s to 1.0s interval while preserving integrated damage.
Hardware Impact: Avoids per-frame evaluation when low-end thermal pressure exists; exact gain pending source integration and profiler proof.

## Decision 001 - Owner Integration
Problem: The XML mentioned `Assets/_Project/Scripts/Combat`, but the project truth places combat health/status ownership in `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs`; creating a new manager would duplicate authority.
Solution: Convert `CombatDamageRuntime` to a partial class and add `CombatDamageRuntime_StatusEffects.cs` for the status FSM slice. Existing `uint` status bits remain an ABI mirror only; authoritative state is `CombatStatusEffectState.StatusEffectMask` at offset 0.
Rejected Alternatives: A standalone `HectonStatusEffectManager` was rejected because it would hot-poll target identity and fight `CombatDamageRuntime` for health truth.
Scalability potential: Low/Middle/High/Ultra share one target slot map and one status state row; fidelity scales by cadence and VFX, not by alternate code paths.
Hardware Impact: Avoids an extra target lookup and managed dispatch per affected entity; estimated 10-35 us per 2k registered targets on i3/MX350-class hardware.

## Decision 002 - Vault State Layout
Problem: Status timers need overlap refresh, deterministic expiry, and ARM64-safe mask reads without `Dictionary<EffectType,float>` cache misses.
Solution: Use explicit 64B `CombatStatusEffectState`: `ulong StatusEffectMask` offset 0, two `float4` timer packs at offsets 8 and 24, frame stamps at 40/44, byte FSM states at 48-55, hash at 56.
Rejected Alternatives: Expanding managed receiver classes or using per-effect components was rejected because expiry would allocate/destroy objects and fragment the heap.
Scalability potential: Low uses sparse slow cadence; Middle uses normal cadence; High/Ultra use the same state row and spend saved cycles on toxic bubble feedback and debug telemetry.
Hardware Impact: One 64B row per target keeps mask/timers inside one cache line; estimated L1 improvement versus scattered arrays is 20-60 us per 2k targets.

## Decision 003 - Damage Route
Problem: DoT damage is gameplay truth and cannot be owned by a status side system.
Solution: `EvaluateStatusEffectsJob` computes integrated damage in Burst, stages `CombatDamageSignal` rows in Vault `71268`, and owner completion publishes the rows through the first-party SignalBus. `DispatchStatusResults` records status telemetry only and does not own health truth.
Rejected Alternatives: Direct health subtraction inside status evaluation and worker-thread SignalBus publication were rejected because the matrix requires central combat damage routing from the owner phase.
Scalability potential: The route can batch low-tier DoT into larger less frequent magnitudes; high-tier can emit smaller, more frequent signals without changing authority.
Hardware Impact: Adds one unmanaged queue write for active DoT rows, but removes direct duplicate health ownership. Expected net is correctness over raw microseconds; low-tier cadence limits queue pressure.

## Decision 004 - Atomic Application
Problem: Multiple weapons/environment systems can apply poison or bleed to the same entity in one phase.
Solution: Requests enter `NativeQueue<CombatStatusEffectRequest>`; `ApplyStatusEffectRequestsJob` resolves target slots and uses an Interlocked CAS OR on the first `ulong` word of the 64B state row, then refreshes timer lanes with `math.max`.
Rejected Alternatives: Main-thread direct `StatusEffectMask |= bit` was rejected because it races with scheduled status jobs and lacks a request ledger.
Scalability potential: Low tier coalesces overlapping requests into one mask row; High/Ultra can tolerate more request sources without object churn.
Hardware Impact: CAS loop is only on application, not evaluation; expected cost is sub-microsecond per request batch unless many sources hammer one target.

## Decision 005 - Continuous Cadence
Problem: Evaluating poison every frame wastes CPU and creates binary quality behavior.
Solution: Cadence is `lerp(max=1.0s, min=0.1s, SmoothStep(GlobalQualityWeight))`; damage magnitude scales by accumulated `dt`, preserving total damage.
Rejected Alternatives: Low/Ultra code branches were rejected because they alter scheduling shape and invite divergent gameplay truth.
Scalability potential: Low/Middle/High/Ultra are points on one scalar curve. Low batches; Ultra uses tighter cadence and richer VFX.
Hardware Impact: On i3/MX350, 1.0s cadence can cut status passes by roughly 6-10x versus 10Hz/60Hz evaluation while preserving integrated damage.

## Decision 006 - VFX Lie
Problem: Poison needs visual readability without CPU particle ownership.
Solution: Managed status feedback emits `BubbleSpawnSignal` with `AbsoluteUniversePosition` resolved from runtime origin. Geometry remains GPU/procedural downstream.
Rejected Alternatives: Instantiating particle systems or attaching poison bubble components was rejected because it would reintroduce per-entity managed state.
Scalability potential: Low can shed noncritical VFX through SignalBus policy; Ultra can raise bubble scale/frequency without touching status truth.
Hardware Impact: Estimated 200+ us saved during swarm poison bursts versus CPU particle spawning on weak hardware.

## Decision 007 - Tooling And Proof
Problem: The work needs proof artifacts beyond code changes.
Solution: Added UI Toolkit tuner, debug gizmo, CSV ingestion, OOP scanner, status/rationale/log files, 300-entry Vault telemetry ring, and `Dump_SHINOBU_319.bin` path.
Rejected Alternatives: Chat-only report was rejected because CTO review reads disk logs, and scanners prevent regression.
Scalability potential: Editor-only tools can be heavy; runtime remains flat native buffers and SignalBus lanes.
Hardware Impact: Editor tools have no player runtime cost. Telemetry writes one 64B entry per active result; estimated 1 us per write.

## Decision 008 - Atomic Row Stride Correction
Problem: The first Interlocked OR cast the 64B state array to `ulong*` and indexed by slot, which would write slot 1 into byte offset 8 of row 0 instead of row 1.
Solution: Keep the required Interlocked CAS OR but compute the byte address as `base + slot * 64`, targeting `CombatStatusEffectState.StatusEffectMask@0`. Add slot bounds checks before touching state/mirror/timer lanes.
Rejected Alternatives: Removing Interlocked entirely was rejected because the assignment explicitly requires atomic bit application and external producers can converge on one target. Blind hash-map trust was rejected because stale target maps become memory corruption, not a recoverable dropped request.
Scalability potential: Low/Middle/High/Ultra use the same row layout and only cadence/VFX density changes. Atomic contention is application-only and independent of visual fidelity.
Hardware Impact: Corrects a catastrophic correctness fault. Normal valid requests add a few integer comparisons; invalid requests are dropped without memory fault. Estimated overhead under 1 us per small request batch on i3/MX350.

## Decision 009 - Phase-Correct VFX Staging
Problem: Emitting `BubbleSpawnSignal` directly from `EvaluateStatusEffectsJob` mixed simulation math with visual publication and required a SignalBus writer in the worker.
Solution: Add explicit 64B `CombatStatusEffectVfxRequest` rows in Vault BufferID `71267`. The Burst job writes exact `double3` AUP plus intensity/radius into this lane; owner completion publishes `BubbleSpawnSignal` after the job fence.
Rejected Alternatives: CPU particle instantiation was rejected as managed visual ownership. Direct worker SignalBus publication was rejected after audit because owner completion is the cleaner phase boundary.
Scalability potential: Low quality raises bubble cadence to 48 frames and sheds VFX through SignalBus policy; Middle/High/Ultra reduce cadence toward 8 frames without changing mask truth or damage route.
Hardware Impact: Keeps geometry off CPU. Adds one 64B staging write per accepted bubble, replacing hot visual queue publication from the worker. Estimated 200+ us saved versus CPU particle/component bursts remains valid.

## Decision 010 - Telemetry Ring And Vault Lock Discipline
Problem: Completion telemetry used signed modulo and the Vault buffers were unlocked before owner-side counter/telemetry writes.
Solution: Use unsigned modulo for telemetry cursors and keep buffers locked until solve microseconds, VFX count, completion row, anomaly flag, and dump decision are written.
Rejected Alternatives: `math.abs(cursor) % capacity` was rejected because `int.MinValue` can remain negative. Unlock-before-proof was rejected because the proof artifact belongs to the same owner phase as the completed job.
Scalability potential: Telemetry cadence follows status cadence; no device tier changes DTO shape or dump ABI.
Hardware Impact: No meaningful frame gain. Prevents rare long-session ring corruption and write-after-unlock races on cheap devices and high-thread desktop runs.

## Decision 011 - Cold Signal Lane Prewarm And Source Boundary
Problem: Opening SignalBus writers from status simulation can cold-initialize lanes if nobody prewarmed them. The status slice also depended on `DamageSourceIds` declared beside Habitat integrity.
Solution: Initialize `CombatDamageSignal` and `BubbleSpawnSignal` during status storage bootstrap without owning their shared configuration, then fail scheduling if native storage is absent. Replace status use of Habitat's internal source class with a local `StatusEffectEnvironmentHazardSourceId` ABI constant.
Rejected Alternatives: Letting first poison tick allocate/register SignalBus storage was rejected as a hitch. Moving the global source ID table in this pass was rejected because many domains already depend on the legacy internal class and this task owns only the status slice.
Scalability potential: Cold prewarm is constant. Low/Middle/High/Ultra keep identical source IDs and signal payload layout.
Hardware Impact: Avoids first-hit native lane allocation during combat. Estimated hitch reduction is workload dependent; runtime steady-state cost unchanged.

## Decision 012 - NaN Vaccination In Status Math
Problem: A corrupted Vault duration pack or malformed tuning row could propagate NaN through timer decrement, damage calculation, telemetry, and queued combat signals.
Solution: Sanitize previous duration `float4` packs with `math.select(float4.zero, value, math.isfinite(value))`, sanitize DPS and raw damage before use, and keep anomaly telemetry for non-finite health/damage detection.
Rejected Alternatives: Trusting editor sliders or zeroed memory was rejected because long-session rollback/load faults must not poison the frame. Throwing exceptions from Burst was rejected because the job must write blackbox evidence, not crash first.
Scalability potential: Same ALU path across Low/Middle/High/Ultra; quality affects cadence and visual density only.
Hardware Impact: Adds a small SIMD finite mask and scalar finite checks per active row. Estimated cost is below the saved cadence budget; prevents catastrophic NaN fanout on low-end and desktop targets.

## Decision 013 - Request-Only Cadence Collapse And Pure Ingress
Problem: The status queue facade still called `EnsureInitialized`, so the first gameplay request could execute cold native allocation. Also, request-only slow ticks scheduled the full O(MaxTargets) evaluation pass with `DeltaTime=0`.
Solution: `TryQueueStatusEffect` now fails closed unless the status request queue was prewarmed by the combat owner. `TryScheduleStatusEffectJobs` schedules only `ApplyStatusEffectRequestsJob` when cadence debt has not matured; the O(MaxTargets) `EvaluateStatusEffectsJob` runs only when continuous cadence says damage integration is due.
Rejected Alternatives: Lazy allocation from the write facade was rejected as a first-hit combat hitch. Running the full evaluator for zero-dt request frames was rejected because applying bits and timers is sufficient until the next integration cadence.
Scalability potential: Low quality gets sparse damage integration and cheap request admission; Middle/High/Ultra still tighten cadence continuously and can spend the saved request-only frames on VFX density.
Hardware Impact: Avoids allocator hitch on first poison hit and saves the full target scan on request-only frames. Estimated 20-60 us per 2k targets on i3/MX350-class hardware.

## Decision 014 - Proof Artifact Merge Discipline
Problem: The editor scanner could overwrite the shared `PHYSICS_OPTIMIZATION_REPORT.json`, deleting sibling agents' proof keys. The dedicated self-audit also omitted several 64B DTO layouts.
Solution: `OOP_Buff_Scanner` now merges only the `shinobu319StatusEffectsScanner` property and writes `PHYSICS_OPTIMIZATION_REPORT_SHINOBU_319.json` as a sidecar. The XML self-audit now lists request, state, tuning, telemetry, counter, and VFX layouts.
Rejected Alternatives: Whole-file JSON rewrite from the scanner was rejected as multi-agent report data loss. Partial DTO proof was rejected because ARM64 padding must be demonstrated for every Burst/Vault payload.
Scalability potential: Editor-only proof changes do not affect runtime quality. They protect integration review and prevent reports from hiding cross-agent regressions.
Hardware Impact: No player runtime cost. Review/debug impact is reduced because layout proof is now complete and shared reports remain intact.

## Decision 015 - Owner-Fenced Damage Staging
Problem: The first DoT implementation published `CombatDamageSignal` from the Burst worker through a SignalBus parallel writer. That mixed simulation math with hot publication, bypassed owner-completion backpressure, and made `CanMutateTargets()` capable of finalizing status jobs without status completion proof.
Solution: Add Vault buffer `71268 Shinobu319StatusEffectDamageSignals` and counter lane 8. `EvaluateStatusEffectsJob` reserves slots with a 64B padded Interlocked counter and writes existing 64B Core `CombatDamageSignal` rows. `CompleteStatusEffectFrame()` publishes those signals after the job fence, records anomaly hash `0x5319D001` if SignalBus backpressure rejects a gameplay damage packet and `0x5319D002` if native storage disappears, writes telemetry with the captured evaluation delta, then unlocks status buffers and any borrowed armor AUP buffers. `CanMutateTargets()` now refuses all target mutation while a status job is scheduled and leaves status completion to `LateFrameTick` or shutdown.
Rejected Alternatives: Keeping `NativeQueue<CombatDamageSignal>.ParallelWriter` in the worker was rejected because owner-side publication and route proof belong after the fence. Direct status health mutation remains rejected because combat damage truth belongs to the central damage router. Copying armor AUPs into a status-owned persistent shadow buffer was rejected because it would create a second owner for target position truth.
Scalability potential: Low quality still batches status integration by cadence and emits fewer staged rows; Middle/High/Ultra tighten cadence continuously. The damage ABI, mask truth, and SignalBus route stay identical across the curve.
Hardware Impact: Adds one contiguous 64B write per active DoT row and one owner-side publish loop over staged rows. It removes worker queue publication and prevents race/fence holes; expected overhead is bounded by active DoT count and amortized by the existing cadence collapse.

## Decision 016 - VFX Lane Is Not Gameplay Admission
Problem: The schedule gate required both damage and bubble VFX SignalBus native storage before applying status requests. That made a visual lane capable of blocking poison/bleed truth.
Solution: Keep damage SignalBus native storage as a fail-closed requirement for simulation frames because DoT health truth needs the central damage route. Remove `BubbleSpawnSignal` from the schedule gate; VFX publication remains best-effort in owner completion and missing VFX storage only suppresses the Dear Lie presentation.
Rejected Alternatives: Failing the entire status frame because toxic bubble presentation is unavailable was rejected because visual availability must not alter gameplay truth. Publishing CPU particles as fallback was rejected because it violates the Dear Lie and Zero-GC requirements.
Scalability potential: Low quality can shed VFX entirely without pausing status truth; Middle/High/Ultra get denser bubbles when the lane exists.
Hardware Impact: No measurable speed claim. It prevents false gameplay stalls caused by optional visual infrastructure.

## Decision 017 - External Compile Wall Fence
Problem: The first legal build window opened after earlier CPU/process gates, but the project compile failed in files outside the SHINOBU_319 status-effects domain.
Solution: Record the failure as an external compile wall: `VRSomaticProvider.Comfort.cs` references missing `VRSomaticKinematicStateMirrorDTO` and `VRSomaticComfortDTO`; `PlayerKinematicsRuntime_HandIK.cs` references missing `PlayerHandIkConfigFlags`. Leave those files untouched because they belong to VR somatic/player kinematics owners.
Rejected Alternatives: Editing unrelated gameplay DTOs or flags was rejected as cross-domain sabotage. Launching repeated builds was rejected because one gated probe already produced the blocking evidence.
Scalability potential: No runtime scalability change. The status FSM remains on Vault buffers and continuous GlobalQualityWeight cadence; compile proof is blocked by foreign symbols.
Hardware Impact: No player runtime impact. Developer iteration cost is limited by stopping after one legal build probe instead of hammering the compile wall.

## Decision 018 - Request-Only Armor Dependency Isolation
Problem: `TryScheduleStatusEffectJobs` resolved and refreshed armor `TargetRootAups` before knowing whether the frame needed a DoT/VFX simulation sweep. A request-only frame only needs the target slot map and status Vault rows, so armor AUP availability could falsely block atomic mask application.
Solution: Move armor view resolution, AUP refresh, and armor Vault lock into the `hasSimulationWork` branch. Split status Vault locking so request-only frames lock only state/tuning/telemetry/cursor/counters; VFX and staged damage lanes lock only for simulation sweeps. Request-only frames now schedule only `ApplyStatusEffectRequestsJob` and do not depend on presentation/spatial AUP lanes.
Rejected Alternatives: Keeping the early armor dependency was rejected because it lets a non-required cross-domain read gate status truth ingestion. Locking VFX/damage staging for request-only frames was rejected because those buffers are unused when cadence debt has not matured. Creating a status-owned AUP shadow buffer was rejected because target position truth already belongs to the armor/combat target lane.
Scalability potential: Low quality benefits most because request-only frames are common when cadence debt has not matured; Middle/High/Ultra still lock armor AUPs only for real integrated damage/VFX sweeps.
Hardware Impact: Avoids unnecessary Vault handle resolution, transform snapshot refresh, and two unused Vault lock attempts on request-only frames. Estimated 5-25 us saved on weak CPUs when bursts of status applications arrive between cadence ticks.

## Decision 019 - Active Status Telemetry Before Result Early-Out
Problem: `StatusEffectCounterActive` was incremented only after `shouldWriteResult`, so stable active status rows with no damage/change/anomaly during a cadence pass were omitted from completion telemetry.
Solution: Increment the active counter immediately after live mask resolution and anomaly evaluation, before the early return. Result counters still track only rows that publish damage/change/anomaly.
Rejected Alternatives: Leaving active count as result count was rejected because blackbox telemetry must distinguish "no active statuses" from "stable active statuses without a result this frame".
Scalability potential: Low cadence frames now report true active row pressure even when damage is batched; High/Ultra reports remain comparable because the counter definition no longer depends on result emission.
Hardware Impact: One Interlocked add per live row during evaluation. This is acceptable because evaluation already runs only on matured cadence, and accurate telemetry is required for the 300-frame blackbox.

## Decision 020 - Owner-Folded Telemetry Ring
Problem: `EvaluateStatusEffectsJob` wrote `CombatStatusEffectTelemetryEntry[300]` ring slots from an `IJobParallelFor` using an Interlocked cursor and modulo wrap. More than 300 result rows in one job could cause same-job slot reuse, and `[NativeDisableParallelForRestriction]` hid the race from Unity safety checks. A second stale-data risk existed because invalid indices could return before clearing `ResultActiveBySlot[index]`.
Solution: Remove parallel ring writes from the Burst job. The job now stages normal result data in `ResultsBySlot`/`ResultActiveBySlot`; owner completion folds result telemetry into the 300-entry ring after the fence, then writes the completion summary row. Dump export now starts at the write cursor when wrapped, producing oldest-to-newest ring order. `ResultActiveBySlot[index]` is cleared before full index validation.
Rejected Alternatives: Keeping modulo writes in the parallel job was rejected as nondeterministic telemetry corruption. Adding a new per-slot telemetry Vault buffer was rejected because existing result staging already carries the needed post-fence data without another active BufferID. Trusting stale result-active bytes was rejected because dependency array length mismatches must fail closed.
Scalability potential: Low/Middle/High/Ultra keep the same ring ABI. High/Ultra can produce more result rows, but owner-folding preserves deterministic blackbox order instead of racing inside Burst.
Hardware Impact: Removes parallel ring contention and same-slot write risk. Owner fold is O(result rows) after the fence; this is acceptable because it only iterates rows that already produced status output.

## Decision 021 - Reserved BufferID Honesty
Problem: Docs listed `71265`/`71266` beside active runtime Vault lanes even though CSV profile ingestion and scanner reports currently use cold editor filesystem routes.
Solution: Mark `71265` and `71266` as reserved-only IDs in the ledger/route card. Active H-PHI buffers remain `71260..71264`, `71267`, and `71268`.
Rejected Alternatives: Implementing Vault-backed CSV/report storage in this pass was rejected because it is not required for player runtime truth, and adding dead active buffers would increase proof surface without a consumer.
Scalability potential: No runtime curve change. The correction prevents future agents from treating reserved IDs as live authority.
Hardware Impact: No runtime cost. Documentation now matches actual allocation behavior.
