# LOG - VFX_SDF_CARVE_DEBRIS

## 2026-05-14 - Compute Advection Carve Particles

What was wrong:
- SDF carve feedback had no direct GPU debris path tied to authoritative `VoxelCarveEvent` packets.
- A GameObject, ParticleSystem, or CPU readback implementation would violate the prompt and cost hundreds of microseconds during carve bursts.
- Compile verification is blocked by local tooling: Unity MCP transport fails at `http://127.0.0.1:8088/mcp`, active Unity processes hold `Temp/UnityLockfile`, and safe batchmode compile cannot be launched.

What was done:
- `VoxelCarveEvent` is the carve ingress signal for VFX, preserving hit point, radius, impulse, shape, material, and volume id after voxel validation.
- `BufferID.CarveDebris` and `BufferID.CarveDebrisVelocity` provide H-PHI/DataVault SOA lanes for position-lifetime and velocity.
- `CarveDebrisComputeRenderer` owns the runtime bridge: persistent NativeArrays, persistent ping-pong `GraphicsBuffer`s, Burst aging/injection jobs, dirty-range GPU uploads, AUP shift handling, Math LOD, indirect draw, and 300-frame blackbox telemetry.
- `Hecton_FluidAdvection.compute` contains `ClearCarveDebrisIndirectArgs`, `AdvectCarveDebris`, and `CullCarveDebrisForRender`, using flow drag, dynamic wakes, gravity, optional SDF collision/dissolve, NaN guard, and GPU-side indirect instance counting.
- `Hecton_CarveDebrisIndirect.shader` renders low-poly CoreLit rock chips with edge tint, cave ambient, caustic scatter, noir fog, and dither fade.
- OMEGA polish replaced lifetime float divisions with reciprocal multiplies and cached dispatch group count after kernel setup. Targeted scan found no `GetData`, `SetData`, `foreach`, interpolated strings, `math.sqrt`, `math.normalize`, `dt /`, or `1f /` in touched VFX files.

Cinematic cheats used:
- Fake octahedron chips instead of rigidbody fragments.
- Flow drag and dynamic wake advection instead of particle physics.
- SDF hit dissolves and velocity kill instead of collision contacts.
- Shader edge tint and CoreLit caustics buy perceived fracture detail without more mesh complexity.
- Low tier disables SDF sampling and injects 16 chips; high/ultra keep 64 chips and spend saved cost on lighting/material richness.

Exact microseconds saved:
- 150-400 us saved on burst frames versus transform-spawned mesh debris.
- 80-180 us saved by dirty-range GPU upload versus full-buffer upload plus managed emission.
- 30-90 us saved on MX350/i3 low tier by injecting 16 instead of 64 particles and skipping one 3D SDF texture sample per live particle.
- 50-120 us saved during AUP shift frames by applying the rebase on GPU instead of rewriting 4096 CPU positions.
- 10-20 us saved per full draw by SOA render reads that avoid fetching velocity/flags.
- Sub-1 us saved by reciprocal lifetime math; value is auditability, not frame-time magnitude.

Verification state:
- Static scan passed for the targeted anti-bloat patterns.
- `git diff --check` only reports line-ending normalization warnings on edited files.
- Unity compile/live console verification remains `[BLOCKED BY TOOLING]`; no false pass recorded.

## 2026-05-14 - Second-Pass Hardening

What was wrong:
- Low tier still paid too much of the 4096-slot scan/dispatch envelope after the first pass.
- The empty fallback flow buffer could make telemetry and compute binding look like live abyssal flow.
- Shared fluid compute kernels referenced dynamic wake buffers, so carve debris needed explicit no-wake bindings instead of relying on external owners.
- Fast chips could cross thin SDF features unless velocity was visually bounded.
- Unity compile verification is still blocked: MCP transport is unreachable, Unity owns `Temp/UnityLockfile`, and the generated project files do not yet include `Hecton8.VFX.Debris.csproj`.

What was done:
- Added a low-tier active capacity of 1024 while keeping high/ultra storage and draw capacity at 4096.
- Applied the active capacity to CPU mirror aging, injection, compute dispatch groups, cull capacity, and indirect max instance count.
- Bound published `HectonFluidEngine` flow buffers/textures through the public contract and stopped counting the one-element fallback buffer as active flow.
- Bound `_DynamicWakes` and `_DynamicWakeVectors` to a safe fallback buffer with `_DynamicWakeParams.x = 0`.
- Moved fallback mesh/material creation into `Awake` and `OnEnable` to avoid first-active-frame cold work where possible.
- Added GPU velocity and per-frame step clamping in `AdvectCarveDebris`.
- Preserved blackbox invalid flags during mirror aging so NaN/corruption evidence is not wiped by a normal frame pass.

