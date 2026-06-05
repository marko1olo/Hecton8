# 2016 Surface/Photic Material Debt Triage

Status: STATIC TRIAGE ONLY. No Unity, no scene edits, no asset edits, no material edits, no generated texture writes, no runtime/profiler proof.

Agent ID: 2016
Date: 2026-06-04

## Scope

Allowed output scope was limited to `Docs/Reports/Batch20/2016_*` plus concise `Status_2016`, `Rationale_2016`, and `LOG_2016`.

This pass inspected existing authority docs, prior 2011 validator outputs, existing material audit CSVs, and selected `.mat` YAML enough to rank blockers for the Unity owner. It did not run Unity or expensive builds.

## Authorities Read

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `rendering.md`
- `shaders.md`
- `water.md`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

## Static Evidence Summary

- `Docs/Reports/Batch20/2011_AGGREGATE_VISUAL_DEBT_MATRIX.csv:5` reports 356 materials, 65 with issues, 21 with unresolved texture refs, and 50 unresolved refs.
- `Docs/Reports/Batch20/2011_AGGREGATE_VISUAL_DEBT_MATRIX.csv:6` reports 14 surface blocker materials and 31 surface unresolved texture refs.
- `Docs/Reports/Batch20/2011_AGGREGATE_VISUAL_DEBT_MATRIX.csv:7` reports 58 channel-packing candidates and 212.28 MiB estimated savings.
- `Docs/Reports/Batch20/2011_AGGREGATE_VISUAL_DEBT_MATRIX.csv:8` reports 55 materials missing detail maps and only 3 materials with detail.
- `Docs/Reports/Batch20/2011_AGGREGATE_VISUAL_DEBT_MATRIX.csv:21` reports procedural placeholder material files under `WorldRuntime/ProceduralPlaceholders`; local recount found 30 current `.mat` files in that folder.
- `Docs/Reports/Batch20/2014_HANDOFF_BLOCKERS.csv:3` says no current Game View, Scene View, or profiler proof exists.
- `Docs/Reports/Batch20/2014_HANDOFF_BLOCKERS.csv:9` keeps ocean/shoreline/waterline acceptance blocked by missing Frame Debugger/profiler/GC/memory/VRAM proof and partial Crest quarantine fail.
- `Docs/Reports/Batch20/2014_HANDOFF_BLOCKERS.csv:12` keeps material/channel contracts blocking relink/import because unresolved refs and channel gaps remain.

## Top 10 Blocking Materials / Material Families

1. `Art/Materials/Skybox.mat`
   - Block: six skybox face texture refs unresolved.
   - Evidence: `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_unresolved_texture_refs.csv:5`.
   - Why it blocks: surface and sky cannot meet the bright/readable premium floor with unresolved cubemap/face sources.
   - Required proof: resolved first-party sky source refs, source-aware validator result, 360 and cropped sky captures, import settings, and profiler only if shader/render route changes.

2. `Art/Materials/Mat_HectonSky.mat`
   - Block: unresolved `_HighCloudTex` and `_MainCloudAtlas`; primary PBR slots are empty in YAML.
   - Evidence: `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_unresolved_texture_refs.csv:2`; direct YAML GUID refs at `Assets/_Project/Art/Materials/Mat_HectonSky.mat:69` and `:77`; empty slot headers at `:48`, `:52`, and `:88`.
   - Why it blocks: sky/cloud/Aegir visual route cannot rely on missing cloud atlas refs or empty primary material roles.
   - Required proof: resolved cloud atlas refs, authored cloud material role report, long/crop/360 captures, no surface darkening.

3. `Art/Materials/Mat_HectonSky_CloudOverlay.mat`
   - Block: same unresolved cloud refs as main Hecton sky material.
   - Evidence: `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_unresolved_texture_refs.csv:3`; direct YAML GUID refs at `Assets/_Project/Art/Materials/Mat_HectonSky_CloudOverlay.mat:69` and `:77`.
   - Why it blocks: overlay cannot be accepted as surface/sky polish while source refs are unresolved.
   - Required proof: resolved overlay refs, alpha/coverage role proof, sky capture with overlay enabled and no muddy/crushed surface.

4. `Art/Materials/terrain.mat`
   - Block: unresolved `_BaseMap`, `_MainTex`, and `_Rock_Albedo` all point at GUID `47f0a231c050423488e0ff6f7d66f813`; normal/detail/mask roles are empty.
   - Evidence: `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_unresolved_texture_refs.csv:7`; direct YAML refs at `Assets/_Project/Art/Materials/terrain.mat:41`, `:65`, `:81`; empty normal/detail/mask slot headers at `:44`, `:48`, `:68`.
   - Why it blocks: coastline/terrain floor needs wet rock, strata, sediment, and material breakup. Missing terrain refs collapse the surface route.
   - Required proof: resolved terrain texture family, MRAO/normal/detail contract, flat-light and URP-lit previews, coastline captures.

5. `Art/Materials/Mat_TriplanarRock.mat`
   - Block: unresolved `_Rock_Albedo` and empty PBR/detail roles.
   - Evidence: `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_unresolved_texture_refs.csv:4`; direct YAML ref at `Assets/_Project/Art/Materials/Mat_TriplanarRock.mat:85`; empty role headers at `:48`, `:52`, `:72`.
   - Why it blocks: large geology/shoreline triplanar material is a surface and medium-depth hero route dependency.
   - Required proof: resolved triplanar source refs, object scale and projection contract, normal blending proof, coastline/medium-depth rock captures.

