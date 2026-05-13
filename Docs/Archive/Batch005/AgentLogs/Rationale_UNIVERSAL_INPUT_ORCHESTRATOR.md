# Rationale - UNIVERSAL_INPUT_ORCHESTRATOR

Status: PENDING VERIFICATION

## Initial Decisions

Problem: Input domain must support PC, Mac, Steam Deck, Quest VR without hardware-specific gameplay polling.
Solution: Use Unity Input System `InputAction` assets at the Unity boundary, convert once per pre-simulation frame into blittable input state/signal structs and bitmasks, then feed existing determinism/registry/signal infrastructure if present.
Rejected Alternatives: Legacy `UnityEngine.Input`, `InputManager.Instance`, device-name string branches, and per-frame `Gamepad.current` lookup are rejected by CTRL_Device_Abstraction_Haptics and zero-GC mandates.
Scalability potential: Low = direct cached action reads and binary haptic cull; Middle = EWMA gyro smoothing and delta-gated haptics; High = priority haptic blending; Ultra = device-specific trigger/haptic extensions behind adapters only.
Hardware Impact: i3/MX350 target is under 0.20 ms input+haptics budget if reads are cached and haptic writes are delta-gated; current measured proof absent.

Problem: Multiple agents are rewriting adjacent systems.
Solution: Only use existing `GlobalRegistry` interfaces or typed EventBus/signal queues; add contracts by expansion only if source proves no existing owner.
Rejected Alternatives: Direct references to player, submarine, haptics director concrete classes, or scene search wiring.
Scalability potential: Low = no-op fallback if service absent; Middle/High/Ultra = richer providers can register without changing consumers.
Hardware Impact: Interface/queue boundary preserves cache behavior and avoids scene scans on low-end silicon.

Problem: Critical input failure needs post-mortem evidence.
Solution: Push current input scheme hash and high-level frame state into a fixed-size blackbox buffer or existing telemetry owner once discovered.
Rejected Alternatives: Debug.Log spam, unbounded text traces, or exception-driven diagnostics.
Scalability potential: Low = 300-entry scheme/action ring; High/Ultra = extra device/smoothing/haptic state fields if budget allows.
Hardware Impact: 300 compact entries are sub-20 KB scale; no VRAM impact.

## Loop 1 Decisions

Problem: The project already had a bootstrap-owned `InputManager` and deterministic `InputDispatcher`, but no singleton call sites.
Solution: Kept `InputManager` as the Unity InputAction owner and added `IInputDeterminismService` as the registry-facing contract tier; `IInputService` now inherits it for compatibility.
Rejected Alternatives: Removing the `InputManager` component or inventing another scene singleton would break bootstrap wiring and duplicate InputAction ownership.
Scalability potential: Low = current registry service; Middle = replay/delay state; High = external universal provider can bind through the contract; Ultra = platform adapters stay behind the same state signal.
Hardware Impact: No runtime search added; registry lookup remains O(1), estimated 0 us frame delta on i3/MX350.

Problem: The production action asset lacked Quest/OpenXR controller schemes.
Solution: Extended the referenced `HectonRuntimeInputActions.inputactions` with `KeyboardMouse`, `Gamepad`, and `XR_Touch`; mapped XR grip to `Interact` and shared the same bitmask as non-VR input.
Rejected Alternatives: A new orphan `.inputactions` asset or Quest-only code path was rejected because bootstrap references the Resources asset and the project is multiplatform.
Scalability potential: Low = keyboard/mouse no haptic work; Middle = generic gamepad; High = XR Touch via Input System; Ultra = future device-specific bindings can be added to the same action map.
Hardware Impact: Binding expansion is cold data only; hot path remains cached action/state reads.

Problem: `dotnet build Hecton8.Core.csproj` cannot currently isolate input code because the core project is red on unrelated missing domain assemblies and generated symbols.
Solution: Recorded the dependency wall and ran narrower input assembly builds; `Hecton8.Input.csproj` and `Hecton8.Input.Generated.csproj` pass with 0 errors.
Rejected Alternatives: Claiming green compile or patching out-of-domain Environment/Audio/World/Save dependencies.
Scalability potential: Low/Middle/High/Ultra unaffected; compile wall is assembly graph debt, not input runtime cost.
Hardware Impact: No runtime impact.

## Loop 2 Decisions

Problem: VR controller grip must mean the same gameplay action as PC interact without a VR-only branch in gameplay.
Solution: Bound both XR hand `gripPressed` controls to `Interact` and mirrored runtime XR grip into `PlayerInputAction.Interact` before deterministic publication.
Rejected Alternatives: Separate Quest interact enum, hand-specific gameplay callbacks, or polling XR devices in player code.
Scalability potential: Low = no XR work outside XR mode; Middle = one generic XR_Touch scheme; High = hand-specific presentation can use XR snapshots; Ultra = richer hand tracking can feed the same bitmask.
Hardware Impact: One bitwise OR in XR-active frames, under 1 us on i3/MX350.

