# SHINOBU_304 Final Report - 2026-05-22

What was wrong:
- Existing AI pathfinding had WFC funnel support but no standalone async 3D voxel SDF A* route for caves.
- Long-range fauna pathing still risked floor/navmesh thinking or physics query coupling if implemented naively.
- No SHINOBU_304 DataVault route existed for requests, solver scratch, heap, raw path, smoothed AUP waypoints, species profiles, or black-box telemetry.

What was done:
- Added `VoxelAStarContracts.cs` with explicit-layout DTOs: `PathRequestDTO` 64B, `PathResultDTO` 128B, `VoxelPathWaypointDTO` 32B, `PathfindingTelemetryEntry` 64B, solver/node/heap/tuning/profile structs.
- Added `VoxelAStarJobs.cs`: `NativeMinHeap`, `GenerateMockPathingSDFJob`, `EvaluateVoxelPathJob`, `SmoothPathStringPullingJob`, and `VoxelPathingProfileCsvParser`.
- Extended `PathFunnelNavmeshRuntime` as partial with `PathFunnelNavmeshRuntime_VoxelAStar.cs`; no competing pathfinding manager was created.
- Added DataVault `BufferID`s for requests, ring state, solver state, nodes, heap, heap positions, raw path, waypoints, results, telemetry, tuning, mock SDF, SDF header, profiles, CSV scratch, and closed-set debug.
- Added editor-only tooling: `AbyssalPathfindingTunerWindow`, `VoxelAStarDebugGizmo`, `OOP_NavMesh_Scanner`.
- Generated `Docs/Reports/AI_OPTIMIZATION_REPORT.json` and `Docs/Reports/AI_OPTIMIZATION_REPORT_SHINOBU_304.json`; current SHINOBU_304 scan found 0 runtime `NavMesh`/`SphereCast`/managed path queue hits in AI runtime scope.

Cinematic cheats used:
- Replaced physical cave probing with SDF scalar clearance checks.
- Mock cave uses cheap triangle-wave tube/chamber/shaft SDF, not simulated rock physics.
- Low quality increases weighted A* behavior and lowers per-frame node budget; path may take more frames rather than blocking.
- String-pulling validates straight-line clearance through SDF instead of using collision casts.

Exact microseconds saved:
- Measured exact saved microseconds: 0, because Unity profiler/build execution was blocked by project CPU/dotnet guard.
- Design-level avoided costs: no `Physics.SphereCast`, no `NavMesh.CalculatePath`, no managed `Queue<PathRequest>`, no `List<Node>`, no `Dictionary`, no `MemClear` over large node/heap buffers.
- Telemetry `BurstMicros` now records owner-side schedule-to-finalize wall latency, not fake kernel time. Exact Burst kernel microseconds require Unity profiler instrumentation.

Compile / verification:
- Build not launched. CPU guard remained over 50% and an active `dotnet` process existed.
- Static runtime scans passed for forbidden NavMesh/physics/path queue tokens and managed allocation tokens under SHINOBU_304 runtime scope.
- `git diff --check` passed with line-ending warnings only.

