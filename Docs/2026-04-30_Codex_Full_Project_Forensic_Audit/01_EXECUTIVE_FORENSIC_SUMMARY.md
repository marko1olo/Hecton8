# Executive Forensic Summary

Date: 2026-05-07
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-12 Current Status Override

HECTON-8 has moved from MMF documentation claims to FileStream/native-window source truth for save I/O.

Code truth:

| Surface | Current Source Truth |
|---|---|
| Save persistence | `SaveBinaryStorage.AsyncWriteManager` uses `FileStream`, cached native read windows, throttled flush, and portable sequential/random access flags |
| MMF | no first-party `MemoryMappedFile` source hit was found in the current save path |
| Data Monolith | `.h8bin` runtime load still uses boot-only `File.ReadAllBytes` staging before native blit |
| Event bus | `GlobalSignals.cs` is no longer the old prose-only five-artery model; R21 static scan sees `73` direct native queue slots, `133` typed `SignalBus<T>` lanes, and a `DebugSignal` lane |
| Registry | `GlobalRegistry` remains the service locator; singleton self-sovereignty is still architectural debt |

This audit is documentation-verified, not runtime-certified. Unity import, Console, Play Mode, profiler, GCMonitor, and player-build proof remain `PENDING VERIFICATION`.

## Bottom Line

HECTON-8 is not fake. There is real engineering depth, real subsystem ownership work, real native memory work, real Jobs/Burst adoption, real HUD optimization work, and real custom platform intent.

It is also not stable enough to be called production-clean.

Current state:

| Axis | Status |
|---|---|
| subsystem count | high |
| native/Burst adoption | real but uneven |
| ownership discipline | split authority and direct-reference debt |
| compile confidence | blocked by unresolved external symbol families |
| documentation trust | must be source-verified before use |

The strongest reality is not polish. It is subsystem ambition.
The weakest reality is not visuals. It is ownership discipline and integration hygiene.

## Audit Scores

These are evidence-based readiness estimates, not marketing numbers.

| Area | Score | Verdict |
|---|---:|---|
| Engineering depth | 78% | Real implementation exists in many critical systems |
| Shipping readiness | 34% | Too many active contradictions and compile/editor risks |
| Architecture discipline | 41% | Registry vision exists, but authority is split and violated |
| Zero-GC compliance | 57% | Strong intent, uneven enforcement |
| Jobs/Burst effectiveness | 64% | Real usage, reduced by barrier abuse and monolith coupling |
| Documentation accuracy | 63% | Useful, but partially stale versus current code/editor truth |
| Player-facing cohesion | 52% | Strong atmosphere potential, unstable systemic confidence |
| Test maturity | 8% | Practically absent for first-party runtime |

## What Is Actually Good

- `GlobalRegistry` is real and broad. This is not a fake service locator note in a document. It is actively used across the codebase.
- `SystemDispatcher` is real and acts as a genuine cadence owner for update lanes and deferred event flushing.
- `SaveManager` is serious, custom, and substantially beyond prototype quality in intent and surface.
- `PlayerCriticalProceduralAudioRenderer` shows real DSP/native/job-minded engineering, not placeholder audio scripting.
- `SuitHUDV4CanvasOverlay` contains visible zero-GC HUD discipline, including char-buffer formatting instead of naive string churn.
- The project has large-scale native collection and Burst adoption across world, fluid, fauna, and procedural systems.

## What Is Actually Bad

- Current editor/runtime verification state is not trustworthy enough for clean operational confidence.
- The architecture claims a disciplined registry/bootstrap regime, but the codebase still contains heavy singleton, `Instance`, and `DontDestroyOnLoad` residue.
- Bootstrap authority is split between multiple owners. That is not a style issue. That is a failure surface.
- Several owner files are so large that they are now integration liabilities, not strength indicators.
- DOTS/Entities is mostly a seam and a promise, not a live production backend.
- The active world scene contains debug, authoring, runtime, experimental, and temporary residue in one place.
- Test evidence is functionally non-existent for the actual game.
- Fresh reverification exposed editor/log instability and `SetResource` error flood as the current live blocker surface.

## Real Versus Paper

Real:
- custom save architecture
- dispatcher/registry runtime backbone
- zero-GC HUD work
- procedural world ambition
- native/Burst-heavy compute surfaces
- custom audio subsystem depth

Mostly paper or partial reality:
- DOTS production backend
- clean single-owner bootstrap authority
- fully enforced zero-GC policy
- fully normalized service locator architecture
- robust automated test confidence
- doc-to-code consistency

## Auditor Verdict

This project is beyond prototype.
This project is not yet production-safe.

The main risk is not that nothing exists.
The main risk is that too much exists without enough ownership compression, verification, and cleanup.

## Player Verdict

Player-facing risk table:

| Surface | Current Evidence |
|---|---|
| atmosphere/system density | source shows large custom runtime surfaces |
| consistency | ownership drift and compile blockers remain |
| pacing | not runtime-verified in Play Mode |
| systemic collision risk | old singleton/direct-reference paths coexist with registry/signals |
| polish | cannot be certified without fresh Unity import, Console, profiler, and playtest logs |

## Regression Model

CPU:
- High risk in monolith world/player owners and barrier-heavy job patterns.

GC:
- Mixed risk. There is real zero-GC work, but project-wide enforcement is incomplete.

Memory:
- High risk due to large runtime surfaces, scene clutter, and editor-state render texture pressure.

Cadence:
- High risk because runtime authority is split and some systems still bypass the stated architecture.

Correctness:
- Immediate risk because editor truth, console truth, log truth, and document truth do not stay aligned reliably over time.

Why kept:
- Because the project contains enough real engineering value that cleanup is cheaper than replacement.

Why rejected:
- Any narrative that the project is already disciplined, unified, or near-shipping would be false.
