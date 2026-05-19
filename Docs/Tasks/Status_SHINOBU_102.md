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
  - DOD: `ResolveScaledCommandBudget()` uses `quality^2`; `LoadSheddingJob` deterministically drops haptic/subtitle first and survival last, with CSV priority override and per-opcode `MaxPerFrame` cap enforcement.
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
  - DOD: added 300-entry `KernelExecutionTelemetryEntry` vault ring, exact per-frame AUP violation counts, and `Dump_COMMAND_FORGE.bin` spike dump on >0.5ms.
  - Alternative rejected: chat-only or managed log-only proof.
  - Microsecond estimate: one 64-byte telemetry write per processed frame; <1 us expected hot cost.
- [x] Task 18 KERNEL_INSPECTOR_EDITOR_WINDOW
  - DOD: added UI Toolkit `Mod Kernel Inspector` window with telemetry histogram and red shedding feedback.
  - Alternative rejected: IMGUI-only tuner reuse.
  - Microsecond estimate: editor-only; no runtime frame impact.
- [x] Task 19 CSV_KERNEL_TUNING_INGESTOR
  - DOD: added vault scratch `ReadOnlySpan<byte>` parser for `kernel_tuning_profiles.csv`; parser computes FNV opcode hashes, writes unmanaged tuning DTOs, and haptic/subtitle kernels consume profile range, duration, intensity, flags, priority, and max-frame data.
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
- Loop 9 standalone kernel parity pass: reread SHINOBU_102 XML and ARM64/Zero-GC/AUP mandates; added direct `+/-100000m` AUP magnitude guard to `HapticPulseKernelJob` and `TriggerSubtitleCue` alias support to `SubtitleCueKernelJob` so profiling jobs match the authoritative inlined router.
- Loop 10 active-doc counter pass: read `Docs/Tasks/POLISH.txt`, `Docs/README.md`, and `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`; corrected the active architecture ledger Mod API gate tuple from schema `14 / 160` to current static validation `15 / 161`.
- Loop 11 Mod API schema drift pass: static validator re-failed on `Source=162 Schema=161`; identified missing denied signal `ThermalSourceSignal`, updated schema/docs/architecture rows to revision 16 with `162 / 2 / 160`, and kept the mod projection surface unchanged.
- Loop 12 compile-wall/static proof pass: reread status/rationale/XML/binary ledger/AGENTS/domain map; confirmed no local ModdingAPI `.asmdef` exists, focused-greped the new kernel validator/editor/contracts path for sibling-domain usings, and recorded that SHINOBU_102 added no World/Gameplay/Physics/Caves/Localization dependency to the active kernel route. Re-ran the static Mod API validator at schema revision 16.
- Loop 13 telemetry/profile consumption pass: converted kernel AUP telemetry from boolean presence to exact count inside the existing 64-byte stats DTO, marked validator stats output as `[WriteOnly]`, and wired `ModKernelTuningProfile` into active and standalone haptic/subtitle Burst routes so CSV range/duration/intensity/flags are not inert.
- Loop 14 per-opcode budget pass: enforced `ModKernelTuningProfile.MaxPerFrame` inside `LoadSheddingJob`, normalized `TriggerSubtitleCue` to the subtitle profile, and made the shedder run when pending count exceeds the smallest active profile cap even if aggregate global budget is not yet exceeded.
- Loop 15 exact cap early-out pass: replaced the coarse `count <= cap` LoadSheddingJob early return with exact normalized opcode counters, preventing unnecessary ring compact-copy when a mixed queue exceeds the smallest profile cap but no individual opcode violates its own cap.
- Loop 16 pending overflow telemetry pass: repurposed `ModSandboxRingState` offset 44 as `PendingOverflowDropped`, saturating-counted full-ring enqueue drops, and folded that counter into pre-simulation dropped telemetry before resetting it.
- Loop 17 dead-shedder removal pass: removed unused private `DropThermalBacklog()` head-drop implementation so the only remaining backlog shedding route is the Burst `LoadSheddingJob` with priority and per-opcode caps.
- Loop 18 editor cadence pass: throttled `Mod Kernel Inspector` telemetry scan, label writes, and histogram repaints to 10Hz using `EditorApplication.timeSinceStartup`, preserving live feedback while reducing editor-loop overhead.

