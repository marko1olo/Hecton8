# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# SHINOBU_361 Reference Bootstrap Prompts

Status: ACTIVE / LOOKDEV SEED PASS
Agent: SHINOBU_361
Purpose: manual reference creation when a texture family has no good existing visual reference.

These prompts are not a replacement for the 175 production prompts. They are the first look-dev seeds. Generate them before the full production queue when a family has no approved reference. Pick winners, then use those winners as same-family reference images for the final production textures.

Do not add these seed images to the production manifest unless an artist explicitly promotes one into a real texture target.

## Bootstrap Rule

If a family has no approved visual reference:

1. Generate 3 candidates from the matching seed prompt.
2. Reject anything with dramatic lighting, black grime, random text, logos, perspective props, noisy detail, or generic military sci-fi taste.
3. Pick one winner.
4. Use that winner as the same-family reference for the real prompt cards in `TextureProductionQueue_SHINOBU_361_HANDMADE.md`.
5. Keep the final texture prompt unchanged except for adding the chosen reference image.

## REF_SEED_001 - Premium Habitat Material Grammar

Use for: first habitat wall, ceiling, bulkhead, trim, floor, module, tool, and support marker reference when no approved habitat texture exists yet.

Prompt to copy:
Create a beautiful seamless PBR albedo reference for a premium abyssal research habitat wall system, not a square tile pattern. Build it as layered subsea architecture: one broad warm off-white composite pressure wall skin, a few long satin titanium structural ribs, graphite rubber gasket lanes that follow real pressure seams, inset service raceways, blank removable access hatches, recessed mounting rails for future instruments, and small teal/amber material accents used as opaque locator paint, not glowing light bars. Avoid repeated bathroom-tile squares; the wall must read as a continuous engineered base surface with layered construction depth. Keep open calm wall fields for later cable and tool overlays. Add controlled salt dust in deep seam corners, polished hand-wear near service rails, tiny coating chips on hard edges, and no abandoned grime. The design should feel funded, precise, heavy, pressure-rated, and habitable, closer to real subsea industrial hardware plus NASA Punk cleanliness than generic sci-fi panels. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Accept if: it reads as the HECTON-8 hard-surface master style in one image, not as random sci-fi metal.

## REF_SEED_002 - Habitat Floor Navigation Language

Use for: prologue floors, floor stripes, safe-route plates, anti-slip surfaces, traversal panels.

Prompt to copy:
Create a seamless premium expedition floor texture reference for an abyssal habitat route, with broad warm off-white composite floor plates, satin graphite anti-slip insets, thin amber navigation stripes, subtle teal maintenance ticks, titanium edge rails, shallow drainage grooves, rubber isolation seams, and light polished foot traffic arcs. The result should look clean, strong, and used with care, not abandoned. Wear is curated: small edge chips, mineral dust in grooves, faint salt rings near seams, and smooth rubber shine in walking lanes. Keep the shapes wide enough to read during gameplay and elegant enough for close inspection. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Accept if: the floor looks inviting and functional, with navigation color accents but no warning-sign clutter.

## REF_SEED_003 - Habitat Wall And Ceiling Utility Trim

Use for: wall panels, ceiling panels, service hatches, conduit sheets, modular room trims.

Prompt to copy:
Create a seamless high-quality albedo reference for the utility layer of an abyssal habitat wall and ceiling system. This is not the base wall skin and not a bathroom tile. Design a layered service overlay: long cable raceway covers, pressure-rated conduit lanes, removable utility hatches, instrument mounting rails, gasketed pass-through collars, restrained clamp brackets, blank sensor plates, and a few opaque teal/amber locator paint blocks with no glow and no text. Let some areas stay open so this layer can sit over a simpler wall base. Use warm off-white composite covers, satin titanium brackets, graphite rubber cable lips, pale salt only inside deep joints, and controlled maintenance scuffs. The surface must feel like real maintained subsea engineering: modular, functional, layered, heavy, and premium. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Accept if: it can guide walls and ceilings without becoming too busy or gloomy.

## REF_SEED_004 - Satin Titanium, Rubber, Ceramic Swatch Tile

