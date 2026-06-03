# AppliedLore Localization Status

Generated from `Docs/Lore/AppliedContent/packets/*.packets.json`.
The game reads baked CSV/blob records; markdown is publication output only.

Status meanings:
- `source_ready`: no draft/native-review marker was present in packet text.
- `draft_native_pass_pending`: visible text was stripped of draft markers, but the locale still needs native review.

Locale rows:
- `en_US`: source_ready=460, draft_native_pass_pending=0, packet_rows=460, exported_pages=920, direction=ltr
- `ru_RU`: source_ready=455, draft_native_pass_pending=5, packet_rows=460, exported_pages=920, direction=ltr
- `ja_JP`: source_ready=65, draft_native_pass_pending=395, packet_rows=460, exported_pages=920, direction=ltr
- `zh_CN`: source_ready=65, draft_native_pass_pending=395, packet_rows=460, exported_pages=920, direction=ltr
- `fr_FR`: source_ready=70, draft_native_pass_pending=390, packet_rows=460, exported_pages=920, direction=ltr
- `es_ES`: source_ready=70, draft_native_pass_pending=390, packet_rows=460, exported_pages=920, direction=ltr
- `de_DE`: source_ready=70, draft_native_pass_pending=390, packet_rows=460, exported_pages=920, direction=ltr
- `pl_PL`: source_ready=70, draft_native_pass_pending=390, packet_rows=460, exported_pages=920, direction=ltr
- `uk_UA`: source_ready=70, draft_native_pass_pending=390, packet_rows=460, exported_pages=920, direction=ltr
- `ar_SA`: source_ready=70, draft_native_pass_pending=390, packet_rows=460, exported_pages=920, direction=rtl
- `id_ID`: source_ready=65, draft_native_pass_pending=395, packet_rows=460, exported_pages=920, direction=ltr
- `ko_KR`: source_ready=70, draft_native_pass_pending=390, packet_rows=460, exported_pages=920, direction=ltr
- `he_IL`: source_ready=70, draft_native_pass_pending=390, packet_rows=460, exported_pages=920, direction=rtl
- `pt_BR`: source_ready=70, draft_native_pass_pending=390, packet_rows=460, exported_pages=920, direction=ltr
- `nl_NL`: source_ready=65, draft_native_pass_pending=395, packet_rows=460, exported_pages=920, direction=ltr

Operational rule: do not encode native-review state inside player-visible prose.
Use `flags`/frontmatter/status index for routing, QA and publication gates.
