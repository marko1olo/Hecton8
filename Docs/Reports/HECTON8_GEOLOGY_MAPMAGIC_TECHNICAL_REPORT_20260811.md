# HECTON-8 Geology & MapMagic Pipeline Technical Audit & Handoff Report

**Date**: 2026-08-11  
**Project**: HECTON-8 (NASA-Punk / Deep Sea Noir 3D Unity Game)  
**Status**: VERIFIED & DOCUMENTED  
**Source Log File**: `C:\Users\Admin\Documents\Без имени.txt`  

---

## Direct Authority Quote & Intake Compliance

> "Automated test runners and scripts are strictly forbidden from calling `EditorSceneManager.SaveScene`, `PrefabUtility.SaveAsPrefabAsset`, or `EditorUtility.SetDirty` on production assets to prevent wiping level-designer changes. Any runtime adjustments must occur in-memory only." (`AGENTS.md:126`)

> "Running MapMagic/Compute Shader generation tests with `-nographics` in batchmode is strictly banned... Use state-machine polling via `EditorApplication.update` to wait for stable frames (Terrain length == 9, alphamaps loaded, active TerrainCollider on all chunks) and at least 200+ frames of complete silence before capturing diagnostic renders or screenshots." (`AGENTS.md:130`)

---

## 1. Executive Overview

This report documents the full investigation, root-cause diagnosis, code fixes, and compliance checks performed across HECTON-8 terrain geology and MapMagic generation pipelines (as logged in `C:\Users\Admin\Documents\Без имени.txt`).

All critical engineering defects related to batchmode generation hangs, diagnostic exit-code traps, graph topology verification, scene preservation rules, and slope-scatter safety have been investigated, resolved, and verified against `AGENTS.md` mandates.

---

## 2. Technical Findings & Root Cause Analysis

### 2.1. MapMagic Batchmode Hang (`IsGenerating() == true` Deadlock)

* **Root Cause**: `MapMagicObject.cs:160` contained a legacy guard:
  ```csharp
  if (UnityEngine.Application.isBatchMode)
      return;
  ```
  This guard was placed directly above `CoroutineManager.Update()`. In Unity `-batchmode`, `CoroutineManager.Update()` was skipped every frame.
* **Mechanism**:
  1. `TerrainTile.cs:765,770` enqueues terrain `ApplyNow`/`ApplyRoutine` tasks into `Den.Tools.Tasks.CoroutineManager`.
  2. `TerrainTile.cs:791,848` sets `applyReady = true` ONLY inside those coroutines.
  3. `TerrainTile.cs:899` evaluates `IsGenerating` as `generateStarted && !applyReady`.
  4. Because `CoroutineManager.Update()` was bypassed under batchmode, `applyReady` remained `false` forever.
* **Resolution**:
  - `GraphOutputRenderer.cs` was updated to explicitly pump `Den.Tools.Tasks.CoroutineManager.Update()` inside `EditorApplication.update` and during wait polling.
  - Set `globals.heightMainApply` to `ApplyType.SetHeights` (CPU path), bypassing the GPU blit fatal that the batchmode guard originally aimed to conceal.
  - Generation settled successfully in **83.8s – 99.3s** with **220 consecutive quiet frames**, exiting cleanly with `IsGenerating == false`.

### 2.2. Unity Batchmode Exit Code 0 Fallacy

* **Discovery**: In Unity 6000.5.0f1 `-batchmode`, when C# compilation fails (`error CS`), the Unity process terminates with **Exit Code 0** without executing `-executeMethod` or writing any logs or output files.
* **Impact**: External test runners checking `$LASTEXITCODE == 0` misinterpret compilation crashes as success.
* **Rule Mandatory**: All automated scripts and diagnostic invocations MUST verify `error CS` count in logs AND verify physical existence of generated artifacts on disk before declaring `VERIFIED`.

### 2.3. Scene Integrity & AGENTS.md:126 Firewall Rule Violation

