# HECTON-8 Construction And Economy Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Evidence class: STATIC_DOC
Scope: base construction, modules, logistics, power/oxygen/fluid networks, inventory, resources, crafting, salvage economy, storage, and infrastructure progression.

## 0. Prime Construction Law

Construction is survival infrastructure first, not only home decoration.

Every built object must answer:

- what survival problem it solves;
- what network it connects to;
- what it costs to build and maintain;
- what can fail;
- how it changes route, oxygen, power, pressure, storage, repair, scan, or docking.

If a buildable is only cosmetic comfort, it is secondary dressing and cannot carry progression weight.

User vision lock: safe rooms and player bases may be cozy, beautiful, decorated, and emotionally worth protecting once their pressure/oxygen/power/logistics basis is credible. Subnautica-like base decoration is allowed. Industrial survival function remains the foundation; comfort is not forbidden.

## 1. Resource Taste

Resources must have physical identity:

- salvage plate;
- seal kit;
- oxygen cartridge;
- pump casing;
- cable spool;
- relay board;
- filter media;
- ceramic insulator;
- pressure glass;
- biological sample.

Generic colored rocks and abstract currency are rejected. The item should imply use, weight, condition, and origin.

## 2. Crafting Law

Crafting is not magic assembly:

- recipes must fit material truth;
- tool or station requirements matter;
- crafting time/cost should create risk or planning;
- output should change player decision;
- failures or shortages should be visible.

Do not make generic tech trees that ignore pressure, oxygen, power, seals, pumps, and logistics.

## 3. Logistics Networks

Networks are directed systems:

- power;
- oxygen;
- fluid/pressure;
- data/signal;
- storage/material.

They must have readable state: overloaded, isolated, ruptured, brownout, blocked, starved, priority-shed. UI, audio, lights, and world state must reflect network truth.

## 4. Base Modules

Modules must look and behave like pressure equipment:

- seal logic;
- pump or air handling;
- power path;
- maintenance access;
- structural support;
- docking or route logic;
- damage state.

Clean modular rooms with no life-support logic are rejected.

Comfort modules and decoration are allowed after survival infrastructure is real. They should still show industrial material logic: mounts, seals, wiring, condensation, pressure-rated frames, maintenance access, or plausible lightweight personal objects.

## 5. Inventory And Storage

Inventory is logistics, not infinite pockets:

- capacity/mass/volume constraints;
- item condition;
- stack rules;
- container identity;
- route/extraction cost;
- quick-use clarity.

World items are data records plus dumb proxies. No smart object soup.

## 6. Economy Progression

Economy should create hard choices:

- spend seals on repair or expansion;
- salvage valuable part or keep route safe;
- power scanner or oxygen systems;
- build comfort later, survival now;
- strip a dead module or preserve evidence.

If the optimal answer is always "collect everything", economy design is weak.

## 7. Construction QA Gates

Reject if:

- buildable has no survival function;
- resource is abstract filler;
- module ignores pressure/life-support;
- network state is invisible;
- crafting output changes no decision;
- inventory is unlimited convenience;
- runtime path uses heap objects or string lookups;
- base feels cozy before it feels maintained.

## 8. Truth Ownership

Construction truth is owned by data, logistics, persistence, and world systems:

- `data.md` owns item/resource DTO shape and network payload layout.
- `construction.md` owns buildable function, resource taste, recipe meaning, and infrastructure role.
- `persistence.md` owns saved build state, damage, inventory, and world scars.
- `streaming.md` owns residency of placed assets and HLOD/proxy behavior.
- `physics.md` owns pressure/collision/flooding consequences.

Build UI and VFX may present state but must not invent power, oxygen, storage, pressure, or resource truth.

## 8A. Presentation-Only Boundary

Build previews, ghost meshes, holograms, comfort props, status screens, sparks, condensation, alarms, wetness, damage decals, and construction VFX are presentation-only. They may show authoritative construction/logistics/physics snapshots; they must not own resource counts, recipe completion, oxygen, pressure, power, storage, module damage, route unlocks, or save state.

Placement helpers may reject impossible placements only by reading the construction owner, physics proxy state, and logistics rules. They must not create alternate placement truth inside UI, VFX, or editor-preview logic.

## 9. GlobalQualityWeight Scaling

Low/Middle/High/Ultra are continuous planning labels on the same `GlobalQualityWeight` curve, not binary switches:

- Low: network truth, readable module state, shared materials, clear alarms, simple proxy meshes, no loss of survival function.
- Middle: richer module dressing, local VFX, clearer preview affordances, and stronger storage/power/oxygen readability.
- High: better damage, wetness, material response, maintenance screens, and room comfort dressing.
- Ultra: dense maintenance detail, secondary animations, cockpit/base screen richness, and comfort polish without changing resource or network truth.

## 10. Proof Artifacts

Construction work must prove:

- buildable purpose;
- recipe and resource identity;
- network connections;
- failure state;
- save/persistence state;
- collision/proxy state;
- low-tier screenshot;
- runtime allocation and query route if implemented.

## 10A. First-20 Route Hook

The first-20 route needs construction language only where it removes a route blocker: emergency repair, oxygen/power extension, storage clarity, salvage conversion, or first safe-room/base affordance. Decoration may make shelter desirable, but it must not replace the early survival function.

## 11. Acceptance Sentence

Construction is accepted only when it behaves like survival infrastructure, exposes network truth, saves physical consequences, avoids cozy filler as progression, and remains readable on compact hardware.
