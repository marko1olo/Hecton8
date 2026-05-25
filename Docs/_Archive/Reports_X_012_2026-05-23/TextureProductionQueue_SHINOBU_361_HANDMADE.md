# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# SHINOBU_361 Handmade Texture Prompts

Status: HANDMADE COVERAGE COMPLETE / PENDING ART QA
Evidence class: HUMAN_AUTHORED_PRODUCTION_TEXT

This file is the manual rewrite pass. It is not generated from the template prompt body. The goal is to give image generators art-direction that can produce attractive HECTON-8 material images with clear shape language and premium surface taste.

Wall correction: the active wall generation prompts are now the layered wall workpack files in `Docs/Reports/TextureGeneratorWorkpack_SHINOBU_361/`. First `REF_SEED_001` tile-like output was rejected. For wall targets, use `NEXT_RETRY_WALL_SYSTEM.md` and the patched `B01_007` through `B01_015` workpack files before falling back to older text here.

## Style Target

HECTON-8 texture style is premium expedition hardware under the ocean: beautiful, precise, lived-in, pressure-rated, and expensive. It should feel like NASA research equipment adapted for abyssal survival, with clean silhouettes, readable material separation, warm off-white pressure panels, satin titanium, graphite rubber, pale mineral dust, teal engineering paint, amber safety accents, restrained coral/violet biological color, and controlled salt wear. Use clean value structure, curated wear, restrained saturation, deliberate sci-fi manufacturing detail, and neutral flat lighting.

Every image prompt still requires flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

## BLOCKER PROMPTS - Prologue Habitat Surfaces

### SHINOBU_361_HAND_001 - ceiling_10_trimsheet_normal_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/ceiling_10_trimsheet_normal_Normal.png`

Prompt to copy:
Premium heightfield source for a modular abyssal habitat ceiling trim sheet, designed for a clean BC5 normal map. Make a precise pattern of recessed service channels, thin acoustic baffles, gasketed inspection seams, countersunk fastener wells, cable raceway lips, and shallow pressed ribs. The surface should feel engineered and expensive: satin titanium under off-white ceramic coating, graphite rubber separators, small amber locator strips, and fine salt dust gathered only inside seam grooves. Keep the design bright enough to read, with crisp bevel logic and balanced mid-value forms. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Use this as a height/normal source, not a color hero texture. Convert panel ribs, gasket lips, fastener wells, and raceway grooves into a BC5 tangent-space normal. Keep depth shallow and clean so ceiling modules do not look like crushed junk.

ORM plan:
Red AO should be strongest inside service grooves and fastener wells. Green roughness should range from 0.42 on satin coated panels to 0.72 on rubber baffles and salt-dusted seams. Blue metallic only for exposed titanium pinstripes, otherwise 0.

### SHINOBU_361_HAND_002 - floor_05_stripes_basecolor_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_05_stripes_basecolor_Albedo.png`

Prompt to copy:
Premium NASA Punk habitat floor stripe base color texture for the player prologue route. Make a clean modular floor panel with warm off-white composite plates, satin graphite anti-slip inserts, thin amber safety stripes, subtle teal maintenance marks, and narrow titanium edge rails. It should look functional and inviting, like a well-funded ocean research base with maintained expedition wear. Add controlled boot scuffs, a few salt halos near panel seams, tiny edge chips, and faint rubber polish in walking lanes. Preserve wide readable shapes for gameplay navigation. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Generate a shallow BC5 normal from anti-slip grain, stripe paint thickness, panel seam lips, and rail bevels. Do not make deep dents; the floor must stay premium and level.

ORM plan:
Red AO in plate seams and under rail lips. Green roughness around 0.58 for composite plates, 0.74 for anti-slip rubber, 0.38 on lightly polished walking lanes, and 0.65 on painted amber stripes. Blue metallic 1.0 only for exposed titanium rails.

### SHINOBU_361_HAND_003 - floor_05_trimsheet_normal_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_05_trimsheet_normal_Normal.png`

Prompt to copy:
Heightfield source for a clean modular habitat floor trim sheet normal map. Create engineered shallow geometry: anti-slip micro ridges, inset drainage channels, rounded panel seams, gasket lips, rail screw sockets, small access plate bevels, and subtle pressure-formed metal ribs. The image must be readable as premium expedition flooring, with balanced mid-value forms and clean maintained seam contrast. Use a precise industrial rhythm that can tile without visible repetition. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Bake to BC5 normal. Anti-slip ridges should be fine, panel seams medium depth, drainage channels slightly deeper, screw sockets rounded and non-spiky.

ORM plan:
Red AO in drainage channels, screw sockets, and seam lips. Green roughness high on anti-slip ridges, medium on sealed composite, lower on worn metal rails. Blue metallic only for rails and screw rims.

### SHINOBU_361_HAND_004 - floor_large_8x8_trimsheet_normal_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/floor_large_8x8_trimsheet_normal_Normal.png`

Prompt to copy:
Large-format habitat floor heightfield for an 8x8 meter modular room tile. Build a dignified expedition deck: broad composite panels, long satin titanium reinforcement spines, recessed utility covers, quiet anti-slip fields, soft gasket seams, and sparse circular maintenance hatches. The surface should feel spacious and premium, not noisy. Include only subtle wear: polished foot traffic arcs, pale salt caught in seam corners, and tiny paint abrasion on amber orientation strips. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Bake a low-amplitude BC5 normal. The big floor must not shimmer; keep high frequency detail restrained and use strong shape hierarchy.

ORM plan:
Red AO under hatches and reinforcement spines. Green roughness 0.55 to 0.8 depending on rubber/composite, with slightly lower roughness on polished walk arcs. Blue metallic on titanium reinforcement only.

### SHINOBU_361_HAND_005 - Mat_HectonSurface_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HectonSurface_Normal.png`

Prompt to copy:
Planetary surface normal source for Hecton seen as a clean, readable alien ocean world material. Create broad geological height forms: smooth abyssal shelves, shallow basin ridges, pale mineral crust networks, soft sediment plains, and elegant tectonic contour bands. Keep the surface attractive and scientific, like a high-resolution planetary survey plate with deliberate large landforms. Use subtle opal mineral veins and fine sediment texture only as height cues. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Generate BC5 normal directly from large landform height and fine mineral texture. Avoid high contrast crater spam; this map should support beautiful planetary material response.

ORM plan:
Red AO in basin ridges and mineral fracture networks. Green roughness high on sediment plains, medium on mineral crust. Blue metallic near zero except rare mineral flecks.

### SHINOBU_361_HAND_006 - Mat_Visor_Glass_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Visor_Glass_Albedo.png`

Prompt to copy:
Premium deep sea visor glass albedo texture for a player helmet surface. Make a clean smoked glass material with faint sea-green tint, subtle laminated layers, transparent graphite edge seals, tiny micro-scratches, a few pressure stress arcs, and barely visible anti-fog coating swirls. The result should feel expensive, sharp, wearable, and aerospace-grade. Keep the diffuse mostly clear and restrained; reflection and shader should provide shine later. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Only very shallow BC5 normal detail for micro-scratches, stress arcs, and coating swirls. Do not add deep cracks unless a separate damage decal asks for it.

ORM plan:
Red AO almost zero except edge seal zones. Green roughness low to medium: 0.08 clean glass, 0.22 on anti-fog coating, 0.36 on micro-scratched edge wear. Blue metallic 0.

### SHINOBU_361_HAND_007 - wall_01_2x3_a_stripes_basecolor_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_2x3_a_stripes_basecolor_Albedo.png`

Prompt to copy:
Clean modular habitat wall stripe base color for a 2x3 meter starter corridor panel. Use warm off-white pressure plating, a satin titanium frame, graphite gasket borders, slim amber direction stripes, and one restrained teal service stripe. Make it feel like a bright, safe underwater research module, with clear panel hierarchy for navigation. Add soft hand scuffs near access zones, pale salt dust in seams, tiny coating chips on hard edges, and no heavy rust. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Derive BC5 normal from plate bevels, gasket borders, and paint thickness. Keep labels/stripes readable as paint, not raised plastic blocks.

ORM plan:
Red AO under frame edges and gasket borders. Green roughness 0.48 on coated plates, 0.68 on graphite gasket, 0.55 on amber/teal paint, lower on worn hand-contact edges. Blue metallic on exposed frame only.

### SHINOBU_361_HAND_008 - wall_01_2x3_a_trimsheet_normal_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_2x3_a_trimsheet_normal_Normal.png`

Prompt to copy:
Heightfield source for a 2x3 modular habitat wall trim sheet. Design shallow pressed panel geometry: rectangular service plates, elegant bevels, gasket grooves, recessed screw wells, narrow cable conduit covers, and a few small inspection hatch outlines. Keep forms crisp and readable, with premium manufacturing precision. Avoid random scratches covering everything; leave calm open plate areas for lighting to breathe. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Bake to BC5 normal. Use medium depth for panel seams, shallow depth for gasket grooves, and tiny but clean fastener wells.

ORM plan:
Red AO in grooves and hatch outlines. Green roughness higher on gaskets and open composite panels, lower on satin metal bevels. Blue metallic only on the frame/bevel language.

### SHINOBU_361_HAND_009 - wall_01_4x3_c_labels_basecolor_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_labels_basecolor_Albedo.png`

Prompt to copy:
Premium habitat wall label base color for a 4x3 meter interior panel, designed for a readable prologue corridor. Use off-white ceramic pressure panels, graphite service bands, small amber locator blocks, teal maintenance tabs, and blank label plaques that imply technical labeling without actual readable text. The panel should look organized, calm, and high budget. Add tasteful edge wear, faint cleaning swirls, a few salt freckles in lower seams, and subtle discoloration around access plates. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Normal detail should support label plaques, access plates, gasket seams, and subtle bevels. Do not emboss real text.

ORM plan:
Red AO around plaques and access plates. Green roughness 0.5 to 0.75 across ceramic, graphite, and painted locator blocks. Blue metallic on exposed fasteners and thin frame strips only.

### SHINOBU_361_HAND_010 - wall_01_4x3_c_stripes_basecolor_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_stripes_basecolor_Albedo.png`

Prompt to copy:
Wide 4x3 meter habitat wall stripe base color with strong visual composition. Create a balanced pattern of off-white pressure panels, broad graphite lower service strip, satin titanium separators, thin amber safety stripe, and a muted teal route stripe. It should guide the player through a bright underwater research corridor without feeling like a warning sign wall. Add careful scuffs, small rubbed corners, light salt in lower seams, and clean manufactured edges. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from broad panel bevels, stripe paint thickness, and separator lips. Keep material transitions crisp.

ORM plan:
Red AO in separator lips and lower strip seams. Green roughness medium on coating, high on graphite strip, slightly smoother on titanium separators. Blue metallic only on separators and fasteners.

### SHINOBU_361_HAND_011 - wall_01_4x3_c_trimsheet_normal_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_c_trimsheet_normal_Normal.png`

Prompt to copy:
Normal-map height source for a large 4x3 habitat wall trim sheet. Build a precise modular rhythm: wide pressure panel bevels, recessed horizontal service troughs, gasket channels, low-profile handhold recesses, thin frame steps, and small access cover screws. The design should feel calm, spacious, and expensive, not cluttered. Leave smooth plate fields between details so the final lighting reads premium. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Bake to BC5. Use clean bevel gradients and avoid noisy height; this is a hero corridor wall normal.

ORM plan:
Red AO in troughs, handhold recesses, and screw wells. Green roughness high on rubber channels, medium on plates, low-medium on titanium steps. Blue metallic on titanium frame steps.

### SHINOBU_361_HAND_012 - wall_01_4x3_door_02_wing_labels_basecolor_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_labels_basecolor_Albedo.png`

Prompt to copy:
Door wing label base color for a premium abyssal habitat bulkhead. Use warm off-white pressure plating, graphite hinge-side rubber, satin titanium latch plates, amber caution blocks, teal maintenance tabs, and blank technical label fields with no readable words. Make the door feel safe, sealed, and beautifully engineered. Add slight hand polish around handle zones, gentle salt whitening near lower gaskets, tiny chipped paint at latch contact points, and maintained expedition wear. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Derive normal from latch plate bevels, gasket steps, blank label plaques, hinge covers, and handle scuff areas. Keep label areas flat enough for later decals.

ORM plan:
Red AO around latches, hinge covers, and gasket edges. Green roughness high on rubber seals, medium on coated panels, lower on polished latch plates. Blue metallic on latch and hinge metal only.

### SHINOBU_361_HAND_013 - wall_01_4x3_door_02_wing_stripes_basecolor_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_stripes_basecolor_Albedo.png`

Prompt to copy:
Bulkhead door wing stripe base color for the prologue habitat. Create a confident asymmetric design: off-white sealed door panels, graphite gasket border, satin titanium hinge rail, thin amber pressure-warning stripe, and a muted teal route identifier stripe. It should read as a beautiful emergency-rated door with maintained pressure-rated hardware. Add controlled edge chips where panels meet, subtle hand wear near handle zones, pale salt dust near the bottom seal, and clean color blocking. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Normal should support door panel bevels, hinge rail, gasket border, and stripe paint thickness. Keep the door face readable from gameplay distance.

ORM plan:
Red AO under gasket borders and hinge rail. Green roughness medium on panels, high on graphite seals, slightly lower on worn handle areas. Blue metallic for hinge rail and exposed latch metal.

### SHINOBU_361_HAND_014 - wall_01_4x3_door_02_wing_trimsheet_normal_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_01_4x3_door_02_wing_trimsheet_normal_Normal.png`

Prompt to copy:
Heightfield source for a sealed habitat door wing trim sheet normal map. Design a pressure-rated surface with broad inset door plates, rounded gasket grooves, latch recesses, hinge rail bevels, small inspection caps, and shallow handle wear depressions. The geometry language should be elegant and robust, with a strong premium industrial rhythm. Avoid jagged damage and random grime; this door is maintained. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Bake to BC5 with clear medium-depth door seams and shallow gasket/latch details. Avoid hard black height cuts that would invert normals.

ORM plan:
Red AO in gasket grooves, latch recesses, and hinge rail underside. Green roughness high on seals, medium on coated plates, lower on polished latch contact zones. Blue metallic on hinge/latch areas.

### SHINOBU_361_HAND_015 - wall_04_3x6_d_trimsheet_normal_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/wall_04_3x6_d_trimsheet_normal_Normal.png`

Prompt to copy:
Tall 3x6 habitat wall trim sheet height source for a premium underwater research module. Create elegant vertical pressure panels, long satin titanium ribs, recessed life-support conduit covers, slim gasket seams, circular diagnostic ports, and shallow service hatch outlines. The surface should feel tall, calm, and architectural, with enough open breathing room between details. Add only tiny maintained wear: softened rib edges, slight salt in lower seams, and gentle access-panel scuffs. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Bake to BC5 normal. Use long clean vertical gradients for ribs, medium seams for hatches, and subtle microdetail so the wall does not shimmer.

ORM plan:
Red AO in conduit covers, diagnostic ports, and lower gasket seams. Green roughness medium-high on panels and gaskets, lower on satin titanium ribs. Blue metallic only for ribs and port rims.

## FLORA_EPIDERMIS PROMPTS - Beautiful Abyssal Biology

### SHINOBU_361_HAND_016 - MAT_family_coral_branching_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_branching_Albedo.png`

Prompt to copy:
Elegant branching abyssal coral epidermis albedo for HECTON-8 reef props. Build a clean organic surface made of slender ivory branches, translucent rose-violet growth tips, pale teal mineral freckles, and soft amber bioluminescent pinpoints tucked into branch forks. The texture should feel alien but beautiful, like a living specimen catalogued by a NASA ocean lab. Keep the diffuse readable, premium, calm, clean, and organized, with deliberate biological patterning instead of random organic clutter. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal should emphasize fine coral pores, branch ridges, soft growth rings, and shallow fork creases. Keep pore detail fine and elegant, not spiky.

ORM plan:
Red AO in branch forks and pore clusters. Green roughness 0.55 on ivory coral skeleton, 0.32 on translucent growth tips, 0.7 inside dry mineral freckles. Blue metallic 0.

### SHINOBU_361_HAND_017 - MAT_family_coral_branching_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_branching_Placeholder_Albedo.png`

Prompt to copy:
Production replacement for a branching coral placeholder, designed as a high-value modular reef surface. Use elegant off-white coral tubes, soft mauve membranes between small branches, teal micro-speckles, and restrained amber node glow marks that can become an emissive mask later. The image should look like a clean scientific close-up texture, not a noisy fantasy bark. Keep broad readable biological forms and a pleasant color balance suitable for a playable underwater scene. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Generate a gentle BC5 normal from tube ridges, node lips, membrane folds, and small pores. Avoid deep cracked damage.

