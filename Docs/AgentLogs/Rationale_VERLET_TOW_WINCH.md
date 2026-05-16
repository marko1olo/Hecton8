# Rationale_VERLET_TOW_WINCH

Status: `PENDING VERIFICATION`

## Pre-Code Analysis

Problem: Unity Joint towing is explicitly rejected for deterministic salvage cables. Spring-like force fights PhysX and produces rubber-band failure under high mass ratios.
Solution: Implement a fixed-size SoA Verlet cable path with Burst jobs, `math.rsqrt`, finite guards, force-packet style output, and a 300-frame blackbox ring.
Rejected Alternatives: Unity `SpringJoint`, `ConfigurableJoint`, and `HingeJoint` are forbidden; direct steady-state `Rigidbody.AddForce` from tether code violates physics ownership.
Scalability potential: Low uses 3 active math segments and a taut-line visual fake; Middle uses the full 10 point authority; High/Ultra can feed richer indirect-render visual data without changing authority.
Hardware Impact: Low-end i3/MX350 target is protected by fixed buffer sizes, no per-frame allocation, segment-count LOD, and no Unity Joint solver explosions. Estimated gain is pending measurement.

## Decision Log

- Problem: Runtime towing already had a procedural tether host, but the task forbids singleton/joint coupling and requires deterministic towing.
  Solution: Kept the local `TetherManager` registry and `TetherSignals` entry points; moved fixed tick registration to `PriorityLayer.Environment` so the solver runs before player kinematics.
  Rejected Alternatives: Adding `TetherManager.Instance` or a Unity `SpringJoint`/`ConfigurableJoint` would reintroduce hidden global order and PhysX spring explosions.
  Scalability potential: Low/Mid keep the existing manager pool; High/Ultra only spend extra cost in the render path.
  Hardware Impact: 0 us runtime overhead for singleton purge; expected saved failure cost is unbounded compared to joint-solver explosions on i3/MX350.

- Problem: The solver needed a 10-segment cable state that other systems can consume without direct code dependencies.
  Solution: Added fixed BufferID lanes for positions, previous positions, velocities, masses, and segment tensions; each active tether reserves one fixed slot and publishes canonical 11 points / 10 segments in SoA form.
  Rejected Alternatives: Per-frame managed arrays and object graphs were rejected because they allocate and create ownership ambiguity across 20+ parallel systems.
  Scalability potential: Low samples from 3 authority segments into the 10-segment public contract; High/Ultra publish full 10-segment data without changing consumers.
  Hardware Impact: Estimated +3-6 us publish cost for one active tether on i3/MX350; zero GC and fixed slots avoid allocator spikes.

- Problem: World-space Verlet nodes lose precision when floating origin shifts or towing occurs far from origin.
  Solution: Stored solver nodes in local offset space relative to the tow anchor, rebasing only the origin and visual upload coordinates.
  Rejected Alternatives: Raw world-space float nodes were rejected because they become unstable under AUP/floating-origin motion; double math inside Burst was rejected as cost without visual benefit.
  Scalability potential: Same authority path from Low to Ultra; saved precision can be spent on richer High/Ultra rendering instead of bigger physics math.
  Hardware Impact: Estimated <1 us rebase cost for 11 nodes; prevents NaN cascades that would otherwise dump full physics frames.

- Problem: Towing needed a deterministic cable authority path instead of a joint.
  Solution: Added `VerletCableSolverJob`, using `math.rsqrt` for constraint normalization, finite guards, segment tension output, and pinned endpoint constraints.
  Rejected Alternatives: Unity joints and distance-only scalar springs were rejected; they cannot expose per-segment tension or stable visual state.
  Scalability potential: Low uses 3 segments / 2 iterations; Middle uses 10 / 3; High/Ultra use 10 / 5 and richer rendering.
  Hardware Impact: Estimated 8-20 us per active tether on i3/MX350 depending on tier; no per-frame allocation.

- Problem: Tension needs to drive audio/VFX without hard references.
  Solution: Added `TetherTensionSignal` with AUP endpoints, tension force, snap threshold, normalized tension, reactive scalar, and node count.
  Rejected Alternatives: Direct VFX/audio component calls were rejected because they invent cross-domain dependencies.
  Scalability potential: Low can ignore or coarsely sample the scalar; High/Ultra can layer dust, vibration, sparks, and shader pulses.
  Hardware Impact: Estimated <2 us signal publish when tension is active; NativeQueue-style bus remains decoupled.

- Problem: High-tier visuals still used a procedural primitive draw while the prompt required an indirect cylindrical impostor route.
  Solution: Added a High/Ultra `Graphics.RenderMeshIndirect` path using a persistent six-vertex segment mesh, one indirect args buffer, and shader instance mapping to cable segments.
  Rejected Alternatives: Generating tube meshes per frame was rejected for GC and upload cost; forcing indirect on Low was rejected because MX350 should prefer the cheaper primitive fallback.
  Scalability potential: Low/Mid keep cheap `RenderPrimitives`; High/Ultra get segment-instanced impostor draw with stress pulse support.
  Hardware Impact: Estimated neutral to -5 us CPU on High/Ultra by moving segment repetition into indirect instancing; no benefit claimed for MX350.