<SELF_AUDIT>
  <TASKS>
    <TASK id="01" status="PASS">Archaeology rg scan completed and logged.</TASK>
    <TASK id="02" status="PASS">Existing `PathFunnelNavmeshRuntime` extended as partial.</TASK>
    <TASK id="03" status="PASS">Signal matrix checked; DataVault result DTO route used.</TASK>
    <TASK id="04" status="PASS">Runtime AI NavMesh/physics path token scan returned 0 hits.</TASK>
    <TASK id="05" status="PASS">Native request ring replaces managed path queue for new route.</TASK>
    <TASK id="06" status="PASS">Burst mock SDF generator implemented.</TASK>
    <TASK id="07" status="PASS">NativeMinHeap implemented over preallocated arrays.</TASK>
    <TASK id="08" status="PASS">Voxel SDF A* clearance evaluation implemented.</TASK>
    <TASK id="09" status="PASS">Persistent time-sliced solver implemented.</TASK>
    <TASK id="10" status="PASS">SDF string-pulling smoothing implemented.</TASK>
    <TASK id="11" status="PASS">Continuous quality heuristic and node budget implemented.</TASK>
    <TASK id="12" status="PASS">AUP local subtraction and double3 output implemented.</TASK>
    <TASK id="13" status="PASS">Deterministic Burst and explicit result DTO layout implemented; runtime replay proof pending build.</TASK>
    <TASK id="14" status="PASS">Large scratch buffers request UninitializedMemory; search stamps guard stale memory.</TASK>
    <TASK id="15" status="PASS">300-frame telemetry and raw dump path implemented; exact kernel timing pending profiler.</TASK>
    <TASK id="16" status="PASS">UI Toolkit tuner implemented.</TASK>
    <TASK id="17" status="PASS">ReadOnlySpan CSV parser implemented.</TASK>
    <TASK id="18" status="PASS">Scene-view debug gizmo implemented.</TASK>
    <TASK id="19" status="PASS">OOP scanner and JSON report implemented.</TASK>
    <TASK id="20" status="PASS">Static self-audit completed; build blocked by guard.</TASK>
  </TASKS>
  <ARM64_CHECK>
    PathRequestDTO: size 64; StartAUP offset 0; EndAUP offset 24; RequiredRadius offset 48; RequesterEntityHash offset 52; Flags offset 56; pad offset 60.
    PathResultDTO: size 128; hot scalar fields 0..63; StartAUP offset 64; EndAUP offset 88; padding/reserved 112..127.
    VoxelPathWaypointDTO: size 32; PositionAUP offset 0; NodeIndex offset 24; Flags offset 28.
    PathfindingTelemetryEntry: size 64; fixed scalar telemetry fields 0..63.
  </ARM64_CHECK>
  <ZERO_GC_CHECK>Runtime pathfinding scan found no `new List`, `new Dictionary`, LINQ, async/await, `Queue<PathRequest>`, NavMesh, SphereCast, or hot `GetComponent` tokens in SHINOBU_304 runtime files.</ZERO_GC_CHECK>
  <AUP_CHECK>Requests subtract SDF grid `double3 OriginAUP` before float voxel conversion; smoothed waypoints add local voxel centers back to `double3` AUP.</AUP_CHECK>
  <DEAR_LIE_CHECK>SDF scalar clearance replaces collision physics; weighted A* plus per-frame node budget turns long paths into bounded async work.</DEAR_LIE_CHECK>
  <DEPENDENCY_CHECK>Runtime route uses `GlobalDataVault` handles and existing `PathFunnelNavmeshRuntime`; no new sibling runtime dependency was introduced.</DEPENDENCY_CHECK>
  <BLACKBOX>300-entry `PathfindingTelemetryEntry` ring and `Docs/AgentLogs/Dump_SHINOBU_304.bin` dump path implemented.</BLACKBOX>
</SELF_AUDIT>

---

# SHINOBU_304 Polish Pass Report - Loop 12 - 2026-05-22

What was wrong:
- The static active-job bridge removed editor scene searches, but it used a plain `int` with no memory-ordering contract.

What was done:
- `PathFunnelNavmeshRuntime.IsAnyVoxelAStarJobActive()` now uses `System.Threading.Volatile.Read`.
- Schedule/finalize/teardown accounting now uses `System.Threading.Interlocked`.
- Re-extracted the `SHINOBU_304` prompt from `CURRENT_BATCH.md`: `chars=27631`, `task_mentions=20`.
- Re-audited asmdefs: `Hecton8.AI.Pathfinding` has no direct sibling runtime dependency.

Cinematic cheats used:
- No new physical simulation. The pathfinder remains SDF-scalar clearance plus time-sliced A*/string-pulling.

Exact microseconds saved:
- Measured saved microseconds: 0. Build/profiler execution remains blocked by guard.
- Runtime added cost: one atomic op per scheduled/finalized job, not per node.
- Guard: CPU 100.00%, active `dotnet` PIDs 6528 and 12072. No compile launched.

Verification:
- Runtime-only forbidden scan excluding editor files returned 0 hits.
- Editor scene-search scan returned 0 hits.
- Scoped `git diff --check` passed for SHINOBU_304 files with CRLF/LF warnings only.

---

# SHINOBU_304 Polish Pass Report - Loop 13 - 2026-05-22

What was wrong:
- `NativeMinHeap` was zero-GC but internally stored NativeArray views; the extracted prompt explicitly demanded raw-pointer heap ownership.

What was done:
- Converted `NativeMinHeap` to `unsafe` pointer-backed storage over heap, heap-position mirror, and node record buffers.
- Added explicit pointer lengths and retained bounds checks before every heap/node access.
- Marked `EvaluateVoxelPathJob` as `unsafe`; NativeArray fields remain on the job for Unity safety/dependency tracking.

Cinematic cheats used:
- Unchanged: SDF scalar clearance remains the collision proxy; no physics sweeps or NavMesh routes were added.

Exact microseconds saved:
- Measured saved microseconds: 0. Build/profiler still blocked.
- Structural target: heap sift/pop/decrease now avoid NativeArray wrapper access inside the helper.
- Guard: CPU 100.00%, active `dotnet` PIDs 6528 and 15808. No compile launched.

Verification:
- Runtime-only forbidden scan returned 0 hits.
- DTO/property/Pack scan returned 0 hits.
- Scoped `git diff --check` passed for SHINOBU_304 files with CRLF/LF warnings only.

---

# SHINOBU_304 Scanner Hardening - 2026-05-22

