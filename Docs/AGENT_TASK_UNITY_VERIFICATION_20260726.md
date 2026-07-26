# AGENT TASK — Unity Verification Wave, 2026-07-26

Status: `PENDING VERIFICATION`
Evidence class: task brief only. This file is not proof of anything.
Requester: static/numeric audit pass (no Unity access). Every claim below is labelled
either `PROVEN_NUMERICALLY` (math executed outside Unity) or `NEEDS_RUNTIME_PROOF`.

Read `AGENTS.md` and `COMMON_SENSE.md` before starting. Obey the process gates.

---

## 0. Mandatory process preflight

Do not skip. `AGENTS.md` build gate:

```powershell
Get-Process Unity,dotnet,csc,msbuild -ErrorAction SilentlyContinue |
  Select-Object Name,Id,CPU | Format-Table -AutoSize
(Get-Counter '\Processor(_Total)\% Processor Time' -SampleInterval 1 -MaxSamples 3).CounterSamples.CookedValue
```

If CPU > `50%`, or a Unity import/compile is active, or another agent holds the Unity slot,
stop and report `BUILD_GATE_BLOCKED: <reason>`. Do not poll in a tight loop.

**Concurrency warning.** At the time this brief was written the following files had
mtimes inside the previous hour, i.e. another agent was editing them live:
`Assets/_Project/Scripts/HectonVoxelEngine.cs`, `HectonVoxelVolume.cs`,
`Assets/_Project/Shaders/HectonTerrain.shader`, `HectonTerrainLitPasses.hlsl`,
`HectonTerrainSampling.hlsl`. Confirm they are idle before compiling, and do not
revert another agent's work to make a gate pass.

---

## 1. HIGHEST PRIORITY — run the PureLogic EditMode tests

Three source fixes were applied by the audit pass. All three are `PROVEN_NUMERICALLY`
but none has been compiled or executed in Unity. This single step converts them to proven.

Files changed:

| File | Change |
|---|---|
| `Assets/_Project/Scripts/PureLogic/Systems/CoreTempEquilibriumSolver.cs` | completed a Padé range reduction (`exp(-v/4)` was never raised to the 4th power) |
| `Assets/_Project/Scripts/PureLogic/Kinematics/SomaticDragCurveCalculator.cs` | validated 4 previously unvalidated config params; drag can no longer be NaN or negative |
| `Assets/_Project/Scripts/PureLogic/Systems/AmbientTemperatureDepthGradientCalculator.cs` | guarded `maxLatitude` (was `0f/0f` → NaN, and negative → `Math.Clamp` threw) |

Test files extended (appended cases, existing cases untouched):
`Tests/CoreTempEquilibriumSolverTests.cs` (+3), `Tests/SomaticDragCurveCalculatorTests.cs` (+3),
`Tests/AmbientTemperatureDepthGradientCalculatorTests.cs` (+2).

Run EditMode tests for the `Hecton8.PureLogic.Tests` assembly:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" `
  -projectPath "C:\hades\Hecton8" -batchmode -quit `
  -runTests -testPlatform EditMode `
  -assemblyNames Hecton8.PureLogic.Tests `
  -testResults "C:\hades\Hecton8\Docs\Reports\PureLogic_EditMode_20260726.xml" `
  -logFile "C:\hades\Hecton8\Docs\Reports\PureLogic_EditMode_20260726.log"
```

**Acceptance:** zero failures across the whole assembly, and these 8 new cases present and passing:

- `CoreTempEquilibriumSolverTests.Test_MatchesNewtonCoolingLaw_Case06`
- `CoreTempEquilibriumSolverTests.Test_SuitResistanceScalesCooling_Case07`
- `CoreTempEquilibriumSolverTests.Test_MonotonicAndNoOvershoot_Case08`
- `SomaticDragCurveCalculatorTests.Test_ConfigParametersNeverLeakNaN_Case06`
- `SomaticDragCurveCalculatorTests.Test_DragIsNeverNegative_Case07`
- `SomaticDragCurveCalculatorTests.Test_ValidConfigBehaviourUnchanged_Case08`
- `AmbientTemperatureDepthGradientCalculatorTests.Test_DegenerateMaxLatitude_Case06`
- `AmbientTemperatureDepthGradientCalculatorTests.Test_DefaultBandBehaviourUnchanged_Case07`

**If any FAIL:** report the exact assertion and actual value. Do not "fix" the test to make it
pass — the assertions were derived from closed-form math, so a failure means either the source
fix or my numeric model is wrong, and that must be resolved, not silenced.

Report back: test count, pass/fail, artifact path, and the failure text of anything red.

---

## 2. Batchmode compile validation

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" `
  -projectPath "C:\hades\Hecton8" -batchmode -quit `
  -executeMethod Hecton8.Editor.BootstrapArchitectureValidator.ValidateBootstrapArchitecture `
  -logFile "C:\hades\Hecton8\Docs\Reports\Compile_20260726.log"