Use for: material consistency checks across habitat, tools, vehicles, and support objects.

Prompt to copy:
Create a seamless reference tile that clearly separates HECTON-8 materials: satin titanium with fine brushed grain, warm off-white ceramic composite with subtle molded pores, graphite pressure rubber with soft matte texture, translucent teal polymer inserts, small amber safety enamel, and pale mineral residue caught only along junctions. Arrange the materials as a tasteful modular industrial pattern with clean divisions and believable physical wear. The image must show premium material response through color and surface detail only, not through baked highlights. Keep saturation restrained and elegant, with no military camouflage, no rusty junk, and no black crushed dirt. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Accept if: each material reads instantly and can be sampled as a palette reference.

## REF_SEED_005 - Visor Glass And Sealed Transparent Polymer

Use for: visor glass, inspection ports, sealed screen covers, translucent terminal surfaces.

Prompt to copy:
Create a seamless albedo reference for pressure-rated transparent polymer and visor glass used in premium abyssal expedition equipment. Show a clean smoky-clear polymer surface with faint teal optical coating, very fine micro-scratches, tiny salt freckles near gasket contact areas, soft laminated edge bands, subtle pressure stress arcs, and delicate amber service alignment marks. Keep the base mostly transparent-looking through restrained milky gray-blue values, not black, not mirror chrome. It should feel like an expensive scientific viewport that has survived use but is still carefully maintained. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Accept if: it reads as premium sealed glass/polymer, not cracked horror glass or glossy stock plastic.

## REF_SEED_006 - Diegetic Terminal Surface Without Text

Use for: terminal surrounds, interface panels, HUD-adjacent material, projected UI bases without actual labels.

Prompt to copy:
Create a seamless albedo reference for a diegetic abyssal research terminal surface with no readable text. Use warm off-white ceramic frame pieces, satin titanium bevels, graphite rubber button fields, translucent teal light pipes, small amber inactive locator windows, thin etched circuit-like grooves, and clean maintenance scuffs. The surface must suggest advanced instrument hardware without letters, numbers, symbols, labels, fake UI words, or logos. Make it beautiful and calm, with strong panel hierarchy and precise manufacturing detail. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Accept if: it gives UI-adjacent flavor while staying safe for texture tiling and avoiding generated text artifacts.

## REF_SEED_007 - Tool Casing And Handheld Equipment

Use for: cutter, scanner, construction tool placeholders, tool trial material states.

Prompt to copy:
Create a seamless albedo reference for handheld HECTON-8 expedition tool casing material. Combine warm off-white impact composite, satin titanium heat-sink ribs, graphite rubber grip fields, teal diagnostic enamel, amber power-window accents, fine screw wells, molded seam lines, small polished contact spots, and precise salt dust at gasket edges. It should look practical and expensive, like a real scientific rescue tool built for ocean pressure, not a toy and not a weapon. Keep the design readable in first person, with confident shape rhythm and controlled micro-detail. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Accept if: it can become the common reference for every tool material family.

## REF_SEED_008 - Gameplay Signal Surface Family

Use for: valid/invalid build ghosts, safe pockets, hazard pockets, resource pockets, route power, support markers.

Prompt to copy:
Create a seamless albedo reference for HECTON-8 gameplay signal materials that stay diegetic and premium. Use the normal habitat palette as the base: off-white composite, graphite rubber, satin titanium, teal science paint, and amber locator enamel. Add clean color-coded material zones without text: calm teal for safe/interactive systems, amber for power and navigation, coral-orange for caution, soft violet for biological interest, and pearl mineral white for resource readability. The image should look like believable expedition hardware, not a UI icon sheet. Keep all shapes broad, readable, and elegant, with minor scuffs and salt halos only where surfaces meet. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Accept if: gameplay meaning is carried by material zones and color restraint, not by signs or words.

## REF_SEED_009 - Resource Pocket Mineral Biology

Use for: resource veins, resource pockets, collectible nodes, biological-mineral hybrid textures.

