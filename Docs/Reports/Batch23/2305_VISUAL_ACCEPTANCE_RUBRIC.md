# 2305 Visual Acceptance Rubric

Status: STATIC VERIFIED RUBRIC / NO UNITY RUN
Agent: 2305
Scope: visual proof packets for surface, shoreline, underwater 0-5 m, underwater 20-50 m, Aegir/celestials, and regression low-oblique.

## Authority Basis

- `AGENTS.md`: surface, sky, Aegir, moons, coastline, ocean surface, photic shallows, and medium-depth hero routes must be bright, readable, premium, and at least Subnautica-level.
- `TASTE.md` and `VISION_LOCKS.md`: darkness belongs to depth/caves/interiors/storms/eclipse windows, not to hiding weak surface/shallow art.
- `quality.md`: screenshot/log claims must keep evidence labels honest; static proof cannot become runtime proof.
- `presentation.md`: screenshots must reveal route, scale, pressure, machinery, danger, or evidence; beauty shots alone are insufficient.
- `water.md`: water must show route visibility, depth cue, material response, and readable haze; generic blue/green fog and flat planes are rejected.
- `terrain.md`: terrain must show credible geology, material breakup, silhouettes, and route logic.
- `lighting.md`: surface and photic lighting must remain readable; blackness cannot hide unfinished assets.
- `vfx.md`: foam, caustics, bubbles, silt, and particles are consequences, not debug clutter.
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`: text search and static logs are only static evidence.

## Evidence Labels

- `STATIC VERIFIED`: file/screenshot/report/log inspected. No runtime health implied.
- `PLAYER-CAPTURE CANDIDATE`: screenshot exists and visually matches the route label, but runtime log/profiler state may still be pending.
- `REJECTED VISUAL PROOF`: artifact exists but fails route/taste/content requirements.
- `PENDING VERIFICATION`: claim needs Unity, Play Mode, profiler, Frame Debugger, Memory Profiler, or current log proof.

Forbidden upgrades: `accepted`, `clean`, `stable`, `runtime verified`, `optimized`, or `release ready` without matching artifacts.

## Packet Required Views

Each acceptance packet must include all six views from one capture session:

| View | Required file suffix | Must show |
|---|---|---|
| Surface | `surface_coast_aegir_ui_off` | Bright ocean surface, sky/clouds, coastline/terrain, Aegir context, no UI. |
| Shoreline close | `shoreline_close_1m` | 1 m waterline, foam/wetness contact, rock material transition, scale. |
| Underwater 0-5 m | `underwater_0_5m` | Actual shallow underwater camera state, photic clarity, visible surface underside/water volume where relevant, seabed/rocks/biota/route. |
| Underwater 20-50 m | `underwater_20_50m_route` | Actual medium-depth route, depth haze, terrain silhouettes, route/return cue, no surface-plane clipping. |
| Aegir/celestial | `aegir_celestial_long` | Large textured Aegir/moons/sky behind horizon/atmosphere, fixed map position, scale. |
| Regression low-oblique | `regression_low_oblique` | Low/compact-like oblique view showing surface/shore/water/terrain composition remains readable. |

Any missing view is `MISSING_VIEW` and blocks acceptance. Any view whose content does not match its route label is `FALSE_LABEL` and blocks acceptance.

## Pass Criteria By View

### Surface

Pass requires:
- bright, readable ocean color with wave normal/specular response;
- coastline or terrain silhouette with material breakup;
- sky/cloud structure and Aegir/celestial context;
- no crushed blacks, one-note green/blue tint, or darkness-as-cover;
- at least one route/scale cue: coastline path, island shelf, rocks, descent line, equipment, station, or visible destination.

Reject:
- neon/acid water or terrain;
- black water surface hiding weak art;
- flat tinted plane with no wave/material response;
- primitive Aegir dot/disc;
- surface with no route or scale cue.

### Shoreline Close

Pass requires:
- organic foam contact at rocks/shoreline;
- wetness/material transition at waterline;
- textured rocks with strata, erosion, sediment, roughness/specular variation;
- shallow water transparency or depth falloff;
- no debug foam sheets, black strips, or hard clipping.

Reject:
- foam as a uniform lace/grid/sheet overlay;
- hard black streaks at shoreline;
- pale slab terrain;
- shoreline that is just a water plane intersecting a low-detail mesh.

### Underwater 0-5 m

Pass requires:
- actual underwater view, not a surface/coast duplicate;
- bright photic clarity with readable water volume;
- seabed or shore shelf visible with caustic/floor response where light supports it;
- terrain/material detail: rocks, sediment, slope, biota, industrial trace, or route object;
- haze that gives depth without hiding geometry.

Reject:
- surface skyline/Aegir visible as the main composition while labeled underwater;
- acid green or yellow flat slab;
- empty seabed plane;
- hard waterline cut through frame;
- caustics/particles as debug clutter.

### Underwater 20-50 m

Pass requires:
- actual medium-depth camera position with believable depth cue;
- near/mid/far structure through haze;
- route/return cue, landmark silhouette, cable, wreck line, shelf, cave mouth, light, or instrument context;
- terrain detail remains readable and not hidden by fog;
- no surface-plane clipping or shallow duplicate framing.

Reject:
- same composition as surface or 0-5 m proof;
- darkness/haze hiding empty terrain;
- flat shell terrain with no geology or route;
- debug particles, black bars, or false caustic noise.

### Aegir And Celestials

Pass requires:
- Aegir large enough to sell orbital scale;
- texture/cloud bands/atmospheric softness visible;
- placed behind horizon/atmosphere rather than pasted in front of terrain/water;
- fixed map position across packet views unless camera movement explains parallax;
- not overexposed, muddy, or reduced to a primitive dot/disc.

Reject:
- primitive sphere/disc/dot;
- muddy sine stripes or low-resolution bands;
- Aegir cutting through foreground incorrectly;
- no separate celestial proof when surface composition does not clearly show it.

### Regression Low-Oblique

Pass requires:
- low/compact-like oblique angle with water, shoreline/terrain, sky/Aegir or route context;
- composition remains readable without high-end-only effects;
- no LOD collapse into flat planes;
- no hidden debug overlays or screenshot tool contamination.

Reject:
- low-oblique view that only repeats a beauty surface angle;
- terrain/surface breaks from oblique camera;
- missing route/scale cue;
- clipped water plane or black/pale slabs.

## Explicit Reject Examples From 1466-1473

- `ACID_GREEN`: neon/acid terrain or water tint used as brightness.
- `FLAT_TINT_PLANE`: broad flat green/blue/yellow plane sold as water or seabed.
- `BLACK_STREAKS`: black bands/strips at waterline or terrain contacts.
- `PALE_SLAB`: yellow/pale underwater slab with no material or route structure.
- `FALSE_LABEL`: `h8_1473_underwater_0_5m.png` and `h8_1473_underwater_20_50m_route.png` visually match the surface/coast composition, not underwater proof.
- `DEBUG_FOAM`: foam variants that read as sheet, lace, vertex/debug overlay, or unrelated clutter.
- `WEAK_AEGIR`: primitive celestial dot/disc or muddy procedural sphere.
- `EMPTY_SEABED`: seabed without geology, biota, material breakup, route cue, or scale witness.

## Pass Direction From Mandatory References

The mandatory reference images establish the acceptance direction in words:

- bright waterline with clear ocean color and shoreline contact;
- readable underwater depth haze with near/mid/far terrain structure;
- caustic floor response on shallow/lit surfaces, not global debug noise;
- organic shoreline foam, broken by rocks and wave contact;
- textured rocks with strata, wetness, sediment, and readable silhouette;
- Aegir/large celestial body behind atmosphere/horizon with scale, softness, and cloud texture.

The references are not permission to copy UI or exact composition. They define visual floor and failure contrast.

## Proof Metadata Requirements

Each packet must include a metadata manifest beside screenshots:

- packet id and capture session id;
- UTC/local timestamp for each screenshot;
- scene name and route state;
- camera id, position, rotation, FOV, near/far, and depth band;
- capture source: GameView/MainRT/SceneView, and whether UI is on/off;
- `GlobalQualityWeight` value and named quality lane;
- render scale, post stack state, underwater renderer state, fog/water/caustic/foam toggles;
- route script or capture harness version;
- screenshot checksum and file byte size;
- corresponding log path with last timestamp after final screenshot;
- fault summary: exceptions/errors/warnings/compile/import/update/playmode flags during capture window.

Missing or inconsistent metadata is `FAKE_METADATA`.

## Screenshot Naming Convention

Format:

`h8_[packet]_[session]_[view]_[quality]_[ui]_[yyyyMMdd_HHmmss].png`

Allowed `view` values:

- `surface_coast_aegir`
- `shoreline_close_1m`
- `underwater_0_5m`
- `underwater_20_50m_route`
- `aegir_celestial_long`
- `regression_low_oblique`

Examples:

- `h8_1474_s01_surface_coast_aegir_q060_uioff_20260604_183000.png`
- `h8_1474_s01_underwater_20_50m_route_q060_uioff_20260604_183045.png`

Ad hoc diagnostic toggles may use extra suffixes, but they cannot replace the six required packet views.

## Brightness Gate Without Neon Abuse

Surface brightness passes only when luminance is carried by sky, sun/atmosphere, water reflectance, terrain exposure, and material response. It fails when brightness is achieved by clipped white, acid green/yellow, full-screen teal, or crushed contrast.

Judge by:
- visible material identity after brightness: rock stays rock, water stays water;
- no single hue dominates the whole frame;
- terrain has shadows/highlights but not black-crushed masses;
- water has natural blue/green depth variation, not debug saturation;
- Aegir remains atmospheric and textured, not a blown-out disc.

## Underwater Haze Gate

Underwater haze passes when it stages distance while preserving route structure. It fails when it erases terrain, hides empty seabed, or turns the frame into a monochrome sheet.

Minimum readable elements:
- near foreground material;
- mid-distance route/landmark silhouette;
- far falloff;
- at least one return/hazard/scale cue.

Darkness is allowed in caves, storms, interiors, and deeper bands, but open 0-100 m water remains bright/readable unless a specific temporary event is documented.

## Foam And Caustics Gate

Foam passes when it has contact cause, scale, breakup, and wet shoreline relation. Caustics pass when light, depth, and surface state justify them.

Reject:
- uniform foam sheet;
- lace/vertex/debug overlay;
- caustics below believable shallow/lit depth without light source;
- caustics used to hide flat terrain;
- particles/noise replacing material detail.

## Terrain And Material Gate

Terrain passes when geology and material detail survive gameplay camera distance:

- macro silhouette: shelf, ridge, cove, slope, arch, trench, cliff, or route line;
- meso breakup: ledges, cracks, sediment, erosion, debris, coral/biota, industrial trace;
- micro material: normals, wetness, roughness/specular variation, strata, decals or masks.

Reject flat terrain shells, smooth noise hills, empty seabed planes, and low-poly cliffs dressed only by tint.

## Aegir Gate

Aegir passes only if:

- large and beautiful enough to establish orbital scale;
- textured with cloud bands or gas/atmosphere detail;
- softened by atmosphere and horizon integration;
- stable/fixed relative to map and sky rules;
- not a primitive dot, flat disc, pasted sphere, or muddy sine-band procedural object.

## Runtime Fault Gate

No visual packet can be accepted if any of these are true during or after the capture window:

- repeated exception or unresolved error spam;
- `H8_PLAYMODE_EXIT` or invalid forced-load exit;
- compile loop or import/update loop;
- shader/material error;
- screenshot route writes into `Assets` and triggers import churn;
- latest log predates the final screenshot;
- no log tail showing clean post-capture state.

This gate does not require 2305 to run Unity. It requires future Unity owners to attach clean evidence.

## One-Page Rejection Steer Template

Use this when rejecting a packet:

```
Packet:
Verdict: REJECTED VISUAL PROOF
Evidence class: STATIC VERIFIED / PLAYER-CAPTURE CANDIDATE / PENDING VERIFICATION

