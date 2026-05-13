# Rationale - CROSS_PLATFORM_IL2CPP_SENTINEL

Status: PENDING VERIFICATION until Unity console/build logs confirm.

## Initial Decision Journal

Problem: Quest/ARM64 IL2CPP can strip generic and interface-heavy code that works under Editor JIT.
Solution: Add explicit Editor build pipeline, forced IL2CPP/ARM64 settings, linker preservation, and platform-specific graphics constraints.
Rejected Alternatives: Manual PlayerSettings changes are not reproducible; Mono/JIT Editor success is not evidence for Quest.
Scalability potential: Low uses smaller Android package and strict stripping; Middle keeps deterministic build hooks; High/Ultra reuse saved CPU/VRAM budget for richer visuals, not platform entropy.
Hardware Impact: Expected runtime gain is stability, not frame time. Build-time stripping may reduce APK size and memory pressure on Quest/i3/MX350; exact MB pending build artifact.

Problem: Debug crash testing can become a shipping crash vector.
Solution: Gate ForceCrash behind `UNITY_EDITOR || DEVELOPMENT_BUILD` and make it explicit, not automatic.
Rejected Alternatives: RuntimeInitializeOnLoad crash hooks and always-on debug UI allocate/ship risk before GameBootstrapper.
Scalability potential: Low devices keep debug subsystem compiled out in release; high-tier dev builds can validate crash telemetry.
Hardware Impact: Release runtime 0 us. Development debug path cost only when manually invoked.

Problem: Compute shaders may compile in DirectX and fail under Metal/Quest if thread groups or pragmas are wrong.
Solution: Inspect compute kernels, add explicit compute requirements where missing, and keep numthreads within 1024 total threads per group.
Rejected Alternatives: Assuming DirectX compile means Metal safety; hardcoding 256-thread desktop assumptions.
Scalability potential: Low/Middle keep 64-thread portable dispatch; High/Ultra may add larger variants only after capture.
Hardware Impact: Prevents failed builds/crashes. Runtime microseconds saved pending GPU capture.

## Loop 1 Decisions

Problem: Platform builds were not reproducible from a terminal entry point, and Android could silently fall back to Mono/JIT-like assumptions in Editor validation.
Solution: Created global `HectonBuildPipeline` with `BuildAndroidQuest`, `BuildMacSilicon`, and `BuildWindows`; Android build forces IL2CPP, ARM64-only, Vulkan, High stripping, and Burst AOT settings.
Rejected Alternatives: Menu-only Editor tooling, manual PlayerSettings changes, ARMv7 compatibility masks, and restoring lax settings after build.
Scalability potential: Low uses ARM64-only artifact and High stripping to reduce APK pressure; Middle keeps deterministic CI; High/Ultra spend saved platform risk on richer visuals after device proof.
Hardware Impact: Release runtime 0 us. Expected low-end gain is reduced install/memory pressure; exact APK bytes pending Android build.

Problem: Burst AOT state can depend on Editor preferences and missing Android target settings.
Solution: Build hook sets `BurstCompiler.Options.EnableBurstCompilation` and writes `ProjectSettings/BurstAotSettings_Android.json` with ARMv8A/AArch64 mask when Android build executes.
Rejected Alternatives: Depending on existing Windows Burst settings or Burst menu state.
Scalability potential: Low/Middle get predictable SIMD/native job output on Quest; High/Ultra can widen visual systems only after Burst player proof.
Hardware Impact: Runtime cost is no additional managed work; Burst may reduce CPU time in jobs, exact microseconds require player profile.

Problem: High managed stripping can erase generic lanes and native container wrappers that Editor JIT keeps reachable.
Solution: Added `Assets/link.xml` preserving `SignalBus<T>`, signal interfaces, `GlobalRegistry`, `GlobalSignals`, and Unity native container generic shells.
Rejected Alternatives: Lowering managed stripping level or adding reflection-driven keepalive code.
Scalability potential: Low keeps smaller build while preserving correctness; High/Ultra preserve the same deterministic signal backbone.
Hardware Impact: 0 us runtime. Size impact pending build report.

