# Visual Front P0/P1 Synthesis - 2026-06-05

Status: `CONTROLLER_SYNTHESIS / VISUAL PROMOTION REJECTED`
Evidence class: `SUBAGENT_STATIC_IMAGE_AUDIT + STATIC_DOC + DIRECT_IMAGE_REVIEW`

No Unity, build, import, Play Mode, profiler, scene save, prefab save, material save, Addressables mutation, or project-setting mutation was performed by this synthesis.

## Verdict

Visual promotion is rejected.

Accepted static claims:

- The current mandatory reference folder has 15 images and is the correct comparison source.
- Latest captures/logs are diagnostic evidence only.
- `h8_1914_surface_water_recovery_probe` is editor-only unsaved probe evidence, not product proof.
- `h8_1914_surface_crest_recovery_probe` is editor-only unsaved probe evidence, not product proof.

Rejected claims:

- surface water quality;
- foam/contact shoreline quality;
- coast/terrain material truth;
- Aegir/sky hero quality;
- photic shallows and medium-depth route proof;
- product-face/HUD proof;
- any `h8_1475` visual acceptance.

## Missing Proof

- No `Docs/Screenshots/HectonProofPackets/h8_1475_{session}/` packet.
- No `manifest.json`, `manifest.sha256`, copied Unity log, console export, dirty-state audit, no-mutation readback, or Frame Debugger/Stats packet.
- No compact/high comparison captures.
- No accepted active Crest/OceanRenderer, sky/Aegir, terrain, foam/contact, flora/material slot readback.
- No profiler/GC/memory proof for runtime visual paths.
- No active production player/HUD/tool route proof.

## Latest Visual Failure

Latest inspected frames still show:

- rectangular slab water;
- green seam haze;
- black shoreline undercuts;
- noisy acid terrain;
- weak translucent Aegir;
- no foam/wet edge;
- no underwater route density;
- no HUD/tool/product-face proof.

This is not “almost there.” It is a failed surface route.

Direct review of `Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.png` confirms the same rejection:

- foreground ocean reads as a hard rectangular slab with a visible far edge;
- the island underside is black, detached, and physically unconvincing;
- the haze line is green and flat instead of atmospheric coastal depth;
- Aegir reads as a giant transparent sphere pasted into the sky, not integrated celestial art;
- no gameplay HUD/tool/player route is visible.

The metadata tags this capture as `surface_actual_terrain_crest_recovery_probe_editor_only_unsaved` and shows temporary `H8_TEMP_SurfaceHorizonHazeProbe_1428` involvement. It is useful diagnostic evidence only.

Repeat review after ProbeB/ProbeC reruns:

- `Docs/Screenshots/MCP/h8_1914_surface_crest_recovery_probe.*` was overwritten again at `2026-06-05 23:33`.
- Current PNG hash: `9AE99C3DAA35D23BCF61057AC669A87F37FEF300C44A323117AA41E2E3FF4115`.
- Current metadata hash: `6034F65E0C9DF9A8A7085ABBD498DDC40012E6D6DBB72849984236B9D138DB45`.
- The latest image adds a visible rectangular material/terrain patch in the lower right, making the surface route worse.
- Metadata shows MapMagic active with `Main Terrain` at size `(15000.00, 0.00, 15000.00)` and first-party `H8_WORLD_TERRAIN_SHELL_1428` active separately. The route is a mixed diagnostic composition, not a stable production surface.
- Reusing the same output filename destroys evidence chain quality. Future proof captures need unique packet directories and hashes.

Detailed active/candidate/rejected route classification is captured in `Docs/Orchestration/SURFACE_ROUTE_STATIC_CLASSIFICATION_20260605.md`.

## P0 Tasks

1. Produce full `h8_1475` proof packet only after process gate clears and active player/HUD/tool route is known. Raw MCP PNGs are rejected.
2. Ocean/Crest readback: active OceanRenderer, canonical Crest materials, foam/normals/caustics slots, `_WD_*` classification, Frame Debugger rows.
3. Kill slab-water presentation. Fix ocean/terrain horizon geometry so no rectangular plane edge or dark band is visible before any haze/post layer.
4. Replace rejected visible waterline art. Use cleaned contact sources only after channel semantics, import, tile, and material proof.
5. Build real shoreline contact: wet rock transition, foam touching geometry, shallow transparency, contact breakup, 1-3m gameplay-height capture.
6. Terrain/geology repair: stop black/yellow noisy slick slopes. Prove basalt/sediment/wetness/normal/mask slots and route-scale material breakup.
7. Aegir/sky repair: prove active skybox/Aegir/cloud slots, improve limb/cloud-band integration, remove toy transparent-sphere read.
8. Underwater 0-5m route capture: water ceiling, seabed, route cue, flora/coral/fauna scale, readable instrument state.

## P1 Tasks

1. Medium-depth 20-50m hero route: cliffs/shelves, particles, flora/coral anchors, fauna silhouettes, return cue, no mislabeled surface shots.
2. Flora/coral proxy purge from active world: replace `WorldProceduralProxy` material contamination with final non-proxy material/LOD proof.
3. Coastline composition pass against `VREF-03` and `VREF-04`: layered islands/cliffs, whitewater, vegetation scale, route landmark, not empty horizon.
4. Compact/Middle/High/Ultra consequence proof: same route truth, continuous quality scaling, no ugly low mode, high/ultra spend on richer water/terrain/flora/sky detail.

## Controller Constraints

- Do not add rocks, plants, or coral as camouflage before water/shore/terrain/sky base passes.
- Do not use green haze, darkness, bloom, fog, vignette, or post as acceptance cover.
- Do not bind artist textures into Crest `_WD_*` wave-data slots.
- Do not blindly assign `MAT_H8_SurfaceCrestOcean_1428.mat`; it is a candidate with prior overdrive risk, not active proof.
- Do not use `H8VisualProofCapture1912.cs` diagnostic probe outputs as h8_1475 acceptance.

Final status: `REJECTED / PENDING UNITY READBACK AND PROOF PACKET`.

## 2026-06-06 Banach Static Visual Critic

Evidence class: `STATIC_DOC / STATIC_IMAGE_REVIEW / NO_UNITY_READBACK`.

Banach inspected all 15 mandatory visual references and current visual evidence/contact sheets. Verdict remains `REJECTED / PENDING UNITY + h8_1475 SCREENSHOT PROOF`.

P0 failures:

- no canonical `h8_1475` proof packet;
- surface ocean still reads as a slab;
- shoreline contact has no credible wet rock, foam, refraction, or shallow transparency proof;
- terrain/coast material truth fails against the reference floor;
- Aegir/sky reads toy-integrated rather than atmospheric;
- 0-5 m underwater route is empty/slab and lacks route ecology/instrument context;
- production player/HUD/tool witness is absent.

P1 failures:

- 20-50 m medium/deep hero route is unproven;
- product-face source gates for tools, pickups, transport, player shell, sky, and ocean remain unresolved;
- flora/coral/UI assets are candidates only until material/import/atlas binding and compact/high screenshot proof exist.

Do not hide any of these with raw MCP PNGs, controller prose, green haze, bloom, fog, darkness, cards, or decorative rocks/flora/coral. Later workers must repair the base route first, then produce canonical proof.
