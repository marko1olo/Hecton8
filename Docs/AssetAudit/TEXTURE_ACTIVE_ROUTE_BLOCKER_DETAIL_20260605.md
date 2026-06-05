# Texture Active Route Blocker Detail - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_SOURCE`.
Scope: static texture/material usage rows with active route, priority, proxy, placeholder, rejected support, Aegir/sky, terrain/geology, or flora/coral blocker evidence.

No Unity run, material edit, prefab edit, scene save, import edit, build, profiler, Addressables operation, or asset mutation was performed. This file proves serialized/static reachability only.

CSV companion: `Docs/AssetAudit/TEXTURE_ACTIVE_ROUTE_BLOCKER_DETAIL_20260605.csv`.

## Summary

| Priority | Rows | Static Meaning |
|---|---:|---|
| P0 | 45 | Rejected foam/contact and proxy/placeholder material contamination need Unity readback and replacement/proof. |
| P1 | 44 | Sky/Aegir/cloud and terrain/geology route sources need readback, cleaned PBR, slot proof, and screenshots. |
| P2 | 20 | Referenced/candidate rows need owner assignment before route promotion. |

Family counts:

| Family | Rows | Static Meaning |
|---|---:|---|
| `terrain_geology` | 51 | Terrain/geology sources are materially reachable but route proof and clean PBR remain absent. |
| `flora_coral_fauna` | 48 | Imported flora/coral stacks are reachable; proxy contamination and import/LOD/material proof remain blockers. |
| `sky_aegir_cloud` | 8 | Sky/Aegir/cloud sources are reachable; hero material slot and bright-route screenshot proof remain absent. |
| `water_foam_rejected_support` | 1 | Rejected foam source is active-route reachable. |
| `water_foam_caustic` | 1 | Water/caustic support source needs material/readback proof. |

## Owner Rules

- Static reachability is not active renderer proof.
- Do not raw patch material YAML from this table.
- Do not promote `foam.png`, `WorldProceduralProxy`, or placeholder material routes.
- Future owner must pair this CSV with `ASSET_OWNER_06_UNITY_READBACK_EXECUTION_PACKET.md` and `TEXTURE_MATERIAL_FAMILY_ROUTE_MATRIX_20260605.csv`.

## Regression Model

- CPU: static CSV derivation only.
- GC: no runtime code touched.
- Memory/VRAM: no residency proof.
- Cadence: no runtime cadence changed.
- Correctness: active-route visual blockers are row-addressable for future material/Unity owner.

Final status: `PENDING VERIFICATION`.
