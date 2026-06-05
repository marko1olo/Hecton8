# 1891 AITexture Product-Face Hardening Packet

Agent: 1891  
Mode: REPORT_ONLY_STATIC_PIPELINE_PACKET  
Evidence class: STATIC_SOURCE / STATIC_DOC only  
Status: STATIC VERIFIED for source/report inspection; PENDING UNITY/PROFILER VERIFICATION for any editor execution, import, material mutation, prefab mutation, render cost, GC, or visual result.

No source, assets, prefabs, scenes, binaries, generated meshes, task files, `.meta`, or DataMonolith files were edited.

## Authority Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `Docs/Reports/Batch18/1886_PRODUCT_FACE_TEXTURE_AUTHORING_PIPELINE_DISCOVERY.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/DATA_Inventory_Resources_Items_SOA_Layout.txt`

`Docs/Actual Domains of Project.txt` was checked and was absent. Narrow inferred domain: product-face AI/procedural texture ingestion, material contract validation, and prefab relink containment.

## Static Source Inventory

Targeted source root: `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269`.

| Concern | Static owner | File | Methods/classes |
|---|---|---|---|
| Constants and output paths | `AITextureControlMapConstants` | `AITextureControlMapBakerTypes.cs` | `ImportedTextureFolder`, `ImportedMaterialFolder`, `PrefabBindingManifestPath`, `PrefabBindingReportPath`, `ProfileCsvPath`, `BakeBlackBoxDumpPath` |
| Texture import hook | `AITextureImportPostprocessor` | `AITextureImportAndMaterialPipeline.cs` | `OnPreprocessTexture`, `OnPostprocessTexture` |
| Import settings | `AITextureImportPolicy` | `AITextureImportAndMaterialPipeline.cs` | `IsManagedAiTexture`, `ClassifyMapKind`, `BuildConfig`, `Apply`, `SelectMaxSize` |
| Post-import side effects | `AITexturePostImportDrain` | `AITextureImportAndMaterialPipeline.cs` | `Enqueue`, `DrainPendingPostImports` |
| Material creation/binding | `AITextureMaterialBinder` | `AITextureImportAndMaterialPipeline.cs` | `BindTexture`, `AssignTexture`, `SetTextureIfPossible`, `BuildAssetKey` |
| Prefab material-slot mutation | `AITextureMaterialBinder` | `AITextureImportAndMaterialPipeline.cs` | `AssignMaterialFromManifest`, `ApplyMaterialToPrefab`, `ParseManifestRow`, `WritePrefabBindingReport` |
| Rollback/presentation labels | `AITextureRollbackFence` | `AITextureImportAndMaterialPipeline.cs` | `MarkAsset`, `WriteExclusionReport` |
| Import and bake reports | `AITexturePipelineReport` | `AITextureImportAndMaterialPipeline.cs` | `WriteBakeReport`, `WriteIngestionReport` |
| Inbox ingestion | `AITextureIngestionWatcher` | `AITextureIngestionWatcher.cs` | `StartWatcher`, `ProcessInboxNow`, `DrainPendingImports`, `CopyIntoProjectIfReady` |
| CSV profiles | `AITextureProfileCsv` | `AITextureProfileCsv.cs` | `TrySelectProfileForAsset`, `TryParseFirstProfileFromCsv`, `WriteDefaultCsvIfMissing` |
| Control-map baking | `AITextureControlMapBaker` | `AITextureControlMapBaker.cs` | `BakeFolder`, `BakeMeshes`, `BakePass`, `ProcessReadbackCompletion`, `WritePngAsync`, `BuildTelemetry` |
| Forge UI entry point | `AITextureForgeWindow` | `AITextureForgeWindow.cs` | `RunBake`, `LoadCsvProfile`, `BuildSettings`, buttons for inbox/material scan/audit |
| Material scanner | `Material_Setup_Scanner` | `Material_Setup_Scanner.cs` | `RunScan`, `FindAlbedoTexture`, `MergeIntoSharedRenderingReport` |
| Black-box telemetry | `AITextureBakeBlackBox` | `AITextureBakeBlackBox.cs` | `Record`, `Dump`, `Ensure`, `WriteEntry` |
| Static self-audit | `AITextureControlMapSelfAudit` | `AITextureControlMapSelfAudit.cs` | `WriteSelfAuditReport` |
| Legacy archaeology | `AITexturePipelineArchaeology` | `AITexturePipelineArchaeology.cs` | `RunFromMenu`, `Scan`, `WriteReport` |
| Scene preview | `AITextureLiveMapPreview` | `AITextureLiveMapPreview.cs` | `SetPreview`, `DrawPreview`, `LocateMeshForPreview` |

