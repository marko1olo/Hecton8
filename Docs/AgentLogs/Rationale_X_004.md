# Rationale_X_004

Status: LOOP 43 GRAPHICSBUFFER/VISUAL-SYNC CLOSURE APPLIED ACROSS 20 TRACKED RUNTIME FILES; RUNTIME `SetData`, `UploadArraySetData`, AND `ReadPixels` ARE ABSENT FROM `Assets/_Project/Scripts`; TOUCHED HOT-METHOD GATE IS GREEN (`PHYSICS_HULL_UI_AUDIO_TOUCHED_HOT_HITS=0`) AND UI STRING FRAME GATE IS GREEN (`UI_STRING_FRAME_HITS=0`), BUT ROOT-COVERAGE ROSLYN/BUILD GATES ARE BLOCKED BY CPU 73-88 AND ACTIVE DOTNET/VBCSCOMPILER ROWS; UNITY RUNTIME/PROFILER NOT RUN

## Session Bootstrap

Problem: X_004 needs persistent memory before code because context compression is expected and chat is not authoritative.
Solution: Created `Docs/Tasks/Status_X_004.md` and this rationale file before code mutation.
Rejected Alternatives: Chat-only tracking; stale archive logs; broad cross-agent context.
Scalability potential: File-backed task state has no runtime cost and prevents duplicate agent work.
Hardware Impact: 0 us runtime; editor-only coordination.

Problem: Domain boundary must be constrained to Presentation & UX and cross-domain proof tooling.
Solution: Use the X_004 prompt, `Docs/Actual Domains of Project.txt`, and relevant `.agents-skills` mandates before code selection.
Rejected Alternatives: Editing simulation systems speculatively without a hit list.
Scalability potential: Low/Middle/High/Ultra presentation cost can scale while simulation truth stays invariant.
Hardware Impact: Expected benefit depends on findings; no metric claimed before scan.

## Static Audit Tool

Problem: Presentation pollution was spread across thousands of files; manual rg would miss namespace/type leaks and hot-method context.
Solution: Added `Tools/PresentationDecouplingAudit`, a Roslyn AST scanner that classifies runtime/editor/simulation/presentation files, hot methods, forbidden presentation APIs, mutable truth writes, UI string GC risks, and emits a JSON proof artifact.
Rejected Alternatives: Unity editor-only scanner, rg-only token matching, or modifying systems before a hit list.
Scalability potential: Low tier avoids CPU presentation leakage; middle/high/ultra can spend saved main-thread time on richer GPU visuals without changing simulation truth.
Hardware Impact: Runtime cost 0 us. Review cost estimated down by 20-45 us per finding cluster; no frame metric claimed.

Problem: Initial static matching risked false positives by treating identifiers like `MaterialDensity` as `Material`.
Solution: Added whole-identifier token matching for forbidden type names and a presentation-name classifier for Biolum/GPUScatter/HLOD/Impostor/Renderer/Shader/Visual file families.
Rejected Alternatives: Broad substring matching, blanket whitelisting `World/`, or deleting findings manually from the report.
Scalability potential: Cleaner proof keeps low/middle/high/ultra decisions focused on true CPU/GPU boundary leaks.
Hardware Impact: Runtime cost 0 us. Static report triage reduced; exact rerun blocked by active dotnet/csc.

## Runtime Boundary Decisions

Problem: `HabitatFluidIncursionDirector.PostFixedTick` wrote `_H8GlobalFloodScalar` through `Shader.SetGlobalFloat`, coupling fluid simulation completion to presentation state.
Solution: Store the scalar in `_pendingFloodScalar` and flush it from `IRenderable.Render` with a dirty bit.
Rejected Alternatives: Leaving the shader write in PostFixedTick, polling GlobalRegistry from a renderer, or adding a managed event.
Scalability potential: Low tier skips redundant dirty frames; middle/high/ultra keep the same fluid truth while shader-side flood/muffle visuals scale continuously by `GlobalQualityWeight`.
Hardware Impact: Estimated 2-5 us avoided on dirty flood frames on i3/MX350 class hardware; 0 us measured because compile/profiler gate was blocked.

Problem: `SoundscapeSystem.SlowTick` wrote `_SoundscapeDepthTier` directly to shaders during the slow gameplay/audio lane.
Solution: Queue `_pendingShaderTier` and register `ILateFrameTickable`; `LateFrameTick` performs the shader write once per dirty tier.
Rejected Alternatives: `Update`, `LateUpdate`, `AudioSource`-style managed dispatch, or direct shader writes in the slow tick.
Scalability potential: Low tier receives sparse tier globals; middle/high/ultra can layer richer GPU soundscape-reactive visuals from the same immutable tier snapshot.
Hardware Impact: Estimated 1-3 us avoided per tier change on i3/MX350; 0 us measured.

Problem: `FloraInteractionManager.Tick` uploaded prop-wash and interaction buffers directly to global shader state while also handling gameplay/environment interaction calculations.
Solution: Queue a fixed visual snapshot in Tick and flush prop-wash globals, interaction buffer, and interaction count from `LateFrameTick`.
Rejected Alternatives: Running buffer uploads in Tick, creating a new renderer dependency, or allocating a managed event payload.
Scalability potential: Low tier can publish fewer interaction points; middle/high/ultra can increase visual point density and shader overkill without changing gameplay density queries.
Hardware Impact: Estimated 6-8 us avoided per active flora interaction frame on i3/MX350; 0 us measured.

Problem: Presentation-owned renderers were still doing GPU/global material work from `Tick`, which violates the X_004 phase rule even when the file is not simulation-owned.
Solution: Added `ILateFrameTickable` to Biolum diffusion, GPU scatter, distant landmark, HLOD, and octahedral impostor renderers; changed their dispatcher registration to late-frame; left `Tick` as a no-op compatibility stub.
Rejected Alternatives: Classifier-only suppression, keeping renderer `Tick` hot because it is visually owned, or moving work into Unity `LateUpdate`.
Scalability potential: Low tier keeps cheap late-frame visual commits; middle/high/ultra can spend GPU budget on scatter, biolum volume, and impostor richness without leaking into pre-visual phases.
Hardware Impact: Estimated 18 us pre-visual contention removed on i3/MX350 class hardware; 0 us measured.

Problem: The mutable-presentation proof was polluted by generic `TryResolveHandle` hits that represent read-handle consumers, not write access.
Solution: Removed `TryResolveHandle` from mutable detection and matched exact invocation member names for `TryWriteHandle`, `ResolveWriteHandle`, `ResolveWrite`, `AcquireWrite`, `TryAcquireWrite`, `TryGetMutable`, `GetMutable`, and `TryResolveMutable`.
Rejected Alternatives: Keeping 262 false positives, post-processing the JSON, or trusting presentation scripts without a scanner.
Scalability potential: Static proof stays actionable across low/middle/high/ultra builds; false-positive removal has no runtime cost.
Hardware Impact: 0 us runtime; reviewer triage cost reduced.

## Verification Boundary

Problem: Static proof and compile proof were required, but the batch forbids dotnet/csc launches while external compiler work is active or CPU is above the threshold.
Solution: Waited for the gate to clear, reran the analyzer, then ran `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false`.
Rejected Alternatives: Violating the explicit no-dotnet-under-load rule, killing other agents' compiler processes, or claiming a build pass without evidence.
Scalability potential: No runtime implication; protects parallel agent throughput and prevents false build attribution.
Hardware Impact: 0 us runtime. CLI compile proof returned 0 warnings and 0 errors; Unity runtime/profiler proof still not run.

## APEX Re-Audit Addendum - 2026-05-23

Problem: The first scanner only caught direct calls inside hot methods; `Tick -> helper -> Shader.SetGlobal*` and UI string helpers could still pass.
Solution: Added same-type helper closure scanning for hot simulation methods and expanded UI hot-path checks for concat, `string.Concat`, `new string`, `AppendFormat`, and `.ToString`. UI string checks are now scoped to UI/Visor/HUD/PDA paths.
Rejected Alternatives: rg-only second pass, treating `LateFrameTick` as equivalent to simulation, or suppressing helper chains as noise.
Scalability potential: Low devices avoid CPU-side presentation drift; middle/high/ultra retain the same truth DTO and spend saved budget on GPU masks, post effects, and overdraw only in visual sync.
Hardware Impact: 0 us runtime for the scanner; 334 project-wide legacy findings remain visible instead of hidden.

Problem: Weather, seismic, ocean, and surface-weather systems were publishing shader globals from pre-visual lanes through helper methods.
Solution: `GlobalWeatherDirector`, `HectonSeismicTideDirector`, `HectonSurfaceWeatherDirector`, and `ShinobuOceanSurfaceAtmosphereRuntime` now queue scalar/vector/buffer state and bind shader globals from `LateFrameTick`. Ocean wave upload was split from shader binding so readback preparation no longer implies `Shader.SetGlobalBuffer`.
Rejected Alternatives: Leaving helper writes in `Tick`, using Unity `LateUpdate`, or adding managed events.
Scalability potential: Low = sparse scalar flush; Middle = normal shader globals; High = extra fog/snow/godray masks; Ultra = visual overkill from the same scalar DTOs without changing simulation truth.
Hardware Impact: Estimated 12-20 us avoided on dirty environmental visual frames on i3/MX350; not profiler-measured.

Problem: PDA data-log Tick could allocate strings through lore-surface key concatenation and author/summary prefix concatenation when stress corruption or hidden-record flashes refreshed.
Solution: Added cold catalog caches for lore surface keys, author display lines, summary lines, and unknown author/date lines; hot Tick paths now reuse cached strings and char buffers.
Rejected Alternatives: Keeping `string.Concat(logId, ".", surfaceId)` in Tick, allocating temporary `StringBuilder`, or disabling corruption effects.
Scalability potential: Low = stable static text; Middle/High/Ultra = richer corruption masks without per-frame managed string churn.
Hardware Impact: Estimated 2-6 us and 1-3 short-lived string allocations avoided on PDA refresh frames; not profiler-measured.

Problem: Hull breach jet presentation still used per-material property mutation during the deformation visual sync path.
Solution: Replaced `breachJetMaterial.SetBuffer/SetVector/SetFloat/SetMatrix` with global shader writes and kept the procedural indirect draw in visual sync. Hull deformation jobs and `Tick` still write/read DTOs only.
Rejected Alternatives: Spawning particles, mutating material instances per frame, or tying breach visuals to health calculation.
Scalability potential: Low = fewer breach jets; Middle/High/Ultra = denser GPU breach plume using the same breach DTO buffer.
Hardware Impact: Estimated 3-5 us avoided from material property churn on breach frames; not profiler-measured.

Problem: Project-wide strict scan still reports legacy hot-path presentation coupling outside the targeted paths, mostly `World`, `Fauna`, and `Gameplay`.
Solution: Did not mark global purity. Latest report remains the proof artifact: `fatalHotPath=334`, `uiStringGcRisks=0`, `parseFailures=0`, hash `bb52bbb5df934a51c017469c2987c20ce9410067d3f05d984afc64ae3f4f2302`.
Rejected Alternatives: Hiding residual findings by classifier suppression, claiming green based only on targeted paths, or launching a broad refactor loop across unrelated domains.
Scalability potential: Residual cleanup can be batched by owner domain; no gameplay truth change should be accepted without a route card.
Hardware Impact: Current patch compiles cleanly; remaining runtime savings are unclaimed until those residual paths are fixed and profiled.

## APEX Re-Audit Addendum 02 - Targeted Visual Sync Enforcement

Problem: `SargassumCutManager.Tick`, `SlowTick`, and `RegisterExternalCut` still reached compute dispatch, RenderTexture clears, and `SargassumDebrisParticleSystem.EmitBurst` through helper chains.
Solution: Hot methods now only queue mask/damage/debris data. `LateFrameTick` performs texture clears, compute dispatch, damage-volume update, shader publication, and debris burst emission.
Rejected Alternatives: Keeping compute dispatch in Tick because it is "only visual", or deleting cut visuals.
Scalability potential: Low = sparse cut mask/damage updates; Middle = current RT quality; High/Ultra = denser damage volume and debris with identical gameplay truth.
Hardware Impact: Estimated 8-14 us main-thread visual work removed from active cut frames on i3/MX350; not profiler-measured.

Problem: `SargassumGlobalDragManager.Tick/SlowTick` still reached render-resource allocation and fallback transform scaling through helper paths.
Solution: Scavenger render-resource work and draw preparation moved into `LateFrameTick`; fallback collapse chunk transform scaling was removed from the simulation helper path.
Rejected Alternatives: Classifier suppression or keeping direct Transform mutation for fallback-only visuals.
Scalability potential: Low = no nested/scavenger render churn in simulation; Middle/High/Ultra = visual nesting richness scales in late frame only.
Hardware Impact: Estimated 3-6 us avoided on active scavenger frames; not profiler-measured.

Problem: `FloraInteractionManager.Tick` still published submarine wash globals, flow-field globals, wake arrays, player flora globals, damage reaction globals, and sediment particles through helper chains.
Solution: Tick now queues dirty scalar/DTO state. `LateFrameTick` refreshes flow-field GPU data, processes wake visual buffers, emits sediment particles, and flushes shader globals. Reset paths queue a late-frame zeroing pass instead of writing shader globals from Tick.
Rejected Alternatives: Treating ecology visuals as simulation, or suppressing `World/FloraInteractionManager` because it has mixed ownership.
Scalability potential: Low = scalar-only visual updates with low cadence; Middle = current buffers; High/Ultra = larger wake/flow/sediment visuals from the same queued truth.
Hardware Impact: Estimated 31 us shifted out of hot ecology Tick on dirty visual frames; not profiler-measured.

