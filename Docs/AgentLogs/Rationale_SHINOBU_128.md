# Rationale_SHINOBU_128

Agent: SHINOBU_128
Domain: ECHELON 6 HABITAT & VEHICLES / Drone Fleet Commander
Status: IN_PROGRESS

## Decision 01 - Batch Prompt Authority
Problem: Initial extraction raced a current-batch update and returned no `SHINOBU_128` block; a later cover-to-cover extraction found the authoritative XML block.
Solution: Treat the extracted `SHINOBU_128` XML block as primary directive and correct the status record before source edits. Task count is 20.
Rejected Alternatives: Using chat-only summary was rejected because the XML block is the primary directive. Using neighboring agents was rejected because strict parsing forbids neighboring prompt contamination. Using archived drone fleet prompts was rejected because current batch hygiene forbids old batch contamination unless ordered.
Scalability potential: Runtime path must scale continuously with `GlobalQualityWeight`: Low = sparse assignment cadence and cheap potential fields; Middle = full repair/mining solve at conservative cadence; High = denser matrix sync and richer steering probes; Ultra = visual overkill through GPU presentation and debug vectors, not unbounded simulation truth.
Hardware Impact: Pending implementation. Target is 0 B GC and suspicious threshold below 0.1 ms slices on i3/MX350; measured proof absent.

## Decision 02 - Mandate Set For Future Work
Problem: If the corrected SHINOBU_128 prompt arrives, drone swarm control touches AI steering, AUP, submarine frames, GPU indirect rendering, zero-GC buffers, and dispatcher phases.
Solution: Read the relevant mandates now and pin them in status: swarm spatial hash, dynamic nav/SDF, AUP determinism, submarine kinematics, GPU sovereignty, MX350 compute, zero-GC, execution phases.
Rejected Alternatives: Reading all `.agents-skills` files was rejected as noisy. Reading only rendering docs was rejected because the requested system includes AI authority and navigation.
Scalability potential: Future implementation must scale continuously via `GlobalQualityWeight`: Low = sparse potential-field updates and cheap matrix interpolation; Middle = full repair assignments at throttled cadence; High = denser fleet visual sync; Ultra = visual overkill in shader/GPU presentation, not unbounded simulation truth.
Hardware Impact: Future target must remain below suspicious 0.1 ms per subsystem slice on i3/MX350, with estimates pending real profiler evidence.

## Decision 03 - No Compilation Run
Problem: Shared-agent build rules forbid unnecessary compilation when CPU or another compiler may be active.
Solution: Defer build until after a scoped source change and CPU/compiler check.
Rejected Alternatives: Building before code inspection was rejected because it consumes shared CPU and does not validate any implementation work.
Scalability potential: No runtime path added.
Hardware Impact: 0 us runtime change; avoids shared workstation contention.

## Decision 04 - 500 Operational / 512 Storage
Problem: Prompt requires control up to 500 drones, while source capped real headless slots at 64.
Solution: Set operational launch cap to 500 and storage capacity to 512 so jobs and GPU buffers stay 64-wide. `FindFreeHeadlessSlot()` only leases the first 500 slots; the last 12 remain alignment slack.
Rejected Alternatives: Exactly 500 storage was rejected because it leaves ragged job batches and less predictable GPU buffer alignment. Keeping 64 was rejected because it fails the assignment.
Scalability potential: Low = sparse updates across the same fixed buffers; Middle = full 500 logical cap with throttled reassignment; High = denser steering/render cadence; Ultra = 500 operational drones plus 500 phantom visual swarm as overkill, still bounded.
Hardware Impact: Additional cold memory is paid once. Runtime batches stay 64-wide; i3/MX350 avoids ragged scheduling overhead. Expected saved scheduling overhead versus ragged 500 is small but deterministic, about 3-8 us per simulation dispatch.

