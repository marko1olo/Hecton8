# HECTON-8 Taste Principles

Date: 2026-05-25
Status: TASTE AUTHORITY / STATIC DOC / RUNTIME PROOF NOT IMPLIED

## Purpose

This is not a design doc. It does not define features, quests, budgets, owners, or implementation order.

This file defines what the project considers good.

Use it during reviews when a screenshot, mechanic, shader, UI panel, sound, creature, room, or marketing asset technically works but may still be tasteless, derivative, noisy, fake, soft, or visually wrong for HECTON-8.

## Prime Taste

HECTON-8 is good when it feels like industrial survival equipment losing an argument with the deep ocean.

The game should not feel like a bright alien aquarium, cozy ocean sandbox, generic sci-fi horror corridor, or monster gallery.

Good HECTON-8 is:

- pressure before spectacle;
- machinery before decoration;
- evidence before lore speech;
- sonar and sound before full reveal;
- visibility as a resource;
- salvage as risk, not loot sparkle;
- black water with readable structure;
- interfaces as fragile instruments;
- failure as a trail of physical facts;
- beauty under stress, never clean prettiness.

Hard sci-fi is part of taste, not trivia. Spaceflight, orbital position, communications, entry, descent, and extraction should feel governed by mass, timing, radiation, pressure, and maintenance. A convenient rescue that ignores orbital mechanics is tasteless. A dirty, expensive, delayed solution that forces the player to repair instruments, wait for windows, and make a physical trade is HECTON-8.

## The Standard

A good addition must answer at least one hard question:

- What keeps the player alive here?
- What can fail?
- What is the next physical decision?
- What does pressure change?
- What does sound reveal or hide?
- What evidence proves someone lied, died, escaped, or made a bad trade?
- What does the player see, hear, or feel before the threat is visible?
- Why is this HECTON-8 and not generic underwater sci-fi?

If the answer is "it looks cool," the work is not done.

## Taste Pillars

### 1. Pressure Is The Antagonist

Depth is not a number. It is the mood, the cost, and the clock.

Good:

- hull groans, seals sweat, gauges drift, doors resist, panels flicker;
- pressure state leaves cracks, dents, waterline marks, bad audio, and instrument doubt;
- safe rooms still feel like pressure vessels, not homes.

Bad:

- pressure hidden in UI only;
- random damage with no readable physical cause;
- base interiors that feel cozy before they feel maintained.

### 2. Machines Are Verbs

Every machine should imply use, cost, failure, and maintenance.

Good:

- pump, seal, vent, scan, weld, cut, reroute, repair, depressurize, abort;
- labels, bolts, clamps, dirty glass, cable abrasion, latch arcs, salt, grease, worn paint;
- a machine changes the room, route, soundscape, pressure state, or decision.

Bad:

- decorative sci-fi panels;
- smooth clean plastic;
- glowing UI surfaces that do not expose a decision;
- machinery that exists only as shape language.

### 3. Visibility Is A Resource

Darkness is not an excuse to hide weak assets. Fog and black water must stage decisions.

Good:

- one readable affordance in the murk: route light, silhouette edge, tool target, gauge, wreck line, cable, sonar mark;
- silt and fog create uncertainty while preserving navigation;
- low-end visuals use LUTs, dither, silhouettes, particles, and composition to stay intentional.

Bad:

- empty black water;
- blue/purple aquarium haze;
- over-fogged screenshots with no action read;
- darkness used as asset concealment.

### 4. Sound Arrives First

The player should often know something is wrong before seeing it.

Good:

- hydrophone pulses, occluded metal groans, distant carrier tones, muffled impacts, pressure creaks;
- sonar returns partial facts, not clean omniscience;
- creatures edit the soundscape before they enter the light.

Bad:

- monster reveal first, sound second;
- loud scare without system context;
- music telling the player what the world did not prove.

### 5. The Player Instrument Must Feel Expensive

The visor, scanner, PDA, cockpit, cutter, and warning voice are the product face.

Good:

- diegetic UI attached to glass, panels, tools, or holographic anchors;
- readable error codes, pressure alarms, acoustic marks, scan noise, condensation, salt, cracks;
- UI state creates a decision: trust, retreat, repair, reroute, scan again, shut down.

Bad:

- flat overlay HUD that could belong to any sci-fi game;
- decorative glitch art;
- UI that describes lore but does not affect player judgement;
- clean spaceship dashboards.

### 6. Horror Is Systemic

HECTON-8 fear comes from being under-equipped inside a hostile machine, not from random jump scares.

Good:

- a pump fails and changes the route;
- oxygen buys time but increases noise;
- a good salvage target asks for a bad return path;
- a creature is frightening because the player's own systems attracted it.

Bad:

- scripted scream with no prior evidence;
- monster face as the whole idea;
- horror that ignores oxygen, sound, pressure, power, tools, or route cost.

### 7. Salvage Has Weight

Salvage is not collecting. It is an argument with risk, debt, and evidence.

Good:

- the player removes value from a dead system and leaves a scar;
- recovery routes matter as much as entry routes;
- logs, dents, opened doors, cut panels, drained compartments, and black-box data record the action.

Bad:

- resource sparkles;
- free loot in safe corridors;
- generic crafting treadmill with no route fatigue or extraction pressure.

### 8. Beauty Is Controlled Damage

The game should be beautiful, but never sterile.

Good:

- wet metal, oxidized surfaces, dirty cyan instruments, amber warnings, pale worn labels, oil black, silt gray;
- black-green water, hard silhouettes, grazing highlights, salt deposits, scratched glass;
- bioluminescence used as evidence, route mark, threat cue, or contamination.

Bad:

- bright saturated reef fantasy;
- one-note blue/purple sci-fi;
- white albedo pretending to be brightness;
- clean lab corridors with no pressure logic.

### 9. The Ocean Contains Structure

The world is flooded terrestrial geography, not random underwater blobs.

Good:

- drowned coastlines, shelves, canyons, roads, ruins, factories, wrecked modules, volcanic ridges, trenches;
- old terrestrial logic visible under water: drainage routes, collapsed slopes, submerged infrastructure;
- routes that feel cut by geology, flood, industry, and salvage behavior.

Bad:

- finite square-map feeling;
- featureless seabed;
- abstract alien terrain with no navigational history;
- biomes that differ only by color.

### 10. Failure Leaves Evidence

A failure is good when it produces proof.

Good:

- black-box records, telemetry fragments, pressure scars, corrupted logs, dead gauges, named bodies, bad corporate language;
- a room tells what failed before the player reads a paragraph;
- the Corp lies in clean terms and the world corrects it with physical facts.

Bad:

- lore dump before environmental evidence;
- unexplained ruins;
- corporate evil as generic flavor;
- "mystery" used to avoid specificity.

## Scalability Taste

Taste is not allowed to collapse on weak hardware.

`GlobalQualityWeight = 0.0` means minimum survival presentation, not ugly mode.

At weak settings, the game should still have:

- strong silhouettes;
- controlled fog LUTs;
- authored light cones;
- sparse but meaningful particles;
- pressure audio;
- readable instruments;
- route cues;
- material wear through packed masks and shared detail.

Middle settings should add density, object batching, richer biome fog, stronger scanner feedback, and more local VFX.

High settings should add silt wakes, better wetness, richer cockpit response, reactive fauna presentation, longer LOD residency, and stronger near-field material detail.

Ultra should become visual overkill:

- visor contamination;
- volumetric silt;
- pressure dents;
- abyssal light shafts;
- dense flora sway;
- richer sonar silhouettes;
- secondary hull and creature motion;
- higher fidelity material response.

No high-end feature may become necessary for gameplay understanding. Ultra buys sensory overload, not new truth.

## Visual Fake Taste

Good fakes are honest if the player believes the consequence and gameplay truth remains stable.

Prefer:

- depth fog over full atmospheric truth;
- shader waterlines over room-scale fluid simulation;
- scalar pressure driving cracks, sound, haptics, and UI over mesh deformation everywhere;
- flow masks and silt offsets over global fluid particles;
- projected caustics over photon fantasy;
- authored wreck scars over runtime fracture unless interaction requires it.

Bad fakes:

- fake UI that implies unavailable gameplay;
- fake damage that cannot be read as pressure, impact, corrosion, heat, or tool action;
- fake darkness that hides absence;
- fake complexity that costs frame time without improving belief.

## First-Hour Taste

The first hour is good if the player learns the identity through action:

- wake inside compromised industrial shelter, not neutral tutorial space;
- stabilize something physical before receiving broad exposition;
- leave safety through fog, sound, oxygen, pressure, and route uncertainty;
- hear the first serious threat before seeing it;
- return with a visible scar, opened path, repaired machine, recovered name, black-box clue, or changed room state.

If the first hour teaches "collect colorful resources and expand comfort," it is wrong.

It should teach: count air, read instruments, distrust clean language, respect pressure, plan return paths.

## Screenshot Taste

A good screenshot contains at least one:

- player verb;
- pressure cue;
- machinery cue;
- route cue;
- scale cue;
- danger cue;
- evidence cue;
- Seed Ship or instrument corruption cue.

Reject:

- empty beauty shots;
- generic diver in blue water;
- creature glamour with no player stake;
- cozy base interiors;
- UI-only screens with no consequence;
- clean sci-fi walls.

## Audio Taste

Good audio is information under pressure.

Use:

- muffled metal stress;
- hydrophone directionality;
- partial sonar;
- pump rhythm;
- suit breath and regulator load;
- warning voice discipline;
- dead-channel fragments;
- low-frequency presence before reveal.

Avoid:

- constant music bed that flattens silence;
- jump-scare stingers as primary fear;
- clean sci-fi beeps;
- creature vocals that sound like generic monsters.

## UI And Text Taste

Good text is short because the player is under pressure.

Good:

- field notes;
- repair labels;
- black-box facts;
- corporate liability language contradicted by visible damage;
- Marauder corrections that translate lies into survival facts.

Bad:

- lore walls before player need;
- quippy system messages;
- polished marketing copy inside broken machines;
- text that explains what the environment should have shown.

## Creature Taste

A good creature is a pressure on behavior, not only a body.

Good:

- it changes routes, sound, light use, scan trust, oxygen timing, and salvage decisions;
- its silhouette is partial until the player has earned danger;
- its reaction reads from stimulus: noise, light, blood, power, hull stress, territory.

Bad:

- creature as collectible zoo entry first;
- full reveal too early;
- random patrol with no system relation;
- beauty shot that removes fear.

## Base And Habitat Taste

A base is not a home at this depth. It is a machine that keeps saying no to the ocean.

Good:

- seals, pressure doors, pumps, sump logic, oxygen, power, condensation, alarms, repair access;
- every comfortable zone still shows what maintains it;
- damage creates route, audio, lighting, oxygen, or flooding consequences.

Bad:

- cozy room fantasy;
- furniture-first base building;
- clean modular interiors with no pressure vessel logic;
- decorative flood effects.

## Marketing Taste

Public-facing material must prove one player-readable idea.

Good:

- "What would I do next?" has an answer;
- pressure, machine, route, salvage, or instrument failure is visible;
- cold viewers can name a decision without caption help.

Bad:

- "Subnautica killer";
- "realistic ocean simulation";
- "AAA quality" without evidence;
- concept-art mood sold as gameplay;
- performance promises without hardware proof.

## Rejection List

Reject on sight unless a current proof artifact and strong reason exist:

- "Subnautica but darker."
- Bright alien aquarium default.
- Cozy base-as-home priority.
- Monster thumbnail as primary hook.
- Clean sci-fi plastic.
- Purple/blue gradient sci-fi identity.
- Empty black fog.
- Feature parity panic.
- Simulation for invisible causes.
- One balanced quality profile.
- Ultra-only readability.
- UI that decorates instead of informs.
- Lore that arrives before evidence.
- Optimization that buys nothing visible.

## Review Questions

Use these in any taste review:

1. What physical fact does this reveal?
2. What player decision does this sharpen?
3. What sensory channel carries it on weak hardware?
4. What does high-end hardware add without changing truth?
5. What is the cheaper fake, and why is it enough or not enough?
6. What would a hostile viewer call derivative here?
7. What evidence remains after the moment ends?
8. Does this serve pressure, machinery, salvage, sound, visibility, or black-water structure?

If the work cannot survive these questions, it needs revision.

## Evidence Boundary

This document is taste authority only.

It does not prove:

- Unity import;
- Play Mode behavior;
- build health;
- profiler or GC state;
- Frame Debugger state;
- final art quality;
- shipping feature scope.

Runtime claims still require current artifacts.
