# 2903 Shoreline Foam / Caustic Texture Prompt And QA Pack

Agent ID: 2903_SHORELINE_FOAM_CAUSTIC_TEXTURE_PROMPT_AND_QA_PACK  
Date: 2026-06-04  
Mode: static prompt and QA pack only. No image generation, no Unity, no Play Mode, no build, no `Assets/**` writes.  
Evidence class: STATIC_DOC  
Generation state: NOT RUN  
Unity import state: NOT READY  
Route acceptance state: PENDING UNITY/PROFILER/SCREENSHOT VERIFICATION

## Evidence Labels

Claim: This file defines future Gemini/browser prompts, staging paths, channel contracts, and static QA gates.  
Evidence Class: STATIC_DOC  
Artifact: `Docs/GeneratedAssets/Gemini/Prompts/Batch29/2903_SHORELINE_FOAM_CAUSTIC_TEXTURE_PROMPT_AND_QA_PACK.md`  
Command or Unity tool: text authoring only.  
Date: 2026-06-04  
Residual risk: no generated images, no image QA, no PBR derivation, no Unity material binding, no screenshot proof.

Claim: Static prompt packs are not generated asset proof and cannot promote any source to Unity import.  
Evidence Class: STATIC_DOC  
Artifact: `AGENTS.md`, `QA_Evidence_Text_Filter_Audit.txt`, `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`, Batch28 shoreline audit.  
Command or Unity tool: static document review.  
Date: 2026-06-04  
Residual risk: future workers must still run actual image intake, manual review, import validation, and scene proof.

## Authority Loaded

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `water.md`
- `terrain.md`
- `rendering.md`
- `shaders.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `Docs/Reports/Batch28/2803_SHORELINE_FOAM_PHOTIC_TERRAIN_STATIC_ART_ROUTE_AUDIT.md`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`

## Current Blocker Basis

Evidence Class: STATIC_DOC / STATIC_SOURCE from Batch28 report only.

The shoreline route is blocked because current static evidence shows rejected or incomplete material sources:

- active broad photic terrain still depends on rejected wet basalt source lineage without a complete normal/MRAO/wetness/shell-sand/contact stack;
- active visible shoreline foam is a generic transparent ribbon, not a contact-owned foam/salt/wetness mask family;
- active floor caustics are a transparent additive sine fake, useful only as support after depth/light/receiver gating, not broad caustic proof;
- existing wet basalt 1428/1429/periodic variants and Batch21 shell/sand candidates are rejected or reference-only;
- no current 1 m shoreline close proof exists with waterline foam, wet rock, shell/sand substrate, shallow caustic read, and material-scale witnesses.

This pack targets source candidates only. It must not be used to edit `Assets/**`, replace materials, or claim `READY_FOR_UNITY_IMPORT`.

## Staging Paths

All future generated candidates from this prompt pack must stay outside `Assets/**`.

Output root:

```text
Docs/GeneratedAssets/Gemini/Outputs/Batch29/2903/
```

Required family folders:

```text
Docs/GeneratedAssets/Gemini/Outputs/Batch29/2903/WetBasaltShoreline/
Docs/GeneratedAssets/Gemini/Outputs/Batch29/2903/ShellSandSubstrate/
Docs/GeneratedAssets/Gemini/Outputs/Batch29/2903/ShoreFoamSaltWetContact/
Docs/GeneratedAssets/Gemini/Outputs/Batch29/2903/ShallowCausticMasks/
Docs/GeneratedAssets/Gemini/Outputs/Batch29/2903/Manifests/
Docs/GeneratedAssets/Gemini/Outputs/Batch29/2903/TilePreviews/
Docs/GeneratedAssets/Gemini/Outputs/Batch29/2903/ContactSheets/
Docs/GeneratedAssets/Gemini/Outputs/Batch29/2903/Rejected/
```

Audit root:

```text
Docs/GeneratedAssets/Gemini/Audit/Batch29/2903/
```

Allowed future source filenames:

```text
TX_B29_2903_WetBasaltShoreline_AlbedoHeightSource_[timestamp]_Gemini.png
TX_B29_2903_WetBasaltShoreline_WetSaltMaskSource_[timestamp]_Gemini.png
TX_B29_2903_PhoticShellSandSubstrate_AlbedoHeightSource_[timestamp]_Gemini.png
TX_B29_2903_ShoreFoamSaltWetContact_RGBAMaskSource_[timestamp]_Gemini.png
TX_B29_2903_ShallowCausticReceiver_GrayscaleMaskSource_[timestamp]_Gemini.png
TX_B29_2903_ShallowCausticLookup_RGBAMaskSource_[timestamp]_Gemini.png
```

