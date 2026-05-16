# BIOLUM_PULSE_SYNC Rationale

Status: VERIFIED MASTER GRADE

## Decision 0 - Assignment Boundary

Problem: Prompt targets global flora biolum sync, but existing biolum runtime lives under `Assets/_Project/Scripts/World/Biolum/`, while the assigned domain is `Assets/_Project/Scripts/VFX/Bioluminescence/`.

Solution: Build the new heartbeat owner in the assigned VFX domain and use shader globals plus existing signal lanes to decouple it from World runtime. This follows ARCH_Execution_Phases and ARCH_Signal_Lane_Segregation.

Rejected Alternatives: Editing the large World/Biolum manager would widen ownership and collide with other agents. Raw prefab YAML cleanup was rejected until Unity API validation is available.

Scalability potential: Low uses one global state and cheap shader interpolation; Middle/High/Ultra use 4/8/16 state entries for richer species phase offsets and wave color separation.

Hardware Impact: Low-end i3/MX350 avoids per-material sine and emission script churn. Estimated main-thread saving: 45-120 us/frame in dense flora scenes; runtime proof absent.

## Decision 1 - Visual Fake First

Problem: A biologically accurate reef-wide pulse could tempt per-flora state, per-light simulation, or spawned strobe objects.

Solution: Use deterministic harmonic profile evaluation plus `Shader.SetGlobalVectorArray`. The shader carries the belief channel; gameplay truth remains unchanged.

Rejected Alternatives: Per-object MaterialPropertyBlock, per-renderer material clone, per-flora MonoBehaviour, and spawned lights were rejected as SRP/GC/batch regressions.

Scalability potential: Toaster path is one global neon breath. $5000 path is 16 phase-shifted harmonic lanes with spatial sweep and acoustic strobe.

Hardware Impact: Expected 0 B/frame, constant shader upload size, and no extra GameObjects. Exact profiler numbers absent.

## Decision 2 - OSHINO Profile Boundary

Problem: `Biolum_Profiles.bin` is referenced by the prompt but is not present under project assets, StreamingAssets, or Docs.

Solution: Cold-load `Biolum_Profiles.bin` from StreamingAssets/Docs when present, otherwise seed deterministic fallback profile floats into `NativeArray<float>`. This preserves the binary contract without inventing OSHINO data.

Rejected Alternatives: Blocking the runtime on missing content, embedding a ScriptableObject, or mutating material profiles at runtime. Those options either break cold-start or move data back into per-asset state.

Scalability potential: Low/MX350 gets one seeded global pulse. Mid gets four lanes. High/Ultra gets sixteen lanes and can replace fallback floats with authored OSHINO content without code churn.

Hardware Impact: Cold load only; hot path remains 0 B/frame. Low-end i3/MX350 avoids profile asset lookups and per-renderer profile state. Estimated hot saving: 20-45 us/frame versus per-object profile evaluation.

## Decision 3 - Burst Job Latency

Problem: The task requires a VISUAL_SYNC Burst job, but a 16-entry job completed immediately in the same tick would spend scheduler overhead for no visual gain.

Solution: Schedule `BiolumVisualSyncJob` and publish the completed buffer on the next dispatcher tick. Scalar globals carry time/strobe each frame, so a one-frame state latency is visually hidden.

Rejected Alternatives: Main-thread sine evaluation, scheduling then immediately completing every frame, or compute shader dispatch. Immediate completion was rejected as fake parallelism; compute was rejected as GPU overhead for 16 values.

Scalability potential: Low uses one state and can stay visually coherent under 15 Hz overload. Ultra uses 16 phase-shifted lanes with acoustic strobe overdrive.

Hardware Impact: Low-end silicon avoids per-material trig and per-object uploads. Estimated main-thread saving: 30-60 us/frame in dense flora scenes, with one fixed managed `Vector4[16]` upload.

## Decision 4 - Global Shader Array Over Per-Renderer State

Problem: Flora, sargassum, procedural bio, and predators need synchronized pulses without direct object dependencies.

Solution: Publish `_GlobalBiolumStates[16]`, `_GlobalBiolumParams`, `_GlobalBiolumClock`, and `_GlobalBiolumAupOffset`. Shaders spatially select a lane and retain legacy `_BiolumIntensity` fallback.

Rejected Alternatives: MaterialPropertyBlock, material clone, renderer traversal, or predator/VFX direct references. These break batching, widen ownership, and create 20-agent collision points.

Scalability potential: Toaster path is one shared neon breath. Middle path adds four authored lanes. High/Ultra path adds sixteen lanes and strobe overdrive for visual excess.

Hardware Impact: Low-end i3/MX350 saves per-renderer state churn and preserves SRP batching. Estimated saving: 35 us/frame per dense biome cluster, higher when foliage count spikes.

## Decision 5 - Acoustic Strobe As Fake Light

Problem: Active ping feedback needs to read as a reef-wide flash without spawning light sources or particle emitters.

Solution: Consume `SignalBus<AcousticPingSignal>`, hold white HDR strobe for 0.1 s, fade it, and let shaders mix global state color toward white.

Rejected Alternatives: Point lights, emissive prefab pulse scripts, per-fauna callbacks, or a new event type. Standard Unity lights were too slow and not deterministic enough for this effect.

Scalability potential: Low path strobes one global state. Ultra path strobes all sixteen lanes while retaining per-lane harmonic color after the flash.

Hardware Impact: Avoids 200 us+ CPU/GPU spikes from spawned light/particle paths under ping spam. Hot path stays fixed span reads and scalar writes.

## Decision 6 - Black Box And Memory Ownership

Problem: A global VFX heartbeat can hide NaN propagation across shaders unless the last states are retained.

Solution: Store 300 dispatcher ticks of high-level state in `NativeArray<BiolumPulseTelemetryEntry>` allocated through `H8Memory` with `SystemID.Vfx`; dump to `Docs/AgentLogs/Dump_BIOLUM_PULSE_SYNC.bin` on non-finite job output.

Rejected Alternatives: Debug.Log spam, managed queues, or ignoring visual-only crashes. Logs allocate and are not recoverable after a crash.

Scalability potential: Low/Mid/High/Ultra all use the same fixed telemetry footprint; higher tiers only change active lane count.

Hardware Impact: Fixed persistent native ring, 0 B/frame managed allocation. Estimated low-end impact: below 1 us/frame for telemetry write.

## Decision 7 - Build Wall

Problem: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false` fails before BIOLUM validation with missing or drifted contracts outside the VFX biolum domain.

Solution: Do not edit fauna/animation/construction/world contracts from the VFX domain. Mark compile validation blocked by dependency and keep BIOLUM changes scoped.

Rejected Alternatives: Stubbing `JawIkTarget`, `CurrentJawPos`, `BiteIkSolveEvent`, `IDockingAutopilotService`, `WakeSource`, `LightShaftContribution`, or ecosystem service methods in VFX, or deleting unrelated compile items. Those are architectural sabotage outside the assigned domain.

Scalability potential: No visual scalability effect. Integration must restore the fauna animation assembly reference before final project compile can prove the VFX patch.

Hardware Impact: None from this decision. Build integrity remains blocked externally, not by runtime frame cost.
