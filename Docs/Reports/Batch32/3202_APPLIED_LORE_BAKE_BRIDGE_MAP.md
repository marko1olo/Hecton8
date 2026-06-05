# 3202 AppliedLore Bake Bridge Map

Status: BLOCKED BY SOURCE OWNERSHIP / STATIC_SOURCE.

First-20 route blocker removed: this report prevents P461/P462/P463 first-hour custody, PressureSeal bridge, and public/wiki spoiler-gate bridge packets from entering authoritative route cards before canonical AppliedLore source rows exist.

## Controller Addendum After 3202 Completion

Controller integrated two additional RS093 authoring packets after this worker report was written:

- `P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE`
- `P464_BLACK_KEEL_CLAIM_WINDOW_BRIDGE`

The same source-order rule applies to P461/P462/P463/P464. They are production Markdown authoring packets only until canonical packet JSON, generated packet CSV rows, generated hash constants, publication index rows, and source validation accept them.

Do not create `RS093_route_cards.csv` for any of the four packets until all referenced packet IDs exist in canonical AppliedLore source/export rows.

Mandates followed:
- QA_Evidence_Text_Filter_Audit.txt
- UI_Localization_Babel_RTL_FontSwap_ZeroAlloc.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- TOOL_Designer_Facades_CSV_Binary_Bridge.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt

Authority read:
- AGENTS.md
- PROJECT_BIBLES.md
- TASTE.md
- VISION_LOCKS.md
- authoring.md
- data.md
- localization.md
- writing.md
- narrative.md
- quality.md
- Docs/Lore/Canon_Locks.md
- Docs/Lore/Lore_Bible.md
- Docs/Lore/Lore_Content_System.md
- Docs/Lore/Lore_Localization_Model.md
- Docs/Lore/Lore_Multilingual_Content_Architecture.md

## Source-Order Map

1. Production packet Markdown:
   - Files: `Docs/Lore/AppliedContent/production_packets/P461_PACKET_CUSTODY_BRIDGE.production.md`, `Docs/Lore/AppliedContent/production_packets/P462_PRESSURE_SEAL_FIRST_REPAIR_BRIDGE.production.md`, `Docs/Lore/AppliedContent/production_packets/P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE.production.md`.
   - Current evidence: STATIC_DOC only. These files contain packet briefs and locale sections, but they are not canonical importer packet objects.

2. Canonical packet JSON:
   - Expected first safe source file: `Docs/Lore/AppliedContent/packets/RS093_LORE_SYSTEM_INTEGRATION_BRIDGE.packets.json`.
   - Required manifest route: `Docs/Lore/AppliedContent/release_sets/RS093_LORE_SYSTEM_INTEGRATION_BRIDGE_manifest.json` must expose that JSON through `packet_sources`.
   - Current blocker: RS093 has `packets` but no `packet_sources`; it has `canonical_importer_sources: []` and `authoring_packet_sources` pointing at Markdown. `Tools/AppliedLoreImporter.py::collect_packets()` ignores both of those fields.
   - Required JSON schema evidence: existing packet bundles use `schema`, `release_set_id`, `runtime_contract`, `packets[]`, and per-packet `localized[locale]` fields for `title`, `scanner`, `terminal`, `audio`, `in_game_wiki`, `external_site`, and optional `field_note`.

3. Packet source CSV and generated hashes:
   - Owner tool: `Tools/AppliedLoreImporter.py`.
   - Command after canonical JSON is complete: `python Tools/AppliedLoreImporter.py --root .`
   - Generated files: `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv` and `Assets/_Project/Scripts/Core/Generated/H8AppliedLoreHashes.cs`.
   - Row order: importer sorts packet objects by `packet_id`; for each packet it emits `TARGET_LOCALES` order.
   - Current blocker: P461/P462/P463 are absent from generated CSV and hash constants.

4. Page export:
   - Owner tool: `Tools/AppliedLorePageExporter.py`.
   - Command after importer succeeds: `python Tools/AppliedLorePageExporter.py --root . --overwrite`
   - Generated outputs: `Docs/Lore/AppliedContent/in_game_wiki/<locale>/<packet>.md`, `Docs/Lore/AppliedContent/external_site/<locale>/<packet>.md`, `Publication_Surface_Index.csv`, `Publication_Cluster_Index.csv`, and localization status index.
   - Current blocker: exporter also consumes `collect_packets()`, so it will fail until RS093 has canonical `packet_sources`.

