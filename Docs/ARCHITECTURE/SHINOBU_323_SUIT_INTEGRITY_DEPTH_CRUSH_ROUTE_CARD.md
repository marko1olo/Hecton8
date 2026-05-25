# SHINOBU_323 Suit Integrity Depth Crush Route Card

- Route ID: `SHINOBU_323_SUIT_INTEGRITY_DEPTH_CRUSH_SOLVER`
- Date: 2026-05-22
- Owner: `SHINOBU_323`
- Domain: Echelon 5 Combat & Survival Physiology / Crush Depth Integrity
- Files: `ShinobuSuitIntegrityRuntime.cs`, `ShinobuSuitIntegrityJobs.cs`, `ShinobuSuitIntegrityData.cs`
- Status: `YELLOW / STATIC SOURCE VERIFIED / EXTERNAL COMPILE DEPENDENCY BLOCKS UNITY RUNTIME PROOF`

Problem: player suit and exoskeleton crush depth must not be represented by scene trigger volumes, `Physics.OverlapBox`, or player-script hardcoded depth damage.

## Authority Route

- Owner: `ShinobuSuitIntegrityRuntime` is the sole owner of the player suit pressure/integrity fact.
- Cold dependencies: `GlobalRegistry.DataVault`, `GlobalRegistry.Player`, and `GlobalRegistry.TickDispatcher` are cached only during bootstrap/service replacement.
- Hot data route reads borrowed read-only `BufferID.PlayerKinematicState` (`LockstepPlayerKinematicState[1]`) through cached `IDataVault.TryReadHandle`.
- Converts to `double3`.
- Subtracts sea level in double precision.
- Casts only relative depth to `float`.
- Target identity route: borrowed read-only `ShinobuMetabolismConstants.MetabolismStatesBuffer` supplies player `EntityHashID`.
- Buffer details: `MetabolicStateDTO[1]`, BufferID `70238`.
- Runtime stores Metabolic and cold player fallback hashes separately.
- `LockstepPlayerKinematicState` is 64 bytes, stores no identity field; `StableId` compatibility accessor is constant fallback only.
- Borrow binding route:
  - if descriptor is absent at boot, `BorrowVaultArray` may late-bind existing owner-created descriptor with `IDataVault.TryGetGenerationHandle`;
  - reads through `TryReadHandle`;
  - never allocates, locks, mutates, or releases foreign Kinematic/Metabolic buffers.
- Bootstrap fallback: cached `PlayerRuntimePoseSnapshot.Aup` remains only as a cold/service bridge when the Kinematic Vault fact is absent or not yet valid; it is not the preferred authority route.
- Naming note: active player-owned kinematic Vault fact is `LockstepPlayerKinematicState`.
- It is not the Physics/KCC `KinematicStateDTO` row used for separate KCC/debris domains.
- SHINOBU_323 reads player-owned Core fact; no sibling Physics/KCC assembly dependency or authority-lane mutation.
- Truth output: `SuitIntegrityDTO` in `GlobalDataVault` buffer `72510`.
- Damage route: catastrophic implosion emits unmanaged `SignalBus<CombatDamageSignal>` with `9999f` magnitude and `Pressure | MicroFracture` damage mask.
- Acoustic route: pre-failure groan emits unmanaged `SignalBus<MovementAcousticSignal>`; audio system owns playback.
- Presentation route: late-frame visual sync publishes scalar buckling data through `HectonShaderGlobalDataVaultBridge.PublishSuitCrushDearLie` slot `21`.

## Payload Contract

- BufferIDs: `72510..72517`, owned by `SystemID.GameplayPlayer`.
- `72510` `SuitIntegrityDTO[entityCapacity]`
- `72511` `SuitPressureProfileDTO[16]`
- `72512` `SuitIntegrityTuningDTO[1]`
- `72513` `SuitIntegrityTelemetryEntry[300]`
- `72514` `SuitIntegrityVisualDTO[entityCapacity]`
- `72515` `SuitHydrostaticMockAupDTO[300]`
- `72516` CSV scratch `byte[8192]`
- `72517` dump scratch `byte[19232]`
- Primary ABI: `SuitIntegrityDTO=32`: `CurrentIntegrity01@0`, `AppliedPressureATM@4`, `MicroFractureAccumulation@8`, `EquippedSuitHash@12`, `IntegrityFlags@16`, pads `20/24/28`.
- Supporting ABIs: `SuitPressureProfileDTO=64`, `SuitIntegrityTuningDTO=64`, `SuitIntegrityVisualDTO=32`, `SuitHydrostaticMockAupDTO=64`, `SuitIntegrityTelemetryEntry=64`.

All runtime DTOs are explicit-layout unmanaged rows. No `Pack=1`, managed references, hot-path properties, scene object references, or array-of-interface dispatch are part of the route.

## Phase And Dependency Route