ORM plan:
Red AO in tube junctions and membrane pockets. Green roughness 0.5 to 0.78, lower on living membranes and higher on calcified areas. Blue metallic 0.

### SHINOBU_361_HAND_018 - MAT_family_coral_brittle_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_brittle_Albedo.png`

Prompt to copy:
Beautiful brittle coral albedo texture for fragile abyssal reef edges. Create thin porcelain-like coral plates with hairline mineral seams, opal blue dusting, tiny violet fracture veins, and clean cream-colored calcified surfaces. The mood is delicate and expensive, like a rare sample tray under a research light. Add controlled chipped rims and small powdery break points while keeping the surface attractive, alive, clean, and collectible. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal should carry thin brittle ridges, chipped plate rims, powder pores, and shallow mineral seams. Do not create high-contrast canyon cracks.

ORM plan:
Red AO in fracture seams and under chipped ledges. Green roughness high on powdery ceramic coral, lower on opal mineral veins. Blue metallic 0.

### SHINOBU_361_HAND_019 - MAT_family_coral_low_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_low_Albedo.png`

Prompt to copy:
Low mat coral epidermis albedo for soft reef ground cover. Design rounded cream and pale peach coral pads, subtle lavender growth seams, tiny teal symbiotic speckles, and occasional amber pin-light cells arranged in gentle clusters. It should read as soft alien reef carpeting, pleasant and tactile, with enough shape contrast for terrain blending. Use a healthy reef palette, clean lobe separation, and purposeful biological variation. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Use a shallow BC5 normal for soft pad bulges, seam troughs, tiny pores, and sponge-like dimples. Keep height rounded.

ORM plan:
Red AO in pad seams and between clustered polyps. Green roughness 0.42 on moist living pads, 0.66 on matte calcified rims. Blue metallic 0.

### SHINOBU_361_HAND_020 - MAT_family_coral_low_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_low_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for low coral placeholder material, built for attractive seabed coverage. Use smooth ivory coral cushions, soft salmon-pink growth bands, faint turquoise biological freckles, and small pearl highlights painted only as material color, not directional light. The texture must tile into a gentle living floor without becoming flat paste. Keep the design clean, elegant, and scientifically plausible for a premium NASA-Punk deep sea biome. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from cushion domes, growth-band edges, pore fields, and narrow seams. Keep high frequency detail controlled to avoid shimmer.

ORM plan:
Red AO between cushion lobes. Green roughness 0.38 to 0.72 depending on moist tissue versus calcified rim. Blue metallic 0.

### SHINOBU_361_HAND_021 - MAT_family_coral_massive_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_massive_Albedo.png`

Prompt to copy:
Massive coral albedo for large reef boulders in an elegant abyssal biome. Create broad ivory coral plates with soft terracotta undertones, pearl-gray mineral webbing, muted violet growth scars, and sparse teal symbiont freckles. The material should feel ancient, heavy, and beautiful, like biological architecture grown under pressure. Add tasteful abrasion on exposed ridges and pale sediment collected in grooves, without making it filthy. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal should support broad coral mounds, slow growth rings, mineral web seams, and shallow sediment grooves.

ORM plan:
Red AO in deep growth seams and mineral web intersections. Green roughness 0.62 on calcified coral, 0.78 in sediment grooves, 0.44 on living tinted scars. Blue metallic 0.

### SHINOBU_361_HAND_022 - MAT_family_coral_massive_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_massive_Placeholder_Albedo.png`

Prompt to copy:
High-quality replacement for massive coral placeholder albedo. Make a large-scale reef skin with cream coral armor plates, pale apricot internal color at worn edges, restrained cyan mineral dust, and clean branching vein patterns that break up the surface without clutter. The result should look good close to the camera and from traversal distance: big readable masses first, fine pores second. Keep it premium, alive, clean, and organized. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Bake BC5 from broad plate relief, vein seams, worn lip bevels, and fine pore fields. Large forms must dominate.

ORM plan:
Red AO in vein seams and under plate lips. Green roughness high on chalky coral armor, medium on worn living edges. Blue metallic 0.

### SHINOBU_361_HAND_023 - MAT_family_coral_plate_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_plate_Albedo.png`

Prompt to copy:
Plate coral albedo for layered alien reef shelves. Build overlapping fan plates in warm ivory, pale coral pink, and subtle lavender shadows painted as material color only. Add translucent teal rim cells, pearl mineral dust, and tiny amber biological points along selected edges. The surface should feel graceful and decorative but still natural, like reef architecture grown in slow pressure currents. Avoid busy camouflage and random speckle noise. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal should form thin overlapping plate lips, radial growth striations, subtle edge pores, and shallow underside creases.

ORM plan:
Red AO under overlapping lips. Green roughness 0.5 on living rims, 0.72 on chalky plate centers. Blue metallic 0.

### SHINOBU_361_HAND_024 - MAT_family_coral_plate_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_coral_plate_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for plate coral placeholder, with a refined HECTON-8 reef palette. Use layered cream coral fans, salmon edge blush, faint violet mineral lines, teal symbiotic freckles, and clean crescent growth bands. The image should be beautiful in a game engine: clear tiling rhythm, readable layers, flat photographic neutrality, crisp calcified surfaces, and elegant living edges. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Create BC5 normal from fan ridges, crescent bands, overlapping rims, and small pores. Keep plate lips thin and consistent.

ORM plan:
Red AO between layered fan plates. Green roughness high on calcified centers, lower on fresher colored edges. Blue metallic 0.

### SHINOBU_361_HAND_025 - MAT_family_kelp_abyssal_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_abyssal_Albedo.png`

Prompt to copy:
Abyssal kelp epidermis albedo for premium alien seaweed surfaces. Create long graphite-green fronds with translucent teal edges, pale mineral speckling, muted amber vein nodes, and gentle violet pressure bruising. The material should feel supple, expensive, alive under deep water, and dimensional enough to read on moving fronds. Keep the frond veins elegant and directional while preserving seamless tile logic. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from long vein ridges, soft membrane wrinkles, frond edge thickness, and tiny salt/mineral pits. Avoid crunchy bark relief.

ORM plan:
Red AO along main vein troughs and folded membrane pockets. Green roughness 0.28 on wet frond skin, 0.55 on mineral-speckled zones. Blue metallic 0.

### SHINOBU_361_HAND_026 - MAT_family_kelp_canopy_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_canopy_Albedo.png`

Prompt to copy:
Canopy kelp albedo for tall readable underwater plant silhouettes. Use layered olive-teal blades, clear pale rib structures, soft cyan translucent edges, and tiny warm amber buoyancy cells embedded along the ribs. The surface should feel healthy and graceful, with a cinematic NASA research-biodome taste and curated botanical patterning. Leave enough value separation for animation and wind sway shaders. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal should be smooth and flowing: main ribs, secondary veins, soft blade wrinkles, and rounded buoyancy cells.

ORM plan:
Red AO in rib junctions and cell bases. Green roughness 0.24 on wet blades, 0.4 on translucent edges, 0.6 on pale rib tissue. Blue metallic 0.

### SHINOBU_361_HAND_027 - MAT_family_kelp_canopy_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_canopy_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for kelp canopy placeholder. Build a clean tiling sheet of elegant deep sea kelp: broad satin green fronds, teal-lit translucent rims, warm ochre vein beads, and pale mineral dust caught near naturally feathered edges. Make it attractive and art-directed, close to premium aquarium botany and expedition specimen photography. Preserve clear large shapes so the vegetation reads from a distance. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Use BC5 normal for blade curvature cues, vein relief, bead lips, and fine membrane texture. Keep tears minimal and graceful.

ORM plan:
Red AO around vein beads and overlapped blade edges. Green roughness low on wet membranes, medium on dusty/mineralized edges. Blue metallic 0.

### SHINOBU_361_HAND_028 - MAT_family_kelp_patch_dense_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_patch_dense_Albedo.png`

Prompt to copy:
Dense kelp patch albedo for seabed clusters. Make overlapping narrow fronds in deep teal, clean olive, and desaturated blue-green, with pale rib highlights as material color and scattered amber growth nodes. The texture should tile into a rich living patch without becoming unreadable camouflage. Add small sediment freckles and soft edge variation, but maintain a refined, bright-enough underwater palette. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal should separate overlapping fronds with shallow ridges, folded tips, and small growth nodes. Avoid noisy grass fuzz.

ORM plan:
Red AO where fronds overlap. Green roughness 0.25 on wet blades, 0.5 on sedimented edges. Blue metallic 0.

### SHINOBU_361_HAND_029 - MAT_family_kelp_patch_dense_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_patch_dense_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for dense kelp patch placeholder, optimized for lush but readable ground vegetation. Use layered satin teal leaves, pale turquoise rim cells, warm amber seed nodes, and restrained violet undertones in shaded membrane tissue painted without directional light. The final texture should feel abundant and premium, not messy. Create a rhythmic tile with clear blade groups and small negative spaces between clusters. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Generate BC5 normal from blade overlaps, node bumps, center ribs, and soft membrane folds. Keep amplitudes low for dense tiling.

ORM plan:
Red AO between leaf clusters and under overlaps. Green roughness low-medium on moist blades, higher on pale mineral specks. Blue metallic 0.

### SHINOBU_361_HAND_030 - MAT_family_kelp_tall_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_tall_Albedo.png`

Prompt to copy:
Tall kelp epidermis albedo for hero fronds and vertical underwater silhouettes. Create long elegant bands of blue-green plant membrane, satin olive ribbing, translucent cyan margins, and small amber pressure bladders placed with design restraint. The texture should look premium and cinematic when stretched on tall geometry, with strong vertical language, soft biological variation, and clean plant tissue readability. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from long ribs, pressure bladder lips, subtle vertical wrinkles, and rounded frond edges. Preserve clean flow.

ORM plan:
Red AO around bladder bases and rib seams. Green roughness 0.22 on wet membrane, 0.44 on ribs, 0.6 on mineral-dusted edges. Blue metallic 0.

### SHINOBU_361_HAND_031 - MAT_family_kelp_tall_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_kelp_tall_Placeholder_Albedo.png`

Prompt to copy:
High-quality replacement for tall kelp placeholder albedo. Build a clean vertical frond material with deep teal center tissue, pale green rib lines, subtle violet pressure mottling, cyan translucent edges, and sparse golden-amber growth beads. It should feel like elegant alien botany inside a high-end underwater expedition game, not a generic seaweed photo. Keep the composition tileable and usable on long meshes. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from vertical veins, bead rims, mild membrane waves, and thin edge thickness. Avoid high-frequency fuzz.

ORM plan:
Red AO in vein valleys and under bead rims. Green roughness low on wet tissue, medium on pale ribs, higher on mineral-dusted edge scars. Blue metallic 0.

### SHINOBU_361_HAND_032 - MAT_family_plant_giant_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_plant_giant_Albedo.png`

Prompt to copy:
Giant alien plant epidermis albedo for large abyssal flora props. Use broad living plates in muted teal and soft jade, pearl-white vein highways, gentle violet undertones, and small amber bioluminescent pores arranged like a controlled biological circuit. The texture should feel majestic and high budget, with large readable forms for big meshes and fine surface character only as a second layer. Keep the plant tissue elegant, resilient, clean, and non-grotesque. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal should carry broad plate swelling, thick vein ridges, pore lips, and shallow tissue wrinkles. Large forms first.

ORM plan:
Red AO in vein junctions and pore clusters. Green roughness 0.3 on living plates, 0.46 on veins, 0.62 on mineralized scars. Blue metallic 0.

### SHINOBU_361_HAND_033 - MAT_family_plant_giant_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_family_plant_giant_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for giant plant placeholder, with a premium abyssal botanical identity. Create large satin teal plant panels, pale opal vascular seams, soft coral-pink growth blush, and tiny amber cell clusters placed sparingly. It should look powerful, beautiful, and non-hostile by default, suitable for alien landmark vegetation. Keep the palette luminous but controlled, with no oversaturated neon and no grime wash. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Generate BC5 normal from vascular seams, broad tissue bulges, pore fields, and soft scar lips. Avoid lumpy random noise.

ORM plan:
Red AO in vascular seams and pore clusters. Green roughness low-medium on satin tissue, higher on scarred or mineralized seams. Blue metallic 0.

### SHINOBU_361_HAND_034 - MAT_FloraProxy_Coral_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_FloraProxy_Coral_Albedo.png`

Prompt to copy:
Proxy coral albedo for simple flora meshes that need to read as high-quality coral from a distance. Use clean ivory coral massing, peach growth zones, muted lavender pore shadows as material color, teal symbiont speckles, and a few amber glow-cell dots. Favor bold readable patches over microdetail because this proxy may sit on cheap geometry. The result must upgrade a placeholder mesh into attractive reef dressing without requiring complex shaders. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from large coral lumps, pore clusters, and gentle growth-ring seams. Keep it robust for simple meshes.

ORM plan:
Red AO in pores and between coral lumps. Green roughness high on calcified areas, lower on fresh peach growth zones. Blue metallic 0.

### SHINOBU_361_HAND_035 - MAT_FloraProxy_Kelp_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_FloraProxy_Kelp_Albedo.png`

Prompt to copy:
Proxy kelp albedo for lightweight underwater vegetation. Create attractive broad strokes of teal-green frond material, pale rib structures, cyan translucent edge bands, and tiny amber growth nodes. The texture should make simple cards or low-poly fronds feel intentional and premium. Keep the design legible at distance with a clean NASA-Punk biodiversity palette, not generic algae clutter. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal should support large frond ribs, soft folded membrane, and edge thickness; avoid fine fuzz that will alias.

ORM plan:
Red AO near rib intersections and folds. Green roughness low on wet frond surface, medium on pale ribs and mineral speckles. Blue metallic 0.

### SHINOBU_361_HAND_036 - MAT_FloraProxy_MicroGrass_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_FloraProxy_MicroGrass_Albedo.png`

Prompt to copy:
Micrograss proxy albedo for small abyssal seabed flora. Make fine but readable clusters of soft jade blades, pale turquoise tips, tiny pearl sediment grains, and restrained amber seed sparks. The texture should feel delicate and alive, like engineered biodiversity reclaiming a research site, while staying clean enough for alpha cards or simple patches. Keep clusters organized, luminous, and separated by soft natural gaps. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from small blade ridges, soft clump mounds, seed nodes, and sediment grains. Keep height shallow for dense patches.

ORM plan:
Red AO in clump bases. Green roughness medium on blades, high on sediment grains, lower on moist tips. Blue metallic 0.

### SHINOBU_361_HAND_037 - MAT_FloraProxy_Sargassum_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/MAT_FloraProxy_Sargassum_Albedo.png`

Prompt to copy:
Sargassum-style abyssal flora proxy albedo with a refined deep-sea palette. Use elegant olive ribbons, honey-amber air bladders, muted teal membrane edges, pale mineral dust, and tiny violet stress freckles. It should feel buoyant, decorative, and biologically plausible, not a brown seaweed mat. Keep the tile readable for floating strands and simple prop meshes. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from ribbon folds, bladder rims, small vein lines, and soft edge thickness. Do not over-crinkle.

ORM plan:
Red AO under bladder bases and ribbon overlaps. Green roughness low-medium on wet ribbons, higher on mineral dust and dried stress freckles. Blue metallic 0.

### SHINOBU_361_HAND_038 - Mat_Organic_PlantBud_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/Mat_Organic_PlantBud_Albedo.png`

Prompt to copy:
Organic abyssal plant bud albedo for close-up resource or flora props. Create a closed bud surface with satin jade outer scales, pearly white seam lips, soft coral-pink inner glow visible only in narrow creases, and tiny amber bioluminescent pores arranged with symmetry. The texture should feel valuable, touchable, and strange in a good way, like a rare living sample in a premium expedition lab. Keep the bud elegant, hydrated, healthy, and collectible. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from overlapping bud scales, seam lips, pore rims, and subtle tissue swelling. Keep forms rounded and elegant.

ORM plan:
Red AO under scale overlaps and bud seams. Green roughness 0.28 on satin living tissue, 0.46 on pale seam lips. Blue metallic 0.

### SHINOBU_361_HAND_039 - Mat_Organic_PlantCanopy_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/Mat_Organic_PlantCanopy_Albedo.png`

