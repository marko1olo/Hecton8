# HANDOFF 1775 - Audio Blackbox Transcript Screenwriter

Evidence class: STATIC_DOC / STATIC_SOURCE.

## Files Edited

- `Docs/Lore/AppliedContent/packets/RS088_AUDIO_TRANSCRIPT_ARTICLE_SEEDS.packets.json`
- `Docs/Lore/AppliedContent/packets/RS058_IN_GAME_ARTIFACT_AUDIO_SURFACES.packets.json`
- `Docs/Lore/AppliedContent/packets/RS050_FIRST_HOUR_MICRO_SCRIPT_SURFACES.packets.json`
- `Docs/Lore/AppliedContent/production_audits/1775/audio_blackbox_inventory.csv`
- `Docs/Lore/AppliedContent/production_audits/1775/subtitle_segmentation_notes.md`
- `Docs/Lore/AppliedContent/production_audits/1775/speaker_source_map.md`
- `Docs/Lore/AppliedContent/production_audits/1775/locale_status_notes.md`
- `Docs/Lore/AppliedContent/production_audits/1775/audio_style_sheet.md`
- `Docs/Tasks/Status_1775.md`
- `Docs/AgentLogs/Rationale_1775.md`

## VO Casting Needed

- `P436_BLACK_KEEL_APPROACH_TRANSCRIPT_SEED`: Black Keel tender / carrier automation.
- `P437_DEEP_REACH_SANITIZED_PACKET_TRANSCRIPT_SEED`: Recovery Compliance packet voice.
- `P438_WORKER_DOSSIER_AUDIO_TRANSCRIPT_SEED`: Mara Venn, pump chief.
- `P439_ATLAS_REPAIR_TRACE_TRANSCRIPT_SEED`: Atlas-6 maintenance telemetry.
- `P440_ENDING_RECORD_TRANSCRIPT_SEED`: dossier recorder.
- `P286_CAPSULE_BLACKBOX_AUDIO_01`: damaged capsule recorder.
- `P290_QUARANTINE_RELAY_FRAGMENT`: quarantine relay / custody voice.
- `P246_BLACK_KEEL_APPROACH_AUDIO_PACKET`: Black Keel first-hour tender.
- `P249_SANITIZED_ACCIDENT_PACKET_BODY`: Deep Reach public packet.
- `P250_FIRST_ATLAS_REPAIR_TRACE_SCENE`: Atlas first trace telemetry.

## Subtitle UI Review Needed

- `P290`: long denial beat; split on narrow UI.
- `P437`: legal/cost line may expand in DE/RU/PL and RTL.
- `P246`: `4.8 tonne-window` must remain readable and stable.
- `P286`: frame range `12-19` and black-box source label must remain legible.

## Placement / Spoiler Notes

- `P436`, `P246`, `P286`, `P249`, `P250` are first-hour safe.
- `P437` is early/mid Deep Reach pressure; it does not expose full liability chain.
- `P438` is worker evidence; no family hook.
- `P439` is Atlas maintenance telemetry; safe only after first Atlas trace context.
- `P440` is ending/spoiler-gated.
- `P290` belongs to partial exit/custody routes.

## Localization Blockers

- `ru_RU` rows in `RS088_AUDIO_TRANSCRIPT_ARTICLE_SEEDS.packets.json` were source-synced and mojibake-cleaned, but remain native-review required.
- Other non-English rows remain draft/source-sync-required after `en_US` authority edits.
- No non-English VO, lip-sync, native-reviewed, or runtime-ready claim is valid.
- Existing mojibake/draft rows outside `RS088` remain a localization backlog item outside this repair scope.
