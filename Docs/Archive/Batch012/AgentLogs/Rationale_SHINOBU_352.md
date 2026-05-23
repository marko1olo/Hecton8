# SHINOBU_352 Rationale

## Decision 0001: Mandate Selection

Problem: Vocal warning queue touches hot audio presentation, Vault native data, SignalBus routes, AUP directional math, and black-box diagnostics.
Solution: Apply OPT_Zero_GC, DATA_Runtime_Struct_Layout_ARM64, ARCH_Signal_Lane_Segregation, AUD_DSP_Audio_Synthesis, MATH_AUP precision, and DBG_Telemetry mandates before source edits.
Rejected Alternatives: Generic Unity audio manager rules were rejected because they allow AudioSource and managed queues that violate the assignment.
Scalability potential: Low evaluates bounded critical warnings; Middle evaluates standard queue depth; High keeps richer telemetry; Ultra permits deeper presentation diagnostics without changing gameplay truth.
Hardware Impact: Static estimate only. Avoiding managed queues removes GC spikes on i3/MX350; expected hot-path saving is sub-100us under burstable queue stress, pending profiler proof.

## Decision 0002: Integration Boundary

Problem: A pre-existing `VocalWarningSystem` owns the current BETTY warning route and Vault buffers.
Solution: Treat it as the domain owner unless source review proves a better existing `HectonAudioRuntime` boundary. Extend isolated files or a partialized owner rather than creating a competing manager.
Rejected Alternatives: Standalone `HectonVoiceWarningManager` rejected due duplicate authority and merge conflict risk with parallel audio agents.
Scalability potential: Low keeps one owner and one route; Middle/High/Ultra can add richer editor/debug readers through the same owner.
Hardware Impact: Static estimate only. One route avoids extra scene components and hot polling; expected CPU gain is from less duplicated drain/sort work, pending profiler proof.

## Decision 0003: Vault Heap Shape

Problem: The old VWS pending queue was a `NativeArray<byte>` plus insertion sort, so priority was implicit enum order and could not express hull breach preempting battery through a continuous score.
Solution: Replace pending state with `NativeMinHeap<VocalWarningDTO>` over `BufferID.AudioVocalWarningQueue`, exact 16-byte DTO layout, plus separate heap/current/dispatch/profile Vault buffers.
Rejected Alternatives: `Queue<AudioClip>`, managed voice-event classes, or linear `List<T>.Sort` were rejected because they allocate, compare object identity, and cannot prove ARM64 DTO layout.
Scalability potential: Low clamps evaluation to 8 warnings; Middle admits common survival lanes; High evaluates 64 signal entries; Ultra keeps richer radio/spatial presentation while preserving the same authoritative facts.
Hardware Impact: Static estimate only. Heap depth is 64 max, O(log n) insert/pop, expected sort-path saving versus old repeated insertion/shift is 15-60us under dense mock input on i3/MX350 pending profiler proof.

## Decision 0004: Signal And Subtitle Route

Problem: The old hot dispatch published `VocalCueSignal` and also called `SubtitleManager.DisplaySubtitle` with fallback managed strings.
Solution: Keep `VocalCueSignal` for the vocal synthesis runtime and publish synchronized hash-only `SubtitleCueSignal`; localization/UI remains owned by subtitle runtime.
Rejected Alternatives: Direct UI mutation and fallback strings were rejected because the VWS hot route must not allocate or own text rendering.
Scalability potential: Low devices still receive a single hash per warning; Middle/High/Ultra can add richer subtitle presentation downstream without changing VWS dispatch.
Hardware Impact: Static estimate only. Removing fallback span/string path eliminates per-dispatch managed UI work and avoids subtitle-side scene coupling; expected hot saving is low tens of microseconds during warning bursts.

## Decision 0005: Interruption Math

Problem: Critical water breach must interrupt low battery deterministically without clip-name or string comparisons.
Solution: Assign hull breach base priority 1000 plus critical boost, battery base priority 120, then require `candidate > current + 180` and interrupt flags in `DispatchVoiceOverJob`.
Rejected Alternatives: Fading/mixing or audio-thread decision logic was rejected because audio DSP should consume a command, not own gameplay priority math.
Scalability potential: Low uses the same thresholds; Middle/High/Ultra only increase noncritical evaluation depth and presentation distortion/spatial blend.
Hardware Impact: Static estimate only. One float comparison and flag mask replaces audio-source arbitration; expected saving is under 10us per dispatch and prevents overlapping speech.