Prompt to copy:
Organic plant canopy albedo for broad alien leaf clusters. Use layered translucent teal leaves, pale jade ribs, pearl mineral dust near edges, soft violet underside mottling painted as material color, and sparse amber vein nodes. The canopy should feel lush, graceful, expensive, bright enough for underwater readability, and clean enough to support shader translucency later. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal should form leaf ribs, soft leaf overlap, edge thickness, and shallow membrane waviness. Keep large leaf silhouettes readable.

ORM plan:
Red AO at leaf overlaps and vein nodes. Green roughness low on translucent tissue, medium on ribs, high on mineral-dusted edges. Blue metallic 0.

### SHINOBU_361_HAND_040 - Mat_Organic_PlantStem_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/Mat_Organic_PlantStem_Albedo.png`

Prompt to copy:
Organic plant stem albedo for flexible abyssal flora trunks. Build a vertical biological surface with satin teal-green stem bands, pearl-white vascular ridges, faint coral-pink growth seams, tiny amber pore nodes, and subtle mineral scratches from current-carried sediment. It should feel engineered by nature under pressure: elegant, resilient, smooth, cleanly readable on cylindrical meshes, and distinct from terrestrial bark. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from long vascular ridges, seam grooves, small pore nodes, and flexible stem wrinkles. Keep the vertical rhythm continuous.

ORM plan:
Red AO in vascular grooves and pore bases. Green roughness 0.34 on satin stem tissue, 0.55 on ridges, 0.68 on mineral scratch zones. Blue metallic 0.

### SHINOBU_361_HAND_041 - Mat_Resource_Membrane_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/FLORA_EPIDERMIS/Mat_Resource_Membrane_Albedo.png`

Prompt to copy:
Premium resource membrane albedo for collectible or interactable organic material. Create a translucent opal membrane with pale cyan and soft violet internal veining, warm amber nutrient nodes, pearl-white tension lines, and clean hydrated surface variation. The texture should look valuable and desirable, like a rare biotech specimen with a clear gameplay silhouette. Keep the diffuse smooth enough for shader translucency and masks, with clear veins that can guide gameplay readability. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal should be very shallow: membrane tension wrinkles, raised vein cords, node rims, and soft hydrated dimples. Avoid deep cracks.

ORM plan:
Red AO around nutrient nodes and vein crossings. Green roughness low on hydrated membrane, slightly higher on pearl tension lines. Blue metallic 0.

## GEOLOGY_TRIPLANAR PROMPTS - Elegant Hecton Stone

### SHINOBU_361_HAND_042 - MAT_family_cave_entrance_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_cave_entrance_Albedo.png`

Prompt to copy:
Premium Hecton cave entrance albedo for a triplanar rock material. Create pressure-smoothed basalt in deep blue-gray, pale limestone abrasion bands, opal-cyan mineral seams, pearl sediment dust, and small amber sulfur freckles placed with restraint. The surface should feel like a beautiful abyssal threshold carved by ancient current and maintained by mineral flow. Use broad readable geology first, fine pores second, with clean value separation for gameplay pathfinding. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from large cave-worn stone bands, mineral seam lips, shallow erosion pockets, and fine pore fields. Keep the surface triplanar-safe with no directional lighting baked into height.

ORM plan:
Red AO in mineral seams and erosion pockets. Green roughness 0.62 on dry basalt planes, 0.46 on pressure-polished lips, 0.78 on pale sediment dust. Blue metallic 0 except rare mineral flecks near 0.08.

### SHINOBU_361_HAND_043 - MAT_family_cave_entrance_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_cave_entrance_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for a cave entrance placeholder in the HECTON-8 abyss. Make a refined stone surface with cool slate-blue basalt, cream mineral wash, soft teal fracture veins, satin-worn ridges, and delicate violet pressure staining. It should look like a handpicked hero material for entering a cave system, with confident shape hierarchy and cinematic but flat diffuse color. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Generate a BC5 normal from smooth carved ridges, shallow cave abrasions, mineral vein relief, and small sediment pockets.

ORM plan:
Red AO in fracture veins and under layered ridges. Green roughness 0.5 on polished stone, 0.72 on matte mineral wash, 0.82 in sediment pockets. Blue metallic 0.

### SHINOBU_361_HAND_044 - MAT_family_landmark_spire_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_landmark_spire_Placeholder_Albedo.png`

Prompt to copy:
Landmark spire placeholder replacement albedo for a tall Hecton mineral formation. Create elegant vertical basalt striations, pale opal veins, cool graphite stone plates, sea-glass teal mineral bands, and tiny amber crystalline inclusions. The texture should support a memorable silhouette: noble, strange, readable from distance, and beautiful up close. Keep the pattern vertical and architectural while remaining tileable. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal should reinforce vertical mineral ribs, vein lips, split stone plates, and small crystal inclusions. Maintain clean long gradients for spire meshes.

ORM plan:
Red AO inside plate splits and vein crossings. Green roughness 0.55 on basalt ribs, 0.35 on opal vein edges, 0.7 on weathered matte planes. Blue metallic 0.05 only in crystalline inclusions.

### SHINOBU_361_HAND_045 - MAT_family_rock_cluster_medium_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_rock_cluster_medium_Placeholder_Albedo.png`

Prompt to copy:
Medium rock cluster albedo for modular Hecton seabed props. Use rounded pressure-polished stones in slate, soft blue-gray, and pale mineral beige, with thin teal seam deposits and pearl sediment caught between cluster forms. The texture should give simple rock meshes a premium expedition-game finish: clear boulder separation, calm color variety, and refined abyssal geology. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from rounded boulder lobes, seam trenches, mineral crust lips, and small pitted pores. Keep the cluster forms broad.

ORM plan:
Red AO between boulder lobes and under crust lips. Green roughness 0.48 on polished tops, 0.76 in sedimented seams. Blue metallic 0.

### SHINOBU_361_HAND_046 - MAT_family_rock_shelf_large_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_rock_shelf_large_Albedo.png`

Prompt to copy:
Large rock shelf albedo for walkable or backdrop Hecton geology. Create broad horizontal stone ledges in cool graphite basalt, pale cream sediment layers, opal blue mineral ribbons, and smooth pressure-worn edges. The material should feel stable, spacious, and premium, like a natural platform shaped by deep ocean currents. Preserve large readable slabs and gentle color transitions for traversal clarity. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from broad shelf steps, long sediment striations, vein lips, and subtle erosion dimples. Favor stable large forms over noisy stone grain.

ORM plan:
Red AO below shelf lips and inside sediment layers. Green roughness 0.52 on worn ledge tops, 0.78 in mineral dust, 0.42 on polished pressure edges. Blue metallic 0.

### SHINOBU_361_HAND_047 - MAT_family_rock_small_floor_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/MAT_family_rock_small_floor_Placeholder_Albedo.png`

Prompt to copy:
Small floor rock placeholder replacement albedo for traversable seabed stone. Build a clean field of compact rounded stones, blue-gray basalt chips, pale mineral dust, teal micro-veins, and soft pearl sediment in the low gaps. The surface should look walkable, legible, and attractive, with controlled variation that tiles without visual clutter. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from small rounded stones, shallow gaps, mineral vein relief, and fine sediment dimples. Keep amplitude moderate for ground traversal.

ORM plan:
Red AO in stone gaps. Green roughness 0.58 on stone tops, 0.82 on sediment, 0.4 on polished pebble edges. Blue metallic 0.

### SHINOBU_361_HAND_048 - mat_Rock_Shared_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock_Shared_Albedo.png`

Prompt to copy:
Shared Hecton rock albedo for broad triplanar use across multiple meshes. Make a versatile premium abyssal basalt with blue-gray plates, pale opal mineral cracks, pearl sediment dusting, subtle violet pressure bands, and small teal crystalline speckles. The material must support cliffs, shelves, cave lips, and props without looking repetitive, using large calm shapes plus refined microdetail. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from layered basalt plates, mineral cracks, pressure bands, and small pore clusters. Keep all height language neutral for triplanar projection.

ORM plan:
Red AO in cracks and under plate lips. Green roughness 0.5 on polished basalt, 0.72 on mineral dust, 0.38 on opal seams. Blue metallic 0.03 in teal crystal specks only.

### SHINOBU_361_HAND_049 - mat_Rock_Shared_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock_Shared_Normal.png`

Prompt to copy:
Heightfield source for the shared Hecton rock BC5 normal map. Create layered basalt relief with wide stone plates, gentle pressure-polished ridges, shallow opal vein grooves, tiny sediment pits, and rounded mineral crust edges. The source should be clean, balanced, and physically useful for many rock meshes, with no lighting baked into the image. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Convert directly to BC5 normal. Large plate relief should dominate, vein grooves medium depth, pores shallow, and crust edges smooth.

ORM plan:
Red AO follows plate seams and vein grooves. Green roughness medium-high on basalt and high in pits. Blue metallic near zero except tiny mineral inclusions.

### SHINOBU_361_HAND_050 - mat_Rock_Shared_ORM.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock_Shared_ORM.png`

Prompt to copy:
Packed ORM source for shared Hecton rock, generated as an RGB mask image. Red channel should describe ambient occlusion in plate cracks, vein grooves, and sediment pits. Green channel should describe roughness variation: smoother pressure-polished stone, rougher mineral dust, medium basalt planes. Blue channel should be almost empty with only rare teal crystal flecks. Keep masks clean, tileable, non-directional, and usable for triplanar projection. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Use the shared normal map for geometry; this RGB output is mask data only. Match AO regions to the normal groove layout.

ORM plan:
Red equals AO, Green equals roughness, Blue equals metallic. Target values: AO 0.25-1.0, roughness 0.38-0.86, metallic 0 except rare mineral flecks at 0.08.

### SHINOBU_361_HAND_051 - mat_Rock2_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock2_Albedo.png`

Prompt to copy:
Second shared Hecton rock albedo variant for biome contrast. Use cooler graphite-blue basalt, milky quartz-like veins, muted cyan mineral halos, pale silt patches, and gentle lavender pressure mottling. The material should pair with the main rock set while giving level art a clear alternate stone family. Keep the look premium, natural, readable, and calm. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from quartz vein lips, shallow plate breaks, rounded stone ridges, and small silt pockets. Keep shape hierarchy medium-large.

ORM plan:
Red AO in vein margins and plate breaks. Green roughness 0.48 on polished graphite stone, 0.74 on silt, 0.36 on quartz-like vein surfaces. Blue metallic 0.

### SHINOBU_361_HAND_052 - mat_Rock2_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock2_Normal.png`

Prompt to copy:
Heightfield source for the second Hecton rock normal map. Create clean quartz-veined basalt relief: medium stone slabs, smooth vein ridges, shallow broken edges, rounded silt pockets, and pressure-polished high points. The image must be useful as a normal source, with clear elevation logic and uniform flat generation. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Bake to BC5 normal. Vein ridges should be smooth, slab edges medium depth, silt pockets shallow and broad.

ORM plan:
Red AO in slab separations and pocket bases. Green roughness high in silt pockets, medium on basalt, lower on vein ridges. Blue metallic 0.

### SHINOBU_361_HAND_053 - mat_Rock2_ORM.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/mat_Rock2_ORM.png`

Prompt to copy:
Packed ORM RGB mask for the second Hecton rock variant. Design clean channel behavior: red AO in quartz vein margins, plate gaps, and silt pockets; green roughness with smooth vein ridges, medium basalt slabs, and high-roughness pale silt; blue metallic held near zero for a non-metallic stone family. Keep the mask crisp, tileable, and aligned to the Rock2 height language. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Pair with `mat_Rock2_Normal.png`; do not add independent height in this mask source.

ORM plan:
Red AO 0.3-1.0, Green roughness 0.35-0.88, Blue metallic 0.0 with optional mineral flecks below 0.05.

### SHINOBU_361_HAND_054 - Mat_Tool_Flashlight_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Mat_Tool_Flashlight_Placeholder_Albedo.png`

Prompt to copy:
Placeholder replacement albedo for a HECTON-8 field flashlight tool used in mineral survey spaces. Create a premium handheld equipment material: satin graphite polymer body, warm off-white ceramic grip panels, teal lens gasket, tiny amber locator notch, brushed titanium screw collars, and clean pressure-rated seams. The texture should feel expensive, compact, and practical, like NASA dive hardware carried through an abyssal cave. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from grip texture, gasket lips, screw collar bevels, shallow seam lines, and tiny molded polymer grain.

ORM plan:
Red AO under collars, seams, and gasket lips. Green roughness 0.46 on polymer, 0.62 on ceramic grip, 0.32 on titanium collars, 0.24 on lens gasket seal. Blue metallic 1.0 for collars only.

### SHINOBU_361_HAND_055 - Mat_TriplanarRock_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Mat_TriplanarRock_Albedo.png`

Prompt to copy:
Core triplanar Hecton rock albedo for broad cliffs and terrain. Build a high-quality abyssal stone surface with layered blue basalt, pale mineral crust, opal-cyan fracture ribbons, soft sediment patches, and pressure-polished edge color. The texture must be projection-friendly: no directional scene lighting, no object shadows, no visible perspective, just clean material color and large readable geological rhythm. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from broad rock strata, fracture ribbons, mineral crust thickness, and fine pores. Keep all detail isotropic enough for triplanar blending.

ORM plan:
Red AO in fracture ribbons and under crust layers. Green roughness 0.52 on polished basalt, 0.8 on crust and sediment, 0.4 on opal mineral ribbons. Blue metallic 0.

### SHINOBU_361_HAND_056 - River_Rock_FBX_riverrock_basecolor_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/River_Rock_FBX_riverrock_basecolor_Albedo.png`

Prompt to copy:
River rock base color replacement for imported FBX river stones adapted to Hecton's abyss. Create smooth rounded stones in blue-gray, pearl beige, sea-glass teal, and pale lavender mineral tones, with soft current-polished edges and fine sediment caught between pebbles. The result should look tactile, clean, and beautiful under underwater lighting, with enough color variation to avoid flat pebble repetition. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from rounded pebble domes, shallow gaps, soft abrasion rings, and tiny mineral pits. Avoid sharp fractured relief.

ORM plan:
Red AO between pebbles. Green roughness 0.36 on polished stone tops, 0.7 in sediment gaps, 0.5 on mineral patches. Blue metallic 0.

### SHINOBU_361_HAND_057 - Rock_4_t_rock_4_basecolor_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_basecolor_Albedo.png`

Prompt to copy:
Rock 4 base color replacement for a strong modular Hecton stone set. Use angular but elegant basalt plates, cool slate-blue planes, pale cream fractured mineral edges, opal teal seams, and restrained amber crystalline dust. The material should look robust and high fidelity on cliffs, scattered rocks, and cave props, with clear medium-scale forms and refined close-up detail. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal should follow angular plate breaks, bevel-like mineral edges, seam grooves, and fine rock pores. Keep the fracture pattern tile-safe.

ORM plan:
Red AO inside plate gaps and under fractured lips. Green roughness 0.54 on slate planes, 0.76 on mineral dust, 0.42 on polished seam edges. Blue metallic 0.03 in amber crystal dust only.

### SHINOBU_361_HAND_058 - Rock_4_t_rock_4_normal_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_normal_Normal.png`

Prompt to copy:
Dedicated heightfield source for Rock 4 normal map. Create a clean angular basalt relief pattern with plate steps, rounded fracture lips, shallow opal seam grooves, pressure-polished ridges, and fine mineral pores. This is a normal-source image: strong height logic, controlled contrast, and no light direction baked into the forms. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Bake directly to BC5 tangent-space normal. Plate steps medium depth, pores shallow, seam grooves clean, ridges smooth enough for triplanar use.

ORM plan:
Red AO strongest in seam grooves and under fracture lips. Green roughness high in pore fields, medium on broad plates, lower on polished ridges. Blue metallic near zero.

### SHINOBU_361_HAND_059 - Rock_4_t_rock_4_roughness_ORM.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/Rock_4_t_rock_4_roughness_ORM.png`

Prompt to copy:
Packed ORM RGB source for Rock 4. Red channel should define AO in angular basalt gaps, seam grooves, and pore clusters. Green channel should define roughness with smooth pressure-polished ridges, medium slate plates, and high-roughness mineral dust. Blue channel should stay nearly empty with only rare crystalline flecks. Keep the mask clean, legible, tileable, and matched to the Rock 4 basecolor and normal family. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Pair with `Rock_4_t_rock_4_normal_Normal.png`; do not invent unrelated height in the mask.

ORM plan:
Red AO 0.22-1.0, Green roughness 0.38-0.88, Blue metallic 0.0 with sparse crystal flecks up to 0.08.

### SHINOBU_361_HAND_060 - SAMMPLE_1_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/SAMMPLE_1_Albedo.png`

