# Rationale_UI_DIEGETIC_INPUT

STATUS: PENDING VERIFICATION

## Decision 0 - Domain and Mandate Intake
Problem: The task spans diegetic terminal input, hand IK, input abstraction, haptics, shaders, and zero-GC UI. Blind implementation would create cross-domain concrete dependencies.
Solution: Bound ownership to ECHELON 8 Diegetic Terminals and integrate outward through existing interfaces, GlobalRegistry, or EventBus-style bridges after codebase verification. Loaded eight mandates before coding.
Rejected Alternatives: Direct dependencies on animation/audio/combat concrete classes were rejected because concurrent agents may be rewriting those systems and AGENTS.md forbids cross-domain concrete coupling.
Scalability potential: Low uses single-panel, fixed buffers, NonAlloc raycast, simple highlight/CRT math. Middle raises update cadence. High/Ultra spend saved CPU/GPU on richer scanlines, glare, hand pose fidelity, and higher RT polish.
Hardware Impact: Estimated low-end i3/MX350 gain versus EventSystem/GraphicRaycaster path is 80-250 us per active terminal frame, pending Unity profiler proof.

## Decision 1 - Initial State Files
Problem: Anti-amnesia state did not exist for this agent ID.
Solution: Created `Status_UI_DIEGETIC_INPUT.md` and `Rationale_UI_DIEGETIC_INPUT.md` before runtime code edits.
Rejected Alternatives: Chat-only state was rejected because compression will destroy context.
Scalability potential: Disk state allows loop-by-loop continuation without loading neighboring prompts.
Hardware Impact: Runtime impact 0 us; workflow-only.

## Decision 2 - Loop 1 Projection and PAL
Problem: Terminal input needed 3D screen hits without Unity UI pointer routing, while scroll-wheel dials still had to stay behind the PAL.
Solution: Kept `DiegeticPanelController.TryProjectRayToPanel` on pure plane math and `math.rcp`, capped effective reach to 2 m, added `PlayerInputState.ScrollDelta`, and forwarded cached UI scroll into `DiegeticPanelInputEvent.AnalogDelta`.
Rejected Alternatives: `EventSystem.current.IsPointerOverGameObject`, `GraphicRaycaster`, per-frame `InputAction` string lookup, and physics raycast cursor paths were rejected as slower and allocation-risk paths.
Scalability potential: Low uses one ray-plane solve and fixed event structs. Middle enables keyboard+dial on one terminal. High/Ultra can spend saved CPU on richer CRT damage/glare and higher RT polish without changing input architecture.
Hardware Impact: Estimated low-end i3/MX350 savings versus EventSystem/GraphicRaycaster cursor path remain 80-250 us per active terminal frame; scroll forwarding adds one cached action read per dispatcher frame.

## Decision 3 - Damage and Glare Visual Cheats
Problem: Damage and flashlight readability needed visible feedback without coupling panel runtime to combat or light systems.
Solution: Implemented `IDamageReceiver.ReceiveDamage`, `TriggerDamageGlitch`, and `SetFlashlightGlare`, then drove `_TerminalDamageGlitch` and `_FlashlightGlare` shader floats. Shader uses triangle pulses, hash bands, scanline alpha, and UV shifts; no added texture sample.
Rejected Alternatives: Rigidbody screen shake, secondary camera jitter, material instantiation, and combat-runtime registration were rejected because they add cross-domain ownership and unstable frame cost.
Scalability potential: Low uses one hashed horizontal UV offset and dim glare. Middle keeps scanlines and spark bands. High/Ultra can raise intensity, RT resolution, and phosphor persistence through existing material/panel settings.
Hardware Impact: Estimated shader cost is a few ALU ops when active; avoids 0.05-0.2 ms CPU camera/UI rebuild paths on MX350-tier hardware, pending Unity GPU proof.