## Decision 0006: AUP Direction Hash

Problem: Directional warnings from radiation/fluid/pipe events can be wrong at large map coordinates if absolute positions are cast to float before subtraction.
Solution: `ResolveCompassDirectionHash` subtracts listener/threat AUP grid/local values in double precision, then casts the localized delta to float3 for `atan2`.
Rejected Alternatives: `AbsoluteUniversePosition.ToRuntimeFloat3()` inside the queue job and absolute float conversion were rejected because they hide global origin assumptions and precision loss.
Scalability potential: Low can ignore direction hash when no threat AUP exists; Middle/High/Ultra use the same hash and spend only a small atan2 cost for external threats.
Hardware Impact: Static estimate only. Direction hash cost is bounded by `MaxEvaluations`; low-tier worst case is 8 directional atan2 calls, expected under 20us on i3/MX350.

## Decision 0007: Rollback And Black Box

Problem: Audio queue state is presentation-only but still needs postmortem proof when priority math fails.
Solution: Document rollback exclusion in `Docs/ARCHITECTURE/VOCAL_WARNING_QUEUE_SHINOBU_352.md` and write a 300-frame Vault telemetry ring with dump path `Docs/AgentLogs/Dump_SHINOBU_352.bin`.
Rejected Alternatives: Putting VWS heap bytes into Merkle/StateRingBuffer was rejected because it would create false desyncs from transient audio presentation.
Scalability potential: Low records compact heap/current state; Middle/High/Ultra retain the same ring and richer editor views without altering save or rollback truth.
Hardware Impact: Static estimate only. A 64-byte x 300 ring is 19.2 KB resident and one row write per frame; expected CPU cost is under 5us.

## Decision 0008: Report Artifact Boundary

Problem: `Docs/Reports/AUDIO_OPTIMIZATION_REPORT.json` already exists as another agent's report, so overwriting it would destroy an unrelated proof artifact.
Solution: Create `Docs/Reports/AUDIO_OPTIMIZATION_REPORT_SHINOBU_352.json` and make the SHINOBU_352 scanner write that path.
Rejected Alternatives: Replacing the shared `AUDIO_OPTIMIZATION_REPORT.json` was rejected because the worktree contains parallel agent output and the project forbids reverting/overwriting unrelated work.
Scalability potential: Low/Middle/High/Ultra unaffected; this is documentation/proof routing only.
Hardware Impact: Static estimate only. No runtime impact.

## Decision 0009: Compile Wall Handling

Problem: First `dotnet build Hecton8.slnx --no-restore` surfaced missing `Temp/obj/*/project.assets.json`, many unrelated domain errors, and three SHINOBU_352 `CS8156` errors from passing `NativeArray` indexer expressions by `in`.
Solution: Fix SHINOBU_352 `CS8156` by copying indexed DTOs into locals before by-ref comparison; then shut down spawned build-server workers.
Rejected Alternatives: Editing VRSomatic, airlock, combat, PDA, input, gyro, tether, or package restore state was rejected because those are unrelated owners and outside VOCAL_WARNING_SYSTEM_AUDIO_QUEUE.
Scalability potential: Low/Middle/High/Ultra unchanged; this was a source-level legality fix.
Hardware Impact: Static estimate only. No runtime change; avoids compiler defensive-ref failure.

## Decision 0010: Local Vault Lane And Tuning Row

Problem: Adding SHINOBU-specific enum members to `H8Memory.cs` creates unnecessary core churn and merge risk while 20+ agents are editing shared infrastructure.
Solution: Keep existing audio warning lanes and add SHINOBU_352 extra rows as local casted `BufferID` constants `72430..72435` inside `VocalWarningSystem.cs`; store designer-editable priorities and interruption threshold in one 64-byte `VocalWarningTuningDTO` Vault row.
Rejected Alternatives: New `H8Memory` enum entries were rejected after polish because they touched a shared core file without needing a global public ID surface. Serialized MonoBehaviour tuning fields were rejected because gameplay priority truth must be Vault-owned and blittable.
Scalability potential: Low keeps the same DTO/threshold route with shallow evaluation; Middle/High/Ultra edit the same tuning row while richer presentation remains downstream.
Hardware Impact: Static estimate only. Removes a shared-core compile dependency and keeps hot priority reads as one 64-byte cache-line row; expected runtime delta is sub-1us versus constants, with better iteration isolation.

