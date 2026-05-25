# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# SHINOBU_361 Texture Generation Execution Playbook

Status: ACTIVE / OPERATOR READY / PENDING ART QA
Agent: SHINOBU_361
Scope: 175 unique texture targets from `TextureProductionQueue_SHINOBU_361_HANDMADE.md`

This is the working order for generating the textures. It explains what to do when references exist, what to do when they do not exist, how to preserve style unity, and how to accept or reject generated candidates.

## One Sentence Direction

HECTON-8 textures must look like expensive abyssal expedition equipment and beautiful alien ocean material science: precise, bright enough to read, physically believable, restrained in saturation, not grimy horror trash.

## Source Files

- Main prompt book: `Docs/Reports/TextureProductionQueue_SHINOBU_361_HANDMADE.md`
- Batch 01 golden prompt override: `Docs/Reports/TextureProductionBatch01_Blockers_GoldenPrompts_SHINOBU_361.md`
- Seed prompts when references are missing: `Docs/Reports/TextureReferenceBootstrap_SHINOBU_361.md`
- Style lock and reference rules: `Docs/Reports/TextureGenerationStyleLock_SHINOBU_361.md`
- PBR set and external pre-reference guide: `Docs/Reports/TexturePBRSetAndExternalReferenceGuide_SHINOBU_361.md`
- Unique production queue: `Docs/Reports/TextureProductionQueue_SHINOBU_361.csv`
- Import plan: `Docs/Reports/BatchImportTextures_SHINOBU_361_import_plan.csv`
- Manifest: `Docs/Reports/production_texture_manifest.csv`

## Current Scope

- Total unique production textures: 175
- Generate replacement PBR: 171
- Rebake source and fix import: 4
- Priority split: 15 `BLOCKER`, 154 `MEDIUM`, 6 `LOW`
- Category split: 126 `HABITAT_INTERIORS`, 26 `FLORA_EPIDERMIS`, 23 `GEOLOGY_TRIPLANAR`

## Non-Negotiable Prompt Contract

Every generation request must preserve:

- flat, top-down, orthogonal orthographic view
- completely uniform diffuse lighting
- zero directional shadows
- perfect seamless tiling
- no text, no generated labels, no logos, no watermark
- no border
- no perspective object scene
- no dramatic cinematic lighting

These are material sources. They are not posters, concept art, screenshots, or beauty renders.

## Style Contract

Use this material language across all prompts and references:

- Warm off-white ceramic/composite pressure surfaces.
- Satin titanium rails, ribs, fasteners, and reinforcement strips.
- Graphite rubber seals, anti-slip fields, grips, and gasket channels.
- Teal/cyan science accents for diagnostics, optics, and safe interaction.
- Amber locator/power/safety accents.
- Pearl, opal, pale silt, and turquoise mineral deposits.
- Restrained coral/violet/olive biology for living surfaces.
- Wear is controlled: polished use marks, tiny salt halos, fine scratches, mineral dust in seams.

Reject:

- black crushed grime
- abandoned horror grime
- rusty junkyard sci-fi
- military camouflage
- random warning labels
- text-like markings
- oversaturated neon diffuse colors
- cinematic shadows
- noisy detail that will shimmer after mip compression

## Reference Rule

Use at most three references per generation:

1. Global mood reference: Hecton planet/cloud/ocean palette.
2. Category reference: flora atlas, geology surface, or habitat seed.
3. Same-family approved reference: the best already-approved output for that family.

Never attach ten references. It will average the result into mud.

Internet images can be used only as do-references. Project-owned textures and approved outputs stay above them. Use real subsea engineering, Subnautica concept art, and Pinterest only to understand shape grammar and mood, never to copy a texture or override the HECTON style lock. Detailed source list and search strings are in `Docs/Reports/TexturePBRSetAndExternalReferenceGuide_SHINOBU_361.md`.

## If References Do Not Exist

Do not guess from random internet images. Generate seed references first.

Minimum seed pass:

1. `REF_SEED_001` - Premium Habitat Material Grammar
2. `REF_SEED_002` - Habitat Floor Navigation Language
3. `REF_SEED_010` - Flora Pearl Membrane, Kelp, And Coral Bridge
4. `REF_SEED_011` - Opal Basalt And Pale Sediment Geology

Optional seeds when needed:

- `REF_SEED_003` - Habitat Wall And Ceiling Utility Trim
- `REF_SEED_004` - Satin Titanium, Rubber, Ceramic Swatch Tile
- `REF_SEED_005` - Visor Glass And Sealed Transparent Polymer
- `REF_SEED_006` - Diegetic Terminal Surface Without Text
- `REF_SEED_007` - Tool Casing And Handheld Equipment
- `REF_SEED_008` - Gameplay Signal Surface Family
- `REF_SEED_009` - Resource Pocket Mineral Biology
- `REF_SEED_012` - Hecton Storm Sky And Ocean Color Plate

For each seed:

1. Generate 3 candidates.
2. Pick 1 winner.
3. Save the winner as look-dev reference only.
4. Use it as the same-family reference for production prompts.
5. Once two production textures in that family are approved, stop using the seed and use approved production textures instead.

Seed images are not production targets and do not change the 175-texture queue.

## Candidate Naming

Use stable names so QA can track decisions:

- Seed candidate: `LOOKDEV_REF_SEED_001_A.png`, `LOOKDEV_REF_SEED_001_B.png`, `LOOKDEV_REF_SEED_001_C.png`
- Seed winner: `LOOKDEV_APPROVED_REF_SEED_001.png`
- Production candidate: `CANDIDATE_SHINOBU_361_HAND_002_A.png`
- Approved albedo: use the target filename from the card, for example `floor_05_stripes_basecolor_Albedo.png`
- Approved normal: matching `_Normal.png`
- Approved ORM: matching `_ORM.png`

Do not rename final approved targets casually; Unity links and manifest paths depend on stable names.

## Batch 0 - Build The Look-Dev Base

Goal: establish visual taste before touching the final 175 textures.

Generate:

1. `REF_SEED_001` habitat master.
2. `REF_SEED_002` habitat floor/navigation.
3. `REF_SEED_004` material swatch.
4. `REF_SEED_010` flora bridge.
5. `REF_SEED_011` geology.

Optional if needed:

1. `REF_SEED_005` visor glass.
2. `REF_SEED_007` tool casing.
3. `REF_SEED_008` gameplay signal.
4. `REF_SEED_009` resource pocket.

Accept only if the result could sit in HECTON-8 without apology. If it looks technically correct but ugly, reject it.

## Batch 1 - The 15 Blockers

These are first because they affect the prologue/starting presentation.

Use `Docs/Reports/TextureProductionBatch01_Blockers_GoldenPrompts_SHINOBU_361.md` as the prompt source for this batch. It is the stronger V2 prompt set for the first 15 blockers.

Generate 3 candidates per target. Use habitat seed references until two production blockers are approved.

Blocker targets:

1. `Mat_HectonSurface_Normal.png`
2. `Mat_Visor_Glass_Albedo.png`
3. `ceiling_10_trimsheet_normal_Normal.png`
4. `floor_05_stripes_basecolor_Albedo.png`
5. `floor_05_trimsheet_normal_Normal.png`
6. `floor_large_8x8_trimsheet_normal_Normal.png`
7. `wall_01_2x3_a_stripes_basecolor_Albedo.png`
8. `wall_01_2x3_a_trimsheet_normal_Normal.png`
9. `wall_01_4x3_c_labels_basecolor_Albedo.png`
10. `wall_01_4x3_c_stripes_basecolor_Albedo.png`
11. `wall_01_4x3_c_trimsheet_normal_Normal.png`
12. `wall_01_4x3_door_02_wing_labels_basecolor_Albedo.png`
13. `wall_01_4x3_door_02_wing_stripes_basecolor_Albedo.png`
14. `wall_01_4x3_door_02_wing_trimsheet_normal_Normal.png`
15. `wall_04_3x6_d_trimsheet_normal_Normal.png`

