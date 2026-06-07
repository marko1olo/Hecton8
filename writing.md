# HECTON-8 Writer-Screenwriter Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Evidence class: STATIC_DOC
Scope: in-world articles, encyclopedia pages, survivor diaries, terminal notes, black-box fragments, scanner/codex entries, technical explainers, mineral notes, ship/drive articles, public lore pages, and multilingual AppliedContent text packets.

## Prime Law

Write artifacts, not specifications.

Every text must feel like it was written by a person, institution, survivor, technician, insurer, Marauder, journalist, public archivist, or machine interface inside the world. It must not read like an AI-generated design note explaining what the packet is for.

The reader should believe the text existed before the player found it.

## Authority Route

Use this file with:

- `narrative.md` for missions, evidence order, quest state, logs, and black-box truth;
- `Docs/Lore/Lore_Bible.md` and `Docs/Lore/Canon_Locks.md` for canon;
- `Docs/Lore/Lore_Content_System.md`, `Docs/Lore/Lore_Localization_Model.md`, and `Docs/Lore/Lore_Multilingual_Content_Architecture.md` for Article ID, LocID, surface, source-voice, spoiler, and locale packet structure;
- `localization.md` for string IDs, fallback, RTL/CJK, subtitles, and runtime text behavior;
- `textes.md` only for real-world public marketing/community copy.

This file owns prose quality, genre voice, realism, anti-AI wording, and multilingual package shape. It does not own simulation truth, quest state, UI runtime, or public readiness claims.

## First-20 Route Hook

- First-20 moment: first scanner result, terminal note, black-box fragment, field note, or codex unlock that makes the opening route's resource, tool, hazard, or return-path decision more concrete.
- Route blocker removed: prevents first-route text from becoming generic lore, AI-sounding packet prose, or exposition detached from a physical evidence object.
- Proof class: STATIC_DOC hook only; acceptance still requires source brief, surface-specific artifact text, canon sources, locale status where production content is requested, and runtime/UI proof only when the text is implemented.

## Writer And Screenwriter Roles

The writer and the screenwriter may be the same person, but they are not the same mode of work.

Screenwriter mode owns the situation:

- what the player has just done;
- what the player can see, touch, hear, scan, or doubt;
- what evidence exists before text appears;
- what decision, route, danger, or emotional pressure the text changes;
- which speaker/source has access to this knowledge.

Writer mode owns the artifact:

- the exact voice of the person, institution, instrument, or archive;
- the visible prose;
- the omissions, bias, broken context, and physical marks;
- the English authority text;
- the 15-locale delivery shape.

Do not start in Writer mode. First define the scene, unlock, evidence object, and knowledge boundary. Then write the artifact. A beautiful paragraph attached to no object, no scene, and no player need is rejected.

If one agent does both roles, it must explicitly pass these gates:

1. Scene gate: where is the player and why does this text appear now?
2. Evidence gate: what physical or system artifact proves the text belongs here?
3. Knowledge gate: what can this source know, and what must remain unknown?
4. Voice gate: what exact human/institution/instrument wrote or recorded it?
5. Surface gate: scanner, terminal, codex, audio, website, wiki, note, dossier, or black box.
6. Localization gate: stable IDs, English authority text, and all 15 locale rows or explicit draft status.

## Required Writer Inputs

Before writing any article, diary, note, log, or encyclopedia entry, define:

- speaker or institution;
- audience;
- date or approximate era;
- surface: external site, in-game wiki, scanner, terminal, audio, black-box, survivor diary, corporate memo, field note;
- unlock context;
- what the writer knows;
- what the writer does not know;
- what the writer is hiding, if anything;
- physical object or evidence that carries the text;
- gameplay reason the player reads it now.

If those facts are missing, write a short source brief first. Do not invent omniscient prose.

## Source Brief Template

Every substantial content request should begin with a compact source brief. Keep it short, but do not skip fields.

```text
Packet ID:
Article ID:
Loc namespace:
Runtime layer:
Surface targets:
Spoiler level:
Canon sources:
Speaker/source:
Audience:
Date/era:
Location/depth/route:
Unlock context:
Evidence object:
What this source knows:
What this source does not know:
What this source hides or gets wrong:
Player use:
Forbidden facts:
Required proper nouns/units:
LocIDs:
Localization status:
```

