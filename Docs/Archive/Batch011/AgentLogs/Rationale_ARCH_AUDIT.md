# ARCH_AUDIT Rationale

Date: 2026-05-20
Status: COMPLETE

## Decision 1

Problem: User requested a global architecture direction audit, but no CURRENT_BATCH XML or explicit agent id was provided.
Solution: Use ARCH_AUDIT as a bounded audit identity and create disk state under Docs/Tasks and Docs/AgentLogs.
Rejected Alternatives: Inventing a batch prompt or task count from unrelated archived batches would contaminate the audit.
Scalability potential: Audit will judge low, middle, high, and ultra paths through GlobalQualityWeight and Math LOD expectations.
Hardware Impact: No runtime code change. Expected runtime impact 0 us on i3/MX350.

## Decision 7

Problem: Second-pass audit found that some "getter" routes are not pure. `PlayerRuntimeContextService.TryGetActiveRuntimeContext()` resolves through `GlobalRegistry.PlayerRuntimeContextRuntime`, calls `SyncPlayerContext()`, and the fast path publishes movement snapshots. If many systems call this in one frame, a read path becomes a hidden multi-consumer mutation path.
Solution: Treat global/runtime context getters as read-only APIs. Runtime context services should publish once in their own dispatcher tick, then consumers read immutable frame snapshots or cached context without side effects.
Rejected Alternatives: Letting every fauna/audio/visual/tool consumer pull-and-sync the player context is convenient but creates unbounded per-frame duplicated work.
Scalability potential: Low tier benefits from one player-context publish per frame; middle/high/ultra can add richer context fields without multiplying sync cost by consumer count.
Hardware Impact: No runtime code change. Expected runtime impact 0 us on i3/MX350. Future savings require profiling after route cleanup.

## Decision 8

Problem: `GlobalDataVault.TryGetLatestCreated()` is documented as latest-created/editor diagnostics, but many runtime files use it as a fallback. That bypasses registry authority and can hide boot/lifecycle bugs.
Solution: Allow `TryGetLatestCreated()` only in bootstrap, diagnostics, editor tools, crash-dump, or controlled core fallback. Domain runtime systems should get `IDataVault` by injection/registry once, cache a generation handle, and fail closed if authority is absent.
Rejected Alternatives: Runtime fallback to "whatever vault was latest" masks ownership errors and makes multiple-vault/hot-swap behavior ambiguous.
Scalability potential: Clean vault ownership prevents low-tier relocation stalls and lets high/ultra increase arena-backed buffers without route ambiguity.
Hardware Impact: No runtime code change. Expected runtime impact 0 us on i3/MX350.

## Decision 9

