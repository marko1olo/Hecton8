# NAV_DEAD_RECKONING Status

Prompt: `NAV_DEAD_RECKONING`
Role: `UX_ENGINEER`
Chat name: `Gyro-Compass Drift`
Domain: `ECHELON 8 PRESENTATION & UX / Submarine Navigation Interface`
Status: `PENDING VERIFICATION`

## Mandates Identified Before Coding

- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `UI_Diegetic_Physical_Interfaces.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `CORE_Submarine_Vehicles_Kinematics_AUP.txt`

## State Machine

- [ ] Task 1. SINGLETON ERADICATION: Purge `CompassManager.Instance`. Register `IInertialNavigationService`.
- [ ] Task 2. SIGNAL MIGRATION: Consume `AupShiftSignal`, `ImpactSignal`, and `BrownoutSignal`.
- [ ] Task 3. ASMDEF ISOLATION: `Hecton8.UI.Navigation` depends ONLY on Contracts.
- [ ] Task 4. DEAD CODE HUNT: Eradicate any UI scripts reading `Camera.main.transform.rotation` directly.
- [ ] Task 5. DEAD RECKONING S.O.A.: Define `double3 EstimatedAUP` and `float GyroDriftError`.
- [ ] Task 6. BURST INTEGRATOR: On `FastTick`, integrate `EstimatedAUP += SubmarineVelocity * dt`.
- [ ] Task 7. BROWNOUT PENALTY: If `BrownoutSignal` is active, apply `GyroDriftError += dt * 0.5f`.
- [ ] Task 8. IMPACT PENALTY: Consume `ImpactSignal`. `GyroDriftError += severity * 2.0f`.
- [ ] Task 9. ERROR APPLICATION: Apply procedural rotation matrix from `GyroDriftError * sin(time)`.
- [ ] Task 10. COCKPIT SYNC: Expose `EstimatedAUP` and `GyroDriftError` to `VehicleSubOsCockpitRuntime`.
- [ ] Task 11. ZERO-GC TEXT: Compass bearing string through `ZeroGCFormatter.FastIntToChars` over `Span<char>`.
- [ ] Task 12. RECALIBRATION INTERACTION: Physical cockpit button hold for 3 seconds recalibrates.
- [ ] Task 13. HUD GLITCHING: If `GyroDriftError > 10.0f`, push UI chromatic aberration scalar to visor post.
- [ ] Task 14. AUP SHIFT SAFETY: `EstimatedAUP` subtracts `ShiftOffset` exactly.
- [ ] Task 15. MATH LOD: Same scalar math on Low; no reduction needed.
- [ ] Task 16. ZERO-GC: Integration job allocates 0 bytes.
- [ ] Task 17. BLACKBOX DUMP: Push `GyroDriftError` and `CalibrationCount` to Telemetry.
- [ ] Task 18. SAVE SYSTEM SYNC: Serialize `EstimatedAUP` and `GyroDriftError` into `SaveBinaryStorage`.
- [ ] Task 19. OMEGA COMPILE CHECK: Verify `double3` math does not cast to `float3` before integration.

## Loop Evidence

- Loop 0: Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`; status/rationale files were missing and initialized. Compile not attempted yet.
