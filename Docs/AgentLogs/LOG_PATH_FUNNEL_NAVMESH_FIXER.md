# PATH_FUNNEL_NAVMESH_FIXER Log

## Surgical Record - 2026-05-16

What was wrong:
- Assigned `Assets/_Project/Scripts/AI/Pathfinding/` folder did not exist.
- Funnel smoothing had no owned Burst path, no sector-local/AUP contract, no WFC door invalidation bridge, and no blackbox telemetry in this domain.
- Radius-only corner protection was insufficient; portal contracts needed an SDF clearance lane from the navgrid owner.
- Full `dotnet build` validation is blocked by upstream Core compile errors outside AI/PATHING.

What was done:
- Added `Hecton8.AI.Pathfinding.asmdef` to isolate the new AI pathing module.
- Added `PathFunnelContracts.cs` with `NavPortal`, `PathFunnelResult`, `PathFunnelMathLod`, invalidation payloads, active-path records, and 300-frame telemetry entries.
- Added `FunnelSmoothingJob.cs`: Burst `IJob`, no `Vector3`, XZ cross-product funnel string pulling, Low/Middle/High/Ultra look-ahead, stressed look-ahead 1, NaN/collinear guards, door block checks against `WfcOutpostGrid`, radius erosion, SDF clearance clamp, and AUP blit output.
- Added `PathFunnelSchedule.cs`: PRE_SIMULATION schedule helper and POST_SIMULATION readback helper that refuses to force-complete unfinished jobs.
- Added `PathFunnelNavmeshRuntime.cs`: cached `IDataVault`, `SignalBus<WfcOutpostStateChangedSignal>` consumption, exact 500-bit corridor masks, door-close invalidation, bounded invalidation ring, and binary blackbox dump path.
- Updated `Docs/Tasks/Status_PATH_FUNNEL_NAVMESH_FIXER.md` and `Docs/AgentLogs/Rationale_PATH_FUNNEL_NAVMESH_FIXER.md` with task-by-task DOD, rejected alternatives, scalability tiers, and hardware impact.

Cinematic cheats used:
- Chose XZ scalar cross-product string pulling for WFC outpost corridors instead of angle math or 3D physical steering.
- Used bounded Math LOD look-ahead rather than full-corridor smoothing on every request.
- Used pre-eroded SDF clearance carried on the portal instead of sampling raw SDF texture data inside AI.
- Door invalidation is exact bitmask membership, not resimulated obstacle physics.

Exact microseconds saved:
- Cross-product funnel versus angle/acos smoothing: estimated 20-80 us per 32-portal path and 30-50 scalar ALU ops per portal.
- WFC door bitmask invalidation versus corridor cell scans: estimated 20-80 us per door event.
- Stressed look-ahead 1 versus high-tier 16: estimated 20-60 us per long path during frame pressure.
- Non-blocking readback versus forced job completion: estimated 50-300 us sync spike avoided.
- Telemetry blackbox write: estimated under 1 us/frame; dump allocation occurs only on explicit dump/crash path.

