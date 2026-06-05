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

## Owner And Hot-Path Law

Physics gameplay truth is owned by the specific physics phase/system that consumes cached force, collision, pressure, tether, contact, or damage packets. UI, VFX, audio, haptics, animation, camera, localization, and editor previews are presentation-only consumers; they must not apply forces, resolve contacts, mutate collision truth, or invent damage/pressure state.

Hot physics paths must be zero-GC and owner-routed. Forbidden in active contact, movement, force, tether, flooding, or damage loops: managed allocation, LINQ, string lookup, scene search, hot `GlobalRegistry` polling, `GetComponent`, `Camera.main`, per-contact delegates, direct `Rigidbody.AddForce` outside the apply lane, synchronous GPU readback, same-frame schedule/readback loops, and hidden `.Complete()` outside an approved completion window.

Physics read accessors are pure. `Get*`, `TryGet*`, `Resolve*`, and `Read*` paths may not publish signals, allocate/grow buffers, complete jobs, sync scene state, search objects, or mutate authority.

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

Cable sag, vibration, bend, recoil, and distant readability default to visual splines, VAT, audio, haptics, and shader approximations. Per-frame bend raycasts are rejected for presentation-only cables.

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

Character and vehicle environment collision route:

- terrain, voxel cave, tunnel, and large geology traversal collision uses baked voxel/terrain colliders or an approved SDF read model/DataVault snapshot owned by the voxel/terrain route;
- hot movement and vehicle loops must not run synchronous `SphereCast`, `CapsuleCast`, or `Raycast` chains as the primary environment collision truth;
- `RaycastCommand.ScheduleBatch` or bounded `Physics.*NonAlloc` casts are allowed for tool contact, interaction probing, scanner/sonar utility, or strict one-off diagnostics only when the owner phase, cadence, static buffers, and profiler/GC proof are named;
- if a temporary cast route is used while SDF/collider bake proof is missing, report it as `PENDING VERIFICATION` or explicit migration debt, not final collision architecture.

## Vehicle Force And Damage Source Anchors

Evidence class: STATIC_SOURCE only.

- `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsContracts.cs` defines `SubmarineForceAccumulator`, `SubmarineKinematicTelemetry`, `SubmarineHydrodynamicsTelemetry`, `GyroTelemetryEntry`, and `SubmarineDynamicsConstants.BlackBoxFrames = 300`.
- Force ownership boundary: control, buoyancy, drag, impact, gyro, flood, and damage penalties converge into `SubmarineForceAccumulator` for the submarine simulation lane. UI, VFX, audio, lighting, and damage jobs do not own direct Rigidbody force application.
- `Assets/_Project/Scripts/Physics/Vehicles/VehicleComponentDamageJobs.cs` maps AUP impact signals into `VehicleGridCellDTO`, reduces cell integrity, evaluates thrust/buoyancy/sensor/drag/flood/fire/structure scalars, and publishes `VehicleDamageStateDTO`.
- Damage ownership boundary: damage jobs own component damage state, not vehicle force truth. `SubmarineDynamicsRuntime` consumes only the published DTO scalars.
- Black-box presence: vehicle dynamics and component damage source contain 300-frame telemetry lanes. Runtime dump artifact, decode proof, Unity import, profiler/GC proof, and player-build proof are absent in this static pass.

## GlobalQualityWeight Scaling

Low/Middle/High/Ultra are continuous planning labels on the same `GlobalQualityWeight` curve, not binary switches:

- Low: core collision truth, pressure channels, scalar flooding, simple force packets, strong audio/UI feedback.
- Middle: denser contact sampling where budgeted, clearer local damage presentation, richer tether visuals.
- High: better leak, wetness, debris, haptic, and material response around the same force/contact truth.
- Ultra: secondary physical presentation and debug richness only within bounded owner phases, never unbounded simulation truth.

`GlobalQualityWeight` may scale secondary debris, leak particle density, wetness material richness, tether visual segment count, contact audio/haptic richness, debug draw depth, and noncritical presentation cadence. It must not change collision truth, damage channel ownership, force authority, fixed-step route, save identity, or gameplay contact semantics.

## First-20 Route Hook

Physics first-20 proof is required for oxygen/pressure danger, tool contact, basic collision proxies, first hull/module damage, first leak/flooding presentation, and safe recovery from invalid force/contact data. If scalar state and premium presentation preserve belief, do not add physical simulation.

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

If fluid, tether, vehicle, or tool contact behavior changed, the proof must include the owning route and why a cheaper premium approximation was insufficient.

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

Physics is accepted only when it is bounded, phase-owned, deterministic, readable to the player, and more useful than a cheaper premium presentation approximation.
