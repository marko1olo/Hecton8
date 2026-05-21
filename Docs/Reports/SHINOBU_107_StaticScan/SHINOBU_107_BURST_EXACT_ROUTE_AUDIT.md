# SHINOBU_107 Burst Exact Route Audit

Date: 2026-05-21
Evidence: STATIC_SOURCE / SHINOBU_140 scanner output / source-window audit
Scanner Source: `Docs/Reports/SHINOBU_107_StaticScan/SHINOBU_140_Burst_Job_Directives.json`

## Loop 400 Exact Classifier Tokens

These are exact filename tokens added to `Tools/RunShinobu140StaticScanners.py`. They are not broad folder rules.

| Exact filename token | Rows cleared | Deterministic evidence |
| --- | ---: | --- |
| `cartographygridjobs.cs` | 16 | Cartography reveal, sonar discovery, rollback snapshot copy, RLE save/debug rows, state hash and telemetry. |
| `hullintegritytypes.cs` | 16 | Hull damage, hydrostatic pressure, crush dent, buckling, breach/deformation state, mapped copy and telemetry. |
| `shinobu19economyledger.cs` | 15 | Inventory ledger transactions, crafting DAG/fast-fail, durability, container transfer, hotbar, loot magnet insert/query, telemetry. |
| `buoyancysimdvectorization.cs` | 15 | Hydrodynamic state transforms, AUP localization, spatial query, culling masks, deterministic telemetry. |
| `abyssalthermodynamicsjobs.cs` | 12 | Thermal grid initialize/injection/diffusion/residual reduction, temperature sample, origin shift, telemetry. |
| `chemicalinfluencegrid.cs` | 11 | Chemical source commit, injection, diffusion, grid publish, origin shift, sampling, telemetry. |
| `submarineosthermalgridruntime.cs` | 11 | Topology rebuild, thermal signal injection, power-grid relaxation, short-circuit isolation, external heat, telemetry. |
| `shinobusocketconstructionjobs.cs` | 11 | Socket snapping, bounds verification, builder ghost validation, indirect args, placed-module commit, construction telemetry. |
| `baseatmospherelogisticsjobs.cs` | 10 | Atmosphere CSR topology, gas consumption/source injection/leak, diffusion, quantization, conservation, telemetry. |
| `cablephysicssolver132.cs` | 10 | Tether endpoint advance, cable constraints, spline vertices, mapped GPU copy, indirect args, telemetry. |
| `structuralintegritycalculatortypes.cs` | 9 | Structural pressure, material strength, SDF/AUP anchor, graph stress, collapse signal, edge sever. |
| `powergridjacobicontracts.cs` | 6 | CSR power graph, voltage solve, battery integration, equipment drain, state hash telemetry, AUP mock network. |

## Loop 401 Exact Classifier Tokens

These are exact filename tokens added after a second source-window audit and read-only sidecar audit. They are not broad folder rules.

| Exact filename token | Rows cleared | Deterministic evidence |
| --- | ---: | --- |
| `shinobuphysiologyjobs.cs` | 9 | Tissue compartments, pressure/gas state, oxygen/CNS toxicity, physiology damage signal emission, state-hash telemetry. |
| `shinobulogisticsrouter.cs` | 8 | CSR logistics graph, power/oxygen/pressure flow solve, AUP local shift, state flags, blackbox state hash. |
| `buoyancydisplacementjobs.cs` | 7 | Buoyancy state, wake triggers, ambient currents, force packets, sleep/grounding SDF, telemetry. |
| `vehiclecomponentdamagejobs.cs` | 7 | Vehicle damage grid, impact mapping, integrity reduction, hazard emission, damage state publish. |
| `macroecosystemmathematicianruntime.cs` | 7 | Ecosystem sector front/back state, population solve, biomass diffusion, sector index, telemetry. |
| `sumppumppipegridjobs.cs` | 7 | Pump nodes, pipe CSR graph, pressure solver, edge flow, water evacuation, state hash telemetry. |
| `bulkheadcontainmentjobs.cs` | 7 | Bulkhead state, door lock/override, collision result, catastrophic damage, AUP distance checks, telemetry. |
| `habitatfluidincursionjobs.cs` | 6 | Fluid compartments, hull breach ingress, BFS pressure equalization, waterline mass summary, telemetry. |
| `thermodynamicshazardgridruntime.cs` | 6 | Heat/radiation source emission, diffusion, rebase, hazard grid telemetry, AUP origin hash. |
| `fabricationassemblerruntime.cs` | 5 | Fabrication timing/progress, emitted signals, power/crafting state, rollback-safe deterministic progress. |
| `scavenginglootoracle.cs` | 5 | Loot table resolution, harvest request, biome modifiers, yield publish, save/economy outcome. |
| `inventorysoautility.cs` | 4 | Inventory transfer validation, compaction/defragment, item hashes/counts, condition decay. |
| `hectonanomalysdfjobs.cs` | 7 | Terrain/SDF snapping, anomaly pillar/fissure injection, voxel density writes, AUP terrain sampling. |