Prompt to copy:
Create a seamless albedo reference for attractive abyssal resource pockets where alien mineral growth meets living reef tissue. Show pearly mineral crust, opal flecks, soft turquoise deposits, restrained violet-coral membranes, tiny amber crystalline inclusions, wet translucent edges, and pale sediment caught in shallow cavities. The material should feel valuable and discoverable, not diseased. Keep the composition clear enough for gameplay: clusters, veins, and pockets should read at a distance while still containing beautiful close-up detail. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Accept if: it looks rewarding and alien without becoming gross or over-saturated.

## REF_SEED_010 - Flora Pearl Membrane, Kelp, And Coral Bridge

Use for: families that bridge existing kelp/coral atlases with new flora prompts.

Prompt to copy:
Create a seamless albedo reference for beautiful HECTON-8 shallow abyssal biology, blending pearl membrane, olive-teal kelp skin, coral-violet reef ridges, soft opal pores, restrained cyan bioluminescent vein paths, and wet translucent tissue edges. The surface should look alive, healthy, and alien, with curated biological rhythm and elegant pattern flow. Avoid horror slime, gore, rot, and random tentacle clutter. Keep the albedo restrained so glow can be handled by a separate emissive mask and shine by roughness response. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Accept if: it improves the existing flora family without fighting the current `TX_ProceduralBio_Shallows` look.

## REF_SEED_011 - Opal Basalt And Pale Sediment Geology

Use for: geology when planet refs are too macro and no approved triplanar rock reference exists.

Prompt to copy:
Create a seamless albedo reference for premium alien abyssal geology on Hecton. Use blue-charcoal basalt as a restrained base, not pure black, with pale silt deposits, opal mineral veins, turquoise hydrothermal staining, pearl-white fracture dust, soft sediment pockets, and small polished wet mineral faces. The surface should feel ancient, readable, and beautiful, like a scientific sample from a deep ocean planet. Keep cracks and veins orientation-neutral for triplanar projection, with no scenic cliff forms and no directional shadow. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Accept if: it tiles as terrain and still feels like HECTON-8, not generic gray rock.

## REF_SEED_012 - Hecton Storm Sky And Ocean Color Plate

Use for: skybox, storm, gas giant, ocean backdrop, distant celestial color consistency.

Prompt to copy:
Create a seamless painterly color reference for Hecton atmospheric and oceanic presentation, using deep ocean blue, teal storm bands, opal mist, pale cyan cloud veils, muted amber storm warmth, and soft pearl highlights. The image should feel like a calm scientific planetary survey translated into a beautiful game sky source, not a stock space background. Keep the forms broad and elegant with no starscape clutter, no spaceship, no horizon scene, no cinematic sunbeam, and no hard directional lighting. Use flat, top-down, orthogonal orthographic view, completely uniform diffuse lighting, zero directional shadows, perfect seamless tiling, no text watermark, no letters, no numbers, no logo, no border, no perspective object scene.

Accept if: it unifies sky/celestial color with Hecton materials without becoming concept art.

## How To Use These Seeds Against The Production Queue

- `REF_SEED_001`, `002`, `003`, `004`: feed into `HABITAT_INTERIORS` hard-surface prompts.
- `REF_SEED_005`: feed into visor/glass/polymer prompts.
- `REF_SEED_006`: feed into terminal/HUD-adjacent material prompts.
- `REF_SEED_007`: feed into tool prompts.
- `REF_SEED_008`: feed into gameplay signal/support/resource marker prompts.
- `REF_SEED_009`: feed into resource pocket and collectible prompts.
- `REF_SEED_010`: feed into flora prompts only when existing flora refs are insufficient.
- `REF_SEED_011`: feed into geology/triplanar prompts.
- `REF_SEED_012`: feed into sky/celestial prompts.

Minimum batch plan when no references exist:

1. Generate `REF_SEED_001`, `REF_SEED_002`, `REF_SEED_010`, and `REF_SEED_011`.
2. Pick one winner from each.
3. Generate the 15 `BLOCKER` targets using the winning habitat seeds.
4. Promote the best two `BLOCKER` outputs into hard references.
5. Continue through the 175 production prompts using the promoted references.
