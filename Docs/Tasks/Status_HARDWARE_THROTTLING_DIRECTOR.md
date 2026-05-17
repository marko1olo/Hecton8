# Status_HARDWARE_THROTTLING_DIRECTOR

## Identity
- Prompt: HARDWARE_THROTTLING_DIRECTOR
- Domain: CORE/HARDWARE
- Task Count: 18
- Active Phase: OMEGA PASS 11 - TRANSIENT SCALABILITY LEASES ACTIVE, CURRENT BUILD BLOCKED BY EXTERNAL FAUNA WALL
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
- [x] 13. BLACKBOX_LOGGING | DOD: `HomeostasisBlackBoxEntry[300]` in DataVault records `PeakSystemHealthIndex01` and `LastThermalAction`; NaN/fault dump path is `Docs/AgentLogs/Dump_HARDWARE_THROTTLING_DIRECTOR.bin`; thermal cold-service emergency dump path is `Docs/AgentLogs/Dump_HARDWARE_THROTTLING_DIRECTOR_ThermalService.bin`; both owned dump writers now use fixed little-endian `Span<byte>` serialization instead of managed binary writer objects. Rejected: managed per-frame logs, stale agent-ID dump names, and managed binary formatter objects in fault-path blackbox dumps. Estimate: 1 us/frame fixed ring write, 0 GC; emergency dump allocation risk reduced, not frame-time measured.
- [x] 14. TRIPLE_STRIKE_REPAIR | DOD: Android bridge fault path falls back to `SystemInfo.processorFrequency` pressure bias. Rejected: failed JNI equals blind thermal policy. Estimate: 1 us cold fallback sample, 0 us/frame hot path.
- [x] 15. HOMEOSTASIS_SIGNAL | DOD: `SystemHealthSignal(Level)` and related health/index/kill-switch lanes publish through `SignalBus<T>.Push`; hardware/DRS legacy `GlobalSignals.Publish` calls were removed from the owned paths. Rejected: legacy EventBus, managed delegates, duplicate signal invention. Estimate: 6 us/frame and 0.5 KB/frame allocation risk avoided versus managed broadcast assumptions.
- [x] 16. MAC_METAL_THERMALS | DOD: `UNITY_OSX && !UNITY_EDITOR` path uses cached Objective-C selectors for `NSProcessInfo.thermalState`; shader audit found no compute `numthreads` product over 1024 and no Metal exclusion in the scanned shader set. Rejected: DirectX-only thermal/render shortcuts. Estimate: 0 us/frame hot path, 5-second cold sample only.
- [x] 17. VR_REFRESH_SYNC | DOD: XR active plus Level 2+ pressure requests 72 Hz through `HectonXRRuntimeState.TryRequestDisplayRefreshRateHz(72f)` and caps `Application.targetFrameRate` when runtime API is unavailable. Rejected: forcing Quest to hold 90 Hz while thermally constrained. Estimate: 0 us CPU hot path, GPU power reduction only.
- [BLOCKED BY DEPENDENCY] 18. FINAL_VALIDATION | DOD: Omega Pass 10 clean-output build exited 0 before Pass 11. Omega Pass 11 clean-output build now fails only on external `Fauna/FaunaBrain.Compatibility.cs` missing `FlagsAttribute`/`Flags` and external `HectonPlayerMovement.cs` duplicate-using warning; no current error names `GlobalRegistry`, `HardwareThermalService`, `PlatformAdaptiveBudgetGovernor`, `PlatformBatteryWatchdog`, `HomeostasisBrain`, or `ThermalDynamicResolutionAdapter`. Rejected: claiming green after the transient lease patch while external compile errors exist. Last verified green log: `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass10_CleanOutDir.txt` at 00:01:54.86. Current blocked log: `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass11_CleanOutDir.txt`.

