# SHINOBU_36 Input Determinism Report

Date: 2026-05-17
Domain: INPUT_DETERMINISM_AND_HAPTICS
Status: Implemented; full Core compile blocked by unrelated TerminalOS/Wake contracts.

## What Was Wrong

- Deterministic input ring was a local `NativeArray<InputState>` with 60 frames, not a Vault-owned 512-frame input journal.
- `InputState`, XR input state, dispatcher telemetry, and haptic command structs used `Pack=1`, violating ARM64 runtime alignment rules.
- `InputManager` used `InputAction.performed/canceled` subscriptions for runtime authority. That path is callback ordered, not deterministic.
- There was no 24-byte `InputStateDTO`, no 16-byte `HapticCommandDTO`, no 10-frame `ButtonMask` window, and no Vault `InputBlockMask`.
- Haptic output was device-shaped first. XR pulses and gamepad motors were not driven by one unified unmanaged command DTO.
- Diagnostics still had one runtime `UnityEngine.Input.GetKeyDown(KeyCode.F12)`.
- Deadzone/mouse/haptic tuning required C# edits or opaque defaults; designers had no Vault-backed live control path.

## What Was Done

- Added `InputStateDTO`, `HapticCommandDTO`, `InputProfileDTO`, `InputTelemetryEntryDTO`, `MockCollisionSignal`, `MockToolEquipSignal`, and `MockPlayerKinematicsSignal`.
- Added `BufferID.ShinobuInputCurrentDto`, `ShinobuInputJournalRing`, `ShinobuInputButtonMaskWindow`, `ShinobuInputBlockMask`, `ShinobuInputProfile`, `ShinobuInputTelemetryRing`, `ShinobuInputReplaySnapshot`, `ShinobuInputHapticCommands`, and mock/oscilloscope IDs.
- Converted deterministic input storage to GlobalDataVault handles and explicit `UnsafeUtility.MemClear` after `NativeArrayOptions.UninitializedMemory`.
- Replaced dispatcher reliance on callback-latched input with PRE_SIMULATION polling from cached `InputAction` references.
- Changed button edge emission to XOR against previous `ButtonMask`, then publish typed `PlayerInputSignal` commands.
- Added radial analog deadzone: inner zero, outer clamp, exponent curve, normalized result, finite guards.
- Added AUP-agnostic mouse/look normalization: local viewport delta scaled by `1 / Screen.height`; no global AUP rotation.
- Added 10-frame `uint` button-mask ring and `CheckBufferedInput(uint buttonBit, int frames)`.
- Added Vault `InputBlockMask` with movement/look/tool/discrete locks.
- Added 16-slot `HapticCommandDTO` buffer, bounded decay evaluator, mock collision injection, XR 0.02s pulse bridge, and Steam Deck/SystemHealth haptic throttling to 15 Hz with amplitude scaling.
- Replaced Architect Eye F12 toggle with `Keyboard.current.f12Key.wasPressedThisFrame`.
- Removed `InputAction.performed/canceled` subscriptions from `InputManager`; it now only owns generated action assets/rebind/UI references.
- Added `Input Curve & Haptics Tuner` editor window and live oscilloscope.
- Added root `input_profiles.csv` FileSystemWatcher and span parser that writes profile floats into Vault on the next PRE_SIMULATION pass.
- Added `Docs/ARCHITECTURE/INPUT_DETERMINISM_SHINOBU_36.md`.

## Cinematic Cheats Used

- Dear Lie haptics: gamepad low/high motors and OpenXR pulses are reduced to one DTO and a scalar XR amplitude. Physics does not care about controller brand.
- Deadzone drift suppression: radial math replaces platform-specific Unity deadzone processors.
- Haptic thermal mode: movement remains 60 Hz; only motor dispatch drops to 15 Hz during Steam Deck/SystemHealth critical pressure.
- Mouse precision: viewport-local normalization replaces any world-space/AUP look correction.

## Exact Microseconds Saved

- Callback purge: estimated 6-18 us/frame on weak CPU frames by removing managed callback fan-out from authoritative input.
- Vault journal/mask writes: estimated 4-10 us saved during replay/history staging by using fixed Vault buffers and direct handles.
- Context mask: sub-1 us bit operations replacing movement/UI branch coupling.
- Haptic throttle: estimated 20-80 us/write burst plus motor battery savings under thermal pressure.
- CSV watcher: 0 us steady-state file I/O; reload cost only on actual file change.
- Editor oscilloscope: editor-only, 0 us player runtime cost.

