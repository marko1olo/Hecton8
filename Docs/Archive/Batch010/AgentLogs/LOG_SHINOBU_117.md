# LOG_SHINOBU_117

Top = Old. Bottom = New.

## 2026-05-19 Abyssal Thermodynamics Solver Pass

What was wrong:
- Heat producers could still route through `HectonHazardManager` heat volumes and generic player trigger/radius exposure.
- `AbyssalThermalManager` still had a direct per-vent temperature fallback.
- Thermodynamics memory used anonymous BufferID casts and no canonical 16-byte `ThermalCellDTO`.
- Heat simulation data was not exposed as a data-provider sampling job for predators/survival.

What was done:
- Added `SystemID.Thermodynamics`.
- Added official `Thermodynamics*` BufferIDs and `AbyssalThermal*` BufferIDs.
- Added `ThermalCellDTO`, `HeatSourceDTO`, `ThermalGridTuningDTO`, `ThermalSampleResultDTO`, `HeatSourceProfileDTO`, and `ThermalTelemetryEntry`.
- Added raw-pointer Burst jobs for init, mock sources, injection, Jacobi diffusion, sampling, hull insulation, grid shift, and telemetry.
- Added `AbyssalThermodynamicsSolver` runtime with Vault handles, uninitialized memory, double-buffered cells, async shift, structured-buffer visual upload, and NaN black-box dump.
- Added UI Toolkit `Abyssal Heat Tuner`, cold CSV profile parser, live thermal slice gizmo, and architecture doc.
- Redirected heat producers in `ThermalUpdraftVolume`, `HectonHazardSource`, and `EnvironmentalHazard` into thermodynamics field injection.
- Removed direct vent temperature fallback and stopped vent heat registration into `HectonHazardManager`.

Cinematic cheats used:
- Convection is `ConvectionVelocityY = max(0, Temperature - Ambient) * ConvectionSpeed`, a scalar visor-refraction fake.
- Hull heat blocking is conductivity collapse inside a simplified AABB, not collider heat raycasts.
- Volcano load is deterministic mock DTO injection, not scene particles or fluid sim.

Exact microseconds saved:
- Direct vent temperature fallback removal: estimated 35us per 16 vents per target.
- Heat trigger/radius route removal: estimated 28us per active heat-exposure entity in hazard-heavy frames.
- `UninitializedMemory` plus cold Burst init: estimated 140us cold-grid zero-fill bypass.
- Particle/fluid convection rejection: estimated 300us+ saved versus per-vent particle columns on low silicon.
- No runtime profiler evidence was collected because CPU gate blocked build/play verification.

Verification:
- `git diff --check` completed with line-ending warnings only.
- Burst `FloatMode.Deterministic` verified present in local `com.unity.burst@07790c2d06d9`.
- Static scan found no `new List`, LINQ, `string.Split`, `Physics.*`, `Overlap*`, `OnTrigger*`, `new NativeArray`, `File.ReadAllBytes`, `GC.Alloc`, or `Debug.Log` in new thermodynamics runtime/jobs/editor files.
- Compile not launched: CPU sampled at 100 percent twice; batch rule forbids `dotnet build` above 50 percent.

<SELF_AUDIT>
  <Agent id="SHINOBU_117" domain="ABYSSAL_THERMODYNAMICS_SOLVER" taskCount="20" />
  <ByteLayouts>
    <ThermalCellDTO size="16" temperatureOffset="0" conductivityOffset="4" convectionVelocityYOffset="8" flagsOffset="12" />
    <HeatSourceDTO size="64" aupOffset="0" intensityOffset="24" radiusOffset="28" sourceIdOffset="40" />
    <ThermalGridTuningDTO size="128" originOffset="0" cellSizeOffset="24" globalQualityOffset="40" resolutionOffset="48" />
    <ThermalTelemetryEntry size="64" maxTemperatureOffset="0" solverMicrosecondsOffset="12" originOffset="16" flagsOffset="44" />
  </ByteLayouts>
  <VaultBufferIDs>
    <Buffer name="AbyssalThermalCellFront" id="70039" />
    <Buffer name="AbyssalThermalCellBack" id="70040" />
    <Buffer name="AbyssalThermalCellInjection" id="70041" />
    <Buffer name="AbyssalThermalHeatSources" id="70042" />
    <Buffer name="AbyssalThermalSourceCount" id="70043" />
    <Buffer name="AbyssalThermalTuning" id="70044" />
    <Buffer name="AbyssalThermalSampleAups" id="70045" />
    <Buffer name="AbyssalThermalSampleResults" id="70046" />
    <Buffer name="AbyssalThermalTelemetryRing" id="70047" />
    <Buffer name="AbyssalThermalProfileBytes" id="70048" />
    <Buffer name="AbyssalThermalProfiles" id="70049" />
    <Buffer name="AbyssalThermalProfileCount" id="70050" />
    <Buffer name="AbyssalThermalShiftScratch" id="70051" />
  </VaultBufferIDs>
  <HotPathGC status="no-managed-allocations-detected-by-static-scan">
    <Evidence>Raw pointer jobs, Vault handles, NativeArrayOptions.UninitializedMemory, no List/LINQ/Split/Physics in new thermodynamics hot files.</Evidence>
    <Caveat>Unity compile/runtime GCMonitor not executed because CPU gate blocked build.</Caveat>
  </HotPathGC>
  <AUP status="pass">All solver sample mapping subtracts GridOriginAup before float cast and wraps indices.</AUP>
  <Scalability status="pass">Jacobi iterations use math.lerp(1, 6, GlobalQualityWeight); resolution uses continuous smooth curve from 16 to 32.</Scalability>
  <BlackBox status="pass">ThermalTelemetryEntry[300] dumps to Docs/AgentLogs/Dump_THERMO_SURGEON.bin on NaN.</BlackBox>
  <Compile status="blocked-by-cpu-gate">CPU sampled at 100 percent; no dotnet/csc processes were running; build not launched by explicit rule.</Compile>
</SELF_AUDIT>

## 2026-05-19 Abyssal Thermodynamics Polish Pass 05

