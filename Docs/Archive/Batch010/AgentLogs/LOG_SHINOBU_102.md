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

## 2026-05-19 Mixed Thermal Shed Telemetry Mask Addendum

What was wrong: mixed shed+process frames kept the thermal dropped count, but lost the `ThermalShed` reason flag because `statsBuffer` is intentionally cleared between `LoadSheddingJob` and `ValidateFutureCommandEnvelopeJob`.

What was done:

- Added a finalization-local `telemetryStats` copy.
- ORed `FutureCommandRejectReason.ThermalShed` into telemetry flags whenever `validationState.ThermalDropped != 0`.
- Changed dropped-count merge from raw addition to saturating addition.
- Re-ran `Docs/Modding/Validate_Mod_API_Static.ps1`: PASS, schema revision 16, `KernelTuningProfileCount=3`.
- Re-ran forbidden hot-path grep: no matches.
- Re-ran scoped `git diff --check`: PASS with CRLF warnings only.

Cinematic Cheats used: unchanged.

Exact Microseconds saved: no hot Burst job delta. Finalization adds one branch and one OR; this buys correct 300-frame forensic reconstruction of thermal shedding.

<SELF_AUDIT update="2026-05-19-mixed-thermal-shed-telemetry-mask">
  <TASK_RECONCILIATION>
    <TASK id="10" status="PASS_POLISHED">Thermal shedding now preserves both count and reason flag on mixed work frames.</TASK>
    <TASK id="17" status="PASS_POLISHED">Kernel telemetry flags now include `ThermalShed` whenever `ShedByThermal` is nonzero.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No DTO layout changed. `KernelExecutionTelemetryEntry` remains 64 bytes with `ShedByThermal@24` and `Flags@56`.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Low-quality frames that shed optional commands and still process survival/standard work now retain thermal cause flags in the blackbox ring. Workload math is unchanged.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No new buffers. Existing telemetry ring ID `70915` and stats buffer route remain unchanged.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>No JobHandle or NoAlias changes. Merge occurs after the scheduled validation job is complete or after synchronous `Run()` finalization.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No dependency or assembly change.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Dear Lie unchanged; telemetry now reports thermal pressure that triggers command shedding/fallback decisions.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Optional Command Shed Bucket Proof Addendum

What was wrong: `LoadSheddingJob.ResolveDropPriority()` used a hard-coded `0.30f` optional threshold. The checked-in `HapticPulse` profile is `0.35`, so haptic spam was treated as standard work instead of being shed with subtitle spam first.

What was done:

- Added `FutureCommandSandboxConstants.KernelOptionalPriorityMax = 0.50f` and `KernelSurvivalPriorityMin = 0.90f`.
- Replaced the magic shedder thresholds with those constants.
- Extended `Validate_Mod_API_Static.ps1` to parse the constants and prove checked-in `HapticPulse`/`SubtitleCue` priorities stay in the optional bucket while `SurvivalOverride` stays in the protected bucket.
- Re-ran `Docs/Modding/Validate_Mod_API_Static.ps1`: PASS, schema revision 16, `KernelTuningProfileCount=3`.
- Re-ran forbidden hot-path grep on `FutureCommandSandboxValidator.cs` and `ModKernelInspectorWindow.cs`: no matches.
- Re-ran scoped `git diff --check`: PASS with CRLF warnings only.

Cinematic Cheats used: unchanged. The haptic Dear Lie remains the scalar camera-juice fallback; this pass makes sure thermal pressure drops haptic spam before standard packets instead of preserving it as standard work.

Exact Microseconds saved: no new hot-path ALU cost. Expected low-end protection remains the documented 50-300 us on hostile command bursts, now actually applied to checked-in haptic priority.

<SELF_AUDIT update="2026-05-19-optional-command-shed-bucket-proof">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Injection point unchanged in PRE_SIMULATION.</TASK>
    <TASK id="02" status="PASS">Legacy lanes remain quarantined.</TASK>
    <TASK id="03" status="PASS">No DTO properties added.</TASK>
    <TASK id="04" status="PASS">No layout change or Pack=1 added.</TASK>
    <TASK id="05" status="PASS">Emergency opcode map unchanged.</TASK>
    <TASK id="06" status="PASS">Survival signal route unchanged.</TASK>
    <TASK id="07" status="PASS">Haptic signal/fallback route unchanged.</TASK>
    <TASK id="08" status="PASS">Subtitle numeric signal route unchanged.</TASK>
    <TASK id="09" status="PASS">Haptic Dear Lie unchanged.</TASK>
    <TASK id="10" status="PASS_POLISHED">Checked-in haptic/subtitle profiles are now statically proven to be optional shed work.</TASK>
    <TASK id="11" status="PASS">Rollback suppression unchanged.</TASK>
    <TASK id="12" status="PASS">Rejection telemetry unchanged.</TASK>
    <TASK id="13" status="PASS">AUP validation unchanged.</TASK>
    <TASK id="14" status="PASS">Vault allocation route unchanged.</TASK>
    <TASK id="15" status="PASS">Burst attributes unchanged.</TASK>
    <TASK id="16" status="PASS">NoAlias fields unchanged.</TASK>
    <TASK id="17" status="PASS">Telemetry ring unchanged.</TASK>
    <TASK id="18" status="PASS">Inspector unchanged.</TASK>
    <TASK id="19" status="PASS">CSV parser unchanged in this pass; static priority proof extended.</TASK>
    <TASK id="20" status="PASS">Injection gizmo unchanged.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No DTO layout changed. `ModKernelTuningProfile` remains 32 bytes and all 64-byte telemetry/stats records remain cache-line sized.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below `GlobalQualityWeight=0.3`, the polynomial global budget still collapses command throughput. With `KernelOptionalPriorityMax=0.50`, checked-in haptic and subtitle traffic are both optional bucket drops before standard work; survival remains protected by `KernelSurvivalPriorityMin=0.90`.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No new buffers; existing IDs `70914..70920` remain unchanged.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>No job graph or aliasing change. `LoadSheddingJob` still reads/writes the same vault arrays with existing `[NoAlias]` annotations.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling dependency or asmdef change.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Dear Lie remains scalar camera-juice fallback for low-quality haptics. The priority proof ensures the fake is not undermined by preserving default haptics as standard command work.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Semantic Kernel Tuning Range And Static Facade Drift Addendum

What was wrong:

- `TryParseKernelTuningCsvLine()` had strict token parsing but still used clamp/saturate as a semantic fallback. Rows such as `priority=-1`, `max_per_frame=0`, `range=-5`, or `max_duration=999` could mutate the live profile vault instead of failing closed.
- `Validate_Mod_API_Static.ps1` failed before reaching the command-kernel CSV checks because the Modding docs/schema still referenced `HectonAPI.Localization.InjectTable` even though active source now exposes only the rejected `InjectBabelEnvelope(ReadOnlySpan<byte>)` seam.

