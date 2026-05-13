# Recon: TECH_RESEARCHER_EVOLVER

Status: PENDING VERIFICATION

This file records static reconnaissance for mandate purge verification.

## Phase 1 Static Audit

### Task 1: Dead Code Hunt

Command: `rg -n "^\s*public\s+interface\s+I[A-Za-z0-9_]+" Assets/_Project/Scripts/Core/Contracts Assets/_Project/Scripts/World/Contracts Assets/_Project/Scripts/Core/BootstrapContracts`

Interfaces found:

| Interface | Hits | Files | Verdict |
|---|---:|---:|---|
| IPlatformIntegration | 2 | 2 | KEEP. Implemented by `Input/UserOptionsPersistence.cs`; not dead. |
| IScatterSimulationBackendProvider | 3 | 1 | DELETE CANDIDATE. Only used inside its registry file; no external provider implementation found. |
| IInputRebindService | 4 | 3 | KEEP. Implemented by `Core/RebindingManager.cs`; exposed through `GlobalRegistry.InputRebind`. |
| IScatterSimulationBackend | 9 | 5 | KEEP. Referenced by world scatter simulation backend contracts. |
| IInputBindingService | 37 | 6 | KEEP. Active binding service contract. |
| IServiceHeartbeat | 49 | 45 | KEEP. Broad lifecycle contract usage. |
| IServiceShutdown | 51 | 48 | KEEP. Broad lifecycle contract usage. |

### Task 2: Enumerator Purge

Command: `rg -n "\bIEnumerator\b|\bStartCoroutine\b|\byield\s+return\b" Assets/_Project/Scripts -g "*.cs" --glob "!Editor/**" --glob "!**/Editor/**"`

Result: no core/runtime hits. The only hits from the broader scan were editor compliance scanners:

| File | Pattern | Verdict |
|---|---|---|
| Assets/_Project/Scripts/Editor/PerformanceHotPathValidator.cs | StartCoroutine scanner token | KEEP. Editor-only audit tool. |
| Assets/_Project/Scripts/Editor/ZeroGCComplianceScanner.cs | StartCoroutine scanner token | KEEP. Editor-only audit tool. |

Purge status: runtime `IEnumerator` infection not detected by static scan. Runtime verification still absent.

### Task 3: Singleton Audit

Command: `rg -n "\bpublic\s+static\s+[A-Za-z0-9_<>,.]+\s+Instance\s*(=>|\{)|\bpublic\s+static\s+readonly\s+[A-Za-z0-9_<>,.]+\s+Instance\b" Assets/_Project/Scripts -g "*.cs" --glob "!Editor/**" --glob "!**/Editor/**"`

Result: 34 runtime hits. Most are compatibility facades returning `GlobalRegistry.*`, not self-registering MonoBehaviour singletons. They still preserve the forbidden `public static Instance` access shape and must be treated as migration debt.

High-signal examples:

| File | Verdict |
|---|---|
| AsyncLoadHelper.cs | `Instance => null`; obsolete facade, deletion candidate after call-site scan. |
| ObjectPoolManager.cs | `Instance => GlobalRegistry.ObjectPool`; compatibility facade, not a classic singleton but still banned API shape. |
| HectonAtmosphereManager.cs | property body must be inspected by owning agent; static facade remains. |
| HectonFluidEngine.cs | property body must be inspected by owning agent; static facade remains. |
| MapMagicBridge.cs | property body must be inspected by owning agent; third-party bridge facade remains. |
| WorldStateManager.cs | property body must be inspected by owning agent; static facade remains. |
| EnumFastComparer.cs | `static readonly Instance`; non-Mono utility cache, acceptable if zero-GC comparer singleton is isolated from service ownership law. |

Purge status: NOT PURGED. Static facades remain. No source changes performed by Tech Researcher.

### Task 4: ASMDEF Audit

File: `Assets/_Project/Scripts/Hecton8.Core.asmdef`

Findings:

| Check | Result |
|---|---|
| Direct reference to `Hecton8.UI` | PASS. No `Hecton8.UI` asmdef reference found. |
| Direct reference to Unity UI packages | PRESENT. `Unity.TextMeshPro` and `UnityEngine.UI` remain. |
| Shattered core dependency shape | FAIL. Core still references `Hecton8.Logistics`, `Hecton8.Cartography`, `Hecton8.SpaceEngine098Terrain`, `Hecton8.Input`, `Hecton8.Input.Generated`, URP, GPUInstancer, VolumetricLightBeam, Addressables, and TextMeshPro. |

