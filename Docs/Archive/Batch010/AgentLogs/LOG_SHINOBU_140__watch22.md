# SHINOBU_140 Log

## 2026-05-20 - Presentation Suppression Vault Route

What was wrong:
- Rollback visual fence existed, and netcode audio suppression existed, but particle/presentation suppression had no shared unmanaged route.
- Active state files were missing from mandated live paths again.

What was done:
- Recreated active status/rationale/log files.
- Added `SystemDispatcherMasterPresentationSuppression = 70626`.
- Added explicit 32-byte `DispatcherPresentationSuppressionDTO` and flag constants.
- `SystemDispatcher` now writes one Vault-owned suppression row before `VISUAL_SYNC`.
- Fallback scanner now reports `Rollback_Fence_Compliance: critical=0 warning=0`.
- `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` now documents SHINOBU_140 buffers `70620..70626`.

Cinematic cheats used:
- Rollback presentation is not simulated. The dispatcher publishes a scalar/bitmask suppression record and skips `VISUAL_SYNC`; GPU/VFX/audio presentation domains can fade/mute/skip through this single fact.

Exact microseconds saved:
- Before: presentation domains had no common rollback suppression fact and could emit work during resim frames.
- After: one 32-byte Vault write plus skipped visual-sync phase on rollback/health-pressure frames.
- Exact visual-sync savings remain profiler-pending because registered presentation systems vary by scene.

Verification:
- `python -m py_compile Tools/RunShinobu140StaticScanners.py Tools/CalculateHPhi.py` passed.
- Brace delta: `SystemDispatcher.cs=0`, `SystemDispatcherContracts.cs=0`, `H8Memory.cs=0`.
- `git diff --check` passed with line-ending warnings only.
- `python -B Tools/RunShinobu140StaticScanners.py --output-dir Docs/Reports` returned gate `1` by design: `totalCritical=3538`, `totalWarnings=515`.
- `python -B Tools/CalculateHPhi.py --workers 1 --json-output Docs/Reports/HECTON_PHI_SCORE_FINAL.json --graph-output Docs/Reports/HECTON_PHI_ARCHITECTURE_GRAPH.png --atlas Docs/PROJECT_ATLAS.md` passed; canonical H-Phi embeds the same red gate.
- H-Phi was re-run after the ledger update and again reported `files=5206`, `DOMAIN_INDEX_COUNT=85`, `STATUS: PHI CALCULATED`.
- No new build was launched in this loop; previous legal targeted build is blocked by 314 external errors outside the touched files.

<SELF_AUDIT>
  <Agent id="SHINOBU_140" domain="MASTER_INTEGRATION_SURGEON / Echelon 9 Global Architecture" taskCount="20" status="PENDING_VERIFICATION" />
  <TaskReconciliation>
    <Task id="01" result="PASS_STATIC_GATE" />
    <Task id="02" result="PASS_STATIC_GATE" />
    <Task id="03" result="PASS_LOCAL_DTO" />
    <Task id="04" result="PASS_STATIC_LAYOUT" />
    <Task id="05" result="PASS" />
    <Task id="06" result="PASS_STATIC_SOURCE" />
    <Task id="07" result="PASS_STATIC_GATE" />
    <Task id="08" result="PASS_STATIC_ORCHESTRATION" />
    <Task id="09" result="PASS_STATIC_GATE" />
    <Task id="10" result="PASS" />
    <Task id="11" result="PASS_OWNER_LOCAL_FENCE_AND_SUPPRESSION_ROUTE" />
    <Task id="12" result="PASS_STATIC_GATE" />
    <Task id="13" result="PASS" />
    <Task id="14" result="PASS_STATIC_GATE_RED_DEBT_REMAINS" />
    <Task id="15" result="PASS_LOCAL_BUFFERS" />
    <Task id="16" result="PASS_STATIC_SOURCE" />
    <Task id="17" result="PASS_EDITOR_SOURCE" />
    <Task id="18" result="PASS_STATIC_TOOL_RED_GATE_BOUND" />
    <Task id="19" result="PASS_EDITOR_SOURCE" />
    <Task id="20" result="BLOCKED_BY_DEPENDENCY_BUILD_314_ERRORS" />
  </TaskReconciliation>
  <StructLayout name="DispatcherPresentationSuppressionDTO" sizeBytes="32" alignment="16-byte-multiple">
    <Field name="FrameId" offset="0" size="4" />
    <Field name="Flags" offset="4" size="4" />
    <Field name="GlobalQualityWeight" offset="8" size="4" />
    <Field name="Suppression01" offset="12" size="4" />
    <Field name="RollbackFlags" offset="16" size="4" />
    <Padding offsets="20-31" size="12" />
  </StructLayout>
  <StructLayout name="DispatcherTimingDTO" sizeBytes="32">
    <Field name="PreSimMs" offset="0" size="4" />
    <Field name="SimWaitMs" offset="4" size="4" />
    <Field name="PostSimMs" offset="8" size="4" />
    <Field name="VisualSyncMs" offset="12" size="4" />
    <Field name="FrameId" offset="16" size="4" />
    <Padding offsets="20-31" size="12" />
  </StructLayout>
  <ScalabilityCurve>Below GlobalQualityWeight 0.3, dispatcher background work stays near the 0.10-0.45 ms budget and rollback/health pressure collapses presentation to one Vault suppression row plus skipped visual sync. Above that, the same continuous scalar lets presentation domains restore visual overkill without a binary device branch.</ScalabilityCurve>
  <HPhiVaultStatus privatePersistentArraysAdded="0" buffers="70620,70621,70622,70623,70624,70625,70626" />
  <PointerAliasingAndDependencyGraph consumed="registered simulation JobHandles plus rollback state buffer 70752" produced="combined simulation handle, timing ring 70623, suppression row 70626" noAlias="No new Burst jobs in Loop 11" />
  <CompileGuard directNetworkingReferenceInCore="false" directVfxAudioReferenceForSuppression="false" compileWallCriticalFindings="124" />
  <DearLie before="dispatcher-side rollback presentation simulation or per-emitter suppression loop" after="one 32-byte suppression record plus skipped VISUAL_SYNC" complexityBefore="O(visual_systems + emitters)" complexityAfter="O(1)" />
</SELF_AUDIT>
