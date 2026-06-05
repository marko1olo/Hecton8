# Batch29 1912 Visual Rejection And Reference Compare

Date: 2026-06-04 22:50 +04:00.
Evidence class: STATIC_FILESYSTEM + HUMAN_VISUAL_REVIEW.

## Verdict

`h8_1912_surface_edit_main.png` is `REJECTED`.

It is not a proof packet. It is a raw editor surface capture without manifest, checksums, route predicates, clean post-capture log window, underwater views, shoreline proof, or profiler/GC evidence.

The image is also visually below the HECTON-8 floor.

## Instructions Recalled

Binding visual floor from `AGENTS.md`, `TASTE.md`, `VISION_LOCKS.md`, `water.md`, `rendering.md`, `terrain.md`, and `celestial.md`:

- Surface, coast, sky, Aegir, moons, ocean skin, photic shallows, and 0-100 m routes must be bright, beautiful, readable, detailed, and premium.
- Subnautica-level surface/shallow/mid-depth readability is the floor, not the ceiling.
- Darkness/noir is for depth, caves, interiors, storms, eclipses, and pressure events. It cannot hide bad water, terrain, sky, or celestial art.
- Normal proof requires six route-correct views plus manifest, checksums, camera/depth/quality/toggles/log path, and clean log window newer than the final screenshot.
- `GlobalQualityWeight` is continuous only. No binary ugly/beautiful mode.
- Fake-first is allowed only when the fake looks premium.
- Screenshots/log junk must not be written under `Assets`.
- Unity slot must not be stolen from the Unity owner.

## Current 1912 Artifacts

- `Docs/Screenshots/MCP/h8_1912_surface_edit_main.png`
- `Docs/Screenshots/MCP/h8_1912_surface_edit_main.txt`
- `Docs/Screenshots/MCP/h8_1912_surface_after_quarantine_b.png`
- `Docs/Screenshots/MCP/h8_1912_surface_after_quarantine_b.txt`
- `Docs/Screenshots/MCP/h8_1912_surface_quarantine.txt`
- `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs`
- `Assets/_Project/Scripts/Editor/H8VisualProofCapture1912.cs.meta`
- `Docs/Logs/UnityCapture_1912_surface.log`
- `Docs/Logs/UnityQuarantine_1912_surface.log`
- `Docs/Logs/UnityCapture_1912_surface_after_quarantine.log`

## Visual Compare

Compared against local project reference-direction captures:

- `Docs/Screenshots/1428_surface_game_cloud_deck_pass12.png`
- `Docs/Screenshots/1428_sky_foam_caustics_pass_game.png`
- `Docs/Screenshots/1428_gameview_ocean_foam_pass19.png`
- `Docs/Screenshots/MCP/h8_1473_mainrt_crest_foam_shoreline.png`
- `Docs/Screenshots/MCP/h8_1908_surface_runtime_ui_on.png`
- `Docs/Screenshots/MCP/h8_1474_underwater_0_5m.png`

These older captures are not final acceptance either. They still have water/color/terrain weaknesses. They are useful only as directional references for what `1912` lost or failed to reach: open waterline composition, horizon readability, brighter sky, Aegir texture presence, some water specular/foam attempt, and less foreground blocker domination.

`1912` fails against the reference direction:

- Foreground is dominated by black primitive boulders/slabs. They read as placeholder shells, not wet premium geology.
- Water reads as a flat dark green mesh/sheet. It lacks convincing surface waves, specular breakup, depth falloff, refraction, foam, and caustic relationship.
- A yellow/green artifact is visible on the left foreground. This is not a production shoreline/water signal.
- Shoreline foam is not proven. There is no close 1 m wet-contact proof and no organic foam following terrain.
- Caustic proof is absent. `H8_FloorCausticSoft_1443` exists in metadata, but the visible image does not prove believable caustic receiver/light logic.
- Terrain remains shell/black/weak. It does not show strata, sediment, wetness, scale witnesses, erosion, or premium generated material.
- Aegir is large and visible, but still reads as a dirty spherical impostor with muddy bands and weak atmospheric softness.
- The shot is surface-only. It proves nothing about 0-5 m underwater, 20-50 m route depth, particles, water volume, return cues, or depth structure.

## Visual Matrix

