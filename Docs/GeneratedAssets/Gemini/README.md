# Gemini Generated Asset Corpus

Status: `STATIC_SOURCE / STATIC_DOC / OPERATOR_QUEUE`.

This folder contains Gemini prompt packs, generated source references, operator queues, audit outputs, rejected candidates, and derived/refined texture source experiments. It is not a production import folder.

Subfolders and files here can support source selection only. They do not prove:

- Unity import into `Assets/**`.
- Texture importer settings, compression, packing, or mip configuration.
- Material, shader, terrain, water, sky, Addressables, or scene binding.
- Runtime texture readiness, residency, VRAM, memory, frame-time, GC, or platform proof.
- Visual acceptance for surface, photic shallows, coastline, terrain, ocean, Aegir, moons, clouds, or any route.

`Docs/GeneratedAssets/Gemini/Outputs/Batch22/` is an expected/future output path from `README_GENERATION_QUEUE_20260604.md`. It is not evidence that Batch22 generation occurred unless an explicit generation task creates files there with sidecar manifests and follow-up audit.

Current browser-generation queue:

- `Docs/GeneratedAssets/Gemini/README_GENERATION_QUEUE_20260606.md`
- `Docs/GeneratedAssets/Gemini/Batch30_PRIORITY_QUEUE_20260606.csv`
- `Docs/GeneratedAssets/Gemini/Prompts/Batch30/3001_GEMINI_ASSET_GENERATION_PROMPT_PACK_20260606.md`
- `Docs/GeneratedAssets/Gemini/Batch30_BROWSER_INTAKE_MANIFEST_TEMPLATE_20260606.md`
- expected output path: `Docs/GeneratedAssets/Gemini/Outputs/Batch30/`
- expected audit path: `Docs/GeneratedAssets/Gemini/Audit/Batch30/`

Batch30 targets current P0/P1 source gaps for foam/contact, photic terrain, Aegir/sky, oxygen HUD, flora/coral/kelp, caustic receiver, and deep biolum source material. It is browser/Gemini source generation only.

Current broad texture-source expansion queue:

