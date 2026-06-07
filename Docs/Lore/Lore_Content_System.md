# HECTON-8 Lore Content System

Status: working structure.
Evidence class: STATIC_DOC / CONTENT_CORPUS.
Purpose: keep massive lore usable for game codex, terminals, PDA entries, website articles, and writer reference.

## Authority And Proof Boundary

Use `narrative.md` for narrative truth, evidence order, mission state, and text placement. Use `writing.md` for actual in-world prose, encyclopedia pages, scanner/codex text, survivor diaries, terminal notes, technical articles, and AppliedContent packets. Use `localization.md` for LocID status, locale rows, RTL/CJK/fallback, and runtime text proof gates. Use `textes.md` before any real-world marketing, store, social, creator, community, or public-copy use.

This document and the lore markdown corpus are STATIC_DOC / CONTENT_CORPUS evidence only. They do not prove Unity placement, runtime loading, h8bin/Data Monolith bake readiness, localization native review, public website publication, player build behavior, profiler/GC status, or platform readiness.

## Content Rule

We are not producing documentation for its own sake.

Every mature lore file is a future content packet. It must be suitable for at least one real delivery target: in-game codex, scanner note, terminal document, audio transcript, website article, wiki page, contract dossier, or writer-side canon reference.

If a lore unit cannot define Article ID, source voice, spoiler level, unlock route, related articles, and localization path, it remains a draft note.

Mature content must not stop at a specification for future writers. When the task requests content, the packet should contain actual player/public-facing prose for its target surface: encyclopedia entry, scanner fact, survivor diary, terminal note, audio transcript, website article, wiki page, technical note, mineral note, or dossier. Author notes can accompany it, but they do not replace the artifact text.

## Content Layers

Canon Lock:
Short truth. Used by writers and designers. No flavor drift.

Writer Reference:
Full explanation, spoilers allowed, cause/effect clear.

Player Codex:
In-game encyclopedia text. Clear, readable, may be partial or source-biased.

Terminal / Document:
In-world source. Can lie, omit, sanitize, panic, or contradict.

Marauder Field Note:
Practical, angry, procedural. Good for player-facing clarity without omniscience.

Deep Reach Internal:
Corporate, legal, technical, evasive. Good for exposing crime through language.

Website Public:
Spoiler-safe public article for marketing/support/wiki.

Website Archive:
Spoiler-enabled article after release or for deep lore pages.

## Source Voices

Neutral Reference:
Used in writer docs and website overview. Avoids excessive style.

Marauder:
Short sentences. Procedure. Air, debt, dead, route, claim, pressure.

Deep Reach:
Liability, continuity, assets, variance, containment, authorized recovery, delayed certification.

Atlas-6:
Classification, repair, continuity, substrate, inventory, pressure, signal, routing. Not emotional unless corrupted.

Public Domain:
Clean, civic, sanitized. Uses dates and institutions. Avoids body count unless forced.

## Article Metadata Fields

Every mature article should eventually define:

- Article ID.
- Loc namespace.
- Canon status.
- Canon owner.
- Runtime layer: Core, World, or Narrative.
- Spoiler level.
- Source voice options.
- In-game category.
- First unlock route.
- Content targets.
- Localization status.
- Website-safe summary.
- Related articles.
- Open questions.

## Spoiler Levels

0 Public:
Safe before playing.

1 Early Game:
Safe after arrival / first PDA.

2 Midgame:
Requires exploration evidence.

3 Deep Game:
Requires deep modules / Atlas contamination evidence.

4 Ending:
Final truth and outcome material.

## In-Game Categories

Human Space:
Domains, routes, law, salvage economy, history.

Ships And Technology:
Travel, propulsion, carrier classes, survival gear, pressure tools.

Aegir System:
Star anchor, gas giant, moons, route, orbital windows.

HECTON-8:
Moon, depth bands, geology, resources, biosphere, colony remains.

Factions:
Marauders, Deep Reach, claim brokers, domain authorities.

Atlas And Anomalies:
Atlas-6, repair logic, biomechanical systems, signal corruption.

