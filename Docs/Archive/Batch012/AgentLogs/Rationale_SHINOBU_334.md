# SHINOBU_334 Rationale - DRONE_FLEET_NAVIGATION_KERNEL

Date: 2026-05-22
Status: CODED / COMPILE BLOCKED BY EXTERNAL WALL

## Pre-Code Analysis
Problem: Existing drone navigation authority was unknown; implementing a new manager could create duplicate route ownership.
Solution: rg archaeology found the active owner as `DroneFleetManager`, with `DroneCognitionJob` and `DroneFleetNavigationKernel` already holding drone-specific Burst jobs. I patched those routes in place.
Rejected Alternatives: Standalone `HectonDroneNavigationManager`; direct hot `GlobalRegistry` lookups; dependency on sibling agent runtime internals.
Scalability potential: Low keeps smaller solve budgets and greedy heuristic; Middle increases nodes; High keeps near-A* paths; Ultra can spend extra budget on smoother debug visualization without changing gameplay truth.
Hardware Impact: Expected i3/MX350 gain is from avoiding main-thread NavMesh/Physics sync and keeping path data contiguous. Measured gain: 0 us, no profiler run.

Problem: `DroneStateDTO` layout did not match the assignment; battery and flags were swapped and field names were legacy.
Solution: `DroneStateDTO` now uses explicit 64B layout: `CurrentAUP@0`, `Velocity@24`, `CurrentTargetHashID@36`, `TaskStateFlags@40`, `BatteryLevel@44`, private uint pads at `48/52/56/60`.
Rejected Alternatives: Sequential layout, public padding writes, property wrappers, or compatibility aliases that hide wrong offsets.
Scalability potential: All tiers share the same state truth; quality only changes cadence/budget, not DTO identity.
Hardware Impact: 64B stride supports cache-line snapshots and ARM64-aligned double3 reads. Estimated snapshot stride win: 6 us per 512-drone buffer copy.

Problem: `ScheduleDroneMacroAStar` only cleared waypoint buffers; A* existed but never executed, and steering ignored macro waypoints.
Solution: The manager now schedules `DroneMacroAStarJob`; `DroneCognitionJob.TryResolveMacroWaypoint` reads the AUP waypoint lane and resolves local deltas by subtracting the drone AUP first.
Rejected Alternatives: Direct target steering only; managed route lists; Transform/MonoBehaviour pilots.
Scalability potential: Low solves fewer drones per frame; Middle resumes partial route states; High/Ultra can run higher solve budgets and tighter string pull.
Hardware Impact: Expected 55 us saved per 50-drone tick versus Transform/NavMesh steering path; measured gain 0 us.

Problem: Full 3D A* per drone can spike if all nodes are expanded in one frame.
Solution: Added `DroneAStarPersistentState[512]` plus per-drone heap/g-cost/cameFrom/nodeState slices sized `512 * 512`. `MaxNodesExpandedPerDrone` gates each frame and active searches resume by search hash, open count, best node, and goal node.
Rejected Alternatives: One-frame solve; clearing global scratch per drone; coroutine delay without preserving open/closed sets.
Scalability potential: Low quality resumes over more frames; Middle increases node count; High/Ultra approaches complete A* sooner.
Hardware Impact: Estimated 120 us spike avoided on low-end silicon under a 50-drone route burst; measured gain 0 us.

Problem: A* neighbor expansion previously only used a binary wall test.
Solution: `MockSDFGrid.SampleClearance` and `IsBlockedForRadius` now compare SDF clearance against `RequiredDroneRadius`; the path job rejects undersized cells and string-pulls only through line samples that preserve clearance.
Rejected Alternatives: `Physics.SphereCast`, collider probing, Bezier smoothing after an invalid path.
Scalability potential: Low samples up to 16 line points on accepted path candidates; Ultra can raise solve budget without changing obstacle authority.
Hardware Impact: Estimated 45 us saved per path solve versus physics queries; measured gain 0 us.

