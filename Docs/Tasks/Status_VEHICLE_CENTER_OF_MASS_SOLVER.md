# Status_VEHICLE_CENTER_OF_MASS_SOLVER

Agent: HYDRO_MECHANIC
Prompt ID: VEHICLE_CENTER_OF_MASS_SOLVER
Domain: ECHELON 6 - HABITAT & VEHICLES
Status: PENDING VERIFICATION
Batch Source: Docs/Tasks/CURRENT_BATCH.md lines 516-559
Task Count: 19

## Mandates Read Before Coding
- PHYS_Fluid_Incursion_Interior.txt
- CORE_Submarine_Vehicles_Kinematics_AUP.txt
- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- PHYS_Determinism_Multithreaded_Body_Solving.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt

## State Machine Checklist
- [x] Task 0 - Prompt extraction and mandate isolation | DOD: CLI regex extracted only `<AGENT_PROMPT id="VEHICLE_CENTER_OF_MASS_SOLVER">`; eight task-relevant mandates read before coding. Alternative rejected: IDE/open-tab memory. Estimate: 120 us.
- [ ] Task 1 - SINGLETON ERADICATION N/A extending SubmarineAutoLevelPidJob | Pending code inspection. Estimate: TBD.
- [ ] Task 2 - SIGNAL MIGRATION consume SubmarineFloodStateSignal | Pending implementation. Estimate: TBD.
- [ ] Task 3 - ASMDEF ISOLATION Hecton8.Vehicles.Physics -> Contracts | Pending implementation. Estimate: TBD.
- [ ] Task 4 - DEAD CODE HUNT Rigidbody.centerOfMass in Update loop | Pending scan. Estimate: TBD.
- [ ] Task 5 - MASS SOA request RoomWaterLevels, RoomVolumes, RoomLocalAUPs | Pending implementation. Estimate: TBD.
- [ ] Task 6 - WATER MASS CALCULATION WaterLevel * Volume * 1025 | Pending implementation. Estimate: TBD.
- [ ] Task 7 - CENTER OF MASS SHIFT in SlowTick Burst job | Pending implementation. Estimate: TBD.
- [ ] Task 8 - INERTIA TENSOR FAKE via angular drag multiplier | Pending implementation. Estimate: TBD.
- [ ] Task 9 - PID BIAS feed COM offset to autopilot | Pending implementation. Estimate: TBD.
- [ ] Task 10 - SINKING THRESHOLD disables PID at >40 percent base mass | Pending implementation. Estimate: TBD.
- [ ] Task 11 - AUDIO STRESS acoustic stress signal | Pending implementation. Estimate: TBD.
- [ ] Task 12 - ZERO-GC proof | Pending static scan/profiler unavailable. Estimate: TBD.
- [ ] Task 13 - AUP SHIFT SAFETY local room AUP math | Pending implementation. Estimate: TBD.
- [ ] Task 14 - EXECUTION PHASE SIMULATION before PID | Pending implementation. Estimate: TBD.
- [ ] Task 15 - MATH LOD 1Hz ColdTick on Low Tier | Pending implementation. Estimate: TBD.
- [ ] Task 16 - HAPTICS low-frequency request on critical flood | Pending implementation. Estimate: TBD.
- [ ] Task 17 - BLACKBOX telemetry COM offset and water mass | Pending implementation. Estimate: TBD.
- [ ] Task 18 - EVENT BUS VehicleCommandSignal critical list at pitch >30 | Pending implementation. Estimate: TBD.
- [ ] Task 19 - OMEGA COMPILE CHECK Burst weighted average loop | Pending compilation. Estimate: TBD.

## Iteration Log
- Loop 0: Status/Rationale files were missing at start. No old batch data found for this ID. Created fresh state files.
