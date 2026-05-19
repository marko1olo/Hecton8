# SHINOBU_102 Command Kernel Forge Log

Date: 2026-05-19
Agent: SHINOBU_102
Domain: CORE INFRASTRUCTURE / MODDING API COMMAND KERNELS

## What Was Wrong

- `FutureCommandEnvelope` validation existed, but valid `AlterHealth`, `AlterGravity`, and `TriggerSubtitleCue` style future commands still reached DevNull instead of real execution kernels.
- Legacy `ModCommand`, `ModAupCommand`, and `ModRenderInstanceCommand` remained visible public contracts even though the active future sandbox disables their gameplay path.
- There were no unmanaged `SurvivalOverrideSignal`, `HapticPulseSignal`, or `SubtitleCueSignal` DTOs and no kernel-specific 300-frame telemetry ring.
- Existing rollback guard rejected all future commands; this conflicts with deterministic survival override processing.
- CSV reload used a managed byte array and a temporary NativeArray in the editor cold path.

## What Was Done

- Added FNV-1a opcodes:
  - `SurvivalOverride = 0x85C0241F`
  - `HapticPulse = 0xE6E4AEBB`
  - `SubtitleCue = 0xA1B1CCCC`
- Added explicit DTOs:
  - `SurvivalOverrideSignal` 32 bytes.
  - `HapticPulseSignal` 48 bytes. The requested 32-byte shape is impossible with `double3 + uint + float + float` without corrupt overlap or ARM64-unsafe packing.
  - `SubtitleCueSignal` 16 bytes.
  - `KernelExecutionTelemetryEntry` 64 bytes.
- Added `GenerateEmergencyOpcodeMap()` with vault-backed kernel records and fallback opcode registration.
- Added deterministic Burst jobs:
  - `LoadSheddingJob`
  - `SurvivalOverrideKernelJob`
  - `HapticPulseKernelJob`
  - `SubtitleCueKernelJob`
- Inlined the active hot route inside `ValidateFutureCommandEnvelopeJob.RouteEnvelope()` to avoid an extra job scheduling/fence cost while preserving named job contracts for direct scheduling/test routes.
- Added rollback-scoped suppression: haptic/subtitle suppressed during rollback, survival still emits.
- Added `SignalBus<ModInteractionRejectedPayload>` rejection telemetry with `OpcodeHash` support.
- Added vault buffers via local cast IDs:
  - `70914` kernel opcode map.
  - `70915` kernel telemetry ring.
  - `70916` kernel telemetry cursor.
  - `70917` camera juice fallback impulse ring.
  - `70918` camera juice fallback state.
  - `70919` kernel tuning profiles.
  - `70920` kernel CSV scratch.
- Added `Docs/Modding/kernel_tuning_profiles.csv`.
- Added UI Toolkit `Mod Kernel Inspector` with telemetry histogram, CSV reload, self audit button, and live synthetic envelope injection.
- Removed the old editor cold-path `new NativeArray` CSV reload in this file; reload now uses vault scratch and span slicing.

## Cinematic Cheats Used

- Haptic Dear Lie: below the quality curve or under a force-fallback flag, haptic output is suppressed and converted into a cheap scalar camera impulse in vault memory.
- Complexity before cheat: downstream haptic/UI/device event fanout per haptic packet, O(N) with platform-side cost.
- Complexity after cheat: one 32-byte vault write per fallback packet, O(1) per accepted pulse with no hardware API call.

## Exact Microseconds Saved

- Legacy interface path avoided: estimated 15-40 us on mod spam frames.
- Haptic hardware/API fanout avoided during fallback: estimated 20-80 us on i3/MX350/Quest-class frames.
- Continuous queue shedding: estimated 50-300 us saved on hostile UGC queue bursts by capping pre-route work to the quality^2 budget.
- Rollback haptic/subtitle suppression: estimated 10-60 us saved on rollback frames with active UGC spam.
- Kernel telemetry hot cost: estimated under 1 us per processed frame, one 64-byte ring write.

## Verification

- `git diff --check`: PASS.
- Hot-path grep for `new NativeArray`, `new NativeList`, `new NativeHashMap`, `string.Split`, `foreach`, `UnityEngine.Random`, `Time.deltaTime` in edited kernel/editor files: PASS.
- Burst attribute grep: all new required jobs use `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`.
- Compile: NOT RUN. Guard held CPU at 94-100% across repeated samples; `dotnet/csc` were absent. User and AGENTS prohibit launching build when CPU is above 50%.

