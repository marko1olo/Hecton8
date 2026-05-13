# Rationale_EVENT_PROJECTION_BRIDGE

## Decision 0 - Prompt Isolation And Authority

Problem: The user supplied an agent identity and prompt id, but the root `CURRENT_BATCH.md` path did not contain the target prompt.
Solution: Used `Docs/Tasks/CURRENT_BATCH.md` and an attribute-aware PowerShell raw-read regex to isolate exactly `<AGENT_PROMPT id="EVENT_PROJECTION_BRIDGE" role="MODDING_LEAD">`.
Rejected Alternatives: MCP/basic file reading and neighboring prompt context were rejected because batch files can truncate or bleed adjacent agent directives.
Scalability potential: Low/Middle/High/Ultra unchanged; this is governance work to prevent architectural bleed.
Hardware Impact: 0 us runtime impact on i3/MX350.

## Decision 1 - Required Mandate Set

Problem: Mod event projection touches core signal routing, native queues, managed callbacks, AUP conversion, telemetry, and async loading.
Solution: Mandates selected before code edits: `ARCH_Global_Registry_ServiceLocator_DI_Init`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, `DBG_Telemetry_Crash_Reporting_PostMortem`, `MATH_Coordinate_Precision_AUP_FloatingOrigin`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits`, `STRM_Async_Standard`, `ARCH_Project_Bootstrap_Sequence_Init_Safety`.
Rejected Alternatives: Reading all mandate files was rejected as context waste; reading only EventBus code was rejected because task spans managed/native boundary and loader timing.
Scalability potential: Low tier must sample fewer mod events; Ultra tier can project richer public metadata without taxing first-party simulation.
Hardware Impact: Expected target is bounded bridge overhead under 0.1 ms on i3/MX350 after cap/throttle; no measured proof yet.

## Decision 2 - Registry Owned Modding Bridge

Problem: The prompt demanded `HectonEventBus.Instance` purge and GlobalRegistry resolution for the mod bus.
Solution: Added `IModdingBridge`, `GlobalRegistry.ModdingBridge`, and `GlobalRegistryServiceSlot.ModdingBridgeRuntime`; `ModEventProjectionBridge` registers and unregisters itself through the registry.
Rejected Alternatives: A new static singleton accessor was rejected because it preserves the same dependency shape under another name. Direct first-party references to `HectonEventBus` were rejected for new projection code.
Scalability potential: Low/Middle/High/Ultra all use the same registry contract; the internal bridge changes caps by tier without changing callers.
Hardware Impact: Registry read is a static field read; estimated 1 us/frame or less, unmeasured.

## Decision 3 - Native Snapshot To DTO Queue Projection

Problem: Mods need public events, but managed callbacks cannot run inside Burst or first-party simulation without GC and determinism risks.
Solution: Burst jobs read `SignalBus<CombatDamageSignal>` and `SignalBus<WeatherChangedSignal>` snapshots, condense them into `ModEventDto`, and enqueue them into `NativeQueue<ModEventDto>`. Managed delegates run later in `LateFrameTick`.
Rejected Alternatives: Direct `Action<T>` invocation from first-party systems was rejected for GC and exception leakage. Copying full gameplay payloads was rejected because modders do not need internal authority state.
Scalability potential: Low tier projects 10 events/frame; Middle/High/Ultra project 50 events/frame and can spend saved cycles on richer public metadata later.
Hardware Impact: First-party no-subscriber path is designed for 0 B managed allocation. Capped queue work is O(10) on MX350 low tier and O(50) on higher tiers; profiler proof blocked.

## Decision 4 - Immediate Per-Mod Cull Policy

Problem: A single mod callback can stall the frame or allocate enough memory to destabilize low-end hardware.
Solution: Wrapped each projected callback in `Stopwatch`, `GC.GetAllocatedBytesForCurrentThread`, and `try/catch`. Over 2 ms, over 1 MB/frame, or exception disables that subscriber and emits telemetry.
Rejected Alternatives: The previous multi-stall/cumulative-only tolerance was rejected for this bridge because projected callbacks sit on a public mod boundary and must fail closed.
Scalability potential: Low tier benefits most because one bad mod cannot consume the entire frame budget. Ultra tier can allow more projected events but not more per-mod abuse.
Hardware Impact: Prevents repeated >2 ms callback stalls and >1 MB/frame managed heap spikes on i3/MX350.

## Decision 5 - Pre-Simulation Mod Command Drain

Problem: Spawn/damage commands requested by mods must enter the simulation at a deterministic boundary, not after rendering decisions.
Solution: Moved standard and AUP `ModCommand` drains into `SystemDispatcher.Update()` before gameplay simulation. Deferred managed events and render commands remain in LateFrame.
Rejected Alternatives: Keeping all mod drains in LateFrame was rejected because it adds at least one frame of latency and mixes simulation authority with presentation.
Scalability potential: Low tier gets predictable command admission; high tier can process the same queue without changing the authority boundary.
Hardware Impact: Timing safety improvement; direct microsecond savings unmeasured.

## Decision 6 - ASMDEF Isolation Blocked

Problem: The prompt requires `Hecton8.Modding` to depend on Contracts and Core.Signals, but current files are still inside a broader core assembly and core types reference modding implementation directly.
Solution: Marked Task 3 blocked and documented the dependency cycle in recon/status instead of fabricating an asmdef that breaks the project harder.
Rejected Alternatives: Creating a `Hecton8.Modding.asmdef` inside the current dependency graph was rejected because `GlobalRegistry` and dispatcher code still reference modding types and no clean `Core.Signals` asmdef exists.
Scalability potential: Correct assembly isolation later will improve compile boundaries across all tiers; no runtime tier impact now.
Hardware Impact: 0 us runtime impact; avoids build churn.

## Decision 7 - First-Party Managed Event Migration Blocked

Problem: Task 2 requires all first-party `HectonEventBus` subscriptions to move to native `SignalBus<T>`, but recon found live gameplay/meta consumers including cancellable player damage.
Solution: Implemented the isolated projection bridge and documented all known first-party managed users in `RECON_EVENT_PROJECTION_BRIDGE.md`; marked full migration blocked pending native contracts.
Rejected Alternatives: Blind search-and-replace was rejected because cancellable managed payloads and profile/meta side effects need deterministic replacement contracts.
Scalability potential: Once migrated, Low tier avoids managed event traffic entirely; High/Ultra can still project public mod DTOs without contaminating first-party logic.
Hardware Impact: Current bridge prevents new first-party managed bridge cost; existing managed event debt remains until follow-up migration.

## Decision 8 - Verification Wall

Problem: The final task requires Burst compile verification, but the project does not compile globally and Unity MCP validation is unavailable.
Solution: Attempted `dotnet build Hecton8.Core.csproj --no-restore` and retried Unity MCP validation after polish; recorded the global compile wall / no Unity session and kept status `PENDING VERIFICATION`.
Rejected Alternatives: Claiming verification from source inspection was rejected. Reverting unrelated broken dependencies was rejected because parallel agents own those slices.
Scalability potential: None until compiler evidence exists.
Hardware Impact: No measured hardware impact can be claimed until Unity compile and profiler data are available.

## OMEGA POLISH CHANGES

Problem: The polish mandate required an anti-bloat pass after core tasks were completed or blocked. The bridge still had one floating-point division in the managed watchdog elapsed-time conversion.
Solution: Cached `1000.0 / Stopwatch.Frequency` as `_stopwatchTicksToMilliseconds` and converted elapsed milliseconds with multiplication. Re-ran local scans for `foreach`, `string.Format`, interpolation, `.ToString()`, `math.sqrt`, and `math.normalize` across the bridge slice; no hits were found. Burst jobs use bounded `for` loops and bitmask sample flags.
Rejected Alternatives: Leaving the division was rejected under the frame-time dictatorship rule. Replacing finite-value guards in Burst jobs with branchless masks was rejected because they are safety clamps, not visual approximation math, and they only run on capped public-signal projection.
Scalability potential: Low stays at 10 projected DTOs/frame. Middle/High/Ultra stay at 50 DTOs/frame. The cinematic cheat remains sampled public reality instead of full signal replay.
Hardware Impact: One managed watchdog division removed per dispatched projected mod callback. Exact microsecond gain is unmeasured; expected impact is sub-us but deterministic. Unity validation retry remained unavailable, so no measured claim is recorded.

Final Git Diff:
- `M Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs` - added `Hecton8.Core.Signals` import and cached watchdog reciprocal multiplier.
- `M Docs/Tasks/Status_EVENT_PROJECTION_BRIDGE.md` - checklist, blocker status, iterative loop evidence, polish loop.
- `M Docs/AgentLogs/Rationale_EVENT_PROJECTION_BRIDGE.md` - decisions and OMEGA polish rationale.
- `?? Docs/Tasks/RECON_EVENT_PROJECTION_BRIDGE.md` - first-party managed event recon.
- `?? Docs/AgentLogs/LOG_EVENT_PROJECTION_BRIDGE.md` - CTO-facing final log.
