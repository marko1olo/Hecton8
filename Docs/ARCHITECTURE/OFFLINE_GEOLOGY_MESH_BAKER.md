# Offline Geology Mesh Baker

Date: 2026-05-24
Status: STATIC SOURCE POLISHED / PENDING VERIFICATION
Owner: SHINOBU_208 / Echelon 2 World Generation
Evidence class: STATIC_SOURCE / STATIC_DOC

Full historical baker snapshot: `../_Archive/Architecture_X_012_APEX_2026-05-24_FILE_CAP/ARCHITECTURE_APEX_PRE_FILE_CAP_OFFLINE_GEOLOGY_MESH_BAKER.md`.

## Contract

| Area | Rule |
|---|---|
| generation | Editor-only tools under `Assets/_Project/Scripts/Editor/GeologyForge` |
| runtime input | immutable baked mesh assets from `Assets/_Project/BakedGeometry/Geology` |
| gameplay | collision/proxy/SDF ownership is outside this render-bake lane |
| rollback | baked mesh vertices/indices are not per-frame Merkle or `StateRingBuffer` truth |
| GameObjects | generated prefabs and `LODGroup` wrappers are not emitted by this lane |
| colliders | generated meshes do not add `MeshCollider` |

## Binary Layout

| Payload | Layout |
|---|---|
| vertex stream | 32 bytes |
| position | Float32x3 at byte `0` |
| normal | Float32x3 at byte `12` |
| color | UNorm8x4 at byte `24`; `Color.r` stores AO |
| uv0 | UNorm16x2 at byte `28` |
| BRG manifest | `geology_mesh_manifest.h8geom` |
| manifest header | `64` bytes |
| manifest record | `128` bytes |
| bounds extents | bytes `60..71` |
| aligned GUID lanes | start at byte `72` |

## Editor Pipeline

- Profile setup finite-vaccinates radius, height, frequency, amplitude, ridged/Voronoi weights, `IsoLevel`, `GlobalQualityWeight`, and `SectorAup` before jobs schedule.
- AUP zero lanes are canonicalized before seed hashing.
- SDF extraction uses fixed packed-nibble tetra edge LUT shared by count and extraction jobs.
- Complement cases reverse triangle winding and are checked by `ValidateComplementWinding()`.
- Editor raw working rows are fixed at 64 bytes.
- Async baking runs one variation per editor tick through `BakeProfilesAsync` and `EditorApplication.update`.
- Asset editing opens only around LOD asset creation.
- Existing LOD assets are backed up under `_H8Backups` before overwrite.
- Failed save/GUID/manifest paths delete newly created partial assets and transient meshes.

## CSV Rules

- Existing empty CSV throws `CsvErrorNoProfiles=1009`.
- Oversized, short-read, or length-changing CSV throws `CsvErrorFileSize=1008`.
- Supported header schema is validated before rows parse.
- Optional UTF-8 BOM is skipped.
- Old layouts without `iso_level` remain valid by header-token detection.
- Missing CSV uses deterministic mock profile for editor/CI bootstrap only.
- Sector AUP cells use the double parser.

## Output Rules

- `.h8geom` writes only when manifest records exist.
- Empty-surface bakes may write metrics/report evidence, not overwrite prior valid manifests.
- Manifest, dump, bake report, layout audit, and scanner report use `.tmp` replacement and preserve `.bak` where one exists.
- Missing manifest proof cannot hide orphan meshes behind `unmanifestedMeshCount=0`.
- Runtime mesh-generation scanners are editor proof tooling only.

## Quality Scaling

`GlobalQualityWeight` is continuous.

It may scale SDF noise, fractional octaves, Voronoi/ridged contribution, AO budget/steps/range, UV scale, LOD budgets, collapse size, and transition distances. It creates no binary forks.

## Black Box

- Bake telemetry rows are fixed 64 bytes.
- Ring size: 300 entries.
- Fault dump: `Docs/AgentLogs/Dump_SHINOBU_208.bin`.
- No dump file is expected until a fault path is exercised.

## Proof Required

Unity import, bake execution, mesh inspector validation, Frame Debugger, GCMonitor, player-route proof, and generated-asset inspection remain pending.
