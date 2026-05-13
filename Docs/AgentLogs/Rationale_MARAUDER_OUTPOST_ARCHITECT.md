# Rationale - MARAUDER_OUTPOST_ARCHITECT

State: PENDING VERIFICATION

## Initial Scope Decision

Problem: The prompt demands deterministic abandoned outpost generation, but explicitly forbids hundreds of shell GameObjects.
Solution: Implement a domain-isolated outpost runtime with an interface boundary, native WFC grid, Burst structural solver, native matrix extraction, and GPU buffer rendering path. Only gameplay interactables become GameObjects.
Rejected Alternatives: A prefab room graph with instantiated wall/corridor prefabs is rejected because renderer count, Transform churn, and per-object setup would blow the MX350 budget. A pure scene-authored base is rejected because the prompt requires deterministic WFC from world seed and sector hash.
Scalability potential: Low uses 5x5x3 grid, minimal shell families, cheap supports, and reduced variation. Middle uses 10x10x5 with stable wear. High adds more visual variants and richer shell material response. Ultra spends saved CPU on overkill visual damage/wear variation without changing gameplay topology.
Hardware Impact: i3/MX350 target is one solver job plus one matrix extraction pass at sector hydration, then stable GPU buffer rendering. Estimated runtime hot-path cost after generation is below 0.05 ms CPU for draw submission and 0 B/frame managed allocation if render buffers remain resident.

## Mandate Binding

Problem: Cross-domain dependencies are unstable because 20+ agents may be editing adjacent systems.
Solution: Use contracts and GlobalRegistry/signal lanes for discovery, generation trigger, AUP shift, and optional height sampling. Compile-safe fallbacks will preserve deterministic output if a dependency is absent.
Rejected Alternatives: Direct concrete calls into MapMagic bridge, construction managers, or singleton base owners were rejected because that creates compile fragility and violates domain isolation.
Scalability potential: Same contract can serve cheap deterministic fallback height on low devices and richer height/terrain sampling on high devices.
Hardware Impact: Interface query is cold/lifecycle path. No per-frame registry polling is planned.
