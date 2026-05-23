# LOG_SHINOBU_345

## 2026-05-23 - CELESTIAL_ORBIT_TRIG_CALCULATOR
What was wrong:
- Runtime celestial presentation still had a physical sun-light transform route: `sunLight.transform.forward = ...` and a low-tier `Quaternion.Euler` lock path in `HectonCelestialEngine`.
- The existing Vault celestial DTO was a 32-byte mixed scalar state, not the required 64-byte optics layout with `double3 SunDirection` and `double3 MoonDirection`.
- Eclipse behavior was phase-style scalar logic, not a direct dot-product shadow scalar over normalized sun/moon vectors.
- The CSV profile path did not match `celestial_orbit_profiles.csv`.
- The project had no SHINOBU_345 static proof artifact for OOP sun rotation removal.

What was done:
- Replaced runtime sun movement writes in `HectonCelestialEngine` with cached mathematical direction generation through `ApplyMathematicalSunDirection`.
- Disabled the binary low-tier physical light rotation path. Continuous cadence now consumes `HomeostasisBrain.GlobalQualityWeight`.
- Rebuilt `CelestialStateDTO` as explicit 64-byte optics truth:
  - offset 0: `double3 SunDirection`
  - offset 24: `double3 MoonDirection`
  - offset 48: `float EclipseShadowScalar01`
  - offset 52: `float TimeOfDay01`
  - offset 56/60: padding
- Added 64-byte `EnvironmentStateDTO` for tide vector, double simulation time, tide level, tremor scalar, event flags, frame, derivative, quality, and sequence.
- Added/renamed Burst jobs:
  - `GenerateMockOrbitalTimeJob`
  - `EvaluateCelestialOrbitsJob`
- Implemented double-time modulo wrapping and polynomial sin/cos path with Low/Middle/High/Ultra quality blend.
- Calculated `EclipseShadowScalar01` from `math.dot(moonDirection, sunDirection)`; no raycasts or linecasts.
- Wrote `EnvironmentStateDTO.TideVector` from combined harmonic gravitational pull.
- Uploaded shader globals:
  - `_HectonCelestialSunDirection`
  - `_HectonCelestialMoonDirection`
  - `_HectonCelestialEclipseShadowScalar01`
- Changed CSV cold/editor profile source to `celestial_orbit_profiles.csv`.
- Added `Orbital Mechanics Tuner` UI Toolkit entry with `SunOrbitSpeed`, `MoonOrbitSpeed`, and `OrbitalInclination` sliders mutating Vault-backed orbital parameters.
- Added Scene View orbit debug: yellow sun vector, blue moon vector, `EclipseShadowScalar01` label from Scene View camera origin.
- Added `OOP_Sun_Scanner` and appended non-destructive `shinobu345CelestialOrbitScanner` report data to `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`.
- Updated black-box telemetry dump to write a raw `ReadOnlySpan<byte>` over the 300-entry telemetry ring to `Docs/AgentLogs/Dump_SHINOBU_345.bin`.

Cinematic cheats used:
- Dear Lie lighting: shaders receive mathematical sun/moon vectors and eclipse scalar; CPU does not rotate a Directional Light for day/night motion.
- Eclipse uses normalized-vector dot product and smoothed scalar overlap, not light rays.
- Tide uses harmonic pull vector, not arbitrary Update-time sine timer.
- Low quality reduces cadence and harmonic/math precision continuously instead of disabling the simulation.

Exact microseconds saved or bounded:
- Runtime sun transform write removal: estimated 5-20 us per celestial presentation update on i3/MX350, plus avoided transform hierarchy/light dirtiness.
- Eclipse intersection: dot product + scalar curve, estimated under 1 us per FrostTick solve.
- Shader Dear Lie upload: 2 vectors + 1 float, estimated under 5 us per publish.
- `EvaluateCelestialOrbitsJob`: estimated 3-25 us per solve depending on `GlobalQualityWeight` and active harmonics.
- Continuous solve cadence: Low quality may run at 1.0 s intervals instead of 0.1 s, shedding up to 90% celestial ALU cadence.
- Zero-init bypass: celestial Vault buffers use `NativeArrayOptions.UninitializedMemory` and deterministic overwrite; saves cold zero-fill work.

Verification:
- Static forbidden scan: PASS. Scope `Assets/_Project/Scripts/Environment`, `Assets/_Project/Scripts/Lighting`, `Assets/_Project/Scripts/HectonCelestialEngine.cs`; 22 files; 0 `Transform.Rotate`, 0 transform-forward writes, 0 `Quaternion.Euler` transform writes, 0 `Mathf.Sin(Time.time*)`.
- JSON report parse: PASS via `ConvertFrom-Json`.
- Whitespace validation: PASS via `git diff --check` for touched files; line-ending warnings only.
- Build: NOT RUN. Guard blocked it: active `dotnet` PID 25560 remained and CPU samples were 100% / 72.46%. Launching another build would violate the explicit project rule.

