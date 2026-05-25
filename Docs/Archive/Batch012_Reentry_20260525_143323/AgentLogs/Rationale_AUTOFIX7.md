# AUTOFIX7 Rationale

## Decision 1: Continue Central Diagnostic Route Cleanup

Problem: Runtime C# still contains direct Unity `Debug.*` calls outside the central `H8Debug` facade. This fragments release stripping policy and hides potential diagnostic string allocation paths.

Solution: Replace direct `Debug.Log`, `Debug.LogWarning`, `Debug.LogError`, and `Debug.LogException` call sites in a new source-only slice with `Hecton8.Core.H8Debug` equivalents. Preserve diagnostic content and context arguments.

Rejected Alternatives: Rewriting logger architecture was rejected because `H8Debug` already exists and has the required overloads. Removing logs was rejected because critical failure breadcrumbs are part of crash/debug evidence.

Scalability potential: Low/MX350 keeps diagnostics centralized and conditionally stripped. Middle/High/Ultra retain richer development diagnostics without changing gameplay truth.

Hardware Impact: No normal-frame CPU/GPU/VRAM work added. Expected benefit is lower release-path diagnostic risk; profiler proof remains pending.

## Decision 2: Avoid YAML, Settings, Public API, and Ownership Changes

Problem: The worktree is active and parallel agents may be editing adjacent systems. Broad refactors would increase integration risk.

Solution: Touch only selected C# source files and AUTOFIX7 docs/logs. Do not mutate prefabs, scenes, assets, packages, project settings, public signatures, signal lanes, DataVault ownership, save identity, or dispatcher phase assignments.

Rejected Alternatives: Raw project cleanup, prefab mutation, and route redesign were rejected because this task has no specific proof artifact or route card for those changes.

Scalability potential: No tier behavior changed. This preserves continuous `GlobalQualityWeight` semantics.

Hardware Impact: Neutral; source-level diagnostic hygiene only.

## Decision 3: Build Gate Obeyed

Problem: Compile/build verification is required where possible, but AGENTS forbids launching dotnet build when CPU is above 50% or `dotnet`/`csc` is already running. Current state: CPU=93, csc process 56240 active, dotnet process 50252 active.

Solution: Do not start another build. Use available static gates: scoped direct-debug scan and `git diff --check`. Mark compile/runtime proof as blocked/pending, not green.

Rejected Alternatives: Forcing a build was rejected because it violates the project build gate and can interfere with active compiler work by another process.

Scalability potential: No tier behavior changed. Low/Middle/High/Ultra retain identical runtime truth; diagnostics are centralized under existing conditional facade.

Hardware Impact: No measured runtime delta. Expected normal-frame cost remains 0us; fault/development diagnostics are now routed consistently.
