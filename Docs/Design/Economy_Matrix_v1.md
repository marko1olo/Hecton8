# Economy Matrix v1

Date: 2026-05-14
Status: ECONOMY BALANCED by `Tools/EconomyValidator.py`; literal 30 kW energy pacing and runtime Unity proof remain PENDING VERIFICATION.
Agent: ECONOMY_DATA_BALANCER
Domain: Auxiliary Node, text/data only

## Authority Boundary

Static documentation only. Current source, active architecture contracts, fresh proof artifacts, and official platform rules override dated claims in this file. No runtime, profiler, memory, render, platform, public-page, or ship-readiness proof is implied by this file alone.

## Generated Files

- `Data/Economy/Resource_Distribution_Matrix.csv`
- `Data/Economy/Recipes.json`
- `Data/Economy/Items.csv`
- `Data/Economy/Survival_Stats.json`
- `Data/Economy/Runtime_Binding_Review.json`
- `Data/Economy/Runtime_Binding_Plan.json`
- `Data/Economy/Time_To_First_Submarine.json`
- `Tools/EconomyValidator.py`

## Mandates Followed

- `DATA_Inventory_Resources_Items_SOA_Layout.txt`: static IDs, no hot-path strings, item data as offline table.
- `MATH_Deterministic_RNG_SlotMachine.txt`: resource spawn tables use integer LCG weights.
- `CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt`: O2 drain scales from stress and depth pressure.
- `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`: fabrication costs are explicit kWh values.
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`: all runtime-facing IDs carry precomputed hashes.
- `OPT_Premium_Approximation_Protocol.txt`: survival drain uses table math, not simulated body chemistry.

## Hash Contract

All runtime-facing IDs use the project hash convention: `Hecton.Localization.LocHash.Compute`, stored as unsigned decimal `hash32` values. This is FNV-1a 32-bit over UTF-16 code units in case-preserving order. The validator checks every `*_id` field with a sibling `*_hash32` field. Example proof: `Data_TitaniumScrap` now hashes to unsigned `3511699502`, the same bit pattern as signed runtime hash `-783267794`.

## Runtime Binding Review

Static data was checked against existing `stableId` / `m_Name` surfaces under `Assets/_Project/Data/Items`, `Assets/_Project/Data/Construction`, `Assets/_Project/Data/Lore/SuitUpgrades`, `Assets/_Project/Data/Tools`, and `Assets/_Project/Data/Crafting/Recipes`.

- Economy recipe graph references 55 unique item/build IDs.
- 33 IDs already match discovered project data IDs.
- 22 IDs are economy-defined module/upgrade targets and require either importer mapping or new authoring assets before direct runtime catalog use: `Module_AirlockFrame`, `Module_FabricatorBench`, `Module_FoundationKit`, `Module_O2Recycler`, `Module_PowerRelay`, `Module_PressurizedContainer`, `Module_StorageLocker`, `Module_SumpPump`, `Upgrade_AbyssPressureShell`, `Upgrade_AbyssalStabilizer`, `Upgrade_BallastOptimizer`, `Upgrade_DepthCompensator`, `Upgrade_EmergencyO2Rack`, `Upgrade_EngineOverdriveManifold`, `Upgrade_GuidanceModule`, `Upgrade_HighCapacityCell`, `Upgrade_HullArmorLattice`, `Upgrade_HydraulicActuator`, `Upgrade_ReactorBypassCoupler`, `Upgrade_SilentRunningBaffle`, `Upgrade_SonarAmplifier`, `Upgrade_ThermalShielding`.

This is not a Unity runtime proof. It is a static integration warning so the Data Monolith importer does not silently treat these IDs as current `ItemCatalog` entries.

`Data/Economy/Runtime_Binding_Review.json` exists and records the static source scan behind the 22 unresolved economy-defined IDs. Suggested IDs in that review are candidates only; they are not runtime mappings.

`Data/Economy/Runtime_Binding_Plan.json` exists and keeps all 22 unresolved rows blocked behind owner decisions. `runtime_use_allowed_count` remains `0`; no unresolved row is runtime-approved by this document.

## Scarcity Formula

Resource rarity score:

```text
rarity_score = base_rarity
             + (distance_from_origin_m / 1000)
             * depth_multiplier
             * resource_depth_bias
