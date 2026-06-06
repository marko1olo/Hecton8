# Unity Surface Crest Capture Observation - 2026-06-06

Status: `UNITY_LOG_OBSERVATION / SOURCE_COMPILE_FIX_APPLIED / TERMINALOS_ASMDEF_FIX_PRESENT / H8_1914_AUTORUN_DISABLED / LATEST_COMPILE_ONLY_PASS_AFTER_HOOK_FIX / EXTERNAL_PROBE_LOOP_STOPPED / H8_1914_DIAGNOSTIC_ONLY`.
Evidence class: `UNITY_BATCH_LOG_READ + STATIC_SOURCE_FIX + STATIC_SCREENSHOT_REVIEW + STATIC_METADATA_READ + PROCESS_CONTROL_LOG`.

## Scope

This report started by observing Unity batchmode processes that were already running. The controller later launched controlled Unity batch compile attempts after the Unity-family process gate was clear. The first controlled attempt exited before compilation due to Unity licensing/access-token initialization failure. A later controlled compile reached Tundra and produced clean compile-success markers, then Unity/ILPP did not quit cleanly and was stopped after log growth ceased. The controller did not start Play Mode, run a player build, run profiler, or accept generated screenshots as product proof.

The controller made one source fix in `Assets/_Project/Scripts/SeamGapDitherRenderer.cs`: removed a stale `_registeredToDispatcher = false;` assignment left after the file had already dropped `IUpdatable`, `Tick(float)`, and the `_registeredToDispatcher` field.

## Commands And Artifacts

