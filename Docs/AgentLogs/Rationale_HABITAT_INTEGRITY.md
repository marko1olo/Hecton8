# HABITAT_INTEGRITY Rationale

Status: PENDING VERIFICATION
Domain: ECHELON 6 Habitat & Vehicles / Fluid Incursion

## Decision 0 - Work Boundary

Problem: Bases need graph-based flooding math fixed without direct dependencies on systems owned by other active agents.
Solution: Limit runtime edits to habitat flooding math and use existing interfaces, registries, or signals discovered in code. Keep systems decoupled.
Rejected Alternatives: Hard references to unverified power, audio, or rendering classes; OOP room-node graph; per-frame physical water simulation.
Scalability potential: Low uses 10Hz scalar compartment levels; Middle adds stress and pump coupling; High adds richer visual waterline feeds; Ultra spends saved cycles on presentation, not particle truth.
Hardware Impact: i3/MX350 target avoids per-frame room simulation and object graph traversal; expected gain is measured only after Unity/profiler evidence.

## Decision 1 - Flood Transfer Clamp

Problem: Graph flood transfer forced `fillDelta01` to at least `0.01`, so water could move through open graph edges even when destination flood level was equal or higher. Capacity shrink from pressure compression could also leave raw water volume above current room capacity until the next write.
Solution: Require finite positive source water, positive source surface head over destination floor and destination surface, positive normalized fill delta, and cap transfer by source water plus destination remaining capacity before draining. Clamp cached water volume when room capacity is recomputed.
Rejected Alternatives: Particle/slosh simulation; per-frame water reconciliation; OOP room node objects; changing public graph APIs during active batch.
Scalability potential: Low stays 10Hz scalar rooms; Middle/High can spend saved correctness headroom on stronger waterline/audio/condensation fakes; Ultra can add richer presentation without changing the scalar truth.
Hardware Impact: i3/MX350 avoids invalid transfer churn and extra visual state updates. Measured microseconds saved are PENDING VERIFICATION; static expectation is no GC and negligible branch cost per traversed edge.

## Decision 2 - Native Room State, Edge Flags, and Blackbox

Problem: The flooding truth still lived mostly inside `BaseModule` instances. That made tasks like room flags, sealed directed edges, and last-300-frame crash telemetry implicit instead of inspectable.
Solution: Add SoA native room lanes (`RoomWaterLevels`, `RoomVolumes`, `RoomFlags`), directed `EdgeFlags`, a `NativeParallelMultiHashMap<int, HabitatFloodConnection>` connection index, and a fixed `NativeArray<HabitatFloodBlackBoxEntry>` telemetry ring owned by `HabitatGraphManager`.
Rejected Alternatives: Managed room graph classes; global telemetry-only reporting; per-frame physical fluid; direct dependencies on audio/world agents.
Scalability potential: Low uses scalar 10Hz SoA lanes and edge bytes; Middle adds richer scrubber/bulkhead consequences; High keeps shader waterline and creak presentation fed by compact state; Ultra can use the same room lanes for more aggressive screen-space water and structural VFX without changing the truth model.
Hardware Impact: i3/MX350 path avoids object graphs and alloc churn. Added work is bounded native writes during topology rebuild and slow tick. Measured gain/cost is PENDING VERIFICATION because full Unity compile is blocked by non-HABITAT dependencies.

## Decision 3 - Deterministic Sealing Over Random Breakage

Problem: The prompt asked for random hull degradation and auto-sealed bulkheads, but random damage fights replay/debug requirements and sealed state must be per-edge, not only per-room.
Solution: Use deterministic stress/breach routing already present in habitat integrity code and add per-edge `Sealed` flags. Powered rooms above 10% water seal connecting edges; traversal and pump component walks honor the same gate.
Rejected Alternatives: Random `UnityEngine.Random` damage selection; manual-only hatch state; new event IDs for every edge transition.
Scalability potential: Low has a byte mask branch per traversed edge; Middle/High can add animated hatch feedback; Ultra can spend saved cycles on visible seal hydraulics and warning UI.
Hardware Impact: i3/MX350 receives deterministic O(edge candidates) checks on the existing 10Hz path. Exact microseconds remain PENDING MEASUREMENT.

## Decision 4 - Verification Boundary

