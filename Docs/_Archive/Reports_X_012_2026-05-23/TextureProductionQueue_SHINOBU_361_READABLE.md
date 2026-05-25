# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# SHINOBU_361 Texture Production Queue - Readable

Status: PENDING VERIFICATION
Evidence class: STATIC_SOURCE
Purpose: readable art-production companion to `TextureProductionQueue_SHINOBU_361.csv`.

## Summary
- Unique target textures: 175
- Source slot references collapsed: 413
- Prompt contract: natural English only; no weighted, bracketed, or legacy generator syntax.
- View contract: every prompt requires flat, top-down, orthogonal orthographic view, uniform diffuse lighting, zero directional shadows, and seamless tiling.
- Dear Lie contract: rivets, seams, salt, pores, membrane veins, cracks, and weld lips are baked into albedo, BC5 normal, and packed ORM maps instead of geometry.

## Category Counts
- FLORA_EPIDERMIS: 26
- GEOLOGY_TRIPLANAR: 23
- HABITAT_INTERIORS: 126

## Texture Role Counts
- ALBEDO: 153
- AO: 3
- EMISSIVE: 1
- NORMAL: 12
- ORM: 1
- ROUGHNESS: 1
- TEXTURE: 4

## Resolution Counts
- 512: 6
- 1024: 149
- 2048: 20

## Action Counts
- GENERATE_REPLACEMENT_PBR: 171
- REBAKE_SOURCE_TO_PNG_AND_FIX_IMPORT: 4

## Generation Rules
- Albedo: generate exactly the prompt paragraph; keep diffuse dark and flat-lit; do not paint directional highlights.
- Normal: follow the BC5 normal plan; use luminance extraction only for shallow detail and dedicated normal prompts for rivets, grates, bevels, and deep cracks.
- ORM: pack AO in Red, Roughness in Green, Metallic in Blue; keep linear, mipmapped, and BC7 on Standalone.
- Android/mobile: import generated maps with ASTC_6x6 unless a platform capture proves a tighter format is required.


## HABITAT_INTERIORS Batch B

### 001. Mat_HectonSurface_Normal.png

- Queue ID: `SHINOBU_361_QUEUE_0015`
- Action: `REBAKE_SOURCE_TO_PNG_AND_FIX_IMPORT`
- Texture role: `NORMAL`
- Priority: `BLOCKER`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HectonSurface_Normal.png`
- Source paths: `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/Mat_HectonSurface.mat`
- Slots: `_BumpMap`
- Reference states: `IMPORT_ISSUE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat HectonSurface, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan:
Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 002. Mat_Visor_Glass_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0068`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `BLOCKER`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Visor_Glass_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Mat_Visor_Glass.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Visor Glass, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 003. ceiling_10_trimsheet_normal_Normal.png

- Queue ID: `SHINOBU_361_QUEUE_0002`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `NORMAL`
- Priority: `BLOCKER`
- Resolution: `1024`
- Source count: `6`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/ceiling_10_trimsheet_normal_Normal.png`
- Source paths: `Assets/_Project/_PROLOGUE_CONTENT/Models/ceiling_10.fbx`
- Slots: `EmbeddedTexturePath_0;EmbeddedTexturePath_1;EmbeddedTexturePath_2;EmbeddedTexturePath_3;EmbeddedTexturePath_4;EmbeddedTexturePath_5`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for ceiling 10, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan:
Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 004. floor_05_stripes_basecolor_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0004`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `BLOCKER`
- Resolution: `1024`
- Source count: `6`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_05_stripes_basecolor_Albedo.png`
- Source paths: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_05.fbx`
- Slots: `EmbeddedTexturePath_3;EmbeddedTexturePath_4;EmbeddedTexturePath_5;EmbeddedTexturePath_9;EmbeddedTexturePath_10;EmbeddedTexturePath_11`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for floor 05, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 005. floor_05_trimsheet_normal_Normal.png

- Queue ID: `SHINOBU_361_QUEUE_0003`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `NORMAL`
- Priority: `BLOCKER`
- Resolution: `1024`
- Source count: `6`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_05_trimsheet_normal_Normal.png`
- Source paths: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_05.fbx`
- Slots: `EmbeddedTexturePath_0;EmbeddedTexturePath_1;EmbeddedTexturePath_2;EmbeddedTexturePath_6;EmbeddedTexturePath_7;EmbeddedTexturePath_8`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for floor 05, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan:
Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 006. floor_large_8x8_trimsheet_normal_Normal.png

- Queue ID: `SHINOBU_361_QUEUE_0005`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `NORMAL`
- Priority: `BLOCKER`
- Resolution: `1024`
- Source count: `6`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_large_8x8_trimsheet_normal_Normal.png`
- Source paths: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_large_8x8.fbx`
- Slots: `EmbeddedTexturePath_0;EmbeddedTexturePath_1;EmbeddedTexturePath_2;EmbeddedTexturePath_3;EmbeddedTexturePath_4;EmbeddedTexturePath_5`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for floor large 8x8, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan:
Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 007. wall_01_2x3_a_stripes_basecolor_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0007`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `BLOCKER`
- Resolution: `1024`
- Source count: `6`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_2x3_a_stripes_basecolor_Albedo.png`
- Source paths: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_2x3_a.fbx`
- Slots: `EmbeddedTexturePath_3;EmbeddedTexturePath_4;EmbeddedTexturePath_5;EmbeddedTexturePath_9;EmbeddedTexturePath_10;EmbeddedTexturePath_11`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for wall 01 2x3 a, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 008. wall_01_2x3_a_trimsheet_normal_Normal.png