<SELF_AUDIT>
  <TASK_CHECK>
    <TASK id="01" status="PASS">Archaeology scan performed; existing owners selected.</TASK>
    <TASK id="02" status="PASS">No duplicate `HectonCelestialManager`; patched existing owners.</TASK>
    <TASK id="03" status="PASS">Existing `EclipseGameplayEventPayload` lane reused.</TASK>
    <TASK id="04" status="PASS">Runtime sun transform writes removed.</TASK>
    <TASK id="05" status="PASS">No `Mathf.Sin(Time.time*)` target hits; double-time Burst path used.</TASK>
    <TASK id="06" status="PASS">`GenerateMockOrbitalTimeJob` implemented.</TASK>
    <TASK id="07" status="PASS">`EvaluateCelestialOrbitsJob` implemented with double modulo and polynomial sin/cos.</TASK>
    <TASK id="08" status="PASS">Eclipse scalar uses dot product, no raycasts.</TASK>
    <TASK id="09" status="PASS">Shader Dear Lie globals published from owner phase.</TASK>
    <TASK id="10" status="PASS">`EnvironmentStateDTO.TideVector` written by orbital job.</TASK>
    <TASK id="11" status="PASS">Cadence scales by continuous `GlobalQualityWeight`.</TASK>
    <TASK id="12" status="PASS">Time/phase math remains double until bounded output.</TASK>
    <TASK id="13" status="PASS">Burst deterministic attributes applied to jobs/math helpers.</TASK>
    <TASK id="14" status="PASS">Celestial Vault buffers use `UninitializedMemory` and job overwrite.</TASK>
    <TASK id="15" status="PASS">300-entry telemetry ring and raw dump path implemented.</TASK>
    <TASK id="16" status="PASS">Orbital Mechanics Tuner sliders added.</TASK>
    <TASK id="17" status="PASS">CSV route switched to `celestial_orbit_profiles.csv` span/scratch parser path.</TASK>
    <TASK id="18" status="PASS">Scene View sun/moon/eclipse debug implemented.</TASK>
    <TASK id="19" status="PASS">`OOP_Sun_Scanner` and JSON report section added.</TASK>
    <TASK id="20" status="PASS_STATIC_BUILD_GATED">Self-audit completed; build gated by active dotnet/CPU rule.</TASK>
  </TASK_CHECK>
  <ARM64_CHECK>
    <DTO name="CelestialStateDTO" size="64" offsets="SunDirection:0,MoonDirection:24,EclipseShadowScalar01:48,TimeOfDay01:52,pad0:56,pad1:60" />
    <DTO name="EnvironmentStateDTO" size="64" offsets="TideVector:0,CurrentSimulationTime:24,GlobalTideLevel:32,SeismicTremorIntensity:36,ActiveEventFlags:40,Frame:44,TideDerivative:48,GlobalQualityWeight:52,Sequence:56,pad0:60" />
    <DTO name="CelestialTelemetryEntry" size="64" offsets="Frame:0,SunAngleRadians:4,EclipseShadowScalar01:8,SeismicTremorIntensity:12,ActiveEventFlags:16,ActiveHarmonics:20,CurrentSimulationTime:24,SolverComputeTimeMs:32,GlobalQualityWeight:36,TideVectorMagnitude:40,Sequence:44,StateHash:48" />
  </ARM64_CHECK>
  <ZERO_GC_CHECK>Hot orbital solve uses unmanaged pointers, raw DTO fields, no LINQ, no managed celestial body classes, no transform search, no string allocation. Editor scanner/tuner/gizmos are outside player hot path.</ZERO_GC_CHECK>
  <AUP_CHECK>Orbital time is global double scalar; tide vector stays `double3` in Vault. No absolute AUP is cast to float for orbit truth.</AUP_CHECK>
  <BLACKBOX_CHECK>Fault or solver timing over 0.1 ms writes the 300-entry telemetry ring to `Docs/AgentLogs/Dump_SHINOBU_345.bin`.</BLACKBOX_CHECK>
  <BUILD_CHECK status="GATED">No green compile claim. Build blocked by active `dotnet` process and CPU guard.</BUILD_CHECK>
</SELF_AUDIT>

## 2026-05-23 - POLISH LOOP ADDENDUM
What was wrong:
- Subagent audit found P0 Vault collisions: SHINOBU_345 raw `BufferID` casts overlapped HullIntegrity and Somatic lanes, and `(SystemID)74` aliased GameplayCombat.
- `HectonCelestialEngine` still carried a legacy analytical celestial solver and object presentation writes for sun disc/planet-shine.
- `OOP_Sun_Scanner` was a destructive shared-report writer and the current `PHYSICS_OPTIMIZATION_REPORT.json` had no SHINOBU_345 section.
- Orbital Mechanics Tuner telemetry graph was still reading seismic telemetry instead of celestial telemetry.
- Celestial CSV ingestion parsed values as float before applying orbital/tuning clamps.

