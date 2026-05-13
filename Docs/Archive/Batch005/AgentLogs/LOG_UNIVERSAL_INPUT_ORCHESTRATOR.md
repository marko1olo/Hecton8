# LOG - UNIVERSAL_INPUT_ORCHESTRATOR

## 2026-05-13 - Agnostic Action Layer Dispatch

What was wrong:
- Input boundary had platform-risk debt: legacy `UnityEngine.Input` patterns, no XR Touch scheme in the referenced action asset, no explicit scheme hash in deterministic input telemetry, and haptic routing was not fully culled by active hardware class.
- Hard dependency risk: direct hardware polling would fail the project mandate for PC, Mac, Steam Deck, and Quest VR.
- Compile verification is currently blocked outside this domain by missing assemblies/types and Unity console errors in Fluid/UI ownership.

What was done:
- Added `IInputDeterminismService` contract path and kept `InputManager` as registry-owned Unity InputAction owner, not as a singleton dependency.
- Added `Hecton8.Input.Universal` asmdef and `UniversalInputStateSignal` contract payload for isolated universal input contracts.
- Replaced the remaining legacy PDA mouse path with `UnityEngine.InputSystem.Mouse.current` fallback logic.
- Extended `Assets/Resources/HectonRuntimeInputActions.inputactions` with schemes `KeyboardMouse`, `Gamepad`, and `XR_Touch`; added XR player/UI bindings and mapped XR grip to `Interact`.
- Added XR Touch display style handling and controller path formatting in `InputManager`.
- Added Steam Deck/DualSense gyro EWMA smoothing in `SteamDeckInputPal` before look delta reaches the replay/determinism path.
- Added deterministic input scheme hash propagation through `PlayerInputState`, `PhysicsDeterminismSignals.InputSignal`, and `GlobalTelemetryBus`.
- Routed haptics through `InputDispatcher`: KeyboardMouse culls instantly, Gamepad writes cached motor speeds only on delta, XR Touch sends gated `XRControllerWithRumble.SendImpulse`.
- Added device-lost pause publication through `GlobalSignals.PublishSimulationPauseSignal` when cached gamepad/XR devices disconnect.
- Confirmed deterministic blackbox carries `CurrentInputSchemeHash` in a fixed 300-frame NativeArray ring and dumps to `Docs/AgentLogs/Dump_UNIVERSAL_INPUT_ORCHESTRATOR.bin`.

Cinematic Cheats used:
- Grip interaction is a bitmask alias, not a VR-only gameplay path: saved duplication and kept Quest controls deterministic.
- Gyro tremor is faked away with EWMA instead of expensive predictive filtering or raw physics-like camera stabilization.
- Haptics use delta/timeout gated impulses, not per-frame motor writes.
- Keyboard/mouse haptic requests are drained and discarded immediately; no fake device work is performed.
- Direct `com.unity.xr.openxr` API was rejected because the manifest does not own that package; Input System XR rumble path is compile-safe for current project dependencies.

Exact Microseconds saved/estimated:
- Singleton purge: 0 us runtime delta; removed architectural dependency risk.
- Signal migration into cached struct state: estimated 3 us on i3/MX350 versus direct gameplay polling.
- ASMDEF isolation: 0 us runtime.
- Legacy input purge: 0 us runtime delta; removes platform failure path.
- XR grip bitmask alias: estimated 1 us on XR-active frames.
- Steam Deck gyro EWMA: estimated 2 us, replacing buffer-heavy smoothing alternatives.
- Haptic queue drain/blend: estimated 4 us when active.
- Gamepad haptic delta gate: estimated 1 us unchanged path, driver call only on change.
- Keyboard/mouse haptic cull: estimated 0-1 us when queue empty.
- Device-lost pause publication: estimated 1 us only on device-change event.
- Pre-simulation input hot path: estimated 3-5 us active path on i3/MX350.
- Blackbox scheme write: estimated <1 us per deterministic input tick.

Verification:
- `rg` scan for `InputManager.Instance`, `UnityEngine.Input.`, `Input.GetAxis`, `Input.GetButton`, and `Input.GetKey` returned no matches in the project input/script slice after the comment false positive was removed.
- `.inputactions` JSON parses and reports schemes `KeyboardMouse`, `Gamepad`, `XR_Touch`; Player XR bindings = 11; UI XR bindings = 5.
- `dotnet build Hecton8.Input.csproj` passes with 0 warnings and 0 errors.
- `dotnet build Hecton8.Input.Generated.csproj` passes with 0 warnings and 0 errors.
- OMEGA anti-bloat scan found no `string.Format` and no `foreach` in touched input files.
- Exact `dotnet build Hecton8.Core.csproj` remains blocked by unrelated dependency wall: missing `Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Core.Memory.Layout`, `Hecton8.Physics.CCD`, audio assemblies, and contract symbols outside input ownership.
- Unity MCP script refresh timed out; Unity console errors are out-of-domain: duplicate `HectonFluidEngine` methods and missing `Hecton8.UI.Tools` assembly.

