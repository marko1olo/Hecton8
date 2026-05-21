# DEBUG.LOG DELETION QUEUE â€” Unsanitized First-Party Runtime Logs
Date: 2026-05-07
Status: PENDING VERIFICATION
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only as static documentation/source orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current verification artifacts, and the latest DOC_GLOBAL reports.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->



> **Status:** ETA SANITIZED
> **Mandates Followed:** AGENTS.md Â§ Debug Log Hygiene â€” "Guard: #if UNITY_EDITOR || DEVELOPMENT_BUILD OR [System.Diagnostics.Conditional]"

---

## 1. CRITERIA

Queued items meet **ALL** of the following:
- Located under `Assets/_Project/Scripts/` (first-party runtime)
- Call `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` directly
- **NOT** wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`
- **NOT** inside an `Editor/` folder
- **NOT** a one-time critical init error (AGENTS.md exception)

Smoke-test utilities (`*SmokeTester.cs`) are noted but deprioritized because they are test-only MonoBehaviours and are typically stripped from release builds by assembly definition or scene exclusion.

---

## 2. DELETION QUEUE

### ðŸ”´ HIGH PRIORITY â€” Runtime systems that may spam

| # | File | Method | Line | Violation | Fix Strategy |
|---|------|--------|------|-----------|--------------|
| 1 | `AcousticZoneController.cs` | `LogDiagnostic(string message)` | 1207 | `Debug.Log(message, this)` without guard | Wrap in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` or delete if diagnostic is dead code |
| 2 | `AcousticZoneController.cs` | `LogOnceWarning(ref bool, string)` | 2748 | `Debug.LogWarning(message, this)` without guard | Wrap in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` |
| 3 | `AtlasSignalDecoder.cs` | `LogSignalFullyDecoded()` | 251 | `Debug.Log("[AtlasDecoder] Signal fully decoded...")` | Wrap or delete; signal decode is rare, but still allocates string |
| 4 | `AtlasSignalSystem.cs` | `LogSignalFirstDetected(float)` | 434 | `Debug.Log($"[AtlasSignal] Signal first detected...")` | Wrap or replace with event-only telemetry |
| 5 | `AtlasSignalSystem.cs` | `LogSignalDecoded(string)` | 450 | `Debug.Log($"[AtlasSignal] Signal decoded: {messageId}")` | Wrap or replace with event-only telemetry |
| 6 | `AtlasSignalSystem.cs` | `LogRevealStageUnlocked(int,float)` | 456 | `Debug.Log($"[AtlasSignal] Reveal stage...")` | Wrap or replace with event-only telemetry |
| 7 | `AudioLogSystem.cs` | `LogPlaybackCompleted(string)` | 291 | `Debug.Log($"[AudioLog] Playback completed: {completedId}")` | Wrap in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` |
| 8 | `AudioLogSystem.cs` | `LogLoadedCount(int)` | 309 | `Debug.Log($"[AudioLog] Loaded {discoveredCount} discovered logs.")` | Wrap in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` |
| 9 | `Atlas6DirectiveSystem.cs` | `LogPlayerStatus(Atlas6PlayerStatus)` | 360 | `Debug.Log($"[Atlas6] Player status: {newStatus}")` | Wrap in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` |
| 10 | `BeaconNetworkSystem.cs` | *(inline)* | 286 | `Debug.Log($"[BeaconNetwork] Deployed {label} at {position}")` gated by `verboseLogging` | Guard the entire block with `#if UNITY_EDITOR || DEVELOPMENT_BUILD` so `verboseLogging` bool does not leak string alloc in release |

### ðŸŸ¡ LOW PRIORITY â€” Smoke testers (test-only, likely stripped)

| # | File | Note |
|---|------|------|
| 11 | `BarterRuntimeSmokeTester.cs` | Debug.Log / LogWarning inside coroutine smoke test |
| 12 | `BuilderRuntimeSmokeTester.cs` | Debug.Log / LogWarning inside coroutine smoke test |
| 13 | `FabricationRuntimeSmokeTester.cs` | Debug.LogError inside coroutine smoke test |
| 14 | `FieldToolRuntimeSmokeTester.cs` | Debug.LogWarning inside coroutine smoke test |

> Smoke testers are **not runtime gameplay systems**. They should be moved to an `Editor/` or `Tests/` assembly, or wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` as a belt-and-suspenders measure.

---

## 3. COMPLIANT EXAMPLES (DO NOT TOUCH)

These files already follow the mandate and serve as the reference pattern:

```csharp
// CORRECT PATTERN
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.LogWarning("[System] Warning message");
#endif
```

- `GameBootstrapper.cs:312` â€” `Debug.LogException` inside guard
- `SystemDispatcher.cs` â€” all logs inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`
- `GlobalRegistry.cs` â€” all logs inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`
- `GameTickManager.cs` â€” all logs inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`
- `BootstrapController.cs:515` â€” `LogWarning` inside guard
- `HectonMusicDirector.cs:716` â€” `LogError` inside guard
- `AudioLogPickup.cs:123` â€” `LogWarning` inside guard
- `PlayerCriticalProceduralAudioRenderer.cs` â€” warnings inside guard + one-shot bool gates
- `FaunaBrain.cs:466` â€” watchdog log throttled to 5 s + inside guard

---

## 4. QUICK-FIX PATCH TEMPLATE

Apply to every queued method:

```csharp
private static void LogSignalFirstDetected(float strength)
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    Debug.Log($"[AtlasSignal] Signal first detected. Strength: {strength:F2}");
#endif
}
```

For `AcousticZoneController.LogDiagnostic`, if the method is dead code, delete the method body entirely rather than guarding it.

---

*Queue generated by ARCHIVARIUS. Re-audit after each sprint merge.*