Problem: Loop 1 Unity compile did not reach green state.
Solution: Ran Unity script validation and console check; new `HectonBuildPipeline.cs` has 0 diagnostics, but global compile is blocked by unrelated existing errors in `PlayerCriticalProceduralAudioRenderer.cs` and `EcosystemDirector.cs`.
Rejected Alternatives: Reporting success from static validation or modifying unrelated audio/ecosystem domains without authority.
Scalability potential: No platform claim until dependency owners clear compile.
Hardware Impact: 0 us runtime from this loop; blocked build prevents APK size measurement.

## Loop 2 Decisions

Problem: `GlobalRegistry` resolves interface/concrete service slots through generic/static caches and explicit `Type` comparisons; IL2CPP High stripping can remove members that Editor JIT leaves intact.
Solution: Added `[Preserve]` to `GlobalRegistry`, public generic getters, private generic registration/resolution methods, cold slot resolver, and generic slot cache.
Rejected Alternatives: Runtime reflection keepalive, assembly-wide preserve only, or lowering Android stripping.
Scalability potential: Low keeps High stripping with stable service lookup; Middle/High/Ultra preserve the same registry contract without adding runtime work.
Hardware Impact: 0 us runtime. Expected low-end benefit is fewer missing-method crashes, not frame-time reduction.

Problem: `GlobalSignals` uses generic signal lanes and unmanaged interface contracts that are easy to under-root in AOT/linker analysis.
Solution: Added `[Preserve]` to signal marker interfaces, transformer interface, `SignalBusRegistry`, `SignalBus<T>`, and `GlobalSignals`.
Rejected Alternatives: Forcing every closed signal lane through runtime reflection or weakening stripping.
Scalability potential: Low retains typed lane determinism; High/Ultra keep the same signal backbone while spending performance budget on visual density.
Hardware Impact: 0 us runtime. Memory/size impact pending IL2CPP build report.

Problem: macOS architecture API in this Unity 6000 install does not expose the prompt's `MacArchitecture.ARM64` enum.
Solution: Used verified project API `PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, 1)`; MCP package helper confirms 1 = arm64.
Rejected Alternatives: Inventing `MacArchitecture.ARM64`, universal/Rosetta builds, or x86_64 default.
Scalability potential: Low/Middle avoid translation overhead on Apple Silicon; High/Ultra can spend native CPU headroom on presentation systems.
Hardware Impact: 0 us runtime in code; native player avoids Rosetta translation tax once built.

Problem: Compute shaders needed Metal compliance evidence, not DirectX-only assumptions.
Solution: Added explicit compute requirement pragmas. Verified via Unity `ShaderUtil.IsGraphicsAPISupported(... Metal)` that both compute shaders report Metal support. Kernel threadgroups: `Hecton_MarineSnow` 1 or 64 total threads; `InstanceCulling` 64 total threads, all below Metal's 1024 threadgroup ceiling.
Rejected Alternatives: Increasing threadgroup size for desktop throughput or assuming current editor graphics API is sufficient.
Scalability potential: Low/Middle keep portable 64-thread groups; High/Ultra can add separate variants only after GPU capture.
Hardware Impact: Runtime unchanged. Metal import shows existing warning about `SampleAbyssalFlowField` potential uninitialized use; no console shader errors were emitted, but this remains a shader hygiene risk.

Problem: Loop 2 compile did not reach a clean project state.
Solution: Validated changed C# files independently and checked Unity console; current blockers are unrelated `Terrain` namespace/type errors in world/geology files.
Rejected Alternatives: Editing world/geology domain without dispatch authority or pretending compile is green.
Scalability potential: Platform verification remains gated until dependency owners restore compile.
Hardware Impact: 0 us runtime from this loop.

## Loop 3 Decisions

Problem: Native crash telemetry needs a way to prove IL2CPP crash survival without shipping a permanent crash vector.
Solution: Added `IL2CPPCrashTelemetryDebugMenu`, a manual component/context-menu bridge that calls `CrashTelemetryBuffer.EnsureRuntimeInstance()` and `UnityEngine.Diagnostics.Utils.ForceCrash` only under `UNITY_EDITOR || DEVELOPMENT_BUILD`.
Rejected Alternatives: `RuntimeInitializeOnLoadMethod`, automatic debug UI creation, `Update` polling, or a release-active crash path.
Scalability potential: Low/release builds carry no crash action; development builds on high-tier machines can validate crash dumps before Quest deployment.
Hardware Impact: Release runtime 0 us. Manual development invocation allocates only because it deliberately arms telemetry immediately before terminating.

