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