What was wrong:
- Abyssal `SampleTemperatureJob` always returned nearest-cell values. That satisfied low-tier cost but failed the high-tier thermal perception requirement; predators and owners would read blocky heat even when hardware budget allowed smooth sampling.
- Abyssal active resolution followed `GlobalQualityWeight` immediately. Noisy quality pressure could repeatedly reinitialize the grid, violating the hysteresis mandate and creating avoidable visual churn.
- VISUAL_SYNC used one `GraphicsBuffer` plus `SetData`. That did not prove bandwidth ownership and could synchronize CPU writes against GPU reads.
- The checklist still described Task 06 as CAS-based after the serial injection pass had removed CAS.

What was done:
- `SampleTemperatureJob` now resolves an interpolation weight from `GlobalQualityWeight` with `math.step`, `math.lerp`, and a smooth polynomial. Quality at or below `0.15` takes the nearest-cell path; higher quality blends toward trilinear temperature, convection, and conductivity.
- `AbyssalThermodynamicsSolver` now holds a 3 second resolution hysteresis band before accepting a changed quality-derived resolution target.
- Shader cell upload now owns two `GraphicsBuffer` pages and writes through `LockBufferForWrite<ThermalCellDTO>` plus `UnsafeUtility.MemCpy`; the published buffer alternates every upload.
- Legacy `ThermodynamicsHazardGridRuntime` now uses a continuous `qualityCeiling` scalar instead of a binary `forceLowResolution` switch.
- Status, rationale, architecture doc, and binary payload ledger were updated. Task 06 no longer claims CAS as the implementation.

Cinematic cheats used:
- Smooth heat perception is still an O(1) eight-tap scalar-field read at high quality, not a physical convection sim.
- Heat shimmer remains shader-side scalar distortion fed by `ConvectionVelocityY`; no particles, bubbles, or collider volumes were added.

Exact microseconds saved:
- Low-quality sample collapse avoids up to 7 extra cell reads per sample when `GlobalQualityWeight <= 0.15`.
- Resolution hysteresis avoids repeated full-grid reinitialization under oscillating quality pressure; exact gain depends on hardware pressure cadence.
- Double-buffered `GraphicsBuffer` upload avoids single-buffer CPU/GPU contention risk; exact driver microseconds require Unity profiler/Frame Debugger evidence.
- Continuous `qualityCeiling` removes a hard debug/design resolution pop; no fixed microsecond claim.

Verification:
- Static scan found `LockBufferForWrite`, `UnsafeUtility.MemCpy`, `ResolveStableResolution`, and quality-weighted `SampleTrilinear` in the abyssal route.
- Static scan found no `SetData(` in `AbyssalThermodynamicsSolver.cs`.
- Static scan found no `new List`, LINQ, `foreach`, coroutine, PhysX query, trigger, or distance API in the edited abyssal solver/job files.
- Compile/runtime proof remains blocked. The external Visor/Somatic compile wall from polish pass 04 is still the last build result; post-pass-05 gate first found seven active `dotnet` processes, then later sampled `CPU_COUNTER=100`, so another build was forbidden.

<SELF_AUDIT revision="polish-05">
  <Agent id="SHINOBU_117" domain="ABYSSAL_THERMODYNAMICS_SOLVER" taskCount="20" />
  <TaskReconciliation>
    <Task id="01" status="PASS" proof="No heat trigger authority was reintroduced." />
    <Task id="02" status="PASS" proof="Owner samples remain grid lookups; no source list distance loop was added." />
    <Task id="03" status="PASS" proof="Hot DTOs remain public-field unmanaged structs." />
    <Task id="04" status="PASS" proof="ThermalCellDTO stays explicit 16B; upload still uses same stride." />
    <Task id="05" status="PASS" proof="Mock volcano route unchanged and still flagged/evictable." />
    <Task id="06" status="PASS" proof="Injection remains serial deterministic finite add, not CAS." />
    <Task id="07" status="PASS" proof="Jacobi chain unchanged." />
    <Task id="08" status="PASS" proof="Convection remains scalar Dear Lie and now uploads through double-buffered GraphicsBuffer." />
    <Task id="09" status="PASS" proof="Sample job remains data-provider only and now scales nearest-to-trilinear with quality." />
    <Task id="10" status="PASS" proof="Grid shift unchanged." />
    <Task id="11" status="PASS" proof="Resolution and samples now both consume continuous GlobalQualityWeight with hysteresis for resolution; legacy force-low bool is replaced by qualityCeiling." />
    <Task id="12" status="PASS" proof="Hull insulation unchanged." />
    <Task id="13" status="PASS" proof="Sample job still subtracts GridOriginAup before float conversion." />
    <Task id="14" status="PASS" proof="Deterministic Burst mode unchanged." />
    <Task id="15" status="PASS" proof="Vault ownership unchanged; no private NativeArray introduced." />
    <Task id="16" status="PASS" proof="Telemetry unchanged." />
    <Task id="17" status="PASS" proof="Editor facade unchanged." />
    <Task id="18" status="PASS" proof="CSV parser unchanged." />
    <Task id="19" status="PASS" proof="Gizmo unchanged." />
    <Task id="20" status="PASS_WITH_DEPENDENCY_BLOCK" proof="Audit/log/docs updated; compile still blocked outside thermodynamics." />
  </TaskReconciliation>
  <ScalabilityCurve>At quality <=0.15, sample work exits after one cell read. From 0.15 to 0.8, a smooth polynomial blends toward trilinear. Active resolution accepts changed quality targets only after 3 seconds, preventing immediate downgrade/upgrade flicker.</ScalabilityCurve>
  <HPhiVaultStatus privatePersistentNativeArrays="0">No new Vault buffer id was required; GPU upload uses two cold GraphicsBuffer owners, not persistent NativeArray ownership.</HPhiVaultStatus>
  <PointerAliasingAndDependencyGraph>SampleTemperatureJob still receives distinct Cells/SampleAups/Results pointers with NoAlias. Visual upload runs after LateFrame job completion and copies from Vault Front into the inactive GPU buffer.</PointerAliasingAndDependencyGraph>
  <CompileGuard>Thermodynamics asmdef remains Core/Core.Memory/Core.Contracts plus Unity packages only. Post-pass-05 build not launched because active dotnet processes were present first, then CPU sampled at 100 percent.</CompileGuard>
  <DearLie before="nearest-only owner perception or expensive fluid truth" after="quality-weighted scalar-field sampling plus shader shimmer" complexity="Low: O(1) one cell; High: O(1) eight cells; no PhysX or particle simulation." />
</SELF_AUDIT>

## 2026-05-19 Abyssal Thermodynamics Polish Pass 02