Problem: `CreatureDamageManager.Tick` published leviathan wound shader globals directly.
Solution: Converted the manager to `ILateFrameTickable`; wound registration only dirties the upload and late frame publishes the global wound buffer/sphere.
Rejected Alternatives: Keeping the active owner as an `ITickable`, or cloning per-creature materials.
Scalability potential: Low = bounded 8 wound globals; Middle/High/Ultra = shader-side wound projection from the same vector array.
Hardware Impact: Estimated 2-4 us avoided on wound-active frames; not profiler-measured.

Problem: PDA spectrogram, VR brownout, and half-res particle constants used single active GPU buffers, risking same-frame CPU/GPU write-read overlap.
Solution: Added A/B `GraphicsBuffer` write promotion for PDA segment and args buffers, brownout constants, and half-res particle constants. Uploads use `LockBufferForWrite` where required and bind the promoted read buffer after upload.
Rejected Alternatives: `SetData` on a single buffer, main-thread blocking readback/fence waits, or per-frame material clones.
Scalability potential: Low = minimum DTO cadence; Middle/High/Ultra = richer GPU masks and particle overlays without CPU stalls.
Hardware Impact: Estimated 4-9 us stall risk removed on frames where GPU consumes previous constants; not profiler-measured.

Problem: The user requested proof that helmet/PDA UI no longer formats strings in hot loops.
Solution: Latest Roslyn report has `uiStringGcRisks=0`; targeted UI/PDA/visor paths have no fatal hot-path findings. PDA feedback shader clear is queued to late frame instead of Tick.
Rejected Alternatives: Manual grep-only proof or allowing `.ToString()` behind helper methods.
Scalability potential: Low/Middle/High/Ultra UI reads flat DTOs and GPU masks; fidelity scales by shader/constant data, not managed strings.
Hardware Impact: Estimated 0 B GC in scanned UI hot paths; runtime GC profiler not executed.

Problem: Final proof requires both analyzer and compile, but project-wide residuals remain.
Solution: Regenerated `Docs/Reports/PRESENTATION_DECOUPLING_OPTIMIZATION_REPORT_X_004.json` after patches: `fatalHotPath=94`, `mutablePresentation=0`, `uiStringGcRisks=0`, `parseFailures=0`, hash `e56d0b3a65123d9172c473324458fac3f330ec17e42a962945b117052c4a6bdb`. Ran `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false`: 0 warnings, 0 errors.
Rejected Alternatives: Claiming global clean while `FaunaBrain`, `HectonSubmarineOS`, `VRSomaticProvider`, `RandomEventSystem`, `MantaScooter`, and other residual files are still flagged.
Scalability potential: Targeted visual-sync routes are clean; remaining owners need separate route cards or late-frame migration without changing gameplay truth.
Hardware Impact: Compile proof only. No Unity runtime/profiler capture was executed, so microsecond estimates remain engineering estimates.

## APEX Re-Audit Addendum 03 - Global Fatal Closure

Problem: The follow-up strict helper-chain report still had 28 fatal hot-path presentation routes after the previous pass. The residual owners were fallback presentation bridges in fauna, submarine OS, somatic VR, random events, scooter headlights, camera rigs, docking/airlock transforms, oxygen/floater/harvestable visuals, outpost graphics setup, chunk fade material writes, world chunk shader globals, fluid decals, spark draws, and drill terrain snap transforms.
Solution: Converted each route to dirty scalar/DTO queues consumed from `LateFrameTick`, `Render`, or late-frame fallback flushes. Rigidbodies keep physics-owned position routes; non-rigidbody transform/renderer/game-object fallbacks now queue a presentation pose or enable-state and flush after simulation. `MarauderOutpostGenerationService.TryRequestGeneration` no longer creates graphics resources from the `Tick` chain; `LateFrameTick` owns `EnsureGraphicsResources`.
Rejected Alternatives: Analyzer suppression, treating presentation-owned `Tick` methods as acceptable, or leaving fallback-only `SetActive`, `SetPositionAndRotation`, `Material.SetFloat`, `Shader.SetGlobal*`, and `Graphics.DrawMesh*` calls in hot helper chains.
Scalability potential: Low = sparse dirty-bit flushing and cheaper shader masks; Middle = current GPU buffers/scalars; High = denser decals/sparks/chunk fade/outpost visuals; Ultra = richer GPU-side lies from the same immutable state with no simulation authority change.
Hardware Impact: Estimated 22-41 us of pre-visual contention/allocation risk removed across dirty frames on i3/MX350-class hardware; not profiler-measured.

Problem: GraphicsBuffer upload routes needed proof that CPU writes cannot collide with GPU reads in the same frame.
Solution: PDA spectrogram segments/args, VR brownout constants, half-res particle constants, diegetic visor lens globals, and existing water/visual-aging style routes use A/B `GraphicsBuffer` promotion: select inactive write buffer, `LockBufferForWrite`, write DTO, `UnlockBufferAfterWrite`, assign active/read buffer, bind via `SetGlobalConstantBuffer`, `SetBuffer`, or render params in VISUAL_SYNC/render pass. No `GetData`, readback wait, or same-buffer `SetData` loop is used in the checked paths.
Rejected Alternatives: Single-buffer `SetData`, main-thread fences, material clones, or CPU-side trigonometric animation loops.
Scalability potential: Low = minimum DTO cadence; Middle = stable constant-buffer updates; High/Ultra = more shader-driven glitch, lens, particle, PDA, and hull/flood/cavitation detail without changing simulation truth.
Hardware Impact: Estimated 4-9 us stall risk removed on frames where GPU consumes previous constants; not profiler-measured.

Problem: Helmet/PDA UI hot loops needed a hard string-allocation gate.
Solution: Latest Roslyn report reports `uiStringGcRisks=0`; UI hot checks include concat, `string.Concat`, `new string`, `AppendFormat`, and `.ToString`. PDA/helmet paths read DTOs or cached/cold strings and push GPU masks/buffers during late-frame or render-feature execution.
Rejected Alternatives: Manual grep-only proof, allowing `.ToString()` behind helper chains, or mutating `TMP_Text.text` from frame loops.
Scalability potential: Low/Middle/High/Ultra all keep UI data flat and allocation-free; visual richness scales in shader masks and constant buffers.
Hardware Impact: Static 0-risk proof only; runtime GC profiler not executed.

Problem: Final compile proof after the last fatal closure could not be launched without violating the project gate.
Solution: Regenerated the analyzer: `files=2375`, `runtimeFiles=863`, `simulationFiles=589`, `fatalHotPath=0`, `boundaryLeaks=128`, `mutablePresentation=0`, `uiStringGcRisks=0`, `parseFailures=0`, hash `9f57d2c490c62ed07f0fadcf640ce7dd0b7ed319e732761e2f1a4ef41583eb97`. Did not run a new `dotnet build` because CPU probes hit 100% and external `dotnet.exe`/`csc.exe` processes were active.
Rejected Alternatives: Launching a competing build under CPU >50%, killing other agents' compiler processes, or claiming a compile pass without running it.
Scalability potential: No runtime implication; protects parallel agent throughput.
Hardware Impact: 0 us measured. Last previous CLI compile before the final outpost move was clean; latest compile proof remains pending.

## APEX Re-Audit Addendum 04 - Paranoid Visual Scalar Closure

Problem: The repeated source audit found that hull deformation and flooding shader/indirect draw calls are phase-correct, but the PDA frequency spectrogram still built visual sine segment geometry on CPU/Burst and uploaded a structured segment buffer. That is not a simulation leak, but it is not the strictest Dear Lie implementation.
Solution: Removed the PDA visual `FrequencyTuningWaveGpuSegment` Vault/buffer route. The CPU Burst job now computes only gameplay error truth arrays. `PDADecryptionSpectrogramPanel.RenderWaveMesh` sends frequency/amplitude/layout scalars, and `Hecton_PDA_FrequencyTuningWave.shader` reconstructs target/player line geometry in the vertex shader using `sin` per instance. Indirect args remain A/B `GraphicsBuffer` with `LockBufferForWrite`.
Rejected Alternatives: Keeping the structured segment upload because it was Burst-safe; hiding it behind the analyzer; moving unlock/error truth to shader where gameplay would depend on presentation.
Scalability potential: Low = 32 points, one shader sine per segment, no segment upload; Middle = normal 64-96 points; High = 128 points; Ultra = higher visual density can be bought by raising segment count without changing unlock/error truth DTO layout.
Hardware Impact: Estimated 3-7 us avoided on active PDA tuning frames on i3/MX350 by deleting segment DTO writes and `GraphicsBuffer` upload. Not profiler-measured.

Problem: User requested proof that hull deformation, flooding, helmet/PDA visuals, and flora presentation are blind to simulation and frame-string allocation.
Solution: Re-read owner methods: `HullIntegrityRuntime.Tick` schedules DTO jobs only and `LateFrameTick` owns `UploadDentsToGpu`, `UploadDeformationsToGpu`, `UploadBreachJetsToGpu`, and `RenderBreachJets`; `HabitatFluidIncursionDirector.FixedTick/PostFixedTick` compute/swap fluid DTOs and `Render` owns flood scalar/waterline shader upload; `StructuralIntegrityCalculatorRuntime.LateFrameTick -> AfterSolverComplete -> UploadStatesToGpu` owns structural shader binding; `FloraInteractionManager.Tick` queues visual requests and `LateFrameTick -> FlushQueuedTickVisualWork/FlushInteractionVisualSync` owns shader/particle presentation. Last analyzer artifact reports `uiStringGcRisks=0` and scans `.ToString`, concat, `string.Concat`, `new string`, `AppendFormat`, and `TMP_Text.text` in hot UI methods.
Rejected Alternatives: Treating broad `rg` presentation tokens as phase proof; claiming every raw `Shader.Set*` line is a leak without owner-method context.
Scalability potential: Low/Middle/High/Ultra keep simulation truth in DTO/job lanes while visual density, shader masks, and buffer capacities scale continuously by `GlobalQualityWeight`.
Hardware Impact: 0 us measured for this re-read. It preserves the previous estimated 22-41 us pre-visual contention removal and adds the PDA 3-7 us estimate above.

Problem: Analyzer/build proof after the PDA shader-side patch is required, but current machine state violates the explicit build gate.
Solution: Rechecked CPU/process gate: CPU was 99.81-100%; one probe had active `csc.exe`/`dotnet.exe`, one probe had no compilers but CPU remained above 50%, and the final probe had 8 active `dotnet.exe` processes. Did not run `dotnet run` for the analyzer or `dotnet build`.
Rejected Alternatives: Running `dotnet` under CPU >50%; claiming a fresh analyzer/build pass from stale data.
Scalability potential: No runtime effect; avoids starving parallel agents.
Hardware Impact: 0 us measured. Latest post-PDA compile/analyzer proof remains pending.

## APEX Re-Audit Addendum 05 - PDA CPU Trigonometry Removal

Problem: After the visual segment-buffer route was deleted, `PDADecryptionSpectrogramPanel` still sampled target/player sine waves in CPU/Burst jobs for error truth. That was no longer presentation pollution, but it still violated the stricter user requirement that PDA effects not run CPU trigonometry in frame paths.
Solution: Removed the PDA wave NativeArrays, `JobHandle`, Burst jobs, and CPU `math.sin` sampling. `Tick` now queues a scalar error from normalized frequency/amplitude deltas only. `LateFrameTick` consumes that scalar for unlock/feedback and renders the PDA wave via shader scalars. Nightmare drift now uses a deterministic triangle scalar instead of `noise.cnoise`. The only remaining sine for the PDA waveform is in `Hecton_PDA_FrequencyTuningWave.shader` vertex code.
Rejected Alternatives: Keeping CPU sine because it was Burst-safe; moving unlock truth fully into the shader; using compute/readback for error, which would introduce synchronization risk.
Scalability potential: Low = 32 visual segments with scalar C# error; Middle = normal segment count; High/Ultra = higher vertex segment count and richer shader waveform, with no C# trig/job/fence cost.
Hardware Impact: Estimated 2-5 us CPU math/job/fence overhead avoided on active PDA tuning frames on i3/MX350. Combined PDA estimate after segment-buffer and trig removal: 5-12 us avoided; not profiler-measured.

Problem: Fresh proof is still required after the no-trig PDA patch.
Solution: Ran `rg` checks showing no `math.sin`, `noise.cnoise`, PDA wave jobs, or segment-buffer symbols remain in `PDADecryptionSpectrogramPanel`; only HLSL `sin` remains in `Hecton_PDA_FrequencyTuningWave.shader`. `git diff --check` is clean. CPU probe remained at 99.81%, so analyzer/build were not launched.
Rejected Alternatives: Running `dotnet` under CPU >50%; reporting a fresh Roslyn artifact without running it.
Scalability potential: No runtime effect; prevents false proof.
Hardware Impact: 0 us measured. Fresh analyzer/build proof remains pending behind CPU gate.

## APEX Re-Audit Addendum 06 - Fresh Analyzer and Compile Boundary

