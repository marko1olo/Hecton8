# HECTON-8 Combat, Damage Routing, And Threat Contact Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: combat contact, damage routing, hitboxes, armor/penetration tables, creature attacks, tool-as-weapon boundaries, module damage, status effects, and combat proof gates.

## Prime Law

Combat in HECTON-8 is contact with hostile pressure, machinery, biology, and panic. It is not arena gunplay.

Every damaging event must have source, medium, impact surface, resistance, consequence, and readable feedback. If damage is just HP subtraction with a red flash, reject it. Combat should change route, oxygen, noise, tools, hull integrity, suit function, creature behavior, or extraction risk.

## Truth Ownership

Damage truth is owned by damage/survival/physics/AI/vehicle/module systems, not VFX or UI.

Combat owns hit intent, attack/contact rules, weapon/tool boundaries, hitbox contract, damage event shape, and reaction expectations. It consumes physics contacts, tool state, AI attack state, and survival/module resistance snapshots. VFX/UI/audio/haptics present damage but do not invent it.

## Damage Event Contract

Every damage packet must include:

- source id/type;
- target id/type;
- contact point or local hit volume;
- damage channel;
- impulse or pressure component;
- material/resistance row;
- status flags;
- timestamp/tick;
- authority owner;
- black-box fields for last 300 frames where critical.

Damage channels include pressure, cut, burn, pierce, crush, shock, toxin, radiation, hypoxia, decompression, and biological contamination. Channels must map to survival, module, armor, creature, or world consequences.

## Hitboxes And Penetration

Rules:

- hitboxes use local AABB/capsule/sphere/convex primitives, not visual mesh triangles;
- armor/penetration uses baked LUTs or scalar tables;
- angle/material/resistance must matter where the mechanic is visible;
- hitbox names are stable ids, not scene object names;
- critical hits require readable anatomy or module affordance;
- large fauna contact uses body zones and attack phases, not arbitrary trigger spam.

## Tool-As-Weapon Boundary

Industrial tools can harm, but they remain tools:

- cutter: heat/cut channel, limited reach, power/noise cost;
- drill: vibration, puncture, debris, tool lock risk;
- scanner/sonar: sensory disruption, not free damage;
- welder: repair/heat, accidental burn, limited combat value.

If a tool becomes a normal weapon with no maintenance, risk, power, or noise consequence, reject it.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` may scale impact decals, hit reaction richness, blood/cloud density, haptics, camera response, audio layers, and secondary VFX. It must not change damage truth, hitbox layout, penetration tables, save identity, or authority route.

Compact keeps clear hit feedback, stable damage packets, simple reactions, and readable failure. High tiers add richer decals, animation layers, shader wounds, and contact presentation without changing damage results.

## Production Packet

Any combat, hitbox, penetration, damage contact, or threat-interaction change must declare:

- damage packet schema and owner route;
- hitbox/proxy map;
- resistance, penetration, armor, and vulnerability table;
- tool-as-weapon boundary if tools can harm;
- reaction presentation through animation/VFX/audio/haptics/UI;
- black-box fields for last contact and damage route;
- Compact and High readability proof;
- profiler/GC proof when runtime contact code changes.

Combat without visible contact geometry, legible damage cause, or owner-routed consequences is rejected.

## Proof Artifacts

Combat/damage work must provide:

- damage packet schema;
- hitbox/proxy map;
- resistance/penetration table;
- owner route for each damage channel;
- black-box fields for critical systems;
- compact feedback screenshot/capture if visual;
- profiler proof if runtime contact processing changed;
- save/load proof if damage persists.

## Rejection Gates

Reject:

- HP-only damage with no physical channel;
- visual meshes used as hit truth;
- VFX/UI inventing damage;
- creature attacks with no telegraph/contact phase;
- tools turned into generic weapons;
- penetration/armor claims without table/proof;
- combat work without black-box route for faults.

## Acceptance Sentence

Combat is accepted only when damage is routed through explicit channels, hit truth uses cheap proxies, consequences affect survival/world decisions, presentation stays honest, and runtime proof exists for any implemented contact path.
