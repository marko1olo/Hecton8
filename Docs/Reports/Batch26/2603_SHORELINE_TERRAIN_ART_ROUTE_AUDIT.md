# 2603 Shoreline / Terrain Art Route Audit

Date: 2026-06-04  
Worker: Batch26 Worker 2603  
Mode: static/read-only audit. No Unity Editor slot, no dotnet build, no Play Mode, no process kills.  
Write scope: this report only.

## Verdict

`1474` remains `REJECTED VISUAL PROOF`.

`shoreline_close_1m` is not close shoreline proof. It is a distant/coast surface read with no verified 1 m camera distance, no organic contact foam proof, no wet rock/sand/shell transition proof, and no material scale cue. The required shoreline close view is explicitly "1 m waterline, organic foam contact, wet rock transition, material breakup, scale cue" in `Docs/Reports/Batch25/2505_VISUAL_PROOF_WATCHDOG_GATE.md:101-107`.

The active route is not failing because the taste floor is ambiguous. It is failing because the visible terrain/material route is under-authored for the required surface/photic quality floor, and because rejected/generated texture sources are still not production-ready material families.

## Authority Loaded

Root/domain authority:

- `AGENTS.md`
- `VISION_LOCKS.md`
- `terrain.md`
- `water.md`
- `rendering.md`
- `TASTE.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `3DMODEL_GEOLOGY_ROCKS.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `Docs/PROCEDURAL_ASSET_PIPELINE.md`
- `Docs/Reports/Batch25/BATCH25_SYNTHESIS_FOR_UNITY_OWNER.md`

Relevant mandate registry:

- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`

Relevant visual law:

- `VISION_LOCKS.md:31-38` sets Subnautica-level as the floor for surface, coast, exposed rock, ocean skin, sky, Aegir, moons, and photic shallows.
- `VISION_LOCKS.md:40-46` forbids darkness as a way to hide bad terrain, weak textures, empty water, or unfinished celestial art.
- `TASTE.md:103-128` requires bright, legible, detailed surface/coast/shallows, with wetness, strata, sediment, scale, material breakup, waterline detail, and real ocean readability.
- `terrain.md:38-42` requires surface/photic terrain to read wet, bright, geologically shaped, and materially detailed, with foam contact and mineral breakup.
- `water.md:30-41` requires beautiful, bright, readable surface and shallow water with terrain readable through shallows, foam, refraction, caustic hints, and waterline wetness.
- `rendering.md:12-20` forbids default noir grade on surface/coast/sky/Aegir and requires readable terrain material and Subnautica-level captures.

## Packet Evidence

Current 1474 files in `Docs/Screenshots/MCP`:

| File | Last write | SHA256 |
|---|---:|---|
| `h8_1474_surface_coast_aegir_ui_off.png` | 2026-06-04 19:25:09 | `047F62921B4024DB7F064808EE4C321C195C468782FB4081ECE9EADEEC9624A2` |
| `h8_1474_shoreline_close_1m.png` | 2026-06-04 19:25:14 | `E94584D4C360865B44E47C6F735C796C494A42B41130D12F1952F618D3654A63` |
| `h8_1474_underwater_0_5m.png` | 2026-06-04 19:25:18 | `ED187CC7E54D7FFF83FE705DFC434DE3AFA2EC9EF350972A555200975C8677D2` |
| `h8_1474_underwater_20_50m_route.png` | 2026-06-04 19:25:22 | `677AD0764F86DE55B6FAC108EC1B56A34AEC1A68B71BC3A1B3746225E8D3F36F` |
| `h8_1474_aegir_celestial_long.png` | 2026-06-04 19:25:27 | `993AE5E551F2038B12A9908AE0B5D157B470336660DE1E2392657FF9339BCEFB` |
| `h8_1474_regression_low_oblique.png` | 2026-06-04 19:25:31 | `47029C89FDCD53794848960C64DC6660FC4A65D4B1ECB70A5AE6603C5B53B742` |

No `h8_1475*` files were found in `Docs/Screenshots/MCP`.

Batch25 already blocks promotion:

- `Docs/Reports/Batch25/BATCH25_SYNTHESIS_FOR_UNITY_OWNER.md:6-16` says no visual acceptance is possible: no `1475` six-view packet, latest evidence is reject-only `1474`, route proof is contaminated, and underwater/foam/caustic quality remains unproven.
- `Docs/Reports/Batch25/BATCH25_SYNTHESIS_FOR_UNITY_OWNER.md:43-51` requires a full visual packet: surface/coast/Aegir, shoreline close foam/wet contact, underwater 0-5 m, underwater 20-50 m route, Aegir/celestial long, low-oblique regression, manifest, and stable log tail.
- `Docs/Reports/Batch25/2505_VISUAL_PROOF_WATCHDOG_GATE.md:74-87` explicitly rejects current 1474 diagnostics for missing views, missing metadata, faulted log tail, slab/cut risk, no caustic proof, and empty underwater route.
- `Docs/Reports/Batch25/2505_VISUAL_PROOF_WATCHDOG_GATE.md:116-125` defines `FALSE_LABEL`, `MISSING_FOAM`, `EMPTY_UNDERWATER`, `SLAB_HARD_CUT`, `RUNTIME_WARNING`, and `AEGIR_SKY_SHORE_FAIL`. The 1474 close/underwater names do not satisfy their labels visually.

## Why The Route Reads Shell / Black / Weak

### 1. Active photic terrain is materially under-authored

`H8_PhoticRouteTerrain_1464` is active and rendered in the current scene:

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:57861-57866` names the object and marks it active.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:57883-57889` shows its `MeshRenderer` enabled.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:57906-57907` binds material GUID `bdbb2649ef167e74c9bc048ac189dd2c`, mapped to `Assets/_Project/Art/Materials/World/Photic1464/MAT_H8_PhoticRouteTerrain_1464.mat`.

