<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# 2026-05-07 CRUCIBLE Repeat Hardening Log
Date: 2026-05-07

Status: PENDING VERIFICATION

## Mandates Applied

- TOOL_Procedural_Wreckage_Generator
- MATH_Coordinate_Precision_AUP_FloatingOrigin
- OPT_Native_Memory_Collections_JobSystem_Protocol
- OPT_Zero_GC_Policy_AllocFree_Mandate
- OPT_Performance_Budgets_FrameTime_VRAM_Limits
- ARCH_Global_Registry_ServiceLocator_DI_Init
- UI_Data_Streaming_ZeroGC_Optimization
- AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation

## Inquisition Findings

- GC purge: targeted hot-path scan did not find new `.ToString()`, `string.Format`, LINQ materialization, `_instance`, or `DontDestroyOnLoad` in the hardening scope. No runtime profiler trace was captured, so 0B GC at runtime is not proven here.
- AUP radius: `SpectrumSystem` still had sonar and passive-radar decisions deriving long-range deltas from runtime `Vector3`. Passive radar source selection now gates emitters with AUP distance and clamps source eligibility to 30m. Abyssal-anchor sonar proximity now resolves nearest distance with `AbsoluteUniversePosition.DistanceSq`.
- Barrier audit: `.Complete()` remains only in cold/synchronous publication points: BRG wreck matrix publish and wreck material world-shift upload. `DispatcherJobSwap.TryComplete(... forceComplete: false)` remains the non-blocking swap path.
- Native arrays/lists: targeted native collection scan confirmed registrations and unregister/dispose paths through `NativeMemorySentinel` in wreck, spectrum, voxel, and audio systems.
- Singleton residue: current `SceneRuntimeService` is registry-backed through `GlobalRegistry.SceneRuntime`; the previous Unity console `_instance` errors were stale relative to current source.

## Excisions

- Removed runtime-space passive radar source selection in favor of AUP distance sorting and a 30m source cap.
- Removed runtime-space abyssal-anchor distance checks in sonar response and contact append paths; checks now use AUP distance before writing contacts.
- Fixed `CameraJuiceSystem` compile blocker by using the explicit `Hecton8.Physics.SubmarineStructuralGrid` type name at the structural-fatigue chromatic contribution site.

## AAA Cheat

- The wreck navigation proxy path short-circuits authored or disabled navigation meshes instead of building/sampling unnecessary proxy geometry. That preserves a cinematic/simple convex navigation proxy path and avoids paying for mesh work when gameplay has no consumer.

## Metrics

- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:ErrorsOnly`
  - Result: `Build succeeded. 0 Warning(s) 0 Error(s)`
  - Time: `00:00:35.15`
- `git diff --check` on the hardening scope:
  - Result: clean; only existing CRLF normalization warnings were emitted.
- Unity Editor log:
  - Domain reload completed.
  - Asset Pipeline Refresh reported `compile time=2 ms`.
  - Tail inspected did not show current C# compiler errors.
- Unity MCP console:
  - Blocked. `read_console` returned `Unity session not available; please retry`.
  - `set_active_instance Hecton8@5898b2fd69afdd2d` returned `No Unity instances are currently connected. Start Unity and press 'Start Session'.`

## Residual Risk

- MCP console 0-error proof was not obtained because the MCP Unity instance disconnected after refresh. Per project gate, status remains `PENDING VERIFICATION`.
- Runtime zero-GC was not proven by GCMonitor or Profiler capture in this pass.