What was done:

- Added runtime semantic bounds for kernel tuning policy before DTO creation: priority `[0,1]`, max-per-frame `>=1`, range `[1,100000]`, duration `[0.01,30]`, intensity scale `>=0`.
- Removed parser-side clamp/saturate fallback in `ModKernelTuningProfile` construction; the DTO receives already-validated policy.
- Added static validator range helpers and enforced the same semantic bounds on checked-in `kernel_tuning_profiles.csv`.
- Updated Modding static schema/audit docs from `InjectTable` to `InjectBabelEnvelope`, preserving disabled runtime dictionary localization injection.
- Re-ran `Docs/Modding/Validate_Mod_API_Static.ps1`: PASS, schema revision 16, `KernelTuningProfileCount=3`, `PublicContentMethodCount=14`.
- Focused grep for `InjectTable` in active Modding source/docs returned no matches.
- Re-ran scoped `git diff --check`: PASS with CRLF warnings only.

Cinematic Cheats used: unchanged. The haptic Dear Lie still collapses hardware fanout into scalar camera-juice impulse under low quality or explicit profile flag; this pass protects the CSV authority that controls when that fake is allowed.

Exact Microseconds saved: runtime hot path 0 us. Cold reload adds bounded range checks only. The protected saving remains the command-spam guard: malformed tuning can no longer remove per-opcode caps and expose weak CPUs to the bounded 50-300 us hostile queue cost.

<SELF_AUDIT update="2026-05-19-semantic-tuning-range-and-static-facade-drift">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">DevNull routing remains traced through the PRE_SIMULATION FutureCommandSandboxValidator route.</TASK>
    <TASK id="02" status="PASS">Legacy command surfaces remain obsolete/quarantined; no legacy dispatch was reopened.</TASK>
    <TASK id="03" status="PASS">Hot DTO/signal structs remain public-field based; no property setters added.</TASK>
    <TASK id="04" status="PASS">ARM64 explicit layouts unchanged; no Pack=1 added.</TASK>
    <TASK id="05" status="PASS">Emergency opcode map remains bootstrap/mock only.</TASK>
    <TASK id="06" status="PASS">SurvivalOverride still emits typed signal and does not mutate survival owner state directly.</TASK>
    <TASK id="07" status="PASS">HapticPulse still performs local AUP math and emits haptic signal or fallback scalar only.</TASK>
    <TASK id="08" status="PASS">SubtitleCue remains numeric-token-only.</TASK>
    <TASK id="09" status="PASS">Dear Lie haptic scalar fallback unchanged and protected by profile validation.</TASK>
    <TASK id="10" status="PASS">Continuous quality-weight load shedding unchanged; malformed CSV can no longer disable profile caps.</TASK>
    <TASK id="11" status="PASS">Rollback suppression semantics unchanged: haptic/subtitle suppressed, survival preserved.</TASK>
    <TASK id="12" status="PASS">Kernel rejection telemetry unchanged.</TASK>
    <TASK id="13" status="PASS">AUP precision/range validation unchanged.</TASK>
    <TASK id="14" status="PASS">Vault-backed buffers unchanged; no private persistent native allocation added.</TASK>
    <TASK id="15" status="PASS">Burst directive policy unchanged.</TASK>
    <TASK id="16" status="PASS">NoAlias job field policy unchanged.</TASK>
    <TASK id="17" status="PASS">300-frame telemetry ring unchanged.</TASK>
    <TASK id="18" status="PASS">UI Toolkit inspector unchanged.</TASK>
    <TASK id="19" status="PASS_POLISHED">CSV tuning now fails closed on syntactically valid but semantically invalid command policy.</TASK>
    <TASK id="20" status="PASS">Live command injection gizmo unchanged.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No DTO layout changed. `ModKernelTuningProfile` remains 32 bytes: `OpcodeHash@0` 4, `PriorityWeight@4` 4, `MaxPerFrame@8` 4, `Flags@12` 4, `MaxDurationSeconds@16` 4, `RangeMeters@20` 4, `IntensityScale@24` 4, `Reserved@28` 4. Total 32 bytes, 16-byte multiple. `FutureCommandValidationStats` and `KernelExecutionTelemetryEntry` remain 64-byte cache-line records.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below `GlobalQualityWeight=0.3`, optional haptic/subtitle traffic remains shed by the polynomial budget and haptic output collapses to scalar camera-juice fallback where policy requires it. The new semantic CSV gate prevents bad authoring from removing low-tier caps; high/ultra still raise profile values inside validated envelope bounds.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No new buffers. Existing requested IDs: `70914` opcode map, `70915` telemetry ring, `70916` telemetry cursor, `70917` camera impulse ring, `70918` camera state, `70919` tuning profiles, `70920` CSV scratch. Zero private array allocations added.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>No job graph changes. Runtime validation still consumes resolved vault arrays and writes through SignalBus writers with existing `[NoAlias]` fields. CSV range validation is cold/editor and does not create JobHandle dependencies.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling runtime assembly reference was added. The Modding static docs were aligned to current Modding source only; no Localization runtime owner code or asmdef was touched.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Specific fake remains haptic hardware feedback collapsed to a bounded scalar camera-juice impulse under thermal/quality pressure. Complexity before policy protection: malformed CSV could permit optional command fanout proportional to hostile queue pressure. After protection: optional work remains bounded by validated per-opcode caps and quality-scaled shedding.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 CSV Authority And Vault Guard Addendum

What was wrong:
- `TryReloadAllowedOpcodesCsvFromDisk()` referenced `Docs/Modding/allowed_opcodes.csv`, but that file was missing, leaving the editor reload facade permanently dependent on emergency bootstrap opcodes.
- Allowed-opcode and tuning-profile reloads read only the first 16KB scratch prefix for oversized files. A valid prefix could mutate live policy while silently dropping the tail.
- `ModKernelTuningProfile` was passed into `LoadSheddingJob` and `ValidateFutureCommandEnvelopeJob`, but scheduler/self-audit preflight did not require that vault buffer to resolve.

What was done:
- Added `Docs/Modding/allowed_opcodes.csv` with the 12 exact hex hashes declared by `FutureCommandOpcodes`.
- Added fail-closed file length guards for both CSV reload paths before scratch reads.
- Added `kernelProfiles.IsCreated` checks to `TryPrepareValidationJob()` and `RunSelfAudit()`.
- Extended `Docs/Modding/Validate_Mod_API_Static.ps1` so the static gate parses `FutureCommandOpcodes` and proves `allowed_opcodes.csv` is exact and duplicate-free.

