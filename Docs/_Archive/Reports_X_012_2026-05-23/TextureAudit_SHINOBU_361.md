# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# SHINOBU_361 Texture Audit and Bake Queue

Status: PENDING VERIFICATION
Evidence class: STATIC_SOURCE

## Summary
- target_files_scanned: 972
- audited_slots: 4529
- deficiency_slots: 413
- stub_texture_count: 0
- forbidden_format_texture_count: 0
- import_issue_texture_count: 7
- estimated_missing_texture_vram_mib: 783.529
- texture_budget_mib: 900.0
- texture_budget_status: PASS

## Unique Production Queue
- unique_target_textures: 175
- duplicate_slot_references_collapsed: 238
- queue_csv: `Docs/Reports/TextureProductionQueue_SHINOBU_361.csv`
- queue_json: `Docs/Reports/TextureProductionQueue_SHINOBU_361.json`
- queue_readable: `Docs/Reports/TextureProductionQueue_SHINOBU_361_READABLE.md`

### Unique Queue Priority Counts
- BLOCKER: 15
- LOW: 6
- MEDIUM: 154

### Unique Queue Category Counts
- FLORA_EPIDERMIS: 26
- GEOLOGY_TRIPLANAR: 23
- HABITAT_INTERIORS: 126

### Unique Queue Action Counts
- GENERATE_REPLACEMENT_PBR: 171
- REBAKE_SOURCE_TO_PNG_AND_FIX_IMPORT: 4

## Forensic Category Counts
- COCKPIT_SURFACES: 3
- DECAL_SHEETS: 12
- FLORA_EPIDERMIS: 1658
- GEOLOGY_TRIPLANAR: 556
- HABITAT_INTERIORS: 2300

## Forensic Priority Counts
- BLOCKER: 162
- LOW: 177
- MEDIUM: 4190

## Production Prompts

### SHINOBU_361_PROMPT_0033 HABITAT_INTERIORS

- Source: `Assets/_Project/_Archive/Mat_Ocean.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Ocean_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Ocean, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0040 HABITAT_INTERIORS

- Source: `Assets/_Project/_Archive/Mat_Ocean.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Ocean_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Ocean, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0049 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/ceiling_10.fbx`
- Slot: `EmbeddedTexturePath_0`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/ceiling_10_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for ceiling 10, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0050 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/ceiling_10.fbx`
- Slot: `EmbeddedTexturePath_1`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/ceiling_10_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for ceiling 10, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0051 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/ceiling_10.fbx`
- Slot: `EmbeddedTexturePath_2`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/ceiling_10_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for ceiling 10, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0052 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/ceiling_10.fbx`
- Slot: `EmbeddedTexturePath_3`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/ceiling_10_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for ceiling 10, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0053 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/ceiling_10.fbx`
- Slot: `EmbeddedTexturePath_4`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/ceiling_10_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for ceiling 10, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0054 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/ceiling_10.fbx`
- Slot: `EmbeddedTexturePath_5`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/ceiling_10_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for ceiling 10, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0055 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_05.fbx`
- Slot: `EmbeddedTexturePath_0`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_05_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for floor 05, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0056 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_05.fbx`
- Slot: `EmbeddedTexturePath_1`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_05_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for floor 05, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0057 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_05.fbx`
- Slot: `EmbeddedTexturePath_2`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_05_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for floor 05, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0058 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_05.fbx`
- Slot: `EmbeddedTexturePath_3`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_05_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for floor 05, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0059 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_05.fbx`
- Slot: `EmbeddedTexturePath_4`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_05_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for floor 05, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0060 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_05.fbx`
- Slot: `EmbeddedTexturePath_5`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_05_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for floor 05, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0061 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_05.fbx`
- Slot: `EmbeddedTexturePath_6`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_05_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for floor 05, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0062 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_05.fbx`
- Slot: `EmbeddedTexturePath_7`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_05_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for floor 05, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0063 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_05.fbx`
- Slot: `EmbeddedTexturePath_8`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_05_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for floor 05, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0064 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_05.fbx`
- Slot: `EmbeddedTexturePath_9`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_05_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for floor 05, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0065 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_05.fbx`
- Slot: `EmbeddedTexturePath_10`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_05_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for floor 05, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0066 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_05.fbx`
- Slot: `EmbeddedTexturePath_11`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_05_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for floor 05, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0067 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_large_8x8.fbx`
- Slot: `EmbeddedTexturePath_0`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_large_8x8_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for floor large 8x8, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0068 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_large_8x8.fbx`
- Slot: `EmbeddedTexturePath_1`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_large_8x8_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for floor large 8x8, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0069 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_large_8x8.fbx`
- Slot: `EmbeddedTexturePath_2`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_large_8x8_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for floor large 8x8, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0070 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_large_8x8.fbx`
- Slot: `EmbeddedTexturePath_3`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_large_8x8_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for floor large 8x8, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0071 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_large_8x8.fbx`
- Slot: `EmbeddedTexturePath_4`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_large_8x8_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for floor large 8x8, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0072 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/floor_large_8x8.fbx`
- Slot: `EmbeddedTexturePath_5`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_large_8x8_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for floor large 8x8, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0073 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_2x3_a.fbx`
- Slot: `EmbeddedTexturePath_0`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_2x3_a_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 2x3 a, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0074 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_2x3_a.fbx`
- Slot: `EmbeddedTexturePath_1`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_2x3_a_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 2x3 a, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0075 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_2x3_a.fbx`
- Slot: `EmbeddedTexturePath_2`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_2x3_a_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 2x3 a, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0076 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_2x3_a.fbx`
- Slot: `EmbeddedTexturePath_3`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_2x3_a_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 2x3 a, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0077 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_2x3_a.fbx`
- Slot: `EmbeddedTexturePath_4`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_2x3_a_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 2x3 a, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0078 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_2x3_a.fbx`
- Slot: `EmbeddedTexturePath_5`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_2x3_a_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 2x3 a, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0079 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_2x3_a.fbx`
- Slot: `EmbeddedTexturePath_6`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_2x3_a_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 2x3 a, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0080 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_2x3_a.fbx`
- Slot: `EmbeddedTexturePath_7`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_2x3_a_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 2x3 a, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0081 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_2x3_a.fbx`
- Slot: `EmbeddedTexturePath_8`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_2x3_a_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 2x3 a, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0082 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_2x3_a.fbx`
- Slot: `EmbeddedTexturePath_9`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_2x3_a_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 2x3 a, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0083 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_2x3_a.fbx`
- Slot: `EmbeddedTexturePath_10`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_2x3_a_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 2x3 a, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0084 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_2x3_a.fbx`
- Slot: `EmbeddedTexturePath_11`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_2x3_a_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 2x3 a, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0085 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slot: `EmbeddedTexturePath_0`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0086 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slot: `EmbeddedTexturePath_1`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0087 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slot: `EmbeddedTexturePath_2`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0088 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slot: `EmbeddedTexturePath_3`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0089 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slot: `EmbeddedTexturePath_4`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0090 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slot: `EmbeddedTexturePath_5`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0091 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slot: `EmbeddedTexturePath_6`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_labels_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0092 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slot: `EmbeddedTexturePath_7`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_labels_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0093 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slot: `EmbeddedTexturePath_8`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0094 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slot: `EmbeddedTexturePath_9`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0095 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slot: `EmbeddedTexturePath_10`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0096 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slot: `EmbeddedTexturePath_11`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0097 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slot: `EmbeddedTexturePath_12`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0098 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slot: `EmbeddedTexturePath_13`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0099 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slot: `EmbeddedTexturePath_14`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_labels_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0100 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slot: `EmbeddedTexturePath_15`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_labels_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0101 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_c.fbx`
- Slot: `EmbeddedTexturePath_16`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_labels_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 c, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0102 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slot: `EmbeddedTexturePath_0`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0103 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slot: `EmbeddedTexturePath_1`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0104 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slot: `EmbeddedTexturePath_2`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0105 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slot: `EmbeddedTexturePath_3`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0106 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slot: `EmbeddedTexturePath_4`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0107 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slot: `EmbeddedTexturePath_5`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0108 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slot: `EmbeddedTexturePath_6`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_labels_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0109 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slot: `EmbeddedTexturePath_7`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_labels_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0110 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slot: `EmbeddedTexturePath_8`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_labels_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0111 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slot: `EmbeddedTexturePath_9`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0112 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slot: `EmbeddedTexturePath_10`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0113 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slot: `EmbeddedTexturePath_11`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_stripes_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0114 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slot: `EmbeddedTexturePath_12`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0115 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slot: `EmbeddedTexturePath_13`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0116 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slot: `EmbeddedTexturePath_14`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0117 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slot: `EmbeddedTexturePath_15`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_labels_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0118 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slot: `EmbeddedTexturePath_16`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_labels_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0119 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_01_4x3_door_02_wing.fbx`
- Slot: `EmbeddedTexturePath_17`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_labels_basecolor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 01 4x3 door 02 wing, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0120 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_04_3x6_d.fbx`
- Slot: `EmbeddedTexturePath_0`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_04_3x6_d_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 04 3x6 d, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0121 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_04_3x6_d.fbx`
- Slot: `EmbeddedTexturePath_1`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_04_3x6_d_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 04 3x6 d, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0122 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_04_3x6_d.fbx`
- Slot: `EmbeddedTexturePath_2`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_04_3x6_d_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 04 3x6 d, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0123 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_04_3x6_d.fbx`
- Slot: `EmbeddedTexturePath_3`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_04_3x6_d_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 04 3x6 d, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0124 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_04_3x6_d.fbx`
- Slot: `EmbeddedTexturePath_4`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_04_3x6_d_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 04 3x6 d, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0125 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Models/wall_04_3x6_d.fbx`
- Slot: `EmbeddedTexturePath_5`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_04_3x6_d_trimsheet_normal_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for wall 04 3x6 d, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0145 HABITAT_INTERIORS

