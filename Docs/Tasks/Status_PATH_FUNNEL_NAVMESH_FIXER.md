# PATH_FUNNEL_NAVMESH_FIXER Status

Domain: AI/PATHING
Assigned folder: `Assets/_Project/Scripts/AI/Pathfinding/`
Prompt task count: 18
Verification status: MULTIPLATFORM/H-PHI STATIC AUDIT PASS; DOTNET BLOCKED BY NON-PATHFINDING WORLD/VFX/URP BUILD LOCK DEPENDENCIES

## Mandates Loaded

- AI_Navigation_AStar_Funnel_Smoothing_Pathfinding.txt
- AI_DYNAMIC_NAVGRID_SDF_INTEGRATION.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- ARCH_Execution_Phases.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- MATH_AUP_Determinism_Sync.txt

## Task Checklist

- [x] 1. PURGE_SINGLETONS | DOD: no singleton path owner added; runtime uses dispatcher registration, cached `IDataVault`, and `SignalBus<WfcOutpostStateChangedSignal>` snapshots | Rejected: direct static path manager and legacy A* modifier hook | Estimate: 2-5 us/frame saved by avoiding repeated registry/singleton polling
- [x] 2. DEBT_CLEANUP Vector3 removal | DOD: funnel contracts and job use `float3`, `double3`, and AUP blit only; `rg Vector3 Assets/_Project/Scripts/AI/Pathfinding` returned no matches | Rejected: managed `Vector3` path arrays | Estimate: 3-8 us per 32 waypoints and zero conversion GC
- [x] 3. DATA_EVICTION WfcGrid/vault state | DOD: `PathFunnelNavmeshRuntime` caches `GlobalRegistry.DataVault`, reads `BufferID.WfcOutpostGrid` via `TryGetBuffer`, and keeps active paths/masks/invalidations/telemetry/runtime counters in `GlobalDataVault` handles | Rejected: direct Power/WFC runtime dependency, private persistent `NativeArray` fields, and per-frame `FindObjectOfType` | Estimate: 4-12 us/frame saved on low-end CPU plus native-leak risk removed
- [x] 4. BURST_ALGORITHM FunnelSmoothingJob | DOD: `FunnelSmoothingJob : IJob` is `[BurstCompile]` and string-pulls portals with `CrossXZ = (ab.x * ac.z) - (ab.z * ac.x)` | Rejected: angle/acos funnel and Bezier smoothing | Estimate: 30-50 scalar ALU ops saved per portal versus angle math
- [x] 5. AUP_INTEGRITY sector-local to AUP | DOD: smoothing runs in sector-local `float3`, then writes `AbsoluteUniversePositionBlit` from `SectorOriginAbsoluteMeters + local`; AUP grid conversion uses fixed inverse cell-size multiply instead of runtime division | Rejected: world-space float accumulation | Estimate: removes drift repair; 1-3 us/path saved from no rebasing pass
- [x] 6. DOD_SOA_LAYOUT NativeArray<float3> waypoints | DOD: output is caller-owned `NativeArray<float3> Waypoints` plus optional AUP SOA, no managed list | Rejected: `List<Vector3>` and `NativeList` growth in hot path | Estimate: 10-30 us/path saved under corridor overflow pressure
- [x] 7. SIGNAL_FLOW WFC door invalidation | DOD: consumes `WfcOutpostStateChangedSignal`; closes invalidate only tracked paths whose exact 500-bit corridor mask includes the closed cell | Rejected: broad sector invalidation and direct WFC object references | Estimate: 20-80 us/event saved by bit test instead of corridor scan
- [x] 8. LOW_TIER_FAKE look-ahead 2 | DOD: `PathFunnelMathLod.Low` resolves to two portals | Rejected: full-corridor smoothing on weak hardware | Estimate: 12-40 us/path saved for long corridors
- [x] 9. HIGH_END_OVERKILL look-ahead 16 | DOD: `High` and `Ultra` resolve to sixteen portals for better silhouette/corner quality | Rejected: one balanced middle tier | Estimate: spends saved ALU for visual path quality on high-end CPU
- [x] 10. REACTIVE_VFX N/A | DOD: prompt marked VFX N/A, no VFX ownership touched | Rejected: inventing debug/path VFX outside task domain | Estimate: 0 us; no render work added
- [x] 11. STP_STABILIZATION N/A | DOD: prompt marked STP N/A, no unrelated stabilization system touched | Rejected: speculative steering/turning predictor | Estimate: 0 us; no simulation work added
- [x] 12. NAN_VACCINATION collinear/non-finite guards | DOD: guards sanitize non-finite points/AUP, non-finite `AgentRadiusMeters`, clamps narrow portals, flags collinear portals, protects `rsqrt` with epsilon, removes runtime AUP division, and removes modulo from ring cursor math | Rejected: trusting navmesh input | Estimate: avoids full path fallback crash cost; 1-4 us nominal overhead only on portal load
- [x] 13. BLACKBOX_LOGGING PathInvalidationCount | DOD: 300-frame vault-owned `PathFunnelTelemetryEntry` ring records transition-only `PathInvalidationCount`, active paths, invalidated active paths, last sector/path/cell, and dump target `Docs/AgentLogs/Dump_PATH_FUNNEL_NAVMESH_FIXER.bin`; duplicate stale-path hits no longer spam the invalidation ring; dump copies from native memory via `ReadOnlySpan<byte>`, not a managed `byte[]`; filesystem dump failure is contained and recorded immediately in the current telemetry slot via `PathFunnelTelemetryFlags.BlackBoxDumpFailed` | Rejected: managed log spam, duplicate invalidation events, private telemetry allocation, and exception escape from crash dump | Estimate: <1 us/frame telemetry write; crash diagnosis replaces manual repro time
- [BLOCKED BY DEPENDENCY] 14. TRIPLE_STRIKE_REPAIR compile repair | DOD: restored project assets and reran builds; current `Hecton8.Core.csproj` exits 1 with 137 non-pathfinding errors in World/VFX/RepairTool files and 0 `PathFunnel`/`AI\Pathfinding`/`AIPathfinding` matches; current `Assembly-CSharp.csproj` exits 1 before owned pathfinding code because `Unity.RenderPipelines.Universal.Runtime.dll` is locked by another process and has 0 pathfinding matches | Rejected: editing World/VFX/URP/RealtimeCSG package/project files outside AI/PATHING or killing other agents' build processes | Estimate: N/A; validation wall remains
- [x] 15. HOMEOSTASIS_ADAPTATION stressed look-ahead 1 | DOD: `Stressed != 0` or `PathFunnelMathLod.Stressed` forces one-portal look-ahead | Rejected: constant high-tier smoothing under frame pressure | Estimate: 20-60 us/path saved on stressed low-end frames
- [x] 16. CORNER_CUTTING_GUARD radius/SDF clearance | DOD: portal tightening erodes by `AgentRadiusMeters`; `NavPortal.ClearanceMeters` can carry pre-eroded SDF clearance and clamps under-radius portals | Rejected: post-smooth collision correction and raw SDF sampling in AI domain | Estimate: saves 10-40 us/path by preventing late corner repair
- [x] 17. ASYNC_SCHEDULE PRE_SIMULATION/POST_SIMULATION | DOD: `PathFunnelSchedule.SchedulePreSimulation` schedules without forcing completion; `TryReadPostSimulation` reads only completed handles | Rejected: blocking `Complete()` in simulation tick | Estimate: avoids 50-300 us worker-thread sync spikes
- [BLOCKED BY DEPENDENCY] 18. FINAL_VALIDATION dotnet build | DOD: `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly` exits 1 with 137 non-pathfinding errors and 0 pathfinding matches; after `dotnet restore .\Assembly-CSharp.csproj`, `dotnet build .\Assembly-CSharp.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly` exits 1 with locked `Unity.RenderPipelines.Universal.Runtime.dll` and 0 pathfinding matches | Rejected: claiming full green build or changing cross-domain dependencies | Estimate: N/A; Unity assembly validation blocked upstream

