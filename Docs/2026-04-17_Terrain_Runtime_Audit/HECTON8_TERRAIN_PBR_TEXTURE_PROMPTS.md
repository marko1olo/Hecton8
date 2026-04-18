# HECTON-8 Terrain / Mineral PBR Prompt Pack

Status: `PENDING VERIFICATION`
Date: `2026-04-17`
Goal: production-grade prompts for terrain and mineral texture generation for the sharper MapMagic terrain pass, with separate generation prompts for `Albedo / Normal / Roughness`.

## Global Prompt Rules

Append these constraints to every generation run if the model tends to drift:

- seamless
- tileable
- 8k
- PBR material texture set
- no camera perspective
- no object render
- no scene composition
- no text
- no labels
- no borders

## 1. Underwater Basalt Rocks

### Albedo

`Seamless tileable 8k PBR albedo texture for underwater basalt rock seabed, dark volcanic basalt, fractured planes, pressure-worn edges, mineral micro-pitting, subtle cold-water oxidation, damp deep-sea surface breakup, realistic macro and micro detail balance, lighting-neutral flat color information only, no baked highlights, no baked shadows, no ambient occlusion shading, no normal-map lighting, no scene composition, no camera angle, pure albedo map`

### Normal

`Seamless tileable 8k tangent-space normal map for underwater basalt rock seabed, fractured basalt planes, chipped edges, mineral micro-pitting, pressure-worn erosion, crisp medium and fine surface relief, clean physically plausible rock height translation, neutral blue normal map appearance, no color or albedo information, no lighting, no shadows, no scene composition, pure normal map`

### Roughness

`Seamless tileable 8k PBR roughness texture for underwater basalt rock seabed, dark volcanic basalt with fractured planes, subtle wetness variation, rough eroded stone mixed with slightly smoother pressure-polished edges, mineral micro-pitting, realistic grayscale roughness breakup only, no albedo color, no lighting, no scene composition, pure roughness map`

## 2. Deep Sea Sand with Ripples

### Albedo

`Seamless tileable 8k PBR albedo texture for deep sea sand with current-formed ripple patterns, fine cold-ocean sediment, soft ridge repetition, subtle shell dust and micro-grit breakup, slightly damp compacted troughs, realistic underwater seabed surface, lighting-neutral flat color only, no baked highlights, no baked shadows, no depth shading, no scene framing, pure albedo map`

### Normal

`Seamless tileable 8k tangent-space normal map for deep sea sand with current-formed ripple patterns, fine sediment ridges, soft troughs, subtle shell dust relief, clean medium-frequency surface waves, physically plausible seabed height translation, neutral blue normal map appearance, no color information, no lighting, no scene framing, pure normal map`

### Roughness

`Seamless tileable 8k PBR roughness texture for deep sea sand with ripple patterns, slightly damp compacted troughs, drier fine-grain ridges, subtle shell dust and micro-grit variation, believable grayscale roughness breakup for underwater sediment, no color, no lighting, no scene framing, pure roughness map`

## 3. Glowing Mineral Vein (Lapis / Copper Style)

### Albedo

`Seamless tileable 8k PBR albedo texture for alien deep-sea glowing mineral vein embedded in dark host rock, lapis and oxidized copper inspired coloration, rich cobalt blue and copper-green mineral channels, crystalline fracture lines, wet conductive seams, emissive-friendly vein layout but no bloom, lighting-neutral flat color only, no baked highlights, no baked shadows, no environment scene, pure albedo map`

### Normal

`Seamless tileable 8k tangent-space normal map for alien mineral vein embedded in dark host rock, crystalline fracture lines, sharp mineral seams, chipped stone edges, recessed conductive channels, strong structural relief for polished mineral against rough rock, neutral blue normal map appearance, no color information, no lighting, no environment scene, pure normal map`

### Roughness

`Seamless tileable 8k PBR roughness texture for alien mineral vein in dark host rock, strong grayscale contrast between smoother polished mineral channels and rough matte stone, wet conductive seams, crystalline fracture variation, no color, no bloom, no lighting, no environment scene, pure roughness map`

## 4. Coral Surface Macro

### Albedo

`Seamless tileable 8k PBR albedo texture for coral surface macro detail, dense calcified pores, organic ridges, subtle growth banding, eroded reef skin, fine biological surface complexity, believable hard-soft transitions, underwater material only, lighting-neutral flat color only, no baked highlights, no baked shadows, no specimen framing, pure albedo map`

### Normal

`Seamless tileable 8k tangent-space normal map for coral surface macro detail, dense calcified pores, organic ridges, growth banding, eroded reef skin, fine biological relief, believable hard-soft transitions, strong pore and ridge definition, neutral blue normal map appearance, no color information, no lighting, no specimen framing, pure normal map`

### Roughness

`Seamless tileable 8k PBR roughness texture for coral surface macro detail, nuanced grayscale variation across calcified chalky areas and slightly moist smoother patches, pores, ridges, eroded reef skin, no color, no lighting, no specimen framing, pure roughness map`

## 5. Alien Seabed Crust

### Albedo

`Seamless tileable 8k PBR albedo texture for alien seabed crust, cracked mineral-biological sediment plate, sulfuric and saline residue hints, dark abyssal floor accretion, irregular crust islands, fine connective seams, subtle xenobiological contamination patterns, grounded and believable, lighting-neutral flat color only, no baked highlights, no baked shadows, no props, pure albedo map`

### Normal

`Seamless tileable 8k tangent-space normal map for alien seabed crust, cracked sediment plate, crust islands, fine connective seams, layered accretion, fissures, mineral-biological surface relief, strong but believable micro and medium detail, neutral blue normal map appearance, no color, no lighting, no props, pure normal map`

### Roughness

`Seamless tileable 8k PBR roughness texture for alien seabed crust, complex grayscale breakup from chalky crust to damp fissures, mineral residue, saline deposits, abyssal accretion variation, no color information, no lighting, no props, pure roughness map`

## Shared Negative Prompt

`object render, diorama, landscape shot, perspective, dramatic lighting, shadow cast, text, labels, frame, border, UI, glossy beauty render, concept art composition, cinematic scene, non-seamless pattern, visible tile seam`