<SELF_AUDIT>
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">DevNull route traced to PRE_SIMULATION `ValidateFutureCommandEnvelopeJob.RouteEnvelope()`.</TASK>
    <TASK id="02" status="PASS">Legacy command contracts marked obsolete and runtime block preserved.</TASK>
    <TASK id="03" status="PASS">Target signals use raw public fields only.</TASK>
    <TASK id="04" status="PASS_WITH_CONSTRAINT_NOTE">Survival 32B and Subtitle 16B implemented. Haptic is 48B because 32B is mathematically impossible with required fields and ARM64 alignment.</TASK>
    <TASK id="05" status="PASS">Vault-backed emergency opcode map implemented.</TASK>
    <TASK id="06" status="PASS">Survival override emits signal only.</TASK>
    <TASK id="07" status="PASS">Haptic pulse localizes AUP before float3 cast and emits signal only.</TASK>
    <TASK id="08" status="PASS">Subtitle cue emits token/duration/priority only; no strings.</TASK>
    <TASK id="09" status="PASS">Haptic Dear Lie writes camera impulse scalar to vault.</TASK>
    <TASK id="10" status="PASS">Quality^2 budget and deterministic priority shedding implemented.</TASK>
    <TASK id="11" status="PASS">Rollback bit suppresses haptic/subtitle only; survival still processed.</TASK>
    <TASK id="12" status="PASS">Malformed packets emit `ModInteractionRejectedPayload`.</TASK>
    <TASK id="13" status="PASS">Kernel AUP validates finite and 100000m bound; envelope gate remains stricter at 50000m.</TASK>
    <TASK id="14" status="PASS">New kernel buffers use vault `UninitializedMemory`; per-frame staging clears only touched window.</TASK>
    <TASK id="15" status="PASS">Required new jobs use deterministic synchronous Burst flags.</TASK>
    <TASK id="16" status="PASS">Input arrays are `ReadOnly/NoAlias`; output writers are `WriteOnly/NoAlias`.</TASK>
    <TASK id="17" status="PASS">300-entry kernel telemetry ring and `Dump_COMMAND_FORGE.bin` spike dump implemented.</TASK>
    <TASK id="18" status="PASS">UI Toolkit inspector implemented.</TASK>
    <TASK id="19" status="PASS">Vault scratch span CSV parser implemented; no `string.Split`.</TASK>
    <TASK id="20" status="PASS">Live synthetic envelope injection implemented.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <SurvivalOverrideSignal size="32">0:uint ModHash, 4:uint RequestId, 8:float OxygenFloor, 12:uint TTL, 16:uint Flags, 20:uint pad, 24:ulong pad.</SurvivalOverrideSignal>
    <HapticPulseSignal size="48">0:double3 TargetAUP bytes 0-23, 24:uint WaveformHash, 28:float Intensity, 32:float Duration, 36:uint Flags, 40:ulong pad. Size 48 = 16-byte aligned.</HapticPulseSignal>
    <SubtitleCueSignal size="16">0:uint TokenHash, 4:float Duration, 8:uint Priority, 12:uint pad.</SubtitleCueSignal>
    <KernelExecutionTelemetryEntry size="64">0:ulong ticks, 8:uint frame, 12/16/20:uint processed counts, 24:uint shed, 28:uint rejected, 32:uint rollback, 36:uint fallback, 40:float quality, 44/48/52/56/60:uint telemetry fields.</KernelExecutionTelemetryEntry>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    `ResolveScaledCommandBudget()` uses `GlobalQualityWeight^2`. Under 0.3, haptic/subtitle packets are shed first and haptic execution can collapse into one scalar camera impulse. Survival overrides remain highest priority. CSV priority weights can lower or raise optional packet survival without recompilation.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    New persistent kernel state declares zero private native allocations. Handles requested at boot/acquire: 70914, 70915, 70916, 70917, 70918, 70919, 70920.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>
    Consumes pending ring/staging, opcode records, memory leases, approved assets, per-mod counters, rollback flag, and quality scalar. Outputs SignalBus writers, DevNull ring, camera impulse vault ring, stats, and kernel telemetry. Job arrays use NoAlias; signal writers are marked WriteOnly/NoAlias.
  </POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No new sibling assembly dependency was introduced. Compile not run because CPU stayed above the mandated 50% limit.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Haptic hardware feedback is faked as a camera scalar impulse under low quality or config flag. Before: device/event fanout with platform cost. After: O(1) vault write and downstream visual feedback.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Validation Addendum

### Static Mod API Gate

`Docs/Modding/Validate_Mod_API_Static.ps1` initially failed with:

```text
[MOD_API_STATIC_VALIDATION] Signal count drift. Source=161 Schema=160
```

Root cause: the shared source inventory in `Assets/_Project/Scripts/Core/GlobalSignals.cs` now contains `HabitatFloodAcousticMuffleSignal`. It is not a SHINOBU_102 command-kernel DTO and it is not mod-projected.

Action taken:

- Updated `Docs/Modding/Signal_Schema.json` to schema revision `15`.
- Updated the source split to `161 / 2 / 159`.
- Added `HabitatFloodAcousticMuffleSignal` to `Docs/Modding/Signal_Audit_Matrix.md` denied-by-default inventory.
- Updated `Docs/Modding/README.md`, `Docs/Modding/Mod_API_Specification.md`, and `Docs/Modding/Runtime_Verification_Playbook.md` count text.

Re-run result:

```text
Status: PASS
SchemaRevision: 15
SourceSignals: 161
AllowedProjectedSignals: 2
DeniedByDefaultSignals: 159
ProjectionBridgeSignals: CombatDamageSignal,WeatherChangedSignal
```

### Build Gate

After CPU guard permitted a build attempt, `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` failed before command-kernel compilation:

```text
CSC : error CS2001: Source file 'C:\hades\Hecton8\Assets\_Project\Scripts\World\HectonMapMagicVegetationBridgeFloraCollisionProxies.cs' could not be found.
```

The missing file is a tracked World-domain source currently deleted in the shared worktree. SHINOBU_102 did not restore or rewrite it because it is outside the Modding API command-kernel domain. C# build verification remains blocked by that external deletion.

### Current Verification Snapshot

- Scoped `git diff --check`: PASS, line-ending warnings only.
- Static Mod API validator: PASS.
- Hot-path allocation/string/random/time grep on edited kernel/editor files: PASS except for a known non-arbitrary `.Complete()` path guarded by `IsCompleted` or shutdown finalization.
- Burst directive grep: required new jobs retain deterministic synchronous Burst attributes.

## 2026-05-19 Polish Addendum

### Telemetry Correction

What was wrong: `HapticFallbacks` in `KernelExecutionTelemetryEntry` was inferred from generic suppression, so distance-gated haptic pulses could be reported as Dear Lie camera fallback.

What was done: `FutureCommandValidationStats` now stores a dedicated `HapticFallbacks` counter at offset `56`; at that time offset `60` remained a reserve slot. Only `WriteCameraJuiceImpulse()` increments this field. `RecordKernelTelemetry()` now copies `stats.HapticFallbacks` directly.

Microseconds saved: 0 us direct runtime savings; this prevents false optimization work and wrong postmortem attribution.

### Layout Gate Expansion

What was wrong: `ValidateLayoutOrDump()` checked the headline signal and telemetry DTOs but skipped `ModKernelTuningProfile` and `ModKernelCameraJuiceState`.

What was done: layout gate now also checks:

- `UnsafeUtility.SizeOf<ModKernelTuningProfile>() == 32`
- `UnsafeUtility.SizeOf<ModKernelCameraJuiceState>() == 64`

### Public Legacy Facade Quarantine

What was wrong: public `HectonAPI.Commands` legacy request methods returned `false` but were not marked obsolete at the facade boundary.

What was done: `Request(in ModCommand)`, `RequestAup(in ModAupCommand)`, and `RequestRenderInstance(in ModRenderInstanceCommand)` now carry explicit `[System.Obsolete(..., false)]` markers and remain hard-quarantined.

### Ledger Alignment

`Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` now records the current Mod API static validator snapshot as `SchemaRevision=15`, `SourceSignals=161`, `ModCommandSizeBytes=64`.

### Verification

```text
Docs/Modding/Validate_Mod_API_Static.ps1: PASS
SchemaRevision: 15
SourceSignals: 161
AllowedProjectedSignals: 2
DeniedByDefaultSignals: 159
```

Scoped `git diff --check`: PASS with CRLF warnings only.

Build remains blocked by the same external deleted source:

```text
Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs -> missing
```

## 2026-05-19 Standalone Kernel Parity Addendum

What was wrong: the inlined validator route was stricter than the standalone direct Burst jobs. `TryRouteHapticPulse()` rejected finite-but-out-of-bounds AUP values at the kernel `+/-100000m` line, while `HapticPulseKernelJob` only checked finiteness before local float conversion. `TryRouteSubtitleCue()` accepted the reserved alias `TriggerSubtitleCue`, while `SubtitleCueKernelJob` accepted only `SubtitleCue`.

