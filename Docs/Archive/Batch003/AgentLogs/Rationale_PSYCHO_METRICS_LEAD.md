# Rationale_PSYCHO_METRICS_LEAD

Status: PENDING VERIFICATION

## Decision 0: Task Authority And Memory Files

Problem: The assigned player-stress system must be implemented while 20+ agents may be changing adjacent domains.
Solution: Use disk-backed checklist/rationale as long-term memory and restrict source edits to the Player Stress & Fear System boundary plus contract/signal integration points.
Rejected Alternatives: Direct player singleton calls were rejected because the prompt explicitly requires autonomous S.O.A. stress logic via signals. Cross-domain concrete references were rejected because AGENTS.md requires GlobalRegistry or signal corridors.
Scalability potential: Low uses 10Hz scalar stress and disables hallucination. Middle keeps scalar signal fanout. High adds richer presentation consumers through the same signal. Ultra can drive heavier audio/visor effects without changing stress authority.
Hardware Impact: i3/MX350 impact is expected to remain below 0.1ms because the authority state is one scalar, evaluated on SlowTick, with no managed allocation in the frame lane.

## Mandates Applied

- CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt
- UI_Data_Streaming_ZeroGC_Optimization.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt

## Decision 1: Non-Destructive Signal Consumption

Problem: `DamageSignal` is already consumed by `CombatDamageRuntime`; a second queue drain would drop damage packets and corrupt combat.
Solution: Add latest-signal mirrors in `GlobalSignals` for damage, acoustic, light, and player stress. The stress runtime consumes sequence-stamped snapshots without stealing queue payloads.
Rejected Alternatives: Direct `TryDequeueDamage` in physiology was rejected because NativeQueue is not multicast. Duplicating damage producers was rejected because 20+ agents are editing adjacent domains.
Scalability potential: Low/MX350 pays one volatile sequence read per signal per SlowTick. Middle/High/Ultra can add more consumers through latest snapshots without additional queue lanes.
Hardware Impact: i3/MX350 estimated gain is avoidance of double-dispatch damage recovery; snapshot read cost is under 2 us per SlowTick.

## Decision 2: SlowTick S.O.A. Authority

Problem: Stress needs deterministic 10Hz math, but the current dispatcher slow lane is 0.5s and carries no delta parameter.
Solution: Run only from `ISlowTickable.SlowTick()` and execute five internal 0.1s substeps, clamping with `math.saturate`.
Rejected Alternatives: Adding an `Update()` or `IUpdatable` loop was rejected because the prompt requires the 10Hz SlowTick dispatcher. Reading `Time.deltaTime` was rejected because dispatcher-owned time must stay authoritative.
Scalability potential: Low uses the scalar path only; Middle/High keeps the same scalar but consumers can overdrive audio/visor; Ultra can add denser presentation without changing the authority state.
Hardware Impact: i3/MX350 cost is five scalar iterations per slow tick, estimated under 4 us and zero managed allocation.

## Decision 3: Apex Proximity Contract

Problem: `WorldSpatialHashGrid` is internal to the world assembly, while physiology must not depend directly on world internals.
Solution: Add `IEcosystemDirectorService.TryGetApexPredatorThreat(...)`; `EcosystemDirector` performs the non-alloc spatial hash query and returns one scalar proximity.
Rejected Alternatives: Making `WorldSpatialHashGrid` public was rejected as architecture leakage. Scene scans or `FindObjectsOfType` were rejected as slow and allocation-prone.
Scalability potential: Low reads one scalar and disables hallucinations. Middle uses the same proximity. High/Ultra can increase downstream visual/audio response without additional spatial queries.
Hardware Impact: i3/MX350 query is capped at 16 hits and 50m radius; expected slow-lane cost stays below 0.02 ms in populated sectors.

## Decision 4: Physiology Consequence Fanout

Problem: Oxygen, audio, and visor systems need stress but must not be directly owned by the stress authority.
Solution: Publish `PhysiologyStateSignal` and `PlayerStressSignal`; audio reads the latest stress signal inside its DSP owner, and visor stress VFX reads the same signal before setting shader globals consumed by `HectonVisorUberPostFeature`.
Rejected Alternatives: Direct `HectonSurvivalSystem` oxygen mutation and direct audio playback from physiology were rejected as god-object coupling. A second post-process pass was rejected because the visor already has a global stress input.
Scalability potential: Low keeps one scalar. Middle uses heartbeat and chromatic response. High increases shader distortion. Ultra can layer richer DSP and post behavior without changing physiology authority.
Hardware Impact: i3/MX350 additional work is two latest-signal reads plus one scalar max operation in existing consumers; estimated under 2 us per consumer tick.