- Source: `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/Mat_HectonSurface.mat`
- Slot: `_BumpMap`
- State: `IMPORT_ISSUE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HectonSurface_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat HectonSurface, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0184 HABITAT_INTERIORS

- Source: `Assets/_Project/Resources/UI/MAT_DiegeticTooltipGlyph.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_DiegeticTooltipGlyph_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT DiegeticTooltipGlyph, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_0185 HABITAT_INTERIORS

- Source: `Assets/_Project/Resources/UI/MAT_DiegeticTooltipIcon.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_DiegeticTooltipIcon_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT DiegeticTooltipIcon, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2036 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_pocket_hazard_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_hazard_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family pocket hazard Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2042 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_pocket_hazard_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_hazard_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family pocket hazard Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2050 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_pocket_safe_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_safe_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family pocket safe Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2056 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_pocket_safe_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_safe_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family pocket safe Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2064 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_route_power_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_route_power_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family route power Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2070 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_route_power_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_route_power_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family route power Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2078 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_ruin_cluster_medium_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_cluster_medium_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family ruin cluster medium Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2084 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_ruin_cluster_medium_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_cluster_medium_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family ruin cluster medium Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2092 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_ruin_megastructure_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_megastructure_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family ruin megastructure Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2098 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_ruin_megastructure_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_megastructure_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family ruin megastructure Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2106 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_ruin_module_single_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_module_single_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family ruin module single Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2112 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_ruin_module_single_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_module_single_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family ruin module single Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2120 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_service_scar_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_service_scar_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family service scar Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2126 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Construction/MAT_family_service_scar_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_service_scar_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family service scar Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2134 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Debris/MAT_family_debris_field_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_debris_field_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family debris field Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2140 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Debris/MAT_family_debris_field_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_debris_field_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family debris field Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2148 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Debris/MAT_family_debris_scatter_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_debris_scatter_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family debris scatter Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2154 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Debris/MAT_family_debris_scatter_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_debris_scatter_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family debris scatter Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2162 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Fauna/MAT_family_creature_spawn_passive_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_spawn_passive_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature spawn passive Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2168 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Fauna/MAT_family_creature_spawn_passive_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_spawn_passive_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature spawn passive Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2176 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Fauna/MAT_family_creature_spawn_predator_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_spawn_predator_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature spawn predator Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2182 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Fauna/MAT_family_creature_spawn_predator_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_spawn_predator_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature spawn predator Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2190 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Fauna/MAT_family_egg_cluster_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_egg_cluster_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family egg cluster Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2196 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Fauna/MAT_family_egg_cluster_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_egg_cluster_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family egg cluster Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2204 FLORA_EPIDERMIS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_coral_branching_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_branching_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family coral branching Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2210 FLORA_EPIDERMIS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_coral_branching_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_branching_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family coral branching Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2218 FLORA_EPIDERMIS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_coral_low_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_low_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family coral low Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2224 FLORA_EPIDERMIS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_coral_low_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_low_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family coral low Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2232 FLORA_EPIDERMIS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_coral_massive_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_massive_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family coral massive Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2238 FLORA_EPIDERMIS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_coral_massive_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_massive_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family coral massive Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2246 FLORA_EPIDERMIS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_coral_plate_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_plate_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family coral plate Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2252 FLORA_EPIDERMIS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_coral_plate_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_plate_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family coral plate Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2260 FLORA_EPIDERMIS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_kelp_canopy_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_canopy_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family kelp canopy Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2266 FLORA_EPIDERMIS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_kelp_canopy_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_canopy_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family kelp canopy Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2274 FLORA_EPIDERMIS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_kelp_patch_dense_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_patch_dense_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family kelp patch dense Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2280 FLORA_EPIDERMIS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_kelp_patch_dense_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_patch_dense_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family kelp patch dense Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2288 FLORA_EPIDERMIS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_kelp_tall_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_tall_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family kelp tall Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2294 FLORA_EPIDERMIS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_kelp_tall_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_tall_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family kelp tall Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2302 FLORA_EPIDERMIS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_plant_giant_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_plant_giant_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family plant giant Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2308 FLORA_EPIDERMIS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Flora/MAT_family_plant_giant_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_plant_giant_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family plant giant Placeholder, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2316 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/LargeThreats/MAT_family_creature_zone_abyss_apex_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_abyss_apex_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature zone abyss apex Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2322 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/LargeThreats/MAT_family_creature_zone_abyss_apex_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_abyss_apex_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature zone abyss apex Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2330 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/LargeThreats/MAT_family_creature_zone_large_threat_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_large_threat_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature zone large threat Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2336 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/LargeThreats/MAT_family_creature_zone_large_threat_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_large_threat_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature zone large threat Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2344 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/LargeThreats/MAT_family_creature_zone_reef_apex_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_reef_apex_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature zone reef apex Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2350 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/LargeThreats/MAT_family_creature_zone_reef_apex_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_reef_apex_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature zone reef apex Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2358 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/LargeThreats/MAT_family_creature_zone_ruin_apex_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_ruin_apex_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature zone ruin apex Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2364 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/LargeThreats/MAT_family_creature_zone_ruin_apex_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_ruin_apex_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature zone ruin apex Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2372 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Resources/MAT_family_pocket_resource_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_resource_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family pocket resource Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2378 HABITAT_INTERIORS

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/Resources/MAT_family_pocket_resource_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_resource_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family pocket resource Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2386 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/TerrainLod/MAT_family_cave_entrance_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_cave_entrance_Placeholder_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for MAT family cave entrance Placeholder, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2392 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/TerrainLod/MAT_family_cave_entrance_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_cave_entrance_Placeholder_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for MAT family cave entrance Placeholder, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2400 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/TerrainLod/MAT_family_landmark_spire_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_landmark_spire_Placeholder_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for MAT family landmark spire Placeholder, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2406 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/TerrainLod/MAT_family_landmark_spire_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_landmark_spire_Placeholder_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for MAT family landmark spire Placeholder, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2428 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/TerrainLod/MAT_family_rock_cluster_medium_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_rock_cluster_medium_Placeholder_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for MAT family rock cluster medium Placeholder, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2434 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/TerrainLod/MAT_family_rock_cluster_medium_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_rock_cluster_medium_Placeholder_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for MAT family rock cluster medium Placeholder, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2442 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/TerrainLod/MAT_family_rock_small_floor_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_rock_small_floor_Placeholder_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for MAT family rock small floor Placeholder, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2448 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders/TerrainLod/MAT_family_rock_small_floor_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_rock_small_floor_Placeholder_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for MAT family rock small floor Placeholder, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2456 FLORA_EPIDERMIS

- Source: `Assets/_Project/Data/Flora/GeneratedProxies/Materials/MAT_FloraProxy_Coral.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_FloraProxy_Coral_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT FloraProxy Coral, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2462 FLORA_EPIDERMIS

