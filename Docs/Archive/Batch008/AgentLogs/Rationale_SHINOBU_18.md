# Rationale_SHINOBU_18

Date: 2026-05-17
Agent: SHINOBU_18
Domain: ECHELON 7 / Marine Snow & Silt Compute
Status: PENDING VERIFICATION

## Authority Snapshot

Primary prompt extracted from `Docs/Tasks/CURRENT_BATCH.md` by `SHINOBU_18` tag. Neighbor prompts ignored.

Hard constraints:
- No Unity `ParticleSystem`.
- GPU-owned particle state through `GraphicsBuffer`.
- `ParticleDataDTO` is 32 bytes: `float3 Position` + `float Lifetime` + `float3 Velocity` + `float Size`.
- `DynamicWakeDTO` is 32 bytes: `float3 Position` + `float Radius` + `float3 Force` + `float Falloff`.
- CPU updates parameters and dispatches only; no CPU particle-position loop.
- Flow and submarine wakes must be locally mocked until real producers exist.
- Visual fake first: curl/hash noise + wake vectors + Euler advection, not fluid truth.

## Initial Decisions

Problem: Silt must appear volumetric and responsive without CPU particle updates.
Solution: GPU ping-pong particle buffers, compute advection, procedural indirect quads, and mock wake/flow buffers.
Rejected Alternatives: Unity Shuriken/ParticleSystem and CPU arrays are rejected because they move particle integration onto CPU and violate the prompt.
Scalability potential: Low uses reduced dispatch and wake-disabled shader path; Middle uses full 100k with cheap wake loop; High/Ultra spend saved CPU on denser particles, richer noir lighting, and stronger turbulence fakery.
Hardware Impact: Expected low-end gain is removing per-particle CPU integration entirely. Exact microsecond savings are PENDING measurement.

Problem: AUP/floating origin can make curl noise swim or snap.
Solution: Use a universe-noise offset / total offset uniform and evaluate noise in camera-relative stable coordinates.
Rejected Alternatives: Raw shifted world-space noise in shader is rejected because origin rebases create visible discontinuities.
Scalability potential: Low uses one hash-noise octave; High/Ultra can layer extra fake curl fields.
Hardware Impact: Extra uniform math is expected below 10 us CPU, GPU cost PENDING.

Problem: Agent cannot depend on submarine/wake/flow systems owned by other agents.
Solution: Local mock DTOs and provider interfaces inside the silt VFX domain; later real systems can bind compatible buffers through GlobalDataVault/GlobalRegistry.
Rejected Alternatives: Concrete references to Agent 11 systems are rejected as parallel-agent coupling.
Scalability potential: Mock path remains usable for tests and editor tuning.
Hardware Impact: Mock CPU work must be cold or low-cadence; no per-particle CPU work.

## Loop 1 - Tasks 01-05

Problem: Legacy binary silt profiles from Batches 005-007 are not guaranteed to exist in live `StreamingAssets`.
Solution: Scanned archive/docs paths and implemented `GenerateEmergencyMockSilt()` so tuning falls back to 32-byte unmanaged constants in Vault instead of throwing or spinning on IO.
Rejected Alternatives: Blocking initialization on `silt_density_profiles.h8bin` / `particle_turbulence.bin` was rejected because missing content would kill the renderer before visual fallback.
Scalability potential: Low uses deterministic emergency density; High/Ultra can hot-load CSV or binary data later without changing the compute ABI.
Hardware Impact: Measured proof absent. Avoided retry/file-error path estimated at 20-80 us/frame when binaries are absent.

Problem: Marine snow cannot spend CPU cycles on particle position updates.
Solution: Runtime state moved to `GraphicsBuffer` ping-pong buffers and compute kernels; CPU only binds constants, uploads small wake pages, and dispatches.
Rejected Alternatives: Unity `ParticleSystem`, `Instantiate`, and CPU arrays were rejected because they violate the prompt and produce frame-time spikes at 100k particles.
Scalability potential: Low dispatches fewer particles; Ultra spends saved CPU on dense noir particulate and richer lighting.
Hardware Impact: Measured proof absent. Replacing a hypothetical 100k CPU update loop is estimated at 3000-8000 us/frame avoided on MX350-class CPU.