## Decision 5: Base Recovery Contract Boundary

Problem: No dedicated HabitatIntegrity read model exists for "player inside powered BaseModule"; direct polling would require scanning BaseModule internals.
Solution: Use `ModuleStatusEvents` enter/exit payload as the existing base interior contract and cache `HasPower` while inside.
Rejected Alternatives: Polling all active modules, using trigger collider queries, or referencing `HabitatIntegrityManager` static state were rejected as either slow or semantically wrong.
Scalability potential: Low/MX350 pays zero steady polling cost. Middle/High/Ultra can enrich base safety presentation from the same payload without new queries.
Hardware Impact: i3/MX350 gain is avoiding per-SlowTick module scans; event cache write is effectively free outside enter/exit.

## Decision 6: Hallucination As Debris Fake

Problem: High stress needs perceptual terror without creating AI, physics, or persistence load.
Solution: Emit a `DebrisSpawnSignal` with a `GhostlyFish` hash at the edge of the player's view when stress exceeds 0.9, with deterministic cooldown and no real actor.
Rejected Alternatives: Spawning fauna brains or physics props was rejected because hallucination is presentation, not simulation. Per-frame random checks were rejected in favor of SlowTick xorshift.
Scalability potential: Low/MX350 disables the fake entirely. Middle can emit rare ghost debris. High can increase shader response. Ultra can let the debris consumer overdraw more aggressively without changing physiology math.
Hardware Impact: i3/MX350 saves draw calls by disabling the feature; high-tier cost is one signal packet on rare eligible SlowTicks.

## Decision 7: Peak Stress Blackbox

Problem: Panic spikes must be explainable after a crash without writing text logs or allocating managed buffers.
Solution: Add `CrashTelemetryBuffer.ReportPeakStressEvent` and pack stress/O2 into the fixed 64-byte telemetry entry ring.
Rejected Alternatives: Appending markdown or CSV during gameplay was rejected because file I/O and strings are not acceptable on the runtime path.
Scalability potential: Low only records scalar peaks. Middle/High/Ultra can correlate richer audio/visual effects against the same stress peak counter.
Hardware Impact: i3/MX350 only pays on peak events; no per-frame blackbox allocation and no export unless existing crash logic exports the ring.

## Decision 8: Omega Polish And Contract Debt

Problem: The post-checklist polish mandate requires removing heavyweight math primitives and declaring the remaining contract boundary debt.
Solution: Use `NormalizeApproxNoSqrt` with `math.rsqrt`, reciprocal multiply for acoustic attenuation, and `InvByteMax` for byte normalization. The physiology asmdef references `Hecton8.Core` because `GlobalSignals`, `GlobalRegistry`, `ModuleStatusEvents`, and the current player runtime context are still hosted by the core assembly; the code itself crosses through signal and registry contracts only.
Rejected Alternatives: Moving `GlobalSignals` or player runtime context into a contracts asmdef during this task was rejected as cross-domain assembly surgery. Direct UI/audio/world references were rejected. Leaving `normalizesafe` and inline range divisions was rejected by the Omega math audit.
Scalability potential: Low/MX350 runs scalar stress, oxygen multiplier, and telemetry while disabling hallucination. Middle keeps rare ghost debris. High drives stronger heartbeat and visor response. Ultra can add heavier downstream presentation without changing the physiology authority or adding simulation cost.
Hardware Impact: i3/MX350 gains come from SlowTick-only execution, capped 16-hit predator query, no frame-lane scans, and no hallucination draw packets on low tiers. Expected stress authority cost remains roughly 10-30 us per SlowTick in populated sectors, 0 B GC hot path.

## Decision 9: Contract Pose Snapshot And Voxel-Light Producer

Problem: Recheck found two architecture defects: physiology still touched concrete `PlayerRuntimeContextService`, and `LightLevelSignal` had no producer, leaving darkness stress dependent on a future unwired system.
Solution: Add `PlayerRuntimePoseSnapshot` to `IPlayerRuntimeContext` and have `PlayerRuntimeContextService` publish AUP, runtime position, and forward vector through that contract. Add a throttled 10Hz light scalar publisher to `HectonCaveVoxelLightingVolume`, derived from the player-centered SDF byte at the follow target, routed through `GlobalSignals.Publish(in LightLevelSignal)`.
Rejected Alternatives: Direct access to `PlayerRuntimeContextService`, direct reads of `HectonCaveVoxelLightingVolume.ActiveRuntimeInstance` from physiology, and scene searches for light volumes were rejected. They violate the signal/contract boundary and create cross-domain compile coupling. A real light simulation was rejected; the SDF byte is already the visual cave-darkness proxy.
Scalability potential: Low/MX350 samples one byte every six frames and disables hallucination. Middle/High/Ultra can increase downstream audio/visor response from stress without increasing light sampling cost. The cave lighting owner can later enrich `LightLevelSignal` without touching physiology.
Hardware Impact: i3/MX350 cost is one local matrix transform, three clamps, one SDF byte read, and one signal packet every ~6 frames; estimated below 2 us at 60 FPS cadence, 0 B GC. Removing the concrete player-runtime dependency reduces assembly coupling risk without adding runtime work.