- Queue ID: `SHINOBU_361_QUEUE_0006`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `NORMAL`
- Priority: `BLOCKER`
- Resolution: `1024`
- Source count: `6`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_2x3_a_trimsheet_normal_Normal.png`
- Source paths: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_2x3_a.fbx`
- Slots: `EmbeddedTexturePath_0;EmbeddedTexturePath_1;EmbeddedTexturePath_2;EmbeddedTexturePath_6;EmbeddedTexturePath_7;EmbeddedTexturePath_8`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for wall 01 2x3 a, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan:
Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 009. wall_01_4x3_c_labels_basecolor_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0010`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `BLOCKER`
- Resolution: `1024`
- Source count: `5`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_labels_basecolor_Albedo.png`
- Source paths: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slots: `EmbeddedTexturePath_6;EmbeddedTexturePath_7;EmbeddedTexturePath_14;EmbeddedTexturePath_15;EmbeddedTexturePath_16`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 010. wall_01_4x3_c_stripes_basecolor_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0009`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `BLOCKER`
- Resolution: `1024`
- Source count: `6`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_stripes_basecolor_Albedo.png`
- Source paths: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slots: `EmbeddedTexturePath_3;EmbeddedTexturePath_4;EmbeddedTexturePath_5;EmbeddedTexturePath_11;EmbeddedTexturePath_12;EmbeddedTexturePath_13`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 011. wall_01_4x3_c_trimsheet_normal_Normal.png

- Queue ID: `SHINOBU_361_QUEUE_0008`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `NORMAL`
- Priority: `BLOCKER`
- Resolution: `1024`
- Source count: `6`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_trimsheet_normal_Normal.png`
- Source paths: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slots: `EmbeddedTexturePath_0;EmbeddedTexturePath_1;EmbeddedTexturePath_2;EmbeddedTexturePath_8;EmbeddedTexturePath_9;EmbeddedTexturePath_10`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan:
Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 012. wall_01_4x3_door_02_wing_labels_basecolor_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0013`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `BLOCKER`
- Resolution: `1024`
- Source count: `6`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_labels_basecolor_Albedo.png`
- Source paths: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slots: `EmbeddedTexturePath_6;EmbeddedTexturePath_7;EmbeddedTexturePath_8;EmbeddedTexturePath_15;EmbeddedTexturePath_16;EmbeddedTexturePath_17`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 013. wall_01_4x3_door_02_wing_stripes_basecolor_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0011`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `BLOCKER`
- Resolution: `1024`
- Source count: `6`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_stripes_basecolor_Albedo.png`
- Source paths: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slots: `EmbeddedTexturePath_0;EmbeddedTexturePath_1;EmbeddedTexturePath_2;EmbeddedTexturePath_9;EmbeddedTexturePath_10;EmbeddedTexturePath_11`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 014. wall_01_4x3_door_02_wing_trimsheet_normal_Normal.png

- Queue ID: `SHINOBU_361_QUEUE_0012`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `NORMAL`
- Priority: `BLOCKER`
- Resolution: `1024`
- Source count: `6`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_trimsheet_normal_Normal.png`
- Source paths: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slots: `EmbeddedTexturePath_3;EmbeddedTexturePath_4;EmbeddedTexturePath_5;EmbeddedTexturePath_12;EmbeddedTexturePath_13;EmbeddedTexturePath_14`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan:
Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 015. wall_04_3x6_d_trimsheet_normal_Normal.png

- Queue ID: `SHINOBU_361_QUEUE_0014`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `NORMAL`
- Priority: `BLOCKER`
- Resolution: `1024`
- Source count: `6`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_04_3x6_d_trimsheet_normal_Normal.png`
- Source paths: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_04_3x6_d.fbx`
- Slots: `EmbeddedTexturePath_0;EmbeddedTexturePath_1;EmbeddedTexturePath_2;EmbeddedTexturePath_3;EmbeddedTexturePath_4;EmbeddedTexturePath_5`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for wall 04 3x6 d, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan:
Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

## FLORA_EPIDERMIS

