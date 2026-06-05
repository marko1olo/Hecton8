# 1906 PBR Channel Derivation QA Packet

Agent: 1906  
Role: PBR channel derivation QA specialist  
Evidence class: STATIC_DOC / STATIC_SOURCE only  
Unity/build/import/material/prefab/scene/profiler/player proof: NOT RUN

## Boundary

This packet validates static PBR channel contracts and source-readiness only. It did not open Unity, call Unity MCP tools, run dotnet/build/package restore, import textures, edit Assets, create Unity serialized files, assign materials, touch prefabs, touch scenes, or create `.meta` files.

Owned outputs:

- `Docs/GeneratedAssets/Gemini/QA/1906/`
- `Docs/Reports/Batch19/1906_SHADER_CHANNEL_CONTRACT_MATRIX.csv`
- `Docs/Reports/Batch19/1906_PBR_CHANNEL_DERIVATION_QA.md`
- `Docs/Reports/Batch19/1906_PBR_CHANNEL_DERIVATION_QA.csv`
- `Docs/Tasks/Status_1906.md`
- `Docs/AgentLogs/Rationale_1906.md`
- `Docs/AgentLogs/LOG_1906.md`

## Authority Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `Docs/Orchestration/GEMINI_BROWSER_IMAGE_ASSET_WORKFLOW.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `rendering.md`
- `performance.md`
- `quality.md`
- `Docs/Reports/Batch18/1886_PRODUCT_FACE_TEXTURE_AUTHORING_PIPELINE_DISCOVERY.md`
- `Docs/Reports/Batch18/1888_PRODUCT_FACE_TEXTURE_CHANNEL_MANIFEST_AND_SHADER_AUDIT.md`
- `Docs/Reports/Batch18/1891_AITEXTURE_PRODUCT_FACE_HARDENING_PACKET.md`
- `Docs/Reports/Batch18/1894_PRODUCT_FACE_TEXTURE_SOURCE_MANIFEST_SCHEMA.md`

Mandates read:

- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`

Additional source-ledger files inspected for candidate inventory:

- `Docs/Reports/Batch18/1821_SHORELINE_WATERLINE_OFFLINE_BAKE_SPEC.md`
- `Docs/Reports/Batch18/1821_SHORELINE_WATERLINE_BAKE_INPUTS.csv`
- `Docs/Reports/Batch18/1880_TOOL_MATERIAL_TEXTURE_MATRIX.csv`
- `Docs/Reports/Batch18/1881_RESOURCE_MATERIAL_TEXTURE_MATRIX.csv`
- `Docs/Reports/Batch18/1882_TRANSPORT_PLAYER_MATERIAL_TEXTURE_MATRIX.csv`
- `Docs/Reports/Batch18/1883_SKY_OCEAN_MATERIAL_TEXTURE_MATRIX.csv`
- `Docs/Reports/Batch18/1888_PRODUCT_FACE_TEXTURE_CHANNEL_MANIFEST.csv`
- `Docs/Reports/Batch18/1894_PRODUCT_FACE_TEXTURE_SOURCE_MANIFEST_SEED.csv`
- `Docs/Reports/Batch18/1896_TOOLSCREEN_DIEGETIC_SHADER_CHANNEL_MATRIX.csv`

`Docs/Actual Domains of Project.txt` was checked and absent. Narrow domain used: static PBR channel derivation and shader-channel contract QA for generated/docs-only sources.

## Contract Lock

Accepted exact static contracts are written to:

`Docs/Reports/Batch19/1906_SHADER_CHANNEL_CONTRACT_MATRIX.csv`

Accepted contracts:

- `PackedMaskV1_ToolDecayLit`: `_MaskMap R Metallic, G AO/Occlusion, B Smoothness, A EmissionMask`.
- `ProceduralBio_ORM`: `_ORMAtlas R Occlusion, G Roughness, B Metallic, A EmissionMask`.
- `MraoAtlasLit_MRAO`: `_MraoMap R Metallic, G Roughness, B AO, A EmissionMask`.
- `SuitVisor_VisorMask`: `_VisorMaskTex R Dirt, G Scratch, B Salt, A Condensation`.
- `ToolScreenDiegetic_Signal`: `_ToolScreenTex RGB live display signal; alpha not read`.
- `FoamRibbon_RGB`: `_BaseMap/_MainTex R foam flow A, G foam flow B, B breakup`; no accepted alpha texture contract in this packet.

Blocked contracts:

- AI/UberNoir ARM: blocked. Filename terms `_arm`, `_mask`, `_packed`, `ARM`, `MRAO`, and `ORM` are not channel truth.
- Resource mineral, resource organic, transport hull/rubber/trim/glass, and suit body/trim routes: blocked until material owner chooses shader route and source manifest.
- ToolScreenDiegetic `_BaseMap`, `_MainTex`, and `_EmissionMap`: blocked because Batch18 1896 says they are declared but not sampled.

## Source Inventory Result

Actual docs-side image source found:

- `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_1428_MANIFEST.md`
- `Docs/GeneratedAssets/Gemini/QA/Gemini_Downloaded_PNG_preview.png`
- `Docs/GeneratedAssets/Gemini/QA/TX_Gemini_WetBasaltShoreline_Albedo_20260604_tile2x2.jpg`

The manifest points at a canonical texture under `Assets/**`. This packet did not read, write, classify, import, or promote that Asset path as owned output.

Batch18 source-ledger candidates are mostly missing-source or blocked-contract rows:

- Tool families need albedo, normal, and PackedMaskV1 maps. Source status: `PENDING SOURCE`.
- Resource minerals need seam/nodule sources and material owner route. Source status: `BLOCKED_CHANNEL_CONTRACT`.
- Organics need pickup-owned derivatives and organic shader route. Source status: `BLOCKED_CHANNEL_CONTRACT`.
- Transport and player suit body/trim/glass need dedicated families. Source status: `BLOCKED_CHANNEL_CONTRACT`.
- Foam/waterline bake products are missing. Source status: `PENDING SOURCE`.
- Environment donors are route-owned references, not ProductFace PBR sources. Status: `STATIC REJECTED` for donor use.

Final inventory rows are in:

`Docs/Reports/Batch19/1906_PBR_CHANNEL_DERIVATION_QA.csv`

## Static Preview QA

Safe local image tooling found: Python Pillow.

Generated QA-only artifacts:

- `Docs/GeneratedAssets/Gemini/QA/1906/TX_H8_WetBasaltShoreline_1428_static_channel_QA_contact_sheet.png`
- `Docs/GeneratedAssets/Gemini/QA/1906/TX_H8_WetBasaltShoreline_1428_static_channel_QA_metrics.txt`

Preview content:

- source albedo;
- 2x2 tile preview;
- height-like luma;
- normal-from-height preview;
- roughness candidate;
- AO candidate;
- metallic-zero candidate;
- emission-zero candidate.

Metrics from the QA file:

- dimensions: 1024x1024;
- alpha: opaque;
- luma mean/stddev: 85.97/46.97;
- edge seam mean absolute RGB diff left-right/top-bottom: 30.78/33.40.

These previews are not production maps. They are QA visualization only.

## Derivation QA

### WetBasaltShoreline 1428

Status: `STATIC REJECTED` for production PBR derivation.

Reason:

- Source is albedo-only.
- Edge seam metrics are too high for a production seamless material without a seam-fix pass.
- Normal-from-luma preview risks embossing color/albedo noise instead of physical fracture relief.
- AO-from-luma risks broad darkening instead of cavity-only occlusion.
- Roughness-from-luma is only a visual heuristic and does not prove wet/dry basalt material state.
- Metallic and emission should remain zero for basalt unless an explicit ore/hot vent material owner supplies a different route.

Required before promotion:

- seam-fixed albedo source;
- true height or sculpt/bake source;
- normal map with direction consistency;
- cavity-biased AO;
- roughness map separating wet cracks/mineral stains from dry raised sediment;
- packed map under exact shader contract;
- Unity import/material/scene proof by Unity owner.

### Tools

ToolDecayLit contract is static-accepted. Source maps are missing. Future derivation must obey wear logic:

- exposed metal only where paint is chipped or metal is visible;
- salt and scratches must follow plausible handling, edges, and direction;
- roughness must separate paint, exposed metal, rubber, glass, grime, and wetness;
- emission masks belong only to diagnostics, lenses, indicators, or real readout surfaces.

### Resource Minerals

No accepted source-channel candidates. MRAO reference exists, but per-resource owner route is not locked. Future derivation must enforce:

- metallic only in real metal seam or ore inclusion;
- host rock non-metal;
- AO cavity-only in cracks/under nodules;
- roughness based on fracture, wetness, soot, residue, or polished seam state;
- no terrain recolor sold as ore.

### Organic / Coral / Kelp

No accepted source-channel candidates. ProceduralBio ORM is static-accepted only when an organic pickup owner declares it. Future derivation must enforce:

- porous or fibrous structure;
- AO in folds, cups, sockets, and branch intersections;
- constrained biolum masks only where ecology/gameplay reason exists;
- no dense alpha-blend dependence;
- no random neon.

### Foam / Waterline

FoamRibbon RGB contract is static-accepted, but required packed foam/waterline source products are missing. Future derivation must enforce:

- tileable breakup;
- non-noisy shape control;
- clean mask packing;
- no muddy dirty-foam default for normal bright surface;
- storm dirty foam only as event-scoped material.

## Accepted Source-Channel Candidates

None for production PBR channel packing.

The packet accepts shader-channel contracts, not production source-channel candidates. Every actual source candidate is either rejected, pending source, or blocked by channel contract/owner route.

## Rejected Candidates

- `WetBasaltShoreline_1428` production PBR derivation: rejected until seam-fix and true channel sources exist.
- Environment route assets as ProductFace/body donors: rejected.
- Generic AI/UberNoir ARM maps: blocked, not accepted.
- Placeholder/default/package/flat material routes cited by Batch18: rejected by prior evidence and retained as rejection conditions here.

## Required Unity Owner Actions

- Do not import or bind any 1906 QA preview as a material texture.
- For WetBasaltShoreline: produce seam-fixed source and real normal/MRAO, then import with albedo sRGB, normal NormalMap/BC5 where applicable, packed mask linear, mips on.
- For ProductFace: choose per-family shader route before authoring packed maps.
- For ToolScreenDiegetic: bind real RT or authored fallback to `_ToolScreenTex`; do not use ignored declared texture slots as proof.
- For foam/waterline: bake the missing packed foam/waterline textures and prove active scene assignment.
- Capture Unity screenshots, material debug/channel previews, Frame Debugger/RenderGraph/profiler proof before upgrading any row above static evidence.

## Not Proven

PENDING UNITY OWNER:

- import policy;
- compression;
- sRGB/linear settings;
- normal map importer type;
- shader assignment;
- material preview;
- TerrainLayer or material slot binding;
- prefab relink;
- scene activation;
- runtime visual proof;
- Frame Debugger / RenderGraph proof;
- profiler proof;
- GC proof;
- VRAM/memory proof;
- player capture.

## Low / Middle / High / Ultra Consequences

- Low: correct material identity must survive at reduced resolution. No flat placeholders, no noir/darkness hiding, no false PBR channels.
- Middle: full albedo/normal/packed stack is required for accepted families, with roughness/AO/wetness logic by material.
- High: add richer normals, wetness, decals, scratches, and channel validation depth after source truth is locked.
- Ultra: add hero-resolution sources, denser detail/decal layers, richer preview/proof artifacts, and sensory overkill. Channel order, material semantics, gameplay truth, prefab authority, and save identity do not change.

## Validation Method

Static validation only:

- read required authority and mandate files;
- parsed relevant Batch18 CSV ledgers with `Import-Csv`;
- generated bounded Pillow QA preview under `Docs/GeneratedAssets/Gemini/QA/1906`;
- wrote contract matrix and final QA CSV;
- ran CSV parse/count checks after writing;
- ran forbidden output scan for `Assets/**` under owned 1906 paths;
- ran `git diff --check` on owned text outputs.

Result remains `STATIC VERIFIED` only for files, reports, contracts, CSV parseability, and generated QA previews.