Every candidate needs a sidecar manifest beside the image:

```text
[same_filename_without_extension]_MANIFEST.md
```

Manifest minimum:

- prompt ID;
- full prompt and negative prompt;
- source tool;
- generation timestamp;
- SHA-256;
- dimensions;
- color-space intent;
- tileability intent;
- intended meters per tile;
- channel contract;
- evidence class;
- QA command;
- QA output paths;
- status: `SOURCE_CANDIDATE`, `PASS_STATIC`, `REVIEW`, `REJECT`, or `READY_FOR_UNITY_IMPORT`.

`READY_FOR_UNITY_IMPORT` is forbidden until static image QA, manual 2x2/3x3 review, manifest review, PBR derivation plan, and Unity-owner import proof are completed by a later task.

## Global Negative Prompt

Use this negative prompt for every family unless a narrower family prompt adds stricter exclusions:

```text
visible seams, mismatched tile edges, repeated obvious AI stamp, large hero shape, baked lighting, baked directional highlight, cast shadow, painted caustic shadow in albedo, perspective, horizon, object render, landscape scene, camera depth of field, text, logo, UI, watermark, frame, border, low resolution, blur, jpeg artifacts, compression mush, flat Perlin noise, generic grunge, crayon texture, cartoon, painterly, plastic gloss, chrome wetness, muddy black noir grade, pure black crushed albedo, generic blue sci-fi haze, neon fantasy reef, random symbols, fake labels, diagonal spotlight, hidden terrain through darkness, material identity replaced by noise
```

## Family 1 - Wet Basalt Shoreline

Prompt ID: `TX_B29_2903_WetBasaltShoreline_AlbedoHeightSource`  
Evidence Class: STATIC_DOC  
Purpose: source for future wet basalt albedo cleanup, height/normal derivation, roughness/wetness/MRAO derivation, and waterline material response.  
Required dimensions: 4096x4096 preferred, 2048x2048 minimum for source candidate.  
Tileability: seamless square tile, 2x2 and 3x3 must show no hard edge or obvious repeated plate.  
Intended meters per tile: 2 m per tile for broad shoreline cliffs/shelves; optional 1 m crop variant for close waterline proof.  
Color-space intent: sRGB albedo/source; no baked lighting.  
Import state: NOT READY.

Primary prompt:

```text
Create ONE seamless square tileable PBR material source texture for bright photic shoreline wet basalt on an alien ocean moon in a premium Unity URP underwater survival game. This is an orthographic top-down material sample, not a scene. Material identity: volcanic black-gray basalt, salt-water erosion, chipped fracture planes, small pores, rough mineral grains, stratified cracks, wet cavities, tide-polished ridges, pale salt residue in crevices, tiny shell grit and sand caught in cracks, subtle teal-gray mineral staining, and believable waterline wear. The surface must read as real wet rock in clear shallow daylight, with strong material breakup at micro, meso, and macro scale, but no single giant repeated crack. Include height-like relief cues useful for later normal, AO, roughness, and wetness-mask derivation. Use neutral bright photic daylight only as even illumination. No directional lighting, no cast shadows, no perspective, no horizon, no object silhouette, no text, no logo, no UI. Output one image only.
```

Family-specific negative prompt:

```text
black mud, pure black slabs, smooth tar, chrome rock, flat asphalt, generic noise rock, repeated giant cracks, wet gloss painted everywhere, dry desert basalt, cliff photo, shoreline landscape, wave scene, baked caustic highlights, black noir darkness, shell/sand covering the rock entirely, clean plastic, low-poly rock, blurred basalt, AI scribble fractures
```

Optional variant prompt - close contact:

```text
Create ONE seamless square 1-meter tileable orthographic PBR material source texture for close-camera wet basalt at the exact ocean waterline. Emphasize wet/dry transition traces, salt crust in crevices, tide-polished raised ridges, small shell grit lodged in fractures, black-gray volcanic mineral grains, and height-like relief for later normal and wetness masks. Keep the material bright and readable under shallow daylight. No foam sheet, no wave scene, no perspective, no baked shadows, no painted caustics, no text, no logo.
```

