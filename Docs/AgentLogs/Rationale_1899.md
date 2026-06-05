# Rationale 1899

Evidence class: STATIC_SOURCE
Runtime proof: PENDING UNITY

## Decisions

- Kept `WorldProceduralOrganicMiscFinalAuthoring.RebuildOrganicMiscFinals` blocked. Reason: targeted source shows it creates visible `PrimitiveType` sphere/cylinder/capsule meshes through `GameObject.CreatePrimitive`.
- Required a new OrganicMisc-specific generator or DCC route. Reason: seaweed/coral builders are useful source patterns, but 1858 classifies existing flora/geology packages as candidates without manifests/proof, not accepted final OrganicMisc replacements.
- Required stable vertex color semantics: R=sway, G=biolum, B=AO/cavity, A=family mask. Reason: `3dmodel.md` and `3DMODEL_FLORA_CORAL.md` mandate channel semantics and shader consumption without runtime per-vertex gameplay computation.
- Required hidden `ANCHOR_*` and `COL_*` routes. Reason: gameplay truth must remain separate from visible art; LOD0 visual mesh collision is rejected by `3dmodel.md`.
- Kept all proof claims static. Reason: task forbade Unity/import/build/PlayMode/profiler/screenshots; QA mandate forbids upgrading text evidence to runtime or visual proof.

## Residual Risk

- Contract quality is static only. Future generator implementation may expose missing shader/material constraints once Unity import and screenshots are allowed.
- Existing reports disagree on total audit package counts because 1851 and 1858 reflect different audit snapshots. This contract uses both only as static evidence for the OrganicMisc primitive defects and missing proof requirements.
