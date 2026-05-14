# UX_HPHI_SIGNAL_WIRING Rationale

## Initial Authority And Scope
Problem: Legacy UI controllers are suspected to poll singletons or registry state in `Update()`, weakening H-Phi purity and causing avoidable hot-path work.
Solution: Treat the active XML prompt and Echelon 8 Presentation & UX domain as scope, then refactor only UI-owned code or cross-domain contracts required for signal boundaries.
Rejected Alternatives: Borrowing implementation details from neighboring prompts, editing concrete player/inventory systems directly, or using scene search as a runtime transport.
Scalability potential: Low uses 15Hz dirty evaluation and char buffers; Middle/High use fuller signal cadence; Ultra can spend saved CPU on richer glyph/VR/holographic feedback without changing hot-path allocation law.
Hardware Impact: Expected low-end gain is removal of per-frame singleton/registry polling and string churn; exact i3/MX350 microseconds remain PENDING VERIFICATION until profiling.

## Mandate Selection
Problem: UI refactor touches hot text paths, registry access, input glyphs, VR placement, and telemetry.
Solution: Read UI_Data_Streaming_ZeroGC_Optimization, UI_Diegetic_Physical_Interfaces, ARCH_Global_Registry_ServiceLocator_DI_Init, OPT_Zero_GC_Policy_AllocFree_Mandate, DBG_Telemetry_Crash_Reporting_PostMortem, OPT_Performance_Budgets_FrameTime_VRAM_Limits, CTRL_Device_Abstraction_Haptics, and REND_VR_Stencil_Masking before code.
Rejected Alternatives: Generic Unity UI update patterns, `TMP_Text.text`, `FindObjectOfType`, `Quaternion.Slerp`, or Update-based polling.
Scalability potential: Toaster path is dirty-flagged, throttled, text-buffer only; high-end path may add higher-cadence diegetic/VR feedback from the same signal snapshots.
Hardware Impact: Static expectation is sub-0.1 ms UI control overhead on MX350; measured proof absent.

## Authority Review Boundary
Problem: The project has stale root mirrors and many concurrent dirty files, so a source-only scan can target the wrong authority or overwrite another agent.
Solution: Use `AGENTS.md`, `Docs/README.md`, the Echelon domain map, signal/dispatch/UI architecture docs, and the active XML tag as the authority chain; treat existing dirty UI files as shared workspace state.
Rejected Alternatives: Trusting `PROJECT_ATLAS.md` as current authority, assuming all `IUpdatable` UI is within this prompt, or rewriting broad UI subsystems without ownership proof.
Scalability potential: Low keeps scope on gameplay HUD signal plumbing; Middle/High/Ultra can expand visual richness only after the reactive path is clean and compile-proven.
Hardware Impact: The expected gain is bounded to removed polling/string churn in selected hot UI components; broad project-wide estimates remain invalid until profiler proof exists.

## Loop 1 Late-Frame Ownership
Problem: Main HUD and interaction prompt were registered as `IUpdatable` UI owners, so their visual work drained in dispatcher Update rather than VISUAL_SYNC.
Solution: Keep compatibility `Tick(float)` bodies, but stop runtime updatable registration and drive them from `ILateFrameTickable.LateFrameTick()`; legacy updatable registration is actively unregistered if present.
Rejected Alternatives: Adding MonoBehaviour `Update()`, adding a new dispatcher phase, or migrating every UI class in one broad pass without ownership proof.
Scalability potential: Low/MX350 runs HUD dirty evaluation on a 15Hz gate unless a signal lands; Mid/High/Ultra keep high-cadence visual sync and can spend the saved Update-lane time on richer HUD effects.
Hardware Impact: Expected gain on i3/MX350 is removal of one main HUD visual solve and one prompt solve from the Update lane on non-dirty frames; exact microseconds remain unmeasured because compile is blocked upstream.