What was wrong:
- `OOP_NavMesh_Scanner` used raw source `IndexOf`, so forbidden tokens in comments/strings could pollute the proof.

What was done:
- Replaced raw source scan with comment/string stripped lightweight syntax scan.
- Added type/method/invocation node counters.
- Refreshed shared and SHINOBU-stable JSON reports.

Cinematic cheats used:
- No runtime work. The scanner is an editor proof artifact.

Exact microseconds saved:
- Runtime saved microseconds: 0. Editor-only validator.

Verification:
- Report counters: `filesScanned=31`, `syntaxTypeNodes=224`, `syntaxMethodNodes=942`, `syntaxInvocationNodes=9977`, `forbiddenHitCount=0`.
- Scoped `git diff --check`: passed with CRLF/LF warnings only.

<SELF_AUDIT pass="polish_loop_11">
  <TASKS>
    <TASK id="01" status="PASS">Scanner hardening followed renewed assignment extraction.</TASK>
    <TASK id="02" status="PASS">No owner class split.</TASK>
    <TASK id="03" status="PASS">No new signal route.</TASK>
    <TASK id="04" status="PASS">NavMesh/physics route proof strengthened.</TASK>
    <TASK id="05" status="PASS">Native request ring unchanged.</TASK>
    <TASK id="06" status="PASS">Mock SDF unchanged.</TASK>
    <TASK id="07" status="PASS">NativeMinHeap unchanged.</TASK>
    <TASK id="08" status="PASS">SDF A* unchanged.</TASK>
    <TASK id="09" status="PASS">No new blocking path.</TASK>
    <TASK id="10" status="PASS">String pulling unchanged.</TASK>
    <TASK id="11" status="PASS">Quality curve unchanged.</TASK>
    <TASK id="12" status="PASS">AUP route unchanged.</TASK>
    <TASK id="13" status="PASS">DTO layout unchanged.</TASK>
    <TASK id="14" status="PASS">No clear/memset path added.</TASK>
    <TASK id="15" status="PASS">Telemetry unchanged.</TASK>
    <TASK id="16" status="PASS">Editor facade unchanged.</TASK>
    <TASK id="17" status="PASS">CSV bridge unchanged.</TASK>
    <TASK id="18" status="PASS">Gizmo unchanged.</TASK>
    <TASK id="19" status="PASS">Scanner now performs structural syntax pass over stripped code.</TASK>
    <TASK id="20" status="PASS">Reports refreshed.</TASK>
  </TASKS>
  <STRUCT_LAYOUT>No DTO layout changed in Loop 11.</STRUCT_LAYOUT>
  <H_PHI_VAULT>No new runtime storage.</H_PHI_VAULT>
  <DEPENDENCY_GRAPH>No runtime job graph changes.</DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No build launched.</COMPILE_GUARD>
  <DEAR_LIE>Runtime still uses SDF scalar collision fake.</DEAR_LIE>
</SELF_AUDIT>

---

# SHINOBU_304 Editor Fence Polish - 2026-05-22

What was wrong:
- The Loop 9 editor safety fence used `Resources.FindObjectsOfTypeAll` to discover active runtimes. It was editor-only, but still a scene scan and managed array allocation during tuner/gizmo callbacks.

What was done:
- Added an owner-maintained static voxel-job active count.
- Incremented the count on evaluate/smooth job scheduling.
- Decremented the count on late-frame finalize and teardown forced-complete paths.
- Changed tuner graph and debug gizmo to use `PathFunnelNavmeshRuntime.IsAnyVoxelAStarJobActive()`.

Cinematic cheats used:
- No new simulation. This is a cheap ownership signal, not scene discovery.

Exact microseconds saved:
- Measured saved microseconds: 0. Build/profiler still guarded.
- Structural savings: removed editor repaint scene scan and managed runtime-array allocation.

Verification:
- Runtime forbidden scan excluding Editor: 0 hits.
- Editor scene-search scan: 0 hits for `FindObjectsOfType`, `Resources.FindObjectsOfTypeAll`, and `FindObjectOfType` under SHINOBU_304 editor files.
- Brace balance: runtime partial `65/65`, tuner `35/35`, gizmo `18/18`.