Problem: Pre-bootstrap runtime init hooks may allocate before `GameBootstrapper` owns the frame.
Solution: Ran a static audit for early runtime initialize hooks with allocation/search/log patterns and wrote suspects to `RuntimeInitAudit_CROSS_PLATFORM_IL2CPP_SENTINEL.md`.
Rejected Alternatives: Rewriting 33 cross-domain suspects during an unrelated platform build task.
Scalability potential: Low devices benefit once owners migrate these to bootstrap-controlled init; High/Ultra get deterministic startup instrumentation.
Hardware Impact: New code adds 0 us. Existing suspects remain PENDING VERIFICATION and require owner cleanup.

Problem: Android build result file must exist even when the build method cannot execute.
Solution: `HectonBuildPipeline` writes `Build_Result_[Platform].txt` on real invocation; because current compile blockers prevent `HectonBuildPipeline` from loading, wrote `Build_Result_AndroidQuest.txt` with exact current compiler errors and 0-byte artifact status.
Rejected Alternatives: Reporting an APK size without an APK or opening another Unity instance against an already-open project.
Scalability potential: Low/Middle CI gets deterministic result files once compile is green; High/Ultra same.
Hardware Impact: 0 us runtime. APK/memory impact remains unmeasured.

Problem: `UNITY_EDITOR` blocks can hide runtime data that vanishes under IL2CPP/player builds.
Solution: Scanned runtime script folders excluding Editor directories; found 1648 `#if UNITY_EDITOR` occurrences and logged the audit. New platform code uses release-safe inert methods instead of stripping whole gameplay data paths.
Rejected Alternatives: Mass editing noisy debug/editor branches without owner-specific semantic proof.
Scalability potential: Low build safety improves only after targeted owner reviews; High/Ultra unaffected until those risks are retired.
Hardware Impact: 0 us runtime from this task.

Problem: Android build method execution is blocked.
Solution: Queried the current editor domain; `HectonBuildPipeline` type is not loaded because compilation is blocked. Current compiler errors are duplicate `SectorHydratedSignal`/`StructLayout` in `GlobalSignals.cs`, missing `ILateFrameTickable.LateFrameTick()` in `HectonFluidEngine`, and failed entry-point discovery.
Rejected Alternatives: Claiming method execution or editing unrelated fluid/signal gameplay definitions from this platform pass.
Scalability potential: No platform artifact until compile owners clear the wall.
Hardware Impact: 0 us runtime.

## Loop 5 OMEGA Polish

Problem: OMEGA required anti-bloat review for runtime `string.Format`, generics, and `dotnet build Hecton8.Core.csproj`.
Solution: Scanned runtime folders excluding Editor for `string.Format`; found 19 pre-existing occurrences in `HectonDiscoveryManager.cs`, `LocalizationManager.cs`, `SaveSlotUI.cs`, and `ScannerTool.cs`. New platform/crash files contain 0 `string.Format` calls.
Rejected Alternatives: Editing unrelated UI/scanner/localization systems while the project is already in a compile wall.
Scalability potential: Low devices still need those runtime formatting paths removed by their owners; platform changes add none.
Hardware Impact: New code 0 us runtime. Existing string formatting can allocate on UI/scanner paths; measurements absent.

Problem: IL2CPP can struggle with unnecessary generic surfaces.
Solution: Reviewed new code; no new runtime generic container or reflection-heavy path was introduced. Existing `SignalBus<T>`/`NativeQueue<T>` generics are architectural infrastructure and were preserved instead of replaced because concrete conversion would be a cross-system rewrite.
Rejected Alternatives: Replacing the signal bus with concrete lanes in a platform pass.
Scalability potential: Low/Middle keep existing deterministic signal architecture; High/Ultra unaffected until a dedicated signal-lane specialization pass is assigned.
Hardware Impact: 0 us runtime from this work.

