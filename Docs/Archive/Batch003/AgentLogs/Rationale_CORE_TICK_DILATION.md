# CORE_TICK_DILATION Rationale

Status: PENDING VERIFICATION (GLOBAL COMPILE DEPENDENCIES)
Domain: CORE & MEMORY INFRASTRUCTURE (Engine Foundation)

## Loaded Mandates

- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- ARCH_Project_Bootstrap_Sequence_Init_Safety.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- CORE_Global_State_Reset_NonReload_Transitions.txt

## Decision Journal

Problem: Unity `Time.timeScale` and singleton `TimeManager.Instance` create deterministic timing and init-order risk.
Solution: Build a GlobalRegistry-owned dispatcher with explicit bootstrap registration, dense preallocated buckets, dilated and unscaled time lanes.
Rejected Alternatives: Unity `Time.timeScale`, coroutine delays, and singleton self-registration. They break determinism, allocate, or violate bootstrap ownership.
Scalability potential: Low uses dispatcher math only and disables bullet-time post effects; Middle allows cheap post signal; High enables richer post layering; Ultra can spend saved CPU/GPU headroom on stronger visual treatment without changing simulation truth.
Hardware Impact: Expected low-end i3/MX350 gain is removal of singleton lookup churn and coroutine/task delay allocations in timing paths; exact microseconds remain PENDING PROFILER.

Problem: Existing timing was split between Unity globals, menu code, physics hit-stop, and dispatcher accumulators.
Solution: Route pause and dilation through `ITickDispatcher` plus `SimulationPauseSignal`; store canonical time in a four-slot NativeArray SOA.
Rejected Alternatives: `Time.timeScale` writes, coroutine waits, and a new MonoBehaviour time owner. They create hidden order dependencies and extra Unity message surfaces.
Scalability potential: Low uses no post-process bullet-time and only scalar math; Middle uses signal-driven stress fake; High and Ultra can increase fullscreen treatment without changing simulation cadence.
Hardware Impact: i3/MX350 estimate is 0.005-0.008 ms saved during pause/hit-stop paths by avoiding scattered global state and redundant fixed catch-up.

Problem: Four update cadences must run without spawning extra Unity message loops or managed iterator garbage.
Solution: Extend the existing `SystemDispatcher` with preallocated registry buckets and reverse-index loops; keep all cadence gates inside the single `Update`.
Rejected Alternatives: `List<T>.ForEach`, coroutines, timers, or a second dispatcher MonoBehaviour. They add GC or non-deterministic order.
Scalability potential: Low tier clamps bullet-time visuals and uses scalar math only; Middle/High/Ultra can use the same signal to buy stronger audio/visor treatment while simulation cost stays flat.
Hardware Impact: i3/MX350 estimate is 0.006-0.012 ms saved in subscriber iteration at 100+ tickables versus scattered Update methods, with bigger gains under hitch catch-up.

Problem: Audio and physics were tied to Unity global timescale behavior.
Solution: Physics fixed accumulation consumes dilated dispatcher delta; world audio samples `GlobalSignals.TimeDilationScalar` and applies a floor-limited pitch ratio to 3D/non-UI sources.
Rejected Alternatives: mixer-only slow motion and full pitch scaling. Mixer-only desyncs gameplay audio, full scaling sounds fake and damages intelligibility.
Scalability potential: Low uses the audio scalar only; Middle adds visor stress fake; High/Ultra can layer stronger fullscreen work because the DSP operation remains a scalar multiply.
Hardware Impact: i3/MX350 estimate is below 0.003 ms per scalar transition because active source pitches are refreshed only on meaningful scalar change.

Problem: Pause must freeze simulation without freezing diegetic UI, menu input, or visual feedback.
Solution: Add `IUnscaledFastTickable`, move pause menu and suit HUD presentation onto it, and let only core/menu-safe late-frame queues drain while simulation scalar is zero.
Rejected Alternatives: global `Time.timeScale` pause, queue flushing, or leaving UI on the dilated lane. They either freeze menus or destroy queued simulation truth.
Scalability potential: Low tier keeps the lane and disables bullet-time post; Middle/High/Ultra reuse the signal to enrich visor feedback.
Hardware Impact: i3/MX350 estimate is under 0.001 ms for unscaled UI registration and up to 0.1 ms saved while paused by skipping simulation arteries.

