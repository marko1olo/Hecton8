# COMPUTE H-PHI BUDGET GATE ATTEMPT 2026-05-17

Status: AUDIT COMPLETE
Scope: HECTON-8 only. Timaert excluded.
Evidence class: static H-Phi budget attempt + current H-Phi artifact review.
Search keywords: H-Phi; HPhi; hphi; ash-fi; ash_phi; ASh-Fi; HФ; Аш-Фи; integration-metric; architecture-integration; token-H-Phi-ROI; compute-H-Phi.

## Attempt

Timestamp: 2026-05-17T00:00+04:00 local.

Command class:

```powershell
Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json -RequireCoreBuildGate `
  -MaxCoreAsmdefDebtReferences 25 `
  -MaxGeneratedProjectDebtReferences 10 `
  -MaxSourceBackedBridgeDebtReferences 14 `
  -MaxSourceBackedCompileBridgeDebtReferences 8 `
  -MaxProjectReferenceReplacementDebtReferences 6 `
  -MaxAupPrecisionRisk 0 `
  -MaxFindObjectCalls 0 `
  -MaxLegacyEventPublish 28 `
  -MaxDuplicateSignalNames 0 `
  -MaxUnityUpdateMethods 0 `
  -MaxGlobalRegistrySurface 5060 `
  -MaxGetComponentCalls 321 `
  -MaxNativeArrayRefs 7074 `
  -MaxLinqSurface 3 `
  -MaxCoroutineSurface 0 `
  -MaxManagedFormatSurface 534 `
  -MaxJobCompleteSurface 58 `
  -MaxPrimaryManagedRuntimeRisk 147 `
  -MaxOwnerBlockedNativeArrayRefs 6262 `
  -MaxPrimaryOwnerBlockedNativeArrayRefs 5678 `
  -MinDataSovereignty 0.021306 `
  -MinMemoryAlignment 0.506309 `
  -MinRuntimeHPhiRisk 0.000636
```

Result: timed out after 244 seconds. No `COMPUTE_HPHI_BUDGET_GATE_*.json` artifact was produced.

## Honest Verdict

There is no fresh budget-gate `EXIT=0` or `EXIT=1` artifact from this attempt. Do not cite it as a completed gate run.

Use the current completed artifact instead:

`Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_CURRENT_20260516_171857.json`

From that artifact, old baseline gate status is inferable:

| Gate | Current | Old limit | Pass |
|---|---:|---:|---:|
| GlobalRegistry surface max | 5,291 | 5,060 | no |
| NativeArray refs max | 7,299 | 7,074 | no |
| ManagedFormat surface max | 535 | 534 | no |
| JobComplete surface max | 73 | 58 | no |
| PrimaryManagedRuntimeRisk max | 148 | 147 | no |
| DataSovereignty min | 0.114950891 | 0.021306 | yes |
| MemoryAlignment min | 0.528974740 | 0.506309 | yes |
| RuntimeHPhiRisk min | 0.004164939 | 0.000636 | yes |

Conclusion: H-Phi score improved. Strict old absolute budgets are not clean. A completed budget run is still pending because the full gate timed out interactively.

STATUS: AUDIT COMPLETE.
