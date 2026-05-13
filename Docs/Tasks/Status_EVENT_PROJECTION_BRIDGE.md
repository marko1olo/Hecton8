# Status_EVENT_PROJECTION_BRIDGE

Prompt source: `Docs/Tasks/CURRENT_BATCH.md` `<AGENT_PROMPT id="EVENT_PROJECTION_BRIDGE" role="MODDING_LEAD">`
Agent: `MODDING_LEAD`
Current status: `PENDING VERIFICATION`
Last prompt extraction: 2026-05-13 via PowerShell `Select-String`.
Verification state: Unity MCP unavailable after final retry; `dotnet build Hecton8.Core.csproj` is blocked by global cross-domain compile errors outside the bridge slice.

## Checklist

- [x] Prompt extracted | DOD: CLI extraction isolated only the `EVENT_PROJECTION_BRIDGE` XML block cover-to-cover. Rejected: basic MCP/read-only skim because batch prompts can bleed neighboring agents. Estimate: 900 us one-time.
- [x] Task 1 SINGLETON ERADICATION | DOD: `rg` found no `HectonEventBus.Instance`; bridge resolves through `GlobalRegistry.ModdingBridge` via `IModdingBridge` and `GlobalRegistryServiceSlot.ModdingBridgeRuntime`. Rejected: new static singleton accessor. Estimate: 1 us/frame null-check path.
- [ ] Task 2 SIGNAL MIGRATION | [BLOCKED BY FIRST-PARTY MANAGED EVENT DEBT] DOD used: recon enumerated managed first-party publishers/subscribers in `Docs/Tasks/RECON_EVENT_PROJECTION_BRIDGE.md`. Rejected: blind mass migration, because cancellable damage and profile/meta systems require native signal/read-model contracts. Estimate: 0 us saved until separate migration task lands.
- [ ] Task 3 ASMDEF ISOLATION | [BLOCKED BY ASMDEF CYCLE] DOD used: assembly boundary audit. Rejected: adding `Hecton8.Modding.asmdef` now because current monolithic `Hecton8.Core.asmdef` still directly references modding types and there is no isolated `Core.Signals` asmdef to depend on cleanly. Estimate: 0 us runtime; build-risk avoided.
- [x] Task 4 DEAD CODE HUNT | DOD: searched `SubmarineStructuralGrid` and `FaunaBrain` for `EventBus.Publish` and `HectonEventBus`; no direct publish path found. Rejected: editing unrelated fauna/structure code. Estimate: 0 us/frame.
- [x] Task 5 THE BRIDGE JOB | DOD: `ModEventProjectionBridge` schedules Burst jobs after simulation and reads `SignalBus<CombatDamageSignal>` / `SignalBus<WeatherChangedSignal>` snapshots. Rejected: invoking managed callbacks from simulation/Burst. Estimate: 0 us with no projected subscribers; capped path pending profiler.
- [x] Task 6 UNMARSHALING DTO QUEUE | DOD: jobs write condensed `ModEventDto` values into persistent `NativeQueue<ModEventDto>`. Rejected: managed event objects and boxing. Estimate: 0 B managed allocation.
- [x] Task 7 MANAGED DISPATCHER | DOD: `LateFrameTick` drains `ModEventDto` and invokes `Action<ModEventDto>` after native simulation. Rejected: direct callback from first-party systems. Estimate: bounded by cap; pending profiler.
- [x] Task 8 STOPWATCH WATCHDOG | DOD: every projected mod callback is timed; over 2 ms disables that subscriber and logs `[MOD CULLED: TIMEOUT]`. Rejected: previous multi-stall tolerance for this bridge. Estimate: 0-2 ms hard ceiling per offending callback before cull.
- [x] Task 9 GC TRACKING | DOD: bridge and `ModCommandDispatcher` use `GC.GetAllocatedBytesForCurrentThread`; >1 MB/frame culls the mod. Rejected: cumulative-only 16 MB tolerance. Estimate: protects 1 MB/frame managed heap spike on i3/MX350.
- [x] Task 10 EVENT THROTTLING | DOD: projection cap is 50 events/frame. Rejected: full public signal replay. Estimate: O(min(signal count, 50)).
- [x] Task 11 EXCEPTION ISOLATION | DOD: managed mod delegate calls are wrapped in `try/catch`; Burst jobs contain no try/catch. Rejected: letting mod exceptions escape into first-party simulation. Estimate: zero on success path except managed call boundary.
- [x] Task 12 ZERO-GC FIRST PARTY | DOD: first-party systems continue publishing native `SignalBus<T>` data; projection is skipped when there are no projected mod subscribers. Rejected: always-on managed projection. Estimate: 0 B first-party managed allocation by design; measurement blocked.
- [x] Task 13 AUP SHIFT SAFETY | DOD: bridge converts world/AUP-facing positions to player-relative `float3` before exposing DTOs. Rejected: exposing absolute/AUP math to mod callbacks. Estimate: 3 subtracts/event.
- [x] Task 14 MATH LOD | DOD: low tier uses 10 projected events/frame; higher tiers use 50. Rejected: balanced single cap. Estimate: 80 percent event-loop reduction on low tier versus high cap.
- [x] Task 15 BLACKBOX DUMP | DOD: culled mod hash/reason data is stored in a fixed 300-entry native circular buffer and emitted through telemetry. Rejected: log-only cull reporting. Estimate: 32 bytes/entry persistent native memory.
- [x] Task 16 MOD COMMAND QUEUE | DOD: existing persistent `NativeQueue<ModCommand>` is drained in `PRE_SIMULATION`; LateFrame keeps deferred/render-only drains. Rejected: applying spawn/damage commands in LateFrame. Estimate: removes one-frame command latency; pending profiler.
- [x] Task 17 AWAITABLE MOD LOADER | DOD: `ModLoader.LoadMods` uses Unity `Awaitable.NextFrameAsync()` across hook install, discovery, and localization flush. Rejected: `Task`/threaded Unity object access and synchronous boot freeze. Estimate: distributes loader hitch across frames.
- [x] Task 18 FILE SCAN RECON | DOD: `Docs/Tasks/RECON_EVENT_PROJECTION_BRIDGE.md` documents managed event users, dead-code search, and blockers. Rejected: chat-only report. Estimate: 0 us runtime.
- [ ] Task 19 OMEGA COMPILE CHECK | [BLOCKED BY GLOBAL COMPILE WALL] DOD attempted: `dotnet build Hecton8.Core.csproj --no-restore`; Unity MCP refresh/console/script validation unavailable on retry. Rejected: claiming Burst verification without compiler evidence. Estimate: 0 us runtime change.

## Iteration Log

- Loop 1 complete: extracted prompt, read mandates, audited registry/event bus/signal bus. Tasks 1 and 4 completed; Tasks 2 and 3 identified as architectural blockers.
- Loop 2 complete: implemented `ModEventDto` and `ModEventProjectionBridge` Burst projection path for Tasks 5-7. Self-check: managed callbacks only run in LateFrame.
- Loop 3 complete: added timeout, GC cull, event cap, exception isolation, and low-tier cap for Tasks 8-11 and 14. Self-check: no try/catch inside Burst jobs.
- Loop 4 complete: added player-relative coordinate projection, blackbox cull telemetry, pre-simulation command draining, and Awaitable loader for Tasks 13 and 15-17.
- Loop 5 complete: recon file written for Task 18 and compile verification attempted for Task 19. Result remains `PENDING VERIFICATION` due existing global compile wall and unavailable Unity MCP session.
- Loop 6 complete: OMEGA polish pass read and executed. Replaced watchdog millisecond division with a cached reciprocal multiplier; no `foreach`, string formatting, `math.sqrt`, or `math.normalize` hits found in the bridge slice. Final Unity MCP validation retry failed because the Unity session is unavailable / timed out waiting for readiness. Status remains `PENDING VERIFICATION`.
