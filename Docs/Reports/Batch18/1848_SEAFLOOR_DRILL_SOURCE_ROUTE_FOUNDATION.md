# 1848 - Seafloor Drill Source Route Foundation

## Scope

First-hour Drill route source foundation. This does not claim visual/prefab completion.

## Changes

- `Assets/_Project/Scripts/SeafloorDrillTool.cs`
  - Added `SeafloorDrillTool : PlayerTool, IToolModule`.
  - Publishes `InteractionSignal` through `IInteractionSignalService`.
  - Uses `InteractionEffectType.Drill` and `ToolCapabilityMasks.Drill`.
  - Uses `PlayerTool.RequestPrimarySurfaceHit` for the existing zero-allocation surface query lane.
  - Does not call `ICuttable.ApplyCutDamage` or bypass resource-node tool gates.
  - Adds cooldown, recoil, haptic feedback, operational summary/directive, and registry rebind handling.

- `Assets/_Project/Scripts/PlayerToolManager.cs`
  - Expanded `knownToolPrefabs` default capacity to 13.
  - Added future `Tool_SeafloorDrill_Held.prefab` path to editor auto-resolve.

- `Assets/_Project/Scripts/ToolLoadoutProvisioner.cs`
  - Expanded `allToolItems` default capacity to 13.
  - Added future `Item_Tool_SeafloorDrill.asset` path to editor auto-resolve.

## Evidence

- Copper is already Drill-gated in `ResourceNodeTemplate_CopperVein.asset`.
- `ResourceNode.ApplyInteractionSignal` already accepts Drill signals and maps them to Drill loot tooling.
- No existing `Item_Tool_SeafloorDrill`, `ToolMetadata_SeafloorDrill`, or held prefab exists yet.

## Verification

- `git diff --check` on the touched drill/tool-manager/provisioner files passed.
- Focused source scan confirmed:
  - `ToolCapabilityMasks.Drill`
  - `InteractionEffectType.Drill`
  - `interactionService.Publish(in signal, hit.collider)`
  - new item/prefab auto-resolve paths.
- Unity compile/editor validation was not launched because Unity editor, `Unity.ILPP.Runner`, and shader compilers were active.

## Remaining

- Author real `Item_Tool_SeafloorDrill.asset`.
- Author real `ToolMetadata_SeafloorDrill.asset`.
- Author a real `Tool_SeafloorDrill_Held.prefab` with production-grade visual mesh/materials. Do not stamp a primitive placeholder and call it done.
- Register it in `ItemCatalog`.
- Decide whether it is starter-granted or craftable; if craftable, author a real `Recipe_SeafloorDrill.asset`.
- Run Unity content validation once the editor is free.
