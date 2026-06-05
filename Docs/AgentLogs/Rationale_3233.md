# Rationale 3233

Evidence class: STATIC_SOURCE.

Decision: build RS095 as an authoring-only candidate bundle using RS094 top-level schema and packet object shape.

Reason: task explicitly requires a canonical packet JSON candidate, not source packet edits, runtime bake, importer execution, route-card export, or DataMonolith readiness.

Packet inclusion rule: include only controller-validated P465, P466, P475, P476, P477, P478, and P479. P480-P483 remain active/unvalidated and are excluded.

Localization rule: each packet uses localized dict with 15 locale keys and required surface keys: title, scanner, terminal, audio, in_game_wiki, external_site, field_note. English rows use source-authority surfaces. Non-English rows preserve source draft rows where available; compact packets with only localized Text retain explicit draft-pending surface expansion instead of claiming native-reviewed surface coverage.

Readiness rule: manifest and packet runtime flags stay false. This candidate is not importer-ready, runtime-ready, native-reviewed, DataMonolith-ready, h8bin-ready, Unity-verified, or published.

Protected-path rule: production packet files and all protected source/generated/runtime paths were read-only.

Regression model: no runtime CPU, GC, memory, cadence, or gameplay effect. Risk is static content/schema drift only. Validation is JSON parsing, key/count scans, forbidden-ready-key scan, and path-scope check.