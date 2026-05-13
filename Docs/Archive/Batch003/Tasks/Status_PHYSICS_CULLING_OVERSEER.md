# Status_PHYSICS_CULLING_OVERSEER

STATUS: PENDING - COMPILE BLOCKED BY DEPENDENCY
Agent: LOCOMOTION_ENGINEER
Prompt ID: PHYSICS_CULLING_OVERSEER
Task Count: 19
Domain: META, POLISH & INTEGRATION (Quality Control)

## Mandates Loaded
- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- PHYS_Determinism_Multithreaded_Body_Solving.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- ARCH_Project_Bootstrap_Sequence_Init_Safety.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt

## State Machine
- [x] Task 1: SINGLETON ERADICATION | Used existing GameBootstrapper-owned GlobalPhysicsStateManager and GlobalRegistry IPhysicsCullingOverseer slot. | Alternative rejected: new PhysicsOptimization.Instance singleton. | Estimate: 35 us saved.
- [x] Task 2: SIGNAL MIGRATION | Player AUP/camera/depth read from PlayerRuntimeContextService first, GlobalRegistry.Player fallback only. | Alternative rejected: FindObjectOfType or transform-only player lookup. | Estimate: 15 us saved.
- [x] Task 3: ASMDEF ISOLATION | No new asmdef or cross-asmdef dependency added; culling contract remains in Hecton8.Physics surface consumed by GlobalRegistry. | Alternative rejected: BootstrapContracts edit. | Estimate: 0 us runtime.
- [x] Task 4: DEAD CODE HUNT | rg scan found no PhysicsOptimization.Instance; later purge removed PickupItem's local player-distance sleep/wake branch and left fauna/world lifecycle sleeps untouched. | Alternative rejected: deleting unrelated fluid/fauna lifecycle sleep logic. | Estimate: 15 us saved per active loose pickup fixed tick.
- [x] Task 5: NATIVE RIGIDBODY REGISTRY | Added NativeArray<float3> RigidbodyAUPs, byte state/result lanes, float distance diagnostics, and mapped Rigidbody[] refs. | Alternative rejected: managed List.Remove registry. | Estimate: 60 us saved.
- [x] Task 6: BURST DISTANCE CULLING | Added PhysicsDistanceCullingJob using math.distancesq at local 10 Hz cadence. | Alternative rejected: main-thread Vector3.Distance every FixedTick. | Estimate: 140 us saved.
- [x] Task 7: FRUSTUM BIAS | Behind-camera bodies use 50 percent sleep threshold scale. | Alternative rejected: uniform cull radius. | Estimate: 45 us saved.
- [x] Task 8: DEPTH-BASED VARIANCE | Depth >= 500 m applies 20 percent sleep-distance reduction. | Alternative rejected: full solver radius in abyss fog. | Estimate: 70 us saved.
- [x] Task 9: VELOCITY DAMPENING | Linear/angular velocities are multiplied by 0.9 before explicit sleep. | Alternative rejected: frozen visual state with unchanged velocity. | Estimate: 5 us cost, visual stability gained.
- [x] Task 10: EXPLICIT SLEEP DISPATCH | Main-thread dispatcher applies Sleep/WakeUp only after Burst job completion. | Alternative rejected: implicit PhysX sleep. | Estimate: 30 us saved.
- [x] Task 11: KINEMATIC CULL | Bodies beyond 100 m enter culling-owned isKinematic/detectCollisions false mode. | Alternative rejected: sleeping only. | Estimate: 180 us saved.
- [x] Task 12: COLLIDER STRIPPING | Heavy bodies with cached MeshCollider refs strip colliders beyond 150 m and restore prior enabled state. | Alternative rejected: leaving MeshColliders in far broadphase. | Estimate: 240 us saved.
- [x] Task 13: HYSTERESIS | Kinematic and MeshCollider restore threshold is 90 m; sleep wake threshold is below sleep threshold. | Alternative rejected: threshold edge flipping. | Estimate: 25 us saved.
- [x] Task 14: EXCLUSION BITMASK | Added PhysicsCullingFlags.IgnoreCulling, IPhysicsCullingFlagProvider, and connection culling locks; player/sub/vehicle bodies auto-excluded, active tether/dock bodies locked. | Alternative rejected: tags as primary contract. | Estimate: 10 us saved.
- [x] Task 15: EVENT BUS AWAKEN | Acoustic ping, acoustic impulse, and physics impact events wake only nearby culled bodies. | Alternative rejected: direct sonar/audio dependencies or wake-all. | Estimate: 80 us saved.
- [x] Task 16: ORIGIN SHIFT SAFETY | Pending culling jobs are completed/discarded before origin-shift mutation; native snapshots update without wakeups. | Alternative rejected: applying stale culling result after shift. | Estimate: crash prevention.
- [x] Task 17: ZERO-GC TRACKING | Registration/removal uses fixed arrays, Dictionary index map, and swap-with-last removal; no runtime List.Remove path added. | Alternative rejected: per-frame managed containers. | Estimate: 50 us saved.
- [x] Task 18: MATH LOD | Low/MX350 sleep distance is 40 m; higher tiers retain 50 m and visual budget. | Alternative rejected: single middle-ground threshold. | Estimate: 90 us saved on low tier.
- [!] Task 19: OMEGA COMPILE CHECK | BLOCKED BY DEPENDENCY: dotnet build Assembly-CSharp.csproj fails first in Hecton8.Bootstrap.Contracts.csproj because BootstrapStatus.cs cannot resolve ITickDispatcher and GlobalRegistry. | Alternative rejected: editing unrelated BootstrapContracts work. | Estimate: 0 us runtime.

