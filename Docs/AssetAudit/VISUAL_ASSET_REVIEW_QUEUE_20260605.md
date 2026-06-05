# Visual Asset Review Queue - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_DOC` + `STATIC_IMAGE_QA` + `STATIC_SOURCE`.
Scope: visual review order only. No materials, prefabs, scenes, import settings, or files under `Assets` were changed.

## Input Evidence

- `Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.csv`
- `Docs/AssetAudit/TEXTURE_VISUAL_REVIEW_20260605.md`
- `Docs/AssetAudit/TEXTURE_MATERIAL_USAGE_REVIEW_20260605.md`
- `Docs/AssetAudit/SOURCE_PROTOTYPE_CLEANUP_REVIEW_20260605.md`
- `Docs/AssetAudit/TEXTURE_IMPORT_ROLE_MATRIX_20260605.csv`
- `Docs/AssetAudit/ADDRESSABLES_ASSET_GROUP_PLAN_20260605.csv`

Queue file:

- `Docs/AssetAudit/VISUAL_ASSET_REVIEW_QUEUE_20260605.csv`

## Review Order

1. Waterline foam/contact because rejected `foam.png` is active-route reachable.
2. Proxy flora/coral/kelp because `WorldProceduralProxy` materials reach `02_HECTON_WORLD`.
3. Aegir/sky because surface and first-exit beauty cannot use toy/prototype celestial art.
4. Terrain wet basalt/shell sand because photic route needs material identity, not random scans.
5. Flora/geology prefab candidates because source pools exist but material/LOD proof is absent.
6. Oxygen UI sprite because HUD readability is player-critical.
7. Unknown useful sources and orbit-bound refs last because ownership is ambiguous.

## Required Proof

- Unity material-slot readback.
- Scene renderer user list for active route refs.
- Bright surface/photic screenshots.
- Frame Debugger/Stats where visible route rendering changes.
- Import settings proof for color space, texture type, compression, mips, streaming mips, and platform max size.
- Addressables/handle/release proof before broad runtime residency claims.

## Scalability Consequences

- Low/compact: preserve beauty through correct material roles, compressed maps, strong silhouettes, and route composition. No flat fallback.
- Middle: route-owned PBR stacks and candidate prefab pools can expand only after binding/import proof.
- High: spend saved budget on richer Aegir/cloud detail, wet-edge masks, detail normals, and longer LOD residency.
- Ultra: visual overkill through layered sky/ocean/terrain/flora detail only after memory and render proof.

## Regression Model

- CPU: no runtime code changed.
- GC: no runtime code changed.
- Memory/VRAM: no import/residency changed.
- Cadence: no runtime cadence changed.
- Correctness: reduces visual review ordering ambiguity only; no visual acceptance.

Final status: `PENDING_VERIFICATION`.