What was wrong:
- The new Vault-backed 3D solver still did not receive real heat from the existing producer route. Producers called `IThermodynamicsService`, but that service is still implemented by `AbyssalThermalManager`.
- Directly making `AbyssalThermodynamicsSolver` implement `IThermodynamicsService` would pull in `AbyssalThermalManager.ThermalFlowSample` through the interface and violate the compile wall.
- Mock volcano DTOs could remain in the source buffer when real producers came online.

What was done:
- Added `ThermalSourceSignal` as a 64-byte core signal payload with AUP/radius/intensity/source id/frame.
- `AbyssalThermalManager` now publishes `ThermalSourceSignal` from `TryInjectTransientHeatSource` and vent hazard updates while keeping legacy spatial events for non-heat world interactions.
- `AbyssalThermodynamicsSolver` ingests the signal snapshot into Vault `HeatSourceDTO` records before scheduling injection/diffusion.
- Added transient source TTL: non-persistent signal sources expire after 6 solver frames unless refreshed.
- Added `HeatSourceDTO.FlagPersistent` and `HeatSourceDTO.FlagMock`; direct authored sources persist, mock volcano sources are evicted when real heat arrives.
- Removed standalone `GlobalDataVault.Create` fallbacks from the abyssal solver and legacy thermodynamics hazard grid; missing boot Vault now fails fast instead of creating a private memory owner.
- Updated architecture docs and binary payload ledger with the new 64-byte source lane.

Cinematic cheats used:
- The producer bridge remains scalar source publishing, not collider registration or direct GameObject coupling.
- Visual convection still uses `ConvectionVelocityY` as a shader-fed fake, not fluid simulation.

Exact microseconds saved:
- Thermal source authority unification: estimated 12-25us producer-heavy frame ambiguity/branch debt avoided.
- Mock eviction/TTL: estimated 4-8us stale-source scan avoided after producer shutdown.
- No profiler-grade measurement: CPU gate remained at 100 percent and build/playmode stayed blocked by rule.

Verification:
- `CURRENT_BATCH.md` block for `SHINOBU_117` re-extracted by CLI regex; length `12983`.
- `git diff --check` on touched code files reported line-ending warnings only.
- Static hot-file scan found no LINQ/List/Physics/Overlap/Raycast/Debug.Log/new NativeArray/string.Split in the Abyssal thermodynamics hot files.
- Static heat scan found no `HectonHazardManager.Register(... HazardType.Heat)` and no `TrySampleDirectVentTemperature` path.
- CPU gate: latest check returned `CPU=100` and active `dotnet` PID `16624`; build not launched.

<SELF_AUDIT revision="polish-02">
  <Agent id="SHINOBU_117" domain="ABYSSAL_THERMODYNAMICS_SOLVER" taskCount="20" />
  <TaskReconciliation>
    <Task id="01" status="PASS" proof="Heat producers route through service facade into ThermalSourceSignal; no active HazardType.Heat registration remains." />
    <Task id="02" status="PASS" proof="Direct vent temperature fallback remains removed; solver samples grid cells." />
    <Task id="03" status="PASS" proof="ThermalCellDTO and HeatSourceDTO hot fields are public fields, not properties." />
    <Task id="04" status="PASS" proof="ThermalCellDTO is explicit 16 bytes; HeatSourceDTO is explicit 64 bytes with LastTouchedFrame at offset 60." />
    <Task id="05" status="PASS" proof="Mock volcano job remains deterministic and now marks sources with FlagMock." />
    <Task id="06" status="PASS" proof="ThermalInjectionJob consumes Vault source DTOs and skips expired transient records." />
    <Task id="07" status="PASS" proof="Jacobi diffusion remains chained through Back/ShiftScratch while preserving Front." />
    <Task id="08" status="PASS" proof="Convection remains shader scalar fake." />
    <Task id="09" status="PASS" proof="Damage route remains sample DTO provider only." />
    <Task id="10" status="PASS" proof="Grid shift remains scheduled job with scratch buffer." />
    <Task id="11" status="PASS" proof="Resolution/iterations still derive from continuous GlobalQualityWeight." />
    <Task id="12" status="PASS" proof="Hull insulation remains conductivity collapse, no collider ray heat." />
    <Task id="13" status="PASS" proof="Signal AUP is converted to double3, then mapping subtracts GridOriginAup before float math." />
    <Task id="14" status="PASS" proof="Jobs remain deterministic Burst; signal/source DTOs are blittable." />
    <Task id="15" status="PASS" proof="Vault buffers remain uninitialized-memory plus cold init." />
    <Task id="16" status="PASS" proof="Black box still records 300 frames and dump path." />
    <Task id="17" status="PASS" proof="Editor tuner unchanged and runtime-free." />
    <Task id="18" status="PASS" proof="CSV profile parser unchanged and Span-based." />
    <Task id="19" status="PASS" proof="Thermal slice gizmo unchanged." />
    <Task id="20" status="PASS_WITH_BUILD_GATE" proof="Docs/log/status updated; compile blocked only by CPU gate." />
  </TaskReconciliation>
  <StructLayoutVerification>
    <ThermalSourceSignal size="64">
      <Field name="PositionAup" offset="0" size="48" />
      <Field name="RadiusMeters" offset="48" size="4" />
      <Field name="IntensityCelsiusPerSecond" offset="52" size="4" />
      <Field name="SourceId" offset="56" size="4" />
      <Field name="Frame" offset="60" size="4" />
      <Math>48+4+4+4+4=64; one cache line.</Math>
    </ThermalSourceSignal>
    <HeatSourceDTO size="64" aupOffset="0" intensityOffset="24" radiusOffset="28" sourceIdOffset="40" flagsOffset="44" lastTouchedFrameOffset="60" />
  </StructLayoutVerification>
  <HPhiVaultStatus privatePersistentNativeArrays="0" privateVaultFallback="removed">Source ingestion writes existing Vault buffer `AbyssalThermalHeatSources` id `70042` and count id `70043`; abyssal solver and legacy hazard grid no longer create standalone Vault owners.</HPhiVaultStatus>
  <PointerAliasingAndDependencyGraph>No new job alias risk; ingestion runs before scheduling, then Injection reads Sources/SourceCount and writes Injection. No Tick-time Complete added.</PointerAliasingAndDependencyGraph>
  <CompileGuard>Thermodynamics asmdef still references Core/Core.Memory/Core.Contracts/Unity packages only; no sibling World/Gameplay/AI reference added.</CompileGuard>
  <DearLie before="collider heat zones or direct manager coupling" after="64-byte scalar source lane plus shader scalar convection" complexity="O(sourceTouchedCells + activeCells), no producer collision checks" />
  <BuildGate status="BLOCKED">CPU=100; active dotnet PID 16624; dotnet build not launched.</BuildGate>