Problem: Steam Deck/DualSense gyro is noisy and cannot enter the replay ring as raw tremor.
Solution: Added EWMA smoothing inside `SteamDeckInputPal` before adding gyro to `LookDelta`; device binding remains cached from gamepad change handling.
Rejected Alternatives: Raw angular velocity, moving average buffers, or Steam Deck-specific gameplay polling.
Scalability potential: Low = gyro disabled/no-op; Middle = EWMA; High = tuned alpha per hardware tier; Ultra = user sensitivity curves behind the same PAL.
Hardware Impact: Two float2 lerps/clamps, estimated 2 us on i3/MX350.

Problem: Haptic requests come from physics/fauna/vehicle lanes and must not leak hardware decisions back into emitters.
Solution: `InputDispatcher` drains `GlobalSignals.HapticRequest`, blends it with `ToolHapticsRuntime`, culls KeyboardMouse, routes XR to `XRControllerWithRumble.SendImpulse`, and routes gamepad to cached motor speeds with delta gating.
Rejected Alternatives: Per-frame `Gamepad.current`, direct OpenXR package dependency absent from manifest, or haptic dispatch from gameplay emitters.
Scalability potential: Low = discard on KBM; Middle = cached gamepad rumble; High = XR impulse refresh gating; Ultra = priority device profiles under the same request packet.
Hardware Impact: Inactive path is a queue drain and two zero-output gates; active path avoids driver writes unless amplitude changes or XR refresh expires.

## Loop 3 Decisions

Problem: Keyboard/mouse users must not pay for haptic hardware work.
Solution: Scheme hash gates haptic output; `KeyboardMouse` drains requests, zeros any stale motors, and resets XR amplitudes.
Rejected Alternatives: Leave requests queued for later or branch on device display strings.
Scalability potential: Low = full cull; Middle = gamepad delta gate; High = XR impulse gate; Ultra = device-profile impulse envelopes.
Hardware Impact: Empty/inactive path is sub-1 us and avoids driver calls.

Problem: Controller loss can leave movement input latched and drive the vehicle.
Solution: Device removal/disconnect for cached gamepad or XR controllers publishes `SimulationPauseSignal` through `GlobalSignals`.
Rejected Alternatives: Direct submarine control reference, scene lookup, or waiting for gameplay code to detect stale input.
Scalability potential: Low/Middle = pause on device loss; High/Ultra = future reconnect resume signal can use the same sequence/source hash.
Hardware Impact: Event-only branch, no frame cost.

Problem: Input failures need objective post-mortem data under this agent ID.
Solution: Deterministic input blackbox uses a 300-entry NativeArray carrying `InputState`, scheme hash, and packed axes; non-finite sanitization dumps to `Docs/AgentLogs/Dump_UNIVERSAL_INPUT_ORCHESTRATOR.bin`.
Rejected Alternatives: Log spam, managed lists, or writing another agent's dump path.
Scalability potential: Low = scheme/action ring; Middle = replay MMF; High = crash telemetry bridge; Ultra = per-device adapter state can be packed later.
Hardware Impact: One NativeArray write per deterministic input tick, estimated <1 us.

Problem: Compile verification is mandatory, but current project graph is red outside input ownership.
Solution: Ran exact `dotnet build Hecton8.Core.csproj`, then narrower input builds and Unity MCP console read. Core and Unity are blocked by external domain errors, while Input assemblies compile.
Rejected Alternatives: Editing Fluid/UI/World/Audio dependency walls from the input domain.
Scalability potential: Not runtime relevant.
Hardware Impact: No runtime impact.

## Loop 4 Decisions

Problem: Polish mandate required anti-bloat checks after core tasks, including no `string.Format`, no `foreach`, exact core build, and git diff evidence.
Solution: Ran targeted scans over touched input/domain files; no `string.Format` or `foreach` remains in this slice. Reran exact `dotnet build Hecton8.Core.csproj`; it is still blocked by unrelated missing assemblies/types before input-specific failures. Captured diff/stat evidence without reverting concurrent work.
Rejected Alternatives: Parsing polish mandate early, hiding the dependency wall, or editing unrelated Fluid/UI/World/Audio assemblies.
Scalability potential: Low = no managed iterator/string formatting overhead; Middle = cached scheme gates; High = haptic driver calls only on delta; Ultra = richer per-device profiles without changing emitters.
Hardware Impact: Low-end i3/MX350 keeps input hot path at estimated 3-5 us, haptic inactive path at 0-1 us, and scheme telemetry at <1 us on change.

