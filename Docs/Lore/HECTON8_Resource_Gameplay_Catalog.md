# HECTON-8 Resource Gameplay Catalog

Status: working resource atlas.
Purpose: define what the player extracts, scans, repairs with, fears, sells, and carries into endings.

## Resource Rule

Resources are story pressure, not just crafting icons.

Every resource should answer:

- why it exists on HECTON-8;
- what the player does with it;
- what hazard it creates;
- what faction or system cares;
- what ending or evidence value it can carry.

## Natural Resources

Reef Fiber:
Source: shallow mats and tough plant analogues.
Use: early repairs, filters, cordage, insulation.
Hazard: fouling, rot, fauna attraction.
Story Use: shows shallow ecology is useful, not just scenery.

Thermal Mass Stone:
Source: vent fields and mineral terraces.
Use: heat storage, power buffers, shelter warmth, crafting.
Hazard: thermal shock, burns, acid deposits.
Story Use: makes geology part of survival.

Brine Salts:
Source: brine stairs and density layers.
Use: chemical processing, ballast tuning, preservation, batteries.
Hazard: corrosion, wrong-density traps.
Story Use: turns descent into reading the ocean.

Iron Coral:
Source: mid/deep mineral-biological growths.
Use: reinforcement, tool heads, pressure ribs.
Hazard: brittle under wrong temperature, can carry contamination.
Story Use: life and geology already overlap before Atlas corruption.

## Industrial Salvage

Heat-Shield Ceramic:
Source: bathy-drop crash trail, old capsules.
Use: insulation, pressure tool upgrades, thermal protection.
Hazard: limited, sharp debris, crash-site predator routes.
Story Use: arrival remains useful and visible.

Relay Cores:
Source: service buoys, signal masts, route hardware.
Use: uplink repair, mapping, signal boosts.
Hazard: old charge, salt damage, bad data.
Story Use: makes communication a salvage chain.

Pressure Valves:
Source: colony modules, industrial descent, pump spines.
Use: habitat repair, suit upgrades, brine route control.
Hazard: explosive decompression, wrong fitting standards.
Story Use: worker infrastructure becomes player survival.

Drone Parts:
Source: repair drones and nests.
Use: automation upgrades, scanner tuning, bait, bypass tools.
Hazard: Atlas attention, contaminated components.
Story Use: drones are systems, not only enemies.

## Biological Resources

Oxygen Biota:
Source: shallows, aquaculture remains, filter mats.
Use: scrubber supplements, emergency air, lure/bait.
Hazard: spoilage, toxins, predator interest.
Story Use: living ocean helps the player before it scares them.

Conductive Kelp:
Source: cable forest.
Use: low-grade wiring, sensor mesh, current lures.
Hazard: shock, drone response, signal ghosts.
Story Use: blurs adaptation and repair logic.

Lantern Enzymes:
Source: Lantern Grass, Lantern Sifters, thermal ecology.
Use: low-light markers, chemical analysis, stealth routes.
Hazard: unstable glow, attracts predators.
Story Use: beauty has a survival function.

Filter Shell:
Source: Brine Siphoners, deep filter organisms.
Use: water processing, toxin screens, pressure membranes.
Hazard: parasite risk, contamination.
Story Use: the ocean can be technology if understood.

## Strategic / Deep Resources

Blue Debt:
Source: pressure caskets, process stacks, thermal-industrial scars.
Use: money, upgrades, evidence, bait, ending payload.
Hazard: pressure instability, signal attention, contract pressure.
Story Use: main resource temptation tied to Deep Reach and Atlas.

Pressure Glass:
Source: processed Xenon-Omega adjacent structures.
Use: sensor lenses, containment windows, deep lab optics.
Hazard: shatters violently outside correct pressure/temperature.
Story Use: makes "resource" feel engineered and dangerous.

Atlas-Compatible Lattice:
Source: deep factory/thermal zones.
Use: final interface, high-end repair, evidence payload.
Hazard: Atlas classification, drone retrieval, ending contamination.
Story Use: connects resource economy to final moral question.

Continuity Substrate:
Source: bottom-zone infrastructure and protected process routes.
Use: endgame payload, proof, leverage, catastrophic export risk.
Hazard: if exported badly, Deep Reach can repeat the crime elsewhere.
Story Use: forces final decision beyond loot.

## Resource Pressure By Depth

Shallows:
Survival resources. Mostly safe, useful, beautiful.

Drowned Shelf:
Human salvage. Tools, air, seals, records.

Service Canyons:
Route salvage. Cables, relays, caches.

Cable Forest:
Hybrid resources. Conductive biota, drone-adjacent materials.

Industrial Descent:
Blue debt and process hardware. Strong reward, strong pressure.

Brine Stairs:
Chemistry and preservation.

Thermal Cathedral:
Rare minerals, heat, pressure lattices.

Deep Abyss:
Evidence, high-pressure salvage, dangerous samples.

Atlas Bottom:
Final payload materials and moral hazards.

## Ending Relevance

Material Ending:
Player exports blue debt or high-value salvage without full truth.

Evidence Ending:
Player exports records, names, Atlas directives, and Deep Reach process proof.

Containment Ending:
Player prevents key substrate or Atlas signal from leaving.

Deep Reach Capture Ending:
Player exports the wrong payload through a compromised carrier path.

Atlas Continuity Ending:
Player allows some form of Atlas/ocean continuity to persist or communicate.

## Runtime Notes

Resources should be finite authored classes with seeded placement and state flags. Do not simulate chemistry continuously. Use containment state changes, scanner events, and route flags to create consequences.
