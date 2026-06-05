# 2020 First-20 Gameplay Repair Candidate

Date: 2026-06-04
Agent: 2020
Evidence class: STATIC VERIFIED. No Unity run. No build. No implementation or asset edits.
Scope: docs-only candidate patch/spec for first 20 minutes.

## Authority Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `gameplay.md`
- `survival.md`
- `inventory.md`
- `persistence.md`
- `world.md`
- `tools.md`
- `quality.md`
- `Docs/Reports/Batch20/2015_FIRST_HOUR_SURVIVAL_RESOURCE_REPAIR_PLAN.md`
- `Docs/Reports/Batch20/2015_ROUTE_BLOCKERS.csv`
- `Docs/Reports/Batch20/2015_PROOF_MATRIX.csv`

Mandates loaded:

- `.agents-skills/PROG_Quest_State_Graph_Logic.txt`
- `.agents-skills/CORE_Abyss_Survival_Systems_O2_Pressure_Logic.txt`
- `.agents-skills/DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `.agents-skills/DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `.agents-skills/CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`

`Docs/Actual Domains of Project.txt` is absent. Narrow domain inferred: first-20 gameplay route, survival/resource/tool/crafting/death proof planning.

## Static Findings

The current first-hour route is split and not implementation-ready.

- `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs:715-728` still defaults the first resource/craft spine to `quest_copper_sample`, `Data_Copper`, and `Comp_CopperWire`.
- `Data/Narrative/Quest_Graph.json:107-142` defines a separate first-hour spine: `Data_TitaniumScrap` then `Item_Tool_Scanner`.
- `Data/Narrative/Quest_Graph.json:53-65` leaves `quest_copper_sample` as a first-hour quest with no `respawnEventId`.
- `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_CopperVein.asset:19-24` is Drill-gated and spans 40-420 m.
- `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_TitaniumScrap.asset:19-24` is Salvage-gated and spans 0-220 m.
- `Assets/_Project/Prefabs/Player.prefab:1511-1515` assigns four starter tool prefabs, while extra tools live only in `knownToolPrefabs`.
- `Assets/_Project/Data/World/HarvestableTemplate_TitaniumOutcrop.asset:20-32` can yield titanium, silver, and copper, but static source does not prove first-route placement or starter-tool compatibility.
- `Assets/_Project/Data/Crafting/Recipes/Recipe_CopperWire.asset:26-32` requires 1 `Data_Copper`; `Data/Economy/Crafting_Costs.json` and `Data/Economy/Recipes.json` require 2.
- `Assets/_Project/Data/Crafting/Recipes/Recipe_Scanner.asset:27-35` requires `Comp_SensorPackage` and `Comp_CopperWire`, so the titanium quest does not match scanner fabrication truth.
- `Docs/ARCHITECTURE/SHINOBU_329_PLAYER_RESPAWN_RECONCILIATION_ROUTE_CARD.md:14-30` defines the respawn/inventory penalty authority route, but first-hour critical item policy remains unproven.

## Candidate Decision

Recommended first-20 truth owner:

`quest_first_hour_collect_titanium -> Recipe_CopperWire -> Recipe_Scanner -> Item_Tool_Scanner`

This is not a pure titanium scanner route. It is a scenic shallow salvage loop where titanium proves tool/extraction contact, copper wire proves fabrication/material economy, and scanner is the first route-changing craft.

Reasons:

- Scanner is a real first-20 decision upgrade: route reading, evidence finding, return planning, and threat confidence. Copper wire alone is not product-facing enough.
- Copper remains meaningful through `Comp_CopperWire`, but `CopperVein` stays Drill-gated. First-20 copper must come from a shallow outcrop/cache/debris source, not from cheapening CopperVein.
- Titanium stays first visible salvage proof, but the route cannot depend on SalvageSampler unless the starter loadout/unlock explicitly grants it.
- The route can satisfy the vision lock: semi-open, bright, scenic, uneasy, oxygen-pressured, and not a spreadsheet collection loop.