## Decision 10: Valid Light Contract And Panic Flag Semantics

Problem: Invalid cave-light samples used a bright fallback scalar and physiology accepted that scalar as recovery authority. Panic trauma also wrote a cause ordinal into a flag field, which makes downstream bitmask consumers ambiguous.
Solution: Move `LightLevelSignalSampleKinds` and `LightLevelSignalFlags` into the shared signal contract. `HectonCaveVoxelLightingVolume` marks validity explicitly, and `PlayerStressMetricsRuntime` updates light stress only when the sample is valid cave SDF data. Panic escalation now writes `FlagPanicAttack`, and source entity IDs are cached at `Awake` instead of read during rare signal emission.
Rejected Alternatives: Treating no-volume fallback as safe light was rejected because it hides missing lighting data and can erase darkness stress. Duplicating magic flag values in world and physiology was rejected because signal interpretation must live with the signal. Keeping the panic ordinal in `Flags` was rejected because it violates bitmask semantics.
Scalability potential: Low/MX350 avoids false recovery when the light producer is inactive and still pays only one byte flag test per SlowTick. Middle/High/Ultra can add richer light sources by setting the shared validity contract without changing the stress authority.
Hardware Impact: i3/MX350 gains are small but deterministic: one native `GetInstanceID()` call is moved off the stress/light signal path, invalid light samples cost a byte test only, and no managed allocation or scene lookup was introduced.

## Decision 11: Neutral No-Light State And AUP-Relative Hallucination

Problem: A bright default light scalar still allowed recovery before any valid light packet arrived, and hallucination placement converted runtime coordinates back through floating-origin state despite already owning a player AUP snapshot.
Solution: Introduce `NeutralLightLevel01 = 0.5` so missing or invalid light packets cause neither darkness buildup nor recovery. Generate hallucination `DebrisSpawnSignal.PositionAup` by adding the deterministic view-edge offset to the snapshotted player AUP in double precision.
Rejected Alternatives: Keeping `LightLevel01 = 1.0` as startup fallback was rejected because no-data is not safe light. Recomputing AUP from runtime hallucination position was rejected because it needlessly touches floating-origin global state and can drift around origin shifts.
Scalability potential: Low/MX350 keeps scalar stress deterministic with no hallucination spawn. Middle/High/Ultra get more stable ghost-debris placement under floating-origin shifts without adding scene queries, physics, or actors.
Hardware Impact: i3/MX350 saves a floating-origin conversion on rare hallucination emission and replaces missing-light handling with one scalar assignment. Hot path remains 0 B GC.

## Decision 12: Recon Evidence Instead Of Player-Health Deletion

Problem: A deprecated copy of the batch prompt requested recon evidence for redundant player fear/panic variables. Fresh scans found no mutable player fear/panic state, but did find `HectonPlayerHealth.Stress01`, a health/hazard composite with different ownership.
Solution: Add `Docs/AgentLogs/RECON_PSYCHO_METRICS_LEAD.md` with scan commands, findings, and the reason no deletion was performed. Keep `HectonPlayerHealth.Stress01` untouched because it is combat/survival damage presentation, not the psychological stress authority.
Rejected Alternatives: Deleting or hijacking `HectonPlayerHealth.Stress01` was rejected because it would cross into health/survival damage semantics and could break existing UI/audio consumers. Treating tooltip text as runtime panic state was rejected as false-positive cleanup.
Scalability potential: Low through Ultra keep separate scalar lanes: health hazard stress remains where existing consumers expect it, while psychological stress fans out by signal.
Hardware Impact: i3/MX350 runtime cost is 0 us; recon is documentation only and prevents destructive cleanup churn.

## Decision 13: Survival O2 Consumer For Psycho-Metrics