Channel contract for later derivation:

- Albedo/source: basalt base color only, no shadows or highlights.
- Height-like source: relief may be present in the same source but not as lighting.
- Future Normal: derive offline from height/source or sculpted correction.
- Future MRAO: R metallic 0 except sparse true mineral inclusions only; G roughness with wet ridge/cavity variation; B AO cavity-biased; A wetness/salt/mineral family mask.
- Future Wet/Salt mask: white/pale salt residue and wet cavities must be separable from rock color.

Reject if:

- the texture looks like asphalt, tar, charcoal, or black mud instead of basalt;
- albedo is crushed dark to hide weak detail;
- wetness is uniform chrome gloss;
- shell/sand dominates the tile and destroys basalt identity;
- 2x2/3x3 reveals obvious repeated hero cracks or mismatched borders;
- output is a cliff/shoreline photo rather than a flat material sample.

## Family 2 - Photic Shell / Sand / Calcite Substrate

Prompt ID: `TX_B29_2903_PhoticShellSandSubstrate_AlbedoHeightSource`  
Evidence Class: STATIC_DOC  
Purpose: source for bright shallow shell/sand/calcite substrate under clear water near shoreline shelves.  
Required dimensions: 4096x4096 preferred, 2048x2048 minimum.  
Tileability: seamless square tile, 2x2 and 3x3 required.  
Intended meters per tile: 1.5 m per tile; optional 1 m close material crop.  
Color-space intent: sRGB albedo/source; no baked lighting.  
Import state: NOT READY.

Primary prompt:

```text
Create ONE seamless square tileable orthographic PBR material source texture for bright photic shell-sand-calcite substrate in a clear shallow alien ocean shoreline. This is a flat material sample, not a scene. Material identity: fine submerged sand and silt, broken shell chips, pale calcite fragments, limestone grit, small rounded basalt grains, reef-grit pieces, subtle algae and mineral staining in protected cavities, sediment partially burying fragments, and soft current ripple logic. The tile must be beautiful, premium, physically scaled to about 1.5 meters, and readable through clear shallow water. Include height-like relief cues for later normal and AO derivation: tiny ripple ridges, fragment edges, cavity pockets, buried chips, and shell/calcite breakup. Use even neutral photic daylight. No horizon, no perspective, no plants, no coral bodies, no fish, no object render, no text, no logo, no UI, no baked directional light, no painted caustic shadows. Output one image only.
```

Family-specific negative prompt:

```text
beige mud, generic sand noise, blue-gray muddy sand, shell pile object render, repeated cloned shell, giant shell hero shape, coral carpet, seaweed, kelp, fish, tropical aquarium candy colors, black substrate, baked caustics, cast shadows, glossy plastic shells, unreadable mush, uniform gravel, flat color fill
```

Optional variant prompt - basalt/sediment transition:

```text
Create ONE seamless square tileable orthographic PBR material source texture for shallow-water substrate where wet basalt grit transitions into shell sand and calcite silt. Include black-gray basalt chips, pale shell fragments, calcite flecks, soft sand, silt accumulation around mineral edges, subtle algae stain in cavities, and physically plausible 1-meter scale. Bright photic readability, no foam, no shoreline scene, no perspective, no baked lighting, no text, no logo.
```

Channel contract for later derivation:

- Albedo/source: base color only, no shadows/highlights.
- Future Normal: derive from ripple ridges, shell chip edges, calcite fragments, silt pockets.
- Future MRAO: R metallic 0; G roughness varies between wet shell, matte silt, calcite, basalt grains; B AO only in cavities/under fragments; A optional shell/calcite/silt family mask.
- Future blend mask: substrate must allow separation between shell/calcite, sand/silt, basalt grit, and algae/mineral stain.

Reject if:

- the tile reads as beige mud or generic sand;
- repeated shell stamps are visible in 2x2 or 3x3;
- it contains coral bodies, plants, animals, horizon, or object perspective;
- it uses painted caustic shadows or baked sun direction;
- it lacks enough relief signal for later normal/AO derivation;
- it becomes dark/noir to hide weak substrate.

## Family 3 - Shore Foam / Salt / Wet Contact RGBA Mask