Problem: The no-CPU-trig PDA patch needed a fresh static proof artifact, not stale report reuse.
Solution: Waited until the project gate allowed the analyzer and regenerated `Docs/Reports/PRESENTATION_DECOUPLING_OPTIMIZATION_REPORT_X_004.json`: `files=2379`, `runtimeFiles=864`, `simulationFiles=590`, `presentationFiles=274`, `fatalHotPath=0`, `boundaryLeaks=128`, `namespaceLeaks=51`, `mutablePresentation=0`, `uiStringGcRisks=0`, `parseFailures=0`, hash `2daf5ce9562f97e394be02ec72b26e03f113b8d0940b2d8219f9a2db1dc809f6`.
Rejected Alternatives: Claiming the prior `9f57...` analyzer artifact as proof after source changed; suppressing namespace/boundary debt.
Scalability potential: Low/Middle/High/Ultra presentation remains phase-owned; remaining 128 boundary leaks are cold namespace/type ownership debt, not hot presentation calls by the current scanner.
Hardware Impact: 0 us measured. Static proof only; runtime profiler still not run.

Problem: Compile proof after the fresh analyzer failed outside the X_004 presentation patch.
Solution: Ran `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false` only after CPU dropped below the gate and no compiler process was active. Build failed in untracked `Assets/_Project/Scripts/Core/Signals/SignalBridgeRoutes.cs` with unresolved `AupPreShiftSignal`, `AupShiftSignal`, `TimeDilationSignal`, `SimulationPauseSignal`, `BulletTimeVisualSignal`, `CraftingCompletedSignal`, and `SurvivalVitalsChangedSignal`. Source inspection shows the route file imports `Hecton8.Core.Contracts.Signals`; the referenced payload definitions exist in `GlobalSignals.cs` and `HectonSignalLaneContract.cs`, so this is a Core signal-route/project-state dependency, not a PDA/visor/hull presentation compile error. A later rerun is blocked by CPU >50% and active `dotnet`/`csc`.
Rejected Alternatives: Editing Core signal routing without a route-card owner after a single external failure; hiding the build failure; rerunning `dotnet` while other compiler processes are active.
Scalability potential: No gameplay/presentation runtime change. The correct owner must reconcile signal contract assembly/project generation so low/middle/high/ultra builds share one typed signal route.
Hardware Impact: 0 us runtime. Build proof remains blocked by dependency; no X_004 microsecond estimate changed.

## APEX Re-Audit Addendum 07 - Root Coverage and Direct Hot-Lane Purge

Problem: The previous analyzer did not classify root-level runtime scripts under `Assets/_Project/Scripts/*.cs` as simulation files. That under-reported legacy root systems such as boids, seam dither, meteor splash, PDA inventory parallax, and root hull/fluid presentation routes.
Solution: Expanded `Tools/PresentationDecouplingAudit` simulation roots to include `Assets/_Project/Scripts/` and added PDA/Visor/Lens presentation classification. Ran an independent direct hot-method scanner for `Update`, `FixedUpdate`, `Tick`, `FixedTick`, `PostFixedTick`, `SlowTick`, and Burst `Execute` bodies while Roslyn was CPU-gated.
Rejected Alternatives: Trusting the stale green artifact, suppressing root files as mixed ownership, or claiming helper-chain proof without rerunning the analyzer.
Scalability potential: Low/Middle/High/Ultra builds now receive stricter proof coverage; no gameplay truth or DTO layout changes.
Hardware Impact: 0 us runtime. Proof correctness only.

Problem: `SubmarineStructuralGrid.PostFixedTick` and impact helpers still reached leak-plume compute dispatch, global leak buffers, fake crush shader globals, and hull impact sparks through fixed/post-fixed routes.
Solution: Fixed/post-fixed code now accumulates scalar/request state only. `LateFrameTick` flushes fake crush shader globals, queued hull spark bursts, leak plume compute/global buffer publication, and leak plume particle rendering.
Rejected Alternatives: Leaving compute/particles in post-fixed because they are visually cheap, spawning managed events, or tying animation/deformation visuals back to health calculation.
Scalability potential: Low = sparse breach plume/spark flush; Middle = current plume capacity; High/Ultra = denser GPU plume and crush displacement with identical hull truth.
Hardware Impact: Estimated 8-18 us shifted out of dirty fixed/post-fixed frames on i3/MX350-class hardware; not profiler-measured.

Problem: `HectonFluidEngine.FixedTick/PostFixedTick` directly queued or published current water level shader globals, ocean wave uniforms, abyssal flow shader globals, and cavitation particles.
Solution: Fixed/post-fixed lanes now produce pending scalar/DTO state only. `LateFrameTick` owns water-level UI/shader publication, ocean wave global flush, abyssal flow compute/global publication, and cavitation particle emission. Cavitation shockwave physics remains in post-fixed because it affects truth.
Rejected Alternatives: Publishing shader globals from fixed simulation, moving physical shockwave to presentation, or using managed event fanout.
Scalability potential: Low = minimum visual cadence/scalars; Middle = current wave/abyssal flow; High/Ultra = richer abyssal textures and cavitation particles from the same immutable state.
Hardware Impact: Estimated 10-22 us of dirty-frame presentation work shifted from fixed/post-fixed; not profiler-measured.

Problem: PDA/visor/helmet-adjacent mock and feedback paths still used explicit CPU trigonometry/noise for visual motion.
Solution: Replaced checked `math.sin`, `math.cos`, `math.sincos`, and `noise.cnoise` in visor AR/noir mock data, glitch surgeon mock corruption/radar, wrist HUD mock vitals/taps/projector, terminal gaze mock, dynamic decals, and gyro noise with triangle scalars or bounded random vectors. Remaining explicit CPU trig is not claimed clean: `TopographicalSonarSynthesizer` still contains CPU sine/cosine synthesis and `TerminalOsRuntime` has editor-gizmo sine.
Rejected Alternatives: Claiming GPU-only effects while CPU trig remained, moving gameplay truth to shader/readback, or deleting debug visual behavior.
Scalability potential: Low = triangle approximations; Middle = same scalar cadence; High/Ultra = shader-side visual overkill can replace remaining approximations without changing truth routes.
Hardware Impact: Estimated 1-4 us on active mock/UI visual frames; not profiler-measured.

Problem: A lightweight direct hot-method scan still found direct presentation calls in `HectonBoidController.Tick`, `MeteorSplashQuadVfx.Tick`, `SeamGapDitherRenderer.Tick`, `PDAInventoryTab.Tick`, and `SargassumMicroFaunaBoids.Tick`.
Solution: Moved boid compute/draw, meteor quad draws, seam dither material/draw/upload, PDA inventory parallax shader commit, and sargassum microfauna compute dispatch to `LateFrameTick`. `Tick` now either queues scalar/dirty state or is a compatibility no-op for those visual systems.
Rejected Alternatives: Classifier suppression, leaving visual-only `Tick` as acceptable, or converting GPU visual systems into gameplay simulation owners.
Scalability potential: Low = cheap late-frame visual commits; Middle = current visual density; High/Ultra = more GPU boids/dither/splash/PDA richness without hot simulation pollution.
Hardware Impact: Estimated 6-14 us of pre-visual contention shifted on dirty visual frames; not profiler-measured.

Problem: Fresh Roslyn/helper-chain and compile proof is still required after these patches.
Solution: Ran `git diff --check` for touched files: clean except CRLF warnings. Ran direct hot-method scanner: 0 direct forbidden presentation calls in checked hot methods/jobs. Ran direct UI/PDA/Visor string scanner: 0 direct `.ToString()`, `string.Concat`, `new string`, `AppendFormat`, `.text =`, or `TMP_Text` local declarations in checked frame/job bodies. Rechecked build gate: latest probes stayed above CPU threshold or had active `dotnet/csc`, so no analyzer or build was launched.
Rejected Alternatives: Violating the explicit no-dotnet-under-load rule, killing other agents' compiler processes, or reporting stale analyzer/build proof as current.
Scalability potential: No runtime effect; protects parallel build throughput.
Hardware Impact: 0 us measured. Fresh analyzer/build proof remains pending behind the gate.

## APEX Re-Audit Addendum 08 - Mixed Root Presentation Queues

Problem: The stale root-coverage artifact and follow-up source audit showed mixed simulation/presentation owners still reachable through helper chains: `BaseModule` leak/flood/hum visuals, `AcousticZoneController.Tick -> ApplyAmbientLoopState`, `HectonPlayerMovement` brine/VR shader globals plus particles/audio/camera impulse feedback, and `Fabricator.SlowTick` assembly preview/welding/error feedback.
Solution: Added/extended `ILateFrameTickable` ownership. Hot lanes now write pending scalar/DTO state only: BaseModule queues leak/flood/hum and water-level shader uploads; Acoustic queues ambient loop and graph state; Player queues brine shader state, VR comfort globals, splash/bubble particles, footstep/splash/gasp audio, camera impulse scalars, and silt burst intensity; Fabricator queues assembly visual commands, spark/welding state, error material feedback, and craft one-shot audio. VISUAL_SYNC flushes the Unity presentation APIs.
Rejected Alternatives: Suppressing mixed root classes in the analyzer; leaving helper-chain `AudioSource.Play`, `ParticleSystem.Emit`, `Shader.SetGlobal*`, renderer material toggles, and `SetPropertyBlock` behind method indirection; pushing gameplay truth into shaders/readback.
Scalability potential: Low = sparse dirty scalar queues and no hot presentation calls; Middle = current visual/audio fidelity; High = denser particles and shader masks; Ultra = visual overkill from the same scalar DTOs while health, fabrication progress, fluid/brine truth, and kinematics stay CPU/Burst-owned.
Hardware Impact: Estimated 24-49 us of dirty-frame/slow-frame pre-visual contention shifted out of patched mixed roots on i3/MX350-class hardware. Not profiler-measured.

Problem: Fresh proof after these patches cannot be generated while respecting the project build gate.
Solution: Ran direct hot-method source scanners instead of launching Roslyn/dotnet. Patched target files report `TARGET_DIRECT_HOT_PRESENTATION_COUNT=0`; project candidate direct scan reports `DIRECT_HOT_PRESENTATION_COUNT=0` after excluding `PerformanceMonitor.Tick -> Stopwatch.Stop()` as a non-presentation false positive. `git diff --check` is clean except CRLF normalization warnings. CPU/process probe reports `CPU=100` with active `dotnet` and `csc`, so analyzer/build were not launched.
Rejected Alternatives: Running `dotnet run` or `dotnet build` under CPU >50%; claiming a fresh Roslyn/build pass from the stale artifact; killing other agents' compiler processes.
Scalability potential: No runtime effect; preserves parallel agent throughput and avoids false ownership of external compile state.
Hardware Impact: 0 us measured. Fresh Roslyn helper-chain and compile proof remain pending.

## APEX Re-Audit Addendum 08 - Fresh Root Roslyn Result

Problem: The first root-coverage analyzer rerun reported 413 fatal routes because the broad simulation root also classified UI/audio/rendering presentation files as simulation.
Solution: Corrected classifier precedence: presentation root/name classification now excludes a file from simulation classification even when the broad root matches.
Rejected Alternatives: Accepting inflated false positives, deleting the broad root, or suppressing the report.
Scalability potential: Low/Middle/High/Ultra proof coverage stays broad without counting first-party presentation roots as simulation.
Hardware Impact: 0 us runtime. Proof-only.

Problem: The corrected root-coverage analyzer still reports global residual presentation leakage.
Solution: Regenerated `Docs/Reports/PRESENTATION_DECOUPLING_OPTIMIZATION_REPORT_X_004.json`: `files=2390`, `runtimeFiles=1757`, `simulationFiles=1434`, `presentationFiles=323`, `fatalHotPath=291`, `boundaryLeaks=290`, `mutablePresentation=0`, `uiStringGcRisks=0`, `parseFailures=0`, hash `43db130df09a25c728557798606a0ccb84bf5a51a74060c4e73c924a8943f3a0`. Top residual owners: `HectonCelestialEngine` 105, `BaseModule` 18, `MapMagicRuntimeBridge` 17, `OrbitalRelativityDirector` 15, `WorldGenerativeGeologySeamExecutionDirector` 15, `HectonPlayerMovement` 15, `WorldProceduralScatterDirector` 10, `SpatialAudioManager` 10, `WorldCaveDirector` 10.
Rejected Alternatives: Claiming global purity based on the direct scan, hiding mixed-root debt, or editing large cross-domain systems without route-card ownership.
Scalability potential: The residual list is now a real owner queue; each route must be split into immutable DTO/signal truth plus late-frame presentation.
Hardware Impact: 0 us measured. Existing estimates apply only to patched X_004 routes, not the 291 residuals.

Problem: The corrected analyzer identified a remaining X_004-adjacent fixed-lane leak in `HectonFluidEngine`: `FixedTick -> QueueSplashdownBubbleRing -> EnsureFluidAdvectionState -> EnsureEmptyFluidAdvectionTexture`, which could allocate/apply a fallback `Texture3D` from fixed simulation.
Solution: Split fluid advection readiness into storage readiness and visual readiness. Fixed and signal-queue paths call `EnsureFluidAdvectionState` and `IsFluidAdvectionStorageReady`; `LateFrameTick` and cold init call `EnsureFluidAdvectionVisualState`, which owns `EnsureEmptyFluidAdvectionTexture`, `Texture3D.SetPixel`, `Texture3D.Apply`, and RTHandle allocation.
Rejected Alternatives: Letting fixed lane create the visual fallback once, moving particle truth to shader/readback, or suppressing `Texture3D.Apply` as cold.
Scalability potential: Low/Middle/High/Ultra all keep fixed advection queues storage-only; visual fallback texture exists only for render graph/VISUAL_SYNC.
Hardware Impact: Estimated 2-4 us first-use fixed-frame allocation/apply risk shifted out of fixed lane; not profiler-measured.

