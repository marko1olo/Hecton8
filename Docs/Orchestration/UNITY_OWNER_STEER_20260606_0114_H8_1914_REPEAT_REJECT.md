# Unity Owner Steer - 2026-06-06 01:14 h8_1914 Repeat Reject

Status: `ACTIVE_STEER / DIAGNOSTIC_ROUTE_REJECTED`.
Evidence class: `PROCESS_TABLE + UNITY_LOG_TEXT + PRIOR_SCREENSHOT_REJECTION + CONTROLLER_MATRIX`.

No Unity command, build, import, Play Mode, profiler, scene save, prefab save, material save, project-setting mutation, runtime source mutation, or raw YAML edit was performed by this steer.

## Observed Active Process

At controller refresh, a separate Unity batch process was active:

- executable: `Unity.exe`
- command: `-batchmode -projectPath C:\hades\Hecton8 -executeMethod Hecton8.Editor.H8VisualProofCapture1912.CaptureSurfaceCrestRecoveryProbeAndExit`
- log: `Docs/Logs/UnityCaptureSurfaceCrestActualTerrainProbeI_20260606_011429.log`

The process gate was red: Unity, dotnet, ILPP, PackageManager, and ShaderCompiler-related processes were active, and CPU samples were at 100 percent.

## Hard Verdict

This active batch route is not acceptance proof.

`H8VisualProofCapture1912.CaptureSurfaceCrestRecoveryProbeAndExit` is already classified as diagnostic rejection-only because it mutates or depends on editor-only scene/render state and writes raw `h8_1914` output instead of a canonical h8_1475 packet.

Any output from this run must be labelled:

- `DIAGNOSTIC_ONLY`
- `NOT_H8_1475`
- `NOT_NO_MUTATION_PROOF`
- `NOT_PRODUCT_VISUAL_ACCEPTANCE`

## Current Log Concerns

The active log already shows:

- long script compilation and IL post-processing;
- domain reload over 100 seconds;
- repeated skipped invalid test/sample assemblies;
- script compilation requested because AssetDatabase observed script-compilation-related changes;
- the same rejected capture method as the execution target.

This is not a clean proof window.

## Required Owner Response

1. Let the active batch finish only if stopping it would risk editor/project state. Do not start another Unity process in parallel.
2. Do not claim the resulting screenshot as progress unless it is explicitly filed as diagnostic rejection evidence.
3. Do not overwrite or reuse `Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.*` as proof.
4. After process gate clears, run no-mutation readback first, not another color/haze/card A/B.
5. Only canonical h8_1475 proof packet may be considered for acceptance, and only after active player/HUD/tool route is known.

## Blocking Links

- `Docs/Orchestration/SURFACE_AUTHORITATIVE_ROUTE_RECOVERY_MATRIX_20260605.md`
- `Docs/Orchestration/H8_1475_PROOF_TOOL_INTEGRITY_SYNTHESIS_20260605.md`
- `Docs/Orchestration/MAPMAGIC_HYDRAULIC_EROSION_JOB_SAFETY_STATIC_REVIEW_20260606.md`
- `taskslocal/night_controller_20260605/NIGHT_OWNER_01_UNITY_GATE_READBACK.txt`
- `taskslocal/night_controller_20260605/NIGHT_OWNER_02_SURFACE_AUTHORITATIVE_ROUTE.txt`
- `taskslocal/night_controller_20260605/NIGHT_OWNER_04_H8_1475_FALSE_PROOF_BLOCKER.txt`

Final status: `REJECTED_AS_ACCEPTANCE / WAIT_FOR_PROCESS_GATE`.