Prompt to copy:
Replacement albedo for sample rock texture 1, upgraded into a beautiful Hecton mineral test surface. Create a balanced swatch of blue-gray basalt, pearl sediment veils, pale cyan mineral veins, soft lavender pressure stains, and small cream fracture edges. It should work as a clean production sample for material tuning: attractive, neutral, tileable, and readable under any level lighting. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from medium stone slabs, vein lips, fracture edges, and fine sediment texture. Keep the sample broad enough for shader tests.

ORM plan:
Red AO in vein and fracture zones. Green roughness 0.5-0.82 with higher sediment and lower polished mineral lips. Blue metallic 0.

### SHINOBU_361_HAND_061 - SAMMPLE_image_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/SAMMPLE_image_Albedo.png`

Prompt to copy:
Replacement albedo for sample image rock material, treated as a polished Hecton geology calibration tile. Use elegant slate-blue stone, pale opal mineral branching, cream sediment dust, restrained teal crystalline speckles, and soft graphite plate variation. The image should be pleasant enough for real environment use while remaining a reliable texture sample: clear material zones, flat diffuse color, seamless tiling, and no scene composition. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from opal branch seams, graphite plate steps, sediment dust grain, and small mineral pores.

ORM plan:
Red AO in branching seams and plate steps. Green roughness medium on stone, high on sediment, lower on opal mineral branches. Blue metallic 0.03 on crystal speckles only.

### SHINOBU_361_HAND_062 - terrain_1_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/terrain_1_Albedo.png`

Prompt to copy:
Terrain 1 albedo for Hecton seabed traversal. Create a readable natural floor of compact blue basalt grains, pale silt fans, soft teal mineral dust, pearl shell-like fragments, and gentle lavender pressure staining. The material should guide movement with calm values and polished sci-fi beauty, suitable for wide terrain without obvious repetition. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from shallow seabed ripples, compact grains, small shell-like fragments, and soft silt ridges. Keep amplitude low for terrain comfort.

ORM plan:
Red AO in silt ripple troughs and fragment bases. Green roughness high on silt, medium on basalt grains, lower on polished fragments. Blue metallic 0.

### SHINOBU_361_HAND_063 - terrain_2_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/terrain_2_Albedo.png`

Prompt to copy:
Terrain 2 albedo for alternate Hecton seabed zones. Use smoother pale mineral flats, blue-gray stone islands, opal-cyan vein traces, faint amber mineral specks, and soft sediment arcs that imply current flow through color only. The look should be open, calm, and elegant for larger exploration spaces, with strong tiling discipline and clear value hierarchy. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from soft sediment arcs, low stone islands, vein traces, and subtle mineral grain. Keep it gentle for broad terrain.

ORM plan:
Red AO in vein traces and around stone islands. Green roughness 0.8 on mineral flats, 0.55 on stone islands, 0.42 on opal vein traces. Blue metallic 0.

### SHINOBU_361_HAND_064 - terrain_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/GEOLOGY_TRIPLANAR/terrain_Albedo.png`

Prompt to copy:
Main Hecton terrain albedo for broad seabed and rock blending. Create a premium abyssal exploration floor with layered blue basalt fragments, pale pearl sediment, clean teal mineral seams, opal dust fields, and gentle coral-violet geological undertones. The material should look beautiful in motion, support gameplay readability, and blend naturally with the flora and habitat palettes. Use large calm shapes, refined microdetail, and flat diffuse color. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from broad basalt fragments, sediment fields, mineral seam lips, and fine seabed grain. Keep height mid-low for terrain stability.

ORM plan:
Red AO in seams and between fragments. Green roughness 0.62 on basalt, 0.84 on pearl sediment, 0.38 on opal dust and polished mineral seams. Blue metallic 0.

## HABITAT_INTERIORS PROMPTS - Gameplay Proxies And World Families

### SHINOBU_361_HAND_065 - Mat_BuildGhost_Invalid_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_BuildGhost_Invalid_Albedo.png`

Prompt to copy:
Invalid build ghost albedo for HECTON-8 construction placement preview. Create a premium translucent technical material with frosted off-white projection panels, soft coral-red warning edge bands, thin graphite grid seams, tiny amber diagnostic ticks, and pale cyan holographic scan noise. It should clearly communicate blocked placement while still looking like refined NASA-Punk interface hardware projected into the world. Keep shapes clean, readable, and elegant for gameplay feedback. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal should be almost flat, with only shallow grid seam relief, projection-panel bevel hints, and tiny diagnostic tick embossing.

ORM plan:
Red AO minimal, only in grid seams. Green roughness 0.18 for holographic panels, 0.42 for frosted projection bands. Blue metallic 0.

### SHINOBU_361_HAND_066 - Mat_BuildGhost_Valid_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_BuildGhost_Valid_Albedo.png`

Prompt to copy:
Valid build ghost albedo for HECTON-8 construction placement preview. Design a clean translucent construction projection with pearl-white panels, soft teal approval edges, slim graphite alignment grid, tiny amber anchor marks, and subtle cyan scan-line variation. The material should feel precise, calm, and premium, like a safe underwater habitat module ready to assemble. Make the tile readable at distance and pleasant up close. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Use a very shallow BC5 normal for alignment seams, panel edge lips, and anchor mark relief. Keep it mostly flat for shader translucency.

ORM plan:
Red AO almost none except grid intersections. Green roughness 0.16 on projection fields, 0.36 on frosted edge bands. Blue metallic 0.

### SHINOBU_361_HAND_067 - MAT_DiegeticTooltipGlyph_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_DiegeticTooltipGlyph_Albedo.png`

Prompt to copy:
Diegetic tooltip glyph albedo sheet for HECTON-8 in-world UI surfaces. Create a clean material of tiny unlabeled interface glyph blocks, pearl-white etched panels, teal micro-lines, amber selection pips, and graphite backing strips. Do not render readable letters or words; use abstract technical marks that feel like a polished underwater research UI. The texture should support crisp masking, signage, and small holographic decals without becoming noisy. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal should carry shallow etched glyph relief, tiny panel bevels, and smooth backing-strip grooves.

ORM plan:
Red AO in etched grooves and panel seams. Green roughness 0.34 on etched ceramic, 0.52 on graphite backing, 0.22 on holographic color marks. Blue metallic 0.

### SHINOBU_361_HAND_068 - MAT_DiegeticTooltipIcon_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_DiegeticTooltipIcon_Albedo.png`

Prompt to copy:
Diegetic tooltip icon albedo sheet for HECTON-8 interaction prompts. Build a premium abstract icon material with simple rounded technical symbols, pearl ceramic icon plates, teal active accents, amber confirmation dots, and satin graphite underlayers. Keep all symbols non-textual, minimal, and readable as in-world interface language. The result should feel like a high-end dive computer UI embedded in habitat surfaces. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from icon plate bevels, shallow symbol embossing, and thin graphite channel grooves.

ORM plan:
Red AO under icon plates and inside channel grooves. Green roughness 0.3 on ceramic icons, 0.55 on graphite, 0.2 on active color accents. Blue metallic 0.

### SHINOBU_361_HAND_069 - Mat_DroneProxy_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_DroneProxy_Albedo.png`

Prompt to copy:
Drone proxy albedo for a HECTON-8 utility drone placeholder. Create a compact premium equipment skin with warm off-white ceramic armor panels, satin titanium service rails, graphite rubber sensor gaskets, teal navigation accents, amber locator windows, and subtle salt-polished edges. It should read as friendly research hardware, clean and useful, with small panel details that survive low-poly proxy geometry. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from armor plate bevels, sensor gasket lips, screw wells, and fine molded rubber grain.

ORM plan:
Red AO under armor seams and sensor gaskets. Green roughness 0.48 on ceramic panels, 0.34 on titanium rails, 0.7 on rubber gaskets. Blue metallic 1.0 for rails and screw rims only.

### SHINOBU_361_HAND_070 - MAT_ErrorCube_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_ErrorCube_Albedo.png`

Prompt to copy:
Diagnostic error cube albedo for missing or debug geometry in HECTON-8. Make a polished technical warning material with pearl-white diagnostic tiles, coral-red fault bands, amber status pips, graphite borders, and teal calibration marks. It should be unmistakably a debug/error surface while still fitting the project art direction, like a professional engineering overlay rather than a raw placeholder. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from tile bevels, fault-band paint thickness, tiny status pip embossing, and graphite border grooves.

ORM plan:
Red AO in tile seams and border grooves. Green roughness 0.5 on ceramic tiles, 0.58 on red bands, 0.42 on teal calibration paint. Blue metallic 0.

### SHINOBU_361_HAND_071 - MAT_family_creature_spawn_passive_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_spawn_passive_Albedo.png`

Prompt to copy:
Passive creature spawn family albedo for non-threatening Hecton fauna zones. Create a calm biological ground marker made of pearly shell dust, soft jade membrane traces, pale coral-pink nutrient flecks, small teal symbiotic dots, and gentle amber warmth in tiny rounded nodes. It should look inviting and alive, signaling safe biodiversity rather than danger. Keep forms broad enough for gameplay readability and beautiful enough for close inspection. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from rounded nutrient nodes, soft membrane seams, shell-dust grains, and shallow biological dimples.

ORM plan:
Red AO around nodes and membrane seams. Green roughness 0.38 on hydrated membrane, 0.72 on shell dust, 0.48 on nutrient flecks. Blue metallic 0.

### SHINOBU_361_HAND_072 - MAT_family_creature_spawn_passive_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_spawn_passive_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for passive creature spawn placeholder. Build a soft reef-biome tile with creamy sediment, tiny opal shell chips, translucent jade biological trails, pale peach micro-polyp marks, and subtle amber seed cells. The texture should quietly tell the player this area supports harmless life. Use pleasant color separation, clean organic rhythm, and no scenic lighting. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal should be shallow: shell chips, soft trails, micro-polyp lips, and light sediment ripples.

ORM plan:
Red AO in shell-chip bases and trail edges. Green roughness high on sediment, low-medium on hydrated trails. Blue metallic 0.

### SHINOBU_361_HAND_073 - MAT_family_creature_spawn_predator_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_spawn_predator_Albedo.png`

Prompt to copy:
Predator creature spawn family albedo for Hecton danger zones, expressed with elegant warning biology. Use deep teal armored scale traces, pearl abrasion edges, controlled coral-red pressure markings, amber sensory-node dots, and graphite-blue mineral silt. The surface should feel alert and predatory while preserving beauty and readability. Make the danger signal clear through pattern rhythm, disciplined color contrast, and clean territorial markings. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from scale ridges, sensory node lips, shallow claw-like scrape channels, and silt grain.

ORM plan:
Red AO beneath scale overlaps and scrape channels. Green roughness 0.42 on polished scale traces, 0.74 in silt, 0.55 on red pressure markings. Blue metallic 0.

### SHINOBU_361_HAND_074 - MAT_family_creature_spawn_predator_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_spawn_predator_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for predator spawn placeholder. Create a sleek abyssal threat marker with dark teal biological plates, pearl scraped rims, thin coral warning lines, amber eye-like sensor dots, and cool mineral sediment between forms. It should be tense but refined, readable as a predator ecology zone while remaining premium and art-directed. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from plate overlaps, warning-line paint ridges, sensor-dot rims, and shallow scrape marks.

ORM plan:
Red AO in plate seams and scrape marks. Green roughness 0.38 on plate surfaces, 0.68 on sediment, 0.52 on warning lines. Blue metallic 0.

### SHINOBU_361_HAND_075 - MAT_family_creature_zone_abyss_apex_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_abyss_apex_Albedo.png`

Prompt to copy:
Abyss apex creature zone albedo for the deepest high-threat biome. Create a majestic pressure-born surface with blue-black teal scale plates, opal white scar rims, cyan mineral veins, restrained amber sensory pores, and violet pressure gradients painted as material color. The texture should feel rare, powerful, and beautiful, like the territory of a top abyssal animal. Keep forms large, clean, and readable for zone identity. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from large scale plate lips, vein grooves, sensory pore rims, and pressure-worn ridge arcs.

ORM plan:
Red AO under plate lips and vein grooves. Green roughness 0.34 on polished scale plates, 0.66 on scar rims, 0.5 on mineral veins. Blue metallic 0.

### SHINOBU_361_HAND_076 - MAT_family_creature_zone_abyss_apex_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_abyss_apex_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for abyss apex zone placeholder. Build a refined high-pressure ecology marker with deep petrol teal biological armor, pale opal ridge dust, thin cyan vein geometry, amber pore constellations, and soft violet undertones. It should read as apex territory at a glance while staying elegant enough for an AAA underwater environment. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal should use broad armor ridges, shallow vein cuts, pore rims, and smooth pressure bands.

ORM plan:
Red AO in armor overlaps and vein cuts. Green roughness medium-low on armor, high on opal dust, medium on pore zones. Blue metallic 0.

### SHINOBU_361_HAND_077 - MAT_family_creature_zone_large_threat_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_large_threat_Albedo.png`

Prompt to copy:
Large threat creature zone albedo for Hecton open-water danger areas. Use strong readable biological patterning: graphite-teal plate fragments, pearl scrape trails, coral-red boundary strokes, amber sensory beads, and pale mineral silt caught in recesses. The result should feel like a territorial warning surface designed by nature, sharp and beautiful without visual clutter. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from plate fragments, boundary-stroke ridges, sensory bead rims, and shallow silt recesses.

ORM plan:
Red AO in recesses and under plate fragments. Green roughness 0.4 on plates, 0.6 on colored strokes, 0.78 on silt. Blue metallic 0.

### SHINOBU_361_HAND_078 - MAT_family_creature_zone_large_threat_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_large_threat_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for large threat zone placeholder. Create a clean danger-biome tile with deep teal biological plating, coral linear warning motifs, opal chipped edges, amber dot clusters, and blue-gray seabed silt. It should be clear and game-readable, with premium material taste and restrained contrast that works in underwater lighting. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from broad plating, colored line ridges, chipped edge lips, and dot cluster bumps.

ORM plan:
Red AO around plates and dot clusters. Green roughness 0.44 on living plates, 0.7 on silt, 0.52 on coral motifs. Blue metallic 0.

### SHINOBU_361_HAND_079 - MAT_family_creature_zone_reef_apex_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_reef_apex_Albedo.png`

Prompt to copy:
Reef apex creature zone albedo for a powerful predator that belongs to coral ecology. Combine ivory coral fragments, teal scale traces, pearl shell dust, warm amber sensory pores, and restrained rose-violet biological markings. The texture should feel dangerous but beautiful, integrated with reef life rather than separate from it. Keep large forms readable and color-coded for zone recognition. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from coral fragments, scale ridges, shell-dust pockets, and sensory pore lips.

ORM plan:
Red AO in fragment gaps and scale seams. Green roughness high on coral/shell, medium on scale traces, lower on hydrated pore areas. Blue metallic 0.

### SHINOBU_361_HAND_080 - MAT_family_creature_zone_reef_apex_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_reef_apex_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for reef apex zone placeholder. Build a premium reef-threat marker with porcelain coral plates, teal biological armor strips, amber pore constellations, pale sediment dust, and soft coral-pink edge blush. The material should guide the player with elegant warning biology while staying compatible with the flora palette. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from coral plate edges, armor-strip seams, pore rims, and sediment pockets.

ORM plan:
Red AO beneath armor strips and coral plate overlaps. Green roughness high on coral, medium on armor, low-medium around hydrated pores. Blue metallic 0.

### SHINOBU_361_HAND_081 - MAT_family_creature_zone_ruin_apex_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_ruin_apex_Albedo.png`

Prompt to copy:
Ruin apex creature zone albedo for top predators around ancient structures. Blend satin graphite ruin fragments, teal biological scale traces, pearl mineral dust, amber sensor-like pores, and faded off-white ceramic chips. The texture should feel like apex territory crossing old engineered surfaces, elegant and readable rather than chaotic. Use a premium ruin-meets-biology palette that connects world lore and gameplay danger. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from ruin chip bevels, biological scale seams, pore rims, and dust-filled mechanical grooves.

ORM plan:
Red AO in mechanical grooves and scale seams. Green roughness 0.42 on satin ruin fragments, 0.7 on dust, 0.38 on polished biological scales. Blue metallic 0.4 only on exposed engineered fragments.

### SHINOBU_361_HAND_082 - MAT_family_creature_zone_ruin_apex_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_creature_zone_ruin_apex_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for ruin apex zone placeholder. Create a clean tile of ancient ceramic fragments, graphite machine ribs, teal scale residue, pale mineral dust, and warm amber pore marks. The material should clearly say high-level creature territory inside a ruined engineered site, with attractive shape hierarchy and restrained color. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from ceramic fragment lips, machine rib grooves, scale residue ridges, and pore bumps.