## Loop 1 Signal Binding
Problem: HUD dirty state was cadence-driven and only partially signal-aware through system health.
Solution: Add `ConsumeReactiveSignals()` for `PlayerStateSignal`, `InventoryChangedSignal`, `InputStateSignal`, and `SystemHealthSignal`; inventory revisions invalidate quickbar caches, player squeeze drives a cheap pulse boost, input-scheme changes dirty prompt/glyph state.
Rejected Alternatives: Reading `GlobalRegistry.Player` / `PlayerInventory` every frame as the reason to redraw, or creating managed C# events that bypass the signal corridor.
Scalability potential: Low uses signal-triggered updates plus 15Hz fallback; Middle uses signal-triggered text refresh at existing cadence; High/Ultra retain smooth per-frame visual effects while text and icon churn stay dirty-gated.
Hardware Impact: Static estimate: saves repeated quickbar cache churn and registry-triggered redraw decisions on MX350; profiler proof unavailable until assembly references compile.

## Loop 1 Compile Gate
Problem: `Assembly-CSharp.csproj` cannot compile due missing assembly/domain references unrelated to this UI patch (`Environment.Fluids`, `Audio.Virtualization`, `Core.Scheduling`, `World.Terrain`, WFC/outpost contracts, etc.).
Solution: Treat this as dependency wall strike one for compile verification, then rerun a filtered build search for touched UI file diagnostics.
Rejected Alternatives: Editing cross-domain asmdefs blindly, reverting other agents' work, or declaring a clean compile without evidence.
Scalability potential: No runtime scalability change; this preserves the patch and isolates the build blocker for integrator repair.
Hardware Impact: None until compile wall is resolved.

## Loop 2 Dirty Rendering
Problem: UI text and icon refresh needed a frame-local signal reason, not a blind cadence reason.
Solution: Signal snapshots now set bounded dirty flags; inventory/input dirty state survives until the corresponding quickbar/prompt refresh consumes it, while player and system health flags clear after a visual pass.
Rejected Alternatives: Making UI subscribe to managed events, polling concrete services for dirty decisions, or rebuilding prompt strings every frame.
Scalability potential: Low/MX350 gets a 15Hz fallback only when no signal lands; Mid/High/Ultra retain smooth visual effects without reviving string churn.
Hardware Impact: Expected MX350 gain is fewer quickbar and prompt cache invalidations on no-change frames; exact microseconds are blocked by the compile wall.

## Loop 2 VR And Glyphs
Problem: VR projection canvas hard-follow can jitter, and prompt glyph changes were coupled to input service events rather than the deterministic input signal lane.
Solution: XR projection pose now uses `CinematicMath.FastNlerp` for rotation and a cheap position lerp; `InteractionUI` consumes `InputStateSignal.CurrentInputSchemeHash` and maps the known KBM/Gamepad/SteamDeck/XR hashes into existing cached glyph/localization expansion.
Rejected Alternatives: `Quaternion.Slerp`, per-frame `GlobalRegistry.NativeInputManager` polling, or allocating new glyph strings on every prompt probe.
Scalability potential: Low keeps glyph rebuilds on scheme transitions only; High/Ultra can use the same binding for richer prompt artwork because the transport is signal-driven.
Hardware Impact: Expected gain is jitter reduction in XR and removal of display-style polling from prompt refresh; no profiler number yet.

## Loop 2 H-Phi Evidence
Problem: The prompt requires deletion counts as disk evidence.
Solution: Added `Docs/AgentLogs/HphiUiUpdateDeletion_UX_HPHI_SIGNAL_WIRING.md` with direct Unity Update count, dispatcher Update-lane purge count, and compile-filter evidence.
Rejected Alternatives: Reporting counts only in final chat or overstating deleted methods when static scan found zero direct UI `Update()` methods.
Scalability potential: No direct runtime scaling; this prevents false architectural reporting.
Hardware Impact: None beyond making the two purged dispatcher Update-lane owners auditable.

## Loop 3 Low-Tier LOD And Telemetry
Problem: H-Phi reactive UI still needed bounded fallback work for low-end devices and a measurable per-frame update counter.
Solution: Gate non-signal HUD dirty evaluation to every fourth frame on Low/Mx350, while reactive signal hits bypass the throttle; publish `ActiveUiUpdatesPerFrame` through hash-only `GlobalTelemetryBus.PublishPerformanceWarning`.
Rejected Alternatives: A global 15Hz UI refresh that would delay warnings/input glyphs, managed log strings, or editor-only counters that disappear in player builds.
Scalability potential: Low/Mx350 gets 15Hz fallback text/icon churn with immediate signal response; Middle/High keep normal visual cadence; Ultra can use the same telemetry to budget extra hologram/glyph polish.
Hardware Impact: Expected low-end gain is fewer HUD dirty evaluations on no-signal frames; measured microseconds remain blocked by compile/profiler unavailability.