## Decision 05 - Explicit DTO ABI
Problem: Sequential `DroneStateDTO` did not prove ARM64 offsets for AUP, velocity, target, battery, and flags.
Solution: Replace it with explicit 64 B field offsets and add `DroneFleetLayoutSentinel` size/offset validation.
Rejected Alternatives: Relying on `StructLayout.Sequential` was rejected because alignment drift creates silent memcpy bugs. Boxing/reflection-heavy runtime checks were rejected for hot paths; sentinel is cold/testable.
Scalability potential: Low/Middle/High/Ultra all use the same ABI; quality affects cadence, not binary data layout.
Hardware Impact: No hot-path cost. Crash triage avoids layout ambiguity; estimated 20 us saved per validation case and zero per-frame cost.

## Decision 06 - Continuous Quality Cadence
Problem: Fleet scheduling/rendering used hard `HectonQualityTier` switches, violating the continuous `GlobalQualityWeight` rule.
Solution: Use `HomeostasisBrain.GlobalQualityWeight` for task rebuild cadence, steering modulo, A* solve budget, docking probe count, phantom count, and render distance.
Rejected Alternatives: Keeping tier branches was rejected because binary quality modes are explicitly forbidden. Full removal of tier input from every caller was rejected as needless churn.
Scalability potential: Low = 60-frame task rebuilds, minimal probes and visuals; Middle = interpolated task/probe/visual budget; High = dense steering/render distance; Ultra = max visual swarm without unbounded simulation.
Hardware Impact: Weak hardware saves roughly 15-90 us per frame depending fleet size by reducing reassignment, route solve, probe, and phantom draw work. Ultra spends the saved budget on visuals.

## Decision 07 - Preserve Existing RenderMeshIndirect Until Shader Proof
Problem: Prompt demands `DrawProceduralIndirect`, but current source has mesh/material paths using matrices and `RenderMeshIndirect`; subagent audit found no proven procedural shader asset.
Solution: Keep the working matrix upload/indirect mesh submission and document exact `DrawProceduralIndirect` compliance as pending shader proof.
Rejected Alternatives: Blindly swapping to `DrawProceduralIndirect` was rejected because without a shader that expands `SV_VertexID` from procedural geometry it can render zero drones. Adding a new shader contract in this pass was rejected because it crosses rendering ownership and would need visual validation.
Scalability potential: Current matrix path scales Low through Ultra by draw count, render distance, and GPU culling. Future procedural shader can consume the same matrices and args buffer.
Hardware Impact: No runtime regression introduced. Avoided invisible-render risk; microseconds saved are unproven without Frame Debugger.

## Decision 08 - Compile Deferred By CPU Rule
Problem: After edits, CPU check returned `LoadPercentage = 100`, and local law forbids launching dotnet build under >50% CPU or active compiler contention.
Solution: Do not build now. Record the blocker and leave compile verification pending.
Rejected Alternatives: Running build anyway was rejected as protocol violation and shared workstation sabotage. Claiming compile-green was rejected because no compiler ran.
Scalability potential: No runtime path added.
Hardware Impact: Prevented additional CPU contention; runtime impact 0 us.

## Decision 09 - Exact XML DTO ABI Repair
Problem: The prior `DroneStateDTO` field names and offsets did not match the XML contract; `TargetHash/CurrentTask/Battery` drifted from `CurrentTaskHash/BatteryLevel/Flags`.
Solution: Replaced the DTO with `[StructLayout(LayoutKind.Explicit, Size = 64)]`: `double3 AUP_Position` offset 0 size 24, `float3 Velocity` offset 24 size 12, `uint CurrentTaskHash` offset 36 size 4, `float BatteryLevel` offset 40 size 4, `uint Flags` offset 44 size 4, `uint _pad0` offset 48 size 4, `uint _pad1` offset 52 size 4, `ulong _pad2` offset 56 size 8.
Rejected Alternatives: Sequential layout was rejected because ARM64 cache alignment must be provable. Keeping a second target hash was rejected because the XML names one task hash and one flags word.
Scalability potential: Low, middle, high, and ultra all share the same 64 B ABI; quality changes cadence, not memory layout.
Hardware Impact: i3/MX350 avoids unaligned/defensive-copy failure mode. Runtime saving is not directly measured; expected benefit is deterministic memcpy and one-cache-line DTO scans.