What was done:
- Added named `BufferID.Shinobu345*` values 73350..73372 in `H8Memory` and routed `HectonSeismicTideDirector` through those names.
- Changed `SeismicSystemId` to `SystemID.HabitatAtmosphere`; removed `(SystemID)74` alias risk.
- Added owner-tagged Vault read locks around the celestial pointer-export job window and releases on commit/failure.
- Made `HectonCelestialEngine` consume the published `CelestialRuntimeSnapshot` when valid, defaulted the old analytical solver off, and stopped republishing global snapshot truth from that consumer path.
- Removed runtime sun visual transform position/rotation writes and planet-shine light rotation; planet-shine now publishes shader direction/intensity/color.
- Upgraded `OOP_Sun_Scanner` to merge/upsert only `shinobu345CelestialOrbitScanner` while preserving other agents' report sections.
- Re-added SHINOBU_345 proof to `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`: 26 scanned files, 0 forbidden hits.
- Switched the tuner graph to `CelestialTelemetryEntry` sun angle/eclipse scalar.
- Added double CSV parsing for celestial rows before final float DTO assignment.

Cinematic Cheats used:
- Physical sun-disc object presentation is suppressed; shader vectors carry the visual lie.
- Planet shine no longer rotates a Directional Light; shaders receive vector/scalar data.
- Eclipse proof remains dot-product scalar, not rays.

Exact Microseconds saved:
- Residual transform sync removal: estimated 5-20 us per presentation update on i3/MX350.
- Vault lock overhead: estimated below 5 us per FrostTick solve; correctness guard, not an optimization.
- Scanner/report changes: editor-only, 0 us runtime.

Verification:
- Forbidden celestial OOP scan: PASS, 0 `Transform.Rotate`, 0 transform-forward writes, 0 transform rotation writes through `Quaternion.Euler` or `Quaternion.LookRotation`, 0 sun visual transform position writes, 0 `Mathf.Sin(Time.time*)`.
- Shared report JSON parse: PASS via `ConvertFrom-Json`.
- `git diff --check`: PASS for touched SHINOBU_345 files; line-ending warnings only.
- Build: NOT RUN. Latest CPU guard samples were 100% / 80.35% / 65.22%, above the project limit. No green compile claim.

## 2026-05-23 - POLISH LOOP 6 ADDENDUM
What was wrong:
- `SeismicEvaluationJob` still scheduled six raw Vault pointers without owner locks, even after the celestial kernel had been fenced.
- `_nextSeismicEvaluationTime` advanced before pointer ownership was proven, so a lock failure could suppress the next solve interval.
- `OOP_Sun_Scanner.cs` existed on disk but was not listed in `Hecton8.Editor.csproj`, leaving dotnet/editor project builds blind to the proof artifact.

What was done:
- Added `TryLockSeismicEvaluationVaultBuffers` and reverse-order unlock for `EventSlotsBuffer`, `SeismicStateBuffer`, `ShakeOffsetBuffer`, `TurbiditySpikeBuffer`, `TelemetryRingBuffer`, and `MockSiltSignalBuffer`.
- Released the seismic Vault locks after `DispatcherJobFence.TryComplete` publishes telemetry/output, or immediately on pointer-open failure.
- Moved `_nextSeismicEvaluationTime` advancement to successful schedule only.
- Added `Assets\_Project\Scripts\Editor\OOP_Sun_Scanner.cs` to `Hecton8.Editor.csproj` beside the existing OOP scanners.
- Gated legacy `_orbitJobOutput` allocation behind `enableAnalyticalOrbitSolver`, so the default SHINOBU path no longer keeps that fallback NativeArray alive at boot.

Cinematic Cheats used:
- No new physical simulation. This loop only hardened memory ownership around existing deterministic oscillator/fake shockwave output.

Exact Microseconds saved:
- Vault lock overhead is estimated below 5 us per scheduled seismic evaluation on i3/MX350.
- Cadence correction has no direct speed gain; it prevents a missed evaluation after transient lock contention.
- Editor scanner project inclusion: 0 us runtime.
- Legacy fallback output gate: small boot/memory gain from skipping one persistent NativeArray row and sentinel registration on the default path; 0 B hot-path GC.

