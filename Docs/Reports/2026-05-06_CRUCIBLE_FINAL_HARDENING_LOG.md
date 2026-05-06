# 2026-05-06 CRUCIBLE FINAL HARDENING LOG
Date: 2026-05-07

Status: PENDING VERIFICATION

Scope:
- Procedural wreck BRG generation and collision/nav proxy path.
- Sonar/acoustic material compile break found by Unity console.
- Registry-backed celestial runtime compatibility found by build gate.
- Fake radar thermal-noise duplicate compile break found by Unity console.

Mandates applied:
- TOOL_Procedural_Wreckage_Generator.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- UI_Data_Streaming_ZeroGC_Optimization.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt

Inquisition findings:
- GC purge: audited wreck/anomaly/voxel/wreck-material touched files for `.ToString`, `string.Format`, interpolation, LINQ, dictionary `foreach`, and `new`. No new managed allocation in wreck Tick/Update path. Remaining interpolations and `new Mesh` calls are debug/editor/cold generation paths.
- AUP radius: no `Vector3.Distance` in audited touched files. `WreckMaterialRegistry.SlowTick` reads transform position only to create an `AbsoluteUniversePosition`, then uses squared AUP distance.
- Barrier audit: `ProceduralWreckGenerator.PublishWreckRenderPayload` still completes a TempJob upload job in cold publish. `WreckMaterialRegistry.ApplyOriginShift` completes an origin-shift job in world-shift phase. No new per-frame hot-path `Complete`.
- Native arrays: `ProceduralWreckGenerator` persistent native containers are registered with `NativeMemorySentinel` and disposed/unregistered on destroy. TempJob containers in render payload publish are disposed in `finally`.
- Singleton residue: celestial singleton compatibility now reads `GlobalRegistry.CelestialEngine`; no `_instance` was reintroduced for `HectonCelestialEngine`.

Excisions:
- Replaced unconditional procedural navigation proxy mesh construction with `ResolveNavigationProxyMesh` / `ResolveNavigationProxyMeshAsync`. Authored collision/nav proxy now short-circuits generated mesh build.
- Added sonar material resolver methods in `PlayerCriticalProceduralAudioRenderer` to close missing `AcousticEchoEvent.AudioMaterialId` compile path.
- Added registry-backed `HectonCelestialEngine.ActiveRuntimeInstance` compatibility property to close old call sites without restoring a local singleton.

AAA cheat:
- The expensive mesh-build lie was removed from wreck generation. When `buildAsyncNavMesh` is disabled or an authored `wreckCollisionProxyMesh` exists, generation no longer builds a procedural proxy mesh. The player sees the same simple center collision/nav proxy; CPU does not spend time baking unused per-wreck mesh data.

Metrics:
- `git diff --check` on hardened files: exit 0. Only CRLF normalization warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:ErrorsOnly`: Build succeeded, 0 warnings, 0 errors, 00:01:06.23.
- Unity MCP `read_console` filtered to errors after script compile: 0 log entries.
- `validate_script Assets/_Project/Scripts/Core/GlobalRegistry.cs`: 0 warnings, 0 errors.
- `validate_script Assets/_Project/Scripts/HectonCelestialEngine.cs`: static validator reports duplicate factory-method signatures, but `rg` shows one declaration per flagged method and compiler/MCP console both report 0 errors. Treat as validator false positive, not a compile blocker.

Residual risk:
- No runtime profiler capture was run. Zero-GC runtime behavior is supported by static audit only, not by a 60s profiler allocation trace.
- Existing singleton residue remains in systems outside the wreck domain, including `HectonSurfaceWeatherDirector._instance`.
