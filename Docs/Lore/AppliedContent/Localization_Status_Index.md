# AppliedLore Localization Status

Generated from AppliedContent release-set manifests and packet JSON sources.
The game reads baked CSV/blob records; markdown is publication output only.

Status meanings:
- `source_authority`: English authority row for current AppliedContent export.
- `draft_machine_or_llm`: non-English generated draft row; native/fluent review not proven.
- Reviewed states (`fluent_reviewed`, `native_reviewed`, `runtime_ready`) require explicit per-locale proof and are not inferred from packet presence.

Locale rows:
- `en_US`: source_authority=643, draft_machine_or_llm=0, draft_marker_rows=0, packet_rows=643, exported_pages=1236, direction=ltr
- `ru_RU`: source_authority=0, draft_machine_or_llm=643, draft_marker_rows=632, packet_rows=643, exported_pages=1236, direction=ltr
- `ja_JP`: source_authority=0, draft_machine_or_llm=643, draft_marker_rows=632, packet_rows=643, exported_pages=1236, direction=ltr
- `zh_CN`: source_authority=0, draft_machine_or_llm=643, draft_marker_rows=632, packet_rows=643, exported_pages=1236, direction=ltr
- `fr_FR`: source_authority=0, draft_machine_or_llm=643, draft_marker_rows=632, packet_rows=643, exported_pages=1236, direction=ltr
- `es_ES`: source_authority=0, draft_machine_or_llm=643, draft_marker_rows=632, packet_rows=643, exported_pages=1236, direction=ltr
- `de_DE`: source_authority=0, draft_machine_or_llm=643, draft_marker_rows=632, packet_rows=643, exported_pages=1236, direction=ltr
- `pl_PL`: source_authority=0, draft_machine_or_llm=643, draft_marker_rows=632, packet_rows=643, exported_pages=1236, direction=ltr
- `uk_UA`: source_authority=0, draft_machine_or_llm=643, draft_marker_rows=632, packet_rows=643, exported_pages=1236, direction=ltr
- `ar_SA`: source_authority=0, draft_machine_or_llm=643, draft_marker_rows=632, packet_rows=643, exported_pages=1236, direction=rtl
- `id_ID`: source_authority=0, draft_machine_or_llm=643, draft_marker_rows=632, packet_rows=643, exported_pages=1236, direction=ltr
- `ko_KR`: source_authority=0, draft_machine_or_llm=643, draft_marker_rows=632, packet_rows=643, exported_pages=1236, direction=ltr
- `he_IL`: source_authority=0, draft_machine_or_llm=643, draft_marker_rows=632, packet_rows=643, exported_pages=1236, direction=rtl
- `pt_BR`: source_authority=0, draft_machine_or_llm=643, draft_marker_rows=632, packet_rows=643, exported_pages=1236, direction=ltr
- `nl_NL`: source_authority=0, draft_machine_or_llm=643, draft_marker_rows=632, packet_rows=643, exported_pages=1236, direction=ltr

Operational rule: do not encode native-review state inside player-visible prose.
Use `flags`/frontmatter/status index for routing, QA and publication gates.
Canonical packet JSON and publication pages are source/export evidence only; native/fluent review, route cards, h8bin bake, Unity placement, and runtime readiness require separate proof.
