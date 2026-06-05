# Rationale 1804 - Applied Lore DataMonolith

## Authority Constraints

- `AGENTS.md`: Data Monolith readiness requires active `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` plus import/bake/boot validation. Source files or stale binaries are not runtime readiness.
- `PROJECT_BIBLES.md`: Use `writing.md`, `narrative.md`, `localization.md`, `data.md`, and `authoring.md` for this domain. Do not bulk-read unrelated bibles.
- `VISION_LOCKS.md`: In-world content targets all 15 supported languages immediately, but native-final and runtime-ready claims require proof.
- `TASTE.md`: Lore must follow evidence before exposition and must not read like generic sci-fi or design-spec prose.
- `quality.md`: Static text search is not runtime proof. Proof labels must stay separate.
- `writing.md`: AppliedContent packets need speaker/source, surface, unlock context, evidence object, LocIDs, and honest 15-locale status.
- `narrative.md`: Narrative text must be earned by physical evidence and change a player decision or understanding.
- `localization.md`: Full locale roster is `en_US`, `ar_SA`, `de_DE`, `es_ES`, `fr_FR`, `he_IL`, `id_ID`, `ja_JP`, `ko_KR`, `nl_NL`, `pl_PL`, `pt_BR`, `ru_RU`, `uk_UA`, `zh_CN`; non-English agent text is draft unless proof says otherwise.
- `data.md`: Runtime data claims need stable layout, owner, finite values, and proof; static inspection remains STATIC_SOURCE.
- `authoring.md`: Human-readable CSV/JSON/docs must bake through validation into binary artifacts; no runtime parsing or direct active-binary overwrite claims.

## Mandates Loaded

- `.agents-skills/TOOL_Designer_Facades_CSV_Binary_Bridge.txt`: CSV/source authoring must have schema/hash/validation; binary writes require atomic temp/write/readback/replace.
- `.agents-skills/UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt`: Localization keys must be stable/hash-backed in runtime; 15-locale/RTL/CJK/font status cannot be faked.
- `.agents-skills/DATA_Runtime_Struct_Layout_ARM64.txt`: Binary/native payload claims require layout proof; packed cold records are not hot runtime DTO proof.
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`: Static search proves text presence only; every claim must carry an evidence class.
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`: Runtime text/data consumption must not allocate in hot paths; static source review cannot claim 0 B GC.

## Decisions

- Use static-only reconciliation unless a safe Unity/DataMonolith validator is discovered and CPU/editor state is clear.
- Treat all 1770-1779 handoff claims as leads until verified against files.
- Do not change Batch 17 content text editorially unless the error is objective, small, schema-safe, and rollback is recorded.
- Do not run Unity bake or dotnet build: CPU was above the 50 percent gate and multiple Unity processes were active.
- Use `AppliedLoreRuntimeAudit.py` normal entrypoints as the authoritative full-audit gate. The direct module-level AppliedLore packet parity check is useful evidence, but it is not a substitute for full audit, bake, import, boot, route validation, or runtime UI proof.
- Do not hand-edit `P151_BLACK_KEEL_CONTRACT_APPROACH/ru_RU` generated page/index drift in 1804. The safe owner route is exporter/source reconciliation because generated files can be overwritten and the same route must settle `P456`.
- Do not patch `P456_SITE_HOME_LONGFORM_BRIEF` in generated Markdown only. Current packet source also contains production-brief residue, so page-only edits would hide the symptom and leave the DataMonolith source wrong.
- Downgrade the 1778 `P288_WORKER_LOCKER_NAMEPLATE_SAMPLE/ja_JP` stale-binary lead for current disk state: direct AppliedLore packet parity now passes. It remains historical evidence, not the current first blocker.
- Treat 15-locale row coverage as coverage only. No native-final, native-reviewed, release-clean, TMP-fit, RTL/CJK visual, or runtime-ready localization status was found or claimed.

## No-Edit Decision

No AppliedLore source/content files were changed by 1804.

Rejected fixes:

- `P151` generated frontmatter hand patch: would not settle exporter ownership and could be overwritten.
- `P456` generated-page rewrite: source CSV remains wrong, so generated output would regress on export.
- `static_data.h8bin` rebake: Unity/editor contention and CPU load violate the task gate.
- Legacy single-packet JSON migration: schema/exporter owner decision required.

## Proof Boundaries

- `STATIC_SOURCE PASS`: CSV shape and row counts.
- `STATIC_BINARY PASS`: direct AppliedLore packet record parity only.
- `FULL AUDIT FAIL`: `AppliedLoreRuntimeAudit.py` stops at P151 generated publication drift.
- `PENDING UNITY/DATAMONOLITH BAKE`: import, bake, boot, runtime, scene placement, TMP rendering, and gameplay unlock proof.
