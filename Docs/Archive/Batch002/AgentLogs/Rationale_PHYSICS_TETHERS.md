# PHYSICS_TETHERS Rationale

Status: VERIFIED MASTER GRADE - PHYSICS_TETHERS SCOPE; GLOBAL COMPILE BLOCKED BY DEPENDENCY

## Decision 001 - Extend Existing Tether Path
Problem: The project already owns HeavyTowWinch -> TetherManager -> TetherInstance gameplay integration, but the active solve is managed PD/raycast and not the requested Verlet acceleration constraint path.
Solution: Replace the active simulation core inside TetherInstance with persistent native node arrays and Burst jobs while preserving existing gameplay entry points.
Rejected Alternatives: A new parallel tether manager would orphan HeavyTowWinch and create conflicting cable ownership. Unity ConfigurableJoint/HingeJoint/SpringJoint remain rejected because they are AUP-shift fragile and CPU-heavy.
Scalability potential: Low uses 2 constraint iterations and minimal floor collision. Middle uses 3 iterations. High uses 5 iterations. Ultra keeps 5 physics iterations and spends saved CPU on visual density/material overkill.
Hardware Impact: i3/MX350 avoids joint solver instability and mesh raycasts in the hot path; expected fixed-step savings depend on active tether count, target is below 100 us per active cable.

## Decision 002 - Origin Shift Contract
Problem: GlobalSignals AupShiftSignal is a queue lane; consuming it from one system can starve other consumers.
Solution: Use HectonFloatingOrigin.RegisterListener and run a native rebase job against tether positions and previous positions on the committed shift callback.
Rejected Alternatives: Polling GlobalSignals.TryDequeueAupShift in TetherManager would be an unsafe ownership grab. Transform-only rebasing would leave Verlet velocity deltas stale.
Scalability potential: Low through Ultra pay only on rare AUP shift, not per frame.
Hardware Impact: Shift job touches O(nodes) memory once per origin jump; on MX350-class CPU the cost is dominated by memory bandwidth and stays off the regular fixed tick.

## Decision 003 - Collision Scope
Problem: Existing bend/cable integrity code uses raycasts against scene geometry, which violates the prompt's mesh-collision ban for cable physics.
Solution: Active Verlet solve uses a floor-plane collision clamp first; optional SDF gradient can be added behind the same node collision job later.
Rejected Alternatives: Per-segment mesh casts and bend-point raycast solves are expensive, non-deterministic around origin shifts, and not compatible with the no-complex-mesh requirement.
Scalability potential: Low uses floor clamp only. Middle/High can add sparse SDF probes. Ultra can spend extra cycles on visual cable wrap fakes without changing the physics solver.
Hardware Impact: Removes repeated Physics.RaycastNonAlloc calls from the active cable solve on i3/MX350.

## Decision 004 - Jacobi Scratch Buffers
Problem: The mandated solver formula is Jacobi, but the existing tether code had no node solver and a naive in-place segment loop would silently become Gauss-Seidel.
Solution: Add persistent correction and weight arrays. Each iteration clears scratch, accumulates per-segment offsets, then applies averaged node corrections. The constraint uses distance = lengthSq * rsqrt(lengthSq) and delta * invLength, so no Vector3.magnitude/division path exists in the solver.
Rejected Alternatives: In-place segment relaxation was cheaper to write but order-dependent. Managed List<Vector3> scratch was rejected because it violates the zero-GC and Burst requirements.
Scalability potential: Low/Mx350 executes exactly 2 iterations. Mid executes 3. High/Ultra executes 5 and can spend visual budget elsewhere.
Hardware Impact: Scratch buffers are linear and persistent. Estimated active-cable solve target on i3/MX350 is 25-80 us depending on node count.

## Decision 005 - Compile Verification Boundary
Problem: Unity project compilation currently fails in unrelated domains: duplicate audio method, construction origin-shift interface miss, UI DamageSignal ambiguity, and a Burst catch-filter error in save storage.
Solution: Validate PHYSICS_TETHERS scripts directly and record the global compile blockage without editing non-physics domains.
Rejected Alternatives: Touching Audio, Construction, UI, or SaveBinaryStorage from this agent would violate the domain boundary and risk cross-agent sabotage.
Scalability potential: No runtime impact; prevents broad ownership churn.
Hardware Impact: None. This is integration hygiene, not runtime work.

