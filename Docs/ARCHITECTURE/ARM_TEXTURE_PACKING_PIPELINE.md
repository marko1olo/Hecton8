# ARM Texture Packing Pipeline

Status: STATIC_ROUTE_DOC / RUNTIME_PROOF_PENDING
Evidence class: STATIC_DOC / STATIC_SOURCE
Owner domain: Editor-only tech-art texture packing
Review disposition: YELLOW / STATIC_DOC_ONLY until compile/import/runtime/profiler/player proof exists.

Owner: `SHINOBU_214`

Domain: Editor-only tech-art texture packing.

Assembly: `Assets/_Project/Scripts/Editor/TextureChannelPacker/Hecton8.Rendering.TexturePacker.Editor.asmdef`.

## Contract

- Production `_MaskMap` ARM/ORM route: `R=AO`, `G=Roughness`, `B=Metallic`.

- ARM alpha is shader-specific. `Hecton8/Rendering/UberNoir` treats `A` as emission/default 1. `Hecton8/Rendering/MasterLit` currently must treat ARM `A` as reserved/default 1 unless the shader owner fixes and proves ARM-layout emission.

- `Hecton8/Rendering/MasterLit` requires `_MasterShadowParams.w = 3` to decode production ARM. Its material default is layout `0` (`MRAO`), so importing an ARM texture without setting the layout is wrong.

- As of the 2026-06-05 static YAML scan, no current serialized `.mat`, `.prefab`, `.unity`, or `.asset` user of `Hecton8/Rendering/Hecton_Master_Lit` or `_MasterShadowParams` was found. Any new Batch31 MasterLit promotion must therefore create material serialization proof instead of relying on existing material state.

- `Hecton8/Bakers/MraoAtlasLit` is a preview/baker/source route, not the production ARM route. It decodes `_MraoMap` as `R=Metallic`, `G=Roughness`, `B=AO`, `A=Emission`.

- `TerrainMaster` control maps and photic terrain-only shaders are not packed PBR mask consumers. Do not route Batch31 packed masks into terrain control channels.

- Batch31 rule: never assign a generated `MRAOSource` texture to production `_MaskMap` by filename. Repack MRAO to ARM or explicitly target a shader/material layout that decodes MRAO.

- Output root: `Assets/_Project/BakedGeometry/Textures/`.

- Standalone output compression target: BC7 for ARM masks, BC5 for generated Sobel normals.

- Packer config ABI: `TexturePackerConfigDTO`, explicit 16 bytes, raw fields only.

- Request normalization is value-isolated at the API boundary and validated by `ref` internally before output path/dimension resolution.

- Output resolution resolves width and height independently from AO/Roughness/Metallic/Albedo sources, rounds each axis to power-of-two, then clamps each axis to the selected max size.

- Compile wall: the texture packer Editor asmdef references only Unity Collections, Mathematics, Burst, and Jobs packages. It does not reference sibling Hecton8 runtime assemblies.

- Accessor purity: `Resolve*` helpers are pure dimension/mip math.
- Asset path creation, CSV cursor parsing, set-key building, prefab path building, and format-label construction use command/parser/build names.
- No read accessor mutates state or creates folders.

## Static Evidence Anchors

- `Assets/_Project/Scripts/Editor/TextureChannelPacker/HectonMaskChannelPacker.cs`: current packer contract states `R=AO, G=Roughness, B=Metallic, A=Emission/default 1`.

- `Assets/_Project/Scripts/Editor/TextureChannelPacker/HectonArmTextureChannelPacker.cs`: Toksvig/mip output writes `AO, Roughness, Metallic, 255`.

- `Assets/_Project/Scripts/Editor/TextureChannelPacker/TextureChannelPackerWindow.cs`: preview text reports `R=AO, G=Roughness, B=Metallic`.

- `Assets/_Project/Scripts/Editor/HectonMasterMaterialMigrator1615.cs`: migrator writes `_MasterShadowParams.w` from detected mask semantics. `UberNoir` `_MaskMap` migrates to layout `3`; generic `_MaskMap` migrates to layout `1`; `_MRAOMap`/`_MraoMap`/`_PackedMap` falls back to layout `0`.

- `Assets/_Project/Art/Shaders/Hecton_Master_Lit.shader`: `_MasterShadowParams.w` selects mask layout; layout `3` decodes ARM `R=AO/G=Roughness/B=Metallic`, while emission mask weighting is currently layout `2` only.

- `Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl`: `_MaskMap` decode uses `R=AO`, `G=Roughness`, `B=Metallic`, `A=Emission`.

- `Assets/_Project/Art/Shaders/Bakers/Hecton_MraoAtlasLit.shader`: `_MraoMap` decode uses `R=Metallic`, `G=Roughness`, `B=AO`, `A=Emission`.

- `Assets/_Project/Art/Shaders/TerrainMaster.shader`: terrain lighting uses albedo alpha for smoothness, constant metallic, and occlusion `1`; it has no packed PBR mask sampler.

## Runtime Boundary

Generated ARM textures are visual assets, not gameplay state.

- Exclude generated texture bytes from rollback snapshots.

- Exclude material assignments and importer/mipmap settings from Merkle state hashes.

- Exclude generated texture data from `StateRingBuffer`.

- If a gameplay system needs material identity, hash the stable asset GUID/token only, never texture pixels.

## Editor Blackbox

- `TexturePackerTelemetryEntry` is explicit 64 bytes: one cache line per forensic entry.

- `TexturePackerBlackBox` owns a 300-entry Editor-only `NativeArray` ring and releases it on assembly reload or editor quit.

- Dump target: `Docs/AgentLogs/Dump_SHINOBU_214.bin`.

- Dump trigger: manual menu, pack exception, or non-finite pack timing.

- This ring is not runtime authority memory and is not a gameplay rollback buffer.

- Job completion boundaries are Editor materialization only: texture asset serialization, mip `SetPixelData`, mock benchmark reporting, UI preview texture creation.
- No runtime `.Complete()` path exists in this domain.

## Scalability

- Low: one ARM mask sampler, baked AO, mild macro variation, lower max source size.

- Middle: one ARM mask sampler, Toksvig roughness mips, moderate source size.

- High: one ARM mask sampler, stronger Sobel normal generation where source albedo supports it.

- Ultra: spend saved sampler/bandwidth budget on near-field shader detail while keeping ARM sampler count unchanged.

- Macro FBM is continuous: base low-frequency octave always remains; octave 1/2 weights fade with `math.smoothstep(GlobalQualityWeight)` before the result is normalized.

## CSV Profile Flags

- Forge batch requests start from `TexturePackingProfile.Flags`.
- UI applies visible normal/Toksvig/invert toggles after profile selection.
- Macro/noise authority stays in the CSV recipe.
- Prop profiles can disable the full macro pass.

- Empty flag cell: default hard-surface recipe.

- `none`, `off`, `false`, `0`: no optional stages.

- `macro` or `noise`: offline macro AO/Roughness bake.

- `toksvig` or `mip`: variance-preserving ARM mips.

- `normal` or `sobel`: offline Sobel normal generation.

- `invert` or `smoothness`: smoothness-source inversion into roughness.

## Reports

- `Docs/Reports/TEXTURE_PACKING_REPORT.json`: last pack operation and byte estimates.

- `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`: material scan for loose AO/Roughness/Metallic sampler stacks.

- `Docs/Reports/TEXTURE_PACKER_LAYOUT_REPORT.json`: DTO offset validation.