Problem: Absolute world coordinates lose precision when cast to float before subtraction.
Solution: Destination and waypoint math subtracts `double3` AUP first, then converts local deltas to `float3`. `PathWaypointDTO` now carries `double3 PositionAUP`.
Rejected Alternatives: Float-only waypoint output; camera-relative local-only path truth.
Scalability potential: Same truth route across Low/Middle/High/Ultra.
Hardware Impact: Prevents precision repair work later; speed gain is 0 us, correctness-only.

Problem: Quality scaling was hard-pinned because `ResolveAuthoritativeQualityWeight()` returned `1f`.
Solution: It now reads `HomeostasisBrain.GlobalQualityWeight`; A* solve count, node budget, and heuristic weight are continuous lerps. `Reserved0` acts as a cold designer heuristic override and CSV key `HeuristicWeight`.
Rejected Alternatives: binary hardware switches; always-ultra solve.
Scalability potential: Low uses greedy `2.25` heuristic and smaller node slices; Ultra trends to `1.05` heuristic and larger slices.
Hardware Impact: Estimated 70 us shed on low quality route pressure; measured gain 0 us.

Problem: The project needs hard proof that drone OOP pathing is gone.
Solution: Added `OOP_Drone_Nav_Scanner`, stable SHINOBU_334 report, and idempotent aggregate AI report upsert. Static rg found 0 runtime hits for NavMesh/Physics/managed path queues in Construction/AI/Vehicles excluding Editor.
Rejected Alternatives: Chat-only claim; scanner that overwrites aggregate reports without a stable copy; append-only aggregate section that duplicates `shinobu334DroneNavigation` on repeated runs.
Scalability potential: Scanner is editor-only and does not affect runtime tiers.
Hardware Impact: 0 us runtime.

Problem: Black-box dump path did not include the required SHINOBU_334 artifact.
Solution: Drone fleet dump now also writes `Docs/AgentLogs/Dump_SHINOBU_334.bin` from the existing 300-frame blackbox ring.
Rejected Alternatives: New managed crash buffer; chat explanation after crash.
Scalability potential: Same telemetry route across all tiers.
Hardware Impact: 0 us normal runtime; dump only on fault.

Problem: Build verification hit a project-wide compile wall outside this agent's dependency boundary.
Solution: One guarded build was launched only after CPU dropped to 17.87% and no dotnet/csc was active. Build failed on external missing partials/contracts. I did not alter those domains.
Rejected Alternatives: Rebuild spam; editing VRSomatic/Submarine/Gyro/Combat/Metabolism contracts outside assigned domain.
Scalability potential: None.
Hardware Impact: Build consumed one guarded compile attempt; no runtime effect.

## Ultra Mandate Polish Pass

Problem: Drone docking obstacle abort still relied on Unity `RaycastCommand`/`RaycastHit`, which preserved PhysX scene-query dependency inside the drone navigation owner.
Solution: Removed the docking raycast buffers, handles, reset completion, and scheduling path. Docking spline aborts now sample `MockSDFGrid` with bounded segment taps and abort when clearance falls below the resolved drone radius.
Rejected Alternatives: Keeping deferred `RaycastCommand` because it is asynchronous; wrapping PhysX in another helper; MeshCollider/SphereCast corridor probes.
Scalability potential: Low samples fewer spline taps through existing segment cap; Middle/High/Ultra can afford tighter SDF cadence without changing path truth.
Hardware Impact: Estimated 18 us saved on i3/MX350 during a 192-probe docking burst; measured gain 0 us, no profiler run.

Problem: One global drone radius was insufficient for micro welders versus mining drones, causing either wall clipping or over-wide rejection.
Solution: Reused `DroneChassisSpecDTO@36` as `ClearanceRadiusMeters`, kept the struct 64B, parsed it from cold CSV, applied fallback radii per chassis, encoded the chosen value into `HeadlessDroneState.ReservedTail0`, and resolved it in `DroneMacroAStarJob`.
Rejected Alternatives: Adding a second DTO lane; changing `DroneStateDTO`; branching by managed drone prefab in Burst; binary low/high clearance modes.
Scalability potential: Clearance is gameplay truth and remains stable across quality tiers; quality only changes solve cadence and node budget.
Hardware Impact: 0 us estimated speed gain; reduces stuck recovery churn and false path failures on weak hardware.

