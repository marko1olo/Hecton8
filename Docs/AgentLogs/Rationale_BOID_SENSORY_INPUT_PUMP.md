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

Problem: The sensory threat upload used `LockBufferForWrite`, but it still targeted one GraphicsBuffer. On weak drivers and async GPU queues, a single upload/read resource can serialize if the CPU maps the buffer while the previous dispatch still reads it.
Solution: Split the sensory threat upload into two 16-slot GraphicsBuffers and select by `_frameParity`. The CPU writes the parity-selected buffer in `UpdateBoidSensoryThreats`, then `BindSimulationUniforms` binds that same buffer to `_PredatorAUPBuffer` immediately before dispatch. The CPU-side source remains a vault-resolved `NativeArray<float4>` view.
Rejected Alternatives: Keeping one GraphicsBuffer was rejected because it violates the batch bandwidth discipline. Triple buffering was rejected because the payload is only 256 B and one dispatch consumes the upload before `_frameParity` flips, so two buffers cover the read/write hazard without unnecessary VRAM churn. Copying through a managed array was rejected for GC and bandwidth.
Scalability potential: Low/Middle/High/Ultra keep the same 16-slot threat contract. Low still uses endpoint spheres, High/Ultra still use capsule SDF, and the upload path no longer depends on a single mutable GPU resource.
Hardware Impact: Adds one 16-slot float4 GraphicsBuffer, 256 B VRAM plus Unity resource overhead. Expected CPU impact is neutral to slightly positive under driver contention; estimated 0-2 us/frame saved on MX350/Steam Deck when the previous frame has not retired, pending GPU profiler proof.

