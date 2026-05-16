# PATH_FUNNEL_NAVMESH_FIXER Status

Domain: AI/PATHING
Assigned folder: `Assets/_Project/Scripts/AI/Pathfinding/`
Prompt task count: 18
Verification status: MULTIPLATFORM/H-PHI STATIC AUDIT PASS; HECTON8.CORE BLOCKED BY MISSING NON-PATHING CONTRACT SOURCE FILES; ASSEMBLY-CSHARP BLOCKED BY NON-PATHFINDING REALTIMECSG DEPENDENCY

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
- [x] 5. AUP_INTEGRITY sector-local to AUP | DOD: smoothing runs in sector-local `float3`, then writes `AbsoluteUniversePositionBlit` from `SectorOriginAbsoluteMeters + local`; AUP grid conversion uses the Core contract sector-size constant and fixed inverse cell-size multiply instead of runtime division | Rejected: world-space float accumulation and unlabelled duplicate AUP sector constants | Estimate: removes drift repair; 1-3 us/path saved from no rebasing pass
- [x] 6. DOD_SOA_LAYOUT NativeArray<float3> waypoints | DOD: output is caller-owned `NativeArray<float3> Waypoints` plus optional AUP SOA, no managed list | Rejected: `List<Vector3>` and `NativeList` growth in hot path | Estimate: 10-30 us/path saved under corridor overflow pressure
- [x] 7. SIGNAL_FLOW WFC door invalidation | DOD: consumes `WfcOutpostStateChangedSignal`; closes invalidate only tracked paths whose exact WFC contract cell mask includes the closed cell; asmdef directly references `Hecton8.Core.Contracts`, and pathing constants now alias `WfcOutpostPersistenceConstants.CellCount` plus `WfcOutpostCellStateFlags.DoorOpen` instead of duplicating magic values | Rejected: broad sector invalidation, transitive asmdef visibility, duplicate WFC constants, and direct WFC object references | Estimate: 20-80 us/event saved by bit test instead of corridor scan
- [x] 8. LOW_TIER_FAKE look-ahead 2 | DOD: `PathFunnelMathLod.Low` resolves to two portals | Rejected: full-corridor smoothing on weak hardware | Estimate: 12-40 us/path saved for long corridors
- [x] 9. HIGH_END_OVERKILL look-ahead 16 | DOD: `High` and `Ultra` resolve to sixteen portals for better silhouette/corner quality | Rejected: one balanced middle tier | Estimate: spends saved ALU for visual path quality on high-end CPU
- [x] 10. REACTIVE_VFX N/A | DOD: prompt marked VFX N/A, no VFX ownership touched | Rejected: inventing debug/path VFX outside task domain | Estimate: 0 us; no render work added
- [x] 11. STP_STABILIZATION N/A | DOD: prompt marked STP N/A, no unrelated stabilization system touched | Rejected: speculative steering/turning predictor | Estimate: 0 us; no simulation work added
- [x] 12. NAN_VACCINATION collinear/non-finite guards | DOD: guards sanitize non-finite points/AUP, non-finite `AgentRadiusMeters`, clamps narrow portals, flags collinear portals, protects `rsqrt` with epsilon, guards finite-but-out-of-range AUP grid casts, removes runtime AUP division, removes modulo from ring cursor math, and explicitly covers tail bytes in owned `Pack = 1` binary structs | Rejected: trusting navmesh input, unchecked double-to-long grid casts, or leaving ABI tail holes for ARM64/Quest | Estimate: avoids full path fallback crash cost; 1-4 us nominal overhead only on portal load; AUP overflow/tail-byte coverage has no measured runtime microsecond claim
- [x] 13. BLACKBOX_LOGGING PathInvalidationCount | DOD: 300-frame vault-owned `PathFunnelTelemetryEntry` ring records transition-only `PathInvalidationCount`, active paths, invalidated active paths, last sector/path/cell, and dump target `Docs/AgentLogs/Dump_PATH_FUNNEL_NAVMESH_FIXER.bin`; invalidation ring capacity clamps to at least 2 slots so read/write cursors can distinguish empty from one queued payload; duplicate stale-path hits no longer spam the invalidation ring; dump copies from native memory via `ReadOnlySpan<byte>`, not a managed `byte[]`; filesystem dump failure is contained and recorded immediately in the current telemetry slot via `PathFunnelTelemetryFlags.BlackBoxDumpFailed` | Rejected: managed log spam, one-slot unreadable rings, duplicate invalidation events, private telemetry allocation, and exception escape from crash dump | Estimate: <1 us/frame telemetry write; crash diagnosis replaces manual repro time
- [BLOCKED BY DEPENDENCY] 14. TRIPLE_STRIKE_REPAIR compile repair | DOD: current `Hecton8.Core.csproj` exits 1 with 3 missing non-pathing contract source files and 0 pathfinding matches; after restore, current `Assembly-CSharp.csproj` exits nonzero with 216 missing RealtimeCSG source errors and 0 pathfinding matches | Rejected: editing Core contract owners, RealtimeCSG package/project files, or claiming full project green while upstream dependencies fail | Estimate: N/A; full validation wall remains outside pathfinding
- [x] 15. HOMEOSTASIS_ADAPTATION stressed look-ahead 1 | DOD: `Stressed != 0` or `PathFunnelMathLod.Stressed` forces one-portal look-ahead and reports the effective `PathFunnelMathLod.Stressed` in `PathFunnelResult.MathLod` | Rejected: constant high-tier smoothing under frame pressure and result telemetry that reports requested LOD instead of executed LOD | Estimate: 20-60 us/path saved on stressed low-end frames
- [x] 16. CORNER_CUTTING_GUARD radius/SDF clearance | DOD: portal tightening erodes by `AgentRadiusMeters`; `NavPortal.ClearanceMeters` can carry pre-eroded SDF clearance and clamps under-radius portals | Rejected: post-smooth collision correction and raw SDF sampling in AI domain | Estimate: saves 10-40 us/path by preventing late corner repair
- [x] 17. ASYNC_SCHEDULE PRE_SIMULATION/POST_SIMULATION | DOD: `PathFunnelSchedule.SchedulePreSimulation` schedules without forcing completion; `TryReadPostSimulation` reads only completed handles | Rejected: blocking `Complete()` in simulation tick | Estimate: avoids 50-300 us worker-thread sync spikes
- [BLOCKED BY DEPENDENCY] 18. FINAL_VALIDATION dotnet build | DOD: `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly` exits 1 with 3 missing non-pathing contract source files and 0 pathfinding matches; after `dotnet restore .\Assembly-CSharp.csproj`, `dotnet build .\Assembly-CSharp.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly` exits nonzero with 216 missing RealtimeCSG source errors and 0 pathfinding matches | Rejected: claiming full green build or changing contract/RealtimeCSG dependencies outside AI/PATHING | Estimate: N/A; Unity assembly validation blocked upstream

