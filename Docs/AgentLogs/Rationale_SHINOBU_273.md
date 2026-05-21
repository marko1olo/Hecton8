# Rationale_SHINOBU_273

Status: POLISH STATIC VERIFIED / BUILD BLOCKED BY CPU GATE

## Decision 001 - Authority Surface
Problem: Frequency hacking must not become another Canvas-driven minigame or direct terminal singleton.
Solution: Own a decryption kernel in Echelon 8 that transforms unmanaged DTO state, with unlock state emitted as typed unmanaged signal payloads and visual state consumed by shader-side StructuredBuffer.
Rejected Alternatives: Screen-space Canvas puzzle, LineRenderer waveform mesh, per-terminal managed puzzle component state, and string-keyed shader updates. These violate diegetic UI, zero-GC, and global authority rules.
Scalability potential: Low keeps flat oscilloscope line/noise math; Middle adds smoother envelope; High increases glow and anti-aliased curve; Ultra spends saved CPU on richer CRT/noise shader presentation.
Hardware Impact: MX350/i3 target avoids Canvas rebuild and CPU mesh line generation; expected saved CPU is tens to hundreds of microseconds when replacing UI rebuilds, pending profiler proof.

## Decision 002 - Mandate Set
Problem: Task crosses UI, native memory, AUP, SignalBus, shader, and crash telemetry.
Solution: Use 8 mandates: UI diegetic interfaces, zero-GC UI data, ARM64 DTO layout, zero-GC policy, signal lane segregation, GlobalRegistry DI, AUP determinism, and black-box telemetry.
Rejected Alternatives: Reading only UI docs or treating the task as shader-only. Unlocking doors makes this gameplay authority, not presentation-only.
Scalability potential: Continuous quality scalar drives shader envelope/noise and update cadence without changing authoritative DTO layout.
Hardware Impact: Stable 32-byte DTOs and bounded signals keep cache behavior predictable on low-end silicon; richer visuals live only in shader quality math.

## Decision 003 - DTO Authority And Unlock Route
Problem: Terminal hacking needs gameplay authority without managed UI state or cross-domain door dependencies.
Solution: Add `DecryptionPuzzleDTO`, `DecryptionTerminalDTO`, `DecryptionKnobInputDTO`, `DecryptionTelemetryEntry`, and `TerminalUnlockedSignal` as explicit unmanaged payloads. Puzzle solve emits `SignalBus<TerminalUnlockedSignal>` only; consumers decide what node unlock means.
Rejected Alternatives: Direct door/component references from TerminalOS, string command dispatch, and managed terminal puzzle objects. Those create hot polling and cross-domain sabotage.
Scalability potential: Low through Ultra keep identical DTO layout and authority path; only shader fidelity, solve cadence, and visual noise scale.
Hardware Impact: 64 puzzle entries at 32 bytes = 2048 bytes hot puzzle state. Signal payload is 32 bytes, accepted by SignalBus ABI. MX350/i3 impact is below cache-line noise versus Canvas event stacks.

## Decision 004 - Oscilloscope Visual Lie
Problem: The prompt demands sine-frequency comparison on the terminal screen without Canvas, LineRenderer, or CPU mesh rebuild.
Solution: Bind `_GlobalDecryptionPuzzles` as a StructuredBuffer to `Assets/_Project/Art/Shaders/Hecton_DiegeticTerminal.shader`; shader draws target/player sine traces, grid, noise, and solved tint from one DTO per terminal slice.
Rejected Alternatives: TMP text waveform, LineRenderer polyline, RenderTexture Canvas, or PDA spectrogram reuse. They allocate or rebuild CPU-visible UI surfaces.
Scalability potential: Low uses thin lines and sparse noise; Middle adds denser grid; High increases anti-aliased thickness/noise; Ultra spends GPU ALU on stronger CRT interference while authority remains unchanged.
Hardware Impact: CPU upload is 2048 bytes when dirty. Shader ALU is local to terminal pixels, avoiding CPU canvas rebuilds; expected CPU saving versus managed UI is 50-300 us during active tuning.

## Decision 005 - Physical Knob Kernel
Problem: Agent 271 may own physical terminal interaction, but this agent cannot wait on or depend on unfinished code.
Solution: Use existing `TerminalInteractionDTO` hover/hold/scroll state as a decoupled knob input source. Left half of the terminal adjusts frequency; right half adjusts phase. The Burst kernel applies deltas only inside AUP-derived interaction radius.
Rejected Alternatives: New `MonoBehaviour` knob components, `GetComponent` scans, or direct calls into VR bridge code. Those add dependencies and hot scene searching.
Scalability potential: Low reduces update cadence through global quality; Middle/High/Ultra retain precision and spend saved time on visual presentation.
Hardware Impact: One 64-byte knob DTO is written per owner frame. Burst pass over 64 puzzles is expected under 100 us on i3/MX350; telemetry dumps if this is false.