## Decision 0011: Critical Lane De-Duplication

Problem: The polish critical pre-pass made flood/fluid/pipe/oxygen/crush run before battery, but the old generic loops still processed those same lanes later. Because critical warning IDs intentionally bypass cooldown, the same hull breach hash could be inserted twice in one frame before heap coalescing.
Solution: Keep one critical pass for flood/fluid/pipe/oxygen/crush, remove the duplicate generic critical loops, and leave noncritical brownout/health/radiation/battery/survival scans after it. `NativeMinHeap.Insert` still coalesces by `AudioBankHashID` and promotes the higher priority row.
Rejected Alternatives: Relying only on heap coalescing was rejected because it burns queue scans and hides duplicate producer logic. Disabling critical cooldown bypass was rejected because water breach must preempt battery immediately.
Scalability potential: Low spends its 8 evaluation slots on survival-critical lanes first; Middle/High/Ultra admit broader noncritical lanes without changing priority truth.
Hardware Impact: Static estimate only. Avoids duplicate signal loop traversal and heap insertion attempts; expected saving is 5-25us during dense flood/oxygen frames on i3/MX350 pending profiler proof.

## Decision 0012: Editor Heap Proof Surface

Problem: The first gizmo exposed only pending count/current priority, not the raw heap rows requested by the XML task.
Solution: Add an editor-only `EditorTryGetHeapEntry` reader and display heap rows 0..2 with hash and priority in `VocalWarningQueueDebugGizmo`.
Rejected Alternatives: Debug logs and audio listening were rejected because they create managed noise and do not prove heap ordering from the scene view.
Scalability potential: Runtime unaffected across Low/Middle/High/Ultra; editor-only proof can be richer without entering player builds.
Hardware Impact: Editor-only. No player runtime cost.

## Decision 0013: Player-Scope Scanner Closure

Problem: The XML asks for Player, Combat, and Physiology audio-spam proof, but this project has Player/HectonPlayer-named files distributed outside an `Assets/_Project/Scripts/Player` directory.
Solution: Extend the editor scanner to scan recursive Audio/Gameplay/Physiology roots plus Player/HectonPlayer-named files outside those already scanned roots.
Rejected Alternatives: Keeping the missing directory in the scanner was rejected because it creates a false proof gap. Scanning every script recursively from `Assets/_Project/Scripts` was rejected because this agent's report would start auditing unrelated domains.
Scalability potential: Runtime unaffected; proof coverage improves without adding gameplay work.
Hardware Impact: Editor-only. No player runtime cost.

## Decision 0014: Unity Meta Stabilization

Problem: New Unity C# assets without committed `.meta` files receive importer-generated GUIDs later, creating nondeterministic editor churn for other agents.
Solution: Add explicit `.cs.meta` files for the three new SHINOBU_352 editor scripts.
Rejected Alternatives: Letting Unity auto-generate GUIDs was rejected because the project is already under heavy parallel edit pressure.
Scalability potential: Runtime unaffected; import stability improves across all hardware tiers.
Hardware Impact: Editor/import-only. No player runtime cost.

## Decision 0015: Single Tuning Snapshot Read

Problem: `EvaluateWarningPrioritiesJob` previously sanitized the 64-byte tuning row through `ResolveTuning` on every `TryQueue` call. That was legal but wasteful under dense mock input because the same Vault row was re-read repeatedly within one job.
Solution: Read and sanitize `VocalWarningTuningDTO` once at the start of `Execute`, then pass it by `in` to every `TryQueue` call.
Rejected Alternatives: Leaving per-signal tuning reads was rejected because it hides repeated cache-line fetches in the hottest priority production path. Copying tuning into managed fields was rejected because designers must mutate the Vault row without runtime object state.
Scalability potential: Low still processes 8 critical/top warnings; Middle/High/Ultra process deeper queues with one tuning snapshot instead of N repeated row reads.
Hardware Impact: Static estimate only. Saves one 64-byte Vault row read and clamp block per queued warning after the first; expected dense-frame saving is 2-12us on i3/MX350 pending profiler proof.

