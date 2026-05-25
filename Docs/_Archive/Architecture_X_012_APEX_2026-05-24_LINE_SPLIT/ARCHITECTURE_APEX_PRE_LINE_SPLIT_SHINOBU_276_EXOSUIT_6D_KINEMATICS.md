# [ARCHIVE] Pre-Line-Split Architecture Snapshot

Date: 2026-05-24
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Original: Docs/ARCHITECTURE/SHINOBU_276_EXOSUIT_6D_KINEMATICS.md
Rule: historical snapshot only; not active doctrine.

# SHINOBU_276 Exosuit 6DoF Kinematics



Owner: `SHINOBU_276`

Domain: Echelon 4 Player, Kinematics & Tools / Exosuit 6DoF Kinematics

Proof class: static source plus narrow compile attempt. `Hecton8.Core.csproj` build is blocked before SHINOBU_276 diagnostics by external `CS2001` missing `Assets/_Project/Scripts/IBuildPlacementRule.cs`; Unity Play Mode and profiler proof remain pending.



## Authority Route



`ExosuitKinematicsRuntime` owns exosuit movement truth and stores it in `GlobalDataVault` generation handles. `ExosuitStateDTO` is the rollback authority row: explicit 64 bytes, `double3 AUP_Position@0`, `float3 Velocity@24`, `float3 AngularVelocity@36`, `float ThrusterHeat@48`, `uint Flags@52`, `uint ReservedLock@56`, private `uint _pad0@60`.



- All SHINOBU_276 Vault lanes are acquired with `SystemID.Physics`.

- `HectonPlayerMovement` no longer calls the exosuit runtime directly and never writes the Vault input row.

- It submits an unmanaged pending intent row through `Hecton8.Core.ExosuitKinematicAuthority`; that facade binds, reports authority, and unbinds only when the input handle owner is `SystemID.Physics`, clears pending DTO/sequence on every bind transition and unbind, and gates submit/consume through `HasActiveAuthority()`.

- The runtime consumes that pending DTO and writes `BufferID.ShinobuExosuitFrameInput` in its owner phase, keeping the player bridge out of the job read window.

- When that authority is active, legacy exosuit grapple and jump jet Rigidbody force routes are bypassed, environment-handler force buffers are consumed without motor writes, queued external kinematic velocity is not applied to the Rigidbody motor, KCC sweep scheduling/wall-scrape feedback is suppressed, generic wipeout/damping/velocity clamp mutators are skipped, dynamic collision `CapsuleCollider` shape writes, heavy-tow `Rigidbody.centerOfMass` writes, wall-kick motor/force writes, voxel no-clip recovery motor writes, transport carrier motor writes, and ladder snap motor writes are suppressed, and legacy exosuit foot support ray probes are disabled.

- Contact truth for the heavy suit comes from the byte-SDF integrator, not player Rigidbody corrections.



Hydraulic pressure and cockpit readback live in `ExoScreenDTO`; collision normals and push magnitudes live in `ExosuitSolverOutput`. They are diagnostics/presentation, not rollback truth.



## Execution



Runtime schedules one normal fixed-tick job: `ExosuitKinematicIntegrationJob`. The previous standalone mock input stage is retained as `GenerateMockExosuitInputsJob` for isolated/fuzzer use, but production procedural drift is folded into the integrator through `ProceduralWeightMilli` to avoid a tiny scheduled job.



- The integrator uses deterministic Burst, `[NoAlias]` lanes, `NativeArrayOptions.UninitializedMemory` Vault buffers, and AUP-local math: subtract `double3 CameraAup` from `double3 AUP_Position`, then cast the local delta to `float3`.

- Runtime buffer views and editor-facing tuning reads are resolved through `IDataVault.TryReadHandle` after owner-phase locks are acquired where applicable, avoiding generation-fault publication or resolve-counter mutation from read-shaped helpers.

- Public editor-facing readers also reject matching BufferIDs unless the resolved generation handle owner is `SystemID.Physics`, so stale/foreign rows fail closed.

- The same owner guard is applied to SHINOBU local handles before private buffer views or writer locks are accepted.

- Cold emergency seed data, fixed-tick input/tuning/terrain staging, CSV ingestion, and editor-facing tuning writes mutate Vault rows only through `TryAcquireWriteLock`/`ReleaseWriteLock` with `SystemID.Physics`; they fail closed if the row is missing, foreign-owned, empty, or locked.

- Each fixed tick stages `ExosuitFrameInputDTO.GlobalQualityWeight` from `min(HomeostasisBrain.GlobalQualityWeight, ExosuitTuningDTO.GlobalQualityWeight)`, so global thermal quality is live while tuning remains a designer cap.

- Burst jobs then resolve quality from `min(input.GlobalQualityWeight, tuning.GlobalQualityWeight)`; `SanitizeQualityWeight` preserves finite values and `DefaultQualityWeight` is only invalid-data fallback.

