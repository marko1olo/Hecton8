# SHINOBU_272 Physiological Gas Toxicity

Owner: `Hecton8.Physiology.ShinobuPhysiologyRuntime`

Runtime truth:
- Breathing fractions live in local Vault buffer `70214` (`ShinobuPhysiologyConstants.BreathingGasFractionsBuffer`).
- Gas tuning lives in local Vault buffer `70215` (`ShinobuPhysiologyConstants.GasPhysiologyTuningBuffer`).
- Dalton partial pressures and gas stress live in local Vault buffer `70239` (`ShinobuPhysiologyConstants.GasPhysiologyStatesBuffer`).
- Nitrogen tissue tension is integrated from N2 partial pressure, not total ambient pressure.
- Health and survival do not own gas truth. They consume `PhysiologyStateSignal`, `SurvivalVitalsChangedSignal`, and `CombatDamageSignal`.
- `PhysiologyStateSignal` owns the stable SHINOBU source hash, gas toxicity cause, and gas status mask constants so health does not guess cross-domain bit positions.

Hot routes:
- Simulation: `GenerateMockBreathingGasJob` -> `CalculatePartialPressuresJob` -> `IntegrateBloodGasTensionsJob` -> `CalculateCnsToxicityJob` -> `OxygenConsumptionJob`.
- Damage: severe CNS/CO2/hypoxia emits unmanaged `CombatDamageSignal`.
- Visual: Physiology publishes only unmanaged `PhysiologyStateSignal`/`HypoxiaSignal`; `GlobalShaderDispatcher` owns shader-slot projection into slot 7/11 (`_HectonDcsPhysiologyParams`, `_HectonGasToxicityParams`, `_HypoxiaSignal`).
- Runtime tick does not poll CSV files. `physiological_gas_profiles.csv` and `tissue_halftime_profiles.csv` are loaded only during cold Vault initialization; editor sliders write the Vault tuning rows directly.
- Editor/test injectors and diagnostic readers fail closed while a physiology job is scheduled.
- Scheduled jobs own Vault locks through seed, resolve, schedule, post-job publish, telemetry patch, visual sync, fatal dump check, and unlock.

Black box:
- `PhysiologyTelemetryEntry` ring remains 300 entries.
- Fatal gas, fatal bends, or invalid math dumps to `Docs/AgentLogs/Dump_SHINOBU_272.bin`.

Validation:
- `Physiology_OOP_Scanner` writes `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_272.json` and upserts the `shinobu272PhysiologyOopScanner` summary into `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`.
- Latest static verification: DTO property scan 0 hits; direct Physiology health/depth-damage/shader-bridge scan 0 runtime hits; report JSON parse OK; compile not launched while CPU preflight reports 100%.
