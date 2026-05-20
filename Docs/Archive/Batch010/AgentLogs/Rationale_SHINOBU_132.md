# Rationale_SHINOBU_132

Agent: SHINOBU_132
Domain: Tether & Cable Physics
State: ACTIVE

## Decision 00 - Mandate Selection

Problem: Cable solver touches physics truth, rendering, AUP precision, native memory, jobs, telemetry, and quality scaling.
Solution: Read task-specific mandates before code: PHYS tether constraints, ARM64 DTO layout, AUP determinism, zero-GC, native memory/jobs, cinematic fake-first, execution phases, crash telemetry.
Rejected Alternatives: Starting from Unity PhysX joints or LineRenderer is banned; reading only AGENTS.md misses task-specific byte layout and telemetry rules.
Scalability potential: Low uses fewer solver iterations and spline samples; Middle keeps stable gameplay truth; High increases visual spline density; Ultra spends saved CPU/GPU time on smoother thick cable rendering, not extra gameplay truth.
Hardware Impact: On i3/MX350, expected savings come from deleting PhysX joint islands and LineRenderer rebuilds; target reduction is 60-300 us/frame depending on active cable count, pending profiler proof.

## Decision 01 - Fresh Batch State

Problem: Required Status/Rationale files were missing at session start.
Solution: Create fresh files before marking any implementation task done.
Rejected Alternatives: Chat-only state tracking violates anti-amnesia and CTO file review requirements.
Scalability potential: No runtime effect; improves multi-agent coordination.
Hardware Impact: 0 us gameplay impact.

## Decision 02 - SHINOBU_132 DTO Contract

Problem: Existing AUP tether code used TetherNodeDTO and NativeArray copy/writeback patterns; prompt required CableNodeDTO with exact 64-byte ABI and pointer refs.
Solution: Added CableNodeDTO at explicit offsets 0/24/48/52/56-63 and wired layout validation. New Burst jobs walk CableNodeDTO* with UnsafeUtility.AsRef.
Rejected Alternatives: Renaming TetherNodeDTO would risk breaking SHINOBU143 and other agents. NativeArray element copies were rejected because they hide struct-copy mutation hazards and add bandwidth.
Scalability potential: Low/Middle devices get compact contiguous 64-byte node lanes; High/Ultra can spend saved cache bandwidth on more iterations and denser spline output.
Hardware Impact: i3/MX350 estimate: 2-8 us/frame saved on 250-node solves versus copy/writeback loops; larger savings when cache contention is high.

## Decision 03 - Cable Visual Lie Instead Of LineRenderer

Problem: BioCableIK and two cable-like infrastructure paths still referenced LineRenderer, causing CPU mesh rebuild risk and violating cable-render ban.
Solution: Removed cable-specific LineRenderer references and routed BioCableIK through the existing ConnectionSplineBatchRenderer pipe-link path without adding a new Core batch/API. Logistics and relay paths rely on existing spline batch submission.
Rejected Alternatives: Leaving disabled LineRenderer fields would keep serialized legacy dependencies and make future agents re-enable them. Creating per-cable Mesh objects or a BioCable-specific Core renderer service was rejected for allocation, upload cost, and compile-wall damage.
Scalability potential: Low uses one shader-bent tube per cable span; Middle/High keep stable spline shape; Ultra can increase material/lighting richness without extra physical nodes.
Hardware Impact: i3/MX350 estimate: 30-120 us/frame saved during active cable visual refresh compared with LineRenderer position uploads and bounds rebuilds.

## Decision 04 - Event Bus Reaction Forces

Problem: Tether tension must affect other physics systems without direct Rigidbody mutation or a hard dependency on another agent's body map.
Solution: Wrote finite PhysicsEventPayload rows to the existing SignalBus NativeQueue from the final constraint pass, using existing PressureImpulse event type plus CableNodeFlags132.TetherTensionEvent in StatusBits and a vault mirror for inspection.
Rejected Alternatives: Direct Rigidbody.AddForce would couple solver to scene bodies and break deterministic scheduling. Adding a SHINOBU_132-specific PhysicsEventType to Core was rejected because it widens a shared enum during a 20-agent batch. A custom managed C# event was rejected for GC and ordering uncertainty.
Scalability potential: Low can ignore cosmetic consumers while retaining force packets; Middle routes gameplay force; High/Ultra can add VFX/audio reactions from the same event lane.
Hardware Impact: i3/MX350 estimate: <10 us/frame for event writes at mock scale; avoids costly scene-component lookups in solver.

## Decision 05 - Mock Solver Integration

