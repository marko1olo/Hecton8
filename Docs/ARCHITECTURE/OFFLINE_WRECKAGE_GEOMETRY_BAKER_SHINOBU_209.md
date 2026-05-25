# Offline Wreckage Geometry Baker - SHINOBU_209



Date: 2026-05-20



Status: STATIC IMPLEMENTATION / PROJECT COMPILE BLOCKED OUTSIDE DOMAIN



## Domain



Offline Editor-only deformation of pristine structural meshes into immutable damaged mesh states:



- Stressed



- Ruptured



- Collapsed



Runtime gameplay must not run vertex deformation, radial blast tearing, normal recalculation, convex hull generation, or Rigidbody debris spawning for static wreckage architecture.



## Route Moment



First-20-minutes route blocker removed: static wreck/habitat breaches can show readable structural damage without adding frame-time spikes during the Copper Wire route.



## Runtime Fence



Generated `.mesh` assets and collider proxy meshes are immutable environment assets.

Rollback/netcode synchronizes only `OfflineWreckageDamageState` as a small state index.

Excluded from rollback rings and Merkle hashing: mesh vertices, convex hull points, scorch colors, torn topology.



- Runtime destruction enforcement is handled by `Runtime_Destruction_Scanner`, which scans requested combat/environment roots outside `Editor/` for runtime mesh mutation, skinned damage, shatter code, and Rigidbody fragment spawning.
- Current static reports: `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` and `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_209.json`, findingCount `0`.
- Prior shared canonical report content is preserved as bounded provenance fields plus `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_PREVIOUS_SHINOBU_209.json`; it is not recursively embedded into the next canonical report.
- `previousReportBytes` is measured as UTF-8 encoded bytes to match the atomic writer, not UTF-16 character count, and `previousReportHash` hashes raw emitted UTF-8 bytes without name-hash case/whitespace normalization.
- Report string emission escapes JSON control characters; previous-agent extraction validates quote termination with backslash-run parity and fails closed unless the extracted field is explicitly a JSON string.


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



Forge preview, batch bake, and mock benchmark allocate one counter row with `NativeArrayOptions.UninitializedMemory`.

`BuildTornTrianglesJob` fully overwrites `Counters[0]` before hull generation, reporting, or preview reads it.



The Forge profile cache is a fixed 16-slot value cache. It is not a Persistent `NativeArray`, not runtime state, and not a Vault route.



- Source mesh extraction preserves every triangle submesh.
- The Forge builds 16-byte index-copy tiles of `OfflineWreckageSubMeshIndexRangeDTO` data, 384 indices per full tile, containing source index start, destination index start, count, and `baseVertex`.
- Descriptor `indexStart` is clamped to typed source index-buffer capacity.
- Available count is capped and triangle-aligned.
- 16-bit and 32-bit copy lanes add `baseVertex` through a 64-bit temporary.
- Final index clamps to int.
- The output still collapses to one immutable triangle stream for runtime mesh swap, but material/submesh authoring sections are not silently dropped during baking.



## Atomic Payload Writes



- Damage-state mapping `.bytes` files remain exactly 32 bytes and are written little-endian from stack spans.
- The 16 padding bytes are explicitly zeroed before the four hashes are written, so the serialized DTO is deterministic instead of inheriting stack residue.
- The writer emits to a same-volume `.tmp.<processId>.<ordinal>` path with exclusive access, then publishes with `File.Replace` for existing targets or `File.Move` for first creation before `AssetDatabase.ImportAsset`.



- When owned `.bytes`, JSON, and black-box dumps are generated, publication uses `File.Replace(temp, final, null)`.
- Readers do not observe a missing final path.
- This is an atomic-write contract, not proof that every final artifact currently exists.
- First creation uses `File.Move`.
- Direct final-path overwrite, fixed shared `.tmp` reuse, and delete-then-move replacement are rejected because interrupted or concurrent editor IO can produce torn, stale, or missing proof artifacts.



Generated mesh assets use deterministic output paths:

- visual: `GEN_<sanitizedSourceName>_<sourcePathHash>_<STATE>.asset`;
- collider: `GEN_<sanitizedSourceName>_<sourcePathHash>_<STATE>_COLLIDER.asset`.

First bake creates asset. Repeated bakes refresh via `EditorUtility.CopySerialized`, preserving `.meta` GUID.



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



- Expected atomic output: `Docs/Reports/WRECKAGE_MOCK_BENCHMARK_SHINOBU_209.json`.
- Artifact is absent in this checkout.
- Required before proof: Editor/CI benchmark entrypoint, Unity import, menu execution logs.



## Unity Import Identity



Unity import identity:

- Scope: owned `.cs` and `.asmdef` under `Assets/_Project/Scripts/World/OfflineWreckageBaker`.
- `.meta` state: explicit files present.
- Duplicate GUID scan: no duplicates.
- Rejected risk: Unity minting local GUIDs on first import.
- Generated outputs: baked meshes and map bytes avoid `GenerateUniqueAssetPath`.
- Path rule: stable source-hash paths preserve references across rebakes.



## Editor Preview Lifetime