Problem: Analyzer/build proof after the fluid split is required.
Solution: Rechecked gate after the patch; CPU returned to 100% with active `dotnet/csc`, so no rerun or compile was launched.
Rejected Alternatives: Violating the explicit no-dotnet-under-load rule or reporting the `43db...` artifact as current after a source edit.
Scalability potential: No runtime effect.
Hardware Impact: 0 us measured. Fresh rerun remains pending.

## APEX Re-Audit Addendum 09 - Celestial, Base, Acoustic, MapMagic, Scan, Terrain

Problem: Current source still had helper-chain presentation commits after the stale 291-fatal Roslyn artifact: `HectonAtmosphereManager.SlowTick` reached sun transform and shader globals, `BaseModule` hot lanes reached pressure visual transform/reef `SetActive`, `AcousticZoneController.Tick` could route `AudioSource.outputAudioMixerGroup` and build diagnostic strings, `HectonScanMarkerSystem.Tick` rendered instanced markers and mutated material properties, `MapMagicRuntimeBridge.SlowTick` published terrain fade/shadow globals and disabled MapMagicObject, and `WorldGenerativeGeologyTerrainSeamApplier.SlowTick` cleared voxel blend shader globals.
Solution: Moved current runtime routes to VISUAL_SYNC ownership. Atmosphere queues sun pose, sun direction, cycle shader scalars, and giant abyss light for `LateFrameTick`. Base queues pressure scale/rotation and reef proxy enable state. Acoustic queues ambient mixer routing and uses constant diagnostic summaries without `string.Concat`. Scan markers keep Tick as timer aging and draw/material updates in `LateFrameTick`. MapMagic queues planetary canvas shader publish and runtime generation fence for `LateFrameTick`. Terrain seam clear now queues shader zeroing and flushes from `LateFrameTick`.
Rejected Alternatives: Relying on `Application.isPlaying` branches inside shared helpers as proof; leaving visual-only `Tick` systems outside VISUAL_SYNC; suppressing stale Roslyn findings without source changes.
Scalability potential: Low = sparse dirty flushes and cheap scalar masks; Middle = current marker/terrain/sky fidelity; High = denser marker and shadow masks; Ultra = richer GPU-side sky/terrain/scan lies from the same DTO/scalar truth.
Hardware Impact: Estimated 22-52 us shifted from dirty slow/tick frames on i3/MX350-class hardware. Not profiler-measured.

Problem: Fresh objective proof is still not global.
Solution: Ran a targeted helper-chain source scanner. `HectonAtmosphereManager.cs`, `BaseModule.cs`, `AcousticZoneController.cs`, `HectonScanMarkerSystem.cs`, and `MapMagicRuntimeBridge.cs` reported `HELPER_HOT_PRESENTATION_METHOD_COUNT=0` with `LateFrameTick` excluded as the presentation sink. `WorldGenerativeGeologyTerrainSeamApplier.cs` reports one conservative hit on `UploadVoxelBlendMaskTexture`; source inspection shows the runtime path returns before that editor-only upload branch, while the actual runtime shader-clear route is now queued. `git diff --check` on the touched files is clean except CRLF normalization warnings. Dotnet analyzer/build were not launched because CPU was 99%, above the project gate.
Rejected Alternatives: Running Roslyn/dotnet under CPU >50%; claiming global purity while subagent evidence still lists SpatialAudio, HectonFabricatorUI, geology seam execution, WorldCave async presentation, fauna fallback activation, orbital/floating-origin presentation routes.
Scalability potential: No runtime effect from proof work; remaining owners need the same scalar/DTO to late-frame split.
Hardware Impact: 0 us measured. Stale global Roslyn artifact remains `fatalHotPath=291`; current patches reduce known target debt but do not prove project-wide clean.

## APEX Re-Audit Addendum 10 - 25-File VISUAL_SYNC Sweep

Problem: Current-source helper scans still found real or conservative hot helper-chain routes after the earlier status pass: audio pitch/source updates, fabricator UI visibility, geology seam execution object activation, fauna fallback activation, orbital/floating-origin presentation queues, content hologram transforms, Crest depth-cache population, scavenge spawn/cull, asset lifecycle disables, player-tool anchor poses, demo-controller camera/UI pose, snap-switch lever rotation, battery snap pose, survival narcosis shader scalar, inventory rust shader scalar, BaseModule light toggles, and HectonFluid GPU buoyancy dispatch.
Solution: Converted those routes to `ILateFrameTickable`-owned pending scalar/DTO/pose queues. `FixedTick`, `Tick`, and `SlowTick` now stage data only; `LateFrameTick` owns `Shader.SetGlobal*`, `GraphicsBuffer`/compute dispatch, `AudioSource`, `TMP`/CanvasGroup enable state, `Transform` pose, renderer enable, and particle presentation in the patched routes.
Rejected Alternatives: Analyzer suppression, direct Unity `LateUpdate`, managed event fanout, keeping fallback-only visual calls in hot helpers, or moving gameplay truth into shader/readback.
Scalability potential: Low = sparse dirty-bit flushes and cheaper GPU masks; Middle = current fidelity; High = denser particles, markers, and shader masks; Ultra = visual overkill from the same immutable scalar/DTO data without changing authority.
Hardware Impact: Estimated 42-88 us of dirty update/fixed/slow presentation contention shifted on i3/MX350-class hardware; not profiler-measured.

Problem: UI string allocation proof needed a current-source gate after the new UI/tool patches.
Solution: Ran runtime UI/PDA/helmet-adjacent frame scanner excluding editor windows: `RUNTIME_UI_STRING_FRAME_HITS=0`. The only broader scanner hit was an editor-only XRay window `.text =`, not runtime helmet/PDA UI.
Rejected Alternatives: Counting editor windows as runtime UI failures; relying on stale `uiStringGcRisks=0` only.
Scalability potential: Runtime UI remains DTO/char-array/shader-mask oriented across low/middle/high/ultra tiers.
Hardware Impact: Estimated 0 B GC in scanned runtime UI hot methods; runtime GC profiler not executed.

## APEX Re-Audit Addendum 11 - Analyzer Contraction and World Residual Patch

Problem: Fresh `PresentationDecouplingAudit.exe` after the first current-source sweep reduced global fatal hot-path routes from 291 to 32 but still proved global purity false. Residuals were mostly `WorldCaveDirector`, `WorldProceduralScatterDirector`, `WorldGenerativeGeologyTerrainSeamApplier`, `HectonWorldGenerator`, plus residual transform-job/physics/QA routes.
Solution: Moved cave entrance/dressing/fungi/geyser presentation to a pending cave visual-sync queue drained in `LateFrameTick`; moved terrain seam voxel blend-mask texture upload and shader publication to `LateFrameTick`; moved scatter runtime reconcile/create/apply into a late-frame visual-sync request; queued world-generator renderer disables for `LateFrameTick`.
Rejected Alternatives: Leaving world generation visual work in `SlowTick`, suppressing editor/runtime mixed branches, or doing instantiate/renderer/particle/material work in the simulation cadence.
Scalability potential: Low = fewer dirty cave/scatter/seam visual syncs; Middle = current cave/scatter/seam fidelity; High/Ultra = richer cave dressing, scatter density, and terrain seam masks from the same generated state.
Hardware Impact: Estimated 18-37 us shifted out of dirty world/scatter/seam frames on i3/MX350-class hardware; not profiler-measured.

Problem: Final objective proof is still bounded by the project build gate.
Solution: Ran patched-file source helper-chain scanner after the second batch: `PATCHED_SOURCE_HELPER_HITS=0` for 15 current files. `git diff --check` for touched files is clean except CRLF normalization warnings. Final Roslyn rerun and compile were not launched because active `dotnet` and `VBCSCompiler` processes remained after the prior analyzer executable run.
Rejected Alternatives: Killing other agents' compiler processes, running build under the explicit active-compiler ban, or claiming final project-wide green from a stale artifact.
Scalability potential: No runtime effect; preserves parallel agent throughput and keeps proof attribution honest.
Hardware Impact: 0 us measured. Latest completed analyzer is stale by the final world patch and still reports `fatalHotPath=32`.

## APEX Re-Audit Addendum 12 - Dispatcher, Foveated, Origin Residuals

Problem: The 32-fatal Roslyn artifact still had residual non-world routes, and source inspection confirmed several were real phase leaks: foveated visual interpolation was scheduled from the simulation dispatcher cadence, foveated doppler protection wrote `AudioSource` state in `BeginDispatcherFrame`, simulation-bucket sync wrote `_SimulationBucketInterpolationAlpha` before VISUAL_SYNC, global physics teleport redundantly wrote `body.transform.SetPositionAndRotation`, and floating origin wrote root transform positions from an `IJobParallelForTransform.Execute`.
Solution: Added `IFoveatedDispatcher.VisualSyncTick`, moved foveated transform interpolation and doppler/pitch writes to that VISUAL_SYNC call, removed the transform-access interpolation job, and called it from `SystemDispatcher.RunDispatcherLateFrame` after foveated job completion. `PublishSimulationBucketSync` now stages the alpha scalar and `FlushSimulationBucketVisualSync` writes the shader global from late frame. `RequestVisualStaticGlitch` now only stages glitch lifetime; shader globals update from the existing late-frame visual static state. `GlobalPhysicsStateManager` keeps rigidbody position/rotation and `PublishTransform` but drops the redundant direct transform write. `HectonFloatingOrigin.Tick` now queues the pending origin shift for late frame; actual cached scene-root transform rebase runs from `LateFrameTick` while the AUP/vault rebase remains deterministic and frame-locked.
Rejected Alternatives: Keeping the transform-access jobs because they are fast, suppressing analyzer residuals, moving physics teleport truth into presentation, or using a managed event fanout for foveated/audio state.
Scalability potential: Low = fewer pre-visual transform/audio/shader writes and no same-frame transform job; Middle = same visual interpolation fidelity in late frame; High/Ultra = foveated visual interpolation can later be promoted to GPU/VAT presentation without touching simulation tick cadence.
Hardware Impact: Estimated 7-16 us shifted or removed on dirty foveated/origin/simulation-bucket frames on i3/MX350-class hardware. Not profiler-measured.

Problem: Final objective proof is still unavailable.
Solution: Ran `git diff --check` for the four new files: clean except CRLF warnings. Re-read the X_004 prompt from `Docs/Tasks/CURRENT_BATCH.md`. Latest build gate probe had no `dotnet/csc/VBCSCompiler` rows but CPU was `99.81%`, so Roslyn analyzer/build were not launched under the explicit CPU gate.
Rejected Alternatives: Running dotnet under CPU >50%, reporting the stale 32-fatal artifact as current, or killing other agents' work.
Scalability potential: No runtime effect; proof attribution remains honest.
Hardware Impact: 0 us measured. Fresh analyzer/build proof remains pending.

## APEX Re-Audit Addendum 13 - Structural, Encounter, XR, Tool, Audio Purge

Problem: The repeated source pass found a real structural/flooding presentation leak: `ConstructionManager.SlowTick -> HabitatGraphManager.ApplyHydrodynamicStress` still reached habitat vibration, base emergency state, analytical stress globals, and module stress `GraphicsBuffer` upload/bind.
Solution: `HabitatGraphManager` now stages habitat vibration, emergency state, analytical stress, and module stress metadata as pending scalar/DTO state. `ConstructionManager.LateFrameTick` calls `FlushVisualSync`, which owns `Shader.SetGlobal*`, `GraphicsBufferUploadUtility.UploadNativeArray`, `Shader.SetGlobalBuffer`, and module stress params publication.
Rejected Alternatives: Treating low-frequency `SlowTick` as visually safe; keeping the module stress upload inline because it uses a double-buffer-friendly utility; duplicating health/integrity truth in the shader.
Scalability potential: Low = low-tier stress bit/peak scalar only; Middle = current module stress buffer; High/Ultra = denser GPU displacement and breach shimmer from the same module stress DTOs.
Hardware Impact: Estimated 5-11 us shifted out of dirty structural slow frames on i3/MX350-class hardware. Not profiler-measured.

Problem: `HectonDirectorAI.Tick -> EncounterDirector.Advance` still uploaded predator AUP buffer data and shader globals while AI truth was advancing. This contaminated encounter simulation cadence with presentation binding.
Solution: `EncounterDirector` now queues full/player predator AUP uploads and clear commands. `HectonDirectorAI.LateFrameTick` calls `FlushPredatorAupVisualSync`, where the A/B `GraphicsBuffer` upload, global buffer bind, count, and params are committed. Tick now only stores scalar/player position intent.
Rejected Alternatives: Leaving the player AUP slot upload in Tick for freshness; relying on a single shared buffer; moving predator threat truth into the shader.
Scalability potential: Low = one player predator sphere; Middle = player plus active predators; High/Ultra = denser predator/noise visual response from the same 16-slot AUP buffer without altering encounter authority.
Hardware Impact: Estimated 3-7 us of dirty AI tick GPU upload/bind contention shifted to VISUAL_SYNC. Not profiler-measured.