- Scheduled solver admission uses `TryAcquireJobBufferViews`: mutable State/Input/Tuning/Output/Screen/Telemetry/Cursor/Footstep/Haptic/Silt/Acoustic lanes acquire writer locks and pass the returned arrays directly to Burst, while read-only Terrain/Flow/Crush lanes use read locks.

- Telemetry elapsed patching is routed through `TryOpenHeldJobWriteBuffer`, requiring `_jobBuffersLocked` plus the expected telemetry/cursor BufferIDs before mutation, before those local rows are released for readback/dumps.



- Collision consumes one owner-published Vault descriptor before scheduling.
- The voxel owner holds the `BufferID.VoxelSdfPayloadDescriptor` write lock while refreshing `BufferID.VoxelSdfTexture3D`, then publishes `VoxelSdfPayloadDescriptorDTO[1]` at `BufferID` 620; the descriptor binds buffer id, buffer generation, dimensions, rebased runtime origin, cell size, range, owner, byte count, and validity flags into one 64-byte immutable payload.
- `ExosuitKinematicsRuntime` no longer imports `Hecton8.Caves`; `TryAcquireVoxelSdfPayload` locks the descriptor and byte buffer with `SystemID.Physics`, reads both through generation handles, requires descriptor and SDF handles to prove exact BufferID plus `SystemID.WorldStreaming`, rejects zero or mismatched SDF generation, rejects byte-count/owner mismatches, and falls back to `MockTerrainSDF` when the descriptor cannot prove a coherent SDF snapshot.
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



`ExosuitKinematicsTunerWindow` is UI Toolkit editor tooling. It writes `BaseMass`, `HydraulicLatencySeconds`, `ThrusterForce`, `ClampRange`, `GlobalQualityWeight`, `SdfEpsilonMeters`, `GravityMultiplier`, and `MaxSubsteps` into the Vault tuning row through the runtime writer-lock facade; it reads live labels through the pure `TryReadTuning` facade.



`Data/Physics/exosuit_performance_profiles.csv` is parsed once during cold Vault initialization into `ExosuitTuningDTO` by byte hashing and float parsing. The file is loaded into Vault scratch through `Span<byte>` over the native buffer while holding the `ShinobuExosuitCsvScratch` writer fence, then parsed as `ReadOnlySpan<byte>` and committed to `ShinobuExosuitTuning` only while holding that row's writer fence. Both fences release in `finally`. The parser still avoids `string.Split` and managed byte-array copies. Periodic reload is editor-only behind `UNITY_EDITOR`; player/development fixed ticks do not perform file IO.



Mock/procedural input generation uses `Unity.Mathematics.Random` only. Seeds mix stable exosuit source hash, kilometer-quantized AUP sector hash, frame, quality, and action mask. External player authority disables procedural RNG for normal runtime control.



`OnDrawGizmos` and the editor window render capsule bounds, desired velocity, and SDF normals from Vault readback.



## Diagnostics



The black box is `ExosuitTelemetryEntry[300]` in `BufferID.ShinobuExosuitTelemetryRing`. It records AUP, velocity, heat, hydraulic pressure, SDF push, elapsed milliseconds, frame, flags, and state hash. Faults, non-finite state, and over-0.1 ms solver completions dump fixed-size rows to `Docs/AgentLogs/Dump_SHINOBU_276.bin` and `Docs/AgentLogs/Dump_EXO_KINEMATICS.bin`; budget breaches first patch `ExosuitStateFlags.BudgetExceeded` and `SolverComputeTimeMs`, then use the same one-dump-per-frame guard as fault dumps. The duplicate guard is armed only after telemetry and cursor buffers resolve, so a failed resolve does not suppress a later same-frame fault/budget attempt.



Standalone helper jobs use the same deterministic Burst directive as the primary integrator and sanitize non-finite inputs through `ExosuitMathGuards` before SDF, pressure, clamp, or heat math. They are retained for tests/fallbacks, while the production owner phase still schedules the single integration job to avoid tiny-job overhead.



- `ExosuitLayoutVerifier.ValidateRuntimeLayouts()` validates state/input/tuning/signal/screen/terrain/output/telemetry sizes and editor offsets.
- `Exosuit_Physics_Inquisition` reports a static pass/fail verdict instead of an unconditional purge summary; guarded legacy `ApplyExosuit*` and indirect motor-force routes are warning data only after the same method scope has passed the active authority bypass, and legacy method scopes without that guard now increment the unguarded counter at scope exit.
- The player bridge now additionally guards environment motor flushes, queued external kinematics, KCC sweep/wall-scrape feedback, generic damping/clamps, dynamic collision/heavy-tow physics mutation routes, wall kick, voxel no-clip recovery, transport carrier motion, ladder snap, and exosuit foot probes with the same authority decision.
- The scanner upserts its aggregate JSON node with source hash and UTC ticks through a lock-file guarded read/modify/write and temp-file atomic replace, so concurrent editor scanner nodes are not clobbered by direct `File.WriteAllText`.
- `Docs/Reports/SHINOBU_276_SELF_AUDIT.xml` contains the task reconciliation and byte-layout proof.