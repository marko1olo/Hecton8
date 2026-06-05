# Batch31 Channel Semantics Decision Queue - 2026-06-05

Status: `PENDING_VERIFICATION / STATIC_IMAGE_QA_ONLY`.
Evidence class: `STATIC_SOURCE + STATIC_IMAGE_QA`.
Unity import: absent.
Material binding: absent.
Visual acceptance: absent.
Runtime proof: absent.

CSV companion: `Docs/AssetAudit/BATCH31_CHANNEL_SEMANTICS_DECISION_QUEUE_20260605.csv`.

## Scope

This queue separates usable Batch31 albedo/normal source candidates from blocked packed-mask candidates. It is based on static manifests plus manual contact-sheet inspection of:

- `Docs/GeneratedAssets/Batch31_LocalPBR/Batch31_LocalPBR_contact_sheet.png`
- `Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/Batch31_PromotionPrep_contact_sheet.png`
- `Docs/AssetAudit/BATCH31_LOCAL_PBR_IMPORT_INTENT_20260605.md/.csv/.json`
- `Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/Batch31_PromotionPrep_static_QA.json`
- the three per-package `*_PROMOTION_MANIFEST.md/.json` files.

This artifact is not permission to import textures into Unity. It is an owner decision queue for terrain/geology, shoreline, ocean-contact, and material-route owners.

## Findings

- `TX_B31_WetBasaltShoreline_1429` has the strongest material identity for wet shoreline rock, but it carries macro-repeat and shoreline-only risk. It cannot be used as broad terrain fill without scale and 2x2 seam proof.
- `TX_B31_PhoticSeabedSubstrate_2102` is usable as a shallow seabed candidate, but it needs tile scale, shell/debris readability, and baked-shadow review.
- `TX_B31_PhoticShellSandSubstrate_2102` is bright and route-appropriate for photic shallows, but it overlaps with the seabed candidate and needs a distinct biome/material role before both are promoted.
- All three packed-mask candidates remain blocked. The manifests describe generated MRAO-style candidates, while the production `_MaskMap` route expects an explicit ARM target or a deliberate MRAO decoder target.
- Promotion-prep root files are real non-empty files, but they remain inspection/prep evidence only. They are not import proof, material proof, texture residency proof, or visual acceptance.

## Required Owner Decision

Before Unity promotion, the owner must choose one route:

- `ARM_REPACK`: repack to production ARM `R=AO G=Roughness B=Metallic` and prove the target material/shader layout, including serialized layout fields where required.
- `MRAO_TARGET`: deliberately bind to a shader route that decodes MRAO and prove that route with material readback.

Importing any `MRAOSource`, `PROMO_MRAO_Candidate`, or `_MaskMap` by filename alone is rejected.

## Low / Middle / High / Ultra Consequences

- Low/compact: admit only one proven shoreline/photic material at a time; protect memory with mips, BC7/BC5, and no unresolved mask imports.
- Middle: use albedo/normal candidates after seam proof and owner-approved mask semantics.
- High: spend visual budget on stronger normal/detail layering and shoreline/photic variation after route screenshots pass.
- Ultra: richer near-field material layering is allowed only after mask semantics, material layout, sampler count, residency, and visual-reference proof are stable.

## Regression Model

- CPU: static document and image review only; no runtime CPU change.
- GPU: no shader/material import or sampler route changed.
- GC: no runtime code touched; no `0 B/frame` claim.
- Memory/VRAM: no texture imported or resident.
- Correctness: queue blocks false promotion of MRAO-style masks as production `_MaskMap` inputs.
- Visual: contact sheets are source QA only. Surface, shoreline, photic terrain, and medium-depth route acceptance still require Unity screenshots against mandatory references.

Final status: `PENDING_VERIFICATION`.
