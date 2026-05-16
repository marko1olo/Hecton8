# LOG_AMBIENT_BIOTA_DIRECTOR

## 2026-05-16 - Prompt Extraction Blocker

What was wrong: `AMBIENT_BIOTA_DIRECTOR` is listed in the companion launcher instruction file, but the active `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="AMBIENT_BIOTA_DIRECTOR">`. The batch audit confirms this ID is missing and states missing prompts must not be invented or synthesized.

What was done: Read `AGENTS.md`, `Docs/Actual Domains of Project.txt`, extracted/search-validated the batch with PowerShell CLI, read eight relevant `.agents-skills` mandates, created `Docs/Tasks/Status_AMBIENT_BIOTA_DIRECTOR.md`, created `Docs/AgentLogs/Rationale_AMBIENT_BIOTA_DIRECTOR.md`, and recorded this log.

Cinematic Cheats used: None implemented. Future authorized ambient biota work should prefer deterministic spawn fakes, pooled presentation objects, GPU/instanced visual density, and Math LOD instead of simulating every background organism as gameplay truth.

Exact Microseconds saved: 0 us runtime. No code path was changed. Blocking prevents unauthorized runtime cost from unscoped spawning/pooling logic.

Verification: No compilation attempted because no source code was modified and no authorized task list exists.

## 2026-05-16 - Phase 1 Great Purge / Loop 1

What was wrong: `AMBIENT_BIOTA_DIRECTOR` prompt was injected after the initial blocker. Phase 1 required removing ambient biota object-spawn patterns and replacing them with a GPU-ready stream. Existing runtime scans found no direct `AmbientLifeManager.Instance` and no direct ambient/fish `Object.Instantiate`, but there was also no authoritative `AI/Ambient` SOA service for consumers to bind.

What was done: Added `SystemID.AmbientBiota`; added `BufferID.BiotaAUPs`, `BufferID.BiotaVelocities`, and `BufferID.BiotaStates`; added `AmbientBiotaState` and `IAmbientBiotaService`; registered `GlobalRegistry.AmbientBiota`; created `Assets/_Project/Scripts/AI/Ambient/Hecton8.AI.Ambient.asmdef` with direct `Hecton8.Core`, `Hecton8.Core.Contracts`, and `Hecton8.Core.Memory` references; implemented `AmbientBiotaDirector` with DataVault-backed AUP/velocity/state arrays, deterministic Burst spawn, modulo-bucket drift, biomass gating, abyssal-flow input, and late-frame job completion. No `Instantiate`, `Random.Range`, `foreach`, or Unity `Update` exists in the new ambient domain.

Cinematic Cheats used: Background organisms are treated as SOA visual particles with deterministic Brownian noise and flow-field advection, not physical agents. Low tier marks slots as billboard-ready and runs 1/16 drift buckets. High/Ultra can spend the same contract on larger capacity and later indirect GPU draw variation.

Exact Microseconds saved: 2000-8000 us estimated spawn-spike avoidance per 64-object burst versus `Instantiate`; 1-3 us/frame estimated singleton/scene-path avoidance after consumers bind `GlobalRegistry.AmbientBiota`; 50-150 us estimated cold realloc avoidance after DataVault residency; 300-1200 us/frame estimated avoided versus per-fish MonoBehaviour transform drift at equivalent visible density. Profiler confirmation is blocked by project-wide compile errors.

Verification: Static scans passed for forbidden patterns in `Assets/_Project/Scripts/AI/Ambient` and touched Core contracts. `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false` failed outside this task on unrelated project-wide errors: missing `JobAdmissionLane` references, missing `HectonShaderGlobalDataVaultBridge`, missing `GlobalSignals` signal types, missing voxel-debris constants, and stale generated project references. Compile status recorded as `[BLOCKED BY DEPENDENCY]` in `Docs/Tasks/Status_AMBIENT_BIOTA_DIRECTOR.md`.

## 2026-05-16 - Loop 2/3 Data Sovereignty, NaN Guard, Blackbox

What was wrong: The first ambient director pass still owned private `NativeArray` fields and a local macro counter allocation. That failed the data-sovereignty requirement, left no 300-frame ambient blackbox, and did not yet cover AUP double precision, low-tier fake math, high-tier light reaction, or reactive organic debris.

