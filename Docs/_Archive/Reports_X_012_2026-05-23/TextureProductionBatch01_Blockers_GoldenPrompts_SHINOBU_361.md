# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# SHINOBU_361 Batch 01 Golden Prompts - 15 Blockers

Status: ACTIVE / GOLDEN PROMPT OVERRIDE / PENDING ART QA
Agent: SHINOBU_361
Scope: first 15 `BLOCKER` targets from `TextureProductionQueue_SHINOBU_361.csv`

Use this file for the first real generation batch. These prompts supersede the older blocker prompt wording in the handmade queue for Batch 01 only. The target paths and PBR plans remain the same.

Wall correction: after first `REF_SEED_001` review, wall/blocker prompts `B01_007` through `B01_015` are superseded by the layered wall workpack prompts in `Docs/Reports/TextureGeneratorWorkpack_SHINOBU_361/Prompts/Batch01_Blockers/` and `NEXT_RETRY_WALL_SYSTEM.md`. Do not use old wall wording that produces repeated rounded square panels.

## Batch 01 Goal

This batch must define the whole HECTON-8 material taste. The outputs should look beautiful, precise, expensive, and useful in gameplay. The player should feel they are inside a funded abyssal research habitat, not a broken junk station.

Reference setup when no approved production references exist:

- Primary seed: `LOOKDEV_APPROVED_REF_SEED_001.png`
- Floor seed: `LOOKDEV_APPROVED_REF_SEED_002.png`
- Material swatch seed: `LOOKDEV_APPROVED_REF_SEED_004.png`
- Glass seed for visor only: `LOOKDEV_APPROVED_REF_SEED_005.png`
- Optional global mood: one Hecton planet/cloud reference, used for palette only

After two good blocker outputs exist, promote them to `APPROVED_STYLE_ANCHOR` and use those instead of seed references.

## B01_001 - Mat_HectonSurface_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HectonSurface_Normal.png`

Reference pack:

- Hecton planet surface mood reference if available.
- `LOOKDEV_APPROVED_REF_SEED_011.png` if geology seed exists.

Prompt to copy:
Create a premium heightfield source for the planet Hecton as an alien ocean-world survey surface, designed for conversion into a clean BC5 normal map. Build elegant large-scale landform relief: smooth abyssal shelves, soft basin ridges, continental mineral shelves, pearl-white sediment plains, opal fracture networks, and subtle turquoise hydrothermal seams. The image should feel scientific, expensive, and readable, like a high-resolution orbital geology plate prepared by a NASA ocean lab. Avoid crater spam, random noise, black pits, and dramatic landscape lighting; this is a material source, not a space painting. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Normal plan:
Generate BC5 normal from broad landform height first, then fine mineral relief. Keep all relief smooth and readable.

ORM plan:
Red AO only in basin ridges and mineral fractures. Green roughness high on sediment plains and medium on mineral crust. Blue metallic near zero except rare mineral flecks.

Accept if:
It reads as beautiful alien-ocean geology, not a noisy fantasy planet.

## B01_002 - Mat_Visor_Glass_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Visor_Glass_Albedo.png`

Reference pack:

- `LOOKDEV_APPROVED_REF_SEED_005.png`
- Optional approved habitat material swatch anchor

Prompt to copy:
Create a seamless albedo source for premium pressure-rated visor glass used in HECTON-8 expedition helmets. Show a clean smoky-clear transparent polymer with a faint sea-green optical coating, subtle laminated layers, graphite gasket contact haze, delicate anti-fog wipe arcs, tiny hairline micro-scratches, soft pressure stress curves, and a few pale salt freckles near sealed edges. The material should feel wearable, expensive, aerospace-grade, and maintained. Keep the diffuse restrained and elegant; shine will come from shader reflection and roughness, not painted highlights. Do not create cracked horror glass or mirrored chrome. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Normal plan:
Very shallow BC5 normal for micro-scratches, coating arcs, and edge stress. No deep cracks.

ORM plan:
Red AO almost zero except gasket contact haze. Green roughness 0.06 to 0.28 depending on scratch density. Blue metallic 0.

