# HECTON-8 Multilingual Lore Architecture

Status: working architecture.
Evidence class: STATIC_DOC / CONTENT_CORPUS.
Purpose: make the whole lore base translation-ready without turning writing into spreadsheet sludge.

## Authority And Proof Boundary

Use `narrative.md` for canon truth, evidence order, scene packet, unlock route, and mission/text placement. Use `writing.md` for artifact prose, voice, source knowledge, and multilingual packet shape. Use `localization.md` for accepted locale statuses, stable string IDs, RTL/CJK/fallback, and runtime text proof. Use `textes.md` for real-world public copy, store copy, social posts, creator outreach, captions, and publication-facing claims.

This architecture is STATIC_DOC / CONTENT_CORPUS evidence only. It does not prove Unity placement, player-visible runtime delivery, h8bin/Data Monolith bake readiness, public website publication, native review, profiler/GC status, or platform readiness.

## Working Rule

Write the world once as meaning and voice. Export it into many surfaces and languages.

The user-facing lore should be world-first: objects, places, creatures, routes, damage, tools, contracts, pressure, sky, bodies, ships, and choices. The multilingual layer exists to preserve that world in every language, not to make writers argue about abbreviations.

## Lore Packet Shape

Every important lore unit should be stored as a packet with these text shapes:

Title:
Short name for UI, codex list, website heading.

Scanner Short:
One or two tight sentences. Used while the player is moving.

Field Note:
Practical Marauder voice. Useful, biased, physical.

Codex Body:
Readable player encyclopedia version. Can be partial.

Terminal Fragment:
In-world document. Can lie if source-labeled.

Website Public:
Spoiler-safe article version.

Website Archive:
Spoiler-enabled version for late marketing, wiki, or post-release archive.

Audio Transcript:
Short line or sequence that can become VO/radio/subtitles later.

Writer Truth:
Canon explanation, not player-facing unless marked.

## Translation Unit Size

Do not translate whole giant lore articles as one block.

Preferred units:

- title: 2-8 words;
- scanner short: 80-180 characters source length;
- field note: 1-4 short paragraphs;
- codex body: 300-900 words when needed;
- terminal fragment: 40-400 words;
- audio line: one breath or one subtitle beat;
- website public: 300-1200 words;
- website archive: as long as needed, but still sectioned.

This keeps PDA cards, scanner popups, subtitles, RTL layout, CJK wrapping, and website exports controllable.

## Source Voice Profiles

Neutral Reference:
Plain physical facts. No drama. Best for writer truth, wiki summary, and website overview.

Marauder:
Pressure, air, debt, routes, tools, hull, battery, names, bad maps. Short and useful. Does not sound like a poet.

Deep Reach:
Clean technical language. It should feel like a machine trying to hide a grave under maintenance vocabulary. Use sparingly; less lawyer talk, more operational coldness.

Atlas-6:
Classification, continuity, repair, substrate, pressure, inventory, routing, compatible material, failed distinction. Not villain speech.

Public Domain:
Clean, educational, sanitized. Good for pre-release website and in-universe public records.

Player Personal:
Rare. Triggered by Barnard marks, names, familiar tools, debt, recovered crew traces. It should arrive late enough to feel earned.

## Content Ownership

Canon owner:
Short truth source. Usually `Canon_Locks.md`, `Lore_Bible.md`, or a locked encyclopedia article.

Packet owner:
The gameable packet that turns truth into objects and moments.

Surface owner:
PDA, scanner, terminal, website, audio, ending dossier, or future wiki.

Translation owner:
Locale files and baked string data. Translation must preserve source voice and intent, not English word order.

## World-First Structure

The lore architecture should be organized around what the player touches:

- Arrival and broken equipment.
- Shallows and first ecology.
- Drowned colony spaces.
- Salvage and debt pressure.
- Aegir sky/orbit windows.
- HECTON-8 depth bands.
- Blue debt and other resources.
- Flora/fauna scan families.
- Deep Reach operational traces.
- Atlas repair scars.
- Bottom factory temple.
- Endings and payloads.