## Decision 4 - Compile Wall Status
Problem: Full solution compile is blocked by repository state outside this prompt: missing NuGet assets/Unity.Sdk package projects plus unrelated `PredatorCognitionDomain` and `VoxelDeltaProcessor` errors.
Solution: Ran narrower verification. `Hecton8.Input.csproj` and `Assembly-CSharp.csproj` build clean. `Hecton8.Core.csproj` still fails on non-UI files before any UI_DIEGETIC_INPUT errors appear.
Rejected Alternatives: Editing Fauna/Voxel/package projects was rejected as architectural trespass outside ECHELON 8 Presentation & UX.
Scalability potential: Verification can become full Unity compile once integrator clears external blockers; current code remains isolated behind existing core contracts.
Hardware Impact: Runtime impact 0 us; verification-only dependency wall.

## Decision 5 - Loop 2 Interaction Feedback and Physical Gates
Problem: Physical terminal feedback needed haptics, stencil containment, CRT polish, reach enforcement, and mechanical switch dragging without turning terminals into standard Unity UI.
Solution: Reused existing `ToolHapticsRuntime.EnqueueSinusoidalCommand` bridges in keyboard, kinematic terminal bridge, buttons, and snap switches. Kept stencil and CRT curvature/scanlines in `Hecton_DiegeticPanelUnlit.shader`. Changed panel reach resolution to hard-cap at 2 m using AUP distance. Verified `PhysicalSnapSwitch.TryQueueHandPress` resolves desired state from hand-local position and publishes through the interaction signal queue.
Rejected Alternatives: Unity UI button callbacks, real light components, physics ray cursor hits, and per-switch Update polling were rejected because they add allocations, unstable frame cost, or cross-domain coupling.
Scalability potential: Low runs fixed haptic commands, a single stencil pass, and cheap CRT ALU. Middle adds damage/glare bands. High/Ultra can increase render texture fidelity and screen effects while the interaction loop remains fixed.
Hardware Impact: Haptic/audio/switch paths stay queue-driven; expected MX350-tier CPU gain versus EventSystem plus live Light feedback is 100-300 us per active cockpit panel cluster, pending profiler proof.

## Decision 6 - Loop 3 Dials, Tooltips, Hand Hover, Audio
Problem: Terminals needed analog dials, damage feedback, tooltips, hand proximity, and mechanical audio without introducing per-widget allocations or hard dependencies on audio/animation implementations.
Solution: Added `PhysicalPanelDial` with scroll-to-local-rotation mapping from `DiegeticPanelInputEvent.AnalogDelta`. Kept damage response on `IDamageReceiver`. Verified `DiegeticTooltipSystem` uses fixed glyph buffers and `ReadOnlySpan<char>`. Kept hand hover through `KinematicTerminalInteractionBridge.DispatchHandIkTarget`. Added `IAudioService.QueueAudioEvent` hooks for keyboard presses, dial ticks, and snap switches.
Rejected Alternatives: TextMeshPro string assignment, UI sliders, hard-coded audio clip playback for every widget, and direct FABRIK component calls were rejected as allocation/coupling risks.
Scalability potential: Low only rotates knobs on scroll and queues one audio token. Middle adds haptics/tooltips. High/Ultra can assign richer audio event ids and higher-fidelity CRT visuals while retaining the same fixed payloads.
Hardware Impact: Queue audio events are 32-byte blittable payloads; expected gain versus ad-hoc AudioSource spawning is 50-300 us and zero transient managed garbage per interaction.

## Decision 7 - Loop 4 Final UI Gates
Problem: The remaining tasks were mostly verification gates: avoid slow pointer APIs, keep branchless highlights, prove rolling boot text, expose flashlight glare, and ensure new assets have metadata.
Solution: Confirmed no `EventSystem.current.IsPointerOverGameObject` use in the terminal path and `GraphicRaycaster` is disabled. Retained `math.select` for keyboard highlight state. Verified `HectonSubmarineOsDisplay` renders a rolling char-buffer log with `Span<char>.TryFormat` and `SetCharArray`. Added `.meta` for `PhysicalPanelDial` and confirmed edited terminal files have no Cyrillic characters.
Rejected Alternatives: Cleaning unrelated project-wide Cyrillic comments/tooltips was rejected because many files outside this prompt contain legacy localized comments and changing them would be unbounded churn.
Scalability potential: Low uses fixed buffers and disabled raycasters. Middle uses authored event ids and glare controls. High/Ultra can raise monitor visuals without changing data paths.
Hardware Impact: Avoiding EventSystem pointer tests and string boot-log rebuilds preserves the 80-250 us per active terminal-frame budget on MX350-tier hardware; exact profiler proof remains pending.

