# Rationale_DEBRIS_PHYSICS_FAKE

## Decision 1 - Missing Batch Prompt

Problem: The requested prompt ID `DEBRIS_PHYSICS_FAKE` has no `<AGENT_PROMPT>` block in `Docs/Tasks/CURRENT_BATCH.md`, so there is no authoritative task list, no task count, and no XML-scoped domain boundary.

Solution: Treat the task as blocked before implementation. The mandatory batch extraction was executed with a CLI regex, then cross-checked against `Docs/Tasks/CURRENT_BATCH_AUDIT_20260516.md`, which explicitly lists `DEBRIS_PHYSICS_FAKE` as missing from the active batch.

Rejected Alternatives: Implementing from the launcher list alone was rejected because it would synthesize a missing prompt. Reading archived batch logs was rejected because active hygiene rules forbid using previous batch material unless explicitly ordered.

Scalability potential: No runtime debris implementation was created. Low, Middle, High, and Ultra tiers remain undefined until the authoritative prompt exists.

Hardware Impact: 0 us runtime change on i3/MX350 because no gameplay/render code was changed.

## Decision 2 - No Source Edits

Problem: GPU-only debris chips would touch VFX/rendering code and likely signal ingestion. Without the XML task list, the write boundary and required integration points are undefined.

Solution: Leave source unchanged and record the blocker in `Status_DEBRIS_PHYSICS_FAKE.md` and this rationale file.

Rejected Alternatives: Editing existing `DebrisManager`, `HectonFluidEngine`, shader, or signal code based only on a one-line task was rejected because it risks colliding with active agents and violates the current batch prompt protocol.

Scalability potential: The desired solution should eventually be GPU-resident, signal-driven, and tiered from cheap billboard/triangle chips on low hardware to denser shader-lit shard fields on high hardware.

Hardware Impact: 0 us runtime change. No bandwidth, GC, CPU, or VRAM delta introduced.

## Decision 3 - Phase 1 CPU Debris Purge

Problem: Voxel carve aftermath still had a CPU debris path that registered dropped items per carve and a legacy laser path that called `IDebrisService.SpawnBurst`. This violates the XML rule: zero GameObjects and all debris rendered by indirect instancing.

Solution: Removed the dropped-item debris loop and the legacy transient SpawnBurst path from `VoxelDeltaProcessor`. Voxel carve, mining outcrops, drills, player CCD impacts, and vehicle CCD impacts now publish `DebrisSpawnSignal` with `FlagComputeShard`. The existing `CarveDebrisComputeRenderer` consumes the non-destructive signal snapshot and injects requests into the DataVault-backed compute path.

Rejected Alternatives: Pooling dropped-item GameObjects was rejected because it still burns transform, registry, and lifecycle CPU. Keeping `IDebrisService` as a fallback was rejected because it preserves a second debris authority and makes scene cleanup ambiguous.

Scalability potential: Low = capped signal scan and existing low-tier renderer capacity. Middle = GPU shards without SDF collision. High = denser compute shards with shader tumble. Ultra = expanded particle capacity and heavier material response without producer code changes.

Hardware Impact: Expected i3/MX350 gain is the removed per-carve dropped-item registration loop plus removed legacy SpawnBurst work. Runtime measurement is pending compile/runtime profiling; static estimate is tens to hundreds of microseconds saved on burst-heavy mining frames, depending on the previous `carveDebrisMaxCount` path.

## Decision 4 - Debris Service Registry Boundary

Problem: Bootstrap still treated `DebrisManager` as the debris runtime service, which can instantiate or preserve a GameObject-based service during startup even when GPU debris is the target authority.

Solution: Added `IDebrisComputeService` and `GlobalRegistryServiceSlot.DebrisComputeRuntime`. `CarveDebrisComputeRenderer` registers itself as the compute debris service. Bootstrap readiness and scene cleanup now target `GlobalRegistry.DebrisCompute` instead of creating or depending on the legacy `DebrisManager`.

Rejected Alternatives: Directly referencing the VFX renderer from bootstrap was rejected because it would hard-wire a scene component into core startup. Reusing `IDebrisService` for GPU debris was rejected because the old service contract exposes CPU debris semantics such as `ClearActiveDebris` and SpawnBurst-era ownership.

Scalability potential: Low = no bootstrap-created debris GameObject on weak devices. Middle = renderer self-registers only when present. High = future GPU debris variants can implement the same registry contract. Ultra = multiple visual implementations can be scene-selected without changing mining producers.