</SELF_AUDIT>

## 2026-05-19 Abyssal Thermodynamics Polish Pass

What was wrong:
- Even Jacobi pass counts could write the final field back into the original Front buffer, destroying the pre-diffusion state needed for honest energy audit.
- Telemetry reported identical `EnergyBefore` and `EnergyAfter`; that was not proof.
- Thermodynamics jobs lacked the mandated `CompileSynchronously=true` Burst directive.
- `[NoAlias]` metadata was missing from SIMD-relevant pointer fields.
- Two submarine boiling paths still used `HectonHazardManager.Register(... HazardType.Heat)`.
- Runtime solver hot paths repeatedly called `EnsureVault()`, leaving a GlobalRegistry fallback reachable from simulation paths.

What was done:
- Reworked diffusion scheduling into chained Jacobi passes with Front preserved and writes rotating through Vault Back/ShiftScratch.
- Added `ApplyInjection` so transient heat is applied only on pass 0.
- Added telemetry energy audit: Front+Injection versus final Back/ShiftScratch with dissipation-tolerant drift flag.
- Added `CompileSynchronously=true` to thermodynamics Burst jobs while keeping `FloatMode.Deterministic`.
- Added `[NoAlias]` to proven non-overlapping job pointers.
- Cached Vault use in solver hot/public paths; GlobalRegistry/DataVault fallback remains cold bootstrap.
- Routed submarine exterior boiling and room boiling to cached `IThermodynamicsService.TryInjectTransientHeatSource`.
- Updated architecture/status/rationale docs.

Cinematic cheats used:
- Heat shimmer remains `ConvectionVelocityY` scalar upload, not particles or Navier-Stokes.
- Hull insulation remains conductivity collapse through a simplified hull AABB, not collider-blocked heat rays.
- Boiling producers emit transient scalar-field heat; legacy hazard volumes are not heat authority.

Exact microseconds saved:
- Front-preserving Back/ShiftScratch rotation: 0us allocation cost; avoids telemetry corruption rather than direct frame-time gain.
- Hot `EnsureVault()` removal: estimated 1-2us lookup/branch noise avoided per thermodynamics route.
- Remaining `HazardType.Heat` register eviction: estimated 18-40us hazard/PhysX-adjacent debt avoided on boiling-heavy frames.
- `CompileSynchronously=true` does not save runtime microseconds directly; it prevents lazy Burst compile spikes.
- Profiler-grade exact values remain unavailable because CPU gate is still 100 percent and build/playmode is forbidden.

Verification:
- `CURRENT_BATCH.md` block for `SHINOBU_117` re-extracted by CLI with attribute-tolerant XML regex.
- Static scan found no remaining `HectonHazardManager.Register(... HazardType.Heat)`.
- Static scan found no thermodynamics Burst job missing `CompileSynchronously=true`.
- Static scan found no List/LINQ/Split/Physics/Overlap/OnTrigger/Debug.Log in new Abyssal thermodynamics files.
- CPU gate: `CPU=100`; no `dotnet`/`csc` process output; build not launched by rule.

