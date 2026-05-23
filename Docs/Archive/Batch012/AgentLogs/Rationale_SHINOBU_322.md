# Rationale_SHINOBU_322

Status: POLISHED_PENDING_COMPILE_GUARD
Agent: SHINOBU_322
Domain: Echelon 5 Combat & Physiology / Hypoxia & Gas Toxicity

## Initial Route Record

Problem: Hypoxia and nitrogen narcosis must affect player perception and input before KCC without mutating PostProcessVolume profiles or player speed truth.
Solution: Use stateless Burst kernels over unmanaged DTO buffers, PRE_SIMULATION input corruption, SlowTick impairment evaluation, VISUAL_SYNC shader scalar upload, and 300-entry blackbox telemetry.
Rejected Alternatives: Runtime PostProcessVolume edits, PlayerController speed multipliers, managed Random, LINQ scans, private persistent NativeArray ownership, and hot GlobalRegistry polling. These violate zero-GC, KCC truth ownership, DataVault sovereignty, or execution phase boundaries.
Scalability potential: Low uses polynomial and sine/LUT-style drift with reduced ALU; Middle keeps deterministic drift and full vignette scalar; High adds richer shader-side noir contraction and chromatic response; Ultra spends saved CPU on presentation-only shader overkill, not gameplay truth bloat.
Hardware Impact: i3/MX350 target is sub-0.1 ms by keeping CPU work in flat arrays, using continuous GlobalQualityWeight to trade complex drift for cheap sine drift, and pushing visual terror to UberNoir shader constants.

## Mandate Selection

Problem: Task crosses physiology, KCC input, AUP, Burst, shader, telemetry, and DTO layout.
Solution: Read 8 mandates: CORE_Abyss_Survival_Systems_O2_Pressure_Logic, DATA_Runtime_Struct_Layout_ARM64, OPT_Zero_GC_Policy_AllocFree_Mandate, OPT_Native_Memory_Collections_JobSystem_Protocol, MATH_AUP_Determinism_Sync, ARCH_Execution_Phases, REND_Shader_Noir_Aesthetics_Dithering_Fog, DBG_Telemetry_Crash_Reporting_PostMortem.
Rejected Alternatives: Reading the full registry would waste context and violate the registry read rule. Reading only rendering or only physiology would miss KCC/AUP/native constraints.
Scalability potential: Mandates define low/middle/high/ultra behavior without binary quality switches.
Hardware Impact: Prevents avoidable GC, cache misalignment, same-frame job stalls, and shader/profile mutation on low-end silicon.

## Loop 1 Decisions

Problem: Legacy O2 distress was routed through CameraJuiceSystem ChromaticAberration profile mutation, sharing a health/O2 slow tick and making oxygen visuals a renderer-profile side effect.
Solution: Removed the O2 chromatic cache/call/mutation surface and added SensoryImpairmentOopScanner to keep any future PostProcessVolume or direct speed route visible in RENDERING_OPTIMIZATION_REPORT.
Rejected Alternatives: Keeping a disabled zero-write reset branch still mutates Volume overrides and leaves the old route alive. Moving oxygen logic into CameraJuice would keep perception coupled to camera state instead of physiology truth.
Scalability potential: Low has no CPU-side post profile work; Middle/High/Ultra spend quality through UberNoir shader constants and deterministic input distortion.
Hardware Impact: i3/MX350 avoids repeated Volume override writes and managed post stack pressure; estimated hot saving is small per call but removes a fragile GC/renderer invalidation route.

Problem: Hypoxia/narcosis needed a gameplay feel change without corrupting KCC authority or speed ownership.
Solution: Added SensoryImpairmentDTO and CorruptPlayerInputJob to bend InputStateDTO and PredictedInputDTO before KCC consumers read them.
Rejected Alternatives: Multiplying HectonPlayerMovement speed, injecting status effects into KCC, or publishing hot GlobalRegistry commands. These create hidden authority routes and rollback disagreement.
Scalability potential: Low uses cheap sine drift; Middle blends deterministic value noise; High/Ultra only increase presentation complexity through continuous GlobalQualityWeight.
Hardware Impact: Single-row input mutation is flat NativeArray work; estimated under 10 us on i3/MX350 before profiler proof.

