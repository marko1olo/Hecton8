# Batch20 / 2010 GlobalQualityWeight Matrix

Status: STATIC VERIFIED / NO UNITY / NO IN-GAME PROOF
Owner: Batch20 worker 2010
Scope: visual proof and scene repair acceptance matrix for `GlobalQualityWeight`

## Evidence Boundary

This report is static planning. It does not prove Unity import, Play Mode behavior, scene wiring, Frame Debugger state, RenderGraph state, profiler cost, GC allocation, Memory Profiler state, VRAM pressure, or final visual quality.

Static inspection found:

- `Tools/VerifyVisualLodMatrix.py`: PASS. Binary `Data/System/Visual_Scalability_Matrix.bin` is 2048 bytes, little-endian, 16-byte aligned, 4 tier records, 4 extra records, 0 FNV collisions.
- `Tools/VisualStressSim.py`: PASS. Evidence label is `PYTHON_OFFLINE_NOT_RUNTIME_PROOF`.
- `Data/System/Visual_Scalability_Matrix.json`: uses `TOASTER`, `DECK`, `PRO`, `GOD_MODE` tier labels and marks runtime proof `PENDING_VERIFICATION`.
- `OceanSinglePassRuntime` and shoreline foam code consume continuous `GlobalQualityWeight` for wake resolution, visual overrides, foam active limits, shader loop limits, decay, telemetry, and dump triggers.
- `ToxicOutgassingChemistryRuntime` consumes continuous quality for resolution, tick interval, sampling blend, source radius, turbulence/detail, signal stride, telemetry, and NaN-triggered black-box dump.
- Geology/topography editor tools use `GlobalQualityWeight` through smooth/saturated interpolation for preview resolution, ridge/warp, terrace strength, and DTO layout.
- World procedural validators enforce LODGroup/crossfade contracts and reject placeholder-only finals, but no scene/runtime proof was run.

## Scalar Law

`GlobalQualityWeight` is one continuous float:

- `0.0`: minimum survival presentation. It is not ugly mode.
- `1.0`: visual overkill. It is not new gameplay truth.

Low, middle, high, and ultra below are review labels over this scalar. They are not separate authority routes, DTO layouts, save identities, hitbox sets, resource tables, route truths, or gameplay semantics.

Required mapping for proof reviews:

| Label | Scalar Range | Meaning | Authority Boundary |
| --- | --- | --- | --- |
| Low | `0.00-0.30` | Compact survival readability with authored composition. | May lower density, cadence, resolution, sample count, diagnostic depth. Must preserve route, material identity, gameplay truth, and beauty floor. |
| Middle | `0.30-0.62` | Normal player-facing quality. | Full route readability and gameplay truth with disciplined presentation density. |
| High | `0.62-0.86` | Rich visual response. | Adds material detail, reflection, silt, foam, cloud depth, shadow eligibility, and longer LOD residency only where readable. |
| Ultra | `0.86-1.00` | Sensory overkill. | Adds premium near-field detail and cinematic density. Must not create new player knowledge or new gameplay truth. |

## Domain Matrix