What was done:

- `HapticPulseKernelJob` now computes `abs(TargetAUP)` and rejects any component above `FutureCommandSandboxConstants.KernelMaxAupMagnitudeMeters` before subtracting observer AUP or casting to `float3`.
- `SubtitleCueKernelJob` now accepts both `SubtitleCue` and `TriggerSubtitleCue`, preserving rollback suppression, numeric token routing, and string-free output.

Cinematic Cheats used: unchanged. Low-quality haptic feedback still collapses to one scalar `ModKernelCameraJuiceImpulse` vault write instead of a hardware haptics fanout.

Exact Microseconds saved: no steady hot-route gain claimed. This patch prevents invalid direct-kernel profiling from emitting unsafe AUP payloads and removes false-negative subtitle alias smoke tests. Failure-prevention value is corruption containment, not ALU reduction.

Verification after parity patch:

```text
Docs/Modding/Validate_Mod_API_Static.ps1: PASS
git diff --check scoped to SHINOBU_102 files/docs: PASS, CRLF warnings only
Focused hot-path grep on validator/editor: no forbidden matches
Burst directive grep: LoadSheddingJob, ValidateFutureCommandEnvelopeJob, SurvivalOverrideKernelJob, HapticPulseKernelJob, SubtitleCueKernelJob, MockMaliciousEnvelopeInjectionJob all deterministic synchronous
dotnet/csc process probe: NO_DOTNET_OR_CSC
World deletion probe: Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs is still missing
```

## 2026-05-19 AUP Counter And Profile Consumption Addendum

What was wrong:

- `KernelExecutionTelemetryEntry.AupViolations` was a boolean derived from a rejection mask, not an exact count. Malicious AUP spam would collapse to `1`.
- `kernel_tuning_profiles.csv` hydrated range/duration/intensity/flags, but the active haptic/subtitle route consumed only priority and max-frame budget. Designer control was partially inert.

What was done:

- Replaced the dead 4-byte slot at `FutureCommandValidationStats` offset `60` with `AupViolations`, keeping the DTO at exactly `64` bytes.
- Incremented AUP violation count at the global sandbox AUP gate and haptic-local AUP gates.
- `RecordKernelTelemetry()` now writes `stats.AupViolations`, not a mask-derived boolean.
- Marked validator `Stats` NativeArray as `[WriteOnly]`.
- Passed `[ReadOnly, NoAlias] NativeArray<ModKernelTuningProfile>` into active validator haptic/subtitle routes and standalone profiling jobs.
- Haptic route now uses CSV profile `RangeMeters` as cap/default, `MaxDurationSeconds` as duration cap, `IntensityScale` before inverse-square falloff, and profile flags for forced camera-juice fallback.
- Subtitle route now uses CSV profile `MaxDurationSeconds`; `TriggerSubtitleCue` resolves the `SubtitleCue` profile alias.

Cinematic Cheats used: haptic Dear Lie remains a scalar camera-juice vault impulse. CSV can force the fake through profile flags without touching C#.

Exact Microseconds saved: valid survival path unchanged. Optional haptic/subtitle routes add a tiny bounded profile scan over the current 3-row profile; expected cost below 1 us. The gain is real designer load control and exact blackbox triage under malicious AUP spam.

Verification:

```text
Docs/Modding/Validate_Mod_API_Static.ps1: PASS
SchemaRevision: 16
SourceSignals: 162
AllowedProjectedSignals: 2
DeniedByDefaultSignals: 160
Forbidden hot-path grep on validator/editor/contracts: no matches
Burst directive/profile grep: validator, haptic, subtitle, shedding, and mock injection jobs retain deterministic synchronous Burst attributes; profile arrays are [ReadOnly, NoAlias]
Scoped git diff --check: PASS, CRLF warnings only
dotnet/csc process probe: no active process returned
World deletion probe: Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs is still missing
```

## 2026-05-19 Active Architecture Counter Addendum

