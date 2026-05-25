# Flooded Terrestrial Geography

Date: 2026-05-21
Status: PENDING VERIFICATION
Owner: SHINOBU_ARCHIVARIUS_SURGEON
Evidence class: STATIC_DOC

## Scope

This is the active world-generation geography template.

Rejected historical framing:

- block-style prototype terrain
- fixed square-map terrain
- single biome plateaus without flood logic

## World Premise

The playable world is infinite-paged flooded terrestrial geology. Sea level rose approximately `2000 m` over a pre-flood landmass.

Visible land is mostly mountain-crest volcanic islands. Most former lowland, river, coastal, and urban terrain is submerged.

## Depth Bands

| Band | Depth | Required geography |
|---|---:|---|
| islands | above 0 m | small volcanic peaks, ridges, caldera rims, eroded mountain crests |
| shallow shelf | 0-400 m | drowned coastlines, beaches, reefs, roads/ruins, gradual sediment shelves |
| mid slope | 400-1200 m | collapsed slopes, landslides, drowned valleys, broken escarpments |
| canyon | variable, usually 200-1800 m | submerged riverbeds and flood-cut canyons; sinuous routes, silt pockets, exposed rock walls |
| deep basin | 1200-4000 m | low-light plains, talus fields, abyssal sediment, sparse structures |
| hadal trench | below 4000 m | tectonic trenches, black-water shafts, high pressure gates, volcanic vents |

## Generation Rules

- Page terrain by sector; never assume a finite square map.
- Start with pre-flood terrestrial elevation and drainage logic.
- Apply the 2 km deluge as a sea-level transform.
- Convert old rivers into submerged canyon routes.
- Convert former coasts into shelves and drowned infrastructure.
- Use volcanic/island chains where peaks breach sea level.
- Use biome placement from depth, slope, substrate, light, heat, flow, and human/alien disturbance.

## Visual Cheats

- Current and sediment motion use scalar fields and shader offsets.
- Distant terrain uses HLOD/impostors.
- Canyon fog, silt, and caustics are shader/volume presentation unless gameplay requires physics.
- Terrain deformation visible to the player may use precomputed masks or paged deltas.

## Non-Claims

Scope: geography doctrine. Generator, biome profile, terrain asset, and streaming-scene implementation proof absent.