Verdict: not UI-asmdef coupled, but not truly shattered.

### Task 5: Mandate Audit

Command read all mandate files in `.agents-skills` and counted upgrade/debt tokens.

| Metric | Value |
|---|---:|
| `.txt` mandate files read | 75 |
| README claimed mandate count | 53 |
| Files with deprecated/debt tokens in top-20 list | 13 |
| Top RenderGraph debt file | REND_URP_Graphics_HotPath_Optimization_HLOD.txt |
| Top async debt file | STRM_Async_Standard.txt |
| Top zero-GC debt file | OPT_Zero_GC_Policy_AllocFree_Mandate.txt |

Registry README inventory is stale and must be updated to 75.

## Phase 2 Mandate Evolution

### Task 6: Unity 6 RenderGraph

Updated: `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`

Added:
- `RecordRenderGraph` hardening.
- Compatibility Mode ban for new URP work.
- `Graphics.Blit`, `CommandBuffer.Blit`, direct `SetRenderTarget`, and hidden native command-buffer work as legacy debt.
- Engineering Data table for RenderGraph gates.

### Task 7: Awaitable Upgrade

Updated: `.agents-skills/STRM_Async_Standard.txt`

Added:
- `Awaitable` / `Awaitable<T>` as the Unity object orchestration default.
- Pooled single-consumer Awaitable rule.
- `async Task` and per-request `Task.Run` bans for Unity object paths.

### Task 8: GPU Resident Drawer

Updated:
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_Instanced_Flora_Physics.txt`

Added:
- MeshRenderer-owned static/HLOD/flora uses GPU Resident Drawer first.
- Manual BRG reserved for pure data with no stable MeshRenderer ownership.
- GPU occlusion requires RenderGraph, Forward+, compute support, and profiler proof.

### Task 9: Addressables Residency API

Updated: `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

Added:
- Addressables 2.7 `AssetLoadMode.RequestedAssetAndDependencies` default.
- `AllPackedAssetsAndDependencies` restricted to tiny always-hot bundles with resident memory proof.
- Resident, committed, graphics memory, and handle count must be reported separately.

### Task 10: Zero-GC String Audit

Updated: `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`

Added:
- `ReadOnlySpan<char>` and `Span<char>` laws.
- `TMP_Text.SetCharArray` submission path.
- bans on `SetText(string)`, `text =`, interpolation, `new string(span)`, `string.Create`, enum/numeric `.ToString()`, and Span capture/storage.

## Phase 3 Data & Diagnostics

### Task 11: Native Memory Tracking

Updated: `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`

Added:
- Sentinel registration law for NativeContainers, raw `UnsafeUtility.Malloc`, queues, arenas, and upload staging.
- registration/unregistration ordering.
- Engineering Data table for allocation type, required fields, release rule, and failure class.

### Task 12: Burst 1.8.x Features

Updated: `.agents-skills/MATH_Rsqrt_i3_SIMD.txt`

Evidence:
- Package lock resolves `com.unity.burst` to 1.8.28.
- Local package exposes `v64`, `v128`, and `v256`.
- No local package evidence for `v512`.

Added:
- v128 baseline fallback law.
- v256 High/Ultra only with guards and benchmarks.
- deterministic FloatMode warning for processor intrinsics.

### Task 13: Cross-Domain Audit

Updated:
- `.agents-skills/LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`
- `.agents-skills/CORE_Weather_Abyssal_FlowField_Currents.txt`

Verdict:
- No direct contradiction after edits. Logistics transports coolant/resource availability; thermodynamics owns heat state; flow consumes read-only summaries for VFX/current fakes.

### Task 14: Project Atlas Update

Generated:
- `Docs/PROJECT_ATLAS.md`
- `PROJECT_ATLAS.md`

Verdict:
- Direct `Hecton8.UI` reference from Core is absent.
- Core remains broad and not truly shattered due Unity UI packages, Logistics, Cartography, Input, terrain, URP, Addressables, GPUInstancer, and VolumetricLightBeam references.

