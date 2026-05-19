# SHINOBU_102 Status

Agent: SHINOBU_102
Domain: CORE INFRASTRUCTURE / MODDING API COMMAND KERNELS
Task count: 20

## Relevant Mandates Read

- ARCH_Execution_Phases: command drain must stay in PRE_SIMULATION and register scheduled jobs.
- ARCH_Global_Registry_ServiceLocator_DI_Init: no hot-path service polling; use existing vault/signal interfaces.
- ARCH_Signal_Lane_Segregation: emit typed unmanaged signals, not direct cross-domain mutations.
- DATA_Runtime_Struct_Layout_ARM64: explicit layouts, 8-byte aligned double/ulong fields, no Pack=1.
- OPT_Zero_GC_Policy_AllocFree_Mandate: no gameplay-path managed allocations, LINQ, boxing, string formatting.
- OPT_Native_Memory_Collections_JobSystem_Protocol: vault-backed NativeArray state and NoAlias jobs.
- MATH_AUP_Determinism_Sync: subtract observer AUP before casting local deltas to float3.
- DBG_Telemetry_Crash_Reporting_PostMortem: 300-frame ring and binary dump on fault/spike.

## Checklist

- [x] Task 01 DEV_NULL_ROUTING_ANALYSIS
  - DOD: traced `ModCommandDispatcher.DrainPreSimulation()` -> `FutureCommandSandboxValidator.DrainPreSimulation()` -> `ValidateFutureCommandEnvelopeJob.RouteEnvelope()`; injection point is pre-DevNull routing inside the Burst validator job.
  - Alternative rejected: legacy IModCommandKernel path, because it is disabled and managed-interface based.
  - Microsecond estimate: 15-40 us saved on spam frames by not reactivating legacy managed interface dispatch.
- [x] Task 02 LEGACY_COMMAND_ERADICATION
  - DOD: marked `ModCommand`, `IModCommandKernel`, `ModAupCommand`, and `ModRenderInstanceCommand` obsolete while preserving `LegacyCommandSurfaceEnabled = false` hard block.
  - Alternative rejected: deleting legacy API, because public API breakage is outside this domain pass.
  - Microsecond estimate: 5-15 us avoided by preventing accidental legacy queue writes and downstream dispatch.
- [x] Task 03 CS1612_ENCAPSULATION_PURGE
  - DOD: created `SurvivalOverrideSignal`, `HapticPulseSignal`, `SubtitleCueSignal` with raw public fields and no properties.
  - Alternative rejected: properties on signal structs, because Burst sees methods/defensive copies.
  - Microsecond estimate: 2-6 us per 1k signal copies by avoiding property call/copy patterns.
- [x] Task 04 ARM64_PADDING_RECONSTRUCTION
  - DOD: explicit layouts added and `ValidateLayoutOrDump()` checks sizes; `HapticPulseSignal` is 48 bytes because 32 bytes cannot hold `double3 + uint + float + float` safely.
  - Alternative rejected: Pack=1 or sequential layout.
  - Microsecond estimate: prevents unaligned ARM64 double loads; worst-case haptic spam avoids multi-cache-line repair cost.
- [x] Task 05 EMERGENCY_OPCODE_MAPPING_MOCK
  - DOD: added `GenerateEmergencyOpcodeMap()` with vault-backed records for FNV hashes `0x85C0241F`, `0xE6E4AEBB`, `0xA1B1CCCC`.
  - Alternative rejected: waiting for allowed_mod_opcodes.h8bin.
  - Microsecond estimate: cold-path only; saves integration stalls from missing opcode data.
- [x] Task 06 SURVIVAL_OVERRIDE_KERNEL
  - DOD: added deterministic Burst `SurvivalOverrideKernelJob` and inlined hot-route emission to `SignalBus<SurvivalOverrideSignal>`.
  - Alternative rejected: direct health/oxygen mutation.
  - Microsecond estimate: 10-25 us saved by no cross-domain survival mutation or managed callback.
- [x] Task 07 HAPTIC_PULSE_KERNEL
  - DOD: added deterministic Burst `HapticPulseKernelJob`; route localizes AUP before float3 cast, range-gates, inverse-square scales, and emits only haptic signal or fallback scalar.
  - Alternative rejected: direct XR/Input API calls.
  - Microsecond estimate: 20-80 us saved during spam because hardware/API fanout is not in kernel.