Problem: The prompt mandates exact ARM64 DTO layouts and private padding.
Solution: Added explicit 32-byte SensoryImpairmentDTO with offsets 0/4/8/12 and private uint padding, plus InitializeOnLoad and runtime layout guards.
Rejected Alternatives: Sequential structs, properties, nested classes, or implicit padding.
Scalability potential: Same DTO ABI scales across device tiers; quality changes do not alter save/network layout.
Hardware Impact: Prevents unaligned loads and defensive-copy hazards on ARM64/mobile CPUs.

Problem: No external gas source may exist during isolated tests.
Solution: GenerateMockToxicityDataJob can allocate/use the shared gas state lane and write deterministic synthetic hypoxia/narcosis profiles.
Rejected Alternatives: Scene oxygen emitters, coroutine-driven test profiles, managed Random, or hidden survival-system calls.
Scalability potential: Low/Middle/High/Ultra use the same deterministic profile; only visual/input response scales.
Hardware Impact: One-row mock avoids scene search and object churn; estimated 8 us on i3/MX350.

## Loop 2 Decisions

Problem: Gas toxicity must become continuous impairment without changing physiology truth or KCC authority.
Solution: EvaluateSensoryImpairmentJob reads GasPhysiologyStateDTO and emits SensoryImpairmentDTO with polynomial hypoxia, smoothed narcosis drift, and latency milliseconds.
Rejected Alternatives: Managed intoxication component, oxygen enum states, or binary drunk/not-drunk toggles.
Scalability potential: Low uses the same DTO with cheaper visual/noise response; Middle/High/Ultra keep truth stable while visuals scale.
Hardware Impact: One sequential NativeArray read/write; estimated 7 us on i3/MX350 before profiler proof.

Problem: The player must feel narcosis as delayed, wrong control without modifying movement speed.
Solution: CorruptPlayerInputJob blends current input with delayed PredictedInputDTO ring data, then adds deterministic drift to MoveAxis and LookDelta.
Rejected Alternatives: Speed multipliers, Rigidbody impulses, KCC acceleration patches, and coroutine input delays.
Scalability potential: Input truth layout stays fixed; quality only changes drift texture richness.
Hardware Impact: One ring read and one ring write; estimated under 10 us on i3/MX350.

Problem: Hypoxia tunnel vision must reach UberNoir without runtime PostProcess profile mutation.
Solution: LateFrameTick publishes x=hypoxia through the existing HectonShaderGlobalDataVaultBridge physiology gas toxicity slot, preserving the established shader global route.
Rejected Alternatives: MaterialPropertyBlock scatter, per-camera Volume weight, or new shader global owner.
Scalability potential: Low can consume scalar as simple vignette; Ultra can spend shader cost on chromatic/tunnel overkill without changing CPU route.
Hardware Impact: DataVault shader slot write is constant-size and avoids per-camera component mutation.

Problem: Quality scaling must not become low/high binary branching.
Solution: Drift math uses GlobalQualityWeight as a smooth blend between cheap sine drift and deterministic value noise.
Rejected Alternatives: if (lowEnd) branches, platform defines, and separate DTO layouts per tier.
Scalability potential: Low, Middle, High, Ultra are points on one curve, not separate code paths.
Hardware Impact: Low tier mostly pays sine math; higher tier buys richer sensory lie with saved shader/CPU budget.

## Loop 3 Decisions

Problem: AUP coordinates can exceed float-safe ranges, but drift noise needs a spatial seed.
Solution: CorruptPlayerInputJob reads PredictedInputAupTargetDTO, subtracts the player AUP origin in double3, and only then casts the local delta to float3.
Rejected Alternatives: Transform.position, absolute float3 AUP, or camera-local seed. Those lose precision or add scene coupling.
Scalability potential: Same seed route works for Low through Ultra; higher quality only consumes the seed with richer drift math.
Hardware Impact: One double3 subtraction; negligible on i3/MX350 compared to avoiding precision-induced input jitter.

Problem: Rollback and prediction must recognize the sensory lie without breaking network/input ABI.
Solution: PredictedInputDTO keeps its 32-byte layout and marks ExtrapolatedDearLie plus Valid after corruption.
Rejected Alternatives: Adding DTO fields, changing save/network identity, or routing through a managed prediction object.
Scalability potential: Quality changes do not alter rollback state layout.
Hardware Impact: One uint flag write; no heap impact.

