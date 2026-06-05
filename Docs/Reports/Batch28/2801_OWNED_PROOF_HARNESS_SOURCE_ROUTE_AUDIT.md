# 2801 Owned Proof Harness Source Route Audit

Date: 2026-06-04
Agent: 2801
Evidence class: STATIC_SOURCE + STATIC_DOC
Runtime proof status: PENDING_VERIFICATION
Write scope used: `Docs/Reports/Batch28/2801_OWNED_PROOF_HARNESS_SOURCE_ROUTE_AUDIT.md`

## Authority Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `quality.md`
- `camera.md`
- `presentation.md`
- `testing.md`
- `performance.md`
- `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `Docs/Reports/Batch27/2703_OWNED_CAPTURE_MANIFEST_HARNESS_SPEC.md`
- `Docs/Reports/Batch27/BATCH27_SYNTHESIS_FOR_UNITY_OWNER.md`
- Supporting prior failure context: `Docs/Reports/Batch26/2601_CAPTURE_HARNESS_DEPTH_METADATA_AUDIT.md`, `Docs/Reports/Batch26/2606_PROOF_WATCHDOG_PROCESS_HYGIENE_AUDIT.md`

`Docs/Actual Domains of Project.txt` produced no substantive domain content in static read. Narrow inferred domain: camera/capture/proof harness/testing/performance/global authority.

## Applied Mandates

- `QA_Evidence_Text_Filter_Audit`: static text search proves only source/doc presence, not runtime correctness.
- `ARCH_Execution_Phases`: capture proof must read immutable owner snapshots from declared phases; editor harness cannot invent runtime truth.
- `OPT_Zero_GC_Policy_AllocFree_Mandate`: runtime-facing proof snapshot accessors must be side-effect-free and allocation-free; editor file IO/hash work is cold.
- `GLOBAL_AUTHORITY_BOUNDARIES`: `GlobalRegistry` is cold identity/DI only. Harness may resolve once during preflight and cache interfaces. No hot polling route.
- `quality.md`: proof labels must remain `STATIC_SOURCE` / `STATIC_DOC` until editor/player artifacts exist.
- `camera.md`: screenshot evidence must carry capture truth label, camera route identity, and cannot hide missing gameplay proof.
- `testing.md`: manifest must label evidence class/tool/artifact/timestamp/unresolved failures.
- `performance.md`: no static performance claims; hot path additions must be zero-GC by construction and later profiler-proven.

## Verdict

There is no owned `1475` proof packet and no first-party HECTON proof harness in the audited source. Existing capture routes emit raw image files and limited path/dimension data. They do not bind screenshots to route id, camera pose, depth truth, underwater visual state, UI state, continuous quality, material/post-stack toggles, owner ids, manifest checksum, or a clean post-capture log window.

Smallest owner-correct route:

1. Add public, side-effect-free proof snapshot read models to the actual runtime owners.
2. Add one authored `1475` route capture rig that owns view ids, camera anchors, route predicates, and diagnostic overlay policy.
3. Add one editor-only owned harness that resolves/caches owners in cold preflight, captures screenshots, hashes files, reads PNG dimensions, writes manifest/checksum, copies/binds `Editor.log`, then waits and validates a clean post-capture log window.
4. Reject every packet that lacks any required owner snapshot or clean log gate.

Anything less is a renamed screenshot folder, not proof.

## Existing Capture Routes

### First-Party Dev Menu

Source:
- `Assets/_Project/Editor/HectonDevToolsMenu.cs:189` defines `Docs/Screenshots`.
- `Assets/_Project/Editor/HectonDevToolsMenu.cs:191` exposes `Capture Screenshot -> Docs/Screenshots (Play Mode)`.
- `Assets/_Project/Editor/HectonDevToolsMenu.cs:194-199` creates `screenshot-{timestamp}.png` and calls `ScreenCapture.CaptureScreenshot(absolutePath)`.
- `Assets/_Project/Editor/HectonDevToolsMenu.cs:200` logs only the screenshot path.

Audit result:
- Raw image route only.
- No manifest.
- No SHA256.
- No route id.
- No camera pose binding.
- No depth predicate.
- No quality snapshot.
- No UI state.
- No material/post-stack toggles.
- No clean log window.

### MCP Screenshot Utility

Source:
- `Library/PackageCache/com.coplaydev.unity-mcp@fbdb152757bd/Runtime/Helpers/ScreenshotUtility.cs:10` defines `ScreenshotCaptureResult`.
- `ScreenshotUtility.cs:34-41` returns `FullPath`, `AssetsRelativePath`, `SuperSize`, `IsAsync`, `ImageBase64`, `ImageWidth`, `ImageHeight`.
- `ScreenshotUtility.cs:46` defaults MCP output to `Docs/Screenshots/MCP`.
- `ScreenshotUtility.cs:103-110` routes default captures through `ScreenCapture.CaptureScreenshot`.
- `ScreenshotUtility.cs:149-188` camera captures render to texture, `EncodeToPNG`, then `File.WriteAllBytes`.
- `ScreenshotUtility.cs:549-587` prepares result paths and resolves project-root screenshot folder.

Audit result:
- MCP can produce image path, dimensions, inline image, and camera-specific output.
- MCP does not produce HECTON proof metadata.
- MCP output under `Docs/Screenshots/MCP` is not an owned `1475` packet.
- MCP package code is not the correct owner for HECTON route truth, depth truth, quality truth, or clean log validation.

### MCP ManageScene / ManageCamera

Source:
- `Library/PackageCache/com.coplaydev.unity-mcp@fbdb152757bd/Editor/Tools/Cameras/ManageCamera.cs:63-74` delegates screenshot commands to `ManageScene`.
- `Library/PackageCache/com.coplaydev.unity-mcp@fbdb152757bd/Editor/Tools/ManageScene.cs:515` enters `CaptureScreenshot`.
- `ManageScene.cs:519-526` reads filename, super size, and `game_view` default.
- `ManageScene.cs:559-572` supports surround/orbit/positioned captures.
- `ManageScene.cs:581-604` resolves a target camera and falls back to `Camera.main` or all cameras.
- `ManageScene.cs:612-627` returns only path/fullPath/superSize/isAsync/camera/captureSource for camera route.
- `ManageScene.cs:637-683` returns only path/fullPath/superSize/isAsync/captureSource for default route.
- `ManageScene.cs:1120-1160` positioned capture writes a PNG and returns path, base64, dimensions, view position, and optional view target.
- `ManageScene.cs:1186-1193` resolves `Docs/Screenshots/MCP`.
- `ManageScene.cs:1470-1522` can schedule AssetDatabase import when a captured path is asset-relative.

Audit result:
- Useful for raw capture mechanics and maybe emergency inspection.
- Not acceptable as the final owned proof route.
- The owned packet must write under `Docs/Screenshots/HectonProofPackets/h8_1475_{session_id}/`.
- The owned harness should avoid `Assets` output and avoid relying on AssetDatabase import for proof.

### MCP Bridge Log Risk

Source:
- `Assets/_Project/Scripts/Editor/HectonMcpHttpBridgeAutostart1428.cs:19` points to `http://127.0.0.1:8088`.
- `HectonMcpHttpBridgeAutostart1428.cs:89` logs connection.
- `HectonMcpHttpBridgeAutostart1428.cs:97` and `:105` log disconnect/reconnect warnings.