## Loop 3 Static Zero-GC And Compile Wall
Problem: The prompt demands no `string.Format`/LINQ in the UI update path and a compile-backed allocation check, but the project build fails before UI diagnostics.
Solution: Run constrained scans on touched UI files for `string.Format`, LINQ operators/usings, interpolation, `SetText`, and `TMP_Text.text`; then run a third compile strike and classify the same missing cross-domain namespaces/contracts as an upstream assembly wall.
Rejected Alternatives: Trusting a polluted whole-workspace scan, editing AI/Audio/Physics/World asmdefs from the UX domain, or reporting a clean runtime allocation proof without a compiling domain.
Scalability potential: The hot path now stays char-buffer/dirty-flag based; existing managed prompt strings remain cold transition state until a broader interaction-contract refactor can replace `IInteractable.GetInteractText()`.
Hardware Impact: Static estimate on i3/MX350 is removal of Update-lane solve ownership and fewer no-op HUD text evaluations; exact saved microseconds are PENDING until the assembly wall is repaired and Unity profiler data exists.

## OMEGA POLISH CHANGES
Problem: The low-tier 15Hz gate used a four-frame modulo check after core completion; the cadence is correct, but the polish mandate requires deleting avoidable arithmetic.
Solution: Replace `cadenceFrame % 4 == 0` with `(cadenceFrame & 3) == 0` through `LowTierDirtyCadenceFrameMask`, preserving the 15Hz-at-60fps cadence and signal bypass.
Rejected Alternatives: Keeping the modulo because JIT/compiler may optimize it, or throttling all UI work to 15Hz and delaying warning/input signals.
Scalability potential: Low/Mx350 takes the bitmask fallback path; Middle/High/Ultra keep full visual cadence and can spend the saved budget on richer H-Phi glyph/VR presentation.
Hardware Impact: Measured gain is 0 us because the editor/project cannot compile for profiler proof; static estimate is sub-1 us per low-tier HUD dirty-evaluation tick on i3/MX350.
Cinematic Cheats Used: signal-bypassed 15Hz dirty gate, bitmask cadence test, rational `ApproximateOneMinusExpNeg` damping, `CinematicMath.FastNlerp` XR lazy-follow, TMP `SetCharArray` staging buffers.
Final Git Diff:
```diff
diff --git a/Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs b/Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs
@@
-        private const int LowTierDirtyCadenceFrameModulo = 4;
+        private const int LowTierDirtyCadenceFrameMask = 3;
@@
-                cadenceFrame % LowTierDirtyCadenceFrameModulo == 0;
+                (cadenceFrame & LowTierDirtyCadenceFrameMask) == 0;
```

## H-PHI UPGRADE PASS 1 - VISUAL SYNC SWEEP
Problem: The first pass cleaned the main HUD/prompt path, but clean UI-owned visual controllers still occupied the dispatcher Update lane through `ITickable` / `IUpdatable` registration. This kept presentation-only work split across Update and VISUAL_SYNC, reducing H-Phi purity and adding unnecessary dispatcher fan-out.
Solution: Converted `AnalogGaugeNeedle3D`, `AcousticRadarSphereRenderer`, `SubmarineSonarHoloMapRenderer`, and `PDAMapTab` to late-frame-only ownership. PDA map cadence, acoustic blip matrix sampling, sonar mesh sampling, and analog spring motion now execute from `ILateFrameTickable.LateFrameTick()` with `SystemDispatcher.CurrentFrameDeltaTime` where needed.
Rejected Alternatives: A broad UI assembly rewrite, touching currently dirty files owned by other agents, direct Unity `Update()`, keeping no-op `Tick(float)` shims, or inventing cross-domain dependencies to force signal availability.
Scalability potential: Low keeps fewer dispatcher lanes and delayed sonar mesh LOD changes; Middle keeps normal visual cadence; High/Ultra retain smooth interpolation and denser sonar map grids without Update-lane split work. The submarine holo-map now probes quality tier at 0.5s cadence and requires 2s hysteresis before changing LOD.
Hardware Impact: Measured gain is 0 us because profiler proof is blocked. Static impact on i3/MX350: four UI controllers removed from dispatcher Update ownership, one-frame visual handoff removed for acoustic/sonar/PDA map paths, and submarine quality-tier reads reduced from 60/s to 2/s while visible.
Regression Model: CPU risk is lower dispatcher fan-out but slightly more work inside VISUAL_SYNC for the same visual frame; GC risk unchanged because no managed allocation pattern was added; memory risk unchanged; cadence risk is contained by preserving existing refresh intervals; correctness risk is visual-only timing change because sampling and draw now occur in the same late-frame phase.
Failure Modes: If a dependency appears after `OnEnable`, visual controllers still depend on existing registration timing; no broad retry loop was added because it would reintroduce hot registry polling. Existing compile wall still blocks Unity/player verification.
Verification: Constrained `rg` found no `ITickable`, `IUpdatable`, `TryRegisterUpdatable`, public `Tick`, LINQ, `string.Format`, interpolation, `SetText`, `.text =`, or `Quaternion.Slerp` in the four edited files. `git diff --check` passed except line-ending warnings. Filtered `dotnet build Hecton8.Core.csproj --no-restore -m:1 /p:BuildInParallel=false` returned only existing non-UX missing type/namespace errors and no diagnostics for the edited UI files.