## Iterative Loops
- [x] Loop 1: prompt extracted, mandates/domain read, singleton/data ownership purge verified.
- [x] Loop 2: Phase 2 sensory kernel verified against Android, standalone/Steam Deck, Burst SHI, and EWMA requirements.
- [x] Loop 3: Phase 3 sacrifice masks, DRS 0.75 cap, AI 1 Hz, time dilation, and 3000-frame restoration verified.
- [x] Loop 4: Phase 4 NaN, blackbox, fallback, signal, Mac, and XR refresh paths verified.
- [x] Loop 5: Omega polish performed: JNI cache check, bitwise mask check, typed signal purge, Pack=1 interface patch, shader thread-group audit, final build.
- [x] Loop 6: Omega pass 3 performed: owned blackbox dump names rechecked; stale thermal-director dump path removed from `HardwareThermalService`.
- [x] Loop 7: Omega pass 4 performed: unnamed padding removed from the 24-byte thermal blackbox entry and 24-byte DRS runtime snapshot contract without changing ABI sizes.
- [x] Loop 8: Omega pass 5 performed: managed binary writer usage removed from owned homeostasis/thermal blackbox dump paths and replaced with fixed little-endian span serialization.
- [x] Loop 9: Omega pass 6 performed: SHI dump order changed to oldest-to-newest ring order and fresh build validation exited 0.
- [x] Loop 10: Omega pass 7 performed: owned surface re-audited for singleton/event/local NativeArray/Update/string-format/managed-writer/Pack debt; shared-output build warning isolated; clean-output build validation exited 0 with 0 warnings.
- [x] Loop 11: Omega pass 8 performed: DRS blackbox raw native-memory dump was replaced with chronological explicit little-endian serialization; hardware profile guard and static owned scans passed; clean-output build is blocked by external determinism compile wall.
- [x] Loop 12: Omega pass 9 performed: Android JNI thermal ownership centralized in `HardwareThermalService`; HomeostasisBrain no longer owns a parallel Android `PowerManager` bridge and consumes the registry snapshot instead.
- [x] Loop 13: Omega pass 10 performed: Android `getThermalHeadroom(30)` polarity was corrected so higher headroom-envelope usage raises thermal pressure instead of inverted false emergencies; clean-output build validation exited 0.
- [x] Loop 14: Omega pass 11 performed: one-way thermal/platform/battery low-tier demotions were replaced with transient low-tier leases in `GlobalRegistry`; current build is blocked by external fauna compatibility errors.

