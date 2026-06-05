# Validation Commands - 1777

Evidence class: STATIC_SOURCE / STATIC_DOC.

## LoreTextBoundsVerifier

Command:

```powershell
python Tools/LoreTextBoundsVerifier.py --root . --json-report Docs/Lore/AppliedContent/production_audits/1777/lore_text_bounds_report.json --csv-report Docs/Lore/AppliedContent/production_audits/1777/lore_text_bounds_report.csv
```

Output:

```text
lore_text_bounds packets=460 surfaces=48300 issues=61060 collisions=0 rewrites=0
json=Docs/Lore/AppliedContent/production_audits/1777/lore_text_bounds_report.json
csv=Docs/Lore/AppliedContent/production_audits/1777/lore_text_bounds_report.csv
```

## AppliedLoreRuntimeAudit

First run command:

```powershell
python Tools/AppliedLoreRuntimeAudit.py --root . --source-only
```

First run output:

```text
AppliedLore audit FAILED: Publication page C:\hades\Hecton8\Docs\Lore\AppliedContent\external_site\ru_RU\P456_SITE_HOME_LONGFORM_BRIEF.md missing frontmatter line: localization_status: source_ready
```

Correction:

- Updated `external_site/ru_RU/P456_SITE_HOME_LONGFORM_BRIEF.md` frontmatter to `localization_status: source_ready` and `localization_flags: 0`.
- Updated matching `Publication_Surface_Index.csv` row to `source_ready`, flags `0`.
- Current `Localization_Status_Index.md` recount is `ru_RU`: `source_ready=435`, `draft_native_pass_pending=25`.
- Reason: source packet flags currently classify `P456_SITE_HOME_LONGFORM_BRIEF/ru_RU` as `source_ready`; the aggregate locale count is lower because later packet rows carry draft/native-review prefixes. This is not a native-review proof.

Second run command:

```powershell
python Tools/AppliedLoreRuntimeAudit.py --root . --source-only
```

Second run output:

```text
AppliedLore source audit OK: packets=460 locales=15 rows=6900 visible_marker_csv_fields=48300 visible_marker_pages=13830 source_route=ok binding_map_rows=460 target_backlog_rows=460 target_prefab_candidate_rows=460 target_auto_prefab_rows=86 target_manual_rows=374 target_candidate_paths=1120 manual_policy_rows=374 manual_terminal_policy_rows=27 manual_discovery_policy_rows=347 manual_template_prefab_rows=27 manual_terminal_prefab_rows=27 placement_plan_rows=374 placement_terminal_rows=27 placement_discovery_rows=347 scene_placement_serialized_rows=7 scene_placement_covered_rows=34 scene_terminal_preview_rows=27 scene_terminal_os_runtime_rows=1 scene_terminal_os_runtime_renderer_slots=27 scene_terminal_os_runtime_transform_slots=27 scene_terminal_os_runtime_verified_slots=27 scene_world_bytes=1799829 scene_world_roots=39 scene_world_mapmagic_rows=2 scene_world_terrain_rows=1 scene_world_terrain_collider_rows=1 scene_world_dependency_warnings=0 scene_world_crest_markers=1 scene_world_ocean_prefab_assets=2 scene_world_ocean_prefab_refs=23 graph_rows=460 route_cards=454 route_source_rows=454 wiki_pages=6900 site_pages=6900 index_pages=30 publication_frontmatter_pages=13800 publication_surface_rows=13800 publication_cluster_rows=150 scene_bindings=7 prefab_bindings=42 asset_bindings=0 authoring_bindings=49
```

Follow-up source-route wording check:

```powershell
python -m py_compile Tools/AppliedLorePageExporter.py
python Tools/AppliedLoreRuntimeAudit.py --root . --source-only
rg scan for stale status-index source wording, stale `ru_RU` aggregate counts, and stale scene-world byte count across exporter/status/audit/log files.
```

Follow-up output:

```text
AppliedLorePageExporter.py py_compile OK.
AppliedLore source audit OK: packets=460 locales=15 rows=6900 visible_marker_csv_fields=48300 visible_marker_pages=13830 source_route=ok binding_map_rows=460 target_backlog_rows=460 target_prefab_candidate_rows=460 target_auto_prefab_rows=86 target_manual_rows=374 target_candidate_paths=1120 manual_policy_rows=374 manual_terminal_policy_rows=27 manual_discovery_policy_rows=347 manual_template_prefab_rows=27 manual_terminal_prefab_rows=27 placement_plan_rows=374 placement_terminal_rows=27 placement_discovery_rows=347 scene_placement_serialized_rows=7 scene_placement_covered_rows=34 scene_terminal_preview_rows=27 scene_terminal_os_runtime_rows=1 scene_terminal_os_runtime_renderer_slots=27 scene_terminal_os_runtime_transform_slots=27 scene_terminal_os_runtime_verified_slots=27 scene_world_bytes=1799829 scene_world_roots=39 scene_world_mapmagic_rows=2 scene_world_terrain_rows=1 scene_world_terrain_collider_rows=1 scene_world_dependency_warnings=0 scene_world_crest_markers=1 scene_world_ocean_prefab_assets=2 scene_world_ocean_prefab_refs=23 graph_rows=460 route_cards=454 route_source_rows=454 wiki_pages=6900 site_pages=6900 index_pages=30 publication_frontmatter_pages=13800 publication_surface_rows=13800 publication_cluster_rows=150 scene_bindings=7 prefab_bindings=42 asset_bindings=0 authoring_bindings=49
No stale source-route/count pattern matches.
```