Problem: DTO mutation through properties risks CS1612 copies and hidden managed access patterns.
Solution: `VfxConfigurationDTO` is raw-field only, with `VolumetricSiltConfigurationAccess.ElementAt()` returning `ref VfxConfigurationDTO` over Vault memory.
Rejected Alternatives: `{ get; private set; }` and copied `NativeArray<T>` elements were rejected.
Scalability potential: Same ABI can be used by jobs, editor tuner, CSV ingest, and compute binding without wrapper churn.
Hardware Impact: Measured proof absent. Copy avoidance estimate: 2-10 us/frame in hot tuning paths.

Problem: ARM64/Metal DTO reads require deterministic 16-byte GPU alignment.
Solution: `ParticleDataDTO` layout is `float3 Position` 0-11, `float Lifetime` 12-15, `float3 Velocity` 16-27, `float Size` 28-31. `DynamicWakeDTO` layout is `float3 Position` 0-11, `float Radius` 12-15, `float3 Force` 16-27, `float Falloff` 28-31. Runtime guard checks `UnsafeUtility.SizeOf<T>() == 32`.
Rejected Alternatives: `[StructLayout(Pack=1)]`, byte-level packing, and 28-byte wake structs were rejected.
Scalability potential: SIMD-friendly layout is shared by C# and HLSL.
Hardware Impact: Measured proof absent. Avoids misaligned load penalty, estimate 5-30 us/frame on ARM64-class devices.

Problem: Real submarine wake producers are outside this agent's visibility.
Solution: Added local `MockWakeSignal`, `MockFlowField`, and small Burst jobs that populate Vault-backed wake DTOs at a low cadence.
Rejected Alternatives: Direct references to Agent 11 runtime classes were rejected as compile-wall coupling.
Scalability potential: Mock path stays active for editor/test scenes; real producer can replace buffer contents through GlobalDataVault.
Hardware Impact: Measured proof absent. Mock upload is capped to four 32-byte wake DTOs and legacy vectors, estimate under 20 us/update.

## Loop 2 - Tasks 06-10

Problem: 100,000 silt particles need movement without CPU position updates.
Solution: `Hecton_MarineSnow.compute` evaluates one particle per thread from `ParticleDataDTO`, combines global flow, mock flow, curl fake, wake vectors, and Euler integration, then writes the next DTO and render metadata.
Rejected Alternatives: CPU integration, Unity ParticleSystem, and true fluid simulation were rejected.
Scalability potential: Low uses cheap flow and fewer particles; High/Ultra layer extra curl and wake disturbance.
Hardware Impact: Measured proof absent. Mock GPU estimator now records 72-450 us for common tier/count combinations.

Problem: Submarine/wake turbulence must disturb silt without concrete class coupling.
Solution: Compute consumes `StructuredBuffer<DynamicWakeDTO>` and applies radius/falloff force; legacy vector buffers remain for existing fluid payloads.
Rejected Alternatives: Per-particle CPU wake impulse calculation and direct Agent 11 class references were rejected.
Scalability potential: Low tier can disable the dynamic wake path through scalability parameters; Ultra keeps the DTO loop for vortex density.
Hardware Impact: Measured proof absent. Mock estimator prices high-tier wake influence at 12 us/wake and low-tier at 4 us/wake.

Problem: Rendering cannot become a CPU draw/mesh rebuild loop.
Solution: The renderer ping-pongs data/meta buffers, binds the write buffers to material, and keeps indirect procedural draw submission.
Rejected Alternatives: Mesh spawning, per-particle GameObjects, and CPU-built quads were rejected.
Scalability potential: Same draw path handles 10k toaster mode and 100k high tier.
Hardware Impact: Measured proof absent. CPU draw/setup avoidance estimated at 500-1500 us/frame.