Problem: Required `dotnet build Hecton8.Core.csproj` does not pass.
Solution: Ran the command. Result: failed after 00:01:09.59 with 150 errors, 0 warnings. Summary written to `DotnetBuild_Hecton8Core_CROSS_PLATFORM_IL2CPP_SENTINEL.txt`.
Rejected Alternatives: Stopping at Unity console errors or hiding the failed build.
Scalability potential: No platform/AOT proof until assembly references and duplicate concurrent edits are reconciled.
Hardware Impact: 0 us runtime. Build artifact unavailable.

## Loop 4 Self-Review

Problem: Runtime-adjacent debug code can quietly violate the zero-GC/bootstrap rules after the main implementation pass.
Solution: Scanned new/changed platform files for `System.Reflection`, `RuntimeInitializeOnLoadMethod`, `string.Format`, `Update`, `LateUpdate`, `FixedUpdate`, and new allocations. Findings: only Editor-only build pipeline allocations and exceptions; crash bridge has no hot path and no auto init.
Rejected Alternatives: Trusting visual inspection or hiding crash code behind a stripped whole-file preprocessor branch.
Scalability potential: Low/release carries no active crash path; development builds keep the same manual verification hook.
Hardware Impact: 0 us release runtime.

Problem: Concurrent agents modified many files in the same working tree, including `GlobalSignals.cs` and `GlobalRegistry.cs`.
Solution: Preserved existing changes and limited edits to linker/preserve/platform concerns. Current compile blockers in touched files are from pre-existing/concurrent signal additions, not from the preserve attributes.
Rejected Alternatives: Reverting or rewriting neighboring agent work.
Scalability potential: Integration remains possible once owners reconcile duplicate signal definitions and interface contract drift.
Hardware Impact: 0 us runtime.

## Loop 6 Compile Wall Reduction

Problem: `VehicleSubOsCockpitRuntime` depended on `Hecton8.UI.Diegetic.Contracts` only to implement a read-model interface with no discovered consumers, and the parent `Hecton8.Core` assembly was failing before platform build code could load.
Solution: Removed the interface import/implementation while retaining the public read-model properties. This cuts the fragile cross-asmdef edge without removing data.
Rejected Alternatives: Moving contract files between assemblies or adding more Core references during a compile wall.
Scalability potential: Low/Middle keep the existing cockpit data on the concrete runtime; High/Ultra can reintroduce a contract through a clean owner-owned asmdef pass.
Hardware Impact: 0 us runtime. Compile dependency surface reduced.

Problem: `HardwareThermalService` referenced `Hecton8.Core.ScalabilityTierProfiles.LowMx350` across an assembly boundary where Unity could not resolve the bootstrap-contract type.
Solution: Passed the documented low-profile byte `0` directly to `GlobalRegistry.RegisterScalabilityTierOverride(byte)`.
Rejected Alternatives: Adding another assembly dependency or duplicating `ScalabilityTierProfiles` in the hardware leaf.
Scalability potential: Low/MX350 throttle policy remains explicit; High/Ultra are unaffected until throttling forces a low-tier override.
Hardware Impact: 0 us normal runtime. Under throttling, the same low-tier policy applies without a compile-time contract edge.

Problem: `GlobalDataVault` is a memory leaf assembly but concurrent edits reintroduced upward calls into `GlobalRegistry`, `GlobalSignals`, and `NativeMemorySentinel`, breaking assembly order and IL2CPP hygiene.
Solution: Removed upward references, kept defrag/audit state local, restored `System.Diagnostics.Stopwatch`, and ensured `VaultGapAuditResult`/`VaultGapAuditJob` are local structs inside the memory assembly.
Rejected Alternatives: Making `Hecton8.Core.Memory` depend on parent `Hecton8.Core`, which would create an assembly cycle and fragile IL2CPP ordering.
Scalability potential: Low devices keep deterministic 64-byte alignment and fragmentation telemetry without registry coupling; High/Ultra can spend memory headroom while the vault still audits fragmentation locally.
Hardware Impact: 0 us from the compile repair. Defrag/audit work remains cold/FrostTick-owned; no per-frame platform cost added by this pass.

