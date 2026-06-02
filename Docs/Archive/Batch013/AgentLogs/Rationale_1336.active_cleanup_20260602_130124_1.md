# Rationale 1336 - SHADER_WARMUP_AND_VARIANT_COLLECTION_COMPILER

Date: 2026-05-26
Status: STATIC GREEN - LOOP 27 BUILD BLOCKED BY MISSING UNITY CSPROJ / HOST CPU AND GRAPHICSSTATE TRACE GAP - UNITY IMPORT/PROFILER PENDING

## Decision 001 - Prompt Source

Problem: User ordered extraction from root `current_batch.md`, but that file is absent.
Solution: Located live batch at `Docs/Tasks/CURRENT_BATCH.md` with CLI `rg`, extracted `<AGENT_PROMPT id="1336"...>` by regex including attributes.
Rejected Alternatives: Using archived batch files; reading neighboring prompts; guessing task count from chat text.
Scalability potential: No runtime effect. Prevents wrong-domain edits in multi-agent batch.
Hardware Impact: 0 us/frame. Avoided irrelevant code churn on i3/MX350.

## Decision 002 - Mandate Set

Problem: Shader warmup spans bootstrap, async, graphics, DTO layout, telemetry, and budgets.
Solution: Read eight relevant mandates before code: bootstrap sequence, zero-GC, async standard, shader stutter, URP hot path, ARM64 struct layout, crash telemetry, performance budgets.
Rejected Alternatives: Reading the whole registry; using only AGENTS.md; starting edits from prompt text alone.
Scalability potential: Low/Middle/High/Ultra path remains constrained by owned variants and boot gating, not runtime first-use compilation.
Hardware Impact: Static decision. Expected gameplay first-use shader spike removal; exact microseconds pending Unity profiler.

## Decision 003 - Initial Bootstrap Finding

Problem: Existing bootstrap contains shader variant collection fields but may still block the main thread.
Solution: Source scan identified `WarmConfiguredShaderVariantCollectionsAsync` and `collection.WarmUp()` in `GameBootstrapper.cs` as the primary target.
Rejected Alternatives: Adding `Shader.WarmupAllShaders`; moving warmup into gameplay; creating a new global rendering manager.
Scalability potential: Low/MX350 warms only curated collections during boot; Middle/High/Ultra may carry richer variant assets without changing gameplay authority.
Hardware Impact: Potentially removes first-use shader stalls; exact saved microseconds pending implementation and profiler proof.

## Decision 004 - Warmup API Route

Problem: Unity 6 local docs expose `ShaderWarmup.WarmupShaderFromCollection`, not a confirmed `ShaderVariantCollection.WarmUpAsync` method.
Solution: Use manifest-driven `ShaderWarmup.WarmupShaderFromCollection(collection, shader, setup)` and yield via `AwaitableDebtMonitor.NextFrameAsync` after a continuous GlobalQualityWeight batch size of 1/2/4/8.
Rejected Alternatives: `Shader.WarmupAllShaders`; legacy `collection.WarmUp()`; coroutine; dummy material instantiation.
Scalability potential: Low = one shader attempt/frame; Middle = two; High = four; Ultra = eight. Same truth route, higher boot cadence only.
Hardware Impact: Low i3/MX350 avoids a single blocking warmup wall. Saved gameplay microseconds pending profiler, expected to move driver stalls to boot veil.
First 20 Minutes moment: Boot -> world load.
Route impact: Flashlight/visor/PDA/caustic route shaders compile before simulation.
Proof required: Unity compile and profiler run.
Parked work rejected: Whole-project shader warmup.

## Decision 005 - Manifest Fail-Closed

Problem: SVC assets do not expose their shader list at runtime, so blind per-collection async warmup cannot prove coverage.
Solution: Add explicit `Shader[] shaderWarmupShaders` to `GameBootstrapper` and `BootstrapController`, wire current `00_BOOTSTRAP` scene with shader GUIDs from configured SVC assets.
Rejected Alternatives: Reflection into SVC internals; falling back to `collection.WarmUp()` when manifest is missing.
Scalability potential: Low/Middle/High/Ultra vary batch cadence, not manifest ownership.
Hardware Impact: 0 us/frame. Boot now fails closed instead of entering world with untracked compile debt.
First 20 Minutes moment: Boot.
Route impact: Startup correctness is visible before player control.
Proof required: Edit test checks scene/controller wiring.
Parked work rejected: Runtime scene search for shaders.

## Decision 006 - Telemetry Ring

Problem: Failure without a native black box would leave no reproducible proof of warmup stalls or missing assets.
Solution: Add explicit 64B `BootstrapTelemetryEntry`, allocate 300 entries from GlobalDataVault under local bootstrap buffer id `(BufferID)76000`, record native DTO states through DataVault writer locks.
Rejected Alternatives: `List<T>` timings; `Debug.Log` as primary telemetry; duplicate `74410` buffer ID.
Scalability potential: Fixed 19.2 KB ring across all devices. Ultra can generate more boot events but the ring remains bounded.
Hardware Impact: Fixed 64B writes on boot/failure only; no gameplay frame tax on i3/MX350.
First 20 Minutes moment: Boot.
Route impact: Boot failures become diagnosable, not anecdotal.
Proof required: Size/offset edit test and compile.
Parked work rejected: Expanding CrashTelemetryBuffer schema.

## Decision 007 - Blackbox Dump Route

Problem: The prompt requires `Docs/AgentLogs/Dump_1336_Bootstrapper.bin` on catastrophic warmup failure.
Solution: Copy the 19.2 KB native ring to a cold failure scratch buffer and queue a worker-thread `AsyncWriteManager.WriteAll` to the required raw binary path.
Rejected Alternatives: Writing only persistent-data-path logs; synchronous main-thread dump as the only route.
Scalability potential: No steady-state cost. Failure dump is constant size across weak and high-end devices.
Hardware Impact: 19.2 KB copy on fatal boot only; 0 us/frame during gameplay.
First 20 Minutes moment: Boot.
Route impact: Provides postmortem data if warmup blocks route entry.
Proof required: Failure branch compile and source audit.
Parked work rejected: Complex new telemetry exporter dependency.

## Decision 008 - Variant Compiler Scope

Problem: The collection must be strict, but full-project material dependency traversal would preserve useless permutations.
Solution: Add editor-only compiler scoped to `00_BOOTSTRAP`, `01_MAIN_MENU`, `02_HECTON_WORLD`, player, scanner, repair, flashlight, and VFX/tool material roots, with Unity stereo/instancing/lightmap/fog/additional-light keyword rejection and max 512 variants.
Rejected Alternatives: Whole-project SVC capture; keeping editor/debug/VR/lighting keywords by default.
Scalability potential: Low devices receive a bounded payload; Ultra can carry richer authored roots by explicitly extending the root list.
Hardware Impact: Lower import/build variant count. Runtime savings pending Unity profiler.
First 20 Minutes moment: Boot -> tool interaction -> hazard/VFX response.
Route impact: Covers the actual route visuals instead of hidden permutations.
Proof required: Run editor compiler and inspect `BOOTSTRAP_SHADER_VARIANT_COMPILER_1336.json`.
Parked work rejected: Automatic keyword inference from every material in the repository.

## Decision 009 - Build Scope

Problem: Full workspace build would compile unrelated dirty domains from 20+ active agents and saturate the host.
Solution: After CPU/dotnet/csc gate, ran `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal /m:1`; it compiled modified runtime bootstrap files with 0 warnings and 0 errors in 2.62s.
Rejected Alternatives: Full solution build; repeated build spam; launching build while CPU was above 50%.
Scalability potential: No runtime impact. Keeps validation focused on the modified runtime assembly.
Hardware Impact: 0 us/frame. Host CPU load controlled after initial harness timeout.
First 20 Minutes moment: Boot.
Route impact: Runtime bootstrap code is compiler-clean.
Proof required: Unity refresh/import to compile new editor/test files and run profiler.
Parked work rejected: Claiming editor/test execution without Unity asset import.

## Decision 010 - Measurement Honesty

Problem: The report contract asks for measured warmup milliseconds, but no Unity playmode/profiler route was executed in this shell session.
Solution: Reported `measuredWarmupMilliseconds: null` with a specific reason in `BOOTSTRAP_OPTIMIZATION_REPORT_1336.json` instead of fabricating a number.
Rejected Alternatives: Fake profiler numbers; extrapolating shader driver compile time from static code.
Scalability potential: Measurement must be taken on Low/Middle/High/Ultra hardware profiles during real Unity boot.
Hardware Impact: No runtime change. Prevents false performance claims.
First 20 Minutes moment: Boot -> world load.
Route impact: Remaining proof is explicit and not hidden.
Proof required: Unity profiler capture during `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`.
Parked work rejected: Chat-only unverifiable performance claim.

## Decision 011 - APEX Native Field Re-Audit

Problem: The rejection correctly targeted the risk of claiming memory sovereignty without proving native field ownership after code changes.
Solution: Re-extracted AGENT_PROMPT 1336 from `Docs/Tasks/CURRENT_BATCH.md`, scanned the four touched C# files for `NativeArray<T>`, `NativeList<T>`, `NativeQueue<T>`, `NativeParallelHashMap<K,V>`, `NativeParallelMultiHashMap<K,V>`, and `UnsafeList<T>` field declarations, and classified every native collection hit as method-local/out transient view. Persistent native field count is 0.
Rejected Alternatives: Counting raw text hits as fields; ignoring pre-existing method signatures; storing a persistent `NativeArray<BootstrapTelemetryEntry>` on the bootstrapper.
Scalability potential: Low/Middle/High/Ultra all keep the same Vault-owned 300-entry ring. Quality changes warmup cadence only, not memory ownership.
Hardware Impact: Prevents stale raw aliases during Vault compaction. i3/MX350 impact is avoided crash/stall risk, not steady-frame CPU cost.
First 20 Minutes moment: Boot -> world load.
Route impact: Shader telemetry survives warmup failure without blocking DataVault compaction.
Proof required: Roslyn/static ledger and final scoped compile once CPU gate permits.
Parked work rejected: Cross-domain memory enum edits as proof of ownership.

## Decision 012 - Local BufferID Instead Of H8Memory Edit

Problem: Adding `BootstrapShaderWarmupTelemetryRing` to `H8Memory.cs` touched a core memory contract outside the assigned bootstrap domain.
Solution: Removed that enum edit and kept `private const BufferID BootstrapShaderWarmupTelemetryRingBufferId = (BufferID)76000` inside `GameBootstrapper`. The buffer remains explicit, stable, and local to the bootstrap owner.
Rejected Alternatives: Leaving a cross-domain enum mutation; reusing an unrelated existing BufferID; allocating a managed telemetry list.
Scalability potential: Stable ID and DTO contract across Low/Middle/High/Ultra. Ultra does not gain a new route or schema, only faster batch cadence.
Hardware Impact: 0 us/frame. Reduces integration blast radius on weak machines by keeping Core.Memory untouched by 1336.
First 20 Minutes moment: Boot.
Route impact: Boot shader warmup remains isolated from global memory enum churn.
Proof required: `git diff` confirms no 1336 change remains in `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`.
Parked work rejected: Broad BufferID registry refactor.

## Decision 013 - Compaction-Aware Lock Proof

Problem: A telemetry write is only safe if the Vault can reject writes during compaction and the bootstrapper releases immediately.
Solution: Verified `GlobalDataVault.TryAcquireWriteLock<T>` checks `Volatile.Read(ref _compactionFence)` before and during acquisition; `RecordBootstrapShaderWarmupTelemetry` wraps the write in `try/finally` and calls `ReleaseWriteLock` before any await boundary.
Rejected Alternatives: Holding a pinned ring view across warmup frames; resolving once and caching the NativeArray; queueing writes through a managed list.
Scalability potential: Same lock window on all tiers: one fixed DTO store. Low devices do not pay extra; Ultra does not extend lock lifetime.
Hardware Impact: A single boot telemetry event writes 64 bytes under a short lock. No gameplay hot-frame tax on i3/MX350.
First 20 Minutes moment: Boot -> first controllable frame.
Route impact: The simulation cannot begin after shader warmup failure, but Vault compaction remains free to proceed between events.
Proof required: final compile and `Docs/Reports/BOOTSTRAP_APEX_REAUDIT_1336.json`.
Parked work rejected: async lock lifetime across `NextFrameAsync`.

## Decision 014 - APEX Compile Gate Discipline

Problem: A final compile was required after APEX corrections, but the host was saturated above the 50% CPU threshold.
Solution: Waited until CPU fell below threshold and no `dotnet`/`csc` process was active, then ran one scoped build: `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal /m:1`. Result: 0 warnings, 0 errors, 0.83s.
Rejected Alternatives: Launching build while CPU was 100%; running full solution; repeating builds after every scanner.
Scalability potential: No runtime change. Validation stays limited to the bootstrap runtime assembly and avoids starving sibling agents.
Hardware Impact: 0 us/frame. Host resource discipline preserved during multi-agent execution.
First 20 Minutes moment: Boot.
Route impact: Runtime shader warmup code is compiler-clean after local BufferID and explicit padding changes.
Proof required: Unity refresh/import remains pending for the new editor/test files.
Parked work rejected: Claiming Unity editor test execution without Unity asset import.

## Decision 015 - No-Throw Warmup Cancellation

Problem: The 1336 shader warmup loop still used `ct.ThrowIfCancellationRequested()` and `NextFrameAsync(cancellationToken: ct)`, which made cancellation exception-driven inside the audited warmup route.
Solution: Replaced warmup-loop cancellation with explicit `ct.IsCancellationRequested` fail-closed returns and used `AwaitableDebtMonitor.NextFrameAsync()` without a cancellation token inside the shader warmup method. The method-scope scanner now reports 0 `ThrowIfCancellationRequested` and 0 tokenized awaits.
Rejected Alternatives: Keeping exception-driven cancellation because the route is cold boot; broad-editing unrelated bootstrap methods outside 1336 ownership.
Scalability potential: Low/Middle/High/Ultra keep the same continuous batch cadence and the same cancellation semantics. No tier gains a separate route.
Hardware Impact: 0 us/frame. Removes avoidable cancellation exception path during warmup on i3/MX350.
First 20 Minutes moment: Boot -> world load.
Route impact: If boot is canceled during shader warmup, the method returns false without throwing through the warmup loop.
Proof required: Method-scope static scan and scoped compile.
Parked work rejected: Reporting broad file hits as 1336 warmup violations.