<SELF_AUDIT pass="polish_loop_10">
  <TASKS>
    <TASK id="01" status="PASS">Assignment re-read before Loop 10.</TASK>
    <TASK id="02" status="PASS">Owner partial unchanged.</TASK>
    <TASK id="03" status="PASS">No new signal or dependency added.</TASK>
    <TASK id="04" status="PASS">No NavMesh/physics route added.</TASK>
    <TASK id="05" status="PASS">Native request ring unchanged.</TASK>
    <TASK id="06" status="PASS">Mock SDF unchanged.</TASK>
    <TASK id="07" status="PASS">NativeMinHeap unchanged.</TASK>
    <TASK id="08" status="PASS">SDF A* unchanged.</TASK>
    <TASK id="09" status="PASS">No blocking completion added.</TASK>
    <TASK id="10" status="PASS">String pulling unchanged.</TASK>
    <TASK id="11" status="PASS">Quality curve unchanged.</TASK>
    <TASK id="12" status="PASS">AUP route unchanged.</TASK>
    <TASK id="13" status="PASS">DTO layout unchanged.</TASK>
    <TASK id="14" status="PASS">No memclear added.</TASK>
    <TASK id="15" status="PASS">Telemetry route unchanged; editor graph fence now avoids scene scan.</TASK>
    <TASK id="16" status="PASS">Editor tuner fence now uses static owner count.</TASK>
    <TASK id="17" status="PASS">CSV bridge unchanged.</TASK>
    <TASK id="18" status="PASS">Debug gizmo fence now uses static owner count.</TASK>
    <TASK id="19" status="PASS">Scanner route unchanged.</TASK>
    <TASK id="20" status="PASS">Static verification updated; build guard still active.</TASK>
  </TASKS>
  <STRUCT_LAYOUT>No DTO layout changed in Loop 10.</STRUCT_LAYOUT>
  <H_PHI_VAULT>No new buffers or private native arrays added.</H_PHI_VAULT>
  <DEPENDENCY_GRAPH>Static active count mirrors evaluate/smooth `JobHandle` ownership; editor consumers fail closed without scene search.</DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No build launched.</COMPILE_GUARD>
  <DEAR_LIE>SDF scalar clearance remains the physics bypass.</DEAR_LIE>
</SELF_AUDIT>

---

# SHINOBU_304 Concurrency Fence Report - 2026-05-22

What was wrong:
- Static subagent audit found request-ring writes, result/debug reads, telemetry graph reads, and tuning writes could touch Vault buffers while voxel evaluate/smoothing jobs were scheduled.

What was done:
- Added pure `IsVoxelAStarJobActive()` fence on `PathFunnelNavmeshRuntime`.
- `TryEnqueueVoxelPathRequest` and `TryReadVoxelPathResult` now fail closed while voxel jobs are active.
- Late-frame telemetry fault readback skips until no voxel job owns telemetry.
- Editor tuner writes and telemetry graph reads defer while voxel jobs are active.
- Debug gizmo skips result/waypoint/closed-set reads while voxel jobs are active.

Cinematic cheats used:
- No blocking `Complete()` was introduced. The fix is a branch fence, not a synchronization stall.

Exact microseconds saved:
- Measured saved microseconds: 0. Build/profiler still blocked by CPU/compiler guard.
- Structural savings: avoids Unity safety-handle races without adding a managed staging queue or second result snapshot.

Verification:
- Runtime forbidden scan excluding Editor: 0 hits.
- Brace balance: runtime partial `62/62`, tuner `36/36`, gizmo `19/19`.
- Scoped trailing-whitespace scan: 0 hits.

<SELF_AUDIT pass="polish_loop_9">
  <TASKS>
    <TASK id="01" status="PASS">Newton audit consumed; no P0 compile/API issue reported.</TASK>
    <TASK id="02" status="PASS">Partial owner unchanged.</TASK>
    <TASK id="03" status="PASS">No new signal or sibling dependency introduced.</TASK>
    <TASK id="04" status="PASS">No NavMesh/physics route added.</TASK>
    <TASK id="05" status="PASS">Request ring remains native; writes are fenced while jobs own it.</TASK>
    <TASK id="06" status="PASS">Mock SDF unchanged.</TASK>
    <TASK id="07" status="PASS">NativeMinHeap unchanged.</TASK>
    <TASK id="08" status="PASS">SDF A* unchanged.</TASK>
    <TASK id="09" status="PASS">Time-slicing unchanged; no new blocking completion.</TASK>
    <TASK id="10" status="PASS">String pulling unchanged; debug reads fenced.</TASK>
    <TASK id="11" status="PASS">Continuous quality route unchanged.</TASK>
    <TASK id="12" status="PASS">AUP route unchanged.</TASK>
    <TASK id="13" status="PASS">Rollback DTO layout unchanged.</TASK>
    <TASK id="14" status="PASS">No full-buffer clear added.</TASK>
    <TASK id="15" status="PASS">Telemetry readback fenced until jobs complete.</TASK>
    <TASK id="16" status="PASS">Editor tuner write deferred during active jobs.</TASK>
    <TASK id="17" status="PASS">CSV bridge unchanged from Loop 8 writer lock repair.</TASK>
    <TASK id="18" status="PASS">Debug gizmo skips active jobs.</TASK>
    <TASK id="19" status="PASS">Scanner route unchanged.</TASK>
    <TASK id="20" status="PASS">Static self-audit updated; build still guarded.</TASK>
  </TASKS>
  <STRUCT_LAYOUT>No DTO layout changed in Loop 9.</STRUCT_LAYOUT>
  <H_PHI_VAULT>No new buffers or private native arrays added.</H_PHI_VAULT>
  <DEPENDENCY_GRAPH>Evaluate/smooth jobs still publish `JobHandle`s through `H8Memory.RegisterActiveJob`; owner/editor consumers fail closed until those handles complete.</DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No build launched; guard remains active.</COMPILE_GUARD>
  <DEAR_LIE>SDF scalar clearance remains the physics bypass; concurrency fix adds no simulation cost.</DEAR_LIE>