Problem: AUP rebasing can create a false large delta or same-frame simulation with shifted coordinates.
Solution: Tie the dispatcher pause to existing origin-shift frame lock and avoid destructive reads from `AupPreShiftSignal`.
Rejected Alternatives: draining the global AUP queue inside dispatcher. That would steal packets from other systems and violate multi-consumer signaling.
Scalability potential: Low/Middle/High/Ultra share identical deterministic pause semantics; visuals scale independently through the bullet-time signal.
Hardware Impact: i3/MX350 estimate is a sub-microsecond branch and prevention of expensive post-rebase recovery work.

Problem: Tick subscribers need four cadences without allocator debt or iterator garbage.
Solution: Use preallocated registry buckets and raw reverse-index loops for fast, slow, cold, frost, and unscaled UI lanes.
Rejected Alternatives: `List<T>.ForEach`, LINQ, coroutine cadences, and timer callbacks. They allocate or hide scheduling order.
Scalability potential: Low keeps the same flat traversal and disables bullet-time post; Middle/High/Ultra can add more subscribers without multiplying Unity message loops.
Hardware Impact: i3/MX350 estimate is 0.006 ms saved at 128 subscribers versus scattered `Update` calls; not profiler-confirmed due compile wall.

Problem: Slow motion must read as cinematic without spending low-tier fill-rate.
Solution: Publish `BulletTimeVisualSignal` and let the visor post layer the fake only outside low tier; the simulation remains scalar math.
Rejected Alternatives: physical motion blur, per-object temporal trails, and always-on post. They spend GPU to simulate a controllable presentation effect.
Scalability potential: Low = scalar only, no visor post. Middle = mild vignette/chroma. High = stronger fullscreen treatment. Ultra = overkill visual stack with unchanged tick truth.
Hardware Impact: MX350 estimate is 0.02 ms fill saved during bullet-time by zeroing the post intensity on low tier.

Problem: A crash during time dilation needs objective state, not a verbal guess.
Solution: Write scalar and tick overhead through `CrashTelemetryBuffer.ReportTimeDilationState` into the fixed telemetry ring.
Rejected Alternatives: `Debug.Log`, ad hoc text dumps, and no blackbox. Logs allocate and do not preserve the last-frame state consistently.
Scalability potential: Low/Middle/High/Ultra share the same blackbox record while visuals scale independently.
Hardware Impact: i3/MX350 estimate is below 0.001 ms per frame for a fixed ring write.

Problem: Compile verification is blocked outside this task after the dispatcher code path was cleaned.
Solution: Stop at the dependency wall per 3-strikes protocol and record exact blockers: active `Hecton8.Core.csproj` lacks generated references for concurrently added `Hecton8.Core.Memory`, `Hecton8.Physics.Determinism`, and `Hecton8.Cartography` assemblies.
Rejected Alternatives: rewriting other agents' assembly boundaries, editing unrelated generated project ownership, or reverting untracked memory/physics/cartography work. That would violate concurrent-agent ownership.
Scalability potential: Once the generated project graph is refreshed, this dispatcher design remains independent of those domain implementations through registry/signals.
Hardware Impact: Runtime impact is none; verification is blocked before player execution, so GC-byte measurement remains static-only.

## OMEGA POLISH CHANGES

Problem: Polish audit found a perceptual audio pitch curve using `math.sqrt` for slow-motion easing.
Solution: Replaced it with `saturatedScalar * (2f - saturatedScalar)`, a multiply-only ease-out that keeps the same "not heavily pitched down" intent.
Rejected Alternatives: Keeping `math.sqrt`, adding a LUT for one scalar, or pushing mixer snapshots. The sqrt was unnecessary; a LUT would add complexity for one value; snapshots would hide the deterministic scalar.
Scalability potential: Low gets the cheapest pitch fake plus disabled visor post; Middle keeps mild audio/visor feedback; High and Ultra can layer heavier visuals without changing simulation cadence.
Hardware Impact: i3/MX350 estimate is 0.2 us saved per scalar refresh; exact profiler data blocked by global compile dependencies.