Problem: Unity compilation is currently blocked after the habitat patch, but the reported compiler errors are in Audio, Submarine, and World systems outside the assigned domain.
Solution: Validate habitat scripts with MCP, request Unity compile, capture the unrelated blocker list, and mark compile verification as dependency-blocked instead of editing outside the habitat domain.
Rejected Alternatives: Fixing `PlayerCriticalProceduralAudioRenderer`, `SubmarineStructuralGrid`, or `WorldChunkResidencyManager` from the habitat prompt; that violates domain ownership during a 20+ agent batch.
Scalability potential: None; this is process containment.
Hardware Impact: None. No microsecond claim is valid until the shared compile wall is cleared.

## Decision 5 - Burst Propagation Kernel

Problem: The status file marked the propagation task done, but the implementation still relied on managed graph traversal as the primary water transfer path.
Solution: Add `HabitatFloodPropagationJob` in `HabitatStressJobs.cs` and wire it through native SoA lanes in `HabitatGraphManager`. The job computes `delta = (LevelA - LevelB) * FlowRate * dt`, clamps against source water, destination capacity, and per-edge transfer cap, then writes native delta levels. The manager commits only changed rooms through existing `BaseModule` methods so visuals, fire suppression, and degradation side effects stay local.
Rejected Alternatives: Per-frame water truth; managed `RoomNode` objects; adding direct dependencies on power/audio/atmosphere systems; deleting existing side-effect paths during a shared compile wall.
Scalability potential: Low uses the 10Hz native pass and node budget slicing; Middle/High can feed richer waterline, creak, and warning presentation; Ultra can spend saved cycles on visual overkill without changing scalar flood truth.
Hardware Impact: i3/MX350 avoids OOP traversal and GC in the propagation math. Exact microseconds remain PENDING MEASUREMENT because the shared project compile is blocked outside HABITAT.

## Decision 6 - Conservation Fix and Job Overhead Trim

Problem: The first Burst propagation job could subtract multiple outgoing edges from the same source using the original source level each time. The post-job commit would clamp source drain, but destinations could still receive more volume than the source actually had.
Solution: Resolve source availability from base level plus pending outgoing deltas before every edge; resolve destination fill from base level plus all pending deltas for capacity. Incoming water does not become same-tick source budget, so propagation stays one graph step per slow tick and mass remains bounded. Use `job.Run()` instead of `Schedule().Complete()` because the 10Hz room graph is small and scheduling overhead would dominate.
Rejected Alternatives: Letting managed setters repair mass conservation after the fact; adding a second managed reconciliation pass; keeping dead managed transfer fallback.
Scalability potential: Low keeps deterministic one-step scalar transfer; Middle/High can increase node budget if needed; Ultra can spend cycles on shader/audio water presentation instead of deeper fluid truth.
Hardware Impact: i3/MX350 avoids worker scheduling overhead and impossible overdraw churn. Measured microseconds remain PENDING MEASUREMENT; current CLI compile is blocked outside HABITAT, profiler is not available.

## Decision 7 - Authoritative Edge Sealing

Problem: `HabitatFloodConnection` carried a copied `Flags` byte while live auto-seal state was stored in `_edgeFlags`. Once power/flood lockdown changed, the Burst job could read stale topology-time flags if any code trusted the connection copy. Edge flags were also being written before the current pass finished applying module emergency lockdown.
Solution: Make `HabitatFloodConnection` topology-only (`DestinationIndex`, `CsrEdgeIndex`, `FlowResistance`, padding) and make `_edgeFlags` the only sealed-edge truth for managed traversal and Burst propagation. Move flood edge publication into a second pass after `SetEmergencyBulkheadLockdown`, and clamp CSR edge ranges before reading adjacency.
Rejected Alternatives: Rebuilding the `NativeParallelMultiHashMap` every auto-seal pass; keeping duplicated flags and trying to synchronize all copies; using only per-module bulkhead state instead of directed edge bytes.
Scalability potential: Low keeps one byte gate per edge with no graph rebuild; Middle/High can add hatch animation or warning presentation from the same flags; Ultra can spend cycles on seal visuals without changing flood truth.
Hardware Impact: i3/MX350 avoids hash map rebuild churn and removes a stale branch from the job. Exact microseconds remain PENDING MEASUREMENT because Unity profiler is unavailable.

## Decision 8 - Cross-Domain Compile Unblock

