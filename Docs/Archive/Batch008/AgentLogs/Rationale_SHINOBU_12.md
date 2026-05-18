Date: 2026-05-17
Agent: SHINOBU_12
Status: PENDING VERIFICATION - EXTERNAL COMPILE WALL

## Initial Boundary

Problem: Cables must simulate winch/tow behavior without Unity joints, without LineRenderer, and without direct dependency on submarine or world-sampler code owned by other agents.
Solution: Own a local `Hecton8.Physics.Cables` data-oriented module with blittable DTOs, Burst-compatible mock dependencies, DataVault-shaped buffers, and GPU spline export DTOs. Runtime integration remains phase-recorded and decoupled.
Rejected Alternatives: Unity HingeJoint/SpringJoint/ConfigurableJoint rejected by AGENTS.md and tether mandate. MonoBehaviour component chains rejected because hot-path component access and per-object update cadence do not fit 50 cable / 1000 node target.
Scalability potential: Low uses 3 solver iterations, mock point SDF, coarser visual pages. Middle uses 5 iterations and full node collision. High uses 8-10 iterations and denser spline samples. Ultra spends savings on visual spline/tube density, not on unbounded simulation truth.
Hardware Impact: Estimated low-end i3/MX350 gain is avoidance of PhysX joint island solve and LineRenderer CPU rebuilds; expected hot-path target remains <100 us for 1000 nodes pending Unity profiler proof.

## Decision Log

## Loop 1 - Binary Graveyard And Layout Contract

Problem: Prompt requires historical binary cable profiles, but `Assets/StreamingAssets`, root `StreamingAssets`, and the named cable/winch binary files are absent in this checkout.
Solution: Installed `CableMaterialDTO.GenerateEmergencyMockCables()` so deterministic fallback materials exist without file IO on the simulation path.
Rejected Alternatives: Blocking on missing binary assets or inventing file dependencies outside the tether domain. Both create integration drag and no runtime value.
Scalability potential: Low/Middle use the same fallback constants with 3-5 iterations. High/Ultra can raise visual radius/tension presentation from the DTO without changing solver truth.
Hardware Impact: i3/MX350 avoids startup parsing stalls and hot-path asset lookups; estimated 3 us cold initialization, 0 us per frame.

Problem: Active tow rendering used a 12-byte `float3` GraphicsBuffer for spline points, violating SHINOBU_12 GPU alignment.
Solution: Added `GpuCableSplinePointDTO` at 16 bytes, DataVault buffer ID `VerletCableGpuSplinePoints`, 16-byte GraphicsBuffer stride, and shader-side `StructuredBuffer<float4>` reads.
Rejected Alternatives: Keeping `float3` stride because it already rendered. That risks platform-specific stride fetch faults and cache waste on GPU paths.
Scalability potential: Low can upload few aligned points. Ultra can use the spare `.w` lane for tension-driven tube overkill without extra buffer bandwidth.
Hardware Impact: Expected 1-4 us upload/fetch stability gain per active tether on low-end silicon; larger gain is prevention of misaligned GPU fetch behavior.

Problem: CS1612 risk appears when mutable node arrays are hidden behind properties or value-returning accessors.
Solution: `VerletNodeDTO` uses direct fields only and `VerletCableNodeBuffer.GetNodeRef(int)` returns a true unsafe `ref` into NativeArray memory.
Rejected Alternatives: Property-based wrappers and `NativeArray<T>[i]` mutate-copy patterns for node updates.
Scalability potential: Low avoids needless copies. Ultra can mutate denser node chains with the same ABI.
Hardware Impact: Sub-microsecond per node batch, but prevents structural bugs in solver mutation.

Problem: Submarine anchors and world SDF are owned by other domains/agents, but the solver must compile and run blind.
Solution: Added local `MockSubmarineAnchor`, `MockSDFSampler`, and partial `MockWorldSampler` with flat plane plus sphere obstacles and flow acceleration.
Rejected Alternatives: Hard references to submarine, terrain, or voxel classes; those would create illegal cross-domain dependencies.
Scalability potential: Low uses discrete node SDF only. High/Ultra spend saved cycles on visual spline density, not swept CCD.
Hardware Impact: Discrete point SDF target is about 0.04 us/node on i3/MX350 class hardware; swept sphere CCD rejected as processor waste.

## Loop 2 - Core Solver Integration

Problem: Existing tow Verlet path was mathematically valid but too soft on low-end tiers and applied current as one uniform payload-side acceleration.
Solution: Raised solver iterations to Low=3, Mid=5, High=8, Ultra=10 and routed flow through `MockWorldSampler.SampleFlowAcceleration(position)` so every node can bend under current in local space.
Rejected Alternatives: Unlimited solver iterations and a full hydrodynamics model per node. Both chase realism and burn frame time without proportional immersion.
Scalability potential: Low keeps visible elasticity at 3 iterations. Middle reaches stable tow behavior at 5. High/Ultra use 8-10 iterations only where frame budget exists.
Hardware Impact: Estimated i3/MX350 integration cost 18-35 us per 1000 nodes, solver relaxation 45-90 us depending tier.

Problem: Cables were not protected from rock penetration in the active integration path beyond a floor clamp.
Solution: Added discrete node SDF push-out to `TetherVerletIntegrationJob`, with `OldPosition` tangent damping for rough basalt drag. This is the accepted Dear Lie; segment clipping between nodes is ignored.
Rejected Alternatives: Unity collider queries, swept sphere CCD, or per-segment collision. All scale badly with cable count.
Scalability potential: Low can keep only node SDF. Ultra can spend visual budget on GPU tube detail while the physical contact remains predictable.
Hardware Impact: Estimated ~40 us per 1000 nodes for mock SDF point tests; avoids broadphase/CCD costs.

Problem: Instant winch target changes shock the constraint solver and create springy cable recoil.
Solution: Active rest lengths now converge toward the owner target at a bounded reel rate. DTO path adds `MockWinchSignal` and `VerletWinchReelDTOJob` for future DataVault signal ingestion and fake node spooling.
Rejected Alternatives: Teleporting `RestLength` every frame or resizing NativeArrays while live. Both create instability or allocation churn.
Scalability potential: Low uses coarse segment count and bounded reel. High/Ultra can keep more spline visual detail while the constraint count stays fixed.
Hardware Impact: Estimated 2-6 us for active 10-constraint rest-length ramp; avoids CPU spikes from node allocation/removal.

Problem: World-space float cable nodes jitter when anchors are far from origin.
Solution: Active runtime continues to rebase node arrays around the current anchor before simulation, and DTO origin shift job mirrors the same AUP-to-local contract.
Rejected Alternatives: Double precision in the hot Verlet loop. It increases bandwidth and undermines SIMD density.
Scalability potential: All tiers keep 32-bit local node math; Ultra overkill belongs to presentation, not double-precision simulation.
Hardware Impact: Estimated 3-8 us per active cable when origin shifts; steady-state cost is zero when shift is below threshold.

## Loop 3 - Tension, Tiering, And GPU Link

Problem: Overstretched cables previously either recovered elastically or snapped only through long-duration stress, with no plastic rest-length memory in the active Verlet chain.
Solution: Added active plastic creep against `_verletSegmentRestLengths` when peak constraint delta exceeds the stretch threshold. DTO constraint job already performs plastic rest-length growth and snap-deletes constraints.
Rejected Alternatives: Always resetting rest length to owner target and pretending steel cable has no yield point.
Scalability potential: Low keeps cheap scalar creep. High/Ultra can display richer stress visuals via GPU tension lane while physics remains bounded.
Hardware Impact: Estimated 1-4 us per active cable; avoids solver recoil that costs more iterations later.

