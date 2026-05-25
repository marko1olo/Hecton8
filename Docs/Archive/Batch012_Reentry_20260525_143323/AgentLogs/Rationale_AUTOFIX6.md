# AUTOFIX6 Rationale

## Decision 1: Continue Direct Unity Diagnostics Cleanup

Problem: First-party runtime files still contain direct `Debug.Log*` calls. AGENTS mandates guarded diagnostics and zero-GC hot-path discipline; raw Unity diagnostics create inconsistent release behavior and make log spam harder to audit.

Solution: Replace direct runtime `Debug.LogWarning`, `Debug.LogError`, and `Debug.LogException` call sites with `Hecton8.Core.H8Debug` equivalents. Keep messages, context objects, and control flow unchanged.

Rejected Alternatives: Full logging architecture rewrite was rejected because it would touch public contracts and compete with parallel agents. Removing logs was rejected because critical init/failure diagnostics must remain visible through the sanctioned route.

Scalability potential: Low keeps diagnostic strings behind the central build/debug policy. Middle/High/Ultra retain richer diagnostics without changing gameplay truth or DTO layout.

Hardware Impact: Expected hot-path heap risk reduction on i3/MX350 where direct diagnostics accidentally reached runtime. Per-hit estimate is small and conditional; static route cleanup is the proof available without profiler.

## Decision 2: Source-Only, No YAML Or Project Settings

Problem: The project is dirty and parallel agents are active. Raw YAML/project settings edits risk asset corruption and cross-domain conflicts.

Solution: Limit AUTOFIX6 to C# source diagnostics and agent logs. No prefabs, scenes, assets, packages, or project settings.

Rejected Alternatives: Raw YAML mutation and broad project cleanup were rejected due AGENTS prefab/YAML warning and lack of a precise FileID-safe task.

Scalability potential: No content or render-tier behavior changes. Low/Middle/High/Ultra all receive safer diagnostics with identical gameplay.

Hardware Impact: No VRAM/GPU impact. CPU impact is neutral outside diagnostic fault paths.

## Decision 3: Build Gate Obeyed Instead Of Forcing Dotnet

Problem: AGENTS forbids launching dotnet build when CPU is above 50% or another dotnet/csc process is running. Current machine state was CPU=74 with dotnet process 64580 active.

Solution: Stop at static verification: scoped direct-debug call-site scan, H8Debug routed call count, and `git diff --check`. Mark compile as blocked by environment gate, not green.

Rejected Alternatives: Forcing `dotnet build` was rejected because it violates the explicit build gate and risks interfering with another active agent/process.

Scalability potential: No tier behavior changed. Low/Middle/High/Ultra keep identical gameplay; diagnostics remain centralized and conditionally stripped.

Hardware Impact: No runtime cost added. Expected benefit is removal of unmanaged diagnostic-policy drift; profiler proof remains pending.
