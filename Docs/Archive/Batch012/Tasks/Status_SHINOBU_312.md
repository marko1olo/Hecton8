# SHINOBU_312 - ANXIETY_COOL_DOWN_RING_BUFFER

Status: CODE COMPLETE / BUILD GATED BY ACTIVE DOTNET
Domain: ECHELON 3 FLORA, FAUNA & BIOTA / Predator Cognition Anxiety Cooling
Task count: 20

## Mandates Read

- AI_Creature_Cognition_States.txt
- ARCH_Execution_Phases.txt
- ARCH_Signal_Lane_Segregation.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- MATH_AUP_Determinism_Sync.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt

## State Machine

- [x] Task 01 MANDATORY_CODEBASE_GREP_SCAN | DOD: rg scan over AI/Cognition/GlobalSignals/SignalBus/current batch. Runtime coroutine/timer offenders were not found; editor scanner pattern strings only. Alternative rejected: blind deletion. Estimate: 0 us hot path.
- [x] Task 02 PARTIAL_CLASS_INTEGRATION_MANDATE | DOD: no standalone HectonAnxietyManager; existing UtilityAICognitionVault made partial and SHINOBU_312 surface isolated in UtilityAICognitionVault_AnxietyDecay.cs. Alternative rejected: duplicate cognition runtime owner. Estimate: 0 us hot path, avoids second owner poll.
- [x] Task 03 SIGNALBUS_MATRIX_VERIFICATION | DOD: FaunaStateChangedSignal and FocusBrokenSignal lanes confirmed; no new signal invented because state truth remains Vault-local. Alternative rejected: CreatureCalmedDownSignal fragmentation. Estimate: 0 us hot path.
- [x] Task 04 COROUTINE_STATE_INQUISITION | DOD: StartCoroutine/WaitForSeconds/IEnumerator audit found no runtime AI anxiety cooldown scripts to delete. Alternative rejected: deleting editor scanner strings. Estimate: replaces potential coroutine resume cost, approx 12-40 us per active managed cooldown set.
- [x] Task 05 MANAGED_TIMER_CLASS_PURGE | DOD: CoolDownTimer/CooldownTimer/Time.time audit found no active managed anxiety timer owner; new route uses FrostTick dt. Alternative rejected: object timers. Estimate: avoids object chase/cache miss class, approx 8-25 us per 1k timers.
- [x] Task 06 EMERGENCY_MOCK_ANXIETY_ENVIRONMENT | DOD: GenerateMockAnxietySpikesJob plus mock shelter SDF job implemented for dense fear/aggression stress data. Alternative rejected: scene-only scare reproduction. Estimate: 4096 mock rows under one Burst batch, 0 managed allocations.
- [x] Task 07 BURST_EXPONENTIAL_DECAY_KERNEL | DOD: CalculateAnxietyDecayJob uses Burst deterministic IJobParallelFor, NoAlias arrays, raw pointer AsRef mutation, exponential formula. Alternative rejected: coroutine cooldown. Estimate: expected 180-420 us for 4096 rows depending exp weight.
- [x] Task 08 THE_DEAR_LIE_STATE_TRANSITION | DOD: values below CalmingThreshold are snapped to 0; Agitated bit cleared by AND inverted mask; Flee/Hunt snaps to Patrol when both scalars calm. Alternative rejected: infinite exponential tail. Estimate: saves approx 30-90 us over long calm tails.
- [x] Task 09 CONTINUOUS_SCALABILITY_DECAY_APPROXIMATION | DOD: GlobalQualityWeight and ThermalPressure01 produce continuous exact-exp weight; exact exp is skipped when weight is effectively zero and otherwise lerps with linear decay. Alternative rejected: binary low/high hardware branch. Estimate: low-tier saves approx 120-260 us per 4096 rows against all-exp path.
- [x] Task 10 SHELTER_BASED_COOLING_MULTIPLIER | DOD: Vault-owned SDF sample multiplies decay rate from negative shelter distance. Alternative rejected: pathfinding/trigger dependency. Estimate: one scalar fetch per entity, approx 35-80 us per 4096 rows.
- [x] Task 11 AUP_PRECISION_SDF_LOCALIZATION | DOD: creature AUP minus SDF origin executed in double before float3 downcast and index math. Alternative rejected: absolute float world coordinate. Estimate: 0.5-1.5 us per 1k rows vs unsafe failures.
- [x] Task 12 ROLLBACK_NETCODE_STATE_FENCE | DOD: all new jobs use FloatMode.Deterministic and fixed DTO layouts. Alternative rejected: platform-varying unmanaged mutation. Estimate: deterministic proof value, no frame cost beyond Burst mode.
- [x] Task 13 ZERO_INIT_OVERHEAD_BYPASS | DOD: anxiety profiles/tuning/scratch/telemetry/shelter buffers requested with NativeArrayOptions.UninitializedMemory and cold defaults overwrite active slots. Alternative rejected: MemClear/OS zero reliance. Estimate: saves approx 20-70 us on buffer acquisition.
- [x] Task 14 TELEMETRY_ANXIETY_RECORDER | DOD: 300-entry AnxietyTelemetryEntry ring, cursor, average fear/aggression, shelter counts, nonfinite counts, microsecond patch, dump to Docs/AgentLogs/Dump_SHINOBU_312.bin on fault/>0.5ms. Alternative rejected: log-only diagnostics. Estimate: telemetry scan approx 45-110 us per 4096 rows.
- [x] Task 15 ANXIETY_TUNER_EDITOR_WINDOW | DOD: UI Toolkit AI Anxiety Tuner added with Vault-backed sliders, line graph, mock generation, FrostTick execution, dump and scanner buttons. Alternative rejected: recompilation-only tuning. Estimate: editor-only.
- [x] Task 16 CSV_PSYCHOLOGY_PROFILES_INGESTOR | DOD: fauna_psychology_profiles.csv cold parser uses NativeArray<byte>, ReadOnlySpan<byte>, FNV-1a species hash, AupPrecisionMath.TryParseFloat, no float.Parse. Alternative rejected: managed string split/object parser. Estimate: cold boot only.
- [x] Task 17 LIVE_DECAY_DEBUG_GIZMO | DOD: Scene View draws fear yellow and aggression red bars from raw CognitionStateDTO/AUP rows. Alternative rejected: log-only inspection. Estimate: editor-only.
- [x] Task 18 ARCHITECTURAL_METRIC_VALIDATOR | DOD: OOP_Timer_Scanner added and report artifact created with "OOP Coroutine Timers Eradicated"; scanner excludes Editor proof tooling, scopes AI/Fauna/Biota/Sensory by path or namespace, strips comments/strings and brace-scans IEnumerator while blocks for Time.deltaTime. Alternative rejected: manual audit only and Roslyn dependency expansion. Estimate: editor/static only.
- [x] Task 19 UNALIGNED_MEMORY_TRAP_GUARD | DOD: InitializeOnLoad guard calls TryRunAnxietySelfAudit and checks Profile=16/Align=4, Scratch=64, Telemetry=64; throws global::Hecton8.Core.FatalArchitectureException on drift. Alternative rejected: InvalidOperationException/soft warning. Estimate: editor load only.
- [x] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | DOD: TryRunAnxietySelfAudit validates struct sizes, alignment, ring length and fault threshold; Binary Payload Integration Ledger now records BufferIDs 71971..71978 and scratch false-sharing guard. Compile gate blocked by active dotnet so no new build was launched. Alternative rejected: violating rebuild protection. Estimate: 0 us hot path.

