# AGENT TASK — Unity Verification Wave, 2026\-07\-26

Status: `PENDING VERIFICATION`
Evidence class: task brief only. This file is not proof of anything.
Requester: static/numeric audit pass (no Unity access). Every claim below is labelled
either `PROVEN_NUMERICALLY` (math executed outside Unity) or `NEEDS_RUNTIME_PROOF`.

Read `AGENTS.md` and `COMMON_SENSE.md` before starting. Obey the process gates.

* * *

## 0\. Mandatory process preflight

Do not skip. `AGENTS.md` build gate:

```powershell
Get-Process Unity,dotnet,csc,msbuild -ErrorAction SilentlyContinue |
  Select-Object Name,Id,CPU | Format-Table -AutoSize
(Get-Counter '\Processor(_Total)\% Processor Time' -SampleInterval 1 -MaxSamples 3).CounterSamples.CookedValue
```

If CPU \> `50%`, or a Unity import/compile is active, or another agent holds the Unity slot,
stop and report `BUILD_GATE_BLOCKED: <reason>`. Do not poll in a tight loop.

**Concurrency warning.** At the time this brief was written the following files had
mtimes inside the previous hour, i.e. another agent was editing them live:
`Assets/_Project/Scripts/HectonVoxelEngine.cs`, `HectonVoxelVolume.cs`,
`Assets/_Project/Shaders/HectonTerrain.shader`, `HectonTerrainLitPasses.hlsl`,
`HectonTerrainSampling.hlsl`. Confirm they are idle before compiling, and do not
revert another agent's work to make a gate pass.

* * *

## 1\. HIGHEST PRIORITY — run the PureLogic EditMode tests

Three source fixes were applied by the audit pass. All three are `PROVEN_NUMERICALLY`
but none has been compiled or executed in Unity. This single step converts them to proven.

Files changed:

| File | Change |
| --- | --- |
| `Assets/_Project/Scripts/PureLogic/Systems/CoreTempEquilibriumSolver.cs` | completed a Padé range reduction (`exp(-v/4)` was never raised to the 4th power) |
| `Assets/_Project/Scripts/PureLogic/Kinematics/SomaticDragCurveCalculator.cs` | validated 4 previously unvalidated config params; drag can no longer be NaN or negative |
| `Assets/_Project/Scripts/PureLogic/Systems/AmbientTemperatureDepthGradientCalculator.cs` | guarded `maxLatitude` (was `0f/0f` → NaN, and negative → `Math.Clamp` threw) |

Test files extended (appended cases, existing cases untouched):
`Tests/CoreTempEquilibriumSolverTests.cs` (\+3), `Tests/SomaticDragCurveCalculatorTests.cs` (\+3),
`Tests/AmbientTemperatureDepthGradientCalculatorTests.cs` (\+2).

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
pass — the assertions were derived from closed\-form math, so a failure means either the source
fix or my numeric model is wrong, and that must be resolved, not silenced.

Report back: test count, pass/fail, artifact path, and the failure text of anything red.

* * *

## 2\. Batchmode compile validation

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe" `
  -projectPath "C:\hades\Hecton8" -batchmode -quit `
  -executeMethod Hecton8.Editor.BootstrapArchitectureValidator.ValidateBootstrapArchitecture `
  -logFile "C:\hades\Hecton8\Docs\Reports\Compile_20260726.log"
