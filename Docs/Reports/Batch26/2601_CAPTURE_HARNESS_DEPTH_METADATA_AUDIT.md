# Batch26 2601 Capture Harness / Depth Metadata Audit

Date: 2026-06-04  
Worker: 2601 Capture Harness / Depth Proof Auditor  
Scope: static/read-only audit. No Unity Editor slot, no Play Mode, no dotnet build, no process kills.  
Evidence classes used: `STATIC_DOC`, `STATIC_SOURCE`, `STATIC_FILE`, `STATIC_IMAGE_INSPECTION`. No runtime/profiler acceptance is claimed.

## Verdict

Packet 1474 is rejected as `FALSE_LABEL` plus `MISSING_METADATA`.

The six final `h8_1474_*` screenshots are distinct PNG files, but they do not prove distinct routes. Static image inspection shows the supposed underwater and shoreline views are the same surface/coast/Aegir composition with small framing changes. A filename saying `underwater_0_5m`, `underwater_20_50m_route`, or `shoreline_close_1m` is not route proof.

No HECTON-8-owned capture harness, per-image manifest, checksum manifest, camera/depth manifest, quality manifest, toggle manifest, or clean log binding was found for packet 1474. The available screenshot paths are generic menu/MCP capture utilities that can save a caller-provided filename without proving active scene, route, camera depth, water state, underwater renderer state, quality weight, or log hygiene.

## Authority Context

- `STATIC_DOC` `AGENTS.md`: root authority requires evidence-based work, visual floor for surface/sky/Aegir/moons/coastline/ocean/photic/medium-depth routes, and the three-pillar pass: graphics, optimization, gameplay.
- `STATIC_DOC` `VISION_LOCKS.md`: surface/coast/ocean/Aegir/moons/photic and medium-depth routes must stay readable and premium; `GlobalQualityWeight=0` is not an ugly mode.
- `STATIC_DOC` `camera.md`: capture rigs must label truth; screenshots cannot hide missing route proof.
- `STATIC_DOC` `presentation.md`: surface captures are beauty proof, not noir; weak art cannot be hidden by darkness.
- `STATIC_DOC` `water.md`: surface/photic water requires bright readable water, foam/refraction/caustic hints, and depth/route cues.
- `STATIC_DOC` `rendering.md`: surface/photic/medium captures must meet the visual floor; rendering owns visual/capture truth but not gameplay truth.
- `STATIC_DOC` `Docs/Reports/Batch25/BATCH25_SYNTHESIS_FOR_UNITY_OWNER.md:13-52`: Batch25 synthesized no visual acceptance, required clean route, then six-view packet plus manifest fields.
- `STATIC_DOC` `Docs/Orchestration/UNITY_OWNER_STEER_20260604_1474_FULL_PACKET_REJECT_FALSE_VIEWS.md:17-37`: 1474 was already rejected for false labels, no manifest/checksum/camera/depth/quality/toggles/log path, and dirty log.

## Why Packet 1474 Produced False Underwater/Shoreline Views

1. Generic capture accepted caller intent as filename truth. The MCP screenshot path stores `fileName` and returns path metadata, but does not validate route/depth.
2. The screenshot result schema has no HECTON-8 proof fields. It records file path, super size, async flag, and optional base64 dimensions only.
3. The scene/camera screenshot tool returns only `path`, `fullPath`, `superSize`, `isAsync`, `camera`, and `captureSource`. It does not return camera transform, water surface height, depth band, underwater state, quality weight, render scale, toggles, checksum, active route, or log path.
4. There is no packet manifest tying the six PNGs to a single clean capture session.
5. The final six images show the same surface/coast/Aegir route family. Distinct hashes only prove byte-distinct images, not depth-distinct captures.
6. The 1474 log contains compile/import/domain reload/leak/fault evidence after and around the packet window, so even a visually plausible screenshot would still need clean-session proof before acceptance.

## Source Evidence

### First-Party Screenshot Menu