Legal/insurance/corporate systems remain background pressure unless they become visible through a contract screen, cargo tag, carrier call, blocked rescue, or sanitized Deep Reach memo.

## Multilingual Pipeline

1. Writer creates or updates a lore packet.
2. Packet gets stable Article ID and text shapes.
3. Source voice is marked per text shape.
4. Content is exported into localization entries.
5. Translators preserve meaning, source voice, and surface length.
6. QA checks scanner/PDA/terminal/subtitle/web layouts.
7. Approved text bakes into runtime data.

No runtime translation generation. No source-voice rewriting during localization. No translated keys. Do not claim `runtime_ready` until the assigned surface has its proof artifact; do not claim public publication readiness from exported markdown or generated pages alone.

## Runtime Boundary

There is no gameplay-time lore interpreter.

Human-readable lore files are cold authoring sources. Build tools can parse them, validate them, and bake them. Runtime systems should receive compact static data:

- numeric Article ID hash;
- numeric LocID hash;
- surface enum: scanner, codex, terminal, audio, website export, ending dossier;
- source voice enum;
- spoiler byte;
- unlock route ID;
- related article indices;
- optional seed tags for placement/discovery order.

The runtime does not read markdown, parse prose, search article links, translate strings, or infer source voice.

The runtime boundary is not satisfied by this document or by markdown count. It requires baked static data, current h8bin/Data Monolith evidence where applicable, ownership/wiring proof, and runtime/profiler/GC evidence for changed delivery systems.

## Data-Oriented Delivery

Lore should become arrays, indices, and immutable records:

- packet headers in one contiguous table;
- text records in one contiguous table;
- relationship records in one contiguous table;
- unlock records in one contiguous table;
- localized text in baked string pools.

Consumer systems read by ID. They do not own truth.

Expected path:

1. POI, scanner, terminal, quest, or ending system publishes an unlock/event ID.
2. Lore/codex owner records unlocked packet IDs.
3. UI requests already-unlocked packet records.
4. Localization resolves LocID hash in the active language pool.
5. UI renders bounded text shapes.

Forbidden path:

1. Runtime scans markdown.
2. Runtime parses article headings.
3. Runtime builds strings from free-form data.
4. Runtime decides which source voice a text has.
5. Runtime searches scene objects to discover lore.

## Zero-GC Rules For Lore

- No runtime string-key lookup.
- No runtime translation generation.
- No markdown parsing in gameplay.
- No LINQ/string concatenation in HUD, scanner, or repeated UI paths.
- No hot-path dictionary growth.
- No scene search for lore ownership.
- No procedural rewrite of canon text.

Variable seeds change where and when a packet appears, which variant is discovered, and what context surrounds it. They do not rewrite canon truth.

## DoD Fit

The lore architecture follows project DoD by treating content as data:

- one fact owner;
- stable ID route;
- event-driven unlock;
- immutable runtime records;
- localized string-pool lookup;
- no direct dependency between unrelated gameplay systems;
- no gameplay truth change from language or quality settings.

`GlobalQualityWeight` may scale presentation density: extra VO, extra terminal pages, richer UI animation, additional scan subnotes, or more environmental dressing. It must not change Article ID, LocID, unlock truth, ending eligibility, save identity, or source facts.

## Naming Policy

Keep hard setting names stable unless a style guide later says otherwise:

- HECTON-8.
- Aegir.
- Atlas-6.
- Deep Reach.
- Black Keel.
- Xenon-Omega.
- blue debt.

Localized prose can explain or transliterate around them, but the setting identity should remain recognizable across languages and websites.

## Current Packet Families

The first multilingual-ready families are:

- arrival and Black Keel;
- Aegir sky windows;
- bright shallows;
- drowned colony spaces;
- blue debt;
- cable kelp and repair drones;
- Barnard marks;
- Atlas repair scars;
- bottom factory temple;
- false exits and final payload decisions.

These should become the first source packets before broad translation work.
