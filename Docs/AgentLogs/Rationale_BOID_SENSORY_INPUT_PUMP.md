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

Problem: Sensory data was vault-backed but still cached as local persistent NativeArray fields, which fails the stricter H-PHI interpretation.
Solution: Replaced `_boidSensoryThreatsNative` and `_boidSensoryBlackBox` fields with `VaultBufferHandle<float4>` and `VaultBufferHandle<BoidSensoryBlackBoxEntry>`. The system now resolves transient NativeArray views from `GlobalDataVault` only at the upload/blackbox boundary.
Rejected Alternatives: Keeping local NativeArray views was rejected because field ownership still looks like private state even when the backing allocation lives in the vault. Resolving with managed arrays was rejected for GC and copy cost.
Scalability potential: Low resolves a 16-slot threat view and 300-entry telemetry ring only when the simulation dispatch is active. Middle/High/Ultra keep the same vault contract, so richer visual response does not fork memory ownership.
Hardware Impact: Adds an estimated ~1 us/frame handle resolution cost on i3/MX350 and removes private persistent sensory collection ownership. Persistent memory remains fixed at 256 bytes for threats plus 19.2 KB for blackbox.

Problem: The boid surface still published three outgoing events through `GlobalSignals.Publish`, which is a legacy wrapper despite forwarding to typed lanes.
Solution: Replaced those calls with direct `SignalBus<DebrisSpawnSignal>.Push`, `SignalBus<AcousticPingSignal>.Push`, and `SignalBus<SwarmDispersedSignal>.Push`.
Rejected Alternatives: Keeping wrappers was rejected because the inquisition explicitly requires typed lanes. Adding new duplicate signals was rejected because existing typed payloads already cover debris, acoustic pings, and swarm dispersion.
Scalability potential: Low/Middle/High/Ultra all use the same typed lanes and frame snapshots; higher tiers can add visual consumers without changing the producer.
Hardware Impact: Expected save is small but real: removes wrapper queue writes for these boid-originated events and avoids legacy lane pressure. Estimated 1-3 us/frame saved during kill/frenzy/dispersion bursts, 0 steady-state cost.

Problem: The third build probe still cannot validate the sensory surface because unrelated systems fail first.
Solution: Logged `Docs/AgentLogs/Build_BOID_SENSORY_INPUT_PUMP_Polish3.txt` and scanned for touched-file diagnostics. None reference `SargassumMicroFaunaBoids.cs`, `H8Memory.cs`, `SargassumMicroFaunaBoids.compute`, or `BoidFishInstanced.shader`.
Rejected Alternatives: Editing `LockstepStateValidator`, `EcosystemDirector`, or `SubmarineFluidDynamics` was rejected as cross-domain compile-wall work.
Scalability potential: No runtime impact; this is integration debt outside the sensory pump.
Hardware Impact: 0 us/frame; build-only blocker.

Problem: The stricter data-sovereignty pass showed that the wider boid runtime still cached vault-backed buffers as persistent local `NativeArray` fields, and predator bite staging still used a local persistent `NativeQueue`.
Solution: Converted static obstacles, boid state, food-chain telemetry, leviathan path/node state, foveated LOD state, simulation frame constants, and threat-grid staging to `VaultBufferHandle<T>` fields. `NativeRingBuffer<T>` now stores only a vault handle and cursor metadata. Predator bite job output now uses vault-backed fixed `BoidKillSignal` plus count buffers under `BufferID.SargassumKillSignals` and `BufferID.SargassumKillSignalCount`.
Rejected Alternatives: Keeping local views was rejected because the file still looked like a private data owner. Keeping `NativeQueue<BoidKillSignal>` was rejected because the job is single-threaded and a fixed vault array plus count is cheaper and easier to audit. Moving predator bite results into global `SignalBus<T>` was rejected because it would delay same-frame boid state mutation until the global signal flush.
Scalability potential: Low keeps fixed kill staging and resolves only the buffers touched by the active path. Middle/High keep the same vault contract while allowing richer predator/beam visuals. Ultra can add visual consumers without changing memory ownership or staging layout.
Hardware Impact: Vault handle resolution adds an estimated 3-6 us CPU on active boid frames versus cached local views, pending profiler proof. Replacing queue enqueue/dequeue with fixed array/count staging is estimated to save 2-4 us during predator bite bursts and 0 steady-state; it also removes one persistent local native allocation from the boid system.