That material is not a complete shoreline terrain material family:

- `MAT_H8_PhoticRouteTerrain_1464.mat:25-35` has only `_BaseMap` plus scalar controls. No normal, mask/MRAO, wetness mask, shell/sand blend, or contact-breakup texture stack is serialized there.
- `MAT_H8_PhoticRouteTerrain_1464.mat:32-40` relies on `_CausticStrength`, `_FillLight`, `_TextureScale`, `_WetSpec`, and tint colors.
- Its shader `Assets/_Project/Art/Shaders/H8_PhoticTerrainLit_1453.shader:3-13` exposes only base map, tints, caustic strength, texture scale, fill light, and fake wet spec.
- `H8_PhoticTerrainLit_1453.shader:89-99` samples one base map with triplanar projection.
- `H8_PhoticTerrainLit_1453.shader:116-121` adds sine caustics and a fake wet term, then floors the color. This is presentation math over a weak material source, not premium wet rock/sand/shell PBR proof.

The base map used by the active terrain material is the rejected wet basalt source:

- `MAT_H8_PhoticRouteTerrain_1464.mat:26-28` references texture GUID `f423facb87a22fe49b436302764cb854`.
- That GUID maps to `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png`.
- `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_1428_MANIFEST.md:24-33` says the source is `REJECT` as a production tile and forbids direct active basalt TerrainLayer or broad unbroken shoreline terrain use.
- `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_1428_MANIFEST.md:46-52` lists the required family as pending: normal, MRAO, TerrainLayer, and material.
- `Docs/GeneratedAssets/Gemini/QA/WetBasalt1428/GeminiTextureIntakeAudit.md:21-23` records `REJECT`, LR seam `30.780`, TB seam `33.396`, luminance mean `85.971`.

Conclusion: the active terrain route is consuming a source that its own manifest says must not be used as broad active shoreline terrain. This explains the black/shell/repeated weak read.

### 2. The active close-foam candidate is a transparent ribbon, not proof of contact foam

`H8_ORGANIC_SHORELINE_FOAM_FINE_1469` is active and rendered now:

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:73844-73849` names it and marks it active.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:73850-73857` shows its renderer enabled.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:73874-73875` binds material GUID `937eb9eb615657644ae3de1fcf2d76d4`, mapped to `MAT_H8_ShorelineFoamFine_1469.mat`.

Its material route is still a transparent overlay:

- `Assets/_Project/Art/Materials/World/Photic1469/MAT_H8_ShorelineFoamFine_1469.mat:30-35` uses generic `foam.png` for `_BaseMap` and `_MainTex`.
- `MAT_H8_ShorelineFoamFine_1469.mat:41-59` sets alpha/softness/threshold and transparent blend state.
- `MAT_H8_ShorelineFoamFine_1469.mat:61-65` uses foam color and two tiling vectors, not a shoreline contact mask family.
- `Assets/_Project/Art/Shaders/H8_ShorelineFoamRibbon_1428.shader:17-24` declares a transparent unlit ribbon.
- `H8_ShorelineFoamRibbon_1428.shader:92-111` samples flowing texture channels and fades edges, but has no terrain/water depth ownership or verified contact-cause proof.

This can be a useful visual fake after isolation, but it does not by itself prove premium wet rock/sand/shell/contact foam. The close packet still must show actual 1 m contact, wetness transition, material breakup, and scale cue.

### 3. The active Crest foam input is not visible proof

`H8_CREST_FOAM_INPUT_PASS_1464` is active, but it is a simulation input path:

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:38681-38686` marks the object active.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:38694-38707` shows `Crest.RegisterFoamInput` enabled with `_disableRenderer: 1`.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:38708-38715` shows the `MeshRenderer` disabled.

