# Asset Owner 11 - Water Foam / Contact Authoring Packet

Status: `PENDING_VERIFICATION`
Evidence class: `STATIC_DOC` / `STATIC_SOURCE` / `STATIC_IMAGE_QA` only.
Boundary: no Unity import, material edit, Crest slot readback, scene save, runtime test, profiler, Frame Debugger, Stats, Memory Profiler, or in-game screenshot proof exists in this packet.
First-20 route moment: bright surface exit, ocean contact, shoreline/waterline readability, and photic-shallow route credibility.

## Mandates Followed

- `STRM_Async_Asset_Upload_Texture_Settings`
- `REND_URP_Graphics_HotPath_Optimization_HLOD`
- `water.md`
- `rendering.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`

## Exact Blocker

`foam.png` is rejected but active-route reachable. Static image QA says it reads as a flat tiled turquoise pattern. Static route docs mark it reachable through active world/ocean users. That makes current reachable foam/contact art a P0 visual blocker.

Flat/tiled foam contact is unacceptable for HECTON-8 surface, shoreline, and waterline presentation. Surface and photic-shallow water must stay bright, readable, premium, and at least Subnautica-level. Darkness, fog, bloom, or post grading cannot hide weak foam/contact art.

## Authoring Target

Produce offline-authored water foam/contact support maps for premium surface and waterline contact. Do not import or bind them until the Unity gate clears.

Required map stack:

- `foam_contact` albedo: clean salt foam, wet residue, and thin contact breakup. No baked lighting, no turquoise pool-foam color, no obvious tile islands.
- `foam_contact` normal/detail: fine bubble ridge and waterline disturbance only where useful. Normal strength must survive mip/compression without plastic noise.
- `foam_contact` MRAO/mask: linear channel-packed support map. Channels must be independent and documented. AO/roughness/wetness must follow material logic, not false-color noise.
- `foam_contact` RGBA contact mask: separate salt rim, wet edge, bubble breakup, residue, and shoreline/contact falloff where needed by shader/material contract.

Visual requirements:

- 2x2 and 4x4 tile checks must show no hard seam, repeated macro ring, checkerboard foam, or obvious source crop.
- Contact breakup must work at shoreline, hull/waterline, rock edge, and shallow terrain contact scales.
- Foam must read in bright surface lighting and not depend on abyssal darkness.
- Shallow water must keep terrain readability through contact edges.
- Ocean surface must retain clean color, specular read, refraction readability, and believable wet edge response.
- Authoring may use cleanup/source-only packs as reference, not as final imported art.

## Crest / Unity Asset Rule

Do not write Crest runtime wrappers, material clones, or runtime material overrides. If Crest requires a material asset, assign the asset through the approved Unity/material owner route after readback. No runtime instantiation of Crest materials.

Do not raw-patch `.mat`, `.prefab`, `.unity`, `.asset`, or `.meta` files. Do not edit `Assets/` in this packet. Author source/proof outside runtime assets until the importer/material gate is green.

## Safe Route

1. Use `Docs/GeneratedAssets/AssetSystem_20260605/FoamContactPrototype_20260605/` and `Docs/GeneratedAssets/AssetSystem_20260605/CleanupPass_20260605/` as reference/source only.
2. Author final candidate PBR/support maps offline with documented channel roles.
3. Produce contact sheet proof before import: albedo, normal preview, MRAO channels, RGBA mask channels, 2x2 tile, 4x4 tile, flat light, grazing light, and compression/mip preview if available.
4. Route proposed imports through `TEXTURE_IMPORT_ROLE_MATRIX_20260605.csv`: `foam_contact` `albedo`, `normal`, `mrao_mask`, `rgba_contact_mask`.
5. After Unity gate clears, perform importer readback for sRGB, normal type, linear masks, compression, mips, streaming mips, max size, and platform overrides.
6. After material gate clears, assign/read back approved Crest/ocean material slots without wrapper or clone.
7. Only then request bright surface, shoreline, waterline, and photic-shallow screenshots plus Stats, Frame Debugger, and memory proof.

## Acceptance Gates

- Contact sheet before import: REQUIRED.
- Importer readback: PENDING UNITY.
- Crest/ocean slot readback: PENDING UNITY.
- Bright surface screenshots: PENDING UNITY.
- Shoreline/waterline screenshots: PENDING UNITY.
- Photic-shallow readability screenshot: PENDING UNITY.
- Stats / Frame Debugger / material pass count: PENDING UNITY.
- Texture memory / VRAM residency / async upload behavior: PENDING UNITY.
- Final visual acceptance: blocked until route screenshots prove no flat/tiled foam and no muddy surface downgrade.

## Regression Model

- CPU: static authoring only here. Future shader/material use must prove no new pass or material path exceeds assigned budget; any feature above `0.1 ms` needs load-shed proof.
- GC: no runtime code touched here. Future material/render integration must prove `0 B/frame` hot-path impact.
- VRAM: authoring must avoid unbounded texture stacks. Use compressed imported maps, mips, streaming policy, and channel packing. Texture residency proof is pending Unity.
- SetPass: avoid extra material slots and per-object material clones. Crest/ocean assignment must not introduce duplicate material instances or uncontrolled variants.
- Shader variants: prefer uniform branches and existing shader contracts. New keywords or variants require ShaderVariantCollection and variant-count proof.
- Visual correctness: rejected `foam.png` cannot remain visible on active waterline/ocean contact routes. Cleanup sources remain reference-only until reauthored, imported, bound, and screenshot-proven.

## Continuous GlobalQualityWeight Consequences

- Low / compact, `GlobalQualityWeight` near `0.0`: compressed role-correct maps, conservative normal intensity, baked AO/contact masks, fewer foam layers, lower update/detail density. Bright surface, clean water color, shoreline readability, and premium wet-edge silhouette remain mandatory.
- Middle, around `0.35`: route-owned albedo/normal/MRAO/contact mask stack, stable mips, clear wet edge breakup, no proxy or source-only substitution.
- High, around `0.7`: richer detail normals, better micro-bubble breakup, stronger wet residue masks, longer near-field foam/detail residency after memory and render proof.
- Ultra, near `1.0`: layered contact response, denser micro-breakup, richer shoreline/hull wetness interaction, and visual overkill captures after Stats/Frame Debugger/memory proof. Gameplay truth, Crest ownership, material authority, and import route do not change.

## Rejection Conditions

- `foam.png` remains visible as final reachable waterline/contact art.
- Candidate reads as tiled turquoise pool foam, flat noise, blurry mud, or false-color mask art.
- Contact sheet hides seams by cropping, darkness, bloom, or fog.
- Runtime texture generation, runtime compression, runtime Crest material clone, or wrapper path is introduced.
- Imported maps have undocumented channel roles, wrong color space, missing mips for world use, wrong normal import, or unproven compression.
- Compact lane becomes ugly mode instead of a lower-density version of premium surface/waterline art.

Final status: `PENDING_VERIFICATION`.
