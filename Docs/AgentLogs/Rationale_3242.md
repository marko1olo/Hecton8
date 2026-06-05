# Rationale 3242

Decision: write a new standalone P491 packet instead of touching older production packets.
Reason: task explicitly forbids edits to P461-P490 and scopes output to P491 plus worker artifacts.

Decision: keep exact locale status tokens only inside P491 locale rows.
Reason: validation requires exact row counts; duplicating the tokens in logs would poison simple text scans.

Decision: make the player/public disclaimer an in-world evidence hold surface, not a developer warning.
Reason: writing.md requires artifacts, not specifications. The text uses archive, codex, terminal, and Marauder voices while preserving honest review boundaries.

Decision: mark future LocIDs as proposed only.
Reason: task forbids source table edits, route card edits, generated pages, h8bin changes, Unity assets, runtime scripts, and binding maps.

Decision: separate claimant wording, witness-language risk, corporate translation laundering, and public archive captioning across all surfaces.
Reason: task requires those concepts and localization.md requires stable source meaning before any future language proof.

Evidence boundary:
- Evidence class is STATIC_DOC.
- No runtime, source-table, native-review, font/layout, publication, or bake proof is claimed.

Scalability note:
- Low/Compact: shortest caption/stamp form only.
- Middle: codex and public disclaimer.
- High: adds Marauder annotation and custody crosslink.
- Ultra: adds extended archive captioning and optional witness-vs-claimant comparison.