Accept if:
It feels like expensive scientific visor material, not damage decal glass.

## B01_003 - ceiling_10_trimsheet_normal_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/ceiling_10_trimsheet_normal_Normal.png`

Reference pack:

- `LOOKDEV_APPROVED_REF_SEED_001.png`
- `LOOKDEV_APPROVED_REF_SEED_003.png` if available

Prompt to copy:
Create a beautiful modular abyssal habitat ceiling trim heightfield source for BC5 normal baking. Design recessed service channels, quiet acoustic baffles, gasketed inspection seams, countersunk fastener wells, cable raceway lips, shallow pressed ribs, and small maintenance hatch outlines. The construction language should be calm and premium: off-white ceramic-coated pressure panels, satin titanium ribs, graphite rubber separators, amber locator inserts, and pale mineral dust gathered only in the deepest grooves. Leave clean open panel areas so the ceiling feels engineered and spacious, not busy. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Normal plan:
Bake panel ribs, baffles, gasket lips, wells, and raceway grooves into a shallow BC5 normal. Avoid crushed dents and jagged damage.

ORM plan:
Red AO in grooves and fastener wells. Green roughness 0.42 on satin coating, 0.72 on rubber and salt-dusted seams. Blue metallic only for exposed titanium ribs.

Accept if:
It looks like a premium pressure habitat ceiling, not random sci-fi panel noise.

## B01_004 - floor_05_stripes_basecolor_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_05_stripes_basecolor_Albedo.png`

Reference pack:

- `LOOKDEV_APPROVED_REF_SEED_001.png`
- `LOOKDEV_APPROVED_REF_SEED_002.png`
- Optional approved habitat floor anchor after it exists

Prompt to copy:
Create a seamless albedo source for the first HECTON-8 prologue route floor. Make a confident modular floor panel with warm off-white composite pressure plates, satin graphite anti-slip insets, thin amber route stripes, restrained teal maintenance ticks, satin titanium edge rails, rubber isolation seams, and shallow drainage lines. The floor should look safe, premium, readable, and used with care, like a well-funded ocean research base. Add curated boot polish, soft scuffs in walking lanes, tiny edge chips, pale salt halos in seams, and subtle rubber sheen. Keep large shapes broad enough for gameplay navigation and close-up quality high enough for first-person inspection. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Normal plan:
Generate shallow BC5 normal from anti-slip grain, seam lips, rail bevels, stripe paint thickness, and drainage lines.

ORM plan:
Red AO in seams and rail lips. Green roughness 0.55 for composite, 0.74 for anti-slip rubber, 0.38 on polished walking lanes, 0.65 on painted stripes. Blue metallic 1.0 on exposed titanium rails only.

Accept if:
It feels inviting and functional, not warning-sign clutter or industrial dirt.

## B01_005 - floor_05_trimsheet_normal_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_05_trimsheet_normal_Normal.png`

Reference pack:

- `LOOKDEV_APPROVED_REF_SEED_002.png`
- approved floor albedo anchor if available

Prompt to copy:
Create a heightfield source for a clean modular habitat floor trim sheet normal map. Build shallow engineered geometry: fine anti-slip ridges, inset drainage channels, rounded composite panel seams, rubber gasket lips, titanium rail screw sockets, access plate bevels, quiet pressure-formed ribs, and small service cover outlines. The surface should feel strong, level, and premium underfoot. Keep the rhythm organized and the value structure balanced so the normal map will read clearly without shimmer. Avoid heavy dents, random cracks, and noisy scratches across every area. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Normal plan:
Bake anti-slip ridges as fine detail, seams and drainage channels as medium detail, screw sockets as rounded forms. BC5 normal only.

ORM plan:
Red AO in channels, screw sockets, and seam lips. Green roughness high on ridges, medium on sealed composite, lower on worn titanium rail rims. Blue metallic only for rail and screw metal.

Accept if:
It produces a clean premium floor normal, not a noisy grayscale scratch plate.