## Decision 8 - Self-Inquisition Repairs
Problem: Reading the code after checklist closure found two integration gaps: bridge-dispatched panel events did not forward scroll analog data, and physical panel buttons still preferred clip playback over the NativeQueue audio token path.
Solution: Added `TerminalActionFlags.Scroll`, forwarded `PlayerInputState.ScrollDelta` through `KinematicTerminalInteractionBridge`, ORed `DiegeticPanelInputEventType.Scroll`, and made `PhysicalPanelButton` prefer `IAudioService.QueueAudioEvent` when an authored event id exists.
Rejected Alternatives: Depending only on `DiegeticPanelController` dispatch for dials was rejected because some terminals route through the kinematic bridge. Leaving panel buttons on clip-only playback was rejected because Task 15 explicitly requires NativeQueue audio routing.
Scalability potential: Low still sends one fixed event struct and one 32-byte audio token. Middle uses existing clip fallback for legacy buttons. High/Ultra can author richer mechanical event tables without changing code.
Hardware Impact: Fix preserves zero-GC input and replaces potential AudioSource pool work with a queued payload when authored; expected interaction gain remains 50-300 us per press versus ad-hoc clip paths.

## OMEGA POLISH CHANGES
Problem: Final anti-bloat audit found remaining honest math and validation ambiguity after task closure.
Solution: Replaced remaining hot or near-hot divisions with `math.rcp` in panel pixel-basis math, snap-switch blend math, and `FastDecayBlend`. Replaced `PhysicalPanelDial`'s `quaternion.AxisAngle` with the same no-trig approximate axis rotation pattern used by `PhysicalSnapSwitch`. Re-ran `dotnet build Hecton8.Core.csproj` as required; it fails only on external Construction/Physics symbols.
Rejected Alternatives: Leaving `AxisAngle` was rejected because dial rotation is visual and can use a cinematic approximation. Fixing `TransitionHatchMeshState`/`Hecton8.Physics.SyncTransforms` was rejected as non-domain compile trespass.
Scalability potential: Low path uses 2 m AUP gating, fixed-size input/audio payloads, approximate axis rotation, and low bridge cadence. Middle enables haptics, tooltips, boot log, glare, and CRT scanlines. High/Ultra can spend saved cycles on larger RTs, stronger shader bands, phosphor persistence, authored audio tables, and higher-fidelity hand snap targets without changing hot data flow.
Hardware Impact: Honest calculations replaced: screen UV divisions -> reciprocal multiplication; shader screen/depth divisions -> `rcp`; dial `AxisAngle` trig -> polynomial sin/cos fake; snap switch blend division -> reciprocal multiply; decay division -> reciprocal multiply. Estimated low-end gain remains 80-250 us per active terminal frame versus EventSystem/GraphicRaycaster/input-field/audio-source paths, plus 50-300 us per mechanical interaction when NativeQueue audio ids are authored.
Build Health: `dotnet build C:\hades\Hecton8\Hecton8.Core.csproj --no-restore -p:BuildProjectReferences=false` fails in `HabitatGraphManager.cs` and `ConstructionManager.cs`; no errors reference UI_DIEGETIC_INPUT files. `Assembly-CSharp.csproj` and `Hecton8.Input.csproj` pass with 0 warnings after final edits.
Final Git Diff: modified `Hecton_DiegeticPanelUnlit.shader`, `InputDispatcher.cs`, `PlayerInputState.cs`, `InputManager.cs`, `PhysicalSnapSwitch.cs`, `DiegeticPanelController.cs`, `PhysicalPanelButton.cs`; added `PhysicalPanelDial.cs` and `.meta`; updated `Status_UI_DIEGETIC_INPUT.md` and `Rationale_UI_DIEGETIC_INPUT.md`.
