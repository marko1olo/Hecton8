# Rationale 3246

Decision: write a new standalone P494 packet instead of touching earlier production packets.
Reason: task explicitly forbids edits to P461-P493 and scopes output to P494 plus worker artifacts.

Decision: connect P488, P491, P492, and packet notary/witness hash authority by packet ID and route role.
Reason: P488 and P491 existed in the production packet directory at initial write. P492 was absent at initial check and later appeared concurrently, so the safe route is to reference its caption-chain role without editing or validating another worker's file.

Decision: make the index an in-world/public browsing surface, not a developer sitemap.
Reason: writing.md requires artifacts instead of specifications. The packet uses archive editor, dossier clerk, and Marauder voices while preserving spoiler and custody boundaries.

Decision: keep future LocIDs proposed only.
Reason: task forbids source CSV edits, route card edits, generated page edits, h8bin changes, Unity assets, runtime scripts, importer/exporter work, and publication tooling.

Decision: group evidence by custody route, physical proof, claimant language, and consequence branch rather than chronology.
Reason: canon and narrative rules require evidence before exposition. Clean chronology can imply accident or false closure; custody/proof/source/consequence keeps the player-facing evidence route honest.

Evidence boundary:
- Evidence class is STATIC_DOC.
- No runtime, source-table, native-review, font/layout, generated-page, Unity, DataMonolith, h8bin, or public deployment proof is claimed.

Scalability note:
- Low/Compact: title, one index line, four labels, and custody warning only.
- Middle: category explanation plus archive crosslink.
- High: Marauder annotation and richer crosslink labels.
- Ultra: dense archive browsing and secondary crosslinks after the spoiler gate only.