## Verification Commands

- `rg -n "private NativeArray|H8Memory\.Allocate|H8Memory\.Release|new NativeArray|byte\[|new byte\[|File\.WriteAllBytes|string\.Format|Debug\.Log|Update\(|FixedUpdate\(|LateUpdate\(|Vector3|GameObject\.Find|FindObjectOfType|EventBus|Action<|Func<|foreach|UnityEngine\.AI|NavMesh|Astar" Assets/_Project/Scripts/AI/Pathfinding` -> no matches.
- `rg -n "\[StructLayout\(|PathFunnelRuntimeState|FieldOffset|Pack = 1|Reserved|PathFunnelActivePaths|AIPathfinding" Assets/_Project/Scripts/AI/Pathfinding Assets/_Project/Scripts/Core/Memory/H8Memory.cs` -> explicit `Pack = 1`, tail-byte `Reserved*` coverage, vault runtime state, and stable IDs present.
- `rg -n "WfcOutpostCellCount|WfcCellMaskWordCount|WfcDoorOpenFlag|WfcOutpostPersistenceConstants|WfcOutpostCellStateFlags|Hecton8.Core.Contracts" Assets/_Project/Scripts/AI/Pathfinding` -> WFC pathing constants alias Core contracts and asmdef has direct Core.Contracts reference.
- `Select-String -Path Docs/AgentLogs/Build_PATH_FUNNEL_NAVMESH_FIXER_CoreDependency.log -Pattern "PathFunnel|AI\\Pathfinding|AI/Pathfinding|Hecton8.AI.Pathfinding|AIPathfinding|PathFunnelTelemetryFlags|FunnelSmoothingJob|InvalidMathLod"` -> no matches.
- `Select-String -Path Docs/AgentLogs/Build_PATH_FUNNEL_NAVMESH_FIXER_AssemblyCSharp.log -Pattern "PathFunnel|AI\\Pathfinding|AI/Pathfinding|H8Memory.cs|PathFunnelRuntimeState|AIPathfinding|FunnelSmoothingJob|InvalidMathLod"` -> no matches.
- `rg -n "%|math\.rsqrt|math\.rcp|/" Assets/_Project/Scripts/AI/Pathfinding/PathFunnelNavmeshRuntime.cs Assets/_Project/Scripts/AI/Pathfinding/FunnelSmoothingJob.cs` -> modulo/runtime reciprocal removed; only XML comment slashes, epsilon-guarded `math.rsqrt`, and one compile-time `const double` inverse-sector division remain.
- `dotnet restore .\Assembly-CSharp.csproj` -> exit 0, restored project assets.
- Unity Bee/Roslyn `Hecton8.AI.Pathfinding.rsp` source-list check -> response file includes all four owned pathfinding `.cs` files and direct Core/Core.Contracts/Core.Memory refs; manual csc probe exits 1 only because `Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.ref.dll` is missing, evidence in `Docs/AgentLogs/Build_PATH_FUNNEL_NAVMESH_FIXER_PathfindingRsp.log`.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly` -> nonzero with 216 missing RealtimeCSG source errors, 0 pathfinding matches; evidence in `Docs/AgentLogs/Build_PATH_FUNNEL_NAVMESH_FIXER_AssemblyCSharp.log`.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 /nr:false /v:minimal /clp:ErrorsOnly` -> exit 1 with 3 missing non-pathing contract source files, 0 pathfinding matches; evidence in `Docs/AgentLogs/Build_PATH_FUNNEL_NAVMESH_FIXER_CoreDependency.log`.