This is correct for a Crest input, but it is not visual proof unless the next packet includes Crest sim/frame proof and a close shoreline capture where foam visibly follows waterline contact.

### 4. The active caustic sheet can reinforce flat/shell reads

`H8_FloorCausticSoft_1443` is active and rendered:

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:64133-64138` marks it active.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:64155-64161` shows renderer enabled.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:64178-64179` binds `MAT_H8_FloorCausticSoft_1443`.

The material and shader are a no-texture additive fake:

- `Assets/_Project/Art/Materials/World/Photic1428/MAT_H8_FloorCausticSoft_1443.mat:25-32` has no texture envs and only scale/sharpness/tint.
- `Assets/_Project/Art/Shaders/H8_FloorCausticSoft_1443.shader:14-26` is transparent additive, `ZWrite Off`, `Cull Off`.
- `H8_FloorCausticSoft_1443.shader:67-76` derives caustics from sine functions only.

`water.md:159-164` rejects caustics that hide geometry or confuse interactables and baked/fake lighting inside albedo. This route may be acceptable as a subtle fake only after receiver/depth proof. Current 1474 does not prove it; Batch24 already called the caustic read a small streak, not broad believable lace (`Docs/Reports/Batch24/2402_UNDERWATER_MATERIAL_RECEIVER_AUDIT.md:239-245`).

### 5. Historical black slab suspects changed, but remain watchlisted

Batch24 identified `H8_DEPTH_LOW_SHELF_1428` as a top dark shelf suspect (`Docs/Reports/Batch24/2401_CURRENT_SCENE_DELTA_UNDERWATER_CUT_AUDIT.md:48-54`). Current scene state differs:

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:10164-10169` still has `H8_DEPTH_LOW_SHELF_1428` active.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:10179-10180` shows the slab transform, scale `{58,1.15,8}`.
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity:10185-10192` shows its `MeshRenderer` now disabled.
- The bound material maps to `Assets/_Project/Art/Materials/MAT_H8WorldAbyssRidge_1428.mat`, whose base color is near-black and has no base texture at `MAT_H8WorldAbyssRidge_1428.mat:27-56`.

Current static evidence should not blame this disabled renderer as the active source. It remains a reject-on-reenable risk because it is a large near-black slab material.

### 6. Ocean/curtain material state still carries plane and black-band risk

Batch25 material audit flags clip and overdrive risk:

- `Docs/Reports/Batch25/2504_CREST_OCEAN_CLIP_FOAM_CAUSTIC_RISK_AUDIT.md:58-69` identifies removed clip keywords and `_ClipSurface/_ClipUnderTerrain` set to `0` as blocker-level terrain/water clipping suspects.
- `2504_CREST_OCEAN_CLIP_FOAM_CAUSTIC_RISK_AUDIT.md:87-88` says darker shallow shadow can create black bands/streaks.
- `2504_CREST_OCEAN_CLIP_FOAM_CAUSTIC_RISK_AUDIT.md:107-109` flags `Ocean_UnderwaterCurtain` losing under-terrain clip and transparency keywords while activating caustics.

Current material text confirms the risk:

- `Assets/Crest/Crest/Materials/Ocean.mat:14-24` has caustics/foam/underwater keywords but no clip keywords.
- `Ocean.mat:102-113` has caustics enabled and `_ClipSurface: 0`, `_ClipUnderTerrain: 0`.
- `Ocean.mat:128-132` has foam enabled and `_FoamScale: 0.044`.
- `Assets/Crest/Crest/Materials/Ocean_UnderwaterCurtain.mat:14-25` has `_CAUSTICS_ON` and `_UNDERWATER_ON`.
- `Ocean_UnderwaterCurtain.mat:50-56` has `_CausticsStrength: 10` and `_ClipSurface: 0`.
- `Ocean_UnderwaterCurtain.mat:92-97` has black grazing/shadow colors and green-white foam colors.
- `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat:14-27` keeps clip/foam/caustics/transparency/underwater keywords, but `:105-116`, `:128-134`, `:159`, and `:180-196` show heavy caustic/foam/wave values that still require proof.

## Generated Asset Queue Status

Ready status: none.

The Gemini queue explicitly prevents production binding from current sources:

- `Docs/GeneratedAssets/Gemini/README_GENERATION_QUEUE_20260604.md:1-4` says static evidence only and do not save generated images into `Assets/**`.
- `README_GENERATION_QUEUE_20260604.md:25-37` names the needed order: wet basalt, shell sand substrate, shore foam/salt contact, caustic mask, algae/biofilm, etc.
- `README_GENERATION_QUEUE_20260604.md:39-44` says stop derivation when source audit is `REJECT` and stop repeated hero forms even if metrics look acceptable.
- `README_GENERATION_QUEUE_20260604.md:71-79` lists all inspected wet basalt, seabed, and shell-sand sources as blocked/rejected and states no inspected source is currently `READY_FOR_DERIVATION`.

Asset status:

| Target | Current status | Evidence | Route consequence |
|---|---|---|---|
| Wet basalt 1428 | `REJECT`, albedo only, already imported into `Assets` | `TX_H8_WetBasaltShoreline_1428_MANIFEST.md:24-33`, `:46-52`; `QA/WetBasalt1428/GeminiTextureIntakeAudit.md:21-23` | May be source/reference or small masked decal only. Must not be broad active shoreline terrain. |
| Wet basalt 1429 | `REJECT`, not imported into `Assets` | `TX_H8_WetBasaltShoreline_1429_MANIFEST.md:18-27`, `:39-49`, `:65-71` | Better reference only; not final PBR source, not seam-fixed. |
| Photic shell/sand B21 | `REJECT` | `Outputs/Batch21/TX_B21_PhoticShellSandSubstrate_..._MANIFEST.md:23-39`, `:40-51` | Useful reference; not direct Unity material/TerrainLayer. |
| Photic seabed B21 | `REJECT` | `Outputs/Batch21/TX_B21_PhoticSeabedSubstrate_..._MANIFEST.md:27-46` | Useful reference; not direct Unity material/TerrainLayer. |
| Batch21 audit | `PASS_STATIC: 0`, `REJECT: 2` | `Docs/GeneratedAssets/Gemini/Audit/Batch21/GeminiTextureIntakeAudit.md:6-10`, `:21-24` | No shell/sand/seabed source is promotable. |

The active scene is therefore missing the required premium material set for shoreline proof: wet basalt PBR, shell/sand/calcite substrate PBR, waterline wetness/contact foam mask, caustic receiver mask, and import/preview proof.

## Import / Staging Risks

Do not solve this by more `Assets/**` churn.

Rules:

- `Docs/Reports/Batch25/BATCH25_SYNTHESIS_FOR_UNITY_OWNER.md:53-59` says do not claim progress from diagnostics, do not write screenshots under `Assets/Screenshots`, do not add dark/green haze, and do not accept numeric foam/caustic boosts without proof.
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md:151-168` says failed texture families must not be saved into production asset routes; rejected bakes may write diagnostic artifacts under docs/quarantine.
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md:173-184` defines correct order: manifest/import settings, source ingest, tile/baked-light rejection, derive normal/AO/roughness/metallic/emission/detail, pack MRAO, import, bind shared material, render neutral/low/grazing previews, validate, then allow mesh generators to reference it.
- `3DMODEL_TEXTURES_MATERIALS.md:120-131` requires offline import settings and material assets, not runtime texture generation or duplicated materials.
- `3DMODEL_TEXTURES_MATERIALS.md:133-155` rejects missing roles/settings and requires role reports, import settings, UV/projection proof, material slot/SRP Batcher proof, and preview captures.
- `PROCEDURAL_ASSET_PIPELINE.md:186-205` rejects temporary art committed as final generated content and requires final generated content to be deterministic, materially credible, optimized, proof-backed, and aligned with taste.

Owner-correct staging plan:

1. Texture/generation owner keeps new Gemini or paintover sources under `Docs/GeneratedAssets/Gemini/Outputs/Batch26/ShorelineTerrain/` with sidecar manifests, SHA256, prompt/source notes, and 2x2/3x3 previews. No `Assets/**` write for sources that have not passed static intake.
2. Static QA owner runs `Tools/GeminiTextureIntakeAudit.py` on each candidate. Any `REJECT` stops derivation. Do not create normal/MRAO from rejected albedo. Do not "periodic edge pin" and call it seam proof; 1429 already proved that failure mode.
3. Material owner promotes only a source that is at least `PASS_STATIC` plus manual 2x2/3x3 visual pass into a complete family: albedo, normal/height-derived normal, packed MRAO/wetness, optional shell/salt/contact mask, optional caustic receiver mask. Use final deterministic names before import.
4. Unity/import owner imports once into a final route path, with platform settings and `.meta` created in a quiet import window. Do not replace `L_Basalt.terrainlayer` or Rock031 GUIDs in place; the 1428 manifest forbids that at `TX_H8_WetBasaltShoreline_1428_MANIFEST.md:37-44`.
5. Scene/material owner binds promoted families through shared `MAT_*` assets or TerrainLayers only. No per-instance material clones, no runtime `renderer.material`, no broad direct use of rejected albedo.
6. Route owner tests one variable at a time: active terrain material, 1469 foam ribbon, Crest foam input, caustic receiver, ocean clip/curtain settings. No combined "everything brighter" packet.
7. Visual proof owner captures the full `1475` packet only after import/compile/log quiet state and writes screenshots outside `Assets/**`.