## Decision 016 - Warmup API Failure Code

Problem: The previous warmup route had `catch (Exception)` plus `Debug.LogException`, which weakened the no-throw/fail-closed proof even though it was not a simulation Tick.
Solution: Removed the broad catch from `WarmConfiguredShaderVariantCollectionsAsync`. Known Unity warmup API failures now fall through typed catches and record numeric `WarmupApiFailure = 5` through the existing unmanaged telemetry ring before returning false.
Rejected Alternatives: Swallowing all exceptions; logging exception objects; allowing a bad SVC/shader pair to continue into simulation.
Scalability potential: Same failure code and black-box route across Low/Middle/High/Ultra.
Hardware Impact: Failure path only. No steady gameplay frame cost.
First 20 Minutes moment: Boot.
Route impact: Bad authored shader manifests fail closed before `_preWarmAssetsReady`.
Proof required: `Hecton8.Core` compile passed after patch, 0 warnings, 0 errors.
Parked work rejected: `catch (Exception)` in the 1336 warmup method.

## Decision 017 - Narrow Try Scope

Problem: Even after removing `catch (Exception)`, the warmup method still had typed catches around the whole method body, so bounds checks, cursor logic, telemetry calls, and awaits were inside a catch region.
Solution: Removed the outer try from `WarmConfiguredShaderVariantCollectionsAsync`. Added `TryWarmShaderVariant` so typed catches only wrap `ShaderWarmup.WarmupShaderFromCollection(collection, shader, _shaderWarmupSetup)`. The rest of the warmup route now relies on explicit bounds/cancellation checks and fail-closed returns.
Rejected Alternatives: Keeping typed catches around the full method; broad-editing unrelated bootstrap methods; letting Unity API failures escape into the bootstrap state machine.
Scalability potential: Low/Middle/High/Ultra keep the same manifest and cadence. No tier-specific exception route.
Hardware Impact: Failure path only. Removes catch coverage from non-API warmup logic on low-end i3/MX350 class hardware.
First 20 Minutes moment: Boot -> shader veil.
Route impact: Authored bad shader pairs fail closed with `WarmupApiFailure = 5`; indexing/cursor logic remains proactively bounded instead of catch-handled.
Proof required: Method-scope token scan, Roslyn native alias audit, and `Hecton8.Core` compile.
Parked work rejected: Method-level try/catch in the 1336 shader warmup block.

## Decision 018 - Scanner Window Isolation

Problem: `TryWarmShaderVariant` was placed immediately after `WarmConfiguredShaderVariantCollectionsAsync`, so the simple scanner window from warmup start to telemetry setup included the helper's typed catches and made the method proof ambiguous.
Solution: Moved `TryWarmShaderVariant` below `ResolveShaderWarmupBatchSize`, outside the audited warmup-method substring. The scanner now proves the warmup method itself has 0 try/catch, 0 broad catch, 0 throw-cancel, 0 tokenized await, 0 LINQ/string formatting, and 0 managed allocation tokens.
Rejected Alternatives: Explaining away the scanner ambiguity; leaving helper catches adjacent to the method; deleting typed Unity API failure handling entirely.
Scalability potential: No route change. Low/Middle/High/Ultra keep continuous cadence; proof boundary is now unambiguous for all tiers.
Hardware Impact: 0 us/frame. This is proof hygiene and does not add runtime work.
First 20 Minutes moment: Boot.
Route impact: Warmup method is now directly auditable without including helper implementation details.
Proof required: Method substring scan, native alias audit, scoped compile.
Parked work rejected: Ambiguous scanner ranges.

## Decision 019 - Failure Dump Snapshot Lock

Problem: The failure dump route copied the shader warmup telemetry ring through `TryResolveHandle`, which produced a raw native view without the same compaction-aware writer fence used by normal telemetry writes.
Solution: Snapshot the ring in `QueueBootstrapShaderWarmupTelemetryDump` through `TryAcquireWriteLock(in _shaderWarmupTelemetryHandle, SystemID.Bootstrap, out NativeArray<BootstrapTelemetryEntry> ring)`, copy into the preallocated fatal scratch buffer, and release in `finally` before queuing the worker write.
Rejected Alternatives: Caching a persistent `NativeArray<BootstrapTelemetryEntry>` field; holding the lock while the worker writes to disk; copying from a resolved view during active Vault compaction.
Scalability potential: Low/Middle/High/Ultra all keep the same 19.2 KB dump snapshot. Quality changes warmup cadence only, not memory authority or dump size.
Hardware Impact: Failure path only. On i3/MX350 the lock window is one bounded 19.2 KB memory copy; gameplay frames pay 0 us.
First 20 Minutes moment: Boot -> failed shader veil.
Route impact: A fatal shader warmup failure now produces a black-box snapshot without blocking future Vault compaction.
Proof required: method line audit, Roslyn native alias audit, scoped compile.
Parked work rejected: Async pinned view across frames.

## Decision 020 - SlowTick Dev String Purge

Problem: `GameBootstrapper.SlowTick` still had a guarded `Debug.LogError` that concatenated service name and tick count. It was pre-existing and outside shader warmup, but it is in a hot method in a file owned by this task.
Solution: Replace the dynamic log payload with a static interned message and keep the numeric `GlobalTelemetryBus.PublishPerformanceWarning` plus `CrashTelemetryBuffer.ReportRuntimeWatchdogStall` path as the diagnostic carrier.
Rejected Alternatives: Leaving the dev-build allocation because it was behind `UNITY_EDITOR || DEVELOPMENT_BUILD`; widening the edit into unrelated heartbeat routing; adding a string builder or format helper.
Scalability potential: Low/Middle/High/Ultra keep identical watchdog behavior. Diagnostics stay numeric and telemetry-owned.
Hardware Impact: Removes managed string allocation potential from the heartbeat SlowTick path. Estimated steady gameplay saving is event-dependent; audited forbidden-token count is now 0.
First 20 Minutes moment: Boot -> first controllable frame heartbeat validation.
Route impact: Watchdog still fires, but the hot method no longer constructs diagnostic strings.
Proof required: SlowTick token scan and scoped compile.
Parked work rejected: Debug string concatenation inside hot method.

## Decision 021 - Final APEX Compile Gate

Problem: The dump-lock and SlowTick string patches changed production source after the previous compile proof.
Solution: Re-ran CPU gate (`CPU 3.7%`, no `dotnet`/`csc`) and then a single scoped compile: `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal /m:1`. Result: 0 warnings, 0 errors, 70.74s.
Rejected Alternatives: Full solution build spam; reporting old 96.26s compile after source mutation; launching build while sibling agents were active.
Scalability potential: No runtime behavior change. Validation remains limited to the modified runtime assembly.
Hardware Impact: 0 us/frame. Host resource discipline preserved.
First 20 Minutes moment: Boot.
Route impact: Runtime bootstrap code compiles after all APEX patches.
Proof required: Unity asset import, editor tests, Play Mode profiler, and platform shader-cache traversal remain pending.
Parked work rejected: Fabricated Unity profiler numbers.

## Decision 022 - Bootstrap Scene YAML Alignment Check

Problem: The task wires a serialized shader manifest into `00_BOOTSTRAP.unity`, and raw YAML edits can corrupt Unity scene structure.
Solution: Re-ran the mandated scene validation command for `m_RootGameObject` and a focused line audit around the modified `shaderVariantCollections`/`shaderWarmupShaders` block. This scene file does not contain `m_RootGameObject` text in its current baseline YAML, but the modified manifest block is aligned under the intended `MonoBehaviour` and preserves fileID/GUID/type structure.
Rejected Alternatives: Blind find/replace; broad Unity scene rewrite; claiming Unity import without launching the Editor.
Scalability potential: No runtime cost. Low/Middle/High/Ultra all receive the same boot manifest route.
Hardware Impact: 0 us/frame. Prevents scene serialization drift from invalidating the boot route.
First 20 Minutes moment: Boot.
Route impact: `BootstrapController` can forward the explicit shader manifest to `GameBootstrapper` at boot.
Proof required: Unity import/scene open remains pending.
Parked work rejected: YAML mutation without line-level property alignment check.

## Decision 023 - Filtered Hot-Path Roslyn Proof

Problem: A whole-file scanner over `GameBootstrapper.cs`, `BootstrapController.cs`, the editor compiler, and edit tests reports cold/editor allocations and string concatenations that do not execute inside `Tick`, `SlowTick`, `LateFrameTick`, Burst jobs, or the audited warmup loop.
Solution: Ran the existing Roslyn hot-path scanner over all four C# files and preserved the raw report as `Docs/Reports/BOOTSTRAP_HOTPATH_ROSLYN_AUDIT_1336_RAW.json`. Then produced `Docs/Reports/BOOTSTRAP_HOTPATH_ROSLYN_AUDIT_1336.json` filtered to the hot owners `SlowTick` and `WarmConfiguredShaderVariantCollectionsAsync`: 0 findings, 0 job structs.
Rejected Alternatives: Deleting cold/editor allocations unrelated to the prompt; claiming whole-file 0 when the raw scanner proves that is false; treating cold editor tooling as simulation-frame code.
Scalability potential: Low/Middle/High/Ultra all keep the same zero-GC hot owners. Variant compiler remains editor-only.
Hardware Impact: 0 us/frame in audited hot owners. Whole-file cold/editor findings have no gameplay-frame cost.
First 20 Minutes moment: Boot -> first controllable frame.
Route impact: Shader warmup and heartbeat validation do not allocate managed memory on the audited frame routes.
Proof required: Raw and filtered Roslyn JSON reports plus scoped compile already passed after final source mutation.
Parked work rejected: Sanitized prose without raw scanner artifact.

## Decision 024 - Unity 6 PSO Trace Gap

Problem: Official Unity 6 documentation says DirectX 12, Metal, and Vulkan need GraphicsStateCollection traces for accurate PSO warmup. SVC-only warmup can still leave driver work when vertex layout or render target setup differs.
Solution: Added an optional PSO warmup stage before SVC manifest traversal. It uses `GraphicsStateCollection.WarmUpProgressively` and waits through the existing Awaitable frame-yield pattern.
Rejected Alternatives: Claiming SVC plus `ShaderWarmupSetup.vdecl` is full modern API proof; adding a hidden camera-render scene; forcing `Shader.WarmupAllShaders`.
Scalability potential: Low devices can ship no PSO files and use the bounded SVC route; middle/high/ultra builds can add API-specific `.graphicsstate` traces without changing simulation authority.
Hardware Impact: PSO work moves into boot only when trace files are configured. On i3/MX350 this avoids first-cave/first-flashlight driver stalls; exact microseconds require Unity profiler.
First 20 Minutes moment: Boot -> first cave/flashlight/VFX appearance.
Route impact: Modern graphics API warmup now has a real file-backed route, not a comment.
Proof required: Generate one `.graphicsstate` per target graphics API from a development build and configure `shaderGraphicsStateCollectionPaths`.
Parked work rejected: Fake PSO completion without trace assets.

## Decision 025 - File-Backed GraphicsStateCollection Contract

Problem: A serialized `GraphicsStateCollection[]` field is not a reliable Unity scene contract. The Unity 6 API exposes constructors and `LoadFromFile` for `.graphicsstate` files, while the project has no such assets.
Solution: Replaced `GraphicsStateCollection[] shaderGraphicsStateCollections` with `string[] shaderGraphicsStateCollectionPaths` in `GameBootstrapper` and `BootstrapController`. Runtime accepts absolute paths, `Assets/...` paths in Editor, or StreamingAssets-relative paths, then loads `new GraphicsStateCollection(resolvedPath)`.
Rejected Alternatives: Keeping non-portable object references; adding editor-only `DefaultAsset` fields to runtime code; silently ignoring bad PSO paths.
Scalability potential: Low/Middle/High/Ultra can ship different trace sets by platform/API with the same runtime loop. The path list is optional and does not change gameplay truth.
Hardware Impact: Cold boot file load only. No gameplay frame tax. Missing/bad files fail closed before simulation.
First 20 Minutes moment: Boot.
Route impact: The bootstrapper can now consume real trace artifacts when produced.
Proof required: Current C# compile after CPU gate, Unity import, and trace-file profiler validation.
Parked work rejected: Decorative PSO fields.

## Decision 026 - Fail-Closed Empty Shader Collections

Problem: A missing `ShaderVariantCollection[]` could previously be treated as a successful warmup. That launches the game with all variant compile debt still deferred to gameplay.
Solution: `collectionCount <= 0` now records `MissingShaderCollections`, queues the black-box dump, shows the BIOS shader failure overlay, raises bootstrap failure, and returns false.
Rejected Alternatives: Best-effort boot with expected stutter; replacing the missing list with `Shader.WarmupAllShaders`; delaying detection to first gameplay VFX.
Scalability potential: All tiers share the same authority rule: simulation starts only after configured shader debt is paid.
Hardware Impact: Prevents first-use compile spikes on weak hardware. Exact savings require profiler markers `Shader.CreateGPUProgram` and `CreateGraphicsGraphicsPipelineImpl`.
First 20 Minutes moment: Boot -> player control.
Route impact: Misconfigured boot scenes no longer pass silently.
Proof required: Unity import/editor test execution remains pending.
Parked work rejected: Soft warning only.

## Decision 027 - Honest Build Gate