Verification:
- Forbidden celestial OOP scan: PASS, 0 forbidden hits.
- Shared report JSON parse: PASS via `ConvertFrom-Json`.
- `git diff --check`: PASS for touched SHINOBU_345 files; line-ending warnings only.
- Build guard before compile: PASS, no `dotnet`/`csc`, CPU samples 39.96 / 19.04 / 24.94.
- `dotnet build Hecton8.Core.csproj --no-restore`: FAILED on external dependencies before SHINOBU file errors:
  - `AirlockPressurizationJobs.cs` and `AirlockPressurizationRuntime.cs`: missing `FluidCompartmentDTO`.
  - `HectonNarrativeDirector.cs`: missing `IUpdatable.Tick(float)` and `ILateFrameTickable.LateFrameTick()` implementations.
  - `SolarPanel.cs`: missing `SolarPanelStateDTO` and `SolarConditionsDTO`.
- No green compile claim. No second build loop launched.

## 2026-05-23 - POLISH LOOP 7 ADDENDUM
What was wrong:
- `HectonCelestialEngine` still read `GlobalRegistry.CelestialRuntimeSnapshot` from the celestial cadence.
- Several presentation helpers read registry services from `SlowTick` call stacks instead of cached cold dependencies.
- `HectonSeismicTideDirector.SlowTick` refreshed registry dependencies and imported the global celestial snapshot even though it owns the current celestial route.
- `OOP_Sun_Scanner` missed aliased transform writes and secondary transform rotation APIs.
- The shared ledger contained stale SHINOBU_346 `70099..70121` IDs and a historical `CelestialStateDTO=32` statement.

What was done:
- Rewired `TryApplyPublishedCelestialSnapshot` to read `Shinobu345CelestialStateRead` and `Shinobu345EnvironmentState` through cold-cached `IDataVault` descriptors and `TryReadOnlyHandle`.
- Cached ocean, weather, GI relay, underwater visuals, random events, dynamic resolution, world seed generator, player context, and biome matrix during cold lifecycle.
- Removed `RefreshCachedRuntimeState()` from `HectonSeismicTideDirector.SlowTick` and stopped importing `GlobalRegistry.CelestialRuntimeSnapshot`.
- Added `[NoAlias]` to the legacy `CelestialOrbitMathJob.Output` fallback.
- Hardened `OOP_Sun_Scanner` for aliases plus `RotateAround`, `LookAt`, `SetPositionAndRotation`, `localRotation`, and `eulerAngles`.
- Updated the route card, binary ledger, status, rationale, and shared physics report to reflect cached Vault consumption and the current 64-byte ABI.

Cinematic Cheats used:
- No physical lighting route was restored. The CPU still ships vectors/scalars; shader and presentation paths carry the visual lie.

Exact Microseconds saved:
- Hot registry demotion: expected below 5 us per celestial cadence on i3/MX350, with the larger win being removal of global authority coupling.
- Seismic SlowTick registry cut: estimated 1-5 us per SlowTick.
- Scanner/docs repair: 0 us runtime.

Verification:
- Scoped CLI mirror scan: PASS, 0 forbidden transform rotation/forward/position APIs and 0 `Mathf.Sin(Time.time*)` across 26 files.
- Shared report JSON parse: PASS via `ConvertFrom-Json`.
- `git diff --check`: PASS for touched SHINOBU_345 files; line-ending warnings only.
- Stale ledger scan: PASS, no remaining current-text hits for `CelestialStateDTO remains 32 bytes`, `70100 EventSlotsBuffer`, `70099..70117`, or `consumes the published snapshot`.
- Registry scan: `GlobalRegistry.CelestialRuntimeSnapshot` no longer appears in `HectonCelestialEngine` or `HectonSeismicTideDirector`; remaining service reads are confined to cold cache/initialization helpers.
- Build remains blocked by the already recorded external Airlock/Narrative/Solar compile wall; no rebuild launched in this loop.

## 2026-05-23 - POLISH LOOP 8 ADDENDUM
What was wrong:
- `HectonCelestialEngine` still owned scene-lifetime private `NativeArray` fields for presentation blackbox, three atmosphere gradient LUTs, and the opt-in legacy orbit fallback output.
- The first candidate repair range `73373..73377` collided with SHINOBU_354 camera-juice BufferIDs.
- Rationale Decision 011 still described the superseded `GlobalRegistry.CelestialRuntimeSnapshot` read path as current.

What was done:
- Added named SHINOBU_345 Vault lanes `73393..73397`: presentation blackbox, day/sunset/night gradient LUTs, and legacy fallback orbit output.
- Replaced the private `NativeArray` fields in `HectonCelestialEngine` with `VaultGenerationHandle<T>` descriptors and method-local resolved views.
- Added a Vault lock/unlock window for the opt-in async `CelestialOrbitMathJob` fallback output row.
- Patched Decision 011 and Decision 016 so the rationale matches the current cached Vault read and Vault-backed fallback-output route.
- Updated the route card and binary ledger with the accepted `73393..73397` range and the rejected colliding `73373..73377` range.

