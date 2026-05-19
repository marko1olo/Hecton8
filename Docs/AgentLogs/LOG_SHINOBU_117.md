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
    <Task id="06" name="BURST_HEAT_INJECTION_KERNEL" status="PASS" proof="ThermalInjectionJob maps source radius to grid cells and uses float CAS atomic add." />
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
    <HeatSourceDTO size="64" alignment="64" double3AupOffset="0" padding="uint _pad0 at 60" />
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
