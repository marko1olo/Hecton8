# Batch20 / 2010 Three-Pillar Acceptance

Status: STATIC VERIFIED / NO UNITY / NO IN-GAME PROOF

## Prime Rule

A visual repair passes only when graphics, optimization, and gameplay all pass. One failed pillar rejects the work.

This task did not run Unity. All runtime gates below remain `PENDING VERIFICATION` until fresh artifacts exist.

## Pillar 1: Graphics

Pass requires:

- Surface, sky, Aegir, moons, coastline, ocean surface, photic shallows, and medium-depth hero routes meet or exceed Subnautica-level readability and beauty.
- Low label over `GlobalQualityWeight` still looks authored: strong silhouettes, clean water color, readable terrain, motivated lighting, material identity, and route landmarks.
- High and Ultra add sensory richness: foam breakup, cloud depth, reflection, wetness, silt, material masks, local shafts, denser ecology, better close-camera detail.
- Darkness is restricted to depth, caves, interiors, storms, and temporary eclipse windows. It cannot hide surface or shallow weakness.
- Every screenshot contains at least one route, pressure, machinery, scale, danger, evidence, or player-decision cue.

Reject:

- Flat/muddy/primitive low-tier visuals.
- Generic blue aquarium haze.
- Black fog hiding missing content.
- Aegir/moons as low-resolution blobs or procedural scribbles.
- Coastline terrain that reads as smooth noise or blocky filler.
- ProductFace objects built from primitive capsules/boxes with texture noise.

## Pillar 2: Optimization

Pass requires:

- Hot paths allocate 0 B/frame for 300-frame capture.
- Any runtime feature over 0.1 ms has profiler proof, load-shed path, and lower-cost fallback.
- RenderGraph/Frame Debugger proof exists for new render passes, buffers, shoreline foam, waterline, fog, volumetrics, post, or ocean wake changes.
- VRAM and memory evidence exists for any texture, RT, buffer, particle, terrain, or HLOD residency change.
- `GlobalQualityWeight` scales cadence, resolution, density, distance, sample count, shader loop limits, and diagnostics continuously.
- LOD/HLOD changes use hysteresis: minimum 3 seconds or 5 meters.
- Fake-first route is documented before simulation: shader/LUT/VAT/impostor/flow-map/proxy before physical simulation.

Reject:

- Binary quality branches.
- Runtime JSON/string parsing or GlobalRegistry hot polling.
- Material clones, hidden blits, unbounded transparent overdraw, full volumetric default on compact.
- Exact microsecond claims without profiler evidence.
- Optimization that buys no visible improvement.

## Pillar 3: Gameplay

Pass requires:

- Quality never changes gameplay truth ownership, DTO layout, save identity, collider/hitbox truth, route authority, resource identity, command semantics, tide phase truth, or hazard truth.
- Low label preserves survival readability: oxygen/pressure warnings, route cues, return path, hazard silhouettes, instruments, and interaction affordances.
- High and Ultra add sensory information only. They do not reveal hidden gameplay truth unavailable to Low/Middle.
- Visual fakes do not lie about missing systems. If foam, lights, UI, damage, gas, or weather implies a playable state, the owning system must publish that state.
- Screenshot proof includes a player decision or route consequence.

Reject:

- Ultra-only route readability.
- Low-tier removal of the only hazard cue or return path.
- Presentation that invents damage, power, flooding, storm, or objective state.
- Pretty scenes with no route decision, evidence, survival pressure, or machinery logic.

## Required Review Sentence

Use this exact review shape for each repaired scene/domain:

`Graphics: [PASS/FAIL/PENDING]. Optimization: [PASS/FAIL/PENDING]. Gameplay: [PASS/FAIL/PENDING]. Overall: [PASS only if all three pass]. Evidence: [artifact paths].`

For this Batch20 worker 2010 deliverable, overall state is `PENDING VERIFICATION` because no Unity, screenshot, profiler, GC, Frame Debugger, RenderGraph, Memory Profiler, or player-build artifact was produced.