| Artifact | Status | What it proves | What it fails |
| --- | --- | --- | --- |
| `h8_1912_surface_edit_main.png` | REJECT | Raw surface camera exists after quarantine/capture script. | Foreground black placeholders, flat green water sheet, yellow artifact, dirty Aegir, no shoreline close proof, no underwater proof, no manifest. |
| `h8_1912_surface_after_quarantine_b.png` | REJECT | Repeated raw surface camera exists after quarantine path. | Visually same failure as `surface_edit_main`: black blockers, flat green sheet, yellow artifact, dirty Aegir, no foam, no caustics, no underwater proof, no manifest. |
| `h8_1474_underwater_0_5m.png` | REJECT | The capture system can label a route as underwater. | Label is false: image is surface/coast/Aegir again. No water volume, particles, depth, caustics, or route cue. |
| `h8_1908_surface_runtime_ui_on.png` | REJECT / direction-only | A more open surface composition exists and water has some normal breakup. | Still green/dark, weak terrain, no acceptable foam/wet-contact, Aegir remains muddy. |
| `h8_1473_mainrt_crest_foam_shoreline.png` | REJECT / direction-only | Camera can reach a shoreline/waterline composition with terrain, Aegir, and wave surface. | Shoreline is dark, foam not convincing, water color still toxic/flat, terrain material response weak. |
| `1428_sky_foam_caustics_pass_game.png` | REJECT / direction-only | Bright sky, waterline, specular, Aegir texture direction, and some foam attempt are better aligned with target. | Not enough terrain detail, no close wet-contact proof, no underwater volume proof. |
| `1428_surface_game_cloud_deck_pass12.png` | REJECT / direction-only | Horizon/Aegir/coast composition is cleaner than 1912. | Water/terrain still below floor; not a current runtime proof. |

Conclusion: the project has had better composition and waterline direction than `1912`, but none of these frames is acceptance. The next packet cannot be a "less bad" variant. It must prove production water, shoreline, terrain, Aegir, and route-correct underwater views in one manifest-bound package.

## Proof Hygiene Failure

`H8VisualProofCapture1912.cs` is a one-off editor capture/quarantine script inside `Assets/_Project/Scripts/Editor`. It triggers compile/import/domain reload and contaminates the proof lane.

The bigger defect: `QuarantineSurfaceRejectsAndExit()` opens `Assets/_Project/Scenes/02_HECTON_WORLD.unity`, disables renderers by name, marks the scene dirty, and calls `EditorSceneManager.SaveScene(scene)`.

This is not a proof harness. It is a permanent scene mutation route.

Static diff evidence:

- `Assets/_Project/Scenes/02_HECTON_WORLD.unity` changed massively after the 1912 quarantine pass.
- `h8_1912_surface_quarantine.txt` lists 23 disabled renderers, including debug lanes, noir slabs/curtains, broken foam sheets, and black primitive boulder objects.

The quarantine list names real blockers, but saving those disables into the production scene is not an accepted fix unless an owner-correct scene cleanup pass deliberately owns and reviews every changed object.

## Log Rejection

Latest watchdog output:

- `STATIC_BLOCKED`
- `DIRTY_LOG_TOKENS_FOUND`
- `RAW_PNG_SET_NO_MANIFEST`

Dirty log evidence includes:

- compile/import/domain reload activity;
- ILPP activity;
- MCP transport noise;
- `Curl error 42`;
- memory leak telemetry / stack allocator warning;
- no clean post-capture proof window.
- `UnityCapture_1912_surface_after_quarantine*` logs still include editor import/refresh/shutdown noise and MCP server shutdown noise.

## Required Correction

1. Stop using `h8_1912_surface_edit_main.png` or `h8_1912_surface_after_quarantine_b.png` as progress proof.
2. Stop writing one-off proof scripts under `Assets` unless the Unity owner intentionally accepts the import/reload cost and the script is kept reusable, manifest-bound, and owner-clean.
3. Do not call `SaveScene()` from a reject-quarantine helper unless the task is explicitly a scene cleanup task with reviewed object list and rollback path.
4. Re-establish scene state deliberately. The massive scene diff must be reviewed by the Unity owner before any acceptance run.
5. Produce a real `1475` or newer manifest-bound packet under `Docs/Screenshots/HectonProofPackets/...`, not raw MCP screenshots.
6. Build the view around actual premium surface/shoreline/underwater requirements:
   - remove foreground black shells from camera path;
   - replace weak boulders/shoreline with accepted wet basalt/shell/sediment material route;
   - restore real waterline composition;
   - show organic foam and wet-contact at 1 m;
   - show believable caustics in shallow/underwater views with receiver/light reason;
   - show real underwater volume, particles, depth bands, and return route cues;
   - keep Aegir bright, textured, atmospheric, and artifact-free.

## Current State

No visual acceptance exists.

`1474` remains the latest complete rejected six-view packet.

`1912` is a raw diagnostic surface-only rejection plus a proof-hygiene warning.
