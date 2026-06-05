# Unity Owner Read-Only Progress Critique - ID 2023

Date: 2026-06-04
Scope: read-only critique for active Unity owner `Продолжить работу по логам`.
Evidence class: STATIC_DOC, STATIC_LOG, STATIC_PROCESS, STATIC_FILE, STATIC_IMAGE.
Unity/MCP/build status: not run.

## Authorities Read

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`
- `quality.md`, `rendering.md`, `water.md`, `terrain.md`, `world.md`, `3dmodel.md`, `PROCEDURAL_ASSET_PIPELINE.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `Docs/Orchestration/ORCHESTRATOR_DAY_20260604.md`, latest relevant Unity/visual sections by targeted search
- `Docs/Orchestration/UNITY_AGENT_STEER_20260604_VISUAL_SLOT.md`
- `Docs/Reports/Batch20/2018_UNITY_ACTIVE_OWNER_HEALTH_WATCH.md`
- `Docs/Reports/Batch20/2019_PRIMITIVE_PROXY_ART_DEBT_ELIMINATION_PLAN.md`
- `Docs/Reports/Batch20/UNITY_IMPORT_CHURN_READONLY_AUDIT_20260604.md`

`Docs/Actual Domains of Project.txt` was checked and is absent.

## Executive Call

Direct steer is warranted, but only as a light single message. Do not kill Unity, do not take over the editor, do not trigger builds, and do not ask for immediate interruption while imports may still be active.

Current Unity-owner risk level: HIGH for visual acceptance, MEDIUM-HIGH for import/capture churn.

Reason: log/process evidence shows active Unity/import/shader/MCP context and repeated scene/material imports. The latest relevant Unity visual screenshot is stale and already below the visual floor. Later imported Photic1453/1455 work has no current screenshot proof.

## FACT

1. Unity editor is running.
   Evidence: STATIC_PROCESS. Process snapshot found main `Unity.exe` PID 52264 with `-projectPath C:\hades\Hecton8 -logFile Docs\AgentLogs\UnityEditor_visual_audit_restart.log`.

2. Asset import workers are running.
   Evidence: STATIC_PROCESS. `Unity.exe` PIDs 19376 and 12164 are `AssetImportWorker0/1`.

3. Shader compiler helpers and ILPP are resident.
   Evidence: STATIC_PROCESS. Multiple `UnityShaderCompiler.exe` processes, `Unity.ILPP.Runner.exe`, and `UnwrapCL.exe` helpers were present.

4. No live `dotnet.exe`, `csc.exe`, `MSBuild.exe`, or Bee backend process was present in the inspected process snapshot.
   Evidence: STATIC_PROCESS. This does not prove future compile safety.

5. Latest Unity log is active relative to prior reports.
   Evidence: STATIC_FILE. `Docs/AgentLogs/UnityEditor_visual_audit_restart.log` last write observed at 2026-06-04 15:01:25, size 1,810,728 bytes.

6. Log tail shows repeated imports of `02_HECTON_WORLD.unity`, `Assets/_Project/Art/Materials/World/Photic1453/*`, `Assets/_Project/Art/Meshes/World/Photic1453/*`, `H8_UnderwaterSurfaceSheet_1455.shader`, and `MAT_H8_SurfaceCrestOcean_1428.mat`.
   Evidence: STATIC_LOG.

7. Log tail shows asset refreshes with `compile time=0 ms` during the final Photic1453/1455 import window.
   Evidence: STATIC_LOG. This reduces current compile-wall suspicion but does not clear import churn.

8. Earlier health evidence recorded one `Unexpected transport error from import worker 0 (possible crash). code=10054`.
   Evidence: STATIC_LOG via `2018_UNITY_ACTIVE_OWNER_HEALTH_WATCH.md`. This remains a risk item, not proof of current editor death.

9. Earlier health evidence recorded Bee compile churn: one `ExitCode: 4` followed by `ExitCode: 0`.
   Evidence: STATIC_LOG via `2018_UNITY_ACTIVE_OWNER_HEALTH_WATCH.md`. Current process scan found no live Bee process.

10. MCP capture route warnings exist.
    Evidence: STATIC_LOG. Repeated `Releasing render texture that is set as Camera.targetTexture!` stack traces through `MCPDynamicCode` and `MCPForUnity.Editor.Tools.ExecuteCode` were recorded earlier.

11. MCP dynamic code continued after the latest imports.
    Evidence: STATIC_LOG. Tail includes `H8_1457_REVERT_BAD_FOG: ocean fog 0.018/0.022/0.021, underwater factor .68 far .78, meniscus serialized on.` with an `MCPDynamicCode` stack.

