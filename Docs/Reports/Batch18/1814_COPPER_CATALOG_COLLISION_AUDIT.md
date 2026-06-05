# 1814 Copper Catalog Collision Audit

Agent: 1814 / COPPER_CATALOG_COLLISION_AUDITOR
Proof boundary: STATIC VERIFIED source/data audit only. No Unity Editor, runtime, PlayMode, profiler, GCMonitor, or frame-time claim is made here.

## Verdict

The first-20 copper route has one valid raw-resource owner:

- `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset`
- GUID `7a9f752461931354e865d30b319c0f35`
- `stableId: Data_Copper`
- `isRawResource: 1`
- `category: Material`
- `resourceFamily: ElectronicsMetal`
- `progressionTier: Tier0`
- `maxStack: 32`
- `worldPrefab` GUID `1dbf7c4f900fc7e4b8c2a5bc7b5e17d2`

The colliding asset is legacy or orphaned for the first-20 route:

- `Assets/_Project/Data/Items/Data_Copper.asset`
- GUID `84877e24023afe648a6682f49f11defa`
- `stableId: Data_Copper`
- `isRawResource: 0`
- `resourceFamily: 0`
- `progressionTier: 0`
- `maxStack: 64`
- `worldPrefab: null`

This is a real identity collision because save, quest, catalog, and hash routes key item truth through `stableId` or its hash. Two assets with `stableId: Data_Copper` cannot safely coexist under the project data roots while disagreeing on raw-resource semantics.

## Authority Inputs