- `STATIC_SOURCE` `Assets/_Project/Editor/HectonDevToolsMenu.cs:189-200` defines `ScreenshotsFolderName = "Docs/Screenshots"`, menu item `Capture Screenshot -> Docs/Screenshots (Play Mode)`, filename `screenshot-{DateTime.Now:yyyyMMdd-HHmmss}.png`, and calls `ScreenCapture.CaptureScreenshot(absolutePath)`.
- This menu emits a PNG path only. It does not emit manifest JSON, SHA256, active scene, camera transform, depth, route, water state, quality, toggles, or log pointer.
- `STATIC_SOURCE` `Assets/_Project/Editor/HectonDevToolsMenu.cs:841-843` explicitly avoids forcing selection while automation drives MCP observation. That is not a proof harness.

### MCP Capture Package

- `STATIC_SOURCE` `Packages/manifest.json:3` and `Packages/packages-lock.json:3-6` show `com.coplaydev.unity-mcp` is installed from git.
- `STATIC_SOURCE` `Library/PackageCache/com.coplaydev.unity-mcp@fbdb152757bd/Runtime/Helpers/ScreenshotUtility.cs:10-30` defines `ScreenshotCaptureResult` fields: `FullPath`, `AssetsRelativePath`, `SuperSize`, `IsAsync`, optional base64 image data, image width, and image height.
- `STATIC_SOURCE` `ScreenshotUtility.cs:44-46` uses folder `Docs/Screenshots/MCP`.
- `STATIC_SOURCE` `ScreenshotUtility.cs:103-110` captures via `ScreenCapture.CaptureScreenshot`.
- `STATIC_SOURCE` `ScreenshotUtility.cs:145-188` camera fallback renders a camera into a `RenderTexture`, reads pixels, encodes PNG, and writes bytes.
- `STATIC_SOURCE` `ScreenshotUtility.cs:549-565` prepares filename/folder and returns `ScreenshotCaptureResult`.
- `STATIC_SOURCE` `ScreenshotUtility.cs:579-587` resolves the screenshot folder to project root plus `Docs/Screenshots/MCP`.
- `STATIC_SOURCE` `ScreenshotUtility.cs:590-594` defaults filename to `screenshot-{DateTime.Now:yyyyMMdd-HHmmss}` or uses the caller-supplied name.

### MCP Camera/Scene Tool

- `STATIC_SOURCE` `Library/PackageCache/com.coplaydev.unity-mcp@fbdb152757bd/Editor/Tools/Cameras/ManageCamera.cs:63-74` delegates `screenshot` and `screenshot_multiview` to `ManageScene.HandleCommand`.
- `STATIC_SOURCE` `Library/PackageCache/com.coplaydev.unity-mcp@fbdb152757bd/Editor/Tools/ManageScene.cs:520-531` accepts capture source values `game_view` or `scene_view`.
- `STATIC_SOURCE` `ManageScene.cs:559-566` supports generic surround/orbit batch modes.
- `STATIC_SOURCE` `ManageScene.cs:569-572` creates a temporary camera for positioned view capture when view target/position are supplied.
- `STATIC_SOURCE` `ManageScene.cs:581-614` can capture from a resolved camera with `ScreenshotUtility.CaptureFromCameraToAssetsFolder`.
- `STATIC_SOURCE` `ManageScene.cs:619-627` returns only `path`, `fullPath`, `superSize`, `isAsync`, `camera`, and `captureSource`.
- `STATIC_SOURCE` `ManageScene.cs:637-683` default capture returns only path/fullPath/superSize/isAsync/captureSource for game view.
- `STATIC_SOURCE` `McpToolsSection.cs:594-618` exposes generic GUI buttons for Game View, Scene View, and Multiview, saving to `Docs/Screenshots/MCP`.
- `STATIC_SOURCE` `McpToolsSection.cs:682-755` click handlers call generic screenshot methods; multiview writes `Multiview_yyyyMMdd_HHmmss.png`.

### MCP Bridge Autostart

