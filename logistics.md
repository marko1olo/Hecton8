# HECTON-8 Logistics, Power, Oxygen, And Network Flow Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: power grids, oxygen pressure, fluid networks, coolant, data/signal networks, pipes, cables, pumps, brownouts, rupture, graph solving, and logistics proof gates.

## Prime Law

Logistics are the nervous system of survival infrastructure. They are directed graph truth, not decorative pipes.

Every cable, pipe, pump, relay, oxygen line, coolant loop, and data trunk must either carry a system fact or remain visual dressing. HECTON-8 rejects per-segment MonoBehaviour simulation, physics-driven throughput, fake power lights with no state, invisible network failures, and base systems that feel cozy before they feel maintained.

## Truth Ownership

Logistics owns network topology, node potential/load, capacity, resistance, priority, brownout, rupture, isolation, and graph solve outputs. It does not own thermodynamics, survival physiology, construction recipes, UI, VFX, audio, or physics collision.

Other systems consume read-only summaries or typed events. They must not mutate graph buffers directly or poll component fields as network truth.

## Network Types

Supported network families:

- power DC;
- oxygen pressure;
- fluid/pressure;
- thermal coolant transport;
- data/signal;
- fuel/liquid where gameplay requires it.

Each family defines unit, capacity, resistance, producer, consumer, priority, failure state, and visual/audio/UI proxy.

## Graph Runtime Law

Required:

- CSR or equivalent cache-linear graph snapshot;
- mutation source separate from solve snapshot;
- dirty topology rebuild;
- fixed logistics cadence;
- Burst/native data solve when runtime;
- bounded node and edge capacity;
- no recursive traversal;
- no physics callbacks as truth;
- no per-segment Update/FixedUpdate.

Network visualization is a proxy. Throughput is graph math.

## Failure And Readability

Network failures must be player-readable:

- brownout;
- overloaded;
- isolated;
- ruptured;
- severed;
- starved;
- priority-shed;
- emergency bypass.

UI, audio, lights, VFX, and machinery response must agree with network truth. A dead pump that still sounds healthy is rejected.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` may scale visual flow indicators, diagnostics, UI detail, spark/leak effects, graph debug overlays, and noncritical presentation cadence. It must not change network topology truth, capacity math, producer/consumer identity, save layout, or brownout authority.

Compact keeps graph truth, simple proxies, strong alarms, low-frequency updates, and no decorative flow spam. High tiers add richer pipe glow, local VFX, and terminal diagnostics after graph cost is proven.

## Production Packet

Any logistics, power, oxygen, fluid, coolant, or data-network implementation must declare:

- network family and physical unit of measure;
- node schema, edge schema, and owner route;
- producers, consumers, buffers, valves, breakers, pumps, relays, or routers;
- update cadence and amortization window;
- overload, brownout, leak, blockage, and repair thresholds;
- failure presentation route through UI/audio/VFX/lights;
- save/load persistence if network state survives scene transitions;
- Compact and High readability proof;
- profiler/GC proof when runtime graph code changes.

A network packet without failure states is rejected. HECTON-8 base systems must fail physically and legibly, not silently become numbers.

## Proof Artifacts

Logistics work must provide:

- network family and unit table;
- node/edge schema;
- producer/consumer list;
- graph solve cadence;
- failure states and thresholds;
- save/load proof if persistent;
- compact UI/world failure capture;
- profiler/GC proof for runtime solve claims;
- black-box fields for critical life-support networks.

## Rejection Gates

Reject:

- per-pipe simulation loops;
- physics or trigger callbacks owning throughput;
- invisible power/oxygen failures;
- UI inventing network state;
- unbounded graph rebuild;
- scene search for network nodes in runtime hot paths;
- reports that claim network performance without profiler proof.

## Acceptance Sentence

Logistics are accepted only when network truth is graph-owned, failure is readable, presentation is decoupled, compact tier remains clear, persistence is stable where needed, and runtime graph claims have proof.