- Source: `Assets/_Project/Data/Flora/GeneratedProxies/Materials/MAT_FloraProxy_Coral.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_FloraProxy_Coral_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT FloraProxy Coral, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2470 FLORA_EPIDERMIS

- Source: `Assets/_Project/Data/Flora/GeneratedProxies/Materials/MAT_FloraProxy_Kelp.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_FloraProxy_Kelp_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT FloraProxy Kelp, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2476 FLORA_EPIDERMIS

- Source: `Assets/_Project/Data/Flora/GeneratedProxies/Materials/MAT_FloraProxy_Kelp.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_FloraProxy_Kelp_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT FloraProxy Kelp, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2484 FLORA_EPIDERMIS

- Source: `Assets/_Project/Data/Flora/GeneratedProxies/Materials/MAT_FloraProxy_MicroGrass.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_FloraProxy_MicroGrass_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT FloraProxy MicroGrass, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2490 FLORA_EPIDERMIS

- Source: `Assets/_Project/Data/Flora/GeneratedProxies/Materials/MAT_FloraProxy_MicroGrass.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_FloraProxy_MicroGrass_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT FloraProxy MicroGrass, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2498 FLORA_EPIDERMIS

- Source: `Assets/_Project/Data/Flora/GeneratedProxies/Materials/MAT_FloraProxy_Sargassum.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_FloraProxy_Sargassum_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT FloraProxy Sargassum, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2504 FLORA_EPIDERMIS

- Source: `Assets/_Project/Data/Flora/GeneratedProxies/Materials/MAT_FloraProxy_Sargassum.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_FloraProxy_Sargassum_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT FloraProxy Sargassum, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_2644 HABITAT_INTERIORS

- Source: `Assets/_Project/Data/AI/GeneratedProxies/Materials/Mat_DroneProxy.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_DroneProxy_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat DroneProxy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2650 HABITAT_INTERIORS

- Source: `Assets/_Project/Data/AI/GeneratedProxies/Materials/Mat_DroneProxy.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_DroneProxy_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat DroneProxy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2658 HABITAT_INTERIORS

- Source: `Assets/_Project/Data/AI/GeneratedProxies/Materials/Mat_HeavyHunterProxy.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HeavyHunterProxy_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat HeavyHunterProxy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2664 HABITAT_INTERIORS

- Source: `Assets/_Project/Data/AI/GeneratedProxies/Materials/Mat_HeavyHunterProxy.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HeavyHunterProxy_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat HeavyHunterProxy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2672 HABITAT_INTERIORS

- Source: `Assets/_Project/Data/AI/GeneratedProxies/Materials/Mat_HunterProxy.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HunterProxy_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat HunterProxy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2678 HABITAT_INTERIORS