- `STATIC_SOURCE` `Assets/_Project/Scripts/Editor/HectonMcpHttpBridgeAutostart1428.cs:10-20` defines an editor autostart bridge to `http://127.0.0.1:8088`.
- `STATIC_SOURCE` `HectonMcpHttpBridgeAutostart1428.cs:27-35` configures MCP prefs and reconnect scheduling.
- `STATIC_SOURCE` `HectonMcpHttpBridgeAutostart1428.cs:38-50` skips batch/compiling/updating and attempts connection.
- `STATIC_SOURCE` `HectonMcpHttpBridgeAutostart1428.cs:53-105` uses reflection into MCP service locator and logs connection/reconnect warnings.
- `STATIC_SOURCE` `HectonMcpHttpBridgeAutostart1428.cs:198-207` sets MCP editor prefs. This is bridge setup, not a depth-proof harness.

### Runtime Fact Owner Candidates

These are candidates a future owned harness can read from; their existence is not proof that packet 1474 used them.

- `STATIC_SOURCE` `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:78` defines `HectonUnderwaterVisuals`.
- `STATIC_SOURCE` `HectonUnderwaterVisuals.cs:93` exposes `ActiveRuntimeInstance`.
- `STATIC_SOURCE` `HectonUnderwaterVisuals.cs:1041` registers underwater visuals in `GlobalRegistry`.
- `STATIC_SOURCE` `Assets/_Project/Scripts/Core/GlobalRegistry.cs:1426` exposes `UnderwaterVisuals`.
- `STATIC_SOURCE` `GlobalRegistry.cs:3519` registers underwater visuals runtime.
- `STATIC_SOURCE` `Assets/_Project/Scripts/World/DepthZoneDirector.cs:334` implements `IDepthZoneReadModel`.
- `STATIC_SOURCE` `DepthZoneDirector.cs:401` exposes `CurrentZone`.
- `STATIC_SOURCE` `DepthZoneDirector.cs:556` resolves zones by depth.
- `STATIC_SOURCE` `Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:286` exposes continuous `GlobalQualityWeight`.
- `STATIC_SOURCE` `Assets/_Project/Scripts/World/DynamicResolutionScaler.cs:196-200` exposes current/target render scale fields.

## Packet 1474 File Evidence

All six final packet PNGs are in `Docs/Screenshots/MCP` and all are `1008x567`.

| File | Last Write | Bytes | SHA256 | Static visual verdict |
|---|---:|---:|---|---|
| `h8_1474_surface_coast_aegir_ui_off.png` | 2026-06-04 19:25:09 | 676489 | `047F62921B4024DB7F064808EE4C321C195C468782FB4081ECE9EADEEC9624A2` | Surface/coast/Aegir composition. |
| `h8_1474_shoreline_close_1m.png` | 2026-06-04 19:25:14 | 677531 | `E94584D4C360865B44E47C6F735C796C494A42B41130D12F1952F618D3654A63` | Not 1 m shoreline proof; medium/distant surface view, no close foam/wet contact proof. |
| `h8_1474_underwater_0_5m.png` | 2026-06-04 19:25:18 | 670798 | `ED187CC7E54D7FFF83FE705DFC434DE3AFA2EC9EF350972A555200975C8677D2` | False underwater label; sky/coast/Aegir/surface still visible, no underwater volume/depth/route proof. |
| `h8_1474_underwater_20_50m_route.png` | 2026-06-04 19:25:22 | 676153 | `677AD0764F86DE55B6FAC108EC1B56A34AEC1A68B71BC3A1B3746225E8D3F36F` | False medium-depth route label; same surface family, no 20-50 m water volume or route cue proof. |
| `h8_1474_aegir_celestial_long.png` | 2026-06-04 19:25:27 | 670893 | `993AE5E551F2038B12A9908AE0B5D157B470336660DE1E2392657FF9339BCEFB` | Cropped/longer Aegir framing, not a full packet substitute. |
| `h8_1474_regression_low_oblique.png` | 2026-06-04 19:25:31 | 675808 | `47029C89FDCD53794848960C64DC6660FC4A65D4B1ECB70A5AE6603C5B53B742` | Low-oblique surface/coast family; includes visible Aegir artifact/stripe. |