5. Route card source:
   - Candidate source file: `Docs/Lore/AppliedContent/route_cards/RS093_route_cards.csv`.
   - Owner tool: `Tools/AppliedLoreRouteCardExporter.py`.
   - Command only after P461/P462/P463 source rows exist and source audit accepts them: `python Tools/AppliedLoreRouteCardExporter.py --root .`
   - Generated output: `Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv`.
   - Current state: `RS093_route_cards.csv` and `.meta` are absent. This is correct.

6. Static data h8bin:
   - Owner tool: `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs`.
   - Editor menu: `Hecton8/Data Monolith/Bake Static Data`.
   - Batch method found: `Hecton8.EditorValidation.H8DataMonolithCompiler.BakeFromCommandLine`.
   - Output: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
   - This task did not run Unity, did not bake, and did not edit `static_data.h8bin`.

## First Safe Insertion Point

First safe insertion is not `applied_lore_packets.csv` and not `RS093_route_cards.csv`.

The first safe insertion point is a canonical packet bundle:

`Docs/Lore/AppliedContent/packets/RS093_LORE_SYSTEM_INTEGRATION_BRIDGE.packets.json`

Then update the RS093 manifest to include:

```json
"packet_sources": [
  "Docs/Lore/AppliedContent/packets/RS093_LORE_SYSTEM_INTEGRATION_BRIDGE.packets.json"
]
```

Do not remove `authoring_packet_sources` unless the controller wants Markdown custody removed. It is harmless as documentation, but it is not importer input.

## Blocker

No source-only patch was made.

Reason:
- The current Markdown packets do not provide a complete importer-ready per-locale field matrix. The importer requires `title`, `scanner`, `terminal`, `audio`, `in_game_wiki`, `external_site`, and optional `field_note` for every locale. P461/P462/P463 Markdown locale rows are production-facing packet notes, not full canonical JSON rows.
- A no-write `collect_packets()` check fails because RS093 declares P461/P462/P463 in `packets` without canonical packet source objects.
- A fresh full source-only audit currently fails before RS093 acceptance on an existing P002 Portuguese mojibake leakage in generated/page outputs: `csv:P002_BLACK_KEEL_CONTACT/pt_BR/terminal`, `Docs/Lore/AppliedContent/in_game_wiki/pt_BR/P002_BLACK_KEEL_CONTACT.md`, and `Docs/Lore/AppliedContent/external_site/pt_BR/P002_BLACK_KEEL_CONTACT.md`.

Until those blockers are fixed, adding `RS093_route_cards.csv` would reproduce the earlier unknown-packet failure.

## DTO And Runtime Layout Implications

Adding P461/P462/P463 to canonical packet JSON would add 45 packet-locale CSV rows before bake: 3 packets x 15 locales.

Expected generated changes after importer:
- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`: +45 rows.
- `Assets/_Project/Scripts/Core/Generated/H8AppliedLoreHashes.cs`: +3 packet hash constants.

Expected generated changes after page export depend on `surface_mask`, but at minimum they add publication pages/index rows for the enabled publication surfaces.

Expected generated changes after route-card export, if RS093 route cards are later added:
- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv`: +3 route rows for RC495/RC496/RC497 candidates if accepted.

No runtime DTO layout change is implied by these source additions. Existing records stay:
- `H8AppliedLorePacketRecord`: 128 bytes by audit constant.
- `H8AppliedLoreRouteRecord`: 128 bytes by audit constant.

Evidence remains STATIC_SOURCE. Unity/Burst/IL2CPP/player/runtime layout proof was not run.

## Validation Evidence

Claim: RS093 manifest parses and declares three packets.
Evidence class: STATIC_SOURCE.
Command: JSON parse script over `Docs/Lore/AppliedContent/release_sets/RS093_LORE_SYSTEM_INTEGRATION_BRIDGE_manifest.json`.
Output: `manifest schema=H8.APPLIED_LORE_RELEASE_SET.V0 packets=3 canonical_importer_sources=0 authoring_packet_sources=3 canonical_importer_ready=False runtime_ready=False`.

