# RECON_CORE_SCAVENGING_CRAFTING

Prompt: CORE_SCAVENGING_CRAFTING
Domain: ECHELON 4 - Scavenging, S.O.A. Inventory, Crafting
Status: PENDING VERIFICATION

## Scan Scope

- Command: `rg -n "List<\s*Item|Item\.ID\s*==|ScriptableObject.*inventory|Inventory.*ScriptableObject|List<InventoryCost>|List<RecipeData>|List<ItemData>" Assets/_Project/Scripts --glob '!Library/**' --glob '!Temp/**'`
- Command: targeted string-id scan over `Assets/_Project/Scripts/Inventory`, `CraftingSystem.cs`, `RecipeData.cs`, `PlayerInventory.cs`, `PDAInventoryTab.cs`

## Findings

- `Assets/_Project/Scripts/RecipeData.cs:56` - authored `List<InventoryCost>` remains cold authoring data. Hot path now bakes `RecipeMask`.
- `Assets/_Project/Scripts/BuildableData.cs:98` - authored construction `List<InventoryCost>` outside crafting hot path.
- `Assets/_Project/Scripts/Fabricator.cs:80` - authored `List<RecipeData>` recipe catalog.
- `Assets/_Project/Scripts/Fabricator.cs:192` - `_visibleRecipes` managed UI/cache list.
- `Assets/_Project/Scripts/Fabricator.cs:1537` - reads `recipe.ingredients`; downstream crafting check now has bitmask fast-fail.
- `Assets/_Project/Scripts/ItemCatalog.cs:178` - authored `List<ItemData>` catalog.
- `Assets/_Project/Scripts/ItemCatalog.cs:201` and `:649` - runtime item overlay list for modded items.
- `Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs:516` - deferred mod item registrations.
- `Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs:595` - runtime-only mod recipe overlay.
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:645` and `:867` - cold `List<ItemData>` scratch for item catalog hash cache rebuild.
- `Assets/_Project/Scripts/Editor/*BootstrapAuthoring.cs` - editor-only authoring lists.

## String-ID Scan

- No targeted `Item.ID == "..."` matches found in the scavenging/crafting/inventory files scanned.
- Item IDs resolve through `ItemData.PersistentHashId`, which is computed via `LocHash.Compute(PersistentId)` using the existing FNV-1a path.

## Risk Notes

- Authored `List<InventoryCost>` remains acceptable for ScriptableObject editing but must not be used as the first validation gate in recipe browsing.
- Any future storage/container transfer should call the SoA `UnsafeUtility.MemCpy` helper only for same-layout dense ranges; shaped grid transfers still need placement validation before bulk copy.