## Required Art Route

Minimum recoverable route for premium shoreline:

- Wet basalt: corrected source or authored paintover with no repeated hero veins, no baked lighting, no crushed black crevices, normal + MRAO/wetness, triplanar scale proof, and macro breakup mask.
- Shell/sand/calcite substrate: corrected bright photic source with stochastic small/medium shell distribution, no directional bands, normal/height, roughness/AO/wetness, and terrain transition mask.
- Waterline contact: organic foam/salt mask driven by shoreline/contact context. Ribbon overlay can be used only after it visually reads as contact-caused, not a sheet/grid strip.
- Wet transition: exposed rock needs readable wetness gradient, puddle/specular response, mineral/salt staining, sediment accumulation, and small scale witnesses at 1 m.
- Caustics: subtle receiver-specific lace on valid lit receivers. No global sine sheet, no neon, no caustics hiding geometry.
- Terrain composition: macro route silhouette plus meso ledges/shelves/fractures and micro material detail. `terrain.md:29-36` rejects flat planes, smooth sinusoidal hills, random isolated rocks, and resource-dot terrain.

## Proof Requirements

The next acceptable packet must be `1475`, not a renamed 1474 continuation:

- Six same-session screenshots with required names and actual matching views per `Docs/Reports/Batch25/2505_VISUAL_PROOF_WATCHDOG_GATE.md:97-108`.
- `h8_1475_s01_shoreline_close_1m_q060_uioff_*.png` must show actual 1 m waterline with organic foam contact, wet rock transition, material breakup, shell/sand or sediment scale cue, and shallow depth falloff.
- Underwater views must be actual underwater camera states. Surface-looking duplicates trigger `FALSE_LABEL` per `2505_VISUAL_PROOF_WATCHDOG_GATE.md:116-121`.
- Manifest must include paths, sizes, SHA256, local/UTC timestamps, scene, route state, camera transform/FOV/depth band, capture source, UI state, continuous `GlobalQualityWeight`, render scale, post/underwater/foam/caustic/fog states, route harness version, log path, and fault summary.
- Log tail must be newer than final screenshot and clean/stable. `2505_VISUAL_PROOF_WATCHDOG_GATE.md:123-127` blocks acceptance on stale/faulted logs or any single reject code.
- Visual checklist must pass `2505_VISUAL_PROOF_WATCHDOG_GATE.md:163-172`: surface, shoreline, underwater 0-5 m, underwater 20-50 m, Aegir/sky, and low-oblique all pass.

## Low / Middle / High / Ultra Consequences

- Low / compact: no ugly mode. Keep bright/readable ocean, wet rock identity, shell/sand scale cue, and contact foam. Reduce texture resolution, scatter density, decal density, reflection/caustic cadence, and far detail first.
- Middle: use 2048-class key world materials where budget allows, active wetness/contact masks, enough substrate decals and foam breakup to read as genuinely good, not merely functional.
- High: spend budget on richer normal/detail maps, denser near-field geology, better specular wetness, subtle receiver caustics, more foam variation, and stronger sky/Aegir integration without changing route truth.
- Ultra: add local shoreline overkill only after base pass is proven: extra decal layers, higher precision source bakes, richer reflection/caustic/foam layering, and dense close material witnesses. Do not hide unresolved slabs, rejected textures, empty seabed, or false packet labels.

## Final Blocking Statement

Current shoreline/terrain art route is blocked by rejected source textures, incomplete PBR material families, active weak terrain binding, unproven transparent foam overlay, active no-texture caustic sheet risk, and invalid 1474 proof labels. No asset in the inspected Gemini queue is ready for direct production binding. The next owner should stage corrected sources under docs, pass static intake and manual tile review, build complete material families, import once in a quiet owner window, isolate one visual route at a time, then produce the required 1475 six-view packet with manifest and clean log proof.
