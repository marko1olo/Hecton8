# Copper Starter Chain Reachability - 2026-06-05

Status: STATIC BLOCKER / FIRST-20 RESOURCE CHAIN NOT PROVEN

Evidence class: `STATIC_SOURCE`, `STATIC_ASSET`.

## Verdict

Copper data is internally coherent, but copper is not proven reachable in the first route because the copper node requires Drill and the starter drill route is missing.

This breaks the first-20 resource -> tool -> craft/repair/build spine if copper remains the selected V0 resource.

## Coherent Copper Data

- `Assets/_Project/Data/Lore/Quests/Quest_CopperSample.asset:30` completes on `Data_Copper`, value `1`.
- `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset:16` uses `stableId: Data_Copper`.
- `Assets/_Project/Data/Crafting/Recipes/Recipe_CopperWire.asset:26` produces `Comp_CopperWire`.
- `Assets/_Project/Data/Crafting/Recipes/Recipe_CopperWire.asset:29` consumes `Data_Copper`.
- `Assets/_Project/Data/Crafting/Recipes/Recipe_CopperWire.asset:33` has no scan lock.

## Blocker

- `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_CopperVein.asset:19` requires tool class `2`.
- `Assets/_Project/Scripts/Scavenging/ResourceNodeTemplate.cs:36` maps `Drill = 2`.
- `Assets/_Project/Scripts/ResourceNode.cs:504` maps Drill to `ToolCapabilityMasks.Drill`.
- `Assets/_Project/Scripts/KnifeTool.cs:190` applies `Cut`, not `Drill`.
- `Assets/_Project/Scripts/SeafloorDrillTool.cs:116` has drill code and returns `Drill`.
- Missing assets:
  - `Assets/_Project/Data/Items/Tools/Item_Tool_SeafloorDrill.asset`
  - `Assets/_Project/Data/Tools/ToolMetadata_SeafloorDrill.asset`
  - `Assets/_Project/Prefabs/Tools/Held/Tool_SeafloorDrill_Held.prefab`
- `Assets/_Project/Prefabs/Player.prefab:1681` has starter provisioning disabled.
- `Assets/_Project/Prefabs/Player.prefab:1691` all-tool list does not include seafloor drill.
- `Assets/_Project/Scripts/Editor/ContentSanityValidator.cs:1115` explicitly treats Drill-gated copper without first-hour seafloor drill route as incomplete.

## Replacement Options

### Preferred Short Reroute

`Data_FiberKelp -> Comp_FiberMesh -> Comp_PressureSeal`

- `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_FiberKelpStand.asset:19` requires tool class `0`, depth `0-140`.
- `Assets/_Project/Data/Crafting/Recipes/Recipe_FiberMesh.asset:29` consumes `Data_FiberKelp`.
- `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs:740` already accepts `Comp_PressureSeal` as first craft.
- Caveat: membrane/resin route placement still needs proof.

### Minimal Shallow Craft Proof

`Data_SilicaShards -> Comp_GlassPanel`

- `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_SilicaShardCluster.asset:19` requires tool class `0`, depth `0-260`.
- `Assets/_Project/Data/Crafting/Recipes/Recipe_GlassPanel.asset:29` consumes `Data_SilicaShards`.
- Caveat: `Comp_GlassPanel` is not currently accepted by `FirstHourDirector.cs:727`; it needs a real repair/build use or director/quest swap.

## Next Decision

Choose one:

1. Author starter seafloor drill item, metadata, held prefab, and starter acquisition/loadout route.
2. Replace first-route copper with FiberMesh/PressureSeal or Silica/GlassPanel and update quest/director/resource placement.

Until one path is implemented and Unity-verified, first-20 resource chain remains `PENDING VERIFICATION`.
