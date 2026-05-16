# BOID_SENSORY_INPUT_PUMP Rationale

## Mandates Loaded

- AI_Flocking_Boids_Swarm_SpatialHash_Logic
- GPU_Compute_Kernels_Kernels_Optimization_MX350
- MATH_AUP_Determinism_Sync
- OPT_Zero_GC_Policy_AllocFree_Mandate
- ARCH_Execution_Phases
- ARCH_Signal_Lane_Segregation
- AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation
- REND_GPU_Sovereignty
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First

## Decisions

Problem: The prompt domain path `Assets/_Project/Scripts/AI/Boids/` does not exist, but the project already has a GPU boid system in `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` and `Assets/_Project/Art/Shaders/SargassumMicroFaunaBoids.compute`.
Solution: Implement the sensory pump in the existing active GPU boid surface and log this as a critical cross-domain interface correction.
Rejected Alternatives: Creating a new AI/Boids system would duplicate dispatch ownership, miss the live compute shader, and burn frame time on a dead path.
Scalability potential: Low uses a fixed 16-slot buffer and simple sphere math. Middle adds decayed acoustic slots. High enables beam capsule checks. Ultra can raise visual response while retaining the same buffer contract.
Hardware Impact: Expected cost on i3/MX350 is bounded to a 16-entry loop and one upload of 256 bytes per simulation frame; projected under 0.02 ms GPU and under 0.01 ms CPU.

Problem: Existing predator AUP data is already bound to `_PredatorAUPBuffer`, and the prompt requires light/sound stimulus to enter the existing `StructuredBuffer<float4>` threat array.
Solution: Keep `_PredatorAUPBuffer` as the sensory 16-slot array and move encounter predator input to `_EncounterPredatorAUPBuffer` so old predator behavior remains isolated.
Rejected Alternatives: Packing predator, light, and acoustic stimuli into the same slots would destroy encounter semantics and force branch-heavy slot decoding.
Scalability potential: Same sensory buffer supports cheap endpoint spheres and expensive capsule interpretation without changing C# buffer layout.
Hardware Impact: Avoids extra structured buffer stride changes and keeps upload bandwidth fixed at 256 bytes.

Problem: Player position must not be read from `Transform.position`.
Solution: Use `PlayerRuntimeContext` movement/look snapshots and AUP signal payloads. Submarine runtime is used only for vehicle center when available, falling back to player snapshot data.
Rejected Alternatives: Direct player Transform polling would violate AUP determinism and create hidden ordering dependencies.
Scalability potential: Snapshot data can be produced at lower cadence on weak devices while high-end can use predicted AUP and precise look vectors.
Hardware Impact: Snapshot reads are scalar memory reads; no scene graph traversal.

Problem: `SubmarineLightsChangedSignal` has a legacy dequeue API, but boids need non-destructive access alongside cognition.
Solution: Consume `SignalBus<SubmarineLightsChangedSignal>.GetFrameSnapshot()` directly; the legacy queue is already backed by the typed SignalBus lane through `CreateQueue<T>`.
Rejected Alternatives: Draining `GlobalSignals.TryDequeueSubmarineLightsChanged` inside boids would advance the lane cursor and cause race-order behavior. Duplicating `Publish` into `SignalBus.Push` was rejected after inspection because it would enqueue duplicate light events.
Scalability potential: SignalBus snapshot allows multiple consumers without extra allocations.
Hardware Impact: Zero extra ring-buffer pushes; only bounded snapshot scan of 8 events.

Problem: Compute shader ALU increases when boids evaluate sensory threats.
Solution: Use one fixed 16-entry sensory loop; low and simplified tiers use sphere distance only, full tier interprets slot 1 as a beam capsule with one closest-point projection.
Rejected Alternatives: Render-side SDF, per-fragment buffer sampling, or CPU-expanded beam sphere chains. These either duplicate work per pixel or inflate upload slots.
Scalability potential: Low = endpoint sphere and 4-slot practical use. Middle = endpoint sphere plus ping decay. High = capsule SDF and albedo flag. Ultra = same buffer contract with stronger visual response, no layout change.
Hardware Impact: Low-tier ALU adds approximately 12 scalar ops per active slot; full-tier flashlight adds approximately 22 scalar ops for slot 1. CPU upload remains 256 bytes; expected CPU cost stays below 10 us/frame on i3/MX350.