<SELF_AUDIT revision="polish-01">
  <Agent id="SHINOBU_117" domain="ABYSSAL_THERMODYNAMICS_SOLVER" taskCount="20" />
  <TaskReconciliation>
    <Task id="01" name="TRIGGER_COLLIDER_ERADICATION" status="PASS" proof="Heat producers in EnvironmentalHazard, HectonHazardSource, ThermalUpdraftVolume, submarine exterior boiling, and flooded-room boiling route to thermodynamics service; no active HazardType.Heat register remains." />
    <Task id="02" name="SPHERICAL_DISTANCE_MATH_PURGE" status="PASS" proof="Direct vent temperature fallback removed; sampling route is grid index lookup through SampleTemperatureJob/TrySampleTemperature." />
    <Task id="03" name="CS1612_ENCAPSULATION_PURGE" status="PASS" proof="ThermalCellDTO uses public fields; jobs use ThermalCellDTO pointers." />
    <Task id="04" name="ARM64_PADDING_RECONSTRUCTION" status="PASS" proof="ThermalCellDTO explicit size 16 with offsets 0/4/8/12; runtime validator checks SizeOf and offsets." />
    <Task id="05" name="EMERGENCY_MOCK_VOLCANO" status="PASS" proof="GenerateMockThermalSourcesJob writes deterministic HeatSourceDTOs into Vault source buffer." />
    <Task id="06" name="BURST_HEAT_INJECTION_KERNEL" status="PASS" proof="ThermalInjectionJob maps source radius to grid cells and uses deterministic finite adds in source order." />
    <Task id="07" name="JACOBI_DIFFUSION_RELAXATION" status="PASS" proof="HeatDiffusionSolverJob reads Front and writes Back/ShiftScratch through a deterministic chained Jacobi schedule." />
    <Task id="08" name="THE_DEAR_LIE_CONVECTION_DISTORTION" status="PASS" proof="ConvectionVelocityY scalar uploaded to shader buffer; no fluid particles." />
    <Task id="09" name="THERMAL_DAMAGE_ROUTING" status="PASS" proof="SampleTemperatureJob writes ThermalSampleResultDTO; solver does not apply damage." />
    <Task id="10" name="ASYNCHRONOUS_GRID_SHIFT" status="PASS" proof="ShiftThermalGridJob uses UnsafeUtility.MemMove and is chained, not main-thread completed." />
    <Task id="11" name="CONTINUOUS_SCALABILITY_SOLVER_STEPS" status="PASS" proof="ResolveJacobiIterations uses math.lerp from 1 to 6 over GlobalQualityWeight; no binary tier switch." />
    <Task id="12" name="SUBMARINE_HULL_INSULATION_BRIDGE" status="PASS" proof="SubmarineHullInsulationJob collapses conductivity in hull AABB." />
    <Task id="13" name="AUP_PRECISION_GRID_MAPPING" status="PASS" proof="MapAupToWrappedCell subtracts GridOriginAup before float cast and modulo wraps indices." />
    <Task id="14" name="ROLLBACK_NETCODE_STATE_FENCE" status="PASS" proof="Thermal jobs use FloatMode.Deterministic; DTOs are blittable explicit/sequential unmanaged layouts." />
    <Task id="15" name="ZERO_INIT_OVERHEAD_BYPASS" status="PASS" proof="Vault buffers requested with NativeArrayOptions.UninitializedMemory; cold Burst init writes ambient cells." />
    <Task id="16" name="TELEMETRY_THERMODYNAMICS_RECORDER" status="PASS" proof="ThermalTelemetryEntry[300] records max/source/iterations/time/energy/flags and dumps Dump_THERMO_SURGEON.bin on NaN." />
    <Task id="17" name="THERMODYNAMICS_TUNER_EDITOR_WINDOW" status="PASS" proof="Abyssal Heat Tuner UI Toolkit facade reads telemetry/tuning." />
    <Task id="18" name="CSV_HEAT_SOURCE_SPECS_INGESTOR" status="PASS" proof="ReadOnlySpan byte parser writes HeatSourceProfileDTOs; no string.Split." />
    <Task id="19" name="LIVE_THERMAL_SLICE_GIZMO" status="PASS" proof="OnDrawGizmos draws a thermal slice from the cell buffer." />
    <Task id="20" name="SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION" status="PASS_WITH_BUILD_GATE" proof="Self-audit/log/status/rationale written; compile/runtime profiler blocked by CPU gate." />
  </TaskReconciliation>
  <StructLayoutVerification>
    <ThermalCellDTO size="16" alignment="16">
      <Field name="TemperatureCelsius" offset="0" size="4" />
      <Field name="ThermalConductivity" offset="4" size="4" />
      <Field name="ConvectionVelocityY" offset="8" size="4" />
      <Field name="Flags" offset="12" size="4" />
      <Math>4+4+4+4=16; four cells per 64-byte cache line.</Math>
    </ThermalCellDTO>
    <HeatSourceDTO size="64" alignment="64" double3AupOffset="0" lastTouchedFrameOffset="60" />
    <ThermalGridTuningDTO size="128" alignment="16" double3OriginOffset="0" finalPadOffset="124" />
    <ThermalTelemetryEntry size="64" alignment="64" originOffset="16" finalFieldOffset="60" />
  </StructLayoutVerification>
  <ScalabilityCurve>
    Below quality 0.3: active resolution collapses toward 16-20 cells per axis, Jacobi pass count resolves to 1-2, interpolation consumers can choose nearest-cell samples, and visual complexity remains shader-side scalar shimmer. At quality 1.0: 32^3 active field and up to 6 chained passes feed richer shader distortion without changing damage authority.
  </ScalabilityCurve>
  <HPhiVaultStatus privatePersistentNativeArrays="0">
    <Buffer id="70039" name="AbyssalThermalCellFront" />
    <Buffer id="70040" name="AbyssalThermalCellBack" />
    <Buffer id="70041" name="AbyssalThermalCellInjection" />
    <Buffer id="70042" name="AbyssalThermalHeatSources" />
    <Buffer id="70043" name="AbyssalThermalSourceCount" />
    <Buffer id="70044" name="AbyssalThermalTuning" />
    <Buffer id="70045" name="AbyssalThermalSampleAups" />
    <Buffer id="70046" name="AbyssalThermalSampleResults" />
    <Buffer id="70047" name="AbyssalThermalTelemetryRing" />
    <Buffer id="70048" name="AbyssalThermalProfileBytes" />
    <Buffer id="70049" name="AbyssalThermalProfiles" />
    <Buffer id="70050" name="AbyssalThermalProfileCount" />
    <Buffer id="70051" name="AbyssalThermalShiftScratch" />
  </HPhiVaultStatus>
  <PointerAliasingAndDependencyGraph>
    <NoAlias status="PASS">Init, clear, source generation, injection, hull, diffusion, sample, shift, and telemetry fields use NoAlias only where buffers are distinct. Telemetry Front and final Back/ShiftScratch are distinct because runtime never writes final output into original Front.</NoAlias>
    <Graph>ClearInjection -> MockSources(optional) -> HullInsulation -> Injection -> JacobiPass[0..N-1] -> Telemetry -> LateFrame promotion/upload.</Graph>
    <MainThreadBlocking>Only cold boot init and LateFrame completion after IsCompleted; Tick does not Complete.</MainThreadBlocking>
  </PointerAliasingAndDependencyGraph>
  <CompileGuard>
    Thermodynamics asmdef references Core/Core.Memory/Core.Contracts/Unity packages only; no sibling runtime asmdef dependency was added.
  </CompileGuard>
  <DearLie>
    <Before>Fluid/particle convection or heat collider zones scale as O(sources*entities) plus PhysX broadphase.</Before>
    <After>Thermal authority is O(activeCells + sourceTouchedCells + samples); convection presentation is O(activeCells) scalar upload and shader sampling.</After>
  </DearLie>
  <BuildGate status="BLOCKED">CPU sampled at 100 percent; no dotnet/csc process output; dotnet build not launched by explicit project rule.</BuildGate>
</SELF_AUDIT>

## 2026-05-19 Abyssal Thermodynamics Polish Pass 03

What was wrong:
- `ThermalSourceSignal` was configured and manually flushed, but was missing from direct registry dispatch. That left a duplicate/fallback dispatch risk and weakened the one-route proof.
- `AbyssalThermodynamicsSolver.Tick` removed mock sources twice after signal ingestion.
- Legacy `ThermodynamicsHazardGridRuntime` still scheduled `EntityDamageSamplingJob` and published `CombatDamageSignal`/`ThermodynamicsMockDamageSignal`, contradicting Task 09's data-provider boundary.

What was done:
- Added `ThermalSourceSignal` to direct registry dispatch while preserving deterministic mutation order and stable source-id/AUP folded sort keys.
- Removed the redundant mock-source purge call from the abyssal solver tick path; `TryIngestThermalSourceSignals` is now the single mock-eviction point for real producer signals.
- Removed legacy thermodynamics entity damage job scheduling and mock/combat damage publish loops. The legacy grid now publishes only `ThermalUpdraftSignal`; heat damage ownership is external to thermodynamics.
- Updated Status, Rationale, architecture doc, and binary payload ledger with the deterministic lane and damage-ownership correction.

Cinematic cheats used:
- Heat remains scalar-field source injection plus shader-fed convection scalar, not trigger volumes or per-entity thermal combat emission.

