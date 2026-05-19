# SHINOBU_36 Input Determinism

Date: 2026-05-18
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R32 architecture R4/proof-wording correction is the latest artifact-backed local static DOC_GLOBAL boundary for architecture/root documentation. R31 remains the prior current-boundary propagation layer, R30 remains the prior internal-currentness layer, R29 remains the prior stale-gate/global-authority layer, R28 remains the prior interior-boundary layer, and R27 remains the latest source-counter/index snapshot until rerun.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-19 DOC_GLOBAL R32 Current Boundary Note

R32 artifact-backed reread evidence keeps this file as static input-determinism orientation, not runtime input-device, replay, or platform proof. Current root/architecture boundary is `Docs/Reports/2026-05-19_DOCUMENTATION_R32_ARCHITECTURE_R4_AND_PROOF_WORDING_LOCAL.md`; R31 remains the prior current-boundary propagation correction. R30 remains the prior internal-currentness correction, R29 remains the prior stale-gate/global-authority correction, R28 remains the prior interior-boundary correction, and R27 source counters are retained until a newer counter pass reruns them. Current static gates: `Tools/AtlasCheck.py` remains red on `59` missing refs (RealtimeCSG vendor refs plus absent `VaultXRayWindow.cs` and `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`); `Docs/Modding/Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity/runtime/profiler/player-build proof remains absent.

Authoritative input sampling lives in `InputDispatcher.PreSimulationInputTick`.

Static/source contract; runtime proof pending fresh Unity import, Unity Console, Play Mode, profiler, GCMonitor, player-build, and route artifacts:
- `InputStateDTO` is 24 bytes: `float2 LookDelta`, `float2 MoveAxis`, `uint ButtonMask`, `uint _pad0`.
- `ButtonMask` bit layout: 0 Jump, 1 Interact, 2 PrimaryFire, 3 SecondaryFire, 4 Sprint, 5 Dash, 6 PDA, 7 Inventory, 8 Cancel, 9 TabNext, 10 TabPrevious, 11-14 ToolSlot1-4, 15 Flashlight, 16 Pause.
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
- `input_profiles.csv` in the project root is monitored by a cold `FileSystemWatcher`; file I/O and byte parsing are staged from the watcher path into a scalar `InputProfileDTO`, and the next PRE_SIMULATION pass only copies that staged DTO into Vault.
- Supported CSV keys: `inner_deadzone`, `outer_deadzone`, `move_exponent`, `mouse_sensitivity`, `mouse_acceleration`, `haptic_power_scale`, `haptic_thermal_scale`, `haptic_thermal_amplitude_scale`, `haptic_dispatch_interval_seconds`, `haptic_dispatch_interval`, and `mock_collision`.
- The CSV loader is event-gated. No steady-state per-frame file existence check or stream read occurs after initialization, and no dirty-load `FileStream` runs from the gameplay tick.