## B01_006 - floor_large_8x8_trimsheet_normal_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_large_8x8_trimsheet_normal_Normal.png`

Reference pack:

- `LOOKDEV_APPROVED_REF_SEED_002.png`
- approved floor anchor if available

Prompt to copy:
Create a large-format 8x8 meter habitat floor heightfield source for BC5 normal baking. The design should feel like a spacious expedition deck: broad composite pressure plates, long satin titanium reinforcement spines, recessed utility covers, wide quiet anti-slip fields, soft gasket seams, sparse circular maintenance hatches, and shallow orientation strips. Keep the composition dignified and calm, with large clean shapes that will not shimmer in gameplay. Add only refined wear cues: polished foot-traffic arcs, pale mineral dust in seam corners, soft rubber compression on walking paths, and tiny paint abrasion on amber locator strips. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Normal plan:
Low-amplitude BC5 normal. Large forms should lead; high-frequency detail must stay restrained.

ORM plan:
Red AO under hatches, spines, and seam lips. Green roughness 0.55 to 0.82 across composite/rubber, lower on polished walk arcs. Blue metallic on reinforcement spines and hatch rims.

Accept if:
It feels spacious and premium, not noisy industrial plating.

## B01_007 - wall_01_2x3_a_stripes_basecolor_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_2x3_a_stripes_basecolor_Albedo.png`

Reference pack:

- `LOOKDEV_APPROVED_REF_SEED_001.png`
- `LOOKDEV_APPROVED_REF_SEED_003.png`
- approved wall anchor if available

Prompt to copy:
Create a seamless albedo source for a 2x3 meter starter corridor wall stripe panel in a premium abyssal habitat. Use warm off-white pressure plating, satin titanium frame rails, graphite gasket borders, a slim amber direction stripe, and one restrained teal service stripe. The wall should feel safe, bright enough to read, and cleanly manufactured for a funded ocean research mission. Add soft hand scuffs near access zones, pale salt dust only in seams, tiny coating chips on hard edges, and mild cleaning swirls. Keep the panel hierarchy clear and calm. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Normal plan:
Derive BC5 normal from plate bevels, gasket borders, frame rails, and paint thickness. Keep stripes painted, not chunky.

ORM plan:
Red AO under frame and gasket edges. Green roughness 0.48 on coating, 0.68 on rubber, 0.55 on painted stripes, lower on worn edges. Blue metallic on exposed frame rails only.

Accept if:
It guides the player without looking like hazard wallpaper.

## B01_008 - wall_01_2x3_a_trimsheet_normal_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_2x3_a_trimsheet_normal_Normal.png`

Reference pack:

- `LOOKDEV_APPROVED_REF_SEED_003.png`
- approved wall anchor if available

Prompt to copy:
Create a heightfield source for a 2x3 modular habitat wall trim sheet. Design shallow pressed geometry with rectangular service plates, soft bevels, gasket grooves, recessed fastener wells, narrow cable conduit covers, slim frame lips, and small inspection hatch outlines. The wall must feel precise, clean, and expensive, with smooth open plate fields between details so lighting can breathe. Use deliberate manufacturing rhythm and avoid random scratched clutter. This is a normal-map source for a maintained pressure habitat, not a ruined wall. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Normal plan:
BC5 normal with medium depth for seams, shallow depth for grooves, and clean rounded fastener wells.

ORM plan:
Red AO in grooves and hatch outlines. Green roughness higher on gaskets and composite, lower on satin metal bevels. Blue metallic only on frame lips.

Accept if:
It reads as organized wall construction, not a random panel generator output.

## B01_009 - wall_01_4x3_c_labels_basecolor_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_labels_basecolor_Albedo.png`

Reference pack:

- `LOOKDEV_APPROVED_REF_SEED_001.png`
- `LOOKDEV_APPROVED_REF_SEED_006.png` if terminal/no-text seed exists
- approved wall anchor if available