ORM plan:
Red AO in rib grooves and fragment gaps. Green roughness high on dust and ceramic chips, medium on scale residue, low-medium on exposed machine ribs. Blue metallic on machine ribs only.

### SHINOBU_361_HAND_083 - MAT_family_debris_field_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_debris_field_Albedo.png`

Prompt to copy:
Debris field family albedo for HECTON-8 exploration spaces. Create a premium scatter material with off-white ceramic habitat chips, satin titanium slivers, graphite gasket scraps, pearl sediment, teal paint flecks, and tiny amber emergency-plastic fragments. The surface should tell a story of engineered material carried by currents while remaining clean, readable, and beautiful in aggregate. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from chip bevels, gasket scrap thickness, sediment grains, and metal sliver edges. Keep debris readable without sharp spikes.

ORM plan:
Red AO under chips and scraps. Green roughness high on sediment and ceramic, medium on gasket rubber, low-medium on titanium slivers. Blue metallic on titanium fragments only.

### SHINOBU_361_HAND_084 - MAT_family_debris_field_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_debris_field_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for debris field placeholder. Build a clean current-sorted scatter of pearl sediment, off-white pressure-panel fragments, teal paint chips, graphite seal strips, and brushed metal flecks. It should work as a readable world-runtime material, giving debris zones a premium HECTON-8 identity without cluttering gameplay visibility. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from fragment bevels, seal-strip lips, sediment dimples, and small metal fleck edges.

ORM plan:
Red AO beneath fragments. Green roughness 0.78 on sediment, 0.58 on ceramic, 0.68 on rubber, 0.34 on metal flecks. Blue metallic on metal flecks only.

### SHINOBU_361_HAND_085 - MAT_family_debris_scatter_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_debris_scatter_Albedo.png`

Prompt to copy:
Debris scatter albedo for small repeated HECTON-8 fragments. Create sparse readable chips of ceramic white, satin titanium, graphite gasket rubber, teal paint, amber safety plastic, and pale mineral dust. The material should tile lightly, suitable for small prop scatter or ground overlay, with enough negative space and clean material separation. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from individual chip bevels, tiny screw rims, rubber strip edges, and shallow sediment grain.

ORM plan:
Red AO under each chip. Green roughness by material: ceramic 0.58, rubber 0.76, dust 0.84, metal 0.32. Blue metallic on metal chips and screw rims.

### SHINOBU_361_HAND_086 - MAT_family_debris_scatter_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_debris_scatter_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for debris scatter placeholder. Make a sparse high-quality texture of tiny expedition-material fragments: pearl ceramic flakes, teal coating shards, graphite rubber crumbs, brushed titanium specks, and pale sediment. It should feel intentionally art-directed and tile without forming obvious repeated clusters. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from flake thickness, shard edges, speck rims, and soft sediment dimples. Keep relief small.

ORM plan:
Red AO under flakes and specks. Green roughness high for sediment and rubber, medium for ceramic, low-medium for titanium. Blue metallic only on titanium specks.

### SHINOBU_361_HAND_087 - MAT_family_egg_cluster_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_egg_cluster_Albedo.png`

Prompt to copy:
Egg cluster family albedo for alien Hecton fauna, designed as valuable biology instead of shock imagery. Create pearly translucent egg capsules, jade membrane cords, soft coral-pink internal glow, tiny amber nutrient dots, and opal shell dust between rounded forms. The texture should feel strange, collectible, and beautiful, with clear clusters and premium material taste. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from rounded capsule domes, membrane cords, nutrient-dot rims, and soft shell dust.

ORM plan:
Red AO between egg capsules and under cords. Green roughness 0.18 on translucent capsules, 0.42 on membranes, 0.76 on shell dust. Blue metallic 0.

### SHINOBU_361_HAND_088 - MAT_family_egg_cluster_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_egg_cluster_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for egg cluster placeholder. Build a refined biological cluster with opal-white capsules, pale teal membrane webbing, coral-pink internal color, amber nutrient beads, and pearl sediment around the base. The material should be readable as alien reproduction ecology while staying elegant and usable in a broad adventure scene. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from capsule curvature, membrane webbing, bead bumps, and shallow base sediment.

ORM plan:
Red AO in capsule gaps and under membrane webbing. Green roughness low on capsule skins, medium on membranes, high on sediment. Blue metallic 0.

### SHINOBU_361_HAND_089 - MAT_family_landmark_spire_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_landmark_spire_Albedo.png`

Prompt to copy:
Landmark spire family albedo for world proxy materials in HECTON-8. Create a refined vertical mineral-and-structure skin with satin graphite ribs, off-white ceramic relic plates, opal teal mineral seams, pale sediment dust, and tiny amber survey marker flecks. It should identify a memorable navigation landmark, bridging engineered history and natural abyssal mineral growth. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from vertical ribs, ceramic plate edges, mineral seam grooves, and dust-filled recesses.

ORM plan:
Red AO between ribs and under plate lips. Green roughness 0.44 on graphite ribs, 0.62 on ceramic, 0.78 on dust, 0.38 on mineral seams. Blue metallic 0.35 on engineered ribs only.

### SHINOBU_361_HAND_090 - MAT_family_pocket_hazard_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_hazard_Albedo.png`

Prompt to copy:
Hazard pocket family albedo for dangerous micro-areas in the HECTON-8 world. Use clean coral-red mineral warning bands, amber sulfur crystals, graphite basalt chips, pale salt crust, and teal pressure-fluid stains arranged as a readable environmental caution pattern. It should signal risk through color and material identity while still looking like a beautiful natural-technical pocket. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from sulfur crystal rims, mineral band edges, salt crust plates, and basalt chip gaps.

ORM plan:
Red AO in crystal bases and chip gaps. Green roughness 0.8 on salt crust, 0.52 on basalt, 0.35 on sulfur crystals, 0.58 on red mineral bands. Blue metallic 0.

### SHINOBU_361_HAND_091 - MAT_family_pocket_hazard_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_hazard_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for hazard pocket placeholder. Create a polished danger-zone material with amber crystal clusters, coral-red mineral veining, pale salt films, graphite stone chips, and cyan fluid residue. The tile should be game-readable at a glance and attractive close up, with a premium expedition geology palette. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from crystal clusters, mineral veins, salt film edges, and stone-chip relief.

ORM plan:
Red AO under crystals and chips. Green roughness high on salt films, medium on stone, lower on crystal facets. Blue metallic 0.

### SHINOBU_361_HAND_092 - MAT_family_pocket_resource_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_resource_Albedo.png`

Prompt to copy:
Resource pocket family albedo for valuable Hecton collection zones. Create an attractive deposit texture with pearl sediment, opal cyan mineral seams, warm amber resin beads, silver-white silica flecks, muted copper inclusions, and clean teal survey dust. The material should feel rewarding and legible, like a rich pocket a player wants to inspect. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from bead domes, mineral seam lips, silica fleck edges, and shallow sediment pockets.

ORM plan:
Red AO in seam pockets and under resource beads. Green roughness high on sediment, medium on resin, low-medium on mineral flecks. Blue metallic 0.4 for copper/silver inclusions only.

### SHINOBU_361_HAND_093 - MAT_family_pocket_resource_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_resource_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for resource pocket placeholder. Build a clean collectible-zone texture with pale mineral dust, opal blue seams, tiny copper glints, pearl silica chips, amber resin droplets, and soft teal scan residue. It should communicate value through beautiful material contrast and organized clusters. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from resin droplet rims, seam grooves, chip bevels, and fine mineral dust.

ORM plan:
Red AO around droplets and chip bases. Green roughness high on mineral dust, medium on resin, low on polished silica and metal inclusions. Blue metallic only for copper/silver glints.

### SHINOBU_361_HAND_094 - MAT_family_pocket_safe_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_safe_Albedo.png`

Prompt to copy:
Safe pocket family albedo for calm shelter or low-risk exploration zones. Create a bright underwater refuge material with warm off-white sediment, soft teal mineral rings, pearly shell dust, gentle jade membrane traces, and tiny amber comfort markers. It should feel safe, breathable, and beautiful, supporting player orientation without looking sterile. Use broad clean shapes and a relaxed premium palette. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from soft mineral rings, shell dust, shallow membrane traces, and mild sediment ripples.

ORM plan:
Red AO in ring grooves and shell chip bases. Green roughness high on sediment, medium on membranes, low-medium on polished shell flecks. Blue metallic 0.

### SHINOBU_361_HAND_095 - MAT_family_pocket_safe_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_pocket_safe_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for safe pocket placeholder. Make a clean refuge-zone tile with pearl sediment, pale teal circular mineral patterns, soft jade biological traces, cream shell fragments, and tiny warm amber guide dots. The material should read as inviting and stable, with enough texture richness to avoid flatness and enough calm space for gameplay clarity. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from circular mineral grooves, shell fragment bevels, soft biological trace ridges, and fine sediment grain.

ORM plan:
Red AO in mineral grooves and under shell fragments. Green roughness 0.82 on sediment, 0.5 on biological traces, 0.42 on shell fragments. Blue metallic 0.

## HABITAT_INTERIORS PROMPTS - Route, Ruin, Sky, Module, Resource

### SHINOBU_361_HAND_096 - MAT_family_route_power_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_route_power_Albedo.png`

Prompt to copy:
Route power family albedo for HECTON-8 traversal guidance and energized infrastructure. Create a clean material with warm off-white cable housings, satin titanium conduit ribs, teal energy-routing paint, amber capacitor dots, graphite insulation bands, and pale salt dust caught along seam edges. The texture should clearly imply powered route infrastructure without becoming a glowing billboard. Keep the design premium, readable, and modular for repeated world use. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from conduit ribs, cable housing bevels, insulation band lips, and small capacitor-dot rims.

ORM plan:
Red AO under conduit ribs and between insulation bands. Green roughness 0.48 on ceramic housings, 0.34 on titanium ribs, 0.72 on graphite insulation, 0.42 on painted route marks. Blue metallic on titanium ribs only.

### SHINOBU_361_HAND_097 - MAT_family_route_power_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_route_power_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for route power placeholder. Build a tidy power-routing tile with pearl ceramic cable panels, teal linework, amber node markers, graphite gasket strips, and brushed metal pinrails. It should guide the player through infrastructure spaces with a refined engineering look, suitable for modular construction surfaces and runtime markers. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from cable panel seams, node marker embossing, gasket strip grooves, and pinrail bevels.

ORM plan:
Red AO in seams and under pinrails. Green roughness medium on ceramic, high on graphite strips, low-medium on brushed metal. Blue metallic on pinrails and exposed pins only.

### SHINOBU_361_HAND_098 - MAT_family_ruin_cluster_medium_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_cluster_medium_Albedo.png`

Prompt to copy:
Medium ruin cluster family albedo for HECTON-8 ancient engineered fragments. Use off-white ceramic relic plates, satin graphite internal ribs, opal teal mineral growth in seams, pearl sediment dust, and sparse amber survey flecks. The material should feel like valuable ruins discovered under pressure: elegant, readable, and integrated with ocean mineralization. Keep broken shapes organized and premium rather than noisy. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from ceramic plate breaks, rib grooves, mineral seam deposits, and sediment-filled cracks.

ORM plan:
Red AO under plate breaks and rib grooves. Green roughness 0.62 on ceramic, 0.42 on graphite ribs, 0.78 on sediment, 0.36 on mineral seams. Blue metallic 0.35 on internal ribs.

### SHINOBU_361_HAND_099 - MAT_family_ruin_cluster_medium_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_cluster_medium_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for medium ruin cluster placeholder. Create a clean cluster texture of pale ceramic ruin tiles, graphite machine-edge fragments, teal mineral seams, pearl dust, and amber marker flecks. The result should sell old technical architecture without losing readability on small meshes. Use clear large fragments first and fine mineral detail second. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from tile fragment lips, machine-edge grooves, mineral seam ridges, and dust pockets.

ORM plan:
Red AO between fragments. Green roughness high on dust and ceramic, medium on graphite, lower on mineral ridges. Blue metallic on graphite machine fragments only.

### SHINOBU_361_HAND_100 - MAT_family_ruin_megastructure_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_megastructure_Albedo.png`

Prompt to copy:
Ruin megastructure family albedo for monumental Hecton architecture. Create large off-white pressure-ceramic slabs, broad satin graphite structural bands, opal-cyan mineral rivers, pale sediment veils, and small amber survey registration marks. The texture should feel massive and noble, useful on giant ruin meshes and still beautiful up close. Use spacious shapes, clean value hierarchy, and restrained abyssal color. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from broad slab steps, structural band lips, mineral river grooves, and sediment veil texture.

ORM plan:
Red AO below slab steps and band lips. Green roughness 0.58 on ceramic slabs, 0.44 on graphite bands, 0.8 on sediment veils, 0.36 on mineral rivers. Blue metallic 0.25 on graphite structural bands.

### SHINOBU_361_HAND_101 - MAT_family_ruin_megastructure_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_megastructure_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for ruin megastructure placeholder. Build a grand engineered ruin material with wide ceramic plate fields, graphite ribs, teal mineral seams, pearl silt deposits, and tiny amber locator remnants. It should read as ancient scale, high craft, and underwater preservation, with large calm forms suitable for huge surfaces. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from wide plate bevels, rib grooves, mineral seams, and shallow silt layers. Large forms dominate.

ORM plan:
Red AO in rib grooves and plate seams. Green roughness medium-high on ceramic, medium on graphite, high on silt. Blue metallic on exposed rib material only.

### SHINOBU_361_HAND_102 - MAT_family_ruin_module_single_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_module_single_Albedo.png`

Prompt to copy:
Single ruin module family albedo for smaller ancient structure pieces. Create a modular tile of pale ceramic panels, graphite underframe strips, teal mineral edge deposits, pearl dust, and small amber alignment marks. It should look like one recovered piece of high-end ocean technology, usable on compact props and modular fragments. Keep the design clean, specific, and readable. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from panel bevels, underframe strip lips, mineral deposits, and dust-filled screw wells.

ORM plan:
Red AO around underframe strips and screw wells. Green roughness 0.6 on ceramic, 0.45 on graphite, 0.78 on dust, 0.38 on mineral edge deposits. Blue metallic on underframe strips.

### SHINOBU_361_HAND_103 - MAT_family_ruin_module_single_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_ruin_module_single_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for single ruin module placeholder. Make a compact engineered relic texture with off-white ceramic access plates, graphite gasket channels, teal mineral growth along seams, pearl sediment dust, and amber calibration flecks. The material should upgrade a simple proxy into a readable preserved technology fragment. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from access plate lips, gasket channels, mineral seam ridges, and small calibration flecks.

ORM plan:
Red AO in gasket channels and plate seams. Green roughness medium on ceramic, high on dust, low-medium on graphite. Blue metallic only on exposed mechanical flecks.

### SHINOBU_361_HAND_104 - MAT_family_service_scar_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_service_scar_Albedo.png`

Prompt to copy:
Service scar family albedo for repaired HECTON-8 surfaces and old maintenance marks. Create warm off-white pressure coating, satin titanium patch plates, teal inspection paint, amber sealant dots, graphite gasket scars, and pale salt dust gathered along repair edges. The surface should feel maintained and useful, showing history without looking neglected. Make the repaired areas attractive, precise, and readable. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from patch plate bevels, gasket scars, sealant dot rims, shallow scratches, and paint thickness.

ORM plan:
Red AO under patch plates and gasket scars. Green roughness 0.5 on coating, 0.34 on titanium patches, 0.7 on gasket scars, 0.58 on sealant. Blue metallic on titanium patches only.

### SHINOBU_361_HAND_105 - MAT_family_service_scar_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_family_service_scar_Placeholder_Albedo.png`

Prompt to copy:
Replacement albedo for service scar placeholder. Build a clean repair-history texture with ceramic coating, precise patch panels, teal inspection strokes, amber sealant beads, graphite gasket remnants, and controlled salt wear. It should read as professional maintenance inside a premium underwater base, with good PBR material separation. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from repair patch edges, inspection-paint ridges, sealant bead domes, and shallow scuff lines.

ORM plan:
Red AO around patch edges and gasket remnants. Green roughness medium on coating, low on metal patches, high on rubber remnants and salt wear. Blue metallic on exposed patch metal only.

### SHINOBU_361_HAND_106 - Mat_GasGiant_Emissive.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_GasGiant_Emissive.png`