- Source: `Assets/_Project/Data/AI/GeneratedProxies/Materials/Mat_HunterProxy.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HunterProxy_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat HunterProxy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2686 HABITAT_INTERIORS

- Source: `Assets/_Project/Data/AI/GeneratedProxies/Materials/Mat_LeviathanProxy.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_LeviathanProxy_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat LeviathanProxy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2692 HABITAT_INTERIORS

- Source: `Assets/_Project/Data/AI/GeneratedProxies/Materials/Mat_LeviathanProxy.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_LeviathanProxy_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat LeviathanProxy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2700 HABITAT_INTERIORS

- Source: `Assets/_Project/Data/AI/GeneratedProxies/Materials/Mat_SmallPassiveProxy.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_SmallPassiveProxy_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat SmallPassiveProxy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2706 HABITAT_INTERIORS

- Source: `Assets/_Project/Data/AI/GeneratedProxies/Materials/Mat_SmallPassiveProxy.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_SmallPassiveProxy_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat SmallPassiveProxy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2714 HABITAT_INTERIORS

- Source: `Assets/_Project/Data/AI/GeneratedProxies/Materials/Mat_TerritorialProxy.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_TerritorialProxy_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat TerritorialProxy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2720 HABITAT_INTERIORS

- Source: `Assets/_Project/Data/AI/GeneratedProxies/Materials/Mat_TerritorialProxy.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_TerritorialProxy_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat TerritorialProxy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2736 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Mat_GasGiant.mat`
- Slot: `_CelestialOcclusionTex`
- State: `IMPORT_ISSUE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_GasGiant_ORM.png`
- Resolution: 512

Material Subject: High-end modular ocean habitat material for Mat GasGiant, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The surface must separate cavities, worn rough patches, and metallic substrate clearly enough to derive packed ORM masks.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2738 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Mat_GasGiant.mat`
- Slot: `_EmissionMap`
- State: `IMPORT_ISSUE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_GasGiant_Emissive.png`
- Resolution: 512

Material Subject: High-end modular ocean habitat material for Mat GasGiant, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2745 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HectonSky_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat HectonSky, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2751 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
- Slot: `_HighCloudTex`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HectonSky_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat HectonSky, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2753 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
- Slot: `_MainCloudAtlas`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HectonSky_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat HectonSky, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2755 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HectonSky_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat HectonSky, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2765 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Mat_HectonSky_CloudOverlay.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HectonSky_CloudOverlay_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat HectonSky CloudOverlay, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2771 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Mat_HectonSky_CloudOverlay.mat`
- Slot: `_HighCloudTex`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HectonSky_CloudOverlay_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat HectonSky CloudOverlay, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2773 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Mat_HectonSky_CloudOverlay.mat`
- Slot: `_MainCloudAtlas`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HectonSky_CloudOverlay_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat HectonSky CloudOverlay, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2775 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Mat_HectonSky_CloudOverlay.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HectonSky_CloudOverlay_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat HectonSky CloudOverlay, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2784 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/MAT_SargassumFoamDamping.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_SargassumFoamDamping_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT SargassumFoamDamping, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2785 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/MAT_SargassumMicroFaunaBoids.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_SargassumMicroFaunaBoids_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT SargassumMicroFaunaBoids, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2786 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/MAT_SargassumOilFilm.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_SargassumOilFilm_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT SargassumOilFilm, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2787 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/MAT_SargassumWaveDamping.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_SargassumWaveDamping_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT SargassumWaveDamping, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2788 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Mat_Shelf.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Shelf_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Shelf, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2794 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Mat_Shelf.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Shelf_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Shelf, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2804 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Mat_Sun.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Sun_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Sun, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2809 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Materials/Mat_TriplanarRock.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Mat_TriplanarRock_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for Mat TriplanarRock, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2815 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Materials/Mat_TriplanarRock.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Mat_TriplanarRock_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for Mat TriplanarRock, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2819 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Materials/Mat_TriplanarRock.mat`
- Slot: `_Rock_Albedo`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Mat_TriplanarRock_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for Mat TriplanarRock, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2824 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Mat_Visor_Glass.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Visor_Glass_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Visor Glass, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2832 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Mat_Visor_Glass.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Visor_Glass_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Visor Glass, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2843 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Meshy_AI_Alien_barnacles_clust_0301230506_texture.fbx`
- Slot: `EmbeddedTexturePath_0`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Meshy_AI_Alien_barnacles_clust_0301230506_texture_image_0_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Meshy AI Alien barnacles clust 0301230506 texture, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2844 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Meshy_AI_Alien_barnacles_clust_0301230506_texture.fbx`
- Slot: `EmbeddedTexturePath_1`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Meshy_AI_Alien_barnacles_clust_0301230506_texture_image_0_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Meshy AI Alien barnacles clust 0301230506 texture, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2845 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Meshy_AI_Alien_barnacles_clust_0301230506_texture.fbx`
- Slot: `EmbeddedTexturePath_2`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Meshy_AI_Alien_barnacles_clust_0301230506_texture_image_0_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Meshy AI Alien barnacles clust 0301230506 texture, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2846 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Meshy_AI_Alien_barnacles_clust_0301230506_texture.fbx`
- Slot: `EmbeddedTexturePath_3`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Meshy_AI_Alien_barnacles_clust_0301230506_texture_image_0_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Meshy AI Alien barnacles clust 0301230506 texture, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2861 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/red.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/red_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for red, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2867 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/red.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/red_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for red, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2875 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Sand.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Sand_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Sand, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2881 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Sand.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Sand_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Sand, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2889 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Skybox.mat`
- Slot: `_BackTex`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Skybox_Albedo.png`
- Resolution: 512

Material Subject: High-end modular ocean habitat material for Skybox, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2894 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Skybox.mat`
- Slot: `_DownTex`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Skybox_Albedo.png`
- Resolution: 512

Material Subject: High-end modular ocean habitat material for Skybox, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2896 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Skybox.mat`
- Slot: `_FrontTex`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Skybox_Albedo.png`
- Resolution: 512

Material Subject: High-end modular ocean habitat material for Skybox, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2897 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Skybox.mat`
- Slot: `_LeftTex`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Skybox_Albedo.png`
- Resolution: 512

