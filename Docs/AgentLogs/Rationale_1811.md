# Agent 1811 Rationale

## Authority Basis

Explicit Agent ID 1811. Status, rationale, log, and report artifacts are required.

Authorities read:
AGENTS.md, PROJECT_BIBLES.md, VISION_LOCKS.md, TASTE.md, quality.md, writing.md, narrative.md, localization.md, Docs/Lore/WriterScenarioAgentPrompt.md, Docs/Reports/Batch18/1804_APPLIED_LORE_DATAMONOLITH_RECONCILE.md.

Additional lore/source-route context consulted:
Docs/Lore/Lore_Content_System.md, Docs/Lore/Lore_Localization_Model.md, Docs/Lore/Lore_Multilingual_Content_Architecture.md, Docs/Lore/Website_Publication_Map.md, and targeted Canon_Locks.md / Lore_Bible.md excerpts for P456 names, spoiler tier, public-site boundary, route pressure, Black Keel, Aegir, Deep Reach, P-63, and Atlas-gate constraints.

Relevant mandates read:
QA_Evidence_Text_Filter_Audit.txt, TOOL_Designer_Facades_CSV_Binary_Bridge.txt, UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt, DATA_Runtime_Struct_Layout_ARM64.txt.

Docs/Actual Domains of Project.txt is absent. Narrow domain: AppliedContent public/in-game writing, localization status honesty, CSV source hygiene.

## Decisions

- Treat P456 as a source repair first. Generated pages may be edited only after source rows are corrected and schema/status safety is proven by static checks.
- Preserve packet, article, unlock, release-set, route-card, locale, and CSV schema identity.
- Do not claim native-final or runtime-ready localization. Non-English agent-generated text remains draft unless proof exists.
- Do not run Unity, DataMonolith bake, PlayMode, profiler, or broad exporter routes in this task.
- P456 residue is present in all 15 source locale rows and all 30 generated `external_site` / `in_game_wiki` pages. Repair must cover the full P456 locale set, not only en_US/ru_RU.
- Keep `en_US` as source authority with `flags=0`. Downgrade `ru_RU` from source-ready to draft/native-review-pending because current RU is mojibake and no native review/font/layout proof exists. Preserve all other non-English rows as draft/native-review-pending with `flags=1`.
- Source owner is `Docs/Lore/AppliedContent/packets/RS092_PUBLIC_SITE_LONGFORM_ARTICLE_BRIEFS.packets.json`. `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv` is a generated mirror from `Tools/AppliedLoreImporter.py`; direct CSV-only repair is not authoritative.
- Non-English P456 rows now use clean English fallback text behind `Draft XX localization pending native pass.` prefixes. The importer strips the prefix for visible CSV/page copy and preserves `flags=1`. This avoids mojibake and avoids false native-final claims.
- Static route used: `python Tools\AppliedLoreImporter.py --root .`, then P456-only markdown rendering using `Tools/AppliedLorePageExporter.py` functions, plus index/status refresh. This is not a DataMonolith bake and does not claim runtime proof.
- `Tools/AppliedLoreRuntimeAudit.py --root . --source-only` was safe to run but failed on unrelated P151 frontmatter status drift. P151/exporter drift is outside 1811 and was not repaired.
- Broad generated page/index dirt exists in the current working tree. Do not revert other agents' generated content; report P456 proof separately from unrelated drift.