Cinematic Cheats used:
- Kept allowlist/tuning authoring as CSV hydrated into unmanaged vault buffers instead of building a managed editor/runtime registry. The hot Burst path continues to consume fixed NativeArrays only.

Exact Microseconds saved:
- Runtime hot path: 0 us added.
- Fault prevention: prevents bad CSV reload from removing per-opcode caps, preserving the existing 50-300 us hostile-spam protection on weak CPU/GPU pairs.

Verification:
- `Docs/Modding/Validate_Mod_API_Static.ps1`: PASS, `SchemaRevision=16`, `FutureCommandAllowedOpcodeCount=12`.
- `git diff --check` scoped to SHINOBU_102 files: PASS with CRLF warnings only.
- Forbidden hot-path grep on validator/editor: no matches.
- Full build intentionally not launched; known external World deletion remains the compile-wall blocker.

<SELF_AUDIT agent_id="SHINOBU_102" loop="25">
  <task_reconciliation>
    <task id="01" status="PASS">DevNull injection point unchanged; new CSV source only affects allowlist hydration.</task>
    <task id="02" status="PASS">Legacy command surface remains disabled/obsolete.</task>
    <task id="03" status="PASS">No hot DTO properties added.</task>
    <task id="04" status="PASS">No Pack=1 or layout change added.</task>
    <task id="05" status="PASS">Emergency mock remains bootstrap-only; `allowed_opcodes.csv` is now the explicit editor-reload source.</task>
    <task id="06" status="PASS">Survival kernel unchanged.</task>
    <task id="07" status="PASS">Haptic kernel unchanged; profile vault guard now hard-fails missing profile storage.</task>
    <task id="08" status="PASS">Subtitle kernel unchanged; profile vault guard now hard-fails missing profile storage.</task>
    <task id="09" status="PASS">Dear Lie fallback unchanged.</task>
    <task id="10" status="PASS">Load shedding keeps profile caps and now depends on a required resolved profile buffer.</task>
    <task id="11" status="PASS">Rollback behavior unchanged.</task>
    <task id="12" status="PASS">Rejection telemetry unchanged.</task>
    <task id="13" status="PASS">AUP validation unchanged.</task>
    <task id="14" status="PASS">No new persistent local data; CSV still uses vault scratch.</task>
    <task id="15" status="PASS">No Burst directive regression.</task>
    <task id="16" status="PASS">NoAlias profile usage remains in jobs; scheduler now verifies the buffer exists.</task>
    <task id="17" status="PASS">Telemetry unchanged; bad CSV no longer erases policy before proof.</task>
    <task id="18" status="PASS">Editor facade reload now has an actual allowed-opcode CSV file.</task>
    <task id="19" status="PASS">Tuning CSV oversized reloads fail closed before truncation.</task>
    <task id="20" status="PASS">Injection gizmo route unchanged.</task>
  </task_reconciliation>
  <struct_layout_verification>
    <dto name="FutureCommandEnvelope" size="64">Offsets unchanged: OpcodeHash 0, ModderSignature 4, TargetAUP 8, PayloadData 32, IntegrityHash 48, _pad0 56.</dto>
    <dto name="ModKernelTuningProfile" size="32">Offsets unchanged: OpcodeHash 0, PriorityWeight 4, MaxPerFrame 8, Flags 12, MaxDurationSeconds 16, RangeMeters 20, IntensityScale 24, Reserved 28.</dto>
    <dto name="ModSandboxRingState" size="64">Offsets unchanged; PendingOverflowDropped remains offset 44.</dto>
  </struct_layout_verification>
  <scalability_curve>Below GlobalQualityWeight 0.3, load shedding continues to collapse optional haptic/subtitle throughput first; bad CSV authoring can no longer remove those caps through truncation or missing allowlist source.</scalability_curve>
  <h_phi_vault_status>No private NativeArray allocations added. Existing buffers used: 70914 opcode map, 70915 telemetry ring, 70916 telemetry cursor, 70917 camera impulse ring, 70918 camera state, 70919 tuning profiles, 70920 CSV scratch.</h_phi_vault_status>
  <pointer_aliasing_dependency_graph>KernelProfiles remains `[ReadOnly, NoAlias]`; scheduler consumes pending ring/state/stats/profile buffers and outputs `ValidateFutureCommandEnvelopeJob` JobHandle through existing scheduled validation state.</pointer_aliasing_dependency_graph>
  <compile_guard>No sibling-domain assembly reference introduced; changed files stay in ModdingAPI runtime/editor docs.</compile_guard>
  <dear_lie_confirmation>The reload path is still a CSV-to-vault policy fake, not a managed registry or runtime reflection scan. Complexity stays O(bytes) cold, O(1)/bounded NativeArray scans hot.</dear_lie_confirmation>
</SELF_AUDIT>

## 2026-05-19 CSV Change-Control Visibility Addendum

What was wrong:
- The new allowed-opcode CSV and existing kernel-tuning CSV were not listed in the Modding contract index or change-control checklist.
- Static validation required `allowed_opcodes.csv` content equality but did not yet require the CSV files to be discoverable through the documented workflow.

What was done:
- Added `allowed_opcodes.csv` and `kernel_tuning_profiles.csv` to `Docs/Modding/README.md`.
- Added a change-control matrix row for future command envelope allowlist/kernel tuning CSV edits.
- Added both CSV files to checklist audit files.
- Extended `Validate_Mod_API_Static.ps1` to require the tuning CSV path and index/checklist links for both CSVs.

Cinematic Cheats used:
- Documentation remains a static gate instead of runtime reflection/discovery. The engine still consumes vault-hydrated unmanaged data; humans get visible CSVs and the gate proves they exist.

Exact Microseconds saved:
- Runtime hot path: 0 us added.
- Developer loop: avoids manual hunt for hidden CSV policy files; no frame-time claim.

Verification:
- `Docs/Modding/Validate_Mod_API_Static.ps1`: PASS, `FutureCommandAllowedOpcodeCount=12`.
- `git diff --check` scoped to touched Modding docs/source: PASS with CRLF warnings only.
- Full build intentionally not launched.

## 2026-05-19 Exact CSV Read Addendum

What was wrong:
- CSV reloads rejected empty/oversized files, but still trusted a single `FileStream.Read(Span<byte>)` call as if it filled the requested span.
- A short read from a shared file could feed a valid prefix into the transactional parser and mutate live allowlist/profile buffers without the missing tail rows.

What was done:
- Added `read != readLength` fail-closed checks to both `TryReloadAllowedOpcodesCsvFromDisk()` and `TryReloadKernelTuningProfilesCsvFromDisk()`.