`OfflineWreckagePreviewStore` owns only transient preview `Mesh`.

It marks it `HideFlags.HideAndDontSave` and disposes before assembly reload/editor quit. Same lifecycle hook disposes 300-entry black-box `NativeArray` ring.



Black-box ring registration uses `Hecton8.Core.Contracts.NativeMemoryTrackingBridge`, not a direct root Core dependency.

If Core sentinel bridge is installed, it records owner `OfflineWreckageBlackBox`, label `s_ring`, lifetime `Session`, and 300 * 64 bytes. If absent, registration no-ops and disposal releases the ring.



## Atomic Publication



- Owned `.bytes`, JSON reports, benchmark output, scanner output, and black-box dumps write to unique same-volume `.tmp.<processId>.<ordinal>` paths with `FileMode.CreateNew`.
- Publication uses `File.Replace` for existing final artifacts and `File.Move` for first creation.
- If another Editor tool changes final-path existence between the first observation and commit, `OfflineWreckageAtomicFile.Publish` retries once after re-observing file state while the owned temp still exists.



## Collision Lie



Visual mesh may twist and tear. Physical proxy is an offline support-mapped convex hull. No dynamic Rigidbody fragments are generated.



- Thin structural inputs keep their measured extents.
- If one bounds axis collapses to zero, support hull expands only that axis to `0.01 m` half-extent.
- It sets `WarningHullBoundsExpanded`.
- It does not replace a bulkhead or hull plate with a unit cube unless all bounds are invalid/non-finite.



## Black Box



The baker records one `OfflineWreckageTelemetryEntry` per baked state into a fixed 300-entry `NativeArray` ring. On non-finite vertex detection it dumps `Docs/AgentLogs/Dump_SHINOBU_209.bin`.



Dump layout is binary and fixed:



- header: 32 bytes, little-endian `magic/version/capacity/retained/cursor/start/entrySize/reserved`



- row: raw `OfflineWreckageTelemetryEntry`, 64 bytes



- size: `32 + retainedRows * 64`



- The writer no longer uses `BinaryWriter`;
- it explicitly clears the 32-byte header before writing fields,
- copies each DTO row through `UnsafeUtility.CopyStructureToPtr` into a stack span,
- and writes the file atomically through a unique same-directory temp path.



## Scalability



Low: baked mesh swap plus 8-point hull, no runtime deformation.



Middle: same runtime cost, richer stressed/ruptured/collapsed source assets.



High: stronger baked scorch and tear detail, same runtime state index.



Ultra: denser authored inputs and more aggressive baked visual states, still no runtime geometry math.



`GlobalQualityWeight` is continuous.

Low values collapse deformation amplitudes, scorch intensity, and tear detail through `math.lerp`/`math.smoothstep`. Seam vertex duplication approaches zero before high-detail tear expansion.

Collision proxy stays the same 8-point hull at all qualities.



Burst bake kernels sanitize non-finite quality/radius/torsion/damage/intensity scalars inside the job.

Corrupt CSV or CI mock input collapses to finite bounded local math before `sqrt`, `rsqrt`, `rcp`, trigonometry, or `smoothstep`.



## AUP Precision



Forge exposes module and blast AUP as six `DoubleField` controls. The baker subtracts `double3 blastAup - double3 moduleAup` before casting the localized delta to `float3`.



## Burst Mode



Owned jobs use `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`. The outputs are immutable editor assets, not rollback-authoritative state; runtime synchronization remains a small integer damage-state index.



## Verification Status



- Static source exists and the runtime-root scan report has been generated with zero findings.

- Pass 11 static scans were clean for stale index-copy symbols and sibling runtime references
- Pass 12 hardened submesh descriptor bounds and 16-bit `baseVertex` clamping
- Pass 13 wired the editor-only black-box ring into `Hecton8.Core.Contracts.NativeMemoryTrackingBridge`
- Pass 14 replaced the flat-axis unit-cube hull fallback with measured-bounds expansion plus warning propagation
- Pass 15 bounded previous-report scanner preservation
- Pass 16 corrected scanner provenance to UTF-8 byte semantics
- Pass 17 removed name-hash normalization from scanner provenance hashing
- Pass 18 hardened scanner JSON escaping/extraction
- Pass 19 made previous-agent extraction fail closed on non-string values
- Pass 20 hardened atomic artifact publish against final-path existence races
- Pass 21 hardened normal-angle finite guards before reciprocal square root
- Pass 22 hardened deformation scalar/vector inputs across mock, shear, radial blast, tear split, and color bake jobs
- Pass 23 removed allocator zero-fill from the deterministic 64-byte counter row call sites.

- Forbidden API scan only found scanner pattern constants.

- One single-core dotnet build launched after CPU measured 45.095 percent.
- No dotnet/csc process was active.
- Result: stopped on 72 unrelated `Hecton8.Core.csproj` missing-type errors.
- Errors were outside owned offline wreckage baker domain.

- Unity import, Burst Inspector, Editor bake run, Console, Frame Debugger, GCMonitor, Memory Profiler, and player build proof remain pending.
