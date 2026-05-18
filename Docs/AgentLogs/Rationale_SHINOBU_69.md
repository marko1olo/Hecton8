# Rationale_SHINOBU_69

Agent: SHINOBU_69
Domain: Volumetric Plasma / Beam VFX
Status: ACTIVE - BLOCKED BY EXISTING CORE COMPILE ERRORS; UNITY/BURST IMPORT OF NEW PLASMABEAM FILES PENDING

## Decision 00 - Duplicate Prompt And Stale State Isolation

Problem: `CURRENT_BATCH.md` contains duplicate `SHINOBU_69` prompts, and the active status/rationale files reverted to SaveSystem WAL content while the user explicitly assigned `SHINOBU_VOLUMETRIC_PLASMA_BEAM`.

Solution: Bind this run to the second `SHINOBU_69` block at `CURRENT_BATCH.md:2736`, role `VOLUMETRIC_PLASMA_AND_BEAM_DIRECTOR`. Archive stale SaveSystem files under `Docs/Archive/Batch009_Reentry_SHINOBU_69_SaveSystem_StaleAfterVfx`.

Rejected Alternatives: Executing both domains was rejected as cross-domain sabotage. Reusing SaveSystem reports was rejected because it makes CTO audit data false.

Scalability potential: Low/MX350 uses triangle tubes, minimal length segments, and no noise. Middle restores moderate segments. High/Ultra use 8 radial, 20 length segments, shader overdrive, and deterministic crackle.

Hardware Impact: Hygiene has no frame-time effect. It prevents wrong-domain compile edits and false completion reports.

## Decision 01 - Independent VFX Runtime Instead Of ToolKinematics Mutation

Problem: Existing `ToolKinematics` has a 32B `ToolBeamVertexDTO`, but its layout is `Position/Radius/Normal/U`, not the requested `Position/ColorPacked/UV/_pad0`, and it runs in tool ownership phases.

Solution: Create a separate `Hecton8.VFX.PlasmaBeam` runtime with vault-owned `BeamStateDTO`, `BeamVertexDTO`, trigonometry LUT, indirect args, mock signals, acoustic taps, and telemetry. Tool integration remains future-safe through vault state/signal DTOs.

Rejected Alternatives: Editing ToolKinematics was rejected because it would violate Agent 22 ownership and force unrelated compile walls. Third-party VolumetricLightBeam and legacy LineRenderer paths were rejected because they generate meshes/components outside the batching contract.

Scalability potential: Runtime state accepts real tool inputs later without changing rendering. Low/Middle/High/Ultra all use the same indirect draw contract.

Hardware Impact: Avoids per-tool renderer state churn and managed mesh rebuilds. Expected low-end gain is avoiding SetPass/batcher breakage; exact microseconds require Unity Frame Debugger/Profiler.

## Decision 02 - Vault Buffer IDs In Core Enum

Problem: Vault law requires persistent `NativeArray` storage to be requested from `GlobalDataVault`, but the VFX beam buffers had no stable `BufferID`s.

Solution: Add `ShinobuPlasmaBeam*` IDs `71120..71128` to `H8Memory.cs`. This is the only core edit and is required for buffer sovereignty.

Rejected Alternatives: Private persistent `NativeArray` fields were rejected because they fragment allocator ownership. Casting ad hoc `BufferID` constants inside the runtime was rejected because hidden IDs are harder for Integrator/MemorySentinel to audit.

Scalability potential: Low can lower capacities later through vault budgeting; Ultra can increase visual payloads while preserving a single memory authority.

Hardware Impact: Normal frame impact is zero. It prevents allocator churn and supports compaction fences on low-memory devices.

## Decision 03 - Dear Lie Procedural Tube

Problem: Real plasma particles or volumetric raymarching would waste CPU/GPU budget for a standard tool beam and introduce extra renderers.

Solution: Generate a scrolling tube mesh in Burst, upload a 32B vertex stream to `GraphicsBuffer`, and draw with `Graphics.DrawProceduralIndirect`. The shader fakes energy motion from UV scrolling and procedural band/spark functions.

Rejected Alternatives: `LineRenderer`, `TrailRenderer`, `ParticleSystem`, `new Mesh()`, third-party volumetric beam package, and complex raymarching were rejected as batching-breaking or overbuilt.

Scalability potential: Low collapses to 2 length segments x 3 radial segments and zero noise. Middle ramps segments. High/Ultra spend saved CPU on noise curvature and shader intensity.

Hardware Impact: Complexity changes from per-renderer CPU component rebuild to O(activeBeams * segments * radial) Burst math plus one indirect draw. Estimated low path is 20 beams * 2 * 3 * 6 = 720 vertices; ultra is 19,200 vertices.

## Decision 04 - ARM64 Layout And AUP Local Math

Problem: Beam vertices must be SIMD/GPU-upload friendly, and 100km AUP coordinates cannot be cast directly to float for trigonometry.

Solution: Use explicit layouts: `BeamVertexDTO` 32B, `BeamStateDTO` 128B, `BeamTrigLutEntry` 8B, `PlasmaBeamRuntimeScalarsDTO` 64B, `AcousticEchoTap` 32B, and `PlasmaBeamTelemetryEntry` 64B. The mesh job subtracts `CameraAup`/`ToolAup` first, then casts local deltas to `float3`.

Rejected Alternatives: `Pack=1`, managed arrays, and absolute `double3` trig were rejected. `float3` absolute world coordinates were rejected because they jitter at 50km.

Scalability potential: Identical layout across all quality weights; only segment/noise math changes.

Hardware Impact: 32B vertices align for GPU upload; 64B telemetry/scalar records are cache-line friendly. Low-end impact is avoiding unaligned reads and defensive struct copies.

