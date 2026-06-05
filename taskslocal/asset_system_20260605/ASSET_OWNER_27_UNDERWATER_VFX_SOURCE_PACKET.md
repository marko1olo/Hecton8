# ASSET_OWNER_27_UNDERWATER_VFX_SOURCE_PACKET

Role: `ASSET_OWNER_27_UNDERWATER_VFX_SOURCE_PACKET_WRITER`
Date: 2026-06-05
Status: `SOURCE_PACKET_ONLY / PENDING GENERATION / PENDING UNITY PROOF`
Evidence class: `STATIC_DOC`
Write scope: future offline source generation/prep for underwater VFX masks only.
Runtime/import state: no images generated, no Unity import, no material binding, no scene/prefab/code mutation.

## Authority Read

- `AGENTS.md`
- `TASTE.md`
- `Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_REJECTION_20260605.md`
- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`
- `Docs/AssetAudit/TEXTURE_AUTHORING_RECIPES_20260605.md`
- `Docs/Reports/Batch31/3106_UNDERWATER_ROUTE_VOLUME_OWNER.md`
- `vfx.md`
- `water.md`
- `rendering.md`
- `performance.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`

Mandates followed:

- `.agents-skills/REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`

## First-20 Route Impact

Removes source blockers for the first bright photic exit and first swim route. The missing masks must support water volume, route depth, surface contact, scale cues, and light readability without hiding weak terrain/water art behind fog, darkness, or full-screen particle noise.

## Non-Negotiable Boundary

This packet is not final art acceptance.

Prototype/source work may live under `Docs/GeneratedAssets/AssetSystem_20260605/UnderwaterVFXSource_YYYYMMDD/` with manifests and contact sheets. Final import into `Assets/_Project/...` is a separate Unity owner task after cleanup, import settings, material role proof, screenshots, Frame Debugger, GC, and texture-memory evidence.

Do not direct-import Gemini, AI, generated, or rough cleanup outputs as final runtime art. Usable Gemini/source material is not automatically rejected because it has a small mark or watermark. Preserve usable source, record cleanup debt, remove marks during source prep, then rebuild final masks/PBR-role maps with proof. Blind destructive cropping that damages tileability, scale, alpha edge quality, or material signal is rejected.

Do not touch `Assets`, `ProjectSettings`, `Packages`, code, scenes, prefabs, or materials from this packet.

## Required Source Pack Outputs

Future owner must generate/prep these four source families:

1. Fish silhouette card atlas.
2. Sparse marine snow/mote atlas.
3. Foam/contact ring mask.
4. Shallow beam/caustic mask.

Recommended source folder:

`Docs/GeneratedAssets/AssetSystem_20260605/UnderwaterVFXSource_YYYYMMDD/`

Required generated files:

- `TX_H8_UnderwaterFishSilhouette_CardAtlasRGBA_Source_YYYYMMDD.png`
- `TX_H8_UnderwaterFishSilhouette_ChannelPreview_Source_YYYYMMDD.png`
- `TX_H8_MarineSnow_MoteAtlasRGBA_Source_YYYYMMDD.png`
- `TX_H8_MarineSnow_ChannelPreview_Source_YYYYMMDD.png`
- `TX_H8_FoamContactRing_MaskRGBA_Source_YYYYMMDD.png`
- `TX_H8_FoamContactRing_ChannelPreview_Source_YYYYMMDD.png`
- `TX_H8_ShallowBeamCaustic_MaskRGBA_Source_YYYYMMDD.png`
- `TX_H8_ShallowBeamCaustic_ChannelPreview_Source_YYYYMMDD.png`
- `UnderwaterVFXSource_MANIFEST_YYYYMMDD.md`
- `UnderwaterVFXSource_CONTACT_SHEET_YYYYMMDD.png`

## Family 1 - Fish Silhouette Card Atlas

Purpose: readable scale cues and distant fauna evidence for 0-5 m and 20-50 m underwater route proof. These are not creature truth, AI truth, or final fauna models.

Source size:

- Prototype source: 2048 x 2048 PNG.
- Atlas layout: 4 x 4 cells, 16 silhouette cards.
- Safe cell padding: minimum 24 px source padding and alpha bleed into transparent edge.
- Final import candidate: 1024 or 2048 depending on texture memory proof and `GlobalQualityWeight`; mips on.

Channel packing:

- R: solid body opacity mask.
- G: fin/translucent appendage mask.
- B: rim/counter-shading intensity mask for subtle depth cue.
- A: final combined alpha.

RGB must stay mask data, not colored painted fish, unless the final shader explicitly uses a separate albedo role. The alpha edge must be feathered enough for soft particles but not blurred into unreadable mush.

Exact generation prompt:

```text
Create a seamless-production 2048x2048 transparent PNG atlas for underwater distant fish silhouette cards, 4x4 grid, 16 unique species-like silhouettes inspired by real marine profiles and alien deep-sea plausibility, side view and slight three-quarter variants, NASA-punk deep ocean survival tone, readable scale silhouettes, asymmetric natural fins, no cartoon style, no aquarium color, no full creature render, no background, no water, no bubbles, no text, no logo, no watermark, no frame, no perspective scene, no cast shadows, no baked lighting. Each cell must contain one centered dark neutral silhouette with subtle internal mask regions for body, fins, and rim counter-shading. Output must preserve transparent alpha with clean edges and generous padding for mipmaps.
```

Negative prompt:

```text
cartoon fish, tropical aquarium, colorful reef fish, mascot, monster glamour pose, full illustration scene, blue fog background, gradients as background, blurry silhouettes, tiny unreadable fish, text, logo, watermark, frame, JPEG damage, baked shadow, camera perspective, glow blobs, duplicated identical cells
```

Prep instructions:

- Build channel preview where R/G/B/A are shown separately.
- Run black, white, bright photic-water, and dark-twilight background previews.
- Reject cells that read as birds, arrows, UI icons, random blobs, or finished fauna portraits.
- Preserve 16 variants even if final runtime uses fewer; shader/card selection may scale density continuously.

## Family 2 - Sparse Marine Snow / Mote Atlas

Purpose: depth and volume cue. Sparse snow is evidence of water volume, not full-screen noise.

Source size:

- Prototype source: 1024 x 1024 PNG.
- Atlas layout: 4 x 4 cells, 16 mote/snow fleck shapes.
- Final import candidate: 512 or 1024, mips on, compressed.
- Motes must survive mip preview at 50 percent and 25 percent scale.

Channel packing:

- R: sharp fleck/core mask.
- G: soft halo/falloff mask.
- B: irregularity/phase/size variation mask.
- A: final opacity.

Exact generation prompt:

```text
Create a 1024x1024 transparent PNG atlas for sparse underwater marine snow and suspended motes, 4x4 grid, 16 unique tiny organic fleck masks, photic-to-medium-depth seawater, subtle plankton/mineral/silt fragments, high-detail alpha shapes, no background, no full-screen noise, no cloudy overlay, no fog texture, no bubbles as circles, no stars, no text, no logo, no watermark. Each cell must have one small irregular mote or cluster with clean alpha, sharp core mask, soft halo mask, and natural nonuniform edges. Designed for soft-particle billboards and GPU drift, not as a screen overlay.
```

Negative prompt:

```text
snowstorm, starfield, dust cloud, smoke, fog sheet, blue gradient, white speckle wallpaper, circular bubbles, lens dirt, glitter, magic particles, bokeh, text, logo, watermark, identical dots, noisy full-frame texture
```

Prep instructions:

- Contact sheet must show actual atlas and simulated use at sparse densities: 0.15x, 0.35x, 0.65x.
- Reject if the texture reads as stars, magic glitter, dirt-on-camera, or global snowstorm.
- Runtime owner must drive density through `GlobalQualityWeight` and distance/cause gates, not binary low/high toggles.

## Family 3 - Foam / Contact Ring Mask

Purpose: waterline, surfacing, rock/shore contact, and local surface-breakup support. This replaces color-only rings and rejected visible foam support. It is not a Crest wrapper and not permission to clone Crest materials.

Source size:

- Prototype source: 2048 x 2048 PNG.
- Layout: 2 x 2 ring/decal variants or one central ring plus three partial edge/contact variants.
- Final import candidate: 1024 or 2048 based on memory and close-camera proof.
- Mips on. Alpha bleed mandatory.

Channel packing:

- R: salt rim / thin high-frequency edge.
- G: wet contact band / broad darkening support.
- B: bubble breakup / irregular foam pockets.
- A: shoreline residue / final opacity/falloff.

Exact generation prompt:

```text
Create a 2048x2048 transparent PNG mask atlas for realistic ocean foam and water contact rings, HECTON-8 photic shallow water, salt rim, wet edge, bubble breakup, shoreline residue, surfacing ring, rock-water contact, industrial survival tone, low-contrast white and blue-gray mask information only, no turquoise pool pattern, no flat painted ring, no full-screen foam sheet, no background, no text, no logo, no watermark, no baked lighting, no cast shadows. Pack four variants: one open circular surfacing ring, one broken shoreline edge strip, one rock-contact crescent, one sparse bubble residue patch. Preserve clean alpha falloff and mip-safe edge bleed.
```

Negative prompt:

```text
swimming pool foam, cyan wallpaper, soap bubbles, white paint ring, perfect circle, vector icon, full-screen noise, cloudy overlay, beach stock photo, hard black outline, text, logo, watermark, baked shadows, identical repeated bubbles
```

Prep instructions:

- Must pass preview over wet basalt, shell sand, and bright water backgrounds.
- Must include 1x, 2x, and 4x tile/repetition checks for edge strip variant.
- Reject if ring is visibly circular UI decoration, a flat cyan sheet, or a hard-edged decal.
- Final owner must prove active material slot and no runtime Crest material clone.

## Family 4 - Shallow Beam / Caustic Mask

Purpose: local shallow-light readability and water-material response where a believable light reason exists. This is for local cards/projectors/material masks. Do not use it as a global full-screen caustic cover unless a rendering owner proves a RenderGraph route and cost.

Source size:

- Prototype source: 2048 x 2048 PNG for source.
- Final import candidate: 1024 or 2048 for local masks; 512 allowed only if compact capture preserves readability.
- Tile mode: tileable only for caustic sub-region; beam falloff variants are not required to tile.

Channel packing:

- R: broad beam opacity/falloff.
- G: caustic filament intensity.
- B: silt occlusion/breakup mask.
- A: final soft falloff/clip mask.

Exact generation prompt:

```text
Create a 2048x2048 PNG mask sheet for shallow underwater light beams and local caustic breakup, photic HECTON-8 seawater, realistic soft shaft falloff, thin caustic filaments, broken by suspended silt, no scene background, no blue fog painting, no global overlay, no lens flare, no bloom, no text, no logo, no watermark, no baked shadows. Pack channels as mask data: broad beam falloff, caustic filament intensity, silt occlusion breakup, final alpha. Include one tileable caustic quadrant and three non-tile beam/falloff variants. Must support bright shallow readability without hiding weak geometry.
```

Negative prompt:

```text
god ray wallpaper, blue smoke, cloudy overlay, fantasy magic beams, neon laser, aquarium caustics everywhere, full-screen fog, bloom, lens flare, starburst, text, logo, watermark, hard stripes, muddy low-resolution blur
```

Prep instructions:

- Contact sheet must preview mask on shallow rock, water surface underside, and neutral gray.
- Caustic quadrant needs 2x2 tile test with no obvious seams.
- Beam masks must fade to zero cleanly at edges and survive mip preview without banding.
- Reject if it makes the route less readable or looks like generic blue sci-fi grading.

## Alpha, Tiling, And Mip QA

Required for every family:

- Alpha matte preview on black, white, mid-gray, bright photic water, and dark-twilight backgrounds.
- Mip preview at full, 50 percent, 25 percent, and 12.5 percent scale.
- Compression preview using intended runtime class: BC7 for RGBA masks where feasible, BC3 only if BC7 is rejected by platform route, R8/BC4 allowed only for single-channel final variants.
- Edge bleed into transparent pixels: at least 16 px for 1024, 24 px for 2048.
- No black fringe, white fringe, halo rectangle, premultiplied-alpha mismatch, or cell-boundary leak.
- Tile checks: marine snow density simulation; foam edge strip 1x/2x/4x; caustic quadrant 1x/2x/4x.
- Histogram sanity: no crushed full-white alpha except deliberate core flecks; no full-frame nonzero alpha.
- Source metadata must state whether each channel is sRGB or linear. Mask maps are linear. Albedo-like colored previews are not runtime mask truth.

## Prototype vs Final Import Boundary

Prototype/source allowed:

- Gemini/AI/procedural source with cleanup debt recorded.
- Watermark/mark present only in preserved source, never in final import candidate.
- Contact sheets, channel previews, tile tests, prompt logs, cleanup notes.
- Storage under `Docs/GeneratedAssets/...`.

Final import candidate requires:

- Watermark/mark removed without damaging alpha, tileability, or channel semantics.
- PBR/material role manifest even for non-PBR masks.
- Import setting plan: mips, compression, sRGB false for masks, alpha handling, max size per quality lane, streaming policy.
- Unity owner readback of effective texture/material slot.
- Compact and High screenshots proving the effect helps route readability.
- Texture memory delta before/after.
- Frame Debugger proof for pass/material contribution.
- Profiler/GC proof: 0 B/frame hot path for any runtime system using the masks.

Direct final import is rejected if the source is still a raw generated image, has baked lighting, watermark artifacts, false color channels, no alpha QA, no mip proof, or no Unity readback plan.

## Runtime Use Rules For Future Owner

- VFX owns presentation only. It must not own route truth, pressure truth, oxygen truth, AI truth, save identity, or creature truth.
- Fish cards are scale/readability impostors, not fauna AI.
- Marine snow density must be sparse and bounded. No full-screen noise.
- Foam/contact masks must support local contact or surfacing. No global foam sheet.
- Shallow beam/caustic masks need believable light reason and must not hide weak geometry.
- All density, cadence, and fidelity changes must consume continuous `GlobalQualityWeight`.
- Compact keeps silhouettes, route cues, sparse particles, and material identity.
- Middle adds better local particles and mask detail.
- High adds richer silt/bubble/beam layering and stronger surface underside response.
- Ultra adds visual overkill only after compact route readability passes; it does not add new gameplay truth.

## Hard Reject Criteria

Reject source or final candidate if any of these are true:

- Full-screen noise, blue fog cover, black void cover, or bloom-dependent readability.
- Fish silhouettes read as cartoon icons, tropical aquarium art, monster thumbnails, or finished creature portraits.
- Marine snow reads as stars, magic glitter, lens dirt, or a dense snowstorm.
- Foam reads as turquoise swimming-pool texture, perfect UI ring, hard decal, or visible repeated sheet.
- Caustic/beam mask reads as generic god-ray wallpaper, neon laser, or route-hiding haze.
- Watermark/mark remains in final import candidate.
- Alpha edges show black/white fringe after mip/compression preview.
- Channels are undocumented, identical by accident, wrong color space, or false PBR.
- Source relies on baked directional lighting, cast shadows, scene perspective, text, logos, or framed-object composition.
- Final candidate lacks compact/high screenshot, Frame Debugger, 0 B/frame hot-path proof, or texture memory delta.

## Proof Gates For Future Owner

Source proof:

- `UnderwaterVFXSource_MANIFEST_YYYYMMDD.md` with prompts, source refs, channel packing, cleanup debt, QA verdict, and rejection list.
- Contact sheet with atlas, channel previews, alpha backgrounds, density simulation, and tile/mip checks.
- Explicit `SOURCE_ONLY` or `FINAL_IMPORT_CANDIDATE_PENDING_UNITY_PROOF` disposition per file.

Unity/import proof:

- Compact screenshot from true underwater `0.5-5.0 m` view with surface underside, foreground route material, route/return cue, sparse motes, and at least one fish scale cue.
- Compact screenshot from true underwater `20-50 m` route view with foreground/mid/background separation and no surface horizon dominance.
- High screenshot for both views showing richer but bounded particles, fish/foam/beam contribution, and no changed route truth.
- Frame Debugger proof of active material/pass contribution and no hidden full-screen cover.
- Unity readback of active material/shader GUIDs for fish, motes/snow, foam/contact, and beam/caustic routes if bound.
- Profiler or GCMonitor proof: 0 B/frame hot path for runtime users.
- Texture memory delta before/after import; report compact and high residency expectation.
- Console proof with no new import/material/runtime errors.

Evidence rule: until these artifacts exist, status remains `PENDING UNITY/PROFILER VERIFICATION`.

## Regression Model

- CPU: no runtime change in this packet. Future runtime users must prove no hot-path scene search, material clone, or particle CPU readback.
- GC: no runtime change in this packet. Future runtime users require 0 B/frame proof.
- Memory/VRAM: no asset imported in this packet. Future owner must report texture memory delta and mip/streaming settings.
- Cadence: future use must scale density and update cadence continuously through `GlobalQualityWeight`, with hysteresis/load-shed where runtime state changes.
- Correctness: masks are presentation fakes only. They do not own gameplay truth, save truth, route truth, pressure, oxygen, or AI.

Final status: `SOURCE_PACKET_ONLY / PENDING GENERATION / PENDING UNITY PROOF`.