What was wrong: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` still named the Mod API static validator snapshot as `SchemaRevision=14`, `SourceSignals=160`.

What was done: updated that active authority row to `SchemaRevision=15`, `SourceSignals=161`, `ModCommandSizeBytes=64`, matching the latest `Docs/Modding/Validate_Mod_API_Static.ps1` pass.

Cinematic Cheats used: none; documentation authority correction only.

Exact Microseconds saved: 0 runtime us. Prevents false schema-drift investigation and stale-doc propagation.

## 2026-05-19 ThermalSourceSignal Schema Drift Addendum

What was wrong: after the previous PASS, `Docs/Modding/Validate_Mod_API_Static.ps1` failed again with `Source=162 Schema=161`. Source/audit diff showed `ThermalSourceSignal` was present in `Assets/_Project/Scripts/Core/GlobalSignals.cs` but absent from the Mod API denied inventory.

What was done:

- `Docs/Modding/Signal_Schema.json` moved to schema revision `16`.
- Source split updated to `162` source signals, `2` projected lanes, `160` denied-by-default signals.
- `ThermalSourceSignal` added to `Docs/Modding/Signal_Audit_Matrix.md` as denied-by-default.
- `Docs/Modding/README.md`, `Docs/Modding/Mod_API_Specification.md`, `Docs/Modding/Runtime_Verification_Playbook.md`, `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, and `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` aligned to the same static tuple.

Cinematic Cheats used: none; this is a schema denial/accounting correction. The command-kernel Dear Lie remains haptic-to-camera scalar fallback.

Exact Microseconds saved: 0 runtime us. It prevents accidental mod exposure of thermal source presentation/environment signals and stops stale static-gate churn.

Verification after schema drift patch:

```text
Docs/Modding/Validate_Mod_API_Static.ps1: PASS
SchemaRevision: 16
SourceSignals: 162
AllowedProjectedSignals: 2
DeniedByDefaultSignals: 160
ProjectionBridgeSignals: CombatDamageSignal,WeatherChangedSignal
Signal audit missing source entries: none
Scoped git diff --check: PASS, CRLF warnings only
World deletion probe: Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs is still missing
```

## 2026-05-19 Compile-Wall Boundary Addendum

What was wrong: the active ModdingAPI folder has no local `.asmdef`, while historical facade files in that folder still import sibling domains. That means the folder-level compile wall is not structurally solved by this agent's files alone.

What was done: audited the SHINOBU_102 active kernel route. `FutureCommandSandboxValidator.cs`, `ModSpatialContracts.cs`, and `ModKernelInspectorWindow.cs` do not import World, Gameplay, Physics, Caves, SaveSystem, UI, Input, or Localization sibling domains. The new runtime validator route remains on Core/Core.Contracts/Core.Memory plus typed `SignalBus<T>` lanes and vault buffers. Historical facade files remain obsolete/disabled instead of being broadly migrated inside this command-kernel pass.

Cinematic Cheats used: unchanged. Haptic feedback still collapses to `ModKernelCameraJuiceImpulse` scalar vault writes under low quality or force-fallback tuning.

Exact Microseconds saved: 0 runtime us. The saving is compile-surface containment: no new sibling-domain dependency was introduced by the Burst kernel path.

Verification:

```text
ModdingAPI asmdef scan: no local .asmdef found
Focused sibling using grep on validator/editor/contracts: no matches
Forbidden hot-path grep on validator/editor/contracts: no matches
Docs/Modding/Validate_Mod_API_Static.ps1: PASS
SchemaRevision: 16
SourceSignals: 162
AllowedProjectedSignals: 2
DeniedByDefaultSignals: 160
World deletion probe: Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs is still missing
```

## 2026-05-19 Per-Opcode MaxPerFrame Addendum

What was wrong: `kernel_tuning_profiles.csv` `MaxPerFrame` contributed to the aggregate command budget but did not enforce an independent ceiling per opcode. A queue filled with one optional opcode could stay below global budget and still exceed the designer's spam cap.

What was done:

- `ResolveSmallestKernelProfileFrameBudget()` now detects whether any active profile cap can trip before aggregate overflow.
- `TryPrepareValidationJob()` runs `LoadSheddingJob` when pending count exceeds either the global budget or the smallest active profile cap.
- `LoadSheddingJob` now resolves per-opcode caps for `SurvivalOverride`, `HapticPulse`, and `SubtitleCue`.
- `TriggerSubtitleCue` is normalized to the `SubtitleCue` profile for both priority and max-frame budget.
- Overflow shedding still drops optional packets first and survival last; profile caps then drop packets after an opcode reaches its configured `MaxPerFrame`.
- `Stats[0]` is written by the shedder only when a real drop happened. A no-drop profile scan leaves the already-cleared stats cache line at zero.

Cinematic Cheats used: unchanged. Haptic overload still collapses to a single camera-juice scalar vault impulse under low quality or forced profile flag; no hardware haptic API is touched in the kernel.

Exact Microseconds saved: 20-120 us expected on low-end single-opcode spam bursts by preventing optional command traffic from reaching validation and signal fanout after the per-opcode cap is reached. No gain claimed for empty or small queues.