Continuation correction:
- Re-scan of `Assets/_Project/**/*.asmdef` found 24 first-party asmdefs after concurrent workspace changes.
- Initial atlas omitted `Hecton8.Core.Memory`, `Hecton8.Core.Time`, `Hecton8.Lighting`, `Hecton8.Physics.Determinism`, `Hecton8.QA`, `Hecton8.QA.Editor`, `Hecton8.EditModeTests`, and `Hecton8.PlayModeTests`.
- Atlas files were updated to include those assemblies and mark `Hecton8.Lighting` / `Hecton8.QA` Core dependency as architecture debt.

## Phase 4 Safety & Quality Gates

### Task 15: Quality Gates

Updated: `Docs/QUALITY_GATES.md`

Added:
- MX350 hard budget gate.
- Physics total <= 2.0ms p95.
- main thread, VRAM, texture, RT/depth, SetPass, batch, GC, and native memory flatness gates.
- load-shed trigger table.

### Task 16: Event Bus Enforcement

Updated: `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`

Added:
- `RULE-09B` banning `GlobalRegistry.Get<T>()` for simple state updates.
- decision table for service query vs EventBus broadcast vs dirty state update.

### Task 17: AUP Drift Docs

Updated: `.agents-skills/MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`

Added:
- 300-frame snap-fence telemetry law.
- shift id stale-cache rejection.
- tiered drift probe cadence and max camera-relative error table.

### Task 18: Engineering Data / Anti-Fluff Sweep

Command result:

| Metric | Value |
|---|---:|
| `.agents-skills/*.txt` mandates | 75 |
| Missing `Engineering Data` before sweep | 64 |
| Missing `Engineering Data` after sweep | 0 |

Action:
- Appended concise `Engineering Data` gate tables to all missing mandate files.
- Existing richer tables were preserved.

### Task 19: Final Verification

Build review:

| Attempt | Command / Condition | Result |
|---|---|---|
| Loop 1 | `dotnet build Hecton8.Core.csproj --no-restore` | Failed, 24 C# errors in global dependencies. |
| Loop 2 | `dotnet build Hecton8.Core.csproj --no-restore` | Timed out after 124s during active compile contention. |
| Final | process scan | Active dotnet/MSBuild compile lane present, including `Hecton8.Core.csproj`; no parallel compile launched. |

Verdict:
- Source compile status: PENDING VERIFICATION / BLOCKED BY DEPENDENCY.
- Mandate docs are static-text changes; no runtime source files were intentionally edited by TECH_RESEARCHER_EVOLVER.

## Continuation: Voxel Mandate Static Recheck

Command class: static source/document scan only. No `dotnet build`.

Findings:
- `Assets/_Project/Scripts/HectonVoxelEngine.cs` declares itself as Unity 6 URP, Burst + Jobs, Marching Cubes, multi-primitive SDF.
- Current source uses `NativeArray<float>` working density buffers and `NativeArray<sbyte>` quantized density for `VoxelMCCountJob` / `VoxelMCExtractJob`.
- Current source contains 300-entry `VoxelMeshPipelineTelemetryEntry` Black Box and dump path `Docs/AgentLogs/Dump_VOXEL_MESH_PIPELINE.bin`.
- Existing voxel mandates still claimed `NativeArray<half>` runtime density and Transvoxel primary mesh with Marching Cubes forbidden.

Action:
- Updated voxel SDF, voxel world logic, and MapMagic/voxel integration mandates to mark current source truth: Marching Cubes primary, float working density, sbyte quantized extraction, Transvoxel/fp16 only as future migration targets with proof gates.
- No runtime source edited by TECH_RESEARCHER_EVOLVER.

## Continuation: Player/Pipe Static Recheck

Command class: static source/document scan only. No `dotnet build`.

### Player Kinematics

File: `Assets/_Project/Scripts/HectonPlayerMovement.cs`

