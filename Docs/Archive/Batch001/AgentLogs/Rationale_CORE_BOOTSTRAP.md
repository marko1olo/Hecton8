# Rationale_CORE_BOOTSTRAP

Status: PENDING VERIFICATION
Agent: BIOS_COMMANDER
Prompt: CORE_BOOTSTRAP

## Mandates Loaded

- ARCH_Project_Bootstrap_Sequence_Init_Safety
- ARCH_Global_Registry_ServiceLocator_DI_Init
- OPT_Zero_GC_Policy_AllocFree_Mandate
- OPT_Native_Memory_Collections_JobSystem_Protocol
- STRM_Async_Standard
- STRM_Asset_Lifecycle_Addressables_Loading_Memory
- DBG_Telemetry_Crash_Reporting_PostMortem
- OPT_Performance_Budgets_FrameTime_VRAM_Limits

## Baseline Decision

Problem: The requested prompt path `Docs/Tasks/CURRENT_BATCH.md` does not exist; the active batch file is `Docs/Tasks/CURRENT_BATCH.txt`.
Solution: Extracted `<AGENT_PROMPT id="CORE_BOOTSTRAP">` from the `.txt` file with a PowerShell regex using singleline mode.
Rejected Alternatives: Did not infer tasks from neighboring prompts. Did not rely on IDE tabs or chat text as authoritative batch state.
Scalability potential: Not runtime code; no tier impact.
Hardware Impact: 0 us runtime impact on i3/MX350.

Problem: Boot tasks touch registry, async loading, telemetry, and global quality tiers; wrong mandate selection would cause architecture drift.
Solution: Loaded 8 mandates covering bootstrap order, GlobalRegistry, zero-GC, native memory/jobs, Awaitable async, Addressables lifecycle, crash telemetry, and frame/VRAM budgets.
Rejected Alternatives: Did not bulk-load every registry file. Did not use dated reports as policy.
Scalability potential: Low/Mid/High/Ultra decisions must be encoded through quality and math precision contracts, not ad hoc per-system branches.
Hardware Impact: Expected gain is boot determinism and reduced cold-start stalls; exact microseconds pending code audit.

## Loop 1 Decisions

Problem: Hardware tiering had no persisted numeric score, which made BIOS decisions hard to audit after boot.
Solution: Added a cold `HardwareProfilerSnapshot` and deterministic 0-100 score based on VRAM, RAM, and logical CPU count. The boot profile now stores `HardwareScore`.
Rejected Alternatives: Rejected per-frame hardware checks and scattered quality decisions in presentation systems.
Scalability potential: Low/Mid/High/Ultra now have one BIOS-owned score source. Toaster path locks cheap math; high-end path remains eligible for visual overkill.
Hardware Impact: ~40 us cold score computation; 0 us hot path. Avoids repeated `SystemInfo` reads and inconsistent tier branching.

Problem: CPU<6 lock was incomplete because `ResolveMathPrecisionLevel` could still choose High precision when a 5-core machine had enough VRAM/RAM.
Solution: Added the hard low-precision guard before any High/Ultra precision branch.
Rejected Alternatives: Rejected relying on `qualityTier == Low` indirectly because the precision resolver had its own independent logic.
Scalability potential: Low/MX350 uses Low math, Mid+ can still use richer math when the CPU/VRAM floor is real.
Hardware Impact: Expected low-end gain is avoiding high-precision math/shader branches on weak CPU bins; exact frame impact requires Unity profiler.

Problem: Bootstrap phase mixed allocator/event/data initialization with presentation warmup, weakening the mandated topological boot ladder.
Solution: Split cold boot into explicit methods: allocators, EventBus/telemetry queues, MMF storage, Data Monolith, then core services and presentation warmup.
Rejected Alternatives: Rejected leaving shader/VRAM presentation setup inside the memory/data prewarm phase.
Scalability potential: Low tier reaches core readiness with fewer presentation dependencies; High/Ultra still gets SVC/keyword preparation before activation.
Hardware Impact: 0 us hot path. Cold boot gains are deterministic ordering, not measured speed.