What was done: Replaced director-owned arrays with `VaultBufferHandle<T>` for AUPs, velocities, states, macro hydration counters, telemetry ring, and telemetry cursor. Reworked drift/spawn/dehydrate distance gates to use `double3` AUP deltas. Added modulo bucket enforcement through `ISimulationBucketer`, low-tier triangle-noise billboard motion, high-tier headlight flee/emission panic, finite guards around dt/velocity/rsqrt/AUP math, persistent velocity motion-vector source, bounded organic `DebrisSpawnSignal` emission on expiry, and a vault-owned 300-entry blackbox that dumps `Dump_AMBIENT_BIOTA_DIRECTOR.bin` on sanitized faults.

Cinematic Cheats used: Low tier uses ring offsets, triangle noise, billboard flags, and 30 m stress-radius culling instead of collision or steering. High/Ultra spend the saved CPU on larger visible bubble, richer scale/emission, and headlight panic rather than physical fish simulation.

Exact Microseconds saved: No profiler run was possible. Engineering estimates remain: 70-90% drift CPU avoided by 1/16 modulo buckets versus full sweep; 15-40 us/frame low-tier savings by skipping high-tier reaction math; 2000-8000 us spawn-spike avoidance per 64-object burst versus `Instantiate`; under 2 us/late-frame expected for the fixed telemetry write. High-tier overkill intentionally spends an estimated 20-60 us/frame on the active bucket for visible reactivity.

Verification: Static audit in `Assets/_Project/Scripts/AI/Ambient` found no `Instantiate`, `Object.Instantiate`, `Random.Range`, `Update(`, `foreach`, `string.Format`, `EventBus`, direct `H8Memory.Allocate`, or private `NativeArray` fields. `git diff --check` passed for the ambient file. Compile gate is currently `[BLOCKED BY DEPENDENCY]`: `Docs/AgentLogs/Dump_AMBIENT_BIOTA_DIRECTOR_BUILD.txt` records unrelated errors in `World/SargassumMicroFaunaBoids.cs` and `RepairTool.cs`. The ambient asmdef Bee response file exists, but its `Hecton8.Core.ref.dll` reference is stale/missing while the core project build is blocked by those foreign errors.

## 2026-05-16 - Loop 4 Indirect Draw And Biome Sync

What was wrong: The SOA stream existed, but there was no renderer bridge for `Graphics.RenderMeshIndirect`, and biome identity was not influencing species/emission. A hidden correctness bug also remained: macro-hydrated state wrote arbitrary swarm hashes into the `Reserved` flag field.

What was done: Added optional indirect rendering inside `AmbientBiotaDirector`: persistent GPU buffers for AUPs, velocities/motion vectors, states, one indirect args buffer, fallback quad mesh, and one `Graphics.RenderMeshIndirect` call. Added existing typed `BiomeChangedSignal` snapshot consumption through `ReadOnlySpan<BiomeChangedSignal>` and folded the biome hash into deterministic species/emission selection. Cleared active-state `Reserved` to flags-only so debris/fault flags cannot be spoofed by swarm hashes.

Cinematic Cheats used: Low tier can render simple vertex-animated billboard quads from velocity/state buffers. High/Ultra can let the material interpret the same buffers for pale abyssal jellyfish, colorful plankton, panic biolume, and dense organic soup without CPU matrix generation.

Exact Microseconds saved: No profiler run. Expected win is removal of per-instance matrix construction and transform submission; the remaining CPU work is three bulk buffer uploads and one indirect draw. Biome snapshot scan is slow-path and expected below 1 us when no transition signals exist.

Verification: Static ambient scan still passes with no forbidden hot-path patterns. `git diff --check` passes. Full compile remains blocked by unrelated `World/SargassumMicroFaunaBoids.cs` and `RepairTool.cs` errors in `Docs/AgentLogs/Dump_AMBIENT_BIOTA_DIRECTOR_BUILD.txt`.

## 2026-05-16 - Loop 5 Omega Polish And Final Validation

What was wrong: Omega polish was not complete. `AmbientBiotaDriftJob.Execute` still had branch source in the advection kernel, and final validation was still carrying an older project-wide compile wall.

What was done: Re-read the `AMBIENT_BIOTA_DIRECTOR` XML block from `Docs/Tasks/CURRENT_BATCH.md`, applied the Omega mandate to the drift kernel, and replaced early branch exits with mask-driven `math.select` decisions. Added fixed selectors for AUP/state/double3 payloads, made normalization and clamp finite guards branchless, kept `Reserved` as flags-only, and reran the static forbidden-pattern audit. Reran `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false`; it succeeded with 0 warnings and 0 errors.

