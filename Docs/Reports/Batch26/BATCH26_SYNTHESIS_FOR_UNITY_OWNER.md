# Batch26 Synthesis For Unity Owner

Date: 2026-06-04 21:07 +04:00.
Scope: no-Unity static/process/report synthesis for the rejected `1474` visual/runtime proof lane.

## Current Verdict

No acceptance is possible.

`1474` remains the newest complete screenshot packet under `Docs/Screenshots/MCP`, and it remains rejected. No `1475` packet or manifest exists.

Reject basis:
- `underwater_0_5m` and `underwater_20_50m_route` are false labels, not route-correct underwater captures.
- `shoreline_close_1m` is not a 1 m shoreline/waterline proof.
- Packet has no manifest, checksums, camera transforms, depth bands, quality weight, render scale, toggles, material state, route state, or log binding.
- Screenshots do not prove foam, wet contact, caustics, underwater volume, particles, medium-depth route structure, or premium Aegir/celestial quality.
- Logs around prior proof are dirty with compile/import/domain reload, MCP transport errors, ready-lock service rejection, and native leak evidence.

## Fresh Process State

At recovery, Unity was active again with a new process:
- `Unity` PID `12120`, started `2026-06-04 21:04:51`.
- `mcp-for-unity` PID `11060` remained active.
- No `dotnet`, `csc`, `MSBuild`, `VBCSCompiler`, `Unity.ILPP.Runner`, or `UnityShaderCompiler` was sampled in the latest process check.

This means `Docs/AgentLogs/UnityEditor_visual_audit_restart_1474b.log` is not the latest live Unity session proof. Any new claim needs a fresh log tail from the current Unity session, newer than the final screenshot.

## Batch26 Findings

### 2601 Capture Harness / Depth Metadata

The existing screenshot routes prove file creation only. They do not prove world route truth.

The first-party dev screenshot menu and MCP screenshot utility can write PNGs to `Docs/Screenshots` or `Docs/Screenshots/MCP`, but they do not emit HECTON-owned manifest fields for active scene, route state, camera position, water depth, player/cockpit depth, underwater state, material state, continuous `GlobalQualityWeight`, render scale, toggles, checksum, or clean log path.

Next packet must be produced by an owned wrapper that captures six views and writes one same-session manifest. Raw filenames like `underwater_20_50m_route` are not proof.

### 2602 Foam / Caustics / Crest Material

Static material state explains the missing foam/caustics without proving runtime behavior.

Key blockers:
- Surface route uses `Assets/Crest/Crest/Materials/Ocean.mat`; clip flags now serialize as off for the active surface route, but final brightness and foam readability are unproven.
- Underwater owner references `Assets/Crest/Crest/Materials/Ocean-Underwater.mat`; it serializes `_Caustics: 0` and `_CausticsStrength: 0` with clip/transparency keywords on.
- `Crest.UnderwaterRenderer` has `_volumeGeometry: {fileID: 0}` and `_copyOceanMaterialParamsEachFrame: 1`, so asset-only tuning can be overwritten or become false confidence.
- `H8_FloorCausticSoft_1443` is active/renderer-enabled, but it is a transparent additive sine fake with no intrinsic depth/light/occlusion owner.
- `H8_UnderwaterHazeCurtain_1454`, low-water occlusion slabs, pressure lid, depth shelf, and ceiling occlusion must not be raw-enabled. They need owner gating and low-oblique proof.

### 2603 Shoreline / Terrain Art Route

Current shoreline/terrain route is under-authored.

Key blockers:
- `H8_PhoticRouteTerrain_1464` is active and uses `MAT_H8_PhoticRouteTerrain_1464`.
- That material uses `TX_H8_WetBasaltShoreline_Albedo_1428.png` as a broad input.
- The 1428 wet basalt manifest and QA mark that source as `REJECT`; it is albedo-only, seam/problematic, and forbidden for broad active shoreline terrain.
- The active foam candidate is a transparent ribbon overlay, not proof of contact-caused foam or wet transition.
- The active caustic fake can reinforce flat/shell reads if it is not receiver/depth/light owned.

Do not fix this with darkness, haze, generic green tint, or more material-number boosts. The route needs accepted source textures, complete material families, controlled import, and proof at 1 m waterline.

### 2604 Aegir / Celestial Owner

Owner decision is still unresolved.

Current static state:
- `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474` has `sunVisualTransform: {fileID: 1985271341}`.
- `HectonCelestialEngine.sunVisualTransform` remains `{fileID: 0}`.
- `SURFACE_LOW_SUN_DISC_1428` exists but is inactive and its MeshRenderer is disabled.
- `HectonUnderwaterVisuals.ApplySunVisualState()` hides the scene sun visual when `HectonAtmosphereManager` is cached.
- `HectonCelestialEngine.ApplySunOcclusion()` treats an assigned atmosphere manager as sky-material-owned primary sun route and only toggles scene sun visual when atmosphere does not own it.

Required decision:
- Either declare and prove sky-material-owned sun disc through `Mat_HectonSky` / atmosphere globals, and stop expecting the inactive mesh sun disc.
- Or assign/activate the scene-mesh sun route consistently through `HectonCelestialEngine.sunVisualTransform`, renderer state, material quality, and atmosphere-present source behavior.

The current middle state is rejected.

