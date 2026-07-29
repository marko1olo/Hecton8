# 3DMODEL_TEXTURES_MATERIALS

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Evidence class: STATIC_DOC / AUTHORING_STANDARD
Scope: generated and authored textures, material assignment, PBR masks, atlas packing, UV density, texture import settings, and shader data streams for generated assets.

For source creation recipes, AI-assisted texture prompts, procedural height/normal/MRAO bake rules, and visual acceptance gates, read `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md` before authoring or generating texture families.

## First-20 Route Hook

- First-20 moment: first readable wet rock, pressure-rated metal, tool surface, shallow flora/coral material, resource node, route decal, or machinery label seen in the opening route.
- Route blocker removed: prevents generated meshes from entering the first route with flat color, blurry atlases, fake PBR channels, unreadable labels, or runtime texture repair.
- Proof class: STATIC_DOC until texture role reports, import settings, UV/atlas proof, material previews, Unity import evidence, compact capture, and route screenshot exist.

## 1. Texture Source Law

Generated meshes must use existing high-quality human-authored or AI-assisted texture assets when available. Synthetic flat colors are allowed only as validator/debug placeholders and must not ship as final art.

The texture choice must support the product visual floor, not only shader correctness. Surface, coastline, shallow, medium-depth hero path, close-interaction, capsule, fauna, flora, geology, tool, and structure materials must read as detailed and beautiful in scene captures. If an optimized atlas turns the asset into blurry mud or flat color, the atlas is rejected even if the import settings are technically valid.

Texture generation may be AI-assisted, compute-baked, or externally authored, but Unity runtime must consume imported compressed texture assets. Runtime `Texture2D` creation, compression, pixel filling, or texture mask baking is banned for production gameplay.

## 2. Naming And Roles

Texture naming:

- `TX_[Family]_[Variant]_BaseColor`
- `TX_[Family]_[Variant]_NormalGL`
- `TX_[Family]_[Variant]_MaskMap_UnityURP`
- `TX_[Family]_[Variant]_ARM_AO_Rough_Metal`
- `TX_[Family]_[Variant]_Height`
- `TX_[Family]_[Variant]_Emission`
- `TX_[Family]_[Variant]_Detail`
- `TX_[Family]_[Variant]_Atlas`

**AMENDED 2026-07-29 on measurement, by the lead.** The suffixes above replace
`_Albedo` / `_Normal` / `_MRAO`, which are RETAINED HERE AS SUPERSEDED rather than deleted so the
history of the decision is not lost. Basis: a census of the 703 `TX_*` files actually shipped under
`Assets/_Project/Art/TEXTURES` returned **`_Albedo` 0 files and `_MRAO` 0 files**, against
**`_BaseColor` 138, `_NormalGL` 138, `_MaskMap_UnityURP` 138**, with `_ARM_AO_Rough_Metal` and
`_Height` completing a consistent five-map set at 2K. The document's literal suffixes therefore had
**zero instances across a year of shipped art**, so the document was the thing that was wrong.
`Tools/Blender/h8forge/law.py:499-500` already carried the shipped convention and flagged the
conflict unresolved with "Flagged for the lead rather than resolved here"; this section resolves it.
Note that `law.py` writes `TX_<Family>_<Set>_<Role>` while this section writes
`TX_[Family]_[Variant]_[Role]` — `Set` and `Variant` are the same slot under two names, and neither
spelling is a second convention.

Material naming:

- `MAT_[Family]_[Variant]`
- `MAT_[Family]_Atlas`
- `MAT_[Family]_HLOD`

Every generated material must define its texture role paths in a manifest. Missing texture is fatal unless the generator is explicitly producing a placeholder diagnostic asset.

## 3. PBR Channel Packing

Default packed mask, `_MaskMap_UnityURP`:

- R = Metallic.
- G = Ambient occlusion.
- B = Unused.
- A = Smoothness.

**AMENDED 2026-07-29 on measurement, by the lead.** The previous packing read
`R = Metallic / G = Roughness or smoothness according to shader contract / B = Ambient occlusion /
A = Emission, wetness, or family mask`, and is RETAINED HERE AS SUPERSEDED rather than deleted.
Basis: the 138 shipped `_MaskMap_UnityURP` files and **both** master shaders use Unity URP's
packing, and the decode is bit-exact against `Hecton_ModuleHardSurfaceLit` (`_MaskMap` label at
:71, decode at :349-353). So AO lives in **G**, not B, and smoothness in **A**, not G. B carries
nothing. Requiring the manifest to state roughness-versus-smoothness is therefore moot for this
map: A is smoothness by definition of the format.

