# BIOLUM_PULSE_SYNC Log

## 2026-05-16 Start

What was wrong: Existing flora glow is driven by scattered globals and legacy controller paths. Prompt requires a single global heartbeat and no per-object material property blocks.

What was done: Assignment extracted from `Docs/Tasks/CURRENT_BATCH.md`; relevant mandates and stable docs read; no stale `Status_BIOLUM_PULSE_SYNC.md` or `Rationale_BIOLUM_PULSE_SYNC.md` existed.

Cinematic Cheats used: Deterministic shader-global pulse selected over physical or per-flora simulation.

Exact Microseconds saved: Pending compile/profiler proof. Static estimate range: 45-120 us/frame in dense flora scenes.

## 2026-05-16 Implementation

What was wrong: Flora/floor/ocean biolum globals existed, but there was no VFX-domain owner for a synchronized multi-profile pulse, no OSHINO binary ingestion path, no acoustic ping strobe, no 15 Hz overload downgrade, and predators were not guaranteed to read the same global heartbeat.

What was done: Added `Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs`. It cold-loads `Biolum_Profiles.bin` from StreamingAssets/Docs when present, otherwise seeds deterministic fallback profile floats into `NativeArray<float>`. It schedules `BiolumVisualSyncJob` Burst work into a fixed `NativeArray<float4>` and uploads `_GlobalBiolumStates` with `Shader.SetGlobalVectorArray`. It consumes `AupShiftSignal`, `FrameTimeSignal`, and `AcousticPingSignal`; uses H8 dispatcher time; limits overload updates to 15 Hz; and records a 300-tick native blackbox dumping `Docs/AgentLogs/Dump_BIOLUM_PULSE_SYNC.bin` on non-finite output. Patched `Hecton_ProceduralBio`, `Hecton_KelpMaster_GPUI`, `Hecton_SargassumMaster`, `Hecton_LeviathanOrganic`, and `Hecton_LeviathanTentacleIndirect` to consume the global array while retaining legacy fallback intensity.

Cinematic Cheats used: One global shader heartbeat replaces per-flora physical simulation. Acoustic pings are faked as a white HDR emissive strobe instead of spawned lights, particles, or renderer traversal. Spatial variety is a hash/phase lane pick, not per-entity state.

Exact Microseconds saved: Estimated 45 us/frame from removing per-material pulse math in dense flora, 35 us/frame from avoiding per-renderer material state churn, 10 us/frame from low-tier one-state upload, 30 us/frame under overload by dropping job cadence to 15 Hz, and 200 us+ avoided per ping burst by not spawning lights/particles. Profiler proof is blocked by external compile failures.

