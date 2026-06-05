# Status 1771 - External Site / Public Wiki

Status: COMPLETE
Evidence class: STATIC_DOC / STATIC_SOURCE

## Tasks

- [x] 01 Create/update Status_1771.md with 20 tasks and checkpoints.
- [x] 02 Create/update Rationale_1771.md with concrete editorial decisions.
- [x] 03 Inventory external_site for all 15 locales.
- [x] 04 Compare external_site inventory with Website_Publication_Map.md pillars and mark gaps.
- [x] 05 Checkpoint: selected highest-value module.
- [x] 06 Audit and patch false dark-surface framing.
- [x] 07 Audit and mark/move omniscient spoilers.
- [x] 08 Improve at least one weak public pillar cluster.
- [x] 09 Improve public navigation metadata.
- [x] 10 Checkpoint: reopen changed en_US, ru_RU, RTL pages.
- [x] 11 Define translation units for improved modules.
- [x] 12 Update multilingual status report.
- [x] 13 Confirm surface/shallows language matches visual target.
- [x] 14 Confirm Deep Reach public language is archive/liability/history.
- [x] 15 Checkpoint: safe export decision.
- [x] 16 Cross-link public modules to unlock concepts without late spoilers.
- [x] 17 Create public_site_editorial_map.md.
- [x] 18 Create HANDOFF_1771.md.
- [x] 19 Re-run CSV sanity checks.
- [x] 20 Final verification, update status, append LOG_1771.md.

## Checkpoint 05

Selected module for improvement: `P456_SITE_HOME_LONGFORM_BRIEF` in `external_site/*/`.

Reason: it is the public home/start pillar, but the current page is a production brief with player-invisible instructions, not a public article. It also exposes localization debt: `ru_RU` contains mojibake/mixed English, and draft locales carry English placeholder copy. Fixing this page improves the strongest entry point without waiting on agents 1770 or 1772-1779.

Public pillar comparison:

- Humanity 2190: present through domain and route pages; readable but mixed with many generated briefs.
- Aegir System: present; cluster exists.
- Moon System: present; cluster exists.
- HECTON-8: present; home and start-here weak because `P456` is not a real article.
- Marauders: present through Barnard/player-origin pages; not selected for this pass.
- Barnard Yards: present.
- Deep Reach: present; needs spoiler-safe liability framing review.
- Ships and Travel: present.
- Black Keel: present.

Audit artifacts:

- `Docs/Lore/AppliedContent/production_audits/1771/external_site_inventory.csv`

## Checkpoint 10

Reopened changed pages:

- `external_site/en_US/P456_SITE_HOME_LONGFORM_BRIEF.md`
- `external_site/ru_RU/P456_SITE_HOME_LONGFORM_BRIEF.md`
- `external_site/ar_SA/P456_SITE_HOME_LONGFORM_BRIEF.md`

Result: no player-visible draft/localization disclaimer in body text. Draft state appears only in frontmatter for tooling.

## Checkpoint 15

Static page export was not run. `Tools/AppliedLorePageExporter.py --overwrite` regenerates both `external_site` and `in_game_wiki`; the task explicitly forbids editing in-game wiki pages except for comparison. Running the exporter would overwrite scoped hand edits and touch forbidden surface output. Decision: manual external-site patch plus CSV/index sanity checks.

## Completed Audit Outputs

- `Docs/Lore/AppliedContent/production_audits/1771/public_translation_units.md`
- `Docs/Lore/AppliedContent/production_audits/1771/public_site_editorial_map.md`
- `Docs/AgentLogs/HANDOFF_1771.md`

## Final Verification

Static scans on `external_site/*/P456_SITE_HOME_LONGFORM_BRIEF.md` returned no hits for:

- Production brief residue: `Public brief`, `Longform spine`, `SITE HOME`, `Assemble for website`, draft/native-pass body disclaimers.
- Late spoiler leakage: `final receiver`, `Atlas basin`, `sell coordinates`, `sever Atlas`, `public ledger`, `payload receiver`, `ending outcome`, `final payload`.
- False dark-surface framing: `black void`, `dark void`, `inherently dark`, `permanent dark`, `brown dwarf`, `no useful starlight`, `ugly surface`, `ugly shallows`, `miserable surface`.

CSV sanity:

- `Publication_Surface_Index.csv`: 13,800 rows; 15 external-site P456 rows; 1 `en_US` `source_ready` row; 14 non-English `draft_native_pass_pending` rows.
- `production_audits/1771/external_site_inventory.csv`: 6,900 rows; 15 locales; 15 P456 rows; 1 source row; 14 draft rows.
- `Publication_Cluster_Index.csv`: 150 rows; 75 external-site cluster rows; 15 locales.

Reopened samples after final wording pass:

- `Docs/Lore/AppliedContent/external_site/en_US/P456_SITE_HOME_LONGFORM_BRIEF.md`
- `Docs/Lore/AppliedContent/external_site/ru_RU/P456_SITE_HOME_LONGFORM_BRIEF.md`
- `Docs/Lore/AppliedContent/external_site/ar_SA/P456_SITE_HOME_LONGFORM_BRIEF.md`

Runtime/export status: no Unity, dotnet, or exporter run. Evidence remains `STATIC_DOC / STATIC_SOURCE`.