Hardware Impact: Expected i3/MX350 gain is removal of startup/lifecycle work for the legacy debris runtime and less scene teardown churn. Static estimate is small per frame but important during mining spikes because producers now publish one unmanaged signal instead of touching a service object.

## Decision 5 - Non-Destructive Signal Ingestion

Problem: `GlobalSignals.TryDequeueDebrisSpawn` is a destructive queue. If the GPU renderer consumed it directly, it could starve other systems or depend on execution order.

Solution: Mirrored `DebrisSpawnSignal` into `SignalBus<DebrisSpawnSignal>` during publish. `CarveDebrisComputeRenderer` reads the frame snapshot and only handles signals tagged with `FlagComputeShard`.

Rejected Alternatives: Draining the existing queue in VFX was rejected because it introduces order bugs. Adding direct calls from mining scripts to the renderer was rejected because it creates cross-domain coupling and breaks the multi-agent decoupling rule.

Scalability potential: Low = bounded scan of 64 debris spawn signals per frame. Middle = multiple producers can publish unmanaged signals without allocations. High = renderer-side capacity controls density. Ultra = high-tier renderer can read the same signal lane and expand injection quality.

Hardware Impact: Expected i3/MX350 gain is indirect: signal fanout avoids per-producer service lookups and avoids CPU object handoff. The added snapshot push is unmanaged and fixed-capacity; microsecond cost is expected below the old CPU debris branch and must be validated in runtime profiling.

## Decision 6 - Compile Blocker Boundary

Problem: `dotnet build Hecton8.Core.csproj` is blocked by external domain dependency drift. First compile stopped in `Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs` on unresolved bite IK types. After concurrent worktree movement, the next compile stopped on docking autopilot, VFX wake, and ecosystem macro swarm contracts: unresolved `IDockingAutopilotService`, `ActiveSplineData`, `WakeSource`, `WakeTelemetryEntry`, plus new `IEcosystemDirectorService` members not implemented by `EcosystemDirector`.

Solution: Do not edit fauna code under the debris prompt. Record the compile blocker and leave the Phase 1 debris purge intact. The targeted debris static scan found no remaining legacy CPU debris calls in the modified purge path.

Rejected Alternatives: Patching fauna, docking, wake, or ecosystem types from the debris task was rejected because it would cross into other agent domains without an interface-level debris reason. Reverting those changes was rejected because they were not authored by this agent and may be active work.

Scalability potential: Low/Middle/High/Ultra debris scalability remains unaffected by this external build break; runtime profiling is blocked until the fauna compile wall is resolved by its owner or integrator.

Hardware Impact: 0 us direct debris runtime impact from this decision. Compile validation is blocked externally, so measured microsecond proof for Phase 1 must wait.

## Decision 7 - Multiplatform ABI Lockdown

Problem: Debris request and telemetry structs were sequential without explicit packing or size. On ARM64/Quest and IL2CPP, implicit padding differences can corrupt DataVault reads or blackbox dumps.

Solution: Locked `CarveDebrisRequest` and `CarveDebrisTelemetryEntry` to `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]`. `DebrisSpawnSignal` and `VoxelCarveEvent` are already fixed-size packed/explicit signals. Data remains in `GlobalDataVault` buffers; local `NativeArray` fields in the renderer are leases, not owned allocations.

Rejected Alternatives: Leaving CLR default packing was rejected because Quest/Android cannot be trusted to preserve desktop assumptions. Moving to managed classes was rejected because it breaks Burst and DataVault sovereignty.

Scalability potential: Low = predictable 64B request/telemetry cache lines. Middle = same data contract with 4096 shards. High = same ABI scales to 16,384 shards. Ultra = extra visual passes can read identical buffer layout without producer changes.

Hardware Impact: Static estimate is 0 us direct frame gain; this prevents platform-only memory faults and keeps DataVault inspection deterministic.

## Decision 8 - H-Phi SoA And Tier Split

Problem: The debris path still behaved like a middle-ground 4096-particle system. It did not expose the XML-named `_DebrisBuffer`/`_DebrisPhysicsBuffer` contract and did not buy high-end visuals with low-tier savings.

Solution: Kept the authoritative DataVault buffers `BufferID.CarveDebris` and `BufferID.CarveDebrisVelocity`, bound them to shader names `_DebrisBuffer` and `_DebrisPhysicsBuffer`, and split capacity by tier: 1024 low, 4096 mid, 16,384 high/ultra. Per-carve injection now scales 16/48/128 by tier.