## Iteration Log
- Loop 0: Prompt extracted, domain checked, status/rationale created, mandates loaded.
- Loop 1: Tasks 1-5 implemented in GlobalPhysicsStateManager and GlobalRegistry. Build attempt 1 timed out after 124 s with MSBuild nodes still active.
- Loop 2: Tasks 6-10 implemented: Burst job, 10 Hz cadence, frustum/depth bias, damped sleep, explicit dispatch. Build attempt 2 blocked by BootstrapContracts missing ITickDispatcher/GlobalRegistry.
- Loop 3: Tasks 11-14 implemented: kinematic cull, MeshCollider strip, hysteresis, IgnoreCulling flags. Build attempt 3 with BuildProjectReferences=false blocked by missing generated metadata dlls.
- Loop 4: Tasks 15-16 implemented: EventBus wakes, origin-shift discard/update path, black-box dump to Docs/AgentLogs/Dump_PHYSICS_CULLING_OVERSEER.bin on invalid input/NaN.
- Loop 5: Tasks 17-18 implemented and manually audited with rg/diff checks; task 19 remains dependency-blocked.
- Loop 6: OMEGA_POLISH parsed after tasks completed/blocked. Removed direct math.sqrt from acoustic impulse radius, confirmed bitmask/squared-distance hot path, and reran Hecton8.Core build into the same BootstrapContracts wall.
- Loop 7: Static-only hardening after user instruction to avoid dotnet build. Fixed velocity dampening order before kinematic cull, removed obsolete sleep helper methods, bounded telemetry ring writes, preserved late explicit culling flags on already tracked bodies, removed duplicate SlowTick scheduling, added DataVault fallback, bounded native clear length against vault-resized buffers, and cleared the GlobalRegistry culling slot during reset.
- Loop 8: Second static-only pass. Moved duplicate tracked-body registration onto a no-job-complete path for hydrodynamic/connection hot callers, removes stale same-EntityId body ghosts before re-adding, and rejects tracking when required native/black-box buffers are unavailable instead of crashing later.
- Loop 9: Third static-only pass. Origin-shift now includes culling-owned kinematic bodies instead of treating them as authored kinematic props, native culling lanes must meet fixed capacity before scheduling/tracking, and runtime reset clears the black-box telemetry cursor and entries.
- Loop 10: Dead-code purge follow-up. Removed PickupItem's local player-transform distance sleep/wake check, kept registration with GlobalPhysicsStateManager, skipped loose-current work while sleeping, and routed awake loose-current force/torque as ambient no-wake packets so overseer sleep is not churned awake by currents.
- Loop 11: Reporter reentrancy hardening. Moved PhysicsStateReporter attachment until after the rigidbody index and entity map are committed, preventing AddComponent OnEnable recursion from creating orphaned duplicate tracked-body slots.
- Loop 12: Native lane self-heal. EnsureNativeState now discards pending culling work and releases undersized native lanes before reallocating/reacquiring fixed-capacity buffers, including DataVault-owned RigidbodyAUPs aliases.
- Loop 13: Player depth sanitization. Physics culling player-state resolution now clamps invalid runtime or fallback depth to zero before abyss threshold logic, preventing NaN depth from disabling deterministic depth LOD.
- Loop 14: Registry slot mapping. GlobalRegistry now resolves IPhysicsCullingOverseer to the existing PhysicsStateManager service slot so diagnostics and rebound masks do not treat the overseer facade as Unknown.
- Loop 15: Idempotent culling command enforcement. Already-owned sleep, kinematic, and MeshCollider-strip states now reassert their Unity component state when external systems disturb them, and hot restore paths use the known body index instead of a linear self-lookup.
- Loop 16: Ambient packet sleep authority guard. PhysicsApplySystem now asks IPhysicsCullingOverseer before applying no-wake ambient force/torque packets, preventing environmental AddForce/AddTorque from implicitly waking far culled bodies between 10 Hz enforcement passes.
- Loop 17: Tether culling-lock enforcement. Active tether and dock connections now increment explicit culling locks, and newly attached tether/dock bodies restore from any existing overseer sleep/kinematic/mesh-strip state immediately.
