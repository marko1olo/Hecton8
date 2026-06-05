# Rationale 3244

Date: 2026-06-05
Worker: 3244
Evidence class: STATIC_DOC

## Decision Record

Decision: create a new standalone P492 packet instead of touching existing P461-P491 packets.

Reason: the assigned task scopes writes to P492 and worker tracking files and explicitly forbids edits to earlier packets.

Decision: make the caption chain an in-world archive/custody surface, not a developer note.

Reason: writing.md requires believable artifacts. The packet uses public archive, dossier, terminal stamp, and Marauder voices while preserving the same custody facts.

Decision: connect redaction header, native localization hold, witness hashes, Tau Ceti ledger delay, and public misuse risk in every primary surface.

Reason: the task requires those links, and canon locks state public evidence only matters when custody, witness hash, and relay notary escape claimant control.

Decision: mark all LocIDs as proposed and keep all runtime/source/bake/public-surface claims out of the packet.

Reason: the task forbids source table edits, route cards, generated pages, h8bin changes, Unity assets, runtime scripts, and importer/exporter work.

Decision: use ASCII-safe draft locale text for this static packet after the first mojibake marker scan failed.

Reason: adjacent packets contain visible encoding corruption, and this channel converted native-script draft rows into mojibake markers. The task requires strict UTF-8, no replacement characters, and no explicit mojibake markers. Native-script authoring remains a future review task, not a state claimed here.

## Canon Constraints Applied

- Evidence comes before exposition.
- Public archive copy is spoiler-gated and must not expose final payload consequences outside its lane.
- Salvage truth becomes public evidence only when chain-of-custody, witness hashes, and relay notary leave claimant control.
- Tau Ceti can create delayed public pressure; it cannot rescue the player or control who misuses the record.
- Translation/native review status must stay honest and must not be upgraded by markdown source text.

## Proof Constraint

This pass is static documentation only. It does not prove Unity placement, runtime loading, source extraction, h8bin/Data Monolith state, site posting, native-language review, profiler behavior, GC behavior, or public owner approval.

## Scalability Note

- Low/Compact: shortest caption/stamp form only.
- Middle: dossier caption and terminal stamp.
- High: public caption and Marauder correction.
- Ultra: extended archive commentary and secondary caption variants without changing Article ID, LocIDs, custody truth, receiver outcome, locale state, save identity, or public claim state.