```

Then scan for `error CS`. Per `AGENTS.md` Bee cache rule: confirm the log actually shows
`Csc Library/Bee/artifacts/.../Hecton8.PureLogic.dll` being recompiled. A Bee cache hit means
the tests above did **not** exercise the edited code — if so, touch the asmdef or delete
`Library/Bee/artifacts` and re\-run step 1.

**Acceptance:** exit 0, zero `CSxxxx` errors, and proof of a real `Hecton8.PureLogic.dll` recompile.

* * *

## 3\. NEEDS\_RUNTIME\_PROOF — voxel cave surface orientation

The audit verified statically, and I want a runtime cross\-check because the conclusion is
load\-bearing and rests on two conventions cancelling out.

What was `PROVEN_NUMERICALLY` outside Unity:

- `MarchingCubesLookupTable.EdgeTable`\: all 256 entries match the canonical MC topology
  derived from first principles (edge crossed ⟺ its two corner bits differ). 0 mismatches.
- `HectonVoxelEngine` `triTable`\: 4096 entries, canonical Bourke.
- Engine corner layout (`CubeDensities.d0..d7`, engine line \~3017) is Bourke with **y and z
  swapped** — a reflection (basis determinant `+1` vs Bourke's `-1`), which flips triangle
  winding exactly once.
- `ResolveCubeIndex` (line \~2904) sets a bit when `d < 0f`, and `solidNeighborCount`
  (line \~3589) treats `> 0f` as solid ⇒ **positive density \= rock, the set region is AIR**.
- Simulating the engine's exact emission on a sphere\-cavity SDF: 1208/1208 triangles wind
  toward the air cavity, i.e. toward the player. Shading normal `-gradient` (line \~3471)
  also points out of rock. Winding and normal agree; 100% consistency ⇒ mesh not torn.

**Conclusion: orientation is correct.** Two conventions cancel. Confirm at runtime:

1. Generate a cave region and enter it in Play Mode. Capture a screenshot from **inside** a
   cave looking at a wall, and one from **outside** looking at terrain.
2. Confirm walls are visible from the player side (not culled/invisible) and lit correctly
   (not black or inside\-out).
3. Confirm no missing\-triangle holes at chunk boundaries.

**Acceptance:** two screenshots plus a written description of what is actually visible
(`AGENTS.md` requires a VLM visual description, not just a file path).

**This is a trap for future work — please add to `voxels.md` if it is not already there:**
the mirrored corner layout and the AIR\-as\-set\-region choice are load\-bearing *as a pair*.
Anyone who "fixes" the corner order to canonical Bourke, or flips the density sign, will
invert every cave wall in the game. It currently works and is undocumented.

* * *

## 4\. NEEDS\_RUNTIME\_PROOF — terrain / shader passes reported clean statically

Audited read\-only (files were being edited concurrently, so nothing was changed):

- `HectonTerrain.shader`\: 31 `multi_compile` are the URP\-required set; the file already
  documents deliberate `shader_feature` hygiene citing `COMMON_SENSE #16`. No violation found.
- `HectonTerrainLitPasses.hlsl`\: every `half3` is a direction/normal
  (`viewDirWS`, `normalWS`, `tangentWS`); `positionWS` stays full `float`.
  **`COMMON_SENSE #4` compliant** — no half\-precision world positions.
- `HectonTerrainSampling.hlsl`\: correctly avoids reopening `CBUFFER_START(UnityPerMaterial)`
  (documented at line \~18) — SRP Batcher safe.
- `TerrainSeamDitherAlphaCalculator`\: 4×4 matrix verified equal to the canonical Bayer
  matrix (\+1, /17). Correct. `x & 3` wraps negative coords correctly.
  Note only: index is `x*4 + y`, which transposes the pattern versus the usual `y*4 + x`.
  Dispersion is preserved so this is cosmetic, not a defect — leave it unless art objects.
- `VoxelMeshHeightSeamBlendCalculator`\: correct. Minor: the `blendWidth <= 0` branch uses
  exact float equality, which is fragile but only in that degenerate case.

Needed at runtime: Frame Debugger / SRP Batcher confirmation that terrain draws actually
batch, and a shot\-list comparison against the mandatory reference folder
`Docs\mandatory if you work on systems that user sees (...)` per the Visual Reference
Parity Gate. Report `VISUAL_ROUTE_INVALID` if below the April baseline.

* * *

## 5\. Profiler / GC capture on the first\-20 route

Per `Docs/QUALITY_GATES.md` and `AGENTS.md` budgets: 60 s capture on the shallow route.
Targets: 60 FPS / 16.67 ms, main thread 12 ms, **GC 0 B/frame**, SetPass ≤ 600,
batches ≤ 1800, memory ≤ 4096 MB. Compact\-tier VRAM ceiling 1800 MB.

Report the actual numbers, not "looks fine". Numbers without a profiler artifact path are
rejected by the project's own evidence law.

* * *

## 6\. Report format

For each step: exact command, timestamp, exit code, artifact path, error/warning counts,
and pass/fail. Keep `PENDING VERIFICATION` on anything not actually executed. Do not mark
anything `VERIFIED` without the matching artifact. If a step is blocked, name the exact gate.

Open items deliberately **not** touched by the audit pass, listed so they are not lost:

1. `Assets/_Project/Scripts/Thermodynamics/MockHazardGenerator.cs` — the word `Mock` is banned
   by the Hollow System Ban. It is a flag\-gated diagnostic fallback (`enableMockHazards`) that
   seeds one 1000 °C source when real producers are absent. The real gap it exposes: no
   WFC/geology hazard\-source producer is wired, so the world has no genuine thermal hazards.
   It also stores a source id in `HazardSourceDTO._pad0`, i.e. a padding field used as data.
