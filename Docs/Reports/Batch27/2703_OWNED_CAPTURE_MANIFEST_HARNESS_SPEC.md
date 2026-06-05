# Batch27 Worker 2703 - Owned Capture Manifest Harness Spec

## Scope

Task file: `C:\hades\Hecton8\taskslocal\batch27_runtime_proof_recovery\2703_OWNED_CAPTURE_MANIFEST_HARNESS_SPEC.txt`

Evidence class: `STATIC VERIFIED` for source and document inspection. Runtime capture acceptance remains `PENDING RUNTIME PROOF`.

This report specifies a HECTON-owned capture and manifest harness for the next proof packet. It does not claim packet 1475 is captured. No Unity Editor run, Play Mode run, dotnet build, asset import, process kill, or project file edit was performed.

## Authority Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `camera.md`
- `presentation.md`
- `rendering.md`
- `water.md`
- `quality.md`
- `Docs/ARCHITECTURE/FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`
- `Docs/Reports/Batch26/2601_CAPTURE_HARNESS_DEPTH_METADATA_AUDIT.md`
- `Docs/Reports/Batch26/2606_PROOF_WATCHDOG_PROCESS_HYGIENE_AUDIT.md`

`Docs/Actual Domains of Project.txt` was missing. Narrow inferred domain: capture, camera, presentation, rendering, water, quality, and proof hygiene.

## Mandates Applied

- `QA_Evidence_Text_Filter_Audit.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Signal_Lane_Segregation.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`

These mandates force the harness to separate static evidence from runtime proof, use cold owner registration and typed signals, avoid hot scene searches, avoid GC in runtime phases, and reject generic screenshot files without route/depth/quality/log proof.

## Static Findings

Current first-party dev capture route:

- `Assets/_Project/Editor/HectonDevToolsMenu.cs` has a Play Mode menu item that writes `Docs/Screenshots/screenshot-{timestamp}.png` through `ScreenCapture.CaptureScreenshot`.
- It creates a PNG path and logs the path only.
- It does not write a manifest.
- It does not bind scene, route, depth, camera transform, water state, quality weight, render scale, post stack, UI state, log window, or SHA256.

Current MCP package capture route:

- `Library/PackageCache/com.coplaydev.unity-mcp@fbdb152757bd/Runtime/Helpers/ScreenshotUtility.cs` returns `FullPath`, `AssetsRelativePath`, `SuperSize`, `IsAsync`, `ImageWidth`, and `ImageHeight`.
- `Editor/Tools/ManageScene.cs` screenshot action supports `game_view`, `scene_view`, `surround`, and `orbit` captures.
- `Editor/Tools/Cameras/ManageCamera.cs` delegates screenshot actions to `ManageScene`.
- These routes can emit image files and optional inline images, but they do not emit HECTON route/depth/quality/log proof.
- MCP capture can schedule AssetDatabase import when writing under `Assets`; proof captures must not depend on asset import or screenshot files under `Assets`.

Current owner source evidence:

- `GlobalRegistry` exposes cold references for `FluidSurfaceCurrent`, `HectonUnderwaterVisuals`, `DynamicResolution`, `DynamicResolutionRuntime`, `DepthZone`, and `DepthZoneReadModel`.
- `HectonUnderwaterVisuals` owns underwater visual state and registers with `GlobalRegistry`, but the useful proof values are private/debug-oriented. No public owned proof snapshot read model was found.
- `DepthZoneDirector` implements `IDepthZoneReadModel` and exposes `CurrentZone`, but exact current depth and predicate-grade depth proof are not exposed through a public snapshot.
- `DynamicResolutionScaler` implements `IDynamicResolutionRuntime` and already exposes `CurrentRenderScale`, `CurrentRenderScale01`, `TargetRenderScale01`, and `TryGetSnapshot`.
- `HomeostasisBrain` exposes continuous `GlobalQualityWeight` and hardware/scalability snapshots.
- `SystemDispatcher` publishes typed `CameraPositionSignal` and `CameraFrustumSignal`, but no proof route owner/view-id source was found.

