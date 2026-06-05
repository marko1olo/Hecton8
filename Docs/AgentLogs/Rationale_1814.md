# Rationale 1814 - COPPER_CATALOG_COLLISION_AUDITOR

## Decisions
- Initial decision: keep `Data_Copper` assets read-only until the reference graph proves one unambiguous raw-route owner and a reversible migration path.
- Route owner decision: `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset` is the first-20 owner because active catalog, pickup prefab, copper vein yield, recipes, barter, biome resource plans, player starter reference, and editor bootstrap utilities point to GUID `7a9f752461931354e865d30b319c0f35`.
- Mutation decision: do not edit or delete `Assets/_Project/Data/Items/Data_Copper.asset` in agent 1814. The legacy root asset is unreferenced in the searched route graph, but manual data mutation belongs to a scoped data-owner patch with Unity/reference validation and `.meta` handling.
