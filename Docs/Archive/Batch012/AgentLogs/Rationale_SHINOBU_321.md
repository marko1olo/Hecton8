# Rationale_SHINOBU_321

Status: POLISH LOOP 7 - PENDING VERIFICATION

## Decision 00: Initial Boundary

Problem: SHINOBU_321 requires a medical decompression simulator but must not create a competing authority path or unmanaged memory route without source proof.

Solution: Load the batch prompt, domain document, and eight relevant mandates before code. Keep implementation inside the physiology domain if an owner exists. Use Burst jobs over fixed buffers and typed unmanaged signals only after confirming existing contracts.

Rejected Alternatives: A standalone `HectonDecompressionManager` would duplicate ownership and create merge risk with existing physiology/player code. A MonoBehaviour velocity-damage script is explicitly rejected by the assignment and violates zero-GC/tick doctrine.

Scalability potential: Low uses grouped tissues and reduced cadence while preserving lethal M-value boundaries. Middle runs full 16 tissue truth at slow tick. High/Ultra spend saved CPU only on presentation telemetry, tuner visualization, and visor/audio effects, not divergent gameplay truth.

Hardware Impact: Expected hot-path shape is fixed NativeArray traversal with 128-byte DTOs. On i3/MX350, grouped 4-tissue mode avoids 12 compartment exponentials under pressure; exact gain pending source implementation and profiler proof.

## Decision 01: Mandates Selected

Problem: The task intersects medicine math, native memory, AUP precision, signals, execution phases, and crash telemetry.

Solution: Loaded these mandates: Zero GC, ARM64 struct layout, Native Memory/Jobs, Floating Origin, AUP Determinism, Signal Lane Segregation, Execution Phases, Postmortem Telemetry.

Rejected Alternatives: Reading only physiology docs is insufficient because the assignment creates a native DTO, uses a typed signal, and depends on AUP depth.

Scalability potential: Mandates force continuous quality, phase-specific scheduling, and fault records across weak/middle/high/ultra hardware.

Hardware Impact: Prevents managed heap stalls and misaligned ARM64 accesses before implementation.

## Decision 02: Existing Owner Integration

Problem: A decompression simulator can easily become a second physiology authority if implemented as a new manager.

Solution: Reused `ShinobuPhysiologyRuntime`, converted it to `partial`, and added `ShinobuPhysiologyRuntime_Decompression.cs` for decompression-specific editor/read accessors. The hot loop remains scheduled by the existing runtime and uses existing Vault handles.

Rejected Alternatives: A standalone `HectonDecompressionManager` would duplicate pressure/gas ownership and compete with the existing `PhysiologyStateSignal` and `CombatDamageSignal` lanes.

Scalability potential: Low/Middle/High/Ultra all use the same owner and DTO layout; quality only changes how many representative tissue compartments are evaluated, not authority ownership.

Hardware Impact: No new MonoBehaviour tick or registry polling. MX350 path avoids extra manager dispatch and avoids a second DataVault lookup route.

## Decision 03: Legacy Velocity Damage Isolation

Problem: `HectonSurvivalSystem` still had a velocity-based decompression signal path using `_rapidAscentMetersPerSecond` and immediate ascent thresholds.

Solution: Disabled `ShouldApplyImmediateDecompressionDamage` and `ApplyRapidAscentDamage` so bends damage routes only through the 16-tissue Vault model and `SignalBus<CombatDamageSignal>`.

Rejected Alternatives: Deleting the whole survival composite was rejected because it owns unrelated hunger, temperature, radiation, pressure, save, and UI contracts. Leaving the immediate path alive would double-punish rapid ascents.

Scalability potential: Low tier removes the legacy branch entirely. Middle/High/Ultra spend decompression budget inside the SIMD tissue model and presentation signals instead of duplicate damage checks.

Hardware Impact: Removes two legacy branch/signal paths from the survival tick when rapid ascent risk is high; estimated saving is 0.2-0.6 us on i3/MX350 under ascent stress, plus removal of duplicate gameplay side effects.

## Decision 04: 128-Byte Decompression State

Problem: The existing decompression row was 80 bytes and exposed `TissueTensions`, `AmbientPressure`, and `AscentRate`, which did not match the assignment's ARM64/SIMD envelope.

Solution: Replaced it with explicit 128-byte `DecompressionStateDTO`: fixed `TissueTensionsN2[16]` at offset 0, `CurrentAmbientPressure` at 64, `GradientAdvantage` at 68, `BubbleFlags` at 72, and uint padding to offset 124. Added layout guards.

Rejected Alternatives: Keeping the 80-byte row would preserve ABI but fails the task and risks cache-line ambiguity for 16-tissue SIMD traversal. A managed array is rejected by Zero-GC mandate.

Scalability potential: Low/Middle/High/Ultra share identical save/DTO memory shape; only active math representation changes by continuous quality.