No matching `h8_1474*.json`, `h8_1474*manifest*`, or generic manifest file was found under `Docs/Screenshots/MCP`.

## Dirty Log Evidence

`STATIC_FILE` `Docs/AgentLogs/UnityEditor_visual_audit_restart_1474b.log` is not clean-session proof.

Representative fault/import tokens:

- `UnityEditor_visual_audit_restart_1474b.log:29` licensing error.
- `UnityEditor_visual_audit_restart_1474b.log:1301` Asset Pipeline Refresh total `236.840` seconds with `ForceSynchronousImport`.
- `UnityEditor_visual_audit_restart_1474b.log:1317` `CompileScripts: 194815.487ms`.
- `UnityEditor_visual_audit_restart_1474b.log:1514`, `1537`, `1539` additional Asset Pipeline Refresh events.
- `UnityEditor_visual_audit_restart_1474b.log:1602` and later MCP `LogError` entries.
- `UnityEditor_visual_audit_restart_1474b.log:2044` Asset Pipeline Refresh with `ForceSynchronousImport | ForceDomainReload`.
- `UnityEditor_visual_audit_restart_1474b.log:2621` `CriticalBootException: [GlobalRegistry] Ready-locked registry rejected registration: HectonUnderwaterVisuals`.
- `UnityEditor_visual_audit_restart_1474b.log:3034-3101` WeatherEvents persistent allocation leak evidence.
- `UnityEditor_visual_audit_restart_1474b.log:8497` and `11914` persistent allocation leak totals.

This log cannot be used to certify capture stability, underwater visuals registration, clean imports, clean compilation, or clean MCP state.

## Manifest / Checksum System Status

No first-party screenshot proof manifest generator was found in the inspected capture paths.

The MCP package can emit PNGs and basic file path metadata. It does not generate SHA256, packet manifest JSON, per-view route/depth records, active route records, quality records, toggle records, or log-tail bindings. Prior reports contain manually assembled hash tables, but those are audit artifacts, not a capture harness.

## Required Owned Capture Harness

The next packet must not rely on raw user-named MCP screenshots as acceptance artifacts. Use an owned HECTON-8 capture wrapper that stages each route, verifies route truth, captures the image, computes metadata/checksums, and writes one manifest for the session.

Hard preflight:

- Active scene must be `02_HECTON_WORLD` or the manifest must identify the exact accepted world scene.
- Editor must not be compiling, importing, updating, or domain reloading.
- No `dotnet`, `csc`, ILPP, import, or asset refresh work may overlap the capture session.
- `HectonUnderwaterVisuals` registration must be clean before underwater captures.
- Console/log tail must be stable before first screenshot and after final screenshot.
- `Assets/Screenshots` must remain empty or explicitly marked ignored; authoritative packet output is `Docs/Screenshots/MCP` or a project-owned proof folder under `Docs/Screenshots`.

Hard per-capture flow:

1. Select required view ID from the six accepted view IDs.
2. Move/select the player/cockpit/capture rig through an owned route harness, not by filename only.
3. Wait through the owner phase that publishes route/depth/render snapshots.
4. Read immutable snapshots from owners/registry interfaces. Do not hot-poll scene objects as proof.
5. Verify camera depth, player/cockpit depth, camera/player separation, active underwater state, water fog, route cue, and quality fields before screenshot.
6. Capture PNG.
7. Compute width, height, byte size, SHA256, timestamps, and bind log path/tail.
8. Reject the individual view immediately if depth band, route cue, clean state, or metadata fields fail.

## Exact View Requirements

### `surface_coast_aegir_ui_off`

