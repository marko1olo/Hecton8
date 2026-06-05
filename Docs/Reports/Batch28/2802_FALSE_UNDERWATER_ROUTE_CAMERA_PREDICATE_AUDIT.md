# 2802 False Underwater Route / Camera Predicate Audit

Date: 2026-06-04  
Agent: 2802_FALSE_UNDERWATER_ROUTE_CAMERA_PREDICATE_AUDIT  
Evidence class: STATIC_SOURCE + STATIC_DOC. Runtime capture acceptance remains PENDING UNITY/PROFILER VERIFICATION.  
Scope: route/camera/depth predicate audit only. No Unity Editor run, no Play Mode, no build, no `Assets/**` edits.

## Verdict

The rejected `underwater_0_5m` and `underwater_20_50m_route` files failed because the current capture path accepts filename intent as truth. The generic first-party/MCP screenshot emitters can write a PNG named `underwater_*` without proving camera depth, water level, underwater visual owner state, depth zone, route anchor, material/pass state, quality weight, render scale, or clean log binding.

Static source also shows a second failure class: even a genuinely underwater camera can still be rejected if visible service slabs, hard horizontal cuts, blue walls, flat terrain, or disabled/missing underwater dressing make the image read as surface/primitive proxy art.

First-20-minutes impact: this audit removes a proof blocker for the spectacular bright/shallow first exit and descent route. It does not improve visuals by itself.

## Authorities And Mandates Used

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `water.md`
- `camera.md`
- `player.md`
- `presentation.md`
- `.agents-skills/REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/ARCH_Execution_Phases.txt`
- `Docs/Reports/Batch26/BATCH26_SYNTHESIS_FOR_UNITY_OWNER.md`
- `Docs/Reports/Batch27/BATCH27_SYNTHESIS_FOR_UNITY_OWNER.md`
- `Docs/Reports/Batch26/2601_CAPTURE_HARNESS_DEPTH_METADATA_AUDIT.md`
- `Docs/Reports/Batch27/2703_OWNED_CAPTURE_MANIFEST_HARNESS_SPEC.md`
- `Docs/Reports/Batch24/2401_CURRENT_SCENE_DELTA_UNDERWATER_CUT_AUDIT.md`

`Docs/Actual Domains of Project.txt` was absent. Narrow inferred domain: water, camera/capture, player depth, presentation proof, and route predicates.

## Static Source Findings

### Generic Capture Has No Route Truth

- `Assets/_Project/Editor/HectonDevToolsMenu.cs:189-200` writes `Docs/Screenshots/screenshot-{timestamp}.png` with `ScreenCapture.CaptureScreenshot(absolutePath)`. It records no scene, route, depth, water, quality, render scale, checksum, or log fields.
- `Library/PackageCache/com.coplaydev.unity-mcp@fbdb152757bd/Runtime/Helpers/ScreenshotUtility.cs:10-42` returns only path/supersize/async/base64/dimensions fields.
- `ScreenshotUtility.cs:46`, `579-587`, and `590-603` route MCP output to `Docs/Screenshots/MCP` and accept caller-provided filenames.
- `ScreenshotUtility.cs:103-110` uses `ScreenCapture.CaptureScreenshot`; `ScreenshotUtility.cs:149-188` can render a specific camera to a temporary RT and write bytes. Neither path reads HECTON route/depth owners.
- `ManageScene.cs:520-556` accepts `game_view` or `scene_view`; `ManageScene.cs:569-573` can create a positioned temp camera; `ManageScene.cs:581-614` resolves a target camera.
- `ManageScene.cs:619-627` and `637-682` return path, full path, supersize, async flag, camera name, and capture source. No route/depth/underwater predicate fields exist.
- `ManageCamera.cs:63-74` delegates screenshot actions to `ManageScene`.

Conclusion: current screenshot infrastructure proves file creation only. It cannot reject false underwater labels before capture.

### Underwater Owner Has Predicate Inputs But No Public Proof Snapshot

- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:78-93` defines `HectonUnderwaterVisuals` and an internal `ActiveRuntimeInstance`.
- `HectonUnderwaterVisuals.cs:98-101` defines visual hysteresis thresholds: enter `0.08m`, exit `0.03m`, forced underwater `0.18m`, and camera override threshold `0.15m`.
- `HectonUnderwaterVisuals.cs:1996-2025` runs the underwater visual tick, resolves current depth, resolves underwater state, updates diagnostics, and caches visual depth/state.
- `HectonUnderwaterVisuals.cs:2142-2161` applies this in `LateFrameTick`, then applies cached camera/ocean presentation.
- `HectonUnderwaterVisuals.cs:4513-4518` exposes `CurrentDepth`, but it is a live property over private resolution logic, not a predicate-grade snapshot.
- `HectonUnderwaterVisuals.cs:4550-4557` exposes `IsUnderwater`, but it can fall back to current depth and does not include camera, water level, owner frame, pass state, or route id.
- `HectonUnderwaterVisuals.cs:4815-4844` ensures a main-camera underwater pass in runtime; `4853-4894` does similar editor gameplay-camera pass handling.
- `HectonUnderwaterVisuals.cs:5437-5483` resolves particles/marine snow from the main camera, but this is private owner setup and not exposed as proof.
- `HectonUnderwaterVisuals.cs:5684-5729` computes suspended detail emission only when `isUnderwater`.
- `HectonUnderwaterVisuals.cs:7017-7040` resolves shallow caustic strength only when underwater.
- `HectonUnderwaterVisuals.cs:7339-7353` resolves water level from atmosphere sea level, physics water level, then `waterLevelFallback`.
- `HectonUnderwaterVisuals.cs:7356-7371` sanitizes water level to `0` if invalid or more than `1000m` from camera Y. This can make the visual owner report surface/non-underwater while a filename still says underwater.
- `HectonUnderwaterVisuals.cs:7374-7392` resolves current depth from active visual camera depth, then player movement depth unless camera depth exceeds player depth by more than `0.15m`.
- `HectonUnderwaterVisuals.cs:7400-7440` resolves underwater state using camera/player depth, hysteresis, player locomotion mode, and submerged state.
- `HectonUnderwaterVisuals.cs:7908-7912` writes `_debugDepth` and `_debugIsUnderwater`, but these are private/editor diagnostics, not an accepted public read model.
- `HectonUnderwaterVisuals.cs:7098-7115` warns on missing `biomePalette`, `oceanUnderwaterMaterial`, `skyMaterial`, or `globalLightCurve`. Prior logs already saw these as proof blockers.

Conclusion: the owner contains enough truth to reject false underwater captures, but the current public surface does not expose an immutable proof snapshot. Reflection over private debug fields is not an accepted route.

### Player Depth Exists But Is Not Bound To Capture

- `Assets/_Project/Scripts/HectonPlayerMovement.cs:1852-1899` exposes water immersion, locomotion mode, current water surface Y, `CurrentDepth`, and `IsPlayerSubmerged`.
- `HectonPlayerMovement.cs:6799-6803` resolves fallback water surface from `IFluidSurfaceCurrentReadModel.WaterLevel` or serialized fallback.
- `HectonPlayerMovement.cs:9470-9488` resolves locomotion into dry, shallow, surface swim, underwater swim, and exosuit modes.

Conclusion: player/cockpit depth can corroborate camera depth, but current capture does not require or record it.

### Depth Zone Read Model Is Too Narrow For Predicate Proof

- `Assets/_Project/Scripts/World/DepthZoneDirector.cs:334` implements `IDepthZoneReadModel`.
- `DepthZoneDirector.cs:401` exposes only `CurrentZone`.
- `DepthZoneDirector.cs:454-486` reads `survivalSystem.Depth` on `SlowTick`, finds a zone, and updates `_currentZone`.
- `DepthZoneDirector.cs:556-576` selects the deepest matching authored zone.
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:5124-5130` defines `IDepthZoneReadModel` as `DepthZoneProfile CurrentZone { get; }` only.

Conclusion: `CurrentZone` is not enough for `underwater_20_50m_route`. Predicate proof needs exact current depth, source, band pass, zone min/max/hash, update frame, and stale-age.

### Quality And Render Scale Are Available

- `Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs:285-292` exposes continuous `GlobalQualityWeight` and target render scale.
- `HomeostasisBrain.ScalabilityDictator.cs:2443-2467` exposes a hardware dictator snapshot with sanitized `GlobalQualityWeight`.
- `Assets/_Project/Scripts/World/DynamicResolutionScaler.cs:196-200` exposes current and target render scale.
- `DynamicResolutionScaler.cs:752-755` exposes `TryGetSnapshot`.
- `Assets/_Project/Scripts/Core/Contracts/DrsContracts.cs:27-47` defines `DynamicResolutionRuntimeSnapshot`.
- `Assets/_Project/Scripts/Core/Contracts/CoreContractsAssemblyMarker.cs:94-100` defines `IDynamicResolutionRuntime.TryGetSnapshot`.

Conclusion: capture manifests can bind continuous quality and render scale without a binary low/high switch.

## Why The False Underwater Files Can Happen

