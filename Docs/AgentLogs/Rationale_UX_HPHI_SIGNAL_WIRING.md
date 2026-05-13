# UX_HPHI_SIGNAL_WIRING Rationale

## Initial Authority And Scope
Problem: Legacy UI controllers are suspected to poll singletons or registry state in `Update()`, weakening H-Phi purity and causing avoidable hot-path work.
Solution: Treat the active XML prompt and Echelon 8 Presentation & UX domain as scope, then refactor only UI-owned code or cross-domain contracts required for signal boundaries.
Rejected Alternatives: Borrowing implementation details from neighboring prompts, editing concrete player/inventory systems directly, or using scene search as a runtime transport.
Scalability potential: Low uses 15Hz dirty evaluation and char buffers; Middle/High use fuller signal cadence; Ultra can spend saved CPU on richer glyph/VR/holographic feedback without changing hot-path allocation law.
Hardware Impact: Expected low-end gain is removal of per-frame singleton/registry polling and string churn; exact i3/MX350 microseconds remain PENDING VERIFICATION until profiling.

## Mandate Selection
Problem: UI refactor touches hot text paths, registry access, input glyphs, VR placement, and telemetry.
Solution: Read UI_Data_Streaming_ZeroGC_Optimization, UI_Diegetic_Physical_Interfaces, ARCH_Global_Registry_ServiceLocator_DI_Init, OPT_Zero_GC_Policy_AllocFree_Mandate, DBG_Telemetry_Crash_Reporting_PostMortem, OPT_Performance_Budgets_FrameTime_VRAM_Limits, CTRL_Device_Abstraction_Haptics, and REND_VR_Stencil_Masking before code.
Rejected Alternatives: Generic Unity UI update patterns, `TMP_Text.text`, `FindObjectOfType`, `Quaternion.Slerp`, or Update-based polling.
Scalability potential: Toaster path is dirty-flagged, throttled, text-buffer only; high-end path may add higher-cadence diegetic/VR feedback from the same signal snapshots.
Hardware Impact: Static expectation is sub-0.1 ms UI control overhead on MX350; measured proof absent.

## Authority Review Boundary
Problem: The project has stale root mirrors and many concurrent dirty files, so a source-only scan can target the wrong authority or overwrite another agent.
Solution: Use `AGENTS.md`, `Docs/README.md`, the Echelon domain map, signal/dispatch/UI architecture docs, and the active XML tag as the authority chain; treat existing dirty UI files as shared workspace state.
Rejected Alternatives: Trusting `PROJECT_ATLAS.md` as current authority, assuming all `IUpdatable` UI is within this prompt, or rewriting broad UI subsystems without ownership proof.
Scalability potential: Low keeps scope on gameplay HUD signal plumbing; Middle/High/Ultra can expand visual richness only after the reactive path is clean and compile-proven.
Hardware Impact: The expected gain is bounded to removed polling/string churn in selected hot UI components; broad project-wide estimates remain invalid until profiler proof exists.