Validation: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false` failed outside BIOLUM. Observed external blockers include missing `Hecton8.VFX.Wakes`, `IDockingAutopilotService`, `LightShaftContribution`, duplicate `LockstepStateValidator.SanitizeFinite`, and `IEcosystemDirectorService` signature drift. No BIOLUM compile errors surfaced before the external wall.

## 2026-05-16 Multiplatform Inquisition Pass

What was wrong: BIOLUM still held private persistent NativeArray fields, used Pack=4 telemetry, and loaded profile floats through per-value binary reads. High-tier visuals were multi-lane, but not overdriven per-pixel.

What was done: Evicted BIOLUM native storage into `GlobalDataVault` with new buffer IDs `BiolumProfileFloats`, `BiolumGlobalStates`, and `BiolumBlackBox`. The runtime now stores only `VaultBufferHandle<T>` and resolves locked transient views for jobs/writes. Packed `BiolumPulseTelemetryEntry` as explicit Pack=1 Size=40 for ARM64/Quest. Replaced profile loading with a 512-byte stackalloc sequential read. Added High/Ultra shader secondary-lane overdrive driven by `_GlobalBiolumClock`, while Low/MX350 remains one global Dear Lie.

Cinematic Cheats used: Low tier uses one global pulse plus shader triangle overdrive off. High tier uses secondary lane interference from the existing global array rather than real lights, compute particles, raymarch volume, or per-flora simulation.

Exact Microseconds saved: Native eviction does not claim frame savings; it buys DataVault ownership and fragmentation control. Stackalloc sequential profile read is estimated to reduce cold MicroSD read overhead by 50-200 us versus per-float `BinaryReader` dispatch. High-tier overdrive deliberately spends extra pixel ALU; exact GPU cost requires capture after the external compile wall.

Validation: Static scan shows no BIOLUM `Update`, `FixedUpdate`, `LateUpdate`, `MaterialPropertyBlock`, material clone, `string.Format`, or `Debug.Log`. Latest `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false` fails externally at `GlobalDataVault.ValidateAbiLayout` duplicate before BIOLUM validation.

## 2026-05-16 Job-System Inquisition Pass

What was wrong: The DataVault pass still let `CompleteScheduledJobAndPublish()` call `JobHandle.Complete()` from `Tick()` without first proving the VISUAL_SYNC job had finished. That is a latent frame spike on saturated worker threads and violates the phase discipline even when the job payload is small.

What was done: Added explicit telemetry flags for non-finite output and job overruns. The runtime now checks `_stateJobHandle.IsCompleted` before publishing. If the job overruns, it keeps the last uploaded shader state, records the overrun in the blackbox, and does not schedule a second job while the profile/state DataVault buffers remain locked. Removed the defensive per-tick update self-registration attempt so the dispatcher registration stays in lifecycle code.

Cinematic Cheats used: On a late job, the shader reuses the previous global pulse for one frame instead of blocking the main thread or recomputing sine math on the CPU. That is the correct Dear Lie: visual continuity over simulation purity.

Exact Microseconds saved: Normal frame savings are 0 us because a completed handle still finalizes immediately. Worst-case saved time is the avoided worker-fence stall; exact microseconds require Unity profiler capture after external compile walls are cleared. The prior static estimates remain unchanged: 45 us/frame dense-flora sine avoidance, 35 us/frame renderer-state churn avoidance, 10 us/frame low-tier lane reduction, 30 us/frame overload cadence reduction, and 200 us+ per ping burst from avoiding spawned lights/particles.

Validation: `dotnet build Hecton8.Core.csproj --no-restore -v:diag -m:1 /p:UseSharedCompilation=false` still fails outside BIOLUM. Current first blockers are `GameBootstrapper.Initialize` signature drift and `ToolDurabilitySystem` missing `_itemStates`, `_pendingDecayDt`, `_wearMultipliers`, `_slotActive`, `_breakdownEvents`, and `_disposeHandle`. Filtered build evidence shows no `BiolumPulseSyncRuntime`, `GlobalDataVault`, `H8Memory`, or `HectonShaderGlobalDataVaultBridge` errors. Static scan still finds no BIOLUM `Update`, `FixedUpdate`, `LateUpdate`, `MaterialPropertyBlock`, `string.Format`, `EventBus`, `Debug.Log`, local `new NativeArray`, or private persistent `NativeArray` fields.

## 2026-05-16 Shader NaN Sweep

What was wrong: `Hecton_SargassumMaster` still used direct division by radius squared in cut-mask and propwash falloff math in both the forward path and the duplicate shadow/depth helper path. The radius had a clamp, but this was still weaker than the project NaN vaccination standard.

What was done: Replaced the direct divisions with `rcp(max(radiusSq, 0.0001))` and multiplication. The visual equation is unchanged under normal radii; the denominator floor is explicit for mobile GPU compilers.

Cinematic Cheats used: Same dot-product radius fake, safer reciprocal form. No physical propwash or cut simulation was added.

Exact Microseconds saved: 0 us claimed. This is a stability patch, not an optimization patch.

Validation: Follow-up shader scan shows the edited Sargassum radius sites now resolve through `rcp(max(...))`; no direct `washDistSq / (washRadius * washRadius)` or `dot(delta, delta) / (radius * radius)` remains in that shader.

## 2026-05-16 Blackbox And Lifecycle Sweep

What was wrong: The blackbox dump ABI did not match the Pack=1 telemetry entry because it wrote selected fields through `BinaryWriter` and skipped reserved bytes. The runtime also still performed external wiring from `Awake()` and could re-read profile data on repeated enables.

What was done: Replaced the dump path with raw unmanaged writes: a Pack=1 16-byte dump header plus 300 raw Pack=1 40-byte telemetry entries. Removed `Awake()` external DataVault work. Tick now uses cached `_dataVault` only and fails closed if lifecycle injection did not provide it. Added `_profilesLoaded` so `Biolum_Profiles.bin` is read once per runtime lifetime unless the editor reload command explicitly resets it.

Cinematic Cheats used: Reusing the vaulted profile after first cold load preserves the same global heartbeat without touching disk again. On low hardware the visual lie stays one global pulse; no repeated profile import is allowed to steal frame time.

Exact Microseconds saved: Blackbox ABI patch claims 0 us hot-path savings. Profile I/O latch avoids an estimated 50-200 us MicroSD cold-read stall on each repeated enable after the first load. Tick registry fallback removal avoids an unbounded hot-path lookup if lifecycle setup fails; exact microseconds are not claimed.

Validation: Static scan reports no BIOLUM `Awake`, `Update`, `FixedUpdate`, `LateUpdate`, `MaterialPropertyBlock`, `string.Format`, `EventBus`, `Debug.Log`, `BinaryWriter`, local `new NativeArray`, or private persistent `NativeArray` fields. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false` still fails outside BIOLUM: `EcosystemRuntimeInstaller` cannot find `Hecton8.AI.Ecosystem`, and `SubmarineFluidDynamics` cannot find `VaultNativeBuffer<>`. No BIOLUM/DataVault/H8Memory/shader-bridge compile errors surfaced.

## 2026-05-16 VISUAL_SYNC Overrun Tripwire

What was wrong: Job overruns were recorded as flags, but a permanently late VISUAL_SYNC job could keep stale shader states forever without forcing a dump.

What was done: Added `_jobOverrunFrames` and a 300-frame threshold. If `_stateJobHandle.IsCompleted` stays false for 300 consecutive ticks, BIOLUM dumps the fixed-size blackbox with reason `TelemetryFlagJobOverrun`.

Cinematic Cheats used: The shader keeps rendering the last valid global pulse while the job is late. That is intentional visual continuity, not hidden simulation truth.

Exact Microseconds saved: 0 us claimed. This is survival instrumentation. Normal cost only occurs during overrun and is a saturated integer increment plus one branch.

Validation: Static scan still reports no BIOLUM `Awake`, `Update`, `FixedUpdate`, `LateUpdate`, `MaterialPropertyBlock`, `string.Format`, `EventBus`, `Debug.Log`, `BinaryWriter`, local `new NativeArray`, or private persistent `NativeArray` fields. Latest `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false` fails outside BIOLUM in `ArchitectEyeVisualizer` (`VaultProbeUtility.IsFinite`) and `EcosystemPopulationBalancer` (`SignalBusRegistry`, `EntityDeathSignal`, ref return issue). No BIOLUM compile errors surfaced.
