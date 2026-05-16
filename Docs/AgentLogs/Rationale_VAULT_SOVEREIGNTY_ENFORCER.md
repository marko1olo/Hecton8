# Rationale - VAULT_SOVEREIGNTY_ENFORCER

Status: DATAVAULT NO-REGRESSION GATE GREEN; HECTON8.CORE BUILD GREEN; ZERO-DEBT SOVEREIGNTY STILL PENDING

## Decision 001 - Scope Boundary
Problem: The prompt asks for DataVault statelessness across all domains, but the authoritative write domain is Core/Memory and the workspace is active with many agents.
Solution: Use Core/Memory as the ownership point, migrate only prompt-named concrete offenders and compile-driven call sites. DataVault handles and H8Memory owner tracking remain the DOD pattern.
Rejected Alternatives: A broad cross-domain rewrite would create compile walls and public API churn. Leaving gameplay-local persistent NativeArrays untouched would violate Data Vault Sovereignty.
Scalability potential: Low uses centralized caps and cold boot buffers. Middle uses stable handles. High and Ultra can spend centralized capacity on VoxelSdfTexture3D/cache-heavy visuals.
Hardware Impact: On i3/MX350, centralized allocation avoids native heap fragmentation spikes and preserves VRAM headroom. Estimated gain pending static delta and profiler proof.

## Decision 002 - Mandate Set
Problem: NativeArray ownership touches memory, zero-GC, AUP, telemetry, registry injection, signal lanes, and frame budget constraints.
Solution: Loaded OPT_Native_Memory_Collections_JobSystem_Protocol, OPT_Zero_GC_Policy_AllocFree_Mandate, OPT_HectonArenaAllocator_2_0, DBG_Telemetry_Crash_Reporting_PostMortem, MATH_Coordinate_Precision_AUP_FloatingOrigin, ARCH_Global_Registry_ServiceLocator_DI_Init, ARCH_Signal_Lane_Segregation, and OPT_Performance_Budgets_FrameTime_VRAM_Limits.
Rejected Alternatives: Reading the entire registry would waste context and increase chance of unrelated domain bleed. Reading only the two required files would miss AUP, telemetry, and signal constraints in the task list.
Scalability potential: Low-Middle-High-Ultra decisions remain tied to pool caps, handle safety, telemetry, and cold boot seeding rather than per-system ad hoc behavior.
Hardware Impact: On i3/MX350, strict zero-GC and owner-scoped native memory reduce allocator jitter; exact microsecond savings remain PENDING VERIFICATION.

## Decision 003 - Sargassum Vault Eviction
Problem: SargassumMicroFaunaBoids owned many persistent private NativeArrays and disposed them from the component lifecycle, making the fauna system stateful.
Solution: Added Sargassum BufferID lanes and resolved those arrays through IDataVault with SystemID.WorldSargassum. Component teardown now unregisters views without freeing vault memory.
Rejected Alternatives: Keeping H8Memory.Allocate fallbacks would preserve fragmented ownership. Rewriting the NativeQueue kill lane into a custom ring was deferred because the current IDataVault API does not expose queue semantics.
Scalability potential: Low uses centralized 512MB pressure; Middle and High keep boid/LOD staging arrays stable; Ultra can raise boid visual density without reallocating per component.
Hardware Impact: On i3/MX350 this removes repeated native heap allocations for sargassum staging. Estimated saved allocator time: 18,000 us during scene/component setup, 0 us hot path.

## Decision 004 - Rigidbody AUP Precision
Problem: RigidbodyAUPs were stored as float3, which violates AUP precision requirements and poisons lockstep/hash validation at large offsets.
Solution: Converted the vault lane, culling job input, lockstep hash sampling, and headless NaN scan to double3. Distance tests cast to float only after finite double computation.
Rejected Alternatives: Keeping float3 and documenting it as camera-relative would keep the vault as lossy authority. Storing full AbsoluteUniversePosition structs would increase stride and cache pressure for the culling job.
Scalability potential: Low still performs one double3 length-squared per tracked body; High/Ultra retain long-range precision for overkill physics telemetry.
Hardware Impact: On i3/MX350 the added double math is bounded to 512 bodies and remains below the 0.1 ms suspicion threshold; saved debugging cost is avoiding AUP drift false positives.