Top reject codes:
1.
2.
3.

Failed views:
- Surface:
- Shoreline:
- Underwater 0-5 m:
- Underwater 20-50 m:
- Aegir/celestial:
- Regression low-oblique:

Objective fault gate:
- Latest screenshot timestamp:
- Latest log timestamp:
- Exceptions/errors/forced-load/import-loop:

Required 1474-style recapture:
- Six required views from one session.
- Metadata manifest with camera/depth/quality/log/checksum.
- Clean post-capture runtime tail.
- Actual underwater 0-5 m and 20-50 m route views.
- Surface/shoreline brightness without neon saturation.
- Foam/caustics as organic water response, not debug overlays.
- Textured rocks/seabed and Aegir atmospheric scale.
```

## 1473 Verdict

Verdict: REJECTED VISUAL PROOF.

Top reject codes for original 1473:

1. `FALSE_LABEL`: `h8_1473_underwater_0_5m.png` and `h8_1473_underwater_20_50m_route.png` show the same surface/coast/Aegir composition, not underwater proof.
2. `MISSING_VIEW`: no valid actual 20-50 m route proof exists in the accepted packet set.
3. `STALE_LOG`: Batch22 evidence states no clean post-1473 capture runtime tail for the original packet.
4. `RUNTIME_FAULT`: prior/current visual-audit evidence includes repeated celestial `ArgumentNullException` and forced-load/import-loop risks until a clean later packet proves closure.
5. `PALE_SLAB` / `FLAT_TINT_PLANE`: later `h8_1473_mainrt_underwater_0_5m.png` diagnostic is underwater but shows flat acid/pale slab content, not acceptable photic shallows.

What 1474 must include:

- six required packet views from one capture session using the naming convention above;
- actual underwater 0-5 m and 20-50 m views with visible depth, seabed, route, haze, and material response;
- bright surface and shoreline without neon/acid saturation;
- organic foam/wetness and justified caustics, not debug sheets;
- textured rocks/seabed and Aegir behind atmosphere/horizon;
- metadata manifest with checksums, camera/depth, `GlobalQualityWeight`, post stack, renderer states, and current clean log after final screenshot.
