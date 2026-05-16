# Status_HARDWARE_THROTTLING_DIRECTOR

## Identity
- Prompt: HARDWARE_THROTTLING_DIRECTOR
- Domain: CORE/HARDWARE
- Task Count: 18
- Active Phase: OMEGA PASS 2 - HOMEOSTASIS PATCH APPLIED, CURRENT VALIDATION BLOCKED BY EXTERNAL COMPILE WALL
- Microsecond Policy: values below are static DOD budget estimates unless marked build/tool measured. No profiler capture was run in this pass.

## Mandates Read
- PROJECT_LTS_Compatibility_Layer.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Execution_Phases.txt
- C:/hades/Hecton8/AGENTS.md
- Docs/Actual Domains of Project.txt

## Phase 1 - The Great Purge
- [x] 1. PURGE_SINGLETONS | DOD: `rg BatteryManager.Instance ThermalMonitor.Instance` is clean in the owned hardware/homeostasis surface; `HardwareThermalService` is registry-gated through `GlobalRegistry.HardwareThermal`. Rejected: private BatteryManager/ThermalMonitor singleton ownership. Estimate: 4 us cold branch removed, 0 us hot path.
- [x] 2. DEBT_CLEANUP | DOD: `Application.targetFrameRate` runtime authority is bootstrap/homeostasis/XR bridge only; no UI writer was found in the owned pass. Rejected: UI-side thermal frame caps. Estimate: 0 us runtime, governance-only.
- [x] 3. DATA_EVICTION | DOD: `HardwareMetrics`, `HardwareFrameTimes`, `HomeostasisBlackBox`, `HardwareThermalSeverity`, and `HardwareThermalBlackBox` resolve through `GlobalDataVault` handles with `SystemID.HardwareHomeostasis`. Rejected: locally owned persistent NativeArrays. Estimate: 3 us cold vault resolve, 0 us per frame after handle cache.

## Phase 2 - Cross-Platform Sensory Kernel
- [x] 4. ANDROID_JNI_BRIDGE | DOD: `UNITY_ANDROID` path caches `AndroidJavaClass`, activity, and `PowerManager`; calls `getThermalHeadroom(30)` only through FrostTick cadence. Rejected: per-frame JNI polling. Estimate: 40 us/frame avoided versus illegal per-frame JNI; 0 us/frame in compliant hot path.
- [x] 5. STEAM_DECK_SENSORS | DOD: standalone fallback samples `SystemInfo.batteryLevel` and `SystemInfo.batteryStatus` through the cold poll countdown; no disk reads or MicroSD-sensitive telemetry files added. Rejected: Linux sensor file polling from frame code. Estimate: 12 us/frame avoided versus per-frame SystemInfo polling assumption; 0 us/frame in hot path.
- [x] 6. SHI_CALCULATION | DOD: Burst function pointer computes `TempError*0.5 + BatteryPressure*0.3 + FrameJitter*0.2` and clamps finite output. Rejected: managed LINQ/object policy calculators. Estimate: 1 us/frame SHI cost budget, 25 us/frame avoided versus managed policy fanout.
- [x] 7. EWMA_SMOOTHING | DOD: `ShiEwmaAlpha = 0.12f` stabilizes sacrifice changes and prevents flicker between thresholds. Rejected: raw threshold toggling. Estimate: 1 us/frame cost, prevents restore/throttle oscillation with no GC.

## Phase 3 - Hierarchy of Sacrifice
- [x] 8. LEVEL_1_WARNING | DOD: SHI > 0.6 applies `SecondaryCaustics | MicroDebrisAdvection` through `SystemKillSwitchMask`. Rejected: visual quality reduction by global low-quality preset. Estimate: 350 us GPU/VFX budget recovered on low tier.
- [x] 9. LEVEL_2_THROTTLING | DOD: SHI > 0.8 disables only `ProceduralSway` and `HighQualityIK` by mask while DRS caps non-low tiers to 0.75; profile JSON and generated catalog now use Level 2 mask `0x0000000000000330`. Rejected: early SSR/volumetric/foveated cuts that give high-end PCs mobile visuals. Estimate: 1600 us GPU budget recovered by DRS cap, 220 us CPU animation budget recovered, with high-end visual overkill preserved until Level 3.
- [x] 10. LEVEL_3_CRITICAL | DOD: SHI > 0.95 sets `AiOneHz` and `TimeDilation09`; `SystemDispatcher.ApplyHomeostasisKillSwitch` requests 1 Hz AI/slow tick and 0.9 dilation. Rejected: sudden OS-driven stutter with no simulation pacing. Estimate: 1800 us CPU budget recovered under emergency pressure.
- [x] 11. POWER_RECOVERY | DOD: SHI < 0.3 for 3000 frames arms sequential restoration, then restores one active bit every 60 frames and skips absent emergency bits. Rejected: instant full-quality snapback and wasted restore ticks on bits that were never disabled. Estimate: 1 us/frame state machine cost, prevents repeated 350-3970 us quality churn.

