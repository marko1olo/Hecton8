# Asset Static Row Blocker Summary - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_DOC` + `STATIC_SOURCE` + `STATIC_YAML_SCAN` + `AUDIO_PROBE` + `STATIC_IMAGE_PROBE`.
Scope: compact dispatch summary built from current audio, texture, prefab, and active-route blocker matrices.

This file is not Unity import proof, runtime proof, material proof, prefab proof, audio mix proof, VRAM proof, or visual acceptance. It summarizes static row risk so future owners can avoid reading 1357 CSV rows before choosing a packet.

CSV companion: `Docs/AssetAudit/ASSET_STATIC_ROW_BLOCKER_SUMMARY_20260605.csv`.

## Summary

- Risk groups: `16`.
- Audio risk groups: `5`.
- Texture risk groups: `5`.
- Prefab risk groups: `6`.
- Largest row groups: `NO_STATIC_LODGROUP_TOKEN`=221, `BUILTIN_PRIMITIVE_MESH_REF`=183, `PROXY_OR_PLACEHOLDER_ROUTE`=118, `ACTIVE_ROUTE_TEXTURE_BLOCKERS`=109, `LONG_GT10S`=98, `MESH_COLLIDER_TOKEN`=76.

## Use

Start here when assigning a new static asset worker. Open the named owner packet, then the source matrix named by the row.

## Rejection Boundary

- Do not treat row count as severity without route visibility and Unity readback.
- Do not claim acceptance from this summary.
- Do not edit `Assets/` or raw YAML from this summary.
- Do not bypass the named owner packet.

Final status: `PENDING VERIFICATION`.
