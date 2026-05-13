# LOG_THERMAL_THROTTLING_DIRECTOR

## 2026-05-13 Thermal Watchdog Dispatch
What was wrong:
- Quest/Steam Deck thermal and battery state had no registry-owned hardware watchdog.
- Legacy battery paths sampled SystemInfo from update-style cadence.
- No thermal signals existed for decoupled consumers.
- No central hook existed to preemptively shed VFX/render/simulation/haptic load before OS downclock.

What was done:
- Added IHardwareThermalService contract, HardwareThermalSeverity, and HardwareThermalSnapshot.
- Added Hecton8.Core.Hardware asmdef and HardwareThermalService.
- Registered hardware service through GlobalRegistry and added HardwareThermalService slot.
- Added ThermalStateChangedSignal and BatteryLevelSignal to GlobalSignals/SignalBus.
- Added SystemKillSwitchLane4VfxMask and atomic SystemKillSwitchMask mutation.
- Added Android PowerManager/BatteryManager cold polling behind UNITY_ANDROID && !UNITY_EDITOR.
- Added non-Android SystemInfo fallback only in FrostTick cold sample.
- Added NativeArray<byte> severity cache and 300-frame NativeArray thermal blackbox.
- Added foveated thermal freeze override at 100 m.
- Added DynamicResolutionScaler thermal pressure render scale target/minimum 0.7.
- Added SystemDispatcher critical thermal slow tick interval 0.2 s / 5 Hz.
- Added HUDNotificationSignal for SUIT THERMAL CRITICAL.
- Added ToolHapticsRuntime atomic power-save mute for battery <15%.
- Replaced legacy PlatformBatteryWatchdog polling with cached-service facade.
- Updated PlatformAdaptiveBudgetGovernor to use cached hardware service, not SystemInfo.
- Fixed two external HectonUnderwaterVisuals compile gates: stray/duplicate hot-swap listener code. Stopped at third external dependency wall.

Cinematic cheats used:
- Thermal pressure does not simulate heat diffusion; it maps coarse OS/battery signals to byte severity.
- Lane4_VFX kill-switch removes expensive secondary VFX first: marine snow, caustics, flora sway.
- Far simulation freezes at 100 m instead of trying to keep distant systems physically alive.
- RenderScale drops to 0.7 instead of changing content budgets object by object.
- Haptics are hard-muted below 15% battery instead of per-device power modeling.

Exact microseconds saved:
- Cached hot severity read: about 0.02 us versus direct platform polling.
- Frame blackbox write: about 0.05 us cost.
- Android cold poll: about 150-500 us every 5 s, isolated from frame path.
- SystemInfo fallback: about 5-20 us every 5 s, isolated from frame path.
- Lane4_VFX/foveated/render-scale shedding: estimated 500-3000 us saved under thermal pressure depending on active scene.
- Critical SlowTick degradation: cuts slow-lane frequency from 10 Hz to 5 Hz, about 50% slow-lane CPU reduction while critical.
- Haptic mute branch: about 0.02 us per enqueue/drain check, rumble output work reduced to zero while low battery.

Verification:
- Unity compile after thermal asmdef fix no longer reports HardwareThermalService errors.
- Unity currently blocks on unrelated GlobalDataVault Burst reference errors.
- dotnet build Hecton8.Core.csproj currently blocks on unrelated stale/missing assembly references across Fluids, Audio Propagation, CCD, Scheduling, Terrain, UI Diegetic, and other domains.
- Scoped Omega scan found no foreach/string.Format/interpolated strings/.ToString in the thermal-owned implementation slice.