Conclusion: current image capture exists, but current proof capture does not. Packet 1474-style generic screenshots remain rejectable as `FALSE_LABEL` and `MISSING_METADATA`.

## Proposed Files And Classes

Runtime contracts, no Unity Editor dependencies:

- `Assets/_Project/Scripts/Proof/Capture/HectonProofCaptureContracts.cs`
  - `HectonProofCaptureManifest`
  - `HectonProofScreenshotRecord`
  - `HectonProofRoutePredicate`
  - `HectonProofRouteSnapshot`
  - `HectonUnderwaterProofSnapshot`
  - `HectonDepthZoneProofSnapshot`
  - `HectonQualityProofSnapshot`
  - `HectonLogWindowProof`
  - `HectonProofRejectCode`

- `Assets/_Project/Scripts/Proof/Capture/IHectonProofReadModels.cs`
  - `IHectonProofRouteReadModel.TryGetRouteCaptureSnapshot(out HectonProofRouteSnapshot snapshot)`
  - `IUnderwaterVisualProofReadModel.TryGetUnderwaterProofSnapshot(out HectonUnderwaterProofSnapshot snapshot)`
  - `IDepthZoneProofReadModel.TryGetDepthZoneProofSnapshot(out HectonDepthZoneProofSnapshot snapshot)`

Runtime owner additions needed later:

- `HectonUnderwaterVisuals`
  - Implement `IUnderwaterVisualProofReadModel`.
  - Snapshot must be read-only and side-effect free.
  - Include water surface height, camera visual depth, underwater active flag, fog density/color, caustics strength, mote emission, route owner frame, and visual state hash.

- `DepthZoneDirector`
  - Implement `IDepthZoneProofReadModel`.
  - Snapshot must include current depth meters, current zone id/name, zone hash, transition state, and source frame.
  - `CurrentZone` alone is insufficient for exact view predicates.

- `HectonProofRouteCaptureRig`
  - Runtime/editor-safe route owner for packet captures.
  - Owns the seven view IDs below and authored camera anchor transforms.
  - Publishes immutable proof route snapshots.
  - Must not search the scene during capture.

Editor harness, cold path only:

- `Assets/_Project/Editor/ProofCapture/HectonOwnedProofCaptureWindow.cs`
  - Menu/window entry point.
  - Preflight only. No automatic Play Mode launch.

- `Assets/_Project/Editor/ProofCapture/HectonOwnedProofCaptureHarness.cs`
  - Orchestrates preflight, per-view predicate checks, screenshot request, hash, manifest, and post-log gate.
  - Rejects before capture when route/depth/quality owners are unavailable.

- `Assets/_Project/Editor/ProofCapture/HectonOwnedProofManifestWriter.cs`
  - Writes deterministic JSON to `Docs/Screenshots/HectonProofPackets/{packet_id}/manifest.json`.
  - Writes reject manifests with explicit reject codes.

- `Assets/_Project/Editor/ProofCapture/HectonProofLogWindowValidator.cs`
  - Binds current Editor log path.
  - Validates clean pre-capture and 60 second post-capture log window.
  - Copies the validated log to the packet folder only after the clean gate passes.

- `Assets/_Project/Editor/ProofCapture/HectonProofScreenshotHasher.cs`
  - Computes SHA256, byte size, UTC/local timestamps, and PNG dimensions from file bytes.
  - Must not import screenshots into `Assets`.

- `Assets/_Project/Editor/ProofCapture/HectonProofRoutePredicateEvaluator.cs`
  - Evaluates per-view predicates against owner snapshots.
  - Writes pass/fail details into the manifest.

Optional menu integration:

- Add only a thin menu call in `Assets/_Project/Editor/HectonDevToolsMenu.cs` after the harness exists.
- Existing raw screenshot item must remain labeled raw/dev and must not imply proof capture.

## Output Location

Owned proof packet root:

`Docs/Screenshots/HectonProofPackets/h8_1475_{session_id}/`