- Problem: A stuck wreck can create unbounded endpoint delta and poison the simulation.
  Solution: Clamped Verlet displacement velocity and exported `PeakCableTension` into the 300-frame telemetry ring / dump file.
  Rejected Alternatives: Allowing raw velocity through the solver was rejected because one voxel SDF penetration can explode every downstream force.
  Scalability potential: All tiers use the same safety clamp; High/Ultra can visualize higher stress before snap without destabilizing authority.
  Hardware Impact: Estimated <1 us per 11-node integration pass using `math.rsqrt`; prevents catastrophic recovery cost.

- Problem: Tension must obey Newton's 3rd law and snap cleanly.
  Solution: Routed equal/opposite force packets through `PhysicsForceRouter`, scaled by mass ratio and max acceleration, and clears DataVault plus emits `ImpactSignal(Snap)` on snap.
  Rejected Alternatives: Direct `Rigidbody.AddForce` and one-sided wreck pulling were rejected because they bypass force ownership and feel fake under load.
  Scalability potential: Low uses the same force packets; High/Ultra can spend snap signal intensity on particles and cable fragments.
  Hardware Impact: Estimated 2 queued force packets per active tow step; no GC.

- Problem: Multiplatform layout risk remained in tether signal and telemetry structs.
  Solution: Locked `TetherTensionSignal`, `TetherSnappedSignal`, `TetherFiredSignal`, `TetherVerletTelemetryEntry`, and `TetherManagerTelemetryEntry` to `StructLayout.Sequential, Pack=1` with explicit sizes.
  Rejected Alternatives: Default CLR packing was rejected because implicit padding is not acceptable for ARM64/Quest signal lanes and binary blackbox dumps.
  Scalability potential: Low/Mid/High/Ultra all consume the same stable payload layout; high tier can add visual work without changing native signal ABI.
  Hardware Impact: 0 us runtime claim; avoided padding drift and binary-dump ambiguity on mobile silicon.

- Problem: Snap and fire notifications still had private queue ownership after the H-Phi pass.
  Solution: Moved snap and fire payloads to typed `SignalBus<T>` lanes; readers use `ReadOnlySpan<T>` snapshots. The fire path keeps only a fixed-size managed Unity-object resolver sidecar for immediate same-frame attach.
  Rejected Alternatives: Managed delegates/EventBus were rejected; pure unmanaged fire payloads carrying `Rigidbody`/`Collider` references are impossible without replacing the attach API with registry handles.
  Scalability potential: Low can drop or defer non-critical consumers through lane policy; High/Ultra can attach richer audio/VFX to the same fire/snap/tension lanes.
  Hardware Impact: Removed one private persistent `NativeQueue<TetherFiredSignal>` from tether code; fire sidecar is fixed 16 entries and 0 B/frame GC.

- Problem: Public cable DataVault lanes still had a private fallback allocation path.
  Solution: Removed the fallback for public SOA cable export; if `GlobalDataVault` is absent or fenced, the system fails closed instead of creating a private authority copy.
  Rejected Alternatives: Keeping a private fallback NativeArray was rejected because it violates data sovereignty and creates split-brain physics state.
  Scalability potential: Low devices get predictable memory ownership; High/Ultra consumers can trust one canonical 10-segment cable lane.
  Hardware Impact: Avoids hidden fallback allocation spikes; publish cost remains estimated +3-6 us per active tether.

- Problem: Remaining per-instance solver/visual staging arrays cannot be fully evicted without a broader DataVault handle refactor.
  Solution: Moved all remaining tether-owned runtime `NativeArray` allocations through `H8Memory.Allocate/Release(SystemID.Physics)` and documented the remaining private working-memory exception instead of claiming full statelessness.
  Rejected Alternatives: Inventing new BufferIDs for every solver scratch lane during a dependency-blocked batch was rejected because it would expand the blast radius across generated project files and DataVault capacity policy.
  Scalability potential: Low/Mid/High/Ultra still use fixed capacities and deterministic segment LOD; a future owner can promote scratch lanes to vault handles without changing the public cable contract.
  Hardware Impact: 0 B/frame GC; leak visibility improves through the memory sentinel. Full private working-memory eviction remains not completed.

- Problem: Directed compile caught that `TetherFiredSignal` lived on a contract path whose generated project treatment was unstable.
  Solution: Placed the actual `TetherFiredSignal` payload in compiled `TetherSignals.cs`, restored the generated-project contract path as an empty compile anchor, and explicitly aliased runtime fire usage to the core contract type.
  Rejected Alternatives: Editing the generated `.csproj` was rejected because Unity overwrites it; leaving the real payload only in the contract stub was rejected after the compiler failed to resolve it.
  Scalability potential: No tier-specific behavior; this is a build-integrity fix.
  Hardware Impact: 0 us runtime; removed a compile blocker created by the SignalBus migration.
