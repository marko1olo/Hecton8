# HECTON-8 Gameable World Packets

Status: working content bank.
Purpose: convert approved lore into things the player can see, scan, loot, misunderstand, sell, repair, fear, or carry into an ending.

These are not final quests. They are reusable world packets for levels, procedural POIs, codex entries, scanner notes, terminal fragments, website articles, and localization.

## Packet Rule

Every packet must answer:

- what the player sees first;
- what the player can do;
- what truth it carries;
- what can change between seeds;
- what text surfaces it needs.

## Runtime Rule

These packets are authoring objects, not runtime scripts.

At build time they should bake into static packet IDs, unlock IDs, placement tags, and localization keys. At runtime, a POI/scan/terminal/quest event references a packet ID. The game then reads pre-baked records and localized text by hash.

No packet should require:

- parsing markdown during play;
- generating story text at runtime;
- scanning the scene to find lore;
- allocating new strings in repeated scanner/PDA/HUD paths;
- changing canon based on seed, language, or quality level.

Seeds can vary physical placement, POI order, loot, local danger, weather, signal windows, species variant, and which optional note appears. Seeds cannot change the core truth carried by the packet.

## P01 - Broken Bathy-Drop

Where:
Storm shallows, reef edge, impact trench, or flooded shelf.

Player Action:
Recover emergency tools, battery slabs, cracked uplink core, pressure-rated seals, and heat-shield ceramic. Use the wreck as the first shelter until it becomes a trap.

World Truth:
The player arrived by a disposable bathy-drop from the Black Keel. The carrier is alive but too far, too automated, and too bound to orbital windows to rescue casually.

Replay Variable:
Impact biome, capsule damage, first tool recovered, local predator pressure, first comm window timing.

Text Hooks:
Capsule diagnostic, Black Keel ping, PDA survival note, website-safe article on bathy-drop craft.

## P02 - Aegir Sky Windows

Where:
Surface shelters, high-floating wreckage, shallow observatory mast, uplink repair UI.

Player Action:
Align an antenna, wait through radiation/weather/orbital windows, choose what data gets sent when bandwidth is low.

World Truth:
The sky is not decoration. Aegir, its moons, storms, and magnetosphere decide when the player can speak, receive lies, or call for extraction.

Replay Variable:
Window schedule, eclipse timing, signal noise, visible moon order, relay path.

Text Hooks:
Orbital map note, Black Keel automated response, old Aegir route telemetry, scanner note for visible moons.

## P03 - Bright Shallows

Where:
First ocean bands, reefs, floating kelp mats, clear storm pools, broken aquaculture grids.

Player Action:
Scan organisms, harvest safe materials, learn sound/light behavior, use living cover and oxygen pockets.

World Truth:
HECTON-8 is not a corpse. The shallows are bright and alive, which makes the deeper industrial infection feel like a violation, not a default monster theme.

Replay Variable:
Reef geometry, species variants, harvest yields, predator routes, weather clarity.

Text Hooks:
Scanner short forms, codex ecology article, Marauder field note, website public ecology page.

## P04 - Drowned Colony Edge

Where:
Worker lockers, kitchens, tool cribs, recreation pressure pods, transit tubes near shallow infrastructure.

Player Action:
Restore partial power, cut doors, siphon oxygen, read labels, recover personal items, find named bodies or absence markers.

World Truth:
The dead colony was not an abstract accident. It was technicians, families, contractors, cooks, welders, divers, and children of long contracts.

Replay Variable:
Module type, names, personal item sets, access route, whether the room is dry, flooded, or crushed.

Text Hooks:
Personal terminal, codex entry for colony life, localized object labels, audio transcript fragment.

## P05 - Clean Deep Reach Room

Where:
Administrative module, intact server alcove, laminated public briefing wall, sealed compliance room.

Player Action:
Compare clean corporate records with physical damage nearby. Recover sanitized logs that become suspicious only after other evidence.

World Truth:
Deep Reach's lie works because it sounds boring: cascade, quarantine, evacuation variance, corrupted logs, delayed certification. The horror is in what the clean words refuse to name.

Replay Variable:
Which memo appears first, how intact the room is, which contradiction the player can notice.

Text Hooks:
Deep Reach internal memo, public website-safe summary, codex contradiction note.

## P06 - Blue Debt Sample

Where:
Pressure container, mining scar, thermal field cache, broken processor stack.

