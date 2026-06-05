# Rationale 1869

Evidence class: STATIC_SOURCE / STATIC_DOC

## Decisions

- Classified `EquipmentPropBaker1715.cs` and `EquipmentPrefabFactory.cs` as support routes, not completed tool source assets. They can produce/assemble hard-surface equipment but do not provide distinct accepted held/world meshes for the 12 named tools.
- Classified `Hecton_ToolDecayLit.shader` and `Hecton_ToolScreenDiegetic.shader` as viable material/display support, not material proof. Concrete texture role paths and screenshots are still required.
- Classified `M_ScannerMarkerQuad.asset` as scanner marker support only. It does not replace the scanner body.
- Marked 11 rows `NEEDS_SOURCE_ASSET` because data/material paths are mostly resolved but no accepted body mesh exists.
- Marked `Tool_Propulsion` `NEEDS_DECISION` because held material resolves to a package-cache URP Lit asset while world material resolves to the project placeholder.
- Did not propose `WorldProceduralProxy` as a replacement route because task forbids it and it is not a handheld tool source route.

## Low / Middle / High / Ultra Consequence

- Low: preserve distinct silhouette, verb read, anchor truth, packed material identity, and cheap `COL_*` proxies. No ugly mode.
- Middle: add labels, grime, straps, vents, seals, residue, and richer pickup readability.
- High: add stronger bevels, glass/wetness, heat wear, material response, and longer near LOD residency.
- Ultra: add secondary detail meshes, micro scratches, cable/spool detail, tiny screws, subtle glow, and sensory overkill only. No gameplay truth, item ID, collider identity, recipe truth, or authority route changes.
