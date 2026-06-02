# 3DMODEL_TEXTURES_MATERIALS

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Scope: generated and authored textures, material assignment, PBR masks, atlas packing, UV density, texture import settings, and shader data streams for generated assets.

For source creation recipes, AI-assisted texture prompts, procedural height/normal/MRAO bake rules, and visual acceptance gates, read `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md` before authoring or generating texture families.

## 1. Texture Source Law

Generated meshes must use existing high-quality human-authored or AI-assisted texture assets when available. Synthetic flat colors are allowed only as validator/debug placeholders and must not ship as final art.

Texture generation may be AI-assisted, compute-baked, or externally authored, but Unity runtime must consume imported compressed texture assets. Runtime `Texture2D` creation, compression, pixel filling, or texture mask baking is banned for production gameplay.

## 2. Naming And Roles

Texture naming:

- `TX_[Family]_[Variant]_Albedo`
- `TX_[Family]_[Variant]_Normal`
- `TX_[Family]_[Variant]_MRAO`
- `TX_[Family]_[Variant]_Emission`
- `TX_[Family]_[Variant]_Detail`
- `TX_[Family]_[Variant]_Atlas`

Material naming:

- `MAT_[Family]_[Variant]`
- `MAT_[Family]_Atlas`
- `MAT_[Family]_HLOD`

Every generated material must define its texture role paths in a manifest. Missing texture is fatal unless the generator is explicitly producing a placeholder diagnostic asset.

## 3. PBR Channel Packing

Default packed mask:

- R = Metallic.
- G = Roughness or smoothness according to shader contract. The manifest must state which one.
- B = Ambient occlusion.
- A = Emission, wetness, or family mask.

Normal maps use BC5 on Standalone where possible. Albedo and packed masks use BC7 on Standalone, ASTC 6x6 on mobile/XR targets where required by platform. Albedo is sRGB. Normal and mask textures are linear.

Separate AO, roughness, metallic, and emission textures are rejected unless a shader or DCC export requires an intermediate offline step. The shipped material should minimize texture bindings.

## 4. UV Density And Distortion Gates

Every generator must compute UV metrics:

```text
surfaceArea3D = triangleArea(position)
surfaceAreaUV = triangleArea(uv)
texelDensity = sqrt(surfaceAreaUV * texturePixels) / sqrt(surfaceArea3D)
stretchRatio = max(edgeLengthUV / edgeLength3D) / min(edgeLengthUV / edgeLength3D)
```

Acceptance:

- Hero/near surfaces: stretchRatio <= 1.15.
- Standard surfaces: stretchRatio <= 1.25.
- Distant-only/HLOD: stretchRatio <= 1.50 if no readable normal detail exists.
- Adjacent island texel density mismatch <= 20 percent unless material scale changes deliberately.

## 5. Atlas Packing

Approved packers:

- MaxRects.
- Skyline.
- Guillotine with best-area-fit and rotation support.

Rejected:

- Random packing.
- Order-dependent shelf packing with more than 25 percent empty space.
- Atlas without mip padding.

Padding:

- 512: 8 px.
- 1024: 12 px.
- 2048: 16 px.
- 4096: 24 px.

Edge bleed must fill padding. Normal maps bleed in tangent-space color, not black. MRAO bleeds channel values, not transparent zero.

## 6. Material Slot Discipline

Material slots are expensive. Use few, meaningful slots.

Allowed slot meanings:

- Slot 0: primary material.
- Slot 1: secondary exposed/wear/fracture material.
- Slot 2: trim/gasket/organ/mineral/secondary surface.
- Slot 3: emissive/decal/special effect.

More than four slots requires written proof that atlas/mask blending cannot represent the surface without breaking visual quality. Material-per-part generation is rejected.

## 7. Triplanar Rules

Triplanar is allowed for:

- Large geology.
- Irregular coral mass surfaces.
- Heavy corrosion overlays.
- Hull grime/wetness projection.

Triplanar requires:

- Documented object scale.
- Stable world/object-space coordinates.
- Normal blending correction.
- Material scale consistent across LODs.
- UV0 fallback or decal coordinate channel.
- No runtime material clone per instance.

## 8. Import And Streaming Rules

Texture import must be enforced offline:

- Albedo: sRGB true, compressed high quality.
- Normal: NormalMap type, sRGB false, BC5/ASTC.
- MRAO/masks: sRGB false, compressed high quality.
- Mips enabled for world textures.
- Max size follows `GlobalQualityWeight` bake target and platform budget.
- No uncompressed runtime texture for generated world art.

Generated meshes must reference material assets, not duplicate materials. Per-instance variation uses vertex colors, material property blocks only where approved, atlas rects, or instancing data. Runtime `renderer.material` is banned.

## 9. Rejection Gates

Reject if:

- Texture roles are missing or undocumented.
- Albedo/mask/normal import settings are wrong.
- MRAO channels are empty, identical by accident, or wrong color space.
- Atlas padding is below required minimum.
- UV stretch exceeds family threshold.
- Runtime code is required to generate final textures.
- Generated material count breaks SRP Batcher/instancing without proof.

## 10. Proof Artifacts

Texture and material generation must output:

- material family, source texture ids, AI/procedural source notes when relevant, and final imported asset paths;
- texture role report for albedo, normal, MRAO, emission, height, decal, trim, and mask maps;
- import setting report proving sRGB, normal map type, compression, mip chain, max size, and streaming settings;
- UV density, stretch, island overlap, atlas rect, padding, and edge bleed report;
- material slot and SRP Batcher compatibility report;
- preview captures for albedo-only, normal-only, mask-channel view, flat lighting, and final URP lighting;
- explicit `PENDING UNITY/PROFILER VERIFICATION` if only static material rules changed.

## 11. Acceptance Sentence

A generated texture/material set is accepted only when every map has a documented PBR role, UVs or projection coordinates are measured, atlas padding survives mips, import settings are correct, material slots remain batchable, and final visual richness comes from valid offline maps rather than runtime texture generation or fake color noise.
