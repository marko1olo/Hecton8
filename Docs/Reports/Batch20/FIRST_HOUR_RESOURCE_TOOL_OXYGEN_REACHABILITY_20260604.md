# First-Hour Resource, Tool, Oxygen Reachability Audit

Date: 2026-06-04  
Scope: Static file audit only. No Unity, no build, no Assets edits.  
Evidence boundary: Text/data/source inspection can identify blockers and proof gaps. It does not prove runtime reachability, spawn placement, interaction wiring, save/load, or death/respawn behavior.

## Authority Read

Root/project authority read: `AGENTS.md`, `TASTE.md`, `VISION_LOCKS.md`, `PROJECT_BIBLES.md`, `gameplay.md`, `survival.md`, `inventory.md`, `tools.md`, `construction.md`, `persistence.md`.

Relevant mandates read:

- `.agents-skills/DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `.agents-skills/CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt`
- `.agents-skills/CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `.agents-skills/DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`

`Docs/Actual Domains of Project.txt` was not present. Narrow domain inferred as first-hour gameplay/resource/tool/O2/save-respawn reachability.

## Top Static Findings

1. `ResourceNodeTemplate_CopperVein.asset` is Drill-gated and starts at 40 m. Exact values: `requiredToolClass: 2`, `minimumDepthMeters: 40`, `maximumDepthMeters: 420`, `defaultLootCount: 2`, yield `Data_Copper` amount 1-2.

2. The standard player prefab does not grant a Drill tool. `Assets/_Project/Prefabs/Player.prefab` quick-slot `toolPrefabs` point to Scanner, Repair, Builder, and LaserCutter. `Tool_SalvageSampler_Held.prefab` is only in `knownToolPrefabs`, not assigned. No SeafloorDrill prefab is serialized.

3. `SeafloorDrillTool.cs` exists and returns `ToolCapabilityMasks.Drill`, but both canonical authored asset paths are missing:
   - `Assets/_Project/Data/Items/Tools/Item_Tool_SeafloorDrill.asset`: missing
   - `Assets/_Project/Prefabs/Tools/Held/Tool_SeafloorDrill_Held.prefab`: missing

4. LaserCutter is not a CopperVein bypass. `LaserCutter.GetCapabilityMask()` returns `ToolCapabilityMasks.PlasmaCut`, which resolves to `Cut | Burn | Laser`, while CopperVein resolves to `Drill`.

5. FirstHourDirector currently points at copper: `firstResourceItemId = "Data_Copper"` and `firstCraftResultItemId0 = "Comp_CopperWire"`. It also lists `Data_EmergencyO2Canister`, `Item_Tool_BeaconDeployer`, `Item_Tool_Repair`, and `Comp_PressureSeal` as later first-hour craft milestones.

6. There is a possible non-drill copper source: `Assets/_Project/Data/World/HarvestableTemplate_TitaniumOutcrop.asset` has weighted loot entries for `Data_TitaniumScrap` 2-4 weight 5, `Data_SilverOre` 1-2 weight 1, and `Data_Copper` 1 weight 1. `HarvestableOutcrop.cs` does not apply a tool capability gate to `ApplyInteractionSignal`. This is not proof that a first-hour copper outcrop is placed, wired, reachable, or quest-compatible.

7. Oxygen is statically plausible for a shallow 40 m copper trip, but not proven. `Standard_Suit_V1.asset` has `maxOxygen: 139.2400`, `oxygenConsumptionRate: 0.0150`, and `safeDepth: 50`. `HectonSurvivalSystem` pressure scaling is `1 + depth * 0.1`, clamped 1-16. At 40 m, pressure scale is 5x. Minimal drain is about `0.075 O2/sec`, or 30.9 minutes from full reserve before grace; cruise movement at the 1.55 ceiling is about 20.0 minutes. Stress, leaks, carry mass, barotrauma, and route mistakes reduce that.

8. Copper max depth exceeds starter safe depth by 370 m. Copper is safe only in the shallow edge of its 40-420 m range. A first-hour copper route needs authored shallow placement or constrained spawn proof.

9. Crafting authority is inconsistent. `Recipe_CopperWire.asset` requires 1 `Data_Copper`; `Data/Economy/Crafting_Costs.json` records `Recipe_CopperWire` as requiring 2 `Data_Copper`. That is a one-fact/two-owner violation until the runtime source of truth is declared and validated.

## Exact Starter Resource Data

Primary resource-node assets:

| Path | Stable ID | Display | Tool Class | Depth m | Probability | Yield |
|---|---|---:|---:|---:|---:|---|
| `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_FiberKelpStand.asset` | `resource.node.fiber_kelp_stand` | Fiber Kelp Stand | 0 Any | 0-140 | 0.82 | `Data_FiberKelp` |
| `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_SilicaShardCluster.asset` | `resource.node.silica_shard_cluster` | Silica Shard Cluster | 0 Any | 0-260 | 0.78 | `Data_SilicaShards` |
| `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_TitaniumScrap.asset` | `resource.node.titanium_scrap` | Titanium Scrap Field | 4 Salvage | 0-220 | 0.88 | `Data_TitaniumScrap` |
| `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_CopperVein.asset` | `resource.node.copper_vein` | Copper Vein | 2 Drill | 40-420 | 0.72 | `Data_Copper` |
| `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_MembraneTissueBloom.asset` | `resource.node.membrane_tissue_bloom` | Membrane Tissue Bloom | 0 Any | 80-460 | 0.60 | `Data_MembraneTissue` |
| `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_HydrocarbonResinPod.asset` | `resource.node.hydrocarbon_resin_pod` | Hydrocarbon Resin Pod | 0 Any | 120-540 | 0.62 | `Data_HydrocarbonResin` |
| `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_SulfurVentClump.asset` | `resource.node.sulfur_vent_clump` | Sulfur Vent Clump | 0 Any | 120-680 | 0.56 | `Data_SulfurClumps` |
| `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_SilverVein.asset` | `resource.node.silver_vein` | Silver Vein | 2 Drill | 180-620 | 0.58 | `Data_SilverOre` |

Additional starter-relevant harvestable:

| Path | Stable ID | Data | Route Implication |
|---|---|---|---|
| `Assets/_Project/Data/World/HarvestableTemplate_TitaniumOutcrop.asset` | `harvestable.titanium_outcrop` | baseHealth 42, toolResistance 2.75, materialClass 3, loot `Data_TitaniumScrap` 2-4 weight 5, `Data_SilverOre` 1-2 weight 1, `Data_Copper` 1 weight 1 | Possible non-drill copper/titanium route if placed and wired. Static file search found the template asset but did not prove scene/prefab placement. |

## Tool Class and Starter Availability

Tool class mapping:

- `ResourceNodeTemplate.HarvestToolClass`: `Any = 0`, `Knife = 1`, `Drill = 2`, `Laser = 3`, `Salvage = 4`.
- `ResourceNode.ResolveRequiredToolCapabilityMask`: Knife -> Cut, Drill -> Drill, Laser -> Laser, Salvage -> Salvage, Any -> `uint.MaxValue`.
- `ToolCapabilityMasks`: Drill is `1u << 1`; PlasmaCut is `Cut | Burn | Laser`.
- `SeafloorDrillTool.GetCapabilityMask()` returns Drill and publishes `InteractionEffectType.Drill`.
- `LaserCutter.GetCapabilityMask()` returns PlasmaCut and publishes `InteractionEffectType.PlasmaCut`/`Boil`.

Starter loadout evidence:

- `PlayerToolManager.toolPrefabs` in `Player.prefab`: Scanner, Repair, Builder, LaserCutter.
- `PlayerToolManager.knownToolPrefabs`: Scanner, Repair, Builder, LaserCutter, Flashlight, Propulsion, SalvageSampler, BeaconDeployer, EnvAnalyzer, Knife, StunPistol, HarpoonLauncher.
- Source defaults: `grantAssignedToolItemsOnRuntimeStart = true`, `runtimeStartToolGrantBudget = 4`.
- `ToolLoadoutProvisioner` is not a production grant route in current prefab: `provisionInventoryOnStart: 0`, `assignCoreLoadoutOnStart: 0`, `provisionConstructionMaterialsOnStart: 0`, `startupPreset: null`. It references `Item_Tool_SeafloorDrill.asset`, but that asset is missing.
- `ContentSanityValidator.cs` already encodes this as an error: CopperVein is Drill-gated and the SeafloorDrill item/prefab route must be authored or replaced by an explicit validated alternative. It explicitly forbids falling back to Knife/Any as a cheap fix.

## Oxygen, Reserve, Upgrade, Hose, Bottle, Base

Player survival:

- `Assets/_Project/Data/Survival/Standard_Suit_V1.asset`: maxOxygen 139.2400, oxygenConsumptionRate 0.0150, maxEnergy 200, energyConsumptionRate 0.8, maxIntegrity 100, safeDepth 50, pressureDamageRate 2, pressureScalePerMeter 0.02.
- `HectonSurvivalSystem.surfaceOxygenRefillRate`: 15 per second when surface contract says head is in air.
- `HectonSurvivalSystem.ResolveOxygenPressureScale`: ambient pressure `1 + depth * 0.1`, clamped 1-16.
- `HectonSurvivalSystem.ResolveOxygenMovementScale`: 1.0 to 1.55 by movement speed.
- `HectonSurvivalSystem` also applies stress, leak, carry mass, rebreather, and barotrauma multipliers.
- `MetabolicStateContract.HypoxiaAgonyDurationSeconds`: 4 seconds oxygen grace.