## Rejected Tokens

- Broad folder tokens such as `World`, `Physics`, `Construction`, `Power`, `Habitat`, `Atmosphere`, `Inventory`, `Cartography`, `Thermodynamics`, or `UI`.
- Broad semantic tokens such as `pressure`, `damage`, `signal`, `state`, `grid`, `thermal`, or `power`.
- SHINOBU_200 `SignalWardenRuntime.cs`, untracked `WalIntegrityFuzzerCore.cs`, and untracked `EmergencyMockOceanKinematicsAdapter.cs`.
- Whole-file tokens rejected in Loop 401 despite deterministic rows: `UpgradeMatrixCompiler.cs`, `BiomeTransitionFogBlendJobs.cs`, `TerminalOsTypes.cs`, `DroneFleetNavigationKernel.cs`, `TradeMarauderRuntime.cs`, and `BallisticsRuntime.cs`; each mixes authority jobs with presentation/tool/visual rows that need owner proof before classifier expansion.

## Loop 402 Exact Classifier Tokens

These exact filename tokens survived the sidecar/source-window audit. They clear deterministic authority rows only; they do not create folder or semantic exemptions.

| Exact filename token | Rows cleared | Deterministic evidence |
| --- | ---: | --- |
| `submarineautopilotsdfnavigator.cs` | 6 | Vehicle SDF navigation, sector-local AUP flow sampling, avoidance, desired velocity, and blackbox telemetry. |
| `submarinedynamicscontracts.cs` | 5 | Added-mass, damping, force integration, 6D submarine kinematics, and deterministic telemetry contracts. |
| `worldregrowthsimulation.cs` | 5 | Regrowth diffusion, tombstone decay, world-state integration, and telemetry over persistent sector facts. |
| `shinobumetabolismjobs.cs` | 5 | Metabolism initialization, physiology integration, combat signal emission, and deterministic physiology telemetry. |
| `spaceengine098terrainkernels.cs` | 5 | Ridged terrain, crater/rille kernels, pipeline metric generation, and deterministic finite checksums. |

## Loop 402 Rejected Mixed Tokens

- `worldvolumetricbiomeclassificationjobs.cs`: contains deterministic biome classification rows, but also stress/audit harness rows that need owner proof.
- `stressdrivenspawndirector.cs`: contains spawn-state rows, but also preload/debug/mock support rows.
- `scannerdataminingrouter.cs`: contains deterministic scan rows, but also mock/tool/presentation lanes and shader/VFX route data.
- `hydraulicerosionjob.cs`: contains erosion rows, but also silt paint-mask/normalization publication rows.
- `proceduralcoraljobs.cs`: contains mixed render matrix extraction, indirect draw arguments, GPU sway, and bioluminescence presentation rows.

## Result

- Loop 400-402 cumulative `Burst_Job_Directives`: `522 -> 272`
- Loop 402 local `Burst_Job_Directives`: `298 -> 272`
- Loop 400-402 cumulative `totalCritical`: `602 -> 352`
- Loop 402 local `totalCritical`: `378 -> 352`
- `Static_Gate_Regression`: `0`

No C# runtime source, DTO layout, SignalBus lane, DataVault ownership, save identity, dispatcher schedule, or SHINOBU_200-owned signal-thread file changed in Loops 400-402.