```

Then scan for `error CS`. Per `AGENTS.md` Bee cache rule: confirm the log actually shows
`Csc Library/Bee/artifacts/.../Hecton8.PureLogic.dll` being recompiled. A Bee cache hit means
the tests above did **not** exercise the edited code — if so, touch the asmdef or delete
`Library/Bee/artifacts` and re-run step 1.

**Acceptance:** exit 0, zero `CSxxxx` errors, and proof of a real `Hecton8.PureLogic.dll` recompile.

---

## 3. NEEDS_RUNTIME_PROOF — voxel cave surface orientation

The audit verified statically, and I want a runtime cross-check because the conclusion is
load-bearing and rests on two conventions cancelling out.

What was `PROVEN_NUMERICALLY` outside Unity:

- `MarchingCubesLookupTable.EdgeTable`: all 256 entries match the canonical MC topology
  derived from first principles (edge crossed ⟺ its two corner bits differ). 0 mismatches.
- `HectonVoxelEngine` `triTable`: 4096 entries, canonical Bourke.
- Engine corner layout (`CubeDensities.d0..d7`, engine line ~3017) is Bourke with **y and z
  swapped** — a reflection (basis determinant `+1` vs Bourke's `-1`), which flips triangle
  winding exactly once.
- `ResolveCubeIndex` (line ~2904) sets a bit when `d < 0f`, and `solidNeighborCount`
  (line ~3589) treats `> 0f` as solid ⇒ **positive density = rock, the set region is AIR**.
- Simulating the engine's exact emission on a sphere-cavity SDF: 1208/1208 triangles wind
  toward the air cavity, i.e. toward the player. Shading normal `-gradient` (line ~3471)
  also points out of rock. Winding and normal agree; 100% consistency ⇒ mesh not torn.

**Conclusion: orientation is correct.** Two conventions cancel. Confirm at runtime:

1. Generate a cave region and enter it in Play Mode. Capture a screenshot from **inside** a
   cave looking at a wall, and one from **outside** looking at terrain.
2. Confirm walls are visible from the player side (not culled/invisible) and lit correctly
   (not black or inside-out).
3. Confirm no missing-triangle holes at chunk boundaries.

**Acceptance:** two screenshots plus a written description of what is actually visible
(`AGENTS.md` requires a VLM visual description, not just a file path).

**This is a trap for future work — please add to `voxels.md` if it is not already there:**
the mirrored corner layout and the AIR-as-set-region choice are load-bearing *as a pair*.
Anyone who "fixes" the corner order to canonical Bourke, or flips the density sign, will
invert every cave wall in the game. It currently works and is undocumented.

---

## 4. NEEDS_RUNTIME_PROOF — terrain / shader passes reported clean statically

Audited read-only (files were being edited concurrently, so nothing was changed):

- `HectonTerrain.shader`: 31 `multi_compile` are the URP-required set; the file already
  documents deliberate `shader_feature` hygiene citing `COMMON_SENSE #16`. No violation found.
- `HectonTerrainLitPasses.hlsl`: every `half3` is a direction/normal
  (`viewDirWS`, `normalWS`, `tangentWS`); `positionWS` stays full `float`.
  **`COMMON_SENSE #4` compliant** — no half-precision world positions.
- `HectonTerrainSampling.hlsl`: correctly avoids reopening `CBUFFER_START(UnityPerMaterial)`
  (documented at line ~18) — SRP Batcher safe.