</SELF_AUDIT>

---

# SHINOBU_304 Subagent Audit Repair - 2026-05-22

What was wrong:
- Hot `FastTick` still had a route to base path-funnel `EnsureVaultBuffers -> GetGenerationHandle`.
- WFC grid handle was resolved by `TryGetGenerationHandle` inside the hot tick path.
- String-pulling one-sample LOS checked `t=1` and could accept a blocked interior shortcut.
- Result slot selection used `requesterHash % Results.Length`, so colliding requesters could overwrite completed paths.
- Smoothing ignored runtime waypoint capacity above `DefaultWaypointCapacity`.
- Editor tuner mutated tuning without an explicit vault writer fence.

What was done:
- Added cold `BootstrapPathFunnelCold`, cached WFC handle on bootstrap/hotswap, and made hot mutation views fail closed when cold handles are missing; WFC hot reads now use `TryReadHandle`.
- Added `TryReadVaultBuffer` and routed public read accessors through pure read handles.
- Changed result slot selection to linear probing with same-hash reuse, free/nonterminal reuse, and oldest-terminal eviction only when full.
- Changed LOS to interior sampling via `i / (samples + 1)`.
- Changed smoothing waypoint cap to `min(segmentCapacity, tuning.MaxWaypoints)`.
- Added UI Toolkit telemetry graph for nodes expanded and schedule-to-finalize micros; tuner writes now use `TryAcquireWriteLock` and `ReleaseWriteLock`.
- Changed debug gizmo reads to `TryReadHandle`.

Cinematic cheats used:
- The route still uses SDF scalar clearance as the physical lie. No NavMesh, `Physics.SphereCast`, mesh collider, or GameObject debug path was introduced.
- Low quality keeps one midpoint SDF sample; higher quality increases bounded SDF samples continuously.

Exact microseconds saved:
- Measured saved microseconds: 0. Build/profiler run remains blocked by guard.
- Structural savings: removed hot vault growth/acquire branch and hot WFC generation-handle lookup; collision repair adds only bounded result-table probes on search admission, not per-node expansion.

Verification:
- Runtime forbidden scan excluding Editor: 0 hits for NavMesh, physics casts, managed path queues, managed collection allocation tokens, LINQ, hot `foreach`, async/task, and scene-search APIs.
- Brace balance: `VoxelAStarContracts.cs 16/16`, `VoxelAStarJobs.cs 154/154`, `PathFunnelNavmeshRuntime.cs 97/97`, `PathFunnelNavmeshRuntime_VoxelAStar.cs 60/60`, tuner `33/33`, gizmo `17/17`, scanner `13/13`.
- Scoped trailing-whitespace scan: 0 hits in SHINOBU_304 files.
- Scoped tracked `git diff --check`: passed with line-ending warnings only.
- Build guard sampled CPU at 100.00%; latest guard refresh sampled CPU 100.00% with active `dotnet` PID 5468. Compile was not launched.