* **Incident**: Invoking `CreateSandboxV2.cs` executed `EditorSceneManager.SaveScene`, overwriting the baked 5 MB scene `020_RENDER_SANDBOX_V2.unity` (containing 10 authored `TerrainTiles` & `TerrainData`) with a 10 KB empty scene.
* **Resolution**:
  - `020_RENDER_SANDBOX_V2.unity` was immediately restored from Git HEAD (`git checkout HEAD -- Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity`), returning size to exact bitwise 4,908 KB.
  - User confirmed that sandbox scenes are procedural generator entrypoints and authorized in-memory mutations.
  - Automated runners are prohibited from invoking `SaveScene` on level-authored scenes per `AGENTS.md:126`.

### 2.4. Scatter Slope Calculation Audit

* **Audit**: Evaluated whether procedural scatter candidate evaluation reads a `Slope01` value capped at 51.3° (which would cause rocks/props to spawn incorrectly on vertical walls).
* **Finding**: `WorldProceduralScatterDirector.cs` and `WorldProceduralFieldSampler.cs` calculate `slopeDegrees` directly from the raw un-clamped gradient using:
  ```csharp
  float slopeDegrees = math.degrees(MathLodApproximation.ApproxAtanFast(gradient));
  ```
* **Verdict**: **SAFE**. Scatter rules evaluate raw mathematical slope up to 90°. `Slope01` capping is isolated to material blending contracts and does not pollute scatter placement logic.

### 2.5. Geology Graph Topology & Height Output

* **Graph File**: `Assets/_Project/Data/World/Sandbox/HECTON_PROCEDURAL_GEOLOGY_GRAPH.asset`
* **Topology Chain**: `HectonSandboxAbyssalShelfMapMagicNode` $\rightarrow$ `HectonBiomeMatrixMapMagicPostProcessNode` $\rightarrow$ `HeightOutput200`.
* **Diagnostics**:
  - `DynamicGraphAuditorTask.cs` confirmed all 5 nodes and 9 inlet links are validly connected.
  - `DumpRawHeightsTask.cs` fixed `System.Environment` namespace shadowing (`global::System.Environment.GetCommandLineArgs()`) and handled null products gracefully with diagnostic exception messages.
  - Vertical span checks: Graph authored span = 6,000m (`highWorldY = 1000`, `lowWorldY = -5000`), matching `WorldVerticalExtentContracts.cs`.

---

## 3. Summary of Code & Asset Modifications

| File | Status | Description |
|---|---|---|
| [`Assets/_Project/Scripts/Editor/GraphOutputRenderer.cs`](file:///C:/hades/Hecton8/Assets/_Project/Scripts/Editor/GraphOutputRenderer.cs) | Modified | Added `CoroutineManager` pump in batchmode, stable-frame gate (220 frames), terrain bounds window alignment, thread cleanup on exit. |
| [`Assets/_Project/Scripts/Editor/Diagnostics/DynamicGraphAuditorTask.cs`](file:///C:/hades/Hecton8/Assets/_Project/Scripts/Editor/Diagnostics/DynamicGraphAuditorTask.cs) | Modified | Fixed `global::System.Environment` qualification to prevent compilation errors under Hecton8 namespace. |
| [`Assets/_Project/Scripts/Editor/Diagnostics/DumpRawHeightsTask.cs`](file:///C:/hades/Hecton8/Assets/_Project/Scripts/Editor/Diagnostics/DumpRawHeightsTask.cs) | Modified | Added dynamic graph loading, null product safety checks, and vertical span logging. |
| [`Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity`](file:///C:/hades/Hecton8/Assets/_Project/Scenes/020_RENDER_SANDBOX_V2.unity) | Restored | Restored from Git HEAD to 4,908 KB after batchmode overwrite. |

---

## 4. Authority Handoff Receipt

`Authority used: AGENTS.md; PROJECT_BIBLES.md; terrain.md; .agents-skills/README.md; VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt; OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt; C:\Users\Admin\Documents\Без имени.txt.`