2. `CoreTempEquilibriumSolver` is orphaned — nothing in the runtime calls it;
   `HectonSurvivalSystem` integrates core temperature with its own inline
   `ResolveExponentialTemperatureStep`. Two owners of one truth. Decide the owner.
   (That live path uses `ApproximateExpNegPositive`, which I verified is accurate to \~1e\-14 %
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
   calculator, and throwing is not Burst\-compatible. The engine calls it from a
   `[BurstCompile]` job (line \~3033) passing 8 densities that do nothing.
6. `HectonVoxelVolume` has two trilinear implementations: an inline `math.lerp` version
   (line \~262) and a call to the PureLogic helper (line \~1545). Both use the same
   `x + 2y + 4z` corner order so results agree — but note that order is **different** from
   the Marching Cubes corner order used elsewhere in the voxel path. The duplication may be
   deliberate (PureLogic has `noEngineReferences: true`, so it cannot use `Unity.Mathematics`
   inside a Burst job). Confirm intent before consolidating.
7. `UI_HUD_V4_PROGRESS.md` claims a full Shapes\-driven HUD was implemented in
   `HectonSuitHUD_v4.cs`. That file is now a 2.2 KB compatibility shim. The doc overstates
   the live state and should be corrected.

## ROUND 2 — corrected after the 2026\-07\-26 18:07 run

The first wave was executed correctly and reported honestly: exit code `0` was refused as
proof because the results artifact was absent. That was the right call. Two things went
wrong, and **neither was the agent's fault — step 2 of the original brief was simply the
wrong tool.** Diagnosis below, then the corrected commands.

### Why the test XML was never written

Run 1 (tests) and run 2 (validator) differ in exactly one decisive line.

Run 1 log:

```text
[Licensing::Module] Error: Failed to handshake to channel: "LicenseClient-Admin"
[Licensing::Module] LicensingClient has failed validation; ignoring
[Licensing::Module] Error: Access token is unavailable; failed to update
```

Run 2 log:

```text
[Licensing::Module] Successfully connected to LicensingClient on channel: "LicenseClient-Admin-6000.5.0"
```

**Primary cause: the editor had no valid licence during the test run.** Unity's Test
Framework needs one; without it the editor still boots, still compiles (which is why Bee
produced `Hecton8.PureLogic.Tests.dll`), still exits `0` — and silently never starts the
test runner, so no XML is emitted. That matches the log exactly: the only `-runTests`
mentions in 123,940 bytes are the command\-line echo at lines 16–21, and there is no
`TestRunner`/`Test run finished` section anywhere.

**Secondary suspect, and a real project defect either way — Odin is broken on this Unity
version.** Two `InitializeOnLoad` handlers threw during domain reload:

```text
MissingFieldException: Field not found:
  System.Action`1<UnityEngine.UIElements.IMGUIContainer>
  UnityEngine.UIElements.UIElementsUtility.s_BeginContainerCallback
  at Sirenix.Utilities.Editor.GUITimeHelper.Init ()
  at Sirenix.OdinInspector.Editor.Internal.UIToolkitIntegration.ImguiElementUtils.Init ()
```

Sirenix/Odin is compiled against a UIElements API that Unity `6000.5.0f1` no longer
exposes. `AGENTS.md` keeps Odin editor\-only so player builds are unaffected, but this
throws on **every** editor domain reload, and an unhandled exception inside
`ProcessInitializeOnLoadMethodAttributes` can abort later callbacks in that same phase —
which is where the Test Framework registers. Worth fixing on its own merits: update Odin
to a Unity 6.5\-compatible build, or quarantine it.

### Why step 2 failed — my brief was wrong, not the run

`BootstrapArchitectureValidator.ValidateBootstrapArchitecture` requires `00_BOOTSTRAP.unity`
to already be **open**, and it reports through `EditorUtility.DisplayDialog` — a modal
dialog, which is meaningless under `-batchmode`. The log proves both:

```text
DisplayDialog: Bootstrap Validation ? 00_BOOTSTRAP.unity not found or not loaded.
  at BootstrapArchitectureValidator.cs:53
```

It was never a general compile validator. Do not use it as one. Compile proof comes from
the compile log itself, and run 1 already delivered that: a full 38.17 s script compilation
across 3835 Bee nodes with **0 `error CS`**. Treat that as `CLI_COMPILE` evidence for the
edited source state and nothing more.

### 2\.1 — Re\-run the tests, with the post\-conditions enforced

Save as `C:\hades\Hecton8\Tools\RunPureLogicProof.ps1` and run it. Writing a script file
avoids the nested\-quote parse error the previous one\-liner hit
(`@result=''Failed''` → `MissingPropertyName`).

```powershell
$ErrorActionPreference = 'Continue'
$unity  = 'C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe'
$report = 'C:\hades\Hecton8\Docs\Reports'
$xml    = Join-Path $report 'PureLogic_EditMode_R2.xml'
$log    = Join-Path $report 'PureLogic_EditMode_R2.log'
Remove-Item -Force -ErrorAction SilentlyContinue $xml, $log

& $unity -projectPath 'C:\hades\Hecton8' -batchmode -quit `
    -runTests -testPlatform EditMode `
    -assemblyNames Hecton8.PureLogic.Tests `
    -testResults $xml -logFile $log
$exit = $LASTEXITCODE

# POST-CONDITION 1 — licence must have connected, or the whole run is void.
$licOk  = Select-String -Path $log -Pattern 'Successfully connected to LicensingClient' -Quiet
$licBad = Select-String -Path $log -Pattern 'LicensingClient has failed validation' -Quiet
"EXIT=$exit  LICENCE_OK=$licOk  LICENCE_FAILED=$licBad"
if (-not $licOk -or $licBad) {
    'VOID: editor had no valid licence. Test Framework cannot run. See 2.2.'
    exit 2
}

# POST-CONDITION 2 — the results artifact must exist.
if (-not (Test-Path $xml)) { 'FAIL: no results XML. Exit code 0 is not proof.'; exit 3 }

# POST-CONDITION 3 — the edited production assembly must have been recompiled by Bee.
$csc = Select-String -Path $log -Pattern 'Csc .*Hecton8\.PureLogic\.dll'
"PURELOGIC_DLL_RECOMPILED=$([bool]$csc)"
$csc | ForEach-Object { $_.Line }

[xml]$x = Get-Content -Raw $xml
$r = $x.'test-run'
"TOTAL=$($r.total) PASSED=$($r.passed) FAILED=$($r.failed) SKIPPED=$($r.skipped) RESULT=$($r.result)"

$wanted = @(
  'Test_MatchesNewtonCoolingLaw_Case06','Test_SuitResistanceScalesCooling_Case07',
  'Test_MonotonicAndNoOvershoot_Case08','Test_ConfigParametersNeverLeakNaN_Case06',
  'Test_DragIsNeverNegative_Case07','Test_ValidConfigBehaviourUnchanged_Case08',
  'Test_DegenerateMaxLatitude_Case06','Test_DefaultBandBehaviourUnchanged_Case07')
foreach ($w in $wanted) {
    $hit = @($x.SelectNodes('//test-case') | Where-Object { $_.name -eq $w })
    if ($hit.Count -eq 0) { "MISSING $w" }
    else { $hit | ForEach-Object { "CASE $w = $($_.result)" } }
}
foreach ($f in @($x.SelectNodes('//test-case')) | Where-Object { $_.result -eq 'Failed' }) {
    "FAIL $($f.fullname)"
    $f.failure.message.'#text'
    $f.failure.'stack-trace'.'#text'
}
```

**Acceptance:** `LICENCE_OK=True`, XML present, `PURELOGIC_DLL_RECOMPILED=True`,
`FAILED=0`, and all 8 cases reported `Passed`. Anything else is `PENDING VERIFICATION`.

If `PURELOGIC_DLL_RECOMPILED=False` but everything else passes, the tests may have run
against a cached DLL. Force it and re\-run once:

```powershell
(Get-Item 'C:\hades\Hecton8\Assets\_Project\Scripts\PureLogic\Hecton8.PureLogic.asmdef').LastWriteTime = Get-Date
```

Prefer that over deleting `Library/Bee/artifacts`, which costs a full rebuild.

### 2\.2 — If the licence is still not connecting

This is an environment blocker, not a code problem. Do not work around it and do not
fabricate results. Report `BUILD_GATE_BLOCKED: Unity licence unavailable` and try, in order:

1. Open Unity Hub once, confirm the account is signed in and the seat is active, close it,
   then re\-run 2.1. A Hub sign\-in usually re\-establishes `LicenseClient-Admin`.
2. If the Hub is fine, restart the `Unity Licensing Client` (the run\-2 log shows it *can*
   connect on this machine, so the failure is transient, not a missing entitlement).
3. Only then consider `-manualLicenseFile`.

The evidence that this is transient: the identical editor connected successfully four
minutes later in run 2. Nothing about the project changed between them.

### 2\.3 — Compile proof, corrected

Replace the old step 2 entirely. No scene, no dialog:

```powershell
$log = 'C:\hades\Hecton8\Docs\Reports\Compile_R2.log'
& 'C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe' `
    -projectPath 'C:\hades\Hecton8' -batchmode -quit -logFile $log
$errs = @(Select-String -Path $log -Pattern 'error CS[0-9]+')
"CS_ERRORS=$($errs.Count)"
$errs | ForEach-Object { $_.Line }
Select-String -Path $log -Pattern 'AssetDatabase: script compilation time' | ForEach-Object { $_.Line }
```

A plain `-batchmode -quit` import forces the same compile the validator was standing in for.
**Acceptance:** `CS_ERRORS=0` plus a visible script\-compilation line.

### 2\.4 — Findings the run surfaced, with their evidence class stated honestly

Neither blocks the proof wave. The second one is **narrower than I first wrote it** — the
correction is recorded here rather than quietly dropped.

**A. Odin/Sirenix is incompatible with Unity 6000.5.0f1.** Evidence class: live editor log,
solid. `MissingFieldException` on `UIElementsUtility.s_BeginContainerCallback`, thrown from
`GUITimeHelper.Init` and `ImguiElementUtils.Init`, twice per domain reload. Odin is
editor\-only under `AGENTS.md`, so player builds are unaffected, but it destabilises
`InitializeOnLoad` ordering and is the likely accomplice in the silent test\-runner failure.
Worth its own ticket: move Odin to a Unity 6.5\-compatible build, or quarantine it.

**B. `SaveData.cs` violates the project's own save law. Whether data is actually lost is
NOT established — do not act on that part until it is tested.**

What is confirmed, by direct file read and by the agent's real compile log:

- `SaveData` declares `Dictionary<string, float> toolDurabilityMap` (line 153),
  `Dictionary<string, bool> toolBrokenMap` (line 156) and
  `Dictionary<string, string> CustomModData` (line 365).
- `SaveData` does **not** implement `ISerializationCallbackReceiver` — zero matches in the file.
- Unity's analyzer flags all three as `UAC1009: uses an unsupported type`.
- `AGENTS.md` states: *"Managed\-collections with dynamic allocations (e.g.
  `Dictionary<string, T>` or `HashSet<string>`) in the root structures of `SaveData.cs` are
  banned; serialization must rely on `ISerializationCallbackReceiver` and parallel flat lists."*

So the **law violation is proven**\: banned collection types, in `SaveData` root, without the
required callback receiver. That alone justifies a task.

What I could **not** establish, and previously overstated: whether these fields survive a
save/load cycle. This project does not use Unity's built\-in serializer for saves — it has a
custom binary path (`SaveBinaryStorage.cs`, \~510 KB). A `UAC1009` warning only says Unity's
*own* serializer would skip the field; it says nothing about a custom binary writer. My
attempt to grep for the field names in the binary writer proved nothing, because the same
grep also returned zero hits for `discoveredBiomeBitWords` — a field that is definitely
persisted. The correct read of that result is that my search instrument was wrong, not that
the data is lost. I was also working from a 241\-file subset of the project's 6,339 `.cs`
files, so no cross\-file "nothing references this" claim from my side is admissible.

**Settle it with a round\-trip, not with reasoning.** Wear a tool down to a known durability,
break a second tool, save, quit to menu, reload, and read the values back. If they survive,
this is a law/hygiene ticket only. If they reset, it is a save\-integrity defect and the fix
is parallel flat lists behind `ISerializationCallbackReceiver`.

**Correction to my earlier note:** `discoveredBiomeIds` (`HashSet<int>`, line 159) is **not**
a defect. Its own comment marks it *"Legacy … kept only for backward\-compatible migration
reads"*, it is set to `null` in the factory (line 452), and it has already been superseded by
`discoveredBiomeBitWords` (`long[]`, line 162) — which is exactly the flat\-layout pattern the
law asks for. That one was correctly migrated and I should not have listed it.

Lower priority, same evidence class as A: `AbsoluteUniversePositionBlit128` (`SaveData.cs`
line 1018) and `AbsoluteUniversePosition` (`NetworkNodeData.cs` line 137) are `UAC1001`\-skipped
for missing `[Serializable]` — again, real under Unity's serializer, unknown under the custom
binary path, same round\-trip test settles it.
