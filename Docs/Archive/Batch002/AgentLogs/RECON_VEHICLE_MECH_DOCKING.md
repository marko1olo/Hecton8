# VEHICLE_MECH_DOCKING Recon

Scan scope: `Assets/_Project/Scripts/**/*.cs`
Command evidence:
- `rg -n "FixedJoint|CharacterJoint" Assets/_Project/Scripts -g "*.cs"` returned no matches.
- `rg -n "\.SetParent\s*\(" Assets/_Project/Scripts -g "*.cs"` returned 238 matches.
- `rg -n "FixedJoint|CharacterJoint|SetParent\s*\(" Assets/_Project/Scripts/Construction/VehicleDockingModule.cs` returned no matches.

## Findings

No Unity physics joint types were found in script source.

Runtime `SetParent` sites that should not be copied into docking code:
- `Assets/_Project/Scripts/Gameplay/Floater.cs:348` and `:422` dynamically attaches/detaches floaters to the player.
- `Assets/_Project/Scripts/PlayerToolManager.cs:1312` and `:1358` attaches/detaches held tool instances to the hand anchor.
- `Assets/_Project/Scripts/Gameplay/HectonPlayerCameraRig.cs:271` reparents VR/camera tracking root.
- `Assets/_Project/Scripts/ModularEquipmentEngine.cs:231` detaches modular equipment root.
- `Assets/_Project/Scripts/BeaconNetworkSystem.cs:534` reparents beacon body to runtime beacon root.

Cold/runtime construction sites with acceptable scope:
- `ObjectPoolManager.cs` pool container parenting.
- UI builder scripts such as `HUDQuickBar`, `PlayerPDA`, `PDAInventoryTab`, and `HectonFabricatorUI`.
- world/procedural generation builders under `World*`, `Cave*`, and `HectonVoxel*`.
- editor authoring scripts under `Assets/_Project/Scripts/Editor`.

Docking decision: keep `VehicleDockingModule` on kinematic pose sync and AUP-relative state. Do not use runtime `SetParent`, `FixedJoint`, or `CharacterJoint` for vehicle lock.