Cinematic Cheats used:
- None beyond the existing CSV-to-vault authoring path. This is a correctness guard for the cold facade.

Exact Microseconds saved:
- Runtime hot path: 0 us added.
- Fault prevention: keeps existing command-budget protections from being weakened by a partial authoring read.

Verification:
- `Docs/Modding/Validate_Mod_API_Static.ps1`: PASS.
- `git diff --check` scoped to touched files: PASS with CRLF warnings only.
- Source grep confirms both reload paths contain `read != readLength`.

## 2026-05-19 Profile Lookup Collapse Addendum

What was wrong:
- The scheduler resolved `ModKernelTuningProfile` once, then profile budget helpers resolved the same vault handle again in PRE_SIMULATION.

What was done:
- `ResolveKernelProfileFrameBudget()` and `ResolveSmallestKernelProfileFrameBudget()` now consume the already-resolved `NativeArray<ModKernelTuningProfile>`.

Cinematic Cheats used:
- No new fake. This is vault lookup hygiene; persistent private cache was rejected.

Exact Microseconds saved:
- Estimated below 1-3 us per scheduling frame on weak CPUs by removing duplicate vault handle resolution. Runtime allocations remain 0 B.

Verification:
- `Docs/Modding/Validate_Mod_API_Static.ps1`: PASS.
- `git diff --check` scoped to touched files: PASS with CRLF warnings only.
- Source grep confirms helper signatures and callsites use `kernelProfiles`.

## 2026-05-19 Tuning Lookup Collapse Addendum

What was wrong:
- Quality and tuning resolution each re-resolved the same tuning vault buffer in the scheduler path.

What was done:
- Added resolved-buffer overloads for tuning/quality and used one `NativeArray<FutureCommandSandboxTuning>` in snapshot, self-audit, and PRE_SIMULATION scheduling paths.

Cinematic Cheats used:
- No new fake. This removes redundant service lookup while preserving the same continuous `GlobalQualityWeight` math.

Exact Microseconds saved:
- Estimated below 1-3 us per command scheduling frame on weak CPUs. Runtime allocations remain 0 B.

Verification:
- `Docs/Modding/Validate_Mod_API_Static.ps1`: PASS.
- `git diff --check` scoped to touched files: PASS with CRLF warnings only.
- Source grep confirms resolved tuning arrays are passed into helper calls.

## 2026-05-19 Tuning Vault Fail-Fast Addendum

What was wrong:
- Active command validation could fall back to default tuning if the tuning vault buffer was missing, hiding a boot/vault fault.

What was done:
- Added `tuningBuffer.IsCreated` guards to `RunSelfAudit()` and `TryPrepareValidationJob()`.

Cinematic Cheats used:
- None. This is fail-closed policy authority enforcement.

Exact Microseconds saved:
- Valid hot path: 0 us change after lookup collapse.
- Failure-path protection: prevents ungoverned command processing if tuning policy is absent.

Verification:
- `Docs/Modding/Validate_Mod_API_Static.ps1`: PASS.
- `git diff --check` scoped to touched files: PASS with CRLF warnings only.
- Source grep confirms `!tuningBuffer.IsCreated` guards exist.

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

## 2026-05-19 Editor Subscription Hygiene Addendum

What was wrong: `ModKernelInspectorWindow.CreateGUI()` subscribed to `EditorApplication.update` with `+= Tick` and trusted `OnDisable()` for cleanup. UI Toolkit visual-tree rebuilds can call `CreateGUI()` again, producing duplicated editor polling if the old delegate was still present.

What was done:

- `CreateGUI()` now executes `EditorApplication.update -= Tick` before `+= Tick`.
- `_nextRefreshTime` resets to `0d` before the immediate `Tick()` call.
- Multiple inspector windows remain independent because the subscription hygiene is instance-local.

Cinematic Cheats used: unchanged.

Exact Microseconds saved: prevents accidental duplicate 10Hz telemetry scans and repaints per inspector instance after UI rebuild. Runtime player impact is 0 us.

<SELF_AUDIT update="2026-05-19-editor-subscription">
  <TASK_RECONCILIATION>
    <TASK id="18" status="PASS_POLISHED">Editor facade update subscription is now idempotent.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No DTO layout changed.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Inspector overhead no longer scales with accidental duplicate subscriptions.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No storage changes.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>No job graph changes.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No runtime dependency changes.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Haptic camera scalar fallback remains unchanged.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Self-Audit Overflow Probe Addendum

What was wrong: `RunSelfAudit()` proved malformed packet rejection, but not the exact AUP violation counter or the new pending-ring overflow telemetry. The self-audit did not cover the same blackbox facts now used for spam forensics.

What was done:

- Added `exactAupTelemetry = stats.AupViolations == 1u`.
- Added a local `ModSandboxRingState overflowProbe`.
- Filled only the local probe state to capacity and called `EnqueuePendingEnvelope()` once against the staging buffer.
- Required `PendingOverflowDropped == 1`, `PendingCount == staging.Length`, and `PendingHead == 1`.
- `RunSelfAudit()` now fails and dumps blackbox if malformed rejection, exact AUP telemetry, or overflow telemetry regresses.

Cinematic Cheats used: unchanged.

Exact Microseconds saved: 0 runtime us; cold self-audit only. This saves QA time by detecting telemetry blind spots before endurance spam runs.

<SELF_AUDIT update="2026-05-19-self-audit-overflow-probe">
  <TASK_RECONCILIATION>
    <TASK id="12" status="PASS_POLISHED">Self-audit still validates malformed packet rejection.</TASK>
    <TASK id="13" status="PASS_POLISHED">Self-audit now requires exact AUP violation count.</TASK>
    <TASK id="17" status="PASS_POLISHED">Self-audit now probes pending-ring overflow telemetry.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>`ModSandboxRingState` remains 64 bytes and the probe validates offset-44 overflow semantics indirectly through `PendingOverflowDropped`.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>No runtime curve change; this is cold proof for spam telemetry.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No new buffers; probe uses existing staging buffer and local stack state.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>No job graph changes.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No dependency changes.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Haptic scalar camera fallback remains unchanged.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 No-Work Kernel Telemetry Addendum

What was wrong: if `TryPrepareValidationJob()` drained zero envelopes, it could write generic sandbox telemetry and return before writing the command-kernel telemetry ring. A pure enqueue-overflow or shed-only frame could therefore be absent from the 300-frame command forge blackbox.

What was done:

- The no-drain path now creates a local zero-work `FutureCommandValidationStats`.
- `Dropped` and `RejectionMask` reflect `thermalDropped` when enqueue overflow or shedder drops occurred.
- `RecordKernelTelemetry()` is called with `elapsedTicks=0`, current quality, pending depth, and `ShedByThermal=thermalDropped`.
- No validation job is scheduled or run for zero envelopes.