Audit result:
- Bridge warnings can dirty `Editor.log` during capture.
- The owned harness must either disable/noise-gate this path for proof capture or reject dirty logs.

## Current Runtime Owner Candidates

### Underwater Visual Truth

Source:
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:78` declares `HectonUnderwaterVisuals`.
- `HectonUnderwaterVisuals.cs:93` exposes `ActiveRuntimeInstance`.
- `HectonUnderwaterVisuals.cs:232` references `DepthZoneDirector`.
- `HectonUnderwaterVisuals.cs:236` references underwater suspended motes.
- `HectonUnderwaterVisuals.cs:343` owns `waterLevelFallback`.
- `HectonUnderwaterVisuals.cs:397-401` owns shallow caustics controls.
- `HectonUnderwaterVisuals.cs:7017` resolves caustics strength.
- `HectonUnderwaterVisuals.cs:7395` resolves underwater visual state.
- `HectonUnderwaterVisuals.cs:7400` resolves underwater visual state for camera depth.
- `HectonUnderwaterVisuals.cs:7446` resolves active visual camera depth.
- `HectonUnderwaterVisuals.cs:7908-7912` updates private debug depth/underwater diagnostics.

Gap:
- No public proof snapshot was found.
- Private debug fields are not acceptable proof API.

Required owner addition:
- Add a side-effect-free `TryGetUnderwaterProofSnapshot(out HectonUnderwaterProofSnapshot snapshot)` implemented by `HectonUnderwaterVisuals`.
- Snapshot must include camera depth, water level, signed depth from surface, underwater bool, fog/turbidity/ambient state, caustics enabled/strength, motes enabled/rate/visibility, material/post-stack toggles, owner frame, and validity flags.
- Accessor must not search scene, allocate, mutate, publish, sync, complete jobs, or readback GPU state.

### Depth Zone Truth

Source:
- `Assets/_Project/Scripts/World/DepthZoneDirector.cs:334` implements `IDepthZoneReadModel`.
- `DepthZoneDirector.cs:372` stores `_currentZone`.
- `DepthZoneDirector.cs:401` exposes `CurrentZone`.
- `DepthZoneDirector.cs:556-575` finds a zone for depth.
- `DepthZoneDirector.cs:879-895` registers through `GlobalRegistry.RegisterDepthZoneRuntime(this)` and `GlobalRegistry.DepthZone`.
- `Assets/_Project/Scripts/World/DepthZoneProfile.cs:31-39` defines zone id/display name/min depth/max depth.
- `DepthZoneProfile.cs:65` exposes `ZoneHash`.
- `DepthZoneProfile.cs:124` checks `ContainsDepth(float depth)`.
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:5127-5129` current `IDepthZoneReadModel` exposes only `CurrentZone`.