Special rule for filenames containing `labels`: do not generate readable text. Use label-like recessed plates, blank decals, locator tabs, and abstract calibration blocks. Generated letters are rejected.

## Batch 2 - Promote The First Style Anchors

After Batch 1:

1. Pick the best habitat floor output.
2. Pick the best wall/ceiling trim output.
3. Pick the best glass/polymer output if available.
4. Mark them as `APPROVED_STYLE_ANCHOR`.

These anchors become stronger than bootstrap seeds. From here, all habitat/tool/support prompts should reference the approved anchors.

## Batch 3 - Flora

Generate all 26 `FLORA_EPIDERMIS` targets.

Reference pack:

1. Existing flora atlas if useful: `TX_ProceduralBio_Shallows_AlbedoAtlas.png`.
2. Matching imported family albedo if available: kelp/coral.
3. `REF_SEED_010` winner only if the existing refs are insufficient.
4. Approved same-family output after the first two flora textures pass.

Visual target:

- beautiful alien biology
- healthy wet surfaces
- pearl membrane
- olive/teal kelp
- coral/violet reef structures
- cyan bioluminescent veins only as mask planning, not bright diffuse paint

Reject:

- gore
- rot
- horror slime
- oversaturated neon
- random tentacle clutter
- edible candy colors

## Batch 4 - Geology

Generate all 23 `GEOLOGY_TRIPLANAR` targets.

Reference pack:

1. `surface_diff.png` for planetary palette.
2. `surface_norm.png` or `surface_spec.png` for material response.
3. `REF_SEED_011` winner if no strong rock reference exists.
4. Approved same-family output after the first two geology textures pass.

Visual target:

- blue-charcoal basalt as base, never pure black
- pale silt deposits
- opal mineral veins
- turquoise hydrothermal staining
- pearl fracture dust
- orientation-neutral cracks

Reject:

- gray noise
- directional scene lighting
- cliff photos
- obvious top/bottom orientation
- high-contrast cracks that tile visibly

## Batch 5 - Remaining Habitat, Tools, Resources, Support

Generate remaining `HABITAT_INTERIORS` after blocker anchors exist.

Reference pack:

1. Approved habitat floor or wall anchor.
2. Function-specific seed if needed:
   - `REF_SEED_005` glass/polymer
   - `REF_SEED_006` terminal
   - `REF_SEED_007` tools
   - `REF_SEED_008` gameplay signals
   - `REF_SEED_009` resources
3. Same-family approved output when available.

Visual target:

- consistent hard-surface language
- clear gameplay readability
- material beauty first, warning colors second
- no fake text or generated UI labels

## Batch 6 - Low Priority Sky / Celestial / Residual

Generate the 6 `LOW` targets last.

Reference pack:

1. `Aegir_storms.png`
2. `clouds0_diff.png`
3. `REF_SEED_012` winner if needed

These can be more painterly, but they still must not become generic space art or cinematic screenshots.

## Production Candidate Review

Each candidate gets one of these decisions:

- `APPROVE`: use as final albedo/height source.
- `PROMOTE_REF`: strong enough to become a same-family reference.
- `RETRY_PROMPT`: prompt direction is right, candidate failed.
- `REJECT`: wrong style or unusable source.

Reject immediately if:

- it has text, numbers, fake labels, logos, or watermarks
- it is not seamless
- it has perspective objects
- it uses dramatic lighting
- it is too dark to read after mip compression
- it looks like abandoned horror grime
- it has random high-frequency noise
- it loses material separation
- it cannot produce a sensible normal/ORM set

## PBR Build Rules

Expanded operator detail is in `Docs/Reports/TexturePBRSetAndExternalReferenceGuide_SHINOBU_361.md`. The short version below is not enough for final import; it is a reminder.

For every approved source:

### Albedo

- Role: color and material identity only.
- Format: BC7 for Standalone.
- sRGB: on.
- No baked highlights.
- No baked shadow.

### Normal

- Role: shallow fake geometry.
- Format: BC5.
- sRGB: off.
- Use dedicated normal/height generation for trim sheets, floors, panels, geology, and strong organic relief.
- Use luminance-derived normal only when detail is shallow and clean.

