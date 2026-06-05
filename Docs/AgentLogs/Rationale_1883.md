# Rationale 1883

Date: 2026-06-04
Evidence class: STATIC_DOC / STATIC_SOURCE

## Decisions

- Classified all surface normal-state roles as bright/readable only. Noir, storm, dirty foam, abyss, and depth materials are constrained to event/deep/cave/interior scopes.
- Treated first-party `MAT_H8_SurfaceCrestOcean_1428` as a credible candidate, not active proof, because current static prefab/scene evidence still routes Crest to `Assets/Crest/Crest/Materials/Ocean.mat`.
- Marked moon texture semantics `MISSING_SOURCE_REQUIRED` because material names exist but this audit did not prove concrete moon texture bindings and phase map roles.
- Marked `SargassumMicroFaunaBoids` as `PARTIAL_EXISTING_SOURCE_WITH_MISSING_MESH`: material/compute/script route exists, but prefab `boidMesh` is built-in plane and VAT textures are null.
- Did not edit source, assets, prefabs, scenes, meta files, binaries, or generated meshes.

## Scaling Rule

Compact/Middle/High/Ultra are documentation checkpoints for continuous `GlobalQualityWeight`. Compact must remain bright and beautiful; High/Ultra add sensory richness only.

## Proof Boundary

This task proves static source availability and risk classification only. It does not prove Unity import, active material binding, visual quality, runtime hidden state, frame cost, GC, or acceptance.