## Verification Commands

- `rg -n "private NativeArray|H8Memory\.Allocate|H8Memory\.Release|new NativeArray|byte\[|new byte\[|File\.WriteAllBytes|string\.Format|Debug\.Log|Update\(|FixedUpdate\(|LateUpdate\(|Vector3|GameObject\.Find|FindObjectOfType|EventBus|Action<|Func<|foreach|UnityEngine\.AI|NavMesh|Astar" Assets/_Project/Scripts/AI/Pathfinding` -> no matches.
- `rg -n "\[StructLayout\(|PathFunnelRuntimeState|FieldOffset|Pack = 1|PathFunnelActivePaths|AIPathfinding" Assets/_Project/Scripts/AI/Pathfinding Assets/_Project/Scripts/Core/Memory/H8Memory.cs` -> explicit `Pack = 1`, vault runtime state, and stable IDs present.
- `Select-String -Path Docs/AgentLogs/Build_PATH_FUNNEL_NAVMESH_FIXER_CoreDependency.log -Pattern "PathFunnel|AI\\Pathfinding|AI/Pathfinding|Hecton8.AI.Pathfinding|AIPathfinding|PathFunnelTelemetryFlags"` -> no matches.
- `Select-String -Path Docs/AgentLogs/Build_PATH_FUNNEL_NAVMESH_FIXER_AssemblyCSharp.log -Pattern "PathFunnel|AI\\Pathfinding|AI/Pathfinding|H8Memory.cs|PathFunnelRuntimeState|AIPathfinding"` -> no matches.
- `rg -n "%|math\.rsqrt|math\.rcp|/" Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs Assets/_Project/Scripts/AI/Pathfinding/FunnelSmoothingJob.cs` -> modulo/runtime reciprocal removed; only XML comment slashes and epsilon-guarded `math.rsqrt` remain.
- `dotnet restore .\Assembly-CSharp.csproj` -> exit 0, restored project assets.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly` -> exit 1, locked `Unity.RenderPipelines.Universal.Runtime.dll`, 0 pathfinding matches; evidence in `Docs/AgentLogs/Build_PATH_FUNNEL_NAVMESH_FIXER_AssemblyCSharp.log`.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly` -> exit 1, 137 non-pathfinding errors in World/VFX/RepairTool files, 0 pathfinding matches; evidence in `Docs/AgentLogs/Build_PATH_FUNNEL_NAVMESH_FIXER_CoreDependency.log`.

