# HECTON-8 Gemini Prompt - Inventory Gap Sheet From Live ItemData

Positive reference image:
`Docs/GeneratedAssets/Gemini/Outputs/Batch30/InventoryIsolatedObjects_20260607/TX_B30_InventoryIsolatedObjects_Source_20260607_Gemini.png`

Use the positive reference only for physical 3D prop readability, separated object-sheet composition, hard-surface material richness, and three-quarter inventory presentation. Do not copy the exact objects.
Reference caveat: if the reference contains cropped props, generator watermarks, or text-like surface marks, treat those as defects to avoid, not as style targets.
All new objects must have unmarked physical surfaces: scratches, seams, bevels, dirt, chips, and abstract wear are allowed, but any deliberate glyph-like stroke, label plate, serial mark, icon, printed symbol, or UI marking is a failure.

Do not use old project UI sprites as references. They are legacy and must not influence this image.

## Prompt

Create one improved HECTON-8 inventory object source sheet.

Generate twelve distinct AA-quality physical objects in a clean invisible four-column by three-row layout. Use the exact reading order below; the companion spec JSON maps these positions to project PersistentIds. Do not render any position markers, names, captions, numbers, letters, arrows, symbols, or grid.

top-left position: dedicated salvage sampler tool with short cutting jaw, sample corer tip, bio-growth scraping edge, sealed grip, pressure-rated hinge
top-second position: single rugged electrolyte ampoule held alone in a protective pressure sleeve, translucent fluid window, capped injection neck
top-third position: irregular titanium salvage plate fragment with torn pressure-hull edge, bolt holes, scraped oxide, cut brace rib, clearly scrap not a clean ingot
top-right position: compact seafloor drill with short hardened bit, pressure-sealed motor housing, mineral dust scuffs, two-hand industrial grip
middle-left position: compact beacon deployment tool with folded handle, antenna socket, pressure-rated clasp
middle-second position: rugged fabrication builder tool with industrial grip, modular nozzle, material feed port
middle-third position: laser cutter tool with ceramic heat shield, lens shroud, gasket seams, scorched nozzle edge
middle-right position: hydroacoustic scanner wand with sensor face, hydrophone ribs, blank sealed glass
bottom-left position: bundled copper wire spool with real copper strands, black insulating ties, pressure-safe spool core
bottom-second position: pressure seal component with thick gasket ring, clamp segments, worn rubber and titanium bite marks
bottom-third position: sealant pack with rugged squeeze cartridge, capped nozzle, plain industrial polymer pouch
bottom-right position: sealed sensor module cluster with glass sensor eye, ceramic sockets, cable ports

Each object must be a believable AA/AAA survival-game inventory prop, not a flat icon. Aim above Subnautica item thumbnail quality: stronger material breakup, clearer silhouette, better industrial logic, less toy-like, less mobile-game.

Layout constraints:
- one object per invisible cell
- large empty spacing between objects
- every object fully inside its cell with at least one quarter of the cell kept as empty padding
- keep a clear safety moat around every object: at least fifteen percent of cell width and height empty on all sides
- no handle, drill bit, wire strand, nozzle, ring, or ingot corner may enter the outer cell-border band
- each object centered as a complete physical product render, never a close-up crop
- if a tool or resource feels too large for its cell, make it smaller rather than cropping it
- no object touches the image border
- no object is cropped
- no overlap
- no visible grid lines
- neutral dark gray matte background, flat and removable
- no floor horizon
- no cast shadows that connect objects

Hard negative constraints:
- no text
- no labels
- no letters
- no numbers
- no alphanumeric glyphs
- no fake alien glyphs
- no label plates
- no text-like decal noise
- no readable markings printed on object surfaces
- no printed surface marks of any kind
- no screen UI text
- blank screens, lenses, and glass only
- no serial numbers
- no warning stickers
- no diagrams or pictograms that resemble labels
- no logos
- no UI frames
- no circular badges
- no square icon cards
- no inventory slot backgrounds
- no captions
- no sticker-sheet look
- no mobile-game icon style
- no flat vector art
- no cartoon toy look
- no object touching any edge
- no decorative sparkle on the objects

Rendering target:
three-quarter view, crisp edges, real thickness, bevels, bolts, seams, gaskets, scratches, chipped paint, grime, worn polymer, ceramic, glass, titanium, copper, rubber, restrained cyan instrument accents, strong readable silhouette at small inventory size, natural object-camera distance with no badge rim, halo, glow card, or app-store icon pose.

Identity must come from silhouette, mechanical construction, material, color accents, and proportions only, never text.
