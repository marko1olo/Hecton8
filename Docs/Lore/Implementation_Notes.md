# HECTON-8 Lore Implementation Notes

Status: working list.
Purpose: what we are putting into project lore, docs, future data, and player-facing content.

## Already Fixed In Docs

- `Lore_Bible.md`: 2190 present, non-Solar Aegir, no FTL, salvage-carrier arrival, damaged bathy-drop, Deep Reach proxy pressure, Atlas damaged repair logic, replay structure.
- `TASTE.md`: hard sci-fi / NASA-punk constraint for interstellar logistics and local orbital mechanics.
- `Narrative_Crystallization.md`: decision trail, source notes, real-system candidates, ship speed bands, human domains, Go2Starss propulsion source.

## We Are Implementing As Lore

- Aegir route as a no-FTL corporate claim system, probably anchored to Epsilon Eridani / Ran unless a later astronomy pass changes it.
- Aegir as one claim inside an already-expanded human sphere, not the first extrasolar destination.
- Human space by 2190 as layered domains: Sol Core, inner relay domains, independent or semi-independent habitats, corporate claim systems, dead/cold claims.
- Sparse mature frontier scale: enough civilization for law, debt, routes, claims, and salvage culture; not enough for quick rescue or clean oversight.
- HECTON-8 as a fictional ocean moon with pressure chemistry, tidal/geothermal support, layered geology, extractable industrial resources, and Deep Reach life support.
- Strategic resource layer: `Xenon-Omega` is locked as Deep Reach's corporate codename for a pressure-stable material/process family, not a literal simple isotope.
- Deep Reach as a legally present but physically distant power.
- Player as a Marauder salvage professional, economically trapped and technically competent.
- Carrier as local Aegir-system infrastructure, not player-owned freedom.
- Escape as a chain:
  - repair or replace high-gain uplink;
  - recover ephemeris / carrier timing;
  - rebuild pressure-rated ascent package;
  - secure energy, buoyancy, thermal mass, or fuel equivalent;
  - wait for orbital/radiation/weather window;
  - decide what payload, evidence, or coordinates leave with the player.
- Atlas-6 escalation by depth:
  - shallow: living ocean still beautiful;
  - mid-depth: drones, broken industrial modules, cable flora;
  - deep: fauna with industrial intrusion, stations as organs;
  - bottom: factory-ship temple, Deep Reach/Atlas/ocean fusion.

## We Are Implementing As Writing Infrastructure

- `Canon_Locks.md`: short truth source for stable decisions.
- `Open_Questions.md`: unresolved decisions that need user control.
- `Encyclopedia/`: player-facing or near-player-facing articles.
- `Encyclopedia/README.md`: rules for article tone and spoiler handling.
- Individual encyclopedia entries for:
  - Aegir system;
  - HECTON-8;
  - human domains;
  - relay spine;
  - corporate claims;
  - dead claims;
  - salvage economy;
  - Aegir route;
  - strategic pressure resources;
  - named human domains;
  - Aegir gas giant;
  - HECTON-8 geology and resources;
  - humanity overview;
  - technology overview;
  - Seed Program;
  - Deep Reach;
  - Marauders;
  - Atlas-6;
  - interstellar travel and ship classes.

## Later Data Targets

These are not implemented yet; they are future content/data candidates.

- PDA encyclopedia records.
- Terminal articles and sealed corporate memos.
- Scanner database entries.
- Contract dossier records.
- Marauder field notes.
- Old route telemetry / transit archives.
- Ending dossier summaries.

## Current Lore Growth Vector

Build outward in this order:

1. Human expansion model.
2. Route infrastructure and ship classes.
3. Deep Reach as interdomain corporate power.
4. Aegir as a late corporate claim.
5. HECTON-8 colony and catastrophe.
6. Atlas-6 and the HECTON-8 strategic-resource layer.
7. Marauder profession and player contract.
8. Replayable evidence ecology and endings.

## Locked Current Answers

- Named domain count: 6 major nodes, smaller systems implied.
- Player origin: Barnard Yards / connected frontier salvage belt, not Earth/Sol.
- Deep Reach age: older than Aegir; Aegir is one of its dirtiest later projects.
- Aegir public profile: known to specialists, insurers, Marauders, and corporations; ordinary citizens know it only as a distant old accident if at all.
- Xenon-Omega: Deep Reach codename for pressure-grown xenon-rich clathrate/defect lattices and associated processing, used for extreme computation, high-energy containment, and Atlas-compatible pressure infrastructure.

## Runtime Constraint

Lore delivery must stay data-driven and event-triggered.

- No hot scene search.
- No runtime procedural text generation for core truth.
- No allocation-heavy lore routing.
- Long articles belong in static data or compiled localization/content blobs.
- Variable replay should alter discovery order, context, and presentation, not the underlying canon truth.
