# FIRST20 Route Implementation Order - Agent 2024

Status: STATIC SPEC COMPLETE / UNITY PROOF NOT RUN
Scope: no Unity, no Play Mode, no build, no Assets edits.
Workspace: `C:\hades\Hecton8`
Agent ID: `2024`

## Authority Files Read

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`
- `gameplay.md`
- `survival.md`
- `tools.md`
- `sonar.md`
- `construction.md`
- `inventory.md`
- `persistence.md`
- `quality.md`
- `Docs/ARCHITECTURE/FIRST_20_MINUTES_ROUTE_BRIEF.md`
- `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`
- Batch20 reports under `Docs/Reports/Batch20/`

Relevant mandates loaded:

- `PROG_Quest_State_Graph_Logic.txt`
- `CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt`
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

## Static Verdict

The next implementation can be assigned, but only in the order below. The current route is not a safe one-shot patch because three authority conflicts exist.

1. Scanner conflict: `Player.prefab` assigns `Tool_Scanner_Held` in `toolPrefabs`, while `Quest_FirstHour_CraftScanner.asset` treats `Item_Tool_Scanner` as a crafted first-hour completion.
2. Recipe authority conflict: `RecipeData` assets and `Data/Economy/Crafting_Costs.csv` disagree on scanner and copper wire costs. Static code shows Fabricator paths reading `RecipeData`, while DataMonolith audit files claim economy binary authority. Runtime truth is not proven here.
3. Copper source conflict: `ResourceNodeTemplate_CopperVein.asset` is Drill-gated and must remain Drill-gated. No starter `Item_Tool_SeafloorDrill.asset` or held drill prefab exists. Early copper must come from a separate shallow non-vein source. `WorldRuntimeBootstrapAuthoring.cs` currently references `resource.node.copper`, but the verified copper node stable ID is `resource.node.copper_vein`.

## Single Truth Route

Use this route as the first implementation target:

```text
exit lifepod
-> collect Data_TitaniumScrap from a deterministic shallow starter source
-> collect Data_Copper from a deterministic shallow non-vein source
-> craft Comp_CopperWire
-> acquire or craft Comp_SensorPackage through an explicit first-route source
-> craft or activate Item_Tool_Scanner
-> use scanner as the first route-changing tool
-> survive oxygen pressure through readable return routes and optional oxygen support
-> death/drop/base respawn cannot softlock the above
-> save/load preserves position, inventory, quest state, depleted/opened/scanned state, death record, and route-critical drops
```

`CopperVein` remains Drill-gated. Do not change `ResourceNodeTemplate_CopperVein.asset.requiredToolClass` from Drill. The first copper source must be a separate loose pickup, outcrop/cache, or explicitly authored shallow non-vein node owned by the resource route.

Quest truth should be `QuestData` assets first, with `Data/Narrative/Quest_Graph.json` regenerated as a mirror after the asset route is patched. The JSON file states runtime authority remains `QuestData` unless leadership promotes JSON as source of truth.

## Implementation Order

### 01 - Resolve Scanner Starter Truth

Owner files:

- `Assets/_Project/Prefabs/Player.prefab`
- `Assets/_Project/Scripts/ToolLoadoutProvisioner.cs`
- `Assets/_Project/Data/Items/Tools/Item_Tool_Scanner.asset`
- `Assets/_Project/Prefabs/Tools/Held/Tool_Scanner_Held.prefab`
- `Assets/_Project/Data/Lore/Quests/Quest_FirstHour_CraftScanner.asset`

Required decision:

- If scanner is the first route-changing craft, remove it from functional starter quick slots or gate it as inactive/broken until the route completes.
- If scanner remains a real starter tool, do not make scanner crafting the first-hour spine; replace the route endpoint with a different capability change.

Preferred order for this objective: scanner is not a functional starter tool. It becomes the first route-changing craft or activation result.

Rollback boundary: restore starter quick-slot assignment only. Do not alter scanner item identity or scan system data.

### 02 - Resolve Crafting Runtime Authority

Owner files:

- `Assets/_Project/Data/Crafting/Recipes/Recipe_CopperWire.asset`
- `Assets/_Project/Data/Crafting/Recipes/Recipe_Scanner.asset`
- `Assets/_Project/Data/Crafting/Recipes/Recipe_SensorPackage.asset`
- `Data/Economy/Crafting_Costs.csv`
- `Data/Economy/Crafting_Costs.manifest.json`
- `Assets/_Project/Scripts/Fabricator.FastFail.cs`
- `Assets/_Project/Scripts/HectonFabricatorUI.cs`
- `Assets/_Project/Scripts/Economy/Shinobu19EconomyLedger.cs`

Required decision:

- Declare whether first-hour runtime crafting reads `RecipeData`, DataMonolith economy rows, or a synced import product.
- Patch both surfaces to match once authority is declared.

Preferred first-route cost model:

- `Comp_CopperWire`: copper cost must be identical across active recipe surfaces.
- `Item_Tool_Scanner`: keep `Comp_CopperWire x1 + Comp_SensorPackage x1` only if `Comp_SensorPackage` has an explicit reachable first-route source. Otherwise the scanner route remains blocked.
- `scan.expedition_contact` requirement on `Recipe_Scanner.asset` blocks the route unless that scan unlock is also placed and proven before scanner crafting. Remove, defer, or explicitly source that scan entry before requiring the recipe.

Rollback boundary: revert recipe/economy rows only. Do not edit Fabricator runtime unless the authority decision requires it.

### 03 - Author Deterministic Starter Resource Sources

Owner files and candidate directories:

- `Assets/_Project/Data/Scavenging/ResourceNodes/`
- `Assets/_Project/Data/World/HarvestableTemplate_TitaniumOutcrop.asset`
- `Assets/_Project/Scripts/Scavenging/ResourceNodeTemplate.cs`
- `Assets/_Project/Scripts/ResourceNode.cs`
- `Assets/_Project/Scripts/Gameplay/HarvestableOutcrop.cs`
- `Assets/_Project/Scripts/Scavenging/ScavengingLootOracle.cs`
- `Assets/_Project/Scripts/Editor/WorldRuntimeBootstrapAuthoring.cs`
- `Assets/_Project/Data/World/ZoneProfiles/ZoneProfile_Resources_Starter.asset`
- `Assets/_Project/Data/World/ZonePlans/ZonePlan_ZoneProfile_Resources_Starter.asset`
- `Assets/_Project/Data/Biomes/ResourcePlans/ResourcePlan_LittoralKarst.asset`

Required targets:

- Titanium: `Data_TitaniumScrap` must be obtainable before scanner crafting without requiring an unavailable tool. Existing `ResourceNodeTemplate_TitaniumScrap.asset` is Salvage-gated. Existing `HarvestableTemplate_TitaniumOutcrop.asset` includes titanium and copper loot but does not by itself prove guaranteed route yield or placement.
- Copper: `Data_Copper` must be obtainable from a shallow non-vein source. The `resource.node.copper` socket in `WorldRuntimeBootstrapAuthoring.cs` is not a verified template ID. Fix the source binding or replace it with an explicit pickup/outcrop/cache route.
- Sensor package: `Comp_SensorPackage` must either be a reachable cache/quest reward or have all ingredients reachable in the same first route. Current `Recipe_SensorPackage.asset` pulls circuit board, precision lens, and silver; that is not yet proven as first-20 reachable.

Rollback boundary: remove only the new/changed starter resource source bindings. Do not change CopperVein.

### 04 - Align Starter Tool Interaction

Owner files:

- `Assets/_Project/Prefabs/Player.prefab`
- `Assets/_Project/Scripts/ToolLoadoutProvisioner.cs`
- `Assets/_Project/Data/Items/Tools/Item_Tool_SalvageSampler.asset`
- `Assets/_Project/Prefabs/Tools/Held/Tool_SalvageSampler_Held.prefab`
- `Assets/_Project/Scripts/SalvageSamplerTool.cs`
- `Assets/_Project/Scripts/SeafloorDrillTool.cs`

Required target:

- If the starter titanium source uses `ResourceNodeTemplate_TitaniumScrap.asset`, the route needs `SalvageSampler` before that node because the template is Salvage-gated.
- If the route does not grant `SalvageSampler`, use loose pickups or outcrops that can be collected with the approved starter interaction.
- Do not grant `SeafloorDrill`; drill item and held prefab were not found, and CopperVein must stay Drill-gated.
- Treat `ToolLoadoutProvisioner` construction-material provisioning as dev/test evidence only until runtime route ownership proves it runs in the shipped first route.

Rollback boundary: quick-slot/loadout changes only.

### 05 - Patch Quest And FirstHourDirector Spine

Owner files:

- `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs`
- `Assets/_Project/Data/Lore/Quests/Quest_CopperSample.asset`
- `Assets/_Project/Data/Lore/Quests/Quest_FirstHour_CollectTitanium.asset`
- `Assets/_Project/Data/Lore/Quests/Quest_FirstHour_CraftScanner.asset`
- `Assets/_Project/Data/Narrative/Quest_Graph.json`
- `Assets/_Project/Scripts/Quest/QuestData.cs`
- `Assets/_Project/Scripts/Quest/QuestManager.cs`
- `Assets/_Project/Scripts/Quest/QuestStateManager.cs`

Required target:

- Make `quest_first_hour_collect_titanium` the first real resource step.
- Keep `quest_copper_sample` as material/electronics substep or secondary resource step, not the sole route identity.
- Make scanner craft/activation the first route-changing craft if Order 01 selects the scanner route.
- Preserve critical item and respawn event fields for titanium/scanner. Add equivalent softlock protection for copper and sensor package if they become route-critical.
- Regenerate `Quest_Graph.json` only after `QuestData` assets are patched.

Rollback boundary: quest assets plus FirstHourDirector route constants. Do not mutate unrelated quest DAG.

### 06 - Author Oxygen Fairness, Not Safety Padding

Verified mechanics:

- `Standard_Suit_V1.asset`: max oxygen, oxygen consumption, safe depth, pressure scale.
- `HectonSurvivalSystem.cs`: surface refill, underwater drain, oxygen pressure scaling, oxygen grace, death handoff, save/load oxygen fields.
- `Data_EmergencyO2Canister.asset`: consumable oxygen restore.
- `Recipe_EmergencyO2Canister.asset`: existing recipe surface.
- `OxygenPlant.cs` and `OxygenBubble.cs`: candidate environmental oxygen support.
- `ModuleLifeSupportComponent.cs`: base/internal breathable reserve route.

Required target:

- The route must stay survivable if careful and lethal if careless.
- Surface return, air pocket, plant/bubble, emergency canister, or base life support can support readability. They must not remove oxygen death.
- No verified gameplay oxygen-hose owner exists. Suit visual hose meshes are art only; do not claim hose gameplay until a real owner file exists.

Rollback boundary: oxygen support placements/recipes only. Do not globally change suit oxygen truth unless the survival owner approves.

### 07 - Death, Drop, Base Respawn, And Softlock Guard

Owner files:

- `Docs/ARCHITECTURE/SHINOBU_329_PLAYER_RESPAWN_RECONCILIATION_ROUTE_CARD.md`
- `Assets/_Project/Scripts/Gameplay/PlayerDeathReconciliationBridge.cs`
- `Assets/_Project/Scripts/Physiology/ShinobuRespawnReconciliationRuntime.cs`
- `Assets/_Project/Scripts/PlayerInventory_SoaQuery.cs`
- `Assets/_Project/Scripts/PlayerInventory.cs`
- `Assets/_Project/Scripts/Gameplay/Loot/LootMagnetSystem.cs`
- `Assets/_Project/Scripts/Construction/ModuleLifeSupportComponent.cs`

Required target:

- Death from oxygen or avoidable threat can happen.
- Respawn target must be a verified safe anchor/base/medical bay for the first route.
- Death penalty can drop non-critical resources, but must not softlock scanner route. Route-critical resources either retain, respawn through quest critical item events, or recover through death cache.
- Prove `InventoryDeathLootCacheSignal` to data-only cache and recovery path later in PlayMode.

Rollback boundary: respawn tuning/penalty rules and first-route critical item flags. Do not add scene reload as death authority.

### 08 - Save/Load Proof Targets

Owner files:

- `Assets/_Project/Scripts/SaveData.cs`
- `Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs`
- `Assets/_Project/Scripts/SaveBinaryStorage.cs`
- `Assets/_Project/Scripts/Quest/QuestManager.cs`
- `Assets/_Project/Scripts/Quest/QuestStateManager.cs`
- `Assets/_Project/Scripts/Quest/QuestRuntimeTypes.cs`
- `Assets/_Project/Scripts/PlayerInventory.cs`
- `Assets/_Project/Scripts/SaveSystemRuntimeSmokeTester.cs`
- `Assets/_Project/Scripts/SaveRecoverySmokeTester.cs`
- `Assets/_Project/Scripts/SavePersistenceOmegaSmokeTester.cs`

Required target:

- Route save must preserve inventory cells/state, oxygen/death record, quest active/completed or packed state, depleted/opened resource state, scanner state, player position, and base/respawn-relevant state.
- Existing smoke testers cover save system integrity and recovery, not the complete first-20 gameplay route. Add or configure a route-specific proof only after Orders 01-07 are implemented.

Rollback boundary: route proof harness only. Do not change binary layout unless a save owner signs the ABI change.

## Proof Ladder Summary

Use `2024_first20_acceptance_proof_ladder.csv` for gate-level assignment. The ladder starts with static path checks and ends with Unity route proof. Agent 2024 did not run Unity, Play Mode, build, profiler, import, or any test.

## What Not To Touch

- Do not change `ResourceNodeTemplate_CopperVein.asset` to `Any`, `Knife`, `Scanner`, or `Salvage`.
- Do not grant SeafloorDrill as a workaround.
- Do not use `ToolLoadoutProvisioner` dev grants as acceptance.
- Do not patch only `Quest_Graph.json` while leaving `QuestData` assets stale.
- Do not patch only `RecipeData` or only `Crafting_Costs.csv` after authority is declared.
- Do not make the first route dark or hide weak surface/water art behind noir lighting.
- Do not remove oxygen death or aggressive-contact death.
- Do not claim save/load proof from serializer presence alone.
- Do not touch unrelated Assets, scenes, prefabs, materials, packages, project settings, or builds in this planning task.

## Scalability Consequences

- Low: same route truth, same item IDs, fewer optional oxygen bubbles, fewer ambient scans, lower spawn/set-dressing density. No softlock and no gameplay identity change.
- Middle: full starter route density, readable hazards, deterministic resources, standard scanner route.
- High: richer scan feedback, stronger visual signposting, more route set dressing, same authoritative data.
- Ultra: visual overkill in water, biota, route landmarks, scan presentation, and proof captures only. No separate ultra-only route truth.

## Assignment Readiness

Implementation can be assigned next as ordered work. It cannot be assigned as a single broad "fix first hour" task. The first implementer must take Order 01 and Order 02 before resource/quest patching, because scanner/loadout and recipe authority decide which later edits are valid.