12. Fresh acceptance screenshots are missing.
    Evidence: STATIC_FILE + STATIC_IMAGE. The latest relevant Unity visual capture found under `Docs/Orchestration/Captures` is `unity_focus_state_20260604_125701.png` at 12:57. Newer captures after 14:30 are Codex/Gemini/control-plane images, not Unity visual proof.

13. The 12:57 Unity capture is below the surface/photic visual floor.
    Evidence: STATIC_IMAGE. It shows grey/barren coast, dark-green flat ocean read, and Aegir as a translucent sky disc/sticker. This screenshot is stale after later imports and cannot judge current Photic1453/1455 results.

14. Generated names are mixed.
    Evidence: STATIC_FILE + STATIC_LOG. `HeroWetBoulder`, `HeroTubeCoralCluster`, `PhoticHeroTerrain`, `Left/RightPhoticReefWall`, and `SurfaceFoamLace` sound final-intended. `WorldProceduralProxy/MAT_family_*`, `MutedKelpAccent`, `MutedCoralAccent`, `Broken*Foam`, `VisibleFoamUnlit`, and low-byte haze/surface-sheet assets remain proxy-risk names until screenshots, material proof, LOD/collider proof, and placement gates exist.

15. Static prior reports still show production visual blockers.
    Evidence: STATIC_DOC. Batch20 reports cite active scene primitive mesh references, null material slots, dry-land kelp/coral/rock placement risk, placeholder/default material blockers, and missing current proof for waterline/photic/Aegir.

## RISK

1. Import-race risk remains active.
   Cause: repeated scene/material/shader imports and live AssetImportWorkers/ShaderCompilers. Interruption or extra scene/asset writes can extend churn.

2. Visual regression risk remains high.
   Cause: only inspected Unity shot is below floor, and no fresh capture proves later fixes.

3. False acceptance risk is high.
   Cause: asset names and import success are not render proof. Static docs/logs cannot prove Subnautica-level surface, water, Aegir, coastline, or photic shallows.

4. Proxy contamination risk remains high.
   Cause: `WorldProceduralProxy` materials are still imported; Batch20 found primitive/proxy scene debt and dry-land placement risks.

5. MCP screenshot route risk remains medium.
   Cause: prior render texture lifecycle warnings through MCP dynamic code. The active owner should use a fixed capture path and detach target textures before release.

6. It is not safe to interrupt by killing processes or stealing Unity ownership.
   Cause: Unity, AssetImportWorkers, ShaderCompilers, ILPP, UnwrapCL, and MCP transport are live. Static evidence does not prove a hang.

## STEER

Use one light steer only. The steer should not demand immediate Unity interruption. It should tell the active Unity owner to finish the current import/save burst, stop micro-saves/new MCP capture experiments until idle, and produce the minimum external proof captures before claiming progress.

Hard rejects to restate:

- land kelp;
- primitive rocks, cubes, spheres, planes, or primitive seaweed as visible route art;
- package/default materials in product-facing surface/photic routes;
- null materials;
- dry terrain scatter using underwater rules;
- darkness, fog, bloom, haze, or post-process hiding unfinished art;
- Aegir as stripe-ball, sticker, translucent disc, cyan rim, hard seam, or disconnected sky layer.

Minimum capture set if no fresh screenshots exist:

1. Game view surface/coast/water/Aegir, UI on.
2. Same Game view, UI off.
3. Matching Scene view from same position, gizmos off.
4. Shoreline close shot 1-2 m above water: foam, wet rock, shallow substrate, material breakup.
5. Underwater 0-5 m: surface underside, shore shallows, caustics/refraction, readable beauty.
6. Underwater 20-50 m photic route: route cue, particles/silt, terrain silhouettes, biota density.
7. Aegir long shot and crop: no rim/seam/sticker edge; atmospheric occlusion.
8. Regression angle for old white ocean quads/ribs or low-oblique ocean plane artifacts.

Save captures outside `Assets`, preferably `Docs/Orchestration/Captures` or `Docs/Screenshots`.

## Low / Middle / High / Ultra Consequences

Low: must preserve ocean color, route cues, wet material identity, and non-primitive silhouettes while reducing density/resolution/cadence only.

Middle: should show genuinely good waterline, terrain material breakup, photic density, and Aegir integration, not just absence of errors.

High: should spend headroom on richer shoreline material variation, better foam/contact detail, denser biota, and stronger atmosphere/water response.

Ultra: should add visual overkill in captures without changing gameplay truth: richer clouds/Aegir, longer LOD residency, denser particles/biota, stronger near-field material response.

No lane may use primitive/proxy finals, null/default materials, dry-land underwater scatter, or darkness as concealment.

## Verdict

Status: PENDING UNITY PROOF.

The active Unity owner appears unblocked but currently risky. Light steer is warranted now; process interruption is not warranted by evidence.
