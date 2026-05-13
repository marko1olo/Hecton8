# Status_HEADLESS_SIMULATION_RUNNER

Agent: HEADLESS_SIMULATION_RUNNER
Role: QA_ENGINEER
Domain: CI/CD Pipeline / Headless QA
Prompt Source: Docs/Tasks/CURRENT_BATCH.md
Status: PENDING VERIFICATION

## Mandate Intake

- PENDING: ARCH_Project_Bootstrap_Sequence_Init_Safety.txt
- PENDING: ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- PENDING: DBG_Telemetry_Crash_Reporting_PostMortem.txt
- PENDING: OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- PENDING: OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- PENDING: MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- PENDING: MATH_Deterministic_RNG_SlotMachine.txt
- PENDING: QA_Evidence_Text_Filter_Audit.txt

## Checklist

- [ ] Task 1: SINGLETON ERADICATION N/A | DOD: confirm no singleton work required | Alternative rejected: inventing a singleton purge outside prompt | Estimate: PENDING us
- [ ] Task 2: SIGNAL MIGRATION | DOD: consume CrashTelemetrySignal and ProgressionEventSignal through existing decoupled interfaces | Alternative rejected: direct concrete dependency | Estimate: PENDING us
- [ ] Task 3: ASMDEF ISOLATION | DOD: Hecton8.QA.Headless references contracts only where possible | Alternative rejected: dumping runner into Assembly-CSharp | Estimate: PENDING us
- [ ] Task 4: BOOT ARGUMENTS | DOD: parse -batchmode and -h8headless without per-frame string work | Alternative rejected: editor-only toggles | Estimate: PENDING us
- [ ] Task 5: THE GREAT SILENCE | DOD: headless path avoids VFX, Audio, UI, Camera init | Alternative rejected: initialize then disable | Estimate: PENDING us
- [ ] Task 6: THE TIME CRANK | DOD: force 100x simulation scalar and unlocked frame rate in headless mode | Alternative rejected: changing project time settings globally | Estimate: PENDING us
- [ ] Task 7: GHOST PLAYER | DOD: mathematical AUP walker drives world/ecology triggers | Alternative rejected: spawning a real Player prefab | Estimate: PENDING us
- [ ] Task 8: BIOMASS AUDIT | DOD: daily CSV with prey/predator biomass | Alternative rejected: Debug.Log spam | Estimate: PENDING us
- [ ] Task 9: GAS AUDIT | DOD: pressure finite/non-negative validation | Alternative rejected: UI gauges | Estimate: PENDING us
- [ ] Task 10: MEMORY LEAK AUDIT | DOD: H8Memory growth window over 10 simulated days | Alternative rejected: snapshot-only memory check | Estimate: PENDING us
- [ ] Task 11: EXTINCTION EVENT | DOD: predator biomass zero writes ECOLOGY_COLLAPSE and exits 1 | Alternative rejected: warning-only failure | Estimate: PENDING us
- [ ] Task 12: SUCCESS CRITERIA | DOD: 100 simulated days exits 0 after safety dumps | Alternative rejected: indefinite soak | Estimate: PENDING us
- [ ] Task 13: AUP SHIFT SAFETY | DOD: ghost crosses 5000m boundaries and validates shift cadence | Alternative rejected: local-only movement | Estimate: PENDING us
- [ ] Task 14: ZERO-GC | DOD: no managed allocation in simulation loop by code inspection and profiler hook where available | Alternative rejected: LINQ/strings per loop | Estimate: PENDING us
- [ ] Task 15: MATH LOD | DOD: force High tier math path for runner | Alternative rejected: balanced hardware auto tier | Estimate: PENDING us
- [ ] Task 16: TELEMETRY CAPTURE | DOD: dump blackbox before Application.Quit | Alternative rejected: post-exit cleanup | Estimate: PENDING us
- [ ] Task 17: RECONNAISSANCE | DOD: scan headless log spam risks and suppress runner spam | Alternative rejected: console as telemetry bus | Estimate: PENDING us
- [ ] Task 18: OMEGA COMPILE CHECK | DOD: compile and document CI invocation | Alternative rejected: chat-only claim | Estimate: PENDING us

## Loop Ledger

- Loop 0: Prompt extracted. Status/rationale files created. Code untouched.
