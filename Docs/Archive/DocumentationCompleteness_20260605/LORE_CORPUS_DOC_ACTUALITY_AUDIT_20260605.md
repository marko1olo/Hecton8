# Lore Corpus Documentation Actuality Audit - 2026-06-05

Status: PENDING VERIFICATION
Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM
Owner: DOCUMENTATION_COMPLETENESS_WORKER

## Scope

Mission: lore corpus documentation actuality and route-boundary audit.

Write scope: this report only.

No Unity, dotnet build, importers, tests, Play Mode, profiler, GCMonitor, scene save, prefab mutation, or runtime command was run.

First-20 route impact: removes documentation ambiguity around first-hour lore handoff surfaces such as crash shelf, first scanner/wiki/terminal/audio packets, Black Keel contact, and worker-evidence packet routing. This report does not prove those routes are implemented or runtime-visible.

## Authority And Evidence Read

Root authority sampled as requested:

- `AGENTS.md` writing/narrative/localization/evidence rules.
- `PROJECT_BIBLES.md` route entries for `narrative.md`, `writing.md`, and `localization.md`.
- `narrative.md`.
- `writing.md`.
- `localization.md`.
- `Docs/README.md`.
- `Docs/Reports/DocumentationCompleteness_20260605/DOC_COMPLETENESS_SYNTHESIS_AND_PATCH_QUEUE_20260605.md`.

