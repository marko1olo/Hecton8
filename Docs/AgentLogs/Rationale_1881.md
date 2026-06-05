# Rationale 1881

Evidence class: STATIC_SOURCE / STATIC_DOC only.

Decisions:

- Classified existing `Mat_Resource_*` files as placeholder material paths, not final material proof, because all texture slots inspected are empty.
- Kept `CopperOre` mapped to `Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset`; no `Data_CopperOre.asset` route was invented.
- Treated terrain/geology textures and generated geology meshes as host/context candidates only. Ore seams, silica shards, sulfur nodules, resin, membrane, and titanium scrap require explicit source maps.
- Treated kelp texture families and BioForge kelp meshes as usable candidate language for `FiberKelp`, but not an accepted harvested pickup package.
- Marked `Item_Titanium` as quarantine-or-canonical-relink only. It must not carry unresolved material GUID `31321ba15b8f8eb4c954353edc038b1d` forward.
- Used continuous `GlobalQualityWeight` material-richness scaling only. Data, collider, recipe, save, and authority truth remain unchanged.

Residual risk:

- Static file existence does not prove import settings, shader compatibility, SRP Batcher state, runtime rendering, or visual quality.
- No screenshots or profiler artifacts exist for this packet because the task forbade Unity execution.
