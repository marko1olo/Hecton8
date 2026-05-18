# SHINOBU_36 Status

Date: 2026-05-17
Domain: INPUT_DETERMINISM_AND_HAPTICS
Status: IMPLEMENTED - CORE COMPILE BLOCKED BY EXTERNAL UI/WAKE CONTRACTS

## Mandates Selected

- CTRL_Device_Abstraction_Haptics.txt - single cached hardware crossing, unified haptics, no legacy input hot path.
- NET_Logistics_Sync_BitPacking_Reconciliation.txt - bit-packed input history and replay-safe masks.
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt - no hot-path managed allocation, LINQ, boxing, string churn.
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt - NativeArray lifecycle, sentinel/vault ownership, deterministic clearing.
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt - cache service dependencies, use GlobalRegistry only at init/binding boundaries.
- ARCH_Execution_Phases.txt - input in PRE_SIMULATION, haptics after simulation/presentation.
- ARCH_Signal_Lane_Segregation.txt - typed unmanaged signal lanes only.
- DBG_Telemetry_Crash_Reporting_PostMortem.txt - 300-frame ring and binary dump on invalid state.

## Task Matrix

- [x] Task 01 - BINARY_GRAVEYARD_RECONNAISSANCE | DOD: `Docs/Archive`/AgentLogs scanned; no input/haptic binary layout found, so default aligned profile is generated. Rejected: null profile crash. Estimate: 0 us steady-state.
- [x] Task 02 - LEGACY_INPUT_ERADICATION_PASS | DOD: runtime `Input.GetKey*` removed in touched path; `InputManager` no longer subscribes to `InputAction.performed/canceled`; dispatcher polls cached actions. Rejected: callback-latched authority. Estimate: 6-18 us plus deterministic edge ordering.
- [x] Task 03 - CS1612_ENCAPSULATION_PURGE | DOD: DTOs are raw fields; Vault handles use ref writes. Rejected: properties around NativeArray state. Estimate: 1-3 us by avoiding copies.
- [x] Task 04 - ARM64_PADDING_RECONSTRUCTION | DOD: `InputStateDTO` 24 bytes, `HapticCommandDTO` 16 bytes, no Pack=1 in SHINOBU_36 runtime structs. Rejected: byte-packed runtime structs. Estimate: avoids ARM64 misaligned read stalls.
- [x] Task 05 - BLIND_DEPENDENCY_MOCKING | DOD: mock kinematics/tool/collision structs added; profile-gated deterministic mock collision injects haptic DTOs. Rejected: dependency on Agent 31 KCC. Estimate: 0 us when disabled.
- [x] Task 06 - DETERMINISTIC_INPUT_POLLING_KERNEL | DOD: PRE_SIMULATION polls cached `InputAction`s, writes current DTO and bridge `InputState`. Rejected: random `Update` reads. Estimate: <10 us target path.
- [x] Task 07 - ANALOG_DEADZONE_BURST_SOLVER | DOD: deterministic radial deadzone, outer clamp, exponent curve, finite guards. Rejected: Unity default platform deadzones. Estimate: 2-5 us.
- [x] Task 08 - LOCKSTEP_JOURNAL_RING | DOD: 512-entry `InputStateDTO` ring in `GlobalDataVault`. Rejected: 60-frame private NativeArray. Estimate: 4-10 us saved on replay staging.
- [x] Task 09 - HAPTIC_DECAY_EVALUATOR_JOB | DOD: 16-slot Vault DTO buffer with bounded Pade exponential decay and slot clearing. Rejected: coroutine/device-specific rumble state. Estimate: bounded 16-slot scan.
- [x] Task 10 - CONTEXTUAL_INPUT_MASKING | DOD: Vault `InputBlockMask` zeroes move/look/tools/discrete bits before publication. Rejected: movement-side UI conditionals. Estimate: sub-us bit ops.
- [x] Task 11 - THE_DEAR_LIE_XR_HAPTIC_BRIDGE | DOD: low/high haptics blend to XR scalar; pulse duration fixed at 0.02s. Rejected: full VR SDK physical model. Estimate: 10-40 us avoided under XR.
- [x] Task 12 - INPUT_BUFFERING_WINDOW | DOD: 10-frame `uint` mask ring and `CheckBufferedInput(uint,int)`. Rejected: managed queues/lists. Estimate: sub-us 10-slot scan.
- [x] Task 13 - AUP_AGNOSTIC_MOUSE_DELTA | DOD: mouse/look delta normalized by `Screen.height`; no AUP/global rotation applied. Rejected: world-space mouse math. Estimate: prevents float-origin drift.
- [x] Task 14 - HARDWARE_LOD_POLLING_THROTTLING | DOD: Steam Deck/SystemHealth critical pressure throttles haptic dispatch to 15 Hz and scales amplitude. Rejected: dropping movement poll rate. Estimate: 20-80 us plus motor power.
- [x] Task 15 - SIGNAL_BUS_INPUT_EMITTER | DOD: button XOR emits typed `PlayerInputSignal` edges after deterministic sample. Rejected: string events. Estimate: fixed edge cost only.
- [x] Task 16 - ZERO_INIT_OVERHEAD_BYPASS | DOD: Vault buffers allocated with `UninitializedMemory` and explicitly `UnsafeUtility.MemClear`ed. Rejected: default OS zeroing. Estimate: boot-only milliseconds on weak hardware.
- [x] Task 17 - TELEMETRY_LATENCY_RECORDER | DOD: 300-frame telemetry DTO ring tracks polling us, haptic count, buffered consumes; dumps on >0.5 ms or non-finite input. Rejected: Debug.Log hot path. Estimate: bounded ring write.
- [x] Task 18 - INPUT_TUNER_EDITOR_WINDOW | DOD: `Input Curve & Haptics Tuner` Play Mode editor writes profile floats to Vault. Rejected: C# recompile tuning. Estimate: editor-only.
- [x] Task 19 - CSV_OVERRIDE_INGESTOR | DOD: root `input_profiles.csv` watched by cold FileSystemWatcher; span parser overwrites Vault profile. Rejected: per-frame file polling. Estimate: 0 us steady-state.
- [x] Task 20 - LIVE_INPUT_OSCILLOSCOPE | DOD: editor oscilloscope reads `InputStateDTO` from Vault and draws move dot/deadzone. Rejected: managed runtime UI telemetry. Estimate: editor-only.