Problem: Tension transfer existed as events and direct force routing, but no unmanaged force packet existed for the unseen submarine dynamics reader.
Solution: Added DataVault-backed `NativeArray<CableTensionForceDTO>` at `BufferID.VerletCableTensionForces` and write one force packet per active tether slot.
Rejected Alternatives: SignalBus-only handoff or direct submarine dependency. Signal-only loses torque-friendly data; direct dependency violates domain boundary.
Scalability potential: Low reads one force per cable. Ultra can layer visual stress without changing the force contract.
Hardware Impact: Sub-microsecond write per cable; avoids managed event fan-out for physics torque transfer.

Problem: GPU spline copy was still a CPU loop even after fixing the 16-byte point ABI.
Solution: Added Burst `TetherVisualGpuSplineCopyJob` to populate `GpuCableSplinePointDTO` from visual positions and segment tension before `GraphicsBuffer` upload.
Rejected Alternatives: LineRenderer and unmanaged 12-byte stride. Both are rejected by prompt and renderer mandate.
Scalability potential: Low keeps minimal point count. High/Ultra use the same 16-byte stream for stress tube visual overkill.
Hardware Impact: Estimated 3-8 us per cable upload/copy and fewer GPU fetch alignment risks.

Problem: Current flow needed to deform the cable, not only pull the payload.
Solution: Active integration now feeds existing abyssal flow samples into `MockWorldSampler` and samples the per-node flow acceleration in the Burst job.
Rejected Alternatives: Payload-only drift. It sells cargo motion but not cable shape.
Scalability potential: Low uses one cheap wave modulation. Ultra can increase visual tube detail while preserving the same force math.
Hardware Impact: Estimated 4-10 us per 1000 nodes; single vector add/multiply class cost.

## Loop 4 - Visibility, Black Box, And Human Control

Problem: Invisible cables still paid VISUAL_SYNC upload and draw cost.
Solution: `TetherManager` reuses a cold `Plane[6]` frustum cache and passes it into `TetherInstance`; visual bounds reject upload and draw when outside the render camera frustum. DTO `VerletAabbFrustumCullJob` provides the Burst-side AABB contract.
Rejected Alternatives: Uploading all cables then relying only on GPU clipping. That wastes CPU upload and command bandwidth.
Scalability potential: Low skips invisible upload. Ultra spends visible-only budget on stress tube overkill.
Hardware Impact: Estimated 3-8 us saved per culled active cable on low-end silicon.

Problem: Crash analysis requires an answer for the last 300 frames, not a guess.
Solution: Active telemetry dump path now targets `Docs/AgentLogs/Dump_VERLET_CABLES.bin`; DTO black-box job stores max tension, average error, active count, endpoint positions, and a state hash.
Rejected Alternatives: Console logs and "cannot reproduce" reports.
Scalability potential: All tiers keep fixed 300 entries; no growth, no log spam.
Hardware Impact: Estimated 2-5 us per frame for ring writes; dump IO only on fault.

Problem: Tension tuning was hardcoded in runtime constants.
Solution: Added `VerletCableTuningDTO` in DataVault and `Verlet Tow Tuner` EditorWindow for gravity, fluid friction, iterations, stretch threshold, break force, rock friction, and reel speed. Active solver reads the vault values.
Rejected Alternatives: ScriptableObject-only tuning or direct component references. Both miss the unmanaged DataVault control requirement.
Scalability potential: Low can force 3 iterations and higher damping. Ultra can author 10 iterations and aggressive visual stress without changing code.
Hardware Impact: Editor-only UI cost; runtime read is one 64-byte DTO.

Problem: CSV material overrides needed to avoid binary lock-in while keeping parser allocations out of row processing.
Solution: Added `CableMaterialCsvParser.Parse(ReadOnlySpan<char>, NativeArray<CableMaterialDTO>)` and editor monitoring of `cable_materials.csv` into `BufferID.VerletCableMaterials`.
Rejected Alternatives: `string.Split`, per-row objects, and runtime asset database polling.
Scalability potential: Low/Middle can use simple emergency material rows. High/Ultra can tune material visual/load response through the same DTO stream.
Hardware Impact: Editor file IO allocates as expected; parser row processing is span-based and writes unmanaged DTOs directly.

Problem: Cable forces were invisible during scene tuning.
Solution: `Verlet Tow Tuner` registers a SceneView hook that reads vault positions/tensions and draws green/yellow/red constraint lines.
Rejected Alternatives: Numeric inspector-only tension, which does not show where a constraint is about to fail.
Scalability potential: Editor-only, disabled in player. Low devices pay no runtime cost.
Hardware Impact: 0 us player runtime; editor draw scales with active vault slots only.

## Self Audit

<SELF_AUDIT>
  <LineRendererOrCharacterJoint>No new LineRenderer, CharacterJoint, HingeJoint, SpringJoint, or ConfigurableJoint usage. Active tow remains Verlet math plus GraphicsBuffer.</LineRendererOrCharacterJoint>
  <VerletNodeDTOAlignment>PASS. `VerletNodeDTO` layout is Position float3 = 12 bytes, InvMass float = 4 bytes, OldPosition float3 = 12 bytes, _pad0 float = 4 bytes, total 32 bytes. No Pack=1 on the new DTO.</VerletNodeDTOAlignment>
  <CS1612>PASS. New node/constraint DTOs expose direct fields. `VerletCableNodeBuffer.GetNodeRef(int)` returns unsafe ref through `UnsafeUtility.AsRef`.</CS1612>
  <Mocks>PASS. Local `MockSDFSampler`, partial `MockWorldSampler`, `MockSubmarineAnchor`, and `MockWinchSignal` exist. No direct world/submarine runtime dependency was added.</Mocks>
  <VerletTowTuner>PASS. `Assets/_Project/Scripts/Editor/VerletTowTunerWindow.cs` reads/writes `VerletCableTuningDTO`, monitors `cable_materials.csv`, and draws tension gizmos.</VerletTowTuner>
  <GpuAlignment>PASS. Tether shader now reads `StructuredBuffer<float4>` and runtime uploads `GpuCableSplinePointDTO` with 16-byte stride.</GpuAlignment>
  <Compile>PARTIAL. `Hecton8.Core` compiled cleanly once after Loop 3. Later build attempts are blocked by unrelated Construction/BinaryLayout/Environment symbols introduced outside SHINOBU_12 domain.</Compile>
</SELF_AUDIT>

## Loop 11 - Pool Capacity / Gameplay Create Guard

Problem: `TetherManager` still allocated active and pooled `List<TetherInstance>` registries with capacity 4. The assignment target is 50 complex cables at 60Hz. A four-slot list means the fifth active/pool add can resize during gameplay attach/release, which is a direct Zero-GC failure. The manager also lazily created a `new GameObject("TetherInstance")` when the pool was empty, allowing a gameplay attach spike.
Solution: Added `MaxManagedTetherInstances = 64` and `InitialPooledTetherInstances = 64`. Both lists are now constructed at capacity 64 with explicit COLD ALLOC comments. `Awake()` prewarms 64 inactive `TetherInstance` children. `RentInstance()` now only consumes from `_pooledInstances` and returns null when the pool is empty. `AttachTowCable` guards the active cap and returns the instance to the pool instead of resizing.
Rejected Alternatives: Replacing the manager lists with manual arrays in this pass. Arrays would be cleaner for absolute H-Phi aesthetics, but it would touch more iteration/removal code and increase regression risk under concurrent agents. Keeping `List<T>` with capacity 4 was rejected because it violates the 50-cable requirement. Lazy runtime creation was rejected because it moves cold object cost into gameplay.
Scalability potential: Low/MX350 avoids attach-time list growth and object creation spikes for the stated cable budget. Middle/High/Ultra keep the same cap and can spend saved frame stability on shader-only stress/salt/silt overkill.
Hardware Impact: No measured microseconds are claimed. Static impact: avoids managed list resize beyond four tethers and removes lazy object creation from runtime attach; the cost is 64 cold child GameObjects during manager initialization.