## Decision 005 - Vault Relocation Contract
Problem: A high-end vault must expand beyond the default arena, but moving the raw arena invalidates stale cached pointers.
Solution: Added arena growth limits, relocation records, 64-byte pointer validation, and generation-preserving metadata refresh. Existing VaultBufferHandle resolution continues to throw FatalMemoryException on stale cached identity.
Rejected Alternatives: Allocating 4GB at boot would punish low and mid hardware. Silently updating arbitrary NativeArray aliases is impossible without owning every consumer.
Scalability potential: Low clamps at 512MB. High/Ultra may expand toward 4GB for VoxelSdfTexture3D-class caches while emitting MemoryAddressShiftSignal.
Hardware Impact: On i3/MX350 growth is capped and defrag remains telemetry-only under stress; estimated hot-path pointer resolution delta: under 1 us because checks run on handle resolve/allocation paths, not per element.

## Decision 006 - DataVault Pressure Feedback
Problem: Capacity pressure previously stayed in telemetry and did not reach the diegetic PDA.
Solution: SystemDispatcher publishes a MemoryPressureSignal when DataVault pressure exceeds 80%, and PDAShellChrome displays a fixed vault-fragmentation tag for a 300-frame window.
Rejected Alternatives: Adding a new UI-only event lane would duplicate MemoryPressureSignal. Per-frame polling from UI would couple PDA directly to Core/Memory.
Scalability potential: Low/MX350 sees early 512MB pressure warnings. High/Ultra can tolerate larger caches but still warns at the same ratio.
Hardware Impact: PDA signal consumption is a frame snapshot scan over the existing lane; estimated runtime cost is below 2 us per open-PDA late-frame tick.

## Decision 007 - Float Sanitization and Blackbox
Problem: Vault-owned float buffers can preserve NaN payloads across systems and make postmortems useless.
Solution: DataVault sanitizes float/double scalar and vector views during Get/Try/Resolve, and the defrag ring stores ActiveBufferCount beside fragmentation ratio.
Rejected Alternatives: Sanitizing every producer would miss future producers. Logging only the ratio would not identify live vault pressure.
Scalability potential: Low uses finite zero fallbacks. High/Ultra keep wider caches but still dump deterministic blackbox state.
Hardware Impact: Sanitization is O(n) on buffer exposure, not per element access. MX350 cost is paid during buffer resolve/allocation boundaries, estimated 1,000-3,000 us for large cold buffers.

## Decision 008 - Build Wall Classification
Problem: Validation cannot reach zero errors because the workspace build wall has moved into files outside the vault lane.
Solution: Fixed touched vault-adjacent compile faults, then reran `dotnet build .\Hecton8.Core.csproj --no-restore`. The latest failure is `Assets/_Project/Scripts/Construction/VehicleDockingModule.cs` duplicate `IsLowDockingMathTier`, `ResolveSystemStress01`, and `ResetDockingRuntimeCaches`.
Rejected Alternatives: Patching unrelated construction/autopilot logic would violate the CORE/DATA domain boundary and collide with another modified file.
Scalability potential: No runtime scalability effect; this preserves integration stability by refusing unrelated churn.
Hardware Impact: No frame-time impact. Build remains blocked externally after local compile fault removal.

## Decision 009 - Player Motor Hidden NativeArray Closure
Problem: Omega scan found `HectonPlayerMotorNativeState` behind HectonPlayerMovement still using direct NativeArray constructor/dispose ownership for KCC raycast command/result buffers.
Solution: Converted scheduled sweep and kinematic repair target buffers to vault-first BufferIDs with `SystemID.GameplayPlayer`, falling back only to `H8Memory.Allocate` and releasing through DataVault view invalidation or `H8Memory.Release`.
Rejected Alternatives: Leaving KCC raycast buffers as local persistent arrays would keep player movement stateful. Moving Unity raycast command buffers fully into generic Sargassum-style helpers would hide ownership and add churn.
Scalability potential: Low keeps the tiny KCC command/result lanes centralized and owner-tracked. Middle/High/Ultra can increase ray batch counts through vault capacity without reintroducing component-owned native heap fragments.
Hardware Impact: On i3/MX350 this removes local allocator ownership and direct disposal from the player motor helper. Estimated cold allocator churn avoided: 5,000-20,000 us across resize/setup events; hot path remains unchanged.

