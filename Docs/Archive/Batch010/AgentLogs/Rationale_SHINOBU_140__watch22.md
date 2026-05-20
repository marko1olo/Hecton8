# SHINOBU_140 Rationale

Status: PENDING VERIFICATION

## Decision 00 - Evidence Before Surgery

Problem: The latest mandate repeats the full 20-task assignment and the active state files were missing from mandated paths.

Solution: Re-extract the `SHINOBU_140` XML block from `Docs/Tasks/CURRENT_BATCH.md`, confirm `TASK_COUNT=20`, reread the binary payload ledger, and recreate active state files from current evidence.

Rejected Alternatives: Proceeding from chat memory was rejected. Editing archive files was rejected because active mandate paths must exist.

Scalability potential: File-backed state prevents context loss across the 20-agent batch.

Hardware Impact: Documentation/tooling only.

## Decision 01 - Rollback Presentation Suppression Route

Problem: Task 11 had a remaining presentation leak: netcode owned audio suppression, dispatcher skipped visual sync, but particle suppression had no shared owner-local surface.

Solution: Add `DispatcherPresentationSuppressionDTO`, explicit 32 bytes, and Vault buffer `70626` owned by `SystemDispatcher`. The dispatcher writes this one-record state before `VISUAL_SYNC`: rollback frames set `RollbackFence | AudioSuppression | ParticleSuppression | VisualSyncSuppressed`; health-pressure frames set `HealthPressure | VisualSyncSuppressed`; non-suppressed frames write zero flags.

Rejected Alternatives: A new `SignalBus<T>` lane was rejected because it would be a global route without a route-card owner. Direct VFX/Audio references were rejected as compile-wall breaches. A dispatcher-owned rollback resimulation loop was rejected because `RollbackFixedPipelineJob.ExecuteRollback()` and `HeadlessResimulationCommandJob` already own restore/resimulation/audio command emission; duplicating them would double-run side effects.

Scalability potential: Low devices collapse rollback presentation work to one Vault read and skipped visual sync. Middle/high/ultra presentation systems can read the same scalar and decide how much visual overkill to resume after the fence clears.

Hardware Impact: One overwritten 32-byte Vault row per visual-sync decision; no private native allocation; no managed allocation.

## Decision 02 - Layout Math

Problem: New presentation suppression state must not reintroduce unaligned DTO debt.

Solution: Use explicit offsets: `FrameId=0 uint`, `Flags=4 uint`, `GlobalQualityWeight=8 float`, `Suppression01=12 float`, `RollbackFlags=16 uint`, pads `20..31`. Total `32` bytes, an exact multiple of 16. It is not an atomic counter, so 64-byte false-sharing padding is not required.

Rejected Alternatives: Sequential layout and bool fields were rejected. A 16-byte record was rejected because it could not carry rollback flags and quality scalar.

Scalability potential: Same record supports weak and ultra devices.

Hardware Impact: 32-byte aligned row, cache-stable.

## Decision 03 - H-Phi Red Gate Binding

Problem: Static scanner truth must remain in canonical H-Phi after the new route.

Solution: Re-run fallback scanners and H-Phi. Latest values: `totalCritical=3538`, `totalWarnings=515`, `Rollback_Fence_Compliance=0 critical / 0 warning`, canonical `gate_passed=false`.

Rejected Alternatives: Claiming green H-Phi was rejected because global static debt remains.

Scalability potential: Governance only.

Hardware Impact: Fallback scan cost 59.1 s; H-Phi refresh cost 4.4 s; player cost 0 us.

## Decision 04 - Compile Wall Scope

Problem: Compile-wall scanner still reports 124 critical findings, mostly legacy Core source and asmdef edges. Removing all of them in this pass would touch many ownership boundaries.

Solution: Keep the scanner red and fix the local regression class: no direct `Hecton8.Networking` Core reference remains, and the new presentation suppression route uses Core/Vault contracts only.

Rejected Alternatives: Mass editing `GlobalRegistry`, `GlobalSignals`, and generated project references was rejected as cross-domain surgery without owner contract review.

Scalability potential: The red gate now provides a precise backlog while avoiding uncontrolled merge damage.

Hardware Impact: Tool-only.

## Decision 05 - Ledger Route Card

Problem: Adding buffer `70626` without architecture ledger ownership would create a hidden payload route.

Solution: Add a SHINOBU_140 section to `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` documenting buffers `70620..70626`, the 32-byte suppression DTO layout, the rollback probe boundary, and the reason dispatcher does not duplicate netcode resimulation.

Rejected Alternatives: Chat-only proof and status-file-only proof were rejected because payload ownership belongs in the architecture ledger.

Scalability potential: Presentation suppression consumers now have a single documented fact route for weak through ultra devices.

Hardware Impact: Documentation only; runtime impact remains the one 32-byte Vault overwrite already recorded.

## Active Mandates

- ARCH_Execution_Phases
- ARCH_Global_Registry_ServiceLocator_DI_Init
- ARCH_Signal_Lane_Segregation
- DATA_Runtime_Struct_Layout_ARM64
- OPT_Native_Memory_Collections_JobSystem_Protocol
- OPT_Zero_GC_Policy_AllocFree_Mandate
- MATH_AUP_Determinism_Sync
- DBG_Telemetry_Crash_Reporting_PostMortem