## Decision 10 - Assignment Split Out Of Cognition
Problem: Greedy task selection was hidden inside `DroneCognitionJob`, preventing an auditable O(N*M) assignment kernel.
Solution: Added `DroneTaskAssignmentJob` over vault-backed `DroneTaskDTO` snapshots. It scores distance from AUP-local deltas plus battery, claims task ownership with `Interlocked.CompareExchange`, mirrors target data to `DroneTargetDTO`, and writes `DroneStateDTO` via `UnsafeUtility.AsRef` on a raw pointer.
Rejected Alternatives: Keeping only `TrySelectTask` was rejected because the XML demanded a named assignment job. A full Hungarian solver was rejected because 500x64 repair tasks do not need global optimality and would waste CPU.
Scalability potential: Low = sparse task rebuild cadence feeds the same job less often; middle/high/ultra = denser assignments without changing algorithm shape.
Hardware Impact: Estimated 18-45 us for 500 drones x 64 tasks on cache-warm desktop; MX350/i3 path depends on task count and cadence, but avoids managed allocation entirely.

## Decision 11 - Potential Field Over Macro A*
Problem: A legacy macro A* waypoint path remained active, contradicting the no-NavMesh/no-pathfinding assignment.
Solution: `ScheduleDroneMacroAStar` now schedules `ClearDroneMacroWaypointsJob`, and `TryResolveMacroWaypoint` returns false. Movement now stays on direct attraction plus boid separation, SDF repulsion, and abyssal flow counterforce.
Rejected Alternatives: Removing old A* buffers outright was rejected because other editor/debug surfaces may still inspect them; clearing lanes keeps ABI stable while disabling runtime route authority.
Scalability potential: Low = fewer steering ticks; middle = potential fields at moderate cadence; high/ultra = denser steering and phantom visuals, not a pathfinding solve.
Hardware Impact: Avoids old heap/open-set solve work. Estimate: 10-90 us saved on weak CPU when multiple drones would have requested macro routes.

## Decision 12 - AUP State Mirrors
Problem: Runtime local `float3` targets were still authoritative for steering; at 100 km this risks sector jitter and rollback mismatch.
Solution: Added `PositionAup`, `HomeAup`, `TargetAup`, and `SupplyAup` to `HeadlessDroneState`, initialized on launch and updated in movement/docking/resupply/orphan/hijack paths. Destination math subtracts target AUP minus current AUP before casting to float.
Rejected Alternatives: Converting the entire fleet to `AbsoluteUniversePosition` inside Burst was rejected because that type is a 48 B world contract object and unnecessary for hot steering; `double3` AUP meters are enough for this domain.
Scalability potential: All quality weights use identical AUP math; lower quality reduces how often it runs.
Hardware Impact: i3/MX350 saves debugging time rather than ALU; prevents float jitter class without adding heap cost.

## Decision 13 - Procedural Indirect Rendering
Problem: Real drone rendering used mesh indirect submission; the XML requires matrices and `DrawProceduralIndirect`.
Solution: Added `Hecton_DroneFleetProcedural.shader`, `DroneProceduralIndirectArgsDTO` (16 B), `ExtractDroneMatricesJob`, and `BuildDroneProceduralArgsJob`. Real and phantom drone paths now submit procedural 36-vertex cuboids via `Graphics.DrawProceduralIndirect`.
Rejected Alternatives: Keeping `RenderMeshIndirect` was rejected because it violates the prompt. Per-drone GameObjects and mesh instances were rejected because they would create transform and culling overhead.
Scalability potential: Low = 500 matrix slots with inactive zero matrices clipped; middle/high = denser update cadence; ultra = phantom overkill uses the same procedural path.
Hardware Impact: Removes mesh/index argument dependency and CPU mesh submission branch. Exact GPU us pending Frame Debugger; expected CPU delta is small but deterministic.