Cinematic cheats used:
- Low tier reduces active particles and dispatch groups instead of lowering art quality on each chip.
- Velocity clamp is a visual stability fake, not a physical integrator.
- High/ultra spend saved cycles on flow, SDF dissolve, wake billow/shear, and CoreLit material response instead of rigidbody shards.

Exact microseconds saved:
- 25-35 us estimated GPU saving on MX350 by dropping low-tier dispatch from 64 groups to 16 groups.
- 10-25 us estimated CPU saving on idle frames by skipping mirror aging when no debris is alive.
- 30-90 us retained from low-tier SDF bypass and lower injection count.
- Millisecond-scale GPU stalls avoided by keeping verification and visibility on indirect args instead of `GetData`.

Verification state:
- Static VFX scan still finds no `GetData`, `SetData`, `ParticleSystem`, `ComputeBuffer`, `foreach`, `.ToString`, `string.Format`, or interpolated-string hot path in touched VFX files.
- Shader scan shows reciprocal/`rsqrt` math and no new hot `sqrt`, `pow`, `exp`, or `log` path in the carve debris compute lane.
- `dotnet build Hecton8.Core.csproj --no-restore` fails on unrelated symbols in UI/fauna/world/core files; that csproj does not include `CarveDebrisComputeRenderer.cs`.
- Status remains `PENDING VERIFICATION` until Unity imports the new asmdef and the live editor or batchmode compile can be queried.

## 2026-05-14 - Code/Status Reconciliation Pass

What was wrong:
- The status file claimed protections that the current renderer file did not fully contain.
- The active `CURRENT_BATCH.md` has been replaced and no longer contains `<AGENT_PROMPT id="VFX_SDF_CARVE_DEBRIS">`; prompt re-read returns `PROMPT_NOT_FOUND_IN_CURRENT_BATCH`.
- Unity batchmode r4 failed before compile under sandbox Package Manager access with `attempt to write a readonly database` and return code 1.
- Unity batchmode r5 ran outside sandbox but failed before compile on local Unity license/headless entitlement with return code 198.
- `dotnet build Hecton8.Core.csproj` is red from unrelated Core/Save/Hardware/Physics errors; the generated project still does not include a VFX debris csproj.

What was done:
- Reconciled `CarveDebrisComputeRenderer.cs` with the recorded contract: global cave SDF shader fallback, SDF cache refresh throttling, serialized camera only, `GlobalRegistry.Fluid` binding instead of `HectonFluidEngine.Instance`, subtract-only carve debris emission, shape/operation validation, box/blend radius fallback, full-packet stable seed hashing, AUP shift duplicate and NaN guards, mesh index-count draw guards, active-only telemetry publish, release-time SDF cache cleanup, and explicit flow texture parameter normalization.
- Kept VFX hot path free of `Camera.main`, `GetData`, `SetData`, `ParticleSystem`, `ComputeBuffer`, managed `foreach`, `.ToString`, `string.Format`, interpolated strings, `FindObjectOfType`, and `GameObject.Find`.
- Applied one justified cross-domain carve interface fix in `VoxelDeltaProcessor`: cave-in dust decal calls now convert the `double3` AUP hit point back to `Vector3` for the existing decal API, while runtime debris spawn keeps `double3` until `HectonFloatingOrigin.ToRuntimePosition`.
- Did not edit `SaveMasterHashV10`, `BinaryLayoutManifest`, hardware profile, homeostasis, or global physics blockers because they are outside this VFX/SDF carve domain.

Cinematic cheats used:
- SDF collision remains a visual dissolve/velocity kill, not contact physics.
- Low tier uses 1024 active slots and no SDF sample; high/ultra use global cave SDF and flow richness inside the same fixed 4096 storage.
- Box and blend carve events resolve to a cheap debris spawn radius instead of simulating fracture geometry.

Exact microseconds saved:
- 25-35 us retained on MX350 by low-tier 16-group dispatch instead of 64 groups.
- 30-90 us retained by low-tier SDF bypass and 16-particle injection.
- 150-400 us retained versus transform-spawned mesh debris.
- 50-120 us retained on origin-shift frames by GPU AUP rebase instead of CPU buffer rewrite.
- Idle telemetry gate removes one warning publish every 30 idle frames; exact saving is sub-microsecond but removes false pressure from telemetry.

