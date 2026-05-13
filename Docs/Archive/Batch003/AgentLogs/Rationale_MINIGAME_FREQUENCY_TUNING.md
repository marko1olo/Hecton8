# Rationale_MINIGAME_FREQUENCY_TUNING

Status: PENDING VERIFICATION
Domain: Presentation & UX / Frequency Tuning (Scanning)
Prompt: Scanner Artifact Decryption

## Initial Mandate Selection

Problem: The scanner artifact minigame requires hot-path waveform math, GPU presentation, input, haptics, audio sync, and no Canvas/LineRenderer fallback.
Solution: Use selected mandates for diegetic UI, zero-GC, NativeArray jobs, GPU buffer rendering, MX350 scaling, device abstraction, and DSP parameter sync.
Rejected Alternatives: uGUI, LineRenderer, direct singleton calls, Unity Input polling, AudioSource event strings, and runtime material clones. They violate prompt or project mandates.
Scalability potential: Low uses 32-point wave arrays and cheapest visual tube; Middle/High use 128 points and richer visual distortion; Ultra can add heavier shader detail while preserving same data contract.
Hardware Impact: Estimated gain for low-end silicon such as i3/MX350 is avoidance of Canvas rebuilds and LineRenderer CPU churn; expected saving is pending measurement, target <0.1 ms system CPU.

## Decision Log

### Loop 1 - Tasks 1-5

Problem: The legacy spectrogram minigame was a Canvas/uGUI polling panel and no `ScannerToolActiveSignal` lane existed.
Solution: Replaced `PDADecryptionSpectrogramPanel` with a dispatcher-owned native waveform system; added `ScannerToolActiveSignal` to `GlobalSignals`; published scanner state from `ScannerTool` only on active/target changes to prevent unbounded queue growth.
Rejected Alternatives: Keeping slider wrappers around `AtlasSignalDecoder.SubmitWaveMatch` was rejected because it preserved Canvas rebuild cost and the old frequency/phase interface. Publishing scanner-active every frame was rejected because a disabled PDA would leave the native queue to grow.
Scalability potential: Low uses 32 allocated samples and 62 GPU segments; Middle/High/Ultra use 128 samples and 254 GPU segments through the same contract.
Hardware Impact: Estimated i3/MX350 saving is removal of slider/image layout churn and managed TMP formatting from the hot path; projected CPU saving 40-90 microseconds per active frame versus the old panel, pending Unity profiler proof.

Problem: The requested `GlobalProfileManager.Difficulty` property is absent in this branch.
Solution: Bound hard-mode drift to the existing `RunModifierController.IsNightmareModeActive` and `DynamicDifficultyDirector.Current` contracts, which are the live registered difficulty services.
Rejected Alternatives: Adding a new GlobalProfileManager difficulty property was rejected as cross-domain profile mutation outside this prompt.
Scalability potential: Low/normal avoids drift; hard/nightmare adds one slow deterministic noise sample on the main thread per frame.
Hardware Impact: Estimated cost is below 2 microseconds on low-end silicon; no allocation.

