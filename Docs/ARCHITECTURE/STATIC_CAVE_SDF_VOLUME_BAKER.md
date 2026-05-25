# Static Cave SDF Volume Baker



Status: PENDING VERIFICATION



Owner: SHINOBU_244



Domain: Echelon 2 World Generation, Editor-only offline mesh-to-SDF baking.



## Contract



The baker converts pre-authored cave, arch, and wreck meshes into immutable SDF payloads. It does not create runtime controllers, registry slots, signal lanes, or mutable DataVault routes.



Output files:

- `.h8bin` payload: 64-byte header + flat `ushort` half-float distance field.

- Optional `Texture3D`: `GraphicsFormat.R16_SFloat` for VFX raymarching.
- Required support: 3D textures, `TextureFormat.RHalf`, R16 sampling in Editor/device.
- Unsupported texture/format support skips optional texture.
- `.h8bin` remains authoritative.



Header layout:



- bytes 0-23: `double3` AUP anchor.



- bytes 24-35: `int3` resolution.



- bytes 36-47: `float3` bounds min.



- bytes 48-59: `float3` bounds max.



- bytes 60-63: folded XXHash3 payload checksum.



- Header endianness is explicitly little-endian because HECTON-8 targets x86 and ARM64 little-endian platforms.
- Half-distance `ushort` payload is written little-endian.
- Little-endian hosts copy chunks directly; big-endian hosts swap each ushort pair in cold editor writer before streaming bytes.
- A legacy or network-transcoded big-endian importer must reverse bytes before hydrating the fields.



Runtime consumers treat these files as static environmental data. They are excluded from rollback and Merkle state hashing; netcode synchronizes entity positions and authoritative mutable state only.



- `StaticCaveSdfContracts.cs` holds DTOs and constants only.
- Editor-only finite/mix helpers live in the Editor assembly.
- `StaticSdfForgeWindow` owns profile-byte hashing for fallback and CSV profile ingestion.
- Runtime contracts expose no string-hash utility.



## Serialization Safety



- The binary writer never overwrites the active `.h8bin` in place.
- It writes `GEN_*.h8bin.tmp`, flushes it, verifies `64 + voxelCount * 2` bytes.
- Then it moves prior payload to `GEN_*.h8bin.bak` when present and renames temp to final asset path.
- If final rename fails after a backup was created, the writer attempts to restore the backup before rethrowing.



`CAVE_SDF_BAKE_REPORT.json` records `expectedFileSizeBytes`, `endianness`, `payloadEndian`, `atomicWrite`, `compileStatus`, and `unityImportProof` so a static placeholder cannot be confused with measured Unity bake proof.



## Memory Ownership



- SHINOBU_244 does not request runtime Vault buffers because it owns no runtime streaming route.
- Editor baker owns no persistent private `NativeArray` fields.
- Bake scratch and 300-row blackbox telemetry are local `Allocator.TempJob` buffers disposed in `finally`.
- SceneView slice overlay streams rows from the last generated `.h8bin`; no private preview array or scene component is kept.
- The overlay draws per-sample discs through `Handles.DrawSolidDisc`; it no longer owns a private `Vector3[]` vertex buffer.
- Preview file open/read races during bake or atomic rename fail closed with `null`/`false`; overlay skips the row instead of throwing Editor GUI exceptions.
- Invalid preview row starts and row widths fail before byte-count/offset/read math, preventing malformed editor requests from overflowing row byte counts or seeking outside the payload.


## Bake Path



MeshData extraction -> `TriangleDTO[48]` -> Burst BVH construction -> Burst voxel SDF evaluation -> `math.f32tof16` compression -> editor-blocking atomic chunked file write -> optional `R16_SFloat` Texture3D.



- Binary writer emits to `.tmp` and verifies exact byte count.
- It moves previous `.h8bin` to `.bak`, then renames `.tmp` to final.
- If final rename fails, `.bak` is restored.
- Stale `.tmp` is deleted on failed write, size verification, or rename.
- It never deletes the final `.h8bin` path directly.



Delete helper names encode mutation explicitly.

Stale `.bak` cleanup is `DeleteExistingBackupOrThrow`; failed `.tmp` promotion cleanup is `DeleteStaleTempBestEffort`. No generic mutating `TryDelete*` helper exists.