## Loop 5 Decisions

Problem: The CTO-visible report must live on disk and survive context compression.
Solution: Appended `Docs/AgentLogs/LOG_UNIVERSAL_INPUT_ORCHESTRATOR.md` with wrong state, completed work, compile blockers, cinematic cheats, and exact microsecond estimates.
Rejected Alternatives: Chat-only completion report or unverifiable optimism.
Scalability potential: Low/Middle/High/Ultra reporting documents the platform tiers and where visual overkill can be bought with saved haptic/input cycles.
Hardware Impact: Documentation only; no runtime impact.

## Loop 6 Decisions

Problem: Re-audit found a factual mismatch: status/rationale claimed the blackbox dump used this agent ID, while `InputDispatcher` still pointed to `Dump_INPUT_DETERMINISM_BRIDGE.bin`.
Solution: Changed the dump constant to `Docs/AgentLogs/Dump_UNIVERSAL_INPUT_ORCHESTRATOR.bin`; no runtime allocation or cadence change.
Rejected Alternatives: Leaving the older bridge dump name or creating a second duplicate dump.
Scalability potential: Low/Middle/High/Ultra unchanged; post-mortem ownership becomes unambiguous across agent slices.
Hardware Impact: 0 us frame cost.

Problem: Automation override scheme hashes were assigned, then overwritten by current hardware scheme resolution in the same capture.
Solution: Preserve non-zero override scheme hashes and fall back to current scheme only when the override does not provide one.
Rejected Alternatives: Treating all automation/replay inputs as local hardware or adding a new override-specific signal.
Scalability potential: Low = local hardware scheme; Middle = replay scheme hash; High/Ultra = remote/device-lab automation can preserve origin without new channels.
Hardware Impact: One integer branch per captured frame, estimated <1 us on i3/MX350.

Problem: Keyboard/mouse haptic culling still scanned tool-runtime haptic buffers after discarding `HapticRequest`.
Solution: Return immediately after draining requests, zeroing gamepad motors, and resetting XR haptics for `KeyboardMouse`.
Rejected Alternatives: Scanning read-only tool haptic buffers on hardware that cannot emit haptics.
Scalability potential: Low = no haptic CPU beyond queue drain; Middle/High/Ultra keep richer haptic scan only when hardware can use it.
Hardware Impact: Saves estimated 1-3 us on keyboard/mouse frames with active tool haptic buffers.

## Loop 7 Decisions

Problem: PDA parallax still read `Mouse.current`, which is a direct hardware lookup outside the agnostic action asset path.
Solution: Added `InputManager.TryReadUiPoint(out Vector2)` over the cached UI `Point` action and routed PDA parallax through `GlobalRegistry.NativeInputManager`; fallback remains screen center.
Rejected Alternatives: Leaving direct mouse polling, adding a public interface method for one UI presentation use, or inventing another input service.
Scalability potential: Low = center fallback without pointer; Middle = mouse/touch point through UI action; High/Ultra = XR/UI pointer bindings can feed the same action later.
Hardware Impact: Equivalent one `InputAction.ReadValue<Vector2>()` at UI boundary; no device search and no GC.

Problem: Two direct `Keyboard.current` reads remained in development-only overlay toggles.
Solution: Added dev-only UI `InputAction` events on `InputManager` and routed `BlackBoxMetricDashboard` plus `EngineHealthOverlay` through subscriptions instead of per-frame hardware polling.
Rejected Alternatives: Leaving dev-only keyboard polling, deleting useful diagnostics, or adding a second debug input singleton.
Scalability potential: Low = no debug overlay cost when disabled; Middle = editor keyboard debug actions; High = gamepad/Deck debug bindings can be added to the same UI action map; Ultra = XR debug gestures can bind without changing overlay code.
Hardware Impact: Removes two development polling reads per UI tick; estimated 1-2 us saved in editor/development overlay frames on i3/MX350.

## Loop 8 Decisions

Problem: The debug overlay fix needed to preserve useful F3/Ctrl+F10 toggles without reintroducing hardware-specific branches.
Solution: Extended `HectonRuntimeInputActions.inputactions` with `DebugToggleBlackBoxDashboard` and `DebugToggleEngineHealthOverlay`; runtime repair also creates those actions/composites if an older action asset is cloned.
Rejected Alternatives: Hardcoding `Keyboard.current`, relying only on the asset with no runtime repair, or moving debug toggles into gameplay action bits.
Scalability potential: Low = keyboard debug controls; Middle = gamepad or Steam Deck chord bindings; High = XR controller debug chords; Ultra = platform-specific debug controls via bindings only.
Hardware Impact: Event-only cold path; 0 us pre-simulation cost and no device lookup in overlay `Tick`.