Cinematic Cheats used: unchanged.

Exact Microseconds saved: no CPU saving claimed. This adds one 64-byte telemetry write on no-drain telemetry frames; expected cost below 1 us. It saves forensic time by making spam pressure visible.

<SELF_AUDIT update="2026-05-19-no-work-kernel-telemetry">
  <TASK_RECONCILIATION>
    <TASK id="17" status="PASS_POLISHED">Kernel blackbox now records no-drain drop-only frames.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No DTO layout changed.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Low-quality overflow/shed-only frames now appear in telemetry without running a zero-count validation job.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No new buffers; existing kernel telemetry ring is used.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>No job graph changes; no-work telemetry is a direct ring write.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No dependency changes.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Haptic scalar fallback remains unchanged.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Transactional Kernel CSV Tuning Addendum

What was wrong: `TryIngestKernelTuningProfilesCsv()` cleared the live vault profile buffer before proving that the full CSV was valid. A malformed row or an over-capacity file could leave the command kernels with erased or partially applied designer budgets.

What was done:

- Added `TryValidateKernelTuningProfilesCsv()`.
- Added `IsKernelTuningCsvMetadataLine()` for empty/comment/header rows.
- `TryIngestKernelTuningProfilesCsv()` now validates all real rows and profile capacity before `MemClearArray(profiles)`.
- The second pass writes profiles only after the first pass proves the file can be applied coherently.
- No persistent arrays, managed lists, `string.Split`, or gameplay allocations were added.

Cinematic Cheats used: unchanged. Haptic low-quality feedback still uses the scalar camera-juice fallback instead of hardware/API fanout.

Exact Microseconds saved: hot path remains 0 us. Cold reload adds one bounded CSV scan but prevents a bad editor tuning file from removing low-end command budgets and allowing optional spam into frame-critical lanes.

<SELF_AUDIT update="2026-05-19-transactional-kernel-csv">
  <TASK_RECONCILIATION>
    <TASK id="19" status="PASS_POLISHED">Kernel tuning CSV ingest is now fail-closed and transactional over the existing scratch span.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No DTO layout changed. `ModKernelTuningProfile` remains the existing 32-byte unmanaged profile DTO.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Low-quality protection survives bad CSV reloads because previous profile caps are preserved on parser failure. Middle/High/Ultra profile budgets apply only when the entire file validates.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No new buffers. Existing vault profile buffer `70919` and CSV scratch `70920` are reused.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>No job graph changes; this is a cold reload path before Burst jobs consume the profile array.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No runtime dependency or asmdef changes.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Haptic scalar camera fallback remains the command-kernel fake; this pass protects the designer caps that decide when it is used.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Static Gate Explicit Layout Addendum

What was wrong: `Docs/Modding/Validate_Mod_API_Static.ps1` still required `ModAupResponse` to use `LayoutKind.Sequential`. The source contract is now `LayoutKind.Explicit, Size = 64`, so the static gate failed before the schema size comparison.

What was done:

- Updated the validator regex to accept `LayoutKind.Sequential` or `LayoutKind.Explicit`.
- Kept the hard `Size = N` extraction and schema comparison.
- Re-ran the static Mod API validator: PASS, schema revision 16, `ModAupResponseSizeBytes=64`.

Cinematic Cheats used: none.

Exact Microseconds saved: runtime 0 us. Developer-gate false failure removed; ARM64 fixed-size layout proof remains intact.

<SELF_AUDIT update="2026-05-19-static-gate-explicit-layout">
  <TASK_RECONCILIATION>
    <TASK id="04" status="PASS_GUARD_POLISHED">Static validation now accepts explicit fixed-size public DTO layout instead of demanding sequential layout.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>`ModAupResponse` source is `LayoutKind.Explicit, Size=64`; static gate still compares source size to schema `64`.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>No runtime curve change.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No storage changes.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>No job graph changes.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>Static script change only; no sibling runtime references or asmdef changes.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Unchanged.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Transactional Allowed Opcode CSV Addendum

What was wrong: `TryIngestAllowedOpcodesCsv()` cleared the live opcode record buffer before proving the CSV was valid. Bad input could partially apply an allowlist or replace current policy with emergency mock opcodes.

What was done:

- Added `TryValidateAllowedOpcodesCsv()`.
- Added metadata/header handling for empty, comment, `opcode`, and `opcodehash` lines.
- Added duplicate opcode hash rejection before live mutation.
- `TryIngestAllowedOpcodesCsv()` now clears and rewrites `opcodeRecords` only after the full source proves valid and within capacity.
- Removed the bad-file fallback-to-emergency behavior from this ingest path; emergency opcodes remain bootstrap/mock authority only.

Cinematic Cheats used: unchanged. Haptic low-quality feedback still uses scalar camera-juice fallback.

Exact Microseconds saved: hot path remains 0 us. Cold reload adds a bounded validation scan. It prevents malformed allowlist reloads from exposing optional command spam to the pre-simulation router.

<SELF_AUDIT update="2026-05-19-transactional-allowed-opcodes-csv">
  <TASK_RECONCILIATION>
    <TASK id="05" status="PASS_POLISHED">Emergency opcode mapping remains isolated to bootstrap/mock fallback, not bad authoritative CSV replacement.</TASK>
    <TASK id="10" status="PASS_POLISHED">Load shedding now consumes an allowlist that cannot be partially rewritten by malformed CSV.</TASK>
    <TASK id="19" status="PASS_POLISHED">Both tuning and opcode CSV cold paths are staged and fail closed.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No DTO layout changed. `FutureCommandOpcodeRecord` remains the existing 16-byte unmanaged allowlist record.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Low-quality devices keep previous command policy when CSV validation fails; Middle/High/Ultra richer policies apply only after full allowlist validation.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No new buffers. Existing opcode record buffer and CSV scratch buffer are reused.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>No job graph changes; this is a cold reload path before the validator job reads opcode records.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No runtime dependency or asmdef changes.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Haptic scalar camera fallback remains the command-kernel fake; this pass protects the allowlist that gates whether such optional commands enter the kernel.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Kernel Tuning Hash-Drift Guard Addendum

What was wrong: `kernel_tuning_profiles.csv` used opcode names. The current names hash to the intended `FutureCommandOpcodes`, but the contract depended on unstated FNV behavior. Alias names can hash to different values than their route constants, producing inert profiles.

What was done:

- Converted checked-in kernel tuning rows to exact hex opcode hashes.
- Removed inline comments from data rows so the zero-GC CSV parser never relies on numeric fallback behavior.
- Extended `Validate_Mod_API_Static.ps1` to read `kernel_tuning_profiles.csv`.
- Static validation now rejects non-hex profile tokens, duplicate profile hashes, hashes missing from `FutureCommandOpcodes`, missing command-kernel profiles, and non-kernel extra profiles.
- Re-ran static validation: PASS, schema revision 16, `KernelTuningProfileCount=3`.