Problem: Android build method still needed an exact artifact size or exact failure after compile-wall reduction.
Solution: Invoked Unity CLI with `-executeMethod HectonBuildPipeline.BuildAndroidQuest` and wrote `Build_Result_AndroidQuest.txt`. Unity exited before method execution with return code 1, likely because the project was already open; the exact log tail is recorded.
Rejected Alternatives: Fabricating APK size or killing the user's active Unity editor to free the project lock.
Scalability potential: CI path remains deterministic once run from a clean editor/batch process; Low/Middle/High/Ultra build settings are already encoded in `HectonBuildPipeline`.
Hardware Impact: APK size unavailable. Runtime impact remains 0 us because no player artifact was produced.

Problem: OMEGA `dotnet build Hecton8.Core.csproj` needed a fresh result after compile-wall fixes.
Solution: Re-ran `dotnet build .\Hecton8.Core.csproj --no-restore`; current result is 91 errors, 0 warnings, logged to `DotnetBuild_Hecton8Core_CROSS_PLATFORM_IL2CPP_SENTINEL.txt`.
Rejected Alternatives: Reporting the older 150-error result after source and assembly edits changed the state.
Scalability potential: No platform/AOT proof until generated `.csproj` references catch up with asmdef split assemblies or those dependencies are moved behind contracts.
Hardware Impact: 0 us runtime. Build artifact remains unavailable.

## Loop 7 Memory-Leaf Re-Verification

Problem: Concurrent edits reintroduced a direct `MemoryAddressShiftSignal` publish through `Hecton8.Core.GlobalSignals` inside `GlobalDataVault`, pulling the memory leaf back toward the parent core assembly and reproducing the same IL2CPP/asmdef compile risk.
Solution: Removed the signal publish from the relocation path and kept relocation state local to metadata/block descriptors. Restored `System.Diagnostics.Stopwatch` for cold watchdog timing and verified `VaultGapAuditResult`/`VaultGapAuditJob` are local memory-assembly structs.
Rejected Alternatives: Adding a parent `Hecton8.Core` or signal assembly reference to `Hecton8.Core.Memory`; that creates upward coupling and a fragile AOT assembly graph.
Scalability potential: Low devices keep deterministic vault relocation and 64-byte alignment audits without a signal bus dependency; Middle/High/Ultra can still spend memory headroom on richer systems while the vault remains a bounded leaf service.
Hardware Impact: 0 us new per-frame cost. Defrag/relocation remains cold/FrostTick-owned; saved risk is compile/AOT stability, not measured frame time.

Problem: The live source alternated between plain Jobs-only gap audit and a Burst/Mathematics-qualified variant during concurrent edits.
Solution: Kept `Hecton8.Core.Memory.asmdef` references broad enough for the live qualified source requirements: `Unity.Collections`, `Unity.Jobs`, `Unity.Burst`, and `Unity.Mathematics`.
Rejected Alternatives: Chasing a narrower asmdef during active concurrent edits and re-breaking compile the moment `[Unity.Burst.BurstCompile]` or `Unity.Mathematics.math` returns.
Scalability potential: Low gets a compile-stable job graph; High/Ultra can Burst-compile cold audit jobs where present without changing runtime architecture.
Hardware Impact: 0 us runtime from asmdef references. Package reference cost is editor/build graph only.

Problem: Required `dotnet build Hecton8.Core.csproj` needed a current result after the memory-leaf repair.
Solution: Re-ran `dotnet build .\Hecton8.Core.csproj --no-restore`; current result is 90 errors, 0 warnings, logged to `DotnetBuild_Hecton8Core_CROSS_PLATFORM_IL2CPP_SENTINEL.txt`. No current errors mention `GlobalDataVault`, `VaultGapAudit`, `Stopwatch`, `GlobalSignals`, or `MemoryAddressShiftSignal`.
Rejected Alternatives: Treating Unity's inactive log as proof of current source failure, or claiming a green build from source grep alone.
Scalability potential: Platform/AOT proof is still blocked until Unity regenerates/uses current asmdef references or dependency owners move cross-domain types behind contracts.
Hardware Impact: 0 us runtime. Build artifact remains unavailable.