Problem: The stress authority published `PhysiologyStateSignal.O2DrainMultiplier`, but survival oxygen drain still resolved only local health/hull/movement/trauma stress. The intended panic O2 penalty could therefore be visible to logs while not changing actual oxygen drain.
Solution: Add a latest snapshot mirror for `PhysiologyStateSignal` and have `HectonSurvivalSystem.ResolveOxygenStressScale()` read the fresh multiplier non-destructively. The multiplier is clamped to 2.5x and max-composed with existing survival stress scaling, preserving existing pressure/injury behavior without multiplying two stress models into runaway drain.
Rejected Alternatives: Directly mutating `oxygen` from physiology was rejected because physiology owns signals, not survival resource state. Destructively draining `PhysiologyStateSignal` in survival was rejected because NativeQueue is single-consumer. Multiplying survival stress and psycho-metrics stress was rejected because pressure/hull stress already affects oxygen and would create unfair double taxation.
Scalability potential: Low/MX350 pays one latest-signal read and one frame-freshness test per survival SlowTick. Middle/High/Ultra get the same gameplay consequence while presentation can overdrive audio/visor from the same stress scalar.
Hardware Impact: i3/MX350 cost is estimated below 2 us per survival SlowTick, 0 B GC. The change buys a concrete survival consequence without adding polling, scene queries, or per-frame work.

## Decision 14: Missing-Pose Stale Stress Decay

Problem: If the player pose contract disappears during scene transition or bootstrap ordering, the stress runtime could keep publishing the previous stress/O2 multiplier as fresh data.
Solution: Add `HandleMissingPlayerPose()` to decay the internal stress scalar by one slow-tick recovery quantum, clear transient impulses/threat, set light neutral, recompute the O2 multiplier, and publish neutral flags.
Rejected Alternatives: Publishing the last state unchanged was rejected because it extends stale panic. Hard-resetting stress to zero was rejected because a one-frame registry miss should not erase accumulated tension. Suppressing publication entirely was rejected because consumers would hold stale data until freshness windows expire.
Scalability potential: Low through Ultra get deterministic scene-transition behavior from the same scalar path, with no presentation or simulation branch explosion.
Hardware Impact: i3/MX350 cost is a few scalar assignments only on missing-pose SlowTicks, estimated below 1 us and 0 B GC.

## Decision 15: Physiology NaN Vaccination And Blackbox Export

Problem: Peak-stress telemetry explained high panic, but a non-finite stress/O2 scalar could still contaminate survival oxygen, audio heartbeat, or visor shader consumers before a crash export identified physiology as the source.
Solution: Add `CrashTelemetryBuffer.ReportPhysiologyNan(...)` with a dedicated `PhysiologyNan` error bit/export reason and bypass-cooldown blackbox export. `PlayerStressMetricsRuntime` now validates stress, light, predator, impulse, recovery, O2, and peak-stress fields before publishing; invalid state records numeric telemetry, resets to neutral scalar state, and publishes neutral signals.
Rejected Alternatives: Debug logging was rejected because hot-path string logging violates zero-GC and is not a blackbox. Silently clamping every invalid value was rejected because it erases the failure evidence. Throwing exceptions was rejected because physiology should degrade to safe state and keep survival deterministic.
Scalability potential: Low/MX350 pays only scalar finite checks on 10Hz SlowTick and keeps hallucination disabled. Middle/High/Ultra get the same safe authority state while richer audio/visor presentation remains insulated from NaN payloads.
Hardware Impact: i3/MX350 cost is roughly eight finite checks per SlowTick plus one telemetry write only on fault; expected normal-path cost is below 1 us and 0 B GC. Fault path buys deterministic recovery and a binary blackbox entry instead of contaminating downstream systems.

## Decision 16: Downstream Stress Consumer Finite Guards

Problem: The physiology authority now rejects non-finite stress, but audio and visor consequence consumers still accepted the latest stress packet and survival vitals through raw `math.saturate` paths. A stale packet from an old session state or another publisher could still push NaN into heartbeat parameters or shader globals.
Solution: Sanitize psycho-metrics stress in `PlayerCriticalProceduralAudioRenderer`, sanitize visor stress/interaction/vital/fog/frost scalar inputs in `PlayerStressVFX`, and reset cached trauma/pulse state if it ever becomes non-finite before shader or heartbeat math runs.
Rejected Alternatives: Trusting the authority-only guard was rejected because shared signal buses can have multiple publishers and stale values. Adding managed error logging in the consumers was rejected because these are hot presentation paths and the physiology blackbox already owns fault evidence.
Scalability potential: Low/MX350 pays only scalar finite checks in existing audio/UI ticks. Middle/High/Ultra can run stronger presentation effects without risking a single bad scalar poisoning global shader state or DSP parameter interpolation.
Hardware Impact: i3/MX350 cost is a handful of branchless-style finite checks around existing scalar math, estimated below 1 us per affected tick and 0 B GC. Avoided fault cascade cost is materially higher because shader/DSP NaN recovery is harder to isolate after publication.