Problem: A* failures had no first-party hot signal for downstream diagnostics, inviting a custom managed "stuck" event.
Solution: Reused existing `SignalBus<SystemGlitchSignal>` with source hash `0x53333334` and reason `34`, throttled once per frame from A* telemetry.
Rejected Alternatives: New `DroneStuckSignal`, C# event multicast, log spam from the simulation loop.
Scalability potential: Signal volume is bounded and independent of drone count after per-frame throttle.
Hardware Impact: Estimated 3 us saved per avoided managed dispatch path; measured gain 0 us.

Problem: Drone helper paths still performed direct `GlobalRegistry` reads and render fallback used `Camera.main`; repair spark VFX cast absolute AUP to `float3`.
Solution: Cached Construction/Player/Submarine/Fluid context refs during cold `EnsureInitialized()`, removed direct hot reads from task/formation/fluid helpers, removed `Camera.main`, and emitted VFX spark hit points through `AbsoluteUniversePosition.ToRuntimeFloat3()`.
Rejected Alternatives: Scene search fallback, absolute `double3 -> float3` cast, direct registry reads in recurring helpers.
Scalability potential: All tiers keep the same authority route; low devices avoid scene search and global registry churn, high tiers spend cycles on visuals instead.
Hardware Impact: Estimated 1-2 us saved per helper cluster; AUP fix is correctness-only at 100km scale.

Problem: Reports overstated scanner precision and did not prove RaycastCommand/RaycastHit eradication; binary payload ledger had no SHINOBU_334 boundary entry.
Solution: Scanner now describes itself as a comment/string-stripped regex pass, treats RaycastCommand/RaycastHit as forbidden tokens, escapes JSON control characters, and emits route/layout fields. `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` now records SHINOBU_334 BufferIDs, ABI, runtime route, scalability, Dear Lie, fault, and Data Monolith status.
Rejected Alternatives: Roslyn dependency inside the editor scanner; chat-only audit; leaving ledger ownership implicit.
Scalability potential: Documentation/proof only, no runtime tier impact.
Hardware Impact: 0 us runtime.

## 2026-05-23 Ultra Mandate Polish Pass 2

Problem: Task 16 required a modern UI Toolkit tuner with a live graph, but the previous editor facade still used `IMGUIContainer`, `EditorGUILayout`, and button-driven IMGUI controls.
Solution: Rebuilt `FleetAutomationTunerWindow` with UI Toolkit sliders, toggles, buttons, and a fixed-array `Painter2D` telemetry graph for nodes expanded, steering delay, and active avoidance vectors.
Rejected Alternatives: Keeping IMGUI because it was editor-only; adding runtime debug UI; creating a separate tuner window that would split designer authority.
Scalability potential: Low-tier devices pay 0 runtime cost because the graph is editor-only; mid/high/ultra designers can raise path budgets and visually inspect avoidance pressure without recompiling C#.
Hardware Impact: 0 us runtime. Editor-only managed label strings remain outside hot play simulation.

Problem: Designer slider writes needed to mutate the Vault-backed tuning DTO through `UnsafeUtility.AsRef`, not rely on a NativeArray indexer copy path.
Solution: `DroneFleetManager.ApplyDroneFleetTuningConstants` now resolves the tuning buffer pointer with `GetUnsafePtr()` and assigns through `UnsafeUtility.AsRef<DroneFleetTuningConstants>`.
Rejected Alternatives: ScriptableObject tuning authority, `NativeArray[0] = value` writeback, or a managed singleton settings object.
Scalability potential: Quality/tuning changes remain continuous scalar controls and do not alter DTO layout, authority route, or save identity.
Hardware Impact: 0 us measurable runtime; removes ambiguity around defensive copy mutation in the cold designer bridge.