## Decision 006 - CSV And Editor Tuning
Problem: Designers need target frequency profiles and live tuning without entering managed runtime puzzle state.
Solution: Add dev-build CSV ingest for `decryption_puzzles.csv` and an editor UI Toolkit tuner that writes DTO targets and scalar weights through `TerminalOsRuntime` owner APIs.
Rejected Alternatives: ScriptableObject runtime lookups and inspector-only serialized puzzle lists. They are cold-data authoring surfaces, not hot DTO ownership.
Scalability potential: CSV changes only target parameters. Quality scaling is continuous through existing `GlobalQualityWeight`.
Hardware Impact: CSV polling is editor/development only every 30-120 frames; release builds skip it.

## Decision 007 - Black Box Forensics
Problem: A minigame that can unlock terminals must not fail with undocumented NaN or budget spikes.
Solution: Maintain 300 `DecryptionTelemetryEntry` records in Vault and dump `Docs/AgentLogs/Dump_SHINOBU_273.bin` on NaN or >0.1 ms solver time.
Rejected Alternatives: Debug.Log-only reports, profiler-only proof, or no dump until QA. These fail postmortem reconstruction.
Scalability potential: Low through Ultra preserve the same forensic ring; no tier can disable fault evidence.
Hardware Impact: Ring is 19,200 bytes native memory. One 64-byte telemetry write per frame is acceptable on low-end silicon.

## Decision 008 - Accessor Purity And Owner-Phase Finalization
Problem: Public `Try*` read routes were able to finalize the decryption job, record telemetry, upload the shader buffer, and dump black-box files outside the owner phase.
Solution: Move all decryption finalization back to `LateFrameTick()` and make public reads return false while a job is in flight. Editor writes now also fail closed while scheduled instead of forcing completion.
Rejected Alternatives: Keeping "helpful" accessors that complete work was rejected because read accessors must be pure; adding blocking `.Complete()` was rejected because dispatcher-owned job fences already exist.
Scalability potential: Low/Middle/High/Ultra all use the same owner-phase route; quality can change cadence, never the completion authority route.
Hardware Impact: Removes unpredictable main-thread stalls from editor/read consumers; expected gain is workload-dependent, with worst-case avoided stall larger than the entire 0.1 ms decryption budget.

## Decision 009 - Deterministic Fused Kernel
Problem: Parallel puzzle mutation created false-sharing risk on prompt-mandated 32-byte puzzle rows, and variable `Time.unscaledDeltaTime` entered gameplay-facing unlock state.
Solution: Replace the three parallel mutation jobs with one fused deterministic Burst `IJob`, using `HectonPhysicsContract.FixedDeltaTimeSeconds`, `SystemDispatcher.CurrentFrameId`, and a `StepFrames` scalar for idle cadence preservation.
Rejected Alternatives: Padding `DecryptionPuzzleDTO` to 64 bytes was rejected because the XML assignment explicitly mandates 32 bytes. Keeping three `IJobParallelFor` passes was rejected because adjacent rows share cache lines and two extra schedules are waste.
Scalability potential: Low quality skips idle evaluations through a continuous 6..1 stride; Middle tightens stride; High/Ultra evaluate every frame and spend saved CPU on richer shader density/noise. Active knob input always evaluates stride 1.
Hardware Impact: On i3/MX350, two job schedules per evaluation are removed and false-sharing cache invalidation is avoided. CPU-saving estimate: 6-40 us per active evaluation before profiler proof.

## Decision 010 - Route Card And Shader Binding Repair
Problem: The route was missing from the binary payload ledger, and shader decryption count/buffer used global setters that could leave stale state after buffer release.
Solution: Add `SHINOBU_273_FREQUENCY_TUNING_DECRYPTION_ROUTE_CARD.md`, ledger BufferID/signal ownership, material-path decryption buffer binding, shader target 4.5, and material count reset on dispose.
Rejected Alternatives: Chat-only documentation, production DataMonolith readiness claims without `static_data.h8bin`, and global decryption shader setters were rejected.
Scalability potential: Low/Middle/High/Ultra share one shader ABI; quality scales line thickness, grid density, static, and idle CPU stride without DTO layout changes.
Hardware Impact: Material-path binding avoids process-global stale StructuredBuffer reads. Static DataMonolith remains missing; CSV/mock fallback is editor/development only.