## Decision 0016: Roslyn AST Voice Scanner

Problem: Task 19 explicitly requires AST parsing, but the first SHINOBU_352 scanner used lexical line matching after the Player/HectonPlayer scope expansion.
Solution: Upgrade `OOP_Voice_Scanner_SHINOBU_352` to `CSharpSyntaxTree` AST primary scanning for vocal-warning regressions: `AudioSource.PlayOneShot`, `DisplaySubtitle`, `PlayWarning`, `Queue<AudioClip>`, voice queues/lists, and `Dictionary<string, AudioClip>`; lexical fallback now runs only on parser failure. The scanner writes a SHINOBU_352 sidecar and non-destructively upserts a section into the shared audio optimization report.
Rejected Alternatives: Lexical-only proof was rejected because comments and string literals can create false positives/negatives. A generic `.Play()` ban was rejected after it caught non-vocal continuous audio owners such as thruster/breathing loops; SHINOBU_352 owns Bitchin' Betty warning routing, not every authored audio loop in the project. Overwriting `AUDIO_OPTIMIZATION_REPORT.json` was rejected because it already contains other agents' proof sections.
Scalability potential: Runtime unaffected; Low/Middle/High/Ultra all benefit from keeping managed voice triggers out of player code.
Hardware Impact: Editor-only. No player runtime cost; architectural gain is preventing future `AudioSource` and managed queue regressions before they enter hot gameplay paths.

## Decision 0017: Raw Blackbox Dump

Problem: The first blackbox fault dump wrote every telemetry field through `BinaryWriter`. That was a cold path, but it still violated the XML requirement for a raw `ReadOnlySpan<byte>` dump and made the dump schema depend on managed writer order instead of a fixed blittable header.
Solution: Add `VwsTelemetryDumpHeader=32` and write the header plus oldest-to-newest raw `VwsTelemetryEntry=64` rows through `FileStream.Write(ReadOnlySpan<byte>)` from the native telemetry ring. The dump latch now flips only after a successful file write.
Rejected Alternatives: Keeping `BinaryWriter` was rejected because it is a managed serializer on the proof route. Writing only the physical ring order was rejected because wrapped rings are harder to inspect during forensic replay.
Scalability potential: Runtime hot path unchanged across Low/Middle/High/Ultra; fault replay gains a fixed binary schema for every hardware tier.
Hardware Impact: Hot path no change. Fault path removes 14 per-row managed writer calls, replacing them with at most three span writes; expected cold dump saving is hundreds of microseconds when a dump is emitted.

## Decision 0018: Raw Heap Node Mutation

Problem: `NativeMinHeap<VocalWarningDTO>` originally used `NativeArray` indexer reads/writes. It was allocation-free, but Task 08 explicitly asks for raw pointer swaps and the CS1612 mandate asks for in-place mutation through `UnsafeUtility.AsRef`.
Solution: Add `NodeRef` and `StateRef` helpers using `NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks` plus `UnsafeUtility.AsRef`, then route insert, pop, expire discard, sift-up, sift-down, and swap through ref-return node/state access.
Rejected Alternatives: Leaving indexer writes was rejected because it obscures defensive-copy risks and does not satisfy the raw pointer swap proof. Introducing an interface-based heap comparator was rejected because IL2CPP/Burst hot paths should stay concrete.
Scalability potential: Low still uses 8-row admission; Middle/High/Ultra get the same O(log 64) heap with fewer indexer copies as queue depth grows.
Hardware Impact: Static estimate only. Removes repeated NativeArray indexer copy/writeback patterns in heap swaps; expected dense-frame saving is 3-15us on i3/MX350 pending profiler proof.

## Decision 0019: Owner-Local Fallback Frame Identity

Problem: Dispatcher frames use `DispatcherTimingDTO.FrameId`, but the fallback `IUpdatable`/`ISlowTickable` route and editor mock seed still read `Time.frameCount`. That does not allocate, but it is still Unity `Time` state in a route that should be owner-local and rollback-fenced.
Solution: Add `_ownerFrameCounter` and `NextOwnerFrameId`, use it for fallback `Tick`, `SlowTick`, and mock threat seed. The dispatcher route still passes the authoritative dispatcher frame id.
Rejected Alternatives: Keeping `Time.frameCount` was rejected because the mandate forbids leaning on Unity time for critical state transitions. Using `DateTime` or random seeds was rejected because it would make mock stress runs less reproducible.
Scalability potential: Low/Middle/High/Ultra unchanged; this is frame identity hygiene and deterministic fallback behavior.
Hardware Impact: Static estimate only. No measurable runtime cost; removes Unity `Time` property reads from the VWS fallback path.

