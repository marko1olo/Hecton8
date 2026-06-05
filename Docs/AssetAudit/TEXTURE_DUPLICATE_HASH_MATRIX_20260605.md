# Texture Duplicate Hash Matrix - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_HASH_SCAN` + `STATIC_IMAGE_PROBE`.
Scope: image rows from `TEXTURE_FILE_TECHNICAL_PROPERTIES_20260605.csv` under `Assets/_Project`.

This file is not deletion authorization, Unity import proof, material binding proof, visual acceptance, or runtime proof. Exact hash duplicates prove byte equality only; same basename and family groups are route-review hints only.

CSV companion: `Docs/AssetAudit/TEXTURE_DUPLICATE_HASH_MATRIX_20260605.csv`.

## Summary

- Texture rows scanned: `140`.
- Exact hash duplicate groups: `3`.
- Exact hash duplicate rows: `6`.
- Same-basename groups: `3`.
- Normalized family-name groups: `18`.

## Use

- Use exact hash groups to find redundant source candidates before import or Addressables planning.
- Use same-basename and family-name groups to find Aegir/cloud/prologue, PBR-stack, terrain/geology, and generated/source-route duplication risk.
- Do not delete or move files from this matrix without Unity material/prefab/scene reference readback and owner approval.

## Required Future Gates

- Resolve texture GUID users through Unity and material readback.
- Confirm route owner, import role, Addressables group/key, and material slot for each candidate.
- Compare visual result in route screenshots before any source consolidation.
- Preserve GUIDs or update references only through Unity-safe paths.

## Rollback Conditions

- Consolidation removes a referenced texture, breaks a material slot, changes GUID identity, or damages visual floor.
- Same-basename/family review is treated as proof of pixel equivalence.
- Source-only/generated pack rows are promoted without cleaned PBR/channel proof.

## Continuous GlobalQualityWeight Consequences

- Low/compact: use duplicate knowledge to reduce residency pressure while preserving material identity and route readability.
- Middle: keep route-owned PBR stacks and stable mip behavior.
- High: spend saved memory on better detail maps, Aegir/cloud response, terrain breakup, and organic material detail.
- Ultra: extend hero texture residency and material layering after measured proof. Ownership and gameplay truth do not change.

## Regression Model

- CPU: static hash scan only.
- GC: no runtime code touched.
- Memory/VRAM: duplicate source bytes are mapped; resident memory is unproven.
- Cadence: no runtime cadence changed.
- Correctness: deletion/import decisions remain blocked by Unity readback and owner proof.

Final status: `PENDING VERIFICATION`.