Findings:
- Strict scan found no `StartCoroutine`, `IEnumerator`, `yield return`, `FindObjectOfType`, `GameObject.Find`, `Resources.Load`, `.ToString()`, or `string.Format` hits in the file.
- `GetComponent<Rigidbody>()` and camera `GetComponent<Camera>()` hits are in `Awake`; no hot-path component lookup was found by the focused scan.
- The single `Debug.LogWarning` hit is guarded by `#if UNITY_EDITOR || DEVELOPMENT_BUILD` in `SetSuit(null)`.
- The file already owns a 300-frame player kinematics Black Box and dump path `Docs/AgentLogs/Dump_PLAYER_KINEMATICS.bin`.
- The file contains severe mojibake in comments and serialized `[Header]` strings. This is not a frame-time issue, but it is an inspector/maintainability defect for the player owner.
- Hot-path helper chains still read static registry properties from `Tick`/`FixedTick` paths:
  - `RefreshVrComfortSettingsCache()` reads `GlobalRegistry.Settings` from both `Tick` and `FixedTick`.
  - `ResolveFallbackWaterSurfaceY()` reads `GlobalRegistry.Fluid` in water-height paths.
  - `ResolveOceanKinematics()` reads `GlobalRegistry.OceanKinematics` during fixed water sampling.
  - environmental helpers read `GlobalRegistry.SargassumDrag`, `GlobalRegistry.Thermodynamics`, and `GlobalRegistry.ResourceDistribution`.

Verdict:
- Coroutine purge remains clean in this file by static scan.
- Source has architecture debt: per-frame registry property reads should be cached by the player owner or refreshed by service changed/shutdown signals. TECH_RESEARCHER did not edit player runtime code.

### Pipe Logistics

Files:
- `Assets/_Project/Scripts/Construction/FluidPipeGraphRuntime.cs`
- `Assets/_Project/Scripts/Construction/LogisticsPipeTransportScheduler.cs`
- `Assets/_Project/Scripts/Construction/LogisticsPipeNode.cs`
- `Assets/_Project/Scripts/Construction/WaterPumpModule.cs`
- `Assets/_Project/Scripts/Logistics/FluidPipePressureJobs.cs`
- `Assets/_Project/Scripts/Logistics/FluidPipeGraphTypes.cs`

Findings:
- Focused scan found no `Update`, `FixedUpdate`, `StartCoroutine`, `IEnumerator`, `yield return`, scene search, `.ToString()`, or `string.Format` violations in the pipe logistics files listed above.
- `FluidPipeGraphRuntime` runs through `ISlowTickable` and `ILateFrameTickable`, schedules `FluidPipePressureSolveJob`, and uses `DispatcherJobSwap.TryComplete` instead of unconditional same-path `Complete`.
- Pipe graph native arrays, hash map, queue, and telemetry rings are registered with `NativeMemorySentinel`.
- `FluidPipeGraphConstants.BlackBoxFrameCount = 300`, with dump path `Docs/AgentLogs/Dump_PIPE_LOGISTICS_ARCHITECT.bin`.
- `WaterPumpModule` uses a static active-pump list and feeds `FluidPipeGraphRuntime.ApplyPumpInputs()` at pipe cadence; no per-frame MonoBehaviour pump loop was found.
- `Debug.LogWarning` in `LogisticsPipeTransportScheduler` is guarded by conditional attributes and `#if`; `Debug.LogError` in `FluidPipeGraphRuntime` is guarded and only fires on dump failure.

Verdict:
- Pipe logistics static shape is aligned with the mandate better than the older docs implied. Runtime proof remains absent because no Unity play/profiler run and no build were launched.

Action:
- Updated `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt` with `RULE-09C` so future agents cannot hide hot-path registry polling behind static convenience properties.
- No runtime source edited by TECH_RESEARCHER_EVOLVER.

## Continuation: Logistics Mandate Source Alignment

Command class: static source/document scan only. No `dotnet build`.

Mandate file:
- `.agents-skills/LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`

Findings:
- Old mandate wording treated `LogisticsTick` as entirely non-main-thread and implied every pipe/cable MonoBehaviour was forbidden.
- Current pipe source evidence separates roles:
  - `FluidPipeGraphRuntime` is a shared `ISlowTickable` / `ILateFrameTickable` runtime adapter that schedules and consumes pressure jobs.
  - `LogisticsPipeNode` and `WaterPumpModule` are Unity authoring/runtime adapters, not per-frame simulation loops.
  - Pipe pressure/transport truth is in native/job data with 300-frame Black Box telemetry and dump path `Docs/AgentLogs/Dump_PIPE_LOGISTICS_ARCHITECT.bin`.

