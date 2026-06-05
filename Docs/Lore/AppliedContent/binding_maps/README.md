# AppliedContent Runtime Binding Maps

Status: scene-authoring source.
Purpose: define which baked AppliedLore packets should be assigned to concrete scene/prefab components.

These maps are not runtime interpreters. They are handoff data for Unity authoring passes.

Freshness boundary: local counts and "current" statements in this README are static snapshots unless a timestamped command output or audit artifact says otherwise. Binding CSV rows, publication pages, prefab rows, and scene-target worklists do not prove Unity placement, runtime visibility, or DataMonolith readiness.

## Current Maps

- `RS001_RS003_runtime_binding_map.csv`: first 15 baked packets, with hex and decimal hashes for Unity inspector assignment.
- `RS001_RS009_scene_binding_targets.csv`: concrete first-party prefab/data-asset candidates for P001-P045.
- `RS010_scene_binding_targets.csv`: concrete first-party prefab/data-asset candidates for P046-P050.
- `RS001_RS010_manual_binding_policy.csv`: historical filename for a manual binding policy snapshot. Treat row counts and RS coverage as current only when a newer audit command says so.
- `RS001_RS010_scene_placement_plan.csv`: historical filename for a deterministic scene placement-plan snapshot. Treat row counts, scene coverage, and object placement as current only when a newer Unity API placement pass and audit artifact say so.
- `Assets/_Project/Prefabs/Narrative/AppliedLore/PFB_AppliedLore_MessageTerminalAnchor.prefab`: reusable zero-hash terminal template.
- `Assets/_Project/Prefabs/Narrative/AppliedLore/Terminals/PFB_AppliedLore_Terminal_*.prefab`: generated terminal prefabs for terminal policy rows. Treat them as scene-placement candidates only after `manual_terminal_prefab_rows` reaches the terminal policy row count in a timestamped audit output; placement is still a separate authoring pass.
- `*_scene_binding_targets.csv` rows are not serialized binding proof. They are worklists for assigning existing packet hashes through Unity API/editor tooling.

## Runtime Fields

- `NarrativeDiscovery.appliedLorePacketHash`
- `NarrativeSpatialTriggerAuthoring.AppliedLoreHash`
- `ScannableFragment.appliedLoreQuarterPacketHash`
- `ScannableFragment.appliedLoreHalfPacketHash`
- `ScannableFragment.appliedLoreFinalPacketHash`
- `MessageTerminal.appliedLorePacketHash`

## Last Recorded Source-Only Snapshot

This README previously recorded `Tools/AppliedLoreRuntimeAudit.py --root . --source-only` with `packets=460`, `rows=6900`, `route_cards=454`, `manual_policy_rows=374`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=347`, `placement_plan_rows=374`, `scene_placement_serialized_rows=7`, `scene_bindings=7`, `prefab_bindings=43`, and `authoring_bindings=50`. Without a timestamped command artifact, these numbers are static documentation evidence only.

Meaning for that snapshot: source rows, generated pages, route-card source data, prefab binding rows, and seven serialized scene-binding rows were visible to the audit. That does not prove placement completion, runtime content visibility, native localization, or player-build behavior. Twenty-seven terminal policy rows were recorded for TerminalOS renderer/transform slots; three hundred forty-seven manual rows were recorded as `NarrativeDiscovery` placement backlog instead of TerminalOS expansion.

Next Unity-safe pass: open `Assets/_Project/Scenes/02_HECTON_WORLD.unity` in Unity and run `Hecton8/Lore/Apply Applied Lore Scene Placement Plan`. The menu is log-only for MCP automation; it refuses to edit unloaded scenes. After Unity saves the loaded scene, rerun the audit. A scene placement pass is not complete until a timestamped Unity API placement pass and rerun audit report the expected scene bindings for the scoped target.

Do not raw-edit `.unity` or `.prefab` YAML for this pass. Use Unity API/editor tooling so file IDs and prefab overrides stay valid.

## Historical Source-Only Target

The prior source-only target was `packets=460`, `rows=6900`, `route_cards=454`, `manual_policy_rows=374`, `manual_terminal_policy_rows=27`, `manual_discovery_policy_rows=347`. Newer RS093+ files are outside that target unless a newer audit says otherwise. Runtime scene placement still requires the Unity editor placement pass and proof artifact.