## Decision 011 - Background Fault Export
Problem: Dumping `Dump_SHINOBU_273.bin` from the owner frame can violate the 0.1 ms decryption budget exactly when a fault needs evidence.
Solution: Copy the fixed 300-row Vault telemetry ring into a cold-created `DecryptionBlackBoxDumpWriter` command and let a background writer emit a little-endian header plus raw telemetry rows. Backpressure sets `FaultDecryptionDumpBackpressure` and publishes one telemetry warning.
Rejected Alternatives: Synchronous file writes in `LateFrameTick`, Debug.Log-only reports, or dropping the dump. The first stalls gameplay; the other two destroy postmortem evidence.
Scalability potential: Low/Middle/High/Ultra preserve identical fault evidence. Quality never disables the black-box route.
Hardware Impact: Owner frame does bounded 300-entry value copy only on fault. The unbounded disk stall leaves the gameplay phase, which matters on i3/MX350 storage and Quest-class flash.

## Decision 012 - Editor Facade And Pointer Evidence
Problem: The tuner did not expose the exact XML-required decryption controls, and decryption unsafe pointer fields needed explicit proof for aliasing and lifetime.
Solution: Add Base Frequency, Snap Tolerance, Noise Density, and GlobalQualityWeight Override controls. Replace string-assembled readout with UI Toolkit numeric fields. Add three safety proof paragraphs to each decryption pointer field and keep `[NoAlias]`.
Rejected Alternatives: Inspector-only tuning, string label readouts, or unexplained `NativeDisableUnsafePtrRestriction`. Those are either slow evidence surfaces or weak Burst safety claims.
Scalability potential: Designers can drive Low/Middle/High/Ultra tuning without recompiling C#; runtime DTO layout and authority route stay fixed.
Hardware Impact: Editor-only managed controls do not enter player runtime. Pointer proof lets the fused Burst job keep aliasing assumptions defensible.

## Decision 013 - Cold Registry Retry Backoff
Problem: When Vault or dispatcher services are unavailable, repeated `EnsureRuntimeReady()` attempts can poll `GlobalRegistry` every owner frame while resources are still missing.
Solution: Gate native resource and late-frame registration retry attempts with a continuous `GlobalQualityWeight`-derived 30..120 frame stride. Service lookup remains a cold DI/bootstrap action; no hot loop or decryption job polls `GlobalRegistry`.
Rejected Alternatives: Per-frame retry spam, binary low-end switch, or removing retry and risking a permanently inert runtime if services appear late.
Scalability potential: Low quality backs off toward 120 frames, middle sits between, high/ultra retries closer to 30 frames for faster editor recovery. Gameplay truth, DTO layout, and signal route do not change.
Hardware Impact: Under missing-service failure, low-tier hardware avoids up to 59/60 registry lookups compared with 60 Hz polling; steady-state registered runtime pays zero extra cost.

## Decision 014 - Raw Span Fault Dump Format
Problem: The decryption background writer still used `BinaryWriter` to serialize each telemetry field, leaving avoidable managed helper surface in the fault exporter.
Solution: Write a fixed 24-byte little-endian header through `stackalloc Span<byte>` and emit each 64-byte `DecryptionTelemetryEntry` row as a raw `ReadOnlySpan<byte>` from the blittable struct.
Rejected Alternatives: Keeping field-by-field `BinaryWriter`, allocating a temporary `byte[]`, or returning synchronous dump writes to the owner frame. These either keep managed serialization overhead or reintroduce frame stalls.
Scalability potential: Low/Middle/High/Ultra keep the same binary proof route; quality never changes the dump ABI.
Hardware Impact: Background export now writes one header plus contiguous fixed-size rows. Owner-frame cost remains bounded to copying 300 value rows only on fault.

## Decision 015 - Proof Artifact Preservation
Problem: `Minigame_Canvas_Inquisition` could overwrite the SHINOBU_273 JSON report section with a reduced field set, erasing route card, BufferID, and Loop 7 evidence.
Solution: Extend the editor scanner's generated JSON section to include timestamp, scanned scope, route card, Vault BufferIDs, determinism/false-sharing/fault-export/editor-facade/cold-registry patches, status, and DataMonolith caveat.
Rejected Alternatives: Treating the hand-edited report as separate from the scanner output. The scanner is the mandated proof generator; it must not destroy evidence when rerun.
Scalability potential: Editor-only proof generation; runtime quality and gameplay truth are untouched.
Hardware Impact: Editor-only string assembly remains outside player runtime. It prevents proof drift without adding gameplay cost.

