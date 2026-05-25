# SHINOBU_345 Celestial Orbit Route Card



Date: 2026-05-23

Owner: `SHINOBU_345 / CELESTIAL_ORBIT_TRIG_CALCULATOR`

Owner domain: Echelon 7 Atmosphere & Celestial

Owning file/system: `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs`

Status: `YELLOW_STATIC_SOURCE`



## Problem



Forbidden owners:

- Unity object transforms.
- `Transform.Rotate()`.
- Physical light rotation.
- `Mathf.Sin(Time.time)`.
- Physics raycasts.

Required outputs: deterministic sun/moon vectors, eclipse scalar, tide vector, and forensic telemetry for atmosphere, GI, tide, predator, solar, shader, and editor-debug consumers. No second celestial truth owner.



## Route Card



```text

Route ID: SHINOBU_345_CELESTIAL_ORBIT_VAULT

Date: 2026-05-23

Owner: SHINOBU_345 / HectonSeismicTideDirector

Owner domain: Echelon 7 Atmosphere & Celestial

Owning file/system: Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs



Problem:

  Object-driven day/night mechanics invalidate transforms, break deterministic rollback, and make eclipse/tide truth depend on presentation state.

Why owner-local data is insufficient:

  GI relay, shaders, tide/seismic, solar, predator behavior, editor x-ray, and telemetry need the same immutable celestial truth.

Why direct caller/owner interface is insufficient:

  Consumers cross rendering, gameplay, and editor phases; Burst jobs and GI relay need generation-checked native rows, not a managed interface call.



Instrument:

  [ ] GlobalRegistry cold service/interface

  [x] SignalBus<T> first-party broadcast

  [ ] GlobalSignals bridge/direct queue

  [ ] HectonEventBus mod/API/cold event

  [x] GlobalDataVault / IDataVault

  [x] Black-box/telemetry route



- Producer/consumer phase: Producer: FrostTick/slow owner cadence in HectonSeismicTideDirector.
- Consumers: VISUAL_SYNC shader globals, `HectonCelestialEngine` cached read-only Vault handles, GI relay Visual Sync, tide/seismic owner state, solar/predator consumers through Vault/signal payloads.
- Cadence/capacity: Celestial solve interval `lerp(0.1s, 1.0s, 1 - GlobalQualityWeight)`.
- `CelestialStateDTO[1]`, `EnvironmentStateDTO[1]`, `CelestialTelemetryEntry[300]`, presentation blackbox `[300]`, three `float4[8]` atmosphere LUT lanes, and opt-in legacy fallback output `[1]`.
- Expected max events/reads per frame: 1 owner write per accepted solve, 1 runtime snapshot publish per owner publish, max 1 eclipse gameplay signal on state transition.
- GlobalQualityWeight behavior: Quality changes active harmonics, polynomial blend, solve cadence, and presentation richness only.
- It does not change BufferIDs, DTO layout, save identity, signal identity, or authority route.


Accessor purity:

  [x] No Get/TryGet/Resolve/Read API publishes signals

  [x] No Get/TryGet/Resolve/Read API syncs scene state

  [x] No Get/TryGet/Resolve/Read API allocates/grows buffers

  [x] No Get/TryGet/Resolve/Read API completes jobs

  [x] No Get/TryGet/Resolve/Read API mutates global state

  [x] No Get/TryGet/Resolve/Read API searches the scene



- Payload/data shape: managed fields `0`; `UnityEngine.Object` fields `0`.
- `CelestialStateDTO`: `64` bytes.
  - offset `0`: `double3 SunDirection`, `24` bytes;
  - offset `24`: `double3 MoonDirection`, `24` bytes;
  - offset `48`: `float EclipseShadowScalar01`, `4` bytes;
  - offset `52`: `float TimeOfDay01`, `4` bytes;
  - offset `56`: `uint _pad0`, `4` bytes;
  - offset `60`: `uint _pad1`, `4` bytes.
- `EnvironmentStateDTO`: `64` bytes.
  - offset `0`: `double3 TideVector`, `24` bytes;
  - offset `24`: `double CurrentSimulationTime`, `8` bytes;
  - offset `32`: `float GlobalTideLevel`, `4` bytes;
  - offset `36`: `float SeismicTremorIntensity`, `4` bytes;
  - offset `40`: `uint ActiveEventFlags`, `4` bytes;
  - offset `44`: `uint Frame`, `4` bytes;
  - offset `48`: `float TideDerivative`, `4` bytes;
  - offset `52`: `float GlobalQualityWeight`, `4` bytes;
  - offset `56`: `uint Sequence`, `4` bytes;
  - offset `60`: `uint _pad0`, `4` bytes.
- `CelestialTelemetryEntry`: `64` bytes; black-box ring `300` rows.
- Overflow/failure: Non-finite direction/tide/eclipse sets `CelestialEventFlagNonFinite`, clamps safe fallback vectors, and dumps telemetry.
- If a new FrostTick solve is scheduled but not finalized, owner publish reuses the last finite `Shinobu345CelestialStateRead` / `Shinobu345EnvironmentState` / `Shinobu345CelestialFlowModifiers` snapshot through a pure read-only cached-solve helper.
- Hardcoded emergency vectors are first-boot/no-valid-snapshot fallback only.


Telemetry fields:

  Frame, SunAngleRadians, EclipseShadowScalar01, SeismicTremorIntensity, ActiveEventFlags, ActiveHarmonics, CurrentSimulationTime, SolverComputeTimeMs, GlobalQualityWeight, TideVectorMagnitude, Sequence, StateHash.

Black-box fields:

  `CelestialTelemetryEntry[300]` dumped to `Docs/AgentLogs/Dump_SHINOBU_345.bin`.

Profiler marker:

  Stopwatch proxy currently recorded around dispatcher job schedule/finalize window; exact Burst-worker timing remains Unity Profiler/Burst Inspector proof debt.

GC proof required:

  Unity Play Mode + GCMonitor 0 B/frame for owner solve/publish path; static proof only exists today.



- Shutdown/disposal: Owner finalizes outstanding jobs through dispatcher fence and releases Vault locks before disable/destroy.

- Scene unload behavior: Vault rows remain DataVault-owned; owner stores generation descriptors and does not free cross-domain truth directly.

- Stale-handle behavior: Exact BufferID, nonzero generation, successful resolve/read handle, `IsCreated`, and required length are checked before pointer export or borrowed read.

- Stale or undersized presentation scratch descriptors are repaired only by the cold lifecycle helper, not by cadence read helpers.

- Borrowed presentation route: `HectonCelestialEngine` caches `IDataVault`, `Shinobu345CelestialStateRead`, and `Shinobu345EnvironmentState` handles during cold lifecycle.

- Its cadence reads use `TryReadOnlyHandle` against those cached descriptors and fail closed to the legacy fallback path; no `GlobalRegistry.CelestialRuntimeSnapshot` poll remains in the celestial presentation cadence.

- Presentation owned scratch: `HectonCelestialEngine` stores only `VaultGenerationHandle<T>` descriptors for its presentation blackbox, atmosphere gradient LUTs, and opt-in legacy orbit fallback output.

- `RefreshColdRuntimeDependencies` is the only path that calls `EnsureGenerationHandle` for these rows; cadence helpers use `TryResolveExistingCelestialPresentationBuffer` and fail closed without allocation or growth.

- Blackbox reset and gradient LUT rebuilds are explicit cold/command writes (`ResetCelestialBlackBoxState`, `RefreshAtmosphereGradientSamplesIfDirty`), never hidden inside `TryResolve*`, `Resolve*`, or sampler helpers.

- The fallback async output row is locked before scheduling and unlocked after dispatcher finalization.

- Candidate lanes `73373..73377` were rejected after collision with SHINOBU_354, so the accepted presentation scratch range is `73393..73397`.

- Fallback ABI note: `CinematicOrbitState` is explicit 32 bytes (`float3` offsets 0/12, scalar offsets 24/28), `CelestialBlackBoxEntry` is explicit 64 bytes, and `CelestialOrbitJobOutput` is explicit 192 bytes over `CelestialRuntimeSnapshot` plus padding.

- The fallback route remains opt-in and consumer-side; primary celestial truth stays in the 64-byte Vault DTOs.

- Owner read surface: `HectonSeismicTideDirector` readbacks for celestial state, environment state, celestial flow, seismic/celestial tuning, and water surface AUP now use `TryReadOnlyVaultBuffer<T>` over `IDataVault.TryReadOnlyHandle`.

- Mutable owner views are reserved for write/publish/CSV/editor ensure phases, and allocation-capable editor tuning helpers use `Ensure*` names.



Rejected alternatives:

  [x] owner-local field

  [x] cached owner interface

  [x] existing SignalBus lane

  [x] existing Vault buffer

  [x] cold HectonEventBus hook

  [ ] no global route needed



- Why this does not increase global monolith risk: The route exposes one cache-line optics truth, one cache-line environment truth, typed transition signal, and one telemetry ring.
- It does not add a new registry service, managed event bus, or physical light authority.
- H-Phi impact expected: Positive only by deleting duplicate object sun authority; H-Phi is not used as approval evidence.
- Proof required before GREEN: Unity import, Console clean, Burst Inspector, GCMonitor 0 B/frame.
- Also required: Frame Debugger shader global proof and profiler timing under 0.1 ms.
- Also required: Unity execution of `OOP_Sun_Scanner` and external compile wall cleared.
- Reviewer: Pending Integrator/Architecture review.
- Review disposition: YELLOW Status: ACCEPTED_STATIC_SOURCE / RUNTIME_PROOF_PENDING
```