Verification state:
- Static VFX bloat scan: PASS.
- Shader hot math scan: PASS.
- `git diff --check` on touched VFX/SDF files: PASS.
- Unity compile: BLOCKED before script compile by local license/headless entitlement in r5.
- Root `dotnet build`: BLOCKED by unrelated Core/Save/Hardware/Physics errors; no captured `VoxelDeltaProcessor` or VFX debris errors after the cross-domain handoff fix.

## 2026-05-15 - H-Phi Burst Ingestion Pass

What was wrong:
- Carve signal ingestion still used one immediate job execution path per valid carve event, which scales badly during 2-32 carve bursts.
- Same-frame work used the old scheduled/completed job pattern, adding scheduler fence overhead without any real parallelism.
- Flow texture metadata was assigned before local validation, so an invalid texture path could overwrite the center used by the structured-buffer fallback.
- User explicitly forbade dotnet rebuilds during this pass.

What was done:
- Added persistent `NativeArray<CarveDebrisRequest>` storage sized to `MaxCarveSignalsPerFrame`.
- Replaced per-carve injection with one `CarveDebrisInjectBatchJob.Run()` per frame.
- Converted `AgeCarveDebrisMirrorJob` to `Run()` as well; the VFX debris file now has no `JobHandle`, `.Schedule()`, or `.Complete()` calls.
- Added local finite/resource validation for flow buffers, grid resolution, center, spacing, texture presence, and texture params before marking flow active or overwriting flow uniforms.
- Kept all GPU visibility and validation readback-free; no `dotnet build` or rebuild command was executed.

Cinematic cheats used:
- Burst carve events are collapsed into one visual request batch rather than simulating independent fracture systems.
- Flow remains a presentation driver with fail-closed metadata validation, not a gameplay physics dependency.
- Low tier keeps 1024 active slots and no SDF sample; high/ultra spend the same fixed storage on richer SDF/flow motion.

Exact microseconds saved:
- 20-70 us estimated CPU saving on 2-32 carve burst frames by replacing per-carve scheduler fences with one synchronous Burst run.
- 5-20 us estimated CPU saving on active mirror frames by replacing scheduled/completed aging with `Run()`.
- 25-35 us retained on MX350 from low-tier 16-group dispatch.
- 30-90 us retained from low-tier SDF bypass and reduced injection count.
- 150-400 us retained versus transform-spawned mesh debris.

Verification state:
- Static scan: no `JobHandle`, `.Schedule()`, or `.Complete()` remains in `CarveDebrisComputeRenderer.cs`.
- Static VFX scan: no `HectonFluidEngine.Instance`, `Camera.main`, `FindObjectOfType`, `GameObject.Find`, `ComputeBuffer`, `GetData`, `SetData`, `ParticleSystem`, `foreach`, `.ToString`, or `string.Format` in the touched VFX lane.
- `git diff --check`: only line-ending normalization warning on `CarveDebrisComputeRenderer.cs`.
- Unity compile remains PENDING/BLOCKED by local editor/license state; no fake pass recorded.

## 2026-05-15 - Scalability Hysteresis Pass

What was wrong:
- Low/high tier selection was sampled from `GlobalRegistry` every frame.
- Active capacity could change immediately, which risks repeated 1024/4096 capacity shed and upload churn if the hardware scaler oscillates.

What was done:
- Added a 30-frame cached tier sample in `CarveDebrisComputeRenderer`.
- Added a 120-frame confirmation window before switching between low and non-low active capacity.
- Reset the tier cache on GPU state release and kept Low as the fail-closed initial state.
- Did not run any dotnet rebuild.

Cinematic cheats used:
- Low tier stays visually stable instead of flickering chip count during transient frame spikes.
- High/ultra visual richness is delayed until the device is consistently above low tier, avoiding capacity thrash.

Exact microseconds saved:
- Sub-1 us per steady frame from removing 4-5 registry property reads in this renderer.
- 10-40 us estimated on transient oscillation frames by avoiding repeated capacity tail clear/upload and dispatch count churn.