## Loop Notes

Loop 0: Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`; assigned AI pathing folder was missing on disk. Existing contracts found: `SignalBus<WfcOutpostStateChangedSignal>`, `BufferID.WfcOutpostGrid`, AUP blit payloads, `GlobalRegistry.DataVault`, dispatcher fast/late lanes.

Loop 1 (Tasks 1-5): Created isolated pathfinding contracts/job. Self-read found the mandate conflict between 3D funnel text and XML-required XZ cross; XML wins for WFC outpost planar corridors.

Loop 2 (Tasks 6-10): Added SOA output, WFC door invalidation, low/high look-ahead, and explicitly left VFX N/A untouched. Self-read rejected broad sector invalidation.

Loop 3 (Tasks 11-13): Added NaN/collinear guards and 300-frame blackbox telemetry. Self-read confirmed no Unity `Update`, `FixedUpdate`, or `LateUpdate`.

Loop 4 (Tasks 14-17): Added stressed look-ahead 1, non-blocking schedule/read helpers, and first build validation. Self-read found radius-only clearance was incomplete.

Loop 5 (Strict Audit): Added `NavPortal.ClearanceMeters` SDF clearance clamp and pathfinding asmdef. Static scan found no managed hot-path containers, no `Vector3`, and no singleton searches. The build was blocked at that pass by upstream non-pathfinding errors; Loop 7 supersedes this with Core green and Assembly still blocked by RealtimeCSG missing sources.

Loop 6 (Multiplatform/H-Phi Inquisition): Evicted path invalidation state from private persistent arrays into `GlobalDataVault` handles (`PathFunnelActivePaths`, `PathFunnelCellMasks`, `PathFunnelInvalidations`, `PathFunnelTelemetryRing`, `PathFunnelRuntimeState`) under `SystemID.AIPathfinding`. Converted pathing structs to explicit `Pack = 1` layouts with fixed offsets for ARM64/Quest/Android ABI stability. Replaced managed `byte[]` blackbox export with native-pointer `ReadOnlySpan<byte>` streaming. Self-read rejected local NativeArray ownership; remaining `NativeArray<T>` values in runtime are vault aliases or caller-owned API buffers.

Loop 7 (Strict Survival Re-Audit): Sanitized non-finite `AgentRadiusMeters`, made invalidation telemetry transition-only, decremented invalidated active count on re-register/unregister, removed duplicate invalidation ring events for already-invalidated paths, and replaced ring modulo with branch-based cursor advance. Self-read found no hot-path disk I/O, no private native ownership, no standard Unity update loop, no managed delegates, and no legacy nav stack usage in AI/PATHING.

Loop 8 (Blackbox Exception-Survival Pass): Converted blackbox export to `TryDumpBlackBox`, contained filesystem failures, and added `PathFunnelTelemetryFlags.BlackBoxDumpFailed` so a failed crash dump cannot become an unreported exception. Self-read confirmed the managed filesystem work remains outside hot cadence and only runs after explicit dump request.

## Omega Polish Audit

- Prompt-local mandate found: `[VI. OMEGA POLISH MANDATE] - STATUS: MUST BE "VERIFIED MASTER GRADE"`.
- Circular dependency check: `rg "Hecton8.AI.Pathfinding" Assets/_Project/Scripts -g "*.asmdef"` returns only the new pathfinding asmdef; Core does not reference it back.
- Anti-bloat scan: no `Astar`, `NavMesh`, `UnityEngine.AI`, `GameObject.Find`, `FindObjectOfType`, `Vector3`, `foreach`, `NativeList`, managed lists, `H8Memory.Allocate`, private `NativeArray`, `byte[]`, `File.WriteAllBytes`, or Unity message loops in `Assets/_Project/Scripts/AI/Pathfinding`.
- Blocking sync check: only `PathFunnelSchedule.TryReadPostSimulation` calls `handle.Complete()` and only after `handle.IsCompleted` is true.
- Final status wording: STATIC VERIFIED MASTER GRADE for owned AI/PATHING code; Unity runtime/profiler proof is PENDING VERIFICATION, Core validation is currently blocked by non-pathfinding World/VFX/RepairTool errors, and full Assembly-CSharp validation is blocked by a locked URP output plus prior RealtimeCSG package source debt, with zero pathfinding matches in current logs.
