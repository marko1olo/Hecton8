# Unity Import Churn Read-Only Audit - 2026-06-04

Status: STATIC VERIFIED / RISK ACTIVE
Evidence class: STATIC_SOURCE, STATIC_LOG, GIT_STATUS
Scope: read-only audit of `02_HECTON_WORLD.unity`, Photic1428 assets, and `H8_PhoticWaterVolume_1429.shader`.

No Unity commands, MCP tools, builds, Asset edits, or scene edits were run.

## Authority Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `Docs/README.md`
- `streaming.md`, `shaders.md`, `performance.md`, `authoring.md`, `quality.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/REND_Shader_Stutter_Linux_Vulkan.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Verdict

This looks like active Unity scene/art authoring churn, not a proven infinite import loop. It is still unsafe to steer scene or Asset writes now.

Reason: the audit observed timestamps advancing during the read-only pass. `Docs/AgentLogs/UnityEditor_visual_audit_restart.log` moved from 13:03:33 to 13:08:08, `Assets/_Project/Scenes/02_HECTON_WORLD.unity` moved to 13:08:08, and new Photic1428 mesh/material assets moved to 13:08:07. The scene import count also increased to 17 hits by the final snapshot.

Safe action now: read-only review and coordination only.
Unsafe action now: editing `02_HECTON_WORLD.unity`, Photic1428 assets, shader/material assets, or any import-sensitive generated asset until the Unity owner declares idle or file timestamps stop moving.

## Evidence

Primary log:

- `Docs/AgentLogs/UnityEditor_visual_audit_restart.log`
- Final observed size: 1,626,281 bytes
- Final observed timestamp: 2026-06-04 13:08:08

Scene file:

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- Git status: modified
- Final observed size: 2,431,146 bytes
- Final observed timestamp: 2026-06-04 13:08:08

Repeated scene imports:

- 17 total `02_HECTON_WORLD.unity` import hits in the editor log.
- First hit: line 16099, worker import.
- Late repeated imports include lines 17819, 17857, 17866, 17878, 17886, 17942, 18079, 18368, 18398, 18494, 18594, 18612, 18677, 18735, 18788, 18849.
- Worker import evidence: `Logs/AssetImportWorker1.log` line 442 imports `02_HECTON_WORLD.unity` in 0.1752479 seconds with static and dynamic dependencies.
- Late editor scene imports are cheap individually: about 0.00094 to 0.00242 seconds, static dependencies only.
- The repeated imports are coupled to short Asset Pipeline refreshes, commonly 0.167 to 0.468 seconds in the late window.

Photic asset imports:

- 111 import hits for `Assets/_Project/Art/(Meshes|Materials)/World/Photic1428/`.
- 59 unique non-meta Photic1428 mesh/material paths were seen in import logs.
- Import span in log: lines 18164 to 18846.
- `H8_PhoticWaterVolume_1429.shader` import: line 18403, 0.0086241 seconds, static and dynamic dependencies.

Current Photic file footprint:

- `Assets/_Project/Art/Meshes/World/Photic1428`: 72 files, 36 non-meta assets, 5,232,867 bytes, newest write 2026-06-04 13:08:07.
- `Assets/_Project/Art/Materials/World/Photic1428`: 46 files, 23 non-meta materials, 77,476 bytes, newest write 2026-06-04 13:08:07.
- `Assets/_Project/Art/Shaders/H8_PhoticWaterVolume_1429.shader`: 4,217 bytes, newest write 2026-06-04 12:37:19.
- `Assets/_Project/Art/Shaders/H8_PhoticWaterVolume_1429.shader.meta`: present and untracked.

Newest Photic files observed:

- `Assets/_Project/Art/Meshes/World/Photic1428/MESH_H8_BrokenShallowBreaker_1434.asset`
- `Assets/_Project/Art/Meshes/World/Photic1428/MESH_H8_BrokenShoreFoam_Inner_1434.asset`
- `Assets/_Project/Art/Meshes/World/Photic1428/MESH_H8_BrokenShoreFoam_Outer_1434.asset`
- Corresponding `.meta` files exist.
- All three are untracked and timestamped 2026-06-04 13:08:07.

Git status highlights:

- Modified: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- Untracked: `Assets/_Project/Art/Meshes/World/Photic1428/`
- Untracked: `Assets/_Project/Art/Materials/World/Photic1428/`
- Untracked: `Assets/_Project/Art/Shaders/H8_PhoticWaterVolume_1429.shader`
- Untracked: `Assets/_Project/Art/Shaders/H8_PhoticWaterVolume_1429.shader.meta`
- Untracked: `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png`
- Relevant path status count: 123 status lines for the scoped scene/Photic/shader/texture paths.

Other tracked shader files are modified under `Assets/_Project/Art/Shaders/`, including Aegir, cloud, visor, fog, decal, storm ocean, and sky shaders. This widens shader-import risk but was not audited beyond git status.

## Compile And Domain Reload

Current Photic/import window:

- After line 18580, the editor log reports `Scripting: domain reloads=0, domain reload time=0 ms, compile time=0 ms` at lines 18624, 18690, 18748, 18796, and 18857.
- No script compile or domain reload is evidenced in the final Photic scene-import window.

Earlier in the same editor session:

- Script compilation and domain reloads did occur before the current Photic window.
- Examples: initial refresh compile and reload around lines 126-143 and 262-2090; reloads around lines 16770-16990 and 17435-17599.
- That earlier compile history does not prove the current churn is compile-driven.

AssetImportWorker logs:

- `Logs/AssetImportWorker0.log`: 122,902 bytes, timestamp 2026-06-04 11:35:32.
- `Logs/AssetImportWorker1.log`: 116,921 bytes, timestamp 2026-06-04 11:35:32.
- These worker logs are stale relative to the 13:08 editor log, but Worker1 contains the initial dynamic scene import.

## Postprocessor Cost Risk

Late import refreshes are not expensive because each asset import is slow; they are expensive because repeated refreshes trigger postprocessors.

Observed late-window postprocessor samples:

- `PostProcessAllAssets`: 105.773 ms, 110.950 ms, 170.689 ms, 179.769 ms, 181.202 ms.
- `TextureArrayPreProcessor.OnPostprocessAllAssets`: 98.710 ms, 102.473 ms, 162.853 ms, 168.575 ms, 169.230 ms.
- `MaterialPostprocessor.OnPostprocessAllAssets`: 0.056 ms to 2.308 ms in sampled late refreshes.
- `ShaderGraphAssetPostProcessor.OnPostprocessAllAssets`: 0.162 ms to 1.107 ms in sampled late refreshes.

Risk: repeated small Photic/material/scene writes can accumulate hundreds of milliseconds of editor-side postprocessor work. This is editor/import churn evidence, not runtime profiler evidence.

## Screenshots In Assets

Recent image scan under `Assets` found one new/untracked PNG:

- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png`
- Size: 3,073,068 bytes
- Timestamp: 2026-06-04 11:43:44

