# 1843 First-Hour Tool And Craft Gate Patch

## Scope

Closed three first-hour progression leaks found by static inspection:
- direct tool-hit fallback could damage `ResourceNode` through `ICuttable` without respecting authored `requiredToolClass`;
- `FirstHourDirector` completed `FirstCraft` for any crafted result;
- legacy root `Assets/_Project/Data/Items/Data_Copper.asset` duplicated `stableId: Data_Copper` while the canonical raw copper asset already owned active routes.

## Changes

- `ResourceNode` now implements `IInteractionVulnerabilitySource`.
- `ResourceNodeTemplate` exposes `RequiredToolClass` for cold/runtime validators.
- `ToolCapabilityMasks` now includes `Salvage`.
- `ToolHitUtility.ApplyDamage` accepts an optional `toolCapabilityMask` and rejects direct damage when the target exposes an incompatible vulnerability mask.
- Knife, harpoon, stun pistol, and salvage sampler now pass their capability masks into direct tool-hit damage.
- `FirstHourDirector` now accepts only useful early crafted results for `FirstCraft`:
  - `Comp_CopperWire`
  - `Data_EmergencyO2Canister`
  - `Item_Tool_BeaconDeployer`
  - `Item_Tool_Repair`
  - `Comp_PressureSeal`
- `FirstHourDirector` consumes rich `SignalBus<CraftingCompletedSignal>` snapshots in `LateFrameTick`, while legacy `CraftingEvents` remains supported through the same whitelist.
- `ContentSanityValidator` now verifies:
  - first-hour craft milestone whitelist items exist in `ItemCatalog`;
  - whitelist items are produced by recipes;
  - `ResourceNodeTemplate_CopperVein.asset` remains Drill-gated.
- Deleted obsolete legacy copper asset:
  - `Assets/_Project/Data/Items/Data_Copper.asset`
  - `Assets/_Project/Data/Items/Data_Copper.asset.meta`

## Evidence

- Active canonical copper route uses `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset` GUID `7a9f752461931354e865d30b319c0f35`.
- Legacy GUID `84877e24023afe648a6682f49f11defa` had no active references under `Assets/_Project` except its own `.meta` before deletion.
- Post-delete scan under `Assets/_Project` found:
  - one active `stableId: Data_Copper`, the raw resource asset;
  - no active legacy GUID references;
  - canonical raw copper referenced by ItemCatalog, Player prefab starter material, recipes, barter offers, resource channels/plans, copper vein, and copper pickup prefab.
- `git diff --check` passed for touched files; only existing CRLF conversion warnings were emitted.

## Verification Blocked

Unity/runtime validation and editor `ContentSanityValidator` execution were not launched because the editor slot was busy:
- active Unity editor;
- `Unity.ILPP.Runner`;
- multiple `UnityShaderCompiler` processes.

Launching `dotnet` or Unity validation during that state would violate the project build/CPU rule.

## Remaining

- Validate in Unity when the editor/import slot is clear.
- Continue first-hour route work:
  - production starter loadout authority;
  - oxygen hose/tank route clarity;
  - real scene placement and runtime smoke pass.
