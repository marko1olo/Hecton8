# 1826 Copper Route Data Owner Patch Packet

Agent: 1826 / COPPER_ROUTE_DATA_OWNER_PATCH_PACKET  
Date: 2026-06-04  
Evidence class: STATIC_SOURCE / STATIC_DOC only  
Runtime state: PENDING UNITY SLOT  
Mutation state: NO ASSET OR SOURCE MUTATION PERFORMED

## Scope

This packet converts the Batch18 copper identity findings into a future data-owner patch sequence. It does not edit `.asset`, `.meta`, source, scene, prefab, source-data, generated CSV, binary, or task files. It does not run Unity, PlayMode, importers, exporters, DataMonolith bake, or builds.

Owned outputs:

- `Docs/Reports/Batch18/1826_COPPER_ROUTE_DATA_OWNER_PATCH_PACKET.md`
- `Docs/Reports/Batch18/1826_COPPER_REFERENCE_CHECKLIST.csv`
- `Docs/Tasks/Status_1826.md`
- `Docs/AgentLogs/Rationale_1826.md`
- `Docs/AgentLogs/LOG_1826.md`

## Authorities Read

Root and domain:

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `data.md`
- `tools.md`
- `gameplay.md`
- `inventory.md`
- `persistence.md`
- `authoring.md`

Requested root files absent or empty at the checked path:

- `items.md`
- `crafting.md`

Batch18 evidence:

- `Docs/Reports/Batch18/1803_FIRST20_ROUTE_BLOCKER_MATRIX.md`
- `Docs/Reports/Batch18/1814_COPPER_CATALOG_COLLISION_AUDIT.md`
- `Docs/Reports/Batch18/1815_STARTER_TOOL_AND_ROUTE_CRAFT_AUTHORITY.md`
- `Docs/Reports/Batch18/1816_SURFACE_ROUTE_UNITY_SLOT_PACKET.md`

Mandates loaded:

- `.agents-skills/DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `.agents-skills/DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `.agents-skills/TOOL_Designer_Facades_CSV_Binary_Bridge.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/PROG_Quest_State_Graph_Logic.txt`
- `.agents-skills/CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`

`Docs/Actual Domains of Project.txt` was checked and is absent. Narrow domain inferred: item identity, inventory/crafting route data, save identity, first-route copper proof packet.

## Static Evidence Reconfirmed

### Raw Route Owner

Path:

- `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset`

Meta:

- `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset.meta`
- GUID: `7a9f752461931354e865d30b319c0f35`

Relevant serialized fields:

- `m_Name: Data_Copper`
- `m_EditorClassIdentifier: Assembly-CSharp::Hecton8.Items.ItemData`
- `legacyItemName: Copper Ore`
- `stableId: Data_Copper`
- `maxStack: 32`
- `category: 1`
- `resourceFamily: 2`
- `progressionTier: 1`
- `isRawResource: 1`
- `worldPrefab: {fileID: 3748236069054768607, guid: 1dbf7c4f900fc7e4b8c2a5bc7b5e17d2, type: 3}`

Static active-reference search found this raw GUID in active first-party data, prefabs, and editor/source paths, including:

- `Assets/_Project/Data/Items/ItemCatalog.asset`
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_CopperOre.prefab`
- `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_CopperVein.asset`
- `Assets/_Project/Data/Crafting/Recipes/Recipe_CopperWire.asset`
- `Assets/_Project/Data/Crafting/Recipes/Recipe_BatteryCell.asset`
- biome resource plans/channels/family profiles
- barter offers
- `Assets/_Project/Prefabs/Player.prefab`
- editor bootstrap/validation source paths

Conclusion: raw copper is the current route owner and must keep `stableId: Data_Copper`.

### Legacy Colliding Asset

Path:

- `Assets/_Project/Data/Items/Data_Copper.asset`

Meta:

- `Assets/_Project/Data/Items/Data_Copper.asset.meta`
- GUID: `84877e24023afe648a6682f49f11defa`

Relevant serialized fields:

- `m_Name: Data_Copper`
- `m_EditorClassIdentifier: Assembly-CSharp::Hecton.Items.ItemData`
- `legacyItemName: Copper`
- `stableId: Data_Copper`
- `maxStack: 64`
- `category: 0`
- `resourceFamily: 0`
- `progressionTier: 0`
- `isRawResource: 0`
- `worldPrefab: {fileID: 0}`

Static active-reference search across `Assets/_Project` found `84877e24023afe648a6682f49f11defa` only in the legacy asset `.meta`. No active first-party route data/source/prefab reference to the legacy GUID was found.

Conclusion: this asset is not current first-route copper owner, but its `stableId: Data_Copper` and `m_Name: Data_Copper` are unsafe because catalog and save routes resolve by persistent ID/hash aliases.

### Collision Proof

Command evidence:

```powershell
rg -n "stableId: Data_Copper" Assets\_Project\Data
```

Result:

- `Assets/_Project/Data/Items/Data_Copper.asset:16`
- `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset:16`

This is a real identity collision under the active data root.

## Why This Blocks Route Truth

`ItemData.PersistentId` returns `stableId` when present. `ItemData.PersistentHashId` hashes `PersistentId`. `ItemData.MatchesPersistentHash` accepts the persistent hash. `ItemCatalog.RebuildLookup` adds `item.PersistentId`, `item.name`, and hash aliases. `ContentSanityValidator.ValidateItemTemplates` scans all `ItemData` assets under `Assets/_Project/Data` and records duplicate `PersistentId`.

Route/craft/save users of this identity:

- `Quest_CopperSample.asset` completes with `completionId: Data_Copper`.
- `FirstHourDirector` uses `firstResourceItemId = "Data_Copper"` and hash checks for pickup/save recovery.
- `H8Hashes.Items.DataCopperId = "Data_Copper"` and `DataCopperHash = 2276338585u`.
- `TradeMarauderRuntime` hashes `"Data_Copper"`.
- `Recipe_CopperWire.asset` consumes raw copper GUID `7a9f752461931354e865d30b319c0f35`.
- `ResourceNodeTemplate_CopperVein.asset` yields raw copper GUID `7a9f752461931354e865d30b319c0f35`.

Risk:

- A saved or quest-collected `Data_Copper` stack cannot distinguish raw copper ore from the legacy root asset if both remain reachable.
- If the legacy asset enters `ItemCatalog`, lookup can become ambiguous or bind to non-raw copper: wrong `isRawResource`, wrong stack, no world prefab, wrong family/tier.
- UI/log text cannot be route truth. The patch must resolve data identity, not rename a display string only.

## Safe Mutation Options

### Option A - Recommended: Quarantine And Rename Legacy Asset, Preserve GUID

Future data owner uses Unity `AssetDatabase` or controlled serialized edit in a Unity-authorized patch slot.

Exact intent:

- Keep raw asset unchanged:
  - path: `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset`
  - GUID: `7a9f752461931354e865d30b319c0f35`
  - `stableId: Data_Copper`
- Move/rename legacy asset with its `.meta` preserved:
  - from: `Assets/_Project/Data/Items/Data_Copper.asset`
  - to: `Assets/_Project/Data/Items/Legacy/Data_CopperHullPlate_Legacy.asset`
  - GUID stays `84877e24023afe648a6682f49f11defa`
- Update legacy serialized identity:
  - `m_Name: Data_CopperHullPlate_Legacy`
  - `stableId: Data_CopperHullPlate_Legacy`
  - `legacyItemName: Legacy Copper Hull Plate`
  - `legacyDescription`: legacy/non-route salvage description only
- Keep legacy asset out of `ItemCatalog.asset` unless a later route owner gives it a real salvage use, localization rows, stack rules, world prefab, and validation proof.

Why:

- Removes duplicate `PersistentId`.
- Removes duplicate `item.name` alias if the asset is ever added to catalog.
- Preserves the legacy GUID for hidden migration/reference discovery.
- Avoids immediate deletion of old data with unclear historical intent.
- Leaves current raw route references untouched.

Rollback:

- Move the legacy asset and `.meta` back to `Assets/_Project/Data/Items/Data_Copper.asset`.
- Restore `m_Name` and `stableId` only by reverting the whole patch from VCS or pre-patch backup. Do not hand-edit partial rollback if Unity imported the asset.
- If validation failed because the move path is illegal, move back through Unity `AssetDatabase.MoveAsset` and rerun validation.

### Option B - Rename StableId In Place Only

Future data owner changes the legacy asset fields but leaves the file path at `Assets/_Project/Data/Items/Data_Copper.asset`.

Minimum fields:

- `m_Name: Data_CopperHullPlate_Legacy`
- `stableId: Data_CopperHullPlate_Legacy`
- `legacyItemName: Legacy Copper Hull Plate`

Risk:

- File path still misleads future agents and authoring tools.
- Lower clarity than Option A.

Use only if asset moves are blocked by an active Unity/import constraint.

Rollback:

- Revert the one asset file from VCS/pre-patch backup.
- No `.meta` movement expected.

### Option C - Delete Legacy Asset And Meta

Allowed only after preflight reference proof and owner signoff.

Preflight required:

- `rg -n "84877e24023afe648a6682f49f11defa" Assets/_Project`
- Expected result before delete: only the legacy `.meta` file.
- `rg -n "Assets/_Project/Data/Items/Data_Copper.asset|Assets\\_Project\\Data\\Items\\Data_Copper.asset" Assets/_Project`
- Expected result before delete: no active source/data/prefab dependency.

Deletion command must remove asset and `.meta` together. Unity AssetDatabase deletion is preferred. Filesystem deletion must delete both paths in one scoped operation and scan for orphan meta after.

Risk:

- Deletes possible migration evidence.
- Future old-save migration analysis loses a concrete legacy descriptor.

Rollback:

- Restore both `Assets/_Project/Data/Items/Data_Copper.asset` and `Assets/_Project/Data/Items/Data_Copper.asset.meta` from VCS/pre-patch backup.
- Reimport and rerun content validation.

### Option D - Migrate References To Raw Asset, Then Delete

Not needed for current active first-party roots because static GUID search found no active reference to legacy GUID outside its `.meta`.

Use only if a future preflight discovers hidden active references to `84877e24023afe648a6682f49f11defa`.

Rollback:

- Revert migrated reference files and restore legacy asset/meta.

## Recommended Patch Owner Sequence

### 0. Slot Preconditions

Required:

- Single data-owner slot.
- Unity/import slot free.
- CPU/build gate clear.
- No active editor import or compile.
- VCS or file backup available for:
  - `Assets/_Project/Data/Items/Data_Copper.asset`
  - `Assets/_Project/Data/Items/Data_Copper.asset.meta`
  - `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset`
  - `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset.meta`
  - `Assets/_Project/Data/Items/ItemCatalog.asset`

Do not start if another agent is editing item data, catalog, crafting recipes, source data, or DataMonolith output.

### 1. Static Preflight

Run before mutation:

```powershell
rg -n "stableId: Data_Copper" Assets\_Project\Data
rg -n "7a9f752461931354e865d30b319c0f35|84877e24023afe648a6682f49f11defa" Assets\_Project
rg -n "Assets/_Project/Data/Items/Data_Copper.asset|Assets\\_Project\\Data\\Items\\Data_Copper.asset" Assets\_Project Docs\ARCHITECTURE Docs\Reports\Batch18
rg -n "Data_Copper|Recipe_CopperWire|quest_copper_sample|Quest_CopperSample" Assets\_Project
```

Preflight pass conditions:

- `stableId: Data_Copper` appears exactly twice before patch: raw plus legacy.
- Raw GUID appears in active route references.
- Legacy GUID appears only in its `.meta` or in references explicitly approved for migration.
- No active first-party source path depends on the legacy root path.

If any active legacy GUID reference is found, label `BLOCKED_REFERENCE_TO_LEGACY_GUID` and use Option D instead of Option A.

### 2. Mutate Legacy Only

Preferred Unity operations:

1. Create `Assets/_Project/Data/Items/Legacy/` if it does not exist.
2. Move `Assets/_Project/Data/Items/Data_Copper.asset` to `Assets/_Project/Data/Items/Legacy/Data_CopperHullPlate_Legacy.asset` through Unity AssetDatabase so `.meta` and GUID are preserved.
3. Edit the moved asset:
   - `m_Name = Data_CopperHullPlate_Legacy`
   - `stableId = Data_CopperHullPlate_Legacy`
   - `legacyItemName = Legacy Copper Hull Plate`
4. Do not add this moved asset to `ItemCatalog.asset`.
5. Do not touch raw copper, copper recipes, copper node, pickup prefab, quest completion ID, or `H8Hashes`.

Do not change route truth from `Data_Copper` to a new raw ID. Current saves/quests/hash constants must keep resolving to raw copper.

### 3. Static Post-Mutation Checks

Run after mutation, before Unity validation:

```powershell
rg -n "stableId: Data_Copper" Assets\_Project\Data
rg -n "stableId: Data_CopperHullPlate_Legacy|m_Name: Data_CopperHullPlate_Legacy" Assets\_Project\Data\Items
rg -n "7a9f752461931354e865d30b319c0f35" Assets\_Project
rg -n "84877e24023afe648a6682f49f11defa" Assets\_Project
rg -n "guid: 7a9f752461931354e865d30b319c0f35|guid: 84877e24023afe648a6682f49f11defa" Assets\_Project
```

Expected:

- `stableId: Data_Copper` appears only in `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset`.
- legacy moved asset has distinct `m_Name` and `stableId`.
- raw GUID references are unchanged.
- legacy GUID appears in the moved `.meta` and any explicitly migrated/approved legacy references only.

### 4. Unity Import And Content Validation

Future patch owner must run in Unity:

- Let Unity import the moved/edited asset.
- Run `Hecton-8/Validate Content`.
- Record the validation report/output.

Required pass fields:

- `ItemDataDuplicatePersistentId=0`
- `ItemCatalogDuplicateHashes=0`
- `ItemCatalogLookupAmbiguities=0`
- no `RecipeRouteErrors` for `Recipe_CopperWire.asset` or `Recipe_BatteryCell.asset`
- no `ResourceNodeYieldNotCataloged` or `ResourceNodeYieldMissingWorldPrefab` for `ResourceNodeTemplate_CopperVein.asset`

If Unity validation cannot run, label `BLOCKED_VALIDATOR_NOT_RUN`. Static checks alone are not runtime/import proof.

### 5. Catalog Resolution Checks

Future Unity owner must prove:

- `ItemCatalog.FindById("Data_Copper")` resolves to `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset`.
- `ItemCatalog.FindByHash(H8Hashes.Items.DataCopperHash)` resolves to the raw asset.
- `ItemCatalog.HasLookupAmbiguity == false`.
- raw descriptor fields in runtime descriptor match route expectations:
  - stackable true
  - max stack 32
  - category Material
  - raw resource true by authoring source
  - world prefab not null

### 6. DataMonolith Gate

If DataMonolith consumes item/catalog data, the data-owner patch is not runtime-ready until:

- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is rebuilt by the approved owner.
- Import/bake/boot validation proves the rebuilt binary contains a single `Data_Copper` record resolved to the raw asset semantics.
- Old binary is not used as proof after asset mutation.

If bake/import is unavailable, label `BLOCKED_DATAMONOLITH_STALE`.

### 7. Route Smoke Proof Gates

Future Unity/player route proof must show:

- Copper vein or route source yields raw `Data_Copper`.
- `quest_copper_sample` completes from actual raw copper pickup/inventory state.
- `Recipe_CopperWire.asset` consumes raw copper and produces `Comp_CopperWire`.
- Save/load restores `Data_Copper` stack with raw descriptor semantics and stack rule 32.
- No duplicate ID/hash warnings in console/content validation.

This packet does not fix:

- starter tool authority for `requiredToolClass: 2`;
- generic `FirstCraft` completion from any craft;
- live route socket population;
- visual/player/profiler proof.

Those remain separate Batch18 blockers.

## Reference Checklist

Machine-readable checklist:

- `Docs/Reports/Batch18/1826_COPPER_REFERENCE_CHECKLIST.csv`

High-risk rows:

- legacy GUID active references: none found outside `.meta` in active `Assets/_Project`.
- duplicate stable ID: confirmed in raw and legacy assets.
- quest/craft route uses stable ID/hash and raw GUID mixed route, so identity cleanup must happen before runtime proof.

## Failure Labels

Use these exact labels in the future patch status:

- `BLOCKED_REFERENCE_TO_LEGACY_GUID`: active first-party reference to `84877e24023afe648a6682f49f11defa` exists beyond legacy `.meta` and requires migration.
- `BLOCKED_RAW_GUID_MISSING`: raw asset or raw `.meta` missing.
- `BLOCKED_RAW_GUID_CHANGED`: raw asset GUID changed from `7a9f752461931354e865d30b319c0f35`.
- `BLOCKED_DUPLICATE_STABLE_ID`: post-patch `rg "stableId: Data_Copper" Assets/_Project/Data` returns more than raw asset.
- `BLOCKED_LEGACY_ALIAS_STILL_DATACOPPER`: legacy asset still has `m_Name: Data_Copper` or `stableId: Data_Copper`.
- `BLOCKED_CATALOG_RESOLVES_LEGACY`: `ItemCatalog.FindById/FindByHash` resolves to non-raw legacy.
- `BLOCKED_VALIDATOR_NOT_RUN`: Unity content validation was not run after mutation.
- `BLOCKED_DATAMONOLITH_STALE`: binary data payload not rebuilt/validated after mutation.
- `BLOCKED_ROUTE_STARTER_TOOL`: copper still requires `requiredToolClass: 2` and route starter authority is unproven.
- `BLOCKED_FIRSTCRAFT_GENERIC`: `FirstHourDirector` still accepts any craft as `FirstCraft`.

## Validation Gates Summary

Static gates that do not require Unity:

- asset and `.meta` existence for raw and legacy paths;
- raw/legacy GUID search in `Assets/_Project`;
- `stableId: Data_Copper` uniqueness after patch;
- raw GUID still referenced by catalog, pickup, copper vein, recipes;
- no active legacy path dependency in `Assets/_Project`.

Unity/editor gates:

- Unity import completes.
- `Hecton-8/Validate Content` reports zero duplicate item IDs/hashes/lookup ambiguity.
- catalog ID/hash resolution maps to raw copper.
- resource node/crafting/quest references pass editor validation.

Runtime gates:

- pickup -> quest completion;
- Copper Wire craft;
- save/load raw copper descriptor;
- no duplicate ID warnings;
- no route dependency on UI strings/log text.

DataMonolith gates:

- approved bake/import;
- single raw `Data_Copper` record in binary;
- boot validation with current binary;
- no claim from stale `static_data.h8bin`.

## First-20 Route Consequences

Copper sample:

- Keeps `completionId: Data_Copper`.
- Must resolve to raw copper only after patch.

Tool class:

- Unchanged. `ResourceNodeTemplate_CopperVein.asset` still has `requiredToolClass: 2`.
- Starter tool authority remains blocked until a product route owner proves the correct extractor.

Craft gate:

- `Recipe_CopperWire.asset` already consumes raw copper GUID.
- `FirstHourDirector` generic first-craft gate remains a separate blocker from 1815.

Save/load identity:

- `Data_Copper` ID/hash must remain stable.
- Old and new route saves using `Data_Copper` should resolve to raw copper after duplicate removal.
- No quality tier or UI text may alter save identity.

DataMonolith:

- Any item/catalog binary output becomes stale after asset identity mutation until rebuilt and validated by the data owner.

## GlobalQualityWeight Consequences

Compact:

- Same `Data_Copper` ID, raw descriptor, stack rule, recipe input, quest completion, and save identity. Presentation may use cheaper pickup visuals only.

Middle:

- Same data truth. Normal item proxy/craft UI density.

High:

- Same data truth. Raw copper may get richer material/world prefab presentation through existing raw owner only.

Ultra:

- Visual overkill only: richer ore material, pickup VFX, catalog icon/model polish. No different stable ID, recipe truth, save identity, DTO layout, or route authority.

## Final Scan

No runtime/import/profiler/player proof is claimed.  
No deletion is recommended without reference proof and rollback.  
No asset/source/scene/prefab/source-data/generated CSV/binary/task file was edited by this agent.  
Future patch must not claim acceptance until Unity validation, DataMonolith gate, and route smoke proof exist.

## Final State

PATCH_PACKET_COMPLETE.  
Runtime/editor/import/DataMonolith acceptance remains PENDING VERIFICATION.