Problem: Cross-domain edits were required by prompt tasks but could violate ownership if coupled directly.
Solution: Audio, UI, visor, watchdog, physics hit-stop, and scene reset integrations route through `GlobalSignals`, `ITickDispatcher`, or `GlobalRegistry` instead of direct subsystem dependencies.
Rejected Alternatives: direct calls into concrete subsystem owners, singleton state, or `Time.timeScale` writes. Those would create ordering debt and break concurrent-agent isolation.
Scalability potential: Low/Middle/High/Ultra receive the same core scalar; each presentation domain can scale cost locally.
Hardware Impact: i3/MX350 estimate is 3-6 us saved during pause/hit-stop transitions by centralizing the state change and avoiding redundant Unity global writes.

Problem: Final polish required a diff and zero-GC audit.
Solution: Static audit found no `Task.Delay`, `Time.deltaTime`, `Time.fixedDeltaTime`, `foreach`, `.ForEach`, `string.Format`, or interpolated strings in edited timing paths. One existing `builder.ToString()` remains in a cold ghost-service report path because the method contract returns a report string.
Rejected Alternatives: rewriting unrelated diagnostics or unrelated `math.normalizesafe` camera-forward code in `GlobalPhysicsStateManager`.
Scalability potential: Diagnostics stay cold; hot dispatcher code remains flat array traversal.
Hardware Impact: No runtime hot-path cost added by the polish changes.

Final Git Diff: `Assets/_Project/Scripts/Core/FrameTimeWatchdog.cs`, `Assets/_Project/Scripts/Core/GlobalRegistry.cs`, `Assets/_Project/Scripts/Core/GlobalSignals.cs`, `Assets/_Project/Scripts/Core/RuntimeWatchdog.cs`, `Assets/_Project/Scripts/Core/SceneRuntimeService.cs`, `Assets/_Project/Scripts/Core/SystemDispatcher.cs`, `Assets/_Project/Scripts/CrashTelemetryBuffer.cs`, `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs`, `Assets/_Project/Scripts/ITickable.cs`, `Assets/_Project/Scripts/SpatialAudioManager.cs`, `Assets/_Project/Scripts/UI/PauseMenuController.cs`, `Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs`, `Assets/_Project/Scripts/Visor/SuitHUDPresentationController.cs`, plus new `Assets/_Project/Scripts/Core/Time/Hecton8.Core.Time.asmdef` and this agent's status/rationale files. Stat for tracked files: 13 source files changed, 3998 insertions, 244 deletions. The new asmdef/status/rationale/log files are untracked additions.

## 2026-05-13 STATIC RECHECK ADDENDUM

Problem: AUP origin-shift interpolation still sampled Unity fixed-time globals after the custom dispatcher fixed accumulator became authoritative.
Solution: `SystemDispatcher` now publishes `CurrentFixedInterpolationAlpha` from its fixed-step accumulator remainder, and `HectonFloatingOrigin` reads that dispatcher-owned value.
Rejected Alternatives: Keeping `Time.fixedDeltaTime`, mirroring Unity fixed time, or exposing the accumulator itself. Unity fixed time is no longer the source of truth; exposing the accumulator would leak scheduler internals.
Scalability potential: Low/Middle/High/Ultra all share one cheap scalar; high-end visual interpolation can consume it without making AUP math heavier.
Hardware Impact: i3/MX350 estimate is sub-0.1 us; the real gain is deterministic rebase presentation under dilation/pause.

Problem: Camera hit-stop still cached a concrete dispatcher type, and pause smoke tools/audio still had time-scale-era pause semantics.
Solution: Added `ITickDispatcher.RequestCoreTickDilation`; camera juice uses the interface, and pause audio/verifiers resolve pause through `ITickDispatcher.SimulationPaused` with `GlobalSignals` fallback.
Rejected Alternatives: Concrete `SystemDispatcher` fields, Unity `Time.timeScale` checks, or duplicated pause flags. They create brittle ordering and cross-domain coupling.
Scalability potential: Low tier keeps scalar-only pause/hit-stop; Middle/High/Ultra can layer presentation on the same signal without changing callers.
Hardware Impact: i3/MX350 estimate is 1-3 us saved during pause/hit-stop verification paths by avoiding redundant global-time checks and concrete dependency recovery.

