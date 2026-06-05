# 3214 RS093 Binding Map And Cluster Boundary

Evidence class: STATIC_SOURCE
Agent: 3214
Date: 2026-06-05

## Scope

Owned files changed:
- `Docs/Lore/AppliedContent/binding_maps/RS093_runtime_binding_map.csv`
- `Docs/Lore/AppliedContent/binding_maps/RS093_scene_binding_targets.csv`
- `Docs/Reports/Batch32/3214_RS093_BINDING_MAP_AND_CLUSTER_BOUNDARY.md`
- `Docs/Tasks/Status_3214.md`
- `Docs/AgentLogs/LOG_3214.md`

Forbidden files not edited by this agent:
- `Docs/Lore/AppliedContent/Publication_Cluster_Index.csv`
- `Docs/Lore/AppliedContent/graphs/RS084_SITE_WIKI_NAVIGATION_CLUSTERS_evidence_graph.csv`
- `Docs/Lore/AppliedContent/route_cards/*`
- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv`
- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`
- Unity scenes, prefabs, assets, and runtime scripts

## What Was Wrong

P461-P464 were present in the canonical AppliedLore source path and publication surface export, but no RS093 binding-map rows existed. `Tools/AppliedLoreRuntimeAudit.py --root . --source-only` validates every baked packet against `*_runtime_binding_map.csv`; missing RS093 rows keep the source-only audit blocked.

`Publication_Cluster_Index.csv` is not the correct repair surface. The current audit/export contract reads only `Docs/Lore/AppliedContent/graphs/RS084_SITE_WIKI_NAVIGATION_CLUSTERS_evidence_graph.csv` through `NAVIGATION_CLUSTER_GRAPH_PATH`, requires `arc_id=site_wiki_navigation_clusters`, requires `primary_surface=external_site`, and rejects any graph length other than exactly five rows. P461-P464 are RS093 bridge packets, not RS084 site/wiki navigation-cluster packets.

## What Changed

Added `RS093_runtime_binding_map.csv` with four rows:
- `P461_PACKET_CUSTODY_BRIDGE` = `0xB9E31203` / `3118666243`
- `P462_PRESSURE_SEAL_FIRST_REPAIR_BRIDGE` = `0x39EB648E` / `971728014`
- `P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE` = `0x64DCA3D4` / `1692181460`
- `P464_BLACK_KEEL_CLAIM_WINDOW_BRIDGE` = `0x6893BE85` / `1754513029`

Added `RS093_scene_binding_targets.csv` with four `NarrativeDiscovery.appliedLorePacketHash` authoring rows. Candidate paths are existing first-party prefabs only:
- `Assets/_Project/Prefabs/Narrative/AppliedLore/PFB_AppliedLore_MessageTerminalAnchor.prefab`
- `Assets/_Project/Prefabs/Construction/Final/PFB_Module_Corridor.prefab`
- `Assets/_Project/Prefabs/Construction/Final/PFB_Module_ServicePump.prefab`
- `Assets/_Project/Prefabs/Construction/Final/PFB_Debris_WreckField.prefab`

## Verification

Commands run:

```text
Get-CimInstance Win32_Processor | Select-Object -ExpandProperty LoadPercentage
```

Output:

```text
93
```

```text
Get-Process | Where-Object { $_.ProcessName -in @('dotnet','csc','VBCSCompiler','Unity','Unity Hub') } | Select-Object ProcessName,Id,CPU
```

Output:

```text
ProcessName    Id       CPU
-----------    --       ---
Unity       10764 44.328125
```

Process gate was red. Python audit was skipped by task rule.

Static source confirmation:

```text
rg -n "P461_PACKET_CUSTODY_BRIDGE|P462_PRESSURE_SEAL_FIRST_REPAIR_BRIDGE|P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE|P464_BLACK_KEEL_CLAIM_WINDOW_BRIDGE" Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv
```

Observed source CSV rows for P461-P464 across the locale block starting at line 6902.

```text
source_csv_matches=60
```