<SELF_AUDIT pass="polish_loop_7">
  <TASKS>
    <TASK id="01" status="PASS">Subagent audit consumed as independent static archaeology, no neighboring prompt merged.</TASK>
    <TASK id="02" status="PASS">Existing partial owner remains `PathFunnelNavmeshRuntime`.</TASK>
    <TASK id="03" status="PASS">No new SignalBus payload added; result authority remains DataVault DTOs.</TASK>
    <TASK id="04" status="PASS">Runtime NavMesh/physics route remains absent.</TASK>
    <TASK id="05" status="PASS">Request queue remains NativeArray ring.</TASK>
    <TASK id="06" status="PASS">Mock SDF remains cold bootstrap.</TASK>
    <TASK id="07" status="PASS">NativeMinHeap unchanged from Loop 6 repair; result collision bug fixed outside heap.</TASK>
    <TASK id="08" status="PASS">SDF A* route now prevents endpoint-only LOS acceptance during smoothing.</TASK>
    <TASK id="09" status="PASS">Hot tick no longer reacquires/grows vault handles.</TASK>
    <TASK id="10" status="PASS">String pulling samples interior points and respects actual waypoint capacity.</TASK>
    <TASK id="11" status="PASS">Continuous quality still scales node budget, heuristic, step sampling, and smoothing samples.</TASK>
    <TASK id="12" status="PASS">AUP output path unchanged.</TASK>
    <TASK id="13" status="PASS">DTOs remain explicit and blittable; build proof pending guard.</TASK>
    <TASK id="14" status="PASS">No new full-buffer memclear added.</TASK>
    <TASK id="15" status="PASS">Telemetry ring remains 300 entries and now has editor graph readout.</TASK>
    <TASK id="16" status="PASS">Editor tuner now uses vault write lock and live graph.</TASK>
    <TASK id="17" status="PASS">CSV bridge unchanged.</TASK>
    <TASK id="18" status="PASS">Debug gizmo uses pure read handles.</TASK>
    <TASK id="19" status="PASS">Scanner/report unchanged.</TASK>
    <TASK id="20" status="PASS">Static audit updated; compile blocked by CPU guard.</TASK>
  </TASKS>
  <STRUCT_LAYOUT>No DTO field layout changed in Loop 7.</STRUCT_LAYOUT>
  <H_PHI_VAULT>No private NativeArray ownership added. Cold-only handle cache added for WFC grid descriptor.</H_PHI_VAULT>
  <DEPENDENCY_GRAPH>Job scheduling/completion graph unchanged from Loop 6: FastTick schedules only; LateFrame finalizes only after `IsCompleted`.</DEPENDENCY_GRAPH>
  <COMPILE_GUARD>CPU sampled 100.00% with active `dotnet` PID 5468; no build launched.</COMPILE_GUARD>
  <DEAR_LIE>Scalar SDF checks remain the collision proxy; low-quality LOS is one midpoint read instead of a physics sweep.</DEAR_LIE>
</SELF_AUDIT>

---

# SHINOBU_304 Polish Pass Report - 2026-05-22

What was wrong:
- `NativeMinHeap` originally read `HeapPositions[nodeIndex]` before any search-stamp validation. That buffer is intentionally allocated as `UninitializedMemory`, so a fresh search could treat random memory as an open-set position.
- `FastTickVoxelAStar` could call `DispatcherJobFence.TryFinalizeCompleted` and therefore place a completion fence outside the late-frame completion window.
- `EnsureVoxelAStarViews` still routed through `EnsureVoxelAStarVaultBuffers`, which meant the scheduling path could re-enter a cold allocation/acquire route.
- Stale `NodeBudgetYield` flags could survive into a later success/failure result if not cleared at the start of the resumed evaluation pass.
- Default waypoint arena segmentation was too tight before the 4096 waypoint capacity adjustment; 64 result slots would have received only 8 waypoints each.

What was done:
- Added `HeapPosition` to the 32-byte search-stamped `VoxelPathNodeRecord` layout and made it the authoritative decrease-key position.
- Reworked `NativeMinHeap` to accept `Nodes` plus `SearchId`; it now reads heap position only from a matching `SearchId` node record and mirrors positions into `HeapPositions` only as debug/interop state.
- Seeded start/fresh neighbor records with `HeapPosition = -1` before heap insertion, preventing stale old-search positions from becoming current-search authority.
- Moved voxel A* job finalization out of `FastTickVoxelAStar`; FastTick schedules only when no voxel job is pending, and late-frame finalizes only after `JobHandle.IsCompleted`.
- Changed hot enqueue/view resolution to fail closed unless cold bootstrap already acquired vault handles.
- Cleared transient `NodeBudgetYield | TimeSliceOverBudget` at the start of an active resumed search pass.
- Added SDF sampling along diagonal neighbor steps using continuous quality-scaled 1..3 samples, reducing corner clipping in 26-neighbor movement.
- Added explicit smoothing-side SDF buffer validation and corrected `AverageNodesExpanded` to `total / elapsedSearchFrames`.

Cinematic cheats used:
- Collision remains a scalar SDF clearance proxy. No mesh collider, sphere cast, NavMesh, or dynamic obstacle graph was added.
- Corner safety uses 1..3 cheap SDF samples per step, not physics sweeps. Low quality keeps one sample; higher quality spends more ALU for smoother, safer cave motion.
- Async route truth is unchanged by quality. Quality only changes expansion cadence, heuristic inflation, and smoothing/clearance sample density.

Exact microseconds saved:
- Measured saved microseconds: 0. Build/profiler execution remains blocked by the active compiler guard.
- Structural savings: removed an O(grid cells) heap-position clear alternative; removed a hidden FastTick completion site; avoided physics scene synchronization entirely.
- Latest guard: CPU sampled 39.74%, but active `dotnet` PID 3104 existed. No compile launched.