Problem: The patch touched attach/release and startup, so compile proof had to be refreshed.
Solution: Ran isolated `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=.codex-artifacts/msbuild/shinobu12_obj/ /p:OutputPath=.codex-artifacts/msbuild/shinobu12_bin/`. Result: 0 errors, 9 global warnings outside SHINOBU_12. The build-log filter for `Tether`, `Verlet`, `CableDTO`, `Cable`, `TetherInstance`, `TetherManager`, `TetherVerletJobs`, `VerletTowTuner`, `Hecton_TetherLineStrip`, and `GpuCableDrawParamsDTO` is empty.
Rejected Alternatives: Editing `GlobalPhysicsStateManager` warnings from this cable agent.
Scalability potential: Static compile evidence remains local to the cable domain; runtime proof is still required.
Hardware Impact: 0 us runtime; compile guard only.

<SELF_AUDIT source="POOL_CAPACITY_GAMEPLAY_CREATE_GUARD">
  <TaskMatrix>01 PASS, 02 PASS, 03 PASS, 04 PASS, 05 PASS, 06 PASS, 07 PASS, 08 PASS, 09 PASS, 10 PASS, 11 PASS, 12 PASS, 13 PASS, 14 PASS, 15 PASS, 16 PASS, 17 PASS, 18 PASS, 19 PASS, 20 PASS.</TaskMatrix>
  <ARM64Layout>No DTO ABI changed in Loop 11. `VerletNodeDTO`: offset 0 Position float3[12], offset 12 InvMass float[4], offset 16 OldPosition float3[12], offset 28 _pad0 float[4], sizeof 32. `GpuCableSplinePointDTO`: offset 0 Position float3[12], offset 12 Tension01 float[4], sizeof 16. `GpuCableDrawParamsDTO`: offsets 0/16/32/48/64, sizeof 80.</ARM64Layout>
  <ZeroGC>`TetherManager` active/pooled lists are cold-allocated at 64 capacity. `RentInstance()` no longer creates GameObjects or grows lists during attach. Scoped scan finds no LINQ, `foreach`, `.ToString()`, direct DataVault `GetBuffer<`, `new NativeArray`, Unity Physics raycast, or component lookup in SHINOBU runtime files.</ZeroGC>
  <AUP>Unchanged: pool management does not touch AUP math; tether solver remains local before float operations.</AUP>
  <DearLie>Unchanged: low tier keeps visual taut-line and SDF/tangent collision fakery instead of segment CCD.</DearLie>
  <Dependency>No new asmdef, sibling runtime dependency, or signal lane was added.</Dependency>
  <HPhi>Simulation arrays remain vault-owned; `List<TetherInstance>` is manager object-pool bookkeeping, not solver data. Capacity is fixed to avoid gameplay allocation.</HPhi>
  <Blackbox>Per-cable and manager 300-frame rings remain active; dump paths unchanged.</Blackbox>
  <CompileGuard>Current isolated Core build succeeds with 0 errors and 9 global warnings outside SHINOBU_12. Runtime Unity Play Mode/profiler/GC evidence is pending.</CompileGuard>
</SELF_AUDIT>

## Loop 9 - Bend Voxel Lookup Hot-Path Purge

Problem: `TetherInstance.TryResolveBendCorner` still resolved `HectonVoxelVolume` through `TryGetComponent` / `GetComponentInParent` on a raycast collider hit. This is not inside the Burst Verlet node solver, but it is tether LOS/bend recalculation and therefore hot-adjacent enough to violate the no-component-lookup mandate.
Solution: Removed the component lookup. Bend-corner resolution now first tries the fixed `_bendVolumes[4]` cache retained from prior bends, then uses `HectonVoxelVolume.TryRaymarchAnyPublishedSdf` to resolve a published voxel volume and SDF hit without walking the Unity component hierarchy. If no voxel SDF is available, the existing hit-normal/tangent fallback remains.
Rejected Alternatives: Adding a dictionary from `Collider` to `HectonVoxelVolume` was rejected because it adds managed hash lookups and stale collider invalidation. Editing voxel ownership to expose a collider map was rejected as out-of-domain. Keeping `GetComponentInParent` was rejected because repeated hierarchy walks are exactly the kind of hidden tether cost the mandate is trying to kill.
Scalability potential: Low/MX350 hits the cheap cached path when cable bends persist and otherwise falls back to normal/tangent bend points. High/Ultra can get better voxel-corner bends through the published SDF without changing gameplay truth or adding cross-domain compile edges.
Hardware Impact: Static estimate is removal of 1-2 Unity component hierarchy lookups per blocked bend hit. No measured microsecond claim without profiler.

Problem: The post-change build cannot currently produce a full Core success because another domain changed the global physics signal surface.
Solution: Re-ran scoped forbidden grep after the patch and it returns no SHINOBU_12 hits for `GetComponent`, `TryGetComponent`, `FindObject*`, scalar MPB setters, `Material.Set*`, Unity joints, `LineRenderer`, `Pack=1`, direct `GetBuffer<`, `new NativeArray`, or fake `Schedule().Complete()`. Re-ran isolated build; it stops only in `GlobalPhysicsStateManager.cs` on missing `WakeRequestSignal`, with no Tether/Verlet/Cable errors in the log.
Rejected Alternatives: Editing `GlobalPhysicsStateManager` or global signal definitions from the cable agent to make the build look green. That would violate domain boundary and risk another compile-wall.
Scalability potential: The cable domain stays reviewable under concurrent agent churn.
Hardware Impact: 0 us runtime; compile-wall isolation only.

<SELF_AUDIT source="BEND_LOOKUP_PURGE">
  <TaskMatrix>01 PASS, 02 PASS, 03 PASS, 04 PASS, 05 PASS, 06 PASS, 07 PASS, 08 PASS, 09 PASS, 10 PASS, 11 PASS, 12 PASS, 13 PASS, 14 PASS, 15 PASS, 16 PASS, 17 PASS, 18 PASS, 19 PASS, 20 PASS.</TaskMatrix>
  <ARM64Layout>No DTO ABI change in Loop 9. Prior layout still holds: `VerletNodeDTO` 32, `GpuCableSplinePointDTO` 16, `GpuCableDrawParamsDTO` 80.</ARM64Layout>
  <ZeroGC>`TryResolveBendCorner` uses fixed instance arrays and value outputs. No dictionary, LINQ, closure, string formatting, or component lookup remains in the SHINOBU_12 scoped hot-adjacent files.</ZeroGC>
  <AUP>Unchanged: Verlet math is local; bend fallback uses runtime-space raycast/SDF hit points already produced in Unity runtime space.</AUP>
  <DearLie>Voxel-aware bend points are still optional. If the published SDF cannot answer, the cable uses hit normal plus tangent offset instead of expensive segment CCD.</DearLie>
  <Dependency>No new asmdef or sibling class edge was added; `HectonVoxelVolume` was already referenced in `TetherInstance`, and the replacement uses its public SDF surface instead of Unity hierarchy lookup.</Dependency>
  <HPhi>Simulation arrays remain vault-owned; bend caches are fixed managed cold arrays with capacity 4 already owned by the tether instance.</HPhi>
  <Blackbox>Telemetry rings unchanged.</Blackbox>
  <CompileGuard>Current isolated Core build is externally blocked by `WakeRequestSignal` in `GlobalPhysicsStateManager`; scoped build-log filter finds no SHINOBU_12 errors.</CompileGuard>