Hardware Impact: 128-byte rows align cleanly to two cache lines. Fixed buffer traversal avoids a managed object dereference per tissue and prevents heap pressure on low-end silicon.

## Decision 05: Schreiner/Buhlmann Kernel And Visual Lie

Problem: The existing integrator used a scalar Haldane exponential and a ratio M-value approximation. The task requires Schreiner-style source-rate integration, 4-wide SIMD, and Buhlmann `a/b` ceilings while keeping visual hallucination effects out of CPU particles.

Solution: `IntegrateBloodGasTensionsJob` now loads four tissues at a time through `v128` registers, evaluates `float4` Schreiner math, writes `GradientAdvantage`/`BubbleFlags`, and emits `PhysiologyStateSignal` plus `CombatDamageSignal` only on active bubbling. `GlobalShaderDispatcher` already consumes `PhysiologyStateSignal.CauseDecompression` and maps supersaturation/narcosis/pressure into the physiology decompression shader payload.

Rejected Alternatives: A CPU particle or audio object spawn was rejected because presentation belongs to shader/DSP consumers. A direct health decrement was rejected because combat/health owns damage application.

Scalability potential: Low uses representative compartments across the 16-tissue envelope. Middle expands representative count. High/Ultra evaluate all 16 and feed richer shader payloads without changing gameplay DTO layout.

Hardware Impact: Four-wide lanes keep the 16-compartment solve to four vector groups. Estimated low-end hot-path target remains under 2 us for one player row; measured profiler proof is still absent.

## Decision 06: Continuous Quality And Black Box

Problem: The assignment rejects binary low/high switches and requires postmortem evidence for invalid decompression math.

Solution: `ResolveActiveCompartmentCount(GlobalQualityWeight)` maps continuously from 4 to 16. The low endpoint mirrors four averaged tissue groups across the fixed 16-tissue DTO so lethal boundaries still evaluate against per-compartment Buhlmann coefficients. The existing 300-entry `PhysiologyTelemetryEntry` ring remains the black box and now dumps to `Dump_SHINOBU_321.bin`.

Rejected Alternatives: A hard low-tier boolean was rejected. A separate decompression telemetry buffer was rejected because the existing physiology black box already records depth, nitrogen load, supersaturation, fatal flags, and execution microseconds under the physiology owner.

Scalability potential: Low uses four grouped tissue values. Middle increases representative count. High evaluates dense 16 rows. Ultra keeps dense truth and allows presentation systems to spend the saved CPU on shader/audio intensity.

Hardware Impact: On MX350/i3, low grouped mode removes unique slow-tissue divergence and keeps memory traversal fixed. Expected saving is ALU pressure rather than memory bandwidth; profiler proof remains pending.

## Decision 07: Cold Tooling And CSV

Problem: Designers need decompression tuning and compartment visibility without adding runtime UI allocations or managed CSV parsing to the hot path.

Solution: Added `HaldaneanDecompressionTunerWindow` and Scene View gizmo under the Editor assembly only. Runtime exposes partial read/write methods that refuse same-frame job readback. CSV ingest remains cold boot and uses `ReadOnlySpan<byte>` plus deterministic key hashes; a root `buhlmann_zh16_profiles.csv` seed provides the 16 ZH-L16 nitrogen half-times and `a/b` rows.

Rejected Alternatives: Runtime IMGUI/on-screen bars were rejected. `float.Parse`, `string.Split`, and CsvHelper were rejected because they allocate and are culture-sensitive. A new tuning DTO was rejected to avoid adding a fresh Vault route during a multi-agent batch.

Scalability potential: Low devices do not pay for editor tooling in player builds. High/Ultra devices can increase shader/audio presentation intensity from the existing `PhysiologyStateSignal` without changing decompression truth.

Hardware Impact: Editor-only allocations do not enter player hot paths. Cold CSV scratch uses existing Vault byte buffer; no recurring managed parser allocation is added.

## Decision 08: Build Gate

Problem: Final compile verification was required but project policy forbids starting dotnet/build while CPU is over 50% or another `dotnet`/`csc.exe` is active.

Solution: Did not launch a build. Recorded the gate state: CPU sampled at 100%, one `csc.exe` process and two `dotnet.exe` processes were active. Used static source checks instead: old decompression field names, forbidden parser APIs, OOP timer patterns, JSON validity, and diff whitespace.

Rejected Alternatives: Starting another `dotnet build` would violate the explicit hardware protection rule and risk interfering with another agent's compile.

Scalability potential: No runtime scalability impact; this is a verification hygiene decision.

Hardware Impact: Avoided adding another compiler workload while CPU was saturated.

## Decision 09: Black-Box Current Cursor And Legacy DCS Facade Purge

