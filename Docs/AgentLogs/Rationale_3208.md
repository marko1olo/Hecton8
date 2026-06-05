# Rationale 3208

Evidence class: STATIC_SOURCE only.

## Decisions

- New standalone tool used instead of merging into `Tools/AppliedLoreRuntimeAudit.py` to avoid touching importer/exporter/runtime audit behavior.
- `U+FFFD`, invalid UTF-8 decode, and exact known UTF-8-as-Latin-1 mojibake sequences are hard failures.
- Broad single-codepoint markers are warnings because characters such as `U+00E2` can be legitimate localized text.
- Non-English generated title+body exact clones are warnings when status is draft/pending.
- Non-English generated title+body exact clones are failures when status is native/runtime/publication/public/website ready.
- Missing `en_US` baseline for a scanned generated page is a failure because clone comparison cannot be proven.
- Cheap sample command used `P45[6-9]*,P460*` because the task explicitly allowed limited P456-P460 validation.

## Rejected Actions

- No packet text repair.
- No generated page repair.
- No source CSV, h8bin, route card, importer/exporter, Unity asset, scene, or project setting change.
- No Unity/dotnet/build/profiler claim.