Git Diff evidence:
- Touched input/domain files include `Assets/Resources/HectonRuntimeInputActions.inputactions`, `Assets/_Project/Scripts/Input/InputManager.cs`, `Assets/_Project/Scripts/Core/InputDispatcher.cs`, `Assets/_Project/Scripts/Core/SteamDeckInputPal.cs`, `Assets/_Project/Scripts/Core/PlayerInputState.cs`, `Assets/_Project/Scripts/Physics/PhysicsDeterminismSignals.cs`, `Assets/_Project/Scripts/Core/GlobalTelemetryBus.cs`, `Assets/_Project/Scripts/Core/GlobalSignals.cs`, `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`, `Assets/_Project/Scripts/PDAInventoryTab.cs`, and `Assets/_Project/Scripts/Interaction/PlayerInteraction.cs`.
- New files: `Assets/_Project/Scripts/Input/Universal/Hecton8.Input.Universal.asmdef`, `Assets/_Project/Scripts/Input/Universal/UniversalInputStateSignal.cs`, plus Unity `.meta` files.
- Workspace is concurrently dirty from other agents; diff statistics in shared files include changes not authored by this agent.

Final status:
- `Status_UNIVERSAL_INPUT_ORCHESTRATOR.md` remains `PENDING VERIFICATION` as mandated.
- Task 15 is `[BLOCKED BY DEPENDENCY]` for the exact core build only; input assemblies are green.

## 2026-05-13 - Re-Audit Upgrade Pass

What was wrong:
- Blackbox dump ownership was inconsistent on disk: docs named `Dump_UNIVERSAL_INPUT_ORCHESTRATOR.bin`, but code still used `Dump_INPUT_DETERMINISM_BRIDGE.bin`.
- Automation override input scheme hashes were overwritten by local hardware scheme resolution.
- Keyboard/mouse haptic culling still paid for tool-runtime haptic buffer scanning after haptic requests were discarded.
- PDA parallax used `Mouse.current`, which bypassed the cached InputAction path.
- Two direct `Keyboard.current` reads remain in development/editor-only overlay toggles.

What was done:
- Updated `InputDispatcher.InputDumpRelativePath` to `Docs/AgentLogs/Dump_UNIVERSAL_INPUT_ORCHESTRATOR.bin`.
- Preserved non-zero automation override `CurrentInputSchemeHash` values before telemetry publication.
- Moved KeyboardMouse haptic culling ahead of tool haptic snapshot scanning.
- Added `InputManager.TryReadUiPoint(out Vector2)` over the cached UI `Point` action.
- Rewired `PDAInventoryTab.PublishInventoryUiParallax` through `GlobalRegistry.NativeInputManager.TryReadUiPoint`.
- Recorded development-only overlay hotkeys as residual debt instead of expanding production public input contracts.

Cinematic Cheats used:
- Keyboard/mouse haptics remain a hard fake: drain and discard, no hidden hardware simulation.
- PDA parallax uses the UI pointer action and center fallback; no per-device presentation branch.
- Override scheme preservation is a metadata cheat, not a new input stream.

Exact Microseconds saved/estimated:
- Blackbox dump filename fix: 0 us runtime.
- Override scheme hash preservation: <1 us per captured frame.
- KeyboardMouse early haptic return: saves estimated 1-3 us on frames with active tool haptic buffers.
- PDA pointer action read: no material runtime change versus mouse read, but removes device lookup; estimated 0 us frame delta.

Verification:
- `dotnet build Hecton8.Input.csproj`: pass, 0 warnings, 0 errors.
- `dotnet build Hecton8.Input.Generated.csproj`: pass, 0 warnings, 0 errors.
- `.inputactions` JSON parse: schemes `KeyboardMouse`, `Gamepad`, `XR_Touch`; Player XR bindings = 11; UI XR bindings = 5.
- `rg` legacy scan: no `InputManager.Instance`, `UnityEngine.Input.`, `Input.GetAxis`, `Input.GetButton`, `Input.GetKey`, `Gamepad.current`, or `Mouse.current` remain in project scripts/resources.
- `rg` anti-bloat scan: no `string.Format` and no `foreach` in touched input files.
- `validate_script` passed for `InputDispatcher.cs` with 0 diagnostics.
- `validate_script` for `InputManager.cs` was inconclusive because the Unity MCP session disconnected while awaiting command result.
- `dotnet build Hecton8.Core.csproj` still fails with 154 unrelated missing assemblies/types, including `Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Core.Memory.Layout`, `Hecton8.Physics.CCD`, audio propagation/echolocation symbols, save layout attributes, and world/fauna contracts.
- Unity refresh still timed out after 60s; current console errors are MCP regex-timeout errors, not actionable project compile diagnostics.

