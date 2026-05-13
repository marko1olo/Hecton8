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
- [ ] Task 6: BURST EVALUATOR | Justification: pending job implementation | Alternatives Rejected: pending | Estimate: pending us
- [ ] Task 7: TIER 0 ACTIVE | Justification: pending threshold implementation | Alternatives Rejected: pending | Estimate: pending us
- [ ] Task 8: TIER 1 PERIPHERAL | Justification: pending cadence gate | Alternatives Rejected: pending | Estimate: pending us
- [ ] Task 9: TIER 2 FROZEN | Justification: pending freeze gate | Alternatives Rejected: pending | Estimate: pending us

## Phase 3: Consequence Wiring

- [ ] Task 10: BOID CONTROLLER CULL | Justification: pending controller integration | Alternatives Rejected: pending | Estimate: pending us
- [ ] Task 11: PREDATOR BRAIN CULL | Justification: pending brain integration | Alternatives Rejected: pending | Estimate: pending us
- [ ] Task 12: ANIMATION LOD | Justification: pending VAT/property path scan | Alternatives Rejected: pending | Estimate: pending us
- [ ] Task 13: AUP WRAPPING | Justification: pending AUP/SDF interface scan | Alternatives Rejected: pending | Estimate: pending us

## Phase 4: Safety & LOD

- [ ] Task 14: COMBAT OVERRIDE | Justification: pending combat signal scan | Alternatives Rejected: pending | Estimate: pending us
- [ ] Task 15: AUP SHIFT SAFETY | Justification: pending AUP shift signal scan | Alternatives Rejected: pending | Estimate: pending us
- [ ] Task 16: ZERO-GC | Justification: pending static audit | Alternatives Rejected: pending | Estimate: pending us
- [ ] Task 17: MATH LOD | Justification: pending quality tier source scan | Alternatives Rejected: pending | Estimate: pending us
- [ ] Task 18: TELEMETRY | Justification: pending blackbox API scan | Alternatives Rejected: pending | Estimate: pending us
- [ ] Task 19: OMEGA COMPILE CHECK | Justification: pending compile/test | Alternatives Rejected: pending | Estimate: pending us

## Iteration Log

- Loop 0: Prompt extracted by CLI. Mandates identified. Code scan pending.
- Loop 1: Tasks 1-5 implemented and prompt re-extracted by CLI. Compile verification pending.
