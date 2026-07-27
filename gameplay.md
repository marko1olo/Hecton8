# HECTON-8 Gameplay Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Evidence class: STATIC_DOC
Scope: player verbs, survival loop, salvage, pressure, oxygen, tools, construction, progression, failure, mission flow, and systemic gameplay taste.

## First-20 Route Hook

- First-20 moment: damaged safe anchor, first semi-open shallow exit, first oxygen/pressure retreat decision, first tool/resource repair chain, first hazard response, and first return-path consequence.
- Route blocker removed: prevents the opening from collapsing into generic pickup/crafting or a Copper Wire-only proof route with no pressure, machinery, beauty, failure, or physical decision.
- Proof class: STATIC_DOC until play repro, save/load proof, UI/readability capture, failure evidence, and route screenshots/clips exist.

## 0. Prime Gameplay Law

HECTON-8 gameplay is not collection. It is a chain of physical decisions under pressure.

Every player-facing mechanic must sharpen at least one decision:

- go deeper or retreat;
- spend oxygen or save it;
- make noise or stay hidden;
- cut power or keep systems alive;
- salvage value or keep route safety;
- trust an instrument or verify it;
- repair now or leave a scar;
- open a pressure boundary or find another route.

If a mechanic only fills time, gives generic loot, or imitates another survival game without pressure logic, reject it.

## 1. Core Loop

The main loop is:

1. Read instruments.
2. Plan route and return path.
3. Enter hostile space.
4. Spend oxygen, power, noise, hull, tool heat, time, and visibility.
5. Recover salvage, evidence, route access, repair material, or black-box truth.
6. Return changed.
7. Repair, reroute, build, decode, or commit deeper.

The player must bring back a physical consequence: opened door, drained room, repaired pump, recovered name, broken tool, changed route, new pressure scar, or black-box clue. Pure resource pickup is weak.

The first product-facing loop is semi-open and visually compelling. It begins from a damaged safe anchor, opens into beautiful shallow water with unease nearby, lets the player look around when oxygen and local safety allow it, then proves oxygen/depth pressure, resource/tool use, craft/repair/build, hazard response, and save/load.

Copper Wire may be one resource chain inside this loop, but Copper Wire alone is not the first-20-minutes identity.

## 2. Survival Resources

Survival resources are decision pressure, not UI bars:

- Oxygen: time, breathing stress, route cost, panic cost, refill planning.
- Hull/pressure: safe envelope, damage memory, lockout, leaks, sound cues.
- Power: light, scanner, pumps, doors, comms, construction, tool heat.
- Noise: attraction, sonar trade, machinery risk, creature reaction.
- Visibility: fog, silt, darkness, light discipline, route readability.
- Inventory mass/volume: extraction fatigue, tool choice, salvage trade.

Every resource must have a visible or audible world consequence. Hidden spreadsheet drain is rejected.

Oxygen uses the Subnautica-like readability model: a clear reserve, route planning, upgrades, and physical extension routes such as better tanks, hoses/tethers from a ship or base, or other credible expedition support. The player can die quickly if they ignore oxygen.

Interface Split Law: Visor HUD glass displays ONLY minimalist critical survival metrics (Oxygen reserve, Depth/Pressure, Hazard Warnings). All 3D Sonar, maps, inventory, salvage diagnostics, and technical data live on the Tactical Wrist PDA (КПКТ-планшет) and vehicle dashboards.

Hydroacoustic Stealth & Drone Counterplay: Atlas-6 drones react to acoustic noise and light. Counterplay is non-lethal and systemic: EMP directional pings, active sonar jamming, or baiting native abyssal bio-creatures into attacking noisy drones.

## 3. Verbs

Primary verbs:

- scan;
- listen;
- weld;
- cut;
- patch;
- pump;
- seal;
- vent;
- reroute;
- depressurize;
- anchor;
- recover;
- decode;
- build;
- retreat.

Every new item, UI screen, mission, or environment should support these verbs. Generic attack/loot/build loops are rejected unless they are translated into HECTON-8 physical operations.