Problem: Compile verification is blocked by unrelated project errors before this slice can be validated by `dotnet build`.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` and captured the dependency wall: missing `Hecton8.Cartography`, `SubmarineAutoLevelBallastController`, `HabitatDeconstructionTelemetryEntry`, and an unrelated `HectonBiolumManager` interface mismatch.
Rejected Alternatives: Editing Cartography/Ballast/Biolum domains was rejected because this agent is bounded to the minigame/presentation slice.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact; compile proof remains blocked by external dependencies.

### Loop 2 - Tasks 6-10

Problem: The minigame needed waveform generation, input response, staged locks, and error reduction without managed per-frame math.
Solution: Added Burst `FrequencyWaveGenerateJob` and `FrequencyWaveErrorJob`; left-stick Y maps to amplitude, right-stick X maps to frequency through cached `PlayerInputState`; three deterministic stage targets lock after 2 continuous seconds under 0.05 normalized error.
Rejected Alternatives: Main-thread `Mathf.Sin`/`Mathf.Abs`, coroutines, and `Slider` values were rejected because they add managed presentation work and preserve the old control model.
Scalability potential: Low/MX350 schedules 32 iterations; Mid/High/Ultra schedules 128 iterations and richer shader presentation from the same buffer.
Hardware Impact: Estimated i3/MX350 CPU cost is 5-18 microseconds for 32-point Burst math and error reduction after scheduling overhead; no managed GC.

Problem: NaN proof cannot be a chat claim.
Solution: Added a 300-frame native telemetry ring and cold binary dump to `Docs/AgentLogs/Dump_MINIGAME_FREQUENCY_TUNING.bin` before sanitizing invalid job results.
Rejected Alternatives: Logging strings or ignoring invalid values was rejected because the Black Box mandate requires crash-state evidence.
Scalability potential: Ring is fixed-size on all tiers; Ultra can add richer shader visuals without changing telemetry.
Hardware Impact: Hot-path cost is one fixed struct write per completed frame; estimated under 2 microseconds on i3/MX350.

### Loop 3 - Tasks 11-15

Problem: The wave match needed visible PDA feedback without resurrecting Canvas geometry, LineRenderer CPU rebuilds, or per-frame managed upload arrays.
Solution: Reused the project `GraphicsBufferUploadUtility` lock/memcpy path, kept persistent segment and indirect-args buffers, and drew target/player wave tube segments through `Graphics.RenderMeshIndirect` using the dedicated `Hecton8/PDA/FrequencyTuningWave` shader.
Rejected Alternatives: `LineRenderer`, uGUI `Image`, TMP graph text, and CPU mesh regeneration were rejected because they keep presentation work on the main thread and fail the BRG/PDA requirement.
Scalability potential: Low/MX350 uploads 62 GPU segment instances from 32 samples; Middle/High/Ultra upload 254 instances from 128 samples and can increase tube pulse/color richness in shader without changing CPU contracts.
Hardware Impact: Estimated i3/MX350 saving is 25-70 microseconds versus LineRenderer/Canvas refresh for two curves; actual profiler capture remains blocked by project compile errors.

Problem: Feedback had to reach visor, audio, and haptics without direct singleton calls or new scene dependencies.
Solution: Pushed `_HectonFrequencyTuningError01` as a shader global consumed by `HectonVisorUberPostFeature`, published `ToolAcousticSignal`, raised the existing interaction signal, and queued sinusoidal tool haptics scaled by inverse error.
Rejected Alternatives: Per-frame material cloning, direct `AudioSource` mutation, and UnityEvent/string haptic names were rejected because they allocate or couple this minigame to concrete presentation objects.
Scalability potential: Low tier uses scalar feedback only; High/Ultra can spend the same scalar on stronger chromatic/hum shader work and richer haptic envelopes.
Hardware Impact: Estimated low-end cost is below 10 microseconds per 0.1 second feedback tick plus one global float change when error changes; no managed allocation on the hot path.

Problem: Loop 3 compile proof is blocked before the frequency-tuning files are reached.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:BuildProjectReferences=false`; the current wall is `Hecton8.Cartography`/`CartographyAup`/`MapRevealSignal` in PDA map tracking plus `LabelSwapScheduler.PendingSwap`.
Rejected Alternatives: Editing Cartography or UI scheduling dependencies was rejected because it is outside this prompt and would violate domain boundaries.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact; source verification continues until the dependency wall is cleared by owners.

### Loop 4 - Tasks 16-19

Problem: The prompt names `GlobalProfileManager.Difficulty`, but `GlobalProfileManager` in this branch has no difficulty property; difficulty is represented by `RunModifierController` and `DynamicDifficultyDirector.Current`.
Solution: Kept the minigame read-only against existing meta services and applied slow deterministic `noise.cnoise` drift to target frequency and amplitude only when nightmare or high pressure modifiers are active.
Rejected Alternatives: Adding `GlobalProfileManager.Difficulty` or mutating profile data from the minigame was rejected as cross-domain profile ownership violation. Random drift was rejected because it makes match windows nondeterministic.
Scalability potential: Low/normal path has no drift. Middle/High/Ultra can use drift to make alien interference feel less static without extra allocations.
Hardware Impact: Estimated i3/MX350 drift cost is 1-3 microseconds per active frame on hard/nightmare; zero cost when not active.

Problem: Low-tier math LOD initially used 32 active samples but still allocated high-tier arrays, which wasted native memory and did not literally reduce array size.
Solution: Changed native target/player arrays and GPU segment buffer to allocate from the resolved point count: Low/MX350/Unknown allocate 32 samples and 62 GPU segments, while Mid/High/Ultra allocate 128 samples and 254 GPU segments.
Rejected Alternatives: Keeping fixed 128-size arrays with an active count was rejected after re-reading Task 18. Dynamic reallocation every frame was rejected because tier changes are not a hot-path behavior.
Scalability potential: Low is a cheap 32-sample approximation. Middle/High/Ultra keep 128 points and can spend saved CPU/GPU budget on shader overkill instead of more CPU math.
Hardware Impact: Estimated MX350 saving versus 128 active samples is 8-20 microseconds CPU scheduling/work plus 192 fewer GPU segment instances per active frame.