Problem: `SystemDispatcher.RunDispatcherUpdate -> HectonXRRuntimeState.RefreshFrameState` still wrote foveation, cadence, origin-shift, and pose-sync shader globals before simulation lanes ran. Origin-shift pose lock also published shader state from non-visual routes.
Solution: `HectonXRRuntimeState` now stages all dynamic XR shader vectors in a static pending cache. `SystemDispatcher.RunDispatcherLateFrame` calls `HectonXRRuntimeState.FlushVisualSyncShaderState` after foveated simulation visual sync and simulation-bucket flush. Cold reset remains direct lifecycle cleanup only.
Rejected Alternatives: Calling `Shader.SetGlobalVector` from `RefreshFrameState`, treating XR state as harmless because it is presentation-owned, or forcing shader state through readback.
Scalability potential: Low = cadence/origin/foveation scalar masks only; Middle = current foveated shader state; High/Ultra = richer XR comfort/foveation shaders from the same late-frame vectors.
Hardware Impact: Estimated 2-5 us of pre-simulation shader-global churn shifted to VISUAL_SYNC on XR frames. Not profiler-measured.

Problem: `LaserCutter.ToolTick -> WfcLaserCutRuntime.TryApplyDoorCut` wrote WFC cut sphere/progress/heat globals while mutating sealed-door gameplay truth.
Solution: `WfcLaserCutRuntime` now queues cut sphere/progress/heat/molten/overkill scalars and exposes `FlushVisualSync`; `SystemDispatcher.RunDispatcherLateFrame` commits those shader globals. Door progress truth remains in the Vault cell scalar and door state application route.
Rejected Alternatives: Keeping direct globals because the effect is tool-local; spawning decals/particles; making the shader own door progress.
Scalability potential: Low = one cut sphere and heat scalar; Middle = current molten/overkill scalar set; High/Ultra = richer cut distortion/spark shader work from the same scalar packet.
Hardware Impact: Estimated 2-4 us shifted out of active cutting tool frames. Not profiler-measured.

Problem: Project direct hot-lane scan still found `PlayerThrusterAudio.Tick` mutating `AudioSource.volume` and `AudioSource.pitch`.
Solution: `PlayerThrusterAudio` now implements `ILateFrameTickable`. Tick calculates current audio scalars only and queues pending source volume/pitch; `LateFrameTick` applies the `AudioSource` properties and unregisters until another dirty update. Procedural audio sample synthesis remains in the audio callback and reads the scalar fields.
Rejected Alternatives: Letting audio-source property writes stay in Tick; replacing the existing procedural clip during this pass; routing gameplay movement back through managed audio events.
Scalability potential: Low = minimal source scalar flush; Middle = current procedural thruster loop; High/Ultra = richer DSP source can consume the same volume/pitch scalars without touching gameplay tick.
Hardware Impact: Estimated 4-7 us of dirty player tick audio API contention shifted to LateFrame on active swim/transport frames. Not profiler-measured.

Problem: Fresh proof was needed after the new source patch batch.
Solution: Ran a project direct hot-lane scanner over runtime scripts: `DIRECT_PROJECT_HOT_PRESENTATION_COUNT=0` for `Update`, `FixedUpdate`, `Tick`, `FixedTick`, `PostFixedTick`, `SlowTick`, `Execute`, and `ToolTick` bodies against shader/material/audio/text/draw/dispatch/buffer-upload APIs. Ran runtime UI/PDA/Visor string scanner: `RUNTIME_UI_STRING_FRAME_HITS=0`. Ran `git diff --check` for the current files: clean except CRLF normalization warnings. Build/analyzer gate briefly reached CPU `71%` with no active compiler rows, then reclosed at CPU `96%` with active `dotnet`, `csc`, and `VBCSCompiler`; `dotnet` proof remained blocked by the explicit CPU >50%/active-compiler rule.
Rejected Alternatives: Running Roslyn/build under CPU >50%; claiming helper-chain green from the stale `fatalHotPath=32` artifact; ignoring the direct audio hit because it was small.
Scalability potential: Static proof improves regression pressure across low/middle/high/ultra tiers; runtime fidelity remains scalable through dirty scalar/DTO queues.
Hardware Impact: 0 us measured for proof. Combined current-pass engineering estimate: 16-34 us shifted out of dirty simulation/tool/audio frames; not profiler-measured.

## APEX Re-Audit Addendum 14 - Bridge, Ecology, Fluid Helper-Chain Closure

Problem: Current source still had helper-chain phase leaks after the direct hot-lane gate was green. `HectonShaderGlobalDataVaultBridge.Publish*` wrote shader globals directly when the visual-sync dispatcher had not become active yet. `EcosystemDirector.PublishBiolumFlashBang` and biomass telemetry helpers could write biolum/biomass globals from service-call or ecology completion routes. `HectonFluidEngine.FixedTick` helper chains could complete splashdown GPU upload work, create advection graphics buffers, or upload advection particle buffers before VISUAL_SYNC.
Solution: Bridge publishers now update Vault/read mirrors and mark fallback shader globals dirty; `SystemDispatcher.RunDispatcherLateFrame` owns `HectonShaderGlobalDataVaultBridge.FlushFallbackVisualSync`. Ecosystem biolum flash and biomass overgrowth now stage scalar/vector packets and flush from `FlushQueuedEcosystemVisuals`. HectonFluid fixed/storage routes now create/mutate native staging arrays only; splashdown impulse completion, advection visual buffer creation, empty texture fallback creation, and A/B advection buffer uploads are owned by `LateFrameTick`.
Rejected Alternatives: Leaving fallback shader writes in `Publish*` because they only happen before dispatcher activation; keeping fluid first-use GPU allocations in fixed helpers; using single-buffer `SetData`; treating ecosystem globals as harmless because they are visual-only.
Scalability potential: Low = sparse scalar/DTO flush and minimum advection payload cadence; Middle = current A/B buffer publication; High = denser fluid/ecology shader masks; Ultra = richer GPU-side biolum, biomass, splash, and advection lies from the same simulation-owned DTOs.
Hardware Impact: Estimated 12-27 us shifted out of dirty bridge/ecology/fluid frames on i3/MX350-class hardware. Not profiler-measured.

Problem: Fresh proof after the bridge/ecology/fluid patch was required.
Solution: Ran current source-only gates. Direct hot-lane scanner reports `DIRECT_PROJECT_HOT_PRESENTATION_COUNT=0` for runtime `Update`, `FixedUpdate`, `Tick`, `FixedTick`, `PostFixedTick`, `SlowTick`, `Execute`, and `ToolTick` bodies after excluding `Stopwatch.Stop()` as a non-presentation false positive. Runtime UI/PDA/Visor scanner reports `UI_STRING_CANDIDATE_FILES=60`, `RUNTIME_UI_STRING_FRAME_HITS=0`. Targeted helper scanner reports `TARGETED_HELPER_FORBIDDEN_HITS=0` for the patched bridge/ecology/fluid helper routes. `git diff --check` is clean except CRLF normalization warnings.
Rejected Alternatives: Claiming Roslyn/build green from the stale `fatalHotPath=32` artifact; running dotnet under the explicit CPU gate; broad manual text claims without method-scoped scanners.
Scalability potential: Static proof has no runtime cost and prevents low-tier CPU stalls from reentering fixed/update lanes while leaving high/ultra visuals GPU-scalable.
Hardware Impact: 0 us measured for proof. Analyzer/build remained blocked by CPU=100%; Unity runtime/profiler/GC proof still not executed.

## APEX Re-Audit Addendum 15 - UI/Ocean/Fluid Residual Slice

Problem: A broad source-only helper callgraph still found real residual routes outside the prior bridge/ecology/fluid patch. `HectonFluidEngine.FixedTick` could reach GPU buffer creation through `ReallocateNativeArrays`. `ShinobuOceanSurfaceAtmosphereRuntime.SlowTick` could ensure wave graphics/readback buffers and upload wave payloads. PDA inventory/loadout/data-log tabs refreshed TMP/char-array UI from `Tick`. `VehicleSubOsCockpitRuntime.Tick` uploaded sonar taps, updated offscreen TMP metrics, applied screen material state, and could retry radar graphics resource creation through quality-policy changes.
Solution: Removed GPU buffer creation from HectonFluid native reallocation; GPU buoyancy/abyssal buffers are now ensured by visual dispatch paths. Ocean SlowTick now marks simulation/weather state only; `LateFrameTick` owns wave graphics/readback buffer checks and upload. PDA inventory, loadout, and data-log ticks now stage dirty flags, timers, and deltas; `LateFrameTick` owns refresh/text/hologram presentation. Vehicle cockpit tick now computes power/damage/state and schedules jobs only; `LateFrameTick` owns radar graphics retry, sonar upload/dispatch, offscreen text, material state, and camera state.
Rejected Alternatives: Treating UI `Tick` text as acceptable because it uses `SetCharArray`; leaving first-use GPU creation in fixed/slow helpers; hiding residuals by excluding presentation-owned files from source scans; running Roslyn/build while CPU gate is closed.
Scalability potential: Low = lower cadence dirty refresh and smaller radar/ocean payloads; Middle = current UI/ocean/cockpit presentation; High = denser sonar/radar and richer ocean shader payloads; Ultra = visual overkill from the same scalar/native DTO truth without changing gameplay authority.
Hardware Impact: Estimated 18-39 us shifted out of dirty fixed/slow/UI frames on i3/MX350-class hardware. Not profiler-measured.

Problem: Proof needed to distinguish this targeted slice from the still-stale project-wide Roslyn artifact.
Solution: Ran targeted callgraph for HectonFluid, Shinobu ocean atmosphere, PDA inventory, PDA loadout, PDA data log, and vehicle cockpit: `TARGETED_UI_ENV_CALLGRAPH_HOT_PRESENTATION_COUNT=0`. Ran project direct hot-lane scanner: `DIRECT_PROJECT_HOT_PRESENTATION_COUNT=0`. Ran runtime UI/PDA/Visor string scanner: `UI_STRING_CANDIDATE_FILES=60`, `RUNTIME_UI_STRING_FRAME_HITS=0`. Re-ran the wide source-only helper callgraph: `SOURCE_HELPER_HOT_PRESENTATION_COUNT=139`, so project-wide helper purity is still not proven. `git diff --check` is clean except CRLF warnings.
Rejected Alternatives: Reporting the broad source-only 170-hit list as final without fixing any of it; claiming global helper-chain green without Roslyn; launching dotnet under CPU >50%.
Scalability potential: Source gates are regression pressure only; runtime scalability comes from dirty scalar/DTO queues and late-frame GPU/UI sinks.
Hardware Impact: 0 us measured for proof. Analyzer/build remained blocked by CPU=79%; Unity runtime/profiler/GC proof still not executed.

## APEX Re-Audit Addendum 16 - Audio, Visor, Scatter Visual-Tick Contraction

Problem: The wide helper scan still exposed real phase leaks in audio and renderer-owned systems. `AcousticZoneController.Tick` queued ambience but still called one-shot audio helpers, could resolve `AudioSource`/`AudioListener` from player lookup, and touched mixer routing through helper chains. `AdaptiveStemAudioMixer.Tick` finalized audio jobs and immediately wrote `AudioSource` volume/filter/play state. `ThermalDynamicResolutionAdapter.Tick` committed dynamic-resolution runtime state, shader globals, and scale changes. Flora tint, VR focus, material decay, stress VFX, camera speed lines, spectrum sonar, underwater visuals, marine snow, and GPU scatter all had update/slow routes reaching shader/material/compute/audio/draw sinks.
Solution: Converted the affected routes to VISUAL_SYNC queues. Acoustic hot lanes now compute scalar timing and queue transition/storm/vegetation/sonar/manta/fatal-pressure cue DTOs; `LateFrameTick` owns `PlayStatic2D`, `AudioSource.Play/Stop`, mixer routing, and ambient source defaults. Adaptive stem job completion copies `StemMixFrameDTO`/rule DTO; late frame applies Unity audio or publishes dynamic-music signal. Dynamic-resolution Tick now only computes target state and marks pending render/runtime/global commits; late frame owns `DynamicResolutionHandler`, `ScalableBufferManager`, runtime override, shader globals, and scale telemetry publication. Spectrum, underwater visuals, marine snow, and GPU scatter now use Tick as a delta accumulator and execute their former visual tick from `LateFrameTick`.
Rejected Alternatives: Suppressing presentation-owned files in the scanner, accepting audio/renderer Tick because they are "not gameplay", moving audio source writes into Unity `LateUpdate`, using managed events per cue, or running single-buffer GPU uploads from update lanes.
Scalability potential: Low = sparse dirty-bit queues and lower visual tick payloads; Middle = current fidelity; High = richer sonar, fog, marine snow, and scatter GPU passes; Ultra = visual overkill from the same scalar/DTO snapshots without moving authority into shaders or audio components.
Hardware Impact: Estimated 38-86 us shifted out of dirty update/slow frames on i3/MX350-class hardware. Not profiler-measured.

Problem: `PlayerThrusterAudio.Tick` was still caught by the direct hot-lane scanner after the first audio-source patch because the method body referenced identifiers containing `AudioSource` and ensured the procedural clip from Tick.
Solution: Removed `_audioSource` and procedural clip readiness checks from Tick; renamed pending fields/methods to neutral Unity-output names; `LateFrameTick` ensures the procedural clip and applies source volume/pitch.
Rejected Alternatives: Whitelisting the file, weakening the scanner token set, or leaving clip creation in Tick because it is rare.
Scalability potential: Low/Middle/High/Ultra all compute the same thruster scalar envelope while Unity source mutation remains in VISUAL_SYNC.
Hardware Impact: Estimated 1-3 us and one rare clip-allocation path removed from dirty player update frames. Not profiler-measured.