Problem: Shader math LOD keyword selection depended on helper side effects, making the boot evidence weaker than the prompt demanded.
Solution: `GameBootstrapper` now explicitly enables `_MATH_LOD_LOW` or `_MATH_LOD_HIGH` based on `GlobalRegistry.MathPrecision` before calling the shared `DistanceMath` push.
Rejected Alternatives: Rejected relying only on `DistanceMath.PushShaderMathLod` because the directive required explicit global keyword calls in boot.
Scalability potential: Low path prewarms cheap variants; High/Ultra prewarms high-precision visual variants.
Hardware Impact: ~8 us cold keyword switch; no hot-path cost.

Problem: First compile verification attempt timed out, leaving no evidence for loop 1.
Solution: Re-ran `dotnet build Hecton8.Core.csproj` with a longer timeout.
Rejected Alternatives: Rejected marking tasks complete based on static inspection only.
Scalability potential: Not runtime code.
Hardware Impact: Build result: 0 warnings, 0 errors, elapsed 48.64s. Runtime verification remains PENDING VERIFICATION.

## Loop 2 Decisions

Problem: A managed `Task.Run` path remained in MMF read prefetching, which can borrow threadpool capacity unpredictably during boot-adjacent storage work.
Solution: Replaced it with one named persistent background `Thread` and removed `System.Threading.Tasks` from the storage file. Bootstrap domain handshake already uses `Awaitable.BackgroundThreadAsync()` and returns through `Awaitable.MainThreadAsync()`.
Rejected Alternatives: Rejected threadpool dispatch because it gives no stable affinity or capacity control. Rejected synchronous directory preparation on the main thread because it can stall scene transition.
Scalability potential: Low/MX350 avoids threadpool contention with audio/main-thread work; High/Ultra keeps I/O preparation off the frame lane without adding per-frame cost.
Hardware Impact: 0 us hot path. Cold path trades one explicit persistent thread allocation for removal of managed task scheduling jitter.

Problem: Missing ocean kinematics plugins could let the bootstrapper load a world with invalid physics contracts, turning a dependency error into undefined runtime behavior.
Solution: Added a reflection fast-fail gate against `Hecton8.Plugins` that requires at least one concrete `IOceanKinematics` implementation before world load proceeds.
Rejected Alternatives: Rejected soft warnings and late null-provider checks after scene activation.
Scalability potential: Tier independent; deterministic failure protects all hardware profiles from invalid boot state.
Hardware Impact: ~70 us cold assembly/type scan; 0 us after boot.

Problem: Burst worker defaults can saturate all logical CPU cores, starving the OS, main thread, and audio on i3/MX350-class machines.
Solution: Clamped Unity Job workers to `ProcessorCount - 1`, further bounded by `JobsUtility.JobWorkerMaximumCount`.
Rejected Alternatives: Rejected Unity default saturation and a fixed worker count because both mis-handle low-end and high-end machines.
Scalability potential: Low tier preserves one CPU lane for responsiveness; High/Ultra still uses all but one logical core for job throughput.
Hardware Impact: ~4 us cold configuration; expected low-end gain is lower scheduling starvation risk rather than measurable hot-path math savings.

Problem: VSync and uncapped frame pacing make BIOS performance budgets dependent on monitor refresh and driver state.
Solution: Forced `QualitySettings.vSyncCount = 0` and set the custom cap through `Application.targetFrameRate = 60`.
Rejected Alternatives: Rejected monitor VSync as policy and rejected tier-dependent caps that could hide bottlenecks during boot verification.
Scalability potential: Low/MX350 gets predictable 60 FPS pacing; High/Ultra can spend surplus frame budget on visual systems after core readiness.
Hardware Impact: ~2 us cold setting write; frame pacing stability is the expected gain.

Problem: Registry-owned NativeArrays and pooled services need deterministic reverse shutdown; relying on Unity object destruction order is not a disposal model.
Solution: Added `IServiceShutdown.DisposeAll()` as the named facade and routed `GlobalRegistry.DisposeAllRegisteredServices()` through reverse slot iteration.
Rejected Alternatives: Rejected unordered resets and direct `IDisposable.Dispose()` calls because service-owned shutdown paths may include more than disposable field cleanup.
Scalability potential: Tier independent; prevents persistent native-memory residue across all profiles.
Hardware Impact: ~3 us per registered shutdown service on exit; 0 us hot path.