Problem: Uninitialized vault buffers save startup cost only if every active row is deterministically overwritten.
Solution: All SHINOBU_322 buffers are requested with NativeArrayOptions.UninitializedMemory and InitSensoryImpairmentJob writes baseline rows.
Rejected Alternatives: ClearMemory or UnsafeUtility.MemClear. Both pay for zeroing data that active init overwrites.
Scalability potential: Same init works across capacity changes; active subset defines work cost.
Hardware Impact: Saves cold allocation clearing on low-end CPUs; no hot path cost.

Problem: Faults in control drift must be explainable after the frame is gone.
Solution: A 300-entry SensoryImpairmentTelemetryEntry ring records depth, gas pressures, hypoxia, narcosis, latency, drift, quality, flags, and execution time; NonFinite or over-budget entries dump raw bytes.
Rejected Alternatives: Debug.Log spam, managed lists, or postmortem guesswork.
Scalability potential: Low keeps same 300-frame black box; Ultra can add shader-only spectacle without changing telemetry.
Hardware Impact: One 64-byte write per input mutation and one patch write for gas/depth; estimated 5 us on i3/MX350.

## Loop 4 Decisions

Problem: Designers need live tuning without creating a second source of truth.
Solution: Sensory Impairment Tuner reads the telemetry ring into preallocated editor arrays, draws hypoxia/narcosis lines, and writes the vault tuning row through UnsafeUtility.AsRef.
Rejected Alternatives: ScriptableObject mirror, IMGUI-only one-off fields, or managed runtime singleton state.
Scalability potential: Tuning edits affect continuous Low/Middle/High/Ultra response curves without changing DTO layout.
Hardware Impact: Editor-only; zero player hot-path cost.

Problem: Suit/gas profiles need data-driven override without introducing managed dictionaries into evaluation.
Solution: Cold CSV ingestor copies bytes into a vault scratch buffer and parses spans into fixed profile DTO rows.
Rejected Alternatives: CsvHelper, string.Split, LINQ, and runtime Dictionary key lookups.
Scalability potential: Profiles define curve constants; GlobalQualityWeight still controls runtime cost.
Hardware Impact: Cold-only disk IO and parse; no frame cost on i3/MX350.

Problem: Runtime drift debugging must not instantiate scene helpers.
Solution: SceneView gizmo draws editor Handles from SensoryImpairmentDTO.
Rejected Alternatives: LineRenderer, debug prefabs, or gizmo state GameObjects.
Scalability potential: Editor-only visualization independent of runtime tier.
Hardware Impact: No player build impact.

Problem: Static proof is required for OOP visual mutation eradication.
Solution: SensoryImpairmentOopScanner scans VFX/Rendering/Physiology/Input surfaces for PostProcess, O2, narcosis, speed, and lerp hack patterns and writes a JSON report section.
Rejected Alternatives: Chat-only claims, manual grep screenshots, or runtime warnings.
Scalability potential: Scanner enforces the single shader route regardless of quality tier.
Hardware Impact: Editor-only.

Problem: Layout failure must fail fast before ARM64 runtime damage.
Solution: InitializeOnLoad validator throws FatalArchitectureException if sensory/input interop layouts fail.
Rejected Alternatives: warnings, comments, or relying on Burst errors after the fact.
Scalability potential: Same ABI on weak and high-end hardware.
Hardware Impact: Prevents unaligned access traps; no runtime hot cost.

## Loop 5 Decisions

Problem: Cached DataVault handles can become stale after another owner recreates a shared input/gas buffer.
Solution: TryResolveExistingBuffer now retries the latest generation handle after a failed resolve before declaring the lane unavailable.
Rejected Alternatives: Hot GlobalRegistry polling, scene lookup fallback, or allocating a replacement input buffer owned by SHINOBU_322. Those would create competing truth routes.
Scalability potential: Low/Middle/High/Ultra all use the same shared lane; quality does not change buffer ownership.
Hardware Impact: Normal path unchanged. Stale-generation recovery adds one cold TryGetGenerationHandle call only on failure; estimated 6 us worst case on i3/MX350.

