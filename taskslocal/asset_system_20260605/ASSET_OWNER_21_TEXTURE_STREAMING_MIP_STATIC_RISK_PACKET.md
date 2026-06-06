# Asset Owner 21 - Texture Streaming/Mip Static Risk Packet

Status: `PENDING_VERIFICATION`.
Scope: future owner packet for texture streaming mips, mip pressure, hero-scale source rows, large source rows, and static sRGB/name mismatch risks.
Evidence class: `STATIC_DOC`, `STATIC_SOURCE`, `STATIC_META_SCAN`, `STATIC_IMAGE_PROBE` only.

No Unity run, import, material edit, prefab edit, scene save, Addressables build, profiler capture, Memory Profiler capture, Frame Debugger capture, screenshot capture, runtime test, or `Assets/` mutation is covered by this packet.

## Mandates Followed

- `STRM_Async_Asset_Upload_Texture_Settings`
- `QA_Evidence_Text_Filter_Audit`
- `Docs/Reports/Batch32/CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md`

## Evidence Boundary

Static source:

- `Docs/AssetAudit/TEXTURE_FILE_TECHNICAL_PROPERTIES_20260605.md`
- `Docs/AssetAudit/TEXTURE_FILE_TECHNICAL_PROPERTIES_20260605.csv`
- `Docs/AssetAudit/TEXTURE_IMPORT_ROLE_MATRIX_20260605.md`
- `taskslocal/asset_system_20260605/ASSET_OWNER_09_TEXTURE_MATERIAL_IMPORT_BLOCKERS_PACKET.md`

Hard boundary:

- CSV rows and `.meta` text are not Unity importer readback.
- Source byte size is not resident texture memory.
- Pixel dimensions are not route visual quality.
- Static image probe is not material, shader, mip, or scene proof.
- This packet is routing evidence only. Future import/material/scene work must use Unity importer/API routes, not raw YAML mutation.
- Mip/streaming downgrades must be judged against the mandatory visual-reference digest for bright surface, sky/Aegir, shoreline, photic shallows, flora/coral, UI/cockpit, and medium-depth contexts. Memory relief that makes those contexts flat, blurry, muddy, or primitive is rejected.

## Static Row Map

All counts below are from `Docs/AssetAudit/TEXTURE_FILE_TECHNICAL_PROPERTIES_20260605.csv`.

Latest role-ledger guard:

- `python -B Tools\ValidateTextureRoleTechnicalLedger.py --no-fail` returns `TEXTURE_ROLE_TECHNICAL_LEDGER_REJECTED blockers=3 rows=13 exact_asset_rows=2 directory_rows=4 docs_source_only_rows=7 ledger_matches=136`.
- `flora_coral:albedo` has no matching static ledger row with `streaming_mips=1` under `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported`.
- `flora_coral:normal_detail_mask` has no matching static ledger row with `streaming_mips=1` under the same imported flora/coral source scope.
- `ui_oxygen_mask:mask` maps to `Assets/_Project/Art/Sprites/oxygen-tank.png`; the role matrix expects linear/mask treatment, while static importer meta reports `sRGB=1`.
- This is owner routing evidence only. Do not raw-edit `.meta`; future repair must use Unity importer/API readback and HUD/material proof.

| Risk selector | Rows | Static split | Future blocker |
|---|---:|---|---|
| `policy_flags` contains `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` | 81 | `sky_aegir_cloud` 14, `terrain_geology` 1, `texture_source` 66 | World/sky/flora/detail sources may stream poorly or not shed mips under pressure. Requires importer readback, route ownership, material user readback, and compact VRAM proof before any route claim. |
| `policy_flags` contains `HERO_SCALE_PIXELS` | 12 | `sky_aegir_cloud` 11, `terrain_geology` 1 | Hero-scale rows can dominate upload and residency pressure. Requires visual-floor screenshots and mip residency proof before keeping high source scale in visible routes. |
| `policy_flags` contains `SOURCE_GT8MB` | 11 | `sky_aegir_cloud` 2, `terrain_geology` 3, `texture_source` 6 | Large source files can create import, compression, and upload risk. Requires imported format/size readback and Memory Profiler evidence before route use. |
| `policy_flags` contains `STATIC_META_SRGB_RISK_FOR_NONCOLOR_NAME` | 2 | `sky_aegir_cloud` 1, `texture_source` 1 | Names imply normal/non-color data while static meta says sRGB. Requires role proof and Unity importer readback before binding. |

The exact 81-row streaming-mip risk set is the CSV selector above. Do not hand-enter paths into import tooling; filter the CSV at execution time and then verify every generated target path against Unity importer readback.