| Evidence | Result |
| --- | --- |
| Current Unity command line | External batchmode: `Hecton8.Editor.H8VisualProofCapture1912.CaptureSurfaceCrestRecoveryProbeAndExit`, log `Docs\Logs\UnityCaptureSurfaceCrestActualTerrainProbeO_20260606_030119.log`. |
| `Docs\Logs\UnityCaptureSurfaceCrestActualTerrainProbeO_20260606_030119.log` | First ScriptAssemblies pass succeeded; later reload failed on `SeamGapDitherRenderer.cs(322,21)` before the controller patch; the batch still opened the scene and wrote h8_1914 diagnostic artifacts. |
| `rg -n "_registeredToDispatcher\|IUpdatable\|void Tick\(" Assets/_Project/Scripts/SeamGapDitherRenderer.cs` | Exit 1, no matches after controller patch. |
| `git diff --check -- Assets/_Project/Scripts/SeamGapDitherRenderer.cs` | Exit 0, CRLF warning only. |
| `Docs\Screenshots\MCP\h8_1914_surface_crest_recovery_probe.png` | Written by the external Unity batch, 1,368,988 bytes. Static visual review only. |
| `Docs\Screenshots\MCP\h8_1914_surface_crest_recovery_probe.txt` | Written by the external Unity batch, 11,417 bytes. Static metadata read only. |
| `Docs\Logs\UnityCaptureSurfaceCrestActualTerrainProbeP_20260606_032005.log` | External ProbeP stalled after script compilation request with no `ExitCode`, no scene load, and no artifact write. |
| `Docs\Logs\UnityCaptureSurfaceCrestActualTerrainProbeQ_20260606_032559.log` | External ProbeQ repeated the same stalled capture route before the controller stopped the parent powershell task and Unity child processes. |
| `Docs\Logs\UnityCaptureSurfaceCrestActualTerrainProbeS_20260606_034800.log` | External ProbeS showed the same capture-loop pattern. The controller stopped all remaining capture-route parent powershell jobs and Unity-family child processes. |
| `Docs\Logs\UnityCaptureSurfaceCrestActualTerrainProbe_20260606_052914.log` | External h8_1914 route relaunched after the clean compile. Controller stopped Unity PID 3396 plus ILPP/UnityAutoQuitter children. Terrain evidence validator rejected it with `blockers=9`. |
| `Docs\Logs\UnityCaptureSurfaceCrestActualTerrainProbe_20260606_053214.log` | External h8_1914 route relaunched again. It reached one compile-phase Tundra success and `ExitCode: 0`, but did not produce accepted capture artifacts; terrain evidence validator rejected it with `blockers=9`. |
| `Docs\Logs\UnityCaptureSurfaceCrestActualTerrainProbe_20260606_053625.log` | External h8_1914 route relaunched again. Controller stopped it before useful compile/capture evidence; terrain evidence validator rejected it with `blockers=9`. |
| `Docs\Logs\UnityCaptureSurfaceCrestActualTerrainProbe_20260606_054049.log` | External h8_1914 route relaunched again and failed with return code 1. It recorded stale/moving snapshot `CS0246` rows for `DecryptionPuzzleDTO` and `DecryptionKnobInputDTO`; latest compile-only proof contains no related scoped blocker rows. Terrain evidence validator rejected it with `blockers=11`. |
| `Docs\Logs\UnityCaptureSurfaceCrestActualTerrainProbe_20260606_054423.log` | External h8_1914 route relaunched again. Controller stopped the rogue child tree; terrain evidence validator rejected it with `blockers=9`. |
| `Docs\Logs\UnityCaptureSurfaceCrestActualTerrainProbe_20260606_054748.log` | External h8_1914 route relaunched again, reached Tundra success, and wrote h8_1914 screenshot/metadata. Terrain evidence validator still rejected it with `blockers=9`, including Unity `MemoryLeaks`, diagnostic h8_1914 metadata, disabled/unlinked MapMagic route, and non-eroded terrain outputs. |
| `Docs\Logs\UnityCaptureSurfaceCrestActualTerrainProbe_20260606_060418.log` | External h8_1914 route relaunched and failed with return code 1 on `TerminalOsLayoutValidator.cs` / `OscilloscopeDecryptionTunerWindow.cs` missing `DecryptionPuzzleDTO` / `DecryptionKnobInputDTO` through the editor asmdef reference route. Controller patched `Hecton8.UI.TerminalOS.Editor.asmdef` to reference `Hecton8.Core.Contracts`. |
| `Docs\Logs\UnityCaptureSurfaceCrestActualTerrainProbe_20260606_061409.log` | External h8_1914 route relaunched after the asmdef patch, reached `Tundra build success`, and wrote h8_1914 screenshot/metadata. Terrain evidence validator rejected it with `blockers=9`; controller stopped the Unity-family process tree. Diagnostic only. |
| `Docs\Logs\UnityCaptureSurfaceCrestActualTerrainProbe_EditorGPU_20260606_062050.log` | External h8_1914 route relaunched through the EditorGPU path, reached `Tundra build success`, and wrote h8_1914 screenshot/metadata. `Tools\ValidateTerrainProbeEvidence.py --require-production` rejected it with `blockers=9`; controller stopped compiler leftovers. Diagnostic only. |
| `Docs\Logs\UnityCaptureSurfaceNoTerrainShell_EditorGPU_20260606_062938.log` | External h8_1914 route relaunched through the NoTerrainShell EditorGPU path, reached `Tundra build success`, but was stopped before complete capture. `Tools\ValidateTerrainProbeEvidence.py --require-production` rejected it with `blockers=9`, including `capture-output-missing`. Diagnostic only. |
| `Docs\Logs\UnityCaptureSurfaceNoTerrainShell_EditorGPU_20260606_063306.log` | External h8_1914 route relaunched again through NoTerrainShell, reached `Tundra build success`, but was stopped before complete capture. `Tools\ValidateTerrainProbeEvidence.py --require-production` rejected it with `blockers=9`, including `capture-output-missing`. Diagnostic only. |
| `Docs\Logs\UnityCaptureSurfaceNoTerrainShell_AutorunEditorGPU_20260606_063601.log` | Unity launched through an `[InitializeOnLoadMethod]` autorun hook and marker file in `H8VisualProofCapture1912.cs`; controller stopped it, removed the hook, and deleted the marker. Process-cleanup evidence only. |
| `Docs\Logs\UnityCompileAutorunHook_20260606_064114.log` | Compile-only batch after autorun-hook removal reached `Tundra build success` and final Unity return code 0. Scoped scan found no compile-error markers. Compile/import evidence only; not runtime, terrain, or acceptance proof. |
| `Docs\Logs\UnityCompile_SeamGapDitherRendererFix_20260606_032610.log` | Controller controlled compile attempt exited code 1 before compilation; log shows licensing validation/access-token failure and no compiler diagnostics. |
| `Docs\Logs\UnityCompileAfterProofPatch_20260606_033000.log` | Controller controlled compile reached Tundra twice: `ExitCode: 0` at lines 270 and 1797, Tundra success at lines 1789 and 2722, no `error CS`/Tundra-failed markers. Unity/ILPP was stopped after it stopped writing the log. |
| `Docs\Logs\UnityCompileClean_20260606_042058.log` | Later compile refresh: initial Tundra success, then moving-worktree `HazardZoneManager` telemetry-method `CS0103`; current disk source contains those methods. |
| `Docs\Logs\UnityCompileClean_20260606_042751.log` | Later compile refresh: stale/moving snapshot failed on missing `DecryptionPuzzleDTO`, `DecryptionKnobInputDTO`, and `BinaryBlittableSafe` imports. |
| `Docs\Logs\UnityCompileClean_20260606_0446_import_fix.log` | Latest compile refresh: no scoped old source blockers; Csc progressed through source assemblies, then final failure at `Unity.ILPP.Trigger.exe` `ExitCode -1` after the system-error dialog. |
| `C:\hades\.codex_ops\logs\UnityCompileClean_20260606_051745_stable_import.log` and `Docs\Logs\UnityCompileClean_20260606_051745_stable_import.log` | Superseding compile refresh: Tundra success at lines 1240, 2174, and 2187, final Unity return code 0 at line 2521, and no scoped old source blocker markers. |

