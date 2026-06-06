# H8 Visual Proof Capture 1912 Static Risk Review - 2026-06-05

Evidence class: `STATIC_SOURCE_REVIEW`.

No Unity run, Play Mode, scene save, prefab save, material save, import, profiler, Frame Debugger, or project-setting mutation was performed by this review.

## Target

- `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs`

## Static Findings

1. Current source no longer contains `CaptureSurfaceWaterRecoveryProbeAndExit()` or the old water-readability shader path.
   - The old `h8_1914_surface_water_recovery_probe` text capture still records `surface_water_recovery_probe_editor_only_unsaved` and `H8_TEMP_SurfaceWaterReadabilityProbe_1428=MISSING`.
   - That old artifact remains diagnostic rejection evidence only.
   - It is no longer valid to report a current `H8VisualProofCapture1912.cs` reference to the deleted water-readability shader unless a fresh source scan proves the reference returned.

2. `CaptureSurfaceCrestRecoveryProbeAndExit()`
   - Emits `h8_1914_surface_crest_recovery_probe`.
   - Metadata truth string: `surface_actual_terrain_crest_recovery_probe_editor_only_unsaved`.
   - Calls `ApplySurfaceCrestRecoveryProbe()`.
   - Calls `ConfigureSurfaceHorizonHazeProbe()` and references `SurfaceHorizonHazeShaderPath` / `Assets/_Project/Art/Shaders/H8_SurfaceHorizonHaze_1428.shader`.
   - Calls `ConfigureActualTerrainMapMagicProbe(camera)` and mutates MapMagic graph/range generation state through `SerializedObject` and direct property writes.
   - Mutates the scene OceanRenderer through `SerializedObject` and `ApplyModifiedPropertiesWithoutUndo()`.
   - Changes include `_material`, `_waterBodyCulling`, `_extentsSizeMultiplier`, `_minScale`, `_maxScale`, `_lodDataResolution`, `_geometryDownSampleFactor`, `_lodCount`, `_createSeaFloorDepthData`, `_createFoamSim`, and `_createShadowData`.
   - Creates `HideAndDontSave` temporary probe materials through `new Material(...)`.
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
- Capture scripts must not reference stale or missing shader/material/scene/graph paths. A missing diagnostic asset path is a proof-tool blocker, not an excuse to accept the fallback screenshot.

## Rejection Rules

Reject any proof packet that:

- uses `h8_1914_surface_water_recovery_probe.png` or `h8_1914_surface_crest_recovery_probe.png` as acceptance evidence;
- omits capture-truth metadata;
- hides water/terrain defects with temp horizon haze;
- changes Crest/OceanRenderer serialized fields before capture and presents the frame as production state;
- saves `02_HECTON_WORLD.unity` during a no-mutation proof lane.

## 2026-06-06 Dialogue Refresh

The pasted Unity-worker dialogue and current source diff show the proof-risk widened, not narrowed.

New static risk classes in current `H8VisualProofCapture1912.cs` include:

- `System.Reflection` / private Crest `RunUpdate` invocation;
- `System.Threading.Thread.Sleep` editor-loop pumping;
- camera near/far/culling mutation;
- behaviour enable toggles around Crest;
- MapMagic `globals.height`, height apply, and interpolation overrides;
- terrain `materialTemplate`, layers, instancing, pixel error, and basemap distance overrides;
- `EditorApplication.QueuePlayerLoopUpdate` / `SceneView.RepaintAll` frame pumping.

`Tools/ValidateVisualProofCaptureGuardrails.py --mode harness-candidate --strict` now rejects these patterns. Current source rejects with `REJECT_CANONICAL_HARNESS_SOURCE violations=102 diagnostic_only=true` after the latest concurrent source drift and refined pattern pass.

This still does not prove a canonical harness exists. It proves the current runner is more strongly quarantined as diagnostic-only.

## Regression Model

- CPU: no runtime measurement.
- GC: no runtime measurement.
- Memory/VRAM: no residency proof.
- Correctness: proof-class risk only; no runtime behavior claim.
- Visual: diagnostic probe output can reject visible failure, not promote acceptance.

Final status: `PENDING VERIFICATION / PROOF_TOOL_RISK`.

## 2026-06-06 Crest Machinery Removal Update

Current `H8VisualProofCapture1912.cs` no longer contains the old Crest/MapMagic/material/terrain mutation machinery. Public h8_1914-h8_1918 routes are disabled-output routes only. Private shared Crest capture, raw `RenderCamera`, raw `WriteMetadata`, `System.Reflection`, MapMagic generation, terrain presentation writes, material clones, static Unity object state, and raw image readback have been removed from this source.

Fresh static checks:

- `python -B -m unittest Tools.test_validate_visual_proof_capture_guardrails` returned 30 tests OK.
- `python -B Tools\ValidateVisualProofCaptureGuardrails.py` returned `VISUAL_PROOF_CAPTURE_GUARDRAILS_OK risks=0 asset_refs=0 categories=`.
- `python -B Tools\ValidateVisualProofCaptureGuardrails.py --mode harness-candidate --allow-diagnostic-rejection` returned `PASS_DIAGNOSTIC_REJECTION_SOURCE violations=2 diagnostic_only=true`.

Reject stale review claims that current `H8VisualProofCapture1912.cs` still contains private Crest `RunUpdate`, MapMagic generation, terrain layer/material mutation, raw camera render, raw readback, temp material clone proof paths, old MCP output root, or legacy h8_1912/h8_1913/h8_1914 capture names. The remaining rejection is class/status only: disabled diagnostic route payload text. This is still not a canonical h8_1475 proof harness and not visual acceptance.