## Hero-Scale Rows

| Class | Path | Static size | Static flags |
|---|---|---:|---|
| `sky_aegir_cloud` | `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/Aegir_storms.png` | 4096x2048, 0.340 MB | `HERO_SCALE_PIXELS`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` |
| `sky_aegir_cloud` | `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds.png` | 4096x2048, 2.873 MB | `HERO_SCALE_PIXELS`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` |
| `sky_aegir_cloud` | `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds0_diff.png` | 4096x2048, 10.482 MB | `HERO_SCALE_PIXELS`, `SOURCE_GT8MB`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` |
| `sky_aegir_cloud` | `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/clouds0_diff.png` | 4096x2048, 7.011 MB | `HERO_SCALE_PIXELS`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` |
| `sky_aegir_cloud` | `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_bump.png` | 4096x2048, 0.455 MB | `HERO_SCALE_PIXELS`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` |
| `sky_aegir_cloud` | `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_diff.png` | 4096x2048, 1.293 MB | `HERO_SCALE_PIXELS`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` |
| `sky_aegir_cloud` | `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_norm.png` | 4096x2048, 1.025 MB | `HERO_SCALE_PIXELS`, `STATIC_META_SRGB_RISK_FOR_NONCOLOR_NAME`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` |
| `sky_aegir_cloud` | `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_spec.png` | 4096x2048, 0.176 MB | `HERO_SCALE_PIXELS`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` |
| `sky_aegir_cloud` | `Assets/_Project/Art/TEXTURES/Aegir_storms.png` | 4096x2048, 0.340 MB | `HERO_SCALE_PIXELS`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` |
| `sky_aegir_cloud` | `Assets/_Project/Art/TEXTURES/clouds.png` | 4096x2048, 2.873 MB | `HERO_SCALE_PIXELS` |
| `sky_aegir_cloud` | `Assets/_Project/Art/TEXTURES/clouds0_diff.png` | 4096x2048, 10.482 MB | `HERO_SCALE_PIXELS`, `SOURCE_GT8MB`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` |
| `terrain_geology` | `Assets/_Project/Art/Models/Rocks/Rock 7/Materials/2.jpg` | 4000x4000, 5.009 MB | `HERO_SCALE_PIXELS` |

## Large Source Rows Over 8 MB

| Class | Path | Static size | Static flags |
|---|---|---:|---|
| `sky_aegir_cloud` | `Assets/_Project/Art/TEXTURES/clouds0_diff.png` | 4096x2048, 10.482 MB | `HERO_SCALE_PIXELS`, `SOURCE_GT8MB`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` |
| `sky_aegir_cloud` | `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds0_diff.png` | 4096x2048, 10.482 MB | `HERO_SCALE_PIXELS`, `SOURCE_GT8MB`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` |
| `texture_source` | `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.low/detail___family.coral.low.png` | 2048x2048, 10.208 MB | `SOURCE_GT8MB`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` |
| `texture_source` | `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.plate/detail___family.coral.plate.png` | 2048x2048, 9.862 MB | `SOURCE_GT8MB`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` |
| `terrain_geology` | `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock037_2K-JPG_NormalGL.jpg` | 2048x2048, 9.690 MB | `SOURCE_GT8MB` |
| `terrain_geology` | `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/TOP1Rock028_2K-JPG_NormalGL.jpg` | 2048x2048, 9.572 MB | `SOURCE_GT8MB` |
| `texture_source` | `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.kelp.canopy/detail___family.kelp.canopy.png` | 2048x2048, 9.220 MB | `SOURCE_GT8MB`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` |
| `texture_source` | `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.branching/detail___family.coral.branching.png` | 2048x2048, 9.108 MB | `SOURCE_GT8MB`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` |
| `terrain_geology` | `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock012_2K-JPG_NormalGL.jpg` | 2048x2048, 8.798 MB | `SOURCE_GT8MB` |
| `texture_source` | `Assets/_Project/Art/TEXTURES/Sky/eb2.png` | 2048x2048, 8.789 MB | `SOURCE_GT8MB`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` |
| `texture_source` | `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/family.coral.low/albedo___family.coral.low.png` | 2048x2048, 8.610 MB | `SOURCE_GT8MB`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` |

## Static sRGB Name-Risk Rows