Problem: The latest PSO path patch changed runtime source after the last successful scoped compile.
Solution: Rechecked host state before compiling. CPU stayed 74.8%, 99.8%, and 99.8%; no `dotnet` or `csc` processes were active, but the project rule forbids launching dotnet build while CPU is above 50%.
Rejected Alternatives: Running build anyway to manufacture a green report; reporting the pre-patch compile as current; killing unrelated Python/Code processes.
Scalability potential: No runtime effect. Preserves multi-agent host stability.
Hardware Impact: 0 us/frame. This is validation discipline.
First 20 Minutes moment: None; build proof pending.
Route impact: Static source proof is current, compile proof must be rerun when CPU gate opens.
Proof required: `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal /m:1` once CPU <= 50% and no dotnet/csc process is active.
Parked work rejected: False `VERIFIED_GREEN` status.

## Decision 028 - GraphicsSettings Preload Authority Purge

Problem: `ProjectSettings/GraphicsSettings.asset` preloaded `SuitVisor_HUDPhosphor` and `Hecton_FoundationPylon` through Unity's global Preloaded Shaders list. Unity uses the legacy SVC warmup route there, outside `GameBootstrapper` telemetry, timeout, fail-closed handling, and continuous GlobalQualityWeight cadence.
Solution: Cleared `m_PreloadedShaders` and moved custom SVC ownership into `00_BOOTSTRAP.unity`, where `BootstrapController` forwards the manifest to `GameBootstrapper`.
Rejected Alternatives: Leaving global preload as a backup; adding duplicate custom SVCs to both GraphicsSettings and bootstrap; using `Shader.WarmupAllShaders`.
Scalability potential: Low/Middle/High/Ultra now share one shader-warmup authority route. Device quality changes cadence and optional trace assets only, not ownership.
Hardware Impact: Removes an uncontrolled built-player preload path. On i3/MX350 class hardware this prevents an opaque Unity warmup wall outside telemetry; exact microseconds require profiler markers.
First 20 Minutes moment: Boot -> visor/HUD activation.
Route impact: The loading veil stays up until the owned bootstrap route succeeds or fails closed.
Proof required: Unity import, edit tests, and profiler route remain pending.
Parked work rejected: Treating GraphicsSettings auto-preload as harmless.

## Decision 029 - SuitVisor HUD Manifest Coverage

Problem: `SuitVisor_HUDPhosphor.shadervariants` was globally preloaded but absent from `00_BOOTSTRAP.unity`. The SVC references SuitVisor, Hecton_IGNDitheredBackground, and Hecton_ScannerMarkerInstanced shader GUIDs used by HUD canvas/scanner routes.
Solution: Added the SVC GUID `d14e68d77ad84fbab8fd71ee8cbe5a21` and shader GUIDs `559786835a571e0428aa349084ff940b`, `021ae9f459be4094b8800c25a19d5d9e`, and `93d4136c67d64c5bb944522bd67021a1` to the bootstrap scene manifest.
Rejected Alternatives: Trusting `m_PreloadedShaders`; warming the whole UI shader folder; adding a render-camera warmup scene without PSO trace proof.
Scalability potential: Low devices get the minimal HUD/visor variants in the same batch cadence; High/Ultra can add `.graphicsstate` traces later for exact PSO states.
Hardware Impact: Boot-only extra manifest entries. Runtime gameplay cost is 0 us/frame; expected benefit is avoiding first visor/scanner shader compilation stalls.
First 20 Minutes moment: First visor HUD and scanner marker use.
Route impact: HUD/visor visual readiness is controlled by `GameBootstrapper` instead of hidden Unity preload.
Proof required: Unity import and profiler validation.
Parked work rejected: Broad folder warmup.

## Decision 030 - Current Non-Green Validation Boundary

Problem: The latest YAML/ProjectSettings/test changes altered source artifacts after the last successful scoped build.
Solution: Re-ran static proof and CPU gate. `git diff --check` passed except Git LF/CRLF warnings; warmup/wait methods scan 0 forbidden hot-path tokens; native field-like declarations remain 0. CPU stayed 59.6%, 62.7%, and 99.0% with no dotnet/csc process active, so build remains blocked by the project CPU rule.
Rejected Alternatives: Running dotnet above 50% CPU; reporting the old build as current; hiding the failed green status in prose.
Scalability potential: No runtime change. Preserves host fairness in a 20+ agent workspace.
Hardware Impact: 0 us/frame. Validation discipline only.
First 20 Minutes moment: None; proof artifact maintenance.
Route impact: Static proof is current, compile proof is pending.
Proof required: `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal /m:1` once CPU <= 50%, then Unity import/edit tests/profiler.
Parked work rejected: False `VERIFIED_GREEN`.

## Decision 031 - Scanner Pulse Direct Shader Coverage

Problem: `Tool_Scanner_Held.prefab` has a direct serialized shader reference to `Hecton_ScannerPulseInstanced.shader` (`83fe0f4ef4dc4260ae2b77e1e1e218b2`), but no active bootstrap `ShaderVariantCollection` contained that shader and `00_BOOTSTRAP.unity` did not pass it to `GameBootstrapper`.
Solution: Added the scanner pulse shader to `SuitVisor_HUDPhosphor.shadervariants` with `INSTANCING_ON` and added the same shader GUID to the bootstrap `shaderWarmupShaders` manifest.
Rejected Alternatives: Relying on runtime first-use compilation; adding a broad scanner shader folder to the boot manifest; moving scanner ownership into a new global renderer.
Scalability potential: Low/Middle/High/Ultra all share the same scanner pulse route. Quality changes warmup cadence only. High/Ultra can add per-API `.graphicsstate` traces later without changing the manifest authority.
Hardware Impact: 0 us/frame steady cost. On i3/MX350 this removes one likely first-scanner-pulse shader compile debt; exact microseconds require Unity profiler marker capture.
First 20 Minutes moment: First scanner activation.
Route impact: Scanner marker and pulse now share owned bootstrap warmup coverage.
Proof required: Unity import and profiler capture remain pending.
Parked work rejected: Fake completion without SVC and scene-manifest linkage.

## Decision 032 - Direct Shader Dependency Compiler Coverage

Problem: The editor compiler only collected material dependencies. Prefabs in this route also serialize direct `Shader` fields (`Suit_HUD_Canvas`, scanner tool), so the generated collection could omit critical HUD/scanner shaders.
Solution: Added `Suit_HUD_Canvas.prefab`, shader dependency collection, explicit direct shader roots, and `DirectShaderKeywordManifest` for `_HUD_PHOSPHOR_MODE` and scanner `INSTANCING_ON`.
Rejected Alternatives: Whole-project shader scan; material-only traversal; creating a dummy render scene to discover direct shader fields.
Scalability potential: Low keeps a strict direct route list; Middle/High/Ultra can extend explicit roots with authored first-20-minutes shader references without turning the compiler into full-project warmup.
Hardware Impact: Editor/build-time only. Runtime benefit is fewer missing first-use variants on low-end hardware; exact driver savings require profiler.
First 20 Minutes moment: Boot -> visor HUD -> scanner activation.
Route impact: The compiler now mirrors actual first-20-minutes direct shader ownership instead of guessing via materials.
Proof required: Run the Unity editor menu `Hecton/Validation/Rendering/Compile Bootstrap Shader Variant Collection 1336` after import.
Parked work rejected: Broad shader folder warmup.

## Decision 033 - Contextual Instancing Retention

Problem: The compiler blanket-stripped `INSTANCING_ON`, `DOTS_INSTANCING_ON`, and `PROCEDURAL_INSTANCING_ON`. Existing SVC data and scanner shaders prove that some first-route shaders require instancing variants.
Solution: `ShouldKeepInstancingKeyword` now keeps `INSTANCING_ON` only for authored instanced/indirect/GPUI shaders and keeps DOTS/procedural keywords only for indirect/procedural shader names or paths.
Rejected Alternatives: Keeping all instancing keywords; stripping all instancing keywords; hand-adding only the current scanner asset while leaving the compiler wrong for the next direct instanced shader.
Scalability potential: Low avoids keyword spam; Middle/High/Ultra can use richer instanced visuals without making every material carry instancing permutations.
Hardware Impact: Prevents scanner instanced-route runtime compilation without exploding the SVC beyond the 512 variant budget. Exact savings require profiler.
First 20 Minutes moment: Scanner overlay/pulse during early resource discovery.
Route impact: Instanced scanner presentation is represented in boot warmup while broad instancing spam remains rejected.
Proof required: Unity editor compiler run and SVC inspection.
Parked work rejected: Binary low/high instancing switch.

## Decision 034 - Current Build Gate After Direct Coverage Patch

Problem: Source artifacts changed after the prior static report, so compile proof must be refreshed, but host CPU is saturated.
Solution: Re-ran static gates and sampled CPU. Exact warmup/wait method brace scan returned 0 forbidden hot-path tokens; native field scan returned 0 native collection field declarations; `git diff --check` returned no whitespace errors, only Git LF/CRLF warnings. CPU reached 100.0% and no `dotnet`/`csc` process was active, so dotnet build remains blocked by AGENTS.md.
Rejected Alternatives: Running dotnet above 50% CPU; reporting old compile proof as current; killing unrelated processes owned by other agents.
Scalability potential: No runtime change. Preserves multi-agent host fairness.
Hardware Impact: 0 us/frame. Validation discipline only.
First 20 Minutes moment: None.
Route impact: Current patch is static-green only; runtime/import/profiler proof remains pending.
Proof required: `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal /m:1` once CPU <= 50%, Unity import, editor tests, and profiler.
Parked work rejected: False `VERIFIED_GREEN`.

## Decision 035 - Final Static Snapshot Boundary

Problem: The final reports had to be machine-readable and current after the direct shader/instancing patch, but the host remained saturated.
Solution: Parsed both JSON reports with `ConvertFrom-Json`, reran exact hot-path scans, reran native field-declaration scan, reran `git diff --check`, and sampled the build gate. Latest gate: CPU 100.0%, `dotnet` count 0, `csc` count 0, so no new scoped build was launched.
Rejected Alternatives: Launching `dotnet build` above the 50% CPU gate; reporting a stale compile as current; hiding the non-green validation boundary.
Scalability potential: No runtime behavior change. Low/Middle/High/Ultra still use one owned manifest route, optional `.graphicsstate` traces, and continuous `GlobalQualityWeight` cadence.
Hardware Impact: 0 us/frame. Host fairness preserved during multi-agent execution; runtime shader warmup remains static-clean but Unity import/profiler proof is still required.
First 20 Minutes moment: Boot -> first scanner/HUD activation.
Route impact: Static coverage for scanner pulse and direct shader references is current; compiler/runtime proof is pending until the active build load clears.
Proof required: One scoped `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal /m:1` after CPU <= 50% and no `dotnet`/`csc`, then Unity import/editor tests/profiler and platform `.graphicsstate` traces.
Parked work rejected: False `VERIFIED_GREEN`.

## Decision 036 - Scoped Build After CPU Gate Opened

Problem: The final source patch still required compile proof after CPU saturation cleared.
Solution: Sampled CPU at 22.9% with 0 `dotnet` and 0 `csc`, then ran one scoped command: `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal /m:1`. Result: exit 0, 0 errors, 15 CS0649 warnings from `Assets/_Project/Scripts/World` and `Assets/_Project/Scripts/Gameplay`, not from the 1336 bootstrap/shader-warmup files.
Rejected Alternatives: Full solution build; editing unrelated World/Gameplay warnings; pretending warnings are 1336 defects; running repeated builds after success.
Scalability potential: No runtime route change. The shader warmup authority remains one bootstrap route with optional `.graphicsstate` traces and continuous `GlobalQualityWeight` cadence.
Hardware Impact: 0 us/frame. Compile proof only; runtime first-use shader driver savings still require Unity profiler.
First 20 Minutes moment: Boot -> first HUD/scanner activation.
Route impact: Runtime C# source compiles after direct shader, scene manifest, and compiler instancing changes. Unity import/editor tests/profiler remain the real unresolved validation boundary.
Proof required: Unity asset import, `BootstrapShaderWarmupEditTests`, real boot profiler capture, and target API `.graphicsstate` traces.
Parked work rejected: Cross-domain warning cleanup by agent 1336.

## Decision 037 - GraphicsState Object Lifetime

Problem: `TryLoadGraphicsStateCollection` created a `GraphicsStateCollection` and returned `false` for an empty trace without destroying the UnityEngine.Object.
Solution: Destroy and null the collection when `totalGraphicsStateCount <= 0`; also destroy any non-null collection in the typed API failure catches before returning fail-closed.
Rejected Alternatives: Letting empty trace assets leak native Unity objects; ignoring empty files because `.graphicsstate` traces are optional today.
Scalability potential: Low/Middle/High/Ultra all keep the same PSO trace contract. Optional trace files can scale by platform/API without leaking on bad authoring data.
Hardware Impact: Cold boot failure path only. Prevents accumulated native object debt on weak devices during repeated failed boot attempts.
First 20 Minutes moment: Boot shader veil.
Route impact: Bad `.graphicsstate` files now fail closed and release the transient Unity object before the BIOS failure overlay.
Proof required: Unity import/runtime validation with an empty trace file remains pending.
Parked work rejected: Hiding lifetime debt behind "trace assets are missing".

## Decision 038 - Black-Box Dump Without Vault Availability

Problem: `QueueBootstrapShaderWarmupTelemetryDump` set `_shaderWarmupDumpQueued` before acquiring the DataVault lock. If the Vault was unavailable or the ring view was invalid, the method returned without writing `Dump_1336_Bootstrapper.bin`.
Solution: Add `WriteBootstrapShaderWarmupFallbackDumpHeader`, a one-entry 64B fallback `BootstrapTelemetryEntry` with `MissingTelemetryRing`, then queue the same worker writer even when Vault snapshotting fails.
Rejected Alternatives: Debug.Log-only failure; retrying across frames while the boot failure path is already collapsing; holding a lock across worker I/O.
Scalability potential: Same constant-size 19.2 KB scratch buffer on every tier; fallback writes only 64 bytes if the ring is unavailable.
Hardware Impact: Fatal path only. Runtime steady-state remains 0 us/frame; low-end hardware still gets a deterministic postmortem artifact.
First 20 Minutes moment: Boot failure before first simulation frame.
Route impact: Fatal shader warmup failures now have a black-box artifact even when the telemetry ring cannot be locked.
Proof required: Unity/player failure drill remains pending.
Parked work rejected: Marking dump queued without a file write.