Reserve and upgrade:

- `Assets/_Project/Data/Lore/SuitUpgrades/SuitUpgrade_Oxygen_Tier1_AuxReservoir.asset`: `upgradeId: suit_oxygen_t1_aux_reservoir`, category Oxygen, tier 1, `deltaMaxOxygen: 4`, `deltaSafeDepth: 0`, `requirements: []`, `requiredBlueprintId: chen_m_blueprint`.
- `SuitUpgradeResolver.cs` maps `suit_oxygen_t1_aux_reservoir` and Oxygen tier 1 to `HighCapacityTank`; `SuitUpgradeManager.cs` also aliases `Item_Equip_OxygenRig_T1` and `Item_Equip_OxygenRig_T2` hashes to that bit.
- `Assets/_Project/Data/Items/Resources/Processed/Data_EmergencyO2Canister.asset`: stableId `Data_EmergencyO2Canister`, consumable, maxStack 4, weight 0.9, `oxygenRestore: 35`.
- `Recipe_EmergencyO2Canister.asset`: result 1 Emergency O2 Canister, ingredients `Data_MembraneTissue` x1, `Comp_FiberMesh` x1, `Data_ElectrolyteSalts` x1, craftTime 1.5, powerCost 5.
- `PlayerInventory.ConsumeOneItem` applies `runtimeDescriptor.OxygenRestore` through `HectonSurvivalSystem.RefillOxygen`.

Environmental oxygen support:

- `OxygenBubble.cs`: default `oxygenAmount = 15`; collection invokes oxygen restore.
- `OxygenPlant.cs`: requires assigned `oxygenBubblePrefab`; default `releaseInterval = 5`, `releaseVariation = 0.5`; uses ObjectPoolManager and skips release if pool unavailable.
- `HectonSurvivalSystem.TryApplyLocalizedOxygenPocket` can refill from AUP air-pocket sampling to at least a fraction of runtime max oxygen.

Hose/base/bottle data:

- No authored first-hour hose route was found in non-archive `Assets/_Project/Data` or `Data`.
- `Assets/_Project/Data/Survival/SurvivalDatabaseRuntime.txt` includes oxygen-related IDs: `Proc_OxygenPellet`, `Proc_EmergencyO2Canister`, `Comp_OxygenManifold`, `Comp_ScrubberBed`, `Item_Equip_OxygenRig_T1`, `Item_Equip_OxygenRig_T2`, `Build_Oxygen_ScrubberRack`, `Build_Oxygen_Tank`, `Wreck_OxygenLineCoil`, `Wreck_O2QuotaLedger`.
- `SubmarineAtmosphereSystem.cs` base-room defaults: room capacity 8, oxygen tank capacity 100, initial oxygen fraction 0.2095, low oxygen threshold 0.2, player O2 drain 0.5 percent/sec/player, atmosphere slow tick 1 sec, brownout oxygen supply threshold 0.40, brownout occupied-room drain 0.0008 units/sec.
- `Recipe_ModuleO2Recycler.asset` and `Item_StandardO2Tank.asset` were not present at the checked Unity asset paths. `Data/Economy/Time_To_First_Submarine.json` lists `Module_O2Recycler`, but its status is `economy.path.requires_literal_energy_rebalance`, so it is not first-hour proof.

## Softlock and Bottleneck Assessment

Hard static blocker if the intended first-hour copper route uses `ResourceNodeTemplate_CopperVein.asset`: the node requires Drill, the player does not start with Drill, and the Drill item/prefab route is missing.

Not proven hard blocker if the intended copper source is `HarvestableTemplate_TitaniumOutcrop.asset`: that data can yield copper without a serialized required tool class, and `HarvestableOutcrop` accepts interaction signals without capability gating. However, static search did not prove outcrop scene/prefab placement, first-hour marker routing, or that its weighted copper yield completes the copper quest in time.

Titanium has a parallel risk. The resource-node version `ResourceNodeTemplate_TitaniumScrap.asset` requires Salvage class, while starter quick slots do not grant SalvageSampler. The titanium outcrop template can yield titanium, but needs placement/wiring proof.

Oxygen is not a static hard blocker for a shallow 40 m route. The suit has enough paper reserve for a 40 m descent/return if the route is short, readable, and near-surface. It becomes a blocker if the authored copper node is deeper than 50 m, if route distance is long, if oxygen plant/bubble support is assumed but not placed, or if stress/carry/leak multipliers make the return fail.

Crafting has a data-authority risk. The Unity recipe says Copper Wire costs 1 copper; the economy bake says 2. PlayMode must prove the actual runtime crafting authority.

Save/load/respawn behavior is not statically proven. Respawn has a signal/vault route and fallback lifepod/mock bay defaults, but no authored penalty CSV was found. With no penalty rules, the job emits fallback inventory penalty, and `PlayerInventory` skips dropping tools/equipment but may drop resources. Copper/titanium/wire retention after death must be proven.

