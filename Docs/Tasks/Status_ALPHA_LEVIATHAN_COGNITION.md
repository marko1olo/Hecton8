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

- [x] Task 1: SINGLETON ERADICATION / extend `FaunaBrain` only | DOD: no new MonoBehaviour; existing `FaunaBrain` + `PredatorCognitionDomain` only. Rejected custom component. Static estimate: 0 us hot path.
- [x] Task 2: SIGNAL MIGRATION / consume `AcousticPingSignal` | DOD: `CreatureUtilityBrain.Evaluate` consumes latest `AcousticPingSignal`, ignores leviathan roar echo, converts AUP to runtime. Rejected direct scanner/player sonar dependency. Static estimate: ~0.6 us per active predator slow tick.
- [x] Task 3: ASMDEF ISOLATION / `Hecton8.AI.Cognition` -> Contracts | DOD: added `Hecton8.AI.Cognition.asmdef` referencing `Hecton8.Core.Contracts` + `Unity.Mathematics`. Rejected moving fauna runtime into new asmdef. Static estimate: 0 us.
- [x] Task 4: S.O.A. STALKING STATE / `NativeArray<byte> StalkingPhase` | DOD: `_stalkingPhases` and `_stalkingPhaseStartTimes` are persistent SoA lanes in `PredatorCognitionDomain`. Rejected managed enum list. Static estimate: 1 byte + 4 bytes per slot.
- [x] Task 5: CIRCLING MATH / tangent at `FogEnd - 10m` | DOD: `ResolveAlphaCircleDirection` uses player vector, `cross(Up, away)`, and radial correction to fog ring. Rejected Transform-space steering. Static estimate: ~0.08 us per active alpha eval.
- [x] Task 6: ACOUSTIC AVOIDANCE / gaze/headlight dive | DOD: dot > 0.8 or retinal exposure/blindness switches to Hidden dive; high tier biases SDF gradient. Rejected physical burrow simulation. Static estimate: ~0.12 us high tier, ~0.03 us low tier.
- [x] Task 7: PSYCHOLOGICAL STRIKE / false charge and roar | DOD: Phase 2 forces Feint, 30 m/s speed multiplier, and one-shot `AcousticPingSignal` roar. Rejected real attack on charge start. Static estimate: ~0.1 us Burst + one managed signal on transition.
- [x] Task 8: VEER OFF <15m / no hit | DOD: Phase 2 transitions to VeerOff at <15m; Alpha override clears `ShouldAttack`. Rejected damage path. Static estimate: ~0.05 us per active alpha eval.
- [x] Task 9: BIOMASS OVERRIDE / ignore ecological biomass | DOD: apex predators bypass `ApplyEcologyChainOverrides`. Rejected ecosystem concrete branch in Burst job. Static estimate: saves ecology override call for alpha.
- [x] Task 10: AUP SHIFT SAFETY | DOD: acoustic and stalking targets carry AUP and are resolved against current floating-origin offset. Rejected stale Transform-only target authority. Static estimate: AUP conversion only on slow tick.
- [x] Task 11: MATH LOD / Low tier radial fallback | DOD: low tier disables SDF dive and uses radial steer away. Rejected MX350 SDF gradient path. Static estimate: ~0.03 us low tier.
- [x] Task 12: EXECUTION PHASE / SIMULATION SlowTick 10Hz | DOD: alpha predators force `AlphaLeviathanSlowTickIntervalSeconds = 0.1f` in `PrepareEvaluationDueFlags`. Rejected per-frame Update cognition. Static estimate: max 10 evals/sec per alpha.
- [x] Task 13: ZERO-GC | DOD: Burst math path uses NativeArrays/scalars only; source scan found no new managed collections/LINQ in Alpha hot path. Rejected closures/delegates. Static estimate: 0 B/frame.
- [x] Task 14: BLACKBOX DUMP / Alpha phase telemetry | DOD: 300-entry `NativeArray<AlphaLeviathanTelemetryEntry>` ring + `Dump_ALPHA_LEVIATHAN_COGNITION.bin` on fault. Rejected unbounded text log. Static estimate: 64 bytes per telemetry entry.
- [x] Task 15: [BLOCKED BY DEPENDENCY] OMEGA COMPILE CHECK / Burst `rsqrt` vector math | DOD: static scan confirms Alpha distance/direction uses `math.rsqrt`; full compile blocked by stale/generated project references and no live Unity session. Rejected claiming runtime proof. Static estimate: compile proof unavailable.

## Loop Log

- Loop 0: Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`; domain and mandates read; existing dirty files detected and preserved. Compile not run yet.
- Loop 1: Tasks 1-5 implemented in existing cognition bridge/domain; prompt re-read after source pass. `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` failed with 131 missing generated/cross-asmdef dependencies, including stale `Hecton8.AI.Cognition` reference.
- Loop 2: Tasks 6-10 implemented; Unity refresh requested through MCP, but editor readiness timed out after 60s and console read reported no Unity session.
- Loop 3: Tasks 11-13 static scanned for hot-path allocations, `Find*`, LINQ, and vector normalization. Alpha additions use NativeArrays/scalars; managed signal publish exists only on false-charge transition.
- Loop 4: Task 14 telemetry reviewed: fixed-size 300-entry ring, fault dump path, no unbounded per-frame log.
- Loop 5: Task 15 source proof reviewed: Alpha direction/distance uses `math.rsqrt`; full compile remains blocked by external project dependency wall. Status remains PENDING VERIFICATION.
