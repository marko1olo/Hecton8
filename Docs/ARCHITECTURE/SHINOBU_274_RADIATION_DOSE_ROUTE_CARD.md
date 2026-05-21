# SHINOBU_274 Radiation Dose Route Card

Status: YELLOW_STATIC_REVIEW_PENDING_UNITY_IMPORT
Date: 2026-05-21
Owner: SHINOBU_274 / Radiation Scrubber

## Fact Ownership

- Radiation physiological truth: `RadiationStateDTO` in `GlobalDataVault`.
- Radiation source truth: `RadiationSource` lane in `GlobalDataVault`.
- Health truth: existing `HectonPlayerHealth`; only `RadiationHazardGrid` applies radiation fatigue through `SetRadiationExposure` after owning dose integration and emits `SignalBus<CombatDamageSignal>` for sickness damage.
- Shielding truth: read-only `BulkheadStateDTO`/`BulkheadPlaneDTO` from SHINOBU_220 and Voxel SDF read model. SHINOBU_274 does not mutate construction state.

## Route

1. Cold boot gets Vault handles for `Shinobu274Radiation*` buffers.
2. Radiation sources enter via `SignalBus<RadiationSourceSignal>`; legacy `HazardZoneManager.RegisterZone(... Radiation)` is redirected to this source lane.
3. Compatibility reads through `HectonHazardManager.GetHazardIntensity(... Radiation)` sample `RadiationHazardGrid` directly; they do not query `HazardZoneManager`.
4. Atmospheric, solar, and radioactive trauma deltas enter via `SignalBus<RadiationDoseSignal>` and are drained only by `RadiationHazardGrid` into an exact pending-dose lane.
5. `SystemDispatcher` Simulation phase schedules `CalculateRadiationExposureJob`.
6. Job reads source AUPs, player AUP, Voxel SDF bytes, and bulkhead DTOs; writes `RadiationStateDTO` and one pending `CombatDamageSignal` lane.
7. PostSimulation reads completed Vault state, bridges combat damage to `SignalBus<CombatDamageSignal>`, and updates `HectonPlayerHealth`.
8. VisualSync uploads scalar globals for visor static and UberNoir hand vertex mutation.

## Phase And Failure Mode

- Simulation: schedules Burst jobs and returns the `JobHandle` to `SystemDispatcher`.
- Simulation does not drain source/dose signals or rebuild grids while a previous radiation job is active; deferred processing is preferred over mutating Vault lanes under a live reader.
- If a previous radiation job is still active at the next Simulation phase, source signals are requeued to the typed SignalBus for the next flush, external dose is folded into `_pendingExternalDoseRad`, and iodine treatment is folded into `_pendingIodineDoseReductionRad`. This prevents PostSimulation snapshot clearing from dropping gameplay facts without forcing completion.
- Read-only compatibility intensity queries use the stable read grid while a radiation job is active; inverse-square source sampling resumes after the job is finalized.
- PostSimulation: consumes only after dispatcher completion window.
- VisualSync: shader globals only; no gameplay authority.
- Save serialization does not force-complete active jobs; it writes the last completed dose and current read-grid snapshot. Live load and DataVault hot-swap are deferred until PostSimulation observes no active radiation/diffusion job. Forced completion is teardown/disposal-only, where buffers are being released.
- If Vault handles are unavailable, the system fails closed and keeps the last state; it does not allocate local NativeArrays or run managed dose fallback.

## Exact Dose And Grid Safety

- External dose uses `_pendingExternalDoseRad`, so atmospheric/solar/trauma rads are included once as exact dose. External intensity still drives `CurrentExposureRate` and shader/static severity but is not multiplied by `dt` a second time.
- Iodine reductions consume pending external dose before accumulated dose to prevent same-frame hidden radiation debt.
- Diffusion read/write parity is tracked by `_gridBuffersSwapped`; `RefreshVaultViews` maps the current front/back buffers without copying the whole grid.

## Proof Artifacts

- `Docs/Tasks/Status_SHINOBU_274.md`
- `Docs/AgentLogs/Rationale_SHINOBU_274.md`
- `Docs/AgentLogs/LOG_SHINOBU_274.md`
- `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`
- `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_274.json`
- `Docs/AgentLogs/Dump_SHINOBU_274.bin` on NaN/radiation death

Review disposition: YELLOW until Unity import/Console, Play Mode, profiler GC, and player build proof exist.
