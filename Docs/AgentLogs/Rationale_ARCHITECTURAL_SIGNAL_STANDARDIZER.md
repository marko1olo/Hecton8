# Rationale - ARCHITECTURAL_SIGNAL_STANDARDIZER

Status: PENDING VERIFICATION
Evidence class: STATIC_SOURCE / STATIC_DOC until compile or Unity logs exist.

## Intake Decisions

Problem: Project has multiple communication mechanisms (`Action<T>`, `UnityEvent`, legacy `EventBus.Publish`, direct `NativeQueue<T>`, and current `GlobalSignals` NativeQueue lanes).
Solution: Start with forensic audit and narrow code targets before changing contracts. DOD pattern: evidence-first source scan under QA_Evidence_Text_Filter_Audit.
Rejected Alternatives: Blind global replacement of events is too risky in a 40+ agent dirty workspace and would likely damage unrelated domains.
Scalability potential: Low tier uses flat unmanaged signal packets and budgeted drains; Middle/High/Ultra can spend saved callback overhead on richer VFX/audio reactions without changing gameplay truth.
Hardware Impact: Removing managed callback fanout from hot lanes is expected to save microseconds per dispatch on i3/MX350, but measured proof is absent until profiler/GCMonitor.

Problem: `Docs/Tasks/CURRENT_BATCH.md` does not include this agent ID, while user provided a full XML block in chat.
Solution: Treat the user-provided XML as the active prompt boundary and log the batch mismatch. Continue with disk memory files to satisfy anti-amnesia.
Rejected Alternatives: Executing neighboring batch prompts from `CURRENT_BATCH.md` would violate strict parsing and cross-agent boundaries.
Scalability potential: N/A, process integrity decision.
Hardware Impact: N/A.

Problem: Signal contract changes risk breaking other active agents.
Solution: Favor additive wrappers and typed lanes in `Hecton8.Core.Contracts` where source confirms need; avoid public signature mutation unless compile error proves dependency.
Rejected Alternatives: Mass public API mutation violates interface immutability and creates avoidable compile walls.
Scalability potential: Stable contracts let low-tier and high-tier consumers read the same packets with tier-specific drain budgets.
Hardware Impact: Preserves cache locality by minimizing bridge layers.

Problem: Damage routing had two cross-domain packet shapes and one internal combat job packet; deleting the local gameplay structs would mutate public receiver signatures across multiple files.
Solution: Pin `Hecton8.Core.Signals.CombatDamageSignal` as the unified cross-domain SignalBus lane and rewire `CombatDamageRuntime` to consume its frame snapshot. Keep local job packet as internal runtime data and keep legacy `DamageSignal` queue only as compatibility/latest-state mirror.
Rejected Alternatives: Removing `Hecton8.Gameplay.DamageSignal` immediately would break `IDamageSignalReceiver`, `SubmarineStructuralGrid`, mountable vehicle receivers, and habitat callbacks without a full receiver migration pass.
Scalability potential: Low tier drains capped snapshot counts; high/ultra can consume the same packet for richer hull dent/audio/visor feedback without callback fanout.
Hardware Impact: Removes one destructive queue drain from combat ingress and replaces it with contiguous snapshot reads. Estimated gain: 1-4us per active damage burst on i3/MX350, PENDING PROFILER.

Problem: Typed `SignalBus<T>.Push()` accepted non-finite float payloads and deferred sanitization to selected consumers.
Solution: Add a Push-level finite guard for consolidated damage/impact lanes using `math.isfinite`, fallback zeros, and numeric telemetry via `GlobalTelemetryBus.PublishMathGuardInvalidNumber`.
Rejected Alternatives: Reflection-based field scanning was rejected because it would put metadata logic in hot signal paths; adding methods to `ISignal` was rejected because it would break every existing signal struct.
Scalability potential: Low tier gets deterministic safe fallback; high/ultra can trust signal consumers and spend cycles on visual overkill rather than defensive checks per consumer.
Hardware Impact: Adds a small type-branch guard to Push. Invalid-number path saves crash/debug time and emits black-box telemetry. Estimated normal-path cost: sub-1us per push, PENDING PROFILER.

Problem: `dotnet build Hecton8.Core.csproj` fails before runtime verification.
Solution: Record failure as dependency compile wall. Do not revert this agent's changes because compiler output showed no errors in the touched files.
Rejected Alternatives: Fixing missing `MacroSwarm`, `BrineLayerSample`, audio virtualization, and fluid assemblies is integrator scope and outside this signal-standardizer pass unless a touched call-site breaks.
Scalability potential: N/A.
Hardware Impact: N/A.

## Mandates Loaded

- ARCH_Global_Registry_ServiceLocator_DI_Init.txt: GlobalRegistry only for stable service discovery/direct queries; broadcasts use EventBus/signal packets; no hot-loop registry polling.
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt: hot paths allocate 0 managed bytes; no LINQ, string ops, unmanaged payload string, or delegate churn.
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt: NativeQueue/NativeArray ownership, persistent allocation tracking, SPSC/MPSC discipline, no mid-frame Complete.
- DBG_Telemetry_Crash_Reporting_PostMortem.txt: fixed 300-frame ring buffer and binary dump model for critical system state.
- QA_Evidence_Text_Filter_Audit.txt: static text search is not runtime proof.
- CORE_Damage_System_Hull_Integrity_VFX_Feedback.txt: DamageSignal channel separation and payload expectations.