## Log Markers

- Line 273: `ExitCode: 0 Duration: 24s`.
- Line 296: `*** Tundra build success (24.07 seconds), 6 items updated, 3801 evaluated`.
- Line 406: `ExitCode: 0 Duration: 7m:43s`.
- Line 1715: `*** Tundra build success (463.24 seconds - 0:07:43), 442 items updated, 3801 evaluated`.
- Lines 1761, 3898, 3900: `CS0103` for `_registeredToDispatcher`.
- Line 3903: `Reloading assemblies failed`.
- Line 4110: scene `Assets/_Project/Scenes/02_HECTON_WORLD.unity` loaded.
- Lines 4116 and 4129: h8_1914 PNG and TXT artifacts written.

ProbeP/ProbeQ/ProbeS markers:

- ProbeP stopped at script compilation request with no `ExitCode` marker and no scene/capture markers.
- ProbeQ was launched by parent `powershell.exe` PID 7748 with the same `CaptureSurfaceCrestRecoveryProbeAndExit` command. The controller stopped PID 7748 and Unity child processes after the loop repeated the stalled capture route.
- ProbeS was launched by parent `powershell.exe` PID 1352 with the same capture route. The controller stopped the remaining capture-route parent jobs and confirmed the Unity-family process gate was empty afterward.

Controlled compile markers:

- `UnityCompile_SeamGapDitherRendererFix_20260606_032610.log` exits with return code 1 before project compilation.
- The log contains `LicensingClient has failed validation` and `Error: Access token is unavailable; failed to update`.
- The log contains no `error CS`, no `Tundra build success`, and no `Tundra build failed`.
- `UnityCompileAfterProofPatch_20260606_033000.log` contains `ExitCode: 0` at lines 270 and 1797.
- `UnityCompileAfterProofPatch_20260606_033000.log` contains Tundra build success at lines 1789 and 2722.
- Scoped error scan over `UnityCompileAfterProofPatch_20260606_033000.log` emitted no `error CS`, `Tundra build failed`, `Compilation failed`, `Scripts have compiler errors`, or `Reloading assemblies failed` rows.
- `Library/ScriptAssemblies/Hecton8.Core.dll` updated at 2026-06-06 03:35:56 local time.
- Line 4173: Unity `MemoryLeaks` payload.

## Findings

### Finding 1 - Medium - Corrected Source Compiled, Runtime Proof Still Missing

The stale `_registeredToDispatcher` reference is removed from source text, and a scoped `rg` scan found no remaining symbol reference in `SeamGapDitherRenderer.cs`. The ProbeO Unity log still contains the old compiler failure, but the later controlled compile log `UnityCompileAfterProofPatch_20260606_033000.log` reached Tundra successfully twice and emitted no scoped C# error markers. This is compile evidence only; it is not Play Mode, runtime, profiler, or visual acceptance proof.

### Finding 2 - High - h8_1914 Remains Diagnostic-Only

The generated screenshot and metadata are useful readback, not product acceptance. The metadata says `captureTruth=surface_actual_terrain_crest_recovery_probe_editor_only_unsaved`. The route is still h8_1914, not h8_1475, and cannot replace canonical visual acceptance.

### Finding 3 - High - MapMagic Readback Shows A Bypassed Graph

The metadata reports MapMagic tiles ready and one height output, but the graph remains in a bypassed diagnostic state: erosion node disabled, anomaly node disabled, `splat.sedimentIn=UNLINKED`, and `anomaly.heightIn=UNLINKED`.

