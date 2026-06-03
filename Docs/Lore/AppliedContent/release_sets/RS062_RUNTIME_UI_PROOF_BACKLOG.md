# Runtime UI Proof Backlog

Status: production-facing AppliedLore source.
Runtime rule: source/export only; runtime consumes baked static data and string-pool offsets.

Lock PDA, scanner, terminal, dossier and localization overflow proof surfaces for future runtime UI implementation without claiming Unity proof.

## Packets

- `P306_PDA_CODEX_STATE_PROOF_CARD` - PDA Codex State Proof Card: Codex entries unlock by evidence state and packet hash, not by global lore progress.
- `P307_SCANNER_STAGE_BINDING_PROOF_CARD` - Scanner Stage Binding Proof Card: Scanner text escalates by scan stage and physical evidence, not by authorial explanation.
- `P308_TERMINAL_SLOT_PROOF_CARD` - Terminal Slot Proof Card: Terminals own short operational records through baked packet hashes and physical operator context.
- `P309_DOSSIER_ENDING_RECORD_PROOF_CARD` - Dossier Ending Record Proof Card: The dossier persists interpretation, route warnings and ending records, not gear or world truth.
- `P310_LOCALIZED_OVERFLOW_PROOF_CARD` - Localized Overflow Proof Card: Localization is accepted only after layout, font, overflow, RTL/CJK and subtitle timing proof.