## Decision 010 - Omega Global Debt Classification
Problem: Repo-wide audit still reports 1357 `new NativeArray<T>` constructor sites across 206 files, so the literal global rule is not satisfied by the whole active workspace.
Solution: Marked the prompt-owned vault scope as verified and recorded the repo-wide count as cross-domain legacy debt. The current batch has many parallel agents with their own NativeArray/DataVault migrations; this agent should not rewrite 206 unrelated files without domain ownership.
Rejected Alternatives: Claiming global clean would be false. Performing a massive regex rewrite would break job dependency disposal, vault BufferID contracts, and active parallel work.
Scalability potential: Low/Middle/High/Ultra benefit immediately inside the prompt scope; full-project sovereignty requires a separate coordinated migration or compile-time audit gate.
Hardware Impact: No additional runtime cost. The audit cost is tooling-only; the remaining global debt is a scheduling/integration risk, not hidden frame-time work from this patch.

## Decision 011 - ABI Guard Hardening
Problem: ARM64/Quest/Android builds are intolerant of implicit native struct padding, especially for blackbox and vault metadata crossing Burst/native boundaries.
Solution: Verified Core/Memory structs use `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = ...)]` where they are serialized or stored in native rings, and verified cold-start `ValidateAbiLayout()` guards for H8Memory and GlobalDataVault.
Rejected Alternatives: Trusting C# sequential layout without explicit size would leave Quest/IL2CPP packing drift invisible until runtime.
Scalability potential: Low keeps metadata compact and predictable. Middle/High/Ultra retain deterministic telemetry and relocation metadata without platform-specific forks.
Hardware Impact: On i3/MX350 and ARM64, this is cold-start only. Estimated hot-path gain is 0 us; avoided failure mode is corrupted metadata reads and crash-dump ambiguity.

## Decision 012 - Stress-Gated Arena Movement
Problem: Raw arena growth and defrag-style movement can add CPU pressure exactly when the frame is already overloaded.
Solution: `FrostTickDefrag(elapsedSeconds, systemStress01)` marks MemMove blocked when `SystemStress01 > 0.9`, sets the stress-halt telemetry flag, and `TryGrowArena` refuses raw arena movement while blocked.
Rejected Alternatives: Always moving memory during pressure would protect capacity at the cost of hitching. Disabling all growth permanently would punish High/Ultra visual cache workloads.
Scalability potential: Low uses hard 512MB cap and halts relocation under stress. High/Ultra can expand toward 4GB during non-stressed windows for SDF/visual cache overkill.
Hardware Impact: On i3/MX350 this prevents worst-case native MemMove spikes during overload. Exact saved time depends on arena size; hot-path cost is one boolean branch on growth/defrag paths.

## Decision 013 - Signal Debt Boundary
Problem: The latest inquisition found prompt-adjacent legacy `System.Action` player events and `PhysicsEventBus` registrations even though Core/Memory itself is clean.
Solution: Recorded the debt and blocked it as cross-domain because the player events have live subscribers in weather, audio, visor, VFX, and UI, while `PhysicsEventBus` is still widely used by physics/audio/AI systems. Sargassum sensory code already consumes typed `SignalBus<T>` snapshots.
Rejected Alternatives: Removing public events from `HectonPlayerMovement` or unregistering `GlobalPhysicsStateManager` from `PhysicsEventBus` in the vault lane would break subscribers without a coordinated Signal Authority migration.
Scalability potential: Low/Middle benefit from existing zero-allocation typed lanes where already adopted; High/Ultra need the same typed-lane contract before adding richer sensory/VFX traffic.
Hardware Impact: No direct runtime gain from documentation. Removing managed delegates later would cut subscription churn risk and reduce hidden GC hazards, but proof requires a coordinated subscriber rewrite.