Rejected Alternatives: A single balanced capacity was rejected because HECTON-8 requires toaster mode and God-mode, not a compromise. Interleaved particle structs were rejected because float4 SoA is cheaper for compute and shader reads.

Scalability potential: Low = Dear Lie with 1024 shards and skipped SDF/flow. Middle = 4096 visible chips without RTX assumptions. High = 16,384 shards with SDF/flow and improved lighting. Ultra = same contract can carry future POM/SSS material overkill.

Hardware Impact: Low-tier static estimate: 0.3-1.2 ms GPU avoided under debris storms. High tier spends the saved CPU path on 4x previous maximum shard density.

## Decision 9 - Shader-Only Tumble And STP Motion Vectors

Problem: Debris orientation was static per particle ID and render params forced no motion vectors, producing procedural shimmer risk under STP/upscalers.

Solution: Moved tumble to the vertex shader using particle ID plus time. Added a MotionVectors pass that reconstructs previous position from `_DebrisPhysicsBuffer.xyz * deltaTime`, and changed render params from `ForceNoMotion` to object motion.

Rejected Alternatives: CPU quaternion updates were rejected because they reintroduce per-shard transform work. Leaving motion vectors off was rejected because it hides cost while degrading STP stability.

Scalability potential: Low = shader hash tumble without CPU cost. Middle = stable motion history for 4096 shards. High/Ultra = deterministic tumble remains cheap at 16,384 shards and supports heavier material lighting later.

Hardware Impact: Static estimate is 20-90 us CPU avoided versus CPU-side rotation for thousands of shards. Motion-vector GPU cost is pending capture; image stability is the intended spend.

## Decision 10 - Homeostasis And SetData Stall Removal

Problem: Stress adaptation and SetData stall defense were incomplete as explicit task evidence.

Solution: Debris lifetime decay now multiplies by 4 when `SystemStress01 > 0.9`, shortening lifetime by 75% and recycling slots faster. The upload path uses `GraphicsBuffer.LockBufferForWrite` on double-buffered position/velocity buffers and contains no `GraphicsBuffer.SetData` hot path.

Rejected Alternatives: Dropping all producer signals during stress was rejected because it removes tactile feedback. Full-buffer `SetData` was rejected because it can stall the main thread and create MicroSD-like hitch symptoms during streaming pressure.

Scalability potential: Low = pressure recycles shards aggressively. Middle = stable buffer uploads. High/Ultra = high density remains bounded by capacity and lifetime pressure.

Hardware Impact: Static estimate: stress mode reduces active particle residence by 4x. SetData avoidance prevents intermittent sync spikes; exact microseconds require GPU/main-thread profiling after external compile blockers are cleared.

## Decision 11 - Omega Compute Branch Audit

Problem: After the core checklist was complete/blocked, the Omega mandate required a second debris-kernel audit. The carve debris path still needed proof that branch removal would not create a worse MX350 path, and mid-tier dispatch had to avoid spending high-tier work only to discard threads by capacity guard.

Solution: Kept correctness branches only where they prevent out-of-range writes, avoid unnecessary atomics, or preserve the low-tier SDF skip. Confirmed `ClampCarveDebrisVelocity` uses `rcp(max(dt, 0.0001))`, `rsqrt(max(speedSq, 0.000001))`, and `step`/`lerp` instead of a speed branch. Confirmed carve invalid-state handling collapses non-finite particles with masks before GPU writes. Confirmed cull distance gating uses `step`/`lerp` and visible increment masking. Confirmed dispatch groups are resolved from the active tier capacity, so middle tier no longer burns high-tier thread count.

Rejected Alternatives: Fully branchless SDF collision was rejected because it would call the SDF helper on the low-tier path and violate the XML low-tier skip. InterlockedAdd with zero for invisible particles was rejected because it replaces a cheap visibility branch with atomic pressure. Rewriting shared silt/bubble/fluid helper branches was rejected because those kernels are outside the debris prompt and would risk cross-domain regressions.

Scalability potential: Low = SDF skip and 1024-thread dispatch remain intact. Middle = 4096-capacity dispatch avoids the old high-tier 16,384-thread sweep. High = 16,384 particles keep full SDF/flow and motion-vector render path. Ultra = same SoA buffers can feed heavier material shading without producer churn.