Problem: Data Monolith source path is structurally good, but authoritative `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent in the current checkout. `Data/Balance/Baked/H8StaticData.bin` exists, but it is not the active StreamingAssets runtime payload.
Solution: Treat Data Monolith as source-ready but payload-blocked until the editor/build bake gate emits and validates `static_data.h8bin`.
Rejected Alternatives: Calling static data ready because an older baked binary exists elsewhere would be a false authority claim.
Scalability potential: Once baked, low tier can load compact static tables and high/ultra can consume larger table/radius data through the same monolith route.
Hardware Impact: No runtime code change. Expected runtime impact 0 us on i3/MX350.

## Decision 2

Problem: The question is architectural, so documentation-only claims can be stale or aspirational.
Solution: Read authority docs first, then verify claims against actual source files for registry, event buses, data vaults, memory allocators, tick dispatch, Burst jobs, and editor tools.
Rejected Alternatives: Reporting from PROJECT_ATLAS only is too weak; it can lag real code.
Scalability potential: Source-based audit can identify whether foundations actually permit toaster-to-ultra scaling.
Hardware Impact: No runtime code change. Expected runtime impact 0 us on i3/MX350.

## Decision 3

Problem: Global authority can become a new monolith if GlobalRegistry, GlobalSignals, DataVault, and HectonEventBus are treated as generic shared access points.
Solution: Judge the architecture by owner-local-first routing: GlobalRegistry for cold identity, SignalBus for first-party hot broadcasts, GlobalSignals as legacy/generated bridge, HectonEventBus for mod/cold projection, DataVault for cross-domain native state only.
Rejected Alternatives: A single universal bus/service locator is cheaper to write but destroys hot-path predictability and ownership.
Scalability potential: Low tier uses owner-local cached handles, capped signal lanes, coalescing, and survival frame limits; middle/high/ultra can spend the same routes on larger snapshots and extra visual lanes through continuous GlobalQualityWeight.
Hardware Impact: No runtime code change. Expected runtime impact 0 us on i3/MX350.

## Decision 4

Problem: The user's Burst concern is valid when C# schedules tiny jobs, completes them immediately, or copies data back to Unity objects in the same phase.
Solution: Check actual source for dispatcher-owned fences and representative heavy systems. SystemDispatcher combines simulation handles and completes in a post-simulation swap window; proximity, scatter, voxel, and fluid mostly delay or batch completion. Fluid still has a pre-schedule drain risk outside the formal swap window.
Rejected Alternatives: Declaring Burst/Jobs good or bad globally is technically false. The overhead depends on job cardinality, batching, data locality, and sync-point placement.
Scalability potential: Low tier should increase batching, reduce lane limits, and skip cosmetic jobs; middle/high/ultra can run heavier jobs only when the work is large enough to amortize scheduling and memory movement.
Hardware Impact: No runtime code change. Expected runtime impact 0 us on i3/MX350. Potential future savings are unclaimed until profiler artifacts exist.

## Decision 5

Problem: Editor/manual readiness can be confused with runtime readiness.
Solution: Classify editor windows/tools as STATIC_SOURCE only. Data Monolith compiler, save slot manager, performance/audit windows, signal validators, import/build guards, blackbox viewers, and domain tuners exist in source, but import/menu/asset/playmode proof was not produced.
Rejected Alternatives: Calling tools production-ready from file presence alone would be a fake report.
Scalability potential: Proper editor tooling supports low/middle/high/ultra tuning by baking data, validating layouts, and exposing continuous quality controls before runtime.
Hardware Impact: No runtime code change. Expected runtime impact 0 us on i3/MX350.

## Decision 6

Problem: Compile verification was requested by standing workflow, but this turn made no runtime code changes and the project rules forbid unnecessary dotnet builds, especially under possible parallel agent load.
Solution: Do not run a build. Mark runtime compile/profiler/playmode proof as pending, not green.
Rejected Alternatives: Running dotnet/Unity compile for an architecture audit could collide with other agents and produce noisy evidence unrelated to this no-code task.
Scalability potential: No scaling decision depends on a no-op build; future runtime proof must include profiler, GC, signal, and memory artifacts across quality weights.
Hardware Impact: No runtime code change. Expected runtime impact 0 us on i3/MX350.

## Decision 10

Problem: The audit findings existed in chat/report form but were not yet promoted into permanent project authority. Future agents could keep repeating the same hidden getter, DataVault fallback, and Jobs/Burst mistakes.
Solution: Insert a concise English doctrine block into `AGENTS.md` and mirror the enforceable parts into stable architecture docs and task-relevant mandates.
Rejected Alternatives: Leaving the rules only in chat is amnesia-prone. Creating a new isolated policy file would be weaker than updating the active authority spine.
Scalability potential: Low-tier systems get fewer hidden per-consumer syncs, hot registry polls, and schedule/complete stalls; high/ultra can spend saved cycles on visual overkill through the same routes.
Hardware Impact: Documentation-only change. Expected runtime impact 0 us on i3/MX350 until code cleanup follows.

## Decision 11

Problem: `Get*`/`TryGet*`/`Resolve*` APIs can become stealth mutation paths when they publish signals, sync context, allocate/grow buffers, or complete jobs.
Solution: Make read accessor purity a project-level rule in `AGENTS.md`, Global Authority docs, route-card/review gates, registry mandate, signal mandate, and native-memory mandate.
Rejected Alternatives: Reviewing these case-by-case without a named rule allows the pattern to return under different helper names.
Scalability potential: Owner-phase publication keeps cost O(owner) instead of O(consumers), which protects low-end CPUs and leaves high-tier headroom for richer snapshots.
Hardware Impact: Documentation-only change. Expected runtime impact 0 us on i3/MX350 until call sites are migrated.

## Decision 12

Problem: Data Monolith source readiness and DataVault convenience fallbacks can be confused with runtime authority readiness.
Solution: Require active `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` payload proof for Data Monolith readiness and restrict `GlobalDataVault.TryGetLatestCreated()` to bootstrap/editor/diagnostic/crash or documented core fallback.
Rejected Alternatives: Treating source files, old baked binaries, or latest-created Vault fallback as production authority hides boot and ownership errors.
Scalability potential: Stable payload and Vault ownership let low/middle/high/ultra scale capacity through declared routes instead of ambiguous runtime discovery.
Hardware Impact: Documentation-only change. Expected runtime impact 0 us on i3/MX350 until bake/runtime route cleanup follows.