## Decision 016 - Subagent Audit Closure
Problem: Read-only audit found one public terminal command accessor finalizing a click-resolve job, one proof artifact overstating targeted scan evidence as a full purge, and one undocumented `TerminalStateDTO` dirty-byte overlap.
Solution: `TryDequeueCommand` now returns false while click resolution is scheduled and leaves finalization to owner `LateFrameTick()`. `Minigame_Canvas_Inquisition` and `RENDERING_OPTIMIZATION_REPORT.json` now report targeted canvas-token absence with an explicit non-project-wide claim scope. `TerminalStateDTO.IsDirty` is documented as the unused alpha byte of `BackgroundColor`, and `TerminalOsLayoutValidator` verifies that packed ABI.
Rejected Alternatives: Keeping consumer-side job finalization because it is non-blocking was rejected; read routes must not mutate completion state. Keeping `Managed Puzzle Canvases Purged` was rejected because it implied project-wide scene/prefab proof the scanner does not perform. Moving `IsDirty` to a new stride was rejected because `TerminalBlit.compute` and the GPU state buffer use the existing 48-byte ABI where RGB masks ignore byte 7.
Scalability potential: Low/Middle/High/Ultra use identical command/signal ownership and terminal state ABI; only cadence and shader density scale.
Hardware Impact: Command consumers no longer trigger job-fence state changes outside owner phase. Packed dirty byte preserves 48-byte terminal state upload stride instead of expanding to 64 bytes for a visual terminal row.

## Decision 017 - CI Math Gate Repair
Problem: The terminal interaction path still contained `math.sqrt` and terminal plane sizing still used `math.length`, both of which are banned by the local `CI_MATH_VIOLATIONS` gate even though the surrounding math was finite-guarded.
Solution: Replace distance and axis length evaluation with finite-guarded helpers using `dot + rsqrt`, explicit epsilon denominators, and fallback selection. The SHINOBU_273 TerminalOS scope now scans clean for `math.sqrt`, `math.length`, `Mathf.Sqrt`, `Vector3.Distance`, `UnityEngine.Random`, `Random.Range`, and `.normalized`.
Rejected Alternatives: Keeping sqrt tokens because they were guarded was rejected because CI gates are source-token based. Switching to `Vector3.Distance` or `.normalized` was rejected because it would reintroduce Unity managed math surfaces and new banned tokens. Adding a broad suppression was rejected because the terminal route can satisfy the gate directly.
Scalability potential: Low/Middle/High/Ultra all share the same math route; quality still changes cadence and shader density only, never DTO layout or authority. The helper cost does not introduce a tier switch.
Hardware Impact: On low-end silicon, `rsqrt` maps cleanly to SIMD/NEON-friendly reciprocal square root paths and avoids source-level CI failure. Expected runtime delta is neutral-to-positive versus sqrt, but profiler proof remains pending behind the CPU/build guard.

## Decision 018 - Public Vault Read Purity
Problem: Public `TryGet*Copy` accessors were no longer completing jobs, but they still reused `TryOpenVaultBuffer`, which calls `GlobalDataVault.TryResolveHandle<T>`. On stale, missing, or fenced handles that route records generation faults and debug resolution counters, so a read accessor could still mutate global Vault diagnostics.
Solution: Add `TryReadVaultBuffer<T>` and route all public copy/read accessors through `GlobalDataVault.TryReadHandle<T>`. Keep `TryOpenVaultBuffer<T>` and `TryResolveHandle<T>` only for owner/write paths where fault accounting and mutation authority are legitimate.
Rejected Alternatives: Keeping one helper for all Vault access was rejected because it hides side effects behind `TryGet`. Suppressing Vault fault telemetry globally was rejected because owner/write failures still need diagnostics. Returning cached managed copies was rejected because it creates shadow state outside the Vault.
Scalability potential: Low/Middle/High/Ultra use the same read purity route; quality can change cadence, but never truth ownership or the access contract.
Hardware Impact: On low-end silicon, stale diagnostic/editor reads now fail closed without `Interlocked` debug counter increments or generation fault recording. Runtime gain is failure-path only; the main value is preserving the read accessor contract.