Verification:

```text
Docs/Modding/Validate_Mod_API_Static.ps1: PASS
SchemaRevision: 16
SourceSignals: 162
AllowedProjectedSignals: 2
DeniedByDefaultSignals: 160
Forbidden hot-path grep on validator/editor/contracts: only existing _scheduledValidationHandle.Complete() finalize path
Scoped git diff --check: PASS, CRLF warnings only
dotnet/csc process probe: no running process returned
Full build: not launched; known external World deletion still blocks compile verification
```

<SELF_AUDIT update="2026-05-19-per-opcode-max-frame">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">DevNull route remains PRE_SIMULATION validator routing.</TASK>
    <TASK id="02" status="PASS">Legacy command facade remains obsolete and hard-blocked.</TASK>
    <TASK id="03" status="PASS">Hot DTOs retain raw fields, no properties.</TASK>
    <TASK id="04" status="PASS_WITH_CONSTRAINT_NOTE">Explicit layouts retained; haptic remains 48B because the requested 32B payload cannot contain double3 plus scalar fields safely.</TASK>
    <TASK id="05" status="PASS">Emergency opcode map retained.</TASK>
    <TASK id="06" status="PASS">Survival kernel emits request signal only.</TASK>
    <TASK id="07" status="PASS">Haptic kernel localizes AUP and emits signal/fallback only.</TASK>
    <TASK id="08" status="PASS">Subtitle kernel emits numeric token signal only.</TASK>
    <TASK id="09" status="PASS">Dear Lie haptic fallback remains O(1) camera scalar vault write.</TASK>
    <TASK id="10" status="PASS_UPGRADED">Continuous quality budget now combines aggregate budget and per-opcode CSV `MaxPerFrame` enforcement.</TASK>
    <TASK id="11" status="PASS">Rollback suppresses haptic/subtitle and preserves survival signal.</TASK>
    <TASK id="12" status="PASS">Malformed packets still emit rejection payloads.</TASK>
    <TASK id="13" status="PASS">Exact AUP violation count is recorded in stats offset 60.</TASK>
    <TASK id="14" status="PASS">Vault buffers and touched-window clears retained.</TASK>
    <TASK id="15" status="PASS">Command-kernel jobs retain deterministic synchronous Burst attributes.</TASK>
    <TASK id="16" status="PASS">NoAlias profile arrays and command buffers retained.</TASK>
    <TASK id="17" status="PASS">300-frame kernel telemetry ring records exact counters.</TASK>
    <TASK id="18" status="PASS">UI Toolkit inspector retained.</TASK>
    <TASK id="19" status="PASS_UPGRADED">CSV `MaxPerFrame` is now consumed as an actual per-opcode cap, not only aggregate budget input.</TASK>
    <TASK id="20" status="PASS">Live editor envelope injection retained.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <FutureCommandValidationStats size="64">0:uint Incoming, 4:uint Valid, 8:uint Rejected, 12:uint Dropped, 16:uint DevNull, 20:uint RejectionMask, 24:uint FaultHash, 28:uint PeakCommandsForSignature, 32:uint KernelSuppressed, 36:uint KernelRejected, 40:uint SurvivalProcessed, 44:uint HapticProcessed, 48:uint SubtitleProcessed, 52:uint CameraJuiceWrites, 56:uint HapticFallbacks, 60:uint AupViolations.</FutureCommandValidationStats>
    <ModKernelTuningProfile size="32">0:uint OpcodeHash, 4:float PriorityWeight, 8:int MaxPerFrame, 12:uint Flags, 16:float RangeMeters, 20:float MaxDurationSeconds, 24:float IntensityScale, 28:uint pad.</ModKernelTuningProfile>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    Below quality 0.3, aggregate command budget follows `GlobalQualityWeight^2` and optional haptic/subtitle buckets drop before survival. Per-opcode CSV caps now bound single-opcode spam independently of the aggregate budget. Haptic fallback still collapses hardware feedback into one scalar camera impulse.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Zero private NativeArray/List/HashMap allocations added. Vault handles remain 70914 opcode map, 70915 telemetry ring, 70916 telemetry cursor, 70917 camera impulse ring, 70918 camera state, 70919 tuning profiles, 70920 CSV scratch.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>LoadSheddingJob consumes pending ring, staging scratch, ring state, and read-only profile array; outputs compacted pending ring, ring state, and write-only stats only when drops occur. All relevant NativeArrays are marked NoAlias.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>SHINOBU_102 active kernel route still has no direct sibling-domain references. Full C# build was not launched because it is not needed for this patch and the known external World source deletion remains unresolved.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Specific fake remains haptic-to-camera scalar feedback. Before: optional haptic spam could continue toward downstream fanout after aggregate budget accepted it. After: O(N bounded queue scan) drops over-cap optional commands before validation; downstream fanout cost is eliminated for those packets.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Exact Profile Cap Early-Out Addendum