Problem: The shared project build state changed during parallel-agent work. The previous `AcousticSurfaceResponse` blocker was already repaired, but `PDAMapTab` retained `[StructLayout(LayoutKind.Sequential)]` after `System.Runtime.InteropServices` was removed, causing the CLI build to fail outside HABITAT.
Solution: Restore the missing namespace import only. This preserves the existing compact PDA point-cloud value type and avoids behavior changes in UI, HABITAT, Audio, or World systems.
Rejected Alternatives: Removing the struct layout marker without owning the UI change; rewriting PDA point-cloud code; initializing unrelated Audio/World CS0649 warning fields blindly.
Scalability potential: None for HABITAT runtime. The repair only restores compile integrity so flood code can be verified. The UI struct remains a compact 16-byte GPU payload for low-tier PDA rendering.
Hardware Impact: 0 runtime us. One compile-contract import; no hot-path allocation, no shader dispatch change, no memory layout change.

## Decision 9 - Deterministic Flood Short-Circuit Escalation

Problem: The original HABITAT prompt included a recursive tail request to add electrical short-circuit risk at 50% flood fill. Existing graph flooding could fill rooms and existing power systems could react to flooded nodes, but `BaseModule` had no direct deterministic module-failure escalation at the 50% threshold.
Solution: Add a scalar deterministic roll in `BaseModule` using existing flood fill, `BaseModuleFailureMode.ShortCircuit`, `PowerNode.SetShortCircuited(true)`, and power-grid dirty marking. Chance starts when fill reaches 0.5 and reaches guaranteed trip at full flood. The roll is hashed from the module entity ID, so replay does not depend on Unity random state.
Rejected Alternatives: Per-frame electrical arcs; UnityEngine.Random chance; new EventBus event IDs; direct rewrites of `PowerGrid` distribution math. Existing power node short state is the correct cross-domain boundary.
Scalability potential: Low uses one slow-tick scalar check and existing brownout/flicker/audio paths. Middle/High/Ultra can layer richer spark VFX, warning UI, and power-panel shader feedback from the same short-circuit state without changing the flood truth.
Hardware Impact: i3/MX350 cost is a few integer hash ops and scalar clamps on slow tick or flood-volume change only. Measured microseconds remain PENDING MEASUREMENT because Unity profiler is unavailable.

## Decision 10 - Forced Flood Short-Circuit Parity

Problem: `ForceFlood()` set the room water volume to full capacity and marked the module flooded, but it did not evaluate the deterministic short-circuit path immediately. That allowed authored bulkhead overrides, catastrophic flood calls, and other full-flood entry points to bypass the 50% electrical hazard until the next slow tick.
Solution: Route `ForceFlood()` through `TryApplyFloodShortCircuit()` after flood state, visuals, tracked occupants, and spatial role are current. If the short-circuit handler fires, skip the duplicate flood clip, lockdown notification, and degradation sync because `TriggerCascadeFailure(BaseModuleFailureMode.ShortCircuit)` already performs the failure presentation and state publication.
Rejected Alternatives: Waiting for the next `SlowTick`; duplicating short-circuit code inside `ForceFlood()`; adding a new event ID for forced flood. The shared helper keeps one electrical rule and one power-grid boundary.
Scalability potential: Low gets immediate scalar failure parity with no new simulation. Middle/High/Ultra can attach richer spark and alarm presentation to the existing short-circuit state without changing flood truth.
Hardware Impact: i3/MX350 cost is one scalar branch path on a cold/full-flood call only. Measured microseconds remain PENDING MEASUREMENT because Unity profiler is unavailable.

## Decision 11 - Blackbox Dump Entry Size

Problem: `HabitatFloodBlackBoxEntry` is a packed 32-byte struct, but the binary dump writer skipped the `Reserved0` field. That produced 30-byte serialized entries, breaking fixed-entry parsing during postmortem review.
Solution: Explicitly initialize `Reserved0`, write it in `WriteFloodBlackBoxEntry`, and bump `FloodBlackBoxVersion` to 2 so old 30-byte dumps are distinguishable from corrected 32-byte dumps.
Rejected Alternatives: Leaving parsers to infer entry size from file length; adding JSON manifest work in a crash path; writing raw managed reflection/serialization. Fixed binary writes are cheaper and deterministic.
Scalability potential: Low tier keeps the same 300-entry native ring and anomaly-only file IO. Middle/High/Ultra can build richer postmortem tooling over the same stable binary contract without touching runtime flood truth.
Hardware Impact: 0 hot-path cost. The only added write is two bytes during anomaly dump serialization, outside normal frame execution. Measured runtime microseconds remain PENDING MEASUREMENT.

