# Rationale 3217

Evidence class: STATIC_SOURCE
Date: 2026-06-05

Decisions:
- Used `discovery_world_prop_required` with `visibly_marked_world_prop` for P461-P464 because the task explicitly required `NarrativeDiscovery.appliedLorePacketHash` policy rows and empty terminal template prefabs.
- Reused source prefab candidates already present in `RS001_RS010_scene_placement_plan.csv` and confirmed they exist on disk. No new prefab, scene, or asset route was introduced.
- Continued the existing `authoring` / `applied_lore_backlog` placement grid. This is source-only authoring data, not serialized scene proof.
- Skipped `python Tools/AppliedLoreRuntimeAudit.py --root . --source-only` because process gate was red. CPU sampled above 50 percent and Unity was running.

Forbidden surfaces:
- No route-card CSV edit.
- No h8bin edit.
- No Unity scene or prefab edit.
- No runtime script edit.
- No production packet Markdown edit.
