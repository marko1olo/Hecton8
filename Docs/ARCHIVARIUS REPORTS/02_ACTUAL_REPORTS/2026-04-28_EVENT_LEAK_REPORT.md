# EVENT_LEAK_REPORT.md

**Date:** 2026-04-29  
**Status:** PENDING VERIFICATION  
**Scope:** current `HectonEventBus.Subscribe<T>()` ownership and unsubscribe hygiene in selected runtime subscribers under `Assets/_Project/Scripts/`

**Mandates Followed:** `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

---

## Method

- Re-read the current `HectonEventBus` implementation.
- Re-checked live subscribers named in the older report against their present `OnDisable` / `OnDestroy` / local unsubscribe methods.
- Downgraded any claim not supported by current source.

---

## Current Bus Shape

- `HectonEventBus` is a typed static bus in `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs`.
- Each subscription returns a disposable `HectonEventSubscription`.
- Channels are backed by managed `List<SubscriptionEntry>` containers, not by `NativeQueue<T>` or static zero-alloc event queues.
- Leak safety therefore depends on explicit token disposal by every subscriber owner.

---

## Rechecked Subscribers

| File | Current state | Evidence summary |
|---|---|---|
| `Meta/GlobalProfileManager.cs` | `LOW RISK` | `OnDisable` and `OnDestroy` call `UnsubscribeFromEventBus()`; all tracked tokens are disposed and nulled |
| `Meta/RunModifierController.cs` | `LOW RISK` | `OnDisable` / `OnDestroy` call `UnsubscribeFromEventBus()`; both tokens are disposed and nulled |
| `Meta/DynamicDifficultyDirector.cs` | `LOW RISK` | explicit `UnsubscribeFromEventBus()` path on disable/destroy |
| `PDA/PDALogbookManager.cs` | `LOW RISK` | explicit `UnsubscribeFromOwners()` and `UnsubscribeFromEventBus()` on disable/destroy |
| `Progression/PDAContextualAdvisorySystem.cs` | `LOW RISK` | explicit `UnsubscribeFromEventBus()` on disable/destroy |
| `Progression/PlayerAchievementRegistry.cs` | `LOW RISK` | explicit `UnsubscribeFromEventBus()` on disable/destroy |
| `World/EnvironmentalStrainManager.cs` | `LOW RISK` | all three tokens disposed and nulled in both `OnDisable` and `OnDestroy` |
| `Quest/QuestManager.cs` | `NO CURRENT HectonEventBus SUBSCRIPTIONS CONFIRMED` | older report referenced bus risk here, but current file does not expose matching `HectonEventBus.Subscribe<T>()` evidence in this pass |

---

## Invalid Claims Removed From Prior Version

- `GlobalProfileManager.cs` is not currently a confirmed leak site.
- `RunModifierController.cs` is not currently a confirmed leak site.
- `EnvironmentalStrainManager.cs` is not currently an unverified leak site.
- `QuestManager.cs` was listed in the old table without present matching subscription evidence in this pass.

---

## Remaining Risks

- This is static source verification only. No pooled-despawn replay or scene-teardown runtime trace was executed.
- `HectonEventBus` remains managed-list based. That is architecture debt relative to the project event-bus mandate, even where explicit disposal is present.
- Any subscriber not re-read in this pass remains outside this document's proof boundary.

---

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | None. Documentation-only rewrite. |
| GC | None. Documentation-only rewrite. |
| Memory | None. Documentation-only rewrite. |
| Cadence | None. Runtime code unchanged. |
| Correctness | Improved documentation accuracy by removing false leak accusations. |

---

## Verdict

No confirmed high-risk `HectonEventBus` leak remains in the specific subscribers re-read in this pass.  
Runtime teardown behavior is still `PENDING VERIFICATION`.