```text
rg -n "P461_PACKET_CUSTODY_BRIDGE|P462_PRESSURE_SEAL_FIRST_REPAIR_BRIDGE|P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE|P464_BLACK_KEEL_CLAIM_WINDOW_BRIDGE|B9E31203|39EB648E|64DCA3D4|6893BE85" Assets/_Project/Scripts/Core/Generated/H8AppliedLoreHashes.cs
```

Output:

```text
469:        public const uint P461_PACKET_CUSTODY_BRIDGE = 0xB9E31203u;
470:        public const uint P462_PRESSURE_SEAL_FIRST_REPAIR_BRIDGE = 0x39EB648Eu;
471:        public const uint P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE = 0x64DCA3D4u;
472:        public const uint P464_BLACK_KEEL_CLAIM_WINDOW_BRIDGE = 0x6893BE85u;
```

```text
rg -n "P461_PACKET_CUSTODY_BRIDGE|P462_PRESSURE_SEAL_FIRST_REPAIR_BRIDGE|P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE|P464_BLACK_KEEL_CLAIM_WINDOW_BRIDGE" Docs/Lore/AppliedContent/Publication_Surface_Index.csv
```

Observed in-game wiki and external-site rows for P461-P464 across locale blocks.

```text
surface_index_matches=120
```

Static file-shape checks:

```text
runtime_rows=4
scene_target_rows=4
runtime_header_ok=True
scene_header_ok=True
```

Hash checks:

```text
P461_PACKET_CUSTODY_BRIDGE hex_ok=True uint_ok=True component=NarrativeDiscovery field=appliedLorePacketHash
P462_PRESSURE_SEAL_FIRST_REPAIR_BRIDGE hex_ok=True uint_ok=True component=NarrativeDiscovery field=appliedLorePacketHash
P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE hex_ok=True uint_ok=True component=NarrativeDiscovery field=appliedLorePacketHash
P464_BLACK_KEEL_CLAIM_WINDOW_BRIDGE hex_ok=True uint_ok=True component=NarrativeDiscovery field=appliedLorePacketHash
```

Candidate path checks:

```text
Assets/_Project/Prefabs/Narrative/AppliedLore/PFB_AppliedLore_MessageTerminalAnchor.prefab exists=True
Assets/_Project/Prefabs/Construction/Final/PFB_Module_Corridor.prefab exists=True
Assets/_Project/Prefabs/Construction/Final/PFB_Module_ServicePump.prefab exists=True
Assets/_Project/Prefabs/Construction/Final/PFB_Debris_WreckField.prefab exists=True
```

Scoped git status after edit:

```text
 M Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin
 M Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv
 M Docs/Lore/AppliedContent/Publication_Cluster_Index.csv
?? Docs/AgentLogs/LOG_3214.md
?? Docs/Lore/AppliedContent/binding_maps/RS093_runtime_binding_map.csv
?? Docs/Lore/AppliedContent/binding_maps/RS093_scene_binding_targets.csv
?? Docs/Reports/Batch32/3214_RS093_BINDING_MAP_AND_CLUSTER_BOUNDARY.md
?? Docs/Tasks/Status_3214.md
```

The three modified prohibited files were already dirty before this agent wrote owned files and were not edited by this agent.

## Counts

- Binding map rows added: 4.
- Scene-target rows added: 4.
- Source-only audit: skipped because CPU/process gate was red.
- Runtime/native/DataMonolith readiness: not claimed.
- Unity/editor/playmode/profiler verification: not run.

## Regression Model

CPU/GC/memory/cadence: no runtime code, scenes, prefabs, assets, h8bin, or route-card source changed by this agent. Runtime impact is `PENDING VERIFICATION`.

Correctness: static CSV rows now match audit-required headers, packet IDs, FNV-1a hashes, allowed component/field pair, and existing prefab candidate paths.

Failure modes: future Python audit can still fail on unrelated concurrent edits, source CSV drift, publication index drift, modified prohibited files by other agents, or scene placement backlog. No runtime readiness is implied.

## Cluster Boundary

Do not force P461-P464 into `Publication_Cluster_Index.csv` under the current exporter. That file is validated from the RS084 graph only. Adding RS093 bridge packets without first expanding the navigation-cluster graph contract would create invalid cluster rows and mix bridge-packet source wiring with site/wiki navigation-cluster truth.