Problem: `NativeDisableParallelForRestriction` suppressions were technically correct but undocumented, which makes future race review impossible under the project rules.
Solution: Added local three-part safety justifications beside each suppressed group in `DroneFleetNavigationKernel` and `DroneCognitionJob`: one Execute index owns one row, cross-index writes use Interlocked/CAS helpers, and lanes are distinct Vault buffers with `[NoAlias]`.
Rejected Alternatives: Removing the suppressions and breaking required cross-index atomic lanes; leaving comments in the log only.
Scalability potential: No tier impact. Reviewability prevents future defensive synchronization that would cost low-end silicon.
Hardware Impact: 0 us runtime; preserves Burst vectorization assumptions.

Problem: Two forced completions exist for reset and origin shift. They were not documented as cold sync boundaries and could be misread as hidden gameplay-frame `.Complete()` calls.
Solution: Added explicit reset/origin-shift sync-boundary comments in `DroneFleetManager`, preserving behavior until the dispatcher exposes a non-blocking rebase phase.
Rejected Alternatives: Changing origin-shift behavior speculatively; deleting reset safety; adding normal-frame completes.
Scalability potential: Normal frame path stays handle-driven; rare rebase/reset may still block by design.
Hardware Impact: 0 us normal runtime.

Problem: `OOP_Drone_Nav_Scanner` reported "OOP NavMesh Calls Eradicated" unconditionally, making a failure report lie.
Solution: Scanner status and summary now depend on `forbiddenHitCount == 0`; failures emit `FORBIDDEN DRONE NAV TOKENS FOUND`.
Rejected Alternatives: Trusting chat/manual review; leaving unconditional PASS wording because current scan is clean.
Scalability potential: Documentation/proof only.
Hardware Impact: 0 us runtime.

## 2026-05-23 Ultra Mandate Polish Pass 3

Problem: Render shader IDs were cached through property getters that mutated static fields with `Shader.PropertyToID`, violating the project doctrine that read accessors must be pure.
Solution: Removed mutating property getters and introduced explicit cold `EnsureDroneShaderPropertyIds()` called from initialization/render-buffer setup. Render paths now read raw cached fields only.
Rejected Alternatives: Keeping lazy getter mutation because it is cheap; converting IDs to hardcoded integers; resolving property IDs inside draw calls.
Scalability potential: Low-tier devices avoid surprise cold work in render read paths; high/ultra keep the same shader binding route with no authority changes.
Hardware Impact: 0 us measured. Expected effect is removing cold-cache risk, not frame-time reduction.

Problem: `CurrentSnapshot` and `IsEmergencyOverclockActive` property getters called `EnsureInitialized()`, so a read accessor could allocate Vault buffers, register signals, or touch the registry.
Solution: Getters now return existing cached state only. Owner boot remains explicit through existing command/update paths.
Rejected Alternatives: Keeping side-effect getters for convenience; adding another managed snapshot cache; moving initialization into external callers without proof.
Scalability potential: All tiers preserve the same fleet truth; reads cannot accidentally bootstrap the drone owner during UI polling.
Hardware Impact: 0 us measured; prevents cold allocation spikes from passive reads.

Problem: Rollback navigation requires deterministic Burst math, and the code needed a fresh explicit audit after the Ultra mandate.
Solution: Static scan verified every SHINOBU_334 `IJob`/`IJobParallelFor` in `DroneFleetNavigationKernel` and `DroneCognitionJob` uses `FloatMode.Deterministic` plus synchronous Burst compile and standard precision. No `FloatMode.Fast` remains in those jobs.
Rejected Alternatives: `FloatMode.Fast` for pathing speed; relying on a previous report.
Scalability potential: Quality changes node budget and cadence only; deterministic math preserves identical route truth across low/mid/high/ultra hardware.
Hardware Impact: 0 us measured; deterministic choice can be slower than fast math but protects co-op rollback.
