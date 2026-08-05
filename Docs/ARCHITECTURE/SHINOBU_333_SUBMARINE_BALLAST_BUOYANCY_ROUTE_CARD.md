# SHINOBU_333 Submarine Ballast Buoyancy Route Card

Status: `STATIC_ROUTE_DOC / RUNTIME_PROOF_PENDING`
Evidence class: `STATIC_DOC / STATIC_SOURCE`
Owner domain: vehicles/submarine ballast and buoyancy
Review disposition: `YELLOW / STATIC_DOC_ONLY` until compile/import/runtime/profiler/player proof exists.

- Date: 2026-05-22 Owner: `SHINOBU_333 / SUBMARINE_BALLAST_BUOYANCY_SOLVER` Domain: Echelon 6 Vehicles & Kinematics / submarine ballast,
- displacement,
- and pressure-gated buoyancy Status: STATIC SOURCE,
- FULL BUILD BLOCKED OUTSIDE DOMAIN Review Disposition: `YELLOW / STATIC_SOURCE_ONLY` Required Promotion Proof: Unity import,
- green compile,
- Play Mode solver tick,
- GC allocation trace,
- profiler timing,
- and Data Monolith payload validation.

## Authority

- One fact: ballast-tank water volume, compressed-air pressure, displaced hull volume, and the derived vertical buoyancy force.
- One owner: `SubmarineAutoLevelBallastController` owns the current compatibility bridge and Vault lanes until a dedicated vehicle dynamics owner absorbs ballast scheduling.
- One route: `GlobalDataVault` rows -> `EvaluateBallastTanksJob` -> `CalculateBuoyancyForceJob` -> `PhysicsForceRouter.QueueAmbientForce`.
- One proof artifact: `SubmarineBallastTelemetryEntry[300]` plus `Docs/AgentLogs/Dump_SHINOBU_333.bin` on non-finite or >500 us state.

Forbidden ballast routes:

- `Rigidbody.mass` mutation.
- Direct `Rigidbody.AddForce`.
- Water `Physics.OverlapSphere`.
- `GlobalRegistry` polling inside Burst jobs.

SHINOBU_332 suppression:

- Read source: owner counter `BufferID.Shinobu332GyroCounters`.
- Access: cached read-only Vault handle.
- SHINOBU_333 does not own, allocate, or release that buffer.

## Force Dispatch Decision

- The XML prompt names a `NativeQueue<ForcePacketDTO>.ParallelWriter` for `PhysicsApplySystem`.
- The current vehicle path already centralizes Unity rigidbody force application through `PhysicsForceRouter`, and `SubmarineAutoLevelBallastController` is already registered in fixed/post-fixed phases.
- Adding a second unmanaged force queue here would create a second apply owner for the same vertical force before the project exposes a Vehicles-owned `ForcePacketDTO` contract.

Chosen route:

- `EvaluateBallastTanksJob` mutates only `BallastTankDTO` rows and sparse acoustic signals.
- `CalculateBuoyancyForceJob` writes one `SubmarineBallastForcePacketDTO` row and one telemetry ring row.
- `PostFixedTick` completes through `DispatcherJobSwap.TryComplete`; only completed valid packets are converted into the existing force-router call.

This preserves the no-direct-physics-mutation boundary while avoiding an invented sibling-domain dependency.

## Buffer IDs

- `71771 Shinobu333BallastTanks` - `BallastTankDTO[4]`, 32-byte tank rows, `UninitializedMemory`, seeded during cold/native-state setup.
- `71772 Shinobu333BallastCommands` - `BallastTankCommandDTO[4]`, 32-byte command rows, `UninitializedMemory`, overwritten before scheduling.
- `71773 Shinobu333BallastFluidSamples` - `SubmarineBallastFluidSampleDTO[1]`, 160-byte AUP/depth/density row, `UninitializedMemory`.
- `71774 Shinobu333BallastForcePackets` - `SubmarineBallastForcePacketDTO[1]`, 128-byte force packet, `UninitializedMemory`.
- `71775 Shinobu333BallastTelemetryRing` - `SubmarineBallastTelemetryEntry[300]`, 64-byte black-box ring.
- `71776 Shinobu333BallastProfiles` - `SubmarineBallastProfileDTO[64]`, 64-byte cold CSV profile rows.
- `71777 Shinobu333BallastTuning` - `SubmarineBallastTuningDTO[1]`, 64-byte editor/cold tuning row.
- `71778 Shinobu333BallastCsvScratch` - cold CSV byte scratch.