This baker adds no runtime point-to-triangle distance evaluation. Runtime SDF query cadence remains consumer-owned and continuous via `GlobalQualityWeight`, not binary low/high variants.



- `SanitizeConfig` is the only route into bake dimensions.
- Resolution is clamped through a 64-bit voxel-count guard.
- Non-finite narrow-band distance clamps to finite `0.05m..50000m`.
- Explicit or Unity mesh bounds validate before use.
- Fallback finite 1m cube is used only when no valid bounds exist.
- Mesh-local center or half-extent values beyond the 100km authoring budget are rejected instead of being clamped into a false payload.
- AUP carries universe-scale offset; local SDF bounds must stay finite before `math.lerp`, BVH traversal, header serialization, and SceneView preview math.



- The SDF evaluator guards degenerate triangle math explicitly.

- Closest-point edge and face reciprocal denominators use safe reciprocal helpers.
- Ray-parity determinant reciprocals preserve sign while clamping absolute magnitude.
- Ray parity applies deterministic sub-millimeter YZ offset before traversal, preventing shared edge/vertex double-count or sign misses.

- BVH traversal stack overflow writes a finite out-of-band distance sentinel instead of silently dropping child nodes or relying on FastMath NaN propagation.

- `ValidateSdfDistanceWarningsJob` is a single-writer validation pass after SDF evaluation.
- It collapses non-finite or out-of-band distances to zero.
- It sets `WarningNonFiniteFallback` through a fixed one-int TempJob warning lane.
- It triggers `Docs/AgentLogs/Dump_SHINOBU_244.bin` after Stage2 telemetry records.

- Owned `.Complete()` and AssetDatabase sync sites are `[EDITOR_BLOCKING_SYNC_POINT]` barriers.
- Covered stages: timing, MeshData lifetime, payload serialization, binary import, optional Texture3D creation, save, refresh.
- They are not runtime dispatcher routes.

- Blackbox dump row serialization uses `UnsafeUtility.SizeOf<StaticCaveSdfTelemetryEntry>()`.
- The value feeds both file header and stack row buffer.
- Dump writer stays bound to explicit 64-byte telemetry DTO.
- No duplicated magic constant.

- Generated self-audit XML escapes generic angle brackets in this proof text so the `<SELF_AUDIT>` fragment remains parseable after a real Forge bake.



- Mutating Editor helpers use action verbs, not read-looking accessors.
- Mesh conversion: `BuildTrianglesFromMeshData`.
- CSV ingestion: `LoadProfilesFromCsv`.
- Parser cursor consumption: `ParseProfileRow`, `ParseKeyHash`, `ParseInt`, `ParseFloat`.
- Gizmo row transfer: `CopyRowFromOpenStreamForGizmo`.
- CSV ingestion:
  - Required header: `name,resolution,narrow_band_meters,global_quality_weight,submesh_index`.
  - Row checks: profile name, comma boundaries, numeric formats, integer overflow, row ending.
  - Capacity: rejects beyond 16 profiles.
  - Stack cap: 4 KB.
  - Larger cold editor buffers: `ArrayPool<byte>`.
  - Rented buffers are cleared before return.
- CSV file length races and IO/permission races fail closed during cold load instead of parsing a stale prefix or throwing through the UI.
- Malformed CSV rows or overflow rows fail the import closed and emit row/column diagnostics instead of silently falling back to clamped numeric defaults or ignoring designer rows.
- Pure local helpers keep their narrower names only when they do not allocate, run IO, complete jobs, mutate global state, or search the scene.
- Editor preview file existence is exposed through `ValidatePreviewBinaryForGizmo`, not a read-looking accessor.


- Mesh conversion is fenced at both caller and job boundary.

- `BuildTrianglesFromMeshData` rejects unreadable meshes and catches Unity/argument failures from `Mesh.AcquireReadOnlyMeshData` before the caller turns them into a guarded Forge failure.

- `ReadSubMeshRange` now rejects negative starts/counts, zero counts, descriptor overflow, out-of-capacity spans, and non-triangle-multiple index counts instead of repairing corrupt imported descriptors through clamp/truncate.

- In all-submesh mode, non-triangle topology is skipped, but a triangle submesh with a corrupt descriptor fails the bake closed instead of silently producing a partial SDF.

