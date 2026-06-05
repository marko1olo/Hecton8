# 2101 Wet Basalt Static QA Gate Checklist

Agent ID: 2101  
Evidence class: STATIC_DOC  
Runtime, Unity import, material binding, scene capture, profiler, GC, memory, and VRAM proof: PENDING VERIFICATION

## Boundary

This checklist is for future wet basalt shoreline, triplanar cliff rock, wetness/waterline, and foam/salt contact source candidates. It does not approve any image, texture, material, TerrainLayer, shader, scene route, or Unity import.

Candidates must stay under `Docs/GeneratedAssets/Gemini/Outputs/Batch21/` or a QA subfolder until accepted by a future Unity owner. Do not save generated candidates directly into `Assets/**`.

## Intake Checklist

- [ ] File path is outside `Assets/**`.
- [ ] Prompt ID recorded.
- [ ] SHA-256 recorded by future intake owner.
- [ ] Square and power-of-two where tileable material source is required.
- [ ] 2x2 tile preview exists.
- [ ] Manual 3x3 tile preview exists.
- [ ] 100 percent crop at tile intersection reviewed.
- [ ] 50 percent zoom checked for macro repetition.
- [ ] Mip/blur approximation checked for seam worsening.
- [ ] No account name or email in filename, report, or manifest.

## Shared Reject Gate

Reject if any source contains:

- perspective, horizon, scene composition, object silhouette, cliff scene, wave photo, or framed render;
- labels, text, logo, UI, watermark, symbols, border, or frame;
- baked highlights, baked shadows, lighting gradient, directional cast shadow, vignette, or photographic glare;
- crayon/procedural scribbles, random noise sold as material, smooth blob rock, or low-resolution bands;
- flat plastic wetness, chrome wetness, uniform glossy overlay, or muddy dark cover-up;
- black/noir darkness used to hide weak surface/coastline detail;
- obvious repeated hero cracks, plates, bubbles, stains, or mineral islands;
- albedo color in mask maps;
- false metallic on ordinary basalt, salt, foam, sediment, or non-ore rock;
- foam as opaque snow strips or broad dirty storm grime by default.

## Edge Diff Gate

Use 8-bit mean absolute edge difference unless the source is HDR/float.

```text
left_right_edge_diff = mean(abs(pixel[x=0,y] - pixel[x=width-1,y]))
top_bottom_edge_diff = mean(abs(pixel[x,y=0] - pixel[x,y=height-1]))
```

Pass targets:

- Preferred production source target: `<= 8.0`.
- Review band: `> 8.0` and `<= 12.0`; manual 3x3 must show no visible seam at intended scale.
- Reject band: `> 12.0` or any visible hard seam.
- Automatic reject: `> 20.0`, unless a channel-specific reviewer proves visual continuity for a special signed/vector map.

## Albedo Gate

Acceptance:

- sRGB source intent.
- Base color only.
- Wet basalt black/gray range with mineral/salt/sediment variation.
- Bright photic readability; not crushed black.
- No baked shadows, highlights, or directional lighting.

Reject:

- beauty render with light direction;
- cliff or rock scene photo;
- pure black/noir cover-up;
- flat gray procedural noise;
- wetness represented as painted glare.

## Normal / Height Gate

Acceptance:

- Tangent-space normal source or height/relief source suitable for offline normal derivation.
- Relief follows cracks, pores, chipped edges, ridges, eroded basalt grain, and foam/salt contact where applicable.
- Edge continuity passes raw and visual checks.
- No albedo color, lighting, AO, or fake shadow.

Reject:

- embossing color stains without physical relief logic;
- inverted-looking cracks or pillow basalt;
- flat normal with no useful relief;
- sparkle noise that will break under compression/mips.

## Roughness Gate

Acceptance:

- Grayscale linear source.
- Wet cracks and damp mineral stains are smoother under roughness convention.
- Dry chips, salt crust, and eroded pores are rougher.
- Not constant gray.
- Not identical to AO.

Reject:

- full black mirror surface;
- full white chalk surface;
- random dirt map;
- smoothness map mislabeled as roughness without documented inversion.

## AO Gate

Acceptance:

- Grayscale linear source.
- Cavity-biased only.
- Cracks, pores, undercuts, sediment pockets, and fracture intersections darken.
- Exposed planes remain light.

Reject:

- broad dirty overlay;
- vignette or directional shadow;
- same image as roughness;
- black/noir map used to hide weak albedo.

## Packed MRAO / Wetness Gate

Source contract for 2101:

- R = Metallic.
- G = Roughness.
- B = AO.
- A = Wetness/family mask unless target shader rejects alpha wetness.

Basalt sanity:

- R must be black/zero.
- G must match accepted roughness source.
- B must match accepted cavity AO source.
- A must be documented as wetness/family/unused before import.
- Linear import intent, not sRGB.
- Channels must be independently inspected.

Reject:

- metallic whole rock;
- color albedo packed into RGB;
- roughness and AO identical by accident;
- alpha used as random grime without shader contract;
- filename-based ORM/ARM/MRAO assumptions without channel proof.

## Waterline Wetness Mask Gate

Suggested source contract:

- R = wetness strength.
- G = drying falloff / transition softness.
- B = salt, sediment, and mineral breakup.
- A = specular boost or reserved confidence mask only if accepted.

Acceptance:

- irregular wet/dry transition;
- crack-following salt/mineral/sediment breakup;
- surface brightness and material identity preserved.

Reject:

- hard straight black stripe;
- muddy dark waterline cover-up;
- uniform plastic gloss;
- mask changing terrain/gameplay truth.

## Foam / Contact Mask Gate

Suggested source contract:

- R = long foam strand / contact strength.
- G = cross-flow wet edge breakup.
- B = foam lace, bubbles, sediment/salt interruption.
- A = optional caustic receiver or confidence mask only if accepted.

Acceptance:

- thin foam lace and broken strands;
- sparse translucent contact breakup;
- not a full white strip;
- believable bright shoreline use.

Reject:

- opaque snow foam;
- flat white bands;
- storm grime default;
- perspective wave photo;
- repeated bubble stamps.

## Future Proof Required

- [ ] Albedo-only preview.
- [ ] Normal-only preview.
- [ ] Mask-channel preview.
- [ ] Flat-light preview.
- [ ] Grazing-light preview.
- [ ] Final URP-lit preview.
- [ ] Shoreline close capture.
- [ ] Shoreline wide capture.
- [ ] Waterline edge capture.
- [ ] Low/compact capture.
- [ ] Frame Debugger/RenderGraph proof if shader/render route changes.
- [ ] Profiler/GC/memory/VRAM proof if runtime, residency, or material binding behavior changes.

Static docs or source files alone can only claim `STATIC VERIFIED`.