Action:
- Updated the logistics mandate with a current source alignment section for the pipe runtime adapter boundary.
- Replaced stale guard wording with: graph solve never runs on main thread; main-thread cadence may only schedule/consume jobs and touch Unity-owned objects/signals.
- Replaced the broad MonoBehaviour ban with a per-segment simulation/update ban while allowing cold endpoint adapters.
- No runtime source edited by TECH_RESEARCHER_EVOLVER.

## Continuation: Player Service Snapshot Mandate

Command class: static source/document scan only. No `dotnet build`.

Source evidence:
- `Assets/_Project/Scripts/HectonPlayerMovement.cs` contains player tick/fixed profiler markers and dump path `Docs/AgentLogs/Dump_PLAYER_KINEMATICS.bin`.
- Static scan still finds registry reads in movement-related helper chains, including settings, fluid, ocean kinematics, sargassum drag, thermodynamics, and resource distribution.
- The vehicle kinematics mandate previously required strict `PlatformCache` reuse but did not require equivalent service snapshot evidence.

Action:
- Updated `.agents-skills/CORE_Submarine_Vehicles_Kinematics_AUP.txt` with a player service snapshot policy.
- Added explicit bans for `GlobalRegistry.Settings`, `Fluid`, `OceanKinematics`, `SargassumDrag`, `Thermodynamics`, and `ResourceDistribution` from player kinematic tick/fixed chains.
- Added Black Box evidence requirements for snapshot validity flags, provider version counters, fallback-water flag, and last service-rebind frame.
- No runtime source edited by TECH_RESEARCHER_EVOLVER.

## Continuation: Mandate Pseudocode Zero-GC Scrub

Command class: static mandate/document scan only. No `dotnet build`.

Findings:
- `CORE_Submarine_Vehicles_Kinematics_AUP.txt` still used `for each col in overlapColliders`, `Length(cache.delta_position)`, and `PlatformCache singleton` wording.
- `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt` still used `for each edge`, `for each consumer`, and `foreach upstream neighbor` pseudocode in graph/pressure paths.
- These were documentation examples, not runtime C# changes, but they contradicted zero-GC/indexed-loop and singleton-purge guidance.

Action:
- Converted the player penetration loop to indexed access.
- Converted logistics edge, consumer, and upstream-neighbor examples to index/range loops.
- Replaced high-G delta length check with `lengthsq` against a precomputed squared threshold.
- Replaced PlatformCache singleton wording with shared owner wording.
- No runtime source edited by TECH_RESEARCHER_EVOLVER.

## Continuation: Black Box Export Mandate Alignment

Command class: static source/document scan only. No `dotnet build`.

Findings:
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt` allocated a 300-frame `NativeArray<DebugLogEntry>` but exported only 50 preceding states.
- Static source evidence shows current player, pipe, voxel, habitat, and drone critical systems use 300-frame Black Boxes and dump to `Docs/AgentLogs/Dump_*.bin`.
- The central mandate also used `foreach component`, `vel.magnitude`, and per-fault `Task.Run` wording in telemetry examples.

Action:
- Updated the central telemetry mandate to export the full 300-entry ring oldest-to-newest.
- Added owner-specific dump path convention: `Docs/AgentLogs/Dump_[OWNER_ID].bin`.
- Replaced NaN sweep pseudocode with indexed iteration and squared velocity state.
- Replaced per-fault task wording with pre-owned export worker / `Awaitable.BackgroundThreadAsync` / persistent log thread wording.
- No runtime source edited by TECH_RESEARCHER_EVOLVER.

## Continuation: Engineering Data Header Normalization

Command class: static mandate/document scan only. No `dotnet build`.

Findings:
- Corrected atlas scan confirms `ASMDEF_COUNT=24` and `ATLAS_H8_ROWS=24`; the prior missing list came from a scanner expecting backticks not present in the atlas table.
- 13 evidence sections across 11 mandate files used `Engineering Data:` labels instead of standardized `## Engineering Data` headings.

Action:
- Normalized `Engineering Data:` to `## Engineering Data` in the affected mandate files.
- Preserved multiple evidence tables in mandates that intentionally carry local rule evidence for separate sections.
- No runtime source edited by TECH_RESEARCHER_EVOLVER.
