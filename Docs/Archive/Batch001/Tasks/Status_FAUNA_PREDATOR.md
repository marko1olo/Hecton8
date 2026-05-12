# Status_FAUNA_PREDATOR

Agent: FAUNA_PREDATOR
Role: PREDATOR_ARCHITECT
Domain: ECHELON 3 - FLORA, FAUNA & BIOTA
Source prompt: Docs/Tasks/CURRENT_BATCH.md
Status: VERIFIED MASTER GRADE (FAUNA DOMAIN; Hecton8.Core compiles with external warnings)

Mandates loaded:
- AI_Creature_Cognition_States.txt
- AI_Director_Encounter_Manager.txt
- AI_DYNAMIC_NAVGRID_SDF_INTEGRATION.txt
- AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt
- MATH_Rsqrt_i3_SIMD.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt

## State Machine

- [x] Task 1 - APEX FLANKING S-CURVE | Justification: DOD used apex-only `CinematicMath.FastNlerp` steering from current forward to lateral S-curve target; final vector finite-guarded. | Alternatives Rejected: NavMesh, spline solver, and all-predator smooth steering. | Estimate: +2.0 us per apex SlowTick, PENDING VERIFICATION.
- [x] Task 2 - SWARM DOMINANT SNAP | Justification: DOD preserved Tier 1/2 `ResolveDominantAxis` path and only branched apex predators into smooth steering. | Alternatives Rejected: global rsqrt steering upgrade because swarm enemies must stay cheap/snappy. | Estimate: 0 us regression for non-apex predators, PENDING VERIFICATION.
- [x] Task 3 - SQUARED DOT-PRODUCT VISION CONE | Justification: DOD verified predator/domain/director/sensor cone checks use unnormalized target vectors with squared dot comparisons; no acos/sqrt path added. | Alternatives Rejected: normalized vectors, `Vector3.Angle`, and acos cone checks. | Estimate: -0.4 us per cone check versus normalized angle path, PENDING VERIFICATION.
- [x] Task 4 - POLYNOMIAL UTILITY AI | Justification: DOD verified no `Pow01`, `math.pow`, or `Mathf.Pow` in fauna utility files; final predator action scores are now `score * score`. | Alternatives Rejected: AnimationCurve/SO curves and pow calls in Burst hot path. | Estimate: -0.8 us per predator utility eval versus pow-style scoring, PENDING VERIFICATION.
- [x] Task 5 - CONSTANT LEAD INTERCEPT | Justification: DOD verified player and pack intercept use fixed `0.65f` lead (`PlayerPosition + PlayerVelocity * PredatorInterceptLeadSeconds`). | Alternatives Rejected: ETA solve, quadratic intercept, and sqrt-based projectile prediction. | Estimate: -1.2 us per target solve versus ETA approximation, PENDING VERIFICATION.
- [x] Task 6 - ACOUSTIC SIGHT THROUGH WALLS | Justification: DOD uses noise threshold plus explicit `math.distancesq(input.PlayerPosition, input.Position) < 2500f` and promotes acoustic ping to player seen without occlusion. | Alternatives Rejected: physics occlusion and normalized distance checks. | Estimate: -0.3 us per acoustic check versus sqrt distance, PENDING VERIFICATION.
- [x] Task 7 - VORTEX STEERING DOMINANT AXIS | Justification: DOD uses voxel/SDF wall probe, dominant horizontal normal, and `cross(normal, up)` escape vector. | Alternatives Rejected: synchronous `Physics.Raycast` in AI steering hot path. | Estimate: -4.0 us per blocked steering event versus main-thread raycast, PENDING VERIFICATION.
- [x] Task 8 - RAYCAST BUDGETING | Justification: Director LOS uses `NativeArray<RaycastCommand>[1]`, `PredatorSightMaxRaysPerFrame = 1`, and `PredatorSightIntervalSeconds = 0.5f` toward player AUP/probe after squared-cone prefilter. | Alternatives Rejected: unbounded per-predator raycasts and per-frame line checks. | Estimate: hard cap 1 scheduled ray per 0.5s globally, stricter than prompt budget, PENDING VERIFICATION.
- [x] Task 9 - PACK HUNTING SYNC | Justification: DOD verified predator species target sharing through `NativeParallelHashMap<int, float3>` with parallel writer in swarm analysis and read-only lookup in evaluation. | Alternatives Rejected: managed dictionaries and direct predator-to-predator references. | Estimate: -2.0 us per pack update versus managed registry, PENDING VERIFICATION.
- [x] Task 10 - SDF-GRADIENT AMBUSH PULL | Justification: DOD verified ambushers call `TryResolveThreatVoxelGradient` and push toward crevice target via dominant-axis SDF gradient sample. | Alternatives Rejected: neighbor voxel scan, NavMesh cover query, and procedural cover search. | Estimate: -6.0 us per ambush solve versus 6-neighbor gradient scan, PENDING VERIFICATION.
- [x] Task 11 - KINETIC ENTANGLEMENT IMPACT | Justification: DOD publishes `ImpactSignal` from predator bite impulse after `predatorVelocity * predatorMass`. | Alternatives Rejected: direct audio/VFX calls and managed event fanout. | Estimate: +1 queue publish only on successful bite, PENDING VERIFICATION.
- [x] Task 12 - ANIMATION-DRIVEN SPEED SURGE | Justification: DOD verified forward velocity multiplier is modulated by deterministic `TrianglePulse01` in `ResolveTailSurgeSpeedMultiplier`. | Alternatives Rejected: random curves, Animator parameter polling, and sin/cos oscillators. | Estimate: +0.4 us per movement tick, deterministic and allocation-free, PENDING VERIFICATION.
- [x] Task 13 - CAMOUFLAGE SHADER LERP | Justification: DOD verified cached shader IDs drive depth/ambient camouflage tint/params/strength on runtime material state. | Alternatives Rejected: `MaterialPropertyBlock` because project AGENTS forbids MPB on standard geometry; per-frame material churn. | Estimate: 0 hot AI us; cold material setup only, PENDING VERIFICATION.
- [x] Task 14 - PRECOMPUTED RECIPROCALS | Justification: DOD uses constants such as `PredatorAcousticSightInvRangeSqr`, `FlockCountInvSoftCap`, `QuantizedByteInvScale`, `MemoryLifetimeInvSeconds`, and `ApexSCurveInvMaxDistanceSqr`. | Alternatives Rejected: repeated runtime divisions in steering and scoring weights. | Estimate: -0.2 us per weighted steering/utility block, PENDING VERIFICATION.
- [x] Task 15 - MATHGUARD CHECK | Justification: DOD verifies final `DesiredDirection`, rsqrt normalization, and new `SanitizeSteeringVector` use `MathGuard.IsFinite` with dominant-axis fallback. | Alternatives Rejected: unchecked vector propagation and NaN-tolerant downstream physics. | Estimate: +0.1 us for guard, prevents invalid steering crash chain, PENDING VERIFICATION.
- [x] Task 16 - WANDER LCG HASH | Justification: DOD replaced patrol refresh frac constants with deterministic LCG-style integer hash seeded by `WanderSequence` and center coordinates. | Alternatives Rejected: `math.sin`, `math.cos`, Perlin object/state, and managed random. | Estimate: -0.2 us per wander refresh, PENDING VERIFICATION.
- [x] Task 17 - SLOWTICK STAGGER | Justification: DOD verified `_nextEvaluationTimes` stagger uses `(slot & 31) * PredatorUtilityEvaluationStaggerStepSeconds` before due flags. | Alternatives Rejected: synchronized all-predator utility ticks. | Estimate: removes worst-case predator utility spike; per-slot cost unchanged, PENDING VERIFICATION.
- [x] Task 18 - NO DEBUG STRINGS | Justification: DOD searched fauna AI for `string.Format` and fixed-string interpolation hits; no hot AI debug string lane found. | Alternatives Rejected: managed debug formatting in AI updates. | Estimate: 0 GC from debug strings in checked fauna paths, PENDING VERIFICATION.
- [x] Task 19 - S.O.A. STATE MACHINE | Justification: DOD verified predator state is stored in `_chosenStates` as `NativeArray<byte>` and state transitions use `math.select` in Burst evaluation. | Alternatives Rejected: managed enum lists and object state machines. | Estimate: -1.0 us per 256-slot pass versus managed state fanout, PENDING VERIFICATION.
- [x] Task 20 - OMEGA COMPILE CHECK | Justification: DOD reviewed `CombatDamageRuntime.cs` and verified predator bite uses `CombatDamageSignal`, `CombatDamageSignalDetail`, `PackSignalMeta`, and `TryQueueDamage`; domain compile passes. | Alternatives Rejected: legacy direct health damage path as primary API. | Estimate: API bridge 0 allocations; compile status PENDING VERIFICATION because full dependency graph is blocked outside fauna.