Two consequences that must not be lost:

- **This is the PACKED TEXTURE mask only.** It says nothing about the VERTEX-COLOUR contract, which
  is separate and unchanged: organic `R = sway amplitude, G = biolum mask/phase, B = baked AO,
  A = family-specific`; hard surface `R = edge wear, G = oxidation, B = baked AO, A = emission/decal`
  (`3dmodel.md:123-126`, `:132-137`). Baked AO sits in **B of the vertex stream** and in **G of the
  packed texture**, and those two facts are both true. Conflating them is how a reader ends up
  applying a harvest mask as occlusion.
- `_ARM_AO_Rough_Metal` is a DIFFERENT layout that also ships (R = AO, G = roughness, B = metal) and
  is NOT interchangeable with `_MaskMap_UnityURP`. Binding it where the URP mask is expected puts AO
  in the metallic slot. Pick by suffix, never by assumption.

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

## 9. Runtime Truth And Hot-Path Boundary

Texture and material generation is an editor/offline authoring route. Runtime truth is the imported texture asset, material asset, shader contract, atlas rect, channel manifest, streaming handle, and prefab/material reference that consume the generated output.

Runtime hot paths must not create `Texture2D` assets, fill pixels, compress textures, bake masks, repack atlases, unwrap UVs, call `renderer.material`, or instantiate per-prefab materials. Runtime may only bind approved material assets, update predeclared shader parameters, select already-imported texture/atlas variants through an owner route, and stream/release tracked handles.

`GlobalQualityWeight` may scale texture max size, streaming residency, decal/material detail intensity, and optional diagnostics. It must not change material channel semantics, atlas rect identity, prefab authority, gameplay truth, save identity, or shader ABI.

## 9.1 Decal Source And Binding Contract

Decal atlases are not shippable merely because an image exists.

Accepted decal path:

- source prompt/output identifies the asset as `DECAL_ATLAS`, `UV_ATLAS`, or a specific material-source role;
- atlas review proves no readable text, no watermark/logo, no cropped islands, enough transparent/padded border, and no baked lighting that should belong to the scene;
- split/padded/alpha candidate tooling produces imported source textures with stable `.meta` GUIDs;
- generated materials live under first-party material folders and keep transparent render state;
- authoring tools bind material assets or texture arrays to prefabs/renderer features by stable asset reference;
- runtime systems consume the imported material/array reference only. They must not extract islands, create textures, repair alpha, or build materials during gameplay.

Current source routes:

- world-support damage/glass/organic decals: Batch34 alpha candidates -> `WorldSupportGeneratedDecalMaterialBuilder` -> first-party generated decal materials -> deterministic quad children in support/world authoring.
- visor trauma decals: Batch34 alpha candidates -> `Batch34VisorTraumaDecalArrayIntegrator` -> `TX_B34_VisorTrauma_DecalArray.asset` -> `DeferredDecalPass`.
- padded needs-work atlases are handoff sources for UV/decal binding, not inventory icons and not automatic Lit materials.

Failure path to check before acceptance: missing source texture, bad alpha edge, insufficient atlas padding, wrong sRGB/linear import state, missing `.meta` GUID, material cloned per prefab, stale vendor decal prefab, missing renderer feature binding, wrong atlas slice order, and runtime code attempting to generate or repair final decal assets.

## 10. Rejection Gates

Reject if:

- Texture roles are missing or undocumented.
- Albedo/mask/normal import settings are wrong.
- MRAO channels are empty, identical by accident, or wrong color space.
- Atlas padding is below required minimum.
- UV stretch exceeds family threshold.
- Runtime code is required to generate final textures.
- Generated material count breaks SRP Batcher/instancing without proof.

## 11. Proof Artifacts

Texture and material generation must output:

- material family, source texture ids, AI/procedural source notes when relevant, and final imported asset paths;
- texture role report for albedo, normal, MRAO, emission, height, decal, trim, and mask maps;
- import setting report proving sRGB, normal map type, compression, mip chain, max size, and streaming settings;
- UV density, stretch, island overlap, atlas rect, padding, and edge bleed report;
- material slot and SRP Batcher compatibility report;
- preview captures for albedo-only, normal-only, mask-channel view, flat lighting, and final URP lighting;
- explicit `PENDING UNITY/PROFILER VERIFICATION` if only static material rules changed.

## 12. Acceptance Sentence

A generated texture/material set is accepted only when every map has a documented PBR role, UVs or projection coordinates are measured, atlas padding survives mips, import settings are correct, material slots remain batchable, and final visual richness comes from valid offline maps rather than runtime texture generation or fake color noise.
