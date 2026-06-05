# 3106 Underwater Route Volume Owner

ID: `3106`
Role: `UNDERWATER_ROUTE_VOLUME_OWNER`
Date: 2026-06-05
Status: `STATIC VERIFIED` for route/VFX classification. `PENDING VERIFICATION` for Unity readback, visual acceptance, Frame Debugger, profiler, GC, player capture, and ProofGate packet.

## Scope

Static owner pass only. No Unity, no build, no scene/material/prefab/shader mutation.

Reason: process gate is red. Active sampled blockers: Unity 4616, Unity.ILPP.Runner 14928, UnityAutoQuitter 2752, UnityShaderCompiler 13716.

## Mandates Followed

- `.agents-skills/REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

Authority read:

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `water.md`
- `rendering.md`
- `terrain.md`
- `world.md`
- `vfx.md`
- `taskslocal/batch31_night_visual_recovery/3106_UNDERWATER_ROUTE_VOLUME_OWNER.txt`
- `Docs/Reports/Batch31/MATERIAL_TEXTURE_CRITICALS_20260605.md`
- `Docs/Reports/Batch31/CONTROLLER_SYNTHESIS_20260605_0118.md`
- `Docs/Reports/Batch31/3102_PROOF_HARNESS_1475_OWNER.md`
- `Docs/Reports/Batch31/3103_WATER_CREST_FOAM_CAUSTIC_OWNER.md`

## Verdict

No current underwater proof exists.

Current rejected frames are either false surface captures or a flat green underwater fill. They fail the 0-100 m light lock, route grammar, particle volume, material truth, and return readability requirements.

## Current Capture Classification

| Capture | Classification | Reason |
|---|---|---|
| `h8_1473_underwater_0_5m.png` | REJECT / FALSE LABEL | Surface horizon, sky, Aegir, coastline, and ocean skin dominate. No underwater route proof. |
| `h8_1473_underwater_20_50m_route.png` | REJECT / FALSE LABEL | Same surface composition; no 20-50 m depth predicate. |
| `h8_1473_mainrt_underwater_0_5m.png` | REJECT / FLAT FILL | Green/yellow slab, weak surface underside, no seafloor route, no foreground/mid/background separation. |
| `h8_1474_underwater_0_5m.png` | REJECT / FALSE LABEL | Surface shot, not underwater. |
| `h8_1474_underwater_20_50m_route.png` | REJECT / FALSE LABEL | Surface shot, not medium-depth route. |

## Hard Acceptance: True 0-5 m Underwater View

Required predicates:

- camera is underwater, visual depth `0.5-5.0 m`;
- sky/horizon is not dominant; the surface underside may occupy upper frame but not replace the underwater view;
- surface underside shows believable wave/foam/refraction contact or a premium substitute;
- foreground has readable wet rock/coral/industrial trace;
- midground has route anchor, cable, pinger, salvage cut, buoy shadow, or other return cue;
- background has shelf/coastline underside/depth falloff, not flat fog;
- suspended motes/snow are visible but not global noise;
- at least one scale cue: fish silhouette, cable, rock shelf, module debris, or terrain landmark;
- water color remains bright/readable photic water, not abyss noir, black void, or toxic green fill;
- UI/instrument route cue is visible if the proof view claims playable route context.

Reject if:

- sky, Aegir, or horizon dominates;
- the image is a flat color sheet;
- no particle volume exists;
- no return cue exists;
- the seafloor/terrain material reads as primitive or hidden.

## Hard Acceptance: True 20-50 m Route View

Required predicates:

- camera is underwater, visual depth `20-50 m`;
- route is a medium-depth photic/twilight transition, not abyss darkness;
- foreground/midground/background are separated by terrain, particles, haze, and light falloff;
- a forward route and return cue are both visible;
- terrain reads as shelf/ridge/cable path/canyon/industrial wreck logic, not empty plane;
- particles support depth and disturbance: motes/snow/silt/bubbles by cause, not constant screen filler;
- beams or caustic hints exist only with a believable light reason;
- route anchors survive compact quality through silhouette, not ultra-only VFX;
- no fog/post/darkness hides weak geometry.

Reject if:

- surface horizon remains the main composition;
- the view is generic green/blue haze;
- no route decision is readable;
- no scale/evidence cue exists.

## Static Underwater Owner Findings

`Assets/_Project/Scripts/HectonUnderwaterVisuals.cs` has routes for:

- suspended motes;
- GPU marine snow;
- exhale bubbles;
- shallow sun beam;
- depth haze/fog;
- surface/light transitions;
- adaptive motes/bubbles/beam scaling through quality-weight-like budget floors.

Scene serialization for `H8_UNDERWATER_VISUALS_RUNTIME_OWNER_1474` shows:

- `underwaterSuspendedMotes: {fileID: 0}`
- `underwaterMarineSnow: {fileID: 0}`
- `underwaterExhaleBubbles: {fileID: 0}`
- `shallowSunBeamLight: {fileID: 0}`
- `enableSuspendedMotes: 1`
- `enableExhaleBubbles: 1`
- `enableShallowSunBeam: 1`
- `adaptiveMotesBudgetFloor: 0.55`
- `adaptiveBubbleBudgetFloor: 0.6`
- `adaptiveBeamBudgetFloor: 0.7`

Static verdict: runtime fallback lookup may bind camera children/components, but scene serialization does not prove those children exist, are enabled, render, or pass visual quality.

## VFX / Volume Classification

| Item | Static State | Disposition |
|---|---|---|
| Motes | Script route exists; scene direct ref null; `MAT_H8_PhoticMotes_1428` `_BaseMap` null. | BLOCKED. Needs real sprite/atlas or GPU snow proof. |
| Marine snow | Script route exists via `HectonMarineSnowRenderer`; scene direct ref null. | PENDING UNITY READBACK. Prefer GPU-side drift with hard pool caps. |
| Bubbles | Script route exists; scene direct particle ref null; bubble trail can route through marine snow/FluidBubbleBurstSink if operational. | PENDING UNITY READBACK. Event-driven only. |
| Shallow beams | Script route exists; scene light ref null; depth/light fade exists statically. | PENDING UNITY READBACK. Must be shallow-light gated. |
| Haze | Scalar fog/haze route exists; `H8_UnderwaterHazeCurtain_1454` exists in scene. | REJECT curtain/slab as proof until bounded and route-readable. |
| Surface sheet | `H8_UnderwaterSurfaceSheet_1455` exists in scene. | HIGH RISK. Reject if it produces flat green/yellow fill or hides weak water. |
| Route anchors | `Route_Frontier`, `Lane_DarkRoute`, `Lane_BeaconRoute`, `Route_Anchor`, and world anchors exist statically. | Use `Route_Anchor` and `Lane_BeaconRoute` for next proof pair; do not make first proof a dark route. |
| Fish silhouette | `MAT_H8_PhoticFishSilhouette_1430` exists but texture slots are null. | BLOCKED. Needs real silhouette texture/mesh proof. |
| Foam ring | `MAT_H8_SurfaceFoamRing_1432` exists but `_BaseMap` null. | BLOCKED. Color-only ring rejected. |
| Visible foam | `MAT_H8_VisibleFoamUnlit_1436` exists but `_BaseMap` and `_MainTex` null. | BLOCKED. Needs mask/contact proof. |

## Proposed Actual Proof Route

### View A: `underwater_0_5m`

Camera:

- position below surface at visual depth `1.5-3.0 m`;
- pitch slightly upward/forward so the surface underside reads in the upper third;
- keep seabed/coral/rock shelf in lower half;
- include `Route_Anchor` or a pinger/cable return marker at mid-left or mid-right;
- include a forward `Lane_BeaconRoute` cue in background.

Required content:

- surface underside with wave/refraction read;
- foreground rock/coral/industrial trace;
- sparse motes and 1-2 scale silhouettes;
- readable return cue;
- no dominant sky/horizon.

### View B: `underwater_20_50m_route`

Camera:

- position at visual depth `28-38 m`;
- look along shelf/canyon/cable route, not upward to horizon;
- keep route anchor behind or side-readable as return cue;
- include forward route beacon, industrial debris, or terrain landmark.

Required content:

- terrain shelf/ridge/cable path;
- medium-depth haze with structure;
- particles/silt only as evidence;
- route and return cues visible at compact quality;
- no abyss-dark grade.

## Visual Fake Route

Default implementation path:

1. Authored route anchors and geometry.
2. Baked/assigned sprite masks for motes, fish silhouettes, foam ring, and visible foam.
3. GPU-side marine snow / impostor drift where available.
4. Event-driven bubbles and silt from player movement, exhale, impact, or tool events.
5. Bounded shallow beam proxy tied to depth and light factor.
6. Haze as fog/depth presentation, not a curtain that hides weak assets.

Rejected:

- full water/particle physics as default;
- global always-on snow/bubbles/debris;
- unbounded transparent curtains;
- surface sheet that creates flat green fill;
- fog/darkness as art concealment.

## GlobalQualityWeight Consequences

These are continuous anchors, not binary switches.

- Low / compact: route silhouettes, clean photic water color, sparse sprite/GPU motes, one readable return cue, no broad haze curtain, no flat sheet, no ultra-only readability.
- Middle: more local particles, better foam/fish masks, modest shallow beam, richer route landmark material response.
- High: denser but bounded motes/snow, richer local silt/bubbles from events, stronger surface underside/refraction detail, longer landmark readability.
- Ultra: volumetric-looking particle layering, richer light shafts, denser ecology silhouettes, stronger material response. No new route truth or gameplay truth.

## Proof Packet Requirements

Next acceptance candidate must be under:

`Docs/Screenshots/HectonProofPackets/h8_1475_{session}/`

Required:

- `manifest.json`
- `manifest.sha256`
- copied Unity editor log
- canonical screenshot names from ProofGate
- route/depth/UI predicates
- `global_quality_weight` as continuous float and `qNNN` label
- post-capture clean log window

Underwater entries must include:

- visual depth meters;
- underwater flag true;
- route anchor hash/name;
- forward route cue;
- return cue;
- particle route state;
- active material/shader GUIDs for underwater owner, motes/snow/bubbles/beam/surface sheet/haze if present.

## First-20-Minutes Route Impact

This removes a blocker for the first bright photic exit and first swim route. The player must see where to go, how to return, and why oxygen/depth matters. Current captures do not prove that.

## Regression Model

- CPU: no code changed. Future particle/beam/haze runtime cost must be profiled; any 0.1 ms+ feature needs load-shed.
- GC: no code changed. Future hot particle/owner paths need 0 B/frame proof.
- Memory/VRAM: no asset changed. Future masks/atlases and GPU buffers need VRAM reporting.
- Cadence: future VFX must scale smoothly through `GlobalQualityWeight` and hysteresis, not binary low/high toggles.
- Correctness: VFX must not own gameplay truth, route truth, pressure, oxygen, damage, AI, or save identity.

## Verification State

Verified:

- Authority/task files read.
- Process gate is red.
- Current underwater-labeled screenshots fail.
- Scene underwater direct refs are null.
- Required photic readability material masks are null/color-only.
- Static route plan and rejection gates are documented.

Pending:

- Unity readback.
- Real underwater camera predicates.
- Material/texture binding.
- Player-capture proof.
- Frame Debugger/RenderGraph proof.
- Profiler/GC proof.
- ProofGate strict validation.
- Human visual acceptance.