Cinematic Cheats used: unchanged. The haptic Dear Lie remains scalar camera-juice impulse under low quality or forced fallback; this pass protects the CSV policy that controls that fallback.

Exact Microseconds saved: runtime 0 us. Prevents inert profile rows from disabling per-opcode caps and exposing the pre-simulation router to the previously bounded 50-300 us hostile-spam cost.

<SELF_AUDIT update="2026-05-19-kernel-tuning-hash-drift-guard">
  <TASK_RECONCILIATION>
    <TASK id="10" status="PASS_POLISHED">Per-opcode load shedding profiles are now statically tied to exact command-kernel opcode hashes.</TASK>
    <TASK id="19" status="PASS_POLISHED">Checked-in tuning CSV is hex-only and statically validated for the exact live kernel profile set.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No DTO layout changed. `ModKernelTuningProfile` remains the existing 32-byte unmanaged DTO consumed from vault buffer `70919`.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below quality 0.3, haptic/subtitle optional traffic still collapses through polynomial budget shedding and camera-juice fallback; the static gate now proves those profile caps are not inert.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No new buffers. Existing tuning profile vault `70919` and CSV scratch vault `70920` remain the only storage used for profile policy.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>No job graph changes. Burst jobs still consume the profile array as `[ReadOnly, NoAlias]` data already resolved before scheduling.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>Docs/static-validator CSV guard only; no sibling runtime dependency or asmdef reference added.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Haptic hardware fanout is still replaced by scalar camera impulse when quality/fallback policy says so; static validation now protects that policy from name-hash drift.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Runtime Tuning Duplicate Guard Addendum

What was wrong: `TryValidateKernelTuningProfilesCsv()` accepted duplicate opcode profile rows. The hot profile lookup returns the first matching row, so duplicate rows created order-dependent caps while still passing transactional validation.

What was done:

- Added `ContainsKernelTuningProfileBefore()` over the existing CSV scratch span.
- `TryValidateKernelTuningProfilesCsv()` now rejects duplicate `OpcodeHash` values before clearing the live profile vault.
- No managed collections, `foreach`, `string.Split`, persistent arrays, or gameplay allocations were introduced.
- Re-ran static validation: PASS, schema revision 16, `KernelTuningProfileCount=3`.
- Re-ran forbidden hot-path grep on validator/editor: no matches.

Cinematic Cheats used: unchanged. The haptic Dear Lie still converts low-quality haptic feedback to scalar camera impulse; this pass protects profile policy from row-order ambiguity.

Exact Microseconds saved: runtime 0 us. Cold duplicate detection is bounded by tiny profile count. It preserves the existing 50-300 us hostile-spam cap by preventing duplicate profile rows from weakening effective `MaxPerFrame`.

<SELF_AUDIT update="2026-05-19-runtime-tuning-duplicate-guard">
  <TASK_RECONCILIATION>
    <TASK id="19" status="PASS_POLISHED">Runtime tuning CSV ingest now rejects duplicate opcode profile rows before live vault mutation.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No DTO layout changed. `ModKernelTuningProfile` remains 32 bytes.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Low quality still sheds optional haptic/subtitle traffic first; duplicate-profile authoring can no longer create ambiguous caps across tiers.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No new buffers. Duplicate scan uses existing CSV scratch span and tuning profile vault `70919` remains the live storage.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>No job graph changes; the validation occurs before Burst jobs consume `[ReadOnly, NoAlias]` profile arrays.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>Single ModdingAPI runtime file plus logs/docs only; no sibling runtime references or asmdef edits.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Camera-juice fallback remains the fake; this pass keeps its tuning source deterministic.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Strict Tuning Numeric Parse Addendum

What was wrong: `TryParseKernelTuningCsvLine()` accepted malformed numeric tuning tokens by falling back to defaults. A bad CSV row could therefore pass validation and alter command spam policy silently.

What was done:

- Replaced fallback numeric reads with strict parsing for priority, max-per-frame, flags, range, max-duration, and intensity-scale.
- Flags now use a strict token parser that accepts decimal uint or hex uint only.
- Missing numeric columns and trailing non-empty data now make the row invalid.
- Re-ran static validation: PASS, schema revision 16, `KernelTuningProfileCount=3`.
- Re-ran forbidden hot-path grep on validator/editor: no matches.

Cinematic Cheats used: unchanged. The haptic scalar camera fallback remains the low-quality feedback fake; strict CSV parsing protects the policy that selects it.

Exact Microseconds saved: runtime 0 us. Cold parse cost remains bounded by CSV byte count. Prevents malformed tuning from weakening caps and exposing the 50-300 us hostile-spam cost.

<SELF_AUDIT update="2026-05-19-strict-tuning-numeric-parse">
  <TASK_RECONCILIATION>
    <TASK id="19" status="PASS_POLISHED">Tuning CSV rows now fail closed on malformed numeric policy fields.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No DTO layout changed. `ModKernelTuningProfile` remains 32 bytes with unchanged offsets.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Low devices retain previous known-good command caps on typo; Middle/High/Ultra overkill tuning applies only from exact numeric rows.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No new buffers; parser still uses CSV scratch vault `70920` and profile vault `70919`.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>No job graph changes; this is a cold validation step before `[ReadOnly, NoAlias]` profile arrays reach Burst jobs.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>ModdingAPI runtime parser only; no sibling runtime references or asmdef edits.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Haptic camera-juice fake remains unchanged; malformed tuning can no longer silently disable it or its caps.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Signed Int Token Guard Addendum

What was wrong: `TryParseIntAscii()` accepted a bare `-` as zero. In strict tuning CSV validation, that could turn a malformed `MaxPerFrame` token into an applied zero cap.

What was done:

- Added a `digitSeen` check after the optional sign.
- Bare sign tokens now fail the shared ASCII int parser.
- Re-ran static validation: PASS, schema revision 16, `KernelTuningProfileCount=3`.
- Re-ran forbidden hot-path grep on validator/editor: no matches.

Cinematic Cheats used: unchanged.

Exact Microseconds saved: runtime 0 us. Cold parser cost is one boolean branch; the gain is fail-closed tuning policy.

<SELF_AUDIT update="2026-05-19-signed-int-token-guard">
  <TASK_RECONCILIATION>
    <TASK id="19" status="PASS_POLISHED">Strict tuning parse now rejects bare signed integer tokens.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No DTO layout changed.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Malformed cap tokens no longer collapse low-tier command budgets silently.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No buffer changes.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>No job graph changes.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>Parser-only ModdingAPI runtime edit; no sibling runtime references or asmdef edits.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Unchanged; haptic camera-juice fallback policy remains protected by strict CSV validation.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Decimal Parser Overflow Guard Addendum

