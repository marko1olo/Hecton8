# ARM Texture Packing Pipeline

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R47 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Current root/architecture boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md` as STATIC_DOC/STATIC_SOURCE/FILESYSTEM/PY_TOOL evidence. R46 remains the prior interior-authority/route-field/proof-language correction; R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; runtime proof remains absent.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-20 R47): `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md` is the latest local static root/architecture authority-spine, runtime-wording, and counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Owner: `SHINOBU_214`
Domain: Editor-only tech-art texture packing.
Assembly: `Assets/_Project/Scripts/Editor/TextureChannelPacker/Hecton8.Rendering.TexturePacker.Editor.asmdef`.

## Contract

- `_MaskMap` is ARM: `R=AO`, `G=Roughness`, `B=Metallic`, `A=Emission/default 1`.
- Output root: `Assets/_Project/BakedGeometry/Textures/`.
- Standalone output compression target: BC7 for ARM masks, BC5 for generated Sobel normals.
- Packer config ABI: `TexturePackerConfigDTO`, explicit 16 bytes, raw fields only.
- Request normalization is value-isolated at the API boundary and validated by `ref` internally before output path/dimension resolution.
- Output resolution resolves width and height independently from AO/Roughness/Metallic/Albedo sources, rounds each axis to power-of-two, then clamps each axis to the selected max size.
- Compile wall: the texture packer Editor asmdef references only Unity Collections, Mathematics, Burst, and Jobs packages. It does not reference sibling Hecton8 runtime assemblies.

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

## Scalability

- Low: one ARM mask sampler, baked AO, mild macro variation, lower max source size.
- Middle: one ARM mask sampler, Toksvig roughness mips, moderate source size.
- High: one ARM mask sampler, stronger Sobel normal generation where source albedo supports it.
- Ultra: spend saved sampler/bandwidth budget on near-field shader detail while keeping ARM sampler count unchanged.
- Macro FBM is continuous: base low-frequency octave always remains; octave 1/2 weights fade with `math.smoothstep(GlobalQualityWeight)` before the result is normalized.

## CSV Profile Flags

- Forge batch requests start from `TexturePackingProfile.Flags`; the UI only applies the visible normal/Toksvig/invert toggles after profile selection. Macro/noise authority stays in the CSV recipe, so prop profiles can explicitly disable the full macro pass.
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
