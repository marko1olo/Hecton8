# Aegir Moon System

Article ID: location.aegir.moons
Status: locked working direction / names still renameable.
Spoiler Level: 1 Early Game
Primary Category: Aegir System
Source Voices: Public, Marauder Field Note, Writer Notes
First Unlock: orbit dossier / Aegir route map / carrier ephemeris
Website Use: spoiler-safe with limited detail
Related: Aegir_System, Aegir_Gas_Giant, HECTON_8, Aegir_Route

## Public

Aegir is not a lone planet with one important moon. It is a gas-giant system with multiple major moons, smaller moonlets, ring debris, relay points, radiation lanes, and transfer hazards.

HECTON-8 is one of those moons. It is not the innermost and not the farthest. It sits in the useful middle-outer problem zone: deep enough in Aegir's system for tides, eclipses, orbital geometry, and magnetospheric interference to matter, but not so hostile that the surface is permanent night.

## Working Moon Roster

This roster is a writer-facing scaffold, not final public naming.

- Aegir-I: inner scorched rock / radiation marker / no stable salvage.
- Aegir-II: ring-shepherd rubble body / navigation hazard.
- Aegir-III: fractured ice moon / old survey caches.
- Aegir-IV: relay and depot moonlet / route-control infrastructure.
- Aegir-V: mined sulfide/ice body / exhausted claim.
- Aegir-VI: brine-ice prospect / failed pressure labs.
- Aegir-VII: resonant rubble and impact scars / transfer-window hazard.
- Aegir-VIII: HECTON-8 / ocean moon / Atlas-6 and Deep Reach disaster site.
- Aegir-IX: cold volatile moon / cryochemical industry.
- Aegir-X through XII: outer captured irregulars / smuggler caches, dead claims, and sensor ghosts.

## Marauder Field Note

Never say "the Aegir moon" unless you want a navigator to stop trusting you.

HECTON-8 is just the one that still owes everybody money.

## Writer Notes

The moon system gives replay and hard-sci-fi pressure without requiring full simulation:

- carrier windows depend on Aegir orbit and moon geometry;
- other moons justify relays, occlusion, packet delays, false claims, route hazards, and old salvage rumors;
- HECTON-8 can have strong tides and eclipses without being alone in the sky;
- partial endings can send payloads or evidence through non-HECTON route infrastructure.

Implementation bias:

- Use authored ephemeris/window tables and existing celestial/tide DTOs.
- Existing celestial/tide code has orbital parameter slots and moon direction support. Treat that as presentation/data support, not a mandate for N-body simulation.
- Runtime truth should stay deterministic and cheap. Moon count can be lore-rich while active visuals use a small number of staged bodies.