Material Subject: High-end modular ocean habitat material for Skybox, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2898 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Skybox.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Skybox_Albedo.png`
- Resolution: 512

Material Subject: High-end modular ocean habitat material for Skybox, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2902 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Skybox.mat`
- Slot: `_RightTex`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Skybox_Albedo.png`
- Resolution: 512

Material Subject: High-end modular ocean habitat material for Skybox, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2903 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Skybox.mat`
- Slot: `_UpTex`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Skybox_Albedo.png`
- Resolution: 512

Material Subject: High-end modular ocean habitat material for Skybox, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2904 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Snow.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Snow_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Snow, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2905 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Snow.mat`
- Slot: `_BumpMap`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Snow_Normal.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Snow, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2910 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Snow.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Snow_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Snow, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2913 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Snow.mat`
- Slot: `_ParallaxMap`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Snow_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Snow, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2918 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Materials/terrain 1.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/terrain_1_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for terrain 1, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2924 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Materials/terrain 1.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/terrain_1_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for terrain 1, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2932 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Materials/terrain 2.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/terrain_2_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for terrain 2, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2938 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Materials/terrain 2.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/terrain_2_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for terrain 2, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2946 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Materials/terrain.mat`
- Slot: `_BaseMap`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/terrain_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for terrain, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2952 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Materials/terrain.mat`
- Slot: `_MainTex`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/terrain_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for terrain, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_2956 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Materials/terrain.mat`
- Slot: `_Rock_Albedo`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/terrain_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for terrain, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3087 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Skyboxes/Mat_Skybox_Day.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Skybox_Day_Albedo.png`
- Resolution: 512

Material Subject: High-end modular ocean habitat material for Mat Skybox Day, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3093 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Skyboxes/Mat_Skybox_Day.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Skybox_Day_Albedo.png`
- Resolution: 512

Material Subject: High-end modular ocean habitat material for Mat Skybox Day, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3102 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Skyboxes/Mat_Skybox_Night.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Skybox_Night_Albedo.png`
- Resolution: 512

Material Subject: High-end modular ocean habitat material for Mat Skybox Night, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3108 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Skyboxes/Mat_Skybox_Night.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Skybox_Night_Albedo.png`
- Resolution: 512

Material Subject: High-end modular ocean habitat material for Mat Skybox Night, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3117 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Skyboxes/Mat_Skybox_Storm.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Skybox_Storm_Albedo.png`
- Resolution: 512

Material Subject: High-end modular ocean habitat material for Mat Skybox Storm, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3123 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Skyboxes/Mat_Skybox_Storm.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Skybox_Storm_Albedo.png`
- Resolution: 512

Material Subject: High-end modular ocean habitat material for Mat Skybox Storm, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3145 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx`
- Slot: `EmbeddedTexturePath_0`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/SAMMPLE_image_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for SAMMPLE, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3146 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx`
- Slot: `EmbeddedTexturePath_1`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/SAMMPLE_image_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for SAMMPLE, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3147 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx`
- Slot: `EmbeddedTexturePath_2`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/SAMMPLE_1_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for SAMMPLE, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3148 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx`
- Slot: `EmbeddedTexturePath_3`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/SAMMPLE_1_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for SAMMPLE, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3149 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx`
- Slot: `EmbeddedTexturePath_4`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/SAMMPLE_image_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for SAMMPLE, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3150 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx`
- Slot: `EmbeddedTexturePath_5`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/SAMMPLE_image_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for SAMMPLE, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3151 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx`
- Slot: `EmbeddedTexturePath_6`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/SAMMPLE_image_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for SAMMPLE, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3152 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx`
- Slot: `EmbeddedTexturePath_7`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/SAMMPLE_image_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for SAMMPLE, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3153 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx`
- Slot: `EmbeddedTexturePath_8`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/SAMMPLE_image_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for SAMMPLE, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3154 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx`
- Slot: `EmbeddedTexturePath_9`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/SAMMPLE_1_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for SAMMPLE, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3155 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx`
- Slot: `EmbeddedTexturePath_10`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/SAMMPLE_1_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for SAMMPLE, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3156 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx`
- Slot: `EmbeddedTexturePath_11`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/SAMMPLE_1_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for SAMMPLE, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3157 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx`
- Slot: `EmbeddedTexturePath_12`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/SAMMPLE_1_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for SAMMPLE, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3158 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 7/SAMMPLE.fbx`
- Slot: `EmbeddedTexturePath_13`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/SAMMPLE_1_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for SAMMPLE, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3160 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock2.mat`
- Slot: `_BaseMap`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock2_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for mat Rock2, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3162 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock2.mat`
- Slot: `_BumpMap`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock2_Normal.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for mat Rock2, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3167 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock2.mat`
- Slot: `_MainTex`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock2_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for mat Rock2, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3171 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock2.mat`
- Slot: `_OcclusionMap`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock2_ORM.png`
- Resolution: 1024

Material Subject: Striking alien seafloor geology texture for mat Rock2, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The surface must separate cavities, worn rough patches, and metallic substrate clearly enough to derive packed ORM masks.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3177 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock_Shared.mat`
- Slot: `_BaseMap`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock_Shared_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for mat Rock Shared, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3179 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock_Shared.mat`
- Slot: `_BumpMap`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock_Shared_Normal.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for mat Rock Shared, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3184 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock_Shared.mat`
- Slot: `_MainTex`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock_Shared_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for mat Rock Shared, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3188 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/mat_Rock_Shared.mat`
- Slot: `_OcclusionMap`
- State: `MISSING_GUID`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock_Shared_ORM.png`
- Resolution: 1024