## Decision 039 - Current Build Gate After Loop 11

Problem: Runtime source changed after the last scoped build, so compile proof is stale until rerun.
Solution: Re-ran static gates, then sampled host state. CPU stayed 100%, with 1 `dotnet` and 1 `csc` process active. The project rule forbids launching another build in this state.
Rejected Alternatives: Running a second dotnet build alongside an active compiler; claiming the previous build covers the new source; killing unrelated processes.
Scalability potential: No runtime behavior change. Host fairness preserved for the 20+ agent workspace.
Hardware Impact: 0 us/frame. Validation blocker only.
First 20 Minutes moment: None.
Route impact: Static proof is current; scoped compile must be refreshed when CPU <= 50% and no dotnet/csc process is active.
Proof required: `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal /m:1` after the gate opens.
Parked work rejected: False green report.

## Decision 040 - Direct Shader Priority In Compiler

Problem: `HectonShaderVariantCollectionCompiler1336` added direct HUD/scanner shader roots after material variants. The 512-variant cap could be consumed by broad material dependencies before the must-have direct routes were emitted.
Solution: Add `BuildSortedDirectShaderReferenceRoots` and `AddDirectShaderVariantsFirst`; direct shader roots are now sorted and emitted before material dependency variants.
Rejected Alternatives: Raising `MaxCompiledVariants`; trusting HashSet/material order; leaving scanner/HUD direct roots dependent on remaining budget.
Scalability potential: Low keeps a bounded collection; Middle/High/Ultra can extend explicit direct roots without turning the compiler into whole-project shader warmup.
Hardware Impact: Editor/build-time only. Prevents first-use scanner/HUD shader compile debt on weak hardware without increasing runtime simulation cost.
First 20 Minutes moment: First visor HUD and scanner activation.
Route impact: Critical visible routes now get deterministic compiler priority.
Proof required: Run Unity editor menu `Hecton/Validation/Rendering/Compile Bootstrap Shader Variant Collection 1336` after import.
Parked work rejected: Bigger variant budget as a substitute for priority.

## Decision 041 - Stable Master Variant Asset Route

Problem: The compiler output path `Assets/_Project/Art/Shaders/Variants/Hecton8MasterVariants.shadervariants` had no asset/meta and no scene reference, so a compiler run would create an unreferenced GUID unless someone manually rewired the bootstrap scene.
Solution: Create a stable empty SVC asset with GUID `b271bb14b4bd4c2b9e41e34d86af5336` and add it to `00_BOOTSTRAP.unity:shaderVariantCollections`. The compiler will clear/fill this same asset.
Rejected Alternatives: Leaving output asset creation to ad hoc editor state; hand-editing generated GUID later; adding the compiler output only to `GraphicsSettings.m_PreloadedShaders`.
Scalability potential: Same route on all tiers. Low can keep the generated collection minimal; Ultra can fill richer first-route variants while staying under explicit budget.
Hardware Impact: Empty asset adds no variant warmup work until the compiler fills it. Existing non-empty SVCs preserve current coverage.
First 20 Minutes moment: Boot shader veil.
Route impact: Generated master collection now has a stable bootstrap route.
Proof required: Unity import and compiler menu execution remain pending.
Parked work rejected: Compiler output with no owner route.

## Decision 042 - Current Build Gate After Loop 12

Problem: Editor compiler, test, scene, and asset files changed after the last scoped build.
Solution: Re-ran static gates and sampled host state. CPU stayed 100%, with 1 `dotnet` and 1 `csc` process active. No build launched under the project rule.
Rejected Alternatives: Parallel build during another active compiler; claiming Loop 10 build covers Loop 12 source; editing unrelated warnings.
Scalability potential: No runtime behavior change. Host fairness preserved.
Hardware Impact: 0 us/frame. Validation blocker only.
First 20 Minutes moment: None.
Route impact: Static proof is current; compile/import proof remains pending.
Proof required: scoped build after CPU <= 50% and no dotnet/csc process, then Unity import/editor tests/profiler.
Parked work rejected: False verified-green claim.

## Decision 043 - Scene Manifest Coverage Test

Problem: The edit tests only checked individual GUID strings. That did not prove every shader inside every configured SVC is represented in `shaderWarmupShaders`.
Solution: Add `BootstrapScene_ShaderManifestCoversEveryConfiguredCollection`, resolving scene SVC GUIDs through `AssetDatabase.GUIDToAssetPath`, extracting shader GUIDs from each SVC YAML, and requiring each one in the explicit warmup manifest.
Rejected Alternatives: Manual GUID spot checks; trusting the current scene list; runtime reflection into SVC internals.
Scalability potential: All tiers keep one manifest authority. Low/Middle/High/Ultra can add or remove SVCs only if the manifest remains complete.
Hardware Impact: Editor validation only, 0 us/frame.
First 20 Minutes moment: Boot -> first visible HUD/scanner/VFX route.
Route impact: Prevents adding an SVC whose shader is not handed to `ShaderWarmup.WarmupShaderFromCollection`.
Proof required: Unity edit-test execution remains pending; CLI mirror already found 0 missing shader GUIDs.
Parked work rejected: String spot-checks as full manifest coverage proof.

## Decision 044 - Current Build Gate After Loop 13

Problem: Test code changed after Loop 12, so compile proof remains stale.
Solution: Re-ran static gates and sampled host state. Latest CPU samples stayed 100%, 100%, 100% with no active dotnet/csc, so no build was launched.
Rejected Alternatives: Running dotnet at 92% CPU; claiming previous compile covers new tests.
Scalability potential: No runtime behavior change. Host fairness preserved.
Hardware Impact: 0 us/frame. Validation blocker only.
First 20 Minutes moment: None.
Route impact: Static proof is current; compile/import proof remains pending.
Proof required: scoped build after CPU <= 50% and no dotnet/csc process.
Parked work rejected: False verified-green claim.

## Decision 045 - Telemetry Ring Readiness Is Not BufferID

Problem: `EnsureBootstrapShaderWarmupTelemetryRing` could leave `_shaderWarmupTelemetryHandle.BufferID` nonzero after a failed validation lock, allowing a later call to return true without proving the ring is lockable and correctly sized.
Solution: Added `_shaderWarmupTelemetryReady` and changed validation to use `TryAcquireWriteLock(in _shaderWarmupTelemetryHandle, SystemID.Bootstrap, out NativeArray<BootstrapTelemetryEntry> ring)` with `ReleaseWriteLock` in `finally`. Success now requires a created ring with capacity at least 300.
Rejected Alternatives: Keeping `TryResolveHandle` as validation; treating nonzero `BufferID` as readiness; caching a persistent `NativeArray<BootstrapTelemetryEntry>` field.
Scalability potential: Same 19.2 KB ring on Low/Middle/High/Ultra. Quality only changes warmup cadence, not memory authority.
Hardware Impact: Boot-only lock window. No gameplay frame cost on i3/MX350.
First 20 Minutes moment: Boot shader veil before first simulation tick.
Route impact: The shader warmup gate cannot pass on a stale or unvalidated Vault handle.
Proof required: scoped build when CPU gate opens; Unity import/test remains pending.
Parked work rejected: False compaction-safety proof.

## Decision 046 - Multiple ShaderWarmup Vertex Declarations

Problem: A single mesh-style `ShaderWarmupSetup.vdecl` (Position/Normal/Tangent/UV0) is not enough proof for HUD/UI shader pipeline states. Unity's warmup API requires the rendering setup, including vertex data layout, to match the runtime draw path.
Solution: Added two prebuilt setup records: mesh P/N/T/UV0 and HUD/UI Position/Color/UV0. The warmup loop now warms every manifest shader against each setup, with no per-iteration allocations and no string inspection of `Shader.name`.
Rejected Alternatives: One mesh-only setup; runtime dummy render scene; per-shader string heuristics inside the warmup loop; waiting for missing `.graphicsstate` traces as the only mitigation.
Scalability potential: Low devices get deterministic minimal extra boot attempts; Middle/High/Ultra can still add exact `.graphicsstate` PSO traces later without changing ownership.
Hardware Impact: Extra boot work only. Runtime steady cost is 0 us/frame. Prevents first HUD/visor/scanner PSO miss on modern APIs when trace assets are absent.
First 20 Minutes moment: First visor HUD and scanner activation.
Route impact: SVC warmup is less likely to miss UI/HUD vertex-layout states before world entry.
Proof required: Unity profiler on DX12/Vulkan/Metal to confirm no `Shader.CreateGPUProgram` or PSO creation after veil lowers.
Parked work rejected: Claiming PSO completeness from SVC shader GUID membership alone.

## Decision 047 - Scalable Warmup Timeout

Problem: A fixed 5000 ms warmup timeout can false-fail on weak hardware when low-tier cadence yields one shader/setup attempt per frame, especially after adding UI vertex-layout setup and optional `.graphicsstate` traces.
Solution: Replaced the fixed timeout with `ResolveShaderWarmupTimeoutMilliseconds(collectionCount, shaderCount, graphicsStateCollectionCount, setupCount)`. It adds attempt budget, PSO trace budget, and continuous `GlobalQualityWeight` low-quality padding, then clamps to 5s..30s.
Rejected Alternatives: Infinite timeout; binary low/high timeout switch; starting simulation while warmup is incomplete; raising the timeout blindly for every device.
Scalability potential: Low receives more boot patience because cadence is slower; Ultra keeps a tighter boot window while still using the same authority route.
Hardware Impact: No gameplay frame cost. Reduces false fatal boot on i3/MX350 while preserving fail-closed behavior.
First 20 Minutes moment: Boot -> world load.
Route impact: The loading veil waits for real warmup without punishing low-tier cadence as a failure.
Proof required: device/player boot timing capture.
Parked work rejected: Fixed timeout as universal truth.

## Decision 048 - Remove Meta Pass From Runtime Variant Budget

Problem: The editor compiler included `PassType.Meta`, a lightmap/editor pass, inside the strict 512-variant runtime warmup budget.
Solution: Removed `PassType.Meta` from `WarmupPassTypes` and added `runtimePassBudgetOnly=true` to the compiler report contract.
Rejected Alternatives: Increasing `MaxCompiledVariants`; leaving non-runtime passes in the collection; using whole-project warmup to hide budget waste.
Scalability potential: Low keeps the smallest runtime payload; Middle/High/Ultra can spend saved variant budget on visible first-20-minutes routes rather than lightmap meta passes.
Hardware Impact: Editor/build-time budget recovery. Runtime steady cost is 0 us/frame; expected benefit is lower boot warmup work and fewer critical runtime variants starved by budget.
First 20 Minutes moment: Boot -> first HUD/scanner/VFX activation.
Route impact: The generated master SVC now prioritizes runtime-relevant passes.
Proof required: Unity editor compiler run after import.
Parked work rejected: Spending budget on non-runtime lightmap metadata.

## Decision 049 - Scoped Build Blocked By Foreign AupCell Contract

Problem: After Loop 14, the CPU gate opened and a scoped `Hecton8.Core` build was required. The build failed, but every compiler error was a cross-domain `PoolSlotData.AupCell` contract mismatch in Fauna/Gameplay files outside 1336 ownership.
Solution: Recorded the dependency failure and did not patch Fauna/Gameplay from the shader warmup agent. The 1336-owned files were not named in compiler errors.
Rejected Alternatives: Editing `PoolSlotData`, Fauna, or Gameplay contracts from the bootstrap shader domain; running repeated builds against the same foreign error; reporting green.
Scalability potential: No runtime route change. Prevents cross-domain churn while keeping the shader warmup work isolated.
Hardware Impact: 0 us/frame. Compile validation is blocked by unrelated ownership.
First 20 Minutes moment: None.
Route impact: 1336 source still needs a clean build after the Fauna/Gameplay contract is reconciled by its owner.
Proof required: rerun one scoped build after `PoolSlotData.AupCell` errors are fixed by the owning agent.
Parked work rejected: Cross-domain fix-forward.

## Decision 050 - Modern API Requires GraphicsState Trace

Problem: Unity's modern graphics API path is not fully proven by curated SVC warmup. Direct3D12, Vulkan, and Metal need `.graphicsstate` trace data for exact PSO warmup, but the project currently has zero trace assets.
Solution: Added `RequiresGraphicsStateCollectionsForCurrentApi()`. On Direct3D12, Vulkan, and Metal, `GameBootstrapper` now fails closed with `MissingGraphicsStateCollections` before SVC traversal if no trace paths are configured.
Rejected Alternatives: Claiming SVC-only as PSO proof on modern APIs; allowing hidden first-use PSO creation after the loading veil; forcing non-modern APIs to require missing trace assets.
Scalability potential: Low/Middle on Direct3D11 can still use bounded SVC warmup. Low/Middle/High/Ultra on Direct3D12/Vulkan/Metal must provide platform traces before simulation. On toaster hardware this prevents lying about smoothness; on $5000 hardware it enables exact trace-driven PSO overkill instead of surprise runtime compilation.
Hardware Impact: 0 us/frame. On i3/MX350-class hardware this trades a hard boot failure for avoiding uncontrolled first-use PSO stalls. Exact saved microseconds require player profiler trace.
First 20 Minutes moment: Boot -> first visor/HUD/scanner/VFX activation.
Route impact: Modern API builds cannot enter the world without trace-backed PSO proof.
Proof required: Generate target API `.graphicsstate` traces, configure `shaderGraphicsStateCollectionPaths`, then profile first-use traversal.
Parked work rejected: False `VERIFIED_GREEN` while trace assets are missing.

