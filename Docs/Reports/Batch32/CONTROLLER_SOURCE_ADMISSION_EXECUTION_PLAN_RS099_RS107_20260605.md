# Controller Source Admission Execution Plan RS099-RS107

Evidence class: STATIC_CONTROLLER_PLAN.
Runtime proof: absent.
Source admission proof: absent.
DataMonolith/h8bin proof: absent.
Publication proof: absent.

Mandates followed: QA_Evidence_Text_Filter_Audit; UI_Localization_Babel_RTL_FontSwap_ZeroAlloc; DATA_Runtime_Struct_Layout_ARM64; TOOL_Designer_Facades_CSV_Binary_Bridge.

Authority docs read: `authoring.md`, `data.md`, `quality.md`, `writing.md`, `narrative.md`, `localization.md`, `Docs/Lore/Lore_Content_System.md`, `Docs/Lore/Lore_Localization_Model.md`.

## Scope

This plan covers source-admission preparation for static source-candidate release sets RS099-RS107 only:

- RS099 P496-P499.
- RS100 P500-P502.
- RS101 P503-P505.
- RS102 P506-P508.
- RS103 P509-P511.
- RS104 P512-P514.
- RS105 P515-P517.
- RS106 P518-P520.
- RS107 P521-P523.

This plan does not admit rows, edit source CSV, generate pages, create route cards, bake h8bin, mutate Unity assets, or claim runtime readiness.

## Current Facts

- `Tools/AppliedLoreImporter.py` is the current packet-source importer.
- `Tools/AppliedLoreImporter.py::collect_packets()` consumes manifest `packet_sources` first; authoring-only manifests without `packet_sources` are skipped unless `canonical_importer_ready` is true and `canonical_importer_sources` exist.
- Read-only importer collection currently returns `collected_packets=464`.
- Read-only importer collection currently returns `scoped_P496_P523=0`.
- RS099-RS107 manifests currently have no `packet_sources`.
- RS099-RS107 manifests currently have no `canonical_importer_sources`.
- RS099-RS107 manifests have `canonical_importer_ready=false` and `runtime_ready=false`.
- RS099-RS107 packet bundles parse as static source candidates but are not importer-schema bundles.

Importer-required localized fields from `Tools/AppliedLoreImporter.py`:

- `title`
- `scanner`
- `terminal`
- `audio`
- `in_game_wiki`
- `external_site`
- `field_note`

Current missing importer-localized-field counts:

| Release set | Packet count | Missing importer localized fields |
|---|---:|---:|
| RS099 | 4 | 360 |
| RS100 | 3 | 270 |
| RS101 | 3 | 270 |
| RS102 | 3 | 270 |
| RS103 | 3 | 270 |
| RS104 | 3 | 270 |
| RS105 | 3 | 270 |
| RS106 | 3 | 270 |
| RS107 | 3 | 270 |

## Required Execution Order

1. Assign one explicit source/bake owner and freeze the RS099-RS107 scope.
2. Convert static surface-candidate bundles into canonical importer-schema JSON without changing packet IDs, article IDs, locale codes, source/draft status, or spoiler boundaries.
3. Validate every canonical importer candidate before any manifest points to it.
4. Add manifest `packet_sources` only after schema validation passes.
5. Run a no-write or review-only `collect_packets()` proof and confirm P496-P523 are collected exactly once.
6. Run `python Tools/AppliedLoreImporter.py --root .` only after source ownership accepts the generated CSV/hash diff.
7. Run `python Tools/AppliedLorePageExporter.py --root . --overwrite` only after importer output is accepted.
8. Create route-card source CSVs only after packet rows and hash constants exist.
9. Run `python Tools/AppliedLoreRouteCardExporter.py --root .` only after route cards reference known packet IDs.
10. Run `python Tools/AppliedLoreRuntimeAudit.py --root . --source-only` after source/page/route generation.
11. Run DataMonolith/h8bin bake only after source-only audit is green and the process gate is clean.
12. Run Unity/import/runtime proof only after h8bin proof exists and Unity ownership is assigned.

## Rejection Gates

- Reject direct source CSV edits without importer proof.
- Reject route-card creation before P496-P523 exist in generated packet CSV/hash constants.
- Reject runtime Markdown/JSON parsing.
- Reject `canonical_importer_ready=true` while importer schema is incomplete.
- Reject generated page, h8bin, DataMonolith, Unity placement, or publication readiness claims from static bundles.
- Reject non-English native-readiness claims. All non-English rows remain `draft_machine_or_llm` until native review, RTL/CJK/font/layout checks, source extraction, and runtime proof exist.
- Reject source admission while Unity/build/import processes are active or while no explicit source/bake owner exists.

## Proof Packet Required From Future Owner

- Manifest diff showing exactly which RS099-RS107 manifests gained `packet_sources`.
- Canonical importer-schema JSON path list.
- JSON parse proof.
- Importer field completeness proof for every packet and locale.
- Read-only collection proof: total packet count and scoped P496-P523 count.
- Source CSV/hash diff summary after importer.
- Page exporter diff summary after page export.
- Route-card source CSV diff and route-card exporter proof, if route cards are included.
- `Tools/AppliedLoreRuntimeAudit.py --root . --source-only` output.
- DataMonolith/h8bin bake proof only if the owner proceeds beyond source admission under a clean process gate.
- Unity/import/runtime proof only after h8bin proof exists.

## Low / Middle / High / Ultra Consequence

No runtime path changes in this plan.

Future runtime consequence must be presentation-density only:

- Low/Compact: one safe label, one next-proof hint, or one suppression reason per evidence object.
- Middle: add source voice, confidence state, and held proof class where layout permits.
- High: add relation filters and evidence-family browsing.
- Ultra: add dense comparison panes and unlock-history panels.

These lanes must not change packet IDs, DTO layout, source status, save identity, route authority, spoiler truth, or native-review state.

## Boundary

This is a static execution plan. It is not source admission, importer execution, route-card wiring, generated-page export, h8bin bake, DataMonolith payload, Unity placement, runtime string-pool extraction, native localization review, public website publication, wiki publication, or player-build proof.