Verification:
- Static anti-bloat scans passed for owned pathfinding files: no `Vector3`, managed lists, `NativeList`, Unity message loops, `GameObject.Find`, A*, or Unity NavMesh.
- `dotnet restore .\Assembly-CSharp.csproj` succeeded.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly` failed with 110 upstream Core errors and 0 `PathFunnel`/`AI\Pathfinding` matches. Evidence: `Docs/AgentLogs/Build_PATH_FUNNEL_NAVMESH_FIXER_CoreDependency.log`.
- Latest `dotnet build .\Assembly-CSharp.csproj --no-restore -m:2 /nr:false` ended `EXIT=-1` before C# diagnostics. Evidence: `Docs/AgentLogs/Build_PATH_FUNNEL_NAVMESH_FIXER_AssemblyCSharp.log`.

Integrator note:
- Do not treat the build as green. The pathfinding module is statically clean, but project validation is blocked by unrelated Core dependency errors in files such as `HectonVoxelEngine.cs`, `PredatorCognitionDomain.cs`, `GameBootstrapper.cs`, `VoxelDeltaProcessor.cs`, `GlobalSignals.cs`, and `SystemDispatcher.cs`.

## Multiplatform/H-Phi Polish Record - 2026-05-16

What was wrong:
- The runtime ownership model was not strict enough for the follow-up gate: private persistent path invalidation arrays are unacceptable when GlobalDataVault exists.
- `NavPortal` still relied on sequential packing; that leaves ABI interpretation to the runtime/compiler instead of making Quest/ARM64 layout explicit.
- The blackbox dump path used a managed `byte[]` copy before file export.
- The previous build evidence was stale: Core dependency count changed, and Assembly-CSharp needed a fresh restore/build attempt.

What was done:
- Path invalidation state now resolves through vault handles for `PathFunnelActivePaths`, `PathFunnelCellMasks`, `PathFunnelInvalidations`, `PathFunnelTelemetryRing`, and `PathFunnelRuntimeState` under `SystemID.AIPathfinding`.
- `PathFunnelRuntimeState` stores active count, ring cursors, telemetry cursor, invalidation count, last path/corridor/sector/cell, dump request, and vault generation in one explicit 64-byte block.
- All pathing binary structs use explicit `Pack = 1` field offsets. 64-bit fields remain on aligned offsets where those structs contain them.
- Blackbox export now streams from the native telemetry pointer through `ReadOnlySpan<byte>` and `FileStream`; no managed `byte[]` copy remains.
- Re-ran static debt scans and build probes. Core build: 33 non-pathfinding errors. Assembly-CSharp build: 217 non-pathfinding errors, primarily missing RealtimeCSG source files plus Core dependency failures. Both logs have zero pathfinding matches.

Cinematic cheats used:
- Kept WFC door response as exact 500-bit corridor mask tests instead of simulating obstacles or broad path physics.
- Preserved tiered look-ahead: Low 2, stressed 1, High/Ultra 16. This is the "Dear Lie" path: cheap scalar geometry under load, smoother silhouette when budget exists.
- Did not add visual systems because the XML says VFX N/A. Saved pathing budget is explicitly reserved for presentation owners to spend on silt, visor, hull, and particle overkill.

Exact microseconds saved:
- Cross-product funnel versus angle/acos smoothing remains estimated at 20-80 us per 32-portal path and 30-50 scalar ALU ops per portal.
- Door bitmask invalidation versus corridor scans remains estimated at 20-80 us per door event.
- Stressed look-ahead 1 versus 16 remains estimated at 20-60 us per long path during frame pressure.
- DataVault eviction does not claim a measured frame-time win; its gain is lifetime safety and avoiding private persistent native ownership. Measured profiler proof is absent.

Verification:
- `rg` hard-ban scan over `Assets/_Project/Scripts/AI/Pathfinding` found no private `NativeArray`, `H8Memory.Allocate`, `H8Memory.Release`, `new NativeArray`, `byte[]`, `File.WriteAllBytes`, `string.Format`, `Debug.Log`, Unity update loops, `Vector3`, scene `Find*`, legacy `EventBus`, delegates, `NavMesh`, or A* usage.
- Struct layout scan confirmed `Pack = 1`, explicit offsets, `PathFunnelRuntimeState`, `SystemID.AIPathfinding`, and path funnel vault `BufferID` entries.
- `Select-String` over both build logs found zero `PathFunnel`, `AI\Pathfinding`, `AI/Pathfinding`, `PathFunnelRuntimeState`, or `AIPathfinding` matches.

Integrator note:
- Build is still blocked. Do not label this runtime-verified. Static AI/PATHING audit is clean; Unity import, Play Mode, profiler, GCMonitor, and player build remain pending after upstream dependency repair.