Problem: The full native-state eviction still needed ARM64 layout proof and a clean compile pass.
Solution: Changed non-GPU vault/job structs touched by this pass to `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = ...)]`, added `UnsafeUtility.SizeOf` validation for kill signals, foveated LOD packets, static obstacle cache entries, food-chain telemetry, and sensory blackbox entries, then logged a successful `dotnet build` at `Docs/AgentLogs/Build_BOID_SENSORY_INPUT_PUMP_Polish7.txt`.
Rejected Alternatives: Changing GPU interop structs like `BoidData`, `GrazingAnchorData`, `MassiveThreatData`, and `SimulationFrameConstants` to Pack=1 was rejected because their HLSL stride/offset contracts already require 4-byte interop alignment and are separately validated.
Scalability potential: Low/Middle/High/Ultra all share fixed binary layouts, so platform tier changes do not fork native memory contracts.
Hardware Impact: Layout validation is cold-path. Build succeeded with one unrelated duplicate-source warning, so runtime impact remains 0 us/frame.

Problem: The typed light and acoustic lanes can contain more signals than this boid pump is allowed to scan, but the local cap was reading the oldest entries. A remove/clear light signal could also leave a stale signal-light endpoint alive whenever the player flashlight remained on.
Solution: Read the newest capped snapshot window for both `SubmarineLightsChangedSignal` and `AcousticPingSignal`. On remove/clear/brownout, clear the cached signal-light intensity unconditionally; if the player flashlight is on, the threat is rebuilt from the player-origin path on the same frame. Changed the ping slot cursor to `uint` so long sessions cannot wrap a signed cursor into a negative modulo result.
Rejected Alternatives: Increasing the cap was rejected because it spends CPU on old events instead of fixing ordering. Adding a private active-light table was rejected as new local state in the sensory pump. Destructive queue reads were rejected again because they would race other signal consumers.
Scalability potential: Low/Middle keep the same three ping slots and endpoint-sphere lie, but now those slots always represent the freshest acoustic stimuli under burst pressure. High/Ultra keep the capsule SDF beam path while removal events stop false beam avoidance immediately.
Hardware Impact: Newest-window indexing is arithmetic-only and keeps the same bounded loop counts; estimated 0 us/frame added on i3/MX350. Clearing stale light intensity is a scalar assignment. The unsigned cursor removes a long-session crash class with no measurable frame cost.

Problem: The post-signal-order pass required a fresh compile proof, not inherited evidence.
Solution: Logged `Docs/AgentLogs/Build_BOID_SENSORY_INPUT_PUMP_Polish8.txt` from `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /maxcpucount:1`; it succeeded with 0 warnings and 0 errors.
Rejected Alternatives: Reusing the Polish7 compile was rejected because the source changed after that pass.
Scalability potential: No runtime scaling impact; this is validation evidence.
Hardware Impact: 0 us/frame; build-only proof.

Problem: The shader sensory loop clamped radii, but malformed threat payloads could still feed `max`, `dot`, direct segment division, or closest-point projection before all NaN guards fired.
Solution: Added finite checks for `_EncounterPredatorAUPBuffer` and `_PredatorAUPBuffer` slots before radius math. Hardened `ClosestPointOnSegment` against non-finite sample/start/end, non-finite segment length, and non-finite projection, and changed projection division to `projection * rcp(max(segmentLengthSq, EPSILON))`.
Rejected Alternatives: Relying on CPU clamps was rejected because GPU buffers can be rebound by other systems or corrupted by platform-specific translation. Replacing the capsule SDF with low-tier sphere math on all platforms was rejected because it would remove the high-end beam-parting behavior required by the prompt.
Scalability potential: Low/Middle still use sphere math with finite payload rejection. High/Ultra keep capsule SDF while the closest-point path is now NaN-safe across D3D, Metal, and mobile translators.
Hardware Impact: Adds two finite checks per active sensory/encounter slot and a finite projection guard in capsule mode. Estimated cost is under 1 us/frame on i3/MX350 for the capped 16-slot loop, with 0 disk or allocation impact.

Problem: The shader NaN pass needed a fresh validation record.
Solution: Logged `Docs/AgentLogs/Build_BOID_SENSORY_INPUT_PUMP_Polish9.txt` from `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /maxcpucount:1`; it succeeded with 0 warnings and 0 errors.
Rejected Alternatives: Claiming shader syntax safety without rerunning the project compile gate was rejected.
Scalability potential: No runtime scaling impact; this is validation evidence.
Hardware Impact: 0 us/frame; build-only proof.
