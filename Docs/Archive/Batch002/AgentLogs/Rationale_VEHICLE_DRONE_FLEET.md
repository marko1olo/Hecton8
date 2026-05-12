# Rationale_VEHICLE_DRONE_FLEET

Status: PENDING VERIFICATION  
Agent: VEHICLE_DRONE_FLEET  
Domain: ECHELON 6 HABITAT & VEHICLES / Drone Fleet Commander  

## Decision 0 - Initial Architecture Gate

Problem: Existing batch objective states drones are GameObjects with NavMeshAgents, which is incompatible with 50-drone CPU budget and violates headless swarm target.

Solution: Use SoA NativeArray ownership for drone state, Burst IJobParallelFor for movement/repulsion, NativeQueue command drains for repair/mining handoff, and GPU buffer rendering for presentation. DOD practice: isolate hot-path data from Unity objects and expose only decoupled queues/registry interfaces.

Rejected Alternatives: Standard Unity NavMeshAgent per drone was rejected for per-agent component overhead, Transform writes, path replans, and poor Burst compatibility. Physics collider avoidance was rejected because anti-clipping can be a deterministic visual fake using squared-distance repulsion.

Scalability potential: Low tier hides drones past 50m and preserves headless logic. Middle tier renders nearby drones with reduced update cadence. High tier extends render range to 150m. Ultra can spend saved CPU on denser drone VFX, underbelly ore glow, and laser visual overkill.

Hardware Impact: i3/MX350 expected gain is removal of per-drone NavMeshAgent/Transform/Collider work. Exact microseconds remain PENDING VERIFICATION until Unity profiler and GCMonitor data exist.

## Decision 1 - Tasks 1-5 Headless State And Motion

Problem: Existing drone fleet was partly headless but still capped at 8 slots and exposed only an AoS `NativeArray<HeadlessDroneState>`, which did not satisfy the 50-drone batch requirement or the explicit SoA state stream.

Solution: Raised `HeadlessDroneCapacity` to 64, added persistent `NativeArray<float3> s_DronePositionsSoA`, `NativeArray<byte> s_DroneStateBytes`, and mirrored them from `DroneCognitionJob.WriteOutputs`. The byte mapping is fixed to batch contract: 0 Idle, 1 Mining, 2 Repairing, 3 Returning. DOD practice: hot-path data is native, preallocated, and written by one `IJobParallelFor`.

Rejected Alternatives: Kept no GameObject-per-drone or NavMeshAgent path. Rejected per-drone managed state adapters because they would reintroduce Transform/component churn. Existing AoS was kept for compatibility, but the SoA streams now provide the required flat lanes without ripping through unrelated hub code.

Scalability potential: Low uses the same logic but can hide far visuals. Mid keeps the 64-slot pool with cheaper cull distance. High and Ultra can spend the saved CPU on VFX and denser indirect draw presentation.

Hardware Impact: i3/MX350 gain is capacity headroom without 64 managed agents. Microsecond estimate: 50-250 us saved per active 50-drone scene versus component/NavMesh motion, pending profiler proof.

## Decision 2 - Tasks 4-5 Kinematics And Anti-Collision

Problem: Drone movement needed deterministic kinematic swarm motion with fake anti-collision and no physics colliders.

Solution: Preserved the existing Burst motion loop using `math.rsqrt` through `SafeNormalize`, squared-distance arrival checks, spatial-hash neighbor lookup, and repulsion force based on `math.lengthsq`. DOD practice: visual fake first, no collider simulation, no allocations, no per-drone physics bodies.

Rejected Alternatives: Rejected Rigidbody avoidance, Unity Physics queries, and NavMesh obstacle carving because they scale badly and are harder to keep deterministic. Also rejected exact all-pairs collision because the spatial hash already provides bounded neighbor checks.

Scalability potential: Low/MX350 keeps the same cheap squared-distance separation. High/Ultra can add visual-only wake/laser detail without making avoidance more physical.

Hardware Impact: i3/MX350 expected gain is avoiding 50 collider/agent updates. Microsecond estimate: 80-400 us saved versus collider avoidance in a 50-drone scene, pending profiler proof.

## Decision 3 - Compile Gate 1

Problem: Full `dotnet build Hecton8.Core.csproj` fails before the drone domain is reached.

Solution: Treated this as external dependency failure and ran Unity MCP `validate_script` on touched drone scripts. Both `DroneCognitionJob.cs` and `DroneFleetManager.cs` reported 0 diagnostics. DOD practice: isolate local proof when global compile wall is unrelated.