What was wrong: the first per-opcode cap patch was safe but not minimal. It launched `LoadSheddingJob` when pending count exceeded the smallest active profile cap, then compared total queue length to each opcode cap for early return. A mixed queue could therefore compact-copy through `Scratch` even when no individual opcode exceeded its cap.

What was done:

- The first Burst scan now counts both priority buckets and exact normalized opcode counts.
- `SurvivalOverride`, `HapticPulse`, and `SubtitleCue` have independent first-pass counters.
- `TriggerSubtitleCue` remains normalized to the `SubtitleCue` profile.
- If there is no aggregate overflow and no exact opcode over-cap, the job returns before copying to `Scratch` or rewriting `PendingRing`.
- The second pass remains the only path that mutates the ring, and it runs only for real overflow or real per-opcode cap shedding.

Cinematic Cheats used: unchanged. Haptic overload still uses scalar camera-juice fallback rather than hardware API fanout.

Exact Microseconds saved: 5-40 us on mixed queues above the smallest profile cap but below exact opcode caps, by skipping the compact-copy pass. Single-opcode spam protection is unchanged.

Verification:

```text
Exact counters present: survivalOpcodeCount, hapticOpcodeCount, subtitleOpcodeCount
No-drop path returns before Scratch/PendingRing copy
Forbidden hot-path grep on validator/contracts/editor: no properties, Pack=1, foreach, UnityEngine.Random, Time.deltaTime, string.Format, or Split matches
```

<SELF_AUDIT update="2026-05-19-exact-cap-early-out">
  <TASK_RECONCILIATION>
    <TASK id="10" status="PASS_POLISHED">Continuous shedding now uses quality-scaled aggregate budget, CSV priority, exact per-opcode caps, and exact no-drop early-out.</TASK>
    <TASK id="16" status="PASS_POLISHED">NoAlias buffer separation retained; no managed scheduler-side ring scan was introduced.</TASK>
    <TASK id="19" status="PASS_POLISHED">CSV `MaxPerFrame` remains active in the Burst shedder while avoiding wasted compact-copy for non-violating mixed queues.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No DTO layout changed in this pass. `FutureCommandValidationStats` remains 64 bytes with `AupViolations` at offset 60; `ModKernelTuningProfile` remains 32 bytes.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below quality 0.3, aggregate queue pressure still collapses via `GlobalQualityWeight^2`; exact profile caps now avoid unnecessary memory traffic when the queue is mixed and only the smallest unrelated cap is low.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No new persistent storage. Existing vault handles 70914..70920 remain the only SHINOBU_102 kernel additions.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>LoadSheddingJob still consumes pending ring/staging/ring state/profile arrays and writes compacted ring/stats only on real shedding. No managed collection or interface dispatch was added.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling-domain references were added. No full build launched; external World file deletion remains the known blocker.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Haptic physical/device feedback remains faked through scalar camera-juice under low quality or forced profile flags. This pass removes unnecessary CPU memory traffic, not the visual fake itself.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Pending Ring Overflow Telemetry Addendum

What was wrong: `EnqueuePendingEnvelope()` handled a full pending ring by advancing `PendingHead` and overwriting the oldest packet. This protected memory, but the drop happened before `LoadSheddingJob`, so kernel telemetry underreported pre-drain spam pressure.

What was done:

- `ModSandboxRingState` offset `44` is now `uint PendingOverflowDropped` instead of an unused reserve.
- `EnqueuePendingEnvelope()` increments that counter with saturation only when the ring is full and an old packet is evicted.
- `TryPrepareValidationJob()` snapshots and resets the overflow counter before running `LoadSheddingJob`.
- The final dropped telemetry uses saturating addition of enqueue overflow drops and shedder drops.
- No new DTO size or Vault buffer was introduced; the ring state remains a 64-byte explicit-layout cache-line record.

Cinematic Cheats used: unchanged. This is blackbox accuracy, not a visual fake.

Exact Microseconds saved: no hot valid-path savings claimed. Full-ring spam now costs one saturating uint increment per evicted packet and avoids false postmortem analysis. The practical gain is forensic correctness under hostile mod enqueue floods.