Cinematic Cheats used: Low tier remains a deterministic billboard soup: triangle noise, ring spawning, 30 m stress cull, no collision. High/Ultra retain light avoidance, panic emission, larger density, biome-colored species families, and indirect GPU presentation without CPU matrices.

Exact Microseconds saved: Exact measured microseconds are unavailable; no Unity Profiler/GCMonitor run occurred. Engineering estimates unchanged: `Instantiate` burst avoidance 2000-8000 us per 64-object burst, modulo bucket drift reduction 70-90% versus full sweep, telemetry under 2 us/late frame, and indirect draw replacing per-instance matrix submission. Branchless Omega pass is a determinism/compliance tradeoff, not a measured frame-time claim.

Verification: `AmbientBiotaDriftJob.Execute` has no `if (` source after the Omega pass. `Assets/_Project/Scripts/AI/Ambient` has no forbidden ambient hot-path patterns from the static scan. `git diff --check` passed with CRLF warnings only. Final compile log is `Docs/AgentLogs/Dump_AMBIENT_BIOTA_DIRECTOR_BUILD.txt`: build succeeded, 0 warnings, 0 errors. Unity Editor import, Play Mode, GCMonitor, Frame Debugger, RenderDoc, and player-build proof remain pending.

## 2026-05-16 - Loop 6 Multiplatform GPU Bandwidth Pass

What was wrong: The ambient indirect renderer still used `GraphicsBuffer.SetData` for full-capacity AUP, velocity, state, and indirect-args uploads. That is unacceptable for Steam Deck/MX350 bandwidth discipline and violates the `LockBufferForWrite` upload rule.

What was done: Replaced single GPU payload buffers with double-buffered A/B lanes. Uploads now write to the non-current buffer with `LockBufferForWrite`, copy from vault-resolved `NativeArray` views through `UnsafeMemoryCopyGuard`, and swap the read index only after all SOA payload streams succeed. Indirect args now use a locked write and update only when mesh/capacity changes. The ambient asmdef now enables unsafe code for the explicit native copy path.

Cinematic Cheats used: Low tier remains a cheap deterministic visual fake: billboard flags, triangle noise, no collision, stress radius clamp. High/Ultra keep light avoidance, panic emission, biome-tinted species families, and indirect rendering from the same buffers.

Exact Microseconds saved: No profiler measurement was run. Expected gain is reduced upload/synchronization overhead versus full `SetData` every late frame. Runtime file I/O added by this pass is 0 B/frame; all rendering payload data comes from DataVault buffers already in memory.

Verification: Static ambient scan found no `SetData`, `private NativeArray`, direct `H8Memory.Allocate`, `Update`, `LateUpdate`, `FixedUpdate`, `foreach`, `string.Format`, `Instantiate`, `Random.Range`, legacy `EventBus`, managed delegate patterns, `Camera.main`, scene find, coroutine, or `Resources.Load`. `AmbientBiotaDriftJob.Execute` still has no `if (` source. `git diff --check` passed with CRLF warnings only. The Loop 6 `dotnet build` exited 0 with 1 warning and 0 errors. The warning was outside ambient: `CS2002` duplicate source include for `Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs`. Loop 7 supersedes this with the current foreign dependency wall.

## 2026-05-16 - Loop 7 Bee Boundary And Args Double Buffer

What was wrong: The previous validation relied too heavily on the generated `Hecton8.Core.csproj`. The actual Unity asmdef boundary for `Hecton8.AI.Ambient` is Bee/Roslyn, and that path still lacks `Hecton8.Core.ref.dll`. The indirect renderer also still had a single indirect-args buffer while the payload streams were double-buffered.

What was done: Re-read the `AMBIENT_BIOTA_DIRECTOR` XML block. Ran direct Bee response-file validation for `Hecton8.AI.Ambient`, then attempted to rebuild the missing dependency chain. The wall is outside `AI/Ambient`: Bucketing cannot see `GlobalRegistry`, Scheduling references missing `Lane2AI`/`Lane3Physics`, Audio Virtualization has missing core refs and `VirtualVoice` unmanaged constraint errors, and the current generated global build fails in Diagnostics/Audio/World. Inside the ambient domain, replaced the single indirect-args buffer with A/B `GraphicsBuffer` lanes and pass the resolved read args buffer into `Graphics.RenderMeshIndirect`.