Verification:
- Scoped `git diff --check` for SHINOBU_304 touched files passed; full repo diff-check still reports unrelated trailing whitespace in the already-modified `Docs/Tasks/CURRENT_BATCH.md`.
- Runtime scan excluding Editor returned 0 hits for `NavMeshAgent`, `NavMesh.CalculatePath`, `NavMeshPath`, `Physics.SphereCast`, `SphereCastAll`, `Queue<PathRequest>`, `new List<`, `new Dictionary<`, `System.Linq`, hot `foreach`, `async/await`, `Task<`, `GetComponent`, and `FindObjectOfType`.
- New-file brace balance: `VoxelAStarContracts.cs 16/16`, `VoxelAStarJobs.cs 151/151`, `PathFunnelNavmeshRuntime_VoxelAStar.cs 59/59`, editor tooling balanced.
- Asmdef check: `Hecton8.AI.Pathfinding` references Core/Core.Contracts/Core.Memory and Unity Burst/Collections/Jobs/Mathematics; no direct sibling runtime domain dependency was added.

<SELF_AUDIT pass="polish_loop_6">
  <TASKS>
    <TASK id="01" status="PASS">CLI extraction of `SHINOBU_304` assignment repeated; task count remains 20.</TASK>
    <TASK id="02" status="PASS">Existing `PathFunnelNavmeshRuntime` remains the owner via partial extension.</TASK>
    <TASK id="03" status="PASS">No new path-failure signal invented; result route remains Vault-backed DTOs.</TASK>
    <TASK id="04" status="PASS">Runtime NavMesh/physics route scan remains clean.</TASK>
    <TASK id="05" status="PASS">Request ingress remains flat NativeArray ring state, not managed queue.</TASK>
    <TASK id="06" status="PASS">Mock SDF remains cold Burst generation and is not in FastTick allocation route.</TASK>
    <TASK id="07" status="PASS">NativeMinHeap decrease-key authority repaired through search-stamped node positions.</TASK>
    <TASK id="08" status="PASS">SDF clearance checks now cover endpoint and diagonal step interior samples.</TASK>
    <TASK id="09" status="PASS">Time-sliced state resumes without stale transient yield flags.</TASK>
    <TASK id="10" status="PASS">String-pulling writes per-result waypoint segments.</TASK>
    <TASK id="11" status="PASS">Quality remains continuous: node budget, heuristic, and sample count are lerped/smoothed.</TASK>
    <TASK id="12" status="PASS">AUP local subtraction and double3 output route unchanged.</TASK>
    <TASK id="13" status="PASS">Deterministic Burst attributes and explicit DTO layouts unchanged; compile proof pending guard.</TASK>
    <TASK id="14" status="PASS">No full node/heap memclear introduced; uninitialized memory is guarded by SearchId and HeapPosition.</TASK>
    <TASK id="15" status="PASS">300-frame telemetry ring and dump route unchanged; exact Burst microseconds pending profiler.</TASK>
    <TASK id="16" status="PASS">UI Toolkit tuner still mutates Vault tuning DTOs.</TASK>
    <TASK id="17" status="PASS">CSV parser remains cold `ReadOnlySpan<byte>` ingestion.</TASK>
    <TASK id="18" status="PASS">Scene gizmo remains editor-only and reads Vault snapshots.</TASK>
    <TASK id="19" status="PASS">Scanner/report route remains present.</TASK>
    <TASK id="20" status="PASS">Polish static self-audit passed; compiler guard blocked build.</TASK>
  </TASKS>
  <STRUCT_LAYOUT>
    `PathRequestDTO=64`: `StartAUP@0 size24`, `EndAUP@24 size24`, `RequiredRadius@48 size4`, `RequesterEntityHash@52 size4`, `Flags@56 size4`, `_pad0@60 size4`.
    `VoxelPathNodeRecord=32`: `GCost@0 size4`, `FCost@4 size4`, `ParentIndex@8 size4`, `SearchId@12 size4`, `BestGoalDistanceSqBits@16 size4`, `HeapPosition@20 size4`, `Flags@24 size1`, padding `25..31 size7`.
    `VoxelPathHeapNode=24`: `NodeIndex@0 size4`, `FCost@4 size4`, `GCost@8 size4`, `TieBreak@12 size4`, padding `16..23 size8`.
  </STRUCT_LAYOUT>
  <H_PHI_VAULT>
    No private persistent NativeArray ownership was added. Runtime stores generation handles only.
    Vault BufferIDs: 73420..73436 for requests, ring, solver, nodes, heap, heap-position mirror, raw path, waypoints, results, telemetry, tuning, mock SDF, header, species profiles, profile count, CSV scratch, and closed debug.
  </H_PHI_VAULT>
  <DEPENDENCY_GRAPH>
    Consumes no incomplete upstream `JobHandle` in FastTick. Outputs `_voxelAStarEvaluateHandle` or `_voxelAStarSmoothHandle` through `H8Memory.RegisterActiveJob`.
    Finalization is late-frame only after `IsCompleted`; teardown is the only forced completion route.
    Burst jobs use `[NoAlias]` on non-overlapping NativeArray fields.
  </DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No build launched after active `dotnet` PID 3104 appeared. No direct sibling runtime asmdef dependency added.</COMPILE_GUARD>
  <DEAR_LIE>SDF scalar clearance and triangle-wave mock caves replace physics collision probing. Before: physics sweeps would be O(expanded nodes * physics scene sync). After: O(expanded nodes * bounded SDF samples) over contiguous arrays.</DEAR_LIE>
