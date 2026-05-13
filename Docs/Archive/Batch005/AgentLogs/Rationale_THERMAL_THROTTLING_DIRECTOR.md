# Rationale_THERMAL_THROTTLING_DIRECTOR

Status: PENDING VERIFICATION

## Decision 0: Domain Boundary and Mandate Selection
Problem: Thermal throttling must cross hardware, rendering, foveated simulation, haptics, telemetry, and UI boundaries without concrete cross-domain dependencies.
Solution: Use GlobalRegistry-owned interfaces for immediate commands and typed signal structs for broadcast state changes. Thermal polling cadence is FrostTick only, with hot paths reading cached bytes.
Rejected Alternatives: A classic BatteryManager singleton and per-frame SystemInfo/Android polling were rejected because they violate registry and zero-GC mandates.
Scalability potential: Low uses render scale and static distant simulation; Middle restores VFX gradually; High keeps richer foveated thresholds; Ultra can trade saved cycles into denser visible VFX while keeping thermal rollback.
Hardware Impact: i3/MX350 and Quest avoid OS-level downclock spikes by preemptively shedding VFX/render scale/tick cadence. Estimated saved cost is workload-dependent; initial target is 500-3000 us during thermal pressure.

## Decision 1: Registry Service and Assembly Boundary
Problem: Hardware polling must be globally visible without reintroducing BatteryManager.Instance or hard dependencies from VFX/UI/haptics into platform code.
Solution: Added IHardwareThermalService in the contracts assembly and a separate Hecton8.Core.Hardware asmdef. GlobalRegistry owns only the interface slot and the SystemKillSwitchMask bit.
Rejected Alternatives: A static BatteryManager singleton or embedding Android code in core runtime classes would spread platform dependencies and violate registry ownership.
Scalability potential: Low uses the interface to shed costly systems; Middle/High/Ultra can consume the same cached severity and spend recovered headroom on richer visible effects after recovery.
Hardware Impact: Cached service read cost is about 0.05 us. Avoids repeated service discovery and blocks Java bridge churn on Quest/i3/MX350 class devices.

## Decision 2: FrostTick Polling and Severity Hysteresis
Problem: Android Java and SystemInfo can allocate or stall if polled from render/update paths.
Solution: HardwareThermalService polls Android BatteryManager/PowerManager only under UNITY_ANDROID && !UNITY_EDITOR from FrostTick, with non-Android SystemInfo fallback in the same cold sample. Severity is cached in NativeArray<byte> with two-sample recovery hysteresis.
Rejected Alternatives: Per-frame temperature checks, direct SystemInfo reads inside PlatformAdaptiveBudgetGovernor, and raw threshold toggles without hysteresis.
Scalability potential: Low device enters throttling early and holds it stable; Ultra can recover without visual flicker after two FrostTick samples.
Hardware Impact: Cold poll estimated 150-500 us every 5 seconds on Android, 5-20 us on fallback platforms. Hot-path cost is a cached byte read/write only.

## Decision 3: Load Shedding as Cinematic Cheat
Problem: Prevent OS thermal downclock before frame time spikes while preserving the illusion of an active world.
Solution: Severity 2 applies Lane4_VFX kill switch, 100 m foveated freeze distance, and 0.7 render scale. Severity 3 also halves SlowTick from 10 Hz to 5 Hz.
Rejected Alternatives: Simulating real thermal physics, disabling whole subsystems by direct references, or using Time.timeScale for critical thermal pressure.
Scalability potential: Low freezes far simulation and removes secondary VFX; Middle keeps near-field motion; High/Ultra recover into full VFX after hysteresis.
Hardware Impact: Estimated 500-3000 us saved depending on active VFX/simulation load, plus GPU pixel cost drop from render scale 1.0 to 0.7.

## Decision 4: Haptic Power Save
Problem: Low battery needs to stop rumble without requiring every haptic producer to learn battery policy.
Solution: ToolHapticsRuntime owns an atomic power-save mute, clears buffers on activation, and early-outs enqueue/drain/snapshot paths.
Rejected Alternatives: Scaling each HapticRequest producer or writing a direct HAPTICS_DIRECTOR dependency into the thermal service.
Scalability potential: Low devices save battery immediately; high-end/charged devices run unchanged.
Hardware Impact: Hot-path branch estimated 0.02 us; low-battery rumble work drops to zero.

## Decision 5: Blackbox and Telemetry
Problem: Thermal actions must be diagnosable after a crash or NaN without managed per-frame logs.
Solution: HardwareThermalService writes severity, battery percent, thermal status, action mask, and frame into a fixed 300-frame NativeArray ring. Critical severity dumps Docs/AgentLogs/Dump_THERMAL_THROTTLING_DIRECTOR.bin and publishes telemetry hashes.
Rejected Alternatives: List<T>, string logging, or depending solely on chat/build output.
Scalability potential: Same telemetry layout works across Low to Ultra; high-end builds can consume the action mask for richer diagnostics without changing the hot path.
Hardware Impact: About 0.05 us per frame for native ring write; dump cost is cold IO only on critical.

## Decision 6: External Compile Wall Handling
Problem: Verification was blocked first by unrelated HectonUnderwaterVisuals syntax/duplicate hot-swap listener errors, then by unrelated UI Diegetic and GlobalDataVault Burst reference errors.
Solution: Fixed the two HectonUnderwaterVisuals errors because they were direct compile gates and caused by duplicated hot-swap interface code. Stopped after the third unrelated wall per 3-strikes protocol.
Rejected Alternatives: Editing UI/Memory/Audio/Fluids/Scheduling domains broadly to force a green build; that would exceed the thermal domain and risk sabotaging other agents.
Scalability potential: Thermal code remains decoupled; integrator can repair foreign asmdef/reference walls independently.
Hardware Impact: No runtime impact. Verification risk remains external until those domains compile.

## OMEGA POLISH CHANGES
Problem: Anti-bloat audit required proof that the thermal implementation did not add honest physics, managed iteration, string formatting, or avoidable floating divisions.
Solution: Replaced haptic normalized power division with `math.rcp`; Android battery scale conversion already uses `math.rcp`; thermal policy uses byte/bitmask severity and cached action masks instead of per-frame calculations. Scoped scans found no `foreach`, `string.Format`, interpolated strings, or `.ToString()` in HardwareThermalService, PlatformAdaptiveBudgetGovernor, PlatformBatteryWatchdog, ToolHapticsRuntime, or CoreContractsAssemblyMarker.
Rejected Alternatives: More realistic thermal modeling, per-device curve fitting, direct per-system VFX shutdowns, or managed diagnostic strings. Those would spend frame time to describe a problem instead of buying visual stability.
Scalability potential: Low = VFX lane killed, 0.7 render scale, 100 m freeze, 5 Hz critical slow tick, haptics muted below 15% battery. Middle = same signals with faster recovery after hysteresis. High = default visuals restored after two cool FrostTicks. Ultra = saved budget can be reinvested by consumers once severity returns below throttling.
Hardware Impact: New per-frame work is a single NativeArray blackbox write and cached atomic checks, estimated 0.05 us/frame. FrostTick polling cost is cold. Thermal pressure savings remain 500-3000 us plus GPU pixel reduction. Compile status remains PENDING because Unity now reports unrelated `GlobalDataVault` Burst reference errors after the thermal asmdef error was fixed.
