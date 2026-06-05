# Validation Results - 1774

Evidence class: STATIC_SOURCE / command output.

## Commands

All packet JSON parse:

```text
python - <<json parse over Docs/Lore/AppliedContent/packets/*.packets.json>>
JSON_OK packet_files=91
```

Changed packet JSON parse:

```text
JSON_OK Docs/Lore/AppliedContent/packets/RS031_FIRST_HOUR_PLAYABLE_SPINE.packets.json packets= 5
JSON_OK Docs/Lore/AppliedContent/packets/RS050_FIRST_HOUR_MICRO_SCRIPT_SURFACES.packets.json packets= 5
JSON_OK Docs/Lore/AppliedContent/packets/RS058_IN_GAME_ARTIFACT_AUDIO_SURFACES.packets.json packets= 5
JSON_OK Docs/Lore/AppliedContent/packets/RS072_COLONY_DAILY_LIFE_EVIDENCE_ATLAS.packets.json packets= 5
JSON_OK Docs/Lore/AppliedContent/packets/RS082_DEEP_REACH_ARTIFACT_MEMO_PACK.packets.json packets= 5
```

AppliedContent source-only audit:

```text
python Tools/AppliedLoreRuntimeAudit.py --root C:\hades\Hecton8 --source-only
AppliedLore source audit OK: packets=460 locales=15 rows=6900 visible_marker_csv_fields=48300 visible_marker_pages=13830 source_route=ok binding_map_rows=460 target_backlog_rows=460 target_prefab_candidate_rows=460 target_auto_prefab_rows=86 target_manual_rows=374 target_candidate_paths=1120 manual_policy_rows=374 manual_terminal_policy_rows=27 manual_discovery_policy_rows=347 manual_template_prefab_rows=27 manual_terminal_prefab_rows=27 placement_plan_rows=374 placement_terminal_rows=27 placement_discovery_rows=347 scene_placement_serialized_rows=7 scene_placement_covered_rows=34 scene_terminal_preview_rows=27 scene_terminal_os_runtime_rows=1 scene_terminal_os_runtime_renderer_slots=27 scene_terminal_os_runtime_transform_slots=27 scene_terminal_os_runtime_verified_slots=27 scene_world_bytes=1799829 scene_world_roots=39 scene_world_mapmagic_rows=2 scene_world_terrain_rows=1 scene_world_terrain_collider_rows=1 scene_world_dependency_warnings=0 scene_world_crest_markers=1 scene_world_ocean_prefab_assets=2 scene_world_ocean_prefab_refs=23 graph_rows=460 route_cards=454 route_source_rows=454 wiki_pages=6900 site_pages=6900 index_pages=30 publication_frontmatter_pages=13800 publication_surface_rows=13800 publication_cluster_rows=150 scene_bindings=7 prefab_bindings=42 asset_bindings=0 authoring_bindings=49
```

Changed packet AI/design-marker scan:

```text
CHANGED_PACKET_MARKERS_OK files=5
```

Whitespace check:

```text
git diff --check -- [five changed packet files]
Result: clean. Git reported LF-to-CRLF warnings for two pre-existing working-copy files only.
```

## Residual Risk

- Source-only audit does not prove Unity runtime UI layout, native localization quality, or player-facing page regeneration.
- Non-English rows changed in this pass are draft/native-review-pending.