## Decision 014 - DataVault Dump Ownership
Problem: GlobalDataVault blackbox dumps were still named `Dump_PLATINUM_DATA_VAULT_WARDEN*.bin`, so crash evidence would not satisfy the current agent contract.
Solution: Renamed DataVault dump paths to `Docs/AgentLogs/Dump_VAULT_SOVEREIGNTY_ENFORCER.bin` and `Docs/AgentLogs/Dump_VAULT_SOVEREIGNTY_ENFORCER_PHIVOD.bin`.
Rejected Alternatives: Leaving legacy dump names would make postmortem discovery fail for this batch. Duplicating writes to both names would add avoidable I/O and create stale evidence ambiguity.
Scalability potential: Low/Middle/High/Ultra unchanged at runtime; this is crash-path evidence hygiene.
Hardware Impact: 0 us hot path. Crash/anomaly dump writes one deterministic binary target instead of a legacy-named file.

## Decision 015 - Submarine Vault View Repair
Problem: A parallel DataVault migration in `SubmarineFluidDynamics` replaced writable NativeArray fields with expression-bodied properties returning `NativeArray<T>` structs, making every indexer write a write to a temporary and breaking the Core build.
Solution: Restored cached NativeArray view fields while preserving the actual ownership in `VaultBufferHandle<T>`. `EnsureNativeState` now assigns each view from the existing vault allocation helper.
Rejected Alternatives: Rewriting every write site to local resolved views would be a risky broad refactor. Reverting the migration would reintroduce local native ownership.
Scalability potential: Low keeps submarine flood buffers centralized and capped. High/Ultra can expand buffer-backed hydro state through DataVault without per-component ownership.
Hardware Impact: 0 us hot path from the compile fix itself; handle resolution remains cold initialization. Avoided runtime failure is missing/temporary NativeArray state in submarine hydrodynamics.

## Decision 016 - Final Validation Boundary
Problem: Focused Core validation could be made green, but the broader generated `Assembly-CSharp.csproj` still fails before gameplay assemblies because the RealtimeCSG generated project references missing package source files.
Solution: Repaired Core compile errors and verified `dotnet build .\Hecton8.Core.csproj --no-restore` succeeds with 0 warnings and 0 errors. Recorded `Assembly-CSharp.csproj` as an external package/project wall.
Rejected Alternatives: Editing or deleting RealtimeCSG generated project references from the vault lane would mutate third-party/package project state outside this task.
Scalability potential: No runtime scalability effect; this preserves the Core/Data proof line and exposes the remaining package wall honestly.
Hardware Impact: No frame-time impact. Build verification is tooling-only.

## Decision 017 - Predator Cognition Vault Eviction
Problem: `PredatorCognitionDomain` had been partially migrated to DataVault semantics but the required `EnsureCoreCognitionVaultBuffers()` method was missing, leaving Core validation broken and core AI cognition arrays without a compile-proven vault ownership path.
Solution: Added a vault resolver for every core predator cognition buffer using existing `BufferID.PredatorCognition*` lanes and `SystemID.AICognition`. The remaining `NativeList` and `NativeParallelHashMap` containers stay under `NativeMemorySentinel` because the current DataVault API exposes `NativeArray<T>` buffers, not list/hash-map semantics.
Rejected Alternatives: Reintroducing local `new NativeArray<T>` constructors would violate the prompt rule. Rewriting hash maps into bespoke vault arrays in this pass would change AI algorithms and collide with active fauna-domain work.
Scalability potential: Low keeps cognition arrays inside the global 512MB cap. Middle/High/Ultra can increase fauna cognition density through centralized vault capacity instead of per-system native heap ownership.
Hardware Impact: On i3/MX350 this removes predator cognition NativeArray constructor/dispose ownership from the AI system. Exact runtime microseconds are not profiled; impact is cold setup allocator churn reduction, not a claimed hot-path frame gain.

