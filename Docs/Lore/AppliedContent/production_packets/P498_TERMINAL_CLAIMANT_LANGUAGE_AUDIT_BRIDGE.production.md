# P498 Claim Procedure Language Audit Bridge

## Header Metadata

Packet ID: `P498_TERMINAL_CLAIMANT_LANGUAGE_AUDIT_BRIDGE`

Article ID: `TERMINAL_CLAIMANT_LANGUAGE_AUDIT_BRIDGE`

Loc namespace: `LORE_EVIDENCE_TERMINAL_CLAIMANT_LANGUAGE_AUDIT_BRIDGE`

Canonical visible title: Claim Procedure Language Audit

Legacy ID note: stable IDs retain the old `CLAIMANT` token for route compatibility. Visible copy, localization guidance, and generated player text use claim procedure language instead.

Runtime layer: Narrative authoring source.

Surface targets: terminal audit, in-game wiki, public site, field note.

Speaker/source set: Packet Notary Interface audit terminal, public evidence archive, Marauder evidence handler.

Connected lanes: claim procedure wording, worker testimony, quarantine wording, payout wording, partial return wording, localization custody, relation graph.

Proof boundary: the packet JSON, generated pages, publication index, and DataMonolith CSV are the delivery source. This document defines intent and translator guardrails; it does not certify Unity placement, h8bin payloads, native review, or final UI layout.

## Source Brief

Deep Reach paperwork can preserve a physical record while changing the category attached to it. P498 teaches the reader to anchor every clean procedure label to the room, latch, worker tag, route clock, returned object count, and custody strip that produced it.

The evidence pattern is concrete:

- Pump Room B remains occupied after flood mark `03:18`.
- Triage Door 2 is latched from the control side.
- Worker tag `R-17` is absent while the suit ring is logged in salvage mass.
- A return tray is missing four personal kits.
- The office files those facts as release-window variance, quarantine review, Keelmark exposure, and partial cargo eligibility.

The packet must not frame this as a courtroom dispute between named plaintiffs. It is a salvage-custody audit: a procedure noun is being used to route a person, door, tag, or body count away from the fact that produced it.

## Visible Surface Texts

### Scanner

CLAIM LANGUAGE AUDIT // Procedure category found beside physical evidence. Verify room, latch, worker tag, route clock, and returned kit count before accepting the category.

### Terminal

CLAIM LANGUAGE AUDIT / TAU MIRROR INTAKE

Raw: Pump Room B queue occupied after flood mark 03:18.

Filed as: release-window variance.

Raw: Triage Door 2 latched from control side.

Filed as: quarantine review.

Raw: Worker tag R-17 absent; suit ring logged in salvage mass.

Filed as: Keelmark exposure.

Raw: return tray missing four personal kits.

Filed as: partial cargo eligibility.

Reject any category that cannot point to a room, tag, latch, or clock.

### Wiki / Site Body

Deep Reach paperwork can bury a record without deleting it. It keeps the room, tag, timestamp, and body count, then swaps the noun beside them. In the Tau mirror cache, Pump Room B marked occupied after flood-time 03:18 is filed as release-window variance. Triage Door 2 latched from the control side is filed as quarantine review. Worker tag R-17 missing while the suit ring sits in salvage mass is filed as Keelmark exposure. A return tray with four empty kit hooks is filed as partial cargo eligibility.

Read the record from the floor upward: room, latch, tag, clock, returned object count. Claim procedure language becomes evidence only when it can be pinned to a physical trace. If a file keeps the category and drops the worker, the office has already cleaned the route once.

### Audio

They did not scrub the line. They changed the filing noun. Start with the room, not the form.

### Field Note

If a file sounds clean, count hooks, tags, latches, and clocks. The missing noun is usually the worker.

## Localization Guardrails

All 15 locales are present in `Docs/Lore/AppliedContent/packets/RS099_PUBLIC_EVIDENCE_GOVERNANCE_BRIDGE.packets.json` and are exported to the site/wiki pages plus `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`.

Visible titles:

| Locale | Title |
|---|---|
| en_US | Claim Procedure Language Audit |
| ru_RU | Аудит языка претензий |
| uk_UA | Аудит мови претензій |
| es_ES | Auditoría del lenguaje de reclamación |
| fr_FR | Audit du langage des réclamations |
| de_DE | Audit der Anspruchssprache |
| pt_BR | Auditoria da linguagem de reivindicação |
| pl_PL | Audyt języka roszczeń |
| nl_NL | Audit van claimtaal |
| id_ID | Audit Bahasa Klaim |
| ja_JP | 請求手続き文言監査 |
| ko_KR | 청구 절차 언어 감사 |
| zh_CN | 索赔程序语言审计 |
| ar_SA | تدقيق لغة المطالبات |
| he_IL | ביקורת שפת תביעות |

Forbidden visible terms:

- Russian: no `истец`, no `язык истцов`, no courtroom plaintiff framing.
- Ukrainian: no `позивач`, no courtroom plaintiff framing.
- Arabic: no `المطالبين` for this packet; use claim/procedure wording.
- Hebrew: no `תובעים` for this packet; use claim/procedure wording.
- Indonesian: no `Penggugat`; use `klaim`.
- Spanish/French/Portuguese: avoid visible claimant/person nouns when the source means claim procedure language.
- English: do not use `claimant` in visible copy; keep it only inside stable technical IDs.

Draft localization status remains draft outside `en_US`; do not claim native review.

## Integration Notes

Source of truth: packet JSON localized rows.

Generated surfaces: in-game wiki and public site pages for all locales.

Runtime bridge: DataMonolith applied lore CSV rows.

Owner: Narrative AppliedContent.

Failure paths to preserve:

- no data: missing packet row must fail coverage.
- bad data: visible plaintiff/claimant wording must be caught by scoped text scan.
- stale data: targeted exporter must refresh generated pages, publication index, and baked CSV.
- localization drift: technical IDs may keep legacy tokens, but visible text must not.
- source confusion: field note and terminal body must point back to physical evidence, not abstract procedure.

## Authoring Lock

Do not soften the packet into generic “evidence can be manipulated” prose. Every revision must keep the physical chain: room, latch, worker tag, clock, returned kit count, custody category.

Do not add rescue promises, clean legal verdicts, instant response, FTL implication, or ending-route disclosure.

Do not use player-facing authoring explanations such as “this article shows” or “the player learns.” The text should read like recovered evidence, not a design memo.
