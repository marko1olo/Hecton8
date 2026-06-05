# Model File Import Risk Matrix - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_SOURCE_META_SCAN`.
Scope: model-like source files under `Assets/_Project`.

This file is not Unity importer readback, polycount proof, LOD proof, collider proof, material proof, visual acceptance, or runtime proof. File size and `.meta` text do not prove model quality.

CSV companion: `Docs/AssetAudit/MODEL_FILE_IMPORT_RISK_MATRIX_20260605.csv`.

## Summary

- Model source files scanned: `16`.
- Extensions: `fbx=16`.
- Static read/write-on flags: `0`.
- Mesh compression off/unknown flags: `16`.
- Import animation-on flags: `16`.
- Material import route flags: `9`.
- Generate-collider flags: `0`.

## Required Future Unity Gates

- Read back importer settings through Unity APIs: read/write, mesh compression, normals/tangents, scale, animation import, material import, and collider generation.
- Read mesh/poly/LOD/collider data from prefab or imported asset, not from file size.
- Confirm authored LODs, collision proxies, material slots, bounds, pivots, sockets, and route placement before promotion.
- Capture route screenshots, Stats, Frame Debugger, profiler, and memory after any import/prefab/material edit.

## Rollback Conditions

- Import edits change scale, pivots, sockets, anchors, collider truth, material identity, Addressables identity, or prefab GUID route without owner proof.
- Read/write stays enabled without CPU-read owner.
- Visual mesh is used as production MeshCollider truth.
- LOD or material proof fails surface/shallow/medium-depth visual floor.

## Continuous GlobalQualityWeight Consequences

- Low/compact: keep proven silhouettes, baked material identity, stable collider proxies, and dithered LOD; reduce density/residency smoothly.
- Middle: maintain product-face material identity and stable LOD transition bands.
- High: extend LOD residency and spend budget on richer bevels, trim, wear, detail normals, and route dressing.
- Ultra: increase near-field detail and material response after measured proof. Prefab identity, collider authority, and gameplay truth do not change.

## Regression Model

- CPU: future risks are renderer count, LOD evaluation, collider count, and import-time processing. Static scan makes no CPU claim.
- GC: no runtime code touched.
- Memory/VRAM: file size and meta flags are not resident memory.
- Cadence: no runtime cadence changed.
- Correctness: importer source risk is mapped only; Unity readback remains required.

Final status: `PENDING VERIFICATION`.