## Phase 4 - Stability, Telemetry, Blackbox
- [x] 12. NAN_VACCINATION | DOD: SHI, metrics, battery data, time dilation, and scale paths use finite clamps/guards before publishing or blackbox writes. Rejected: trusting invalid `SystemInfo` or divide results. Estimate: 2 us/frame guard budget; prevents unbounded GPU/CPU fault propagation.
- [x] 13. BLACKBOX_LOGGING | DOD: `HomeostasisBlackBoxEntry[300]` in DataVault records `PeakSystemHealthIndex01` and `LastThermalAction`; NaN/fault dump path is `Docs/AgentLogs/Dump_HARDWARE_THROTTLING_DIRECTOR.bin`. Rejected: managed per-frame logs. Estimate: 1 us/frame fixed ring write, 0 GC.
- [x] 14. TRIPLE_STRIKE_REPAIR | DOD: Android bridge fault path falls back to `SystemInfo.processorFrequency` pressure bias. Rejected: failed JNI equals blind thermal policy. Estimate: 1 us cold fallback sample, 0 us/frame hot path.
- [x] 15. HOMEOSTASIS_SIGNAL | DOD: `SystemHealthSignal(Level)` and related health/index/kill-switch lanes publish through `SignalBus<T>.Push`; hardware/DRS legacy `GlobalSignals.Publish` calls were removed from the owned paths. Rejected: legacy EventBus, managed delegates, duplicate signal invention. Estimate: 6 us/frame and 0.5 KB/frame allocation risk avoided versus managed broadcast assumptions.
- [x] 16. MAC_METAL_THERMALS | DOD: `UNITY_OSX && !UNITY_EDITOR` path uses cached Objective-C selectors for `NSProcessInfo.thermalState`; shader audit found no compute `numthreads` product over 1024 and no Metal exclusion in the scanned shader set. Rejected: DirectX-only thermal/render shortcuts. Estimate: 0 us/frame hot path, 5-second cold sample only.
- [x] 17. VR_REFRESH_SYNC | DOD: XR active plus Level 2+ pressure requests 72 Hz through `HectonXRRuntimeState.TryRequestDisplayRefreshRateHz(72f)` and caps `Application.targetFrameRate` when runtime API is unavailable. Rejected: forcing Quest to hold 90 Hz while thermally constrained. Estimate: 0 us CPU hot path, GPU power reduction only.
- [ ] 18. FINAL_VALIDATION | [BLOCKED BY DEPENDENCY] Phase4 validation previously exited 0, but Omega Pass 2 rebuild now fails on external `GameBootstrapper.cs` and `ToolDurabilitySystem.cs` errors not introduced by the hardware mask patch. Rejected: claiming current green build with unrelated compile wall active. Current log: `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass2.txt`.

## Iterative Loops
- [x] Loop 1: prompt extracted, mandates/domain read, singleton/data ownership purge verified.
- [x] Loop 2: Phase 2 sensory kernel verified against Android, standalone/Steam Deck, Burst SHI, and EWMA requirements.
- [x] Loop 3: Phase 3 sacrifice masks, DRS 0.75 cap, AI 1 Hz, time dilation, and 3000-frame restoration verified.
- [x] Loop 4: Phase 4 NaN, blackbox, fallback, signal, Mac, and XR refresh paths verified.
- [x] Loop 5: Omega polish performed: JNI cache check, bitwise mask check, typed signal purge, Pack=1 interface patch, shader thread-group audit, final build.

## Static Audit Evidence
- `rg 'GlobalSignals\.Publish|EventBus|Action<|Func<|event\s|new NativeArray|H8Memory\.Allocate|Update\(|LateUpdate\(|FixedUpdate\(|string\.Format|\.ToString\(' Assets/_Project/Scripts/Core/Hardware Assets/_Project/Scripts/Core/HomeostasisBrain.cs Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs` returned no owned violations.
- `rg '\$"' Assets/_Project/Scripts/Core/Hardware Assets/_Project/Scripts/Core/HomeostasisBrain.cs Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs` returned no interpolated strings.
- Hardware/homeostasis structs and the hardware-adjacent signal lanes `ResolutionChangedSignal`, `SystemHealthIndexSignal`, `HUDNotificationSignal`, `ThermalStateChangedSignal`, and `BatteryLevelSignal` now declare `Pack = 1`.
- Shader audit: no scanned compute shader has `numthreads(x,y,z)` product over 1024. `#pragma require compute` exists, but no `only_renderers d3d` or `exclude_renderers metal` hit was found.
- Hardware profile mask audit: no stale `0x0000000000000070` or `0x00000000002007F0` remains in the hardware profile data/catalog rows after Level 1/2 tightening.

## Compile State
- [x] HISTORICAL PHASE4 BUILD GREEN: `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_Phase4_Strike3.txt`.
- Historical output: `Build succeeded. 0 Warning(s). 0 Error(s).`
- [BLOCKED BY DEPENDENCY] CURRENT OMEGA PASS 2 BUILD: `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass2.txt`.
- Current blockers: `HectonUnderwaterVisuals.cs` missing `_biomeFog*` fields, `GameBootstrapper.cs(2761,34)` invalid `Initialize` overload, and `ToolDurabilitySystem.cs` missing native-state resolver/field members. No current error is in the hardware/homeostasis mask patch.

## Phase Ownership Record
- Phase: PRE_SIMULATION for SHI policy, FrostTick for hardware API sampling, POST_SIMULATION for blackbox/signal writes.
- Owner Assembly: Hecton8.Core plus Core/Hardware service boundary.
- DataVault Buffers Written: `BufferID.HardwareMetrics`, `BufferID.HardwareFrameTimes`, `BufferID.HomeostasisBlackBox`, `BufferID.HardwareThermalSeverity`, `BufferID.HardwareThermalBlackBox`.
- Signal Lanes Published: `SystemHealthSignal`, `FrameTimeSignal`, `KillSwitchSignal`, `SystemHealthIndexSignal`, `HUDNotificationSignal`, `ResolutionChangedSignal`, `BatteryLevelSignal`, `ThermalStateChangedSignal`.
- Status: HOMEOSTASIS PATCH ACTIVE; CURRENT GREEN BUILD BLOCKED BY EXTERNAL NON-HARDWARE COMPILE WALL.