## Decision 0020: Direct SignalBus Dispatch And Raw State Refs

Problem: Final voice/subtitle publication still routed through `GlobalSignals.Publish`, and several owner state rows used `NativeArray` indexer writeback. The route was allocation-free, but it weakened the proof that SHINOBU_352 uses the typed hot corridor and raw Vault mutation end to end.
Solution: Publish dispatch directly through `SignalBus<VocalCueSignal>.TryPush` and `SignalBus<SubtitleCueSignal>.TryPush`, record rejected cue/subtitle lanes as heap fault bits, and reset current playback if the cue itself is rejected. Initialization, clear, telemetry, dispatch, current-state, cooldown, flag, severity, and source writes now use `NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks` plus `UnsafeUtility.AsRef`.
Rejected Alternatives: Keeping the `GlobalSignals.Publish` wrapper was rejected because direct typed lanes are the first-party hot broadcast route and already apply `SignalBus<T>` finite guards. Leaving indexer writes was rejected because Task 20 requires proof against defensive writeback paths in the priority queue owner.
Scalability potential: Low devices still push one cue and optional subtitle; Middle/High/Ultra keep the same signal truth while richer DSP/UI presentation downstream can scale independently.
Hardware Impact: Static estimate only. Direct typed publish removes one wrapper call per accepted cue/subtitle and rejected-lane state no longer lies about active playback; raw state refs remove repeated indexer writeback patterns, expected dense-frame saving is low single-digit microseconds on i3/MX350 pending profiler proof.

## Decision 0021: Compile Gate Preservation After Context Compaction

Problem: Context resumed with Task 20 still pending a guarded compile retry, but current system load violates the project build gate.
Solution: Re-read `Status_SHINOBU_352.md`, `Rationale_SHINOBU_352.md`, `CURRENT_BATCH.md`, `AGENTS.md`, and the relevant Zero-GC/ARM64/Signal mandates; re-extract SHINOBU_352 with an attribute-aware regex; sample CPU and compiler processes before any build command. Latest sample: CPU 58.2, zero `dotnet/csc` workers.
Rejected Alternatives: Launching `dotnet build` above the 50% CPU gate was rejected by AGENTS policy. Killing unknown `dotnet` workers was rejected while they existed because they could belong to other agents or Unity tooling.
Scalability potential: Runtime unchanged. Preserving the compile gate protects iteration velocity while SHINOBU_352 remains source-audited and ready for the next legal compile window.
Hardware Impact: No runtime delta. Avoids extra IO/CPU pressure on an already saturated development machine.

## Decision 0022: Guarded Compile Retry And External Wall

Problem: Task 20 requires compile evidence after SHINOBU_352 `CS8156` was fixed.
Solution: Waited until CPU 36.3 and zero `dotnet/csc` workers, then ran one `dotnet build Hecton8.slnx --no-restore`. The build failed on six missing `Temp/obj/*/project.assets.json` files and two unrelated `Hecton8.Core` construction/habitat namespace errors. No new SHINOBU_352 compiler error appeared in the visible output. `dotnet build-server shutdown` was run after the attempt and succeeded.
Rejected Alternatives: Editing `Construction/HatchLockJobs.cs`, `Construction/BulkheadContainmentRuntime_HatchLocks.cs`, package restore state, or third-party generated projects was rejected because those are outside VOCAL_WARNING_SYSTEM_AUDIO_QUEUE and would break ownership boundaries.
Scalability potential: Runtime unchanged. SHINOBU_352 remains source-audited behind an external compile wall.
Hardware Impact: No runtime delta. Build attempt consumed compile time only; build servers were shut down to release worker processes.

## Decision 0023: SubtitleCueSignal Route Correction