Cinematic Cheats used:
- No physical celestial simulation was added. The CPU still computes/cache-reads small vector/scalar rows; shaders and presentation LUTs fake the visible sky response.

Exact Microseconds saved:
- Runtime speed gain is small; this is mainly ownership safety. Removing five private persistent native allocations reduces scene lifetime and compaction risk.
- Legacy fallback output lock is expected below 5 us and only applies when the opt-in fallback solver is enabled.

Verification:
- `HectonCelestialEngine` scan: PASS, 0 `private NativeArray<`, 0 `new NativeArray<`, and 0 stale `_orbitJobOutput/_celestialBlackBox/_*AtmosphereGradientSamples` field references.
- BufferID enum duplicate scan scoped to `BufferID`: PASS, 0 duplicates.
- Candidate collision scan: `73373..73377` belongs to SHINOBU_354; SHINOBU_345 uses `73393..73397`.
- Scoped forbidden celestial transform/time-sine scan: PASS, 0 hits.
- Registry scan: PASS, `GlobalRegistry.CelestialRuntimeSnapshot` absent from `HectonCelestialEngine` and `HectonSeismicTideDirector`.
- Shared report JSON parse: PASS via `ConvertFrom-Json`.
- `git diff --check`: PASS for touched SHINOBU_345 files; line-ending warnings only.
- Build remains blocked by the already recorded external Airlock/Narrative/Solar compile wall; no rebuild launched in this loop.

## 2026-05-23 - POLISH LOOP 9 ADDENDUM
What was wrong:
- Loop 8 removed private presentation `NativeArray` fields, but the first Vault helper still allowed a read-looking `TryResolve...` path to call `EnsureGenerationHandle`.
- The cold helper treated any matching nonzero generation descriptor as valid without proving that the row still resolved, was created, and met the required length.

What was done:
- Split lifecycle allocation from cadence reads in `HectonCelestialEngine`.
- `RefreshColdRuntimeDependencies` now owns cold presentation scratch ensure for `73393..73397`.
- `EnsureColdCelestialPresentationHandle<T>` validates `TryResolveHandle`, `IsCreated`, and length before deciding not to re-ensure a stale or undersized row.
- Runtime helpers now use `TryResolveExistingCelestialPresentationBuffer` only. They fail closed without allocation/growth, job completion, scene search, registry polling, or global mutation.
- The old `EnsureCelestialRuntimeBuffers` name was removed; `OnEnable` now refreshes cold dependencies before `TryResolveCelestialRuntimeBuffers`.
- Updated status, rationale, route card, and binary payload ledger with the cold allocation boundary.

Cinematic Cheats used:
- No new physical simulation. The CPU still computes/reads small celestial vector and scalar rows; shader/presentation paths keep faking sky, caustics, atmosphere gradients, and planet shine.

Exact Microseconds saved:
- No claimed hot-path speed win. This removes hidden Vault ensure/grow work from cadence helpers and prevents presentation stalls on low-end i3/MX350 silicon. Expected avoided spike is below 5 us in normal cases, higher only when a stale row would otherwise be recreated in a cadence path.

Verification:
- Old helper scan: PASS, 0 `TryEnsureAtmosphereGradientSamples`, 0 `TryEnsureCelestialBlackBoxBuffer`, 0 old `TryResolveCelestialPresentationBuffer(` hits.
- Allocation boundary scan: PASS, `EnsureGenerationHandle` appears only in `EnsureColdCelestialPresentationHandle<T>`.
- Presentation allocation scan: PASS, 0 `private NativeArray<`, 0 `new NativeArray<`, and 0 stale private field references in `HectonCelestialEngine`.
- Scoped forbidden celestial transform/time-sine scan: PASS, 0 hits in the SHINOBU celestial sweep.
- Build remains blocked by the already recorded external Airlock/Narrative/Solar compile wall; no rebuild launched in this loop.

## 2026-05-23 - POLISH LOOP 10 ADDENDUM
What was wrong:
- Loop 9 confined `EnsureGenerationHandle` to cold lifecycle, but `TryResolveCelestialBlackBoxBuffer` still reset blackbox counters and cleared the ring.
- Gradient sampling helpers still called `RefreshAtmosphereGradientSamplesIfDirty`, so `ResolveScriptSunsetCloudColor`, `ResolveScriptNightCloudColor`, and `ResolveScriptSunsetHorizonColor` could transitively mutate presentation scratch.