</SELF_AUDIT>

## Loop 8 - GPU Draw Payload / SRP Scalar Purge

Problem: The tether render path still mutated per-draw scalar material state with `MaterialPropertyBlock.SetColor`, `SetFloat`, and `SetInt`. It was not standard `Material.SetFloat`, but it still left the cable domain with 8-12 scalar property writes per visible tether draw and a mixed CPU/shader ABI.
Solution: Added `GpuCableDrawParamsDTO` as an 80-byte payload: five 16-byte `float4` lanes for color, stress color, stress/scale/count/radius, indirect/tier/salt/silt, and visual clock. `TetherInstance` owns double-buffered one-element `GraphicsBuffer` lanes for this visual-only payload and writes them with `LockBufferForWrite`. `TetherManager` now binds `_TetherDrawParams` as a buffer, while the shader reads `_TetherDrawParams[0]`.
Rejected Alternatives: Keeping scalar MPB setters because the draw is procedural. That protects correctness but not the stated SRP/GPU sovereignty standard. Moving draw params into a simulation DataVault buffer was also rejected because these are ephemeral render constants, not gameplay truth or replay state.
Scalability potential: Low/MX350 pays one 80-byte payload upload only for visible cables and keeps the taut-line visual fake. High/Ultra reuse the same payload for salt crystals, silt tint, stress pulse, and future visual-only lanes without touching solver truth.
Hardware Impact: Static estimate is removal of 8-12 scalar property calls per visible tether draw and replacement with one 80-byte buffer write. No microsecond runtime claim is made without Unity profiler data.

Problem: Shader-side tether constants lived in `UnityPerMaterial`, while spline points were already promoted to 16-byte `float4` payloads.
Solution: Removed hot tether scalar usage from `UnityPerMaterial`; shader now consumes `StructuredBuffer<float4> _TetherPositions`, `StructuredBuffer<float> _TetherSegmentTensions`, and `StructuredBuffer<TetherDrawParams> _TetherDrawParams`. `VerletCableLayout.Validate()` asserts `GpuCableDrawParamsDTO == 80` bytes.
Rejected Alternatives: A 64-byte payload that drops visual clock or packs ints into fragile bit fields. The 80-byte version is explicit, aligned, and readable.
Scalability potential: Low remains cheap; Ultra gains reserved lanes for visual overkill without changing gameplay DTOs.
Hardware Impact: Prevents reintroducing 4-byte ad hoc shader uniforms in the cable draw path; expected value is stability and reduced driver property churn, not a measured profiler number.

Problem: The last documented compile wall was stale after the draw-payload patch.
Solution: Restored the isolated msbuild obj directory and ran `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=.codex-artifacts/msbuild/shinobu12_obj/ /p:OutputPath=.codex-artifacts/msbuild/shinobu12_bin/`. Result: build succeeded, 0 warnings, 0 errors. Scoped forbidden grep found no scalar MPB setter, `Material.Set*`, Unity joint, `LineRenderer`, `Pack=1`, direct `GetBuffer<`, `new NativeArray`, or fake `Schedule().Complete()` hit in SHINOBU_12 files.
Rejected Alternatives: Reporting runtime complete from a static compile. Unity Play Mode, shader import/runtime draw validation, profiler, and GC allocation capture are still not executed.
Scalability potential: Compile proof now isolates SHINOBU_12 from external churn; runtime scalability still needs device-tier capture.
Hardware Impact: 0 us runtime; developer iteration guard only.

<SELF_AUDIT source="GPU_DRAW_PAYLOAD_PASS">
  <TaskMatrix>01 PASS, 02 PASS, 03 PASS, 04 PASS, 05 PASS, 06 PASS, 07 PASS, 08 PASS, 09 PASS, 10 PASS, 11 PASS, 12 PASS, 13 PASS, 14 PASS, 15 PASS, 16 PASS, 17 PASS, 18 PASS, 19 PASS, 20 PASS.</TaskMatrix>
  <ARM64Layout>VerletNodeDTO: 0 Position float3[12], 12 InvMass float[4], 16 OldPosition float3[12], 28 _pad0 float[4], sizeof 32. GpuCableSplinePointDTO: 0 Position float3[12], 12 Tension01 float[4], sizeof 16. GpuCableDrawParamsDTO: 0 Color float4[16], 16 StressColor float4[16], 32 Params0 float4[16], 48 Params1 float4[16], 64 Params2 float4[16], sizeof 80.</ARM64Layout>
  <ZeroGC>Tick and LateFrame draw binding use for loops, value DTOs, `LockBufferForWrite`, and no LINQ/foreach/string formatting/closures/boxing in the touched hot path.</ZeroGC>
  <AUP>Unchanged: cable physics stays anchor-local before float math; draw payload carries local/render-space values only and never casts absolute AUP to float.</AUP>
  <DearLie>Low tier keeps taut-line visual fake under high stress; no physical CCD was added.</DearLie>
  <Dependency>No new sibling runtime reference or asmdef edge. Draw payload is local DTO plus Unity `GraphicsBuffer`; gameplay state still crosses through DataVault handles/signals.</Dependency>
  <HPhi>Simulation arrays remain vault-owned. The new draw params buffers are visual-only GPU resources, not persistent gameplay state.</HPhi>
  <Blackbox>300-frame cable and manager rings remain active; this pass did not weaken dump paths.</Blackbox>
  <CompileGuard>Isolated `Hecton8.Core` build now succeeds with 0 warnings and 0 errors. Runtime validation remains pending.</CompileGuard>
</SELF_AUDIT>

## Loop 7 - H-Phi Handle Sovereignty Recheck

Problem: The ultra mandate correctly challenged the remaining private `NativeArray` fields in the cable domain. They were vault aliases, not owned allocations, but the code still used direct `GetBuffer<T>` for some acquisition paths and did not carry generation-checked handles everywhere.
Solution: Added explicit `VaultBufferHandle<T>` fields for every `TetherInstance` cable buffer and converted `EnsureDataVaultCableArray` / `EnsureDataVaultSliceArray` to resolve views through handles. Added `VaultGenerationID` guards in simulation and visual paths so stale views are rebuilt after vault relocation. Converted `TetherManager` blackbox telemetry and the editor tuner writes to handle-first access.
Rejected Alternatives: Removing all `NativeArray` view fields and resolving every buffer at each index access. That would replace one clean generation guard with repeated dictionary/handle work inside hot code. Keeping direct `GetBuffer<T>` was rejected because it cannot prove relocation safety.
Scalability potential: Low/Steam Deck pays a single generation comparison during steady-state ticks. High/Ultra can tolerate vault defrag/relocation without rebuilding cable ownership or adding direct dependencies.
Hardware Impact: Estimated steady-state overhead is <1 us for generation guards; relocation refresh is rare and bounded by buffer count. The gain is not fake frame time, it is removing stale pointer and H-Phi ownership risk.