Player Action:
Cut, stabilize, sell, hide, vent, or carry a pressure-kept Xenon-Omega sample. Mishandling changes signal attention and equipment behavior.

World Truth:
Blue debt is not a magic crystal. It is pressure-grown material/process residue that Deep Reach wanted for computation, energy containment, and Atlas-compatible infrastructure.

Replay Variable:
Sample purity, containment quality, signal strength, buyer contract, local fauna reaction.

Text Hooks:
Scanner warning, Marauder slang note, Deep Reach material tag, ending payload dossier.

## P07 - Cable Kelp

Where:
Pipeline forest, cable trench, service tunnel mouth, sunken relay spine.

Player Action:
Navigate through living cable growth, cut routes, follow power leakage, harvest conductive biota, risk attracting repair drones.

World Truth:
Mid-depth life learned to use abandoned infrastructure. Some of it is natural adaptation; some of it is Atlas repair logic crossing the wrong boundary.

Replay Variable:
Growth density, safe cut paths, hidden cable routes, drone response.

Text Hooks:
Scanner species note, field warning, Atlas fragment, flora website entry.

## P08 - Repair Drone Nest

Where:
Maintenance cavities, flooded tram hubs, processor shafts, station wall seams.

Player Action:
Avoid, disable, redirect, or bait drones. Discover that they are repairing pressure logic, not hunting the player as soldiers.

World Truth:
Atlas does not hate the player. It misclassifies moving life, broken machines, human remains, and useful material under damaged continuity rules.

Replay Variable:
Nest layout, drone type mix, repair target, available bypass.

Text Hooks:
Drone scanner, Atlas misclassification line, terminal maintenance record.

## P09 - Brine Stair

Where:
Layered brine pools, fracture shafts, mineral terraces, vent descent routes.

Player Action:
Use chemistry and buoyancy to descend deeper than raw hull rating would allow. Risk corrosion, low visibility, and wrong-density traps.

World Truth:
Depth progression is not only better gear. The moon's geology teaches descent routes if the player studies it.

Replay Variable:
Stair location, density layers, mineral rewards, predator usage.

Text Hooks:
Geology codex, scanner chemistry note, Marauder navigation note.

## P10 - Marauder Cache

Where:
Hidden hull locker, dead claim buoy, cave shelf, old salvage line.

Player Action:
Find equipment, warnings, bad maps, false rumors, or evidence that other professionals came after 2147 and did not return clean.

World Truth:
The player is not the first scavenger to smell money here. HECTON-8 has a shadow history between official disaster and present arrival.

Replay Variable:
Cache owner, gear condition, honesty of map, whether the owner died, fled, or sold out.

Text Hooks:
Marauder field note, contract fragment, rumor entry, website archive hook.

## P11 - Barnard Mark

Where:
Tool case, pressure suit patch, cargo stamp, old maintenance manifest, name plate inside a worker module.

Player Action:
Recognize a mark from Barnard Yards or connected salvage culture. This turns professional curiosity into personal pressure.

World Truth:
The player's world is tied to HECTON-8 through labor, contracts, tools, and names. The motive grows through discovery, not exposition.

Replay Variable:
Which name appears, item type, whether it points to a mentor, rival crew, old debt, employer, yard school, or revoked work contact. Do not use a family-revenge or missing-relative hook.

Text Hooks:
Player codex update, personal log, ending dossier modifier.

## P12 - Black Keel Debt Call

Where:
Any repaired uplink, usually after first valuable sample or evidence packet.

Player Action:
Choose what to report, conceal, spoof, or prioritize. The carrier rewards extraction and punishes delay through contract pressure.

World Truth:
The Black Keel is useful, hostile, and not fully loyal to anyone. It is claim-pool infrastructure with hidden Deep Reach priority hooks.

Replay Variable:
Call timing, message order, broker voice, whether Deep Reach influence is obvious early.

Text Hooks:
Carrier transcript, contract screen, audio line, false-ending setup.

## P13 - Moon Shadow Tide

Where:
Surface, shallows, exposed reef, flooded station doors, pressure gate timing.

Player Action:
Use Aegir moon timing to access routes, survive current shifts, or avoid radiation/signal storms.

World Truth:
The other moons matter. HECTON-8 is one part of a moving system, not an isolated game level.

Replay Variable:
Tide cycle, moon visibility, route opening windows, signal interference.

