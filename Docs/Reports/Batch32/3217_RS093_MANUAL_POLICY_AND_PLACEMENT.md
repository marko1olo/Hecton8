# 3217 RS093 Manual Policy And Placement

Status: STATIC_SOURCE_REPAIRED / PYTHON_AUDIT_BLOCKED_BY_PROCESS_GATE
Evidence class: STATIC_SOURCE
Date: 2026-06-05

## Scope

Owned files changed:
- `Docs/Lore/AppliedContent/binding_maps/RS001_RS010_manual_binding_policy.csv`
- `Docs/Lore/AppliedContent/binding_maps/RS001_RS010_scene_placement_plan.csv`
- `Docs/Reports/Batch32/3217_RS093_MANUAL_POLICY_AND_PLACEMENT.md`
- `Docs/Tasks/Status_3217.md`
- `Docs/AgentLogs/LOG_3217.md`
- `Docs/AgentLogs/Rationale_3217.md`

Mandates followed:
- `QA_Evidence_Text_Filter_Audit.txt`
- `UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt`
- `DATA_Runtime_Struct_Layout_ARM64.txt`
- `TOOL_Designer_Facades_CSV_Binary_Bridge.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

Authority read:
- `AGENTS.md`
- `authoring.md`
- `data.md`
- `localization.md`
- `writing.md`
- `quality.md`
- `Docs/Lore/Lore_Content_System.md`
- `Docs/Lore/AppliedContent/binding_maps/README.md`

## Repair

Added four `NarrativeDiscovery.appliedLorePacketHash` manual policy rows:
- `P461_PACKET_CUSTODY_BRIDGE` = `0xB9E31203` / `3118666243`
- `P462_PRESSURE_SEAL_FIRST_REPAIR_BRIDGE` = `0x39EB648E` / `971728014`
- `P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE` = `0x64DCA3D4` / `1692181460`
- `P464_BLACK_KEEL_CLAIM_WINDOW_BRIDGE` = `0x6893BE85` / `1754513029`

Added four scene placement plan rows:
- Scene path: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- Placement root: `__APPLIED_LORE_SCENE_PLACEMENT`
- Component/field: `NarrativeDiscovery.appliedLorePacketHash`
- Depth/zone: `authoring` / `applied_lore_backlog`
- Source prefabs: existing prefab candidates already referenced by the placement plan and confirmed present on disk.

## Source Check Output

Command: PowerShell `Import-Csv` shape and required-field check.

```text
policy_rows=378
placement_plan_rows=378
P461_PACKET_CUSTODY_BRIDGE: policy=1 placement=1
P462_PRESSURE_SEAL_FIRST_REPAIR_BRIDGE: policy=1 placement=1
P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE: policy=1 placement=1
P464_BLACK_KEEL_CLAIM_WINDOW_BRIDGE: policy=1 placement=1
duplicate_policy_packet_ids=0
duplicate_placement_packet_ids=0
CSV_SHAPE_CHECK=PASS
```

## Process Gate

`python Tools/AppliedLoreRuntimeAudit.py --root . --source-only` was not run.

Reason:

```text
CPU_LOAD_PERCENT=55
Unity 10052 running
```

Batch instruction says to skip Python audit when the process gate is red.

## Forbidden Surface Check

No Unity, dotnet, scene, prefab, runtime script, packet Markdown, route-card CSV, or h8bin write was performed by this task.

Worktree note: forbidden paths were dirty at verification time from adjacent work:
- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`
- `Docs/Lore/AppliedContent/route_cards/RS001_RS003_route_cards.csv`
- `Docs/Lore/AppliedContent/binding_maps/RS093_runtime_binding_map.csv`
- `Docs/Lore/AppliedContent/binding_maps/RS093_scene_binding_targets.csv`

3217 did not edit or revert those paths.

## Remaining State

Runtime/native/DataMonolith/publication readiness is not claimed. Unity scene placement remains pending a Unity-safe authoring pass.