Problem: Editor Object lookup could bind ambiguously in future files that import System.
Solution: Qualified FindAnyObjectByType through UnityEngine.Object in tuner and SceneView gizmo.
Rejected Alternatives: Runtime singleton, Resources lookup, or editor GameObject helper. Those add avoidable state or scene coupling.
Scalability potential: Editor-only, no runtime tier impact.
Hardware Impact: Zero player-build cost.

Problem: Compile proof is required, but local protocol forbids dotnet build when CPU is above 50% or dotnet/csc is active.
Solution: Guard checked CPU 100%, dotnet/csc count 0; compile not launched. Static checks completed: prompt extraction, legacy route grep, git diff whitespace check, and self-audit patch.
Rejected Alternatives: Launching dotnet build under 100% CPU would violate the explicit batch protocol and contaminate parallel agent work.
Scalability potential: Verification method does not alter runtime behavior; pending compile must be done when machine load is legal.
Hardware Impact: Prevents build contention on the shared workstation.

## Loop 6 Polish Decisions

Problem: Public `TryGet*` read accessors were using the same resolver as owner write phases, which could refresh cached Vault handles and violate the pure accessor doctrine.
Solution: Added `TryReadCachedBuffer<T>` using the already-cached `VaultGenerationHandle<T>` plus `IDataVault.TryReadHandle`; `TryGetSensoryImpairment`, `TryGetTuning`, `TryGetLatestTelemetry`, `TryGetInputDriftDebug`, and the editor telemetry copier now read immutable snapshots without handle growth or refresh.
Rejected Alternatives: Leaving resolver reuse in place, polling `GlobalDataVault.TryGetLatestCreated()`, or scene-searching runtime objects from read methods. Those mutate route state or create diagnostic-only dependencies in runtime.
Scalability potential: Low/Middle/High/Ultra all read the same immutable DTO layout; quality does not change the accessor route.
Hardware Impact: Prevents accidental cold-path handle refresh during editor/runtime reads; normal hot cost is one descriptor read and no allocation.

Problem: Emergency mock toxicity used Unity realtime, which is non-deterministic under rollback and test replay.
Solution: Mock gas time is now derived from the frame counter and the fixed 60 Hz sensory latency frame rate before entering `GenerateMockToxicityDataJob`.
Rejected Alternatives: `Time.realtimeSinceStartup`, coroutine phase, or `UnityEngine.Random`. These drift across machines and break deterministic replay.
Scalability potential: Same mock curve is used on weak, middle, high, and ultra devices; GlobalQualityWeight only changes response cost and shader spectacle.
Hardware Impact: Removes Unity time read from the mock route; estimated unchanged ALU cost, but deterministic replay is preserved on i3/MX350 and ARM64.

Problem: Telemetry execution timing could be mislabeled because the slow tick patched gas rows with evaluation elapsed time, while the task requires corruption-job execution time.
Solution: `RunInputCorruption` now measures the `CorruptPlayerInputJob.Run(1)` window directly and patches `ExecutionMicroseconds`; slow tick passes a negative sentinel so gas/depth patching cannot overwrite the corruption measurement.
Rejected Alternatives: Stopwatch around the whole input frame, slow tick timing, or profiler-only manual inspection. Those do not write the blackbox proof lane.
Scalability potential: Timing is independent of visual quality and DTO layout; low quality can prove the sine path cost while high quality can prove richer drift cost.
Hardware Impact: One timestamp pair around the synchronous pre-KCC mutation; negligible versus the value of proving sub-0.1 ms behavior.

Problem: The live drift gizmo originally read sensory scalars, not both raw and corrupted input vectors, so it was not a direct x-ray of the mutation.
Solution: Added 64-byte `SensoryInputDriftDebugDTO` in the Vault, written by `CorruptPlayerInputJob` with raw/corrupted move/look vectors, frame, flags, and state hash. The SceneView gizmo reads this DTO and draws green raw and red corrupted vectors.
Rejected Alternatives: LineRenderer debug objects, reading the KCC after consumption, or editor-only mirror state. These allocate or observe the wrong owner boundary.
Scalability potential: Editor-only proof path; runtime data layout remains fixed across quality weights.
Hardware Impact: One 64-byte write per input corruption when buffer is present; false sharing is avoided by single-row cache-line sizing.

