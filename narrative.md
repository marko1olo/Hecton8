# HECTON-8 Narrative And Evidence Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Evidence class: STATIC_DOC
Scope: missions, quests, black-box records, logs, lore, environmental storytelling, corporate language, objectives, codex, text/audio evidence, and progression truth.

Route note: this file owns narrative truth, evidence order, mission state, and text placement. For the actual prose quality of in-world articles, encyclopedia pages, survivor diaries, scanner/codex entries, technical notes, and AppliedContent packets, use `writing.md` with this file.

## First-20 Route Hook

- First-20 moment: Marauder salvage motive, first evidence object, and a physical Deep Reach or Atlas contradiction discovered before exposition during the opening resource/tool/hazard route.
- Route blocker removed: prevents the first route from using generic tutorial objectives, family melodrama, or lore text unsupported by room, machine, route, body, or instrument evidence.
- Proof class: STATIC_DOC hook only; acceptance still requires mission operation statement, evidence-channel list, quest IDs, text sample, and screenshot/capture evidence where the beat is player-facing.

## 0. Prime Narrative Law

Evidence comes before exposition.

HECTON-8 narrative is strongest when the room, machine, route, body, log fragment, and instrument disagree with clean corporate language. Lore walls are rejected unless the player already needs the information.

Opening motive lock: the protagonist starts as a Marauder/salvage worker driven by contract, debt, payout, route opportunity, procedure, and survival economics. Do not replace the opening with missing family, revenge, romance, chosen-one destiny, secret passenger drama, or other personal melodrama unless the user explicitly overturns this lock. Personal weight may emerge through professional guilt, recognition of workers, procedures, evidence, and consequences, but the first motive is salvage work.

Deep Space & Orbital Isolation Law: Planet Hecton-8 is deep in uncharted space with NO live corporate personnel on site. All corporate communication/debt settlement is processed via an automated, drifting Orbital Hub / Drop-Pod Relay through laser pulses and single-use atmospheric ascent pods.

Endgame Narrative Axis (1000m+ "Ultra-Deep-8"): The catastrophe was caused by an ancient geothermal mechanism beneath the crust. Atlas-6 tried to contain it at human cost. Endgame branches: 1) Evacuation via Orbital Relay, 2) Override Atlas-6 into an autonomous citadel, 3) Core reactor meltdown.

## 1. Mission Shape

Missions must be physical operations:

- restore pressure;
- recover black-box data;
- open a route;
- repair pump/power/oxygen;
- identify failure;
- salvage a named component;
- verify a lie;
- extract and return.

Generic fetch quests are rejected unless the route, evidence, and risk make them HECTON-8.

## 1A. Screenwriter Scene Packet

Every authored scene, log discovery, diary placement, terminal read, black-box fragment, codex unlock, or public/wiki article handoff must start as a scene packet before prose is written.

Required fields:

- scene or content ID;
- location, depth band, route, or website/wiki surface;
- player state before the text appears;
- physical operation underway;
- evidence object carrying the text;
- visible contradiction or missing fact;
- source voice: survivor, Deep Reach, Marauder, Atlas, scanner, public archive, Black Keel, or neutral reference;
- player decision or understanding changed by this beat;
- spoiler level and unlock prerequisite;
- follow-up lead, route, risk, or dossier consequence.

If a beat has no physical operation and changes no player decision, it is not a scene. It is reference material and belongs in writer docs or website archive, not the critical path.

## 1B. Writer/Screenwriter Handoff

Scenario work owns sequence, evidence, and player need. Writing owns the artifact voice.

Correct handoff:

1. Scenario defines the place, action, evidence, knowledge boundary, and unlock.
2. Writer uses `writing.md` to produce the actual artifact text for the correct surface.
3. Localization uses `localization.md` and lore localization docs to preserve the same meaning across locales.
4. Runtime/UI receives stable IDs and bounded surfaces, never free-form markdown.

Do not ask a writer to "make lore for this area" without a scene packet. Do not ask a scenario agent to solve prose quality by dumping exposition into a log.

If a task asks for an encyclopedia entry, survivor diary, scanner fact, audio log, terminal note, website article, wiki page, or technical/mineral/engine article, the output must include the actual readable artifact text through `writing.md`. Scenario notes alone are not enough. For production or AppliedContent packets, preserve the 15-locale route through `localization.md` and the lore localization docs.

## 1C. Evidence-First Prose Firewall

A scenario packet may not hand the writer an abstract thesis and call it lore. Reject the handoff before prose if the beat is primarily:

- category collapse: infrastructure, ocean, biology, workers, debt, and Atlas described as "one body", "one skin", "one tissue", "one system", or equivalent without a specific room, machine, organism, document, and route consequence;
- organ metaphor in place of blocking: corridor as gut, wall as valve, cable as flower, base as organism, or similar language when the scene has no literal biological/mechanical operation to inspect;
- all-caps machine prophecy instead of a terminal record with source, timestamp, owner, field names, values, and failure state;
- corporate/legal abstraction that hides the actual event but has no recoverable human, room, shift, queue, door, pressure state, cargo return, or custody trail;
- "the player learns that..." or "this represents..." instead of a player action, contradiction, and consequence.

The recovery is not a prettier sentence. Replace the beat with a physical operation:

- where it happens;
- what equipment or organism is involved;
- which human procedure or automated route mislabels it;
- what evidence the player sees first;
- what decision, risk, route, debt, salvage value, or distrust changes afterward.

If that recovery cannot be written from canon, mark the beat `BLOCKED_SOURCE_BRIEF` with the missing fact. Do not ask the writer to make it sound deep.

## 2. Evidence Stack

A narrative beat should use at least two evidence channels:

- environmental damage;
- machine state;
- black-box audio;
- terminal log;
- object placement;
- named body or missing person trace;
- UI/instrument contradiction;
- route scar;
- corporate memo.

Text alone is weak. Visual evidence alone can be ambiguous. Together they build trust.

## 3. Writing Taste

Good text:

- short;
- specific;
- physical;
- operational;
- dry under pressure;
- contradicted by world evidence where useful.

Bad text:

- broad lore dump;
- jokes during pressure;
- generic corporate villainy;
- poetic vagueness hiding missing facts;
- exposition before player need.

## 3A. Dialogue, Logs, And Playback

Dialogue/log writing is accepted only when it behaves like a recorded event.

Use:

- interruption, clipping, alarm priority, breath, suit noise, carrier delay, or missing packet only when the surface supports it;
- one urgent operational fact per beat;
- a named object, route, person, pressure state, or task;
- partial knowledge and wrong assumptions appropriate to the speaker;
- silence, cut-off, or contradiction where the artifact should be damaged.

Reject:

- trailer one-liners;
- speeches about the theme;
- perfect final messages;
- omniscient survivors;
- villains explaining the plot;
- Atlas talking like a person who wants drama;
- exposition that could be read before or after the scene without changing meaning.

A good audio/log beat should make the player inspect a room, distrust a document, mark a route, repair a system, or remember a name. If it only "adds atmosphere", cut it or move it to optional archive content.

## 4. Quest State

Quest/progression state must be data-driven:

- numeric/baked IDs;
- no runtime string comparisons;
- event-driven transitions;
- fail-closed state reads;
- save-compatible flags;
- no hidden observers/delegates in hot path.

Narrative cannot own simulation truth. It reacts to world facts and records them.

## 5. Corporate Language

Corporate text should be clean, evasive, and operational:

- "material retention" for stripping dead modules;
- "calibration drift" for warning suppression;
- "asset reassignment" for missing workers;
- "pressure variance" for structural failure.

The world must correct the lie with physical facts.

## 6. Codex And Logs

Codex/log entries must be earned:

- after scan;
- after recovery;
- after repair;
- after black-box decode;
- after route discovery;
- after creature/system encounter.

Do not dump encyclopedia text before the player has a reason.

## 7. Narrative QA Gates

Reject if:

- mission has no physical operation;
- text arrives before evidence;
- objective could belong unchanged in any survival game;
- corporate evil is generic;
- quest state uses runtime strings;
- no failure/evidence state exists;
- logs are long without player need;
- narrative beat changes no decision.

## 8. Truth Ownership

Narrative owns interpretation and evidence routing, not simulation truth. World, physics, construction, AI, persistence, and gameplay systems own the facts. Narrative records those facts, frames them, reveals contradictions, and controls mission/evidence state through data-driven IDs.

Text, audio logs, and codex entries must never be the only source of critical truth if the player can plausibly inspect the physical consequence.

## 9. GlobalQualityWeight Scaling

Compact uses shorter text, stronger objective clarity, clearer evidence icons, and fewer optional fragments. Middle adds more environmental evidence and log layering. High adds richer voice/audio fragments and physical clue chains. Ultra adds dense archive material, secondary contradictions, and stronger black-box reconstruction without changing mission truth.

## 10. Proof Artifacts

Narrative work must provide:

- mission operation statement;
- evidence channel list;
- quest state IDs;
- save/persistence note;
- failure/evidence aftermath;
- text sample;
- localization expansion risk if UI text changed;
- screenshot or capture showing evidence before exposition where applicable.

## 11. Acceptance Sentence

Narrative is accepted only when evidence precedes exposition, mission state is data-owned, text is operational, and the player can connect physical consequences to the lie or decision.
