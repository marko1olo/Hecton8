# AUTOFIX8 Rationale

## Decision 1: Source-Only Diagnostic Centralization

Problem: Remaining first-party runtime files still use direct Unity diagnostics. Direct `Debug.*` fragments conditional stripping policy and makes release allocation/log spam audits weaker.

Solution: Route direct diagnostics through existing `Hecton8.Core.H8Debug` overloads. Preserve exact message intent, context objects, and exception payloads.

Rejected Alternatives: Logger rewrite was rejected because the existing facade is sufficient. Removing diagnostics was rejected because failure breadcrumbs are part of blackbox/debug evidence.

Scalability potential: Low/MX350 keeps diagnostic overhead centralized and conditionally stripped. Middle/High/Ultra retain richer development breadcrumbs without changing gameplay truth.

Hardware Impact: No new normal-frame CPU/GPU/VRAM cost. Expected benefit is reduced release diagnostic risk; profiler proof remains pending.

## Decision 2: No Runtime Authority Changes

Problem: Parallel agents and a dirty worktree make broad architecture edits high-risk.

Solution: Touch only selected C# source files and AUTOFIX8 docs/logs. No YAML, scenes, prefabs, assets, project settings, packages, public signatures, DTO layouts, save identity, signal lanes, DataVault ownership, or dispatcher phases.

Rejected Alternatives: Raw YAML cleanup, project settings changes, and global-route redesign were rejected because this task has no route card or proof artifact for those moves.

Scalability potential: `GlobalQualityWeight` behavior stays unchanged across Low/Middle/High/Ultra.

Hardware Impact: Neutral outside diagnostic fault/development paths.

## Decision 3: Build Gate Obeyed

Problem: Verification needs compile proof, but project law forbids launching dotnet/Unity compile when CPU is above 50% or another compiler is active.

Solution: Run scoped static verification and `git diff --check`, then query the local gate before any build command. Gate result: CPU 84%, dotnet/csc process count 0. Build was not launched.

Rejected Alternatives: Forcing `dotnet build` was rejected because it violates AGENTS.md and would compete with active machine load. Claiming compile success without running it was rejected as fake proof.

Scalability potential: Low/Middle/High/Ultra behavior unchanged. The work only centralizes diagnostics; no quality tier, gameplay truth, or visual budget changed.

Hardware Impact: Normal gameplay frame cost remains 0us. Fault/development paths now route through the existing stripping facade; runtime compile proof remains pending until the CPU gate opens.