Hardware Impact: Measured proof absent because Unity/runtime profiling is blocked by external compile errors. Static estimate: middle tier avoids dispatching 12,288 extra threads per debris advection/cull pass compared with a 16,384-thread sweep. Low-tier preserves the SDF skip instead of paying helper-branch cost.

## Decision 12 - Low-Tier Wake Bypass And Vault Handle Boundary

Problem: The carve debris compute path set low-tier flow to zero, but still called `ApplyDynamicWakes(previousPosition, flow)`. On MX350/Quest this could execute the dynamic wake loop even when the debris tier was supposed to skip flow-heavy work. The H-Phi audit also found private `NativeArray<T>` storage fields in the renderer, which made the system look stateful even though the buffers were DataVault-owned.

Solution: Changed `AdvectCarveDebris` so low tier bypasses both `SampleAbyssalFlow` and `ApplyDynamicWakes`; non-low tiers still get flow and wake response. Replaced persistent debris `NativeArray<T>` fields with `VaultBufferHandle<T>` fields, then resolved method-local `NativeArray<T>` views only inside the active tick. This satisfies data-sovereignty visibility without introducing local native allocations.

Rejected Alternatives: A branchless wake mask was rejected because HLSL would still evaluate `ApplyDynamicWakes` and pay the loop. Keeping private `NativeArray<T>` fields was rejected after the renewed H-Phi audit because it fails the grep-level ownership rule. Editing `GlobalDataVault` to add a no-sanitize hot resolve was rejected as cross-domain core memory work outside this prompt.

Scalability potential: Low = no flow texture sample and no dynamic wake loop in carve debris advection. Middle = wakes remain available for 4096 shards. High/Ultra = wakes, SDF collision, shader tumble, and motion vectors remain active for 16,384 shards.

Hardware Impact: Measured proof absent for runtime frame cost. Static low-tier work avoided: one dynamic-wake helper call per live carve debris thread, up to the 1024 low-tier capacity, including up to `HECTON_DYNAMIC_WAKE_CAPACITY` wake-slot checks inside that helper when wakes are active. Handle resolution has potential full-payload sanitize cost; compile is clean, profiler capture is still required before claiming microsecond savings for that refactor.

## Decision 13 - Final Validation Gate

Problem: The status file still carried stale external compile-block state after the debris handle migration and low-tier compute pass.

Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`. The build succeeded with 0 warnings and 0 errors at that moment. This was later superseded by a fresh external compile wall recorded in Decision 14.

Rejected Alternatives: Leaving `[BLOCKED BY DEPENDENCY]` in status was rejected because the latest compiler evidence supersedes the earlier drift. Reporting runtime microseconds was rejected because only compilation and static source validation were performed, not Unity profiler capture.

Scalability potential: Low remains 1024 shards with wake/SDF bypass. Middle remains 4096 shards with active-capacity dispatch. High/Ultra remain 16,384 shards with wake, SDF, shader tumble, motion vectors, and indirect rendering.

Hardware Impact: 0 us directly saved by the compile gate. It proved the debris source was buildable before later external domain drift re-broke the project.

## Decision 14 - External Tools Compile Wall

Problem: A later `dotnet build` no longer fails in debris. It now fails in external Bootstrap/Tools ownership: `GameBootstrapper.Initialize` call arity mismatch and `ToolDurabilitySystem` references to missing private data-vault fields plus missing `DurabilityDecayJob.BreakdownWriter`.

Solution: Stop at the dependency boundary and mark task 18 `[BLOCKED BY DEPENDENCY]` with the latest compiler evidence. The only small syntax blocker seen before this pass, `InputDispatcher.cs` preprocessor placement, is no longer the reported compiler failure.

Rejected Alternatives: Repairing the full durability system from the debris prompt was rejected because it would be cross-domain implementation, not a debris interface fix. Reporting the previous clean build as current truth was rejected because the latest compiler run is authoritative.

Scalability potential: Debris scalability is unchanged: low stays 1024 no-wake/no-SDF, middle stays 4096 active dispatch, high/ultra stay 16,384 with wake/SDF/tumble/motion vectors.

Hardware Impact: 0 us direct debris runtime impact. External compile drift blocks runtime profiling, so no new measured microsecond claims are made.

## Decision 15 - Final Compile Green After External Wall Moved

Problem: Task 18 was still marked blocked by external compile drift. A fresh compiler pass was required before claiming any final validation state.

Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`. Current workspace compiled successfully to `Temp\bin\Debug\Hecton8.Core.dll` with 0 warnings and 0 errors. Task 18 can be marked done.

