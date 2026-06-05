# Encyclopedia vs AppliedContent Comparison - 1772

Evidence class: STATIC_DOC / STATIC_SOURCE.

## Summary

- Encyclopedia article files checked: 37
- In-game wiki packet rows checked for mapping: 460 en_US rows
- Encyclopedia files missing Article ID: 21
- Encyclopedia files with no verified direct/title packet mapping: 36
- Encyclopedia files without ## Player Codex: 37
- Writer-note-only risk rows: 35

## Findings

- `Docs/Lore/Encyclopedia` is still a writer-facing draft bank. It is useful as canon/reference input, but most files are not direct PDA pages.
- `Docs/Lore/AppliedContent/Publication_Surface_Index.csv` and packet JSON are the practical source for current generated in-game wiki pages.
- Exact Article ID mapping between Encyclopedia and AppliedContent is sparse because AppliedContent uses granular packet IDs and newer article namespaces.
- Treat Encyclopedia pages that lack `## Player Codex` as draft/reference until split into player-facing packet surfaces.
- Do not promote writer notes, table handoff notes, UI proof cards, or route-card implementation notes directly into the PDA without source-voice rewrite.

## CSV Output

- `production_audits/1772/encyclopedia_appliedcontent_comparison.csv` lists every Encyclopedia file, parsed Article ID, verified packet mappings, and issue flags.

## Issue Flags

- `MISSING_ARTICLE_ID`: file cannot be safely mapped into a stable packet route.
- `NO_VERIFIED_PACKET_MAPPING`: no direct Article ID or exact title match in the current en_US in-game wiki index.
- `NO_PLAYER_CODEX_SECTION`: file does not contain a dedicated player codex section.
- `WRITER_NOTE_ONLY_RISK`: file has writer notes but no player codex section; unsafe for direct PDA export.
