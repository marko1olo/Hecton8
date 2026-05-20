# SHINOBU_210 Offline Module Damage Baker

Date: 2026-05-20
Status: STATIC SOURCE UPDATED - PENDING UNITY IMPORT / PROFILER PROOF

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R47 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

Current root/architecture boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md` as STATIC_DOC/STATIC_SOURCE/FILESYSTEM/PY_TOOL evidence. R46 remains the prior interior-authority/route-field/proof-language correction; R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; runtime proof remains absent.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-20 R47): `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md` is the latest local static root/architecture authority-spine, runtime-wording, and counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
## Boundary

SHINOBU_210 owns offline Editor generation of baked habitat damage mesh variants only:

- Stressed mesh state
- Ruptured mesh state
- Collapsed mesh state
- Primitive collision-lie hull DTOs
- Mesh-hash mapping DTOs
- Editor forge and static scanner

It does not own runtime structural truth, rollback state, visual VFX particles, PhysX debris, or network authority.

## Reserved Contract IDs

These are owner-local reserved IDs for a future consumer import path. The offline baker does not request these
buffers during gameplay.

- `73320` `HabitatDamageStateMappings`: `ModuleDamageStateMappingDTO[4096]`, explicit 32-byte rows.
- `73321` `HabitatDamageHullProxies`: `HabitatDamageHullDTO[32768]`, explicit 64-byte rows.
- `73322` `HabitatDamageBakeTelemetryRing`: `HabitatDamageBakeTelemetryEntry[300]`, explicit 64-byte rows.
- `73323` `HabitatDamageBakeTelemetryCursor`: `int[1]`.

No edit to the central `BufferID` enum was made in this pass. A runtime owner that imports these assets must either
promote the IDs through the Core memory owner or keep the local numeric range documented here before requesting Vault
handles.

## Runtime Contract

Runtime code receives no `MonoBehaviour`, `ScriptableObject`, `MeshFilter`, `Rigidbody`, `MeshCollider`, blendshape,
particle, or deformation controller from this lane.

`HabitatDamageMeshStateResolver` maps continuous pressure scalar to state index using a branch-light sum of three
`math.step` thresholds: Stressed at 0.33333334, Ruptured at 0.6666667, and Collapsed at 0.95. It then resolves the baked
mesh hash from `ModuleDamageStateMappingDTO`. The actual mesh-object table remains a rendering/presentation owner
concern; gameplay/netcode truth is only the integer damage state.

## Assembly Boundary

The offline baker lives in `Hecton8.Habitat.Deformation.DamageBake.Editor` under
`Assets/_Project/Scripts/Habitat/Deformation/Editor/DamageBake/`. That assembly references only
`Hecton8.Habitat.Deformation.Contracts`, Burst, Collections, Jobs, and Mathematics. It does not reference the runtime
deformation assembly.

## Vertex Layout

The generated `.mesh` vertex stream is 32 bytes:

- `0..11`: position `float3`
- `12..19`: normal `float16x4`
- `20..23`: tangent `snorm8x4`
- `24..27`: uv0 `float16x2`
- `28..31`: color `unorm8x4`

Editor scratch vertex arrays are also explicit: source vertices are 64 bytes and working vertices are 128 bytes so
parallel bake writes begin on 64-byte boundaries. This is Editor-only scratch memory and is not serialized as gameplay
state.

Bake settings are explicit 80 bytes with `double3` AUP fields first: `ModuleAup` at offset 0 and `SeaLevelAup` at
offset 24, followed by float tuning values including `GlobalQualityWeight` at offset 64.

The Burst bake jobs avoid direct `math.length` calls in crush/stress paths. Length magnitudes are computed through a
guarded `math.rsqrt(math.max(dot(v, v), epsilon))` helper to keep zero-length vectors finite on weak CPU targets.

The mock corridor and buckling kernel now share the same geometric assumption: corridor axis is local `Z`, and radial
crush is applied in the local `X/Y` cross-section. The mock benchmark samples the full cylinder length by using
`max(1, lengthSegments - 1)` as the axial denominator, avoiding an off-by-one short corridor.

Tear displacement is branch-light for the common no-tear case: seam intensity is converted to an `active` scalar with
`math.step`, and quality richness uses `math.smoothstep` before `math.lerp`-driven gap and hull scaling.

Hull proxy output is zeroed before any vertex scan, and the hull job returns empty hull rows unless at least one finite
deformed vertex contributes to bounds. This prevents uninitialized or sentinel-derived collision-proxy rows from leaking
into manifests after a bad source mesh or NaN input. Mesh bounds use the same finite-vertex count guard before producing
a Unity `Bounds` value.

The mesh pack job is chained onto the deformation/normal/color/hull dependency graph and completed once at the Unity
serialization boundary. Baked vertices are written into a `NativeArray<HabitatDamageBakedVertex>` and uploaded with
`Mesh.SetVertexBufferData`; indices are uploaded with `Mesh.SetIndexBufferData`. The only remaining `Complete()` calls
are cold Editor synchronization points where Unity APIs require CPU-visible mesh data or benchmark timing.
The final pack stage also clamps non-finite position, tangent, UV, stress, and tear values before writing half/snorm/color
fields, preventing NaN payloads from becoming serialized mesh assets.
Generated mesh and manifest paths are deterministic. Re-bake refreshes existing `.asset` files through
`EditorUtility.CopySerialized` instead of minting numbered `GenerateUniqueAssetPath` orphans.

Source mesh indices are compacted through explicit 16-byte `HabitatDamageIndexRangeDTO` rows. Each row records a
triangle submesh's raw `indexStart`, compact destination start, count, and `baseVertex`. The Burst copy job applies
those rows and clamps the final index to `0..vertexCount-1` before tear and normal passes, so multi-submesh habitat
modules do not accidentally bake non-triangle submeshes, wrong base-vertex topology, or out-of-range bad-import
indices.

The runtime-destruction scanner reads source files into a temporary `NativeArray<byte>` and scans code-context bytes
outside comments and string/char literals. It does not use `File.ReadAllLines`, `File.ReadAllBytes`, `string.Split`, or
managed per-file `byte[]` buffers.
Before overwriting the shared canonical `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`, it streams the prior report to
`Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_PREVIOUS_SHINOBU_210.json` and records only byte count plus FNV-1a hash in
the new canonical report. It does not embed recursive full-report JSON.

Pointer aliasing annotations are restricted to buffers with clear physical separation. Source mesh attribute byte views
are read-only but can overlap when Unity stores attributes in one interleaved stream, so they are not marked `[NoAlias]`.
Output vertices, compact index buffers, range rows, hull rows, and packed vertex outputs remain annotated where they do
not overlap by construction.

## Dear Lie

The player sees twisted metal and breach holes. Runtime collision is represented by primitive hull DTOs and avoids
deformed `MeshCollider` rebuilds. Visual rust/stress is packed into vertex colors for shader consumption instead of
unique per-module textures.

## Blackbox

The Editor bake report owns a fixed 300-entry `NativeArray<HabitatDamageBakeTelemetryEntry>` only for the bake queue
lifetime. It records module hash, state hash, triangle counts, hull count, quality weight, Burst milliseconds, fault
flags, and output mesh hash. The queue writes `Docs/AgentLogs/Dump_SHINOBU_210.bin` as explicit little-endian binary:
24-byte header followed by 64-byte telemetry rows. Float words use `math.asuint`; integer byte swapping uses a
source-local bitwise fallback rather than package-version-specific `math.reversebytes`. The queue disposes the ring at
completion or no-batch exit. This is not a gameplay Vault allocation and must not be treated as rollback state.

## Verification

Static source verification exists only for this pass. Unity import, Burst compile, Mesh Inspector layout proof,
Profiler, GCMonitor, and player-build proof remain pending.