## Static Audit Evidence
- `rg 'GlobalSignals\.Publish|EventBus|Action<|Func<|event\s|new NativeArray|H8Memory\.Allocate|Update\(|LateUpdate\(|FixedUpdate\(|string\.Format|\.ToString\(' Assets/_Project/Scripts/Core/Hardware Assets/_Project/Scripts/Core/HomeostasisBrain.cs Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs` returned no owned violations.
- `rg '\$"' Assets/_Project/Scripts/Core/Hardware Assets/_Project/Scripts/Core/HomeostasisBrain.cs Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs` returned no interpolated strings.
- Hardware/homeostasis structs and the hardware-adjacent signal lanes `ResolutionChangedSignal`, `SystemHealthIndexSignal`, `HUDNotificationSignal`, `ThermalStateChangedSignal`, and `BatteryLevelSignal` now declare `Pack = 1`; the 24-byte thermal blackbox entry and 24-byte DRS runtime snapshot now name all reserved bytes instead of relying on unnamed padding.
- Shader audit: no scanned compute shader has `numthreads(x,y,z)` product over 1024. `#pragma require compute` exists, but no `only_renderers d3d` or `exclude_renderers metal` hit was found.
- Hardware profile mask audit: no stale pre-tightening Level 1/2 mask remains in the hardware profile data/catalog rows after Level 1/2 tightening.
- Blackbox dump audit: no owned legacy thermal-director dump path remains in code; homeostasis and thermal service dumps now carry the `HARDWARE_THROTTLING_DIRECTOR` agent ID.
- Managed writer audit: no managed binary writer class usage remains in `HardwareThermalService`, `HomeostasisBrain`, the hardware contracts, or the thermal DRS adapter.
- Omega pass 7 re-audit: `GlobalSignals.Publish`, `EventBus`, managed delegate lanes, local `new NativeArray`, direct allocator calls, standard Unity `Update` methods, string formatting, managed binary writers, non-`Pack = 1` struct layouts, stale mask constants, stale dump IDs, and singleton manager references returned no owned violations. The only `Application.targetFrameRate` hits are cadence/jitter reads inside `HomeostasisBrain.ResolveTargetFrameRate`, not scattered UI writers.
- Omega pass 8 re-audit: DRS blackbox no longer writes `ReadOnlySpan<byte>` over raw native telemetry memory; SHI, thermal-service, and DRS fault dumps now all use explicit little-endian span serialization. Static scans for raw memory dump writers, managed binary writers, legacy event buses, local `new NativeArray`, direct allocator calls, standard Unity `Update` methods, string formatting, and non-`Pack = 1` layouts returned no owned violations.
- Omega pass 9 re-audit: `getThermalHeadroom(30)` is now owned by `HardwareThermalService` on FrostTick/API 30+ and is combined with `getCurrentThermalStatus`; HomeostasisBrain has no remaining `AndroidJava*`, `TrySampleAndroidThermals`, `EnsureAndroidThermalBridge`, or `DisposeAndroidThermalBridge` code. Snapshot-derived SHI CPU temperature now takes the max of raw battery/thermal temperature and severity-derived synthetic pressure temperature, so severe Android headroom cannot be masked by a cool battery thermistor.
- Omega pass 10 re-audit: `MapThermalHeadroomToStatus` no longer computes `1 - headroom`; Android headroom is treated as non-negative envelope usage where `1.0` maps to severe throttling. Static scans for missing `Pack = 1`, legacy event/delegate lanes, local `new NativeArray`, direct allocator calls, standard Unity `Update` methods, string formatting, managed binary writers, raw `ReadOnlySpan<byte>` dumps, and stale Android bridge ownership returned no owned violations.
- Omega pass 11 re-audit: thermal throttling, platform pressure, and critical battery no longer call persistent `RegisterScalabilityTierOverride`; they use releasable transient low-tier masks. The only remaining production `RegisterScalabilityTierOverride` in the hardware surface is `HardwareTierDetector` boot-time immutable classification; QA harness overrides remain in `Assets/_Project/Scripts/QA/Headless`.