Problem: Filling an entire unseen world volume wastes particles and overdraw.
Solution: Respawn logic uses deterministic hash math to place particles in a camera-forward shell/frustum illusion, keeping density in the player's view.
Rejected Alternatives: Simulating particles hundreds of meters behind or outside the camera was rejected.
Scalability potential: Low gets density where visible; Ultra spends budget on richer front-volume layering.
Hardware Impact: Measured proof absent. Estimated GPU savings 100-300 us/frame versus a large blind volume at equivalent perceived density.

Problem: Floating origin shifts can make turbulence patterns jump.
Solution: Renderer publishes total AUP offset from `HectonFloatingOrigin.CurrentTotalOffsetDouble`; compute evaluates turbulence/curl on `positionWS - _HectonFloatingOriginOffset.xyz`.
Rejected Alternatives: Casting absolute AUP/world positions directly to float noise coordinates was rejected.
Scalability potential: Stable turbulence is shared by every quality tier.
Hardware Impact: Measured proof absent. CPU impact is uniform update only, estimated below 5 us/frame.

## Loop 3 - Tasks 11-14

Problem: Silt inside cave/rock geometry must not float through solid surfaces.
Solution: Compute path uses the bound low-resolution SDF/cave texture and `ResolveSdfParticleCollision()` to zero/fade particles inside terrain.
Rejected Alternatives: CPU raycasts, Physics casts, or terrain queries per particle were rejected.
Scalability potential: Low can disable SDF by scalability params; High/Ultra keeps collision for cave-settling atmosphere.
Hardware Impact: Measured proof absent. Moving this to GPU texture sampling avoids an estimated 100-500 us/frame CPU query budget.

Problem: Biome density data may be missing while designers still need visible density control.
Solution: Binary absence is handled by emergency tuning; CSV/Vault density flows into `ResolveEffectiveDensityScale()` and dispatch count interpolation rather than hard-popping counts.
Rejected Alternatives: Failing renderer startup or abrupt particle-count switches were rejected.
Scalability potential: Low can collapse density to 10k active particles; Ultra can use full 100k plus fog-density injection.
Hardware Impact: Measured proof absent. Dispatch reduction estimated at 100-600 us GPU/frame depending density and hardware tier.

Problem: MX350/toaster tier cannot pay for the dynamic wake loop every frame.
Solution: Added `#pragma multi_compile _ _MATH_LOD_LOW` to `Hecton_MarineSnow.compute` and `Hecton_MarineSnow.shader`; `_MATH_LOD_LOW` returns zero dynamic wake flow before loops execute. Runtime still caps active particles by tier/system stress.
Rejected Alternatives: Keeping only uniform branches was rejected because the prompt explicitly required a shader variant and low-tier loop removal.
Scalability potential: Low gets falling noir dust; High/Ultra keep wake vortex detail.
Hardware Impact: Measured proof absent. 100k->10k plus wake-loop skip is mock-estimated at 200-300 us GPU/frame saved plus branch/loop pressure.

Problem: Marine snow must integrate with Agent 17 noir lighting without per-instance material changes.
Solution: Particle shader samples URP main light and `Hecton_WaterExtinction.hlsl`, then applies depth-darkened scatter to the shared particle draw.
Rejected Alternatives: Flat unlit particles and `Material.SetFloat` per instance were rejected.
Scalability potential: Low keeps cheap LUT tint; High/Ultra get richer black-blue scatter and headlight response.
Hardware Impact: Measured proof absent. Added shared shader lighting cost estimated 20-80 us/frame.

## Loop 4 - Tasks 15-17

Problem: Acoustic/sonar shockwaves must visibly shove silt without depending on another agent's signal class.
Solution: Added 32-byte `MockAcousticSignal`, cached mock pulse state, hot-bound `_MarineSnowMockAcousticPulse` / `_MarineSnowMockAcousticParams`, and compute-side radial impulse math guarded against non-finite values.
Rejected Alternatives: Direct dependency on Agent 15 acoustic runtime and CPU-side particle velocity edits were rejected.
Scalability potential: Low can keep the pulse rare and cheap; Ultra uses the saved CPU budget for broad shockwave noir turbulence.
Hardware Impact: Measured proof absent. GPU radial impulse path is estimated at 20-60 us while active.