Problem: Fresh proof was needed after Loop 28 without violating the build gate.
Solution: Re-extracted X_004 prompt (`X_004_PROMPT_CHARS=12664`). Ran targeted expanded helper-chain scanner: `TARGETED_EXPANDED_CALLGRAPH_HOT_PRESENTATION_COUNT=0` for the 12 current audio/visor/scatter/material files. Ran current project direct scanner: `DIRECT_PROJECT_HOT_PRESENTATION_COUNT=0`. Ran runtime UI/PDA/Visor string scanner: `UI_STRING_CANDIDATE_FILES=262`, `RUNTIME_UI_STRING_FRAME_HITS=0`. `git diff --check` is clean except CRLF normalization warnings.
Rejected Alternatives: Claiming stale Roslyn green, running `dotnet` while CPU gate is closed, or ignoring the direct `PlayerThrusterAudio` hit as a naming false positive.
Scalability potential: Static gates prevent low-tier CPU regressions while preserving high/ultra GPU-only visual escalation.
Hardware Impact: 0 us measured for proof. Analyzer/build remained blocked by CPU=73% with no active compiler rows; Unity runtime/profiler/GC proof still not executed.

## APEX Re-Audit Addendum 17 - Music Director Audio-Source Closure

Problem: `HectonMusicDirector.Tick/SlowTick` still reached Unity music bed and stinger `AudioSource` mutation through fade, selection, override, and stinger helper chains. This was not visible in the direct body scan because the writes were behind helper methods.
Solution: Added `ILateFrameTickable` ownership. Tick now accumulates music delta and marks a pending music visual tick. SlowTick marks a pending context reevaluation. `LateFrameTick` runs the existing music tick/context logic, preserving selection behavior while moving `ConfigureVoice`, `StopVoiceImmediate`, `TryPlayStinger`, source volume, pitch, clip, loop, mixer-group, play, and stop writes out of update/slow lanes.
Rejected Alternatives: Suppressing audio-owned files, rewriting the whole music state machine, routing through managed events per fade, or leaving music as a special-case exception.
Scalability potential: Low = sparse music source commits and procedural synthesis ownership can take over; Middle = current bed/stinger crossfade fidelity; High/Ultra = richer stem/stinger layering from the same late-frame scalar/context route.
Hardware Impact: Estimated 6-14 us shifted out of dirty music update/slow frames on i3/MX350-class hardware. Not profiler-measured.

Problem: Proof needed after the music director migration.
Solution: Ran targeted helper-chain scanner over music plus current audio/visor/scatter files: `TARGETED_AUDIO_VISUAL_CALLGRAPH_HOT_PRESENTATION_COUNT=0`. Re-ran current project direct hot-lane scanner: `DIRECT_PROJECT_HOT_PRESENTATION_COUNT=0`. `git diff --check` for Loop 29 touched files is clean except CRLF normalization warnings.
Rejected Alternatives: Counting direct body green as enough, running final Roslyn/build under active compiler load, or omitting the music director because it is presentation-owned.
Scalability potential: Static gates remain the cheap proof layer across all quality tiers; runtime scalability comes from late-frame audio/visual commits.
Hardware Impact: 0 us measured for proof. Final analyzer/build remained blocked by CPU=100% with active `dotnet` and `csc`; Unity runtime/profiler/GC proof still not executed.

## APEX Re-Audit Addendum 18 - Tool, AudioLog, Dynamic Host, and Carve Debris Closure

Problem: The next current-source slice still had real helper-chain phase leaks. `BuilderTool`, `FlashlightTool`, and `ScannerTool` could reach `Renderer.Get/SetPropertyBlock` from tool-frame routes. `LaserCutter.UsePrimary` still played an overheat cue through `PlayStatic2D`, and laser/repair helpers reached line/light/audio/particle/shader sinks. `PlayerBuilder.ToolTick` could route input helpers to `PlayStatic2D`. `AudioLogSystem.SlowTick` could complete one log and start the next through `PlayStatic2D` or narrative radio interference. `DynamicMusicGranularSynthesizer.SlowTick` configured the Unity host `AudioSource`. `CarveDebrisComputeRenderer.Tick` ran compute dispatch and `RenderMeshIndirect`.
Solution: Converted the slice to explicit VISUAL_SYNC ownership. Tool frames now set dirty state only; `LateFrameTick` owns MPB updates, line/light/audio/decal/particle sinks, and laser heat shader publication. PlayerBuilder stores up to four fixed pending clip references and flushes one-shots in late frame. AudioLog queues playback clip, volume, bit-crush preference, and interference scalar; late frame applies the narrative sink or fallback audio service. DynamicMusic SlowTick marks host config dirty; late frame configures the Unity source. CarveDebris Tick now updates Vault/mirror/black-box state and queues dt/quality/capacity; late frame ensures GPU state, flips A/B buffers, dispatches compute, binds render buffers, and renders indirect debris.
Rejected Alternatives: Whitelisting tool files as "presentation-owned"; allowing one-shot audio because it is small; moving audio to managed event spam; keeping `CarveDebris` dispatch in Tick because it is a renderer; creating new GameObject particle routes; single-buffering debris state.
Scalability potential: Low = sparse dirty-bit commits, four-slot builder audio cap, minimum debris capacity; Middle = current one-shot/audio/debris fidelity; High = richer shader heat/decal and bit-crushed radio; Ultra = denser debris compute and stronger GPU-only lies from the same scalar/DTO route. Gameplay truth ownership and DTO layouts remain unchanged across tiers.
Hardware Impact: Estimated 34-73 us shifted out of dirty tool/audio/debris frames on i3/MX350-class hardware. Not profiler-measured. Proof: `CURRENT_AUDIO_TOOL_DEBRIS_CALLGRAPH_HOT_PRESENTATION_COUNT=0`; `git diff --check` clean except CRLF normalization warnings. Build/Roslyn not rerun because CPU gate was closed at 90%.

## APEX Re-Audit Addendum 19 - Diegetic Panel Direct-Frame UI Closure

Problem: A current project direct body scan still found a real presentation write in `DiegeticPanelController.Tick`: panel-camera enable state and cursor presentation could be mutated from the `ITickable` lane. This is a UI-owned system, but it still violates the phase boundary because the tick lane is not the visual sink.
Solution: Added pending late-frame state for cursor visibility, cursor pose, and panel-camera enabled state. `Tick` and helper routes now compute panel interaction truth, cursor target position, and dirty flags. `LateFrameTick` applies `SetPositionAndRotation`, cursor `Graphic/Renderer/Collider.enabled`, and panel-camera enable changes, then continues existing phosphor/material refresh.
Rejected Alternatives: Whitelisting diegetic UI because it is presentation-owned; leaving `panelCamera.enabled` in Tick because the body hit was small; moving input processing itself to late frame and risking gameplay input order.
Scalability potential: Low = cursor/camera commits only when dirty; Middle = current diegetic panel RT/cursor behavior; High/Ultra = richer phosphor/cursor/material effects from the same late-frame presentation path.
Hardware Impact: Estimated 3-8 us shifted out of active panel tick frames. Not profiler-measured. Proof: `DIRECT_PROJECT_HOT_PRESENTATION_COUNT=0` after excluding `PerformanceMonitor.Tick -> Stopwatch.Stop()` as non-presentation; `git diff --check` clean except CRLF warnings. Build/Roslyn not rerun because CPU gate was closed at 68%.

## APEX Re-Audit Addendum 20 - Subagent Residual Closure

Problem: Read-only subagents found real residual phase leaks after Loop 31: `InternalFloodWaterlineRuntime.FastTick` published internal waterline shader globals; `SubmarineStructuralGrid.PostFixedTick` could register pressure-spray decals through `AbyssalFluidDecals`; `HectonSurfaceWeatherDirector.Tick` directly drove `SurfaceWeatherVfxRig` splash/lightning state; `FaunaBrain.SlowTick` wrote infection colors through `Material.SetVector`; PDA inventory detail refresh could allocate through `ToLowerInvariant()` and dynamic `new char[]`; active visor/HUD/compositor copy helpers could grow lists from frame paths.
Solution: Converted each route to scalar/DTO dirty state with VISUAL_SYNC ownership. Flood waterline now queues shader packets and flushes from `LateFrameTick`; structural breaches queue local spray DTOs and register decals only in `LateFrameTick`; surface weather stages binding delta and splash DTOs, then applies VFX rig state in `LateFrameTick`; fauna ecosystem overlays queue infection visual flushes and material writes run from `LateFrameTick`; PDA loadout recommendation uses `IndexOf(..., OrdinalIgnoreCase)` without lowercase allocation; PDA dynamic text uses preallocated staging/fallback arrays; active HUD/controller/compositor copies guard capacity before `Add`; visor BIOS font scan/material commit moved from `Tick` to `LateFrameTick`.
Rejected Alternatives: Treating `FastTick`/`PostFixedTick` as acceptable because the visuals are small; letting first-use PDA text expansion allocate in a rare refresh; allowing list growth because active HUD count is normally low; moving gameplay flood/fatigue/infection truth into shaders or decals; suppressing subagent findings as presentation-owned false positives.
Scalability potential: Low = dirty scalar queues, one bounded PDA fallback buffer, and no list growth; Middle = current flood/weather/fauna/PDA fidelity; High = denser weather/flood visual masks and richer fauna infection emission; Ultra = GPU-overkill waterline/weather/fauna lies from the same scalar DTOs without altering simulation truth.
Hardware Impact: Estimated 29-67 us shifted or avoided on dirty weather/flood/fauna/visor/PDA frames on i3/MX350-class hardware. Not profiler-measured. Proof: `PROJECT_DIRECT_HOT_PRESENTATION_OR_GC_COUNT=0`, `TARGET_DIRECT_HOT_PRESENTATION_OR_GC_COUNT=0`, targeted PDA/visor allocation grep has no `ToLowerInvariant()` or dynamic `new char[capacity]`, and `git diff --check` is clean except CRLF warnings. Fresh Roslyn/build proof remains blocked by CPU/compiler gate (`CPU=85`, active `dotnet`/`csc`).

## APEX Re-Audit Addendum 21 - Async Buoyancy GPU Readback Phase Closure

Problem: The remaining read-only subagent finding was real. `AsyncBuoyancyReadbackRuntime.PreSimulationTick` prepared requests and then called `DispatchGpuReadback`, which uploaded request/wave data into `GraphicsBuffer`, read shader globals, configured the wave-height compute shader, dispatched compute, and issued `AsyncGPUReadback.Request` from the pre-simulation phase. That kept a GPU/Vault presentation bridge inside a physics-owned phase.
Solution: Split request preparation from GPU submission. `PreSimulationTick` now updates time/quality, prepares request DTOs, writes tuning/counters, records a visual-sync dispatch flag, and clears queued request count. `VisualSyncTick` runs `FlushQueuedGpuReadbackDispatch`, which owns `DispatchGpuReadback`, `LockBufferForWrite`, shader-global reads, compute parameter binding, compute dispatch, and `AsyncGPUReadback.Request`. `ScheduleSimulation` consumes previous completed GPU snapshots with no wait, or uses the existing mock path when the prior visual-sync dispatch reported GPU unavailable.
Rejected Alternatives: Leaving readback submission in pre-simulation because it is physics-related; forcing same-frame GPU data into simulation; adding a blocking fence/readback; moving buoyancy truth into the shader; single-buffering the request payload.
Scalability potential: Low = fewer samples and mock fallback without GPU stalls; Middle = current triple-buffer readback cadence; High = denser request/wave payloads; Ultra = richer GPU-side wave sampling while simulation still reads immutable snapshots and never waits for presentation.
Hardware Impact: Estimated 8-19 us GPU API/pre-simulation contention shifted out of the physics phase on i3/MX350-class hardware. Not profiler-measured. Proof: `ASYNC_BUOYANCY_DIRECT_HOT_PRESENTATION_COUNT=0`; project direct scan with `PreSimulationTick` included reports `PROJECT_DIRECT_HOT_PRESENTATION_OR_GC_COUNT=0`. Fresh Roslyn/build proof remains blocked by CPU gate (`CPU=71`; no active compiler rows, but CPU >50% forbids launch).

## APEX Re-Audit Addendum 22 - Current-Source MicroFauna, MapMagic, DepthZone Closure

