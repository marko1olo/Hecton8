# SHINOBU_276 Exosuit 6DoF Kinematics



Owner: `SHINOBU_276`

Domain: Echelon 4 Player, Kinematics & Tools / Exosuit 6DoF Kinematics

Proof class: static source plus compile attempt. `Hecton8.Core.csproj` build is blocked before SHINOBU_276 diagnostics by external `CS2001` missing `Assets/_Project/Scripts/IBuildPlacementRule.cs`; Unity Play Mode and profiler proof remain pending.



## Authority Route



`ExosuitKinematicsRuntime` owns exosuit movement truth and stores it in `GlobalDataVault` generation handles.

`ExosuitStateDTO`: rollback authority row, explicit 64 bytes. Offsets: `AUP_Position@0`, `Velocity@24`, `AngularVelocity@36`, `ThrusterHeat@48`, `Flags@52`, `ReservedLock@56`, `_pad0@60`.



- All SHINOBU_276 Vault lanes are acquired with `SystemID.Physics`.

- `HectonPlayerMovement` no longer calls the exosuit runtime directly and never writes the Vault input row.

- It submits unmanaged pending intent through `Hecton8.Core.ExosuitKinematicAuthority`.
- Facade binds, reports authority, and unbinds only when input handle owner is `SystemID.Physics`.
- It clears pending DTO/sequence on bind transition and unbind.
- Submit/consume gate: `HasActiveAuthority()`.

- The runtime consumes that pending DTO and writes `BufferID.ShinobuExosuitFrameInput` in its owner phase, keeping the player bridge out of the job read window.

- Active authority suppresses:
  - legacy exosuit grapple Rigidbody force route;
  - jump-jet Rigidbody force route;
  - environment-handler force-buffer motor writes;
  - queued external kinematic velocity application to Rigidbody motor;
  - KCC sweep scheduling and wall-scrape feedback;
  - generic wipeout, damping, velocity-clamp mutators;
  - dynamic `CapsuleCollider` shape writes;
  - heavy-tow `Rigidbody.centerOfMass` writes;
  - wall-kick, voxel no-clip recovery, transport carrier, and ladder snap motor writes;
  - legacy exosuit foot support ray probes.

- Contact truth for the heavy suit comes from the byte-SDF integrator, not player Rigidbody corrections.



Hydraulic pressure and cockpit readback live in `ExoScreenDTO`; collision normals and push magnitudes live in `ExosuitSolverOutput`. They are diagnostics/presentation, not rollback truth.



## Execution



Runtime schedules one normal fixed-tick job: `ExosuitKinematicIntegrationJob`.

`GenerateMockExosuitInputsJob` remains for isolated/fuzzer use. Production procedural drift is folded into integrator through `ProceduralWeightMilli` to avoid tiny scheduled job.



- The integrator uses deterministic Burst, `[NoAlias]` lanes, `NativeArrayOptions.UninitializedMemory` Vault buffers, and AUP-local math: subtract `double3 CameraAup` from `double3 AUP_Position`, then cast the local delta to `float3`.

- Runtime buffer views and editor-facing tuning reads are resolved through `IDataVault.TryReadHandle` after owner-phase locks are acquired where applicable, avoiding generation-fault publication or resolve-counter mutation from read-shaped helpers.

- Public editor-facing readers also reject matching BufferIDs unless the resolved generation handle owner is `SystemID.Physics`, so stale/foreign rows fail closed.

- The same owner guard is applied to SHINOBU local handles before private buffer views or writer locks are accepted.

- Vault row mutations use `TryAcquireWriteLock`/`ReleaseWriteLock` with `SystemID.Physics`.
- Covered paths: cold emergency seed data, fixed-tick input/tuning/terrain staging, CSV ingestion, editor tuning writes.
- Fail-closed cases: missing, foreign-owned, empty, or locked row.

- Each fixed tick stages `ExosuitFrameInputDTO.GlobalQualityWeight` from `min(HomeostasisBrain.GlobalQualityWeight, ExosuitTuningDTO.GlobalQualityWeight)`, so global thermal quality is live while tuning remains a designer cap.

- Burst jobs then resolve quality from `min(input.GlobalQualityWeight, tuning.GlobalQualityWeight)`; `SanitizeQualityWeight` preserves finite values and `DefaultQualityWeight` is only invalid-data fallback.

- Scheduled solver admission uses `TryAcquireJobBufferViews`: mutable State/Input/Tuning/Output/Screen/Telemetry/Cursor/Footstep/Haptic/Silt/Acoustic lanes acquire writer locks and pass the returned arrays directly to Burst, while read-only Terrain/Flow/Crush lanes use read locks.

- Telemetry elapsed patching is routed through `TryOpenHeldJobWriteBuffer`, requiring `_jobBuffersLocked` plus the expected telemetry/cursor BufferIDs before mutation, before those local rows are released for readback/dumps.



