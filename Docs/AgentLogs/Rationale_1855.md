# Rationale 1855

Evidence class: STATIC_SOURCE

## Decisions

- Selected mandates: TOOL_Procedural_Wreckage_Generator, OPT_Cinematic_Cheat_Protocol_Visual_Fake_First, OPT_Performance_Budgets_FrameTime_VRAM_Limits, REND_URP_Graphics_HotPath_Optimization_HLOD, STRM_Asset_Lifecycle_Addressables_Loading_Memory, QA_Evidence_Text_Filter_Audit.
  - Reason: the task is a no-mutation construction final mesh rebuild packet requiring offline mesh package rules, LOD/HLOD/collision policy, streaming/VRAM boundaries, and static-only evidence labeling.
- Added hard-surface and equipment root bibles beyond the packet read list.
  - Reason: the requested output specifies pressure modules, pylons, turbines, corridors, service pumps, wreckage, and scrap equipment-like carriers.
- Classified `Assets/ScifiFacility/Models` as the strongest valid non-primitive source tree, with `Assets/ScifiFacility/Prefabs` usable only as packaging/reference candidates.
  - Reason: static inventory found 282 FBX model files and 255 prefabs; only two ScifiFacility prefabs referenced Unity built-in primitive cube meshes and must be sanitized before reuse.
- Classified `WreckagePrefabFactory` as a future wreck/debris assembler candidate, not a direct Construction/Final replacement route.
  - Reason: it requires real hull/debris/COL source groups and material sets; the default baked wreckage source folder had no real files under static inspection.

## Boundaries

- No Unity, builds, importers, bakes, prefab edits, asset edits, source edits, scene edits, binary edits, or `.meta` edits.
- Current proof can only be `STATIC_SOURCE` / `STATIC_DOC`.
