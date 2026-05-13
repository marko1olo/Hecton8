# LOG_EVENT_PROJECTION_BRIDGE

## 2026-05-13 - Native-to-Managed Mod Bridge

What was wrong:
- `HectonEventBus` is a managed mod callback surface, but first-party code still uses it for gameplay/meta events.
- Mod callbacks could allocate, stall, or throw without a projection-specific per-frame quarantine policy.
- Public native signals were not projected through a DTO queue; managed callbacks were not isolated behind a late-frame bridge.
- Mod commands were still drained in LateFrame for standard/AUP commands, adding unsafe timing for spawn/damage requests.

What was done:
- Added `IModdingBridge`, `GlobalRegistry.ModdingBridge`, and `GlobalRegistryServiceSlot.ModdingBridgeRuntime`.
- Added `ModEventDto` and `ModEventProjectionBridge`.
- Projection jobs read `SignalBus<CombatDamageSignal>` and `SignalBus<WeatherChangedSignal>` snapshots after simulation, write condensed DTOs into `NativeQueue<ModEventDto>`, and never invoke managed callbacks from Burst.
- LateFrame dispatcher invokes `Action<ModEventDto>` subscriptions through `HectonEventBus.SubscribeProjected` / `HectonAPI.Events.SubscribeProjected`.
- Added 2 ms Stopwatch cull, `[MOD CULLED: TIMEOUT]` logging, per-frame 1 MB GC cull, exception isolation, event cap 50, low-tier cap 10, and fixed 300-entry native cull telemetry.
- Moved standard/AUP `ModCommand` drains to `PRE_SIMULATION`; LateFrame keeps deferred event/render drains.
- Converted mod bootstrapping to Unity `Awaitable` across frames.
- Wrote `Docs/Tasks/RECON_EVENT_PROJECTION_BRIDGE.md` with first-party managed event debt and blockers.

Cinematic Cheats used:
- Sampled reality: mods receive at most 50 public events/frame, or 10 on low tier/MX350.
- Condensed metadata: mods get `ModEventDto`, not full first-party payloads or every physical collision.
- Relative coordinates: mods receive player-relative vectors instead of absolute/AUP complexity.

Exact Microseconds saved:
- Exact measured savings: not available. Unity MCP compile/console validation was unavailable, and `dotnet build Hecton8.Core.csproj` is blocked by existing global compile errors outside this slice.
- Estimated low-tier event-loop reduction: 80 percent versus the high-tier 50-event cap.
- Estimated no-subscriber first-party projection cost: 0 B managed allocation and a skipped bridge path after the subscriber-count guard.

Verification:
- `rg` found no `HectonEventBus.Instance`.
- `rg` found no direct `EventBus.Publish` / `HectonEventBus` use in `SubmarineStructuralGrid` or `FaunaBrain`.
- `dotnet build Hecton8.Core.csproj --no-restore` failed with 110 global errors from missing cross-domain types/namespaces including scheduling, environment fluids, memory layout, CCD physics, acoustic propagation, fauna interface mismatches, and signal contract gaps.
- Unity MCP `refresh_unity`, console read, and script validation failed because the Unity session was unavailable; final retry after OMEGA polish also timed out / returned no Unity session.

Status:
- `PENDING VERIFICATION`.
- Task 2 blocked by first-party managed event debt.
- Task 3 blocked by current asmdef/core-modding cycle.
- Task 19 blocked by global compile wall.

## 2026-05-13 - OMEGA Polish Addendum

What was wrong:
- The managed watchdog elapsed-time conversion still used a floating-point division.

What was done:
- Cached the Stopwatch tick-to-millisecond reciprocal and switched runtime elapsed conversion to multiplication.
- Re-scanned the bridge slice for `foreach`, string formatting/interpolation, `.ToString()`, `math.sqrt`, and `math.normalize`. No bridge-slice hits found.
- Confirmed try/catch blocks remain in managed dispatch code before Burst jobs; Burst projection jobs contain no try/catch.

Cinematic Cheats used:
- Event sampling stayed capped at 10 low / 50 high.
- Public DTO projection stayed condensed; no full signal replay added during polish.

Exact Microseconds saved:
- Exact measured savings unavailable due global compile wall and unavailable Unity MCP session.
- One floating-point division removed per projected managed callback dispatch.

## 2026-05-13 - Lifetime Hardening Addendum

What was wrong:
- Projection work could theoretically outlive the public signal snapshot frame if the late-frame dispatcher did not complete it before snapshot clear.
- DTO backlog state could be overwritten by a new post-simulation projection if late-frame budget deferred dispatch.
- `IsInitialized` could be true even when the late-frame dispatcher lane rejected the bridge.
- `SystemDispatcher.Update()` still read the bridge through `GlobalRegistry.ModdingBridge` each frame.

What was done:
- Added a projection/backlog guard so new post-simulation projection cannot start while a previous job or DTO backlog exists.
- Forced unfinished projection completion inside the late-frame swap window and emits a hashed performance warning for the overrun.
- Added stale queue-count recovery when the native queue is empty before the tracked DTO count reaches zero.
- Cached the active `IModdingBridge` in `SystemDispatcher` during bridge install/shutdown instead of polling the registry in the simulation frame.
- Rolled back registry/native queue bindings and released native state when late-frame registration fails, preserving the `IsInitialized` contract.

Cinematic Cheats used:
- Backpressure keeps the same sampled public reality model: 10 low-tier DTOs/frame, 50 higher-tier DTOs/frame.
- No full physics replay or managed payload expansion was added.

Exact Microseconds saved:
- Exact measured savings unavailable. Filtered `dotnet build Hecton8.Core.csproj --no-restore` still fails on global cross-domain missing-type debt; Unity MCP validation returned `no_unity_session`.
- Removed one per-frame registry bridge read from `SystemDispatcher.Update()`.
- Prevented a possible native snapshot lifetime fault; correctness impact is higher than any microsecond estimate.