Material Subject: Striking alien seafloor geology texture for mat Rock Shared, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The surface must separate cavities, worn rough patches, and metallic substrate clearly enough to derive packed ORM masks.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3194 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slot: `EmbeddedTexturePath_0`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_basecolor_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3195 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slot: `EmbeddedTexturePath_1`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_basecolor_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3196 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slot: `EmbeddedTexturePath_2`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_basecolor_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3197 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slot: `EmbeddedTexturePath_3`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_normal_Normal.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3198 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slot: `EmbeddedTexturePath_4`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_normal_Normal.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3199 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slot: `EmbeddedTexturePath_5`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_normal_Normal.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3200 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slot: `EmbeddedTexturePath_6`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_roughness_ORM.png`
- Resolution: 1024

Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The surface must separate cavities, worn rough patches, and metallic substrate clearly enough to derive packed ORM masks.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3201 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slot: `EmbeddedTexturePath_7`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_roughness_ORM.png`
- Resolution: 1024

Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The surface must separate cavities, worn rough patches, and metallic substrate clearly enough to derive packed ORM masks.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3202 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slot: `EmbeddedTexturePath_8`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_roughness_ORM.png`
- Resolution: 1024

Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The surface must separate cavities, worn rough patches, and metallic substrate clearly enough to derive packed ORM masks.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3203 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slot: `EmbeddedTexturePath_9`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_basecolor_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3204 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slot: `EmbeddedTexturePath_10`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_basecolor_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3205 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slot: `EmbeddedTexturePath_11`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_basecolor_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3206 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slot: `EmbeddedTexturePath_12`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_normal_Normal.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3207 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slot: `EmbeddedTexturePath_13`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_normal_Normal.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3208 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slot: `EmbeddedTexturePath_14`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_normal_Normal.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The albedo must expose clear luminance depth cues that can drive a stable tangent space normal extraction.

Normal plan: Generate or replace the BC5 normal directly. Use the albedo prompt as the height language, then run Tools/MaterialAudit.py as the compatibility audit after import; luminance-derived normals are acceptable for scratches, salt crystals, pores, silt, and shallow seams, but panel rivets need a dedicated normal bake to prevent inverted depth.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3209 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slot: `EmbeddedTexturePath_15`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_roughness_ORM.png`
- Resolution: 1024

Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The surface must separate cavities, worn rough patches, and metallic substrate clearly enough to derive packed ORM masks.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3210 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slot: `EmbeddedTexturePath_16`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_roughness_ORM.png`
- Resolution: 1024

Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The surface must separate cavities, worn rough patches, and metallic substrate clearly enough to derive packed ORM masks.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3211 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock_4_-_UNIVERSALNYY_VYBOR/UNIVERSALNYY_VYBOR_(TEKSTURY)/Rock_4.fbx`
- Slot: `EmbeddedTexturePath_17`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_roughness_ORM.png`
- Resolution: 1024

Material Subject: Striking alien seafloor geology texture for Rock 4, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The surface must separate cavities, worn rough patches, and metallic substrate clearly enough to derive packed ORM masks.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3240 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 5/orig/River_Rock_FBX.fbx`
- Slot: `EmbeddedTexturePath_0`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/River_Rock_FBX_riverrock_basecolor_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for River Rock FBX, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3241 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 5/orig/River_Rock_FBX.fbx`
- Slot: `EmbeddedTexturePath_1`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/River_Rock_FBX_riverrock_basecolor_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for River Rock FBX, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3242 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 5/orig/River_Rock_FBX.fbx`
- Slot: `EmbeddedTexturePath_2`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/River_Rock_FBX_riverrock_basecolor_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for River Rock FBX, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3243 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 5/orig/River_Rock_FBX.fbx`
- Slot: `EmbeddedTexturePath_3`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/River_Rock_FBX_riverrock_basecolor_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for River Rock FBX, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3244 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 5/orig/River_Rock_FBX.fbx`
- Slot: `EmbeddedTexturePath_4`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/River_Rock_FBX_riverrock_basecolor_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for River Rock FBX, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3245 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Models/Rocks/Rock 5/orig/River_Rock_FBX.fbx`
- Slot: `EmbeddedTexturePath_5`
- State: `MISSING_EMBEDDED_TEXTURE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/River_Rock_FBX_riverrock_basecolor_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for River Rock FBX, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3330 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_BuildGhost_Invalid.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_BuildGhost_Invalid_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat BuildGhost Invalid, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3336 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_BuildGhost_Invalid.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_BuildGhost_Invalid_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat BuildGhost Invalid, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3344 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_BuildGhost_Valid.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_BuildGhost_Valid_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat BuildGhost Valid, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3350 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_BuildGhost_Valid.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_BuildGhost_Valid_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat BuildGhost Valid, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3363 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_LeakWetSheen.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_LeakWetSheen_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat LeakWetSheen, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3371 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_Module_Corridor.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_Corridor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Module Corridor, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3377 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_Module_Corridor.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_Corridor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Module Corridor, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3385 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_Module_CurrentTurbine.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_CurrentTurbine_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Module CurrentTurbine, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3391 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_Module_CurrentTurbine.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_CurrentTurbine_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Module CurrentTurbine, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3399 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_Module_Foundation.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_Foundation_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Module Foundation, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3405 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_Module_Foundation.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_Foundation_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Module Foundation, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3413 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_Module_Pylon.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_Pylon_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Module Pylon, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3419 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_Module_Pylon.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_Pylon_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Module Pylon, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3427 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_Module_ServicePump.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_ServicePump_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Module ServicePump, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3433 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_Module_ServicePump.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_ServicePump_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Module ServicePump, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3442 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Anchor.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Anchor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat ToolTrial Anchor, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3448 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Anchor.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Anchor_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat ToolTrial Anchor, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3456 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Cargo.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Cargo_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat ToolTrial Cargo, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3462 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Cargo.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Cargo_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat ToolTrial Cargo, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3470 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Combat.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Combat_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat ToolTrial Combat, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3476 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Combat.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Combat_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat ToolTrial Combat, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3484 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Dark.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Dark_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat ToolTrial Dark, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3490 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Dark.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Dark_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat ToolTrial Dark, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3498 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Dormant.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Dormant_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat ToolTrial Dormant, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3504 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Dormant.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Dormant_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat ToolTrial Dormant, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3512 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Heavy.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Heavy_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat ToolTrial Heavy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3518 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Heavy.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Heavy_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat ToolTrial Heavy, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3526 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Scan.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Scan_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat ToolTrial Scan, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3532 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Construction/Mat_ToolTrial_Scan.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Scan_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat ToolTrial Scan, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3540 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Diagnostics/MAT_ErrorCube.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_ErrorCube_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT ErrorCube, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3546 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Diagnostics/MAT_ErrorCube.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_ErrorCube_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT ErrorCube, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3554 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Gameplay/MAT_PlayerSwimBlockout.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_PlayerSwimBlockout_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT PlayerSwimBlockout, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3560 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Gameplay/MAT_PlayerSwimBlockout.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_PlayerSwimBlockout_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT PlayerSwimBlockout, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3568 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Copper.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Copper_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Resource Copper, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3574 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Copper.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Copper_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Resource Copper, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3582 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Fiber.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Fiber_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Resource Fiber, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3588 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Fiber.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Fiber_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Resource Fiber, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3596 FLORA_EPIDERMIS

- Source: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Membrane.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/Mat_Resource_Membrane_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for Mat Resource Membrane, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_3602 FLORA_EPIDERMIS