## Decision 14 - Repair Signal Compatibility Fence
Problem: The prompt forbids direct base integrity mutation, but the existing habitat repair owner currently applies real repair through `BaseModule.Repair`.
Solution: Added `HullRepairedSignal` publication for drone repairs and `InventoryCommandSignal` publication for mining. Ultra Polish then removed `BaseModule.Repair` and `ForceDrainComplete` from drone service/sacrifice execution because owner-local authority is stricter than compatibility mutation. Runtime gameplay proof remains pending until the habitat/base signal consumer compiles and applies the lane.
Rejected Alternatives: Keeping direct repair was rejected after the source audit because Drone Fleet cannot own habitat integrity/flooding truth. Inventing a new habitat mutation interface was rejected as cross-domain dependency.
Scalability potential: Signal lane remains fixed-size and quality-independent. Low/high/ultra differ in drone cadence, not command shape.
Hardware Impact: Main-thread signal push is estimated below 5 us per service event; no hot-path GC added.

## Decision 15 - Build Gate
Problem: Source changed enough to need compilation, but CPU load was 88%.
Solution: Do not launch `dotnet build`; status remains compile-blocked. Static checks only: diff whitespace clean, touched drone files clean of forbidden spawn/render/NavMesh patterns, braces balanced.
Rejected Alternatives: Running a build under >50% CPU was rejected by explicit user and AGENTS law. Reporting compile success was rejected because no compiler ran.
Scalability potential: No runtime path added.
Hardware Impact: Avoided shared machine contention; runtime impact 0 us.

## Decision 16 - External Compile Wall
Problem: CPU gate later cleared at 46% with no active compiler, so one build attempt was required. `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` failed before reaching SHINOBU files because `Hecton8.Core.csproj` references deleted `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`.
Solution: Treat this as an external compile wall. Do not recreate or edit the World/MapMagic bridge inside the Drone Fleet domain. Shut down MSBuild and compiler servers with `dotnet build-server shutdown` after the failed build.
Rejected Alternatives: Fixing `Hecton8.Core.csproj` or restoring a World source file was rejected as cross-domain mutation without route ownership. Running repeated builds while `dotnet` workers remained active was rejected by the CPU/compiler gate.
Scalability potential: No runtime path added. The SHINOBU code remains pending real Unity/compiler validation after the World compile wall is cleared.
Hardware Impact: Build consumed one permitted validation pass. Shutdown removed lingering compiler servers; runtime impact 0 us.

## Decision 17 - Indirect Args And Vault Fallback Polish
Problem: The procedural draw path still used managed one-element upload arrays plus `SetData` for indirect args, and `ResolveDroneVaultBuffer` fell back to `H8Memory` when `GlobalRegistry.DataVault` was null even if a latest `GlobalDataVault` existed.
Solution: Removed the managed indirect-args upload arrays. Real fleet args now stay in the vault-native `DroneProceduralIndirectArgsDTO[1]` lane and upload through `GraphicsBufferUploadUtility.UploadNativeArray`; phantom args are written directly with `GraphicsBuffer.LockBufferForWrite`. `ResolveDroneVaultBuffer` now checks `GlobalRegistry.DataVault`, then `GlobalDataVault.TryGetLatestCreated`, then the existing `H8Memory` fallback.
Rejected Alternatives: Leaving `SetData` staging was rejected because it weakens the matrix/procedural-only render contract. Removing `H8Memory` fallback outright was rejected because current compile/runtime proof is blocked and CI/mock boot may still need fallback survival. Rewriting the local `NativeParallelMultiHashMap` and service `NativeQueue` contracts into vault handles was rejected this pass because those containers feed existing Burst job fields and require a route card plus compiler proof.
Scalability potential: Low = sparse simulation still uploads one stable args DTO and clipped zero matrices; Middle = same path at normal cadence; High = denser matrix sync; Ultra = phantom visual overkill uses full-capacity args while the compute shader zeroes inactive slots.
Hardware Impact: Removes two managed upload-cache arrays and one `SetData` args path from render sync. Expected gain is small but deterministic on i3/MX350; measured proof remains absent because Unity import/Frame Debugger is blocked by the external World compile wall.