### 016. MAT_FloraProxy_Coral_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0047`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_FloraProxy_Coral_Albedo.png`
- Source paths: `Assets/_Project/Data/Flora/GeneratedProxies/Materials/MAT_FloraProxy_Coral.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT FloraProxy Coral, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 017. MAT_FloraProxy_Kelp_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0048`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_FloraProxy_Kelp_Albedo.png`
- Source paths: `Assets/_Project/Data/Flora/GeneratedProxies/Materials/MAT_FloraProxy_Kelp.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT FloraProxy Kelp, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 018. MAT_FloraProxy_MicroGrass_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0049`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_FloraProxy_MicroGrass_Albedo.png`
- Source paths: `Assets/_Project/Data/Flora/GeneratedProxies/Materials/MAT_FloraProxy_MicroGrass.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT FloraProxy MicroGrass, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 019. MAT_FloraProxy_Sargassum_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0050`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_FloraProxy_Sargassum_Albedo.png`
- Source paths: `Assets/_Project/Data/Flora/GeneratedProxies/Materials/MAT_FloraProxy_Sargassum.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT FloraProxy Sargassum, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 020. MAT_family_coral_branching_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0132`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_branching_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat`
- Slots: `_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT family coral branching, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 021. MAT_family_coral_branching_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0030`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_branching_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_coral_branching_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT family coral branching Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 022. MAT_family_coral_brittle_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0133`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_brittle_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_brittle.mat`
- Slots: `_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT family coral brittle, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 023. MAT_family_coral_low_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0134`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_low_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_low.mat`
- Slots: `_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT family coral low, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 024. MAT_family_coral_low_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0031`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_low_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_coral_low_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT family coral low Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 025. MAT_family_coral_massive_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0135`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_massive_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_massive.mat`
- Slots: `_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT family coral massive, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 026. MAT_family_coral_massive_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0032`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_massive_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_coral_massive_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT family coral massive Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 027. MAT_family_coral_plate_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0136`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_plate_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_plate.mat`
- Slots: `_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT family coral plate, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 028. MAT_family_coral_plate_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0033`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_plate_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_coral_plate_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT family coral plate Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 029. MAT_family_kelp_abyssal_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0146`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_abyssal_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_abyssal.mat`
- Slots: `_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT family kelp abyssal, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 030. MAT_family_kelp_canopy_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0147`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_canopy_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_canopy.mat`
- Slots: `_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT family kelp canopy, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 031. MAT_family_kelp_canopy_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0034`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_canopy_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_kelp_canopy_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT family kelp canopy Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 032. MAT_family_kelp_patch_dense_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0148`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_patch_dense_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_patch_dense.mat`
- Slots: `_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT family kelp patch dense, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 033. MAT_family_kelp_patch_dense_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0035`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_patch_dense_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_kelp_patch_dense_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT family kelp patch dense Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 034. MAT_family_kelp_tall_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0149`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_tall_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_tall.mat`
- Slots: `_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT family kelp tall, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 035. MAT_family_kelp_tall_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0036`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_tall_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_kelp_tall_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT family kelp tall Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 036. MAT_family_plant_giant_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0151`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_plant_giant_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_plant_giant.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT family plant giant, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 037. MAT_family_plant_giant_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0037`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_plant_giant_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_plant_giant_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for MAT family plant giant Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 038. Mat_Organic_PlantBud_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0173`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/Mat_Organic_PlantBud_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_PlantBud.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for Mat Organic PlantBud, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 039. Mat_Organic_PlantCanopy_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0174`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/Mat_Organic_PlantCanopy_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_PlantCanopy.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for Mat Organic PlantCanopy, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 040. Mat_Organic_PlantStem_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0175`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/Mat_Organic_PlantStem_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_PlantStem.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for Mat Organic PlantStem, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### 041. Mat_Resource_Membrane_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0112`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/Mat_Resource_Membrane_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Membrane.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Beautiful abyssal flora epidermis for Mat Resource Membrane, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

## GEOLOGY_TRIPLANAR

### 042. MAT_family_cave_entrance_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0131`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_cave_entrance_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_cave_entrance.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for MAT family cave entrance, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 043. MAT_family_cave_entrance_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0043`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_cave_entrance_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/TerrainLod/MAT_family_cave_entrance_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for MAT family cave entrance Placeholder, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 044. MAT_family_landmark_spire_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0044`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_landmark_spire_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/TerrainLod/MAT_family_landmark_spire_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for MAT family landmark spire Placeholder, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 045. MAT_family_rock_cluster_medium_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0045`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_rock_cluster_medium_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/TerrainLod/MAT_family_rock_cluster_medium_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for MAT family rock cluster medium Placeholder, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 046. MAT_family_rock_shelf_large_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0155`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_rock_shelf_large_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_rock_shelf_large.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for MAT family rock shelf large, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 047. MAT_family_rock_small_floor_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0046`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_rock_small_floor_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/TerrainLod/MAT_family_rock_small_floor_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for MAT family rock small floor Placeholder, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 048. Mat_Tool_Flashlight_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0121`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Mat_Tool_Flashlight_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Tools/Mat_Tool_Flashlight_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for Mat Tool Flashlight Placeholder, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 049. Mat_TriplanarRock_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0067`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `3`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Mat_TriplanarRock_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Mat_TriplanarRock.mat`
- Slots: `_BaseMap;_MainTex;_Rock_Albedo`
- Reference states: `EMPTY_REQUIRED_SLOT;MISSING_GUID`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for Mat TriplanarRock, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 050. River_Rock_FBX_riverrock_basecolor_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0092`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `6`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/River_Rock_FBX_riverrock_basecolor_Albedo.png`
- Source paths: `Assets/_Project/Art/Models/Rocks/Rock 5/orig/River_Rock_FBX.fbx`
- Slots: `EmbeddedTexturePath_0;EmbeddedTexturePath_1;EmbeddedTexturePath_2;EmbeddedTexturePath_3;EmbeddedTexturePath_4;EmbeddedTexturePath_5`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for River Rock FBX, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 051. Rock_4_t_rock_4_basecolor_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0089`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `6`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_basecolor_Albedo.png`
- Source paths: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slots: `EmbeddedTexturePath_0;EmbeddedTexturePath_1;EmbeddedTexturePath_2;EmbeddedTexturePath_9;EmbeddedTexturePath_10;EmbeddedTexturePath_11`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 052. Rock_4_t_rock_4_normal_Normal.png