- Source: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Membrane.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/Mat_Resource_Membrane_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for Mat Resource Membrane, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_3610 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Resin.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Resin_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Resource Resin, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3616 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Resin.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Resin_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Resource Resin, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3624 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Scrap.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Scrap_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Resource Scrap, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3630 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Scrap.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Scrap_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Resource Scrap, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3638 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Silica.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Silica_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Resource Silica, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3644 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Silica.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Silica_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Resource Silica, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3652 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Silver.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Silver_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Resource Silver, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3658 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Silver.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Silver_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Resource Silver, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3666 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Sulfur.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Sulfur_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Resource Sulfur, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3672 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Sulfur.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Sulfur_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Resource Sulfur, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3680 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_BeaconDeployer_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_BeaconDeployer_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool BeaconDeployer Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3686 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_BeaconDeployer_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_BeaconDeployer_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool BeaconDeployer Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3694 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_Builder_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Builder_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool Builder Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3700 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_Builder_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Builder_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool Builder Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3708 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_EnvAnalyzer_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_EnvAnalyzer_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool EnvAnalyzer Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3714 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_EnvAnalyzer_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_EnvAnalyzer_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool EnvAnalyzer Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3722 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_Flashlight_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Mat_Tool_Flashlight_Placeholder_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for Mat Tool Flashlight Placeholder, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3728 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_Flashlight_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Mat_Tool_Flashlight_Placeholder_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for Mat Tool Flashlight Placeholder, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3736 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_HarpoonLauncher_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_HarpoonLauncher_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool HarpoonLauncher Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3742 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_HarpoonLauncher_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_HarpoonLauncher_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool HarpoonLauncher Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3750 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_Knife_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Knife_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool Knife Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3756 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_Knife_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Knife_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool Knife Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3764 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_LaserCutter_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_LaserCutter_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool LaserCutter Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3770 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_LaserCutter_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_LaserCutter_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool LaserCutter Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3778 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_Propulsion_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Propulsion_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool Propulsion Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3784 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_Propulsion_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Propulsion_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool Propulsion Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3792 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_Repair_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Repair_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool Repair Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3798 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_Repair_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Repair_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool Repair Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3806 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_SalvageSampler_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_SalvageSampler_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool SalvageSampler Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3812 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_SalvageSampler_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_SalvageSampler_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool SalvageSampler Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3820 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_Scanner_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Scanner_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool Scanner Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3826 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_Scanner_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Scanner_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool Scanner Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3834 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_StunPistol_Placeholder.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_StunPistol_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool StunPistol Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3840 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Tools/Mat_Tool_StunPistol_Placeholder.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_StunPistol_Placeholder_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Tool StunPistol Placeholder, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3848 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/VFX/MAT_LightningBolt.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_LightningBolt_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT LightningBolt, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3852 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_cave_entrance.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_cave_entrance_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for MAT family cave entrance, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3858 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_cave_entrance.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_cave_entrance_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for MAT family cave entrance, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3873 FLORA_EPIDERMIS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_branching.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_branching_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family coral branching, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_3890 FLORA_EPIDERMIS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_brittle.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_brittle_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family coral brittle, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_3907 FLORA_EPIDERMIS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_low.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_low_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family coral low, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_3924 FLORA_EPIDERMIS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_massive.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_massive_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family coral massive, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_3941 FLORA_EPIDERMIS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_coral_plate.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_plate_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family coral plate, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_3951 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_spawn_passive.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_spawn_passive_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature spawn passive, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3957 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_spawn_passive.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_spawn_passive_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature spawn passive, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3965 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_spawn_predator.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_spawn_predator_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature spawn predator, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3971 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_spawn_predator.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_spawn_predator_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature spawn predator, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3979 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_zone_abyss_apex.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_abyss_apex_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature zone abyss apex, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3985 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_zone_abyss_apex.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_abyss_apex_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature zone abyss apex, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3993 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_zone_large_threat.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_large_threat_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature zone large threat, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_3999 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_zone_large_threat.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_large_threat_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature zone large threat, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4007 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_zone_reef_apex.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_reef_apex_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature zone reef apex, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4013 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_zone_reef_apex.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_reef_apex_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature zone reef apex, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4021 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_zone_ruin_apex.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_ruin_apex_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature zone ruin apex, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4027 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_creature_zone_ruin_apex.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_ruin_apex_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family creature zone ruin apex, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4035 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_debris_field.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_debris_field_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family debris field, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4041 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_debris_field.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_debris_field_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family debris field, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4049 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_debris_scatter.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_debris_scatter_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family debris scatter, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4055 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_debris_scatter.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_debris_scatter_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family debris scatter, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4063 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_egg_cluster.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_egg_cluster_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family egg cluster, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4069 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_egg_cluster.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_egg_cluster_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family egg cluster, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4084 FLORA_EPIDERMIS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_abyssal.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_abyssal_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family kelp abyssal, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_4101 FLORA_EPIDERMIS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_canopy.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_canopy_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family kelp canopy, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_4118 FLORA_EPIDERMIS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_patch_dense.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_patch_dense_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family kelp patch dense, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_4135 FLORA_EPIDERMIS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_kelp_tall.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_tall_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family kelp tall, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_4145 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_landmark_spire.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_landmark_spire_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family landmark spire, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4151 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_landmark_spire.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_landmark_spire_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family landmark spire, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4159 FLORA_EPIDERMIS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_plant_giant.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_plant_giant_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family plant giant, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_4165 FLORA_EPIDERMIS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_plant_giant.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_plant_giant_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for MAT family plant giant, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_4173 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_pocket_hazard.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_hazard_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family pocket hazard, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4179 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_pocket_hazard.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_hazard_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family pocket hazard, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4187 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_pocket_resource.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_resource_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family pocket resource, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4193 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_pocket_resource.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_resource_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family pocket resource, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4201 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_pocket_safe.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_safe_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family pocket safe, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4207 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_pocket_safe.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_safe_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family pocket safe, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4243 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_rock_shelf_large.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_rock_shelf_large_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for MAT family rock shelf large, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4249 GEOLOGY_TRIPLANAR

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_rock_shelf_large.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_rock_shelf_large_Albedo.png`
- Resolution: 2048

Material Subject: Striking alien seafloor geology texture for MAT family rock shelf large, blue black basalt, graphite silt, pale mineral crust, opal hydrothermal veins, oxidized copper traces, and elegant layered stone suited for seamless triplanar cave and cliff projection. Surface Wear and Details: Crisp porous basalt pockets, pearly sediment dusting, bright but natural mineral seams, eroded shelf edges, delicate crystal flecks, readable macro layering, and dense organic grain with no painted directionality or muddy black fill. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked cavity AO from pores and cracks, Green equals roughness from 0.78 on dry basalt to 0.92 in silt, Blue equals metallic 0.0 except rare mineral flecks at 0.05. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4271 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_route_power.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_route_power_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family route power, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4277 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_route_power.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_route_power_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family route power, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4285 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_ruin_cluster_medium.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_cluster_medium_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family ruin cluster medium, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4291 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_ruin_cluster_medium.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_cluster_medium_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family ruin cluster medium, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4299 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_ruin_megastructure.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_megastructure_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family ruin megastructure, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4305 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_ruin_megastructure.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_megastructure_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family ruin megastructure, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4313 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_ruin_module_single.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_module_single_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family ruin module single, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4319 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_ruin_module_single.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_module_single_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family ruin module single, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4327 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_service_scar.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_service_scar_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family service scar, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4333 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_family_service_scar.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_service_scar_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT family service scar, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4344 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_ProceduralBio_Shallows.mat`
- Slot: `_ORMAtlas`
- State: `IMPORT_ISSUE`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_ProceduralBio_Shallows_ORM.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT ProceduralBio Shallows, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene. The surface must separate cavities, worn rough patches, and metallic substrate clearly enough to derive packed ORM masks.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4345 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldProceduralProxy/MAT_sargassum_leaf_scraps.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_sargassum_leaf_scraps_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for MAT sargassum leaf scraps, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4348 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_AbyssApex.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_AbyssApex_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Support AbyssApex, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4354 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_AbyssApex.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_AbyssApex_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Support AbyssApex, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4362 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_CreaturePassive.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_CreaturePassive_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Support CreaturePassive, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4368 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_CreaturePassive.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_CreaturePassive_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Support CreaturePassive, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4376 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_CreaturePredator.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_CreaturePredator_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Support CreaturePredator, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4382 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_CreaturePredator.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_CreaturePredator_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Support CreaturePredator, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4390 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_HazardPocket.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_HazardPocket_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Support HazardPocket, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4396 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_HazardPocket.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_HazardPocket_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Support HazardPocket, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4404 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_ReefApex.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_ReefApex_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Support ReefApex, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4410 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_ReefApex.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_ReefApex_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Support ReefApex, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4418 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_ResourcePocket.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_ResourcePocket_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Support ResourcePocket, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4424 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_ResourcePocket.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_ResourcePocket_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Support ResourcePocket, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4432 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_RuinApex.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_RuinApex_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Support RuinApex, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4438 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_RuinApex.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_RuinApex_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Support RuinApex, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4446 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_SafePocket.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_SafePocket_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Support SafePocket, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4452 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/WorldSupport/Mat_Support_SafePocket.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_SafePocket_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Support SafePocket, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4460 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_EggNest.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Organic_EggNest_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Organic EggNest, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4466 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_EggNest.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Organic_EggNest_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Organic EggNest, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4474 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_EggShell.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Organic_EggShell_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Organic EggShell, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4480 HABITAT_INTERIORS

