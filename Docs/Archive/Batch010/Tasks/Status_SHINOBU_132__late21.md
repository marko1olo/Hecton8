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
- [ ] Verify current code against all 20 tasks.
- [ ] Patch only SHINOBU_132-owned cable/tether files and unavoidable first-party cable call sites.
- [ ] Static scan for forbidden Unity joints, LineRenderer cable paths, managed hot-path allocation, and deterministic Burst flags.
- [ ] Guarded compile only if CPU and dotnet/csc policy allow it.

## 20-Task Matrix

- [ ] Task 01 CONFIGURABLE_JOINT_PURGE - pending live source scan.
- [ ] Task 02 LINE_RENDERER_CABLE_PURGE - pending live source scan.
- [ ] Task 03 CS1612_ENCAPSULATION_PURGE - pending DTO/job verification.
- [ ] Task 04 ARM64_PADDING_RECONSTRUCTION - pending layout verification.
- [ ] Task 05 EMERGENCY_MOCK_TETHER_DATA - pending mock job verification.
- [ ] Task 06 BURST_VERLET_INTEGRATION_KERNEL - pending job verification.
- [ ] Task 07 CONSTRAINT_SOLVER_RELAXATION - pending job verification.
- [ ] Task 08 SPLINE_VISUAL_DEAR_LIE - pending render-spline verification.
- [ ] Task 09 ASYNC_GPU_SPLINE_UPLOAD - pending upload path verification.
- [ ] Task 10 CONTINUOUS_QUALITY_ITERATIONS - pending quality curve verification.
- [ ] Task 11 REACTION_FORCE_ROUTING - pending SignalBus/force packet verification.
- [ ] Task 12 ABYSSAL_CURRENT_ADVECTION - pending flow input verification.
- [ ] Task 13 AUP_LOCAL_DELTA_MATH - pending double3-to-local float scan.
- [ ] Task 14 ROLLBACK_NETCODE_STATE_FENCE - pending deterministic state scan.
- [ ] Task 15 UNINITIALIZED_VAULT_BOOTSTRAP - pending allocation scan.
- [ ] Task 16 TELEMETRY_TETHER_RECORDER - pending 300-frame dump verification.
- [ ] Task 17 CABLE_PHYSICS_TUNER_WINDOW - pending editor facade verification.
- [ ] Task 18 ZERO_GC_CSV_CABLE_MATERIALS - pending parser verification.
- [ ] Task 19 LIVE_VERLET_DEBUG_GIZMO - pending gizmo verification.
- [ ] Task 20 SELF_AUDIT_AND_GATES - pending after implementation/verification.