- Queue ID: `SHINOBU_361_QUEUE_0090`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `NORMAL`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `6`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_normal_Normal.png`
- Source paths: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slots: `EmbeddedTexturePath_3;EmbeddedTexturePath_4;EmbeddedTexturePath_5;EmbeddedTexturePath_12;EmbeddedTexturePath_13;EmbeddedTexturePath_14`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan:
Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 053. Rock_4_t_rock_4_roughness_ORM.png

- Queue ID: `SHINOBU_361_QUEUE_0091`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ROUGHNESS`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `6`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_roughness_ORM.png`
- Source paths: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slots: `EmbeddedTexturePath_6;EmbeddedTexturePath_7;EmbeddedTexturePath_8;EmbeddedTexturePath_15;EmbeddedTexturePath_16;EmbeddedTexturePath_17`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The surface must separate cavities, worn rough patches, and metallic substrate clearly enough to derive packed ORM masks.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 054. SAMMPLE_1_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0082`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `TEXTURE`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `7`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/SAMMPLE_1_Albedo.png`
- Source paths: `Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx`
- Slots: `EmbeddedTexturePath_2;EmbeddedTexturePath_3;EmbeddedTexturePath_9;EmbeddedTexturePath_10;EmbeddedTexturePath_11;EmbeddedTexturePath_12;EmbeddedTexturePath_13`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for SAMMPLE, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 055. SAMMPLE_image_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0081`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `TEXTURE`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `7`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/SAMMPLE_image_Albedo.png`
- Source paths: `Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx`
- Slots: `EmbeddedTexturePath_0;EmbeddedTexturePath_1;EmbeddedTexturePath_4;EmbeddedTexturePath_5;EmbeddedTexturePath_6;EmbeddedTexturePath_7;EmbeddedTexturePath_8`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for SAMMPLE, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 056. mat_Rock2_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0083`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock2_Albedo.png`
- Source paths: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock2.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `MISSING_GUID`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for mat Rock2, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 057. mat_Rock2_Normal.png

- Queue ID: `SHINOBU_361_QUEUE_0084`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `NORMAL`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock2_Normal.png`
- Source paths: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock2.mat`
- Slots: `_BumpMap`
- Reference states: `MISSING_GUID`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for mat Rock2, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan:
Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 058. mat_Rock2_ORM.png

- Queue ID: `SHINOBU_361_QUEUE_0085`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `AO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock2_ORM.png`
- Source paths: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock2.mat`
- Slots: `_OcclusionMap`
- Reference states: `MISSING_GUID`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for mat Rock2, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The surface must separate cavities, worn rough patches, and metallic substrate clearly enough to derive packed ORM masks.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 059. mat_Rock_Shared_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0086`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock_Shared_Albedo.png`
- Source paths: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock_Shared.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `MISSING_GUID`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for mat Rock Shared, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 060. mat_Rock_Shared_Normal.png

- Queue ID: `SHINOBU_361_QUEUE_0087`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `NORMAL`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock_Shared_Normal.png`
- Source paths: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock_Shared.mat`
- Slots: `_BumpMap`
- Reference states: `MISSING_GUID`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for mat Rock Shared, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan:
Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 061. mat_Rock_Shared_ORM.png

