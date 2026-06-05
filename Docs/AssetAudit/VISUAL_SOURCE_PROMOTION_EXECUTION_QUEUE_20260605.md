# Visual Source Promotion Execution Queue - 2026-06-05

Status: `PENDING_VERIFICATION / STATIC_SOURCE_QUEUE_ONLY`

Evidence class: `STATIC_IMAGE_QA + STATIC_SOURCE + STATIC_DOC`

CSV companion: `Docs/AssetAudit/VISUAL_SOURCE_PROMOTION_EXECUTION_QUEUE_20260605.csv`

## Scope

This queue translates the mandatory visual references and current source coverage into owner actions. It does not accept any asset, import setting, material, prefab, scene, Crest route, screenshot, Frame Debugger row, memory number, or runtime behavior.

Inputs used:

- `Docs/Reports/Batch32/CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md`
- `Docs/AssetAudit/VISUAL_REFERENCE_OWNER_REQUIREMENT_MATRIX_20260605.csv`
- `Docs/AssetAudit/VISUAL_REFERENCE_PATH_CONTINUITY_20260605.csv`
- `Docs/AssetAudit/VISUAL_HERO_SOURCE_COVERAGE_MATRIX_20260605.csv`
- `Docs/AssetAudit/GENERATED_SOURCE_PACK_FILE_INVENTORY_20260605.csv`
- `Docs/AssetAudit/TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.csv`
- `Docs/AssetAudit/BATCH31_CHANNEL_SEMANTICS_DECISION_QUEUE_20260605.csv`
- `Docs/AssetAudit/FOAM_CONTACT_SOURCE_ROLE_DECISION_QUEUE_20260605.csv`
- Latest rejected diagnostic proof context from `Docs/Orchestration/VISUAL_FRONT_P0_P1_SYNTHESIS_20260605.md`

## Findings

- The current diagnostic surface screenshot is rejected: slab water, black detached shoreline underside, rectangular material patch, weak terrain material truth, and pasted/toy Aegir.
- Mandatory references require bright, readable surface/ocean/sky/coast and photic shallows. Darkness, haze, bloom, or green overlays cannot hide weak surface art.
- Generated and cleanup source packs are source-only. They become useful only through import-role proof, material slot readback, route screenshot proof, Frame Debugger/Stats, memory proof, and owner acceptance.
- The visual work is blocked less by missing ideas and more by missing route proof. Future owners need execution gates, not more loose inspiration notes.

## Queue Summary

| Queue | Priority | Route | Owner route |
|---|---|---|---|
| VSPQ-01 | P0 | Surface sky/Aegir/coast | `ASSET_OWNER_14`; `ASSET_OWNER_16`; `ASSET_OWNER_20`; `ASSET_OWNER_36` |
| VSPQ-02 | P0 | Waterline foam/contact | `ASSET_OWNER_11`; `ASSET_OWNER_20`; `ASSET_OWNER_36` |
| VSPQ-03 | P0 | Photic terrain PBR | `ASSET_OWNER_16`; `ASSET_OWNER_20`; `ASSET_OWNER_36` |
| VSPQ-04 | P0 | Visible proxy placeholder purge | `ASSET_OWNER_12`; `ASSET_OWNER_22`; `ASSET_OWNER_36` |
| VSPQ-05 | P1 | Kelp/coral density | `ASSET_OWNER_12`; `ASSET_OWNER_22`; `ASSET_OWNER_27`; `ASSET_OWNER_36` |
| VSPQ-06 | P1 | HUD oxygen and cockpit/product-face readability | `ASSET_OWNER_17`; `ASSET_OWNER_24`; `ASSET_OWNER_25`; `ASSET_OWNER_36` |
| VSPQ-07 | P1 | Deep bioluminescent route | `ASSET_OWNER_12`; `ASSET_OWNER_16`; `ASSET_OWNER_27`; `ASSET_OWNER_36` |
| VSPQ-08 | P1 | Capsule/base medium-depth product route | `ASSET_OWNER_24`; `ASSET_OWNER_25`; `ASSET_OWNER_27`; `ASSET_OWNER_36` |
| VSPQ-09 | P1 | Water ceiling shimmer and receiver response | `ASSET_OWNER_20`; `ASSET_OWNER_16`; `ASSET_OWNER_36` |
| VSPQ-10 | P2 | Generated source-pack boundary | `ASSET_OWNER_02`; `ASSET_OWNER_09`; `ASSET_OWNER_15`; `ASSET_OWNER_16`; `ASSET_OWNER_20`; `ASSET_OWNER_36` |

## Hard Rejection Gates

- Reject any screenshot using diagnostic/editor-mutating `H8VisualProofCapture1912` probe output as product acceptance.
- Reject source-pack/contact-sheet promotion without Unity import role, material slot, Addressables/residency where relevant, and route screenshot proof.
- Reject Crest material clones, wrappers, or blind material assignment.
- Reject active visible `WorldProceduralProxy` route content in proof screenshots.
- Reject dark, green, fogged, bloomed, or cropped surface proof that hides weak water, terrain, sky, Aegir, or shoreline art.

## Low / Middle / High / Ultra Consequences

- Low: keep bright water, readable Aegir/sky, material identity, wet edge, silhouettes, and HUD state. Reduce residency/cadence only through owner-approved continuous `GlobalQualityWeight` routes.
- Middle: use route-owned PBR stacks, conservative foam/contact masks, stable LOD, and readable cockpit/HUD once material/import proof exists.
- High: spend budget on richer detail normals, wetness masks, sky/Aegir layering, denser near-field dressing, and stronger water receiver response.
- Ultra: add capture-grade density and overdetail only after the same owner route passes memory, Frame Debugger, Stats, and route screenshot proof. Do not change gameplay truth ownership or asset authority.

## Regression Model

- CPU: static documentation only; no runtime CPU claim.
- GC: no runtime code touched; no `0 B/frame` claim.
- Memory/VRAM: source candidates are not residency proof. Future promotion must prove texture memory and Addressables ownership.
- Cadence: no runtime cadence changed.
- Correctness: future owners now have exact rejection gates and proof requirements for visual source promotion. Product visual status remains blocked by Unity/runtime proof.

Final status: `PENDING_VERIFICATION`.
