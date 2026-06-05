# 1847 - First-Hour Oxygen Route Guard

## Scope

Cold content validation for the first-hour emergency oxygen route.

## Changes

- `Assets/_Project/Scripts/Editor/ContentSanityValidator.cs`
  - Added `EmergencyO2CanisterItemId`.
  - Added `FirstHourOxygenRouteErrorCount`.
  - Added `ValidateFirstHourOxygenRoute`.
  - The validator now rejects a first-hour O2 route when `Data_EmergencyO2Canister` is missing, not consumable, non-stackable, not craftable, absent from `ItemCatalog`, or cataloged without positive `OxygenRestore`.
  - Summary now emits `FirstHourOxygenRouteErrors`.

## Evidence

- `Data_EmergencyO2Canister.asset` exists and currently has `isConsumable: 1`, `stackable: 1`, `maxStack: 4`, `oxygenRestore: 35`.
- `Recipe_EmergencyO2Canister.asset` exists under `Assets/_Project/Data/Crafting/Recipes` and produces `Data_EmergencyO2Canister`.
- `ConsumableItem.TryConsume` and `PlayerInventory.ConsumeOneItem` both route positive oxygen restore to `HectonSurvivalSystem.RefillOxygen`.
- `ItemCatalog.ItemRuntimeDescriptor` contains `IsConsumable` and `OxygenRestore`, so the validation checks the same descriptor route used by inventory consumption.

## Verification

- `git diff --check -- Assets/_Project/Scripts/Editor/ContentSanityValidator.cs` passed.
- Focused source scan confirmed `FirstHourOxygenRoute` and `FirstHourOxygenRouteErrors` are present.
- Unity validation was not launched because Unity editor, `Unity.ILPP.Runner`, and shader compilers were active.

## Remaining

- First-hour copper remains Drill-gated, which is correct.
- The project still needs a real `Item_Tool_SeafloorDrill` item, held prefab, and tool route. Do not weaken copper to Knife or Any.