## Loop Notes

Loop 0: Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md`; assigned AI pathing folder was missing on disk. Existing contracts found: `SignalBus<WfcOutpostStateChangedSignal>`, `BufferID.WfcOutpostGrid`, AUP blit payloads, `GlobalRegistry.DataVault`, dispatcher fast/late lanes.

Loop 1 (Tasks 1-5): Created isolated pathfinding contracts/job. Self-read found the mandate conflict between 3D funnel text and XML-required XZ cross; XML wins for WFC outpost planar corridors.

Loop 2 (Tasks 6-10): Added SOA output, WFC door invalidation, low/high look-ahead, and explicitly left VFX N/A untouched. Self-read rejected broad sector invalidation.

Loop 3 (Tasks 11-13): Added NaN/collinear guards and 300-frame blackbox telemetry. Self-read confirmed no Unity `Update`, `FixedUpdate`, or `LateUpdate`.

Loop 4 (Tasks 14-17): Added stressed look-ahead 1, non-blocking schedule/read helpers, and first build validation. Self-read found radius-only clearance was incomplete.

Loop 5 (Strict Audit): Added `NavPortal.ClearanceMeters` SDF clearance clamp and pathfinding asmdef. Static scan found no managed hot-path containers, no `Vector3`, and no singleton searches. The build was blocked at that pass by upstream non-pathfinding errors; later loop records supersede that build snapshot.

Loop 6 (Multiplatform/H-Phi Inquisition): Evicted path invalidation state from private persistent arrays into `GlobalDataVault` handles (`PathFunnelActivePaths`, `PathFunnelCellMasks`, `PathFunnelInvalidations`, `PathFunnelTelemetryRing`, `PathFunnelRuntimeState`) under `SystemID.AIPathfinding`. Converted pathing structs to explicit `Pack = 1` layouts with fixed offsets for ARM64/Quest/Android ABI stability. Replaced managed `byte[]` blackbox export with native-pointer `ReadOnlySpan<byte>` streaming. Self-read rejected local NativeArray ownership; remaining `NativeArray<T>` values in runtime are vault aliases or caller-owned API buffers.

Loop 7 (Strict Survival Re-Audit): Sanitized non-finite `AgentRadiusMeters`, made invalidation telemetry transition-only, decremented invalidated active count on re-register/unregister, removed duplicate invalidation ring events for already-invalidated paths, and replaced ring modulo with branch-based cursor advance. Self-read found no hot-path disk I/O, no private native ownership, no standard Unity update loop, no managed delegates, and no legacy nav stack usage in AI/PATHING.

Loop 8 (Blackbox Exception-Survival Pass): Converted blackbox export to `TryDumpBlackBox`, contained filesystem failures, and added `PathFunnelTelemetryFlags.BlackBoxDumpFailed` so a failed crash dump cannot become an unreported exception. Self-read confirmed the managed filesystem work remains outside hot cadence and only runs after explicit dump request.

Loop 9 (ABI Tail-Byte Pass): Added explicit reserved tail fields to `PathFunnelResult`, `PathFunnelActivePath`, and `PathFunnelInvalidation` so every byte in the fixed-size pathing wire structs is intentionally covered. Self-read rejected relying on implicit unused bytes inside `Pack = 1` structs for ARM64/Quest stability. Struct scan confirmed reserved tail offsets; hard-ban and cursor/division scans remain clean.

Loop 10 (Extreme-Value Survival Pass): Guarded AUP grid conversion against finite-but-out-of-range double-to-long casts and clamped invalidation rings to a minimum of 2 usable slots plus bounded max capacities. Self-read rejected one-slot rings and inspector-driven native capacity blowups.

Loop 11 (Asmdef Contract Pass): Added direct `Hecton8.Core.Contracts` reference to `Hecton8.AI.Pathfinding.asmdef` because `PathFunnelNavmeshRuntime` imports `Hecton8.Core.Contracts.Signals`. Self-read rejected relying on transitive Core visibility. Unity Bee response-file probe confirms all four owned source files are listed but cannot complete because `Hecton8.Core.ref.dll` is missing upstream.

Loop 12 (Interface Drift Purge): Replaced duplicated WFC cell count, mask word count, and door-open flag values with Core contract constants in `PathFunnelConstants`. Self-read rejected local magic values for a shared signal/data lane.

Loop 13 (Homeostasis Truth Pass): `FunnelSmoothingJob` now resolves one effective math LOD once, writes that effective tier to `PathFunnelResult.MathLod`, and drives look-ahead from the same byte. Self-read rejected blackbox/result payloads that claim High/Ultra while executing stressed one-portal smoothing. That revalidation found no owned pathfinding diagnostics; Loop 15 contains the current build blocker counts.

Loop 14 (Burst Contract Const Pass): AUP conversion uses the Core contract sector-size constant while computing the inverse as a compile-time `const double`, avoiding the `HectonPhysicsContract` static ref property from the Burst job. Self-read rejected a Burst kernel touching contract static constructor/property code.

Loop 15 (Source Truth Reconciliation Pass): Re-read the owned pathfinding source after the status/rationale files and found drift: the docs recorded the Burst const inverse before the source actually matched it. Reconciled `FunnelSmoothingJob` to the compile-time inverse, added invalid Math LOD fallback to Low with an explicit result flag, removed unused invalidation-buffer resolution from register/unregister mutations, and refreshed build logs. Current Core wall is 3 missing non-pathing contract source files; Assembly-CSharp remains blocked by 216 RealtimeCSG missing sources; both logs have 0 owned pathfinding matches.

## Omega Polish Audit

- Prompt-local mandate found: `[VI. OMEGA POLISH MANDATE] - STATUS: MUST BE "VERIFIED MASTER GRADE"`.
- Circular dependency check: `rg "Hecton8.AI.Pathfinding" Assets/_Project/Scripts -g "*.asmdef"` returns only the new pathfinding asmdef; Core does not reference it back.
- Anti-bloat scan: no `Astar`, `NavMesh`, `UnityEngine.AI`, `GameObject.Find`, `FindObjectOfType`, `Vector3`, `foreach`, `NativeList`, managed lists, `H8Memory.Allocate`, private `NativeArray`, `byte[]`, `File.WriteAllBytes`, or Unity message loops in `Assets/_Project/Scripts/AI/Pathfinding`; owned binary structs use explicit `Pack = 1` offsets with reserved tail-byte coverage; WFC constants are aliased from Core contracts instead of duplicated.
- Blocking sync check: only `PathFunnelSchedule.TryReadPostSimulation` calls `handle.Complete()` and only after `handle.IsCompleted` is true.
- Homeostasis truth check: stressed jobs now report effective `PathFunnelMathLod.Stressed`; the published result no longer reports requested High/Ultra when the executed kernel used one-portal look-ahead.
- Final status wording: STATIC VERIFIED MASTER GRADE for owned AI/PATHING code; Unity runtime/profiler proof is PENDING VERIFICATION, `Hecton8.Core.csproj` is currently blocked by missing non-pathing contract source files, and full Assembly-CSharp validation is blocked by RealtimeCSG package source debt with zero pathfinding matches in current logs.