## Iteration Log

- Loop 0: Prompt extracted from CURRENT_BATCH.txt because CURRENT_BATCH.md is absent. Initial mandate and owner discovery complete.
- Loop 1: Tasks 1-5 patched/reviewed. `dotnet build .\Assembly-CSharp.csproj --no-restore` succeeded with 12 unrelated package warnings and 0 errors.
- Loop 2: Tasks 6-10 patched/reviewed. Full dependency build failed outside fauna (`HectonCelestialEngine` missing celestial helper methods; `HectonFluidEngine` missing `ImpactSignal`). `dotnet build .\Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 /p:UseSharedCompilation=false /nr:false` succeeded with 0 warnings and 0 errors.
- Loop 3: Tasks 11-15 reviewed/patched. `dotnet build .\Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 /p:UseSharedCompilation=false /nr:false` succeeded with 0 warnings and 0 errors.
- Loop 4: Tasks 16-20 patched/reviewed. `dotnet build .\Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 /p:UseSharedCompilation=false /nr:false` succeeded with 0 warnings and 0 errors.
- Loop 5: First polish lookup missed attributed tag; self-review ran `git diff --check` on touched fauna/status/rationale files and domain compile succeeded with 0 warnings and 0 errors.
- Loop 6: Parsed `<POLISH_MANDATE id="OMEGA_POLISH">`. Added hardware-tier gate for apex S-curve via `GlobalRegistry.ScalabilityTier` in managed bridge and `HighTierSmoothSteering` native input flag. `Assembly-CSharp.csproj --no-dependencies` passed with 0 warnings and 0 errors. `Hecton8.Core.csproj` initially failed outside fauna in `VoxelDeltaProcessor.cs` with 10 errors, 0 warnings.
- Loop 7: Continuation build re-run after dependency churn. `Hecton8.Core.csproj --no-restore -m:1 /p:UseSharedCompilation=false /nr:false` now succeeds with 0 errors and 28 external warnings from URP/GPUInstancer/Crest packages. `Assembly-CSharp.csproj --no-dependencies` still succeeds with 0 warnings and 0 errors.