Problem: Buffer initialization for 100k particles can spike if CPU seeds arrays or buffers are recreated.
Solution: Runtime allocates `GraphicsBuffer` data/meta ping-pong buffers during setup or capacity resize only, then runs `InitializeParticles` compute kernel for both buffers.
Rejected Alternatives: Per-frame allocation, CPU seed arrays, and `SetData` full particle uploads were rejected.
Scalability potential: Low allocates 10k; Ultra allocates 100k once and keeps the same GPU path.
Hardware Impact: Measured proof absent. Avoids a likely 1000-4000 us gameplay allocation/initialization spike.

Problem: The blackbox must prove dispatched particle counts and fatal state rather than saying "unknown."
Solution: 300-frame Vault telemetry ring stores `DispatchedParticleCount`, capacity, wake count, mock GPU microseconds, camera position, state hash, and flags; dump paths are `Docs/AgentLogs/Dump_SILT_VFX.h8dump` and prompt-compatible `Docs/AgentLogs/Dump_SILT_VFX.bin`.
Rejected Alternatives: Per-frame `Debug.Log`, managed strings, and no dump on NaN were rejected.
Scalability potential: Low telemetry stays identical; High/Ultra can tune mock threshold or replace mock timing with a real fence readback later.
Hardware Impact: Measured proof absent. Ring write estimate is under 5 us/frame.

Problem: Core build verification revealed one SHINOBU_18 type mismatch after telemetry field tightening.
Solution: Stored `CommandSequence` as `uint` to match `_lastVehicleCommandSequence`, then reran filtered Core build.
Rejected Alternatives: Casting into a signed field was rejected because command sequence is already unsigned.
Scalability potential: No visual impact; keeps blackbox binary dump stable.
Hardware Impact: No runtime cost.

## Loop 5 - Tasks 18-20

Problem: Designers need live balancing of silt without C# recompilation.
Solution: Added `VolumetricSiltTunerWindow` under `#if UNITY_EDITOR`; sliders write `VfxConfigurationDTO` directly to GlobalDataVault unmanaged memory.
Rejected Alternatives: Runtime ScriptableObject mutation and serialized C# constants were rejected.
Scalability potential: Low/Mid/High/Ultra tuning can be adjusted live through the same DTO.
Hardware Impact: Editor-only. Runtime hot path cost: 0 us.

Problem: CSV balance changes must not allocate strings or lists in the gameplay path.
Solution: `VolumetricSiltCsvParser` parses a preallocated byte buffer, hashes fixed lowercase keys, clamps values, and updates Vault tuning. File polling/read is now staged by `H8_SiltCsvReader` background thread; `Tick()` only consumes staged bytes.
Rejected Alternatives: `string.Split`, LINQ, JSON, random-access hot IO, and main-thread `File.Exists` / `FileStream` polling were rejected.
Scalability potential: Designers can author toaster and Ultra density profiles without code changes.
Hardware Impact: Measured proof absent. CSV path is background staged; hot frame file-IO cost is 0 us.

Problem: Invisible wake vectors need human-readable scene debugging.
Solution: EditorWindow registers `SceneView.duringSceneGui`, reads Vault `DynamicWakeDTO`, and draws yellow wire discs plus force lines for each active wake.
Rejected Alternatives: Runtime debug GameObjects, ParticleSystem wake previews, and gizmo-only MonoBehaviours were rejected.
Scalability potential: Same DTO visualizes mock wake today and real producer wake buffers later.
Hardware Impact: Editor-only. Runtime hot path cost: 0 us.

## Loop 6 - Polish Corrections