Problem: Loop 2 build verification failed after BIOS edits, but compiler errors were in non-BIOS files changed by other workstreams.
Solution: Recorded the external compile wall and did not edit `Fauna` or `Voxel` domain files from the BIOS prompt.
Rejected Alternatives: Rejected cross-domain repairs because the prompt boundary limits this agent to core/bootstrap/memory infrastructure unless a critical cross-domain interface requires it.
Scalability potential: Not runtime code.
Hardware Impact: Build attempt: 0 warnings, 9 errors, elapsed 89.94s. Error files: `PredatorCognitionDomain.cs`, `VoxelDeltaProcessor.cs`. No CORE_BOOTSTRAP-edited file appeared in the compiler error set.

## Loop 3 Decisions

Problem: A crash during bootstrap previously risked leaving no compact restart signal for safe-mode routing.
Solution: Verified the existing 32-byte unmanaged `boot.bin` marker path records Started, PhaseStarted, ServiceStarted, CoreReady, WorldGen, Complete, and Fatal states. Next launch reads the marker and requests safe mode when the previous run did not finish Complete.
Rejected Alternatives: Rejected PlayerPrefs/string state because it is slower, less deterministic, and easier to corrupt under crash pressure.
Scalability potential: Tier independent; safe-mode fallback protects MX350 and high-end rigs equally from repeated boot loops.
Hardware Impact: ~12 us per marker write setup plus storage cost; no hot-path frame cost.

Problem: Texture tier content could still be fetched lazily after core readiness, causing first-world hitching.
Solution: Added a tier-label Addressables dependency prewarm for `Tier_Low` or `Tier_High` during presentation bootstrap before `CoreReady` is recorded and dispatched.
Rejected Alternatives: Rejected loading both tiers because MX350 cannot afford redundant texture residency. Rejected lazy mid-game download because it spends hitch budget during play.
Scalability potential: Low/MX350 pulls only low texture dependencies; High/Ultra gets the high-label texture group before gameplay.
Hardware Impact: One cold Addressables dependency operation; 0 us hot path after the dependency group is warm.

Problem: Lore/Encyclopedia MMF support existed, but a boot-time owner could still open payload memory and scratch buffers before the player asks for lore.
Solution: Added `LoreEncyclopediaLazyProxy`, which stores index/payload paths only and creates `LoreMmfEncyclopedia` on first `TryLoadEntryUtf16` call.
Rejected Alternatives: Rejected boot-time string hydration and rejected scene-object lookup for encyclopedia access.
Scalability potential: Low/MX350 keeps boot memory lean; High/Ultra still gets full MMF-backed lore retrieval on demand.
Hardware Impact: 0 us boot payload read. First-use cost is deferred to a user-facing lore request.

Problem: High-traffic registry lookups can become interface dispatch noise when systems repeatedly resolve input, physics, tick manager, telemetry, or audio.
Solution: Verified `[ThreadStatic]` caches in `GlobalRegistry` for those service slots.
Rejected Alternatives: Rejected dictionary-backed service location and repeated interface slot lookup per access.
Scalability potential: Tier independent; cheaper service reads leave more frame budget for visual overkill on high-end and less CPU pressure on low-end.
Hardware Impact: Estimated ~20 ns saved per cached registry read after first hit on each thread.

Problem: Explicit static constructors on registry services can execute hidden logic before BIOS has ordered boot dependencies.
Solution: Verified `BootstrapStaticConstructorAuditor` scans editor assemblies for explicit static constructors on `ISystem` implementers and throws.
Rejected Alternatives: Rejected manual review and runtime post-fact detection.
Scalability potential: Tier independent; deterministic boot policy.
Hardware Impact: Editor-only audit; 0 us player hot path.

Problem: Loop 3 required a fresh compile after Addressables, lazy proxy, heartbeat, and GC edits.
Solution: Re-ran `dotnet build Hecton8.Core.csproj`.
Rejected Alternatives: Rejected carrying forward the earlier external compile wall as current evidence after other agents fixed their files.
Scalability potential: Not runtime code.
Hardware Impact: Build result: 3 warnings, 0 errors, elapsed 128.35s. Warnings are unused fields in `ProceduralWreckGenerator.cs`, outside CORE_BOOTSTRAP.

## Loop 4 Decisions

