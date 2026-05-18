# Rationale_SHINOBU_62

## Decision 001: Duplicate ID Disambiguation To Ocean Authority

Problem: `CURRENT_BATCH.md` contains two `SHINOBU_62` blocks and stale flora/fauna state repeatedly overwrote this agent's files. The current user request names Gerstner waves, atmospheric scattering, no standard Skyboxes, and buoyancy/visual wave sync.  
Solution: treat the second `SHINOBU_62` block, `OCEAN_SURFACE_AND_ATMOSPHERE_DIRECTOR`, as the only active authority.  
Rejected Alternatives: merging flora/fauna with ocean work, or continuing the first duplicate prompt.  
Scalability potential: Low/Middle/High/Ultra lanes apply to ocean math, surface rendering, and atmosphere only.  
Hardware Impact: zero runtime cost; prevents domain sabotage and wrong-system edits.

## Decision 002: Gerstner Math As The Dear Lie

Problem: buoyant objects desync when physics samples a different surface than the renderer displaces. FFT/fluid truth is too costly and not controllable.  
Solution: one shared `WaveParametersDTO` array feeds CPU Burst evaluators and HLSL Gerstner displacement.  
Rejected Alternatives: FFT ocean, Navier-Stokes, Unity water plane, visual-only shader waves, or separate buoyancy approximation.  
Scalability potential: Low uses 4 waves and foam attenuation; Middle fades fractional contributions; High/Ultra reaches 16 waves, foam, rain ripple, and larger radial grid.  
Hardware Impact: low lane skips 12 of 16 wave contributions per sample; for 100 buoyancy samples that avoids roughly 1200 sincos contributions per batch.

## Decision 003: AUP Wrapping Before Float Trig

Problem: 100km-scale absolute coordinates make sine/cosine phase unstable if cast directly to float.  
Solution: project `double3 AUP` onto wave direction, wrap by wavelength in double precision, then cast the wrapped phase to float.  
Rejected Alternatives: absolute GPU coordinates, raw runtime floats, or huge float offsets.  
Scalability potential: all quality lanes share the precision-safe phase path.  
Hardware Impact: prevents far-origin visual/physics drift without a broader simulation.

## Decision 004: ARM64 DTO Layout

Problem: DTOs must be fast for NativeArray reads and GPU uploads on Quest-class ARM64.  
Solution: `WaveParametersDTO` is explicit 32B: offset 0 `float4 DirectionAndSteepness`, 16 `float PhaseSpeed`, 20 `float Amplitude`, 24 `float Wavelength`, 28 `uint _pad0`. Atmosphere/weather/lod/telemetry/waterline lanes are 64B.  
Rejected Alternatives: `Pack=1`, bool-heavy DTOs, auto-properties, or managed classes.  
Scalability potential: quality changes count/scalars, not memory shape.  
Hardware Impact: aligned loads and one-cache-line telemetry/signal entries reduce ARM64 cache penalties.

## Decision 005: Vault Handles Instead Of Private NativeArrays

Problem: private persistent NativeArray fields fragment ownership and violate the Vault law.  
Solution: runtime stores `VaultBufferHandle<T>` fields and resolves buffers from `GlobalDataVault`/`GlobalRegistry.DataVault`; overwritten buffers request `NativeArrayOptions.UninitializedMemory`.  
Rejected Alternatives: `new NativeArray` in runtime, static arrays, object-owned unmanaged memory.  
Scalability potential: capacity stays stable while quality controls active wave count, grid horizon, and shader fakes.  
Hardware Impact: avoids redundant clears and keeps ownership visible to memory telemetry.

## Decision 006: Decoupled Signals And Globals

Problem: ocean must notify audio/shader/physics without direct sibling runtime references.  
Solution: waterline emits `WaterlineBreachSignal`; wind publishes `_GlobalFlowVector`/`_H8GlobalFlow`; ocean registers as `IOceanKinematics` through `HectonOceanRegistry`.  
Rejected Alternatives: trigger colliders, direct audio calls, direct flora/kelp dependencies, or interface arrays in hot loops.  
Scalability potential: signal/vector routes stay constant across quality.  
Hardware Impact: one transition signal and one vector upload replace broad Unity event/collider paths.

## Decision 007: Shader-Side Foam, Rain, And Atmosphere

Problem: CPU foam particles, rain collision, and standard skyboxes either desync from wave math or burn CPU on visuals.  
Solution: foam uses Gerstner Jacobian scalar; rain uses scalar normal perturbation; atmosphere uses analytical Rayleigh/Mie/gas-giant HLSL.  
Rejected Alternatives: standard Skybox, particle impacts, CPU foam mesh, volumetric atmosphere raymarch.  
Scalability potential: Low attenuates foam/rain/atmosphere; Ultra spends saved CPU on GPU visual overkill.  
Hardware Impact: CPU cost stays at scalar upload and hash-gated wave buffer upload.

## Decision 008: Deterministic Mock Buoyancy

Problem: submarine dynamics are out of domain, but the evaluator must prove batch physics can query exact height/normal without allocations.  
Solution: `MockBuoyancyQueryHydrationJob` fills 10,000 deterministic AUP samples using `Unity.Mathematics.Random`; query jobs sample the same evaluator.  
Rejected Alternatives: `UnityEngine.Random`, managed arrays, per-object MonoBehaviour calls, or waiting on Agent 11.  
Scalability potential: query quality uses the same active-wave continuum as renderer and physics.  
Hardware Impact: batch jobs avoid 10,000 managed calls.

