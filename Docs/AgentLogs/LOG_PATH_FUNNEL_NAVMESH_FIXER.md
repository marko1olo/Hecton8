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
