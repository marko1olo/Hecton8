# Controller Source Admission Blocker Map RS099-RS104

Evidence class: STATIC_CONTROLLER_SYNTHESIS.
Runtime proof: absent.
Native localization proof: absent.
DataMonolith/h8bin proof: absent.
Publication proof: absent.

## Scope

Reviewed static source-candidate release sets:

- RS099_PUBLIC_EVIDENCE_GOVERNANCE_BRIDGE
- RS100_PUBLIC_EVIDENCE_CLEANUP_CONFLICT_BRIDGE
- RS101_COUNTER_INDEX_ALIAS_HOLD_BRIDGE
- RS102_PROOF_ORDER_RELATION_RECEIPT_BRIDGE
- RS103_CUSTODY_DOWNGRADE_REVIEW_BRIDGE
- RS104_DISPUTE_HOLD_CHECKLIST_BRIDGE

## Current State

| Release set | Packet scope | Current evidence | Admission blocker |
|---|---|---|---|
| RS099 | P496-P499 | STATIC_SOURCE candidate | No explicit source/bake owner; no source CSV insertion; no route-card mapping; no generated-page export; no h8bin bake; no Unity/runtime proof. |
| RS100 | P500-P502 | STATIC_SOURCE candidate | No explicit source/bake owner; no source CSV insertion; no route-card mapping; no generated-page export; no h8bin bake; no Unity/runtime proof. |
| RS101 | P503-P505 | STATIC_SOURCE candidate | No explicit source/bake owner; no source CSV insertion; no route-card mapping; no generated-page export; no h8bin bake; no Unity/runtime proof. |
| RS102 | P506-P508 | STATIC_SOURCE candidate | No explicit source/bake owner; no source CSV insertion; no route-card mapping; no generated-page export; no h8bin bake; no Unity/runtime proof. |
| RS103 | P509-P511 | STATIC_SOURCE candidate | No explicit source/bake owner; no source CSV insertion; no route-card mapping; no generated-page export; no h8bin bake; no Unity/runtime proof. |
| RS104 | P512-P514 | STATIC_SOURCE candidate | No explicit source/bake owner; no source CSV insertion; no route-card mapping; no generated-page export; no h8bin bake; no Unity/runtime proof. |

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

Required before native/public lock:

- Arabic/Hebrew RTL direction, punctuation, and UI fit proof.
- Japanese/Korean/Chinese font coverage and line-break proof.
- Expansion review for German, Dutch, Polish, Portuguese, Spanish, French, Indonesian, Russian, and Ukrainian.
- Legal/archive terminology review for claimant-safe, custody, hold, dispute, receiver, proof order, and route alias language.

## Runtime Boundary

Runtime must not parse Markdown or these JSON source candidates. Runtime may only consume approved baked string pools or binary source data after the source/bake gate produces a validated output.

## Risk Model

CPU/GC/memory: no runtime code changed by this map.

Cadence: no runtime cadence changed.

Correctness: the main risk is evidence-class drift from STATIC_SOURCE into importer/runtime claims. This map keeps the blocker list explicit.

Failure mode: a later worker may cite RS099-RS104 as source admission. That is rejected unless the gate above is completed with fresh proof artifacts.