Rejected Alternatives: Keeping the stale `[BLOCKED BY DEPENDENCY]` marker was rejected because current compiler evidence supersedes it. Claiming Unity runtime, Quest, Android, Metal, or profiler proof was rejected because this pass only proves the C# project compile gate.

Scalability potential: Debris source remains tiered: low/MX350 uses 1024 shards with wake/SDF bypass; middle uses 4096 active-capacity dispatch; high/ultra use 16,384 shards with wake, SDF, shader tumble, motion vectors, and indirect rendering.

Hardware Impact: 0 us directly saved by the compile gate. It removes validation blockage and allows actual Unity/profiler capture next; no measured runtime microseconds are claimed.

## Decision 16 - Multiplatform Static Inquisition Recheck

Problem: A later instruction required a fresh ARM64/Quest, Metal, Steam Deck I/O, H-Phi, signal lane, NaN, and blackbox audit after the prior compile-green entry. The status file only recorded the older 4.22s compile pass and did not show the newest targeted debris scan.

Solution: Re-ran the DEBRIS XML extraction, re-ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`, and re-ran targeted debris scans for local native storage, instantiation, `GraphicsBuffer.SetData`, disabled motion vectors, standard `Update()`, string formatting, legacy bus traffic, managed delegates, and singleton debris access. The compile passed with 0 warnings and 0 errors in 58.78s. The static scan returned no forbidden debris-domain matches. Shader audit confirmed carve debris thread groups are 64 or 1, below the 1024 group ceiling.

Rejected Alternatives: Reporting the older compile pass was rejected because the user explicitly requested current memory and no stale truth. Editing outside VFX/debris after a clean build was rejected because cross-domain compile defects are not currently present. Claiming Quest/Android/Metal/player build proof was rejected because only C# compile and static shader/source scans were run.

Scalability potential: Low/MX350 remains 1024 shards with wake/SDF bypass and bounded signal ingestion. Middle remains 4096 active-capacity dispatch. High remains 16,384 shards with SDF, wake response, shader tumble, motion vectors, and indirect rendering. Ultra can spend the same GPU-only SoA path on denser material/detail work without adding producer CPU cost.

Hardware Impact: Measured microsecond gain is still not claimed. Static risk reduction: no GameObject debris instantiate path in the audited debris target set; no hot-path `SetData` stall path; no private debris `NativeArray<T>` ownership fields; no shader thread-group above Metal/Quest limits.

## Decision 17 - Shader Omega Mask Polish

Problem: The static shader audit still found small normalization and basis-selection ternaries in carve debris shader code. These were not the expensive SDF/wake skip branches, so they could be removed without damaging MX350 behavior.

Solution: Replaced debris shader safe-normalize ternaries with `step` + `lerp` + `rsqrt(max(lengthSq, epsilon))`. Replaced both forward and motion-vector debris basis-up selection ternaries with `step` + `lerp`. Replaced the dynamic wake low-tier slot-cap ternary with `step` + `lerp` while preserving the current shader-source wake capacity values. Re-ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`; it passed with 0 warnings and 0 errors in 5.61s.

Rejected Alternatives: Removing the carve debris SDF/wake skip guard was rejected because it would execute `SampleAbyssalFlow`, `ApplyDynamicWakes`, or `IsSolidVoxel` on low-tier paths and violate the MX350 Dear Lie requirement. Removing finite-state ternaries was rejected because shader boolean-to-float casts are less portable than the current explicit NaN kill path.

Scalability potential: Low remains protected from unnecessary wake/SDF work. Middle and High keep the same visible behavior with slightly more ALU-predictable normalization and basis construction. Ultra retains the same indirect draw and motion-vector path.

Hardware Impact: No measured microsecond claim. Static improvement: removes branch-like helper selection in render/motion-vector basis and dynamic wake cap selection without expanding low-tier helper evaluation.

## Decision 18 - High-Tier Debris Material Overkill

Problem: The high tier had density, SDF bounce, wake response, shader tumble, and motion vectors, but the material response was still too close to the middle-tier shard look. The prompt explicitly requires spending saved cycles on high-end visual excess without charging MX350/Quest.

