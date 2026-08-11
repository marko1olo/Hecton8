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

This report documents the full investigation, root-cause diagnosis, code fixes, and compliance checks performed across HECTON-8 terrain geology and MapMagic generation pipelines over the entire session of 2026-08-10 and 2026-08-11.

Critical engineering defects related to batchmode generation hangs, diagnostic exit-code traps, graph topology verification, scene preservation rules, and macro-geology generation have been investigated, resolved, and verified against `AGENTS.md` mandates. A significant portion of the work involved correcting faulty diagnostic tools that were misleading observers about the true state of the terrain geometry.

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

### 2.5. Geology Graph Topology & Diagnostic Auditors

* **Graph File**: `Assets/_Project/Data/World/Sandbox/HECTON_PROCEDURAL_GEOLOGY_GRAPH.asset`
* **Diagnostics Fixes**:
  - `DynamicGraphAuditorTask.cs` fixed `System.Environment` namespace shadowing (`global::System.Environment.GetCommandLineArgs()`). Added `-graphAsset` parameter support to explicitly audit specific graphs rather than hardcoding the Biomes graph.
  - `DumpRawHeightsTask.cs` added dynamic graph loading, per-graph span reading from authored node fields (preventing hardcoded 12000m span assumption), and null product handling with descriptive error messages.
  - GPU Refusal Guard added to `DumpRawHeightsTask` since compute output is all zeros without a GPU context.

### 2.6. Macro-Geology Noise Fields Defect

* **Incident**: Clean-room splatmaps resolved only 2 of 8 authored material classes. 47 macro-geology noise fields were returning constants instead of noise.
* **Root Cause**: `DoubleFractalSimplexNoise01` and `DoubleRidgedMultifractal01` lacked overloads for `(scaledPos, seed ^ constant, octaves)`, causing C# to silently bind the uint `seed` to the float `frequency` argument (resulting in frequency values like `1.37e9`). The high frequency caused the skewed lattice coordinates to exceed integer range, resulting in a degenerate cell (yielding simplex 0).
* **Resolution**: Fixed at the signature level by adding the `(double2, uint, int)` overloads. Overload resolution now prefers the new methods, restoring organic multi-scale forms and bringing material classes from 2 to 6.

### 2.7. World Size & Diagnostic Misaiming

* **Incident**: `CleanRoomTerrainTest` was taking screenshots of the world that were entirely empty or flat, leading to the false conclusion that the geometry was broken.
* **Root Causes & Resolution**:
  - **Height X-Ray Was Blind**: `WriteScalarMap` was passed a fixed 5200m range (-5000..200). A 149m relief (2.9% of range) rendered uniformly grey. X-Ray now self-normalises against its measured extent.
  - **Beauty Camera Aiming**: Camera was fixed at a hardcoded position, looking at a point 1450m above the surface. Aiming was rewritten to dynamically frame the grid based on size and shoot from a 38-degree elevation.
  - **Probe Sites Outside World**: The world extent is explicitly bounded to 30km (±15000m), but probe sites (`p1..p5`) were placed up to 777km out. Sites were updated to `w1..w5` based on in-world percentile of slope distributions (w1 = 9.3 deg, w5 = 57.0 deg).

### 2.8. Slope Budget & Physical Material Ramps

* **Conflict Analysis**: Shelf lerp attribution requires ~32.7km to drop 2860m at a 5-degree angle. With a 30km world width, a 41.6km authored width cannot physically fit in the world. Scaling world extent and width proportionately by x4 yields stable medians (~19.7 deg) and halves the share of the world >40 degrees.
* **Angle of Repose Sync**: The resolver held two conflicting bounds for sediment sliding (angle of repose closed at 37.8° while steepSlope opened at 23.0°). `steepSlope` is now strictly defined as exactly `(1 - angleOfRepose)`.
* **Talus Apron**: A new talus (rock debris / gravel) term was introduced in the repose band. This activated the previously dead `ReefRubble` class for submarine scarps.
* **Ratchet**: Implemented `WorldTerrainDetailContracts` ratchet pinned in BOTH directions per site to catch regressions where cliffs are silently flattened or plains roughened.

### 2.9. Power Logistics Router Fence
* **Fix**: Placed a read fence for `_counters` after the mid-tick CSR rebuild schedule inside `ApplyDeterministicMockModuleToggle` (`ShinobuLogisticsRouter.cs`). Prevented an `InvalidOperationException` that aborted the slow-tick lane.

---

## 3. Summary of Code & Asset Modifications

| File | Status | Description |
|---|---|---|
| `GraphOutputRenderer.cs` | Modified | Added `CoroutineManager` pump in batchmode, stable-frame gate (220 frames), terrain bounds window alignment, thread cleanup on exit. |
| `DumpRawHeightsTask.cs` | Modified | Added dynamic graph `-graphAsset` loading, null product safety checks, per-graph vertical span derivation, and GPU refusal guard. |
| `DynamicGraphAuditorTask.cs` | Modified | Fixed `global::System.Environment` qualification; implemented `-graphAsset` routing. |
| `WorldMacroGeologyFields.cs` | Modified | Restored 47 macro-geology noise fields via new `(double2, uint, int)` signature overloads. |
| `CleanRoomTerrainTest.cs` | Modified | Fixed blind Height X-Ray (now self-normalising), re-aimed beauty camera, stitched 3x3 tiles, added `w1..w5` valid probe sites. |
| `WorldTerrainDetailContracts.cs` | Modified | Ratcheted approved seafloor geometry per site; synchronized angle of repose ramps and added talus aprons. |
| `ShinobuLogisticsRouter.cs` | Modified | Fenced `_counters` read after mid-tick CSR rebuild schedule. |
| `020_RENDER_SANDBOX_V2.unity` | Restored | Restored from Git HEAD to 4,908 KB after batchmode overwrite. |

---

## 4. Authority Handoff Receipt

`Authority used: AGENTS.md; PROJECT_BIBLES.md; terrain.md; .agents-skills/README.md; VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt; OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt; C:\Users\Admin\Documents\Без имени.txt.`