Problem: Task 14 explicitly requires `SubtitleCueSignal`, but the post-compaction proof surface still named legacy `SubtitleSignal`, and VWS needed deterministic conversion from voice dispatch fields into the 16-byte subtitle cue payload without reading UI-owned clocks.
Solution: `PublishDispatchIfNeeded` now writes `SubtitleCueSignal` directly with token hash, bounded millisecond duration, compact interrupt/direction flags, and `StartAudioFrame=0` as a sentinel. `BabelSubtitleSyncRuntime` resolves that sentinel to its current `s_audioFrameClock`, keeping the audio queue decoupled from the UI concrete runtime while preserving synchronization in the subtitle owner phase.
Rejected Alternatives: Referencing `BabelSubtitleSyncRuntime.CurrentAudioFrame` from VWS was rejected as a concrete cross-domain dependency. Publishing legacy `SubtitleSignal` was rejected because it does not satisfy the XML lane. Creating a new subtitle lane was rejected because `SubtitleCueSignal` already exists and is configured by the subtitle owner.
Scalability potential: Low still publishes one 16-byte cue per accepted warning. Middle/High/Ultra can enrich downstream subtitle presentation from the same token hash and flags without adding VWS work or changing warning truth.
Hardware Impact: Static estimate only. One primitive duration clamp and byte flag pack are sub-1us on i3/MX350; removing the legacy bridge avoids extra wrapper work and keeps the hot route typed and finite.

## Decision 0024: Build Gate Held For Active Dotnet Workers

Problem: After the `SubtitleCueSignal` correction, C# files changed and compile evidence is desirable, but AGENTS forbids launching `dotnet build` when another `dotnet`/`csc` worker is active.
Solution: Sampled the build gate only. CPU was 30.5%, but seven `dotnet` processes were active, so no build was launched.
Rejected Alternatives: Starting a build anyway was rejected by compile discipline. Killing unknown `dotnet` workers was rejected because they may belong to Unity or another active agent.
Scalability potential: Runtime unchanged. Preserving the gate avoids IO/CPU contention during parallel agent work.
Hardware Impact: No runtime delta. Prevents unnecessary compile pressure on the shared workstation.

## Decision 0025: Subtitle Contract And Asmdef Boundary Audit

Problem: Task 14 requires the exact `SubtitleCueSignal` lane. Source review found another same-name DTO in the mod sandbox and multiple Audio/UI asmdefs, so a naive assumption could create either a duplicate type error or a sibling assembly dependency.
Solution: Verified the same-name mod DTO lives in `namespace Hecton8.Modding` and remains a separate mod-lane payload. Verified root `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs` and root `Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs` are not inside the child `Hecton8.Audio.*` or `Hecton8.UI.*` asmdefs; they are covered by the parent Core assembly surface, so no new sibling runtime reference was introduced. Also verified existing code uses `[ReadOnly, NoAlias] NativeArray<T>.ReadOnly` job fields and raw `FileStream.Write(ReadOnlySpan<byte>)` dump paths, matching VWS source patterns.
Rejected Alternatives: Moving `SubtitleCueSignal` into a new contract file was rejected because the current asmdef boundary does not require a core-file move and the user mandate confines SHINOBU_352 to its domain unless necessary. Treating `Hecton8.Modding.SubtitleCueSignal` as the owner subtitle lane was rejected because its ABI is duration/priority oriented and not the UI subtitle owner route.
Scalability potential: Runtime unchanged. Low/Middle/High/Ultra continue to use the same 16-byte UI subtitle cue; assembly proof only protects iteration speed and route ownership.
Hardware Impact: No runtime delta. Avoids a future compile-wall edit in shared contracts and preserves a single typed hot lane for subtitle sync.

## Decision 0026: Build Gate Held For Active Dotnet Workers After Boundary Audit

Problem: A fresh compile would be useful after the source-boundary audit, but AGENTS forbids launching `dotnet build` while another `dotnet`/`csc` worker is active.
Solution: Re-sampled the build gate twice. CPU was legal at 15.3% and later 39.8%, but seven `dotnet` processes were active both times, so no build was launched and no unknown worker was killed.
Rejected Alternatives: Starting a parallel build was rejected by compile discipline. Killing active `dotnet` workers was rejected because they may belong to Unity import or another agent.
Scalability potential: Runtime unchanged. Preserving the compile gate avoids IO contention while the VWS route remains source-audited.
Hardware Impact: No runtime delta. Prevents avoidable workstation contention during parallel production work.