## Decision 018 - Predator and Vault ABI Guard
Problem: The ARM64/Quest scan found vault-backed predator cognition payloads still relying on default sequential packing, and `VaultBufferHandle<T>` had Pack=1 without an explicit size.
Solution: Made `VaultBufferHandle<T>` `[StructLayout(Pack=1, Size=24)]`, added a handle size check to `GlobalDataVault.ValidateAbiLayout()`, and gave predator cognition vault payloads explicit Pack=1/Size values with a cold `ValidateAbiLayout()` guard. The submarine flood-mass result payload was also fixed to Pack=1/Size=44.
Rejected Alternatives: Trusting CLR default layout would leave IL2CPP/ARM64 layout drift invisible. Adding explicit Size to Unity job descriptor structs with `NativeArray<T>` fields was rejected because their debug/safety size is Unity-version-dependent and they are not vault element payloads.
Scalability potential: Low avoids corrupted compact buffers on Quest/Android. High/Ultra keep deterministic cache and telemetry layout when large fauna and hydro buffers are centralized.
Hardware Impact: 0 us hot path. The layout checks run at cold initialization and fail fast before corrupted memory reaches Burst jobs.

## Decision 019 - Current Validation Evidence
Problem: Prior status still carried stale counts and did not include the latest Predator cognition and ABI repairs.
Solution: Re-ran focused Core build, prompt-adjacent constructor scans, no-Pack layout scan, and broader Assembly-CSharp validation. Updated the status and log files with current numbers.
Rejected Alternatives: Reporting the older green build and older 1335 constructor count would be stale. Claiming repo-wide sovereignty would be false because the full repo still has direct NativeArray constructor debt.
Scalability potential: Low/Middle/High/Ultra unchanged directly; this decision keeps evidence accurate so follow-up migration can be scheduled by real counts.
Hardware Impact: Tooling-only. Current proof: focused `Hecton8.Core` build is green; broader generated Unity build remains blocked by missing RealtimeCSG source files.

## Decision 020 - DataVault Heartbeat Blackbox
Problem: DataVault blackbox entries were recorded by defrag/anomaly activity, but the user mandate requires the last 300 frames of critical system heartbeat, not only maintenance events.
Solution: Added `IDataVault.RecordHeartbeat()` and wired `SystemDispatcher.Update()` to record both `H8Memory.RecordHeartbeat()` and DataVault heartbeat once per frame. DataVault heartbeat writes into the existing fixed 300-entry `MemoryDefragTelemetryEntry` ring with a heartbeat flag and no file I/O.
Rejected Alternatives: Forcing `FrostTickDefrag()` every frame would violate the 0.1 ms frame budget and perform unnecessary gap analysis. Writing a file every frame would create Steam Deck/MicroSD stutter risk.
Scalability potential: Low keeps the heartbeat as one fixed-ring write. Middle/High/Ultra retain deterministic crash history without consuming GPU/visual budget.
Hardware Impact: One NativeArray element write per frame, 0 us hot-path disk I/O. Exact microseconds are not profiled; expected cost is below the memory defrag profiler noise floor.

## Decision 021 - Shader Thread Group Audit Boundary
Problem: The multiplatform inquisition requires Metal/Mac and Quest thread group safety, but the current agent owns Core/Data rather than shader implementation.
Solution: Ran a repo compute-kernel scan resolving numeric `numthreads(...)` constants under `Assets/_Project`. Result: 74 kernels, max resolved group size 512, Over1024=0. No shader edit was needed from the Core/Data lane.
Rejected Alternatives: Editing shader code without an over-limit finding would violate domain boundaries and risk graphics-agent conflicts. Ignoring shader evidence would leave the Metal/Quest claim unproved.
Scalability potential: Low/Quest/Metal stay below known 1024 thread group limits. High/Ultra can still spend visual budget because no forced shader downscale was introduced.
Hardware Impact: Tooling-only audit, 0 us runtime change.