Gap:
- `CurrentZone` alone cannot prove the screenshot's exact depth, predicate source, min/max pass, or snapshot frame.

Required owner addition:
- Add a new proof read model rather than overloading the existing minimal `IDepthZoneReadModel`.
- Candidate API: `bool TryGetDepthZoneProofSnapshot(out HectonDepthZoneProofSnapshot snapshot)`.
- Snapshot must include depth meters, zone id, display name, zone hash, min/max, contains-depth result, pending transition state if any, source owner frame, and validity flags.

### Continuous Quality / Scalability Truth

Source:
- `Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:34-37` defines `ScalabilityStateDTO` with `GlobalQualityWeight`.
- `HomeostasisBrain.ScalabilityDictator.cs:264` stores `_globalQualityWeight`.
- `HomeostasisBrain.ScalabilityDictator.cs:286` exposes continuous `GlobalQualityWeight`.
- `HomeostasisBrain.ScalabilityDictator.cs:2443-2466` exposes `TryGetHardwareDictatorSnapshot(out SystemHealthDTO health, out ScalabilityStateDTO state)`.

Audit result:
- Strong candidate owner for continuous quality in manifest.
- Manifest must record float value, not binary low/high tags.
- Optional labels like `q000`, `q500`, `q1000` may be derived only from the continuous value and must not replace it.

### Dynamic Resolution Truth

Source:
- `Assets/_Project/Scripts/World/DynamicResolutionScaler.cs:54` implements `IDynamicResolutionRuntime`.
- `DynamicResolutionScaler.cs:196-204` exposes render scale/current/target/system override/thermal override.
- `DynamicResolutionScaler.cs:752-755` exposes `TryGetSnapshot(out DynamicResolutionRuntimeSnapshot snapshot)`.
- `DynamicResolutionScaler.cs:860-868` writes snapshot render scale, target scale, frame time, and pressure state.

Audit result:
- Existing snapshot is usable for manifest render-scale binding if the runtime owner is initialized and cached in harness preflight.

### Global Registry Route

Source:
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs:1426` exposes `UnderwaterVisuals`.
- `GlobalRegistry.cs:1538` exposes `DynamicResolutionRuntime`.
- `GlobalRegistry.cs:1553` exposes `DepthZone`.
- `GlobalRegistry.cs:1558` exposes `DepthZoneReadModel`.
- `GlobalRegistry.cs:3519-3522` registers underwater visuals runtime.
- `GlobalRegistry.cs:3725-3728` registers dynamic resolution runtime.
- `GlobalRegistry.cs:3749-3752` registers depth zone runtime.
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:4670`, `:4672`, `:4745` define service slots for dynamic resolution, depth zone, and underwater visuals.

