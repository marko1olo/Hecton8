# 1849 AppliedLore Internal Surface Gate

Status: STATIC SOURCE PASS

## Problem

Internal production packets were baked and exported as full player/public surfaces because `Tools/AppliedLoreImporter.py` forced every packet to `surface_mask=127`.

High-risk examples:
- `P196_RESOURCE_TABLE_PLACEHOLDER_CONTRACT`
- `P200_NATIVE_LOCALIZATION_PASS_CONTRACT`
- `P261_RESOURCE_YIELD_AUTHORING_ROWS`
- `P316`-`P320` placement priority locks
- `P446`-`P450` Unity placement scene briefs
- `P451`-`P455` localization/accessibility QA briefs

Those records are useful controller/production notes, but they are not believable in-game encyclopedia, scanner, terminal, audio, or public site prose.

## Changes

- Added packet-level `surface_mask` support to `Tools/AppliedLoreImporter.py`.
- Marked five internal release sets as `surface_mask=65` (`Title + FieldNote` only):
  - `RS040_NUMERIC_TUNING_SOURCE_RULES`
  - `RS053_NUMERIC_AUTHORING_BRIDGE_SURFACES`
  - `RS064_UNITY_PLACEMENT_PRIORITY_BACKLOG`
  - `RS090_UNITY_PLACEMENT_SCENE_BRIEFS`
  - `RS091_NATIVE_LOCALIZATION_AND_ACCESSIBILITY_QA_BRIEFS`
- Updated `Tools/AppliedLorePageExporter.py` to:
  - export only enabled publication surfaces;
  - omit disabled packet ids from localized indexes;
  - remove stale generated pages for disabled surfaces only when frontmatter proves `source: AppliedContent packet JSON`.
- Updated `Tools/AppliedLoreRuntimeAudit.py` to treat disabled generated wiki/site pages as failures and validate publication indexes against surface masks.
- Updated `H8StaticDataArena.TryGetAppliedLoreUtf8` so runtime reads fail when the requested surface bit is disabled in `SurfaceMask`.

## Verification

Commands:

```powershell
python -m py_compile Tools/AppliedLoreImporter.py Tools/AppliedLorePageExporter.py Tools/AppliedLoreRuntimeAudit.py
git diff --check -- Tools/AppliedLoreImporter.py Tools/AppliedLorePageExporter.py Tools/AppliedLoreRuntimeAudit.py Assets/_Project/Scripts/Data/Monolith/H8StaticDataArena.cs Docs/Lore/AppliedContent/packets/RS040_NUMERIC_TUNING_SOURCE_RULES.packets.json Docs/Lore/AppliedContent/packets/RS053_NUMERIC_AUTHORING_BRIDGE_SURFACES.packets.json Docs/Lore/AppliedContent/packets/RS064_UNITY_PLACEMENT_PRIORITY_BACKLOG.packets.json Docs/Lore/AppliedContent/packets/RS090_UNITY_PLACEMENT_SCENE_BRIEFS.packets.json Docs/Lore/AppliedContent/packets/RS091_NATIVE_LOCALIZATION_AND_ACCESSIBILITY_QA_BRIEFS.packets.json
python Tools/AppliedLoreImporter.py --root .
python Tools/AppliedLorePageExporter.py --root .
python Tools/AppliedLoreRouteCardExporter.py --root .
python Tools/AppliedLoreRuntimeAudit.py --root . --source-only
```

Results:

```text
applied_lore_packets=460 localized_rows=6900 draft_localization_rows=5561
applied_lore_pages_written=0 skipped_existing=13050 removed_disabled=750 index_pages_written=30
applied_lore_route_cards=454
AppliedLore source audit OK: packets=460 locales=15 rows=6900 wiki_pages=6525 site_pages=6525 publication_frontmatter_pages=13050 publication_surface_rows=13050 publication_cluster_rows=150
```

Runtime bake was not claimed. Unity was still occupied by the active editor/refactor verification worker and shader/compiler processes.