Problem: Origin shifts must not perturb the matching math.
Solution: Wave math uses normalized local sample indices and local PDA transform only for final draw placement; no `AbsoluteUniversePosition`, AUP shift signal, or world-space coordinate enters target/player/error calculations.
Rejected Alternatives: World-position wave phase or AUP-seeded phase was rejected because origin rebases would appear as minigame drift unrelated to player input.
Scalability potential: Same contract across tiers; Ultra can add shader shimmer after local transform without touching solve math.
Hardware Impact: No measurable hot-path cost; avoids rebase correction work entirely.

Problem: Omega compile proof is still blocked by dependency errors outside this domain.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:BuildProjectReferences=false`; current errors are missing `Hecton8.Cartography`, missing `Hecton8.Physics.Determinism/InputSignal`, and downstream map types. The Burst error job signature and `math.abs` body are source-verified.
Rejected Alternatives: Editing Cartography/Physics domains or declaring success without compile evidence was rejected.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact; compile status remains dependency-blocked, not verified.

## OMEGA POLISH CHANGES

Problem: The final anti-bloat scan found one literal floating-point division in the deterministic stage seed normalizer.
Solution: Replaced `(1f / 16777215f)` with the precomputed `Hash24ToUnit` reciprocal constant and kept all hot-path scaling on multiplications or `math.rcp`.
Rejected Alternatives: Leaving the literal division was rejected because the polish mandate requires reciprocal precomputation. A lookup table for the actual sine waves was rejected because the core prompt explicitly requires `math.sin` in the Burst generation job.
Scalability potential: Low/MX350 keeps 32 samples and 62 GPU segments; Middle/High/Ultra keep 128 samples and 254 GPU segments. Ultra visual overkill is shader-side pulse/color work, not higher CPU sample count.
Hardware Impact: The reciprocal constant saves sub-microsecond scalar setup; the real MX350 gain remains the 32-sample LOD and 192 fewer GPU segment instances per active frame.

Problem: Silo audit identified existing `ScannerTool` string formatting outside this minigame insertion.
Solution: Left existing scanner operational text code untouched and kept new tuning publication to fixed-size `ScannerToolActiveSignal`; the frequency panel itself has no `foreach`, `string.Format`, `.ToString()`, `Mathf.Abs`, `math.sqrt`, `math.normalize`, uGUI, LineRenderer, or `MinigameManager.Instance`.
Rejected Alternatives: Refactoring broad scanner UI strings was rejected as an unrelated scan-tool domain change and a likely conflict with parallel agents.
Scalability potential: The new minigame path is scalar/native/GPU-buffer based on every tier.
Hardware Impact: No new managed formatting cost from this prompt.

Problem: Final build validation remains globally red.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` after polish. Errors are missing `Hecton8.Core.Memory`/`IDataVault`/`SystemID`, missing `Hecton8.Physics.Determinism` signals, missing `Hecton8.Cartography` types, and downstream `PDAMapTab`/`PlayerExplorationTracker` failures before this minigame slice can be isolated.
Rejected Alternatives: Marking verified master grade was rejected because the compiler wall is objective.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact; status stays PENDING VERIFICATION due external compile dependencies.

Final Git Diff: tracked files changed: `GlobalSignals.cs`, `SignalCryptographySmokeTester.cs`, `ScannerTool.cs`, `PDADecryptionSpectrogramPanel.cs`, `HectonVisorUberPostFeature.cs`. New untracked files: `Hecton_PDA_FrequencyTuningWave.shader`, `Status_MINIGAME_FREQUENCY_TUNING.md`, `Rationale_MINIGAME_FREQUENCY_TUNING.md`, `LOG_MINIGAME_FREQUENCY_TUNING.md`. `git diff --check` passed for the touched tracked files; build server was shut down after validation.

Cinematic Cheats Used: local normalized sine samples instead of physical signal simulation; BRG segment/tube impostor shader instead of LineRenderer geometry; scalar visor aberration and acoustic/haptic error feedback instead of simulated radio interference; deterministic hard-mode drift with cheap coherent noise; Math LOD 32/128 instead of one middle-ground sample count.

## 2026-05-13 AAA Recheck - No Build Per User

Problem: `LateFrameTick` still sampled `Time.deltaTime` when committing the waveform result, which reintroduced a Unity static-time read into a dispatcher-owned system.
Solution: Cache sanitized `Tick(float deltaTime)` as `_lastTickDeltaTime` and use it in `CommitWaveResult` after the scheduled jobs complete in late frame.
Rejected Alternatives: Sampling `Time.deltaTime` in `LateFrameTick` or estimating from `Time.unscaledTime` was rejected because the dispatcher already owns the authoritative delta.
Scalability potential: Low/Middle/High/Ultra all share one deterministic cadence; no tier-specific branch is needed.
Hardware Impact: Estimated runtime saving is 0 microseconds; value is determinism and reduced timing drift on low-end i3/MX350 frames under stalls.