- navigation: `Docs/GeneratedAssets/Gemini/Prompts/Batch34/3401_TEXTURE_SOURCE_EXPANSION_NAV_20260608.md`
- service-agent instructions: `Docs/GeneratedAssets/Gemini/Prompts/Batch34/3402_TEXTURE_SERVICE_AGENT_INSTRUCTIONS_20260608.md`
- prompt pack: `Docs/GeneratedAssets/Gemini/Prompts/Batch34/3401_TEXTURE_SOURCE_EXPANSION_PROMPT_PACK_20260608.md`
- direct submission part 1: `Docs/GeneratedAssets/Gemini/Prompts/Batch34/3403_TEXTURE_SOURCE_EXPANSION_DIRECT_PART1_25_20260608.md`
- direct submission part 2: `Docs/GeneratedAssets/Gemini/Prompts/Batch34/3404_TEXTURE_SOURCE_EXPANSION_DIRECT_PART2_25_20260608.md`
- direct submission validator: `Tools/ValidateBatch34DirectPromptQueue.py`
- targeted fix prompts: `Docs/GeneratedAssets/Gemini/Prompts/Batch34/3405_TEXTURE_SOURCE_FIX_PROMPTS_20260608.md`
- expected output path: `Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/`
- intake tool: `Tools/ProcessBatch34TextureExpansion.py`
- curation tool: `Tools/CurateBatch34TextureExpansion.py`
- intake manifest: `Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/QA/Batch34_TextureExpansion_IntakeManifest.json`
- intake summary: `Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/QA/Batch34_TextureExpansion_Intake.md`
- curation manifest: `Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/QA/Batch34_TextureExpansion_CurationManifest.json`
- curated import queue: `Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/QA/Batch34_TextureExpansion_UnityImportQueue.csv`
- curated ready sources: `Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/Curated/ReadyStatic/`
- local-only sources: `Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/Curated/LocalOnly/`
- needs-work sources: `Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/Curated/NeedsWork/`
- Unity material-pack promotion tool: `Tools/PromoteBatch34TextureExpansionToUnityPack.py`
- Unity material-pack manifest: `Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/GeminiMaterialAtlas_Manifest.json`
- Unity material-pack preview: `Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/PREVIEW_Batch20260608_TextureExpansion_Materials.png`
- Unity source-atlas promotion tool: `Tools/PromoteBatch34TextureExpansionSourceAtlases.py`
- Unity source-atlas importer: `Assets/_Project/Scripts/Editor/Batch34SourceAtlasImporter.cs`
- Unity source-atlas validator: `Tools/ValidateBatch34SourceAtlasPack.py`
- Unity source-atlas importer validator: `Tools/ValidateBatch34SourceAtlasImporter.py`
- Unity source-atlas manifest: `Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/GeminiBatch34SourceAtlases_Manifest.json`
- Unity source-atlas preview: `Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/PREVIEW_Batch34_SourceAtlases.png`
- source-atlas alpha candidate extractor: `Tools/ExtractBatch34SourceAtlasAlphaCandidates.py`
- source-atlas alpha candidate manifest: `Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/AlphaCandidates/Batch34_SourceAtlasAlphaCandidates_Manifest.json`
- source-atlas alpha candidate preview: `Docs/GeneratedAssets/Gemini/Outputs/Batch34_TextureExpansion/AlphaReview/PREVIEW_Batch34_SourceAtlasAlphaCandidates.png`
- Unity alpha-candidate promotion tool: `Tools/PromoteBatch34AlphaCandidatesToUnitySources.py`
- Unity alpha-candidate validator: `Tools/ValidateBatch34AlphaCandidatePack.py`
- Unity alpha-candidate manifest: `Assets/_Project/Art/TEXTURES/Generated/GeminiBatch34SourceAtlases_20260608/AlphaCandidates/GeminiBatch34AlphaCandidates_Manifest.json`
- Unity visor trauma decal array integrator: `Assets/_Project/Scripts/Editor/Batch34VisorTraumaDecalArrayIntegrator.cs`
- Unity visor trauma decal array route validator: `Tools/ValidateBatch34VisorTraumaDecalArrayRoute.py`
- Unity generated-material apply-all entry point: `Assets/_Project/Scripts/Editor/GeminiMaterialIntegrationApplier.cs`
- Unity generated-material apply-all runner: `Tools/RunGeminiMaterialUnityApplyAll.ps1`
- Unity generated-material apply-all runner validator: `Tools/ValidateGeminiUnityApplyRunner.py`
- generated-material static preflight: `Tools/RunGeminiMaterialStaticPreflight.ps1`
- live generated-material catalog builder: `Tools/BuildGeminiMaterialCatalog.py`
- live generated-material catalog: `Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialCatalog_20260608.json`
- live generated-material catalog alias: `Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialCatalog_Latest.json`
- live generated-material catalog doc: `Docs/GeneratedAssets/GeminiMaterialCatalog_20260608.md`

Batch34 adds 50 non-duplicate source prompts across terrain materials, hard-surface trim sheets, decals, flora/coral UV atlases, fauna UV atlases, and resource/salvage pickup atlases. Install the service-agent instructions before sending prompt jobs so the external generator uses consistent reference presets, output types, watermark handling, and reject rules. Batch34 explicitly avoids re-generating wet basalt, generic foam, and already-cataloged Gemini material families unless a later task proves a new role.

Batch34 intake result as of 2026-06-08: 50/50 sources processed, 0 missing, 0 hard source rejects. Curation result: 38 ready static/alpha sources, 4 local-only sources, 8 needs-work sources. Unity import/material binding remains pending and must start from the curated import queue, not from raw Downloads.

