# Handoff 1777 - Localization Text Bounds QA

Evidence class: STATIC_SOURCE / STATIC_DOC.

## Edited Files

- `Docs/Lore/AppliedContent/Localization_Status_Index.md`
- `Docs/Lore/AppliedContent/Publication_Surface_Index.csv`
- `Docs/Lore/AppliedContent/external_site/ru_RU/P456_SITE_HOME_LONGFORM_BRIEF.md`
- `Docs/Lore/AppliedContent/production_audits/1777/*`
- `Assets/_Project/Scripts/Data/Monolith/H8AppliedLoreRuntime.cs`
- `Assets/_Project/Scripts/Gameplay/MessageTerminal.cs`
- `Assets/_Project/Scripts/LocalizationManager.cs`
- `Assets/_Project/Scripts/ScannableTarget.cs`
- `Assets/_Project/Scripts/UI/HectonOSBootManager.cs`
- `Assets/_Project/Scripts/UI/LocalizedFontResolver.cs`
- `Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs`
- `Assets/_Project/Scripts/UI/PauseMenuController.cs`
- `Tools/AppliedLorePageExporter.py`
- `Docs/Tasks/Status_1777.md`
- `Docs/AgentLogs/Rationale_1777.md`
- `Docs/AgentLogs/HANDOFF_1777.md`
- `Docs/AgentLogs/LOG_1777.md`

## Blocking Facts

- No native-final or native-reviewed proof was found. Non-English rows remain review backlog unless a native/fluent review artifact is supplied.
- `LoreTextBoundsVerifier.py` reports `issues=61060` static text-bound/status-risk findings. This is static source evidence, not TMP/runtime proof.
- Literal marker scan found `draft native pass phrase`, `placeholder`, `TODO`, and `machine localization phrase` in authoring/page bodies. Exact files are in `production_audits/1777/literal_marker_audit.csv` and summarized in `draft_status_leakage_audit.md`.
- `placeholder` is partly a packet subject (`P196_RESOURCE_TABLE_PLACEHOLDER_CONTRACT`), not automatically an error. It is still a release-publication risk if surfaced as player/public copy without route intent.
- No confirmed file-level mojibake remains after codepoint inspection. Console mojibake was display encoding. `RECUPERAÇÃO` and UTF-8 BOM INDEX files were false positives.

## Review Queues

- Native review queue: `Docs/Lore/AppliedContent/production_audits/1777/native_review_queue.md`
- Exact issue candidates: `Docs/Lore/AppliedContent/production_audits/1777/localization_issue_candidates.csv`
- Text expansion risk: `Docs/Lore/AppliedContent/production_audits/1777/text_expansion_risk.md`
- RTL/CJK reader requirements: `Docs/Lore/AppliedContent/production_audits/1777/rtl_cjk_static_reader_requirements.md`

## Runtime/Export Status

- Locale directories: all 15 official locales exist for `external_site` and `in_game_wiki`.
- Page coverage: 460 content pages per locale per surface; no missing/extra content pages in the official locale roster.
- Active packet rows: 460 packet IDs across bundle and legacy single-packet JSON files.
- Runtime source-only audit passes after correcting stale `ru_RU/P456` external-site frontmatter/index status.
- Runtime localization handoff now includes Hebrew/Dutch language plumbing, active AppliedLore locale routing for PDA/terminal/scannable title fallback, bounded PDA metadata seeding, metadata revision updates after existing-row writes, and lock-safe scannable lore entity vault writes.

## Required Follow-Up

- Native/fluent reviewers must handle non-English rows before any `native_reviewed` or `runtime_ready` claim.
- Reader/UI agent must prove RTL bidi isolation and CJK wrapping in `reader.html` or actual TMP surfaces. Static direction fields alone are not proof.
- Publication owner must decide whether internal QA/proof-card packets are allowed on external/player surfaces or should be excluded from release publication sets.