## 4. Tools

Tools must have:

- readable physical form;
- input rhythm;
- heat, charge, pressure, signal, noise, or durability cost;
- failure mode;
- upgrade path that changes decision quality, not only numeric strength;
- UI/readout that belongs to the tool or suit.

A tool that only increases a number is weak. A better tool should open a new route, reduce risk, expose better evidence, or let the player choose a different trade.

## 5. Salvage

Salvage is risk and evidence:

- It should require entry route and return route planning.
- It should leave a scar on the world.
- It should have weight, ownership, condition, and use.
- It should sometimes be morally or operationally ugly: cannibalize a dead module, strip life support, recover a name instead of a part.
- It must not sparkle like generic loot.

Reward text without world change is rejected.

## 6. Construction And Base Systems

Construction is survival infrastructure first, but cozy safe rooms and beautiful player bases are allowed when pressure, oxygen, power, maintenance, and industrial material logic remain visible.

Hybrid Base Reclamation Law: Players can claim abandoned Deep Reach undersea research/drill stations by repairing water pumps and power reactors, then expand them using custom pressure-rated modular hulls (Hull Pressure Integrity) attached to or around the reclaimed structures.

Good construction:

- keeps pressure, oxygen, power, routes, storage, repair, scanning, or docking alive;
- shows seals, pumps, conduits, valves, supports, brackets, drains, and access panels;
- creates maintenance debt;
- has failure states;
- changes route and soundscape.

Bad construction:

- furniture-first comfort;
- clean room fantasy;
- free expansion without material/logistics cost;
- modules that do not explain how they survive depth.

## 7. Progression

Progression should unlock new decisions, not just higher stats:

- deeper pressure range;
- safer return paths;
- better signal interpretation;
- stronger tool precision;
- new repair methods;
- construction that changes route planning;
- creature counterplay through knowledge, not domination.

Reject feature parity grind, generic crafting ladders, and upgrades whose only player-facing result is bigger numbers.

## 8. Failure

Failure must leave evidence:

- black-box record;
- changed room state;
- pressure scar;
- lost salvage;
- damaged tool;
- corrupted archive;
- dead route;
- audible memory.

Death/retry without evidence is cheap. Failure should teach the physical system.

Ordinary death may respawn the player at a base or safe anchor and drop carried resources in the world. Core tools remain with the player unless a special authored event explicitly removes them. This supports punishment without destroying baseline agency.

## 9. Continuous Quality Scaling

`GlobalQualityWeight` changes presentation density, hint cadence, screen material, audio richness, VFX, and LOD residency. It must not change gameplay truth, route validity, save identity, resource math, or authority ownership.

Compact must remain readable and tense. Ultra buys sensory overload, not new facts.

## 10. Gameplay QA Gates

Reject if:

- no player decision is sharpened;
- no physical state changes;
- no return-path pressure exists;
- reward is generic loot;
- UI bar hides the world consequence;
- mechanic needs runtime allocation or polling against doctrine;
- failure leaves no evidence;
- high-end feature becomes necessary for understanding;
- the system could belong unchanged in any generic survival game.

## 11. Truth Ownership

Gameplay owns verbs, resource meaning, progression decisions, and failure consequences. It does not directly own mesh generation, UI rendering, audio playback, physics force application, persistence layout, or AI cognition. Those domains publish and consume through their own bibles.

Every gameplay mechanic must name the owner of its truth: oxygen, pressure, power, route, salvage, mission, tool, vehicle, construction, or creature pressure.

## 12. Proof Artifacts

Gameplay work must provide:

- player decision statement;
- physical state changed;
- resource cost or explicit reason no resource is involved;
- failure evidence;
- owner/authority route;
- save/persistence note if state survives;
- compact-tier readability proof;
- play repro or explicit `PENDING VERIFICATION`.

## 13. Acceptance Sentence

Gameplay is accepted only when it sharpens a physical decision, changes world or player state, leaves evidence, has explicit authority ownership, remains readable on compact hardware, and proves its runtime claims.