## Vault Buffers



- `73350` `Shinobu345TideTelemetry` - legacy tide telemetry rows.

- `73351` `Shinobu345SeismicEvents` - seismic event rows.

- `73352` `Shinobu345ShakeOffsets` - visual shake offset row.

- `73353` `Shinobu345TurbiditySpikes` - silt/turbidity scalar row.

- `73354` `Shinobu345SeismicTelemetryRing` - seismic blackbox ring.

- `73355` `Shinobu345SeismicTuning` - seismic tuning row.

- `73356` `Shinobu345MockNarrativeTriggers` - cold mock narrative trigger row.

- `73357` `Shinobu345MockCameraPositions` - cold mock camera AUP row.

- `73358` `Shinobu345MockSiltSignals` - mock silt signal row.

- `73359` `Shinobu345MockBaseModules` - mock base module rows.

- `73360` `Shinobu345CelestialStateWrite` - owner write `CelestialStateDTO[1]`.

- `73361` `Shinobu345CelestialStateRead` - immutable reader `CelestialStateDTO[1]`.

- `73362` `Shinobu345CelestialTelemetryRing` - `CelestialTelemetryEntry[300]`.

- `73363` `Shinobu345CelestialTuning` - `CelestialTuningDTO[1]`.

- `73364` `Shinobu345CelestialCsvScratch` - cold CSV scratch bytes.

