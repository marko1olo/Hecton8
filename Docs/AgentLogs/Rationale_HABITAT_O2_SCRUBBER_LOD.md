# Rationale HABITAT_O2_SCRUBBER_LOD

STATUS: PENDING VERIFICATION

## Initial Decision Record
Problem: Gas and power diffusion for unoccupied bases risks wasting CPU on i3/MX350 when bases are far from the player.
Solution: Implement hibernation as SOA state in native buffers, gate Burst jobs by awake byte mask, and apply analytical catch-up on wake.
Rejected Alternatives: Per-compartment simulation while far away rejected because it burns frame budget for invisible state. Coroutine wake polling rejected because gameplay hot-path scheduling must use dispatcher/tick cadence. Managed dictionaries rejected because global state belongs in native SOA storage.
Scalability potential: Low = 150m hibernation threshold and scalar catch-up. Middle = 500m threshold and standard leak decay. High = longer awake residency and richer telemetry. Ultra = use saved CPU for visual overkill outside this gas solver, not for particle gas truth.
Hardware Impact: Estimated low-end gain is proportional to sleeping base count; each hibernating base avoids recurring gas/power job work except FrostTick distance checks. Exact microseconds remain PENDING VERIFICATION without Unity profiler logs.

## Mandate Bindings
- CORE_Abyss_Survival_Systems_O2_Pressure_Logic: Dalton-law compartments must remain deterministic and gameplay correct.
- LOGI_Energy_Networks_Power_Grid_Graph_Flow: battery drain catch-up must clamp, never underflow, and preserve graph truth.
- MATH_Coordinate_Precision_AUP_FloatingOrigin: base distance checks must use AUP data, not transform positions.
- ARCH_Global_Registry_ServiceLocator_DI_Init: cross-domain dependencies must be cached outside hot paths.
- OPT_Native_Memory_Collections_JobSystem_Protocol: native buffers must have explicit ownership and disposal.
- OPT_Zero_GC_Policy_AllocFree_Mandate: no managed allocation in tick/job/catch-up paths.
- DBG_Telemetry_Crash_Reporting_PostMortem: hibernation transitions need black-box state hooks if the existing domain supports them.
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First: analytical catch-up is the required deterministic lie, replacing thousands of invisible diffusion iterations.