Solution: Added a high-tier-only procedural crystal/strata mask in `Hecton_CarveDebrisIndirect.shader` using triangle-noise-style banding and hash masks. The high-tier fragment path perturbs normals, sharpens edge response, and adds a subtle crystal rim contribution. `CarveDebrisComputeRenderer` now resolves the high-tier flag once, binds it to the material, enables receive shadows on high tier, and forces shadow casting on high tier when the serialized debris shadow mode is Off.

Rejected Alternatives: Adding texture assets, 16-tap POM, or full SSS immediately was rejected because Unity shader import is currently blocked externally and new assets would increase validation surface. Applying the effect to all tiers was rejected because low-tier must remain a Dear Lie. CPU-side shard material randomization was rejected because it violates the GPU-only shard path.

Scalability potential: Low = unchanged cheap baseline, no crystal branch work beyond the tier test. Middle = unchanged 4096-shard path. High = denser 16,384 shards with extra material grain, edge sparkle, and shadows. Ultra = same hook can later feed POM/SSS texture detail once the project imports cleanly.

Hardware Impact: No measured microseconds are claimed. Static impact is an intentional high-tier GPU ALU spend only; low-tier particle cap, wake/SDF bypass, and cheap material path remain unchanged.

## Decision 19 - Unity Import Validation Wall

Problem: `Hecton8.Core.csproj` passes with non-shared compilation, but that project does not include the debris assembly. Attempts to validate the actual Unity debris assembly hit external dependency walls before debris proof: `Assembly-CSharp.csproj` is missing RealtimeCSG source files, direct Bee debris compilation lacks valid generated refs, and Unity batch import fails in Audio/Editor asmdef/reference resolution.

Solution: Recorded task 18 as `[BLOCKED BY DEPENDENCY]` for Unity import/player/shader validation while preserving the current core C# pass and debris static evidence. The Unity import log is kept at `Docs/AgentLogs/UnityImport_DEBRIS_PHYSICS_FAKE.log`. No runtime, platform-player, GPU profiler, or shader-import success is claimed.

Rejected Alternatives: Faking completion from the core-only compile was rejected. Replacing Bee refs with incompatible dlls was rejected after duplicate `IDataVault`/`VaultBufferHandle<T>` types appeared. Repairing Audio, Editor tools, RealtimeCSG, or SaveSystem references from the debris prompt was rejected because those are outside the VFX/Debris domain and would collide with other agents.

Scalability potential: Debris tier behavior remains intact by source: low/MX350 1024 shards with no wake/SDF, middle 4096 active-capacity dispatch, high/ultra 16,384 with wake/SDF/tumble/motion vectors and high-tier material overkill. Runtime platform proof is blocked until external assembly import is repaired.

Hardware Impact: 0 us direct runtime change from this validation decision. It prevents false microsecond reporting and preserves a clean boundary for the integrator.

## Decision 20 - Global Wake Param Mirror And Blackbox Wake Flag

Problem: The carve debris compute shader now consumes the global wake array contract (`_GlobalWakeBuffer`, `_GlobalWakeVectors`, `_GlobalWakeParams`), but the renderer did not explicitly mirror `_GlobalWakeParams` into its compute dispatch. That risks stale zero wake params on backends where global compute state is not reliably visible, and the blackbox could not distinguish flow-field response from wake response.

Solution: Added a debris-owned `_GlobalWakeParams` property ID, resolved the global wake params once per dispatch, clamped the slot limit to the shader's 16-slot capacity, and forced a zero-wake parameter block on low tier. Added `WakeActiveFlag` to the debris blackbox flags and reset `_blackBoxDumped` whenever the DataVault-backed telemetry ring is cleared.

Rejected Alternatives: Copying `_GlobalWakeBuffer` and `_GlobalWakeVectors` into private debris arrays was rejected because it would duplicate wake data and violate the shared global lane. Reading global vector arrays back into C# was rejected because it would allocate or mirror data that another domain already owns. Forcing wakes on low tier was rejected because MX350/Quest must keep the Dear Lie path.

Scalability potential: Low = no wake loop, explicit zero params. Middle = bounded 16-slot wake response only when the global wake publisher provides active slots. High/Ultra = debris shards can visibly react to wake turbulence while the blackbox records when that path was active.

Hardware Impact: No measured microseconds are claimed. Static low-tier impact is preserved at zero wake work. Middle/high add one scalar global-param read on CPU and one compute uniform set; the wake loop remains bounded to 16 slots and only runs off low tier.