- `73365` `Shinobu345CelestialFlowModifiers` - `CelestialFlowModifierDTO[1]`.

- `73366` `Shinobu345CelestialMockTimeline` - double mock timeline row.

- `73367` `Shinobu345CelestialOrbitalParameters` - `CelestialOrbitalParameterDTO[8]`.

- `73368` `Shinobu345EnvironmentState` - `EnvironmentStateDTO[1]`.

- `73369` `Shinobu345SeismicStates` - seismic state rows.

- `73370` `Shinobu345WaterSurfaceAupY` - tide water-surface AUP-Y scalar.

- `73371` `Shinobu345SeismicFaultProfiles` - cold fault profile rows.

- `73372` `Shinobu345SeismicCsvScratch` - cold seismic CSV scratch bytes.

- `73393` `Shinobu345CelestialPresentationBlackBox` - presentation-side `CelestialBlackBoxEntry[300]`.

- `73394` `Shinobu345CelestialGradientDay` - `float4[8]` day atmosphere visual LUT.

- `73395` `Shinobu345CelestialGradientSunset` - `float4[8]` sunset atmosphere visual LUT.

- `73396` `Shinobu345CelestialGradientNight` - `float4[8]` night atmosphere visual LUT.

- `73397` `Shinobu345CelestialLegacyOrbitOutput` - opt-in legacy fallback `CelestialOrbitJobOutput[1]`.



## Dear Lie



CPU output:

- Normalized sun/moon directions.
- Eclipse scalar.
- Tide vector.
- No light rotation, raycasts, moon-geometry simulation, or water-mesh deformation.

Rendering consumers draw discs, caustic/specular response, GI darkening, and planet shine from cached Vault vectors/scalars plus shader globals.



Complexity before the lie: `O(objects * frame)` transform/light updates plus possible ray/physics checks.

Complexity after the lie: `O(activeHarmonics)` per FrostTick solve, then constant-size shader global/Vault publish.



## Review



- Global authority review:
  - Result: `YELLOW`
  - Route ID: `SHINOBU_345_CELESTIAL_ORBIT_VAULT`
  - Owner: `HectonSeismicTideDirector`
  - Instrument: `GlobalDataVault`, `SignalBus<EclipseGameplayEventPayload>`, shader globals, telemetry dump
  - Producer/consumer: FrostTick owner solve -> Visual Sync/gameplay consumers
  - Cadence/capacity: `0.1..1.0s` continuous solve interval
- 1 state row
- 300 telemetry rows
- Overflow/failure:
  - finite guards
  - cached valid snapshot while a new solve is in flight
  - emergency vectors only when no finite snapshot exists
  - non-finite flag and dump
- Shutdown/disposal: dispatcher fence completion and Vault unlock before teardown.
- Proof required before GREEN: Unity import/Console, Burst Inspector, GCMonitor/profiler, Frame Debugger, player build.
- Reason: static source route is narrow and current.
- but runtime proof is absent and project build is blocked by unrelated compile-wall dependencies.