- Source: `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_EggShell.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Organic_EggShell_Albedo.png`
- Resolution: 1024

Material Subject: High-end modular ocean habitat material for Mat Organic EggShell, warm off-white pressure panels, satin titanium frames, clean graphite service strips, soft teal engineering accents, amber safety markings, and optimistic NASA Punk industrial design built for a beautiful livable research base. Surface Wear and Details: Controlled scuffs, readable panel breaks, subtle salt whitening, fine rubber gasket wear, neat screw pockets, lightly abraded safety paint, tidy maintenance patina, and baked conduit and grate detail that sells depth without making the surface muddy or grim. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals baked AO in panel seams, grates, conduit gaps, and screw pockets, Green equals roughness 0.35 on worn sealed paint rising to 0.82 on rust, dust, and salt crust, Blue equals metallic 1.0 for exposed steel or aluminum and 0.0 for paint, rubber, and insulation. Use BC7 linear, mipmaps on.

### SHINOBU_361_PROMPT_4488 FLORA_EPIDERMIS

- Source: `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_PlantBud.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/Mat_Organic_PlantBud_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for Mat Organic PlantBud, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_4494 FLORA_EPIDERMIS

- Source: `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_PlantBud.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/Mat_Organic_PlantBud_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for Mat Organic PlantBud, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_4502 FLORA_EPIDERMIS

- Source: `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_PlantCanopy.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/Mat_Organic_PlantCanopy_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for Mat Organic PlantCanopy, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_4508 FLORA_EPIDERMIS

- Source: `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_PlantCanopy.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/Mat_Organic_PlantCanopy_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for Mat Organic PlantCanopy, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_4516 FLORA_EPIDERMIS

- Source: `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_PlantStem.mat`
- Slot: `_BaseMap`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/Mat_Organic_PlantStem_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for Mat Organic PlantStem, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.

### SHINOBU_361_PROMPT_4522 FLORA_EPIDERMIS

- Source: `Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_PlantStem.mat`
- Slot: `_MainTex`
- State: `EMPTY_REQUIRED_SLOT`
- Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/Mat_Organic_PlantStem_Albedo.png`
- Resolution: 1024

Material Subject: Beautiful abyssal flora epidermis for Mat Organic PlantStem, translucent kelp skin, pearlescent coral bark, soft membrane tissue, sea-glass greens, muted violet undertones, and elegant teal bioluminescent vein paths reserved for a separate emissive mask. Surface Wear and Details: Glossy wet film, delicate pore fields, fine vein relief, small barnacle scars, soft frilled membrane edges, luminous biological pattern logic, and high frequency organic breakup that feels alive and premium instead of diseased sludge. Lighting and View constraints: Perfect flat diffuse lighting, completely uniform overhead illumination with zero directional shadows, flat, top-down, orthogonal orthographic view, no perspective skew, no dramatic highlights, no baked light direction. Topology and Finish: Perfectly seamless, infinitely tileable texture with crisp AAA material readability, balanced mid-value diffuse tones, premium NASA Punk Deep Sea Noir mood, beautiful controlled color separation, high frequency detail baked into the texture for the Dear Lie, no muddy black wash, no horror grime, no bright clean saturated albedo, no text watermark, no border, no perspective object scene.

Normal plan: Create the albedo first, then generate a BC5 normal from controlled luminance height using the existing material audit workflow as the compatibility check. Scratches, salt scales, basalt pores, membrane veins, and chipped paint are compatible with height-to-normal extraction; large rivets, grates, and panel bevels must be baked as deliberate height shapes, not inferred from random contrast.

ORM plan: Pack _ORM as Red equals AO in pores, vein roots, and barnacle scars, Green equals roughness 0.18 on wet mucus film rising to 0.65 on scarred coral bark, Blue equals metallic 0.0. Store bioluminescent vein intensity in a separate emissive mask, not diffuse albedo. Use BC7 linear for ORM and keep emissive linear.