Audit result:
- Valid only for cold preflight identity resolution.
- Harness must cache references and then call pure proof accessors.
- Runtime capture loop must not poll `GlobalRegistry` per screenshot frame.

### Camera Signal Corroboration

Source:
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs:6706` defines `GlobalRenderContext`.
- `SystemDispatcher.cs:6729-6733` sets current render context and publishes camera signals.
- `SystemDispatcher.cs:6736` enters `PublishCameraSignals`.
- `SystemDispatcher.cs:6750-6755` pushes `CameraPositionSignal`.
- `SystemDispatcher.cs:6757-6766` pushes `CameraFrustumSignal`.

Audit result:
- Good corroborating owner for camera pose/frustum frame.
- Not sufficient for `1475` route/view identity by itself.
- The proof rig still needs authored view ids and route predicates.

## Missing First-Party Proof Components

Static search found no first-party source hits for:

- `HectonProofCapture`
- `ProofCapture`
- `ProofRoute`
- `RouteCapture`
- `HectonProofPackets`
- `manifest.sha256`
- `route_predicate`
- `log_window`

Required new components:

- Runtime contracts: `Assets/_Project/Scripts/Proof/Capture/HectonProofCaptureContracts.cs`
- Runtime read models: `Assets/_Project/Scripts/Proof/Capture/IHectonProofReadModels.cs`
- Route rig: `Assets/_Project/Scripts/Proof/Capture/HectonProofRouteCaptureRig.cs`
- Editor window: `Assets/_Project/Scripts/Editor/Proof/HectonOwnedProofCaptureWindow.cs`
- Editor harness: `Assets/_Project/Scripts/Editor/Proof/HectonOwnedProofCaptureHarness.cs`
- Manifest writer: `Assets/_Project/Scripts/Editor/Proof/HectonOwnedProofManifestWriter.cs`
- Screenshot hash/dimension reader: `Assets/_Project/Scripts/Editor/Proof/HectonProofScreenshotFileProbe.cs`
- Log gate: `Assets/_Project/Scripts/Editor/Proof/HectonProofLogWindowValidator.cs`
- Predicate evaluator: `Assets/_Project/Scripts/Editor/Proof/HectonProofRoutePredicateEvaluator.cs`

These are proposed implementation paths only. This audit did not create or modify `Assets`.

## Required Packet Layout

Root:

`Docs/Screenshots/HectonProofPackets/h8_1475_{session_id}/`

Required files:

- `manifest.json`
- `manifest.sha256`
- `UnityEditor_{packet_id}_{session_id}.log`
- `screenshots/{view_index}_{view_id}.png`

Required view ids:

- `surface_coast_aegir_ui_off`
- `shoreline_close_1m`
- `underwater_0_5m`
- `underwater_20_50m_route`
- `aegir_celestial_long`
- `regression_low_oblique`
- `proof_debug_overlay_route_state` as diagnostic only, never as production substitute.

## Required Manifest Fields

Top-level fields:

- `schema`: fixed proof manifest schema id.
- `schema_version`: integer.
- `packet_id`: `h8_1475`.
- `session_id`: unique run id.
- `evidence_class`: expected `EDITOR_VERIFIED` after harness run; current audit is only `STATIC_SOURCE`.
- `truth_label`: gameplay/staged/editor/concept, exact value chosen by harness based on run mode.
- `created_utc`: ISO timestamp.
- `project_root`: absolute or project-relative root.
- `unity_version`: Unity editor version.
- `active_scene`: active scene path/name.
- `loaded_scenes`: array of loaded scene paths/names.
- `output_root`: packet root.
- `manifest_path`: relative packet path.
- `manifest_sha256`: duplicated in `manifest.sha256`.
- `editor_log_path`: copied log path in packet.
- `editor_log_source_path`: `Application.consoleLogPath` source path.
- `log_window_start_utc`, `log_window_end_utc`, `log_window_duration_seconds`.
- `log_window_status`: pass/fail.
- `log_window_reject_tokens`: tokens checked.
- `global_quality_weight`: continuous float.
- `quality_sample_owner`: `HomeostasisBrain`.
- `dynamic_resolution_snapshot`: current scale, target scale, override flags, pressure data.
- `hardware_dictator_snapshot_hash`: deterministic hash or compact summary.
- `owner_snapshots`: owner type, instance id if safe, owner frame, validity flags.
- `screenshots`: ordered screenshot records.
- `rejection_gates`: evaluated gates and pass/fail state.
- `unresolved_failures`: explicit list; empty only when actually empty.

Per screenshot fields:

- `view_index`
- `view_id`
- `view_role`: production or diagnostic.
- `capture_source`: game_view/camera/render_texture/etc.
- `path`
- `file_name`
- `sha256`
- `byte_length`
- `width`
- `height`
- `created_utc`
- `last_write_utc`
- `camera_owner`
- `camera_name`
- `camera_position`
- `camera_rotation`
- `camera_forward`
- `camera_fov`
- `camera_near_clip`
- `camera_far_clip`
- `camera_signal_frame`
- `route_owner`
- `route_id`
- `route_anchor_id`
- `route_predicates`
- `route_predicate_status`
- `water_level`
- `camera_depth_meters`
- `signed_depth_from_surface`
- `depth_zone_id`
- `depth_zone_display_name`
- `depth_zone_hash`
- `depth_zone_min`
- `depth_zone_max`
- `depth_zone_contains_depth`
- `underwater_active`
- `fog_state`
- `turbidity_state`
- `ambient_state`
- `caustics_enabled`
- `caustics_strength`
- `motes_enabled`
- `motes_visibility`
- `material_toggle_hash`
- `post_stack_toggle_hash`
- `ui_state_owner`
- `ui_visible`
- `ui_mode`
- `global_quality_weight`
- `render_scale_current`
- `render_scale_target`
- `snapshot_owner_frames`
- `warnings`

## Proof Binding Requirements

### Route Id

Owner route must be authored in `HectonProofRouteCaptureRig`, not inferred from filename, camera name, or screenshot folder.

Gate:
- Reject if view id is missing, duplicated, unknown, or if route predicates fail.

### Camera Pose

Primary source:
- The capture rig's authored camera anchor and the active capture camera transform.

Corroboration:
- `SystemDispatcher` camera position/frustum signals if available for the capture frame.

Gate:
- Reject if manifest camera pose differs from the actual capture camera used for the PNG.

### Depth

Primary sources:
- `HectonUnderwaterVisuals` proof snapshot.
- `DepthZoneDirector` proof snapshot.

Gate:
- Reject if depth state is inferred only from view id, filename, scene name, or zone object without exact current depth.

### UI

Required:
- Bind UI-visible/UI-off state to the real UI owner or an explicit proof overlay owner.

Gate:
- Reject if UI state is written as a manifest constant with no owner snapshot.

### Continuous Quality

Required:
- Manifest writes `GlobalQualityWeight` as a float.
- Low/Middle/High/Ultra consequences must derive from this float and not change gameplay truth ownership, DTO layout, save identity, or authority route.

Gate:
- Reject binary quality switches.

### Material / Post-Stack Toggles

Required:
- Owner snapshot from underwater/water/sky/post stack systems, with compact deterministic hash if full field list is too large.

Gate:
- Reject if screenshots lack material/post-stack state binding for surface, underwater, and celestial views.

### Owner Ids

Required:
- Type name, owner category, owner frame/sample frame, validity flags, and stable proof hash where available.

Gate:
- Reject if manifest records values without owner identity.

### Clean Log Window

Required:
- Copy `Application.consoleLogPath` to packet as `UnityEditor_{packet_id}_{session_id}.log`.
- Record source log path.
- Clear or mark the pre-capture baseline.
- Capture images.
- Wait at least 60 seconds post-capture.
- Re-read log window.
- Reject if errors/exceptions/import failures/reconnect spam or known proof reject tokens appear after baseline.

Gate:
- Reject if log is older than final screenshot.
- Reject if log is copied without window timestamps.
- Reject if MCP bridge or editor tooling emits warning/error noise during the proof window.

## Minimal Implementation Sequence

1. Add proof DTOs and read-model interfaces under a narrow `Proof/Capture` runtime path with no editor dependency.
2. Implement `TryGetDepthZoneProofSnapshot` in `DepthZoneDirector`.
3. Implement `TryGetUnderwaterProofSnapshot` in `HectonUnderwaterVisuals`.
4. Add `HectonProofRouteCaptureRig` with authored view specs, camera anchors, production/diagnostic roles, route ids, and predicate definitions.
5. Add editor harness/window under `Scripts/Editor/Proof`.
6. Harness preflight resolves `GlobalRegistry` owners once, validates all required read models, caches references, and records missing owner failures.
7. Harness captures each required view into `Docs/Screenshots/HectonProofPackets/h8_1475_{session_id}/screenshots/`.
8. Hash each PNG, read dimensions, timestamp files, and evaluate per-view predicates.
9. Copy and validate `Editor.log` with a clean 60-second post-capture window.
10. Write `manifest.json`, then `manifest.sha256`.
11. Reject packet on any missing snapshot, dirty log, failed predicate, bad dimension, missing screenshot, missing SHA, or binary quality value.

## Compile / Runtime Risks

- Adding interfaces to existing core contract files can trigger assembly-definition dependency failures. Prefer a narrow proof contract assembly visible to the owners and editor harness.
- `HectonUnderwaterVisuals` has prior readiness/registration concerns from Batch27. Harness must reject missing or stale underwater owner rather than fallback to scene search or private debug fields.
- `DepthZoneDirector.CurrentZone` is insufficient for proof. Exact depth and owner frame must be exposed before `1475` can pass.
- Screen capture may be asynchronous. Harness must wait for file existence, stable file size, valid dimensions, and hash completion before manifest write.
- MCP bridge warnings can dirty the proof log window.
- MCP routes may schedule AssetDatabase import when output is asset-relative. Owned proof output must stay under `Docs` and avoid import dependency.
- Static source audit did not compile, enter Play Mode, run Unity, or execute screenshots. No runtime behavior is proven here.

## Rejection Gates For 1475

Reject the packet if any of these are true:

- No `manifest.json`.
- No `manifest.sha256`.
- No copied `UnityEditor_{packet_id}_{session_id}.log`.
- No clean 60-second post-capture log window.
- Any required production view missing.
- Diagnostic overlay substituted for a production screenshot.
- Screenshot has no SHA256, byte length, width, height, or timestamps.
- Route id missing or inferred from filename/folder.
- Camera pose missing or not bound to actual capture camera.
- Depth missing or inferred from label.
- Depth zone missing exact depth/min/max/contains result.
- Underwater visual state missing owner snapshot.
- UI state missing owner snapshot.
- `GlobalQualityWeight` missing or reduced to binary low/high value.
- Dynamic resolution snapshot missing.
- Material/post-stack toggle state missing.
- Owner ids/frames/validity flags missing.
- Any unresolved editor error/warning/import failure appears in proof log window.
- Any field claims `VERIFIED` without matching artifact path and timestamp.

## Blockers

1. No existing first-party `HectonProof` harness source was found.
2. No `Docs/Screenshots/HectonProofPackets/h8_1475_{session_id}/` packet or manifest was found in static screenshot docs.
3. Existing capture routes are raw screenshot emitters, not proof packet writers.
4. `HectonUnderwaterVisuals` lacks a public proof snapshot for depth/underwater/material/post-stack truth.
5. `DepthZoneDirector` exposes only `CurrentZone` through `IDepthZoneReadModel`; exact depth proof is missing.
6. No authored `1475` route capture rig/view-id owner was found.
7. No clean `Editor.log` window validator tied to capture packets was found.
8. MCP bridge/editor warnings are a known dirty-log risk until the owned harness gates them.

## Scalability Consequences

- Weak device / Minimum Survival: harness must still capture the same route truth and manifest fields, with lower render scale and cheaper visual cadence recorded by `GlobalQualityWeight`.
- Middle tier: same truth route, moderate render scale, proof fields unchanged.
- High tier: same truth route, higher render scale and stronger visual toggles recorded in snapshots.
- Ultra tier: same truth route, maximum visual-overkill toggles allowed only as fidelity; no change to gameplay truth, DTO identity, save identity, route authority, or manifest schema.

## Final Static Classification

This audit is `STATIC_SOURCE` and `STATIC_DOC` only. It identifies the owner-correct path and current blockers. It does not produce or validate runtime `1475` proof.