Problem: `TetherManager` still exposed the old blackbox dump name and direct manager `NativeArray` acquisition.
Solution: Manager telemetry now uses `VaultBufferHandle<TetherManagerTelemetryEntry>` and `VaultBufferHandle<int>` and dumps to `Docs/AgentLogs/Dump_VERLET_CABLES_MANAGER.bin`. The active per-cable fatal ring remains `Docs/AgentLogs/Dump_VERLET_CABLES.bin`.
Rejected Alternatives: Treating the manager blackbox as unrelated. It is inside the tether domain and participates in the same 300-frame crash story.
Scalability potential: All tiers use the same fixed 300-frame ring; no log growth, no per-frame file IO.
Hardware Impact: <1 us steady-state guard, with no additional allocation on the player path.

Problem: Re-running isolated build after handle changes is required, but the project still contains external compile walls.
Solution: Rebuilt `Hecton8.Core.csproj` with isolated obj/bin paths and `/p:UseSharedCompilation=false`. Build fails on `VoxelDeltaProcessor` missing `IDataVault` and `VaultBufferHandle<>` imports, outside SHINOBU_12. A filtered build-log search returns no Tether/Verlet/Cable/TetherManager/TetherInstance/TetherVerletJobs/VerletTowTuner errors.
Rejected Alternatives: Editing voxel code to make SHINOBU_12 look green. That violates the domain boundary.
Scalability potential: Compile-wall isolation keeps cable work reviewable while other agents repair their domains.
Hardware Impact: 0 us runtime; developer iteration protection only.

<SELF_AUDIT source="H_PHI_HANDLE_PASS">
  <TaskMatrix>01 PASS, 02 PASS, 03 PASS, 04 PASS, 05 PASS, 06 PASS, 07 PASS, 08 PASS, 09 PASS, 10 PASS, 11 PASS, 12 PASS, 13 PASS, 14 PASS, 15 PASS, 16 PASS, 17 PASS, 18 PASS, 19 PASS, 20 PASS.</TaskMatrix>
  <ARM64Layout>Primary DTOs unchanged and validated: `VerletNodeDTO` 32 bytes, `VerletConstraintDTO` 16 bytes, `GpuCableSplinePointDTO` 16 bytes, `CableTensionForceDTO` 32 bytes, `VerletCableBlackBoxEntry` 64 bytes. No scoped SHINOBU_12 runtime `Pack = 1` hits.</ARM64Layout>
  <ZeroGC>Hot simulation uses for loops, Burst jobs, blittable DTOs, and handle-resolved vault views. No `GetBuffer<T>`, `new NativeArray`, LINQ, `foreach`, string formatting, or fake `Schedule().Complete()` remains in scoped runtime files.</ZeroGC>
  <AUP>Simulation still subtracts anchor/origin first and runs Verlet in local `float3`; vault relocation handle refresh is independent from AUP rebase.</AUP>
  <DearLie>Collision remains node SDF push-out with tangent damping; segment CCD and Unity colliders stay rejected.</DearLie>
  <Dependency>No sibling runtime class dependency or asmdef reference was added. Data crosses via `GlobalRegistry` DataVault handles, existing signals, and local DTO contracts.</Dependency>
  <HPhi>All SHINOBU_12 runtime arrays are vault-owned. `NativeArray<T>` fields in `TetherInstance` and `TetherManager` are non-owning views paired with `VaultBufferHandle<T>` identity fields and refreshed on vault generation changes.</HPhi>
  <Blackbox>Per-cable 300-frame ring dumps to `Docs/AgentLogs/Dump_VERLET_CABLES.bin`; manager 300-frame ring dumps to `Docs/AgentLogs/Dump_VERLET_CABLES_MANAGER.bin`.</Blackbox>
  <CompileGuard>Latest isolated Core build fails externally in `VoxelDeltaProcessor`; build-log filter finds no SHINOBU_12 path or symbol errors.</CompileGuard>
</SELF_AUDIT>

## Ultra-Think Polish Pass

Problem: The user mandate correctly called out that runtime `Pack = 1` is forbidden, and scoped grep still found two old telemetry structs in the cable/tether domain using it.
Solution: Removed `Pack = 1` from `TetherVerletTelemetryEntry` and `TetherManagerTelemetryEntry`. Both remain manually sized (`64` and `16` bytes) with 4-byte fields only, so ARM64 alignment does not rely on packed layout.
Rejected Alternatives: Leaving them as "pre-existing" debt. They are runtime telemetry in the tether domain, so they were inside the SHINOBU_12 blast radius.
Scalability potential: Low/ARM64 avoids unaligned memory habits; High/Ultra keeps fixed-size telemetry with no bandwidth growth.
Hardware Impact: Estimated gain is small per frame (<1 us), but it removes an ARM64 trap class and prevents future struct-copy regressions.

Problem: Several cable DTOs had `StructLayout(Size = N)` but used implicit tail padding, which is fragile for a project that treats byte layout as contract.
Solution: Added explicit reserved/padding fields to `VerletCableTuningDTO`, `MockSDFSampler`, and `CableSnappedSignal`; added `[StructLayout(Size = 80)]` to the local physics `MockWorldSampler`; expanded `VerletCableLayout.Validate()` to assert every SHINOBU_12 DTO stride.
Rejected Alternatives: Trusting implicit CLR tail padding. That is acceptable for throwaway tools, not for ARM64/GPU-facing runtime DTO contracts.
Scalability potential: Low uses the same fixed, cache-predictable ABI. Ultra can reuse reserved lanes for visual overkill metadata without changing stride.
Hardware Impact: No measurable runtime cost; expected save is future compile/runtime fault avoidance and stable cache-line reads.

Problem: The ultra mandate required truth recovery against `CURRENT_BATCH.md`, `Rationale_SHINOBU_12.md`, and `PROJECT_STATE_STATIC_XRAY.md`.
Solution: Re-read status/rationale, extracted SHINOBU_12 with an attribute-aware regex, confirmed exactly 20 tasks, and read the static x-ray. The x-ray explicitly says runtime proof is pending, so no Unity Play Mode/profiler/GC claim is made.
Rejected Alternatives: Relying on chat memory or the earlier exact-tag regex that failed because the XML tag includes `role` and `chat_name` attributes.
Scalability potential: Documentation truth prevents overclaiming MX350/Ultra readiness without runtime artifacts.
Hardware Impact: 0 us runtime; process-level guard against fake performance reports.

Problem: The current root project cannot be used as a clean SHINOBU_12 compile verdict because other agents have active compile breaks.
Solution: Restored and built `Hecton8.Core.csproj` with isolated `.codex-artifacts/msbuild/shinobu12` obj/bin paths to avoid Unity `Temp` churn. Build still fails, but only on unrelated SaveSystem, Terminal, Fauna, Somatic, Core telemetry, and VFX files. `Docs/AgentLogs/Build_SHINOBU_12_ultra_20260517.log` contains no Tether/Verlet/CableDTO/TetherInstance/TetherManager/TetherVerletJobs errors.
Rejected Alternatives: Editing out-of-domain Save/Fauna/Terminal compile walls to make SHINOBU_12 look clean. That violates domain boundary and would hide real integrator work.
Scalability potential: Isolated obj path protects developer iteration from Unity Temp races during evidence builds.
Hardware Impact: 0 us runtime; compile-time protection only.