Rejected Alternatives: Rejected editing `HectonArenaAllocator`, `HectonSurvivalSystem`, `TetherInstance`, or `AbyssalThermalManager`; those are outside the assigned drone fleet domain and match the three-strike dependency wall rule.

Scalability potential: No runtime scalability change; this preserves domain isolation while the integrator repairs core compile blockers.

Hardware Impact: No runtime impact. Verification status remains PENDING VERIFICATION because project-wide compile is blocked by non-drone code.

## Decision 4 - Tasks 6-9 Repair Command, Sparks, Return, Dock

Problem: Existing repair work was applied by the late-frame slot scan directly, so the Burst job did not emit a decoupled service command. Weld visuals also depended only on voxel weld application, not the global debris signal lane requested by the batch.

Solution: Added prewarmed persistent `NativeQueue<DroneServiceCommand>` and a `ParallelWriter` field on `DroneCognitionJob`. Repair/attack drones enqueue service work while stationary. The manager drains the queue after job completion, validates slot/drone id, and applies repair/cut/hijack through the existing managed owner. `DispatchRepairWeld` now publishes `DebrisSpawnSignal` with spark kind at an AUP. DOD practice: Burst emits value commands; managed systems mutate modules only on the owner thread.

Rejected Alternatives: Rejected calling `BaseModule.Repair` from Burst, direct Habitat Builder coupling, and per-drone ParticleSystem ownership. Rejected LineRenderer or particle components per drone; the spark event is a centralized signal.

Scalability potential: Low tier can ignore or throttle debris signals downstream while repair logic still runs. High/Ultra can render denser spark debris from the same event without touching drone logic.

Hardware Impact: i3/MX350 expected gain is preventing per-drone managed service polling from becoming the command source. Microsecond estimate: 20-120 us saved at 50 drones, pending profiler proof.

## Decision 5 - Task 10 Mining Laser Dependency

Problem: The batch requires mining drones, ore-node endpoints, a mining laser shader, and ore carriage. The current construction domain has `AutonomousExtractorSystem` and `ResourceNode`, but the drone fleet task model only exposes `RepairModule` and `CutParasite`. There is no safe `DroneFleetTaskKind.Mining`, ore target AUP, carrying flag, or storage return contract in the drone interface.

Solution: Mark mining laser runtime hookup as blocked by dependency instead of inventing cross-domain resource APIs. DOD practice: do not create direct dependencies on code that does not exist; use registry or event contracts when available.

Rejected Alternatives: Rejected hijacking `AutonomousExtractorSystem` internals, adding direct `ResourceNode` fields to repair tasks, or drawing decorative lasers without dispatch semantics. That would be false completion and architectural bleed.

Scalability potential: Once a mining dispatch contract exists, the visual should be a shader beam spanning two points, not `LineRenderer`, with Low hiding far beams and Ultra adding ore glow/submesh toggle.

Hardware Impact: No runtime gain yet because task is blocked. Avoided a likely managed dependency loop and fake report.

## Decision 6 - Tasks 11-13 Ore Dependency, Math LOD, Zero-GC

Problem: Ore transport depends on the same missing mining drone contract as the laser path. Math LOD and zero-GC behavior were partially present but not aligned with the batch render-distance requirement.

Solution: Marked ore transport blocked by dependency. Added compute-culling render distance uniforms: Low/MX350/Unknown render real drones to 50m, Mid to 100m, High/Ultra to 150m. Logic still runs headless because culling is render-only. Preserved single `IJobParallelFor` evaluation and prewarmed native queues/arrays. DOD practice: Math LOD affects visual cost, not simulation correctness.

Rejected Alternatives: Rejected stopping drone logic past 50m, because the batch requires headless logic to continue. Rejected managed distance filtering before draw because the compute path already owns compaction and indirect instance count.

Scalability potential: Low hides distant drones and spends no visual cycles. Mid extends readability. High/Ultra use 150m visibility and can later add ore glow/laser overkill once mining contracts exist.

Hardware Impact: i3/MX350 expected gain is GPU instance culling before indirect draw expansion. Microsecond estimate: 30-150 us GPU/CPU presentation cost avoided in far-drone scenes, pending renderdoc/profiler proof.

## Decision 7 - Tasks 14-15 Recon And Omega Gate

Problem: Batch required vehicle script recon and compute-frustum compile verification. Source recon can be completed, but full compile remains blocked by non-drone dependency errors.

Solution: Wrote `Docs/AgentLogs/RECON_VEHICLE_DRONE_FLEET.md` with every `LookAt`/`Slerp` offender from `Assets/_Project/Scripts`. Verified drone files use velocity forward assignment / `quaternion.LookRotationSafe` and docking nlerp. Verified source path for compute culling: `TryRenderGpuCulledFleet` uploads frustum planes, camera position, render-distance square, dispatches `CS_CullDrones`, and copies append count into indirect args.