No field may be filled with vibes. If the answer is unknown, write `UNKNOWN - DO NOT INVENT` and either stop or make discovery the task.

A source brief is an input or blocker, not the final artifact, unless the user explicitly asked only for a source brief.

## Production Writing Loop

Use this order for real work:

1. Read `PROJECT_BIBLES.md`, `narrative.md`, `localization.md`, this file, and exact canon sources in `Docs/Lore`.
2. Extract only the canon facts needed for the packet.
3. Choose the source brief and surface contract.
4. Draft the English authority text per surface.
5. Cut generic openings, explanations of theme, and authoring notes.
6. Add concrete object/route/tool/body/procedure detail only where the source could know it.
7. Build the 15-locale rows from the same meaning; mark non-native translations as draft.
8. Check length/shape risk for scanner, PDA, terminal, subtitles, and website.
9. Provide a proof packet: sources used, IDs, surfaces, locale roster, native-review status, forbidden facts avoided.

The loop must produce content, not a proposal about content. A task asking for a diary should end with a diary. A task asking for a technical article should end with a readable technical article, not notes about the future article.

If the same content problem repeats after one rewrite pass, stop polishing phrasing and change the source route: revise the scene/evidence/knowledge boundary, choose a different surface or speaker, or report the exact missing canon/source blocker.

Localization lock: AppliedContent and major in-world content should be prepared for all 15 supported locales immediately. Non-native or machine-assisted text must be labeled honestly through status/frontmatter/index fields, never as player-visible disclaimers and never as native-final without proof.

## Surface Truth Contract

One packet can export to many places, but every place must sound like its own artifact.

Use the same canon facts, then change the source and reader:

- Game scanner: short observed fact, confidence, hazard, action limit.
- PDA/codex: useful recovered knowledge after unlock, not omniscient encyclopedia.
- Terminal: in-world document with headers, routed action, stale authority, missing body cost.
- Audio log: spoken fragment under interruption, not a paragraph read aloud.
- Black box: telemetry, event marker, contradiction, damaged transcript.
- Survivor diary: next-hour need, object/person/route, wrong assumption, fatigue.
- Marauder note: practical correction, route pressure, contempt for bad data, no poetry.
- Deep Reach memo: clean liability language, defensible lie, no villain confession.
- Atlas fragment: classification/repair/routing language, not emotional villain dialogue.
- External site/wiki: public readable article with spoiler gate and no fake marketing claim.

Do not reuse the same paragraph across surfaces with only headings changed. That is spreadsheet prose.

## Anti-AI Prose Ban

Reject text that sounds like a packet summary, content brief, or AI article.

Hard-ban patterns unless quoted as bad corporate copy:

- "`X` defines/explains/shows why..."
- "This entry explores..."
- "serves as a reminder"
- "a testament to"
- "more than just"
- "at its core"
- "in a world where"
- "a delicate balance of"
- "a unique blend of"
- "both beautiful and terrifying"
- "the real horror is..."
- "not just X, but Y"
- "X turns Y into Z" when used as generic meta-description;
- field notes that tell the writer how to use the article;
- scanner text that summarizes authoring intent instead of sensor output;
- audio lines that sound like trailer taglines;
- encyclopedia pages that read like root design docs.

Strong HECTON-8 prose uses concrete nouns, dates, quantities, roles, failure states, custody marks, stains, gaps, and procedural pressure.

## Anti-Machine Edit

Run this pass before accepting any draft.

Delete or rewrite:

- generic opening sentence that explains what the article is about;
- summary phrases that could fit any sci-fi setting;
- analogies that are not something this exact speaker would use;
- metaphors that hide missing physical facts;
- emotional labels without behavior or evidence;
- "lore voice" that knows everything and touches nothing;
- repeated sentence rhythm across different sources;
- explanation of what the player should feel;
- fake moral clarity where the source should be partial, tired, evasive, or wrong.

Replace with:

- a job, object, place, route, number, custody mark, tool, failure mode, or physical trace;
- one concrete human mistake or omission;
- one fact the source cannot know;
- one detail the player can later see, scan, repair, steal, or contradict.

Read the draft aloud as the source. If a pump technician, exhausted survivor, scanner, corporate counsel, Marauder, public archivist, and Atlas fragment could all say the same line, the line is dead.

## Human Specificity