### ORM

- SHINOBU authoring format: packed RGB, sRGB off.
- Red: ambient occlusion.
- Green: roughness.
- Blue: metallic.
- URP Lit warning: standard URP Lit expects Metallic in Red, Occlusion in Green, and Smoothness in Alpha. If the material route is raw URP Lit, repack before assignment: `R=Metallic`, `G=AO`, `A=1-Roughness`.

Typical values:

- Ceramic/composite: Metallic 0.0, Roughness 0.45-0.75.
- Satin titanium: Metallic 1.0, Roughness 0.28-0.55.
- Graphite rubber: Metallic 0.0, Roughness 0.65-0.9.
- Glass/polymer: Metallic 0.0, Roughness 0.05-0.28 depending on scratches.
- Flora membrane: Metallic 0.0, Roughness 0.25-0.65 with wet variation.
- Basalt/silt: Metallic 0.0, Roughness 0.58-0.9.
- Opal/mineral flecks: Metallic 0.0-0.15, Roughness 0.18-0.45.

## Unity Import Order

1. Place approved files into their target paths.
2. Run `Tools/BatchImportTextures.py` dry-run.
3. Check import plan for target suffix and role.
4. Apply import metadata only to existing Unity `.meta` files.
5. Open Unity and let it import.
6. Validate material previews.
7. Validate SceneView/route surfaces.
8. Only after Unity import and visual QA, rerun static audit.

No runtime readiness claim without Unity import, Console, Play Mode, Memory Profiler, Frame Debugger, and capture evidence.

## What The Artist/Operator Sends To The Generator

For a normal production texture after seed approval:

1. One prompt paragraph from `TextureProductionQueue_SHINOBU_361_HANDMADE.md`.
2. One global/style anchor image if needed.
3. One category image.
4. One same-family approved image.

Do not add explanations, markdown, tables, file paths, or internal notes into the generator prompt. The generator should receive the clean prompt text and the selected reference images only.

## Example: First Floor Texture

Texture: `floor_05_stripes_basecolor_Albedo.png`

References:

1. `LOOKDEV_APPROVED_REF_SEED_001.png`
2. `LOOKDEV_APPROVED_REF_SEED_002.png`
3. Later, approved `floor_05_stripes_basecolor_Albedo.png` becomes the family ref for other floor/stripe textures.

Prompt:

Use card `SHINOBU_361_HAND_002` from the handmade prompt book.

Acceptance:

- broad readable floor plates
- warm off-white composite
- graphite anti-slip
- amber route stripes
- teal maintenance accents
- controlled salt/wear
- no readable labels
- no perspective scene
- seamless tile

## Example: First Flora Texture

References:

1. `TX_ProceduralBio_Shallows_AlbedoAtlas.png`
2. matching imported kelp/coral albedo when available
3. `LOOKDEV_APPROVED_REF_SEED_010.png` only if existing refs are insufficient

Prompt:

Use the matching flora card from the handmade prompt book.

Acceptance:

- beautiful living material
- restrained biological color
- wet specular planning
- emissive veins planned as mask, not painted neon albedo
- seamless tile
- no gore, no rot, no horror slime

## Example: First Geology Texture

References:

1. `surface_diff.png`
2. `surface_norm.png`
3. `LOOKDEV_APPROVED_REF_SEED_011.png`

Prompt:

Use the matching geology card from the handmade prompt book.

Acceptance:

- opal basalt and pale sediment
- triplanar-safe orientation
- no cliff photo
- no directional shadow
- seamless tile
- clean mineral identity

## Done Criteria

The texture generation work is not done when images exist. It is done when:

1. Every one of the 175 target textures has an approved albedo or rebaked source.
2. Every generated albedo has matching normal and ORM plan executed.
3. All files are placed at target paths.
4. Import metadata is correct and channel packing matches the material route.
5. Static audit no longer reports these missing/stub debts.
6. Unity material previews are visually acceptable.
7. Route surfaces do not show magenta, flat placeholders, wrong tiling, text artifacts, or black-grime drift.