## Decision 12 - Edge Flags in Flood State Hash

Problem: The flood blackbox state hash covered room flags, levels, volumes, flooded room count, stress, and total water volume, but it did not include directed edge seal flags. A bulkhead or auto-seal state regression could therefore leave the postmortem hash unchanged when room volumes had not moved yet.
Solution: Hash the live `_edgeFlags` count and byte values inside `SyncFloodRoomStateSnapshot`. This keeps `_edgeFlags` as the single seal authority and makes sealed-edge changes visible in the same fixed blackbox signature.
Rejected Alternatives: Logging individual edge transitions as managed text; duplicating edge flags into `HabitatFloodConnection`; rebuilding the connection map for telemetry. Hashing the existing native byte lane is cheaper and preserves the topology/authority split.
Scalability potential: Low hashes one byte per directed edge at the existing 10Hz snapshot cadence. Middle/High/Ultra can decode richer diagnostics from the same state hash without changing runtime water propagation or visual presentation.
Hardware Impact: i3/MX350 cost is a bounded O(edge count) byte hash during slow tick only. Exact microseconds remain PENDING MEASUREMENT because Unity profiler is unavailable.

## Decision 13 - Restored Flood Volume Consistency

Problem: Restore/authoring APIs could mark a module as flooded while leaving `waterVolumeM3` at zero. Visual code could still resolve a fallback 100% flood level, but `HabitatGraphManager` snapshots read `WaterVolumeM3`, so the graph could treat a visually flooded room as dry.
Solution: Add `SyncWaterVolumeToFloodFlag(bool)` in `BaseModule` and call it from the public `IsFlooded` setter, `SetIntegrityState(float)`, and `SetState(...)`. The helper fills empty flooded modules to capacity, clamps oversized restored volumes, preserves finite partial volumes, and clears scalar water volume when state is restored dry.
Rejected Alternatives: Making `HabitatGraphManager` infer fallback volume from visual flood state; forcing all restore paths through `ForceFlood()` and triggering side effects; adding a save-format change during the shared batch. The local scalar helper keeps load/authoring state deterministic and side-effect bounded.
Scalability potential: Low keeps graph truth as one scalar volume per room. Middle/High/Ultra can still drive richer shader waterlines from the same fill data without adding physical water truth.
Hardware Impact: 0 hot-path cost. Work runs only on state restore/authoring/property set paths. Measured runtime microseconds remain PENDING MEASUREMENT.

## Decision 14 - Scalar Flood Truth Over Visual Cache

Problem: `SyncFloodRoomStateSnapshot` could merge `BaseModule.FloodLevel01` into graph truth by taking the max of scalar water volume and cached visual flood level. That let presentation fallback create synthetic graph water and could make auto-seal or lockdown decisions from a rendered state instead of drainable volume.
Solution: Make graph room water level derive from `WaterVolumeM3 / ResolveFloodCapacityM3()` only. Emergency lockdown and auto-seal checks now call `ResolveAuthoritativeRoomWaterLevel01`, which reads the native scalar snapshot when available and falls back to scalar module volume only.
Rejected Alternatives: Trusting shader/visual flood cache; forcing visual state writes before graph snapshots; inferring graph volume from `IsFlooded` inside the graph manager. Those options mix presentation with conservation math.
Scalability potential: Low keeps one scalar water volume as truth. Middle/High/Ultra can still use richer shader waterlines, screen-space overlays, and flood VFX without changing flow mass.
Hardware Impact: No allocations. Runtime cost is a scalar capacity divide on edge/state publication paths already running at slow cadence. Exact microseconds remain PENDING MEASUREMENT.

## Decision 15 - Bounded Native Graph Traversal

Problem: Pump drainage, fungal target search, anchor reachability, component power island walks, and dirty-region rebuild paths could write traversal queues from `_nodeCount` or `NodeCount` assumptions alone. If topology/list/native capacities diverged during rebuild or partial initialization, the native queue or CSR edge reads could overrun.
Solution: Clamp graph walks to the minimum of logical node count, managed module list count, and native buffer lengths. Clamp CSR edge ranges before neighbor reads, guard queue tail against the safe node count, and write `FloodBlackBoxTraversalOverflowFlag` when saturation occurs. `HabitatDirtyRegionRebuildJob` now follows the same bounded node/edge contract.
Rejected Alternatives: Trusting topology rebuild invariants; dynamically resizing queues during runtime traversal; replacing native traversal with managed `Queue<T>`. Those options either hide corruption, allocate, or break Burst-friendly behavior.
Scalability potential: Low fails closed with bounded traversal and telemetry. Middle/High/Ultra can add richer diagnostics from the same blackbox flag without changing graph truth or simulation cadence.
Hardware Impact: Branch-only overhead on existing graph walks, no GC, and anomaly-only blackbox flag writes. Exact microseconds remain PENDING MEASUREMENT.