Rejected alternatives:

- Making `CopperVein` tool class `Any`, `Knife`, or `Scanner`. This breaks tool truth and removes the reason for a later Drill route.
- Keeping both `quest_copper_sample` and `quest_first_hour_collect_titanium` as mandatory first spines. That preserves the current split.
- Making scanner craft require only titanium. That hides the existing recipe truth conflict instead of resolving it.

## Player-Facing Route

Target duration: first 20 minutes, with the first mandatory loop completeable in 8-12 minutes by a normal player who reads oxygen and returns.

Route name: `First20_ScaffoldReef_SalvageLoop`.

Moments:

1. Exit safe anchor into bright photic shallows.
   - Depth band: 0-35 m for the mandatory route.
   - Readable landmark: broken scaffold mast, cable run, or buoyant wreck spine visible from safe anchor.
   - Unease: distant silhouette, hydrophone groan, or route-shadow event staged beyond the resource pocket, not blocking baseline collection.

2. Oxygen turn-back lesson.
   - Mandatory resource pocket is close enough to return alive on starter oxygen.
   - Optional deeper glint at 45-65 m tempts overextension and can kill or force retreat.
   - O2 warning is instrument-first: gauge/audio/visor, not only a UI bar.

3. Titanium salvage contact.
   - First `Data_TitaniumScrap` source is an authored shallow outcrop/debris pocket with deterministic titanium yield.
   - If SalvageSampler is required, it must be starter-assigned or unlocked before this objective.
   - Harvest leaves a visible scar/depleted state for save/load proof.

4. Copper wire material contact.
   - Copper for first `Comp_CopperWire` comes from the same scenic loop by authored shallow outcrop/cache/debris source.
   - `CopperVein` remains Drill-gated for later progression.
   - Candidate economy owner: align both runtime `RecipeData` and `Data/Economy` mirrors to 2 copper unless Crafting owner declares Unity `RecipeData` as runtime source and regenerates mirrors.

5. Scanner craft.
   - Scanner remains the first meaningful route-changing craft.
   - Candidate scanner ingredients: `Comp_SensorPackage` + `Comp_CopperWire`; route must place/provide the sensor package or make it an explicit safe-anchor salvage reward.
   - Quest graph should complete scanner from actual craft result, not from titanium collection alone.

6. Death/recovery check.
   - Core tools are retained.
   - `Item_Tool_Scanner` is retained once crafted, or a deterministic recovery marker/cache is created.
   - Loose copper/titanium can drop to death cache if recoverable and quest state cannot deadlock.

7. Save/load evidence.
   - Depleted outcrop/scar persists.
   - Quest flags persist.
   - Death cache persists.
   - Scanner/copper wire inventory state persists.

## Truth Owner Decisions

| Fact | Candidate owner | Route |
|---|---|---|
| First-20 mandatory quest spine | Gameplay/Narrative | `Quest_Graph.json` + `FirstHourDirector` aligned to one spine |
| First resource placement | World/Scavenging | authored route anchor/outcrop/cache, not profile prose |
| Tool capability gate | Tools/Interaction | starter tool loadout/unlock must match required source |
| Runtime recipe truth | Crafting/Data | one owner selected, mirrors regenerated from it |
| Oxygen/death truth | Survival/Respawn | survival state + respawn reconciliation route |
| Inventory drop/retain truth | Inventory | penalty rules from DataVault, SignalBus result telemetry |
| World scar persistence | Persistence/World | stable IDs and save section for depleted source/cache |
| Scenic readability | World/Rendering/Presentation | screenshots required by implementing owner |

## Oxygen, Tank, Hose, Tool Constraints

First-20 candidate constraints:

- Starter oxygen supports the mandatory loop only when the player turns back after collection.
- Mandatory route remains inside 0-35 m unless oxygen support is physically placed.
- Optional route extension uses one of:
  - visible emergency O2 canister pickup;
  - safe pocket/refill landmark;
  - later hose/tether from safe anchor or base, not required for first mandatory resource.
- Tank upgrade is not the first mandatory craft. It can be optional or immediately after scanner proof.
- Scanner does not bypass oxygen. It reduces navigation uncertainty and reveals evidence/resource confidence.
- SalvageSampler/Drill are not silently assumed. If a resource requires them, the route must grant/unlock them before the objective.

## Performance-Safe Route Rules

- No runtime scene search or string lookup in first-hour hot paths.
- Resource, recipe, quest, and respawn identifiers resolve to stable numeric hashes/baked IDs for runtime.
- Route markers and scenic cues are authored/proxy-based; no per-frame dynamic objective search.
- Visual route richness scales with `GlobalQualityWeight`, but item IDs, recipe costs, quest state, oxygen math, death rules, and save identity do not.
- Cheap-first presentation options: authored landmark silhouette, pooled bubbles/silt, scan decal, outcrop scar mesh/material state, audio cue. Do not simulate broad fluid/mineral systems for the first-route proof.

## Scaling Consequences

- Low: same route, same recipe, same oxygen/death truth. Use strong landmarks, sparse premium scatter, simple pooled VFX, clear outcrop silhouettes, instrument O2 warnings.
- Middle: richer shallow biota, better material breakup, more scan/depletion feedback, moderate silt/bubble response.
- High: denser scenic reef/scaffold composition, stronger caustic hints, better wet rock/salvage material response, richer tool/fabricator feedback.
- Ultra: visual overkill only: longer sightline beauty, dense but bounded biota, richer scanner acoustic visualization, detailed outcrop scars, higher fidelity fabricator arcs. No gameplay truth changes.

## Proof Gates For Implementing Owner

- `FH20_SINGLE_SPINE`: one mandatory first-20 route active.
- `FH20_STARTER_TOOL_MATCH`: starter loadout or unlock matches first resource source.
- `FH20_TITANIUM_REACH`: collect deterministic `Data_TitaniumScrap` from authored shallow route.
- `FH20_COPPER_REACH`: collect deterministic `Data_Copper` from authored shallow route without CopperVein gate violation.
- `FH20_CRAFT_COPPERWIRE`: fabricator consumes declared copper quantity from runtime recipe owner.
- `FH20_CRAFT_SCANNER`: scanner craft consumes actual ingredients and completes matching quest.
- `FH20_OXYGEN_RETURN`: route complete with nonzero O2 margin; overextension remains dangerous.
- `FH20_DEATH_RECOVERY`: death after resource/craft preserves core tools and avoids critical-item deadlock.
- `FH20_SAVE_LOAD`: quest, inventory, route scar, death cache, and marker state survive load.
- `FH20_SCENIC_CAPTURE`: compact and normal screenshots show bright scenic photic loop with unease.
- `FH20_BLACK_BOX`: survival/respawn failure has last-300-frame telemetry fields sufficient to explain death and inventory drop.

## Unresolved Questions

- Which system is the active runtime recipe owner today: Unity `RecipeData` assets, baked `Data/Economy/*.h8bin`, or a bridge? Candidate assumes a single owner must be declared before implementation.
- Does an authored first-route placement system already exist for shallow outcrop/cache anchors, or must the implementing owner add a route placement packet?
- Which of the starter tool prefab GUIDs correspond to Scanner, Repair, Builder, LaserCutter, SalvageSampler, and any drill candidate? Static prefab names were not resolved in this docs-only pass.
- Is `Comp_SensorPackage` meant to be a safe-anchor salvage reward, a crafted component, or a placed resource? Scanner craft cannot be truth-aligned until this is explicit.

## Status

Candidate is implementation-ready as a design/spec packet only. Runtime claims remain `PENDING VERIFICATION`.