Problem: The first Polish10 build attempt failed because `Temp/obj/Hecton8.Core/project.assets.json` was missing, not because of source diagnostics.
Solution: Ran `dotnet restore Hecton8.Core.csproj`, logged `Docs/AgentLogs/Restore_BOID_SENSORY_INPUT_PUMP_Polish10.txt`, then reran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /maxcpucount:1` and logged `Docs/AgentLogs/Build_BOID_SENSORY_INPUT_PUMP_Polish10.txt`; build succeeded with 0 warnings and 0 errors.
Rejected Alternatives: Treating the missing assets file as a compile wall was rejected because restore is the correct non-code repair for `NETSDK1004`.
Scalability potential: No runtime scaling impact; this is validation hygiene.
Hardware Impact: 0 us/frame; build-only proof.

Problem: The ping-pong sensory upload removed the read/write resource hazard, but unchanged frames still locked and copied the same 256 B payload into the parity-selected GPU buffer.
Solution: Added `HashBoidSensoryThreatUpload` over the fixed 16-slot vault view and per-parity upload cache state. Each sensory GraphicsBuffer receives its first upload after creation; later frames skip `GraphicsBufferUploadUtility.UploadNativeArray` only if the selected buffer already holds the same payload hash. Cache state is reset when sensory buffers are recreated or released.
Rejected Alternatives: Blind upload every dispatch was rejected by the bandwidth discipline rule. A managed dirty-state copy was rejected for GC. Hashing only active slots was rejected because stale inactive slots would be invisible to the dirty gate; the full 16-slot contract is cheap and exact.
Scalability potential: Low/Middle benefit most during steady submarine/no-ping frames because the endpoint-sphere payload can remain unchanged. High/Ultra keep the capsule SDF behavior while avoiding needless PCIe/cache pressure when the beam payload is stable.
Hardware Impact: Hashing 16 `float4` slots adds a small fixed CPU loop, estimated under 1 us/frame on i3/MX350. Skipping an unchanged upload saves one buffer lock and 256 B memcpy for that parity buffer; estimated 0-2 us/frame in stable scenes, pending GPU profiler proof.

Problem: The Pack=1 audit can accidentally break GPU interop structs if applied blindly.
Solution: Added source comments above Pack=4 GPU/HLSL interop structs explaining that Pack=4 is intentional and guarded by `ValidateGpuStructLayouts`; non-GPU vault/job structs remain Pack=1 with explicit size gates.
Rejected Alternatives: Changing all structs to Pack=1 was rejected because HLSL `float3`, `float4`, `int4`, and `uint` scalar layout contracts are 4-byte interop contracts here. Leaving the exception undocumented was rejected because it invites future rot.
Scalability potential: Low/Middle/High/Ultra share one validated layout contract, so platform tier changes do not fork binary layout.
Hardware Impact: 0 us/frame; comments only.

Problem: Polish11 validation was interrupted by concurrent external edits in `ArchitectEyeVisualizer` and then by output DLL lock retries.
Solution: Logged three build probes. Strike1 and Strike2 failed only in `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs`, outside this domain. Strike3 succeeded and logged `Docs/AgentLogs/Build_BOID_SENSORY_INPUT_PUMP_Polish11_Strike3.txt`; the only remaining warnings are two `MSB3026` copy retries caused by another process holding `Hecton8.Core.dll`.
Rejected Alternatives: Editing `ArchitectEyeVisualizer` was rejected as outside the AI/COMPUTE boid sensory domain. Hiding the warnings was rejected because the build did not produce a zero-warning log.
Scalability potential: No runtime scaling impact; this is validation evidence.
Hardware Impact: 0 us/frame; build-only proof.

Problem: The post-success Polish12 build recheck hit a moving compile wall in unrelated systems while the boid sensory files stayed diagnostic-clean.
Solution: Logged `Build_BOID_SENSORY_INPUT_PUMP_Polish12.txt`, `Build_BOID_SENSORY_INPUT_PUMP_Polish12_Strike2.txt`, and `Build_BOID_SENSORY_INPUT_PUMP_Polish12_Strike3.txt`. Strike1 failed in `ArchitectEyeVisualizer`, `AbyssalThermalManager`, and `PlayerCriticalProceduralAudioRenderer`; Strike2 timed out with an empty log and the spawned process later exited; Strike3 failed in `HectonMarineSnowRenderer`. Scans found no diagnostics in `SargassumMicroFaunaBoids.cs`, `SargassumMicroFaunaBoids.compute`, `BoidFishInstanced.shader`, or `H8Memory.cs`.
Rejected Alternatives: Editing VFX, audio, thermal, or diagnostics code was rejected because those files are outside the AI/COMPUTE boid sensory domain and would mask other agents' dependency walls.
Scalability potential: No runtime scaling impact; this preserves the boid sensory buffer contract while external owners repair their compile breaks.
Hardware Impact: 0 us/frame; build-only blocker.

Problem: Shader-side NaN guards are necessary but not sufficient when a vault-resolved 16-slot threat view can contain stale or corrupted non-finite payload before upload.
Solution: Added a fixed-slot CPU sanitizer immediately after light/acoustic signal ingestion and before upload hashing. It zeros non-finite payloads, clamps positive radii below `SensoryThreatMinRadiusMeters`, clears inactive slots, and returns a slot-specific anomaly hash for the blackbox dump.
Rejected Alternatives: Trusting the shader guard was rejected because malformed data would still be uploaded and cached by the dirty gate. A managed staging copy was rejected for GC. Scanning only active slots was rejected because inactive stale slots are exactly the residue class this pass removes.
Scalability potential: Low/Middle/High/Ultra all retain the same 16-slot threat contract. Low still uses the endpoint sphere lie, High/Ultra still use capsule SDF, and all tiers get identical pre-upload sanitation.
Hardware Impact: Fixed 16 `float4` scan, estimated under 1 us/frame on i3/MX350. No measured profiler number is claimed. This can save an unknown GPU recovery cost by preventing non-finite data from reaching mobile/Metal drivers.

Problem: The blackbox dump path preserved 300 frames but wrote cursor order and could collapse detailed sanitizer findings into a generic anomaly constant.
Solution: Dump now writes oldest-to-newest from the ring cursor, marks the dump sentinel before disk I/O, catches `IOException`/`UnauthorizedAccessException`, and carries the pre-upload slot anomaly hash into the dump header unless later blackbox state is also invalid.
Rejected Alternatives: Repeated dump retry was rejected because Steam Deck MicroSD pressure matters after a crash. Generic-only anomaly codes were rejected because they hide the corrupt slot source. Per-frame text logging remained rejected for I/O and GC.
Scalability potential: Low records enough to reproduce the fixed-slot sensory contract. Middle/High/Ultra keep the same 64-byte entry while visual capture systems can correlate the anomaly header with richer effects.
Hardware Impact: 0 us/frame steady-state I/O. Ordered dump cost is one anomaly-only 19.2 KB write. Hash preservation is scalar math already inside the anomaly path.

Problem: Fresh Polish13 validation after sanitizer changes cannot reach this domain because unrelated systems currently fail first.
Solution: Logged `Build_BOID_SENSORY_INPUT_PUMP_Polish13.txt`, `Build_BOID_SENSORY_INPUT_PUMP_Polish13_Strike2.txt`, and `Build_BOID_SENSORY_INPUT_PUMP_Polish13_Strike3.txt`. Strike1 failed in `DiegeticGyroCompassRuntime`, `HeavyTowWinch`/`TetherSignals`, and `EcosystemDirector`. Strike2 failed in `DiegeticGyroCompassRuntime` and `EcosystemDirector`. Strike3 exited `-1` with an empty log. Touched-surface scans found no boid sensory diagnostics.
Rejected Alternatives: Editing UI navigation, tether physics, or ecosystem director files was rejected as cross-domain compile-wall work under the domain boundary rule.
Scalability potential: No runtime scaling impact; this is validation accounting.
Hardware Impact: 0 us/frame; build-only blocker.

Problem: The fixed-slot sensory threat consumer was reading newest acoustic pings, but the secondary acoustic panic path still scanned the oldest `MovementAcousticSignal` and `AcousticPingSignal` entries during burst pressure.
Solution: Changed both secondary consumers to compute `signalStart = max(0, Length - Limit)` and iterate from that index through the current frame snapshot. This keeps the freshest sound stimulus in both the threat-buffer path and the broader panic/scatter path.
Rejected Alternatives: Increasing scan caps was rejected because it spends CPU on old sound. A private acoustic event cache was rejected because SignalBus snapshots already provide a typed lane and local state would violate H-PHI.
Scalability potential: Low/Middle keep the same bounded scan and newest three ping slots. High/Ultra get fresher acoustic reaction without widening buffers or shader loops.
Hardware Impact: Same loop cap and 0 GC. Arithmetic-only index shift, estimated 0 us/frame added on i3/MX350.

Problem: `RegisterAcousticPanicBurst` and shader `ResolveAcousticPanicChaos` still assumed finite acoustic panic frame data before radius square, smoothstep, seed math, and directional normalization.
Solution: CPU now rejects non-finite origin/radius/duration/strength before writing acoustic panic state. Shader now rejects non-finite position/origin/radius/strength/time, validates radius square and distance square, and substitutes safe seed/time values when hash seed inputs are malformed.
Rejected Alternatives: Relying only on CPU signal validation was rejected because frame constants can be stale, corrupted, or rebound. Removing the acoustic chaos path was rejected because it is a cheap visual/audio panic fake required for engine noise and pings.
Scalability potential: Low uses the same dot-product/triangle-noise style acoustic lie. Middle/High/Ultra keep stronger visible panic behavior without risking NaN propagation on mobile/Metal.
Hardware Impact: Adds a small fixed set of scalar finite checks only when acoustic panic is active; estimated under 1 us/frame on i3/MX350, no profiler proof claimed. It prevents unbounded GPU failure cost from NaN propagation.

Problem: The acoustic hardening pass required fresh compile evidence instead of inherited Polish11/Polish13 state.
Solution: Logged `Docs/AgentLogs/Build_BOID_SENSORY_INPUT_PUMP_Polish14.txt` from `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /maxcpucount:1`; build succeeded with 0 warnings and 0 errors.
Rejected Alternatives: Reusing an older green build or reporting the previous external compile wall as current was rejected because source changed after both.
Scalability potential: No runtime scaling impact; this is validation evidence.
Hardware Impact: 0 us/frame; build-only proof.

Problem: `math.saturate` and L1 normalization were still being used as if NaN could not enter light/acoustic signal payloads or fallback vectors. A NaN strength can survive comparisons, poison `MassiveThreatData.Strength`, and then travel into the compute shader through the GPU threat buffer.
Solution: Added `SaturateFinite01`, applied it to submarine-light intensity, acoustic ping intensity, movement volume, sonar intensity, acoustic panic strength, VAT hit intensity, and swarm-dispersed signal payloads. Hardened `RegisterPredatorFearBurst`, `TryPublishSwarmDispersedSignal`, `RegisterVatHitReaction`, and `HandleMassiveDisplacement` so non-finite position/radius/duration/strength data is rejected before GPU-facing state changes.
Rejected Alternatives: Trusting `math.saturate` was rejected because NaN comparison behavior is not a validation policy. Adding a new signal lane was rejected because the existing typed lanes are the correct contract; the consumer needed stricter validation, not interface churn. Logging every malformed producer event was rejected because this is hot ingress and blackbox anomaly paths already own binary telemetry.
Scalability potential: Low/Middle keep the same endpoint sphere and three ping slots with malformed payloads dropped cheaply. High/Ultra retain capsule SDF and richer panic visuals without allowing corrupt producer values to enter GPU threat state.
Hardware Impact: Finite scalar checks are estimated under 1 us/frame on i3/MX350 for the bounded signal windows. No profiler proof is claimed. The gain is stability and avoided GPU NaN recovery, not measured frame-time reduction.

Problem: CPU and compute shader L1 normalize helpers still used direct division after a guard. This is safer than blind division, but it violates the reciprocal discipline and left non-finite fallback vectors able to propagate.
Solution: Replaced direct L1 division with `rcp`/`math.rcp` multiplied by the vector after finite denominator checks. CPU fallback vectors are now finite-checked and normalized or collapsed to zero. Compute fallback vectors are finite-checked before return. `WriteBoidSensoryThreatSlot` now clears non-positive radii instead of promoting a bad negative radius into an active threat.
Rejected Alternatives: Leaving the helpers unchanged was rejected because they are shared by acoustic/light/fear paths. Switching all shader paths to Euclidean `rsqrt` was rejected because L1 normalization is the cheap Dear Lie used to keep MX350 cost down.
Scalability potential: Low/Middle preserve the cheap L1 fake. High/Ultra keep the same vector field behavior while removing a backend-sensitive direct division and bad fallback propagation class.
Hardware Impact: Reciprocal multiply is equivalent or cheaper on target shader backends and avoids division syntax in hot helpers. CPU impact is a few scalar finite checks in shared helpers, estimated under 1 us/frame; no profiler proof is claimed.

Problem: The post-finite-signal compile proof cannot complete because unrelated determinism code is currently broken.
Solution: Logged three probes: `Build_BOID_SENSORY_INPUT_PUMP_Polish15.txt`, `Build_BOID_SENSORY_INPUT_PUMP_Polish15_Strike2.txt`, and `Build_BOID_SENSORY_INPUT_PUMP_Polish15_Strike3.txt`. Strike1/2 fail on missing `ValidateBinaryLayout` in `LockstepStateValidator`; Strike3 fails on missing lockstep/system-glitch lane constants in the same file. Touched-surface log scans found no diagnostics in `SargassumMicroFaunaBoids.cs`, `SargassumMicroFaunaBoids.compute`, `BoidFishInstanced.shader`, or `H8Memory.cs`.
Rejected Alternatives: Editing `LockstepStateValidator` was rejected under the domain boundary rule because it belongs to core determinism, not the AI/COMPUTE boid sensory pump. Reverting the finite-signal hardening was rejected because no diagnostic implicates it.
Scalability potential: No runtime scaling impact; this is validation accounting.
Hardware Impact: 0 us/frame; build-only blocker.

Problem: The sensory buffer itself was sanitized, but the older shader headlight photophobia and high-tier curtain-parting helpers still trusted frame constants after the buffer path. A corrupted player position, forward vector, panic radius, or headlight panic scalar could still produce NaN acceleration even when threat slots were clean.
Solution: Added finite guards to `ResolveHeadlightPhotophobiaForce` and `ResolvePlayerCurtainPartingForce` before axial/radial cone math and high-tier split math. Shader L1 fallback vectors are now finite-checked and normalized through guarded `rcp` instead of returned raw.
Rejected Alternatives: Removing the headlight and curtain helpers was rejected because they buy the high-tier beam-parting visual behavior required by the prompt. CPU-only validation was rejected because frame constants can be stale or rebound after CPU sensory slot sanitation.
Scalability potential: Low/Middle still pay almost nothing because their sensory avoidance is endpoint-sphere and simple panic. High/Ultra keep the stronger headlight/curtain visual while being protected from malformed frame constants.
Hardware Impact: Adds finite checks only in two helper paths, estimated under 1 us/frame on i3/MX350 when active. No profiler proof is claimed. Failure cost avoided is mobile/Metal NaN propagation.

Problem: The post-headlight-shader pass still cannot get a fresh project compile because unrelated diagnostics code is currently broken.
Solution: Logged `Docs/AgentLogs/Build_BOID_SENSORY_INPUT_PUMP_Polish16.txt`; it fails in `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs` on missing `DebugSignal`. Static debt scans and touched-surface scans remain clean for boid sensory files.
Rejected Alternatives: Editing `ArchitectEyeVisualizer` was rejected as cross-domain diagnostics work. Claiming a green compile after shader edits was rejected because the current project build is blocked.
Scalability potential: No runtime scaling impact; validation accounting only.
Hardware Impact: 0 us/frame; build-only blocker.
