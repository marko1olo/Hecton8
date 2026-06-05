# Rationale 1774 - Document Voice Choices

Evidence class: STATIC_DOC.

## Authorities Used

- `AGENTS.md`, `PROJECT_BIBLES.md`, `writing.md`, `narrative.md`, `localization.md`, `textes.md`.
- `Docs/Lore/Lore_Bible.md`, `Canon_Locks.md`, `Lore_Content_System.md`, `Lore_Localization_Model.md`, `Lore_Multilingual_Content_Architecture.md`.
- `Docs/Lore/Humanity_2190_Game_Texture.md`, `Gameable_World_Packets.md`, `Final_Payloads_Gameplay_Map.md`, `AppliedContent/README.md`.
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`.

## Voice Rules Applied

- Terminal fragments: header, source, object state, route consequence, damaged but usable clue.
- Survivor / Marauder notes: immediate need, route/object pressure, wrong assumption, incomplete knowledge.
- Corporate memos: liability-safe action, exposure, continuity, custody, release, certification, no clean confession.
- Colony artifacts: schedule, ledger, tool board, bunk, water, meal, med, route, repair, family/community friction.
- Black-box / relay extracts: machine state, event marker, one contradiction, no empathy prose.

## Exact Repair Choices

- `RS031`: converted first-hour design beats into claim terminal, bathy-drop damage diagnostic, P-63 work order, Deep Reach incident notice, and Atlas maintenance trace.
- `RS050`: first-hour material remains crash diagnostics, pump-room tasking, sanitized accident text, and useful-wrong Atlas trace.
- `RS058`: recovered artifacts remain black-box fragments, work orders, locker nameplates, correction notes, and quarantine relay text.
- `RS072`: colony material remains bunk routines, water ledgers, tool certification boards, community notices, and last-normal-day evidence.
- `RS082`: Deep Reach material remains margin acceptance, safety-weight variance, quarantine hold, loss-conversion, and return-action language.

## Rejected Fake Exposition

- Player-facing fields that say `OPENING BEAT`, `should`, `defines`, `gameplay`, `the player`, or explain authorial purpose.
- Corporate text that admits murder or sounds like a villain monologue.
- Survivor notes that know the full disaster chain.
- Colony artifacts that only summarize colony mood.
- Surface/photic wording that implies HECTON-8's bright zones are inherently ugly, dark, muddy, or worse than depth.

## Localization Decision

`en_US` is source authority. For RS031, non-English rows were refreshed from the repaired source text and marked `Draft XX localization pending native pass.` This prevents stale localized rows from preserving old design-note meaning while still being honest that native review did not happen.

## Index Decision

No publication/index row was changed. Packet JSON is authoring source; regenerated page paths and runtime bindings require the export/import pipeline, not hand edits.

## Scalability Consequences

- Low: same packet IDs and compact scanner/audio units carry the clue.
- Middle: terminal and field-note rows carry object and route context.
- High: PDA/wiki rows add evidence relationships without changing truth.
- Ultra: external/archive surfaces can add richer context without changing canon, LocIDs, unlocks, save identity, or ending truth.
