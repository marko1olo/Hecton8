# Rationale 1810

## Decisions

1. Static prep only.
   - Reason: task default forbids Unity takeover/build/profiler unless slot and CPU/build gates pass. The requested output is a proof harness MD+CSV, not runtime capture.
   - Consequence: all runtime, visual, Play Mode, profiler, GC, Frame Debugger, Memory Profiler, and player-build claims remain PENDING UNITY SLOT.

2. Do not add or edit Unity test code.
   - Reason: existing tests already cover useful partial gates: zero-GC PlayMode sampling, survival formulas, ocean DTO/layout quality scaling, celestial math, scene/static authority violations, and QA watchdog profiler recorder paths. The missing piece is a route proof packet, not another broad test harness.
   - Consequence: report lists leverage points only and requires later Unity verifier to run or extend tests under a controlled slot.

3. Preferred proof storage under `Docs/Reports/Batch18/1810_Captures/`.
   - Reason: AGENTS.md forbids saving temporary fluff in Assets because it triggers Unity import churn. Existing `Assets/Screenshots/` files are current baseline references, but new proof captures should live with the Batch18 report packet unless a Unity tool hardcodes `Assets/Screenshots/`.
   - Consequence: `Assets/Screenshots/` is allowed only for Unity-generated captures that already land there; the verifier must copy the exact path into the report and avoid overwrites.

4. Proof IDs use `1810_*` labels, not `1806_*`.
   - Reason: 1806 already owns the action manifest labels. 1810 owns proof-harness acceptance labels and must avoid collision.
   - Consequence: CSV and report define `1810_PROOF_*`, `1810_SHOT_*`, `1810_PM_*`, `1810_PROF_*`, `1810_GC_*`, and `1810_FD_*` labels.

5. Acceptance stays three-pillar.
   - Reason: AGENTS.md and quality.md reject beautiful-empty, fast-flat, and complex-slow results.
   - Consequence: every proof row requires visual, gameplay, and performance evidence before acceptance.
