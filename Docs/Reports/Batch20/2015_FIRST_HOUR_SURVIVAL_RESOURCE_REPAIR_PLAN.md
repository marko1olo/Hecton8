# 2015 First-Hour Survival Resource Repair Plan

Date: 2026-06-04
Agent: 2015
Evidence class: STATIC SOURCE / STATIC DATA ONLY. No Unity run. No build. No scene, asset, material, texture, or C# implementation edits.

## Authority Read

- `AGENTS.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `gameplay.md`
- `survival.md`
- `inventory.md`
- `construction.md`
- `tools.md`
- `player.md`
- `world.md`
- `data.md`
- Prior report: `Docs/Reports/Batch20/FIRST_HOUR_RESOURCE_TOOL_OXYGEN_REACHABILITY_20260604.md`

Relevant mandates:

- `.agents-skills/CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt`
- `.agents-skills/CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `.agents-skills/DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

`Docs/Actual Domains of Project.txt` was not present. Narrow inferred domain: first-hour survival/resource/tool/crafting/death route repair planning.

## Static Conclusion

The first-hour route is not implementation-ready. The primary blocker is not oxygen math. The primary blocker is route authority split plus missing tool/resource proof.

The current data exposes two incompatible first-hour spines:

- Copper/copper-wire spine: `FirstHourDirector` uses `Data_Copper` and `Comp_CopperWire`.
- Titanium/scanner spine: quest data uses `Data_TitaniumScrap` and `Item_Tool_Scanner`.

At the same time, the CopperVein route is Drill-gated, the starter player does not receive a Drill, and the SeafloorDrill item/prefab paths are missing. Titanium resource-node data is Salvage-gated, while the starter quick slots do not grant the SalvageSampler. A titanium outcrop can yield both titanium and copper, but static data does not prove scene placement, first-hour marker routing, harvest compatibility, oxygen return, or quest completion.

## Desired Player-Facing Behavior

The opening route should remain semi-open, bright, scenic, and physically legible in the photic zone. The player exits the safe anchor into a beautiful shallow route with uneasy threat nearby, not a dark corridor and not a spreadsheet pickup loop.

Accepted first-hour behavior:

1. Player exits lifepod/safe anchor into readable 0-50 m shallow route.
2. Oxygen is the first active constraint; the route teaches turn-back timing without hiding the world.
3. Starter resource pocket contains enough deterministic titanium/copper pathing to craft the first meaningful tool or component.
4. First craft changes route capability: scanner, beacon/deployable return aid, pressure seal, repair route, or explicit oxygen safety margin.
5. Death respawns at base/safe anchor, preserves core tools, drops or caches ordinary carried resources only through deterministic rules.
6. Route leaves evidence: harvested outcrop scar, opened cache, repaired machine, black-box/quest state, or changed world marker.

Rejected behavior:

- Making CopperVein `Any` or Knife-gated as a cheap fix.
- Leaving copper in the 40-420 m procedural range without authored shallow proof.
- Treating starter zone profile text as scene placement proof.
- Crafting scanner from titanium while a separate director still pushes copper wire as the first gate.
- Dropping core starter tools or critical quest items on death without recovery proof.
- Hiding first-hour weakness behind darkness, fog, or empty water.

## Repair Plan

### 1. Pick One First-Hour Truth Owner

Owner: Gameplay/Narrative integration owner.

Blocker:

- `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs:714-740` defines copper and copper wire first.
- `Data/Narrative/Quest_Graph.json:107-142` defines first-hour titanium then scanner.
- `Data/Narrative/Quest_Graph.json:53-65` still contains separate `quest_copper_sample`.

Repair:

- Decide whether the first product-facing spine is:
  - A: `Data_TitaniumScrap -> Item_Tool_Scanner`, with copper as optional/parallel electronics resource; or
  - B: `Data_Copper -> Comp_CopperWire`, with scanner craft moved later or explicitly dependent on copper wire.
- Update the losing route to a secondary/optional objective. Do not leave both as first mandatory route owners.

Required proof:

- Static: one route table showing quest id, trigger id, completion id, critical item, respawn event, route owner.
- PlayMode: fresh first-hour run advances only the chosen mandatory spine.

Risk if wrong:

- Quest UI and director guidance can send the player to different resources. Saves may mark one route complete while the other remains blocking.

### 2. Fix Copper Access Without Cheapening CopperVein

Owner: Tools + Scavenging + World placement owner.

Blocker:

- `ResourceNodeTemplate_CopperVein.asset:19-24`: `requiredToolClass: 2`, depth `40-420`.
- `ResourceNodeTemplate.cs:32-38`: Drill is enum value `2`.
- `ResourceNode.cs:504-522`: Drill maps to `ToolCapabilityMasks.Drill`.
- `Player.prefab:1511-1515`: assigned quick slots are Scanner, Repair, Builder, LaserCutter by serialized prefab refs.
- `Player.prefab:1516-1528`: known tools include SalvageSampler, but not assigned.
- SeafloorDrill paths are missing:
  - `Assets/_Project/Data/Items/Tools/Item_Tool_SeafloorDrill.asset`
  - `Assets/_Project/Prefabs/Tools/Held/Tool_SeafloorDrill_Held.prefab`
- `ContentSanityValidator.cs:1098-1135` already rejects non-Drill CopperVein and reports missing first-hour drill route.

Repair options:

- Preferred if CopperVein is mandatory: author a real SeafloorDrill item and held prefab, wire `_toolData`, add it to the intended unlock/loadout path before CopperVein is required, and keep CopperVein Drill-gated.
- Preferred if Drill is not first-hour: keep CopperVein Drill-gated and route first-hour copper through a placed shallow outcrop/cache/pickup with explicit authored placement, quest marker, depletion/scar, and proof.

Required proof:

- Static: item asset path, held prefab path, player loadout/unlock source, capability mask, node template.
- PlayMode: harvest `Data_Copper` from start with no debug tools, return alive, craft the intended output.

Risk if wrong:

- Hard softlock on copper. The player sees a copper objective but cannot extract the resource with available tools.

### 3. Fix Titanium Access Before Scanner Quest

Owner: Scavenging + World placement + Quest owner.

Blocker:

- `Quest_Graph.json:107-142` makes `Data_TitaniumScrap` the first-hour prerequisite for scanner craft.
- `ResourceNodeTemplate_TitaniumScrap.asset:19-24`: required tool class `4` Salvage, depth `0-220`.
- `Player.prefab:1511-1515`: SalvageSampler is not in assigned quick slots.
- `HarvestableTemplate_TitaniumOutcrop.asset:20-32` can yield titanium 2-4, silver 1-2, and copper 1, but static data does not prove placement or harvest route.

Repair:

- If titanium/scanner is the chosen spine, route titanium through a shallow placed outcrop or loose resource pocket that is reachable with starter verbs.
- If SalvageSampler is intended as the first extraction tool, explicitly grant/unlock it before the titanium resource-node objective.
- If the outcrop is the intended bypass, add proof that `HarvestableOutcrop` interaction can be completed by the actual starter tool and that the outcrop is placed in the first-hour route.

Required proof:

- Static: source of `Data_TitaniumScrap`, placement rule or scene anchor, tool/capability route, quest marker route.
- PlayMode: collect titanium after `first_hour_exit_lifepod`, craft `Item_Tool_Scanner`, verify quest state.

Risk if wrong:

- Scanner craft objective becomes a dead branch or depends on random weighted copper/titanium results with no deterministic recovery.

### 4. Resolve Recipe Authority Conflict

Owner: Crafting/Data owner.

Blocker:

- `Assets/_Project/Data/Crafting/Recipes/Recipe_CopperWire.asset:26-32` requires 1 `Data_Copper`.
- `Data/Economy/Crafting_Costs.json` records `Recipe_CopperWire` requiring 2 `Data_Copper`.
- `Recipe_Scanner.asset:27-35` requires SensorPackage and CopperWire, while quest graph says scanner completion follows titanium collection.

Repair:

- Declare runtime recipe authority: Unity `RecipeData` assets or baked `Data/Economy` payload.
- Regenerate the non-authoritative mirror from the owner.
- Align scanner recipe with chosen route: titanium must either feed a required scanner ingredient or stop pretending to be the scanner prerequisite.

Required proof:

- Static: recipe owner declaration and matching ingredient counts across active runtime and generated mirrors.
- PlayMode: fabricator consumes the expected ingredients and emits the expected item.

Risk if wrong:

- A player can collect the correct quest resource but fail fabrication, or tests pass against the wrong data source.

### 5. Prove Oxygen Return And Recovery Support

Owner: Survival + World route owner.

Static facts:

- `Standard_Suit_V1.asset:15-22`: max oxygen `139.2400`, consumption `0.0150`, safe depth `50`, pressure scale per meter `0.02`.
- `HectonSurvivalSystem.cs:236`: surface oxygen refill rate `15`.
- `HectonSurvivalSystem.cs:1946-1954`: oxygen pressure scaling exists.
- `Data_EmergencyO2Canister.asset:42`: `oxygenRestore: 35`.
- `PlayerInventory.cs:1704-1708`: consumable oxygen restore routes to `HectonSurvivalSystem.RefillOxygen`.
- `ZonePlan_ZoneProfile_Resources_Starter.asset:50-55`: safe pocket plan exists as authoring intent, not placement proof.

Repair:

- Keep first mandatory resource route inside or near 0-50 m unless a visible oxygen support route is authored.
- Place recovery oxygen as actual route content if the route exceeds shallow safe planning: oxygen plant/bubble, emergency canister pickup, or safe pocket.
- Route O2 warnings through diegetic instrument feedback; do not rely on UI bar only.

Required proof:

- PlayMode route: start full O2, exit, collect required resource, return to refill/safe anchor alive.
- Capture peak depth, minimum oxygen normalized, route duration, refill source, and failure behavior if player overextends.

Risk if wrong:

- The route can look statically possible but kill players during normal exploration or force a linear speedrun through the scenic opening.

### 6. Make Death/Respawn Resource Rules Explicit

Owner: Respawn + Inventory owner.

Static facts:

- `Docs/ARCHITECTURE/SHINOBU_329_PLAYER_RESPAWN_RECONCILIATION_ROUTE_CARD.md:14-30` defines respawn authority, inventory penalty command, death loot cache signal, and dropped count telemetry.
- `PlayerInventory_SoaQuery.cs:265-315` removes one item and emits `InventoryDeathLootCacheSignal`.
- `PlayerInventory.cs:2961-2972` publishes `InventoryRespawnPenaltyResultSignal`.
- `PlayerInventory.cs:2980-3009` reads penalty rules from DataVault only when provided.
- `PlayerInventory.cs:3035-3045` retains equipment and only retains tools when current tool hash matches and rule says retain.
- `Quest_Graph.json:118` and `138` list first-hour respawn event ids for titanium/scanner, but `quest_copper_sample` has no respawn event.

Repair:

- Add first-hour penalty rules for chosen critical items:
  - Preserve core tools.
  - Preserve crafted scanner if it is the route owner or create deterministic recovery marker.
  - Ordinary loose copper/titanium can drop/cache, but must be recoverable and must not deadlock quest completion.
- Add respawn event policy to copper route if copper remains first-hour critical.

Required proof:

- PlayMode: die with starter tools, copper/titanium, copper wire, scanner, and oxygen support item in inventory; verify respawn, dropped count, death loot cache/recovery, quest state, and tool retention.

Risk if wrong:

- Death after successful collection can delete the only critical resource/tool or preserve the wrong item, causing silent first-hour deadlocks.

### 7. Preserve The Product-Facing Scenic Route

Owner: World/Visual route owner.

Static facts:

- `VISION_LOCKS.md` and `TASTE.md` require bright readable photic shallows and a semi-open first exit.
- `ZonePlan_ZoneProfile_Resources_Starter.asset:36-55` describes resource pockets, node clusters, and safe pockets, but not runtime proof.

Repair:

- Author the resource route as a visible shallow loop, not a random pocket:
  - route anchor;
  - resource pocket;
  - safe pocket or oxygen support;
  - uneasy threat silhouette/sound;
  - return landmark.
- Keep darkness for caves/interiors/storm events, not baseline shallow-water concealment.

Required proof:

- Normal and compact screenshots from start, route midpoint, resource node, return landmark, and safe anchor.
- PlayMode route proof and profiler/Frame Debugger proof belong to implementing owner, not this static plan.

Risk if wrong:

- The repair may technically complete crafting while failing the product vision. That is still rejected.

## Quality Scaling Consequences

Resource, quest, oxygen, recipe, death, and save truth must not vary by quality tier.

- Low: same items, recipes, node gates, O2 formulas, quest ids, death rules, and route placement. Reduce VFX density, outcrop debris count, shader samples, and noncritical animation. Keep silhouettes, route landmarks, O2 warnings, and resource affordances readable.
- Middle: add fuller harvest feedback, clearer fabricator/audio cadence, moderate bubble/silt/scan effects, and richer but still bounded resource proxy models.
- High: add better rock/coral material breakup, water caustic hints, instrument response, oxygen bubble shimmer, and route lighting. No easier resources or changed consumption.
- Ultra: visual overkill only: richer wet material response, dense but controlled scenic biota, higher fidelity tool/fabricator effects, stronger telemetry visualization, and longer landmark richness. No new gameplay truth.

## Required Implementation Proof Gates

- `FH_ROUTE_AUTHORITY_SINGLE_OWNER`: one first-hour spine active; no copper/titanium director split.
- `FH_STARTER_LOADOUT`: starter tools/unlocks match the first mandatory resource gate.
- `FH_RESOURCE_TITANIUM_REACH`: if titanium spine remains, collect `Data_TitaniumScrap` from authored route.
- `FH_RESOURCE_COPPER_REACH`: if copper spine remains, collect `Data_Copper` from authored route.
- `FH_CRAFT_FIRST_TOOL_OR_COMPONENT`: craft scanner or copper wire using the actual runtime recipe owner.
- `FH_OXYGEN_RETURN_ALIVE`: return alive with recorded peak depth and lowest O2.
- `FH_SCENIC_ROUTE_CAPTURE`: compact and normal screenshots prove bright, readable, scenic photic route with unease.
- `FH_RESPAWN_RESOURCE`: die after collection/craft, respawn, verify tool retention, drop/cache behavior, quest state.
- `FH_SAVE_LOAD_ROUTE`: save/load preserves inventory, quest state, depleted resource scar, and route markers.
- `FH_BLACK_BOX`: survival/respawn failure exports or exposes last-300-frame telemetry.

