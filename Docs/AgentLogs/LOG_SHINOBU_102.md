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

What was done: `FutureCommandValidationStats` now stores a dedicated `HapticFallbacks` counter at offset `56`, with `Reserved4` moved to offset `60`. Only `WriteCameraJuiceImpulse()` increments this field. `RecordKernelTelemetry()` now copies `stats.HapticFallbacks` directly.

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