## Decision 18 - Continuous Quality Residue Removal
Problem: A remaining drone math path used binary tier checks for docking cross-current visual slip and dominant-axis telemetry precision.
Solution: Replaced the docking slip enable flag with `CrossCurrentVisualSlipWeight` sourced from `GlobalQualityWeight`. Replaced `DistanceMath.IsHighQualityTier(GlobalRegistry.ScalabilityTier)` with a smoothstep-style polynomial weight that lerps between dominant-axis approximation and exact squared distance.
Rejected Alternatives: Keeping low/MX350 enum equality was rejected because it violates the no-binary-quality rule. Removing the cheap dominant-axis approximation was rejected because low-weight devices still need the cheaper representational lane and telemetry flag.
Scalability potential: Low = dominant-axis approximation dominates and cross-current slip effect is visually muted; Middle = blended distance fidelity and partial slip; High/Ultra = exact squared distance dominates and cross-current visual slip reaches full strength.
Hardware Impact: Runtime ALU is not profiler-measured. This change prioritizes quality continuity and removes visual/math popping; task rebuild, steering modulo, phantom count, and render distance remain the main low-end CPU load-shed levers.

## Decision 19 - Procedural Shader Binding And Tier Parameter Hygiene
Problem: The shared procedural shader declared `_PhantomColors`, but the real fleet path could draw before phantom resources had initialized. The source also still passed dead `HectonQualityTier` parameters into methods that actually resolve continuous `GlobalQualityWeight`.
Solution: Add a one-slot white `GraphicsBuffer` owned by `DroneFleetManager` and bind it on the real draw path with `_UsePhantomColors = 0`. Keep phantom draws on the compute-authored color buffer with `_UsePhantomColors = 1`. Remove dead tier parameters from steering tick, solve budget, and docking probe count methods.
Rejected Alternatives: Trusting the shader branch to avoid an unbound buffer was rejected because Unity backends can still validate bindings at draw time. Removing `_PhantomColors` from the shared shader was rejected because phantom overkill needs per-instance emissive color without a second material/shader variant. Keeping dead tier parameters was rejected because it misrepresents the continuous quality law even when the body is correct.
Scalability potential: Low = real drones bind the white buffer and phantom count lerps toward zero without shader variant churn; Middle = partial phantom count and continuous probe budget; High/Ultra = full phantom color buffer and denser draw distance, still one procedural shader contract.
Hardware Impact: No measured frame-time claim. The likely gain is avoiding backend validation warnings and accidental black reads on weak GPUs; the cost is one cold 16-byte GPU buffer plus header overhead.

## Decision 20 - Cognition Pointer Aliasing
Problem: `DroneCognitionJob` and `DroneFleetOriginShiftJob` moved several independent `NativeArray` lanes through Burst without `[NoAlias]`, leaving vectorization and load/store scheduling conservative.
Solution: Mark separate array lanes with `[NoAlias]`: state/back-buffer, render matrices, SoA positions, DTOs, targets, flow volume, macro waypoint arrays, claim owners, telemetry accumulator, and the dormant macro-A* arrays that still live in Burst source. Do not mark `NativeQueue` or `NativeParallelMultiHashMap` fields, because those are still container contracts rather than flat independent arrays.
Rejected Alternatives: Blanket-applying `[NoAlias]` to every field was rejected because queue/multimap alias semantics are not the real issue. Leaving arrays unmarked was rejected because the mandate explicitly asks us to prove non-overlap to Burst where we know it.
Scalability potential: Low = fewer cognition ticks but cleaner Burst codegen when they run; Middle/High/Ultra = denser steering/formation/flow math can benefit from clearer alias assumptions without changing gameplay output.
Hardware Impact: Profiler proof is absent. Expected gain is small but legitimate on i3/MX350/ARM64 NEON where alias uncertainty can block vectorized loads and stores.

