# Rationale 1778 - Applied Lore DataMonolith Integrator

## Decisions

- Batch mode is active because the supplied task file names Agent ID `1778` and requires status/rationale/log artifacts.
- Runtime route law stayed fixed: no runtime Markdown/JSON/CSV parsing for AppliedLore. Runtime consumes baked static records, hashes, route hashes, surface masks, unlock IDs, flags, and localized string byte slices only.
- The 15-locale roster stayed fixed: `en_US`, `ru_RU`, `ja_JP`, `zh_CN`, `fr_FR`, `es_ES`, `de_DE`, `pl_PL`, `uk_UA`, `ar_SA`, `id_ID`, `ko_KR`, `he_IL`, `pt_BR`, `nl_NL`.
- Source-only audit was the primary safety gate because Unity scene placement and DataMonolith bake would touch shared scene/binary outputs during parallel work.
- Importer and route-card exporter were run because they are deterministic owned-scope generators. Importer changed `applied_lore_packets.csv`; route-card export and hash constants produced no semantic diff.
- Page exporter was initially treated as risky because it rewrites generated publication pages and indexes. After importer changed draft flags, post-import source audit failed on publication frontmatter drift. Running `AppliedLorePageExporter.py --root . --overwrite` was the correct owner route, and the post-export source audit passed.
- Full H8BIN proof split into two facts: pre-generation full audit passed, then post-generation full audit failed because `static_data.h8bin` is stale. This is a bake blocker, not a source-route blocker.
- Generated docs use `CANDIDATE` only for write-capable commands not executed at the time of the safe-command decision. Runtime method names and menu paths were verified with `rg` against existing C# source.

## Command Evidence

- `python Tools/AppliedLoreRuntimeAudit.py --root . --source-only` passed before generation.
- `python Tools/AppliedLoreRuntimeAudit.py --root .` passed before generation with `applied_records=6900` and `applied_routes=454`.
- `python Tools/AppliedLoreImporter.py --root .` output: `applied_lore_packets=460 localized_rows=6900 draft_localization_rows=5180`.
- `python Tools/AppliedLoreRouteCardExporter.py --root .` output: `applied_lore_route_cards=454`.
- `python Tools/AppliedLorePageExporter.py --root . --overwrite` output: `applied_lore_pages_written=13800 skipped_existing=0 index_pages_written=30`.
- `python Tools/AppliedLoreRuntimeAudit.py --root . --source-only` after page export passed with `publication_frontmatter_pages=13800`, `publication_surface_rows=13800`, and `publication_cluster_rows=150`.
- `python Tools/AppliedLoreRuntimeAudit.py --root .` after page export failed: `Record 120 P288_WORKER_LOCKER_NAMEPLATE_SAMPLE/ja_JP scanner length mismatch: csv=88 blob=71`.

## Residual Blockers

- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` must be rebaked through `Hecton8/Data Monolith/Bake Static Data`.
- Scene placement remains incomplete: source-only audit reports `scene_bindings=7`, `prefab_bindings=42`, `authoring_bindings=49`, and `scene_placement_covered_rows=34` against 460 packets.
- Runtime/in-game behavior is not proven until Unity import, Play Mode, player build packaging, profiler/GC checks, and PDA/scanner/terminal interaction tests run.