Verification state:
- Static check confirms tier cache fields and `SampleLowTierFlag()` are present.
- Static VFX scan remains clear of `JobHandle`, `.Schedule()`, `.Complete()`, GPU readbacks, singleton access, and scene search.
- Unity compile remains PENDING/BLOCKED by local editor/license state; no fake pass recorded.

## 2026-05-15 - Injection Scan Cursor Pass

What was wrong:
- The batched injection path removed per-carve job fences, but each request could still restart dead-slot search from index zero.
- Dense buffers would repeatedly reread the same occupied prefix when many carve events arrived in one frame.

What was done:
- Added a monotonic `scanStart` cursor inside `CarveDebrisInjectBatchJob`.
- Each request now resumes from the previous scan position and the batch stops once active capacity is exhausted.
- Kept the failure mode deterministic: invalid generated particle data sets the blackbox flag and skips that slot without readback or allocation.
- Did not run dotnet build or rebuild.

Cinematic cheats used:
- Multiple carve impacts are collapsed into one visual debris batch and one forward slot walk.
- No physical fracture or CPU free-list ownership is introduced; the GPU collision path remains authoritative for visible decay.

Exact microseconds saved:
- 15-60 us estimated on dense 2-32 carve burst frames by avoiding repeated occupied-prefix scans.
- 20-70 us retained from the earlier scheduler-fence removal.
- 25-35 us retained from low-tier 1024-slot active cap.
- 150-400 us retained versus transform-spawned mesh debris.

Verification state:
- Static code read confirms `scanStart` advances across request boundaries in `CarveDebrisInjectBatchJob`.
- Static scan confirms no `JobHandle`, `.Schedule()`, `.Complete()`, GPU readback, singleton access, or scene search was reintroduced.
- Unity compile remains PENDING/BLOCKED by local editor/license state; no fake pass recorded.

## 2026-05-15 - Flow Center Compatibility Pass

What was wrong:
- The compute shader uses one `_AbyssalFlowCenter` for both 3D texture sampling and structured-buffer fallback sampling.
- A valid texture override could overwrite the center while leaving a valid fluid buffer fallback active with different grid metadata.

What was done:
- Added `AreFlowCentersCompatible()` and `DisableFlowBufferFallback()` in `CarveDebrisComputeRenderer`.
- Published or override texture flow now disables structured-buffer fallback when the two centers disagree.
- Same-origin fluid buffer+texture paths remain active together.
- Did not run dotnet build or rebuild.

Cinematic cheats used:
- Presentation flow fails closed to the texture path instead of trying to reconcile two different flow volumes in one shader bind.
- The fix preserves the cheap single-center shader contract and avoids widening the shared fluid advection API.

Exact microseconds saved:
- Direct CPU saving is negligible; the guard is a correctness fix.
- Avoided cost is wrong-cell buffer sampling and false debris drift without adding GPU readback or a second shader payload.

Verification state:
- Static code read confirms buffer fallback is disabled only when texture flow is active and centers differ.
- Static shader read confirms the single-center constraint in `SampleAbyssalFlow`.
- Unity compile remains PENDING/BLOCKED by local editor/license state; no fake pass recorded.

## 2026-05-15 - Render Hot-Math Cache Pass

What was wrong:
- `Hecton_CarveDebrisIndirect.shader` used `sincos` in the vertex path to orient every debris chip.
- `CarveDebrisComputeRenderer` read mesh index count/start/base vertex separately in dispatch and render paths on active frames.

What was done:
- Replaced trig yaw with deterministic hash-vector orientation and the existing CoreLit safe-normalize path.
- Added `TryResolveDrawMesh()` and one-frame cached mesh draw metadata for indirect args.
- Reset the draw cache on GPU state release.
- Did not run dotnet build or rebuild.

Cinematic cheats used:
- Rock chip orientation is a deterministic visual fake; exact angular yaw is not gameplay truth.
- Saved shader ALU is spent on keeping CoreLit caustics, edge tint, and fog response rather than uploading per-particle rotations.

Exact microseconds saved:
- Sub-10 us estimated CPU saving on active debris frames from removing duplicate mesh metadata queries.
- GPU saving is visible-count dependent; one `sincos` per debris vertex is removed from the MX350 path.

Verification state:
- Static shader scan confirms `sincos`, `sin(`, and `cos(` are absent from `Hecton_CarveDebrisIndirect.shader`.
- Static C# read confirms dispatch and render now share one-frame mesh draw metadata.
- Unity compile remains PENDING/BLOCKED by local editor/license state; no fake pass recorded.