```

LCG spawn weight:

```text
lcg_spawn_weight_u16 = max(1, round(authored_weight / (1 + distance_term * 0.08)))
distance_term = (distance_from_origin_m / 1000) * depth_multiplier * resource_depth_bias
```

This preserves deterministic integer weighted selection while making farther and deeper resources economically rarer. Deep biomes can still favor a rare resource through authored affinity; the rarity score remains higher and is visible to the balancing layer.

## Resource Matrix

- Biomes: 10.
- Resources: 15.
- Rows: 150.
- Quartz request is mapped to the existing project material `Data_SilicaShards`, because current content ledger uses silica/glass rather than `Data_Quartz`.
- Laser Cutter node break counts are included per resource for tool tiers 1-3.

## Crafting Progression

`Recipes.json` contains 40 recipes:

- Tier 1 components: 12 recipes.
- Tier 2 tools and base modules: 14 recipes.
- Tier 3 submarine upgrades: 14 recipes.

Progression medians from the generated data:

```text
Tier 1 median cost: 4.35 baseline units
Tier 2 median cost: 15.95 baseline units
Tier 3 median cost: 42.90 baseline units
Tier2/Tier1 ratio: 3.667
Tier3/Tier2 ratio: 2.690
```

The validator accepts ratios in `[2.4, 3.8]`. This keeps the requested 3-to-1 ladder without forcing every individual recipe into identical cost shape.

## Survival Metrics

O2 drain:

```text
liters_per_second = base_liters_per_second
                  * stress_multiplier
                  * depth_pressure_multiplier
                  * activity_multiplier
```

Base O2 is `0.10 L/s` against a `600 L` tank. Stress, pressure, and activity are table-driven. Fast swim calorie burn is exactly `9.0 kcal/min`, which is 3x the slow swim value of `3.0 kcal/min`.

Minimum quality uses direct table lookup on SlowTick. Intermediate weights can interpolate. High and maximum quality only improve presentation smoothing and warning feedback; gameplay drain stays table-authoritative.

## Time To First Submarine

Definition used for this matrix: first functional wet-sub handoff requires:

```text
Module_FoundationKit
Module_AirlockFrame
Module_FabricatorBench
Module_PowerRelay
Module_O2Recycler
Upgrade_DepthCompensator
Upgrade_BallastOptimizer
```

Fully expanded raw resource requirement:

```text
Data_TitaniumScrap: 38
Data_Copper: 22
Data_HydrocarbonResin: 15
Data_SulfurClumps: 11
Data_NickelOre: 9
Data_LithiumCrystal: 6
Data_SilverOre: 5
Data_FiberKelp: 3
Data_GoldOre: 1
Data_SilicaShards: 1
```

Top-level target recipes require `163.9 kWh` and `22.8 s` machine craft time. That number is not the full expansion. Recursive prerequisites require 46 recipe batches across 17 unique recipes, `433.1 kWh`, and `81.3 s` machine craft time.

At a literal `30 kW` energy source, recursive fabrication alone waits `866.2 minutes`; total static route + harvest + handling + energy wait is `901.7 minutes`. Therefore the old `41 minute` estimate is invalid if `fabrication_kwh` is literal player-gated energy. It only holds as a route/harvest pacing estimate if fabrication energy is precharged, abstracted, or supplied by a much stronger source. `Time_To_First_Submarine.json` records this as `economy.path.requires_literal_energy_rebalance`, and the validator now proves the recursive expansion.

## Regression Model

- CPU: data files are offline tables. Runtime work should be integer hash lookup and table sampling only.
- GC: no runtime strings are required if the engine consumes hash fields.
- Memory: CSV/JSON are authoring/input assets; Data Monolith should bake them into contiguous runtime blobs.
- Correctness: validator checks project-compatible hashes, row counts, duplicate biome/resource pairs, exact recipe category quotas, recipe cycles, value math, result value parity, no-profit deconstruction, tier progression, `Items.csv` exact raw/crafted item set and source-recipe parity, survival band ranges, runtime binding drift, binding-plan blocked status, time-to-first-submarine recursive expansion, 5-50 recursive batch pacing, milestone row/result consistency, and global generated-file hash collisions.
- Failure modes: missing engine importer, economy-defined IDs not mapped to runtime assets, or runtime system still reading strings would invalidate runtime integration.