Prompt to copy:
Gas giant emissive mask source for HECTON-8 sky presentation. Create a soft cinematic planet-light pattern with warm amber storm bands, pale cyan aurora arcs, pearl cloud glow islands, and restrained violet atmospheric veins. This should be an emissive mask source, not a scenic painting: clean shapes, flat values, no perspective planet sphere, no object scene. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
No normal generation required for emissive mask. If needed, only derive ultra-shallow cloud-band flow.

ORM plan:
For emissive use: white/value areas emit, black/value areas stay off. If packed into ORM elsewhere, keep metallic 0 and roughness high outside glow bands.

### SHINOBU_361_HAND_107 - Mat_GasGiant_ORM.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_GasGiant_ORM.png`

Prompt to copy:
Packed ORM source for gas giant sky material. Red channel should describe soft occlusion in cloud-band troughs and storm eddies. Green channel should hold high roughness for diffuse atmospheric cloud and lower roughness only in smooth luminous aurora lanes. Blue channel should stay black because the gas giant is non-metallic. Keep the RGB mask abstract, clean, tileable if needed, and aligned to broad elegant atmospheric bands. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
No dedicated normal required; cloud depth belongs in shader or emissive/cloud masks.

ORM plan:
Red AO 0.4-1.0 in cloud troughs, Green roughness 0.55-0.95, Blue metallic 0.

### SHINOBU_361_HAND_108 - Mat_HeavyHunterProxy_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HeavyHunterProxy_Albedo.png`

Prompt to copy:
Heavy hunter proxy albedo for a large Hecton predator placeholder. Create a premium biological armor material with deep petrol-teal plates, pearl worn ridges, controlled coral-red pressure lines, amber sensory pores, and opal mineral dust along contact edges. It should read as powerful and intelligent while staying beautiful and game-readable on proxy geometry. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from large armor plate lips, pore rims, pressure line ridges, and mineral-dust edge texture.

ORM plan:
Red AO under plate lips and around pores. Green roughness 0.34 on polished plates, 0.62 on worn ridges, 0.78 on mineral dust. Blue metallic 0.

### SHINOBU_361_HAND_109 - Mat_HectonSky_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HectonSky_Albedo.png`

Prompt to copy:
Hecton sky albedo atlas source for an ocean-world atmosphere. Create refined bands of deep cyan atmosphere, pale pearl cloud veils, soft teal horizon glow, muted violet upper haze, and warm amber distant light traces. The material should support a hopeful alien sky above a deep sea world, cinematic but clean, with broad gradients and no scenic objects. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
No normal required for sky albedo. Optional ultra-soft cloud normal only if shader uses it, derived from cloud veil luminance.

ORM plan:
For sky, keep metallic 0, roughness high and uniform if packed, AO minimal except subtle cloud density troughs.

### SHINOBU_361_HAND_110 - Mat_HectonSky_CloudOverlay_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HectonSky_CloudOverlay_Albedo.png`

Prompt to copy:
Hecton sky cloud overlay albedo for atmospheric layering. Create soft pearl and pale cyan cloud ribbons, teal vapor wisps, opal translucent edges, and faint lavender pressure-haze bands. The texture should be beautiful as an overlay mask: airy, clean, broad, and usable with scrolling shaders. Keep contrast controlled so clouds layer without blocking the sky. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Optional very shallow cloud normal from soft ribbon edges and vapor wisps only.

ORM plan:
For cloud overlay masks, use green roughness high and blue metallic 0; red can represent density/occlusion only if shader expects it.

### SHINOBU_361_HAND_111 - Mat_HunterProxy_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_HunterProxy_Albedo.png`

Prompt to copy:
Hunter proxy albedo for agile Hecton predator placeholders. Create sleek teal biological plates, pearl edge abrasion, coral-red motion streak markings, amber sensory dot rows, and subtle blue-gray mineral dust. The material should feel fast, readable, and refined on simple proxy shapes, with strong directionality and premium biology detail. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from sleek plate seams, sensory dots, motion-streak paint ridges, and soft edge abrasion.

ORM plan:
Red AO in plate seams and dot bases. Green roughness 0.32 on sleek plates, 0.58 on markings, 0.78 on mineral dust. Blue metallic 0.

### SHINOBU_361_HAND_112 - Mat_LeakWetSheen_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_LeakWetSheen_Albedo.png`

Prompt to copy:
Leak wet sheen albedo for controlled water presence on HECTON-8 habitat surfaces. Create transparent-looking teal moisture trails, pearl salt rims, soft cyan puddle edges, faint amber utility-fluid tint in tiny beads, and clean off-white surface hints beneath. The texture should look like beautiful maintained wetness from pressure seals, useful for decals or material overlays, with no scenic reflection baked in. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal should be ultra-shallow: bead rims, puddle edge meniscus, salt rim grain, and soft wet streak thickness.

ORM plan:
Red AO nearly zero except under salt rims. Green roughness low on wet trails, higher on salt rims. Blue metallic 0.

### SHINOBU_361_HAND_113 - Mat_LeviathanProxy_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_LeviathanProxy_Albedo.png`

Prompt to copy:
Leviathan proxy albedo for a monumental Hecton creature placeholder. Create enormous elegant biological armor language: deep teal whale-like plates, pearl scar ridges, opal cyan vein channels, amber sensory constellations, and restrained violet pressure gradients. The texture should make a simple proxy feel majestic, ancient, and beautiful, with huge readable forms and minimal clutter. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from broad plate transitions, large scar ridges, vein channel grooves, and sensory pore rims. Large scale dominates.

ORM plan:
Red AO under plate transitions and vein channels. Green roughness 0.3 on smooth plates, 0.62 on ridges, 0.5 on vein channels. Blue metallic 0.

### SHINOBU_361_HAND_114 - MAT_LightningBolt_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_LightningBolt_Albedo.png`

Prompt to copy:
Lightning bolt albedo source for stylized energy decals in HECTON-8. Create clean branching energy ribbons in pale cyan, white-hot pearl, teal corona edges, and tiny amber capacitor sparks on a neutral flat base. The shape language should be sharp and elegant, suitable for masks, VFX cards, and diagnostic arcs. Keep it graphic, crisp, and non-scenic. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
No normal required unless used as a raised decal; optional shallow BC5 edge ridge from ribbon thickness.

ORM plan:
For decal use, keep metallic 0, roughness low on energy ribbons, high on neutral base. Emissive mask should follow pearl/cyan energy shapes.

### SHINOBU_361_HAND_115 - Mat_Module_Corridor_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_Corridor_Albedo.png`

Prompt to copy:
Habitat corridor module albedo for HECTON-8 construction pieces. Create warm off-white pressure panels, satin titanium ribs, graphite gasket channels, teal route paint, amber safety pips, and pale salt dust in lower seams. The material should look safe, expensive, modular, and instantly traversable, with clean bands that guide the eye through a corridor. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from panel seams, rib bevels, gasket grooves, safety pip embossing, and subtle anti-slip grain.

ORM plan:
Red AO in gasket channels and under ribs. Green roughness 0.5 on panels, 0.34 on titanium ribs, 0.72 on graphite gaskets, 0.58 on painted route marks. Blue metallic on titanium ribs only.

### SHINOBU_361_HAND_116 - Mat_Module_CurrentTurbine_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_CurrentTurbine_Albedo.png`

Prompt to copy:
Current turbine module albedo for underwater power infrastructure. Create satin titanium blade housings, off-white ceramic nacelle panels, graphite rubber seals, teal flow-direction paint, amber service dots, and pale mineral streaks shaped by water movement. The texture should feel hydrodynamic, precise, and premium, with forms that support rotating machinery without needing extra geometry detail. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from blade-housing lips, nacelle panel bevels, seal grooves, service dot rims, and subtle flow streaks.

ORM plan:
Red AO in seal grooves and under housings. Green roughness 0.38 on titanium, 0.54 on ceramic panels, 0.76 on rubber seals, 0.68 on mineral streaks. Blue metallic on titanium housings.

### SHINOBU_361_HAND_117 - Mat_Module_Foundation_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_Foundation_Albedo.png`

Prompt to copy:
Foundation module albedo for heavy HECTON-8 habitat support parts. Build a robust material with off-white pressure concrete composite, satin titanium anchor plates, graphite vibration pads, teal survey lines, amber locking indicators, and pearl sediment gathered around footing seams. It should feel heavy, stable, and high-budget, suitable for base construction and underwater load-bearing pieces. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from anchor plate bevels, vibration pad lips, composite pores, survey-line paint thickness, and sediment ridges.

ORM plan:
Red AO under anchor plates and pad lips. Green roughness 0.7 on composite, 0.34 on titanium, 0.78 on rubber pads, 0.84 on sediment. Blue metallic on anchor plates only.

### SHINOBU_361_HAND_118 - Mat_Module_Pylon_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_Pylon_Albedo.png`

Prompt to copy:
Pylon module albedo for vertical HECTON-8 support structures. Create long satin titanium ribs, warm off-white ceramic clamp panels, graphite cable collars, teal alignment stripes, amber inspection dots, and subtle salt-worn vertical edges. The texture should stretch well on tall supports and read as engineered load-bearing hardware in an abyssal base. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from long rib bevels, clamp panel lips, cable collar grooves, inspection-dot rims, and fine vertical edge wear.

ORM plan:
Red AO under collars and clamp lips. Green roughness 0.36 on titanium ribs, 0.52 on ceramic clamps, 0.74 on graphite collars, 0.68 on salt-worn edges. Blue metallic on titanium ribs.

### SHINOBU_361_HAND_119 - Mat_Module_ServicePump_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Module_ServicePump_Albedo.png`

Prompt to copy:
Service pump module albedo for HECTON-8 life-support machinery. Create off-white pump casing panels, satin titanium pipe collars, graphite flexible hose sections, teal service-flow arrows, amber pressure-status dots, and pale mineral residue near connector seams. The material should feel practical, precise, and maintained, with clear component separation for repeated machinery props. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from casing seams, pipe collar bevels, hose ribbing, service-flow paint edges, and connector residue.

ORM plan:
Red AO in hose ribs and connector seams. Green roughness 0.5 on casing, 0.33 on titanium collars, 0.78 on hose rubber, 0.72 on mineral residue. Blue metallic on collars and pipe fittings.

### SHINOBU_361_HAND_120 - Mat_Ocean_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Ocean_Albedo.png`

Prompt to copy:
Ocean albedo source for HECTON-8 water material. Create a clean abstract water color texture with deep teal gradients, pale cyan caustic veils, pearl suspended particulate, subtle violet depth undertones, and soft amber bioluminescent dust traces. The source should support shader motion and transparency, so keep it broad, elegant, seamless, and free of baked wave shadows or scenery. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Optional normal should be very shallow: soft water ripple direction, suspended particulate texture, and caustic veil flow. Main water motion belongs in shader.

ORM plan:
Metallic 0. Roughness low-medium if used for water sheen; red AO minimal. Keep masks broad and shader-friendly.

### SHINOBU_361_HAND_121 - Mat_Organic_EggNest_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Organic_EggNest_Albedo.png`

Prompt to copy:
Organic egg nest albedo for alien Hecton fauna sites. Create a refined nest surface with pearl membrane cords, soft jade biological matting, opal-white shell dust, coral-pink nutrient veins, and tiny amber warmth in rounded cells. It should feel like protected alien ecology, visually rich and collectible, with clear rounded forms and premium color discipline. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from membrane cord braids, rounded cell rims, shell dust, and shallow nutrient vein relief.

ORM plan:
Red AO between cords and cells. Green roughness 0.34 on hydrated membrane, 0.74 on shell dust, 0.46 on nutrient veins. Blue metallic 0.

### SHINOBU_361_HAND_122 - Mat_Organic_EggShell_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Organic_EggShell_Albedo.png`

Prompt to copy:
Organic eggshell albedo for Hecton fauna materials. Create pearly translucent shell plates, pale cyan mineral speckles, soft coral inner blush along thin seams, opal dust, and tiny amber nutrient pinpoints. The texture should be delicate, premium, and readable, like an alien shell sample under expedition lab analysis. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from shell plate curvature, seam lips, mineral speckles, and fine shell pores.

ORM plan:
Red AO in shell seams and pore clusters. Green roughness 0.24 on translucent shell, 0.62 on opal dust, 0.42 on inner blush seams. Blue metallic 0.

### SHINOBU_361_HAND_123 - MAT_PlayerSwimBlockout_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_PlayerSwimBlockout_Albedo.png`

Prompt to copy:
Player swim blockout albedo for readable prototype volumes that still fit HECTON-8 presentation. Create a clean semi-technical material with translucent cyan panels, pearl-white calibration blocks, teal flow lines, amber boundary dots, and graphite edge marks. It should clearly signal swim-space debugging or traversal volume while looking intentional and polished in capture. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Very shallow BC5 normal from calibration block seams, flow-line paint, boundary dot rims, and edge mark grooves.

ORM plan:
Red AO minimal. Green roughness 0.22 on translucent panels, 0.5 on calibration blocks, 0.42 on graphite marks. Blue metallic 0.

### SHINOBU_361_HAND_124 - MAT_ProceduralBio_Shallows_ORM.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_ProceduralBio_Shallows_ORM.png`

Prompt to copy:
Packed ORM RGB source for procedural shallows biology. Red channel should mark AO in coral pad seams, kelp bases, shell-dust pockets, and small membrane folds. Green channel should vary roughness across wet living tissue, chalky shell dust, polished mineral flecks, and soft sediment. Blue channel should stay near zero with only rare mineral inclusions. Keep masks clean, organic, readable, seamless, and aligned to bright shallow-biome material families. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Pair with procedural bio albedo/normal family; this RGB output is mask data only.

ORM plan:
Red AO 0.25-1.0 in biological creases, Green roughness 0.22-0.86, Blue metallic 0 with optional mineral flecks below 0.05.

### SHINOBU_361_HAND_125 - Mat_Resource_Copper_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Copper_Albedo.png`

Prompt to copy:
Copper resource albedo for collectible Hecton mineral chunks. Create warm copper inclusions embedded in blue-gray basalt, pale pearl sediment, teal oxide mineral halos, and small amber-orange metallic facets. The resource should look valuable and easy to identify, with attractive contrast against the cooler Hecton environment. Keep clusters organized, game-readable, and suitable for small pickup meshes or deposit surfaces. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from copper facet edges, basalt pockets, oxide halo ridges, and sediment grain.

ORM plan:
Red AO around embedded copper and basalt pockets. Green roughness 0.32 on copper facets, 0.68 on oxide halos, 0.78 on sediment. Blue metallic 1.0 on copper facets only.

## HABITAT_INTERIORS PROMPTS - Resources, Sargassum, Sky, Support

### SHINOBU_361_HAND_126 - Mat_Resource_Fiber_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Fiber_Albedo.png`

Prompt to copy:
Fiber resource albedo for collectible Hecton biological material. Create bundled satin jade fibers, pearl-white strand highlights, pale teal membrane threads, tiny amber nutrient nodes, and soft coral-pink growth seams. The resource should look useful, flexible, clean, and valuable, readable on pickup meshes and resource deposits. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from bundled strand ridges, membrane thread overlaps, nutrient node rims, and soft fiber fray.

ORM plan:
Red AO between fiber bundles. Green roughness 0.42 on satin fibers, 0.58 on dry strand edges, 0.3 on hydrated membrane threads. Blue metallic 0.

### SHINOBU_361_HAND_127 - Mat_Resource_Resin_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Resin_Albedo.png`

Prompt to copy:
Resin resource albedo for Hecton collectible organic deposits. Create translucent amber resin beads, opal cyan internal streaks, pearl mineral dust, soft jade membrane anchors, and warm honey-colored thickness variation. The material should feel desirable and tactile, like a high-value biotech crafting component from an abyssal plant. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from rounded resin bead domes, membrane anchors, dust grains, and soft meniscus rims.

ORM plan:
Red AO around bead bases and membrane anchors. Green roughness 0.18 on resin, 0.62 on mineral dust, 0.42 on membrane anchors. Blue metallic 0.

### SHINOBU_361_HAND_128 - Mat_Resource_Scrap_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Scrap_Albedo.png`

Prompt to copy:
Scrap resource albedo for recoverable HECTON-8 engineering fragments. Create small satin titanium shards, warm off-white ceramic chips, graphite gasket pieces, teal coating flakes, amber plastic locator bits, and pearl sediment dust. It should look useful for crafting and clearly artificial, with premium material separation and clean silhouette fragments. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from shard bevels, chip thickness, gasket strip edges, and sediment grain.

