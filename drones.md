# HECTON-8 Drones, Automation, And Remote Systems Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Evidence class: STATIC_DOC
Scope: repair drones, mining bots, scanner probes, remote cameras, automation commands, docking/charging, drone AI boundaries, UI control, and drone proof gates.

## First-20 Route Hook

- First-20 moment: opening salvage route may show neutral or hostile old-system probes, remote scanner traces, or relay evidence as route pressure, not friendly automation.
- Route blocker removed: prevents early drone work from erasing survival pressure, bypassing tool owners, or implying helper-drone dependency before the first route proves basic swim, scan, repair, and return decisions.
- Proof class: STATIC_DOC hook only; acceptance still requires role contract, command owner route, active-count/cadence budget, compact readability proof, and profiler/GC proof for runtime automation changes.

## Prime Law

Drones are fragile industrial tools with limited authority, not autonomous magic helpers.

A drone must extend a player verb through risk, range, noise, power, maintenance, or perspective. HECTON-8 rejects free automation, omniscient repair bots, pet-like decorative drones with no system role, and AI helpers that erase survival pressure.

Current design lock: friendly helper drones are not the default production direction. The main underwater drones are neutral or hostile systems from source/ship/old infrastructure, with deeper encounters trending more hostile or dangerous. Player-helper drone roles remain future/candidate work and must not erase survival pressure.

## Truth Ownership

Drones own command queue, assigned task, local navigation intent, charge/health state, tool payload, link quality, and remote sensor snapshot. They do not own world truth, inventory truth, repair authority, construction truth, or player objectives.

Drone actions commit through the same tool/logistics/construction/damage owners that player tools use. A drone cannot repair, mine, scan, or transport by bypassing the normal owner route.

## Drone Roles

Allowed roles:

- repair assistant;
- mining/salvage probe;
- remote scanner;
- tether relay;
- camera/inspection probe;
- small cargo tug;
- emergency beacon carrier.

Each role must declare task, cost, failure, noise, power, range, docking, and recovery.

## Runtime Discipline

Required:

- bounded active drone count;
- command queue with fixed capacity;
- low-cadence planning;
- shared navigation/sensor snapshots;
- no per-drone scene searches;
- no independent physics-heavy behavior unless player-critical;
- black-box state for critical automation faults.

Use simple steering, flow fields, beacons, and authored docking paths before expensive pathfinding.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` may scale drone light detail, sensor visualization, animation richness, diagnostic overlays, secondary idle motion, and noncritical update cadence. It must not change task authority, item transfer rules, repair truth, save identity, or command ownership.

Compact keeps few active drones, simple steering, clear UI, and strong failure cues. High tiers add richer body animation, camera feeds, light sweeps, and diagnostic presentation.

## Production Packet

Any drone, probe, automation, or remote-system implementation must declare:

- role: repair, mining, scanner, relay, cargo, decoy, construction assist, or diagnostic;
- command queue schema and owner route;
- action authority: what the drone may read, reserve, mutate, repair, mine, scan, or carry;
- active count cap, update cadence, and sleep rule;
- docking, charging, tether, loss, jam, damage, and recall states;
- inventory/resource transfer route if any;
- AI/pathing ownership and obstacle policy;
- Compact and High readability proof for drone state;
- profiler/GC proof when runtime automation changes.

No drone may become an invisible worker that changes the world without a visible command, owner route, and failure state.

## Proof Artifacts

Drone work must provide:

- drone role and task contract;
- command queue schema;
- owner route for each action;
- active count/cadence budget;
- docking/charging/failure state;
- compact UI/world capture;
- save/load proof if persistent;
- profiler/GC proof for runtime drone logic.

## Rejection Gates

Reject:

- drone repairing or mining without owner commit route;
- omniscient scouting;
- unlimited active drone count;
- per-drone Update scene searches;
- automation that removes player survival decisions;
- decorative drone pets with no tool role;
- runtime claims without active count and profiler proof.

## Acceptance Sentence

Drones are accepted only when they extend player tools through bounded commands, preserve owner authority, cost power/risk, fail visibly, scale continuously, and prove runtime cost.