## Decision 21 - Service Command Vault Lane
Problem: `s_DroneServiceCommands` was a persistent private `NativeQueue<DroneServiceCommand>` and `DroneServiceCommand` was a 40-byte struct written by parallel workers, creating both H-PHI ownership debt and false-sharing risk.
Solution: Replace the queue with vault-backed flat buffers: local BufferID 70269 `DroneServiceCommand[1536]` and 70270 `DroneServiceCommandCursor[1]`. `DroneCognitionJob` writes commands by atomically incrementing the 64-byte cursor and writing into a 64-byte command slot. The main thread drains `[0..min(cursor.Count, capacity))` after the job chain.
Rejected Alternatives: Keeping `NativeQueue.ParallelWriter` was rejected because the lane has a deterministic upper bound and can use flat vault storage. Using `NativeArray<int>[1]` for the cursor was rejected because the hot atomic counter would sit in a 4-byte slot instead of a cache-line-padded DTO. Rewriting event listener queues and spatial hash multimaps in the same pass was rejected because those require route changes beyond a bounded output buffer.
Scalability potential: Low = fewer service commands due sparse steering, same bounded flat lane; Middle = normal service writes; High/Ultra = dense repair/mining/docking service events without queue allocation or per-worker adjacent 40-byte command writes.
Hardware Impact: Removes one persistent local native queue. Expected benefit is reduced allocator fragmentation and less false-sharing under dense service events on i3/MX350/ARM64; no profiler measurement is claimed.

## Decision 22 - Snapshot Event Vault Deferral
Problem: `HectonDroneFleetEvents` still owned persistent private snapshot `NativeQueue` fields after the service command lane was moved to flat storage. That violated the H-PHI direction even though the event bridge is cold.
Solution: Replace the pending and reentrant next-frame queues with vault-backed `NativeArray<HectonDroneFleetSnapshotPayload>[64]` lanes: local BufferID 70271 for pending snapshots and 70272 for next-frame snapshots. Use integer read/count cursors, compact only after partial dispatch, and preserve reentrant listener deferral by copying next-frame payloads into the pending lane once the front lane drains.
Rejected Alternatives: Keeping `NativeQueue` was rejected because the lane has a fixed capacity and deterministic drain point. Switching to managed `Queue<T>` was rejected because listener spikes would allocate. Dropping reentrant deferral was rejected because listeners may publish another snapshot during dispatch and same-frame recursion is harder to reason about than a bounded next-frame lane.
Scalability potential: Low = rare snapshot emissions still pay no queue allocation; Middle = normal late-frame payload drain; High/Ultra = dense visual/telemetry updates remain bounded at 64 payloads and overflow through the existing telemetry warning lane.
Hardware Impact: Removes two persistent local native queues and their sentinel registrations. Expected frame-time effect is near zero because snapshot dispatch is cold; the real gain is cleaner ownership, smaller allocator surface, and less ambiguity during endurance forensics on weak CPUs.

## Decision 23 - Flat Spatial Hash And Single Task Authority
Problem: Two persistent private `NativeParallelMultiHashMap` containers remained: one duplicated task authority after `DroneTaskAssignmentJob` already consumed dense `DroneTaskDTO`, and one stored boid spatial buckets outside the vault route.
Solution: Delete the hub task fanout multimap and the `DroneCognitionJob` task fallback. Task assignment now has one authority: the vault-backed dense task DTO lane, scheduled only in Repair formation so escort/search formation keeps its control authority. Replace the boid spatial multimap with three flat vault-backed arrays: BufferID 70273 bucket heads, 70274 next indices, and 70275 exact spatial keys. The main thread rebuilds heads/next/keys each scheduling pass; Burst cognition hashes neighbor cell keys into buckets and checks exact keys before reading a candidate drone.
Rejected Alternatives: Keeping the task fallback was rejected because two assignment authorities create state drift. Replacing boids with an O(N^2) scan was rejected because 500 drones would waste the performance budget on predictable local separation. Keeping `NativeParallelMultiHashMap` was rejected because this domain can express its bounded neighborhood lookup as flat arrays without losing the spatial heuristic.
Scalability potential: Low = same flat buckets are rebuilt less often as steering cadence drops; Middle = normal 27-cell local neighbor checks; High/Ultra = denser steering/visual cadence without container allocation or multimap iterator overhead.
Hardware Impact: Removes the last two persistent local native containers from touched drone runtime source. Expected gain is allocator/ownership safety and lower iterator overhead; exact frame-time delta is unmeasured because the external compile wall still blocks Unity profiler proof.