Read and applied:

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `quality.md`
- `gameplay.md`
- `inventory.md`
- `tools.md`
- `survival.md`
- `persistence.md`
- `Docs/Reports/Batch18/1803_FIRST20_ROUTE_BLOCKER_MATRIX.md`
- `.agents-skills/DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `.agents-skills/DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `.agents-skills/PROG_Quest_State_Graph_Logic.txt`
- `.agents-skills/TOOL_Designer_Facades_CSV_Binary_Bridge.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

Relevant standards:

- Item identity must be stable and save-safe.
- Runtime inventory/crafting should resolve pre-baked IDs and descriptors, not scene objects.
- Quest collection checks must bind to one unambiguous item identity.
- Static route validation may not claim runtime, profiler, or PlayMode proof.

## Collision Evidence

The duplicate stable ID exists in exactly the route-relevant item assets found by static search:

- `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset:16`
  - `stableId: Data_Copper`
  - raw route semantics at lines 31-35 and prefab at line 53
- `Assets/_Project/Data/Items/Data_Copper.asset:16`
  - `stableId: Data_Copper`
  - non-raw semantics at lines 31-35 and no prefab at line 53

Hash route:

- `Assets/_Project/Scripts/Core/Generated/H8Hashes.cs:359`
  - `DataCopperId = "Data_Copper"`
- `Assets/_Project/Scripts/Core/Generated/H8Hashes.cs:360`
  - `DataCopperHash = 2276338585u`
- `Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs:1880`
  - hashes `"Data_Copper"` for economy copper demand/offer routes.

Quest route:

- Actual quest path is `Assets/_Project/Data/Lore/Quests/Quest_CopperSample.asset`.
- `Quest_CopperSample.asset:30`
  - `completionId: Data_Copper`
- `Assets/_Project/Scripts/Editor/QuestGraphRepairUtility.cs:41-46`
  - repair utility writes `Quest_CopperSample` completion to `"Data_Copper"`.
- `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs:717`
  - serialized default `firstResourceItemId = "Data_Copper"`.
- `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs:809-812`
  - static `DataCopperItemId` and hash.

World/pickup/crafting route:

- `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_CopperVein.asset:41`
  - harvest yield points to raw copper GUID `7a9f752461931354e865d30b319c0f35`.
- `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_CopperOre.prefab:174`
  - pickup item data points to raw copper GUID `7a9f752461931354e865d30b319c0f35`.
- `Assets/_Project/Data/Crafting/Recipes/Recipe_CopperWire.asset:29`
  - ingredient points to raw copper GUID `7a9f752461931354e865d30b319c0f35`.
- `Assets/_Project/Data/Crafting/Recipes/Recipe_BatteryCell.asset:29`
  - ingredient points to raw copper GUID `7a9f752461931354e865d30b319c0f35`.
- `Assets/_Project/Data/Items/ItemCatalog.asset:46`
  - active catalog entry points to raw copper GUID `7a9f752461931354e865d30b319c0f35`.

Legacy GUID edge check:

- Static GUID search found `84877e24023afe648a6682f49f11defa` only in `Assets/_Project/Data/Items/Data_Copper.asset.meta` among the route-relevant data, prefab, and script roots searched.
- No current first-party route reference was found to the legacy root asset.

Raw GUID edge check:

- Static GUID search found `7a9f752461931354e865d30b319c0f35` in catalog, pickup prefab, player prefab starter data, crafting recipes, barter offers, biome resource channels/plans, world harvest templates, and the copper vein node template.
- This makes the raw asset the current route owner.

## Catalog And Validator Risk

`ItemCatalog.asset` currently includes the raw copper asset and does not include the legacy root copper asset. Under the active catalog alone, `Data_Copper` is expected to resolve to raw copper.

That is not sufficient. `ContentSanityValidator.ValidateItemTemplates` scans all `ItemData` assets under `Assets/_Project/Data`, registers each `PersistentId`, and registers each hash. Because both copper assets live under the data root and both expose `PersistentId == "Data_Copper"`, static content validation should treat this as a duplicate identity even if the legacy asset is omitted from `ItemCatalog.asset`.

`ItemCatalog.RebuildLookup` also has ambiguity detection for duplicate string and hash aliases inside the active catalog. If the legacy asset is added later, descriptor resolution becomes order-dependent: raw copper can be displaced by a non-raw item with a different stack size, no world prefab, and different category/family fields.

## Save And Quest Risk

Save/load risk is identity-level, not prefab-level.

`ItemData.PersistentId` returns `stableId` when present. `ItemData.PersistentHashId` hashes that value. `MatchesPersistentHash` accepts the persistent hash. `FirstHourDirector` checks collected inventory and save inventory by the hash of `"Data_Copper"`.

Therefore:

- A saved `Data_Copper` stack cannot encode whether it came from raw copper or the legacy root asset.
- If catalog resolution ever binds `Data_Copper` to the legacy root asset, loaded copper can become non-raw, maxStack 64, no world prefab, and wrong resource family.
- Quest `Quest_CopperSample` can complete from either asset if both are reachable through pickup/inventory paths because completion is hash/id based.
- Crafting currently uses direct raw GUID references, but quest and save synchronization use `Data_Copper` and remain vulnerable to identity collision.

## Localization And UI Risk

Both colliding assets use the same logical item identity while carrying conflicting authoring semantics:

- Legacy root asset presents `legacyItemName: Copper` and non-raw semantics.
- Raw asset presents `legacyItemName: Copper Ore` and raw-resource semantics.
- Both use the same `ITEM_COPPER_NAME` localized name key.

If the legacy root asset enters catalog or UI lookup routes, the UI can present a copper label that does not match stack rules, raw classification, pickup prefab, or crafting semantics. This is a player-facing route clarity issue, not only a data cleanliness issue.

## Data Edit Decision

No `Data_Copper` asset was edited by agent 1814.

Reason:

- The task is an audit/report slot.
- The raw route owner is clear, but the correct fate of the legacy asset is a data-authority decision: rename, quarantine, or delete.
- Manual deletion or rename outside Unity risks `.meta`/GUID churn and serialized reference damage.
- The legacy asset has a differing serialized class identifier (`Hecton.Items.ItemData`) from the raw asset (`Hecton8.Items.ItemData`), so a blind patch could hide a migration issue.

No rollback artifact is required for this agent because no data asset mutation was performed.

## Recommended Fix

Preferred fix path:

1. Keep `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset` as the only `stableId: Data_Copper` owner.
2. Quarantine the root legacy asset by assigning it a distinct identity, for example `Legacy_CopperHullPlate` or `Data_CopperHullPlate_Legacy`.
3. If the legacy asset is still narratively needed, rename its display/localization identity to match hull plating or salvage, not raw copper ore.
4. If the legacy asset is not needed, delete it only through a scoped Unity asset operation or a controlled filesystem deletion that also removes `Data_Copper.asset.meta`.
5. Extend editor validation so duplicate `ItemData.PersistentId` fails across all `Assets/_Project/Data`, not only active catalog entries.
6. Keep `Quest_CopperSample.completionId`, `FirstHourDirector.firstResourceItemId`, `H8Hashes.Items.DataCopperId`, and route bootstrap utilities pointed at `"Data_Copper"` only after the raw asset remains the unique owner.
7. Run content validation in Unity after the data patch. That proof remains pending and must not be claimed by this audit.

Unsafe fix path to reject:

- Do not make `Quest_CopperSample` point to a GUID.
- Do not change route constants from `Data_Copper` to a new raw ID without migration coverage.
- Do not add the legacy root asset to `ItemCatalog.asset`.
- Do not alter recipe ingredient GUIDs away from the raw copper asset.
- Do not delete the legacy asset without deleting its `.meta` and checking serialized references.

## Scoped Future Patch Prompt

Use this only for the data-owner implementation slot:

```text
Patch the copper catalog collision. Keep Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset as the only stableId Data_Copper owner. Rename or quarantine Assets/_Project/Data/Items/Data_Copper.asset to a distinct legacy hull-plating identity without changing the raw GUID 7a9f752461931354e865d30b319c0f35 or active route references. Preserve .meta files on rename, or delete both .asset and .meta only if static GUID search proves no references. After patch, verify no duplicate stableId Data_Copper remains under Assets/_Project/Data and that Quest_CopperSample, ItemCatalog, copper pickup prefab, copper vein node, Recipe_CopperWire, Recipe_BatteryCell, and FirstHourDirector still resolve Data_Copper to the raw asset.
```

## Required Later Verification

Pending verification for a future Unity-capable slot:

- Run editor content sanity validation.
- Confirm `ItemCatalog.FindById("Data_Copper")` resolves raw copper.
- Confirm `ItemCatalog.FindByHash(H8Hashes.Items.DataCopperHash)` resolves raw copper.
- Confirm `Quest_CopperSample` completes from the copper ore pickup.
- Confirm save/load restores the raw copper descriptor and stack rules.
- Confirm `Recipe_CopperWire` and `Recipe_BatteryCell` still consume raw copper.
- Confirm no orphan `.meta` exists if the legacy asset is renamed or deleted.

## Scalability Consequences

Low: route truth stays a single hash/descriptor; no extra runtime lookup or quality-dependent branch is introduced.
Middle: catalog validation catches authoring drift before route testing.
High: richer copper presentation can be attached to the raw asset without changing save identity.
Ultra: optional visual overkill can use the raw world prefab and material variants while preserving the same stable ID.

## Final Classification

STATIC VERIFIED: collision source identified, route owner identified, affected systems mapped, data edit deferred as unsafe for this audit slot.

PENDING VERIFICATION: Unity content validator, PlayMode pickup/quest/craft flow, save/load rehydration, and any actual asset mutation.