1. A caller can pass `h8_1474_underwater_0_5m.png` or `h8_1474_underwater_20_50m_route.png` to MCP capture. `BuildFileName()` sanitizes and writes it. It does not validate depth.
2. `ManageScene` can capture Game View, Scene View, a named camera, or a temporary positioned camera. None of those paths require `HectonUnderwaterVisuals.IsUnderwater == true`.
3. If a positioned/temp camera is used, it can bypass the player route and still return a successful PNG.
4. If the real camera remains near/above surface, `ResolveWaterLevel()` or `ResolveCurrentDepth()` can report `0m` or non-underwater while the file is still named underwater.
5. `DepthZoneDirector.CurrentZone` can be stale or too coarse for exact 20-50 m proof because the read model does not expose current depth or source frame.
6. Missing underwater material/sky/palette references or unregistered underwater owner can invalidate underwater presentation, but generic capture does not fail on those warnings.
7. The 1473/1474 orchestration evidence shows the output visually matched surface/coast/Aegir with small framing shifts, not depth-distinct routes.
8. The 2401 audit shows active rendered slab/occlusion/service geometry can create hard cuts, blue walls, and flat seabed. That is a visual rejection even after a future camera predicate passes.

## Required Public Read-Only Proof Snapshots

Add these later through side-effect-free owner interfaces. Do not implement them by reflection or hot scene search.

### `IUnderwaterVisualProofReadModel`

`TryGetUnderwaterProofSnapshot(out HectonUnderwaterProofSnapshot snapshot)` must include:

- owner name, instance id, enabled/active state;
- source frame, dispatcher phase, and snapshot sequence;
- water surface Y and source: atmosphere, physics, fallback;
- capture camera name/id/source and world position/forward/FOV;
- camera depth below water;
- player/cockpit root name/id, world position, depth, locomotion mode, submerged flag;
- resolved visual depth;
- underwater active flag from the same logic used by presentation;
- underwater pass enabled/active on the capture camera;
- Crest/non-Crest underwater route label;
- ocean underwater material instance/path/GUID if available;
- fog color/density/turbidity/extinction values;
- caustics enabled/strength/depth gate;
- suspended motes/marine snow operational flag and current emission/density;
- cached light factor and depth band values;
- route cue visibility flag supplied by route owner, not by image review;
- rejection flags for missing `biomePalette`, `oceanUnderwaterMaterial`, `skyMaterial`, `globalLightCurve`, unresolved camera, unresolved owner, or stale snapshot.

### `IDepthZoneProofReadModel`

`TryGetDepthZoneProofSnapshot(out HectonDepthZoneProofSnapshot snapshot)` must include:

- exact current depth meters;
- depth source: survival, player, cockpit, camera/proxy;
- zone id/name/hash;
- zone min/max depth;
- hull tier if relevant;
- update frame/sequence and age in frames/seconds;
- band pass for target min/max;
- route segment id if a route rig owns the capture;
- stale/missing-source rejection flag.

### `IHectonRouteCaptureProofReadModel`

`TryGetRouteCaptureSnapshot(out HectonProofRouteSnapshot snapshot)` must include:

- packet id, session id, required view id;
- authored route anchor id/name;
- active route segment id/name/hash;
- capture camera source: gameplay, cockpit, proof rig, temp diagnostic;
- camera transform/FOV and allowed tolerance from authored anchor;
- route cue id and visible bool;
- shoreline distance for shoreline view;
- expected depth min/max and actual predicate result;
- production vs diagnostic flag;
- UI policy;
- view duplication/hash/similarity guard against surface-lookalike reuse.

### Quality / Render Snapshot

Use existing:

- `HomeostasisBrain.GlobalQualityWeight`;
- `HomeostasisBrain.TryGetHardwareDictatorSnapshot(...)`;
- `IDynamicResolutionRuntime.TryGetSnapshot(...)`.

Manifest must record Low / Middle / High / Ultra consequences as continuous quality behavior, not binary tier proof.

## Static Route Predicates

These predicates must run before a PNG is accepted. If a predicate fails before screenshot, abort the view and write a reject manifest.

### Common Preflight For All Production Views

- Active scene is the accepted world scene or manifest explicitly names the accepted substitute.
- Capture source is not raw MCP-only; it is wrapped by a HECTON-owned manifest harness.
- Route capture owner has staged the requested view id.
- Capture camera is the authored gameplay/cockpit/proof camera for that view, not an unlabeled temp camera.
- Camera transform/FOV matches the route anchor tolerance.
- `HectonUnderwaterVisuals` proof snapshot exists for water views.
- `DepthZoneDirector` proof snapshot exists for underwater route views.
- `GlobalQualityWeight` is finite `0.0..1.0` and recorded as a float.
- Dynamic resolution snapshot exists and records current/target render scale.
- No capture output path is under `Assets/Screenshots`.
- Clean log gate is attached to the packet.

### `underwater_0_5m`

Hard numeric predicate:

- `water_surface_y` finite.
- `camera_depth_below_water_m >= 0.25` and `<= 5.0`.
- The `0.25m` lower guard intentionally avoids the existing `0.03/0.08/0.18m` waterline hysteresis ambiguity.
- `underwater_active == true`.
- underwater pass enabled and active for the capture camera.
- player/cockpit/proxy depth recorded; if player/proxy is outside `0..7.5m`, the route must state why the capture is an explicit proof-rig shot.
- camera/player distance recorded and within authored tolerance.