- [x] Task 08 SUBTITLE_CUE_KERNEL
  - DOD: added deterministic Burst `SubtitleCueKernelJob`; route emits numeric `SubtitleCueSignal` only.
  - Alternative rejected: string/localization lookup inside kernel.
  - Microsecond estimate: avoids managed string/localization lookup; 5-30 us per subtitle spam burst.
- [x] Task 09 THE_DEAR_LIE_HAPTIC_FALLBACK
  - DOD: low quality curve or `KernelFlagForceHapticCameraFallback` writes `ModKernelCameraJuiceImpulse` into vault and suppresses haptic bus output.
  - Alternative rejected: simulating or calling hardware haptics from command kernels.
  - Microsecond estimate: 20-80 us saved on i3/MX350/Quest haptic spam frames.
- [x] Task 10 CONTINUOUS_COMMAND_LOAD_SHEDDING
  - DOD: `ResolveScaledCommandBudget()` uses `quality^2`; `LoadSheddingJob` deterministically drops haptic/subtitle first and survival last, with CSV priority override support.
  - Alternative rejected: binary hardware-tier switches.
  - Microsecond estimate: caps worst-case command queue work from O(N spam) to O(dynamic budget); expected 50-300 us saved on hostile mod bursts.
- [x] Task 11 ROLLBACK_FREEZE_COMPLIANCE
  - DOD: `IsRollbackFrozen()` still reads buffer `(BufferID)70752` bit `1<<4`, but suppression moved into kernel route; haptic/subtitle are suppressed and survival still emits.
  - Alternative rejected: current global rollback rejection of all commands.
  - Microsecond estimate: 10-60 us saved on rollback UI/audio spam frames.
- [x] Task 12 KERNEL_REJECTION_TELEMETRY
  - DOD: `RejectEnvelope()` emits `ModInteractionRejectedPayload` into a configured `SignalBus` lane with ModHash, OpcodeHash, RequestId, Reason.
  - Alternative rejected: throwing or silent discard.
  - Microsecond estimate: fault path only; avoids downstream crash/autopsy time rather than hot-frame savings.
- [x] Task 13 AUP_PRECISION_VALIDATION
  - DOD: envelope gate keeps existing +/-50km sandbox; haptic kernel additionally validates finite double3 and +/-100000m before local float3 cast.
  - Alternative rejected: casting double3 directly to float3.
  - Microsecond estimate: prevents NaN/AUP corruption; avoids unbounded spatial hash/physics recovery cost.
- [x] Task 14 ZERO_INIT_OVERHEAD_BYPASS
  - DOD: new kernel map, telemetry, camera fallback, tuning, and scratch buffers use `GlobalDataVault` with `UninitializedMemory`; per-frame staging uses `MemClearElements()` only over the processing window.
  - Alternative rejected: zero-filling large buffers on scene load.
  - Microsecond estimate: avoids clearing untouched staging slots; expected 5-40 us saved depending backlog window.
- [x] Task 15 BURST_SYNCHRONOUS_COMPILATION
  - DOD: `SurvivalOverrideKernelJob`, `HapticPulseKernelJob`, `SubtitleCueKernelJob`, and `LoadSheddingJob` all use synchronous deterministic Burst attributes.
  - Alternative rejected: async Burst first-command hitch.
  - Microsecond estimate: avoids first-command Burst compile hitch; frame hitch prevention, not steady-state CPU.
- [x] Task 16 POINTER_ALIASING_STRICTNESS
  - DOD: input envelope arrays are `[ReadOnly, NoAlias]`; output signal writers are `[WriteOnly, NoAlias]`; disjoint vault buffers are used for source ring/staging/telemetry/camera impulses.
  - Alternative rejected: letting Burst assume aliasing between vault buffers.
  - Microsecond estimate: expected SIMD/vectorization preservation under Burst; 5-25 us per large batch.
