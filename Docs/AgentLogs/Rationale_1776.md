# Rationale 1776 - Fact Owners And Crosslinks

Evidence class: STATIC_DOC / STATIC_SOURCE only.

## Authority Decisions

- Fact registry must separate truth from source voice. Survivor, corporate, Marauder, scanner, and Atlas surfaces may be partial, wrong, or evasive; fact rows must label the contradiction state.
- Article IDs, packet IDs, LocIDs, unlock IDs, spoiler tiers, and locale roster are stable identity fields. Translation or player-note work cannot rename them.
- Public/wiki surfaces must not leak Atlas-basin payload outcomes, final receiver consequences, or deep ending truth outside spoiler gates.
- Surface, sky, Aegir, moons, coastline, ocean skin, and photic shallows are bright/readable/premium outside storms, interiors, depth, caves, and temporary eclipse windows. Permanent dark-surface claims are canon conflicts.
- Player notes are memory aids after discovery. They must cite route clues, resource needs, contradictions, persons, audio, scanner evidence, repair state, or navigation clues. They must not state final truth before the unlock/evidence route supports it.
- Runtime implementation is out of this task scope. Audit artifacts may propose fact IDs and note templates, but must not invent runtime UI fields or claim baked/runtime readiness.

## Initial Crosslink Decisions

- Use `.packets.json` bundles as owned packet authoring source for this pass. Individual packet JSON outside the requested bundle pattern is evidence for orphan classification, not a reason to silently expand edit scope.
- `Publication_Surface_Index.csv` and `Publication_Cluster_Index.csv` are generated/ingestion surfaces per `AppliedContent/README.md`; direct edits require exact metadata-only proof. Companion audit is preferred unless an unambiguous stale row is found.
- `RS001_FIRST_DESCENT.packets.json` absence is recorded as bundle-scope drift because the README and surface index reference RS001/P001-P005 content while the requested packet bundle pattern starts at RS002.

## Fix Decisions

- No generated index rows were edited. Reason: the surface-index drift resolves to legacy single-packet JSON evidence, not dead content. A safe fix requires schema/exporter ownership, not hand-editing generated CSV.
- No packet body text was edited. Reason: surface-brightness scan found true-supporting guard text, not canon conflicts.
- No new runtime fields were proposed. Player-note candidates reference existing packet IDs, article IDs, and unlock IDs only.
- Cluster separation was handled through `cluster_surface_purpose_audit.csv` because the existing cluster index already has player questions/truth payloads and no dead references.

## Validation Decisions

- Exact validation output is stored in `Docs/Lore/AppliedContent/production_audits/1776/validation_output.txt`.
- Evidence class remains STATIC_SOURCE / STATIC_DOC. No runtime bake, Unity UI, string-pool, localization native review, or publication readiness is claimed.
