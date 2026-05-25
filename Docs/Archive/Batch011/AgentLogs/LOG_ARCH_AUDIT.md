# ARCH_AUDIT Log

## 2026-05-20 Architecture/Foundation Audit

What was wrong:
- Project direction had to be checked against source, not remembered discussion or stale docs.
- Global authority surface is large: current scan found 6138 `GlobalRegistry.` hits under `Assets/_Project/Scripts`.
- Signal surface is mixed but improving: 257 `GlobalSignals.Publish`, 302 direct `SignalBus<T>.Push`, 310 `GetFrameSnapshot`, and 19 `HectonEventBus.Publish` hits under scripts.
- Runtime proof is still absent in read docs: no fresh Unity import, Console, Play Mode, Profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save-load, or visual route proof was produced in this audit.
- Burst/Jobs overhead risk is not imaginary. It appears when jobs are small, same-frame completed, or data is copied back to managed/Unity objects too often.

What was done:
- Read authority docs, domain map, 8 relevant mandates, global architecture docs, runtime execution plan, quality gates, global authority boundaries, signal corridor, registry locator, Data Monolith, arena, dispatch, and boot topology.
- Checked source for `GlobalRegistry`, `GlobalSignals`/`SignalBus<T>`, `SystemDispatcher`, `DispatcherJobFence`, `DispatcherJobSwap`, `GlobalDataVault`, `HectonEventBus`, `GameBootstrapper`, and representative heavy Burst/job paths.
- Spawned three read-only sub-auditors for core authority, Burst/jobs, and editor/manual surfaces; integrated only evidence that matched source/docs.
- Mapped editor/manual tools: Data Monolith compiler, save slot manager, performance/audit windows, render/URP validators, import/build guards, signal/layout validators, blackbox viewers, physics/world/audio/UI tuners, and smoke probes.

Cinematic Cheats used:
- No runtime cinematic cheat was implemented. Audit confirmed the documented direction is visual-fake-first: capped/coalesced signal lanes, continuous GlobalQualityWeight, Math LOD, dispatcher time slicing, and hybrid Burst jobs instead of DOTS migration for appearance.

Exact Microseconds saved:
- Measured runtime saved: 0 us. No runtime code was changed.
- Claimed runtime optimization savings: 0 us. No profiler evidence was collected.
- Avoided false-positive build/compile workload: unmeasured. No dotnet/Unity build was launched.

Verdict:
- Direction is globally correct: hybrid foundation, cold GlobalRegistry, typed hot SignalBus snapshots, DataVault-backed cross-domain native memory, dispatcher-owned job fences, H8Memory/native sentinel tracking, blackbox telemetry, and continuous quality scaling.
- State is not green. It is a controlled danger zone: authority surface is broad, legacy publish routes remain common, DataVault migration is incomplete by docs, giant files remain risk multipliers, and runtime proof is still pending.
- Burst/Jobs should stay, but only as amortized batch work behind dispatcher phases. Tiny/noisy jobs, same-frame Schedule->Complete, and forced readback loops must be rejected unless profiler artifacts prove the gain.

## 2026-05-20 Follow-up: Global System Doctrine

What was wrong:
- Some read-looking global routes are mutators. `PlayerRuntimeContextService.TryGetActiveRuntimeContext()` calls `SyncPlayerContext()` and can publish movement snapshots on each consumer call.
- `GlobalDataVault.TryGetLatestCreated()` is widely used as a runtime fallback even though its own comment describes latest-created/editor diagnostics.
- Data Monolith source path exists, but the authoritative player payload `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent. `Assets/AddressableAssetsData` and `Assets/_SourceData` are present with 0 entries.
- Mixed signal style remains: legacy `GlobalSignals.Publish` and direct `SignalBus<T>.Push` coexist in active gameplay code.

What was done:
- Quantified hot global surfaces by file, checked player/fauna/audio/fluid examples, verified `PlayerRuntimeContextService` side effects, verified DataVault latest-created fallback surface, checked Data Monolith payload path, and reviewed dispatcher post-fixed swap behavior.

Cinematic Cheats used:
- None implemented. The doctrine remains: visual fakes, Math LOD, capped signals, coalesced signals, and one-owner snapshots before simulation detail.

Exact Microseconds saved:
- Measured runtime saved: 0 us. No runtime code changed.
- Future target: remove duplicated player-context sync/publish calls, but savings are not claimed without profiler evidence.

Near-term doctrine:
- Getters must not publish, sync hierarchies, allocate, grow buffers, or complete jobs.
- GlobalRegistry is bootstrap/cold dependency identity only; hot systems cache interfaces or consume snapshots.
- DataVault is not a dictionary. Allocate/grow during owner initialization; hot paths resolve stable handles only.
- SignalBus is the first-party hot broadcast route; GlobalSignals is legacy/generated bridge; HectonEventBus is mod/cold.
- Jobs are accepted only when work is large enough and completion happens in dispatcher-owned windows.
- Data Monolith cannot be called runtime-ready until `static_data.h8bin` exists and the player boot/import proof exists.

## 2026-05-20 Master Prompt Rule Promotion

What was wrong:
- The global-system audit doctrine was not yet embedded in the permanent authority spine. Future agents could still treat the findings as optional chat memory.
- Existing docs already had the right direction, but the exact hard blockers for pure accessors, DataVault latest-created fallback, Jobs/Burst amortization, and Data Monolith payload proof were not centralized enough.

What was done:
- Added the English global-systems doctrine block to `AGENTS.md`.
- Updated stable architecture docs: `GLOBAL_AUTHORITY_OPERATING_MODEL.md`, `GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`, `GLOBAL_AUTHORITY_BOUNDARIES.md`, `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`, `GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`, `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`, `GLOBAL_REGISTRY_SERVICE_LOCATOR.md`, `GLOBAL_SIGNAL_CORRIDOR.md`, and `DATA_MONOLITH_H8BIN_SPEC.md`.
- Updated mandates: `.agents-skills/README.md`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `ARCH_Signal_Lane_Segregation.txt`, `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`, `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, and `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`.
- Build was not run because this was a documentation/mandate update and the worktree is under heavy parallel activity. Runtime proof remains pending.

Cinematic Cheats used:
- No runtime cheat implemented. Governance now explicitly preserves the existing visual-fake-first, continuous `GlobalQualityWeight`, SignalBus snapshot, and dispatcher-window model.

Exact Microseconds saved:
- Measured runtime saved: 0 us.
- Claimed runtime optimization savings: 0 us.
- Expected future target after code cleanup: fewer hidden per-consumer syncs, fewer hot registry polls, fewer same-frame job stalls, and fewer ambiguous DataVault fallbacks. No savings claimed without profiler evidence.

Inserted doctrine:
- One fact -> one owner -> one route -> one proof artifact.
- Read accessors are pure: no publish, sync, allocation/growth, job completion, global mutation, or scene search.
- Runtime context owners publish once from owner phases; consumers read snapshots or cached interfaces.
- `GlobalRegistry` is cold identity/DI only.
- `SignalBus<T>` is first-party hot broadcast; `GlobalSignals` is bridge/legacy; `HectonEventBus` is mod/API/cold only.
- `GlobalDataVault` is cross-domain native ownership, not a global heap; `TryGetLatestCreated()` is not a domain runtime fallback.
- Burst/Jobs require amortized data-local batches and dispatcher-owned completion windows.
- Data Monolith readiness requires the active StreamingAssets `static_data.h8bin` payload and import/bake/boot proof.
- `GlobalQualityWeight` is continuous and cannot change gameplay truth ownership, DTO layout, save identity, or authority route.
