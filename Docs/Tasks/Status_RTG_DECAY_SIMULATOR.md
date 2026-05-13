# RTG_DECAY_SIMULATOR Status

Agent: THERMAL_ENGINEER
Domain: Radioisotope Thermals / Power-Thermal Systems
Task Count: 19
Status: PENDING VERIFICATION

## Mandates Read
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- DATA_Save_Persistence_Binary_Delta_Checksum.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- CORE_Tools_Equipment_Interaction_Raycast_Heat.txt

## Loop 1 - Tasks 1-5
- [ ] 1. SINGLETON ERADICATION: Purge `PowerGeneratorManager.Instance` | Justification: pending code scan completion; DOD is no new singleton access and no direct manager dependency. Rejected alternative: classic singleton. Estimate: 6 us cold scan impact.
- [ ] 2. SIGNAL MIGRATION: N/A, SOA state read by Logistics Grid | Justification: pending RTG SOA output lane. Rejected alternative: single-use event for wattage polling. Estimate: 2 us per logistics read.
- [ ] 3. ASMDEF ISOLATION: `Hecton8.Power.Generators` -> Contracts | Justification: pending isolated assembly. Rejected alternative: dumping RTG into Core assembly. Estimate: 0 us runtime.
- [ ] 4. DEAD CODE HUNT: Eradicate `Update()` methods inside `RTG_Item.cs` | Justification: scan found no `RTG_Item.cs`; DOD becomes no Update in new RTG code. Rejected alternative: inventing legacy dependency. Estimate: 0 us.
- [ ] 5. S.O.A. RTG DATA | Justification: pending NativeArray start/half-life/output buffers. Rejected alternative: per-component float-only truth. Estimate: 3-12 us per 64 RTGs at cold cadence.

## Loop 2 - Tasks 6-10
- [ ] 6. BURST DECAY JOB | Justification: pending IJobParallelFor. Rejected alternative: per-RTG MonoBehaviour Tick. Estimate: 8-20 us per 64 RTGs at 1 Hz.
- [ ] 7. PADE APPROXIMATION | Justification: pending guarded denominator. Rejected alternative: `math.exp` in hot job. Estimate: 2-5 us saved per 64 RTGs.
- [ ] 8. HEAT INJECTION | Justification: pending typed radiation source + thermodynamics proxy. Rejected alternative: physical thermal diffusion per RTG. Estimate: 4 us cold path.
- [ ] 9. LOGISTICS COUPLING | Justification: pending `IPowerComponent.PowerRating` backed by SOA output. Rejected alternative: direct `FluidPipeGraphRuntime` concrete reference. Estimate: 1 us query.
- [ ] 10. UI READOUT | Justification: pending normalized output property. Rejected alternative: per-frame string HUD update. Estimate: 0 B GC.

## Loop 3 - Tasks 11-12, 18
- [ ] 11. DEPLETION THRESHOLD | Justification: pending dead flag latch below 5%. Rejected alternative: removing radiation when power dies. Estimate: 1 us.
- [ ] 12. REPROCESSING | Justification: pending crafting-facing depleted isotope flag/hash. Rejected alternative: direct Fabricator concrete edit before contract scan. Estimate: 0 us until queried.
- [ ] 18. SAVE SYSTEM SYNC | Justification: pending start time payload fields. Rejected alternative: runtime-only decay reset on load. Estimate: save-only.

## Loop 4 - Tasks 13-17, 19
- [ ] 13. AUP SHIFT SAFETY | Justification: time absolute; only source positions use runtime/AUP conversion for radiation. Rejected alternative: sector time math. Estimate: 0 us.
- [ ] 14. MATH LOD | Justification: pending Low/MX350 10-second gate. Rejected alternative: uniform 1 Hz on toaster. Estimate: 90% low-tier job dispatch reduction.
- [ ] 15. ZERO-GC | Justification: pending static scan and compile. Rejected alternative: Lists/strings in tick. Estimate: 0 B target.
- [ ] 16. BLACKBOX DUMP | Justification: pending 300-entry NativeArray telemetry ring. Rejected alternative: Debug.Log-only postmortem. Estimate: 64 B * 300 native.
- [ ] 17. EVENT BUS | Justification: pending `HUDNotificationSignal` below 20% latch. Rejected alternative: UI singleton call. Estimate: O(1) queue push.
- [ ] 19. OMEGA COMPILE CHECK | Justification: pending division guard verification. Rejected alternative: blind denominator use. Estimate: 0 us after guard.

## Compile Checkpoints
- [ ] Checkpoint A after tasks 1-5: PENDING
- [ ] Checkpoint B after tasks 6-10: PENDING
- [ ] Checkpoint C after tasks 11-19: PENDING
- [ ] Omega polish: PENDING