Required artifacts:

- `manifest.json`
- `screenshots/{view_index}_{view_id}.png`
- `UnityEditor_{packet_id}_{session_id}.log`
- `manifest.sha256`

Rejected runs keep their packet folder but must carry:

- `final_disposition: REJECTED`
- `reject_codes: [...]`
- `may_submit_as_runtime_proof: false`

## Required View IDs And Predicates

Six production views are required. A seventh diagnostic view is required only as a harness self-check and must not substitute for any production view.

1. `surface_coast_aegir_ui_off`
   - UI hidden except unavoidable engine/harness watermark state recorded as `ui_visible: false`.
   - Camera visual depth is above surface or within dry tolerance.
   - Coastline, ocean surface, sky, and Aegir are visible by authored route predicate.
   - Not allowed to use darkness, fog, crop, or storm state to hide surface/water weakness.

2. `shoreline_close_1m`
   - Authored route shoreline distance is `<= 1.25m`.
   - Water surface, wet edge/shore transition, and coastline route context must be in frame.
   - Camera may be near surface but must record water surface height and camera height/depth.

3. `underwater_0_5m`
   - Camera visual depth is `> 0m` and `<= 5m`.
   - Underwater visual owner reports underwater active.
   - Photic shallows must remain readable. Darkness-only acceptance is forbidden.
   - Snapshot must include fog, caustics, motes, depth, and quality weight.

4. `underwater_20_50m_route`
   - Camera visual depth is `>= 20m` and `<= 50m`.
   - Route owner reports the intended route state and authored forward path cue.
   - Depth zone snapshot must agree with visual depth band.
   - Medium-depth hero route must not be hidden by muddy fog or black-screen noir.

5. `aegir_celestial_long`
   - Aegir/celestial route predicate must be true from the route owner.
   - Camera transform, FOV, and route anchor id must match the authored long-view spec.
   - Sky and ocean context must be visible; it is not a crop-only moon screenshot.

6. `regression_low_oblique`
   - Camera pitch and route anchor identify low oblique surface/regression view.
   - Captures coastline, ocean surface, and route-readable geometry.
   - Used to detect flat water, primitive terrain, muddy sky, and presentation regressions.

7. `proof_debug_overlay_route_state`
   - Diagnostic-only capture.
   - UI/debug overlay may be visible.
   - Must show route id, depth, zone, GlobalQualityWeight qNNN label, render scale, and harness session id.
   - If this is used to replace one of the six production views, the manifest must reject the packet.

## Manifest Schema

Top-level fields:

- `schema_name`
- `schema_version`
- `harness_name`
- `harness_version`
- `packet_id`
- `session_id`
- `created_utc`
- `created_local`
- `project_root`
- `unity_version`
- `active_scene`
- `loaded_scenes`
- `first_20_minutes_moment`
- `evidence_class`
- `final_disposition`
- `may_submit_as_runtime_proof`
- `reject_codes`
- `output_root`
- `manifest_path`
- `log_path`
- `log_sha256`
- `log_window_start_utc`
- `log_window_end_utc`
- `post_capture_clean_seconds`
- `forbidden_log_tokens`
- `global_quality_weight`
- `global_quality_label`
- `dynamic_resolution_snapshot`
- `hardware_dictator_snapshot_hash`
- `route_owner_name`
- `route_owner_version`
- `route_session_id`
- `camera_source`
- `ui_policy`
- `screenshots`
- `derived_checks`

Per-screenshot fields:

- `view_index`
- `view_id`
- `production_view`
- `diagnostic_view`
- `file_path`
- `file_name`
- `sha256`
- `byte_size`
- `png_width`
- `png_height`
- `capture_requested_utc`
- `file_created_utc`
- `file_last_write_utc`
- `capture_source`
- `camera_name`
- `camera_instance_id`
- `camera_position_world`
- `camera_rotation_euler`
- `camera_forward`
- `field_of_view_degrees`
- `near_clip_meters`
- `far_clip_meters`
- `route_anchor_id`
- `route_state_id`
- `route_state_hash`
- `route_predicate_version`
- `route_predicate_pass`
- `route_predicate_failures`
- `water_surface_height`
- `camera_visual_depth_meters`
- `player_or_proxy_depth_meters`
- `depth_zone_id`
- `depth_zone_name`
- `depth_zone_hash`
- `depth_predicate_pass`
- `underwater_active`
- `underwater_visual_state_hash`
- `fog_density`
- `fog_color`
- `caustics_strength`
- `suspended_motes_emission`
- `global_quality_weight`
- `global_quality_label`
- `render_scale_current`
- `render_scale_target`
- `post_stack_hash`
- `ui_visible`
- `owner_snapshot_frames`
- `log_offset_or_timestamp_at_capture`

Derived checks:

- `all_required_views_present`
- `all_required_views_unique`
- `all_required_views_have_sha256`
- `all_production_views_ui_policy_pass`
- `all_depth_predicates_pass`
- `all_route_predicates_pass`
- `quality_weight_is_continuous_float`
- `no_binary_quality_switch_detected`
- `post_capture_log_window_clean`
- `manifest_written_after_final_screenshot`
- `log_last_write_after_final_screenshot`
- `screenshots_outside_assets_folder`
- `no_asset_import_dependency`
- `packet_acceptance_label`

## Failure Behavior

Pre-capture failures:

- Do not capture PNGs.
- Write a reject manifest only.
- Use `final_disposition: REJECTED_PRECAPTURE`.
- Use `may_submit_as_runtime_proof: false`.

Per-view predicate failures before screenshot:

- Do not capture that view.
- Abort remaining production capture unless explicitly running diagnostic mode.
- Write reject manifest with exact predicate failures.

File/hash failures after screenshot:

- Keep the file for forensic review.
- Mark screenshot and manifest rejected.
- Do not submit packet as runtime proof.

Post-capture log failures:

- Keep screenshots.
- Mark manifest rejected.
- Use reject code `POST_CAPTURE_LOG_DIRTY`.
- Do not relabel packet as verified later without a new clean run.

False-label prevention:

- The harness writes the packet acceptance label.
- Humans and external agents may not relabel raw screenshots as `PLAYER-CAPTURE VERIFIED` without `final_disposition: ACCEPTED_BY_HARNESS`.
- Static reports can only say `STATIC VERIFIED` or `PENDING RUNTIME PROOF`.

## Clean Log Window Gate

The harness must implement the Batch26 2606 minimum:

1. Start from a clean Unity session.
2. Wait until import, domain reload, ILPP, compile, MCP transport noise, and package refresh noise are complete.
3. Capture all required screenshots in one route state sequence and one continuous `GlobalQualityWeight`.
4. After the final screenshot, wait at least 60 seconds.
5. Require the Editor log last write time to be newer than the final screenshot.
6. Require the log tail to cover the capture and post-capture period.

Forbidden tokens in the clean window:

- `Error`
- `Exception`
- `Warning`
- `LogError`
- `Found 1 leak`
- `Leak Detected`
- `shader error`
- `material error`
- `not valid. Loading of assembly skipped`
- `CompileScripts`
- `Asset Pipeline Refresh`
- `H8_PLAYMODE_EXIT`
- `forced`
- `Access token is unavailable`
- MCP WebSocket failure text
- MCP transport startup failure text

Any hit rejects the packet.

## Owner Snapshot Sources

Use these sources through cold preflight and cached read models. No hot scene search is allowed during capture.

- Route/camera:
  - New `HectonProofRouteCaptureRig`.
  - Existing `SystemDispatcher` `CameraPositionSignal` and `CameraFrustumSignal` can be recorded as corroborating camera signal frames.
  - Direct selected capture camera transform may be sampled on the cold editor capture path after route staging.

- Depth:
  - Existing `DepthZoneDirector` must be extended with a side-effect-free proof snapshot.
  - `CurrentZone` alone is not enough.

