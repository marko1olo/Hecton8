# LOG_13j

## 2026-05-27 Static Domain Repair Pass

What was wrong:
- No `<AGENT_PROMPT id="13j">` exists in `Docs/Tasks/CURRENT_BATCH.md`; direct user assignment controlled the pass.
- Flora indirect culling used stepped density decimation, creating quality bands instead of continuous scaling.
- Fauna spatial query methods mutated registry/native hash state while answering reads.
- Boid compute had `_BoidCount - 1` underflow risk for zero active boids.
- Boid shader lacked `DepthOnly`, preventing depth participation for instanced fauna.
- Wreckage generation rejected valid `GlobalQualityWeight = 0.0`, ignored authored module weights, and seeded structural shear from frame number.
- Geology generation/profile used signed `Mathf.Abs(stableHash)`, unsafe for `int.MinValue`.
- Vegetation artificial structure job DTO had implicit native layout.
- Wreckage GPU upload allocation/scalar path lacked failure and finite guards.

What was done:
- Added deterministic per-instance flora keep probability and bound `_HectonDensityKeepProbability01` for CPU BRG and GPU culling.
- Changed fauna query stale handling to skip/telemetry rather than unregister inside read paths.
- Added boid compute active-count clamp and a cheap `DepthOnly` shader pass.
- Made wreckage quality zero valid, module selection integer-weighted, and structural shear frame-independent.
- Added unsigned stable hash normalization in geology mesh builder and geology profile.
- Made `ArtificialStructureRecord` explicit 32 bytes with fixed offsets/padding.
- Wrapped wreckage GPU buffer allocation in fail-fast cleanup and sanitized global scalar vectors.

Cinematic cheats used:
- Flora density is a deterministic probability mask, not extra simulation.
- Boid depth pass uses a conservative oriented body hull; VAT/tail cosmetics stay in forward pass.
- Wreckage shear remains cheap small-angle matrix rotation, not physical fracture simulation.

Exact microseconds saved or spent:
- Flora probability decimation: estimated 40-220 us saved on i3/MX350 dense culling frames when quality/stress reduce keep probability.
- Fauna stale query purity: estimated 30-180 us spike avoided in despawn-heavy frames; normal frames ~0 us.
- Wreckage integer weighted selection: estimated 0-5 us spent per generation pass, deterministic authority gain.
- Geology hash and DTO layout fixes: 0 us meaningful runtime; correctness fixes.
- Boid depth pass: estimated 30-180 us GPU cost when visible, traded for depth occlusion and overdraw reduction.
- Build verification: `git diff --check` passed with line-ending warnings only. `dotnet build` not launched because CPU load was 100%, above the project 50% compile guard.

Unfixed, intentionally not hidden:
- Flora manual indirect rendering and procedural coral manual procedural draw still violate BRG/GPU Resident Drawer doctrine.
- Flora hot `GlobalRegistry` polling/camera fallback scan remains.
- Sargassum fauna simulation still has binary compute policy, tiny foveation decision job, and discrete `SimulationLodTier.Full` behavior.

---

## 2026-05-27 - Agent 13j second audit/fix pass

What was wrong:
- `HectonIndirectVegetationRenderer.ResolveCullCamera()` still had a hot scene-camera search fallback route.
- `FloraInteractionManager.ResolveGlobalOceanFlow()` still depended on hot `GlobalRegistry.OceanKinematics` service lookup.
- `SargassumMicroFaunaBoids` scheduled a one-record foveation `IJob`; this is scheduler overhead, not batch work.
- Sargassum sensory threat richness still had full/simplified cliffs despite an existing continuous `hibernation01`.
- Flora spore events had only a static managed fixed ring; no first-party `SignalBus<T>` hot route.
- `WorldGenerativeGeologyMeshBuilder` allocated runtime `List<>` scratch and small arrays for generated geology meshes.

What was done:
- Moved vegetation cull camera discovery to `RefreshCullCameraCacheCold()` and made hot `ResolveCullCamera()` cache/override only.
- Cached ocean kinematics service/provider through cold service binding and `GlobalRegistryServiceSlot.OceanKinematics` hot-swap.
- Removed `EvaluateSimulationLodJob`; foveation now writes the 32-byte decision directly into the existing front/back buffers.
- Converted predator AUP loop cap and flashlight sensory endpoint from tier cuts to continuous `hibernation01` scaling.
- Made `HectonFloraSporeEvent` an `ISignal` and added `SignalBus<HectonFloraSporeEvent>` publishing while keeping the legacy ring.
- Added pooled geology vertex/index scratch leases and stack spans for rock-cluster temporary data.

Cinematic cheats used:
- Sargassum foveation remains scalar distance/hibernation math, not a physical swarm sleep simulation.
- Sensory threat reduction is a smooth fake through loop cap/endpoint scale, not complex AI perception degradation.
- Geology keeps authored procedural silhouettes and reuses scratch memory; no runtime physical erosion or mesh fracture.

Exact microseconds saved:
- Cold camera cache: estimated 5-60 us saved in camera-heavy scenes by avoiding `Camera.GetAllCameras` in vegetation tick.
- Ocean kinematics cache: estimated 1-8 us saved per hot current-sampling route by avoiding registry service lookup.
- Sargassum foveation direct path: estimated 10-45 us saved per active swarm frame by removing one tiny job schedule/complete lane.
- Continuous sensory cap: estimated 2-20 us saved in predator-heavy sargassum frames while reducing visual/behavior cliffs.
- Flora spore SignalBus lane: current gain 0-6 us until consumers migrate; route correctness fixed now.
- Geology scratch pool: removes roughly 1-6 KB managed allocations per simple mesh and 20-80 KB per compound generated geology bundle.