Prompt to copy:
Create a seamless albedo source for a 4x3 meter premium habitat wall panel with technical label zones but no readable text. Use off-white ceramic pressure panels, graphite service bands, small amber locator blocks, teal maintenance tabs, satin titanium separators, and blank label plaques shaped like professional equipment tags. The design should imply organized engineering information while containing no letters, numbers, words, symbols, logos, or fake UI. Add tasteful edge wear, faint cleaning swirls, pale salt freckles in lower seams, and subtle discoloration around access plates. The panel should feel calm, bright, expensive, and practical. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Normal plan:
Support blank plaques, access plates, gasket seams, and bevels. Do not emboss generated text.

ORM plan:
Red AO around plaques and plates. Green roughness 0.5 to 0.75 across ceramic, graphite, and paint. Blue metallic on fasteners and separator strips only.

Accept if:
It has label structure without any generated typography artifacts.

## B01_010 - wall_01_4x3_c_stripes_basecolor_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_stripes_basecolor_Albedo.png`

Reference pack:

- `LOOKDEV_APPROVED_REF_SEED_001.png`
- `LOOKDEV_APPROVED_REF_SEED_003.png`
- approved wall anchor if available

Prompt to copy:
Create a seamless albedo source for a wide 4x3 meter HECTON-8 corridor wall stripe panel. Compose broad off-white pressure panels, a graphite lower service band, satin titanium separators, a thin amber route stripe, and a muted teal system stripe. The panel should guide the player through a bright underwater research corridor without becoming noisy signage. Add careful scuffs, rubbed corners, pale salt in lower seams, clean manufactured edges, and slight hand-polish near access zones. Keep the image attractive, balanced, and premium. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Normal plan:
BC5 normal from broad bevels, separator lips, stripe paint thickness, and lower band transitions.

ORM plan:
Red AO in separator lips and lower seams. Green roughness medium on coating, high on graphite band, lower on titanium separators. Blue metallic only on separators and fasteners.

Accept if:
It reads as elegant route architecture, not warning tape.

## B01_011 - wall_01_4x3_c_trimsheet_normal_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_trimsheet_normal_Normal.png`

Reference pack:

- `LOOKDEV_APPROVED_REF_SEED_003.png`
- approved wall anchor if available

Prompt to copy:
Create a normal-map height source for a large 4x3 meter habitat wall trim sheet. Build a calm modular rhythm of wide pressure panel bevels, recessed horizontal service troughs, graphite gasket channels, low-profile handhold recesses, thin satin titanium frame steps, small access cover screw wells, and soft service hatch outlines. Leave smooth plate fields between details so the surface feels premium and architectural. Avoid clutter, jagged scratches, crushed panels, or distressed ruin language. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Normal plan:
BC5 normal with clean bevel gradients, medium trough depth, and restrained microdetail.

ORM plan:
Red AO in troughs, recesses, and screw wells. Green roughness high on gasket channels, medium on plates, low-medium on titanium steps. Blue metallic on titanium steps.

Accept if:
It feels like a hero corridor wall normal source.

## B01_012 - wall_01_4x3_door_02_wing_labels_basecolor_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_labels_basecolor_Albedo.png`

Reference pack:

- `LOOKDEV_APPROVED_REF_SEED_001.png`
- `LOOKDEV_APPROVED_REF_SEED_006.png` if available
- approved door/wall anchor if available

Prompt to copy:
Create a seamless albedo source for a premium abyssal habitat bulkhead door wing with blank technical label fields. Use warm off-white pressure plating, graphite hinge-side rubber, satin titanium latch plates, amber caution blocks, teal maintenance tabs, blank professional label plaques, and clean gasket borders. The door should feel safe, sealed, expensive, and maintained. Add slight hand polish around handle zones, gentle salt whitening near lower gaskets, tiny chipped paint at latch contact points, and subtle cleaning swirls. Do not create readable words, numbers, warning text, logos, symbols, or fake UI marks. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Normal plan:
Normal from latch bevels, gasket steps, blank plaques, hinge covers, and handle-zone wear. Keep label fields flat for later decals.