- Queue ID: `SHINOBU_361_QUEUE_0088`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `AO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock_Shared_ORM.png`
- Source paths: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock_Shared.mat`
- Slots: `_OcclusionMap`
- Reference states: `MISSING_GUID`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for mat Rock Shared, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The surface must separate cavities, worn rough patches, and metallic substrate clearly enough to derive packed ORM masks.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 062. terrain_1_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0075`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/terrain_1_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/terrain 1.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for terrain 1, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 063. terrain_2_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0076`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/terrain_2_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/terrain 2.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for terrain 2, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### 064. terrain_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0077`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `2048`
- Source count: `3`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/terrain_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/terrain.mat`
- Slots: `_BaseMap;_MainTex;_Rock_Albedo`
- Reference states: `MISSING_GUID`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: Striking alien seafloor geology texture for terrain, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

## HABITAT_INTERIORS

### 065. MAT_DiegeticTooltipGlyph_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0016`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_DiegeticTooltipGlyph_Albedo.png`
- Source paths: `Assets/_Project/Resources/UI/MAT_DiegeticTooltipGlyph.mat`
- Slots: `_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT DiegeticTooltipGlyph, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 066. MAT_DiegeticTooltipIcon_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0017`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_DiegeticTooltipIcon_Albedo.png`
- Source paths: `Assets/_Project/Resources/UI/MAT_DiegeticTooltipIcon.mat`
- Slots: `_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT DiegeticTooltipIcon, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 067. MAT_ErrorCube_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0108`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_ErrorCube_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Diagnostics/MAT_ErrorCube.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT ErrorCube, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 068. MAT_LightningBolt_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0130`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_LightningBolt_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/VFX/MAT_LightningBolt.mat`
- Slots: `_BaseMap`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT LightningBolt, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 069. MAT_PlayerSwimBlockout_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0109`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_PlayerSwimBlockout_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Gameplay/MAT_PlayerSwimBlockout.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT PlayerSwimBlockout, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 070. MAT_ProceduralBio_Shallows_ORM.png

- Queue ID: `SHINOBU_361_QUEUE_0161`
- Action: `REBAKE_SOURCE_TO_PNG_AND_FIX_IMPORT`
- Texture role: `ORM`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_ProceduralBio_Shallows_ORM.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_ProceduralBio_Shallows.mat`
- Slots: `_ORMAtlas`
- Reference states: `IMPORT_ISSUE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT ProceduralBio Shallows, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The surface must separate cavities, worn rough patches, and metallic substrate clearly enough to derive packed ORM masks.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 071. MAT_SargassumFoamDamping_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0061`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_SargassumFoamDamping_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/MAT_SargassumFoamDamping.mat`
- Slots: `_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT SargassumFoamDamping, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 072. MAT_SargassumMicroFaunaBoids_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0062`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_SargassumMicroFaunaBoids_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/MAT_SargassumMicroFaunaBoids.mat`
- Slots: `_BaseMap`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT SargassumMicroFaunaBoids, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 073. MAT_SargassumOilFilm_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0063`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_SargassumOilFilm_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/MAT_SargassumOilFilm.mat`
- Slots: `_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT SargassumOilFilm, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 074. MAT_SargassumWaveDamping_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0064`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_SargassumWaveDamping_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/MAT_SargassumWaveDamping.mat`
- Slots: `_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT SargassumWaveDamping, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 075. MAT_family_creature_spawn_passive_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0137`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_spawn_passive_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_spawn_passive.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family creature spawn passive, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 076. MAT_family_creature_spawn_passive_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0027`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_spawn_passive_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Fauna/MAT_family_creature_spawn_passive_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family creature spawn passive Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 077. MAT_family_creature_spawn_predator_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0138`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_spawn_predator_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_spawn_predator.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family creature spawn predator, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 078. MAT_family_creature_spawn_predator_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0028`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_spawn_predator_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Fauna/MAT_family_creature_spawn_predator_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family creature spawn predator Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 079. MAT_family_creature_zone_abyss_apex_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0139`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_abyss_apex_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_zone_abyss_apex.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family creature zone abyss apex, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 080. MAT_family_creature_zone_abyss_apex_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0038`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_abyss_apex_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/LargeThreats/MAT_family_creature_zone_abyss_apex_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family creature zone abyss apex Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 081. MAT_family_creature_zone_large_threat_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0140`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_large_threat_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_zone_large_threat.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family creature zone large threat, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 082. MAT_family_creature_zone_large_threat_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0039`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_large_threat_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/LargeThreats/MAT_family_creature_zone_large_threat_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family creature zone large threat Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 083. MAT_family_creature_zone_reef_apex_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0141`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_reef_apex_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_zone_reef_apex.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family creature zone reef apex, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 084. MAT_family_creature_zone_reef_apex_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0040`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_reef_apex_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/LargeThreats/MAT_family_creature_zone_reef_apex_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family creature zone reef apex Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 085. MAT_family_creature_zone_ruin_apex_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0142`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_ruin_apex_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_zone_ruin_apex.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family creature zone ruin apex, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 086. MAT_family_creature_zone_ruin_apex_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0041`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_ruin_apex_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/LargeThreats/MAT_family_creature_zone_ruin_apex_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family creature zone ruin apex Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 087. MAT_family_debris_field_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0143`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_debris_field_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_debris_field.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family debris field, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 088. MAT_family_debris_field_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0025`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_debris_field_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Debris/MAT_family_debris_field_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family debris field Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 089. MAT_family_debris_scatter_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0144`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_debris_scatter_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_debris_scatter.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family debris scatter, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 090. MAT_family_debris_scatter_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0026`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_debris_scatter_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Debris/MAT_family_debris_scatter_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family debris scatter Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 091. MAT_family_egg_cluster_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0145`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_egg_cluster_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_egg_cluster.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family egg cluster, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 092. MAT_family_egg_cluster_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0029`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_egg_cluster_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Fauna/MAT_family_egg_cluster_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family egg cluster Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 093. MAT_family_landmark_spire_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0150`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_landmark_spire_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_landmark_spire.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family landmark spire, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 094. MAT_family_pocket_hazard_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0152`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_hazard_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_pocket_hazard.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family pocket hazard, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 095. MAT_family_pocket_hazard_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0018`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_hazard_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_pocket_hazard_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family pocket hazard Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 096. MAT_family_pocket_resource_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0153`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_resource_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_pocket_resource.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family pocket resource, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 097. MAT_family_pocket_resource_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0042`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_resource_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Resources/MAT_family_pocket_resource_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family pocket resource Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 098. MAT_family_pocket_safe_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0154`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_safe_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_pocket_safe.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family pocket safe, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 099. MAT_family_pocket_safe_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0019`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_safe_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_pocket_safe_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family pocket safe Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 100. MAT_family_route_power_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0156`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_route_power_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_route_power.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family route power, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 101. MAT_family_route_power_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0020`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_route_power_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_route_power_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family route power Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 102. MAT_family_ruin_cluster_medium_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0157`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_cluster_medium_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_ruin_cluster_medium.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family ruin cluster medium, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 103. MAT_family_ruin_cluster_medium_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0021`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_cluster_medium_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_ruin_cluster_medium_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family ruin cluster medium Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 104. MAT_family_ruin_megastructure_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0158`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_megastructure_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_ruin_megastructure.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family ruin megastructure, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 105. MAT_family_ruin_megastructure_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0022`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_megastructure_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_ruin_megastructure_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family ruin megastructure Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 106. MAT_family_ruin_module_single_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0159`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_module_single_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_ruin_module_single.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family ruin module single, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 107. MAT_family_ruin_module_single_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0023`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_module_single_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_ruin_module_single_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family ruin module single Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 108. MAT_family_service_scar_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0160`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_service_scar_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_service_scar.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family service scar, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 109. MAT_family_service_scar_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0024`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_service_scar_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_service_scar_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT family service scar Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 110. MAT_sargassum_leaf_scraps_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0162`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_sargassum_leaf_scraps_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_sargassum_leaf_scraps.mat`
- Slots: `_BaseMap`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for MAT sargassum leaf scraps, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 111. Mat_BuildGhost_Invalid_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0093`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_BuildGhost_Invalid_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Construction/Mat_BuildGhost_Invalid.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat BuildGhost Invalid, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 112. Mat_BuildGhost_Valid_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0094`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_BuildGhost_Valid_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Construction/Mat_BuildGhost_Valid.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat BuildGhost Valid, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 113. Mat_DroneProxy_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0051`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_DroneProxy_Albedo.png`
- Source paths: `Assets/_Project/Data/AI/GeneratedProxies/Materials/Mat_DroneProxy.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat DroneProxy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 114. Mat_HeavyHunterProxy_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0052`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HeavyHunterProxy_Albedo.png`
- Source paths: `Assets/_Project/Data/AI/GeneratedProxies/Materials/Mat_HeavyHunterProxy.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat HeavyHunterProxy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 115. Mat_HectonSky_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0059`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `4`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HectonSky_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
- Slots: `_BaseMap;_HighCloudTex;_MainCloudAtlas;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT;MISSING_GUID`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat HectonSky, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 116. Mat_HectonSky_CloudOverlay_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0060`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `4`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HectonSky_CloudOverlay_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Mat_HectonSky_CloudOverlay.mat`
- Slots: `_BaseMap;_HighCloudTex;_MainCloudAtlas;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT;MISSING_GUID`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat HectonSky CloudOverlay, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 117. Mat_HunterProxy_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0053`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HunterProxy_Albedo.png`
- Source paths: `Assets/_Project/Data/AI/GeneratedProxies/Materials/Mat_HunterProxy.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat HunterProxy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 118. Mat_LeakWetSheen_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0095`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_LeakWetSheen_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Construction/Mat_LeakWetSheen.mat`
- Slots: `_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat LeakWetSheen, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 119. Mat_LeviathanProxy_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0054`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_LeviathanProxy_Albedo.png`
- Source paths: `Assets/_Project/Data/AI/GeneratedProxies/Materials/Mat_LeviathanProxy.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat LeviathanProxy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 120. Mat_Module_Corridor_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0096`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_Corridor_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Construction/Mat_Module_Corridor.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Module Corridor, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 121. Mat_Module_CurrentTurbine_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0097`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_CurrentTurbine_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Construction/Mat_Module_CurrentTurbine.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Module CurrentTurbine, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 122. Mat_Module_Foundation_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0098`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_Foundation_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Construction/Mat_Module_Foundation.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Module Foundation, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 123. Mat_Module_Pylon_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0099`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_Pylon_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Construction/Mat_Module_Pylon.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Module Pylon, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 124. Mat_Module_ServicePump_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0100`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_ServicePump_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Construction/Mat_Module_ServicePump.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Module ServicePump, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 125. Mat_Ocean_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0001`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Ocean_Albedo.png`
- Source paths: `Assets/_Project/_Archive/Mat_Ocean.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Ocean, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 126. Mat_Organic_EggNest_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0171`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Organic_EggNest_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_EggNest.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Organic EggNest, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 127. Mat_Organic_EggShell_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0172`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Organic_EggShell_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_EggShell.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Organic EggShell, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 128. Mat_Resource_Copper_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0110`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Copper_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Copper.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Resource Copper, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 129. Mat_Resource_Fiber_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0111`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Fiber_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Fiber.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Resource Fiber, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 130. Mat_Resource_Resin_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0113`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Resin_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Resin.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Resource Resin, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 131. Mat_Resource_Scrap_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0114`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Scrap_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Scrap.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Resource Scrap, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 132. Mat_Resource_Silica_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0115`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Silica_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Silica.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Resource Silica, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 133. Mat_Resource_Silver_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0116`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Silver_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Silver.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Resource Silver, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 134. Mat_Resource_Sulfur_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0117`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Sulfur_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Sulfur.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Resource Sulfur, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 135. Mat_Shelf_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0065`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Shelf_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Mat_Shelf.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Shelf, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 136. Mat_SmallPassiveProxy_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0055`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_SmallPassiveProxy_Albedo.png`
- Source paths: `Assets/_Project/Data/AI/GeneratedProxies/Materials/Mat_SmallPassiveProxy.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat SmallPassiveProxy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 137. Mat_Sun_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0066`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Sun_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Mat_Sun.mat`
- Slots: `_BaseMap`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Sun, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 138. Mat_Support_AbyssApex_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0163`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_AbyssApex_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_AbyssApex.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Support AbyssApex, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 139. Mat_Support_CreaturePassive_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0164`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_CreaturePassive_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_CreaturePassive.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Support CreaturePassive, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 140. Mat_Support_CreaturePredator_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0165`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_CreaturePredator_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_CreaturePredator.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Support CreaturePredator, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 141. Mat_Support_HazardPocket_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0166`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_HazardPocket_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_HazardPocket.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Support HazardPocket, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 142. Mat_Support_ReefApex_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0167`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_ReefApex_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_ReefApex.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Support ReefApex, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 143. Mat_Support_ResourcePocket_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0168`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_ResourcePocket_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_ResourcePocket.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Support ResourcePocket, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 144. Mat_Support_RuinApex_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0169`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_RuinApex_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_RuinApex.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Support RuinApex, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 145. Mat_Support_SafePocket_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0170`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_SafePocket_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_SafePocket.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Support SafePocket, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 146. Mat_TerritorialProxy_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0056`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_TerritorialProxy_Albedo.png`
- Source paths: `Assets/_Project/Data/AI/GeneratedProxies/Materials/Mat_TerritorialProxy.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat TerritorialProxy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 147. Mat_ToolTrial_Anchor_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0101`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Anchor_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Anchor.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat ToolTrial Anchor, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 148. Mat_ToolTrial_Cargo_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0102`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Cargo_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Cargo.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat ToolTrial Cargo, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 149. Mat_ToolTrial_Combat_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0103`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Combat_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Combat.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat ToolTrial Combat, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 150. Mat_ToolTrial_Dark_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0104`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Dark_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Dark.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat ToolTrial Dark, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 151. Mat_ToolTrial_Dormant_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0105`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Dormant_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Dormant.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat ToolTrial Dormant, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 152. Mat_ToolTrial_Heavy_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0106`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Heavy_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Heavy.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat ToolTrial Heavy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 153. Mat_ToolTrial_Scan_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0107`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Scan_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Scan.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat ToolTrial Scan, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 154. Mat_Tool_BeaconDeployer_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0118`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_BeaconDeployer_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Tools/Mat_Tool_BeaconDeployer_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Tool BeaconDeployer Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 155. Mat_Tool_Builder_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0119`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Builder_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Tools/Mat_Tool_Builder_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Tool Builder Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 156. Mat_Tool_EnvAnalyzer_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0120`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_EnvAnalyzer_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Tools/Mat_Tool_EnvAnalyzer_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Tool EnvAnalyzer Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 157. Mat_Tool_HarpoonLauncher_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0122`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_HarpoonLauncher_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Tools/Mat_Tool_HarpoonLauncher_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Tool HarpoonLauncher Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 158. Mat_Tool_Knife_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0123`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Knife_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Tools/Mat_Tool_Knife_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Tool Knife Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 159. Mat_Tool_LaserCutter_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0124`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_LaserCutter_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Tools/Mat_Tool_LaserCutter_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Tool LaserCutter Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 160. Mat_Tool_Propulsion_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0125`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Propulsion_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Tools/Mat_Tool_Propulsion_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Tool Propulsion Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 161. Mat_Tool_Repair_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0126`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Repair_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Tools/Mat_Tool_Repair_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Tool Repair Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 162. Mat_Tool_SalvageSampler_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0127`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_SalvageSampler_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Tools/Mat_Tool_SalvageSampler_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Tool SalvageSampler Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 163. Mat_Tool_Scanner_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0128`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Scanner_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Tools/Mat_Tool_Scanner_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Tool Scanner Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 164. Mat_Tool_StunPistol_Placeholder_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0129`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_StunPistol_Placeholder_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Tools/Mat_Tool_StunPistol_Placeholder.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Tool StunPistol Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 165. Meshy_AI_Alien_barnacles_clust_0301230506_texture_image_0_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0069`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `TEXTURE`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `4`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Meshy_AI_Alien_barnacles_clust_0301230506_texture_image_0_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Meshy_AI_Alien_barnacles_clust_0301230506_texture.fbx`
- Slots: `EmbeddedTexturePath_0;EmbeddedTexturePath_1;EmbeddedTexturePath_2;EmbeddedTexturePath_3`
- Reference states: `MISSING_EMBEDDED_TEXTURE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Meshy AI Alien barnacles clust 0301230506 texture, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 166. Sand_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0071`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Sand_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Sand.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Sand, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 167. Snow_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0073`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `3`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Snow_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Snow.mat`
- Slots: `_BaseMap;_MainTex;_ParallaxMap`
- Reference states: `EMPTY_REQUIRED_SLOT;MISSING_GUID`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Snow, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 168. Snow_Normal.png

- Queue ID: `SHINOBU_361_QUEUE_0074`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `NORMAL`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Snow_Normal.png`
- Source paths: `Assets/_Project/Art/Materials/Snow.mat`
- Slots: `_BumpMap`
- Reference states: `MISSING_GUID`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Snow, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan:
Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 169. red_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0070`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `MEDIUM`
- Resolution: `1024`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/red_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/red.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for red, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 170. Mat_GasGiant_Emissive.png

- Queue ID: `SHINOBU_361_QUEUE_0058`
- Action: `REBAKE_SOURCE_TO_PNG_AND_FIX_IMPORT`
- Texture role: `EMISSIVE`
- Priority: `LOW`
- Resolution: `512`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_GasGiant_Emissive.png`
- Source paths: `Assets/_Project/Art/Materials/Mat_GasGiant.mat`
- Slots: `_EmissionMap`
- Reference states: `IMPORT_ISSUE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat GasGiant, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 171. Mat_GasGiant_ORM.png

- Queue ID: `SHINOBU_361_QUEUE_0057`
- Action: `REBAKE_SOURCE_TO_PNG_AND_FIX_IMPORT`
- Texture role: `AO`
- Priority: `LOW`
- Resolution: `512`
- Source count: `1`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_GasGiant_ORM.png`
- Source paths: `Assets/_Project/Art/Materials/Mat_GasGiant.mat`
- Slots: `_CelestialOcclusionTex`
- Reference states: `IMPORT_ISSUE`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat GasGiant, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The surface must separate cavities, worn rough patches, and metallic substrate clearly enough to derive packed ORM masks.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 172. Mat_Skybox_Day_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0078`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `LOW`
- Resolution: `512`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Skybox_Day_Albedo.png`
- Source paths: `Assets/_Project/Art/Skyboxes/Mat_Skybox_Day.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Skybox Day, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 173. Mat_Skybox_Night_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0079`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `LOW`
- Resolution: `512`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Skybox_Night_Albedo.png`
- Source paths: `Assets/_Project/Art/Skyboxes/Mat_Skybox_Night.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Skybox Night, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 174. Mat_Skybox_Storm_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0080`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `ALBEDO`
- Priority: `LOW`
- Resolution: `512`
- Source count: `2`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Skybox_Storm_Albedo.png`
- Source paths: `Assets/_Project/Art/Skyboxes/Mat_Skybox_Storm.mat`
- Slots: `_BaseMap;_MainTex`
- Reference states: `EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Mat Skybox Storm, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### 175. Skybox_Albedo.png

- Queue ID: `SHINOBU_361_QUEUE_0072`
- Action: `GENERATE_REPLACEMENT_PBR`
- Texture role: `TEXTURE`
- Priority: `LOW`
- Resolution: `512`
- Source count: `7`
- Target path: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Skybox_Albedo.png`
- Source paths: `Assets/_Project/Art/Materials/Skybox.mat`
- Slots: `_BackTex;_DownTex;_FrontTex;_LeftTex;_MainTex;_RightTex;_UpTex`
- Reference states: `MISSING_GUID;EMPTY_REQUIRED_SLOT`
- Albedo compression: `BC7 sRGB, mipmaps on, Read/Write off`
- Normal compression: `BC5 linear normal, mipmaps on, Read/Write off`
- ORM compression: `BC7 linear packed ORM, mipmaps on, Read/Write off`

Prompt:
Material Subject: High-end modular ocean habitat material for Skybox, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan:
Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan:
Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.