Batch34 Unity material-pack promotion result as of 2026-06-08: 16 material-capable curated sources promoted under `GeminiMaterialAtlases/Batch20260608_TextureExpansion/`, 34 non-material/local/needs-work sources intentionally skipped. The pack validates with `python -B Tools/ValidateExternalPbrPack.py --manifest Assets/_Project/Art/TEXTURES/Generated/GeminiMaterialAtlases/Batch20260608_TextureExpansion/GeminiMaterialAtlas_Manifest.json --min-size 1024`. Unity import is still `PENDING UNITY IMPORT`; existing `ExternalPbrTexturePackImporter` will discover this pack through its Gemini atlas manifest scan. `WorldProxyGeminiBiomeMaterialApplier` now loads old Gemini biome/single manifests plus all Gemini material atlas manifests and adds Batch34 assignments for landmark, safe/resource/hazard pockets, debris, ruin, service-scar, and route-power proxy materials. `Tools/ValidateWorldProxyGeminiBiomeAssignments.py`, `Tools/ValidateHeldToolExternalPbrRules.py`, and `Tools/ValidateWorldToolExternalPbrRules.py` use the same Gemini atlas manifest discovery route, so validation sees provider `Gemini_Batch20260608_TextureExpansion` without hardcoded follow-up edits.

Batch34 source-atlas promotion result as of 2026-06-08: 22 curated-ready DECAL/UV/PICKUP atlases promoted under `GeminiBatch34SourceAtlases_20260608/`, 28 non-ready/material/local sources intentionally skipped. Validate the promoted source pack with `python -B Tools/ValidateBatch34SourceAtlasPack.py` before split/alpha/material work. `Tools/ExtractBatch34SourceAtlasAlphaCandidates.py` produced RGBA matte-extraction candidates: 20 promoted into Unity-visible alpha-candidate sources, while `B34-3437` kelp holdfast and `B34-3449` industrial salvage small-parts are skipped as high-coverage rejects. `Assets/_Project/Scripts/Editor/Batch34SourceAtlasImporter.cs` imports these source-only atlases and alpha candidates with atlas-safe texture settings: `Clamp`, mipmaps, CompressedHQ, Standalone BC7, Android/iPhone ASTC 6x6, and no material creation. Alpha candidates import with `alphaIsTransparency=true`; base source atlases import with `alphaIsTransparency=false`. `Tools/ValidateBatch34SourceAtlasImporter.py --post-apply` verifies Unity-created `.meta` import settings after `Tools/RunGeminiMaterialUnityApplyAll.ps1` succeeds. These atlases are Unity-visible source textures only; do not auto-create Lit materials from them. They need split, padding, alpha extraction, mesh UV binding, or decal-owner binding before production use.

Generated-material catalog state as of 2026-06-08: `Tools/BuildGeminiMaterialCatalog.py` now discovers old Gemini singles, old Gemini biome materials, every `GeminiMaterialAtlases/**/GeminiMaterialAtlas_Manifest.json`, Batch34 source-atlas manifests, and promoted Batch34 alpha-candidate manifests. `Tools/RunGeminiMaterialStaticPreflight.ps1` rebuilds this catalog before state validation, then checks direct prompt queue shape, material manifests, binding semantics, held/world tool rules, world proxy assignments, construction assignments, flora-imported assignments, Batch34 source atlases, Batch34 alpha candidates, source-atlas importer wiring, and the single Unity apply-all runner contract. Current static preflight passes; Unity import/material apply remains `PENDING UNITY IMPORT`.

Visor/decal route as of 2026-06-08: `Batch34VisorTraumaDecalArrayIntegrator` bakes 16 promoted alpha candidates into the `Texture2DArray` expected by `Hecton8.Visor.DeferredDecalPass`, then binds it to `PC_Renderer.asset` and `PC_High_Renderer.asset` through `SerializedObject` in Unity. This route intentionally does not raw-edit renderer YAML. `Tools/ValidateBatch34VisorTraumaDecalArrayRoute.py` statically verifies slice IDs, promoted alpha source coverage, renderer feature presence, and apply-all wiring; `--post-apply` requires the baked array asset and non-empty renderer `decalAtlas` references.

Production routing remains outside this corpus:

- Prompt/source rules: `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- Texture packing and material readiness: `3DMODEL_TEXTURES_MATERIALS.md` and `Docs/ARCHITECTURE/ARM_TEXTURE_PACKING_PIPELINE.md`
- Unity/runtime/rendering proof: `PROCEDURAL_ASSET_PIPELINE.md`, `rendering.md`, `water.md`, `terrain.md`, and `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md`

Do not save Gemini outputs into `Assets/**` without an explicit intake/import task and the required proof chain.
