# Texture Visual Review - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_IMAGE_QA`.

This is a manual review of generated contact sheets. It does not prove Unity import, material binding, route lighting, VRAM residency, or in-game visual quality.

Reviewed contact sheets:

- `Docs/AssetAudit/ContactSheets/sky_aegir_cloud_contact_sheet.png`
- `Docs/AssetAudit/ContactSheets/water_foam_caustic_contact_sheet.png`
- `Docs/AssetAudit/ContactSheets/generated_source_only_contact_sheet.png`
- `Docs/AssetAudit/ContactSheets/terrain_geology_contact_sheet.png`
- `Docs/AssetAudit/ContactSheets/flora_coral_fauna_contact_sheet.png`
- `Docs/AssetAudit/ContactSheets/ui_textures_contact_sheet.png`
- `Docs/AssetAudit/ContactSheets/unknown_textures_contact_sheet.png`

`ReflectionProbe-0.exr` was not rendered in these sheets because the local System.Drawing path cannot decode EXR.

## Sky, Aegir, Clouds

Visual finding:

- Enough source texture exists.
- `TX_H8AegirGasGiantBakedDisc_1428.png` is too soft/toy-like for hero Aegir acceptance. It remains prototype/source only.
- `clouds0_diff.png`, `bo3.png`, and `oblakajip.png` are stronger source ingredients.
- `Aegir_storms.png` is a dark sparse mask. It is not primary beauty art.
- `oblaka!.png` is a plausible main cloud texture candidate but only after shader-slot readback.

Disposition:

- `SOURCE_CANDIDATE_BLOCKED_BY_READBACK`.
- Future owner must build a composed Aegir/cloud material route; do not promote the baked disc as final hero art.

## Water, Foam, Caustics

Visual finding:

- `foam.png` reads as a repeated turquoise pool-foam sheet.
- It is not premium shoreline/waterline contact art.
- Visor droplet/runoff masks are useful for visor/UI material support, not world shoreline.

Disposition:

- `foam.png`: `REJECTED_VISIBLE_SUPPORT_ONLY`.
- Required future pack: RGBA foam/contact mask for salt rim, bubble breakup, wet contact, residue, and Crest-compatible contribution proof.

## Generated Sources

Visual finding:

- Shell/sand sources are useful but show baked highlight/shadow risk.
- Wet basalt sources repeat ridge/tile families and share obvious source sameness.
- Local normal/MRAO outputs are mechanically useful but not final channel authoring.
- The generated source sheet supports use as authoring reference, not direct final import.

Disposition:

- `SOURCE_ONLY_NOT_IMPORTED`.
- Future owner must clean, re-author, channel-pack, and prove in Unity before route use.

## Terrain And Geology

Visual finding:

- There are enough scanned/processed rock, sand, mud, gravel, moss, basalt, and normal sources.
- Route cohesion is weak if these are used as random tiles.
- The strongest direction is authored wet basalt plus shell/sand photic bed, not a pile of unrelated scanned terrain sheets.
- Several source sets are technically usable but need material/tiling discipline, compression, streaming mips, and visual breakup.

Disposition:

- `SOURCE_CANDIDATE_NEEDS_CLEAN_PBR` for wet basalt/sand-shell families.
- `UNASSIGNED_STATIC_SOURCE` for unrelated scanned terrain until a material owner gives them a route role.

## Flora, Coral, Fauna

Visual finding:

- Some coral/kelp textures have convincing close-surface material language.
- The blocker is not lack of source art.
- Every reviewed flora/coral texture remains blocked by material proof and streaming-mip/import proof.
- Several albedo/detail/mask/normal stacks are visually plausible but need prefab material readback and route lighting captures.

Disposition:

- `CANDIDATE_BLOCKED_BY_MATERIAL_PROOF`.
- Do not place visible photic route coral/flora from these sources until Unity material binding, LOD, and screenshot proof exist.

## UI Sprites

Visual finding:

- Most UI sprites are visually stronger than placeholder-grade: battery, copper, cutter, microchip, oxygen, and titanium have detailed product-icon language.
- They are standalone sprite PNGs. Atlas/import/runtime UI proof remains absent.
- `oxygen-tank.png` appears black/empty in the contact sheet. Static pixel sampling found 26.43 percent nonzero alpha but 0 percent nonblack RGB, so treat it as a black silhouette/mask, not a finished colored oxygen icon.
- `Assets/_Project/Art/Sprites/ui/OXYGEN.png` is the actual detailed oxygen icon source candidate.

Disposition:

- `UI_SOURCE_ATLAS_PROOF_PENDING`.
- Do not claim HUD/UI readiness from icon source quality alone.

## Unknown Texture Class

Visual finding:

- Several unknown rows are actually useful source families: `FLOOR.png`, `FLOOR1.png`, mineral seep masks, soft plume noise, `ORGANIC.png`, menu/game art, and prologue planet surface maps.
- They are not route-owned. Their risk is ownership and material-role ambiguity, not necessarily poor visual source quality.
- `surface_norm.png` remains import/type risk because it appears in normal-map role but was statically flagged as non-normal/sRGB.

Disposition:

- Keep as `UNASSIGNED_STATIC_SOURCE` until a route owner assigns material role, import type, and proof target.

## Rejections

- Do not use `foam.png` as visible shoreline/waterline art.
- Do not use `TX_H8AegirGasGiantBakedDisc_1428.png` as final hero Aegir art.
- Do not direct-import generated wet basalt or shell/sand sheets as final.
- Do not use random terrain scans as route art without a named material family and proof.
- Do not promote flora/coral stacks while active route materials still reference proxy/placeholder assets.
- Do not use `oxygen-tank.png` as a finished colored oxygen icon.

## Required Next Proof

- Unity material-slot readback for sky/Aegir/water/terrain/flora.
- Route screenshots in bright surface/photic lighting.
- Texture import proof: compression, mipmaps, streaming mips, sRGB/normal/mask type.
- Material proof: no proxy refs, no null base textures, no stale GUIDs.
- Frame/VRAM proof before any broad route placement.

Final status: `PENDING VERIFICATION`.
