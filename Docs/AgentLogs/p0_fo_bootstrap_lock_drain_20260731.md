# P0 FO bootstrap-lock drain — architect note (2026-07-30 21:26 UTC)

## Blunt answers
- **Least confident:** exact lock holder after GameReady on the failed asmfix run (physics pause vs pending scenes vs stuck scene-rebase barrier) — external BATCH_TIMEOUT killed Unity before runner BOOTSTRAP_TIMEOUT diag could print. New 15s wait-progress lines close that gap.
- **Biggest miss:** armed ecology wait clock ≠ ecology ready. GameReady short-circuit succeeded; FrostTick starve under `IsOriginShiftBootstrapLocked` kept `TryMarkEcologyReady` from ever observing `EcosystemDirector.IsInitialized`.
- **Don't realize:** headless DoD PASS is still **DECLINED** as gameplay proof. Real-game screenshots required. V0 Swim FAIL. `Docs/Screenshots/V0_Playtest` empty. Features without gameplay = DECLINED (KCC, Debris, Geology@2048 headless-only, RuntimeSmokeTester, README art).
- **Implemented but not integrated to gameplay:** ecology clock arm@GameReady + FO flush-while-wait (df139d50f); FO lock-drain under physics pause (this commit); headless asmdef Bootstrap.Contracts + IVT. None of these are a swim/play loop screenshot.

## Root cause (code)
1. `QueuePendingLoadedScene` → `AcquireSceneRebaseTickLock` → `SystemDispatcher.RequestOriginShiftBootstrapLock`.
2. `ProcessPendingSceneSynchronization` / `TryFlushInitialSceneRebaseBeforeTicks` early-returned on `_physicsPauseActive`.
3. `FO.Tick` is the only normal `ResumePhysicsAfterShift` driver and is skipped while bootstrap lock held (`SystemDispatcher` ~5246).
4. Soft-deadlock: lock never releases → no Frost/Slow → ecology never marked ready → external batch watchdog writes sparse `BATCH_TIMEOUT` stub.

## Fix (no mocks, no timeout bumps)
- `ProcessPendingSceneSynchronization`: block only on `_isShiftInProgress`.
- `TryPrepareShiftTargets`: same — cache rebuild legal under physics pause.
- `TryFlushInitialSceneRebaseBeforeTicks`: drive physics resume frame-gate; drain pending; if barrier stuck with empty pending, `CompleteSceneRebaseBarrier` + resume.
- `CopyBootstrapDrainSnapshot` + runner 15s wait progress / richer BOOTSTRAP_TIMEOUT diag.

## DoD
`status` ∉ {ECOLOGY_UNAVAILABLE, BATCH_TIMEOUT, BOOTSTRAP_TIMEOUT}; `ecologySampledDays>0`; `timeDilationDelivered>0`; no error CS.
Prior FAIL: `headless_smoke_20260731_p0_ecology_clock_asmfix.log`, result JSON BATCH_TIMEOUT stub.

## Standing DECLINED
Headless ecology unproved until DoD; Geology@2048 headless-only; KCC FAIL 0x42; Debris EXEMPT; RuntimeSmokeTester; README art; real-game screenshots missing.