Evidence:
Specific documents, logs, contradictions, disaster chain, ending dossiers.

## Rule

One truth can have multiple voices, but not multiple owners.

Example:

- Truth owner: Canon_Locks / Lore_Bible.
- Public voice: Website Public / PDA.
- Corporate lie: Deep Reach terminal.
- Field interpretation: Marauder note.
- Corrupted interpretation: Atlas fragment.

If an article needs a contradiction, label the source voice instead of making canon ambiguous.

## Localization Contract

Localization is part of lore structure, not a late pass.

- Article IDs and LocIDs are stable across all languages.
- Translation changes text only.
- LocIDs are never translated.
- Runtime lookup must use baked hashes and existing string-pool layers.
- Long codex, terminal, and transcript text belongs in `Narrative`.
- Scanner/entity names and short facts belong in `World`.
- Category/UI labels belong in `Core`.
- Website/wiki exports must keep the same Article ID and canon owner as the in-game version.

Target locale roster:

- `en_US`: English source locale for current data.
- `ru_RU`: Russian.
- `ja_JP`: Japanese.
- `zh_CN`: Simplified Chinese. User alias: `cn`.
- `fr_FR`: French.
- `es_ES`: Spanish.
- `de_DE`: German.
- `pl_PL`: Polish.
- `uk_UA`: Ukrainian. User alias: `ua`.
- `ar_SA`: Arabic, RTL.
- `id_ID`: Indonesian. User alias: `in`.
- `ko_KR`: Korean. User alias: `kr`.
- `he_IL`: Hebrew, RTL. User alias: `jewish`.
- `pt_BR`: Portuguese default.
- `nl_NL`: Dutch.

See `Lore_Localization_Model.md` for LocID shape, website export rules, and QA notes.

## Runtime Data Contract

Lore docs are authoring input. The game should consume baked data.

Runtime should receive numeric IDs, enums, offsets, and localized string hashes, not free-form markdown. Content can be rich in the editor, website, wiki, and writer docs, but gameplay delivery must remain event-driven and static-data based.

Markdown presence, article count, or localization packet text is not runtime readiness. Runtime readiness requires the baked static-data route, current h8bin/Data Monolith evidence where applicable, Unity placement or unlock wiring proof, and the matching runtime/profiler/GC evidence for changed systems.

Required separation:

- authoring: markdown/article packets for humans;
- bake: validation, ID generation, string extraction, relationship tables;
- runtime: immutable records, unlock flags, localized string-pool lookup;
- presentation: PDA, scanner, terminal, audio subtitles, ending dossier.

The runtime never becomes a lore interpreter.

## Production AppliedContent File Set

Production AppliedContent is accepted only when the useful prose is present in the files that feed the product:

- canonical packet/source JSON or production packet source with stable packet ID, Article ID, unlock ID, surfaces, source voice, and locale status;
- generated `in_game_wiki/<locale>/...` and/or `external_site/<locale>/...` pages for every in-scope locale;
- `Publication_Surface_Index.csv` and `Localization_Status_Index.md` refreshed when publication surfaces change;
- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv` and `Assets/_Project/Scripts/Core/Generated/H8AppliedLoreHashes.cs` refreshed when runtime data changes;
- route card, binding map, or scene placement plan when a packet is intended to unlock from gameplay;
- validation/import/audit command output with the real evidence class.

Normal proof route:

- `python -B Tools\AppliedLoreTargetedExporter.py --packet-id <ID> --refresh-indexes`
- `python -B Tools\AppliedLoreImporter.py --root .`
- `python -B Tools\AppliedLoreRuntimeAudit.py --source-only`

Full runtime readiness still requires current Data Monolith bake/static-data evidence and Unity/player proof. Source-only audit proves authoring/export coverage, not in-game visibility.

## Current Priority Topics

1. Player origin and starting contract.
2. Salvage carrier and escape chain.
3. HECTON-8 depth bands and resource ecology.
4. Deep Reach liability doctrine.
5. Atlas-6 original directive.
6. Aegir final astronomy and moon system.
