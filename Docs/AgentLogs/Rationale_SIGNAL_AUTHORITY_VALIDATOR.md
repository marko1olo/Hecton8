# Rationale - SIGNAL_AUTHORITY_VALIDATOR

## Session Start

Problem: Signal lanes are reported as duplicated and diluted across names like CombatEvent, HitSignal, and DamageSignal.
Solution: Use task mandate to centralize signal payloads under Hecton8.Core.Contracts.Signals and SignalBus<T> lanes.
Rejected Alternatives: Monolithic EventBus and direct managed Action/UnityEvent dispatch violate lane segregation and zero-GC hot-path rules.
Scalability potential: Low uses bounded snapshots and drops optional VFX traffic under stress; Middle consumes full nearby gameplay lanes; High keeps richer telemetry; Ultra spends saved CPU on visual/audio overkill in VISUAL_SYNC.
Hardware Impact: Expected low-end i3/MX350 gain depends on existing duplication count; target is lower cache churn and no managed hot-path dispatch. Exact microseconds pending scan/build evidence.

## Mandates Selected

- ARCH_Signal_Lane_Segregation: required for typed SignalBus<T> lanes and duplicate eradication.
- OPT_Zero_GC_Policy_AllocFree_Mandate: required for no managed delegates, strings, LINQ, or copying snapshots in hot paths.
- ARCH_Execution_Phases: required for AupShiftSignal PRE_SIMULATION and VISUAL_SYNC-only presentation fan-out.
- DBG_Telemetry_Crash_Reporting_PostMortem: required for telemetry ring and fault hashes without hot-path logging.
- OPT_Performance_Budgets_FrameTime_VRAM_Limits: required for stress-based load shedding and 0.1 ms suspicion threshold.
- CORE_Damage_System_Hull_Integrity_VFX_Feedback: required for canonical damage payload semantics.
- AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC: required for audio transition signal threading constraints.
- MATH_AUP_Determinism_Sync: required for AUP shift signal ordering and finite spatial payloads.

## Decision - Canonical Damage Lane

Problem: Combat damage was split across a legacy DamageSignal surface and the newer CombatDamageSignal lane.
Solution: Removed the DamageSignal payload and publish/dequeue overloads, kept the compatibility writer name backed by NativeQueue<CombatDamageSignal>.ParallelWriter, and rewired producers to populate CombatDamageSignal directly.
Rejected Alternatives: A wrapper adapter would preserve duplicate semantics and keep two payload shapes alive in hot code.
Scalability potential: Low uses one damage packet and one sanitizer path; Middle/High/Ultra spend the saved branch/cache budget on richer hull, HUD, and audio consumers.
Hardware Impact: Estimated 3-8 us saved in damage-heavy frames on i3/MX350 by removing duplicate queue pressure and legacy conversion branches.

## Decision - Contract Namespace Eviction

Problem: Signal payloads were defined and referenced through Hecton8.Core.Signals plus several feature namespaces.
Solution: Moved all ISignal payload declarations and references to Hecton8.Core.Contracts.Signals and left feature systems in their existing domains.
Rejected Alternatives: Keeping per-feature signal namespaces would make SignalBus<T> discovery dependent on producer ownership instead of contract authority.
Scalability potential: Low gets one namespace scan and one typed-lane registry; High/Ultra can add VFX/audio lanes without scattering authority.
Hardware Impact: Runtime gain is indirect; expected 1-2 us saved in bootstrap/audit paths and lower integration churn on cheap devices.

## Decision - Lane Registry Authority

Problem: Feature systems configured lanes locally, which allowed capacity/hash drift.
Solution: Centralized SignalBus<T>.Configure calls inside GlobalSignals.InitializeAllQueues() and reduced feature bootstraps to GlobalSignals.InitializeAllQueues().
Rejected Alternatives: Keeping local Configure calls would allow a late feature to mutate lane capacity after allocation.
Scalability potential: Low gets deterministic capacity limits; Middle/High/Ultra keep full optional lanes without per-feature disagreement.
Hardware Impact: Startup cost is centralized; frame hot path avoids reconfiguration checks. Estimated 2-4 us saved on bootstrap-heavy scene loads.