### 2605 Generated Asset Intake / Staging

No generated Gemini source in the inspected tree is ready for Unity import, PBR derivation, material binding, TerrainLayer replacement, Crest foam binding, or caustic promotion.

Status:
- `READY_FOR_UNITY_IMPORT`: none.
- `READY_FOR_DERIVATION`: none.
- Wet basalt, refined wet basalt, Batch21 photic seabed, and Batch21 shell/sand are all rejected or source-reference-only.
- Foam/salt contact masks, caustic source masks, caustic lookup, shallow algae/biofilm source, accepted wet basalt source, and accepted shell/sand source are missing.

Future generation must stay under `Docs/GeneratedAssets/Gemini/Outputs/Batch26/2605/` with sidecar manifests, SHA256, audit output, and 2x2/3x3 preview review before any derivation or Unity import.

### 2606 Proof Watchdog / Process Hygiene

The proof lane fails before visual taste review.

Hard failures:
- no `h8_1474*manifest*`;
- no `h8_1475*`;
- copied proof log dirty;
- live Unity log dirty in the sampled session;
- current Unity session started after the copied `1474b` log;
- clean window must be same session, newer than final screenshot, stable for at least 60 seconds after final screenshot, and free of compile/import/domain reload/MCP/fault/leak tokens.

## New Runtime Blockers To Clear Before Capture

1. `HectonUnderwaterVisuals` service publication must stop hitting `GlobalRegistry` ready-lock rejection. Static scene owner existence is not runtime proof.
2. `SeamGapDitherRenderer.EnsureBuffers()` persistent `GraphicsBuffer` leak must be fixed or proven absent after fresh reload/play-exit. Stack lines observed in prior log: `SeamGapDitherRenderer.cs:455`, `456`, `466`, `467`, `476`, `481`.
3. Prior `WeatherEvents` leak cleanup remains plausible but unproven until fresh reload/play-exit proof.
4. Current Unity session needs a fresh clean log tail. Old dirty logs cannot certify new proof.

## Required Order For Unity Owner

1. Let Unity settle. Do not capture during compile/import/domain reload/ILPP/shader compile/MCP startup noise.
2. Fix runtime proof health:
   - no ready-lock rejection for `HectonUnderwaterVisuals`;
   - no `SeamGapDitherRenderer` persistent `GraphicsBuffer` leak after reload/play-exit;
   - no stale `WeatherEvents` leak after cleanup;
   - no MCP transport error storm in the accepted proof window.
3. Choose the celestial sun ownership route and make scene/source/proof expectations consistent.
4. Fix underwater ownership before visual capture:
   - resolve missing underwater volume/detail refs or replace with an explicit premium owner path;
   - prove underwater state, depth, material, fog/turbidity, caustic, and route cue fields in manifest.
5. Fix material route from owners:
   - do not raw-enable haze/slabs/curtains;
   - do not rely on `Ocean-Underwater.mat` with caustics at zero for lit photic proof;
   - do not swap to unproven overdriven candidate materials without isolation captures.
6. Fix shoreline art route:
   - remove broad dependency on rejected wet basalt source for acceptance route;
   - stage accepted source textures under docs first;
   - import complete material families only in a quiet owner window;
   - prove 1 m waterline wet contact and foam.
7. Produce a new `1475` packet only after clean state:
   - surface/coast/Aegir, UI on or explicitly declared;
   - surface/coast/Aegir, UI off;
   - shoreline close 1 m;
   - underwater 0-5 m;
   - underwater 20-50 m route;
   - Aegir/celestial long and crop or long shot metadata;
   - low-oblique regression for slabs/planes/white ocean artifacts.
8. Write one manifest with paths, SHA256, sizes, timestamps, active scene, loaded scenes, route state, camera transform/FOV/depth, player/cockpit depth, underwater owner state, material GUIDs and key values, object active/renderer states, continuous `GlobalQualityWeight`, render scale, post/fog/water/foam/caustic/toggle state, log path, and clean-window summary.

## Non-Acceptance Rules

- No acceptance from static YAML, material values, asset names, or diagnostic screenshots.
- No acceptance from a packet without manifest and clean log tail.
- No acceptance from screenshots written under `Assets`.
- No acceptance if underwater views visually read as surface/coast/Aegir views.
- No acceptance if surface/coast/photic/mid-depth/Aegir quality is dark, muddy, flat, primitive, hidden by fog/haze, or below the Subnautica-level floor.
- No acceptance if foam, caustics, volume, or terrain quality are only numeric claims.

## Source Reports

- `Docs/Reports/Batch26/2601_CAPTURE_HARNESS_DEPTH_METADATA_AUDIT.md`
- `Docs/Reports/Batch26/2602_FOAM_CAUSTIC_CREST_MATERIAL_AUDIT.md`
- `Docs/Reports/Batch26/2603_SHORELINE_TERRAIN_ART_ROUTE_AUDIT.md`
- `Docs/Reports/Batch26/2604_AEGIR_CELESTIAL_OWNER_AUDIT.md`
- `Docs/Reports/Batch26/2605_GENERATED_ASSET_INTAKE_STAGING_AUDIT.md`
- `Docs/Reports/Batch26/2606_PROOF_WATCHDOG_PROCESS_HYGIENE_AUDIT.md`
