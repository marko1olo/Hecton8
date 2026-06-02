# HECTON-8 Vehicles Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: submarines, suits, docked seats, vehicle interiors, EVA handoff, platform-relative motion, docking, crushing pressure, cockpit feedback, and vehicle proof.

## Prime Law

Vehicles are not mounts. They are pressure vessels, industrial tools, and survival liabilities. HECTON-8 rejects floaty submarines, arcade hover boxes, cozy mobile homes, and vehicles whose interiors ignore their own motion.

A vehicle must make mass, inertia, pressure, power, noise, visibility, docking, and failure readable.

## Vehicle Truth Ownership

Vehicle truth owns:

- AUP position;
- local offset;
- linear velocity;
- angular velocity;
- pressure rating;
- hull integrity;
- power state;
- ballast or buoyancy state where gameplay-relevant;
- noise emission;
- docked/attached occupants;
- active tool mounts;
- cockpit instrument state.

UI, camera, audio, and VFX consume vehicle truth. They do not invent vehicle state.

## Platform-Relative Motion

Moving interiors need platform-relative math. Player and props inside a moving vessel must inherit platform translation and rotation through cached platform transforms, not parenting hacks.

Rules:

- cache platform matrices once per fixed tick;
- apply platform delta before player movement delta;
- preserve full 3D local input on tilted hulls;
- never flatten Y input just because a hull is pitched;
- cache point velocity at attachment points;
- no repeated matrix inversion per subsystem;
- no `transform.parent` as movement truth.

## Docking And EVA

Docking is an explicit state, not a teleport.

Required states:

- approach;
- align;
- clamp;
- pressure equalize;
- transfer;
- release;
- emergency abort.

EVA handoff preserves momentum. Exit velocity is vehicle point velocity plus player relative velocity. The player must not drop into inertial zero because the vehicle state was ignored.

## Submarine Feel

A submarine must feel heavy and constrained:

- delayed acceleration;
- readable braking;
- limited turn authority;
- thruster noise;
- hull groan under pressure;
- power draw;
- sonar consequence;
- collision fear;
- visibility cost;
- docking precision.

Compact tier can simplify hydrodynamics, but it must keep heavy feel through input response, audio, camera, and cockpit readouts.

## Cockpit And Instruments

Cockpit UI is vehicle hardware. It must expose:

- depth;
- pressure margin;
- hull state;
- power draw;
- oxygen/life support where applicable;
- sonar trust;
- route signal;
- dock state;
- noise output;
- flood/breach alerts.

The cockpit cannot become a clean sci-fi dashboard. It must behave like worn industrial instrumentation fighting condensation, vibration, power noise, and corporate lies.

## Damage And Failure

Vehicle damage must leave physical and instrument evidence:

- hull scars;
- flickering readouts;
- stuck ballast;
- failing lights;
- thruster imbalance;
- pump noise;
- clarity loss;
- pressure alarms;
- emergency docking constraints.

Failure should create decisions before death. If a vehicle simply explodes without readable lead-up, it is rejected unless it is a deliberate catastrophic event with prior evidence.

## Collision And Contact

Vehicle collision truth uses simplified proxies and force packets. Decorative hull detail is not collision. Docking clamps and tool mounts need named anchors, not guessed local positions.

Forbidden:

- LOD0 vehicle mesh as collider;
- direct per-system Rigidbody force ownership;
- docking based on visual overlap only;
- runtime collider cooking;
- camera shake as substitute for collision truth.

## Scalability

`GlobalQualityWeight` scales vehicle presentation density: cockpit material response, sonar polish, exterior detail, secondary hull motion, camera/audio layers, damage decals, and water interaction effects. It never changes vehicle authority, docking truth, EVA handoff, collision proxy identity, save identity, or platform-relative motion math.

Compact uses scalar buoyancy/pressure, simple vehicle proxies, strong audio, cockpit alarms, and authored camera response. Middle adds richer thruster/contact feedback. High adds better flood/damage presentation and cockpit material response. Ultra adds secondary hull motion, richer sonar/cockpit effects, and denser exterior detail without changing vehicle truth.

## Proof Artifacts

Vehicle work must provide:

- vehicle truth owner and dispatcher phase;
- AUP/local pose, velocity, angular velocity, pressure, power, and damage state route;
- docking/EVA state machine capture;
- platform-relative motion proof for occupants;
- collision proxy and force packet proof;
- cockpit/instrument source owner list;
- Compact and High motion/cockpit capture;
- profiler/GC proof for runtime vehicle changes;
- black-box fields for crash, NaN, docking failure, pressure failure, and stuck occupant recovery.

Static source inspection cannot claim vehicle feel. Vehicle feel requires capture.

## Rejection Gates

Reject:

- floaty arcade motion without mass;
- interiors that ignore hull rotation;
- docking as instant teleport;
- EVA with no momentum inheritance;
- vehicles with no pressure/power/noise cost;
- cockpit UI that decorates instead of informing;
- vehicle reports without kinematic state, pressure state, collision proxy, cockpit proof, and low-tier fallback.

## Acceptance Sentence

A vehicle is accepted only when it feels heavy, vulnerable, readable, phase-owned, collision-safe, and inseparable from pressure, power, sound, route, and survival.