Problem: The CSV override path still used main-thread `File.Exists`, `GetLastWriteTimeUtc`, and `FileStream` polling.
Solution: Moved disk polling and sequential file reads into a background `H8_SiltCsvReader` thread with two staged 4096-byte buffers. The main thread checks a volatile dirty flag and parses already-staged bytes only.
Rejected Alternatives: Keeping cold-but-main-thread file IO was rejected because Steam Deck MicroSD stalls are explicitly forbidden.
Scalability potential: Low-tier devices avoid file stalls; high-tier devices retain hot reload for design iteration.
Hardware Impact: Measured proof absent. Hot frame file-IO cost is now 0 us; background polling remains every 0.5 seconds.

Problem: Blackbox dump extension satisfied the XML task `.bin` but not the stronger `.h8dump` mandate.
Solution: Primary dump path is now `Docs/AgentLogs/Dump_SILT_VFX.h8dump`; legacy `Docs/AgentLogs/Dump_SILT_VFX.bin` is also written for prompt compatibility.
Rejected Alternatives: Choosing only one extension was rejected because the active prompt and polish mandate disagree.
Scalability potential: No runtime visual impact; crash forensics are clearer.
Hardware Impact: Fatal-path only; no normal-frame cost.

Problem: Editor tuner had manual `.ToString()` labels that could pollute static zero-GC scans.
Solution: Replaced them with numeric `EditorGUILayout.LongField` display calls.
Rejected Alternatives: Keeping editor-only string formatting was rejected to reduce audit noise.
Scalability potential: Editor-only.
Hardware Impact: Runtime hot path cost remains 0 us.

## Loop 7 - Forensic Follow-Up

Problem: The CSV refresh method still accepted a `dt` parameter after file IO moved to a background staged reader.
Solution: Removed the unused `dt` parameter and call-site clamp so `Tick()` does not imply time-based file polling on the main thread.
Rejected Alternatives: Leaving dead signature noise was rejected because audits treat every hot-path argument as suspicious.
Scalability potential: No visual impact; it keeps the hot path easier to verify for low-end hardware.
Hardware Impact: No measurable runtime change expected; static clarity gain only.

Problem: A final `dotnet build` attempt exceeded 120 seconds and left a live `dotnet` process.
Solution: Stopped the process and retained compile status as PENDING VERIFICATION / BLOCKED BY DEPENDENCY instead of reporting a false green state.
Rejected Alternatives: Waiting indefinitely or hiding the timeout was rejected because it would damage the iteration loop.
Scalability potential: No visual impact.
Hardware Impact: Prevented continued CPU burn from a stuck verification process.

## Loop 8 - Bandwidth Discipline Polish

Problem: The renderer still kept managed upload scratch arrays for frame constants, empty sentinel buffers, and mock wake pages, even though the project mandate requires `GraphicsBuffer.LockBufferForWrite` for GPU updates and page discipline.
Solution: Removed `_frameConstantsUpload`, empty sentinel upload arrays, and mock wake upload arrays. Frame constants, empty sentinels, and mock wake DTO/legacy buffers now write directly into mapped `GraphicsBuffer` memory. Mock wakes are sanitized before upload and default-cleared when inactive.
Rejected Alternatives: Keeping tiny managed arrays was rejected because they are cheap but weaken the forensic proof that CPU only dispatches/binds and writes mapped GPU pages.
Scalability potential: Low tier reduces PCIe and managed-memory audit noise; Ultra keeps the same visual overkill wake/acoustic path without extra CPU particle ownership.
Hardware Impact: Measured proof absent. Expected gain is small in microseconds, but it removes three 4-entry managed wake staging arrays and one per-frame constants array from the upload path.

## Loop 9 - Hot-Path Stall Polish

Problem: `RefreshSiltProfileCsv()` still entered a blocking `lock` when consuming bytes staged by the background CSV reader.
Solution: Replaced the main-thread lock with `Monitor.TryEnter`; if the reader owns the staging lock, the renderer skips CSV consumption for that frame and retries later.
Rejected Alternatives: Blocking on a short memory copy was rejected because Steam Deck MicroSD and antivirus/file-lock stalls are specifically forbidden to leak into `Tick()`.
Scalability potential: Low-tier hardware avoids lock wait spikes; high-tier design iteration keeps hot reload with bounded one-frame latency.
Hardware Impact: Measured proof absent. Expected normal-frame gain is 0 us; worst-case stall risk is reduced by avoiding main-thread lock waits.

