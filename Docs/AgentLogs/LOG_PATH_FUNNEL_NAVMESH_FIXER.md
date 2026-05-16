# PATH_FUNNEL_NAVMESH_FIXER Log

## Surgical Record - 2026-05-16

What was wrong:
- Assigned `Assets/_Project/Scripts/AI/Pathfinding/` folder did not exist.
- Funnel smoothing had no owned Burst path, no sector-local/AUP contract, no WFC door invalidation bridge, and no blackbox telemetry in this domain.
- Radius-only corner protection was insufficient; portal contracts needed an SDF clearance lane from the navgrid owner.
- Full `dotnet build` validation was blocked by upstream non-pathfinding compile errors outside AI/PATHING at this pass.

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
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly` failed with 33 upstream non-pathfinding errors and 0 `PathFunnel`/`AI\Pathfinding` matches. Evidence: `Docs/AgentLogs/Build_PATH_FUNNEL_NAVMESH_FIXER_CoreDependency.log`.
- Earlier `dotnet build .\Assembly-CSharp.csproj --no-restore -m:2 /nr:false /v:minimal /clp:ErrorsOnly` failed with 217 non-pathfinding errors and 0 pathfinding matches. Evidence: `Docs/AgentLogs/Build_PATH_FUNNEL_NAVMESH_FIXER_AssemblyCSharp.log`.

Integrator note:
- At this pass, do not treat the build as green. The pathfinding module was statically clean, but project validation was blocked by unrelated non-pathfinding dependency errors.

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
- AUP grid conversion now uses fixed inverse cell-size multiply; no runtime divide remains in the AUP conversion path.
- Re-ran static debt scans and build probes. At this pass Core build still showed 33 non-pathfinding errors and Assembly-CSharp showed 217 non-pathfinding errors, primarily missing RealtimeCSG source files plus Core dependency failures. Both logs had zero pathfinding matches. This is superseded by the Survival Re-Audit record below: Core is now green, Assembly-CSharp remains blocked by RealtimeCSG missing sources.

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

## Survival Re-Audit Record - 2026-05-16

What was wrong:
- Invalidation telemetry still had drift risk: replacing or unregistering an already-invalidated active path could leave `InvalidatedPathCount` overstated.
- Repeated close events for the same cell/path could enqueue duplicate invalidation payloads and consume blackbox ring capacity without new state.
- Ring cursors used integer modulo; branch wrap is cheaper and sufficient for fixed-size native rings.
- `AgentRadiusMeters` was clamped to non-negative but not sanitized for NaN/Infinity before radius erosion.

What was done:
- `FunnelSmoothingJob` now treats non-finite `AgentRadiusMeters` as zero before applying corner/radius erosion.
- `PathFunnelNavmeshRuntime` now decrements invalidated active count when invalidated paths are re-registered or unregistered.
- WFC invalidation now skips paths already marked invalidated, so `PathInvalidationCount` and invalidation-ring payloads represent state transitions only.
- Invalidation read/write cursors and telemetry cursor now use `AdvanceRingCursor` branch wrapping instead of `%`.
- Re-ran static anti-bloat scans, finite/division scans, Core build, Assembly-CSharp build, and build-log pathfinding filters.

Cinematic cheats used:
- Kept the door reaction as exact bitmask membership and transition telemetry instead of replaying obstacle simulation.
- Kept the prompt-local visual boundary: VFX remains N/A in this XML, so no shader, particle, or presentation-domain files were touched.

Exact microseconds saved:
- Modulo removal is nominal sub-microsecond and not profiler-measured; no fake frame-time claim is made.
- Duplicate invalidation suppression prevents avoidable recovery work and preserves the 300-frame blackbox for real transitions.
- Core pathing estimates remain unchanged: cross-product funnel saves an estimated 30-50 scalar ALU ops per portal versus angle math; door bitmask invalidation saves an estimated 20-80 us per door event versus corridor scans.

Verification:
- Hard-ban `rg` scan over `Assets/_Project/Scripts/AI/Pathfinding` found no private `NativeArray`, `H8Memory.Allocate`, `H8Memory.Release`, `new NativeArray`, `byte[]`, `File.WriteAllBytes`, `string.Format`, `Debug.Log`, Unity update loops, `Vector3`, scene `Find*`, legacy `EventBus`, delegates, `NavMesh`, or A* usage.
- Cursor/division scan found no runtime `%` or `math.rcp`; only XML comment slashes and epsilon-guarded `math.rsqrt` remain in the owned pathing files.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly` exits 0 with 0 errors.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -m:2 /nr:false /v:minimal /clp:ErrorsOnly` exits 1 with 216 missing `RealtimeCSG.csproj` source-file errors and zero pathfinding matches.

Integrator note:
- Owned AI/PATHING source is static verified master grade. Full Unity assembly, import, Play Mode, profiler, GCMonitor, Burst inspector, and player-build proof remain pending until the missing RealtimeCSG package source references are repaired.

## Blackbox Exception-Survival Record - 2026-05-16

What was wrong:
- The binary dump path still used filesystem APIs directly. If the path, directory, or stream failed, the dump request could throw during the crash-diagnosis path.
- Core build evidence changed during parallel workspace work: `Hecton8.Core.csproj` is no longer green and now fails in non-pathfinding World/VFX/RepairTool files.

What was done:
- Added `PathFunnelTelemetryFlags.BlackBoxDumpFailed`.
- Converted `DumpBlackBox` to `TryDumpBlackBox` and contained filesystem failure on the explicit dump path.
- `LateFrameTick` now clears stale dump-failure state on a new request, writes the normal heartbeat, and marks `BlackBoxDumpFailed` if the binary dump cannot be created.
- `PatchTelemetryFlags` updates the just-written telemetry slot on dump failure, so the current 300-frame ring captures the failed dump request immediately.
- Re-ran owned pathing hard-ban scans, cursor/division scans, Core build, and build-log pathfinding filters.

Cinematic cheats used:
- No new simulation or presentation work. This is crash-survival hardening only. The XML still marks VFX N/A.

Exact microseconds saved:
- No measured runtime saving claimed. The normal frame path remains one telemetry struct write; dump I/O still happens only on explicit crash/dump request.

Verification:
- Hard-ban `rg` scan over `Assets/_Project/Scripts/AI/Pathfinding` found no private `NativeArray`, `H8Memory.Allocate`, `H8Memory.Release`, `new NativeArray`, `byte[]`, `File.WriteAllBytes`, `string.Format`, `Debug.Log`, Unity update loops, `Vector3`, scene `Find*`, legacy `EventBus`, delegates, `NavMesh`, or A* usage.
- Cursor/division scan found no runtime `%` or `math.rcp`; only XML comment slashes and epsilon-guarded `math.rsqrt` remain in owned pathing files.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly` exits 1 with 137 non-pathfinding errors and zero pathfinding matches.
- `dotnet restore .\Assembly-CSharp.csproj` exits 0.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly` exits 1 because `Unity.RenderPipelines.Universal.Runtime.dll` is locked by another process; the current Assembly log has zero pathfinding matches.

Integrator note:
- Current compile wall is outside AI/PATHING: `FloraInteractionManager`, `SargassumMicroFaunaBoids`, `RepairTool`, `HectonUnderwaterVisuals`, and a locked URP build output. Do not attribute those failures to the funnel module.