Prompt ID: `TX_B29_2903_ShoreFoamSaltWetContact_RGBAMaskSource`  
Evidence Class: STATIC_DOC  
Purpose: source for presentation-owned contact masks: foam lace, wet edge breakup, salt/sediment residue, and confidence/opacity.  
Required dimensions: 2048x2048 minimum, 4096x4096 preferred for close shoreline source.  
Tileability: seamless square tile; 2x2 and 3x3 must show no stripe, no seam, no repeated bubble stamp.  
Intended meters per tile: 2 m per tile for shoreline ribbons/contact decals; 0.5-1 m detail crop allowed for close foam lace.  
Color-space intent: linear mask source.  
Import state: NOT READY.

Primary prompt:

```text
Create ONE seamless square tileable RGBA mask source for shoreline foam, salt residue, and wet-contact breakup over rough wet basalt and shell-sand at a bright clear alien ocean shoreline. This is source data for a Unity URP material mask, not a scenic wave photo. Orthographic top-down material mask view. The mask should contain thin white sea-foam lace, translucent microbubble clusters, broken foam cells, tide-sheared strands, cross-flow wet edge breakup, salt residue caught along rough rock cracks, sediment interruptions, and irregular contact logic that follows rough shoreline material. Keep the mask sparse and physically plausible; avoid opaque white strips. Channel intent: Red = long foam strand/contact strength, Green = cross-flow wet edge and waterline breakup, Blue = microbubble lace plus sediment interruption, Alpha = optional confidence/soft opacity. No horizon, no perspective, no object silhouette, no text, no logo, no UI, no baked lighting. Output one image only.
```

Family-specific negative prompt:

```text
opaque white ribbon, flat white strip, snow blanket, dirty storm foam covering everything, wave photograph, surf scene, horizon, object render, repeated bubble stamps, circular stamp pattern, black muddy contact band, foam used to hide rock, foam with cast shadows, perspective waterline, blue water photo, logo, text, watermark, noisy grayscale only, full alpha everywhere
```

Optional variant prompt - salt residue dominant:

```text
Create ONE seamless square tileable RGBA mask source for salt residue and wet shoreline contact on rough wet basalt with sparse foam. Orthographic top-down mask/source view. Red channel should emphasize thin foam/contact lines; Green should emphasize wet edge breakup; Blue should emphasize salt/sediment residue interruptions; Alpha should be soft confidence only. Keep it sparse, high-detail, non-repeating, and physically tied to rock cracks and shell-sand interruptions. No perspective, no wave photo, no opaque white strip, no text, no baked light.
```

Channel contract:

- R: long foam strand and primary contact strength.
- G: cross-flow wet edge breakup and tide shear.
- B: microbubble lace, sediment interruption, salt flecks.
- A: soft confidence/opacity; never full-white by default.

Reject if:

- any channel is a full-white strip or full-field noise;
- foam covers the whole tile and hides underlying material;
- 2x2/3x3 shows repeated bubbles, stamps, bands, or seams;
- it looks like a wave photograph or scenic surf;
- alpha is opaque everywhere;
- it cannot be separated into foam/wet/salt roles.

## Family 4 - Shallow Caustic Receiver And Lookup Masks

Prompt IDs:

- `TX_B29_2903_ShallowCausticReceiver_GrayscaleMaskSource`
- `TX_B29_2903_ShallowCausticLookup_RGBAMaskSource`

Evidence Class: STATIC_DOC  
Purpose: source masks for shallow/lit/depth-gated caustic projection and receiver modulation. Not global abyssal caustics.  
Required dimensions: 1024x1024 minimum, 2048x2048 preferred.  
Tileability: seamless square tile; 2x2 and 3x3 required.  
Intended meters per tile: 4 m per tile for receiver modulation; 8 m per tile for lookup pattern.  
Color-space intent: linear mask/source.  
Import state: NOT READY.

Primary prompt - grayscale receiver mask:

```text
Create ONE seamless square tileable grayscale mask source for shallow-water caustic receiver eligibility on rough wet basalt, shell-sand, and shallow seabed surfaces. This is not an albedo texture and not a scenic image. Orthographic top-down mask view. The mask should describe where projected shallow caustics can appear: raised wet rock ridges, shell/calcite fragments, shallow sandy ripples, and exposed receiver surfaces. Keep the pattern organic, subtle, sparse, and material-aware, with protected cavities and occluded cracks darker. No bright painted caustic lines, no baked lighting, no shadows, no perspective, no horizon, no text, no logo. Output one grayscale image only.
```

