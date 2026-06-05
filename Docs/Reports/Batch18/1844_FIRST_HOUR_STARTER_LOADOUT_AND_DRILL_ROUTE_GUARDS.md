# 1844 First-Hour Starter Loadout And Drill Route Guards

Date: 2026-06-04 07:31 +04

## Scope

Patched the first-hour production starter loadout path and added content validation guards for the remaining copper-drill route gap.

## Source Changes

- `Assets/_Project/Scripts/PlayerToolManager.cs`
  - Added production starter loadout fields:
    - `grantAssignedToolItemsOnRuntimeStart`
    - `runtimeStartToolGrantBudget`
  - Added one-shot `TryGrantAssignedToolItemsOnRuntimeStart()`.
  - The grant only materializes missing inventory items for already-authored `toolPrefabs` quick-slot prefabs.
  - It uses the tool prefab's `PlayerTool.ToolData.PersistentId` and `PlayerInventory.TryAddItem`.
  - It does not use `ToolLoadoutProvisioner`, dev menus, logs, or invented item IDs.

- `Assets/_Project/Scripts/Editor/ContentSanityValidator.cs`
  - Added `ValidatePlayerStarterLoadout`.
  - Added `ValidateFirstHourDrillRoute`.
  - Summary now includes:
    - `FirstHourDrillRouteErrors`
    - `PlayerStarterLoadoutErrors`

## Current Evidence

- Canonical `Player.prefab` already authors quick-slot tool prefabs, but runtime availability was inventory-gated.
- `ToolLoadoutProvisioner` is explicitly editor/development-only and has startup provisioning disabled on the canonical player prefab.
- `ResourceNodeTemplate_CopperVein.asset` is Drill-gated after 1843, which is correct for progression.
- No `Item_Tool_SeafloorDrill` `ItemData` or held drill prefab currently exists under the project data/prefab routes. Only `Assets/_Project/Data/Survival/SurvivalDatabaseRuntime.txt` mentions `Item_Tool_SeafloorDrill`.

## Verification

- `git diff --check -- Assets/_Project/Scripts/PlayerToolManager.cs Assets/_Project/Scripts/Editor/ContentSanityValidator.cs`
  - Passed.
  - Git reported LF-to-CRLF working-copy warnings only.
- Process check still shows active Unity editor, `Unity.ILPP.Runner`, and multiple `UnityShaderCompiler` processes.
  - Did not run Unity content validation or a build while compilation/shader work is active.

## Expected Validator Result

Until a real seafloor drill route is authored, `ValidateFirstHourDrillRoute` is expected to report one blocker:

- copper is Drill-gated;
- `Item_Tool_SeafloorDrill` item asset is missing;
- held drill prefab for that item is missing.

Do not solve that by weakening copper to Knife/Any. Author the drill route or an explicit validated alternative.

## Next Required Work

- Author `Item_Tool_SeafloorDrill` and `Tool_SeafloorDrill_Held.prefab`, or create another explicit first-hour extraction route and teach the validator that route.
- Re-run Unity content validation when the editor is not compiling.
- Then perform first-hour runtime validation in Game View:
  - starter tools exist in inventory;
  - quick slots activate;
  - copper cannot be harvested by knife/salvage;
  - copper can be harvested by the intended drill route;
  - oxygen canister craft remains valid.
