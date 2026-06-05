# Controller Source Admission Blocker Map RS099-RS108

Evidence class: STATIC_CONTROLLER_SYNTHESIS.
Runtime proof: absent.
Native localization proof: absent.
DataMonolith/h8bin proof: absent.
Publication proof: absent.

Mandates followed: QA_Evidence_Text_Filter_Audit; UI_Localization_Babel_RTL_FontSwap_ZeroAlloc; DATA_Runtime_Struct_Layout_ARM64; TOOL_Designer_Facades_CSV_Binary_Bridge.

## Scope

Reviewed static source-candidate release sets:

- RS099_PUBLIC_EVIDENCE_GOVERNANCE_BRIDGE through RS108_REVIEW_TEMPLATE_LINK_AUDIT_BRIDGE.

## Current State

| Release set | Packet scope | Current evidence | Admission blocker |
|---|---|---|---|
| RS099 | P496-P499 | STATIC_SOURCE candidate | No explicit source/bake owner; no source CSV insertion; no route-card mapping; no generated-page export; no h8bin bake; no Unity/runtime proof. |
| RS100 | P500-P502 | STATIC_SOURCE candidate | Same blocker set as RS099. |
| RS101 | P503-P505 | STATIC_SOURCE candidate | Same blocker set as RS099. |
| RS102 | P506-P508 | STATIC_SOURCE candidate | Same blocker set as RS099. |
| RS103 | P509-P511 | STATIC_SOURCE candidate | Same blocker set as RS099. |
| RS104 | P512-P514 | STATIC_SOURCE candidate | Same blocker set as RS099. |
| RS105 | P515-P517 | STATIC_SOURCE candidate | Same blocker set as RS099. |
| RS106 | P518-P520 | STATIC_SOURCE candidate | Same blocker set as RS099. |
| RS107 | P521-P523 | STATIC_SOURCE candidate | Same blocker set as RS099; P521-P523 non-English rows are ASCII-safe machine drafts pending native replacement/review. |
| RS108 | P524-P526 | STATIC_SOURCE candidate | Same blocker set as RS099; P524-P526 non-English rows are ASCII-safe machine drafts pending native replacement/review. |

## Required Gate Before Admission

1. Assign an explicit source/bake owner with write scope.
2. Freeze the packet range for that owner.
3. Regenerate or validate source CSV rows under the authoring bridge.
4. Generate route-card mapping with packet IDs, article IDs, LocID roots, spoiler level, and surface set.
5. Generate page/source hashes.
6. Run source static validation.
7. Only after that, run importer/bake to h8bin under the approved data bridge.
8. Only after h8bin exists, run Unity/import/runtime proof before runtime readiness language.

## Native Localization Blockers

All non-English rows remain `draft_machine_or_llm`.

P521-P526 use ASCII-safe machine draft rows. They require native replacement or review before any player-facing non-English release.

## Runtime Boundary

Runtime must not parse Markdown or these JSON source candidates. Runtime may only consume approved baked string pools or binary source data after the source/bake gate produces a validated output.

## Risk Model

CPU/GC/memory: no runtime code changed by this map.

Cadence: no runtime cadence changed.

Correctness: the main risk is evidence-class drift from STATIC_SOURCE into importer/runtime claims. This map keeps the blocker list explicit.

Failure mode: a later worker may cite RS099-RS108 as source admission. That is rejected unless the gate above is completed with fresh proof artifacts.