Problem: The post-job dump hook patched `ExecutionMicroseconds` into the current telemetry cursor row but evaluated fatal flags from the previous row. That left the explicit `>= 0.2 ms` dump requirement unproven. Static editor scan also exposed the old `DcsPhysiologyTunerWindow` retaining managed `float[]` tissue arrays and formatted status strings, creating a legacy decompression facade beside the new Haldanean tuner.

Solution: Added `TelemetryDumpBudgetMicroseconds = 200f`, changed `TryDumpAutopsyIfFatal` to read `_telemetryCursor % telemetry.Length` after patching execution time, and made over-budget/non-finite telemetry dump the same raw ring as fatal/invalid decompression state. Replaced `DcsPhysiologyTunerWindow` with a menu shim to `HaldaneanDecompressionTunerWindow`. The Haldanean tuner now uses throttled cached runtime resolution, fixed label literals, named callbacks, and constant status strings instead of per-refresh numeric formatting.

Rejected Alternatives: Ignoring the editor artifacts as "only editor" was rejected because the assignment explicitly asks for a decompression tuner and managed tissue-array purge. Running a build to prove the patch was rejected because the guard sampled CPU at 100% and later 78.3%, both above the 50% limit.

Scalability potential: Low/Middle/High/Ultra gameplay truth remains unchanged. The polish only improves proof and editor hygiene; the black-box dump now captures over-budget frames across all quality weights.

Hardware Impact: Current-row dump removes a forensic blind spot at no hot-path cost beyond one extra telemetry-row read during owner completion. Removing the legacy editor chart removes cold editor allocations and avoids duplicate designer-facing decompression controls; player runtime cost remains 0 us.

## Decision 10: Read Purity, Editor Write Locks, Telemetry Facade, DataMonolith Gate

Problem: Public physiology `TryGet*` accessors still used the owner resolve helper, which is fail-closed but less strict than the doctrine for pure read accessors. Editor tuning writes were direct Vault row mutation without explicit writer fences. The Haldanean tuner showed tissue bars but did not display the latest ambient telemetry marker or black-box fault state. The required Data Monolith binary was absent during loop 7 inspection.

Solution: Added `TryReadPhysiologyVaultArray` so public `TryGet*` copy accessors read only through `GlobalDataVault.TryReadHandle` and never acquire/grow/publish/complete. Routed `SetEditorTuning`, `SetEditorGasTuning`, and `SetEditorBreathingGasNitrogenFraction` through `TryAcquireWriteLock` and `ReleaseWriteLock` in `finally`. Extended the Haldanean tuner with ambient pressure marker lines and telemetry-fault status sourced from `PhysiologyTelemetryEntry`. Patched the debug M-value accessor to fall back to emergency ZH-L16 `a/b` coefficients when the coefficient row is absent instead of clamping default zero `b` to `0.1`. Recorded `static_data.h8bin` absence as a Data Monolith gate instead of fabricating a binary artifact.

Rejected Alternatives: Leaving resolve-helper reads was rejected because it weakens audit clarity. Creating a separate editor cache or managed decompression model was rejected because it becomes shadow state. Generating `static_data.h8bin` manually was rejected because monolith readiness must come from the import/bake/boot pipeline. Launching build before the gate check remains rejected by the CPU/compile-wall rule.

Scalability potential: Low/Middle/High/Ultra gameplay truth remains unchanged. The facade now reads the same authority row and telemetry ring at editor cadence; high-end editor visualization gets an ambient line and fault status without adding player runtime cost.

Hardware Impact: Player hot path cost is 0 us for the editor changes. Public reads now take a read-fenced Vault view and copy one DTO/row only when no job is scheduled. Editor writes avoid racing owner jobs and prevent hidden same-frame mutation hazards on low-end CPUs.

## Decision 11: Loop 7 Build Attempt And External Compile Wall

Problem: After loop 7 source changes, compile proof was still required. The project rule allowed a build only if CPU was under 50% and no compiler/Unity process was active.

Solution: Sampled CPU at 44% and `Get-Process` showed no `dotnet`, `csc`, `MSBuild`, `VBCSCompiler`, or `Unity` process, then launched one constrained `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`. The build stopped on unrelated files: `RadiationHazardGrid.cs` missing `RadiationStateDTO`, and `VRSomaticProvider.Comfort.cs` missing `VRSomaticKinematicStateMirrorDTO` plus `VRSomaticComfortDTO`. No SHINOBU_321 compiler error appeared in the reported error set.

Rejected Alternatives: Editing radiation or VR somatic files was rejected because they are outside the assigned physiology decompression domain. Re-running the same global build immediately was rejected because the first failure is a clear external compile wall.

Scalability potential: No runtime behavior change. The build proof remains blocked by foreign DTO dependencies, not by the decompression math route.

Hardware Impact: Build was serialized with `-maxcpucount:1` after gate clearance. After the final debug M-value fallback patch, CPU was 20% but `VBCSCompiler` PID 2036 was active, so no additional build retry was launched.