The rejected draft range `71820..71827` collided with SHINOBU_264 async buoyancy readback ownership `71820..71831`.

## Accessor Purity

Cold setup uses `Ensure*Cold`/Vault ensure paths while buffers can still be created.

Hot ballast mutation paths use cached `VaultGenerationHandle<T>` descriptors and `TryAcquireWriteLock` with `finally` release. If a write lock, compaction fence, or handle view fails, the frame fails closed and records the fault in PID telemetry.

They do not call `TryGetGenerationHandle`, allocate, or grow Vault buffers.

- Read-looking APIs in the SHINOBU_333 hot path do not publish signals, search the scene, complete jobs, or mutate global state.
- External readback `BallastFill01` and SHINOBU_332 suppression read the cached Vault route through `TryReadOnlyHandle`; unavailable or fenced memory returns default/inactive.
- Owner-internal observation paths for ballast tanks, tank positions, fill rows, PID telemetry, and ballast telemetry now use read-only Vault views.
- Mutable `TryResolveMutableVaultBuffer` is restricted to job output buffers and is reachable only through lock-held helpers for PID output, flood mass output, and ballast force packets.
- Completion remains explicit in `CompleteBallastSolverJob`, not hidden behind a getter.
- Ballast, PID-output, and dynamic-flood output jobs hold Vault write locks only across their scheduled mutation window, then release them in completion/dispose cleanup.
- `GlobalQualityWeight` and runtime-origin AUP are refreshed in owner/cold callbacks (`Awake`, `OnEnable`, slow tick, scalability/origin events, and DataVault replacement).
- The fixed ballast sample path reads the cached scalar/AUP only.
- The hot PID suppression path no longer calls `SubmarineDynamicsRuntime`; it resolves a cached SHINOBU_332 Vault counter handle and fails inactive if the owner buffer is unavailable.

## CSV And Data Monolith

Cold source path: `Data/Physics/vehicle_ballast_profiles.csv`.

Ingestion route:

`FileStream` sequential cold read -> Vault scratch `71778` -> `ReadOnlySpan<byte>` -> `SubmarineBallastCsvParser.ParseProfiles` -> Vault profile rows `71776`.

Parser uses FNV-1a hashes and manual float parsing. No `float.Parse`, LINQ, or runtime `ScriptableObject` lookup.

Data Monolith state:

- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is present in this workspace.
- Current size: `7,457,664` bytes, mtime 2026-06-07, measured 2026-08-05 (supersedes earlier recorded `1,804,864` bytes).
- Route card remains yellow until H8DM boot validation is linked.
- CSV bridge remains a cold fallback/source ingest path.

## Layout Proof

`BallastTankDTO` is explicit 32 bytes:

- `TankVolumeLiters float` at `0..3`
- `CurrentWaterLiters float` at `4..7`
- `CompressedAirPressureATM float` at `8..11`
- `InputStateFlags uint` at `12..15`
- `PumpRateLitersPerSecond float` at `16..19`
- `_pad0 uint` at `20..23`
- `_pad1 uint` at `24..27`
- `_pad2 uint` at `28..31`

All fields are 4-byte aligned.

Total size: `32`; two rows fit exactly in one 64-byte L1 cache line.

Solver writes different tank rows in parallel. No shared atomic counter is colocated here, so no 64-byte counter padding is required.

## Scalability