- `TerrainSeamDitherAlphaCalculator`: 4×4 matrix verified equal to the canonical Bayer
  matrix (+1, /17). Correct. `x & 3` wraps negative coords correctly.
  Note only: index is `x*4 + y`, which transposes the pattern versus the usual `y*4 + x`.
  Dispersion is preserved so this is cosmetic, not a defect — leave it unless art objects.
- `VoxelMeshHeightSeamBlendCalculator`: correct. Minor: the `blendWidth <= 0` branch uses
  exact float equality, which is fragile but only in that degenerate case.

Needed at runtime: Frame Debugger / SRP Batcher confirmation that terrain draws actually
batch, and a shot-list comparison against the mandatory reference folder
`Docs\mandatory if you work on systems that user sees (...)` per the Visual Reference
Parity Gate. Report `VISUAL_ROUTE_INVALID` if below the April baseline.

---

## 5. Profiler / GC capture on the first-20 route

Per `Docs/QUALITY_GATES.md` and `AGENTS.md` budgets: 60 s capture on the shallow route.
Targets: 60 FPS / 16.67 ms, main thread 12 ms, **GC 0 B/frame**, SetPass ≤ 600,
batches ≤ 1800, memory ≤ 4096 MB. Compact-tier VRAM ceiling 1800 MB.

Report the actual numbers, not "looks fine". Numbers without a profiler artifact path are
rejected by the project's own evidence law.

---

## 6. Report format

For each step: exact command, timestamp, exit code, artifact path, error/warning counts,
and pass/fail. Keep `PENDING VERIFICATION` on anything not actually executed. Do not mark
anything `VERIFIED` without the matching artifact. If a step is blocked, name the exact gate.

Open items deliberately **not** touched by the audit pass, listed so they are not lost:

1. `Assets/_Project/Scripts/Thermodynamics/MockHazardGenerator.cs` — the word `Mock` is banned
   by the Hollow System Ban. It is a flag-gated diagnostic fallback (`enableMockHazards`) that
   seeds one 1000 °C source when real producers are absent. The real gap it exposes: no
   WFC/geology hazard-source producer is wired, so the world has no genuine thermal hazards.
   It also stores a source id in `HazardSourceDTO._pad0`, i.e. a padding field used as data.
2. `CoreTempEquilibriumSolver` is orphaned — nothing in the runtime calls it;
   `HectonSurvivalSystem` integrates core temperature with its own inline
   `ResolveExponentialTemperatureStep`. Two owners of one truth. Decide the owner.
   (That live path uses `ApproximateExpNegPositive`, which I verified is accurate to ~1e-14 %
   in the real `dt/tau` range — it is correct, unlike the orphaned solver was.)
3. `ExtinctionRiskIndexCalculator` accepts `minSafePopScale` and `popRiskMax` and ignores both.
   Invisible at their `1.0f` defaults; silently ignores any caller that tunes them. Not fixed
   because the intended semantics cannot be derived without inventing behaviour.
4. Dead parameters, misleading API only: `QuestObjectiveProgressNormalizer.isOrdered`,
   `HypothermiaShiverCurveCalculator.normalTemp` (passed live from `HectonSurvivalSystem`),
   `FaunaHypnosisPullForceCalculator.lockDuration`.
5. `MarchingCubesLookupTable.Calculate` validates `cornerDensities`/`isoLevel` then ignores
   them and returns only `EdgeTable[caseMask]`, while its XML doc claims it returns
   "interpolatedVertices". It also **throws** on bad input, unlike every other PureLogic
   calculator, and throwing is not Burst-compatible. The engine calls it from a
   `[BurstCompile]` job (line ~3033) passing 8 densities that do nothing.
6. `HectonVoxelVolume` has two trilinear implementations: an inline `math.lerp` version
   (line ~262) and a call to the PureLogic helper (line ~1545). Both use the same
   `x + 2y + 4z` corner order so results agree — but note that order is **different** from
   the Marching Cubes corner order used elsewhere in the voxel path. The duplication may be
   deliberate (PureLogic has `noEngineReferences: true`, so it cannot use `Unity.Mathematics`
   inside a Burst job). Confirm intent before consolidating.
7. `UI_HUD_V4_PROGRESS.md` claims a full Shapes-driven HUD was implemented in
   `HectonSuitHUD_v4.cs`. That file is now a 2.2 KB compatibility shim. The doc overstates
   the live state and should be corrected.