### Finding 4 - Medium - Static Visual Review Shows Improvement But Still Rejects Acceptance

The screenshot shows a visible Aegir, sky/clouds, island silhouette, terrain material, and shoreline elements. It also shows a broad flat rectangular water/terrain band and incomplete product composition. Treat it as negative diagnostic routing evidence, not approval.

### Finding 5 - Medium - External Probe Loop Was Stopped

ProbeP, ProbeQ, ProbeS, and the later `UnityCaptureSurfaceCrestActualTerrainProbe_20260606_052914.log` through `_054748.log` relaunches repeated the same h8_1914 capture route. The controller stopped the parent powershell probe runners, Unity child processes, and competing Codex child shells to clear the process gate. This is process cleanup and rejection evidence, not proof.

### Finding 5 - High - MemoryLeaks Payload Blocks Any Clean-Proof Claim

ProbeO emits a Unity `MemoryLeaks` payload after writing the diagnostic artifacts. This blocks compile/import, terrain-generation, and h8_1475 proof claims until a fresh proof log has no `MemoryLeaks` payload and no dirty compile/import markers.

### Finding 6 - Medium - Current Source Fix Has Adjacent Lifecycle Risks

Planck static recheck confirms current `SeamGapDitherRenderer.cs` no longer contains `_registeredToDispatcher`, and current line 322 is `_registeredLateFrame = false;`. That supports the stale moving-worktree compile classification.

Static risks remain:

- dispatcher replacement resets `_registeredLateFrame` without proving unregister against the previous dispatcher/lane;
- `DisableLegacyGapDitherIfNeeded()` can run from the visual-sync path and use hierarchy `transform.Find(...)` on a throttled cadence;
- list-copy GC safety depends on registry copy methods not growing the preallocated lists.

These are not emergency patch instructions under a red process gate. They are required runtime/profiler/GC proof items after a clean compile/import.

### Finding 7 - Medium - Latest Full Unity Compile Passed, Runtime Proof Still Missing

`UnityCompileClean_20260606_0446_import_fix.log` temporarily failed on `Unity.ILPP.Trigger.exe` `ExitCode -1`, but the later `UnityCompileClean_20260606_051745_stable_import.log` supersedes it with three Tundra success markers and final Unity return code 0. This proves current C# compile/import readiness for the scoped blockers only; it is still not Play Mode, terrain generation, profiler, visual acceptance, or h8_1475 proof.

### Finding 8 - Medium - TerminalOS Editor Contract Reference Was Patched After A Later Probe

`UnityCaptureSurfaceCrestActualTerrainProbe_20260606_060418.log` exposed a new editor asmdef reference blocker after `DecryptionPuzzleDTO` and `DecryptionKnobInputDTO` moved to `Hecton8.Core.Contracts`. The controller added `Hecton8.Core.Contracts` to `Hecton8.UI.TerminalOS.Editor.asmdef`. Later external h8_1914 logs `_061409`, `EditorGPU_062050`, `NoTerrainShell_062938`, and `NoTerrainShell_063306` reached Tundra success after that patch, but because they are rejected h8_1914 routes and were stopped or cleaned up by process control, they are diagnostic compile-stage evidence only. The autorun marker/hook was then removed and `UnityCompileAutorunHook_20260606_064114.log` provides compile-only proof for the latest source state.

## Required Next Gates

1. Keep h8_1914 artifacts out of h8_1475 acceptance packets.
2. Require explicit owner decision for the serialized scene and MapMagic graph mutations.
3. Require runtime/player/visual proof before any product acceptance claim.
4. Do not treat the forced Unity/ILPP cleanup as a clean Editor quit artifact.
5. Require no Unity `MemoryLeaks` payload in the fresh proof log.
6. Verify `SeamGapDitherRenderer` dispatcher hot-swap unregister ownership and visual-sync `transform.Find(...)` cost before accepting the lane.
7. Run a fresh controlled Unity compile/import pass only if source moves again after `UnityCompileAutorunHook_20260606_064114.log` or if runtime/terrain proof is explicitly being collected.

## Final Status

`TERMINALOS_ASMDEF_FIX_PRESENT / H8_1914_AUTORUN_DISABLED / LATEST_COMPILE_ONLY_PASS_AFTER_HOOK_FIX / EXTERNAL_PROBE_LOOP_STOPPED / H8_1914_DIAGNOSTIC_REJECTED / NO_RUNTIME_OR_ACCEPTANCE_PROOF`