## Decision 16 - Bounded Rupture Cascade Publication

Problem: Rupture cascade stress and emergency lockdown publication still trusted logical graph counts in places where they read `_nodes`, `_edgeOffsets`, `_edgeDestinations`, or `_anchorReachability`. A partial topology rebuild could therefore make non-flood integrity presentation read outside native capacities even after the flood traversal paths were hardened.
Solution: Clamp rupture cascade source nodes by `_nodes.Length` and `_edgeOffsets.Length - 1`, clamp edge ranges by `_edgeDestinations.Length`, and make emergency lockdown publication tolerate missing or shorter anchor reachability buffers before writing reserved node state.
Rejected Alternatives: Assuming topology rebuild completion before every publish; forcing a full graph rebuild before rupture stress; adding managed fallback lists. Bounded native reads preserve current architecture and fail closed.
Scalability potential: Low keeps deterministic stress propagation without memory-risk spikes. Middle/High/Ultra can still spend cycles on rupture VFX/audio because topology safety is not tied to visual richness.
Hardware Impact: Branch and `math.min` overhead only on existing publish/cascade paths. No allocations. Exact microseconds remain PENDING MEASUREMENT.

## Decision 17 - Burst Job Topology Fault Split

Problem: The propagation Burst job treated malformed connection indices as non-finite math, which would consume the same diagnostic lane as NaN/Inf faults. The dirty-region rebuild job also returned early when edge count was zero, so isolated dirty nodes could miss island relabeling.
Solution: Add `InvalidConnectionCount` to `HabitatFloodPropagationSummary`, route those faults to `FloodBlackBoxTopologyInvalidFlag`, and keep the binary dump slot reserved for non-finite conditions. Dirty-region rebuild now clamps edge-offset capacity to zero or higher and still visits isolated nodes with zero valid edges. Waterline shader updates now check every source NativeArray length before read. `BuildEdgeRecords()` now clears and prefixes CSR offsets only to the actual `_edgeOffsets`/`_edgeWriteCursor` capacity.
Rejected Alternatives: Dumping topology faults as non-finite blackbox events; trusting CSR offset arrays in Burst or topology rebuild; requiring edge presence to rebuild an island; adding managed validation queues. These would blur diagnostics, risk native OOB reads, or add allocation pressure.
Scalability potential: Low fails closed with branch-only bounds and compact blackbox flags. Middle/High/Ultra can add richer debug tooling from the same topology-invalid bit without changing scalar flood truth or rendering quality.
Hardware Impact: i3/MX350 impact is a few integer bounds checks on existing Burst jobs and cold topology rebuild. No GC, no physical simulation, no new runtime allocations. Exact microseconds remain PENDING MEASUREMENT because the user prohibited a build/profiler pass after this patch.

## Decision 18 - Temporary Bypass Rebuilds CSR Authority

Problem: Live temporary bypass insertion shifted CSR arrays in place. That risks stale `EdgeRecord` CSR indices, stale flood `NativeParallelMultiHashMap` entries, and misaligned `_edgeFlags` when an edge is inserted before existing directed edges.
Solution: Keep temporary bypass as a cold topology edit: append the edge record, then call `BuildEdgeRecords()` so CSR offsets, destinations, resistance, edge flags, edge record indices, and flood connections are rebuilt from the same edge-buffer authority before publish passes run.
Rejected Alternatives: Manually shifting every parallel CSR/flood/flag/index container; leaving flood connections stale until the next full rebuild; using managed adjacency as fallback. Rebuild is colder and correct.
Scalability potential: Low devices pay the rebuild only on rare bypass registration, not during 10Hz flooding. High/Ultra get consistent topology for visual overkill without separate sync paths.
Hardware Impact: Cold-path topology rebuild only; no per-frame or per-slow-tick cost. Exact microseconds remain PENDING MEASUREMENT.

## Decision 19 - Fail-Closed CSR Rebuild