Problem: Final build cannot validate this surface because unrelated integration files fail first.
Solution: Ran three `dotnet build Hecton8.Core.csproj` attempts and checked logs for touched-file diagnostics. None reference the boid sensory files.
Rejected Alternatives: Editing `GlobalRegistry`, `LockstepStateValidator`, bootstrap references, or missing visual signal contracts outside the assigned domain.
Scalability potential: No runtime scaling impact; this is an integration dependency wall.
Hardware Impact: 0 us/frame; build-only blocker.

Problem: The inquisition superseded the original blackbox N/A and requires last-300-frame sensory heartbeat evidence.
Solution: Added a vault-backed `BoidSensoryBlackBoxEntry` ring using `BufferID.SargassumBoidSensoryBlackBox`, `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`, a size validation gate, state hashes, active flags, and anomaly-only binary dump to `Docs/AgentLogs/Dump_BOID_SENSORY_INPUT_PUMP.bin`.
Rejected Alternatives: Per-frame text logging was rejected for Steam Deck MicroSD pressure. A local managed list was rejected for GC and H-PHI failure. A separate owner allocation was rejected because `GlobalDataVault` already owns persistent buffers for this surface.
Scalability potential: Low records fixed slot/radius telemetry only. Middle records ping radii and LOD tier. High records capsule/light flags. Ultra keeps the same 64-byte entry while downstream tools can decode hashes against richer visual captures.
Hardware Impact: Adds 19.2 KB vault memory and an estimated 1-2 us/frame CPU for five `float4` finite checks and hashes on i3/MX350. Disk I/O is 0 in normal frames and one 19.2 KB binary write on anomaly.

Problem: ARM64/Quest and Metal/Mac can punish implicit layout and shader-bool assumptions.
Solution: New blackbox struct is `Pack=1`, fixed `Size=64`, and checked with `UnsafeUtility.SizeOf`. Compute beam mode uses a `uint` mask. Existing thread group size is 64, below Metal's 1024 limit.
Rejected Alternatives: Relying on C# default struct packing was rejected because implicit padding is exactly the Quest failure mode. Keeping HLSL `bool` control state was rejected because Metal translation is less predictable than integer masks.
Scalability potential: Low/Middle/High/Ultra all use the same binary layout and shader define limits, so platform tier changes do not fork the buffer contract.
Hardware Impact: Layout validation is cold-path only. Shader mask change is cost-neutral versus bool and safer for cross-compilation.

Problem: God-mode visual response cannot regress toaster mode or add per-fragment SDF sampling.
Solution: Kept low-tier sensory math as endpoint sphere and full-tier math as compute capsule SDF, then amplified render response with an existing triangle-wave pulse on the `BOID_FLAG_LIGHT_STIMULUS` bit.
Rejected Alternatives: Raymarching, POM, SSS, visor salt crystals, and hull dents were rejected in this domain because the assignment owns boid sensory compute, not visor, hull, or material pipeline systems. A render-side threat-buffer sample was rejected again because it scales with pixels instead of boids.
Scalability potential: Low = sphere lie and one state bit. Middle = sphere plus ping decay. High = capsule SDF and albedo/biolum pulse. Ultra = stronger beam-parting spectacle through the same flag without buffer or thread-group changes.
Hardware Impact: The triangle pulse adds about 3-5 fragment ALU only on visible fish. It avoids the estimated 8 us/frame cost of per-fragment beam SDF on dense fish visibility.

Problem: Revalidation found new project-wide compile blockers after the sensory hardening pass.
Solution: Logged `Docs/AgentLogs/Build_BOID_SENSORY_INPUT_PUMP_Polish2.txt` and `Docs/AgentLogs/Build_BOID_SENSORY_INPUT_PUMP_AssemblyCSharp.txt`, then scanned for touched-file diagnostics. None reference the boid sensory files or `H8Memory.cs`.
Rejected Alternatives: Editing missing Tether or RealtimeCSG files was rejected as outside the AI/COMPUTE sensory domain and would mask another agent's dependency wall.
Scalability potential: No runtime scaling impact. This preserves the sensory patch while the integrator repairs project references.
Hardware Impact: 0 us/frame; build-only blocker.