- [x] Task 17 TELEMETRY_KERNEL_RECORDER
  - DOD: added 300-entry `KernelExecutionTelemetryEntry` vault ring and `Dump_COMMAND_FORGE.bin` spike dump on >0.5ms.
  - Alternative rejected: chat-only or managed log-only proof.
  - Microsecond estimate: one 64-byte telemetry write per processed frame; <1 us expected hot cost.
- [x] Task 18 KERNEL_INSPECTOR_EDITOR_WINDOW
  - DOD: added UI Toolkit `Mod Kernel Inspector` window with telemetry histogram and red shedding feedback.
  - Alternative rejected: IMGUI-only tuner reuse.
  - Microsecond estimate: editor-only; no runtime frame impact.
- [x] Task 19 CSV_KERNEL_TUNING_INGESTOR
  - DOD: added vault scratch `ReadOnlySpan<byte>` parser for `kernel_tuning_profiles.csv`; parser computes FNV opcode hashes and writes unmanaged tuning DTOs.
  - Alternative rejected: managed CSV parser in play mode.
  - Microsecond estimate: cold/editor only; avoids managed split/List parser allocations during play-mode tuning.
- [x] Task 20 LIVE_COMMAND_INJECTION_GIZMO
  - DOD: inspector constructs Survival/Haptic/Subtitle `FutureCommandEnvelope` packets, computes integrity hash, and injects via `FutureCommandSandboxValidator.Request()`.
  - Alternative rejected: requiring compiled mod DLL for kernel testing.
  - Microsecond estimate: editor-only; saves minutes of mod DLL iteration per kernel smoke test.

## Loop Log

- Loop 0 preflight: extracted SHINOBU_102 XML, read architecture binary ledger, confirmed status/rationale were absent and created this file.
- Loop 1 tasks 01-05: DevNull trace, legacy quarantine, DTO layout, emergency map implemented.
- Loop 2 tasks 06-10: survival/haptic/subtitle kernels, haptic Dear Lie fallback, and continuous load shedding implemented; compile verification pending.
- Loop 3 tasks 11-15: rollback-scoped suppression, rejection telemetry, AUP checks, vault uninitialized buffers, deterministic Burst job attributes implemented.
- Loop 4 tasks 16-20: NoAlias/WriteOnly annotations, kernel telemetry ring, UI Toolkit inspector, CSV tuning ingest, and live command injection implemented.
- Loop 5 strict self-read: ran `git diff --check`, hot-path allocation grep, Burst attribute grep, XML prompt reread, and CPU/dotnet guard. Static checks passed; compile blocked because CPU guard stayed above 90-100% while `dotnet/csc` were absent.
- Loop 6 compile gate: after CPU dropped below the guard threshold, `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` was attempted and failed before SHINOBU_102 code compiled because tracked World source `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` is deleted in the shared worktree. This is outside the command-kernel domain and was not restored.
- Loop 7 static Mod API drift gate: `Validate_Mod_API_Static.ps1` first failed on `Source=161 Schema=160`. The extra source signal is `HabitatFloodAcousticMuffleSignal` in `GlobalSignals.cs`; docs/schema were updated to revision 15 with `161 / 2 / 159` and the signal remains denied-by-default. Re-run passed.
- Loop 8 polish pass: fixed kernel telemetry so out-of-range haptic suppression is not counted as Dear Lie fallback, expanded layout gate to include `ModKernelTuningProfile` and `ModKernelCameraJuiceState`, marked public `HectonAPI.Commands` legacy methods obsolete, and aligned the binary payload ledger with Mod API schema revision 15.

## Verification State

- `Docs/Modding/Validate_Mod_API_Static.ps1`: PASS, schema revision 15, source signals 161, projected signals 2, denied-by-default 159.
- `git diff --check` scoped to SHINOBU_102 files and touched Modding docs: PASS with line-ending warnings only.
- Hot-path grep on `FutureCommandSandboxValidator.cs` and `ModKernelInspectorWindow.cs`: no `new NativeArray/List/HashMap`, `string.Split`, `foreach`, `UnityEngine.Random`, or `Time.deltaTime`. One `.Complete()` remains only behind `IsCompleted` or shutdown force-finalization, not arbitrary frame blocking.
- Full C# build: BLOCKED BY EXTERNAL DELETION of `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`; latest file-existence probe still returned `False`.