Cinematic Cheats used: No new physical simulation. Low tier still uses deterministic billboard/triangle-noise fakery; High/Ultra keep biolume panic, biome coloration, and GPU-side visual density. The args change is a synchronization cleanup so the existing visual overkill path remains GPU-fed.

Exact Microseconds saved: No measured microseconds. Expected effect is lower GPU/CPU sync risk on mesh/capacity changes, not a large frame-time number. Runtime managed allocation remains 0 B/frame for this change.

Verification: Static ambient scan found no `SetData`, `private NativeArray`, direct `H8Memory.Allocate`, `Update`, `LateUpdate`, `FixedUpdate`, `foreach`, `string.Format`, `Instantiate`, `Random.Range`, legacy `EventBus`, managed delegate patterns, `Camera.main`, scene find, coroutine, or `Resources.Load`. `AmbientBiotaDriftJob.Execute` still has no `if (` source. `git diff --check` passed with CRLF warnings only. Current `dotnet build` is `[BLOCKED BY DEPENDENCY]` with 7 non-ambient errors recorded in `Docs/AgentLogs/Dump_AMBIENT_BIOTA_DIRECTOR_BUILD.txt`.

## 2026-05-16 - Loop 8 Hot-Path And Blackbox Polish

What was wrong: Ambient `Tick` could still fall back to `GlobalRegistry` lookups when cached dependencies were null. Macro hydrate/dehydrate calls changed vault SOA data without marking GPU buffers dirty. High-tier light panic was sticky. Telemetry wrote a simulation seed frame index instead of a heartbeat index and reported raw cull count as `CullRatePerSecond`.

What was done: Moved missing dependency recovery to `RefreshRegistryDependencies()` on cold/slow paths and verified `Tick(float deltaTime)` has no `GlobalRegistry.` access. Marked GPU payload dirty after macro hydrate/dehydrate recounts. Made high-tier reactive flag and emission decay branchlessly when the headlight cone no longer applies. Added shader parameters for quality profile, system stress, flow vector, and overkill mode. Added a dedicated heartbeat frame counter and finite elapsed-time cull-rate calculation.

Cinematic Cheats used: Low tier remains billboard/triangle-noise fakery. High/Ultra now have explicit shader knobs for flow/stress/overkill and reversible biolume panic, enabling denser visual treatment without gameplay physics or CPU matrices.

Exact Microseconds saved: No measured microseconds. Expected gain is removal of possible per-frame registry probes under missing dependency conditions and avoiding unnecessary corrective GPU uploads after stale states. New scalar telemetry math and material parameter writes are expected below measurable threshold without Unity Profiler proof.

Verification: Static ambient scan remains clean for `SetData`, `private NativeArray`, direct `H8Memory.Allocate`, Unity `Update`, `foreach`, `string.Format`, `Instantiate`, `Random.Range`, legacy `EventBus`, managed delegate patterns, scene searches, coroutine, and `Resources.Load`. `AmbientBiotaDriftJob.Execute` still has no `if (` source. `Tick` has no `GlobalRegistry.` access. `git diff --check` passed with CRLF warnings only. Current `dotnet build` is blocked outside ambient by `TetherManager.cs(264,58): CS0426 TetherSignals.TetherFireRequest does not exist`. Direct ambient Bee compile remains blocked by missing `Hecton8.Core.ref.dll`.

## 2026-05-16 - Loop 9 Job-Fence And Hot-Resolve Purge

What was wrong: Ambient resolve helpers still called `EnsureVaultBuffers()`, so `Tick`/`LateFrameTick` could hide structural DataVault handle work behind simple view resolution. Public macro service calls also needed proof they no longer force `CompleteActiveJob()` mid-frame. The stress/radius path still read the global stress scalar from frame cadence instead of using the slow quality policy cache.

What was done: Removed `EnsureVaultBuffers()` from `TryResolveBiotaBuffers`, `TryResolveMacroCounters`, and `TryResolveTelemetryBuffers`; they now fail fast unless cold/slow setup already created handles. Kept structural vault handle creation in `OnEnable` and `SlowTick`, with `_jobPending` checked before capacity changes. Verified `TryHydrateMacroSwarms` and `TryPackMacroHydratedBiota` have `_jobPending` fail-fast guards and no `CompleteActiveJob()`. Added `_cachedSystemStress01`, finite-clamped in `RefreshQualityPolicy()`, and used it for simulation radius and indirect material stress.

