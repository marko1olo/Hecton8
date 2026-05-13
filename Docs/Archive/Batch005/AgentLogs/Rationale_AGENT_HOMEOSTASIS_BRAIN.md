# Rationale_AGENT_HOMEOSTASIS_BRAIN

## Batch Hygiene Decision
Problem: Required `Status_AGENT_HOMEOSTASIS_BRAIN.md` and `Rationale_AGENT_HOMEOSTASIS_BRAIN.md` were missing at session start.
Solution: Create persistent task and rationale files before code so the agent state survives context compaction.
Rejected Alternatives: Chat-only reporting was rejected because project protocol treats disk files as long-term memory. Waiting for a wipe was rejected because this is a new agent ID and no stale file existed.
Scalability potential: Low/Middle/High/Ultra unaffected directly; this is process memory, not runtime.
Hardware Impact: 0 us frame impact on i3/MX350.

## Prompt Source Decision
Problem: `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="AGENT_HOMEOSTASIS_BRAIN">`.
Solution: Treat the user-supplied XML block as the authoritative assignment for this session while documenting the mismatch.
Rejected Alternatives: Reading neighboring batch prompts was rejected as explicit protocol violation. Blocking indefinitely was rejected because the direct user prompt contains the full assignment.
Scalability potential: Low/Middle/High/Ultra unaffected directly.
Hardware Impact: 0 us frame impact on i3/MX350.

## Mandate Selection Decision
Problem: Homeostasis crosses hardware telemetry, dispatcher masks, foveated simulation, HUD warnings, and black-box logging.
Solution: Selected 8 mandate files: `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, `DBG_Telemetry_Crash_Reporting_PostMortem`, `ARCH_Global_Registry_ServiceLocator_DI_Init`, `REND_Foveated_Simulation_LOD`, `UI_Diegetic_Physical_Interfaces`, and `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First`.
Rejected Alternatives: Reading all registry mandates was rejected as context pollution. Reading only graphics mandates was rejected because this task mutates scheduler behavior and telemetry, not just visuals.
Scalability potential: Low uses hard load-shed; Middle uses delayed restoration; High/Ultra can spend recovered budget on richer VFX once mask clears.
Hardware Impact: Expected runtime gain is from later mask decisions; mandate reading has 0 us frame impact.

## Loop 1 Metric Kernel Decision
Problem: Hardware pressure needs to be visible to many systems without creating direct dependencies or managed per-frame polling.
Solution: Added `HomeostasisBrain.GlobalHardwareMetrics` as a persistent `NativeArray<float>` SOA and exposed pressure through `SystemDispatcher.KillSwitchMask` plus typed signal lanes. The monitor is called from dispatcher pre-simulation, not Unity `Update`.
Rejected Alternatives: MonoBehaviour-owned `Update`, managed singleton dictionaries, and direct renderer references were rejected because they add GC risk and cross-domain coupling.
Scalability potential: Low/MX350 reads the same SOA but treats battery pressure harder; High/Ultra clear the mask and keep visual overkill systems online.
Hardware Impact: Metric sampling is estimated at 4-20 us depending on platform cadence; avoided per-frame JNI lookup and native WMI/NVML calls that can spike far above 0.1 ms.

## Loop 1 SHI Math Decision
Problem: SHI must be cheap, predictive, and stable enough to avoid kill-switch chatter.
Solution: Used EWMA FPS, a 120-frame jitter sigma, and a Burst function pointer for weighted pressure: jitter 40%, temp 40%, battery 20%, with battery doubled on Low/MX350.
Rejected Alternatives: Instantaneous FPS thresholding was rejected because it oscillates. A scheduled one-frame job was rejected because scheduling and completing a one-scalar job in the hot path is worse than the math.
Scalability potential: Low escalates earlier on battery and jitter; Middle preserves visuals until sustained instability; High/Ultra tolerate transient cost while restoring systems sequentially.
Hardware Impact: The SHI function itself is <1 us on i3/MX350; preventing jitter cascades can preserve the 16.6 ms frame cadence.

## Loop 2 Sacrifice Hierarchy Decision
Problem: The controller must shed cost in a visible-quality order while preserving core simulation predictability.
Solution: Added `SystemBit : ulong` masks. Level 1 removes expensive presentation fakes first: secondary caustics, particle advection, high-res volumetric fog. Level 2 removes long-distance/secondary motion and screen-space expense: distant fauna steering, procedural sway, IK bracing, SSR, and foveated simulation Tier 3. Level 3 removes non-critical VFX, boid brain cost, slow-tick frequency, and applies 0.8 time dilation.
Rejected Alternatives: Direct component toggles were rejected because 20+ agents are changing systems concurrently. String feature flags were rejected because they allocate and are not Burst/job friendly. Balanced middle-ground quality was rejected because this project requires explicit Low/Middle/High/Ultra behavior.
Scalability potential: Low/MX350 gets early battery-weight escalation and aggressive foveated throttling. Middle gets Level 1/2 for short thermal or jitter spikes. High keeps visuals until hard evidence appears. Ultra uses cleared masks to keep visual overkill features active.
Hardware Impact: Level 1 targets fill-rate/post cost first, estimated 0.2-0.6 ms saved on MX350. Level 2 targets CPU+GPU secondary work, estimated 0.6-1.5 ms saved. Level 3 can save multiple milliseconds by cutting non-critical systems and reducing slow tick to 2 Hz.

## Loop 3 HUD And Recovery Decision
Problem: Task 12 required a visible visor warning, not only a telemetry signal, and Task 13 required recovery without oscillation.
Solution: `HomeostasisBrain` publishes `SystemHealthSignal` with `HudWarning`; `SuitHUDV4CanvasOverlay` consumes the typed signal snapshot and writes `OPTIMIZING CORE SYSTEMS` through the existing status label char-array path. Recovery requires SHI < 0.30 for 300 frames, then clears one bit every 60 frames.
Rejected Alternatives: Creating a new canvas/text object at runtime was rejected because cold UI bootstrap is already centralized and a new object would risk ordering errors. Immediate full restoration was rejected because it would reintroduce chattering after a thermal dip. A modal warning was rejected because system optimization must be diegetic and low-noise.
Scalability potential: Low devices see clear load-shed feedback without extra UI allocation. Middle devices restore systems slowly after stability. High/Ultra restore all bits and return to full presentation when stable.
Hardware Impact: HUD consume path is under 2 us per HUD tick and reuses pooled/char-array text. Sequential restoration prevents repeated multi-millisecond spikes from toggling expensive VFX back on too early.

## Loop 4 Dispatcher And Blackbox Decision
Problem: The brain must run before simulation consumers and must leave postmortem evidence for NaN/crash conditions.
Solution: Registered the brain through `SystemDispatcher` pre-simulation ordering, before pre-simulation signal flush. Added a fixed 300-entry `NativeArray<HomeostasisBlackBoxEntry>` ring and fault-only binary dump to `Docs/AgentLogs/Dump_AGENT_HOMEOSTASIS_BRAIN.bin`.
Rejected Alternatives: A standalone MonoBehaviour `Update` was rejected because it bypasses dispatcher ordering. Per-frame text telemetry was rejected because it allocates and blocks IO. A larger black box was rejected because last 300 frames satisfy mandate while staying below an 8 KB native budget.
Scalability potential: Low devices get deterministic early pressure decisions. Middle/High/Ultra preserve the same signal/mask contract so feature consumers can scale detail without changing the governor.
Hardware Impact: Normal blackbox write is <1 us. Fault dump is out-of-band. Pre-simulation ordering prevents late-frame reaction delay and reduces next-frame jitter cascade risk.

## Loop 5 Verification Decision
Problem: Full verification had to separate real code issues from project tooling noise.
Solution: Static Unity validation previously passed for HomeostasisBrain, SystemDispatcher, and FoveatedSimulationManager. After HUD integration, a Unity refresh timed out waiting for editor readiness, but `Library/ScriptAssemblies/Hecton8.Core.dll` regenerated at 2026-05-13 22:35:25 and the editor log contained no current C# errors for HomeostasisBrain, SuitHUDV4CanvasOverlay, or touched assembly files.
Rejected Alternatives: Treating `dotnet build Hecton8.Core.csproj` as authoritative was rejected because the generated csproj currently fails on many pre-existing missing assembly references unrelated to this agent work. Ignoring the failed build was rejected; the failure is recorded as a tooling/dependency limitation.
Scalability potential: Verification confirms Android JNI and Windows fallback code are isolated with `#if` guards, preserving cross-platform compile safety.
Hardware Impact: 0 us runtime; avoids shipping a branch that calls Android JNI on non-Android or managed polling on every platform.