## Loop 10 - Shader NaN Vaccination

Problem: Marine-snow shader code still had local `rcp()` and `sqrt()` calls that relied on upstream clamps instead of guarding the denominator/root at the call site.
Solution: Wrapped remaining local reciprocal denominators in `max(..., EPSILON)` or `max(..., 0.0001)` and guarded the respawn radial `sqrt()` with `max(hash, 0.0)`.
Rejected Alternatives: Trusting upstream clamping was rejected because the active mandate requires guarding every division/rsqrt class operation where it is used.
Scalability potential: Low tier avoids NaN propagation in the cheapest path; Ultra keeps visual overkill without shader poison spreading into the render pipeline.
Hardware Impact: Measured proof absent. Added ALU is trivial; risk reduction is the value.

## Loop 11 - Cache-Line and Trig Polish

Problem: `FrameConstantsData` occupied 112 bytes and the private `VehicleWakeJobResult` occupied 40 bytes. Both were legal multiples of 8, but neither expressed clean 16-byte lane intent all the way through the final cache-line audit.
Solution: Padded `FrameConstantsData` to 128 bytes with an explicit `Vector4 Pad0` and mirrored that field in the compute/particle shader `MarineSnowFrameData` structs. Padded `VehicleWakeJobResult` to 48 bytes with `uint Pad0/Pad1`, producing three explicit 16-byte lanes.
Rejected Alternatives: Relying on implicit struct tail padding or "multiple of 8 is enough" was rejected because this domain feeds GPU/Vault interop and the active mandate asks for forensic layout proof.
Scalability potential: Low/Mid/High/Ultra use the same aligned ABI; no tier-specific branch is introduced.
Hardware Impact: Measured proof absent. Expected microsecond delta is negligible; the win is deterministic cache-line/ABI layout and simpler ARM64 audit.

Problem: Low-cadence mock wake/flow jobs still used `math.sin`/`math.cos` for proof turbulence.
Solution: Replaced the mock-only trig with a signed triangle-wave helper. The silt still reads as swirling noir turbulence, but the proof path now uses cheaper, predictable visual fakery.
Rejected Alternatives: Keeping trigonometric motion in mock jobs was rejected because the Cinematic Cheat mandate prefers simple controllable fakes when physics truth is unnecessary.
Scalability potential: Low tier gets cheap deterministic drift; Ultra can spend the GPU budget on denser particles and wake DTOs rather than CPU-side trig proof motion.
Hardware Impact: Measured proof absent. The job runs at low cadence, so normal-frame gain is expected to be small; the deterministic fake removes trig instructions from SHINOBU runtime C#.

Problem: `ResolveTargetCamera()` could retry `TryGetComponent` every tick when the renderer was unbound or misbound.
Solution: Added a 30-frame cold retry gate for component fallback probing while preserving immediate resolution for explicitly changed camera transforms.
Rejected Alternatives: Repeated component probing in a tick method was rejected because hot-path audits treat any component lookup in `Tick()` as suspicious even if it usually early-outs.
Scalability potential: Low-tier startup/miswire cases avoid repeated component probes; high tier behavior is unchanged when the camera is bound correctly.
Hardware Impact: Measured proof absent. Normal bound-camera path is unchanged; worst-case unbound fallback probe cadence drops from every tick to about twice per second at 60 fps.

