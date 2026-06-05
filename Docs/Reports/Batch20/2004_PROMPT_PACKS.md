# Batch20 2004 Flora Coral Prompt Packs

Status: prompt pack only. No image generation was executed by 2004.

Use these prompts for external/Gemini/source-image generation or for an internal art-source pass. Every prompt is for source material and reference extraction, not final Unity proof.

## Global Negative Prompt

No text, no labels, no watermark, no UI, no logo, no perspective camera angle, no visible light source, no cast shadows, no baked scene lighting, no black/noir grading, no muddy haze, no blur, no cartoon finish, no toy plastic, no primitive mesh look, no flat vector pattern, no over-neon glow, no random full-surface bioluminescence, no alpha-card vegetation wall.

## Output Rules

- Produce orthographic, material-sample, or seamless-tile source images.
- Prefer 4096 square source when practical; 2048 minimum for non-hero support.
- Keep source lighting neutral and extractable for PBR maps.
- Include enough macro structure to derive albedo, detail, normal, AO, roughness, and localized emission/wetness masks.
- Do not include fish/fauna silhouettes in material-source images. Ecology composition belongs to proof shots, not texture source.

## SRC_2004_KELP_BLADE_FIBER_4K

Prompt:

Orthographic seamless material source for premium alien photic kelp blades, thick wet translucent lamina with olive green, bronze, and deep teal pigment variation, visible longitudinal ribs, fine vascular fiber streaks, serrated torn edges, healed scars, small abrasion marks, subtle edge thickness, localized darker wet folds, no scene lighting, no shadows, no perspective, no text. Designed for extracting albedo, detail, tangent normal, AO, roughness, and sparse edge translucency masks for a Subnautica-level underwater route.

Reject if:

- It reads as flat paper, grass, cloth, cartoon seaweed, or alpha card.
- It has a photo scene background, water caustic shadows, or black cinematic grade.

## SRC_2004_KELP_HOLDFAST_ROOT_4K

Prompt:

Orthographic seamless material source for kelp holdfast roots gripping submerged rock, dense branching root pads, wet dark olive and brown surface, pale rubbed contact tips, sediment trapped in cavities, barnacle-like micro encrustation, strong contact AO shapes, no perspective, no cast shadows, no labels. Material must support close first-person inspection and vertex AO/cavity masks.

Reject if:

- It looks like generic tree roots, dry dirt, or low-detail brown noise.
- Contact cavities are only painted color without normal/AO structure.

## SRC_2004_KELP_CANOPY_EDGE_4K

Prompt:

Orthographic source sheet for upper kelp canopy edges, broad overlapping fronds, thick torn rims, curled translucent tips, ribbed fan shapes, weathered notches, wet specular roughness variation, olive/green/amber gradients, localized edge glow masks only, no background scene, no light direction, no shadows, no text. Designed for readable waterline silhouettes and large hero canopy variants.

Reject if:

- The canopy becomes a flat curtain or featureless wall.
- Bioluminescence covers the whole surface instead of controlled edges/cuts.

## SRC_2004_INTERTIDAL_WEED_LICHEN_4K

Prompt:

Orthographic material and shape source for alien intertidal shoreline flora, wet tide-pool weed mats, salt grass tufts, dark green algae ribbons stuck to rock, orange and pale lichen crusts, small rooted shoreline plants, brine stains, sand abrasion, wet/dry boundary marks, no underwater kelp silhouettes, no scene lighting, no perspective, no text. Must read as coastal/intertidal, not submerged kelp placed on land.

Reject if:

- It resembles ordinary lawn grass, jungle foliage, or underwater kelp on dry terrain.
- It lacks wet shoreline contact cues.

## SRC_2004_CORAL_BRANCH_CALCITE_4K

Prompt:

Orthographic seamless source for branching alien coral/fossil carbonate, calcified rough branches with welded intersections, porous skin, pale cream, muted coral, oxide red, and mineral teal accents, broken tips, growth rings, sediment in branch crotches, localized cavity biolum masks, no scene lighting, no cast shadows, no perspective, no text. Source must support normal, AO, roughness, and emission extraction for premium branching reef geometry.

Reject if:

- It becomes smooth tubes, plastic antlers, bouquet shapes, or neon coral.
- Branch intersections are unconnected or hidden by glow.

## SRC_2004_CORAL_MASSIVE_POROUS_4K

Prompt:

Orthographic seamless material source for massive coral domes and fossil carbonate heads, lobed porous calcium surface, crater pores, layered growth bands, sediment abrasion, chipped shelter cavities, muted limestone, pink coral, green mineral staining, high AO in pores and cracks, no scene lighting, no shadows, no perspective, no labels. Designed for first-person closeups and believable reef bulk.

Reject if:

- It reads as generic rock, smooth boulder, sponge cartoon, or blurry noise.
- Pores are only color dots without normal depth.

## SRC_2004_CORAL_PLATE_RIM_UNDERSIDE_4K

Prompt:

Orthographic source sheet for plate coral ledges, thick layered carbonate plates with chipped rims, underside striations, shelf terraces, mineral bands, pale limestone and soft coral color variation, dark AO under plate lips, subtle localized biolum in underside cracks, no perspective, no cast shadows, no scene background, no text. Must support plate thickness, underside AO, and side-light route readability.

Reject if:

- It looks like paper sheets, mushroom caps, or flat decals.
- Undersides are absent or texture-only.

## SRC_2004_CORAL_LOW_SPONGE_BED_4K

Prompt:

Orthographic seamless material source for low coral and sponge seafloor beds, small porous mounds, sponge openings, encrusting mats, calcium grains, scattered shell scars, sand/silt abrasion, muted reef colors, localized tiny biological glow in pores only, no perspective, no cast shadows, no text. Material should break up floor routes without becoming noisy carpet.

Reject if:

- It reads as a flat decal carpet, generic gravel, or cartoon sponge.
- It hides missing low mound topology behind color noise.

## SRC_2004_REEF_FAN_SOFT_RIB_4K

Prompt:

Orthographic source sheet for soft reef fan and brittle coral motion accents, ribbed fan membranes with branching support veins, holes, tears, frayed edges, pale mineral ribs, muted teal and coral tissue, localized edge biolum masks, no alpha-card background tricks, no scene lighting, no shadows, no perspective, no text. Source must support geometry-backed fan panels with vertex sway masks.

Reject if:

- It is just a transparent flat fan card.
- It has random full-surface glow or decorative neon lace.

## SRC_2004_ANCHOR_DEBRIS_ENCRUSTED_4K

Prompt:

Orthographic material and shape source for shoreline anchor/debris blend, wet corroded dark metal, barnacles, coral encrustation, rope fibers, chain links, algae growth, salt crust, tide stains, sand abrasion, clear metal versus organic mask regions, no scene lighting, no cast shadows, no perspective, no labels. Used for a coastline transition and scale cue, not as clean sci-fi prop art.

Reject if:

- It is clean metal, primitive cylinders/cubes, or generic shipwreck background art.
- Organic and metal material masks cannot be separated.

## SRC_2004_BIOLUM_DETAIL_MASKS_4K

Prompt:

Orthographic mask-source sheet for localized underwater biological glow, small pores, branch cavities, kelp edge cuts, soft coral rib tips, and route-readable dotted signals, dark neutral background only for mask extraction, no scene lighting, no bloom haze, no random full-surface neon, no text. Glow patterns must look biological and useful for navigation/ecology, not decorative noise.

Reject if:

- Glow covers entire organisms.
- It produces unreadable speckle noise or nightclub colors.

## Family Composition Prompt

Use after individual material source images exist.

Prompt:

Premium photic shallow reef flora/coral source composition for HECTON-8, bright readable Subnautica-level underwater route, tall kelp hero silhouettes with grounded holdfasts, dense kelp patches with clear fauna sight lanes, upper canopy crowns, branching coral, massive coral heads, plate coral ledges, low coral/sponge floor beds, and shoreline intertidal algae/debris transition. Show forms as isolated asset proof on neutral background, not a finished game screenshot. Emphasize silhouettes, topology, material breakup, LOD-read forms, and localized ecological biolum cues. No placeholder shapes, no primitives, no dark masking, no text.

Use this only for art direction reference. It is not a substitute for individual PBR source maps or Unity proof.