What was done:
- Moved blackbox clearing/cursor reset to cold handle regeneration and disposal through `ResetCelestialBlackBoxState`.
- Kept `TryResolveAtmosphereGradientSamples`, `TryResolveCelestialBlackBoxBuffer`, `TryResolveExistingCelestialPresentationBuffer`, and `TryResolveOrbitJobOutput` as existing-view probes only.
- Removed dirty refresh calls from `SampleSunsetAtmosphereGradient` and `SampleNightAtmosphereGradient`.
- Rebuilt newly re-ensured gradient rows from cold lifecycle immediately, and allowed runtime designer edits to refresh only through the explicit `MarkAtmosphereGradientSamplesDirty` command path after the cold Vault cache exists.
- Updated status, rationale, route card, and binary payload ledger with the read-accessor purity boundary.

Cinematic Cheats used:
- No physical sun/moon occlusion, light rays, or object rotation route was added. The CPU still emits small vector/scalar facts; sky colors, cloud tint, haze, caustics, and planet shine remain presentation fakes over those facts.

Exact Microseconds saved:
- Removes hidden O(300) blackbox clear risk from a read-looking buffer resolver.
- Removes hidden 24-row gradient rewrite risk from `Resolve*`/sampler paths.
- Normal steady-state savings are small, but low-end i3/MX350 avoids unpredictable presentation spikes; high-tier devices keep deterministic scheduling for shader overkill.

Verification:
- Accessor purity scan: `TryResolveAtmosphereGradientSamples`, `TryResolveCelestialBlackBoxBuffer`, `TryResolveExistingCelestialPresentationBuffer`, `TryResolveOrbitJobOutput`, `SampleSunsetAtmosphereGradient`, `SampleNightAtmosphereGradient`, and the three `ResolveScript*` color helpers contain no refresh, rebuild, reset, grow, or blackbox cursor mutation tokens.
- Runtime motion API scan: 0 hits for `Transform.Rotate`, `RotateAround`, `LookAt`, `Mathf.Sin(Time.time*)`, and runtime sun/planet-shine transform rotation/position assignments in the SHINOBU target scope. The remaining `sunLight.transform.forward` read is editor-only preview ingestion under `#if UNITY_EDITOR`.
- Allocation boundary scan: `EnsureGenerationHandle` remains one hit, confined to `EnsureColdCelestialPresentationHandle<T>`.
- Presentation allocation scan: 0 `private NativeArray<`, 0 `new NativeArray<`, and 0 stale private presentation array field references in `HectonCelestialEngine`.
- Build remains blocked by the already recorded external Airlock/Narrative/Solar compile wall; no rebuild launched in this loop.

## 2026-05-23 - POLISH LOOP 11 ADDENDUM
What was wrong:
- Several mutable presentation cache helpers still used read-style names after the Vault accessor cleanup.
- `ResolveFirmamentBakeCompute` could assign a compute shader reference in editor.
- `TryResolveFirmamentKernels` cached kernel ids.
- `ResolveSunDirection` wrote the normalized direction cache.
- The sun light-data and sun-disc renderer helpers still mixed component probes with cached reads before the later Loop 14 cold-cache split.

What was done:
- Renamed the firmament and sun-direction mutable helpers to explicit command names; Loop 14 later moved the component probes to cold cache helpers and restored cadence reads to pure cached accessors.
- Updated all local callsites in `HectonCelestialEngine`.
- Left pure math/read helpers untouched.

Cinematic Cheats used:
- No physical celestial route was added. This pass is API hygiene so the shader/vector fake remains easy to audit and hard to regress into object-driven rotation.

Exact Microseconds saved:
- 0 us direct behavior change. The gain is prevention: component probes, compute asset lookups, and cache writes are no longer disguised as read accessors.

Verification:
- Old-name scan at the time: 0 hits for `ResolveFirmamentBakeCompute`, `TryResolveFirmamentKernels`, `ResolveSunDirection`, and the mutable component-probe names reviewed in Loop 11.
- New-name scan at the time: expected command declarations and callsites present; Loop 14 later split component probes into cold cache helpers and pure cached reads.
- Remaining read-style command declaration scan leaves only already-reviewed pure/read probes or out-parameter color/math helpers.
- Burst sweep still shows deterministic SHINOBU jobs and `[NoAlias]` on celestial pointer/output lanes.
- Build remains blocked by the already recorded external Airlock/Narrative/Solar compile wall; no rebuild launched in this loop.

## 2026-05-23 - POLISH LOOP 12 ADDENDUM
What was wrong:
- `HectonSeismicTideDirector` read accessors did not allocate or ensure, but several still opened mutable `NativeArray<T>` views.
- `ResolveGlobalQualityWeight` mutated the quality smoothing filter behind a read-style name.
- `ReadCelestialTuning` fallback could advance that filter while a caller expected a read.
- Editor tuner helpers named `TryResolve*` could allocate/grow tuning and orbital rows through `OpenOrAcquireVaultBuffer`.

