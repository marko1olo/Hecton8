# Scatter Phase 1 Baseline Checklist

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

Verification: `PENDING VERIFICATION`
Scope: `WorldProceduralScatterDirector` baseline before any DOTS decision

This checklist exists to close `Phase 1 - Establish a Real Baseline` from `HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`.

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical data boundary snapshot: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Current actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`.
- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Visual-realistic-fake doctrine snapshot: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`; re-check `.agents-skills` for newer mandates before implementation.
- May 14 DOC_AUDIT override: the cited May 11 compile artifact is absent from the current filesystem; treat that May 11 compile-success line as stale report text. R43 rechecked the current external root `Hecton8*.csproj` no-restore CLI compile surface at `0 Warning(s)` / `0 Error(s)` after restore assets and referenced `Temp\bin\Debug` DLLs exist; full restore graphs still carry vendor/package warnings, and shared `Temp\obj` locks can create transient evidence noise. Runtime, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, import, scene wiring, and visual quality remain `PENDING VERIFICATION`.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
2026-05-04 current-state boundary:

- This checklist defines required evidence, not current evidence.
- Do not treat unchecked rows or older capture instructions as proof that scatter is healthy.
- Current documentation/source orientation starts at `Docs/Reports/2026-05-18_DOCUMENTATION_R15_NAVIGATION_SUPERSESSION_R16_LOCAL.md`, then `Docs/Reports/2026-05-18_DOCUMENTATION_ACTIVE_ENTRYPOINT_NAVIGATION_R15_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_BATCH008_BINARY_HYGIENE_R14_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_GENERIC_REPORT_BOUNDARIES_R13_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_ACTIVE_REMAINDER_R11_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_LONGTAIL_INTERIOR_R10_LOCAL.md`, and `Docs/Reports/2026-05-18_DOCUMENTATION_EVIDENCE_LANGUAGE_AND_COUNTERS_R9_LOCAL.md`; the May 6 / May 4 / May 1 reports are historical context only.

Do not skip steps.
Do not report "BEFORE: N/A".
Do not claim optimization without captures.

---

## 1. Preconditions

- Use the current scatter-refactor branch state.
- Keep scatter backend execution mode at `Disabled` or `Shadow`.
- Do not enable live DOTS ownership.
- Make sure Unity compile/import is clean before entering play mode.
- Test in the real scene flow: `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`.

If global compile is blocked by unrelated changes, do not fake baseline numbers.

---

## 2. Required Runtime Tools

Use the existing first-party tools already in the project:

- `Assets/_Project/Scripts/RuntimePerformanceProfiler.cs`
- `Assets/_Project/Scripts/Tools/VerificationRuntimeProbe.cs`
- `Assets/_Project/Scripts/Dev/ShellVerificationRuntimeSmokeTester.cs`

Required profiler counters from `RuntimePerformanceProfiler`:

- frame time
- main thread
- GC allocated in frame
- system / total used memory
- SetPass calls
- batches

Required scatter stage markers already in code:

- `WorldScatter.Rebuild.Dispatch`
- `WorldScatter.Sampling.Begin`
- `WorldScatter.Sampling.BuildInputs`
- `WorldScatter.Sampling.Schedule`
- `WorldScatter.Processing.Total`
- `WorldScatter.Processing.Cells`
- `WorldScatter.Processing.Rescue`
- `WorldScatter.Processing.Restore`
- `WorldScatter.Reconcile.Total`
- `WorldScatter.Reconcile.Cleanup`
- `WorldScatter.Reconcile.Spawn`
- `WorldScatter.Reconcile.Fauna`
- `WorldScatter.Backend.Shadow.Schedule`
- `WorldScatter.Backend.Shadow.Pump`

---

## 3. Capture Passes

Capture all passes on the same branch state.

### Pass A - Boot Baseline

- Start from `00_BOOTSTRAP`.
- Record until `02_HECTON_WORLD` is fully ready.
- Capture startup memory before player movement begins.
- Save Console output for compile/import/runtime warnings.

Required outputs:

- startup frame spike
- startup main-thread spike
- startup memory snapshot
- whether scatter bootstrap prewarm ran

### Pass B - Idle World Baseline

- Stand still in `02_HECTON_WORLD`.
- Let the scene settle for at least 10 seconds.
- Run one profiler window with no deliberate movement.

Required outputs:

- mean frame time
- mean main-thread time
- GC/frame
- system memory
- SetPass
- batches
- scatter rebuild cadence while idle

### Pass C - Scatter Stress Baseline

- Move through an area known to trigger scatter refresh/rebuild pressure.
- Keep the path repeatable.
- Capture at least one heavy rebuild window.

Required outputs:

- total scatter rebuild time
- sampling time
- rescue time
- restore time
- reconcile total
- reconcile cleanup
- reconcile spawn
- reconcile fauna
- backend shadow schedule/pump cost if shadow is enabled

### Pass D - Shell / Bootstrap Safety

- Use the existing shell verification flow if available.
- Confirm menu -> world -> pause -> return/menu routes do not regress while profiler tooling is active.

Required outputs:

- world handoff still succeeds
- pause recovery still succeeds
- no bootstrap/save race appears

---

## 4. Minimum Evidence Package

For Phase 1 to count as done, attach all of this:

- Unity Console proof of clean compile/import state
- profiler screenshots or capture files for Pass A/B/C
- runtime profiler report text for frame/main-thread/GC/memory/SetPass/batches
- scatter rebuild trace or log with per-stage timings
- exact scene and route used for the capture

Without this package, Phase 1 stays `PENDING VERIFICATION`.

---

## 5. Result Format

Use this exact structure in reports:

`BEFORE`

- frame time:
- main thread:
- GC/frame:
- system memory:
- SetPass:
- batches:
- scatter total:
- sampling:
- rescue:
- restore:
- reconcile:
- cleanup:
- spawn:
- fauna:

`REGRESSION MODEL`

- CPU:
- GC:
- memory:
- cadence:
- correctness:

`HOT PATH IMPACT`

- what moved:
- what did not move:

`FAILURE MODES`

- bootstrap:
- scatter cadence:
- suppression/restore:
- pool reuse:
- shell/menu/world transitions:

Do not replace missing numbers with guesses.

---

## 6. Decision Gate After Baseline

Only after this checklist is complete:

1. decide whether Phase 2 cleanup is sufficient for runtime verification
2. compare scatter bottleneck against the rest of the frame
3. decide whether DOTS stays in scope for `Phase 3`

If scatter is no longer a major measured bottleneck, do not expand DOTS.