- Collision consumes one owner-published Vault descriptor before scheduling.
- Voxel owner holds `BufferID.VoxelSdfPayloadDescriptor` write lock while refreshing `BufferID.VoxelSdfTexture3D`.
- It publishes `VoxelSdfPayloadDescriptorDTO[1]` at `BufferID` 620.
- Descriptor binds buffer id, generation, dimensions, rebased origin, cell size, range, owner, byte count, and flags.
- Payload size: 64 bytes, immutable.
- `ExosuitKinematicsRuntime`:
  - no longer imports `Hecton8.Caves`;
  - `TryAcquireVoxelSdfPayload` locks descriptor and byte buffer with `SystemID.Physics`;
  - reads both through generation handles;
  - requires descriptor/SDF handles to prove exact BufferID plus `SystemID.WorldStreaming`;
  - rejects zero or mismatched SDF generation;
  - rejects byte-count/owner mismatches;
  - falls back to `MockTerrainSDF` when no coherent SDF snapshot is proven.
- Vault read-lock acquire/release uses the same flat metadata route, so SDF read fences do not leak reader counts after unlock.
- Completed jobs release the external SDF descriptor/byte read locks before diagnostic dump IO; SHINOBU-owned telemetry/output rows remain locked only for same-phase readback.
- This preserves one owner for cave geometry and keeps `ExosuitStateDTO` unchanged.


Solver readback does not mutate `Transform.position`. Scene gizmos and editor labels are diagnostics over Vault readback; `ExosuitStateDTO.AUP_Position` remains the movement fact.



## Scalability



`GlobalQualityWeight` continuously scales solver fidelity:



- 0.0-0.3: 2 SDF substeps, wider SDF epsilon skin, nearest-neighbor voxel SDF sampling only, cheap radial normal fallback, reduced CCD contribution, heavier hydraulic latency feel.

- 0.4-0.7: substeps lerp through the middle budget, voxel sampling blends toward trilinear, and secondary probes blend in smoothly.

- 0.8-1.0: solver reaches editor/CSV `MaxSubsteps`, tight SDF epsilon, trilinear voxel SDF plus finite-difference normal blend, stronger CCD/secondary contact smoothing, richer haptic/silt/acoustic presentation.



Quality does not change DTO layout, save identity, rollback ownership, or authority route.



## Tooling



`ExosuitKinematicsTunerWindow` is UI Toolkit editor tooling.

- Writes `BaseMass`, `HydraulicLatencySeconds`, `ThrusterForce`, `ClampRange`, `GlobalQualityWeight`, `SdfEpsilonMeters`, `GravityMultiplier`, `MaxSubsteps`.
- Route: Vault tuning row through runtime writer-lock facade.
- Reads live labels through pure `TryReadTuning`.



- `Data/Physics/exosuit_performance_profiles.csv` is parsed once during cold Vault initialization into `ExosuitTuningDTO` by byte hashing and float parsing.
- File loads into Vault scratch through `Span<byte>` over native buffer while holding `ShinobuExosuitCsvScratch` writer fence.
- Parse uses `ReadOnlySpan<byte>`.
- Commit to `ShinobuExosuitTuning` occurs only while holding that row's writer fence.
- Both fences release in `finally`.
- The parser still avoids `string.Split` and managed byte-array copies.
- Periodic reload is editor-only behind `UNITY_EDITOR`; player/development fixed ticks do not perform file IO.



Mock/procedural input uses `Unity.Mathematics.Random` only. Seeds mix exosuit hash, AUP sector, frame, quality, and action mask. External authority disables procedural RNG.



`OnDrawGizmos` and the editor window render capsule bounds, desired velocity, and SDF normals from Vault readback.



## Diagnostics



- The black box is `ExosuitTelemetryEntry[300]` in `BufferID.ShinobuExosuitTelemetryRing`.
- It records AUP, velocity, heat, hydraulic pressure, SDF push, elapsed milliseconds, frame, flags, and state hash.
- Faults, non-finite state, and over-0.1 ms solver completions dump fixed rows to `Dump_SHINOBU_276.bin` and `Dump_EXO_KINEMATICS.bin`; one dump per frame.
- The duplicate guard is armed only after telemetry and cursor buffers resolve, so a failed resolve does not suppress a later same-frame fault/budget attempt.



Standalone helper jobs use the same deterministic Burst directive as the primary integrator.

They sanitize non-finite inputs through `ExosuitMathGuards` before SDF, pressure, clamp, or heat math.

They remain for tests/fallbacks; production owner phase schedules one integration job to avoid tiny-job overhead.



- `ExosuitLayoutVerifier.ValidateRuntimeLayouts()` validates state/input/tuning/signal/screen/terrain/output/telemetry sizes and editor offsets.
- `Exosuit_Physics_Inquisition`:
  - reports static pass/fail, not unconditional purge summary;
  - treats guarded legacy `ApplyExosuit*` and indirect motor-force routes as warnings only after active authority bypass;
  - increments the unguarded counter at scope exit for legacy method scopes without that guard.
- Player bridge guards more routes with the same authority decision.
- Covered: environment motor flushes, queued external kinematics, KCC sweep/wall-scrape feedback, damping/clamps.
- Also covered: dynamic collision/heavy-tow mutation, wall kick, voxel no-clip recovery, carrier motion, ladder snap, exosuit foot probes.
- Scanner upserts aggregate JSON node with source hash and UTC ticks.
- Write path: lock-file guarded read/modify/write plus temp-file atomic replace.
- Concurrent editor scanner nodes are not clobbered by direct `File.WriteAllText`.
- `Docs/Reports/SHINOBU_276_SELF_AUDIT.xml` contains the task reconciliation and byte-layout proof.
