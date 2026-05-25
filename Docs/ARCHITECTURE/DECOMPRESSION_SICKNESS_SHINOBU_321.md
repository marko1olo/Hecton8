# Decompression Sickness Route - SHINOBU_321

Status: PENDING VERIFICATION - BUILD AND DATA MONOLITH GATED

Owner: `ShinobuPhysiologyRuntime` in Echelon 5 Combat & Survival Physiology.

## Runtime truth route

- Depth source: cached player AUP snapshot in `ShinobuPhysiologyRuntime.WriteEnvironmentSeed`; sea-level `double3` and player `double3` are subtracted before float depth conversion.
- Tissue truth: `GlobalDataVault` buffer `70221` stores `DecompressionStateDTO` rows with explicit 64-byte layout: fast/medium/slow N2 lanes at offsets `8/16/24`, ambient/risk fields through offset `60`, and warning cadence fields in existing pad slots.
- Tissue working rows: `GlobalDataVault` buffer `70235` stores `TissueCompartmentDTO[entityCapacity * 3]`.
- Coefficients: `GlobalDataVault` buffer `70222` stores `HaldaneTissueCoefficientDTO[3]` loaded from `buhlmann_3tissue_profiles.csv`; legacy `buhlmann_zh16_profiles.csv` is comparison/archive data, not a runtime route.
- Job route: `IntegrateBloodGasTensionsJob` runs deterministic Burst math across exactly three scalar tissue lanes and applies `ThreeTissueRiskCorrection` before warning/damage evaluation.
- Damage route: decompression injury is emitted as `SignalBus<CombatDamageSignal>` with `CombatDamageTypeBarotrauma`; no direct `HectonPlayerHealth` mutation is allowed.
- Black box:
  - Decompression ring: `GlobalDataVault` buffer `73343`, `DecompressionTelemetryEntry[300]`.
  - Fields: depth, ambient pressure, leading tissue tension, allowed ambient pressure, M-value gradient, bubble mask, active compartment count, execution microseconds.
  - Support ring: `PhysiologyTelemetryEntry[300]` in buffer `70226`.
  - Dump version: `2`.
  - Dump path: `Docs/AgentLogs/Dump_SHINOBU_321.bin`.
  - Triggers: invalid decompression math, non-finite telemetry, fatal bends, execution time `>= 200 us`.
- Public read accessors: `TryGet*` methods use `GlobalDataVault.TryReadHandle` through a dedicated read helper. They do not acquire buffers, publish signals, search the scene, or complete jobs.
- Editor writes: decompression tuning and breathing-gas overrides acquire DataVault write locks and release them in `finally`; direct unlocked editor writes are not allowed.

Legacy isolation:
- `HectonSurvivalSystem` immediate velocity-based decompression damage is disabled to avoid a second bends authority path.
- `BaseAtmosphereMath` and survival scalar DCS helpers no longer own nitrogen loading or bends damage; decompression authority is SHINOBU physiology only.
- The old `DcsPhysiologyTunerWindow` is now a compatibility menu shim to `HaldaneanDecompressionTunerWindow`; it no longer owns managed tissue arrays or a duplicate chart.
- `HaldaneanDecompressionTunerWindow` reads `DecompressionTelemetryEntry` for ambient-pressure markers and black-box fault status.
- It falls back to `PhysiologyTelemetryEntry` only if the decompression ring is not ready.
- The editor facade shows authority row plus proof artifact state, not a parallel state model.
- Static proof artifact: `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_321.json`.

Verification still required:
- Unity import, Burst compilation, Console, Play Mode GCMonitor, profiler timing, and player-build proof.
- Loop 7 build attempt ran after CPU/compiler gate opened.
- It stopped on unrelated compile-wall files: `RadiationHazardGrid.cs`, `VRSomaticProvider.Comfort.cs`.
- Final debug M-value fallback patch was static-checked.
- Repeat build was suppressed because `VBCSCompiler` PID `2036` was active.
- Data Monolith readiness is not claimed. `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` exists in the current X_012 scan; route-specific boot proof remains pending; the import/bake/boot pipeline must provide that binary before this route is marked monolith-ready.

Loop 8 hardening:
- `IntegrateBloodGasTensionsJob` no longer uses quality-weighted tissue lane reduction. Gameplay truth is always the same three-lane model; quality may scale downstream presentation and telemetry only.
- Runtime scheduling is clamped to the single player physiology row.
- Reason: owner only writes an AUP-derived environment row for index `0`.
- Rows `1..entityCapacity-1` stay out of the hot path until active-humanoid row ownership exists.

X_009 hardening:
- DCS warnings are edge-or-1Hz gated with no managed cooldown/timer; damage truth remains on the 10 Hz SlowTick.
- Narcosis/gas toxicity no longer emits `CauseDecompression`; gas warnings use a separate cadence field in `GasPhysiologyStateDTO`.
- `StatusEffectStateDTO` is explicit 64 bytes with `ulong StatusEffectMask@0`; poison, burn, stun, radiation, hypoxia, bleeding, crushed, brittle, and crippled routes enter via combat/status masks and SHINOBU physiology bridges.