Cinematic Cheats used: No physical simulation was added. Low tier remains a billboard/triangle-noise DataVault stream with stress radius clamp. High/Ultra keep the same SOA/GPU buffers but can spend shader-side cycles on flow/stress/overkill visuals without CPU matrix generation or per-object truth.

Exact Microseconds saved: No measured microseconds. Expected impact is removal of rare hidden structural stalls from hot resolve paths and removal of one frame-cadence global stress read. Macro service calls now avoid forced job completion, preventing potential main-thread synchronization spikes; no profiler number is claimed.

Verification: Static forbidden-pattern audit in `Assets/_Project/Scripts/AI/Ambient` found no `SetData`, `private NativeArray`, direct `H8Memory.Allocate`, Unity `Update`, `foreach`, `string.Format`, `Instantiate`, `Random.Range`, legacy `EventBus`, managed delegate patterns, scene search, coroutine, or `Resources.Load`. `Tick(float deltaTime)` contains no `GlobalRegistry.`, no `EnsureVaultBuffers()`, and no `GlobalSignals.SystemStress01`. `AmbientBiotaDriftJob.Execute` still has no `if (` branch source. `git diff --check` passed with CRLF warnings only. Direct ambient Bee compile is blocked by missing `Hecton8.Core.ref.dll`; global `dotnet build` is blocked outside ambient in `PhysicsApplySystem.cs` with missing force-packet queue fields/helpers and missing `BufferID.PhysicsForce*` entries. No `AmbientBiotaDirector` error appears in the global build log.

## 2026-05-16 - Loop 10 Portable GPU Presentation Payload

What was wrong: The indirect renderer uploaded raw `AbsoluteUniversePosition` to GPU and no shader consumed `_HectonBiota*`. Raw AUP is correct CPU authority, but its 64-bit grid fields are a bad shader ABI for Metal/Quest/Android and leave the visual stream incomplete.

What was done: Replaced raw AUP/velocity/state GPU upload with one double-buffered 64 B `AmbientBiotaGpuInstance` stream. CPU upload derives camera-local float positions from vault AUP truth, finite-clamps velocity/state presentation, packs state/species/bucket/emission into float/uint fields, and binds `_HectonBiotaInstances`. Added `Hecton_AmbientBiotaIndirect.shader` plus `.meta` in the ambient domain. The shader billboards quads, discards inactive slots, reads biome/quality/stress/flow/overkill knobs, and implements low-tier cheap math plus high-tier procedural parallax, SSS, silt, and salt glints.

Cinematic Cheats used: Low tier uses translucent billboard organisms with triangle pulse/noise and no texture samples. High/Ultra use fake biological depth: 16-step procedural parallax, rim SSS, flow-driven silt, salt glints, and panic biolume. No physical particles, no per-fish GameObjects, and no CPU matrices were added.

Exact Microseconds saved: No measured microseconds. Expected bandwidth improvement is one 64 B/slot packed upload instead of three raw streams totaling 96 B/slot, plus fewer material buffer bindings. High-tier shader cost is intentionally spent on visual overkill; Unity GPU profiling remains pending.

Verification: Static ambient scan remains clean for `SetData`, `private NativeArray`, direct `H8Memory.Allocate`, Unity `Update`, `foreach`, `string.Format`, `Instantiate`, `Random.Range`, legacy `EventBus`, managed delegates, scene search, coroutine, and `Resources.Load`. Shader static audit found no `long`, `int64_t`, `uint64_t`, `RWStructuredBuffer`, `Interlocked`, group barriers, `numthreads`, wave intrinsics, derivatives, or texture sampling. `Tick` remains free of `GlobalRegistry.`, `EnsureVaultBuffers()`, and `GlobalSignals.SystemStress01`. `AmbientBiotaDriftJob.Execute` still has no `if (` branch source. Direct ambient Bee compile remains blocked by missing `Hecton8.Core.ref.dll`; global build is blocked outside ambient by diagnostics/UI DTO errors. Unity shader import/compiler proof is not available in this shell session.

## 2026-05-16 - Loop 11 Domain Boundary And False SDF Claim Purge

