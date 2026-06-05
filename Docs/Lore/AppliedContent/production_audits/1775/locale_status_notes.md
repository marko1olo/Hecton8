# Locale Status Notes - 1775

Evidence class: STATIC_DOC.

Changed source locales: `en_US`; `ru_RU` source-synced draft rows for `RS088` only.

Changed packet set:

- `P436_BLACK_KEEL_APPROACH_TRANSCRIPT_SEED`
- `P437_DEEP_REACH_SANITIZED_PACKET_TRANSCRIPT_SEED`
- `P438_WORKER_DOSSIER_AUDIO_TRANSCRIPT_SEED`
- `P439_ATLAS_REPAIR_TRACE_TRANSCRIPT_SEED`
- `P440_ENDING_RECORD_TRANSCRIPT_SEED`
- `P286_CAPSULE_BLACKBOX_AUDIO_01`
- `P290_QUARANTINE_RELAY_FRAGMENT`
- `P246_BLACK_KEEL_APPROACH_AUDIO_PACKET`
- `P249_SANITIZED_ACCIDENT_PACKET_BODY`
- `P250_FIRST_ATLAS_REPAIR_TRACE_SCENE`

## Per-Locale Status

This status applies to every changed transcript above unless a later native localization pass replaces the row.

| Locale | Text status | VO status | Lip-sync / timing status | Native review need |
|---|---|---|---|---|
| en_US | source_authority | VO casting needed | subtitle timing needed | source editorial pass only |
| ru_RU | source-synced draft for RS088; other changed packets still source-sync required | not VO-ready | timing blocked until native text pass | native review required |
| ja_JP | draft_machine_or_llm, source-sync required | not VO-ready | CJK wrap/timing blocked | native review required |
| zh_CN | draft_machine_or_llm, source-sync required | not VO-ready | CJK wrap/timing blocked | native review required |
| fr_FR | draft_machine_or_llm, source-sync required | not VO-ready | expansion/timing blocked | native review required |
| es_ES | draft_machine_or_llm, source-sync required | not VO-ready | expansion/timing blocked | native review required |
| de_DE | draft_machine_or_llm, source-sync required | not VO-ready | high expansion risk | native review required |
| pl_PL | draft_machine_or_llm, source-sync required | not VO-ready | expansion/timing blocked | native review required |
| uk_UA | draft_machine_or_llm, source-sync required | not VO-ready | Cyrillic/timing blocked | native review required |
| ar_SA | draft_machine_or_llm, source-sync required | not VO-ready | RTL/timing blocked | native review required |
| id_ID | draft_machine_or_llm, source-sync required | not VO-ready | timing blocked until synced text | native review required |
| ko_KR | draft_machine_or_llm, source-sync required | not VO-ready | CJK/Hangul timing blocked | native review required |
| he_IL | draft_machine_or_llm, source-sync required | not VO-ready | RTL/timing blocked | native review required |
| pt_BR | draft_machine_or_llm, source-sync required | not VO-ready | expansion/timing blocked | native review required |
| nl_NL | draft_machine_or_llm, source-sync required | not VO-ready | expansion/timing blocked | native review required |

## Hard Notes

- Non-English rows were not promoted to native-reviewed or runtime-ready.
- `ru_RU` rows in `RS088_AUDIO_TRANSCRIPT_ARTICLE_SEEDS.packets.json` were cleaned from mojibake and synced to the revised source beats, but still require native review.
- Other non-English packet text may be stale relative to the changed `en_US` authority line.
- Proper nouns and IDs must remain stable: `Black Keel`, `Deep Reach`, `Recovery Compliance`, `Mara Venn`, `Atlas-6`, `HECTON-8`.
- Numeric/custody phrases must preserve meaning: `18%`, `4.8 tonne-window`, `frames 12-19`, `sample custody`, `receiver window`.