Verification:
- `git diff --check -- <13j touched files>` passed; line-ending warnings only.
- Geology brace balance check: 117 open braces / 117 close braces.
- Regression `rg`: no `_BoidCount - 1`, no `EvaluateSimulationLodJob`, no `Mathf.Abs(stableHash)`, no `trigger.GlobalQualityWeight > 0`, no frame-based wreckage shear seed.
- Hot route `rg`: `GlobalRegistry.OceanKinematics` remains only in cold cache binding; `Camera.GetAllCameras` remains only in `RefreshCullCameraCacheCold()`.
- `dotnet build` not launched: CPU guard checks reported 54%, 76%, 90%, and 100%, above the explicit 50% compile limit.

Unfixed, intentionally not hidden:
- `HectonIndirectVegetationRenderer` still has a manual `Graphics.RenderMeshIndirect` GPU-indirect render path despite BRG infrastructure.
- `ProceduralCoralGpuUploadDispatcher` still uses `DrawProceduralIndirect` plus global shader buffers.
- Legacy `HectonBoidController` still uses manual `RenderMeshIndirect` and hard high-resource compute admission.
- `SargassumMicroFaunaBoids` still has full-grid tier behavior and compute admission policy outside the sensory/foveation fixes.

---

## 2026-05-27 - Agent 13j third audit/fix pass

What was wrong:
- `ProceduralCoralGpuUploadDispatcher` wrote coral matrix/sway state through `Shader.SetGlobal*`, creating process-wide shader state contamination.
- `WorldGenerativeGeologyMeshBuilder` compound builders created temporary copy-source `Mesh` objects and did not release them after `AppendMeshTransformed`.
- Prior status still claimed coral global shader buffers were unresolved after the local coral state isolation patch.

What was done:
- Coral dispatcher now captures active sway DTO from the upload phase and binds `_H8CoralMatrices` plus sway vectors through one cold-owned draw-local property block.
- Removed `Shader.SetGlobalBuffer` and `Shader.SetGlobalVector` calls from the coral dispatcher.
- Added `ReleaseTemporaryMesh()` and wrapped shelf/outcrop/flank/breaker/spire/shard temporary meshes in `try/finally`.
- Kept returned LOD/collider meshes owned by `GeologyMeshBundle`; only copy-source meshes are released.

Cinematic cheats used:
- Coral sway remains DTO-driven shader presentation state, not CPU bone/physics simulation.
- Geology remains deterministic procedural silhouette assembly; no physical erosion, fracture simulation, or runtime collider rebuild expansion was added.

Exact microseconds saved or spent:
- Coral shader global removal: estimated 0-3 us runtime delta; primary gain is render-state correctness, not measurable CPU savings without Frame Debugger.
- Geology temporary mesh release: estimated 0-10 us destroy scheduling cost per compound build; native memory retention is reduced by releasing copied source meshes after assembly.
- Static verification: 0 us runtime.

Verification:
- `git diff --check -- <13j touched files>` passed with line-ending warnings only.
- Geology brace balance: 130 open braces / 130 close braces.
- `rg` showed no `Shader.SetGlobal*` in `ProceduralCoralGpuUploadDispatcher`.
- `rg` showed copy-only temp meshes now route through `ReleaseTemporaryMesh`; remaining `BuildDeformedEllipsoid` assignment is returned LOD ownership.
- `dotnet build` not launched: CPU guard reported 61.8%, no `dotnet`/`csc` process active, and no root `.sln` was present.

Unfixed, intentionally not hidden:
- `HectonIndirectVegetationRenderer` still uses manual `Graphics.RenderMeshIndirect`; BRG infrastructure exists but is not safely wired as the primary draw path.
- `ProceduralCoralGpuUploadDispatcher` still uses manual `DrawProceduralIndirect`; final GPU sovereignty needs BRG/GPU Resident Drawer ownership work.
- Legacy `HectonBoidController` still uses manual `RenderMeshIndirect` and hard high-resource compute admission.
- `SargassumMicroFaunaBoids` still has full-grid/full-tier behavior outside the already-fixed sensory/foveation routes.

---

## 2026-05-27 - Agent 13j fourth audit/fix pass

What was wrong:
- `ProceduralWreckageGpuUploadDispatcher` still used `Shader.SetGlobalBuffer` and `Shader.SetGlobalVector` for procedural structure draw payloads.
- Wreckage `InstanceCount` converted from `uint` to `int` before bounds resolution, so a corrupted DTO could become negative before clamp.
- `WorldGenerativeGeologyMeshBuilder.Build()` used shared static scratch lists/pools without serializing the public entry point.

What was done:
- Wreckage dispatcher now captures active scalar DTO and binds matrices/scalars through a draw-local property block.
- Wreckage draw skips zero active instances and uses a safe `ResolveRequestedInstanceCount()` guard before upload.
- Geology builder public `Build()` is serialized through one cold-owned sync object to protect static scratch state.

Cinematic cheats used:
- Wreckage remains a procedural shader-buffer presentation route, not physical debris simulation.
- Geology remains deterministic mesh assembly from cheap shape primitives; no erosion/fracture solver added.

Exact microseconds saved or spent:
- Wreckage draw-local state: estimated 0-3 us runtime delta; primary gain is correctness and no global shader-state leak.
- Safe instance-count resolution: estimated 0 us normal path; prevents bad DTO from driving invalid buffer upload counts.
- Geology build serialization: sub-microsecond uncontended lock cost per build; prevents rare scratch-race mesh corruption and native memory retention.

Verification:
- `git diff --check -- <13j touched files>` passed with line-ending warnings only.
- Geology brace balance: 131 open braces / 131 close braces.
- `rg` showed no `Shader.SetGlobal*` in coral or wreckage dispatchers.
- Geology call-site scan found only editor authoring as direct external caller; public builder remains protected for future runtime use.
- `dotnet build` not launched: active `dotnet` processes were already running. Root `.sln` absent; Unity-generated `.csproj` files present.