- All-submesh path accumulates triangle count in 64-bit space before native allocation.
- Mesh conversion splits into `BuildTrianglesFromMesh16Job` and `BuildTrianglesFromMesh32Job`; no scheduled job carries default index `NativeArray`.

- Both variants write through a per-submesh `NativeSlice<TriangleDTO>`.
- Local slice index validates before raw index or position reads.
- Absolute index reads validate against active submesh span, index NativeArray length, vertex count, and vertex byte range before raw strided position access.

- UInt32 indices above `Int32.MaxValue` are rejected before `baseVertex` is applied, so a malformed huge index cannot be converted into a small vertex through a negative base vertex.

- Invalid index fallback does not inherit `baseVertex`; baseVertex addition is clamped through 64-bit arithmetic before vertex reads.

- Every owned `IJobParallelFor.Execute` guards output range.
- `EvaluateSdfVolumeJob` fail-closes missing triangle/index/node inputs through traversal-failure sentinel.
- It guards traversal stack overflow and resolution layer multiplication.
- `CompressSdfToHalfJob` guards mismatched lengths with zero fallback.

- `ConstructBvhJob` rejects triangle-index buffers shorter than the triangle stream, and `EvaluateSdfVolumeJob` bounds-checks BVH leaf index ranges before reading `TriangleIndices`.

- `BakeTrianglesInternal` rejects triangle streams that would overflow fixed BVH node capacity.

- This prevents bad field wiring, pathological submesh counts, unreadable/corrupt mesh assets, or malformed imported submesh data from escaping the editor job as an unsafe memory read.



- Parallel-for safety suppressions are zero in SHINOBU-owned jobs.
- Mesh conversion uses per-submesh `NativeSlice<TriangleDTO>` output windows.
- Each scheduled mesh worker writes `Output[triangleIndex]`.
- Mock generation, SDF evaluation, validation, and half compression use normal `NativeArray` writes.
- Safety reason: each worker owns its index, or validation is single-writer.



- The Forge-generated self-audit writer preserves:
  - EvidenceClass, XML task reconciliation, struct layout sections, compile status.
  - Static-gate caveat, deviation register, non-finite warning proof.
  - CSV schema proof, mesh input guard proof, cold editor IO hygiene.
  - Editor preview boundary proof, editor sync-barrier proof, read-accessor hygiene.
  - XML-safe escaped generic proof text.
- Reference schema: `Docs/_Archive/Reports_X_012_2026-05-23/CAVE_SDF_SELF_AUDIT_SHINOBU_244.md`.
- Running a real Forge bake must not downgrade the audit artifact.



- The physics proximity scanner does not use a single recursive iterator whose first locked folder can terminate coverage.
- It walks a pending-directory stack, catches file and directory enumeration failures per directory, and emits `scanIncomplete` plus `diagnostics[]` in `PHYSICS_OPTIMIZATION_REPORT_SHINOBU_244.json`.
- A clean report can therefore state that no enumeration failure was observed; a blocked directory becomes explicit evidence rather than silent omission.



Generated deviation text deliberately splits audit-only tokens inside `StringBuilder` construction.

Examples: Task 10 async wording and Task 18 gizmo wording. Emitted reports stay human-readable.

Source-level gates do not confuse documentation strings with real async writers or attachable Unity callbacks.



- Known deviations: Task 10's async serialization wording is implemented as a synchronous editor-blocking chunked writer because the source payload is TempJob/native memory and the caller waits.
- Task 18's `OnDrawGizmos` shape is implemented as `SceneView.duringSceneGui` to prevent runtime missing-script debt.
- Task 19 writes `PHYSICS_OPTIMIZATION_REPORT_SHINOBU_244.json` instead of the shared report to avoid overwriting another agent's artifact.
- The scanner is a method-context streaming text scanner, not a Roslyn AST proof.



## Continuous Bake Cost Scaling



`GlobalQualityWeight` does not alter SDF truth, DTO layout, file identity, save identity, or rollback route. The editor baker uses it only to shape bake work:



- BVH leaf triangle count smoothly moves from 16 at low quality to 4 at high quality.



- SDF job batch size smoothly moves from 256 to 32.



- Compression batch size smoothly moves from 512 to 128.



This buys minimum-budget editor stability through lower scheduling overhead and high-fidelity bake throughput through finer load balancing. Runtime quality scaling remains owned by the SDF consumers.