Problem: A non-code data note still mentioned `Input.GetAxisRaw`, keeping first-party purge evidence dirty.
Solution: Updated the stale design text to describe the agnostic `InputAction` service.
Rejected Alternatives: Ignoring a first-party scan hit or editing third-party demo packages outside the domain.
Scalability potential: Documentation now points future agents away from legacy polling on all platform tiers.
Hardware Impact: Documentation only; no runtime impact.

Problem: Verification needed to separate input regressions from the existing shared build wall.
Solution: Reran first-party raw-input scan, action asset JSON parse, `dotnet build Hecton8.Input.csproj`, `dotnet build Hecton8.Input.Generated.csproj`, exact `dotnet build Hecton8.Core.csproj`, `git diff --check`, and Unity validation for patched overlays.
Rejected Alternatives: Claiming green Core compile or hiding third-party/historical-doc noise as current first-party code.
Scalability potential: Low/Middle/High/Ultra input path remains binding-driven; no new per-platform branch in gameplay.
Hardware Impact: Touched input code remains no `foreach`, no `string.Format`; hot path budget unchanged, debug overlay polling reduced by estimated 1-2 us when compiled.

## Loop 9 Decisions

Problem: Debug overlay actions were agnostic in code but keyboard-only in bindings, which is insufficient for Steam Deck and Quest verification.
Solution: Added gamepad/Steam Deck Select+Shoulder chords and XR Touch menu+right-hand button chords to the same debug UI actions, with runtime repair matching the action asset.
Rejected Alternatives: Device-specific debug polling, separate Quest/Deck debug components, or leaving target hardware dependent on an attached keyboard.
Scalability potential: Low = keyboard debug hotkeys; Middle = gamepad/Steam Deck chords; High = XR Touch chords; Ultra = additional platform debug chords can be added as bindings only.
Hardware Impact: 0 us pre-simulation cost; debug action matching remains Input System event-driven and cold outside action events.

Problem: Overlay components could enable before `GlobalRegistry.NativeInputManager` existed and then miss debug toggle subscription.
Solution: Added a 30-frame low-cadence retry while unbound; once subscribed, there is no retry work.
Rejected Alternatives: Per-frame hardware polling, scene lookup, or assuming bootstrap order is always stable with 20+ concurrent agents.
Scalability potential: Low/Middle/High/Ultra overlays recover across bootstrap order variations without platform-specific code.
Hardware Impact: Worst case while unbound is one registry lookup every 30 frames in development overlay code; estimated <0.1 us averaged on i3/MX350.

Problem: Exact Core build evidence changed after other agents modified unrelated domains.
Solution: Reran exact `dotnet build Hecton8.Core.csproj`; input assemblies still compile, while Core is blocked by 96 unrelated errors including missing fluid/scheduling/CCD/audio/world/inventory symbols and duplicate `HectonUnderwaterVisuals` members.
Rejected Alternatives: Editing underwater visuals or shared dependency walls from the input domain.
Scalability potential: Not runtime relevant to the input path.
Hardware Impact: No runtime impact.

## Loop 10 Decisions

Problem: The previous overlay bootstrap-order guard used a 30-frame registry retry from `Tick`, which still violates the hot-path registry mandate even though it was low cadence.
Solution: `BlackBoxMetricDashboard` and `EngineHealthOverlay` now implement `IGlobalRegistryHotSwapListener`; enable/start performs one cold lookup, registry replacement performs event-driven rebind, and `Tick` never retries service discovery.
Rejected Alternatives: Keeping low-cadence polling, scene lookup, or adding another debug input singleton.
Scalability potential: Low = no retry work after enable; Middle = debug overlays recover when bootstrap order changes; High = Deck/gamepad and XR debug bindings keep the same callback surface; Ultra = more diagnostic overlays can reuse hot-swap listener wiring without platform branches.
Hardware Impact: Removes the residual unbound-overlay registry lookup from development frames; estimated <0.1 us averaged while unbound and 0 us steady-state on i3/MX350.

Problem: Verification needed to separate this input lifecycle patch from concurrent repo-wide errors.
Solution: Reran first-party raw-input and touched-code anti-bloat scans, focused input builds, Unity script validation for both overlays, targeted diff whitespace check, and exact Core build.
Rejected Alternatives: Editing unrelated Quest rationale whitespace, hiding the Core wall, or patching missing fluid/scheduling/memory-layout/audio/world/inventory/tether/acoustic domains.
Scalability potential: Input slice remains hardware-agnostic; platform-specific debug access is binding data only.
Hardware Impact: Runtime input cost unchanged; dev overlay lifecycle is cleaner and avoids hot-path service discovery.
