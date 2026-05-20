# Status_SHINOBU_132

Agent: SHINOBU_132
Domain: Echelon 4 Player/Kinematics/Tools - Tether & Cable Physics
Role: TETHER_AND_CABLE_PHYSICS_SOLVER
Task count: 20
Current state: ACTIVE_VERIFICATION_RECONSTRUCTION

## Mandates Read

- AGENTS.md: authority, state tracking, batch extraction, reporting, anti-amnesia, domain boundary.
- PHYS_Tether_Cable_Acceleration_Constraints: no Unity joints for cable truth; force packets and visual fakes.
- DATA_Runtime_Struct_Layout_ARM64: explicit layout, no Pack=1, 8/16-byte alignment.
- MATH_AUP_Determinism_Sync: double3 AUP truth, local float deltas after subtraction, deterministic tick.
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First: spline/VAT/dear-lie presentation before CPU over-solve.
- OPT_Zero_GC_Policy_AllocFree_Mandate: zero allocation hot paths, no LINQ/foreach/string churn.
- OPT_Native_Memory_Collections_JobSystem_Protocol: no mid-frame Complete, Vault route proof, NoAlias, handle chaining.
- TOOL_Designer_Facades_CSV_Binary_Bridge: editor facade and CSV bridge requirements.
- CORE_Weather_Abyssal_FlowField_Currents: current sampling is read-only and presentation-first.
- DBG_Telemetry_Crash_Reporting_PostMortem: 300-frame blackbox and binary dump proof.

## Loop 0 - Recovery

- [x] Extracted SHINOBU_132 prompt from `Docs/Tasks/CURRENT_BATCH.md` using CLI search. The tag includes `role` and `chat_name` attributes; exact bare-tag regex is invalid for this batch file.
- [x] Confirmed task count: 20.
- [x] Confirmed current active status/rationale files were missing and recreated them before code changes.
- [x] Live code surface found: `VerletCableDTOs.cs`, `CablePhysicsSolver132.cs`, `CablePhysicsDebugGizmo132.cs`, `Shinobu132CablePhysicsTunerWindow.cs`, `TetherManager.cs`.
- [ ] Patch only SHINOBU_132-owned cable/tether files and the confirmed LineRenderer vine path.
- [ ] Static scan for forbidden Unity joints, LineRenderer cable paths, managed hot-path allocation, and deterministic Burst flags.
- [ ] Guarded compile only if CPU and dotnet/csc policy allow it.

## 20-Task Matrix

- [ ] Task 01 CONFIGURABLE_JOINT_PURGE - live scan found no `ConfigurableJoint`, `SpringJoint`, or `CharacterJoint` in `Assets/_Project/Scripts`; needs final log proof.
- [ ] Task 02 LINE_RENDERER_CABLE_PURGE - `CaveBioRootsGenerator` still uses `LineRenderer` for bioluminescent root/vine visuals; patch required.
- [ ] Task 03 CS1612_ENCAPSULATION_PURGE - `CableNodeDTO` fields are public; pointer jobs need explicit `[NoAlias]` on node pointer fields.
- [ ] Task 04 ARM64_PADDING_RECONSTRUCTION - `CableNodeDTO` explicit 64-byte layout exists; final offset proof pending.
- [ ] Task 05 EMERGENCY_MOCK_TETHER_DATA - `GenerateMockTethersJob` exists; final proof pending.
- [ ] Task 06 BURST_VERLET_INTEGRATION_KERNEL - `SimulateCablePointsJob` exists with deterministic Burst; pointer alias patch pending.
- [ ] Task 07 CONSTRAINT_SOLVER_RELAXATION - `SolveCableConstraintsJob` exists; safety justification patch pending.
- [ ] Task 08 SPLINE_VISUAL_DEAR_LIE - `GenerateSplineVerticesJob` exists; final proof pending.
- [ ] Task 09 ASYNC_GPU_SPLINE_UPLOAD - ticketed `LockBufferForWrite` upload exists; final proof pending.
- [ ] Task 10 CONTINUOUS_QUALITY_ITERATIONS - `ResolveIterationCount` uses continuous lerp; final proof pending.
- [ ] Task 11 REACTION_FORCE_ROUTING - SignalBus route exists; final proof pending.
- [ ] Task 12 ABYSSAL_CURRENT_ADVECTION - sampled external/current acceleration exists; final proof pending.
- [ ] Task 13 AUP_LOCAL_DELTA_MATH - double3 delta before float3 in constraint/spline exists; final proof pending.
- [ ] Task 14 ROLLBACK_NETCODE_STATE_FENCE - deterministic Burst and aligned DTO exist; final proof pending.
- [ ] Task 15 UNINITIALIZED_VAULT_BOOTSTRAP - uninitialized Vault requests exist; final proof pending.
- [ ] Task 16 TELEMETRY_TETHER_RECORDER - 300-entry ring exists; dump must also write task alias `Dump_CABLE_SURGEON.bin`.
- [ ] Task 17 CABLE_PHYSICS_TUNER_WINDOW - UI Toolkit tuner exists; final proof pending.
- [ ] Task 18 ZERO_GC_CSV_CABLE_MATERIALS - byte-span parser exists; final proof pending.
- [ ] Task 19 LIVE_VERLET_DEBUG_GIZMO - gizmo exists; final proof pending.
- [ ] Task 20 SELF_AUDIT_AND_GATES - pending after implementation/verification.
