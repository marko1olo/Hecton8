# Status 3204

ID: 3204
Role: UTF8_MOJIBAKE_AND_CLONE_AUDIT_OWNER
Evidence class: STATIC_SOURCE / STATIC_DOC only
State: COMPLETE - STATIC AUDIT DELIVERED

## Scope Completed

- Read task file `taskslocal/batch32_lore_system_integration/3204_UTF8_MOJIBAKE_AND_CLONE_AUDIT_OWNER.txt`.
- Read required authority docs and 3 relevant mandates.
- Audited production packets under `Docs/Lore/AppliedContent/production_packets/*.md`.
- Included controller updates for P463 and P464.
- Sampled generated P456-P460 pages across 15 locales and both `external_site` / `in_game_wiki`.
- Wrote `Docs/Reports/Batch32/3204_UTF8_MOJIBAKE_AND_CLONE_AUDIT.md`.
- Wrote this status and `Docs/AgentLogs/LOG_3204.md`.

## Findings

- Current production marker scan clean for P418/P461/P462/P463/P464 across required markers and added review markers.
- RS093 P461/P462/P463/P464: 15 locale headers each, zero required bad-codepoint hits.
- Generated P456-P460 pages: 150 sampled files.
- Non-English generated clone comparisons: 140.
- Exact non-English title+body clones versus `en_US`: 140.
- Non-English generated clone files remain `draft_native_pass_pending`: 140.

## Not Done

- No content edits.
- No route card edits.
- No generated page edits.
- No source CSV/h8bin edits.
- No Unity/build/dotnet/runtime validation.
- No native localization claim.

## Next Owner

Implement script-aware static validator:

- fail `U+FFFD`;
- fail exact known mojibake sequences after row/script context;
- warn broad single-codepoint markers;
- block non-English publication/native/runtime status upgrade when generated title+body equals `en_US`;
- require manual review for legitimate Latin/Cyrillic/Arabic/Hebrew/CJK codepoints.

## Controller Addendum

After 3201, generated page status vocabulary changed to `source_authority` / `draft_machine_or_llm`. Clone risk remains: controller spot-check found `ru_RU` P456 in both `in_game_wiki` and `external_site` still exactly matches `en_US` body after normalization.
