# Runtime Blockers - 1778

Evidence class: STATIC_SOURCE / CLI_AUDIT

## Blocking Findings
- No packet-level source blocker found for CSV locale roster, generated packet hashes, route-card packet coverage, supported route-card surfaces, or publication page/index generation.

## Residual Integration Gates
- Current `static_data.h8bin` is stale after source generation: post-page-export full audit failed at `P288_WORKER_LOCKER_NAMEPLATE_SAMPLE/ja_JP` scanner length (`csv=88`, `blob=71`). Run the DataMonolith bake before claiming binary runtime parity.
- Scene placement is incomplete: source-only audit reports `scene_bindings=7`, `prefab_bindings=42`, `authoring_bindings=49`, `scene_placement_covered_rows=34`, while packet count is `460`.
- Manual placement backlog remains: `manual_policy_rows=374`, split as `manual_terminal_policy_rows=27` and `manual_discovery_policy_rows=347`.
- Terminal policy prefabs and TerminalOS slots are present for 27 terminal rows, but Unity scene placement still requires the documented editor menu on the loaded world scene.
- Unity Editor import, Play Mode, player build, profiler, and actual PDA/terminal/scanner runtime proof were not run in this pass.
- `AppliedLorePageExporter.py --root . --overwrite` was run because importer output changed draft flags and publication Markdown/index metadata became stale.

## Locale Roster
- Fixed roster count: 15.
- Draft localization rows flagged in CSV: 5180. These rows are baked with flags; they are native-review risk, not a source-route blocker in current audit.
- `en_US`: rows=460
- `ru_RU`: rows=460
- `ja_JP`: rows=460
- `zh_CN`: rows=460
- `fr_FR`: rows=460
- `es_ES`: rows=460
- `de_DE`: rows=460
- `pl_PL`: rows=460
- `uk_UA`: rows=460
- `ar_SA`: rows=460
- `id_ID`: rows=460
- `ko_KR`: rows=460
- `he_IL`: rows=460
- `pt_BR`: rows=460
- `nl_NL`: rows=460

## Binding Matrix Notes
- Binding matrix rows: 1668. OK rows: 1668.
- Non-OK binding statuses: none.