| Domain | Low `0.00-0.30` | Middle `0.30-0.62` | High `0.62-0.86` | Ultra `0.86-1.00` | Hard Rejection |
| --- | --- | --- | --- | --- | --- |
| Sky/clouds | Bright readable sky gradient, authored cloud silhouettes, no black-crush, cheap LUT/cloud cards. | Layered cloud motion, controlled weather contrast, readable surface light. | Higher cloud depth, selective shafts, stronger weather texture detail. | Cinematic cloud depth, atmospheric softness, capture polish. | Muddy sky, sine-noise scribbles, permanent noir surface, cloud darkness hiding weak art. |
| Aegir/moons | Texture detail remains readable; scale silhouette and atmosphere survive. | Stronger bands, cloud softness, moon silhouettes in route context. | Higher texture/mask detail, reflection contribution, better atmospheric edge. | Premium celestial overkill for captures, still deterministic macro phase. | Low-res blobs, crayon bands, decorative orbit sim with no route consequence. |
| Ocean surface | Clean ocean color, readable wave normals, specular, waterline cue, low-cost foam/refraction. | Better foam breakup, shallow depth falloff, shoreline wetness, richer normals. | Local reflection, caustic hints, richer foam and spray where justified. | High-detail surface response and cinematic reflection, still fake-first. | Flat blue plane, black/muddy water, no foam/waterline at visible shore, expensive fluid sim without player decision. |
| Shoreline foam/waterline | Shader fake, active ring cap, depth compare, authored waterline readability. | More rings, better decay, stronger normal perturbation, wet rock response. | Denser foam response near route/hazard edges, better shader loop budget. | Capture-grade foam breakup and waterline detail. | Decal/camera/particle brute force without proof, foam implying flooding truth, waterline hiding terrain. |
| Photic shallows 0-5m | Bright colorful readable water, terrain visible, sparse but meaningful flora/coral silhouettes. | More coral/flora density, caustic hints, technogenic traces, route landmarks. | Richer material/wetness, denser biota, better local silt and shafts. | Visual overkill shallows with premium material response. | Aquarium haze, empty seabed, muddy fog, flat low-tier art, ultra-only route readability. |
| Medium depth 20-50m | Still readable; subdued only where depth/route says so; silhouettes and return path survive. | Stronger twilight layering, silt cues, route lights, ecology/industry contrast. | Richer shafts, material detail, dynamic local VFX where cause exists. | Dense sensory pressure without changing hazard truth. | Turning medium hero route into black void, hiding weak assets with fog. |
| Coastline terrain/rocks | Wet rock silhouettes, strata, sediment, erosion, route landmarks; reduced scatter only. | More decals, material masks, shoreline props, geology breakup. | Denser near-field geology, richer wetness and contact shadows. | Hero coastline material richness and capture detail. | Smooth noise terrain, blocky cliffs, dark surface treatment, LOD collapse removing route form. |
| Kelp/coral/flora | Authored silhouettes, anchors, harvest/route identities, dithered cutout/impostor path. | More field density, richer sway fake, better masks and biolum roles. | Denser local ecology, better secondary motion/VAT where proven. | Dense flora/coral overkill in capture zones. | Flat untextured cards near camera, alpha-blend overdraw, random glow, LOD removing anchor/harvest identity. |
| ProductFace visible objects | Macro silhouette, material identity, readable labels/instruments, LOD/proxy not primitive. | Better decals, masks, wetness, damage and functional affordances. | Richer near-field detail, selective shadows, stronger wear. | Hero close-camera polish; no new interaction truth. | Primitive capsules/boxes, texture noise hiding bad geometry, UI/instrument illegibility. |
| Screenshot/proof capture/telemetry | Compact capture must show route, material, sky/water/terrain readability; black-box ring configured. | Normal capture plus debug overlays for LOD/fog/foam/telemetry. | Profiler/Frame Debugger/RenderGraph captures for added features. | Cinematic capture plus all lower proof. | Beauty shot without decision/evidence; static docs sold as runtime proof. |

## Continuous Scaling Rules

- Use `smoothstep`, `lerp`, probability thresholds, stride scaling, cadence scaling, capacity scaling, HLOD distance, shader loop limits, and sample counts.
- Use hysteresis: minimum 3 seconds or 5 meters before quality/LOD state migration.
- Prefer visual fakes: LUT fog, shader waterline, flow maps, VAT, impostors, dithered HLOD, baked AO, material masks, audio/UI/haptic cues.
- Compact lane removes cost in this order: decorative particles, secondary shadows, volumetric depth, expensive reflection, far-field density, diagnostics. It does not remove route cues, survival warnings, instrument legibility, material identity, water color, sky readability, or hazard silhouettes.
- Ultra spends saved budget on sensory richness: near-field material detail, foam breakup, cloud depth, local silt, wetness, shafts, reflection, capture polish. It does not add gameplay truth.

## Proof Gates

Every visual repair domain above needs:

- Compact and High screenshot or capture from gameplay-height camera.
- One capture must include the player route or player decision, not only scenery.
- Frame Debugger or RenderGraph proof for render passes, buffers, shader loop changes, waterline/foam, volumetrics, or post.
- Unity Profiler proof for CPU/GPU cost and named markers.
- GC proof: 0 B/frame in hot path for 300 frames.
- Memory/VRAM proof if textures, RTs, buffers, particles, terrain, or HLOD residency changed.
- Black-box/telemetry proof: 300-frame ring, state hash, quality value, fault flags, dump path.
- Rejection note for any real physical simulation not replaced by a fake.

## Static Evidence Notes

- Existing matrix tooling is useful for boot-time fixed data validation, but `TOASTER/DECK/PRO/GOD_MODE` labels must not become binary quality branches.
- Existing ocean and atmosphere code show continuous quality consumption, but runtime acceptance remains pending until Unity/Profiler evidence exists.
- Existing world procedural validators can catch LOD and placeholder failures, but they do not prove final scene beauty.