## Decision 051 - ShaderWarmupSetup Must Match Real Project Mesh Layouts

Problem: Two warmup declarations covered only P/N/T/UV0 mesh and UI ColorUNorm8. Project first-route rendering uses additional layouts: scanner and HUD quads P/UV0, damage/radar cubes P/N, scanner quads P/N/UV0, VFX trails with float Color, and voxel world meshes with ColorUNorm8 plus TexCoord1..4.
Solution: Expanded `_shaderWarmupSetups` to seven static prebuilt layouts. The warmup loop still allocates nothing per attempt and timeout budget scales from setup count.
Rejected Alternatives: Runtime dummy render scene; per-shader string heuristics; leaving real mesh layouts uncovered; adding binary low/high setup lists.
Scalability potential: Low pays more boot frames but no gameplay frame tax. Middle/High/Ultra spend boot veil time on richer exact state coverage. On toaster hardware the path is slow but deterministic; on $5000 hardware the same manifest can warm more render states quickly and leave visual overkill variants to the generated collection.
Hardware Impact: Runtime steady cost remains 0 us/frame. Low-tier boot attempts increase from 196 to 686 with current 7 SVCs / 14 shaders; timeout now covers that wall-clock budget.
First 20 Minutes moment: Scanner overlay, visor HUD, first VFX trail, world voxel visibility.
Route impact: SVC fallback now matches the real first-20-minute vertex declarations instead of a generic mesh lie.
Proof required: Unity profiler and Frame Debugger on target API; absence of Shader.CreateGPUProgram/PSO creation after bootstrap veil.
Parked work rejected: Treating shader GUID membership as complete render-state proof.

## Decision 052 - Current Build Gate After Loop 15

Problem: Runtime source and edit tests changed after Loop 14, so compile proof is stale until rerun.
Solution: Re-ran static gates and sampled the host. CPU averaged 100.0% with 0 `dotnet` and 0 `csc`; AGENTS.md forbids launching `dotnet build` above 50% CPU. The last attempted scoped build remains blocked by foreign `PoolSlotData.AupCell` errors.
Rejected Alternatives: Running dotnet during host saturation; editing Fauna/Gameplay ownership from shader warmup domain; reporting green.
Scalability potential: No runtime behavior change. Host fairness preserved in the 20+ agent workspace.
Hardware Impact: 0 us/frame. Validation blocker only.
First 20 Minutes moment: None.
Route impact: Static 1336 proof is current; compiler proof is pending.
Proof required: one scoped `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal /m:1` after CPU <= 50% and no dotnet/csc, then Unity import/edit tests/profiler.
Parked work rejected: False verified-green claim.

## Decision 053 - Shader Warmup Dump Must Be Temp-Promoted

Problem: `WriteBootstrapShaderWarmupDumpOnWorker` wrote directly to `Dump_1336_Bootstrapper.bin`. A process kill, I/O error, or power failure during the write could leave a partial final black-box artifact.
Solution: Add `_shaderWarmupDumpTempPath`, write through `AsyncWriteManager.WriteAll(tempPath, ...)`, and promote only after successful temp write using `File.Replace` when the final exists or `File.Move` when it does not.
Rejected Alternatives: Direct final overwrite; retrying on the main thread; adding a managed stream path in the warmup failure route.
Scalability potential: Same route on Low/Middle/High/Ultra. Dump size remains fixed at 19.2 KB or 64B fallback; only crash/fatal path pays the file promotion cost.
Hardware Impact: 0 us/frame. Fatal path only. Prevents corrupt postmortem artifact on weak storage.
First 20 Minutes moment: Bootstrap failure before the first simulation tick.
Route impact: Shader warmup black-box dump now follows the project temp-write/promote law instead of becoming a partial final file.
Proof required: Failure-drill in Unity/player remains pending.
Parked work rejected: Calling a direct final dump "good enough" because it is small.

## Decision 054 - Existing UberNoir Radiation SVC Was Unowned

Problem: `Hecton8_UberNoir_RadiationWarmup.shadervariants` existed on disk with shader GUID `fdac643fbeb24374ba9ea1e341842908`, but `00_BOOTSTRAP` did not reference the SVC and the shader was absent from `shaderWarmupShaders`.
Solution: Add SVC GUID `7b4dc2d560314d539f214c8355eb4274` to `shaderVariantCollections` and add the UberNoir shader GUID to the explicit manifest.
Rejected Alternatives: Assuming on-disk SVC files are automatically warmed; relying on GraphicsSettings preloaded shaders; leaving coverage to material dependency traversal.
Scalability potential: Low pays one more curated collection during boot; Middle/High/Ultra get the same owned route and can later add exact `.graphicsstate` traces for this shader.
Hardware Impact: Runtime steady cost 0 us/frame. Prevents first-use UberNoir/radiation variant debt; exact driver microseconds require profiler.
First 20 Minutes moment: Radiation/UberNoir rendering visibility during early world route.
Route impact: All `.shadervariants` under the bootstrap variants directory are now scene-owned and manifest-covered.
Proof required: Unity import/editor test and target player profiler.
Parked work rejected: Unreferenced warmup asset as proof.

## Decision 055 - Compiler Must Prioritize UberNoir Explicit Instancing

Problem: The existing UberNoir SVC carries `INSTANCING_ON` and `DOTS_INSTANCING_ON`, but the compiler did not treat `Hecton8_UberNoir.shader` as a direct root. Under a 512-variant cap, broad material traversal could starve the core shader.
Solution: Add UberNoir to `DirectShaderReferenceRoots` and add explicit `INSTANCING_ON` and `DOTS_INSTANCING_ON` entries to `DirectShaderKeywordManifest`.
Rejected Alternatives: Raising `MaxCompiledVariants`; relying on material order; blanket preserving every instancing keyword for every shader.
Scalability potential: Low keeps a bounded collection. High/Ultra can add more explicit roots without turning the compiler into whole-project warmup.
Hardware Impact: Editor/build-time only. Runtime cost remains 0 us/frame.
First 20 Minutes moment: First core UberNoir/radiation material draw.
Route impact: Core rendering shader variants are emitted before generic material dependencies consume budget.
Proof required: Unity editor compiler menu execution and report check remain pending.
Parked work rejected: Budget inflation instead of direct priority.

## Decision 056 - Timeout Needed Frame-Slice Wall-Clock Accounting

Problem: With 8 SVCs, 15 manifest shaders, and 7 layouts, low-tier warmup can schedule one attempt per frame. The old timeout budget counted attempts and quality padding but undercounted yielded frame wall-clock, risking false fail-closed on weak devices.
Solution: Add `shaderFrameSlices`, quality-scaled `ShaderWarmupLowQualityFrameCadenceMilliseconds` to `ShaderWarmupHighQualityFrameCadenceMilliseconds`, collection-yield frames, and raise the clamp to 60s.
Rejected Alternatives: Infinite timeout; fixed 30s cap after expanding coverage; binary low/high policy; starting simulation before warmup completes.
Scalability potential: Low gets more boot patience; Middle/High/Ultra remain faster through larger batch cadence and the same continuous `GlobalQualityWeight`.
Hardware Impact: Boot-only. No gameplay frame cost. Prevents valid low-tier boot from failing after the system intentionally yields between warmup attempts.
First 20 Minutes moment: Boot veil before world activation.
Route impact: Simulation remains blocked until warmup completes or a real failure occurs; low-tier no longer trips a timeout just because it obeys the frame budget.
Proof required: Device/player boot timing capture remains pending.
Parked work rejected: Attempt-count-only timeout as a truthful low-tier wall-clock model.

## Decision 057 - Current Build Gate After Loop 16

Problem: Runtime source, scene YAML, compiler, tests, and reports changed after Loop 15, so compile proof is stale.
Solution: Re-ran static gates and sampled host state. CPU sampled 98.5% with 0 `dotnet` and 0 `csc`; AGENTS.md forbids launching `dotnet build` above 50% CPU. Latest attempted scoped build still has the foreign `PoolSlotData.AupCell` blocker.
Rejected Alternatives: Running dotnet during host saturation; editing Fauna/Gameplay ownership from shader warmup domain; reporting green.
Scalability potential: No runtime behavior change. Host fairness preserved.
Hardware Impact: 0 us/frame. Validation blocker only.
First 20 Minutes moment: None.
Route impact: Static 1336 proof is current; build/import/profiler proof remains pending.
Proof required: one scoped `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal /m:1` after CPU <= 50% and no dotnet/csc, then Unity import/edit tests/profiler.
Parked work rejected: False verified-green claim.

## Decision 058 - GraphicsState Paths Must Be Player-Portable

Problem: The `.graphicsstate` loader accepted generic `Assets/...` and `ProjectSettings/...` paths as normal runtime configuration. That can pass in Editor but fail in a player build where project folders do not exist, producing a false PSO proof route.
Solution: Replaced raw path resolution with `TryResolveGraphicsStateCollectionPath`. It requires `.graphicsstate`, rejects parent traversal, maps `Assets/StreamingAssets/...` to `Application.streamingAssetsPath`, allows generic `Assets/...` and `ProjectSettings/...` only under `UNITY_EDITOR`, and constrains player absolute paths to StreamingAssets.
Rejected Alternatives: Accepting any project-relative path; silently falling back to StreamingAssets for `Assets/...`; allowing player absolute paths outside the build payload; reporting modern API proof while pathing can be Editor-only.
Scalability potential: Low/Middle/High/Ultra all use the same shipping trace route. Low devices avoid hidden first-use PSO stalls by failing closed if the trace file is not packaged; High/Ultra can ship larger trace sets through the same contract.
Hardware Impact: Runtime steady cost 0 us/frame. Cold path does a few path checks before trace load. Benefit is avoiding an uncontrolled player-only boot failure or hidden PSO miss.
First 20 Minutes moment: Boot -> first HUD/scanner/UberNoir/VFX draw on Direct3D12/Vulkan/Metal.
Route impact: Modern API PSO proof now depends on packaged StreamingAssets trace files, not Editor project folders.
Proof required: Add real target `.graphicsstate` traces under StreamingAssets, configure `shaderGraphicsStateCollectionPaths`, run Unity player boot and profiler.
Parked work rejected: Editor-only path acceptance as production proof.

## Decision 059 - PSO Timeout Telemetry Needs Collection Context

Problem: `WaitForShaderWarmupJobAsync` recorded timeout failures with collection and shader indexes set to `-1`, which hides which GraphicsState trace stalled.
Solution: Pass `collectionIndex`, `shaderIndex`, and `variantCount` into the wait method and record them on timeout. Current PSO calls pass the GraphicsState collection index and graphics state count.
Rejected Alternatives: Leaving timeout dumps anonymous; adding managed strings with path names to hot failure telemetry; creating a per-trace managed diagnostic object.
Scalability potential: Same fixed native telemetry DTO across all tiers. High/Ultra trace sets can grow without losing failure localization.
Hardware Impact: 0 us/frame. Three int arguments on a cold async wait path; no DTO layout change.
First 20 Minutes moment: Boot veil PSO warmup.
Route impact: Black-box dump can identify the stalled trace collection numerically.
Proof required: Failure-drill with an invalid/slow trace collection in Unity/player remains pending.
Parked work rejected: Text diagnostics in the hot route.

## Decision 060 - Current Build Gate After Loop 17

Problem: Runtime source, edit tests, and machine reports changed after Loop 16, so compile proof is stale again.
Solution: Re-ran static gates and sampled host state. CPU sampled 100.0% with 0 `dotnet` and 0 `csc`; AGENTS.md forbids launching `dotnet build` above 50% CPU. Latest attempted scoped build still has the foreign `PoolSlotData.AupCell` blocker.
Rejected Alternatives: Running dotnet during host saturation; editing Fauna/Gameplay ownership from shader warmup domain; reporting green.
Scalability potential: No runtime behavior change. Host fairness preserved.
Hardware Impact: 0 us/frame. Validation blocker only.
First 20 Minutes moment: None.
Route impact: Static 1336 proof is current; build/import/profiler proof remains pending.
Proof required: one scoped `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal /m:1` after CPU <= 50% and no dotnet/csc, then Unity import/edit tests/profiler.
Parked work rejected: False verified-green claim.

## Decision 061 - Non-Empty Shader Array Is Not A Valid Manifest

Problem: `WarmConfiguredShaderVariantCollectionsAsync` accepted any non-empty `shaderWarmupShaders` array. If Unity import/GUID damage turned every element into `null`, the loop recorded null-shader warnings but could still return success when at least one SVC had variants.
Solution: Added `CountValidShaderWarmupShaders` and fail closed with `MissingShaderManifest` when the array has no non-null `Shader` references. Timeout and start telemetry now use the valid shader count.
Rejected Alternatives: Trusting serialized array length; relying on warnings; allowing one valid SVC to mask zero usable shader warmup calls.
Scalability potential: Same behavior across Low/Middle/High/Ultra. Low-tier does not enter the world with a broken manifest; High/Ultra trace/warmup sets do not hide a broken import.
Hardware Impact: 0 us/frame. Boot-only O(n) scan over the shader manifest.
First 20 Minutes moment: Boot -> first HUD/scanner/UberNoir/VFX draw.
Route impact: A broken scene/import manifest now blocks simulation before shader warmup begins.
Proof required: Unity editor test and forced-null manifest playmode drill remain pending.
Parked work rejected: Null warnings as sufficient proof.

## Decision 062 - GUID Membership Is Not Asset Resolution

