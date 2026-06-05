# H8 Visual Proof Capture 1912 Static Risk Review - 2026-06-05

Evidence class: `STATIC_SOURCE_REVIEW`.

No Unity run, Play Mode, scene save, prefab save, material save, import, profiler, Frame Debugger, or project-setting mutation was performed by this review.

## Target

- `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs`

## Static Findings

1. `CaptureSurfaceWaterRecoveryProbeAndExit()`
   - Emits `h8_1914_surface_water_recovery_probe`.
   - Metadata truth string: `surface_water_recovery_probe_editor_only_unsaved`.
   - Calls `ApplySurfaceWaterRecoveryProbe(mainCamera)`.
   - Creates or enables temporary probe state such as `H8_TEMP_SurfaceWaterReadabilityProbe_1428` and `SURFACE_HORIZON_SALT_HAZE_1428`.
   - References `SurfaceWaterReadabilityShaderPath` / `Assets/_Project/Art/Shaders/H8_SurfaceWaterReadability_1428.shader`; the shader and `.meta` are deleted in the current worktree.
   - Resulting screenshot is diagnostic-only. It cannot be accepted as saved scene state or canonical h8_1475 proof.

2. `CaptureSurfaceCrestRecoveryProbeAndExit()`
   - Emits `h8_1914_surface_crest_recovery_probe`.
   - Metadata truth string: `surface_crest_recovery_probe_editor_only_unsaved`.
   - Calls `ApplySurfaceCrestRecoveryProbe()`.
   - Mutates the scene OceanRenderer through `SerializedObject` and `ApplyModifiedPropertiesWithoutUndo()`.
   - Changes include `_material`, `_extentsSizeMultiplier`, `_minScale`, `_maxScale`, `_lodDataResolution`, `_geometryDownSampleFactor`, and `_lodCount`.
   - No matching restore path is visible in the method.
   - Resulting screenshot, if generated, must be treated as an edited diagnostic probe, not product proof.

3. `QuarantineSurfaceRejectsAndExit()`
   - Disables renderers matching static names.
   - Calls `EditorSceneManager.MarkSceneDirty(scene)`.
   - Calls `EditorSceneManager.SaveScene(scene)`.
   - This is an actual scene mutation path, not a no-mutation proof capture path.

## Verdict

Status: `PROOF_TOOL_RISK / DIAGNOSTIC_ONLY`.

`H8VisualProofCapture1912.cs` can produce useful diagnostic rejection screenshots. It is not a safe canonical proof runner for h8_1475 unless the invoked method is proven no-mutation and its metadata says so.

## Required Guardrails

- Canonical h8_1475 proof must use a no-mutation capture method.
- A h8_1475 capture method must not create temporary visual probes, modify serialized OceanRenderer fields, disable scene renderers, save scenes, or rely on `HideAndDontSave` helper objects.
- Diagnostic probe filenames and metadata must retain `editor_only_unsaved` or equivalent rejection-proof wording.
- Any method that mutates scene state must be isolated from proof acceptance routes and must require explicit owner authorization.
- Future proof packet must include dirty-state audit before and after capture.
- Capture scripts must not reference missing shader/material paths. A missing diagnostic shader is a proof-tool blocker, not an excuse to accept the fallback screenshot.

## Rejection Rules

Reject any proof packet that:

- uses `h8_1914_surface_water_recovery_probe.png` or `h8_1914_surface_crest_recovery_probe.png` as acceptance evidence;
- omits capture-truth metadata;
- hides water/terrain defects with temp horizon haze;
- changes Crest/OceanRenderer serialized fields before capture and presents the frame as production state;
- saves `02_HECTON_WORLD.unity` during a no-mutation proof lane.

## Regression Model

- CPU: no runtime measurement.
- GC: no runtime measurement.
- Memory/VRAM: no residency proof.
- Correctness: proof-class risk only; no runtime behavior claim.
- Visual: diagnostic probe output can reject visible failure, not promote acceptance.

Final status: `PENDING VERIFICATION / PROOF_TOOL_RISK`.