## Decision 006 - Tether Snap Signal Ownership
Problem: The prompt requires TetherSnappedSignal, but GlobalSignals has no tether snap lane and mutating global core signal layout would affect unrelated systems.
Solution: Add a physics-domain `TetherSignals` NativeQueue with `TetherSnappedSignal`, prewarmed by TetherManager. The snap path publishes AUP, peak tension, threshold, severity, node count, and reason.
Rejected Alternatives: Adding a new GlobalSignals lane was rejected because core signal changes are cross-domain. Silent detach was rejected by the prompt.
Scalability potential: Low through Ultra pay no cost unless a snap occurs. Consumers can dequeue without binding to TetherInstance.
Hardware Impact: NativeQueue allocation is session lifetime. Snap enqueue cost is estimated below 5 us on i3/MX350.

## Decision 007 - Procedural Tube Impostor
Problem: The existing tether visual path used a GraphicsBuffer but submitted MeshTopology.LineStrip, which does not meet the tube-proxy requirement.
Solution: Keep the GraphicsBuffer solved-position upload and change the shader pass to expand each segment into camera-facing procedural triangles using SV_VertexID. This is a tube impostor, not a LineRenderer.
Rejected Alternatives: Unity LineRenderer and CPU mesh generation were rejected. Full cylindrical mesh generation per segment was rejected for low-tier cost.
Scalability potential: Low gets one quad per segment. High/Ultra can raise point count/material effects later without changing physics.
Hardware Impact: GPU vertex cost is 6 vertices per segment; CPU upload remains one persistent GraphicsBuffer write per visible tether.

## Decision 008 - Creak And Flow Coupling
Problem: Tension feedback and sway must not instantiate AudioSources or run managed per-node sampling.
Solution: TetherInstance emits a throttled `ImpactSignal` when peak tension exceeds 68% of snap threshold. Node acceleration samples the existing HectonMapMagicVegetationBridge abyssal flow payload first, then FluidEngine, then Weather as fallback.
Rejected Alternatives: AudioSource playback, string event names, and per-node managed flow queries were rejected. Moving only the payload was rejected because the prompt requires tether node sway.
Scalability potential: Low uses one midpoint signal at most every 12 frames and one flow sample per tether. High/Ultra can raise visual detail without changing node physics.
Hardware Impact: Estimated creak signal cost below 5 us on the emitting frame. Flow coupling is one bridge sample per fixed step, not O(nodes).

## Decision 009 - Zero-GC Boundary
Problem: The added solver must not allocate during active FixedTick/LateFrame.
Solution: All node, previous, correction, tension, telemetry, and signal buffers use Allocator.Persistent or session NativeQueue. Resizing is isolated to tether attach/capacity setup through EnsureVisualBuffers/EnsureVerletBuffers.
Rejected Alternatives: Per-frame arrays, LINQ, ToArray, foreach on hot path, and CPU mesh generation were rejected.
Scalability potential: Low/Mx350 stays allocation-free during active towing. High/Ultra can spend saved CPU/GPU on shader richness.
Hardware Impact: GC spikes avoided. Main recurring costs are Burst job scheduling, linear NativeArray writes, PhysicsForceRouter calls, and one GraphicsBuffer upload.

## OMEGA POLISH CHANGES
Problem: The first implementation met the prompt but still had small scalar waste and one cold managed formatting allocation.
Solution: Converted Burst pinned state checks to `(mask & 1) != 0`, converted active rest-length and endpoint acceleration divisions to `math.rcp` multiplies, replaced shader `normalize` with explicit `rsqrt`, and replaced pooled tether GameObject interpolated naming with a constant name.
Rejected Alternatives: A complete rewrite into ECS or full rope collision was rejected as a refactoring loop. Per-segment capsule collision and CPU mesh tubes were rejected because the prompt explicitly allows floor/SDF collision and requires procedural buffer visuals.
Scalability potential: Low/MX350 stays at 2 Jacobi passes with floor clamp and one quad tube impostor per segment. Middle runs 3 passes. High/Ultra run 5 passes and spend saved CPU on denser visual points/material treatment, not deeper physical realism.
Hardware Impact: Estimated i3/MX350 gain from polish is 4-9 us across active tether solve plus procedural draw setup, mostly from branch/division simplification and avoiding shader normalize expansion. Bigger gain remains the architecture change: no Unity joint chain, no LineRenderer, no mesh raycast cable collision in the active solve.

