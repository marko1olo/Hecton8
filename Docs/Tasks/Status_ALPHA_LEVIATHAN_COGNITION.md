# Status_ALPHA_LEVIATHAN_COGNITION

Prompt: `ALPHA_LEVIATHAN_COGNITION`
Domain: AI / Fauna Cognition
Status: PENDING VERIFICATION

Mandates read:
- `AI_Creature_Cognition_States.txt`
- `AI_DYNAMIC_NAVGRID_SDF_INTEGRATION.txt`
- `AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

## Checklist

- [x] Task 1: SINGLETON ERADICATION / extend `FaunaBrain` only | DOD: confirmed no new MonoBehaviour path; existing `FaunaBrain` + `PredatorCognitionDomain` are the change surface. Rejected custom component. Estimate: 0 us hot path.
- [ ] Task 2: SIGNAL MIGRATION / consume `AcousticPingSignal` | DOD pending source change. Rejected direct concrete sonar owner dependency. Estimate: pending.
- [ ] Task 3: ASMDEF ISOLATION / `Hecton8.AI.Cognition` -> Contracts | DOD pending asmdef slice. Rejected moving all fauna scripts because it risks cyclic compile damage. Estimate: pending.
- [ ] Task 4: S.O.A. STALKING STATE / `NativeArray<byte> StalkingPhase` | DOD pending source change. Rejected managed enum list. Estimate: pending.
- [ ] Task 5: CIRCLING MATH / tangent at `FogEnd - 10m` | DOD pending Burst math. Rejected Transform-space steering. Estimate: pending.
- [ ] Task 6: ACOUSTIC AVOIDANCE / gaze/headlight dive | DOD pending source change. Rejected physics simulation; use deterministic steering fake. Estimate: pending.
- [ ] Task 7: PSYCHOLOGICAL STRIKE / false charge and roar | DOD pending source change. Rejected real bite for first encounter. Estimate: pending.
- [ ] Task 8: VEER OFF <15m / no hit | DOD pending source change. Rejected damage route. Estimate: pending.
- [ ] Task 9: BIOMASS OVERRIDE / ignore ecological biomass | DOD pending source change. Rejected ecosystem concrete dependency. Estimate: pending.
- [ ] Task 10: AUP SHIFT SAFETY | DOD pending AUP runtime conversion audit. Rejected stale Transform world-space target authority. Estimate: pending.
- [ ] Task 11: MATH LOD / Low tier radial fallback | DOD pending source change. Rejected SDF dive on MX350. Estimate: pending.
- [ ] Task 12: EXECUTION PHASE / SIMULATION SlowTick 10Hz | DOD pending dispatcher cadence audit. Rejected per-frame Update cognition. Estimate: pending.
- [ ] Task 13: ZERO-GC | DOD pending static scan. Rejected LINQ/managed buffers. Estimate: pending.
- [ ] Task 14: BLACKBOX DUMP / Alpha phase telemetry | DOD pending 300-entry ring + dump path. Rejected unbounded logs. Estimate: pending.
- [ ] Task 15: OMEGA COMPILE CHECK / Burst `rsqrt` vector math | DOD pending compile and static math scan. Rejected normalize/sqrt path where reciprocal sqrt is enough. Estimate: pending.

## Loop Log

- Loop 0: Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`; domain and mandates read; existing dirty files detected and preserved. Compile not run yet.