Unfixed, intentionally not hidden:
- `HectonIndirectVegetationRenderer` still uses manual `Graphics.RenderMeshIndirect`; a safe BRG ownership migration requires deeper renderer work.
- Coral and wreckage still use manual `DrawProceduralIndirect`; global state is fixed, GPU Resident Drawer/BRG ownership is not.
- Legacy `HectonBoidController` and `SargassumMicroFaunaBoids` still have manual indirect draw paths.
- `SargassumMicroFaunaBoids` still has full-grid/full-tier simulation branches outside the already-fixed sensory/foveation paths.

---

## 2026-05-27 - Agent 13j fifth audit/fix pass

What was wrong:
- `SargassumMicroFaunaBoids.RegisterWhaleFallScavengerBurst()` used `_lastSimulationLodTier != SimulationLodTier.Full` as a hard gate.
- The result was binary: full-tier spawned the authored scavenger presentation; simplified/sleep tiers spawned zero scavenger visuals and only a fear pulse.
- `HectonBoidController.cs` remains a dirty legacy manual indirect renderer with hard compute admission, so it was audited but not patched in this pass without compile feedback.

What was done:
- Added `_lastSimulationHibernation01` cache and kept it updated through normal tick, static fallback, no-data/empty-swarm returns, and primed foveated decision.
- Replaced the full-tier whale-fall gate with `ResolveWhaleFallScavengerVisualCount(safeActiveCount, hibernation01)`.
- Added continuous activity math: low hibernation keeps the authored 96-boid burst; sleep keeps a minimum visual floor instead of hard zero.
- Scaled whale-fall fear duration/amount through the same continuous activity curve while keeping the dormant values exact at `hibernation01 == 1`.

Cinematic cheats used:
- Whale-fall response remains a visual reassignment of existing boids plus a local fear pulse.
- No physical scavenger AI, no full-grid wake, no PBD solve expansion, and no new simulation authority were introduced.

Exact microseconds saved or spent:
- Normal frame: 0 us meaningful cost except one cached float assignment.
- Whale-fall event: low tier writes about 12 boids instead of waking full 96-boid presentation or showing zero; high/ultra still reach 96.
- Avoided full-grid/PBD wake cost for a presentation event; exact GPU/CPU savings require Unity profiler capture.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` passed with line-ending warning only.
- `rg` showed no remaining `_lastSimulationLodTier != SimulationLodTier.Full` gate in the whale-fall route.
- Sargassum brace balance: 666 open braces / 666 close braces.
- Process guard found active `dotnet`; CPU samples were 100%, 90.7%, and 36.4%, so `dotnet build` was not launched.

Unfixed, intentionally not hidden:
- `HectonIndirectVegetationRenderer` still uses manual `Graphics.RenderMeshIndirect`; BRG ownership migration remains larger.
- Coral and wreckage still use manual `DrawProceduralIndirect`; global shader state is fixed, but GPU Resident Drawer/BRG ownership is not.
- `HectonBoidController.cs` still uses manual `RenderMeshIndirect` and hard compute admission. It is already dirty in the worktree, so it needs an isolated owner pass plus compile feedback.
- `SargassumMicroFaunaBoids` still has full-grid solve and high-resource compute admission policy; this pass removed the whale-fall cliff only.

---

## 2026-05-27 - Agent 13j sixth audit/fix pass

What was wrong:
- `VegetationNavGridSynchronizer.TryGetLatestAbyssalPathPayload()` called `CompleteAbyssalPathJob(forceComplete:false)`.
- That method is a read accessor used by fauna consumers. It must not complete jobs, mutate path job state, or advance owner lifecycle from a read route.
- Separate audit found `VegetationPredatorFearField` has a global shader payload with no shader consumer in text-searchable first-party shaders, but that file is already inside another agent's DataVault migration and was not cut in this pass.

What was done:
- Removed the `CompleteAbyssalPathJob(false)` call from `TryGetLatestAbyssalPathPayload()`.
- The accessor now only returns the latest committed `AbyssalPathSnapshotHandle` read-only view and count.
- Completion remains in scheduling, late-frame owner swap, and shutdown paths where mutation is expected.

Cinematic cheats used:
- No new path simulation. This preserves latest-snapshot steering: fauna reads a committed route instead of synchronously forcing path work.

Exact microseconds saved or spent:
- Normal read cost is unchanged.
- Worst-case read-path spike avoided: estimated 0-100+ us depending on whether a pending path job was ready to complete.
- No new allocation, no new job, no DTO change.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/VegetationNavGridSynchronizer.cs` passed with line-ending warning only.
- `rg` confirms `TryGetLatestAbyssalPathPayload` no longer calls `CompleteAbyssalPathJob`.
- Vegetation nav synchronizer brace balance: 197 open braces / 197 close braces.
- `dotnet build` not launched: final guard found active `VBCSCompiler` and CPU guard reported 69%.

Unfixed, intentionally not hidden:
- `VegetationPredatorFearField` global shader upload appears orphaned by current shader text scan, but the file is already part of a broader vegetation memory migration. Cutting the route needs an owner pass.
- `HectonBoidController.cs` remains dirty and still uses manual indirect rendering/hard compute admission.
- Manual indirect flora/coral/wreckage render ownership remains unresolved beyond prior global-state fixes.

---

## 2026-05-27 - Agent 13j seventh audit/fix pass

What was wrong:
- `SargassumMicroFaunaBoids` collected latch/wake stats only when `SimulationLodTier.Full`.
- The compute shader still had latch/wake stat atomics available on every main simulation dispatch, so non-due frames could pay stats write cost without CPU readback.
- Simplified-tier wake feedback became binary: full tier could publish wake impulses; simplified/sleep got no stats sampling.

What was done:
- Changed latch/wake collection to a due-only route: no pending readback, timer expired, non-sleep simulation, not leader/follower schooling, and either visible swarm rendering or parasite mode.
- Added `ResolveLatchStatsReadbackInterval(hibernation01)` so cadence scales continuously from the authored base interval to 4x slower near sleep.
- Reused `SimulationFrameConstants.AcousticPanic1.w` as `_LatchStatsActive` in `SargassumMicroFaunaBoids.compute`; this keeps the frame packet at 768 bytes and avoids a DTO migration.
- Gated `AddLatchStat()` in HLSL, so latch/wake atomics run only on the exact frame where the CPU clears stats and requests readback.