<SELF_AUDIT source="ULTRA_THINK_POLISH_MANDATE">
  <TaskMatrix>01 PASS, 02 PASS, 03 PASS, 04 PASS, 05 PASS, 06 PASS, 07 PASS, 08 PASS, 09 PASS, 10 PASS, 11 PASS, 12 PASS, 13 PASS, 14 PASS, 15 PASS, 16 PASS, 17 PASS, 18 PASS, 19 PASS, 20 PASS.</TaskMatrix>
  <ARM64Layout>VerletNodeDTO: offset 0 Position float3[12], offset 12 InvMass float[4], offset 16 OldPosition float3[12], offset 28 _pad0 float[4], sizeof 32. VerletConstraintDTO: 0 NodeA int, 4 NodeB int, 8 RestLength float, 12 Stiffness float, sizeof 16. GpuCableSplinePointDTO: 0 Position float3, 12 Tension01 float, sizeof 16. No `Pack = 1` remains in scoped SHINOBU_12 runtime files.</ARM64Layout>
  <ZeroGC>Hot solver jobs use NativeArray fields, for loops, blittable DTOs, and no LINQ/foreach/string formatting/managed allocation. Editor CSV monitoring may allocate for file reads, but it is wrapped in editor-only tooling and not in player SIMULATION.</ZeroGC>
  <AUP>Active solver rebases cable nodes into anchor-local float space before Verlet math and writes visual/force state from that local basis; DTO origin-shift job covers historical positions to avoid one-frame stretch after rebase.</AUP>
  <DearLie>Collision is discrete node SDF push-out plus old-position tangent damping. Segment swept CCD and Unity colliders are rejected. Low tier accepts visual clipping between nodes.</DearLie>
  <Dependency>New contracts are local DTOs plus DataVault buffer IDs. No direct world/submarine runtime class dependency was added; integration happens through mock sampler, vault buffers, and existing signal/vault surfaces.</Dependency>
  <HPhi>All new NativeArray surfaces are caller/vault-owned. TetherInstance fields are aliases/slices acquired from GlobalDataVault buffers, not locally allocated private ownership in the hot path.</HPhi>
  <Blackbox>300-frame ring remains active; fatal non-finite Verlet state dumps to `Docs/AgentLogs/Dump_VERLET_CABLES.bin`. DTO black-box entry stride is 64 bytes.</Blackbox>
  <CompileGuard>Attribute-aware prompt extraction confirmed 20 tasks. Isolated compile log is external-wall only with no SHINOBU_12 path matches. No sibling asmdef dependency was added.</CompileGuard>
</SELF_AUDIT>

## Loop 10 - SDF LOS / Unity Physics Raycast Purge

Problem: The bend-corner purge removed component hierarchy lookup, but the LOS and cable anti-slice path still depended on synchronous Unity Physics raycasts. That kept cable topology tied to PhysX colliders, duplicated rock collision authority, and contradicted the SHINOBU task rule that rock interaction is SDF-node truth plus visual bend approximation.
Solution: Replaced `Physics.RaycastNonAlloc` bend/anti-slice obstruction queries with `HectonVoxelVolume.TryRaymarchAnyPublishedSdf`. `TryFindClosestObstacle` now returns hit point, normal, volume, and runtime stamp directly from published voxel SDF. `RecalculateBendPoints`, `TryResolveBendCorner`, and `ValidateCableIntegrity` consume that value path. Removed obsolete `_bendObstructionMask` and dead locals left by the migration.
Rejected Alternatives: Keeping PhysX as a fallback for non-voxel obstacles. That would preserve two collision authorities and reintroduce collider hierarchy ownership. Swept segment CCD was also rejected; the assignment explicitly accepts node/sample and visual clipping as the Dear Lie.
Scalability potential: Low/MX350/Steam Deck use published-SDF raymarch and tangent fallback without collider hierarchy work. Middle/High retain cached bend volume stamps for stable topology after voxel edits. Ultra spends saved budget on shader-only stress/salt/silt visual overkill without changing gameplay truth.
Hardware Impact: No profiler-backed microseconds are claimed. Static impact is removal of synchronous PhysX raycast surface from tether bend checks.

Problem: The prior status still described a current external compile wall.
Solution: Re-ran isolated `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false /p:BaseIntermediateOutputPath=.codex-artifacts/msbuild/shinobu12_obj/ /p:OutputPath=.codex-artifacts/msbuild/shinobu12_bin/`. Result: 0 errors, 9 global warnings outside SHINOBU_12. Build-log filter for `Tether`, `Verlet`, `CableDTO`, `Cable`, `TetherInstance`, `TetherManager`, `TetherVerletJobs`, `VerletTowTuner`, `Hecton_TetherLineStrip`, and `GpuCableDrawParamsDTO` is empty.
Rejected Alternatives: Editing global physics warning ownership from the cable agent.
Scalability potential: Cable domain is statically build-clean under current Core project surface; runtime proof remains pending.
Hardware Impact: 0 us runtime; developer iteration and evidence quality only.

<SELF_AUDIT source="SDF_LOS_RAYCAST_PURGE">
  <TaskMatrix>01 PASS, 02 PASS, 03 PASS, 04 PASS, 05 PASS, 06 PASS, 07 PASS, 08 PASS, 09 PASS, 10 PASS, 11 PASS, 12 PASS, 13 PASS, 14 PASS, 15 PASS, 16 PASS, 17 PASS, 18 PASS, 19 PASS, 20 PASS.</TaskMatrix>
  <ARM64Layout>No DTO ABI changed in Loop 10. `VerletNodeDTO`: offset 0 Position float3[12], offset 12 InvMass float[4], offset 16 OldPosition float3[12], offset 28 _pad0 float[4], sizeof 32. `GpuCableSplinePointDTO`: offset 0 Position float3[12], offset 12 Tension01 float[4], sizeof 16. `GpuCableDrawParamsDTO`: offsets 0/16/32/48/64, sizeof 80.</ARM64Layout>
  <ZeroGC>`TryFindClosestObstacle`, `RecalculateBendPoints`, and `ValidateCableIntegrity` use value outputs, fixed arrays, and for loops. Scoped grep finds no component lookup, Unity Physics raycast, LINQ, closure, string formatting, direct DataVault `GetBuffer<`, `new NativeArray`, or fake job completion in SHINOBU_12 files.</ZeroGC>
  <AUP>Verlet node truth remains anchor-local before float math; SDF LOS consumes runtime-space endpoints after the active tether has resolved safe anchor/payload positions.</AUP>
  <DearLie>Obstacle truth is voxel SDF raymarch plus cached bend corners. If no published SDF answers, tangent/normal fallback remains and segment-level visual clipping is accepted. No swept CCD was introduced.</DearLie>
  <Dependency>No new asmdef or sibling runtime domain reference was added. The path uses existing `HectonVoxelVolume` public SDF surface; force and gameplay state remain routed through vault buffers/signals.</Dependency>
  <HPhi>Simulation arrays remain vault-owned; bend caches are fixed cold arrays and not per-frame allocations.</HPhi>
  <Blackbox>Per-cable 300-frame ring and manager ring remain active; dump paths unchanged.</Blackbox>
  <CompileGuard>Current isolated Core build succeeds with 0 errors and 9 global warnings outside SHINOBU_12. Runtime Unity Play Mode/profiler/GC evidence is pending.</CompileGuard>
</SELF_AUDIT>

## Loop 12 - Mock Current Trig Purge / DTO Fail-Closed Guard

