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
Solution: Added `HullRepairedSignal` publication for drone repairs and `InventoryCommandSignal` publication for mining. Kept direct `BaseModule.Repair` for now because removing it without a proven habitat consumer would silently break repairs.
Rejected Alternatives: Deleting direct repair was rejected as architectural theater that would break gameplay. Inventing a new habitat mutation interface was rejected as cross-domain dependency.
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
