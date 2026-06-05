# Rationale 1826

Evidence class: STATIC_SOURCE / STATIC_DOC only.

## Decisions

1. Raw owner selection: `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset` remains the only valid `stableId: Data_Copper` owner because active catalog, pickup prefab, copper vein, Copper Wire recipe, Battery Cell recipe, biome resources, barter offers, player prefab data, and editor bootstrap paths point to raw GUID `7a9f752461931354e865d30b319c0f35`.

2. Legacy handling recommendation: quarantine/rename `Assets/_Project/Data/Items/Data_Copper.asset` instead of immediate deletion. Static active search found legacy GUID `84877e24023afe648a6682f49f11defa` only in its `.meta`, but preserving the GUID keeps migration evidence and avoids deletion risk until Unity validation proves no hidden consumers.

3. Legacy rename must change both `stableId` and `m_Name`. `ItemCatalog.RebuildLookup` adds persistent ID and asset name aliases; leaving `m_Name: Data_Copper` would remain a future catalog alias collision even if `stableId` changes.

4. Quest and route constants must stay `Data_Copper`. Changing them would create save/quest migration risk. The fix is one owner for the existing stable ID, not a new raw ID.

5. DataMonolith remains stale after any future asset mutation until the approved owner rebakes/import-validates `static_data.h8bin`. This packet cannot claim runtime readiness from static YAML.