Problem: A solver file alone would not execute or populate telemetry.
Solution: TetherManager now bootstraps SHINOBU132 vault buffers and schedules the 5 x 50-node mock cable solve alongside existing AUP tether jobs.
Rejected Alternatives: New MonoBehaviour scheduler would duplicate dispatcher registration and create another ownership surface. Replacing SHINOBU143 in-place risked breaking unrelated in-flight work.
Scalability potential: Low runs 2 constraint iterations; Middle scales smoothly; High/Ultra use up to 15 iterations and smoother Catmull-Rom visual extraction.
Hardware Impact: i3/MX350 estimate: total mock solver target 80-250 us/frame depending on GlobalQualityWeight; pending Unity profiler proof.

## Decision 06 - Build Policy Handling

Problem: The project requires compile verification, but local CPU stayed above the explicit 50% threshold. Latest samples were 73.73/63.86/37.05 percent; no active dotnet/csc process was visible, generated csproj files exist, and no root .sln was present.
Solution: Per build policy, do not launch dotnet build while average CPU remains above gate. Run static verification instead: rg scans, prompt re-read, diff --check, zero-GC/hot-path grep, LineRenderer/joint grep, and self-audit.
Rejected Alternatives: Starting dotnet while the machine is above the user CPU gate would violate the build rule. Reporting a fake compile pass would be worse than a blocked verification.
Scalability potential: No runtime effect; preserves machine resources for active agents and avoids compile contention.
Hardware Impact: 0 us gameplay impact. Compile remains unverified until CPU is below 50%, no dotnet/csc process is active, and a valid Unity compile path is available. Latest post-polish CPU average was 25.07 percent, but dotnet Id 53260 was already active, so the concurrency gate stayed closed.

## Decision 07 - Compile-Wall Repair And Live Tuning

Problem: The first implementation widened shared Core surfaces for SHINOBU_132 buffer IDs, a custom physics event enum, and a BioCable-specific renderer batch. It also let the editor tuner write data that the solver did not fully consume, and the spline extraction treated five mock cables as one continuous strip.
Solution: Replaced global enum IDs with owner-local numeric BufferID casts `71320..71332`, removed the Core event/renderer mutations, routed tension through an existing SignalBus payload plus domain status bit, made GenerateSplineVerticesJob calculate cable/local indices explicitly, and consumed Vault tuning for gravity, drag, max solver iterations, break force, and spline vertex budget. TetherManager samples `GlobalRegistry.Fluid` once and passes the flow vector into Burst as data; the solver adds deterministic sinusoidal current as fallback/noise.
Rejected Alternatives: Keeping Core enum/service edits would increase compile-wall blast radius. Burst-side access to the fluid service was rejected because jobs must stay pure data kernels. Per-cable GameObjects, LineRenderer rebuilds, and PhysX joints were rejected as standard Unity rope architecture.
Scalability potential: Low uses 2 iterations and 10 visual vertices per cable, Middle increases both continuously, High/Ultra can run up to 15 iterations and 64 spline vertices per cable while still simulating only 50 physics nodes.
Hardware Impact: i3/MX350 estimate: low-quality visual extraction writes 50 vertices total instead of 320 max, saving roughly 5-20 us/frame at mock scale; compile-wall repair prevents unrelated Core recompiles from SHINOBU_132-specific surface churn.

## Decision 08 - Upload Stall Removal And CSV Scratch Repair

Problem: The Task 09 route still force-completed the GraphicsBuffer copy job immediately after LockBufferForWrite, which made the "asynchronous" label false. The editor CSV path also used File.ReadAllBytes, creating a managed byte[] staging allocation for the material bridge.
Solution: Replaced the blocking upload facade with TryBeginSplineVertexUpload/TryFinalizeSplineVertexUpload ticket ownership. The copy job now receives caller dependency, writes through a NoAlias mapped pointer, clamps copy bytes to the mapped destination span, and unlocks only after DispatcherJobFence.TryFinalizeCompleted or forced teardown. Added TetherSplineIndirectArgsDTO, a Burst indirect-args upload job, and a DrawProceduralIndirect helper. CSV reload now reads through FileStream.Read into a Temp NativeArray<byte> and passes ReadOnlySpan<byte> to the existing parser.
Rejected Alternatives: Keeping the force-complete upload would hide a main-thread stall in VISUAL_SYNC. GraphicsBuffer.SetData and managed uint[]/byte[] staging were rejected for allocation/stall risk. A new renderer service in Core was rejected because the renderer owner should bind the returned buffers without SHINOBU_132 widening shared contracts.
Scalability potential: Low still uploads 10 vertices per cable and cheap indirect args; Middle/High increase vertex count continuously; Ultra can expand vertices-per-spline-point in the indirect args without increasing Verlet node truth.
Hardware Impact: i3/MX350 estimate: avoids 10-45 us upload stall when the copy job overlaps with dispatcher work; CSV change is editor/cold-only and removes one managed byte[] allocation per reload.