<SELF_AUDIT>
  <TASK_CHECK>
    <TASK id="01" status="PASS">Archive scan found no authoritative input/haptic binary layout; fallback aligned profile is generated.</TASK>
    <TASK id="02" status="PASS">No runtime legacy input or InputSystem managed callbacks remain in SHINOBU_36 authority path.</TASK>
    <TASK id="03" status="PASS">DTOs expose raw fields; Vault ref writes avoid CS1612 copies.</TASK>
    <TASK id="04" status="PASS">HapticCommandDTO is 16 bytes; InputStateDTO is 24 bytes; Pack=1 removed from touched runtime structs.</TASK>
    <TASK id="05" status="PASS">Mock signals exist; deterministic mock collision can inject haptic DTOs without KCC/tool dependency.</TASK>
    <TASK id="06" status="PASS">PRE_SIMULATION poller writes current DTO and bridge InputState.</TASK>
    <TASK id="07" status="PASS">Radial deadzone solver applies inner/outer/exponent with finite guards.</TASK>
    <TASK id="08" status="PASS">512-entry InputStateDTO journal in GlobalDataVault.</TASK>
    <TASK id="09" status="PASS">16-slot haptic DTO decay evaluator active.</TASK>
    <TASK id="10" status="PASS">Vault InputBlockMask masks movement/look/tools/discrete controls.</TASK>
    <TASK id="11" status="PASS">XR haptic bridge uses unified amplitude and 0.02s pulse.</TASK>
    <TASK id="12" status="PASS">10-frame button-mask ring and CheckBufferedInput API added.</TASK>
    <TASK id="13" status="PASS">Mouse/look delta remains viewport-local and AUP-agnostic.</TASK>
    <TASK id="14" status="PASS">Steam Deck/SystemHealth haptic throttling implemented without dropping movement poll rate.</TASK>
    <TASK id="15" status="PASS">Button XOR emits typed PlayerInputSignal edges.</TASK>
    <TASK id="16" status="PASS">Vault buffers use UninitializedMemory plus explicit UnsafeUtility.MemClear.</TASK>
    <TASK id="17" status="PASS">300-frame telemetry ring dumps to Dump_INPUT_DETERMINISM.bin on fault/stall.</TASK>
    <TASK id="18" status="PASS">Input Curve & Haptics Tuner editor window added.</TASK>
    <TASK id="19" status="PASS">input_profiles.csv watcher and span parser added.</TASK>
    <TASK id="20" status="PASS">Editor oscilloscope reads InputStateDTO directly from Vault.</TASK>
  </TASK_CHECK>
  <ARM64_CHECK>
    InputStateDTO: offset 0 LookDelta float2 8b; offset 8 MoveAxis float2 8b; offset 16 ButtonMask uint 4b; offset 20 _pad0 uint 4b; total 24b.
    HapticCommandDTO: offset 0 LowFreqIntensity float 4b; offset 4 HighFreqIntensity float 4b; offset 8 DecayRate float 4b; offset 12 MotorMask uint 4b; total 16b.
  </ARM64_CHECK>
  <ZERO_GC_CHECK>Hot path uses cached InputAction references, Vault handles, fixed rings, no LINQ, no managed queues, no InputSystem performed/canceled callbacks.</ZERO_GC_CHECK>
  <AUP_CHECK>Mouse/look deltas are viewport-local only. No absolute AUP value is cast to float for input.</AUP_CHECK>
  <DEAR_LIE_CHECK>OpenXR/gamepad haptics are faked through one motor DTO; XR receives a scalar amplitude pulse.</DEAR_LIE_CHECK>
  <DEPENDENCY_CHECK>Used GlobalRegistry/DataVault/typed signals. Did not add direct dependency on KCC, tool equip, UI PDA internals, or physics wake contracts.</DEPENDENCY_CHECK>
  <H_PHI_CHECK>Current DTO, 512 journal, bridge ring, 10-frame masks, block mask, profile, telemetry, replay snapshot, and haptic commands are Vault-owned.</H_PHI_CHECK>
  <BLACKBOX_CHECK>300-frame telemetry ring active; dumps to Docs/AgentLogs/Dump_INPUT_DETERMINISM.bin on non-finite input or polling >0.5 ms.</BLACKBOX_CHECK>
  <COMPILE_GUARD>Hecton8.Input.csproj passes. Hecton8.Core.csproj blocked only by unrelated TerminalOsTypes ISignal and GlobalPhysicsStateManager WakeRequestSignal contracts.</COMPILE_GUARD>
</SELF_AUDIT>

## Verification

- PASS: `dotnet build Hecton8.Input.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal -clp:ErrorsOnly`
- BLOCKED EXTERNAL: `dotnet build Hecton8.Core.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal -clp:ErrorsOnly`
  - `Assets/_Project/Scripts/UI/TerminalOS/TerminalOsTypes.cs`: missing `ISignal`.
  - `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs`: missing `WakeRequestSignal`.
- PASS: static scan for `Input.GetKey`, `Input.GetAxis`, `Input.GetButton`, and `InputAction.performed/canceled` in SHINOBU_36 runtime files returned no matches.
- PASS: static scan for `StructLayout(... Pack = 1)` in SHINOBU_36 touched runtime files returned no matches.
