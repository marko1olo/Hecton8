# Status_THERMODYNAMICS_LEAD

Status: PENDING VERIFICATION
Agent: THERMODYNAMICS_LEAD
Role: PHYSICS_PROGRAMMER
Domain: Abyssal Thermodynamics & Ice / Thermodynamics (Heat Diffusion)
Task Count: 19
Prompt Source: Docs/Tasks/CURRENT_BATCH.md

## Mandates Read

- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt
- CORE_Weather_Abyssal_FlowField_Currents.txt

## Checklist

- [ ] Task 1: SINGLETON ERADICATION - Purge ThermalManager.Instance. Bind IThermodynamicsService.
- [ ] Task 2: SIGNAL MIGRATION - Emit TemperatureChangedSignal(AUP, Temp).
- [ ] Task 3: ASMDEF ISOLATION - Hecton8.Thermodynamics depends on Contracts.
- [ ] Task 4: DEAD CODE HUNT - Eradicate OnTriggerStay used for heat/cold damage.
- [ ] Task 5: THERMAL S.O.A. - Create 32x32x32 NativeArray<float> mapped to the world.
- [ ] Task 6: DIFFUSION JOB - FrostTick Jacobi heat diffusion; voxel SDF density > 0 insulates.
- [ ] Task 7: GEYSER INJECTION - Read active thermal vents from PersistentWorldRegistry and inject +200C.
- [ ] Task 8: BRINE POOL FREEZING - If depth < -1000m, ambient defaults to -2C.
- [ ] Task 9: ICE OVERLAY - Pass local grid temperature to HectonVisorUberPost.shader.
- [ ] Task 10: SUBMARINE SLOWDOWN - If sub AUP temp < -5C, multiply top speed by 0.7f.
- [ ] Task 11: HULL CONTRACTION - Rapid 100C to -5C shift emits CombatDamageSignal(ThermalShock).
- [ ] Task 12: O2 FREEZING - GasDynamicsSolver cuts O2 scrubber efficiency by 50% below 0C.
- [ ] Task 13: AUP SHIFT SAFETY - Shift logical origin of grid when AupShiftSignal fires.
- [ ] Task 14: ZERO-GC - Jacobi diffusion allocates 0 bytes.
- [ ] Task 15: MATH LOD - Low Tier bypasses 3D diffusion; DistanceSq nearest heat source fallback.
- [ ] Task 16: SAVE DELTA - Compress non-ambient cells via RLE and pass to SaveBinaryStorage.
- [ ] Task 17: AUDIO CUES - Thermal shock emits AcousticPingSignal(MetalCreak).
- [ ] Task 18: TELEMETRY - Write PlayerAmbientTemp to Blackbox.
- [ ] Task 19: OMEGA COMPILE CHECK - Verify Burst compilation of diffusion job.

## Loop Log

- Loop 0: Prompt extracted with CLI from Docs/Tasks/CURRENT_BATCH.md. Status/rationale files were absent; hygiene clean. Source scan in progress.
