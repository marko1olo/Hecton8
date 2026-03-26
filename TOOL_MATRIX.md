# Tool Matrix

Do not delete notes from this file.
Append changes instead of replacing history where possible.
Use this as the source of truth for tool data, prefab status, and rollout order.

## Current rollout

As of this pass:
- `ItemData` assets created for 12 tools under `Assets/_Project/Data/Items/Tools`
- `ToolMetadata` assets created for 12 tools under `Assets/_Project/Data/Tools`
- world pickup prefabs created for all 12 tools under:
  - `Assets/_Project/Prefabs/Items/Tools`
- every tool `ItemData` now has a non-null `worldPrefab`
- held prefab scaffolds created for:
  - `Scanner`
  - `Repair`
  - `Builder`
  - `Laser Cutter`
- first-pass runtime scripts added for:
  - `Flashlight Tool Adapter`
  - `Propulsion Tool`
  - `Salvage Sampler`
  - `Beacon Deployer`
  - `Environmental Analyzer`
  - `Survival Blade`
  - `Stun Pistol`
  - `Harpoon Launcher`
- held prefab scaffolds created for those eight tools under `Assets/_Project/Prefabs/Tools/Held`
- placeholder visuals added to those four prefabs as colored cube bodies
- placeholder visuals added to the seven new prefabs as colored cube bodies
- `PlayerToolManager.toolPrefabs[0..3]` assigned to those four held prefabs
- live scene now also has:
  - `PlayerFlashlight` on `Player`
  - `DiveLamp_Light` under `Player/Main Camera`
  - `FlashlightTool` therefore has a real runtime light backend, not just data/prefab scaffolding
- live scene also has:
  - `--- WORLD ---/Tool_Staging`
  - `ToolStagingSpawner` authoring helper on that root
  - all 12 tool world prefabs laid out in-scene for future pickup/drop testing

## Matrix

| Tool | ItemData | ToolMetadata | Held Prefab | Runtime Script | Status |
|---|---|---|---|---|---|
| Scanner | `Item_Tool_Scanner` | `ToolMetadata_Scanner` | `Tool_Scanner_Held` | `ScannerTool` | scaffold ready |
| Repair Tool | `Item_Tool_Repair` | `ToolMetadata_Repair` | `Tool_Repair_Held` | `RepairTool` | scaffold ready |
| Builder Tool | `Item_Tool_Builder` | `ToolMetadata_Builder` | `Tool_Builder_Held` | `BuilderTool` | scaffold ready |
| Laser Cutter | `Item_Tool_LaserCutter` | `ToolMetadata_LaserCutter` | `Tool_LaserCutter_Held` | `LaserCutter` | scaffold ready |
| Flashlight | `Item_Tool_Flashlight` | `ToolMetadata_Flashlight` | `Tool_Flashlight_Held` | `FlashlightTool` + existing `PlayerFlashlight` system | first-pass adapter + prefab |
| Propulsion Tool | `Item_Tool_Propulsion` | `ToolMetadata_Propulsion` | `Tool_Propulsion_Held` | `PropulsionTool` | first-pass runtime + prefab |
| Salvage Sampler | `Item_Tool_SalvageSampler` | `ToolMetadata_SalvageSampler` | `Tool_SalvageSampler_Held` | `SalvageSamplerTool` | first-pass runtime + prefab |
| Beacon Deployer | `Item_Tool_BeaconDeployer` | `ToolMetadata_BeaconDeployer` | `Tool_BeaconDeployer_Held` | `BeaconDeployerTool` | first-pass runtime + prefab |
| Environmental Analyzer | `Item_Tool_EnvAnalyzer` | `ToolMetadata_EnvAnalyzer` | `Tool_EnvAnalyzer_Held` | `EnvironmentalAnalyzerTool` | first-pass runtime + prefab |
| Survival Blade | `Item_Tool_Knife` | `ToolMetadata_Knife` | `Tool_Knife_Held` | `KnifeTool` | first-pass runtime + prefab |
| Stun Pistol | `Item_Tool_StunPistol` | `ToolMetadata_StunPistol` | `Tool_StunPistol_Held` | `StunPistolTool` | first-pass runtime + prefab |
| Harpoon Launcher | `Item_Tool_HarpoonLauncher` | `ToolMetadata_HarpoonLauncher` | `Tool_HarpoonLauncher_Held` | `HarpoonLauncherTool` | first-pass runtime + prefab |

## Notes

- `FlashlightTool` is an adapter over the existing `PlayerFlashlight` system. It does not create a second lighting pipeline.
- `Tool_Flashlight_Held` exists as a scaffold prefab with placeholder visual and bound `ItemData/ToolMetadata`.
- The four created held prefabs are logic scaffolds only. They still need proper visuals, audio refs, and presentation tuning.
- The eight newly added/updated prefabs are also scaffolds. They have real `PlayerTool` scripts and bound `ItemData/ToolMetadata`, but their behavior is intentionally first-pass:
  - `FlashlightTool` = safe wrapper for toggle/status over `PlayerFlashlight`
  - `PropulsionTool` = push/pull rigidbodies
  - `SalvageSamplerTool` = cut/sample + secondary item pickup
  - `BeaconDeployerTool` = world beacon drop + nearest-beacon cleanup
  - `EnvironmentalAnalyzerTool` = target/suit readout via `HUDNotification`
  - `KnifeTool` = short-range melee hit
  - `StunPistolTool` = damage + temporary AI disable runtime helper
  - `HarpoonLauncherTool` = ranged hit + secondary reel impulse
- `ItemCatalog` was expanded to include the new tool item assets.
- All 12 tool items now support `DROP -> world pickup prefab` at the data level.
- Full scene-level tool rack now exists independently of the player's 4 active quick slots.
- There is still a legacy `Item_Builder_Device.asset` in the project; treat it as legacy until we intentionally migrate or remove it.
