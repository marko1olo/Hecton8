# [ARCHIVE] Pre-Strict Architecture Snapshot

Date: 2026-05-24
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Original: Docs/ARCHITECTURE/SHINOBU_311_ACOUSTIC_SENSORY_ROUTE.md
Rule: historical snapshot only; not active doctrine.

# SHINOBU_311 Acoustic Sensory Route

Owner: `PredatorCognitionDomain`.
Phase: AI simulation after SignalBus snapshot staging, before `PredatorCognitionJob`.
Buffers: `72760..72768` for stimuli, 64-byte counter, 128-byte parallel result rows, telemetry, profiles, tuning, CSV scratch. Parasite VFX owns `71980..71987` plus `71989,71990`; SHINOBU_311 explicitly does not use those IDs.
Race guard: acoustic SignalBus staging is owned by `ScheduleFrameEvaluation` after `_evaluationScheduled` rejects overlapping evaluation chains. `BeginDispatcherFrame` must not mutate acoustic Vault stimuli.
Admission guard: if `SwarmAnalysisJob` admission fails while acoustic work is staged, `_lastScheduledFrame` is not advanced and `_acousticSdfPendingStimulusRetry` preserves the staged counter/stimuli across later frames until the acoustic chain consumes them. The latch has no write-only frame sidecar; the pending-retry flag is written into the 64-byte counter through the mutable owner handle only.
Cold-path guard: frame-owned staging, idle telemetry, and acoustic integration only proceed when acoustic Vault handles are already created; they do not allocate Vault buffers or load CSV fallback data from the hot scheduler path.

- Route: `CombatDamageSignal`, `AcousticPingSignal`, and `MovementAcousticSignal` are staged into `AcousticStimulusDTO[128]` in that priority order.
- Combat receives a half-capacity quota, ping receives a quarter-capacity quota, and movement consumes the remaining capacity.
- Non-finite ingress marks `AcousticCounterFlagInvalidIngress` before the bad signal is rejected.
- Dropped valid stimuli are counted in `AcousticCounter64DTO.Reserved0`, flags are copied into `SensoryTelemetryEntry.Reserved1`, and `AcousticFaultStimulusOverflow` / `AcousticFaultNonFinite` mark the telemetry row.
- `GenerateMockAcousticSignalsJob` is an opt-in editor/stress-test `IJobParallelFor`; it fills deterministic fixed mock slots only when no real acoustic signals exist and the Vault tuning flag is enabled.
- If no real or mock acoustic stimuli are present, including frames where no predator cognition slot is due, the owner writes an idle telemetry row and clears stale acoustic result rows for active predators before the first job is scheduled.
- Idle telemetry copies the staged counter flags and dropped-stimulus count before clearing stale results; invalid-only ingress therefore records `AcousticFaultNonFinite` and dumps the raw blackbox without scheduling empty acoustic jobs.
- Silent frames do not call the acoustic integration scheduler after job handoff and do not schedule the attenuation/occlusion/telemetry job chain.
- `CalculateAcousticAttenuationJob` computes inverse-square attenuation after double-precision AUP subtraction.
- `EvaluateAcousticOcclusionJob` culls candidates below the hearing threshold before SDF sampling, then applies SDF ray probes scaled continuously by `GlobalQualityWeight` and injects heard results into cognition drives plus acoustic memory.
- Acoustic occlusion reads the Vault-published `VoxelSdfTexture3D` route through Core contracts; samples outside the published SDF volume fail open at `1.0` instead of applying false dampening.
- Old world singleton bridges are fallback only for the inherited non-acoustic threat snapshot path.
- `AcousticTuningDTO.MaxDistanceMeters` now gates both attenuation and occlusion jobs by clamping profile max distance squared; the X-Ray editor facade exposes max distance and fault budget instead of resetting them to constants on every tuning write.
- `RecordAcousticTelemetryJob` writes a 300-frame `SensoryTelemetryEntry` ring and triggers `Docs/AgentLogs/Dump_SHINOBU_311.bin` on budget or non-finite fault.
- Finalization patches the latest row with measured chain microseconds before dump.
- The dump path is cached during cold initialization.
- If cold path setup fails, later cold initialization or tuning-safe routes may retry; the fault writer itself never retries managed path or directory resolution and fails closed when no cached path exists.
- The dump itself is a 16-byte little-endian header followed by raw 64-byte telemetry rows, and no field-by-field managed writer owns the forensic format.

Diagnostic facade:
Telemetry, result, stimulus, and count readers return false/zero while `_evaluationScheduled` is true and use Vault read handles. They are presentation snapshots only, not a sync point. The mutable X-Ray tuning write route also returns false while `_evaluationScheduled` is true, before opening the tuning Vault or entering the cold allocation route.
Hot owner-phase read-only helpers that only inspect staged counters or mock tuning flags also use Vault read handles; mutable `Open()` remains reserved for owner writes, job output buffers, cold initialization, and the fenced tuning write bridge.

Do not add predator hearing colliders, managed listener fan-out, or new acoustic signal lanes for creature hearing. The editor scanner treats any member `ClosestPoint(...)` call in scoped AI/Fauna/Sensory source as a forbidden collider-style acoustic query unless future semantic proof is added.

Compile proof: Loop 29 changed runtime/editor C# after the prior clean SHINOBU_311 narrow compile. Current compile marker is `PENDING_AFTER_LOOP29_CPU_GUARD_BLOCKED`; rebuild was not launched because CPU/compiler guard remained blocked. The previous guarded Loop 21 proof reported no SHINOBU_311 source errors and external `ConstructionManager.cs` blockers only.