Primary prompt - RGBA caustic lookup:

```text
Create ONE seamless square tileable RGBA source mask for shallow clear-water caustic lookup in a Unity URP ocean survival game. This is a mask/lookup source, not albedo and not a scene. Orthographic top-down abstract optical pattern, physically plausible shallow water caustic lattice, thin refracted light filaments, broken cellular curves, variable line thickness, no single hero shape, no directionally baked lighting. Channel intent: Red = broad low-frequency caustic cells, Green = fine filament detail, Blue = broken secondary ripple interference, Alpha = soft confidence/attenuation. It must tile cleanly and avoid obvious repetition in 2x2 and 3x3. No perspective, no horizon, no object, no text, no logo, no UI. Output one image only.
```

Family-specific negative prompt:

```text
painted sunlight on sand, scenic underwater photo, blue water gradient, baked caustic shadows in albedo, global abyss caustics, neon laser web, hard white cracks, repeated Voronoi stamp, large hero ring, perspective pool photo, horizon, object render, text, logo, watermark, black darkness, full-white mask, uniform gray field
```

Channel contract:

Receiver grayscale:

- White/high: eligible shallow lit receiver zones.
- Mid: partial receiver zones and soft wet ridges.
- Dark: cavities, occluded cracks, blocked surfaces, depth/roughness interruptions.

Lookup RGBA:

- R: broad caustic cell pattern.
- G: fine filament detail.
- B: secondary ripple interference / broken pattern.
- A: soft confidence/attenuation.

Reject if:

- the mask paints visible lighting into albedo-like color;
- it creates global caustics suitable for abyssal terrain without light reason;
- it is a sine-like sheet, hard laser web, or uniform Voronoi stamp;
- 2x2/3x3 shows obvious repeated cells;
- alpha or grayscale is full-white/full-black without receiver logic;
- it hides terrain material or interactable readability.

## Static QA Intake Procedure

Evidence Class: STATIC_DOC. Future QA results must be written by a later generation/intake task.

For each generated candidate:

1. Save image and sidecar manifest under the exact `Docs/GeneratedAssets/Gemini/Outputs/Batch29/2903/` family folder.
2. Compute SHA-256 and record it in the manifest.
3. Record exact prompt, negative prompt, dimensions, source tool, timestamp, role, channel contract, color-space intent, and intended meters per tile.
4. Run static intake QA into `Docs/GeneratedAssets/Gemini/Audit/Batch29/2903/[CandidateName]/`.
5. Generate and save 2x2 preview under `Docs/GeneratedAssets/Gemini/Outputs/Batch29/2903/TilePreviews/`.
6. Generate and save 3x3 preview under `Docs/GeneratedAssets/Gemini/Outputs/Batch29/2903/TilePreviews/`.
7. Generate contact sheet under `Docs/GeneratedAssets/Gemini/Outputs/Batch29/2903/ContactSheets/`.
8. Manually review 1x, 2x2, and 3x3 views at full resolution and thumbnail scale.
9. Assign one status only: `REJECT`, `REVIEW`, or `PASS_STATIC`.
10. Do not assign `READY_FOR_UNITY_IMPORT` unless a later Unity-owner/import task proves the full stack.