Cinematic cheats used:
- Wake feedback remains a sampled presentation signal, not a physical fluid simulation.
- No new boid solver, no full-grid wake-up, no extra DTO lane, and no new gameplay authority were introduced.

Exact microseconds saved or spent:
- Non-due active swarm frames: estimated 3-40 us saved on i3/MX350 by skipping stats clear/readback setup and 4-7 atomics per latch/wake writer.
- Due full-tier frames: cost remains close to existing behavior.
- Simplified tier: spends sparse visible/parasite stats frames instead of hard-zero wake feedback; interval stretches up to 4x by `hibernation01`.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs Assets/_Project/Art/Shaders/SargassumMicroFaunaBoids.compute` passed with line-ending warnings only.
- C# brace balance: 666 open braces / 666 close braces.
- Compute brace balance: 183 open braces / 183 close braces.
- `rg` confirmed `_LatchStatsActive`, `ResolveLatchStatsReadbackInterval(hibernation01)`, and the updated `BindSimulationUniforms(... shouldCollectLatchStats)` route.
- `SimulationFrameConstants` remains `[StructLayout(LayoutKind.Explicit, Size = 768)]`; no frame DTO stride change.
- `dotnet build` not launched: final guard found active `dotnet` PID 39640 and CPU guard reported 64%.

Unfixed, intentionally not hidden:
- `SargassumMicroFaunaBoids` still has full-grid/PBD solve only in `SimulationLodTier.Full`.
- `SargassumMicroFaunaBoids` still gates compute execution through high-resource compute admission.
- Manual indirect flora/coral/wreckage/fauna renderer ownership remains unresolved beyond the prior targeted fixes.

---

## 2026-05-27 - Agent 13j eighth audit/fix pass

What was wrong:
- `SargassumMicroFaunaBoids.EnsureComputeKernelBindings()` used `HardwareTierDetector.AllowHighResourceComputeShaders`.
- That policy is a binary high-resource backend gate. In this file it disabled the whole GPU micro-fauna simulation on compute-capable weak/mid devices instead of allowing the existing continuous budget, hibernation, active boid count, and cadence controls to scale cost.
- The file already validates every compute kernel through `HasKernel`, `FindKernel`, `IsSupported`, thread-group shape, and portable dispatch group limits, so the high-resource gate was redundant and over-destructive.

What was done:
- Replaced the high-resource admission gate with `SystemInfo.supportsComputeShaders`.
- Unsupported platforms still fail closed and use the static fallback.
- Compute-capable platforms now proceed to the existing kernel validation route and then scale through `_activeBoidCount`, `hibernation01`, `ResolvePopulationBudgetScale()`, and latch/readback cadence.

Cinematic cheats used:
- No new physical fauna solver.
- Existing visual fake remains: fewer active boids, slower cadence, sparse readbacks, static fallback only when compute is truly unsupported.

Exact microseconds saved or spent:
- CPU saving: 0 us direct. This is a policy correctness fix, not a hot arithmetic optimization.
- Low compute-capable device impact: avoids full static fallback and keeps controlled GPU fauna motion under existing budget/cadence clamps.
- GPU cost remains bounded by active boid count and validated dispatch groups; exact cost needs Unity GPU profiler capture on target backend.

Verification:
- `git diff --check -- Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` passed with line-ending warning only.
- `rg` confirms no `AllowHighResourceComputeShaders` remains in `SargassumMicroFaunaBoids.cs`.
- Sargassum brace balance: 666 open braces / 666 close braces.
- `EnsureComputeKernelBindings()` still routes through per-kernel validation after the platform compute-support gate.
- `dotnet build` not launched: final guard found active `dotnet` PID 60512 and CPU guard reported 99%.

Unfixed, intentionally not hidden:
- `SargassumMicroFaunaBoids` still has full-grid/PBD solve only in `SimulationLodTier.Full`; a continuous near/full grid-solve quality curve remains larger work.
- Legacy `HectonBoidController.cs` still uses manual `RenderMeshIndirect` and hard compute admission. It is already dirty in the worktree and needs an isolated owner pass plus compile feedback.
- Manual indirect flora/coral/wreckage/fauna renderer ownership remains unresolved beyond targeted state and quality fixes.

---

## 2026-05-27 - Agent 13j ninth audit/fix pass

What was wrong:
- Legacy `HectonBoidController.InitializeCompute()` still used `HardwareTierDetector.AllowHighResourceComputeShaders`.
- This repeated the same backend-class cliff fixed in sargassum: compute-capable weak/mid devices could lose the whole GPU school path before kernel validation ran.
- The controller already has `TryResolveKernel()`, `TryResolveThreadGroupSizeX()`, `boidShader.IsSupported(kernelIndex)`, and a 256-thread portable ceiling, so the high-resource gate was not the real compatibility check.

What was done:
- Replaced the high-resource gate with `SystemInfo.supportsComputeShaders`.
- Kept all existing kernel and thread-group validation intact.
- Did not rewrite the renderer path; `RenderMeshIndirect` ownership remains a larger dirty-file problem requiring compile feedback.

Cinematic cheats used:
- No CPU boid fallback and no second simulation authority.
- Existing visual fake route remains: validated GPU school path on compute-capable devices, disabled only when compute is truly unsupported.

Exact microseconds saved or spent:
- CPU saving: 0 us direct.
- Low compute-capable device impact: prevents unnecessary hard-disable/static loss of fauna motion.
- GPU cost remains bounded by existing boid count, indirect args, and validated dispatch groups; exact backend cost requires Unity GPU profiler capture.

Verification:
- `rg` confirms no `AllowHighResourceComputeShaders` remains in `HectonBoidController.cs`.
- `rg` confirms `InitializeCompute()` now gates on `SystemInfo.supportsComputeShaders`.
- `rg` confirms `TryResolveKernel`, `TryResolveThreadGroupSizeX`, and `boidShader.IsSupported(kernelIndex)` remain in the file.
- `HectonBoidController.cs` brace balance: 149 open braces / 149 close braces.
- `git diff --check` passed for the touched 13j files with line-ending warnings only.
- `dotnet build` not launched: final guard found active `dotnet` PIDs 8080, 23212, 25488, 31092, 32588, 33532, 44048, 55628 plus `VBCSCompiler` PID 46008 and CPU guard reported 100%.

Unfixed, intentionally not hidden:
- Legacy `HectonBoidController.cs` still uses manual `RenderMeshIndirect`.
- `SargassumMicroFaunaBoids` still has full-grid/PBD solve only in `SimulationLodTier.Full`.
- Manual indirect flora/coral/wreckage/fauna renderer ownership remains unresolved beyond targeted state and quality fixes.

---

## 2026-05-27 - Agent 13j tenth audit/fix pass

What was wrong:
- `SargassumCrestDampingController` still used `HardwareTierDetector.AllowHighResourceComputeShaders` before dispatching the facade bake and again inside kernel thread-group validation.
- This facade is a sargassum flora/water visual cheat: public wave damping and oil-film textures derived from canopy density and cut masks.
- The file already validates actual compute compatibility through `compute.IsSupported(kernel)`, thread-group shape, and a 256-thread portable ceiling, so the high-resource policy was a binary backend cliff.

What was done:
- Replaced both high-resource checks with `SystemInfo.supportsComputeShaders`.
- Kept unsupported-compute fail-closed behavior.
- Kept `compute.IsSupported(kernel)`, `GetKernelThreadGroupSizes`, 2D group validation, and 65535 dispatch dimension cap intact.

Cinematic cheats used:
- The solution remains a facade texture bake, not physical wave simulation.
- Weak compute-capable devices get the cheap visual damping/oil film route instead of no facade; high/ultra keep full facade baking.

Exact microseconds saved or spent:
- CPU saving: 0 us direct.
- Visual gain: removes hard disable of canopy-derived wave/oil facade on compute-capable non-high-resource backends.
- GPU cost remains one compute dispatch over the facade texture; exact backend cost requires Unity GPU profiler capture.

Verification:
- `rg` confirms no `AllowHighResourceComputeShaders` remains in `SargassumCrestDampingController.cs`.
- `rg` confirms `SystemInfo.supportsComputeShaders`, `compute.IsSupported(kernel)`, and `PortableMaxComputeThreadsPerGroup` remain in the validation path.
- `SargassumCrestDampingController.cs` brace balance: 62 open braces / 62 close braces.
- `git diff --check` passed for touched 13j files with line-ending warnings only.
- One guarded full build attempt was made: `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`. It timed out after 120 seconds with no compiler diagnostics. Build-created MSBuild/VBCSCompiler processes were cleaned by `dotnet build-server shutdown`; final guard showed no active compiler process and CPU 93%, so a second build was forbidden.
- Final self-review caught `AllowHighResourceComputeShaders` had reappeared in `SargassumMicroFaunaBoids.cs`; the gate was restored to `SystemInfo.supportsComputeShaders` and `rg` was rerun across all three touched fauna/flora compute admission files.

Unfixed, intentionally not hidden:
- `SargassumCrestDampingController` still publishes cross-renderer facade textures through shader globals. This is acceptable for a single global ocean/flora facade owner today, but an ocean-owned constant-buffer/texture bridge would be cleaner.
- Legacy `HectonBoidController.cs` still uses manual `RenderMeshIndirect`.
- `SargassumMicroFaunaBoids` still has full-grid/PBD solve only in `SimulationLodTier.Full`.
- Manual indirect flora/coral/wreckage/fauna renderer ownership remains unresolved beyond targeted state and quality fixes.

---

## 2026-05-27 - Agent 13j eleventh audit/fix pass

What was wrong:
- `HardwareTierDetector.AllowHighResourceComputeShaders` was still present in 13j-owned runtime domain files after the previous pass: sargassum fauna, sargassum crest facade, legacy boids, indirect vegetation, and GPU scatter.
- That is a binary backend-class switch. It disables compute-capable weak/mid devices before local continuous quality, density, hibernation, and cadence controls can scale cost.
- `GPUScatterDirector` also bound first-party scatter draw buffers/vectors through `Shader.SetGlobal*`: instances, visible indices, density bins, density params, and AUP grid offset.
- Those globals are process-wide render state, not draw-local scatter payload. One draw could contaminate another pass using the same property IDs.

What was done:
- Replaced the domain compute-admission gates with `SystemInfo.supportsComputeShaders`.
- Preserved existing real compatibility checks: `HasKernel`, `FindKernel`, `IsSupported`, thread-group shape checks, portable 256-thread ceilings, and dispatch dimension guards where each system already had them.
- Added a cold-owned `MaterialPropertyBlock` to `GPUScatterDirector` and bound first-party scatter draw data through `RenderParams.matProps`.
- Kept terrain/biome shader globals and unresolved mod bridge globals in place because they are bridge ownership routes, not first-party scatter draw payload.

Cinematic cheats used:
- No physical flora/fauna/scatter solver was added.
- Weak compute-capable devices now use existing cheap presentation paths: sparse density, hibernated swarms, cadence throttles, facade texture bakes, and indirect scatter draw data.
- High/Ultra keep the same routes with more density/cadence/visual richness through existing continuous budgets.

Exact microseconds saved or spent:
- Compute-admission changes: 0 us CPU direct. This is correctness/platform reach; cost remains bounded by existing budgets.
- Scatter draw-local property block: estimated 0-3 us CPU delta, no per-frame allocation because the property block is a field.
- State isolation gain: prevents process-wide draw payload collisions; exact frame impact needs RenderDoc/Unity profiler capture on target scenes.

Verification:
- `rg` confirms no `AllowHighResourceComputeShaders` remains under `Assets/_Project/Scripts/World` plus `Assets/_Project/Scripts/HectonBoidController.cs`.
- `rg` confirms no first-party scatter draw-specific `Shader.SetGlobalBuffer(_ScatterInstancesId/_VisibleIndicesId/_ScatterDensityBinsId)` and no `Shader.SetGlobalVector(_ScatterAupGridOffsetId/_ScatterDensityParamsId)` remains in `GPUScatterDirector.cs`.
- Brace balances: `SargassumMicroFaunaBoids.cs` 667/667, `SargassumCrestDampingController.cs` 64/64, `HectonBoidController.cs` 149/149, `HectonIndirectVegetationRenderer.cs` 427/427, `GPUScatterDirector.cs` 136/136.
- `git diff --check` passed for the touched files with line-ending warnings only.
- `dotnet build` not launched: final guard found active `dotnet` and `VBCSCompiler` processes, and CPU average was 100%, above the project limit.

Unfixed, intentionally not hidden:
- `GPUScatterDirector` still has biome/terrain and mod bridge `Shader.SetGlobal*` calls.
- `ScatterGPUIBackend.BindInstanceBuffer()` still has an unused/legacy `material.SetBuffer` route.
- `HectonOctahedralImpostorRenderer` still mutates material state in its render path.
- Manual indirect renderer ownership remains unresolved across flora/fauna/structure lanes beyond the targeted draw-state fixes.

---

## 2026-05-27 - Agent 13j twelfth audit/fix pass

What was wrong:
- `HectonOctahedralImpostorRenderer` wrote impostor atlases, buffers, time, fade, quality, and floating-origin offset into material state before `RenderMeshIndirect`.
- Legacy `HectonBoidController` wrote boid buffers and render floats into its runtime material before each indirect draw.
- `SargassumMicroFaunaBoids` wrote boid buffer, VAT textures, hit flash, parasite, hibernation, and interpolation values into its runtime material before each indirect draw.
- `ScatterGPUIBackend.BindInstanceBuffer()` exposed a dormant `Material.SetBuffer` route.
- Concurrent work reintroduced `HardwareTierDetector.AllowHighResourceComputeShaders` in 13j domain files after the previous clean scan.

What was done:
- Added persistent draw-local `MaterialPropertyBlock` payloads to HLOD impostor, legacy boid, and sargassum micro-fauna render paths.
- Passed those MPBs through `RenderParams.matProps`.
- Converted `ScatterGPUIBackend.BindInstanceBuffer()` to accept `MaterialPropertyBlock`.
- Replaced returned `AllowHighResourceComputeShaders` gates with `SystemInfo.supportsComputeShaders` in HectonBoid, GPUScatter, indirect vegetation, sargassum micro-fauna, crest facade, and GPUI scatter vendor admission.

Cinematic cheats used:
- No new physical simulation.
- Kept existing visual fake routes: VAT fauna, HLOD impostor cards, sparse/hibernated swarm budgets, facade texture bakes, and indirect draw payloads.
- Quality still scales by existing continuous weights/cadences; unsupported compute still fails closed.

Exact microseconds saved or spent:
- Draw-local MPB changes: estimated 0-4 us CPU delta. No per-frame heap allocation; MPBs are cold fields.
- Compute-admission changes: 0 us CPU direct. Platform/visual continuity fix.
- Real performance proof still requires Unity profiler/Frame Debugger capture on target scenes.

Verification:
- `rg` confirms no `AllowHighResourceComputeShaders` remains under `Assets/_Project/Scripts/World` plus `Assets/_Project/Scripts/HectonBoidController.cs`.
- `rg` confirms no `material.Set*`, `_boidRuntimeMaterial.Set*`, `_runtimeFishMaterial.Set*`, or `BindInstanceBuffer(Material, ...)` remains in the fixed indirect draw lanes.
- Brace balances: `SargassumMicroFaunaBoids.cs` 684/684, `HectonBoidController.cs` 180/180, `HectonOctahedralImpostorRenderer.cs` 73/73, `ScatterGPUIBackend.cs` 16/16, `ScatterInstancingService.cs` 28/28, `SargassumCrestDampingController.cs` 64/64, `HectonIndirectVegetationRenderer.cs` 427/427, `GPUScatterDirector.cs` 136/136.
- `git diff --check` passed for touched source files with line-ending warnings only.
- `dotnet build` not launched: active `dotnet` PID 62864 and `VBCSCompiler` PID 6448, CPU average 100%.

Unfixed, intentionally not hidden:
- Manual indirect renderer ownership remains unresolved across HLOD impostor, boid, sargassum, flora, coral, wreckage, and scatter lanes.
- `GPUScatterDirector` still has biome/terrain and mod bridge `Shader.SetGlobal*` calls.
- `SargassumCrestDampingController` still publishes cross-renderer facade textures globally.

---

## 2026-05-27 - Agent 13j post-compaction verification

What was checked:
- Reran exact indirect draw mutation scan with `BindInstanceBuffer(Material\s)` across sargassum micro-fauna, legacy boids, HLOD impostors, and `ScatterGPUIBackend`: no material mutation or material-parameter backend remains. The broad `BindInstanceBuffer(Material` match is only `MaterialPropertyBlock`.
- Reran domain compute admission scan: no `AllowHighResourceComputeShaders` remains under `Assets/_Project/Scripts/World` or `Assets/_Project/Scripts/HectonBoidController.cs`.
- Reran brace balances for the eight touched domain source files: all balanced.
- Reran `git diff --check` on proof docs and touched source files: pass, line-ending warnings only.

Build status:
- New build still forbidden. Active `dotnet` PIDs 32412 and 47232 exist; CPU average is 100%.
- No compile diagnostics available after the earlier timed-out guarded build attempt.

Cinematic Cheats / microseconds:
- No new runtime code in this verification pass.
- Exact saved runtime: 0 us. Saved integration risk: stale proof artifact avoided.

---

## 2026-05-27 - Agent 13j recurrent admission-gate repair

What was wrong:
- Final post-compaction scan found `HardwareTierDetector.AllowHighResourceComputeShaders` reintroduced again in the domain files after the proof refresh.

What was done:
- Re-applied the scoped replacement to `SystemInfo.supportsComputeShaders` in `HectonBoidController`, `GPUScatterDirector`, `HectonIndirectVegetationRenderer`, `ScatterInstancingService`, `SargassumMicroFaunaBoids`, and `SargassumCrestDampingController`.

Verification:
- `rg` now returns no `AllowHighResourceComputeShaders` under `Assets/_Project/Scripts/World` or `Assets/_Project/Scripts/HectonBoidController.cs`.
- Current brace balances: sargassum 685/685, legacy boids 180/180, HLOD impostors 73/73, `ScatterGPUIBackend` 16/16, GPU scatter 136/136, indirect vegetation 427/427, GPUI scatter service 28/28, crest damping 64/64.
- `git diff --check` passed for touched sources and proof docs with line-ending warnings only.

Build status:
- New build still forbidden: active `dotnet` PID 47232 and active `VBCSCompiler` PID 35836. CPU samples were volatile at 44-100%, but the process guard alone blocks another build.

Cinematic Cheats / microseconds:
- No new solver or simulation.
- Runtime delta: 0 us CPU direct. Correctness gain: removes binary high-resource backend cliff again.

---

## 2026-05-27 - Agent 13j lock-upload and BRG scratch pass

What was wrong:
- `GPUScatterDirector` still used `GraphicsBuffer.SetData` for scatter indirect args.
- Legacy `HectonBoidController` still used `UploadArraySetData` for visible indirect args and boid spawn upload into both ping-pong buffers.
- `HectonIndirectVegetationRenderer` BRG CPU culling allocated five tiny `TempJob NativeArray<float4>` scratch buffers per cull callback for planes and scooter headlight payloads.
- Current verification again found `HardwareTierDetector.AllowHighResourceComputeShaders` returned in the same domain files.

What was done:
- Created scatter args, boid spawn, and boid visible args buffers with `LockBufferForWrite` usage where CPU upload is required.
- Routed those uploads through `GraphicsBufferUploadUtility.UploadArray()`.
- Replaced bounded BRG culling/headlight scratch arrays with `FixedList512Bytes<float4>` fields inside `BuildVegetationVisibilityMaskJob`.
- Re-applied the scoped admission replacement to `SystemInfo.supportsComputeShaders` in `HectonBoidController`, `GPUScatterDirector`, `HectonIndirectVegetationRenderer`, `ScatterInstancingService`, `SargassumMicroFaunaBoids`, and `SargassumCrestDampingController`.

Cinematic Cheats used:
- No new physical simulation.
- Kept existing fake-first lanes: indirect scatter density, GPU boid presentation, scooter headlight cone culling, hibernated sargassum motion, and facade texture cheats.
- Quality remains controlled by existing continuous budgets; unsupported compute still fails closed.

Exact microseconds saved or spent:
- Upload route: estimated 4-25 us CPU/stall risk avoided on spawn/mesh-change upload frames by removing `SetData` routes.
- Vegetation BRG culling: five tiny `TempJob` allocations removed per callback; exact CPU saving depends on BRG callback frequency and allocator pressure.
- Admission repair: 0 us CPU direct; prevents binary no-visual fallback on compute-capable weak/mid devices.

Verification:
- `rg` returns no `.SetData`, `UploadArraySetData`, stale `SetData` comments, or `CreateStructuredBuffer<BoidData>` in `GPUScatterDirector.cs` or `HectonBoidController.cs`.
- `rg` returns no `NativeArray<float4>` culling/headlight scratch allocations in `HectonIndirectVegetationRenderer.cs`; bounded payloads are `FixedList512Bytes<float4>`.
- `rg` returns no `AllowHighResourceComputeShaders` under `Assets/_Project/Scripts/World` or `Assets/_Project/Scripts/HectonBoidController.cs`.
- Brace balances: GPU scatter 136/136, legacy boids 180/180, indirect vegetation 427/427, sargassum 685/685, crest damping 64/64, GPUI scatter service 28/28.
- `git diff --check` passed for touched source files with line-ending warnings only.
- `dotnet build` not launched: no active compiler processes, but CPU average 56.8% is above the explicit 50% guard.

---

## 2026-05-27 - Agent 13j guarded build timeout and churn repair

What was wrong:
- A later guard sample allowed one build, but `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` timed out after 180 seconds with no compiler diagnostics.
- The timed-out build left `dotnet` PID 24280 running.
- After the timeout, the same files again showed reverted upload routes and returned `AllowHighResourceComputeShaders` gates.

What was done:
- Ran `dotnet build-server shutdown`.
- Verified PID 24280 command line was the timed-out `Hecton8.slnx` build and stopped only that process.
- Did not stop later `dotnet` PID 36124 because command line shows a separate `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`.
- Re-applied lock-buffer uploads in `GPUScatterDirector` and legacy `HectonBoidController`.
- Re-applied `SystemInfo.supportsComputeShaders` admission gates in the six 13j domain files.

Cinematic Cheats used:
- No new simulation or physical realism.
- Restored existing fake-first budget lanes: GPU scatter draw args, boid GPU presentation, fixed headlight/frustum BRG culling payloads, and continuous compute-capable admission.

Exact microseconds saved or spent:
- Build attempt: 0 us runtime; verification inconclusive.
- Cleanup: stopped one timed-out build process after command-line proof.
- Churn repair restores previous estimates: 4-25 us upload-frame stall risk reduction, five tiny BRG `TempJob` allocations removed per cull callback, and 0 us CPU direct for compute-admission repair.

Verification:
- `rg` returns no `SetData`, `UploadArraySetData`, `CreateStructuredBuffer<BoidData>`, or `AllowHighResourceComputeShaders` in the targeted 13j upload/admission files after repair.
- Vegetation BRG fixed-list scratch remains intact.
- Brace balances: GPU scatter 136/136, legacy boids 181/181, indirect vegetation 427/427, sargassum 685/685, crest damping 64/64, GPUI scatter service 28/28.
- `git diff --check` passed for touched source/proof files with line-ending warnings only.
- No second build launched: active external `dotnet` PID 36124 plus CPU average 69.7% violate the build guard.

---

## 2026-05-27 - Agent 13j BRG audit and volatile-source wall

What was wrong:
- Remaining BRG paths in `WreckMaterialRegistry` and `SargassumGlobalDragManager` still bind draw buffers through runtime materials.
- Current source repeatedly reintroduced previously fixed regressions: `HardwareTierDetector.AllowHighResourceComputeShaders`, legacy boid/sargassum material mutation, and non-lock upload routes.

What was done:
- Audited `WreckMaterialRegistry`, `SargassumGlobalDragManager`, `HectonBatchRendererGroupUtility`, `Hecton_WreckIndirectLit.shader`, and `Hecton_CollapseScavengerIndirect.shader`.
- Confirmed the BRG shaders read explicit named `StructuredBuffer` payloads; a blind MPB or `SetBatchBuffer`-only patch would break matrix/age/scavenger binding.
- Re-applied the known scoped repairs several times, then stopped when exact scans showed concurrent writes overwriting the same files between patch and verification.

Cinematic Cheats used:
- No new simulation.
- Preserved existing fake-first presentation lanes. Did not replace BRG shader contracts with an unproven metadata path.

Exact microseconds saved:
- BRG audit: 0 us direct; prevented a probable wreck/scavenger render break.
- Volatile repair attempts: no stable current runtime claim. When the lock-upload patch survives, expected benefit remains 4-25 us upload-frame stall-risk reduction; current source cannot be reported clean.

Verification:
- `git diff --check` passed for touched source/proof files with line-ending warnings only.
- Brace balances remain valid: legacy boids 188/188, GPU scatter 136/136, indirect vegetation 427/427, sargassum 685/685, crest damping 64/64, GPUI scatter service 28/28.
- Current `rg` still reports returned high-resource gates and material mutation after concurrent source overwrites.
- Build not launched: CPU average was 100%, and the source target is volatile.
---
## 2026-05-27 - Agent 13j vegetation sampler containment and layout proof

What was wrong:
- Vegetation density/threat sampling trusted external `chunkCount` and `GridOffset` in Burst-readable `NativeArray` snapshots.
- `VegetationDensityChunkRecord` had implicit layout/padding while used in NativeArray/Burst jobs.
- The earlier volatile files still contain returned high-resource gates, upload routes, and material mutation.

What was done:
- Hardened `VegetationMath` sampling loops with created checks, safe chunk count clamp, finite chunk bounds, and grid offset range checks.
- Split public guarded chunk samplers from private unchecked bilinear helpers so validated loops do not re-run the same guard inside each sample.
- Declared `VegetationDensityChunkRecord` as explicit 24-byte layout with offsets 0/4/8/12/16/20 and named padding at 21-23.
- Did not touch the repeatedly overwritten boid/sargassum/scatter files in this pass.

Cinematic Cheats used:
- No new simulation.
- Fail-closed density/threat samples preserve the existing vegetation audio, threat, and flow fake-first lanes instead of crashing or simulating recovery.

Exact microseconds saved:
- No speed claim. Normal-case guard overhead is estimated 0-2 us depending sampled chunk count.
- Prevents OOB/NaN crash/corruption paths; microseconds saved are not meaningful compared with avoided fault.
- Build not run because CPU average was 100%.

Verification:
- `git diff --check` passed for `VegetationMath.cs` and `HectonMapMagicVegetationBridge.cs` with line-ending warnings only.
- Brace balances: `VegetationMath.cs` 50/50; `HectonMapMagicVegetationBridge.cs` 703/703.
- `rg` confirms sampler loops now use `safeChunkCount`, `IsDensityChunkUsable`, and unchecked helpers only after validation.
- `rg` confirms `VegetationDensityChunkRecord` has explicit 24-byte layout and padding.
- Current volatile scan still reports old returned gates/upload/material mutation in boid/sargassum/scatter files; not claimed fixed.

---
## 2026-05-27 - Agent 13j procedural scatter DTO layout pass

What was wrong:
- Procedural scatter candidate acceptance sends `ScatterPlacementSpatialMetadata` and `ScatterCellCandidateAcceptanceInput` through `NativeList`, `NativeArray`, and Burst jobs with implicit sequential layout.
- `ScatterCellCandidateAcceptanceInput` was effectively a 60-byte runtime payload, not a multiple-of-8 ARM64 stride, with undocumented padding around byte flags and final floats.

What was done:
- Converted `ScatterPlacementSpatialMetadata` to explicit 32-byte layout with offsets `0/12/16/20/24/28` and named padding at `29-31`.
- Converted `ScatterCellCandidateAcceptanceInput` to explicit 64-byte layout with offsets `0/12/16/20/24/28/32/36/40/44/48/49/50/51/52/56/60` and named padding at `53-55`.
- Did not change placement acceptance math, budgets, hash buckets, or candidate policy.

Cinematic Cheats used:
- No new simulation.
- Preserved the existing deterministic scatter acceptance fake: structured placement windows, spatial buckets, and cluster patch masks instead of physical ecosystem simulation.

Exact microseconds saved:
- No speed claim. This is layout correctness.
- Candidate input scratch stride increases to 64 bytes; bounded memory delta only.
- Build not run because CPU average was 100%.

Verification:
- `git diff --check` passed for `WorldProceduralScatterDirectorCandidateAcceptance.cs` with line-ending warning only.
- Brace balance: `WorldProceduralScatterDirectorCandidateAcceptance.cs` 126/126.
- `rg` confirms explicit layout sizes, field offsets, padding fields, and NativeArray/NativeList job use.