What was wrong: ASCII uint/int parsers could wrap on very large decimal tokens in unchecked arithmetic. Strict tuning validation still had a path where malformed huge values became small valid values.

What was done:

- Added pre-multiply overflow checks to decimal uint and int parsing.
- Overflowing numeric tokens now fail the CSV row before live profile vault mutation.
- Re-ran static validation: PASS, schema revision 16, `KernelTuningProfileCount=3`.
- Re-ran forbidden hot-path grep on validator/editor: no matches.

Cinematic Cheats used: unchanged.

Exact Microseconds saved: runtime 0 us. Cold parse adds one branch per decimal digit; protects profile caps from wrapped garbage values.

<SELF_AUDIT update="2026-05-19-decimal-parser-overflow-guard">
  <TASK_RECONCILIATION>
    <TASK id="19" status="PASS_POLISHED">Strict tuning parse now rejects overflowing decimal integer tokens.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No DTO layout changed.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Malformed huge cap/flag tokens no longer wrap into valid low-tier or overkill-tier policy.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No buffer changes.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>No job graph changes.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>Parser-only ModdingAPI runtime edit; no sibling runtime references or asmdef edits.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Unchanged; strict parser protects the haptic fallback policy source.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Exact Kernel Tuning CSV Column Guard Addendum

What was wrong: strict tuning parse still accepted a malformed row with a trailing empty column such as `0xE6E4AEBB,0.35,128,0,32,5,1,`. The runtime parser saw no non-empty trailing data, and the static gate proved profile hashes without proving exact row width.

What was done:

- Added `CountCsvDelimiters(ReadOnlySpan<byte>)` and required exactly six delimiters before parsing a tuning profile row.
- Extended `Validate_Mod_API_Static.ps1` to require exactly seven columns for every checked-in kernel tuning row.
- Added static token-shape checks for priority, max-per-frame, flags, range, max-duration, and intensity-scale.
- Re-ran `Docs/Modding/Validate_Mod_API_Static.ps1`: PASS, schema revision 16, `KernelTuningProfileCount=3`.
- Re-ran forbidden hot-path grep on `FutureCommandSandboxValidator.cs` and `ModdingAPI/Editor/ModKernelInspectorWindow.cs`: no matches.
- Re-ran scoped `git diff --check`: PASS with CRLF warnings only.

Cinematic Cheats used: unchanged. Haptic feedback still collapses to scalar camera-juice impulse when quality/fallback policy requires it; this pass protects the CSV policy that controls that fake.

Exact Microseconds saved: runtime hot path 0 us. Cold parser adds one delimiter scan per tuning row. It prevents malformed rows from weakening low-tier command caps and exposing the pre-simulation router to the bounded 50-300 us hostile-spam cost.

<SELF_AUDIT update="2026-05-19-exact-kernel-tuning-column-guard">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">DevNull route traced; kernel injection remains in PRE_SIMULATION validator route.</TASK>
    <TASK id="02" status="PASS">Legacy command surface remains obsolete and blocked.</TASK>
    <TASK id="03" status="PASS">Hot signal structs use public fields, no hot properties.</TASK>
    <TASK id="04" status="PASS">Explicit layouts retained; no Pack=1 added.</TASK>
    <TASK id="05" status="PASS">Emergency opcode map remains bootstrap/mock only.</TASK>
    <TASK id="06" status="PASS">SurvivalOverride emits typed signal, no direct survival mutation.</TASK>
    <TASK id="07" status="PASS">HapticPulse stays AUP-local and signal/fallback only.</TASK>
    <TASK id="08" status="PASS">SubtitleCue stays numeric-token-only.</TASK>
    <TASK id="09" status="PASS">Haptic Dear Lie scalar camera fallback unchanged.</TASK>
    <TASK id="10" status="PASS">Continuous quality-weight load shedding unchanged.</TASK>
    <TASK id="11" status="PASS">Rollback suppresses haptic/subtitle while preserving survival.</TASK>
    <TASK id="12" status="PASS">Rejected payload telemetry unchanged.</TASK>
    <TASK id="13" status="PASS">AUP finite/range validation unchanged.</TASK>
    <TASK id="14" status="PASS">Vault-backed uninitialized buffers unchanged.</TASK>
    <TASK id="15" status="PASS">Deterministic synchronous Burst job attributes unchanged.</TASK>
    <TASK id="16" status="PASS">NoAlias job field policy unchanged.</TASK>
    <TASK id="17" status="PASS">300-frame kernel telemetry ring unchanged.</TASK>
    <TASK id="18" status="PASS">UI Toolkit inspector unchanged.</TASK>
    <TASK id="19" status="PASS_POLISHED">Kernel tuning CSV now fails closed on missing, extra, or trailing-empty columns.</TASK>
    <TASK id="20" status="PASS">Live injection gizmo unchanged.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No DTO layout changed in this pass. Primary DTOs remain: `SurvivalOverrideSignal` 32 bytes; `HapticPulseSignal` 48 bytes because `double3(24)+uint(4)+float(4)+float(4)` cannot fit in 32 bytes without illegal overlap; `SubtitleCueSignal` 16 bytes; `ModKernelTuningProfile` 32 bytes; `KernelExecutionTelemetryEntry` 64 bytes; `FutureCommandValidationStats` 64 bytes.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below `GlobalQualityWeight` 0.3, optional haptic/subtitle work remains shed through the polynomial budget and haptic fallback. Exact CSV row width prevents malformed policy from removing those caps.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No new buffers. Existing IDs remain `70914` opcode map, `70915` telemetry ring, `70916` cursor, `70917` camera impulse ring, `70918` camera state, `70919` tuning profiles, `70920` CSV scratch.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>No job graph changes. Validation is cold/editor. Runtime jobs still consume already-resolved vault arrays with `[ReadOnly, NoAlias]` / `[WriteOnly, NoAlias]` fields and return handles through the scheduler path.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>Domain remains scoped to ModdingAPI/static docs. No sibling runtime assembly reference or asmdef edit was introduced. Full build was not launched by instruction and because the known external World file deletion remains the build blocker.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>The Dear Lie remains haptic hardware fanout replaced by scalar camera-juice impulse under quality/fallback policy. Complexity before: optional haptic signal/API fanout can scale with spammed command count. After: Burst shedder caps optional packets and low-quality haptics collapse to O(accepted fallback writes), bounded by profile policy.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Static Tuning Numeric Range Parity Addendum