Claim: Current generated CSVs parse.
Evidence class: STATIC_SOURCE.
Command: CSV parse script over packet CSV, route-card export, and publication indexes.
Output:
- `applied_lore_packets.csv rows=6900 columns=16`
- `applied_lore_route_cards.csv rows=454 columns=19`
- `Publication_Surface_Index.csv rows=13050 columns=13`
- `Publication_Cluster_Index.csv rows=150 columns=21`

Claim: P461/P462/P463 are not canonical packet rows.
Evidence class: STATIC_SOURCE.
Command: structured read of `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`.
Output: `P461_PACKET_CUSTODY_BRIDGE generated_packet_csv=NO`; `P462_PRESSURE_SEAL_FIRST_REPAIR_BRIDGE generated_packet_csv=NO`; `P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE generated_packet_csv=NO`.

Claim: P461/P462/P463 are not generated hash constants, publication index rows, or route export rows.
Evidence class: STATIC_SOURCE.
Command: structured text scan of `H8AppliedLoreHashes.cs`, publication indexes, and `applied_lore_route_cards.csv`.
Output: all three packet IDs reported `generated_hash=NO surface_index=NO cluster_index=NO route_export=NO`.

Claim: `RS093_route_cards.csv` remains absent.
Evidence class: STATIC_SOURCE.
Command: `Test-Path Docs/Lore/AppliedContent/route_cards/RS093_route_cards.csv`; `Test-Path Docs/Lore/AppliedContent/route_cards/RS093_route_cards.csv.meta`
Output: `False`, `False`.

Claim: current importer collection fails at missing RS093 packet sources.
Evidence class: STATIC_SOURCE.
Command: no-write Python import of `AppliedLoreImporter.collect_packets(Path('.').resolve())`.
Output: `ValueError: Manifest packet ids missing from sources: P461_PACKET_CUSTODY_BRIDGE, P462_PRESSURE_SEAL_FIRST_REPAIR_BRIDGE, P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE`.

Claim: source-only audit is not green.
Evidence class: STATIC_SOURCE.
Command: `python Tools/AppliedLoreRuntimeAudit.py --root . --source-only`
Output: `AppliedLore audit FAILED: Player-visible localization mojibake marker leaked: csv:P002_BLACK_KEEL_CONTACT/pt_BR/terminal; page:Docs/Lore/AppliedContent/in_game_wiki/pt_BR/P002_BLACK_KEEL_CONTACT.md; page:Docs/Lore/AppliedContent/external_site/pt_BR/P002_BLACK_KEEL_CONTACT.md`.

Claim: all three production Markdown packets contain all 15 locale sections and only route-card candidates.
Evidence class: STATIC_DOC.
Command: Markdown section scan for `### <locale>` and `route-card candidate` lines.
Output:
- `P461_PACKET_CUSTODY_BRIDGE locale_sections=15/15`; candidate `RC495_P461_PACKET_CUSTODY_BRIDGE`.
- `P462_PRESSURE_SEAL_FIRST_REPAIR_BRIDGE locale_sections=15/15`; candidate `RC496_P462_PRESSURE_SEAL_FIRST_REPAIR_BRIDGE`.
- `P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE locale_sections=15/15`; candidate `RC497_P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE`.

Claim: earlier 34-second audit attempt timed out.
Evidence class: STATIC_SOURCE.
Command: `python Tools/AppliedLoreRuntimeAudit.py --root . --source-only` with 30000 ms timeout.
Output: command timed out after 34017 ms.

## Next Correct Work

1. Create canonical RS093 packet JSON bundle with full importer schema for all three packets and all 15 locales.
2. Add that file to RS093 manifest `packet_sources`.
3. Run no-write `collect_packets()` check until it passes.
4. Run `python Tools/AppliedLoreImporter.py --root .` only after source ownership is accepted.
5. Run `python Tools/AppliedLorePageExporter.py --root . --overwrite`.
6. Fix the existing source-only audit frontmatter blocker or assign it to the owning localization/page-export agent.
7. Add `RS093_route_cards.csv` only after P461/P462/P463 exist in generated packet CSV/hash/page exports and source-only audit is green for source ownership.
8. Run `python Tools/AppliedLoreRouteCardExporter.py --root .`.
9. Only then run Unity/DataMonolith bake through `Hecton8/Data Monolith/Bake Static Data` or `Hecton8.EditorValidation.H8DataMonolithCompiler.BakeFromCommandLine`.

Runtime readiness remains PENDING VERIFICATION.