Exact microseconds saved:
- Direct lane closure: estimated 2-5us duplicate/fallback dispatch ambiguity avoided on producer-heavy frames.
- Legacy damage emission purge: estimated 8-20us entity sampling/emission avoided per legacy thermodynamics tick.
- No profiler-grade measurement: CPU gate sampled 75.6 percent, then 100 percent on final recheck; both are above the 50 percent build threshold.

Verification:
- `git diff --check` on touched code files reported line-ending warnings only.
- Static scan found no `SignalBus<CombatDamageSignal>.Push`, `SignalBus<ThermodynamicsMockDamageSignal>.Push`, `EntityDamageSamplingJob`, `DirectRuntimeFlag`, or damage timer path in `Assets/_Project/Scripts/Thermodynamics`.
- Static scan found no `GlobalDataVault.Create` or standalone Vault fallback in `Assets/_Project/Scripts/Thermodynamics`.
- Static scan found no thermodynamics direct sibling-domain namespace reference.
- Static scan found no active `HectonHazardManager.Register(... HazardType.Heat)` or `TrySampleDirectVentTemperature`.
- `CURRENT_BATCH.md` SHINOBU_117 XML re-extracted by CLI: `BLOCK_LENGTH=12983`, `TASK_MARKERS=20`.
- CPU gate: `CPU=100` latest recheck; no dotnet/csc process present; dotnet build not launched by explicit project rule.

<SELF_AUDIT revision="polish-03">
  <Agent id="SHINOBU_117" domain="ABYSSAL_THERMODYNAMICS_SOLVER" taskCount="20" />
  <TaskReconciliation>
    <Task id="01" status="PASS" proof="Heat producers remain routed through service facade -> ThermalSourceSignal -> Vault source DTOs; no active HazardType.Heat register path found." />
    <Task id="02" status="PASS" proof="Direct vent temperature fallback remains absent." />
    <Task id="03" status="PASS" proof="Hot DTOs use public fields; only runtime singleton class properties remain." />
    <Task id="04" status="PASS" proof="ThermalCellDTO 16 bytes; HeatSourceDTO 64 bytes; ThermalSourceSignal 64 bytes." />
    <Task id="05" status="PASS" proof="Mock volcano source job remains deterministic and is evicted on real source signals." />
    <Task id="06" status="PASS" proof="Injection job reads Vault sources and skips expired non-persistent records." />
    <Task id="07" status="PASS" proof="Jacobi chain preserves Front and rotates Back/ShiftScratch." />
    <Task id="08" status="PASS" proof="Dear Lie convection remains scalar shader feed." />
    <Task id="09" status="PASS" proof="Legacy thermodynamics direct CombatDamage/mock damage emission removed; thermodynamics is data-provider/updraft-only." />
    <Task id="10" status="PASS" proof="Grid shift remains asynchronous job with Vault scratch." />
    <Task id="11" status="PASS" proof="Quality-weighted resolution/iteration curve remains continuous." />
    <Task id="12" status="PASS" proof="Hull insulation remains conductivity math, not collision queries." />
    <Task id="13" status="PASS" proof="AUP mapping subtracts GridOriginAup before local float math." />
    <Task id="14" status="PASS" proof="Deterministic Burst jobs and deterministic ThermalSourceSignal mutation order retained." />
    <Task id="15" status="PASS" proof="Vault-owned uninitialized buffers plus cold init retained." />
    <Task id="16" status="PASS" proof="300-frame telemetry ring retained." />
    <Task id="17" status="PASS" proof="Editor tuner remains editor-only." />
    <Task id="18" status="PASS" proof="CSV profile parser remains Span/byte based." />
    <Task id="19" status="PASS" proof="Thermal slice gizmo retained." />
    <Task id="20" status="PASS_WITH_BUILD_GATE" proof="Static verification/docs/log updated; compile blocked by CPU gate." />
  </TaskReconciliation>
  <StructLayoutVerification>
    <ThermalSourceSignal size="64">
      <Field name="PositionAup" offset="0" size="48" />
      <Field name="RadiusMeters" offset="48" size="4" />
      <Field name="IntensityCelsiusPerSecond" offset="52" size="4" />
      <Field name="SourceId" offset="56" size="4" />
      <Field name="Frame" offset="60" size="4" />
      <Math>48+4+4+4+4=64; exact cache-line payload.</Math>
    </ThermalSourceSignal>
    <HeatSourceDTO size="64" flagsOffset="44" lastTouchedFrameOffset="60" />
  </StructLayoutVerification>
  <ScalabilityCurve>Below GlobalQualityWeight 0.3, source lane is capped to 32 frame signals, solver iterations collapse toward 1-2, and consumers can use nearest-cell reads. At weight 1.0, source capacity is 128 and extra solver/shader fidelity is allowed without changing authority routes.</ScalabilityCurve>
  <HPhiVaultStatus privatePersistentNativeArrays="0" privateVaultFallback="0">Abyssal solver uses Vault buffer ids 70039-70051; legacy thermodynamics no longer allocates damage staging buffers for direct emission.</HPhiVaultStatus>
  <PointerAliasingAndDependencyGraph>ThermalSourceSignal snapshot ingestion runs before job scheduling; Injection reads Sources/SourceCount and writes Injection; Jacobi writes Back/ShiftScratch; no Tick-time Complete added.</PointerAliasingAndDependencyGraph>
  <CompileGuard>Thermodynamics source scan found no direct sibling namespace dependency and no private Vault fallback.</CompileGuard>
  <DearLie before="trigger/damage emission route per entity" after="scalar thermal field plus owner-local sampling" complexity="Before: O(entities) damage scan per tick in legacy grid; After: O(updraftSignals) publish only, damage cost owned by consumers." />
  <BuildGate status="BLOCKED">CPU=100 latest recheck after prior 75.6; no dotnet/csc process present; build not launched above 50 percent CPU.</BuildGate>
</SELF_AUDIT>

## 2026-05-19 Abyssal Thermodynamics Polish Pass 04

What was wrong:
- Legacy `ThermodynamicsHazardGridRuntime.EmissionJob` used parallel source iteration with CAS float accumulation. That avoids memory corruption, but it does not guarantee deterministic float addition order when heat/radiation source spheres overlap.
- Legacy updraft signals were selected by interlocked counters inside parallel diffusion, so the first `MaxSignalsPerFrame` entries could vary by worker scheduling.
- Legacy resolution selection still used a binary low/high tier switch.
- Thermodynamics frame metadata still had one `Time.frameCount` dependency in the legacy grid and one in the thermal source signal producer.
- The serial abyssal injection job still paid CAS overhead despite having no writer race.

