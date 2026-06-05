# 3208 Applied Lore Text Integrity Validator

Date: 2026-06-05 local.
Worker: 3208 - APPLIED_LORE_TEXT_INTEGRITY_VALIDATOR.
Evidence class: STATIC_SOURCE only.
Runtime/native/publication claim: none.
Content edits: none.

## Scope

Implemented `Tools/AppliedLoreTextIntegrityAudit.py`.

The tool validates:

- production packet marker counts under `Docs/Lore/AppliedContent/production_packets/*.md`;
- generated page text markers under `Docs/Lore/AppliedContent/in_game_wiki/*/*.md` and `Docs/Lore/AppliedContent/external_site/*/*.md`;
- generated non-English title/body exact clone risk versus `en_US` after stripping frontmatter and generated HTML comments.

First-20 route blocker removed: cheap static gate now exists for public/wiki/codex AppliedContent corruption and non-English English-clone risk before opening Black Keel / P-63 route text is promoted.

## Authorities And Mandates

Authority docs read:

- `AGENTS.md`
- `taskslocal/batch32_lore_system_integration/BATCH_INDEX.txt`
- `writing.md`
- `localization.md`
- `quality.md`
- `authoring.md`
- `data.md`
- `Docs/Lore/Lore_Localization_Model.md`
- `Docs/Lore/Lore_Multilingual_Content_Architecture.md`
- `Docs/Lore/Lore_Content_System.md`
- `Docs/Reports/Batch32/3204_UTF8_MOJIBAKE_AND_CLONE_AUDIT.md`

Mandates read:

- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt`
- `.agents-skills/TOOL_Designer_Facades_CSV_Binary_Bridge.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

## Tool Behavior

- Fails on invalid UTF-8 decode.
- Fails on `U+FFFD`.
- Fails on exact known UTF-8-as-Latin-1 mojibake sequences.
- Warns on broad single-codepoint markers `U+00C2/U+00C3/U+00D0/U+00D1/U+00D8/U+00E2/U+00E6/U+00EC/U+00D7`.
- Warns when draft/pending non-English generated pages exactly clone `en_US`.
- Fails when non-English exact clones are marked native/runtime/publication/public/website ready.
- Supports cheap sample scan with `--packet-glob 'P45[6-9]*,P460*'` or `--sample-p456-p460`.

## Command

```powershell
python -m py_compile Tools/AppliedLoreTextIntegrityAudit.py
python Tools/AppliedLoreTextIntegrityAudit.py --root . --packet-glob 'P45[6-9]*,P460*'
```

## Output

```text
AppliedLore text integrity audit
root=C:\hades\Hecton8
packet_glob=P45[6-9]*,P460*
production_packets=6
production_fail_markers U+FFFD=0
production_broad_markers U+00C2=0 U+00C3=0 U+00D0=0 U+00D1=0 U+00D7=0 U+00D8=0 U+00E2=1 U+00E6=0 U+00EC=0
production_exact_mojibake=0
generated_pages=150
generated_fail_markers U+FFFD=0
generated_broad_markers U+00C2=0 U+00C3=0 U+00D0=0 U+00D1=0 U+00D7=0 U+00D8=0 U+00E2=0 U+00E6=0 U+00EC=0
generated_exact_mojibake=0
clone_scan en_baselines=10 non_en_compared=140 title_exact=140 body_exact=140 both_exact=140 draft_clone_warnings=140 unknown_clone_warnings=0 partial_clone_warnings=0 ready_clone_failures=0 missing_en_baselines=0
broad_marker_samples:
  Docs/Lore/AppliedContent/production_packets/P465_DEEP_REACH_MANAGED_VARIANCE_BRIDGE.production.md U+00E2=1
clone_warning_samples:
  Docs/Lore/AppliedContent/in_game_wiki/ar_SA/P456_SITE_HOME_LONGFORM_BRIEF.md draft_exact_en_clone
  Docs/Lore/AppliedContent/in_game_wiki/ar_SA/P457_AEGIR_HARD_SCIFI_LONGFORM_BRIEF.md draft_exact_en_clone
  Docs/Lore/AppliedContent/in_game_wiki/ar_SA/P458_DEEP_REACH_LIABILITY_LONGFORM_BRIEF.md draft_exact_en_clone
  Docs/Lore/AppliedContent/in_game_wiki/ar_SA/P459_ATLAS_SPOILER_LONGFORM_BRIEF.md draft_exact_en_clone
  Docs/Lore/AppliedContent/in_game_wiki/ar_SA/P460_BLUE_DEBT_RESOURCE_LONGFORM_BRIEF.md draft_exact_en_clone
  Docs/Lore/AppliedContent/in_game_wiki/de_DE/P456_SITE_HOME_LONGFORM_BRIEF.md draft_exact_en_clone
  Docs/Lore/AppliedContent/in_game_wiki/de_DE/P457_AEGIR_HARD_SCIFI_LONGFORM_BRIEF.md draft_exact_en_clone
  Docs/Lore/AppliedContent/in_game_wiki/de_DE/P458_DEEP_REACH_LIABILITY_LONGFORM_BRIEF.md draft_exact_en_clone
FINAL: WARN
```

## Static Findings

- Tool compiles under Python syntax check.
- Production packet scan currently covers 6 packet files because `P465_DEEP_REACH_MANAGED_VARIANCE_BRIDGE.production.md` exists in `production_packets`.
- No `U+FFFD` failures.
- No exact known mojibake sequence failures.
- One broad marker warning: `U+00E2` in `P465_DEEP_REACH_MANAGED_VARIANCE_BRIDGE.production.md`.
- Codepoint context check shows the P465 hit is a Portuguese accented word using `U+00E2`, not exact mojibake. It remains warning-only.
- Generated P456-P460 sample covers 150 files: 5 packets x 15 locales x 2 surfaces.
- Non-English generated comparisons: 140.
- Exact non-English title+body clones versus `en_US`: 140.
- Clone files are draft-status warnings, not ready-status failures in this sample.

## Not Done

- No packet text edits.
- No generated page edits.
- No source CSV edits.
- No route card edits.
- No h8bin edits.
- No importer/exporter behavior edits.
- No Unity, dotnet, player, profiler, DataMonolith, native review, or publication deployment proof.

## Residual Blockers

- P456-P460 non-English generated pages remain exact English clones and must not be promoted past draft/pending status.
- Broad marker warnings require human review when they appear in legitimate locale text.
- Full generated page scan was not run in this pass; cheap P456-P460 sample mode was used per task.