<SELF_AUDIT agent_id="SHINOBU_18" status="PENDING_VERIFICATION">
  <TASK_MATRIX>
    Task 01 [PASS] Binary graveyard scanned; live silt binaries absent, emergency mock tuning installed.
    Task 02 [PASS] No Unity ParticleSystem path for ambient marine snow; compute/GraphicsBuffer path owns particles.
    Task 03 [PASS] VfxConfigurationDTO uses raw fields and ref-return access; no DTO properties.
    Task 04 [PASS] DynamicWakeDTO is 32 bytes, float4 aligned, no Pack=1.
    Task 05 [PASS] MockWakeSignal and MockFlowField implemented locally; no submarine solver dependency.
    Task 06 [PASS] Compute advection reads ParticleDataDTO and integrates position/velocity/lifetime on GPU.
    Task 07 [PASS] DynamicWakeDTO StructuredBuffer loop applies radius/falloff force; low tier variant skips loop.
    Task 08 [PASS] Renderer binds GPU write buffers to indirect procedural draw path.
    Task 09 [PASS] Respawn logic fakes density in camera-forward frustum/shell.
    Task 10 [PASS] AUP total offset is passed to compute and subtracted before turbulence/curl evaluation.
    Task 11 [PASS] SDF/cave collision path kills/fades particles inside terrain.
    Task 12 [PASS] OSHINO binary content absent; density uses emergency/CSV/Vault fallback and interpolated dispatch count.
    Task 13 [PASS] Tier/system stress throttles particle count; `_MATH_LOD_LOW` shader variant bypasses wake loop.
    Task 14 [PASS] Particle shader samples main light and Hecton water-extinction LUT.
    Task 15 [PASS] MockAcousticSignal drives compute-side radial shockwave impulse.
    Task 16 [PASS] GPU initialization kernel seeds ping-pong buffers; runtime avoids CPU particle seed arrays.
    Task 17 [PASS] 300-frame telemetry ring tracks DispatchedParticleCount and mock GPU microseconds; dump paths are `Docs/AgentLogs/Dump_SILT_VFX.h8dump` and legacy `Docs/AgentLogs/Dump_SILT_VFX.bin`.
    Task 18 [PASS] `Volumetric Silt Tuner` EditorWindow writes Vault tuning.
    Task 19 [PASS] CSV parser uses preallocated bytes and hash keys; no LINQ/string split.
    Task 20 [PASS] Editor SceneView wake visualizer draws DynamicWakeDTO discs and force lines.
  </TASK_MATRIX>
  <ARM64_CHECK>
    ParticleDataDTO size = 32 bytes. Offsets: Position float3 [0..11], Lifetime float [12..15], Velocity float3 [16..27], Size float [28..31]. 16-byte lanes: lane0 Position.xyz+Lifetime, lane1 Velocity.xyz+Size.
    ParticleRenderMetaDTO size = 32 bytes. Offsets: PreviousPosition float3 [0..11], Flags uint [12..15], Uv float2 [16..23], Pad float2 [24..31].
    DynamicWakeDTO size = 32 bytes. Offsets: Position float3 [0..11], Radius float [12..15], Force float3 [16..27], Falloff float [28..31].
    MockWakeSignal size = 32 bytes. Offsets mirror DynamicWakeDTO.
    MockAcousticSignal size = 32 bytes. Offsets: Position float3 [0..11], Radius [12..15], Magnitude [16..19], StartTime [20..23], Duration [24..27], WaveSpeed [28..31].
    VfxConfigurationDTO size = 32 bytes. Offsets: ParticleCount [0..3], CurlNoiseStrength [4..7], WakeInfluence [8..11], GravitySinkingSpeed [12..15], AmbientSize [16..19], DensityScale [20..23], CsvProfileHash [24..27], Version [28..31].
    FrameConstantsData size = 128 bytes. Offsets: CameraPositionTime [0..15], CameraRightDeltaTime [16..31], CameraUpDensity [32..47], FlowFieldCenterCellSize [48..63], ShellParams [64..79], MetaParams [80..95], CameraVelocityStretch [96..111], Pad0 [112..127]. L1 note: exactly two 64-byte lines.
    VehicleWakeJobResult size = 48 bytes. Offsets: PositionWS float3 [0..11], Radius [12..15], VectorWS float3 [16..27], Lifetime [28..31], Intensity [32..35], Flags [36..39], Pad0 [40..43], Pad1 [44..47].
    Runtime guard: `ValidateNativeStructLayouts()` checks `UnsafeUtility.SizeOf<T>()` for all primary DTOs.
  </ARM64_CHECK>
  <ZERO_GC_CHECK>
    `Tick(float dt)` contains no LINQ, closures, boxing, `new string`, `ToString`, ParticleSystem calls, Instantiate, or CPU particle-position loops in the touched runtime files.
    Persistent simulation data is GPU-resident or Vault-resident. Frame constants, empty sentinels, and mock wake pages are written directly with `GraphicsBuffer.LockBufferForWrite`; no managed upload arrays remain on that path.
    CSV monitoring uses a background staged reader and preallocated byte buffers; `Tick()` does no file IO, never waits on the staging lock, and parses only staged bytes. Camera fallback component lookup is cold-retry throttled when unbound. Measured GC proof is absent until Unity Profiler/GCMonitor run.
  </ZERO_GC_CHECK>
  <AUP_CHECK>
    Renderer reads `HectonFloatingOrigin.CurrentTotalOffsetDouble`, casts the camera-relative delta to float vector uniforms only after subtraction, and compute evaluates noise on `positionWS - _HectonFloatingOriginOffset.xyz`.
    Origin-shift listener offsets active particle positions via `_AupShiftOffset`, preserving visual continuity during rebases.
  </AUP_CHECK>
  <DEAR_LIE_CHECK>
    Rejected Navier-Stokes/true suspended sediment. Used hash/curl fake, triangle-wave mock flow/wake motion, DTO wake falloff, camera-frustum respawn, and sonar/acoustic radial impulse. Low tier receives falling noir dust with wake loop compiled out.
  </DEAR_LIE_CHECK>
  <NAN_CHECK>
    Compute/shader reciprocal and root sites in touched SHINOBU shader files are locally guarded with `max(...)`; PCRE scan finds no `rcp(`, `rsqrt(`, or `sqrt(` without a local `max` guard.
  </NAN_CHECK>
  <DEPENDENCY_CHECK>
    No asmdef changes were made. Cross-domain state uses GlobalDataVault BufferIDs (`MarineSnowTuningConstants`, `MarineSnowDynamicWakes`, `MarineSnowMockFlowField`) and local mock DTOs. Signal scan confirmed existing `FluidImpulseSignal`, `WakeGeneratedSignal`, `AcousticPingSignal`, and `AupShiftSignal` lanes; SHINOBU uses typed `SignalBus<FluidImpulseSignal>` for wake publication and keeps `MockWakeSignal` only as the XML-mandated fallback proof contract. No new direct sibling runtime dependency was added for submarine, sonar, biome, or flow producers.
  </DEPENDENCY_CHECK>
  <H_PHI_CHECK>
    Persistent NativeArray data is requested through GlobalDataVault handles. SHINOBU_18 owns no private persistent NativeArray simulation state. Remaining managed arrays are non-simulation CSV staging bytes and immutable quad mesh topology; GPU upload staging for wake/frame payloads now writes mapped GraphicsBuffer memory directly.
  </H_PHI_CHECK>
  <BLACKBOX_CHECK>
    300-frame telemetry ring is active through `BufferID.MarineSnowTelemetryRing`. Non-finite state or mock GPU cost over 1500 us calls `DumpBlackBoxOnce()` and writes `Docs/AgentLogs/Dump_SILT_VFX.h8dump` plus legacy `Docs/AgentLogs/Dump_SILT_VFX.bin`.
  </BLACKBOX_CHECK>
  <COMPILE_GUARD>
    `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --no-incremental /p:UseSharedCompilation=false /nr:false /m:1` remains blocked by external missing DTO/job/signal symbols in `BinaryLayoutManifest`, ecosystem, seismic, somatic, and world sampler code. After fixing SHINOBU_18's unsigned telemetry field, filtered build output showed no remaining SHINOBU_18 C# errors before external errors.
    No circular dependency was introduced because no asmdef was edited.
  </COMPILE_GUARD>
</SELF_AUDIT>
