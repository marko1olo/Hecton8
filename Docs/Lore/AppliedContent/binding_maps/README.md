# AppliedContent Runtime Binding Maps

Status: scene-authoring source.
Purpose: define which baked AppliedLore packets should be assigned to concrete scene/prefab components.

These maps are not runtime interpreters. They are handoff data for Unity authoring passes.

## Current Maps

- `RS001_RS003_runtime_binding_map.csv`: first 15 baked packets, with hex and decimal hashes for Unity inspector assignment.
- `RS001_RS009_scene_binding_targets.csv`: concrete first-party prefab/data-asset candidates for P001-P045.
- `RS010_scene_binding_targets.csv`: concrete first-party prefab/data-asset candidates for P046-P050.
- `RS001_RS010_manual_binding_policy.csv`: hard policy for the 34 non-auto rows. It says which rows require a real diegetic terminal anchor and which require a visibly marked world prop with `NarrativeDiscovery`.
- `RS001_RS010_scene_placement_plan.csv`: deterministic placement plan for those 34 manual rows in `Assets/_Project/Scenes/02_HECTON_WORLD.unity`. It names the prefab source, object name, placement root, transform, component field, display name, and discovery id.
- `Assets/_Project/Prefabs/Narrative/AppliedLore/PFB_AppliedLore_MessageTerminalAnchor.prefab`: reusable zero-hash terminal template.
- `Assets/_Project/Prefabs/Narrative/AppliedLore/Terminals/PFB_AppliedLore_Terminal_*.prefab`: generated terminal prefabs for terminal policy rows. These are ready for scene placement after `manual_terminal_prefab_rows` reaches the terminal policy row count in audit output; placement is still a separate authoring pass.
- `*_scene_binding_targets.csv` rows are not serialized binding proof. They are worklists for assigning existing packet hashes through Unity API/editor tooling.

## Runtime Fields

- `NarrativeDiscovery.appliedLorePacketHash`
- `NarrativeSpatialTriggerAuthoring.AppliedLoreHash`
- `ScannableFragment.appliedLoreQuarterPacketHash`
- `ScannableFragment.appliedLoreHalfPacketHash`
- `ScannableFragment.appliedLoreFinalPacketHash`
- `MessageTerminal.appliedLorePacketHash`

## Current State

`Tools/AppliedLoreRuntimeAudit.py --root .` currently reports `manual_terminal_prefab_rows=27`, `placement_plan_rows=34`, `scene_placement_serialized_rows=0`, `prefab_bindings=43`, `scene_bindings=0`, and `authoring_bindings=43`.

Meaning: code, blob, UI, scanner, terminal, and POI routes exist. Sixteen safe scan rows and twenty-seven terminal policy prefab rows are serialized as prefab authoring assets. No final scene placement is proven yet. The remaining manual placement work is scene/POI authoring, plus seven `NarrativeDiscovery` rows that require visually marked world props.

Next Unity-safe pass: open `Assets/_Project/Scenes/02_HECTON_WORLD.unity` in Unity and run `Hecton8/Lore/Apply Applied Lore Scene Placement Plan`. The menu is log-only for MCP automation; it refuses to edit unloaded scenes. After Unity saves the loaded scene, rerun the audit. A scene placement pass is not complete until `scene_bindings` rises above zero through Unity API-authored scene objects.

Do not raw-edit `.unity` or `.prefab` YAML for this pass. Use Unity API/editor tooling so file IDs and prefab overrides stay valid.