Problem: The OOP scanner was too broad and could report unrelated rendering/editor strings as hypoxia violations.
Solution: Scanner summary was changed to `OOP Visual Mutations Eradicated`, editor roots are skipped, and findings require a sensory context token before forbidden patterns are counted. Sidecar/shared report artifacts record zero SHINOBU_322 findings.
Rejected Alternatives: Broad regex-only scans, manual grep proof, or no disk report. Broad scans obscure real regressions with unrelated rendering code.
Scalability potential: Scanner enforces the same shader-only route for every hardware point on the continuous quality curve.
Hardware Impact: Editor-only; no player build cost.

Problem: Compile proof is still required, but the machine was not legal for build execution.
Solution: Guard checked CPU 97% and active `VBCSCompiler.exe`; dotnet build was not launched. Static checks passed for no hot `Time`, `UnityEngine.Random`, `Complete`, `foreach`, LINQ, `new NativeArray`, private native collections, `Pack=1`, or DTO properties in SHINOBU_322 files. Focused `git diff --check` returned only existing CRLF warnings.
Rejected Alternatives: Starting a build under CPU 99%/active compiler would violate the explicit protocol and damage parallel agent throughput.
Scalability potential: Verification discipline does not affect runtime quality behavior.
Hardware Impact: Protects the shared workstation from compile contention.

## Loop 7 Polish Decisions

Problem: The prompt re-extraction proof used an exact opening tag and failed after `CURRENT_BATCH.md` included `role` and `chat_name` attributes on the SHINOBU_322 tag.
Solution: Re-ran extraction with an attribute-aware CLI regex bound to `id="SHINOBU_322"`, measured block length `23719`, and recounted exactly `20` `Task NN:` headings.
Rejected Alternatives: Trusting the older status note, relying on IDE context, or reading neighboring prompt blocks. Those break the anti-amnesia protocol and allow cross-agent contamination.
Scalability potential: No runtime tier impact; this keeps verification deterministic and scoped.
Hardware Impact: Static CLI only; no player cost.

Problem: Several cold/editor/test write paths resolved Vault buffers and wrote rows without an explicit `TryLockBuffer` fence.
Solution: Added owner-tagged locks around `SetEditorTuning`, `InjectMockGas`, `RunEvaluation` tuning writes, `InitializeDefaults`, and `TryLoadCsvProfilesCold` profile/tuning/scratch writes. Existing input mutation locks and mutation guard remain in place.
Rejected Alternatives: Assuming cold paths are automatically safe, using raw pointer writes without a fence, or moving ownership to private arrays. Those violate Vault sovereignty and compaction safety.
Scalability potential: Low/Middle/High/Ultra all keep the same Vault ABI and write route; quality still only changes continuous math cost and shader presentation.
Hardware Impact: Lock cost is cold/editor/one-row phase overhead. It protects low-end silicon from relocation races and data corruption; hot input mutation still writes one input row, one predicted row, one debug row, and one telemetry row.

Problem: The editor scanner could preserve a stale shared-report section or overwrite the sidecar report with a thinner schema than the committed proof artifact.
Solution: Extended `SensoryImpairmentOopScanner` to emit runtime route, shader route, BufferID list, ABI, evidence class, zero-count proof fields, and static compile status. Its shared-report upsert now replaces an existing `shinobu322SensoryOopScanner` object by matching braces instead of returning early.
Rejected Alternatives: Leaving the manual JSON as the only rich artifact, or appending duplicate shared-report sections. Those make repeated scanner runs non-idempotent and weaken architectural proof.
Scalability potential: Editor-only; no runtime quality effect.
Hardware Impact: Editor file IO/string work only. No player hot path.

Problem: Compile proof remains blocked after the lock pass.
Solution: Guard checked CPU 100% with active external `dotnet build Hecton8.Core.csproj` and `csc.exe`; build remains forbidden by the >50% CPU and active compiler local protocol. Focused static scan found no hot `Time`, `UnityEngine.Random`, `.Complete`, `foreach`, LINQ, `new NativeArray`, private native collection, `Pack=1`, or DTO auto-property hits in SHINOBU_322 files; focused `git diff --check` returned only CRLF normalization warnings in documentation/report files.
Rejected Alternatives: Launching a second build under CPU 100%/active compiler or claiming a compile pass without Unity import/build evidence.
Scalability potential: Verification discipline does not change the continuous quality curve.
Hardware Impact: Prevents compile contention on a saturated workstation.