The name reads as a terrain texture, not a screenshot or proof capture. No recent screenshot-like PNG/JPG/WEBP artifact was found in the targeted image scan. Risk remains if visual-proof agents write future capture output under `Assets`; proof captures belong in `Docs`, not `Assets`.

## Risk Call

Import loop:

- Not proven.
- Repeated scene imports and Photic asset imports are proven.
- Because file timestamps advanced during this audit, active churn is proven.

Scene churn:

- High risk. `02_HECTON_WORLD.unity` is actively modified and repeatedly imported.

Shader/material churn:

- Medium to high risk. `H8_PhoticWaterVolume_1429.shader` is new/untracked, multiple tracked shaders are modified, and postprocessors dominate refresh cost.

Screenshots in Assets:

- Not observed in current targeted scan.
- One recent untracked terrain PNG exists in `Assets`; it should be treated as production texture candidate only after normal asset validation, not as proof capture.

Steering decision:

- Wait before issuing scene/Asset edits.
- Coordinate with the Unity owner and require an idle window or explicit handoff.
- Static steering, report review, or task sequencing is safe.

## No Runtime Confirmation

No Unity Editor command, MCP command, build, Play Mode, profiler, Frame Debugger, Memory Profiler, or screenshot capture was run. All claims above are static log/git/timestamp evidence only.
