# StashAudit_GIT_CONFLICT_RESOLUTION_20260515

Evidence class: STATIC_GIT
Date: 2026-05-15
Branch: main

## Current Sync Gate

- `git fetch origin --prune`: no incoming output.
- `git rev-list --left-right --count origin/main...HEAD`: `0 0`.
- Worktree before this audit held only untracked documentation/archive files:
  - `Docs/AgentLogs/LOG_SUBNAUTICA_RESEARCHER.md`
  - `Docs/AgentLogs/Rationale_SUBNAUTICA_RESEARCHER.md`
  - `Docs/Tasks/Status_SUBNAUTICA_RESEARCHER.md`
  - `Docs/Archive/Batch006/GEMINI OTCHETY.txt`

## Local Stashes

### `stash@{0}`

Name: `On main: codex/git-conflict-resolution backup before reconciliation 2026-05-14`

Base commit: `c68c201b7 Add hardware profile catalog static guard`

Stat with untracked files:
- 76 files changed.
- 681 insertions.
- 176 deletions.
- Includes runtime files such as `HectonSurfaceWeatherDirector.cs`, `ContextualPhysicalIkRig.cs`, `ContextualPhysicalIkRuntime.cs`, `HectonFluidEngine.cs`, `HectonPlayerMovement.cs`, `HectonVoxelVolume.cs`, `LaserCutter.cs`, `SaveManager.cs`, `SubmarineFluidDynamics.cs`, `MarauderOutpostGenerationService.cs`, and `VegetationFlowFieldIntegrator.cs`.
- Includes old active Git integration memory files and `Tools/Architecture/HectonPhiAudit.ps1` as untracked stash payload.

Apply check:
- Command: `git stash show -p 'stash@{0}' | git apply --check --3way`
- Result: `EXIT=1`.
- Failure class: stale patch against current `HEAD`; Git cannot perform the 3-way merge for missing blobs, then direct application fails across runtime and docs hunks.
- First failing files: `HectonSurfaceWeatherDirector.cs`, `ContextualPhysicalIkRig.cs`, `ContextualPhysicalIkRuntime.cs`, `HectonFluidEngine.cs`, `HectonPlayerMovement.cs`, `HectonVoxelVolume.cs`, `LaserCutter.cs`, `ModCommandDispatcher.cs`, `AwaitableDropSequenceDirector.cs`, `SaveManager.cs`.

Decision:
- Do not `stash pop` or `stash apply` onto current `main`.
- Treat as old local forensic backup from before reconciliation, not as pending clean work.

### `stash@{1}`

Name: `On main: !!GitHub_Desktop<main>`

Base commit: `edf70db65 Merge branch 'main' of https://github.com/marko1olo/Hecton8`

Stat:
- 196 files changed.
- 5992 insertions.
- 918 deletions.
- Broad mixed runtime/docs/evidence payload from the original GitHub Desktop interruption window.

Apply check:
- Command: `git stash show -p 'stash@{1}' | git apply --check --3way`
- Result: `EXIT=128`.
- Failure class: patch stream cannot be checked cleanly because Git reports `git diff header lacks filename information when removing 1 leading pathname component (line 7208)`.

Decision:
- Do not `stash pop` or `stash apply` onto current `main`.
- Treat as old GitHub Desktop safety stash. It is local-only and not part of the pushed branch.

## Operator Decision

The current pushed branch is synchronized with `origin/main`. The old stashes are not cleanly applicable and would risk reintroducing stale runtime/doc changes. They are retained as local backup evidence unless explicitly deleted by the operator/user. They do not block commit/push of the current clean documentation checkpoint.