## Proposed Safe Fixes For Unity/Data Owner

1. If CopperVein is the first-hour copper source, author the missing SeafloorDrill route: create `Item_Tool_SeafloorDrill.asset`, create `Tool_SeafloorDrill_Held.prefab`, wire `_toolData`, add it to the intended unlock/loadout path, and validate that `PlayerToolManager` can grant/equip/use it before CopperVein is required.

2. If Drill should not be first-hour, do not downgrade CopperVein to Any/Knife. Leave CopperVein as Drill-gated and route first-hour copper through an authored shallow outcrop or pickup with explicit placement, marker, tutorial text, and proof.

3. Constrain first-hour copper to 40-50 m or author a specific shallow node. Do not rely on the full 40-420 m procedural range for the first copper quest.

4. Resolve recipe authority. Either update `Recipe_CopperWire.asset` to match the economy bake or regenerate the bake from the Unity recipe. Runtime must have one owner.

5. Resolve the titanium branch. Either grant/unlock SalvageSampler before `ResourceNodeTemplate_TitaniumScrap.asset` is required, or route titanium through a proven placed outcrop/pickup.

6. Do not use `ToolLoadoutProvisioner` as hidden production proof. It is disabled on the player prefab and development/editor-gated.

7. If oxygen support is intended, place and prefab-wire `OxygenPlant`/`OxygenBubble` in the first-hour route, or author an explicit emergency canister pickup/recipe route. A data row without scene placement is not survival proof.

8. Add or validate respawn penalty rules for first-hour critical resources and tools. Copper, copper wire, scanner, starter tools, and oxygen reserve must survive or have deterministic recovery.

## Required PlayMode Proof Gates

Required gates before accepting the first hour:

- `FH_RESOURCE_COPPER_REACH`: From lifepod/start, reach the intended copper source at authored depth without using debug tools.
- `FH_RESOURCE_COPPER_HARVEST`: Harvest/collect `Data_Copper`; verify capability gate, item hash, quantity, and quest signal.
- `FH_RESOURCE_RETURN_ALIVE`: Return to surface/lifepod/refill point alive with O2 margin. Record peak depth and lowest oxygen normalized.
- `FH_CRAFT_COPPERWIRE`: Craft `Comp_CopperWire`; verify actual runtime ingredient count and source of truth.
- `FH_TITANIUM_BRANCH`: If titanium quest remains active, collect `Data_TitaniumScrap` through the real starter route and craft `Item_Tool_Scanner`.
- `FH_OXYGEN_SUPPORT`: If oxygen plant/bubble/canister support is claimed, collect/use it and verify `HectonSurvivalSystem.Oxygen` changes by the authored amount.
- `FH_SAVE_LOAD_RESOURCE`: Save after collecting copper/titanium, load, and verify inventory, quest state, node depletion, and route markers.
- `FH_RESPAWN_RESOURCE`: Die after collecting first-hour resource, respawn, verify tool retention, resource loss/recovery, quest state, and death loot cache behavior.
- `FH_DEPTH_REJECTION`: Attempt a copper node deeper than safe starter depth and verify advisory/death/prevention behavior does not softlock the quest.
- `FH_BLACK_BOX`: On death/NaN/failure, verify last-300-frame survival/respawn telemetry is available as deterministic artifact.

## Quality Scaling Consequences

Resource truth must not change by quality tier. Tool class, item IDs, recipe costs, quest completion IDs, save identity, oxygen drain truth, death truth, and respawn authority are fixed.

- Low: keep the same resource nodes, placement, tool gates, O2 formulas, and quest state. Reduce particles, shader samples, bubble VFX density, outcrop debris density, and fabricator presentation only. Route markers and readable silhouettes stay intact.
- Middle: add fuller harvest feedback, clearer O2 warning presentation, and moderate debris/bubble/audio cadence. Resource and oxygen truth unchanged.
- High: richer rock material breakup, scanner hints, water caustics, oxygen bubble shimmer, and diegetic HUD feedback. No extra resources, no easier gates, no altered consumption.
- Ultra: visual overkill only: more debris variation, richer fabricator sparks, higher-fidelity route lighting, better water/foam/refraction, and expanded telemetry visualization. No gameplay truth changes.

## Static Conclusion

The reported CopperVein blocker is real for the CopperVein route: Drill is required, the starter player does not receive Drill, and the Drill asset/prefab route is absent. Oxygen does not statically block a shallow 40 m trip, but the current copper template spans unsafe depths and still requires runtime route proof. A possible outcrop-based copper alternative exists, but it is unproven without scene placement and PlayMode harvest evidence.

No Unity run, no build, no tests, and no Assets edits were performed.