6. `Art/Materials/Construction/Mat_LeakWetSheen.mat`
   - Block: unresolved normal refs, no detail route.
   - Evidence: `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_unresolved_texture_refs.csv:8`; direct YAML refs at `Assets/_Project/Art/Materials/Construction/Mat_LeakWetSheen.mat:23`, `:51`, `:55`; empty detail header at `:26`.
   - Why it blocks: wetness/waterline material response cannot be accepted with invalid normal refs.
   - Required proof: resolved wetness normal/mask refs, wet material preview, waterline/shore shallow capture, Frame Debugger/profiler if shader route changes.

7. `Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/.../mat_Rock2.mat`
   - Block: blocker severity surface unresolved refs for base, normal, main, occlusion; no prompt ORM, packed ORM/mask, or detail map.
   - Evidence: `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_surface_unresolved_texture_refs.csv:2`; `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_material_issues.csv:25`; direct YAML invalid GUID refs at `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock2.mat:41`, `:49`, `:69`, `:85`.
   - Why it blocks: this is explicitly classified as surface blocker material. It cannot be migrated or accepted until refs are restored or invalid slots cleared.
   - Required proof: source base/normal/AO restoration or replacement, MRAO packing contract, material preview, shoreline/rock capture.

8. `Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/.../mat_Rock_Shared.mat`
   - Block: same blocker severity unresolved surface refs as `mat_Rock2`.
   - Evidence: `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_surface_unresolved_texture_refs.csv:3`; `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_material_issues.csv:26`; direct YAML invalid GUID refs at `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock_Shared.mat:41`, `:49`, `:69`, `:85`.
   - Why it blocks: shared rock material can contaminate multiple geology instances.
   - Required proof: same as `mat_Rock2`, plus dependency scan proving no production route still binds the broken shared material.

9. `Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat`
   - Block: no prompt ORM slot, legacy mask channel requires review, no detail map slot per audit despite existing source maps; normal slot header exists but `_BumpMap` is empty in YAML.
   - Evidence: `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_material_issues.csv:11`; `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_surface_material_migration_queue.csv:4`; direct YAML slot headers at `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat:47`, `:51`, `:75`, `:79`.
   - Why it blocks: photic coral cannot be accepted on filename/source presence alone. Mask channel meaning and normal/detail binding must be proven.
   - Required proof: shader-specific channel contract, import settings for albedo/normal/mask/detail, material debug views, photic capture.

10. `Materials/WorldRuntime/ProceduralPlaceholders/*_Placeholder.mat`
   - Block: runtime placeholder materials still exist; representative terrain LOD placeholder lacks prompt ORM/packed mask/detail map.
   - Evidence: `Docs/Reports/Batch20/2011_AGGREGATE_VISUAL_DEBT_MATRIX.csv:21`; local current recount found 30 `.mat` files under `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders`; representative row in `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_material_issues.csv:27`; direct YAML empty detail/mask headers at `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/TerrainLod/MAT_family_rock_arch_large_Placeholder.mat:38` and `:58`.
   - Why it blocks: placeholder material routes are not production visual floor proof for surface, photic shallows, or medium-depth hero routes.
   - Required proof: either diagnostic-only exclusion proof or replacement with authored materials, relink report, and captures.

## Secondary Debt To Keep Open

- `Art/Materials/WorldProceduralProxy/MAT_family_kelp_canopy.mat`: medium-priority channel review and detail-map debt. Evidence: `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_material_issues.csv:17`; migration queue row `:10`; direct YAML slot headers at `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_canopy.mat:44`, `:48`, `:72`, `:76`.
- `Art/Materials/WorldProceduralProxy/MAT_family_rock_arch_large.mat`: no prompt ORM, no packed mask, no detail route. Evidence: `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_material_issues.csv:20`; migration queue row `:15`; direct YAML headers at `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_rock_arch_large.mat:34`, `:38`, `:58`.
- Texture import debt: `Docs/Reports/MaterialAudit_SHINOBU_361_baseline_texture_import_issues.csv:2-6` flags data textures with sRGB on and planet normal/bump textures not imported as normal maps.

## Unity Owner Instructions

Do:

- Resolve missing texture GUIDs before any material migration or relink.
- Define shader-specific channel contracts before treating mask/ORM/MRAO/ARM filenames as valid.
- Replace or explicitly exclude placeholder materials from production route use.
- Produce material previews: albedo-only, normal-only, mask-channel, flat lighting, final URP lighting.
- Capture surface, sky, coastline, waterline, photic, and medium-depth hero views after replacement.
- Include Compact/Middle/High/Ultra consequences: Compact may reduce resolution/density only; Middle must retain good material read; High/Ultra add richer normals, detail masks, reflections, caustics, and visual overkill without changing route truth.

Do not:

- Claim readiness from static CSVs.
- Darken the surface to hide material debt.
- Use package/default Lit materials, proxy unlit materials, null refs, or unresolved GUIDs as production art.
- Guess channel order from filenames.
- Mutate vendor packages or Crest materials.
- Run relinks that were previously marked `DO NOT APPLY`.

## Required Closeout Proof

Minimum closeout packet for each replacement:

1. Material path and owner.
2. Source texture paths and GUIDs.
3. Albedo/normal/MRAO/detail/emission role table.
4. Import setting proof: sRGB, texture type, compression, mips, max size, streaming.
5. Shader slot/channel contract.
6. Dependency scan proving old broken material is not production-bound, or proving the old material is fixed.
7. Unity material preview captures.
8. Current Game View and Scene View captures for surface/photic/medium-depth usage.
9. Frame Debugger/profiler/GC/memory/VRAM proof if shader/render route changed.

## Verdict

STATIC REJECTED for surface/photic material readiness. The active Unity owner should not spend time on aesthetic polish until missing refs, placeholder material routes, and channel contracts are closed with proof. Surface/photic visual floor remains blocked.