## Decision - Stress Based Visual Load Shedding

Problem: Optional VFX signals could flood the bus under high SystemStress01.
Solution: Added SignalBusRegistry.SystemStress01 and per-lane ResolveFrameLimit. Non-critical VFX lanes drop to zero when stress >= 0.8 and propagate fully when stress <= 0.2.
Rejected Alternatives: Static low-tier caps would punish high-end hardware and still overload weak hardware during spikes.
Scalability potential: Low drops debris/droplet/reentry/bullet-time VFX under stress; Middle keeps bounded cinematic lanes; High and Ultra propagate 100% optional feedback when health allows.
Hardware Impact: Estimated worst-spike recovery saves 20-80 us on i3/MX350 by avoiding non-critical snapshot writes.

## Decision - Overflow Fault Handling

Problem: Lane storms above 1024 packets could silently burn frame time and hide the responsible producer.
Solution: Clear the queue, publish GlobalTelemetryBus system degradation with LOVF hash, set the non-critical VFX kill switch bit, and emit a development [LANE_OVERFLOW_FAULT] warning.
Rejected Alternatives: Dropping only oldest packets hides storm severity and leaves producers active.
Scalability potential: Low survives lane storms by cutting optional producers; High/Ultra retain normal overkill while the lane is healthy.
Hardware Impact: Estimated 0.1-0.4 ms saved during storm frames by replacing drain loops with queue clear and kill switch feedback.

## Decision - ABI Layout Exception

Problem: The batch requested sequential Pack=1 for all signal structs, but 130 legacy signal structs use explicit fixed-size layouts and several are ABI unions such as ImpactSignal Force/Velocity and HighSpeedImpactSignal LostKineticEnergy/KineticEnergy.
Solution: Converted newly evicted external signal payloads to sequential Pack=1 fixed-size structs, but retained existing explicit union lanes to avoid corrupting validated offsets and caller semantics.
Rejected Alternatives: Mechanical FieldOffset removal would compile-risk high and would change binary payload offsets for live consumers.
Scalability potential: Low stays stable because binary-compatible lanes are not churned; High/Ultra can get a staged layout migration once union aliases are replaced with properties.
Hardware Impact: No immediate frame gain; avoids an estimated multi-millisecond integration failure cost from invalid payload interpretation.

## Decision - Compile Wall Classification

Problem: dotnet build Hecton8.Core.csproj failed on missing AI/animation dependencies and later on missing `ProceduralLadderClimbRuntime` references in `GlobalRegistry.cs`, not on SignalBus changes.
Solution: Recorded the compile wall and stopped timed-out Assembly-CSharp/dotnet worker processes after integration attempts.
Rejected Alternatives: Editing AI/animation/VFX dependencies would violate the CORE/SIGNALS boundary and risk overwriting other agents.
Scalability potential: Signal lanes remain decoupled; unrelated compile dependency repair can proceed independently.
Hardware Impact: No runtime gain; prevents background compiler workers from burning CPU after timeout.

## Decision - Late Agent Signal Drift

Problem: After the first audit, new files introduced stale `Hecton8.Core.Signals` imports and two local compass lane Configure calls.
Solution: Moved `AnomalyProximitySignal` and `CompassCalibratedSignal` into `Hecton8.Core.Contracts.Signals`, converted both to sequential Pack=1 fixed-size payloads, and registered their lanes in `GlobalSignals.InitializeAllQueues()`.
Rejected Alternatives: Treating late files as out-of-scope would leave the final audit false.
Scalability potential: Low keeps compass anomaly traffic bounded at 4 low-tier packets; High/Ultra allow 16 anomaly packets and 8 calibration packets per frame.
Hardware Impact: Estimated 1-3 us saved at compass startup by avoiding local lane reconfiguration and stale namespace resolution.