ORM plan:
Red AO around latches, hinge covers, plaques, and gasket edges. Green roughness high on rubber, medium on panels, lower on polished latch plates. Blue metallic on latch and hinge metal only.

Accept if:
It has door information architecture without generated text artifacts.

## B01_013 - wall_01_4x3_door_02_wing_stripes_basecolor_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_stripes_basecolor_Albedo.png`

Reference pack:

- `LOOKDEV_APPROVED_REF_SEED_001.png`
- approved door/wall anchor if available

Prompt to copy:
Create a seamless albedo source for a prologue habitat bulkhead door wing with elegant stripe language. Build a confident asymmetric layout of off-white sealed door panels, graphite gasket borders, satin titanium hinge rail, thin amber pressure-alert stripe, and a muted teal route identifier stripe. The result should read as a beautiful emergency-rated door with real pressure hardware, not a warning poster. Add controlled edge chips where panels meet, subtle hand wear near handle zones, pale salt dust near the bottom seal, and clean color blocking. Keep saturation restrained and material separation clear. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Normal plan:
Normal from door bevels, hinge rail, gasket border, and stripe paint thickness. Keep door face readable at gameplay distance.

ORM plan:
Red AO under gasket borders and hinge rail. Green roughness medium on panels, high on seals, lower near polished handle areas. Blue metallic on hinge and latch metal.

Accept if:
It communicates sealed safety and premium engineering without visual clutter.

## B01_014 - wall_01_4x3_door_02_wing_trimsheet_normal_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_trimsheet_normal_Normal.png`

Reference pack:

- `LOOKDEV_APPROVED_REF_SEED_003.png`
- approved door/wall anchor if available

Prompt to copy:
Create a heightfield source for a sealed habitat door wing trim sheet normal map. Design broad inset pressure plates, rounded gasket grooves, latch recesses, hinge rail bevels, small inspection caps, shallow handle-wear depressions, and soft maintenance cover outlines. The geometry should feel robust, elegant, and pressure-rated, with strong premium industrial rhythm and clean open areas. Avoid jagged damage, ruin texture, random scratches, and deep gouges. This door is maintained and mission-critical. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Normal plan:
BC5 normal with clear medium-depth seams and shallow gasket/latch detail. Avoid hard value cliffs that invert normal extraction.

ORM plan:
Red AO in gasket grooves, latch recesses, and hinge rail underside. Green roughness high on seals, medium on coated plates, lower on polished latch zones. Blue metallic on hinge and latch areas.

Accept if:
It looks like a maintained pressure door normal source, not damaged scrap metal.

## B01_015 - wall_04_3x6_d_trimsheet_normal_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_04_3x6_d_trimsheet_normal_Normal.png`

Reference pack:

- `LOOKDEV_APPROVED_REF_SEED_003.png`
- approved wall anchor if available

Prompt to copy:
Create a tall 3x6 meter habitat wall trim sheet heightfield source for a premium underwater research module. Build elegant vertical pressure panels, long satin titanium ribs, recessed life-support conduit covers, slim graphite gasket seams, circular diagnostic ports, shallow service hatch outlines, and soft rib-edge wear. The surface should feel tall, calm, architectural, and expensive, with enough open space between details for clean lighting response. Add only refined maintained wear: slight mineral dust in lower seams, gentle access-panel scuffs, and softened titanium rib edges. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Normal plan:
BC5 normal with long clean vertical gradients for ribs, medium seams for hatches, and restrained microdetail.

ORM plan:
Red AO in conduit covers, diagnostic ports, and lower gasket seams. Green roughness medium-high on panels and gaskets, lower on satin titanium ribs. Blue metallic only for ribs and port rims.

Accept if:
It becomes the tall-wall style anchor for later habitat modules.

## Batch 01 Approval Rule

Do not approve all 15 just because they exist. At least three outputs must be promoted as anchors:

1. One floor anchor.
2. One wall/ceiling trim anchor.
3. One glass/polymer anchor if visor output succeeds.

If Batch 01 does not produce these anchors, rerun the weak prompts before touching the remaining 160 production targets.