ORM plan:
Red AO under fragments. Green roughness 0.34 on titanium, 0.58 on ceramic, 0.74 on rubber, 0.82 on sediment. Blue metallic on titanium shards and screw flecks.

### SHINOBU_361_HAND_129 - Mat_Resource_Silica_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Silica_Albedo.png`

Prompt to copy:
Silica resource albedo for Hecton mineral deposits. Create pearl-white silica chips, pale cyan glassy edges, opal dust, soft blue-gray basalt pockets, and tiny lavender mineral inclusions. The resource should look crisp, bright, and easy to identify, with clean crystalline fragments arranged for pickup and deposit readability. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from crystal chip bevels, glassy edge lips, dust pockets, and shallow basalt recesses.

ORM plan:
Red AO under silica chips. Green roughness 0.28 on glassy edges, 0.72 on dust, 0.56 on basalt. Blue metallic 0.

### SHINOBU_361_HAND_130 - Mat_Resource_Silver_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Silver_Albedo.png`

Prompt to copy:
Silver resource albedo for collectible metallic mineral veins. Create cool silver-white metal flecks embedded in blue-gray basalt, pale opal mineral halos, pearl sediment, and soft teal oxide traces. The texture should make silver instantly valuable without turning into mirror noise: clean clusters, restrained sparkle, and readable cool metal identity. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from silver fleck edges, basalt pockets, oxide halo ridges, and sediment grains.

ORM plan:
Red AO around embedded flecks. Green roughness 0.24 on silver, 0.64 on oxide halos, 0.78 on sediment. Blue metallic 1.0 on silver flecks only.

### SHINOBU_361_HAND_131 - Mat_Resource_Sulfur_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Resource_Sulfur_Albedo.png`

Prompt to copy:
Sulfur resource albedo for Hecton vent-adjacent deposits. Create warm amber-yellow sulfur crystals, pale cream mineral crust, teal fluid residue, blue-gray basalt pockets, and small pearl sediment grains. The material should be vivid, valuable, and readable while staying refined and natural under underwater lighting. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from sulfur crystal facets, mineral crust edges, residue streak lips, and basalt pockets.

ORM plan:
Red AO at crystal bases and crust seams. Green roughness 0.38 on crystal facets, 0.76 on mineral crust, 0.58 on residue. Blue metallic 0.

### SHINOBU_361_HAND_132 - MAT_sargassum_leaf_scraps_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_sargassum_leaf_scraps_Albedo.png`

Prompt to copy:
Sargassum leaf scraps albedo for floating and collected plant fragments. Create elegant olive-teal leaf pieces, cyan translucent edges, honey-amber buoyancy beads, pearl salt dust, and pale violet stress freckles. The scraps should feel light, clean, and beautiful, useful for overlays, particles, or debris fields without losing botanical identity. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from leaf-edge thickness, bead rims, soft vein lines, and salt grains.

ORM plan:
Red AO under overlapped scraps and bead bases. Green roughness 0.28 on wet leaves, 0.48 on beads, 0.72 on salt dust. Blue metallic 0.

### SHINOBU_361_HAND_133 - MAT_SargassumFoamDamping_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_SargassumFoamDamping_Albedo.png`

Prompt to copy:
Sargassum foam damping albedo for surface ecology and motion-damping visuals. Create pale pearl foam cells, soft teal water films, olive sargassum micro-fragments, amber nutrient dots, and clean cyan meniscus edges. The texture should look airy, premium, and useful for shader layering, with broad foam clusters and gentle biological color. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Very shallow BC5 normal from foam cell rims, water-film meniscus edges, and tiny leaf fragment thickness.

ORM plan:
Red AO minimal inside foam cell borders. Green roughness 0.2 on wet film, 0.68 on foam, 0.42 on plant fragments. Blue metallic 0.

### SHINOBU_361_HAND_134 - MAT_SargassumMicroFaunaBoids_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_SargassumMicroFaunaBoids_Albedo.png`

Prompt to copy:
Sargassum micro-fauna boids albedo for tiny animated life particles. Create miniature opal shell specks, translucent teal larval shapes, amber bioluminescent pinpoints, pale jade membrane flecks, and pearl particulate dust. The texture should be charming and readable as small living details, with clear tiny silhouettes for particle use. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Optional very shallow BC5 normal from tiny shell rims, larval body bumps, and particulate dots.

ORM plan:
Red AO around tiny bodies and shells. Green roughness low on translucent larvae, high on shell dust. Blue metallic 0.

### SHINOBU_361_HAND_135 - MAT_SargassumOilFilm_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_SargassumOilFilm_Albedo.png`

Prompt to copy:
Sargassum oil film albedo for subtle biological surface sheen. Create translucent teal-green oil ribbons, pearlescent cyan edges, soft amber nutrient shimmer, pale violet interference bands, and tiny olive plant flecks. The texture should feel like refined organic film useful for shader overlays, not a heavy pollution layer. Keep it thin, elegant, and tileable. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Ultra-shallow BC5 normal from film edges, meniscus curves, and tiny plant fleck thickness.

ORM plan:
Red AO nearly zero. Green roughness 0.12-0.34 across oil ribbons, higher on plant flecks. Blue metallic 0.

### SHINOBU_361_HAND_136 - MAT_SargassumWaveDamping_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/MAT_SargassumWaveDamping_Albedo.png`

Prompt to copy:
Sargassum wave damping albedo for broad surface-plant influence. Create calm olive-teal ribbon fields, pearl foam dust, pale cyan water gaps, amber buoyancy bead trails, and soft jade biological strands. The material should suggest plant mass calming water movement while staying abstract enough for shader motion. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Shallow BC5 normal from ribbon overlaps, bead trails, foam dust rims, and soft strand ridges.

ORM plan:
Red AO in ribbon overlaps. Green roughness low on wet ribbons, high on foam dust, medium on strands. Blue metallic 0.

### SHINOBU_361_HAND_137 - Mat_Shelf_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Shelf_Albedo.png`

Prompt to copy:
Interior shelf albedo for HECTON-8 habitat storage surfaces. Create warm off-white ceramic shelf panels, satin titanium edge rails, graphite anti-slip strips, teal inventory tick marks, amber small locator pips, and controlled salt dust in rear seams. The surface should feel practical, compact, and expensive, fitting research-base storage rather than generic furniture. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from shelf panel bevels, edge rail lips, anti-slip strip grain, and tiny locator pip embossing.

ORM plan:
Red AO under rails and rear seams. Green roughness 0.5 on ceramic, 0.34 on titanium, 0.74 on graphite strips. Blue metallic on edge rails.

### SHINOBU_361_HAND_138 - Mat_Skybox_Day_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Skybox_Day_Albedo.png`

Prompt to copy:
Day skybox albedo source for Hecton. Create broad hopeful atmospheric bands with pale cyan sky, pearl cloud veils, soft teal horizon bloom, muted violet upper air, and subtle amber sunlight haze. The source should be clean and usable for skybox blending, with no objects, no horizon scene, and no perspective composition. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
No normal required. Optional cloud normal should be ultra-soft and derived from veil density.

ORM plan:
Skybox material uses metallic 0, roughness high/uniform, AO minimal.

### SHINOBU_361_HAND_139 - Mat_Skybox_Night_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Skybox_Night_Albedo.png`

Prompt to copy:
Night skybox albedo source for Hecton with elegant deep ocean atmosphere. Create refined indigo-teal gradients, pearl star-dust fields, soft cyan aurora ribbons, muted violet high haze, and tiny warm amber distant light specks. The texture should feel calm, expensive, and readable, like an alien research sky above the sea. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
No normal required for skybox. Keep any optional cloud-height data very soft.

ORM plan:
Metallic 0, roughness high/uniform, AO minimal. Emissive mask can be derived from stars and aurora ribbons.

### SHINOBU_361_HAND_140 - Mat_Skybox_Storm_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Skybox_Storm_Albedo.png`

Prompt to copy:
Storm skybox albedo source for Hecton, dramatic but still premium and readable. Create layered teal-gray cloud bands, pearl rain veils, cyan electrical glow traces, muted violet pressure haze, and restrained amber breaks of distant light. The texture should support weather mood while staying clean, luminous, and controlled, with broad soft forms for skybox projection. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
No normal required; optional cloud normal should be broad and low-amplitude.

ORM plan:
Metallic 0. Roughness high and uniform if packed. Emissive mask can follow cyan electrical traces only.

### SHINOBU_361_HAND_141 - Mat_SmallPassiveProxy_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_SmallPassiveProxy_Albedo.png`

Prompt to copy:
Small passive creature proxy albedo for friendly Hecton fauna placeholders. Create soft jade membrane patches, pearl shell-like ridges, tiny amber sensory dots, pale coral blush, and subtle teal freckling. The material should make simple proxy creatures feel gentle, readable, and alive, with inviting color language and clean biological surfaces. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from soft shell ridges, membrane folds, sensory dot rims, and tiny pore fields.

ORM plan:
Red AO in shell ridge bases and membrane folds. Green roughness 0.35 on membrane, 0.64 on shell ridges, 0.42 on sensory dots. Blue metallic 0.

### SHINOBU_361_HAND_142 - Mat_Sun_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Sun_Albedo.png`

Prompt to copy:
Sun albedo/emissive source for Hecton sky presentation. Create a clean abstract solar texture with warm pearl center bands, soft amber plasma ribbons, pale cyan atmospheric scattering edge, and restrained gold-white glow islands. It should be beautiful and controlled for sky rendering, with no perspective sphere and no scenic composition. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
No normal required. Use this as albedo/emissive source only.

ORM plan:
Metallic 0, roughness high if packed. Emissive intensity should follow pearl and amber plasma bands.

### SHINOBU_361_HAND_143 - Mat_Support_AbyssApex_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_AbyssApex_Albedo.png`

Prompt to copy:
Support marker albedo for abyss apex gameplay classification. Create a refined icon-free material with deep teal pressure bands, opal mineral ribs, amber sensory dots, pearl edge dust, and violet depth undertones. It should support data-driven zone visualization while looking like a natural premium material, not a flat debug color. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from pressure band ridges, mineral rib lips, dot rims, and soft dust pockets.

ORM plan:
Red AO around ribs and dots. Green roughness medium on bands, high on dust, low-medium on mineral ribs. Blue metallic 0.

### SHINOBU_361_HAND_144 - Mat_Support_CreaturePassive_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_CreaturePassive_Albedo.png`

Prompt to copy:
Support marker albedo for passive creature classification. Create soft pearl sediment, jade membrane marks, tiny amber comfort dots, pale coral speckles, and teal biological trails arranged in a calm readable pattern. It should make passive creature support zones attractive and clear without using text or symbols. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from membrane trail ridges, dot rims, sediment grains, and small biological pores.

ORM plan:
Red AO around dots and trails. Green roughness high on sediment, medium on trails, low-medium on hydrated marks. Blue metallic 0.

### SHINOBU_361_HAND_145 - Mat_Support_CreaturePredator_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_CreaturePredator_Albedo.png`

Prompt to copy:
Support marker albedo for predator creature classification. Create disciplined danger biology with deep teal plate traces, coral-red boundary strokes, pearl scraped ridges, amber sensory dots, and graphite mineral dust. The texture should signal predator logic through clean rhythm and contrast while staying premium and readable. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from plate trace lips, boundary stroke ridges, sensory dot rims, and dust grain.

ORM plan:
Red AO under plate traces and dot bases. Green roughness 0.38 on biological plates, 0.58 on strokes, 0.78 on dust. Blue metallic 0.

### SHINOBU_361_HAND_146 - Mat_Support_HazardPocket_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_HazardPocket_Albedo.png`

Prompt to copy:
Support marker albedo for hazard pocket classification. Create coral-red mineral bands, amber crystal nodes, pale salt plates, teal fluid residue, and blue-gray stone chips arranged as a clean warning material. It should read immediately as environmental caution while matching the premium Hecton resource palette. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from mineral band edges, crystal node rims, salt plate lips, and stone chip gaps.

ORM plan:
Red AO under crystals and chips. Green roughness high on salt, medium on bands, low-medium on crystal facets. Blue metallic 0.

### SHINOBU_361_HAND_147 - Mat_Support_ReefApex_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_ReefApex_Albedo.png`

Prompt to copy:
Support marker albedo for reef apex classification. Create porcelain coral plate marks, teal scale traces, amber pore clusters, pearl shell dust, and soft coral-pink edge blush. The material should identify reef apex ecology with beauty and clarity, matching both coral flora and creature-zone palettes. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from coral plate lips, scale trace ridges, pore rims, and shell-dust pockets.

ORM plan:
Red AO in plate overlaps and pore bases. Green roughness high on coral/shell, medium on scales, low-medium on hydrated pores. Blue metallic 0.

### SHINOBU_361_HAND_148 - Mat_Support_ResourcePocket_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_ResourcePocket_Albedo.png`

Prompt to copy:
Support marker albedo for resource pocket classification. Create opal blue mineral seams, pearl sediment, tiny copper and silver flecks, amber resin beads, and teal survey dust arranged in an organized collectible pattern. The material should say "valuable pocket" through PBR material identity rather than text. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from resin beads, mineral seam lips, metal fleck bevels, and sediment grain.

ORM plan:
Red AO around beads and flecks. Green roughness high on sediment, medium on resin, low on metal flecks. Blue metallic on copper/silver flecks only.

### SHINOBU_361_HAND_149 - Mat_Support_RuinApex_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_RuinApex_Albedo.png`

Prompt to copy:
Support marker albedo for ruin apex classification. Blend off-white ceramic relic chips, graphite engineered ribs, teal mineral seams, amber pore markers, and pearl sediment dust into a clear high-tier ruin ecology material. It should communicate apex zone support data while still feeling like real Hecton surface art. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from ceramic chip lips, rib grooves, mineral seam ridges, and pore marker rims.

ORM plan:
Red AO in rib grooves and chip gaps. Green roughness medium-high on ceramic and sediment, lower on graphite ribs. Blue metallic on graphite ribs only.

### SHINOBU_361_HAND_150 - Mat_Support_SafePocket_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Support_SafePocket_Albedo.png`

Prompt to copy:
Support marker albedo for safe pocket classification. Create a calm refuge material with warm pearl sediment, pale teal mineral rings, soft jade membrane trails, cream shell dust, and tiny amber guide dots. It should be inviting, stable, and readable as safety support data while staying integrated with the HECTON-8 natural palette. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from mineral ring grooves, membrane trail ridges, shell dust, and guide dot rims.

ORM plan:
Red AO in ring grooves and dot bases. Green roughness high on sediment and shell dust, medium on membrane trails. Blue metallic 0.

## HABITAT_INTERIORS PROMPTS - Tools, Trials, Residual Source Fixes

### SHINOBU_361_HAND_151 - Mat_TerritorialProxy_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_TerritorialProxy_Albedo.png`

Prompt to copy:
Territorial proxy albedo for Hecton creature and biome ownership markers. Create clean biological boundary material with teal scale traces, pearl mineral lines, amber sensory dots, coral edge strokes, and blue-gray silt. It should feel like an elegant natural territory signal, readable on simple proxy shapes without needing text or icons. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from boundary line ridges, scale trace lips, sensory dot rims, and silt grain.

ORM plan:
Red AO around boundary grooves and dot bases. Green roughness medium on biological traces, high on silt, low-medium on pearl mineral lines. Blue metallic 0.

### SHINOBU_361_HAND_152 - Mat_Tool_BeaconDeployer_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_BeaconDeployer_Placeholder_Albedo.png`

Prompt to copy:
Beacon deployer tool albedo for premium HECTON-8 field equipment. Create warm off-white ceramic casing panels, satin titanium deployment rails, teal signal paint, amber locator windows, graphite rubber grip pads, and pale salt wear on handled edges. It should read as compact navigation hardware for underwater expeditions, clean and desirable as a usable tool. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from casing seams, rail bevels, grip pad grain, locator window rims, and small screw wells.

ORM plan:
Red AO in rails and grip seams. Green roughness 0.52 on ceramic, 0.32 on titanium, 0.72 on rubber pads. Blue metallic on titanium rails and screws.

### SHINOBU_361_HAND_153 - Mat_Tool_Builder_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Builder_Placeholder_Albedo.png`

Prompt to copy:
Builder tool albedo for HECTON-8 habitat construction equipment. Create off-white pressure ceramic shell plates, satin titanium assembly jaws, teal construction guide stripes, amber status nodes, graphite handle gaskets, and fine mineral dust at seam edges. The material should feel precise, expensive, and capable of building underwater modules. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from shell plate bevels, assembly jaw grooves, handle gasket texture, and status node rims.

