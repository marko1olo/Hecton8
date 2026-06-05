# Rationale 3201

## Decision: Status Vocabulary

Changed localization export status names to explicit evidence states:

- `source_authority` for `en_US`.
- `draft_machine_or_llm` for all non-English generated rows without explicit review proof.

Reason: old `source_ready` and `draft_native_pass_pending` collapsed evidence class and implied native-review routing from generated text. Authority docs require source authority, draft, fluent/native reviewed, and runtime-ready to remain separate proof states.

## Decision: Authoring-Only RS093 Handling

Importer now ignores manifests with no `packet_sources` unless `canonical_importer_ready=true` provides canonical importer sources.

Reason: RS093 currently lists P461-P464 as STATIC_DOC production markdown only. Treating `packets` as canonical packet-source requirements made the exporter fail and falsely implied the packets belonged in source CSV/export/hash/h8bin output. Current truth is authoring-only, not canonical source.

## Decision: Mojibake Guard

Runtime audit now checks player-visible CSV/page text for U+FFFD and suspicious mojibake byte-pair/codepoint sequences.

Reason: P462 was repaired after mojibake in non-English draft rows. Future scans must catch corrupted sequence risks, not only U+FFFD.

Rejected broad single-codepoint blocking for `U+00C3`/`U+00D1`: those are valid locale letters in Portuguese/Spanish examples. Guard uses sequence patterns so valid `RECUPERACAO` with uppercase accented letters is not blocked while UTF-8-as-Latin-1 sequences remain detectable.

## Rejected Actions

- Did not add P461-P464 to source CSV.
- Did not add P461-P464 to generated hash constants.
- Did not mutate route-card CSVs.
- Did not edit `static_data.h8bin`.
- Did not run Unity or dotnet build.