Problem: Dispatcher frost/AUP paths were still resolving DataVault through the registry at maintenance points.
Solution: Cache `IDataVault` on service init and refresh it only when AUP pause requests arrive, then use the cached reference for lock/unlock/frost defrag.
Rejected Alternatives: Per-frost registry lookups or direct concrete DataVault dependencies. Registry lookups are avoidable; concrete ownership violates concurrent-agent boundaries.
Scalability potential: Low tier reduces maintenance overhead; High/Ultra can spend the saved core budget on visual feedback while memory maintenance remains stable.
Hardware Impact: i3/MX350 estimate is 0.2-0.5 us avoided per frost/AUP maintenance touch, static estimate only.

Problem: Post-polish verification had to continue without launching `dotnet build` per user instruction.
Solution: Ran static-only audits: targeted `rg` for forbidden Unity time/task patterns, targeted `git diff --check`, and code rereads around dispatcher, pause, AUP, and world-scatter diagnostics.
Rejected Alternatives: Running `dotnet build`, modifying generated project references, or reverting other agents' assembly work. User explicitly prohibited build; dependency wall remains external.
Scalability potential: Verification scope stayed on timing authority and coupling, not unrelated domain refactors.
Hardware Impact: Runtime impact is none; verification confidence is static-only until global compile dependencies are fixed.

Problem: A frame-count hit-stop burst could survive into a menu pause and continue supplying nonzero dilated delta after pause signals were drained.
Solution: Pause now captures the non-burst restore scalar, clears burst state, and makes paused/frozen state outrank `RequestCoreTickDilation`. External scalar requests also clear frame-count bursts.
Rejected Alternatives: Letting burst countdown continue during pause, relying on zero `deltaTime` consumers to behave, or letting stale restore state apply next frame. Those leave pause semantics dependent on unrelated hit-stop order.
Scalability potential: Low tier gets strict scalar freeze; Middle/High/Ultra can still layer hit-stop visuals, but pause remains authoritative.
Hardware Impact: i3/MX350 estimate is sub-0.2 us; the gain is correctness and removing stale burst recovery checks after pause.

Problem: Physics hit-stop duration, legacy tick bootstrap fallback, and frame watchdog sampling still took separate Unity unscaled delta samples.
Solution: Moved those paths to dispatcher-owned unscaled delta: `GlobalPhysicsStateManager.ResolveDispatcherUnscaledDeltaTime`, `GameTickManager` bootstrap fallback, and `FrameTimeWatchdog.Tick`.
Rejected Alternatives: Keeping independent Unity reads or adding another clock service. Independent reads split timing authority; another service duplicates dispatcher state.
Scalability potential: Low/Middle/High/Ultra use one unscaled clock for frame budgeting and hit-stop duration while visual cost remains LOD-controlled.
Hardware Impact: i3/MX350 estimate is 0.3-0.8 us saved across watchdog/hit-stop/fallback sampling paths; static estimate only.

Problem: Runtime visual load-shed and scene transition code still sampled Unity unscaled delta directly.
Solution: Surface weather screen-space rain shedding and scene runtime cinematic transition progression now use `SystemDispatcher.CurrentFrameUnscaledDeltaTime` with a fixed fallback for async transition loops.
Rejected Alternatives: Leaving visuals on independent Unity clocks or coupling them to simulation delta. Independent clocks drift from core frame budgeting; simulation delta would freeze menus/transitions.
Scalability potential: Low tier can shed rain using the same measured frame delta; High/Ultra can spend extra visual budget without diverging from core frame pacing.
Hardware Impact: i3/MX350 estimate is 0.1-0.4 us by avoiding duplicate Unity time reads and keeping load-shed decisions aligned with the dispatcher.