What was wrong: `AmbientBiotaDirector` had a direct `Hecton8.Caves` / `HectonVoxelVolume.GetSDFDensity` dependency and then marked macro-hydrated ambient biota with SDF-emergence flags. That is a cross-domain cave/voxel truth query inside the ambient owner, and the SDF flags were not defensible through an ambient-owned typed service.

What was done: Removed the cave import and the voxel density query from `AmbientBiotaDirector`. Replaced the SDF guard with a finite-AUP/stress presentation gate named `ResolveMacroVisualQualityTier()`. Removed `AmbientBiotaState.FlagSdfEmergence` writes and removed `EntitySpawnSignal.FlagSdfEmergence` from ambient macro spawn payloads. Re-ran domain-boundary, forbidden-pattern, branchless-advection, tick-hot-path, shader-portability, diff, Bee, and global build gates.

Cinematic Cheats used: Macro hydration now stays honest: low/stressed hardware collapses to billboard-quality visual biomass, high/ultra uses shader-side visual overkill without pretending to own cave SDF truth. Silt, salt glints, parallax, SSS, flow tint, and biolume remain render fakes, not physics or voxel-collision truth.

Exact Microseconds saved: No measured microseconds. Expected impact is removal of a macro-hydration cave-volume query/scan from the ambient service path; hot `Tick` and `LateFrameTick` are unchanged. The build wall prevents Unity Profiler/GCMonitor/Frame Debugger proof.

Verification: `rg -n "Hecton8\.Caves|HectonVoxelVolume|FlagSdfEmergence" Assets/_Project/Scripts/AI/Ambient` returns no matches. Static ambient forbidden-pattern scan returns no matches. `AmbientBiotaDriftJob.Execute` still has no `if (` branch source. `Tick(float deltaTime)` still has no `GlobalRegistry.`, no `EnsureVaultBuffers()`, and no `GlobalSignals.SystemStress01`. Shader static audit remains clean for DirectX-only/mobile-hostile features. Direct Bee compile is blocked by missing `Hecton8.Core.ref.dll`; global build is blocked outside ambient by missing contract symbols (`HectonEcologyContract`, `ScalabilityContract`, `HectonPhysicsContract`, `HectonSurvivalContract`). Unity shader import/compiler proof is still unavailable.

## 2026-05-16 - Loop 12 Shader NaN Vaccination And Contract Recheck

What was wrong: The ambient shader had a portable 64 B GPU instance payload, but its vertex/fragment math still used raw `normalize()` for flow, camera axes, drift axis, normals, and view direction. That left a GPU-side NaN path outside the CPU packing guards. The biome-vault claim also needed rechecking against current files.

What was done: Added `SafeNormalize2` and `SafeNormalize3` in `Hecton_AmbientBiotaIndirect.shader` and replaced all raw shader normalization with finite fallbacks. Rechecked Core/World/AI for a vault-owned current-biome buffer; none exists, so ambient remains on the existing typed `BiomeChangedSignal` lane instead of inventing a duplicate or crossing into world-private native maps. Reran static ambient, domain-boundary, shader-portability, branchless-advection, tick-hot-path, diff, Bee, and global build gates.

Cinematic Cheats used: Low tier remains cheap billboard soup with triangle pulse/noise and no collision or texture samples. High/Ultra keep the render fakes: 16-step procedural parallax, rim SSS, flow-driven silt, salt glints, biome tint, and panic biolume. The new safe normalization keeps those visuals alive without adding CPU physics truth.

Exact Microseconds saved: No measured microseconds. This pass is a stability/portability repair, not a frame-time claim. Expected hardware impact is lower NaN/crash risk on Quest/Android/Metal and 0 B/frame additional file I/O or managed allocation. Extra shader ALU remains unprofiled because Unity shader import/profiler proof is unavailable here.

Verification: `rg -n "normalize\(" Assets/_Project/Scripts/AI/Ambient/Hecton_AmbientBiotaIndirect.shader` returns no matches. Static ambient forbidden-pattern scan returns no matches. Shader static audit returns no DirectX-only/mobile-hostile terms. `AmbientBiotaDriftJob.Execute` still has no `if (` branch source. `Tick(float deltaTime)` still has no `GlobalRegistry.`, no `EnsureVaultBuffers()`, and no `GlobalSignals.SystemStress01`. Direct Bee compile is blocked by missing `Hecton8.Core.ref.dll`; global build is blocked outside ambient by `World/EcosystemDirector.cs` index-helper/field errors. No ambient source error appears in the build log.
