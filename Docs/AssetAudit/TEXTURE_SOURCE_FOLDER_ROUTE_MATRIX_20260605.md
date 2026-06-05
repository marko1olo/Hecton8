# Texture Source Folder Route Matrix - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_SOURCE`, `STATIC_DOC`, `STATIC_IMAGE_QA`.
Scope: folder-level texture source map derived from texture ledger, candidate disposition, material usage, and active-route blocker CSVs.

No Unity run, import edit, material edit, prefab edit, scene save, build, play mode, profiler, Frame Debugger, screenshot proof, or `Assets` mutation was performed.

CSV companion: `Docs/AssetAudit/TEXTURE_SOURCE_FOLDER_ROUTE_MATRIX_20260605.csv`.

## Static Summary

- Folders mapped: `56`.
- Texture ledger rows covered: `190`.
- Total source size from ledger: `548.029` MB.
- Docs/generated source-only rows: `50`.
- Streaming-mips-off rows: `142`.
- Static active-build-scene usage rows: `54`.
- Static visible-route user rows: `70`.
- Proxy/placeholder usage rows: `43`.
- Folder highest-priority distribution: `NONE_STATIC`=16, `P0`=12, `P1`=22, `P2`=6.

## High-Risk Folders

- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching`: rows `4`, classes `flora_coral_fauna:4`, highest `P0`, active `4`, visible `4`, proxy `4`, generated `0`. Flora/coral source folder participates in proxy material contamination; final non-proxy material route and Unity proof required. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.brittle`: rows `4`, classes `flora_coral_fauna:4`, highest `P0`, active `4`, visible `4`, proxy `4`, generated `0`. Flora/coral source folder participates in proxy material contamination; final non-proxy material route and Unity proof required. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.low`: rows `4`, classes `flora_coral_fauna:4`, highest `P0`, active `4`, visible `4`, proxy `4`, generated `0`. Flora/coral source folder participates in proxy material contamination; final non-proxy material route and Unity proof required. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.massive`: rows `4`, classes `flora_coral_fauna:4`, highest `P0`, active `4`, visible `4`, proxy `4`, generated `0`. Flora/coral source folder participates in proxy material contamination; final non-proxy material route and Unity proof required. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.plate`: rows `4`, classes `flora_coral_fauna:4`, highest `P0`, active `4`, visible `4`, proxy `4`, generated `0`. Flora/coral source folder participates in proxy material contamination; final non-proxy material route and Unity proof required. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.patch.dense`: rows `4`, classes `flora_coral_fauna:4`, highest `P0`, active `4`, visible `4`, proxy `4`, generated `0`. Flora/coral source folder participates in proxy material contamination; final non-proxy material route and Unity proof required. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.tall`: rows `4`, classes `flora_coral_fauna:4`, highest `P0`, active `4`, visible `4`, proxy `4`, generated `0`. Flora/coral source folder participates in proxy material contamination; final non-proxy material route and Unity proof required. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Art/TEXTURES`: rows `12`, classes `sky_aegir_cloud:4;terrain_geology:1;unknown:6;water_foam_caustic:1`, highest `P0`, active `4`, visible `4`, proxy `0`, generated `0`. Serialized route users exist; active renderer/material proof and screenshot proof required. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future sky-Aegir owner`.
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora`: rows `4`, classes `flora_coral_fauna:4`, highest `P0`, active `0`, visible `4`, proxy `4`, generated `0`. Flora/coral source folder participates in proxy material contamination; final non-proxy material route and Unity proof required. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.abyssal`: rows `4`, classes `flora_coral_fauna:4`, highest `P0`, active `0`, visible `4`, proxy `4`, generated `0`. Flora/coral source folder participates in proxy material contamination; final non-proxy material route and Unity proof required. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.canopy`: rows `4`, classes `flora_coral_fauna:4`, highest `P0`, active `0`, visible `4`, proxy `4`, generated `0`. Flora/coral source folder participates in proxy material contamination; final non-proxy material route and Unity proof required. Next: `ASSET_OWNER_12_FLORA_PROXY_MATERIAL_REPLACEMENT_PACKET.md`.
- `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)`: rows `12`, classes `terrain_geology:12`, highest `P0`, active `0`, visible `3`, proxy `3`, generated `0`. Proxy/placeholder material route touches this folder; reject visible promotion until final material readback exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Assets/_Project/Art/TEXTURES/Sky`: rows `7`, classes `sky_aegir_cloud:7`, highest `P1`, active `5`, visible `5`, proxy `0`, generated `0`. Serialized route users exist; active renderer/material proof and screenshot proof required. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future sky-Aegir owner`.
- `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET`: rows `5`, classes `sky_aegir_cloud:1;unknown:4`, highest `P1`, active `3`, visible `3`, proxy `0`, generated `0`. Serialized route users exist; active renderer/material proof and screenshot proof required. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future sky-Aegir owner`.
- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt`: rows `3`, classes `terrain_geology:3`, highest `P1`, active `3`, visible `3`, proxy `0`, generated `0`. Serialized route users exist; active renderer/material proof and screenshot proof required. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Assets/_Project/Art/TEXTURES/Terrain Textures/sand +green`: rows `3`, classes `terrain_geology:3`, highest `P1`, active `2`, visible `2`, proxy `0`, generated `0`. Serialized route users exist; active renderer/material proof and screenshot proof required. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Assets/_Project/Art/TEXTURES/Terrain Textures/sand`: rows `2`, classes `terrain_geology:2`, highest `P1`, active `2`, visible `2`, proxy `0`, generated `0`. Serialized route users exist; active renderer/material proof and screenshot proof required. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429`: rows `7`, classes `terrain_geology:7`, highest `P1`, active `0`, visible `0`, proxy `0`, generated `7`. Generated/source-only folder; use as reference only until route-owned import/material proof exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429_FullSourcePrototype`: rows `6`, classes `terrain_geology:6`, highest `P1`, active `0`, visible `0`, proxy `0`, generated `6`. Generated/source-only folder; use as reference only until route-owned import/material proof exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT`: rows `3`, classes `sky_aegir_cloud:3`, highest `P1`, active `0`, visible `0`, proxy `0`, generated `0`. Sky/Aegir/cloud source needs hero material slot readback and bright surface screenshot proof. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future sky-Aegir owner`.
- `Docs/GeneratedAssets/Gemini/Refined`: rows `3`, classes `terrain_geology:3`, highest `P1`, active `0`, visible `0`, proxy `0`, generated `3`. Generated/source-only folder; use as reference only until route-owned import/material proof exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Docs/GeneratedAssets/Gemini/QA`: rows `2`, classes `generated_prototype:1;terrain_geology:1`, highest `P1`, active `0`, visible `0`, proxy `0`, generated `2`. Generated/source-only folder; use as reference only until route-owned import/material proof exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Docs/GeneratedAssets/Gemini`: rows `1`, classes `terrain_geology:1`, highest `P1`, active `0`, visible `0`, proxy `0`, generated `1`. Generated/source-only folder; use as reference only until route-owned import/material proof exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Docs/GeneratedAssets/Gemini/QA/1906`: rows `1`, classes `terrain_geology:1`, highest `P1`, active `0`, visible `0`, proxy `0`, generated `1`. Generated/source-only folder; use as reference only until route-owned import/material proof exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Docs/GeneratedAssets/Gemini/QA/WetBasalt1428`: rows `1`, classes `terrain_geology:1`, highest `P1`, active `0`, visible `0`, proxy `0`, generated `1`. Generated/source-only folder; use as reference only until route-owned import/material proof exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Docs/GeneratedAssets/Gemini/QA/WetBasalt1428/tile_previews`: rows `1`, classes `terrain_geology:1`, highest `P1`, active `0`, visible `0`, proxy `0`, generated `1`. Generated/source-only folder; use as reference only until route-owned import/material proof exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429`: rows `1`, classes `terrain_geology:1`, highest `P1`, active `0`, visible `0`, proxy `0`, generated `1`. Generated/source-only folder; use as reference only until route-owned import/material proof exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429/tile_previews`: rows `1`, classes `terrain_geology:1`, highest `P1`, active `0`, visible `0`, proxy `0`, generated `1`. Generated/source-only folder; use as reference only until route-owned import/material proof exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429Periodic`: rows `1`, classes `terrain_geology:1`, highest `P1`, active `0`, visible `0`, proxy `0`, generated `1`. Generated/source-only folder; use as reference only until route-owned import/material proof exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429Periodic/tile_previews`: rows `1`, classes `terrain_geology:1`, highest `P1`, active `0`, visible `0`, proxy `0`, generated `1`. Generated/source-only folder; use as reference only until route-owned import/material proof exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429PeriodicDarkPreserve`: rows `1`, classes `terrain_geology:1`, highest `P1`, active `0`, visible `0`, proxy `0`, generated `1`. Generated/source-only folder; use as reference only until route-owned import/material proof exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429PeriodicDarkPreserve/tile_previews`: rows `1`, classes `terrain_geology:1`, highest `P1`, active `0`, visible `0`, proxy `0`, generated `1`. Generated/source-only folder; use as reference only until route-owned import/material proof exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429PeriodicMean`: rows `1`, classes `terrain_geology:1`, highest `P1`, active `0`, visible `0`, proxy `0`, generated `1`. Generated/source-only folder; use as reference only until route-owned import/material proof exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429PeriodicMean/tile_previews`: rows `1`, classes `terrain_geology:1`, highest `P1`, active `0`, visible `0`, proxy `0`, generated `1`. Generated/source-only folder; use as reference only until route-owned import/material proof exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticShellSandSubstrate_2102`: rows `7`, classes `terrain_geology:7`, highest `P2`, active `0`, visible `0`, proxy `0`, generated `7`. Generated/source-only folder; use as reference only until route-owned import/material proof exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Docs/GeneratedAssets/Gemini/Audit/Batch21/tile_previews`: rows `2`, classes `generated_prototype:1;terrain_geology:1`, highest `P2`, active `0`, visible `0`, proxy `0`, generated `2`. Generated/source-only folder; use as reference only until route-owned import/material proof exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Docs/GeneratedAssets/Gemini/Outputs/Batch21`: rows `2`, classes `generated_prototype:1;terrain_geology:1`, highest `P2`, active `0`, visible `0`, proxy `0`, generated `2`. Generated/source-only folder; use as reference only until route-owned import/material proof exists. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Assets/_Project/Art/Skyboxes`: rows `3`, classes `sky_aegir_cloud:3`, highest `NONE_STATIC`, active `2`, visible `2`, proxy `0`, generated `0`. Serialized route users exist; active renderer/material proof and screenshot proof required. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future sky-Aegir owner`.
- `Assets/_Project/Art/TEXTURES/Terrain Textures/gravel`: rows `3`, classes `terrain_geology:3`, highest `NONE_STATIC`, active `2`, visible `2`, proxy `0`, generated `0`. Serialized route users exist; active renderer/material proof and screenshot proof required. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- `Assets/_Project/Art/TEXTURES/Terrain Textures/rocks`: rows `3`, classes `terrain_geology:3`, highest `NONE_STATIC`, active `2`, visible `2`, proxy `0`, generated `0`. Serialized route users exist; active renderer/material proof and screenshot proof required. Next: `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.md / future terrain-geology owner`.
- Additional high-risk folders are listed in the CSV: `4` rows not printed here.

## Rejections

- Do not classify folder presence as final art acceptance.
- Do not import source-only generated folders directly as product art.
- Do not promote proxy/placeholder material folders into visible route content.
- Do not raw YAML patch material/import/scene/prefab files from this matrix.
- Do not claim importer settings, material binding, Addressables residency, VRAM, SetPass, or visual quality from this matrix.

## Regression Model

- CPU: static source map only; no runtime CPU change.
- GC: no runtime code changed; no hot-path proof.
- Memory/VRAM: source sizes and streaming-mip risks only; no residency proof.
- SetPass: proxy/material usage risk identified only; no Frame Debugger proof.
- Correctness: source folder ownership is clearer; Unity readback, import proof, and screenshots remain required.

Final status: `PENDING_VERIFICATION`.
