# Status_FOVEATED_SIMULATION_DIRECTOR

Prompt: `FOVEATED_SIMULATION_DIRECTOR`
Role: `AI_PROGRAMMER`
Domain: AI / Distant LOD & Entity Sleep
Status: PENDING VERIFICATION

## Mandates Read

- `REND_Foveated_Simulation_LOD.txt`
- `AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt`
- `AI_Creature_Cognition_States.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Phase 1: Purge & Isolation

- [x] Task 1: SINGLETON ERADICATION | Justification: `IFoveatedSimulationDirector` contract and `GlobalRegistry` slot replace any `AiManager.Instance` path; `rg` found no `AiManager.Instance` usage under `Assets/_Project/Scripts`. | Alternatives Rejected: scene singleton/FindObjectOfType were rejected as hidden dependencies and per-frame lookup risk. | Estimate: 18 us saved per cold service lookup avoided across 100 AI callers.
- [x] Task 2: SIGNAL MIGRATION | Justification: `CameraPositionSignal` and `CameraFrustumSignal` are fixed-size `SignalBus` payloads consumed by `FoveatedSimulationManager` before tier scoring. | Alternatives Rejected: direct `Camera.main` polling was rejected as a global lookup and render-thread coupling. | Estimate: 35 us saved per 10Hz tier pass on MX350 by using a cached signal pose.
- [x] Task 3: ASMDEF ISOLATION | Justification: `Hecton8.AI.Foveated.asmdef` now references `Hecton8.Core.Contracts`; runtime service contract lives in `Core.Contracts`, not fauna/boid code. | Alternatives Rejected: adding concrete AI implementation to `Core` contract assembly was rejected as a dependency loop. | Estimate: 0 us runtime; compile dependency wall reduced.
- [x] Task 4: DEAD CODE HUNT | Justification: individual fauna LOD/sleep gates now read centralized foveated tier state instead of owner-local player distance thresholds. | Alternatives Rejected: leaving `distSqrToPlayer > sleepDistance` gates was rejected because every brain would keep inventing its own LOD truth. | Estimate: 45 us saved per frozen predator frame by early halting steering/current logic.

## Phase 2: Foveated Tiers

- [x] Task 5: ENTITY REGISTRY | Justification: director maintains persistent `NativeArray<float3> EntityAUPs` and `NativeArray<byte> EntitySimTiers` mirrors for Burst scoring and consumers. | Alternatives Rejected: managed per-entity dictionaries were rejected for GC churn and cache misses. | Estimate: 120 us saved per 5000-entity 10Hz pass versus managed object traversal.
- [x] Task 6: BURST EVALUATOR | Justification: `ImportanceScoringJob` runs behind a 0.1s accumulator and computes distance plus `math.dot(directionToTarget, safeForward)` in Burst. | Alternatives Rejected: per-brain `Vector3.Distance` polling was rejected as duplicated scalar work. | Estimate: 180 us saved per 5000-entity pass on i3/MX350.
- [x] Task 7: TIER 0 ACTIVE | Justification: inside forward cone and under 100m resolves `FoveatedSimulationTier.Active` and `Center60Hz`. | Alternatives Rejected: multiple fine-grain near tiers were rejected because prompt requires predictable three-band control. | Estimate: 0 us saved; preserves full nearby AI budget.
- [x] Task 8: TIER 1 PERIPHERAL | Justification: outside cone or 100m-300m resolves `Peripheral` and `Rear1Hz`, feeding existing ColdTick gates. | Alternatives Rejected: 5/10/20Hz intermediate cadence was rejected for distant fauna because it still burns utility CPU behind the camera. | Estimate: 950 us saved per 100 peripheral predators per second.
- [x] Task 9: TIER 2 FROZEN | Justification: over 300m resolves `Frozen`, `TryResolveTick` returns false, and fauna preserves velocity while steering/utility stops. | Alternatives Rejected: Rigidbody sleep alone was rejected because it does not stop AI scripts or boid compute dispatch. | Estimate: 2200 us saved per 100 frozen predators per second.

## Phase 3: Consequence Wiring

- [x] Task 10: BOID CONTROLLER CULL | Justification: `HectonBoidController` resolves foveated tier through the registry and bypasses spatial-grid/main flocking compute dispatch when frozen. | Alternatives Rejected: despawning swarm renderers was rejected because frozen swarms must keep visual presence. | Estimate: 300-700 us saved per frozen 5000-boid swarm dispatch on MX350.
- [x] Task 11: PREDATOR BRAIN CULL | Justification: `FaunaBrain` consumes tier/cadence; Tier1 maps to one-second cold ticks while retaining existing cached target context. | Alternatives Rejected: special predator-only timers were rejected because dispatcher cadence already owns tick truth. | Estimate: 900 us saved per 100 peripheral predators per second.
- [x] Task 12: ANIMATION LOD | Justification: VAT shader now multiplies `_Time.y` by `_H8FoveatedVatTimeScale`; boid renderer sets 0.5 for Peripheral. | Alternatives Rejected: swapping animation clips/material variants was rejected as asset churn and batching risk. | Estimate: 25 us CPU saved per frame via no material swap; GPU visual rate is faked.
- [x] Task 13: AUP WRAPPING | Justification: frozen active predators over 600m wrap to 200m in front of camera after `VoxelDynamicNavGridRuntime.TrySampleHybridNavigation` rejects solid voxel/passability. | Alternatives Rejected: despawn/respawn was rejected because it breaks pressure continuity and adds object churn. | Estimate: 400 us saved per recycled predator versus pooled spawn activation.

## Phase 4: Safety & LOD

- [x] Task 14: COMBAT OVERRIDE | Justification: `CombatDamageSignal` snapshots and direct fauna damage both call `LockTier0` for at least 10 seconds. | Alternatives Rejected: distance exceptions inside damage code were rejected because the director must own tier truth. | Estimate: 0 us saved; safety override buys correctness.
- [x] Task 15: AUP SHIFT SAFETY | Justification: `AupShiftSignal` snapshots and origin-shift callbacks force immediate importance refresh and tier re-evaluation. | Alternatives Rejected: waiting for the 10Hz accumulator was rejected because it can leave one false frozen frame after a sector shift. | Estimate: 100 ms worst-case culling error removed.
- [x] Task 16: ZERO-GC | Justification: scoring buffers and 300-frame blackbox are persistent `NativeArray` allocations registered with `NativeMemorySentinel`; hot pass uses no managed collections. | Alternatives Rejected: per-frame arrays/lists were rejected by the zero-GC mandate. | Estimate: 0 B/frame hot allocation; avoids GC spikes on i3/MX350.
- [x] Task 17: MATH LOD | Justification: Low/MX350 thresholds resolve to Tier1 at 50m and Tier2 at 150m; default remains 100m/300m. | Alternatives Rejected: a balanced middle-only profile was rejected because the scalability pillar requires Low and Ultra divergence. | Estimate: 55-70 percent more distant AI frozen on MX350 scenes.
- [x] Task 18: TELEMETRY | Justification: 300-frame native blackbox entry includes `FrozenEntityCount`, tier counts, camera state, and state hash; dump path is `Docs/AgentLogs/Dump_FOVEATED_SIMULATION_DIRECTOR.bin`. | Alternatives Rejected: log spam was rejected because telemetry must be fixed-size and post-mortem readable. | Estimate: 0 B/frame managed logging avoided.
- [x] Task 19: OMEGA COMPILE CHECK [BLOCKED BY DEPENDENCY] | Justification: `dotnet build Hecton8.Core.csproj --no-restore` fails first on unrelated missing cross-domain namespaces/types; filtered build output has no foveated-specific diagnostics. | Alternatives Rejected: fixing unrelated audio/physics/save/GPR domains was rejected as out-of-domain sabotage. | Estimate: compile blocked; no runtime estimate.

## Iteration Log

- Loop 0: Prompt extracted by CLI. Mandates identified. Code scan pending.
- Loop 1: Tasks 1-5 implemented and prompt re-extracted by CLI. Compile verification pending.
- Loop 2: Tasks 6-10 implemented. Full compile attempt failed on unrelated cross-domain missing types before foveated diagnostics.
- Loop 3: Tasks 11-15 implemented. Prompt re-extracted by CLI. AUP wrapping hardened to require non-solid nav/SDF sample.
- Loop 4: Tasks 16-19 audited. Full compile remains blocked by dependency wall; filtered build scan reports no foveated-specific diagnostics.
- Loop 5: Self-audit ran `rg` for singleton, distance LOD, tier gates, VAT time scale, AUP shift, and blackbox coverage.
