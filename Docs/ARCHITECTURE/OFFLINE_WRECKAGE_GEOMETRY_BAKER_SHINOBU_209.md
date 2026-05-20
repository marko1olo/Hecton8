# Offline Wreckage Geometry Baker - SHINOBU_209

Date: 2026-05-20
Status: STATIC IMPLEMENTATION / PROJECT COMPILE BLOCKED OUTSIDE DOMAIN

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R45 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- Docs/README.md
- Docs/DOC_GOVERNANCE.md
- Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md
- current source files
- fresh verification logs and artifacts

Current root/architecture boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md` as STATIC_DOC/STATIC_SOURCE/FILESYSTEM/PY_TOOL evidence. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. R42 remains the prior counter/route-boundary/proof-label correction. R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers; runtime proof remains absent.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-20 R45): `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md` is the latest local static root/architecture R43/R44 residue, proof-artifact wording, source-counter, and atlas-boundary correction. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->


## Domain

Offline Editor-only deformation of pristine structural meshes into immutable damaged mesh states:

- Stressed
- Ruptured
- Collapsed

Runtime gameplay must not run vertex deformation, radial blast tearing, normal recalculation, convex hull generation, or Rigidbody debris spawning for static wreckage architecture.

## Route Moment

First-20-minutes route blocker removed: static wreck/habitat breaches can show readable structural damage without adding frame-time spikes during the Copper Wire route.

## Runtime Fence

The generated `.mesh` assets and collider proxy meshes are immutable environment assets. Rollback/netcode synchronizes only `OfflineWreckageDamageState` as a small state index. Mesh vertex data, convex hull points, scorch colors, and torn topology are excluded from rollback state rings and Merkle hashing.

Runtime destruction enforcement is handled by `Runtime_Destruction_Scanner`, which scans requested and actual combat/environment roots outside `Editor/` for runtime mesh mutation, skinned damage, shatter code, and Rigidbody fragment spawning. Current static reports: `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` and `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_209.json`, findingCount `0`.

## Data Layout

`MeshDamageStateMappingDTO` is explicit 32 bytes:

- offset 0: `uint PristineMeshHash`
- offset 4: `uint StressedMeshHash`
- offset 8: `uint RupturedMeshHash`
- offset 12: `uint CollapsedMeshHash`
- offset 16: `ulong _pad0`
- offset 24: `ulong _pad1`

Generated render mesh vertex layout is 64 bytes interleaved:

- Position `float3`
- Normal `float3`
- Tangent `float4`
- TexCoord0 `float2`
- Color `UNorm8x4`
- TexCoord3 `float3` local AUP/triplanar anchor

`OfflineWreckageBakeCounters64` is explicit 64 bytes:

- offset 0: `int ActiveVertexCount`
- offset 4: `int TornVertexCount`
- offset 8: `int DegenerateTriangleCount`
- offset 12: `int HullVertexCount`
- offset 16: `uint WarningFlags`
- offset 20: `uint _pad0`
- offset 24..63: five `ulong` padding fields

This replaces adjacent tiny `NativeArray<int>` count buffers in the bake dependency chain.

The Forge profile cache is a fixed 16-slot value cache. It is not a Persistent `NativeArray`, not runtime state, and not a Vault route.

Source mesh extraction preserves every triangle submesh. The Forge builds 16-byte index-copy tiles of `OfflineWreckageSubMeshIndexRangeDTO` data, 384 indices per full tile, containing source index start, destination index start, count, and `baseVertex`. Descriptor `indexStart` is clamped to the typed source index buffer capacity, available count is capped and triangle-aligned, and both 16-bit and 32-bit copy lanes add `baseVertex` through a 64-bit temporary with int clamping. The output still collapses to one immutable triangle stream for runtime mesh swap, but material/submesh authoring sections are not silently dropped during baking.

## Atomic Payload Writes

Damage-state mapping `.bytes` files remain exactly 32 bytes and are written little-endian from stack spans. The 16 padding bytes are explicitly zeroed before the four hashes are written, so the serialized DTO is deterministic instead of inheriting stack residue. The writer emits to a unique same-volume `.tmp.<processId>.<ordinal>` path with exclusive access, then publishes with `File.Replace` for existing targets or `File.Move` for first creation before `AssetDatabase.ImportAsset`.

Existing `.bytes`, JSON, and black-box dump artifacts publish with `File.Replace(temp, final, null)` so readers do not observe a missing final path. First creation uses `File.Move`. Direct final-path overwrite, fixed shared `.tmp` reuse, and delete-then-move replacement are rejected because interrupted or concurrent editor IO can produce torn, stale, or missing proof artifacts.

Generated visual and collider mesh assets use deterministic output paths: `GEN_<sanitizedSourceName>_<sourcePathHash>_<STATE>.asset` and `GEN_<sanitizedSourceName>_<sourcePathHash>_<STATE>_COLLIDER.asset`. First bake creates the mesh asset; repeated bakes refresh the existing asset via `EditorUtility.CopySerialized`, preserving the existing `.meta` GUID instead of generating orphaned numbered assets.

## CI Mock Benchmark

`OfflineWreckageMockBenchmark` is an Editor-only automation entrypoint at `HECTON-8/Wreckage Forge/Run Mock Benchmark`. It does not require source art assets and does not instantiate GameObjects.

The benchmark generates:

- 48 x 48 x 6 dense mock vertex lattice
- XY/XZ/YZ min/max boundary surface triangle indices: 5358 quads / 32148 indices at default resolution
- structural shear
- radial blast and tear weights
- torn triangle duplication
- normal/tangent recalculation
- scorch/rust vertex color baking
- 8-point convex hull proxy

It is expected to write `Docs/Reports/WRECKAGE_MOCK_BENCHMARK_SHINOBU_209.json` atomically. That artifact is absent in this checkout until the Editor/CI benchmark entrypoint runs; Unity import and menu execution logs are still required before treating it as proof.

## Unity Import Identity

Every owned `.cs` and `.asmdef` file under `Assets/_Project/Scripts/World/OfflineWreckageBaker` has an explicit `.meta` file. Duplicate GUID scan over the domain returned no duplicates. This prevents Unity from minting local GUIDs for the baker assembly and editor scripts during first import. Generated baked meshes and map bytes also avoid `GenerateUniqueAssetPath`; stable source-hash paths preserve references across rebakes.

## Editor Preview Lifetime

`OfflineWreckagePreviewStore` owns only a transient preview `Mesh`, marks it `HideFlags.HideAndDontSave`, and disposes it before assembly reload/editor quit. The same editor lifecycle hook disposes the 300-entry black-box `NativeArray` ring, keeping retained editor objects bounded to the domain lifetime.

The black-box ring is registered through `Hecton8.Core.Contracts.NativeMemoryTrackingBridge`, not a direct root Core dependency. If the Core sentinel bridge is installed, it records owner `OfflineWreckageBlackBox`, label `s_ring`, lifetime `Session`, and 300 * 64 bytes. If the bridge is absent, registration no-ops and disposal still releases the ring.

## Collision Lie

The visual mesh can be twisted and torn. The physical proxy is a support-mapped convex hull mesh generated offline from the deformed bounds. No dynamic Rigidbody fragments are generated by this baker.

## Black Box

The baker records one `OfflineWreckageTelemetryEntry` per baked state into a fixed 300-entry `NativeArray` ring. On non-finite vertex detection it dumps `Docs/AgentLogs/Dump_SHINOBU_209.bin`.

Dump layout is binary and fixed:

- header: 32 bytes, little-endian `magic/version/capacity/retained/cursor/start/entrySize/reserved`
- row: raw `OfflineWreckageTelemetryEntry`, 64 bytes
- size: `32 + retainedRows * 64`

The writer no longer uses `BinaryWriter`; it explicitly clears the 32-byte header before writing fields, copies each DTO row through `UnsafeUtility.CopyStructureToPtr` into a stack span, and writes the file atomically through a unique same-directory temp path.

## Scalability

Low: baked mesh swap plus 8-point hull, no runtime deformation.
Middle: same runtime cost, richer stressed/ruptured/collapsed source assets.
High: stronger baked scorch and tear detail, same runtime state index.
Ultra: denser authored inputs and more aggressive baked visual states, still no runtime geometry math.

`GlobalQualityWeight` is continuous. At low values, deformation amplitudes, scorch intensity, and tear detail collapse through `math.lerp`/`math.smoothstep`; seam vertex duplication approaches zero before the high-detail tear path expands. The collision proxy remains the same 8-point hull at all qualities to keep runtime physics invariant.

## AUP Precision

Forge exposes module and blast AUP as six `DoubleField` controls. The baker subtracts `double3 blastAup - double3 moduleAup` before casting the localized delta to `float3`.

## Burst Mode

Owned mathematical jobs use `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`. The outputs are immutable editor assets, not rollback-authoritative state; runtime synchronization remains a small integer damage-state index.

## Verification Status

Static source exists and the runtime-root scan report has been generated with zero findings. Pass 11 static scans were clean for stale index-copy symbols and sibling runtime references; Pass 12 hardened submesh descriptor bounds and 16-bit `baseVertex` clamping; Pass 13 wired the editor-only black-box ring into `Hecton8.Core.Contracts.NativeMemoryTrackingBridge`. Forbidden API scan only found scanner pattern constants. One single-core dotnet build was launched after CPU measured 45.095 percent and no dotnet/csc process was active, but it stopped on 72 unrelated `Hecton8.Core.csproj` missing-type errors outside the owned offline wreckage baker domain. Unity import, Burst Inspector, Editor bake run, Console, Frame Debugger, GCMonitor, Memory Profiler, and player build proof remain pending.