Rejected Alternatives: Rejected editing fauna, editor, scene runtime, MantaScooter, power, tether, survival, or arena allocator code. Those are outside this domain and compile wall dependencies are not owned by the drone fleet prompt.

Scalability potential: Recon prevents managed rotation interpolation from leaking into vehicle hot paths. Compute culling is the necessary gate for Low-to-Ultra visual scaling.

Hardware Impact: Source-level gain is avoiding accidental per-Transform interpolation in this domain. Exact microseconds remain PENDING VERIFICATION until the external compile wall is cleared and Unity profiling can run.

## OMEGA POLISH CHANGES

Problem: Polish audit found hot-path divisions and one managed `.normalized` in the drone service path. The black-box mandate also required the drone fleet to retain a fixed 300-frame high-level state ring.

Solution: Replaced hot float divisions with `math.rcp` multiplications in drone scoring, neighbor separation, player repulsion, docking blend, flow sampling, task criticality, and battery averaging. Replaced remaining parasite-cut `.normalized` with `math.rsqrt`. Added `NativeArray<DroneFleetBlackBoxEntry>[300]` with frame, active count, state hash, flags, first position, and bounds. On NaN/non-finite detection, the fleet dumps the ring to `Docs/AgentLogs/Dump_VEHICLE_DRONE_FLEET.bin`.

Rejected Alternatives: Rejected exact normalization/sqrt in visual/service paths, rejected managed per-frame telemetry lists, and rejected writing the dump every frame. The dump is failure-only.

Scalability potential: Low tier gets dominant cheap math and culls distant drones; High/Ultra keep 150m visual range and can spend cycles on VFX once mining contracts exist.

Hardware Impact: Additional polish estimate is 10-60 us saved in dense swarm/service frames on i3/MX350, pending profiler proof. Black-box normal-path cost is a fixed 64-slot scan and one 300-entry ring write, bounded and allocation-free.

Cinematic Cheats Used: squared-distance arrival/culling, spatial-hash repulsion instead of physics, render-only LOD distance cutoff, `rsqrt` normalization, compute append-buffer frustum culling, event-driven spark debris instead of per-drone ParticleSystem.

Final Git Diff: `git diff --stat` for touched drone files reports `DroneCulling.compute`, `DroneCognitionJob.cs`, and `DroneFleetManager.cs` modified. Full working-tree diff also contains pre-existing same-file changes outside this batch; they were preserved, not reverted. Local verification: `validate_script` returned 0 diagnostics for `DroneCognitionJob.cs`; `DroneFleetManager.cs` validation repeatedly disconnected after prior success, while `dotnet build Hecton8.Core.csproj` now reports only external `HectonSurvivalSystem.cs(298,29): SurvivalPhysiologyScalarResult` missing.

## Decision 8 - Honest R&D Continuation / Phantom Swarm Tier Gate

Problem: Further mining work would still be false completion. `ResourceNode` can consume mining interaction signals and `AutonomousExtractorSystem` can harvest fixed nodes, but the drone fleet task model still has no mining task kind, ore AUP target contract, ore carry bit, or storage return handoff. Separately, the existing phantom swarm visual cheat used a fixed 500 indirect instances on every tier, which spends GPU compute/draw budget on Low/MX350 even when the real headless fleet is already render-capped.

Solution: Keep mining laser and ore transport blocked until a real dispatch/storage contract exists. Added a drone-domain, render-only phantom swarm tier gate: Unknown/Low/MX350 draw 0 phantom drones, Mid draws 192, High draws 384, Ultra draws 500. The compute buffer remains sized for Ultra, but dispatch count and indirect args instance count use the tier-specific value. Indirect args upload now happens only when the draw count changes.

Rejected Alternatives: Rejected decorative mining lasers with no resource task semantics, rejected direct `ResourceNode` mutation from drone fleet code, and rejected always drawing 500 fake drones because that burns toaster-tier budget without gameplay value.

Scalability potential: Low/MX350 spends no phantom-drone GPU budget and preserves real headless logic. Mid gets enough visible density to imply automation. High raises density. Ultra keeps full visual overkill with 500 GPU-authored phantom drones.

Hardware Impact: i3/MX350 expected gain is avoiding the 500-thread phantom compute dispatch and indirect draw when the tier cannot afford decorative density. Estimated saving: 30-250 us GPU/driver presentation cost in drone-heavy submarine scenes, pending profiler proof.