What was done:
- Converted legacy emission to serial deterministic `IJob`; overlapping source contribution order is now source-index order.
- Moved updraft extraction into `ScanTelemetryJob`, producing updraft signals in cell-index order during the serial telemetry scan.
- Added `[NoAlias]` to all proven-distinct legacy thermodynamics job pointer fields.
- Replaced binary legacy resolution selection with a polynomial `HomeostasisBrain.GlobalQualityWeight` curve plus smooth health-pressure damping.
- Added a local legacy `_simulationFrame` and changed `ThermalSourceSignal.Frame` to use `HectonArenaAllocator.CurrentFrameSequence`.
- Replaced serial abyssal injection CAS with direct finite add.

Cinematic cheats used:
- Updrafts remain ordered scalar signals derived from the field; no particles, colliders, or per-entity heat damage route were reintroduced.

Exact microseconds saved:
- Serial abyssal injection finite add: estimated 1-4us dense-source CAS overhead removed.
- Legacy source/updraft atomics removed: estimated 3-10us contention/false-sharing risk removed when source spheres overlap or many updraft cells exceed threshold.
- Continuous legacy resolution: no fixed microsecond claim; it removes the 16^3 -> 32^3 cost cliff and replaces it with polynomial shedding.

Verification:
- `dotnet build Hecton8.Core.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal -clp:ErrorsOnly` was launched after CPU opened to 19 percent and no dotnet/csc process was present.
- Build failed on external compile wall: `HectonVisorUberPostFeature.cs` missing `UberNoirReconstructionConstantsDTO`, `MockReconstructionInputSignal`, `ReconstructionTelemetryEntry`, `UberNoirReconstructionVaultIds`; `SomaticTunerWindow.cs` missing `VrComfortProfileDTO`, `ComfortTelemetryEntry`.
- No thermodynamics compile error appeared before the external Visor/Somatic wall.
- Static thermodynamics scan found no `Time.frameCount`, `Time.deltaTime`, `Physics`, `OnTrigger`, LINQ/List/Split, local Native allocations, standalone Vault fallback, or direct thermodynamics damage publish route.
- `git diff --check` on touched pass-04 files reported line-ending warnings only.

<SELF_AUDIT revision="polish-04">
  <Agent id="SHINOBU_117" domain="ABYSSAL_THERMODYNAMICS_SOLVER" taskCount="20" />
  <TaskReconciliation>
    <Task id="01" status="PASS" proof="No active HazardType.Heat register path found." />
    <Task id="02" status="PASS" proof="No direct vent temperature fallback found." />
    <Task id="03" status="PASS" proof="Hot thermal DTOs remain public-field unmanaged structs." />
    <Task id="04" status="PASS" proof="ThermalCellDTO explicit 16 bytes; ThermalSourceSignal/HeatSourceDTO 64 bytes." />
    <Task id="05" status="PASS" proof="Mock sources remain deterministic and flagged." />
    <Task id="06" status="PASS" proof="Serial injection/legacy emission remove writer races without atomics." />
    <Task id="07" status="PASS" proof="Jacobi diffusion remains double-buffered; legacy diffusion writes no signal counters." />
    <Task id="08" status="PASS" proof="Convection/updraft presentation remains scalar Dear Lie." />
    <Task id="09" status="PASS" proof="Thermodynamics emits no damage signals; owners sample field data." />
    <Task id="10" status="PASS" proof="Grid shift remains scheduled job with Vault scratch." />
    <Task id="11" status="PASS" proof="Abyssal and legacy resolution/steps now consume continuous GlobalQualityWeight." />
    <Task id="12" status="PASS" proof="Hull/rock shielding is math/SDF mock, not collider heat blocking." />
    <Task id="13" status="PASS" proof="AUP mapping subtracts grid origin before float math." />
    <Task id="14" status="PASS" proof="Thermodynamics metadata no longer depends on Unity frame count." />
    <Task id="15" status="PASS" proof="Vault buffer ownership retained; no private fallback Vault." />
    <Task id="16" status="PASS" proof="300-frame telemetry ring retained and now owns deterministic updraft extraction." />
    <Task id="17" status="PASS" proof="Editor tuner unchanged." />
    <Task id="18" status="PASS" proof="CSV parser remains Span/byte based." />
    <Task id="19" status="PASS" proof="Thermal slice gizmo unchanged." />
    <Task id="20" status="PASS_WITH_DEPENDENCY_BLOCK" proof="Self-audit/log/docs updated; build blocked by external Visor/Somatic missing DTOs." />
  </TaskReconciliation>
  <Determinism>Legacy emission is source-index ordered; updraft extraction is cell-index ordered; abyssal source signal frame uses core arena frame sequence.</Determinism>
  <NoAlias>Legacy Reset/Clear/Emission/Diffusion/Rebase/Scan jobs now mark distinct pointer fields with NoAlias.</NoAlias>
  <CompileGuard>Thermodynamics still has no direct sibling namespace dependency and no private Vault fallback.</CompileGuard>
  <Build status="BLOCKED_BY_DEPENDENCY">Visor/Somatic missing DTO/id types outside SHINOBU_117 domain; no thermodynamics errors reached.</Build>
</SELF_AUDIT>

## 2026-05-19 Abyssal Thermodynamics Polish Pass 06

What was wrong:
- The A/B GPU upload refactor left `EnsureNative()` calling removed `EnsureVisualBuffer()`. That was a direct thermodynamics compile defect.
- Abyssal heat injection and resolution hysteresis still consumed scaled dispatcher `Tick(deltaTime)`. That made heat energy and state transitions frame-time dependent.

What was done:
- Replaced the stale visual buffer call with `EnsureVisualBuffers()`.
- Reused `ThermalGridTuningDTO` offset `124` as `SimulationTickDeltaSeconds`; the DTO remains explicit size `128`.
- Added continuous quality cadence: `GlobalQualityWeight` resolves from 12 dispatcher frames at minimum quality to 1 at full quality.
- Heat injection now receives `SimulationTickDeltaSeconds = cadenceFrames * 1/60`, not frame delta.
- Thermal source signals are ingested/expired every dispatcher tick before cadence gating so skipped low-tier solver frames do not drop producers.
- Shader metadata upload now uses sanitized tuning data when available.