</SELF_AUDIT>

---

# SHINOBU_304 Polish Pass Report - Loop 14 - 2026-05-22

What was wrong:
- SHINOBU_304 originally used `71930..71946`; `71940..71946` already belong to `ShorelineFoamConstants` ocean rendering lanes.

What was done:
- Moved SHINOBU_304 Vault IDs to `73420..73436` in `H8Memory.BufferID`.
- Updated `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` and the SHINOBU_304 audit log with the collision rejection note.

Cinematic cheats used:
- Unchanged: SDF scalar clearance remains the path collision proxy.

Exact microseconds saved:
- Measured saved microseconds: 0. This is a sovereignty/corruption fix, not a speed path.
- Runtime impact: prevents cross-domain Vault lane aliasing between AI pathfinding and ocean shoreline foam.
- Guard: CPU 72.09%; no compile launched.

Verification:
- Exact `71930..71946` scan is clean for SHINOBU_304-owned files after the migration.
- Candidate `73420..73436` had no first-party BufferID owner in source before adoption; unrelated generated hash substrings are not Vault lanes.
- Build: `Docs/AgentLogs/Build_SHINOBU_304_loop14_incremental.log` failed with 6 external `CS0246` errors in `Gameplay/Combat/CombatDamageRuntime.cs`; no SHINOBU_304 file appeared in the captured error set.

---

# SHINOBU_304 Polish Pass Report - Loop 15 - 2026-05-22

What was wrong:
- A new request could leave an old terminal result visible for the same requester until the next time-sliced search wrote fresh state.
- Raw-pointer heap ownership was correct structurally but under-documented at the unsafe field site.
- Subagent flagged black-box dump managed I/O; the dump path is fault-only but still a managed diagnostic boundary.

What was done:
- Enqueue invalidates same-requester terminal results to `Queued` with finite/clamped radius.
- `BeginSearch` writes a non-terminal `Searching` result before expansion resumes.
- Added a source invariant for `NativeMinHeap` pointer lifetime and NativeArray safety tracking.
- Kept `TryDumpVoxelAStarBlackBox` as late-frame, post-job, fault-only I/O because the dump artifact is mandated.

Cinematic cheats used:
- Unchanged: SDF scalar clearance replaces physics collision probing; no NavMesh, collider sweeps, or managed nodes.

Exact microseconds saved:
- Measured saved microseconds: 0. No profiler run.
- Runtime cost added: bounded result-slot scan on enqueue only.
- Runtime cost avoided: no managed pending-result map, no result-table clear, no extra BufferID route.

Verification:
- Runtime forbidden scan remains 0 hits excluding Editor.
- Scoped trailing-whitespace scan passed for SHINOBU_304 runtime/log files.
- Brace balance: `VoxelAStarJobs.cs 154/154`, `PathFunnelNavmeshRuntime_VoxelAStar.cs 69/69`.
- Build not relaunched; Loop 14 build remains blocked by external Combat DTO compile wall.

---

# SHINOBU_304 Polish Pass Report - Loop 16 - 2026-05-22

What was wrong:
- Consumers could read result metadata but had no zero-GC public API for the smoothed waypoint segment.

What was done:
- Added `TryReadVoxelPathWaypoints(uint, Span<VoxelPathWaypointDTO>, out int)`.
- The accessor uses terminal result metadata, requires caller-owned memory, rechecks job activity, and copies only the validated waypoint segment.

Cinematic cheats used:
- Unchanged: SDF scalar path truth; no GameObjects, NavMesh, or physics casts.

Exact microseconds saved:
- Measured saved microseconds: 0.
- Avoided cost: no managed waypoint array, no public raw Vault handle leakage, no cross-domain direct buffer ownership.

Verification:
- Runtime forbidden scan remains 0 hits for NavMesh, physics cast, managed collection, and async tokens.
- Scoped trailing-whitespace scan passed.
- Brace balance: `VoxelAStarJobs.cs 154/154`, `PathFunnelNavmeshRuntime_VoxelAStar.cs 72/72`.
- Build not relaunched; external Combat DTO compile wall remains the known solution-build blocker.
