# Status_VEHICLE_AUTONOMOUS_DOCKING

Status: PENDING VERIFICATION
Agent: HYDRO_MECHANIC
Prompt ID: VEHICLE_AUTONOMOUS_DOCKING
Domain: ECHELON 6 HABITAT & VEHICLES / Drone Fleet Commander
Task count: 19
Started: 2026-05-13

## Mandates Loaded

- CORE_Submarine_Vehicles_Kinematics_AUP
- CORE_Weather_Abyssal_FlowField_Currents
- MATH_Coordinate_Precision_AUP_FloatingOrigin
- OPT_Zero_GC_Policy_AllocFree_Mandate
- OPT_Native_Memory_Collections_JobSystem_Protocol
- PHYS_Physics_Integrity_Determinism_ForceMode
- ARCH_Global_Registry_ServiceLocator_DI_Init
- DBG_Telemetry_Crash_Reporting_PostMortem
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First

## Setup Evidence

- [x] Prompt extracted cover-to-cover from `Docs/Tasks/CURRENT_BATCH.md`. DOD: CLI extraction by XML id, counted 19 task lines. Rejected: MCP/basic read because batch prompts can truncate or leak neighbor prompts. Estimate: 140 us.
- [x] Domain verified against `Docs/Actual Domains of Project.txt`. DOD: matched Drone Fleet Commander under ECHELON 6. Rejected: broad vehicle ownership because docking edits must stay in drone fleet/autonomous docking surface. Estimate: 90 us.
- [x] Architecture docs checked. DOD: read drone protocol, signal corridor, AUP integration, flow-field math, and doc audit. Rejected: scene-object docking implementation because live drones are native headless slots. Estimate: 420 us.

## Core Tasks

- [ ] 1. Purge `DockingManager.Instance`.
- [ ] 2. Drones consume `DockingRequestSignal` and emit `DockingCompleteSignal`.
- [ ] 3. ASMDEF isolation: `Hecton8.Vehicles.Automation` -> Contracts.
- [ ] 4. Eradicate `Vector3.Slerp` or `MoveTowards` from drone movement scripts.
- [ ] 5. Define `BaseAirlock` entry points as AUP plus Forward vector.
- [ ] 6. Burst cubic Bezier control points P0/P1/P2/P3.
- [ ] 7. Burst Bernstein spline target and tangent evaluation.
- [ ] 8. Kinematic override while docking.
- [ ] 9. Cross-current visual yaw-slip only; trajectory remains spline-authoritative.
- [ ] 10. Cubic speed deceleration without managed math/pow overhead.
- [ ] 11. Hatch animation sync after `t > 0.8` via event command.
- [ ] 12. Clamp at `t >= 1`, exact AUP snap, rigidbody kinematic policy, matrix-only visual attachment.
- [ ] 13. Obstacle abort via raycast corridor; fail signal; AI loiter fallback.
- [ ] 14. AUP shift safety for all spline control points.
- [ ] 15. Math LOD: Low tier ignores cross-current visual tilt.
- [ ] 16. Zero-GC spline math using native state only.
- [ ] 17. Multi-drone batch evaluation in one `IJobParallelFor`.
- [ ] 18. Telemetry: write `DockingAborts` to blackbox.
- [ ] 19. Compile check: Burst Bezier math has no Unity `Vector3`.

## Loop Log

### Loop 0 - Discovery

- Result: Existing live drone docking is in `Assets/_Project/Scripts/Construction/DroneCognitionJob.cs` and currently uses linear `math.lerp` to `HomePosition`.
- Result: No first-party `DockingManager` class or instance was found during scan.
- Result: No drone movement script currently uses `Vector3.Slerp` or `Vector3.MoveTowards`; verification will be repeated after edits.
- Pending: Code patch and compile verification.
