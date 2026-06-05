# 3003 Visual Reference Matrix And Reject Gate

Status: STATIC_DOC + SCREENSHOT_ARTIFACT AUDIT ONLY
Date: 2026-06-04
Agent ID: 3003_VISUAL_REFERENCE_MATRIX_AND_REJECT_GATE

## Scope

Build a local visual reference matrix for the listed `1912`, `1474`, `1473`, `1908`, and `1428` screenshots. Define the minimum reject/accept gates for the next `1475+` packet.

No Assets edited. No Unity run. No build run. No runtime, profiler, Frame Debugger, or Console proof is claimed.

First-20-minutes route impact: this gate protects the first semi-open surface/photic-shallows exit. It removes the blocker where surface and shallow proof can be accepted from dark, mislabeled, low-detail, or route-empty screenshots.

## Mandates Followed

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt`

## Authority Docs Read

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `quality.md`
- `water.md`
- `rendering.md`
- `terrain.md`
- `celestial.md`
- `taskslocal/batch30_1912_scene_visual_recovery/3003_VISUAL_REFERENCE_MATRIX_AND_REJECT_GATE.txt`

## Evidence Classes Used

- `STATIC_DOC`: authority documents and mandate files were read. This proves only local document requirements.
- `SCREENSHOT_ARTIFACT`: the listed PNG files were visually inspected. This proves only visible captured pixels.
- `PENDING VERIFICATION`: any runtime, Play Mode, editor wiring, performance, GC, Frame Debugger, or target hardware claim.

## Hard Finding

All listed screenshots are rejected as acceptance proof for `1475+`.

Reason: none satisfies the combined gate for composition, water surface, foam/caustics, shoreline/wetness, terrain, Aegir/sky, underwater volume, and proof hygiene. Some older frames remain useful as direction-only references, but none may be upgraded into proof.

## Artifact Matrix

| Artifact | Size | Evidence class | Disposition | Primary rejection |
|---|---:|---|---|---|
| `Docs/Screenshots/MCP/h8_1912_surface_edit_main.png` | 1280x720 | `SCREENSHOT_ARTIFACT` | REJECTED | Foreground blobs and dark terrain dominate; waterline, foam, wetness, route, and material read fail. |
| `Docs/Screenshots/MCP/h8_1474_underwater_0_5m.png` | 1008x567 | `SCREENSHOT_ARTIFACT` | REJECTED | Filename says underwater, but visible horizon, sky, and Aegir make it surface proof only. |
| `Docs/Screenshots/MCP/h8_1474_underwater_20_50m_route.png` | 1008x567 | `SCREENSHOT_ARTIFACT` | REJECTED | Filename says 20-50m underwater route, but it is surface-level with no underwater volume. |
| `Docs/Screenshots/MCP/h8_1473_mainrt_crest_foam_shoreline.png` | 1280x720 | `SCREENSHOT_ARTIFACT` | REJECTED | Shoreline contact lacks credible foam/wetness; terrain is crushed dark and route-empty. |
| `Docs/Screenshots/MCP/h8_1908_surface_runtime_ui_on.png` | 1008x567 | `SCREENSHOT_ARTIFACT` | REJECTED | No visible runtime UI/instrument proof despite filename; surface remains dark and low-detail. |
| `Docs/Screenshots/1428_surface_game_cloud_deck_pass12.png` | 1280x720 | `SCREENSHOT_ARTIFACT` | REJECTED | Better Aegir band direction, but water is flat/dark and terrain/shoreline fail material truth. |
| `Docs/Screenshots/1428_sky_foam_caustics_pass_game.png` | 1008x567 | `SCREENSHOT_ARTIFACT` | REJECTED | Better brightness/specular/foam direction, but terrain is primitive and foam/contact reads strip-like. |

## Visual Comparison

### `1912` Main Surface

What works:
- Aegir is present at large scale.
- Sky is bright enough to reject abyss/noir misuse.
- Ocean has visible surface texture.

What fails:
- Foreground rock/foliage forms read as dark blobs, not premium geology or biota.
- Terrain is heavily crushed, with poor material breakup and weak silhouette polish.
- Yellow/debug-looking patches are visible near the left foreground/waterline.
- Water surface reads patterned and flat in places; no premium specular, refraction, or foam proof.
- Shoreline contact is not credible: no wetness gradient, sediment transition, foam, or waterline detail.
- Composition blocks route readability; the foreground consumes the frame without showing a player decision.
- No UI, gameplay verb, threat, salvage, machinery, or return-route cue.

Verdict: reject. It is a current reference for what must be fixed, not an acceptance target.

### `1474_underwater_0_5m`

What it does better than `1912`:
- Cleaner wide composition.
- Horizon and Aegir framing are less obstructed.
- Water plane reads more consistently at distance.

What still fails:
- False underwater label. Visible sky, clouds, horizon, and Aegir prove surface capture.
- No underwater volume, no refraction-depth transition, no route at 0-5m.
- Terrain/coast remains too dark and lacks wet shoreline detail.
- Water lacks convincing foam/caustic/waterline proof.

Verdict: reject. Direction-only for wide surface composition.

### `1474_underwater_20_50m_route`

What it does better than `1912`:
- Stronger broad scenic framing.
- Aegir, sky, island, and sea have a clearer macro composition.
- Foreground obstruction is lower.

What still fails:
- False underwater label. This is not a 20-50m underwater route view.
- No underwater depth falloff, suspended matter, route silhouette, or hazard/return cue.
- Water surface is repetitive; terrain remains under-authored.
- No proof of caustics or underwater volume.

Verdict: reject. Direction-only for macro surface composition.

### `1473_mainrt_crest_foam_shoreline`

What it does better than `1912`:
- Camera is closer to the shoreline problem, making terrain/water contact easier to judge.
- Shore slope and ocean surface relationship are visible.
- Aegir is partially integrated into the sky scale.

What still fails:
- Foam is not credible enough for the filename claim.
- Terrain material is crushed and dark; wetness and strata are weak.
- Shoreline has hard contact, not sediment/water/foam blending.
- Composition is terrain mass plus water, with no route decision or gameplay stake.

Verdict: reject. Direction-only for shoreline audit angle.

### `1908_surface_runtime_ui_on`

What it does better than `1912`:
- Cleaner wide shot with less foreground blockage.
- Aegir placement and horizon scale are readable.
- Water surface has consistent directionality.

What still fails:
- Filename claims runtime UI on, but no visible UI/instrument state is present in the inspected pixels.
- Surface remains too dark for the normal surface/photic lock.
- Coast silhouettes are nearly black, hiding material quality.
- No foam/wetness/shore transition proof.
- No gameplay verb or route proof.

Verdict: reject. Direction-only for wide runtime-ish framing, not UI proof.

### `1428_surface_game_cloud_deck_pass12`

What it does better than `1912`:
- Aegir banding/planet texture direction is more premium and readable.
- Terrain albedo is not crushed into the same black mass.
- Sky/cloud silhouette creates stronger celestial scale.

What still fails:
- Water reads as a large flat dark slab.
- Shoreline/contact detail is weak.
- Terrain material is grey and unconvincing at gameplay distance.
- Small sky artifacts and object specks are visible without gameplay meaning.
- Surface brightness is still too low for the current `VISION_LOCKS.md` direction.

Verdict: reject. Direction-only for Aegir texture/banding.

### `1428_sky_foam_caustics_pass_game`

What it does better than `1912`:
- Brighter surface read.
- Better water sparkle/specular direction.
- Visible foam/contact line direction exists.
- Sky is closer to the bright surface boundary required by `TASTE.md`.

What still fails:
- Foam/contact reads like a strip, not physically motivated waterline breakup.
- Terrain is primitive grey rock with weak material detail.
- Aegir scale is present but not integrated with premium atmosphere/lighting.
- No real caustic proof is visible despite filename claim.
- No route cue, player verb, machinery, threat, or evidence.

Verdict: reject. Direction-only for brightness, water sparkle, and foam intent.

## Required Six Proof Views For `1475+`

### 1. Surface Main Composition

- Must show bright/readable surface, ocean, sky, Aegir/moons where relevant, coastline, and route direction.
- Must include one player-readable stake: route cue, threat cue, salvage/machinery cue, or return-path cue.
- Reject if foreground blobs, darkness, or fog hide bad assets.

### 2. Shoreline / Waterline / Foam

- Must show terrain-water contact at gameplay height.
- Required: foam breakup, wetness gradient, sediment/rock transition, specular response, and no hard seam.
- Reject if foam is a flat strip, absent, or detached from wave/contact logic.

### 3. Photic 0-5m Underwater

- Must be an actual underwater view. No sky/horizon/Aegir as the dominant proof.
- Required: surface refraction above, shallow terrain readable through water, clean ocean color, near caustic hints where justified, route cue.
- Reject false underwater labels.

### 4. 20-50m Underwater Route

- Must show underwater volume, depth falloff, silhouettes, suspended matter or turbidity, and route/return readability.
- Must not look like surface water with a different filename.
- Required: at least one route landmark or hazard silhouette.

### 5. Runtime UI / Instrument Surface View

- Must visibly show the claimed UI/instrument/HUD if the filename says UI on.
- UI must not be proof by filename.
- Required: readable instrument state plus scene context, not UI alone.

### 6. Sky / Aegir / Cloud Direction Pair

- Must show premium Aegir texture/cloud bands/atmospheric softness and how it affects surface composition.
- Required: no muddy sine stripes, no low-res blobs, no disconnected planet pasted behind terrain.
- If using older 1428 frames as direction, they remain direction-only and still rejected as proof.

## Hard Reject Gates For `1475+`

Reject the packet if any item below is true:

- Any screenshot label claims underwater while visible evidence shows surface/horizon/sky-dominant capture.
- Surface, photic-shallow, or medium-depth hero route falls below the Subnautica-level floor required by `TASTE.md` and `VISION_LOCKS.md`.
- Darkness, fog, post, or crushed exposure hides weak terrain, water, sky, celestial art, or missing shoreline detail.
- Aegir/moons/sky look muddy, low-resolution, procedurally scribbled, or disconnected from lighting and water context.
- Water surface lacks readable wave normals, ocean color, specular response, refraction or depth falloff, and shoreline interaction where visible.
- Shoreline lacks foam/wetness/contact transition in any view that claims shoreline or foam proof.
- Terrain reads as random noise, smooth blobs, primitive grey rock, black mass, or unmotivated scatter.
- Foreground hero shapes read as low-poly blobs or placeholder assets.
- Caustics are claimed without visible justified light reason or captured caustic evidence.
- Runtime UI, HUD, or instrument proof is claimed only by filename.
- Screenshot contains debug-looking color patches, obvious clipping, flat water slabs, visible hard seams, or meaningless specks as final proof.
- Report language says "looks better", "visually acceptable", "done", "optimized", "AAA", or "runtime verified" without matching proof artifacts.
- No route cue, player decision, threat, machinery/salvage cue, scale cue, or return-path cue is visible.

## Positive Required Traits For `1475+`

- Surface must be bright, beautiful, readable, and alien, with unease or threat in composition.
- Ocean must show material truth: color depth, waves, normal detail, specular sparkle, refraction/depth transition, foam where wave/shore contact justifies it.
- Terrain must show authored or high-quality generated geology: strata, wetness, sediment, fracture, scale, and readable navigation silhouettes.
- Aegir and moons must show premium texture detail, cloud bands, atmospheric softness, scale, and relation to surface lighting.
- Photic underwater views must be actual underwater views with visible volume, surface refraction, shallow terrain readability, and route cue.
- 20-50m views must show underwater depth, silhouettes, turbidity/particulate structure, and return-route readability.
- UI proof must show actual UI/instrument pixels and the scene consequence it helps judge.
- Compact-tier consequence: keep composition, silhouettes, ocean color, terrain material identity, Aegir/sky readability, and route cues even if reflection, caustic, particle, and cloud density are reduced.
- Middle-tier consequence: add richer water normals, better shoreline foam/wetness, denser but controlled scatter, and stronger Aegir/cloud texture.
- High-tier consequence: add local caustics where justified, richer reflections, longer LOD residency, better wet material response, and more detailed terrain/coastline breakup.
- Ultra-tier consequence: add visual overkill through atmosphere, water sparkle, cloud depth, secondary shafts, local silt/foam nuance, and premium near-field materials without changing route truth.

## Proof Hygiene Rules

- Each screenshot must be named by actual camera context and depth band.
- Each accepted proof view needs artifact path, timestamp, and evidence class.
- Screenshot proof does not imply Unity Console, Play Mode, profiler, GC, Frame Debugger, or build health.
- Runtime/performance claims remain `PENDING VERIFICATION` until current Unity/player/profiler artifacts exist.
- Old direction-only frames can influence art direction but cannot be accepted as current proof.

## Final Disposition

`1475+` must not inherit acceptance from any listed image. The next packet needs fresh proof views that satisfy the six-view checklist and hard gates above.

Current status: PENDING VERIFICATION for runtime and performance. Visual acceptance: REJECTED until fresh `1475+` screenshots prove the gates.
