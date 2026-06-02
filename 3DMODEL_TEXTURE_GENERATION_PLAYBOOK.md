# 3DMODEL_TEXTURE_GENERATION_PLAYBOOK

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Scope: offline creation of generated or AI-assisted texture families for hard-surface modules, flora, coral, fauna, geology, equipment, decals, atlases, and PBR masks.

## 1. Texture Quality Verdict

A texture is accepted only when it improves material identity under URP lighting. A noisy color map is not a texture family. A texture family is a calibrated set of albedo, normal, packed MRAO, optional emission, optional height, and optional decal/detail masks that survive mipmapping, compression, triplanar projection, atlas packing, and LOD transitions.

Generated textures must not carry baked directional lighting, fake cast shadows, camera perspective, text labels, random symbols, photographic JPEG damage, or blurred low-frequency gradients that fight scene lighting. HECTON-8 materials must read as pressure-aged metal, ceramic, rubber, abyssal tissue, mineral crust, sediment, calcified coral, wet rock, or bioelectric tissue. Clean toy plastic and flat procedural noise are rejected.

## 2. Source Generation Contract

All texture source generation is offline. AI image tools, Substance-style graph tools, compute bakers, Photoshop/GIMP edits, and Unity Editor scripts are valid only if they output imported texture assets before runtime.

Every generated texture family must record:

- Family name and intended mesh family.
- Source seed or prompt ID.
- Intended real-world scale in meters per tile.
- Texture size for compact, middle, high, and ultra bake lanes.
- Whether the material is tileable, unique-baked, atlas-packed, decal-only, or triplanar.
- Channel contract for MRAO and emission.
- Visual reference target: corroded steel, aged ceramic, basalt, sediment, soft tissue, calcified coral, rubber, brass, glass, cable insulation, or bioluminescent organ.

AI-assisted texture generation must request orthographic material samples, not object renders. Prompt language must require seamless tileable PBR material texture, no lighting, no shadows, no perspective, no text, no logo, no framed object, no blur, and no glossy plastic unless the material is intentionally polished. If the AI cannot output proper PBR maps, it may output albedo and height-like source only; normal, AO, roughness, metallic, and emission masks must be derived or corrected offline.

## 3. Mandatory Map Stack

Default shipped stack:

- Albedo: base color only. No painted shadows. No direct highlights. sRGB true.
- Normal: tangent-space normal derived from sculpt, height, or high-poly bake. Linear. BC5 where supported.
- MRAO: R metallic, G roughness or smoothness according to shader manifest, B ambient occlusion, A emission/wetness/family mask. Linear.
- Emission: only for bioluminescence, instrument glow, hot venting, energized equipment, or emergency markings.
- Detail: optional high-frequency overlay for near-field surfaces; must be shared by material family.
- Height: offline source for parallax, normal derivation, wear masks, or displacement bake. It may ship only if shader contract and platform budget allow it.

The generator must never invent roughness as a constant gray field unless the material is explicitly uniform. Corroded metal requires roughness variation. Wet rock requires cavity darkening and edge sheen. Organic tissue requires subsurface-like color transitions, pore/fold normals, and bioluminescent masks where appropriate.

## 4. Family Recipes

### Hard-Surface Metal And Ceramic

Build the material from layered fields:

- Base manufacturing layer: steel, titanium alloy, ceramic pressure plating, painted industrial coating, brass/copper accent, or rubber gasket.
- Curvature layer from mesh bevels: exposed edge wear, scraped paint, bright metal rims, salt-polished corners.
- Cavity layer from mesh AO: rust, sediment, black grime, oil, algae staining, mineral deposits.
- Directional drip layer in local gravity axis: streaked corrosion, water trails, soot, biofilm.
- Decal layer: serial plates, hazard bands, pressure marks, scratched maintenance labels, but never random unreadable text smeared across UVs.

Metallic values must obey material truth. Bare steel/brass/copper areas can be metallic. Paint, rust, grime, ceramic, rubber, algae, and sediment are non-metallic. Roughness should be high in corrosion and sediment, medium on aged paint, lower only on wet rims or polished wear.

### Flora, Coral, And Organic Tissue

Organic textures must describe structure, not only color:

- Macro albedo: root-to-tip gradients, growth rings, calcium bands, vascular streaks, bruised tissue, sediment stain.
- Height/normal: pores, folds, ridges, knuckles, branching seams, calcified scales, cracked coral cups.
- AO: deep branch intersections, underside cavities, root sockets, interior cups.
- Emission: bioluminescent veins, dots, sacs, tips, or phase masks. Emission must be spatially meaningful and compatible with vertex color G phase masks.
- Roughness: wet membrane variation, matte calcification, slick mucus only where geometry supports it.

Organic texture generation must not output perfect candy gradients, uniform neon, or symmetrical wallpaper unless the mesh is also biologically symmetric.

### Fauna

Fauna texture families must support animation and silhouette reading:

- Shell/chitin: edge polish, cracked plates, growth seams, impact scars, mineral residue.
- Skin/tissue: folds, scars, pores, organ color variation, belly/back contrast.
- Joints: darker AO, higher roughness, grime buildup, tendon/ligament masks.
- Bioluminescence: eyes, lures, rib canals, fins, spine sacs, or defensive spots with phase variation.

Texture masks must align with deformation loops and skeletal landmarks. A random emission speckle field across a jaw or fin is rejected because it cannot support readable animation.