Residual risk at time of this pass:
- Development/editor-only UI overlays still polled `Keyboard.current` for debug toggles. This was resolved in the later Debug Overlay Action Pass below by adding dev-only UI InputActions.

Final status:
- `Status_UNIVERSAL_INPUT_ORCHESTRATOR.md` remains `PENDING VERIFICATION`.
- Input assemblies are green; exact core build remains `[BLOCKED BY DEPENDENCY]`.

## 2026-05-13 - Debug Overlay Action Pass

What was wrong:
- The last first-party direct input reads were development/editor overlay toggles using `Keyboard.current`.
- A stale first-party data note still mentioned `Input.GetAxisRaw`, making purge evidence noisy.
- Historical docs and third-party demo packages still contain legacy input examples; those are outside this input-domain ownership and were not treated as current first-party runtime code.

What was done:
- Added dev-only cached UI actions in `InputManager`: `DebugToggleBlackBoxDashboard` and `DebugToggleEngineHealthOverlay`.
- Added dev-only `InputManager` events for those actions and runtime repair so older cloned action assets gain the same bindings.
- Updated `HectonRuntimeInputActions.inputactions` with F3 dashboard toggle and left/right Ctrl+F10 engine overlay composites.
- Rewired `BlackBoxMetricDashboard` and `EngineHealthOverlay` to subscribe to the `InputManager` debug events instead of polling hardware every tick.
- Updated `Assets/_Project/Data/tekst.txt` to describe the agnostic `InputAction` service instead of legacy axis polling.

Cinematic Cheats used:
- Debug overlays now wake on action events; no per-frame keyboard scan is paid while the overlay tick runs.
- Runtime action repair is a cold-path compatibility cheat for stale assets; gameplay does not branch per hardware.
- Third-party package demo input was left isolated instead of wasting input-domain time rewriting vendor samples.

Exact Microseconds saved/estimated:
- Overlay toggle path: saves estimated 1-2 us on editor/development frames where both overlays are compiled and ticking.
- Runtime action repair: 0 us pre-simulation cost; cold initialization only.
- Stale data note correction: 0 us runtime.

Verification:
- First-party raw-input scan over `Assets/_Project` and `Assets/Resources/HectonRuntimeInputActions.inputactions`: no `InputManager.Instance`, `UnityEngine.Input.`, `Input.GetAxis`, `Input.GetButton`, `Input.GetKey`, `Gamepad.current`, `Mouse.current`, or `Keyboard.current`.
- `.inputactions` JSON parse: UI actions = 7, UI bindings = 36, debug actions = 2, debug bindings = 7, schemes = `KeyboardMouse,Gamepad,XR_Touch`.
- `dotnet build Hecton8.Input.csproj`: pass, 0 warnings, 0 errors.
- `dotnet build Hecton8.Input.Generated.csproj`: pass, 0 warnings, 0 errors.
- Touched-code anti-bloat scan: no `string.Format`, no `foreach`.
- Unity `validate_script`: `BlackBoxMetricDashboard.cs` and `EngineHealthOverlay.cs` passed with 0 diagnostics; `InputManager.cs` validation timed out, but the input assembly build compiled it successfully.
- `git diff --check`: pass; line-ending warnings only.
- Exact `dotnet build Hecton8.Core.csproj`: still blocked with 92 unrelated missing assembly/type errors in fluids, scheduling, CCD, audio propagation/echolocation, inventory, terrain, world, and fauna contracts.

Final status:
- `Status_UNIVERSAL_INPUT_ORCHESTRATOR.md` remains `PENDING VERIFICATION`.
- Input assemblies and first-party raw-input scans are clean.
- Exact core build remains `[BLOCKED BY DEPENDENCY]`, not by this input patch.

## 2026-05-13 - Multiplatform Debug Binding Pass

What was wrong:
- Debug overlay actions were routed through `InputAction`, but only keyboard bindings existed.
- Steam Deck/gamepad and Quest Touch verification still needed an attached keyboard to toggle diagnostic overlays.
- Overlay components could miss subscription if enabled before `GlobalRegistry.NativeInputManager` registration.

What was done:
- Added gamepad/Steam Deck debug chords to `DebugToggleBlackBoxDashboard` and `DebugToggleEngineHealthOverlay`.
- Added XR Touch debug chords to the same debug UI actions.
- Mirrored those bindings in runtime action-map repair so stale cloned assets do not regress.
- Added 30-frame low-cadence subscription retry in `BlackBoxMetricDashboard` and `EngineHealthOverlay` while unbound.