Suggested command shape, adjust only if the tool path differs:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch29/2903/[Family]/[Candidate].png --out-dir Docs/GeneratedAssets/Gemini/Audit/Batch29/2903/[Candidate]
```

## 2x2 And 3x3 Preview QA Gates

2x2 preview must prove:

- no left/right or top/bottom hard seam;
- no obvious repeated hero crack, shell cluster, bubble stamp, or caustic cell;
- material read survives tiling;
- no border, frame, watermark, text, UI, or perspective edge;
- luminance does not create a cross-shaped seam.

3x3 preview must prove:

- no wallpaper rhythm at gameplay distance;
- no diagonal banding across repeated tiles;
- no repeated central object;
- no cloned shell/foam/caustic stamp pattern;
- macro breakup remains plausible over a larger shoreline surface;
- substrate and basalt still read as authored material, not noise.

Manual material review must inspect:

- full image at 100 percent;
- mip-like reduced view;
- channel separation for RGBA masks;
- histogram sanity: no crushed albedo, no full-white mask fields unless semantically justified;
- material truth: basalt, shell/sand, foam/salt/wetness, and caustics remain distinct.

## Global Reject Criteria

Reject any candidate immediately if it has:

- baked lighting, cast shadows, or painted caustics in albedo/source;
- perspective, horizon, object render, scene photo, frame, logo, watermark, UI, or text;
- visible seams in 2x2;
- obvious repeated hero shapes in 3x3;
- crushed black/noir darkness used to hide weak material identity;
- generic noise instead of physical material;
- no usable channel separation for RGBA mask sources;
- albedo values that look like black mud, beige mud, plastic, chrome, or low-resolution mush;
- foam that is an opaque strip or full-field snow;
- caustics without shallow/lit/receiver logic;
- any source under `Assets/**`.

## Status Labels

Use these exact labels:

- `SOURCE_CANDIDATE`: generated image exists, no QA run yet.
- `REJECT`: hard static blocker exists. Candidate may be archived as reference only.
- `REVIEW`: no immediate hard static blocker, but manual/material/channel review remains unresolved.
- `PASS_STATIC`: static intake and manual 2x2/3x3 source review found no static blocker. This is not Unity acceptance.
- `READY_FOR_UNITY_IMPORT`: allowed only after future explicit import task has PBR derivation plan, channel manifests, import settings, and Unity-owner proof. This prompt pack cannot set this label.

## Continuous GlobalQualityWeight Consequences

These are authoring consequences, not runtime proof.

Low / compact, approximately `GlobalQualityWeight 0.0-0.25`:

- source can bake down to 1024 standard world materials and 512-1024 masks after acceptance;
- preserve basalt identity, shell/sand scale, waterline foam shape, and shallow caustic readability;
- reduce decal density, mask resolution, caustic cadence, and transparent layers before damaging material identity;
- no darkness, green haze, blur, or generic noise cover-up.

Middle, approximately `GlobalQualityWeight 0.25-0.55`:

- use 2048 key shoreline families where memory budget permits;
- keep accepted wetness/contact masks and shell/sand blend detail;
- allow stronger roughness variation, local decals, and better mask precision;
- maintain same material/channel contracts as compact.

High, approximately `GlobalQualityWeight 0.55-0.85`:

- spend budget on richer normals, wetness masks, foam breakup, receiver caustics, shell/calcite detail, and close-waterline material witnesses;
- keep gameplay truth, terrain ownership, and material identity unchanged;
- no new channel meaning hidden behind a high-tier variant.

Ultra, approximately `GlobalQualityWeight 0.85-1.0`:

- allow hero-only 4096 sources, denser offline decal layers, sharper PBR derivation, richer local caustic lookup, and close-camera overkill;
- runtime import still needs compressed, shared, SRP-batcher-compatible material contracts;
- ultra buys sensory richness only, not new route truth or import bypass.

Quality must interpolate through continuous `GlobalQualityWeight`. Do not create binary low/high texture logic. Do not change material identity, channel contracts, save identity, terrain truth, or gameplay route by quality.

## No-Import Lock

No output from this prompt pack may enter `Assets/**` until all are true:

- the source candidate has `PASS_STATIC`;
- 2x2 and 3x3 previews are saved and manually accepted;
- sidecar manifest lists prompt lineage, SHA-256, dimensions, meters per tile, channel contract, and status;
- future normal/MRAO/wetness/foam/caustic derivation plan is written;
- import settings are specified for albedo, normal, MRAO/masks, and optional detail/height;
- Unity owner produces material binding proof, screenshot proof, and route proof;
- status is explicitly changed to `READY_FOR_UNITY_IMPORT` by a later task.

Until then, all candidates remain source/reference material under `Docs/GeneratedAssets/**`.

## Final Static Boundary

Evidence Class: STATIC_DOC.

This file proves only prompt and QA route authoring. It does not prove generated asset quality, image existence, tileability, Unity import correctness, runtime caustics, foam contact, material binding, Frame Debugger state, profiler cost, GC, VRAM, or route acceptance.

Strongest blockers carried forward:

- accepted wet basalt PBR family is missing;
- accepted shell/sand/calcite substrate family is missing;
- accepted foam/salt/wet-contact RGBA mask is missing;
- accepted shallow caustic receiver/lookup mask is missing;
- current active foam/caustic route is support-only and not proof;
- current shoreline route has no accepted 1 m waterline close packet.