Mandates followed:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`.
- `.agents-skills/UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt`.

Lore files sampled:

- `Docs/Lore/Encyclopedia/README.md`.
- `Docs/Lore/Encyclopedia/Article_Index.md`.
- `Docs/Lore/Encyclopedia/ARTICLE_TEMPLATE.md`.
- `Docs/Lore/ContentPacks/CP_Index.md`.
- `Docs/Lore/Lore_Content_System.md`.
- `Docs/Lore/Lore_Localization_Model.md`.
- `Docs/Lore/Lore_Multilingual_Content_Architecture.md`.
- `Docs/Lore/Lore_Bible.md`.
- `Docs/Lore/Canon_Locks.md`.
- `Docs/Lore/AppliedContent/README.md`.
- `Docs/Lore/AppliedContent/Localization_Status_Index.md`.
- `Docs/Lore/AppliedContent/binding_maps/README.md`.
- `Docs/Lore/AppliedContent/graphs/README.md`.
- `Docs/Lore/AppliedContent/route_cards/README.md`.
- sampled generated pages, packet JSON, release-set manifests, route cards, graphs, binding maps, and static audit outputs under `Docs/Lore/AppliedContent`.

## Static Corpus Facts

- `Docs/Lore` currently has 14133 files by filesystem scan.
- Extension counts sampled: 13530 `.md`, 390 `.csv`, 199 `.json`, 11 `.txt`, 2 `.py`, 1 `.html`.
- No `Docs/Lore/README.md` exists.
- Index/README surfaces exist under Encyclopedia, ContentPacks, and AppliedContent, but several are stale against current AppliedContent file spread.
- `Docs/README.md` correctly classifies `Docs/Lore` as "Content authority only" and says it does not prove implementation, route availability, or runtime wiring.

## Audit Answers

### Is `Docs/Lore` clearly indexed and bounded as content authority only?

Partially.

The project-level boundary is correct in `Docs/README.md`: `Docs/Lore` is content authority only and does not prove implementation, route availability, or runtime wiring. AppliedContent generated page frontmatter and packet JSON repeatedly state `runtime_reads_markdown: false` / authoring-only runtime contracts.

The local corpus boundary is weak because `Docs/Lore` has no root `README.md`. A worker entering the corpus directly sees 14133 files and multiple local indexes, but no single local gate that repeats the content-only authority boundary, evidence class, root bible route, and no-runtime-proof rule.

### Do lore docs route to `narrative.md`, `writing.md`, and `localization.md` correctly?

Partially.

Root `writing.md` correctly routes to `narrative.md`, `localization.md`, and the lore content docs. Recent production packets under `Docs/Lore/AppliedContent/production_packets` also cite the root writing/narrative/localization bibles.

The older core lore entry docs mostly do not route back to the root bibles. `Docs/Lore/Lore_Content_System.md`, `Docs/Lore/Lore_Localization_Model.md`, `Docs/Lore/Lore_Multilingual_Content_Architecture.md`, `Docs/Lore/Encyclopedia/README.md`, and `Docs/Lore/Encyclopedia/ARTICLE_TEMPLATE.md` define useful local contracts, but they do not state the root route clearly enough for ordinary authors.

### Are source, proof, and publication boundaries explicit enough?

Mixed.

Strong boundaries found:

- `Docs/Lore/AppliedContent/Localization_Status_Index.md` states generated pages are source/export evidence only and native/fluent review, route cards, h8bin bake, Unity placement, and runtime readiness require separate proof.
- `Docs/Lore/AppliedContent/packets/P001_CRASH_SHELF.json` marks `authoring_only`, `runtime_reads_markdown: false`, and `runtime_generates_translation: false`.
- Generated page frontmatter marks `runtime_reads_markdown: false`, locale, surface, direction, and localization status.
- `Docs/Lore/AppliedContent/production_audits/1778/runtime_blockers.md` explicitly states stale `static_data.h8bin`, incomplete scene placement, manual placement backlog, draft localization rows, and no Unity/player/profiler proof.

Weak boundaries found:

- `Docs/Lore/AppliedContent/README.md` uses "production-facing content layer" and labels `external_site/` as "publication-ready website/wiki articles" before the same folder has complete native-review/runtime/publication proof.
- Several local READMEs use current-state counts or "checked" status without artifact timestamp, evidence class, or supersession link.
- Older audit snapshots disagree with current sampled index counts, so they are evidence snapshots only and need a freshness boundary before being used as current truth.

## Top 10 Gaps

| Priority | Path | Gap | Evidence | Required direction |
|---|---|---|---|---|
| P0 | `Docs/Lore/README.md` | Missing root lore entry point. No local corpus-level index repeats content-only authority, root bible routing, proof classes, and no-runtime-readiness boundary. | `Test-Path Docs/Lore/README.md` returned false; corpus has 14133 files. | Add a local README in a later stable-doc patch, not in this audit. |
| P0 | `Docs/Lore/Lore_Content_System.md` | Useful local packet contract, but no explicit route to `narrative.md`, `writing.md`, or `localization.md`; status is "working structure" without evidence class/runtime-proof boundary. | File defines authoring/runtime separation but root bible names are absent in targeted search. | Add root route and evidence boundary. |
| P0 | `Docs/Lore/Lore_Localization_Model.md` | Status vocabulary is stale versus root localization statuses and AppliedContent frontmatter. Uses "source draft/source locked/loc ready/baked/QA passed/Website Ready" instead of `source_authority`, `draft_machine_or_llm`, `fluent_reviewed`, `native_reviewed`, `runtime_ready`, etc. | File maturity states differ from `localization.md` and `Localization_Status_Index.md`. | Align status taxonomy and proof requirements. |
| P1 | `Docs/Lore/Lore_Multilingual_Content_Architecture.md` | Good runtime boundary, but no root-route handoff to `writing.md` and `localization.md`; status lacks evidence class. | File says no runtime interpreter and no runtime translation, but not the root authority route. | Add short authority route and evidence class. |
| P1 | `Docs/Lore/Encyclopedia/README.md` | Encyclopedia is indexed, but the README routes only to `Lore_Localization_Model.md` for localization and does not route writers to root `narrative.md`, `writing.md`, and `localization.md`. It also mentions marketing-support text without pointing to `textes.md` or publication proof gates. | README lists future PDA/terminal/dossier/wiki/codex/marketing-support uses. | Add root writing/localization/public-copy routing and no-runtime-proof boundary. |
| P1 | `Docs/Lore/Encyclopedia/ARTICLE_TEMPLATE.md` | Template lacks several required source-brief fields from `writing.md`/`narrative.md`: speaker/source, audience, what the source knows/does not know, evidence object, physical operation, player use, forbidden facts, and native-review/runtime proof status. | Template only has compact metadata and delivery hooks. | Expand template fields in a later patch. |
| P1 | `Docs/Lore/AppliedContent/README.md` | Mixed current-source and publication language. It says `external_site/` contains "publication-ready website/wiki articles" while status/proof files show draft localization and missing runtime/publication gates. It lists release sets through RS092 while current sampled files include RS093 and RS094. | Current files include `RS093_LORE_SYSTEM_INTEGRATION_BRIDGE*` and `RS094_PUBLIC_AUTHORITY_BRIDGE_EXPANSION*`. | Split "publication output/source candidate" from real publication-ready status; refresh release-set index. |
| P1 | `Docs/Lore/AppliedContent/Localization_Status_Index.md` | It correctly blocks native/runtime claims, but current counts are not fully self-explaining: `packet_rows=464` and `exported_pages=878` per locale imply unpublished or non-page packet rows without naming that delta. | Direct index/page sample shows 439 generated pages per surface per locale; status index reports 464 packet rows. | Add an explicit unpublished/non-page packet explanation and generation timestamp/source command. |
| P2 | `Docs/Lore/AppliedContent/route_cards/README.md` | Stale local index. It says current file is `RS001_RS003_route_cards.csv` for first 15 baked packets and lists only RC001-RC009 while route-card files now extend through RS093. | Files under `route_cards/` include `RS004_route_cards.csv` through `RS093_route_cards.csv`. | Refresh README or point to generated inventory as current source. |
| P2 | `Docs/Lore/AppliedContent/graphs/README.md` | Stale local index. It lists only `RS001_RS003_evidence_graph.csv` while graph files now extend through RS093. | Files under `graphs/` include many RS004-RS093 graph CSVs. | Refresh README or demote it to historical first-wave note. |

## Supporting Boundary Risks

- `Docs/Lore/AppliedContent/binding_maps/README.md` contains useful no-YAML-edit guidance and source-only placement facts, but its current-state counts (`packets=460`, `scene_bindings=7`, etc.) need a timestamp, command, and supersession pointer before being treated as current truth.
- `Docs/Lore/AppliedContent/production_audits/1770/source_only_audit_result.txt` records a failed source-only audit. Later audit files appear to supersede parts of it, but there is no local freshness index that tells a reader which audit snapshot is current.
- `Docs/Lore/AppliedContent/production_audits/1779/current_reader_audit.md` is useful, but its counts differ from current sampled `Publication_Surface_Index.csv` / generated page counts. Treat it as a static snapshot, not current proof.
- Existing production audit rows already identify player-facing field-note surfaces that contain authoring instructions. Those are blocked from runtime by `Docs/Lore/AppliedContent/production_audits/1770/canon_conflict_audit.md`, but the top-level AppliedContent README does not surface that blocker.

## Regression Model

- CPU: no runtime or source code touched.
- GC: no runtime text path touched; no `0 B/frame` claim.
- Memory: no assets imported, baked, or loaded.
- Cadence: no build/import/play/runtime cadence touched.
- Correctness: this report reduces documentation ambiguity only. It does not patch the corpus.

## Hot Path Impact

None. Static documentation report only.

## Failure Modes

- A writer may enter `Docs/Lore` directly and miss root `writing.md` / `narrative.md` / `localization.md` gates.
- A worker may treat generated Markdown or publication indexes as runtime proof.
- A worker may treat draft localization rows as native-reviewed or runtime-ready.
- A worker may treat stale route-card/graph/binding README counts as current proof.
- A publication worker may misread "publication-ready" as public release approval instead of source/export readiness.

## Kept / Rejected

Kept:

- `Docs/README.md` classification of `Docs/Lore` as content authority only.
- AppliedContent frontmatter and packet JSON `runtime_reads_markdown: false` / authoring-only boundaries.
- Localization status index rule that native/fluent/runtime proof is not inferred from packet presence.

Rejected:

- Runtime/native/publication readiness from docs, generated pages, CSV indexes, or static audits alone.
- Treating local route-card/graph/binding README counts as current proof without timestamped artifact linkage.
- Treating authoring instructions embedded in player-facing surface fields as runtime-ship text.

Final status: PENDING VERIFICATION.