## Decision 05 - Human Control And Forensics

Problem: Designers need tuning without recompilation and QA needs a 300-frame blackbox when beam math produces NaN.

Solution: Add `Plasma Beam Tuner` EditorWindow, `beam_visuals.csv` byte parser, scene wireframe `OnDrawGizmos`, and `Dump_LASER_SURGEON.bin` fault dump.

Rejected Alternatives: ScriptableObject-only tuning was rejected because it requires asset reload/import. Managed string parsing was rejected for runtime/dev hot reload. Shader-only debugging was rejected because it hides bad tube topology.

Scalability potential: CSV/editor can force or release radial segment overrides; `GlobalQualityWeight` remains the runtime authority when override is zero.

Hardware Impact: Editor/CSV paths are cold/dev only. Runtime telemetry costs one 64B ring write per frame.

## Decision 06 - Build Guard

Problem: The implementation adds new Unity C# and should be compiled, but the project rule forbids launching `dotnet build` when CPU load is above 50% or another compiler is running. The generated `Hecton8.Core.csproj` also had not yet imported the new `Assets/_Project/Scripts/VFX/PlasmaBeam` files.

Solution: Run static gates first. When CPU dropped below 50% and no `dotnet`/`csc` process existed, launch a single constrained `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies /p:UseSharedCompilation=false /nr:false /m:1 -v:q /clp:ErrorsOnly`.

Rejected Alternatives: Launching a build at 100% CPU was rejected because it violates the explicit hardware protection rule. Claiming compile success from static scans was rejected because Unity/Burst import has not run. Editing generated csproj files was rejected because Unity overwrites them and it would not be a durable domain fix.

Scalability potential: No runtime tier effect. This preserves developer hardware and iteration stability.

Hardware Impact: Build failed after 42.5s on 6 pre-existing non-VFX errors: `math.reversebytes`, `sanitizedWeight`, `IndustrialLoreBitMask`, and `HectonDrsRenderFeatureGate`. No frame-time effect.

## Decision 07 - Ultra-Polish Sector Seed And Thermal Collapse

Problem: The first VFX pass used a smooth quality curve for length segments. At `GlobalQualityWeight=0.1`, rounding could still emit 3 length segments, which violates the thermal-collapse intent. Mock RNG also lacked an explicit sector component.

Solution: Add `SectorHash` to `PlasmaBeamRuntimeScalarsDTO` at offset 56, keeping the struct exactly 64 bytes. Mock RNG seed is now `SectorHash ^ SystemHash ^ FrameMix ^ LASER`. Length density uses `math.step(0.30, q)` multiplied by a smooth polynomial, so q<0.3 stays exactly at 2 length segments. Simplex crackle uses the same `math.step` gate before evaluating `noise.snoise`.

Rejected Alternatives: A binary low-end hardware switch was rejected because quality must be a continuous float. Leaving the smooth-only curve was rejected because it wastes geometry under thermal throttling. Using `UnityEngine.Random` was rejected for rollback determinism.

Scalability potential: Low uses 2x3 tube cells with zero Simplex calls. Middle gradually restores length density after q crosses 0.3. High/Ultra keep 20x8 cells and per-vertex crackle.

Hardware Impact: Thermal low path saves 120 vertices per beam compared with the prior q=0.1 rounded curve and avoids one Simplex evaluation per vertex. Ultra path cost is unchanged.

## Decision 08 - Phase Adapter Devirtualization

Problem: The first pass used an abstract phase adapter base with virtual overrides. The project dispatcher still routes through `IDispatcherSystem`, but the domain added an unnecessary local virtual chain before that interface boundary.

Solution: Replace the abstract base with four sealed phase adapter classes that implement `IDispatcherSystem` directly and forward only their owned phase method to the runtime.

Rejected Alternatives: Registering one all-phase object was rejected because the dispatcher phase contract expects a single declared `DispatcherPhase` per registered system. Leaving abstract/virtual adapters was rejected because it adds avoidable IL2CPP dispatch surface.

Scalability potential: No visual-tier effect. It preserves the compile wall and dispatcher contract while reducing local polymorphism.

Hardware Impact: Sub-microsecond dispatch hygiene. Main performance value is architectural: less virtual surface in a phase called every frame.

## Decision 09 - VisualSync Allocation Firewall And Shader-Time Rebind

Problem: The boot resource path was also callable from `VisualSyncTick`, which meant an invalidated GPU buffer or material could trigger `new GraphicsBuffer`, `Shader.Find`, or `new Material` during gameplay. The shader also used Unity `_Time.y`, while CPU beam crackle used dispatcher frame-derived time.

Solution: Split resource validation into `EnsureGraphicsResources(allowAllocation)`. Boot calls it with allocation enabled; VisualSync calls it with allocation disabled and skips draw if resources are not already resident. Add `_H8PlasmaFrameTime`, driven by `context.Frame * ResolveSimulationTickDelta(timing)`, and bind it to the material so shader scroll and CPU Simplex phase share deterministic frame time.

Rejected Alternatives: Lazy-recreating GPU resources in VisualSync was rejected because it can produce a visible hitch. Using Unity `_Time` was rejected because it divorces shader flow from deterministic simulation frame progression. Broad `where T : struct` upload constraints were rejected in favor of `where T : unmanaged`.

Scalability potential: Low/Middle/High/Ultra visual tiers are unchanged. The timing path now scales deterministically across all tiers instead of depending on render clock drift.

Hardware Impact: No measured frame-time saving. Prevents a worst-case multi-ms resource recreation hitch on Quest-class hardware and removes one CPU/GPU visual desync vector.