- Camera above water.
- Ocean surface, coastline, sky/cloud, Aegir, and at least one route/scale cue visible.
- UI disabled unless explicitly named `ui_on`.
- Foam/refraction/water contact evidence visible where surface meets shore or geometry.
- Manifest records camera height relative to water surface and confirms not underwater.

### `shoreline_close_1m`

- Camera or target point within 1.25 m of shoreline/waterline contact.
- Waterline, wet shore/rock/sand contact, foam or foam-equivalent premium visual, and water surface detail visible.
- Not accepted if it is a medium/distant coastal beauty shot.
- Manifest records shoreline target position, camera position, distance-to-shoreline, foam state, and wet-contact material state.

### `underwater_0_5m`

- Camera depth below water surface must be `> 0.0 m` and `<= 5.0 m`.
- Player/cockpit root depth must be logged; camera/player separation must be bounded and recorded.
- Underwater state owner must report active.
- Underwater visuals must show water volume, depth cue, particles or equivalent premium suspended detail, caustic/refraction hint where physically visible, and route context.
- Sky/coast/Aegir may only appear if the camera is genuinely below the surface looking through water and the manifest proves depth. Surface-only horizon shots are rejected.

### `underwater_20_50m_route`

- Camera depth below water surface must be `>= 20.0 m` and `<= 50.0 m`.
- Player/cockpit root depth must be in or near the same route band and logged.
- Must show medium-depth route structure, near/mid/far water volume cues, navigation/risk cue, and non-flat volumetric depth.
- Surface/coast/Aegir horizon dominance is rejected.
- Manifest must identify route segment, depth zone, water fog/turbidity values, caustic state or documented depth gating, and underwater renderer state.

### `aegir_celestial_long`

- Camera/lens metadata must prove this is a deliberate long celestial shot, not a crop of another view.
- Aegir/moons/sky/cloud readability must be preserved.
- Any artifact/stripe/banding is a reject flag unless explained by a diagnostic overlay and not submitted as final beauty proof.
- Manifest records lens/FOV, celestial body state, sky profile, cloud/weather state, and exposure/post state.

### `regression_low_oblique`

- Low-oblique angle must show surface, coast/terrain, route scale, and water readability.
- Must not be a duplicate of surface/coast with only tiny FOV/camera drift.
- Manifest records pitch/roll/yaw, camera height/depth, route segment, and visual regression target.

## Required Manifest Schema

Write one JSON manifest per packet, for example `h8_1475_s01_manifest.json`. The file must be generated by the capture harness during the session, not reconstructed manually after review.

Top-level fields:

- `schema_name`
- `schema_version`
- `project`
- `packet_id`
- `session_id`
- `worker_or_operator`
- `capture_started_local`
- `capture_started_utc`
- `capture_completed_local`
- `capture_completed_utc`
- `project_root`
- `unity_version`
- `active_scene`
- `loaded_scenes`
- `route_harness_name`
- `route_harness_version`
- `route_state_id`
- `route_state_name`
- `capture_tool`
- `capture_source`
- `output_root`
- `log_path`
- `log_last_write_local`
- `log_last_write_utc`
- `log_stable_seconds_before_first_capture`
- `log_stable_seconds_after_final_capture`
- `is_compiling`
- `is_updating`
- `asset_import_active`
- `domain_reload_active`
- `ilpp_active`
- `mcp_connected`
- `mcp_error_count`
- `console_error_count`
- `console_exception_count`
- `console_warning_count`
- `fault_summary`
- `global_quality_weight`
- `quality_token_q000_100`
- `render_scale_current`
- `render_scale_target`
- `urp_asset`
- `renderer_asset`
- `post_processing_enabled`
- `post_profile`
- `ui_state`
- `screen_width`
- `screen_height`
- `screenshots`

Per-screenshot fields:

- `view_id`
- `required_view`
- `filename`
- `relative_path`
- `absolute_path`
- `width`
- `height`
- `size_bytes`
- `sha256`
- `last_write_local`
- `last_write_utc`
- `capture_time_local`
- `capture_time_utc`
- `screenshot_kind`
- `capture_source`
- `camera_name`
- `camera_instance_id`
- `camera_source`
- `camera_position_world`
- `camera_rotation_world`
- `camera_forward_world`
- `camera_fov_degrees`
- `camera_near_clip`
- `camera_far_clip`
- `camera_water_surface_y`
- `camera_depth_below_water_m`
- `expected_depth_min_m`
- `expected_depth_max_m`
- `depth_band_pass`
- `player_or_cockpit_root_name`
- `player_or_cockpit_position_world`
- `player_or_cockpit_depth_below_water_m`
- `camera_player_distance_m`
- `depth_zone_id`
- `depth_zone_name`
- `underwater_state_owner`
- `underwater_state_active`
- `underwater_renderer_registered`
- `underwater_renderer_enabled`
- `crest_underwater_pass_active`
- `non_crest_underwater_route_documented`
- `render_settings_fog_enabled`
- `render_settings_fog_color`
- `render_settings_fog_density`
- `water_fog_enabled`
- `water_fog_color`
- `water_fog_density`
- `water_turbidity`
- `foam_enabled`
- `foam_source`
- `foam_strength`
- `caustics_enabled`
- `caustics_source`
- `caustics_strength`
- `caustics_depth_gate_m`
- `surface_refraction_enabled`
- `surface_reflection_enabled`
- `shoreline_target_position_world`
- `shoreline_distance_m`
- `shoreline_wet_contact_visible`
- `route_cue_id`
- `route_cue_visible`
- `aegir_visible`
- `moons_visible`
- `sky_profile`
- `weather_state`
- `exposure_state`
- `ui_visible`
- `reject_flags`
- `notes`

Required manifest-level derived checks:

- `all_required_views_present`
- `all_png_sha256_present`
- `all_png_paths_exist`
- `all_dimensions_match_expected`
- `all_depth_bands_pass`
- `no_false_label_candidates`
- `no_missing_metadata`
- `log_newer_than_final_screenshot`
- `log_clean_after_final_screenshot`
- `assets_screenshots_empty`

## Reject Conditions For Next Packet

Reject immediately if any of these are true:

- Any required view is missing.
- Any filename label lacks matching manifest depth/route proof.
- Any PNG lacks SHA256, dimensions, size, and timestamps.
- Any underwater view lacks camera depth, player/cockpit depth, underwater state, and underwater renderer proof.
- `underwater_0_5m` has camera depth outside `>0` and `<=5`.
- `underwater_20_50m_route` has camera depth outside `>=20` and `<=50`.
- `shoreline_close_1m` lacks shoreline distance proof and foam/wet-contact evidence.
- The log path is missing, stale, older than the final screenshot, or contains compile/import/domain reload/runtime fault tokens after capture start.
- Screenshots are generated under `Assets/Screenshots` without explicit artifact routing approval.
- MCP raw output is submitted without the HECTON-owned manifest wrapper.
- Visual result hides weak water/surface/shoreline/underwater art behind darkness, crop, fog slab, or generic green/blue tint.

## Scalability Consequences

- Low: capture may reduce resolution, cadence, or optional density, but must still show readable surface, shoreline, depth, route, foam/caustic-equivalent cues, and complete metadata.
- Middle: baseline acceptance target; all six views must satisfy visual route proof and manifest truth without relying on ultra-only effects.
- High: saved performance must buy stronger water detail, reflections/refraction, foam, caustic quality, volumetric readability, and cleaner celestial/shoreline presentation. Manifest must record the enabled toggles.
- Ultra: visual overkill is allowed through higher-quality water/sky/volumetric/post settings, but it must not change gameplay truth ownership, route identity, DTO layout, save identity, or acceptance schema.

## Final Finding

1474 failed because the capture path proved file creation, not world truth. The next acceptable packet needs an owned capture harness that binds every PNG to route, camera, depth, quality, toggles, checksums, and a clean log in the same session. Without that manifest, the packet is audit noise regardless of filenames or isolated screenshot beauty.