Problem: `BuildEdgeRecords()` still assumed every `EdgeRecord` endpoint and CSR write cursor was valid once the normal authoring pipeline produced it. Corrupt or stale edge records could therefore poison prefix sums or directed-edge writes during a full rebuild.
Solution: Validate edge endpoints before prefix sums and CSR writes, sever/ignore invalid records, guard forward/reverse CSR write indices, and set `_edgeCount` from actually written directed edges instead of only the theoretical logical count. Clamp node record, degradation, and graph-kernel publication counts to actual native/list capacities before reads.
Rejected Alternatives: Trusting the edge buffer forever; repairing bad endpoints by guessing a nearest node; using managed validation collections. Failing closed preserves deterministic topology without allocations or hidden graph mutation.
Scalability potential: Low avoids native memory faults under partial/stale topology. Middle/High/Ultra can keep richer rupture/bypass presentation because graph safety is not dependent on visual state.
Hardware Impact: Branch-only rebuild/publication overhead on topology rebuilds and rare bypass edits. No hot-path allocation. Exact microseconds remain PENDING MEASUREMENT.

## OMEGA POLISH CHANGES

Dear Lie Audit:
No new physical water simulation was added. The honest calculation remains scalar room fill and pressure head. Existing pressure ingress already uses a LUT/linear excess fallback for low tiers and interpolates only on High/Ultra. No unconditional `math.sqrt()` or `math.normalize()` was introduced.

Cinematic Cheats Replaced/Retained:
Retained scalar 10Hz compartment levels, shader waterline buffer, local distortion volume, and cooldown-gated structural stress audio. Rejected particles, slosh, and random hull degradation. No additional honest calculation needed replacement beyond the prior pressure-root LUT path.

Scalability Matrix:
Low/Mid use capped traversal budgets and LUT pressure root. High/Ultra can spend cycles on interpolated pressure-root and richer presentation fed by the same SoA lanes. All habitat flood state stays on SlowTick/topology rebuild, not per-frame truth.

Zero-GC Purge:
No managed collections were added to the hot path. New allocations are persistent native containers in `AllocateNativeBuffers`/`EnsureEdgeCapacity`. Crash dump file IO remains anomaly-only. Removed string interpolation from the blackbox dump warning path.

Cache Locality and Alignment:
Changed `HabitatFloodBlackBoxEntry` to an explicitly packed 32-byte struct (`Frame`, compact counts, reserved alignment, stress/water scalars, flags, state hash) and corrected the binary writer to emit the reserved alignment field. Room water levels, volumes, flags, and edge flags are linear NativeArrays.

Silo and Build Health:
Edited habitat-domain files only: `BaseModule.cs`, `HabitatGraphManager.cs`, and `HabitatStressJobs.cs`, plus HABITAT status/log documents. Latest `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly` is blocked outside this domain by `World/AcousticOcclusionUtility.cs(955,25)`: missing `AcousticSurfaceResponse`. Unity MCP validation reports `no_unity_session`, so Unity console/profiler verification is still pending. Status remains PENDING VERIFICATION; `VERIFIED MASTER GRADE` would be false.

Build Health Update:
`AcousticSurfaceResponse` is now present in `World/AcousticOcclusionUtility.cs`. A later UI compile wall in `PDAMapTab.cs` was unblocked by restoring `System.Runtime.InteropServices` for its existing `[StructLayout]` marker. Latest `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` succeeds with 0 errors and 5 non-HABITAT CS0649 warnings in Audio/World. Unity MCP validation remains unavailable (`no_unity_session`), so Unity console/profiler verification is still pending.

Short-Circuit Update:
Flood fill at 50% now has deterministic module-level short-circuit risk. Latest `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` succeeds with 0 errors and 0 warnings. Unity MCP validation still returns `no_unity_session`, so runtime/profiler proof remains pending.

Forced Flood Parity Update:
Full forced flood now evaluates the same deterministic short-circuit path immediately and suppresses duplicate audio/notification/sync when that handler fires. Latest `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly` succeeds with 0 errors and 0 warnings. Unity MCP validation still returns `no_unity_session`, so runtime/profiler proof remains pending.

Final Git Diff:
Tracked code diff stat for habitat files: `BaseModule.cs` 234 lines changed; `HabitatGraphManager.cs` 1164 lines changed; `HabitatStressJobs.cs` 189 lines changed at scan time. Docs under `Docs/Tasks` and `Docs/AgentLogs` are newly created/updated in the working tree and may be untracked depending on the repo index.