## Decision 022 - DataVault Sovereignty No-Regression Gate
Problem: The repo still has cross-domain `new NativeArray<T>` debt, so prose-only status lets new violations hide inside the existing backlog.
Solution: Added `Tools/DataVaultSovereigntyAudit.py` with a baseline-backed no-regression mode and a final zero-debt mode. The stable gate is now documented in `Docs/QUALITY_GATES.md`, and the current baseline/report live under `Docs/AgentLogs/`.
Rejected Alternatives: Failing every build immediately with 1250 existing forbidden constructors would block unrelated agents and create false compile noise. Leaving only manual `rg` commands would not stop regression.
Scalability potential: Low/Middle/High/Ultra all benefit from preventing new private native heap islands while existing domains migrate to `GlobalDataVault` or `H8Memory.Allocate`.
Hardware Impact: 0 us runtime. Tooling-only scan currently measures 1256 direct constructors total, 6 allowed inside `H8Memory.cs`, and 1250 forbidden legacy system constructors across 192 files.

## Decision 023 - Current Compile Wall Boundary
Problem: After the no-regression gate landed, current `dotnet build .\Hecton8.Core.csproj --no-restore` fails with 141 errors in files outside the Core/Memory authority lane.
Solution: Classified the wall as external dependency debt: `ProceduralBiteIkJobs` local-name collision, `GameBootstrapper.Initialize` signature mismatch, missing biome fog fields in `HectonUnderwaterVisuals`, and missing native-state fields/helpers in `ToolDurabilitySystem`. DataVault tooling validation remains green.
Rejected Alternatives: Editing animation, bootstrap, visuals, or tools code from the DataVault sovereignty pass would violate domain boundaries and collide with active agents. Reporting the previous green Core build as current would be false.
Scalability potential: No direct runtime scalability effect; this preserves integration truth while the no-regression gate prevents new memory-sovereignty debt.
Hardware Impact: 0 us runtime. Build verification is currently blocked outside owned files.

## Decision 024 - Moving Compile Wall Recovery
Problem: The active workspace kept changing under validation; stale compiler walls reported missing predator job fields, duplicate audio helpers, tether request contracts, and finally a real missing brace in `SubmarineFluidDynamics`.
Solution: Revalidated against current disk each time, avoided duplicate patches when another agent had already moved the file, and repaired the owned submarine guard brace inside `PublishSplashFluidImpulse`. The focused Core build now succeeds with 0 warnings and 0 errors.
Rejected Alternatives: Trusting stale compiler output would have introduced duplicate methods/types. Killing unrelated active-agent build processes would have disrupted shared validation. Leaving the submarine syntax fault would block Core validation.
Scalability potential: No direct tier behavior change. The repaired guard preserves NaN vaccination before publishing fluid impulse signals on all tiers.
Hardware Impact: 0 us hot-path gain claimed. The brace fix restores the existing early-return finite guard; it does not add new runtime work.

## Decision 025 - Drone Fleet Culling No-Regression Repair
Problem: `python Tools\DataVaultSovereigntyAudit.py --fail-on-regression` caught a new `DroneFleetManager` direct `NativeArray<DroneCullingStateGpu>` constructor, increasing that file from 14 to 15 forbidden sites.
Solution: Added `SystemID.Construction` and `BufferID.DroneFleetCullingStates`, routed the culling upload lane through `GlobalDataVault.GetBuffer`, and kept an `H8Memory.Allocate` fallback only when the vault is unavailable. Vault aliases are not disposed by `DroneFleetManager`; fallback allocations release through `H8Memory.Release`.
Rejected Alternatives: Updating the baseline would normalize new debt. Keeping direct `new NativeArray<T>` would violate the no-regression gate. Rewriting all 14 remaining legacy drone arrays in this pass was rejected because that is a construction-domain migration larger than the regression.
Scalability potential: Low/Middle keep the compact 16-byte culling payload centralized under the vault cap. High/Ultra can keep the GPU culling path without adding another private native heap island.
Hardware Impact: Runtime hot path is unchanged; the culling payload still writes one compact element per drone before upload. Cold allocator churn avoided for this new lane is at most the removed persistent `NativeArray<DroneCullingStateGpu>[64]` constructor; exact microseconds are not profiled.