- `GlobalQualityWeight` is consumed as a continuous `0..1` scalar.
- The owner phase maps the scalar to a maximum `1..4` analytical sample budget without platform booleans.
- `CalculateBuoyancyForceJob` consumes `SubmarineBallastFluidSampleDTO.ActiveSampleBudget`: weak devices evaluate the center submerged-ratio only, middle devices add weighted bow/stern approximations, and high/ultra paths average up to four analytical sample points.
- The next analytical sample is fractionally weighted from the continuous scalar, so force output does not jump just because a budget lane becomes available.
- Quality changes sample count and presentation richness only.
- It does not change BufferID identity, save identity, or force ownership.

## Dear Lie

Compressed-air blowdown is not simulated as particles, bubbles, or fluid turbulence.

Truth path: liter integration plus two pressure comparisons.

Sensory lie: sparse `MovementAcousticSignal` on deterministic modulo cadence, volume scaled by pressure delta and released liters.

CPU complexity before the lie would be `O(tanks * bubbles * audio voices)`. Current gameplay truth is `O(tanks)` and acoustic presentation is bounded sparse signal emission.

## Fault And Blackbox

- `SubmarineBallastTelemetryEntry[300]` records frame, flags, hash, net force, buoyant force, ballast gravity.
- It also records water liters, compressed-air mass, ambient pressure, displaced volume, submerged ratio.
- It also records timing-proxy microseconds, quality, active sample count, entity hash, ring cursor.
- PID blackbox `SubmarinePidTelemetryEntry[300]` writes `LastVaultFaultCode`, `LastVaultFaultBufferId`, and `LastVaultFaultFrame`; dump path is `Docs/AgentLogs/Dump_1420_SubmarineNavigation.bin`.
- Current `ComputeMicros` is schedule-to-completion owner timing and is explicitly flagged with `ForceFlagTimingProxy`; exact Burst wall-time promotion requires profiler instrumentation.
- Non-finite force or >500 us timing proxy writes `Docs/AgentLogs/Dump_SHINOBU_333.bin`.

## Verification Boundary

- Static scans show zero targeted dynamic `Rigidbody.mass` ballast writes, zero targeted water `Physics.OverlapSphere` hacks, and zero targeted direct `AddForceAtPosition` sites.
- Full compile remains blocked by sibling-domain errors and one later `csc.exe` exit `-1` without source diagnostics; no SHINOBU_333 source diagnostics appeared after generated-project source inclusion.
- Scanner counts both `Physics.OverlapSphere` and `Physics.OverlapSphereNonAlloc` as forbidden water-volume broadphase routes; non-allocating variant is still OOP water-query, not ballast solver route.

Metadata audit, 2026-05-23:

- Initial gap: missing sidecars for runtime contract, OOP scanner, and CSV source.
- Current state: stable `.meta` files exist for all three.
- GUID uniqueness: verified by source scan.
- `Data/Physics/vehicle_ballast_profiles.csv`: non-Unity external cold source.
- CSV sidecar means repository identity hygiene, not Unity import proof.

- Assembly boundary scan on 2026-05-23 found no SHINOBU-owned runtime asmdef under `Assets/_Project/Scripts/Physics/Vehicles`; only `Hecton8.Physics.Vehicles.Editor.asmdef` exists and remains editor-only.
- SHINOBU_333 did not add or modify runtime asmdef references.
- Runtime files currently compile through existing root `Hecton8.Core.asmdef`.
- Separate vehicle runtime assembly is deferred to integrator-owned boundary migration, not forced from this lane.

Independent read-only hot-path audit, 2026-05-23:

- No remaining fixed/post-fixed calls to `SubmarineDynamicsRuntime`.
- No live global quality reads.
- No live runtime-origin AUP reads.
- No `GlobalRegistry` or scene search in the patched hot path.
- SHINOBU_332 gyro suppression: read-only/non-owning via cached `BufferID.Shinobu332GyroCounters`.
- Added methods: no obvious compile or allocation hazard found.

Cached-handle hardening on 2026-05-23 removed last `TryGetGenerationHandle` fallback from `TryReadVaultBuffer`.

Generation-handle refresh stays in cold `Ensure*Cold` or external snapshot paths. Fixed/post-fixed reads fail closed until owner refresh.