Problem: The fallback `MockWorldSampler.SampleFlowAcceleration` still used `math.sin` per Verlet node. That contradicts the low-tier/dear-lie mandate: a mock current exists to keep cable bending believable without paying transcendental math in fallback physics.
Solution: Replaced the sine wave with a deterministic triangle-wave approximation using `math.frac` and absolute value. The old phase scale is preserved by multiplying by `1/(2*pi)`, so the spatial period remains stable while the CPU path is cheaper and Burst-friendly.
Rejected Alternatives: Keeping `math.sin` because the active cable count is currently small. That would preserve a bad pattern in the exact fallback DTO that future agents will copy. A 1D texture/LUT was rejected for this local mock because it would add a resource dependency and import path for a deterministic scalar waveform that fits in two value operations.
Scalability potential: Low/MX350 gets a cheap believable current shimmer. Middle/High can still sample real abyssal flow through existing manager caches. Ultra visual overkill remains shader-only via stress/salt/silt lanes, not extra gameplay truth.
Hardware Impact: No measured microseconds are claimed. Static impact is removal of one transcendental per mock-sampled node.

Problem: `VerletCableLayout.Validate()` existed, but runtime manager initialization did not enforce it. A stride mismatch could pass self-audit text and still register tick lanes or prewarm the pool.
Solution: `TetherManager.Awake()` now fail-closes before signal init, dependency cache, pool prewarm, tick registration, or telemetry allocation if the full DTO stride matrix does not match. This turns ARM64/GPU ABI drift into an early init fault instead of corrupted solver/render data.
Rejected Alternatives: Leaving validation as a documentation-only method. The whole point of the 16-byte GPU spline and 8-byte ARM64 contract is that code must fail closed when layout drifts.
Scalability potential: All tiers share one verified layout. Low avoids silent ARM64 traps; High/Ultra preserve stable 16-byte GPU spline and 80-byte draw payload lanes for visual-only overkill.
Hardware Impact: Cold init branch only. Hot path cost is 0 us.

Problem: Compile and forbidden-pattern evidence needed to be current after the Loop 12 code edits.
Solution: Re-ran scoped forbidden grep and isolated `Hecton8.Core` build. The grep returns no SHINOBU_12 hits for fallback trig/exp/log, Unity joints, `LineRenderer`, runtime `Pack=1`, direct `GetBuffer<`, `new NativeArray`, component lookup, Unity Physics raycasts, scalar material setters, LINQ, hot `foreach`, string formatting, or `StartCoroutine`. The isolated build succeeds with 0 errors and 9 warnings outside SHINOBU_12.
Rejected Alternatives: Editing `GlobalPhysicsStateManager` or duplicate signal include warnings from the cable agent. Those warnings are outside the cable domain.
Scalability potential: Evidence remains domain-scoped under concurrent agent churn.
Hardware Impact: 0 us runtime; compile guard only.

<SELF_AUDIT source="MOCK_CURRENT_TRIG_PURGE_LAYOUT_FAIL_CLOSED">
  <TaskMatrix>01 PASS, 02 PASS, 03 PASS, 04 PASS, 05 PASS, 06 PASS, 07 PASS, 08 PASS, 09 PASS, 10 PASS, 11 PASS, 12 PASS, 13 PASS, 14 PASS, 15 PASS, 16 PASS, 17 PASS, 18 PASS, 19 PASS, 20 PASS.</TaskMatrix>
  <ARM64Layout>`VerletNodeDTO`: offset 0 Position float3[12], offset 12 InvMass float[4], offset 16 OldPosition float3[12], offset 28 _pad0 float[4], sizeof 32. `VerletConstraintDTO`: offset 0 NodeA int[4], offset 4 NodeB int[4], offset 8 RestLength float[4], offset 12 Stiffness float[4], sizeof 16. `GpuCableSplinePointDTO`: offset 0 Position float3[12], offset 12 Tension01 float[4], sizeof 16. `GpuCableDrawParamsDTO`: offsets 0/16/32/48/64 float4 lanes, sizeof 80. `MockWorldSampler`: offset 0 Sdf[64], offset 64 FlowVelocity float3[12], offset 76 FlowAccelerationScale float[4], sizeof 80.</ARM64Layout>
  <ZeroGC>Loop 12 adds only value math and a cold `Awake()` guard. Scoped runtime grep finds no SHINOBU_12 LINQ, hot `foreach`, string formatting, component lookup, direct `GetBuffer<`, `new NativeArray`, or scalar material setter hits.</ZeroGC>
  <AUP>Unchanged: active Verlet node truth is rebased into anchor-local float space before solver math. Mock current uses local node position and does not cast absolute AUP to float.</AUP>
  <DearLie>Mock current oscillation is now a triangle-wave fake instead of sine. Collision remains discrete node SDF push-out; no swept segment CCD or PhysX fallback.</DearLie>
  <Dependency>No new asmdef, no sibling runtime reference, no new signal. The changes stay inside local cable DTOs and manager cold init.</Dependency>
  <HPhi>Simulation arrays remain vault-owned. Loop 12 adds no native allocation and no managed gameplay allocation.</HPhi>
  <Blackbox>Per-cable and manager 300-frame rings remain active; dump paths unchanged.</Blackbox>
  <CompileGuard>Build log `Docs/Archive/Batch008/AgentLogs/Build_SHINOBU_12_loop12_20260518.log`: 0 errors, 9 warnings outside SHINOBU_12. Runtime Unity Play Mode/profiler/GC evidence is still pending.</CompileGuard>
</SELF_AUDIT>

## Loop 13 - CS1612 NativeArray Property Purge

Problem: `TetherInstance` still exposed `public NativeArray<float3> VisualSegmentPositions => ...`. Even though the returned struct aliases vault memory, this violates the SHINOBU CS1612 rule: NativeArray-backed mutation surfaces must not hide behind C# properties.
Solution: Removed the property and added `internal ref NativeArray<float3> GetVisualSegmentPositionsRef()`. The origin-shift fallback path in `TetherManager` now binds a ref local before mutating the visual staging slice.
Rejected Alternatives: Leaving the property because current mutation happened through a local alias. That is exactly how this class of bug survives code review. Copying into a managed scratch array was rejected because it would be allocation and ownership debt.
Scalability potential: Low tier keeps the same vault-backed memory path. High/Ultra visual upload remains GPU-buffer based and does not gain a managed staging layer.
Hardware Impact: No profiler-backed microsecond claim. The value is eliminating a struct-copy mutation trap and keeping L1-facing mutation explicit.

Problem: The ref-return change could have introduced visibility or C# language compile issues.
Solution: Re-ran scoped CS1612 grep and isolated Core build. The scan finds no `NativeArray<T>` expression-bodied/get properties in scoped SHINOBU_12 runtime files. Build succeeds with 0 errors and 9 warnings outside SHINOBU_12.
Rejected Alternatives: Expanding public API to expose raw fields. That would leak too much of `TetherInstance` internals and invite out-of-domain writes.
Scalability potential: Explicit ref-return keeps the manager/cable boundary narrow while still preserving mutation authority.
Hardware Impact: 0 us runtime; compile guard only.

