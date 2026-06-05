# Controller Source Admission Ledger P461-P517

Evidence class: STATIC_CONTROLLER_SYNTHESIS.
Runtime proof: absent.
Native localization proof: absent.
DataMonolith/h8bin proof: absent.
Publication proof: absent.

## Purpose

This ledger separates authoring packets, source candidates, and source-admitted rows. It prevents a controller or later worker from treating Markdown packets as runtime data.

## State Classes

| State | Meaning | Runtime claim |
|---|---|---|
| SOURCE_ADMITTED_STATIC_AUDITED | Packet is present in current AppliedLore source structures and passed static source audit. | None. |
| STATIC_SOURCE_CANDIDATE | Packet JSON/manifest candidate exists for later importer/bake work. | None. |
| STATIC_DOC_ACCEPTED | Production packet Markdown exists and passed controller text-shape validation. | None. |
| ACTIVE_STATIC_DOC_TASK | Packet is being written and is not accepted until controller validation passes. | None. |

## Packet Ledger

| Packets | Current state | Evidence |
|---|---|---|
| P461-P464 | SOURCE_ADMITTED_STATIC_AUDITED | RS093 source repair and source-only audit: packets 464, rows 6960, route cards 458, graph rows 464. |
| P467-P474 | STATIC_SOURCE_CANDIDATE | RS094 candidate bundle exists; JSON parses; 8 packets; 15 locales per packet; runtime/import/native/binary readiness false. |
| P465, P466, P475-P479 | STATIC_SOURCE_CANDIDATE | RS095 candidate bundle exists; JSON parses; 7 packets; 15 locales per packet; runtime/import/native/binary readiness false. |
| P480-P487 | STATIC_SOURCE_CANDIDATE | RS096 candidate bundle exists after controller repair of two truncated packet IDs; JSON parses; 8 packets; 15 locales per packet; runtime/import/native/binary readiness false. |
| P488-P491 | STATIC_SOURCE_CANDIDATE | RS097 candidate bundle exists; JSON parses; 4 packets; 15 locales per packet; runtime/import/native/binary readiness false. |
| P492-P495 | STATIC_SOURCE_CANDIDATE | RS098 candidate bundle exists after controller BOM/mojibake repair; JSON parses; 4 packets; 15 locales per packet; runtime/import/native/binary readiness false. |
| P496-P499 | STATIC_SOURCE_CANDIDATE | RS099 candidate bundle exists; JSON parses; 4 packets; 15 locales per packet; required surface keys present; runtime/import/native/binary/page/publication readiness false. |
| P500-P502 | STATIC_SOURCE_CANDIDATE | RS100 candidate bundle exists; JSON parses; 3 packets; 15 locales per packet; required surface keys present; runtime/import/native/binary/page/publication readiness false. |
| P503-P505 | STATIC_SOURCE_CANDIDATE | RS101 candidate bundle exists; JSON parses; 3 packets; 15 locales per packet; required surface keys present; runtime/import/native/binary/page/publication readiness false. |
| P506-P508 | STATIC_SOURCE_CANDIDATE | RS102 candidate bundle exists; JSON parses; 3 packets; 15 locales per packet; required surface keys present; runtime/import/native/binary/page/publication readiness false; byte/codepoint mojibake marker scan clean. |
| P509-P511 | STATIC_SOURCE_CANDIDATE | RS103 candidate bundle exists; JSON parses; 3 packets; 15 locales per packet; required surface keys present; runtime/import/native/binary/page/publication readiness false. |
| P512-P514 | STATIC_SOURCE_CANDIDATE | RS104 candidate bundle exists; JSON parses; 3 packets; 15 locales per packet; required surface keys present; runtime/import/native/binary/page/publication readiness false. |
| P515-P517 | STATIC_SOURCE_CANDIDATE | RS105 candidate bundle exists; JSON parses; 3 packets; 15 locales per packet; required surface keys present; runtime/import/native/binary/page/publication readiness false. |

## Admission Rules

- Do not add P465-P517 to source CSV, route cards, generated pages, h8bin, or Unity binding from this ledger.
- Do not treat RS094, RS095, RS096, RS097, RS098, RS099, RS100, RS101, RS102, RS103, RS104, or RS105 as importer-ready. They are source-candidate artifacts only.
- Do not treat non-English packet rows as native-reviewed. All non-English packet rows remain draft_machine_or_llm until native, RTL/CJK/font/layout, source extraction, and runtime proof exist.
- Runtime must consume baked tables/string pools only after the approved authoring bridge. Runtime must not parse Markdown or these JSON candidate files.

## Next Valid Moves

1. Create the next isolated STATIC_DOC packet wave, create the next isolated source-candidate bundle after packet validation, or pause static authoring for source-admission planning.
2. After a clean process gate and explicit source/bake owner, plan source CSV/route-card/generated-page admission in a separate task with rollback scope and fresh static gates.
3. After source admission, run the approved importer/bake path and produce h8bin/runtime proof before any runtime readiness statement.

## Risk Model

CPU/GC/memory: no runtime code changed by this ledger.

Cadence: no runtime cadence changed.

Correctness: main risk is evidence-class drift. This ledger keeps Markdown, JSON candidates, source rows, generated outputs, and runtime binary proof separated.

Failure mode: a later worker may cite this ledger as admission proof. That is rejected. This ledger is a controller map only.