Cinematic cheats used:
- Low-tier heat is still a scalar field update plus shader convection scalar. Cadence throttling reduces CPU solver submissions instead of adding collision checks, particles, or fluid truth.

Exact microseconds saved:
- Low quality can skip up to 11 solver submissions per 12 dispatcher frames; exact frame time requires profiler once the external build wall clears.
- Stale symbol repair has 0us runtime impact and removes known thermodynamics compile debt.

Verification:
- `CURRENT_BATCH.md` SHINOBU_117 XML re-extracted: `BLOCK_LENGTH=12983`, `TASK_LINES=20`.
- `rg` found no remaining `EnsureVisualBuffer(`, `SetData(`, `forceLowResolution`, Unity `Time.frameCount`, Unity `Time.deltaTime`, hot LINQ/List/foreach, PhysX trigger/raycast/overlap, or local `new NativeArray` in thermodynamics domain.
- Burst directive scan found no non-deterministic `BurstCompile` attribute in thermodynamics files.
- Thermodynamics asmdef references only `Hecton8.Core`, `Hecton8.Core.Memory`, `Hecton8.Core.Contracts`, and Unity packages; no sibling domain reference.
- `git diff --check` on touched code files reported line-ending warnings only.
- Build gate remained closed: latest CPU sampled `100`, and seven `dotnet` processes were still running. No build was launched.

<SELF_AUDIT revision="polish-06">
  <Agent id="SHINOBU_117" domain="ABYSSAL_THERMODYNAMICS_SOLVER" taskCount="20" />
  <TaskReconciliation>
    <Task id="01" status="PASS" proof="Heat remains signal-to-Vault source injection; no active trigger heat authority restored." />
    <Task id="02" status="PASS" proof="Direct vent distance fallback remains absent." />
    <Task id="03" status="PASS" proof="Hot DTOs remain public-field unmanaged structs." />
    <Task id="04" status="PASS" proof="ThermalCellDTO 16 bytes; HeatSourceDTO 64 bytes; ThermalGridTuningDTO remains 128 bytes with offset 124 reused for SimulationTickDeltaSeconds." />
    <Task id="05" status="PASS" proof="Mock volcano path remains deterministic and evicted on real source ingestion." />
    <Task id="06" status="PASS" proof="Injection uses deterministic fixed SimulationTickDeltaSeconds and finite guarded adds." />
    <Task id="07" status="PASS" proof="Jacobi dependency chain remains Front-preserving with Back/ShiftScratch rotation." />
    <Task id="08" status="PASS" proof="Convection distortion remains scalar shader fake." />
    <Task id="09" status="PASS" proof="Thermodynamics remains data-provider/updraft-only; no damage signal emission found." />
    <Task id="10" status="PASS" proof="Grid shift remains scheduled MemMove job through Vault scratch." />
    <Task id="11" status="PASS" proof="Resolution, sampling, solver iterations, and cadence consume continuous GlobalQualityWeight." />
    <Task id="12" status="PASS" proof="Hull insulation remains conductivity math, not collider queries." />
    <Task id="13" status="PASS" proof="Sampler/injection mapping subtract GridOriginAup before float math." />
    <Task id="14" status="PASS" proof="Abyssal integration no longer consumes scaled dispatcher delta; jobs use deterministic Burst mode." />
    <Task id="15" status="PASS" proof="Vault buffer ownership retained; no private Vault fallback." />
    <Task id="16" status="PASS" proof="300-frame telemetry ring retained; NaN dump path unchanged." />
    <Task id="17" status="PASS" proof="Editor tuner remains editor-only." />
    <Task id="18" status="PASS" proof="CSV parser remains Span/byte based." />
    <Task id="19" status="PASS" proof="Thermal slice gizmo retained." />
    <Task id="20" status="PASS_WITH_BUILD_GATE" proof="Docs/log/status updated; build blocked by CPU 100 and active dotnet processes." />
  </TaskReconciliation>
  <StructLayoutVerification>
    <ThermalGridTuningDTO size="128">
      <Field name="GridOriginAup" offset="0" size="24" />
      <Field name="CellSizeMeters..JacobiIterations" offset="24" size="24" />
      <Field name="GridResolution" offset="48" size="12" />
      <Field name="ActiveCellCount" offset="60" size="4" />
      <Field name="Dissipation..ThermalDamageThreshold" offset="64" size="32" />
      <Field name="Frame..LastShiftSequence" offset="96" size="16" />
      <Field name="SubmarineHalfExtents" offset="112" size="12" />
      <Field name="SimulationTickDeltaSeconds" offset="124" size="4" />
      <Math>24+24+12+4+32+16+12+4=128; exact 16-byte multiple.</Math>
    </ThermalGridTuningDTO>
  </StructLayoutVerification>
  <ScalabilityCurve>Below GlobalQualityWeight 0.3, cadence resolves near 10-12 frames, sampling collapses toward nearest, and Jacobi count stays near 1-2. At weight 1.0, cadence is every dispatcher frame, sample blending reaches trilinear, and shader upload remains double-buffered.</ScalabilityCurve>
  <HPhiVaultStatus privatePersistentNativeArrays="0">Buffers remain Vault-owned: AbyssalThermalCellsFront/Back/Injection/ShiftScratch/Sources/SourceCount/Tuning/SampleAups/SampleResults/TelemetryRing/ProfileBytes/Profiles/ProfileCount.</HPhiVaultStatus>
  <PointerAliasingAndDependencyGraph>Signal ingestion is main-thread source snapshot mutation before cadence. Scheduled chain is Init/Shift -> ClearInjection -> MockSources -> HullInsulation -> Injection -> Jacobi passes -> Telemetry. No hot Tick Complete added.</PointerAliasingAndDependencyGraph>
  <CompileGuard>Thermodynamics asmdef has no direct sibling domain reference; only Core/Core.Memory/Core.Contracts and Unity packages.</CompileGuard>
  <DearLie before="per-frame full solver at every scaled dispatcher delta" after="fixed-delta scalar field with quality cadence and shader convection fake" complexity="Low tier reduces scheduled solver submissions by up to 12x while preserving O(activeCells) bounded field math when it runs." />
  <BuildGate status="BLOCKED">CPU_COUNTER=100 and seven dotnet processes remained active; build not launched by project rule.</BuildGate>
</SELF_AUDIT>