What was done:
- Added `TryReadOnlyVaultBuffer<T>` over `IDataVault.TryReadOnlyHandle`.
- Moved `TryReadCelestialState`, `TryReadEnvironmentState`, `TryReadCelestialFlow`, `ReadCelestialTuning`, `ReadSeismicTuning`, and `ReadWaterSurfaceAupYOrTide` to immutable read-only Vault views.
- Renamed the mutating quality filter to `UpdateGlobalQualityWeight`.
- Changed `ReadCelestialTuning` fallback to use cached `_globalQualityWeight` without mutating filter state.
- Renamed editor allocation helpers to `EnsureTuningBuffers` and `EnsureOrbitalParameters`.

Cinematic Cheats used:
- No physical simulation or ray route was added. The CPU still computes small sun/moon/tide scalars and vectors; presentation stays shader/global-scalar driven.

Exact Microseconds saved:
- No direct speed claim. The gain is removing accidental write capability and hidden filter mutation from read paths, preventing cache-line invalidation and audit ambiguity on low-end devices.

Verification:
- `HectonSeismicTideDirector` accessor scan: no `Read*`, `TryRead*`, `TryResolve*`, `Get*`, or `Resolve*` declaration contains `TryOpenVaultBuffer`, `OpenOrAcquireVaultBuffer`, `EnsureGenerationHandle`, `UpdateGlobalQualityWeight`, publish calls, or `.Complete()`.
- `HectonCelestialEngine` accessor scan remains clean for the same mutation tokens in read-style declarations.
- `ResolveGlobalQualityWeight`, `TryResolveTuning`, and `TryResolveOrbitalParameters` old names have 0 hits.
- Build remains blocked by the already recorded external Airlock/Narrative/Solar compile wall; no rebuild launched in this loop.

## 2026-05-23 - POLISH LOOP 13 ADDENDUM
What was wrong:
- The primary SHINOBU Vault DTOs were explicit and padded, but the opt-in `HectonCelestialEngine.CinematicOrbitState` Burst fallback helper still relied on implicit sequential layout.
- Broad class-level property grep was noisy because presentation properties are not unmanaged DTO fields; the verification needed to parse struct bodies only.

What was done:
- Converted `CinematicOrbitState` to `[StructLayout(LayoutKind.Explicit, Size = 32)]`.
- Locked offsets to `RegistryOffset` 0, `Direction` 12, `Phase01` 24, and `Fullness01` 28.
- Re-extracted the SHINOBU_345 prompt from `CURRENT_BATCH.md` using the correct attribute-bearing XML tag pattern.
- Re-ran targeted struct ABI scans for auto-properties, `Pack=` markers, and size multiples of 8.

Cinematic Cheats used:
- No object rotation, raycast eclipse, or physical light path was added. This pass preserves the vector/scalar shader fake by hardening the fallback math helper ABI.

Exact Microseconds saved:
- No direct speed claim. The gain is preventing ABI drift and misaligned fallback job state on ARM64-class devices.

Verification:
- `STRUCT_AUTO_PROPERTY_SCAN_0` for struct bodies in `HectonSeismicTideDirector` and `HectonCelestialEngine`.
- `STRUCT_SIZE_MULTIPLE_OF_8_SCAN_0` for explicit-layout structs in the same scope.
- `Pack=` scan returned 0 hits in `HectonSeismicTideDirector`, `HectonCelestialEngine`, `GlobalRegistryContracts`, and `H8Memory`.
- `BUFFERID_ENUM_DUPLICATES_0` from an enum-scoped parser over `BufferID : int`; broad numeric grep was rejected because it matches unrelated constants.
- Primary row report: `CelestialStateDTO SIZE=64`, `EnvironmentStateDTO SIZE=64`, `CelestialTuningDTO SIZE=64`, `CelestialOrbitalParameterDTO SIZE=32`, `CelestialFlowModifierDTO SIZE=32`, `CelestialTelemetryEntry SIZE=64`, `CinematicOrbitState SIZE=32`, `CelestialOrbitJobOutput SIZE=192`, `CelestialBlackBoxEntry SIZE=64`.
- Build remains blocked by the already recorded external Airlock/Narrative/Solar compile wall; no build or rebuild launched in this loop.

## 2026-05-23 - POLISH LOOP 14 ADDENDUM

What was wrong:
- `HectonSeismicTideDirector.SlowTick` still called `EnsureTelemetryRing` and `EnsureSeismicVaultBuffers`, so stale handles could trigger Vault ensure/grow work from slow cadence.
- `HectonCelestialEngine.ShouldCullCelestialForAbyss` still used `PlayerRuntimeContextService.TryGetActiveRuntimeContext` from celestial cadence.
- Sun cookie and sun-disc presentation paths could still reach `TryGetComponent` from `RunCelestialTimeline`.
- `OOP_Sun_Scanner` source would regenerate incomplete BufferID proof text by omitting presentation scratch `73393..73397`.