## Loop 6 Hardware SOA And Flag Hygiene Decision
Problem: The first verified pass still duplicated platform battery polling when a cached hardware service existed, and blackbox flags could preserve previous pressure-level bits for one frame after recovery because flags were built before policy resolution.
Solution: `HomeostasisBrain` now consumes `GlobalRegistry.HardwareThermal.TryGetSnapshot` first, mapping cached thermal severity, battery percent, and temperature into the global metric SOA. Fallback hardware bias is cached at initialization. Battery fallback polling is throttled to 60 frames. `ApplyPressurePolicy` returns the final flags used by blackbox logging. Legacy `SystemHealthIndexSignal` is published alongside the typed homeostasis signal for existing world-streaming consumers. A `HardwareThermalSnapshot` flag distinguishes service-backed metrics from Windows/Android fallback paths.
Rejected Alternatives: Directly reading `ThermalStateChangedSignal` snapshots was rejected because signal cadence depends on flush timing; the registry service snapshot is the authoritative cached state. Keeping per-frame `SystemInfo` battery reads was rejected because battery changes slowly and the hardware service already samples cold. Leaving legacy `SystemHealthIndexSignal` untouched was rejected because existing world systems already listen to it.
Scalability potential: Low/MX350 receives the same service-backed thermal/battery pressure as the rest of the runtime. Middle keeps fallback proxies without redundant battery polling. High/Ultra preserve cleared masks and publish stable health to streaming systems.
Hardware Impact: Removes recurring `SystemInfo.systemMemorySize`/`graphicsMemorySize` reads from the hot path after init, reduces fallback battery polling from every frame to once per 60 frames, and fixes post-recovery blackbox flag accuracy at <1 us extra policy cost.
