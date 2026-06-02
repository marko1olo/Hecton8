# HECTON-8 Physics Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: pressure, hull integrity, fluid incursion, tethers, cables, hands, tool contact, collision truth, damage coupling, and physics presentation.

## Prime Law

Physics is gameplay truth only where it changes player decisions. Everywhere else, use a controlled cinematic fake: scalar state, shader wetness, pooled leak VFX, audio, haptics, UI alarms, and authored animation. Simulating invisible causes is rejected when a cheaper consequence preserves belief.

## Truth Channels

Damage and pressure systems must separate:

- `INTEGRITY`: hull, frame, armor, structural survival.
- `POWER`: propulsion, pumps, lights, tools, life support, sensors.
- `CLARITY`: sonar, visor, HUD, scanner trust, signal fidelity.

These channels are continuous floats. Cross-channel bleed is explicit and event-driven. A single damage number is rejected because it cannot drive readable HECTON-8 failure.

## Fluid And Pressure

Interior flooding defaults to scalar fill ratio plus presentation. Real mass, center-of-mass, inertia, or control truth changes are allowed only for player-controlled vessels, active salvage objects, or hull-breach gameplay where the player can inspect and react.

Forbidden:

- continuous room-scale fluid simulation for background compartments;
- per-frame Rigidbody mass or center-of-mass writes without profiler proof and fallback;
- pressure simulation for rooms the player cannot enter, inspect, or control;
- visual leaks that do not correspond to damage state.

Required presentation stack:

- breach point;
- fill ratio;
- wetness mask;
- audio stress;
- pump state;
- haptic or camera response where relevant;
- UI alarm with trust degradation;
- black-box telemetry for last 300 frames.

## Tethers, Cables, And Grapples

Primary gameplay tether constraints use owned constraint packets or approved acceleration/Verlet-style packets consumed by the physics apply pipeline. Unity production joints are rejected as the default path.

Cable sag, vibration, bend, recoil, and distant readability default to visual splines, VAT, audio, haptics, and shader fakes. Per-frame bend raycasts are rejected for presentation-only cables.

Tether truth must define:

- anchor identities;
- rest length;
- max correction;
- snap stress accumulation;
- force ownership;
- topology cache;
- visual segment count by quality;
- load-shed fallback.

## Hands, Tools, And Contact

Player hand, cutter, welder, scanner, pry, and repair contact must be stable and readable. Tool contact uses bounded raycasts or shape casts, cached target IDs, fixed cadence, and deterministic result packets. Presentation can add sparks, heat, decals, tool shake, audio, and haptic ramps, but the target truth is owned by the tool/interaction system.

No tool may push arbitrary Rigidbody forces directly from UI, animation, VFX, or input code.

## Collision Truth

Collision is not visual detail. Visual meshes, decorative bolts, coral branches, hanging cables, and high-poly geology are not physics truth.

Required:

- primitive compounds for machines, modules, doors, and props;
- convex proxies for irregular rocks, coral, debris, and carcasses;
- cooked assets offline where possible;
- collider layer assignment before prefab save;
- interaction blockers separate from visual LODs;
- physics bake state for voxel chunks before interaction is enabled.

## GlobalQualityWeight Scaling

Compact keeps core collision, pressure channels, scalar flooding, simple force packets, and strong audio/UI feedback. Middle increases contact fidelity and local damage presentation. High adds richer leak/wetness/debris response. Ultra adds more secondary physical presentation, not more unbounded truth.

`GlobalQualityWeight` may scale secondary debris, leak particle density, wetness material richness, tether visual segment count, contact audio/haptic richness, debug draw depth, and noncritical presentation cadence. It must not change collision truth, damage channel ownership, force authority, fixed-step route, save identity, or gameplay contact semantics.

## Proof Artifacts

Physics work must provide:

- owner phase and force/collision route;
- channel state for integrity, power, and clarity where relevant;
- collider/proxy proof;
- fixed-tick or dispatcher cadence;
- profiler/GC proof for runtime physics changes;
- Compact and High presentation capture if visible;
- black-box fields through `telemetry.md`;
- recovery/fallback behavior for NaN, invalid force, over-budget, and collision proxy failure.

If fluid, tether, vehicle, or tool contact behavior changed, the proof must include the owning route and why a cheaper fake was insufficient.

## Rejection Gates

Reject:

- LOD0 as a runtime collider;
- runtime collider cooking for ordinary gameplay;
- per-frame `AddForce` ownership outside physics apply;
- hidden Unity joints as production tethers;
- fluid simulation where scalar state is enough;
- decorative damage with no channel state;
- physics reports without profiler/GC proof.

## Acceptance Sentence

Physics is accepted only when it is bounded, phase-owned, deterministic, readable to the player, and more useful than a cheaper visual fake.
