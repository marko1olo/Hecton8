# 1827 P151-P155 ru_RU Status Drift Fix

Date: 2026-06-04 05:48 +04
Evidence class: STATIC_SOURCE + CLI_AUDIT

## Scope

Fixed stale generated markdown pages for `ru_RU` first-hour packets:

- `P151_BLACK_KEEL_CONTRACT_APPROACH`
- `P152_DROP_CAPSULE_DAMAGE_SEQUENCE`
- `P153_SHALLOW_ANNEX_P63_PUMP_ROOM`
- `P154_FIRST_SANITIZED_ACCIDENT_PACKET`
- `P155_FIRST_ATLAS_REPAIR_TRACE`

Surfaces repaired:

- `Docs/Lore/AppliedContent/in_game_wiki/ru_RU/*.md`
- `Docs/Lore/AppliedContent/external_site/ru_RU/*.md`

## Root Cause

`Publication_Surface_Index.csv` already had the correct `draft_native_pass_pending,1` state for these rows, but the existing markdown pages were stale. `Tools/AppliedLorePageExporter.py` skips existing localized pages unless `--overwrite` is supplied, while indexes are always regenerated. That can leave frontmatter and visible text behind the source packet state.

## Fix Applied

Regenerated exactly 10 pages from current AppliedContent packet JSON through the existing `render_page(...)` path:

- `Docs/Lore/AppliedContent/in_game_wiki/ru_RU/P151_BLACK_KEEL_CONTRACT_APPROACH.md`
- `Docs/Lore/AppliedContent/external_site/ru_RU/P151_BLACK_KEEL_CONTRACT_APPROACH.md`
- `Docs/Lore/AppliedContent/in_game_wiki/ru_RU/P152_DROP_CAPSULE_DAMAGE_SEQUENCE.md`
- `Docs/Lore/AppliedContent/external_site/ru_RU/P152_DROP_CAPSULE_DAMAGE_SEQUENCE.md`
- `Docs/Lore/AppliedContent/in_game_wiki/ru_RU/P153_SHALLOW_ANNEX_P63_PUMP_ROOM.md`
- `Docs/Lore/AppliedContent/external_site/ru_RU/P153_SHALLOW_ANNEX_P63_PUMP_ROOM.md`
- `Docs/Lore/AppliedContent/in_game_wiki/ru_RU/P154_FIRST_SANITIZED_ACCIDENT_PACKET.md`
- `Docs/Lore/AppliedContent/external_site/ru_RU/P154_FIRST_SANITIZED_ACCIDENT_PACKET.md`
- `Docs/Lore/AppliedContent/in_game_wiki/ru_RU/P155_FIRST_ATLAS_REPAIR_TRACE.md`
- `Docs/Lore/AppliedContent/external_site/ru_RU/P155_FIRST_ATLAS_REPAIR_TRACE.md`

No broad markdown overwrite was run.

Then hardened `Tools/AppliedLorePageExporter.py` so future no-`--overwrite` runs do not silently preserve stale publication-gate frontmatter. Existing pages are still skipped by default, except when their `localization_status` / `localization_flags` differ from the current rendered packet state; in that case the affected page is regenerated.

## Verification

Syntax/helper verification:

```powershell
python -m py_compile Tools\AppliedLorePageExporter.py
```

Helper behavior:

```text
old_state ('source_ready', '0')
drift_matches False
same_frontmatter_matches True
```

Command:

```powershell
python Tools\AppliedLoreRuntimeAudit.py --root . --source-only
```

Result:

```text
AppliedLore source audit OK: packets=460 locales=15 rows=6900 ... publication_frontmatter_pages=13800 publication_surface_rows=13800 publication_cluster_rows=150 ...
```

Additional frontmatter parity scan:

```text
rows 13800 mismatches 0
```

Target rows after repair:

```text
in_game_wiki/ru_RU/P151_BLACK_KEEL_CONTRACT_APPROACH.md|draft_native_pass_pending|1|clean
in_game_wiki/ru_RU/P152_DROP_CAPSULE_DAMAGE_SEQUENCE.md|draft_native_pass_pending|1|clean
in_game_wiki/ru_RU/P153_SHALLOW_ANNEX_P63_PUMP_ROOM.md|draft_native_pass_pending|1|clean
in_game_wiki/ru_RU/P154_FIRST_SANITIZED_ACCIDENT_PACKET.md|draft_native_pass_pending|1|clean
in_game_wiki/ru_RU/P155_FIRST_ATLAS_REPAIR_TRACE.md|draft_native_pass_pending|1|clean
external_site/ru_RU/P151_BLACK_KEEL_CONTRACT_APPROACH.md|draft_native_pass_pending|1|clean
external_site/ru_RU/P152_DROP_CAPSULE_DAMAGE_SEQUENCE.md|draft_native_pass_pending|1|clean
external_site/ru_RU/P153_SHALLOW_ANNEX_P63_PUMP_ROOM.md|draft_native_pass_pending|1|clean
external_site/ru_RU/P154_FIRST_SANITIZED_ACCIDENT_PACKET.md|draft_native_pass_pending|1|clean
external_site/ru_RU/P155_FIRST_ATLAS_REPAIR_TRACE.md|draft_native_pass_pending|1|clean
```

## Residual Risk

The repaired `ru_RU` pages are English draft text after marker stripping because the source packet still contains English placeholder prose under `Draft RU localization pending native pass.` That is acceptable for publication gating only because frontmatter now blocks native-ready release. It is not native Russian content.

An initial mojibake scan suspected P222, but this was a false positive caused by PowerShell degrading non-ASCII detection literals inside an inline script. A second scan using explicit Unicode codepoints reported `mojibake_head 0`.
