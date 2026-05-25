# SHINOBU_274 Radiation Dose Route Card

Status: YELLOW_STATIC_REVIEW_PENDING_UNITY_IMPORT
Date: 2026-05-21
Owner: SHINOBU_274 / Radiation Scrubber

## Fact Ownership

- Radiation physiological truth: `RadiationStateDTO` in `GlobalDataVault`.
- Radiation source truth: `RadiationSource` lane in `GlobalDataVault`.
- Health truth: existing `HectonPlayerHealth`; only `RadiationHazardGrid` applies radiation fatigue through `SetRadiationExposure` after owning dose integration and queues `CombatStatusBits.Irradiated64` for sickness status.
- Shielding truth: read-only `BulkheadStateDTO`/`BulkheadPlaneDTO` from SHINOBU_220 and Voxel SDF read model. SHINOBU_274 does not mutate construction state.

## Route

1. Cold boot gets Vault handles for `Shinobu274Radiation*` buffers.
2. Radiation sources enter via `SignalBus<RadiationSourceSignal>`; legacy `HazardZoneManager.RegisterZone(... Radiation)` is redirected to this source lane.
3. Compatibility reads through `HectonHazardManager.GetHazardIntensity(... Radiation)` sample `RadiationHazardGrid` directly; they do not query `HazardZoneManager`.
4. Atmospheric, solar, and radioactive trauma deltas enter via `SignalBus<RadiationDoseSignal>` and are drained only by `RadiationHazardGrid` into an exact pending-dose lane.
5. `SystemDispatcher` Simulation phase schedules `CalculateRadiationExposureJob`.
6. Job reads source AUPs, player AUP, Voxel SDF bytes, and bulkhead DTOs; writes `RadiationStateDTO` and one local 32B `RadiationStatusSignal` critical-status staging row.
7. PostSimulation reads completed Vault state, queues `CombatStatusBits.Irradiated64` through `CombatDamageRuntime.TryQueueStatusEffect`, and updates `HectonPlayerHealth`.
8. VisualSync uploads scalar globals for visor static and UberNoir hand vertex mutation.

## Phase And Failure Mode

- Simulation: schedules Burst jobs and returns the `JobHandle` to `SystemDispatcher`.
- Simulation does not drain source/dose signals or rebuild grids while a previous radiation job is active; deferred processing is preferred over mutating Vault lanes under a live reader.
- Active previous radiation job:
  - requeue source signals to typed SignalBus for next flush;
  - fold external dose into `_pendingExternalDoseRad`;
  - fold iodine treatment into `_pendingIodineDoseReductionRad`;
  - prevent PostSimulation snapshot clearing from dropping gameplay facts;
  - do not force completion.
- Read-only compatibility intensity queries use the stable read grid while a radiation job is active; inverse-square source sampling resumes after the job is finalized.
- PostSimulation consumes only after dispatcher completion window.
- Publishes completed radiation state, pending `RadiationStatusSignal -> CombatStatusBits.Irradiated64`, dose signal, geiger signal, and telemetry even when deferred load/DataVault swap waits for diffusion.
- Structural mutation applies only when no radiation or diffusion job is active.
- While deferred load/DataVault swap waits for diffusion completion, Simulation pauses new radiation evaluation and preserves source, external-dose, and iodine snapshots instead of clearing or dropping them.
- VisualSync: shader globals only; no gameplay authority.
- Save serialization does not force-complete active jobs.
- It writes last completed dose and current read-grid snapshot.
- Live load and DataVault hot-swap defer until PostSimulation observes no active radiation/diffusion job.
- Forced completion is teardown/disposal-only.
- If Vault handles are unavailable, the system fails closed and keeps the last state; it does not allocate local NativeArrays or run managed dose fallback.

## Exact Dose And Grid Safety

- External dose uses `_pendingExternalDoseRad`; atmospheric/solar/trauma rads are included once as exact dose.
- External intensity still drives `CurrentExposureRate` and shader/static severity, without second `dt` multiply.
- Iodine reductions consume pending external dose before accumulated dose to prevent same-frame hidden radiation debt.
- Diffusion read/write parity is tracked by `_gridBuffersSwapped`; `RefreshVaultViews` maps the current front/back buffers without copying the whole grid.
- Public `RegisterSource` and `ReportExternalDose` scalar ingress is explicit finite-safe before SignalBus payload construction. Non-finite source intensity is rejected; non-finite external intensity fails closed to zero.
- Public `RegisterSource` zero/invalid normalized intensity emits `UnregisterSource(sourceId)`.
- This matches owner drain and prevents stale source truth when reactor/anomaly fades.
- Invalid/non-positive radius falls back to `DefaultSourceRadiusMeters`.
- `Dump_SHINOBU_274.bin` row order now matches `RadiationTelemetryEntry` explicit layout: AUP, depth, exposure, cumulative dose, shield, degradation, burst microseconds, frame, shift sequence, source count, source version, flags.

## Generic HazardZoneManager Exception

- `HazardZoneManager` still owns legacy non-radiation scene-scratch buffers for generic heat/toxicity/biohazard volumes (`_volumes`, `_volumeIds`, `_volumeSpatialHandles`, `_volumeCurveLutSamples`, `_jobVolumes`, `_candidateVolumeFlags`, `_spatialQueryHandles`).
- SHINOBU_274 excludes radiation from those generic buffers: radiation registration and reads route to `RadiationHazardGrid`, and completed generic hazard jobs zero radiation cache slots before publishing non-radiation masks.
- Exception scope: existing non-radiation compatibility scratch only. Owner `HazardZoneManager`; capacity `maxZoneCount`; allocations registered with `NativeMemorySentinel`; disposal completes active exposure work before release.
- Remaining generic scratch buffer migration is outside Radiation Scrubber authority and belongs to hazard/environment ownership. Radiation payload correctness does not depend on those private buffers.

## Proof Artifacts

- `Docs/Tasks/Status_SHINOBU_274.md`
- `Docs/AgentLogs/Rationale_SHINOBU_274.md`
- `Docs/AgentLogs/LOG_SHINOBU_274.md`
- `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`
- `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_274.json`
- `Docs/AgentLogs/Dump_SHINOBU_274.bin` on NaN/radiation death

Review disposition: YELLOW until Unity import/Console, Play Mode, profiler GC, and player build proof exist.
