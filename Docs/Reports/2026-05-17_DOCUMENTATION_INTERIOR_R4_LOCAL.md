# Documentation Interior R4 Local
Date: 2026-05-17
Status: LOCAL_ONLY_STATIC_DOC_INTERIOR_PASS / RUNTIME PENDING
Agent: DOC_GLOBAL_DOCS_REFRESH
Evidence Class: STATIC_DOC / FILESYSTEM / GIT_WORKTREE
GitHub: intentionally not used in R4 per user order

## Purpose
R4 updates the inside of active stable documentation, not only navigation indexes. It adds a local, machine-findable actuality boundary to every active stable `.md` / `.txt` document so stale historical counters and old version claims are explicitly subordinate to the current authority spine.

## Scope
- Included: active stable `.md` / `.txt` docs under `Docs/`, including architecture, design, modding, lore, flora, fauna, scatter, space-engine research, and Archivarius `01_GENERAL_INFO`.
- Excluded from mutation: dated reports, archives, active AgentLogs, active Tasks, deprecated folders, generated JSON/config artifacts, generated `obj` / `bin` build outputs, and repository-root authority anchors.
- Repository-root authority anchors remain unchanged: `AGENTS.md`, `BUILD_PLAYTEST_ISSUES.md`, `MASTER_RELEASE_WORK_PLAN.md`.

## R4 Interior Boundary Inserted
Each included document now contains `DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START` / `END` markers immediately after its `Status:` header. The block states that the document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

It also states that no Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless a fresh evidence artifact is linked.

## Verification Snapshot
- Active stable `.md` / `.txt` docs in R4 scope: `150` after excluding generated `obj` / `bin` file lists.
- Active-scope files with R4 interior boundary marker: `150 / 150`.
- Active-scope duplicate R4 interior boundaries: `0`.
- Whole-`Docs` `.md` / `.txt` files containing the marker text: `153` (`150` active docs plus this report, the task status, and the agent log as evidence references).
- Stable active `.md` / `.txt` header failures after R4: `0`.
- Root markdown files: `3` (`AGENTS.md`, `BUILD_PLAYTEST_ISSUES.md`, `MASTER_RELEASE_WORK_PLAN.md`).
- Dirty source/shader files outside documentation scope: `8`.
- Scoped whitespace check: `git diff --check -- Docs ':!Docs/Tasks/CURRENT_BATCH.md'` returned exit `0`.
- Full `git diff --check -- Docs` returned exit `1` only because `Docs/Tasks/CURRENT_BATCH.md` has trailing whitespace on lines `122`, `177`, and `232`; that file is a batch prompt/evidence stream and was not normalized in R4.

## Dirty Source Boundary
- `Assets/_Project/Art/Shaders/Hecton_FluidAdvection.compute`
- `Assets/_Project/Art/Shaders/Hecton_MarineSnow.compute`
- `Assets/_Project/Scripts/Core/CameraJuiceSignals.cs`
- `Assets/_Project/Scripts/Core/IPlatformIntegration.cs`
- `Assets/_Project/Scripts/HectonBoidController.cs`
- `Assets/_Project/Scripts/HectonFluidEngine.cs`
- `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs`
- `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs`

## Result
All active stable `.md` / `.txt` documents in the R4 scope now carry an internal actuality boundary. This is a local-only documentation update and is not committed or pushed by design.