What was wrong: `Validate_Mod_API_Static.ps1` checked tuning float tokens as finite `double` values and signed ints against full `Int32` range. Runtime parsing stores float tokens as `float` and rejects signed integer magnitudes larger than `int.MaxValue`, so the static gate could approve rows that runtime would reject.

What was done:

- `Test-StrictDecimalFloat()` now requires `Abs(value) <= [single]::MaxValue`.
- `Test-StrictInt32()` now requires `[-int.MaxValue, int.MaxValue]`, matching the runtime parser's magnitude guard.
- Re-ran `Docs/Modding/Validate_Mod_API_Static.ps1`: PASS, schema revision 16, `KernelTuningProfileCount=3`.
- Re-ran scoped `git diff --check`: PASS with CRLF warnings only.

Cinematic Cheats used: unchanged.

Exact Microseconds saved: runtime hot path 0 us. Static gate parity prevents malformed large numeric policy from reaching the cold reload facade and weakening command caps.

<SELF_AUDIT update="2026-05-19-static-tuning-numeric-range-parity">
  <TASK_RECONCILIATION>
    <TASK id="19" status="PASS_POLISHED">Static tuning validation now matches runtime parser numeric ranges.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No DTO layout changed.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Low devices retain previous caps on out-of-range numeric policy; richer tiers apply only exact finite-float/int rows.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No buffer changes.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>No job graph changes.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>Static validator only; no runtime dependency or asmdef change.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Haptic camera-juice fallback remains unchanged; static parity protects its tuning source.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Kernel Profile MaxPerFrame Bound Addendum

What was wrong: `MaxPerFrame` profile policy was strict for syntax but unbounded semantically. A huge checked-in row or stale Vault row could overflow the aggregate sum in `ResolveKernelProfileFrameBudget()` or disable the per-opcode spam cap by returning an unrealistic cap.

What was done:
- Added `FutureCommandSandboxConstants.KernelMaxProfileCommandsPerFrame = 10000`.
- Runtime CSV validation now rejects `max_per_frame` outside `[1,10000]` before mutating live profile rows.
- Static validation parses that runtime constant and enforces the same checked-in CSV range.
- Runtime scheduler clamps existing profile rows before aggregate budget sum, smallest-cap checks, and exact per-opcode cap resolution.

Cinematic Cheats used: unchanged. The haptic Dear Lie still converts weak-device haptic pulses to scalar camera juice; this pass preserves the command budget that protects that fake under hostile UGC load.

Exact Microseconds saved: hot-path delta is below 1 us because profile scans are bounded to 16 rows and use integer min/sub/add. The protected path remains the existing 50-300 us hostile-spam cap by preventing overflow or stale-policy cap bypass.

<SELF_AUDIT>
  <PASS_NAME>Bounded Kernel Profile MaxPerFrame Authority</PASS_NAME>
  <THE_20_TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Injection point unchanged: PRE_SIMULATION validator route before DevNull.</TASK>
    <TASK id="02" status="PASS">Legacy command lanes remain obsolete/blocked.</TASK>
    <TASK id="03" status="PASS">No DTO properties introduced.</TASK>
    <TASK id="04" status="PASS">No DTO layout changed.</TASK>
    <TASK id="05" status="PASS">Emergency opcode map unchanged.</TASK>
    <TASK id="06" status="PASS">Survival signal route unchanged.</TASK>
    <TASK id="07" status="PASS">Haptic signal route unchanged.</TASK>
    <TASK id="08" status="PASS">Subtitle numeric route unchanged.</TASK>
    <TASK id="09" status="PASS">Dear Lie fallback unchanged.</TASK>
    <TASK id="10" status="PASS_HARDENED">Load shedding now clamps stale profile caps before aggregate and per-opcode budget math.</TASK>
    <TASK id="11" status="PASS">Rollback suppression unchanged.</TASK>
    <TASK id="12" status="PASS">Rejection telemetry unchanged.</TASK>
    <TASK id="13" status="PASS">AUP guards unchanged.</TASK>
    <TASK id="14" status="PASS">No new local NativeArray/List/HashMap allocation.</TASK>
    <TASK id="15" status="PASS">No Burst directive changed.</TASK>
    <TASK id="16" status="PASS">NoAlias job fields unchanged.</TASK>
    <TASK id="17" status="PASS">Telemetry ring unchanged.</TASK>
    <TASK id="18" status="PASS">Editor inspector unchanged.</TASK>
    <TASK id="19" status="PASS_HARDENED">CSV tuning now fails closed for `max_per_frame` outside `[1,10000]` with static/runtime parity.</TASK>
    <TASK id="20" status="PASS">Injection gizmo unchanged.</TASK>
  </THE_20_TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>No runtime DTO layout changed. `ModKernelTuningProfile` remains 32 bytes: `OpcodeHash@0` 4, `PriorityWeight@4` 4, `MaxPerFrame@8` 4, `Flags@12` 4, `MaxDurationSeconds@16` 4, `RangeMeters@20` 4, `IntensityScale@24` 4, `Reserved@28` 4. Total 32 bytes; 32 % 16 = 0. `FutureCommandValidationStats` and `KernelExecutionTelemetryEntry` remain 64-byte cache-line records.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Global command budget still uses `GlobalQualityWeight^2`. Below 0.3, aggregate processing collapses toward the low-tier floor and optional haptic/subtitle profiles are shed first; the new cap bound prevents authored or stale profile values from punching through that continuous curve.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No private native arrays added. Existing Vault handles remain: 70914 opcode map, 70915 telemetry ring, 70916 cursor, 70917 camera impulse ring, 70918 camera state, 70919 tuning profiles, 70920 CSV scratch.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>Job fields remain `[NoAlias]`; no dependency graph change. `LoadSheddingJob` consumes the pending-ring state and writes compacted pending ring plus stats before `ValidateFutureCommandEnvelopeJob` consumes staging.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>SHINOBU_102 runtime path still has no direct sibling-domain reference added. Full C# build was not launched per explicit instruction and known external World deletion blocker.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Haptic feedback remains faked as one scalar camera-juice impulse on low quality or forced fallback. Heavy hardware haptic fanout stays outside the kernel. Complexity remains O(1) per haptic packet versus downstream device/API fanout; this pass only preserves the budget envelope.</DEAR_LIE_CONFIRMATION>
  <VERIFICATION>`Docs/Modding/Validate_Mod_API_Static.ps1` PASS. Focused grep found `KernelMaxProfileCommandsPerFrame` in runtime semantic validation, static validation, aggregate sum clamp, smallest-cap clamp, and exact per-opcode cap clamp. Forbidden hot-path grep returned no matches. `git diff --check` passed with CRLF warnings only.</VERIFICATION>
</SELF_AUDIT>
