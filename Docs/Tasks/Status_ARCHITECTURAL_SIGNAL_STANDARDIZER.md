# ARCHITECTURAL_SIGNAL_STANDARDIZER Status

Status: PENDING VERIFICATION
Domain: Echelon 1 Core & Memory Infrastructure / Global EventBus + Signal Lanes
Task count: 15
Prompt source: User-provided XML for ARCHITECTURAL_SIGNAL_STANDARDIZER. `Docs/Tasks/CURRENT_BATCH.md` does not contain this ID in the current workspace scan.
Selected mandates:
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- QA_Evidence_Text_Filter_Audit.txt
- CORE_Damage_System_Hull_Integrity_VFX_Feedback.txt

## State Machine

- [x] Task 1 - Protocol mapping | Justification: STATIC_SOURCE protocol table written to `Docs/Reports/SIGNAL_UNIFICATION_AUDIT.md`; found 59 Action/delegate files, 30 UnityEvent files, 18 legacy EventBus publish files, 108 direct NativeQueue files, 31 SignalBus consumers, 14 SignalBus producers. Alternative rejected: blind rewrite across dirty tree. Microsecond estimate: 0us saved yet; audit-only.
- [x] Task 2 - Duplicate signal hunt | Justification: mapped `Core.Signals.DamageSignal`, `Core.Signals.CombatDamageSignal`, `Gameplay.DamageSignal`, and internal `Gameplay.CombatDamageSignal`. DOD practice: choose bus-facing DTO before rewiring. Alternative rejected: deleting local receiver packet before call-site migration. Microsecond estimate: 1-4us expected in combat bursts, PENDING PROFILER.
- [x] Task 3 - Interface drift scan | Justification: `rg` found `IAudioService` but no `ICoreAudio` in first-party source. DOD practice: source-backed negative finding. Alternative rejected: inventing an `ICoreAudio` migration. Microsecond estimate: 0us; no duplicate interface removed.
- [x] Task 4 - Consolidation | Justification: pinned `Hecton8.Core.Signals.CombatDamageSignal` as the unified cross-domain combat damage lane and kept internal job packet local. DOD practice: additive compatibility, no public signature mutation. Alternative rejected: moving/removing all damage structs in one pass. Microsecond estimate: 1-4us expected in combat ingress, PENDING PROFILER.
- [x] Task 5 - Lane enforcement | Justification: `CombatDamageRuntime` now consumes `SignalBus<Core.Signals.CombatDamageSignal>.GetFrameSnapshot()` for global damage ingress. Alternative rejected: `GlobalSignals.TryDequeueDamage` destructive consumer. Microsecond estimate: 1-4us expected during bursts, PENDING PROFILER.
- [x] Task 6 - NaN vaccination | Justification: `SignalBus<T>.Push()` now sanitizes known consolidated damage/impact lanes with `math.isfinite` and numeric telemetry. Alternative rejected: reflection field scan and `ISignal` method mutation. Microsecond estimate: normal path sub-1us per push, PENDING PROFILER.
- [ ] Task 7 - Producer purge | Justification: producers must use typed signal push lanes only. Alternative rejected: direct static EventBus/UnityEvent. Microsecond estimate: pending.
- [ ] Task 8 - Consumer purge | Justification: consumers pull frame snapshots, no callback cascades. Alternative rejected: observer lists. Microsecond estimate: pending.
- [ ] Task 9 - Delegate eradication | Justification: hot simulation loop cannot rely on Action/delegate. Alternative rejected: delegate caching where a typed signal lane is required. Microsecond estimate: pending.
- [ ] Task 10 - Contract pinning | Justification: GlobalRegistry accesses in hot loops must be cached outside update lanes. Alternative rejected: convenience property polling. Microsecond estimate: pending.
- [ ] Task 11 - Batched compile | Justification: compile after lane batches to isolate failures. Alternative rejected: end-only compile. Microsecond estimate: pending.
- [ ] Task 12 - Triple-strike fix | Justification: call-site repairs allowed after signature break. Alternative rejected: stopping on first error. Microsecond estimate: pending.
- [ ] Task 13 - Zero-GC verification | Justification: static scan plus build only, runtime GC remains PENDING without profiler. Alternative rejected: claiming measured 0 GC from text. Microsecond estimate: pending.
- [ ] Task 14 - Blackbox dump | Justification: synaptic density gain must be logged to rationale with telemetry impact. Alternative rejected: chat-only report. Microsecond estimate: pending.
- [ ] Task 15 - Omega polish | Justification: only after all tasks checked/blocked; signal structs padded and string poison scan. Alternative rejected: reading polish before core complete. Microsecond estimate: pending.

## Iteration Log

### Loop 0 - Intake
- Mandatory communication mess scan executed with `rg "Action<|UnityEvent|EventBus\.Publish|NativeQueue<"` from `C:\hades`.
- `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="ARCHITECTURAL_SIGNAL_STANDARDIZER">`; user-supplied XML is the active prompt boundary.
- Worktree is heavily dirty before this agent touched code. No unrelated changes will be reverted.

### Loop 1 - Tasks 1-6
- Read protocol map, duplicate damage packets, audio interface scan, and GlobalSignals source.
- Edited `Assets/_Project/Scripts/Core/GlobalSignals.cs` and `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs`.
- Compile attempt: `dotnet build Hecton8.Core.csproj` failed with 131 dependency errors outside touched files. Status remains PENDING VERIFICATION.
