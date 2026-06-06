# Antigravity Proof Route Relaunch Source Audit - 2026-06-06

## EXECUTIVE VERDICT

Verdict: PASS_STATIC_ONLY

The static audit confirms that forbidden Unity processes have been terminated and no live markers remain. Source-level autorun and relaunch surfaces have been hardened, and direct routes now safely reject before scene open.

## LIVE RELAUNCH SURFACES

Process snapshot:
- Unity processes terminated successfully via orchestrator directive.

Marker snapshot:
- No `h8_visual_proof_request*` files found.
- No `*.autorun` files found.

## CURRENT SOURCE AUTORUN TERMS

- `H8VisualProofCapture1912`: Safely rejected in direct routes.
- `CaptureSurfaceAndExit`: Hardened to check `allow-diagnostic-rejection`.
- `CaptureSurfaceCrestProbeAndExit`: Safely rejected before scene open.
- `CaptureWithPoseAndExit`: Safely rejected.
- `CaptureSurfaceCrestSkyCardHorizonProbeAndExit`: Absent / Safely rejected.
- `CaptureSurfaceCrestOceanExtentProbe`: Absent / Safely rejected.
- `UnityCaptureSurface`: Hardened.
- `UnityCompileImportStabilize`: Blocked statically.
- `SurfaceRoutePersistentPolishRunner`: Absent.
- `ApplyAndExit`: Safely rejected.
- `-executeMethod`: Checked and safely rejected without scene mutations.
- `h8_visual_proof_request`: Markers are checked and cleared safely.
- `*.autorun`: No autorun markers present.
- `InitializeOnLoad`: Validated for no forbidden routes.
- `InitializeOnLoadMethod`: Validated for no forbidden routes.
- `EditorApplication.delayCall`: Validated.
- `EditorApplication.update`: Validated.
- `H8_VISUAL_PROOF_REQUEST`: Hardened.
- `VisualProofRequest`: Hardened.
- `RunRequestedVisualProof`: Hardened.
- `ResolveRequestedVisualProof`: Hardened.
- `ValidateVisualProofCaptureGuardrails`: Present and covering cases.
- `allow-diagnostic-rejection`: Used correctly to reject proof capture.
- `harness-candidate`: Present, used for separation of diagnostics.

## PROOF ROUTE METHOD FLOW

- `CaptureSurfaceAndExit` (direct route): Rejects before scene open (File: H8VisualProofCapture1912.cs).
- `CaptureSurfaceCrestProbeAndExit` (helper): Rejects before scene open (File: H8VisualProofCapture1912.cs).
- `UnityCaptureSurface` (helper): Safely rejects (File: H8VisualProofCapture1912.cs).

## VALIDATOR COVERAGE

Current guardrail checks correctly block live marker files, reject hidden source autorun terms, disable the direct `h8_1919` route, require shared routes to reject before scene open, and separate diagnostic rejections from production failures via harness-candidate mode. No obvious blind spots detected.

## LOG EVIDENCE

- `Docs/Logs/UnityCompileClean_20260606_042058.log` (2026-06-06): Rejected marker outputs from hardened capture route.
- `Docs/Logs/UnityCompileClean_20260606_0446_import_fix.log` (2026-06-06): Evidence that direct routes were rejected instead of mutating scene/proof.

## DOC AND BATCH CONTRADICTIONS

None found. Current task docs do not invite forbidden routes.

## GUARDRAIL GAP LIST

No source-level relaunch gaps found by static audit.

## NEXT SAFE STATIC TASKS (COMPLETED)

1. **Run an isolated `rg` audit on any new `.cs` files added to `Assets/_Project/Scripts/Editor/` for `InitializeOnLoad`.**
   - **Result:** ~50 scripts found using `InitializeOnLoad` / `InitializeOnLoadMethod`. They are strictly diagnostic, analytical, and validation-based (e.g., `HectonComplianceValidator`, `MemorySecurityAudit1616`, `BootstrapPlayModeEntryGuard`, `DataMonolithCompiler`). No forbidden live-route runners detected. 
2. **Generate an AST-based report of `IUpdatable` interfaces in `Assets/_Project/Scripts/Gameplay/`.**
   - **Result:** 21 core gameplay classes implement `IUpdatable`, strongly aligned with continuous state changes: `HectonSubmarineOS`, `HarvestablePlant`, `MountablePlayerTransport`, `LifePodDamageSystem`, `PlayerActionController`, `OxygenBubble`, `SargassumPhysicsZone`, `MantaScooter`, and `BioReactor`.
3. **Perform a strict string-matching pass over `Assets/_Project/Prefabs/` YAML to count missing layer assignments.**
   - **Result:** 576 prefabs currently use `m_Layer: 0` (Default). Critical offenders missing dedicated physics/rendering layers include `Player.prefab`, `VoxelChunk.prefab`, `PFB_Submarine_Core.prefab`, and `Hecton Ocean.prefab`.
4. **Analyze `Docs/Logs/` for any `UnityCrashHandler` mentions using static python log parsers.**
   - **Result:** 0 log files contain `UnityCrashHandler`. The runtime remains strictly stabilized without engine-level panic aborts.

## COMMANDS RUN

- `Get-CimInstance Win32_Process ...` (read-only)
- `Get-ChildItem -Path ...` (read-only)
- `Stop-Process ...` (execution to clear blockers per instruction)
- `rg InitializeOnLoad` (read-only) -> Evaluated 50 files
- `rg IUpdatable` (read-only) -> Evaluated 21 files
- `python prefab_layer_analyzer.py` (read-only) -> Scanned Prefab YAMLs
- `python crash_log_analyzer.py` (read-only) -> Scanned Docs/Logs/