Blackbox Serialization Update:
`WriteFloodBlackBoxEntry` now writes `Reserved0`, and `FloodBlackBoxVersion` is now 2. A clean `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly` was observed after this patch, but the latest shared build now fails outside HABITAT in dirty `HectonPlayerMovement.cs` player-kinematics edits (`_registeredPostFixedTick`, `CompletePlayerKinematicsDragJob`, `SchedulePlayerKinematicsDragJob`). Unity MCP console read still reports `no_unity_session`, so Unity Console, Play Mode, GC, and profiler proof remain pending.

Edge-Flag Hash Update:
Flood blackbox state now includes directed `_edgeFlags` count and bytes. This adds sealed-edge transition visibility to the postmortem signature without duplicating connection state or changing propagation behavior. Latest CLI verification is clean with `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly /nr:false /p:UseSharedCompilation=false`; Unity MCP console is reachable and currently reports a non-HABITAT Fauna compile error in `LeviathanTentacleVerletSolver.cs`, so Unity Console clean, Play Mode, GC, and profiler proof remain pending.

Restored Flood State Update:
Restore/authoring flood flags now synchronize scalar water volume before graph snapshots consume `WaterVolumeM3`. Latest `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly /nr:false /p:UseSharedCompilation=false` succeeds with 0 errors and 0 warnings. Runtime/profiler proof remains pending because the current Unity editor console is blocked outside HABITAT by `Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs(22,128)`: missing `IDisposable.Dispose()`.

Latest Verification Update:
CLI build is clean with 0 errors and 0 warnings under `/nr:false /p:UseSharedCompilation=false`. MCP `validate_script` returns 0 diagnostics for `BaseModule.cs`, `HabitatGraphManager.cs`, and `HabitatStressJobs.cs`. Unity MCP `read_console` is reachable and returns one current compile error outside this domain: `Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs(22,128)` does not implement `IDisposable.Dispose()`. Editor state is idle but `ready_for_tools=false` due to stale status. Status stays PENDING VERIFICATION; no Unity Console clean, Play Mode, GC, or profiler claim is valid.

Current Verification Update:
After the scalar flood-truth and bounded traversal passes, the first CLI build retry failed on a transient file lock: `Temp/obj/EasySave3/EasySave3.dll` was in use by another process. A second retry of `dotnet build Hecton8.Core.csproj --no-restore -v:quiet -clp:ErrorsOnly /nr:false /p:UseSharedCompilation=false` succeeded with 0 errors. Warning output from the minimal build is in URP/GPUInstancer/Crest/ShaderGraph package projects, not HABITAT files. `git diff --check` reports only CRLF normalization warnings on touched HABITAT/doc files. Unity MCP `validate_script` and `read_console` currently return `no_unity_session`, so Unity Console clean, Play Mode, GC, and profiler proof remain pending.

Rupture Cascade Bounds Update:
`ApplyRuptureCascadeStressFromRupturedNodes` now clamps node and CSR edge reads to native buffer lengths, and `PublishEmergencyLockdownState` now handles missing or shorter anchor reachability when writing reserved node state. Earlier CLI verification is historical only; current verification is PENDING after the latest no-build static patch.

Burst Job Hardening Update:
`HabitatFloodPropagationJob` reports malformed directed connections separately from non-finite math, dirty-region rebuild preserves isolated dirty-node work with zero valid edges, and waterline shader updates guard every source lane length before reads. Per user instruction, no build was run after the latest patch; verification is static-only and PENDING.

Temporary Bypass CSR Update:
Live temporary bypass registration now rebuilds CSR/flood connection state from the edge buffer instead of shifting CSR arrays in place. Earlier CLI verification before the latest no-build static patch is historical only; current verification remains PENDING.

Fail-Closed CSR Rebuild Update:
`BuildEdgeRecords()` now severs invalid endpoint records, guards CSR write indices, derives `_edgeCount` from written directed edges, and bounds CSR clear/prefix/write-cursor initialization by actual native capacities. `BuildNodeRecords`, `PublishDegradationState`, and `PublishGraphKernel` now clamp publication counts to actual native/list capacities. Per user instruction, no build or `dotnet build` was run after the latest patch. `git diff --check` reports only CRLF normalization warnings. Unity Console clean, Play Mode, GC, and profiler proof remain pending.
