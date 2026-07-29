# HECTON-8 Lore Corpus

Status: STATIC INDEX - PENDING RUNTIME/UNITY/PUBLICATION VERIFICATION
Evidence class: STATIC_DOC / CONTENT_CORPUS

## Boundary

`Docs/Lore` is a content authority corpus for canon, encyclopedia source text, content packs, localization-facing lore rows, and AppliedContent authoring outputs.

It is not implementation proof. It does not prove runtime wiring, native localization review, baked data readiness, Unity scene placement, in-game visibility, website publication, or public release approval.

Runtime/publication claims require current proof artifacts outside this README: import/bake outputs, static-data validation, Unity/player evidence, localization review records, site publication evidence, or equivalent current gate logs.

## Author Routes

Read these before writing or changing lore content:

- `narrative.md` for evidence order, mission truth, unlock context, and first-20 handoff rules.
- `writing.md` for in-world article, scanner, terminal, audio, diary, technical note, and AppliedContent prose quality.
- `localization.md` for stable LocIDs, locale status, RTL/CJK/fallback rules, and runtime text proof boundaries.
- `Docs/Lore/Encyclopedia/README.md` for article-bank structure.
- `Docs/Lore/ContentPacks/CP_Index.md` for content-pack source grouping.
- `Docs/Lore/AppliedContent/README.md` for packet/export surfaces and current authoring lanes.

## Grand Library Numbering Is An Ordinal, Not An Identifier

The leading number on a `Grand_Library` chapter is a reading-order hint. It is not unique and nothing keys on
it: three chapters share `20_` (`THE_AEGIR_MOONS_AND_ORBITAL_HAZARDS`, `THE_LEVIATHANS`,
`THE_SEED_PROGRAM_AND_THE_ATLAS_DIRECTIVE`) and two share `21_` (`THE_BATHYMETRIC_BANDS_AND_ABYSSAL_SOUND`,
`THE_STYX_DROP_PODS`). `Tools/ValidateGrandLibraryLoreQuality.py` groups by the full filename stem in
`validate_article_group`, so identity is the whole base name plus locale suffix. Renumbering would rename 17
locale files per chapter for no functional gain, so the duplicates stand and this note is the fix.

Two chapters share a TITLE, and that one is deliberate rather than a collision. `19_THE_DEBT_LEDGER` is the
Deep Reach actuarial file, sourced to its Human Resources and Actuarial Division and spoken by an automated
actuarial system. `24_THE_DEBT_LEDGER` is the Keelmark Mutual lien primer, the contractor-facing layer of the
same debt. Each already names the other in its reviewer note. Two institutions describing one debt in their
own registers is the corpus working as intended; do not merge them.

## Source Candidate Rule

AppliedContent packets, generated pages, binding maps, route cards, graphs, publication indexes, and reader outputs are source candidates or authoring outputs unless a current proof artifact says otherwise.

Generated Markdown is not runtime data by itself. Route cards and graphs are not route availability proof by themselves. Localization rows are not native-reviewed or runtime-ready by themselves.

## First-20 Relevance

First-20 relevance here means content handoff surfaces only: scanner/wiki/codex text, terminal entries, audio/subtitle fragments, and evidence packet routing for early player understanding.

This README does not prove those surfaces are placed, visible, localized in runtime, unlocked correctly, baked into `static_data.h8bin`, or playable in Unity.