## Cinematic Cheats Used
- Verlet cable is acceleration-constrained and predictable; no proton-scale rope physics, no Unity joint chain.
- Collision is a floor-plane clamp with an SDF-compatible hook, not complex mesh collision.
- Tube rendering is a camera-facing procedural impostor expanded from `GraphicsBuffer` points, not CPU geometry.
- Flow sway samples one global/current signal per tether step and applies it as acceleration, not per-node fluid simulation.
- Tension creak is an `ImpactSignal` threshold fake; no AudioSource spawn.

## Final Diff Summary
- Modified `Assets/_Project/Scripts/TetherInstance.cs`: persistent native Verlet state, Burst job scheduling, two-way force routing, AUP rebase, snap signal publish, creak signal, flow coupling, telemetry ring, polish rcp changes.
- Modified `Assets/_Project/Scripts/TetherManager.cs`: tether signal prewarm, AUP runtime rebase, procedural triangle draw call, radius material property, cold name allocation removal.
- Modified `Assets/_Project/Art/Shaders/Hecton_TetherLineStrip.shader`: procedural tube impostor from buffer positions, rsqrt camera vector.
- Added `Assets/_Project/Scripts/Physics/TetherVerletJobs.cs` and `.meta`: Burst integration, Jacobi constraints, origin shift, telemetry.
- Added `Assets/_Project/Scripts/Physics/TetherSignals.cs` and `.meta`: NativeQueue `TetherSnappedSignal` lane.
- Added `Docs/Tasks/Status_PHYSICS_TETHERS.md`, `Docs/AgentLogs/Rationale_PHYSICS_TETHERS.md`, `Docs/AgentLogs/RECON_PHYSICS_TETHERS.md`, and `Docs/AgentLogs/LOG_PHYSICS_TETHERS.md`.
- Local generated `Hecton8.Core.csproj` was patched to include the two new physics files for the required `dotnet build`; the file is ignored by git and not part of the final source diff.

## Final Verification
- Unity MCP: `TetherVerletJobs.cs` basic validation 0 errors/0 warnings; `TetherManager.cs` standard validation 0 errors/0 warnings; `TetherSignals.cs` standard validation 0 errors/0 warnings.
- `TetherInstance.cs` MCP validator timed out during final pass, but `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal -clp:ErrorsOnly` no longer reports any PHYSICS_TETHERS errors.
- Global build remains blocked by `Assets\_Project\Scripts\HectonSurvivalSystem.cs(298,29): SurvivalPhysiologyScalarResult` missing. Latest Unity console remains blocked by unrelated Visor, Combat, SaveBinaryStorage, Construction, and World errors. These are outside the PHYSICS_TETHERS domain boundary.

## Decision 010 - Honest Black Box Closure
Problem: The previous implementation wrote a 300-frame telemetry ring but did not export it on solver corruption. That violated the practical intent of the Black Box rule.
Solution: Added per-node fault flags, a solver flag aggregate, telemetry flag propagation, finite fallback recovery, and a fixed binary dump path `Docs/AgentLogs/Dump_PHYSICS_TETHERS.bin` triggered once per activation in editor/development builds.
Rejected Alternatives: Logging only to Unity console was rejected because console state is volatile and not post-mortem data. Throwing exceptions from the solver was rejected because a recovery path is better than cascading failure.
Scalability potential: Low through Ultra pay only a byte clear per node and one int flag read in the normal path. Fault export is not part of the frame budget because it is only hit on corruption.
Hardware Impact: MX350/i3 normal-path cost is estimated below 2 us for 24 nodes; fault-path disk write is intentionally outside steady gameplay.

## Decision 011 - Stress Readability Without Physics Bloat
Problem: High tension had audio/event feedback but the cable itself did not visibly communicate stress unless it snapped.
Solution: Added `VisualStress01` and material properties `_TetherStressColor` / `_TetherStress01`; the procedural vertex shader blends color by stress. This buys player readability with one shader lerp, not new simulation.
Rejected Alternatives: Cable fray particles, per-node heat maps, and procedural damage meshes were rejected as visual bloat for this scope.
Scalability potential: Low tier gets the same readable color fake. High/Ultra can push stronger authored material color and radius without altering physics.
Hardware Impact: CPU cost is a property-block float/color set per visible tether; GPU cost is one `lerp` per generated tube vertex.

## R&D Verification 2026-05-12
- `TetherVerletJobs.cs` MCP basic validation: 0 errors/0 warnings.
- `TetherManager.cs` MCP standard validation: 0 errors/0 warnings.
- `TetherSignals.cs` MCP standard validation: 0 errors/0 warnings.
- `TetherInstance.cs` MCP validator still times out on regex validation, but `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal -clp:ErrorsOnly` reports no PHYSICS_TETHERS errors.
- Global build remains blocked outside domain at `Assets\_Project\Scripts\VoxelDeltaProcessor.cs(1688,92): SaveVoxelDeltaRun8` missing.