- `SlowTick` consumes cached owned Vault handles plus borrowed read-only Kinematic/Metabolic descriptors and schedules `EvaluateHydrostaticPressureJob -> CalculateStructuralYieldJob`.
- `SlowTick` advances its accumulator from cached `ITickDispatcher.TimeSnapshot.Time` delta and honors `SimulationPaused`; no `Time.deltaTime` or hot `GlobalRegistry` polling is part of the timing route.
- `SlowTick` checks `SignalBus<CombatDamageSignal>.HasNativeStorage` and `SignalBus<MovementAcousticSignal>.HasNativeStorage` before opening legacy MPSC writers, preventing cold SignalBus initialization from the simulation path.
- The returned `JobHandle` is registered through `H8Memory.RegisterActiveJob(SystemID.GameplayPlayer, handle)`.
- `LateFrameTick` finalizes only completed jobs via `DispatcherJobFence.TryFinalizeCompleted`; no same-frame readback or arbitrary gameplay `.Complete()` is introduced.
- `OnDisable` may force-complete only for teardown and immediately releases Vault locks.
- Read accessors `TryGetIntegrity`, `TryGetVisual`, `TryGetLatestTelemetry`, and `TryGetTuning` use `IDataVault.TryReadHandle`, return copied DTOs, and fail closed while a job owns the buffers.
- Disable/DataVault hot-swap releases all SHINOBU_323 generation handles through `IDataVault.ReleaseBuffer` before cached descriptors are cleared.

## AUP And Fallback Route

- Valid Kinematic Vault state: pressure uses `seaLevelAup - playerAup` in `double3`.
- Missing Kinematic Vault state with valid cached player service snapshot: pressure uses the service snapshot as bootstrap/editor fallback.
- No valid Kinematic Vault state or player snapshot with mock enabled: synthetic 0m..8000m AUP samples drive stress testing.
- No valid Kinematic Vault state or player snapshot with mock disabled: pressure falls back to sea-level AUP so stale `_lastPlayerAup` cannot synthesize a false implosion.
- Non-finite player AUP or sea-level AUP input is detected before pressure conversion.
- Solver writes surface pressure for current row and raises `NonFinitePressure`.
- Telemetry/dump routing records faulty input instead of hiding it as harmless 1 ATM sample.
- Player damage target hash resolves from separate candidate caches.
- Order: `MetabolicStateDTO.EntityHashID`, Kinematic `StableId`, cold player service hash, deterministic fallback hash.
- Cold fallback can no longer block a later Metabolic override.

## Quality Scaling

- `GlobalQualityWeight` continuously maps cadence with `math.lerp(0.1f, 1.0f, 1.0f - quality)`.
- The elapsed `dt` integrated into fracture truth comes from dispatcher time, so stretched low-tier slow ticks conserve total stress instead of silently under-counting pressure exposure.
- Low: sparse slow ticks and conservative Dear Lie presentation amplitude.
- Middle: tighter cadence and branchless `smoothstep` blend toward stronger HUD/audio presentation.
- High: near-maximum cadence and richer Dear Lie shader response.
- Ultra: maximum cadence plus shader/HUD/audio overkill from the same scalar payload.
- Quality changes cadence and presentation richness only. It never changes DTO layout, authority owner, damage route, save identity, or rollback truth.

## Dear Lie Route

CPU does not deform meshes, mutate post-process volumes, spawn crack GameObjects, or rebuild visual hulls.

Solver publishes buckling, overpressure, integrity loss, and quality to shader global slot `21`.

`UberNoir`/HUD owns screen-edge warp, spider cracks, and concussive deformation illusion.

Complexity before Dear Lie:

- potential scene broadphase/mesh deformation route `O(trigger pairs + mesh vertices)`

Complexity after Dear Lie:

- `O(entityCount)` scalar Burst evaluation
- `O(1)` player-row presentation publish

## Fault Route

- `SuitIntegrityTelemetryEntry[300]` records frame, target hash, depth, ATM pressure, overpressure, microfracture, integrity, buckling, execution microseconds, flags, suit hash, tick interval, and signal flags.
- Dump triggers: non-finite AUP input, non-finite pressure, implosion, over-budget completion.
- Dump target: `Docs/AgentLogs/Dump_SHINOBU_323.bin`.
- `NonFinitePressure` is a current-frame volatile fault bit.
- It clears on recovered samples.
- `Imploded` remains sticky.
- Data Monolith readiness is not claimed here; cold CSV and defaults are the current bridge until `static_data.h8bin` imports the suit profile table.

## Proof

- Scanner: `Assets/_Project/Scripts/Physiology/Editor/OOP_Depth_Scanner.cs`.
- Dedicated report: `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_323.json`.
- Shared index: `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` key `shinobu323DepthCrushScanner`.
- Self audit: `Docs/Reports/SHINOBU_323_SELF_AUDIT.xml`.
- Static status: no runtime `CrushDepthTrigger`, `DepthDamage`, `Physics.OverlapBox`, or `OnTriggerStay` pressure authority found in the owned scan.
- Runtime proof still required: Unity import, Burst Inspector, profiler/GCMonitor, and player-build validation after CPU/dotnet compile gate clears.

Rejected alternatives: BoxCollider death zone, trigger callbacks, `Physics.OverlapBox`, direct health mutation, `Destroy(player)`, mesh dents, post-process edits, material clones, hot registry polling, stale AUP.
