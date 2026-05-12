# PHYSICS_TETHERS State Machine

Prompt ID: PHYSICS_TETHERS
Role: ROPE_MECHANIC
Domain: PHYSICS / TETHER & CABLE PHYSICS
Task count: 15
Status: VERIFIED MASTER GRADE - PHYSICS_TETHERS SCOPE; GLOBAL COMPILE BLOCKED BY DEPENDENCY

## Mandates Read
- PHYS_Tether_Cable_Acceleration_Constraints.txt
- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- MATH_Rsqrt_i3_SIMD.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- CORE_Weather_Abyssal_FlowField_Currents.txt

## Loop 0 - Setup
- [x] Extracted PHYSICS_TETHERS prompt from CURRENT_BATCH.md. DOD: strict XML block extraction by ID. Rejected: IDE tab inference. Estimate: 70 us.
- [x] Read domain file. DOD: assigned boundary limited to physics/tether and cross-domain signal interfaces. Rejected: direct world/UI edits. Estimate: 35 us.
- [x] Recon found existing TetherManager/TetherInstance path. DOD: extend first-party owner instead of duplicate runtime. Rejected: parallel manager without gameplay integration. Estimate: 55 us.

## Tasks
- [x] 1. VERLET NODE S.O.A. DOD: `_verletPositions` and `_verletPreviousPositions` are persistent NativeArray<float3> buffers allocated only through tether buffer sizing. Rejected: managed Vector3 arrays. Estimate: 6 us allocation amortized; 0 us per frame allocation.
- [x] 2. BURST INTEGRATION JOB. DOD: `TetherVerletIntegrationJob` applies `position + (position - previous) + acceleration * dt^2`. Rejected: Rigidbody joint drive. Estimate: 8-18 us for 16 nodes on low-tier CPU.
- [x] 3. JACOBI DISTANCE CONSTRAINTS. DOD: `TetherVerletJacobiConstraintJob` accumulates corrections and weights before applying them per node. Rejected: in-place Gauss-Seidel-only hidden order. Estimate: 15-50 us by tier/node count.
- [x] 4. RSQRT OPTIMIZATION. DOD: solver distance uses `math.rsqrt` and `lengthSq * invLength`; no Vector3.magnitude in constraint job. Rejected: length/division path. Estimate: 2-6 us saved over scalar sqrt/divide loop.
- [x] 5. ITERATION CAPPING MATH LOD. DOD: Low/Mx350/Unknown exactly 2 iterations, Mid 3, High/Ultra 5. Rejected: fixed quality middle ground. Estimate: Low 30 us target, High 70 us target for active 16-node cable.
- [x] 6. AUP ORIGIN SHIFT SYNC. DOD: `TetherVerletOriginShiftJob` subtracts shift from positions, previous positions, and pinned endpoints through HectonFloatingOrigin listener. Rejected: only moving Transforms or consuming GlobalSignals queue. Estimate: 10-25 us per 16-node active cable on shift only.
- [x] 7. COLLISION CHEAT. DOD: active solver clamps nodes to `VerletFloorY + radius`; mesh raycast bend/integrity path is not called by active solve. Rejected: per-segment mesh collision. Estimate: 1-3 us in integration/constraint pass.
- [x] 8. TWO-WAY RIGIDBODY COUPLING. DOD: endpoint tension routes payload acceleration and anchor reaction through PhysicsForceRouter. Rejected: direct Rigidbody.AddForce. Estimate: 4-12 us fixed-step routing overhead.
- [x] 9. SNAP PREVENTION. DOD: peak tension over material threshold accumulates stress, breaks tether, invokes owner snap protocol, and publishes `TetherSnappedSignal`. Rejected: silent detach. Estimate: <5 us on snap frame only.
- [x] 10. BRG / LINE RENDERER PROXY. DOD: solved Verlet positions upload to GraphicsBuffer; Tether shader expands procedural tube impostor triangles; no LineRenderer in tether manager. Rejected: LineRenderer component or CPU mesh build. Estimate: CPU upload 5-20 us by node count.
- [x] 11. TENSION AUDIO FEEDBACK. DOD: tension above 68% snap threshold publishes throttled `ImpactSignal` creak payload. Rejected: AudioSource spawn/string event. Estimate: <5 us on emitting frame, 0 us when below margin.
- [x] 12. WIND/CURRENT SWAY. DOD: Verlet integration receives flow acceleration from HectonMapMagicVegetationBridge abyssal flow, FluidEngine fallback, then Weather fallback. Rejected: moving only payload. Estimate: one flow sample plus vector clamp per fixed step.
- [x] 13. ZERO-GC. DOD: persistent NativeArrays/GraphicsBuffer/NativeQueue; resize only through attach/capacity setup; hot path audit found no new managed allocations in active solve. Rejected: per-frame allocation/LINQ/CPU mesh. Estimate: 0 B managed GC per active fixed/late tick.
- [x] 14. RECONNAISSANCE PROTOCOL. DOD: ConfigurableJoint/CharacterJoint/HingeJoint/SpringJoint scan logged to `RECON_PHYSICS_TETHERS.md`; no active first-party tether offender found. Rejected: undocumented assumption. Estimate: cold scan only, 6 ms shell time.
- [BLOCKED BY DEPENDENCY] 15. OMEGA COMPILE CHECK. DOD: PHYSICS_TETHERS scripts validate 0 diagnostics; constraint jobs contain no managed types. Full Unity compile is blocked by unrelated external scripts. Rejected: editing non-physics domains. Estimate: compile wall external.