Problem: The post-report current-source pass still found real helper-chain leaks. `SargassumMicroFaunaBoids.SlowTick` could route through rematerialization, threat-grid refresh, spawn upload, and origin shift helpers into `GraphicsBuffer`, kernel validation, compute dispatch, and `LockBufferForWrite`. `HectonMapMagicVegetationBridge.Tick/SlowTick` could reach terrain `AsyncGPUReadback.RequestIntoNativeArray`, active vegetation `GraphicsBuffer` upload/bind, shader vegetation-audio globals, and mixer parameter writes. `DepthZoneDirector.SlowTick` pushed depth-zone event, narrative, and HUD notification presentation directly.
Solution: MicroFauna hot lanes now queue GPU-state refresh, threat-grid upload, spawn/grazing/threat/formation/leviathan uploads, render, and origin-shift dispatch; `LateFrameTick` owns `EnsureBuffers`, threat grid upload, kernel validation, compute dispatch, and all `GraphicsBuffer` uploads. MapMagic tick/slow lanes now queue tile readback disposal, deferred startup work, resident tile validation, active-buffer rebuild, and vegetation-audio handoff; `LateFrameTick` owns readback finalization/request, active A/B upload/bind, shader globals, and mixer writes. DepthZone now implements `ILateFrameTickable`; slow lane updates current-zone truth/cooldowns, while late frame raises depth-zone events, narrative discovery, notifications, and development logging.
Rejected Alternatives: Whitelisting MicroFauna and MapMagic because they are renderer-adjacent; keeping terrain height readback submission in slow tick because it feeds AI/physics; allowing notification/event publication from slow tick because it is infrequent; running `dotnet` under CPU >50%; using string/UI rewrites to hide scanner output without moving phase ownership.
Scalability potential: Low = dirty-bit late drains, sparse terrain cache validation, reduced boid/material upload cadence, and bounded UI text buffers. Middle = current foliage/microfauna/depth-HUD behavior. High = denser terrain/vegetation buffers and richer MicroFauna GPU visuals from the same scalar/native DTO route. Ultra = visual-overkill boid, terrain, vegetation-audio, and depth-HUD lies without changing simulation truth, DTO layout, or authority ownership.
Hardware Impact: Estimated 75-173 us shifted or avoided across dirty PDA/helmet/ecology/debris/flora/GPR/sargassum/cave/crest/wreck/indirect/MapMagic/MicroFauna/DepthZone frames on i3/MX350-class hardware. Not profiler-measured. Proof: `MICROFAUNA_HELPER_HOT_PRESENTATION_COUNT=0`; `TARGETED_WORLD_HELPER_HOT_PRESENTATION_COUNT=0`; `TARGET_PATCHED_DIRECT_HOT_PRESENTATION_OR_GC_COUNT=0`; scoped `git diff --check` clean except CRLF normalization warnings. Fresh build/Roslyn remains blocked by CPU gate (`CPU=100`; no active compiler rows returned after wait).

## APEX Re-Audit Addendum 23 - Compile-Wall Triage

Problem: The CPU/compiler gate briefly opened, but `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal -clp:ErrorsOnly` failed before X_004 could get a clean compile signal. The blocker was outside the presentation patch: `MathLodTortureResult` was written through `result.WorstOutput`, but the explicit-layout DTO had only padding at offset 56.
Solution: Restored `WorstOutput` as a `float` at `[FieldOffset(56)]`, preserving the existing `TortureResultSizeBytes` 64-byte ABI and leaving offset 60 as padding. This is a dependency unblock, not a presentation-domain behavior change.
Rejected Alternatives: Ignoring the compile failure as unrelated, changing the torture job to stop recording the output, expanding the struct size and breaking layout contracts, or launching another build while active compiler rows were present.
Scalability potential: Low/Middle/High/Ultra all keep the same 64-byte telemetry result route; the fix only restores deterministic torture telemetry compatibility.
Hardware Impact: 0 us runtime savings claimed. Build rerun remains blocked because active `dotnet`/`csc` rows were present and WMI CPU query returned `Access denied`, so the required CPU/compiler gate could not be proven clean.

## APEX Re-Audit Addendum 24 - Resource Interaction Presentation Closure

Problem: The current-source resource/gameplay slice still had mixed hot helper chains. `SealedDoor` cutting, `ScannableFragment` scan completion, `InteractionHighlighter`, harvestable plants/outcrops, oxygen bubbles, floaters, and oxygen plants could reach `MaterialPropertyBlock`, `Renderer.enabled`, particles, audio, component-disable presentation, or object-pool scene work from interaction/tick methods. `OxygenPlant.Tick/ForceRelease` also sampled `spawnPoint.position` and called `ObjectPoolService.Spawn` directly.
Solution: Converted hot methods to bounded pending state. `LateFrameTick` now owns MPB commits, renderer enable changes, scan registry sync, audio playback, particle playback, debris VFX signals, fragment disable/despawn, and oxygen bubble object-pool release. `OxygenPlant` queues at most four bubble releases per frame and samples scene transform / spawns pooled bubbles only in late frame.
Rejected Alternatives: Whitelisting gameplay resources because they are small, leaving oxygen bubble spawn in `Tick` as a "gameplay object" exception, spawning particles from interaction methods, or moving gameplay health/progress truth into animation/material state.
Scalability potential: Low = dirty-bit late drains and bounded release/audio queues. Middle = current pickup/scan/cut/harvest feedback. High = denser shader/particle feedback from the same scalar DTOs. Ultra = richer GPU/audio lies while gameplay truth remains in timers, health/progress fields, and pooled object ownership.
Hardware Impact: Estimated 18-44 us shifted out of dirty interaction/resource frames on i3/MX350-class hardware. Not profiler-measured. Proof: `TARGET_RESOURCE_DIRECT_HOT_PRESENTATION_OR_GC_COUNT=0`, `RESOURCE_HELPER_HOT_PRESENTATION_COUNT=0`, scoped `git diff --check` clean except CRLF warnings.

Problem: The user asked for a fresh paranoid proof that hull deformation/flooding and UI paths were not hiding presentation or string work.
Solution: Ran a current project forbidden-token candidate direct scan over 437 runtime files containing shader/material/audio/text/draw/dispatch/buffer tokens: `PROJECT_DIRECT_HOT_PRESENTATION_OR_GC_COUNT=0`. Ran the runtime UI/PDA/visor string frame scan: `RUNTIME_UI_STRING_FRAME_HITS=0`. Ran hull/flood/deformation direct scan: `HULL_FLOOD_DEFORM_DIRECT_HOT_PRESENTATION_COUNT=0`. CPU-trig search found no C# trig/noise in PDA/visor/camera/helmet effect routes; remaining non-shader hits are tooltip text and QA headless noise probes.
Rejected Alternatives: Reporting only the local 8-file pass, claiming stale Roslyn green, or launching a new build/analyzer while CPU/compiler gate is closed.
Scalability potential: Low/Middle/High/Ultra all keep simulation blind to presentation APIs in checked hot bodies; fidelity scales by `GlobalQualityWeight` via VISUAL_SYNC scalar/buffer uploads and shader-side sine/glitch/fog math, not CPU presentation loops.
Hardware Impact: 0 us measured for proof. Fresh build/Roslyn is still blocked by CPU 100 and active `dotnet` pid `18664`, so no clean compile claim is made for this final slice.

## APEX Re-Audit Addendum 25 - Fluid, Atmosphere, Weather, and Readback Phase Closure

Problem: The resumed current-source pass found real phase leaks. `HectonFluidEngine.FixedTick` could consume buoyancy GPU readbacks and release `GraphicsBuffer` resources from storage/fixed lanes. `AsyncBuoyancyReadbackRuntime.ScheduleSimulation` could consume GPU readbacks from the simulation schedule path. `SubmarineAtmosphereSystem.PostFixedTick` still reached room oxygen HUD, smoke overlay, visor glitch/distortion, audio log, pressure screech, and base-module ambient visual state. `HectonSurfaceWeatherDirector` still queued thunder audio and storm visor/flashlight pulses before the late-frame visual sink.
Solution: Fluid GPU resource release now records a bit-mask and drains in `LateFrameTick`; buoyancy readback consume is queued from fixed/storage lanes and flushed during late-frame visual sync. Async buoyancy consumes completed GPU snapshots only in `VisualSyncTick`; simulation reads previous immutable snapshots or the mock fallback without blocking. Atmosphere now implements `ILateFrameTickable` and stores pending HUD, smoke, visor, pressure, audio, and overheat scalar state; `LateFrameTick` owns all UI/audio/visor/base visual writes. Surface weather now queues thunder and storm-equipment pulses and applies them only in `LateFrameTick`.
Rejected Alternatives: Forcing same-frame GPU data into simulation, calling `GetData` from physics, releasing Unity graphics resources from fixed disposal paths, treating atmosphere UI/audio as harmless because it is infrequent, or moving gameplay oxygen/pressure truth into shader or audio state.
Scalability potential: Low = sparse late drains and mock buoyancy fallback without stalls. Middle = current weather/atmosphere/fluid fidelity. High = denser visual readback and weather pulse scalars. Ultra = richer GPU-side storm, helmet, fluid, and smoke lies from the same DTO route while simulation remains blind to render/audio state.
Hardware Impact: Estimated 21-48 us shifted out of dirty physics/pre-simulation frames on i3/MX350-class hardware. Not profiler-measured.

## APEX Re-Audit Addendum 26 - UI, World, and Resource Visual-Sync Closure

Problem: The next slice still had hot helper-chain risk around presentation state. PDA shell and suit HUD could grow buffers in rare refresh paths. `BeaconRuntime` changed light intensity from the regular update lane. `EnvironmentalHazard`, `CullingManager`, `ImpostorSystem`, `HectonVoxelStreamingBridge`, `HectonWorldGenerator`, `DestructibleOrganicManager`, `HostileFlora`, and `CarveDebrisComputeRenderer` still mixed renderer/material/audio/object-pool/GPU upload work with tick/slow/fixed helper chains. `HostileFlora` also used transform-facing presentation as part of attack truth.
Solution: UI buffers are fixed and truncated instead of grown; suit HUD gauge buffers are preallocated and refreshed in late frame. Beacon light writes moved to `LateFrameTick`. Hazard indicators, renderer cull state, impostor enable/material/object-pool work, voxel stale despawn, world-generator material restoration, destructible organic audio, and carve-debris buffer uploads now queue bounded dirty state and flush in late frame. Hostile flora now keeps a logical aim rotation for gameplay truth and applies bone rotation/audio playback only in `LateFrameTick`.
Rejected Alternatives: Allocating on rare UI refreshes, whitelisting renderer state because files are presentation-adjacent, keeping transform orientation as attack truth, single-buffering debris uploads, or synchronously despawning voxel volumes from slow tick.
Scalability potential: Low = fixed buffers, dirty-bit renderer commits, bounded audio queues, minimum debris upload cadence. Middle = current UI/world feedback. High = denser impostor/debris/organic effects. Ultra = overkill shader/audio/world presentation using late-frame scalar/native DTOs without changing simulation ownership.
Hardware Impact: Estimated 52-119 us shifted or avoided on dirty UI/world/resource frames on i3/MX350-class hardware. Proof: `PATCHED_DIRECT_HOT_PRESENTATION_OR_GC_COUNT=0`, `PATCHED_GPU_HOT_COUNT=0`, `PATCHED_UI_STRING_FRAME_COUNT=0` across 82 parsed bodies in the latest 18 edited files; scoped `git diff --check` is clean except CRLF normalization warnings. Fresh build/Roslyn is blocked: CPU later fell to 4%, but active `dotnet` pids `20408`, `20944`, `36532`, `44188`, `46516`, and `46648` remained, and the repo rule forbids launching another `dotnet` while those rows exist. A wide PowerShell project scan timed out at 180 seconds and is not claimed as green.

## APEX Re-Audit Addendum 27 - Current-Source Helper-Chain Closure

Problem: The resumed current-source pass found hidden helper-chain presentation work after the last report. `DeepPsychosisController.Tick` could play psychosis audio; `ShinobuOceanSurfaceAtmosphereRuntime.Tick` consumed GPU readbacks; `HectonGIRelaySystem.SlowTick` bound the water cubemap; `RenderTextureLifecycleTracker.SlowTick` ran leak reporting; `ObjectPoolManager.DespawnTimer.Tick` despawned scene objects; `PlayerFootstepAudio.Tick` played footstep clips; `DeployableBeacon.Tick` and `BioReactor.Tick` reached MPB/audio sinks; `DiegeticVisorHudMesh.Tick`, `DiegeticGyroCompassRuntime.SlowTick`, `PDADecryptionSpectrogramPanel.Tick`, `ToolDiegeticDisplayController.Tick`, and `WristHologramHudRuntime.Tick` could allocate/create/update graphics resources; `LifePodTactilePrologueController.Tick` could write BIOS/haptic presentation; `SargassumCutManager.Tick/SlowTick` could resolve visual particle dependencies; QA headless ghost jobs still used CPU noise.
Solution: Hot lanes now stage dirty bits, scalar DTOs, pending audio cues, and bounded counters only. `LateFrameTick` owns psychosis/footstep/reactor/beacon audio, delayed pool despawn, RT leak checks, GI cubemap binding, ocean readback consumption, visor/PDA/tool/wrist graphics resource creation, LifePod BIOS/haptic writes, and sargassum visual dependency lookup. The headless ghost AUP job replaced `noise.cnoise` with a deterministic integer hash, removing CPU noise from the job route.
Rejected Alternatives: Whitelisting UI/presentation-owned `Tick` methods; leaving rare resource creation in hot loops; using single-frame GPU readback consumption in simulation cadence; keeping direct `PlayAtPoint` because it is small; suppressing helper hits as scanner noise; replacing gameplay truth with shader state.
Scalability potential: Low = dirty-bit sparse late drains, fixed UI buffers, bounded pending audio, previous-frame GPU snapshots, and integer-hash QA drift. Middle = current visual behavior with stable phase ownership. High = richer shader-side visor/PDA/wave/GI lies from the same scalar buffers. Ultra = visual overkill in VISUAL_SYNC without changing DTO layout, save identity, authority route, or simulation truth.
Hardware Impact: Estimated 39-94 us shifted or avoided on dirty frames on i3/MX350-class hardware. Not profiler-measured. Proof: `FAST2_CURRENT_DIRECT_HOT_PRESENTATION_OR_GC_COUNT=0` over 488 candidate files and 656 hot bodies; `TARGET_HELPER_HOT_PRESENTATION_COUNT=0` over 242 patched-file methods; `BROAD_HELPER_HOT_PRESENTATION_COUNT=0` over 13,852 runtime candidate methods; `UI_STRING_FRAME_HITS=0` over 169 UI/Visor/PDA files; checked CPU trig/noise presentation routes returned no hits. Build/Roslyn remains blocked by CPU/compiler gate (`CPU=100`, active `dotnet` pids `10076`, `39296`).