| Class | Path | Static size | Static flags | Required future decision |
|---|---|---:|---|---|
| `sky_aegir_cloud` | `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/surface_norm.png` | 4096x2048, 1.025 MB | `HERO_SCALE_PIXELS`, `STATIC_META_SRGB_RISK_FOR_NONCOLOR_NAME`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` | Prove whether this is a normal/non-color map or a color-authored surface layer. If non-color, Unity importer must read back linear/non-sRGB and role-correct type before binding. |
| `texture_source` | `Assets/_Project/Art/TEXTURES/Detali/soft_plume_noise_-_kakoy_to_seryy_nu_norm.png` | 1024x1024, 1.737 MB | `STATIC_META_SRGB_RISK_FOR_NONCOLOR_NAME`, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK` | Prove role. If normal/noise data, Unity importer must read back linear/non-sRGB and shader channel contract before binding. |

## Owner Execution Order

1. Re-open the CSV and filter the four selectors in this packet. Do not use broad filename guesses.
2. Split ownership by route family: sky/Aegir/cloud, terrain/geology, flora/coral/kelp, utility/detail/visor/weather.
3. For each row, name route moment, material target, import role, Addressables/group owner, and rollback trigger before editing imports.
4. Use Unity importer/API routes for texture changes. Do not raw-patch `.meta`, `.mat`, `.prefab`, `.unity`, or `.asset` text.
5. Read back importer settings for every touched asset: sRGB, texture type, normal handling, compression, mipmaps, streaming mips, max size, platform overrides, and read/write.
6. Read back material users and active scene renderers before any route statement: sky/Aegir material, terrain/geology material, flora/coral material, detail/visor/weather material, and any Addressables owner route.
7. Capture memory, upload, render, and screenshot evidence after import/material changes. Static docs do not upgrade the claim.

## Import Readback Gates

Required future evidence per changed texture:

- Unity importer readback for texture type, sRGB, mipmaps, streaming mips, max size, compression, platform overrides, read/write, and alpha state.
- Role readback against `TEXTURE_IMPORT_ROLE_MATRIX_20260605.md`: color/albedo, normal, MRAO/mask/detail, UI sprite, celestial/cloud/storm, or source-only.
- Material slot readback for every active route user.
- Addressables owner readback for heavy world/hero-route dependencies.

Reject the import change if:

- streaming mips remain off on a world/hero visible texture without owner-named proof and compact memory evidence;
- a normal, mask, MRAO, noise, or detail role remains sRGB without a material-authored reason;
- a source-only or rejected generated source is promoted as visible route art without authored material proof;
- max texture size or compression destroys surface, Aegir, waterline, terrain, or organic material readability;
- read/write is enabled without CPU-read owner and lifecycle proof.

## VRAM And Mip Pressure Gates

Future owner must measure, not infer:

- texture memory after scene load;
- total reserved memory after scene load;
- compact 2GB VRAM lane against 1800 MB ceiling and 900 MB texture budget;
- async upload buffer/time slice against the mandate values: Low 64 MB/1 ms, Middle 128 MB/2 ms, High/Ultra 256 MB/4 ms unless measured proof demands a different owner-approved route;
- upload spike behavior for hero-scale and large-source rows;
- mip residency under camera travel across surface, photic shallows, and medium-depth hero route;
- mip downgrade behavior when used/total exceeds the 0.90 graduation trigger.

Reject the route if memory evidence shows the texture budget, compact VRAM ceiling, upload cadence, or route visual floor cannot coexist.

## Screenshot Rejection Gates

Required future captures:

- bright surface exit with sky/Aegir/cloud rows visible where routed;
- coastline/ocean skin and waterline contact with no darkness/fog/post hiding weak art;
- photic shallows with terrain/geology and organic material readability;
- medium-depth hero route with mip transitions observed during movement;
- close and mid camera views for hero-scale Aegir/cloud/geology rows;
- material contact sheets only as support, never as route proof.
- side-by-side digest comparison notes for every route screenshot affected by streaming mip, max-size, compression, or residency changes.

Reject screenshots if:

- surface, sky, Aegir, coastline, ocean skin, photic shallows, or medium-depth hero routes look muddy, flat, blurry, primitive, or hidden by grading/fog;
- hero-scale Aegir/cloud rows resolve as a soft disc, smeared banding, or low-detail sky card;
- flora/coral/kelp rows collapse into noisy flat color or broken alpha silhouettes;
- terrain/geology rows show seams, wrong normal orientation, overcompressed masks, or scale mismatch;
- lower mip behavior creates obvious popping or unreadable material identity.

## Rollback Conditions

Rollback the import/material change and restore the prior asset route if:

- importer readback does not match the intended role;
- material slot readback points to the wrong texture, wrong channel contract, or an unauthorized clone;
- compact memory/VRAM/upload evidence breaches the assigned budget without a better owner-approved route;
- screenshot evidence falls below the surface/shallow/medium-depth visual floor;
- Addressables ownership, release path, or async upload budget is unproven after the change;
- sRGB/linear risk remains unresolved for the two named rows.

Rollback must use versioned Unity/importer-safe routes. Do not raw-edit YAML to repair import or material state.

## Continuous GlobalQualityWeight Consequences

Quality bands below are reporting anchors only. Implementation consumes continuous `GlobalQualityWeight` and must avoid binary quality switches.

- Low/compact: use compressed route-owned maps, shorter high-mip residency, conservative async upload pressure, baked AO/channel packing, stable silhouettes, and readable water/sky/material identity. No proxy texture fallback and no flat art downgrade.
- Middle: keep route-owned PBR stacks, stable streaming mip behavior, controlled residency, dithered LOD, and full route readability with importer/material readback.
- High: spend spare budget on richer detail normals, Aegir/cloud response, geology breakup, organic detail maps, caustic/contact response, and longer high-mip residency after memory proof.
- Ultra: extend hero texture residency, layered material detail, reflections/lighting response, and near-field dressing after measured memory/render evidence. Gameplay truth, save identity, and ownership route do not change.

## Regression Model

- CPU: future import/material work can cause upload stalls or render prep cost. Static packet makes no runtime CPU claim.
- GC: no runtime code touched; allocation proof is absent.
- Memory/VRAM: source size and static `.meta` risk are mapped; resident memory and mip pressure are unproven.
- Cadence: async upload and mip streaming behavior require Unity/player evidence.
- Correctness: wrong streaming, sRGB/linear, texture type, max size, compression, material slot, or Addressables owner can create false visual proof.
- Visual floor: surface, sky, Aegir, coastline, ocean surface, photic shallows, and medium-depth hero routes remain blocked until screenshots and memory evidence exist together.

Final status: `PENDING_VERIFICATION`.

## 2026-06-06 Maxwell Exact Role-Ledger Repair Packet

Evidence class: `STATIC_META / STATIC_LEDGER / NO_UNITY_IMPORT_READBACK`.

`python -B Tools\ValidateTextureRoleTechnicalLedger.py --no-fail` reproduces:

```text
TEXTURE_ROLE_TECHNICAL_LEDGER_REJECTED blockers=3 rows=13 exact_asset_rows=2 directory_rows=4 docs_source_only_rows=7 ledger_matches=136
BLOCKER: flora_coral:albedo:no_matching_streaming_mips_setting:expected=1
BLOCKER: flora_coral:normal_detail_mask:no_matching_streaming_mips_setting:expected=1
BLOCKER: ui_oxygen_mask:mask:srgb_mismatch:Assets/_Project/Art/Sprites/oxygen-tank.png:expected=0:actual=1
```

Exact static state:

- Role matrix rows for `flora_coral/albedo` and `flora_coral/normal_detail_mask` point to `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported` and expect mipmaps plus streaming mips.
- 11 matched flora/coral albedo assets report `meta_mipmaps=1` and `meta_streaming_mips=0`.
- 33 matched flora/coral detail/mask/normal assets report `meta_mipmaps=1` and `meta_streaming_mips=0`.
- `Assets/_Project/Art/Sprites/oxygen-tank.png` reports `textureType=8`, `sRGBTexture=1`, `enableMipMap=0`, `streamingMipmaps=0`.
- The `flora_coral` role row currently covers both coral and kelp families because the source scope is the whole imported flora directory and the validator matches by `albedo`, `detail`, `mask`, and `normal` file-name tokens. A future repair that changes only `family.coral.*` rows will leave the role-ledger validator red.

Exact importer target inventory:

- Albedo streaming targets, all currently `type=0 srgb=1 mips=1 stream=0`: `family.coral.branching.v2/albedo___family.coral.branching.v2.png`; `family.coral.branching/albedo___family.coral.branching.png`; `family.coral.brittle/albedo___family.coral.brittle.png`; `family.coral.low/albedo___family.coral.low.png`; `family.coral.massive.2/albedo___family.coral.massive.2.png`; `family.coral.massive/albedo___family.coral.massive.png`; `family.coral.plate/albedo___family.coral.plate.png`; `family.kelp.abyssal/albedo___family.kelp.abyssal.png`; `family.kelp.canopy/albedo___family.kelp.canopy.png`; `family.kelp.patch.dense/albedo___family.kelp.patch.dense.png`; `family.kelp.tall/albedo___family.kelp.tall.png`.
- Detail/mask/normal streaming targets, all currently `mips=1 stream=0`: `family.coral.branching.v2/detail___family.coral.branching.v2.png`; `family.coral.branching.v2/mask___family.coral.branching.v2.png`; `family.coral.branching.v2/normal___family.coral.branching.v2.png`; `family.coral.branching/detail___family.coral.branching.png`; `family.coral.branching/mask___family.coral.branching.png`; `family.coral.branching/normal___family.coral.branching.png`; `family.coral.brittle/detail___family.coral.brittle.png`; `family.coral.brittle/mask___family.coral.brittle.png`; `family.coral.brittle/normal___family.coral.brittle.png`; `family.coral.low/detail___family.coral.low.png`; `family.coral.low/mask___family.coral.low.png`; `family.coral.low/normal___family.coral.low.png`; `family.coral.massive.2/detail___family.coral.massive.2.png`; `family.coral.massive.2/mask___family.coral.massive.2.png`; `family.coral.massive.2/normal___family.coral.massive.2.png`; `family.coral.massive/detail___family.coral.massive.png`; `family.coral.massive/mask___family.coral.massive.png`; `family.coral.massive/normal___family.coral.massive.png`; `family.coral.plate/detail___family.coral.plate.png`; `family.coral.plate/mask___family.coral.plate.png`; `family.coral.plate/normal___family.coral.plate.png`; `family.kelp.abyssal/detail___family.kelp.abyssal.png`; `family.kelp.abyssal/mask___family.kelp.abyssal.png`; `family.kelp.abyssal/normal___family.kelp.abyssal.png`; `family.kelp.canopy/detail___family.kelp.canopy.png`; `family.kelp.canopy/mask___family.kelp.canopy.png`; `family.kelp.canopy/normal___family.kelp.canopy.png`; `family.kelp.patch.dense/detail___family.kelp.patch.dense.png`; `family.kelp.patch.dense/mask___family.kelp.patch.dense.png`; `family.kelp.patch.dense/normal___family.kelp.patch.dense.png`; `family.kelp.tall/detail___family.kelp.tall.png`; `family.kelp.tall/mask___family.kelp.tall.png`; `family.kelp.tall/normal___family.kelp.tall.png`.
- Full path prefix for every flora target above is `Assets/_Project/Art/TEXTURES/WorldProceduralFlora/Imported/`.

Repair policy:

1. Do not patch `.meta` text.
2. Use Unity `TextureImporter` / `AssetDatabase` routes only.
3. Set `albedo___*.png` to `TextureType=Default`, `sRGB=true`, `mipmapEnabled=true`, `streamingMipmaps=true`.
4. Set `detail___*.png` and `mask___*.png` to `TextureType=Default`, `sRGB=false`, `mipmapEnabled=true`, `streamingMipmaps=true`.
5. Set `normal___*.png` to `TextureType=NormalMap`, `sRGB=false`, `mipmapEnabled=true`, `streamingMipmaps=true`.
6. If `oxygen-tank.png` remains a mask, keep it Sprite/non-mipped/non-streaming and set `sRGB=false`; if the UI owner proves it is a colored icon, reclassify the role row instead.
7. Regenerate `TEXTURE_FILE_TECHNICAL_PROPERTIES_20260605.csv` from post-import Unity state, rerun the role-ledger validator, then update the unit test that currently expects the known rejection.

## 2026-06-06 Ramanujan Texture Importer Repair Clarification

Evidence class: `STATIC_META / STATIC_LEDGER / NO_UNITY_IMPORT_READBACK`.

Ramanujan confirmed the current texture-role rejection is real, not a script false positive.

Unity importer/API repair target remains:

- `WorldProceduralFlora/Imported/**/albedo___*.png`: `TextureType=Default`, `sRGB=true`, `mipmapEnabled=true`, `streamingMipmaps=true`.
- `WorldProceduralFlora/Imported/**/detail___*.png` and `mask___*.png`: `TextureType=Default`, `sRGB=false`, `mipmapEnabled=true`, `streamingMipmaps=true`.
- `WorldProceduralFlora/Imported/**/normal___*.png`: `TextureType=NormalMap`, `sRGB=false`, `mipmapEnabled=true`, `streamingMipmaps=true`.
- `Assets/_Project/Art/Sprites/oxygen-tank.png`: keep as mask/silhouette for now, not as the detailed colored icon; if mask role remains, keep Sprite/non-mipped/non-streaming and set `sRGB=false`.

Do not reclassify `oxygen-tank.png` yet. The separate colored icon candidate is `Assets/_Project/Art/Sprites/ui/OXYGEN.png`; binding/readback proof belongs to the UI owner.
