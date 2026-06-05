# Wet Basalt Seam-Fix QA Checklist

Date: 2026-06-04
Evidence class: STATIC_DOC
Runtime, Unity import, material assignment, screenshots, profiler, and visual acceptance: PENDING VERIFICATION

## Boundary

This checklist defines rejection gates for future wet basalt, waterline, and foam/contact source candidates. It does not prove generated images, Unity import, material binding, active scene use, frame time, memory, VRAM, GC, RenderGraph, or player-visible quality.

Input context:

- Batch19 1906 rejected direct production PBR derivation from the current wet basalt albedo because it is albedo-only and had high seam metrics: left-right `30.78`, top-bottom `33.40`.
- Batch19 1907 requires seam-fixed wet basalt albedo, matching normal/height source, roughness/wetness logic, cavity AO, salt/mineral masks, waterline transition masks, and foam/contact masks before Unity-owner promotion.
- Current source prompt pack: `Docs/GeneratedAssets/Gemini/Prompts/Batch20/WET_BASALT_SEAMFIX_AND_PBR_PROMPTS_20260604.md`.

## Intake Rules

- Save candidates under `Docs/GeneratedAssets/Gemini/` or a QA subfolder first.
- Do not save generated candidates directly into `Assets/**`.
- Keep original downloaded source and QA preview separate.
- Do not claim final PBR material quality from source images alone.
- Do not replace existing basalt TerrainLayer textures by GUID swap. The old Rock031 references are shared and cannot be overwritten as a shortcut.

## Required Candidate Set

Minimum source family before Unity-owner import:

- seam-fixed tileable albedo;
- matching normal map or height/relief source for offline normal derivation;
- roughness source;
- AO source;
- MRAO packed source or offline-packed MRAO from accepted channel sources;
- waterline wetness mask;
- foam/contact mask.

Separate roughness/AO maps are allowed as offline source/intermediate files. Final shipped binding should use the target shader channel contract.

## Edge Diff Gate

Create edge-diff metrics for every square tile candidate before promotion.

Use 8-bit channel mean absolute difference unless the file is HDR/source-float:

```text
left_right_edge_diff = mean(abs(pixel[x=0,y] - pixel[x=width-1,y]))
top_bottom_edge_diff = mean(abs(pixel[x,y=0] - pixel[x,y=height-1]))
```

Pass targets:

- Preferred production target: `<= 8.0` mean absolute difference per compared edge.
- Review band: `> 8.0` and `<= 12.0`; accept only if 3x3 visual tiling shows no seam at intended UV scale.
- Reject band: `> 12.0` or any visible hard seam.
- Automatic reject: `> 20.0`, unless the map is a special signed/vector map and a channel-specific reviewer proves visual continuity.

Current Batch19 albedo metrics `30.78 / 33.40` are rejected for production tile use without seam-fix.

Normal maps require both raw edge comparison and visual normal-lit review. A normal map with matching RGB edges can still fail if the tangent direction creates a lighting seam.

## 3x3 Tiling Visual Gate

For every candidate, generate or view a 3x3 tiled preview.

Reject if:

- seams are visible at tile borders;
- a cross-shaped seam appears at the center tile boundaries;
- large hero cracks, bright stains, foam cells, or mineral islands repeat obviously;
- diagonal repetition reads as wallpaper;
- low-resolution bands or AI brush patterns become visible after tiling;
- the tile works only when darkened, fogged, blurred, or hidden by storm/noir grading.

Required views:

- full 3x3 preview;
- 100 percent crop at a tile intersection;
- 50 percent zoom to check macro repetition;
- mip/blur approximation to check if seams worsen at distance.

## Shared Visual Rejection Gate

Reject any generated source that contains:

- perspective, horizon, scene composition, object silhouette, cliffs, waves as a landscape, or framed render;
- labels, text, logo, UI, watermark, symbols, or border/frame;
- baked highlights, baked shadows, lighting gradient, directional cast shadow, vignette, or photographic glare;
- crayon/procedural scribbles, random noise sold as material, smooth blob rock, or low-resolution bands;
- flat plastic wetness, chrome wetness, uniform glossy overlay, or muddy dark cover-up;
- black/noir darkness used to hide weak surface/coastline detail;
- repetitive obvious tiling that cannot be broken by normal material blending;
- albedo color leaking into mask maps;
- false metallic on ordinary basalt;
- foam rendered as opaque snow strips or dirty storm foam by default.

