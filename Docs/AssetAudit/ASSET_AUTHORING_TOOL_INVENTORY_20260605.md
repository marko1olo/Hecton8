# Asset Authoring Tool Inventory - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_SOURCE`, `STATIC_DOC`.
Scope: local editor/offline tools relevant to textures, materials, meshes, prefabs, music/audio, waveform/voice, and proof audits.

No tool in this inventory was executed in this pass. No Unity import, scene save, prefab edit, material edit, build, Play Mode, or Addressables build was run. Tool presence is not proof that generated assets are product-quality.

## Authority Inputs

- `PROCEDURAL_ASSET_PIPELINE.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- Asset-front mandates already listed in `Docs/AssetAudit/README.md`

## Primary Rule

Use these tools only through a named owner packet and fresh process gate. Editor tools that mutate assets require Unity idle, a scoped target list, a no-save/no-apply plan when readback-only, and a proof artifact path. Offline Python tools that write under `Docs/GeneratedAssets` are safer source-side paths, but they still do not produce final art by themselves.

## High-Value Tool Families

| Family | Tool Paths | Static Role | Current Use |
|---|---|---|---|
| texture_import_planning | `Tools/BatchImportTextures.py`; `Tools/TextureAuditAndBakeDirector_SHINOBU_361.py` | Generates dry-run import plans and static bake queues | Use after `TEXTURE_IMPORT_ROLE_MATRIX_20260605.csv`; do not write `.meta` without Unity-created metas and owner approval. |
| texture_source_qa | `Tools/GeminiTextureIntakeAudit.py`; `Tools/TextureSeamPeriodicRefiner.py`; `Tools/MaterialAudit.py` | Source QA, seam/tile refinement, material audit | Use for source packs under `Docs/GeneratedAssets`; direct `Assets` mutation remains blocked. |
| budget_static_audit | `Tools/MemoryBudgetCheck.py`; `Assets/_Project/Scripts/Editor/VRAMDictator.cs` | Static texture/mesh/RT pressure and VRAM redlines | Use to prioritize, not to claim runtime residency. |
| atlas_icon_bake | `Tools/IconBaker.py`; `Assets/_Project/Editor/Bakers/TextureAtlasPacker.cs` | UI/icon and texture atlas outputs | Needs owner route and import/atlas proof before HUD use. |
| procedural_texture_bake | `Assets/_Project/Editor/Bakers/ProceduralTextureBaker.cs`; `WreckageTextureBaker.cs`; `FaunaTextureBaker.cs`; `GeologicalStrataBaker1724.cs`; `CausticOpticsBaker1719.cs` | Unity editor texture baking | Mutating editor path. Run only after gate and scoped material family target. |
| sky_water_authoring | `Assets/_Project/Editor/HectonSkyAtlasGenerator.cs`; `Assets/_Project/Editor/ShorelineFoamGraftEditorTools.cs` | Sky atlas packing and shoreline foam layout/tuning | Relevant to P0 foam and P1 Aegir/sky; must not wrap/clone Crest materials. |
| flora_texture_material | `WorldProceduralFloraTextureAuthoring.cs`; `WorldProceduralFloraMaterialAuthoring.cs` | Flora texture generation/import fixes/material assignment | Relevant to P0 proxy flora contamination; editor mutation blocked until Unity owner. |
| mesh_clean_save | `HectonMeshGenerator.cs`; `HectonMeshSaver.cs`; `HectonMeshCleaner.cs`; `HectonPhysicsSkinGenerator.cs` | Mesh generation/saving/cleanup helpers | Editor-only; no final route without LOD/material/collider/proof manifest. |
| geology_final_authoring | `WorldProceduralGeologyFinalAuthoring.cs`; `WorldProceduralGeologyFinalValidator.cs`; `RockSculptorEngine1713.cs`; `AbyssalGeologyStudio1606.cs` | Offline geology finals and validators | Candidate route for basalt/geology, not direct active-route proof. |
| flora_coral_mesh | `WorldProceduralCoralMeshBuilder.cs`; `WorldProceduralSeaweedMeshBuilder.cs`; `WorldProceduralFloraFinalVariantAuthoring.cs`; `WorldProceduralFloraFinalVariantValidator.cs` | Coral/kelp/flora mesh and final variant route | Candidate replacement path for proxy flora, after material/import/LOD proof. |
| prefab_assembly | `PrefabAssemblerEngine.cs`; `FloraPrefabFactory.cs`; `FaunaPrefabFactory.cs`; `EquipmentPrefabFactory.cs`; `WreckagePrefabFactory.cs`; `WorldProceduralFinalPrefabQualityGate.cs` | Editor prefab assembly and quality gate | Never bypass with raw YAML; use only with scoped target and readback/proof. |
| audio_static_scan | `Tools/AudioClip_Reference_Scanner.py`; `Docs/Audio/audio_profile_usage_20260605.csv` generator provenance | Static audio route scan | Supports direct-ref discovery only. Does not prove mix/runtime behavior. |
| audio_waveform_voice | `Tools/BiolumWaveform.py`; `Tools/AudioSim.py`; `Tools/voice_baker.py` | Offline waveform/acoustic/voice bake lanes | Use only through audio owner; no final VO/mix proof from static output alone. |

## Current Asset-Front Use

- P0 foam/contact: use `ShorelineFoamGraftEditorTools` only after Unity owner verifies Crest/ocean material slots. Use source-side cleanup packs first; no runtime material wrapper.
- P0 proxy flora/coral/kelp: use flora material/mesh/final validators as replacement route candidates, not as proof. The active scene contamination remains until Unity readback and replacement proof exist.
- P1 Aegir/sky: use `HectonSkyAtlasGenerator` and sky/contact-sheet sources only after material slot readback. The current baked disc remains prototype-only.
- P1 terrain/geology: use geology authoring/validators and texture source QA only after route owner defines material role and proof target.
- P1/P2 UI: use `IconBaker` and atlas tools for source preparation only; HUD binding/readability proof remains separate.
- Audio/music: use static scanners/waveform tools to prioritize. MusicDirector mixer refs, direct Player prefab refs, and import-policy conflict remain owner blockers.

## Rejections

- Do not run editor-mutating tools from this controller without a named owner, clear target list, process gate, and proof plan.
- Do not use tool output as product proof without Unity readback, visual capture, import settings, memory/VRAM proof, and route ownership.
- Do not use generated mesh helpers to promote primitive/proxy pools into visible product content.
- Do not use Python source-side output under `Docs/GeneratedAssets` as imported final art.
- Do not run audio/voice bake tools to bypass placeholder VO, localization, subtitles, or mix proof.

CSV companion: `Docs/AssetAudit/ASSET_AUTHORING_TOOL_INVENTORY_20260605.csv`.

Final status: `PENDING VERIFICATION`.