## Runtime Patch Follow-Up

Scope: `Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs`.

Validation:

```powershell
validate_script Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs level=basic include_diagnostics=true
git diff --check -- Assets/_Project/Scripts/UI/PDAEncyclopediaStreamer.cs
hot-method scan for forbidden lookup/allocation tokens across modified C# files
orphan .meta scan under Assets, Packages, ProjectSettings
```

Output:

```text
PDAEncyclopediaStreamer.cs basic validation: errors=0 warnings=0.
git diff --check: no whitespace errors; CRLF warnings only.
hot-method scan: no forbidden token matches inside Tick/FixedUpdate/LateFrameTick/Execute/Update bodies.
orphan .meta scan: no rows.
dotnet build not launched; dotnet processes were already active.
```

Metadata revision follow-up:

```text
PDAEncyclopediaStreamer.cs basic validation: errors=0 warnings=0.
Hot-method scan after revision patch: no forbidden token matches inside Tick/FixedUpdate/LateFrameTick/Execute/Update bodies.
AppliedLoreRuntimeAudit.py --source-only: PASS, packets=460, locales=15, source_route=ok.
dotnet build not launched; dotnet process was already active.
```

ScannableTarget lore entity lock follow-up:

```text
ScannableTarget.cs: no remaining TryResolveHandle references.
Lore entity consumer route uses TryReadOnlyHandle.
Lore entity writer route uses TryAcquireWriteLock and ReleaseWriteLock in finally, one handle at a time.
Hot-method scan after ScannableTarget patch: no forbidden token matches inside Tick/FixedUpdate/LateFrameTick/Execute/Update bodies.
Basic Unity validate_script: errors=0 warnings=0 for LocalizationManager.cs, H8AppliedLoreRuntime.cs, LocalizedFontResolver.cs, PauseMenuController.cs, HectonOSBootManager.cs, PDAEncyclopediaStreamer.cs, MessageTerminal.cs, ScannableTarget.cs.
Unity console error read: 0 entries.
git diff --check: no whitespace errors; CRLF warnings only.
dotnet build not launched; dotnet process was already active.
```

Final source-only audit after lock follow-up:

```text
AppliedLore source audit OK: packets=460 locales=15 rows=6900 visible_marker_csv_fields=48300 visible_marker_pages=13830 source_route=ok binding_map_rows=460 target_backlog_rows=460 target_prefab_candidate_rows=460 target_auto_prefab_rows=86 target_manual_rows=374 target_candidate_paths=1120 manual_policy_rows=374 manual_terminal_policy_rows=27 manual_discovery_policy_rows=347 manual_template_prefab_rows=27 manual_terminal_prefab_rows=27 placement_plan_rows=374 placement_terminal_rows=27 placement_discovery_rows=347 scene_placement_serialized_rows=7 scene_placement_covered_rows=34 scene_terminal_preview_rows=27 scene_terminal_os_runtime_rows=1 scene_terminal_os_runtime_renderer_slots=27 scene_terminal_os_runtime_transform_slots=27 scene_terminal_os_runtime_verified_slots=27 scene_world_bytes=1801916 scene_world_roots=39 scene_world_mapmagic_rows=2 scene_world_terrain_rows=1 scene_world_terrain_collider_rows=1 scene_world_dependency_warnings=0 scene_world_crest_markers=1 scene_world_ocean_prefab_assets=2 scene_world_ocean_prefab_refs=23 graph_rows=460 route_cards=454 route_source_rows=454 wiki_pages=6900 site_pages=6900 index_pages=30 publication_frontmatter_pages=13800 publication_surface_rows=13800 publication_cluster_rows=150 scene_bindings=7 prefab_bindings=42 asset_bindings=0 authoring_bindings=49
orphan .meta scan under Assets/Packages/ProjectSettings: no rows.
dotnet build not launched; dotnet processes were already active.
```

## Parse Checks

Commands:

```powershell
Import-Csv Docs/Lore/AppliedContent/production_audits/1777/packet_locale_field_matrix.csv
Import-Csv Docs/Lore/AppliedContent/production_audits/1777/locale_directory_inventory.csv
python -m json.tool Docs/Lore/AppliedContent/production_audits/1777/lore_text_bounds_report.json
```

Outputs:

```text
packet_locale_field_matrix.csv rows=105
locale_directory_inventory.csv rows=15
lore_text_bounds_report.json json_ok
```