Problem: Heartbeat polling lived mostly in `IServiceHeartbeat`; the task required an `ISystem`-visible `TickCount` and blackbox dump on freeze.
Solution: Added a default `ISystem.TickCount`, wired bootstrapper and RuntimeWatchdog to sample it, limited bootstrap sampling to 60-second cadence, and routed stale counters into `CrashTelemetryBuffer.ReportRuntimeWatchdogStall`.
Rejected Alternatives: Rejected reflection lookup per service and rejected warning-only stale heartbeat reporting.
Scalability potential: Low/MX350 pays no per-frame scan; High/Ultra get the same watchdog evidence without stealing frame budget.
Hardware Impact: Registry scan every 60 seconds; 0 us per-frame hot path.

Problem: Scene activation must not proceed while resident world prefab pools are still warming.
Solution: Verified `LoadMainMenuAsync` keeps `allowSceneActivation=false` and waits for `WaitForBootstrapActivationGatesAsync`, which checks `PersistentWorldRegistry.AreResidentWorldPrefabPoolsReady()`, before releasing scene activation.
Rejected Alternatives: Rejected progress-only scene activation and rejected activating before resident pools report ready.
Scalability potential: Low tier gets deterministic loading gates; High/Ultra avoids visible pop-in despite heavier prefab pools.
Hardware Impact: Readiness polling only during activation; 0 us hot path.

Problem: The task forbids `foreach` over `_activeSystems` in bootstrapper.
Solution: Verified no `_activeSystems` symbol exists in `GameBootstrapper`; current service/dependency paths use indexed arrays.
Rejected Alternatives: Rejected introducing a compatibility collection or enumerator wrapper.
Scalability potential: Tier independent; no service-iteration allocator pressure.
Hardware Impact: 0 us additional work; avoids enumerator allocation class of errors.

Problem: Threaded Unity logs were flagged but not immediately mirrored as numeric telemetry bus payloads.
Solution: Added `TelemetryEventType.UnityLogFault`, `GlobalTelemetryBus.PublishUnityLogFault`, and routed `Application.logMessageReceivedThreaded` error/exception hashes directly to the bus.
Rejected Alternatives: Rejected Debug echo and raw string dump storage; only FNV-style numeric context hashes enter telemetry.
Scalability potential: Low/MX350 avoids slow console amplification; High/Ultra keeps full blackbox signal density.
Hardware Impact: O(n) hash over Unity-supplied log text on fault only; 0 allocation in BIOS path.

Problem: GC must stay enabled during cold boot allocations but be disabled before world activation consumes the saved frame budget.
Solution: `GameBootstrapper` disables `UnityEngine.Scripting.GarbageCollector.GCMode` immediately after `CoreReady` is written and before bootstrap-complete dispatch.
Rejected Alternatives: Rejected disabling GC before cold boot allocation work and rejected leaving GC enabled into scene activation.
Scalability potential: Low/MX350 avoids GC spikes during first world presentation; High/Ultra spends reclaimed stability on visual systems.
Hardware Impact: One cold property write; 0 us hot path.

Problem: Final build after task 19/20 polish was blocked again by concurrent work outside BIOS ownership.
Solution: Re-ran `dotnet build Hecton8.Core.csproj` and recorded the exact external compile wall without editing `World` prompt files.
Rejected Alternatives: Rejected cross-domain `ProceduralWreckGenerator` repair from BIOS because it belongs to another prompt stream.
Scalability potential: Not runtime code.
Hardware Impact: Final build attempt: 0 warnings, 3 errors, elapsed 10.18s. Error file: `ProceduralWreckGenerator.cs`. No CORE_BOOTSTRAP-edited file appeared in the compiler error set.

## OMEGA POLISH CHANGES

Problem: OMEGA required a post-completion anti-bloat pass across BIOS-owned edits.
Solution: Re-read `<POLISH_MANDATE id="OMEGA_POLISH">`, scanned the edited CORE files for managed iteration/string/new hazards, and reviewed domain boundaries against `Docs/Actual Domains of Project.txt`.
Rejected Alternatives: Rejected changing external world/fauna/construction compile errors from this prompt stream.
Scalability potential: Low/MX350 keeps cold boot deterministic and hot paths untouched; High/Ultra routes through the same BIOS matrix while allowing heavier presentation content after tier prewarm.
Hardware Impact: No new hot-path work added by OMEGA. Final blocking build errors are outside CORE_BOOTSTRAP.