## Channel Sanity Gate

### Albedo

Acceptance:

- sRGB source intent.
- Base color only.
- Natural wet basalt black-gray range with mineral/salt/sediment color variation.
- Bright photic-shallow readability; not crushed black.
- No baked shadows, highlights, or directional lighting.

Reject:

- beauty render with light direction;
- shadowed cliff/rock scene;
- pure black/noir cover-up;
- flat gray procedural noise;
- wetness represented only as painted white glare.

### Normal

Acceptance:

- Tangent-space normal source, preferably OpenGL/NormalGL until Unity owner confirms orientation.
- Relief follows cracks, pores, chipped edges, ridges, and eroded basalt grain.
- Edge continuity passes raw and visual checks.
- No albedo color, no lighting, no AO, no fake shadow.

Reject:

- normal generated by embossing color stains without physical relief logic;
- inverted-looking cracks or pillow-like stone;
- flat purple map with no useful relief;
- severe noise that will sparkle after compression/mips.

### Roughness

Acceptance:

- Grayscale linear source.
- Wet cracks and damp mineral stains are smoother/darker under roughness convention.
- Raised dry basalt chips, salt crust, and eroded pores are rougher/lighter.
- Not constant gray.
- Not identical to AO.

Reject:

- full black mirror surface;
- full white chalk surface;
- random dirt map;
- smoothness output mislabeled as roughness without documented inversion.

### AO

Acceptance:

- Grayscale linear source.
- Cavity-biased only.
- Deep cracks, pores, undercuts, sediment pockets, and fracture intersections darken.
- Exposed planes remain light.

Reject:

- broad dirty overlay over exposed surfaces;
- vignette or directional shadow;
- same image as roughness;
- black/noir map used to hide weak albedo.

### MRAO

Accepted static contract for `Hecton_MraoAtlasLit` from Batch19 1906:

- R = Metallic.
- G = Roughness.
- B = AO.
- A = EmissionMask.

Wet basalt sanity:

- R must be black/zero for ordinary basalt.
- G must match accepted roughness source.
- B must match accepted cavity AO source.
- A must be black/zero unless a Unity material owner explicitly locks a wetness/family-mask variant.
- Linear import intent, not sRGB.
- Channels must be independently inspected.

Reject:

- metallic whole rock;
- color albedo packed into RGB;
- roughness and AO identical by accident;
- alpha used as random grime without shader contract;
- filename-based `ARM`, `ORM`, `MRAO`, or `mask` assumptions without channel proof.

### Waterline Wetness Mask

Suggested source guidance until Unity owner locks exact shader route:

- R = wetness strength.
- G = drying falloff / transition softness.
- B = salt, sediment, and mineral breakup.
- A = specular boost or reserved confidence mask only if accepted.

Acceptance:

- irregular wet/dry transition;
- salt/mineral/sediment breakup follows cracks and shoreline contact;
- preserves surface brightness and material identity.

Reject:

- hard straight black stripe;
- muddy dark waterline cover-up;
- wetness as uniform plastic gloss;
- mask that changes terrain/gameplay truth.

### Foam / Contact Mask

Suggested source guidance until Unity owner locks exact shader route:

- R = long foam strand / contact foam strength.
- G = cross-flow wet edge breakup.
- B = foam lace breakup, bubbles, sediment/salt interruption.
- A = optional caustic receiver or confidence mask only if accepted.

Acceptance:

- thin foam lace and broken strands;
- sparse translucent contact breakup;
- not a full white strip;
- believable shallow shoreline use.

Reject:

- opaque snow foam;
- flat white bands;
- storm grime default for normal surface;
- perspective wave photo;
- repeated bubble stamps.

## Unity Import Settings For Future Owner

These settings are future Unity-owner checklist items, not verified by this document.

Albedo:

- Texture Type: Default.
- sRGB: enabled.
- Mip Maps: enabled for world/coastline use.
- Compression: high quality, BC7 on Standalone where budget allows; platform equivalent on mobile/XR.
- Read/Write: disabled.
- Streaming Mip Maps: enabled where project texture streaming policy requires it.

Normal:

- Texture Type: Normal Map.
- sRGB: disabled.
- Compression: BC5/normal-compressed where supported.
- Mip Maps: enabled.
- Read/Write: disabled.
- Flip green channel only if Unity material preview proves the tangent orientation is wrong.

Roughness, AO, MRAO, wetness, foam/contact masks:

- Texture Type: Default.
- sRGB: disabled.
- Compression: high quality linear mask compression; BC7 for RGBA packed masks where appropriate.
- Mip Maps: enabled for world/coastline use.
- Read/Write: disabled.
- Alpha is Transparency: disabled unless the target shader explicitly samples alpha as transparency.
- No runtime texture compression.

Do not bind separate AO and roughness textures to a shipped material if the shader expects packed MRAO. Use separate maps as offline sources or debug views, then pack into the accepted shader contract.

## Unity Proof Required Later

A Unity owner must provide before any runtime/material acceptance claim:

- material or TerrainLayer slot proof for albedo, normal, and packed mask;
- active scene renderer proof for wet basalt and foam/contact routes;
- Game View and Scene View screenshots: close wet rock, glancing waterline, vertical waterline, wide coast, underwater edge, and 5-20 m photic transition;
- compact, middle, high, and ultra quality captures if visual settings are changed;
- Frame Debugger or RenderGraph proof for new render/foam/caustic/shader paths;
- Profiler/GC/memory/VRAM proof if runtime code, render features, texture residency, or material binding behavior changes.

Static docs or texture files alone can only claim `STATIC VERIFIED`.

## When To Reject

Reject the full candidate family if any required map fails its channel role, seam gate, or material truth gate.

Reject immediately if:

- albedo is not seamless;
- normal does not match albedo scale;
- roughness/AO are generated from random luma without material logic;
- MRAO channels are wrong or undocumented;
- waterline mask hides the shoreline with darkness;
- foam/contact mask is opaque or scenic;
- any map contains text/UI/frame/perspective/baked lighting;
- material would look acceptable only after heavy fog, darkness, blur, or storm cover.

Do not average bad maps into a pass. One false PBR channel makes the family unfit for production promotion.

## Continuous Quality Consequences

These consequences describe texture/source fidelity only. They do not change gameplay truth, save identity, collision, terrain authority, water truth, or shader channel semantics.

| Lane | QA consequence |
|---|---|
| Compact / near 0.0 | Lower resolution and fewer secondary masks are allowed, but seam-free albedo, correct normal, packed mask sanity, visible wet/dry identity, and clean foam/contact read are mandatory. No muddy/noir fallback. |
| Middle / around 0.35 | Clearer channel independence, stronger material scale, better 3x3 tiling result, moderate wetness and foam breakup. |
| High / around 0.7 | Higher source resolution, richer fracture normal, more precise roughness/AO/wetness masks, stronger mineral/salt breakup after proof. |
| Ultra / near 1.0 | Hero source resolution and dense controlled detail are allowed. Ultra adds sensory richness only; it cannot fix bad seams or wrong channels by brute force. |

## Final Checklist

Before handoff to Unity owner:

- [ ] Candidate files are under `Docs/GeneratedAssets/Gemini/`, not `Assets/**`.
- [ ] Required candidate set exists.
- [ ] Edge diff metrics recorded for each tileable source.
- [ ] 3x3 tiling preview reviewed.
- [ ] Albedo has no baked lighting or noir cover-up.
- [ ] Normal map or height source matches material scale and tile edges.
- [ ] Roughness source follows wet/dry basalt logic.
- [ ] AO source is cavity-biased.
- [ ] MRAO channel contract is documented.
- [ ] Waterline wetness mask preserves bright surface readability.
- [ ] Foam/contact mask is sparse, natural, and non-opaque.
- [ ] Rejection reasons are recorded for failed candidates.
- [ ] Unity import/material proof remains explicitly `PENDING VERIFICATION`.
