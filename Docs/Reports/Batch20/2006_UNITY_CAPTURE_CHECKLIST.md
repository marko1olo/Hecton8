# Batch20 Worker 2006 - Unity Capture Checklist

Status: future Unity-owner checklist. Worker 2006 did not open Unity, capture screenshots, run validators, or make runtime claims.

## Preflight

1. Confirm no other Unity/build/profiler task owns the slot.
2. Run the source-aware validator after Aegir/cloud/moon extensions exist.
3. Confirm console has no shader/material/texture import errors.
4. Record active scene, render pipeline asset, quality level, display resolution, camera, time of day, weather state, and `GlobalQualityWeight`.
5. Confirm active skybox material and active Aegir/moon prefabs from runtime, not from static file assumptions.

## Required Capture Set

Capture paired SceneView and GameView shots from the same headings. Do not claim parity without pairs.

1. 360 pan at sea level: headings 0, 45, 90, 135, 180, 225, 270, 315 degrees.
2. 360 pan at 5 m above ocean: same headings.
3. 360 pan at 30 m above ocean: same headings.
4. Waterline shots: half sky / half ocean, facing Aegir, facing away from Aegir, facing sun.
5. Shallow underwater shots: 1 m, 5 m, 20 m, 50 m depth, looking up toward sky and horizon where possible.
6. Aegir close read: horizon-adjacent, high-sky, and off-axis positions.
7. Moon read: each visible moon at normal exposure and with sun glare nearby.
8. Sun/exposure read: sun near horizon, sun high, sun just outside frame.
9. Cloud read: zenith, horizon bank, moving cloud detail, mipped distance.
10. SceneView enforcer off/on comparison if safe: prove it is not hiding GameView mismatch.

## Quality Weight Passes

Use continuous `GlobalQualityWeight` values, not binary tiers:

- Low: 0.0
- Middle: 0.35
- High: 0.7
- Ultra: 1.0

For each pass, record:

- Visible Aegir silhouette and band readability.
- Moon silhouette and phase readability.
- Cloud detail survival after mip/compression.
- Horizon/ocean relation.
- Gameplay readability: heading, coastline, waterline, depth transition, glare danger.
- Frame time and GPU/CPU cost if profiling is allowed by the slot owner.

## Rejection Conditions

- Surface is darkened, stormed, fogged, or night-shifted to hide weak sky.
- Aegir reads as pale sticker, flat circle, sine-striped procedural ball, hard-edged billboard, or translucent disc.
- Moons read as reused terrain rock, noisy blobs, or invisible grey dots.
- Clouds read as flat gradients, muddy alpha sheets, Perlin mush, or repeated obvious tiles.
- Sun/exposure crushes the horizon, ocean, clouds, Aegir, or moons.
- SceneView looks correct but GameView does not.
- Ocean surface and sky appear unrelated.
- Low quality becomes primitive or muddy.
- Ultra quality changes gameplay truth or authority route.

## Required Evidence Fields

Each capture artifact must record:

- Scene path.
- Camera path and mode.
- SceneView or GameView.
- Heading/elevation/depth.
- Time of day/weather state.
- `GlobalQualityWeight`.
- Active skybox material.
- Active Aegir prefab path and material.
- Active moon material names.
- Active cloud atlas texture GUID/path.
- Exposure/tone mapping settings.
- Capture timestamp.

## Proof Boundary

A screenshot can prove only what it shows. A single beautiful still does not prove 360 consistency, Scene/Game parity, gameplay readability, performance, or source-route correctness.
