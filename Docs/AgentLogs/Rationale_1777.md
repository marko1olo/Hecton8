# Rationale 1777 - Localization Text Bounds QA

Evidence class: STATIC_DOC / STATIC_SOURCE unless command output states otherwise.

## Decisions

- Treat task file `taskslocal/batch17_lore_content_1770_1779/1777_LOCALIZATION_TEXT_BOUNDS_QA_COORDINATOR.txt` as explicit agent/logging mode. It provides Agent ID 1777 and says no XML extraction is required.
- Do not claim native-final, native-reviewed, runtime-ready, or UI-fit proof unless a native review/runtime artifact exists. No such proof is present at task start.
- Do not rewrite creative prose. Allowed edits are limited to evidence-backed status/count/index corrections, audit artifacts, and unambiguous player-visible marker or encoding fixes.
- Locale roster is fixed to 15 runtime locales: `en_US`, `ru_RU`, `ja_JP`, `zh_CN`, `fr_FR`, `es_ES`, `de_DE`, `pl_PL`, `uk_UA`, `ar_SA`, `id_ID`, `ko_KR`, `he_IL`, `pt_BR`, `nl_NL`.
- Console mojibake is not accepted as file corruption. Packet text must be checked by Unicode codepoint before marking encoding defects.
- Packet inventory uses all active `Docs/Lore/AppliedContent/packets/*.json` records, not only `*.packets.json`, because current publication/status totals are 460 rows and include legacy single-packet JSON files.
- `AppliedLorePageExporter.py` status-index source wording was corrected for the same reason; leaving the `*.packets.json` claim would keep producing misleading audit evidence.
- `ru_RU/P456_SITE_HOME_LONGFORM_BRIEF` external-site frontmatter was corrected from `draft_native_pass_pending` to `source_ready` because the source packet flags, runtime CSV route, and in-game wiki page classify that packet/locale as source-ready. Current aggregate `ru_RU` status is still `source_ready=435`, `draft_native_pass_pending=25`; this is not a native-review claim.
- PDA AppliedLore metadata seeding must remain presentation-phase work, but not one-frame bulk work. The corrected route scans `16..96` records per VISUAL_SYNC frame from `GlobalQualityWeight`; low devices amortize the catch-up, high devices finish faster, and active entry text still resolves directly from `H8AppliedLoreRuntime`.
- PDA metadata revision must describe row writes, not only first-time imports. Existing DataMonolith/H8LR metadata rows can be rewritten after locale/data source refresh; those writes now trigger `TryCommitMetadataRevision(true)`.
- `ScannableTarget` lore entity snapshot writes must not use legacy mutable `TryResolveHandle`. The route now acquires either the AUP buffer or the hash buffer, writes one slot, and releases in `finally` before touching the next handle. Position resolution and string resolution stay outside locked regions.

## Checkpoint Decisions

- Task 05: Document risks instead of rewriting content. Top risk is draft/status/QA language leaking into publication surfaces, plus native-review backlog and static bounds risk.
- Task 10: No player-facing prose was patched. Exact marker blockers are documented for publication owners/native reviewers.
- Task 15: Static audit artifacts parse; source-only runtime audit passes after stale status correction.
- Task 20: No native-final/native-reviewed/runtime-ready status was claimed.
- Follow-up recount: `Localization_Status_Index.md` and `localization_status_recount.csv` now match current packet source flags for all 15 locales.
- Follow-up lock flattening: `ScannableTarget` keeps read access pure via `TryReadOnlyHandle`; writer access uses `TryAcquireWriteLock`/`ReleaseWriteLock` and never holds both lore entity buffers at once.