ORM plan:
Red AO around jaws, seams, and gaskets. Green roughness 0.5 on ceramic, 0.3 on titanium jaws, 0.74 on graphite grips. Blue metallic on jaws and screw rims.

### SHINOBU_361_HAND_154 - Mat_Tool_EnvAnalyzer_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_EnvAnalyzer_Placeholder_Albedo.png`

Prompt to copy:
Environment analyzer tool albedo for HECTON-8 survey gameplay. Create a clean scientific instrument material with pearl ceramic sensor plates, teal analysis bands, amber sample status dots, satin titanium probe collars, graphite rubber seals, and pale cyan lens insets. It should feel like precise NASA-Punk oceanographic equipment, readable as a scanner/analyzer without text. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from sensor plate bevels, probe collar rims, rubber seals, lens inset lips, and small status dots.

ORM plan:
Red AO in sensor seams and seal grooves. Green roughness 0.48 on ceramic, 0.28 on lenses, 0.34 on titanium, 0.72 on rubber. Blue metallic on probe collars.

### SHINOBU_361_HAND_155 - Mat_Tool_HarpoonLauncher_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_HarpoonLauncher_Placeholder_Albedo.png`

Prompt to copy:
Harpoon launcher tool albedo for HECTON-8 defensive field hardware. Create satin titanium barrel sleeves, warm off-white pressure casing, graphite recoil grip pads, teal alignment stripes, amber safety indicators, and clean salt-polished edge wear. The texture should feel robust and premium, clearly a tool built for underwater pressure rather than a generic weapon surface. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from barrel sleeve grooves, casing panel seams, grip pad grain, safety indicator rims, and screw wells.

ORM plan:
Red AO in barrel grooves and grip seams. Green roughness 0.32 on titanium, 0.52 on casing, 0.76 on grips. Blue metallic on barrel sleeves and screws.

### SHINOBU_361_HAND_156 - Mat_Tool_Knife_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Knife_Placeholder_Albedo.png`

Prompt to copy:
Knife tool albedo for HECTON-8 utility survival equipment. Create brushed titanium blade facets, off-white ceramic spine inserts, graphite grip texture, teal alignment notch, amber safety pin, and fine salt-polished contact wear. It should read as a compact premium tool for cutting fiber, membrane, and salvage, with clean material separation and no excessive damage. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from blade bevels, spine insert lips, grip grain, alignment notch edge, and safety pin rim.

ORM plan:
Red AO in grip grooves and insert seams. Green roughness 0.28 on blade facets, 0.54 on ceramic, 0.76 on graphite grip. Blue metallic on blade and safety pin.

### SHINOBU_361_HAND_157 - Mat_Tool_LaserCutter_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_LaserCutter_Placeholder_Albedo.png`

Prompt to copy:
Laser cutter tool albedo for HECTON-8 salvage work. Create pearl ceramic heat-shield panels, satin titanium focusing rings, teal calibration bands, amber emitter-status dots, graphite insulated grip sections, and pale cyan lens accents. The texture should feel precise, scientific, and powerful, with clean high-tech shape language suitable for close first-person inspection. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from focusing ring bevels, heat-shield seams, grip insulation grain, calibration band thickness, and lens inset rims.

ORM plan:
Red AO in focusing rings and grip grooves. Green roughness 0.5 on ceramic, 0.3 on titanium rings, 0.74 on insulation, 0.22 on lens accents. Blue metallic on rings and emitter parts.

### SHINOBU_361_HAND_158 - Mat_Tool_Propulsion_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Propulsion_Placeholder_Albedo.png`

Prompt to copy:
Propulsion tool albedo for underwater movement equipment. Create satin titanium thruster collars, off-white ceramic cowling panels, graphite intake gaskets, teal flow arrows, amber power nodes, and subtle mineral streaks along water-flow edges. The texture should feel hydrodynamic, compact, and premium, supporting a tool that belongs in a deep-sea research kit. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from thruster collar grooves, cowling panel seams, intake gasket lips, flow-arrow paint thickness, and mineral streaks.

ORM plan:
Red AO in intake grooves and collar seams. Green roughness 0.34 on titanium, 0.52 on ceramic, 0.76 on gaskets, 0.66 on mineral streaks. Blue metallic on thruster collars.

### SHINOBU_361_HAND_159 - Mat_Tool_Repair_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Repair_Placeholder_Albedo.png`

Prompt to copy:
Repair tool albedo for HECTON-8 maintenance gameplay. Create off-white ceramic casing, satin titanium applicator collars, teal repair-flow marks, amber sealant status dots, graphite rubber grip panels, and tiny pale sealant residue near nozzle seams. The tool should feel reliable, precise, and used by professionals in a pressure-rated habitat. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from casing seams, applicator collar bevels, grip panel grain, sealant dot rims, and nozzle residue.

ORM plan:
Red AO around collars and grip seams. Green roughness 0.5 on casing, 0.32 on titanium collars, 0.72 on grips, 0.58 on sealant residue. Blue metallic on collars.

### SHINOBU_361_HAND_160 - Mat_Tool_SalvageSampler_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_SalvageSampler_Placeholder_Albedo.png`

Prompt to copy:
Salvage sampler tool albedo for collecting materials in HECTON-8. Create satin titanium sample jaws, pearl ceramic cartridge panels, teal collection labels as abstract blocks, amber vial indicators, graphite grip seals, and fine mineral dust on contact edges. It should read as a precise field sampler for resources and biological fragments, premium and compact. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from sample jaw bevels, cartridge panel seams, vial indicator rims, grip seal grooves, and contact dust.

ORM plan:
Red AO inside jaw grooves and cartridge seams. Green roughness 0.3 on titanium jaws, 0.52 on ceramic, 0.72 on graphite, 0.8 on dust. Blue metallic on jaws and pins.

### SHINOBU_361_HAND_161 - Mat_Tool_Scanner_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_Scanner_Placeholder_Albedo.png`

Prompt to copy:
Scanner tool albedo for HECTON-8 survey and detection gameplay. Create pearl ceramic shell panels, pale cyan sensor glass, teal scan-band paint, amber target-lock dots, satin titanium lens rings, and graphite rubber grip ribs. It should look like desirable underwater scientific hardware, readable as a scanner even without text or UI. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from shell panel seams, lens ring bevels, grip ribs, target-dot rims, and sensor glass inset lips.

ORM plan:
Red AO in lens rings and grip ribs. Green roughness 0.48 on ceramic, 0.18 on sensor glass, 0.32 on titanium rings, 0.74 on rubber. Blue metallic on rings only.

### SHINOBU_361_HAND_162 - Mat_Tool_StunPistol_Placeholder_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_Tool_StunPistol_Placeholder_Albedo.png`

Prompt to copy:
Stun pistol tool albedo for non-lethal HECTON-8 defense equipment. Create satin titanium emitter rails, off-white ceramic body plates, graphite rubber grip pads, teal charge-channel marks, amber safety windows, and pale cyan capacitor lens insets. The texture should feel controlled, compact, and scientific, closer to research safety hardware than aggression. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from emitter rail grooves, body plate seams, grip pad grain, capacitor lens rims, and safety window lips.

ORM plan:
Red AO in rail grooves and grip seams. Green roughness 0.32 on titanium, 0.52 on ceramic, 0.74 on rubber, 0.2 on lens insets. Blue metallic on emitter rails.

### SHINOBU_361_HAND_163 - Mat_ToolTrial_Anchor_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Anchor_Albedo.png`

Prompt to copy:
Tool trial anchor albedo for prototype anchoring equipment. Create heavy satin titanium anchor ribs, off-white ceramic clamp plates, graphite friction pads, teal alignment bands, amber locking nodes, and pearl sediment on contact edges. It should read as a stable test material for anchoring mechanics with production-grade visual quality. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from anchor rib bevels, clamp plate seams, friction pad grain, and locking node rims.

ORM plan:
Red AO under ribs and pads. Green roughness 0.32 on titanium, 0.52 on ceramic, 0.78 on friction pads. Blue metallic on anchor ribs.

### SHINOBU_361_HAND_164 - Mat_ToolTrial_Cargo_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Cargo_Albedo.png`

Prompt to copy:
Tool trial cargo albedo for prototype carrying and container interactions. Create off-white cargo shell panels, satin titanium latch rails, graphite strap material, teal inventory striping, amber capacity pips, and controlled edge scuffs from repeated handling. The texture should feel functional, clean, and ready for testing while fitting final art direction. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from cargo panel seams, latch rail bevels, strap grain, capacity pip embossing, and edge scuffs.

ORM plan:
Red AO under rails and straps. Green roughness 0.54 on cargo panels, 0.34 on titanium latches, 0.76 on straps. Blue metallic on latch rails.

### SHINOBU_361_HAND_165 - Mat_ToolTrial_Combat_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Combat_Albedo.png`

Prompt to copy:
Tool trial combat albedo for prototype defensive equipment surfaces. Create satin titanium reinforcement strips, pearl ceramic impact panels, graphite grip fields, coral-red safety bands, teal alignment ticks, and amber readiness dots. The material should communicate controlled field testing and safety discipline, not uncontrolled aggression, with clean AAA material separation. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from reinforcement strip bevels, impact panel seams, grip grain, safety band paint thickness, and readiness dot rims.

ORM plan:
Red AO in seams and grip fields. Green roughness 0.34 on titanium, 0.56 on ceramic, 0.76 on grip fields, 0.58 on safety bands. Blue metallic on reinforcement strips.

### SHINOBU_361_HAND_166 - Mat_ToolTrial_Dark_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Dark_Albedo.png`

Prompt to copy:
Low-light tool trial albedo for HECTON-8 prototype equipment tested in reduced visibility. Create graphite-blue casing panels, pearl-white calibration strips, teal visibility marks, amber locator dots, satin titanium edge rails, and pale cyan reflective insets. The material should stay readable and premium in low-light gameplay while keeping clean color separation and a calm expedition identity. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from casing panel seams, calibration strip edges, locator dot rims, reflective inset lips, and rail bevels.

ORM plan:
Red AO in panel seams and under rails. Green roughness 0.5 on casing, 0.34 on titanium, 0.24 on reflective insets, 0.58 on calibration strips. Blue metallic on rails.

### SHINOBU_361_HAND_167 - Mat_ToolTrial_Dormant_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Dormant_Albedo.png`

Prompt to copy:
Dormant tool trial albedo for inactive prototype equipment. Create pearl ceramic casing panels, desaturated teal standby bands, soft amber inactive pips, graphite grip strips, satin titanium service seams, and gentle salt dust around storage edges. The material should read as powered down but maintained, useful for dormant props and test states. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from casing seams, standby band paint, inactive pip rims, grip strip grain, and storage-edge dust.

ORM plan:
Red AO around seams and strips. Green roughness medium on casing, high on dust and grips, low-medium on titanium seams. Blue metallic on service seams.

### SHINOBU_361_HAND_168 - Mat_ToolTrial_Heavy_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Heavy_Albedo.png`

Prompt to copy:
Heavy tool trial albedo for large prototype equipment. Create thick off-white ceramic armor plates, satin titanium load ribs, graphite shock pads, teal alignment bands, amber load-status pips, and pearl sediment on lower contact edges. It should feel strong, expensive, and test-ready, with broad forms that read on bulky meshes. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from armor plate bevels, load rib steps, shock pad texture, status pip rims, and sediment contact edges.

ORM plan:
Red AO under ribs and pads. Green roughness 0.54 on ceramic plates, 0.32 on titanium ribs, 0.8 on shock pads and sediment. Blue metallic on load ribs.

### SHINOBU_361_HAND_169 - Mat_ToolTrial_Scan_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Mat_ToolTrial_Scan_Albedo.png`

Prompt to copy:
Scan tool trial albedo for prototype detection surfaces. Create pearl ceramic scan plates, pale cyan sensor glass strips, teal sweep-line markings, amber data pips, graphite hand-contact pads, and satin titanium lens rails. The texture should feel scientific, readable, and polished for testing scan mechanics and final first-person tool styling. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from scan plate seams, sensor glass insets, sweep-line paint thickness, data pip rims, and lens rail bevels.

ORM plan:
Red AO in lens rail grooves and pad seams. Green roughness 0.48 on ceramic, 0.18 on glass, 0.74 on pads, 0.32 on rails. Blue metallic on lens rails.

### SHINOBU_361_HAND_170 - Meshy_AI_Alien_barnacles_clust_0301230506_texture_image_0_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Meshy_AI_Alien_barnacles_clust_0301230506_texture_image_0_Albedo.png`

Prompt to copy:
Alien barnacle cluster albedo replacement for imported Meshy source. Create elegant porcelain barnacle cups, opal cyan inner rims, pearl shell dust, soft coral-pink growth edges, tiny amber nutrient pores, and pale teal symbiotic speckles. The material should feel like beautiful abyssal hard-surface biology, suitable for clustered props and habitat overgrowth without losing clean shape readability. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from barnacle cup rims, inner bowl curvature, shell dust, pore rims, and clustered base seams.

ORM plan:
Red AO inside cups and between clusters. Green roughness high on shell dust, medium on cup walls, lower on hydrated inner rims. Blue metallic 0.

### SHINOBU_361_HAND_171 - red_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/red_Albedo.png`

Prompt to copy:
Red material replacement for HECTON-8 safety and warning surfaces. Create a premium coral-red ceramic coating with subtle pearl undercoat, satin edge wear, tiny amber inspection specks, faint teal maintenance scratches, and clean graphite seam accents. The red should be useful and beautiful, suitable for safety bands, tool accents, and controlled hazard language without flat placeholder color. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from coating thickness, tiny scratches, seam accents, and gentle edge wear.

ORM plan:
Red AO in seam accents and scratch intersections. Green roughness 0.56 on ceramic coating, 0.42 on polished edge wear. Blue metallic 0.

### SHINOBU_361_HAND_172 - Sand_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Sand_Albedo.png`

Prompt to copy:
Hecton sand albedo for clean abyssal sediment. Create pale pearl sediment grains, soft cream silt, tiny opal cyan shell flecks, muted lavender mineral dust, and subtle teal current lines painted as material color. The sand should feel elegant and alien, bright enough for gameplay readability and calm enough for broad seabed tiling. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from fine sediment ripples, grain clusters, shell fleck edges, and soft current-line ridges.

ORM plan:
Red AO in ripple troughs and under shell flecks. Green roughness high across sediment, slightly lower on shell flecks. Blue metallic 0.

### SHINOBU_361_HAND_173 - Skybox_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Skybox_Albedo.png`

Prompt to copy:
Generic Hecton skybox albedo source for atmosphere fallback. Create a refined alien-ocean sky palette with pale cyan atmospheric bands, pearl cloud veils, soft teal haze, restrained violet upper gradients, and tiny amber distant glow traces. The source should be broad, clean, seamless, and useful for skybox shaders without scenic objects or perspective composition. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
No normal required. Optional cloud normal should be ultra-soft and only support overlay motion.

ORM plan:
Metallic 0. Roughness high/uniform if packed. Emissive mask can follow distant glow traces and bright cloud veils.

### SHINOBU_361_HAND_174 - Snow_Albedo.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Snow_Albedo.png`

Prompt to copy:
Hecton snow or pale cryo-sediment albedo for cold mineral surfaces. Create pearl-white frozen sediment, pale cyan ice dust, opal mineral flecks, soft lavender compression bands, and gentle teal current-polished streaks. The material should feel bright, clean, and alien, suitable for icy deposits or cold seabed zones without becoming a generic terrestrial snow photo. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
BC5 normal from compacted grain ridges, ice dust crust, mineral fleck edges, and soft compression bands.

ORM plan:
Red AO in compression bands and under flecks. Green roughness high on powdery cryo-sediment, lower on polished ice dust. Blue metallic 0.

### SHINOBU_361_HAND_175 - Snow_Normal.png

Target: `Assets/_Project/Art/Textures/Generated/SHINOBU_361/HABITAT_INTERIORS/Snow_Normal.png`

Prompt to copy:
Heightfield source for Hecton snow or pale cryo-sediment normal map. Create shallow compacted sediment waves, fine ice-dust grain, soft opal mineral fleck relief, gentle compression ridges, and smooth current-polished troughs. This is a normal-source image, so focus on clean height logic, low amplitude, and seamless tiling for broad surfaces. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no border, no perspective object scene.

Normal plan:
Bake directly to BC5 normal. Keep ridges shallow, grain fine, fleck relief small, and troughs smooth for stable mip behavior.

ORM plan:
Red AO follows compression troughs and fleck bases. Green roughness high on powder fields, lower on polished icy troughs. Blue metallic 0.