## H-PHI UPGRADE PASS 2 - MICRO VISUAL CONTROLLERS
Problem: After the HUD/prompt and first visual sweep, smaller UI controllers still implemented `ITickable` / `IUpdatable` and registered presentation-only work into dispatcher Update. Several also stayed registered while idle, and one screen-shake overload lost its caller-supplied intensity before playback.
Solution: Converted `UIScreenShake`, `UIFadeTransition`, `SettingsPanelAnimator`, `LocalizedTextMadnessFx`, `ShaderCompassRibbon`, `AudioWaveformAnimator`, `DiegeticPdaFocusDistanceController`, and `PDADataArchaeologyDecryptLabel` to `ILateFrameTickable` ownership. Active controllers sample `SystemDispatcher.CurrentFrameDeltaTime` in VISUAL_SYNC and unregister when idle, disabled, or complete. `UIScreenShake` now stores `_activeShakeIntensity`, and the PDA archaeology label caches quality-tier scramble permission with 0.5s probes and 2s hysteresis.
Rejected Alternatives: A broad UI rewrite across currently dirty files, direct Unity `Update()`, keeping no-op `Tick(float)` shims, per-frame quality-tier reads, mutating serialized shake intensity during playback, or adding coroutine-based fades/animations.
Scalability potential: Low/toaster path removes eight more dispatcher Update owners, disables focus raycast ownership when inactive, and probes scramble quality at 2 Hz instead of 60 Hz. Middle keeps normal visual cadence in VISUAL_SYNC. High keeps smooth diegetic and panel motion without extra allocation. Ultra/$5000 machine can spend the recovered scheduler fan-out on denser H-Phi glyph noise, richer spectrogram labels, and PDA focus polish because the transport remains late-frame and allocation-clean.
Hardware Impact: Measured gain is 0 us because compile/profiler proof is unavailable. Static impact on i3/MX350: eight dispatcher Update slots removed this pass, fourteen total scoped UI Update-lane owners purged/neutralized across all passes, PDA scramble quality reads reduced from about 60/s to 2/s while visible, focus-distance raycast owner unregistered while inactive, and idle fade/shake/panel effects no longer occupy Update registration.
Cinematic Cheats Used: active-only visual registration, cached dispatcher delta, cheap screen-shake noise envelope, late-frame same-phase sampling, quality-tier hysteresis, fixed-rate quality probes, and pooled TMP char buffer writes.
Regression Model: CPU risk drops through lower dispatcher fan-out and fewer idle owners. GC risk is unchanged because no managed hot-path allocation APIs were added and text remains `SetCharArray`. Cadence risk is visual-only: work moved from Update to VISUAL_SYNC while preserving existing state machines. Correctness risk was reduced for `Shake(float)` because caller intensity now persists for the active shake.
Failure Modes: `SystemDispatcher.CurrentFrameDeltaTime` must be valid for late-frame callbacks; this is already an established project pattern. If GlobalRegistry dispatcher is absent at activation time, existing registration semantics still defer ownership. Compile verification remains blocked by project-wide build state and timeout, so Unity runtime proof is pending integrator repair.
Verification: Constrained static scans over the eight edited files found no `ITickable`, `IUpdatable`, `TryRegisterUpdatable`, public `Tick`, LINQ, `string.Format`, interpolation `$"`, `SetText`, `.text =`, `Quaternion.Slerp`, or `math.slerp`. `git diff --check` reported only CRLF normalization warnings. Filtered `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:minimal -m:1 /p:BuildInParallel=false` timed out after 184 seconds before diagnostics; previous compile evidence still classifies the project as blocked by upstream non-UX assembly/domain errors.