Every human-facing text needs at least two grounded details:

- a job title;
- a place name or route label;
- a tool, gauge, seal, pump, locker, plate, clamp, container, wound, sample, badge, or packet;
- a timestamp, shift, pressure rating, mass allowance, temperature, depth band, signal delay, or custody number;
- a sensory fact: condensation line, stale scrubber smell, salt bloom, cracked enamel, dirty visor, wet paper, warped hatch;
- a contradiction between official wording and physical evidence.

Do not explain the entire setting. Make one piece of the setting feel real.

## Genre Contracts

### External Encyclopedia Article

Voice: public, edited, readable, non-spoiler unless marked.

Use:

- short paragraphs;
- source uncertainty where public knowledge is incomplete;
- concrete route, legal, technical, or historical consequence;
- plain definitions before specialized terms.

Avoid:

- terminal all-caps;
- `Scanner/Terminal/Audio/Field Note` sections unless the article is explicitly a multi-surface packet manifest;
- authoring language;
- lore dump with no reader use.

### In-Game Wiki / Codex

Voice: recovered operational knowledge.

Use:

- what the player can do with the information;
- why it matters to route, salvage, survival, evidence, or risk;
- source marker: scan, recovered log, physical sample, black-box decode, Marauder annotation.

Avoid:

- encyclopedia before discovery;
- meta explanation of game design;
- full truth before the player has evidence.

### Scanner Result

Voice: instrument output.

Use:

- observed material;
- confidence;
- hazard;
- immediate action or limitation.

Good scanner text is not poetic. It is useful and incomplete.

### Terminal / Corporate Memo

Voice: procedural, evasive, legally defensible.

Use:

- document ID;
- department;
- requested action;
- liability-safe wording;
- missing human cost.

Corporate text must not confess like a villain. The room or later evidence proves the crime.

### Survivor Diary

Voice: partial, tired, practical, personal without melodrama.

Use:

- what the survivor is trying to do in the next hour;
- a named person, object, or route they care about;
- a wrong assumption they believe at the time;
- sensory detail the camera can later support.

Avoid:

- prophecy;
- complete lore understanding;
- perfect last words;
- speeches about themes.

### Engineering Note

Voice: technician, shipyard, field manual, or maintenance annotation.

Use:

- component names;
- tolerances or pressure classes when useful;
- failure mode;
- workaround;
- cost of the workaround.

Do not write fake technobabble. If the mechanism is not known, write the observable behavior and the maintenance decision.

### Mineral / Resource Note

Voice: field geology, lab intake, salvage valuation, or Marauder handling note.

Use:

- sample source;
- containment requirement;
- contamination risk;
- pressure/temperature history;
- value condition;
- reason it can kill or bankrupt someone.

Never make resources feel magical. Blue debt is frightening because it obeys pressure, custody, contamination, and Atlas classification.

### Technical Article: Ship, Drive, Relay, Engine

Voice: industrial history, engineering note, public explainer, or route manual.

Use:

- what problem the machine solves;
- what infrastructure it requires;
- what it cannot do;
- what it costs in time, mass, shielding, maintenance, or custody.

No miracle engines. No heroic prose. A heavy ship is logistics with heat, debt, and braking windows.

### Audio Log

Voice: spoken under constraint.

Use:

- interruption;
- breath, suit noise, alarm, or transmission artifact when needed;
- one urgent fact;
- human omission.

Audio is not a paragraph read aloud. It is a damaged moment.

### Black-Box Fragment

Voice: telemetry plus human contradiction.

Use:

- state values;
- event marker;
- short transcript or annotation;
- mismatch between system status and reality.

The machine records facts. The horror is in what the facts exclude.

## Content Deliverable Shape

For production content, output a packet that can move between game, site, wiki, notes, audio, and localization without being rewritten from zero.

Minimum packet:

```text
PACKET
Packet ID:
Article ID:
Loc namespace:
Runtime layer:
Canonical title:
Spoiler level:
Canon sources:
Source brief:

SURFACE TEXTS
External site / wiki:
In-game codex:
Scanner short:
Terminal / document:
Audio / transcript:
Field note / Marauder annotation:
Black-box or telemetry fragment:

LOCALIZATION
LocID:
Locale rows with status and text:
en_US [source_authority]:
ar_SA [draft_machine_or_llm]:
de_DE [draft_machine_or_llm]:
es_ES [draft_machine_or_llm]:
fr_FR [draft_machine_or_llm]:
he_IL [draft_machine_or_llm]:
id_ID [draft_machine_or_llm]:
ja_JP [draft_machine_or_llm]:
ko_KR [draft_machine_or_llm]:
nl_NL [draft_machine_or_llm]:
pl_PL [draft_machine_or_llm]:
pt_BR [draft_machine_or_llm]:
ru_RU [draft_machine_or_llm]:
uk_UA [draft_machine_or_llm]:
zh_CN [draft_machine_or_llm]:

QA
Forbidden facts avoided:
Length risks:
Native-review status:
Runtime/site/wiki placement notes:
```

Only include surfaces the task actually needs, but never collapse different surfaces into one generic text. If the task says "article", write the article. If the task says "survivor diary", write the diary. If it says "can go to game/site/wiki/notes", produce separated surface texts or explicitly label which surfaces are not appropriate.

Long articles should have section headings for website/wiki, but in-game codex should stay readable after unlock. Scanner and subtitle text must be separate short forms, not truncated article bodies.

## AppliedContent Packet Shape

Generated packets may contain multiple surfaces, but each surface must be written as its own real artifact.

Bad packet style:

```text
Title
X explains why HECTON-8 is important.

Scanner
One slogan.

Terminal
ALL CAPS SUMMARY.

Audio
Trailer line.

Field Note
Use for art constraints.
```

Required packet style:

- external article reads like an article;
- scanner reads like sensor output;
- terminal reads like an actual terminal or memo;
- audio reads like speech or carrier playback;
- field note reads like a Marauder/technician annotation, not writer instructions.

If writer instructions are needed, put them in source comments or authoring notes, not player-facing text.

## AppliedContent Production Handoff

For production AppliedContent, prose is not done until it is materialized into the content pipeline:

- packet/source JSON or production packet file exists with stable packet ID, Article ID, unlock ID, surface text, and locale status;
- all 15 locale rows contain actual draft text or `BLOCKED_TRANSLATION_DRAFT`;
- target surface files and indexes are updated for `in_game_wiki` and/or `external_site` when those surfaces are in scope;
- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv` and generated hash constants are refreshed when the packet targets runtime;
- route cards or binding maps exist when the packet is meant to unlock from a POI, scanner, terminal, quest, or scene object;
- exporter/importer/audit output is reported with its real proof class.

A loose markdown article, source brief, future integration note, or English-only packet is not production AppliedContent unless the task explicitly requested draft-only work.

## Multilingual Requirement

Unless the task explicitly requests English source only, AppliedContent writing must plan all 15 project locales:

- `en_US`
- `ar_SA`
- `de_DE`
- `es_ES`
- `fr_FR`
- `he_IL`
- `id_ID`
- `ja_JP`
- `ko_KR`
- `nl_NL`
- `pl_PL`
- `pt_BR`
- `ru_RU`
- `uk_UA`
- `zh_CN`

English source is the authority row. Localized rows must preserve:

- packet ID;
- article ID;
- unlock ID;
- speaker/source;
- facts, numbers, units, dates, names, and custody labels;
- spoiler level;
- gameplay instruction;
- tone class.

Localize meaning, not word order. Do not invent local idioms, jokes, metaphors, or extra lore. Personal names remain identity strings; transliterate only when the locale policy requires it. Units must remain gameplay-readable. RTL locales require layout/fallback awareness. CJK and German expansion require UI length proof where text appears in-game.

Machine translation is draft only. Mark native-review status honestly.

For production content, locale rows must contain actual draft text, not only a plan, placeholder, or "same as English" note. If the agent cannot produce a usable draft for a locale, it must mark that locale `BLOCKED_TRANSLATION_DRAFT` and state the blocker. Do not silently omit a locale.

Production locale rows must carry one of these statuses:

- `source_authority`: English authority text.
- `draft_machine_or_llm`: draft translation, not native-approved.
- `BLOCKED_TRANSLATION_DRAFT`: required row exists, but usable draft text could not be produced; blocker must be stated.
- `fluent_reviewed`: reviewed by a fluent reviewer, still not final native QA if UI/audio timing is untested.
- `native_reviewed`: native speaker reviewed meaning, voice, and idiom.
- `runtime_ready`: native-reviewed and tested in the assigned UI/audio/site surface.

When producing all 15 locales in one writing pass, the honest default is `draft_machine_or_llm` for non-English rows unless the task provides native-reviewed text. Do not hide this status in comments; put it in the packet.

Translations must preserve:

- who is speaking;
- what the source knows and does not know;
- exact numbers, dates, units, route names, custody marks, and IDs;
- the same spoiler boundary;
- the same gameplay instruction;
- the same lie or omission if the source is biased.

Translations must not add jokes, idioms, folklore, poetic intensifiers, moral interpretation, or extra exposition to "sound natural". Natural means believable for the source and locale, not bigger.

## GlobalQualityWeight Presentation Density

`GlobalQualityWeight` may scale how much optional lore presentation appears around the same truth:

- Compact: shorter codex bodies, scanner short forms, fewer optional archive fragments, stronger objective clarity.
- Middle: fuller codex entries, more field notes, terminal fragments, and evidence crosslinks.
- High: richer audio fragments, more document variants, stronger environmental contradiction chains, and website/wiki supporting sections.
- Ultra: dense archive material, secondary contradictions, optional dossier commentary, and extended public/wiki versions.

It must not change Article ID, LocID, canon fact, speaker/source, spoiler level, unlock truth, gameplay instruction, save identity, ending eligibility, or public claim state. Quality scaling changes presentation density only. It never makes a different story true.

## Language-Specific Caution

- `ar_SA` and `he_IL`: RTL punctuation, numerals, unit order, and embedded Latin IDs must be checked.
- `ja_JP`, `ko_KR`, `zh_CN`: avoid English idiom carryover; preserve technical IDs and names consistently.
- `de_DE`, `nl_NL`, `pl_PL`, `uk_UA`, `ru_RU`: expect expansion and heavier compound nouns; test UI width.
- `es_ES`, `fr_FR`, `pt_BR`, `id_ID`: avoid over-formal filler; keep operational directness.

If a phrase depends on English wordplay, replace it in source before localization.

## Realism Rules

Good HECTON-8 text usually has restraint:

- one concrete image;
- one procedural fact;
- one consequence;
- one gap.

The gap matters. A survivor does not know the whole conspiracy. A technician does not explain the theme. A public encyclopedia does not have classified truth. A corporate memo does not admit murder. A scanner does not understand guilt.

## Rewriting Bad Text

Bad:

```text
Pellet-Fusion Freight defines the industrial ship history behind HECTON-8.
```

Better external article:

```text
Pellet-fusion freight made Aegir cheap enough to claim and too expensive to abandon. The first cargo did not carry families. It carried reactors, sealant, seed machines, spare hull plates, and enough legal payload to make the route billable before anyone called it habitable.
```

Bad:

```text
Field Note: Use for system overview and skybox/art constraints.
```

Better field note:

```text
Field note, Black Keel optical bay: the shelf has daylight. If a contractor says HECTON-8 is black from orbit, they are selling fear or hiding a bad sensor.
```

Bad:

```text
The vent does not care whether you call it power or weather.
```

Better audio:

```text
Do not stand over the white water. Gauge says heat, visor says oxygen, and both are lying by enough to cook the seal.
```

## Rejection Gates

Reject writing if:

- it sounds like a design specification;
- it uses banned AI/marketing phrases;
- it explains theme instead of showing evidence;
- all surfaces in a packet share the same voice;
- a survivor knows more than they could know;
- a corporation confesses too cleanly;
- a scanner becomes poetic;
- a technical article has no engineering cost;
- a mineral note has no handling condition;
- localization is English-only when the task asked for production content;
- translated variants change facts, names, IDs, or gameplay meaning.

## Production Packet

For any substantial writing task, provide:

- canon sources used;
- surface list;
- speaker/source list;
- unlock context;
- English source text;
- localization plan for all 15 locales or explicit English-only scope;
- forbidden facts avoided;
- evidence object or UI route;
- review status: source draft, localized draft, native-reviewed, or runtime proof attached.

## Acceptance Sentence

Writing is accepted only when it reads like a believable artifact from HECTON-8, preserves canon, avoids AI/specification prose, has a clear speaker and context, and can become a 15-locale packet without changing facts or sounding machine-written.