- Underwater visuals:
  - Existing `HectonUnderwaterVisuals` must expose a side-effect-free proof snapshot.
  - Do not read private debug fields through reflection as the accepted route.

- Quality:
  - `HomeostasisBrain.GlobalQualityWeight`
  - `HomeostasisBrain.TryGetHardwareDictatorSnapshot(...)`
  - `GlobalRegistry.DynamicResolutionRuntime.TryGetSnapshot(...)`

- Water:
  - Surface height and visual depth should come from the water/underwater owner snapshot, not duplicate water calculations in the harness.

## No-Unity Static Implementation Boundary

Can be implemented and reviewed without a Unity run:

- Manifest DTOs and JSON writer.
- SHA256, file size, timestamp, and PNG dimension reader.
- Clean log token filter.
- Reject-code model.
- Route predicate definitions.
- Editor harness shell.
- Static documentation and packet folder naming.

Cannot be accepted without a Unity runtime run:

- Actual packet 1475 screenshots.
- Exact route/depth predicates.
- Underwater visual proof values.
- Clean 60 second post-capture log window.
- GlobalQualityWeight/render-scale values during capture.
- Visual quality acceptance against `TASTE.md`, `presentation.md`, `rendering.md`, and `water.md`.

## Runtime Proof Procedure For Packet 1475

1. Open a clean Unity session under the correct route scene.
2. Wait until compile/import/domain reload/package/MCP noise is stable.
3. Run harness preflight.
4. Confirm required owners are available:
   - `HectonProofRouteCaptureRig`
   - `HectonUnderwaterVisuals` proof read model
   - `DepthZoneDirector` proof read model
   - `DynamicResolutionScaler` runtime snapshot
   - `HomeostasisBrain` quality snapshot
5. Record continuous `GlobalQualityWeight` as numeric value and qNNN label. Do not coerce to low/high binary.
6. Stage each required view through the route rig.
7. Wait the harness-defined visual sync window after each stage.
8. Read owner snapshots.
9. Evaluate predicates before screenshot.
10. Capture PNG under `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/screenshots/`.
11. Compute SHA256, byte size, dimensions, timestamps, and bind capture time to log window.
12. Capture diagnostic view 7.
13. Wait at least 60 seconds after final screenshot.
14. Validate the log window and forbidden token filter.
15. Write final manifest.
16. Accept packet only when manifest says `final_disposition: ACCEPTED_BY_HARNESS` and all derived checks pass.

## First 20 Minutes Impact

This harness removes the current proof blocker for the first 20 minutes vertical slice: it creates an owned route for proving the transition from boot/world load into the semi-open beautiful shallow exit and swim state.

It does not by itself improve visuals or gameplay. It prevents false proof labels. The graphics, optimization, and gameplay pillars still need runtime evidence before the route can be called accepted.

## Scalability Consequences

Low:

- Capture runs with minimum required resolution and cold editor-only hashing.
- Manifest still records exact route/depth/quality truth.
- Visual floor is not lowered.

Middle:

- Capture uses standard route views and full manifest.
- Optional perceptual analysis can remain disabled unless proven useful.

High:

- Capture may include higher screenshot resolution and richer visual owner hashes.
- Runtime truth ownership and DTO layout remain unchanged.

Ultra:

- Capture may add extra diagnostic frames or supersized evidence images.
- It must not alter gameplay truth, route authority, or acceptance predicates.

## Key Blockers

1. Existing first-party and MCP screenshot routes are raw image emitters, not proof harnesses.
2. No HECTON-owned manifest writer/checksum/log binder currently proves route, depth, quality, and log cleanliness.
3. `HectonUnderwaterVisuals` lacks a public side-effect-free proof snapshot.
4. `DepthZoneDirector` exposes `CurrentZone` but not exact predicate-grade depth proof.
5. No owned route capture rig/view-id source was found.
6. Packet 1475 cannot be accepted by static inspection. It requires a clean Unity runtime capture with the harness.
7. The known dirty-log failure mode from Batch26 remains a hard reject until the post-capture clean window gate exists and passes.

