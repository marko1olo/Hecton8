# Status - UNIVERSAL_INPUT_ORCHESTRATOR

Status: PENDING VERIFICATION
Domain: Universal Input / UX Input Abstraction
Task count: 15
Last prompt reread: 2026-05-13 after OMEGA polish direct-dispatch reread

## Mandates Loaded

- CTRL_Device_Abstraction_Haptics.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- PROJECT_LTS_Compatibility_Layer.txt

## State Machine

- [x] 1. Singleton Eradication | DOD: `rg` scan found zero `InputManager.Instance`; service path remains `GlobalRegistry.RegisterInputService(this)` / `IInputDeterminismService`. | Rejected: deleting registry-owned `InputManager` because it is the Unity InputAction owner, not a singleton. | Estimate: 0 us runtime.
- [x] 2. Signal Migration | DOD: deterministic path publishes `InputStateSignal` and `PhysicsDeterminismSignals.InputSignal` from frame-cached `PlayerInputState`; no gameplay hardware polling introduced. | Rejected: direct player/submarine dependency and per-device gameplay callbacks. | Estimate: 3 us on i3/MX350.
- [x] 3. ASMDEF Isolation | DOD: added `Hecton8.Input.Universal` asmdef referencing contracts/math only plus `UniversalInputStateSignal` contract payload. | Rejected: pushing XR/OpenXR references into contracts. | Estimate: 0 us runtime.
- [x] 4. Dead Code Hunt | DOD: first-party scan over `Assets/_Project` and the runtime action asset found zero `InputManager.Instance`, `UnityEngine.Input.`, `Input.GetAxis`, `Input.GetButton`, `Input.GetKey`, `Gamepad.current`, `Mouse.current`, or `Keyboard.current`; PDA and dev overlays now route through cached UI `InputAction` data/events. | Rejected: leaving stale design-note false positives, direct mouse/keyboard polling, or editing third-party demo packages from the input domain. | Estimate: 0 us runtime delta.
- [x] 5. Master Action Asset | DOD: `Assets/Resources/HectonRuntimeInputActions.inputactions` parses as JSON and contains schemes `KeyboardMouse`, `Gamepad`, `XR_Touch`; Player has 11 XR bindings, UI has 5 gameplay XR bindings, and dev debug actions now include keyboard, gamepad/Steam Deck, and XR Touch chords. | Rejected: creating a second orphan action asset not referenced by bootstrap scene. | Estimate: 0 us runtime.
- [x] 6. VR Touch Abstraction | DOD: XR `gripPressed` bindings map to `Interact`, and runtime XR grip adds `PlayerInputAction.Interact` into the same action bitmask. | Rejected: Quest-only action enum or separate VR interact channel. | Estimate: 1 us.
- [x] 7. Steam Deck Gyro | DOD: `SteamDeckInputPal` folds gyro into `LookDelta` through EWMA low-pass smoothing and cached device binding. | Rejected: raw gyro delta into replay ring and per-frame gamepad search. | Estimate: 2 us.
- [x] 8. Rumble Translator | DOD: `InputDispatcher.DrainToolHaptics` drains `GlobalSignals.HapticRequest` and blends with `ToolHapticsRuntime` front buffer. | Rejected: haptic strings, managed events, or scene haptics lookup. | Estimate: 4 us when active.
- [x] 9. OpenXR Haptics | DOD: XR scheme routes amplitudes only to cached `XRControllerWithRumble.SendImpulse` with refresh gating; manifest has no `com.unity.xr.openxr`, so direct OpenXR package API is not a compile-safe dependency. | Rejected: adding OpenXR package/API reference without manifest ownership. | Estimate: 2 us plus device driver impulse.
- [x] 10. Gamepad Haptics | DOD: gamepad scheme uses cached `_cachedGamepad.SetMotorSpeeds` only after epsilon change; no `Gamepad.current` hot-path lookup. | Rejected: calling motor speeds every frame. | Estimate: 1 us unchanged, driver call only on delta.
- [x] 11. Haptic Culling | DOD: `InputSchemeHashKeyboardMouse` drains and discards `HapticRequest`, zeros gamepad haptics, resets XR haptics, and returns before tool-runtime haptic buffer scanning. | Rejected: hidden no-op behind string scheme names or wasted CPU on non-haptic hardware. | Estimate: 0-1 us when queue empty.
- [x] 12. Device Lost Recovery | DOD: cached gamepad/XR removal publishes `SimulationPauseSignal(Paused=1)` through `GlobalSignals`; no submarine direct dependency. | Rejected: direct vehicle pause reference or scene search. | Estimate: 1 us on device-change event only.
- [x] 13. Zero-GC Pre-Simulation Read | DOD: pre-sim path uses cached InputAction-derived fields, struct quantization, NativeArray rings, and for-loops; dev overlay `Tick` no longer performs service retry/polling and relies on hot-swap callbacks. | Rejected: callback-only simulation consumption, managed replay lists, or registry lookup retries from `Tick`. | Estimate: 3-5 us active path on i3/MX350.
- [x] 14. Blackbox Scheme Hash | DOD: deterministic blackbox is `NativeArray<DeterministicInputTelemetryEntry>[300]` with `CurrentInputSchemeHash`; NaN/non-finite sanitization dumps to `Docs/AgentLogs/Dump_UNIVERSAL_INPUT_ORCHESTRATOR.bin`. | Rejected: Debug.Log trace or unbounded text telemetry. | Estimate: <1 us per frame write.
- [x] 15. Omega Compile Check [BLOCKED BY DEPENDENCY] | DOD: exact `dotnet build Hecton8.Core.csproj` rerun; blocked by 152 unrelated missing assemblies/types. `Hecton8.Input.csproj` and `Hecton8.Input.Generated.csproj` pass. Unity validated `BlackBoxMetricDashboard` and `EngineHealthOverlay` with 0 diagnostics. | Rejected: claiming green compile or editing fluid/scheduling/memory-layout/audio/world/inventory/tether/acoustic domains. | Estimate: 0 us runtime.