## Decision 019 - Public Mutation Surface Narrowing
Problem: `OpenTerminalStateRefForOwner`, `ForceDirty`, and `ForceAllDirty` were public even though source search found no external call sites. The first returned a mutable `ref TerminalStateDTO`, and the dirty helpers let callers mutate the upload route without a bounded owner API.
Solution: Make all three helpers private. Existing internal call sites remain unchanged; editor and external tools still use bounded methods that sanitize input and preserve owner-phase rules.
Rejected Alternatives: Leaving public methods with documentation was rejected because public mutable refs are an authority leak. Removing the helpers entirely was rejected because internal owner code still uses them for direct DTO mutation and dirty tracking.
Scalability potential: No quality-tier behavior changes. Low/Middle/High/Ultra retain identical owner APIs, DTO layout, and signal route.
Hardware Impact: No runtime microsecond claim. The hardware benefit is indirect: fewer external mutation routes means fewer impossible-to-reproduce dirty-state races and fewer untracked shader uploads during diagnosis.

## Decision 020 - Evidence Class Downgrade And Shader Path Correction
Problem: The SHINOBU_273 rendering report claimed `STATIC_SOURCE_AND_ASSET_TARGETED`, but the inquisition scanner currently scans source/script folders only and does not enumerate asset files with separate counts. The report also named the shader without the real workspace path.
Solution: Downgrade the report evidence class to `STATIC_SOURCE_TARGETED` and add the exact shader path `Assets/_Project/Art/Shaders/Hecton_DiegeticTerminal.shader` to generated and checked-in proof artifacts.
Rejected Alternatives: Keeping the stronger evidence class was rejected because QA evidence rules forbid asset/runtime claims without scanned file counts and artifacts. Expanding the scanner to broad assets was rejected for this pass because the prompt scope is targeted TerminalOS/Terminals evidence, not project-wide asset proof.
Scalability potential: Documentation-only repair; runtime quality continuum is unchanged.
Hardware Impact: No runtime impact. The benefit is audit accuracy: static proof no longer overstates the integration level.

## Decision 021 - Terminal Shader Variant Removal
Problem: `Hecton_DiegeticTerminal.shader` still declared `shader_feature_local HECTON_TERMINAL_INSTANCED`, and `TerminalOsRuntime` toggled that material keyword at bind time. This creates an avoidable runtime shader-variant warmup risk for a terminal presentation path that can be selected by scalar data.
Solution: Remove the local shader feature and replace it with `_HectonTerminalInstancedMode`, a material scalar. Bind `_TerminalPanelInstances` whenever the buffer exists and branch by scalar in the vertex shader, so non-instanced preview/material paths do not read the StructuredBuffer. Runtime no longer calls `EnableKeyword` or `DisableKeyword` for this terminal path.
Rejected Alternatives: Keeping the keyword and documenting warmup was rejected because the variant can be avoided. Creating a second material was rejected because it increases binding/variant surface. Global shader keywords were rejected as process-wide state.
Scalability potential: Low/Middle/High/Ultra all use one shader variant. Quality still scales continuously through `_HectonDiegeticGlitchQualityWeight` and `_HectonDecryptionNoiseDensity`.
Hardware Impact: Reduces first-use shader hitch risk on low-end GPUs and Linux/Vulkan-style drivers. No microsecond claim without frame-debugger/profiler proof.

## Decision 022 - Decryption GPU Mirror Double Buffer
Problem: `_GlobalDecryptionPuzzles` was uploaded through `GraphicsBuffer.LockBufferForWrite`, but it was still a single GPU buffer. Even at 2048 bytes, that violates the bandwidth discipline rule because the CPU can write a buffer the GPU sampled in the previous terminal material pass.
Solution: Split the decryption shader mirror into `_decryptionPuzzleBuffer0` and `_decryptionPuzzleBuffer1`. `UploadDecryptionPuzzles()` writes the selected upload buffer via `LockBufferForWrite`, flips `_decryptionPuzzleWriteBufferIndex`, then binds the freshly written buffer as `_GlobalDecryptionPuzzles`. Vault DTO ownership and shader struct ABI stay unchanged.
Rejected Alternatives: Keeping one buffer was rejected because it can create a driver sync point. `GraphicsBuffer.SetData` was rejected because the runtime upload path already has zero-copy lock/memcpy infrastructure. Triple buffering was rejected as unjustified memory surface for a 64-row terminal puzzle mirror without profiler evidence.
Scalability potential: Low/Middle/High/Ultra use the same two-buffer mirror; quality changes upload cadence and shader density, not gameplay truth or DTO layout. High/Ultra spend the saved sync risk on richer shader presentation through existing quality scalars.
Hardware Impact: On MX350/i3 and mobile drivers, double buffering reduces CPU/GPU synchronization risk during terminal material sampling. No measured microsecond claim; profiler/frame-debugger proof remains pending behind CPU gate.
