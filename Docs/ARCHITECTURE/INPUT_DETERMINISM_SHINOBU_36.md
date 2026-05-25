# SHINOBU_36 Input Determinism

Date: 2026-05-18

Status: PENDING VERIFICATION

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not input-device runtime, replay determinism, haptics route, profiler, or player-build proof.

- `Assets/_Project/Scripts/Core/InputDispatcher.cs`

- `Assets/_Project/Scripts/Core/InputDeterminismDtos.cs`

- `Assets/_Project/Scripts/Input/Determinism/DeterministicInputContracts.cs`

- `Assets/_Project/Scripts/Input/InputManager.cs`

## Runtime Contract Notes

Authoritative input sampling lives in `InputDispatcher.PreSimulationInputTick`.

Static/source contract; runtime proof pending fresh Unity import, Unity Console, Play Mode, profiler, GCMonitor, player-build, and route artifacts:

- `InputStateDTO` is 24 bytes: `float2 LookDelta`, `float2 MoveAxis`, `uint ButtonMask`, `uint _pad0`.

- `ButtonMask` core: 0 Jump, 1 Interact, 2 PrimaryFire, 3 SecondaryFire, 4 Sprint, 5 Dash, 6 PDA, 7 Inventory.
- `ButtonMask` UI/tools: 8 Cancel, 9 TabNext, 10 TabPrevious, 11-14 ToolSlot1-4, 15 Flashlight, 16 Pause.

- Deterministic history is `BufferID.ShinobuInputJournalRing`, 512 entries in `GlobalDataVault`.

- The current frame DTO is `BufferID.ShinobuInputCurrentDto`, one entry in `GlobalDataVault`.

- Button buffering is `BufferID.ShinobuInputButtonMaskWindow`, ten `uint` masks.

- Legacy `TryConsumeBufferedAction(action, maxAgeSeconds)` remains for existing movement consumers, but the seconds input is converted to a deterministic 60 Hz frame window and no longer uses `Time.time`.

- Context locks use `BufferID.ShinobuInputBlockMask`; UI should set `BlockMovement` and `BlockTools` instead of adding movement-side conditionals.

- Haptics use `HapticCommandDTO`, 16 bytes, with a 16-slot Vault command buffer.

- `PlayerInputState`, `XRInputState`, `BufferedActionEntry`, and tool haptic command payloads now declare explicit natural sizes so runtime input/haptics structs do not rely on implicit tail padding.

- Blackbox telemetry is `BufferID.ShinobuInputTelemetryRing`, 300 entries, dumped to `Docs/AgentLogs/Dump_INPUT_DETERMINISM.bin` on non-finite input or >0.5 ms polling.

Input path:

- `InputManager` owns the generated InputSystem asset and rebinding/UI module references only.

- Runtime input is polled from cached `InputAction` references in `InputDispatcher`.

- Discrete command signals are emitted from current/previous `ButtonMask` XOR, not from `WasPressedThisFrame` or InputSystem callbacks.

- XR look-at uses a cached `IPlayerRuntimeContext` refreshed through the GlobalRegistry hot-swap listener; it does not poll `GlobalRegistry.Player` during ray staging.

- No runtime `Input.GetKeyDown`, `Input.GetAxis`, `Input.GetButton`, or `InputAction.performed/canceled` authority remains in the SHINOBU_36 path.

Designer control:

- `HECTON-8/Input Curve & Haptics Tuner` edits profile floats in Vault during Play Mode.

- `input_profiles.csv` in the project root is the intended cold-profile artifact for the `FileSystemWatcher` path.
- Current root scan does not find the file.
- Treat watcher/profile reload as pending wiring until the CSV exists and a fresh artifact tuple records path, parse output, and runtime/editor environment.

- Supported CSV keys: `inner_deadzone`, `outer_deadzone`, `move_exponent`, `mouse_sensitivity`, `mouse_acceleration`, `haptic_power_scale`, `haptic_thermal_scale`, `haptic_thermal_amplitude_scale`, `haptic_dispatch_interval_seconds`, `haptic_dispatch_interval`, and `mock_collision`.

- The CSV loader is event-gated. No steady-state per-frame file existence check or stream read occurs after initialization, and no dirty-load `FileStream` runs from the gameplay tick.