## Current Behavior

`AITextureControlMapBaker/Shinobu269` is reusable as an editor-only ingestion route. It imports AI texture files under `Assets/_Project/Textures/AI_Texturing`, classifies maps from filename tokens, applies texture import settings, creates/updates a material under `Assets/_Project/Materials/AI_Texturing`, binds albedo/normal/ARM-like maps to `Hecton8/Rendering/UberNoir`, writes reports, and records bake black-box telemetry.

Static evidence also shows a dangerous product-face boundary: if `Assets/_Project/Data/AITexturing/ai_texture_prefab_bindings.csv` exists and a row matches `assetKey,prefabPath,rendererPath,materialSlot`, `AITextureMaterialBinder.AssignMaterialFromManifest` can call `PrefabUtility.LoadPrefabContents`, replace `renderer.sharedMaterials[materialSlot]`, and call `PrefabUtility.SaveAsPrefabAsset`. This is correctly narrow for a generic manifest, but not safe enough for product-face hardening because it can still bind broadly into `Assets/_Project/Prefabs` before the product-face relink owner slot and channel contract are proven.

## Product-Face Risk

1. Broad prefab mutation risk: a generic CSV can relink any prefab under `Assets/_Project/Prefabs` if renderer path and material slot are supplied.
2. Channel ambiguity risk: source code classifies `_arm`, `_mask`, and `_packed` as `AITextureMapKind.Arm`, then binds to `_ArmMap`, `_MaskMap`, and `_MetallicGlossMap`. Prior discovery found shader dialects using ARM, MRAO, ORM, and Packed Mask V1. Product-face cannot treat those as interchangeable.
3. Material overreach risk: ingestion always creates `MAT_[assetKey]_UberNoir.mat` unless shader missing. Product-face resources/tools/transport/player suit need category-specific shader and channel manifests, not default broad UberNoir binding.
4. Inbox overwrite risk: `AITextureIngestionWatcher.CopyIntoProjectIfReady` copies by filename into the imported texture folder with overwrite enabled. Product-face needs immutable manifest IDs and collision rejection, not filename replacement.
5. Report proof risk: existing reports are static/editor tool reports. They are not visual, prefab, runtime, profiler, or product acceptance proof.

## Product-Face Manifest Format

Future implementation should introduce a product-face-specific manifest and refuse the generic prefab-binding CSV for product-face assets.

Recommended path:

- `Assets/_Project/Data/ProductFace/TextureIngestion/product_face_texture_manifest.csv`
- Optional generated static validation report: `Docs/Reports/ProductFace/PRODUCT_FACE_TEXTURE_MANIFEST_VALIDATION.json`

Required CSV columns:

```csv
product_face_id,category,part_id,owner_route,source_texture_path,imported_texture_path,material_path,shader_path,map_role,packed_layout,channel_r,channel_g,channel_b,channel_a,srgb,normal_import,compression_standalone,compression_android,max_size,global_quality_weight,allowed_prefab_path,allowed_renderer_path,allowed_material_slot,relink_owner,status
```

Required rules:

- `product_face_id`: stable FNV-1a/hash-backed ID; no runtime string authority.
- `category`: one of `resource`, `tool`, `transport`, `player_suit`, `visor`, `organic`, `glass`, `rubber`, `metal`, `fabric`, `mineral`.
- `map_role`: one of `Albedo`, `Normal`, `MRAO`, `ARM`, `ORM`, `Emission`, `Detail`, `HeightSource`, `ControlCurvature`, `ControlDepth`, `ControlColorID`.
- `packed_layout`: explicit shader dialect, e.g. `MRAO_RMetallic_GRoughness_BAO_AEmission`, `ARM_ROcclusion_GRoughness_BMetallic_AFamily`, `ORM_ROcclusion_GRoughness_BMetallic_AEmission`, `PackedV1_RMetallic_GOcclusion_BSmoothness_AEmission`.
- `relink_owner`: required for any prefab slot use. Accept only a named product-face relink owner, not the generic AI texture binder.
- `status`: `SOURCE_ONLY`, `IMPORTED_STATIC_VALIDATED`, `MATERIAL_STATIC_VALIDATED`, `RELINK_READY`, `REJECTED`, or `PENDING_UNITY_VERIFICATION`.

Folder layout:

```text
Docs/AI_Texturing_ProductFace/Templates/
Docs/AI_Texturing_ProductFace/Inbox/
Docs/Reports/ProductFace/
Assets/_Project/Textures/ProductFace/{Resources|Tools|Transport|PlayerSuit|Shared}/
Assets/_Project/Materials/ProductFace/{Resources|Tools|Transport|PlayerSuit|Shared}/
Assets/_Project/Data/ProductFace/TextureIngestion/
```

The existing generic folders remain valid for Shinobu269 diagnostics. Product-face production assets should not be placed in `Assets/_Project/Textures/AI_Texturing` or `Assets/_Project/Materials/AI_Texturing` without a migration gate.

## Import Without Prefab Editing

Future product-face import should be two-phase:

1. Source/import phase: copy or import texture files into `Assets/_Project/Textures/ProductFace/...`; apply import settings; label assets; write ingestion report; do not create or mutate prefabs.
2. Material-validation phase: create/update only product-face material assets under `Assets/_Project/Materials/ProductFace/...`; bind maps only when the manifest declares shader path and packed layout; write material validation report.

Prefab relink must be a separate owner phase:

- Reads only `product_face_texture_manifest.csv` rows with `status=RELINK_READY`.
- Requires `allowed_prefab_path`, `allowed_renderer_path`, and `allowed_material_slot`.
- Requires `relink_owner=ProductFaceRelinkOwner` or stricter domain owner token.
- Rejects missing or wildcard renderer paths.
- Rejects any row that targets multiple prefabs or says "all renderers".
- Writes a dry-run diff report before any prefab save.
- Saves prefab only after the product-face relink owner validates mesh/material slot semantics against route artifacts.

## Required Validator Rejections

Validators must reject:

- any product-face texture using the generic `AITextureControlMapConstants.PrefabBindingManifestPath`;
- any prefab write from `AITextureMaterialBinder.AssignMaterialFromManifest` for product-face categories;
- wildcard prefab paths, folder-wide prefab binding, empty renderer paths, negative material slots, and material slots outside the renderer array;
- `_arm`, `_mask`, `_packed`, `MRAO`, `ARM`, or `ORM` maps without explicit channel contract;
- albedo imported with `sRGB=false`;
- normal imported without `TextureImporterType.NormalMap`;
- packed mask imported as sRGB;
- missing mipmaps for world/product-face textures;
- `renderer.material` or runtime material clone use;
- missing `GlobalQualityWeight` consequences;
- default/fallback material use as product-face evidence;
- `UberNoir` material binding when category shader contract requires `ToolDecayLit`, visor shader, glass route, bio route, or another declared shader.

## Future Touch Scope

Future implementation may touch only after an explicit implementation task:

- `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureImportAndMaterialPipeline.cs`
- `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureIngestionWatcher.cs`
- `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/AITextureProfileCsv.cs`
- `Assets/_Project/Scripts/Editor/AITextureControlMapBaker/Shinobu269/Material_Setup_Scanner.cs`
- New editor-only product-face validator/relink source under `Assets/_Project/Scripts/Editor/ProductFace/`
- New product-face manifest assets under `Assets/_Project/Data/ProductFace/TextureIngestion/`
- New reports under `Docs/Reports/ProductFace/`

Future implementation must not touch without a separate scoped task and proof:

- Existing product prefabs under `Assets/_Project/Prefabs/**`
- Scenes
- Runtime code
- DataMonolith
- Generated meshes
- `.meta`
- Task files
- Crest/MapMagic materials
- Sky/ocean/coast/terrain route-owned materials
- Third-party folders
- `Assets/_Project/Data/AITexturing/ai_texture_prefab_bindings.csv` for product-face relink

## Guardrail Contract

Exact future guardrails:

1. Product-face ingestion must use a product-face manifest, not `ai_texture_prefab_bindings.csv`.
2. Import may create/update texture assets and product-face material assets only; prefab writes are forbidden in the import phase.
3. Any prefab material slot mutation must be owned by a product-face relink owner with a dry-run report first.
4. Packed texture maps must declare exact MRAO/ARM/ORM/PackedV1 channel meanings before import or material binding.
5. Filename tokens may classify map role only as a hint; manifest channel contract is the authority.
6. `GlobalQualityWeight` may scale texture size, validation sample count, detail density, and optional reports; it must not alter material role semantics, product identity, prefab authority, DTO layout, or save/runtime truth.
7. AI/procedural textures with baked lighting, fake shadows, perspective, text, watermark, blurred mud, random noise, or false PBR channels must be rejected before material binding.
8. Black-box telemetry remains required for bake/import failures; product-face implementation should use a distinct dump path such as `Docs/AgentLogs/Dump_ProductFaceTextureIngestion_[timestamp].bin` unless an explicit agent ID owns the run.

## Low / Middle / High / Ultra Consequences

- Low: compact lane may use 512-1024 imported texture sizes and shared detail maps, but still requires albedo, normal, and packed mask truth. No flat color shell substitution.
- Middle: category-specific resources/tools/transport/player suit materials require full albedo/normal/packed maps and correct shader layout.
- High: stronger normals, wear/scratch/wetness maps, better decal density, and stricter channel validation can be enabled without changing prefab/material ownership.
- Ultra: hero-only 2048/4096 source maps, richer control maps, additional preview captures, and forensic reports are allowed. Runtime truth and prefab authority do not change.

## Verification Results

Commands required by task were run after writing this packet and the CSV/status/rationale/log files.

- `git diff --check -- Docs/Reports/Batch18/1891_AITEXTURE_PRODUCT_FACE_HARDENING_PACKET.md Docs/Reports/Batch18/1891_AITEXTURE_PRODUCT_FACE_HARDENING_MATRIX.csv Docs/Tasks/Status_1891.md Docs/AgentLogs/Rationale_1891.md Docs/AgentLogs/LOG_1891.md`: exit 0, no output.
- `Import-Csv Docs/Reports/Batch18/1891_AITEXTURE_PRODUCT_FACE_HARDENING_MATRIX.csv | Measure-Object`: Count 12.
- Static term cross-check across the five owned files:
  - `AITextureControlMapBaker`: present
  - `ProductFace`: present
  - `manifest`: present
  - `import`: present
  - `material`: present
  - `prefab`: present
  - `black-box`: present
  - `MRAO`: present
  - `ARM`: present

## Highest Code Risk Before Implementation

Highest risk: `AITextureMaterialBinder.AssignMaterialFromManifest` and `ApplyMaterialToPrefab` already contain a working prefab save path. Product-face implementation must not extend that path directly. It needs a product-face manifest validator and a separate relink owner that defaults to dry-run and rejects broad binding until channel contracts and owner slots are proven.
