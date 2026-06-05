# Player Note Templates - 1776

Evidence class: STATIC_DOC. Authoring templates only; no runtime UI fields are invented.

## Contract

Player notes are memory aids after discovery. They are not neutral encyclopedia text, not final truth delivery, and not omniscient hints. Each note must cite an existing packet ID, article ID, unlock ID, and evidence boundary before it can ship.

## Templates

| Template | Use | Required source | English authority pattern |
|---|---|---|---|
| `route_clue` | A remembered route, blocked path, depth layer, relay, ascent, or return lead. | Packet/article/unlock plus route evidence. | `Route clue: [observed constraint]. Check [known route object] before assuming [unsafe conclusion].` |
| `resource_clue` | Material handling, containment, sample quality, stack/custody pressure, or recipe route. | Resource packet and unlock. | `Resource clue: [material] is useful only if [known handling condition] holds.` |
| `contradiction_clue` | Official text conflicts with physical room, scanner, black-box, or worker evidence. | At least two evidence paths. | `Contradiction: [source A] says [claim]. [source B] shows [observed conflict].` |
| `person_clue` | Worker/person identity linked to job, route, tool, locker, or ledger. | Packet/article plus physical/object evidence. | `Person clue: [name] matters through [job/object/route], not biography alone.` |
| `audio_clue` | A recovered line, carrier call, worker transcript, or Atlas/Deep Reach audio fragment. | Audio/subtitle packet and unlock. | `Audio clue: [speaker/source] says [limited fact]. Missing context: [unresolved boundary].` |
| `scanner_clue` | Scan-stage fact, material state, fauna/ecology clue, hazard readout. | Scanner surface and unlock. | `Scanner clue: [observed signal/material] means [limited action], not [late truth].` |
| `repair_clue` | Tool, P-63, seal, relay, guidance, ascent energy, or legal handshake step. | Repair/route packet and unlock. | `Repair clue: [component] answers [specific blocker]. Still missing: [next known blocker].` |
| `surface_navigation_clue` | Aegir, moon windows, eclipse, storm, relay, coastline, ocean skin, or shallow route readability. | Celestial/surface packet and unlock. | `Navigation clue: [window/hazard] is temporary or route-bound. Surface/shallow readability remains baseline outside that event.` |

## Locale Roster Notes

Do not create fake native translations. For player-facing note labels/templates, use stable LocIDs and mark non-English rows as draft/native-review-required until reviewed.

| Locale | Direction | Template status note |
|---|---|---|
| `en_US` | LTR | Source authority text may be authored now. |
| `ru_RU` | LTR | Draft requires Cyrillic/native review; current mojibake rows elsewhere are not release proof. |
| `ja_JP` | LTR | Requires CJK line-break/font proof. |
| `zh_CN` | LTR | Requires Simplified Chinese CJK font/wrap proof. |
| `fr_FR` | LTR | Requires expansion proof. |
| `es_ES` | LTR | Requires expansion proof. |
| `de_DE` | LTR | Requires compound/overflow proof. |
| `pl_PL` | LTR | Requires Latin Extended glyph proof. |
| `uk_UA` | LTR | Requires Cyrillic/native review. |
| `ar_SA` | RTL | Requires RTL punctuation, numeric, and embedded ID proof. |
| `id_ID` | LTR | Requires direct operational tone review. |
| `ko_KR` | LTR | Requires Hangul font/wrap proof. |
| `he_IL` | RTL | Requires RTL punctuation, numeric, and embedded ID proof. |
| `pt_BR` | LTR | Requires expansion proof. |
| `nl_NL` | LTR | Requires expansion proof. |

## LocID Proposal

Use `LORE_NOTE_<TEMPLATE>_<FIELD>` for template labels only, for example:

- `LORE_NOTE_ROUTE_CLUE_LABEL`
- `LORE_NOTE_RESOURCE_CLUE_LABEL`
- `LORE_NOTE_CONTRADICTION_CLUE_LABEL`
- `LORE_NOTE_SURFACE_NAVIGATION_CLUE_LABEL`

This is a proposed authoring convention. Runtime localization/bake owners must approve before implementation.

## Quality Scaling

- Low: one short note sentence.
- Middle: note plus one unresolved question.
- High: note plus source marker and next evidence route.
- Ultra: note plus optional dossier/crosslink context after unlock.

Scaling changes note density only. It cannot change unlock truth, fact owner, article ID, or spoiler level.

