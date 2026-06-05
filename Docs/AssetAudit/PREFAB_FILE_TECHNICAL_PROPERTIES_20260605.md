# Prefab File Technical Properties - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_YAML_SCAN`.
Scope: prefab source files under `Assets/_Project/Prefabs`.

This file is not Unity prefab readback, scene instance proof, mesh quality proof, material binding proof, collider authority proof, Addressables proof, runtime proof, or visual acceptance. It only records static YAML token risk.

CSV companion: `Docs/AssetAudit/PREFAB_FILE_TECHNICAL_PROPERTIES_20260605.csv`.

## Summary

- Prefab files scanned: `602`.
- Folder count: `40`.
- Prefabs without static `LODGroup` token: `221`.
- Prefabs with built-in primitive mesh refs: `183`.
- Prefabs with `MeshCollider` token: `76`.
- Prefabs with direct `AudioClip` refs: `0`.
- Prefabs with no renderer token: `23`.
- Prefabs with missing-script token: `0`.
- Flag counts: `BUILTIN_PRIMITIVE_MESH_REF`=183, `MESH_COLLIDER_TOKEN`=76, `NO_RENDERER_TOKEN`=23, `NO_STATIC_LODGROUP_TOKEN`=221, `PRODUCT_FACE_SCOPE`=47, `PROXY_OR_PLACEHOLDER_ROUTE`=118.

## Use

Use this matrix before product-face prefab replacement, visible route promotion, collider cleanup, direct-audio-ref unwiring, Addressables grouping, or Unity validator execution.

## Rejection Boundary

- Do not raw-edit prefab YAML from this matrix.
- Do not treat token absence as proof of Unity component absence.
- Do not treat `LODGroup` token presence as LOD quality proof.
- Do not treat non-primitive refs as mesh quality proof.
- Do not claim visual, collider, material, or runtime acceptance from static YAML.

## Regression Model

- CPU: static scan only; no runtime CPU change.
- GC: no runtime code changed; no allocation proof.
- Memory/VRAM: prefab bytes and token counts only; no loaded object, mesh, material, texture, or Addressables residency proof.
- Cadence: no runtime cadence changed.
- Correctness: prefab token risk is mapped; acceptance remains blocked by Unity prefab readback, scene instance readback, material/mesh/collider proof, screenshots, and profiler evidence.

Final status: `PENDING VERIFICATION`.