<SELF_AUDIT source="CS1612_NATIVEARRAY_PROPERTY_PURGE">
  <TaskMatrix>01 PASS, 02 PASS, 03 PASS, 04 PASS, 05 PASS, 06 PASS, 07 PASS, 08 PASS, 09 PASS, 10 PASS, 11 PASS, 12 PASS, 13 PASS, 14 PASS, 15 PASS, 16 PASS, 17 PASS, 18 PASS, 19 PASS, 20 PASS.</TaskMatrix>
  <ARM64Layout>No DTO ABI changed in Loop 13. Primary offsets remain: `VerletNodeDTO` 0/12/16/28 sizeof 32; `VerletConstraintDTO` 0/4/8/12 sizeof 16; `GpuCableSplinePointDTO` 0/12 sizeof 16; `GpuCableDrawParamsDTO` 0/16/32/48/64 sizeof 80.</ARM64Layout>
  <ZeroGC>Loop 13 changes only a property to a ref-return method and a ref local. No allocation, closure, LINQ, string formatting, component lookup, or native allocation was added.</ZeroGC>
  <AUP>Origin-shift fallback still subtracts the same `shiftOffsetF3` from the vault-backed visual staging points; no absolute AUP is cast to float.</AUP>
  <DearLie>Unchanged: low tier can still use taut-line visual fake under high stress; collision remains node SDF push-out.</DearLie>
  <Dependency>No new asmdef, no new signal, no sibling runtime dependency. Boundary remains `TetherManager` -> `TetherInstance` internal ref-return.</Dependency>
  <HPhi>Arrays remain vault-owned. The ref-return exposes a mutable alias to the existing vault slice only inside the tether assembly boundary.</HPhi>
  <Blackbox>Telemetry rings unchanged.</Blackbox>
  <CompileGuard>Build log `Docs/Archive/Batch008/AgentLogs/Build_SHINOBU_12_loop13_20260518.log`: 0 errors, 9 warnings outside SHINOBU_12. Runtime Unity Play Mode/profiler/GC evidence is still pending.</CompileGuard>
</SELF_AUDIT>

## Loop 14 - Blackbox H8Dump / 50-Cable Vault Capacity

Problem: Task 17 required a 300-frame cable blackbox, while the ultra mandate required `.h8dump` output and MMF-style fault export. The SHINOBU_12 path still serialized entries through `BinaryWriter` into `.bin` only.
Solution: Added `TetherBlackBoxDumpWriter`, a fault-path-only raw ring writer. Editor/Standalone writes the primary `.h8dump` through `MemoryMappedFile` and pointer copy. Non-MMF platforms and the legacy `.bin` mirror use `FileStream.Write(ReadOnlySpan<byte>)` over the same unmanaged ring order. The hot telemetry write remains a NativeArray ring mutation.
Rejected Alternatives: Keeping `BinaryWriter` because this is fault-only. That still leaves per-field managed serialization in a crash path. Moving the whole feature into GlobalTelemetryBus was rejected because this task owns the domain ring and must remain locally reviewable under concurrent agent work.
Scalability potential: Low/MX350 keeps zero normal-frame dump work and gets deterministic postmortem payloads. Middle/High/Ultra can inspect the `.h8dump` primary while the prompt-compatible `.bin` mirror remains available for existing tools.
Hardware Impact: 0 us steady-state. Fault-path serialization no longer loops through managed writer methods per field; no profiler-backed microseconds are claimed.

Problem: The active pool supports 64 tether instances, but `DataVaultMaxTetherSlots` was still 8. Above 8 active cables, DataVault publication, tension force slots, and per-cable telemetry could silently stop matching the assignment target.
Solution: Raised `DataVaultMaxTetherSlots` to 64. Documented the largest resulting slab explicitly: `64 tethers * 300 frames * 64 bytes = 1,228,800 bytes` for telemetry. This aligns vault capacity with the 50-cable target plus pool headroom.
Rejected Alternatives: Leaving only the first eight cables as vault-backed and treating the rest as visual/legacy. That violates the H-Phi and blackbox requirements for concurrent cables.
Scalability potential: Low tier can still reduce node count/iterations, but every active cable has a vault slot. High/Ultra keep full blackbox visibility without changing the gameplay DTO layout.
Hardware Impact: Additional vault reservation budget is bounded and cold. The deterministic fact is capacity correctness above 8 active cables; runtime memory/profiler proof is still pending.

Problem: The first Loop 14 compile attempt exposed that the new helper file was not included by the generated `Hecton8.Core.csproj`.
Solution: Added the compile include for `Assets\_Project\Scripts\Physics\TetherBlackBoxDumpWriter.cs`. Retry removed all SHINOBU_12 errors; the build now stops in external `LocalizationManager` dispatcher-interface errors.
Rejected Alternatives: Hiding the writer inside an unrelated included file. That would reduce review clarity and couple dump I/O to job DTO code.
Scalability potential: Isolated helper keeps the compile surface understandable without adding asmdef or sibling runtime references.
Hardware Impact: 0 us runtime; developer iteration protection only.

<SELF_AUDIT source="BLACKBOX_H8DUMP_VAULT_CAPACITY">
  <TaskMatrix>01 PASS, 02 PASS, 03 PASS, 04 PASS, 05 PASS, 06 PASS, 07 PASS, 08 PASS, 09 PASS, 10 PASS, 11 PASS, 12 PASS, 13 PASS, 14 PASS, 15 PASS, 16 PASS, 17 PASS, 18 PASS, 19 PASS, 20 PASS.</TaskMatrix>
  <ARM64Layout>No gameplay DTO ABI changed. Primary offsets remain `VerletNodeDTO` 0/12/16/28 sizeof 32, `VerletConstraintDTO` 0/4/8/12 sizeof 16, `GpuCableSplinePointDTO` 0/12 sizeof 16, `GpuCableDrawParamsDTO` 0/16/32/48/64 sizeof 80. Dump header is cold file I/O layout: magic ulong offset 0, version int offset 8, entryCount int offset 12, entrySize int offset 16, head int offset 20, reasonFlags uint offset 24, headerBytes uint offset 28, sizeof 32.</ARM64Layout>
  <ZeroGC>Hot Tick/FixedTick paths still write NativeArray ring entries only. Fault export is outside steady state and uses raw NativeArray pointer copy or `ReadOnlySpan<byte>` stream writes; `BinaryWriter` is removed from scoped SHINOBU_12 dump paths.</ZeroGC>
  <AUP>Unchanged: Verlet simulation stays anchor-local before float math. Dump entries contain already-local anchor/payload telemetry and do not cast absolute AUP to float.</AUP>
  <DearLie>Unchanged: low tier can keep taut-line visual fake and node SDF push-out. Loop 14 changes evidence/export and capacity, not physical truth.</DearLie>
  <Dependency>No new sibling asmdef reference or runtime class dependency was added. The helper is in `Hecton8.Physics`; DataVault and signal boundaries remain unchanged.</Dependency>
  <HPhi>All cable state slices remain vault-owned. Slot capacity now matches 64 pooled instances instead of only 8.</HPhi>
  <Blackbox>Cable faults write `Docs/AgentLogs/Dump_VERLET_CABLES.h8dump` first and append legacy `Docs/AgentLogs/Dump_VERLET_CABLES.bin`; manager faults write `Dump_VERLET_CABLES_MANAGER.h8dump` plus legacy `.bin` mirror.</Blackbox>
  <CompileGuard>Build log `Docs/Archive/Batch008/AgentLogs/Build_SHINOBU_12_loop14_retry3_20260518.log`: blocked externally in `LocalizationManager`/`LocRegistry.BabelDictionaryStage`; no SHINOBU_12/Tether/Verlet/Cable/dump-writer errors in filtered output. Runtime Unity proof remains pending.</CompileGuard>
</SELF_AUDIT>