Problem: The edit test proved that SVC shader GUIDs were listed in `shaderWarmupShaders`, but did not prove those scene GUIDs resolve to actual `Shader` assets after import.
Solution: `BootstrapScene_ShaderManifestCoversEveryConfiguredCollection` now resolves every manifest shader GUID through `AssetDatabase.GUIDToAssetPath` and loads `UnityEngine.Shader`; it also loads every configured collection as `UnityEngine.ShaderVariantCollection`.
Rejected Alternatives: YAML-only GUID comparison; trusting Unity scene serialization; waiting for runtime null references to reveal import damage.
Scalability potential: All tiers benefit from identical import-proofed manifest authority.
Hardware Impact: Editor-only validation. Runtime steady cost 0 us/frame.
First 20 Minutes moment: Boot -> first route rendering warmup.
Route impact: Scene manifest coverage now catches unresolved shader/collection assets before player boot.
Proof required: Unity editor test execution remains pending.
Parked work rejected: GUID text as sufficient import proof.

## Decision 063 - Current Build Gate After Loop 18

Problem: Runtime source, edit tests, and machine reports changed after Loop 17, so compile proof is stale again.
Solution: Re-ran static gates and sampled host state. CPU sampled 100.0% with 0 `dotnet` and 0 `csc`; AGENTS.md forbids launching `dotnet build` above 50% CPU. Latest attempted scoped build still has the foreign `PoolSlotData.AupCell` blocker.
Rejected Alternatives: Running dotnet during host saturation; editing Fauna/Gameplay ownership from shader warmup domain; reporting green.
Scalability potential: No runtime behavior change. Host fairness preserved.
Hardware Impact: 0 us/frame. Validation blocker only.
First 20 Minutes moment: None.
Route impact: Static 1336 proof is current; build/import/profiler proof remains pending.
Proof required: one scoped `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal /m:1` after CPU <= 50% and no dotnet/csc, then Unity import/edit tests/profiler.
Parked work rejected: False verified-green claim.

## Decision 064 - Partial Null Manifest Must Fail Closed

Problem: Loop 18 rejected all-null shader manifests, but a partially null `shaderWarmupShaders` array or a null `shaderVariantCollections` slot could still warn and continue when another entry was valid. That can hide one broken shader/SVC route and leak first-use compilation into gameplay.
Solution: Added exact index scans. `FindInvalidShaderVariantCollectionIndex` fails with `MissingShaderCollections`; `validShaderCount != shaderCount` plus `FindInvalidShaderWarmupShaderIndex` fails with `MissingShaderManifest`. The warmup loop still has defensive null checks, but they now return failure instead of warning telemetry.
Rejected Alternatives: Keeping null slots as warning-only; relying on the editor test to catch every runtime/controller assignment; using managed text diagnostics for the broken slot.
Scalability potential: Low/Middle/High/Ultra all use the same manifest authority. Low devices avoid hidden first-use stalls; high-end devices do not mask broken overkill variants behind surviving base shaders.
Hardware Impact: 0 us/frame. Cold boot O(n) validation over configured arrays.
First 20 Minutes moment: Boot -> first HUD/scanner/UberNoir/VFX draw.
Route impact: A single null configured collection or shader slot blocks simulation before PSO/SVC warmup.
Proof required: Unity editor test execution and forced-null playmode drill remain pending.
Parked work rejected: Partial manifest success.

## Decision 065 - Current Build Gate After Loop 19

Problem: Runtime source, controller tooltip, edit tests, and reports changed after Loop 18, so compile proof is stale.
Solution: Re-ran static gates and sampled host state. CPU sampled 100.0% with 0 `dotnet` and 0 `csc`; AGENTS.md forbids launching `dotnet build` above 50% CPU. The latest attempted scoped build still has the foreign `PoolSlotData.AupCell` blocker.
Rejected Alternatives: Running dotnet during host saturation; editing Fauna/Gameplay ownership from shader warmup domain; reporting green.
Scalability potential: No runtime behavior change. Host fairness preserved in the 20+ agent workspace.
Hardware Impact: 0 us/frame. Validation blocker only.
First 20 Minutes moment: None.
Route impact: Static 1336 proof is current; build/import/profiler proof remains pending.
Proof required: one scoped `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal /m:1` after CPU <= 50% and no dotnet/csc, then Unity import/edit tests/profiler.
Parked work rejected: False verified-green claim.

## Decision 066 - StreamingAssets Resolver Must Defend Itself

