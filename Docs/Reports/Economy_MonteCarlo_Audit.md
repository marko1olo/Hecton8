# Economy Monte Carlo Audit

Status: ECONOMY PROVEN
Evidence class: CLI_PYTHON + STATIC_DATA. Runtime Unity proof remains PENDING VERIFICATION.

## Inputs

- Distribution source: `C:\Hecton8\Data\Economy\Ore_Distribution.json`
- Source SHA-256: `7416a6cf98c12016e161cee7a65c1d1f22113f81521ef2eef05e30bfc621716f`
- Recipes: `Data/Economy/Recipes.json`
- Note: `Ore_Distribution.json` was used.
- Time model: Time_To_First_Submarine.json: 15.000 harvest minutes / 111 source resources = 0.135135 minutes/node; overhead=20.500; first_base_raw_total=58

## Mandates Followed

- `MATH_Deterministic_RNG_SlotMachine.txt`: integer LCG, integer cumulative weights, multiply-high mapping.
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`: raw resource truth stays data-only.
- `QA_Evidence_Text_Filter_Audit.txt`: claims are labeled as CLI/static evidence, not runtime proof.
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`: no Unity hot path touched.
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`: offline audit replaces runtime probing for this task.

## First Base Raw Demand

Targets: Module_FoundationKit, Module_AirlockFrame, Module_FabricatorBench, Module_PowerRelay, Module_O2Recycler

- `Data_Copper`: 13
- `Data_FiberKelp`: 3
- `Data_GoldOre`: 1
- `Data_HydrocarbonResin`: 8
- `Data_LithiumCrystal`: 3
- `Data_NickelOre`: 3
- `Data_SilicaShards`: 1
- `Data_SilverOre`: 2
- `Data_SulfurClumps`: 6
- `Data_TitaniumScrap`: 18

Titanium requirement: 18
Copper requirement: 13
Dependency coverage gaps: none

## Simulation Parameters

- Players: 10000
- Max nodes per player: 10000
- Monte Carlo node-step floor: 1000000
- Biomes: 10
- Threshold: p99 <= 60.000 minutes
- Node minutes: 0.135135
- Overhead minutes: 20.500
- World seed: 0x48454338

## Results

- Baseline p99 time: 79.555 minutes
- Baseline total mined nodes: 1898602
- Final average time: 41.310 minutes
- Final median time: 40.230 minutes
- Final p90 time: 49.014 minutes
- Final p95 time: 52.264 minutes
- Final p99 time: 59.285 minutes
- Final worst time: 74.419 minutes
- Final p99 nodes: 287.010
- Final total mined nodes: 1539943
- Million-step proof: True
- Copper p99 time: 55.500 minutes
- Copper p99 seed: 0x2CC6F6DF
- Copper worst seed: 0x070AAE21
- Titanium p99 time: 53.608 minutes
- Failures after 10000 nodes: 0
- Tuning applied: True

## Tuning History

- pass 0: p99=79.555 min, copper_p99=59.284 min
- pass 1: p99=59.285 min, copper_p99=55.500 min

## RNG Variance Rationale

Acceptable variance is bounded by p99, not average time. Averages hide unlucky deterministic seeds. The pass condition is no failed player within 10,000 nodes and final p99 time at or below 60 minutes. The 1% copper-starvation seed is reported because copper gates the early electronics chain.

## Regression Model

CPU: offline Python only; no runtime CPU path changed.
GC: no Unity hot path touched; GCMonitor proof is not applicable to this offline audit.
Memory: generated artifacts are bounded files; no runtime memory retained.
Cadence: no Tick/SlowTick/FixedTick cadence changed.
Correctness: recipe closure must resolve all raw dependencies and distribution must cover every raw resource.

## Failure Modes

- If upstream `Ore_Distribution.json` appears later, rerun this tool and compare against the CSV-derived baseline.
- If First Base target scope changes, rerun with an edited target list and do not reuse this report.
- Runtime pickup/craft route remains PENDING VERIFICATION until Play Mode validates actual item acquisition.

## Artifacts

- JSON summary: `Docs\Reports\Economy_MonteCarlo_Audit.json`
- Histogram: `Docs\Reports\Economy_MonteCarlo_TimeToBase_Histogram.png`
- Tuned JSON: `Tools\Economy\Ore_Distribution_Tuned.json`