## Loop Log

- Loop 0: initialized state files; no runtime code touched.
- Loop 1: tasks 1-5 implemented; `dotnet build Hecton8.Core.csproj` attempted and blocked by unrelated missing assembly/reference wall (`Hecton8.Environment.Fluids`, `Hecton8.Core.Memory.Layout`, `Hecton8.Physics.CCD`, audio/cartography-style symbols). `dotnet build Hecton8.Input.csproj` and `Hecton8.Input.Generated.csproj` pass with 0 errors.
- Loop 2: tasks 6-10 verified by source scan. The same `dotnet build Hecton8.Core.csproj` attempt covers this slice and remains blocked by unrelated domain references before input-specific errors; narrower Input assemblies remain green.
- Loop 3: tasks 11-15 completed/blocked. Unity MCP refresh requested script compile, timed out after 60s, and console errors are outside input domain. Status remains PENDING VERIFICATION as mandated.
- Loop 4: OMEGA polish executed after all 15 tasks were checked/blocked; touched input files contain no `string.Format` and no `foreach`. Exact `dotnet build Hecton8.Core.csproj` rerun remains blocked by the same unrelated dependency wall.
- Loop 5: final static audit and persistent report pass; `LOG_UNIVERSAL_INPUT_ORCHESTRATOR.md` records what changed, compile blockers, cinematic cheats, and estimated microseconds saved.
- Loop 6: post-user re-audit fixed blackbox dump filename to `Dump_UNIVERSAL_INPUT_ORCHESTRATOR.bin`, preserved override scheme hashes, and moved KeyboardMouse haptic culling before tool haptic scanning. `Hecton8.Input.csproj` and `Hecton8.Input.Generated.csproj` pass.
- Loop 7: PDA parallax now uses `InputManager.TryReadUiPoint` instead of `Mouse.current`; follow-up audit found dev overlay keyboard polling as the last first-party bypass.
- Loop 8: dev overlays now subscribe to `InputManager` debug UI actions (`DebugToggleBlackBoxDashboard`, `DebugToggleEngineHealthOverlay`) instead of `Keyboard.current`; stale `Input.GetAxisRaw` design text was corrected. First-party raw-input scan is clean, touched code has no `string.Format`/`foreach`, `Hecton8.Input.csproj` and `Hecton8.Input.Generated.csproj` pass, and exact `Hecton8.Core.csproj` remains blocked by unrelated dependency errors.
- Loop 9: debug actions were upgraded for target hardware: F3/Ctrl+F10 remain for keyboard, Select+Shoulder chords cover gamepad/Steam Deck, and XR menu+right-hand button chords cover Quest Touch. Overlay subscription used a 30-frame retry as an interim bootstrap-order guard; Loop 10 superseded it with hot-swap callbacks. Exact Core build remained blocked by 96 unrelated errors.
- Loop 10: dev overlays now implement `IGlobalRegistryHotSwapListener` and subscribe/unsubscribe to `NativeInputManagerRuntime` on registry replacement, removing retry work from `Tick`. First-party raw-input scan is clean, touched input slice has no `string.Format`/`foreach`, `Hecton8.Input.csproj` and `Hecton8.Input.Generated.csproj` pass, Unity validates both overlays with 0 diagnostics, targeted `git diff --check` passes, and exact Core build remains blocked by 152 unrelated errors.