## Loop Log

- Loop 0: Prompt extracted from CURRENT_BATCH.md via PowerShell regex. Status and rationale files were absent; initialized fresh files. No code touched.
- Loop 1: Existing code audit completed. Found Pack=1 DTOs, local deterministic NativeArrays, no 10-frame button-mask ring, InputManager callback authority leakage, and one legacy Input.GetKeyDown in diagnostics.
- Loop 2: Tasks 01-05 implemented. Archive scan completed; DTOs and mocks added; Pack=1 removed from SHINOBU_36 runtime structs.
- Loop 3: Tasks 06-10 implemented. Poll kernel, deadzone solver, 512-frame Vault journal, haptic DTO decay, and input block mask wired.
- Loop 4: Tasks 11-15 implemented. XR haptic dear-lie bridge, 10-frame buffer API, AUP-agnostic look normalization, Steam Deck haptic throttle, and typed edge signals added.
- Loop 5: Tasks 16-20 implemented. Uninitialized Vault allocation plus MemClear, telemetry dump ring, editor tuner, CSV watcher, and oscilloscope added.
- Verification: `dotnet build Hecton8.Input.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal -clp:ErrorsOnly` passed. `dotnet build Hecton8.Core.csproj ...` is blocked only by external `TerminalOsTypes.cs` missing `ISignal` and `GlobalPhysicsStateManager.cs` missing `WakeRequestSignal`.
