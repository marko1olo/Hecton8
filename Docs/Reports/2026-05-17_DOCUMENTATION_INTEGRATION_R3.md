<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# Documentation Integration R3
Date: 2026-05-17
Status: STATIC_DOC_INTEGRATION / RUNTIME PENDING
Agent: DOC_GLOBAL_DOCS_REFRESH
Evidence Class: STATIC_DOC / FILESYSTEM / GIT_CLI

## Purpose
R3 follows the repeated user directive to continue updating documentation after the global refresh and concurrent-delta ledger. This pass converts the root compute drift and missing architecture/report navigation into stable documentation state.

## Actions
- Moved `COMPUTE_AUDIT_BRIEF.md` from repository root to `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_AUDIT_BRIEF.md`.
- Updated `Docs/DOC_GOVERNANCE.md` and `Docs/ROOT_DOCS_REFERENCE.md` so the root documentation target is again exactly three markdown anchors.
- Updated `Docs/ARCHITECTURE/README.md` so every current `Docs/ARCHITECTURE/*.md` contract is indexed.
- Updated `Docs/README.md` and `Docs/Reports/README.md` with the 2026-05-17 global refresh, concurrent ledger, Subnautica actuality pass, actuality manifest, mod ecosystem report, and moved compute brief.
- Updated compute report references so the active 2026-05-16 compute brief points to the report bundle instead of repo root.

## Verification Snapshot
- Root markdown files: `3` (`AGENTS.md`, `BUILD_PLAYTEST_ISSUES.md`, `MASTER_RELEASE_WORK_PLAN.md`).
- Documentation files changed or untracked before staging: `72`.
- Tracked changed docs: `55`.
- Untracked docs: `17`.
- Stable active `.md` / `.txt` docs scanned for headers: `150`.
- Stable active `.md` / `.txt` header failures: `0`.
- Changed/untracked JSON docs parsed: `2`.
- Changed/untracked JSON parse failures: `0`.
- Architecture markdown contracts: `46`.
- Architecture contracts missing from `Docs/ARCHITECTURE/README.md`: `0`.
- Dirty source/shader files intentionally outside this documentation pass: `8`.

## Dirty Source Boundary
- `Assets/_Project/Art/Shaders/Hecton_FluidAdvection.compute`
- `Assets/_Project/Art/Shaders/Hecton_MarineSnow.compute`
- `Assets/_Project/Scripts/Core/CameraJuiceSignals.cs`
- `Assets/_Project/Scripts/Core/IPlatformIntegration.cs`
- `Assets/_Project/Scripts/HectonBoidController.cs`
- `Assets/_Project/Scripts/HectonFluidEngine.cs`
- `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs`
- `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs`

## Runtime Boundary
No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual route proof was collected in R3. This is documentation/filesystem/git evidence only.
