# Status 3214

Task: RS093 binding map and cluster boundary.
Evidence class: STATIC_SOURCE.

## Status

[DONE STATIC] Created RS093 runtime binding map and scene binding target rows for P461-P464.
[DONE STATIC] Documented why `Publication_Cluster_Index.csv` is not the RS093 repair surface under the current RS084 five-row navigation-cluster validator.
[BLOCKED RUNTIME AUDIT] `python Tools/AppliedLoreRuntimeAudit.py --root . --source-only` skipped because process gate was red: CPU load 93%, Unity PID 10764 running.

## Files Changed

- `Docs/Lore/AppliedContent/binding_maps/RS093_runtime_binding_map.csv`
- `Docs/Lore/AppliedContent/binding_maps/RS093_scene_binding_targets.csv`
- `Docs/Reports/Batch32/3214_RS093_BINDING_MAP_AND_CLUSTER_BOUNDARY.md`
- `Docs/Tasks/Status_3214.md`
- `Docs/AgentLogs/LOG_3214.md`

## Verification

- Source CSV presence: P461-P464 found in `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`.
- Hash constants: P461-P464 found in `Assets/_Project/Scripts/Core/Generated/H8AppliedLoreHashes.cs`.
- Surface index presence: P461-P464 found in `Docs/Lore/AppliedContent/Publication_Surface_Index.csv`.
- Candidate prefabs: all RS093 scene-target candidates exist on disk.
- Row counts: `runtime_rows=4`, `scene_target_rows=4`, `source_csv_matches=60`, `surface_index_matches=120`, `hash_constant_matches=4`.
- Header checks: `runtime_header_ok=True`, `scene_header_ok=True`.
- Hash checks: all four RS093 runtime binding rows returned `hex_ok=True` and `uint_ok=True`.
- Git status note: `static_data.h8bin`, `applied_lore_route_cards.csv`, and `Publication_Cluster_Index.csv` were already dirty before 3214 edits and were not touched by this agent.

## Not Claimed

- No Unity verification.
- No runtime/native readiness.
- No DataMonolith readiness.
- No route-card readiness.