Problem: `TryResolveStreamingAssetsPath` assumed its caller already normalized separators. If the helper were reused directly with a Windows backslash path, `HasParentPathSegment` would not catch `..\` traversal before `Path.GetFullPath`.
Solution: Normalize backslashes inside the helper, reject parent segments on the normalized path, resolve under `Application.streamingAssetsPath`, and verify the final absolute path starts with the StreamingAssets root.
Rejected Alternatives: Trusting all future call sites; relying only on `.graphicsstate` extension checks; allowing absolute player paths outside StreamingAssets.
Scalability potential: Same contract for Low/Middle/High/Ultra. Trace payload size can scale by tier, but path authority stays one route.
Hardware Impact: 0 us/frame. Cold PSO path validation only.
First 20 Minutes moment: Boot -> first modern-API PSO warmup trace load.
Route impact: Player PSO trace loading cannot escape the packaged StreamingAssets route.
Proof required: Unity/player path-drill remains pending.
Parked work rejected: Call-site-only path safety.

## Decision 067 - Current Build Gate After Loop 20

Problem: Runtime source, edit tests, reports, and logs changed after Loop 19, so compile proof is stale.
Solution: Re-ran static gates and sampled host state. CPU sampled 100.0% with 0 `dotnet` and 0 `csc`; AGENTS.md forbids launching `dotnet build` above 50% CPU. The latest attempted scoped build still has the foreign `PoolSlotData.AupCell` blocker.
Rejected Alternatives: Running dotnet during host saturation; editing Fauna/Gameplay ownership from shader warmup domain; reporting green.
Scalability potential: No runtime behavior change. Host fairness preserved.
Hardware Impact: 0 us/frame. Validation blocker only.
First 20 Minutes moment: None.
Route impact: Static 1336 proof is current; build/import/profiler proof remains pending.
Proof required: one scoped `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal /m:1` after CPU <= 50% and no dotnet/csc, then Unity import/edit tests/profiler.
Parked work rejected: False verified-green claim.

## Decision 068 - Null Manifest Telemetry Must Not Keep Warning Semantics

Problem: Null collection/shader slots became fail-closed in Loop 19, but private telemetry still had `NullCollection`, `NullShader`, `Warning`, and `Bootstrap.ShaderWarmup.Warning`. That stale schema is a regression path back to warning-and-continue.
Solution: Removed the stale null warning flags, warning phase, and warning hash. Null manifest failures now route only through `MissingShaderCollections` or `MissingShaderManifest`.
Rejected Alternatives: Leaving unused enum values as historical breadcrumbs; documenting around dead flags; reusing warning telemetry for fatal manifest defects.
Scalability potential: Same fail-closed manifest authority on Low/Middle/High/Ultra.
Hardware Impact: 0 us/frame. Private schema cleanup only.
First 20 Minutes moment: Boot -> first route rendering warmup.
Route impact: Broken shader manifests cannot be represented as warnings in the 1336 telemetry route.
Proof required: Unity import/edit test execution remains pending.
Parked work rejected: Warning-only null manifest semantics.

## Decision 069 - StreamingAssets Path Comparison Must Match Platform Case Rules

Problem: StreamingAssets root checks used `OrdinalIgnoreCase` on all platforms. Linux/macOS/Steam Deck path matching is case-sensitive, so a case-insensitive root proof is wrong for Vulkan/Linux targets.
Solution: Added `ResolvePathStringComparison`. Windows Editor/Player uses `OrdinalIgnoreCase`; all other platforms use `Ordinal`.
Rejected Alternatives: Global case-insensitive prefix check; platform-preprocessor split that misses Editor build target behavior; trusting `File.Exists` to catch a bad root after an unsafe prefix pass.
Scalability potential: Same route across Low/Middle/High/Ultra. Linux/Steam Deck path proof now matches filesystem behavior.
Hardware Impact: 0 us/frame. Cold PSO path validation only.
First 20 Minutes moment: Boot -> first Vulkan PSO trace load.
Route impact: `.graphicsstate` trace loading cannot pass the root guard through case-only path mismatch on case-sensitive platforms.
Proof required: Linux/Steam Deck player path drill remains pending.
Parked work rejected: Windows-centric path comparison.

## Decision 070 - URL StreamingAssets Cannot Be Claimed As File Trace Path

Problem: Unity documents Android/WebGL StreamingAssets paths as URL/JAR-backed, not directly accessible through synchronous file APIs. `GraphicsStateCollection` loading is filesystem-path based in this route, so URL roots must fail closed explicitly.
Solution: Added `TryGetStreamingAssetsFileSystemRoot` and `IsUrlLikePath`. URL/JAR StreamingAssets roots now return false before `Path.GetFullPath`/`File.Exists` probing.
Rejected Alternatives: Incidental exception/file-miss behavior; claiming Android/WebGL PSO trace support without a temp extraction route; using UnityWebRequest plus main-thread file write in the current patch without a measured bootstrap drill.
Scalability potential: Low/Middle Android/WebGL fail cleanly until an extraction route exists. PC/Linux/Steam Deck/console filesystem-backed paths remain deterministic. High/Ultra can still ship larger trace sets where filesystem-backed StreamingAssets is valid.
Hardware Impact: 0 us/frame. Cold PSO path validation only.
First 20 Minutes moment: Boot -> modern API PSO trace load.
Route impact: The current implementation no longer implies Android/WebGL `.graphicsstate` support. That platform requires a future measured extraction/cached-file route before claiming PSO proof.
Proof required: Platform-specific player drill and trace extraction design if Android/WebGL becomes a target route.
Parked work rejected: Fake multiplatform claim.

## Decision 071 - Current Build Gate After Loop 21

Problem: Runtime source, edit tests, reports, and logs changed after Loop 20, so compile proof is stale.
Solution: Re-ran static gates and sampled host state. CPU sampled 100.0% with 0 `dotnet` and 0 `csc`; AGENTS.md forbids launching `dotnet build` above 50% CPU. The latest attempted scoped build still has the foreign `PoolSlotData.AupCell` blocker.
Rejected Alternatives: Running dotnet during host saturation; editing Fauna/Gameplay ownership from shader warmup domain; reporting green.
Scalability potential: No runtime behavior change. Host fairness preserved.
Hardware Impact: 0 us/frame. Validation blocker only.
First 20 Minutes moment: None.
Route impact: Static 1336 proof is current; build/import/profiler proof remains pending.
Proof required: one scoped `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal /m:1` after CPU <= 50% and no dotnet/csc, then Unity import/edit tests/profiler.
Parked work rejected: False verified-green claim.

## Decision 072 - Configured GraphicsState URLs Must Not Fall Through

Problem: URL-like configured `.graphicsstate` paths could fall through the relative StreamingAssets branch and become malformed relative filenames before `File.Exists` failed. That is fail-closed by accident, not by contract.
Solution: Reject `IsUrlLikePath(configuredPath)` in `TryResolveGraphicsStateCollectionPath` and reject `IsUrlLikePath(normalizedRelativePath)` in `TryResolveStreamingAssetsPath`.
Rejected Alternatives: Letting `File.Exists` discover the bad route; accepting configured URLs without a measured extraction/cache route; string-parsing URLs into file paths.
Scalability potential: Same trace path authority across Low/Middle/High/Ultra. Future Android/WebGL extraction can add an explicit owned route without weakening current filesystem contract.
Hardware Impact: 0 us/frame. Cold PSO path validation only.
First 20 Minutes moment: Boot -> modern API PSO trace load.
Route impact: `.graphicsstate` configuration is now either filesystem-backed and explicit, or fail-closed.
Proof required: Platform-specific player drill remains pending.
Parked work rejected: Accidental fail-closed URL handling.

## Decision 073 - Loaded GraphicsStateCollection Needs Finally Cleanup

Problem: `WarmConfiguredGraphicsStateCollectionsAsync` destroyed a loaded `GraphicsStateCollection` after awaiting warmup. If the await path unwound unexpectedly, the UnityEngine.Object lifetime was not finally-guarded.
Solution: Wrap `WarmLoadedGraphicsStateCollectionAsync` in `try/finally` and always call `UnityEngine.Object.Destroy(collection)` after a successful load. No catch was added.
Rejected Alternatives: Catching broad exceptions; relying on the normal post-await destroy line; leaking a Unity object in a cold failure path.
Scalability potential: Same object lifetime behavior across tiers and platforms.
Hardware Impact: 0 us/frame. Cold PSO failure-path reliability only.
First 20 Minutes moment: Boot -> GraphicsStateCollection warmup.
Route impact: Every successfully loaded trace collection is destroyed even if warmup exits abnormally.
Proof required: Unity failure-drill with invalid/aborted trace collection remains pending.
Parked work rejected: Non-finally Unity object lifetime.

## Decision 074 - Current Build Gate After Loop 22

Problem: Runtime source, edit tests, reports, and logs changed after Loop 21, so compile proof is stale.
Solution: Re-ran static gates and sampled host state. CPU sampled 99.8% with 0 `dotnet` and 0 `csc`; AGENTS.md forbids launching `dotnet build` above 50% CPU. The latest attempted scoped build still has the foreign `PoolSlotData.AupCell` blocker.
Rejected Alternatives: Running dotnet during host saturation; editing Fauna/Gameplay ownership from shader warmup domain; reporting green.
Scalability potential: No runtime behavior change. Host fairness preserved.
Hardware Impact: 0 us/frame. Validation blocker only.
First 20 Minutes moment: None.
Route impact: Static 1336 proof is current; build/import/profiler proof remains pending.
Proof required: one scoped `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal /m:1` after CPU <= 50% and no dotnet/csc, then Unity import/edit tests/profiler.
Parked work rejected: False verified-green claim.

## Decision 075 - Editor Absolute Trace Paths Must Be Project-Owned

Problem: The Editor branch of `TryResolveGraphicsStateCollectionPath` accepted any absolute `.graphicsstate` path. That could let a local machine file outside the repository pass as PSO proof, while CI/player/import validation has no ownership over that artifact.
Solution: Added `TryGetProjectFileSystemRoot` and constrained rooted Editor paths to that root. Generic `Assets/...` and `ProjectSettings/...` editor paths now also recheck that the resolved full path remains inside the project root.
Rejected Alternatives: Trusting author discipline; allowing arbitrary absolute Editor paths; weakening the player StreamingAssets-only route to match Editor convenience.
Scalability potential: Low/Middle/High/Ultra trace payloads can scale by file size/count, but all traces must be project-owned or packaged. No tier receives a hidden local-machine dependency.
Hardware Impact: 0 us/frame. Cold PSO path validation only; prevents false validation, not steady CPU work.
First 20 Minutes moment: Boot -> modern API PSO trace load.
Route impact: Bootstrap PSO proof now depends on repository/project artifacts, not arbitrary workstation files.
Proof required: Unity editor path test and target player trace drill remain pending.
Parked work rejected: Editor-only absolute artifact proof.

## Decision 076 - GraphicsState Constructor Race Must Fail Closed

Problem: `TryLoadGraphicsStateCollection` checked `File.Exists` and then constructed `GraphicsStateCollection`. A delete/permission/share/race between those calls could throw IO or access exceptions out of the bootstrap gate.
Solution: Added typed catches for `IOException`, `UnauthorizedAccessException`, and `NotSupportedException`, with the same partial-object destroy/null/false route used by Unity/argument/operation failures.
Rejected Alternatives: Broad `catch (Exception)`; relying on `File.Exists`; letting a path race become an unclassified boot exception.
Scalability potential: Same fail-closed loader on weak and high-end devices. Larger High/Ultra trace sets get deterministic failure handling without changing runtime truth ownership.
Hardware Impact: Failure path only. Steady gameplay remains 0 us/frame.
First 20 Minutes moment: Boot veil before simulation begins.
Route impact: Bad or disappearing trace files fail the bootstrap warmup contract instead of escaping into uncontrolled exception flow.
Proof required: Unity failure-drill with inaccessible/deleted trace file remains pending.
Parked work rejected: Exception-driven path race.

## Decision 077 - Current Build Gate After Loop 23

Problem: Runtime source, edit tests, reports, and logs changed after Loop 22, so compile proof is stale again.
Solution: Re-ran static gates and sampled host state. CPU sampled 95.7% with 0 `dotnet` and 0 `csc`; AGENTS.md forbids launching `dotnet build` above 50% CPU. The latest attempted scoped build still has the foreign `PoolSlotData.AupCell` blocker.
Rejected Alternatives: Running dotnet during host saturation; editing Fauna/Gameplay ownership from shader warmup domain; reporting green.
Scalability potential: No runtime behavior change. Host fairness preserved.
Hardware Impact: 0 us/frame. Validation blocker only.
First 20 Minutes moment: None.
Route impact: Static 1336 proof is current; build/import/profiler proof remains pending.
Proof required: one scoped `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal /m:1` after CPU <= 50% and no dotnet/csc, then Unity import/edit tests/profiler.
Parked work rejected: False verified-green claim.

## Decision 078 - BootstrapEvents Must Not Own Persistent NativeQueues

Problem: A widened Roslyn audit over the whole bootstrap folder found two forbidden persistent native fields: `BootstrapEvents._pendingEvents` and `_nextFrameEvents` as static `NativeQueue<BootstrapEventPayload>`. This violated the memory sovereignty rule even though it was adjacent bootstrap infrastructure rather than the shader warmup loop.
Solution: Converted `BootstrapEventPayload` to `ISignal` and moved the lane to `SignalBus<BootstrapEventPayload>`. Producers use `TryPushTracked`, consumers read the current frame snapshot in `FlushPending`, and the static native queue fields plus sentinel registration/disposal were removed.
Rejected Alternatives: Keeping the queues because they were small; wrapping static queues in comments; replacing with managed `Queue<T>`; inventing a local Vault buffer without a dispatcher snapshot contract.
Scalability potential: Low/Middle/High/Ultra all use the first-party signal lane with a 4-payload cap. The route can scale by SignalBus contract constants without exposing local native ownership.
Hardware Impact: Removes two persistent native aliases. Runtime work remains bounded by 4 payloads in the late-frame event artery; no shader warmup frame allocation added.
First 20 Minutes moment: Bootstrap completion notification.
Route impact: Bootstrap completion now follows the same signal ownership doctrine as the rest of the runtime instead of a private static NativeQueue bridge.
Proof required: Unity compile/import remains pending; Roslyn native audit is green after the patch.
Parked work rejected: Direct `NativeQueue<T>` ownership in `BootstrapEvents`.

## Decision 079 - Current Build Gate After Loop 24

Problem: Runtime source and edit tests changed after Loop 23, so compile proof is stale again.
Solution: CPU gate opened once at 39.3% with no dotnet/csc, so a scoped `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal /m:1` was attempted. MSBuild returned MSB1009 because `Hecton8.Core.csproj` is absent from `C:\hades\Hecton8`; repository search found only tool/package/deprecated csproj files. A later CPU sample was 92.2%, so no further build was launched.
Rejected Alternatives: Creating a fake csproj; running broad package/tool builds as a substitute for Unity runtime compile; launching builds during CPU saturation.
Scalability potential: No runtime behavior change. Validation remains honest instead of synthetic.
Hardware Impact: 0 us/frame. Validation blocker only.
First 20 Minutes moment: None.
Route impact: Static Roslyn/source proof is current; Unity-generated project import/compile is still required for semantic compiler proof.
Proof required: regenerate Unity project files or run Unity import/editor tests, then compile.
Parked work rejected: False green compile claim.


## Decision 080 - BootstrapEvents Listener Dispatch Must Not Swallow Exceptions

Problem: After migrating `BootstrapEvents` from static `NativeQueue<BootstrapEventPayload>` fields to `SignalBus<BootstrapEventPayload>`, the late-frame flush still wrapped each listener call in `catch (Exception)` and logged through `H8Debug.LogException`. That is a managed exception/log allocation route inside the bounded bootstrap event artery and hides listener contract violations.
Solution: Removed `DispatchToListener`, broad catch, exception counters, and exception logging. `FlushPending` now calls `listener.OnBootstrapEvent(in payload)` directly from the index loop. `ListenerExceptionCount` remains as a compatibility property returning 0 because listener dispatch is no longer exception-isolated.
Rejected Alternatives: Keeping the catch as defensive glue; converting listener failures into managed logs; changing every external listener API in other domains during the 1336 pass.
Scalability potential: Low/Middle/High/Ultra all use the same 4-payload SignalBus lane. The route is fail-fast instead of silently hiding a broken listener.
Hardware Impact: Removes failure-path managed exception/log work from a late-frame bridge. Steady cost remains bounded by the listener count and payload cap; no shader warmup loop cost added.
First 20 Minutes moment: Bootstrap completion handoff to world/UI systems.
Route impact: Bootstrap event delivery no longer masks contract violations behind managed exception handling.
Proof required: Unity import/editor tests remain pending; source scanner now finds no `DispatchToListener`, `catch (Exception`, or `LogException` in `BootstrapEvents`.
Parked work rejected: Managed exception isolation in a hot bootstrap signal flush.

## Decision 081 - GameBootstrapper Event Source Must Use SignalBus

Problem: `GameBootstrapper` still owned a private managed `BootstrapEventQueue` with an internal `GameBootstrapperEventPayload[] _items` ring. It was bounded, but it duplicated the first-party SignalBus route and kept event publication owned by a custom managed queue instead of the project broadcast artery.
Solution: Converted `GameBootstrapperEventPayload` to `ISignal`, added an explicit 12-payload SignalBus lane configuration, and changed ready/failure publication to `SignalBus<GameBootstrapperEventPayload>.TryPushTracked`. `FlushPendingEvents` now drains `SignalBus<GameBootstrapperEventPayload>.GetFrameSnapshot` and forwards to the existing `IGameBootstrapperEventListener` public facade.
Rejected Alternatives: Editing many non-1336 consumers directly; deleting the listener facade and breaking public API; leaving the managed ring because it was small; using `HectonEventBus` for first-party bootstrap events.
Scalability potential: Low/Middle/High/Ultra all use the same source lane. Capacity and cadence are explicit and bounded; public consumers can later migrate to direct SignalBus reads without another event source.
Hardware Impact: Removes one managed bootstrap event source ring. Dispatch remains cold/late-frame bootstrap work, bounded by 12 payloads and existing listener count.
First 20 Minutes moment: Ready/failure bootstrap notification before first simulation tick.
Route impact: Bootstrap ready/failure signals now originate in the first-party SignalBus route while preserving existing cross-domain listener contracts.
Proof required: Unity compile/import remains pending; static source scanner asserts SignalBus configure/publish/snapshot tokens and absence of `BootstrapEventQueue`.
Parked work rejected: Private managed ring beside SignalBus.

## Decision 082 - Current Build Gate After Loop 25

Problem: Runtime source, edit tests, reports, and logs changed after Loop 24, so compile proof is stale again.
Solution: Re-ran static gates and Roslyn native alias audit. Repository search still found no `Hecton8.Core.csproj` in `C:\hades\Hecton8`, so there is no valid scoped Unity runtime project to build from this shell. No broad package/tool project was used as a substitute.
Rejected Alternatives: Creating a fake csproj; compiling unrelated tool projects as proof; touching other agents' compile blockers; reporting `VERIFIED_GREEN`.
Scalability potential: No runtime behavior change. Validation remains honest in the 20+ agent workspace.
Hardware Impact: 0 us/frame. Validation blocker only.
First 20 Minutes moment: None.
Route impact: Static 1336 proof is current; Unity-generated project import/compile and profiler proof remain pending.
Proof required: regenerate Unity project files or run Unity import/editor tests, then compile and capture boot profiler/GCMonitor.
Parked work rejected: False green compile claim.

## Decision 083 - GameBootstrapper Listener Facade Must Be Reentrant-Safe

Problem: `GameBootstrapper.FlushPendingEvents` drained `SignalBus<GameBootstrapperEventPayload>` but forwarded into the legacy public `IGameBootstrapperEventListener` facade by iterating `_listeners` directly. A listener could call `Register` or `Unregister` from inside `OnGameBootstrapperEvent`, mutating `_listeners` and `_listenerCount` during traversal.
Solution: Added `_isDispatchingGameBootstrapperEvents` and fixed deferred register/unregister buffers using the existing `ListenerSlot` struct. Register/unregister requests during dispatch are queued into fixed 12-slot cold buffers and applied in `finally` after each payload dispatch, matching the proven `BootstrapEvents` pattern.
Rejected Alternatives: Leaving direct mutation because the route is small; replacing the facade with a managed event; touching every external listener consumer in non-1336 domains; adding `List<T>` or LINQ cleanup.
Scalability potential: Low/Middle/High/Ultra keep the same bounded 12-payload source lane and 12 listener mutation slots. Future direct SignalBus consumers can bypass the facade without changing event source ownership.
Hardware Impact: 0 us/frame in gameplay. Bootstrap late-frame bridge remains bounded; the fix removes traversal instability rather than claiming a frame-time win on i3/MX350.
First 20 Minutes moment: Ready/failure bootstrap notification before first simulation tick.
Route impact: Ready/failure listeners cannot corrupt the current dispatch cursor or skip unintended listeners by mutating the facade during callback.
Proof required: Unity compile/import remains pending; static scanner now confirms 0 forbidden tokens in `FlushPendingEvents` and edit test asserts the deferred mutation tokens.
Parked work rejected: Cross-domain listener API migration.

## Decision 084 - Current Build Gate After Loop 26

Problem: Runtime source and edit-test source changed after Loop 25, so compile proof is stale again.
Solution: Re-ran static gates and sampled host state. CPU sampled 99.4% with 0 `dotnet` and 0 `csc`; AGENTS.md forbids launching `dotnet build` above 50% CPU. Repository search still finds no `Hecton8.Core.csproj` in `C:\hades\Hecton8`.
Rejected Alternatives: Running dotnet during host saturation; compiling unrelated tool/package projects as proof; creating a fake Unity csproj; reporting `VERIFIED_GREEN`.
Scalability potential: No runtime behavior change. Validation remains honest in a 20+ agent workspace.
Hardware Impact: 0 us/frame. Validation blocker only.
First 20 Minutes moment: None.
Route impact: Static 1336 proof is current; Unity-generated project import/compile and profiler proof remain pending.
Proof required: regenerate Unity project files or run Unity import/editor tests, then compile after CPU <= 50% and no dotnet/csc.
Parked work rejected: False green compile claim.

## Decision 085 - Background Domain Handshake Must Not Store Managed Exception State

Problem: The background domain handshake still used broad `catch (Exception)` around an unawaited boot Awaitable and stored `_backgroundDomainHandshakeError` as a managed string. If the directory creation failed, the route reported a string side channel instead of a bounded numeric state.
Solution: Replaced the string with `_backgroundDomainHandshakeFailureCode`. `RunBackgroundDomainHandshakeAsync` now switches to the background thread, calls `TryPrepareBackgroundDomainHandshake`, returns typed numeric codes for invalid path, IO, unauthorized, and unsupported path failures, then writes the final state on the main thread. `JoinBackgroundDomainHandshakeAsync` reads the numeric code and emits only fixed literal development logs.
Rejected Alternatives: Keeping the broad catch because it is cold boot; logging exception text; using `Task.Run`; treating missing telemetry directory as harmless.
Scalability potential: Low/Middle/High/Ultra all use the same boot handshake. Weak storage and permission failures fail closed without growing managed strings or leaving the handshake state opaque.
Hardware Impact: Failure path only. 0 us/frame during simulation and shader warmup; prevents an exception/log allocation route in boot.
First 20 Minutes moment: Boot veil before shader warmup and simulation readiness.
Route impact: Telemetry directory preparation no longer carries managed exception text through the bootstrap state machine.
Proof required: Unity import/editor compile remains pending; exact method scanner is green for the handshake methods.
Parked work rejected: Whole-file broad-catch purge outside 1336 ownership.

## Decision 086 - Current Build Gate After Loop 27

Problem: Runtime source, edit-test source, Roslyn native ledger, and reports changed after Loop 26, so compiler proof is stale again.
Solution: Re-ran the scoped static gates. The Roslyn native alias auditor was executed only against `Assets/_Project/Scripts/Bootstrap` and produced `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1336_BOOTSTRAP_LOOP27.json`: 9 files, 0 parse failures, 0 native fields, 0 forbidden persistent candidates. CPU sampled 100.0% with no `dotnet`/`csc`, and `Hecton8.Core.csproj` is still absent from `C:\hades\Hecton8`, so no build was launched.
Rejected Alternatives: Running a full project build during host saturation; compiling unrelated tools/packages as proof; creating a fake Unity csproj; reporting `VERIFIED_GREEN`.
Scalability potential: No runtime behavior change. Validation remains scoped and does not starve sibling agents.
Hardware Impact: 0 us/frame. Validation blocker only.
First 20 Minutes moment: None.
Route impact: Static 1336 proof is current; Unity-generated project import/compile, editor tests, profiler capture, and `.graphicsstate` trace artifacts remain pending.
Proof required: regenerate Unity project files or run Unity import/editor tests, then compile after CPU <= 50% and no dotnet/csc.
Parked work rejected: False green compile claim.

## Decision 087 - Empty Configured Master SVC Is A False Warmup Proof

Problem: `00_BOOTSTRAP` configured `Hecton8MasterVariants`, but the asset contained `m_Shaders: []`. `GameBootstrapper` skipped configured collections with `variantCount <= 0`, so the scene carried a warmup reference that proved nothing.
Solution: Populated `Hecton8MasterVariants.shadervariants` with the 15 first-20-minute manifest shader GUIDs and 18 variants, including the combined `DOTS_INSTANCING_ON INSTANCING_ON` route for `Hecton8_UberNoir`. Added `FindEmptyShaderVariantCollectionIndex` so any configured empty SVC fails closed with `InvalidCollectionSet`.
Rejected Alternatives: Leaving the empty master as a placeholder; silently continuing over empty SVCs; relying on per-feature collections to hide the bad master reference.
Scalability potential: Low/Middle/High/Ultra all boot from one authoritative non-empty collection. Quality still changes cadence only, not truth ownership.
Hardware Impact: Cold boot validation only. Prevents no-op warmup proof on low-end i3/MX350 class devices.
First 20 Minutes moment: Boot veil before flashlight/visor/VFX route.
Route impact: A missing or stale master SVC now blocks simulation instead of leaking first-use shader debt.
Proof required: Unity import must still validate the edited SVC asset.
Parked work rejected: Fake `VERIFIED_GREEN` without Unity import.

## Decision 088 - Scene SVC Cross-Product Waste

Problem: Runtime warmup loops over every configured collection, every manifest shader, and every `ShaderWarmupSetup`. Eight configured SVCs created repeated no-op collection/shader/setup attempts, and the empty master made the first configured collection useless.
Solution: `00_BOOTSTRAP` now wires a single authoritative master SVC. Per-feature SVC assets remain as authoring/source artifacts, but runtime warmup uses one configured collection. CLI mirror reports 1 configured collection, 18 variants, 15 manifest shaders, and 0 missing master shader GUIDs.
Rejected Alternatives: Adding a parallel per-collection shader manifest array; keeping eight runtime collections and accepting no-op attempts; deleting source SVC artifacts.
Scalability potential: Low tier gets fewer yielded boot slices; Middle/High/Ultra can expand the master SVC without changing runtime authority.
Hardware Impact: Reduces the warmup attempt multiplier from 8x manifest cross-product to 1x manifest cross-product. Exact microseconds require Unity profiler.
First 20 Minutes moment: Boot -> world load.
Route impact: Faster low-tier loading screen cadence while preserving fail-closed manifest coverage.
Proof required: Unity profiler capture of boot veil with `ShaderWarmup.WarmupShaderFromCollection` markers.
Parked work rejected: Duplicate per-feature runtime SVC references.

## Decision 089 - Direct Multi-Keyword Variants

Problem: `DirectShaderKeywordEntry` represented only one keyword. Shaders declaring both `#pragma multi_compile_instancing` and `#pragma multi_compile _ DOTS_INSTANCING_ON` can require combined keyword variants, especially for DOTS/instanced first-use render paths.
Solution: `DirectShaderKeywordEntry` now stores sorted `params string[] Keywords`. The editor compiler emits a direct `DOTS_INSTANCING_ON` + `INSTANCING_ON` entry for `Hecton8_UberNoir`, and no longer allocates `new[] { entry.Keyword }` per explicit variant.
Rejected Alternatives: Assuming single-keyword DOTS and instancing variants are enough; hand-maintaining only the raw SVC without fixing the compiler; broad-scanning all shaders outside the bootstrap prompt.
Scalability potential: Low devices do not pay runtime allocations; High/Ultra can add richer direct combos through deterministic compiler data.
Hardware Impact: Editor/build-time correctness. Runtime frame cost stays 0 us/frame.
First 20 Minutes moment: First instanced hull/radiation/Noir material route.
Route impact: Reduces first-use compile risk for DOTS+instancing material variants.
Proof required: Re-run the Unity editor compiler and inspect generated SVC after import.
Parked work rejected: Single-keyword-only direct manifest.

## Decision 090 - Presentation Gate Cancellation Must Not Throw

Problem: `InitializePresentationBootstrapAsync` wrapped the shader warmup gate in `catch (OperationCanceledException)` and used tokenized `AwaitableDebtMonitor.NextFrameAsync(cancellationToken: ct)`. This kept exception-driven cancellation in the owner phase immediately around warmup.
Solution: Rewrote the method to use explicit `ct.IsCancellationRequested` checks and non-tokenized `NextFrameAsync()`, returning false instead of throwing through the normal boot cancellation route.
Rejected Alternatives: Treating the method as outside 1336 scope; leaving exception cancellation because it is cold boot; replacing it with `Task`/coroutine handling.
Scalability potential: Same route on all tiers. Low tier avoids exception-control-flow overhead during long shader/PSO veil.
Hardware Impact: Cold boot only. No gameplay frame cost.
First 20 Minutes moment: Boot veil before first simulation tick.
Route impact: Simulation cannot start after a canceled warmup gate, and the path stays no-throw for normal cancellation.
Proof required: Unity editor compile remains pending.
Parked work rejected: Tokenized await/catch cancellation around the warmup gate.

## Decision 091 - Current Build Gate After Loop 28

Problem: Runtime source, editor source, scene YAML, SVC assets, tests, native ledgers, and reports changed after Loop 27, so compiler/import proof is stale again.
Solution: Re-ran scoped static gates. Native alias Roslyn audit over 9 bootstrap files and 36 editor test files reported 0 parse failures and 0 native field declarations. Exact method scanner reported 0 forbidden tokens in `InitializePresentationBootstrapAsync`, warmup, graphics-state, wait, event, and handshake methods. CPU sampled 100.0%, no `dotnet`/`csc` were running, and `Hecton8.Core.csproj` is absent in `C:\hades\Hecton8`, so no build was launched.
Rejected Alternatives: Running dotnet during CPU saturation; compiling unrelated packages/tools as substitute proof; editing other agents' build blockers; claiming Unity import/profiler success.
Scalability potential: No runtime behavior change. Validation remains honest in the multi-agent workspace.
Hardware Impact: 0 us/frame. Validation blocker only.
First 20 Minutes moment: None.
Route impact: Static 1336 proof is current; Unity import/editor compile, profiler capture, and `.graphicsstate` trace artifacts remain pending.
Proof required: Regenerate Unity project files or run Unity import/editor tests, then compile after CPU <= 50% and no dotnet/csc.
Parked work rejected: False green compile claim.

## Decision 092 - Editor Compiler Must Consume Scene Shader Manifest

Problem: Loop 28 made `00_BOOTSTRAP` runtime-authoritative by wiring one non-empty master SVC, but `HectonShaderVariantCollectionCompiler1336` still regenerated that master from direct roots and dependency/material traversal. A future menu-run compiler pass could silently drop a shader that remains in the scene's `shaderWarmupShaders` manifest.
Solution: Added `CollectBootstrapSceneShaderManifest`. The compiler now reads `00_BOOTSTRAP.unity`, parses the `shaderWarmupShaders:` GUID block, resolves each GUID with `AssetDatabase.GUIDToAssetPath`, and adds those shaders to the priority set before broad traversal. The scene manifest is now an input to the proof artifact, not a parallel hand-maintained list.
Rejected Alternatives: Trusting the current YAML asset forever; adding more static direct roots by hand; using a whole-project shader scrape that would reintroduce variant bloat.
Scalability potential: Low tier keeps one bounded master collection; Middle/High/Ultra can expand the scene manifest and compiler priority set without changing runtime authority or multiplying configured SVCs.
Hardware Impact: Editor/build-time only. Runtime steady state remains 0 us/frame; prevents first-use shader debt after future SVC regeneration.
First 20 Minutes moment: Boot veil before first HUD/scanner/flashlight/world VFX render route.
Route impact: `00_BOOTSTRAP.shaderWarmupShaders` is now the single owner for first-20-minute shader identity, and the compiler mirrors that route.
Proof required: Unity editor import/menu execution still pending; static source/test guards are updated.
Parked work rejected: Broad shader variant capture outside first-20-minute roots.

## Decision 093 - Current Build Gate After Loop 29

Problem: Editor compiler and editor test source changed after Loop 28, invalidating prior static proof.
Solution: Re-ran scoped static gates. `VaultNativeAliasRoslynAudit` over `Assets/_Project/Scripts/Bootstrap` produced `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1336_BOOTSTRAP_LOOP29.json`: 9 files, 0 parse failures, 0 native field declarations. The same audit over `Assets/_Project/Tests/Editor` produced `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_1336_TESTS_LOOP29.json`: 36 files, 0 parse failures, 0 native field declarations. The editor-folder parse audit had 383 files and 0 parse failures; it also exposed one unrelated persistent `NativeArray` in `Assets/_Project/Scripts/Editor/AssemblyGuard/CompileWallXRayWindow.cs`, outside the 1336 shader warmup path. CPU sampled 60.6% with no `dotnet`/`csc`, and `Hecton8.Core.csproj` is still absent, so no build was launched.
Rejected Alternatives: Hiding the unrelated editor native alias; running a broad build without a Unity-generated runtime csproj; compiling unrelated package projects as substitute proof.
Scalability potential: No runtime behavior change. Validation stays scoped and does not mutate other agents' editor diagnostics domain.
Hardware Impact: 0 us/frame. Validation blocker only.
First 20 Minutes moment: None.
Route impact: Static 1336 proof is current; Unity import/editor tests, compiler menu execution, profiler capture, and `.graphicsstate` trace assets remain pending.
Proof required: Regenerate Unity project files or run Unity import/editor tests, then compile after CPU <= 50% and no dotnet/csc.
Parked work rejected: False `VERIFIED_GREEN` report.

## Decision 094 - Scene Manifest GUID Extraction Must Filter Shader FileIDs

Problem: The scene YAML interval between `shaderWarmupShaders:` and `shaderGraphicsStateCollectionPaths:` also contains `runtimeShaderReferenceCatalog`, a ScriptableObject reference with GUID `66443d0a1f184aef87c6fd729fd8f401`. The editor compiler skipped it later by type, but the edit-test proof helper extracted all GUIDs and could treat that catalog as a shader.
Solution: Added `TryExtractShaderReferenceGuid` to require `fileID: 4800000` before extracting a scene shader GUID. Added `ExtractSceneShaderGuids` to the edit tests and asserted the catalog GUID is not part of shader manifest proof.
Rejected Alternatives: Moving the serialized catalog field in the scene; relying on Unity asset type resolution after accepting every GUID; leaving the test proof able to false-fail on a valid neighboring serialized reference.
Scalability potential: Low/Middle/High/Ultra all keep the same scene-owned shader identity route. Future serialized bootstrap references can be added near the manifest without corrupting shader coverage proof.
Hardware Impact: Editor/build-time only. Runtime steady state remains 0 us/frame.
First 20 Minutes moment: Boot veil shader coverage validation.
Route impact: The proof artifact now distinguishes real Shader refs from adjacent ScriptableObject refs.
Proof required: Unity editor tests still pending; static source parser and scene mirror are green.
Parked work rejected: Editing `RuntimeShaderReferenceCatalog` owner code outside 1336 domain.

## Decision 095 - Current Build Gate After Loop 30

Problem: Compiler source, editor tests, and proof ledgers changed after Loop 29.
Solution: Re-ran static gates. Bootstrap native alias audit: 9 files, 0 parse failures, 0 native fields. Editor-test native alias audit: 36 files, 0 parse failures, 0 native fields. Editor-folder parse audit: 383 files, 0 parse failures, and the same unrelated persistent `NativeArray` in `AssemblyGuard/CompileWallXRayWindow.cs`. Exact runtime method scanner: 0 forbidden hits. Corrected scene mirror: 15 shader refs, 16 total GUIDs in the YAML interval, 1 non-shader neighbor GUID, 15 master shader refs, 18 master variants, 0 missing, catalog leak false.
Rejected Alternatives: Counting every GUID in the YAML block as a shader; hiding unrelated editor native debt; running broad compilation without a generated Unity csproj.
Scalability potential: No runtime behavior change. Validation remains precise under growing bootstrap serialized data.
Hardware Impact: 0 us/frame. Validation blocker only.
First 20 Minutes moment: None.
Route impact: Static 1336 proof is current; Unity import/editor tests, compiler menu execution, profiler capture, and `.graphicsstate` trace assets remain pending.
Proof required: Regenerate Unity project files or run Unity import/editor tests, then compile after CPU <= 50% and no dotnet/csc.
Parked work rejected: False `VERIFIED_GREEN` report.