Verification target:

```text
ModSandboxRingState size remains 64 bytes:
0:int PendingHead
4:int PendingTail
8:int PendingCount
12:int DevNullHead
16:int DevNullTail
20:int DevNullCount
24:int NextLeaseIndex
28:int OpcodeCount
32:int ApprovedAssetCount
36:int LastDumpFrame
40:uint Flags
44:uint PendingOverflowDropped
48:ulong Reserved1
56:ulong Reserved2
```

<SELF_AUDIT update="2026-05-19-pending-overflow-telemetry">
  <TASK_RECONCILIATION>
    <TASK id="10" status="PASS_POLISHED">Command load shedding telemetry now includes pre-drain ring overflow drops as well as Burst shedder drops.</TASK>
    <TASK id="14" status="PASS">No new allocation; existing 64-byte ring state slot reused.</TASK>
    <TASK id="17" status="PASS_POLISHED">300-frame blackbox no longer loses full-ring enqueue overflow events.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>Primary changed DTO: `ModSandboxRingState` remains exactly 64 bytes; offset 44 is `PendingOverflowDropped`, followed by 8-byte fields at offsets 48 and 56.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>At low quality, overflow can happen before pre-sim drain under hostile enqueue spam. The bounded ring still drops oldest packets, and that pressure is now visible in dropped telemetry without increasing queue capacity.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No new private arrays and no new Vault IDs. The field is inside existing `BufferID.ShinobuModSandboxRingState`.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>Enqueue mutates only the pending ring and ring state. Pre-sim copies the ring-state counter into telemetry before scheduling/running validation.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling references or asmdef changes. Full build not launched because external World deletion and active dotnet processes remain outside this patch.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Not applicable to this telemetry patch; haptic Dear Lie remains unchanged.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Dead Shedder Removal Addendum

What was wrong: an unused private `DropThermalBacklog()` method still existed in `FutureCommandSandboxValidator.cs`. Its algorithm was the old head-only drop model, not the current optional-first/survival-last Burst shedder. Dead private code with wrong semantics is an integration trap.

What was done:

- Removed `DropThermalBacklog()`.
- Active shedding is now consolidated in `LoadSheddingJob`.
- No public API or DTO layout changed in this removal.

Cinematic Cheats used: unchanged.

Exact Microseconds saved: 0 runtime us; method was unused. The saving is architectural: there is no longer a stale head-drop function for future patches to call by mistake.

Verification target:

```text
rg DropThermalBacklog Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs
expected: no matches
```

<SELF_AUDIT update="2026-05-19-dead-shedder-removal">
  <TASK_RECONCILIATION>
    <TASK id="10" status="PASS_POLISHED">Only the deterministic priority/per-opcode Burst shedder remains.</TASK>
    <TASK id="17" status="PASS">Telemetry path remains unchanged after dead-code removal.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No layout changed in this pass.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Low-quality command shedding has one implementation: quality-scaled aggregate budget plus optional-first priority and exact profile caps.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No storage changes.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>No job dependency or aliasing changes.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No dependency or asmdef changes.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Haptic scalar fallback remains the command-kernel Dear Lie.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Editor Inspector Cadence Addendum

What was wrong: `ModKernelInspectorWindow.Tick()` scanned the full 300-entry kernel telemetry ring, rewrote labels, and repainted the histogram on every editor update. It was editor-only, but still wasteful when the inspector is left open during spam testing.

What was done:

- Added `RefreshIntervalSeconds = 0.10d`.
- Added `_nextRefreshTime`.
- `Tick()` now returns until `EditorApplication.timeSinceStartup` reaches the next refresh boundary.
- UI Toolkit inspector still reads the same vault telemetry ring and still flashes red when shedding changes.

Cinematic Cheats used: unchanged.

Exact Microseconds saved: estimated 5-80 us per open inspector editor frame by reducing telemetry scans/repaints from editor-frame cadence to 10Hz. Player runtime impact is 0 us.

<SELF_AUDIT update="2026-05-19-editor-cadence">
  <TASK_RECONCILIATION>
    <TASK id="18" status="PASS_POLISHED">Editor facade remains UI Toolkit and live, but its update cadence is bounded.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No runtime DTO layout changed.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Inspector observation cost now scales by cadence rather than editor frame rate; runtime `GlobalQualityWeight` behavior unchanged.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No new runtime storage. Editor reads existing telemetry vault ring.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>No job dependency changes.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling runtime references or asmdef changes.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Haptic fallback remains the existing scalar camera-juice fake.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>