## Iteration Log

Loop 0: Prompt extracted from CURRENT_BATCH.md. Status/rationale were missing and created.
Loop 1: Archaeology read mandates, architecture docs, domain boundaries, UtilityAICognition and SignalBus lanes.
Loop 2: Runtime DTOs/jobs added; self-review found all-exp path was still executing and patched low exactWeight to skip exp.
Loop 3: Vault partial added; self-review found CSV hash compare would always reload and patched LastCsvHash to raw file hash.
Loop 4: Editor tuner/scanner/guard added; self-review found fear color was blue and patched to yellow per debug task.
Loop 5: Build guard checked twice. First gate: dotnet PID 5468 and CPU 79.26 percent. Second gate: dotnet PIDs 1548/14272 and CPU 100 percent. Compile launch was blocked by protocol.
Loop 6: Ultra polish pass re-read SHINOBU_312 XML, rationale, ledger and static xray path. PROJECT_STATE_STATIC_XRAY.md is missing on disk. Scratch DTO expanded from 32 to 64 bytes, scanner gained coroutine-while-delta structural pass, ledger entry added, reports updated. Build guards: dotnet PID 1548 active at CPU 18.79 percent; later dotnet PIDs 3056/16936 active at CPU 100 percent. Build blocked.
Loop 7: Re-read prompt/ledger/boundaries after renewed user mandate. Found Task 19 mismatch: guard documented FatalArchitectureException but code threw InvalidOperationException. Patched guard to global::Hecton8.Core.FatalArchitectureException and added editor-only Hecton8.Core asmdef reference. Runtime asmdef remains sibling-free. Scanner scope expanded to AI/Fauna/Biota/Sensory path or namespace. Shared AI report regained a SHINOBU_312 section without deleting the SHINOBU_304 root report. Static JSON/asmdef validation passed. Runtime SHINOBU_312 scan remains clean. Build gate: dotnet PIDs 3056/14000 active, CPU 100 percent.

## Verification

- Static search: runtime AI anxiety path contains no Coroutine, StartCoroutine, WaitForSeconds, CoolDownTimer, Time.time, Time.deltaTime or IEnumerator.
- Signal matrix: existing FaunaStateChangedSignal/FocusBrokenSignal lanes confirmed; no new signal type added.
- Compile: not run. Latest gate found active dotnet PIDs 3056/14000 and CPU 100 percent; build is blocked by both active compiler process and CPU rule.
