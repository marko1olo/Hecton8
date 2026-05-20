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

R45 root/architecture R43/R44 residue/proof-artifact/source-counter correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-20 DOC_GLOBAL R44 Root/Architecture Boundary Note

R45 root/architecture R43/R44 residue/proof-artifact/source-counter correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`) (R44 prior internal-residue/exact-route-field/proof-wording correction) keeps this file as static input-determinism orientation, not runtime input-device, replay, or platform proof. Current DOC_GLOBAL boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`; R44 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R44_ROOT_ARCHITECTURE_INTERNAL_RESIDUE_EXACT_ROUTE_FIELDS_LOCAL.md`; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current static gates: `Tools/AtlasCheck.py` remains red on `ATLAS_CHECK_FAIL references=6741 missing=59` (one Dynamic Decals missing vendor asset ref, RealtimeCSG vendor icon/readme image refs, and missing HabitatDamageBakePipeline source ref in the current atlas); `Docs/Modding/Validate_Mod_API_Static.ps1` passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only. Runtime proof remains absent.

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
- `input_profiles.csv` in the project root is the intended cold-profile artifact for the `FileSystemWatcher` path, but the current root scan does not find the file. Treat watcher/profile reload as pending artifact wiring until the CSV exists and a fresh artifact tuple records the path, parse output, and runtime/editor environment.
- Supported CSV keys: `inner_deadzone`, `outer_deadzone`, `move_exponent`, `mouse_sensitivity`, `mouse_acceleration`, `haptic_power_scale`, `haptic_thermal_scale`, `haptic_thermal_amplitude_scale`, `haptic_dispatch_interval_seconds`, `haptic_dispatch_interval`, and `mock_collision`.
- The CSV loader is event-gated. No steady-state per-frame file existence check or stream read occurs after initialization, and no dirty-load `FileStream` runs from the gameplay tick.