## Current Loop
Loop 1 complete: tasks 1-5 implemented. PHYSICS_TETHERS scripts validate with 0 diagnostics. Global Unity compile is BLOCKED BY UNRELATED DEPENDENCIES:
- Audio PlayerCriticalProceduralAudioRenderer duplicate ResolveGranularMaxVoiceCount.
- Construction VehicleDockingModule missing IOriginShiftListener.OnOriginShift implementation.
- UI DiegeticVisorHudMesh DamageSignal ambiguous reference.
- SaveBinaryStorage Burst catch-filter unsupported.

Loop 2 complete: tasks 6-10 implemented. PHYSICS_TETHERS scripts validate with 0 diagnostics. Global Unity compile remains BLOCKED BY UNRELATED DEPENDENCIES now reported in Gameplay/Combat, RecipeData, HectonBoidController, and CraftingSystem; no PHYSICS_TETHERS errors or warnings remain.

Loop 3 complete: tasks 11-13 implemented. PHYSICS_TETHERS scripts validate with 0 diagnostics. Global Unity compile remains BLOCKED BY UNRELATED DEPENDENCIES:
- World AbyssalThermalManager missing IFixedTickable.FixedTick(float).
- SaveBinaryStorage Burst catch-filter unsupported.

Loop 4 complete: task 14 done, task 15 blocked by unrelated dependencies after repeated compile attempts. Constraint solver managed-type audit result: `TetherVerletJobs.cs` has Burst structs + NativeArray fields only; no UnityEngine/Rigidbody/Vector3/List/Dictionary/string/object fields in solver jobs.

Core status is now 100% checked or blocked. Polish mandate may be parsed next.

Loop 5 complete: Omega polish parsed and executed after all tasks were checked or blocked. DOD: Burst pinned checks now use bitmask tests; shader camera vector uses rsqrt instead of normalize; active tether rest length and endpoint acceleration use `math.rcp`; cold GameObject naming string interpolation removed. Rejected: broad edits outside tether ownership. Estimate: 4-9 us saved across low-tier active tether/render path depending on tether count.