## Compile State
- [x] HISTORICAL PHASE4 BUILD GREEN: `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_Phase4_Strike3.txt`.
- Historical output: `Build succeeded. 0 Warning(s). 0 Error(s).`
- [BLOCKED BY DEPENDENCY] CURRENT OMEGA PASS 2 BUILD: `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass2.txt`.
- Current blockers: `HectonUnderwaterVisuals.cs` missing `_biomeFog*` fields, `GameBootstrapper.cs(2761,34)` invalid `Initialize` overload, and `ToolDurabilitySystem.cs` missing native-state resolver/field members. No current error is in the hardware/homeostasis mask patch.
- [BLOCKED BY DEPENDENCY] CURRENT OMEGA PASS 3 BUILD: `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass3.txt`.
- Current blockers: `PredatorCognitionDomain.cs` missing predator species target/tuning job fields and a `NativeArray<float3>` versus `NativeParallelHashMap<int,float3>` mismatch. No current error is in `HardwareThermalService`, `HomeostasisBrain`, `HardwareProfileCatalog`, `HardwareTierDetector`, `ThermalDynamicResolutionAdapter`, or hardware profile JSON.
- [BLOCKED BY DEPENDENCY] CURRENT OMEGA PASS 4 BUILD: `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass4.txt`.
- Current blockers: `TetherManager.cs(264,58)` references missing `TetherSignals.TetherFireRequest`; `Physics/TetherSignals.cs(167,82)` cannot resolve `TetherFireRequest`. No current error is in `HardwareThermalService`, `HomeostasisBrain`, `CoreContractsAssemblyMarker`, `HardwareProfileCatalog`, `HardwareTierDetector`, `ThermalDynamicResolutionAdapter`, or hardware profile JSON.
- [BLOCKED BY DEPENDENCY] CURRENT OMEGA PASS 5 BUILD: `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass5.txt`.
- Current blockers: `SubmarineFluidDynamics.cs(2051..2094)` syntax errors and missing closing brace. No current error is in `HardwareThermalService`, `HomeostasisBrain`, `CoreContractsAssemblyMarker`, `HardwareProfileCatalog`, `HardwareTierDetector`, `ThermalDynamicResolutionAdapter`, or hardware profile JSON.
- [x] CURRENT OMEGA PASS 6 RETRY BUILD GREEN: `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass6_Retry.txt`.
- Current output: `Build succeeded. 0 Warning(s). 0 Error(s). Time Elapsed 00:02:46.41.`
- [x] CURRENT OMEGA PASS 7 CLEAN OUTDIR BUILD GREEN: `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass7_CleanOutDir.txt`.
- Current output: `Build succeeded. 0 Warning(s). 0 Error(s). Time Elapsed 00:02:03.55.`
- [BLOCKED BY DEPENDENCY] CURRENT OMEGA PASS 8 CLEAN OUTDIR BUILD: `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass8_CleanOutDir_Retry2.txt`.
- Current blockers: `LockstepStateValidator.cs(279,36)` references missing `ValidateBinaryLayout`. Earlier Pass 8 first attempt hit transient/external `H8Memory.cs` duplicate `PhysicsForce*` BufferID errors that disappeared after clean; no Pass 8 compiler error names the hardware/homeostasis/DRS files.
- [BLOCKED BY DEPENDENCY] CURRENT OMEGA PASS 9 CLEAN OUTDIR BUILD: `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass9_CleanOutDir.txt`.
- Current blockers: `World/EcosystemDirector.cs` missing `ClearIndexEntries`, `TryUpsertIndexEntry`, `TryFindIndexEntry`, `ResolveVaultIndexCapacity`, `_sectorIndexByKey`, and `_biomassIndexByKey`. No Pass 9 compiler error names the hardware/homeostasis/DRS files.
- [x] CURRENT OMEGA PASS 10 CLEAN OUTDIR BUILD GREEN: `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass10_CleanOutDir.txt`.
- Current output: `Build succeeded. 0 Warning(s). 0 Error(s). Time Elapsed 00:01:54.86.`
- [BLOCKED BY DEPENDENCY] CURRENT OMEGA PASS 11 CLEAN OUTDIR BUILD: `Docs/AgentLogs/Build_HARDWARE_THROTTLING_DIRECTOR_OmegaPass11_CleanOutDir.txt`.
- Current blockers: `Fauna/FaunaBrain.Compatibility.cs(109,6)` missing `FlagsAttribute`/`Flags`; `HectonPlayerMovement.cs(44,7)` duplicate `System.Runtime.CompilerServices` using warning. No Pass 11 compiler error names the hardware/scalability files touched in this pass.

## Phase Ownership Record
- Phase: PRE_SIMULATION for SHI policy, FrostTick for hardware API sampling, POST_SIMULATION for blackbox/signal writes.
- Owner Assembly: Hecton8.Core plus Core/Hardware service boundary.
- DataVault Buffers Written: `BufferID.HardwareMetrics`, `BufferID.HardwareFrameTimes`, `BufferID.HomeostasisBlackBox`, `BufferID.HardwareThermalSeverity`, `BufferID.HardwareThermalBlackBox`.
- Signal Lanes Published: `SystemHealthSignal`, `FrameTimeSignal`, `KillSwitchSignal`, `SystemHealthIndexSignal`, `HUDNotificationSignal`, `ResolutionChangedSignal`, `BatteryLevelSignal`, `ThermalStateChangedSignal`.
- Status: HOMEOSTASIS PATCH ACTIVE; CURRENT PASS 11 GREEN BUILD BLOCKED BY EXTERNAL FAUNA COMPILE WALL. Last verified green: OMEGA PASS 10.