Presentation predicate:

- shallow water remains bright/readable, not darkness-only;
- water volume, surface underside or depth falloff, seabed/shore transition, route context, and suspended detail are visible;
- caustics/refraction hint is present when physically eligible or the depth/material gate records why it is off;
- sky/coast/Aegir can appear only as a through-water view, not as dominant surface horizon composition;
- no broad horizontal slab, blue wall, opaque cut, or flat proxy plane dominates the frame.

### `underwater_20_50m_route`

Hard numeric predicate:

- `water_surface_y` finite.
- `camera_depth_below_water_m >= 20.0` and `<= 50.0`.
- player/cockpit/proxy depth is in the same route band or an authored proof-rig exception is recorded.
- depth zone snapshot agrees with the 20-50 m band or records exact zone min/max that still contains the camera depth.
- `underwater_active == true`.
- underwater pass enabled and active for the capture camera.
- route owner confirms the medium-depth route segment and forward/return cue.

Presentation predicate:

- near/mid/far water volume structure is visible;
- medium-depth route geometry, terrain silhouettes, landmarks, return cue, or risk cue are visible;
- surface/coast/Aegir horizon dominance is rejected;
- water fog/turbidity is structured, not generic blue/green haze;
- darkness/mud cannot hide missing terrain, weak materials, or empty route;
- no service slab/shelf/lid/occlusion strip cuts through the composition.

## Exact Screenshot Rejection Criteria

Reject any underwater screenshot if any condition is true:

- Filename says underwater but manifest lacks camera depth, water surface Y, underwater owner state, player/proxy depth, route id, quality, render scale, and log path.
- `underwater_0_5m` camera depth is `<0.25m` or `>5m`.
- `underwater_20_50m_route` camera depth is `<20m` or `>50m`.
- `HectonUnderwaterVisuals` proof snapshot is missing, stale, unregistered, disabled, or reports `underwater_active=false`.
- Capture camera underwater pass is missing, disabled, inactive, or bound to a different camera than the PNG source.
- Depth zone snapshot is missing or exposes only `CurrentZone` without exact current depth/source frame.
- The image visually matches the surface/coast/Aegir packet view with only small FOV/camera drift.
- Sky/coast/Aegir/horizon dominate a supposed 20-50 m route.
- A shallow through-water sky view lacks manifest proof that camera is below water.
- No route/return cue, terrain silhouette, seabed/shore transition, biota, silt, particles, caustic/refraction cue, or depth falloff is visible.
- Hard horizontal shelf, rectangular blue wall, opaque waterline strip, false ceiling, pressure lid, caustic sheet, or broad flat proxy plane appears.
- Fog, darkness, crop, bloom, or green/blue tint hides weak art.
- Underwater material reports missing reference, `_Caustics=0` while the screenshot claims caustic proof, or key material/pass values are absent from manifest.
- Packet lacks SHA256/dimensions/timestamps, or log is stale/dirty/newer mismatch.

## Current Strong Blockers

1. No HECTON-owned capture wrapper currently enforces route predicates before PNG acceptance.
2. `HectonUnderwaterVisuals` has private debug fields and public live accessors, but no immutable proof snapshot with camera, depth, pass, material, fog, caustics, motes, and route state.
3. `DepthZoneDirector` implements `IDepthZoneReadModel`, but that contract exposes only `CurrentZone`, not exact predicate-grade depth.
4. MCP/first-party screenshot routes accept caller filenames and return basic path metadata only.
5. Temp/positioned camera capture can produce plausible screenshots without gameplay route truth unless the future route owner forbids unlabeled temp capture for production views.
6. Known active scene slab/occlusion/plane suspects from Batch24 can still make true underwater captures visually invalid.
7. Prior packets had dirty log windows and missing manifests; no static predicate can substitute for a clean runtime capture session.

## Scalability Consequences

Low: predicates are unchanged. Capture may lower resolution or optional density, but must still show shallow/medium route truth, readable water volume, route cues, and complete owner snapshots.

Middle: baseline proof lane. Six production views plus diagnostic overlay must pass route, depth, material, quality, render scale, and clean-log predicates.

High: saved frame cost should buy better water detail, route landmarks, caustic/refraction proof, wet material response, and stronger depth structure. Truth fields remain identical.

Ultra: supersized/extra diagnostic captures are allowed, but they must not change route authority, gameplay truth, DTO layout, or acceptance predicates.

## Final Position

Future underwater screenshots are rejected until a HECTON-owned harness proves the camera is actually in the requested depth band, the underwater owner reports active for that exact camera, the depth zone owner provides exact current depth, and the image itself contains premium water-column/route evidence. A PNG name is not evidence.