## Verification
- Hot-path scan: no `normalize(`, `math.sqrt`, `math.length(`, `Vector3.magnitude`, raw `PinnedMask[index] != 0`, tether-owned `$"` interpolation, or known replaced scalar divisions remain in PHYSICS_TETHERS touched hot files.
- Unity MCP validation: `TetherVerletJobs.cs` basic validation passed with 0 errors/0 warnings; `TetherManager.cs` standard validation passed with 0 errors/0 warnings; `TetherSignals.cs` standard validation passed with 0 errors/0 warnings. `TetherInstance.cs` MCP validator timed out after prior clean validation, so compilation evidence below is authoritative.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal -clp:ErrorsOnly`: BLOCKED by external `Assets\_Project\Scripts\HectonSurvivalSystem.cs(298,29)` missing `SurvivalPhysiologyScalarResult`; no PHYSICS_TETHERS errors remain after generated `Hecton8.Core.csproj` local include fix for new physics files.
- Unity console: BLOCKED by unrelated Visor, Combat, SaveBinaryStorage, Construction, and World errors; no PHYSICS_TETHERS file appears in the latest retrieved compiler errors.

## Honest AAA R&D Loop
- [x] R&D-1. BLACK BOX FAULT EXPORT. DOD: solver now tracks per-node non-finite state with `_verletNodeFaultFlags`, aggregates `_verletSolverFlags`, writes flags into the 300-frame telemetry ring, recovers bad nodes to finite fallback, and exports `Docs/AgentLogs/Dump_PHYSICS_TETHERS.bin` once per activation in editor/development builds. Rejected: Debug.Log-only crash narrative. Estimate: 0 us normal path beyond linear byte clears; fault export is rare path.
- [x] R&D-2. TENSION READABILITY CHEAT. DOD: procedural tether material now blends `_TetherColor` to `_TetherStressColor` from `VisualStress01`; no new physics, no particles, no per-node CPU coloring. Rejected: simulated fraying/thermal particles. Estimate: <1 us CPU, one lerp in vertex shader.
- [x] R&D-3. POST-R&D VERIFICATION. DOD: `TetherVerletJobs.cs`, `TetherManager.cs`, and `TetherSignals.cs` validate clean; hot-path scan still finds no forbidden sqrt/normalize/string-interpolation candidates in touched tether files. `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal -clp:ErrorsOnly` remains blocked outside domain at `VoxelDeltaProcessor.cs(1688,92)` missing `SaveVoxelDeltaRun8`; no PHYSICS_TETHERS errors reported. Rejected: editing voxel/save domain from tether agent. Estimate: compile wall external.
- [x] R&D-4. WATER-DAMPED VERLET STABILITY. DOD: integration job now applies tiered velocity damping before acceleration integration; Low/MX350 = 0.965, Mid = 0.975, High/Ultra = 0.985. Rejected: deeper iteration count or fake substeps. Estimate: one vector multiply per node; expected to save 10-60 us indirectly by hiding low-iteration jitter without raising Jacobi passes.
- [x] R&D-5. STRESS PULSE VISUAL CHEAT. DOD: shader uses a triangle-wave pulse from `_Time` to widen/brighten stressed cable in the existing procedural vertex path. Rejected: particle fray, heat simulation, CPU damage mesh. Estimate: 0 us CPU; one `frac/abs/lerp` cluster per generated vertex.
- [x] R&D-6. POST-STABILITY VERIFICATION. DOD: hot scan clean; `TetherVerletJobs.cs` and `TetherSignals.cs` validate clean; `TetherManager.cs` basic validation passed after transient MCP disconnect. Full `Assembly-CSharp` build timed out; narrowed `Hecton8.Core.csproj` build is blocked by 78 external missing-symbol errors (`HectonPersistentPathPolicy`, `HardwareTierDetector`, `HectonNativeBridge`, etc.) and reports no PHYSICS_TETHERS errors. Rejected: editing core/save/audio/input domains. Estimate: compile wall external.
- [x] R&D-7. LOCALIZED SEGMENT STRESS VISUALIZATION. DOD: existing `_verletSegmentTensions` now uploads to a persistent `VisualSegmentTensionBuffer`; shader samples `_TetherSegmentTensions[segmentIndex]` and blends only the strained segment toward stress color/pulse. Rejected: whole-cable-only tint and per-node CPU color mesh. Estimate: 4-12 us upload for 8-24 floats; 0 B GC.
- [x] R&D-8. POST-SEGMENT-STRESS VERIFICATION. DOD: hot scan remains clean; generated `Hecton8.Core.csproj` build filtered for tether errors returned external missing-symbol errors only and no `Tether*` compiler errors. Unity MCP validation was unstable this pass with disconnects/timeouts, so build output is the authoritative evidence. Rejected: calling this full project green. Estimate: compile wall external.