Problem: Honest calculation audit.
Solution: No physical simulation math was newly introduced in OMEGA. Existing BIOS benchmark already uses a reciprocal lookup for executed physics steps and bitwise low-tier gating in `ShouldForceLowTier`. Addressables tier selection remains a Low/High label branch, not a runtime texture scan.
Rejected Alternatives: Rejected adding LUTs where no per-frame simulation math exists.
Scalability potential: Low path uses `Tier_Low`, low math keywords, CPU<6/VRAM<3000 lock, and 60 FPS cap; High/Ultra uses `Tier_High`, high math keywords, and extra upload/texture budgets.
Hardware Impact: 0 us hot path; one cold benchmark reciprocal lookup replaces a division.

Problem: Zero-GC purge.
Solution: New allocations are cold or fault-only: persistent MMF prefetch thread, lazy lore MMF runtime on first lore request, Addressables handle, and static telemetry event metadata. Threaded log redirection hashes Unity-provided strings and stores numeric telemetry only.
Rejected Alternatives: Rejected raw string storage and Debug echo for threaded log faults.
Scalability potential: Low hardware avoids console amplification and boot-time lore payload hydration; high hardware retains crash signal density.
Hardware Impact: 0 allocation in per-frame BIOS paths; O(n) hash only when Unity emits an error/assert/exception.

Problem: Cache locality and service scans.
Solution: Heartbeat sampling is staggered to a 60-second interval, registry services are sampled by slot index, and existing `[ThreadStatic]` registry caches remain the hot-path lookup mechanism.
Rejected Alternatives: Rejected reflection polling and foreach enumeration over registry services.
Scalability potential: Low/MX350 pays no per-frame scan; High/Ultra gets identical watchdog evidence.
Hardware Impact: 255-slot scan every 60 seconds; no frame-lane scan.

Problem: Silo and build health.
Solution: Domain check maps edited code to Echelon 1: BIOS Bootstrapper, EventBus/telemetry, Data Archivist/MMF, Crash Telemetry, Scalability Dictator, and Tick Dispatcher. The only cross-domain addition is `LoreEncyclopediaLazyProxy`, justified by task 13 and isolated to lazy MMF access with no boot string hydration.
Rejected Alternatives: Rejected repairing `ProceduralWreckGenerator.cs` because it belongs to Echelon 2 Procedural Wreckage.
Scalability potential: The lore proxy defers payload memory on low hardware and preserves high-tier full encyclopedia retrieval on demand.
Hardware Impact: Final build attempt blocked externally: `ProceduralWreckGenerator.cs` has 3 compile errors. Last successful build before concurrent external edits: 3 warnings, 0 errors, elapsed 128.35s.

Final Git Diff:
```
Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs
Assets/_Project/Scripts/Core/BootstrapContracts/InputBindingServiceContracts.cs
Assets/_Project/Scripts/Core/GlobalRegistry.cs
Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs
Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs
Assets/_Project/Scripts/Core/RuntimeWatchdog.cs
Assets/_Project/Scripts/CrashTelemetryBuffer.cs
Assets/_Project/Scripts/Optimization/HardwareProfiler.cs
Assets/_Project/Scripts/SaveBinaryStorage.cs
Assets/_Project/Scripts/Narrative/LoreEncyclopediaLazyProxy.cs (new)
Docs/Tasks/Status_CORE_BOOTSTRAP.md (new)
Docs/AgentLogs/Rationale_CORE_BOOTSTRAP.md (new)
```

Git Diff Stat:
```
.../_Project/Scripts/Bootstrap/GameBootstrapper.cs | 389 ++++++++-
.../InputBindingServiceContracts.cs                |  81 ++
Assets/_Project/Scripts/Core/GlobalRegistry.cs     |  61 +-
.../Scripts/Core/GlobalRegistryContracts.cs        |  24 +-
Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs | 101 ++-
Assets/_Project/Scripts/Core/RuntimeWatchdog.cs    |  94 ++-
Assets/_Project/Scripts/CrashTelemetryBuffer.cs    | 261 +++---
.../Scripts/Optimization/HardwareProfiler.cs       | 112 ++-
Assets/_Project/Scripts/SaveBinaryStorage.cs       | 881 +++++++++++++--------
9 files changed, 1431 insertions(+), 573 deletions(-)
```
