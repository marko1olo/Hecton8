# Dossier Save Presentation Rules

Status: production-facing draft pending native localization.

Replay UI, risk cards, ending records, save flags and spoiler tiers.

## Packets

- `P176_DOSSIER_SELECTION_UI_RULE` - Dossier Selection UI Rule: Dossier Selection UI Rule defines how replay knowledge appears to players.
- `P177_RISK_WEIGHT_CONTRACT_CARD` - Risk Weight Contract Card: Risk Weight Contract Card defines replay contract knobs.
- `P178_ENDING_RECORD_PRESENTATION` - Ending Record Presentation: Ending Record Presentation defines the dossier-facing shape of outcomes.
- `P179_SAVE_PROFILE_KNOWLEDGE_FLAGS` - Save Profile Knowledge Flags: Save Profile Knowledge Flags defines persistence-safe replay memory.
- `P180_WEBSITE_WIKI_SPOILER_TIERING` - Website And Wiki Spoiler Tiering: Website And Wiki Spoiler Tiering defines publication gates for AppliedLore.

## Runtime Rule

Authoring/export source only. Runtime consumes baked static-data rows, packet hashes and string-pool offsets.
No runtime markdown parsing, JSON parsing, live translation or scene search.