### Geology, Rocks, Vents, And Sediment

Geology textures must be scale-calibrated:

- Basalt: chipped high-contrast edges, rough black/gray albedo, fracture normals, sparse wet sheen.
- Sediment: layered tan/gray/black deposits, fine ripple normals, shell fragments, low metallic.
- Hydrothermal vent: mineral bands, sulfur staining, oxidized rims, hot crack emission where approved.
- Ore: localized metallic inclusions, never full-rock metallic unless it is a deliberate ore node.
- Cave wall: stratification, vertical streaking, cavity AO, waterline discoloration.

Large geology should prefer triplanar or world/object projection plus localized decals. Unique unwrap is allowed only for hero rocks, cave entrances, and interactable mineral nodes.

### Equipment, Props, And Instruments

Equipment textures must preserve legibility:

- Control surfaces need clean material zones, readable emissive UI islands, worn edges, and grime only in plausible contact areas.
- Cables need ribbed normals, connector wear, rubber roughness, and color bands assigned by material slot or atlas rect.
- Tools need grip wear, metal edge scratches, oil residue, labels, and serial decals placed in UV space or decal geometry, not randomly baked noise.

## 5. Procedural Bake Algorithms

Approved offline generators may combine:

- fBm noise for fine grain, sediment, paint breakup, and tissue pores.
- Worley/Voronoi fields for corrosion pits, coral cells, cracked mud, ore pockets, and mineral growth.
- Curvature maps from mesh normals for edge wear and polished bevels.
- Ambient occlusion or bent-normal bakes for cavity grime, organic darkening, and underside dirt.
- Signed distance fields for masks around sockets, panel cuts, wounds, vents, lures, and roots.
- Directional accumulation fields for drips, soot, rust trails, sediment settling, and waterline marks.

These fields must be mixed by material semantics, not blind multiplication. Edge wear belongs on convex curvature. Grime belongs in concave cavities and downward streaks. Emission belongs to organs, instruments, vents, or energized seams. Roughness must follow material state, not random color.

## 6. AI-Assisted Texture Prompt Rules

Minimum prompt information:

- Exact material: corroded titanium pressure hull, aged black rubber gasket, calcified abyssal coral, wet basalt cliff, bruised translucent abyssal tissue.
- Orthographic seamless tile.
- PBR material sample.
- No directional light, no cast shadow, no perspective, no object silhouette, no text, no watermark.
- Required scale: 1 m tile, 2 m rock wall tile, 0.25 m rubber detail tile, or family-specific value.
- Color discipline: Deep Sea Noir and NASA-punk, pressure-aged, wet, corroded, mineral-stained, low saturation except controlled emission.

Rejected prompt outputs:

- Looks like a photograph of an object rather than a material sample.
- Has baked light or shadow gradients.
- Has visible repeated AI artifacts after a 2x2 tile test.
- Has impossible material truth, such as metallic rust, glowing dirt, or uniformly glossy coral.
- Has no height signal, no roughness logic, or no usable normal derivation path.

## 7. Continuous Quality Lanes

Texture generation must scale through continuous `GlobalQualityWeight`, not binary low/high switches:

- Compact lane near 0.0: 512 props, 1024 standard world, shared detail maps, aggressive atlas reuse, baked AO in MRAO.
- Middle lane around 0.35: 1024 props, 2048 key world materials, stronger local decals, clearer roughness variation.
- High lane around 0.7: 2048 hero surfaces, richer normals, more atlas families, clearer emission masks.
- Ultra lane near 1.0: 2048/4096 hero-only sources, denser decal layers, high precision source bakes, but shipped runtime still uses approved compression and shared material contracts.

Quality may change texture size, source bake precision, decal density, detail map intensity, and atlas page count. It must not change gameplay identity, material route ownership, prefab authority, or runtime generation law.

## 8. Texture Acceptance Gates

Before a texture family can be referenced by a generated prefab, the texture validator must test:

- 2x2 tile seam check for tileable sources.
- Histogram sanity: no crushed full-black/full-white albedo unless material reference demands it.
- Albedo luminance range compatible with URP lighting; no baked directional highlights.
- Normal strength in family range; no inverted green channel; no flat accidental normal map.
- MRAO channel independence; channels cannot be identical unless manifest proves why.
- Metallic mask matches only real exposed metal or ore.
- Roughness variation supports material identity.
- AO is cavity-biased, not random dirt across exposed planes.
- Emission mask is sparse and semantically placed.
- Compression preview does not destroy key details on compact lane.
- Mip preview does not create dark seams, ringing, or unreadable hazard/detail decals.

If any gate fails, the texture family must not be saved into the production asset route. The bake may write a diagnostic artifact under `Docs/AgentLogs` or an editor-only quarantine folder, but it must not become a referenced runtime material.

## 9. Implementation Order

Texture generation implementation must proceed in this order:

1. Build material family manifests and import settings.
2. Generate or ingest source albedo/height/reference maps.
3. Run tileability and baked-light rejection.
4. Derive normal, AO, roughness, metallic, emission, and detail masks.
5. Pack MRAO and atlas pages with bleed padding.
6. Import with platform compression and mip settings.
7. Bind to shared `MAT_*` assets.
8. Render an editor preview against neutral, low, and grazing URP lights.
9. Run validator gates and write proof artifact.
10. Only then allow mesh generators to reference the material family.

Meshes without this material route may exist as diagnostic geometry only. They are not final HECTON-8 art.
