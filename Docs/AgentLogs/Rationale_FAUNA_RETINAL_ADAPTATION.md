# Rationale: FAUNA_RETINAL_ADAPTATION

Status policy: `PENDING VERIFICATION` until Unity Console / test / profiler evidence exists.

## Mandates Selected

- `AI_Creature_Cognition_States.txt`: predator state change must be utility-gated, not a free managed side effect.
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`: no singleton vision manager; signal lanes or registry contracts only.
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`: headlight positions travel as AUP data, runtime positions reconstructed inside the consumer.
- `MATH_Rsqrt_i3_SIMD.txt`: normalize through `math.rsqrt`; no `Vector3.normalized` in retinal math.
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`: fixed native arrays, no LINQ/delegates/managed allocations in the frame path.
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`: retinal exposure/blindness must live in owner-disposed `NativeArray` state.
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`: last 300 frames retained in a fixed black-box ring.
- `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`: powered/off headlight state must be consumed as a brownout-compatible signal, not polled from gameplay.

## Decisions

Problem: Existing fauna perception had managed flashlight exposure but no predator-facing headlight retinal registry.
Solution: Added `SubmarineLightsChangedSignal` and a fixed `NativeArray<LightSourceData>[4]` consumed by `PredatorCognitionDomain`.
Rejected Alternatives: Direct `Light` polling, `VisionManager.Instance`, and scene light searches were rejected because they create cross-domain coupling and frame allocations.
Scalability potential: Low = 1Hz retina cadence and four brightest lights; Middle = current predator cadence; High = same math with more direct headlight stimulus from authored intensities; Ultra = saved cycles can be spent on VFX and animation reactions.
Hardware Impact: i3/MX350 expected hot-path cost is four distance/dot checks per due predator, replacing any raycast-style light query.

Problem: Headlight blindness needs to survive floating-origin shifts.
Solution: Publisher sends `AbsoluteUniversePosition`; job stores `AbsoluteUniversePositionBlit128` and reconstructs runtime position per predator.
Rejected Alternatives: Runtime-only `float3` registry was rejected because origin shift frames would smear the light source.
Scalability potential: Low/Middle/High/Ultra share the same AUP payload; quality changes cadence, not correctness.
Hardware Impact: AUP reconstruction is paid only for four candidate lights in the due cognition job.

Problem: Blind predators need a behavior result without a bespoke AI state tree.
Solution: Retinal blind state boosts existing utility: aversion sets override threat and perpendicular flinch; frenzy doubles aggression and reuses light frenzy attack weighting.
Rejected Alternatives: Adding a new managed `Blind` AI state was rejected because it would fight the existing packed utility output and compatibility bridge.
Scalability potential: Low uses hard lateral fake; Ultra can layer extra animation/VFX on the same `FaunaStateChangedSignal(Blind)`.
Hardware Impact: Lateral flinch is one cross product and one `math.rsqrt`, no NavMesh or physics impulse.

Problem: Black-box mandate requires evidence when retinal math faults.
Solution: Added a fixed 300-entry `NativeArray<RetinalTelemetryEntry>` and cold fault dump path `Docs/AgentLogs/Dump_FAUNA_RETINAL_ADAPTATION.bin`.
Rejected Alternatives: Debug.Log-only reporting was rejected because it does not retain frame history.
Scalability potential: Same ring on every tier; telemetry export can be richer on high-end without changing core math.
Hardware Impact: One compact ring write after completed cognition evaluation; no hot-path file IO.
