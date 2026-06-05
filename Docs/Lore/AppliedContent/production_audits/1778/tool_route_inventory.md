# AppliedLore Tool Route Inventory

Evidence class: STATIC_SOURCE / CLI_HELP / CLI_AUDIT

## Tools

| Tool | Purpose | Inputs | Outputs | Required args | Safe command |
|---|---|---|---|---|---|
| `Tools/AppliedLoreImporter.py` | Imports AppliedContent packet JSON/manifests into DataMonolith Narrative CSV and generated hash constants. | `Docs/Lore/AppliedContent/release_sets/*_manifest.json`; packet JSON referenced by manifests. | `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`; `Assets/_Project/Scripts/Core/Generated/H8AppliedLoreHashes.cs`. | optional `--root ROOT`. | `python Tools/AppliedLoreImporter.py --root .` is deterministic but write-capable; no dry-run/help-only mode except `--help`. |
| `Tools/AppliedLorePageExporter.py` | Exports localized packet fields into publication Markdown pages and publication indexes. | Packet JSON/manifests; optional external article bodies referenced by packets; `graphs/RS084_SITE_WIKI_NAVIGATION_CLUSTERS_evidence_graph.csv`. | `Docs/Lore/AppliedContent/in_game_wiki/<locale>/*.md`; `external_site/<locale>/*.md`; `INDEX.md`; `Localization_Status_Index.md`; `Publication_Surface_Index.csv`; `Publication_Cluster_Index.csv`. | optional `--root ROOT`; optional `--overwrite`. | `python Tools/AppliedLorePageExporter.py --root . --overwrite` is write-capable; run only when generated packet source has changed and source audit proves publication frontmatter/index drift. |
| `Tools/AppliedLoreRouteCardExporter.py` | Exports checked route-card CSVs into DataMonolith Narrative route source table with route/phase/surface/packet hashes. | `Docs/Lore/AppliedContent/route_cards/*_route_cards.csv`; `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`. | `Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv`. | optional `--root ROOT`. | `python Tools/AppliedLoreRouteCardExporter.py --root .` is deterministic but write-capable; no dry-run/help-only mode except `--help`. |
| `Tools/AppliedLoreRuntimeAudit.py` | Offline validation of AppliedLore source, generated hashes, binding maps, route cards, publication pages, serialized bindings, world scene markers, and optional H8BIN records. | AppliedLore CSV/hash source, route cards, binding maps, publication pages, `02_HECTON_WORLD.unity`, prefabs, and `static_data.h8bin` when not `--source-only`. | Console audit line only; captured by this task to `runtime_audit_source_only.txt` and `runtime_audit_full.txt`. | optional `--root ROOT`; optional `--source-only`. | `python Tools/AppliedLoreRuntimeAudit.py --root . --source-only` is read-only and safe. Full audit is also read-only and was safe here. |

## Help Output Captured

- `python Tools/AppliedLoreImporter.py --help`
- `python Tools/AppliedLorePageExporter.py --help`
- `python Tools/AppliedLoreRouteCardExporter.py --help`
- `python Tools/AppliedLoreRuntimeAudit.py --help`

## Safe Command Decision - Task 05

- Run source-only audit: VERIFIED SAFE. It reads source/export artifacts and produced `runtime_audit_source_only.txt`.
- Run full offline audit: VERIFIED SAFE. It reads `static_data.h8bin` and produced `runtime_audit_full.txt`.
- Run importer: CANDIDATE SAFE, write-capable, owned outputs only. Run only with immediate diff inspection.
- Run route-card exporter: CANDIDATE SAFE, write-capable, owned output only. Run only with immediate diff inspection.
- Run page exporter: RUN AFTER POST-IMPORT AUDIT FAILURE. It has no dry-run, but source audit proved 170 generated frontmatter mismatches after importer output changed. `--overwrite` repaired generated page/index drift.
- Run Unity bake/build/editor placement: BLOCKED for this pass. Not required for static proof; would risk parallel-agent scene churn.