## Verification State

- `Docs/Modding/Validate_Mod_API_Static.ps1`: PASS after Loop 11, schema revision 16, source signals 162, projected signals 2, denied-by-default 160.
- `Docs/Modding/Validate_Mod_API_Static.ps1`: PASS after Loop 12, schema revision 16, source signals 162, projected signals 2, denied-by-default 160.
- `Docs/Modding/Validate_Mod_API_Static.ps1`: PASS after Loop 13, schema revision 16, source signals 162, projected signals 2, denied-by-default 160.
- `git diff --check` scoped to SHINOBU_102 files and touched Modding docs: PASS with line-ending warnings only.
- Hot-path grep on `FutureCommandSandboxValidator.cs` and `ModKernelInspectorWindow.cs`: no `new NativeArray/List/HashMap`, `string.Split`, `foreach`, `UnityEngine.Random`, or `Time.deltaTime`. One `.Complete()` remains only behind `IsCompleted` or shutdown force-finalization, not arbitrary frame blocking.
- Compile-wall focused grep: `FutureCommandSandboxValidator.cs`, `ModKernelInspectorWindow.cs`, and `ModSpatialContracts.cs` contain no direct usings for World/Gameplay/Physics/Caves/Localization sibling domains; legacy facades elsewhere in ModdingAPI still have historical direct references and remain disabled/obsolete.
- Loop 13 source review: `FutureCommandValidationStats` remains 64 bytes; offset 60 is now `AupViolations` instead of a dead reserve slot. Haptic/subtitle route profile lookup remains vault-backed and uses `[ReadOnly, NoAlias]` profile arrays. Forbidden hot-path grep returned no matches; scoped `git diff --check` passed with CRLF warnings only.
- Loop 14 verification: static Mod API validator PASS at schema revision 16. Forbidden hot-path grep found only `_scheduledValidationHandle.Complete()` in the existing finalize path, guarded by `IsCompleted` or shutdown force-finalization. Scoped `git diff --check` PASS with CRLF warnings only. `dotnet/csc` probe returned no running processes; full build intentionally not launched because the known external World deletion is still the compile blocker.
- Loop 15 source review: exact opcode counters are calculated in the first Burst scan and the no-drop path returns before Scratch/PendingRing copy. Forbidden pattern grep on validator/contracts/editor returned no matches for properties, Pack=1, foreach, Random, Time.deltaTime, string.Format, or Split.
- Loop 16 source review: enqueue overflow no longer silently disappears. `ModSandboxRingState` remains 64 bytes; offset 44 changed from reserve to `PendingOverflowDropped`. Pre-simulation reset happens before `LoadSheddingJob`, and telemetry uses saturating addition of enqueue overflow plus shedder drops.
- Loop 17 source review: `rg DropThermalBacklog` now finds no stale private head-drop implementation. Active shedding is consolidated in deterministic Burst code.
- Loop 18 source review: inspector still uses UI Toolkit and direct telemetry ring reads; refresh cadence is 0.10 seconds instead of every editor update. This is editor-only and does not add runtime code.
- Loop 9 verification: static Mod API validator re-run PASS; scoped diff-check PASS with CRLF warnings only; focused hot-path grep on validator/editor returned no matches; Burst directive grep shows all six command-kernel jobs use deterministic synchronous attributes; `dotnet/csc` process probe returned `NO_DOTNET_OR_CSC`.
- Full C# build: BLOCKED BY EXTERNAL DELETION of `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`; latest file-existence probe still returned `False`.