What was done:
- Removed the SlowTick Vault ensure calls; bootstrap remains the cold allocation/grow owner.
- Rewired abyssal cull to `_cachedPlayerContext.TryGetMovementRuntimeState`.
- Added cold cache helpers `CacheSunAdditionalLightDataCold` and `CacheSunDiscRendererCold`; cadence reads now use `TryGetCachedSunAdditionalLightData` and `GetCachedSunDiscRenderer`.
- Updated scanner-generated BufferID text and stale checklist route wording.

Cinematic Cheats used:
- No physical light rotation, ray eclipse, or celestial body simulation was introduced. The path still publishes compact vectors/scalars and lets shader/presentation fakes consume them.

Exact Microseconds saved:
- Removes stale-handle Vault ensure/grow spike risk from slow cadence.
- Removes repeated component lookup risk from absent light-data or sun-disc components.
- Direct steady-state gain is sub-us on i3/MX350; worst-case cadence spikes are the real target.

Verification:
- `HOT_METHOD_MUTATION_TOKEN_SCAN_0` across SHINOBU celestial/seismic `Tick`, `SlowTick`, `LateFrameTick`, `EvaluateAndPublish`, `RunCelestialTimeline`, cookie, occlusion, and moon-phase hot methods.
- Source scan has 0 stale `PlayerRuntimeContextService.TryGetActiveRuntimeContext`, `GlobalRegistry.CelestialRuntimeSnapshot`, old component-helper names, or incomplete scanner BufferID text in SHINOBU source targets.
- Forbidden transform/time-sine scan remains 0.
- Physics report JSON parses and preserves `73350..73372 plus presentation scratch 73393..73397`.
- `git diff --check` reports line-ending warnings only.
- No build or rebuild launched in this loop; external compile wall remains unchanged.

## 2026-05-23 - POLISH LOOP 15 ADDENDUM

What was wrong:
- `ResolveCelestialSolve` could treat an unfinished asynchronous celestial mechanics job as a solve failure.
- On a publish cadence, that path could emit the emergency hardcoded sun/moon vectors even though the previous `Shinobu345CelestialStateRead` row was still valid.
- This was not a transform or GC defect; it was a fail-closed correctness defect in the job-latency path.

What was done:
- Added `TryReadCachedCelestialSolve`.
- When the current job is in flight, a Vault lock fails, or the cadence does not reschedule, the owner now reads the last finite read-side celestial/environment/flow rows and derives a local tide value from them.
- The hardcoded fallback remains only for first-boot or no-valid-snapshot cases.

Cinematic Cheats used:
- No physical celestial body, raycast eclipse, or light rotation was added. The path still uses the existing vector/scalar shader fake and simply avoids publishing default fake data when real cached Vault data exists.

Exact Microseconds saved:
- No direct speed claim. The value is avoiding one-cadence default-vector churn and downstream shader/presentation reactions on low-end hardware while keeping the dispatcher-owned non-blocking job model.

Verification:
- Source read confirms `ResolveCelestialSolve` now calls `TryReadCachedCelestialSolve` before emergency fallback.
- `TryReadCachedCelestialSolve` uses existing read-only accessors and contains no `TryOpenVaultBuffer`, `OpenOrAcquireVaultBuffer`, `EnsureGenerationHandle`, publish call, registry query, component search, or `.Complete()`.
- Scoped forbidden transform/time-sine scan remains 0.
- No build or rebuild launched in this loop; external compile wall remains unchanged.

## 2026-05-23 - POLISH LOOP 15 ROUTE EVIDENCE ADDENDUM

What was wrong:
- The runtime code now reuses the last finite read-side Vault snapshot while a solve is in flight, but the route card and ledger only described non-finite emergency fallback.

What was done:
- Updated `Docs/ARCHITECTURE/SHINOBU_345_CELESTIAL_ORBIT_ROUTE_CARD.md`.
- Updated `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Both now state the strict fallback order: current solve finalized if available, otherwise last finite `Shinobu345CelestialStateRead` / `Shinobu345EnvironmentState` / `Shinobu345CelestialFlowModifiers`, otherwise first-boot emergency vectors.

Cinematic Cheats used:
- No new simulation. The document update preserves the vector/scalar fake: dot-product eclipse, shader-fed sun/moon vectors, no physical light or ray route.

Exact Microseconds saved:
- 0 us in documentation. The protected behavior avoids unnecessary one-cadence downstream shader/presentation churn when an async solve is still pending.

Verification:
- Route-card and ledger text now match `ResolveCelestialSolve` fallback order.
- Brace-aware method scan reports `METHOD_FORBIDDEN_SCAN_0` for `TryReadCachedCelestialSolve`.
- Scoped forbidden transform/time-sine scan remains 0.
- `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` parses.
- `git diff --check` reports line-ending warnings only on touched files.
- No build or rebuild launched for documentation repair.