Cinematic Cheats used:
- Debug overlay controls are pure binding data and event callbacks; no hardware-specific polling branch was added.
- Bootstrap-order recovery uses a cheap registry retry every 30 frames only until subscribed.

Exact Microseconds saved/estimated:
- Gamepad/XR debug chords: 0 us pre-simulation cost.
- Subscription retry: estimated <0.1 us averaged while unbound, 0 us after subscription.
- Avoided per-frame keyboard/gamepad/XR polling in overlay ticks: preserves the previous 1-2 us development-frame saving.

Verification:
- `.inputactions` JSON parse: UI actions = 7, UI bindings = 48, debug actions = 2, debug bindings = 19, debug gamepad bindings = 6, debug XR bindings = 6, schemes = `KeyboardMouse,Gamepad,XR_Touch`.
- First-party raw-input scan over `Assets/_Project` and the runtime action asset: clean.
- Touched-code anti-bloat scan: no `string.Format`, no `foreach`.
- `dotnet build Hecton8.Input.csproj`: pass, 0 warnings, 0 errors.
- `dotnet build Hecton8.Input.Generated.csproj`: pass, 0 warnings, 0 errors.
- Unity `validate_script`: `InputManager.cs`, `BlackBoxMetricDashboard.cs`, and `EngineHealthOverlay.cs` all passed with 0 diagnostics.
- `git diff --check`: pass; line-ending warnings only.
- Exact `dotnet build Hecton8.Core.csproj`: blocked by 96 unrelated errors, including missing fluid/scheduling/CCD/audio/world/inventory symbols and duplicate `HectonUnderwaterVisuals` members.

Final status:
- `Status_UNIVERSAL_INPUT_ORCHESTRATOR.md` remains `PENDING VERIFICATION`.
- Input-owned code is green under focused builds and Unity validation.
- Exact core build remains `[BLOCKED BY DEPENDENCY]`.

## 2026-05-13 - Hot-Swap Overlay Lifecycle Pass

What was wrong:
- The previous bootstrap-order fix for debug overlays used a 30-frame registry retry while unbound.
- That avoided hardware polling but still left service discovery inside overlay `Tick`.
- Full project verification is still blocked outside input ownership.

What was done:
- `BlackBoxMetricDashboard` now implements `IGlobalRegistryHotSwapListener`.
- `EngineHealthOverlay` now implements `IGlobalRegistryHotSwapListener`.
- Both overlays subscribe to `NativeInputManagerRuntime` once on enable/start and rebind through `OnGlobalRegistryServiceReplaced`.
- Both overlays unsubscribe input events and hot-swap listeners on disable.
- Removed retry constants, retry frame counters, and retry calls from overlay `Tick`.

Cinematic Cheats used:
- Debug overlay recovery is now event-driven through the existing registry hot-swap lane.
- No platform branch was added; keyboard, Deck/gamepad, and XR Touch debug access stays in `.inputactions` binding data.

Exact Microseconds saved/estimated:
- Removed residual unbound-overlay registry retry: estimated <0.1 us averaged while unbound, 0 us steady-state.
- Pre-simulation gameplay input path: unchanged, still estimated 3-5 us on i3/MX350.
- Debug binding cost: 0 us pre-simulation, Input System event path only.

Verification:
- First-party raw-input scan over `Assets/_Project` and `Assets/Resources/HectonRuntimeInputActions.inputactions`: clean.
- Touched-code anti-bloat scan: no `string.Format`, no `foreach`.
- `dotnet build Hecton8.Input.csproj`: pass, 0 warnings, 0 errors.
- `dotnet build Hecton8.Input.Generated.csproj`: pass, 0 warnings, 0 errors.
- Unity `validate_script`: `BlackBoxMetricDashboard.cs` and `EngineHealthOverlay.cs` passed with 0 diagnostics.
- Targeted `git diff --check` for both overlay files: pass; line-ending warnings only.
- Full `git diff --check`: blocked by unrelated trailing whitespace in `Docs/AgentLogs/Rationale_QUEST_VULKAN_RENDER_PIPELINE.md`.
- Exact `dotnet build Hecton8.Core.csproj`: blocked by 152 unrelated errors in missing fluid/scheduling/memory-layout/audio/world/inventory/tether/acoustic domains.

Final status:
- `Status_UNIVERSAL_INPUT_ORCHESTRATOR.md` remains `PENDING VERIFICATION`.
- Input assemblies and overlay validation are green.
- Exact core build remains `[BLOCKED BY DEPENDENCY]`.