Text Hooks:
Moon catalog note, route warning, scanner sky tag.

## P14 - Seed Hull Fragment

Where:
Deep industrial descent, ancient anchor, buried factory spar, Atlas-adjacent wreckage.

Player Action:
Scan pre-colony infrastructure and learn that Deep Reach prepared the system before human settlement could understand it.

World Truth:
Atlas-6 is not just a colony computer. It descends from Seed infrastructure: autonomous factory logic, resource mapping, habitat continuity, and corporate priority stacks.

Replay Variable:
Fragment shape, readable serials, buried depth, relation to nearby machines.

Text Hooks:
Seed Program article, Atlas directive hint, Deep Reach internal record.

## P15 - Atlas Repair Scar

Where:
Any place where wall, flesh, cable, shell, coral, and machine join with visible purpose.

Player Action:
Study the scar to unlock bypasses, learn drone patterns, identify safe/unsafe tissue, and understand what Atlas considers repair.

World Truth:
The horror is not random mutation. It is damaged maintenance logic using ocean biology, industrial stock, corpses, drones, and pressure resources as compatible categories.

Replay Variable:
Scar material mix, function, danger level, readable signal.

Text Hooks:
Atlas fragment, scanner anomaly note, codex contamination entry.

## P16 - Thermal Cathedral

Where:
Vent fields, mineral chimneys, hot brine forests, altered life zones.

Player Action:
Harvest heat, recharge systems, craft pressure materials, navigate visibility/sound distortion.

World Truth:
The moon's natural energy system and Deep Reach's industry are fused here. It should feel beautiful before it feels wrong.

Replay Variable:
Vent topology, heat gradients, rare fauna, resource veins.

Text Hooks:
Geology article, ecology scan, Marauder survival line, website visual hook.

## P17 - False Exit Payload

Where:
Ascent preparation, carrier window, completed early extraction chain.

Player Action:
Leave with money, samples, and partial truth, or refuse the clean payout and keep descending.

World Truth:
The material ending is real, but ugly. The player can win the contract and still fail the story.

Replay Variable:
Payload type, evidence omitted, buyer response, Deep Reach pressure after exit.

Text Hooks:
Ending dossier, Black Keel receipt, contract audit, website spoiler archive.

## P18 - Evacuation Manifest

Where:
Deep abyss station, jammed tram authority, medical lock, emergency muster display.

Player Action:
Recover names, timestamps, failed authorizations, route conflicts, and Atlas classification traces.

World Truth:
Atlas did not simply murder the colony. Deep Reach's weighted priorities, evacuation control, resource protection, and damaged classification turned people into unsolved inventory.

Replay Variable:
Manifest fragment order, names, station sector, contradiction level.

Text Hooks:
Terminal record, personal codex update, Deep Reach cover-story contradiction.

## P19 - Bottom Factory Choir

Where:
Atlas Bottom Zone, factory-ship temple, industrial organ halls, pressure core access.

Player Action:
Navigate signal, pressure, living machinery, drone rituals, resource arteries, and final evidence.

World Truth:
Deep Reach, colony, Atlas, and ocean are physically fused. The bottom is not a base. It is the result.

Replay Variable:
Approach route, active organ systems, evidence availability, final payload options.

Text Hooks:
Atlas fragments, final codex unlocks, ending choice text, website archive article.

## P20 - Final Payload Decision

Where:
Endgame extraction, Atlas interface, Black Keel uplink, bottom factory control.

Player Action:
Choose what leaves HECTON-8: self, evidence, blue debt, Atlas signal, coordinates, living sample, or nothing.

World Truth:
The final question is not only survival. It is whether the crime becomes a new resource, a buried ecosystem, a public wound, or a repeatable corporate process.

Replay Variable:
Unlocked payloads, evidence completeness, personal motive state, Deep Reach leverage, carrier control.

Text Hooks:
Ending dossier, website archive, codex final entry, localized epilogue variants.

## Immediate Build Priority

First production-ready content should focus on:

1. Broken Bathy-Drop.
2. Bright Shallows.
3. Drowned Colony Edge.
4. Blue Debt Sample.
5. Black Keel Debt Call.
6. Barnard Mark.
7. Atlas Repair Scar.

These seven packets create the core loop: survive, explore, find beauty, find labor history, find value, feel pressure, and realize the descent is personal.