## APEX Re-Audit Addendum 28 - Phase-Edge UI/World Closure

Problem: The new pass found real phase-edge leaks missed by the previous token set. Tool screen `Tick` reached TMP `SetCharArray` and MPB writes through `RefreshTextAndShaderState`; PDA spectrogram `Tick` could rebuild native handles; wrist HUD `Tick` could refresh material state, run editor CSV string/file work, and schedule the glyph job; vehicle cockpit `Tick` could allocate/release external render textures; suit HUD `Tick` still ran component/hierarchy resolving plus presentation apply; Manta residency/update and fixed lanes spawned/despawned pooled scene objects or toggled pickup/highlighter behaviours; biolum zone `Tick` applied pooled Light GameObject state; PlayerPDA `Tick` and diagnostic `SlowTick` applied canvas fade, tab visibility, audio, low-battery close, `TryGetComponent`, and diagnostic TMP writes.
Solution: Converted each route to VISUAL_SYNC ownership. Hot methods now stage booleans, scalar state, pending audio clips, low-battery close requests, dirty refresh bits, and timing values only. `LateFrameTick` now owns TMP writes, MPB commits, graphics/native resource refresh, glyph job scheduling, editor CSV polling, external RT acquire/release, HUD presentation resolving/apply, Manta residency hydration/despawn, biolum Light activation/properties, PlayerPDA canvas fade/tab/headless state/audio flush, and diagnostic terminal text refresh.
Rejected Alternatives: Treating UI-owned `Tick` as safe, suppressing `SetCharArray` because it is zero-GC, leaving object-pool spawn/despawn in update/fixed lanes, or moving gameplay truth into animation/material/light state.
Scalability potential: Low = bounded dirty-bit late drains, fixed char buffers, sparse light/RT refresh, and one-frame visual latency. Middle = current UI/world behavior. High = richer HUD/PDA/cockpit/biolum presentation from the same DTO state. Ultra = visual-overkill shader/TMP/light lies in VISUAL_SYNC without changing simulation authority or save identity.
Hardware Impact: Estimated 36-82 us shifted or avoided on dirty UI/world frames on i3/MX350-class hardware. Not profiler-measured. Proof: target helper callgraph over 8 edited files reports `TARGET_HELPER_COUNT=0`; project direct hot-body candidate scan reports `PROJECT_DIRECT_CANDIDATE_FILES=825`, `HOT_BODIES=1029`, `PROJECT_DIRECT_HITS=2`, both false positives (`PerformanceMonitor.Stopwatch.Stop()` and `UpgradeMatrixCompiler` local variable `enabled =`). Broad helper scan timed out at 124 seconds, so no project-wide helper-clean claim is made. Build/Roslyn remains blocked by CPU/compiler gate (`CPU=100`, active `dotnet` pids `19092`, `32556`).

## APEX Re-Audit Addendum 29 - GraphicsBuffer, Upload, and Hull Feedback Closure

Problem: The next source pass exposed remaining upload/readback and phase boundary defects. `SpatialAudioManager` published the acoustic radar GPU payload through a single buffer and `SetData`. `BaseModule`, GPU scatter, HLOD, distant landmarks, thermal grid, vegetation telemetry, abyssal vents/smoke, seam-dither args, cockpit fallback damage glyphs, and diegetic tooltip glyph/UV/args paths still had single-buffer or SetData-style upload residues. Crest depth-cache forensic PNG used blocking `ReadPixels`. `PhysicsApplySystem` called a decal-named hull feedback route from collision resolution, and `SubmarineStructuralGrid.QueueHullImpactDecalLocal` resolved transforms before VISUAL_SYNC.
Solution: Converted the current slice to lock-write `GraphicsBuffer` ownership and A/B promotion where the GPU may read the previous frame. Acoustic radar now writes inactive A/B buffers via `GraphicsBufferUploadUtility.UploadArray` and publishes the promoted read buffer. Base module water/flicker, GPU scatter, HLOD, distant landmarks, abyssal thermal, diegetic tooltip glyphs/UV/args, seam args, vegetation telemetry, cockpit fallback glyphs, and thermal grid visual upload now avoid `SetData`. Crest debug PNG now uses dev-only async GPU readback and returns false in release. Hull impact physics now calls `QueueHullImpactFeedbackLocal`; structural grid stores DTOs and resolves transform, sparks, decal signal, and camera shake in `LateFrameTick`.
Rejected Alternatives: Keeping single-buffer `SetData`; blocking depth-cache `ReadPixels`; weakening the scanner; treating physics-to-decal calls as harmless because the callee is small; using same-frame fences; moving gameplay collision truth into a shader path.
Scalability potential: Low = sparse dirty uploads, fixed buffers, one-frame hull feedback latency, and no same-buffer CPU/GPU contention. Middle = current visual behavior. High = denser tooltip/cockpit/scatter/HLOD/thermal payloads from the same DTOs. Ultra = visual-overkill GPU lies through A/B buffers without changing simulation truth, save identity, or authority routes.
Hardware Impact: Estimated 55-126 us stall/API contention risk shifted or avoided on dirty frames on i3/MX350-class hardware. Not profiler-measured. Proof: scoped tracked rewrite count is 20 files; runtime `SetData`, `UploadArraySetData`, and `ReadPixels` search returns no hits in `Assets/_Project/Scripts`; touched hot-method scan reports `PHYSICS_HULL_UI_AUDIO_TOUCHED_HOT_HITS=0`; UI string scan reports `UI_STRING_FRAME_HITS=0` across 169 files and 136 frame bodies; scoped `git diff --check` is clean except CRLF warnings. Build/Roslyn remains blocked by CPU/compiler gate (`CPU=73-88`, active `dotnet` rows and earlier `VBCSCompiler` row).

## APEX Re-Audit Addendum 30 - Current Slice Buffer Sovereignty Re-Sweep

Problem: The resumed source pass found phase-correct VISUAL_SYNC routes that still allowed CPU uploads to single GPU-read buffers. The concrete offenders were PDA sonar constants/HLOD AUP, seam-dither matrix/color/args payloads, GPU scatter flora age/phase/visual-payload/frame constants, cockpit button/damage hologram payloads, sargassum scavenger BRG matrices, sargassum cut/damage stamp command buffers, marauder outpost shell matrix/cell/args buffers, and ecosystem flora predator AUP.
Solution: Converted the current 20-file X_004 slice to inactive-write/active-read promotion. CPU upload paths now resolve a write buffer, upload through `GraphicsBufferUploadUtility` or lock-write, publish that buffer as the active shader/read buffer, then flip the index. Render/compute consumers bind only active buffers. PDA, cockpit, outpost, ecosystem, sargassum, seam-dither, scatter, GPR, biolum, flora, crab IK, wreck, habitat stress, fluid wake/buoyancy, and MapMagic predator-fear routes now use A/B buffer ownership where GPU and CPU could otherwise overlap.
Rejected Alternatives: Trusting VISUAL_SYNC phase ownership alone; keeping single buffers because the upload is late-frame; using blocking fences; using `SetData`; pushing presentation payloads back into simulation-owned DTO layout; launching a build while CPU/compiler gate is closed.
Scalability potential: Low = fixed capacity buffers, sparse dirty uploads, and one-frame visual latency on weak hardware. Middle = current density. High = denser PDA/cockpit/scatter/sargassum/outpost payloads. Ultra = visual-overkill GPU lies from the same scalar/native DTOs without changing simulation truth, save identity, authority route, or Vault ownership.
Hardware Impact: Estimated 68-154 us stall/API contention risk shifted or avoided on dirty presentation frames on i3/MX350-class hardware. Not profiler-measured. Proof: scoped `git diff --stat` reports 20 tracked runtime files; no converted old single-buffer symbols remain in the 8 newly touched files; runtime `SetData`, `UploadArraySetData`, and `ReadPixels` search returns no hits; `TARGET_20_HOT_PRESENTATION_DIRECT_HITS=0`; `TARGET_UI_STRING_FRAME_HITS=0`; scoped `git diff --check` is clean except CRLF warnings. Build/Roslyn remains blocked by CPU/compiler gate (`CPU=100`, active `csc` pid 45568 and `dotnet` pid 48620).

## APEX Re-Audit Addendum 31 - ProceduralOreSpawner Roslyn Fatal Closure

Problem: The fresh `PresentationDecouplingAudit` after the clean build still reported two fatal hot paths, both in `ProceduralOreSpawner`: `SlowTick -> WriteIndirectArgsGpu` reached `GraphicsBuffer.LockBufferForWrite` and `UnlockBufferAfterWrite` for indirect draw args.
Solution: Split truth/state update from GPU publication. `UpdateIndirectArgsBuffer` now writes the Vault DTO and queues a `GeologyIndirectArgsDTO` for visual sync. All slow-tick and rebind clear paths call `QueueIndirectArgsGpu` only. `LateFrameTick` now drains `FlushPendingIndirectArgsGpu` immediately before `RenderDormantOres`, so lock-write GPU args updates are owned by VISUAL_SYNC.
Rejected Alternatives: Leaving the call because indirect args are small; running a fence in `SlowTick`; moving ore render instance truth into the args buffer; suppressing the Roslyn finding; rerunning the analyzer while Unity VBCSCompiler remains alive.
Scalability potential: Low = one queued indirect args DTO and sparse late-frame GPU write. Middle = current dormant ore draw cadence. High = denser ore impostor presentation. Ultra = visual-overkill ore shimmer/decay through shaders while ore truth remains Vault-owned.
Hardware Impact: Estimated 2-5 us GPU API contention shifted out of dirty slow-tick frames on i3/MX350-class hardware. Not profiler-measured. Proof: clean build succeeded with 0 warnings / 0 errors before this patch; fresh Roslyn audit before this patch reported only these 2 fatal findings; after patch `ORE_HOT_PRESENTATION_DIRECT_HITS=0`. Roslyn rerun is blocked by active Unity VBCSCompiler `dotnet` pid 48968 and CPU >50 on follow-up checks.

## APEX Re-Audit Addendum 32 - Phase-Edge UI/Audio/Visor Closure

Problem: The resumed helper-chain scan found presentation work still reachable from frame/slow lanes after the buffer pass. The concrete routes were HUD notification text/canvas alpha, quickbar TMP refresh, Manta HUD text, airlock mixer snapshots/pressure whistle events, player cave reverb mixer/filter writes, relay cable visual submission, PDA barter/construction text refresh, marker/beacon/AR waypoint UI updates, subtitle TMP/canvas writes, UI scaler layout/scale commits, acoustic reverb snapshot transitions, diegetic PDA shell state, tool screen renderer visibility/render-texture decisions, visor lens native/GPU state, screen compositor overlay setup, and visor HUD material/projection state.

Solution: Converted the current 20-file slice to dirty scalar/DTO queues consumed from `LateFrameTick`. Hot lanes now sample gameplay/UI input, timers, signal snapshots, and simple booleans only. `LateFrameTick` owns TMP `SetCharArray`, CanvasGroup alpha, MaterialPropertyBlock commits, AudioMixer/Snapshot writes, procedural audio pings, cable visual submission, render-texture acquire/release, visor GPU upload, and compositor/projection binding. For presentation-owned controllers that were registered as `IUpdatable`, runtime registration now uses `ILateFrameTickable` directly so the method body is no longer a simulation/update lane.

Rejected Alternatives: Suppressing UI-owned `Tick` as harmless; keeping direct `SetCharArray`/CanvasGroup writes in `Tick` because they are allocation-free; using Unity `LateUpdate` outside the dispatcher; moving player/airlock/visor truth into materials; retaining same-frame audio snapshot writes from gameplay completion; launching build/Roslyn while CPU gate is closed.

Scalability potential: Low = sparse dirty-bit late drains, fixed char buffers, one-frame visual latency, and fewer audio/material API touches on weak devices. Middle = current HUD/PDA/visor behavior. High = denser visor/HUD/compositor presentation from the same DTOs. Ultra = shader/material/audio overkill inside VISUAL_SYNC without changing simulation authority, DTO layout, save identity, or Vault ownership.

Hardware Impact: Estimated 47-112 us shifted or avoided on dirty UI/audio/visor frames on i3/MX350-class hardware. Not profiler-measured. Proof: current scoped rewrite count is 20 files; runtime upload/readback search has no `SetData`, `UploadArraySetData`, `ReadPixels`, `LockBufferForWrite`, or `UnlockBufferAfterWrite` hits; `TARGET20_DIRECT_HOT_PRESENTATION_OR_GC_COUNT=0`; `TARGET20_HELPER_HOT_PRESENTATION_OR_GC_COUNT=0`; `RUNTIME_UI_STRING_FRAME_HITS=0`; scoped `git diff --check` is clean except CRLF normalization warnings. Fresh build/Roslyn is blocked by CPU gate (`CPU=100`, no compiler rows).