## Decision 009: GPU Upload Discipline

Problem: uploading the 16-wave buffer every frame wastes CPU/GPU sync and could cold-create from `Tick`.  
Solution: two `GraphicsBuffer` objects are created only from boot/slow/cold mutation paths; uploads are hash-gated and double-buffered; per-frame publication passes `allowColdCreate=false`.  
Rejected Alternatives: single-buffer lock every frame, per-frame `CreateStructuredLockBuffer`, blind memcpy of unchanged waves.  
Scalability potential: all lanes skip uploads when weather is unchanged.  
Hardware Impact: unchanged frames avoid 512B wave DTO upload plus lock/unlock synchronization risk.

## Decision 010: Editor Facade And CSV Ownership

Problem: designers need tuning without recompiles, and CSV parsing cannot allocate row objects.  
Solution: `Atmosphere & Wave Tuner` reads/writes Vault DTOs; `OceanWeatherCsvParser` parses native byte scratch.  
Rejected Alternatives: ScriptableObject-only constants, managed `Split`, LINQ, JSON, or recompiling constants.  
Scalability potential: all lanes consume the same tuned DTOs.  
Hardware Impact: editor work is cold; runtime parser avoids heap churn.

## Decision 011: Compile Gate Compliance

Problem: forced compile is necessary after runtime/editor changes, but AGENTS forbids dotnet build while CPU is above 50% or dotnet/csc is active; when the gate finally opened, Core failed in unrelated Optimization code before ocean diagnostics.  
Solution: build was skipped at 77.26%, 92.26%, and 97.88% CPU; after a clean 15.23% preflight it was launched once. The failure is blocked by duplicate `AssetLifecycleGovernor` members, outside this domain. Temporary `.csproj` edits were removed.  
Rejected Alternatives: ignoring the hardware gate, leaving generated-project edits behind, or patching unrelated Optimization ownership to make ocean look green.  
Scalability potential: none; this protects developer iteration hardware.  
Hardware Impact: avoids adding compile load while the workstation is already busy.

## Decision 012: Deterministic Surface Clock

Problem: buoyancy and shader waves can still desync if one side advances with dispatcher `deltaTime`, editor time, or `Time.frameCount` while the other samples a different phase.  
Solution: ocean runtime now advances `_simulationFrameCounter` at a fixed `SimulationTickDeltaSeconds` of 1/60s, derives `_rawSimulationTimeSeconds` from that counter, and resolves shared `_timeSeconds` through a quality-quantized cadence from 5Hz to 60Hz. Waterline signals, telemetry, LOD frame, CPU wave queries, and shader globals consume the same frame/time source.  
Rejected Alternatives: Unity `Time.deltaTime`, Unity `Time.frameCount`, separate shader time, or a visual-only wave phase.  
Scalability potential: Low lanes collapse evaluation cadence toward 5Hz without a hard hardware branch; Middle/High/Ultra lerp continuously to 60Hz through `q*q*(3-2*q)`.  
Hardware Impact: thermal lanes shed wave phase churn proportionally while preserving exact CPU/GPU phase parity.

## Decision 013: Shader State Hash Gate

Problem: even when waves/weather are unchanged, repeated `Shader.SetGlobal*` calls and buffer binding churn tax CPU render submission.  
Solution: `PublishShaderGlobals` hashes `_timeSeconds`, `GlobalQualityWeight`, active wave count, weather, atmosphere, and LOD state. Unchanged hashes skip scalar/global vector publication and only attempt non-cold wave-buffer upload.  
Rejected Alternatives: unconditional global upload, blind per-frame buffer lock, or cold GraphicsBuffer creation from `Tick`.  
Scalability potential: Low quality naturally repeats quantized time more often and therefore skips more global publication; Ultra updates every frame when quality demands it.  
Hardware Impact: low thermal lanes avoid redundant shader global traffic on repeated 5Hz time slices; unchanged weather still avoids the 16 * 32B wave upload path.

## Decision 014: Endianness Defensive Loader

Problem: binary weather/Gerstner payloads are legacy/script-tool generated today, but future OSHINO or network hydration can arrive with non-native byte order. Silent float corruption would desync physics and visuals.  
Solution: `ReadFloat32` constructs raw bits and retains a `math.reversebytes` path for non-little-endian sources before `math.asfloat`. Current `.bin` probe stays little-endian, but the defensive path is present and testable.  
Rejected Alternatives: `BitConverter.ToSingle`, unsafe unaligned casts, or assuming every payload is host-endian forever.  
Scalability potential: no visual quality dependency; prevents cross-platform data corruption.  
Hardware Impact: cold-load only; no runtime frame cost.

## Decision 015: UI Toolkit Editor Facade

Problem: the editor facade initially used IMGUI `OnGUI`; that is editor-only, but the local mandate rejects old immediate-mode editor loops when a retained UI facade is enough.  
Solution: tuner now uses UI Toolkit `CreateGUI`, retained `Slider`/`Toggle` controls, `SetValueWithoutNotify` for Vault refresh, and the same SceneView wave grid evaluator.  
Rejected Alternatives: keeping `EditorGUILayout`, recompiling constants, ScriptableObject-only tuning, or adding a runtime UI dependency.  
Scalability potential: designer tuning still feeds the same Low/Middle/High/Ultra DTO continuum.  
Hardware Impact: editor-only; reduces repaint-driven immediate-mode allocation risk and keeps runtime untouched.