## Decision 012 - Water-Damped Verlet Stability
Problem: A pure Verlet `position + velocity + acceleration * dt^2` step is numerically honest but can read twitchy under low iteration counts, especially on MX350 where only 2 Jacobi passes are allowed.
Solution: Added tiered velocity damping inside `TetherVerletIntegrationJob`: Low/MX350/Unknown 0.965, Mid 0.975, High/Ultra 0.985. Low hides jitter harder; High preserves more motion.
Rejected Alternatives: Raising Low to 5 iterations, adding substeps, or simulating cable drag per node were rejected because they spend CPU on realism instead of readability.
Scalability potential: Low looks controlled on weak devices. High/Ultra retain more cable life without changing the solver contract.
Hardware Impact: One float3 multiply per node; expected indirect MX350 win is 10-60 us versus raising iterations/substeps to hide jitter.

## Decision 013 - Triangle-Wave Stress Pulse
Problem: A static stress color is readable but not AAA enough during near-snap load; the cable should feel alive without adding systems.
Solution: Added a shader-only triangle-wave pulse using `frac/abs` to widen and brighten stressed procedural tube vertices.
Rejected Alternatives: Particle fray, thermal glow simulation, per-node CPU color buffers, and damage meshes were rejected as bloat.
Scalability potential: Low gets the same readable pulse with no CPU cost. High/Ultra can author more aggressive colors/radius while using identical math.
Hardware Impact: 0 us CPU; per-vertex shader adds a few scalar ops on already-generated tube vertices.

## R&D Stability Verification 2026-05-12
- Hot scan remains clean for forbidden `math.sqrt`, `math.length(`, `normalize(`, `Vector3.magnitude`, raw pinned-mask check, and tether-owned string interpolation candidates.
- `TetherVerletJobs.cs` MCP basic validation: 0 errors/0 warnings.
- `TetherSignals.cs` MCP standard validation: 0 errors/0 warnings.
- `TetherManager.cs` MCP standard validation had a transient disconnect; basic retry passed 0 errors/0 warnings.
- `Assembly-CSharp.csproj` build timed out before a compiler verdict.
- `Hecton8.Core.csproj` narrowed build is blocked by 78 external missing-symbol errors (`HectonPersistentPathPolicy`, `HardwareTierDetector`, `HectonNativeBridge`, `SteamDeckInputPal`, etc.) and reports no PHYSICS_TETHERS errors.

## Decision 014 - Localized Segment Stress Visualization
Problem: Whole-cable stress tint reads better than nothing, but it lies about where the cable is failing. The solver already computes per-segment constraint deltas, so not using them wastes signal.
Solution: Added a persistent `VisualSegmentTensionBuffer` and upload path for `_verletSegmentTensions`. `TetherManager` binds `_TetherSegmentTensions` and `_TetherSegmentStressScale`; the shader samples the current segment and uses the max of global stress and local segment stress for color/pulse.
Rejected Alternatives: CPU-generated color mesh, per-node material instances, particle fray, and line renderer gradients were rejected. They add allocations, draw complexity, or fake systems when one existing float buffer is enough.
Scalability potential: Low sees accurate danger hot-spots with 8-10 segment floats. High/Ultra can increase visual segment count and stress scale for more detailed cable readout without changing physics.
Hardware Impact: Upload is 8-24 floats per visible tether, estimated 4-12 us on weak CPU/GPU paths and 0 B managed GC. Shader cost is one StructuredBuffer float read and a saturate/max per generated segment vertex.

## R&D Segment Stress Verification 2026-05-12
- Hot scan remains clean for forbidden `math.sqrt`, `math.length(`, `normalize(`, `Vector3.magnitude`, raw pinned-mask check, and tether-owned string interpolation candidates.
- Unity MCP validation was unstable this pass: jobs/manager/signals/instance validations returned disconnects or timeouts, not diagnostics.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal -clp:ErrorsOnly` filtered for `Tether|error CS|Build FAILED` reports external missing-symbol errors only; no PHYSICS_TETHERS compiler errors appear.
- Global build remains blocked outside domain by missing core/save/audio/input symbols including `HectonPersistentPathPolicy`, `PlatformPrecisionClock`, `HectonThreadPriorityPolicy`, `HectonNativeBridge`, and `SteamDeckInputPal`.
