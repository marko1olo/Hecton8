# Antigravity Asset Front Static Triage

Status: STATIC VERIFIED
Evidence class: STATIC_DOC, STATIC_SOURCE
Runtime proof: absent

## Current Front
The visual state of the HECTON-8 project is rejected against the mandatory reference requirements in [OBYAZATELNYE PRIMERY PO KARTINKAM](file:///C:/hades/Hecton8/Docs/ОБЯЗАТЕЛЬНЫЕ%20ПРИМЕРЫ%20ПО%20КАРТИНКАМ): current surface water displays a repetitive green/teal procedural sheet, the coastline consists of crushed dark silhouettes with specular noise, the celestial giant Aegir lacks limb detail/atmospheric composition, and the shallow underwater fails to establish visible water ceiling transparency or biome asset density. In parallel, static validation of the asset-front remains at 10,323 rows across 31 CSV files, detailing extensive placeholder/proxy material contamination, missing PBR channel mappings, and built-in primitive mesh risks on final prefabs that must be resolved before in-editor object placement or visual proof can be accepted.

---

## Three Highest-Value Safe Local Actions While Unity/Build Blocked
1. **PBR Texture Channel Packaging and Manifesting**: Process generated source crop files in [Docs/GeneratedAssets/Batch31_LocalPBR/](file:///C:/hades/Hecton8/Docs/GeneratedAssets/Batch31_LocalPBR) (e.g. wet basalt, shell sand) to build isolated normal, height, and MRAO masks and write local JSON metadata definitions, preparing them for future Unity import.
2. **Material and Shader Reference Triage**: Scan material assets (e.g., [Mat_HectonSky.mat](file:///C:/hades/Hecton8/Assets/_Project/Art/Materials/Mat_HectonSky.mat)) to map null texture properties and unresolved GUID slots, separating stale/duplicate serialization rows from true missing texture assets.
3. **Local Audio Loudness and Duration Pre-Auditing**: Analyze source WAV files using local cmd decoding to calculate peak/mean loudness, identify short/critical voices vs. long music beds, and document format-policy exceptions to prevent main-thread latency and VRAM spikes.

---

## Three Actions Forbidden Until Gate Clean
1. **Unity Play Mode and Editor-Scene Mutation**: Entering Play Mode or executing scripts (such as the rejected [H8VisualProofCapture1912.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs) quarantine path) that alter and save the production scene [02_HECTON_WORLD.unity](file:///C:/hades/Hecton8/Assets/_Project/Scenes/02_HECTON_WORLD.unity).
2. **Dotnet Compilation and Assembly Builds**: Spawning `dotnet build`, `csc`, or package restore processes while high-CPU compile, shader-compiler, or asset-import worker tasks are active on the workstation.
3. **Direct Asset Import or Raw YAML Edits**: Importing uncompressed PNGs into `Assets/` or manually editing material/scene YAML to guess or force texture slots (such as Crest `_WD_*` wave-data parameters).

---

## First 20 Minutes Moment Affected
- **First Exit & Shallow Swim**: The visual failure of the ocean surface and the lack of transparent photic depth prevent the player from exiting the bathy-drop into a believable, beautiful, and alien underwater environment.
- **Resource Harvesting & Tool Interaction**: Placeholder proxy materials and primitive geometry block the visibility of starter nodes (e.g., kelp/copper), preventing the player from executing their first harvest or tool actions.
- **Save/Load State Preservation**: Scene quarantine modifications dirty the scene serialization, disrupting the validation of the save/load loop that must restore the player to a stable, identical route coordinate.

---

## Low/Middle/High/Ultra Consequences
- **Low (Continuous Weight 0.0)**: Must preserve readable ocean color, basic sky/Aegir composition, shoreline boundary, and core return cues. The current green sheet water and dark crushed silhouettes fail this floor.
- **Middle (Continuous Weight 0.5)**: Expected player experience. Adds wet rock specular breakup, a coherent sky/cloud structure, and sparse underwater particles to establish route readability.
- **High (Continuous Weight 0.8)**: Adds high-frequency normal detail, shoreline foam/contact masks, denser reef geology, and detailed cloud/Aegir bands to increase visual fidelity.
- **Ultra (Continuous Weight 1.0)**: Sensory overkill. Layers high-resolution source bakes, complex reflection/caustic overlays, and dense particle systems without changing gameplay truth or authority routes.

---

## Evidence Labels
- **STATIC_DOC**: Applied to all indexing, route bibles, and quality contract documents:
  - [AGENTS.md](file:///C:/hades/Hecton8/AGENTS.md)
  - [PROJECT_BIBLES.md](file:///C:/hades/Hecton8/PROJECT_BIBLES.md)
  - [TASTE.md](file:///C:/hades/Hecton8/TASTE.md)
  - [quality.md](file:///C:/hades/Hecton8/quality.md)
  - [VISION_LOCKS.md](file:///C:/hades/Hecton8/VISION_LOCKS.md)
  - [FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md](file:///C:/hades/Hecton8/Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md)
  - [ORCHESTRATOR_NIGHT_20260605.md](file:///C:/hades/Hecton8/Docs/Orchestration/ORCHESTRATOR_NIGHT_20260605.md)
- **STATIC_SOURCE**: Applied to static files, text-search patterns, meta files, and CSV audit assets:
  - [VISUAL_REFERENCE_REJECTION_20260605.md](file:///C:/hades/Hecton8/Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_REJECTION_20260605.md)
  - [ASSET_STATIC_VALIDATION_SUMMARY_20260605.md](file:///C:/hades/Hecton8/Docs/Reports/AssetSystem_20260605/ASSET_STATIC_VALIDATION_SUMMARY_20260605.md)
  - [ASSET_NEXT_ACTION_BOARD_20260605.csv](file:///C:/hades/Hecton8/Docs/AssetAudit/ASSET_NEXT_ACTION_BOARD_20260605.csv)
  - [QA_Evidence_Text_Filter_Audit.txt](file:///C:/hades/Hecton8/.agents-skills/QA_Evidence_Text_Filter_Audit.txt)
  - [OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt](file:///C:/hades/Hecton8/.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt)
  - [OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt](file:///C:/hades/Hecton8/.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt)
- **PENDING VERIFICATION**: Applied to all visual quality, runtime execution, Play Mode, GCMonitor, Frame Debugger, Memory Profiler, and player build claims.

---

## Exact Files Read and Commands Run
### Exact Files Read
- [AGENTS.md](file:///C:/hades/Hecton8/AGENTS.md) (lines 1 to 150)
- [PROJECT_BIBLES.md](file:///C:/hades/Hecton8/PROJECT_BIBLES.md) (lines 1 to 129)
- [TASTE.md](file:///C:/hades/Hecton8/TASTE.md) (lines 1 to 100)
- [quality.md](file:///C:/hades/Hecton8/quality.md) (lines 1 to 251)
- [VISION_LOCKS.md](file:///C:/hades/Hecton8/VISION_LOCKS.md) (lines 1 to 181)
- [FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md](file:///C:/hades/Hecton8/Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md) (lines 1 to 168)
- [ORCHESTRATOR_NIGHT_20260605.md](file:///C:/hades/Hecton8/Docs/Orchestration/ORCHESTRATOR_NIGHT_20260605.md) (lines 1 to 800, 3200 to 3578)
- [VISUAL_REFERENCE_REJECTION_20260605.md](file:///C:/hades/Hecton8/Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_REJECTION_20260605.md) (lines 1 to 129)
- [ASSET_STATIC_VALIDATION_SUMMARY_20260605.md](file:///C:/hades/Hecton8/Docs/Reports/AssetSystem_20260605/ASSET_STATIC_VALIDATION_SUMMARY_20260605.md) (lines 1 to 72)
- [ASSET_NEXT_ACTION_BOARD_20260605.csv](file:///C:/hades/Hecton8/Docs/AssetAudit/ASSET_NEXT_ACTION_BOARD_20260605.csv) (lines 1 to 13)
- [3101_UNITY_SCENE_DIFF_OWNER.md](file:///C:/hades/Hecton8/Docs/Reports/Batch31/3101_UNITY_SCENE_DIFF_OWNER.md) (lines 1 to 309)
- [3102_PROOF_HARNESS_1475_OWNER.md](file:///C:/hades/Hecton8/Docs/Reports/Batch31/3102_PROOF_HARNESS_1475_OWNER.md) (lines 1 to 200)
- [3103_WATER_CREST_FOAM_CAUSTIC_OWNER.md](file:///C:/hades/Hecton8/Docs/Reports/Batch31/3103_WATER_CREST_FOAM_CAUSTIC_OWNER.md) (lines 1 to 279)
- [3104_SHORELINE_TERRAIN_ASSET_OWNER.md](file:///C:/hades/Hecton8/Docs/Reports/Batch31/3104_SHORELINE_TERRAIN_ASSET_OWNER.md) (lines 1 to 392)
- [3105_AEGIR_SKY_CELESTIAL_OWNER.md](file:///C:/hades/Hecton8/Docs/Reports/Batch31/3105_AEGIR_SKY_CELESTIAL_OWNER.md) (lines 1 to 141)
- [Status_3103.md](file:///C:/hades/Hecton8/Docs/Tasks/Status_3103.md) (lines 1 to 53)
- [Status_3101.md](file:///C:/hades/Hecton8/Docs/Tasks/Status_3101.md) (lines 1 to 48)
- [POLISH.txt](file:///C:/hades/Hecton8/Docs/Tasks/POLISH.txt) (lines 1 to 157)
- [QA_Evidence_Text_Filter_Audit.txt](file:///C:/hades/Hecton8/.agents-skills/QA_Evidence_Text_Filter_Audit.txt) (lines 1 to 82)
- [OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt](file:///C:/hades/Hecton8/.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt) (lines 1 to 135)
- [OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt](file:///C:/hades/Hecton8/.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt) (lines 1 to 595)

### Exact Commands Run
- Directory listings (`list_dir`):
  - `C:\hades`
  - `C:\hades\Hecton8`
  - `C:\hades\Hecton8\Docs`
  - `C:\hades\Hecton8\taskslocal`
  - `C:\hades\Hecton8\taskslocal\batch31_night_visual_recovery`
  - `C:\hades\Hecton8\Docs\Tasks`
  - `C:\hades\Hecton8\Docs\ОБЯЗАТЕЛЬНЫЕ ПРИМЕРЫ ПО КАРТИНКАМ`
  - `C:\hades\Hecton8\Docs\Screenshots`
  - `C:\hades\Hecton8\Docs\Reports`
  - `C:\hades\Hecton8\Docs\Reports\Batch31`
  - `C:\hades\.tmp`
  - `C:\Users\danat\.gemini\tmp\danat\chats`
  - `C:\hades\Hecton8\Docs\Reports\Batch31\GeminiTextureIntakeAudit_batch31`
  - `C:\hades\Hecton8\.agent`
  - `C:\hades\Hecton8\.agents-skills`
- Check active permissions (`list_permissions`)
- Check Unity process state (`run_command` with command: `tasklist /FI "IMAGENAME eq Unity.exe"`)
- Introspect active terminals (`run_command` with script: `C:\Users\danat\.gemini\antigravity-ide\scratch\dump_terminal_text.ps1` and `C:\Users\danat\.gemini\antigravity-ide\scratch\read_terminals_v2.ps1`)
- Verify file presence under gate (`run_command` with script: `powershell -Command "Test-Path 'Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md'; Test-Path 'Docs/Orchestration/ORCHESTRATOR_NIGHT_20260605.md'; Test-Path 'Docs/Reports/AssetSystem_20260605/VISUAL_REFERENCE_REJECTION_20260605.md'; Test-Path 'Docs/Reports/AssetSystem_20260605/ASSET_STATIC_VALIDATION_SUMMARY_20260605.md'; Test-Path 'Docs/AssetAudit/ASSET_NEXT_ACTION_BOARD_20260605.csv'"`)
