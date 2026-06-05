# Texture Material Family Route Matrix - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence classes used: `STATIC_DOC`, `STATIC_SOURCE`, `STATIC_IMAGE_QA`.
Scope: static mapping from texture/material family to first-20 route moment, blocker, proof-adjacent artifact, import-role row, owner next action, rejection rule, and scalability consequence.

No Unity run, import, build, prefab edit, scene save, profiler capture, Frame Debugger capture, Addressables build, material mutation, or asset mutation was performed. This artifact does not certify runtime state, material binding, Addressables residency, VRAM, frame time, or in-game visuals.

CSV companion: `Docs/AssetAudit/TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.csv`.

## Mandates Followed

- `QA_Evidence_Text_Filter_Audit`
- `STRM_Async_Asset_Upload_Texture_Settings`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory`
- `REND_URP_Graphics_HotPath_Optimization_HLOD`

## Inputs Consumed

- `AGENTS.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `Docs/AssetAudit/README.md`
- `Docs/AssetAudit/VISUAL_MESH_ASSET_TAXONOMY_20260605.md` and `.csv`
- `Docs/AssetAudit/TEXTURE_ASSET_STATIC_LEDGER_20260605.csv`
- `Docs/AssetAudit/TEXTURE_CANDIDATE_DISPOSITION_20260605.csv`
- `Docs/AssetAudit/TEXTURE_MATERIAL_USAGE_MAP_20260605.csv`
- `Docs/AssetAudit/TEXTURE_IMPORT_ROLE_MATRIX_20260605.csv`
- `Docs/AssetAudit/VISUAL_ASSET_REVIEW_QUEUE_20260605.csv`
- `Docs/AssetAudit/ASSET_PROOF_ARTIFACT_INDEX_20260605.md` and `.csv`
- `Docs/AssetAudit/TEXTURE_VISUAL_REVIEW_20260605.md`
- `Docs/AssetAudit/TEXTURE_MATERIAL_USAGE_REVIEW_20260605.md`
- `Docs/AssetAudit/SOURCE_PROTOTYPE_CLEANUP_REVIEW_20260605.md`

## Matrix Rules

- `StaticUsers` means serialized/static reachability only. It does not prove active renderer use, shader slot effect, scene lighting, material quality, or player-visible result.
- `ProofArtifact` means proof-adjacent static artifact. Contact sheets and static CSVs can guide owner work; they cannot promote assets into route use.
- `OwnerNextAction` names the import-role matrix rows from `TEXTURE_IMPORT_ROLE_MATRIX_20260605.csv` when a row already exists. Missing rows mean the owner must assign a route family before material use.
- Rejection rules are intentionally stronger than dispositions. A static candidate can still be rejected for route use if material binding, import role, screenshot, Frame Debugger, Stats, memory, or Addressables proof is absent.

## Route Family Risks

### Foam / Contact

`foam.png` is the first visual blocker because it is visually rejected and statically reachable through active world/ocean route evidence. The cleanup source direction is better, but it remains source material. Any waterline route must prove Crest/ocean slots, role-correct imports, bright shoreline screenshot, and render stats. A cheap foam fallback is a direct violation of the surface visual floor.

Import-role rows: `foam_contact` `albedo`, `normal`, `mrao_mask`, `rgba_contact_mask`.

### Sky / Aegir / Cloud

The existing baked Aegir disc reads too soft for hero use, while stronger cloud and storm sources still need shader-slot and channel response proof. Static references span `02_HECTON_WORLD` and `01_ORBIT`; orbit refs must not inflate the main route. Surface sky must stay bright, readable, and premium. Darkness, stale slots, or blob storm masks are rejection triggers.

Import-role rows: `aegir_cloud` `band_albedo`, `storm_mask_rgba`, `detail`.

### Terrain / Geology

Wet basalt, shell/sand, and terrain sources exist and some are materially reachable through `02_HECTON_WORLD`. The risk is uncontrolled source mixing: baked lighting, repeated ridges, random scanned tiles, and muddy broad terrain. The route needs cleaned PBR stacks, channel proof, terrain/material readback, tile seam proof, and a bright route screenshot.

Import-role rows: `wet_basalt_shell_sand` `albedo`, `normal_mrao`.

### Flora / Coral Imported Stacks

`WorldProceduralFlora/Imported` has plausible albedo/detail/mask/normal stacks, but material and import proof are still blockers. Static evidence also shows `WorldProceduralProxy` material contamination reaching the active world scene. Flora/coral promotion requires final non-proxy material binding, streaming mip/import proof, alpha/dither path, LOD/silhouette proof, and screenshot proof.

Import-role rows: `flora_coral` `albedo`, `normal_detail_mask`.

### UI Oxygen

`Assets/_Project/Art/Sprites/ui/OXYGEN.png` is the detailed source candidate. `Assets/_Project/Art/Sprites/oxygen-tank.png` reads as a black silhouette/mask in static image review. HUD route use needs sprite import, atlas, binding, and compact-scale screenshot proof. The mask cannot become the colored final oxygen icon.

Import-role rows: `ui_oxygen` `icon_albedo`; `ui_oxygen_mask` `mask`.

### Generated Source-Only Packs

Generated foam/Aegir cleanup outputs and Batch31/Gemini terrain packs are source/reference material only. Direct import would bypass material ownership, channel semantics, compression decisions, shader response, route screenshot proof, and memory proof. Use them as authoring reference, then produce route-owned maps through the import matrix.

### Unassigned Sources

Unknown/useful textures such as floor panels, mineral seep masks, plume noise, `ORGANIC.png`, and prologue planet maps remain unassigned. They need owner, material role, import row, target route, and proof packet before any route use. Random promotion is rejected even if the source looks useful in a contact sheet.

### Proxy / Placeholder Materials

`WorldProceduralProxy` and `WorldRuntime/ProceduralPlaceholders` are rejection surfaces for visible route placement. They are not low-tier substitutes. If they appear in route screenshots or active route renderers, the owner must replace them with final route-owned material families or prove a non-proxy binding.

## Scalability Consequences

- Low: preserve bright surface, waterline breakup, premium Aegir read, wet terrain identity, organic silhouettes, and HUD readability. Reduce density/residency smoothly; never replace route art with proxy or flat fallback textures.
- Middle: require route-owned material stacks, import-role proof, stable LOD/dither paths, and material slot readback before visible placement.
- High: spend saved budget on wet-edge detail, richer Aegir/cloud response, geology breakup, flora detail maps, and longer LOD residency after proof.
- Ultra: add layered atmosphere, material overdetail, denser near-field dressing, and richer shoreline response only after render and memory proof. Gameplay truth and asset ownership route do not change.

## Regression Model

- CPU: no runtime code changed. Future owner work must prove renderer/material/Addressables CPU cost.
- GC: no runtime code changed. No hot-path allocation claim is made.
- Memory/VRAM: static size/reachability and source status only. Texture residency, streaming mips, compression, and release ledgers remain proof-blocked.
- Cadence: no runtime cadence changed.
- Correctness: false promotion risk is reduced by mapping each family to route moment, blocker, proof artifact, import row, and rejection rule.

Final status: `PENDING_VERIFICATION`.