Problem: The first BRG path drew point/bead impostors, which was acceptable for a cheap prototype but visually weaker than the requested sine-wave hacking tube.
Solution: Replaced the GPU payload with `FrequencyTuningWaveGpuSegment` containing center/radius, tangent/length, and color. The Burst generator emits 62/254 continuous segments and uses `math.rsqrt` for tangent normalization; the shader now binds `_HectonFrequencyTuningSegments` and expands each instance into a continuous strip.
Rejected Alternatives: `LineRenderer`, CPU mesh rebuilds, geometry shader extrusion, and per-frame managed vertex arrays were rejected because they move presentation work back onto the CPU or increase platform risk.
Scalability potential: Low = 31 target + 31 player segments; Middle/High/Ultra = 127 target + 127 player segments with the same upload contract. Ultra can add shader-side scan pulse and chroma without increasing CPU sample count.
Hardware Impact: Low-end i3/MX350 saves roughly 0.2 microseconds from 2 fewer draw instances versus the bead path; the real gain is higher visual quality at the same CPU budget.

Problem: The editor smoke tester extracted the no-argument error job when checking generation-job sine math.
Solution: Changed the source audit to extract `public void Execute(int index)` for generation and the no-arg `Execute()` for the error reducer.
Rejected Alternatives: Broad file-level contains checks were rejected because they can pass while the audited method body is wrong.
Scalability potential: No runtime impact; QA catches regressions in Low/Middle/High/Ultra contracts.
Hardware Impact: 0 microseconds runtime; editor-only verification accuracy improved.

Problem: User explicitly requested no `dotnet build`.
Solution: Verification used `rg` forbidden-token scans, required-token scans, trailing-whitespace scan, and `git diff --check` only. Results: no forbidden-token matches, required segment/rsqrt/indirect/native/Burst tokens present, no trailing whitespace, and only CRLF normalization warnings from `git diff --check`.
Rejected Alternatives: Launching `dotnet build` or Unity editor validation was rejected because it violates the current instruction and the project is already dependency-blocked.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact; status remains PENDING VERIFICATION until compile/runtime owners unblock the project.

## 2026-05-13 Activation/Lifecycle Recheck - No Build Per User

Problem: `ScannerTool` correctly avoids per-frame scanner-active spam, but a newly created PDA panel could miss an already-active scanner if the frame snapshot was drained before the panel existed.
Solution: Added a latest `ScannerToolActiveSignal` snapshot plus sequence to `GlobalSignals`; `PDADecryptionSpectrogramPanel` falls back to it only when no frame signal is available and the sequence has not already been consumed.
Rejected Alternatives: Publishing scanner-active every tool tick was rejected because it would grow the signal lane when no panel is active. Direct scanner lookup from the PDA was rejected because it reintroduces cross-domain coupling.
Scalability potential: Low/Middle/High/Ultra all use one fixed 32-byte latest signal and no extra native allocation. High-end visual overkill remains shader-side, not signal-traffic-side.
Hardware Impact: Estimated low-end i3/MX350 gain is stability rather than frame time: zero queue spam and no missed activation; runtime cost is one sequence read on active panel ticks.

Problem: The panel sampled Unity global time/frame values in several render, feedback, telemetry, and unlock paths.
Solution: Cached `Time.unscaledTime` and `Time.frameCount` once in `Tick`, then reused `_lastTickUnscaledTime` and `_lastTickFrame` through late-frame rendering, hard drift, audio, telemetry, and blueprint unlock.
Rejected Alternatives: Leaving scattered static time reads was rejected because it makes a single visual frame less coherent. Requesting a new dispatcher time API was rejected as cross-domain churn during a minigame pass.
Scalability potential: Low/Middle/High/Ultra share coherent per-frame timing; Ultra can spend shader ALU on richer pulse/chroma without adding CPU time reads.
Hardware Impact: Sub-1 microsecond estimated saving on i3/MX350; main value is deterministic frame coherence and lower timing jitter under stalls.

Problem: Static QA needed to catch regressions in these lifecycle fixes.
Solution: Extended `SignalCryptographySmokeTester` to require the latest scanner-active fallback and to assert late-frame code contains no `Time.` access.
Rejected Alternatives: Relying on manual review was rejected because source-body smoke checks already exist for this feature.
Scalability potential: No runtime impact; editor-only gate protects all tier paths.
Hardware Impact: 0 microseconds runtime.

Problem: User again explicitly prohibited `dotnet build`.
Solution: Verification stayed source-only: forbidden-token scan, required-token scan, trailing-whitespace scan, targeted status check, and `git diff --check`. `git diff --check` produced only CRLF normalization warnings on touched C# files.
Rejected Alternatives: Build, Unity play mode, and editor console validation were rejected for this turn.
Scalability potential: No runtime impact.
Hardware Impact: No runtime impact; status remains PENDING VERIFICATION.
