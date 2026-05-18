# SHINOBU_36 Input Determinism

Status: PENDING UNITY VERIFICATION

## Authority

SHINOBU_36 owns deterministic input capture and haptic translation only. It does not own player movement, UI state, tool gameplay, physics collisions, or XR gameplay logic.

The active batch file was missing during the latest pass. The source assignment was recovered from `Docs/Archive/Batch008/Tasks/CURRENT_BATCH.md`.

## Runtime Shape

- PRE_SIMULATION: poll cached Unity InputSystem actions, apply deterministic deadzones, pack buttons into `uint ButtonMask`, write current DTO/journal/button-window Vault buffers.
- POST_SIMULATION: compare `currentButtonMask ^ previousButtonMask`, publish discrete input signals, write 300-frame telemetry.
- VISUAL_SYNC: dispatch haptics from bounded DTO buffers; gamepad and XR are presentation sinks, not gameplay truth.

## DTO Layout

`InputStateDTO`, 24 bytes:

- offset 0: `float2 LookDelta` (8 bytes)
- offset 8: `float2 MoveAxis` (8 bytes)
- offset 16: `uint ButtonMask` (4 bytes)
- offset 20: `uint _pad0` (4 bytes)

`HapticCommandDTO`, 16 bytes:

- offset 0: `float LowFreqIntensity` (4 bytes)
- offset 4: `float HighFreqIntensity` (4 bytes)
- offset 8: `float DecayRate` (4 bytes)
- offset 12: `uint MotorMask` (4 bytes)

No SHINOBU input/haptic DTO uses `Pack=1`.

## CSV Keys

`input_profiles.csv` is key,value ASCII. Supported keys:

- `inner_deadzone`
- `outer_deadzone`
- `move_exponent`
- `mouse_sensitivity`
- `mouse_acceleration`
- `haptic_power_scale`
- `haptic_thermal_amplitude_scale`
- `haptic_thermal_scale`
- `haptic_dispatch_interval_seconds`
- `haptic_dispatch_interval`
- `mock_collision`

Parser behavior:

- no LINQ
- no string split
- no `float.Parse`
- `ReadOnlySpan<byte>` line trim
- lower-ASCII FNV-1a key hash
- dirty/retry-gated file I/O only

## Verification Boundary

Static source and CLI compile checks are not Unity runtime proof. Required remaining proof:

- Unity import and Console error check
- Play Mode route through bootstrap/world scene
- GCMonitor proof of 0 B/frame in input polling/haptic dispatch steady state
- profiler marker sample for polling and haptics
- device haptic proof for gamepad and XR pulse bridge