## H-PHI UPGRADE PASS 3 - LOCALIZATION AND TERMINAL VISUAL OWNERS
Problem: More narrow UI owners still used dispatcher Update for event-driven localization layout, tooltip fades, terminal typing effects, SDF text sharpness, settings preview refresh, and development dashboards. These are presentation/visual-sync concerns, not gameplay Update logic.
Solution: Converted `WorldSpaceTMPSharpnessController`, `LocalizedTMPAutoSizer`, `LocalizedLayoutMirror`, `SettingsLivePreview`, `SettingsComparisonView`, `UITooltip`, `BIOSMessageStreamer`, `HectonSubmarineOsDisplay`, `BlackBoxMetricDashboard`, and `EngineHealthOverlay` to `ILateFrameTickable`. The one-shot localization/layout owners still unregister after pending work; terminal/tooltip/settings owners still register only while dirty, visible, pending, or queued; dev dashboards remain editor/development-only.
Rejected Alternatives: Touching already dirty files owned by other agents, changing localization contracts, replacing terminal typing with managed strings, coroutine timers, direct Unity `Update()`, or retaining `GlobalRegistry.Updatables.Contains` as a registration proof.
Scalability potential: Low/toaster path removes ten more Update-lane UI owners and keeps one-shot localization/layout work out of the hot Update fan-out. Middle keeps current visual cadence. High/Ultra can spend recovered scheduler headroom on richer tooltip/panel and terminal effects without changing text-buffer laws. Development builds also get the debug dashboards out of Update, reducing profiler-noise while inspecting frame health.
Hardware Impact: Measured gain is 0 us because Unity profiler proof is unavailable. Static impact on i3/MX350: ten dispatcher Update slots removed this pass, twenty-four scoped UI Update-lane owners purged/neutralized across all passes, one-shot localization/layout registration no longer probes `GlobalRegistry.Updatables`, and terminal/tooltip effects stay active-only in VISUAL_SYNC.
Cinematic Cheats Used: active-only late-frame registration, fixed-cadence terminal character reveal, cached dispatcher delta, dirty/queued visual ownership, one-shot layout pass deferral, and existing TMP `SetCharArray` buffers.
Regression Model: CPU risk decreases through less Update-lane fan-out; GC risk remains unchanged because no hot-path allocation APIs were introduced; layout cadence risk is limited to a one-phase shift into VISUAL_SYNC for event-driven changes; terminal/tooltip visual correctness is preserved because timers still use dispatcher frame delta.
Failure Modes: Late-frame delta must be available for these callbacks, which is an established `SystemDispatcher` contract already used by prior UI pass files. Localization layout changes may be visible one visual phase later than before, but they are event-driven and unregister immediately after applying. Compile verification is blocked upstream, not by touched UI diagnostics.
Verification: Constrained static scans over the ten edited files found no `ITickable`, `IUpdatable`, `TryRegisterUpdatable`, public `Tick`, LINQ, `string.Format`, interpolation `$"`, `SetText`, `.text =`, `Quaternion.Slerp`, or `math.slerp`. `git diff --check` reported only CRLF normalization warnings. Filtered `dotnet build Hecton8.Core.csproj --no-restore --disable-build-servers --nologo -v:minimal -m:1 /nr:false /p:BuildInParallel=false /p:UseSharedCompilation=false` returned only non-UX errors: `FaunaKinematicsRuntime.cs(242,17)` missing `LeviathanTerrainIkJob.TailWhipDurationSeconds` and `PlayerCriticalProceduralAudioRenderer.cs(10002,31)` missing `PrologueSplashdownSineSweepProbeJob`.
